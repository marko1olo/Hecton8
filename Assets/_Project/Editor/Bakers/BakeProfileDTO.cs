using System;
using UnityEngine;

namespace Hecton8.Editor.Bakers
{
    public enum HectonBakeVariant
    {
        Organic = 0,
        Mineral = 1,
        Industrial = 2
    }

    [Serializable]
    public struct BakeProfileDTO
    {
        public string ProfileName;
        public HectonBakeVariant Variant;
        public int TextureSize;
        public float GlobalQualityWeight;
        public uint Seed;
        public Color BaseColor;
        public Color AccentColor;
        public Color WearOrEmissionColor;
        public float NoiseScale;
        public float PoreDensity;
        public float RustSpread;
        public float EdgeWearIntensity;
        public float NormalStrength;
        public float Metallic;
        public float Roughness;
        public float Emissive;

        public static BakeProfileDTO CoralAbyssal(uint seed)
        {
            return new BakeProfileDTO
            {
                ProfileName = "coral_abyssal_organic",
                Variant = HectonBakeVariant.Organic,
                TextureSize = 2048,
                GlobalQualityWeight = 0.72f,
                Seed = seed,
                BaseColor = new Color(0.05f, 0.11f, 0.13f, 1f),
                AccentColor = new Color(0.18f, 0.42f, 0.45f, 1f),
                WearOrEmissionColor = new Color(0.05f, 0.95f, 0.82f, 1f),
                NoiseScale = 12f,
                PoreDensity = 3.8f,
                RustSpread = 0.05f,
                EdgeWearIntensity = 0.22f,
                NormalStrength = 5.5f,
                Metallic = 0f,
                Roughness = 0.82f,
                Emissive = 0.38f
            };
        }

        public static BakeProfileDTO BasaltMineral(uint seed)
        {
            return new BakeProfileDTO
            {
                ProfileName = "basalt_layered_mineral",
                Variant = HectonBakeVariant.Mineral,
                TextureSize = 2048,
                GlobalQualityWeight = 0.64f,
                Seed = seed,
                BaseColor = new Color(0.035f, 0.041f, 0.048f, 1f),
                AccentColor = new Color(0.28f, 0.33f, 0.36f, 1f),
                WearOrEmissionColor = new Color(0.06f, 0.09f, 0.10f, 1f),
                NoiseScale = 18f,
                PoreDensity = 1.6f,
                RustSpread = 0.0f,
                EdgeWearIntensity = 0.58f,
                NormalStrength = 8f,
                Metallic = 0.02f,
                Roughness = 0.72f,
                Emissive = 0.0f
            };
        }

        public static BakeProfileDTO RustedIndustrial(uint seed)
        {
            return new BakeProfileDTO
            {
                ProfileName = "deep_reach_rusted_industrial",
                Variant = HectonBakeVariant.Industrial,
                TextureSize = 2048,
                GlobalQualityWeight = 0.78f,
                Seed = seed,
                BaseColor = new Color(0.16f, 0.17f, 0.16f, 1f),
                AccentColor = new Color(0.48f, 0.50f, 0.48f, 1f),
                WearOrEmissionColor = new Color(0.70f, 0.22f, 0.065f, 1f),
                NoiseScale = 10f,
                PoreDensity = 2.2f,
                RustSpread = 0.56f,
                EdgeWearIntensity = 0.84f,
                NormalStrength = 4.2f,
                Metallic = 0.86f,
                Roughness = 0.38f,
                Emissive = 0.02f
            };
        }
    }

    public static class BakeProfileSchema1605
    {
        public const string JsonSchema =
            "{ \"type\":\"object\", \"required\":[\"ProfileName\",\"Variant\",\"TextureSize\",\"GlobalQualityWeight\",\"Seed\",\"BaseColor\",\"AccentColor\",\"WearOrEmissionColor\",\"NoiseScale\",\"PoreDensity\",\"RustSpread\",\"EdgeWearIntensity\",\"NormalStrength\",\"Metallic\",\"Roughness\",\"Emissive\"], \"properties\":{\"Variant\":{\"enum\":[\"Organic\",\"Mineral\",\"Industrial\"]},\"TextureSize\":{\"minimum\":512,\"maximum\":4096},\"GlobalQualityWeight\":{\"minimum\":0,\"maximum\":1},\"NoiseScale\":{\"minimum\":0.01},\"PoreDensity\":{\"minimum\":0.01},\"RustSpread\":{\"minimum\":0,\"maximum\":1},\"EdgeWearIntensity\":{\"minimum\":0,\"maximum\":1},\"Metallic\":{\"minimum\":0,\"maximum\":1},\"Roughness\":{\"minimum\":0,\"maximum\":1},\"Emissive\":{\"minimum\":0,\"maximum\":1}} }";
    }
}
