#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class Shinobu305ProceduralIkTunerWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 0.1d;
        private const int HistogramSampleCount = 64;

        private readonly float[] _samplesMicros = new float[HistogramSampleCount];
        private readonly VisualElement[] _bars = new VisualElement[HistogramSampleCount];
        private readonly object[] _telemetryArgs = new object[5];
        private readonly object[] _snapshotArgs = new object[1];
        private readonly object[] _applyArgs = new object[4];
        private Label _readout;
        private ObjectField _targetField;
        private Slider _amplitudeSlider;
        private Slider _speedSlider;
        private Slider _qualitySlider;
        private SliderInt _iterationsSlider;
        private UnityEngine.Object _target;
        private MethodInfo _telemetryMethod;
        private MethodInfo _snapshotMethod;
        private MethodInfo _applyMethod;
        private MethodInfo _clearOverrideMethod;
        private Type _snapshotType;
        private FieldInfo _activeSegmentsField;
        private FieldInfo _constraintIterationsField;
        private FieldInfo _burstSolveMicrosField;
        private FieldInfo _qualityWeightField;
        private int _sampleCursor;
        private double _nextRefresh;

        [MenuItem("Tools/Hecton-8/Procedural IK Tuner")]
        private static void Open()
        {
            GetWindow<Shinobu305ProceduralIkTunerWindow>("Procedural IK Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _targetField = new ObjectField("Leviathan Runtime")
            {
                objectType = typeof(MonoBehaviour),
                allowSceneObjects = true
            };
            _targetField.RegisterValueChangedCallback(evt =>
            {
                _target = evt.newValue;
                BindTarget(_target);
            });
            rootVisualElement.Add(_targetField);

            _readout = new Label("No active FaunaKinematicsRuntime snapshot");
            rootVisualElement.Add(_readout);

            _amplitudeSlider = new Slider("Sine Wave Amplitude", 0f, 6f) { value = 1.1f };
            _speedSlider = new Slider("Sine Wave Speed", 0.05f, 3f) { value = 0.55f };
            _iterationsSlider = new SliderInt("Max FABRIK Iterations", 1, 10) { value = 8 };
            _qualitySlider = new Slider("Global Quality Override", 0f, 1f) { value = 1f };
            RegisterApply(_amplitudeSlider);
            RegisterApply(_speedSlider);
            _iterationsSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            RegisterApply(_qualitySlider);
            rootVisualElement.Add(_amplitudeSlider);
            rootVisualElement.Add(_speedSlider);
            rootVisualElement.Add(_iterationsSlider);
            rootVisualElement.Add(_qualitySlider);

            Button clearOverride = new Button(ClearQualityOverride) { text = "Clear Quality Override" };
            rootVisualElement.Add(clearOverride);

            VisualElement histogram = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = 72,
                    marginTop = 8
                }
            };
            for (int i = 0; i < HistogramSampleCount; i++)
            {
                VisualElement bar = new VisualElement
                {
                    style =
                    {
                        width = 4,
                        height = 1,
                        marginRight = 1,
                        backgroundColor = new Color(0.95f, 0.8f, 0.15f, 1f),
                        alignSelf = Align.FlexEnd
                    }
                };
                _bars[i] = bar;
                histogram.Add(bar);
            }

            rootVisualElement.Add(histogram);
            TryAutoBind();
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshSnapshot;
            EditorApplication.update += RefreshSnapshot;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshSnapshot;
        }

        private void RegisterApply(Slider slider)
        {
            slider.RegisterValueChangedCallback(_ => ApplyTuning());
        }

        private void TryAutoBind()
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour candidate = behaviours[i];
                if (candidate == null)
                    continue;

                MethodInfo method = candidate.GetType().GetMethod("GetLeviathanProceduralTunerSnapshot", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo telemetry = candidate.GetType().GetMethod("TryGetLeviathanProceduralTelemetryForEditor", BindingFlags.Public | BindingFlags.Instance);
                if (method == null && telemetry == null)
                    continue;

                _target = candidate;
                _targetField.value = candidate;
                BindTarget(candidate);
                return;
            }
        }

        private void BindTarget(UnityEngine.Object target)
        {
            _telemetryMethod = null;
            _snapshotMethod = null;
            _applyMethod = null;
            _clearOverrideMethod = null;
            _snapshotType = null;
            if (target == null)
                return;

            Type type = target.GetType();
            _telemetryMethod = type.GetMethod("TryGetLeviathanProceduralTelemetryForEditor", BindingFlags.Public | BindingFlags.Instance);
            _snapshotMethod = type.GetMethod("GetLeviathanProceduralTunerSnapshot", BindingFlags.Public | BindingFlags.Instance);
            _applyMethod = type.GetMethod("ApplyLeviathanProceduralEditorTuning", BindingFlags.Public | BindingFlags.Instance);
            _clearOverrideMethod = type.GetMethod("ClearLeviathanProceduralEditorQualityOverride", BindingFlags.Public | BindingFlags.Instance);
            ParameterInfo[] parameters = _snapshotMethod != null ? _snapshotMethod.GetParameters() : Array.Empty<ParameterInfo>();
            if (parameters.Length == 1 && parameters[0].IsOut)
            {
                _snapshotType = parameters[0].ParameterType.GetElementType();
                if (_snapshotType == null)
                    return;

                _activeSegmentsField = _snapshotType.GetField("ActiveSegmentCount");
                _constraintIterationsField = _snapshotType.GetField("ConstraintIterations");
                _burstSolveMicrosField = _snapshotType.GetField("BurstSolveMicros");
                _qualityWeightField = _snapshotType.GetField("GlobalQualityWeight");
            }
        }

        private void RefreshSnapshot()
        {
            if (EditorApplication.timeSinceStartup < _nextRefresh)
                return;

            _nextRefresh = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            if (_target == null)
                TryAutoBind();

            if (_target == null || (_telemetryMethod == null && (_snapshotMethod == null || _snapshotType == null)))
            {
                _readout.text = "No active FaunaKinematicsRuntime snapshot";
                return;
            }

            if (!TryReadTelemetry(out int activeSegments, out int iterations, out float micros, out float quality, out uint flags) &&
                !TryReadSnapshot(out activeSegments, out iterations, out micros, out quality, out flags))
            {
                _readout.text = "No active FaunaKinematicsRuntime telemetry";
                return;
            }

            _samplesMicros[_sampleCursor] = Mathf.Max(0f, micros);
            _sampleCursor = (_sampleCursor + 1) & (HistogramSampleCount - 1);
            UpdateHistogram();
            _readout.text = "Bones " + activeSegments + " | Iter " + iterations + " | Burst " + micros.ToString("0.00") + " us | Q " + quality.ToString("0.000") + " | Flags 0x" + flags.ToString("X8");
        }

        private bool TryReadTelemetry(out int activeSegments, out int iterations, out float micros, out float quality, out uint flags)
        {
            activeSegments = 0;
            iterations = 0;
            micros = 0f;
            quality = 0f;
            flags = 0u;
            if (_target == null || _telemetryMethod == null)
                return false;

            _telemetryArgs[0] = activeSegments;
            _telemetryArgs[1] = iterations;
            _telemetryArgs[2] = micros;
            _telemetryArgs[3] = quality;
            _telemetryArgs[4] = flags;
            object result = _telemetryMethod.Invoke(_target, _telemetryArgs);
            if (!(result is bool success) || !success)
                return false;

            activeSegments = ReadBoxedInt(_telemetryArgs[0]);
            iterations = ReadBoxedInt(_telemetryArgs[1]);
            micros = ReadBoxedFloat(_telemetryArgs[2]);
            quality = ReadBoxedFloat(_telemetryArgs[3]);
            flags = ReadBoxedUInt(_telemetryArgs[4]);
            return true;
        }

        private bool TryReadSnapshot(out int activeSegments, out int iterations, out float micros, out float quality, out uint flags)
        {
            activeSegments = 0;
            iterations = 0;
            micros = 0f;
            quality = 0f;
            flags = 0u;
            if (_target == null || _snapshotMethod == null || _snapshotType == null)
                return false;

            _snapshotArgs[0] = Activator.CreateInstance(_snapshotType);
            _snapshotMethod.Invoke(_target, _snapshotArgs);
            object snapshot = _snapshotArgs[0];
            activeSegments = ReadInt(_activeSegmentsField, snapshot);
            iterations = ReadInt(_constraintIterationsField, snapshot);
            micros = ReadFloat(_burstSolveMicrosField, snapshot);
            quality = ReadFloat(_qualityWeightField, snapshot);
            return true;
        }

        private void UpdateHistogram()
        {
            float max = 1f;
            for (int i = 0; i < HistogramSampleCount; i++)
                max = Mathf.Max(max, _samplesMicros[i]);

            for (int i = 0; i < HistogramSampleCount; i++)
                _bars[i].style.height = Mathf.Clamp((_samplesMicros[i] / max) * 70f, 1f, 70f);
        }

        private void ApplyTuning()
        {
            if (_target == null || _applyMethod == null)
                return;

            _applyArgs[0] = _amplitudeSlider.value;
            _applyArgs[1] = _speedSlider.value;
            _applyArgs[2] = _iterationsSlider.value;
            _applyArgs[3] = _qualitySlider.value;
            _applyMethod.Invoke(_target, _applyArgs);
        }

        private void ClearQualityOverride()
        {
            if (_target != null && _clearOverrideMethod != null)
                _clearOverrideMethod.Invoke(_target, null);
        }

        private static int ReadInt(FieldInfo field, object target)
        {
            return field != null ? (int)field.GetValue(target) : 0;
        }

        private static float ReadFloat(FieldInfo field, object target)
        {
            return field != null ? (float)field.GetValue(target) : 0f;
        }

        private static int ReadBoxedInt(object value)
        {
            return value is int typed ? typed : 0;
        }

        private static float ReadBoxedFloat(object value)
        {
            return value is float typed ? typed : 0f;
        }

        private static uint ReadBoxedUInt(object value)
        {
            return value is uint typed ? typed : 0u;
        }
    }
}
#endif
