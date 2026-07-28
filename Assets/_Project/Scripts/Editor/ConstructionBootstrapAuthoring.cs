using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Scavenging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Hecton8.Items;

namespace Hecton8.Editor
{
    public static class ConstructionBootstrapAuthoring
    {
        private static readonly List<Renderer> s_RendererCache = new List<Renderer>(32);
        private static GameObject s_StagingRoot;
        private static GameObject s_TrialRangeRoot;

        private const string DataFolder = "Assets/_Project/Data/Construction";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Construction";
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/Construction/Final";
        private const string TitaniumPrefabPath = "Assets/_Project/Prefabs/Item_Titanium.prefab";
        private const string DustParticlesPrefabPath = "Assets/_Project/Prefabs/VFX/PFB_MarineSnowLeakParticles.prefab";
        private const string RuinSeepSheenMaterialPath = "Assets/_Project/Art/Materials/Construction/Mat_RuinSeepSheen.mat";
        private const string SupportCreaturePassiveMaterialPath = "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePassive.mat";
        private const string SupportCreaturePredatorMaterialPath = "Assets/_Project/Art/Materials/WorldSupport/Mat_Support_CreaturePredator.mat";
        private const string LeakVfxChildName = "LeakVfx";
        private const string LeakWetSheenChildName = "LeakWetSheen";
        private const string LeakStripeDecalChildName = "LeakStripeDecal";
        private const string LeakScuffDecalChildName = "LeakScuffDecal";
        private const string RuinSeepSheenMainChildName = "RuinSeepSheen_Main";
        private const string RuinSeepSheenCoreChildName = "RuinSeepSheen_Core";
        private const string RuinSeepSheenBridgeChildName = "RuinSeepSheen_Bridge";
        private const string RuinIndustrialStripeMainChildName = "RuinIndustrialStripe_Main";
        private const string RuinIndustrialStripeCoreChildName = "RuinIndustrialStripe_Core";
        private const string RuinIndustrialStripeBridgeChildName = "RuinIndustrialStripe_Bridge";
        private const string RuinClusterMediumPrefabName = "PFB_Ruin_ClusterMedium.prefab";
        private const string RuinMegastructurePrefabName = "PFB_Ruin_Megastructure.prefab";

