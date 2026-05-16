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
        private const string SearchRoot = "Assets/_Project/Art/Materials/Construction";
        private const string ReportPath = "Docs/AgentLogs/UberNoirMaterialConsolidationReport.md";

        private static readonly string[] SearchRoots = { SearchRoot };

        [MenuItem("Hecton8/Rendering/Consolidate DryZone Materials To UberNoir")]
        public static void ConsolidateProjectMaterials()
        {
            int converted = ConsolidateProjectMaterialsInternal();
            Debug.Log($"[UberNoir] Consolidated {converted} DryZone hard-surface materials into {TargetShaderName}.");
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
            report.AppendLine($"Source shader: `{DryZoneShaderName}`");
            report.AppendLine($"Search root: `{SearchRoot}`");
            report.AppendLine();

            int converted = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || material.shader.name != DryZoneShaderName)
                        continue;

                    int previousPassCount = material.passCount;
                    MaterialSnapshot snapshot = MaterialSnapshot.Capture(material);
                    material.shader = targetShader;
                    snapshot.Apply(material);
                    EditorUtility.SetDirty(material);
                    converted++;
                    report.AppendLine($"- `{path}`: {DryZoneShaderName} ({previousPassCount} passes) -> {TargetShaderName} ({material.passCount} pass)");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            WriteReport(report, converted);
            return converted;
        }

        private static void WriteReport(StringBuilder report, int converted)
        {
            report.AppendLine();
            report.AppendLine($"Converted materials: {converted}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Docs/AgentLogs");
            File.WriteAllText(ReportPath, report.ToString());
        }

        private struct MaterialSnapshot
        {
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

            public static MaterialSnapshot Capture(Material material)
            {
                MaterialSnapshot snapshot = new MaterialSnapshot
                {
                    BaseMap = ReadTexture(material, "_BaseMap", "_MainTex"),
                    MaskMap = ReadTexture(material, "_MaskMap", "_MetallicGlossMap", "_OcclusionMap"),
                    BumpMap = ReadTexture(material, "_BumpMap", "_NormalMap"),
                    BaseColor = ReadColor(material, Color.white, "_BaseColor", "_Color"),
                    EmissionColor = ReadColor(material, Color.black, "_EmissionColor"),
                    RustTint = ReadColor(material, new Color(0.45f, 0.24f, 0.10f, 1f), "_RustSaltColor"),
                    Metallic = ReadFloat(material, 0f, "_Metallic"),
                    Smoothness = ReadFloat(material, 0.5f, "_Smoothness", "_Glossiness"),
                    Occlusion = ReadFloat(material, 1f, "_OcclusionStrength"),
                    BumpScale = ReadFloat(material, 1f, "_BumpScale", "_MicroNormalStrength"),
                    Cutoff = ReadFloat(material, 0.5f, "_Cutoff"),
                    EnvironmentalWear = ReadFloat(material, 0f, "_EnvironmentalWear")
                };

                ReadTextureTransform(material, "_BaseMap", "_MainTex", out snapshot.BaseMapScale, out snapshot.BaseMapOffset);
                return snapshot;
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
                SetVector(material, "_UberNoirFeatureFlags", new Vector4(1f, 1f, 1f, 1f));
                SetVector(material, "_UberNoirRustParams", new Vector4(Mathf.Max(0.2f, EnvironmentalWear), 0.3f, 0.65f, Mathf.Max(0.45f, Smoothness)));
                SetVector(material, "_UberNoirCausticParams", new Vector4(0.35f, 30f, 1f, 0.025f));
                SetVector(material, "_UberNoirBiolumParams", new Vector4(1f, 0.35f, 4f, 1f));
                SetVector(material, "_UberNoirDitherParams", new Vector4(Cutoff, 0.62f, 1f, 1f));
                SetVector(material, "_UberNoirLightingParams", new Vector4(0.35f, 0.08f, 0.35f, 1f));
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

        private static void ReadTextureTransform(Material material, string primaryName, string fallbackName, out Vector2 scale, out Vector2 offset)
        {
            string name = material.HasProperty(primaryName) ? primaryName : fallbackName;
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
    }
}
