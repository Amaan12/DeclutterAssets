using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace DeclutterAssets.Editor
{
    public static class DeclutterAssetsUtility
    {
        // Unique synthetic ID for the Imports root node
        public const int IMPORTS_ROOT_ID = 0x7F000001;

        // Reflection caches
        private static Type s_ProjectBrowserType;
        private static FieldInfo s_FolderTreeField;
        private static FieldInfo s_AssetTreeField;
        private static FieldInfo s_ViewModeField;
        private static MethodInfo s_ShowFolderContentsMethod;
        private static FieldInfo s_PackagesFolderInstanceIdField;
        private static FieldInfo s_SkipHiddenPackagesField;

        private static Type s_TreeViewControllerType;
        private static PropertyInfo s_DataProp;
        private static PropertyInfo s_StateProp;
        private static MethodInfo s_ReloadDataMethod;
        private static MethodInfo s_RepaintMethod;
        private static MethodInfo s_SetSelectionMethod;
        private static PropertyInfo s_OnGUIRowCallbackProp;
        private static FieldInfo s_OnGUIRowCallbackField;
        private static PropertyInfo s_SelectionChangedCallbackProp;
        private static FieldInfo s_SelectionChangedCallbackField;
        private static PropertyInfo s_ExpandedStateChangedProp;
        private static FieldInfo s_ExpandedStateChangedField;
        private static PropertyInfo s_ItemDoubleClickedCallbackProp;
        private static FieldInfo s_ItemDoubleClickedCallbackField;

        private static Type s_TreeViewDataSourceType;
        private static FieldInfo s_RootItemField;
        private static FieldInfo s_RowsField;
        private static FieldInfo s_NeedRefreshRowsField;
        private static MethodInfo s_SetExpandedMethod;
        private static MethodInfo s_IsExpandedMethod;
        private static FieldInfo s_OnVisibleRowsChangedField;

        private static Type s_AssetsTreeViewDataSourceType;
        private static FieldInfo s_RootsTreeViewItemField;
        private static PropertyInfo s_FoldersOnlyProp;
        private static FieldInfo s_FoldersOnlyField;

        private static Type s_RootTreeItemType;
        private static Texture2D s_ImportsFolderIcon;
        private static bool s_Initialized = false;

        static DeclutterAssetsUtility()
        {
            InitializeReflection();
        }

        public static Type FindTypeInLoadedAssemblies(string typeFullName)
        {
            var type = Type.GetType(typeFullName);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(typeFullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        public static void InitializeReflection()
        {
            if (s_Initialized) return;

            try
            {
                s_ProjectBrowserType = FindTypeInLoadedAssemblies("UnityEditor.ProjectBrowser");
                if (s_ProjectBrowserType != null)
                {
                    s_FolderTreeField = s_ProjectBrowserType.GetField("m_FolderTree", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_AssetTreeField = s_ProjectBrowserType.GetField("m_AssetTree", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_ViewModeField = s_ProjectBrowserType.GetField("m_ViewMode", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_ShowFolderContentsMethod = s_ProjectBrowserType.GetMethod("ShowFolderContents", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(int), typeof(bool) }, null);
                    s_PackagesFolderInstanceIdField = s_ProjectBrowserType.GetField("kPackagesFolderInstanceId", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    s_SkipHiddenPackagesField = s_ProjectBrowserType.GetField("m_SkipHiddenPackages", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }

                s_TreeViewControllerType = FindTypeInLoadedAssemblies("UnityEditor.IMGUI.Controls.TreeViewController");
                if (s_TreeViewControllerType != null)
                {
                    s_DataProp = s_TreeViewControllerType.GetProperty("data", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_StateProp = s_TreeViewControllerType.GetProperty("state", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_ReloadDataMethod = s_TreeViewControllerType.GetMethod("ReloadData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_RepaintMethod = s_TreeViewControllerType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_SetSelectionMethod = s_TreeViewControllerType.GetMethod("SetSelection", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int[]), typeof(bool) }, null);

                    s_OnGUIRowCallbackProp = s_TreeViewControllerType.GetProperty("onGUIRowCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_OnGUIRowCallbackField = s_TreeViewControllerType.GetField("onGUIRowCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? s_TreeViewControllerType.GetField("<onGUIRowCallback>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                    s_SelectionChangedCallbackProp = s_TreeViewControllerType.GetProperty("selectionChangedCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_SelectionChangedCallbackField = s_TreeViewControllerType.GetField("selectionChangedCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? s_TreeViewControllerType.GetField("<selectionChangedCallback>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                    s_ExpandedStateChangedProp = s_TreeViewControllerType.GetProperty("expandedStateChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_ExpandedStateChangedField = s_TreeViewControllerType.GetField("expandedStateChanged", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? s_TreeViewControllerType.GetField("<expandedStateChanged>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                    s_ItemDoubleClickedCallbackProp = s_TreeViewControllerType.GetProperty("itemDoubleClickedCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_ItemDoubleClickedCallbackField = s_TreeViewControllerType.GetField("itemDoubleClickedCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? s_TreeViewControllerType.GetField("<itemDoubleClickedCallback>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                s_TreeViewDataSourceType = FindTypeInLoadedAssemblies("UnityEditor.IMGUI.Controls.TreeViewDataSource");
                if (s_TreeViewDataSourceType != null)
                {
                    s_RootItemField = s_TreeViewDataSourceType.GetField("m_RootItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_RowsField = s_TreeViewDataSourceType.GetField("m_Rows", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_NeedRefreshRowsField = s_TreeViewDataSourceType.GetField("m_NeedRefreshRows", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_SetExpandedMethod = s_TreeViewDataSourceType.GetMethod("SetExpanded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int), typeof(bool) }, null);
                    s_IsExpandedMethod = s_TreeViewDataSourceType.GetMethod("IsExpanded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(int) }, null);
                    s_OnVisibleRowsChangedField = s_TreeViewDataSourceType.GetField("onVisibleRowsChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }

                s_AssetsTreeViewDataSourceType = FindTypeInLoadedAssemblies("UnityEditor.AssetsTreeViewDataSource");
                if (s_AssetsTreeViewDataSourceType != null)
                {
                    s_RootsTreeViewItemField = s_AssetsTreeViewDataSourceType.GetField("m_RootsTreeViewItem", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    s_FoldersOnlyProp = s_AssetsTreeViewDataSourceType.GetProperty("foldersOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    s_FoldersOnlyField = s_AssetsTreeViewDataSourceType.GetField("foldersOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?? s_AssetsTreeViewDataSourceType.GetField("m_FoldersOnly", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                s_RootTreeItemType = FindTypeInLoadedAssemblies("UnityEditor.AssetsTreeViewDataSource+RootTreeItem");

                s_Initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeclutterAssets] Failed to initialize reflection bindings: {ex}");
            }
        }

        public static Type ProjectBrowserType => s_ProjectBrowserType;

        public static object GetActiveTree(EditorWindow projectBrowser)
        {
            if (projectBrowser == null) return null;

            int viewMode = GetViewMode(projectBrowser);
            if (viewMode == 0) // One-Column mode uses m_AssetTree
            {
                if (s_AssetTreeField != null)
                {
                    var tree = s_AssetTreeField.GetValue(projectBrowser);
                    if (tree != null) return tree;
                }
                if (s_FolderTreeField != null)
                {
                    var tree = s_FolderTreeField.GetValue(projectBrowser);
                    if (tree != null) return tree;
                }
            }
            else // Two-Column mode (or fallback) uses m_FolderTree
            {
                if (s_FolderTreeField != null)
                {
                    var tree = s_FolderTreeField.GetValue(projectBrowser);
                    if (tree != null) return tree;
                }
                if (s_AssetTreeField != null)
                {
                    var tree = s_AssetTreeField.GetValue(projectBrowser);
                    if (tree != null) return tree;
                }
            }

            return null;
        }

        public static bool IsFoldersOnly(object treeData)
        {
            if (treeData == null) return true;
            try
            {
                if (s_FoldersOnlyProp != null)
                {
                    return (bool)s_FoldersOnlyProp.GetValue(treeData);
                }
                if (s_FoldersOnlyField != null)
                {
                    return (bool)s_FoldersOnlyField.GetValue(treeData);
                }
            }
            catch { }
            return true;
        }

        public static int GetViewMode(EditorWindow projectBrowser)
        {
            if (projectBrowser != null && s_ViewModeField != null)
            {
                return (int)s_ViewModeField.GetValue(projectBrowser);
            }
            return -1;
        }

        public static bool GetSkipHiddenPackages(EditorWindow projectBrowser)
        {
            if (projectBrowser != null && s_SkipHiddenPackagesField != null)
            {
                try
                {
                    return (bool)s_SkipHiddenPackagesField.GetValue(projectBrowser);
                }
                catch { }
            }
            return false;
        }

        public static object GetTreeData(object folderTree)
        {
            if (folderTree == null || s_DataProp == null) return null;
            return s_DataProp.GetValue(folderTree);
        }

        public static TreeViewState GetTreeState(object folderTree)
        {
            if (folderTree == null || s_StateProp == null) return null;
            return s_StateProp.GetValue(folderTree) as TreeViewState;
        }

        public static TreeViewItem GetRootItem(object treeData)
        {
            if (treeData == null || s_RootItemField == null) return null;
            return s_RootItemField.GetValue(treeData) as TreeViewItem;
        }

        public static void SetRootItem(object treeData, TreeViewItem rootItem)
        {
            if (treeData != null && s_RootItemField != null)
            {
                s_RootItemField.SetValue(treeData, rootItem);
            }
        }

        public static IList<TreeViewItem> GetRows(object treeData)
        {
            if (treeData == null || s_RowsField == null) return null;
            return s_RowsField.GetValue(treeData) as IList<TreeViewItem>;
        }

        public static void SetRows(object treeData, IList<TreeViewItem> rows)
        {
            if (treeData != null && s_RowsField != null)
            {
                s_RowsField.SetValue(treeData, rows);
            }
        }

        public static void SetNeedRefreshRows(object treeData, bool value)
        {
            if (treeData != null && s_NeedRefreshRowsField != null)
            {
                s_NeedRefreshRowsField.SetValue(treeData, value);
            }
        }

        public static bool GetNeedRefreshRows(object treeData)
        {
            if (treeData != null && s_NeedRefreshRowsField != null)
            {
                try
                {
                    return (bool)s_NeedRefreshRowsField.GetValue(treeData);
                }
                catch { }
            }
            return false;
        }

        public static void RepaintFolderTree(object folderTree)
        {
            if (folderTree != null && s_RepaintMethod != null)
            {
                s_RepaintMethod.Invoke(folderTree, null);
            }
        }

        public static void ReloadFolderTree(object folderTree)
        {
            if (folderTree != null && s_ReloadDataMethod != null)
            {
                s_ReloadDataMethod.Invoke(folderTree, null);
            }
        }

        public static void SetSelection(object folderTree, int[] selectedIDs, bool revealAndFrame)
        {
            if (folderTree != null && s_SetSelectionMethod != null)
            {
                s_SetSelectionMethod.Invoke(folderTree, new object[] { selectedIDs, revealAndFrame });
            }
        }

        public static void ShowFolderContents(EditorWindow projectBrowser, int folderInstanceID, bool revealAndFrame)
        {
            if (projectBrowser != null && s_ShowFolderContentsMethod != null)
            {
                s_ShowFolderContentsMethod.Invoke(projectBrowser, new object[] { folderInstanceID, revealAndFrame });
            }
        }

        public static Action<int, Rect> GetOnGUIRowCallback(object folderTree)
        {
            if (folderTree == null) return null;
            if (s_OnGUIRowCallbackProp != null)
                return s_OnGUIRowCallbackProp.GetValue(folderTree) as Action<int, Rect>;
            if (s_OnGUIRowCallbackField != null)
                return s_OnGUIRowCallbackField.GetValue(folderTree) as Action<int, Rect>;
            return null;
        }

        public static void SetOnGUIRowCallback(object folderTree, Action<int, Rect> callback)
        {
            if (folderTree == null) return;
            if (s_OnGUIRowCallbackProp != null && s_OnGUIRowCallbackProp.CanWrite)
                s_OnGUIRowCallbackProp.SetValue(folderTree, callback);
            else if (s_OnGUIRowCallbackField != null)
                s_OnGUIRowCallbackField.SetValue(folderTree, callback);
        }

        public static Action<int[]> GetSelectionChangedCallback(object folderTree)
        {
            if (folderTree == null) return null;
            if (s_SelectionChangedCallbackProp != null)
                return s_SelectionChangedCallbackProp.GetValue(folderTree) as Action<int[]>;
            if (s_SelectionChangedCallbackField != null)
                return s_SelectionChangedCallbackField.GetValue(folderTree) as Action<int[]>;
            return null;
        }

        public static void SetSelectionChangedCallback(object folderTree, Action<int[]> callback)
        {
            if (folderTree == null) return;
            if (s_SelectionChangedCallbackProp != null && s_SelectionChangedCallbackProp.CanWrite)
                s_SelectionChangedCallbackProp.SetValue(folderTree, callback);
            else if (s_SelectionChangedCallbackField != null)
                s_SelectionChangedCallbackField.SetValue(folderTree, callback);
        }

        public static Action GetExpandedStateChangedCallback(object folderTree)
        {
            if (folderTree == null) return null;
            if (s_ExpandedStateChangedProp != null)
                return s_ExpandedStateChangedProp.GetValue(folderTree) as Action;
            if (s_ExpandedStateChangedField != null)
                return s_ExpandedStateChangedField.GetValue(folderTree) as Action;
            return null;
        }

        public static void SetExpandedStateChangedCallback(object folderTree, Action callback)
        {
            if (folderTree == null) return;
            if (s_ExpandedStateChangedProp != null && s_ExpandedStateChangedProp.CanWrite)
                s_ExpandedStateChangedProp.SetValue(folderTree, callback);
            else if (s_ExpandedStateChangedField != null)
                s_ExpandedStateChangedField.SetValue(folderTree, callback);
        }

        public static Action<int> GetItemDoubleClickedCallback(object folderTree)
        {
            if (folderTree == null) return null;
            if (s_ItemDoubleClickedCallbackProp != null)
                return s_ItemDoubleClickedCallbackProp.GetValue(folderTree) as Action<int>;
            if (s_ItemDoubleClickedCallbackField != null)
                return s_ItemDoubleClickedCallbackField.GetValue(folderTree) as Action<int>;
            return null;
        }

        public static void SetItemDoubleClickedCallback(object folderTree, Action<int> callback)
        {
            if (folderTree == null) return;
            if (s_ItemDoubleClickedCallbackProp != null && s_ItemDoubleClickedCallbackProp.CanWrite)
                s_ItemDoubleClickedCallbackProp.SetValue(folderTree, callback);
            else if (s_ItemDoubleClickedCallbackField != null)
                s_ItemDoubleClickedCallbackField.SetValue(folderTree, callback);
        }

        public static void RegisterImportsRootInDataSource(object treeData, TreeViewItem importsItem)
        {
            if (treeData == null || importsItem == null || s_RootsTreeViewItemField == null) return;
            try
            {
                var dict = s_RootsTreeViewItemField.GetValue(treeData) as System.Collections.IDictionary;
                if (dict != null)
                {
                    dict["Imports"] = importsItem;
                }
            }
            catch { }
        }

        public static void HookOnVisibleRowsChanged(object treeData, Action callback)
        {
            if (treeData != null && s_OnVisibleRowsChangedField != null)
            {
                var current = s_OnVisibleRowsChangedField.GetValue(treeData) as Action;
                s_OnVisibleRowsChangedField.SetValue(treeData, current + callback);
            }
        }

        public static TreeViewItem CreateRootTreeItem(int id, int depth, TreeViewItem parent, string displayName)
        {
            if (s_RootTreeItemType != null)
            {
                try
                {
                    return (TreeViewItem)Activator.CreateInstance(s_RootTreeItemType, new object[] { id, depth, parent, displayName });
                }
                catch { }
            }

            var fallback = new TreeViewItem(id, depth, displayName);
            fallback.parent = parent;
            return fallback;
        }

        public static Texture2D GetImportsFolderIcon()
        {
            if (s_ImportsFolderIcon == null)
            {
                s_ImportsFolderIcon = EditorGUIUtility.FindTexture("Folder Icon");
                if (s_ImportsFolderIcon == null)
                {
                    var iconContent = EditorGUIUtility.IconContent("FolderOpened Icon");
                    if (iconContent != null && iconContent.image != null)
                    {
                        s_ImportsFolderIcon = iconContent.image as Texture2D;
                    }
                }
            }
            return s_ImportsFolderIcon;
        }
    }
}
