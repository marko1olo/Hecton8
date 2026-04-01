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
            HashSet<string> touchedFamilies = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> discoveredFamilies = new HashSet<string>(StringComparer.Ordinal);
            int linkedVariants = 0;
            int missingPrefabs = 0;

            foreach (string familyGuid in familyGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuid);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadAssetAtPath<WorldPrefabFamilyProfile>(assetPath);
                if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                    continue;

                if (!specsByFamily.TryGetValue(family.familyId, out VariantSpec[] specs))
                    continue;

                discoveredFamilies.Add(family.familyId);
                if (ApplyVariantWave(family, specs, ref linkedVariants, ref missingPrefabs))
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
                $"[WorldFinalVariantAuthoring] First-wave final variants applied. FamiliesTouched={touchedFamilies.Count}, VariantsLinked={linkedVariants}, MissingPrefabs={missingPrefabs}, MissingFamilies={missingFamilies}.");
        }

        private static bool ApplyVariantWave(
            WorldPrefabFamilyProfile family,
            IReadOnlyList<VariantSpec> specs,
            ref int linkedVariants,
            ref int missingPrefabs)
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
            return new Dictionary<string, VariantSpec[]>(StringComparer.Ordinal)
            {
                ["family.rock.small_floor"] = new[]
                {
                    new VariantSpec(
                        "family.rock.small_floor.final.nordic_beach",
                        "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock.prefab",
                        2,
                        new Vector2(0.8f, 1.08f)),
                    new VariantSpec(
                        "family.rock.small_floor.final.mossy_forest",
                        "Assets/_Project/Prefabs/Nature/Rocks/Mossy_Forest_Rock.prefab",
                        1,
                        new Vector2(0.78f, 1.02f))
                },
                ["family.rock.cluster.medium"] = new[]
                {
                    new VariantSpec(
                        "family.rock.cluster.medium.final.beach_formation",
                        "Assets/_Project/Prefabs/Nature/Rocks/Nordic_Beach_Rock_Formation.prefab",
                        2,
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
    }
}
