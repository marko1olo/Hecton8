// ============================================================================
// HECTON-8 — LODStatisticsWindow.cs
// Real-time LOD system performance monitoring window.
//
// RESPONSIBILITIES:
//   • Display registered LOD group count
//   • Display active impostor count
//   • Display frustum/distance culled counts
//   • Display current render scale
//   • Display LOD system CPU time graph
//   • Auto-refresh toggle
//
// ARCHITECTURE:
//   • EditorWindow — menu: Hecton8/LOD System/LOD Statistics
//   • Zero-GC during updates (pre-allocated collections)
//   • Refresh rate: 0.5s (configurable)
//
// PERFORMANCE:
//   • No impact on gameplay performance
//   • Minimal editor overhead
// ============================================================================

#if UNITY_EDITOR

using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Hecton8.Core;
using Hecton8.World;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor window for real-time LOD system performance monitoring.
    /// </summary>
    /// <remarks>
    /// METRICS DISPLAYED:
    ///   • Registered LOD group count
    ///   • Active impostor count
    ///   • Frustum culled count
    ///   • Distance culled count
    ///   • Current render scale
    ///   • LOD system CPU time (ms/frame)
    ///   • CPU time graph (last 60 samples)
    /// 
    /// REFRESH RATE:
    ///   • Auto-refresh: 0.5s interval
    ///   • Manual refresh: on-demand button
    /// </remarks>
    public sealed class LODStatisticsWindow : EditorWindow
    {
        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // COLD ALLOC: List<float>[60] — CPU time history — owner: LODStatisticsWindow
        private readonly List<float> _cpuTimeHistory = new List<float>(60);

        private bool _autoRefresh = true;
        private double _lastRefreshTime;
        private const float RefreshInterval = 0.5f;

        private Vector2 _scrollPosition;

        // Cached stats
        private int _registeredLODGroupCount;
        private int _activeImpostorCount;
        private int _frustumCulledCount;
        private int _distanceCulledCount;
        private float _currentRenderScale;
        private float _lodSystemCPUTime;

        // ══════════════════════════════════════════════════════════
        //  MENU ITEM
        // ══════════════════════════════════════════════════════════

        [MenuItem("Hecton8/LOD System/LOD Statistics")]
        private static void ShowWindow()
        {
            var window = GetWindow<LODStatisticsWindow>("LOD Statistics");
            window.minSize = new Vector2(600f, 500f);
            window.Show();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            _lastRefreshTime = EditorApplication.timeSinceStartup;
        }

        private void OnInspectorUpdate()
        {
            if (!_autoRefresh) return;

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastRefreshTime >= RefreshInterval)
            {
                RefreshStats();
                _lastRefreshTime = currentTime;
                Repaint();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  GUI
        // ══════════════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("LOD System Statistics", EditorStyles.boldLabel);
            EditorGUILayout.Space(5f);

            // Auto-refresh toggle
            EditorGUILayout.BeginHorizontal();
            _autoRefresh = EditorGUILayout.Toggle("Auto Refresh", _autoRefresh);
            if (GUILayout.Button("Refresh Now", GUILayout.Width(100f)))
            {
                RefreshStats();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10f);

            // Check if systems are initialized
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("LOD system statistics are only available in Play Mode.", MessageType.Info);
                return;
            }

            if (GlobalRegistry.LODSystem == null)
            {
                EditorGUILayout.HelpBox("LODSystemManager not found in scene.", MessageType.Warning);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // ──────────────────────────────────────────────────────
            //  LOD SYSTEM METRICS
            // ──────────────────────────────────────────────────────

            EditorGUILayout.LabelField("LOD System", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Registered LOD Groups", _registeredLODGroupCount.ToString(CultureInfo.InvariantCulture));
            DrawStatRow("LOD System CPU Time", _lodSystemCPUTime.ToString("F3", CultureInfo.InvariantCulture) + " ms");

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10f);

            // ──────────────────────────────────────────────────────
            //  CULLING METRICS
            // ──────────────────────────────────────────────────────

            EditorGUILayout.LabelField("Culling System", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Frustum Culled", _frustumCulledCount.ToString(CultureInfo.InvariantCulture));
            DrawStatRow("Distance Culled", _distanceCulledCount.ToString(CultureInfo.InvariantCulture));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10f);

            // ──────────────────────────────────────────────────────
            //  IMPOSTOR METRICS
            // ──────────────────────────────────────────────────────

            EditorGUILayout.LabelField("Impostor System", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Active Impostors", _activeImpostorCount.ToString(CultureInfo.InvariantCulture));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10f);

            // ──────────────────────────────────────────────────────
            //  DYNAMIC RESOLUTION METRICS
            // ──────────────────────────────────────────────────────

            EditorGUILayout.LabelField("Dynamic Resolution", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Current Render Scale", _currentRenderScale.ToString("F3", CultureInfo.InvariantCulture));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10f);

            // ──────────────────────────────────────────────────────
            //  CPU TIME GRAPH
            // ──────────────────────────────────────────────────────

            EditorGUILayout.LabelField("CPU Time Graph (Last 60 Samples)", EditorStyles.boldLabel);
            DrawCPUTimeGraph();

            EditorGUILayout.EndScrollView();
        }

        // ══════════════════════════════════════════════════════════
        //  REFRESH LOGIC
        // ══════════════════════════════════════════════════════════

        private void RefreshStats()
        {
            if (!Application.isPlaying) return;

            // LOD System
            LODSystemManager lodSystemManager = GlobalRegistry.LODSystem;
            if (lodSystemManager != null)
            {
                _registeredLODGroupCount = lodSystemManager.RegisteredLODGroupCount;
                _lodSystemCPUTime = lodSystemManager.LODSystemCPUTime;

                // Update CPU time history
                _cpuTimeHistory.Add(_lodSystemCPUTime);
                if (_cpuTimeHistory.Count > 60)
                {
                    _cpuTimeHistory.RemoveAt(0);
                }
            }

            // Culling System
            CullingManager cullingManager = GlobalRegistry.Culling;
            if (cullingManager != null)
            {
                _frustumCulledCount = cullingManager.FrustumCulledCount;
                _distanceCulledCount = cullingManager.DistanceCulledCount;
            }

            // Impostor System
            ImpostorSystem impostorSystem = GlobalRegistry.Impostors;
            if (impostorSystem != null)
            {
                _activeImpostorCount = impostorSystem.ActiveImpostorCount;
            }

            // Dynamic Resolution
            DynamicResolutionScaler dynamicResolutionScaler = GlobalRegistry.DynamicResolution;
            if (dynamicResolutionScaler != null)
            {
                _currentRenderScale = dynamicResolutionScaler.CurrentRenderScale;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  DRAWING HELPERS
        // ══════════════════════════════════════════════════════════

        private static void DrawStatRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(200f));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCPUTimeGraph()
        {
            if (_cpuTimeHistory.Count == 0)
            {
                EditorGUILayout.HelpBox("No data available yet.", MessageType.Info);
                return;
            }

            Rect graphRect = GUILayoutUtility.GetRect(100f, 150f, GUILayout.ExpandWidth(true));

            // Background
            EditorGUI.DrawRect(graphRect, new Color(0.2f, 0.2f, 0.2f, 1f));

            // Find max value for scaling
            float maxValue = 0f;
            for (int i = 0; i < _cpuTimeHistory.Count; i++)
            {
                if (_cpuTimeHistory[i] > maxValue)
                    maxValue = _cpuTimeHistory[i];
            }

            // Clamp max to at least 2ms for visibility
            if (maxValue < 2f)
                maxValue = 2f;

            // Draw grid lines
            DrawGridLine(graphRect, 0.5f, maxValue, Color.gray);
            DrawGridLine(graphRect, 1.0f, maxValue, Color.gray);
            DrawGridLine(graphRect, 1.5f, maxValue, Color.gray);
            DrawGridLine(graphRect, 2.0f, maxValue, Color.red);

            // Draw graph
            if (_cpuTimeHistory.Count > 1)
            {
                float xStep = graphRect.width / (_cpuTimeHistory.Count - 1);

                for (int i = 0; i < _cpuTimeHistory.Count - 1; i++)
                {
                    float x1 = graphRect.x + i * xStep;
                    float y1 = graphRect.yMax - (_cpuTimeHistory[i] / maxValue) * graphRect.height;

                    float x2 = graphRect.x + (i + 1) * xStep;
                    float y2 = graphRect.yMax - (_cpuTimeHistory[i + 1] / maxValue) * graphRect.height;

                    // Clamp y values
                    y1 = Mathf.Clamp(y1, graphRect.yMin, graphRect.yMax);
                    y2 = Mathf.Clamp(y2, graphRect.yMin, graphRect.yMax);

                    Handles.color = Color.green;
                    Handles.DrawLine(new Vector3(x1, y1, 0f), new Vector3(x2, y2, 0f));
                }
            }

            // Draw labels
            GUI.Label(
                new Rect(graphRect.x + 5f, graphRect.y + 5f, 100f, 20f),
                "Max: " + maxValue.ToString("F2", CultureInfo.InvariantCulture) + " ms",
                EditorStyles.miniLabel);
            GUI.Label(new Rect(graphRect.x + 5f, graphRect.yMax - 20f, 100f, 20f), "0 ms", EditorStyles.miniLabel);
        }

        private static void DrawGridLine(Rect graphRect, float value, float maxValue, Color color)
        {
            float y = graphRect.yMax - (value / maxValue) * graphRect.height;
            if (y < graphRect.yMin || y > graphRect.yMax) return;

            Handles.color = color;
            Handles.DrawLine(
                new Vector3(graphRect.x, y, 0f),
                new Vector3(graphRect.xMax, y, 0f)
            );

            GUI.Label(new Rect(graphRect.xMax - 50f, y - 10f, 50f, 20f), $"{value:F1}ms", EditorStyles.miniLabel);
        }
    }
}

#endif
