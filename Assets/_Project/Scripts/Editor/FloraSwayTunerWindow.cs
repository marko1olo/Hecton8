#if UNITY_EDITOR
using Hecton8.World;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class FloraSwayTunerWindow : EditorWindow
    {
        private Label _readout;
        private Slider _decaySlider;
        private Slider _currentSlider;
        private Slider _massSlider;
        private Toggle _mockToggle;
        private Toggle _gizmoToggle;
        private ObjectField _targetField;
        private FloraInteractionManager _target;
        private SerializedObject _serializedTarget;

        [MenuItem("Tools/Hecton-8/Procedural Flora Sway Tuner")]
        private static void Open()
        {
            GetWindow<FloraSwayTunerWindow>("Procedural Flora Sway Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _targetField = new ObjectField("Target")
            {
                objectType = typeof(FloraInteractionManager),
                allowSceneObjects = true
            };
            _targetField.RegisterValueChangedCallback(evt =>
            {
                _target = evt.newValue as FloraInteractionManager;
                RebindTarget();
            });
            rootVisualElement.Add(_targetField);

            _readout = new Label("Max 0.000 | Res 0 | Cells 0");
            rootVisualElement.Add(_readout);

            _decaySlider = BuildSlider("Spring Decay Rate", 0.1f, 12f, "_floraSwaySpringDecayRate");
            _currentSlider = BuildSlider("Global Current Influence", 0f, 1f, "_floraSwayGlobalCurrentInfluence");
            _massSlider = BuildSlider("Entity Mass Multiplier", 0f, 4f, "_floraSwayEntityMassMultiplier");
            _mockToggle = BuildToggle("Mock Injector", "_enableMockDisplacementInjector");
            _gizmoToggle = BuildToggle("Vector Gizmo", "_drawFloraSwayDebugGizmos");

            Button reloadCsv = new Button(ReloadCsv) { text = "Reload CSV" };
            rootVisualElement.Add(reloadCsv);

            _target = FloraInteractionManager.ActiveRuntimeInstance;
            _targetField.value = _target;
            RebindTarget();
        }

        private void OnEnable()
        {
            if (!FloraInteractionManager.ValidateFloraDisplacementDtoLayout(out int dtoSize, out int forceOffset, out int decayOffset) ||
                !FloraInteractionManager.ValidateFloraSwayTelemetryLayout(out int telemetrySize))
            {
                Debug.LogError("Flora sway DTO layout invalid. FloraDisplacementDTO size=" + dtoSize + " forceOffset=" + forceOffset + " decayOffset=" + decayOffset + " telemetrySize=" + telemetrySize);
            }

            EditorApplication.update += RefreshReadout;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshReadout;
        }

        private Slider BuildSlider(string label, float min, float max, string propertyName)
        {
            Slider slider = new Slider(label, min, max);
            slider.bindingPath = propertyName;
            rootVisualElement.Add(slider);
            return slider;
        }

        private Toggle BuildToggle(string label, string propertyName)
        {
            Toggle toggle = new Toggle(label);
            toggle.bindingPath = propertyName;
            rootVisualElement.Add(toggle);
            return toggle;
        }

        private void RebindTarget()
        {
            rootVisualElement.Unbind();
            _serializedTarget = _target != null ? new SerializedObject(_target) : null;
            if (_serializedTarget != null)
                rootVisualElement.Bind(_serializedTarget);
            RefreshReadout();
        }

        private void RefreshReadout()
        {
            if (_target == null)
                _target = FloraInteractionManager.ActiveRuntimeInstance;

            if (_target == null)
            {
                if (_readout != null)
                    _readout.text = "Max 0.000 | Res 0 | Cells 0";
                return;
            }

            if (_targetField != null && _targetField.value == null)
                _targetField.SetValueWithoutNotify(_target);

            if (_serializedTarget != null)
                _serializedTarget.UpdateIfRequiredOrScript();

            if (_readout != null)
            {
                _readout.text =
                    "Max " + _target.FloraSwayMaxMagnitudeForEditor.ToString("0.000") +
                    " | Res " + _target.FloraSwayResolutionForEditor +
                    " | Cells " + _target.FloraSwayNonZeroCellsForEditor;
            }
        }

        private void ReloadCsv()
        {
            if (_target == null)
                return;

            string path = "Assets/_Project/Data/World/flora_stiffness_profiles.csv";
            _target.TryReloadFloraStiffnessProfilesCsvForEditor(path);
        }
    }
}
#endif
