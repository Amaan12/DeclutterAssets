using System;
using System.Collections.Generic;
using UnityEditor;
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
            public bool LastSkipHiddenPackages;
            public EventCallback<PointerUpEvent> OnPointerUp;
            public EventCallback<PointerDownEvent> OnPointerDown;
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
                binding.FolderTree = activeTree;
                DeclutterAssetsTreePatcher.PatchTree(activeTree, forceRepatch: true);
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

                binding.OnPointerUp = (evt) => CheckAndPatchSingleBinding(binding);
                binding.OnPointerDown = (evt) => CheckAndPatchSingleBinding(binding);
                binding.OnClick = (evt) => CheckAndPatchSingleBinding(binding);

                root.RegisterCallback(binding.OnPointerUp);
                root.RegisterCallback(binding.OnPointerDown);
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

                if (binding.OnPointerUp != null)
                    root.UnregisterCallback(binding.OnPointerUp);
                if (binding.OnPointerDown != null)
                    root.UnregisterCallback(binding.OnPointerDown);
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
            };
            DeclutterAssetsUtility.SetSelectionChangedCallback(folderTree, hookedSelection);

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
                LastSkipHiddenPackages = DeclutterAssetsUtility.GetSkipHiddenPackages(window)
            };

            HookVisualElement(binding);
            DeclutterAssetsTreePatcher.PatchTree(folderTree, forceRepatch: true);

            return binding;
        }

        private static void OnProjectChanged()
        {
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
