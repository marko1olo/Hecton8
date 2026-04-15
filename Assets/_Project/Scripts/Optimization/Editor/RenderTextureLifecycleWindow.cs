#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    /// <summary>
    /// Editor window for viewing RenderTexture lifecycle tracking data.
    /// Displays tracked RT count, total memory, allocations by owner.
    /// Auto-refreshes in Play Mode.
    /// </summary>
    public class RenderTextureLifecycleWindow : EditorWindow
    {
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private Vector2 _scrollPosition;
        private StringBuilder _auditReport = new StringBuilder(4096);
        private double _lastRefreshTime;
        private const double RefreshInterval = 0.5; // Refresh every 0.5s in Play Mode
        
        // ── MENU ITEM ──────────────────────────────────────────────────────────────
        
        [MenuItem("Hecton8/Optimization/RenderTexture Lifecycle Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<RenderTextureLifecycleWindow>("RT Lifecycle");
            window.minSize = new Vector2(600f, 400f);
            window.Show();
        }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void OnEnable()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }
        
        private void Update()
        {
            // Auto-refresh in Play Mode
            if (EditorApplication.isPlaying)
            {
                double currentTime = EditorApplication.timeSinceStartup;
                if (currentTime - _lastRefreshTime >= RefreshInterval)
                {
                    _lastRefreshTime = currentTime;
                    Repaint();
                }
            }
        }
        
        // ── GUI ────────────────────────────────────────────────────────────────────
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            
            // Header
            EditorGUILayout.LabelField("RenderTexture Lifecycle Viewer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5f);
            
            // Check if tracker is available
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to view RenderTexture lifecycle data.", MessageType.Info);
                return;
            }
            
            if (RenderTextureLifecycleTracker.Instance == null)
            {
                EditorGUILayout.HelpBox("RenderTextureLifecycleTracker not available. Ensure VRAMOptimizationBootstrap is running.", MessageType.Warning);
                return;
            }
            
            var tracker = RenderTextureLifecycleTracker.Instance;
            
            // Summary
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Tracked RenderTextures: {tracker.TrackedRenderTextureCount}");
            EditorGUILayout.LabelField($"Total Memory: {(tracker.TrackedRenderTextureMemoryBytes / (1024f * 1024f)):F2} MB");
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10f);
            
            // Refresh button
            if (GUILayout.Button("Refresh", GUILayout.Width(100f)))
            {
                Repaint();
            }
            
            EditorGUILayout.Space(10f);
            
            // Audit report
            EditorGUILayout.LabelField("Allocations by Owner", EditorStyles.boldLabel);
            
            _auditReport.Clear();
            tracker.GenerateAuditReport(_auditReport);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.TextArea(_auditReport.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
