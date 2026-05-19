#if UNITY_EDITOR
using Hecton8.Animation.IK;
using Hecton8.Core;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class LeviathanProceduralAnimationTunerWindow : EditorWindow
    {
        private const string FrequencyKey = "H8.LeviathanIK.SwimFrequency";
        private const string AmplitudeKey = "H8.LeviathanIK.SwimAmplitude";
        private const string ToleranceKey = "H8.LeviathanIK.FabrikTolerance";
        private const string DampingKey = "H8.LeviathanIK.SecondaryDamping";

        private Slider _qualitySlider;
        private Slider _frequencySlider;
        private Slider _amplitudeSlider;
        private Slider _toleranceSlider;
        private Slider _dampingSlider;
        private Toggle _overrideToggle;
        private Label _layoutLabel;
        private Label _runtimeLabel;

        [MenuItem("HECTON-8/Animation/Leviathan Procedural Animation Tuner")]
        public static void Open()
        {
            GetWindow<LeviathanProceduralAnimationTunerWindow>("Leviathan IK");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _overrideToggle = new Toggle("Force GlobalQualityWeight");
            _overrideToggle.value = false;
            _overrideToggle.RegisterValueChangedCallback(_ => ApplyQualityOverride());
            rootVisualElement.Add(_overrideToggle);

            _qualitySlider = new Slider("Quality Weight", 0f, 1f);
            _qualitySlider.value = HomeostasisBrain.GlobalQualityWeight;
            _qualitySlider.showInputField = true;
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyQualityOverride());
            rootVisualElement.Add(_qualitySlider);

            _frequencySlider = CreateSlider("Swim Wave Frequency", 0.05f, 3f, EditorPrefs.GetFloat(FrequencyKey, 0.55f));
            _frequencySlider.RegisterValueChangedCallback(evt => ApplyRuntimeFloat(FrequencyKey, "_swimWaveFrequencyHz", evt.newValue));
            rootVisualElement.Add(_frequencySlider);

            _amplitudeSlider = CreateSlider("Sine Amplitude", 0f, 6f, EditorPrefs.GetFloat(AmplitudeKey, 1.1f));
            _amplitudeSlider.RegisterValueChangedCallback(evt => ApplyRuntimeFloat(AmplitudeKey, "_swimWaveAmplitudeMeters", evt.newValue));
            rootVisualElement.Add(_amplitudeSlider);

            _toleranceSlider = CreateSlider("FABRIK Tolerance", 0.001f, 0.5f, EditorPrefs.GetFloat(ToleranceKey, 0.025f));
            _toleranceSlider.RegisterValueChangedCallback(evt => ApplyRuntimeFloat(ToleranceKey, "_fabrikToleranceMeters", evt.newValue));
            rootVisualElement.Add(_toleranceSlider);

            _dampingSlider = CreateSlider("Secondary Damping", 0f, 1f, EditorPrefs.GetFloat(DampingKey, 0.87f));
            _dampingSlider.RegisterValueChangedCallback(evt => ApplyRuntimeFloat(DampingKey, "_verletDamping", evt.newValue));
            rootVisualElement.Add(_dampingSlider);

            _layoutLabel = new Label();
            rootVisualElement.Add(_layoutLabel);
            _runtimeLabel = new Label();
            rootVisualElement.Add(_runtimeLabel);
            RefreshLayoutLabel();
            RefreshRuntimeLabel();
        }

        private void OnInspectorUpdate()
        {
            RefreshLayoutLabel();
            RefreshRuntimeLabel();
        }

        private void ApplyQualityOverride()
        {
            if (_qualitySlider == null || _overrideToggle == null)
                return;

            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(_qualitySlider.value, _overrideToggle.value);
        }

        private static Slider CreateSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max);
            slider.value = Mathf.Clamp(value, min, max);
            slider.showInputField = true;
            return slider;
        }

        private static void ApplyRuntimeFloat(string editorPrefKey, string serializedFieldName, float value)
        {
            EditorPrefs.SetFloat(editorPrefKey, value);
            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                MonoBehaviour[] behaviours = selected[i].GetComponents<MonoBehaviour>();
                for (int j = 0; j < behaviours.Length; j++)
                {
                    MonoBehaviour behaviour = behaviours[j];
                    if (behaviour == null || behaviour.GetType().FullName != "Hecton8.AI.FaunaKinematicsRuntime")
                        continue;

                    SerializedObject serializedObject = new SerializedObject(behaviour);
                    SerializedProperty property = serializedObject.FindProperty(serializedFieldName);
                    if (property == null || property.propertyType != SerializedPropertyType.Float)
                        continue;

                    property.floatValue = value;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(behaviour);
                }
            }
        }

        private void RefreshLayoutLabel()
        {
            if (_layoutLabel == null)
                return;

            int boneBytes = UnsafeUtility.SizeOf<LeviathanBoneDTO>();
            int targetBytes = UnsafeUtility.SizeOf<LeviathanMockTargetDTO>();
            int constraintBytes = UnsafeUtility.SizeOf<LeviathanBoneConstraintsDTO>();
            int colliderBytes = UnsafeUtility.SizeOf<LeviathanCapsuleColliderDTO>();
            int telemetryBytes = UnsafeUtility.SizeOf<LeviathanTerrainIkTelemetryEntry>();
            int snapshotBytes = UnsafeUtility.SizeOf<LeviathanProceduralTunerSnapshot>();
            _layoutLabel.text =
                $"BoneDTO {boneBytes} B | MockTargetDTO {targetBytes} B | ConstraintDTO {constraintBytes} B | ColliderDTO {colliderBytes} B | Telemetry {telemetryBytes} B | Snapshot {snapshotBytes} B";
        }

        private void RefreshRuntimeLabel()
        {
            if (_runtimeLabel == null)
                return;

            if (!TryGetSelectedRuntime(out ILeviathanProceduralTunerSource runtime))
            {
                _runtimeLabel.text = "Runtime: no selected FaunaKinematicsRuntime";
                return;
            }

            runtime.GetLeviathanProceduralTunerSnapshot(out LeviathanProceduralTunerSnapshot snapshot);
            _runtimeLabel.text = $"Runtime bones {snapshot.ActiveSegmentCount} | Burst {snapshot.BurstSolveMicros:0.0} us | Iter {snapshot.ConstraintIterations} | Quality {snapshot.GlobalQualityWeight:0.00}";
        }

        private static bool TryGetSelectedRuntime(out ILeviathanProceduralTunerSource runtime)
        {
            runtime = null;
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
                return false;

            Component component = selected.GetComponent("FaunaKinematicsRuntime");
            runtime = component as ILeviathanProceduralTunerSource;
            return runtime != null;
        }
    }
}
#endif
