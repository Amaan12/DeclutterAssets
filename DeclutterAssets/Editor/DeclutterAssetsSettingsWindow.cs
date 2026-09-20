using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DeclutterAssets.Editor
{
    public class DeclutterAssetsSettingsWindow : EditorWindow
    {
        private Vector2 m_ScrollPos;
        private string m_NewFolderPath = "";
        private bool m_ShowDiagnostics = false;

        [MenuItem("Tools/Declutter Assets/Settings", false, 100)]
        [MenuItem("Window/Declutter Assets Settings", false, 1000)]
        public static void Open()
        {
            var window = GetWindow<DeclutterAssetsSettingsWindow>("Declutter Assets");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Declutter Assets — Workspace Partition", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Partitions the physical 'Assets/' directory into two clean visual roots in the Project Window:\n" +
                "• Assets: Your personal project code & assets.\n" +
                "• Imports: Third-party & Asset Store imports.\n\n" +
                "Drag & drop any folder or asset onto 'Imports' or 'Assets' to move it between partitions, or configure below.",
                MessageType.Info
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Tracked Imported Items", EditorStyles.boldLabel);

            var settings = DeclutterAssetsSettings.Instance;
            var paths = settings.ImportedPaths;

            m_ScrollPos = EditorGUILayout.BeginScrollView(m_ScrollPos, EditorStyles.helpBox, GUILayout.Height(150));
            if (paths.Count == 0)
            {
                EditorGUILayout.LabelField("No items currently assigned to Imports.", EditorStyles.miniLabel);
            }
            else
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(paths[i], EditorStyles.wordWrappedLabel);

                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUILayout.Button("Remove", GUILayout.Width(65)))
                    {
                        settings.RemoveImport(paths[i]);
                        GUIUtility.ExitGUI();
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Add Asset or Folder to Imports", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            m_NewFolderPath = EditorGUILayout.TextField(m_NewFolderPath);

            if (GUILayout.Button("Browse", GUILayout.Width(65)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Folder to Treat as Import", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    {
                        m_NewFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Path", "Please select an item inside the project's 'Assets' directory.", "OK");
                    }
                }
            }

            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrEmpty(m_NewFolderPath))
                {
                    settings.AddImport(m_NewFolderPath);
                    m_NewFolderPath = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset to Default (Assets/ImportedAssets)"))
            {
                if (EditorUtility.DisplayDialog("Reset Declutter Assets", "Reset imports list to default ('Assets/ImportedAssets')?", "Reset", "Cancel"))
                {
                    while (settings.ImportedPaths.Count > 0)
                    {
                        settings.RemoveImport(settings.ImportedPaths[0]);
                    }
                    if (AssetDatabase.IsValidFolder("Assets/ImportedAssets"))
                    {
                        settings.AddImport("Assets/ImportedAssets");
                    }
                }
            }

            if (GUILayout.Button("Force Project Window Refresh"))
            {
                DeclutterAssetsProjectBrowserHook.ScanAndHookBrowsers();
                settings.Save();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);
            m_ShowDiagnostics = EditorGUILayout.Foldout(m_ShowDiagnostics, "Diagnostics & Inspector", true, EditorStyles.foldoutHeader);
            if (m_ShowDiagnostics)
            {
                DrawDiagnostics();
            }
        }

        private void DrawDiagnostics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var browserType = DeclutterAssetsUtility.ProjectBrowserType;
            EditorGUILayout.LabelField("ProjectBrowser Type:", browserType != null ? browserType.FullName : "<NOT FOUND>");

            if (browserType != null)
            {
                var browsers = Resources.FindObjectsOfTypeAll(browserType);
                EditorGUILayout.LabelField("Open Project Windows:", browsers != null ? browsers.Length.ToString() : "0");

                if (browsers != null && browsers.Length > 0)
                {
                    for (int i = 0; i < browsers.Length; i++)
                    {
                        var window = browsers[i] as EditorWindow;
                        if (window == null) continue;

                        int viewMode = DeclutterAssetsUtility.GetViewMode(window);
                        string modeStr = viewMode == 0 ? "OneColumn" : (viewMode == 1 ? "TwoColumns" : viewMode.ToString());
                        object tree = DeclutterAssetsUtility.GetActiveTree(window);

                        EditorGUILayout.LabelField($"Window #{i + 1} ({modeStr}):", tree != null ? $"ActiveTree found ({tree.GetType().Name})" : "<Tree is NULL>");

                        if (tree != null)
                        {
                            object treeData = DeclutterAssetsUtility.GetTreeData(tree);
                            TreeViewItem rootItem = DeclutterAssetsUtility.GetRootItem(treeData);

                            if (rootItem != null && rootItem.children != null)
                            {
                                var childNames = new List<string>();
                                for (int c = 0; c < rootItem.children.Count; c++)
                                {
                                    var item = rootItem.children[c];
                                    if (item != null)
                                    {
                                        childNames.Add($"{item.displayName} (id:{item.id})");
                                    }
                                }
                                EditorGUILayout.LabelField("  Root Children:", string.Join(", ", childNames));
                            }

                            var rows = DeclutterAssetsUtility.GetRows(treeData);
                            EditorGUILayout.LabelField("  Total Visible Rows:", rows != null ? rows.Count.ToString() : "<null>");
                        }
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }
    }
}
