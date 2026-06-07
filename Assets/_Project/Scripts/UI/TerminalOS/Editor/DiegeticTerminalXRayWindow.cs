#if UNITY_EDITOR
using Hecton8.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.UI.Editor
{
    public sealed class DiegeticTerminalXRayWindow : EditorWindow
    {
        private const double PollIntervalSeconds = 0.12d;

        private TerminalOsRuntime _runtime;
        private ObjectField _runtimeField;
        private Slider _maxDistance;
        private Slider _cursorSnapping;
        private Slider _raycastThickness;
        private IntegerField _frame;
        private IntegerField _evaluated;
        private IntegerField _projected;
        private IntegerField _signals;
        private FloatField _burstUs;
        private FloatField _radius;
        private FloatField _quality;
        private IntegerField _faults;
        private IntegerField _nonFinite;
        private Label _status;
        private double _nextPollTime;

        [MenuItem("Tools/HECTON-8/Diegetic Terminal X-Ray")]
        public static void Open()
        {
            GetWindow<DiegeticTerminalXRayWindow>("Diegetic Terminal X-Ray");
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
                SyncFromRuntime();
                RefreshReadout();
            });
            root.Add(_runtimeField);
            root.Add(new Button(FindRuntime) { text = "Find Runtime" });

            _maxDistance = AddSlider(root, "Max Interaction Distance", 0.5f, 30f, 10f);
            _cursorSnapping = AddSlider(root, "Cursor Snapping Tolerance", 0.0005f, 0.05f, 0.0065f);
            _raycastThickness = AddSlider(root, "Raycast Thickness", 0.001f, 0.08f, 0.01f);
            root.Add(new Button(ApplyTuning) { text = "Apply Projection Tuning" });

            _frame = AddIntReadout(root, "Frame");
            _evaluated = AddIntReadout(root, "Evaluated Terminals");
            _projected = AddIntReadout(root, "Successful Projections");
            _signals = AddIntReadout(root, "Signals Dispatched");
            _burstUs = AddFloatReadout(root, "Burst us");
            _radius = AddFloatReadout(root, "Eval Radius");
            _quality = AddFloatReadout(root, "GlobalQualityWeight");
            _faults = AddIntReadout(root, "Fault Flags");
            _nonFinite = AddIntReadout(root, "Non-Finite Count");

            _status = new Label("No runtime selected.");
            _status.style.marginTop = 8;
            _status.style.whiteSpace = WhiteSpace.Normal;
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

        private static IntegerField AddIntReadout(VisualElement root, string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            root.Add(field);
            return field;
        }

        private static FloatField AddFloatReadout(VisualElement root, string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            root.Add(field);
            return field;
        }

        private void FindRuntime()
        {
            _runtime = FindAnyObjectByType<TerminalOsRuntime>();
            _runtimeField.value = _runtime;
            SyncFromRuntime();
            RefreshReadout();
        }

        private void SyncFromRuntime()
        {
            if (_runtime == null || _maxDistance == null)
                return;

            _maxDistance.SetValueWithoutNotify(_runtime.GetTerminalProjectionMaxInteractionDistance());
            _cursorSnapping.SetValueWithoutNotify(_runtime.GetTerminalProjectionCursorSnappingTolerance());
            _raycastThickness.SetValueWithoutNotify(_runtime.GetTerminalProjectionRaycastThickness());
        }

        private void ApplyTuning()
        {
            if (_runtime == null)
                return;

            Undo.RecordObject(_runtime, "Diegetic Terminal Projection Tuning");
            _runtime.ApplyTerminalProjectionEditorTuning(_maxDistance.value, _cursorSnapping.value, _raycastThickness.value);
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

            if (!_runtime.TryGetLatestTerminalInputTelemetry(out TerminalInputTelemetryEntry entry))
            {
                _status.text = "Telemetry unavailable. Enter Play Mode or select an initialized TerminalOsRuntime.";
                return;
            }

            _frame.value = entry.Frame;
            _evaluated.value = entry.EvaluatedTerminals;
            _projected.value = entry.SuccessfulProjections;
            _signals.value = entry.SignalsDispatched;
            _burstUs.value = entry.BurstMicroseconds;
            _radius.value = entry.EvalRadiusMeters;
            _quality.value = entry.GlobalQualityWeight;
            _faults.value = unchecked((int)entry.FaultFlags);
            _nonFinite.value = entry.NonFiniteCount;
            _status.text = "Vault buffer 71381, projection DTO stride 64, rollback excluded.";
        }
    }
}
#endif
