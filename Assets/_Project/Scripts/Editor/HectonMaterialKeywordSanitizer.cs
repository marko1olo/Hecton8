#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Strips blank and duplicate shader keywords from first-party materials.
    /// This is a material-hygiene pass, not a runtime shader-variant stripper.
    /// </summary>
    internal static class HectonMaterialKeywordSanitizer
    {
        private static readonly string[] MaterialRoots =
        {
            "Assets/_Project/Art/Materials"
        };

        [MenuItem("Hecton/Validation/Asset Pipeline/Sanitize Material Keywords", priority = 197)]
        private static void SanitizeKeywords()
        {
            string[] guids = AssetDatabase.FindAssets("t:Material", MaterialRoots);
            int scannedCount = 0;
            int changedCount = 0;
            int removedKeywordCount = 0;
            StringBuilder report = new StringBuilder(512);

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                    if (material == null)
                        continue;

                    scannedCount++;
                    string[] keywords = material.shaderKeywords;
                    if (keywords == null || keywords.Length <= 0)
                        continue;

                    List<string> sanitizedKeywords = new List<string>(keywords.Length);
                    HashSet<string> seenKeywords = new HashSet<string>();
                    int removedForMaterial = 0;

                    for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
                    {
                        string keyword = keywords[keywordIndex];
                        if (string.IsNullOrWhiteSpace(keyword) || !seenKeywords.Add(keyword))
                        {
                            removedForMaterial++;
                            continue;
                        }

                        sanitizedKeywords.Add(keyword);
                    }

                    if (removedForMaterial <= 0)
                        continue;

                    material.shaderKeywords = sanitizedKeywords.ToArray();
                    EditorUtility.SetDirty(material);
                    changedCount++;
                    removedKeywordCount += removedForMaterial;

                    if (report.Length < 4000)
                    {
                        report.Append(assetPath)
                            .Append(" removed=")
                            .Append(removedForMaterial)
                            .Append('\n');
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[HectonMaterialKeywordSanitizer] scanned=" + scannedCount +
                ", changed=" + changedCount +
                ", removedKeywords=" + removedKeywordCount +
                (report.Length > 0 ? "\n" + report : string.Empty));
        }
    }
}
#endif
