using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Build-time third-party bloat stripper for package demo/sample/documentation folders.
    /// </summary>
    public sealed class ThirdPartyStrippingGuard : IPreprocessBuildWithReport
    {
        private static readonly string[] _thirdPartyRootNames =
        {
            "Crest",
            "MapMagic",
            "Steamworks",
            "GPUInstancer",
            "AstarPathfindingProject",
            "Feel"
        };

        private static readonly string[] _bloatFolderNames =
        {
            "Demo",
            "Demos",
            "Example",
            "Sample",
            "Samples",
            "Documentation",
            "Documentations",
            "Docs",
            "Examples",
            "Resources"
        };

        private static readonly string[] _stripFolderNames =
        {
            "Demo",
            "Demos",
            "Example",
            "Examples",
            "Sample",
            "Samples",
            "Documentation",
            "Documentations",
            "Docs"
        };

        public int callbackOrder => -900;

        public void OnPreprocessBuild(BuildReport report)
        {
            StripThirdPartyBloat(logWarnings: true);
            Audit(logWarnings: true);
        }

        [MenuItem("Tools/Hecton8/Third Party/Audit")]
        private static void AuditFromMenu()
        {
            Audit(logWarnings: true);
        }

        [MenuItem("Tools/Hecton8/Third Party/Strip Build Bloat")]
        private static void StripFromMenu()
        {
            StripThirdPartyBloat(logWarnings: true);
        }

        private static void Audit(bool logWarnings)
        {
            string assetsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
            string thirdPartyPath = Path.Combine(assetsPath, "_ThirdParty");

            for (int i = 0; i < _thirdPartyRootNames.Length; i++)
            {
                string rootName = _thirdPartyRootNames[i];
                string directPath = Path.Combine(assetsPath, rootName);
                string canonicalPath = Path.Combine(thirdPartyPath, rootName);
                if (Directory.Exists(directPath) && !IsUnderDirectory(directPath, thirdPartyPath) && logWarnings)
                    Debug.LogWarning("[ThirdPartyStrippingGuard] Third-party root outside Assets/_ThirdParty: Assets/" + rootName);

                AuditBloatFolders(directPath, logWarnings);
                AuditBloatFolders(canonicalPath, logWarnings);
            }
        }

        private static void StripThirdPartyBloat(bool logWarnings)
        {
            string thirdPartyPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_ThirdParty");
            if (!Directory.Exists(thirdPartyPath))
                return;

            string normalizedThirdPartyPath = NormalizeDirectoryPath(thirdPartyPath);
            string[] directories = Directory.GetDirectories(thirdPartyPath, "*", SearchOption.AllDirectories);
            Array.Sort(directories, (left, right) => right.Length.CompareTo(left.Length));

            int removedCount = 0;
            for (int i = 0; i < directories.Length; i++)
            {
                string candidate = directories[i];
                if (!IsUnderDirectory(candidate, normalizedThirdPartyPath))
                    continue;

                string folderName = Path.GetFileName(candidate);
                if (!ShouldStripFolder(folderName))
                    continue;

                DeleteAssetFolder(candidate);
                removedCount++;
                if (logWarnings)
                    Debug.Log("[ThirdPartyStrippingGuard] Removed build bloat folder: " + ToAssetPath(candidate));
            }

            if (removedCount > 0)
                AssetDatabase.Refresh();
        }

        private static void AuditBloatFolders(string pluginRoot, bool logWarnings)
        {
            if (!Directory.Exists(pluginRoot))
                return;

            for (int i = 0; i < _bloatFolderNames.Length; i++)
            {
                string candidate = Path.Combine(pluginRoot, _bloatFolderNames[i]);
                if (!Directory.Exists(candidate))
                    continue;

                string relative = ToAssetPath(candidate);
                if (logWarnings)
                    Debug.LogWarning("[ThirdPartyStrippingGuard] Audit folder before release build: " + relative);
            }
        }

        private static bool ShouldStripFolder(string folderName)
        {
            for (int i = 0; i < _stripFolderNames.Length; i++)
            {
                if (string.Equals(folderName, _stripFolderNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void DeleteAssetFolder(string absolutePath)
        {
            string assetPath = ToAssetPath(absolutePath);
            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                FileUtil.DeleteFileOrDirectory(absolutePath);
                FileUtil.DeleteFileOrDirectory(absolutePath + ".meta");
            }
        }

        private static bool IsUnderDirectory(string path, string root)
        {
            string normalizedPath = NormalizeDirectoryPath(path);
            string normalizedRoot = NormalizeDirectoryPath(root);
            return normalizedPath.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(
                       normalizedRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectoryPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string relative = absolutePath.StartsWith(projectRoot)
                ? absolutePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : absolutePath;

            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
