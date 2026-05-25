using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Environment.Editor
{
    internal static class BiomeMatrixRuntimeVisualProfileAuthoring
    {
        private const string CatalogPath = "Assets/_Project/Data/Biomes/BiomeMatrixCatalog.asset";
        private const string OutputFolder = "Assets/_Project/Data/Biomes/RuntimeVisualProfiles";

        private readonly struct VisualSeed
        {
            public readonly Color ScatterBase;
            public readonly Color ScatterShallow;
            public readonly Color FogColor;
            public readonly Vector3 DepthFogDensity;
            public readonly float Turbidity;

            public VisualSeed(Color scatterBase, Color scatterShallow, Color fogColor, Vector3 depthFogDensity, float turbidity)
            {
                ScatterBase = scatterBase;
                ScatterShallow = scatterShallow;
                FogColor = fogColor;
                DepthFogDensity = depthFogDensity;
                Turbidity = turbidity;
            }
        }

        [MenuItem("HECTON/Environment/Rebuild Matrix Runtime Visual Profiles")]
        private static void RebuildMatrixRuntimeVisualProfiles()
        {
            HectonBiomeMatrixCatalog catalog = AssetDatabase.LoadAssetAtPath<HectonBiomeMatrixCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[BiomeMatrixRuntimeVisualProfileAuthoring] Missing catalog at '{CatalogPath}'.");
                return;
            }

            EnsureFolder(OutputFolder);

            int createdCount = 0;
            int assignedCount = 0;
            int updatedCount = 0;
            HectonBiomeMatrixProfile[] profiles = catalog.Profiles;
            if (profiles == null || profiles.Length == 0)
            {
                Debug.LogError("[BiomeMatrixRuntimeVisualProfileAuthoring] Catalog has no matrix profiles.");
                return;
            }

            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile matrixProfile = profiles[i];
                if (matrixProfile == null)
                    continue;

                string visualAssetPath = BuildVisualAssetPath(matrixProfile);
                HectonBiomeProfile visualProfile = AssetDatabase.LoadAssetAtPath<HectonBiomeProfile>(visualAssetPath);
                if (visualProfile == null)
                {
                    visualProfile = ScriptableObject.CreateInstance<HectonBiomeProfile>();
                    AssetDatabase.CreateAsset(visualProfile, visualAssetPath);
                    createdCount++;
                }

                ApplyVisualPreset(matrixProfile, visualProfile);
                EditorUtility.SetDirty(visualProfile);
                updatedCount++;

                if (matrixProfile.runtimeVisualProfile != visualProfile)
                {
                    matrixProfile.runtimeVisualProfile = visualProfile;
                    EditorUtility.SetDirty(matrixProfile);
                    assignedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[BiomeMatrixRuntimeVisualProfileAuthoring] Updated {updatedCount} runtime visual profiles. " +
                $"Created: {createdCount}. Assigned: {assignedCount}.");
        }

        private static string BuildVisualAssetPath(HectonBiomeMatrixProfile matrixProfile)
        {
            string safeBiomeName = SanitizeAssetName(matrixProfile.biomeName);
            string fileName =
                $"BiomeVisual_{matrixProfile.matrixIndex:000}_{matrixProfile.region}_{matrixProfile.depthTier:00}_{safeBiomeName}.asset";
            return Path.Combine(OutputFolder, fileName).Replace('\\', '/');
        }

        private static string SanitizeAssetName(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "Unnamed";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            string cleaned = source.Trim();
            for (int i = 0; i < invalidChars.Length; i++)
                cleaned = cleaned.Replace(invalidChars[i], '_');

            cleaned = cleaned.Replace(' ', '_');
            cleaned = cleaned.Replace('-', '_');
            cleaned = cleaned.Replace('/', '_');
            return cleaned;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }

        private static void ApplyVisualPreset(HectonBiomeMatrixProfile matrixProfile, HectonBiomeProfile visualProfile)
        {
            VisualSeed seed = ResolveFamilySeed(matrixProfile.familyId, matrixProfile.biomeName);
            ApplyDepth(ref seed, matrixProfile.depthTier);
            ApplyRegion(ref seed, matrixProfile.region);
            ApplyBiomeSpecificAccent(ref seed, matrixProfile.biomeName);

            visualProfile.biomeName = $"{matrixProfile.biomeName} [{matrixProfile.region} T{matrixProfile.depthTier:00}]";
            visualProfile.scatterColorBase = ClampColor(seed.ScatterBase);
            visualProfile.scatterColorShallow = ClampColor(seed.ScatterShallow);
            visualProfile.fogColor = ClampColor(seed.FogColor);
            visualProfile.depthFogDensity = ClampDepthFog(seed.DepthFogDensity);
            visualProfile.turbidityMultiplier = Mathf.Clamp(seed.Turbidity, 0.55f, 1.75f);
        }

        private static VisualSeed ResolveFamilySeed(string familyId, string biomeName)
        {
            if (familyId == "biome.family.littoral_karst")
                return new VisualSeed(
                    new Color(0.03f, 0.136f, 0.176f, 1f),
                    new Color(0.128f, 0.44f, 0.41f, 1f),
                    new Color(0.024f, 0.184f, 0.224f, 1f),
                    new Vector3(0.28f, 0.17f, 0.11f),
                    0.68f);

            if (familyId == "biome.family.fossil_reef")
                return new VisualSeed(
                    new Color(0.024f, 0.114f, 0.158f, 1f),
                    new Color(0.144f, 0.35f, 0.33f, 1f),
                    new Color(0.026f, 0.154f, 0.194f, 1f),
                    new Vector3(0.3f, 0.185f, 0.115f),
                    0.74f);

            if (familyId == "biome.family.crystal_growth")
                return new VisualSeed(
                    new Color(0.026f, 0.102f, 0.168f, 1f),
                    new Color(0.108f, 0.318f, 0.388f, 1f),
                    new Color(0.028f, 0.164f, 0.206f, 1f),
                    new Vector3(0.25f, 0.165f, 0.105f),
                    0.64f);

            if (familyId == "biome.family.sediment_drift")
                return new VisualSeed(
                    new Color(0.036f, 0.09f, 0.118f, 1f),
                    new Color(0.1f, 0.214f, 0.214f, 1f),
                    new Color(0.042f, 0.12f, 0.138f, 1f),
                    new Vector3(0.4f, 0.22f, 0.125f),
                    1.02f);

            if (familyId == "biome.family.volcanic_glass")
                return new VisualSeed(
                    new Color(0.022f, 0.096f, 0.152f, 1f),
                    new Color(0.082f, 0.286f, 0.332f, 1f),
                    new Color(0.026f, 0.138f, 0.182f, 1f),
                    new Vector3(0.31f, 0.19f, 0.12f),
                    0.82f);

            if (familyId == "biome.family.granite_escarpment" ||
                familyId == "biome.family.tectonic_spine")
                return new VisualSeed(
                    new Color(0.022f, 0.094f, 0.146f, 1f),
                    new Color(0.092f, 0.268f, 0.304f, 1f),
                    new Color(0.024f, 0.136f, 0.178f, 1f),
                    new Vector3(0.32f, 0.2f, 0.125f),
                    0.84f);

            if (familyId == "biome.family.rift_spine")
                return new VisualSeed(
                    new Color(0.016f, 0.062f, 0.11f, 1f),
                    new Color(0.07f, 0.18f, 0.22f, 1f),
                    new Color(0.016f, 0.09f, 0.12f, 1f),
                    new Vector3(0.44f, 0.27f, 0.16f),
                    1.08f);

            if (familyId == "biome.family.abyssal_silt")
                return new VisualSeed(
                    new Color(0.01f, 0.04f, 0.068f, 1f),
                    new Color(0.05f, 0.12f, 0.14f, 1f),
                    new Color(0.012f, 0.058f, 0.07f, 1f),
                    new Vector3(0.52f, 0.31f, 0.18f),
                    1.24f);

            if (familyId == "biome.family.rift_void")
                return new VisualSeed(
                    new Color(0.008f, 0.026f, 0.05f, 1f),
                    new Color(0.032f, 0.08f, 0.1f, 1f),
                    new Color(0.008f, 0.034f, 0.05f, 1f),
                    new Vector3(0.58f, 0.34f, 0.2f),
                    1.3f);

            if (familyId == "biome.family.chemosynthetic_brine")
                return new VisualSeed(
                    new Color(0.02f, 0.072f, 0.088f, 1f),
                    new Color(0.09f, 0.2f, 0.16f, 1f),
                    new Color(0.024f, 0.1f, 0.09f, 1f),
                    new Vector3(0.5f, 0.31f, 0.18f),
                    1.28f);

            if (familyId == "biome.family.volcanic_hadal")
                return new VisualSeed(
                    new Color(0.018f, 0.05f, 0.072f, 1f),
                    new Color(0.08f, 0.13f, 0.12f, 1f),
                    new Color(0.02f, 0.06f, 0.07f, 1f),
                    new Vector3(0.56f, 0.34f, 0.2f),
                    1.22f);

            if (familyId == "biome.family.metallic_hadal")
                return new VisualSeed(
                    new Color(0.015f, 0.05f, 0.076f, 1f),
                    new Color(0.07f, 0.14f, 0.15f, 1f),
                    new Color(0.018f, 0.07f, 0.086f, 1f),
                    new Vector3(0.5f, 0.3f, 0.18f),
                    1.15f);

            if (!string.IsNullOrWhiteSpace(biomeName) &&
                biomeName.ToLowerInvariant().Contains("reef", StringComparison.Ordinal))
                return new VisualSeed(
                    new Color(0.024f, 0.11f, 0.15f, 1f),
                    new Color(0.14f, 0.36f, 0.34f, 1f),
                    new Color(0.026f, 0.16f, 0.18f, 1f),
                    new Vector3(0.32f, 0.2f, 0.12f),
                    0.8f);

            return new VisualSeed(
                new Color(0.016f, 0.07f, 0.11f, 1f),
                new Color(0.08f, 0.2f, 0.22f, 1f),
                new Color(0.02f, 0.1f, 0.13f, 1f),
                new Vector3(0.4f, 0.24f, 0.14f),
                1f);
        }

        private static void ApplyDepth(ref VisualSeed seed, int depthTier)
        {
            if (depthTier <= 2)
            {
                seed = new VisualSeed(
                    Color.Lerp(seed.ScatterBase, new Color(0.044f, 0.204f, 0.272f, 1f), 0.5f),
                    Color.Lerp(seed.ScatterShallow, new Color(0.19f, 0.62f, 0.58f, 1f), 0.62f),
                    Color.Lerp(seed.FogColor, new Color(0.022f, 0.224f, 0.282f, 1f), 0.42f),
                    Vector3.Lerp(seed.DepthFogDensity, new Vector3(0.15f, 0.096f, 0.062f), 0.58f),
                    Mathf.Lerp(seed.Turbidity, Mathf.Max(0.44f, seed.Turbidity - 0.24f), 0.72f));
            }

            float depth01 = Mathf.InverseLerp(1f, 27f, depthTier);
            float darkness = Mathf.SmoothStep(0f, 1f, depth01);
            Color deepWater = new Color(0.018f, 0.086f, 0.156f, 1f);
            Color deepFog = new Color(0.012f, 0.054f, 0.108f, 1f);
            Color deepShallow = new Color(0.052f, 0.156f, 0.228f, 1f);

            seed = new VisualSeed(
                Color.Lerp(seed.ScatterBase, deepWater, darkness * 0.78f),
                Color.Lerp(seed.ScatterShallow, deepShallow, darkness * 0.84f),
                Color.Lerp(seed.FogColor, deepFog, darkness * 0.84f),
                Vector3.Lerp(
                    seed.DepthFogDensity,
                    seed.DepthFogDensity + new Vector3(0.16f, 0.09f, 0.04f),
                    darkness * 0.68f),
                Mathf.Lerp(seed.Turbidity, Mathf.Min(1.42f, seed.Turbidity + 0.16f), darkness * 0.72f));
        }

        private static void ApplyRegion(ref VisualSeed seed, HectonBiomeMatrixProfile.CardinalRegion region)
        {
            Color tint;
            float turbidityOffset;
            switch (region)
            {
                case HectonBiomeMatrixProfile.CardinalRegion.North:
                    tint = new Color(0.06f, 0.17f, 0.16f, 1f);
                    turbidityOffset = 0.08f;
                    break;

                case HectonBiomeMatrixProfile.CardinalRegion.South:
                    tint = new Color(0.05f, 0.22f, 0.24f, 1f);
                    turbidityOffset = -0.06f;
                    break;

                case HectonBiomeMatrixProfile.CardinalRegion.East:
                    tint = new Color(0.032f, 0.18f, 0.28f, 1f);
                    turbidityOffset = -0.02f;
                    break;

                default:
                    tint = new Color(0.07f, 0.15f, 0.18f, 1f);
                    turbidityOffset = 0.04f;
                    break;
            }

            seed = new VisualSeed(
                Color.Lerp(seed.ScatterBase, tint, 0.18f),
                Color.Lerp(seed.ScatterShallow, tint, 0.26f),
                Color.Lerp(seed.FogColor, tint, 0.22f),
                seed.DepthFogDensity,
                seed.Turbidity + turbidityOffset);
        }

        private static void ApplyBiomeSpecificAccent(ref VisualSeed seed, string biomeName)
        {
            if (string.IsNullOrWhiteSpace(biomeName))
                return;

            string lower = biomeName.ToLowerInvariant();
            if (lower.Contains("crystal", StringComparison.Ordinal) ||
                lower.Contains("alabaster", StringComparison.Ordinal) ||
                lower.Contains("glass", StringComparison.Ordinal))
            {
                Color accent = new Color(0.1f, 0.27f, 0.34f, 1f);
                seed = new VisualSeed(
                    seed.ScatterBase,
                    Color.Lerp(seed.ScatterShallow, accent, 0.16f),
                    Color.Lerp(seed.FogColor, accent, 0.08f),
                    seed.DepthFogDensity,
                    seed.Turbidity - 0.06f);
                return;
            }

            if (lower.Contains("silt", StringComparison.Ordinal) ||
                lower.Contains("ash", StringComparison.Ordinal) ||
                lower.Contains("dune", StringComparison.Ordinal))
            {
                Color accent = new Color(0.086f, 0.132f, 0.156f, 1f);
                seed = new VisualSeed(
                    Color.Lerp(seed.ScatterBase, accent, 0.1f),
                    Color.Lerp(seed.ScatterShallow, accent, 0.12f),
                    Color.Lerp(seed.FogColor, accent, 0.14f),
                    seed.DepthFogDensity + new Vector3(0.018f, 0.01f, 0.006f),
                    seed.Turbidity + 0.02f);
                return;
            }

            if (lower.Contains("lava", StringComparison.Ordinal) ||
                lower.Contains("magma", StringComparison.Ordinal) ||
                lower.Contains("thermal", StringComparison.Ordinal) ||
                lower.Contains("brine", StringComparison.Ordinal))
            {
                Color accent = new Color(0.18f, 0.11f, 0.06f, 1f);
                seed = new VisualSeed(
                    seed.ScatterBase,
                    Color.Lerp(seed.ScatterShallow, accent, 0.16f),
                    Color.Lerp(seed.FogColor, accent, 0.12f),
                    seed.DepthFogDensity + new Vector3(0.04f, 0.02f, 0.01f),
                    seed.Turbidity + 0.06f);
                return;
            }

            if (lower.Contains("void", StringComparison.Ordinal) ||
                lower.Contains("rift", StringComparison.Ordinal) ||
                lower.Contains("wound", StringComparison.Ordinal))
            {
                Color accent = new Color(0.018f, 0.06f, 0.114f, 1f);
                seed = new VisualSeed(
                    Color.Lerp(seed.ScatterBase, accent, 0.16f),
                    Color.Lerp(seed.ScatterShallow, accent, 0.16f),
                    Color.Lerp(seed.FogColor, accent, 0.18f),
                    seed.DepthFogDensity + new Vector3(0.012f, 0.008f, 0.004f),
                    seed.Turbidity + 0.01f);
            }
        }

        private static Color ClampColor(Color source)
        {
            source.r = Mathf.Clamp(source.r, 0f, 1f);
            source.g = Mathf.Clamp(source.g, 0f, 1f);
            source.b = Mathf.Clamp(source.b, 0f, 1f);
            source.a = 1f;
            return source;
        }

        private static Vector3 ClampDepthFog(Vector3 source)
        {
            source.x = Mathf.Clamp(source.x, 0.06f, 1.6f);
            source.y = Mathf.Clamp(source.y, 0.045f, 1.1f);
            source.z = Mathf.Clamp(source.z, 0.03f, 0.75f);
            return source;
        }
    }
}
