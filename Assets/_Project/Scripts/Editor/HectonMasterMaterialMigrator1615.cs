#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class HectonMasterMaterialMigrator1615
    {
        private const string MasterShaderName = "Hecton8/Rendering/Hecton_Master_Lit";
        private const string MenuRoot = "Hecton8/Rendering/1615/";

        private static readonly string[] BaseTextureNames =
        {
            "_BaseMap",
            "_Base_Map",
            "_MainTex",
            "_AlbedoMap"
        };

        private static readonly string[] NormalTextureNames =
        {
            "_BumpMap",
            "_NormalMap",
            "_Normal_Map"
        };

        private static readonly string[] MaskTextureNames =
        {
            "_MRAOMap",
            "_MraoMap",
            "_PackedMap",
            "_MaskMap",
            "_Mask_Map",
            "_MetallicGlossMap"
        };

        private static readonly string[] UnsupportedExtraTextureNames =
        {
            "_DetailNormalMap",
            "_DetailMap",
            "_DetailMask",
            "_DetailAlbedoMap",
            "_FreshRockAlbedoMap",
            "_FreshRockNormalMap",
            "_SiltLayerMap",
            "_CavityNoiseRamp",
            "_HectonMicroNormalTex",
            "_BiomeFamilyTintVolume",
            "_SargassumCutMaskRT",
            "_HectonDamageVolumeTex",
            "_RustDetailMap",
            "_BlueNoiseTex",
            "_H8UberNoirAlbedoArray",
            "_H8UberNoirNormalArray",
            "_H8UberNoirMaskArray",
            "_TerrainControlRGBA",
            "_FlowNormal",
            "_DetailTex",
            "_EmissionTex",
            "_EmissionMap",
            "_ParallaxMap",
            "_ParasiteOverlayMap",
            "_ParasiteNormalMap",
            "_NormalAtlas",
            "_MaskAtlas",
            "_BaseAtlas",
            "_HUD_RenderTexture",
            "_ScratchNormalMap",
            "_FingerprintTex",
            "_WaterRunoffNormalTex",
            "_WaterDropletMaskTex"
        };

        private static readonly string[] UnsupportedSourceShaderNameFragments =
        {
            "Hidden/",
            "/UI/",
            "/VFX/",
            "/Sky/",
            "/Celestial/",
            "/Flora/",
            "/Fauna/",
            "GPUInstancer/",
            "Indirect",
            "Impostor",
            "Stencil",
            "Terrain",
            "Visor",
            "PDA",
            "Hologram",
            "Sonar",
            "Radar",
            "Ocean",
            "Weather",
            "Fabrication",
            "Physics/",
            "Runtime/",
            "Submarine",
            "Terminal",
            "Scanner",
            "Tether",
            "Plasma",
            "FluidDecal",
            "Decal"
        };

        private readonly struct MaskSemantics
        {
            public MaskSemantics(float metallicWeight, float roughnessWeight, float occlusionWeight, float emissionWeight, float layout)
            {
                MetallicWeight = metallicWeight;
                RoughnessWeight = roughnessWeight;
                OcclusionWeight = occlusionWeight;
                EmissionWeight = emissionWeight;
                Layout = layout;
            }

            public readonly float MetallicWeight;
            public readonly float RoughnessWeight;
            public readonly float OcclusionWeight;
            public readonly float EmissionWeight;
            public readonly float Layout;
        }

        [MenuItem(MenuRoot + "Migrate Selected Materials To Master Lit", priority = 1615)]
        private static void MigrateSelectedMaterials()
        {
            Shader masterShader = Shader.Find(MasterShaderName);
            if (masterShader == null)
                throw new InvalidOperationException("Hecton_Master_Lit shader is not importable yet.");

            UnityEngine.Object[] selected = Selection.objects;
            int migrated = 0;
            for (int index = 0; index < selected.Length; index++)
            {
                Material material = selected[index] as Material;
                if (material == null)
                    continue;

                if (MigrateMaterial(material, masterShader))
                    migrated++;
            }

            Debug.Log("[HectonMasterMaterialMigrator1615] migrated=" + migrated + " selectedMaterials=" + selected.Length);
        }

        [MenuItem(MenuRoot + "Migrate Selected Materials To Master Lit", true)]
        private static bool CanMigrateSelectedMaterials()
        {
            UnityEngine.Object[] selected = Selection.objects;
            for (int index = 0; index < selected.Length; index++)
            {
                if (selected[index] is Material)
                    return true;
            }

            return false;
        }

        private static bool MigrateMaterial(Material material, Shader masterShader)
        {
            if (material == null || masterShader == null)
                return false;
            if (material.shader == masterShader)
                return false;

            string sourceShaderName = material.shader != null ? material.shader.name : string.Empty;
            if (IsUnsupportedMasterSource(material, sourceShaderName))
                return false;

            Texture baseMap;
            string baseSource;
            Texture normalMap;
            string normalSource;
            Texture maskMap;
            string maskSource;

            bool hasBase = TryGetTexture(material, BaseTextureNames, out baseMap, out baseSource);
            bool hasNormal = TryGetTexture(material, NormalTextureNames, out normalMap, out normalSource);
            bool hasMask = TryGetTexture(material, MaskTextureNames, out maskMap, out maskSource);
            Vector2 baseScale = GetTextureScale(material, baseSource);
            Vector2 baseOffset = GetTextureOffset(material, baseSource);
            Vector2 normalScaleVector = GetTextureScale(material, normalSource);
            Vector2 normalOffset = GetTextureOffset(material, normalSource);
            Vector2 maskScale = GetTextureScale(material, maskSource);
            Vector2 maskOffset = GetTextureOffset(material, maskSource);
            Color baseColor = GetColor(material, "_BaseColor", GetColor(material, "_Color", Color.white));
            Color emissionColor = GetColor(material, "_EmissionColor", Color.clear);
            if (HasAssignedTexture(material, UnsupportedExtraTextureNames))
                return false;

            float metallicScale = Mathf.Clamp01(GetFloat(material, "_Metallic", GetFloat(material, "_MetallicScale", 0f)));
            float smoothnessScale = Mathf.Clamp01(GetFloat(material, "_Smoothness", GetFloat(material, "_GlossMapScale", GetFloat(material, "_Glossiness", 0.55f))));
            float roughnessScale = Mathf.Clamp01(GetFloat(material, "_RoughnessScale", 1f));
            float metallic = metallicScale;
            float smoothness = smoothnessScale;
            float occlusion = GetFloat(material, "_OcclusionStrength", 1f);
            float normalScale = GetFloat(material, "_BumpScale", GetFloat(material, "_NormalScale", 1f));
            if (!material.HasProperty("_BumpScale") && material.HasProperty("_NormalStrength"))
                normalScale *= Mathf.Max(0f, GetFloat(material, "_NormalStrength", 1f));
            float cutoff = GetFloat(material, "_Cutoff", 0.5f);
            float emissionStrength = Mathf.Max(0f, GetFloat(material, "_EmissionStrength", 1f));
            float alphaClipWeight = ResolveAlphaClipWeight(material);
            int targetRenderQueue = ResolveTargetRenderQueue(material, alphaClipWeight);
            MaskSemantics maskSemantics = ResolveMaskSemantics(maskSource, sourceShaderName, hasMask);
            float metallicMapWeight = maskSemantics.MetallicWeight;
            float roughnessMapWeight = maskSemantics.RoughnessWeight;
            ApplyMaskScalarCompatibility(
                maskSemantics,
                metallicScale,
                smoothnessScale,
                roughnessScale,
                ref metallic,
                ref smoothness,
                ref metallicMapWeight,
                ref roughnessMapWeight);
            emissionColor.r *= emissionStrength;
            emissionColor.g *= emissionStrength;
            emissionColor.b *= emissionStrength;
            emissionColor.a = HasVisibleEmission(emissionColor) && emissionStrength > 0.0001f ? maskSemantics.EmissionWeight : 0f;

            Undo.RecordObject(material, "Migrate material to Hecton master lit");
            material.shader = masterShader;
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_EmissionColor", emissionColor);
            material.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            material.SetFloat("_OcclusionStrength", Mathf.Clamp01(occlusion));
            material.SetFloat("_BumpScale", Mathf.Max(0f, normalScale));
            material.SetFloat("_Cutoff", Mathf.Clamp01(cutoff));
            material.SetVector("_MasterSurfaceParams", new Vector4(metallicMapWeight, roughnessMapWeight, maskSemantics.OcclusionWeight, Mathf.Max(0f, normalScale)));
            material.SetVector("_MasterAlphaParams", new Vector4(Mathf.Clamp01(cutoff), 1f, 0.35f, alphaClipWeight));
            material.SetVector("_MasterPomParams", new Vector4(0f, 0f, 0f, 1f));
            material.SetVector("_MasterShadowParams", new Vector4(1f, 0.15f, 0.18f, maskSemantics.Layout));
            ApplySurfaceRouting(material, alphaClipWeight, targetRenderQueue);

            if (hasBase)
                CopyTexture(material, "_BaseMap", baseMap, baseScale, baseOffset);
            if (hasNormal)
                CopyTexture(material, "_BumpMap", normalMap, normalScaleVector, normalOffset);
            if (hasMask)
                CopyTexture(material, "_MaskMap", maskMap, maskScale, maskOffset);

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return true;
        }

        private static bool TryGetTexture(Material material, string[] names, out Texture texture, out string sourceName)
        {
            texture = null;
            sourceName = string.Empty;
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                if (!material.HasProperty(name))
                    continue;

                Texture candidate = material.GetTexture(name);
                if (candidate == null)
                    continue;

                texture = candidate;
                sourceName = name;
                return true;
            }

            return false;
        }

        private static bool HasAssignedTexture(Material material, string[] names)
        {
            for (int index = 0; index < names.Length; index++)
            {
                string name = names[index];
                if (material.HasProperty(name) && material.GetTexture(name) != null)
                    return true;
            }

            return false;
        }

        private static void CopyTexture(Material material, string targetName, Texture texture, Vector2 scale, Vector2 offset)
        {
            material.SetTexture(targetName, texture);
            material.SetTextureScale(targetName, scale);
            material.SetTextureOffset(targetName, offset);
        }

        private static Color GetColor(Material material, string name, Color fallback)
        {
            return material.HasProperty(name) ? material.GetColor(name) : fallback;
        }

        private static float GetFloat(Material material, string name, float fallback)
        {
            return material.HasProperty(name) ? material.GetFloat(name) : fallback;
        }

        private static Vector2 GetTextureScale(Material material, string name)
        {
            return name.Length > 0 && material.HasProperty(name) ? material.GetTextureScale(name) : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string name)
        {
            return name.Length > 0 && material.HasProperty(name) ? material.GetTextureOffset(name) : Vector2.zero;
        }

        private static bool HasVisibleEmission(Color color)
        {
            return color.r > 0.0001f || color.g > 0.0001f || color.b > 0.0001f;
        }

        private static void ApplyMaskScalarCompatibility(
            MaskSemantics maskSemantics,
            float metallicScale,
            float smoothnessScale,
            float roughnessScale,
            ref float metallic,
            ref float smoothness,
            ref float metallicMapWeight,
            ref float roughnessMapWeight)
        {
            if (metallicMapWeight <= 0f && roughnessMapWeight <= 0f)
                return;

            if (maskSemantics.Layout < 0.5f)
            {
                metallic = 0f;
                smoothness = 1f;
                metallicMapWeight *= metallicScale;
                roughnessMapWeight *= roughnessScale;
                return;
            }

            if (maskSemantics.Layout < 2.5f)
            {
                metallic = 0f;
                smoothness = 0f;
                metallicMapWeight *= metallicScale;
                roughnessMapWeight *= smoothnessScale;
                return;
            }

            metallic = 0f;
            metallicMapWeight *= metallicScale;
            roughnessMapWeight *= smoothnessScale;
        }

        private static bool IsUnsupportedMasterSource(Material material, string sourceShaderName)
        {
            string renderType = material.GetTag("RenderType", false, string.Empty);
            string queue = material.GetTag("Queue", false, string.Empty);
            return string.Equals(renderType, "Transparent", StringComparison.OrdinalIgnoreCase) ||
                   queue.StartsWith("Transparent", StringComparison.OrdinalIgnoreCase) ||
                   material.renderQueue >= 3000 ||
                   ContainsAnyFragment(sourceShaderName, UnsupportedSourceShaderNameFragments) ||
                   sourceShaderName.IndexOf("Stencil", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAnyFragment(string source, string[] fragments)
        {
            for (int index = 0; index < fragments.Length; index++)
            {
                if (source.IndexOf(fragments[index], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static float ResolveAlphaClipWeight(Material material)
        {
            string renderType = material.GetTag("RenderType", false, string.Empty);
            string queue = material.GetTag("Queue", false, string.Empty);
            if (string.Equals(renderType, "TransparentCutout", StringComparison.OrdinalIgnoreCase))
                return 1f;
            if (queue.StartsWith("AlphaTest", StringComparison.OrdinalIgnoreCase))
                return 1f;
            if (material.renderQueue >= 2450 && material.renderQueue < 3000)
                return 1f;
            if (GetFloat(material, "_AlphaClip", 0f) > 0.5f)
                return 1f;

            return 0f;
        }

        private static int ResolveTargetRenderQueue(Material material, float alphaClipWeight)
        {
            int sourceQueue = material.renderQueue;
            if (alphaClipWeight > 0.5f)
            {
                if (sourceQueue >= (int)UnityEngine.Rendering.RenderQueue.AlphaTest &&
                    sourceQueue < (int)UnityEngine.Rendering.RenderQueue.Transparent)
                    return sourceQueue;

                return (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }

            if (sourceQueue >= (int)UnityEngine.Rendering.RenderQueue.Geometry &&
                sourceQueue < (int)UnityEngine.Rendering.RenderQueue.AlphaTest)
                return sourceQueue;

            return (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static void ApplySurfaceRouting(Material material, float alphaClipWeight, int targetRenderQueue)
        {
            if (alphaClipWeight > 0.5f)
                material.SetOverrideTag("RenderType", "TransparentCutout");
            else
                material.SetOverrideTag("RenderType", "Opaque");

            material.renderQueue = targetRenderQueue;
        }

        private static MaskSemantics ResolveMaskSemantics(string sourceName, string sourceShaderName, bool hasMask)
        {
            if (!hasMask)
                return new MaskSemantics(0f, 0f, 0f, 0f, 0f);

            if (string.Equals(sourceName, "_MetallicGlossMap", StringComparison.Ordinal))
                return new MaskSemantics(1f, 1f, 0f, 0f, 2f);
            if (string.Equals(sourceName, "_MaskMap", StringComparison.Ordinal) ||
                string.Equals(sourceName, "_Mask_Map", StringComparison.Ordinal))
            {
                if (string.Equals(sourceShaderName, "Hecton8/Rendering/UberNoir", StringComparison.Ordinal))
                    return new MaskSemantics(1f, 1f, 1f, 1f, 3f);

                return new MaskSemantics(1f, 1f, 1f, 1f, 1f);
            }

            return new MaskSemantics(1f, 1f, 1f, 1f, 0f);
        }
    }
}
#endif
