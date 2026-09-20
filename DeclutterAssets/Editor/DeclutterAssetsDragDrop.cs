using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DeclutterAssets.Editor
{
    public class DeclutterAssetsDragDrop
    {
        private readonly EditorWindow m_ProjectBrowser;
        private readonly object m_FolderTree;
        private int m_AssetsInstanceId = 0;

        public DeclutterAssetsDragDrop(EditorWindow projectBrowser, object folderTree)
        {
            m_ProjectBrowser = projectBrowser;
            m_FolderTree = folderTree;
        }

        private int AssetsInstanceId
        {
            get
            {
                if (m_AssetsInstanceId == 0)
                {
                    var assetsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets");
                    if (assetsObj != null)
                    {
                        m_AssetsInstanceId = assetsObj.GetInstanceID();
                    }
                }
                return m_AssetsInstanceId;
            }
        }

        public void HandleRowGUI(int id, Rect rowRect)
        {
            Event evt = Event.current;
            if (evt == null) return;

            bool isImportsRoot = (id == DeclutterAssetsUtility.IMPORTS_ROOT_ID);
            bool isAssetsRoot = (AssetsInstanceId != 0 && id == AssetsInstanceId);

            if (!isImportsRoot && !isAssetsRoot)
            {
                return;
            }

            if (!rowRect.Contains(evt.mousePosition))
            {
                return;
            }

            // Click handling for Imports root (foldout arrow or click/double-click)
            if (isImportsRoot)
            {
                Rect foldoutRect = new Rect(rowRect.x, rowRect.y, 16, rowRect.height);
                bool isClickOnFoldout = foldoutRect.Contains(evt.mousePosition);

                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    if (isClickOnFoldout || evt.clickCount == 2)
                    {
                        ToggleImportsFolding();
                        evt.Use();
                        return;
                    }
                }

                HandleDragOverImports(rowRect, evt);
            }
            else if (isAssetsRoot)
            {
                HandleDragOverAssets(rowRect, evt);
            }
        }

        public void ToggleImportsFolding()
        {
            var state = DeclutterAssetsUtility.GetTreeState(m_FolderTree);
            if (state != null && state.expandedIDs != null)
            {
                if (state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID))
                {
                    state.expandedIDs.Remove(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                    DeclutterAssetsTreePatcher.SetImportsExpandedPref(false);
                }
                else
                {
                    state.expandedIDs.Add(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                    DeclutterAssetsTreePatcher.SetImportsExpandedPref(true);
                }

                DeclutterAssetsTreePatcher.PatchTree(m_FolderTree, forceRepatch: true);
                DeclutterAssetsUtility.RepaintFolderTree(m_FolderTree);
            }
        }

        private void HandleDragOverImports(Rect rowRect, Event evt)
        {
            if (evt.type == EventType.DragUpdated)
            {
                if (CanDropOnImports())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.DragPerform)
            {
                if (CanDropOnImports())
                {
                    DragAndDrop.AcceptDrag();
                    PerformDropOnImports();
                    evt.Use();
                }
            }
            else if (evt.type == EventType.Repaint)
            {
                if (CanDropOnImports() && DragAndDrop.visualMode != DragAndDropVisualMode.None)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.55f, 1.0f, 0.25f));
                }
            }
        }

        private void HandleDragOverAssets(Rect rowRect, Event evt)
        {
            if (evt.type == EventType.DragUpdated)
            {
                if (CanDropOnAssets())
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.DragPerform)
            {
                if (CanDropOnAssets())
                {
                    DragAndDrop.AcceptDrag();
                    PerformDropOnAssets();
                    evt.Use();
                }
            }
            else if (evt.type == EventType.Repaint)
            {
                if (CanDropOnAssets() && DragAndDrop.visualMode != DragAndDropVisualMode.None)
                {
                    EditorGUI.DrawRect(rowRect, new Color(0.25f, 0.55f, 1.0f, 0.25f));
                }
            }
        }

        private bool CanDropOnImports()
        {
            string[] paths = DragAndDrop.paths;
            if (paths == null || paths.Length == 0) return false;

            var settings = DeclutterAssetsSettings.Instance;

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                // Any asset or folder in Assets not already in Imports
                if (!settings.IsDirectImport(path))
                {
                    return true;
                }
            }

            return false;
        }

        private void PerformDropOnImports()
        {
            string[] paths = DragAndDrop.paths;
            if (paths == null) return;

            var settings = DeclutterAssetsSettings.Instance;
            bool changed = false;

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                if (settings.AddImport(path))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                DeclutterAssetsTreePatcher.PatchTree(m_FolderTree, forceRepatch: true);
                DeclutterAssetsUtility.RepaintFolderTree(m_FolderTree);
            }
        }

        private bool CanDropOnAssets()
        {
            string[] paths = DragAndDrop.paths;
            if (paths == null || paths.Length == 0) return false;

            var settings = DeclutterAssetsSettings.Instance;

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;

                if (settings.IsImport(path))
                {
                    return true;
                }
            }

            return false;
        }

        private void PerformDropOnAssets()
        {
            string[] paths = DragAndDrop.paths;
            if (paths == null) return;

            var settings = DeclutterAssetsSettings.Instance;
            bool changed = false;

            foreach (string path in paths)
            {
                if (string.IsNullOrEmpty(path)) continue;

                if (settings.RemoveImport(path))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                DeclutterAssetsTreePatcher.PatchTree(m_FolderTree, forceRepatch: true);
                DeclutterAssetsUtility.RepaintFolderTree(m_FolderTree);
            }
        }

        public void HandleSelectionChanged(int[] selectedIDs)
        {
            if (selectedIDs == null || selectedIDs.Length == 0) return;

            if (selectedIDs[0] == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
            {
                // ONLY call ShowFolderContents in TWO-COLUMN mode (viewMode == 1)
                int viewMode = DeclutterAssetsUtility.GetViewMode(m_ProjectBrowser);
                if (viewMode == 1)
                {
                    object treeData = DeclutterAssetsUtility.GetTreeData(m_FolderTree);
                    TreeViewItem rootItem = DeclutterAssetsUtility.GetRootItem(treeData);
                    if (rootItem != null && rootItem.children != null)
                    {
                        var importsItem = rootItem.children.Find(c => c != null && c.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                        if (importsItem != null && importsItem.children != null && importsItem.children.Count > 0)
                        {
                            var firstChild = importsItem.children[0];
                            if (firstChild != null)
                            {
                                DeclutterAssetsUtility.SetSelection(m_FolderTree, new int[] { firstChild.id }, true);
                                try
                                {
                                    DeclutterAssetsUtility.ShowFolderContents(m_ProjectBrowser, firstChild.id, true);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
        }
    }
}
