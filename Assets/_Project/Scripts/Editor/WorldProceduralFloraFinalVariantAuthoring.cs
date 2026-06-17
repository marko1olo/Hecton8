using System;
using System.Collections.Generic;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class WorldProceduralFloraFinalVariantAuthoring
    {
        private const string ProceduralFamilyFolder = "Assets/_Project/Data/World/ProceduralFamilies";
        internal const string FloraFinalRootFolder = "Assets/_Project/Prefabs/Nature/Flora/Baked";
        private const string OwnedVariantToken = ".final.flora.";
        private const string GeneratedStarterPrefix = "GEN_";
        private static readonly string[] FloraFamilyOrder =
        {
            "family.kelp.tall",
            "family.kelp.patch.dense",
            "family.kelp.canopy",
            "family.kelp.abyssal",
            "family.coral.low",
            "family.coral.branching",
            "family.coral.massive",
            "family.coral.plate",
            "family.coral.brittle"
        };

        private static readonly HashSet<string> FloraFamilies = new HashSet<string>(FloraFamilyOrder, StringComparer.Ordinal);

        [MenuItem("Hecton8/Authoring/Apply Procedural Flora Final Variants", priority = 179)]
        public static void ApplyBakedFloraFinals()
        {
            EnsureFolder("Assets/_Project/Prefabs/Nature");
            EnsureFolder("Assets/_Project/Prefabs/Nature/Flora");
            EnsureFolder(FloraFinalRootFolder);
            EnsureFamilyFolders();

            Dictionary<string, WorldPrefabFamilyProfile> familiesById = LoadFloraFamilies();
            Dictionary<string, List<VariantSpec>> specsByFamily = DiscoverVariantSpecs();

            int linkedVariants = 0;
            int removedVariants = 0;
            int missingFamilies = 0;
            int touchedFamilies = 0;

            for (int familyIndex = 0; familyIndex < FloraFamilyOrder.Length; familyIndex++)
            {
                string familyId = FloraFamilyOrder[familyIndex];
                WorldPrefabFamilyProfile family;
                if (!familiesById.TryGetValue(familyId, out family) || family == null)
                    continue;

                List<VariantSpec> specs;
                specsByFamily.TryGetValue(familyId, out specs);

                if (ApplyFamilyVariants(family, specs, ref linkedVariants, ref removedVariants))
                    touchedFamilies++;
            }

            for (int familyIndex = 0; familyIndex < FloraFamilyOrder.Length; familyIndex++)
            {
                string familyId = FloraFamilyOrder[familyIndex];
                if (!specsByFamily.ContainsKey(familyId))
                    continue;

                if (!familiesById.ContainsKey(familyId))
                {
                    missingFamilies++;
                    Debug.LogWarning($"[WorldProceduralFloraFinalVariantAuthoring] Baked flora prefabs found for missing family '{familyId}'.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[WorldProceduralFloraFinalVariantAuthoring] Baked flora final variants applied. FamiliesTouched={touchedFamilies}, LinkedVariants={linkedVariants}, RemovedVariants={removedVariants}, MissingFamilies={missingFamilies}.");
        }

        internal static bool IsSupportedFloraFamily(string familyId)
        {
            return !string.IsNullOrWhiteSpace(familyId) && FloraFamilies.Contains(familyId);
        }

        internal static IReadOnlyCollection<string> GetSupportedFloraFamilies()
        {
            return FloraFamilies;
        }

        internal static IReadOnlyList<string> GetSupportedFloraFamiliesInOrder()
        {
            return FloraFamilyOrder;
        }

        internal static string ResolveFamilyIdFromAsset(string prefabPath, string prefabName)
        {
            return TryResolveFamilyId(prefabPath, prefabName);
        }

        internal static bool IsGeneratedStarterPrefabName(string prefabName)
        {
            return !string.IsNullOrWhiteSpace(prefabName)
                && prefabName.StartsWith(GeneratedStarterPrefix, StringComparison.Ordinal);
        }

        internal static PrefabMetadata ResolvePrefabMetadata(string familyId, string prefabName)
        {
            PrefabMetadata metadata = new PrefabMetadata(
                ResolveDefaultWeight(familyId),
                ResolveDefaultScaleRange(familyId),
                false,
                false,
                false,
                string.Empty);

            if (string.IsNullOrWhiteSpace(prefabName))
                return metadata;

            string[] tokens = prefabName.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (TryParseWeightToken(token, out int parsedWeight))
                {
                    if (metadata.HasCustomWeight)
                        return metadata.WithError($"Duplicate flora weight metadata token '{token}' in prefab '{prefabName}'. Use only one __wN token.");

                    metadata = metadata.WithWeight(parsedWeight);
                    continue;
                }

                if (TryParseScaleToken(token, out Vector2 parsedScaleRange))
                {
                    if (metadata.HasCustomScaleRange)
                        return metadata.WithError($"Duplicate flora scale metadata token '{token}' in prefab '{prefabName}'. Use only one __sMIN-MAX token.");

                    metadata = metadata.WithScaleRange(parsedScaleRange);
                    continue;
                }

                if (LooksLikeMetadataToken(token))
                    return metadata.WithError($"Unsupported flora metadata token '{token}' in prefab '{prefabName}'.");
            }

            return metadata;
        }

        private static Dictionary<string, WorldPrefabFamilyProfile> LoadFloraFamilies()
        {
            string[] familyGuids = AssetDatabase.FindAssets("t:WorldPrefabFamilyProfile", new[] { ProceduralFamilyFolder });
            Dictionary<string, WorldPrefabFamilyProfile> familiesById = new Dictionary<string, WorldPrefabFamilyProfile>(FloraFamilies.Count, StringComparer.Ordinal);

            for (int i = 0; i < familyGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(familyGuids[i]);
                WorldPrefabFamilyProfile family = AssetDatabase.LoadMainAssetAtPath(assetPath) as WorldPrefabFamilyProfile;
                if (family == null || string.IsNullOrWhiteSpace(family.familyId) || !FloraFamilies.Contains(family.familyId))
                    continue;

                familiesById[family.familyId] = family;
            }

            return familiesById;
        }

        private static Dictionary<string, List<VariantSpec>> DiscoverVariantSpecs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { FloraFinalRootFolder });
            Dictionary<string, List<VariantSpec>> specsByFamily = new Dictionary<string, List<VariantSpec>>(FloraFamilies.Count, StringComparer.Ordinal);
            HashSet<string> authoredFamilies = new HashSet<string>(FloraFamilies.Count, StringComparer.Ordinal);

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                string familyId = TryResolveFamilyId(prefabPath, prefab.name);
                if (string.IsNullOrWhiteSpace(familyId) || !IsSupportedFloraFamily(familyId))
                    continue;

                List<VariantSpec> specs;
                if (!specsByFamily.TryGetValue(familyId, out specs))
                {
                    specs = new List<VariantSpec>(4);
                    specsByFamily.Add(familyId, specs);
                }

                bool generatedStarter = IsGeneratedStarterPrefabName(prefab.name);
                PrefabMetadata metadata = ResolvePrefabMetadata(familyId, prefab.name);
                if (metadata.HasError)
                {
                    Debug.LogWarning(
                        $"[WorldProceduralFloraFinalVariantAuthoring] Skipping flora prefab '{prefabPath}' due to invalid intake metadata. {metadata.Error}");
                    continue;
                }

                if (!generatedStarter)
                    authoredFamilies.Add(familyId);

                specs.Add(new VariantSpec(
                    ResolveVariantIdForPrefab(familyId, prefab.name),
                    prefab,
                    metadata.Weight,
                    metadata.UniformScaleRange,
                    generatedStarter,
                    metadata.HasCustomWeight,
                    metadata.HasCustomScaleRange));
            }

            foreach (KeyValuePair<string, List<VariantSpec>> pair in specsByFamily)
            {
                if (!authoredFamilies.Contains(pair.Key))
                {
                    pair.Value.Sort(CompareVariantSpec);
                    CollapseDuplicateVariantSpecs(pair.Key, pair.Value);
                    continue;
                }

                List<VariantSpec> specs = pair.Value;
                for (int i = specs.Count - 1; i >= 0; i--)
                {
                    if (specs[i].IsGeneratedStarter)
                        specs.RemoveAt(i);
                }

                specs.Sort(CompareVariantSpec);
                CollapseDuplicateVariantSpecs(pair.Key, specs);
            }

            return specsByFamily;
        }

        private static void CollapseDuplicateVariantSpecs(string familyId, List<VariantSpec> specs)
        {
            if (specs == null || specs.Count <= 1)
                return;

            for (int i = specs.Count - 1; i > 0; i--)
            {
                VariantSpec current = specs[i];
                VariantSpec previous = specs[i - 1];
                if (!string.Equals(current.VariantId, previous.VariantId, StringComparison.Ordinal))
                    continue;

                string keptName = previous.Prefab != null ? previous.Prefab.name : previous.VariantId;
                string skippedName = current.Prefab != null ? current.Prefab.name : current.VariantId;
                Debug.LogWarning(
                    $"[WorldProceduralFloraFinalVariantAuthoring] Duplicate flora variant identity '{current.VariantId}' in family '{familyId}'. Keeping '{keptName}' and skipping '{skippedName}'.");
                specs.RemoveAt(i);
            }
        }

        private static bool ApplyFamilyVariants(
            WorldPrefabFamilyProfile family,
            List<VariantSpec> specs,
            ref int linkedVariants,
            ref int removedVariants)
        {
            List<WorldPrefabFamilyProfile.VariantEntry> variants = new List<WorldPrefabFamilyProfile.VariantEntry>(family.variants ?? Array.Empty<WorldPrefabFamilyProfile.VariantEntry>());
            bool changed = false;

            for (int i = variants.Count - 1; i >= 0; i--)
            {
                WorldPrefabFamilyProfile.VariantEntry variant = variants[i];
                if (variant == null || string.IsNullOrWhiteSpace(variant.variantId))
                    continue;

                if (!variant.variantId.Contains(OwnedVariantToken, StringComparison.Ordinal))
                    continue;

                if (ContainsVariantId(specs, variant.variantId))
                    continue;

                variants.RemoveAt(i);
                removedVariants++;
                changed = true;
            }

            if (specs != null)
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    VariantSpec spec = specs[i];
                    int variantIndex = FindVariantIndex(variants, spec.VariantId);
                    WorldPrefabFamilyProfile.VariantEntry entry = variantIndex >= 0
                        ? variants[variantIndex]
                        : new WorldPrefabFamilyProfile.VariantEntry();

                    bool entryChanged = false;
                    entryChanged |= SetIfDifferent(ref entry.variantId, spec.VariantId);
                    entryChanged |= SetIfDifferent(ref entry.prefab, spec.Prefab);
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
            }

            if (!changed)
                return false;

            if (family.variants == null || family.variants.Length != variants.Count)
                family.variants = new WorldPrefabFamilyProfile.VariantEntry[variants.Count];

            for (int i = 0; i < variants.Count; i++)
                family.variants[i] = variants[i];

            EditorUtility.SetDirty(family);
            return true;
        }

        private static bool ContainsVariantId(IReadOnlyList<VariantSpec> specs, string variantId)
        {
            if (specs == null)
                return false;

            for (int i = 0; i < specs.Count; i++)
            {
                if (string.Equals(specs[i].VariantId, variantId, StringComparison.Ordinal))
                    return true;
            }

            return false;
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

        private static string TryResolveFamilyId(string prefabPath, string prefabName)
        {
            if (!string.IsNullOrWhiteSpace(prefabPath))
            {
                string[] pathParts = prefabPath.Replace('\\', '/').Split('/');
                for (int i = pathParts.Length - 2; i >= 0; i--)
                {
                    string familyId = TryConvertSafeFamilyToken(pathParts[i]);
                    if (!string.IsNullOrWhiteSpace(familyId))
                        return familyId;
                }
            }

            return TryConvertSafeFamilyToken(prefabName);
        }

        private static string TryConvertSafeFamilyToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return null;

            string normalized = token;
            if (normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 7);

            int familyIndex = normalized.IndexOf("family_", StringComparison.Ordinal);
            if (familyIndex < 0)
                return null;

            normalized = normalized.Substring(familyIndex);
            int separatorIndex = normalized.IndexOf("__", StringComparison.Ordinal);
            if (separatorIndex >= 0)
                normalized = normalized.Substring(0, separatorIndex);

            string familyId = normalized.Replace('_', '.');
            return IsSupportedFloraFamily(familyId) ? familyId : null;
        }

        internal static string ResolveVariantIdForPrefab(string familyId, string prefabName)
        {
            return $"{familyId}{OwnedVariantToken}{Sanitize(BuildVariantIdentityName(prefabName))}";
        }

        internal static string BuildVariantIdentityName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
                return "variant";

            string[] tokens = prefabName.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                return prefabName;

            List<string> identityTokens = new List<string>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (TryParseWeightToken(token, out _)
                    || TryParseScaleToken(token, out _)
                    || LooksLikeMetadataToken(token))
                    continue;

                identityTokens.Add(token);
            }

            if (identityTokens.Count == 0)
                return prefabName;

            return string.Join("__", identityTokens);
        }

        private static Vector2 ResolveDefaultScaleRange(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                    return new Vector2(0.9f, 1.08f);
                case "family.kelp.patch.dense":
                    return new Vector2(0.94f, 1.08f);
                case "family.kelp.canopy":
                    return new Vector2(0.94f, 1.06f);
                case "family.kelp.abyssal":
                    return new Vector2(0.96f, 1.08f);
                case "family.coral.low":
                    return new Vector2(0.92f, 1.06f);
                case "family.coral.branching":
                    return new Vector2(0.94f, 1.08f);
                case "family.coral.massive":
                    return new Vector2(0.95f, 1.05f);
                case "family.coral.plate":
                    return new Vector2(0.96f, 1.04f);
                case "family.coral.brittle":
                    return new Vector2(0.94f, 1.08f);
                default:
                    return new Vector2(0.95f, 1.05f);
            }
        }

        private static int ResolveDefaultWeight(string familyId)
        {
            switch (familyId)
            {
                case "family.kelp.tall":
                case "family.coral.low":
                    return 2;
                default:
                    return 1;
            }
        }

        private static bool TryParseWeightToken(string token, out int weight)
        {
            weight = 0;
            if (string.IsNullOrWhiteSpace(token) || token.Length < 2 || char.ToLowerInvariant(token[0]) != 'w')
                return false;

            if (!int.TryParse(token.Substring(1), out int parsedWeight))
                return false;

            weight = Mathf.Clamp(parsedWeight, 1, 32);
            return true;
        }

        private static bool TryParseScaleToken(string token, out Vector2 scaleRange)
        {
            scaleRange = default;
            if (string.IsNullOrWhiteSpace(token) || token.Length < 2 || char.ToLowerInvariant(token[0]) != 's')
                return false;

            string payload = token.Substring(1);
            int separatorIndex = payload.IndexOf('-');
            if (separatorIndex <= 0 || separatorIndex >= payload.Length - 1)
                return false;

            if (!int.TryParse(payload.Substring(0, separatorIndex), out int minPercent)
                || !int.TryParse(payload.Substring(separatorIndex + 1), out int maxPercent))
                return false;

            if (minPercent <= 0 || maxPercent <= 0 || minPercent > maxPercent)
                return false;

            scaleRange = new Vector2(minPercent * 0.01f, maxPercent * 0.01f);
            return true;
        }

        private static bool LooksLikeMetadataToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length < 2)
                return false;

            char prefix = char.ToLowerInvariant(token[0]);
            return (prefix == 'w' || prefix == 's') && char.IsDigit(token[1]);
        }

        private static int CompareVariantSpec(VariantSpec left, VariantSpec right)
        {
            int generatedComparison = left.IsGeneratedStarter.CompareTo(right.IsGeneratedStarter);
            if (generatedComparison != 0)
                return generatedComparison;

            int metadataComparison = GetMetadataPriority(right).CompareTo(GetMetadataPriority(left));
            if (metadataComparison != 0)
                return metadataComparison;

            int nameComparison = string.CompareOrdinal(left.Prefab != null ? left.Prefab.name : string.Empty, right.Prefab != null ? right.Prefab.name : string.Empty);
            if (nameComparison != 0)
                return nameComparison;

            return string.CompareOrdinal(left.VariantId, right.VariantId);
        }

        private static int GetMetadataPriority(VariantSpec spec)
        {
            int priority = 0;
            if (spec.HasCustomWeight)
                priority++;

            if (spec.HasCustomScaleRange)
                priority++;

            return priority;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "variant";

            char[] buffer = value.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                char c = buffer[i];
                bool valid = (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9');
                buffer[i] = valid ? char.ToLowerInvariant(c) : '_';
            }

            return new string(buffer).Trim('_');
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            return true;
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
                return;

            int lastSeparator = assetPath.LastIndexOf('/');
            if (lastSeparator <= 0)
                return;

            string parentPath = assetPath.Substring(0, lastSeparator);
            string folderName = assetPath.Substring(lastSeparator + 1);
            EnsureFolder(parentPath);

            if (!AssetDatabase.IsValidFolder(assetPath))
                AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static void EnsureFamilyFolders()
        {
            for (int i = 0; i < FloraFamilyOrder.Length; i++)
            {
                string familyId = FloraFamilyOrder[i];
                EnsureFolder($"{FloraFinalRootFolder}/{familyId.Replace('.', '_')}");
            }
        }

        private readonly struct VariantSpec
        {
            public VariantSpec(
                string variantId,
                GameObject prefab,
                int weight,
                Vector2 uniformScaleRange,
                bool isGeneratedStarter,
                bool hasCustomWeight,
                bool hasCustomScaleRange)
            {
                VariantId = variantId;
                Prefab = prefab;
                Weight = Mathf.Max(1, weight);
                UniformScaleRange = uniformScaleRange;
                IsGeneratedStarter = isGeneratedStarter;
                HasCustomWeight = hasCustomWeight;
                HasCustomScaleRange = hasCustomScaleRange;
            }

            public string VariantId { get; }
            public GameObject Prefab { get; }
            public int Weight { get; }
            public Vector2 UniformScaleRange { get; }
            public bool IsGeneratedStarter { get; }
            public bool HasCustomWeight { get; }
            public bool HasCustomScaleRange { get; }
        }

        internal readonly struct PrefabMetadata
        {
            public PrefabMetadata(
                int weight,
                Vector2 uniformScaleRange,
                bool hasCustomWeight,
                bool hasCustomScaleRange,
                bool hasError,
                string error)
            {
                Weight = weight;
                UniformScaleRange = uniformScaleRange;
                HasCustomWeight = hasCustomWeight;
                HasCustomScaleRange = hasCustomScaleRange;
                HasError = hasError;
                Error = error ?? string.Empty;
            }

            public int Weight { get; }
            public Vector2 UniformScaleRange { get; }
            public bool HasCustomWeight { get; }
            public bool HasCustomScaleRange { get; }
            public bool HasError { get; }
            public string Error { get; }

            public PrefabMetadata WithWeight(int weight)
            {
                return new PrefabMetadata(weight, UniformScaleRange, true, HasCustomScaleRange, HasError, Error);
            }

            public PrefabMetadata WithScaleRange(Vector2 uniformScaleRange)
            {
                return new PrefabMetadata(Weight, uniformScaleRange, HasCustomWeight, true, HasError, Error);
            }

            public PrefabMetadata WithError(string error)
            {
                return new PrefabMetadata(Weight, UniformScaleRange, HasCustomWeight, HasCustomScaleRange, true, error);
            }
        }
    }
}
