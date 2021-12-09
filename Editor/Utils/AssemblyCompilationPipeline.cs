using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.ProjectAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
#if UNITY_2018_2_OR_NEWER
using UnityEditor.Build.Player;

#endif

namespace Unity.ProjectAuditor.Editor.Utils
{
    enum CodeOptimization
    {
        Debug,
        Release
    }

    enum CompilerMessageType
    {
        /// <summary>
        ///   <para>Error message.</para>
        /// </summary>
        Error,
        /// <summary>
        ///   <para>Warning message.</para>
        /// </summary>
        Warning,
        /// <summary>
        ///   <para>Info message.</para>
        /// </summary>
        Info
    }

    class CompilerMessage
    {
        /// <summary>
        ///   <para>Message code.</para>
        /// </summary>
        public string code;
        /// <summary>
        ///   <para>Message type.</para>
        /// </summary>
        public CompilerMessageType type;
        /// <summary>
        ///   <para>Message body.</para>
        /// </summary>
        public string message;
        /// <summary>
        ///   <para>File for the message.</para>
        /// </summary>
        public string file;
        /// <summary>
        ///   <para>File line for the message.</para>
        /// </summary>
        public int line;
    }

    class AssemblyCompilationUnit
    {
        public AssemblyBuilder builder;
        public AssemblyCompilationUnit[] dependencies;
        public CompilerMessage[] messages;

        bool m_Done = false;

        public string assemblyPath
        {
            get
            {
                return builder.assemblyPath;
            }
        }

        public bool IsDone()
        {
            return m_Done;
        }

        public void Update()
        {
            switch (builder.status)
            {
                case AssemblyBuilderStatus.NotStarted:
                    if (dependencies.All(dep => dep.IsDone()))
                    {
                        if (dependencies.All(dep => dep.Success()))
                            builder.Build(); // all references are built, we can kick off this builder
                        else
                            m_Done = true; // this assembly won't be built since it's missing dependencies
                    }
                    break;
                case AssemblyBuilderStatus.IsCompiling:
                    return; // nothing to do
                case AssemblyBuilderStatus.Finished:
                    m_Done = true;
                    break;
            }
        }

        public bool Success()
        {
            if (messages == null)
                return false;
            return messages.All(message => message.type != CompilerMessageType.Error);
        }
    }

    class AssemblyCompilationPipeline : IDisposable
    {
        string m_OutputFolder = string.Empty;

        Dictionary<string, AssemblyCompilationUnit> m_AssemblyCompilationUnits;
#if UNITY_2020_2_OR_NEWER
        string[] m_RoslynAnalyzers;
#endif

        public Action<string, CompilerMessage[]> AssemblyCompilationFinished;

        public static CodeOptimization CodeOptimization = CodeOptimization.Release;

        public AssemblyCompilationPipeline()
        {
#if UNITY_2020_2_OR_NEWER
            m_RoslynAnalyzers = AssetDatabase.FindAssets("l:RoslynAnalyzer").Select(AssetDatabase.GUIDToAssetPath).ToArray();
#endif
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(m_OutputFolder) && Directory.Exists(m_OutputFolder))
            {
                foreach (var unit in m_AssemblyCompilationUnits.Select(pair => pair.Value).Where(u => u.Success()))
                {
                    File.Delete(unit.assemblyPath);
                    File.Delete(Path.ChangeExtension(unit.assemblyPath, ".pdb"));
                }

                m_AssemblyCompilationUnits.Clear();

                // We can't delete the folder because of the CompilationLog.txt created by the AssemblyBuilder compilationTask
                //Directory.Delete(m_OutputFolder, true);
            }
            m_OutputFolder = string.Empty;
        }