        [MenuItem("Hecton8/Authoring/Rebuild Starter Construction Kit", priority = 215)]
        public static void RebuildStarterConstructionKit()
        {
            if (!WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring(nameof(ConstructionBootstrapAuthoring), FinalPrefabFolder))
                return;

            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Construction");
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/Construction");
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Construction");
            EnsureFolder("Assets/_Project/Prefabs/Construction/Final");
            if (WorldSupportGeneratedDecalMaterialBuilder.AreSourceTexturesAvailable())
                WorldSupportGeneratedDecalMaterialBuilder.Build();

            Material foundationMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_Module_Foundation.mat",
                new Color(0.18f, 0.34f, 0.42f, 1.00f),
                false);
            Material corridorMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_Module_Corridor.mat",
                new Color(0.22f, 0.52f, 0.58f, 1.00f),
                false);
            Material pylonMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_Module_Pylon.mat",
                new Color(0.58f, 0.42f, 0.18f, 1.00f),
                false);
            Material pumpMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_Module_ServicePump.mat",
                new Color(0.18f, 0.56f, 0.66f, 1.00f),
                false);
            Material turbineMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_Module_CurrentTurbine.mat",
                new Color(0.22f, 0.68f, 0.34f, 1.00f),
                false);
            Material passiveCreatureMat = AssetDatabase.LoadAssetAtPath<Material>(SupportCreaturePassiveMaterialPath) ?? pylonMat;
            Material predatorCreatureMat = AssetDatabase.LoadAssetAtPath<Material>(SupportCreaturePredatorMaterialPath) ?? corridorMat;

            GameObject foundationFinal = CreateFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Module_Foundation.prefab",
                PrimitiveType.Cube,
                new Vector3(4f, 0.35f, 4f),
                foundationMat,
                true,
                new Vector3(3.4f, 1.8f, 3.4f),
                new[]
                {
                    new SocketSpec("Socket_PosX", new Vector3(2.05f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                    new SocketSpec("Socket_NegX", new Vector3(-2.05f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f)),
                    new SocketSpec("Socket_PosZ", new Vector3(0f, 0f, 2.05f), Quaternion.identity),
                    new SocketSpec("Socket_NegZ", new Vector3(0f, 0f, -2.05f), Quaternion.Euler(0f, 180f, 0f)),
                });
            GameObject corridorFinal = CreateFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Module_Corridor.prefab",
                PrimitiveType.Cube,
                new Vector3(2.2f, 2.2f, 6.2f),
                corridorMat,
                true,
                new Vector3(1.6f, 1.8f, 5.2f),
                new[]
                {
                    new SocketSpec("Socket_Front", new Vector3(0f, 0f, 3.15f), Quaternion.identity),
                    new SocketSpec("Socket_Back", new Vector3(0f, 0f, -3.15f), Quaternion.Euler(0f, 180f, 0f)),
                });
            GameObject pylonFinal = CreateFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Module_Pylon.prefab",
                PrimitiveType.Cylinder,
                new Vector3(0.9f, 2.4f, 0.9f),
                pylonMat,
                false,
                Vector3.zero,
                null);
            GameObject pumpFinal = CreateFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Module_ServicePump.prefab",
                PrimitiveType.Cube,
                new Vector3(1.9f, 1.7f, 1.9f),
                pumpMat,
                false,
                Vector3.zero,
                null);
            GameObject turbineFinal = CreateFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Module_CurrentTurbine.prefab",
                PrimitiveType.Cylinder,
                new Vector3(1.2f, 3.0f, 1.2f),
                turbineMat,
                false,
                Vector3.zero,
                null);
            CreateCompositeFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Debris_ScrapCluster.prefab",
                new Vector3(2.6f, 1.2f, 1.8f),
                new[]
                {
                    new CompositeLodSpec(
                        0.55f,
                        new[]
                        {
                            new VisualPrimitiveSpec("ScrapBeam", PrimitiveType.Cube, new Vector3(1.6f, 0.18f, 0.35f), new Vector3(-0.2f, 0.18f, 0.1f), new Vector3(0f, 18f, -12f), pylonMat),
                            new VisualPrimitiveSpec("CargoFrame", PrimitiveType.Cube, new Vector3(0.9f, 0.55f, 0.75f), new Vector3(0.55f, 0.32f, -0.4f), new Vector3(8f, -24f, 4f), pumpMat),
                            new VisualPrimitiveSpec("BrokenPipe", PrimitiveType.Cylinder, new Vector3(0.28f, 0.75f, 0.28f), new Vector3(-0.65f, 0.28f, -0.35f), new Vector3(90f, 0f, 28f), corridorMat),
                            new VisualPrimitiveSpec("Plate", PrimitiveType.Cube, new Vector3(0.95f, 0.08f, 0.6f), new Vector3(0.15f, 0.08f, 0.45f), new Vector3(0f, -32f, 7f), foundationMat),
                            new VisualPrimitiveSpec("ScavengerA", PrimitiveType.Capsule, new Vector3(0.18f, 0.72f, 0.18f), new Vector3(-0.34f, 0.42f, 0.46f), new Vector3(10f, 0f, 18f), passiveCreatureMat),
                            new VisualPrimitiveSpec("ScavengerB", PrimitiveType.Capsule, new Vector3(0.16f, 0.62f, 0.16f), new Vector3(0.46f, 0.36f, -0.38f), new Vector3(-8f, 0f, -14f), passiveCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.12f,
                        new[]
                        {
                            new VisualPrimitiveSpec("ScrapMass", PrimitiveType.Cube, new Vector3(1.52f, 0.42f, 0.82f), new Vector3(0f, 0.22f, 0.04f), new Vector3(0f, 12f, -6f), pylonMat),
                            new VisualPrimitiveSpec("PipeStub", PrimitiveType.Cylinder, new Vector3(0.24f, 0.64f, 0.24f), new Vector3(-0.28f, 0.26f, -0.18f), new Vector3(90f, 0f, 18f), corridorMat),
                            new VisualPrimitiveSpec("ScavengerSilhouette", PrimitiveType.Capsule, new Vector3(0.32f, 0.74f, 0.32f), new Vector3(0.12f, 0.34f, 0.22f), Vector3.zero, passiveCreatureMat),
                        }),
                });
            CreateCompositeFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Debris_WreckField.prefab",
                new Vector3(4.8f, 2.4f, 3.4f),
                new[]
                {
                    new CompositeLodSpec(
                        0.55f,
                        new[]
                        {
                            new VisualPrimitiveSpec("FieldSpine", PrimitiveType.Cube, new Vector3(3.8f, 0.24f, 0.6f), new Vector3(0f, 0.22f, 0f), new Vector3(0f, 12f, -4f), foundationMat),
                            new VisualPrimitiveSpec("HullChunk", PrimitiveType.Cube, new Vector3(1.6f, 1.1f, 1.2f), new Vector3(-1.2f, 0.55f, 0.45f), new Vector3(0f, 20f, 9f), corridorMat),
                            new VisualPrimitiveSpec("CargoShell", PrimitiveType.Cube, new Vector3(1.2f, 0.85f, 1.1f), new Vector3(1.3f, 0.42f, -0.65f), new Vector3(10f, -25f, -6f), pumpMat),
                            new VisualPrimitiveSpec("PipeRun", PrimitiveType.Cylinder, new Vector3(0.32f, 1.4f, 0.32f), new Vector3(0.7f, 0.35f, 0.9f), new Vector3(90f, 0f, -16f), pylonMat),
                            new VisualPrimitiveSpec("MastStub", PrimitiveType.Cylinder, new Vector3(0.28f, 1.8f, 0.28f), new Vector3(-0.35f, 0.85f, -0.95f), new Vector3(6f, 0f, 12f), pylonMat),
                            new VisualPrimitiveSpec("ServicePlate", PrimitiveType.Cube, new Vector3(2.2f, 0.1f, 0.9f), new Vector3(0.2f, 0.12f, -1.15f), new Vector3(0f, -18f, 0f), foundationMat),
                            new VisualPrimitiveSpec("DriftScavengerA", PrimitiveType.Capsule, new Vector3(0.24f, 0.92f, 0.24f), new Vector3(-0.62f, 0.88f, 1.06f), new Vector3(8f, 0f, 16f), passiveCreatureMat),
                            new VisualPrimitiveSpec("DriftScavengerB", PrimitiveType.Capsule, new Vector3(0.22f, 0.82f, 0.22f), new Vector3(1.54f, 0.72f, -0.86f), new Vector3(-8f, 0f, -14f), passiveCreatureMat),
                            new VisualPrimitiveSpec("HunterPerchDebris", PrimitiveType.Cylinder, new Vector3(0.12f, 1.0f, 0.12f), new Vector3(-0.12f, 1.08f, -1.18f), new Vector3(0f, 0f, 18f), predatorCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.12f,
                        new[]
                        {
                            new VisualPrimitiveSpec("FieldMass", PrimitiveType.Cube, new Vector3(3.6f, 0.44f, 1.2f), new Vector3(0f, 0.22f, -0.12f), new Vector3(0f, 10f, -4f), foundationMat),
                            new VisualPrimitiveSpec("HullMass", PrimitiveType.Cube, new Vector3(2.0f, 1.14f, 1.48f), new Vector3(-0.24f, 0.58f, 0.18f), new Vector3(0f, 14f, 4f), corridorMat),
                            new VisualPrimitiveSpec("DriftSilhouetteDebris", PrimitiveType.Capsule, new Vector3(0.44f, 1.0f, 0.44f), new Vector3(0.34f, 0.54f, 0.32f), Vector3.zero, passiveCreatureMat),
                        }),
                });
            CreateCompositeFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Ruin_ClusterMedium.prefab",
                new Vector3(8.5f, 3.2f, 7f),
                new[]
                {
                    new CompositeLodSpec(
                        0.6f,
                        new[]
                        {
                            new VisualPrimitiveSpec("ModuleA", PrimitiveType.Cube, new Vector3(4.2f, 2.0f, 4.2f), new Vector3(-2.8f, 1.0f, 0.3f), new Vector3(0f, -12f, 0f), foundationMat),
                            new VisualPrimitiveSpec("ModuleB", PrimitiveType.Cube, new Vector3(2.4f, 2.2f, 6.2f), new Vector3(1.8f, 1.1f, -1.2f), new Vector3(0f, 20f, 0f), corridorMat),
                            new VisualPrimitiveSpec("Bridge", PrimitiveType.Cube, new Vector3(1.2f, 0.6f, 2.2f), new Vector3(-0.2f, 1.4f, -0.55f), new Vector3(0f, 8f, 10f), corridorMat),
                            new VisualPrimitiveSpec("Brace", PrimitiveType.Cylinder, new Vector3(0.45f, 2.6f, 0.45f), new Vector3(3.6f, 1.3f, 1.7f), new Vector3(0f, 0f, 8f), pylonMat),
                            new VisualPrimitiveSpec("BaseSlab", PrimitiveType.Cube, new Vector3(7.2f, 0.35f, 5.8f), new Vector3(0f, 0.17f, 0f), new Vector3(0f, 6f, 0f), foundationMat),
                            new VisualPrimitiveSpec("MicroSchoolA", PrimitiveType.Capsule, new Vector3(0.26f, 1.10f, 0.26f), new Vector3(-0.95f, 1.95f, 1.25f), new Vector3(12f, 0f, 22f), passiveCreatureMat),
                            new VisualPrimitiveSpec("MicroSchoolB", PrimitiveType.Capsule, new Vector3(0.22f, 0.92f, 0.22f), new Vector3(2.65f, 1.72f, 0.75f), new Vector3(-8f, 0f, -18f), passiveCreatureMat),
                            new VisualPrimitiveSpec("HunterPerch", PrimitiveType.Cylinder, new Vector3(0.12f, 1.10f, 0.12f), new Vector3(0.85f, 1.86f, -2.15f), new Vector3(0f, 0f, 24f), predatorCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.15f,
                        new[]
                        {
                            new VisualPrimitiveSpec("MassA", PrimitiveType.Cube, new Vector3(5.6f, 1.8f, 4.6f), new Vector3(-1.6f, 0.95f, 0.15f), new Vector3(0f, -6f, 0f), foundationMat),
                            new VisualPrimitiveSpec("MassB", PrimitiveType.Cube, new Vector3(2.0f, 2.0f, 4.4f), new Vector3(2.25f, 1.0f, -0.9f), new Vector3(0f, 16f, 0f), corridorMat),
                            new VisualPrimitiveSpec("Brace", PrimitiveType.Cylinder, new Vector3(0.38f, 2.0f, 0.38f), new Vector3(3.2f, 1.0f, 1.3f), new Vector3(0f, 0f, 8f), pylonMat),
                            new VisualPrimitiveSpec("BaseSlab", PrimitiveType.Cube, new Vector3(6.6f, 0.3f, 5.0f), new Vector3(0f, 0.15f, 0f), new Vector3(0f, 4f, 0f), foundationMat),
                            new VisualPrimitiveSpec("SchoolSilhouette", PrimitiveType.Capsule, new Vector3(0.62f, 1.24f, 0.62f), new Vector3(1.10f, 1.35f, -0.25f), new Vector3(0f, 0f, 14f), passiveCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.04f,
                        new[]
                        {
                            new VisualPrimitiveSpec("Mass", PrimitiveType.Cube, new Vector3(6.6f, 2.2f, 4.8f), new Vector3(-0.3f, 1.1f, -0.2f), new Vector3(0f, 8f, 0f), foundationMat),
                            new VisualPrimitiveSpec("Spur", PrimitiveType.Cube, new Vector3(1.4f, 1.8f, 3.8f), new Vector3(2.7f, 0.9f, 0.8f), new Vector3(0f, 18f, 0f), corridorMat),
                        }),
                });
            CreateCompositeFinalPrefab(
                $"{FinalPrefabFolder}/PFB_Ruin_Megastructure.prefab",
                new Vector3(10.8f, 8.8f, 11f),
                new[]
                {
                    new CompositeLodSpec(
                        0.6f,
                        new[]
                        {
                            new VisualPrimitiveSpec("TowerCore", PrimitiveType.Cube, new Vector3(4.2f, 8.4f, 4.2f), new Vector3(0f, 4.2f, 0f), Vector3.zero, foundationMat),
                            new VisualPrimitiveSpec("UpperRing", PrimitiveType.Cube, new Vector3(7.4f, 0.55f, 7.4f), new Vector3(0f, 6.6f, 0f), Vector3.zero, corridorMat),
                            new VisualPrimitiveSpec("LowerRing", PrimitiveType.Cube, new Vector3(8.2f, 0.7f, 8.2f), new Vector3(0f, 2.1f, 0f), Vector3.zero, corridorMat),
                            new VisualPrimitiveSpec("SideFrameA", PrimitiveType.Cube, new Vector3(1.1f, 6.8f, 1.1f), new Vector3(-3.4f, 3.6f, 2.9f), new Vector3(0f, 12f, 4f), pylonMat),
                            new VisualPrimitiveSpec("SideFrameB", PrimitiveType.Cube, new Vector3(1.1f, 5.8f, 1.1f), new Vector3(3.1f, 3.0f, -2.7f), new Vector3(0f, -18f, -6f), pylonMat),
                            new VisualPrimitiveSpec("Bridge", PrimitiveType.Cube, new Vector3(2.4f, 1.0f, 6.8f), new Vector3(0f, 1.2f, 4.6f), new Vector3(0f, 18f, 0f), foundationMat),
                            new VisualPrimitiveSpec("BasePlate", PrimitiveType.Cube, new Vector3(10.2f, 0.45f, 10.2f), new Vector3(0f, 0.22f, 0f), Vector3.zero, foundationMat),
                            new VisualPrimitiveSpec("DriftSchoolA", PrimitiveType.Capsule, new Vector3(0.38f, 1.80f, 0.38f), new Vector3(-4.15f, 4.55f, 1.85f), new Vector3(6f, 0f, 18f), passiveCreatureMat),
                            new VisualPrimitiveSpec("DriftSchoolB", PrimitiveType.Capsule, new Vector3(0.32f, 1.50f, 0.32f), new Vector3(4.05f, 3.85f, -1.95f), new Vector3(-10f, 0f, -16f), passiveCreatureMat),
                            new VisualPrimitiveSpec("PerchSchool", PrimitiveType.Capsule, new Vector3(0.26f, 1.20f, 0.26f), new Vector3(1.45f, 6.95f, 2.95f), new Vector3(10f, 0f, -8f), passiveCreatureMat),
                            new VisualPrimitiveSpec("HunterMarker", PrimitiveType.Cylinder, new Vector3(0.14f, 1.60f, 0.14f), new Vector3(0.75f, 2.20f, 5.55f), new Vector3(0f, 0f, -20f), predatorCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.15f,
                        new[]
                        {
                            new VisualPrimitiveSpec("Core", PrimitiveType.Cube, new Vector3(4.8f, 7.2f, 4.8f), new Vector3(0f, 3.8f, 0f), Vector3.zero, foundationMat),
                            new VisualPrimitiveSpec("Ring", PrimitiveType.Cube, new Vector3(7.6f, 0.6f, 7.6f), new Vector3(0f, 4.8f, 0f), Vector3.zero, corridorMat),
                            new VisualPrimitiveSpec("StrutA", PrimitiveType.Cube, new Vector3(1.0f, 5.8f, 1.0f), new Vector3(-3.0f, 3.0f, 2.5f), new Vector3(0f, 8f, 4f), pylonMat),
                            new VisualPrimitiveSpec("StrutB", PrimitiveType.Cube, new Vector3(1.0f, 5.0f, 1.0f), new Vector3(2.8f, 2.6f, -2.3f), new Vector3(0f, -12f, -4f), pylonMat),
                            new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(9.4f, 0.35f, 9.4f), new Vector3(0f, 0.18f, 0f), Vector3.zero, foundationMat),
                            new VisualPrimitiveSpec("DriftSilhouette", PrimitiveType.Capsule, new Vector3(0.95f, 2.10f, 0.95f), new Vector3(0.90f, 4.25f, 1.20f), Vector3.zero, passiveCreatureMat),
                            new VisualPrimitiveSpec("HunterSilhouette", PrimitiveType.Cylinder, new Vector3(0.18f, 1.50f, 0.18f), new Vector3(-1.60f, 2.20f, 3.25f), new Vector3(0f, 0f, 16f), predatorCreatureMat),
                        }),
                    new CompositeLodSpec(
                        0.04f,
                        new[]
                        {
                            new VisualPrimitiveSpec("Mass", PrimitiveType.Cube, new Vector3(6.2f, 7.8f, 6.2f), new Vector3(0f, 4.0f, 0f), Vector3.zero, foundationMat),
                            new VisualPrimitiveSpec("Spur", PrimitiveType.Cube, new Vector3(2.4f, 2.2f, 6.0f), new Vector3(0.6f, 1.2f, 3.6f), new Vector3(0f, 16f, 0f), corridorMat),
                            new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(8.6f, 0.3f, 8.6f), new Vector3(0f, 0.15f, 0f), Vector3.zero, foundationMat),
                        }),
                });

            BuildableData foundation = CreateOrUpdateBuildable(
                $"{DataFolder}/Build_Foundation_Platform.asset",
                "Foundation Platform",
                "Primary structural plate for early habitat expansion.",
                foundationFinal,
                BuildableFamily.Structure,
                0f,
                25);
            BuildableData corridor = CreateOrUpdateBuildable(
                $"{DataFolder}/Build_Corridor_Straight.asset",
                "Straight Corridor",
                "Pressurized connector for linking starter modules.",
                corridorFinal,
                BuildableFamily.Habitat,
                -6f,
                35);
            BuildableData pylon = CreateOrUpdateBuildable(
                $"{DataFolder}/Build_Utility_Pylon.asset",
                "Utility Pylon",
                "External support and routing node for later power/data chains.",
                pylonFinal,
                BuildableFamily.Utility,
                0f,
                40);
            BuildableData pump = CreateOrUpdateBuildable(
                $"{DataFolder}/Build_Service_Pump.asset",
                "Service Pump",
                "Flood-control utility module for keeping starter corridors and work bays serviceable.",
                pumpFinal,
                BuildableFamily.Utility,
                -8f,
                20);
            BuildableData turbine = CreateOrUpdateBuildable(
                $"{DataFolder}/Build_Current_Turbine.asset",
                "Current Turbine",
                "Low-profile current generator for early power support on exposed routes.",
                turbineFinal,
                BuildableFamily.Utility,
                18f,
                15);

            AssignStarterBuildCosts(foundation, corridor, pylon, pump, turbine);

            ModuleCatalog catalog = CreateOrUpdateModuleCatalog(
                $"{DataFolder}/ModuleCatalog_Starter.asset",
                foundation,
                corridor,
                pylon,
                pump,
                turbine);

            AssignCatalogToScene(catalog, foundation);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
                EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[ConstructionBootstrapAuthoring] Starter construction kit rebuilt.");
        }

        [MenuItem("Hecton8/Authoring/Rebuild Tool Trial Range", priority = 216)]
        public static void RebuildToolTrialRange()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                Debug.LogError("[ConstructionBootstrapAuthoring] Cannot rebuild tool trial range without a loaded scene.");
                return;
            }

            if (s_StagingRoot == null)
            {
                s_StagingRoot = GameObject.Find("Tool_Staging");
            }

            GameObject stagingRoot = s_StagingRoot;
            if (stagingRoot == null)
            {
                stagingRoot = new GameObject("Tool_Staging");
                SceneManager.MoveGameObjectToScene(stagingRoot, activeScene);
                s_StagingRoot = stagingRoot;
            }

            Transform rangeRoot = null;
            if (s_TrialRangeRoot != null)
            {
                rangeRoot = s_TrialRangeRoot.transform;
            }
            else
            {
                rangeRoot = FindChild(stagingRoot.transform, "Tool_TrialRange");
            }

            if (rangeRoot == null)
            {
                GameObject rangeRootObject = new GameObject("Tool_TrialRange");
                rangeRoot = rangeRootObject.transform;
                rangeRoot.SetParent(stagingRoot.transform, false);
            }
            s_TrialRangeRoot = rangeRoot.gameObject;

            ClearChildren(rangeRoot);
            rangeRoot.localPosition = new Vector3(10f, 0f, 18f);
            rangeRoot.localRotation = Quaternion.identity;
            rangeRoot.localScale = Vector3.one;

            Material trialCargoMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Cargo.mat",
                new Color(0.23f, 0.54f, 0.62f, 1f),
                false);
            Material trialHeavyMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Heavy.mat",
                new Color(0.62f, 0.36f, 0.2f, 1f),
                false);
            Material trialAnchorMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Anchor.mat",
                new Color(0.24f, 0.82f, 0.9f, 1f),
                false);
            Material trialDarkMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Dark.mat",
                new Color(0.06f, 0.08f, 0.12f, 1f),
                false);
            Material trialScanMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Scan.mat",
                new Color(0.32f, 0.78f, 0.44f, 1f),
                false);
            Material trialCombatMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Combat.mat",
                new Color(0.72f, 0.2f, 0.22f, 1f),
                false);
            Material trialDormantMat = CreateOrUpdateMaterial(
                $"{MaterialFolder}/Mat_ToolTrial_Dormant.mat",
                new Color(0.34f, 0.42f, 0.72f, 1f),
                false);

            GameObject cargoLane = CreateSceneRoot("Lane_Cargo", rangeRoot.transform, new Vector3(0f, 0f, 0f));
            CreateTrialCube(cargoLane.transform, "Cargo_Light", new Vector3(0f, 0.7f, 0f), new Vector3(0.8f, 0.8f, 0.8f), 8f, trialCargoMat, FieldTargetRole.CargoLight, "Utility canister for precision pulls and hazard bypass.");
            CreateTrialCube(cargoLane.transform, "Cargo_Work", new Vector3(2.4f, 0.9f, 0f), new Vector3(1.0f, 1.0f, 1.0f), 35f, trialCargoMat, FieldTargetRole.CargoWork, "Standard field crate inside the normal handling band.");
            CreateTrialCube(cargoLane.transform, "Cargo_Heavy", new Vector3(5.2f, 1.0f, 0f), new Vector3(1.2f, 1.2f, 1.2f), 120f, trialHeavyMat, FieldTargetRole.CargoHeavy, "Heavy salvage block that still reacts to controlled propulsion handling.");
            CreateTrialCube(cargoLane.transform, "Cargo_Overweight", new Vector3(8.4f, 1.1f, 0f), new Vector3(1.4f, 1.4f, 1.4f), 520f, trialHeavyMat, FieldTargetRole.CargoOverweight, "Overweight anchor load that should force rerouting instead of direct handling.");

            GameObject salvageLane = CreateSceneRoot("Lane_Salvage", rangeRoot.transform, new Vector3(0f, 0f, 8f));
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, salvageLane.transform, "Trial_Salvage_A", new Vector3(0f, 0.4f, 0f), Quaternion.identity), FieldTargetRole.SalvagePickup, "Loose salvage for short-range recovery drills.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, salvageLane.transform, "Trial_Salvage_B", new Vector3(1.8f, 0.4f, 0.6f), Quaternion.identity), FieldTargetRole.SalvagePickup, "Offset salvage pickup for sampler reach tests.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, salvageLane.transform, "Trial_Salvage_C", new Vector3(3.6f, 0.4f, -0.4f), Quaternion.identity), FieldTargetRole.SalvagePickup, "Loose salvage near the node cluster.");
            CreateTrialResourceNode(
                salvageLane.transform,
                "Trial_Node_Active",
                new Vector3(6.2f, 0.75f, 0f),
                new Vector3(1.2f, 1.2f, 1.2f),
                trialCargoMat,
                TitaniumPrefabPath,
                false);
            CreateTrialResourceNode(
                salvageLane.transform,
                "Trial_Node_Depleted",
                new Vector3(8.8f, 0.75f, 0f),
                new Vector3(1.2f, 1.2f, 1.2f),
                trialHeavyMat,
                TitaniumPrefabPath,
                true);

            GameObject serviceLane = CreateSceneRoot("Lane_ServiceModules", rangeRoot.transform, new Vector3(0f, 0f, 18f));
            GameObject foundation = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Foundation.prefab", serviceLane.transform, "Trial_Module_Foundation_Damaged", new Vector3(0f, 0f, 0f), Quaternion.identity);
            GameObject corridor = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Corridor.prefab", serviceLane.transform, "Trial_Module_Corridor_Flooded", new Vector3(8f, 0f, 0f), Quaternion.identity);
            GameObject controlFoundation = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Foundation.prefab", serviceLane.transform, "Trial_Module_Foundation_Control", new Vector3(15f, 0f, 0f), Quaternion.identity);

            ConfigureModuleState(foundation, 35f, false);
            ConfigureModuleState(corridor, 55f, true);
            ConfigureModuleState(controlFoundation, 100f, false);
            AttachDescriptor(foundation, FieldTargetRole.ServiceDamaged, "Damaged service module used to verify repair and cutter guidance.");
            AttachDescriptor(corridor, FieldTargetRole.ServiceFlooded, "Flooded service module used to verify repair staging and drainage guidance.");
            AttachDescriptor(controlFoundation, FieldTargetRole.ServiceControl, "Control service module used as a stable baseline for module diagnostics.");

            GameObject beaconLane = CreateSceneRoot("Lane_BeaconRoute", rangeRoot.transform, new Vector3(0f, 0f, 30f));
            CreateMarker(beaconLane.transform, "Route_Anchor", new Vector3(0f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteAnchor, "Safe return origin for the beacon network.");
            CreateMarker(beaconLane.transform, "Route_Relay", new Vector3(12f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteRelay, "Mid-lane relay point that keeps the route readable.");
            CreateMarker(beaconLane.transform, "Route_Frontier", new Vector3(28f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteFrontier, "Deep-range route endpoint for frontier progression.");

            GameObject darkLane = CreateSceneRoot("Lane_DarkRoute", rangeRoot.transform, new Vector3(0f, 0f, 42f));
            CreateTrialBlock(darkLane.transform, "DarkRoute_LeftWall_A", new Vector3(0f, 1.2f, 4f), new Vector3(0.35f, 2.4f, 8f), trialDarkMat);
            CreateTrialBlock(darkLane.transform, "DarkRoute_RightWall_A", new Vector3(4f, 1.2f, 4f), new Vector3(0.35f, 2.4f, 8f), trialDarkMat);
            CreateTrialBlock(darkLane.transform, "DarkRoute_Ceiling_A", new Vector3(2f, 2.45f, 4f), new Vector3(4.4f, 0.2f, 8f), trialDarkMat);
            CreateMarker(darkLane.transform, "DarkRoute_Entry", new Vector3(2f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteAnchor, "Entry reference for low-light route discipline.");
            CreateMarker(darkLane.transform, "DarkRoute_Mid", new Vector3(2f, 0.9f, 8f), trialAnchorMat, FieldTargetRole.RouteRelay, "Mid-route marker for beam-mode swaps.");
            CreateMarker(darkLane.transform, "DarkRoute_Far", new Vector3(2f, 0.9f, 16f), trialAnchorMat, FieldTargetRole.RouteFrontier, "Far route marker near the hazard probe.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, darkLane.transform, "DarkRoute_Salvage_Close", new Vector3(1.1f, 0.35f, 3.2f), Quaternion.identity), FieldTargetRole.SalvagePickup, "Close salvage pickup inside a dark route bend.");
            CreateScannableProbe(
                darkLane.transform,
                "DarkRoute_HazardProbe",
                new Vector3(2f, 1.2f, 15.5f),
                trialScanMat,
                "darkroute.hazard_probe",
                "HAZARD PROBE",
                "Hazard",
                "Distant narrow-space hazard marker for focused-beam flashlight checks and cautious route scans.");

            GameObject scanLane = CreateSceneRoot("Lane_ScanCorridor", rangeRoot.transform, new Vector3(0f, 0f, 58f));
            CreateScannableProbe(
                scanLane.transform,
                "Scan_Poi_ExpeditionContact",
                new Vector3(0f, 1f, 0f),
                trialScanMat,
                "scan.expedition_contact",
                "EXPEDITION CONTACT",
                "Expedition",
                "General expedition contact used to verify broad sweep guidance and sparse-contact routing.");
            CreateScannableProbe(
                scanLane.transform,
                "Scan_Poi_ResourceCache",
                new Vector3(4f, 1f, 1.2f),
                trialScanMat,
                "scan.resource_cache",
                "RESOURCE CACHE",
                "Resource",
                "Dense recoverable pocket intended for resource-mode scanner confirmation.");
            CreateScannableProbe(
                scanLane.transform,
                "Scan_Poi_StructureRelay",
                new Vector3(8f, 1f, -0.8f),
                trialScanMat,
                "scan.structure_relay",
                "STRUCTURE RELAY",
                "Structure",
                "Structural relay point intended for structure-mode scanner and analyzer checks.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, scanLane.transform, "Scan_Pickup_ResourceA", new Vector3(3.2f, 0.35f, 0.4f), Quaternion.identity), FieldTargetRole.ResourceCache, "Loose resource cache beside the main probe.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, scanLane.transform, "Scan_Pickup_ResourceB", new Vector3(4.8f, 0.35f, 1.8f), Quaternion.identity), FieldTargetRole.ResourceCache, "Secondary resource trace for scanner sweep density.");

            GameObject combatLane = CreateSceneRoot("Lane_CombatContacts", rangeRoot.transform, new Vector3(0f, 0f, 74f));
            CreateTrialCube(combatLane.transform, "Combat_Dormant", new Vector3(0f, 0.8f, 0f), new Vector3(1.0f, 0.9f, 1.0f), 28f, trialDormantMat, FieldTargetRole.BioformDormant, "Dormant bioform contact. Safe opener window before wake-up.");
            CreateTrialCube(combatLane.transform, "Combat_Aggressive", new Vector3(3.5f, 0.8f, 0f), new Vector3(1.1f, 1.0f, 1.1f), 34f, trialCombatMat, FieldTargetRole.BioformAggressive, "Aggressive bioform contact. Control first before committing to close range.");
            CreateTrialCube(combatLane.transform, "Combat_Fractured", new Vector3(7.0f, 0.8f, 0f), new Vector3(0.95f, 0.8f, 0.95f), 24f, trialCombatMat, FieldTargetRole.BioformFractured, "Fractured target near collapse. Finish window is open.");
            CreateTrialCube(combatLane.transform, "Combat_Down", new Vector3(10.2f, 0.45f, 0f), new Vector3(1.2f, 0.35f, 0.9f), 18f, trialHeavyMat, FieldTargetRole.BioformDown, "Neutralized contact. Threat is over and the lane is ready for recovery.");
            CreateMarker(combatLane.transform, "Combat_Checkpoint", new Vector3(14f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.ExpeditionCheckpoint, "Combat lane checkpoint used for post-contact route discipline.");

            GameObject constructionLane = CreateSceneRoot("Lane_ConstructionOps", rangeRoot.transform, new Vector3(0f, 0f, 82f));
            GameObject socketFoundation = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Foundation.prefab", constructionLane.transform, "Construct_SocketBase", new Vector3(0f, 0f, 0f), Quaternion.identity);
            ConfigureModuleState(socketFoundation, 100f, false);
            AttachDescriptor(socketFoundation, FieldTargetRole.ConstructionSocket, "Stable foundation socket. Good snapped build anchor for rapid deployment.");
            CreateMarker(constructionLane.transform, "Construct_ClearLane", new Vector3(4.2f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.ConstructionClear, "Clear construction lane. Free placement should be possible here.");
            CreateTrialCube(constructionLane.transform, "Construct_Blocker", new Vector3(8.8f, 0.9f, 0f), new Vector3(1.6f, 1.6f, 1.6f), 140f, trialHeavyMat, FieldTargetRole.ConstructionBlocked, "Blocked construction lane. Remove or route around the obstacle before building.");
            CreateMarker(constructionLane.transform, "Construct_SocketGuide", new Vector3(0f, 0.9f, 3.8f), trialAnchorMat, FieldTargetRole.ConstructionSocket, "Socket guide marker. Use it to align snapped placement before committing the module.");

            GameObject powerLane = CreateSceneRoot("Lane_PowerOps", rangeRoot.transform, new Vector3(0f, 0f, 94f));
            GameObject powerTurbine = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_CurrentTurbine.prefab", powerLane.transform, "Power_CurrentTurbine", new Vector3(0f, 0f, 0f), Quaternion.identity);
            AttachDescriptor(powerTurbine, FieldTargetRole.PowerGeneration, "Exposed current turbine position. Good generator anchor for early field power.");
            GameObject powerRelay = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Pylon.prefab", powerLane.transform, "Power_RelayPylon", new Vector3(5.5f, 0f, 0f), Quaternion.identity);
            AttachDescriptor(powerRelay, FieldTargetRole.PowerRelay, "Relay pylon position. Good midpoint for routing service power through the lane.");
            GameObject powerLoad = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_ServicePump.prefab", powerLane.transform, "Power_ServicePump", new Vector3(11f, 0f, 0f), Quaternion.identity);
            AttachDescriptor(powerLoad, FieldTargetRole.PowerLoad, "Service pump load. Wants stable upstream power before flood-control work starts.");
            GameObject powerFlooded = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Corridor.prefab", powerLane.transform, "Power_ServiceRoute", new Vector3(16f, 0f, 0f), Quaternion.identity);
            ConfigureModuleState(powerFlooded, 52f, true);
            AttachDescriptor(powerFlooded, FieldTargetRole.ServiceFlooded, "Flooded service route downstream from the pump and relay chain.");
            CreateMarker(powerLane.transform, "Power_ExposedGuide", new Vector3(0f, 0.9f, 4.2f), trialAnchorMat, FieldTargetRole.PowerGeneration, "Exposed route guide. Turbine support matters here before extending farther.");

            GameObject endgameLane = CreateSceneRoot("Lane_EndgameOps", rangeRoot.transform, new Vector3(0f, 0f, 110f));
            CreateMarker(endgameLane.transform, "Ops_Anchor", new Vector3(0f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteAnchor, "Endgame operation entry anchor. Start of the mixed-role field route.");
            CreateTrialCube(endgameLane.transform, "Ops_Cargo_Work", new Vector3(4.2f, 0.9f, 0f), new Vector3(1.0f, 1.0f, 1.0f), 35f, trialCargoMat, FieldTargetRole.CargoWork, "Work cargo placed early in the route for recovery-loadout advice.");
            AttachDescriptor(SpawnScenePrefab(TitaniumPrefabPath, endgameLane.transform, "Ops_Salvage", new Vector3(7.4f, 0.35f, 0.4f), Quaternion.identity), FieldTargetRole.SalvagePickup, "Loose salvage package in the mid-route recovery pocket.");
            GameObject opsService = SpawnScenePrefab($"{FinalPrefabFolder}/PFB_Module_Corridor.prefab", endgameLane.transform, "Ops_Service_Flooded", new Vector3(12f, 0f, 0f), Quaternion.identity);
            ConfigureModuleState(opsService, 55f, true);
            AttachDescriptor(opsService, FieldTargetRole.ServiceFlooded, "Flooded service contact that should flip the route toward construction/service advice.");
            CreateScannableProbe(
                endgameLane.transform,
                "Ops_Hazard",
                new Vector3(17f, 1f, -0.8f),
                trialScanMat,
                "ops.hazard",
                "OPERATION HAZARD",
                "Hazard",
                "Hazard checkpoint inside the mixed-route lane. Used for exploration/scouting advice.");
            CreateTrialCube(endgameLane.transform, "Ops_Combat_Aggressive", new Vector3(21.5f, 0.8f, 0f), new Vector3(1.1f, 1.0f, 1.1f), 34f, trialCombatMat, FieldTargetRole.BioformAggressive, "Aggressive contact near the route terminus. Should push the active recommendation toward defense.");
            CreateMarker(endgameLane.transform, "Ops_Frontier", new Vector3(27f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.RouteFrontier, "Endgame route frontier. Used to confirm that the route closes back into exploration guidance.");

            GameObject choiceLane = CreateSceneRoot("Lane_ChoiceHub", rangeRoot.transform, new Vector3(0f, 0f, 128f));
            CreateMarker(choiceLane.transform, "Choice_Hub", new Vector3(0f, 0.9f, 0f), trialAnchorMat, FieldTargetRole.ExpeditionCheckpoint, "Open decision hub. Choose the route that best matches your current goal and loadout.");
            CreateMarker(choiceLane.transform, "Choice_To_Recovery", new Vector3(-5f, 0.9f, 5f), trialAnchorMat, FieldTargetRole.ResourceCache, "Left branch favors salvage and resource recovery.");
            CreateMarker(choiceLane.transform, "Choice_To_Construction", new Vector3(0f, 0.9f, 7f), trialAnchorMat, FieldTargetRole.ConstructionSocket, "Center branch favors snapped construction and service work.");
            CreateMarker(choiceLane.transform, "Choice_To_Defense", new Vector3(5f, 0.9f, 5f), trialAnchorMat, FieldTargetRole.BioformAggressive, "Right branch favors defense and control tools.");

            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ConstructionBootstrapAuthoring] Tool trial range rebuilt.");
        }

        [MenuItem("Hecton8/Validation/Validate Tool Trial Range", priority = 217)]
        public static void ValidateToolTrialRange()
        {
            int errorCount = 0;
            int warningCount = 0;

            if (s_TrialRangeRoot == null)
            {
                s_TrialRangeRoot = GameObject.Find("Tool_TrialRange");
            }
            GameObject root = s_TrialRangeRoot;
            if (root == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Tool_TrialRange root.");
                return;
            }

            ValidateCargoLane(root.transform, ref errorCount, ref warningCount);
            ValidateSalvageLane(root.transform, ref errorCount, ref warningCount);
            ValidateServiceLane(root.transform, ref errorCount, ref warningCount);
            ValidateBeaconLane(root.transform, ref errorCount, ref warningCount);
            ValidateDarkRouteLane(root.transform, ref errorCount, ref warningCount);
            ValidateScanCorridorLane(root.transform, ref errorCount, ref warningCount);
            ValidateCombatLane(root.transform, ref errorCount, ref warningCount);
            ValidateConstructionOpsLane(root.transform, ref errorCount, ref warningCount);
            ValidatePowerOpsLane(root.transform, ref errorCount, ref warningCount);
            ValidateEndgameOpsLane(root.transform, ref errorCount, ref warningCount);
            ValidateChoiceHubLane(root.transform, ref errorCount, ref warningCount);

            if (errorCount <= 0 && warningCount <= 0)
            {
                Debug.Log("[ToolTrialRangeValidation] PASS no issues found.");
                return;
            }

            Debug.LogWarning($"[ToolTrialRangeValidation] COMPLETE errors={errorCount} warnings={warningCount}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, bool transparent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = true;
            material.doubleSidedGI = false;
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor("_BaseColor", color);
            ConfigureUrpLitSurface(material, transparent);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateSceneRoot(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void CreateTrialCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, float mass, Material material, FieldTargetRole role, string note)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            if (cube.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            Rigidbody body = cube.GetComponent<Rigidbody>();
            if (body == null)
                body = cube.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.6f;
            body.angularDamping = 0.8f;
            body.useGravity = false;

            AttachDescriptor(cube, role, note);
        }

        private static GameObject SpawnScenePrefab(string prefabPath, Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[ConstructionBootstrapAuthoring] Missing prefab for tool trial range: {prefabPath}");
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
            if (instance == null)
                return null;

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void ConfigureModuleState(GameObject instance, float integrity, bool flooded)
        {
            if (instance == null)
                return;

            BaseModule module = instance.GetComponent<BaseModule>();
            if (module == null)
                module = instance.GetComponentInChildren<BaseModule>();

            if (module == null)
                return;

            module.SetState(new BaseModuleSaveState
            {
                Integrity = integrity,
                Flooded = flooded,
                CascadeFailure = BaseModuleFailureMode.None,
                RepairIntegrityCap = module.MaxIntegrity,
                AirReserveNormalized = 1f,
                Co2Normalized = 0f,
                FloodedReefFloodSeconds = 0f,
                InteriorReefInfestationActive = false
            });
        }

        private static void CreateMarker(Transform parent, string name, Vector3 localPosition, Material material, FieldTargetRole role, string note)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = new Vector3(0.45f, 0.9f, 0.45f);
            if (marker.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;
            if (marker.TryGetComponent(out Collider collider))
                collider.enabled = false;

            AttachDescriptor(marker, role, note);
        }

        private static void CreateTrialBlock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = localScale;

            if (block.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;
        }

        private static void CreateScannableProbe(
            Transform parent,
            string name,
            Vector3 localPosition,
            Material material,
            string entryId,
            string entryTitle,
            string entryCategory,
            string entrySummary)
        {
            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = name;
            probe.transform.SetParent(parent, false);
            probe.transform.localPosition = localPosition;
            probe.transform.localRotation = Quaternion.identity;
            probe.transform.localScale = Vector3.one * 0.7f;

            if (probe.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            if (probe.TryGetComponent(out Collider collider))
                collider.isTrigger = true;

            ScannableTarget target = probe.AddComponent<ScannableTarget>();
            target.Configure(entryId, entryTitle, entryCategory, entrySummary);
            AttachDescriptor(probe, ClassifyScannableRole(entryCategory), entrySummary);
        }

        private static void CreateTrialResourceNode(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            string lootPrefabPath,
            bool depleted)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = name;
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            node.transform.localRotation = Quaternion.identity;
            node.transform.localScale = localScale;

            if (node.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            GameObject lootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(lootPrefabPath);
            SerializedObject so = new SerializedObject(resourceNode);
            so.FindProperty("lootPrefab").objectReferenceValue = lootPrefab;
            so.FindProperty("lootCount").intValue = 2;
            so.FindProperty("maxHealth").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();

            System.Type nodeType = typeof(ResourceNode);
            System.Reflection.FieldInfo currentHealthField =
                nodeType.GetField("_currentHealth", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.FieldInfo depletedField =
                nodeType.GetField("_isDepleted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            if (currentHealthField != null)
                currentHealthField.SetValue(resourceNode, depleted ? 0f : 100f);

            if (depletedField != null)
                depletedField.SetValue(resourceNode, depleted);

            AttachDescriptor(node, depleted ? FieldTargetRole.ResourceNodeDepleted : FieldTargetRole.ResourceNodeActive,
                depleted
                    ? "Spent node used to verify depleted-state tool messaging."
                    : "Active node used to verify live extraction and breach messaging.");
            EditorUtility.SetDirty(resourceNode);
        }

        private static void AttachDescriptor(GameObject target, FieldTargetRole role, string note)
        {
            if (target == null)
                return;

            FieldTargetDescriptor descriptor = target.GetComponent<FieldTargetDescriptor>();
            if (descriptor == null)
                descriptor = target.AddComponent<FieldTargetDescriptor>();

            descriptor.Configure(role, note);
            EditorUtility.SetDirty(descriptor);
        }

        private static FieldTargetRole ClassifyScannableRole(string entryCategory)
        {
            if (string.IsNullOrWhiteSpace(entryCategory))
                return FieldTargetRole.Generic;

            switch (entryCategory.Trim().ToLowerInvariant())
            {
                case "hazard":
                    return FieldTargetRole.HazardProbe;
                case "resource":
                    return FieldTargetRole.ResourceCache;
                case "structure":
                    return FieldTargetRole.StructureRelay;
                case "expedition":
                    return FieldTargetRole.ExpeditionCheckpoint;
                default:
                    return FieldTargetRole.Generic;
            }
        }

        private static Transform FindChild(Transform parent, string name)
        {
            if (parent == null)
                return null;

            return parent.Find(name);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
                return;

            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static void ValidateCargoLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_Cargo");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_Cargo.");
                errorCount++;
                return;
            }

            ValidateCargoMass(lane, "Cargo_Light", 8f, ref errorCount);
            ValidateCargoMass(lane, "Cargo_Work", 35f, ref errorCount);
            ValidateCargoMass(lane, "Cargo_Heavy", 120f, ref errorCount);
            ValidateCargoMass(lane, "Cargo_Overweight", 520f, ref errorCount);
        }

        private static void ValidateSalvageLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_Salvage");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_Salvage.");
                errorCount++;
                return;
            }

            if (!ValidateNamedObject(lane, "Trial_Salvage_A", ref errorCount) |
                !ValidateNamedObject(lane, "Trial_Salvage_B", ref errorCount) |
                !ValidateNamedObject(lane, "Trial_Salvage_C", ref errorCount) |
                !ValidateNamedObject(lane, "Trial_Node_Active", ref errorCount) |
                !ValidateNamedObject(lane, "Trial_Node_Depleted", ref errorCount))
            {
                return;
            }

            for (int i = 0; i < lane.childCount; i++)
            {
                GameObject child = lane.GetChild(i).gameObject;
                bool hasPickup = child.GetComponent("PickupItem") != null || child.GetComponent("HectonItem") != null;
                bool hasNode = child.GetComponent<ResourceNode>() != null;
                if (!hasPickup && !hasNode)
                {
                    Debug.LogWarning($"[ToolTrialRangeValidation] Salvage target missing pickup component: {child.name}", child);
                    warningCount++;
                }
            }

            ValidateDescriptor(lane, "Trial_Salvage_A", ref errorCount);
            ValidateDescriptor(lane, "Trial_Salvage_B", ref errorCount);
            ValidateDescriptor(lane, "Trial_Salvage_C", ref errorCount);
            ValidateResourceNode(lane, "Trial_Node_Active", false, ref errorCount);
            ValidateResourceNode(lane, "Trial_Node_Depleted", true, ref errorCount);
        }

        private static void ValidateServiceLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_ServiceModules");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_ServiceModules.");
                errorCount++;
                return;
            }

            ValidateModuleState(lane, "Trial_Module_Foundation_Damaged", floodedExpected: false, shouldBeDamaged: true, ref errorCount, ref warningCount);
            ValidateModuleState(lane, "Trial_Module_Corridor_Flooded", floodedExpected: true, shouldBeDamaged: true, ref errorCount, ref warningCount);
            ValidateModuleState(lane, "Trial_Module_Foundation_Control", floodedExpected: false, shouldBeDamaged: false, ref errorCount, ref warningCount);
            ValidateDescriptor(lane, "Trial_Module_Foundation_Damaged", ref errorCount);
            ValidateDescriptor(lane, "Trial_Module_Corridor_Flooded", ref errorCount);
            ValidateDescriptor(lane, "Trial_Module_Foundation_Control", ref errorCount);
        }

        private static void ValidateBeaconLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_BeaconRoute");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_BeaconRoute.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Route_Anchor", ref errorCount);
            ValidateNamedObject(lane, "Route_Relay", ref errorCount);
            ValidateNamedObject(lane, "Route_Frontier", ref errorCount);
            ValidateDescriptor(lane, "Route_Anchor", ref errorCount);
            ValidateDescriptor(lane, "Route_Relay", ref errorCount);
            ValidateDescriptor(lane, "Route_Frontier", ref errorCount);
        }

        private static void ValidateDarkRouteLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_DarkRoute");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_DarkRoute.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "DarkRoute_Entry", ref errorCount);
            ValidateNamedObject(lane, "DarkRoute_Mid", ref errorCount);
            ValidateNamedObject(lane, "DarkRoute_Far", ref errorCount);
            ValidateNamedObject(lane, "DarkRoute_Salvage_Close", ref errorCount);
            ValidateDescriptor(lane, "DarkRoute_Entry", ref errorCount);
            ValidateDescriptor(lane, "DarkRoute_Mid", ref errorCount);
            ValidateDescriptor(lane, "DarkRoute_Far", ref errorCount);
            ValidateDescriptor(lane, "DarkRoute_Salvage_Close", ref errorCount);
            ValidateScannableProbe(lane, "DarkRoute_HazardProbe", ref errorCount);
        }

        private static void ValidateScanCorridorLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_ScanCorridor");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_ScanCorridor.");
                errorCount++;
                return;
            }

            ValidateScannableProbe(lane, "Scan_Poi_ExpeditionContact", ref errorCount);
            ValidateScannableProbe(lane, "Scan_Poi_ResourceCache", ref errorCount);
            ValidateScannableProbe(lane, "Scan_Poi_StructureRelay", ref errorCount);
            ValidateNamedObject(lane, "Scan_Pickup_ResourceA", ref errorCount);
            ValidateNamedObject(lane, "Scan_Pickup_ResourceB", ref errorCount);
            ValidateDescriptor(lane, "Scan_Pickup_ResourceA", ref errorCount);
            ValidateDescriptor(lane, "Scan_Pickup_ResourceB", ref errorCount);
        }

        private static void ValidateCombatLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_CombatContacts");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_CombatContacts.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Combat_Dormant", ref errorCount);
            ValidateNamedObject(lane, "Combat_Aggressive", ref errorCount);
            ValidateNamedObject(lane, "Combat_Fractured", ref errorCount);
            ValidateNamedObject(lane, "Combat_Down", ref errorCount);
            ValidateNamedObject(lane, "Combat_Checkpoint", ref errorCount);
            ValidateDescriptor(lane, "Combat_Dormant", ref errorCount);
            ValidateDescriptor(lane, "Combat_Aggressive", ref errorCount);
            ValidateDescriptor(lane, "Combat_Fractured", ref errorCount);
            ValidateDescriptor(lane, "Combat_Down", ref errorCount);
            ValidateDescriptor(lane, "Combat_Checkpoint", ref errorCount);
        }

        private static void ValidateConstructionOpsLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_ConstructionOps");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_ConstructionOps.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Construct_SocketBase", ref errorCount);
            ValidateNamedObject(lane, "Construct_ClearLane", ref errorCount);
            ValidateNamedObject(lane, "Construct_Blocker", ref errorCount);
            ValidateNamedObject(lane, "Construct_SocketGuide", ref errorCount);

            ValidateDescriptor(lane, "Construct_SocketBase", ref errorCount);
            ValidateDescriptor(lane, "Construct_ClearLane", ref errorCount);
            ValidateDescriptor(lane, "Construct_Blocker", ref errorCount);
            ValidateDescriptor(lane, "Construct_SocketGuide", ref errorCount);

            ValidateModuleState(lane, "Construct_SocketBase", floodedExpected: false, shouldBeDamaged: false, ref errorCount, ref warningCount);
            ValidateCargoMass(lane, "Construct_Blocker", 140f, ref errorCount);
        }

        private static void ValidatePowerOpsLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_PowerOps");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_PowerOps.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Power_CurrentTurbine", ref errorCount);
            ValidateNamedObject(lane, "Power_RelayPylon", ref errorCount);
            ValidateNamedObject(lane, "Power_ServicePump", ref errorCount);
            ValidateNamedObject(lane, "Power_ServiceRoute", ref errorCount);
            ValidateNamedObject(lane, "Power_ExposedGuide", ref errorCount);

            ValidateDescriptor(lane, "Power_CurrentTurbine", ref errorCount);
            ValidateDescriptor(lane, "Power_RelayPylon", ref errorCount);
            ValidateDescriptor(lane, "Power_ServicePump", ref errorCount);
            ValidateDescriptor(lane, "Power_ServiceRoute", ref errorCount);
            ValidateDescriptor(lane, "Power_ExposedGuide", ref errorCount);

            ValidateModuleState(lane, "Power_ServiceRoute", floodedExpected: true, shouldBeDamaged: true, ref errorCount, ref warningCount);
        }

        private static void ValidateEndgameOpsLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_EndgameOps");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_EndgameOps.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Ops_Anchor", ref errorCount);
            ValidateNamedObject(lane, "Ops_Cargo_Work", ref errorCount);
            ValidateNamedObject(lane, "Ops_Salvage", ref errorCount);
            ValidateNamedObject(lane, "Ops_Service_Flooded", ref errorCount);
            ValidateNamedObject(lane, "Ops_Hazard", ref errorCount);
            ValidateNamedObject(lane, "Ops_Combat_Aggressive", ref errorCount);
            ValidateNamedObject(lane, "Ops_Frontier", ref errorCount);

            ValidateDescriptor(lane, "Ops_Anchor", ref errorCount);
            ValidateDescriptor(lane, "Ops_Cargo_Work", ref errorCount);
            ValidateDescriptor(lane, "Ops_Salvage", ref errorCount);
            ValidateDescriptor(lane, "Ops_Service_Flooded", ref errorCount);
            ValidateScannableProbe(lane, "Ops_Hazard", ref errorCount);
            ValidateDescriptor(lane, "Ops_Combat_Aggressive", ref errorCount);
            ValidateDescriptor(lane, "Ops_Frontier", ref errorCount);

            ValidateModuleState(lane, "Ops_Service_Flooded", floodedExpected: true, shouldBeDamaged: true, ref errorCount, ref warningCount);
        }

        private static void ValidateChoiceHubLane(Transform root, ref int errorCount, ref int warningCount)
        {
            Transform lane = root.Find("Lane_ChoiceHub");
            if (lane == null)
            {
                Debug.LogError("[ToolTrialRangeValidation] Missing Lane_ChoiceHub.");
                errorCount++;
                return;
            }

            ValidateNamedObject(lane, "Choice_Hub", ref errorCount);
            ValidateNamedObject(lane, "Choice_To_Recovery", ref errorCount);
            ValidateNamedObject(lane, "Choice_To_Construction", ref errorCount);
            ValidateNamedObject(lane, "Choice_To_Defense", ref errorCount);

            ValidateDescriptor(lane, "Choice_Hub", ref errorCount);
            ValidateDescriptor(lane, "Choice_To_Recovery", ref errorCount);
            ValidateDescriptor(lane, "Choice_To_Construction", ref errorCount);
            ValidateDescriptor(lane, "Choice_To_Defense", ref errorCount);
        }

        private static void ValidateCargoMass(Transform lane, string objectName, float expectedMass, ref int errorCount)
        {
            Transform target = lane.Find(objectName);
            if (target == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Missing cargo object: {objectName}");
                errorCount++;
                return;
            }

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Cargo object missing Rigidbody: {objectName}", target.gameObject);
                errorCount++;
                return;
            }

            if (target.GetComponent<FieldTargetDescriptor>() == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Cargo object missing FieldTargetDescriptor: {objectName}", target.gameObject);
                errorCount++;
            }

            if (Mathf.Abs(body.mass - expectedMass) > 0.01f)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Cargo mass mismatch on {objectName}. Expected {expectedMass:0.0}, got {body.mass:0.0}.", target.gameObject);
                errorCount++;
            }
        }

        private static void ValidateModuleState(Transform lane, string objectName, bool floodedExpected, bool shouldBeDamaged, ref int errorCount, ref int warningCount)
        {
            Transform target = lane.Find(objectName);
            if (target == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Missing service module: {objectName}");
                errorCount++;
                return;
            }

            BaseModule module = target.GetComponent<BaseModule>() ?? target.GetComponentInChildren<BaseModule>();
            if (module == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Service module missing BaseModule: {objectName}", target.gameObject);
                errorCount++;
                return;
            }

            if (module.IsFlooded != floodedExpected)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Flood state mismatch on {objectName}.", target.gameObject);
                errorCount++;
            }

            bool isDamaged = module.CurrentIntegrity < module.MaxIntegrity;
            if (shouldBeDamaged && !isDamaged)
            {
                Debug.LogWarning($"[ToolTrialRangeValidation] Expected damaged service target but found intact: {objectName}", target.gameObject);
                warningCount++;
            }

            if (!shouldBeDamaged && isDamaged)
            {
                Debug.LogWarning($"[ToolTrialRangeValidation] Expected intact control module but found damaged: {objectName}", target.gameObject);
                warningCount++;
            }
        }

        private static bool ValidateNamedObject(Transform parent, string name, ref int errorCount)
        {
            if (parent.Find(name) != null)
                return true;

            Debug.LogError($"[ToolTrialRangeValidation] Missing object: {name}");
            errorCount++;
            return false;
        }

        private static void ValidateScannableProbe(Transform parent, string name, ref int errorCount)
        {
            Transform probe = parent.Find(name);
            if (probe == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Missing scannable probe: {name}");
                errorCount++;
                return;
            }

            if (probe.GetComponent<ScannableTarget>() == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Probe missing ScannableTarget: {name}", probe.gameObject);
                errorCount++;
            }

            if (probe.GetComponent<FieldTargetDescriptor>() == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Probe missing FieldTargetDescriptor: {name}", probe.gameObject);
                errorCount++;
            }
        }

        private static void ValidateResourceNode(Transform parent, string name, bool shouldBeDepleted, ref int errorCount)
        {
            Transform target = parent.Find(name);
            if (target == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Missing resource node: {name}");
                errorCount++;
                return;
            }

            ResourceNode node = target.GetComponent<ResourceNode>();
            if (node == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Resource target missing ResourceNode: {name}", target.gameObject);
                errorCount++;
                return;
            }

            if (node.IsDepleted != shouldBeDepleted)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Resource node depletion mismatch on {name}.", target.gameObject);
                errorCount++;
            }

            if (target.GetComponent<FieldTargetDescriptor>() == null)
            {
                Debug.LogError($"[ToolTrialRangeValidation] Resource node missing FieldTargetDescriptor: {name}", target.gameObject);
                errorCount++;
            }
        }

        private static void ValidateDescriptor(Transform parent, string name, ref int errorCount)
        {
            Transform target = parent.Find(name);
            if (target == null)
                return;

            if (target.GetComponent<FieldTargetDescriptor>() != null)
                return;

            Debug.LogError($"[ToolTrialRangeValidation] Missing FieldTargetDescriptor on {name}.", target.gameObject);
            errorCount++;
        }

        private static void ConfigureUrpLitSurface(Material material, bool transparent)
        {
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.SetFloat("_Cull", (float)CullMode.Back);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
            }
        }

        private static GameObject CreateFinalPrefab(
            string prefabPath,
            PrimitiveType primitiveType,
            Vector3 scale,
            Material material,
            bool addBaseModule,
            Vector3 interiorSize,
            SocketSpec[] sockets)
        {
            GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));

            root.AddComponent<ModuleMarker>();
            AddStructuralCollider(root, primitiveType, scale);
            BuildFinalVisuals(root.transform, primitiveType, scale, material, addBaseModule);

            if (addBaseModule)
            {
                BaseModule baseModule = root.AddComponent<BaseModule>();
                GameObject triggerRoot = new GameObject("InteriorTrigger");
                triggerRoot.transform.SetParent(root.transform, false);
                triggerRoot.transform.localPosition = new Vector3(0f, 1f, 0f);
                BoxCollider trigger = triggerRoot.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = interiorSize;

                SerializedObject moduleSo = new SerializedObject(baseModule);
                moduleSo.FindProperty("interiorTrigger").objectReferenceValue = trigger;
                moduleSo.ApplyModifiedPropertiesWithoutUndo();

                AttachModuleLeakVfx(root.transform, baseModule, scale, primitiveType);
            }

            int socketsLayer = HectonLayerMasks.Sockets;
            if (sockets != null)
            {
                foreach (SocketSpec socketSpec in sockets)
                {
                    GameObject socket = new GameObject(socketSpec.Name);
                    socket.transform.SetParent(root.transform, false);
                    socket.transform.localPosition = socketSpec.LocalPosition;
                    socket.transform.localRotation = socketSpec.LocalRotation;
                    if (socketsLayer >= 0)
                        socket.layer = socketsLayer;

                    SphereCollider sphere = socket.AddComponent<SphereCollider>();
                    sphere.isTrigger = true;
                    sphere.radius = 0.15f;

                    socket.AddComponent<ModuleSocket>();
                }
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildFinalVisuals(Transform parent, PrimitiveType primitiveType, Vector3 scale, Material material, bool buildLodChain)
        {
            if (!buildLodChain)
            {
                CreateVisualPrimitive(parent, "__Visual", primitiveType, scale, material);
                return;
            }

            GameObject lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(parent, false);
            List<Renderer> lod0Renderers = new List<Renderer>(4);
            lod0Renderers.Add(CreateVisualPrimitive(lod0.transform, "Body", primitiveType, scale, material));
            AddModuleTrimSet(lod0.transform, scale, material, true, lod0Renderers);

            GameObject lod1 = new GameObject("LOD1");
            lod1.transform.SetParent(parent, false);
            List<Renderer> lod1Renderers = new List<Renderer>(2);
            lod1Renderers.Add(CreateVisualPrimitive(lod1.transform, "Body", primitiveType, new Vector3(scale.x * 0.98f, scale.y, scale.z * 0.98f), material));
            AddModuleTrimSet(lod1.transform, scale, material, false, lod1Renderers);

            GameObject lod2 = new GameObject("LOD2");
            lod2.transform.SetParent(parent, false);
            List<Renderer> lod2Renderers = new List<Renderer>(1)
            {
                CreateVisualPrimitive(lod2.transform, "Body", primitiveType, new Vector3(scale.x * 0.94f, scale.y * 0.96f, scale.z * 0.94f), material)
            };

            LODGroup lodGroup = parent.gameObject.AddComponent<LODGroup>();
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
            lodGroup.SetLODs(new[]
            {
                new LOD(0.6f, lod0Renderers.ToArray()),
                new LOD(0.15f, lod1Renderers.ToArray()),
                new LOD(0.04f, lod2Renderers.ToArray())
            });
            lodGroup.RecalculateBounds();
        }

        private static void AttachModuleLeakVfx(Transform root, BaseModule baseModule, Vector3 scale, PrimitiveType primitiveType)
        {
            if (root == null || baseModule == null)
                return;

            GameObject dustParticlesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DustParticlesPrefabPath);
            if (dustParticlesPrefab == null)
                return;

            Transform visualRoot = root.Find("LOD0");
            if (visualRoot == null)
                visualRoot = root;

            Transform existingLeak = FindChild(visualRoot, LeakVfxChildName);
            if (existingLeak != null)
                Object.DestroyImmediate(existingLeak.gameObject);

            GameObject leakObject = PrefabUtility.InstantiatePrefab(dustParticlesPrefab, visualRoot) as GameObject;
            if (leakObject == null)
                return;

            leakObject.name = LeakVfxChildName;

            if (!leakObject.TryGetComponent(out ParticleSystem leakParticleSystem))
                return;

            ConfigureModuleLeakVfx(leakObject.transform, leakParticleSystem, scale, primitiveType);
            AttachModuleLeakWetSheen(visualRoot, scale, primitiveType);
            AttachModuleIndustrialDecals(visualRoot, scale, primitiveType);
            SyncModuleLeakLodRenderers(root.gameObject, visualRoot);

            SerializedObject moduleSo = new SerializedObject(baseModule);
            moduleSo.FindProperty("leakVfx").objectReferenceValue = leakParticleSystem;
            moduleSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureModuleLeakVfx(Transform leakTransform, ParticleSystem leakParticleSystem, Vector3 scale, PrimitiveType primitiveType)
        {
            if (leakTransform == null || leakParticleSystem == null)
                return;

            bool isFlatModule = primitiveType == PrimitiveType.Cube && scale.y <= 0.5f;

            Vector3 localPosition;
            Vector3 localScale;
            Vector3 shapeScale;
            float lifetime;
            float startSpeed;
            float startSize;
            float emissionRate;
            float maxParticleSize;
            float sortingFudge;

            if (isFlatModule)
            {
                localPosition = new Vector3(scale.x * 0.34f, 0.08f, scale.z * 0.28f);
                localScale = new Vector3(0.14f, 0.22f, 0.14f);
                shapeScale = new Vector3(0.1f, 0.02f, 0.1f);
                lifetime = 2.4f;
                startSpeed = 0.42f;
                startSize = 0.04f;
                emissionRate = 5f;
                maxParticleSize = 0.09f;
                sortingFudge = 1.5f;
            }
            else
            {
                localPosition = new Vector3(scale.x * 0.36f, -scale.y * 0.26f, scale.z * 0.18f);
                localScale = new Vector3(0.18f, 0.32f, 0.18f);
                shapeScale = new Vector3(0.14f, 0.06f, 0.14f);
                lifetime = 2.9f;
                startSpeed = 0.58f;
                startSize = 0.05f;
                emissionRate = 7f;
                maxParticleSize = 0.12f;
                sortingFudge = 2f;
            }

            leakTransform.localPosition = localPosition;
            leakTransform.localRotation = Quaternion.identity;
            leakTransform.localScale = localScale;

            ParticleSystem.MainModule main = leakParticleSystem.main;
            main.playOnAwake = false;
            main.duration = 5.1f;
            main.simulationSpeed = 0.42f;
            main.maxParticles = 64;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;

            ParticleSystem.EmissionModule emission = leakParticleSystem.emission;
            emission.rateOverTime = emissionRate;

            ParticleSystem.ShapeModule shape = leakParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = shapeScale;

            ParticleSystemRenderer renderer = leakParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.lengthScale = 1f;
                renderer.sortingFudge = sortingFudge;
                renderer.maxParticleSize = maxParticleSize;
            }
        }

        private static void AttachModuleLeakWetSheen(Transform visualRoot, Vector3 scale, PrimitiveType primitiveType)
        {
            if (visualRoot == null)
                return;

            Material seepSheenMaterial = AssetDatabase.LoadAssetAtPath<Material>(RuinSeepSheenMaterialPath);
            if (seepSheenMaterial == null)
                return;

            Transform existingSheen = FindChild(visualRoot, LeakWetSheenChildName);
            if (existingSheen != null)
                Object.DestroyImmediate(existingSheen.gameObject);

            GameObject sheenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheenObject.name = LeakWetSheenChildName;
            sheenObject.transform.SetParent(visualRoot, false);

            Collider sheenCollider = sheenObject.GetComponent<Collider>();
            if (sheenCollider != null)
                Object.DestroyImmediate(sheenCollider);

            ConfigureModuleLeakWetSheen(sheenObject.transform, sheenObject.GetComponent<MeshRenderer>(), seepSheenMaterial, scale, primitiveType);
        }

        private static void AttachModuleIndustrialDecals(Transform visualRoot, Vector3 scale, PrimitiveType primitiveType)
        {
            if (visualRoot == null)
                return;

            bool isFlatModule = primitiveType == PrimitiveType.Cube && scale.y <= 0.5f;

            if (isFlatModule)
            {
                AttachGeneratedDecal(
                    visualRoot,
                    LeakStripeDecalChildName,
                    WorldSupportGeneratedDecalMaterialBuilder.WarningStripeMaterialPath,
                    new Vector3(scale.x * 0.25f, (scale.y * 0.5f) + 0.011f, scale.z * 0.18f),
                    new Vector3(90f, 0f, 0f),
                    new Vector3(0.24f, 0.12f, 0.24f));
                AttachGeneratedDecal(
                    visualRoot,
                    LeakScuffDecalChildName,
                    WorldSupportGeneratedDecalMaterialBuilder.CutterScorchMaterialPath,
                    new Vector3(-scale.x * 0.18f, (scale.y * 0.5f) + 0.011f, -scale.z * 0.24f),
                    new Vector3(90f, 0f, 0f),
                    new Vector3(0.18f, 0.14f, 0.18f));
                return;
            }

            AttachGeneratedDecal(
                visualRoot,
                LeakStripeDecalChildName,
                WorldSupportGeneratedDecalMaterialBuilder.WarningStripeMaterialPath,
                new Vector3(scale.x * 0.34f, -scale.y * 0.14f, (scale.z * 0.5f) + 0.011f),
                Vector3.zero,
                new Vector3(0.18f, 0.34f, 0.18f));
            AttachGeneratedDecal(
                visualRoot,
                LeakScuffDecalChildName,
                WorldSupportGeneratedDecalMaterialBuilder.CutterScorchMaterialPath,
                new Vector3(-scale.x * 0.31f, 0f, (scale.z * 0.5f) + 0.011f),
                Vector3.zero,
                new Vector3(0.24f, 0.3f, 0.24f));
        }

        private static void ConfigureModuleLeakWetSheen(
            Transform sheenTransform,
            MeshRenderer sheenRenderer,
            Material seepSheenMaterial,
            Vector3 scale,
            PrimitiveType primitiveType)
        {
            if (sheenTransform == null || sheenRenderer == null || seepSheenMaterial == null)
                return;

            bool isFlatModule = primitiveType == PrimitiveType.Cube && scale.y <= 0.5f;

            if (isFlatModule)
            {
                sheenTransform.localPosition = new Vector3(scale.x * 0.34f, (scale.y * 0.5f) + 0.01f, scale.z * 0.28f);
                sheenTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                sheenTransform.localScale = new Vector3(0.42f, 0.28f, 1f);
            }
            else
            {
                sheenTransform.localPosition = new Vector3(scale.x * 0.36f, -scale.y * 0.28f, (scale.z * 0.5f) + 0.01f);
                sheenTransform.localRotation = Quaternion.identity;
                sheenTransform.localScale = new Vector3(0.34f, 0.72f, 1f);
            }

            sheenRenderer.sharedMaterial = seepSheenMaterial;
            sheenRenderer.shadowCastingMode = ShadowCastingMode.Off;
            sheenRenderer.receiveShadows = false;
            sheenRenderer.lightProbeUsage = LightProbeUsage.Off;
            sheenRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            sheenRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            EditorUtility.SetDirty(sheenRenderer);
        }

        private static void SyncModuleLeakLodRenderers(GameObject prefabRoot, Transform visualRoot)
        {
            if (prefabRoot == null || visualRoot == null)
                return;

            LODGroup lodGroup = prefabRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
                return;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length <= 0)
                return;

            lods[0].renderers = AppendRenderers(
                lods[0].renderers,
                ResolveRenderer(visualRoot, LeakVfxChildName),
                ResolveRenderer(visualRoot, LeakWetSheenChildName),
                ResolveRenderer(visualRoot, LeakStripeDecalChildName),
                ResolveRenderer(visualRoot, LeakScuffDecalChildName));

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);
        }

        private static void AddModuleTrimSet(Transform parent, Vector3 scale, Material material, bool includeCrossMembers, List<Renderer> renderers)
        {
            float shellThickness = Mathf.Max(0.08f, Mathf.Min(scale.x, scale.y, scale.z) * 0.08f);
            float frameWidth = Mathf.Max(0.18f, Mathf.Min(scale.x, scale.z) * 0.18f);
            float ceilingBandHeight = Mathf.Max(0.12f, scale.y * 0.12f);

            renderers.Add(CreateVisualPrimitive(
                parent,
                "TopBand",
                PrimitiveType.Cube,
                new Vector3(scale.x, ceilingBandHeight, frameWidth),
                material,
                new Vector3(0f, (scale.y * 0.5f) - (ceilingBandHeight * 0.5f), 0f)));

            renderers.Add(CreateVisualPrimitive(
                parent,
                "BottomBand",
                PrimitiveType.Cube,
                new Vector3(scale.x, ceilingBandHeight, frameWidth),
                material,
                new Vector3(0f, (-scale.y * 0.5f) + (ceilingBandHeight * 0.5f), 0f)));

            if (!includeCrossMembers)
                return;

            renderers.Add(CreateVisualPrimitive(
                parent,
                "LeftRib",
                PrimitiveType.Cube,
                new Vector3(shellThickness, scale.y, scale.z),
                material,
                new Vector3((-scale.x * 0.5f) + (shellThickness * 0.5f), 0f, 0f)));

            renderers.Add(CreateVisualPrimitive(
                parent,
                "RightRib",
                PrimitiveType.Cube,
                new Vector3(shellThickness, scale.y, scale.z),
                material,
                new Vector3((scale.x * 0.5f) - (shellThickness * 0.5f), 0f, 0f)));
        }

        private static Renderer CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 scale,
            Material material)
        {
            return CreateVisualPrimitive(parent, name, primitiveType, scale, material, Vector3.zero);
        }

        private static Renderer CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 scale,
            Material material,
            Vector3 localPosition)
        {
            return CreateVisualPrimitive(parent, name, primitiveType, scale, material, localPosition, Vector3.zero);
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

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
                Object.DestroyImmediate(visualCollider);

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                if (IsAmbientFaunaPrimitive(name))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }

                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(visual);
            return renderer;
        }

        private static bool IsAmbientFaunaPrimitive(string name)
        {
            switch (name)
            {
                case "MicroSchoolA":
                case "MicroSchoolB":
                case "HunterPerch":
                case "SchoolSilhouette":
                case "ScavengerA":
                case "ScavengerB":
                case "ScavengerSilhouette":
                case "DriftScavengerA":
                case "DriftScavengerB":
                case "HunterPerchDebris":
                case "DriftSilhouetteDebris":
                case "DriftSchoolA":
                case "DriftSchoolB":
                case "PerchSchool":
                case "HunterMarker":
                case "DriftSilhouette":
                case "HunterSilhouette":
                    return true;

                default:
                    return false;
            }
        }

        private static void AddStructuralCollider(GameObject root, PrimitiveType primitiveType, Vector3 scale)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Cylinder:
                    CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
                    capsule.direction = 1;
                    capsule.height = Mathf.Max(scale.y, Mathf.Max(scale.x, scale.z));
                    capsule.radius = Mathf.Max(scale.x, scale.z) * 0.5f;
                    capsule.center = Vector3.zero;
                    break;

                default:
                    BoxCollider box = root.AddComponent<BoxCollider>();
                    box.size = scale;
                    box.center = Vector3.zero;
                    break;
            }
        }

        private static GameObject CreateCompositeFinalPrefab(string prefabPath, Vector3 colliderSize, CompositeLodSpec[] lodSpecs)
        {
            GameObject root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            root.AddComponent<ModuleMarker>();

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);

            BuildCompositeVisuals(root.transform, lodSpecs);
            AttachCompositeRuinLeakVfx(root.transform, prefabPath);
            ApplyAmbientFaunaShadowPolicy(root.transform);
            EditorUtility.SetDirty(root);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AttachCompositeRuinLeakVfx(Transform root, string prefabPath)
        {
            if (root == null || string.IsNullOrEmpty(prefabPath))
                return;

            if (prefabPath.EndsWith(RuinClusterMediumPrefabName))
            {
                AttachAmbientLeakPlume(
                    root,
                    "RuinLeakPlume_Main",
                    new Vector3(2.35f, 1.18f, -0.65f),
                    new Vector3(0.2f, 0.42f, 0.2f),
                    6f,
                    3.1f,
                    0.46f,
                    0.055f,
                    1.8f,
                    0.11f);
                AttachCompositeRuinLeakSheen(root, RuinSeepSheenMainChildName, new Vector3(2.28f, 0.2f, -0.58f), new Vector3(90f, 0f, 0f), new Vector3(0.52f, 0.36f, 1f));
                AttachCompositeRuinIndustrialDecal(root, RuinIndustrialStripeMainChildName, new Vector3(2.04f, 0.22f, -0.42f), new Vector3(90f, 0f, 0f), new Vector3(0.28f, 0.14f, 0.28f));
                SyncCompositeRuinLeakLodRenderers(root.gameObject, root.Find("LOD0"), "RuinLeakPlume_Main", RuinSeepSheenMainChildName, RuinIndustrialStripeMainChildName);
                return;
            }

            if (prefabPath.EndsWith(RuinMegastructurePrefabName))
            {
                AttachAmbientLeakPlume(
                    root,
                    "RuinLeakPlume_Core",
                    new Vector3(2.9f, 2.35f, -2.2f),
                    new Vector3(0.24f, 0.52f, 0.24f),
                    7f,
                    3.4f,
                    0.5f,
                    0.06f,
                    2f,
                    0.12f);
                AttachAmbientLeakPlume(
                    root,
                    "RuinLeakPlume_Bridge",
                    new Vector3(-1.1f, 1.05f, 4.55f),
                    new Vector3(0.2f, 0.44f, 0.2f),
                    5f,
                    2.9f,
                    0.42f,
                    0.05f,
                    1.6f,
                    0.1f);
                AttachCompositeRuinLeakSheen(root, RuinSeepSheenCoreChildName, new Vector3(2.72f, 0.24f, -2.08f), new Vector3(90f, 0f, 0f), new Vector3(0.68f, 0.44f, 1f));
                AttachCompositeRuinLeakSheen(root, RuinSeepSheenBridgeChildName, new Vector3(-1.08f, 1.01f, 4.42f), new Vector3(90f, 0f, 0f), new Vector3(0.44f, 0.28f, 1f));
                AttachCompositeRuinIndustrialDecal(root, RuinIndustrialStripeCoreChildName, new Vector3(2.34f, 0.26f, -1.84f), new Vector3(90f, 0f, 0f), new Vector3(0.32f, 0.16f, 0.32f));
                AttachCompositeRuinIndustrialDecal(root, RuinIndustrialStripeBridgeChildName, new Vector3(-0.96f, 1.02f, 4.28f), new Vector3(90f, 0f, 0f), new Vector3(0.24f, 0.12f, 0.24f));
                SyncCompositeRuinLeakLodRenderers(root.gameObject, root.Find("LOD0"), "RuinLeakPlume_Core", "RuinLeakPlume_Bridge", RuinSeepSheenCoreChildName, RuinSeepSheenBridgeChildName, RuinIndustrialStripeCoreChildName, RuinIndustrialStripeBridgeChildName);
            }
        }

        private static void AttachAmbientLeakPlume(
            Transform root,
            string childName,
            Vector3 localPosition,
            Vector3 localScale,
            float emissionRate,
            float lifetime,
            float startSpeed,
            float startSize,
            float sortingFudge,
            float maxParticleSize)
        {
            if (root == null)
                return;

            GameObject dustParticlesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DustParticlesPrefabPath);
            if (dustParticlesPrefab == null)
                return;

            Transform visualRoot = root.Find("LOD0");
            if (visualRoot == null)
                visualRoot = root;

            Transform existingLeak = FindChild(visualRoot, childName);
            if (existingLeak != null)
                Object.DestroyImmediate(existingLeak.gameObject);

            GameObject leakObject = PrefabUtility.InstantiatePrefab(dustParticlesPrefab, visualRoot) as GameObject;
            if (leakObject == null)
                return;

            leakObject.name = childName;

            if (!leakObject.TryGetComponent(out ParticleSystem leakParticleSystem))
                return;

            leakObject.transform.localPosition = localPosition;
            leakObject.transform.localRotation = Quaternion.identity;
            leakObject.transform.localScale = localScale;

            ParticleSystem.MainModule main = leakParticleSystem.main;
            main.playOnAwake = true;
            main.simulationSpeed = 0.38f;
            main.maxParticles = 72;
            main.startLifetime = lifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;

            ParticleSystem.EmissionModule emission = leakParticleSystem.emission;
            emission.rateOverTime = emissionRate;

            ParticleSystemRenderer renderer = leakParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.lengthScale = 1f;
                renderer.sortingFudge = sortingFudge;
                renderer.maxParticleSize = maxParticleSize;
            }
        }

        private static void AttachCompositeRuinLeakSheen(Transform root, string childName, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return;

            Material seepSheenMaterial = AssetDatabase.LoadAssetAtPath<Material>(RuinSeepSheenMaterialPath);
            if (seepSheenMaterial == null)
                return;

            Transform lod0 = root.Find("LOD0");
            if (lod0 == null)
                lod0 = root;

            Transform existingSheen = FindChild(lod0, childName);
            if (existingSheen != null)
                Object.DestroyImmediate(existingSheen.gameObject);

            GameObject sheenObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sheenObject.name = childName;
            sheenObject.transform.SetParent(lod0, false);

            Collider sheenCollider = sheenObject.GetComponent<Collider>();
            if (sheenCollider != null)
                Object.DestroyImmediate(sheenCollider);

            sheenObject.transform.localPosition = localPosition;
            sheenObject.transform.localRotation = Quaternion.Euler(localEulerAngles);
            sheenObject.transform.localScale = localScale;

            if (sheenObject.TryGetComponent(out MeshRenderer sheenRenderer))
            {
                sheenRenderer.sharedMaterial = seepSheenMaterial;
                sheenRenderer.shadowCastingMode = ShadowCastingMode.Off;
                sheenRenderer.receiveShadows = false;
                sheenRenderer.lightProbeUsage = LightProbeUsage.Off;
                sheenRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                sheenRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                EditorUtility.SetDirty(sheenRenderer);
            }
        }

        private static void AttachCompositeRuinIndustrialDecal(
            Transform root,
            string childName,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return;

            Transform lod0 = root.Find("LOD0");
            if (lod0 == null)
                lod0 = root;

            AttachGeneratedDecal(lod0, childName, WorldSupportGeneratedDecalMaterialBuilder.WarningStripeMaterialPath, localPosition, localEulerAngles, localScale);
        }

        private static void AttachGeneratedDecal(
            Transform parent,
            string childName,
            string materialPath,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            if (parent == null || string.IsNullOrEmpty(childName) || string.IsNullOrEmpty(materialPath))
                return;

            Material decalMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (decalMaterial == null)
                return;

            Transform existingDecal = FindChild(parent, childName);
            if (existingDecal != null)
                Object.DestroyImmediate(existingDecal.gameObject);

            GameObject decalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decalObject.name = childName;
            decalObject.transform.SetParent(parent, false);
            decalObject.transform.localPosition = localPosition;
            decalObject.transform.localRotation = Quaternion.Euler(localEulerAngles);
            decalObject.transform.localScale = localScale;
            Collider collider = decalObject.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);

            if (decalObject.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = decalMaterial;
                ConfigureDecalRenderer(renderer);
            }
        }

        private static void ConfigureDecalRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            EditorUtility.SetDirty(renderer);
        }

        private static void SyncCompositeRuinLeakLodRenderers(GameObject prefabRoot, Transform lod0, params string[] childNames)
        {
            if (prefabRoot == null || lod0 == null || childNames == null || childNames.Length <= 0)
                return;

            LODGroup lodGroup = prefabRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
                return;

            LOD[] lods = lodGroup.GetLODs();
            if (lods == null || lods.Length <= 0)
                return;

            s_RendererCache.Clear();
            for (int i = 0; i < childNames.Length; i++)
            {
                Renderer resolved = ResolveRenderer(lod0, childNames[i]);
                if (resolved != null)
                {
                    s_RendererCache.Add(resolved);
                }
            }

            lods[0].renderers = AppendRenderers(lods[0].renderers, s_RendererCache);

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(lodGroup);
        }

        private static Renderer[] AppendRenderers(Renderer[] existing, List<Renderer> additions)
        {
            int existingCount = existing != null ? existing.Length : 0;
            int additionCount = 0;
            for (int i = 0; i < additions.Count; i++)
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

            for (int i = 0; i < additions.Count; i++)
            {
                if (additions[i] != null && !ContainsRenderer(existing, additions[i]))
                    combined[writeIndex++] = additions[i];
            }

            return combined;
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
            Transform child = FindChild(parent, childName);
            return child != null ? child.GetComponent<Renderer>() : null;
        }

        private static void BuildCompositeVisuals(Transform parent, CompositeLodSpec[] lodSpecs)
        {
            if (lodSpecs == null || lodSpecs.Length <= 0)
                return;

            if (lodSpecs.Length == 1)
            {
                BuildCompositeVisualGroup(parent, lodSpecs[0].Visuals, null);
                return;
            }

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

                if (renderer != null && renderers != null)
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

        private static BuildableData CreateOrUpdateBuildable(
            string path,
            string moduleName,
            string description,
            GameObject finalPrefab,
            BuildableFamily family,
            float powerRating,
            int powerPriority)
        {
            BuildableData asset = AssetDatabase.LoadAssetAtPath<BuildableData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BuildableData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.moduleName = moduleName;
            asset.description = description;
            asset.family = family;
            asset.ghostPrefab = null;
            asset.finalPrefab = finalPrefab;
            asset.powerRating = powerRating;
            asset.powerPriority = powerPriority;
            asset.buildCost ??= new List<InventoryCost>(4);
            asset.buildCost.Clear();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AssignStarterBuildCosts(
            BuildableData foundation,
            BuildableData corridor,
            BuildableData pylon,
            BuildableData pump,
            BuildableData turbine)
        {
            ItemData reinforcedPlate = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_ReinforcedPlate.asset");
            ItemData pressureSeal = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_PressureSeal.asset");
            ItemData copperWire = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_CopperWire.asset");
            ItemData hydraulicActuator = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_HydraulicActuator.asset");
            ItemData relayMatrix = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_RelayMatrix.asset");
            ItemData pumpRotor = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_PumpRotor.asset");
            ItemData batteryCell = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_BatteryCell.asset");
            ItemData powerCoupler = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_PowerCoupler.asset");
            ItemData stabilizerCoil = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/_Project/Data/Items/Resources/Components/Comp_StabilizerCoil.asset");

            if (reinforcedPlate == null || pressureSeal == null || copperWire == null ||
                hydraulicActuator == null || relayMatrix == null || pumpRotor == null ||
                batteryCell == null || powerCoupler == null || stabilizerCoil == null)
                return;

            SetCosts(
                foundation,
                new InventoryCost { item = reinforcedPlate, amount = 2 },
                new InventoryCost { item = pressureSeal, amount = 1 });

            SetCosts(
                corridor,
                new InventoryCost { item = reinforcedPlate, amount = 1 },
                new InventoryCost { item = pressureSeal, amount = 2 },
                new InventoryCost { item = copperWire, amount = 1 });

            SetCosts(
                pylon,
                new InventoryCost { item = reinforcedPlate, amount = 1 },
                new InventoryCost { item = hydraulicActuator, amount = 1 },
                new InventoryCost { item = relayMatrix, amount = 1 });

            SetCosts(
                pump,
                new InventoryCost { item = pressureSeal, amount = 2 },
                new InventoryCost { item = pumpRotor, amount = 1 },
                new InventoryCost { item = batteryCell, amount = 1 });

            SetCosts(
                turbine,
                new InventoryCost { item = reinforcedPlate, amount = 1 },
                new InventoryCost { item = hydraulicActuator, amount = 1 },
                new InventoryCost { item = stabilizerCoil, amount = 1 },
                new InventoryCost { item = powerCoupler, amount = 1 });
        }

        private static void SetCosts(BuildableData buildable, params InventoryCost[] costs)
        {
            if (buildable == null || costs == null || costs.Length == 0)
                return;

            buildable.buildCost ??= new List<InventoryCost>(costs.Length);
            buildable.buildCost.Clear();

            for (int i = 0; i < costs.Length; i++)
            {
                InventoryCost cost = costs[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                buildable.buildCost.Add(new InventoryCost
                {
                    item = cost.item,
                    amount = cost.amount
                });
            }

            EditorUtility.SetDirty(buildable);
        }

        private static ModuleCatalog CreateOrUpdateModuleCatalog(string path, params BuildableData[] modules)
        {
            ModuleCatalog asset = AssetDatabase.LoadAssetAtPath<ModuleCatalog>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ModuleCatalog>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject so = new SerializedObject(asset);
            SerializedProperty listProp = so.FindProperty("allModules");

            // MERGE, never truncate. `listProp.arraySize = modules.Length` dropped every
            // row this tool does not own, and ModuleCatalog_Starter is a SHARED list:
            // ModuleArchitect1712 appends its fabricated modules and the construction
            // catalog repair appends the habitat recipes. Truncating back to the five
            // starter entries deleted both with no error and no symptom except a module
            // quietly vanishing from the build browser -- so re-running
            // "Rebuild Starter Construction Kit" silently undid other tools' work.
            //
            // Existing order is preserved: ModuleCatalog.GetAt/GetViewableAt hand that
            // order straight to the browser UI, so reordering would shuffle the player's
            // build menu.
            int existingCount = listProp.arraySize;
            for (int i = 0; i < modules.Length; i++)
            {
                BuildableData module = modules[i];
                if (module == null)
                    continue;

                bool alreadyPresent = false;
                for (int existing = 0; existing < existingCount; existing++)
                {
                    if (listProp.GetArrayElementAtIndex(existing).objectReferenceValue == module)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }
                if (alreadyPresent)
                    continue;

                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = module;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AssignCatalogToScene(ModuleCatalog catalog, BuildableData defaultBuildable)
        {
            ConstructionManager manager = Object.FindAnyObjectByType<ConstructionManager>(FindObjectsInactive.Include);
            if (manager != null)
            {
                SerializedObject managerSo = new SerializedObject(manager);
                managerSo.FindProperty("catalog").objectReferenceValue = catalog;
                managerSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
            }

            PlayerBuilder builder = Object.FindAnyObjectByType<PlayerBuilder>(FindObjectsInactive.Include);
            if (builder != null)
            {
                SerializedObject builderSo = new SerializedObject(builder);
                builderSo.FindProperty("activeBuildable").objectReferenceValue = defaultBuildable;

                int socketsLayer = HectonLayerMasks.Sockets;
                if (socketsLayer >= 0)
                {
                    int socketMask = 1 << socketsLayer;
                    builderSo.FindProperty("socketLayerMask").intValue = socketMask;
                }

                builderSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(builder);
            }
        }

        private readonly struct SocketSpec
        {
            public SocketSpec(string name, Vector3 localPosition, Quaternion localRotation)
            {
                Name = name;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }

            public string Name { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
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
            public VisualPrimitiveSpec(string name, PrimitiveType primitiveType, Vector3 scale, Vector3 localPosition, Vector3 localEulerAngles, Material material)
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
