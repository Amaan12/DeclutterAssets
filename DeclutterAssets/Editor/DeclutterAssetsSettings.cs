using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeclutterAssets.Editor
{
    [Serializable]
    public class DeclutterAssetsSettings
    {
        private const string SettingsFilePath = "ProjectSettings/DeclutterAssetsSettings.json";
        private const string LegacySettingsFilePath = "ProjectSettings/VirtualImportsSettings.json";

        [SerializeField]
        private List<string> m_ImportedPaths = new List<string>();

        private HashSet<string> m_NormalizedPathsCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static DeclutterAssetsSettings s_Instance;

        public static event Action OnSettingsChanged;

        public static DeclutterAssetsSettings Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    LoadOrCreate();
                }
                return s_Instance;
            }
        }

        public IReadOnlyList<string> ImportedPaths => m_ImportedPaths;

        public static void LoadOrCreate()
        {
            s_Instance = new DeclutterAssetsSettings();

            string loadPath = File.Exists(SettingsFilePath) ? SettingsFilePath : 
                             (File.Exists(LegacySettingsFilePath) ? LegacySettingsFilePath : null);

            if (!string.IsNullOrEmpty(loadPath))
            {
                try
                {
                    string json = File.ReadAllText(loadPath);
                    EditorJsonUtility.FromJsonOverwrite(json, s_Instance);
                    s_Instance.RebuildCache();
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DeclutterAssets] Failed to load settings from {loadPath}: {ex.Message}. Re-initializing.");
                }
            }

            // Default seed: If Assets/ImportedAssets exists, pre-seed it into Imports
            if (AssetDatabase.IsValidFolder("Assets/ImportedAssets"))
            {
                s_Instance.m_ImportedPaths.Add("Assets/ImportedAssets");
            }

            s_Instance.RebuildCache();
            s_Instance.Save();
        }

        public void Save()
        {
            try
            {
                string json = EditorJsonUtility.ToJson(this, true);
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(SettingsFilePath, json);
                RebuildCache();
                OnSettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeclutterAssets] Failed to save settings to {SettingsFilePath}: {ex.Message}");
            }
        }

        private void RebuildCache()
        {
            m_NormalizedPathsCache.Clear();
            if (m_ImportedPaths != null)
            {
                for (int i = 0; i < m_ImportedPaths.Count; i++)
                {
                    string normalized = NormalizePath(m_ImportedPaths[i]);
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        m_NormalizedPathsCache.Add(normalized);
                    }
                }
            }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return path.Replace('\\', '/').Trim().TrimEnd('/');
        }

        public bool IsDirectImport(string path)
        {
            string normalized = NormalizePath(path);
            return m_NormalizedPathsCache.Contains(normalized);
        }

        public bool IsImport(string path)
        {
            string normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized)) return false;

            if (m_NormalizedPathsCache.Contains(normalized)) return true;

            foreach (var importRoot in m_NormalizedPathsCache)
            {
                if (normalized.StartsWith(importRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool AddImport(string path)
        {
            string normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized) || !normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!m_NormalizedPathsCache.Contains(normalized))
            {
                m_ImportedPaths.Add(normalized);
                Save();
                return true;
            }
            return false;
        }

        public bool RemoveImport(string path)
        {
            string normalized = NormalizePath(path);
            bool removed = false;

            for (int i = m_ImportedPaths.Count - 1; i >= 0; i--)
            {
                if (string.Equals(NormalizePath(m_ImportedPaths[i]), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    m_ImportedPaths.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                Save();
                return true;
            }
            return false;
        }

        public void ToggleImport(string path)
        {
            if (IsDirectImport(path))
            {
                RemoveImport(path);
            }
            else
            {
                AddImport(path);
            }
        }
    }
}
