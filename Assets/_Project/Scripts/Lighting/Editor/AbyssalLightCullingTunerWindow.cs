using Hecton8.Lighting;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Lighting.Editor
{
    public sealed class AbyssalLightCullingTunerWindow : EditorWindow
    {
        private DynamicPointLightCullingDirector _target;
        private ObjectField _targetField;
        private Toggle _initializedField;
        private IntegerField _sourceCountField;
        private IntegerField _profileCountField;
        private IntegerField _sourceRevisionField;
        private IntegerField _sourceManifestFlagsField;
        private IntegerField _frameField;
        private IntegerField _evaluatedField;
        private IntegerField _visibleField;
        private IntegerField _culledField;
        private IntegerField _submittedField;
        private IntegerField _maxActiveField;
        private FloatField _qualityField;
        private FloatField _thermalField;
        private FloatField _averageIntensityField;
        private IntegerField _stateSizeField;
        private IntegerField _sourceSizeField;
        private IntegerField _gpuSizeField;
        private IntegerField _telemetrySizeField;

        [MenuItem("HECTON-8/Lighting/Abyssal Light Culling Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalLightCullingTunerWindow>("Abyssal Light Culling Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;

            _target = ResolveTargetCold();
            _targetField = new ObjectField("Runtime")
            {
                objectType = typeof(DynamicPointLightCullingDirector),
                allowSceneObjects = true,
                value = _target
            };
            _targetField.RegisterValueChangedCallback(evt =>
            {
                _target = evt.newValue as DynamicPointLightCullingDirector;
                RefreshStatus();
            });
            root.Add(_targetField);

            VisualElement runtimeGroup = new VisualElement();
            runtimeGroup.style.marginTop = 6;
            _initializedField = DisabledToggle("Initialized");
            _sourceCountField = DisabledInteger("Sources");
            _profileCountField = DisabledInteger("Profiles");
            _sourceRevisionField = DisabledInteger("Source Revision");
            _sourceManifestFlagsField = DisabledInteger("Source Flags");
            runtimeGroup.Add(_initializedField);
            runtimeGroup.Add(_sourceCountField);
            runtimeGroup.Add(_profileCountField);
            runtimeGroup.Add(_sourceRevisionField);
            runtimeGroup.Add(_sourceManifestFlagsField);
            root.Add(runtimeGroup);

            VisualElement telemetryGroup = new VisualElement();
            telemetryGroup.style.marginTop = 6;
            _frameField = DisabledInteger("Frame");
            _evaluatedField = DisabledInteger("Evaluated");
            _visibleField = DisabledInteger("Visible");
            _culledField = DisabledInteger("Culled");
            _submittedField = DisabledInteger("Submitted");
            _maxActiveField = DisabledInteger("Max Active");
            _qualityField = DisabledFloat("Quality");
            _thermalField = DisabledFloat("Thermal");
            _averageIntensityField = DisabledFloat("Avg Intensity");
            telemetryGroup.Add(_frameField);
            telemetryGroup.Add(_evaluatedField);
            telemetryGroup.Add(_visibleField);
            telemetryGroup.Add(_culledField);
            telemetryGroup.Add(_submittedField);
            telemetryGroup.Add(_maxActiveField);
            telemetryGroup.Add(_qualityField);
            telemetryGroup.Add(_thermalField);
            telemetryGroup.Add(_averageIntensityField);
            root.Add(telemetryGroup);

            VisualElement layoutGroup = new VisualElement();
            layoutGroup.style.marginTop = 6;
            _stateSizeField = DisabledInteger("LightCullStateDTO");
            _sourceSizeField = DisabledInteger("SourceDTO");
            _gpuSizeField = DisabledInteger("GpuDTO");
            _telemetrySizeField = DisabledInteger("TelemetryDTO");
            _stateSizeField.SetValueWithoutNotify(UnsafeUtility.SizeOf<LightCullStateDTO>());
            _sourceSizeField.SetValueWithoutNotify(UnsafeUtility.SizeOf<DynamicPointLightSourceDTO>());
            _gpuSizeField.SetValueWithoutNotify(UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>());
            _telemetrySizeField.SetValueWithoutNotify(UnsafeUtility.SizeOf<DynamicPointLightCullingTelemetryEntry>());
            layoutGroup.Add(_stateSizeField);
            layoutGroup.Add(_sourceSizeField);
            layoutGroup.Add(_gpuSizeField);
            layoutGroup.Add(_telemetrySizeField);
            root.Add(layoutGroup);

            Slider quality = new Slider("Quality Override", -1f, 1f) { value = -1f };
            quality.RegisterValueChangedCallback(evt => _target?.SetEditorForceQuality(evt.newValue));
            root.Add(quality);

            Slider fade = new Slider("Base Fade Distance", 4f, 96f) { value = 38f };
            fade.RegisterValueChangedCallback(evt => _target?.SetEditorBaseFadeDistance(evt.newValue));
            root.Add(fade);

            Slider importance = new Slider("Importance Weight", 0.05f, 8f) { value = 1f };
            importance.RegisterValueChangedCallback(evt => _target?.SetEditorImportanceWeight(evt.newValue));
            root.Add(importance);

            Slider sdf = new Slider("SDF Occlusion Threshold", -4f, 4f) { value = -0.05f };
            sdf.RegisterValueChangedCallback(evt => _target?.SetEditorSdfOcclusionThreshold(evt.newValue));
            root.Add(sdf);

            Toolbar toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(FindRuntimeTarget) { text = "Find Runtime" });
            toolbar.Add(new ToolbarButton(() => _target?.GenerateMockLightCullingData()) { text = "Generate 5000 Mock Lights" });
            toolbar.Add(new ToolbarButton(() => _target?.RequestCsvReload()) { text = "Reload Profiles CSV" });
            toolbar.Add(new ToolbarButton(() => _target?.DumpBlackBoxNow()) { text = "Dump Black Box" });
            root.Add(toolbar);

            RefreshStatus();
            root.schedule.Execute(RefreshStatus).Every(250);
        }

        private static IntegerField DisabledInteger(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            return field;
        }

        private static FloatField DisabledFloat(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            return field;
        }

        private static Toggle DisabledToggle(string label)
        {
            Toggle field = new Toggle(label);
            field.SetEnabled(false);
            return field;
        }

        private DynamicPointLightCullingDirector ResolveTargetCold()
        {
            return Object.FindFirstObjectByType<DynamicPointLightCullingDirector>(FindObjectsInactive.Include);
        }

        private void FindRuntimeTarget()
        {
            _target = ResolveTargetCold();
            if (_targetField != null)
                _targetField.SetValueWithoutNotify(_target);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_initializedField == null)
                return;

            if (_target == null)
            {
                _initializedField.SetValueWithoutNotify(false);
                _sourceCountField.SetValueWithoutNotify(0);
                _profileCountField.SetValueWithoutNotify(0);
                _sourceRevisionField.SetValueWithoutNotify(0);
                _sourceManifestFlagsField.SetValueWithoutNotify(0);
                ClearTelemetry();
                return;
            }

            _initializedField.SetValueWithoutNotify(_target.IsInitialized);
            _sourceCountField.SetValueWithoutNotify(_target.ActiveSourceCount);
            _profileCountField.SetValueWithoutNotify(_target.ProfileRuleCount);
            if (_target.TryGetSourceManifestCopy(out DynamicPointLightSourceManifestDTO manifest))
            {
                _sourceRevisionField.SetValueWithoutNotify(unchecked((int)manifest.SourceRevision));
                _sourceManifestFlagsField.SetValueWithoutNotify(unchecked((int)manifest.Flags));
            }
            else
            {
                _sourceRevisionField.SetValueWithoutNotify(0);
                _sourceManifestFlagsField.SetValueWithoutNotify(0);
            }

            if (_target.TryGetCountersCopy(out DynamicPointLightRuntimeCountersDTO counters))
            {
                _frameField.SetValueWithoutNotify(unchecked((int)counters.Frame));
                _evaluatedField.SetValueWithoutNotify(counters.TotalLights);
                _visibleField.SetValueWithoutNotify(counters.VisibleLights);
                _culledField.SetValueWithoutNotify(counters.CulledLights);
                _submittedField.SetValueWithoutNotify(counters.SubmittedLights);
                _maxActiveField.SetValueWithoutNotify(counters.MaxActiveLights);
                _qualityField.SetValueWithoutNotify(counters.QualityWeight);
                _thermalField.SetValueWithoutNotify(counters.ThermalPressure01);
                _averageIntensityField.SetValueWithoutNotify(counters.AverageSubmittedIntensity);
                return;
            }

            ClearTelemetry();
        }

        private void ClearTelemetry()
        {
            _frameField.SetValueWithoutNotify(0);
            _evaluatedField.SetValueWithoutNotify(0);
            _visibleField.SetValueWithoutNotify(0);
            _culledField.SetValueWithoutNotify(0);
            _submittedField.SetValueWithoutNotify(0);
            _maxActiveField.SetValueWithoutNotify(0);
            _qualityField.SetValueWithoutNotify(0f);
            _thermalField.SetValueWithoutNotify(0f);
            _averageIntensityField.SetValueWithoutNotify(0f);
        }
    }
}
