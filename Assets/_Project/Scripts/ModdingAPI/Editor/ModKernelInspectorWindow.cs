#if UNITY_EDITOR
using Hecton8.Modding;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class ModKernelInspectorWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.10d;
        private readonly KernelHistogramElement _histogram = new KernelHistogramElement();
        private Label _statusLabel;
        private Label _shedLabel;
        private Slider _intensitySlider;
        private FloatField _durationField;
        private FloatField _rangeField;
        private IntegerField _ttlField;
        private uint _lastShedTotal;
        private double _nextRefreshTime;

        [MenuItem("HECTON-8/Mod Kernel Inspector")]
        public static void Open()
        {
            GetWindow<ModKernelInspectorWindow>("Mod Kernel Inspector");
        }

        public void CreateGUI()
        {
            FutureCommandSandboxValidator.Initialize();

            rootVisualElement.style.paddingLeft = 8f;
            rootVisualElement.style.paddingRight = 8f;
            rootVisualElement.style.paddingTop = 8f;
            rootVisualElement.style.paddingBottom = 8f;

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.Add(new Button(() => FutureCommandSandboxValidator.TryReloadKernelTuningProfilesCsvFromDisk()) { text = "Reload CSV" });
            toolbar.Add(new Button(() => FutureCommandSandboxValidator.RunSelfAudit()) { text = "Self Audit" });
            toolbar.Add(new Button(() => Inject(FutureCommandOpcodes.SurvivalOverride)) { text = "Inject Survival" });
            toolbar.Add(new Button(() => Inject(FutureCommandOpcodes.HapticPulse)) { text = "Inject Haptic" });
            toolbar.Add(new Button(() => Inject(FutureCommandOpcodes.SubtitleCue)) { text = "Inject Subtitle" });
            rootVisualElement.Add(toolbar);

            _intensitySlider = new Slider("Intensity", 0f, 1f) { value = 0.65f };
            _durationField = new FloatField("Duration") { value = 0.35f };
            _rangeField = new FloatField("Range") { value = 32f };
            _ttlField = new IntegerField("TTL") { value = 180 };
            rootVisualElement.Add(_intensitySlider);
            rootVisualElement.Add(_durationField);
            rootVisualElement.Add(_rangeField);
            rootVisualElement.Add(_ttlField);

            _statusLabel = new Label();
            _shedLabel = new Label();
            rootVisualElement.Add(_statusLabel);
            rootVisualElement.Add(_shedLabel);
            rootVisualElement.Add(_histogram);

            _histogram.style.height = 160f;
            _histogram.style.marginTop = 8f;
            EditorApplication.update += Tick;
            Tick();
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + RefreshIntervalSeconds;
            FutureCommandSandboxValidator.Initialize();
            FutureCommandSandboxTuning tuning = FutureCommandSandboxValidator.GetTuningSnapshot();
            uint shedTotal = 0u;
            uint survival = 0u;
            uint haptic = 0u;
            uint subtitle = 0u;
            uint rejected = 0u;
            for (int i = 0; i < FutureCommandSandboxConstants.KernelTelemetryCapacity; i++)
            {
                if (!FutureCommandSandboxValidator.TryGetKernelTelemetryEntry(i, out KernelExecutionTelemetryEntry entry))
                    continue;

                shedTotal += entry.ShedByThermal;
                survival += entry.SurvivalProcessed;
                haptic += entry.HapticProcessed;
                subtitle += entry.SubtitleProcessed;
                rejected += entry.Rejected;
            }

            _statusLabel.text = $"quality {tuning.GlobalQualityWeightOverride:0.00} thermal {tuning.CpuThermalPressure01:0.00} pending {FutureCommandSandboxValidator.GetPendingEnvelopeCount()}";
            _shedLabel.text = $"survival {survival} haptic {haptic} subtitle {subtitle} shed {shedTotal} rejected {rejected}";
            _histogram.MarkDirtyRepaint();

            if (shedTotal != _lastShedTotal)
            {
                _lastShedTotal = shedTotal;
                rootVisualElement.style.backgroundColor = new Color(0.35f, 0.03f, 0.02f, 0.55f);
            }
            else
            {
                rootVisualElement.style.backgroundColor = new Color(0.04f, 0.045f, 0.05f, 1f);
            }
        }

        private void Inject(uint opcodeHash)
        {
            FutureCommandEnvelope envelope = default;
            envelope.OpcodeHash = opcodeHash;
            envelope.ModderSignature = 0x53483132u;
            envelope.TargetAUP = double3.zero;
            if (opcodeHash == FutureCommandOpcodes.SurvivalOverride)
            {
                envelope.PayloadData = new float4(0.33f, math.max(1, _ttlField.value), 0f, 0f);
            }
            else if (opcodeHash == FutureCommandOpcodes.HapticPulse)
            {
                envelope.PayloadData = new float4(
                    math.asfloat(0x48505431u),
                    math.saturate(_intensitySlider.value),
                    math.max(0.01f, _durationField.value),
                    math.max(1f, _rangeField.value));
            }
            else
            {
                envelope.PayloadData = new float4(math.asfloat(0x53554231u), math.max(0.05f, _durationField.value), 4f, 0f);
            }

            envelope.IntegrityHash = FutureCommandSandboxValidator.ComputeIntegrityHash(in envelope);
            FutureCommandSandboxValidator.Request(in envelope);
        }

        private sealed class KernelHistogramElement : VisualElement
        {
            public KernelHistogramElement()
            {
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.fillColor = new Color(0.08f, 0.085f, 0.09f, 1f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                painter.ClosePath();
                painter.Fill();

                uint peak = 1u;
                for (int i = 0; i < FutureCommandSandboxConstants.KernelTelemetryCapacity; i++)
                {
                    if (!FutureCommandSandboxValidator.TryGetKernelTelemetryEntry(i, out KernelExecutionTelemetryEntry entry))
                        continue;

                    peak = math.max(peak, entry.SurvivalProcessed + entry.HapticProcessed + entry.SubtitleProcessed);
                    peak = math.max(peak, entry.ShedByThermal + entry.Rejected);
                }

                float barWidth = math.max(1f, rect.width / FutureCommandSandboxConstants.KernelTelemetryCapacity);
                for (int i = 0; i < FutureCommandSandboxConstants.KernelTelemetryCapacity; i++)
                {
                    if (!FutureCommandSandboxValidator.TryGetKernelTelemetryEntry(i, out KernelExecutionTelemetryEntry entry))
                        continue;

                    float x = rect.xMin + i * barWidth;
                    float processedHeight = rect.height * ((entry.SurvivalProcessed + entry.HapticProcessed + entry.SubtitleProcessed) / (float)peak);
                    float shedHeight = rect.height * ((entry.ShedByThermal + entry.Rejected) / (float)peak);
                    FillRect(painter, new Rect(x, rect.yMax - processedHeight, barWidth * 0.45f, processedHeight), new Color(0.15f, 0.65f, 0.42f, 1f));
                    FillRect(painter, new Rect(x + barWidth * 0.5f, rect.yMax - shedHeight, barWidth * 0.45f, shedHeight), new Color(0.9f, 0.18f, 0.12f, 1f));
                }
            }

            private static void FillRect(Painter2D painter, Rect rect, Color color)
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
        }
    }
}
#endif
