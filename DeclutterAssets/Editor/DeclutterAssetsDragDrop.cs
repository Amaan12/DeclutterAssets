using System;
using System.Collections.Generic;
using System.IO;
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

        private static bool s_LastImportsExpandedState = true;

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

            var state = DeclutterAssetsUtility.GetTreeState(m_FolderTree);
            if (isImportsRoot && evt.type == EventType.Repaint && state != null && state.expandedIDs != null)
            {
                s_LastImportsExpandedState = state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
            }

            bool isWithinRowY = (evt.mousePosition.y >= rowRect.y && evt.mousePosition.y <= rowRect.y + rowRect.height);

            // Click handling for Imports root (foldout arrow or click/double-click)
            if (isImportsRoot && isWithinRowY)
            {
                bool isMouseDown = (evt.type == EventType.MouseDown);
                bool isUsedMouseDown = (evt.type == EventType.Used && evt.rawType == EventType.MouseDown);

                if (isMouseDown || isUsedMouseDown)
                {
                    // In Unity TreeView, foldout arrow is on the left edge (depth 0 is within [0, 36])
                    bool isClickOnFoldout = (evt.mousePosition.x >= 0 && evt.mousePosition.x <= 36f);
                    bool isClickOnRow = (evt.mousePosition.x > 36f);

                    bool isLeftButton = true;
                    bool isDoubleClick = false;

                    // Safety: Never access evt.button or evt.clickCount on EventType.Used as Unity throws UnityException!
                    if (isMouseDown)
                    {
                        isLeftButton = (evt.button == 0);
                        isDoubleClick = (evt.clickCount == 2);
                    }

                    if (isLeftButton)
                    {
                        if (evt.alt && (isClickOnFoldout || isDoubleClick))
                        {
                            // Alt-click on foldout arrow or Alt-double-click: recursive expand / collapse all
                            bool targetExpanded = !s_LastImportsExpandedState;
                            SetImportsAndChildrenExpanded(targetExpanded);
                            evt.Use();
                            return;
                        }
                        else if (isDoubleClick && isClickOnRow)
                        {
                            // Double-click on row label: toggle Imports root item
                            bool targetExpanded = !s_LastImportsExpandedState;
                            SetImportsExpandedOnly(targetExpanded);
                            evt.Use();
                            return;
                        }
                        else if (isClickOnFoldout)
                        {
                            // Standard click on foldout arrow: keep EditorPrefs preference synced with actual state
                            if (state != null && state.expandedIDs != null)
                            {
                                bool isNowExpanded = state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                                s_LastImportsExpandedState = isNowExpanded;
                                DeclutterAssetsTreePatcher.SetImportsExpandedPref(isNowExpanded);
                            }
                        }
                    }
                }

                Rect fullRowRect = new Rect(0, rowRect.y, Mathf.Max(rowRect.width, 200f), rowRect.height);
                HandleDragOverImports(fullRowRect, evt);
            }
            else if (isAssetsRoot && isWithinRowY)
            {
                Rect fullRowRect = new Rect(0, rowRect.y, Mathf.Max(rowRect.width, 200f), rowRect.height);
                HandleDragOverAssets(fullRowRect, evt);
            }
        }

        public void SetImportsAndChildrenExpanded(bool expand)
        {
            var state = DeclutterAssetsUtility.GetTreeState(m_FolderTree);
            if (state == null || state.expandedIDs == null) return;

            var allFolderIds = GetAllImportedFolderIds();

            if (expand)
            {
                if (!state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID))
                {
                    state.expandedIDs.Add(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                }
                foreach (int folderId in allFolderIds)
                {
                    if (!state.expandedIDs.Contains(folderId))
                    {
                        state.expandedIDs.Add(folderId);
                    }
                }
                s_LastImportsExpandedState = true;
                DeclutterAssetsTreePatcher.SetImportsExpandedPref(true);
            }
            else
            {
                state.expandedIDs.Remove(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                foreach (int folderId in allFolderIds)
                {
                    state.expandedIDs.Remove(folderId);
                }
                s_LastImportsExpandedState = false;
                DeclutterAssetsTreePatcher.SetImportsExpandedPref(false);
            }

            DeclutterAssetsTreePatcher.PatchTree(m_FolderTree, forceRepatch: true);
            DeclutterAssetsUtility.RepaintFolderTree(m_FolderTree);
        }

        public void SetImportsExpandedOnly(bool expand)
        {
            var state = DeclutterAssetsUtility.GetTreeState(m_FolderTree);
            if (state == null || state.expandedIDs == null) return;

            if (expand)
            {
                if (!state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID))
                {
                    state.expandedIDs.Add(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                }
                s_LastImportsExpandedState = true;
                DeclutterAssetsTreePatcher.SetImportsExpandedPref(true);
            }
            else
            {
                state.expandedIDs.Remove(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                s_LastImportsExpandedState = false;
                DeclutterAssetsTreePatcher.SetImportsExpandedPref(false);
            }

            DeclutterAssetsTreePatcher.PatchTree(m_FolderTree, forceRepatch: true);
            DeclutterAssetsUtility.RepaintFolderTree(m_FolderTree);
        }

        public void ToggleImportsFolding(bool includeChildren = false)
        {
            if (includeChildren)
            {
                SetImportsAndChildrenExpanded(!s_LastImportsExpandedState);
            }
            else
            {
                SetImportsExpandedOnly(!s_LastImportsExpandedState);
            }
        }

        private HashSet<int> GetAllImportedFolderIds()
        {
            var ids = new HashSet<int>();
            var settings = DeclutterAssetsSettings.Instance;
            var importedPaths = settings.ImportedPaths;

            if (importedPaths != null)
            {
                for (int i = 0; i < importedPaths.Count; i++)
                {
                    string path = importedPaths[i];
                    if (string.IsNullOrEmpty(path)) continue;

                    CollectFolderAndSubfolderIds(path, ids);
                }
            }

            // Also check items attached to the TreeView itself
            object treeData = DeclutterAssetsUtility.GetTreeData(m_FolderTree);
            if (treeData != null)
            {
                var root = DeclutterAssetsUtility.GetRootItem(treeData);
                if (root != null && root.children != null)
                {
                    for (int i = 0; i < root.children.Count; i++)
                    {
                        var item = root.children[i];
                        if (item != null && item.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                        {
                            CollectItemIdsRecursive(item, ids);
                            break;
                        }
                    }
                }
            }

            return ids;
        }

        private void CollectItemIdsRecursive(TreeViewItem item, HashSet<int> ids)
        {
            if (item == null || item.children == null) return;
            for (int i = 0; i < item.children.Count; i++)
            {
                var child = item.children[i];
                if (child == null) continue;
                if (child.id != DeclutterAssetsUtility.IMPORTS_ROOT_ID && child.id != 0)
                {
                    string path = AssetDatabase.GetAssetPath(child.id);
                    if (string.IsNullOrEmpty(path) || Directory.Exists(path) || AssetDatabase.IsValidFolder(path))
                    {
                        ids.Add(child.id);
                    }
                }
                CollectItemIdsRecursive(child, ids);
            }
        }

        private void CollectFolderAndSubfolderIds(string dirPath, HashSet<int> ids)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return;

                var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dirPath);
                if (assetObj != null)
                {
                    ids.Add(assetObj.GetInstanceID());
                }

                string[] subDirs = Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string subPath = subDirs[i].Replace('\\', '/');
                    if (Path.GetFileName(subPath).StartsWith(".")) continue;

                    var subObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(subPath);
                    if (subObj != null)
                    {
                        ids.Add(subObj.GetInstanceID());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeclutterAssets] Failed to collect folder IDs for {dirPath}: {ex.Message}");
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

            int selId = selectedIDs[0];
            int viewMode = DeclutterAssetsUtility.GetViewMode(m_ProjectBrowser);

            if (selId == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
            {
                // ONLY call ShowFolderContents in TWO-COLUMN mode (viewMode == 1)
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
                                DeclutterAssetsUtility.SetSelection(m_FolderTree, new int[] { firstChild.id }, false);
                                try
                                {
                                    DeclutterAssetsUtility.ShowFolderContents(m_ProjectBrowser, firstChild.id, false);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            else
            {
                // When any folder under Imports is selected in Two-Column mode, ensure right pane shows its contents
                if (viewMode == 1)
                {
                    string path = AssetDatabase.GetAssetPath(selId);
                    if (!string.IsNullOrEmpty(path) && (Directory.Exists(path) || AssetDatabase.IsValidFolder(path)))
                    {
                        try
                        {
                            DeclutterAssetsUtility.ShowFolderContents(m_ProjectBrowser, selId, false);
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
