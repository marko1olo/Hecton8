#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    [InitializeOnLoad]
    internal static class AbyssalCavitationEditorLayoutGuard
    {
        static AbyssalCavitationEditorLayoutGuard()
        {
            AbyssalCavitationLayout.ValidateOrThrow();
        }
    }

    public sealed class AbyssalCavitationTunerWindow : EditorWindow
    {
        private const int HistogramBinCount = 16;
        private delegate void TuningMutator(ref AbyssalCavitationTuningDTO tuning);

        private Label _status;
        private Slider _quality;
        private Slider _inverseSquare;
        private Slider _epsilonClamp;
        private Slider _forceScale;
        private Slider _minPressure;
        private Slider _sdfDampening;
        private Slider _sdfSoftness;
        private Slider _visualIntensity;
        private Slider _maxForce;
        private Slider _shellMeters;
        private Label _telemetry;
        private double _nextTelemetryRefreshTime;
        private readonly char[] _telemetryScratch = new char[192];
        private readonly VisualElement[] _histogramBars = new VisualElement[HistogramBinCount];
        private uint _lastTelemetryFrameIndex = uint.MaxValue;
        private uint _lastTelemetryFlags = uint.MaxValue;
        private uint _lastTelemetryPeakPressureBits;
        private uint _lastTelemetryPeakForceBits;
        private uint _lastTelemetryCpuBits;
        private int _lastTelemetryActive = int.MinValue;
        private int _lastTelemetryCandidates = int.MinValue;
        private bool _telemetryUnavailable = true;

        [MenuItem("Hecton8/Physics/Abyssal Cavitation Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalCavitationTunerWindow>("Cavitation");
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
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _status = new Label("SHINOBU_248 cavitation runtime");
            rootVisualElement.Add(_status);

            _quality = AddSlider("Global Quality Weight", 0f, 1f, OnQualityChanged);
            _inverseSquare = AddSlider("Inverse Square Multiplier", 0.0001f, 16f, OnInverseSquareChanged);
            _epsilonClamp = AddSlider("Epsilon Clamp Value", 0.000001f, 0.01f, OnEpsilonClampChanged);
            _forceScale = AddSlider("Force Scale", 0.00001f, 0.08f, OnForceScaleChanged);
            _minPressure = AddSlider("Minimum Pressure", 0f, 500f, OnMinPressureChanged);
            _sdfDampening = AddSlider("SDF Occlusion Dampening", 0f, 1f, OnSdfDampeningChanged);
            _sdfSoftness = AddSlider("SDF Softness Meters", 0.05f, 24f, OnSdfSoftnessChanged);
            _visualIntensity = AddSlider("Visual Intensity", 0f, 4f, OnVisualIntensityChanged);
            _maxForce = AddSlider("Max Force Newton", 100f, 120000f, OnMaxForceChanged);
            _shellMeters = AddSlider("Shell Meters", 0.05f, 12f, OnShellMetersChanged);

            Button loadCsv = new Button(LoadCsv) { text = "Load ordnance_blast_profiles.csv" };
            Button mock = new Button(InjectMock) { text = "Inject 10 mock detonations" };
            Button singularity = new Button(InjectSingularity) { text = "Inject singularity mock" };
            Button shader = new Button(SyncShader) { text = "Sync shader buffer" };
            rootVisualElement.Add(loadCsv);
            rootVisualElement.Add(mock);
            rootVisualElement.Add(singularity);
            rootVisualElement.Add(shader);
            _telemetry = new Label("Telemetry: --");
            rootVisualElement.Add(_telemetry);
            CreateHistogram();

            RefreshFromRuntime();
            RefreshTelemetryReadout(force: true);
        }

        private void CreateHistogram()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            row.style.height = 48;
            row.style.marginTop = 6;
            rootVisualElement.Add(row);

            for (int i = 0; i < HistogramBinCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 8;
                bar.style.height = 2;
                bar.style.marginRight = 3;
                bar.style.backgroundColor = new Color(0.3f, 0.75f, 1f, 0.7f);
                row.Add(bar);
                _histogramBars[i] = bar;
            }
        }

        private Slider AddSlider(string label, float low, float high, System.Action<float> callback)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt => callback(evt.newValue));
            rootVisualElement.Add(slider);
            return slider;
        }

        private void RefreshFromRuntime()
        {
            if (!AbyssalCavitationRuntime.EnsureInitialized() ||
                !AbyssalCavitationRuntime.TryGetTuning(out AbyssalCavitationTuningDTO tuning))
            {
                _status.text = "Runtime unavailable";
                return;
            }

            _quality.SetValueWithoutNotify(math.saturate(HomeostasisBrain.GlobalQualityWeight));
            _inverseSquare.SetValueWithoutNotify(tuning.InverseSquareMultiplier);
            _epsilonClamp.SetValueWithoutNotify(tuning.EpsilonClampValue);
            _forceScale.SetValueWithoutNotify(tuning.ForceScale);
            _minPressure.SetValueWithoutNotify(tuning.MinPressure);
            _sdfDampening.SetValueWithoutNotify(tuning.SdfOcclusionDampening);
            _sdfSoftness.SetValueWithoutNotify(tuning.SdfSoftnessMeters);
            _visualIntensity.SetValueWithoutNotify(tuning.VisualIntensityScale);
            _maxForce.SetValueWithoutNotify(tuning.MaxForceNewton);
            _shellMeters.SetValueWithoutNotify(tuning.CavitationShellMeters);
            string csv = AbyssalCavitationRuntime.IsCsvLoaded() ? "loaded" : "not loaded";
            _status.text = "Runtime ready | CSV " + csv;
        }

        private void RefreshTelemetryReadout()
        {
            RefreshTelemetryReadout(false);
        }

        private void RefreshTelemetryReadout(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = EditorApplication.timeSinceStartup + 0.2;
            if (_telemetry == null)
                return;

            if (AbyssalCavitationRuntime.TrySampleLatestTelemetry(out ShockwaveTelemetryEntry telemetry))
            {
                uint peakPressureBits = math.asuint(telemetry.PeakPressure);
                uint peakForceBits = math.asuint(telemetry.PeakForce);
                uint cpuBits = math.asuint(telemetry.CpuMicroseconds);
                if (!force &&
                    !_telemetryUnavailable &&
                    _lastTelemetryFrameIndex == telemetry.FrameIndex &&
                    _lastTelemetryFlags == telemetry.Flags &&
                    _lastTelemetryPeakPressureBits == peakPressureBits &&
                    _lastTelemetryPeakForceBits == peakForceBits &&
                    _lastTelemetryCpuBits == cpuBits &&
                    _lastTelemetryActive == telemetry.ActiveShockwaves &&
                    _lastTelemetryCandidates == telemetry.CandidateCount)
                {
                    return;
                }

                _lastTelemetryFrameIndex = telemetry.FrameIndex;
                _lastTelemetryFlags = telemetry.Flags;
                _lastTelemetryPeakPressureBits = peakPressureBits;
                _lastTelemetryPeakForceBits = peakForceBits;
                _lastTelemetryCpuBits = cpuBits;
                _lastTelemetryActive = telemetry.ActiveShockwaves;
                _lastTelemetryCandidates = telemetry.CandidateCount;
                _telemetryUnavailable = false;
                int length = FormatTelemetry(in telemetry, _telemetryScratch);
                _telemetry.text = new string(_telemetryScratch, 0, length);
                RefreshHistogram();
                return;
            }

            if (!_telemetryUnavailable || force)
            {
                _telemetryUnavailable = true;
                _telemetry.text = "Telemetry: --";
                ClearHistogram();
            }
        }

        private void RefreshHistogram()
        {
            for (int i = 0; i < HistogramBinCount; i++)
            {
                VisualElement bar = _histogramBars[i];
                if (bar == null)
                    continue;

                if (!AbyssalCavitationRuntime.TrySampleTelemetryEntry(HistogramBinCount - 1 - i, out ShockwaveTelemetryEntry telemetry))
                {
                    bar.style.height = 2;
                    bar.style.backgroundColor = new Color(0.08f, 0.12f, 0.14f, 0.6f);
                    continue;
                }

                float forceHeight = math.clamp(telemetry.PeakForce * 0.0008f, 2f, 46f);
                float clampBoost = telemetry.EpsilonClampCount > 0 ? math.min(8f, telemetry.EpsilonClampCount * 2f) : 0f;
                bar.style.height = math.min(48f, forceHeight + clampBoost);
                bar.style.backgroundColor = telemetry.EpsilonClampCount > 0
                    ? new Color(1f, 0.08f, 0.02f, 0.9f)
                    : new Color(0.3f, 0.75f, 1f, 0.75f);
            }
        }

        private void ClearHistogram()
        {
            for (int i = 0; i < HistogramBinCount; i++)
            {
                VisualElement bar = _histogramBars[i];
                if (bar == null)
                    continue;

                bar.style.height = 2;
                bar.style.backgroundColor = new Color(0.08f, 0.12f, 0.14f, 0.6f);
            }
        }

        private static int FormatTelemetry(in ShockwaveTelemetryEntry telemetry, char[] buffer)
        {
            int cursor = 0;
            cursor = AppendLiteral(buffer, cursor, "Active ");
            cursor = AppendInt(buffer, cursor, telemetry.ActiveShockwaves);
            cursor = AppendLiteral(buffer, cursor, " | Candidates ");
            cursor = AppendInt(buffer, cursor, telemetry.CandidateCount);
            cursor = AppendLiteral(buffer, cursor, " | Peak pressure ");
            cursor = AppendFixed1(buffer, cursor, telemetry.PeakPressure);
            cursor = AppendLiteral(buffer, cursor, " | Peak force ");
            cursor = AppendFixed1(buffer, cursor, telemetry.PeakForce);
            cursor = AppendLiteral(buffer, cursor, " | Affected ");
            cursor = AppendInt(buffer, cursor, telemetry.AffectedEntities);
            cursor = AppendLiteral(buffer, cursor, " | Eps ");
            cursor = AppendInt(buffer, cursor, telemetry.EpsilonClampCount);
            cursor = AppendLiteral(buffer, cursor, " | CPU us ");
            cursor = AppendFixed1(buffer, cursor, telemetry.CpuMicroseconds);
            cursor = AppendLiteral(buffer, cursor, " | Flags 0x");
            cursor = AppendHex8(buffer, cursor, telemetry.Flags);
            return math.clamp(cursor, 0, buffer.Length);
        }

        private static int AppendLiteral(char[] buffer, int cursor, string literal)
        {
            for (int i = 0; i < literal.Length && cursor < buffer.Length; i++)
                buffer[cursor++] = literal[i];
            return cursor;
        }

        private static int AppendFixed1(char[] buffer, int cursor, float value)
        {
            if (!math.isfinite(value))
                return AppendLiteral(buffer, cursor, "--");

            int scaled = (int)math.round(math.clamp(value, -9999999f, 9999999f) * 10f);
            if (scaled < 0)
            {
                if (cursor < buffer.Length)
                    buffer[cursor++] = '-';
                scaled = -scaled;
            }

            cursor = AppendInt(buffer, cursor, scaled / 10);
            if (cursor < buffer.Length)
                buffer[cursor++] = '.';
            if (cursor < buffer.Length)
                buffer[cursor++] = (char)('0' + math.abs(scaled % 10));
            return cursor;
        }

        private static int AppendInt(char[] buffer, int cursor, int value)
        {
            if (value == int.MinValue)
                return AppendLiteral(buffer, cursor, "-2147483648");

            if (value < 0)
            {
                if (cursor < buffer.Length)
                    buffer[cursor++] = '-';
                value = -value;
            }

            int start = cursor;
            do
            {
                if (cursor >= buffer.Length)
                    return cursor;
                buffer[cursor++] = (char)('0' + (value % 10));
                value /= 10;
            }
            while (value > 0);

            int end = cursor - 1;
            while (start < end)
            {
                char tmp = buffer[start];
                buffer[start] = buffer[end];
                buffer[end] = tmp;
                start++;
                end--;
            }

            return cursor;
        }

        private static int AppendHex8(char[] buffer, int cursor, uint value)
        {
            for (int shift = 28; shift >= 0 && cursor < buffer.Length; shift -= 4)
            {
                int digit = (int)((value >> shift) & 0xFu);
                buffer[cursor++] = (char)(digit < 10 ? '0' + digit : 'A' + digit - 10);
            }

            return cursor;
        }

        private void Mutate(TuningMutator mutator)
        {
            if (!AbyssalCavitationRuntime.EnsureInitialized() ||
                !AbyssalCavitationRuntime.TryGetTuning(out AbyssalCavitationTuningDTO tuning))
                return;

            mutator(ref tuning);
            AbyssalCavitationRuntime.TryApplyTuning(in tuning);
            RefreshFromRuntime();
        }

        private void OnQualityChanged(float value)
        {
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(math.saturate(value), true);
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.GlobalQualityWeight = math.saturate(value));
        }

        private void OnForceScaleChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.ForceScale = value);
        }

        private void OnInverseSquareChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.InverseSquareMultiplier = value);
        }

        private void OnEpsilonClampChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.EpsilonClampValue = value);
        }

        private void OnMinPressureChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.MinPressure = value);
        }

        private void OnSdfDampeningChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) =>
            {
                tuning.SdfOcclusionDampening = value;
                tuning.SdfHardDampening = value;
            });
        }

        private void OnSdfSoftnessChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.SdfSoftnessMeters = value);
        }

        private void OnVisualIntensityChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.VisualIntensityScale = value);
        }

        private void OnMaxForceChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.MaxForceNewton = value);
        }

        private void OnShellMetersChanged(float value)
        {
            Mutate((ref AbyssalCavitationTuningDTO tuning) => tuning.CavitationShellMeters = value);
        }

        private void LoadCsv()
        {
            bool loaded = AbyssalCavitationRuntime.TryLoadDefaultOrdnanceCsv(true);
            _status.text = loaded ? "CSV loaded" : "CSV missing or rejected";
            RefreshFromRuntime();
        }

        private void InjectMock()
        {
            bool injected = AbyssalCavitationRuntime.GenerateMockDetonations();
            _status.text = injected ? "Mock detonations injected" : "Mock injection rejected";
        }

        private void InjectSingularity()
        {
            bool injected = AbyssalCavitationRuntime.GenerateMockSingularityExplosion();
            _status.text = injected ? "Singularity mock injected" : "Singularity injection rejected";
        }

        private void SyncShader()
        {
            int count = AbyssalCavitationRuntime.SyncShaderVisuals();
            _status.text = "Shader spheres: " + count;
        }
    }
}
#endif
