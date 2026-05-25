using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.VoxelSurfaceNets.Editor
{
    public sealed class VoxelMeshTunerWindow : EditorWindow
    {
        private static bool _showRawExtraction;
        private static VoxelSurfaceNetsVaultHandles _handles;
        private static bool _hasHandles;
        private double _nextCsvPollTime;
        private Vector2 _scroll;

        [MenuItem("HECTON-8/Voxel Mesh Tuner")]
        public static void Open()
        {
            GetWindow<VoxelMeshTunerWindow>("Voxel Mesh Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawRawExtraction;
            SceneView.duringSceneGui += DrawRawExtraction;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawRawExtraction;
        }

        private void OnGUI()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not registered.", MessageType.Warning);
                return;
            }

            if (!_hasHandles || !_handles.IsCreated())
                _hasHandles = VoxelSurfaceNetsVault.TryResolve(vault, out _handles);

            if (_hasHandles && EditorApplication.timeSinceStartup >= _nextCsvPollTime)
            {
                _nextCsvPollTime = EditorApplication.timeSinceStartup + 1.0;
                VoxelSurfaceNetsVault.TryPollCsvOverrides(vault, ref _handles, ProjectRoot());
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Resolve Vault", GUILayout.Height(24)))
                _hasHandles = VoxelSurfaceNetsVault.TryResolve(vault, out _handles);

            if (GUILayout.Button("Load CSV", GUILayout.Height(24)) && _hasHandles)
                VoxelSurfaceNetsVault.TryLoadCsvOverrides(vault, ref _handles, ProjectRoot());

            if (GUILayout.Button("Dump Black Box", GUILayout.Height(24)) && _hasHandles &&
                VoxelSurfaceNetsVault.TryResolveViews(vault, ref _handles, out VoxelSurfaceNetsVaultBuffers dumpBuffers))
            {
                VoxelSurfaceNetsVault.TryDumpBlackBox(in dumpBuffers, ProjectRoot(), VoxelSurfaceNetsConstants.FaultSlowExtraction);
            }

            EditorGUILayout.EndHorizontal();

            if (!_hasHandles || !VoxelSurfaceNetsVault.TryGetTuning(vault, ref _handles, out VoxelMeshingTuningDTO tuning))
            {
                EditorGUILayout.HelpBox("Voxel Surface Nets vault buffers are not ready.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            tuning.GlobalQualityWeight = EditorGUILayout.Slider("Global Quality Weight", tuning.GlobalQualityWeight, 0f, 1f);
            tuning.IsoSurface = EditorGUILayout.Slider("Iso-Surface Threshold", tuning.IsoSurface, -1f, 1f);
            tuning.MaxChunksPerFrame = EditorGUILayout.IntSlider("Max Chunks Per Frame", tuning.MaxChunksPerFrame, 1, 2);
            tuning.NormalSmoothingAngleDegrees = EditorGUILayout.Slider("Normal Smoothing Angle", tuning.NormalSmoothingAngleDegrees, 0f, 89f);
            tuning.DecimationAggression = EditorGUILayout.Slider("Decimation Aggression", tuning.DecimationAggression, 0f, 1f);
            tuning.BiomeBlendScale = EditorGUILayout.Slider("Biome Blend Scale", tuning.BiomeBlendScale, 0f, 8f);
            tuning.MaxExtractionMs = EditorGUILayout.Slider("Dump Threshold Ms", tuning.MaxExtractionMs, 0.25f, 4f);
            _showRawExtraction = EditorGUILayout.Toggle("Show Raw Extraction", _showRawExtraction);
            tuning.DebugRawCapture01 = _showRawExtraction ? 1f : 0f;

            if (EditorGUI.EndChangeCheck())
            {
                tuning.Version++;
                VoxelSurfaceNetsVault.TrySetTuning(vault, ref _handles, in tuning);
                SceneView.RepaintAll();
            }

            if (VoxelSurfaceNetsVault.TryResolveViews(vault, ref _handles, out VoxelSurfaceNetsVaultBuffers buffers))
            {
                int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0 ? buffers.TelemetryCursor[0] : 0;
                if (buffers.TelemetryRing.IsCreated && buffers.TelemetryRing.Length > 0)
                {
                    VoxelMeshingTelemetryEntry telemetry = buffers.TelemetryRing[math.clamp(cursor, 0, buffers.TelemetryRing.Length - 1)];
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField("Last Chunk Hash", telemetry.ChunkHash.ToString("X8", CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("Vertices", telemetry.VertexCount.ToString(CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("Indices", telemetry.IndexCount.ToString(CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("Estimated Extraction Ms", telemetry.ExtractionComputeTimeMs.ToString("0.000", CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("Sampling Ratio", telemetry.SamplingRatio.ToString("0.00", CultureInfo.InvariantCulture));
                    EditorGUILayout.LabelField("Decimation Ratio", telemetry.DecimationRatio.ToString("0.00", CultureInfo.InvariantCulture));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawRawExtraction(SceneView sceneView)
        {
            if (!_showRawExtraction)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!_hasHandles || !_handles.IsCreated())
                _hasHandles = VoxelSurfaceNetsVault.TryResolve(vault, out _handles);

            if (!_hasHandles ||
                !VoxelSurfaceNetsVault.TryResolveViews(vault, ref _handles, out VoxelSurfaceNetsVaultBuffers buffers) ||
                !buffers.RawDebugVertices.IsCreated ||
                !buffers.States.IsCreated ||
                buffers.States.Length <= 0)
            {
                return;
            }

            int count = math.min(buffers.States[0].RawDebugVertexCount, buffers.RawDebugVertices.Length);
            if (count < 3)
                return;

            Handles.color = Color.yellow;
            for (int i = 0; i + 2 < count; i += 3)
            {
                float3 a = buffers.RawDebugVertices[i];
                float3 b = buffers.RawDebugVertices[i + 1];
                float3 c = buffers.RawDebugVertices[i + 2];
                Vector3 va = new Vector3(a.x, a.y, a.z);
                Vector3 vb = new Vector3(b.x, b.y, b.z);
                Vector3 vc = new Vector3(c.x, c.y, c.z);
                Handles.DrawLine(va, vb);
                Handles.DrawLine(vb, vc);
                Handles.DrawLine(vc, va);
            }
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }
    }
}
