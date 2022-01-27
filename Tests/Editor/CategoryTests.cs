using NUnit.Framework;
using Unity.ProjectAuditor.Editor;

namespace Unity.ProjectAuditor.EditorTests
{
    class CategoryTests
    {
        [Test]
        public void Category_CustomCategory_IsRegistered()
        {
            const string categoryName = "ThisIsATest";
            var category = ProjectAuditor.Editor.ProjectAuditor.GetOrRegisterCategory(categoryName);

            Assert.AreEqual(IssueCategory.FirstCustomCategory, category);
        }
    }
}
