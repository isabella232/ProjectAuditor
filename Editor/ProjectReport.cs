using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEditor.Macros;

namespace Editor
{
    public class ProjectReport
    {
        private Assembly[] m_PlayerAssemblies;
        
        private DefinitionDatabase m_ApiCalls;
        private DefinitionDatabase m_ProjectSettings;

        public List<ProjectIssue> m_ProjectIssues = new List<ProjectIssue>();

        public ProjectReport()
        {
            m_ApiCalls = new DefinitionDatabase("ApiDatabase");
            m_ProjectSettings = new DefinitionDatabase("ProjectSettings");
            
            m_PlayerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);
        }
        
        public void Create()
        {
            m_ProjectIssues.Clear();
            AnalyzeApiCalls(m_ApiCalls.m_Definitions);
            AnalyzeProjectSettings(m_ProjectSettings.m_Definitions);
        }                

        public void AnalyzeApiCalls(List<ProblemDefinition> problemDefinitions)
        {
            Debug.Log("Analyzing Scripts...");

            // Analyse all Player assemblies, including Package assemblies.
            foreach (var playerAssembly in m_PlayerAssemblies)
            {
                string assemblyPath = playerAssembly.outputPath;
                if (!File.Exists(assemblyPath))
                {
                    Debug.LogError($"{assemblyPath} not found.");
                    return;
                }

                using (var a = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters() { ReadSymbols = true} ))
                {
        //            var callInstructions = a.MainModule.Types.SelectMany(t => t.Methods)
        //                .Where(m => m.HasBody)
        //                .SelectMany(m => m.Body.Instructions)
        //                .Where(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Call);
        //
        //            var myProblems = problemDefinitions
        //                .Where(problem =>
        //                    callInstructions.Any(ci => ((MethodReference) ci.Operand).DeclaringType.Name == problem.type.Name))
        //                .Select(p => new Issue {def = p});
        //
        //            issues.AddRange(myProblems);
    
                    foreach (var m in a.MainModule.Types.SelectMany(t => t.Methods))
                    {
                        if (!m.HasBody)
                            continue;
                
                        foreach (var inst in m.Body.Instructions.Where(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)))
                        {
                            var calledMethod = ((MethodReference) inst.Operand);

                            // HACK: need to figure out a way to know whether a method is actually a property
                            var p = problemDefinitions.SingleOrDefault(c => c.type == calledMethod.DeclaringType.Name && (c.method == calledMethod.Name || ("get_" + c.method) == calledMethod.Name));

                            if (p != null && m.DebugInformation.HasSequencePoints)
                            {
                                var msg = string.Empty;
                                SequencePoint s = null;
                                for (var i = inst; i != null; i = i.Previous)
                                {
                                    s = m.DebugInformation.GetSequencePoint(i);
                                    if (s != null)
                                    {
                                        msg = i == inst ? " exactly" : "nearby";
                                        break;
                                    }
                                }
                
                                if (s != null)
                                {
                                    m_ProjectIssues.Add(new ProjectIssue
                                    {
                                        category = "API Call",
                                        def = p,
                                        url = s.Document.Url,
                                        line = s.StartLine,
                                        column = s.StartColumn
                                    });
                                }
                
                            }
                        }
                    }
                }                
            }
            

        }

        void SearchAndEval(ProblemDefinition p, System.Reflection.Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                try
                {
                    var value = MethodEvaluator.Eval(assembly.Location,
                        p.type, "get_" + p.method, new System.Type[0]{}, new object[0]{});

                    if (value.ToString() == p.value)
                    {
                        m_ProjectIssues.Add(new ProjectIssue
                        {
                            category = "ProjectSettings",
                            def = p
                        });
                        
                        // stop iterating assemblies
                        break;
                    }
                }
                catch (Exception e)
                {
                    // TODO
                }
            }

            
        }
        
        public void AnalyzeProjectSettings(List<ProblemDefinition> problemDefinitions)
        {
            Debug.Log("Analyzing Project Settings...");
//            string [] assemblyNames = new string[]{"UnityEditor.dll", "UnityEngine.dll", "UnityEditor.WebGL.Extensions.dll"}; 
//            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => assemblyNames.Contains(x.ManifestModule.Name));
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var p in problemDefinitions)
            {
                SearchAndEval(p, assemblies);
            }
        }

        public void WriteToFile()
        {
            string json = JsonHelper.ToJson<ProjectIssue>(m_ProjectIssues.ToArray(), true);
            File.WriteAllText("Report.json", json);
        }
    }
}