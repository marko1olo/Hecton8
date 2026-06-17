#if UNITY_EDITOR
using System.IO;
using Hecton8.Animation.KineticCharacter;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class KineticCharacterAnimationTunerWindow : EditorWindow
    {
        private const string DefaultCsvRelativePath = "Assets/_Project/Data/character_rig_constraints.csv";
        private delegate void TuningMutator(ref KineticCharacterTuningDTO dto);

        private Label _runtimeLabel;
        private Label _layoutLabel;
        private Slider _frequencySlider;
        private Slider _amplitudeSlider;
        private Slider _ikToleranceSlider;
        private Slider _breathingSlider;
        private Slider _qualitySlider;
        private int _lastRuntimeMatrixCount = -1;
        private int _lastRuntimeQualityMilli = -1;

        [MenuItem("Hecton8/Animation/Procedural Animation Tuner")]
        private static void Open()
        {
            GetWindow<KineticCharacterAnimationTunerWindow>("Procedural Animation Tuner");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshReadout;
            EditorApplication.update += RefreshReadout;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshReadout;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _runtimeLabel = new Label("Runtime: inactive");
            _layoutLabel = new Label(ResolveLayoutReport());
            root.Add(_runtimeLabel);
            root.Add(_layoutLabel);

            _frequencySlider = AddSlider(root, "Swim Wave Frequency", 0.05f, 8f, OnFrequencyChanged);
            _amplitudeSlider = AddSlider(root, "Sine Amplitude", 0f, 0.35f, OnAmplitudeChanged);
            _ikToleranceSlider = AddSlider(root, "IK Tolerance", 0.0005f, 0.08f, OnIkToleranceChanged);
            _breathingSlider = AddSlider(root, "Breathing Intensity", 0f, 0.08f, OnBreathingChanged);
            _qualitySlider = AddSlider(root, "Global Quality Weight", 0f, 1f, OnQualityChanged);

            Button mockButton = new Button(GenerateMockRig) { text = "Generate Emergency Mock Rig" };
            Button csvButton = new Button(LoadCsv) { text = "Load character_rig_constraints.csv" };
            root.Add(mockButton);
            root.Add(csvButton);
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max, System.Action<float> callback)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt => callback(evt.newValue));
            root.Add(slider);
            return slider;
        }

        private void RefreshReadout()
        {
            if (_runtimeLabel == null)
                return;

            if (!Application.isPlaying ||
                !KineticCharacterAnimatorRuntime.TryGetActiveRuntimeInstance(out KineticCharacterAnimatorRuntime runtime))
            {
                _runtimeLabel.text = "Runtime: inactive";
                return;
            }

            int matrixCount = 0;
            if (runtime.TryGetKineticGraphicsBuffer(out GraphicsBuffer buffer, out int uploadedMatrices))
                matrixCount = math.min(uploadedMatrices, buffer != null ? buffer.count : 0);

            if (runtime.TryResolveTuningForEditor(out NativeArray<KineticCharacterTuningDTO>.ReadOnly tuning) &&
                tuning.IsCreated &&
                tuning.Length > 0)
            {
                KineticCharacterTuningDTO dto = KineticCharacterSanitizer.SanitizeTuning(tuning[0]);
                _frequencySlider?.SetValueWithoutNotify(dto.LocomotionFrequencyHz);
                _amplitudeSlider?.SetValueWithoutNotify(dto.LocomotionAmplitudeMeters);
                _ikToleranceSlider?.SetValueWithoutNotify(dto.IkToleranceMeters);
                _breathingSlider?.SetValueWithoutNotify(dto.BreathingAmplitudeMeters);
                _qualitySlider?.SetValueWithoutNotify(dto.GlobalQualityWeight);
                int qualityMilli = math.clamp((int)math.round(math.saturate(dto.GlobalQualityWeight) * 1000f), 0, 1000);
                if (_lastRuntimeMatrixCount != matrixCount || _lastRuntimeQualityMilli != qualityMilli)
                {
                    _lastRuntimeMatrixCount = matrixCount;
                    _lastRuntimeQualityMilli = qualityMilli;
                    _runtimeLabel.text = BuildRuntimeReadout(matrixCount, qualityMilli);
                }
            }
            else
            {
                _lastRuntimeMatrixCount = -1;
                _lastRuntimeQualityMilli = -1;
                _runtimeLabel.text = "Runtime: active | tuning unavailable";
            }
        }

        private static void MutateTuning(TuningMutator mutator)
        {
            if (!Application.isPlaying ||
                !KineticCharacterAnimatorRuntime.TryGetActiveRuntimeInstance(out KineticCharacterAnimatorRuntime runtime) ||
                !runtime.TryResolveTuningForEditor(out NativeArray<KineticCharacterTuningDTO>.ReadOnly tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            KineticCharacterTuningDTO dto = tuning[0];
            mutator(ref dto);
            runtime.TryApplyEditorTuning(dto);
        }

        private static void OnFrequencyChanged(float value)
        {
            MutateTuning((ref KineticCharacterTuningDTO dto) => dto.LocomotionFrequencyHz = value);
        }

        private static void OnAmplitudeChanged(float value)
        {
            MutateTuning((ref KineticCharacterTuningDTO dto) => dto.LocomotionAmplitudeMeters = value);
        }

        private static void OnIkToleranceChanged(float value)
        {
            MutateTuning((ref KineticCharacterTuningDTO dto) => dto.IkToleranceMeters = value);
        }

        private static void OnBreathingChanged(float value)
        {
            MutateTuning((ref KineticCharacterTuningDTO dto) => dto.BreathingAmplitudeMeters = value);
        }

        private static void OnQualityChanged(float value)
        {
            MutateTuning((ref KineticCharacterTuningDTO dto) => dto.GlobalQualityWeight = value);
        }

        private static void GenerateMockRig()
        {
            if (Application.isPlaying &&
                KineticCharacterAnimatorRuntime.TryGetActiveRuntimeInstance(out KineticCharacterAnimatorRuntime runtime))
            {
                runtime.GenerateEmergencyMockRig();
            }
        }

        private static void LoadCsv()
        {
            if (!Application.isPlaying ||
                !KineticCharacterAnimatorRuntime.TryGetActiveRuntimeInstance(out KineticCharacterAnimatorRuntime runtime))
            {
                return;
            }

            string path = Path.Combine(Directory.GetCurrentDirectory(), DefaultCsvRelativePath);
            if (!File.Exists(path))
                return;

            byte[] bytes = File.ReadAllBytes(path); // EDITOR ONLY: explicit designer import button, not runtime Tick.
            runtime.TryApplyCsvProfileBytes(bytes);
        }

        private static string ResolveLayoutReport()
        {
            bool valid =
                KineticCharacterAnimatorLayout.Validate() &&
                OffsetOf<ProceduralBoneDTO>(nameof(ProceduralBoneDTO.LocalToWorld)) == 0 &&
                OffsetOf<ProceduralIKTargetDTO>(nameof(ProceduralIKTargetDTO.LocalPosition)) == 0 &&
                OffsetOf<ProceduralIKTargetDTO>(nameof(ProceduralIKTargetDTO.Weight01)) == 12 &&
                OffsetOf<KineticCharacterFrameInputDTO>(nameof(KineticCharacterFrameInputDTO.RootSectorX)) == 0 &&
                OffsetOf<KineticCharacterFrameInputDTO>(nameof(KineticCharacterFrameInputDTO.ToolPoseMatrix)) == 144 &&
                OffsetOf<KineticCharacterFrameInputDTO>(nameof(KineticCharacterFrameInputDTO.ActiveToolHash)) == 248 &&
                UnsafeUtility.SizeOf<KineticCharacterFrameInputDTO>() == KineticCharacterAnimatorConstants.FrameInputBytes;

            return "Layout: " +
                   (valid ? "valid" : "invalid") +
                   " | BoneDTO " + UnsafeUtility.SizeOf<ProceduralBoneDTO>() +
                   " | IKTargetDTO " + UnsafeUtility.SizeOf<ProceduralIKTargetDTO>() +
                   " | FrameInputDTO " + UnsafeUtility.SizeOf<KineticCharacterFrameInputDTO>() +
                   " | TelemetryDTO " + UnsafeUtility.SizeOf<KineticAnimationTelemetryEntry>();
        }

        private static string BuildRuntimeReadout(int matrixCount, int qualityMilli)
        {
            int ones = qualityMilli / 1000;
            int fraction = qualityMilli - ones * 1000;
            int hundreds = fraction / 100;
            int tens = (fraction / 10) % 10;
            int units = fraction % 10;
            return "Runtime: active | matrices " +
                   matrixCount +
                   " | quality " +
                   Digit(ones) +
                   "." +
                   Digit(hundreds) +
                   Digit(tens) +
                   Digit(units);
        }

        private static char Digit(int value)
        {
            return (char)('0' + math.clamp(value, 0, 9));
        }

        private static int OffsetOf<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }
}
#endif
