using System;
using System.Collections.Generic;
using System.Linq;
using Unity.ProjectAuditor.Editor.Auditors;
using Unity.ProjectAuditor.Editor.CodeAnalysis;
using Unity.ProjectAuditor.Editor.UI.Framework;
using Unity.ProjectAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor.UI
{
    public class CodeView : DiagnosticView
    {
        ChartData[] m_GroupByFilename;
        ChartData[] m_GroupByType;

        public CodeView(ViewManager viewManager) : base(viewManager)
        {
        }

        public override void AddIssues(IEnumerable<ProjectIssue> allIssues)
        {
            base.AddIssues(allIssues);
            if (m_Desc.category == IssueCategory.Code)
            {
                var usersIssues = m_Issues.Where(
                    i => !AssemblyInfoProvider.IsReadOnlyAssembly(i.GetCustomProperty(CodeProperty.Assembly)));
                var list = usersIssues.GroupBy(i => i.filename).Select(g => new ChartData
                {
                    label = g.Key,
                    value = g.Count(),
                }).ToList();
                list.Sort((a, b) => b.value.CompareTo(a.value));
                m_GroupByFilename = list.ToArray();

                list = usersIssues.GroupBy(i => i.GetCallingMethod()).Select(g => new ChartData
                {
                    label = g.Key,
                    value = g.Count(),
                }).ToList();
                list.Sort((a, b) => b.value.CompareTo(a.value));
                m_GroupByType = list.ToArray();
            }
        }

        protected override void OnDrawInfo()
        {
            EditorGUILayout.LabelField("- Use the Filters to reduce the number of reported issues");
            EditorGUILayout.LabelField("- Use the Mute button to mark an issue as false-positive");

            if (NumCompilationErrors() > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Utility.ErrorIcon, GUILayout.MaxWidth(36));
                EditorGUILayout.LabelField(new GUIContent("Code Analysis is incomplete due to compilation errors"), GUILayout.Width(330), GUILayout.ExpandWidth(false));
                if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(50)))
                    m_ViewManager.ChangeView(IssueCategory.CodeCompilerMessage);
                EditorGUILayout.EndHorizontal();
            }

            DrawBarChart("Top user's files with most issues", new Color(0.0f, 0.6f, 0.6f), m_GroupByFilename);
            //DrawStats("Top types with most issues", m_GroupByType);
        }

        static int NumCompilationErrors()
        {
            var compilerMessages = s_Report.GetIssues(IssueCategory.CodeCompilerMessage);
            return compilerMessages.Count(i => i.severity == Rule.Severity.Error);
        }
    }
}
