using System.Collections.Generic;
using Hecton8.Construction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Authors interior-decor and colony-part procedural finals on top of the shared construction material stack.
    /// </summary>
    public static class WorldProceduralInteriorColonyFinalAuthoring
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Construction/InteriorColony";
        private const string FinalPrefabFolder = "Assets/_Project/Prefabs/Construction/Final/InteriorColony";

        /// <summary>
        /// Rebuilds procedural interior-decor and colony-part finals.
        /// </summary>
        [MenuItem("Hecton/Authoring/Rebuild Procedural Interior And Colony Finals", priority = 180)]
        public static void RebuildInteriorAndColonyFinals()
        {
            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder("Assets/_Project/Art/Materials/Construction");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Construction");
            EnsureFolder("Assets/_Project/Prefabs/Construction/Final");
            EnsureFolder(FinalPrefabFolder);

            Material panelMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Interior_PanelTrim.mat", new Color(0.24f, 0.56f, 0.62f, 1f));
            Material conduitMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Interior_Conduit.mat", new Color(0.84f, 0.58f, 0.18f, 1f));
            Material clutterMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Interior_ServiceClutter.mat", new Color(0.34f, 0.74f, 0.74f, 1f));
            Material colonyHullMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Colony_HabitatHull.mat", new Color(0.20f, 0.40f, 0.46f, 1f));
            Material colonyFrameMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Colony_Frame.mat", new Color(0.54f, 0.44f, 0.22f, 1f));
            Material colonyDockMat = CreateOrUpdateMaterial($"{MaterialFolder}/Mat_Colony_Docking.mat", new Color(0.18f, 0.62f, 0.70f, 1f));

            int createdCount = 0;
            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Interior_PanelTrim.prefab", new Vector3(3.2f, 2.4f, 0.8f), BuildPanelTrimLods(panelMat, conduitMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Interior_ConduitRun.prefab", new Vector3(4.6f, 1.6f, 1.3f), BuildConduitRunLods(conduitMat, panelMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Interior_ServiceClutter.prefab", new Vector3(2.4f, 1.8f, 2.1f), BuildServiceClutterLods(clutterMat, panelMat, conduitMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Colony_HabitatLimb.prefab", new Vector3(9.4f, 4.2f, 8.8f), BuildHabitatLimbLods(colonyHullMat, colonyFrameMat, colonyDockMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Colony_DockingBay.prefab", new Vector3(13.6f, 6.2f, 12.4f), BuildDockingBayLods(colonyDockMat, colonyFrameMat, colonyHullMat)) != null)
                createdCount++;

            if (CreateCompositeFinalPrefab($"{FinalPrefabFolder}/PFB_Colony_HabitatShell.prefab", new Vector3(11.8f, 7.2f, 10.8f), BuildHabitatShellLods(colonyHullMat, colonyFrameMat, colonyDockMat)) != null)
                createdCount++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WorldProceduralInteriorColonyFinalAuthoring] Rebuilt interior/colony final prefabs. Created={createdCount}.");
        }

        private static CompositeLodSpec[] BuildPanelTrimLods(Material panelMat, Material conduitMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("BulkheadPlate", PrimitiveType.Cube, new Vector3(2.6f, 2.0f, 0.18f), new Vector3(0f, 1.0f, 0f), Vector3.zero, panelMat),
                        new VisualPrimitiveSpec("TopRail", PrimitiveType.Cube, new Vector3(2.9f, 0.18f, 0.34f), new Vector3(0f, 1.95f, 0f), Vector3.zero, conduitMat),
                        new VisualPrimitiveSpec("BottomRail", PrimitiveType.Cube, new Vector3(2.9f, 0.14f, 0.28f), new Vector3(0f, 0.18f, 0f), new Vector3(0f, 0f, 4f), conduitMat),
                        new VisualPrimitiveSpec("LeftRib", PrimitiveType.Cube, new Vector3(0.16f, 1.9f, 0.26f), new Vector3(-1.32f, 1.02f, 0f), new Vector3(0f, 0f, -4f), panelMat),
                        new VisualPrimitiveSpec("RightRib", PrimitiveType.Cube, new Vector3(0.16f, 1.9f, 0.26f), new Vector3(1.32f, 1.02f, 0f), new Vector3(0f, 0f, 4f), panelMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("PanelMass", PrimitiveType.Cube, new Vector3(2.7f, 1.95f, 0.22f), new Vector3(0f, 0.98f, 0f), Vector3.zero, panelMat),
                        new VisualPrimitiveSpec("TopRail", PrimitiveType.Cube, new Vector3(2.8f, 0.16f, 0.26f), new Vector3(0f, 1.88f, 0f), Vector3.zero, conduitMat),
                        new VisualPrimitiveSpec("BottomRail", PrimitiveType.Cube, new Vector3(2.75f, 0.12f, 0.24f), new Vector3(0f, 0.20f, 0f), Vector3.zero, conduitMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("PanelSilhouette", PrimitiveType.Cube, new Vector3(2.8f, 2.0f, 0.18f), new Vector3(0f, 1.0f, 0f), Vector3.zero, panelMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildConduitRunLods(Material conduitMat, Material panelMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("MainRun", PrimitiveType.Cylinder, new Vector3(0.34f, 2.1f, 0.34f), new Vector3(0f, 0.74f, 0f), new Vector3(90f, 0f, 0f), conduitMat),
                        new VisualPrimitiveSpec("SideRunA", PrimitiveType.Cylinder, new Vector3(0.16f, 1.35f, 0.16f), new Vector3(-0.62f, 0.92f, 0.16f), new Vector3(90f, 0f, 0f), conduitMat),
                        new VisualPrimitiveSpec("SideRunB", PrimitiveType.Cylinder, new Vector3(0.14f, 1.18f, 0.14f), new Vector3(0.62f, 0.54f, -0.14f), new Vector3(90f, 0f, 0f), conduitMat),
                        new VisualPrimitiveSpec("SupportBeam", PrimitiveType.Cube, new Vector3(4.1f, 0.12f, 0.42f), new Vector3(0f, 0.2f, 0f), new Vector3(0f, 0f, -3f), panelMat),
                        new VisualPrimitiveSpec("Junction", PrimitiveType.Cube, new Vector3(0.52f, 0.42f, 0.56f), new Vector3(0f, 0.86f, 0.08f), new Vector3(0f, 18f, 0f), panelMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("MainRun", PrimitiveType.Cylinder, new Vector3(0.30f, 2.0f, 0.30f), new Vector3(0f, 0.72f, 0f), new Vector3(90f, 0f, 0f), conduitMat),
                        new VisualPrimitiveSpec("SupportBeam", PrimitiveType.Cube, new Vector3(4.0f, 0.12f, 0.34f), new Vector3(0f, 0.2f, 0f), Vector3.zero, panelMat),
                        new VisualPrimitiveSpec("Junction", PrimitiveType.Cube, new Vector3(0.44f, 0.32f, 0.44f), new Vector3(0f, 0.82f, 0f), Vector3.zero, panelMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("RunSilhouette", PrimitiveType.Cylinder, new Vector3(0.28f, 1.95f, 0.28f), new Vector3(0f, 0.7f, 0f), new Vector3(90f, 0f, 0f), conduitMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildServiceClutterLods(Material clutterMat, Material panelMat, Material conduitMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Cabinet", PrimitiveType.Cube, new Vector3(0.85f, 1.2f, 0.7f), new Vector3(-0.58f, 0.6f, -0.18f), new Vector3(0f, -10f, 0f), clutterMat),
                        new VisualPrimitiveSpec("Crate", PrimitiveType.Cube, new Vector3(0.72f, 0.48f, 0.72f), new Vector3(0.52f, 0.24f, 0.42f), new Vector3(0f, 12f, 0f), panelMat),
                        new VisualPrimitiveSpec("Canister", PrimitiveType.Cylinder, new Vector3(0.34f, 0.72f, 0.34f), new Vector3(0.74f, 0.36f, -0.44f), Vector3.zero, conduitMat),
                        new VisualPrimitiveSpec("CableDrum", PrimitiveType.Cylinder, new Vector3(0.54f, 0.22f, 0.54f), new Vector3(-0.02f, 0.16f, 0.12f), new Vector3(90f, 0f, 0f), conduitMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("MassA", PrimitiveType.Cube, new Vector3(0.92f, 1.18f, 0.74f), new Vector3(-0.42f, 0.59f, -0.08f), new Vector3(0f, -6f, 0f), clutterMat),
                        new VisualPrimitiveSpec("MassB", PrimitiveType.Cube, new Vector3(0.84f, 0.44f, 0.82f), new Vector3(0.52f, 0.22f, 0.18f), new Vector3(0f, 10f, 0f), panelMat),
                        new VisualPrimitiveSpec("Canister", PrimitiveType.Cylinder, new Vector3(0.28f, 0.64f, 0.28f), new Vector3(0.68f, 0.32f, -0.28f), Vector3.zero, conduitMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ClutterSilhouette", PrimitiveType.Cube, new Vector3(1.5f, 1.2f, 0.92f), new Vector3(0f, 0.6f, 0f), new Vector3(0f, 8f, 0f), clutterMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildHabitatLimbLods(Material hullMat, Material frameMat, Material accentMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("LimbCore", PrimitiveType.Cube, new Vector3(4.6f, 2.4f, 6.2f), new Vector3(-1.1f, 1.25f, 0f), new Vector3(0f, -8f, 0f), hullMat),
                        new VisualPrimitiveSpec("ForwardNeck", PrimitiveType.Cube, new Vector3(2.4f, 1.7f, 3.4f), new Vector3(2.0f, 1.05f, 1.2f), new Vector3(0f, 18f, 0f), accentMat),
                        new VisualPrimitiveSpec("ServicePod", PrimitiveType.Cube, new Vector3(1.6f, 1.3f, 2.3f), new Vector3(1.6f, 0.74f, -2.0f), new Vector3(0f, -20f, 0f), accentMat),
                        new VisualPrimitiveSpec("UpperFrame", PrimitiveType.Cube, new Vector3(5.4f, 0.28f, 0.9f), new Vector3(-0.4f, 2.24f, 0f), new Vector3(0f, 10f, 0f), frameMat),
                        new VisualPrimitiveSpec("SideBraceA", PrimitiveType.Cylinder, new Vector3(0.32f, 2.8f, 0.32f), new Vector3(-2.6f, 1.2f, 2.6f), new Vector3(0f, 0f, 12f), frameMat),
                        new VisualPrimitiveSpec("BasePlate", PrimitiveType.Cube, new Vector3(6.8f, 0.26f, 7.0f), new Vector3(0f, 0.13f, 0f), new Vector3(0f, 6f, 0f), hullMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Core", PrimitiveType.Cube, new Vector3(5.1f, 2.2f, 6.0f), new Vector3(-0.7f, 1.14f, 0f), new Vector3(0f, -4f, 0f), hullMat),
                        new VisualPrimitiveSpec("Neck", PrimitiveType.Cube, new Vector3(2.0f, 1.5f, 3.0f), new Vector3(2.2f, 0.96f, 0.6f), new Vector3(0f, 14f, 0f), accentMat),
                        new VisualPrimitiveSpec("Brace", PrimitiveType.Cylinder, new Vector3(0.28f, 2.4f, 0.28f), new Vector3(-2.2f, 1.0f, 2.0f), new Vector3(0f, 0f, 10f), frameMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(6.2f, 0.22f, 6.4f), new Vector3(0f, 0.11f, 0f), Vector3.zero, hullMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("LimbMass", PrimitiveType.Cube, new Vector3(5.8f, 2.2f, 6.4f), new Vector3(0f, 1.1f, 0f), new Vector3(0f, 6f, 0f), hullMat),
                        new VisualPrimitiveSpec("NeckMass", PrimitiveType.Cube, new Vector3(1.6f, 1.2f, 2.4f), new Vector3(2.3f, 0.86f, 0.8f), new Vector3(0f, 12f, 0f), accentMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildDockingBayLods(Material dockMat, Material frameMat, Material hullMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("BayMouth", PrimitiveType.Cube, new Vector3(6.8f, 3.6f, 4.2f), new Vector3(0f, 2.0f, 1.6f), Vector3.zero, dockMat),
                        new VisualPrimitiveSpec("Runway", PrimitiveType.Cube, new Vector3(4.4f, 0.34f, 8.2f), new Vector3(0f, 0.18f, -1.4f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("UpperArch", PrimitiveType.Cube, new Vector3(9.6f, 0.42f, 2.4f), new Vector3(0f, 4.2f, 2.6f), new Vector3(0f, 4f, 0f), frameMat),
                        new VisualPrimitiveSpec("SideFrameA", PrimitiveType.Cube, new Vector3(1.0f, 4.8f, 1.0f), new Vector3(-4.2f, 2.4f, 2.0f), new Vector3(0f, 10f, 3f), frameMat),
                        new VisualPrimitiveSpec("SideFrameB", PrimitiveType.Cube, new Vector3(1.0f, 4.6f, 1.0f), new Vector3(4.1f, 2.3f, 1.8f), new Vector3(0f, -12f, -3f), frameMat),
                        new VisualPrimitiveSpec("ServiceBridge", PrimitiveType.Cube, new Vector3(2.2f, 0.8f, 5.0f), new Vector3(0f, 1.0f, -3.4f), new Vector3(0f, 12f, 0f), dockMat),
                        new VisualPrimitiveSpec("RearMass", PrimitiveType.Cube, new Vector3(5.4f, 2.4f, 2.2f), new Vector3(0f, 1.2f, -5.0f), new Vector3(0f, 6f, 0f), hullMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("BayMass", PrimitiveType.Cube, new Vector3(7.2f, 3.4f, 4.0f), new Vector3(0f, 1.9f, 1.4f), Vector3.zero, dockMat),
                        new VisualPrimitiveSpec("Runway", PrimitiveType.Cube, new Vector3(4.0f, 0.28f, 7.4f), new Vector3(0f, 0.14f, -1.1f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("Arch", PrimitiveType.Cube, new Vector3(8.8f, 0.34f, 2.0f), new Vector3(0f, 4.0f, 2.4f), Vector3.zero, frameMat),
                        new VisualPrimitiveSpec("RearMass", PrimitiveType.Cube, new Vector3(5.0f, 2.0f, 2.0f), new Vector3(0f, 1.0f, -4.6f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("Bridge", PrimitiveType.Cube, new Vector3(1.8f, 0.6f, 4.4f), new Vector3(0f, 0.9f, -3.0f), new Vector3(0f, 10f, 0f), dockMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("BaySilhouette", PrimitiveType.Cube, new Vector3(7.8f, 3.4f, 4.2f), new Vector3(0f, 1.9f, 1.2f), Vector3.zero, dockMat),
                        new VisualPrimitiveSpec("Runway", PrimitiveType.Cube, new Vector3(3.6f, 0.24f, 7.0f), new Vector3(0f, 0.12f, -1.0f), Vector3.zero, hullMat),
                    }),
            };
        }

        private static CompositeLodSpec[] BuildHabitatShellLods(Material hullMat, Material frameMat, Material accentMat)
        {
            return new[]
            {
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod0Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ShellCore", PrimitiveType.Cube, new Vector3(5.2f, 5.4f, 5.2f), new Vector3(0f, 2.9f, 0f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("ForwardCollar", PrimitiveType.Cube, new Vector3(6.8f, 0.42f, 6.8f), new Vector3(0f, 4.6f, 0f), Vector3.zero, accentMat),
                        new VisualPrimitiveSpec("SidePodA", PrimitiveType.Cube, new Vector3(1.8f, 2.6f, 3.0f), new Vector3(-3.2f, 1.6f, 1.8f), new Vector3(0f, 12f, 0f), frameMat),
                        new VisualPrimitiveSpec("SidePodB", PrimitiveType.Cube, new Vector3(1.8f, 2.2f, 2.8f), new Vector3(3.0f, 1.4f, -1.7f), new Vector3(0f, -14f, 0f), frameMat),
                        new VisualPrimitiveSpec("DorsalFrame", PrimitiveType.Cube, new Vector3(1.2f, 4.2f, 1.2f), new Vector3(0.4f, 4.3f, 2.8f), new Vector3(0f, 10f, 6f), accentMat),
                        new VisualPrimitiveSpec("BasePlate", PrimitiveType.Cube, new Vector3(8.0f, 0.34f, 8.0f), new Vector3(0f, 0.17f, 0f), Vector3.zero, hullMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod1Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("Core", PrimitiveType.Cube, new Vector3(5.6f, 5.0f, 5.6f), new Vector3(0f, 2.7f, 0f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("Collar", PrimitiveType.Cube, new Vector3(6.6f, 0.34f, 6.6f), new Vector3(0f, 4.3f, 0f), Vector3.zero, accentMat),
                        new VisualPrimitiveSpec("SideMass", PrimitiveType.Cube, new Vector3(2.2f, 2.2f, 3.8f), new Vector3(0f, 1.5f, 2.2f), new Vector3(0f, 6f, 0f), frameMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(7.4f, 0.28f, 7.4f), new Vector3(0f, 0.14f, 0f), Vector3.zero, hullMat),
                    }),
                new CompositeLodSpec(
                    WorldProceduralStructuralContract.RequiredLod2Threshold,
                    new[]
                    {
                        new VisualPrimitiveSpec("ShellSilhouette", PrimitiveType.Cube, new Vector3(6.0f, 5.0f, 6.0f), new Vector3(0f, 2.6f, 0f), Vector3.zero, hullMat),
                        new VisualPrimitiveSpec("Base", PrimitiveType.Cube, new Vector3(7.0f, 0.24f, 7.0f), new Vector3(0f, 0.12f, 0f), Vector3.zero, accentMat),
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
                root.AddComponent<ModuleMarker>();

                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.size = colliderSize;
                collider.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);

                BuildCompositeVisuals(root.transform, lodSpecs);
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
            if (lodSpecs == null || lodSpecs.Length <= 0)
                return;

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
                renderer.sharedMaterial = material;

            return renderer;
        }

        private static Material CreateOrUpdateMaterial(string path, Color baseColor)
        {
            Shader shader = Shader.Find(WorldProceduralStructuralContract.UrpLitShaderName);
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
            material.SetColor("_BaseColor", baseColor);
            ConfigureUrpLitSurface(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureUrpLitSurface(Material material)
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

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
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
