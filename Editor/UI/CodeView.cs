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
        readonly Draw2D m_2D;

        struct GroupStats
        {
            public string name;
            public int count;
        }

        GroupStats[] m_GroupByFilename;
        GroupStats[] m_GroupByType;

        public CodeView(ViewManager viewManager) : base(viewManager)
        {
            m_2D = new Draw2D("Unlit/ProjectAuditor");
        }

        public override void AddIssues(IEnumerable<ProjectIssue> allIssues)
        {
            base.AddIssues(allIssues);
            if (m_Desc.category == IssueCategory.Code)
            {
                var usersIssues = m_Issues.Where(
                    i => !AssemblyInfoProvider.IsReadOnlyAssembly(i.GetCustomProperty(CodeProperty.Assembly)));
                var list = usersIssues.GroupBy(i => i.filename).Select(g => new GroupStats
                {
                    name = g.Key,
                    count = g.Count(),
                }).ToList();
                list.Sort((a, b) => b.count.CompareTo(a.count));
                m_GroupByFilename = list.ToArray();

                list = usersIssues.GroupBy(i => i.GetCallingMethod()).Select(g => new GroupStats
                {
                    name = g.Key,
                    count = g.Count(),
                }).ToList();
                list.Sort((a, b) => b.count.CompareTo(a.count));
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

            DrawStats("Top user's files with most issues", m_GroupByFilename);
            //DrawStats("Top types with most issues", m_GroupByType);
        }

        void DrawStats(string title, GroupStats[] stats)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical();

            var width = 180;
            var barColor = new Color(0.0f, 0.6f, 0.6f);

            var maxGroupSize = (float)stats.Max(g => g.count);
            const int kMaxGroups = 10;
            for (int i = 0; i < stats.Length && i < kMaxGroups; i++)
            {
                var group = stats[i];
                var groupSize = group.count;
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(string.Format("{0} ({1}):", group.name, group.count), GUILayout.Width(300));

                var rect = EditorGUILayout.GetControlRect(GUILayout.Width(width));
                if (m_2D.DrawStart(rect))
                {
                    m_2D.DrawFilledBox(0, 1, Math.Max(1, rect.width * groupSize / maxGroupSize), rect.height - 1, barColor);
                    m_2D.DrawEnd();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }

        static int NumCompilationErrors()
        {
            var compilerMessages = s_Report.GetIssues(IssueCategory.CodeCompilerMessage);
            return compilerMessages.Count(i => i.severity == Rule.Severity.Error);
        }
    }
}
