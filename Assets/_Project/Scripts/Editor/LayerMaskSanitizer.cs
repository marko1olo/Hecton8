#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Validation
{
    /// <summary>
    /// Sanitizes serialized LayerMask poison from HECTON-8 data assets.
    /// </summary>
    internal static class LayerMaskSanitizer
    {
        private const string DataRoot = "Assets/_Project/Data";
        private const string MenuPath = "Hecton-8/Validation/Sanitize Data LayerMasks";
        private const string EverythingUnsignedBits = "m_Bits: 4294967295";
        private const string EverythingSignedBits = "m_Bits: -1";
        private static readonly string[] DataRoots = { DataRoot };

        internal static bool IsSanitizing { get; private set; }

        [MenuItem(MenuPath, priority = 143)]
        private static void SanitizeFromMenu()
        {
            SanitizeDataLayerMasks(true);
        }

        internal static SanitizeResult SanitizeDataLayerMasks(bool saveAssets)
        {
            SanitizeResult result = new SanitizeResult();
            string[] scriptableObjectGuids = AssetDatabase.FindAssets("t:ScriptableObject", DataRoots);

            IsSanitizing = true;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < scriptableObjectGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(scriptableObjectGuids[i]);
                    if (!IsManagedDataAsset(assetPath))
                        continue;

                    ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (asset == null)
                        continue;

                    result.ScannedAssets++;
                    result.SanitizedLayerMaskProperties += SanitizeSerializedLayerMasks(asset, assetPath, result.SanitizedAssetPaths);
                }

                SanitizeYamlLayerBits(CollectDataAssetPaths(), result);

                if (saveAssets && result.SanitizedAssetPaths.Count > 0)
                    AssetDatabase.SaveAssets();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                IsSanitizing = false;
            }

            if (saveAssets && result.SanitizedAssetPaths.Count > 0)
                AssetDatabase.Refresh();

            Debug.Log(
                "[LayerMaskSanitizer] Scanned " +
                result.ScannedAssets.ToString(CultureInfo.InvariantCulture) +
                " ScriptableObject assets. Sanitized properties=" +
                result.SanitizedLayerMaskProperties.ToString(CultureInfo.InvariantCulture) +
                ", yamlFiles=" +
                result.SanitizedYamlFiles.ToString(CultureInfo.InvariantCulture) +
                ".");
            return result;
        }

        internal static int CountPoisonedDataAssets(IReadOnlyList<string> assetPaths, List<string> poisonedPaths)
        {
            int count = 0;
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                if (!IsManagedDataAsset(assetPath) || !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) == null)
                    continue;

                string absolutePath = ToAbsolutePath(assetPath);
                if (!File.Exists(absolutePath))
                    continue;

                if (!FileContainsEverythingMask(absolutePath))
                    continue;

                poisonedPaths.Add(assetPath);
                count++;
            }

            return count;
        }

        private static int SanitizeSerializedLayerMasks(
            UnityEngine.Object asset,
            string assetPath,
            List<string> sanitizedAssetPaths)
        {
            int sanitizedPropertyCount = 0;
            int replacementMask = SelectReplacementMask(asset, assetPath);
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyType != SerializedPropertyType.LayerMask)
                    continue;

                int currentValue = property.intValue;
                if (currentValue >= 0 && currentValue <= HectonLayerMasks.AllDefinedProjectLayersMask)
                    continue;

                property.intValue = replacementMask;
                sanitizedPropertyCount++;
            }

            if (sanitizedPropertyCount <= 0)
                return 0;

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AddUniquePath(sanitizedAssetPaths, assetPath);
            return sanitizedPropertyCount;
        }

        private static void SanitizeYamlLayerBits(IReadOnlyList<string> assetPaths, SanitizeResult result)
        {
            for (int i = 0; i < assetPaths.Count; i++)
            {
                string assetPath = assetPaths[i];
                string absolutePath = ToAbsolutePath(assetPath);
                if (!File.Exists(absolutePath))
                    continue;

                string originalText = File.ReadAllText(absolutePath);
                if (!ContainsEverythingMask(originalText))
                    continue;

                int replacementMask = SelectReplacementMask(null, assetPath);
                string replacementLine = "m_Bits: " + replacementMask.ToString(CultureInfo.InvariantCulture);
                string sanitizedText = originalText
                    .Replace(EverythingUnsignedBits, replacementLine)
                    .Replace(EverythingSignedBits, replacementLine);

                if (string.Equals(originalText, sanitizedText, StringComparison.Ordinal))
                    continue;

                File.WriteAllText(absolutePath, sanitizedText);
                result.SanitizedYamlFiles++;
                AddUniquePath(result.SanitizedAssetPaths, assetPath);
            }
        }

        private static string[] CollectDataAssetPaths()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, DataRoots);
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (IsManagedDataAsset(path) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }

            return paths.ToArray();
        }

        private static int SelectReplacementMask(UnityEngine.Object asset, string assetPath)
        {
            if (IsResourceNodeTemplateAsset(asset, assetPath))
                return HectonLayerMasks.TerrainSdfProbeLayerMask;

            return IsTargetTemplateAsset(asset, assetPath)
                ? HectonLayerMasks.DataTemplateAuthoringMask
                : HectonLayerMasks.AllDefinedProjectLayersMask;
        }

        private static bool IsResourceNodeTemplateAsset(UnityEngine.Object asset, string assetPath)
        {
            string typeName = asset != null ? asset.GetType().Name : string.Empty;
            if (string.Equals(typeName, "ResourceNodeTemplate", StringComparison.Ordinal))
                return true;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return fileName.StartsWith("ResourceNodeTemplate_", StringComparison.Ordinal);
        }

        private static bool IsTargetTemplateAsset(UnityEngine.Object asset, string assetPath)
        {
            string typeName = asset != null ? asset.GetType().Name : string.Empty;
            if (IsTargetTemplateTypeName(typeName))
                return true;

            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return IsTargetTemplateTypeNamePrefix(fileName);
        }

        private static bool IsTargetTemplateTypeName(string typeName)
        {
            return string.Equals(typeName, "ResourceNodeTemplate", StringComparison.Ordinal) ||
                   string.Equals(typeName, "FaunaDataTemplate", StringComparison.Ordinal) ||
                   string.Equals(typeName, "FloraDataTemplate", StringComparison.Ordinal);
        }

        private static bool IsTargetTemplateTypeNamePrefix(string fileName)
        {
            return fileName.StartsWith("ResourceNodeTemplate_", StringComparison.Ordinal) ||
                   fileName.StartsWith("FaunaDataTemplate_", StringComparison.Ordinal) ||
                   fileName.StartsWith("FloraDataTemplate_", StringComparison.Ordinal);
        }

        private static bool IsManagedDataAsset(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.StartsWith(DataRoot + "/", StringComparison.Ordinal);
        }

        private static bool ContainsEverythingMask(string text)
        {
            return text.IndexOf(EverythingUnsignedBits, StringComparison.Ordinal) >= 0 ||
                   text.IndexOf(EverythingSignedBits, StringComparison.Ordinal) >= 0;
        }

        private static bool FileContainsEverythingMask(string absolutePath)
        {
            foreach (string line in File.ReadLines(absolutePath))
            {
                if (ContainsEverythingMask(line))
                    return true;
            }

            return false;
        }

        private static void AddUniquePath(List<string> paths, string assetPath)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (string.Equals(paths[i], assetPath, StringComparison.Ordinal))
                    return;
            }

            paths.Add(assetPath);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        internal sealed class SanitizeResult
        {
            internal readonly List<string> SanitizedAssetPaths = new List<string>(64);
            internal int ScannedAssets;
            internal int SanitizedLayerMaskProperties;
            internal int SanitizedYamlFiles;
        }
    }
}
#endif
