using Hecton8.Core;
using Hecton8.World;
using Hecton8.Environment;
using MapMagic.Core;
using System;
using System.Collections.Generic;
using System.IO;
using GPUInstancer;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class MapMagicWorldValidator
    {
        private const string MapMagicSerializedBlockPrefix = "\n    - refId:";

        private static readonly string[] ForbiddenMapMagicGraphNodeTypeTokens =
        {
            "MapMagic.Nodes.MatrixGenerators.Blur200",
            "MapMagic.Nodes.MatrixGenerators.Erosion200",
            "MapMagic.Nodes.MatrixGenerators.Cavity200",
            "MapMagic.Nodes.MatrixGenerators.Terrace200",
            "MapMagic.Nodes.MatrixGenerators.HectonHydraulicErosionMapMagicNode"
        };

        [MenuItem("Hecton8/Validation/Validate MapMagic World Stack", priority = 236)]
        public static void ValidateMapMagicWorldStack()
        {
            int errorCount = 0;
            int warningCount = 0;
            int uncoveredSocketCount = 0;
            int weakSpatialSocketCount = 0;

            MapMagicObject mapMagic = FindSceneObjectIncludingInactive<MapMagicObject>();
            MapMagicBridge bridge = FindSceneObjectIncludingInactive<MapMagicBridge>();
            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            HectonRockManager rockManager = FindSceneObjectIncludingInactive<HectonRockManager>();
            GPUInstancerPrefabManager gpuiPrefabManager = FindSceneObjectIncludingInactive<GPUInstancerPrefabManager>();
            GameTickManager tickManager = FindSceneObjectIncludingInactive<GameTickManager>();
            ProximityColliderSystem proximityColliderSystem = FindSceneObjectIncludingInactive<ProximityColliderSystem>();
            ObjectPoolManager objectPoolManager = FindSceneObjectIncludingInactive<ObjectPoolManager>();
            BiomeSamplerCache biomeCache = FindSceneObjectIncludingInactive<BiomeSamplerCache>();
            WorldStreamingDirector streamingDirector = FindSceneObjectIncludingInactive<WorldStreamingDirector>();
            WorldSliceDirector worldSliceDirector = FindSceneObjectIncludingInactive<WorldSliceDirector>();
            WorldInterestDirector worldInterestDirector = FindSceneObjectIncludingInactive<WorldInterestDirector>();
            WorldZoneDirector worldZoneDirector = FindSceneObjectIncludingInactive<WorldZoneDirector>();
            WorldContentDirector worldContentDirector = FindSceneObjectIncludingInactive<WorldContentDirector>();
            WorldPopulationDirector worldPopulationDirector = FindSceneObjectIncludingInactive<WorldPopulationDirector>();
            WorldProceduralFillDirector worldProceduralFillDirector = FindSceneObjectIncludingInactive<WorldProceduralFillDirector>();
            WorldProceduralFieldSampler worldProceduralFieldSampler = FindSceneObjectIncludingInactive<WorldProceduralFieldSampler>();
            WorldProceduralScatterDirector worldProceduralScatterDirector = FindSceneObjectIncludingInactive<WorldProceduralScatterDirector>();
            BiomeMatrixDirector biomeMatrixDirector = FindSceneObjectIncludingInactive<BiomeMatrixDirector>();
            WorldReadabilityDirector worldReadabilityDirector = FindSceneObjectIncludingInactive<WorldReadabilityDirector>();
            ScatterBudgetController scatterBudgetController = FindSceneObjectIncludingInactive<ScatterBudgetController>();

            if (mapMagic == null)
            {
                Debug.LogError("[MapMagicWorldValidation] Scene is missing MapMagicObject.");
                errorCount++;
            }
            else
            {
                ValidateMapMagicObject(mapMagic, ref errorCount, ref warningCount);
            }

            if (bridge == null)
            {
                Debug.LogError("[MapMagicWorldValidation] Scene is missing MapMagicBridge.");
                errorCount++;
            }
            else
            {
                ValidateBridge(bridge, mapMagic, ref errorCount, ref warningCount);
            }

            if (tickManager == null)
            {
                Debug.LogError("[MapMagicWorldValidation] Scene is missing GameTickManager.");
                errorCount++;
            }

            if (scavengePopulator == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing ScavengePopulator.");
                warningCount++;
            }

            if (rockManager == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing HectonRockManager.");
                warningCount++;
            }
            else
            {
                ValidateRockManager(rockManager, ref errorCount, ref warningCount);
            }

            if (gpuiPrefabManager == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing GPUInstancerPrefabManager.");
                warningCount++;
            }
            else
            {
                ValidateGPUInstancerManager(gpuiPrefabManager, ref errorCount, ref warningCount);
            }

            if (objectPoolManager == null)
            {
                Debug.LogError("[MapMagicWorldValidation] Scene is missing ObjectPoolManager.");
                errorCount++;
            }

            if (proximityColliderSystem == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing ProximityColliderSystem.");
                warningCount++;
            }
            else
            {
                ValidateProximityColliderSystem(
                    proximityColliderSystem,
                    objectPoolManager,
                    ref errorCount,
                    ref warningCount);
            }

            if (biomeCache == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing BiomeSamplerCache.");
                warningCount++;
            }
            else
            {
                ValidateBiomeCache(biomeCache, ref errorCount, ref warningCount);
            }

            if (scatterBudgetController == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing ScatterBudgetController.");
                warningCount++;
            }

            if (streamingDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldStreamingDirector.");
                warningCount++;
            }
            else
            {
                ValidateStreamingDirector(streamingDirector, ref errorCount, ref warningCount);
            }

            if (worldSliceDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldSliceDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldSliceDirector(worldSliceDirector, ref errorCount, ref warningCount);
            }

            if (worldInterestDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldInterestDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldInterestDirector(worldInterestDirector, ref errorCount, ref warningCount);
            }

            if (worldZoneDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldZoneDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldZoneDirector(worldZoneDirector, ref errorCount, ref warningCount);
            }

            if (worldContentDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldContentDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldContentDirector(worldContentDirector, ref errorCount, ref warningCount);
            }

            if (worldPopulationDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldPopulationDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldPopulationDirector(worldPopulationDirector, ref errorCount, ref warningCount, ref uncoveredSocketCount, ref weakSpatialSocketCount);
            }

            if (worldProceduralFillDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldProceduralFillDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldProceduralFillDirector(worldProceduralFillDirector, ref errorCount, ref warningCount);
            }

            if (worldProceduralFieldSampler == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldProceduralFieldSampler.");
                warningCount++;
            }
            else
            {
                ValidateWorldProceduralFieldSampler(worldProceduralFieldSampler, ref errorCount, ref warningCount);
            }

            if (worldProceduralScatterDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldProceduralScatterDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldProceduralScatterDirector(worldProceduralScatterDirector, ref errorCount, ref warningCount);
            }

            if (biomeMatrixDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing BiomeMatrixDirector.");
                warningCount++;
            }
            else
            {
                ValidateBiomeMatrixDirector(biomeMatrixDirector, ref errorCount, ref warningCount);
            }

            if (worldReadabilityDirector == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene is missing WorldReadabilityDirector.");
                warningCount++;
            }
            else
            {
                ValidateWorldReadabilityDirector(worldReadabilityDirector, ref warningCount);
            }

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[MapMagicWorldValidation] PASS no issues found.");
                return;
            }

            if (uncoveredSocketCount > 0 || weakSpatialSocketCount > 0)
            {
                Debug.LogWarning(
                    $"[MapMagicWorldValidation] Population coverage summary uncoveredSockets={uncoveredSocketCount} weakSpatialSockets={weakSpatialSocketCount}");
            }

            Debug.LogWarning($"[MapMagicWorldValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static void ValidateMapMagicObject(
            MapMagicObject mapMagic,
            ref int errorCount,
            ref int warningCount)
        {
            if (mapMagic.graph == null)
            {
                Debug.LogError("[MapMagicWorldValidation] MapMagicObject has no graph assigned.", mapMagic);
                errorCount++;
            }
            else
            {
                ValidateMapMagicGraphBudget(mapMagic, ref errorCount, ref warningCount);
            }

            if (!mapMagic.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[MapMagicWorldValidation] MapMagicObject root is inactive. Runtime generation is currently dormant.", mapMagic);
                warningCount++;
            }

            if (!mapMagic.hideFarTerrains)
            {
                Debug.LogWarning("[MapMagicWorldValidation] hideFarTerrains is disabled; this increases terrain cost.", mapMagic);
                warningCount++;
            }

            if (mapMagic.mainRange > 2)
            {
                Debug.LogWarning($"[MapMagicWorldValidation] mainRange is high ({mapMagic.mainRange}); expected 1-2 for runtime budgets.", mapMagic);
                warningCount++;
            }

            if (!mapMagic.draftsInPlaymode)
            {
                Debug.LogWarning("[MapMagicWorldValidation] draftsInPlaymode is disabled; runtime terrain loses the draft-to-main detail continuum and far terrain will stay visibly coarse or disappear.", mapMagic);
                warningCount++;
            }

            if (mapMagic.draftsInPlaymode && (int)mapMagic.draftResolution < 65)
            {
                Debug.LogWarning($"[MapMagicWorldValidation] draftResolution is low ({(int)mapMagic.draftResolution}); expected >= 65 to avoid visibly blurry mid-field terrain.", mapMagic);
                warningCount++;
            }

            if (mapMagic.draftsInPlaymode && mapMagic.tileResolution != mapMagic.draftResolution)
            {
                Debug.LogError(
                    $"[MapMagicWorldValidation] draftsInPlaymode is enabled but tileResolution ({(int)mapMagic.tileResolution}) does not match draftResolution ({(int)mapMagic.draftResolution}). This breaks Unity terrain neighboring and causes seam warnings.",
                    mapMagic);
                errorCount++;
            }

            if (mapMagic.tiles.generateRange < mapMagic.mainRange)
            {
                Debug.LogWarning($"[MapMagicWorldValidation] DraftRange ({mapMagic.tiles.generateRange}) is lower than mainRange ({mapMagic.mainRange}); terrain streaming rings are inconsistent.", mapMagic);
                warningCount++;
            }

            if (mapMagic.globals != null && mapMagic.globals.objectsNumPerFrame > 200)
            {
                Debug.LogWarning($"[MapMagicWorldValidation] objectsNumPerFrame is high ({mapMagic.globals.objectsNumPerFrame}); expected <= 200.", mapMagic);
                warningCount++;
            }

            if (!mapMagic.terrainSettings.drawInstanced)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Terrain drawInstanced is disabled.", mapMagic);
                warningCount++;
            }
        }

        private static void ValidateMapMagicGraphBudget(
            MapMagicObject mapMagic,
            ref int errorCount,
            ref int warningCount)
        {
            string graphPath = AssetDatabase.GetAssetPath(mapMagic.graph);
            if (string.IsNullOrEmpty(graphPath))
            {
                Debug.LogError("[MapMagicWorldValidation] MapMagic graph has no asset path; graph budget cannot be audited.", mapMagic);
                errorCount++;
                return;
            }

            string[] dependencies = AssetDatabase.GetDependencies(graphPath, true);
            if (dependencies == null || dependencies.Length == 0)
            {
                ScanMapMagicGraphAssetForForbiddenNodes(graphPath, mapMagic, ref errorCount, ref warningCount);
                return;
            }

            bool scannedAny = false;
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependencyPath = dependencies[i];
                if (string.IsNullOrEmpty(dependencyPath) ||
                    !dependencyPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                scannedAny = true;
                ScanMapMagicGraphAssetForForbiddenNodes(dependencyPath, mapMagic, ref errorCount, ref warningCount);
            }

            if (!scannedAny)
            {
                Debug.LogWarning(
                    $"[MapMagicWorldValidation] Graph '{graphPath}' reported no asset dependencies; only direct scene references were validated.",
                    mapMagic);
                warningCount++;
            }
        }

        private static void ScanMapMagicGraphAssetForForbiddenNodes(
            string assetPath,
            UnityEngine.Object context,
            ref int errorCount,
            ref int warningCount)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return;

            string assetText = File.ReadAllText(fullPath);
            for (int i = 0; i < ForbiddenMapMagicGraphNodeTypeTokens.Length; i++)
            {
                string token = ForbiddenMapMagicGraphNodeTypeTokens[i];
                int searchIndex = 0;
                while (searchIndex < assetText.Length)
                {
                    int tokenIndex = assetText.IndexOf(token, searchIndex, StringComparison.OrdinalIgnoreCase);
                    if (tokenIndex < 0)
                        break;

                    int blockStart = assetText.LastIndexOf(
                        MapMagicSerializedBlockPrefix,
                        tokenIndex,
                        StringComparison.Ordinal);

                    if (blockStart < 0)
                    {
                        Debug.LogError(
                            $"[MapMagicWorldValidation] Forbidden MapMagic graph token '{token}' found in '{assetPath}' but serialized block bounds could not be resolved.",
                            context);
                        errorCount++;
                        searchIndex = tokenIndex + token.Length;
                        continue;
                    }

                    int blockEnd = assetText.IndexOf(
                        MapMagicSerializedBlockPrefix,
                        tokenIndex + token.Length,
                        StringComparison.Ordinal);
                    if (blockEnd < 0)
                        blockEnd = assetText.Length;

                    if (IsSerializedMapMagicGeneratorEnabled(assetText, blockStart, blockEnd))
                    {
                        Debug.LogError(
                            $"[MapMagicWorldValidation] Enabled forbidden MapMagic generator '{token}' found in '{assetPath}'. Runtime terrain graph must use simple noise/Voronoi/curve math only.",
                            context);
                        errorCount++;
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[MapMagicWorldValidation] Disabled forbidden MapMagic generator baggage '{token}' remains serialized in '{assetPath}'. Runtime bypass is active; asset cleanup is still required.",
                            context);
                        warningCount++;
                    }

                    searchIndex = blockEnd;
                }
            }
        }

        private static bool IsSerializedMapMagicGeneratorEnabled(string assetText, int blockStart, int blockEnd)
        {
            int fieldsIndex = assetText.IndexOf("      fields:", blockStart, blockEnd - blockStart, StringComparison.Ordinal);
            int valuesIndex = assetText.IndexOf("      values:", blockStart, blockEnd - blockStart, StringComparison.Ordinal);
            if (fieldsIndex < 0 || valuesIndex < 0 || valuesIndex <= fieldsIndex)
                return true;

            int enabledFieldIndex = assetText.IndexOf("      - enabled", fieldsIndex, valuesIndex - fieldsIndex, StringComparison.Ordinal);
            if (enabledFieldIndex < 0)
                return true;

            int enabledFieldOrdinal = 0;
            int fieldCursor = assetText.IndexOf("      - ", fieldsIndex, valuesIndex - fieldsIndex, StringComparison.Ordinal);
            while (fieldCursor >= 0 && fieldCursor < enabledFieldIndex)
            {
                enabledFieldOrdinal++;
                fieldCursor = assetText.IndexOf("      - ", fieldCursor + 1, valuesIndex - fieldCursor - 1, StringComparison.Ordinal);
            }

            int valueCursor = valuesIndex;
            for (int i = 0; i <= enabledFieldOrdinal; i++)
            {
                valueCursor = assetText.IndexOf("      - t:", valueCursor + 1, blockEnd - valueCursor - 1, StringComparison.Ordinal);
                if (valueCursor < 0)
                    return true;
            }

            int valueLineIndex = assetText.IndexOf("        v:", valueCursor, blockEnd - valueCursor, StringComparison.Ordinal);
            if (valueLineIndex < 0)
                return true;

            int valueLineEnd = assetText.IndexOf('\n', valueLineIndex);
            if (valueLineEnd < 0 || valueLineEnd > blockEnd)
                valueLineEnd = blockEnd;

            string valueText = assetText.Substring(valueLineIndex + 10, valueLineEnd - valueLineIndex - 10).Trim();
            return valueText != "0";
        }

        private static void ValidateBridge(
            MapMagicBridge bridge,
            MapMagicObject mapMagic,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(bridge);
            SerializedProperty mapMagicProp = so.FindProperty("mapMagicObject");
            SerializedProperty playerProp = so.FindProperty("playerTransform");

            if (mapMagicProp == null)
            {
                Debug.LogError("[MapMagicWorldValidation] MapMagicBridge serialized mapMagicObject field was not found.", bridge);
                errorCount++;
            }
            else if (mapMagicProp.objectReferenceValue == null && mapMagic == null)
            {
                Debug.LogError("[MapMagicWorldValidation] MapMagicBridge has no direct or scene-resolvable MapMagicObject.", bridge);
                errorCount++;
            }

            if (playerProp == null || playerProp.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] MapMagicBridge has no explicit playerTransform; runtime auto-resolve is being used.", bridge);
                warningCount++;
            }
        }

        private static void ValidateBiomeCache(
            BiomeSamplerCache biomeCache,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(biomeCache);
            SerializedProperty cellSize = so.FindProperty("cellSize");
            SerializedProperty radiusCells = so.FindProperty("radiusCells");
            SerializedProperty rebuildDistance = so.FindProperty("rebuildDistance");

            if (cellSize == null || cellSize.floatValue < 8f)
            {
                Debug.LogError("[MapMagicWorldValidation] BiomeSamplerCache cellSize is missing or too small.", biomeCache);
                errorCount++;
            }

            if (radiusCells == null || radiusCells.intValue < 1)
            {
                Debug.LogError("[MapMagicWorldValidation] BiomeSamplerCache radiusCells must be >= 1.", biomeCache);
                errorCount++;
            }

            if (rebuildDistance == null || rebuildDistance.floatValue <= 0f)
            {
                Debug.LogError("[MapMagicWorldValidation] BiomeSamplerCache rebuildDistance must be > 0.", biomeCache);
                errorCount++;
            }
        }

        private static void ValidateRockManager(
            HectonRockManager rockManager,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(rockManager);
            SerializedProperty gpuiManager = so.FindProperty("gpuiManager");
            SerializedProperty proximityColliderSystem = so.FindProperty("proximityColliderSystem");
            SerializedProperty rockLayers = so.FindProperty("rockLayers");

            if (gpuiManager == null || gpuiManager.objectReferenceValue == null)
            {
                Debug.LogError("[MapMagicWorldValidation] HectonRockManager has no GPUInstancerPrefabManager assigned.", rockManager);
                errorCount++;
            }

            if (proximityColliderSystem == null || proximityColliderSystem.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] HectonRockManager has no ProximityColliderSystem assigned.", rockManager);
                warningCount++;
            }

            if (rockLayers == null || rockLayers.arraySize <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] HectonRockManager has no rock layer config.", rockManager);
                errorCount++;
                return;
            }

            for (int i = 0; i < rockLayers.arraySize; i++)
            {
                SerializedProperty entry = rockLayers.GetArrayElementAtIndex(i);
                SerializedProperty prefabReference = entry.FindPropertyRelative("prefabReference");
                if (prefabReference == null || prefabReference.objectReferenceValue == null)
                {
                    Debug.LogError($"[MapMagicWorldValidation] HectonRockManager layer {i} has no prefabReference.", rockManager);
                    errorCount++;
                }
            }
        }

        private static void ValidateGPUInstancerManager(
            GPUInstancerPrefabManager gpuiPrefabManager,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(gpuiPrefabManager);
            SerializedProperty prefabList = so.FindProperty("prefabList");

            if (prefabList == null || prefabList.arraySize <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] GPUInstancerPrefabManager prefabList is empty.", gpuiPrefabManager);
                errorCount++;
            }
        }

        private static void ValidateStreamingDirector(
            WorldStreamingDirector streamingDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(streamingDirector);
            SerializedProperty traverseSpeedStart = so.FindProperty("traverseSpeedStart");
            SerializedProperty speedSmoothing = so.FindProperty("speedSmoothing");
            SerializedProperty scatterBudgetController = so.FindProperty("scatterBudgetController");
            SerializedProperty worldSliceDirector = so.FindProperty("worldSliceDirector");

            if (traverseSpeedStart == null || traverseSpeedStart.floatValue <= 0f)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldStreamingDirector traverseSpeedStart must be > 0.", streamingDirector);
                errorCount++;
            }

            if (speedSmoothing == null || speedSmoothing.floatValue <= 0f || speedSmoothing.floatValue > 1f)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldStreamingDirector speedSmoothing must be within (0,1].", streamingDirector);
                errorCount++;
            }

            if (scatterBudgetController == null || scatterBudgetController.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldStreamingDirector is using runtime auto-resolve for ScatterBudgetController.", streamingDirector);
                warningCount++;
            }

            if (worldSliceDirector == null || worldSliceDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldStreamingDirector is using runtime auto-resolve for WorldSliceDirector.", streamingDirector);
                warningCount++;
            }
        }

        private static void ValidateWorldInterestDirector(
            WorldInterestDirector worldInterestDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldInterestDirector);
            SerializedProperty scatterBudgetController = so.FindProperty("scatterBudgetController");
            SerializedProperty worldSliceDirector = so.FindProperty("worldSliceDirector");
            if (scatterBudgetController == null || scatterBudgetController.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldInterestDirector is using runtime auto-resolve for ScatterBudgetController.", worldInterestDirector);
                warningCount++;
            }

            if (worldSliceDirector == null || worldSliceDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldInterestDirector is using runtime auto-resolve for WorldSliceDirector.", worldInterestDirector);
                warningCount++;
            }

            WorldInterestAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldInterestAnchor>();
            int liveCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                if (anchors[i] != null && anchors[i].gameObject != null && anchors[i].gameObject.scene.IsValid())
                    liveCount++;
            }

            if (liveCount <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] Scene has no WorldInterestAnchor roots.", worldInterestDirector);
                warningCount++;
            }
        }

        private static void ValidateProximityColliderSystem(
            ProximityColliderSystem proximityColliderSystem,
            ObjectPoolManager objectPoolManager,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(proximityColliderSystem);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            SerializedProperty colliderPrefab = so.FindProperty("colliderPrefab");
            SerializedProperty activateRadius = so.FindProperty("activateRadius");
            SerializedProperty deactivateRadius = so.FindProperty("deactivateRadius");
            SerializedProperty maxOperationsPerTick = so.FindProperty("maxOperationsPerTick");

            if (colliderPrefab == null || colliderPrefab.objectReferenceValue == null)
            {
                Debug.LogError("[MapMagicWorldValidation] ProximityColliderSystem has no colliderPrefab assigned.", proximityColliderSystem);
                errorCount++;
                return;
            }

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] ProximityColliderSystem is using runtime auto-resolve for playerTransform.", proximityColliderSystem);
                warningCount++;
            }

            if (activateRadius == null || activateRadius.floatValue < 8f)
            {
                Debug.LogError("[MapMagicWorldValidation] ProximityColliderSystem activateRadius is too small.", proximityColliderSystem);
                errorCount++;
            }

            if (deactivateRadius == null || deactivateRadius.floatValue <= activateRadius.floatValue)
            {
                Debug.LogError("[MapMagicWorldValidation] ProximityColliderSystem deactivateRadius must be greater than activateRadius.", proximityColliderSystem);
                errorCount++;
            }

            if (maxOperationsPerTick == null || maxOperationsPerTick.intValue < 4)
            {
                Debug.LogError("[MapMagicWorldValidation] ProximityColliderSystem maxOperationsPerTick must be >= 4.", proximityColliderSystem);
                errorCount++;
            }

            GameObject colliderPrefabObject = colliderPrefab.objectReferenceValue as GameObject;
            if (colliderPrefabObject == null || colliderPrefabObject.GetComponent<BoxCollider>() == null)
            {
                Debug.LogError("[MapMagicWorldValidation] ProximityColliderSystem colliderPrefab is missing a BoxCollider.", proximityColliderSystem);
                errorCount++;
            }

            if (objectPoolManager != null && colliderPrefabObject != null && !HasWarmupPreset(objectPoolManager, colliderPrefabObject))
            {
                Debug.LogWarning("[MapMagicWorldValidation] ObjectPoolManager has no warmup preset for the ProximityColliderSystem collider prefab.", objectPoolManager);
                warningCount++;
            }
        }

        private static bool HasWarmupPreset(ObjectPoolManager objectPoolManager, GameObject prefab)
        {
            SerializedObject so = new SerializedObject(objectPoolManager);
            SerializedProperty presets = so.FindProperty("warmupPresets");
            if (presets == null)
                return false;

            for (int i = 0; i < presets.arraySize; i++)
            {
                SerializedProperty entry = presets.GetArrayElementAtIndex(i);
                SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
                SerializedProperty countProp = entry.FindPropertyRelative("count");
                if (prefabProp == null || countProp == null)
                    continue;

                if (prefabProp.objectReferenceValue == prefab && countProp.intValue > 0)
                    return true;
            }

            return false;
        }

        private static void ValidateWorldSliceDirector(
            WorldSliceDirector worldSliceDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldSliceDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldSliceDirector is using runtime auto-resolve for playerTransform.", worldSliceDirector);
                warningCount++;
            }

            WorldSliceAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldSliceAnchor>();
            int sceneAnchorCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldSliceAnchor anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || !anchor.gameObject.scene.IsValid())
                    continue;

                sceneAnchorCount++;
            }

            if (sceneAnchorCount <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] No WorldSliceAnchor objects found in the scene.", worldSliceDirector);
                warningCount++;
            }

            WorldFidelityRoot[] fidelityRoots = Resources.FindObjectsOfTypeAll<WorldFidelityRoot>();
            int sceneFidelityCount = 0;
            for (int i = 0; i < fidelityRoots.Length; i++)
            {
                WorldFidelityRoot fidelityRoot = fidelityRoots[i];
                if (fidelityRoot == null || fidelityRoot.gameObject == null || !fidelityRoot.gameObject.scene.IsValid())
                    continue;

                sceneFidelityCount++;
            }

            if (sceneFidelityCount <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] No WorldFidelityRoot components found in the scene.", worldSliceDirector);
                warningCount++;
            }
        }

        private static void ValidateWorldZoneDirector(
            WorldZoneDirector worldZoneDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldZoneDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldZoneDirector is using runtime auto-resolve for playerTransform.", worldZoneDirector);
                warningCount++;
            }

            WorldZoneAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldZoneAnchor>();
            int sceneZoneCount = 0;
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldZoneAnchor anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || !anchor.gameObject.scene.IsValid())
                    continue;

                sceneZoneCount++;

                if (anchor.Profile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a zone profile.", anchor);
                    warningCount++;
                    continue;
                }

                if (anchor.DominantMatrixBiome == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a dominant matrix biome.", anchor);
                    warningCount++;
                }

                if (anchor.DominantBiomeFamily == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a dominant biome family.", anchor);
                    warningCount++;
                }
                else if (anchor.DominantMatrixBiome != null && anchor.DominantMatrixBiome.familyProfile != anchor.DominantBiomeFamily)
                {
                    Debug.LogWarning(
                        $"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has dominant biome family that does not match its matrix biome.",
                        anchor);
                    warningCount++;
                }

                if (anchor.Profile.zonePlanProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a zonePlanProfile.", anchor);
                    warningCount++;
                }

                if (anchor.Profile.sandboxAttractionProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a sandboxAttractionProfile.", anchor);
                    warningCount++;
                }

                if (anchor.Profile.motivationProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' is missing a motivationProfile.", anchor);
                    warningCount++;
                }

                if (string.IsNullOrWhiteSpace(anchor.Profile.nearInteractiveFamily))
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has an empty nearInteractiveFamily.", anchor);
                    warningCount++;
                }
                else if (anchor.Profile.nearInteractiveProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has nearInteractiveFamily but no nearInteractiveProfile asset.", anchor);
                    warningCount++;
                }

                if (string.IsNullOrWhiteSpace(anchor.Profile.midVisualFamily))
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has an empty midVisualFamily.", anchor);
                    warningCount++;
                }
                else if (anchor.Profile.midVisualProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has midVisualFamily but no midVisualProfile asset.", anchor);
                    warningCount++;
                }

                if (string.IsNullOrWhiteSpace(anchor.Profile.farSilhouetteFamily))
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has an empty farSilhouetteFamily.", anchor);
                    warningCount++;
                }
                else if (anchor.Profile.farSilhouetteProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has farSilhouetteFamily but no farSilhouetteProfile asset.", anchor);
                    warningCount++;
                }

                if (anchor.Profile.zonePlanProfile != null)
                {
                    if (anchor.Profile.zonePlanProfile.nearPlan.primaryFamily == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Zone plan '{anchor.Profile.zonePlanProfile.name}' is missing near primary family.", anchor.Profile.zonePlanProfile);
                        warningCount++;
                    }

                    if (anchor.Profile.zonePlanProfile.midPlan.primaryFamily == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Zone plan '{anchor.Profile.zonePlanProfile.name}' is missing mid primary family.", anchor.Profile.zonePlanProfile);
                        warningCount++;
                    }

                    if (anchor.Profile.zonePlanProfile.farPlan.primaryFamily == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Zone plan '{anchor.Profile.zonePlanProfile.name}' is missing far primary family.", anchor.Profile.zonePlanProfile);
                        warningCount++;
                    }

                    if (anchor.Profile.zonePlanProfile.routeAnchorPlan == null || anchor.Profile.zonePlanProfile.routeAnchorPlan.family == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Zone plan '{anchor.Profile.zonePlanProfile.name}' is missing routeAnchorPlan family.", anchor.Profile.zonePlanProfile);
                        warningCount++;
                    }

                    if (anchor.Profile.zonePlanProfile.safePocketPlan == null || anchor.Profile.zonePlanProfile.safePocketPlan.family == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Zone plan '{anchor.Profile.zonePlanProfile.name}' is missing safePocketPlan family.", anchor.Profile.zonePlanProfile);
                        warningCount++;
                    }

                    switch (anchor.Kind)
                    {
                        case WorldZoneAnchor.ZoneKind.Resources:
                            if (anchor.Profile.zonePlanProfile.resourcePocketPlan == null || anchor.Profile.zonePlanProfile.resourcePocketPlan.family == null ||
                                anchor.Profile.zonePlanProfile.nodeClusterPlan == null || anchor.Profile.zonePlanProfile.nodeClusterPlan.family == null)
                            {
                                Debug.LogWarning($"[MapMagicWorldValidation] Resource zone plan '{anchor.Profile.zonePlanProfile.name}' is missing resourcePocketPlan or nodeClusterPlan family.", anchor.Profile.zonePlanProfile);
                                warningCount++;
                            }
                            break;

                        case WorldZoneAnchor.ZoneKind.Construction:
                            if (anchor.Profile.zonePlanProfile.buildSocketPlan == null || anchor.Profile.zonePlanProfile.buildSocketPlan.family == null)
                            {
                                Debug.LogWarning($"[MapMagicWorldValidation] Construction zone plan '{anchor.Profile.zonePlanProfile.name}' is missing buildSocketPlan family.", anchor.Profile.zonePlanProfile);
                                warningCount++;
                            }
                            break;

                        case WorldZoneAnchor.ZoneKind.Power:
                            if (anchor.Profile.zonePlanProfile.powerSpinePlan == null || anchor.Profile.zonePlanProfile.powerSpinePlan.family == null)
                            {
                                Debug.LogWarning($"[MapMagicWorldValidation] Power zone plan '{anchor.Profile.zonePlanProfile.name}' is missing powerSpinePlan family.", anchor.Profile.zonePlanProfile);
                                warningCount++;
                            }
                            break;

                        case WorldZoneAnchor.ZoneKind.Service:
                            if (anchor.Profile.zonePlanProfile.serviceChokePlan == null || anchor.Profile.zonePlanProfile.serviceChokePlan.family == null)
                            {
                                Debug.LogWarning($"[MapMagicWorldValidation] Service zone plan '{anchor.Profile.zonePlanProfile.name}' is missing serviceChokePlan family.", anchor.Profile.zonePlanProfile);
                                warningCount++;
                            }
                            break;

                        case WorldZoneAnchor.ZoneKind.Progression:
                        case WorldZoneAnchor.ZoneKind.Combat:
                            if (anchor.Profile.zonePlanProfile.hazardGatePlan == null || anchor.Profile.zonePlanProfile.hazardGatePlan.family == null ||
                                anchor.Profile.zonePlanProfile.rareObjectivePlan == null || anchor.Profile.zonePlanProfile.rareObjectivePlan.family == null)
                            {
                                Debug.LogWarning($"[MapMagicWorldValidation] High-pressure zone plan '{anchor.Profile.zonePlanProfile.name}' is missing hazardGatePlan or rareObjectivePlan family.", anchor.Profile.zonePlanProfile);
                                warningCount++;
                            }
                            break;
                    }
                }

                SerializedObject anchorSo = new SerializedObject(anchor);
                SerializedProperty edgeBlendDistance = anchorSo.FindProperty("edgeBlendDistance");
                SerializedProperty edgeNoiseScale = anchorSo.FindProperty("edgeNoiseScale");
                SerializedProperty edgeNoiseStrength = anchorSo.FindProperty("edgeNoiseStrength");

                if (edgeBlendDistance == null || edgeBlendDistance.floatValue < 4f)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has too little edgeBlendDistance for soft biome edges.", anchor);
                    warningCount++;
                }

                if (edgeNoiseScale == null || edgeNoiseScale.floatValue <= 0f)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has invalid edgeNoiseScale.", anchor);
                    warningCount++;
                }

                if (edgeNoiseStrength == null || edgeNoiseStrength.floatValue <= 0f)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldZoneAnchor '{anchor.name}' has zero edgeNoiseStrength, so zone borders will feel too clean.", anchor);
                    warningCount++;
                }
            }

            if (sceneZoneCount <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] No WorldZoneAnchor objects found in the scene.", worldZoneDirector);
                warningCount++;
            }
        }

        private static void ValidateWorldContentDirector(
            WorldContentDirector worldContentDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldContentDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            SerializedProperty worldZoneDirector = so.FindProperty("worldZoneDirector");

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldContentDirector is using runtime auto-resolve for playerTransform.", worldContentDirector);
                warningCount++;
            }

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldContentDirector is using runtime auto-resolve for WorldZoneDirector.", worldContentDirector);
                warningCount++;
            }

            WorldContentSocket[] sockets = Resources.FindObjectsOfTypeAll<WorldContentSocket>();
            int sceneSocketCount = 0;
            for (int i = 0; i < sockets.Length; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null || socket.gameObject == null || !socket.gameObject.scene.IsValid())
                    continue;

                sceneSocketCount++;

                SerializedObject socketSo = new SerializedObject(socket);
                SerializedProperty contentProfile = socketSo.FindProperty("contentProfile");
                if (contentProfile == null || contentProfile.objectReferenceValue == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldContentSocket '{socket.name}' is missing a content profile.", socket);
                    warningCount++;
                    continue;
                }

                WorldContentProfile profile = contentProfile.objectReferenceValue as WorldContentProfile;
                if (profile == null)
                    continue;

                if (profile.contentKind != WorldContentSocket.ContentKind.Generic && profile.contentKind != socket.Kind)
                {
                    Debug.LogWarning(
                        $"[MapMagicWorldValidation] WorldContentSocket '{socket.name}' kind '{socket.Kind}' conflicts with profile kind '{profile.contentKind}'.",
                        socket);
                    warningCount++;
                }

                WorldZoneAnchor zone = socket.GetZoneAnchor();
                if (zone != null &&
                    profile.preferredZoneKind != WorldZoneAnchor.ZoneKind.Generic &&
                    profile.preferredZoneKind != zone.Kind)
                {
                    Debug.LogWarning(
                        $"[MapMagicWorldValidation] WorldContentSocket '{socket.name}' sits in zone '{zone.ZoneLabel}' but profile prefers zone kind '{profile.preferredZoneKind}'.",
                        socket);
                    warningCount++;
                }
            }

            if (sceneSocketCount <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] No WorldContentSocket objects found in the scene.", worldContentDirector);
                warningCount++;
            }
        }

        private static void ValidateWorldPopulationDirector(
            WorldPopulationDirector worldPopulationDirector,
            ref int errorCount,
            ref int warningCount,
            ref int uncoveredSocketCount,
            ref int weakSpatialSocketCount)
        {
            SerializedObject so = new SerializedObject(worldPopulationDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            SerializedProperty worldZoneDirector = so.FindProperty("worldZoneDirector");
            SerializedProperty worldContentDirector = so.FindProperty("worldContentDirector");
            SerializedProperty rules = so.FindProperty("rules");

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldPopulationDirector is using runtime auto-resolve for playerTransform.", worldPopulationDirector);
                warningCount++;
            }

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldPopulationDirector is using runtime auto-resolve for WorldZoneDirector.", worldPopulationDirector);
                warningCount++;
            }

            if (worldContentDirector == null || worldContentDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldPopulationDirector is using runtime auto-resolve for WorldContentDirector.", worldPopulationDirector);
                warningCount++;
            }

            if (rules == null || rules.arraySize <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldPopulationDirector has no population rules assigned.", worldPopulationDirector);
                warningCount++;
                return;
            }

            for (int i = 0; i < rules.arraySize; i++)
            {
                SerializedProperty entry = rules.GetArrayElementAtIndex(i);
                WorldPopulationRule rule = entry != null ? entry.objectReferenceValue as WorldPopulationRule : null;
                if (rule == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(rule.prefabFamily) && rule.familyProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldPopulationRule '{rule.ruleLabel}' has prefabFamily but no familyProfile asset.", rule);
                    warningCount++;
                }

                if (rule.preferredBiomeFamilies != null)
                {
                    for (int familyIndex = 0; familyIndex < rule.preferredBiomeFamilies.Length; familyIndex++)
                    {
                        if (rule.preferredBiomeFamilies[familyIndex] != null)
                            continue;

                        Debug.LogWarning(
                            $"[MapMagicWorldValidation] WorldPopulationRule '{rule.ruleLabel}' has an empty preferred biome family slot.",
                            rule);
                        warningCount++;
                    }
                }

                if (string.IsNullOrWhiteSpace(rule.gameplayPurpose))
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] WorldPopulationRule '{rule.ruleLabel}' has an empty gameplayPurpose.", rule);
                    warningCount++;
                }
            }

            WorldContentSocket[] sockets = Resources.FindObjectsOfTypeAll<WorldContentSocket>();
            for (int i = 0; i < sockets.Length; i++)
            {
                WorldContentSocket socket = sockets[i];
                if (socket == null || socket.gameObject == null || !socket.gameObject.scene.IsValid())
                    continue;

                WorldZoneAnchor zone = socket.GetZoneAnchor();
                if (zone == null)
                    continue;

                if (!HasMatchingPopulationRule(rules, zone, socket))
                {
                    Debug.LogWarning(
                        $"[MapMagicWorldValidation] WorldContentSocket '{socket.name}' in zone '{zone.ZoneLabel}' has no matching population rule.",
                        socket);
                    warningCount++;
                    uncoveredSocketCount++;
                    continue;
                }

                WorldPopulationRule strongestRule = FindStrongestMatchingPopulationRule(rules, zone, socket);
                if (strongestRule == null)
                    continue;

                string spatialRole = strongestRule.BuildSpatialRole(zone, socket);
                string spatialReason = strongestRule.BuildSpatialRoleReason(zone, socket);
                if (string.IsNullOrWhiteSpace(spatialRole) ||
                    string.Equals(spatialRole, "Generic Point", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(spatialReason) ||
                    string.Equals(spatialReason, "Socket follows the biome's default spatial rhythm.", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(
                        $"[MapMagicWorldValidation] WorldContentSocket '{socket.name}' has weak spatial coverage. role='{spatialRole}' reason='{spatialReason}'.",
                        socket);
                    warningCount++;
                    weakSpatialSocketCount++;
                }
            }
        }

        private static void ValidateBiomeMatrixDirector(
            BiomeMatrixDirector biomeMatrixDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(biomeMatrixDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            SerializedProperty matrixCatalog = so.FindProperty("matrixCatalog");

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] BiomeMatrixDirector is using runtime auto-resolve for playerTransform.", biomeMatrixDirector);
                warningCount++;
            }

            if (matrixCatalog == null || matrixCatalog.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] BiomeMatrixDirector has no matrix catalog assigned.", biomeMatrixDirector);
                warningCount++;
                return;
            }

            HectonBiomeMatrixCatalog catalog = matrixCatalog.objectReferenceValue as HectonBiomeMatrixCatalog;
            if (catalog == null || catalog.Profiles == null)
                return;

            for (int i = 0; i < catalog.Profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = catalog.Profiles[i];
                if (profile == null)
                    continue;

                if (string.IsNullOrWhiteSpace(profile.familyId))
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] Matrix biome '{profile.biomeName}' is missing familyId.", profile);
                    warningCount++;
                }

                if (profile.familyProfile == null)
                {
                    Debug.LogWarning($"[MapMagicWorldValidation] Matrix biome '{profile.biomeName}' is missing familyProfile.", profile);
                    warningCount++;
                }
            }
        }

        private static void ValidateWorldReadabilityDirector(
            WorldReadabilityDirector worldReadabilityDirector,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldReadabilityDirector);
            SerializedProperty worldZoneDirector = so.FindProperty("worldZoneDirector");
            SerializedProperty biomeMatrixDirector = so.FindProperty("biomeMatrixDirector");

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldReadabilityDirector is using runtime auto-resolve for WorldZoneDirector.", worldReadabilityDirector);
                warningCount++;
            }

            if (biomeMatrixDirector == null || biomeMatrixDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldReadabilityDirector is using runtime auto-resolve for BiomeMatrixDirector.", worldReadabilityDirector);
                warningCount++;
            }
        }

        private static void ValidateWorldProceduralFillDirector(
            WorldProceduralFillDirector worldProceduralFillDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldProceduralFillDirector);
            SerializedProperty playerTransform = so.FindProperty("playerTransform");
            SerializedProperty worldZoneDirector = so.FindProperty("worldZoneDirector");
            SerializedProperty worldContentDirector = so.FindProperty("worldContentDirector");
            SerializedProperty biomeMatrixDirector = so.FindProperty("biomeMatrixDirector");
            SerializedProperty rules = so.FindProperty("rules");
            SerializedProperty families = so.FindProperty("families");

            if (playerTransform == null || playerTransform.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector is using runtime auto-resolve for playerTransform.", worldProceduralFillDirector);
                warningCount++;
            }

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector is using runtime auto-resolve for WorldZoneDirector.", worldProceduralFillDirector);
                warningCount++;
            }

            if (worldContentDirector == null || worldContentDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector is using runtime auto-resolve for WorldContentDirector.", worldProceduralFillDirector);
                warningCount++;
            }

            if (biomeMatrixDirector == null || biomeMatrixDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector is using runtime auto-resolve for BiomeMatrixDirector.", worldProceduralFillDirector);
                warningCount++;
            }

            if (rules == null || rules.arraySize <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector has no procedural placement rules assigned.", worldProceduralFillDirector);
                warningCount++;
            }

            if (families == null || families.arraySize <= 0)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFillDirector has no procedural family profiles assigned.", worldProceduralFillDirector);
                warningCount++;
            }

            if (rules != null)
            {
                HashSet<WorldPrefabFamilyProfile> coveredFamilies = new HashSet<WorldPrefabFamilyProfile>(rules.arraySize);
                for (int i = 0; i < rules.arraySize; i++)
                {
                    WorldProceduralPlacementRule rule = rules.GetArrayElementAtIndex(i).objectReferenceValue as WorldProceduralPlacementRule;
                    if (rule == null)
                        continue;

                    if (rule.familyProfile == null)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Procedural rule '{rule.ruleLabel}' has no familyProfile.", rule);
                        warningCount++;
                    }
                    else
                    {
                        coveredFamilies.Add(rule.familyProfile);
                    }

                    if (rule.maxInstances < rule.minInstances)
                    {
                        Debug.LogError($"[MapMagicWorldValidation] Procedural rule '{rule.ruleLabel}' has maxInstances < minInstances.", rule);
                        errorCount++;
                    }

                    if (!string.IsNullOrWhiteSpace(rule.requiredHeatmapChannel) && rule.minHeatmapValue <= 0f)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Procedural rule '{rule.ruleLabel}' uses heatmap '{rule.requiredHeatmapChannel}' but minHeatmapValue is too low to be meaningful.", rule);
                        warningCount++;
                    }
                }

                if (families != null)
                {
                    for (int i = 0; i < families.arraySize; i++)
                    {
                        WorldPrefabFamilyProfile family = families.GetArrayElementAtIndex(i).objectReferenceValue as WorldPrefabFamilyProfile;
                        if (family == null || !family.allowRuntimeScatter)
                            continue;

                        if (!coveredFamilies.Contains(family))
                        {
                            Debug.LogError($"[MapMagicWorldValidation] Procedural family '{family.familyLabel}' allows runtime scatter but has no placement rule coverage.", family);
                            errorCount++;
                        }
                    }
                }
            }

            if (families != null)
            {
                for (int i = 0; i < families.arraySize; i++)
                {
                    WorldPrefabFamilyProfile family = families.GetArrayElementAtIndex(i).objectReferenceValue as WorldPrefabFamilyProfile;
                    if (family == null)
                        continue;

                    if (family.variants == null || family.variants.Length <= 0)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Procedural family '{family.familyLabel}' has no variants.", family);
                        warningCount++;
                        continue;
                    }

                    bool hasPrefab = false;
                    for (int variantIndex = 0; variantIndex < family.variants.Length; variantIndex++)
                    {
                        WorldPrefabFamilyProfile.VariantEntry variant = family.variants[variantIndex];
                        if (variant == null)
                            continue;

                        if (variant.prefab != null)
                            hasPrefab = true;
                    }

                    if (!hasPrefab)
                    {
                        Debug.LogWarning($"[MapMagicWorldValidation] Procedural family '{family.familyLabel}' has variants but no assigned prefabs.", family);
                        warningCount++;
                    }
                }
            }
        }

        private static void ValidateWorldProceduralFieldSampler(
            WorldProceduralFieldSampler worldProceduralFieldSampler,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldProceduralFieldSampler);
            SerializedProperty mapMagicBridge = so.FindProperty("mapMagicBridge");
            SerializedProperty worldZoneDirector = so.FindProperty("worldZoneDirector");
            SerializedProperty biomeMatrixDirector = so.FindProperty("biomeMatrixDirector");
            SerializedProperty slopeProbeMeters = so.FindProperty("slopeProbeMeters");

            if (mapMagicBridge == null || mapMagicBridge.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFieldSampler is using runtime auto-resolve for MapMagicBridge.", worldProceduralFieldSampler);
                warningCount++;
            }

            if (worldZoneDirector == null || worldZoneDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFieldSampler is using runtime auto-resolve for WorldZoneDirector.", worldProceduralFieldSampler);
                warningCount++;
            }

            if (biomeMatrixDirector == null || biomeMatrixDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralFieldSampler is using runtime auto-resolve for BiomeMatrixDirector.", worldProceduralFieldSampler);
                warningCount++;
            }

            if (slopeProbeMeters == null || slopeProbeMeters.floatValue < 1f)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralFieldSampler slopeProbeMeters is too low.", worldProceduralFieldSampler);
                errorCount++;
            }
        }

        private static void ValidateWorldProceduralScatterDirector(
            WorldProceduralScatterDirector worldProceduralScatterDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldProceduralScatterDirector);
            SerializedProperty fieldSampler = so.FindProperty("fieldSampler");
            SerializedProperty proceduralFillDirector = so.FindProperty("proceduralFillDirector");
            SerializedProperty patternCatalog = so.FindProperty("patternCatalog");
            SerializedProperty biomeContextCatalog = so.FindProperty("biomeContextCatalog");
            SerializedProperty cellSize = so.FindProperty("cellSize");
            SerializedProperty radiusCells = so.FindProperty("radiusCells");
            SerializedProperty groundPlacementsPerCell = so.FindProperty("groundPlacementsPerCell");
            SerializedProperty clusterPlacementsPerCell = so.FindProperty("clusterPlacementsPerCell");
            SerializedProperty structureCellStride = so.FindProperty("structureCellStride");
            SerializedProperty structurePlacementsPerWindow = so.FindProperty("structurePlacementsPerWindow");
            SerializedProperty spawnCellStride = so.FindProperty("spawnCellStride");
            SerializedProperty spawnPlacementsPerWindow = so.FindProperty("spawnPlacementsPerWindow");

            if (fieldSampler == null || fieldSampler.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralScatterDirector is using runtime auto-resolve for WorldProceduralFieldSampler.", worldProceduralScatterDirector);
                warningCount++;
            }

            if (proceduralFillDirector == null || proceduralFillDirector.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldProceduralScatterDirector is using runtime auto-resolve for WorldProceduralFillDirector.", worldProceduralScatterDirector);
                warningCount++;
            }

            if (patternCatalog == null || patternCatalog.objectReferenceValue == null)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector is missing WorldProceduralPatternCatalog.", worldProceduralScatterDirector);
                errorCount++;
            }
            else if (patternCatalog.objectReferenceValue is WorldProceduralPatternCatalog catalog)
            {
                if (catalog.FallbackProfile == null)
                {
                    Debug.LogError("[MapMagicWorldValidation] WorldProceduralPatternCatalog is missing its fallback profile.", worldProceduralScatterDirector);
                    errorCount++;
                }

                WorldProceduralPattern[] patterns = (WorldProceduralPattern[])Enum.GetValues(typeof(WorldProceduralPattern));
                for (int i = 0; i < patterns.Length; i++)
                {
                    if (catalog.HasConfiguredProfile(patterns[i]))
                        continue;

                    Debug.LogError($"[MapMagicWorldValidation] WorldProceduralPatternCatalog is missing profile for {patterns[i]}.", worldProceduralScatterDirector);
                    errorCount++;
                }
            }

            if (biomeContextCatalog == null || biomeContextCatalog.objectReferenceValue == null)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector is missing WorldProceduralBiomeFamilyContextCatalog.", worldProceduralScatterDirector);
                errorCount++;
            }
            else if (biomeContextCatalog.objectReferenceValue is WorldProceduralBiomeFamilyContextCatalog contextCatalog)
            {
                if (contextCatalog.FallbackProfile == null)
                {
                    Debug.LogError("[MapMagicWorldValidation] WorldProceduralBiomeFamilyContextCatalog is missing its fallback profile.", worldProceduralScatterDirector);
                    errorCount++;
                }

                string[] biomeFamilyGuids = AssetDatabase.FindAssets("t:HectonBiomeFamilyProfile", new[] { "Assets/_Project/Data/Biomes/FamilyProfiles" });
                for (int i = 0; i < biomeFamilyGuids.Length; i++)
                {
                    string familyPath = AssetDatabase.GUIDToAssetPath(biomeFamilyGuids[i]);
                    HectonBiomeFamilyProfile familyProfile = AssetDatabase.LoadAssetAtPath<HectonBiomeFamilyProfile>(familyPath);
                    if (familyProfile == null || contextCatalog.HasConfiguredProfile(familyProfile))
                        continue;

                    Debug.LogError($"[MapMagicWorldValidation] WorldProceduralBiomeFamilyContextCatalog is missing profile for {familyProfile.familyId}.", worldProceduralScatterDirector);
                    errorCount++;
                }
            }

            if (cellSize == null || cellSize.floatValue < 8f)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector cellSize is too low.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (radiusCells == null || radiusCells.intValue < 2)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector radiusCells is too low.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (groundPlacementsPerCell == null || groundPlacementsPerCell.intValue <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector groundPlacementsPerCell must be positive.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (clusterPlacementsPerCell == null || clusterPlacementsPerCell.intValue <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector clusterPlacementsPerCell must be positive.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (structureCellStride == null || structureCellStride.intValue < 2)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector structureCellStride must be >= 2.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (structurePlacementsPerWindow == null || structurePlacementsPerWindow.intValue <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector structurePlacementsPerWindow must be positive.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (spawnCellStride == null || spawnCellStride.intValue < 2)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector spawnCellStride must be >= 2.", worldProceduralScatterDirector);
                errorCount++;
            }

            if (spawnPlacementsPerWindow == null || spawnPlacementsPerWindow.intValue <= 0)
            {
                Debug.LogError("[MapMagicWorldValidation] WorldProceduralScatterDirector spawnPlacementsPerWindow must be positive.", worldProceduralScatterDirector);
                errorCount++;
            }
        }

        private static bool HasMatchingPopulationRule(
            SerializedProperty rulesProperty,
            WorldZoneAnchor zone,
            WorldContentSocket socket)
        {
            if (rulesProperty == null)
                return false;

            for (int i = 0; i < rulesProperty.arraySize; i++)
            {
                SerializedProperty entry = rulesProperty.GetArrayElementAtIndex(i);
                if (entry == null || entry.objectReferenceValue == null)
                    continue;

                WorldPopulationRule rule = entry.objectReferenceValue as WorldPopulationRule;
                if (rule != null && rule.Matches(zone, socket))
                    return true;
            }

            return false;
        }

        private static WorldPopulationRule FindStrongestMatchingPopulationRule(
            SerializedProperty rulesProperty,
            WorldZoneAnchor zone,
            WorldContentSocket socket)
        {
            if (rulesProperty == null)
                return null;

            WorldPopulationRule bestRule = null;
            float bestWeight = float.MinValue;

            for (int i = 0; i < rulesProperty.arraySize; i++)
            {
                SerializedProperty entry = rulesProperty.GetArrayElementAtIndex(i);
                if (entry == null || entry.objectReferenceValue == null)
                    continue;

                WorldPopulationRule rule = entry.objectReferenceValue as WorldPopulationRule;
                if (rule == null || !rule.Matches(zone, socket))
                    continue;

                float weight = rule.GetEffectiveDensityWeight(zone, socket);
                if (bestRule != null && weight <= bestWeight)
                    continue;

                bestRule = rule;
                bestWeight = weight;
            }

            return bestRule;
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }

        private static Component FindSceneObjectIncludingInactive(Type componentType)
        {
            if (componentType == null)
                return null;

            UnityEngine.Object[] candidates = Resources.FindObjectsOfTypeAll(componentType);
            for (int i = 0; i < candidates.Length; i++)
            {
                Component candidate = candidates[i] as Component;
                if (candidate == null)
                    continue;

                GameObject go = candidate.gameObject;
                if (go == null || !go.scene.IsValid())
                    continue;

                return candidate;
            }

            return null;
        }
    }
}
