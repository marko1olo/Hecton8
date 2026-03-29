using Hecton8.Core;
using Hecton8.World;
using MapMagic.Core;
using System;
using GPUInstancer;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class MapMagicWorldValidator
    {
        [MenuItem("Hecton/Validation/Validate MapMagic World Stack", priority = 236)]
        public static void ValidateMapMagicWorldStack()
        {
            int errorCount = 0;
            int warningCount = 0;

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
            Component scatterBudgetController = FindSceneObjectIncludingInactive(
                Type.GetType("Hecton8.World.ScatterBudgetController, Assembly-CSharp"));

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

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[MapMagicWorldValidation] PASS no issues found.");
                return;
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

            if (mapMagic.draftsInPlaymode)
            {
                Debug.LogWarning("[MapMagicWorldValidation] draftsInPlaymode is enabled; this is expensive for runtime streaming.", mapMagic);
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
        }

        private static void ValidateWorldInterestDirector(
            WorldInterestDirector worldInterestDirector,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(worldInterestDirector);
            SerializedProperty scatterBudgetController = so.FindProperty("scatterBudgetController");
            if (scatterBudgetController == null || scatterBudgetController.objectReferenceValue == null)
            {
                Debug.LogWarning("[MapMagicWorldValidation] WorldInterestDirector is using runtime auto-resolve for ScatterBudgetController.", worldInterestDirector);
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
