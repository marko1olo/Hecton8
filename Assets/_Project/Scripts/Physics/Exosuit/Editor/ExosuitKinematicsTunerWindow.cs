#if UNITY_EDITOR
using Hecton8.Physics.Exosuit;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Exosuit.Editor
{
    public sealed class ExosuitKinematicsTunerWindow : EditorWindow
    {
        private Slider _baseMass;
        private Slider _hydraulicLatency;
        private Slider _thrusterForce;
        private Slider _clampRange;
        private Slider _globalQuality;
        private Slider _sdfEpsilon;
        private Slider _gravityMultiplier;
        private Slider _maxSubsteps;
        private Label _status;
        private Label _pressure;
        private Label _heat;
        private Label _position;
        private Label _velocity;
        private Label _cpu;
        private Label _speed;
        private Label _pushOut;
        private Label _flags;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/Physics/Exosuit Kinematics Tuner")]
        public static void Open()
        {
            ExosuitKinematicsTunerWindow window = GetWindow<ExosuitKinematicsTunerWindow>();
            window.titleContent = new GUIContent("Exosuit Kinematics");
            window.Show();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _status = new Label("Waiting for Play Mode DataVault buffers.");
            root.Add(_status);

            _baseMass = AddSlider(root, "Base Mass", 1000f, 20000f);
            _hydraulicLatency = AddSlider(root, "Hydraulic Latency", 0.05f, 3f);
            _thrusterForce = AddSlider(root, "Thruster Force", 0f, 120000f);
            _clampRange = AddSlider(root, "Magnetic Clamp Range", 0.25f, 5f);
            _globalQuality = AddSlider(root, "GlobalQualityWeight", 0f, 1f);
            _sdfEpsilon = AddSlider(root, "SDF Epsilon", 0.005f, 0.25f);
            _gravityMultiplier = AddSlider(root, "Gravity Multiplier", 0f, 2f);
            _maxSubsteps = AddSlider(root, "Max Substeps", 2f, 8f);

            root.Add(new Label("Readback"));
            _pressure = new Label("Pressure: n/a");
            _heat = new Label("Heat: n/a");
            _position = new Label("AUP: n/a");
            _velocity = new Label("Velocity: n/a");
            _cpu = new Label("Burst CPU: n/a");
            _speed = new Label("Speed: n/a");
            _pushOut = new Label("Push-Out: n/a");
            _flags = new Label("Flags: n/a");
            root.Add(_pressure);
            root.Add(_heat);
            root.Add(_position);
            root.Add(_velocity);
            root.Add(_cpu);
            root.Add(_speed);
            root.Add(_pushOut);
            root.Add(_flags);

            RefreshTuningSliders();
            RefreshReadback();
            root.schedule.Execute(RefreshReadback).Every(250);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawSceneGizmos;
        }

        private Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => WriteTuningFromUi());
            root.Add(slider);
            return slider;
        }

        private void RefreshTuningSliders()
        {
            if (!Application.isPlaying || !ExosuitKinematicsRuntime.TryReadTuning(out ExosuitTuningDTO tuning))
                return;

            _suppressCallbacks = true;
            _baseMass.value = tuning.BaseMass;
            _hydraulicLatency.value = tuning.HydraulicLatencySeconds;
            _thrusterForce.value = tuning.ThrusterForce;
            _clampRange.value = tuning.ClampRange;
            _globalQuality.value = tuning.GlobalQualityWeight;
            _sdfEpsilon.value = tuning.SdfEpsilonMeters;
            _gravityMultiplier.value = tuning.GravityMultiplier;
            _maxSubsteps.value = tuning.MaxSubsteps;
            _suppressCallbacks = false;
        }

        private void WriteTuningFromUi()
        {
            if (_suppressCallbacks ||
                !Application.isPlaying ||
                !ExosuitKinematicsRuntime.TryReadTuning(out ExosuitTuningDTO tuning))
            {
                return;
            }

            tuning.BaseMass = _baseMass.value;
            if (tuning.CurrentMass > tuning.BaseMass || tuning.CurrentMass <= 0f)
                tuning.CurrentMass = tuning.BaseMass;
            tuning.HydraulicLatencySeconds = _hydraulicLatency.value;
            tuning.ThrusterForce = _thrusterForce.value;
            tuning.ClampRange = math.max(tuning.Radius, _clampRange.value);
            tuning.GlobalQualityWeight = math.saturate(_globalQuality.value);
            tuning.SdfEpsilonMeters = math.clamp(_sdfEpsilon.value, 0.005f, 0.25f);
            tuning.GravityMultiplier = math.clamp(_gravityMultiplier.value, 0f, 2f);
            tuning.MaxSubsteps = (uint)math.clamp((int)math.round(_maxSubsteps.value), 2, 8);
            ExosuitKinematicsRuntime.TryWriteTuning(in tuning);
            SceneView.RepaintAll();
        }

        private void RefreshReadback()
        {
            if (!Application.isPlaying)
            {
                _status.text = "Play Mode required for DataVault tuning.";
                return;
            }

            if (!ExosuitKinematicsRuntime.TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning))
            {
                _status.text = "Exosuit DataVault buffers are not initialized.";
                return;
            }

            ExosuitKinematicsRuntime.TryReadScreen(out ExoScreenDTO screen);
            ExosuitKinematicsRuntime.TryReadLastTelemetry(out ExosuitTelemetryEntry telemetry);
            _status.text = "Live unmanaged exosuit authority.";
            _pressure.text = "Pressure: " + screen.HydraulicPressure.ToString("0.000");
            _heat.text = "Heat: " + state.ThrusterHeat.ToString("0.000");
            _position.text = "AUP: " + FormatDouble3(state.AUP_Position);
            _velocity.text = "Velocity: " + FormatFloat3(state.Velocity);
            _cpu.text = "Burst CPU: " + telemetry.SolverComputeTimeMs.ToString("0.000 ms");
            _speed.text = "Speed: " + output.Speed.ToString("0.000 m/s");
            _pushOut.text = "Push-Out: " + output.PushOutMagnitude.ToString("0.000 m");
            _flags.text = "Flags: 0x" + state.Flags.ToString("X8") +
                " | Q " + tuning.GlobalQualityWeight.ToString("0.000") +
                " | G " + tuning.GravityMultiplier.ToString("0.00") +
                " | Eps " + tuning.SdfEpsilonMeters.ToString("0.000") +
                " | Steps " + tuning.MaxSubsteps.ToString();
        }

        private static string FormatDouble3(double3 value)
        {
            return value.x.ToString("0.000") + ", " + value.y.ToString("0.000") + ", " + value.z.ToString("0.000");
        }

        private static string FormatFloat3(float3 value)
        {
            return value.x.ToString("0.000") + ", " + value.y.ToString("0.000") + ", " + value.z.ToString("0.000");
        }

        private static void DrawSceneGizmos(SceneView sceneView)
        {
            if (!Application.isPlaying)
                return;
            if (!ExosuitKinematicsRuntime.TryReadState(out ExosuitStateDTO state, out ExosuitSolverOutput output, out ExosuitTuningDTO tuning))
                return;

            Vector3 center = new Vector3(output.LocalPosition.x, output.LocalPosition.y, output.LocalPosition.z);
            float radius = math.max(0.25f, tuning.Radius);

            Handles.color = Color.green;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Handles.DrawWireDisc(center, Vector3.right, radius);
            Handles.DrawWireDisc(center, Vector3.forward, radius);

            Handles.color = Color.red;
            Vector3 normal = new Vector3(output.PushNormal.x, output.PushNormal.y, output.PushNormal.z);
            Handles.DrawLine(center, center + normal * math.max(0.5f, output.PushOutMagnitude * 4f));

            Handles.color = Color.blue;
            Vector3 desired = new Vector3(output.DesiredVelocity.x, output.DesiredVelocity.y, output.DesiredVelocity.z);
            Handles.DrawLine(center, center + desired);
        }
    }
}
#endif
