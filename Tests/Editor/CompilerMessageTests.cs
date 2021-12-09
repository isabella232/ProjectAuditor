using NUnit.Framework;
using Unity.ProjectAuditor.Editor.Utils;
using CompilerMessageType = UnityEditor.Compilation.CompilerMessageType;

namespace UnityEditor.ProjectAuditor.EditorTests
{
    class CompilerMessageTests
    {
        [Test]
        public void CompilerMessageErrorCanBeParsed()
        {
            var message = AssemblyCompilationPipeline.ParseCompilerMessage(new UnityEditor.Compilation.CompilerMessage
            {
                message = "Assets/Test.cs(14,0): error CS1519: Invalid token 'int' in class, struct, or interface member declaration",
                type = CompilerMessageType.Error,
                file = "Assets/Test.cs",
                line = 14
            });

            Assert.True(message.message.Equals("Invalid token 'int' in class, struct, or interface member declaration"));
            Assert.True(message.code.Equals("CS1519"));
            Assert.True(message.file.Equals("Assets/Test.cs"));
            Assert.AreEqual(Unity.ProjectAuditor.Editor.Utils.CompilerMessageType.Error, message.type);
            Assert.AreEqual(14, message.line);
        }

        [Test]
        public void CompilerMessageInfoCanBeParsed()
        {
            var message = AssemblyCompilationPipeline.ParseCompilerMessage(new UnityEditor.Compilation.CompilerMessage
            {
                message = "Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/Raycasters/PhysicsRaycaster.cs(49,24): info UNT0007: Unity objects should not use null coalescing.",
                type = CompilerMessageType.Error,
                file = "Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/Raycasters/PhysicsRaycaster.cs",
                line = 49,
                column = 24 // not parsed
            });

            Assert.True(message.message.Equals("Unity objects should not use null coalescing."));
            Assert.True(message.code.Equals("UNT0007"));
            Assert.True(message.file.Equals("Library/PackageCache/com.unity.ugui@1.0.0/Runtime/EventSystem/Raycasters/PhysicsRaycaster.cs"));
            Assert.AreEqual(Unity.ProjectAuditor.Editor.Utils.CompilerMessageType.Info, message.type);
            Assert.AreEqual(49, message.line);
        }

        [Test]
        public void CompilerMessageCannotBeParsed()
        {
            var messageBody =
                "Microsoft (R) Visual C# Compiler version 3.5.0-dev-20359-01 (8da8ba0c)\nCopyright (C) Microsoft Corporation. All rights reserved.\n\nerror CS0006: Metadata file 'Library/ScriptAssemblies/UnityEngine.Purchasing.AppleMacos.dll' could not be found\nerror CS0006: Metadata file 'Library/ScriptAssemblies/UnityEngine.Purchasing.Security.dll' could not be found";
            var message = AssemblyCompilationPipeline.ParseCompilerMessage(new UnityEditor.Compilation.CompilerMessage
            {
                message = messageBody,
                type = CompilerMessageType.Error,
                file = "",
                line = 0,
                column = 0 // not parsed
            });

            Assert.True(message.message.Equals(messageBody));
            Assert.True(message.code.Equals("Unknown"));
            Assert.AreEqual(Unity.ProjectAuditor.Editor.Utils.CompilerMessageType.Error, message.type);
        }
    }
}
