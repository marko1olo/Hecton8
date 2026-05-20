#if UNITY_EDITOR
namespace Hecton8.Tools.Editor
{
    using Hecton8.Core;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;

    public sealed class LaserCutterPhysicsTunerWindow : EditorWindow
    {
        private Slider _minimumPower;
        private Slider _maxDistance;
        private Slider _dentMin;
        private Slider _dentMax;
        private Slider _glowLifetime;
        private Slider _batteryWatts;
        private Slider _cooldownFrames;
        private Slider _sparkScale;
        private Slider _quality;
        private IntegerField _frameField;
        private IntegerField _sparkCountField;
        private FloatField _powerField;
        private FloatField _distanceField;
        private Label _status;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/Tools/Laser Cutter DOD Tuner")]
        private static void Open()
        {
            GetWindow<LaserCutterPhysicsTunerWindow>("Laser Cutter DOD");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _status = new Label("DataVault unavailable until runtime bootstrap.");
            _status.style.marginBottom = 8f;
            root.Add(_status);

            _minimumPower = BuildSlider("Minimum Power", 0f, 1f, 0.05f);
            _maxDistance = BuildSlider("Max Distance", 0.1f, 16f, 6f);
            _dentMin = BuildSlider("Dent Radius Min", 0f, 0.25f, 0.045f);
            _dentMax = BuildSlider("Dent Radius Max", 0.05f, 1f, 0.32f);
            _glowLifetime = BuildSlider("Glow Lifetime", 0f, 4f, 0.9f);
            _batteryWatts = BuildSlider("Battery Watts", 0f, 500f, 180f);
            _cooldownFrames = BuildSlider("Cooldown Frames", 1f, 12f, 2f);
            _sparkScale = BuildSlider("Spark Scale", 0f, 3f, 1f);
            _quality = BuildSlider("Global Quality", 0f, 1f, 1f);

            root.Add(_minimumPower);
            root.Add(_maxDistance);
            root.Add(_dentMin);
            root.Add(_dentMax);
            root.Add(_glowLifetime);
            root.Add(_batteryWatts);
            root.Add(_cooldownFrames);
            root.Add(_sparkScale);
            root.Add(_quality);

            VisualElement buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6f } };
            Button mock = new Button(GenerateMockRequests) { text = "Generate Mock Requests" };
            Button validate = new Button(ValidateLayout) { text = "Validate Layout" };
            buttons.Add(mock);
            buttons.Add(validate);
            root.Add(buttons);

            VisualElement telemetry = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 8f } };
            _frameField = BuildIntField("Frame");
            _sparkCountField = BuildIntField("Sparks");
            _powerField = BuildFloatField("Power");
            _distanceField = BuildFloatField("Distance");
            telemetry.Add(_frameField);
            telemetry.Add(_sparkCountField);
            telemetry.Add(_powerField);
            telemetry.Add(_distanceField);
            root.Add(telemetry);
            PullRuntimeState();
        }

        private Slider BuildSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value };
            slider.RegisterValueChangedCallback(_ => PushTuning());
            return slider;
        }

        private static IntegerField BuildIntField(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            field.style.width = 116f;
            field.style.marginRight = 4f;
            return field;
        }

        private static FloatField BuildFloatField(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            field.style.width = 132f;
            field.style.marginRight = 4f;
            return field;
        }

        private void OnEditorUpdate()
        {
            if (_status == null)
                return;

            PullRuntimeState();
        }

        private void PullRuntimeState()
        {
            _suppressCallbacks = true;
            if (LaserCutterDodRuntime.TryGetTuning(out LaserCutterTuningDTO tuning))
            {
                _status.text = "DataVault-backed tuning live.";
                _minimumPower.SetValueWithoutNotify(tuning.MinimumPower01);
                _maxDistance.SetValueWithoutNotify(tuning.DefaultMaxDistanceMeters);
                _dentMin.SetValueWithoutNotify(tuning.DentRadiusMinMeters);
                _dentMax.SetValueWithoutNotify(tuning.DentRadiusMaxMeters);
                _glowLifetime.SetValueWithoutNotify(tuning.GlowLifetimeSeconds);
                _batteryWatts.SetValueWithoutNotify(tuning.BatteryWattsAtPowerOne);
                _cooldownFrames.SetValueWithoutNotify(tuning.CooldownFrames);
                _sparkScale.SetValueWithoutNotify(tuning.SparkIntensityScale);
                _quality.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            }
            else
            {
                _status.text = "DataVault unavailable until runtime bootstrap.";
            }

            if (LaserCutterDodRuntime.TryGetLatestTelemetry(out LaserCutTelemetryEntry entry))
            {
                _frameField.SetValueWithoutNotify((int)entry.Frame);
                _sparkCountField.SetValueWithoutNotify((int)entry.SparkCount);
                _powerField.SetValueWithoutNotify(entry.CuttingPower);
                _distanceField.SetValueWithoutNotify(entry.DistanceMeters);
            }

            _suppressCallbacks = false;
        }

        private void PushTuning()
        {
            if (_suppressCallbacks)
                return;

            LaserCutterTuningDTO tuning = new LaserCutterTuningDTO
            {
                MinimumPower01 = _minimumPower.value,
                DefaultMaxDistanceMeters = _maxDistance.value,
                DentRadiusMinMeters = _dentMin.value,
                DentRadiusMaxMeters = _dentMax.value,
                GlowLifetimeSeconds = _glowLifetime.value,
                BatteryWattsAtPowerOne = _batteryWatts.value,
                CooldownFrames = _cooldownFrames.value,
                SparkIntensityScale = _sparkScale.value,
                LowSparkCount = LaserCutterDodConstants.LowSparkCount,
                UltraSparkCount = LaserCutterDodConstants.UltraSparkCount,
                GlobalQualityWeight = _quality.value,
                Flags = 0u,
                VersionHash = 0x53484C4354554E45UL
            };

            LaserCutterDodRuntime.TrySetTuning(in tuning);
        }

        private void GenerateMockRequests()
        {
            uint frame = unchecked((uint)Time.frameCount);
            LaserCutterDodRuntime.GenerateMockCutterTriggers(32, HectonFloatingOrigin.CurrentTotalOffsetDouble, frame, 0x53483232u);
            PullRuntimeState();
        }

        private void ValidateLayout()
        {
            bool ok = LaserCutterDodLayoutValidator.Validate(out uint faults);
            _status.text = ok ? "Layout OK: LaserCutRequestDTO = 64 bytes." : "Layout fault mask: 0x" + faults.ToString("X8");
        }
    }
}
#endif
