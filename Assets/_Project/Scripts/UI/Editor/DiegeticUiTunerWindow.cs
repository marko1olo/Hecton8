#if UNITY_EDITOR
using Hecton8.UI;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class DiegeticUiTunerWindow : EditorWindow
    {
        private const string WindowTitle = "Diegetic UI Tuner";

        private TerminalOsRuntime _runtime;
        private ObjectField _runtimeField;
        private Slider _distanceSlider;
        private Slider _viewConeSlider;
        private Slider _distortionSlider;
        private Slider _minQualitySlider;
        private Label _statusLabel;

        [MenuItem("Tools/HECTON-8/Diegetic UI Tuner")]
        public static void Open()
        {
            GetWindow<DiegeticUiTunerWindow>(WindowTitle);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _runtimeField = new ObjectField("Terminal Runtime")
            {
                objectType = typeof(TerminalOsRuntime),
                allowSceneObjects = true
            };
            _runtimeField.RegisterValueChangedCallback(evt =>
            {
                _runtime = evt.newValue as TerminalOsRuntime;
                RefreshStatus();
            });
            root.Add(_runtimeField);

            Button findButton = new Button(FindRuntime) { text = "Find Runtime" };
            root.Add(findButton);

            _distanceSlider = CreateSlider(root, "Max Distance", 0.5f, 30f, 10f);
            _viewConeSlider = CreateSlider(root, "View Cone Cos", -0.5f, 0.95f, -0.05f);
            _distortionSlider = CreateSlider(root, "Hologram Distortion", 0f, 1f, 0.35f);
            _minQualitySlider = CreateSlider(root, "Min Quality Weight", 0f, 1f, 0f);

            Button applyButton = new Button(ApplyTuning) { text = "Apply" };
            root.Add(applyButton);

            _statusLabel = new Label("No runtime selected.");
            _statusLabel.style.marginTop = 8f;
            root.Add(_statusLabel);
        }

        private Slider CreateSlider(VisualElement root, string label, float low, float high, float value)
        {
            Slider slider = new Slider(label, low, high)
            {
                value = value,
                showInputField = true
            };
            root.Add(slider);
            return slider;
        }

        private void FindRuntime()
        {
            _runtime = FindFirstObjectByType<TerminalOsRuntime>();
            if (_runtimeField != null)
                _runtimeField.value = _runtime;
            RefreshStatus();
        }

        private void ApplyTuning()
        {
            if (_runtime == null)
                return;

            Undo.RecordObject(_runtime, "Diegetic UI Tuning");
            _runtime.ApplyEditorTuning(
                _distanceSlider.value,
                _viewConeSlider.value,
                _distortionSlider.value,
                _minQualitySlider.value);
            EditorUtility.SetDirty(_runtime);
            RefreshStatus();
        }

        private void OnInspectorUpdate()
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null)
                return;

            if (_runtime == null)
            {
                _statusLabel.text = "No runtime selected.";
                return;
            }

            _statusLabel.text =
                "Quality " + _runtime.GetGlobalQualityWeight().ToString("0.000") +
                " | Frames/update " + _runtime.GetFramesBetweenUpdates() +
                " | Evaluated " + _runtime.GetLastEvaluatedTerminalCount() +
                " | Hover 0x" + _runtime.GetLastHoveredTerminalHash().ToString("X8") +
                " | Intersect us " + math.round(_runtime.GetLastIntersectionMicroseconds()).ToString();
        }
    }
}
#endif
