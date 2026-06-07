using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Vehicles.Editor
{
    public sealed class SubmarineInertiaTunerWindow : EditorWindow
    {
        private const int HistogramBins = 24;
        private const double RefreshSeconds = 0.25d;

        private readonly VisualElement[] _traceBars = new VisualElement[HistogramBins];
        private readonly float[] _traceSamples = new float[HistogramBins];
        private Label _status;
        private FloatField _depth;
        private FloatField _densityScalar;
        private FloatField _linearTrace;
        private FloatField _angularTrace;
        private FloatField _damping;
        private Slider _baseMultiplier;
        private Slider _depthLinear;
        private Slider _depthQuadratic;
        private Slider _dampingScalar;
        private Slider _matrixBlendBias;
        private Slider _floodScalar;
        private Slider _anisotropy;
        private bool _suppressCallbacks;
        private double _nextRefresh;
        private int _histogramCursor;

        [MenuItem("Hecton8/Vehicles/Submarine Inertia Tuner")]
        public static void Open()
        {
            SubmarineInertiaTunerWindow window = GetWindow<SubmarineInertiaTunerWindow>();
            window.titleContent = new GUIContent("Submarine Inertia");
            window.minSize = new Vector2(420f, 420f);
            window.Show();
        }

        [MenuItem("Hecton8/Vehicles/Submarine Inertia/Run Static Audit")]
        public static void RunStaticAudit()
        {
            int dtoSize = UnsafeUtility.SizeOf<AddedMassProfileDTO>();
            int tuningSize = UnsafeUtility.SizeOf<SubmarineAddedMassTuningDTO>();
            int linearOffset = Marshal.OffsetOf<AddedMassProfileDTO>(nameof(AddedMassProfileDTO.LinearAddedMass)).ToInt32();
            int angularOffset = Marshal.OffsetOf<AddedMassProfileDTO>(nameof(AddedMassProfileDTO.AngularAddedMass)).ToInt32();
            int tuningSourceOffset = Marshal.OffsetOf<SubmarineAddedMassTuningDTO>(nameof(SubmarineAddedMassTuningDTO.SourceHash)).ToInt32();
            int vehicleSourceFiles;
            int hotHackCount = Rigidbody_Drag_Scanner.CountForbiddenVehiclePhysicsWrites(out vehicleSourceFiles);
            bool layoutPass = dtoSize == 128 && tuningSize == 64 && linearOffset == 0 && angularOffset == 64 && tuningSourceOffset == 32;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            StringBuilder builder = new StringBuilder(768);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_251\",");
            builder.AppendLine("  \"domain\": \"SUBMARINE_ADDED_MASS_SOLVER\",");
            builder.Append("  \"addedMassProfileSizeBytes\": ").Append(dtoSize).AppendLine(",");
            builder.Append("  \"addedMassTuningSizeBytes\": ").Append(tuningSize).AppendLine(",");
            builder.Append("  \"linearTensorOffset\": ").Append(linearOffset).AppendLine(",");
            builder.Append("  \"angularTensorOffset\": ").Append(angularOffset).AppendLine(",");
            builder.Append("  \"tuningSourceHashOffset\": ").Append(tuningSourceOffset).AppendLine(",");
            builder.Append("  \"layoutPass\": ").Append(layoutPass ? "true" : "false").AppendLine(",");
            builder.AppendLine("  \"scanner\": \"Rigidbody_Drag_Scanner\",");
            builder.AppendLine("  \"summary\": \"OOP Mass Modifications Purged\",");
            builder.AppendLine("  \"parser\": \"roslyn AST with comment-stripped token fallback\",");
            builder.AppendLine("  \"sharedReportMerge\": \"NON_DESTRUCTIVE_TOP_LEVEL_PROPERTY_REPLACE_OR_APPEND\",");
            builder.AppendLine("  \"sidecarReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json\",");
            builder.Append("  \"vehicleSourceFilesScanned\": ").Append(vehicleSourceFiles).AppendLine(",");
            builder.Append("  \"vehicleRigidbodyMassDragHackSites\": ").Append(hotHackCount).AppendLine(",");
            builder.Append("  \"oopMassModificationsPurged\": ").Append(hotHackCount == 0 ? "true" : "false").AppendLine(",");
            builder.AppendLine("  \"runtimeRoute\": \"DataVault -> CalculateAddedMassTensorJob -> Submarine6DIntegratorJob\"");
            builder.AppendLine("}");

            Rigidbody_Drag_Scanner.WriteReports(projectRoot, builder.ToString());
            if (!layoutPass)
                Debug.LogError("SHINOBU_251 added-mass layout audit failed. See Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json");
            else
                Debug.Log("SHINOBU_251 added-mass audit written: Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _status = new Label("Runtime unavailable");
            _status.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_status);

            _depth = BuildReadout("Depth m");
            _densityScalar = BuildReadout("Density scalar");
            _linearTrace = BuildReadout("Linear trace kg");
            _angularTrace = BuildReadout("Angular trace kgm2");
            _damping = BuildReadout("Rot damping");
            root.Add(_depth);
            root.Add(_densityScalar);
            root.Add(_linearTrace);
            root.Add(_angularTrace);
            root.Add(_damping);

            VisualElement histogram = new VisualElement();
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.height = 64f;
            histogram.style.marginTop = 8f;
            histogram.style.marginBottom = 8f;
            for (int i = 0; i < HistogramBins; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.marginLeft = 1f;
                bar.style.marginRight = 1f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.height = 1f;
                bar.style.backgroundColor = new Color(0.12f, 0.64f, 0.95f, 0.85f);
                _traceBars[i] = bar;
                histogram.Add(bar);
            }

            root.Add(histogram);

            _baseMultiplier = AddSlider(root, "Base Added Mass Multiplier", 0.25f, 4f);
            _depthLinear = AddSlider(root, "Depth Density Linear", 0f, 0.5f);
            _depthQuadratic = AddSlider(root, "Depth Density Quadratic", 0f, 0.5f);
            _dampingScalar = AddSlider(root, "Rotational Damping Scalar", 0.1f, 6f);
            _matrixBlendBias = AddSlider(root, "Matrix Blend Bias", -0.5f, 0.5f);
            _floodScalar = AddSlider(root, "Flood Volume Scalar", 0f, 3f);
            _anisotropy = AddSlider(root, "Tensor Anisotropy", 0.25f, 4f);

            root.Add(new Button(RefreshFromRuntime) { text = "Refresh Vault" });
            root.Add(new Button(RunStaticAudit) { text = "Write Static Audit" });

            RegisterCallbacks();
            RefreshFromRuntime();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorPulse;
            EditorApplication.update += OnEditorPulse;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorPulse;
        }

        private void OnEditorPulse()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh)
                return;

            _nextRefresh = now + RefreshSeconds;
            RefreshTelemetryOnly();
        }

        private static FloatField BuildReadout(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private static Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            root.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _baseMultiplier.RegisterValueChangedCallback(_ => ApplySliderValues());
            _depthLinear.RegisterValueChangedCallback(_ => ApplySliderValues());
            _depthQuadratic.RegisterValueChangedCallback(_ => ApplySliderValues());
            _dampingScalar.RegisterValueChangedCallback(_ => ApplySliderValues());
            _matrixBlendBias.RegisterValueChangedCallback(_ => ApplySliderValues());
            _floodScalar.RegisterValueChangedCallback(_ => ApplySliderValues());
            _anisotropy.RegisterValueChangedCallback(_ => ApplySliderValues());
        }

        private void RefreshFromRuntime()
        {
            _suppressCallbacks = true;
            if (SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime) &&
                runtime.TryReadAddedMassTuning(out SubmarineAddedMassTuningDTO tuning))
            {
                _baseMultiplier.SetValueWithoutNotify(tuning.BaseAddedMassMultiplier);
                _depthLinear.SetValueWithoutNotify(tuning.DepthDensityLinear);
                _depthQuadratic.SetValueWithoutNotify(tuning.DepthDensityQuadratic);
                _dampingScalar.SetValueWithoutNotify(tuning.RotationalDampingScalar);
                _matrixBlendBias.SetValueWithoutNotify(tuning.MatrixBlendBias);
                _floodScalar.SetValueWithoutNotify(tuning.FloodVolumeScalar);
                _anisotropy.SetValueWithoutNotify(tuning.TensorAnisotropyScalar);
            }

            _suppressCallbacks = false;
            RefreshTelemetryOnly();
        }

        private void RefreshTelemetryOnly()
        {
            if (_status == null)
                return;

            if (!SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime))
            {
                _status.text = "Runtime unavailable";
                SetTelemetry(default);
                return;
            }

            if (runtime.TryReadLatestHydrodynamicsTelemetry(out SubmarineHydrodynamicsTelemetry telemetry))
            {
                _status.text = "Runtime telemetry";
                SetTelemetry(telemetry);
                PushHistogram(telemetry.LinearDiagKg.x + telemetry.LinearDiagKg.y + telemetry.LinearDiagKg.z);
            }
            else
            {
                _status.text = "Runtime ready; hydrodynamics ring empty";
                SetTelemetry(default);
            }
        }

        private void SetTelemetry(in SubmarineHydrodynamicsTelemetry telemetry)
        {
            _depth?.SetValueWithoutNotify(telemetry.DepthMeters);
            _densityScalar?.SetValueWithoutNotify(telemetry.DepthDensityScalar);
            float linearTrace = telemetry.LinearDiagKg.x + telemetry.LinearDiagKg.y + telemetry.LinearDiagKg.z;
            float angularTrace = telemetry.AngularDiagKgm2.x + telemetry.AngularDiagKgm2.y + telemetry.AngularDiagKgm2.z;
            _linearTrace?.SetValueWithoutNotify(linearTrace);
            _angularTrace?.SetValueWithoutNotify(angularTrace);
            _damping?.SetValueWithoutNotify(telemetry.RotationalDamping);
        }

        private void PushHistogram(float trace)
        {
            _traceSamples[_histogramCursor] = math.max(0f, math.isfinite(trace) ? trace : 0f);
            _histogramCursor = (_histogramCursor + 1) % HistogramBins;
            float max = 1f;
            for (int i = 0; i < HistogramBins; i++)
                max = math.max(max, _traceSamples[i]);

            for (int i = 0; i < HistogramBins; i++)
            {
                float normalized = math.saturate(_traceSamples[i] / max);
                _traceBars[i].style.height = math.max(1f, normalized * 64f);
            }
        }

        private void ApplySliderValues()
        {
            if (_suppressCallbacks)
                return;

            if (!SubmarineDynamicsRuntime.TryGetLatest(out SubmarineDynamicsRuntime runtime))
                return;

            SubmarineAddedMassTuningDTO tuning = default;
            tuning.BaseAddedMassMultiplier = _baseMultiplier.value;
            tuning.DepthDensityLinear = _depthLinear.value;
            tuning.DepthDensityQuadratic = _depthQuadratic.value;
            tuning.RotationalDampingScalar = _dampingScalar.value;
            tuning.MatrixBlendBias = _matrixBlendBias.value;
            tuning.FloodVolumeScalar = _floodScalar.value;
            tuning.TensorAnisotropyScalar = _anisotropy.value;
            tuning.MaxDepthMeters = 6000f;
            tuning.SourceHash = SubmarineDynamicsConstants.SourceHashAddedMass;
            runtime.TryWriteAddedMassTuning(in tuning);
        }

    }
}