        public IEnumerable<AssemblyInfo> Compile(bool editorAssemblies = false, IProgress progress = null)
        {
#if UNITY_2019_3_OR_NEWER
            var assemblies = CompilationPipeline.GetAssemblies(editorAssemblies ? AssembliesType.Editor : AssembliesType.PlayerWithoutTestAssemblies);
#elif UNITY_2018_1_OR_NEWER
            var assemblies = CompilationPipeline.GetAssemblies(editorAssemblies ? AssembliesType.Editor : AssembliesType.Player);
#else
            var assemblies = CompilationPipeline.GetAssemblies();
#endif

            IEnumerable<string> compiledAssemblyPaths;
#if UNITY_2018_2_OR_NEWER
            if (editorAssemblies)
                compiledAssemblyPaths = CompileEditorAssemblies(assemblies);
            else
                compiledAssemblyPaths = CompileAssemblies(assemblies, progress);
#else
            // fallback to CompilationPipeline assemblies
            compiledAssemblyPaths = CompileEditorAssemblies(assemblies, !editorAssemblies);
#endif

            var assemblyInfos = new List<AssemblyInfo>();
            foreach (var compiledAssemblyPath in compiledAssemblyPaths)
            {
                var assemblyInfo = AssemblyInfoProvider.GetAssemblyInfoFromAssemblyPath(compiledAssemblyPath);
                var assembly = assemblies.First(a => a.name.Equals(assemblyInfo.name));
                var sourcePaths = assembly.sourceFiles.Select(file => file.Remove(0, assemblyInfo.relativePath.Length + 1));

                assemblyInfo.sourcePaths = sourcePaths.ToArray();
                assemblyInfos.Add(assemblyInfo);
            }

            return assemblyInfos;
        }

        IEnumerable<string> CompileEditorAssemblies(IEnumerable<Assembly> assemblies)
        {
            // exclude Editor-Only Assemblies
            assemblies = assemblies.Where(a => a.flags != AssemblyFlags.EditorAssembly);
            return assemblies.Select(assembly => assembly.outputPath);
        }

#if UNITY_2018_2_OR_NEWER
        IEnumerable<string> CompileAssemblies(Assembly[] assemblies, IProgress progress = null)
        {
            if (progress != null)
            {
                var numAssemblies = assemblies.Length;
                progress.Start("Assembly Compilation", "Compiling project scripts",
                    numAssemblies);
            }

            m_OutputFolder = FileUtil.GetUniqueTempPathInProject();

            if (!Directory.Exists(m_OutputFolder))
                Directory.CreateDirectory(m_OutputFolder);

            PrepareAssemblyBuilders(assemblies, (assemblyPath, messages) =>
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
                m_AssemblyCompilationUnits[assemblyName].messages = messages;

                if (progress != null)
                    progress.Advance(assemblyName);

                if (AssemblyCompilationFinished != null)
                    AssemblyCompilationFinished(assemblyName, messages);
            });
            UpdateAssemblyBuilders();

            if (progress != null)
                progress.Clear();

