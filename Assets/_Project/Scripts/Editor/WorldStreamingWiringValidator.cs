using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class WorldStreamingWiringValidator
    {
        private const string ManagersRootName = "[MANAGERS]";
        private const string WorldChunkStreamingProfilePath =
            "Assets/_Project/Data/World/Streaming/WorldChunkStreamingProfile.asset";
        private const string WorldProceduralFamilyFolder =
            "Assets/_Project/Data/World/ProceduralFamilies";
        private const string WorldProceduralRuleFolder =
            "Assets/_Project/Data/World/ProceduralPlacementRules";

        [MenuItem("Hecton/Validation/Validate World Streaming Wiring", priority = 235)]
        public static void ValidateWorldStreamingWiring()
        {
            int errorCount = 0;
            int warningCount = 0;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldStreamingWiring] No active loaded scene.");
                return;
            }

            WorldChunkStreamingProfile profile =
                AssetDatabase.LoadAssetAtPath<WorldChunkStreamingProfile>(WorldChunkStreamingProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[WorldStreamingWiring] Missing streaming profile asset at '{WorldChunkStreamingProfilePath}'.");
                errorCount++;
            }

            GameTickManager tickManager = FindSceneObjectIncludingInactive<GameTickManager>();
            if (tickManager == null)
            {
                Debug.LogError("[WorldStreamingWiring] Scene is missing GameTickManager.");
                errorCount++;
            }

            MapMagicBridge bridge = FindSceneObjectIncludingInactive<MapMagicBridge>();
            if (bridge == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing MapMagicBridge (biome/height sampling).");
                warningCount++;
            }

            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            if (scavengePopulator == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing ScavengePopulator.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    scavengePopulator,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] ScavengePopulator.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            FaunaDirector faunaDirector = FindSceneObjectIncludingInactive<FaunaDirector>();
            WorldFaunaSpawnRegistry faunaSpawnRegistry = FindSceneObjectIncludingInactive<WorldFaunaSpawnRegistry>();
            WorldProceduralStateRegistry proceduralStateRegistry = FindSceneObjectIncludingInactive<WorldProceduralStateRegistry>();
            if (faunaSpawnRegistry == null)
            {
                Debug.LogError("[WorldStreamingWiring] Scene is missing WorldFaunaSpawnRegistry.");
                errorCount++;
            }
            if (proceduralStateRegistry == null)
            {
                Debug.LogError("[WorldStreamingWiring] Scene is missing WorldProceduralStateRegistry.");
                errorCount++;
            }

            if (faunaDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing FaunaDirector.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    faunaDirector,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] FaunaDirector.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    faunaDirector,
                    "spawnRegistry",
                    faunaSpawnRegistry,
                    "[WorldStreamingWiring] FaunaDirector.spawnRegistry is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    faunaDirector,
                    "proceduralStateRegistry",
                    proceduralStateRegistry,
                    "[WorldStreamingWiring] FaunaDirector.proceduralStateRegistry is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            if (faunaSpawnRegistry != null)
            {
                ValidateObjectAssignment(
                    faunaSpawnRegistry,
                    "proceduralStateRegistry",
                    proceduralStateRegistry,
                    "[WorldStreamingWiring] WorldFaunaSpawnRegistry.proceduralStateRegistry is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            ScatterBudgetController scatterBudgetController = FindSceneObjectIncludingInactive<ScatterBudgetController>();
            if (scatterBudgetController == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing ScatterBudgetController.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    scatterBudgetController,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] ScatterBudgetController.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            WorldSliceDirector sliceDirector = FindSceneObjectIncludingInactive<WorldSliceDirector>();
            if (sliceDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldSliceDirector.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    sliceDirector,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] WorldSliceDirector.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            WorldStreamingDirector streamingDirector = FindSceneObjectIncludingInactive<WorldStreamingDirector>();
            if (streamingDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldStreamingDirector.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    streamingDirector,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] WorldStreamingDirector.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            WorldProceduralScatterDirector scatterDirector = FindSceneObjectIncludingInactive<WorldProceduralScatterDirector>();
            WorldGenerativeGeologyIntegrationDirector geologyIntegrationDirector = FindSceneObjectIncludingInactive<WorldGenerativeGeologyIntegrationDirector>();
            WorldGenerativeGeologySeamExecutionDirector geologySeamExecutionDirector = FindSceneObjectIncludingInactive<WorldGenerativeGeologySeamExecutionDirector>();
            WorldGenerativeGeologyTerrainSeamApplier geologyTerrainSeamApplier = FindSceneObjectIncludingInactive<WorldGenerativeGeologyTerrainSeamApplier>();
            WorldGenerativeGeologyVoxelBridgeDirector geologyVoxelBridgeDirector = FindSceneObjectIncludingInactive<WorldGenerativeGeologyVoxelBridgeDirector>();
            HectonVoxelEngine voxelEngine = FindSceneObjectIncludingInactive<HectonVoxelEngine>();
            if (scatterDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldProceduralScatterDirector.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    scatterDirector,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] WorldProceduralScatterDirector.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    scatterDirector,
                    "faunaSpawnRegistry",
                    faunaSpawnRegistry,
                    "[WorldStreamingWiring] WorldProceduralScatterDirector.faunaSpawnRegistry is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    scatterDirector,
                    "proceduralStateRegistry",
                    proceduralStateRegistry,
                    "[WorldStreamingWiring] WorldProceduralScatterDirector.proceduralStateRegistry is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            if (geologyIntegrationDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldGenerativeGeologyIntegrationDirector.");
                warningCount++;
            }
            else
            {
                ValidateProfileAssignment(
                    geologyIntegrationDirector,
                    "chunkStreamingProfile",
                    profile,
                    "[WorldStreamingWiring] WorldGenerativeGeologyIntegrationDirector.chunkStreamingProfile is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    geologyIntegrationDirector,
                    "mapMagicBridge",
                    bridge,
                    "[WorldStreamingWiring] WorldGenerativeGeologyIntegrationDirector.mapMagicBridge is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            if (geologySeamExecutionDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldGenerativeGeologySeamExecutionDirector.");
                warningCount++;
            }
            else
            {
                ValidateObjectAssignment(
                    geologySeamExecutionDirector,
                    "integrationDirector",
                    geologyIntegrationDirector,
                    "[WorldStreamingWiring] WorldGenerativeGeologySeamExecutionDirector.integrationDirector is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            if (geologyTerrainSeamApplier == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldGenerativeGeologyTerrainSeamApplier.");
                warningCount++;
            }
            else
            {
                ValidateObjectAssignment(
                    geologyTerrainSeamApplier,
                    "integrationDirector",
                    geologyIntegrationDirector,
                    "[WorldStreamingWiring] WorldGenerativeGeologyTerrainSeamApplier.integrationDirector is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            if (geologyVoxelBridgeDirector == null)
            {
                Debug.LogWarning("[WorldStreamingWiring] Scene is missing WorldGenerativeGeologyVoxelBridgeDirector.");
                warningCount++;
            }
            else
            {
                ValidateObjectAssignment(
                    geologyVoxelBridgeDirector,
                    "seamExecutionDirector",
                    geologySeamExecutionDirector,
                    "[WorldStreamingWiring] WorldGenerativeGeologyVoxelBridgeDirector.seamExecutionDirector is not assigned.",
                    ref errorCount,
                    ref warningCount);
                ValidateObjectAssignment(
                    geologyVoxelBridgeDirector,
                    "voxelEngine",
                    voxelEngine,
                    "[WorldStreamingWiring] WorldGenerativeGeologyVoxelBridgeDirector.voxelEngine is not assigned.",
                    ref errorCount,
                    ref warningCount);
            }

            ValidateProceduralFamilyCoverage(ref errorCount, ref warningCount);

            Debug.Log($"[WorldStreamingWiring] Validation complete. Errors={errorCount}, Warnings={warningCount}.");
        }

        [MenuItem("Hecton/Validation/Fix World Streaming Wiring", priority = 236)]
        public static void FixWorldStreamingWiring()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[WorldStreamingWiring] No active loaded scene.");
                return;
            }

            WorldChunkStreamingProfile profile =
                AssetDatabase.LoadAssetAtPath<WorldChunkStreamingProfile>(WorldChunkStreamingProfilePath);
            if (profile == null)
            {
                Debug.LogError($"[WorldStreamingWiring] Missing streaming profile asset at '{WorldChunkStreamingProfilePath}'.");
                return;
            }

            int fixedCount = 0;
            WorldProceduralStateRegistry proceduralStateRegistry = FindSceneObjectIncludingInactive<WorldProceduralStateRegistry>();
            if (proceduralStateRegistry == null)
            {
                GameObject managersRoot = EnsureManagersRoot();
                proceduralStateRegistry = managersRoot.GetComponent<WorldProceduralStateRegistry>();
                if (proceduralStateRegistry == null)
                {
                    proceduralStateRegistry = managersRoot.AddComponent<WorldProceduralStateRegistry>();
                    fixedCount++;
                }
            }

            ScavengePopulator scavengePopulator = FindSceneObjectIncludingInactive<ScavengePopulator>();
            if (scavengePopulator != null && AssignProfileIfMissing(scavengePopulator, profile))
                fixedCount++;

            FaunaDirector faunaDirector = FindSceneObjectIncludingInactive<FaunaDirector>();
            if (faunaDirector != null && AssignProfileIfMissing(faunaDirector, profile))
                fixedCount++;

            WorldFaunaSpawnRegistry faunaSpawnRegistry = FindSceneObjectIncludingInactive<WorldFaunaSpawnRegistry>();
            if (faunaDirector != null && faunaSpawnRegistry != null && AssignObjectIfMissing(faunaDirector, "spawnRegistry", faunaSpawnRegistry))
            {
                faunaDirector.SetSpawnRegistry(faunaSpawnRegistry);
                fixedCount++;
            }
            if (faunaDirector != null && proceduralStateRegistry != null && AssignObjectIfMissing(faunaDirector, "proceduralStateRegistry", proceduralStateRegistry))
            {
                faunaDirector.SetProceduralStateRegistry(proceduralStateRegistry);
                fixedCount++;
            }
            if (faunaSpawnRegistry != null && proceduralStateRegistry != null && AssignObjectIfMissing(faunaSpawnRegistry, "proceduralStateRegistry", proceduralStateRegistry))
            {
                faunaSpawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
                fixedCount++;
            }

            ScatterBudgetController scatterBudgetController = FindSceneObjectIncludingInactive<ScatterBudgetController>();
            if (scatterBudgetController != null && AssignProfileIfMissing(scatterBudgetController, profile))
                fixedCount++;

            WorldSliceDirector sliceDirector = FindSceneObjectIncludingInactive<WorldSliceDirector>();
            if (sliceDirector != null && AssignProfileIfMissing(sliceDirector, profile))
                fixedCount++;

            WorldStreamingDirector streamingDirector = FindSceneObjectIncludingInactive<WorldStreamingDirector>();
            if (streamingDirector != null && AssignProfileIfMissing(streamingDirector, profile))
                fixedCount++;

            WorldProceduralScatterDirector scatterDirector = FindSceneObjectIncludingInactive<WorldProceduralScatterDirector>();
            if (scatterDirector != null && AssignProfileIfMissing(scatterDirector, profile))
                fixedCount++;
            if (scatterDirector != null && faunaSpawnRegistry != null && AssignObjectIfMissing(scatterDirector, "faunaSpawnRegistry", faunaSpawnRegistry))
            {
                scatterDirector.SetFaunaSpawnRegistry(faunaSpawnRegistry);
                fixedCount++;
            }
            if (scatterDirector != null && proceduralStateRegistry != null && AssignObjectIfMissing(scatterDirector, "proceduralStateRegistry", proceduralStateRegistry))
            {
                scatterDirector.SetProceduralStateRegistry(proceduralStateRegistry);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                Debug.Log($"[WorldStreamingWiring] Fixed {fixedCount} missing streaming/state wiring issue(s).");
            }
            else
            {
                Debug.Log("[WorldStreamingWiring] No fixes needed.");
            }
        }

        private static bool AssignProfileIfMissing(MonoBehaviour behaviour, WorldChunkStreamingProfile profile)
        {
            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty("chunkStreamingProfile");
            if (prop == null)
                return false;

            if (prop.objectReferenceValue != null)
                return false;

            prop.objectReferenceValue = profile;
            so.ApplyModifiedPropertiesWithoutUndo();

            // If the behaviour exposes an explicit setter, call it to refresh runtime caches.
            if (behaviour is ScavengePopulator scavenge)
                scavenge.SetChunkStreamingProfile(profile);
            else if (behaviour is FaunaDirector fauna)
                fauna.SetChunkStreamingProfile(profile);
            else if (behaviour is ScatterBudgetController scatterBudget)
                scatterBudget.SetChunkStreamingProfile(profile);
            else if (behaviour is WorldSliceDirector sliceDirector)
                sliceDirector.SetChunkStreamingProfile(profile);
            else if (behaviour is WorldStreamingDirector streamingDirector)
                streamingDirector.SetChunkStreamingProfile(profile);
            else if (behaviour is WorldProceduralScatterDirector scatterDirector)
                scatterDirector.SetChunkStreamingProfile(profile);

            EditorUtility.SetDirty(behaviour);
            return true;
        }

        private static bool AssignObjectIfMissing(MonoBehaviour behaviour, string propertyName, UnityEngine.Object value)
        {
            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.objectReferenceValue != null)
                return false;

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
            return true;
        }

        private static GameObject EnsureManagersRoot()
        {
            GameObject managersRoot = GameObject.Find(ManagersRootName);
            if (managersRoot == null)
                managersRoot = new GameObject(ManagersRootName);

            return managersRoot;
        }

        private static void ValidateProfileAssignment(
            MonoBehaviour behaviour,
            string propertyName,
            WorldChunkStreamingProfile expectedProfile,
            string missingMessage,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[WorldStreamingWiring] '{behaviour.GetType().Name}' has no '{propertyName}' property.");
                warningCount++;
                return;
            }

            if (expectedProfile == null)
            {
                // profile asset missing already counted as error; avoid spam
                return;
            }

            if (prop.objectReferenceValue == null)
            {
                Debug.LogError(missingMessage);
                errorCount++;
                return;
            }

            if (!ReferenceEquals(prop.objectReferenceValue, expectedProfile))
            {
                Debug.LogWarning(
                    $"[WorldStreamingWiring] '{behaviour.GetType().Name}.{propertyName}' points to a different profile asset. Expected: {expectedProfile.name}");
                warningCount++;
            }
        }

        private static void ValidateObjectAssignment(
            MonoBehaviour behaviour,
            string propertyName,
            UnityEngine.Object expectedObject,
            string missingMessage,
            ref int errorCount,
            ref int warningCount)
        {
            SerializedObject so = new SerializedObject(behaviour);
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[WorldStreamingWiring] '{behaviour.GetType().Name}' has no '{propertyName}' property.");
                warningCount++;
                return;
            }

            if (expectedObject == null)
                return;

            if (prop.objectReferenceValue == null)
            {
                Debug.LogError(missingMessage);
                errorCount++;
                return;
            }

            if (!ReferenceEquals(prop.objectReferenceValue, expectedObject))
            {
                Debug.LogWarning(
                    $"[WorldStreamingWiring] '{behaviour.GetType().Name}.{propertyName}' points to a different object than expected '{expectedObject.name}'.");
                warningCount++;
            }
        }

        private static void ValidateProceduralFamilyCoverage(ref int errorCount, ref int warningCount)
        {
            Dictionary<WorldStreamingLayer, int> familiesPerLayer = new Dictionary<WorldStreamingLayer, int>();
            Dictionary<WorldStreamingLayer, int> finalReadyFamiliesPerLayer = new Dictionary<WorldStreamingLayer, int>();
            Dictionary<WorldStreamingLayer, int> realFinalReadyFamiliesPerLayer = new Dictionary<WorldStreamingLayer, int>();
            Dictionary<WorldStreamingLayer, int> placeholderFinalReadyFamiliesPerLayer = new Dictionary<WorldStreamingLayer, int>();
            int explicitOverrideCount = 0;
            int finalReadyFamilyCount = 0;
            int realFinalReadyFamilyCount = 0;
            int placeholderFinalReadyFamilyCount = 0;
            int largeThreatZoneFamilyCount = 0;

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { WorldProceduralFamilyFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(path);
                if (family == null || !family.allowRuntimeScatter)
                    continue;

                WorldStreamingLayer resolvedLayer = family.ResolveStreamingLayer();
                if (familiesPerLayer.TryGetValue(resolvedLayer, out int count))
                    familiesPerLayer[resolvedLayer] = count + 1;
                else
                    familiesPerLayer.Add(resolvedLayer, 1);

                bool hasFinalReadyVariant = FamilyHasFinalReadyVariant(family);
                bool hasRealFinalReadyVariant = FamilyHasRealFinalReadyVariant(family);
                bool hasPlaceholderFinalReadyVariant = FamilyHasPlaceholderFinalReadyVariant(family);

                if (family.overrideStreamingLayer)
                    explicitOverrideCount++;
                if (family.ResolveContributesLargeThreatZone())
                    largeThreatZoneFamilyCount++;
                if (hasFinalReadyVariant)
                {
                    finalReadyFamilyCount++;
                    if (finalReadyFamiliesPerLayer.TryGetValue(resolvedLayer, out int finalReadyCount))
                        finalReadyFamiliesPerLayer[resolvedLayer] = finalReadyCount + 1;
                    else
                        finalReadyFamiliesPerLayer.Add(resolvedLayer, 1);
                }
                if (hasRealFinalReadyVariant)
                {
                    realFinalReadyFamilyCount++;
                    if (realFinalReadyFamiliesPerLayer.TryGetValue(resolvedLayer, out int realCount))
                        realFinalReadyFamiliesPerLayer[resolvedLayer] = realCount + 1;
                    else
                        realFinalReadyFamiliesPerLayer.Add(resolvedLayer, 1);
                }
                if (hasPlaceholderFinalReadyVariant)
                {
                    placeholderFinalReadyFamilyCount++;
                    if (placeholderFinalReadyFamiliesPerLayer.TryGetValue(resolvedLayer, out int placeholderCount))
                        placeholderFinalReadyFamiliesPerLayer[resolvedLayer] = placeholderCount + 1;
                    else
                        placeholderFinalReadyFamiliesPerLayer.Add(resolvedLayer, 1);
                }

                if (family.ResolveContributesLargeThreatZone() && resolvedLayer != WorldStreamingLayer.LargeThreats)
                {
                    Debug.LogError($"[WorldStreamingWiring] '{family.name}' is marked as a large-threat zone but resolves to '{resolvedLayer}'.");
                    errorCount++;
                }

                if (family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Generic && !family.overrideStreamingLayer)
                {
                    Debug.LogWarning($"[WorldStreamingWiring] '{family.name}' still uses generic procedural domain without explicit streaming-layer override.");
                    warningCount++;
                }

                if (family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn &&
                    resolvedLayer != WorldStreamingLayer.Fauna &&
                    resolvedLayer != WorldStreamingLayer.LargeThreats)
                {
                    Debug.LogWarning($"[WorldStreamingWiring] '{family.name}' uses spawn scatter layer but resolves to '{resolvedLayer}'.");
                    warningCount++;
                }

                if (!family.overrideStreamingLayer)
                {
                    Debug.LogWarning($"[WorldStreamingWiring] '{family.name}' still relies on code fallback for streaming-layer resolution.");
                    warningCount++;
                }
            }

            WarnIfLayerHasNoFinalReadyCoverage(WorldStreamingLayer.TerrainLod, finalReadyFamiliesPerLayer, ref warningCount);
            WarnIfLayerHasNoFinalReadyCoverage(WorldStreamingLayer.Flora, finalReadyFamiliesPerLayer, ref warningCount);
            WarnIfLayerHasNoFinalReadyCoverage(WorldStreamingLayer.Debris, finalReadyFamiliesPerLayer, ref warningCount);
            WarnIfLayerHasNoFinalReadyCoverage(WorldStreamingLayer.Construction, finalReadyFamiliesPerLayer, ref warningCount);

            int ruleCount = CountProceduralRules();
            Debug.Log(
                $"[WorldStreamingWiring] Procedural layer coverage: " +
                $"TerrainLod={GetLayerCount(familiesPerLayer, WorldStreamingLayer.TerrainLod)}, " +
                $"Flora={GetLayerCount(familiesPerLayer, WorldStreamingLayer.Flora)}, " +
                $"Debris={GetLayerCount(familiesPerLayer, WorldStreamingLayer.Debris)}, " +
                $"Resources={GetLayerCount(familiesPerLayer, WorldStreamingLayer.Resources)}, " +
                $"Fauna={GetLayerCount(familiesPerLayer, WorldStreamingLayer.Fauna)}, " +
                $"Construction={GetLayerCount(familiesPerLayer, WorldStreamingLayer.Construction)}, " +
                $"LargeThreats={GetLayerCount(familiesPerLayer, WorldStreamingLayer.LargeThreats)}, " +
                $"ExplicitOverrides={explicitOverrideCount}, FinalReadyFamilies={finalReadyFamilyCount}, RealFinalReadyFamilies={realFinalReadyFamilyCount}, PlaceholderFinalReadyFamilies={placeholderFinalReadyFamilyCount}, " +
                $"LargeThreatZoneFamilies={largeThreatZoneFamilyCount}, Rules={ruleCount}.");
            Debug.Log(
                $"[WorldStreamingWiring] Final-ready coverage by layer: " +
                $"TerrainLod={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.TerrainLod)}, " +
                $"Flora={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.Flora)}, " +
                $"Debris={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.Debris)}, " +
                $"Resources={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.Resources)}, " +
                $"Fauna={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.Fauna)}, " +
                $"Construction={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.Construction)}, " +
                $"LargeThreats={GetLayerCount(finalReadyFamiliesPerLayer, WorldStreamingLayer.LargeThreats)}.");
            Debug.Log(
                $"[WorldStreamingWiring] Real final-ready coverage by layer: " +
                $"TerrainLod={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.TerrainLod)}, " +
                $"Flora={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.Flora)}, " +
                $"Debris={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.Debris)}, " +
                $"Resources={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.Resources)}, " +
                $"Fauna={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.Fauna)}, " +
                $"Construction={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.Construction)}, " +
                $"LargeThreats={GetLayerCount(realFinalReadyFamiliesPerLayer, WorldStreamingLayer.LargeThreats)}.");
            Debug.Log(
                $"[WorldStreamingWiring] Placeholder-backed final-ready coverage by layer: " +
                $"TerrainLod={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.TerrainLod)}, " +
                $"Flora={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.Flora)}, " +
                $"Debris={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.Debris)}, " +
                $"Resources={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.Resources)}, " +
                $"Fauna={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.Fauna)}, " +
                $"Construction={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.Construction)}, " +
                $"LargeThreats={GetLayerCount(placeholderFinalReadyFamiliesPerLayer, WorldStreamingLayer.LargeThreats)}.");
        }

        private static int CountProceduralRules()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WorldProceduralPlacementRule)}", new[] { WorldProceduralRuleFolder });
            return guids != null ? guids.Length : 0;
        }

        private static int GetLayerCount(Dictionary<WorldStreamingLayer, int> familiesPerLayer, WorldStreamingLayer layer)
        {
            return familiesPerLayer.TryGetValue(layer, out int count) ? count : 0;
        }

        private static void WarnIfLayerHasNoFinalReadyCoverage(
            WorldStreamingLayer layer,
            Dictionary<WorldStreamingLayer, int> finalReadyFamiliesPerLayer,
            ref int warningCount)
        {
            if (GetLayerCount(finalReadyFamiliesPerLayer, layer) > 0)
                return;

            Debug.LogWarning($"[WorldStreamingWiring] Streaming layer '{layer}' still has no final-ready procedural families.");
            warningCount++;
        }

        private static bool FamilyHasFinalReadyVariant(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return false;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly)
                    return true;
            }

            return false;
        }

        private static bool FamilyHasRealFinalReadyVariant(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return false;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly && !WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    return true;
            }

            return false;
        }

        private static bool FamilyHasPlaceholderFinalReadyVariant(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return false;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly && WorldProceduralPlaceholderAuthoring.IsPlaceholderFinalVariant(variant))
                    return true;
            }

            return false;
        }

        private static T FindSceneObjectIncludingInactive<T>() where T : UnityEngine.Object
        {
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }
    }
}

