using System.Collections.Generic;
using Unity.ProjectAuditor.Editor;
using Unity.ProjectAuditor.Editor.CodeAnalysis;
using Unity.ProjectAuditor.Editor.UI.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor.UI
{
    public class DiagnosticView : AnalysisView
    {
        public DiagnosticView(ViewManager viewManager) : base(viewManager)
        {
        }

        public override void DrawFoldouts(ProblemDescriptor[] selectedDescriptors)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LayoutSize.FoldoutWidth));

            DrawDetailsFoldout(selectedDescriptors);
            DrawRecommendationFoldout(selectedDescriptors);
            DrawActions();

            EditorGUILayout.EndVertical();
        }

        void DrawActions()
        {
            if (!m_Desc.showActions)
                return;

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = m_Desc.showMuteOptions;

            if (GUILayout.Button(Contents.MuteButton, GUILayout.ExpandWidth(true)))
            {
                var analytic = ProjectAuditorAnalytics.BeginAnalytic();
                var selectedItems = table.GetSelectedItems();
                foreach (var item in selectedItems)
                {
                    SetRuleForItem(item, Rule.Severity.None);
                }

                if (!m_Preferences.mutedIssues)
                {
                    table.SetSelection(new List<int>());
                }

                ProjectAuditorAnalytics.SendEventWithSelectionSummary(ProjectAuditorAnalytics.UIButton.Mute,
                    analytic, table.GetSelectedItems());
            }

            if (GUILayout.Button(Contents.UnmuteButton, GUILayout.ExpandWidth(true)))
            {
                var analytic = ProjectAuditorAnalytics.BeginAnalytic();
                var selectedItems = table.GetSelectedItems();
                foreach (var item in selectedItems)
                {
                    ClearRulesForItem(item);
                }

                ProjectAuditorAnalytics.SendEventWithSelectionSummary(
                    ProjectAuditorAnalytics.UIButton.Unmute, analytic, m_Table.GetSelectedItems());
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        void DrawDetailsFoldout(ProblemDescriptor[] selectedDescriptors)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.FoldoutWidth));
            m_Preferences.details = Utility.BoldFoldout(m_Preferences.details, Contents.DetailsFoldout);
            if (m_Preferences.details)
            {
                if (selectedDescriptors.Length == 0)
                    GUILayout.TextArea(k_NoSelectionText, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
                else if (selectedDescriptors.Length > 1)
                    GUILayout.TextArea(k_MultipleSelectionText, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
                else // if (selectedDescriptors.Length == 1)
                    GUILayout.TextArea(selectedDescriptors[0].problem, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
            }
            EditorGUILayout.EndVertical();
        }

        void DrawRecommendationFoldout(ProblemDescriptor[] selectedDescriptors)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(LayoutSize.FoldoutWidth));
            m_Preferences.recommendation = Utility.BoldFoldout(m_Preferences.recommendation, Contents.RecommendationFoldout);
            if (m_Preferences.recommendation)
            {
                if (selectedDescriptors.Length == 0)
                    GUILayout.TextArea(k_NoSelectionText, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
                else if (selectedDescriptors.Length > 1)
                    GUILayout.TextArea(k_MultipleSelectionText, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
                else // if (selectedDescriptors.Length == 1)
                    GUILayout.TextArea(selectedDescriptors[0].solution, SharedStyles.TextArea, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
            }
            EditorGUILayout.EndVertical();
        }

        void SetRuleForItem(IssueTableItem item, Rule.Severity ruleSeverity)
        {
            var descriptor = item.ProblemDescriptor;

            var callingMethod = "";
            Rule rule;
            if (item.hasChildren)
            {
                rule = m_Config.GetRule(descriptor);
            }
            else
            {
                callingMethod = item.ProjectIssue.GetCallingMethod();
                rule = m_Config.GetRule(descriptor, callingMethod);
            }

            if (rule == null)
                m_Config.AddRule(new Rule
                {
                    id = descriptor.id,
                    filter = callingMethod,
                    severity = ruleSeverity
                });
            else
                rule.severity = ruleSeverity;
        }

        void ClearRulesForItem(IssueTableItem item)
        {
            m_Config.ClearRules(item.ProblemDescriptor,
                item.hasChildren ? string.Empty : item.ProjectIssue.GetCallingMethod());
        }

        static class Contents
        {
            public static readonly GUIContent ActionsFoldout = new GUIContent("Actions", "Actions on selected issues");
            public static readonly GUIContent DetailsFoldout = new GUIContent("Details", "Issue Details");
            public static readonly GUIContent RecommendationFoldout =
                new GUIContent("Recommendation", "Recommendation on how to solve the issue");

            public static readonly GUIContent MuteButton = new GUIContent("Mute", "Always ignore selected issues.");
            public static readonly GUIContent UnmuteButton = new GUIContent("Unmute", "Always show selected issues.");
        }
    }
}
