using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Rendering.Editor
{
    public static class HectonUberNoirMaterialConsolidator
    {
        private const string TargetShaderName = "Hecton8/Rendering/UberNoir";
        private const string DryZoneShaderName = "Hecton8/Environment/Hecton_DryZoneLit";
        private const string RuinSeepShaderName = "HECTON/Environment/RuinSeepSheen";
        private const string WetGlassShaderName = "Triplebrick/Glass";
        private const string ToolDecayShaderName = "Hecton8/Tools/DecayLit";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string CausticsKeyword = "H8_UBERNOIR_CAUSTICS_TEXTURED";
        private const string RefractionKeyword = "H8_UBERNOIR_SCREEN_REFRACTION";
        private const string ConstructionSearchRoot = "Assets/_Project/Art/Materials/Construction";
        private const string ToolsSearchRoot = "Assets/_Project/Art/Materials/Tools";
        private const string ReportPath = "Docs/AgentLogs/UberNoirMaterialConsolidationReport.md";

        private static readonly string[] SearchRoots = { ConstructionSearchRoot, ToolsSearchRoot };
        private static readonly SourceShaderSpec[] SourceShaders =
        {
            new SourceShaderSpec(DryZoneShaderName, ProjectionKind.DryZoneHardSurface),
            new SourceShaderSpec(RuinSeepShaderName, ProjectionKind.RuinSeepSheen),
            new SourceShaderSpec(WetGlassShaderName, ProjectionKind.WetGlassSheen),
            new SourceShaderSpec(ToolDecayShaderName, ProjectionKind.ToolDecaySurface),
            new SourceShaderSpec(UrpLitShaderName, ProjectionKind.UrpLitOpaqueConstructionSurface)
        };

        [MenuItem("Hecton8/Rendering/Consolidate Hard-Surface Materials To UberNoir")]
        public static void ConsolidateProjectMaterials()
        {
            int converted = ConsolidateProjectMaterialsInternal();
            Debug.Log($"[UberNoir] Consolidated {converted} hard-surface materials into {TargetShaderName}.");
        }

        private static int ConsolidateProjectMaterialsInternal()
        {
            Shader targetShader = Shader.Find(TargetShaderName);
            if (targetShader == null)
                throw new InvalidOperationException($"Target shader not found: {TargetShaderName}");

            string[] guids = AssetDatabase.FindAssets("t:Material", SearchRoots);
            StringBuilder report = new StringBuilder(1024);
            report.AppendLine("# UberNoir Material Consolidation Report");
            report.AppendLine();
            report.AppendLine($"Target shader: `{TargetShaderName}`");
            report.AppendLine("Source shaders:");
            for (int i = 0; i < SourceShaders.Length; i++)
                report.AppendLine($"- `{SourceShaders[i].ShaderName}`");

            report.AppendLine("Search roots:");
            for (int i = 0; i < SearchRoots.Length; i++)
                report.AppendLine($"- `{SearchRoots[i]}`");

            report.AppendLine();

            int converted = 0;
            int skipped = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || !TryGetSourceSpec(material.shader.name, out SourceShaderSpec sourceSpec))
                        continue;

                    if (!ShouldConvertMaterial(path, material, sourceSpec.Kind, report))
                    {
                        skipped++;
                        continue;
                    }

                    int previousPassCount = material.passCount;
                    MaterialSnapshot snapshot = MaterialSnapshot.Capture(material, sourceSpec.Kind);
                    material.shader = targetShader;
                    snapshot.Apply(material);
                    EditorUtility.SetDirty(material);
                    converted++;
                    report.AppendLine($"- `{path}`: {sourceSpec.ShaderName} ({previousPassCount} passes) -> {TargetShaderName} ({material.passCount} passes)");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            WriteReport(report, converted, skipped);
            return converted;
        }

        private static void WriteReport(StringBuilder report, int converted, int skipped)
        {
            report.AppendLine();
            report.AppendLine($"Converted materials: {converted}");
            report.AppendLine($"Skipped materials: {skipped}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Docs/AgentLogs");
            File.WriteAllText(ReportPath, report.ToString());
        }

        private static bool TryGetSourceSpec(string shaderName, out SourceShaderSpec spec)
        {
            for (int i = 0; i < SourceShaders.Length; i++)
            {
                if (string.Equals(SourceShaders[i].ShaderName, shaderName, StringComparison.Ordinal))
                {
                    spec = SourceShaders[i];
                    return true;
                }
            }

            spec = default;
            return false;
        }

        private static bool ShouldConvertMaterial(string path, Material material, ProjectionKind kind, StringBuilder report)
        {
            if (kind != ProjectionKind.UrpLitOpaqueConstructionSurface)
                return true;

            if (IsOpaqueMaterial(material))
                return true;

            report.AppendLine($"- `{path}`: skipped {UrpLitShaderName} because transparent preview materials require blend semantics outside UberNoir.");
            return false;
        }

        private static bool IsOpaqueMaterial(Material material)
        {
            if (material.renderQueue >= 3000)
                return false;

            if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
                return false;

            if (material.HasProperty("_Blend") && material.GetFloat("_Blend") > 0.5f)
                return false;

            if (!HasOpaqueColorAlpha(material))
                return false;

            string renderType = material.GetTag("RenderType", false, string.Empty);
            return !string.Equals(renderType, "Transparent", StringComparison.Ordinal);
        }

        private static bool HasOpaqueColorAlpha(Material material)
        {
            Color baseColor = ReadColor(material, Color.white, "_BaseColor", "_Color");
            return baseColor.a >= 0.995f;
        }

        private enum ProjectionKind
        {
            DryZoneHardSurface,
            RuinSeepSheen,
            WetGlassSheen,
            ToolDecaySurface,
            UrpLitOpaqueConstructionSurface
        }

        private readonly struct SourceShaderSpec
        {
            public readonly string ShaderName;
            public readonly ProjectionKind Kind;

            public SourceShaderSpec(string shaderName, ProjectionKind kind)
            {
                ShaderName = shaderName;
                Kind = kind;
            }
        }

        private struct MaterialSnapshot
        {
            public ProjectionKind Kind;
            public Texture BaseMap;
            public Vector2 BaseMapScale;
            public Vector2 BaseMapOffset;
            public Texture MaskMap;
            public Texture BumpMap;
            public Color BaseColor;
            public Color EmissionColor;
            public Color RustTint;
            public float Metallic;
            public float Smoothness;
            public float Occlusion;
            public float BumpScale;
            public float Cutoff;
            public float EnvironmentalWear;
            public float Opacity;
            public float RefractionStrength;
            public float FresnelStrength;
            public float FlowAmount;

            public static MaterialSnapshot Capture(Material material, ProjectionKind kind)
            {
                MaterialSnapshot snapshot = new MaterialSnapshot
                {
                    Kind = kind,
                    BaseMap = ReadTexture(material, "_BaseMap", "_MainTex", "_RoughnessDirt"),
                    MaskMap = ReadTexture(material, "_MaskMap", "_MetallicGlossMap", "_OcclusionMap", "_RoughnessDirt", "_MainTex"),
                    BumpMap = ReadTexture(material, "_BumpMap", "_NormalMap", "_Normal", "_HectonMicroNormalTex"),
                    BaseColor = ReadColor(material, Color.white, "_BaseColor", "_Color", "_TintColor"),
                    EmissionColor = ReadColor(material, Color.black, "_EmissionColor", "_HighlightColor"),
                    RustTint = ReadColor(material, new Color(0.45f, 0.24f, 0.10f, 1f), "_RustSaltColor", "_TintColor"),
                    Metallic = ReadFloat(material, 0f, "_Metallic", "_ParasiteOverlayMetallic"),
                    Smoothness = ReadFloat(material, 0.5f, "_Smoothness", "_Glossiness"),
                    Occlusion = ReadFloat(material, 1f, "_OcclusionStrength"),
                    BumpScale = ReadFloat(material, 1f, "_BumpScale", "_MicroNormalStrength", "_NormalStrength"),
                    Cutoff = ReadFloat(material, 0.5f, "_Cutoff", "_AlphaCutoff"),
                    EnvironmentalWear = ReadFloat(material, 0f, "_EnvironmentalWear", "_Opacity"),
                    Opacity = ReadFloat(material, 1f, "_Opacity"),
                    RefractionStrength = ReadFloat(material, 0f, "_Refraction", "_WaterlineRefractionStrength"),
                    FresnelStrength = ReadFloat(material, 0f, "_FresnelStrength"),
                    FlowAmount = ReadFloat(material, 0f, "_FlowAmount")
                };

                ReadTextureTransform(material, "_BaseMap", "_MainTex", "_RoughnessDirt", out snapshot.BaseMapScale, out snapshot.BaseMapOffset);
                snapshot.ProjectSourceSpecificDefaults();
                return snapshot;
            }

            private void ProjectSourceSpecificDefaults()
            {
                if (Kind == ProjectionKind.RuinSeepSheen)
                {
                    BaseColor.a = Mathf.Clamp01(BaseColor.a * Mathf.Max(Opacity, 0.2f));
                    Metallic = 0f;
                    Smoothness = Mathf.Clamp01(0.82f + FresnelStrength * 0.06f);
                    EnvironmentalWear = Mathf.Clamp01(0.35f + FlowAmount * 0.45f);
                    Cutoff = Mathf.Clamp(Cutoff, 0.28f, 0.62f);
                    RefractionStrength = Mathf.Max(RefractionStrength, 0.018f);
                    return;
                }

                if (Kind == ProjectionKind.WetGlassSheen)
                {
                    BaseColor.a = Mathf.Clamp01(BaseColor.a * Mathf.Max(Opacity, 0.35f));
                    Metallic = 0f;
                    Smoothness = Mathf.Clamp01(Mathf.Max(Smoothness, 0.86f));
                    EnvironmentalWear = Mathf.Clamp01(Mathf.Max(EnvironmentalWear, 0.58f));
                    Cutoff = Mathf.Clamp(Cutoff, 0.32f, 0.58f);
                    RefractionStrength = Mathf.Max(RefractionStrength, 0.055f);
                    return;
                }

                if (Kind == ProjectionKind.ToolDecaySurface)
                {
                    EnvironmentalWear = Mathf.Clamp01(Mathf.Max(EnvironmentalWear, 0.44f));
                    Cutoff = Mathf.Clamp(Cutoff, 0.35f, 0.75f);
                    Smoothness = Mathf.Clamp01(Mathf.Max(Smoothness, 0.5f));
                    return;
                }

                if (Kind == ProjectionKind.UrpLitOpaqueConstructionSurface)
                {
                    EnvironmentalWear = Mathf.Clamp01(Mathf.Max(EnvironmentalWear, 0.24f));
                    Cutoff = Mathf.Clamp(Cutoff, 0.42f, 0.75f);
                    Smoothness = Mathf.Clamp01(Mathf.Max(Smoothness, 0.42f));
                    RefractionStrength = 0f;
                }
            }

            public void Apply(Material material)
            {
                SetTexture(material, "_BaseMap", BaseMap, BaseMapScale, BaseMapOffset);
                SetTexture(material, "_MaskMap", MaskMap, Vector2.one, Vector2.zero);
                SetTexture(material, "_BumpMap", BumpMap, Vector2.one, Vector2.zero);
                SetColor(material, "_BaseColor", BaseColor);
                SetColor(material, "_EmissionColor", EmissionColor);
                SetColor(material, "_RustTint", RustTint);
                SetColor(material, "_RustPitTint", Color.Lerp(RustTint, Color.black, 0.72f));
                SetFloat(material, "_Metallic", Metallic);
                SetFloat(material, "_Smoothness", Smoothness);
                SetFloat(material, "_OcclusionStrength", Occlusion);
                SetFloat(material, "_BumpScale", Mathf.Clamp(BumpScale, 0f, 2f));
                SetFloat(material, "_Cutoff", Cutoff);
                SetFloat(material, "_NoirFogAlpha", 0.62f);
                SetVector(material, "_UberNoirFeatureFlags", ResolveFeatureFlags());
                SetVector(material, "_UberNoirRustParams", ResolveRustParams());
                SetVector(material, "_UberNoirCausticParams", ResolveCausticParams());
                SetVector(material, "_UberNoirBiolumParams", new Vector4(1f, 0.35f, 4f, 1f));
                SetVector(material, "_UberNoirDitherParams", ResolveDitherParams());
                SetVector(material, "_UberNoirLightingParams", ResolveLightingParams());
                SetVector(material, "_UberNoirRefractionParams", ResolveRefractionParams());
                SetKeyword(material, CausticsKeyword, Kind != ProjectionKind.WetGlassSheen);
                SetKeyword(material, RefractionKeyword, RefractionStrength > 0.0001f);
                material.enableInstancing = true;
            }

            private Vector4 ResolveFeatureFlags()
            {
                float pom = Kind == ProjectionKind.WetGlassSheen ? 0f : 1f;
                float caustics = 1f;
                float bending = Kind == ProjectionKind.DryZoneHardSurface || Kind == ProjectionKind.WetGlassSheen ? 1f : 0f;
                float dither = BaseColor.a < 0.995f || Kind == ProjectionKind.RuinSeepSheen || Kind == ProjectionKind.WetGlassSheen ? 1f : 0f;
                return new Vector4(pom, caustics, bending, dither);
            }

            private Vector4 ResolveRustParams()
            {
                float strength = Mathf.Max(0.2f, EnvironmentalWear);
                float pomThreshold = Kind == ProjectionKind.WetGlassSheen ? 0.9f : 0.3f;
                float normalStrength = Kind == ProjectionKind.RuinSeepSheen ? 0.35f : 0.65f;
                if (Kind == ProjectionKind.ToolDecaySurface)
                    normalStrength = 0.78f;
                else if (Kind == ProjectionKind.UrpLitOpaqueConstructionSurface)
                    normalStrength = 0.52f;

                return new Vector4(strength, pomThreshold, normalStrength, Mathf.Max(0.45f, Smoothness));
            }

            private Vector4 ResolveCausticParams()
            {
                float intensity = Kind == ProjectionKind.WetGlassSheen ? 0.12f : Kind == ProjectionKind.ToolDecaySurface || Kind == ProjectionKind.UrpLitOpaqueConstructionSurface ? 0.22f : 0.35f;
                float refractionOffset = Kind == ProjectionKind.WetGlassSheen ? 0.045f : 0.025f;
                return new Vector4(intensity, 30f, 1f, refractionOffset);
            }

            private Vector4 ResolveDitherParams()
            {
                float alphaScale = Mathf.Clamp01(Kind == ProjectionKind.DryZoneHardSurface ? 1f : Mathf.Max(BaseColor.a, 0.35f));
                return new Vector4(Cutoff, 0.62f, 1f, alphaScale);
            }

            private Vector4 ResolveLightingParams()
            {
                float specular = Kind == ProjectionKind.WetGlassSheen ? 0.72f : Kind == ProjectionKind.ToolDecaySurface ? 0.42f : Kind == ProjectionKind.UrpLitOpaqueConstructionSurface ? 0.30f : 0.35f;
                float roughnessFloor = Kind == ProjectionKind.WetGlassSheen ? 0.02f : Kind == ProjectionKind.ToolDecaySurface ? 0.06f : 0.08f;
                return new Vector4(specular, roughnessFloor, 0.35f, 1f);
            }

            private Vector4 ResolveRefractionParams()
            {
                if (RefractionStrength <= 0.0001f)
                    return Vector4.zero;

                float blend = Kind == ProjectionKind.WetGlassSheen ? 0.52f : 0.24f;
                float chromatic = Kind == ProjectionKind.WetGlassSheen ? 0.12f : 0.04f;
                return new Vector4(Mathf.Clamp(RefractionStrength, 0f, 0.12f), 0.5f, blend, chromatic);
            }
        }

        private static Texture ReadTexture(Material material, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (material.HasProperty(names[i]))
                    return material.GetTexture(names[i]);
            }

            return null;
        }

        private static Color ReadColor(Material material, Color fallback, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (material.HasProperty(names[i]))
                    return material.GetColor(names[i]);
            }

            return fallback;
        }

        private static float ReadFloat(Material material, float fallback, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (material.HasProperty(names[i]))
                    return material.GetFloat(names[i]);
            }

            return fallback;
        }

        private static void ReadTextureTransform(Material material, string primaryName, string fallbackName, string secondFallbackName, out Vector2 scale, out Vector2 offset)
        {
            string name = material.HasProperty(primaryName) ? primaryName : material.HasProperty(fallbackName) ? fallbackName : secondFallbackName;
            if (material.HasProperty(name))
            {
                scale = material.GetTextureScale(name);
                offset = material.GetTextureOffset(name);
                return;
            }

            scale = Vector2.one;
            offset = Vector2.zero;
        }

        private static void SetTexture(Material material, string name, Texture texture, Vector2 scale, Vector2 offset)
        {
            if (!material.HasProperty(name))
                return;

            material.SetTexture(name, texture);
            material.SetTextureScale(name, scale);
            material.SetTextureOffset(name, offset);
        }

        private static void SetColor(Material material, string name, Color value)
        {
            if (material.HasProperty(name))
                material.SetColor(name, value);
        }

        private static void SetFloat(Material material, string name, float value)
        {
            if (material.HasProperty(name))
                material.SetFloat(name, value);
        }

        private static void SetVector(Material material, string name, Vector4 value)
        {
            if (material.HasProperty(name))
                material.SetVector(name, value);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }
    }
}
