using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Vehicles.Editor
{
    public sealed class SubmarineAutoLevelTunerWindow : EditorWindow
    {
        private const int GraphBins = 32;
        private const double RefreshSeconds = 0.20d;

        private readonly VisualElement[] _pitchBars = new VisualElement[GraphBins];
        private readonly VisualElement[] _torqueBars = new VisualElement[GraphBins];
        private Label _status;
        private FloatField _pitchError;
        private FloatField _rollError;
        private FloatField _maxTorqueReadout;
        private FloatField _burstMicros;
        private Toggle _enabled;
        private Slider _pitchP;
        private Slider _pitchD;
        private Slider _rollP;
        private Slider _rollD;
        private Slider _maxTorque;
        private bool _suppressCallbacks;
        private double _nextRefresh;
        private int _graphCursor;

        [MenuItem("Hecton8/Vehicles/Submarine Auto-Level Tuner")]
        public static void Open()
        {
            SubmarineAutoLevelTunerWindow window = GetWindow<SubmarineAutoLevelTunerWindow>();
            window.titleContent = new GUIContent("Submarine Auto-Level");
            window.minSize = new Vector2(460f, 470f);
            window.Show();
        }

        [MenuItem("Hecton8/Vehicles/Submarine Auto-Level/Run Static Audit")]
        public static void RunStaticAudit()
        {
            int dtoSize = UnsafeUtility.SizeOf<SubmarineGyroDTO>();
            int pitchPOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.ProportionalGainPitch)).ToInt32();
            int pitchDOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.DerivativeGainPitch)).ToInt32();
            int rollPOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.ProportionalGainRoll)).ToInt32();
            int rollDOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.DerivativeGainRoll)).ToInt32();
            int maxTorqueOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.MaxCorrectionTorque)).ToInt32();
            int enabledOffset = Marshal.OffsetOf<SubmarineGyroDTO>(nameof(SubmarineGyroDTO.AutoLevelEnabledFlag)).ToInt32();
            bool layoutPass = dtoSize == 32 &&
                              pitchPOffset == 0 &&
                              pitchDOffset == 4 &&
                              rollPOffset == 8 &&
                              rollDOffset == 12 &&
                              maxTorqueOffset == 16 &&
                              enabledOffset == 20;

            int fileCount;
            int eulerHits = Euler_Angle_Scanner.CountUnstableVehicleEulerOperations(out fileCount);
            string projectRoot = ResolveProjectRoot();
            Euler_Angle_Scanner.WriteReports(projectRoot, Euler_Angle_Scanner.BuildReportJson(fileCount, eulerHits, layoutPass));
            if (!layoutPass || eulerHits != 0)
                Debug.LogError("SHINOBU_332 auto-level audit failed. See Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_332.json");
            else
                Debug.Log("SHINOBU_332 auto-level audit written: Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_332.json");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _status = new Label("Runtime unavailable");
            _status.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_status);

            _pitchError = BuildReadout("Average pitch error");
            _rollError = BuildReadout("Average roll error");
            _maxTorqueReadout = BuildReadout("Max torque");
            _burstMicros = BuildReadout("Burst us");
            root.Add(_pitchError);
            root.Add(_rollError);
            root.Add(_maxTorqueReadout);
            root.Add(_burstMicros);

            VisualElement graph = new VisualElement();
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.height = 82f;
            graph.style.marginTop = 8f;
            graph.style.marginBottom = 8f;
            for (int i = 0; i < GraphBins; i++)
            {
                VisualElement column = new VisualElement();
                column.style.flexGrow = 1f;
                column.style.marginLeft = 1f;
                column.style.marginRight = 1f;
                column.style.alignSelf = Align.Stretch;
                column.style.flexDirection = FlexDirection.ColumnReverse;

                VisualElement pitch = new VisualElement();
                pitch.style.height = 1f;
                pitch.style.backgroundColor = new Color(0.95f, 0.1f, 0.1f, 0.75f);
                VisualElement torque = new VisualElement();
                torque.style.height = 1f;
                torque.style.backgroundColor = new Color(1f, 0.88f, 0.04f, 0.85f);
                _pitchBars[i] = pitch;
                _torqueBars[i] = torque;
                column.Add(pitch);
                column.Add(torque);
                graph.Add(column);
            }

            root.Add(graph);

            _enabled = new Toggle("Auto-level enabled");
            root.Add(_enabled);
            _pitchP = AddSlider(root, "Pitch P", 0f, 160000f);
            _pitchD = AddSlider(root, "Pitch D", 0f, 60000f);
            _rollP = AddSlider(root, "Roll P", 0f, 160000f);
            _rollD = AddSlider(root, "Roll D", 0f, 60000f);
            _maxTorque = AddSlider(root, "Max torque", 1000f, 200000f);
            root.Add(new Button(RefreshFromRuntime) { text = "Refresh Vault" });
            root.Add(new Button(RunStaticAudit) { text = "Write Static Audit" });

            RegisterCallbacks();
            RefreshFromRuntime();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorPulse;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorPulse;
        }

        private void OnEditorPulse()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh)
                return;

            _nextRefresh = now + RefreshSeconds;
            RefreshTelemetryOnly();
        }

        private static FloatField BuildReadout(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _enabled.RegisterValueChangedCallback(_ => ApplyValues());
            _pitchP.RegisterValueChangedCallback(_ => ApplyValues());
            _pitchD.RegisterValueChangedCallback(_ => ApplyValues());
            _rollP.RegisterValueChangedCallback(_ => ApplyValues());
            _rollD.RegisterValueChangedCallback(_ => ApplyValues());
            _maxTorque.RegisterValueChangedCallback(_ => ApplyValues());
        }

        private void RefreshFromRuntime()
        {
            _suppressCallbacks = true;
            if (SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime) &&
                runtime.TryReadGyroTuning(out SubmarineGyroDTO tuning))
            {
                _enabled.SetValueWithoutNotify((tuning.AutoLevelEnabledFlag & SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled) != 0u);
                _pitchP.SetValueWithoutNotify(tuning.ProportionalGainPitch);
                _pitchD.SetValueWithoutNotify(tuning.DerivativeGainPitch);
                _rollP.SetValueWithoutNotify(tuning.ProportionalGainRoll);
                _rollD.SetValueWithoutNotify(tuning.DerivativeGainRoll);
                _maxTorque.SetValueWithoutNotify(tuning.MaxCorrectionTorque);
            }

            _suppressCallbacks = false;
            RefreshTelemetryOnly();
        }

        private void RefreshTelemetryOnly()
        {
            if (_status == null)
                return;

            if (!SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime))
            {
                _status.text = "Runtime unavailable";
                SetTelemetry(default);
                return;
            }

            if (!runtime.TryReadLatestGyroTelemetry(out GyroTelemetryEntry telemetry))
            {
                _status.text = "Gyro telemetry unavailable";
                SetTelemetry(default);
                return;
            }

            _status.text = "Runtime active";
            SetTelemetry(telemetry);
        }

        private void SetTelemetry(in GyroTelemetryEntry telemetry)
        {
            _pitchError.value = telemetry.AveragePitchError;
            _rollError.value = telemetry.AverageRollError;
            _maxTorqueReadout.value = telemetry.MaxCorrectiveTorque;
            _burstMicros.value = telemetry.BurstElapsedUs;

            float pitchHeight = math.clamp(telemetry.AveragePitchError * 160f, 1f, 78f);
            float torqueHeight = math.clamp(telemetry.MaxCorrectiveTorque / 1200f, 1f, 78f);
            int index = _graphCursor++ % GraphBins;
            if (_pitchBars[index] != null)
                _pitchBars[index].style.height = pitchHeight;
            if (_torqueBars[index] != null)
                _torqueBars[index].style.height = torqueHeight;
        }

        private void ApplyValues()
        {
            if (_suppressCallbacks)
                return;

            if (!SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime))
                return;

            SubmarineGyroDTO tuning = default;
            tuning.ProportionalGainPitch = math.max(0f, _pitchP.value);
            tuning.DerivativeGainPitch = math.max(0f, _pitchD.value);
            tuning.ProportionalGainRoll = math.max(0f, _rollP.value);
            tuning.DerivativeGainRoll = math.max(0f, _rollD.value);
            tuning.MaxCorrectionTorque = math.max(1f, _maxTorque.value);
            tuning.AutoLevelEnabledFlag = _enabled.value ? SubmarineDynamicsConstants.GyroFlagAutoLevelEnabled : 0u;
            runtime.TryWriteGyroTuning(in tuning);
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : dataPath;
        }
    }
}
