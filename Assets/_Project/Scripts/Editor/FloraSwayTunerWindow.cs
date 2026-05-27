#if UNITY_EDITOR
using Hecton8.World;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hecton8.Core;

namespace Hecton8.EditorTools
{
    public sealed class FloraSwayTunerWindow : EditorWindow
    {
        private const string EmptyMaxReadoutText = "Max 0.000";
        private const string EmptyDetailsReadoutText = "Res 0 | Cells 0";
        private const double ReadoutRefreshIntervalSeconds = 0.1d;
        private const int MaxMagnitudeReadoutMilli = 9999;

        private static readonly string[] MaxMagnitudeReadoutCache = BuildMaxMagnitudeReadoutCache();

        private Label _readout;
        private Label _detailsReadout;
        private Slider _decaySlider;
        private Slider _currentSlider;
        private Slider _massSlider;
        private Toggle _mockToggle;
        private Toggle _gizmoToggle;
        private ObjectField _targetField;
        private FloraInteractionManager _target;
        private SerializedObject _serializedTarget;
        private int _lastReadoutMaxMilli = int.MinValue;
        private int _lastReadoutResolution = int.MinValue;
        private int _lastReadoutCells = int.MinValue;
        private double _nextReadoutRefreshTime;

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

            _readout = new Label(EmptyMaxReadoutText);
            rootVisualElement.Add(_readout);
            _detailsReadout = new Label(EmptyDetailsReadoutText);
            rootVisualElement.Add(_detailsReadout);

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
            bool dtoValid = FloraInteractionManager.ValidateFloraDisplacementDtoLayout(out int dtoSize, out int forceOffset, out int decayOffset);
            bool telemetryValid = FloraInteractionManager.ValidateFloraSwayTelemetryLayout(
                out int telemetrySize,
                out int fieldCenterOffset,
                out int telemetryCpuOffset,
                out int telemetryResolutionOffset);
            bool wakeSourceValid = FloraInteractionManager.ValidateConsumedWakeSourceLayout(
                out int wakeSourceSize,
                out int wakeAupOffset,
                out int wakeVelocityOffset,
                out int wakeRadiusOffset,
                out int wakeKindOffset,
                out int wakePaddingOffset);
            bool wakeTelemetryValid = FloraInteractionManager.ValidateConsumedWakeTelemetryLayout(out int wakeTelemetrySize, out int wakeBudgetOffset);
            if (!dtoValid || !telemetryValid || !wakeSourceValid || !wakeTelemetryValid)
            {
                H8Debug.LogError(
                    "Flora sway DTO layout invalid. FloraDisplacementDTO size=" + dtoSize +
                    " forceOffset=" + forceOffset +
                    " decayOffset=" + decayOffset +
                    " telemetrySize=" + telemetrySize +
                    " fieldCenterOffset=" + fieldCenterOffset +
                    " telemetryCpuOffset=" + telemetryCpuOffset +
                    " telemetryResolutionOffset=" + telemetryResolutionOffset +
                    " wakeSourceSize=" + wakeSourceSize +
                    " wakeAupOffset=" + wakeAupOffset +
                    " wakeVelocityOffset=" + wakeVelocityOffset +
                    " wakeRadiusOffset=" + wakeRadiusOffset +
                    " wakeKindOffset=" + wakeKindOffset +
                    " wakePaddingOffset=" + wakePaddingOffset +
                    " wakeTelemetrySize=" + wakeTelemetrySize +
                    " wakeBudgetOffset=" + wakeBudgetOffset);
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
            _lastReadoutMaxMilli = int.MinValue;
            _nextReadoutRefreshTime = 0d;
            RefreshReadout();
        }

        private void RefreshReadout()
        {
            if (_target == null)
                _target = FloraInteractionManager.ActiveRuntimeInstance;

            if (_target == null)
            {
                if (_readout != null)
                    _readout.text = EmptyMaxReadoutText;
                if (_detailsReadout != null)
                    _detailsReadout.text = EmptyDetailsReadoutText;
                _lastReadoutMaxMilli = int.MinValue;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextReadoutRefreshTime)
                return;
            _nextReadoutRefreshTime = now + ReadoutRefreshIntervalSeconds;

            if (_targetField != null && _targetField.value == null)
                _targetField.SetValueWithoutNotify(_target);

            if (_serializedTarget != null)
                _serializedTarget.UpdateIfRequiredOrScript();

            if (_readout != null)
            {
                int maxMilli = Mathf.Clamp(
                    Mathf.RoundToInt(Mathf.Max(0f, _target.FloraSwayMaxMagnitudeForEditor) * 1000f),
                    0,
                    MaxMagnitudeReadoutMilli);
                int resolution = _target.FloraSwayResolutionForEditor;
                int cells = _target.FloraSwayNonZeroCellsForEditor;
                if (maxMilli == _lastReadoutMaxMilli &&
                    resolution == _lastReadoutResolution &&
                    cells == _lastReadoutCells)
                {
                    return;
                }

                _lastReadoutMaxMilli = maxMilli;
                _lastReadoutResolution = resolution;
                _lastReadoutCells = cells;
                _readout.text = MaxMagnitudeReadoutCache[maxMilli];
                if (_detailsReadout != null)
                    _detailsReadout.text = BuildDetailsText(resolution, cells);
            }
        }

        private static string[] BuildMaxMagnitudeReadoutCache()
        {
            string[] cache = new string[MaxMagnitudeReadoutMilli + 1];
            for (int i = 0; i < cache.Length; i++)
                cache[i] = BuildMaxMagnitudeText(i);
            return cache;
        }

        private static string BuildMaxMagnitudeText(int valueMilli)
        {
            char[] buffer = new char[16];
            buffer[0] = 'M';
            buffer[1] = 'a';
            buffer[2] = 'x';
            buffer[3] = ' ';
            int whole = valueMilli / 1000;
            int fraction = valueMilli - whole * 1000;
            int cursor = 4 + WritePositiveInt(whole, buffer, 4);
            buffer[cursor++] = '.';
            buffer[cursor++] = (char)('0' + (fraction / 100));
            buffer[cursor++] = (char)('0' + ((fraction / 10) % 10));
            buffer[cursor++] = (char)('0' + (fraction % 10));
            return new string(buffer, 0, cursor);
        }

        private static int WritePositiveInt(int value, char[] buffer, int offset)
        {
            if (value == 0)
            {
                buffer[offset] = '0';
                return 1;
            }

            int cursor = offset;
            int remaining = value;
            while (remaining > 0)
            {
                buffer[cursor++] = (char)('0' + (remaining % 10));
                remaining /= 10;
            }

            int left = offset;
            int right = cursor - 1;
            while (left < right)
            {
                char temp = buffer[left];
                buffer[left] = buffer[right];
                buffer[right] = temp;
                left++;
                right--;
            }

            return cursor - offset;
        }

        private static string BuildDetailsText(int resolution, int cells)
        {
            return "Res " + resolution + " | Cells " + cells;
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
