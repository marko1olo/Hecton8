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
        private Slider _metalPlasticity;
        private Slider _maxDentDepth;
        private Slider _pressureBuckleThreshold;
        private Slider _visualOverkillLimit;
        private Label _statusLabel;
        private Label _histogramLabel;
        private int _suppressWrite;

        [MenuItem("Hecton-8/Habitat/Hull Deformation Tuner")]
        public static void Open()
        {
            HullIntegrityTunerWindow window = GetWindow<HullIntegrityTunerWindow>();
            window.titleContent = new GUIContent("Hull Deformation Tuner");
            window.minSize = new Vector2(380f, 360f);
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            EditorApplication.update += RefreshFromRuntime;
            SceneView.duringSceneGui -= DrawSceneGizmos;
            SceneView.duringSceneGui += DrawSceneGizmos;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshFromRuntime;
            SceneView.duringSceneGui -= DrawSceneGizmos;
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
            _metalPlasticity = CreateSlider("Metal Plasticity", 0.0001f, 4f);
            _maxDentDepth = CreateSlider("Max Dent Depth", 0.001f, 2f);
            _pressureBuckleThreshold = CreateSlider("Pressure Buckle Threshold", 0f, 1f);
            _visualOverkillLimit = CreateSlider("Visual Overkill Limit", 0f, 1f);

            root.Add(_baseSipMultiplier);
            root.Add(_crushDepthGradient);
            root.Add(_dentRadius);
            root.Add(_dentDepth);
            root.Add(_metalPlasticity);
            root.Add(_maxDentDepth);
            root.Add(_pressureBuckleThreshold);
            root.Add(_visualOverkillLimit);

            Button injectDent = new Button(InjectMockDent) { text = "Inject Mock Dent" };
            root.Add(injectDent);

            Button implosion = new Button(SimulateCatastrophicImplosion) { text = "Simulate Catastrophic Implosion" };
            root.Add(implosion);

            _histogramLabel = new Label("Dents: 0 | minor 0 | major 0 | breaches 0");
            root.Add(_histogramLabel);

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
            RefreshHistogram(runtime);

            _suppressWrite = 1;
            SetSliderWithoutNotify(_baseSipMultiplier, tuning.BaseSipMultiplier);
            SetSliderWithoutNotify(_crushDepthGradient, tuning.CrushDepthGradient);
            SetSliderWithoutNotify(_dentRadius, tuning.DentRadius);
            SetSliderWithoutNotify(_dentDepth, tuning.DentDepth);
            SetSliderWithoutNotify(_metalPlasticity, tuning.MetalPlasticity);
            SetSliderWithoutNotify(_maxDentDepth, tuning.MaxDentDepth);
            SetSliderWithoutNotify(_pressureBuckleThreshold, tuning.PressureBuckleThreshold01);
            SetSliderWithoutNotify(_visualOverkillLimit, tuning.VisualOverkillLimit);
            _suppressWrite = 0;
        }

        private void RefreshHistogram(HullIntegrityRuntime runtime)
        {
            if (_histogramLabel == null)
                return;

            int minor = 0;
            int major = 0;
            int breaches = 0;
            for (int i = 0; i < HullIntegrityConstants.MaxDentCapacity; i++)
            {
                if (!runtime.TryGetDeformation(i, out DeformationStateDTO deformation))
                    continue;

                if ((deformation.Flags & DeformationStateFlags.Breach) != 0u)
                {
                    breaches++;
                    continue;
                }

                if (deformation.Depth >= 0.18f)
                    major++;
                else
                    minor++;
            }

            _histogramLabel.text = $"Dents: {minor + major + breaches} | minor {minor} | major {major} | breaches {breaches}";
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
                DentDepth = _dentDepth != null ? _dentDepth.value : 0.18f,
                MetalPlasticity = _metalPlasticity != null ? _metalPlasticity.value : 1f,
                MaxDentDepth = _maxDentDepth != null ? _maxDentDepth.value : 0.35f,
                PressureBuckleThreshold01 = _pressureBuckleThreshold != null ? _pressureBuckleThreshold.value : 0.82f,
                VisualOverkillLimit = _visualOverkillLimit != null ? _visualOverkillLimit.value : 1f
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

        private void SimulateCatastrophicImplosion()
        {
            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            runtime.GenerateMockHullImpacts(200, 4f);
        }

        private void DrawSceneGizmos(SceneView sceneView)
        {
            HullIntegrityRuntime runtime = HullIntegrityRuntime.ActiveRuntime;
            if (runtime == null)
                return;

            Transform root = runtime.DentRoot;
            Matrix4x4 localToWorld = root != null ? root.localToWorldMatrix : Matrix4x4.identity;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            int deformationCount = 0;
            for (int i = 0; i < HullIntegrityConstants.MaxDentCapacity; i++)
            {
                if (!runtime.TryGetDeformation(i, out DeformationStateDTO deformation))
                    continue;

                deformationCount++;
                Vector3 localPoint = new Vector3(deformation.LocalPosition.x, deformation.LocalPosition.y, deformation.LocalPosition.z);
                Vector3 localNormal = new Vector3(deformation.Normal.x, deformation.Normal.y, deformation.Normal.z);
                Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localPoint);
                Vector3 worldNormal = localToWorld.MultiplyVector(localNormal).normalized;
                float radius = Mathf.Max(0.01f, deformation.Radius);
                bool breach = (deformation.Flags & DeformationStateFlags.Breach) != 0u;

                Handles.color = breach ? Color.red : Color.yellow;
                Handles.SphereHandleCap(0, worldPoint, Quaternion.identity, radius * 0.18f, EventType.Repaint);
                Handles.DrawLine(worldPoint, worldPoint + worldNormal * Mathf.Max(0.1f, radius));
            }

            if (deformationCount > 0)
                return;

            for (int i = 0; i < HullIntegrityConstants.MaxDentCapacity; i++)
            {
                if (!runtime.TryGetDent(i, out HullDentDTO dent))
                    continue;

                Vector3 localPoint = new Vector3(dent.Position.x, dent.Position.y, dent.Position.z);
                Vector3 worldPoint = localToWorld.MultiplyPoint3x4(localPoint);
                Handles.color = Color.yellow;
                Handles.SphereHandleCap(0, worldPoint, Quaternion.identity, Mathf.Max(0.02f, dent.Radius * 0.12f), EventType.Repaint);
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
