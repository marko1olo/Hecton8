#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class VerletTowTunerWindow : EditorWindow
    {
        private const int TetherSlots = 8;
        private const int CablePointCount = 11;
        private const int CableSegmentCount = 10;
        private const int MaterialCapacity = 16;
        private const double RefreshIntervalSeconds = 0.25d;

        private VerletCableTuningDTO _tuning;
        private string _status = "Vault not sampled.";
        private string _csvPath;
        private DateTime _lastCsvWriteUtc;
        private double _nextRefreshTime;
        private bool _hasVault;
        private bool _drawTension = true;

        [MenuItem("Hecton8/Physics/Verlet Tow Tuner")]
        public static void Open()
        {
            GetWindow<VerletTowTunerWindow>("Verlet Tow Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
            _csvPath = ResolveCsvPath();
            RefreshTuning();
            MonitorCsv();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        private void OnGUI()
        {
            RefreshTuning();
            if (!_hasVault)
            {
                EditorGUILayout.HelpBox("GlobalDataVault is not initialized.", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            float gravityY = EditorGUILayout.Slider("Gravity", _tuning.Gravity.y, -40f, 5f);
            float friction = EditorGUILayout.Slider("Fluid Friction", _tuning.FluidFriction, 0.90f, 0.995f);
            int iterations = EditorGUILayout.IntSlider("Constraint Iterations", _tuning.ConstraintIterations, 0, 10);
            float stretch = EditorGUILayout.Slider("Stretch Threshold", _tuning.StretchThreshold01, 0.01f, 0.5f);
            float breakForce = EditorGUILayout.Slider("Break Force", _tuning.BreakForce, 0f, 20000f);
            float rockFriction = EditorGUILayout.Slider("Rock Friction", _tuning.RockFriction01, 0f, 1f);
            float reelSpeed = EditorGUILayout.Slider("Reel Speed", _tuning.ReelSpeedMetersPerSecond, 0.5f, 40f);
            if (EditorGUI.EndChangeCheck())
            {
                _tuning.Gravity = new float3(0f, gravityY, 0f);
                _tuning.FluidFriction = math.saturate(friction);
                _tuning.ConstraintIterations = math.clamp(iterations, 0, 10);
                _tuning.StretchThreshold01 = math.max(0.001f, stretch);
                _tuning.BreakForce = math.max(0f, breakForce);
                _tuning.RockFriction01 = math.saturate(rockFriction);
                _tuning.ReelSpeedMetersPerSecond = math.max(0.001f, reelSpeed);
                WriteTuningToVault(in _tuning);
            }

            _drawTension = EditorGUILayout.Toggle("Draw Tension", _drawTension);
            if (GUILayout.Button("Reload cable_materials.csv"))
                ApplyCsvOverrides();

            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            MonitorCsv();
            Repaint();
        }

        private void RefreshTuning()
        {
            _hasVault = false;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            VaultBufferHandle<VerletCableTuningDTO> tuningHandle = vault.GetBufferHandle<VerletCableTuningDTO>(
                BufferID.VerletCableTuning,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<VerletCableTuningDTO> tuning = tuningHandle.Resolve(vault);
            if (!tuning.IsCreated || tuning.Length == 0)
                return;

            _tuning = tuning[0];
            if (math.lengthsq(_tuning.Gravity) <= 0.000001f)
                _tuning.Gravity = new float3(0f, -9.80665f, 0f);
            if (_tuning.FluidFriction <= 0f)
                _tuning.FluidFriction = 0.975f;
            if (_tuning.StretchThreshold01 <= 0f)
                _tuning.StretchThreshold01 = 0.18f;
            if (_tuning.RockFriction01 <= 0f)
                _tuning.RockFriction01 = 0.58f;
            if (_tuning.ReelSpeedMetersPerSecond <= 0f)
                _tuning.ReelSpeedMetersPerSecond = 18f;

            tuning[0] = _tuning;
            _hasVault = true;
        }

        private static void WriteTuningToVault(in VerletCableTuningDTO tuning)
        {
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            VaultBufferHandle<VerletCableTuningDTO> handle = vault.GetBufferHandle<VerletCableTuningDTO>(
                BufferID.VerletCableTuning,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<VerletCableTuningDTO> buffer = handle.Resolve(vault);
            if (buffer.IsCreated && buffer.Length > 0)
                buffer[0] = tuning;
        }

        private void MonitorCsv()
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
                return;

            DateTime writeTime = File.GetLastWriteTimeUtc(_csvPath);
            if (writeTime == _lastCsvWriteUtc)
                return;

            _lastCsvWriteUtc = writeTime;
            ApplyCsvOverrides();
        }

        private void ApplyCsvOverrides()
        {
            if (string.IsNullOrEmpty(_csvPath) || !File.Exists(_csvPath))
            {
                _status = "cable_materials.csv not found.";
                return;
            }

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
            {
                _status = "Vault unavailable.";
                return;
            }

            byte[] csv = File.ReadAllBytes(_csvPath);
            VaultBufferHandle<CableMaterialDTO> materialsHandle = vault.GetBufferHandle<CableMaterialDTO>(
                BufferID.VerletCableMaterials,
                MaterialCapacity,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<CableMaterialDTO> materials = materialsHandle.Resolve(vault);
            int parsed = CableMaterialCsvParser.Parse(csv.AsSpan(), materials);
            _status = parsed > 0 ? $"CSV overrides applied: {parsed} rows." : "CSV parsed no material rows.";
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            if (!_drawTension || !EditorApplication.isPlaying)
                return;

            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault vault))
                return;

            if (!vault.TryGetBufferHandle(BufferID.TetherVisualSegmentPositions, out VaultBufferHandle<float3> positionHandle) ||
                !positionHandle.IsCreated ||
                !vault.TryGetBufferHandle(BufferID.TetherCableSegmentTensions, out VaultBufferHandle<float> tensionHandle) ||
                !tensionHandle.IsCreated)
            {
                return;
            }

            NativeArray<float3> positions = positionHandle.Resolve(vault);
            NativeArray<float> tensions = tensionHandle.Resolve(vault);
            if (!positions.IsCreated || !tensions.IsCreated)
                return;

            float breakForce = _tuning.BreakForce > 1f ? _tuning.BreakForce : 1000f;
            for (int slot = 0; slot < TetherSlots; slot++)
            {
                int pointOffset = slot * CablePointCount;
                int tensionOffset = slot * CableSegmentCount;
                if (pointOffset + CablePointCount > positions.Length || tensionOffset + CableSegmentCount > tensions.Length)
                    break;

                for (int segment = 0; segment < CableSegmentCount; segment++)
                {
                    float3 a = positions[pointOffset + segment];
                    float3 b = positions[pointOffset + segment + 1];
                    if (!math.all(math.isfinite(a)) || !math.all(math.isfinite(b)))
                        continue;
                    if (math.lengthsq(a) <= 0.000001f && math.lengthsq(b) <= 0.000001f)
                        continue;

                    float stress01 = math.saturate(tensions[tensionOffset + segment] * math.rcp(breakForce));
                    Handles.color = ResolveTensionColor(stress01);
                    Handles.DrawAAPolyLine(3f, ToVector3(a), ToVector3(b));
                }
            }
        }

        private static Color ResolveTensionColor(float stress01)
        {
            if (stress01 > 0.82f)
                return Color.Lerp(Color.yellow, Color.red, math.saturate((stress01 - 0.82f) * 5.55f));

            return Color.Lerp(Color.green, Color.yellow, math.saturate(stress01 * 1.22f));
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static string ResolveCsvPath()
        {
            DirectoryInfo root = Directory.GetParent(Application.dataPath);
            return root == null ? "cable_materials.csv" : Path.Combine(root.FullName, "cable_materials.csv");
        }
    }
}
#endif
