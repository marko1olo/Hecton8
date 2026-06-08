using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Editor
{
    internal static class OrphanedComponentSweeper
    {
        private const string PrefabRoot = "Assets/_Project/Prefabs";
        private const string ReportFileName = "BROKEN_PREFABS.md";
        private static readonly Encoding ReportEncoding = new UTF8Encoding(false);

        [MenuItem("Hecton8/Tools/Run Orphaned Prefab Sweeper")]
        public static void Run()
        {
            string reportPath = ResolveReportPath();
            StringBuilder report = new StringBuilder(1024);
            report.AppendLine("# Broken Prefabs");
            report.AppendLine();
            report.AppendLine("| Prefab | Missing Scripts |");
            report.AppendLine("|---|---:|");

            int brokenPrefabCount = 0;
            int totalMissingScripts = 0;
            if (AssetDatabase.IsValidFolder(PrefabRoot))
            {
                string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
                Array.Sort(prefabGuids, StringComparer.Ordinal);
                for (int i = 0; i < prefabGuids.Length; i++)
                {
                    string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                        continue;

                    int missingCount = CountMissingScripts(prefab);
                    if (missingCount <= 0)
                        continue;

                    brokenPrefabCount++;
                    totalMissingScripts += missingCount;
                    report.Append("| ");
                    report.Append(prefabPath);
                    report.Append(" | ");
                    report.Append(missingCount);
                    report.AppendLine(" |");
                }
            }

            if (brokenPrefabCount == 0)
            {
                report.AppendLine("| None | 0 |");
            }

            report.AppendLine();
            report.Append("Broken prefab count: ");
            report.Append(brokenPrefabCount);
            report.AppendLine();
            report.Append("Total missing scripts: ");
            report.Append(totalMissingScripts);
            report.AppendLine();

            File.WriteAllText(reportPath, report.ToString(), ReportEncoding);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            H8Debug.Log("[OrphanedComponentSweeper] Wrote report: " + reportPath);
        }

        private static int CountMissingScripts(GameObject root)
        {
            if (root == null)
                return 0;

            int total = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            Transform transform = root.transform;
            for (int i = 0; i < transform.childCount; i++)
                total += CountMissingScripts(transform.GetChild(i).gameObject);

            return total;
        }

        private static string ResolveReportPath()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
            return Path.Combine(rootPath, ReportFileName);
        }
    }
}
