// ============================================================================
// HECTON-8 — VRAMVitalsAuditReport.cs
// Editor-only VRAM and performance vitals audit tool.
//
// Menu: Hecton8 → Audit → VRAM & Vitals Report
//
// Scans the current scene and project for:
//   - Texture memory consumption (per-texture + aggregate)
//   - RenderTexture allocation tracking
//   - Material instance leaks (renderer.material copies)
//   - Shader variant count warnings
//   - RT budget compliance (threshold: 500 MB)
//   - Texture budget compliance (threshold: 900 MB)
//
// OWNERSHIP: Editor tooling only. No runtime code.
// ============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// VRAM &amp; Vitals audit report generator.
    /// Produces a comprehensive log of texture/RT/material memory usage.
    /// </summary>
    public static class VRAMVitalsAuditReport
    {
        // ══════════════════════════════════════════════════════════
        //  CONSTANTS — VRAM RED THRESHOLDS (from AGENTS.md)
        // ══════════════════════════════════════════════════════════

        /// <summary>Texture VRAM RED threshold in MB.</summary>
        private const float TextureRedThresholdMB = 900f;

        /// <summary>RenderTexture VRAM RED threshold in MB.</summary>
        private const float RenderTextureRedThresholdMB = 500f;

        /// <summary>Total VRAM budget in MB.</summary>
        private const float TotalVRAMBudgetMB = 2048f;

        /// <summary>Warning threshold as fraction of RED (80%).</summary>
        private const float WarningFraction = 0.8f;

        // ══════════════════════════════════════════════════════════
        //  MENU COMMANDS
        // ══════════════════════════════════════════════════════════

        [MenuItem("Hecton8/Audit/VRAM and Vitals Report")]
        public static void GenerateReport()
        {
            var sb = new StringBuilder(8192); // COLD ALLOC: editor-only.

            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║         HECTON-8 — VRAM & VITALS AUDIT REPORT          ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // ── Texture Memory ───────────────────────────────────
            AuditTextures(sb);

            // ── RenderTexture Memory ─────────────────────────────
            AuditRenderTextures(sb);

            // ── Material Instance Leaks ──────────────────────────
            AuditMaterialLeaks(sb);

            // ── Shader Variant Count ─────────────────────────────
            AuditShaderVariants(sb);

            // ── Summary ──────────────────────────────────────────
            AppendSummary(sb);

            Debug.Log(sb.ToString());
        }

        [MenuItem("Hecton8/Audit/Quick VRAM Check")]
        public static void QuickVRAMCheck()
        {
            long texBytes = 0;
            long rtBytes = 0;

            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] is RenderTexture rt)
                {
                    rtBytes += CalculateRTMemory(rt);
                }
                else
                {
                    texBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(textures[i]);
                }
            }

            float texMB = texBytes / (1024f * 1024f);
            float rtMB = rtBytes / (1024f * 1024f);
            string texStatus = texMB > TextureRedThresholdMB ? "RED" :
                               texMB > TextureRedThresholdMB * WarningFraction ? "WARN" : "OK";
            string rtStatus = rtMB > RenderTextureRedThresholdMB ? "RED" :
                              rtMB > RenderTextureRedThresholdMB * WarningFraction ? "WARN" : "OK";

            Debug.Log($"[VRAM Quick] Textures: {texMB:F1} MB [{texStatus}] | RT: {rtMB:F1} MB [{rtStatus}] | Total: {(texMB + rtMB):F1} MB / {TotalVRAMBudgetMB:F0} MB");
        }

        // ══════════════════════════════════════════════════════════
        //  AUDIT SECTIONS
        // ══════════════════════════════════════════════════════════

        private static void AuditTextures(StringBuilder sb)
        {
            sb.AppendLine("── TEXTURE MEMORY ──────────────────────────────────────");

            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            long totalBytes = 0;
            int count = 0;

            // Top offenders list (simple insertion sort for top 20).
            const int topN = 20;
            var topNames = new string[topN]; // COLD ALLOC: editor-only.
            var topSizes = new long[topN];   // COLD ALLOC: editor-only.
            var topFormats = new string[topN]; // COLD ALLOC: editor-only.
            int topCount = 0;

            for (int i = 0; i < textures.Length; i++)
            {
                Texture tex = textures[i];
                if (tex is RenderTexture) continue; // Handled separately.
                if (tex == null) continue;

                long size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                totalBytes += size;
                count++;

                // Insert into top-N if qualifies.
                if (topCount < topN || size > topSizes[topCount - 1])
                {
                    int insertAt = topCount < topN ? topCount : topN - 1;
                    for (int j = insertAt; j > 0 && topSizes[j - 1] < size; j--)
                    {
                        topNames[j] = topNames[j - 1];
                        topSizes[j] = topSizes[j - 1];
                        topFormats[j] = topFormats[j - 1];
                        insertAt = j - 1;
                    }
                    topNames[insertAt] = tex.name;
                    topSizes[insertAt] = size;
                    topFormats[insertAt] = tex is Texture2D t2d ? t2d.format.ToString() : tex.GetType().Name;
                    if (topCount < topN) topCount++;
                }
            }

            float totalMB = totalBytes / (1024f * 1024f);
            string status = totalMB > TextureRedThresholdMB ? "RED" :
                            totalMB > TextureRedThresholdMB * WarningFraction ? "WARN" : "OK";

            sb.AppendLine($"  Total: {totalMB:F1} MB across {count} textures [{status}]");
            sb.AppendLine($"  Threshold: {TextureRedThresholdMB:F0} MB (RED) / {TextureRedThresholdMB * WarningFraction:F0} MB (WARN)");
            sb.AppendLine();
            sb.AppendLine("  Top 20 Offenders:");

            for (int i = 0; i < topCount; i++)
            {
                float sizeMB = topSizes[i] / (1024f * 1024f);
                sb.AppendLine($"    {i + 1,2}. {topNames[i],-48} {sizeMB,8:F2} MB  [{topFormats[i]}]");
            }
            sb.AppendLine();
        }

        private static void AuditRenderTextures(StringBuilder sb)
        {
            sb.AppendLine("── RENDERTEXTURE MEMORY ────────────────────────────────");

            var rts = Resources.FindObjectsOfTypeAll<RenderTexture>();
            long totalBytes = 0;
            int count = 0;

            const int topN = 15;
            var topNames = new string[topN]; // COLD ALLOC: editor-only.
            var topSizes = new long[topN];   // COLD ALLOC: editor-only.
            var topDims = new string[topN];  // COLD ALLOC: editor-only.
            int topCount = 0;

            for (int i = 0; i < rts.Length; i++)
            {
                RenderTexture rt = rts[i];
                if (rt == null) continue;

                long size = CalculateRTMemory(rt);
                totalBytes += size;
                count++;

                if (topCount < topN || size > topSizes[topCount - 1])
                {
                    int insertAt = topCount < topN ? topCount : topN - 1;
                    for (int j = insertAt; j > 0 && topSizes[j - 1] < size; j--)
                    {
                        topNames[j] = topNames[j - 1];
                        topSizes[j] = topSizes[j - 1];
                        topDims[j] = topDims[j - 1];
                        insertAt = j - 1;
                    }
                    topNames[insertAt] = rt.name;
                    topSizes[insertAt] = size;
                    topDims[insertAt] = $"{rt.width}x{rt.height} {rt.format} AA{rt.antiAliasing}x depth{rt.depth}";
                    if (topCount < topN) topCount++;
                }
            }

            float totalMB = totalBytes / (1024f * 1024f);
            string status = totalMB > RenderTextureRedThresholdMB ? "RED" :
                            totalMB > RenderTextureRedThresholdMB * WarningFraction ? "WARN" : "OK";

            sb.AppendLine($"  Total: {totalMB:F1} MB across {count} RTs [{status}]");
            sb.AppendLine($"  Threshold: {RenderTextureRedThresholdMB:F0} MB (RED) / {RenderTextureRedThresholdMB * WarningFraction:F0} MB (WARN)");
            sb.AppendLine();
            sb.AppendLine("  Top 15 RTs:");

            for (int i = 0; i < topCount; i++)
            {
                float sizeMB = topSizes[i] / (1024f * 1024f);
                sb.AppendLine($"    {i + 1,2}. {topNames[i],-40} {sizeMB,8:F2} MB  [{topDims[i]}]");
            }
            sb.AppendLine();
        }

        private static void AuditMaterialLeaks(StringBuilder sb)
        {
            sb.AppendLine("── MATERIAL INSTANCE LEAKS ─────────────────────────────");

            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            int leakCount = 0;
            var leakedNames = new List<string>(32); // COLD ALLOC: editor-only.

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;

                // Check for instantiated material copies (name ends with " (Instance)").
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    if (mats[m].name.EndsWith(" (Instance)"))
                    {
                        leakCount++;
                        if (leakedNames.Count < 30) // Cap report entries.
                            leakedNames.Add($"{r.gameObject.name} → {mats[m].name}");
                    }
                }
            }

            sb.AppendLine($"  Detected: {leakCount} material instance(s)");
            if (leakCount > 0)
            {
                sb.AppendLine("  [WARN] These are leaked material copies from renderer.material access.");
                sb.AppendLine("  [FIX]  Use MaterialPropertyBlock + renderer.Get/SetPropertyBlock instead.");
                sb.AppendLine();
                for (int i = 0; i < leakedNames.Count; i++)
                    sb.AppendLine($"    • {leakedNames[i]}");
            }
            sb.AppendLine();
        }

        private static void AuditShaderVariants(StringBuilder sb)
        {
            sb.AppendLine("── SHADER VARIANT COUNT ────────────────────────────────");

            var shaders = Resources.FindObjectsOfTypeAll<Shader>();
            int highVariantCount = 0;
            for (int i = 0; i < shaders.Length; i++)
            {
                Shader shader = shaders[i];
                if (shader == null) continue;
                if (!shader.name.StartsWith("Hecton8/")) continue;

                // ShaderUtil is editor-only.
                int passCount = shader.passCount;
                if (passCount > 6)
                {
                    sb.AppendLine($"  [WARN] {shader.name}: {passCount} passes");
                    highVariantCount++;
                }
            }

            if (highVariantCount == 0)
                sb.AppendLine("  All Hecton8/ shaders within pass budget.");
            sb.AppendLine();
        }

        private static void AppendSummary(StringBuilder sb)
        {
            sb.AppendLine("── SUMMARY ─────────────────────────────────────────────");

            long texBytes = 0;
            long rtBytes = 0;

            var textures = Resources.FindObjectsOfTypeAll<Texture>();
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] is RenderTexture rt)
                    rtBytes += CalculateRTMemory(rt);
                else
                    texBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(textures[i]);
            }

            float texMB = texBytes / (1024f * 1024f);
            float rtMB = rtBytes / (1024f * 1024f);
            float totalVRAM = texMB + rtMB;

            string texStatus = texMB > TextureRedThresholdMB ? "🔴 RED" :
                               texMB > TextureRedThresholdMB * WarningFraction ? "🟡 WARN" : "🟢 OK";
            string rtStatus = rtMB > RenderTextureRedThresholdMB ? "🔴 RED" :
                              rtMB > RenderTextureRedThresholdMB * WarningFraction ? "🟡 WARN" : "🟢 OK";
            string totalStatus = totalVRAM > TotalVRAMBudgetMB ? "🔴 RED" :
                                 totalVRAM > TotalVRAMBudgetMB * WarningFraction ? "🟡 WARN" : "🟢 OK";

            sb.AppendLine($"  Textures:       {texMB,8:F1} MB / {TextureRedThresholdMB,6:F0} MB  {texStatus}");
            sb.AppendLine($"  RenderTextures: {rtMB,8:F1} MB / {RenderTextureRedThresholdMB,6:F0} MB  {rtStatus}");
            sb.AppendLine($"  Total VRAM:     {totalVRAM,8:F1} MB / {TotalVRAMBudgetMB,6:F0} MB  {totalStatus}");
            sb.AppendLine();
            sb.AppendLine("══════════════════════════════════════════════════════════");
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════

        private static long CalculateRTMemory(RenderTexture rt)
        {
            if (rt == null) return 0;

            // Estimate: width × height × bpp × MSAA.
            int bpp = GetBitsPerPixel(rt.format);
            long pixels = (long)rt.width * rt.height;
            long baseBytes = pixels * bpp / 8;
            long msaaFactor = rt.antiAliasing > 1 ? rt.antiAliasing : 1;
            long depthBytes = rt.depth > 0 ? pixels * (rt.depth == 16 ? 2 : 4) : 0;

            return (baseBytes * msaaFactor) + depthBytes;
        }

        private static int GetBitsPerPixel(RenderTextureFormat format)
        {
            switch (format)
            {
                case RenderTextureFormat.ARGB32:
                case RenderTextureFormat.Default:
                case RenderTextureFormat.BGRA32:
                    return 32;
                case RenderTextureFormat.ARGBHalf:
                case RenderTextureFormat.RGB565:
                    return 64;
                case RenderTextureFormat.ARGBFloat:
                    return 128;
                case RenderTextureFormat.RHalf:
                    return 16;
                case RenderTextureFormat.RFloat:
                    return 32;
                case RenderTextureFormat.R8:
                    return 8;
                case RenderTextureFormat.Depth:
                    return 32;
                case RenderTextureFormat.RGHalf:
                    return 32;
                case RenderTextureFormat.RGFloat:
                    return 64;
                default:
                    return 32; // Conservative fallback.
            }
        }
    }
}
#endif
