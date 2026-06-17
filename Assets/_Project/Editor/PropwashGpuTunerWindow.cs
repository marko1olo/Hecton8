#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.VFX;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class PropwashGpuTunerWindow : EditorWindow
    {
        private const string ProximityKey = "H8.PropwashGpu.SiltProximity";
        private const string CurlKey = "H8.PropwashGpu.CurlNoiseFrequency";
        private const string QualityKey = "H8.PropwashGpu.QualityOverride";
        private const SystemID OwnerSystem = SystemID.Vfx;

        private Slider _proximitySlider;
        private Slider _curlSlider;
        private Slider _qualitySlider;
        private Label _statusLabel;
        private TelemetryWaterfallElement _waterfall;

        [MenuItem("Hecton8/Rendering/Propwash GPU Tuner")]
        public static void Open()
        {
            GetWindow<PropwashGpuTunerWindow>("Propwash GPU");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _proximitySlider = CreateSlider("Silt Proximity Meters", 0.05f, 8f, EditorPrefs.GetFloat(ProximityKey, 1.8f));
            _curlSlider = CreateSlider("Curl Noise Frequency", 0.01f, 2f, EditorPrefs.GetFloat(CurlKey, 0.215f));
            _qualitySlider = CreateSlider("GlobalQualityWeight Override", -1f, 1f, EditorPrefs.GetFloat(QualityKey, -1f));
            _proximitySlider.RegisterValueChangedCallback(_ => ApplyToVault());
            _curlSlider.RegisterValueChangedCallback(_ => ApplyToVault());
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyToVault());
            rootVisualElement.Add(_proximitySlider);
            rootVisualElement.Add(_curlSlider);
            rootVisualElement.Add(_qualitySlider);

            Button applyButton = new Button(ApplyToVault) { text = "Apply" };
            rootVisualElement.Add(applyButton);
            _waterfall = new TelemetryWaterfallElement();
            rootVisualElement.Add(_waterfall);
            _statusLabel = new Label();
            rootVisualElement.Add(_statusLabel);
            ApplyToVault();
            RefreshTelemetryBinding();
            rootVisualElement.schedule.Execute(RefreshTelemetryBinding).Every(250);
        }

        private static Slider CreateSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max)
            {
                value = value,
                showInputField = true
            };
            return slider;
        }

        private void ApplyToVault()
        {
            if (_proximitySlider == null || _curlSlider == null || _qualitySlider == null)
                return;

            EditorPrefs.SetFloat(ProximityKey, _proximitySlider.value);
            EditorPrefs.SetFloat(CurlKey, _curlSlider.value);
            EditorPrefs.SetFloat(QualityKey, _qualitySlider.value);

            if (!TryResolveEditorVault(out IDataVault vault))
            {
                SetStatus("Play Mode GlobalDataVault required for live tuning.");
                return;
            }

            if (vault.IsCompactionFenceActive)
            {
                SetStatus("GlobalDataVault compaction active; tuning write skipped.");
                return;
            }

            if (!TryAcquirePropwashWriteBuffer(
                    vault,
                    BufferID.PropwashGpuTuning,
                    1,
                    NativeArrayOptions.ClearMemory,
                    out VaultGenerationHandle<PropwashGpuTuningDTO> handle,
                    out NativeArray<PropwashGpuTuningDTO> tuning))
            {
                SetStatus("PropwashGpuTuning unavailable or write-locked.");
                return;
            }

            try
            {
                PropwashGpuTuningDTO dto = tuning[0];
                if (dto.Version == 0u)
                    dto = PropwashGpuContracts.CreateDefaultTuning();

                dto.SiltProximityMeters = _proximitySlider.value;
                dto.CurlNoiseFrequency = _curlSlider.value;
                dto.GlobalQualityWeightOverride = _qualitySlider.value;
                dto.Version = dto.Version == uint.MaxValue ? 1u : dto.Version + 1u;
                tuning[0] = dto;
                SetStatus("Applied PropwashGpuTuning.");
                RefreshTelemetryBinding();
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, OwnerSystem);
            }
        }

        private void RefreshTelemetryBinding()
        {
            if (_waterfall == null)
                return;

            if (TryResolveEditorVault(out IDataVault vault) &&
                !vault.IsCompactionFenceActive &&
                TryReadPropwashVaultBuffer(
                    vault,
                    BufferID.PropwashGpuTelemetryRing,
                    PropwashGpuContracts.TelemetryCapacity,
                    out VaultGenerationHandle<PropwashTelemetryEntry> handle,
                    out NativeArray<PropwashTelemetryEntry> _))
            {
                _waterfall.Bind(vault, handle);
            }
            else
            {
                _waterfall.Bind(null, default);
            }

            _waterfall.MarkDirtyRepaint();
        }

        private static bool TryResolveEditorVault(out IDataVault vault)
        {
            vault = null;
            if (!GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                return false;

            vault = latest;
            return true;
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value;
        }

        private static bool TryAcquirePropwashWriteBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsPropwashVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out NativeArray<T> existing) ||
                !existing.IsCreated ||
                existing.Length < requiredLength)
            {
                handle = vault.EnsureGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    OwnerSystem,
                    options);
            }

            if (!IsPropwashVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryAcquireWriteLock(in handle, OwnerSystem, out buffer))
                return false;

            bool handedOff = false;
            try
            {
                if (!buffer.IsCreated || buffer.Length < requiredLength)
                    return false;

                handedOff = true;
                return true;
            }
            finally
            {
                if (!handedOff)
                {
                    vault.ReleaseWriteLock(in handle, OwnerSystem);
                    handle = default;
                    buffer = default;
                }
            }
        }

        private static bool TryReadPropwashVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsPropwashVaultHandle(in handle, bufferId) ||
                !vault.TryReadHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPropwashVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private sealed class TelemetryWaterfallElement : VisualElement
        {
            private const float GraphHeight = 96f;
            private const float MaxGpuMicroseconds = 1500f;
            private IDataVault _vault;
            private VaultGenerationHandle<PropwashTelemetryEntry> _handle;

            public TelemetryWaterfallElement()
            {
                style.height = GraphHeight;
                style.marginTop = 8f;
                style.marginBottom = 8f;
                generateVisualContent += OnGenerateVisualContent;
            }

            public void Bind(IDataVault vault, VaultGenerationHandle<PropwashTelemetryEntry> handle)
            {
                _vault = vault;
                _handle = handle;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.10f, 0.12f, 0.13f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Stroke();

                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    !IsPropwashVaultHandle(in _handle, BufferID.PropwashGpuTelemetryRing) ||
                    rect.width <= 1f ||
                    rect.height <= 1f)
                    return;

                if (!_vault.TryReadHandle(in _handle, out NativeArray<PropwashTelemetryEntry> telemetry))
                    return;

                if (!telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                int count = Mathf.Min(telemetry.Length, PropwashGpuContracts.TelemetryCapacity);
                float maxBudget = 1f;
                for (int i = 0; i < count; i++)
                    maxBudget = Mathf.Max(maxBudget, telemetry[i].ParticleBudgetLimit);

                DrawLine(painter, rect, telemetry, count, maxBudget, 0);
                DrawLine(painter, rect, telemetry, count, MaxGpuMicroseconds, 1);
            }

            private static void DrawLine(
                Painter2D painter,
                Rect rect,
                NativeArray<PropwashTelemetryEntry> telemetry,
                int count,
                float scale,
                int mode)
            {
                if (count <= 1)
                    return;

                painter.lineWidth = mode == 0 ? 1.5f : 1f;
                painter.strokeColor = mode == 0
                    ? new Color(0.27f, 0.78f, 0.86f, 1f)
                    : new Color(0.98f, 0.58f, 0.22f, 1f);
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    PropwashTelemetryEntry entry = telemetry[i];
                    float value = mode == 0 ? entry.ParticleBudgetLimit : entry.EstimatedGpuMicroseconds;
                    float t = count <= 1 ? 0f : i / (float)(count - 1);
                    float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    float y = Mathf.Lerp(rect.yMax - 3f, rect.yMin + 3f, Mathf.Clamp01(value / Mathf.Max(1f, scale)));
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
