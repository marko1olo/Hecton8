using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class ConstructionCatalogValidator
    {
        private const string ConstructionDataRoot = "Assets/_Project/Data/Construction";

        [MenuItem("Hecton/Validation/Validate Construction Catalog", priority = 240)]
        public static void ValidateConstructionCatalog()
        {
            string[] buildableGuids = AssetDatabase.FindAssets("t:BuildableData", new[] { ConstructionDataRoot });
            string[] catalogGuids = AssetDatabase.FindAssets("t:ModuleCatalog", new[] { ConstructionDataRoot });

            int errorCount = 0;
            int warningCount = 0;
            HashSet<string> moduleNames = new HashSet<string>(System.StringComparer.Ordinal);

            for (int i = 0; i < buildableGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(buildableGuids[i]);
                BuildableData data = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
                if (data == null)
                    continue;

                ValidateBuildable(path, data, moduleNames, ref errorCount, ref warningCount);
            }

            for (int i = 0; i < catalogGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
                ModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(path);
                if (catalog == null)
                    continue;

                ValidateCatalog(path, catalog, ref errorCount, ref warningCount);
            }

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[ConstructionValidation] PASS no issues found.");
                return;
            }

            Debug.LogWarning($"[ConstructionValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static void ValidateBuildable(
            string path,
            BuildableData data,
            HashSet<string> moduleNames,
            ref int errorCount,
            ref int warningCount)
        {
            if (string.IsNullOrWhiteSpace(data.moduleName))
            {
                Debug.LogError($"[ConstructionValidation] Buildable missing moduleName: {path}", data);
                errorCount++;
            }
            else if (!moduleNames.Add(data.moduleName))
            {
                Debug.LogError($"[ConstructionValidation] Duplicate moduleName '{data.moduleName}': {path}", data);
                errorCount++;
            }

            if (data.ghostPrefab == null)
            {
                Debug.LogError($"[ConstructionValidation] Missing ghostPrefab: {path}", data);
                errorCount++;
            }

            if (data.finalPrefab == null)
            {
                Debug.LogError($"[ConstructionValidation] Missing finalPrefab: {path}", data);
                errorCount++;
            }

            if (data.buildCost == null || data.buildCost.Count <= 0)
            {
                Debug.LogWarning($"[ConstructionValidation] No buildCost entries: {path}", data);
                warningCount++;
            }
            else
            {
                for (int i = 0; i < data.buildCost.Count; i++)
                {
                    InventoryCost cost = data.buildCost[i];
                    if (cost == null)
                    {
                        Debug.LogWarning($"[ConstructionValidation] Null buildCost entry #{i} in {path}", data);
                        warningCount++;
                        continue;
                    }

                    if (cost.item == null)
                    {
                        Debug.LogError($"[ConstructionValidation] buildCost item missing at index {i}: {path}", data);
                        errorCount++;
                    }

                    if (cost.amount <= 0)
                    {
                        Debug.LogError($"[ConstructionValidation] buildCost amount <= 0 at index {i}: {path}", data);
                        errorCount++;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(data.description))
            {
                Debug.LogWarning($"[ConstructionValidation] Description is empty: {path}", data);
                warningCount++;
            }
        }

        private static void ValidateCatalog(
            string path,
            ModuleCatalog catalog,
            ref int errorCount,
            ref int warningCount)
        {
            IReadOnlyList<BuildableData> modules = catalog.Modules;
            if (modules == null || modules.Count <= 0)
            {
                Debug.LogError($"[ConstructionValidation] ModuleCatalog is empty: {path}", catalog);
                errorCount++;
                return;
            }

            HashSet<BuildableData> unique = new HashSet<BuildableData>();
            for (int i = 0; i < modules.Count; i++)
            {
                BuildableData module = modules[i];
                if (module == null)
                {
                    Debug.LogError($"[ConstructionValidation] Null module entry #{i}: {path}", catalog);
                    errorCount++;
                    continue;
                }

                if (!unique.Add(module))
                {
                    Debug.LogWarning(
                        $"[ConstructionValidation] Duplicate module reference '{module.moduleName}' in catalog: {path}",
                        catalog);
                    warningCount++;
                }
            }
        }
    }
}
