#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Habitat.Deformation.Editor
{
    /// <summary>
    /// Editor-only facade for DataVault hull integrity tuning and dent DTO visualization.
    /// </summary>
    public sealed class HullIntegrityTunerWindow : EditorWindow
    {
        private Slider _baseSipMultiplier;
        private Slider _crushDepthGradient;
        private Slider _dentRadius;
        private Slider _dentDepth;
        private Label _statusLabel;
        private int _suppressWrite;

        [MenuItem("Hecton-8/Habitat/Hull Integrity Tuner")]
        public static void Open()
        {
            HullIntegrityTunerWindow window = GetWindow<HullIntegrityTunerWindow>();
            window.titleContent = new GUIContent("Hull Integrity Tuner");
            window.minSize = new Vector2(340f, 220f);
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            EditorApplication.update += RefreshFromRuntime;
            SceneView.duringSceneGui -= OnDrawGizmos;
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 10f;
            root.style.paddingBottom = 10f;

            _statusLabel = new Label("Play Mode runtime not bound");
            root.Add(_statusLabel);

            _baseSipMultiplier = CreateSlider("Base SIP Multiplier", 0.1f, 10f);
            _crushDepthGradient = CreateSlider("Crush Depth Gradient", 0.000001f, 0.1f);
            _dentRadius = CreateSlider("Dent Radius", 0.05f, 8f);
            _dentDepth = CreateSlider("Dent Depth", 0.001f, 2f);

            root.Add(_baseSipMultiplier);
            root.Add(_crushDepthGradient);
            root.Add(_dentRadius);
            root.Add(_dentDepth);

            Button injectDent = new Button(InjectMockDent) { text = "Inject Mock Dent" };
            root.Add(injectDent);

            RefreshFromRuntime();
        }

        private Slider CreateSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(OnSliderChanged);
            return slider;
        }

        private void OnSliderChanged(ChangeEvent<float> evt)
        {
            WriteTuning();
        }

        private void RefreshFromRuntime()
        {
            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null || !runtime.TryGetTuning(out HullIntegrityTuningDTO tuning))
            {
                if (_statusLabel != null)
                    _statusLabel.text = "Play Mode runtime not bound";
                return;
            }

            if (_statusLabel != null)
                _statusLabel.text = "Runtime bound";

            _suppressWrite = 1;
            SetSliderWithoutNotify(_baseSipMultiplier, tuning.BaseSipMultiplier);
            SetSliderWithoutNotify(_crushDepthGradient, tuning.CrushDepthGradient);
            SetSliderWithoutNotify(_dentRadius, tuning.DentRadius);
            SetSliderWithoutNotify(_dentDepth, tuning.DentDepth);
            _suppressWrite = 0;
        }

        private void WriteTuning()
        {
            if (_suppressWrite != 0)
                return;

            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            runtime.SetTuning(new HullIntegrityTuningDTO
            {
                BaseSipMultiplier = _baseSipMultiplier != null ? _baseSipMultiplier.value : 1f,
                CrushDepthGradient = _crushDepthGradient != null ? _crushDepthGradient.value : 0.00008f,
                DentRadius = _dentRadius != null ? _dentRadius.value : 1.25f,
                DentDepth = _dentDepth != null ? _dentDepth.value : 0.18f
            });
        }

        private void InjectMockDent()
        {
            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            float radius = _dentRadius != null ? _dentRadius.value : 1.25f;
            float depth = _dentDepth != null ? _dentDepth.value : 0.18f;
            runtime.InjectMockDamage(new MockCombatDamageSignal
            {
                LocalPoint = new Unity.Mathematics.float3(0f, 0f, 0f),
                LocalNormal = new Unity.Mathematics.float3(0f, 1f, 0f),
                Magnitude = 120f,
                Radius = radius,
                TargetHash = HullIntegrityConstants.DefaultBaseHash,
                SourceHash = HullIntegrityConstants.AgentHash,
                Frame = 0u,
                DamageType = 0u,
                Depth = depth
            });
        }

        private void OnDrawGizmos(SceneView sceneView)
        {
            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            Transform root = runtime.DentRoot;
            Matrix4x4 localToWorld = root != null ? root.localToWorldMatrix : Matrix4x4.identity;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            for (int i = 0; i < HullIntegrityConstants.MaxDentCapacity; i++)
            {
                if (!runtime.TryGetDent(i, out HullDentDTO dent))
                    continue;

                Vector3 localPoint = new Vector3(dent.Position.x, dent.Position.y, dent.Position.z);
                Vector3 localNormal = new Vector3(dent.Normal.x, dent.Normal.y, dent.Normal.z);
                Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localPoint);
                Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;
                float radius = Mathf.Max(0.01f, dent.Radius);

                Handles.color = Color.red;
                Handles.DrawWireDisc(worldPoint, Vector3.up, radius);
                Handles.DrawWireDisc(worldPoint, Vector3.right, radius);
                Handles.DrawWireDisc(worldPoint, Vector3.forward, radius);
                Handles.color = Color.green;
                Handles.DrawLine(worldPoint, worldPoint + worldNormal * Mathf.Max(0.1f, radius));
            }
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }
    }
}
#endif
