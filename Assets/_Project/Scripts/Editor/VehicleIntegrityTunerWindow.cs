#if UNITY_EDITOR
using Hecton8.Physics.Vehicles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class VehicleIntegrityTunerWindow : EditorWindow
    {
        private Label _statusLabel;
        private Slider _armorSlider;
        private Slider _radiusSlider;
        private Slider _falloffSlider;
        private Slider _fireSlider;
        private FloatField _integrityField;
        private FloatField _thrustField;
        private FloatField _buoyancyField;
        private FloatField _fireSeverityField;
        private FloatField _totalDamageField;
        private FloatField _burstUsField;
        private IntegerField _breachField;
        private IntegerField _signalField;
        private IntegerField _frameField;
        private IntegerField _hashField;
        private VehicleComponentDamageRuntime _runtime;
        private SerializedObject _serializedRuntime;
        private int _lastRuntimeInstanceId;

        [MenuItem("Hecton/Vehicles/Vehicle Integrity Tuner")]
        public static void Open()
        {
            GetWindow<VehicleIntegrityTunerWindow>("Vehicle Integrity Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _statusLabel = new Label("No VehicleComponentDamageRuntime selected.");
            _armorSlider = BuildSlider("Armor", 0.01f, 5f, "baseArmor");
            _radiusSlider = BuildSlider("Blast Radius", 0.05f, 5f, "explosiveRadiusMeters");
            _falloffSlider = BuildSlider("Falloff", 0.01f, 3f, "explosionFalloff");
            _fireSlider = BuildSlider("Fire Chance", 0f, 1f, "fireChance01");
            _integrityField = BuildReadOnlyFloat("Integrity");
            _thrustField = BuildReadOnlyFloat("Thrust");
            _buoyancyField = BuildReadOnlyFloat("Buoyancy");
            _fireSeverityField = BuildReadOnlyFloat("Fire");
            _breachField = BuildReadOnlyInt("Breaches");
            _frameField = BuildReadOnlyInt("Frame");
            _signalField = BuildReadOnlyInt("Signals");
            _totalDamageField = BuildReadOnlyFloat("Total Damage");
            _burstUsField = BuildReadOnlyFloat("Burst us");
            _hashField = BuildReadOnlyInt("State Hash");

            rootVisualElement.Add(_statusLabel);
            rootVisualElement.Add(_armorSlider);
            rootVisualElement.Add(_radiusSlider);
            rootVisualElement.Add(_falloffSlider);
            rootVisualElement.Add(_fireSlider);
            rootVisualElement.Add(_integrityField);
            rootVisualElement.Add(_thrustField);
            rootVisualElement.Add(_buoyancyField);
            rootVisualElement.Add(_fireSeverityField);
            rootVisualElement.Add(_breachField);
            rootVisualElement.Add(_frameField);
            rootVisualElement.Add(_signalField);
            rootVisualElement.Add(_totalDamageField);
            rootVisualElement.Add(_burstUsField);
            rootVisualElement.Add(_hashField);
        }

        private void OnEnable()
        {
            EditorApplication.update += Refresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Refresh;
        }

        private Slider BuildSlider(string label, float min, float max, string propertyName)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt => ApplyFloat(propertyName, evt.newValue));
            return slider;
        }

        private static FloatField BuildReadOnlyFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private static IntegerField BuildReadOnlyInt(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private void Refresh()
        {
            if (_runtime == null)
            {
                _runtime = Object.FindFirstObjectByType<VehicleComponentDamageRuntime>();
                _serializedRuntime = _runtime != null ? new SerializedObject(_runtime) : null;
                PullSerializedValues();
            }

            if (_runtime == null)
            {
                if (_lastRuntimeInstanceId != 0)
                    _statusLabel.text = "No VehicleComponentDamageRuntime in loaded scene.";
                _lastRuntimeInstanceId = 0;
                ClearReadout();
                return;
            }

            int runtimeId = _runtime.GetInstanceID();
            if (runtimeId != _lastRuntimeInstanceId)
            {
                _statusLabel.text = "Runtime: " + _runtime.name;
                _lastRuntimeInstanceId = runtimeId;
            }

            PullVehicleState();
        }

        private void PullSerializedValues()
        {
            if (_runtime != null && _runtime.TryLockCopyEditorTuning(out VehicleDamageTuningDTO tuning))
            {
                _armorSlider.SetValueWithoutNotify(tuning.BaseArmor);
                _radiusSlider.SetValueWithoutNotify(tuning.ExplosiveRadiusMeters);
                _falloffSlider.SetValueWithoutNotify(tuning.ExplosionFalloff);
                _fireSlider.SetValueWithoutNotify(tuning.FireChance01);
                return;
            }

            if (_serializedRuntime == null)
                return;

            _serializedRuntime.UpdateIfRequiredOrScript();
            SetWithoutNotify(_armorSlider, "baseArmor");
            SetWithoutNotify(_radiusSlider, "explosiveRadiusMeters");
            SetWithoutNotify(_falloffSlider, "explosionFalloff");
            SetWithoutNotify(_fireSlider, "fireChance01");
        }

        private void SetWithoutNotify(Slider slider, string propertyName)
        {
            SerializedProperty property = _serializedRuntime.FindProperty(propertyName);
            if (property != null)
                slider.SetValueWithoutNotify(property.floatValue);
        }

        private void ApplyFloat(string propertyName, float value)
        {
            if (_runtime == null)
                _runtime = Object.FindFirstObjectByType<VehicleComponentDamageRuntime>();
            if (_runtime == null)
                return;

            _runtime.TryWriteEditorTuning(propertyName, value);

            _serializedRuntime ??= new SerializedObject(_runtime);
            _serializedRuntime.Update();
            SerializedProperty property = _serializedRuntime.FindProperty(propertyName);
            if (property == null)
                return;

            property.floatValue = value;
            _serializedRuntime.ApplyModifiedProperties();
            EditorUtility.SetDirty(_runtime);
        }

        private void PullVehicleState()
        {
            if (!_runtime.TryLockCopyEditorDamageSnapshot(
                    out VehicleDamageStateDTO state,
                    out VehicleDamageTelemetryEntry telemetry,
                    out bool hasTelemetry))
            {
                ClearReadout();
                return;
            }

            _integrityField.SetValueWithoutNotify(state.StructuralIntegrity01);
            _thrustField.SetValueWithoutNotify(state.MaxThrustScalar);
            _buoyancyField.SetValueWithoutNotify(state.BuoyancyScalar);
            _fireSeverityField.SetValueWithoutNotify(state.FireSeverity01);
            _breachField.SetValueWithoutNotify((int)state.ActiveBreaches);

            if (hasTelemetry)
            {
                _frameField.SetValueWithoutNotify((int)telemetry.Frame);
                _signalField.SetValueWithoutNotify((int)telemetry.SignalCount);
                _totalDamageField.SetValueWithoutNotify(telemetry.TotalDamage01);
                _burstUsField.SetValueWithoutNotify(telemetry.EstimatedCostUs);
                _hashField.SetValueWithoutNotify(unchecked((int)telemetry.StateHash));
            }
            else
            {
                _frameField.SetValueWithoutNotify(0);
                _signalField.SetValueWithoutNotify(0);
                _totalDamageField.SetValueWithoutNotify(0f);
                _burstUsField.SetValueWithoutNotify(0f);
                _hashField.SetValueWithoutNotify(0);
            }
        }

        private void ClearReadout()
        {
            _integrityField.SetValueWithoutNotify(0f);
            _thrustField.SetValueWithoutNotify(0f);
            _buoyancyField.SetValueWithoutNotify(0f);
            _fireSeverityField.SetValueWithoutNotify(0f);
            _breachField.SetValueWithoutNotify(0);
            _frameField.SetValueWithoutNotify(0);
            _signalField.SetValueWithoutNotify(0);
            _totalDamageField.SetValueWithoutNotify(0f);
            _burstUsField.SetValueWithoutNotify(0f);
            _hashField.SetValueWithoutNotify(0);
        }
    }
}
#endif
