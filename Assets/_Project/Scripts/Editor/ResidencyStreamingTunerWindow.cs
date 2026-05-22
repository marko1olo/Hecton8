using System;
using System.IO;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class ResidencyStreamingTunerWindow : EditorWindow
    {
        private const string CsvAssetPath = "Assets/_Project/Data/World/Streaming/streaming_profiles.csv";
        private WorldChunkResidencyManager _manager;
        private WorldStreamingRuntimeTuning _tuning;
        private long _csvLastWriteTicks;
        private double _nextCsvPollTime;
        private bool _loaded;

        [MenuItem("Hecton/World/Residency & Streaming Tuner")]
        public static void Open()
        {
            GetWindow<ResidencyStreamingTunerWindow>("Residency & Streaming Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGrid;
            EditorApplication.update += PollCsvOverride;
            ResolveManager();
            PullTuning();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGrid;
            EditorApplication.update -= PollCsvOverride;
        }

        private void OnGUI()
        {
            _manager = (WorldChunkResidencyManager)EditorGUILayout.ObjectField("Manager", _manager, typeof(WorldChunkResidencyManager), true);
            if (_manager == null && GUILayout.Button("Find Active Manager"))
                ResolveManager();

            if (!_loaded)
                PullTuning();

            EditorGUI.BeginChangeCheck();
            _tuning.PredictiveVelocityStretch = EditorGUILayout.Slider("Predictive Velocity Stretch", _tuning.PredictiveVelocityStretch, 0f, 10f);
            _tuning.Lod1RadiusMeters = EditorGUILayout.Slider("LOD1 Radius", _tuning.Lod1RadiusMeters, 1f, 3000f);
            _tuning.DehydrationHysteresisMeters = EditorGUILayout.Slider("Dehydration Hysteresis", _tuning.DehydrationHysteresisMeters, 0f, 300f);
            _tuning.MaxConcurrentLoads = EditorGUILayout.IntSlider("Max Concurrent Loads", _tuning.MaxConcurrentLoads, 1, 16);
            _tuning.HydrationCopyBudgetBytes = EditorGUILayout.IntSlider("Hydration Copy Budget", _tuning.HydrationCopyBudgetBytes, 64 * 1024, 1024 * 1024);

            if (EditorGUI.EndChangeCheck())
                ApplyTuning();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pull Runtime"))
                    PullTuning();
                if (GUILayout.Button("Apply"))
                    ApplyTuning();
                if (GUILayout.Button("Reload CSV"))
                    ReloadCsvNow();
            }
        }

        private void ResolveManager()
        {
            _manager = UnityEngine.Object.FindFirstObjectByType<WorldChunkResidencyManager>();
        }

        private void PullTuning()
        {
            if (_manager == null)
                ResolveManager();

            if (_manager != null)
            {
                _tuning = _manager.ReadRuntimeTuning();
                _loaded = true;
                return;
            }

            _tuning = WorldStreamingRuntimeTuning.CreateDefault();
            _loaded = true;
        }

        private void ApplyTuning()
        {
            if (_manager == null)
                ResolveManager();

            if (_manager != null)
                _manager.ApplyRuntimeTuning(in _tuning);

            SceneView.RepaintAll();
        }

        private void PollCsvOverride()
        {
            if (EditorApplication.timeSinceStartup < _nextCsvPollTime)
                return;

            _nextCsvPollTime = EditorApplication.timeSinceStartup + 0.5d;
            string path = Path.Combine(Directory.GetCurrentDirectory(), CsvAssetPath);
            if (!File.Exists(path))
                return;

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (ticks == _csvLastWriteTicks)
                return;

            _csvLastWriteTicks = ticks;
            ReloadCsvNow();
        }

        private void ReloadCsvNow()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), CsvAssetPath);
            if (!File.Exists(path))
                return;

            string csv = File.ReadAllText(path);
            if (_manager == null)
                ResolveManager();

            if (_manager != null && _manager.TryApplyStreamingProfileCsvText(csv))
                _tuning = _manager.ReadRuntimeTuning();
            else if (WorldStreamingProfileCsvParser.TryParse(csv.AsSpan(), ref _tuning))
                ApplyTuning();
        }

        private void DrawSceneGrid(SceneView sceneView)
        {
            if (_manager == null)
                ResolveManager();
            if (_manager == null || !_manager.TryGetChunkResidencyDtos(out NativeArray<ChunkResidencyDTO>.ReadOnly chunks, out int count))
                return;

            int safeCount = math.min(count, chunks.Length);
            for (int i = 0; i < safeCount; i++)
            {
                ChunkResidencyDTO chunk = chunks[i];
                DrawChunkCell(chunk, ResolveColor(chunk.StateFlags));
            }
        }

        private static Color ResolveColor(byte flags)
        {
            if ((flags & ChunkResidencyStateFlags.ThreatOverride) != 0)
                return new Color(0.1f, 0.35f, 1f, 0.85f);
            if ((flags & ChunkResidencyStateFlags.DehydrationPending) != 0)
                return new Color(1f, 0.1f, 0.05f, 0.8f);
            if ((flags & ChunkResidencyStateFlags.HydrationPending) != 0)
                return new Color(1f, 0.85f, 0.1f, 0.8f);
            if ((flags & ChunkResidencyStateFlags.Hydrated) != 0)
                return new Color(0.15f, 0.9f, 0.25f, 0.8f);
            return new Color(0.25f, 0.25f, 0.25f, 0.35f);
        }

        private static void DrawChunkCell(in ChunkResidencyDTO chunk, Color color)
        {
            float half = 96f;
            Vector3 center = HectonFloatingOrigin.ToRuntimePosition(chunk.AUP_Center, HectonFloatingOrigin.CurrentTotalOffsetDouble);
            center.y = 0f;
            Vector3 a = center + new Vector3(-half, 0f, -half);
            Vector3 b = center + new Vector3(half, 0f, -half);
            Vector3 c = center + new Vector3(half, 0f, half);
            Vector3 d = center + new Vector3(-half, 0f, half);
            Handles.color = color;
            Handles.DrawAAPolyLine(3f, a, b, c, d, a);
        }
    }
}
