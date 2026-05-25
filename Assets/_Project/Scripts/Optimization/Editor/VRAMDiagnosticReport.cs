#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core.Contracts;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Generates comprehensive VRAM diagnostic reports for analysis and debugging.
    /// Exports to markdown format with timestamp.
    /// </summary>
    public static class VRAMDiagnosticReport
    {
        private const string ReportDirectory = "Assets/_Project/Optimization/Reports/";
        
        [MenuItem("Hecton8/Optimization/Generate VRAM Diagnostic Report")]
        public static void GenerateReport()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Error", "Enter Play Mode to generate diagnostic report.", "OK");
                return;
            }
            
            if (Hecton8.Core.GlobalRegistry.VRAMMonitor == null || Hecton8.Core.GlobalRegistry.RenderTextureLifecycleService == null)
            {
                EditorUtility.DisplayDialog("Error", "VRAM Optimization System not initialized.", "OK");
                return;
            }
            
            // Generate report
            var report = new StringBuilder(8192);
            
            AppendHeader(report);
            AppendVRAMSummary(report);
            AppendSubsystemBreakdown(report);
            AppendPoolStatistics(report);
            AppendLifecycleAudit(report);
            AppendFormatRecommendations(report);
            AppendResolutionRecommendations(report);
            AppendFooter(report);
            
            // Save to file
            string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = $"VRAM_Diagnostic_Report_{timestamp}.md";
            string filepath = Path.Combine(ReportDirectory, filename);
            
            // Ensure directory exists
            if (!Directory.Exists(ReportDirectory))
            {
                Directory.CreateDirectory(ReportDirectory);
            }
            
            File.WriteAllText(filepath, report.ToString());
            
            // Refresh AssetDatabase
            AssetDatabase.Refresh();
            
            // Ping file in Project window
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(filepath);
            EditorGUIUtility.PingObject(asset);
            
            Debug.Log($"[VRAMDiagnostic] Report generated: {filepath}");
            
            EditorUtility.DisplayDialog("Success", $"Diagnostic report generated:\n{filename}", "OK");
        }
        
        private static void AppendHeader(StringBuilder report)
        {
            report.AppendLine("# VRAM Diagnostic Report");
            report.AppendLine();
            report.AppendLine($"**Generated:** {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"**Unity Version:** {Application.unityVersion}");
            report.AppendLine($"**Platform:** {Application.platform}");
            report.AppendLine($"**Graphics Device:** {SystemInfo.graphicsDeviceName}");
            report.AppendLine($"**Graphics Memory:** {SystemInfo.graphicsMemorySize} MB");
            report.AppendLine($"**System Memory:** {SystemInfo.systemMemorySize} MB");
            report.AppendLine();
            report.AppendLine("---");
            report.AppendLine();
        }
        
        private static void AppendVRAMSummary(StringBuilder report)
        {
            report.AppendLine("## VRAM Summary");
            report.AppendLine();
            
            var monitor = Hecton8.Core.GlobalRegistry.VRAMMonitor;
            monitor.GetVRAMBreakdown(out long textureMB, out long renderTextureMB, out long totalVRAMMB);
            
            float texturePercent = (textureMB / (float)monitor.TextureMemoryBytes) * 100f;
            float rtPercent = (renderTextureMB / (float)monitor.RenderTextureMemoryBytes) * 100f;
            float totalPercent = (totalVRAMMB / (float)monitor.TotalVRAMBytes) * 100f;
            
            report.AppendLine("| Category | Current | Budget | Status |");
            report.AppendLine("|----------|---------|--------|--------|");
            report.AppendLine($"| Texture Memory | {textureMB / (1024f * 1024f):F2} MB | 900 MB | {(monitor.IsTextureMemoryOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            report.AppendLine($"| RenderTexture Memory | {renderTextureMB / (1024f * 1024f):F2} MB | 500 MB | {(monitor.IsRenderTextureMemoryOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            report.AppendLine($"| Total VRAM | {totalVRAMMB / (1024f * 1024f):F2} MB | 1200 MB | {(monitor.IsTotalVRAMOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            report.AppendLine();
        }
        
        private static void AppendSubsystemBreakdown(StringBuilder report)
        {
            report.AppendLine("## Subsystem Breakdown");
            report.AppendLine();
            
            report.AppendLine("| Subsystem | Current | Budget | Status |");
            report.AppendLine("|-----------|---------|--------|--------|");
            
            VisorRTManager visor = Hecton8.Core.GlobalRegistry.VisorRT;
            if (visor != null)
            {
                report.AppendLine($"| Visor | {visor.VisorRTMemoryBytes / (1024f * 1024f):F2} MB | 64 MB | {(visor.IsOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            }
            
            CameraRTManager camera = Hecton8.Core.GlobalRegistry.CameraRT;
            if (camera != null)
            {
                report.AppendLine($"| Camera | {camera.CameraRTMemoryBytes / (1024f * 1024f):F2} MB | 256 MB | {(camera.IsOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            }
            
            PostFXRTManager postfx = Hecton8.Core.GlobalRegistry.PostFXRT;
            if (postfx != null)
            {
                report.AppendLine($"| PostFX | {postfx.PostFXRTMemoryBytes / (1024f * 1024f):F2} MB | 128 MB | {(postfx.IsOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            }
            
            UIRTManager ui = Hecton8.Core.GlobalRegistry.UIRT;
            if (ui != null)
            {
                report.AppendLine($"| UI | {ui.UIRTMemoryBytes / (1024f * 1024f):F2} MB | 64 MB | {(ui.IsOverBudget ? "⚠️ OVER BUDGET" : "✅ OK")} |");
            }
            
            report.AppendLine();
        }
        
        private static void AppendPoolStatistics(StringBuilder report)
        {
            report.AppendLine("## RenderTexture Pool Statistics");
            report.AppendLine();
            
            if (Hecton8.Core.GlobalRegistry.RenderTexturePoolService != null)
            {
                IRenderTexturePoolService pool = Hecton8.Core.GlobalRegistry.RenderTexturePoolService;
                
                report.AppendLine($"**Hit Rate:** {pool.PoolHitRate * 100f:F1}%");
                report.AppendLine($"**Total Pooled RTs:** {pool.TotalPooledCount}");
                report.AppendLine();
                
                if (pool.PoolHitRate < 0.5f)
                {
                    report.AppendLine("⚠️ **WARNING:** Pool hit rate < 50%. Consider:");
                    report.AppendLine("- Increasing pool capacity per format (currently 16)");
                    report.AppendLine("- Standardizing RT sizes (prefer powers of 2: 512, 1024, 2048)");
                    report.AppendLine();
                }
            }
        }
        
        private static void AppendLifecycleAudit(StringBuilder report)
        {
            report.AppendLine("## RenderTexture Lifecycle Audit");
            report.AppendLine();
            
            IRenderTextureLifecycleService tracker = Hecton8.Core.GlobalRegistry.RenderTextureLifecycleService;
            
            report.AppendLine($"**Tracked RenderTextures:** {tracker.TrackedRenderTextureCount}");
            report.AppendLine($"**Total Memory:** {tracker.TrackedRenderTextureMemoryBytes / (1024f * 1024f):F2} MB");
            report.AppendLine();
            
            // Generate audit report
            var auditBuilder = new StringBuilder(4096);
            tracker.GenerateAuditReport(auditBuilder);
            report.Append(auditBuilder.ToString());
            report.AppendLine();
        }
        
        private static void AppendFormatRecommendations(StringBuilder report)
        {
            report.AppendLine("## Format Optimization Recommendations");
            report.AppendLine();
            
            var recommendations = RenderTextureFormatOptimizer.AnalyzeFormats();
            
            if (recommendations.Count == 0)
            {
                report.AppendLine("✅ No format optimization recommendations.");
                report.AppendLine();
                return;
            }
            
            long totalSavings = 0L;
            for (int i = 0; i < recommendations.Count; i++)
                totalSavings += recommendations[i].MemorySavingsBytes;
            
            report.AppendLine($"**Total Recommendations:** {recommendations.Count}");
            report.AppendLine($"**Total Potential Savings:** {totalSavings / (1024f * 1024f):F2} MB");
            report.AppendLine();
            
            report.AppendLine("| RT Name | Owner | Current Format | Recommended Format | Savings |");
            report.AppendLine("|---------|-------|----------------|-------------------|---------|");
            
            for (int i = 0; i < recommendations.Count; i++)
            {
                FormatOptimizationRecommendation rec = recommendations[i];
                report.AppendLine($"| {rec.RenderTexture.name} | {(rec.Owner != null ? rec.Owner.name : "NULL")} | {rec.CurrentFormat} | {rec.RecommendedFormat} | {rec.MemorySavingsBytes / (1024f * 1024f):F2} MB |");
            }
            
            report.AppendLine();
        }
        
        private static void AppendResolutionRecommendations(StringBuilder report)
        {
            report.AppendLine("## Resolution Optimization Recommendations");
            report.AppendLine();
            
            var recommendations = RenderTextureResolutionAnalyzer.AnalyzeResolutions();
            
            if (recommendations.Count == 0)
            {
                report.AppendLine("✅ No resolution optimization recommendations.");
                report.AppendLine();
                return;
            }
            
            long totalSavings = 0L;
            for (int i = 0; i < recommendations.Count; i++)
                totalSavings += recommendations[i].MemorySavingsBytes;
            
            report.AppendLine($"**Total Recommendations:** {recommendations.Count}");
            report.AppendLine($"**Total Potential Savings:** {totalSavings / (1024f * 1024f):F2} MB");
            report.AppendLine();
            
            report.AppendLine("| RT Name | Owner | Current Resolution | Recommended Resolution | Scale | RMSE | Savings | Priority |");
            report.AppendLine("|---------|-------|-------------------|----------------------|-------|------|---------|----------|");
            
            for (int i = 0; i < recommendations.Count; i++)
            {
                ResolutionOptimizationRecommendation rec = recommendations[i];
                report.AppendLine($"| {rec.RenderTexture.name} | {(rec.Owner != null ? rec.Owner.name : "NULL")} | {rec.CurrentWidth}x{rec.CurrentHeight} | {rec.RecommendedWidth}x{rec.RecommendedHeight} | {rec.Scale:F2}x | {rec.RMSE:F1}% | {rec.MemorySavingsBytes / (1024f * 1024f):F2} MB | {rec.Priority} |");
            }
            
            report.AppendLine();
        }
        
        private static void AppendFooter(StringBuilder report)
        {
            report.AppendLine("---");
            report.AppendLine();
            report.AppendLine("**Report generated by VRAM Optimization System**");
            report.AppendLine();
            report.AppendLine("For more information, see:");
            report.AppendLine("- `Assets/_Project/Scripts/Optimization/README.md`");
            report.AppendLine("- `Assets/_Project/Scripts/Optimization/ARCHITECTURE.md`");
            report.AppendLine("- `Assets/_Project/Scripts/Optimization/INTEGRATION_VERIFICATION.md`");
        }
    }
}
#endif
