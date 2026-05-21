#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class Shinobu143CablePhysicsTunerWindow : EditorWindow
    {
        private const int MaterialCapacity = 16;

        private Slider _quality;
        private Slider _gravity;
        private Slider _friction;
        private Slider _stretch;
        private Slider _breakForce;
        private Slider _rockFriction;
        private Slider _reelSpeed;
        private SliderInt _iterations;
        private Label _status;
        private Label _maxTensionReadout;
        private Label _computeTimeReadout;
        private Label _nodeIterationReadout;
        private Label _stateHashReadout;
        private double _nextTelemetryRefreshTime;

        [MenuItem("Hecton8/Physics/SHINOBU 143 Cable Tuner")]
        public static void Open()
        {
            GetWindow<Shinobu143CablePhysicsTunerWindow>("SHINOBU 143 Cable");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshTelemetryReadout;
            EditorApplication.update += RefreshTelemetryReadout;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetryReadout;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _quality = BuildSlider("Global Quality", 0f, 1f, HomeostasisBrain.GlobalQualityWeight);
            _gravity = BuildSlider("Gravity Y", -40f, 5f, -9.80665f);
            _friction = BuildSlider("Fluid Friction", 0.90f, 0.995f, 0.975f);
            _iterations = new SliderInt("Constraint Iterations", 0, 15) { value = 0 };
            _stretch = BuildSlider("Stretch Threshold", 0.001f, 0.5f, 0.18f);
            _breakForce = BuildSlider("Break Force", 0f, 20000f, 0f);
            _rockFriction = BuildSlider("Rock Friction", 0f, 1f, 0.58f);
            _reelSpeed = BuildSlider("Reel Speed", 0.5f, 40f, 18f);
            _status = new Label("Vault not sampled.");
            _maxTensionReadout = new Label("Max tension: --");
            _computeTimeReadout = new Label("Compute us: --");
            _nodeIterationReadout = new Label("Nodes / iterations: --");
            _stateHashReadout = new Label("State hash: --");

            root.Add(_quality);
            root.Add(_gravity);
            root.Add(_friction);
            root.Add(_iterations);
            root.Add(_stretch);
            root.Add(_breakForce);
            root.Add(_rockFriction);
            root.Add(_reelSpeed);
            root.Add(BuildButton("Apply Tuning", ApplyTuning));
            root.Add(BuildButton("Reload cable_materials.csv", ReloadCsv));
            root.Add(BuildButton("Dump Cable Surgeon Ring", DumpCableSurgeon));
            root.Add(_maxTensionReadout);
            root.Add(_computeTimeReadout);
            root.Add(_nodeIterationReadout);
            root.Add(_stateHashReadout);
            root.Add(_status);

            _quality.RegisterValueChangedCallback(evt =>
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(math.saturate(evt.newValue), true));
            PullFromVault();
            RefreshTelemetryReadout();
        }

        private static Slider BuildSlider(string label, float low, float high, float value)
        {
            return new Slider(label, low, high) { value = value };
        }

        private static Button BuildButton(string text, System.Action action)
        {
            return new Button(action) { text = text };
        }

        private void PullFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.VerletCableTuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<VerletCableTuningDTO> tuningHandle,
                    out NativeArray<VerletCableTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                VerletCableTuningDTO dto = tuning[0];
                if (math.lengthsq(dto.Gravity) <= 0.000001f)
                    dto.Gravity = new float3(0f, -9.80665f, 0f);
                if (dto.FluidFriction <= 0f)
                    dto.FluidFriction = 0.975f;
                if (dto.StretchThreshold01 <= 0f)
                    dto.StretchThreshold01 = 0.18f;
                if (dto.RockFriction01 <= 0f)
                    dto.RockFriction01 = 0.58f;
                if (dto.ReelSpeedMetersPerSecond <= 0f)
                    dto.ReelSpeedMetersPerSecond = 18f;

                tuning[0] = dto;
                _gravity.SetValueWithoutNotify(dto.Gravity.y);
                _friction.SetValueWithoutNotify(dto.FluidFriction);
                _iterations.SetValueWithoutNotify(math.clamp(dto.ConstraintIterations, 0, 15));
                _stretch.SetValueWithoutNotify(dto.StretchThreshold01);
                _breakForce.SetValueWithoutNotify(math.max(0f, dto.BreakForce));
                _rockFriction.SetValueWithoutNotify(dto.RockFriction01);
                _reelSpeed.SetValueWithoutNotify(dto.ReelSpeedMetersPerSecond);
                _status.text = "Vault sampled.";
            }
            finally
            {
                vault.ReleaseWriteLock(in tuningHandle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyTuning()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.VerletCableTuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<VerletCableTuningDTO> tuningHandle,
                    out NativeArray<VerletCableTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                tuning[0] = new VerletCableTuningDTO
                {
                    Gravity = new float3(0f, _gravity.value, 0f),
                    FluidFriction = math.saturate(_friction.value),
                    ConstraintIterations = math.clamp(_iterations.value, 0, 15),
                    StretchThreshold01 = math.max(0.001f, _stretch.value),
                    BreakForce = math.max(0f, _breakForce.value),
                    RockFriction01 = math.saturate(_rockFriction.value),
                    ReelSpeedMetersPerSecond = math.max(0.001f, _reelSpeed.value),
                    Reserved0 = 0f,
                    Reserved1 = 0f
                };
                _status.text = "Tuning written to Vault.";
            }
            finally
            {
                vault.ReleaseWriteLock(in tuningHandle, SystemID.CoreDiagnostics);
            }
        }

        private void ReloadCsv()
        {
            string path = ResolveCsvPath();
            if (!File.Exists(path))
            {
                _status.text = "cable_materials.csv not found.";
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.VerletCableMaterials,
                    MaterialCapacity,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<CableMaterialDTO> legacyHandle,
                    out NativeArray<CableMaterialDTO> legacyMaterials))
            {
                _status.text = "Legacy material Vault lane unavailable.";
                return;
            }

            try
            {
                if (!TryAcquireEditorWriteView(
                        vault,
                        BufferID.Shinobu143CableMaterials,
                        MaterialCapacity,
                        SystemID.Physics,
                        NativeArrayOptions.ClearMemory,
                        out VaultGenerationHandle<CableMaterialDTO> shinobuHandle,
                        out NativeArray<CableMaterialDTO> shinobuMaterials))
                {
                    _status.text = "SHINOBU_143 material Vault lane unavailable.";
                    return;
                }

                try
                {
                    int parsed = CableMaterialCsvParser.Parse(bytes.AsSpan(), legacyMaterials);
                    CableMaterialCsvParser.ParseHashTable(bytes.AsSpan(), shinobuMaterials);
                    _status.text = parsed > 0 ? "CSV materials applied." : "CSV parsed no rows.";
                }
                finally
                {
                    vault.ReleaseWriteLock(in shinobuHandle, SystemID.CoreDiagnostics);
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in legacyHandle, SystemID.CoreDiagnostics);
            }
        }

        private void DumpCableSurgeon()
        {
            bool dumped = TetherAupRuntimeIntrospection.TryDumpCableSurgeon(GlobalRegistry.DataVault, 0x5348554Eu);
            _status.text = dumped ? "Cable surgeon dump written." : "Cable surgeon dump unavailable.";
        }

        private void RefreshTelemetryReadout()
        {
            if (_maxTensionReadout == null ||
                _computeTimeReadout == null ||
                _nodeIterationReadout == null ||
                _stateHashReadout == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + 0.25d;
            if (!TetherAupRuntimeIntrospection.TrySampleLatestTelemetry(GlobalRegistry.DataVault, out TetherAupTelemetryEntry telemetry))
            {
                _maxTensionReadout.text = "Max tension: --";
                _computeTimeReadout.text = "Compute us: --";
                _nodeIterationReadout.text = "Nodes / iterations: --";
                _stateHashReadout.text = "State hash: --";
                return;
            }

            _maxTensionReadout.text = "Max tension: " + telemetry.MaxTension.ToString("F2");
            _computeTimeReadout.text = "Compute us: " + telemetry.CpuMicroseconds.ToString("F2");
            _nodeIterationReadout.text = "Nodes / iterations: " + telemetry.NodeCount + " / " + telemetry.IterationCount;
            _stateHashReadout.text = "State hash: 0x" + telemetry.StateHash.ToString("X8");
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    required,
                    owner,
                    options);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;

            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }

        private static string ResolveCsvPath()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(root, "cable_materials.csv");
        }
    }
}
#endif