            return m_AssemblyCompilationUnits.Where(pair => pair.Value.Success()).Select(unit => unit.Value.assemblyPath);
        }

        void PrepareAssemblyBuilders(Assembly[] assemblies, Action<string, CompilerMessage[]> assemblyCompilationFinished)
        {
            var editorAssemblies = false; // for future use
            m_AssemblyCompilationUnits = new Dictionary<string, AssemblyCompilationUnit>();
            // first pass: create all AssemblyCompilationUnits
            foreach (var assembly in assemblies)
            {
                var filename = Path.GetFileName(assembly.outputPath);
                var assemblyName = Path.GetFileNameWithoutExtension(assembly.outputPath);
                var assemblyPath = Path.Combine(m_OutputFolder, filename);
                var assemblyBuilder = new AssemblyBuilder(assemblyPath, assembly.sourceFiles);

                assemblyBuilder.buildFinished += (path, originalMessages) =>
                {
                    var messages = new CompilerMessage[originalMessages.Length];
                    for (int i = 0; i < originalMessages.Length; i++)
                    {
                        messages[i] = ParseCompilerMessage(originalMessages[i]);
                    }

                    assemblyCompilationFinished(path, messages);
                };
#if UNITY_2020_2_OR_NEWER
                assemblyBuilder.compilerOptions = new ScriptCompilerOptions
                {
                    AdditionalCompilerArguments = assembly.compilerOptions.AdditionalCompilerArguments,
                    AllowUnsafeCode = assembly.compilerOptions.AllowUnsafeCode,
                    ApiCompatibilityLevel = assembly.compilerOptions.ApiCompatibilityLevel,
                    CodeOptimization = CodeOptimization == CodeOptimization.Release ? UnityEditor.Compilation.CodeOptimization.Release : UnityEditor.Compilation.CodeOptimization.Debug, // assembly.compilerOptions.CodeOptimization,
                    RoslynAnalyzerDllPaths = m_RoslynAnalyzers
                };
#else
                assemblyBuilder.compilerOptions = assembly.compilerOptions;
#endif
                assemblyBuilder.flags = editorAssemblies ? AssemblyBuilderFlags.EditorAssembly : AssemblyBuilderFlags.DevelopmentBuild;

                // add asmdef-specific defines
                var additionalDefines = new List<string>(assembly.defines.Except(assemblyBuilder.defaultDefines));

                additionalDefines.Add("ENABLE_UNITY_COLLECTIONS_CHECKS");
                assemblyBuilder.additionalDefines = additionalDefines.ToArray();

                // add references to target assemblies
                var additionalReferences = assembly.assemblyReferences.Select(r => Path.Combine(m_OutputFolder, Path.GetFileName(r.outputPath))).ToList();
                // add missing references
                additionalReferences.AddRange(assembly.allReferences.Except(assemblyBuilder.defaultReferences));
                assemblyBuilder.additionalReferences = additionalReferences.ToArray();

                // exclude all assemblies that we are building ourselves to a Temp folder
                var excludeReferences = assemblyBuilder.defaultReferences.Where(r => r.StartsWith("Library")).ToList();
                if (!editorAssemblies)
                    excludeReferences.AddRange(assemblyBuilder.defaultReferences.Where(r => r.EndsWith("UnityEngine.dll")));
                assemblyBuilder.excludeReferences = excludeReferences.ToArray();

#if UNITY_2019_1_OR_NEWER
                assemblyBuilder.referencesOptions = ReferencesOptions.UseEngineModules;
#endif
                m_AssemblyCompilationUnits.Add(assemblyName, new AssemblyCompilationUnit { builder = assemblyBuilder });
            }

            // second pass: find all assembly reference builders
            foreach (var assembly in assemblies)
            {
                var dependencies = new List<AssemblyCompilationUnit>();
                foreach (var referenceName in assembly.assemblyReferences.Select(r => Path.GetFileNameWithoutExtension(r.outputPath)))
                {
                    dependencies.Add(m_AssemblyCompilationUnits[referenceName]);
                }

                m_AssemblyCompilationUnits[Path.GetFileName(assembly.name)].dependencies =
                    dependencies.ToArray();
            }
        }

        internal static CompilerMessage ParseCompilerMessage(UnityEditor.Compilation.CompilerMessage originalMessage)
        {
            var messageBody = originalMessage.message; // as a fallback, use the original message
            var messageCode = "Unknown";
            var messageType = CompilerMessageType.Error;

            // try parsing the message
            var messageStartIndex = originalMessage.message.LastIndexOf("):");
            if (messageStartIndex != -1)
            {
                var messageWithCode = originalMessage.message.Substring(messageStartIndex + 2);
                var messageParts = messageWithCode.Split(new[] {' ', ':'}, 2,
                    StringSplitOptions.RemoveEmptyEntries);
                if (messageParts.Length > 1)
                {
                    // if possible we read the compiler message type from the message because CompilerMessageType does not support CompilerMessageType.Info in 2020.x
                    switch (messageParts[0])
                    {
                        case "error":
                            messageType = CompilerMessageType.Error;
                            break;
                        case "warning":
                            messageType = CompilerMessageType.Warning;
                            break;
                        case "info":
                            messageType = CompilerMessageType.Info;
                            break;
                    }

                    if (messageParts.Length > 1 && messageParts[1].IndexOf(':') != -1)
                    {
                        messageParts = messageParts[1].Split(new[] {' ', ':'}, 2,
                            StringSplitOptions.RemoveEmptyEntries);
                        if (messageParts.Length > 1)
                        {
                            messageBody = messageParts[1];
                            messageCode = messageParts[0];
                        }
                    }
                }
            }

            var message = new CompilerMessage
            {
                message = messageBody,
                file = originalMessage.file,
                line = originalMessage.line,
                code = messageCode,
                type = messageType
            };

            return message;
        }

        void UpdateAssemblyBuilders()
        {
            while (true)
            {
                var pendingUnits = m_AssemblyCompilationUnits.Select(pair => pair.Value).Where(unit => !unit.IsDone());
                if (!pendingUnits.Any())
                    break;
                foreach (var unit in pendingUnits)
                {
                    unit.Update();
                }
                System.Threading.Thread.Sleep(10);
            }
        }

#endif
        public IEnumerable<string> GetCompiledAssemblyDirectories()
        {
#if UNITY_2018_2_OR_NEWER
            yield return m_OutputFolder;
#else
            foreach (var dir in CompilationPipeline.GetAssemblies()
                     .Where(a => a.flags != AssemblyFlags.EditorAssembly).Select(assembly => Path.GetDirectoryName(assembly.outputPath)).Distinct())
            {
                yield return dir;
            }
#endif
        }
    }
}
