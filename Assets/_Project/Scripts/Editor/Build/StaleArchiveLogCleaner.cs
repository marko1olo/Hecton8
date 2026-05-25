#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Build-time hygiene pass for stale text logs in archive/recovery folders.
    /// </summary>
    public sealed class StaleArchiveLogCleaner : IPreprocessBuildWithReport
    {
        private const int RetentionDays = 7;

        private static readonly string[] _roots =
        {
            "Assets/_Recovery",
            "Assets/_Project/_Archive",
            "Docs/_Archive"
        };

        private static readonly string[] _extensions =
        {
            ".log",
            ".txt"
        };

        public int callbackOrder => -875;

        public void OnPreprocessBuild(BuildReport report)
        {
            Clean(logResult: true);
        }

        [MenuItem("Tools/Hecton8/Repository/Clean Stale Archive Logs")]
        private static void CleanFromMenu()
        {
            Clean(logResult: true);
        }

        internal static int Clean(bool logResult)
        {
            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-RetentionDays);
            int removed = 0;

            for (int rootIndex = 0; rootIndex < _roots.Length; rootIndex++)
            {
                string root = _roots[rootIndex];
                string absoluteRoot = Path.GetFullPath(root);
                if (!Directory.Exists(absoluteRoot))
                    continue;

                List<string> files = new List<string>(Directory.EnumerateFiles(absoluteRoot, "*.*", SearchOption.AllDirectories));
                foreach (string file in files)
                {
                    if (!IsSupportedExtension(file) || !IsUnderRoot(file, absoluteRoot))
                        continue;

                    DateTime modifiedUtc = File.GetLastWriteTimeUtc(file);
                    if (modifiedUtc >= cutoffUtc)
                        continue;

                    FileUtil.DeleteFileOrDirectory(file);
                    FileUtil.DeleteFileOrDirectory(file + ".meta");
                    removed++;
                }
            }

            if (removed > 0)
                AssetDatabase.Refresh();

            if (logResult)
                Debug.Log("[StaleArchiveLogCleaner] Removed stale archive/recovery log files: " + removed);

            return removed;
        }

        private static bool IsSupportedExtension(string path)
        {
            string extension = Path.GetExtension(path);
            for (int i = 0; i < _extensions.Length; i++)
            {
                if (string.Equals(extension, _extensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsUnderRoot(string file, string root)
        {
            string normalizedFile = Path.GetFullPath(file).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedFile.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
