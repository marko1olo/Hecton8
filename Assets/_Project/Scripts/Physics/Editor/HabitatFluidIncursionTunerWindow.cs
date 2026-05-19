#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Editor
{
    public sealed class HabitatFluidIncursionTunerWindow : EditorWindow
    {
        private Slider _transferRate;
        private Slider _maxTransfer;
        private Slider _ingressCap;
        private Slider _discharge;
        private Slider _waterDensity;
        private Slider _visualWobble;
        private Slider _acousticGain;
        private Label _readout;
        private VisualElement _barRoot;
        private VisualElement[] _bars;
        private Label[] _barLabels;

        private const int MaxVisibleBars = 64;

        [MenuItem("HECTON-8/Flood Control Tuner")]
        public static void Open()
        {
            HabitatFluidIncursionTunerWindow window = GetWindow<HabitatFluidIncursionTunerWindow>();
            window.titleContent.text = "Flood Control";
            window.minSize = new Vector2(420f, 360f);
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;

            _transferRate = AddSlider(root, "Transfer Rate", 0f, 2f);
            _maxTransfer = AddSlider(root, "Max Transfer M3", 0.01f, 8f);
            _ingressCap = AddSlider(root, "Ingress Cap", 0.01f, 1f);
            _discharge = AddSlider(root, "Discharge", 0.1f, 1f);
            _waterDensity = AddSlider(root, "Water Density", 900f, 1200f);
            _visualWobble = AddSlider(root, "Waterline Wobble", 0f, 2f);
            _acousticGain = AddSlider(root, "Acoustic Gain", 0f, 2f);
            _readout = new Label();
            root.Add(_readout);

            Button refresh = new Button(RefreshFromVault) { text = "Refresh" };
            root.Add(refresh);

            _barRoot = new VisualElement();
            _barRoot.style.marginTop = 8;
            root.Add(_barRoot);
            BuildVolumeBars();

            RegisterCallbacks();
            RefreshFromVault();
        }

        private Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max) { showInputField = true };
            slider.style.marginBottom = 4;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _transferRate.RegisterValueChangedCallback(evt => Mutate(TuningField.TransferRate, evt.newValue));
            _maxTransfer.RegisterValueChangedCallback(evt => Mutate(TuningField.MaxTransfer, evt.newValue));
            _ingressCap.RegisterValueChangedCallback(evt => Mutate(TuningField.IngressCap, evt.newValue));
            _discharge.RegisterValueChangedCallback(evt => Mutate(TuningField.Discharge, evt.newValue));
            _waterDensity.RegisterValueChangedCallback(evt => Mutate(TuningField.WaterDensity, evt.newValue));
            _visualWobble.RegisterValueChangedCallback(evt => Mutate(TuningField.VisualWobble, evt.newValue));
            _acousticGain.RegisterValueChangedCallback(evt => Mutate(TuningField.AcousticGain, evt.newValue));
        }

        private void RefreshFromVault()
        {
            if (!TryGetTuning(out NativeArray<FluidIncursionTuningDTO> tuning))
            {
                if (_readout != null)
                    _readout.text = "Vault buffer unavailable.";
                return;
            }

            FluidIncursionTuningDTO dto = tuning[0];
            SetValueWithoutNotify(_transferRate, dto.TransferRate01PerSecond);
            SetValueWithoutNotify(_maxTransfer, dto.MaxTransferPerNodeM3);
            SetValueWithoutNotify(_ingressCap, dto.MaxIngressPerSecondNormalized);
            SetValueWithoutNotify(_discharge, dto.DischargeCoefficient);
            SetValueWithoutNotify(_waterDensity, dto.WaterDensityKgPerM3);
            SetValueWithoutNotify(_visualWobble, dto.VisualWobbleScalar);
            SetValueWithoutNotify(_acousticGain, dto.AcousticMuffleGain);
            _readout.text = $"q={dto.GlobalQualityWeight:0.000} iter={dto.SolverIterations} rooms={dto.CompartmentCount} edges={dto.EdgeCount}";
        }

        private void BuildVolumeBars()
        {
            _bars = new VisualElement[MaxVisibleBars];
            _barLabels = new Label[MaxVisibleBars];
            for (int i = 0; i < MaxVisibleBars; i++)
            {
                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = 8;
                row.style.marginBottom = 2;

                Label label = new Label(i.ToString("00"));
                label.style.width = 28;
                label.style.fontSize = 8;
                row.Add(label);

                VisualElement track = new VisualElement();
                track.style.flexGrow = 1f;
                track.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.9f);
                track.style.height = 8;

                VisualElement bar = new VisualElement();
                bar.style.width = Length.Percent(0f);
                bar.style.height = 8;
                bar.style.backgroundColor = new Color(0f, 0.18f, 0.65f, 0.95f);
                track.Add(bar);
                row.Add(track);

                _bars[i] = bar;
                _barLabels[i] = label;
                _barRoot.Add(row);
            }
        }

        private void OnEditorUpdate()
        {
            if (_bars == null)
                return;

            UpdateVolumeBars();
        }

        private void UpdateVolumeBars()
        {
            if (!TryGetCompartmentTelemetry(out NativeArray<FluidCompartmentTelemetryDTO> telemetry))
            {
                ClearBars();
                return;
            }

            int visibleCount = math.min(MaxVisibleBars, telemetry.Length);
            for (int i = 0; i < visibleCount; i++)
            {
                FluidCompartmentTelemetryDTO dto = telemetry[i];
                _bars[i].style.width = Length.Percent(math.saturate(dto.Fill01) * 100f);
                _barLabels[i].style.display = DisplayStyle.Flex;
            }

            for (int i = visibleCount; i < MaxVisibleBars; i++)
            {
                _bars[i].style.width = Length.Percent(0f);
                _barLabels[i].style.display = DisplayStyle.None;
            }
        }

        private void ClearBars()
        {
            for (int i = 0; i < MaxVisibleBars; i++)
            {
                _bars[i].style.width = Length.Percent(0f);
                _barLabels[i].style.display = DisplayStyle.Flex;
            }
        }

        private static void SetValueWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private static bool TryGetTuning(out NativeArray<FluidIncursionTuningDTO> tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.ActiveBurstLockMask == 0u &&
                   vault.TryGetBuffer(BufferID.ShinobuFluidTuning, out tuning) &&
                   tuning.IsCreated &&
                   tuning.Length > 0;
        }

        private static bool TryGetCompartmentTelemetry(out NativeArray<FluidCompartmentTelemetryDTO> telemetry)
        {
            telemetry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.ActiveBurstLockMask == 0u &&
                   vault.TryGetBuffer(BufferID.ShinobuFluidCompartmentTelemetry, out telemetry) &&
                   telemetry.IsCreated &&
                   telemetry.Length > 0;
        }

        private static void Mutate(TuningField field, float value)
        {
            if (!TryGetTuning(out NativeArray<FluidIncursionTuningDTO> tuning))
                return;

            FluidIncursionTuningDTO dto = tuning[0];
            switch (field)
            {
                case TuningField.TransferRate:
                    dto.TransferRate01PerSecond = math.max(0f, value);
                    break;
                case TuningField.MaxTransfer:
                    dto.MaxTransferPerNodeM3 = math.max(0.01f, value);
                    break;
                case TuningField.IngressCap:
                    dto.MaxIngressPerSecondNormalized = math.max(0.01f, value);
                    break;
                case TuningField.Discharge:
                    dto.DischargeCoefficient = math.clamp(value, 0.1f, 1f);
                    break;
                case TuningField.WaterDensity:
                    dto.WaterDensityKgPerM3 = math.max(1f, value);
                    break;
                case TuningField.VisualWobble:
                    dto.VisualWobbleScalar = math.max(0f, value);
                    break;
                case TuningField.AcousticGain:
                    dto.AcousticMuffleGain = math.max(0f, value);
                    break;
            }
            tuning[0] = dto;
        }

        private enum TuningField : byte
        {
            TransferRate,
            MaxTransfer,
            IngressCap,
            Discharge,
            WaterDensity,
            VisualWobble,
            AcousticGain
        }
    }
}
#endif
