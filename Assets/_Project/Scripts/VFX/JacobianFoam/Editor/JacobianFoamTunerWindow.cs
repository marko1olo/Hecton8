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
    public sealed class JacobianFoamTunerWindow : EditorWindow
    {
        private const string PinchKey = "H8.JacobianFoam.PinchThreshold";
        private const string DecayKey = "H8.JacobianFoam.DecayRate";
        private const string ShorelineKey = "H8.JacobianFoam.ShorelineDepthFade";
        private const string QualityKey = "H8.JacobianFoam.QualityOverride";
        private static readonly ulong TuningMutationGuardMask =
            JacobianFoamMutationGuardBit(BufferID.JacobianFoamTuning);

        private Slider _pinchSlider;
        private Slider _decaySlider;
        private Slider _shorelineSlider;
        private Slider _qualitySlider;
        private Toggle _previewToggle;
        private Label _statusLabel;
        private Image _previewImage;
        private TelemetryGraphElement _telemetryGraph;

        [MenuItem("HECTON-8/Rendering/Jacobian Foam Tuner")]
        public static void Open()
        {
            GetWindow<JacobianFoamTunerWindow>("Jacobian Foam");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _pinchSlider = CreateSlider("Jacobian Pinch Threshold", 0.05f, 1.5f, EditorPrefs.GetFloat(PinchKey, 0.82f));
            _decaySlider = CreateSlider("Foam Decay Rate", 0.01f, 3f, EditorPrefs.GetFloat(DecayKey, 0.42f));
            _shorelineSlider = CreateSlider("Shoreline Depth Fade", 0.001f, 0.5f, EditorPrefs.GetFloat(ShorelineKey, 0.065f));
            _qualitySlider = CreateSlider("GlobalQualityWeight Override", -1f, 1f, EditorPrefs.GetFloat(QualityKey, -1f));
            _pinchSlider.RegisterValueChangedCallback(_ => ApplyToVault());
            _decaySlider.RegisterValueChangedCallback(_ => ApplyToVault());
            _shorelineSlider.RegisterValueChangedCallback(_ => ApplyToVault());
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyToVault());
            rootVisualElement.Add(_pinchSlider);
            rootVisualElement.Add(_decaySlider);
            rootVisualElement.Add(_shorelineSlider);
            rootVisualElement.Add(_qualitySlider);

            Button applyButton = new Button(ApplyToVault) { text = "Apply" };
            rootVisualElement.Add(applyButton);

            _telemetryGraph = new TelemetryGraphElement();
            rootVisualElement.Add(_telemetryGraph);

            _previewToggle = new Toggle("Live Foam Texture") { value = false };
            rootVisualElement.Add(_previewToggle);
            _previewImage = new Image();
            _previewImage.style.height = 192f;
            _previewImage.scaleMode = ScaleMode.ScaleToFit;
            rootVisualElement.Add(_previewImage);

            _statusLabel = new Label();
            rootVisualElement.Add(_statusLabel);

            ApplyToVault();
            RefreshBindings();
            rootVisualElement.schedule.Execute(RefreshBindings).Every(250);
        }

        private static Slider CreateSlider(string label, float min, float max, float value)
        {
            return new Slider(label, min, max)
            {
                value = value,
                showInputField = true
            };
        }

        private void ApplyToVault()
        {
            if (_pinchSlider == null || _decaySlider == null || _shorelineSlider == null || _qualitySlider == null)
                return;

            EditorPrefs.SetFloat(PinchKey, _pinchSlider.value);
            EditorPrefs.SetFloat(DecayKey, _decaySlider.value);
            EditorPrefs.SetFloat(ShorelineKey, _shorelineSlider.value);
            EditorPrefs.SetFloat(QualityKey, _qualitySlider.value);

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                SetStatus("Play Mode GlobalDataVault required for live tuning.");
                return;
            }

            if (vault.IsCompactionFenceActive)
            {
                SetStatus("GlobalDataVault compaction active; tuning write skipped.");
                return;
            }

            if (!vault.TryGetGenerationHandle(BufferID.JacobianFoamTuning, out VaultGenerationHandle<FoamTuningDTO> handle) ||
                !IsHandleCreated(in handle, BufferID.JacobianFoamTuning))
            {
                handle = vault.EnsureGenerationHandle<FoamTuningDTO>(
                    BufferID.JacobianFoamTuning,
                    1,
                    SystemID.Vfx,
                    NativeArrayOptions.ClearMemory);
            }

            if (!vault.TryAcquireMutationGuard(TuningMutationGuardMask))
            {
                SetStatus("JacobianFoamTuning locked by owner.");
                return;
            }

            try
            {
                if (!OpenLane(vault, in handle, BufferID.JacobianFoamTuning, 1, out NativeArray<FoamTuningDTO> tuning))
                {
                    SetStatus("JacobianFoamTuning buffer unavailable.");
                    return;
                }

                FoamTuningDTO dto = tuning[0];
                if (dto.Version == 0u)
                    dto = JacobianFoamContracts.CreateDefaultTuning();

                dto.PinchThreshold = _pinchSlider.value;
                dto.DecayRate = _decaySlider.value;
                dto.ShorelineDepthFade = _shorelineSlider.value;
                dto.GlobalQualityWeightOverride = _qualitySlider.value;
                dto.Version = dto.Version == uint.MaxValue ? 1u : dto.Version + 1u;
                tuning[0] = dto;
                SetStatus("Applied JacobianFoamTuning.");
                RefreshBindings();
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        private void RefreshBindings()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (_telemetryGraph != null &&
                vault != null &&
                !vault.IsCompactionFenceActive &&
                vault.TryGetGenerationHandle(BufferID.JacobianFoamTelemetryRing, out VaultGenerationHandle<FoamRenderTelemetryEntry> telemetryHandle) &&
                IsHandleCreated(in telemetryHandle, BufferID.JacobianFoamTelemetryRing))
            {
                _telemetryGraph.Bind(vault, telemetryHandle);
            }
            else if (_telemetryGraph != null)
            {
                _telemetryGraph.Bind(null, default);
            }

            if (_telemetryGraph != null)
                _telemetryGraph.MarkDirtyRepaint();

            if (_previewImage != null)
            {
                bool preview = _previewToggle != null && _previewToggle.value;
                _previewImage.image = preview && JacobianFoamGpuRuntime.TryReadFoamPreviewTexture(out RenderTexture foamTexture) ? foamTexture : null;
            }
        }

        private void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value;
        }

        private sealed class TelemetryGraphElement : VisualElement
        {
            private const float GraphHeight = 96f;
            private const float MaxGpuMicroseconds = 1500f;
            private IDataVault _vault;
            private VaultGenerationHandle<FoamRenderTelemetryEntry> _handle;

            public TelemetryGraphElement()
            {
                style.height = GraphHeight;
                style.marginTop = 8f;
                style.marginBottom = 8f;
                generateVisualContent += OnGenerateVisualContent;
            }

            public void Bind(IDataVault vault, VaultGenerationHandle<FoamRenderTelemetryEntry> handle)
            {
                _vault = vault;
                _handle = handle;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1f;
                painter.strokeColor = new Color(0.08f, 0.10f, 0.11f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Stroke();

                if (_vault == null ||
                    _vault.IsCompactionFenceActive ||
                    !IsHandleCreated(in _handle, BufferID.JacobianFoamTelemetryRing) ||
                    rect.width <= 1f ||
                    rect.height <= 1f)
                {
                    return;
                }

                if (!OpenReadLane(_vault, in _handle, BufferID.JacobianFoamTelemetryRing, JacobianFoamContracts.TelemetryCapacity, out NativeArray<FoamRenderTelemetryEntry> telemetry))
                    return;

                int count = Mathf.Min(telemetry.Length, JacobianFoamContracts.TelemetryCapacity);
                DrawLine(painter, rect, telemetry, count, MaxGpuMicroseconds, 0);
                DrawLine(painter, rect, telemetry, count, 1f, 1);
            }

            private static void DrawLine(
                Painter2D painter,
                Rect rect,
                NativeArray<FoamRenderTelemetryEntry> telemetry,
                int count,
                float scale,
                int mode)
            {
                if (count <= 1)
                    return;

                painter.lineWidth = mode == 0 ? 1.5f : 1f;
                painter.strokeColor = mode == 0
                    ? new Color(0.22f, 0.82f, 0.92f, 1f)
                    : new Color(0.92f, 0.92f, 0.32f, 1f);
                painter.BeginPath();
                for (int i = 0; i < count; i++)
                {
                    FoamRenderTelemetryEntry entry = telemetry[i];
                    float value = mode == 0 ? entry.EstimatedGpuMicroseconds : entry.ResolutionScale;
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

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        private static ulong JacobianFoamMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static bool OpenLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsHandleCreated(in handle, bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool OpenReadLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   IsHandleCreated(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }
    }
}
#endif
