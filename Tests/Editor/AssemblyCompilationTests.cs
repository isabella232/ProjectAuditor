using System;
using System.Linq;
using NUnit.Framework;
using Unity.ProjectAuditor.Editor;
using Unity.ProjectAuditor.Editor.Utils;
using UnityEngine;

namespace UnityEditor.ProjectAuditor.EditorTests
{
    class AssemblyCompilationTests
    {
        [Test]
        public void AssemblyCompilation_DefaultAssembly_IsCompiled()
        {
            using (var compilationHelper = new AssemblyCompilationPipeline())
            {
                var assemblyInfos = compilationHelper.Compile();

                Assert.Positive(assemblyInfos.Count());
                Assert.NotNull(assemblyInfos.FirstOrDefault(info => info.name.Equals(AssemblyInfo.DefaultAssemblyName)));
            }
        }
    }
}
