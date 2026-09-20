using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DeclutterAssets.Editor
{
    public static class DeclutterAssetsTreePatcher
    {
        private const string PrefsKeyExpanded = "DeclutterAssets_ImportsRoot_Expanded";
        private static int s_AssetsFolderId = 0;
        private static bool s_InitializedExpanded = false;

        public static bool SuppressAssetsExpansion { get; set; } = false;

        // Cache of imported TreeViewItems so they persist even when Assets is collapsed
        private static readonly Dictionary<string, TreeViewItem> s_CachedImportItems = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase);

        public static int GetAssetsFolderId()
        {
            if (s_AssetsFolderId == 0)
            {
                var assetsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets");
                if (assetsObj != null)
                {
                    s_AssetsFolderId = assetsObj.GetInstanceID();
                }
            }
            return s_AssetsFolderId;
        }

        public static void ClearCache()
        {
            s_CachedImportItems.Clear();
        }

        public static void SetImportsExpandedPref(bool expanded)
        {
            EditorPrefs.SetBool(PrefsKeyExpanded, expanded);
        }

        public static bool NeedsPatch(object folderTree)
        {
            if (folderTree == null) return false;

            try
            {
                object treeData = DeclutterAssetsUtility.GetTreeData(folderTree);
                if (treeData == null) return false;

                TreeViewItem rootItem = DeclutterAssetsUtility.GetRootItem(treeData);
                if (rootItem == null) return true;

                int assetsId = GetAssetsFolderId();

                if (rootItem.id == assetsId || string.Equals(rootItem.displayName, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (rootItem.children == null || rootItem.children.Count == 0)
                {
                    return true;
                }

                bool hasImportsInRoot = false;
                for (int i = 0; i < rootItem.children.Count; i++)
                {
                    var child = rootItem.children[i];
                    if (child != null && child.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                    {
                        hasImportsInRoot = true;
                        break;
                    }
                }
                if (!hasImportsInRoot)
                {
                    return true;
                }

                var rows = DeclutterAssetsUtility.GetRows(treeData);
                if (rows == null || rows.Count == 0)
                {
                    return true;
                }

                bool hasImportsInRows = false;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row != null && row.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                    {
                        hasImportsInRows = true;
                        break;
                    }
                }

                return !hasImportsInRows;
            }
            catch
            {
                return false;
            }
        }

        public static bool PatchTree(object folderTree, bool forceRepatch = false)
        {
            if (folderTree == null) return false;

            try
            {
                object treeData = DeclutterAssetsUtility.GetTreeData(folderTree);
                if (treeData == null) return false;

                TreeViewItem rootItem = DeclutterAssetsUtility.GetRootItem(treeData);
                if (rootItem == null) return false;

                if (s_AssetsFolderId == 0)
                {
                    var assetsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets");
                    if (assetsObj != null)
                    {
                        s_AssetsFolderId = assetsObj.GetInstanceID();
                    }
                }

                // In One-Column mode, if rootItem is Assets itself (depth 0), wrap it in an invisible root
                // so Assets and Imports can sit side-by-side as siblings!
                if (rootItem.id == s_AssetsFolderId || string.Equals(rootItem.displayName, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    var invisibleRoot = new TreeViewItem(-1, -1, "Invisible Root Item");
                    invisibleRoot.children = new List<TreeViewItem> { rootItem };
                    rootItem.parent = invisibleRoot;
                    rootItem.depth = 0;
                    DeclutterAssetsUtility.SetRootItem(treeData, invisibleRoot);
                    rootItem = invisibleRoot;
                }

                if (rootItem.children == null || rootItem.children.Count == 0)
                {
                    return false;
                }

                var rootChildren = rootItem.children;
                TreeViewItem existingImportsItem = null;
                TreeViewItem assetsItem = null;

                for (int i = 0; i < rootChildren.Count; i++)
                {
                    var item = rootChildren[i];
                    if (item == null) continue;

                    if (item.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                    {
                        existingImportsItem = item;
                    }
                    else if ((s_AssetsFolderId != 0 && item.id == s_AssetsFolderId) ||
                             string.Equals(item.displayName, "Assets", StringComparison.OrdinalIgnoreCase))
                    {
                        assetsItem = item;
                    }
                }

                if (assetsItem == null)
                {
                    return false;
                }

                // Verify if visible rows already contain Imports
                var currentRows = DeclutterAssetsUtility.GetRows(treeData);
                bool importsInRows = false;
                if (currentRows != null)
                {
                    for (int i = 0; i < currentRows.Count; i++)
                    {
                        if (currentRows[i] != null && currentRows[i].id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                        {
                            importsInRows = true;
                            break;
                        }
                    }
                }

                if (!forceRepatch && existingImportsItem != null && importsInRows)
                {
                    return false;
                }

                // 1. Prepare or create Imports root item as bold RootTreeItem
                TreeViewItem importsItem = existingImportsItem;
                if (importsItem == null)
                {
                    importsItem = DeclutterAssetsUtility.CreateRootTreeItem(
                        DeclutterAssetsUtility.IMPORTS_ROOT_ID,
                        0, // sibling depth with Assets (depth 0)
                        rootItem,
                        "Imports"
                    );
                    importsItem.icon = DeclutterAssetsUtility.GetImportsFolderIcon();
                    importsItem.children = new List<TreeViewItem>();
                }

                importsItem.parent = rootItem;
                importsItem.depth = 0;

                if (importsItem.children == null)
                {
                    importsItem.children = new List<TreeViewItem>();
                }

                var settings = DeclutterAssetsSettings.Instance;

                // 2. If Assets has children, safely extract any imported items and cache them
                if (assetsItem.children != null && assetsItem.children.Count > 0)
                {
                    var toRemoveFromAssets = new List<TreeViewItem>();

                    for (int i = 0; i < assetsItem.children.Count; i++)
                    {
                        var child = assetsItem.children[i];
                        if (child == null) continue; // Skip Unity collapsed parent placeholder (null)

                        string assetPath = AssetDatabase.GetAssetPath(child.id);
                        string itemName = child.displayName;

                        bool isImport = settings.IsImport(assetPath) ||
                                        settings.IsImport("Assets/" + itemName) ||
                                        settings.IsDirectImport(itemName);

                        if (isImport)
                        {
                            toRemoveFromAssets.Add(child);
                            string key = !string.IsNullOrEmpty(assetPath) ? assetPath : "Assets/" + itemName;
                            s_CachedImportItems[key] = child;
                        }
                    }

                    for (int i = 0; i < toRemoveFromAssets.Count; i++)
                    {
                        assetsItem.children.Remove(toRemoveFromAssets[i]);
                    }
                }

                // 3. Populate Imports children (independent of whether Assets is collapsed or expanded!)
                importsItem.children.Clear();
                var importedPaths = settings.ImportedPaths;

                for (int i = 0; i < importedPaths.Count; i++)
                {
                    string path = importedPaths[i];
                    if (string.IsNullOrEmpty(path)) continue;

                    TreeViewItem itemNode = null;
                    if (s_CachedImportItems.TryGetValue(path, out var cached) && cached != null)
                    {
                        itemNode = cached;
                    }
                    else
                    {
                        itemNode = CreateItemFromPath(path, importsItem.depth + 1, foldersOnly: false);
                        if (itemNode != null)
                        {
                            s_CachedImportItems[path] = itemNode;
                        }
                    }

                    if (itemNode != null)
                    {
                        itemNode.parent = importsItem;
                        UpdateItemDepths(itemNode, importsItem.depth + 1);
                        importsItem.children.Add(itemNode);
                    }
                }

                // 4. Position Imports root item directly after Assets in rootChildren
                rootChildren.Remove(importsItem);
                int assetsIndex = rootChildren.IndexOf(assetsItem);
                int insertIndex = assetsIndex >= 0 ? assetsIndex + 1 : 0;
                if (insertIndex >= rootChildren.Count)
                {
                    rootChildren.Add(importsItem);
                }
                else
                {
                    rootChildren.Insert(insertIndex, importsItem);
                }

                // Register Imports in DataSource (AssetsTreeViewDataSource) so Unity's native SetExpandedWithChildren recognizes it!
                DeclutterAssetsUtility.RegisterImportsRootInDataSource(treeData, importsItem);

                // 5. Manage expansion state (respect user's manual fold/unfold choice!)
                TreeViewState state = DeclutterAssetsUtility.GetTreeState(folderTree);
                if (state != null && state.expandedIDs != null)
                {
                    if (SuppressAssetsExpansion && s_AssetsFolderId != 0)
                    {
                        state.expandedIDs.Remove(s_AssetsFolderId);
                    }

                    if (!s_InitializedExpanded)
                    {
                        bool shouldBeExpanded = EditorPrefs.GetBool(PrefsKeyExpanded, true);
                        if (shouldBeExpanded && !state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID))
                        {
                            state.expandedIDs.Add(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                        }
                        else if (!shouldBeExpanded && state.expandedIDs.Contains(DeclutterAssetsUtility.IMPORTS_ROOT_ID))
                        {
                            state.expandedIDs.Remove(DeclutterAssetsUtility.IMPORTS_ROOT_ID);
                        }
                        s_InitializedExpanded = true;
                    }
                }

                // 6. Rebuild visible flat rows cache
                var newRows = new List<TreeViewItem>();
                List<int> expandedList = state != null ? state.expandedIDs : null;
                bool isFoldersOnly = DeclutterAssetsUtility.IsFoldersOnly(treeData);
                BuildRowsRecursive(rootItem, expandedList, newRows, isFoldersOnly);

                DeclutterAssetsUtility.SetRows(treeData, newRows);
                DeclutterAssetsUtility.SetNeedRefreshRows(treeData, false);
                DeclutterAssetsUtility.RepaintFolderTree(folderTree);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeclutterAssets] Safe catch in PatchTree: {ex.Message}");
                return false;
            }
        }

        private static bool HasNonMetaFiles(string dirPath)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return false;
                var files = Directory.GetFiles(dirPath);
                for (int i = 0; i < files.Length; i++)
                {
                    if (!files[i].EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static bool HasChildContent(string dirPath, bool foldersOnly)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return false;
                if (Directory.GetDirectories(dirPath).Length > 0) return true;
                if (!foldersOnly && HasNonMetaFiles(dirPath)) return true;
            }
            catch { }
            return false;
        }

        private static TreeViewItem CreateItemFromPath(string path, int depth, bool foldersOnly = false)
        {
            try
            {
                var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                int id = assetObj != null ? assetObj.GetInstanceID() : 0;
                if (id == 0) return null;

                string name = Path.GetFileName(path);
                var item = new TreeViewItem(id, depth, name);

                Texture2D icon = AssetDatabase.GetCachedIcon(path) as Texture2D;
                if (icon == null)
                {
                    icon = Directory.Exists(path) 
                        ? DeclutterAssetsUtility.GetImportsFolderIcon() 
                        : (EditorGUIUtility.ObjectContent(assetObj, typeof(UnityEngine.Object)).image as Texture2D);
                }
                item.icon = icon;

                if (Directory.Exists(path))
                {
                    PopulateDirectoryChildren(item, path, foldersOnly);
                }
                else
                {
                    item.children = new List<TreeViewItem>();
                }

                return item;
            }
            catch
            {
                return null;
            }
        }

        private static void PopulateDirectoryChildren(TreeViewItem parentItem, string dirPath, bool foldersOnly = false)
        {
            if (!Directory.Exists(dirPath)) return;

            parentItem.children = new List<TreeViewItem>();

            try
            {
                var subDirs = Directory.GetDirectories(dirPath);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string subPath = subDirs[i].Replace('\\', '/');
                    var subItem = CreateItemFromPath(subPath, parentItem.depth + 1, foldersOnly);
                    if (subItem != null)
                    {
                        subItem.parent = parentItem;
                        bool hasContent = HasChildContent(subPath, foldersOnly);
                        if (hasContent && (subItem.children == null || subItem.children.Count == 0))
                        {
                            subItem.children = new List<TreeViewItem> { null };
                        }
                        parentItem.children.Add(subItem);
                    }
                }

                if (!foldersOnly)
                {
                    var files = Directory.GetFiles(dirPath);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string filePath = files[i].Replace('\\', '/');
                        if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                        var fileItem = CreateItemFromPath(filePath, parentItem.depth + 1, foldersOnly);
                        if (fileItem != null)
                        {
                            fileItem.parent = parentItem;
                            fileItem.children = new List<TreeViewItem>();
                            parentItem.children.Add(fileItem);
                        }
                    }
                }
            }
            catch { }
        }

        private static void UpdateItemDepths(TreeViewItem item, int depth)
        {
            if (item == null) return;
            item.depth = depth;

            if (item.children != null)
            {
                for (int i = 0; i < item.children.Count; i++)
                {
                    var child = item.children[i];
                    if (child != null)
                    {
                        UpdateItemDepths(child, depth + 1);
                    }
                }
            }
        }

        private static bool ShouldPopulateFolderChildren(TreeViewItem folderItem, bool foldersOnly)
        {
            if (folderItem == null) return false;
            if (folderItem.children == null) return true;
            if (folderItem.children.Count == 1 && folderItem.children[0] == null) return true;

            // If empty, check if it actually has content on disk
            if (folderItem.children.Count == 0)
            {
                string path = AssetDatabase.GetAssetPath(folderItem.id);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    bool isUnderImports = IsDescendantOfImports(folderItem);
                    bool allowFiles = !foldersOnly || isUnderImports;
                    if (HasChildContent(path, !allowFiles))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsDescendantOfImports(TreeViewItem item)
        {
            var current = item;
            while (current != null)
            {
                if (current.id == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private static void PopulateFolderChildrenIfLazy(TreeViewItem folderItem, bool foldersOnly = false)
        {
            if (folderItem == null) return;

            string folderPath = AssetDatabase.GetAssetPath(folderItem.id);
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

            try
            {
                var settings = DeclutterAssetsSettings.Instance;
                var validChildren = new List<TreeViewItem>();
                bool isUnderImports = IsDescendantOfImports(folderItem);
                bool allowFiles = !foldersOnly || isUnderImports;

                // 1. Subdirectories
                var subDirs = Directory.GetDirectories(folderPath);
                for (int i = 0; i < subDirs.Length; i++)
                {
                    string subDir = subDirs[i].Replace('\\', '/');
                    if (!isUnderImports && settings.IsImport(subDir)) continue;

                    var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(subDir);
                    int id = assetObj != null ? assetObj.GetInstanceID() : 0;
                    if (id == 0) continue;

                    string name = Path.GetFileName(subDir);
                    var subItem = new TreeViewItem(id, folderItem.depth + 1, name);
                    subItem.parent = folderItem;

                    Texture2D icon = AssetDatabase.GetCachedIcon(subDir) as Texture2D;
                    if (icon == null)
                    {
                        icon = DeclutterAssetsUtility.GetImportsFolderIcon();
                    }
                    subItem.icon = icon;

                    bool hasContent = HasChildContent(subDir, !allowFiles);
                    if (hasContent)
                    {
                        subItem.children = new List<TreeViewItem> { null }; // Lazy marker so foldout arrow renders
                    }
                    else
                    {
                        subItem.children = new List<TreeViewItem>();
                    }

                    validChildren.Add(subItem);
                }

                // 2. Files (always populate for folders under Imports, or when !foldersOnly)
                if (allowFiles)
                {
                    var files = Directory.GetFiles(folderPath);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string filePath = files[i].Replace('\\', '/');
                        if (filePath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                        var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                        int id = assetObj != null ? assetObj.GetInstanceID() : 0;
                        if (id == 0) continue;

                        string fileName = Path.GetFileName(filePath);
                        var fileItem = new TreeViewItem(id, folderItem.depth + 1, fileName);
                        fileItem.parent = folderItem;

                        Texture2D icon = AssetDatabase.GetCachedIcon(filePath) as Texture2D;
                        if (icon == null)
                        {
                            icon = EditorGUIUtility.ObjectContent(assetObj, typeof(UnityEngine.Object)).image as Texture2D;
                        }
                        fileItem.icon = icon;
                        fileItem.children = new List<TreeViewItem>();

                        validChildren.Add(fileItem);
                    }
                }

                folderItem.children = validChildren;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeclutterAssets] Failed to populate children for {folderPath}: {ex.Message}");
            }
        }

        private static void BuildRowsRecursive(TreeViewItem parent, List<int> expandedIDs, List<TreeViewItem> rows, bool foldersOnly = false)
        {
            if (parent == null || parent.children == null) return;

            for (int i = 0; i < parent.children.Count; i++)
            {
                var child = parent.children[i];
                if (child == null) continue; // Skip Unity placeholder nulls

                rows.Add(child);

                if (expandedIDs != null && expandedIDs.Contains(child.id))
                {
                    if (ShouldPopulateFolderChildren(child, foldersOnly))
                    {
                        PopulateFolderChildrenIfLazy(child, foldersOnly);
                    }

                    if (child.children != null && child.children.Count > 0)
                    {
                        BuildRowsRecursive(child, expandedIDs, rows, foldersOnly);
                    }
                }
            }
        }
    }
}
