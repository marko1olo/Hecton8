using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Authors real procedural support finals for pockets, creature spawns, and large-threat ownership zones.
    /// </summary>
    public static class WorldProceduralSupportFinalAuthoring
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/WorldSupport";
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/WorldSupport/Final";
        private const string VentSheenMaterialPath = "Assets/_Project/Art/Materials/Construction/Mat_RuinSeepSheen.mat";
        private const string IndustrialStripeDecalPrefabPath = "Assets/ScifiFacility/Prefabs/decals/stripes_03.prefab";
        private const string IndustrialScuffDecalPrefabPath = "Assets/ScifiFacility/Prefabs/decals/decal_04.prefab";
        private const string RuinApexStripeFrameChildName = "RuinApexStripe_Frame";
        private const string RuinApexStripeCrossSpanChildName = "RuinApexStripe_CrossSpan";
        private const string RuinApexScuffBaseChildName = "RuinApexScuff_Base";

        [MenuItem("Hecton/Authoring/Rebuild Procedural World Support Finals", priority = 179)]
        public static void RebuildWorldSupportFinals()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/WorldSupport");
            EnsureFolder(FinalPrefabFolder);

            Material resourceMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_ResourcePocket.mat", new Color(0.88f, 0.70f, 0.26f, 1f));
            Material hazardMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_HazardPocket.mat", new Color(0.86f, 0.34f, 0.16f, 1f));
            Material safeMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_SafePocket.mat", new Color(0.24f, 0.78f, 0.82f, 1f));
            Material passiveMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_CreaturePassive.mat", new Color(0.42f, 0.82f, 0.54f, 1f));
            Material predatorMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_CreaturePredator.mat", new Color(0.84f, 0.28f, 0.20f, 1f));
            Material abyssMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_AbyssApex.mat", new Color(0.42f, 0.48f, 0.62f, 1f));
            Material reefMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_ReefApex.mat", new Color(0.92f, 0.82f, 0.46f, 1f));
            Material ruinMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Support_RuinApex.mat", new Color(0.34f, 0.66f, 0.82f, 1f));

            int createdCount = 0;
            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Pocket_Resource.prefab", new Vector3(3.2f, 1.8f, 3.0f), BuildResourcePocketLods(resourceMat, passiveMat)) != null)
                createdCount++;

            GameObject hazardPocketPrefab = CreateCompositeFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Support_Pocket_Hazard.prefab",
                new Vector3(3.8f, 2.8f, 3.8f),
                BuildHazardPocketLods(hazardMat, predatorMat));
            if (hazardPocketPrefab != null)
            {
                AttachHazardVentVfx(hazardPocketPrefab);
                createdCount++;
            }

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Pocket_Safe.prefab", new Vector3(4.2f, 2.6f, 4.2f), BuildSafePocketLods(safeMat, passiveMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_CreatureSpawn_Passive.prefab", new Vector3(3.6f, 2.4f, 3.6f), BuildPassiveSpawnLods(passiveMat, safeMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_CreatureSpawn_Predator.prefab", new Vector3(4.2f, 2.8f, 4.0f), BuildPredatorSpawnLods(predatorMat, hazardMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Zone_LargeThreat.prefab", new Vector3(14f, 10f, 14f), BuildLargeThreatZoneLods(predatorMat, abyssMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Zone_AbyssApex.prefab", new Vector3(18f, 16f, 18f), BuildAbyssApexZoneLods(abyssMat, predatorMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Zone_ReefApex.prefab", new Vector3(15f, 10f, 15f), BuildReefApexZoneLods(reefMat, passiveMat)) != null)
                createdCount++;

            GameObject ruinApexPrefab = CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Support_Zone_RuinApex.prefab", new Vector3(17f, 14f, 17f), BuildRuinApexZoneLods(ruinMat, predatorMat));
            if (ruinApexPrefab != null)
            {
                AttachRuinApexIndustrialDecals(ruinApexPrefab);
                createdCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralSupportFinalAuthoring] Rebuilt world-support final prefabs. Created={createdCount}.");
        }

        private static CompositeLodSpec[] BuildResourcePocketLods(Material resourceMat, Material passiveMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("CoreShard", PrimitiveType.Cube, new Vector3(0.9f, 1.1f, 0.8f), new Vector3(0f, 0.56f, 0f), new Vector3(0f, 18f, -8f), resourceMat),
                        new VisualPrimitiveSpec("ShardA", PrimitiveType.Cylinder, new Vector3(0.34f, 1.4f, 0.34f), new Vector3(-0.74f, 0.68f, 0.54f), new Vector3(10f, 0f, 18f), resourceMat),
                        new VisualPrimitiveSpec("ShardB", PrimitiveType.Cylinder, new Vector3(0.28f, 1.2f, 0.28f), new Vector3(0.82f, 0.58f, -0.42f), new Vector3(-8f, 0f, -16f), resourceMat),
                        new VisualPrimitiveSpec("BaseChunkA", PrimitiveType.Sphere, new Vector3(0.82f, 0.46f, 0.76f), new Vector3(-0.58f, 0.18f, -0.22f), Vector3.zero, resourceMat),
                        new VisualPrimitiveSpec("BaseChunkB", PrimitiveType.Sphere, new Vector3(0.68f, 0.36f, 0.62f), new Vector3(0.52f, 0.16f, 0.34f), Vector3.zero, resourceMat),
                        new VisualPrimitiveSpec("ForagerA", PrimitiveType.Capsule, new Vector3(0.24f, 0.92f, 0.24f), new Vector3(-0.24f, 0.34f, 0.78f), new Vector3(6f, 0f, 12f), passiveMat),
                        new VisualPrimitiveSpec("ForagerB", PrimitiveType.Capsule, new Vector3(0.2f, 0.78f, 0.2f), new Vector3(0.44f, 0.28f, -0.66f), new Vector3(-8f, 0f, -14f), passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("MassA", PrimitiveType.Cube, new Vector3(1.2f, 0.92f, 1.0f), new Vector3(0f, 0.46f, 0f), new Vector3(0f, 12f, 0f), resourceMat),
                        new VisualPrimitiveSpec("MassB", PrimitiveType.Cylinder, new Vector3(0.30f, 1.1f, 0.30f), new Vector3(-0.42f, 0.54f, 0.24f), new Vector3(0f, 0f, 14f), resourceMat),
                        new VisualPrimitiveSpec("MassC", PrimitiveType.Cylinder, new Vector3(0.24f, 0.92f, 0.24f), new Vector3(0.46f, 0.44f, -0.18f), new Vector3(0f, 0f, -14f), resourceMat),
                        new VisualPrimitiveSpec("ForagerSilhouette", PrimitiveType.Capsule, new Vector3(0.42f, 0.9f, 0.42f), new Vector3(0.12f, 0.3f, 0.18f), Vector3.zero, passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Silhouette", PrimitiveType.Capsule, new Vector3(0.96f, 1.28f, 0.96f), new Vector3(0f, 0.62f, 0f), Vector3.zero, resourceMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildHazardPocketLods(Material hazardMat, Material predatorMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("VentCore", PrimitiveType.Cylinder, new Vector3(1.2f, 1.3f, 1.2f), new Vector3(0f, 0.62f, 0f), Vector3.zero, hazardMat),
                        new VisualPrimitiveSpec("SpineA", PrimitiveType.Cylinder, new Vector3(0.18f, 1.8f, 0.18f), new Vector3(-1.1f, 0.82f, 0.62f), new Vector3(18f, 0f, 28f), hazardMat),
                        new VisualPrimitiveSpec("SpineB", PrimitiveType.Cylinder, new Vector3(0.18f, 2.0f, 0.18f), new Vector3(1.0f, 0.92f, -0.54f), new Vector3(-18f, 0f, -26f), hazardMat),
                        new VisualPrimitiveSpec("SpineC", PrimitiveType.Cylinder, new Vector3(0.16f, 1.6f, 0.16f), new Vector3(-0.32f, 0.72f, -1.0f), new Vector3(12f, 0f, 14f), hazardMat),
                        new VisualPrimitiveSpec("SpineD", PrimitiveType.Cylinder, new Vector3(0.16f, 1.5f, 0.16f), new Vector3(0.44f, 0.70f, 1.02f), new Vector3(-10f, 0f, -18f), hazardMat),
                        new VisualPrimitiveSpec("ParasiteA", PrimitiveType.Cylinder, new Vector3(0.12f, 1.10f, 0.12f), new Vector3(-0.86f, 1.22f, 0.88f), new Vector3(0f, 0f, 20f), predatorMat),
                        new VisualPrimitiveSpec("ParasiteB", PrimitiveType.Cylinder, new Vector3(0.10f, 0.96f, 0.10f), new Vector3(0.92f, 1.14f, -0.82f), new Vector3(0f, 0f, -22f), predatorMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("VentMass", PrimitiveType.Cylinder, new Vector3(1.3f, 1.1f, 1.3f), new Vector3(0f, 0.54f, 0f), Vector3.zero, hazardMat),
                        new VisualPrimitiveSpec("SpineA", PrimitiveType.Cylinder, new Vector3(0.16f, 1.4f, 0.16f), new Vector3(-0.72f, 0.72f, 0.38f), new Vector3(10f, 0f, 18f), hazardMat),
                        new VisualPrimitiveSpec("SpineB", PrimitiveType.Cylinder, new Vector3(0.16f, 1.5f, 0.16f), new Vector3(0.68f, 0.76f, -0.34f), new Vector3(-10f, 0f, -18f), hazardMat),
                        new VisualPrimitiveSpec("PredatorPerch", PrimitiveType.Cylinder, new Vector3(0.14f, 1.08f, 0.14f), new Vector3(0f, 0.96f, 0.62f), new Vector3(0f, 0f, 12f), predatorMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("HazardSilhouette", PrimitiveType.Capsule, new Vector3(1.42f, 1.6f, 1.42f), new Vector3(0f, 0.8f, 0f), Vector3.zero, hazardMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildSafePocketLods(Material safeMat, Material passiveMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ShelterArch", PrimitiveType.Capsule, new Vector3(1.0f, 1.9f, 1.0f), new Vector3(-0.92f, 0.92f, 0f), new Vector3(0f, 0f, 42f), safeMat),
                        new VisualPrimitiveSpec("ShelterArchB", PrimitiveType.Capsule, new Vector3(1.0f, 1.9f, 1.0f), new Vector3(0.92f, 0.92f, 0f), new Vector3(0f, 0f, -42f), safeMat),
                        new VisualPrimitiveSpec("Canopy", PrimitiveType.Sphere, new Vector3(2.4f, 1.1f, 2.4f), new Vector3(0f, 1.32f, 0f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(1.8f, 0.2f, 1.8f), new Vector3(0f, 0.1f, 0f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("VisitorA", PrimitiveType.Capsule, new Vector3(0.28f, 1.10f, 0.28f), new Vector3(-0.68f, 0.58f, 0.72f), new Vector3(8f, 0f, 12f), passiveMat),
                        new VisualPrimitiveSpec("VisitorB", PrimitiveType.Capsule, new Vector3(0.24f, 0.92f, 0.24f), new Vector3(0.74f, 0.54f, -0.64f), new Vector3(-8f, 0f, -10f), passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Canopy", PrimitiveType.Sphere, new Vector3(2.2f, 1.0f, 2.2f), new Vector3(0f, 1.2f, 0f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(1.6f, 0.18f, 1.6f), new Vector3(0f, 0.09f, 0f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("Support", PrimitiveType.Capsule, new Vector3(0.82f, 1.5f, 0.82f), new Vector3(0f, 0.78f, 0f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("VisitorSilhouette", PrimitiveType.Capsule, new Vector3(0.52f, 1.12f, 0.52f), new Vector3(0f, 0.58f, 0.22f), Vector3.zero, passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("SafeSilhouette", PrimitiveType.Sphere, new Vector3(2.2f, 1.4f, 2.2f), new Vector3(0f, 0.78f, 0f), Vector3.zero, safeMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildPassiveSpawnLods(Material passiveMat, Material safeMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("SpawnRing", PrimitiveType.Cylinder, new Vector3(1.8f, 0.12f, 1.8f), new Vector3(0f, 0.08f, 0f), new Vector3(90f, 0f, 0f), passiveMat),
                        new VisualPrimitiveSpec("BeaconA", PrimitiveType.Capsule, new Vector3(0.42f, 1.6f, 0.42f), new Vector3(-0.92f, 0.78f, 0.54f), new Vector3(8f, 0f, 12f), passiveMat),
                        new VisualPrimitiveSpec("BeaconB", PrimitiveType.Capsule, new Vector3(0.38f, 1.4f, 0.38f), new Vector3(0.86f, 0.68f, -0.48f), new Vector3(-8f, 0f, -10f), passiveMat),
                        new VisualPrimitiveSpec("BeaconC", PrimitiveType.Capsule, new Vector3(0.32f, 1.2f, 0.32f), new Vector3(0.24f, 0.60f, 0.96f), new Vector3(10f, 0f, -6f), safeMat),
                        new VisualPrimitiveSpec("FryA", PrimitiveType.Capsule, new Vector3(0.18f, 0.68f, 0.18f), new Vector3(-0.34f, 0.42f, 0.88f), new Vector3(12f, 0f, 18f), passiveMat),
                        new VisualPrimitiveSpec("FryB", PrimitiveType.Capsule, new Vector3(0.16f, 0.58f, 0.16f), new Vector3(0.46f, 0.36f, -0.82f), new Vector3(-10f, 0f, -16f), passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("SpawnMass", PrimitiveType.Cylinder, new Vector3(1.4f, 0.18f, 1.4f), new Vector3(0f, 0.09f, 0f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("BeaconA", PrimitiveType.Capsule, new Vector3(0.34f, 1.28f, 0.34f), new Vector3(-0.52f, 0.62f, 0.22f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("BeaconB", PrimitiveType.Capsule, new Vector3(0.30f, 1.08f, 0.30f), new Vector3(0.58f, 0.54f, -0.26f), Vector3.zero, safeMat),
                        new VisualPrimitiveSpec("SpawnVisitor", PrimitiveType.Capsule, new Vector3(0.34f, 0.72f, 0.34f), new Vector3(0.14f, 0.28f, 0.42f), Vector3.zero, passiveMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("SpawnSilhouette", PrimitiveType.Capsule, new Vector3(1.0f, 1.28f, 1.0f), new Vector3(0f, 0.62f, 0f), Vector3.zero, passiveMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildPredatorSpawnLods(Material predatorMat, Material hazardMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("NestCore", PrimitiveType.Sphere, new Vector3(1.6f, 0.72f, 1.6f), new Vector3(0f, 0.28f, 0f), Vector3.zero, hazardMat),
                        new VisualPrimitiveSpec("ToothA", PrimitiveType.Cylinder, new Vector3(0.16f, 1.8f, 0.16f), new Vector3(-0.84f, 0.86f, 0.62f), new Vector3(0f, 0f, 32f), predatorMat),
                        new VisualPrimitiveSpec("ToothB", PrimitiveType.Cylinder, new Vector3(0.16f, 2.0f, 0.16f), new Vector3(0.92f, 0.92f, -0.46f), new Vector3(0f, 0f, -34f), predatorMat),
                        new VisualPrimitiveSpec("ToothC", PrimitiveType.Cylinder, new Vector3(0.14f, 1.6f, 0.14f), new Vector3(-0.28f, 0.74f, -1.02f), new Vector3(0f, 0f, 20f), predatorMat),
                        new VisualPrimitiveSpec("ToothD", PrimitiveType.Cylinder, new Vector3(0.14f, 1.5f, 0.14f), new Vector3(0.42f, 0.70f, 1.0f), new Vector3(0f, 0f, -18f), predatorMat),
                        new VisualPrimitiveSpec("ScoutA", PrimitiveType.Cylinder, new Vector3(0.10f, 0.96f, 0.10f), new Vector3(-0.62f, 1.08f, 0.94f), new Vector3(0f, 0f, 18f), predatorMat),
                        new VisualPrimitiveSpec("ScoutB", PrimitiveType.Cylinder, new Vector3(0.10f, 0.88f, 0.10f), new Vector3(0.68f, 1.02f, -0.88f), new Vector3(0f, 0f, -20f), predatorMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("NestMass", PrimitiveType.Sphere, new Vector3(1.8f, 0.66f, 1.8f), new Vector3(0f, 0.24f, 0f), Vector3.zero, hazardMat),
                        new VisualPrimitiveSpec("ToothA", PrimitiveType.Cylinder, new Vector3(0.14f, 1.4f, 0.14f), new Vector3(-0.48f, 0.68f, 0.28f), new Vector3(0f, 0f, 18f), predatorMat),
                        new VisualPrimitiveSpec("ToothB", PrimitiveType.Cylinder, new Vector3(0.14f, 1.5f, 0.14f), new Vector3(0.52f, 0.72f, -0.24f), new Vector3(0f, 0f, -18f), predatorMat),
                        new VisualPrimitiveSpec("ScoutPerch", PrimitiveType.Cylinder, new Vector3(0.12f, 0.94f, 0.12f), new Vector3(0.06f, 0.78f, 0.46f), new Vector3(0f, 0f, 12f), predatorMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("PredatorSilhouette", PrimitiveType.Capsule, new Vector3(1.4f, 1.4f, 1.4f), new Vector3(0f, 0.66f, 0f), Vector3.zero, predatorMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildLargeThreatZoneLods(Material predatorMat, Material abyssMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ZoneSpine", PrimitiveType.Cylinder, new Vector3(0.82f, 8.0f, 0.82f), new Vector3(0f, 4.0f, 0f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("ArchA", PrimitiveType.Capsule, new Vector3(1.2f, 9.2f, 1.2f), new Vector3(-3.8f, 4.6f, 0f), new Vector3(0f, 0f, 28f), abyssMat),
                        new VisualPrimitiveSpec("ArchB", PrimitiveType.Capsule, new Vector3(1.2f, 9.2f, 1.2f), new Vector3(3.8f, 4.6f, 0f), new Vector3(0f, 0f, -28f), abyssMat),
                        new VisualPrimitiveSpec("Ring", PrimitiveType.Cylinder, new Vector3(4.6f, 0.22f, 4.6f), new Vector3(0f, 2.2f, 0f), new Vector3(90f, 0f, 0f), predatorMat),
                        new VisualPrimitiveSpec("SentryA", PrimitiveType.Capsule, new Vector3(0.64f, 2.2f, 0.64f), new Vector3(-2.26f, 2.9f, 2.12f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("SentryB", PrimitiveType.Capsule, new Vector3(0.58f, 2.0f, 0.58f), new Vector3(2.34f, 2.76f, -2.04f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(5.4f, 0.28f, 5.4f), new Vector3(0f, 0.14f, 0f), Vector3.zero, abyssMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Spine", PrimitiveType.Cylinder, new Vector3(0.68f, 7.0f, 0.68f), new Vector3(0f, 3.5f, 0f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("ArchMass", PrimitiveType.Capsule, new Vector3(1.0f, 7.8f, 1.0f), new Vector3(0f, 3.8f, 0f), new Vector3(0f, 0f, 90f), abyssMat),
                        new VisualPrimitiveSpec("SentrySilhouette", PrimitiveType.Capsule, new Vector3(0.86f, 2.4f, 0.86f), new Vector3(0.18f, 2.84f, 0.26f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(4.8f, 0.22f, 4.8f), new Vector3(0f, 0.11f, 0f), Vector3.zero, abyssMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ZoneSilhouette", PrimitiveType.Capsule, new Vector3(3.8f, 8.0f, 3.8f), new Vector3(0f, 4.0f, 0f), Vector3.zero, predatorMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildAbyssApexZoneLods(Material abyssMat, Material predatorMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Monolith", PrimitiveType.Cube, new Vector3(4.6f, 11.8f, 4.6f), new Vector3(0f, 5.9f, 0f), new Vector3(0f, 8f, 0f), abyssMat),
                        new VisualPrimitiveSpec("FinA", PrimitiveType.Cube, new Vector3(1.2f, 9.4f, 8.6f), new Vector3(-3.8f, 4.8f, 0f), new Vector3(0f, 18f, 14f), predatorMat),
                        new VisualPrimitiveSpec("FinB", PrimitiveType.Cube, new Vector3(1.2f, 9.4f, 8.6f), new Vector3(3.8f, 4.8f, 0f), new Vector3(0f, -18f, -14f), predatorMat),
                        new VisualPrimitiveSpec("Halo", PrimitiveType.Cylinder, new Vector3(6.2f, 0.28f, 6.2f), new Vector3(0f, 7.2f, 0f), new Vector3(90f, 0f, 0f), predatorMat),
                        new VisualPrimitiveSpec("WatcherA", PrimitiveType.Capsule, new Vector3(0.72f, 2.6f, 0.72f), new Vector3(-2.12f, 6.9f, 2.36f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("WatcherB", PrimitiveType.Capsule, new Vector3(0.66f, 2.3f, 0.66f), new Vector3(2.34f, 6.34f, -2.08f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(10.4f, 0.5f, 10.4f), new Vector3(0f, 0.24f, 0f), Vector3.zero, abyssMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Mass", PrimitiveType.Cube, new Vector3(5.0f, 10.4f, 5.0f), new Vector3(0f, 5.2f, 0f), Vector3.zero, abyssMat),
                        new VisualPrimitiveSpec("CrossFin", PrimitiveType.Cube, new Vector3(1.0f, 8.0f, 7.0f), new Vector3(0f, 4.0f, 0f), new Vector3(0f, 0f, 90f), predatorMat),
                        new VisualPrimitiveSpec("WatcherSilhouette", PrimitiveType.Capsule, new Vector3(0.96f, 2.8f, 0.96f), new Vector3(0.34f, 6.08f, 0.42f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(9.2f, 0.4f, 9.2f), new Vector3(0f, 0.2f, 0f), Vector3.zero, abyssMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("AbyssSilhouette", PrimitiveType.Capsule, new Vector3(5.4f, 10.8f, 5.4f), new Vector3(0f, 5.4f, 0f), Vector3.zero, abyssMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildReefApexZoneLods(Material reefMat, Material passiveMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Canopy", PrimitiveType.Sphere, new Vector3(7.2f, 3.4f, 7.2f), new Vector3(0f, 4.6f, 0f), Vector3.zero, reefMat),
                        new VisualPrimitiveSpec("StemA", PrimitiveType.Capsule, new Vector3(1.0f, 7.4f, 1.0f), new Vector3(-2.4f, 3.6f, 2.0f), new Vector3(0f, 0f, 16f), passiveMat),
                        new VisualPrimitiveSpec("StemB", PrimitiveType.Capsule, new Vector3(0.92f, 6.8f, 0.92f), new Vector3(2.6f, 3.3f, -1.8f), new Vector3(0f, 0f, -14f), passiveMat),
                        new VisualPrimitiveSpec("StemC", PrimitiveType.Capsule, new Vector3(0.86f, 6.2f, 0.86f), new Vector3(0.6f, 3.0f, 2.8f), new Vector3(0f, 0f, 8f), passiveMat),
                        new VisualPrimitiveSpec("DriftVisitorA", PrimitiveType.Capsule, new Vector3(0.56f, 1.8f, 0.56f), new Vector3(-1.82f, 4.9f, 1.58f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("DriftVisitorB", PrimitiveType.Capsule, new Vector3(0.52f, 1.58f, 0.52f), new Vector3(1.94f, 4.72f, -1.36f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(5.6f, 0.32f, 5.6f), new Vector3(0f, 0.16f, 0f), Vector3.zero, reefMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Canopy", PrimitiveType.Sphere, new Vector3(6.4f, 3.0f, 6.4f), new Vector3(0f, 4.1f, 0f), Vector3.zero, reefMat),
                        new VisualPrimitiveSpec("StemMass", PrimitiveType.Capsule, new Vector3(1.1f, 6.0f, 1.1f), new Vector3(0f, 3.0f, 0f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("CanopyVisitor", PrimitiveType.Capsule, new Vector3(0.72f, 1.9f, 0.72f), new Vector3(0.42f, 4.28f, 0.38f), Vector3.zero, passiveMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(5.0f, 0.28f, 5.0f), new Vector3(0f, 0.14f, 0f), Vector3.zero, reefMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ReefSilhouette", PrimitiveType.Capsule, new Vector3(6.0f, 7.0f, 6.0f), new Vector3(0f, 3.5f, 0f), Vector3.zero, reefMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildRuinApexZoneLods(Material ruinMat, Material predatorMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("FrameA", PrimitiveType.Cube, new Vector3(1.4f, 10.4f, 1.4f), new Vector3(-3.8f, 5.2f, 2.8f), new Vector3(0f, 8f, 4f), ruinMat),
                        new VisualPrimitiveSpec("FrameB", PrimitiveType.Cube, new Vector3(1.4f, 9.8f, 1.4f), new Vector3(3.6f, 4.9f, -2.6f), new Vector3(0f, -10f, -4f), ruinMat),
                        new VisualPrimitiveSpec("CrossSpan", PrimitiveType.Cube, new Vector3(8.8f, 0.52f, 2.2f), new Vector3(0f, 7.8f, 0f), new Vector3(0f, 16f, 0f), ruinMat),
                        new VisualPrimitiveSpec("ThreatNest", PrimitiveType.Sphere, new Vector3(3.8f, 2.2f, 3.8f), new Vector3(0f, 1.1f, 0f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("PerchA", PrimitiveType.Capsule, new Vector3(0.68f, 2.2f, 0.68f), new Vector3(-2.34f, 6.46f, 1.94f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("PerchB", PrimitiveType.Capsule, new Vector3(0.62f, 2.0f, 0.62f), new Vector3(2.18f, 6.18f, -1.74f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Anchor", PrimitiveType.Cylinder, new Vector3(4.4f, 0.3f, 4.4f), new Vector3(0f, 0.15f, 0f), Vector3.zero, ruinMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("FrameMass", PrimitiveType.Cube, new Vector3(7.6f, 7.8f, 2.4f), new Vector3(0f, 4.0f, 0f), new Vector3(0f, 12f, 0f), ruinMat),
                        new VisualPrimitiveSpec("Nest", PrimitiveType.Sphere, new Vector3(3.4f, 2.0f, 3.4f), new Vector3(0f, 1.0f, 0f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("NestSentinel", PrimitiveType.Capsule, new Vector3(0.9f, 2.5f, 0.9f), new Vector3(0.22f, 3.16f, 0.38f), Vector3.zero, predatorMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cylinder, new Vector3(4.0f, 0.24f, 4.0f), new Vector3(0f, 0.12f, 0f), Vector3.zero, ruinMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralSupportContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("RuinSilhouette", PrimitiveType.Capsule, new Vector3(5.6f, 8.4f, 5.6f), new Vector3(0f, 4.2f, 0f), Vector3.zero, ruinMat),
                    }),
            };
        }

        private static GameObject CreateCompositeFinalPrefab(string prefabPath, Vector3 colliderSize, CompositeLodSpec[] lodSpecs)
        {
            if (lodSpecs == null || lodSpecs.Length <= 0)
                return null;

            GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            try
            {
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = colliderSize;
                collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);

                BuildCompositeVisuals(root.transform, lodSpecs);
                ApplyAmbientFaunaShadowPolicy(root.transform);
                EditorUtility.SetDirty(root);
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return savedPrefab != null ? savedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildCompositeVisuals(Transform parent, CompositeLodSpec[] lodSpecs)
        {
            LOD[] lods = new LOD[lodSpecs.Length];
            for (int i = 0; i < lodSpecs.Length; i++)
            {
                GameObject lodRoot = new GameObject($"LOD{i}");
                lodRoot.transform.SetParent(parent, false);

                VisualPrimitiveSpec[] visuals = lodSpecs[i].Visuals;
                List<Renderer> renderers = new List<Renderer>(visuals != null ? visuals.Length : 0);
                BuildCompositeVisualGroup(lodRoot.transform, visuals, renderers);
                lods[i] = new LOD(lodSpecs[i].ScreenRelativeTransitionHeight, renderers.ToArray());
            }

            LODGroup lodGroup = parent.gameObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
        }

        private static void BuildCompositeVisualGroup(Transform parent, VisualPrimitiveSpec[] visuals, List<Renderer> renderers)
        {
            if (visuals == null || visuals.Length <= 0)
                return;

            for (int i = 0; i < visuals.Length; i++)
            {
                Renderer renderer = CreateVisualPrimitive(
                    parent,
                    visuals[i].Name,
                    visuals[i].PrimitiveType,
                    visuals[i].Scale,
                    visuals[i].Material,
                    visuals[i].LocalPosition,
                    visuals[i].LocalEulerAngles);

                if (renderer != null)
                    renderers.Add(renderer);
            }
        }

        private static void ApplyAmbientFaunaShadowPolicy(Transform root)
        {
            if (root == null)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!IsAmbientFaunaPrimitive(renderer.gameObject.name))
                    continue;

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static Renderer CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 scale,
            Material material,
            Vector3 localPosition,
            Vector3 localEulerAngles)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = Quaternion.Euler(localEulerAngles);
            visual.transform.localScale = scale;

            if (visual.TryGetComponent(out Collider visualCollider))
                Object.DestroyImmediate(visualCollider);

            if (visual.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
                if (IsAmbientFaunaPrimitive(name))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }

            return renderer;
        }

        private static bool IsAmbientFaunaPrimitive(string name)
        {
            switch (name)
            {
                case "VisitorA":
                case "VisitorB":
                case "VisitorSilhouette":
                case "ForagerA":
                case "ForagerB":
                case "ForagerSilhouette":
                case "FryA":
                case "FryB":
                case "SpawnVisitor":
                case "ParasiteA":
                case "ParasiteB":
                case "PredatorPerch":
                case "ScoutA":
                case "ScoutB":
                case "ScoutPerch":
                case "DriftVisitorA":
                case "DriftVisitorB":
                case "CanopyVisitor":
                case "SentryA":
                case "SentryB":
                case "SentrySilhouette":
                case "WatcherA":
                case "WatcherB":
                case "WatcherSilhouette":
                case "PerchA":
                case "PerchB":
                case "NestSentinel":
                    return true;

                default:
                    return false;
            }
        }

        private static Material CreateOrUpdateMaterial(string path, Color baseColor)
        {
            Shader shader = Shader.Find(WorldProceduralSupportContract.UrpLitShaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader != null ? shader : Shader.Find(WorldProceduralSupportContract.StandardShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.enableInstancing = true;
            material.color = baseColor;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", baseColor);

            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 0f);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 1f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AttachHazardVentVfx(GameObject prefabAsset)
        {
            if (prefabAsset == null)
                return;

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath))
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform lod0 = FindChildByName(prefabRoot.transform, "LOD0");
                Transform lod1 = FindChildByName(prefabRoot.transform, "LOD1");
                Material particleMaterial = ResolveParticleMaterial();

                if (lod0 != null)
                {
                    ConfigureHazardVentBubbleColumn(
                        lod0,
                        "VentBubbleColumn_Main",
                        new Vector3(0f, 1.16f, 0f),
                        14f,
                        96,
                        0.11f,
                        0.18f,
                        2.8f,
                        4.2f,
                        0.45f,
                        1.2f,
                        particleMaterial);
                    ConfigureHazardVentSheen(
                        lod0,
                        "VentSheen_Main",
                        new Vector3(0f, 0.98f, 0f),
                        new Vector3(90f, 0f, 0f),
                        new Vector3(0.56f, 0.38f, 1f));

                    ConfigureHazardVentBubbleColumn(
                        lod0,
                        "VentBubbleColumn_Secondary",
                        new Vector3(0.24f, 1.02f, -0.18f),
                        7f,
                        48,
                        0.07f,
                        0.13f,
                        2.2f,
                        3.4f,
                        0.35f,
                        0.92f,
                        particleMaterial);
                    ConfigureHazardVentSheen(
                        lod0,
                        "VentSheen_Secondary",
                        new Vector3(0.24f, 0.88f, -0.18f),
                        new Vector3(90f, 0f, 0f),
                        new Vector3(0.34f, 0.24f, 1f));
                }

                if (lod1 != null)
                {
                    ConfigureHazardVentBubbleColumn(
                        lod1,
                        "VentBubbleColumn_LOD1",
                        new Vector3(0f, 0.98f, 0f),
                        6f,
                        40,
                        0.08f,
                        0.14f,
                        2.0f,
                        3.0f,
                        0.32f,
                        0.84f,
                        particleMaterial);
                    ConfigureHazardVentSheen(
                        lod1,
                        "VentSheen_LOD1",
                        new Vector3(0f, 0.84f, 0f),
                        new Vector3(90f, 0f, 0f),
                        new Vector3(0.46f, 0.3f, 1f));
                }

                SyncHazardVentLodRenderers(prefabRoot, lod0, lod1);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureHazardVentBubbleColumn(
            Transform parent,
            string childName,
            Vector3 localPosition,
            float emissionRate,
            int maxParticles,
            float sizeMin,
            float sizeMax,
            float lifetimeMin,
            float lifetimeMax,
            float speedMin,
            float speedMax,
            Material particleMaterial)
        {
            Transform child = FindChildByName(parent, childName);
            if (child == null)
            {
                GameObject childObject = new GameObject(childName);
                childObject.transform.SetParent(parent, false);
                child = childObject.transform;
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            if (!child.TryGetComponent(out ParticleSystem particleSystem))
                particleSystem = child.gameObject.AddComponent<ParticleSystem>();

            if (!child.TryGetComponent(out ParticleSystemRenderer renderer))
                renderer = child.gameObject.AddComponent<ParticleSystemRenderer>();

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 6f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.82f, 0.94f, 1f, 0.28f));
            main.gravityModifier = 0f;
            main.maxParticles = maxParticles;
            main.cullingMode = ParticleSystemCullingMode.Pause;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(emissionRate);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.ConeVolume;
            shape.angle = 7f;
            shape.radius = 0.12f;
            shape.length = 0.36f;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            shape.scale = Vector3.one;

            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.68f),
                    new Keyframe(0.6f, 1f),
                    new Keyframe(1f, 1.26f)));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.78f, 0.92f, 1f), 0f),
                    new GradientColorKey(new Color(0.92f, 0.98f, 1f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.24f, 0.12f),
                    new GradientAlphaKey(0.34f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = false;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = 1f;
            renderer.velocityScale = 0f;
            renderer.cameraVelocityScale = 0f;
            renderer.normalDirection = 1f;
            renderer.sortingFudge = 2f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sharedMaterial = particleMaterial;

            EditorUtility.SetDirty(child.gameObject);
        }

        private static void ConfigureHazardVentSheen(
            Transform parent,
            string childName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            Material ventSheenMaterial = AssetDatabase.LoadAssetAtPath<Material>(VentSheenMaterialPath);
            if (parent == null || ventSheenMaterial == null)
                return;

            Transform child = FindChildByName(parent, childName);
            if (child == null)
            {
                GameObject sheenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sheenObject.name = childName;
                sheenObject.transform.SetParent(parent, false);
                child = sheenObject.transform;
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.Euler(localEulerAngles);
            child.localScale = localScale;

            if (child.TryGetComponent(out Collider collider))
                Object.DestroyImmediate(collider);

            if (child.TryGetComponent(out MeshRenderer sheenRenderer))
            {
                sheenRenderer.sharedMaterial = ventSheenMaterial;
                sheenRenderer.shadowCastingMode = ShadowCastingMode.Off;
                sheenRenderer.receiveShadows = false;
                sheenRenderer.lightProbeUsage = LightProbeUsage.Off;
                sheenRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                sheenRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            EditorUtility.SetDirty(child.gameObject);
        }

        private static void AttachRuinApexIndustrialDecals(GameObject prefabAsset)
        {
            if (prefabAsset == null)
                return;

            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath))
                return;

            GameObject stripePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialStripeDecalPrefabPath);
            GameObject scuffPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IndustrialScuffDecalPrefabPath);
            if (stripePrefab == null || scuffPrefab == null)
                return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform lod0 = FindChildByName(prefabRoot.transform, "LOD0");
                if (lod0 == null)
                    return;

                AttachSupportDecal(
                    lod0,
                    stripePrefab,
                    RuinApexStripeFrameChildName,
                    new Vector3(-3.18f, 5.78f, 3.54f),
                    new Vector3(6f, -102f, 4f),
                    new Vector3(0.92f, 1.46f, 1f));
                AttachSupportDecal(
                    lod0,
                    stripePrefab,
                    RuinApexStripeCrossSpanChildName,
                    new Vector3(0.42f, 7.92f, 1.16f),
                    new Vector3(0f, 18f, 0f),
                    new Vector3(1.42f, 1.78f, 1f));
                AttachSupportDecal(
                    lod0,
                    scuffPrefab,
                    RuinApexScuffBaseChildName,
                    new Vector3(0.58f, 0.42f, 1.64f),
                    new Vector3(84f, 24f, 0f),
                    new Vector3(1.34f, 1.12f, 1f));

                SyncRuinApexIndustrialDecalRenderers(prefabRoot, lod0);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void AttachSupportDecal(
            Transform parent,
            GameObject decalPrefab,
            string childName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (parent == null || decalPrefab == null || string.IsNullOrEmpty(childName))
                return;

            Transform child = FindChildByName(parent, childName);
            if (child == null)
            {
                Object instance = PrefabUtility.InstantiatePrefab(decalPrefab, parent);
                if (instance is not GameObject childObject)
                    return;

                childObject.name = childName;
                child = childObject.transform;
            }

            child.localPosition = localPosition;
            child.localRotation = Quaternion.Euler(localEulerAngles);
            child.localScale = localScale;

            if (child.TryGetComponent(out Renderer renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(child.gameObject);
        }

        private static void SyncRuinApexIndustrialDecalRenderers(GameObject prefabRoot, Transform lod0)
        {
            if (prefabRoot == null || lod0 == null)
                return;

            if (!prefabRoot.TryGetComponent(out LODGroup lodGroup))
                return;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length < 1)
                return;

            lods[0].renderers = AppendRenderers(
                lods[0].renderers,
                ResolveRenderer(lod0, RuinApexStripeFrameChildName),
                ResolveRenderer(lod0, RuinApexStripeCrossSpanChildName),
                ResolveRenderer(lod0, RuinApexScuffBaseChildName));

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);
        }

        private static void SyncHazardVentLodRenderers(GameObject prefabRoot, Transform lod0, Transform lod1)
        {
            if (prefabRoot == null)
                return;

            if (!prefabRoot.TryGetComponent(out LODGroup lodGroup))
                return;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length < 2)
                return;

            lods[0].renderers = AppendRenderers(
                lods[0].renderers,
                ResolveRenderer(lod0, "VentBubbleColumn_Main"),
                ResolveRenderer(lod0, "VentBubbleColumn_Secondary"),
                ResolveRenderer(lod0, "VentSheen_Main"),
                ResolveRenderer(lod0, "VentSheen_Secondary"));

            lods[1].renderers = AppendRenderers(
                lods[1].renderers,
                ResolveRenderer(lod1, "VentBubbleColumn_LOD1"),
                ResolveRenderer(lod1, "VentSheen_LOD1"));

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);
        }

        private static Renderer[] AppendRenderers(Renderer[] existing, params Renderer[] additions)
        {
            int existingCount = existing != null ? existing.Length : 0;
            int additionCount = 0;
            for (int i = 0; i < additions.Length; i++)
            {
                if (additions[i] != null && !ContainsRenderer(existing, additions[i]))
                    additionCount++;
            }

            if (additionCount <= 0)
                return existing ?? System.Array.Empty<Renderer>();

            Renderer[] combined = new Renderer[existingCount + additionCount];
            int writeIndex = 0;
            for (int i = 0; i < existingCount; i++)
                combined[writeIndex++] = existing[i];

            for (int i = 0; i < additions.Length; i++)
            {
                if (additions[i] != null && !ContainsRenderer(existing, additions[i]))
                    combined[writeIndex++] = additions[i];
            }

            return combined;
        }

        private static bool ContainsRenderer(Renderer[] renderers, Renderer candidate)
        {
            if (renderers == null || candidate == null)
                return false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == candidate)
                    return true;
            }

            return false;
        }

        private static Renderer ResolveRenderer(Transform parent, string childName)
        {
            Transform child = FindChildByName(parent, childName);
            return child != null ? child.GetComponent<Renderer>() : null;
        }

        private static Transform FindChildByName(Transform parent, string childName)
        {
            if (parent == null || string.IsNullOrEmpty(childName))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static Material ResolveParticleMaterial()
        {
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (material != null)
                return material;

            return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int separatorIndex = path.LastIndexOf('/');
            string parentFolder = separatorIndex > 0 ? path.Substring(0, separatorIndex) : string.Empty;
            string newFolderName = separatorIndex > 0 ? path.Substring(separatorIndex + 1) : path;
            if (!string.IsNullOrEmpty(parentFolder) && !AssetDatabase.IsValidFolder(parentFolder))
                EnsureFolder(parentFolder);

            AssetDatabase.CreateFolder(parentFolder, newFolderName);
        }

        private readonly struct CompositeLodSpec
        {
            public CompositeLodSpec(float screenRelativeTransitionHeight, VisualPrimitiveSpec[] visuals)
            {
                ScreenRelativeTransitionHeight = screenRelativeTransitionHeight;
                Visuals = visuals;
            }

            public float ScreenRelativeTransitionHeight { get; }
            public VisualPrimitiveSpec[] Visuals { get; }
        }

        private readonly struct VisualPrimitiveSpec
        {
            public VisualPrimitiveSpec(
                string name,
                PrimitiveType primitiveType,
                Vector3 scale,
                Vector3 localPosition,
                Vector3 localEulerAngles,
                Material material)
            {
                Name = name;
                PrimitiveType = primitiveType;
                Scale = scale;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                Material = material;
            }

            public string Name { get; }
            public PrimitiveType PrimitiveType { get; }
            public Vector3 Scale { get; }
            public Vector3 LocalPosition { get; }
            public Vector3 LocalEulerAngles { get; }
            public Material Material { get; }
        }
    }
}
