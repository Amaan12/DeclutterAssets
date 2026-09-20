using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeclutterAssets.Editor
{
    [InitializeOnLoad]
    public static class DeclutterAssetsProjectBrowserHook
    {
        private class BrowserBinding
        {
            public EditorWindow Window;
            public object FolderTree;
            public DeclutterAssetsDragDrop DragDrop;
            public Action<int, Rect> CombinedRowGUI;
            public Action<int[]> CombinedSelection;
            public Action CombinedExpanded;
            public Action<int> CombinedDoubleClick;
            public bool LastSkipHiddenPackages;
            public EventCallback<ClickEvent> OnClick;
        }

        private static readonly Dictionary<EditorWindow, BrowserBinding> s_Bindings = new Dictionary<EditorWindow, BrowserBinding>();
        private static double s_LastScanTime = 0;
        private const double ScanInterval = 0.5; // Throttled scan for newly opened windows

        static DeclutterAssetsProjectBrowserHook()
        {
            DeclutterAssetsUtility.InitializeReflection();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged += OnProjectChanged;
            DeclutterAssetsSettings.OnSettingsChanged += OnSettingsChanged;
        }

        private static void OnEditorUpdate()
        {
            // 1. Instant check on every tick for all bound windows (sub-microsecond)
            CheckAndPatchBoundBrowsers();

            // 2. Throttled scan for newly opened/closed windows
            double now = EditorApplication.timeSinceStartup;
            if (now - s_LastScanTime < ScanInterval)
            {
                return;
            }
            s_LastScanTime = now;

            CleanDeadBindings();
            ScanAndHookBrowsers();
        }

        private static void CheckAndPatchBoundBrowsers()
        {
            foreach (var binding in s_Bindings.Values)
            {
                CheckAndPatchSingleBinding(binding);
            }
        }

        private static void CheckAndPatchSingleBinding(BrowserBinding binding)
        {
            if (binding == null || binding.Window == null) return;

            object activeTree = DeclutterAssetsUtility.GetActiveTree(binding.Window);
            if (activeTree == null) return;

            // If active tree instance changed (e.g. view mode toggle between one-column and two-column)
            if (binding.FolderTree != activeTree)
            {
                UnhookVisualElement(binding);
                binding = HookBrowser(binding.Window, activeTree);
                s_Bindings[binding.Window] = binding;
                return;
            }

            // If packages visibility button was clicked
            bool currentSkip = DeclutterAssetsUtility.GetSkipHiddenPackages(binding.Window);
            if (currentSkip != binding.LastSkipHiddenPackages)
            {
                binding.LastSkipHiddenPackages = currentSkip;
                DeclutterAssetsTreePatcher.PatchTree(activeTree, forceRepatch: true);
                return;
            }

            // If tree rows or structure was invalidated
            if (DeclutterAssetsTreePatcher.NeedsPatch(activeTree))
            {
                DeclutterAssetsTreePatcher.PatchTree(activeTree, forceRepatch: true);
            }
        }

        private static void HookVisualElement(BrowserBinding binding)
        {
            if (binding == null || binding.Window == null) return;

            try
            {
                var root = binding.Window.rootVisualElement;
                if (root == null) return;

                binding.OnClick = (evt) =>
                {
                    // Ignore clicks within IMGUIContainer (tree view, list area)
                    // so we do not disrupt IMGUI double-click timing or selection
                    if (evt.target is IMGUIContainer) return;
                    CheckAndPatchSingleBinding(binding);
                };

                root.RegisterCallback(binding.OnClick);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DeclutterAssets] Could not hook visual element: {ex.Message}");
            }
        }

        private static void UnhookVisualElement(BrowserBinding binding)
        {
            if (binding == null || binding.Window == null) return;

            try
            {
                var root = binding.Window.rootVisualElement;
                if (root == null) return;

                if (binding.OnClick != null)
                    root.UnregisterCallback(binding.OnClick);
            }
            catch { }
        }

        private static void CleanDeadBindings()
        {
            var deadWindows = new List<EditorWindow>();
            foreach (var kvp in s_Bindings)
            {
                if (kvp.Key == null)
                {
                    deadWindows.Add(kvp.Key);
                }
            }

            for (int i = 0; i < deadWindows.Count; i++)
            {
                if (s_Bindings.TryGetValue(deadWindows[i], out var binding))
                {
                    UnhookVisualElement(binding);
                }
                s_Bindings.Remove(deadWindows[i]);
            }
        }

        public static void ScanAndHookBrowsers()
        {
            var browserType = DeclutterAssetsUtility.ProjectBrowserType;
            if (browserType == null)
            {
                DeclutterAssetsUtility.InitializeReflection();
                browserType = DeclutterAssetsUtility.ProjectBrowserType;
                if (browserType == null) return;
            }

            var browsers = Resources.FindObjectsOfTypeAll(browserType);
            if (browsers == null || browsers.Length == 0) return;

            for (int i = 0; i < browsers.Length; i++)
            {
                var window = browsers[i] as EditorWindow;
                if (window == null) continue;

                object folderTree = DeclutterAssetsUtility.GetActiveTree(window);
                if (folderTree == null) continue;

                if (!s_Bindings.TryGetValue(window, out var binding) || binding.FolderTree != folderTree)
                {
                    if (binding != null)
                    {
                        UnhookVisualElement(binding);
                    }
                    binding = HookBrowser(window, folderTree);
                    s_Bindings[window] = binding;
                }
                else
                {
                    if (DeclutterAssetsTreePatcher.NeedsPatch(folderTree))
                    {
                        DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);
                    }
                }
            }
        }

        private static BrowserBinding HookBrowser(EditorWindow window, object folderTree)
        {
            var dragDrop = new DeclutterAssetsDragDrop(window, folderTree);

            var originalRowGUI = DeclutterAssetsUtility.GetOnGUIRowCallback(folderTree);
            Action<int, Rect> hookedRowGUI = (id, rect) =>
            {
                try
                {
                    originalRowGUI?.Invoke(id, rect);
                }
                catch { }

                try
                {
                    dragDrop.HandleRowGUI(id, rect);
                }
                catch { }
            };
            DeclutterAssetsUtility.SetOnGUIRowCallback(folderTree, hookedRowGUI);

            var originalSelection = DeclutterAssetsUtility.GetSelectionChangedCallback(folderTree);
            Action<int[]> hookedSelection = (selectedIDs) =>
            {
                TreeViewState state = DeclutterAssetsUtility.GetTreeState(folderTree);
                int assetsId = DeclutterAssetsTreePatcher.GetAssetsFolderId();
                bool wasAssetsExpanded = state != null && state.expandedIDs != null && state.expandedIDs.Contains(assetsId);

                bool isImportSelection = false;
                if (selectedIDs != null && selectedIDs.Length > 0)
                {
                    int selId = selectedIDs[0];
                    if (selId == DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                    {
                        isImportSelection = true;
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(selId);
                        if (!string.IsNullOrEmpty(path))
                        {
                            if (DeclutterAssetsSettings.Instance.IsImport(path))
                            {
                                isImportSelection = true;
                            }

                            // If a file was selected in the tree, select it in Unity's global selection
                            if (File.Exists(path))
                            {
                                var fileObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                                if (fileObj != null)
                                {
                                    Selection.activeObject = fileObj;
                                }
                            }
                        }
                    }
                }

                if (isImportSelection && !wasAssetsExpanded)
                {
                    DeclutterAssetsTreePatcher.SuppressAssetsExpansion = true;
                }

                try
                {
                    originalSelection?.Invoke(selectedIDs);
                }
                catch { }

                try
                {
                    dragDrop.HandleSelectionChanged(selectedIDs);
                }
                catch { }
                finally
                {
                    if (isImportSelection && !wasAssetsExpanded)
                    {
                        DeclutterAssetsTreePatcher.SuppressAssetsExpansion = false;
                        if (state != null && state.expandedIDs != null && state.expandedIDs.Contains(assetsId))
                        {
                            state.expandedIDs.Remove(assetsId);
                            DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);
                            DeclutterAssetsUtility.RepaintFolderTree(folderTree);
                        }
                    }
                }
            };
            DeclutterAssetsUtility.SetSelectionChangedCallback(folderTree, hookedSelection);

            var originalExpanded = DeclutterAssetsUtility.GetExpandedStateChangedCallback(folderTree);
            Action hookedExpanded = () =>
            {
                try
                {
                    originalExpanded?.Invoke();
                }
                catch { }

                try
                {
                    DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);
                }
                catch { }
            };
            DeclutterAssetsUtility.SetExpandedStateChangedCallback(folderTree, hookedExpanded);

            var originalDoubleClick = DeclutterAssetsUtility.GetItemDoubleClickedCallback(folderTree);
            Action<int> hookedDoubleClick = (clickedId) =>
            {
                try
                {
                    originalDoubleClick?.Invoke(clickedId);
                }
                catch { }

                if (clickedId != 0 && clickedId != DeclutterAssetsUtility.IMPORTS_ROOT_ID)
                {
                    string path = AssetDatabase.GetAssetPath(clickedId);
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (File.Exists(path))
                        {
                            var assetObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (assetObj != null)
                            {
                                AssetDatabase.OpenAsset(assetObj);
                            }
                        }
                        else if (Directory.Exists(path) || AssetDatabase.IsValidFolder(path))
                        {
                            int viewMode = DeclutterAssetsUtility.GetViewMode(window);
                            if (viewMode == 1)
                            {
                                try
                                {
                                    DeclutterAssetsUtility.ShowFolderContents(window, clickedId, false);
                                }
                                catch { }
                            }
                        }
                    }
                }
            };
            DeclutterAssetsUtility.SetItemDoubleClickedCallback(folderTree, hookedDoubleClick);

            object treeData = DeclutterAssetsUtility.GetTreeData(folderTree);
            if (treeData != null)
            {
                DeclutterAssetsUtility.HookOnVisibleRowsChanged(treeData, () =>
                {
                    DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);
                });
            }

            var binding = new BrowserBinding
            {
                Window = window,
                FolderTree = folderTree,
                DragDrop = dragDrop,
                CombinedRowGUI = hookedRowGUI,
                CombinedSelection = hookedSelection,
                CombinedExpanded = hookedExpanded,
                CombinedDoubleClick = hookedDoubleClick,
                LastSkipHiddenPackages = DeclutterAssetsUtility.GetSkipHiddenPackages(window)
            };

            HookVisualElement(binding);
            DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);

            return binding;
        }

        private static void OnProjectChanged()
        {
            DeclutterAssetsTreePatcher.ClearCache();
            foreach (var binding in s_Bindings.Values)
            {
                if (binding.FolderTree != null)
                {
                    DeclutterAssetsTreePatcher.PatchTree(binding.FolderTree, forceRepatch: true);
                    DeclutterAssetsUtility.RepaintFolderTree(binding.FolderTree);
                }
            }
        }

        private static void OnSettingsChanged()
        {
            foreach (var binding in s_Bindings.Values)
            {
                if (binding.FolderTree != null)
                {
                    DeclutterAssetsUtility.ReloadFolderTree(binding.FolderTree);
                    DeclutterAssetsTreePatcher.PatchTree(binding.FolderTree, forceRepatch: true);
                    DeclutterAssetsUtility.RepaintFolderTree(binding.FolderTree);
                }
            }
        }

        #region Context Menus

        [MenuItem("Assets/Declutter/Move to Imports", false, 30)]
        private static void MoveSelectedToImports()
        {
            if (Selection.objects == null) return;
            var settings = DeclutterAssetsSettings.Instance;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    settings.AddImport(path);
                }
            }
        }

        [MenuItem("Assets/Declutter/Move to Imports", true)]
        private static bool ValidateMoveSelectedToImports()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return false;
            var settings = DeclutterAssetsSettings.Instance;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && !settings.IsImport(path))
                {
                    return true;
                }
            }
            return false;
        }

        [MenuItem("Assets/Declutter/Move to Assets", false, 31)]
        private static void MoveSelectedToAssets()
        {
            if (Selection.objects == null) return;
            var settings = DeclutterAssetsSettings.Instance;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    settings.RemoveImport(path);
                }
            }
        }

        [MenuItem("Assets/Declutter/Move to Assets", true)]
        private static bool ValidateMoveSelectedToAssets()
        {
            if (Selection.objects == null || Selection.objects.Length == 0) return false;
            var settings = DeclutterAssetsSettings.Instance;

            foreach (var obj in Selection.objects)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && settings.IsImport(path))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
