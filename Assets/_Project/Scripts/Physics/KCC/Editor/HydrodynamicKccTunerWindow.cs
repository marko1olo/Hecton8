#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.KCC.Editor
{
    public sealed class HydrodynamicKccTunerWindow : EditorWindow
    {
        private Slider _baseDrag;
        private Slider _fluidDensity;
        private Slider _maxSpeed;
        private Slider _gravityMultiplier;
        private Slider _buoyancyScalar;
        private Slider _quality;
        private VelocityGraphElement _graph;
        private double _nextGraphRepaintTime;

        [MenuItem("HECTON-8/Kinematics/Hydrodynamic KCC Tuner")]
        public static void Open()
        {
            GetWindow<HydrodynamicKccTunerWindow>("Hydro KCC");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _baseDrag = AddSlider("Base Drag", 0f, 3f);
            _fluidDensity = AddSlider("Fluid Density", 0f, 2.5f);
            _maxSpeed = AddSlider("Max Speed", 0.1f, 18f);
            _gravityMultiplier = AddSlider("Gravity Multiplier", 0f, 2.5f);
            _buoyancyScalar = AddSlider("Buoyancy", 0f, 2.5f);
            _quality = AddSlider("Global Quality Weight", 0f, 1f);

            _graph = new VelocityGraphElement();
            _graph.style.height = 112;
            _graph.style.marginTop = 8;
            rootVisualElement.Add(_graph);

            RegisterCallbacks();
            ReadFromVault();
        }

        private Slider AddSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.style.marginBottom = 4;
            rootVisualElement.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _baseDrag.RegisterValueChangedCallback(_ => WriteToVault());
            _fluidDensity.RegisterValueChangedCallback(_ => WriteToVault());
            _maxSpeed.RegisterValueChangedCallback(_ => WriteToVault());
            _gravityMultiplier.RegisterValueChangedCallback(_ => WriteToVault());
            _buoyancyScalar.RegisterValueChangedCallback(_ => WriteToVault());
            _quality.RegisterValueChangedCallback(_ => WriteToVault());
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_graph != null && now >= _nextGraphRepaintTime)
            {
                _nextGraphRepaintTime = now + 0.05d;
                _graph.MarkDirtyRepaint();
            }
        }

        private void ReadFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            VaultBufferHandle<HydrodynamicKccTuningDTO> handle = vault.GetBufferHandle<HydrodynamicKccTuningDTO>(
                BufferID.ShinobuHydroKccTuning,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = handle.Resolve(vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
            if (tuning.MaxSpeed <= 0f)
            {
                tuning.BaseDrag = 0.18f;
                tuning.FluidDensity = 1f;
                tuning.MaxSpeed = 6f;
                tuning.GravityMultiplier = 1f;
                tuning.BuoyancyScalar = 1.08f;
                tuning.GlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
                tuningBuffer[0] = tuning;
            }

            SetSliderWithoutNotify(_baseDrag, tuning.BaseDrag);
            SetSliderWithoutNotify(_fluidDensity, tuning.FluidDensity);
            SetSliderWithoutNotify(_maxSpeed, tuning.MaxSpeed);
            SetSliderWithoutNotify(_gravityMultiplier, tuning.GravityMultiplier);
            SetSliderWithoutNotify(_buoyancyScalar, tuning.BuoyancyScalar);
            SetSliderWithoutNotify(_quality, tuning.GlobalQualityWeight);
        }

        private void WriteToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            VaultBufferHandle<HydrodynamicKccTuningDTO> handle = vault.GetBufferHandle<HydrodynamicKccTuningDTO>(
                BufferID.ShinobuHydroKccTuning,
                1,
                SystemID.Physics,
                NativeArrayOptions.ClearMemory);
            NativeArray<HydrodynamicKccTuningDTO> tuningBuffer = handle.Resolve(vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length == 0)
                return;

            HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
            tuning.BaseDrag = math.max(0f, _baseDrag.value);
            tuning.FluidDensity = math.max(0f, _fluidDensity.value);
            tuning.MaxSpeed = math.max(0.1f, _maxSpeed.value);
            tuning.GravityMultiplier = math.max(0f, _gravityMultiplier.value);
            tuning.BuoyancyScalar = math.max(0f, _buoyancyScalar.value);
            tuning.GlobalQualityWeight = math.saturate(_quality.value);
            tuning.ProfileHash = HydrodynamicKccMath.SourceHash;
            tuning.Flags = 1u;
            tuningBuffer[0] = tuning;
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private sealed class VelocityGraphElement : VisualElement
        {
            public VelocityGraphElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1.5f;
                painter.strokeColor = new Color(0.1f, 0.8f, 0.7f, 1f);
                painter.BeginPath();

                if (!HydrodynamicKccRuntime.TryGetEditorTelemetryVaultView(out NativeArray<KinematicTelemetryEntry> telemetry, out int cursorIndex, out int telemetryLength) ||
                    telemetryLength <= 0)
                {
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.Stroke();
                    return;
                }

                float maxSpeed = 0.001f;
                cursorIndex = math.clamp(cursorIndex, 0, telemetryLength - 1);
                int startIndex = (cursorIndex + 1) % telemetryLength;
                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = (startIndex + i) % telemetryLength;
                    maxSpeed = math.max(maxSpeed, telemetry[telemetryIndex].Speed);
                }

                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = (startIndex + i) % telemetryLength;
                    KinematicTelemetryEntry entry = telemetry[telemetryIndex];
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetryLength - 1f));
                    float y = rect.yMax - rect.height * math.saturate(entry.Speed / maxSpeed);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
