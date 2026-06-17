#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Legacy entrypoint kept for menu/search compatibility. The old M.A.S.K. layout is forbidden for UberNoir.
    /// Current contract: R=AO, G=Roughness, B=Metallic, A=Emission/default 1.
    /// </summary>
    internal static class HectonMaskChannelPacker
    {
        private const string MenuPath = "Hecton8/Rendering/Texture Channel Packer/Pack Selected A.R.M.";
        private const string OutputFolder = "Assets/_Project/BakedGeometry/Textures";
        private const int MaxPackedMaskSize = 4096;

        [MenuItem(MenuPath, priority = 210)]
        private static void PackSelectedMasks()
        {
            Texture2D ao = null;
            Texture2D roughness = null;
            Texture2D metallic = null;
            Texture2D albedo = null;
            bool invertRoughness = false;

            UnityEngine.Object[] selected = Selection.objects;
            for (int i = 0; i < selected.Length; i++)
            {
                Texture2D texture = selected[i] as Texture2D;
                if (texture == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(texture);
                string lowerPath = path.ToLowerInvariant();
                if (lowerPath.Contains("occlusion") || lowerPath.Contains("_ao") || lowerPath.Contains("ambient"))
                {
                    ao = texture;
                }
                else if (lowerPath.Contains("rough"))
                {
                    roughness = texture;
                    invertRoughness = false;
                }
                else if (lowerPath.Contains("smooth") && roughness == null)
                {
                    roughness = texture;
                    invertRoughness = true;
                }
                else if (lowerPath.Contains("metal"))
                {
                    metallic = texture;
                }
                else if (lowerPath.Contains("albedo") || lowerPath.Contains("basecolor") || lowerPath.Contains("diffuse"))
                {
                    albedo = texture;
                }
            }

            if (ao == null || roughness == null || metallic == null)
            {
                Debug.LogError("[HectonMaskChannelPacker] Select AO/occlusion, roughness-or-smoothness, and metallic textures. Albedo/basecolor is optional for Sobel normals.");
                return;
            }

            uint flags = HectonArmTextureChannelPacker.FlagInjectMacroNoise |
                         HectonArmTextureChannelPacker.FlagToksvigMipFiltering;
            if (invertRoughness)
                flags |= HectonArmTextureChannelPacker.FlagInvertRoughness;
            if (albedo != null)
                flags |= HectonArmTextureChannelPacker.FlagGenerateNormals;

            TexturePackerRequest request = new TexturePackerRequest
            {
                AoTexture = ao,
                RoughnessTexture = roughness,
                MetallicTexture = metallic,
                AlbedoTexture = albedo,
                Config = HectonArmTextureChannelPacker.DefaultConfig(flags),
                OutputFolder = OutputFolder,
                OutputName = BuildOutputName(ao, roughness, metallic),
                MaxSize = MaxPackedMaskSize,
                MacroNoiseStrength = 0.08f,
                TileSizeMeters = 4.0f,
                MacroWorldSpanMeters = 100000.0f,
                GlobalQualityWeight = 0.55f,
                Seed = 0x5348494Eu
            };

            if (!HectonArmTextureChannelPacker.TryPackArmAsset(request, out TexturePackerRunMetrics metrics))
            {
                Debug.LogError("[HectonMaskChannelPacker] ARM packing failed. Check selected texture import state.");
                return;
            }

            Debug.Log("[HectonMaskChannelPacker] Packed ARM texture: " + metrics.OutputPath);
        }

        private static string BuildOutputName(Texture2D ao, Texture2D roughness, Texture2D metallic)
        {
            string sourceName = ao != null ? ao.name : roughness != null ? roughness.name : metallic != null ? metallic.name : "TextureSet";
            sourceName = RemoveToken(sourceName, "_AmbientOcclusion");
            sourceName = RemoveToken(sourceName, "_Occlusion");
            sourceName = RemoveToken(sourceName, "_Roughness");
            sourceName = RemoveToken(sourceName, "_Metallic");
            sourceName = RemoveToken(sourceName, "_AO");
            sourceName = RemoveToken(sourceName, "_R");
            sourceName = RemoveToken(sourceName, "_M");
            return "TX_ARM_" + SanitizeAssetToken(sourceName);
        }

        private static string RemoveToken(string value, string token)
        {
            return string.IsNullOrEmpty(value) ? value : value.Replace(token, string.Empty);
        }

        private static string SanitizeAssetToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') ||
                    c == '_' ||
                    c == '-')
                {
                    continue;
                }

                chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
#endif
