using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralPlaceholderAuthoring
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        private const string PlaceholderPrefabRoot = "Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders";
        private const string PlaceholderMaterialRoot = "Assets/_Project/Materials/WorldRuntime/ProceduralPlaceholders";

        [MenuItem("Hecton/Authoring/Rebuild Procedural Placeholder Final Variants", priority = 179)]
        public static void RebuildPlaceholderFinalVariants()
        {
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/WorldRuntime");
            EnsureFolder(PlaceholderPrefabRoot);
            EnsureFolder("Assets/_Project/Materials");
            EnsureFolder("Assets/_Project/Materials/WorldRuntime");
            EnsureFolder(PlaceholderMaterialRoot);

            string[] familyGuids = AssetDatabase.FindAssets($"t:{nameof(WorldPrefabFamilyProfile)}", new[] { ProceduralFamilyFolder });
            int placeholderFamilies = 0;
            int updatedFamilies = 0;
            int cleanedFamilies = 0;

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string familyPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(familyPath);
                if (family == null || !family.allowRuntimeScatter)
                    continue;

                int placeholderIndex = FindPlaceholderFinalVariantIndex(family);
                if (FamilyHasRealFinalVariant(family))
                {
                    if (placeholderIndex >= 0)
                    {
                        RemoveVariantAt(family, placeholderIndex);
                        EditorUtility.SetDirty(family);
                        cleanedFamilies++;
                    }

                    continue;
                }

                GameObject prefab = CreateOrUpdatePlaceholderPrefab(family);
                if (prefab == null)
                {
                    Debug.LogWarning($"[WorldPlaceholderAuthoring] Failed to create placeholder prefab for '{family.familyId}'.");
                    continue;
                }

                if (EnsurePlaceholderFinalVariant(family, prefab))
                    updatedFamilies++;

                placeholderFamilies++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[WorldPlaceholderAuthoring] Placeholder final variants rebuilt. PlaceholderFamilies={placeholderFamilies}, UpdatedFamilies={updatedFamilies}, CleanedFamilies={cleanedFamilies}.");
        }

        public static bool IsPlaceholderFinalVariant(WorldPrefabFamilyProfile.VariantEntry variant)
        {
            return variant != null && variant.prefab != null && variant.prefab.GetComponent<WorldProceduralPlaceholderMarker>() != null;
        }

        private static bool EnsurePlaceholderFinalVariant(WorldPrefabFamilyProfile family, GameObject prefab)
        {
            string variantId = $"{family.familyId}.final.placeholder";
            Vector2 scaleRange = ResolvePlaceholderScaleRange(family);
            WorldPrefabFamilyProfile.VariantEntry[] variants = family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>();
            int index = FindPlaceholderFinalVariantIndex(family);
            WorldPrefabFamilyProfile.VariantEntry entry = index >= 0 ? variants[index] : new WorldPrefabFamilyProfile.VariantEntry();

            bool changed = false;
            changed |= SetIfDifferent(ref entry.variantId, variantId);
            changed |= SetIfDifferent(ref entry.prefab, prefab);
            changed |= SetIfDifferent(ref entry.weight, ResolvePlaceholderWeight(family));
            changed |= SetIfDifferent(ref entry.proxyOnly, false);
            changed |= SetIfDifferent(ref entry.finalReady, true);
            changed |= SetIfDifferent(ref entry.uniformScaleRange, scaleRange);

            if (index >= 0)
            {
                variants[index] = entry;
                if (changed)
                {
                    family.variants = variants;
                    EditorUtility.SetDirty(family);
                }

                return changed;
            }

            Array.Resize(ref variants, variants.Length + 1);
            variants[variants.Length - 1] = entry;
            family.variants = variants;
            EditorUtility.SetDirty(family);
            return true;
        }

        private static GameObject CreateOrUpdatePlaceholderPrefab(WorldPrefabFamilyProfile family)
        {
            WorldStreamingLayer layer = family.ResolveStreamingLayer();
            string fileSafeFamilyId = SanitizeFileName(family.familyId);
            string layerFolder = $"{PlaceholderPrefabRoot}/{layer}";
            string materialFolder = $"{PlaceholderMaterialRoot}/{layer}";
            EnsureFolder(layerFolder);
            EnsureFolder(materialFolder);

            Material material = CreateOrUpdateMaterial(family, $"{materialFolder}/MAT_{fileSafeFamilyId}_Placeholder.mat");
            if (material == null)
                return null;

            string recipe = ResolvePlaceholderRecipe(family);
            string prefabPath = $"{layerFolder}/PFB_{fileSafeFamilyId}_Placeholder.prefab";
            GameObject root = new GameObject($"PFB_{fileSafeFamilyId}_Placeholder");
            try
            {
                root.AddComponent<WorldFidelityRoot>().RefreshTrackedComponents();
                root.AddComponent<WorldProceduralPlaceholderMarker>()
                    .Configure(family, $"{family.familyId}.final.placeholder", recipe);

                BuildRecipe(root.transform, family, recipe, material);
                WorldFidelityRoot fidelityRoot = root.GetComponent<WorldFidelityRoot>();
                fidelityRoot.RefreshTrackedComponents();

                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return savedPrefab != null ? savedPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material CreateOrUpdateMaterial(WorldPrefabFamilyProfile family, string materialPath)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                if (shader == null)
                    return null;

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Color color = family.proxyColor;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.Lerp(color, Color.white, 0.25f) * 0.12f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildRecipe(Transform root, WorldPrefabFamilyProfile family, string recipe, Material material)
        {
            Transform visualRoot = CreateChild(root, "__Visual", Vector3.zero, Vector3.zero, Vector3.one);
            foreach (ShapeDef shape in BuildShapesForRecipe(family, recipe))
                CreateShape(visualRoot, shape, material);
        }

        private static ShapeDef[] BuildShapesForRecipe(WorldPrefabFamilyProfile family, string recipe)
        {
            switch (recipe)
            {
                case "KelpTall":
                    return BuildKelpTallShapes();
                case "KelpPatch":
                    return BuildKelpPatchShapes(family);
                case "PlantGiant":
                    return BuildPlantShapes();
                case "CoralLow":
                    return BuildCoralLowShapes(family);
                case "CoralBranching":
                    return BuildCoralBranchingShapes();
                case "DebrisField":
                    return BuildDebrisFieldShapes(family);
                case "DebrisScatter":
                    return BuildDebrisScatterShapes(family);
                case "RuinCluster":
                    return BuildRuinClusterShapes();
                case "RuinMegastructure":
                    return BuildRuinMegaShapes();
                case "RuinModule":
                    return BuildRuinModuleShapes();
                case "PowerRoute":
                    return BuildPowerRouteShapes();
                case "ServiceScar":
                    return BuildServiceScarShapes();
                case "PocketResource":
                    return BuildPocketShapes(false, false);
                case "PocketHazard":
                    return BuildPocketShapes(true, false);
                case "PocketSafe":
                    return BuildPocketShapes(false, true);
                case "EggCluster":
                    return BuildEggShapes(family);
                case "SpawnAnchorPredator":
                    return BuildSpawnAnchorShapes(true);
                case "SpawnAnchorPassive":
                    return BuildSpawnAnchorShapes(false);
                case "LargeThreatZone":
                    return BuildLargeThreatShapes();
                case "RockArch":
                    return BuildRockArchShapes();
                case "CaveEntrance":
                    return BuildCaveEntranceShapes();
                case "LandmarkSpire":
                    return BuildLandmarkShapes();
                case "RockCluster":
                    return BuildRockClusterShapes(family);
                default:
                    return BuildRockSmallShapes(family);
            }
        }

        private static ShapeDef[] BuildRockSmallShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 3, PrimitiveType.Cube, new Vector3(0.72f, 0.36f, 0.58f), 0.56f, 0.08f);
        }

        private static ShapeDef[] BuildRockClusterShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 5, PrimitiveType.Sphere, new Vector3(0.82f, 0.64f, 0.78f), 0.9f, 0.16f);
        }

        private static ShapeDef[] BuildRockArchShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "ArchLeft", new Vector3(-0.7f, 0.6f, 0f), new Vector3(0f, 0f, -5f), new Vector3(0.5f, 1.2f, 0.56f)),
                Shape(PrimitiveType.Cube, "ArchRight", new Vector3(0.7f, 0.56f, 0.04f), new Vector3(0f, 0f, 7f), new Vector3(0.52f, 1.1f, 0.58f)),
                Shape(PrimitiveType.Cube, "ArchTop", new Vector3(0f, 1.24f, 0.02f), new Vector3(0f, 0f, 6f), new Vector3(1.78f, 0.42f, 0.64f)),
                Shape(PrimitiveType.Sphere, "ArchMass", new Vector3(0f, 0.28f, -0.2f), Vector3.zero, new Vector3(0.88f, 0.38f, 0.76f))
            };
        }

        private static ShapeDef[] BuildCaveEntranceShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "Lintel", new Vector3(0f, 1.08f, 0f), Vector3.zero, new Vector3(2.1f, 0.42f, 0.72f)),
                Shape(PrimitiveType.Cube, "WallLeft", new Vector3(-0.88f, 0.58f, 0f), Vector3.zero, new Vector3(0.62f, 1.16f, 0.88f)),
                Shape(PrimitiveType.Cube, "WallRight", new Vector3(0.88f, 0.54f, 0.06f), Vector3.zero, new Vector3(0.62f, 1.08f, 0.92f)),
                Shape(PrimitiveType.Cylinder, "Threshold", new Vector3(0f, 0.12f, 0f), new Vector3(90f, 0f, 0f), new Vector3(1.18f, 0.12f, 1.18f))
            };
        }

        private static ShapeDef[] BuildLandmarkShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cylinder, "Spire", new Vector3(0f, 1.42f, 0f), Vector3.zero, new Vector3(0.46f, 1.6f, 0.46f)),
                Shape(PrimitiveType.Capsule, "Cap", new Vector3(0f, 2.82f, 0f), Vector3.zero, new Vector3(0.34f, 0.52f, 0.34f)),
                Shape(PrimitiveType.Cube, "Base", new Vector3(0f, 0.18f, 0f), new Vector3(0f, 18f, 0f), new Vector3(1.2f, 0.28f, 1.2f))
            };
        }

        private static ShapeDef[] BuildKelpTallShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Capsule, "StalkA", new Vector3(-0.28f, 1.02f, 0f), new Vector3(0f, 0f, 7f), new Vector3(0.14f, 1.08f, 0.14f)),
                Shape(PrimitiveType.Capsule, "StalkB", new Vector3(-0.08f, 1.18f, 0.02f), new Vector3(0f, 10f, -4f), new Vector3(0.14f, 1.22f, 0.14f)),
                Shape(PrimitiveType.Capsule, "StalkC", new Vector3(0.14f, 1.24f, -0.02f), new Vector3(0f, -8f, 6f), new Vector3(0.14f, 1.26f, 0.14f)),
                Shape(PrimitiveType.Cube, "FrondA", new Vector3(-0.08f, 1.46f, 0.08f), new Vector3(0f, 0f, 28f), new Vector3(0.1f, 0.62f, 0.32f)),
                Shape(PrimitiveType.Cube, "FrondB", new Vector3(0.24f, 1.74f, -0.04f), new Vector3(0f, 0f, -22f), new Vector3(0.1f, 0.7f, 0.34f))
            };
        }

        private static ShapeDef[] BuildKelpPatchShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 6, PrimitiveType.Capsule, new Vector3(0.12f, 0.92f, 0.12f), 0.55f, 0.1f);
        }

        private static ShapeDef[] BuildPlantShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cylinder, "Core", new Vector3(0f, 0.7f, 0f), Vector3.zero, new Vector3(0.24f, 0.86f, 0.24f)),
                Shape(PrimitiveType.Cube, "LeafA", new Vector3(0.28f, 1.16f, 0f), new Vector3(26f, 0f, 34f), new Vector3(0.12f, 0.92f, 0.48f)),
                Shape(PrimitiveType.Cube, "LeafB", new Vector3(-0.28f, 1.16f, 0f), new Vector3(26f, 180f, -34f), new Vector3(0.12f, 0.92f, 0.48f)),
                Shape(PrimitiveType.Cube, "LeafC", new Vector3(0f, 1.16f, 0.28f), new Vector3(26f, 90f, 34f), new Vector3(0.12f, 0.92f, 0.48f)),
                Shape(PrimitiveType.Cube, "LeafD", new Vector3(0f, 1.16f, -0.28f), new Vector3(26f, 270f, -34f), new Vector3(0.12f, 0.92f, 0.48f))
            };
        }

        private static ShapeDef[] BuildCoralLowShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 5, PrimitiveType.Sphere, new Vector3(0.34f, 0.3f, 0.34f), 0.42f, 0.16f);
        }

        private static ShapeDef[] BuildCoralBranchingShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cylinder, "Trunk", new Vector3(0f, 0.4f, 0f), Vector3.zero, new Vector3(0.18f, 0.42f, 0.18f)),
                Shape(PrimitiveType.Cylinder, "BranchA", new Vector3(0f, 0.84f, 0f), new Vector3(54f, 25f, 0f), new Vector3(0.1f, 0.56f, 0.1f)),
                Shape(PrimitiveType.Cylinder, "BranchB", new Vector3(0f, 0.84f, 0f), new Vector3(56f, 115f, 0f), new Vector3(0.1f, 0.52f, 0.1f)),
                Shape(PrimitiveType.Cylinder, "BranchC", new Vector3(0f, 0.84f, 0f), new Vector3(52f, 210f, 0f), new Vector3(0.1f, 0.54f, 0.1f)),
                Shape(PrimitiveType.Cylinder, "BranchD", new Vector3(0f, 0.84f, 0f), new Vector3(55f, 300f, 0f), new Vector3(0.1f, 0.5f, 0.1f))
            };
        }

        private static ShapeDef[] BuildDebrisScatterShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 4, PrimitiveType.Cube, new Vector3(0.42f, 0.18f, 0.62f), 0.72f, 0.1f, true);
        }

        private static ShapeDef[] BuildDebrisFieldShapes(WorldPrefabFamilyProfile family)
        {
            List<ShapeDef> shapes = new List<ShapeDef>(BuildDebrisScatterShapes(family))
            {
                Shape(PrimitiveType.Cube, "Frame", new Vector3(0f, 0.18f, 0f), new Vector3(16f, 32f, 12f), new Vector3(1.48f, 0.12f, 0.66f)),
                Shape(PrimitiveType.Cylinder, "Spool", new Vector3(-0.44f, 0.24f, 0.26f), new Vector3(90f, 0f, 0f), new Vector3(0.26f, 0.24f, 0.26f))
            };
            return shapes.ToArray();
        }

        private static ShapeDef[] BuildRuinModuleShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "Body", new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(1.2f, 1.1f, 0.92f)),
                Shape(PrimitiveType.Cube, "Breach", new Vector3(0.18f, 0.74f, 0.46f), new Vector3(0f, 18f, 0f), new Vector3(0.46f, 0.56f, 0.22f))
            };
        }

        private static ShapeDef[] BuildRuinClusterShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "Body", new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(1.2f, 1.1f, 0.92f)),
                Shape(PrimitiveType.Cube, "WingA", new Vector3(-0.92f, 0.54f, -0.12f), new Vector3(0f, -18f, 0f), new Vector3(0.86f, 0.72f, 0.54f)),
                Shape(PrimitiveType.Cube, "WingB", new Vector3(0.96f, 0.34f, 0.18f), new Vector3(0f, 34f, 18f), new Vector3(0.62f, 0.4f, 0.42f))
            };
        }

        private static ShapeDef[] BuildRuinMegaShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "Span", new Vector3(0f, 1.14f, 0f), Vector3.zero, new Vector3(2.8f, 0.42f, 1.24f)),
                Shape(PrimitiveType.Cube, "TowerLeft", new Vector3(-1.12f, 1.02f, 0f), Vector3.zero, new Vector3(0.62f, 2.06f, 0.62f)),
                Shape(PrimitiveType.Cube, "TowerRight", new Vector3(1.12f, 0.88f, 0f), Vector3.zero, new Vector3(0.62f, 1.78f, 0.62f))
            };
        }

        private static ShapeDef[] BuildPowerRouteShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cylinder, "Pylon", new Vector3(0f, 1.06f, 0f), Vector3.zero, new Vector3(0.18f, 1.16f, 0.18f)),
                Shape(PrimitiveType.Cube, "Arm", new Vector3(0f, 1.94f, 0f), Vector3.zero, new Vector3(1.26f, 0.12f, 0.18f)),
                Shape(PrimitiveType.Cylinder, "Node", new Vector3(0f, 0.34f, 0.46f), new Vector3(90f, 0f, 0f), new Vector3(0.22f, 0.18f, 0.22f))
            };
        }

        private static ShapeDef[] BuildServiceScarShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cube, "Strip", new Vector3(0f, 0.12f, 0f), new Vector3(0f, 12f, 0f), new Vector3(1.84f, 0.08f, 0.42f)),
                Shape(PrimitiveType.Cylinder, "Pump", new Vector3(-0.42f, 0.42f, 0f), Vector3.zero, new Vector3(0.22f, 0.36f, 0.22f)),
                Shape(PrimitiveType.Cube, "Panel", new Vector3(0.46f, 0.24f, 0.08f), new Vector3(16f, 22f, 14f), new Vector3(0.46f, 0.14f, 0.26f))
            };
        }

        private static ShapeDef[] BuildPocketShapes(bool hazard, bool safe)
        {
            List<ShapeDef> shapes = new List<ShapeDef>
            {
                Shape(PrimitiveType.Cylinder, "PocketBase", new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(1.06f, 0.08f, 1.06f)),
                Shape(PrimitiveType.Sphere, "PocketCore", new Vector3(0f, 0.38f, 0f), Vector3.zero, safe ? new Vector3(0.46f, 0.28f, 0.46f) : new Vector3(0.52f, 0.36f, 0.52f))
            };

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                float radians = angle * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Cos(radians) * 0.72f, 0.22f, Mathf.Sin(radians) * 0.72f);
                shapes.Add(Shape(
                    hazard ? PrimitiveType.Cylinder : PrimitiveType.Sphere,
                    $"PocketMarker_{i}",
                    position,
                    hazard ? new Vector3(-28f, angle, 0f) : new Vector3(0f, angle, 0f),
                    hazard ? new Vector3(0.12f, 0.62f, 0.12f) : new Vector3(0.14f, 0.24f, 0.14f)));
            }

            return shapes.ToArray();
        }

        private static ShapeDef[] BuildEggShapes(WorldPrefabFamilyProfile family)
        {
            return BuildScatterShapes(family, 5, PrimitiveType.Sphere, new Vector3(0.26f, 0.34f, 0.26f), 0.42f, 0.18f);
        }

        private static ShapeDef[] BuildSpawnAnchorShapes(bool predator)
        {
            List<ShapeDef> shapes = new List<ShapeDef>
            {
                Shape(PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.05f, 0f), new Vector3(90f, 0f, 0f), new Vector3(predator ? 0.92f : 0.68f, 0.08f, predator ? 0.92f : 0.68f))
            };

            int finCount = predator ? 3 : 2;
            for (int i = 0; i < finCount; i++)
            {
                float angle = i * (360f / finCount);
                float radians = angle * Mathf.Deg2Rad;
                shapes.Add(Shape(
                    PrimitiveType.Cube,
                    $"Fin_{i}",
                    new Vector3(Mathf.Cos(radians) * 0.42f, predator ? 0.44f : 0.28f, Mathf.Sin(radians) * 0.42f),
                    new Vector3(predator ? -18f : -8f, angle, predator ? 24f : 12f),
                    new Vector3(0.14f, predator ? 0.72f : 0.42f, 0.34f)));
            }

            return shapes.ToArray();
        }

        private static ShapeDef[] BuildLargeThreatShapes()
        {
            return new[]
            {
                Shape(PrimitiveType.Cylinder, "ZoneDisc", new Vector3(0f, 0.04f, 0f), Vector3.zero, new Vector3(2.6f, 0.04f, 2.6f)),
                Shape(PrimitiveType.Cube, "FinA", new Vector3(1.42f, 0.96f, 0f), new Vector3(-24f, 45f, 12f), new Vector3(0.22f, 1.96f, 0.54f)),
                Shape(PrimitiveType.Cube, "FinB", new Vector3(-1.42f, 0.96f, 0f), new Vector3(-24f, 225f, 12f), new Vector3(0.22f, 1.96f, 0.54f)),
                Shape(PrimitiveType.Cube, "FinC", new Vector3(0f, 0.96f, 1.42f), new Vector3(-24f, 135f, 12f), new Vector3(0.22f, 1.96f, 0.54f)),
                Shape(PrimitiveType.Cube, "FinD", new Vector3(0f, 0.96f, -1.42f), new Vector3(-24f, 315f, 12f), new Vector3(0.22f, 1.96f, 0.54f)),
                Shape(PrimitiveType.Sphere, "Core", new Vector3(0f, 0.54f, 0f), Vector3.zero, new Vector3(0.52f, 0.52f, 0.52f))
            };
        }

        private static ShapeDef[] BuildScatterShapes(WorldPrefabFamilyProfile family, int count, PrimitiveType primitive, Vector3 scale, float radius, float y, bool rotate = false)
        {
            ShapeDef[] shapes = new ShapeDef[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 position = StableOffset(family.familyId, 100 + i, radius, y);
                Vector3 rotation = rotate ? StableEuler(family.familyId, 200 + i, 70f, 120f) : Vector3.zero;
                Vector3 localScale = scale * StableScale(family.familyId, 300 + i, 0.78f, 1.22f);
                shapes[i] = Shape(primitive, $"Shape_{i}", position, rotation, localScale);
            }

            return shapes;
        }

        private static string ResolvePlaceholderRecipe(WorldPrefabFamilyProfile family)
        {
            if (family.ResolveContributesLargeThreatZone())
                return "LargeThreatZone";

            return family.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.Rock => "RockSmall",
                WorldPrefabFamilyProfile.ProceduralDomain.RockCluster => "RockCluster",
                WorldPrefabFamilyProfile.ProceduralDomain.RockArch => "RockArch",
                WorldPrefabFamilyProfile.ProceduralDomain.RockShelf => "RockCluster",
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => "CaveEntrance",
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => "LandmarkSpire",
                WorldPrefabFamilyProfile.ProceduralDomain.Kelp => family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Patch ? "KelpPatch" : "KelpTall",
                WorldPrefabFamilyProfile.ProceduralDomain.Plant => "PlantGiant",
                WorldPrefabFamilyProfile.ProceduralDomain.Coral => family.primaryPattern == WorldProceduralPattern.ReefNavigation ? "CoralBranching" : "CoralLow",
                WorldPrefabFamilyProfile.ProceduralDomain.Debris => family.clusterAccentRole == WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField ? "DebrisField" : "DebrisScatter",
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Landmark ? "RuinMegastructure" : family.placementMode == WorldPrefabFamilyProfile.PlacementMode.Cluster ? "RuinCluster" : "RuinModule",
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => "PowerRoute",
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => "ServiceScar",
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => "PocketResource",
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => "PocketHazard",
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => "PocketSafe",
                WorldPrefabFamilyProfile.ProceduralDomain.Egg => "EggCluster",
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => family.familyId.Contains("predator", StringComparison.OrdinalIgnoreCase) ? "SpawnAnchorPredator" : "SpawnAnchorPassive",
                _ => family.ResolveStreamingLayer() == WorldStreamingLayer.Construction ? "RuinModule" : "RockSmall"
            };
        }

        private static int FindPlaceholderFinalVariantIndex(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return -1;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly && IsPlaceholderFinalVariant(variant))
                    return i;
            }

            return -1;
        }

        private static bool FamilyHasRealFinalVariant(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null)
                return false;

            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null && variant.finalReady && !variant.proxyOnly && !IsPlaceholderFinalVariant(variant))
                    return true;
            }

            return false;
        }

        private static Vector2 ResolvePlaceholderScaleRange(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return new Vector2(0.95f, 1.05f);

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant == null)
                    continue;

                min = Mathf.Min(min, Mathf.Min(variant.uniformScaleRange.x, variant.uniformScaleRange.y));
                max = Mathf.Max(max, Mathf.Max(variant.uniformScaleRange.x, variant.uniformScaleRange.y));
            }

            return min == float.MaxValue ? new Vector2(0.95f, 1.05f) : new Vector2(Mathf.Max(0.1f, min), Mathf.Max(Mathf.Max(0.1f, min), max));
        }

        private static int ResolvePlaceholderWeight(WorldPrefabFamilyProfile family)
        {
            if (family == null || family.variants == null || family.variants.Length == 0)
                return 1;

            int max = 1;
            for (int i = 0; i < family.variants.Length; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = family.variants[i];
                if (variant != null)
                    max = Mathf.Max(max, Mathf.Max(1, variant.weight));
            }

            return max;
        }

        private static void RemoveVariantAt(WorldPrefabFamilyProfile family, int index)
        {
            List<WorldPrefabFamilyProfile.VariantEntry> variants = new List<WorldPrefabFamilyProfile.VariantEntry>(family.variants);
            variants.RemoveAt(index);
            family.variants = variants.ToArray();
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localEulerAngles = localEulerAngles;
            go.transform.localScale = localScale;
            return go.transform;
        }

        private static void CreateShape(Transform parent, ShapeDef shape, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(shape.primitive);
            primitive.name = shape.name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = shape.position;
            primitive.transform.localEulerAngles = shape.euler;
            primitive.transform.localScale = shape.scale;
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            Collider collider = primitive.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static ShapeDef Shape(PrimitiveType primitive, string name, Vector3 position, Vector3 euler, Vector3 scale)
            => new ShapeDef(primitive, name, position, euler, scale);

        private static Vector3 StableOffset(string seed, int salt, float radius, float y)
        {
            float angle = Stable01(seed, salt) * Mathf.PI * 2f;
            float distance = radius * Mathf.Lerp(0.3f, 1f, Stable01(seed, salt + 17));
            return new Vector3(Mathf.Cos(angle) * distance, y, Mathf.Sin(angle) * distance);
        }

        private static Vector3 StableEuler(string seed, int salt, float xMagnitude, float zMagnitude)
        {
            return new Vector3(Mathf.Lerp(-xMagnitude, xMagnitude, Stable01(seed, salt + 11)), Stable01(seed, salt + 23) * 360f, Mathf.Lerp(-zMagnitude, zMagnitude, Stable01(seed, salt + 37)));
        }

        private static float StableScale(string seed, int salt, float min, float max)
            => Mathf.Lerp(min, max, Stable01(seed, salt + 51));

        private static float Stable01(string seed, int salt)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (seed != null ? seed.GetHashCode() : 0);
                hash = hash * 31 + salt;
                return ((uint)hash % 1000U) / 999f;
            }
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            return true;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "generic";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            return new string(chars).Trim('_');
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;
            int slash = assetPath.LastIndexOf('/');
            if (slash <= 0)
                return;
            string parent = assetPath.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, assetPath.Substring(slash + 1));
        }

        private readonly struct ShapeDef
        {
            public ShapeDef(PrimitiveType primitive, string name, Vector3 position, Vector3 euler, Vector3 scale)
            {
                this.primitive = primitive;
                this.name = name;
                this.position = position;
                this.euler = euler;
                this.scale = scale;
            }

            public readonly PrimitiveType primitive;
            public readonly string name;
            public readonly Vector3 position;
            public readonly Vector3 euler;
            public readonly Vector3 scale;
        }
    }
}
