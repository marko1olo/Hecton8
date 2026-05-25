#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Build-time duplicate .meta GUID guard. Duplicates must be merged through Unity asset moves, not text edits.
    /// </summary>
    public sealed class AssetGuidIntegrityChecker : IPreprocessBuildWithReport
    {
        private const string AssetRoot = "Assets";
        private const int MaxReportedDuplicates = 64;

        public int callbackOrder => -880;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!TryValidate(out string message))
                throw new BuildFailedException(message);
        }

        [MenuItem("Tools/Hecton8/Assets/Check Duplicate GUIDs")]
        private static void CheckFromMenu()
        {
            if (!TryValidate(out string message))
                UnityEngine.Debug.LogError(message);
            else
                UnityEngine.Debug.Log("[AssetGuidIntegrityChecker] Duplicate asset GUIDs: 0");
        }

        internal static bool TryValidate(out string message)
        {
            message = string.Empty;
            if (!Directory.Exists(AssetRoot))
                return true;

            List<string> metas = new List<string>(Directory.EnumerateFiles(AssetRoot, "*.meta", SearchOption.AllDirectories));
            metas.Sort(StringComparer.Ordinal);
            Dictionary<string, string> firstPathByGuid = new Dictionary<string, string>(metas.Count, StringComparer.Ordinal);
            StringBuilder builder = null;
            int duplicateCount = 0;

            for (int i = 0; i < metas.Count; i++)
            {
                string metaPath = metas[i];
                if (!TryReadGuid(metaPath, out string guid))
                    continue;

                if (!firstPathByGuid.TryGetValue(guid, out string firstPath))
                {
                    firstPathByGuid.Add(guid, metaPath);
                    continue;
                }

                duplicateCount++;
                if (duplicateCount > MaxReportedDuplicates)
                    continue;

                builder ??= new StringBuilder(4096);
                builder.Append(" - ");
                builder.Append(guid);
                builder.Append(": ");
                builder.Append(firstPath.Replace('\\', '/'));
                builder.Append(" <-> ");
                builder.Append(metaPath.Replace('\\', '/'));
                builder.AppendLine();
            }

            if (duplicateCount == 0)
                return true;

            builder ??= new StringBuilder(256);
            builder.Insert(0, "[AssetGuidIntegrityChecker] Duplicate .meta GUIDs found. Merge imported duplicate assets through Unity AssetDatabase before building.\n");
            if (duplicateCount > MaxReportedDuplicates)
            {
                builder.Append("... additional duplicate GUID pairs: ");
                builder.Append(duplicateCount - MaxReportedDuplicates);
                builder.AppendLine();
            }

            message = builder.ToString();
            return false;
        }

        private static bool TryReadGuid(string metaPath, out string guid)
        {
            guid = string.Empty;
            using (StreamReader reader = new StreamReader(metaPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!line.StartsWith("guid:", StringComparison.Ordinal))
                        continue;

                    guid = line.Substring(5).Trim().ToLowerInvariant();
                    return guid.Length != 0;
                }
            }

            return false;
        }
    }
}
#endif
