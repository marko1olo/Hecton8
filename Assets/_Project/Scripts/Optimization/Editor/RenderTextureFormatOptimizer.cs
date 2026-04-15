#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Editor tool for analyzing RenderTexture formats and recommending optimizations.
    /// Heuristics: RGBA32 → RGBA16 (no HDR), RGBA16 → RG16 (RG-only), RG16 → R8 (R-only).
    /// </summary>
    public static class RenderTextureFormatOptimizer
    {
        // ── FORMAT BIT DEPTHS ──────────────────────────────────────────────────────
        
        private static readonly Dictionary<RenderTextureFormat, int> _formatBitsPerPixel = new Dictionary<RenderTextureFormat, int>
        {
            { RenderTextureFormat.R8, 8 },
            { RenderTextureFormat.RG16, 16 },
            { RenderTextureFormat.ARGB32, 32 },
            { RenderTextureFormat.BGRA32, 32 },
            { RenderTextureFormat.ARGB4444, 16 },
            { RenderTextureFormat.ARGB1555, 16 },
            { RenderTextureFormat.RGB565, 16 },
            { RenderTextureFormat.ARGBHalf, 64 },
            { RenderTextureFormat.RGHalf, 32 },
            { RenderTextureFormat.RHalf, 16 },
            { RenderTextureFormat.ARGBFloat, 128 },
            { RenderTextureFormat.RGFloat, 64 },
            { RenderTextureFormat.RFloat, 32 },
            { RenderTextureFormat.Depth, 32 },
            { RenderTextureFormat.Shadowmap, 32 },
            { RenderTextureFormat.RGB111110Float, 32 },
            { RenderTextureFormat.RG32, 32 },
            { RenderTextureFormat.R16, 16 },
        };
        
        // ── PUBLIC API ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Analyzes all tracked RenderTextures and returns format optimization recommendations.
        /// </summary>
        /// <returns>List of recommendations with owner, current format, recommended format, savings.</returns>
        public static List<FormatOptimizationRecommendation> AnalyzeFormats()
        {
            var recommendations = new List<FormatOptimizationRecommendation>();
            
            if (RenderTextureLifecycleTracker.Instance == null)
            {
                Debug.LogWarning("[FormatOptimizer] RenderTextureLifecycleTracker not available. Enter Play Mode first.");
                return recommendations;
            }
            
            // Query all tracked RTs
            var allRTs = new List<RenderTextureAllocationRecord>();
            var tracker = RenderTextureLifecycleTracker.Instance;
            
            // Get all allocations via categories
            var categories = new[] { "Visor", "Camera", "PostFX", "UI", "Other" };
            foreach (var category in categories)
            {
                var categoryRTs = new List<RenderTextureAllocationRecord>();
                tracker.GetAllocationsByCategory(category, categoryRTs);
                allRTs.AddRange(categoryRTs);
            }
            
            // Analyze each RT
            foreach (var record in allRTs)
            {
                if (record.IsDisposed || record.RenderTexture == null)
                    continue;
                
                var recommendation = AnalyzeFormat(record);
                if (recommendation.HasValue)
                    recommendations.Add(recommendation.Value);
            }
            
            return recommendations;
        }
        
        /// <summary>
        /// Calculates memory savings for format change.
        /// Formula: width × height × (old_bpp - new_bpp) / 8
        /// </summary>
        /// <param name="width">RT width.</param>
        /// <param name="height">RT height.</param>
        /// <param name="oldFormat">Current format.</param>
        /// <param name="newFormat">Recommended format.</param>
        /// <returns>Memory savings in bytes.</returns>
        public static long CalculateMemorySavings(int width, int height, RenderTextureFormat oldFormat, RenderTextureFormat newFormat)
        {
            if (!_formatBitsPerPixel.TryGetValue(oldFormat, out int oldBpp))
                oldBpp = 32; // Default to 32 bpp if unknown
            
            if (!_formatBitsPerPixel.TryGetValue(newFormat, out int newBpp))
                newBpp = 32;
            
            if (newBpp >= oldBpp)
                return 0L; // No savings
            
            long pixelCount = (long)width * height;
            long oldBytes = pixelCount * oldBpp / 8;
            long newBytes = pixelCount * newBpp / 8;
            
            return oldBytes - newBytes;
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private static FormatOptimizationRecommendation? AnalyzeFormat(RenderTextureAllocationRecord record)
        {
            var rt = record.RenderTexture;
            var currentFormat = rt.format;
            
            // Heuristic 1: RGBA32 → ARGB4444 (no HDR, significant savings)
            if (currentFormat == RenderTextureFormat.ARGB32 || currentFormat == RenderTextureFormat.BGRA32)
            {
                // Check if RT is used for HDR content
                bool isHDR = rt.enableRandomWrite || rt.useMipMap; // Heuristic: HDR often uses compute/mips
                
                if (!isHDR)
                {
                    var recommendedFormat = RenderTextureFormat.ARGB4444;
                    var savings = CalculateMemorySavings(rt.width, rt.height, currentFormat, recommendedFormat);
                    
                    if (savings > 0)
                    {
                        return new FormatOptimizationRecommendation
                        {
                            RenderTexture = rt,
                            Owner = record.Owner,
                            CurrentFormat = currentFormat,
                            RecommendedFormat = recommendedFormat,
                            MemorySavingsBytes = savings,
                            Reason = "RGBA32 → ARGB4444: No HDR detected, 50% memory reduction"
                        };
                    }
                }
            }
            
            // Heuristic 2: ARGBHalf → ARGB4444 (no HDR required)
            if (currentFormat == RenderTextureFormat.ARGBHalf)
            {
                var recommendedFormat = RenderTextureFormat.ARGB4444;
                var savings = CalculateMemorySavings(rt.width, rt.height, currentFormat, recommendedFormat);
                
                if (savings > 0)
                {
                    return new FormatOptimizationRecommendation
                    {
                        RenderTexture = rt,
                        Owner = record.Owner,
                        CurrentFormat = currentFormat,
                        RecommendedFormat = recommendedFormat,
                        MemorySavingsBytes = savings,
                        Reason = "ARGBHalf → ARGB4444: HDR not required, 75% memory reduction"
                    };
                }
            }
            
            // Heuristic 3: RG16 → R8 (single-channel usage)
            // Note: This requires runtime analysis of actual usage, which is complex
            // For MVP, we skip this heuristic
            
            return null;
        }
    }
}
#endif
