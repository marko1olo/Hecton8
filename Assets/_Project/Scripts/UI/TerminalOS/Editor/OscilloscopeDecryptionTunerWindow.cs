#if UNITY_EDITOR
using Hecton8.Core.Contracts;
using Hecton8.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class OscilloscopeDecryptionTunerWindow : EditorWindow
    {
        private const string WindowTitle = "Oscilloscope Decryption Tuner";
        private const double PollIntervalSeconds = 0.12d;

        private TerminalOsRuntime _runtime;
        private ObjectField _runtimeField;
        private IntegerField _terminalIndexField;
        private Slider _baseFrequency;
        private Slider _snapTolerance;
        private Slider _noiseDensity;
        private Slider _qualityOverride;
        private Slider _frequencyWeight;
        private Slider _phaseWeight;
        private Slider _frequencySensitivity;
        private Slider _phaseSensitivity;
        private Slider _targetFrequency;
        private Slider _targetPhase;
        private IntegerField _puzzleIdReadout;
        private FloatField _playerFrequencyReadout;
        private FloatField _playerPhaseReadout;
        private FloatField _targetFrequencyReadout;
        private FloatField _targetPhaseReadout;
        private FloatField _accuracyReadout;
        private IntegerField _flagsReadout;
        private IntegerField _telemetryFrameReadout;
        private FloatField _burstReadout;
        private Label _status;
        private double _nextPollTime;

        [MenuItem("Tools/HECTON-8/Oscilloscope Decryption Tuner")]
        public static void Open()
        {
            GetWindow<OscilloscopeDecryptionTunerWindow>(WindowTitle);
        }

        private void OnEnable()
        {
            EditorApplication.update -= PollRuntime;
            EditorApplication.update += PollRuntime;
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollRuntime;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _runtimeField = new ObjectField("Runtime")
            {
                objectType = typeof(TerminalOsRuntime),
                allowSceneObjects = true
            };
            _runtimeField.RegisterValueChangedCallback(evt =>
            {
                _runtime = evt.newValue as TerminalOsRuntime;
                SyncTuningFromRuntime();
                RefreshReadout();
            });
            root.Add(_runtimeField);

            root.Add(new Button(FindRuntime)
            {
                text = "Find Runtime"
            });

            _terminalIndexField = new IntegerField("Terminal Index")
            {
                value = 0
            };
            _terminalIndexField.RegisterValueChangedCallback(_ => RefreshReadout());
            root.Add(_terminalIndexField);

            _baseFrequency = AddSlider(root, "Base Frequency", 0.1f, 12f, 3.25f);
            _snapTolerance = AddSlider(root, "Snap Tolerance", 0.001f, 0.2f, 0.02f);
            _noiseDensity = AddSlider(root, "Noise Density", 0f, 2f, 1f);
            _qualityOverride = AddSlider(root, "GlobalQualityWeight Override", -1f, 1f, -1f);
            _frequencyWeight = AddSlider(root, "Frequency Weight", 0.05f, 1f, 0.22f);
            _phaseWeight = AddSlider(root, "Phase Weight", 0.01f, 0.5f, 0.08f);
            _frequencySensitivity = AddSlider(root, "Frequency Sensitivity", 0.001f, 0.5f, 0.035f);
            _phaseSensitivity = AddSlider(root, "Phase Sensitivity", 0.001f, 0.5f, 0.08f);

            root.Add(new Button(ApplyTuning)
            {
                text = "Apply Kernel Tuning"
            });

            _targetFrequency = AddSlider(root, "Target Frequency", 0.1f, 12f, 4.5f);
            _targetPhase = AddSlider(root, "Target Phase", 0f, Mathf.PI * 2f, 1.2f);
            root.Add(new Button(ApplyTarget)
            {
                text = "Write Target Wave"
            });

            _puzzleIdReadout = AddIntegerReadout(root, "PuzzleID");
            _playerFrequencyReadout = AddFloatReadout(root, "Player Frequency");
            _playerPhaseReadout = AddFloatReadout(root, "Player Phase");
            _targetFrequencyReadout = AddFloatReadout(root, "Target Frequency");
            _targetPhaseReadout = AddFloatReadout(root, "Target Phase");
            _accuracyReadout = AddFloatReadout(root, "Accuracy");
            _flagsReadout = AddIntegerReadout(root, "Flags");
            _telemetryFrameReadout = AddIntegerReadout(root, "Telemetry Frame");
            _burstReadout = AddFloatReadout(root, "Burst us");

            _status = new Label("No runtime selected.");
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.marginTop = 8;
            root.Add(_status);
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max)
            {
                value = value,
                showInputField = true
            };
            root.Add(slider);
            return slider;
        }

        private static FloatField AddFloatReadout(VisualElement root, string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            root.Add(field);
            return field;
        }

        private static IntegerField AddIntegerReadout(VisualElement root, string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            root.Add(field);
            return field;
        }

        private void FindRuntime()
        {
            _runtime = FindAnyObjectByType<TerminalOsRuntime>();
            _runtimeField.value = _runtime;
            SyncTuningFromRuntime();
            RefreshReadout();
        }

        private void SyncTuningFromRuntime()
        {
            if (_runtime == null || _baseFrequency == null)
                return;

            _baseFrequency.SetValueWithoutNotify(_runtime.GetDecryptionBaseFrequency());
            _snapTolerance.SetValueWithoutNotify(_runtime.GetDecryptionSnapTolerance01());
            _noiseDensity.SetValueWithoutNotify(_runtime.GetDecryptionNoiseDensity());
            _qualityOverride.SetValueWithoutNotify(_runtime.GetDecryptionQualityOverride());
        }

        private void ApplyTuning()
        {
            if (_runtime == null)
                return;

            Undo.RecordObject(_runtime, "Oscilloscope Decryption Tuning");
            _runtime.ApplyDecryptionEditorTuning(
                _frequencyWeight.value,
                _phaseWeight.value,
                _frequencySensitivity.value,
                _phaseSensitivity.value,
                _baseFrequency.value,
                _snapTolerance.value,
                _noiseDensity.value,
                _qualityOverride.value);
            EditorUtility.SetDirty(_runtime);
            RefreshReadout();
        }

        private void ApplyTarget()
        {
            if (_runtime == null)
                return;

            Undo.RecordObject(_runtime, "Oscilloscope Decryption Target");
            _runtime.TrySetDecryptionTarget(
                Mathf.Max(0, _terminalIndexField.value),
                _targetFrequency.value,
                _targetPhase.value);
            EditorUtility.SetDirty(_runtime);
            RefreshReadout();
        }

        private void PollRuntime()
        {
            if (_runtime == null || !Application.isPlaying)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextPollTime)
                return;

            _nextPollTime = now + PollIntervalSeconds;
            RefreshReadout();
        }

        private void RefreshReadout()
        {
            if (_status == null)
                return;

            if (_runtime == null)
            {
                _status.text = "No runtime selected.";
                return;
            }

            int index = Mathf.Max(0, _terminalIndexField != null ? _terminalIndexField.value : 0);
            if (!_runtime.TryGetDecryptionPuzzleCopy(index, out DecryptionPuzzleDTO puzzle))
            {
                _status.text = "Puzzle DTO unavailable or job in flight.";
                return;
            }

            _targetFrequency.SetValueWithoutNotify(puzzle.TargetFrequency);
            _targetPhase.SetValueWithoutNotify(puzzle.TargetPhase);
            bool hasTelemetry = _runtime.TryGetLatestDecryptionTelemetryCopy(out DecryptionTelemetryEntry telemetry);
            float burstMicroseconds = hasTelemetry ? telemetry.BurstMicroseconds : _runtime.GetLastDecryptionBurstMicroseconds();
            uint telemetryFrame = hasTelemetry ? telemetry.Frame : 0u;

            _puzzleIdReadout.SetValueWithoutNotify(unchecked((int)puzzle.PuzzleID));
            _playerFrequencyReadout.SetValueWithoutNotify(puzzle.PlayerFrequency);
            _playerPhaseReadout.SetValueWithoutNotify(puzzle.PlayerPhase);
            _targetFrequencyReadout.SetValueWithoutNotify(puzzle.TargetFrequency);
            _targetPhaseReadout.SetValueWithoutNotify(puzzle.TargetPhase);
            _accuracyReadout.SetValueWithoutNotify(puzzle.AlignmentAccuracy01);
            _flagsReadout.SetValueWithoutNotify(unchecked((int)puzzle.Flags));
            _telemetryFrameReadout.SetValueWithoutNotify(unchecked((int)telemetryFrame));
            _burstReadout.SetValueWithoutNotify(burstMicroseconds);
            _status.text = "Telemetry readout active.";
        }
    }
}
#endif
