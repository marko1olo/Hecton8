using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class WorldProceduralFinalVariantAuthoring
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";

        [MenuItem("Hecton/Authoring/Apply Procedural Final Variant First Wave", priority = 178)]
        public static void ApplyFirstWave()
        {
            Dictionary<string, VariantSpec[]> specsByFamily = BuildFirstWaveSpecs();
            string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { ProceduralFamilyFolder });
            HashSet<string> touchedFamilies = new HashSet<string>(familyGuids.Length, StringComparer.Ordinal);
            HashSet<string> discoveredFamilies = new HashSet<string>(familyGuids.Length, StringComparer.Ordinal);
            int linkedVariants = 0;
            int missingPrefabs = 0;
            int rejectedPrimitivePrefabs = 0;

            foreach (string familyGuid in familyGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuid);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                    continue;

                if (!specsByFamily.TryGetValue(family.familyId, out VariantSpec[] specs))
                    continue;

                discoveredFamilies.Add(family.familyId);
                if (ApplyVariantWave(family, specs, ref linkedVariants, ref missingPrefabs, ref rejectedPrimitivePrefabs))
                    touchedFamilies.Add(family.familyId);
            }

            int missingFamilies = 0;
            foreach (string familyId in specsByFamily.Keys)
            {
                if (discoveredFamilies.Contains(familyId))
                    continue;

                missingFamilies++;
                Debug.LogWarning($"[WorldFinalVariantAuthoring] Could not find procedural family '{familyId}' in '{ProceduralFamilyFolder}'.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[WorldFinalVariantAuthoring] First-wave final variants applied. FamiliesTouched={touchedFamilies.Count}, VariantsLinked={linkedVariants}, MissingPrefabs={missingPrefabs}, RejectedPrimitivePrefabs={rejectedPrimitivePrefabs}, MissingFamilies={missingFamilies}.");
        }

        private static bool ApplyVariantWave(
            WorldPrefabFamilyProfile family,
            IReadOnlyList<VariantSpec> specs,
            ref int linkedVariants,
            ref int missingPrefabs,
            ref int rejectedPrimitivePrefabs)
        {
            List<WorldPrefabFamilyProfile.VariantEntry> variants = new List<WorldPrefabFamilyProfile.VariantEntry>(family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>());
            bool changed = false;

            for (int i = 0; i < specs.Count; i++)
            {
                VariantSpec spec = specs[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.PrefabPath);
                if (prefab == null)
                {
                    missingPrefabs++;
                    Debug.LogWarning($"[WorldFinalVariantAuthoring] Missing prefab '{spec.PrefabPath}' for family '{family.familyId}'.");
                    continue;
                }

                if (WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh(prefab))
                {
                    rejectedPrimitivePrefabs++;
                    Debug.LogError($"[WorldFinalVariantAuthoring] Rejecting primitive final prefab '{spec.PrefabPath}' for family '{family.familyId}' variant '{spec.VariantId}'.");
                    continue;
                }

                int variantIndex = FindVariantIndex(variants, spec.VariantId);
                WorldPrefabFamilyProfile.VariantEntry entry = variantIndex >= 0
                    ? variants[variantIndex]
                    : new WorldPrefabFamilyProfile.VariantEntry();

                bool entryChanged = false;
                entryChanged |= SetIfDifferent(ref entry.variantId, spec.VariantId);
                entryChanged |= SetIfDifferent(ref entry.prefab, prefab);
                entryChanged |= SetIfDifferent(ref entry.weight, spec.Weight);
                entryChanged |= SetIfDifferent(ref entry.proxyOnly, false);
                entryChanged |= SetIfDifferent(ref entry.finalReady, true);
                entryChanged |= SetIfDifferent(ref entry.uniformScaleRange, spec.UniformScaleRange);

                if (variantIndex >= 0)
                {
                    variants[variantIndex] = entry;
                }
                else
                {
                    variants.Add(entry);
                    entryChanged = true;
                }

                if (entryChanged)
                    changed = true;

                linkedVariants++;
            }

            if (!changed)
                return false;

            family.variants = variants.ToArray();
            EditorUtility.SetDirty(family);
            return true;
        }

        private static int FindVariantIndex(IReadOnlyList<WorldPrefabFamilyProfile.VariantEntry> variants, string variantId)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant != null && string.Equals(variant.variantId, variantId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            return true;
        }

        private static Dictionary<string, VariantSpec[]> BuildFirstWaveSpecs()
        {
            return new Dictionary<string, VariantSpec[]>(16, StringComparer.Ordinal)
            {
                ["family.rock.small_floor"] = Combine(
                    new[]
                    {
                        new VariantSpec(
                            "family.rock.small_floor.final.nordic_beach",
                            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock.prefab",
                            1,
                            new Vector2(0.8f, 1.08f)),
                        new VariantSpec(
                            "family.rock.small_floor.final.mossy_forest",
                            "Assets/_Project/Prefabs/Nature/Rocks/Mossy_Forest_Rock.prefab",
                            1,
                            new Vector2(0.78f, 1.02f))
                    },
                    BuildIndexedGeologySpecs("family.rock.small_floor", "rock_floor", "PFB_Geo_RockFloor", 10, 2, new Vector2(0.92f, 1.08f))),
                ["family.rock.cluster.medium"] = Combine(
                    new[]
                    {
                        new VariantSpec(
                            "family.rock.cluster.medium.final.beach_formation",
                            "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock_Formation.prefab",
                            1,
                            new Vector2(0.82f, 1.12f)),
                        new VariantSpec(
                            "family.rock.cluster.medium.final.forest_shelf",
                            "Assets/_Project/Prefabs/Nature/Rocks/Forest_Rock_Shelf.prefab",
                            1,
                            new Vector2(0.86f, 1.05f)),
                        new VariantSpec(
                            "family.rock.cluster.medium.final.skala",
                            "Assets/_Project/Prefabs/Nature/Rocks/Rock_Skala.prefab",
                            1,
                            new Vector2(0.78f, 0.98f))
                    },
                    BuildIndexedGeologySpecs("family.rock.cluster.medium", "rock_cluster", "PFB_Geo_RockCluster", 10, 2, new Vector2(0.95f, 1.08f))),
                ["family.rock.shelf.large"] = BuildIndexedGeologySpecs("family.rock.shelf.large", "rock_shelf", "PFB_Geo_RockShelf", 8, 2, new Vector2(0.98f, 1.06f)),
                ["family.rock.arch.large"] = Combine(
                    new[]
                    {
                        new VariantSpec(
                            "family.rock.arch.large.final.arch_large",
                            "Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_RockArch_Large.prefab",
                            1,
                            new Vector2(0.98f, 1.05f))
                    },
                    BuildIndexedGeologySpecs("family.rock.arch.large", "rock_arch", "PFB_Geo_RockArch", 6, 2, new Vector2(0.98f, 1.05f))),
                ["family.cave.entrance"] = Combine(
                    new[]
                    {
                        new VariantSpec(
                            "family.cave.entrance.final.cave_entrance",
                            "Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_Cave_Entrance.prefab",
                            1,
                            new Vector2(0.98f, 1.06f))
                    },
                    BuildIndexedGeologySpecs("family.cave.entrance", "cave_entrance", "PFB_Geo_CaveEntrance", 6, 2, new Vector2(0.98f, 1.06f))),
                ["family.landmark.spire"] = Combine(
                    new[]
                    {
                        new VariantSpec(
                            "family.landmark.spire.final.landmark_spire",
                            "Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/PFB_Geo_Landmark_Spire.prefab",
                            1,
                            new Vector2(0.98f, 1.04f))
                    },
                    BuildIndexedGeologySpecs("family.landmark.spire", "landmark_spire", "PFB_Geo_LandmarkSpire", 6, 2, new Vector2(0.98f, 1.04f))),
                ["family.plant.giant"] = new[]
                {
                    new VariantSpec(
                        "family.plant.giant.final.silhouette",
                        "Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_PlantGiant.prefab",
                        2,
                        new Vector2(0.96f, 1.08f))
                },
                ["family.egg.cluster"] = new[]
                {
                    new VariantSpec(
                        "family.egg.cluster.final.nest_cluster",
                        "Assets/_Project/Prefabs/Nature/OrganicMisc/Final/PFB_Organic_EggCluster.prefab",
                        2,
                        new Vector2(0.94f, 1.06f))
                },
                ["family.ruin.module.single"] = new[]
                {
                    new VariantSpec(
                        "family.ruin.module.single.final.foundation",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Foundation.prefab",
                        2,
                        new Vector2(0.96f, 1.04f)),
                    new VariantSpec(
                        "family.ruin.module.single.final.corridor",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Corridor.prefab",
                        1,
                        new Vector2(0.96f, 1.04f))
                },
                ["family.ruin.cluster.medium"] = new[]
                {
                    new VariantSpec(
                        "family.ruin.cluster.medium.final.cluster_medium",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_ClusterMedium.prefab",
                        2,
                        new Vector2(0.98f, 1.04f))
                },
                ["family.ruin.megastructure"] = new[]
                {
                    new VariantSpec(
                        "family.ruin.megastructure.final.megastructure",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Ruin_Megastructure.prefab",
                        1,
                        new Vector2(0.98f, 1.03f))
                },
                ["family.route.power"] = new[]
                {
                    new VariantSpec(
                        "family.route.power.final.pylon",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Module_Pylon.prefab",
                        2,
                        new Vector2(0.96f, 1.04f)),
                    new VariantSpec(
                        "family.route.power.final.current_turbine",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Module_CurrentTurbine.prefab",
                        1,
                        new Vector2(0.96f, 1.04f))
                },
                ["family.service.scar"] = new[]
                {
                    new VariantSpec(
                        "family.service.scar.final.service_pump",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Module_ServicePump.prefab",
                        2,
                        new Vector2(0.96f, 1.04f))
                },
                ["family.debris.scatter"] = new[]
                {
                    new VariantSpec(
                        "family.debris.scatter.final.scrap_cluster",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Debris_ScrapCluster.prefab",
                        2,
                        new Vector2(0.94f, 1.08f))
                },
                ["family.debris.field"] = new[]
                {
                    new VariantSpec(
                        "family.debris.field.final.wreck_field",
                        "Assets/_Project/Prefabs/Construction/Final/PFB_Debris_WreckField.prefab",
                        2,
                        new Vector2(0.96f, 1.08f))
                },
                ["family.pocket.resource"] = new[]
                {
                    new VariantSpec(
                        "family.pocket.resource.final.cache",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Resource.prefab",
                        2,
                        new Vector2(0.94f, 1.08f))
                },
                ["family.pocket.hazard"] = new[]
                {
                    new VariantSpec(
                        "family.pocket.hazard.final.vent_cluster",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Hazard.prefab",
                        2,
                        new Vector2(0.94f, 1.08f))
                },
                ["family.pocket.safe"] = new[]
                {
                    new VariantSpec(
                        "family.pocket.safe.final.shelter",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Pocket_Safe.prefab",
                        2,
                        new Vector2(0.94f, 1.08f))
                },
                ["family.creature.spawn.passive"] = new[]
                {
                    new VariantSpec(
                        "family.creature.spawn.passive.final.school_anchor",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_CreatureSpawn_Passive.prefab",
                        2,
                        new Vector2(0.96f, 1.06f))
                },
                ["family.creature.spawn.predator"] = new[]
                {
                    new VariantSpec(
                        "family.creature.spawn.predator.final.predator_lair",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_CreatureSpawn_Predator.prefab",
                        2,
                        new Vector2(0.96f, 1.06f))
                },
                ["family.creature.zone.large_threat"] = new[]
                {
                    new VariantSpec(
                        "family.creature.zone.large_threat.final.ownership_zone",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_LargeThreat.prefab",
                        1,
                        new Vector2(0.98f, 1.03f))
                },
                ["family.creature.zone.abyss_apex"] = new[]
                {
                    new VariantSpec(
                        "family.creature.zone.abyss_apex.final.ownership_zone",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_AbyssApex.prefab",
                        1,
                        new Vector2(0.98f, 1.03f))
                },
                ["family.creature.zone.reef_apex"] = new[]
                {
                    new VariantSpec(
                        "family.creature.zone.reef_apex.final.ownership_zone",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_ReefApex.prefab",
                        1,
                        new Vector2(0.98f, 1.03f))
                },
                ["family.creature.zone.ruin_apex"] = new[]
                {
                    new VariantSpec(
                        "family.creature.zone.ruin_apex.final.ownership_zone",
                        "Assets/_Project/Prefabs/WorldSupport/Final/PFB_Support_Zone_RuinApex.prefab",
                        1,
                        new Vector2(0.98f, 1.03f))
                }
            };
        }

        private readonly struct VariantSpec
        {
            public VariantSpec(string variantId, string prefabPath, int weight, Vector2 uniformScaleRange)
            {
                VariantId = variantId;
                PrefabPath = prefabPath;
                Weight = Mathf.Max(1, weight);
                UniformScaleRange = uniformScaleRange;
            }

            public string VariantId { get; }
            public string PrefabPath { get; }
            public int Weight { get; }
            public Vector2 UniformScaleRange { get; }
        }

        private static VariantSpec[] Combine(params VariantSpec[][] groups)
        {
            int totalCount = 0;
            for (int i = 0; i < groups.Length; i++)
                totalCount += groups[i] != null ? groups[i].Length : 0;

            VariantSpec[] combined = new VariantSpec[totalCount];
            int writeIndex = 0;
            for (int i = 0; i < groups.Length; i++)
            {
                VariantSpec[] group = groups[i];
                if (group == null)
                    continue;

                for (int j = 0; j < group.Length; j++)
                    combined[writeIndex++] = group[j];
            }

            return combined;
        }

        private static VariantSpec[] BuildIndexedGeologySpecs(
            string familyId,
            string variantPrefix,
            string prefabPrefix,
            int count,
            int weight,
            Vector2 uniformScaleRange)
        {
            VariantSpec[] specs = new VariantSpec[count];
            for (int i = 0; i < count; i++)
            {
                string suffix = i.ToString("D2");
                specs[i] = new VariantSpec(
                    $"{familyId}.final.{variantPrefix}_{suffix}",
                    $"Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals/{prefabPrefix}_{suffix}.prefab",
                    weight,
                    uniformScaleRange);
            }

            return specs;
        }
    }
}
