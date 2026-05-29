#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Atmosphere.Editor
{
    public sealed unsafe class ShinobuStormPropagationTunerWindow : EditorWindow
    {
        private const SystemID OwnerSystem = SystemID.HabitatAtmosphere;
        private static readonly ulong TuningMutationGuardMask =
            StormPropagationMutationGuardBit(BufferID.ShinobuStormPropagationTuning);
        private static readonly ulong TelemetryGraphMutationGuardMask =
            StormPropagationMutationGuardBit(BufferID.ShinobuStormPropagationTelemetryRing) |
            StormPropagationMutationGuardBit(BufferID.ShinobuStormPropagationTelemetryCursor);

        private Label _status;
        private Slider _decay;
        private Slider _turbidity;
        private Slider _maxTurbidity;
        private Slider _surge;
        private Slider _acoustic;
        private Slider _biolum;
        private Slider _noise;
        private Slider _cadence;
        private Slider _quality;
        private VisualElement _graph;

        [MenuItem("Hecton8/Atmosphere/SHINOBU Storm Propagation Tuner")]
        public static void Open()
        {
            ShinobuStormPropagationTunerWindow window = GetWindow<ShinobuStormPropagationTunerWindow>();
            window.titleContent = new GUIContent("Storm Propagation");
            window.minSize = new Vector2(420f, 420f);
            window.RefreshFromVault();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _status = new Label("Vault state: unresolved");
            root.Add(_status);

            _graph = new VisualElement();
            _graph.style.height = 112f;
            _graph.style.marginTop = 6f;
            _graph.style.marginBottom = 6f;
            _graph.generateVisualContent += DrawTelemetryGraph;
            root.Add(_graph);

            _decay = CreateSlider(root, "Depth Decay", 0.0001f, 0.006f);
            _turbidity = CreateSlider(root, "Turbidity Gain", 0f, 4f);
            _maxTurbidity = CreateSlider(root, "Max Turbidity Multiplier", 1f, 8f);
            _surge = CreateSlider(root, "Surge Scale", 0f, 0.35f);
            _acoustic = CreateSlider(root, "Acoustic Muffle", 0f, 1.5f);
            _biolum = CreateSlider(root, "Biolum Stimulus", 0f, 2f);
            _noise = CreateSlider(root, "Noise Scale", 0f, 1.5f);
            _cadence = CreateSlider(root, "Cadence Hz", ShinobuStormPropagationConstants.MinimumPublicationCadenceHz, 60f);
            _quality = CreateSlider(root, "GlobalQualityWeight Preview", 0f, 1f);

            Button refresh = new Button(RefreshFromVault) { text = "Refresh" };
            Button apply = new Button(ApplyToVault) { text = "Apply" };
            root.Add(refresh);
            root.Add(apply);
            RefreshFromVault();
            root.schedule.Execute(RefreshGraph).Every(500);
        }

        private Slider CreateSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => ApplyToVault());
            root.Add(slider);
            return slider;
        }

        private void RefreshFromVault()
        {
            if (!CopyTuningSnapshotFromVault(out StormPropagationTuningDTO tuning))
            {
                if (_status != null)
                    _status.text = "Vault state: tuning buffer unavailable";
                return;
            }

            SetWithoutNotify(_decay, tuning.DecayConstant);
            SetWithoutNotify(_turbidity, tuning.TurbidityGain);
            SetWithoutNotify(_maxTurbidity, tuning.FogBaseDensityExtinction.z);
            SetWithoutNotify(_surge, tuning.SurgeScale);
            SetWithoutNotify(_acoustic, tuning.AcousticMufflingGain);
            SetWithoutNotify(_biolum, tuning.BiolumDeltaGain);
            SetWithoutNotify(_noise, tuning.NoiseScale);
            SetWithoutNotify(_cadence, tuning.PublicationCadenceHz);
            SetWithoutNotify(_quality, tuning.GlobalQualityWeight);
            if (_status != null)
                _status.text = "Vault state: tuning buffer resolved. Graph: surface intensity vs attenuated depth energy.";
            RefreshGraph();
        }

        private void ApplyToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.ShinobuStormPropagationTuning, out VaultGenerationHandle<StormPropagationTuningDTO> handle) ||
                    !vault.TryResolveHandle(in handle, out NativeArray<StormPropagationTuningDTO> tuning) ||
                    !tuning.IsCreated ||
                    tuning.Length <= 0)
                {
                    return;
                }

                ref StormPropagationTuningDTO row = ref ShinobuStormPropagationNative.ElementAt(tuning, 0);
                row = ShinobuStormPropagationNative.SanitizeTuning(row, 1f);

                row.DecayConstant = ReadSlider(_decay, row.DecayConstant);
                row.TurbidityGain = ReadSlider(_turbidity, row.TurbidityGain);
                row.FogBaseDensityExtinction.z = math.clamp(ReadSlider(_maxTurbidity, row.FogBaseDensityExtinction.z), 1f, 8f);
                row.SurgeScale = ReadSlider(_surge, row.SurgeScale);
                row.AcousticMufflingGain = ReadSlider(_acoustic, row.AcousticMufflingGain);
                row.BiolumDeltaGain = ReadSlider(_biolum, row.BiolumDeltaGain);
                row.NoiseScale = ReadSlider(_noise, row.NoiseScale);
                row.PublicationCadenceHz = ReadSlider(_cadence, row.PublicationCadenceHz);
                row.GlobalQualityWeight = math.saturate(ReadSlider(_quality, row.GlobalQualityWeight));
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        private bool CopyTuningSnapshotFromVault(out StormPropagationTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireMutationGuard(TuningMutationGuardMask))
                return false;

            try
            {
                if (!vault.TryGetGenerationHandle(BufferID.ShinobuStormPropagationTuning, out VaultGenerationHandle<StormPropagationTuningDTO> handle) ||
                    !vault.TryResolveHandle(in handle, out NativeArray<StormPropagationTuningDTO> values) ||
                    !values.IsCreated ||
                    values.Length <= 0)
                {
                    return false;
                }

                tuning = ShinobuStormPropagationNative.SanitizeTuning(ShinobuStormPropagationNative.ReadElement(values, 0), 1f);
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(TuningMutationGuardMask);
            }
        }

        private static void SetWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private static float ReadSlider(Slider slider, float fallback)
        {
            return slider != null && math.isfinite(slider.value) ? slider.value : fallback;
        }

        private void RefreshGraph()
        {
            _graph?.MarkDirtyRepaint();
        }

        private void DrawTelemetryGraph(MeshGenerationContext context)
        {
            Rect rect = _graph.contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            DrawRect(painter, rect, new Color(0.012f, 0.018f, 0.022f, 1f));

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!vault.TryAcquireMutationGuard(TelemetryGraphMutationGuardMask))
                return;

            try
            {
                if (!BorrowTelemetryRingViewUnsafe(vault, out NativeArray<StormPropagationTelemetryEntry> telemetry) ||
                    !BorrowTelemetryCursorViewUnsafe(vault, out NativeArray<int> cursor) ||
                    telemetry.Length <= 1 ||
                    cursor.Length <= 0)
                {
                    return;
                }

                int writeCursor = ShinobuStormPropagationMath.WrapRingIndex(ShinobuStormPropagationNative.ReadElement(cursor, 0), telemetry.Length);
                DrawTelemetryLine(painter, rect, telemetry, writeCursor, true, new Color(0.18f, 0.82f, 0.96f, 1f));
                DrawTelemetryLine(painter, rect, telemetry, writeCursor, false, new Color(0.95f, 0.42f, 0.12f, 0.95f));
            }
            finally
            {
                vault.ReleaseMutationGuard(TelemetryGraphMutationGuardMask);
            }
        }

        private static void DrawTelemetryLine(Painter2D painter, Rect rect, NativeArray<StormPropagationTelemetryEntry> telemetry, int writeCursor, bool attenuated, Color color)
        {
            painter.lineWidth = attenuated ? 1.5f : 1f;
            painter.strokeColor = color;
            painter.BeginPath();
            bool hasPoint = false;
            int count = telemetry.Length;
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = (writeCursor + i) % count;
                StormPropagationTelemetryEntry entry = ShinobuStormPropagationNative.ReadElement(telemetry, sourceIndex);
                float sample = attenuated ? entry.AttenuatedEnergy01 : entry.SurfaceIntensity01;
                float x = rect.xMin + rect.width * (i / (float)(count - 1));
                float y = rect.yMax - math.saturate(sample) * rect.height;
                if (!hasPoint)
                {
                    painter.MoveTo(new Vector2(x, y));
                    hasPoint = true;
                }
                else
                {
                    painter.LineTo(new Vector2(x, y));
                }
            }

            if (hasPoint)
                painter.Stroke();
        }

        private static void DrawRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }

        private static bool BorrowTelemetryRingViewUnsafe(IDataVault vault, out NativeArray<StormPropagationTelemetryEntry> telemetry)
        {
            telemetry = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return vault.TryGetGenerationHandle(BufferID.ShinobuStormPropagationTelemetryRing, out VaultGenerationHandle<StormPropagationTelemetryEntry> handle) &&
                   vault.TryResolveHandle(in handle, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length > 0;
        }

        private static bool BorrowTelemetryCursorViewUnsafe(IDataVault vault, out NativeArray<int> cursor)
        {
            cursor = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return vault.TryGetGenerationHandle(BufferID.ShinobuStormPropagationTelemetryCursor, out VaultGenerationHandle<int> handle) &&
                   vault.TryResolveHandle(in handle, out cursor) &&
                   cursor.IsCreated &&
                   cursor.Length > 0;
        }

        private static ulong StormPropagationMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }
    }
}
#endif
