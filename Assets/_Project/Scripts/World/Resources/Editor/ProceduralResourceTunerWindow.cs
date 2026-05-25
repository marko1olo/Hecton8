using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.Editor
{
    public sealed class ProceduralResourceTunerWindow : EditorWindow
    {
        private const SystemID OwnerSystemId = SystemID.WorldResourceSpawnerRuntime;

        private Label _stats;
        private Slider _baseDensity;
        private Slider _clusterSpread;
        private Slider _normalTolerance;
        private Slider _visualDensity;
        private readonly StringBuilder _statsBuilder = new StringBuilder(192); // COLD ALLOC: StringBuilder[192] — editor telemetry label formatter — owner: ProceduralResourceTunerWindow
        private IDataVault _dataVault;
        private uint _lastStatsFrame;
        private bool _reportedVaultInactive;
        private bool _updateRegistered;

        [MenuItem("HECTON-8/World/Procedural Resource Tuner")]
        public static void Open()
        {
            GetWindow<ProceduralResourceTunerWindow>("Procedural Resource Tuner");
        }

        public void CreateGUI()
        {
            _lastStatsFrame = uint.MaxValue;
            _dataVault = GlobalRegistry.DataVault;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _stats = new Label("Vault not active."); // COLD ALLOC: Label[status] — editor Vault telemetry status label — owner: ProceduralResourceTunerWindow
            rootVisualElement.Add(_stats);

            _baseDensity = CreateSlider("Base Node Density", 0.05f, 3f, 1f);
            _clusterSpread = CreateSlider("Cluster Spread Radius", 0.05f, 4f, 0.85f);
            _normalTolerance = CreateSlider("Surface Normal Alignment Tolerance", 0.05f, 1f, 0.5f);
            _visualDensity = CreateSlider("Visual Cluster Density", 0f, 1f, 1f);
            rootVisualElement.Add(_baseDensity);
            rootVisualElement.Add(_clusterSpread);
            rootVisualElement.Add(_normalTolerance);
            rootVisualElement.Add(_visualDensity);

            _baseDensity.RegisterValueChangedCallback(_ => WriteTuning());
            _clusterSpread.RegisterValueChangedCallback(_ => WriteTuning());
            _normalTolerance.RegisterValueChangedCallback(_ => WriteTuning());
            _visualDensity.RegisterValueChangedCallback(_ => WriteTuning());
            if (!_updateRegistered)
            {
                EditorApplication.update += Tick;
                _updateRegistered = true;
            }
        }

        private void OnDisable()
        {
            if (_updateRegistered)
            {
                EditorApplication.update -= Tick;
                _updateRegistered = false;
            }

            _dataVault = null;
        }

        private void OnFocus()
        {
            _dataVault = GlobalRegistry.DataVault;
        }

        private static Slider CreateSlider(string label, float low, float high, float value)
        {
            Slider slider = new Slider(label, low, high) // COLD ALLOC: Slider[tuning] — editor designer tuning control — owner: ProceduralResourceTunerWindow
            {
                value = value,
                showInputField = true
            };
            return slider;
        }

        private void Tick()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                if (!_reportedVaultInactive)
                {
                    _stats.text = "Vault not active.";
                    _reportedVaultInactive = true;
                }

                return;
            }

            _reportedVaultInactive = false;

            if (TryResolveExistingBuffer(vault, ProceduralGeologyVaultBufferIds.Tuning, out NativeArray<GeologyTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                GeologyTuningDTO row = tuning[0];
                if (!IsTuningUsable(in row))
                    row = GeologyTuningDTO.Default(128f);
                SetSliderWithoutNotify(_baseDensity, row.BaseNodeDensity);
                SetSliderWithoutNotify(_clusterSpread, row.ClusterSpreadRadius);
                SetSliderWithoutNotify(_normalTolerance, row.SurfaceNormalAlignmentTolerance);
                SetSliderWithoutNotify(_visualDensity, row.VisualClusterDensity);
            }

            GeologyGenerationTelemetryEntry latest = default;
            if (TryResolveExistingBuffer(vault, ProceduralGeologyVaultBufferIds.TelemetryRing, out NativeArray<GeologyGenerationTelemetryEntry> telemetry) &&
                telemetry.Length > 0)
            {
                for (int i = 0; i < telemetry.Length; i++)
                {
                    GeologyGenerationTelemetryEntry entry = telemetry[i];
                    if (entry.Frame >= latest.Frame)
                        latest = entry;
                }
            }

            if (latest.Frame == _lastStatsFrame)
                return;

            _lastStatsFrame = latest.Frame;
            _statsBuilder.Clear();
            _statsBuilder.Append("Sector: ");
            _statsBuilder.Append(latest.SectorHash);
            _statsBuilder.Append(" | Core: ");
            _statsBuilder.Append(latest.AuthoritativeNodeCount);
            _statsBuilder.Append(" | Render: ");
            _statsBuilder.Append(latest.RenderNodeCount);
            _statsBuilder.Append(" | Visual: ");
            _statsBuilder.Append(latest.VisualOnlyNodeCount);
            _statsBuilder.Append(" | Depleted Cull: ");
            _statsBuilder.Append(latest.DepletedCullCount);
            _statsBuilder.Append(" | Est us: ");
            _statsBuilder.Append(latest.GenerationBudgetUs);
            _stats.text = _statsBuilder.ToString();
        }

        private void WriteTuning()
        {
            IDataVault vault = _dataVault != null ? _dataVault : GlobalRegistry.DataVault;
            _dataVault = vault;
            if (vault == null)
                return;

            if (!AcquireOrCreateBuffer(
                    vault,
                    ProceduralGeologyVaultBufferIds.Tuning,
                    ProceduralGeologyConstants.TuningCapacity,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<GeologyTuningDTO> tuning))
            {
                return;
            }

            GeologyTuningDTO row = tuning[0];
            if (!IsTuningUsable(in row))
                row = GeologyTuningDTO.Default(128f);

            row.BaseNodeDensity = Mathf.Max(0.05f, _baseDensity.value);
            row.ClusterSpreadRadius = Mathf.Max(0.05f, _clusterSpread.value);
            row.SurfaceNormalAlignmentTolerance = Mathf.Clamp(_normalTolerance.value, 0.05f, 1f);
            row.VisualClusterDensity = Mathf.Clamp01(_visualDensity.value);
            row.SectorSizeMeters = Mathf.Max(16f, math.isfinite(row.SectorSizeMeters) ? row.SectorSizeMeters : 128f);
            row.GlobalQualityWeight = Mathf.Clamp01(HomeostasisBrain.GlobalQualityWeight);
            row.Flags = 0u;
            row.Version = 1u;
            tuning[0] = row;
        }

        private static bool TryResolveExistingBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated)
            {
                return false;
            }

            return true;
        }

        private static bool AcquireOrCreateBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);

            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider == null)
                return;

            slider.SetValueWithoutNotify(value);
        }

        private static bool IsTuningUsable(in GeologyTuningDTO row)
        {
            return row.Version == 1u &&
                   math.isfinite(row.BaseNodeDensity) &&
                   row.BaseNodeDensity >= 0.05f &&
                   row.BaseNodeDensity <= 16f &&
                   math.isfinite(row.ClusterSpreadRadius) &&
                   row.ClusterSpreadRadius >= 0.05f &&
                   row.ClusterSpreadRadius <= 16f &&
                   math.isfinite(row.SurfaceNormalAlignmentTolerance) &&
                   row.SurfaceNormalAlignmentTolerance >= 0.05f &&
                   row.SurfaceNormalAlignmentTolerance <= 1f &&
                   math.isfinite(row.VisualClusterDensity) &&
                   row.VisualClusterDensity >= 0f &&
                   row.VisualClusterDensity <= 1f &&
                   math.isfinite(row.SectorSizeMeters) &&
                   row.SectorSizeMeters >= 16f &&
                   row.SectorSizeMeters <= 100000f;
        }
    }
}
