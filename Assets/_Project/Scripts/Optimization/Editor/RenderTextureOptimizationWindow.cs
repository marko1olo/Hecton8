#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Editor window for RenderTexture optimization recommendations.
    /// Displays format and resolution optimization suggestions with estimated savings.
    /// </summary>
    public class RenderTextureOptimizationWindow : EditorWindow
    {
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private Vector2 _scrollPosition;
        private List<FormatOptimizationRecommendation> _formatRecommendations;
        private List<ResolutionOptimizationRecommendation> _resolutionRecommendations;
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Format Optimization", "Resolution Optimization" };
        
        // ── MENU ITEMS ─────────────────────────────────────────────────────────────
        
        [MenuItem("Hecton8/Optimization/Analyze RT Formats")]
        public static void AnalyzeFormats()
        {
            var window = GetWindow<RenderTextureOptimizationWindow>("RT Optimization");
            window.minSize = new Vector2(800f, 600f);
            window._selectedTab = 0;
            window.RefreshFormatRecommendations();
            window.Show();
        }
        
        [MenuItem("Hecton8/Optimization/Analyze RT Resolutions")]
        public static void AnalyzeResolutions()
        {
            var window = GetWindow<RenderTextureOptimizationWindow>("RT Optimization");
            window.minSize = new Vector2(800f, 600f);
            window._selectedTab = 1;
            window.RefreshResolutionRecommendations();
            window.Show();
        }
        
        // ── GUI ────────────────────────────────────────────────────────────────────
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            
            // Header
            EditorGUILayout.LabelField("RenderTexture Optimization", EditorStyles.boldLabel);
            EditorGUILayout.Space(5f);
            
            // Check if in Play Mode
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to analyze RenderTextures.", MessageType.Info);
                return;
            }
            
            // Tabs
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(10f);
            
            // Refresh button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Analysis", GUILayout.Width(150f)))
            {
                if (_selectedTab == 0)
                    RefreshFormatRecommendations();
                else
                    RefreshResolutionRecommendations();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10f);
            
            // Display recommendations
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_selectedTab == 0)
                DrawFormatRecommendations();
            else
                DrawResolutionRecommendations();
            
            EditorGUILayout.EndScrollView();
        }
        
        // ── FORMAT RECOMMENDATIONS ────────────────────────────────────────────────
        
        private void RefreshFormatRecommendations()
        {
            _formatRecommendations = RenderTextureFormatOptimizer.AnalyzeFormats();
        }
        
        private void DrawFormatRecommendations()
        {
            if (_formatRecommendations == null || _formatRecommendations.Count == 0)
            {
                EditorGUILayout.HelpBox("No format optimization recommendations found.", MessageType.Info);
                return;
            }
            
            // Summary
            long totalSavings = 0L;
            foreach (var rec in _formatRecommendations)
                totalSavings += rec.MemorySavingsBytes;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Total Recommendations: {_formatRecommendations.Count}");
            EditorGUILayout.LabelField($"Total Potential Savings: {(totalSavings / (1024f * 1024f)):F2} MB");
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10f);
            
            // Recommendations list
            foreach (var rec in _formatRecommendations)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField($"RT: {rec.RenderTexture.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Owner: {(rec.Owner != null ? rec.Owner.name : "NULL")}");
                EditorGUILayout.LabelField($"Current Format: {rec.CurrentFormat}");
                EditorGUILayout.LabelField($"Recommended Format: {rec.RecommendedFormat}");
                EditorGUILayout.LabelField($"Memory Savings: {(rec.MemorySavingsBytes / (1024f * 1024f)):F2} MB");
                EditorGUILayout.LabelField($"Reason: {rec.Reason}");
                
                EditorGUILayout.Space(5f);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Optimization", GUILayout.Width(150f)))
                {
                    ApplyFormatOptimization(rec);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5f);
            }
        }
        
        private void ApplyFormatOptimization(FormatOptimizationRecommendation rec)
        {
            if (rec.RenderTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "RenderTexture is null.", "OK");
                return;
            }
            
            bool confirm = EditorUtility.DisplayDialog(
                "Apply Format Optimization",
                $"Change format from {rec.CurrentFormat} to {rec.RecommendedFormat}?\n\n" +
                $"Savings: {(rec.MemorySavingsBytes / (1024f * 1024f)):F2} MB\n\n" +
                $"WARNING: This will modify the RenderTexture at runtime. Visual quality may change.",
                "Apply",
                "Cancel"
            );
            
            if (confirm)
            {
                rec.RenderTexture.Release();
                rec.RenderTexture.format = rec.RecommendedFormat;
                rec.RenderTexture.Create();
                
                Debug.Log($"[FormatOptimizer] Applied format optimization to {rec.RenderTexture.name}: {rec.CurrentFormat} → {rec.RecommendedFormat}");
                
                RefreshFormatRecommendations();
            }
        }
        
        // ── RESOLUTION RECOMMENDATIONS ─────────────────────────────────────────────
        
        private void RefreshResolutionRecommendations()
        {
            _resolutionRecommendations = RenderTextureResolutionAnalyzer.AnalyzeResolutions();
        }
        
        private void DrawResolutionRecommendations()
        {
            if (_resolutionRecommendations == null || _resolutionRecommendations.Count == 0)
            {
                EditorGUILayout.HelpBox("No resolution optimization recommendations found.", MessageType.Info);
                return;
            }
            
            // Summary
            long totalSavings = 0L;
            foreach (var rec in _resolutionRecommendations)
                totalSavings += rec.MemorySavingsBytes;
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Total Recommendations: {_resolutionRecommendations.Count}");
            EditorGUILayout.LabelField($"Total Potential Savings: {(totalSavings / (1024f * 1024f)):F2} MB");
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10f);
            
            // Recommendations list
            foreach (var rec in _resolutionRecommendations)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField($"RT: {rec.RenderTexture.name}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Owner: {(rec.Owner != null ? rec.Owner.name : "NULL")}");
                EditorGUILayout.LabelField($"Current Resolution: {rec.CurrentWidth}x{rec.CurrentHeight}");
                EditorGUILayout.LabelField($"Recommended Resolution: {rec.RecommendedWidth}x{rec.RecommendedHeight} (scale {rec.Scale:F2}x)");
                EditorGUILayout.LabelField($"RMSE: {rec.RMSE:F1}%");
                EditorGUILayout.LabelField($"Memory Savings: {(rec.MemorySavingsBytes / (1024f * 1024f)):F2} MB");
                EditorGUILayout.LabelField($"Priority: {rec.Priority}");
                EditorGUILayout.LabelField($"Reason: {rec.Reason}");
                
                EditorGUILayout.Space(5f);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Optimization", GUILayout.Width(150f)))
                {
                    ApplyResolutionOptimization(rec);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5f);
            }
        }
        
        private void ApplyResolutionOptimization(ResolutionOptimizationRecommendation rec)
        {
            if (rec.RenderTexture == null)
            {
                EditorUtility.DisplayDialog("Error", "RenderTexture is null.", "OK");
                return;
            }
            
            bool confirm = EditorUtility.DisplayDialog(
                "Apply Resolution Optimization",
                $"Change resolution from {rec.CurrentWidth}x{rec.CurrentHeight} to {rec.RecommendedWidth}x{rec.RecommendedHeight}?\n\n" +
                $"Scale: {rec.Scale:F2}x\n" +
                $"RMSE: {rec.RMSE:F1}%\n" +
                $"Savings: {(rec.MemorySavingsBytes / (1024f * 1024f)):F2} MB\n\n" +
                $"WARNING: This will modify the RenderTexture at runtime. Visual quality may change.",
                "Apply",
                "Cancel"
            );
            
            if (confirm)
            {
                rec.RenderTexture.Release();
                rec.RenderTexture.width = rec.RecommendedWidth;
                rec.RenderTexture.height = rec.RecommendedHeight;
                rec.RenderTexture.Create();
                
                Debug.Log($"[ResolutionAnalyzer] Applied resolution optimization to {rec.RenderTexture.name}: {rec.CurrentWidth}x{rec.CurrentHeight} → {rec.RecommendedWidth}x{rec.RecommendedHeight}");
                
                RefreshResolutionRecommendations();
            }
        }
    }
}
#endif
