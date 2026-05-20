#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Graphics.Culling.Editor
{
    public sealed class AbyssalShadowTunerWindow : EditorWindow
    {
        private const string WindowTitle = "Abyssal Shadow Tuner";

        private AbyssalShadowCullingRuntime _runtime;
        private ObjectField _runtimeField;
        private Slider _baseDistanceSlider;
        private Slider _fadeBandSlider;
        private Slider _darknessSlider;
        private TextField _csvPathField;
        private Toggle _gizmoToggle;
        private Label _evaluatedLabel;
        private Label _shadowCulledLabel;
        private Label _darknessLabel;
        private Label _pointLabel;
        private Label _timingLabel;
        private Label _statusLabel;

        [MenuItem("Hecton8/Rendering/Abyssal Shadow Tuner")]
        private static void Open()
        {
            GetWindow<AbyssalShadowTunerWindow>(WindowTitle);
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent(WindowTitle);
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _runtimeField = new ObjectField("Runtime")
            {
                objectType = typeof(AbyssalShadowCullingRuntime),
                allowSceneObjects = true
            };
            _runtimeField.RegisterValueChangedCallback(evt =>
            {
                _runtime = evt.newValue as AbyssalShadowCullingRuntime;
                PullSnapshot();
            });
            root.Add(_runtimeField);

            VisualElement row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            Button findButton = new Button(FindRuntime) { text = "Find" };
            Button runButton = new Button(RunMock) { text = "Run Mock 50K" };
            Button validateButton = new Button(ValidateLayout) { text = "Validate Layout" };
            row.Add(findButton);
            row.Add(runButton);
            row.Add(validateButton);
            root.Add(row);

            _baseDistanceSlider = new Slider("Base Shadow Distance", 20f, 300f) { value = AbyssalShadowCullingConstants.DefaultMaximumShadowDistanceMeters };
            _fadeBandSlider = new Slider("Dither Fade Band", 0.001f, 0.5f) { value = AbyssalShadowCullingConstants.DefaultDitherFadeBand01 };
            _darknessSlider = new Slider("Darkness Threshold", 0f, 1f) { value = AbyssalShadowCullingConstants.DefaultDarknessThreshold };
            _baseDistanceSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _fadeBandSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _darknessSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            root.Add(_baseDistanceSlider);
            root.Add(_fadeBandSlider);
            root.Add(_darknessSlider);

            _gizmoToggle = new Toggle("Live Frustum Gizmo");
            _gizmoToggle.RegisterValueChangedCallback(evt =>
            {
                AbyssalShadowCullingRuntime.SetActiveGizmo(evt.newValue);
                if (_runtime != null)
                    EditorUtility.SetDirty(_runtime);
            });
            root.Add(_gizmoToggle);

            VisualElement csvRow = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            _csvPathField = new TextField("shadow_culling_profiles.csv") { value = AbyssalShadowCullingRuntime.GetActiveProfileCsvPath() };
            _csvPathField.style.flexGrow = 1;
            Button ingestButton = new Button(LoadCsv) { text = "Ingest CSV" };
            csvRow.Add(_csvPathField);
            csvRow.Add(ingestButton);
            root.Add(csvRow);

            _evaluatedLabel = new Label("Evaluated: 0");
            _shadowCulledLabel = new Label("Shadow culled: 0");
            _darknessLabel = new Label("Darkness culled: 0");
            _pointLabel = new Label("Point-light culled: 0");
            _timingLabel = new Label("Timing: 0.000 ms / 0.0 us upload");
            _statusLabel = new Label("Status: idle");
            root.Add(_evaluatedLabel);
            root.Add(_shadowCulledLabel);
            root.Add(_darknessLabel);
            root.Add(_pointLabel);
            root.Add(_timingLabel);
            root.Add(_statusLabel);

            root.schedule.Execute(PollSnapshot).Every(250);
        }

        private void FindRuntime()
        {
            _runtime = FindAnyObjectByType<AbyssalShadowCullingRuntime>();
            _runtimeField.value = _runtime;
            _statusLabel.text = _runtime != null ? "Status: runtime selected" : "Status: runtime missing";
            PullSnapshot();
        }

        private void RunMock()
        {
            bool ok = _runtime != null ? _runtime.RunMockCullingOnce() : AbyssalShadowCullingRuntime.RunActiveMockOnce();
            _statusLabel.text = ok ? "Status: 50K mock pass executed" : "Status: no runtime/vault";
            PollSnapshot();
        }

        private void ValidateLayout()
        {
            bool ok = AbyssalShadowLayoutAudit.ValidateAllLayouts();
            _statusLabel.text = ok ? "Status: Abyssal shadow DTO layouts valid" : "Status: Abyssal shadow DTO layout invalid";
            if (!ok)
                throw new InvalidOperationException("Abyssal shadow DTO layout does not match SHINOBU_134 contracts.");
        }

        private void ApplyTuning()
        {
            float distance = _baseDistanceSlider.value;
            float fade = _fadeBandSlider.value;
            float darkness = _darknessSlider.value;
            if (_runtime != null)
                _runtime.ApplyTunerSettings(distance, fade, darkness);
            else
                AbyssalShadowCullingRuntime.ApplyActiveTunerSettings(distance, fade, darkness);
        }

        private void LoadCsv()
        {
            string path = _csvPathField.value;
            if (_runtime != null)
            {
                _runtime.SetProfileCsvPath(path);
                _statusLabel.text = _runtime.LoadProfileCsv() ? "Status: CSV applied" : "Status: CSV not applied";
            }
            else
            {
                AbyssalShadowCullingRuntime.SetActiveProfileCsvPath(path);
                _statusLabel.text = AbyssalShadowCullingRuntime.LoadActiveProfileCsv() ? "Status: CSV applied" : "Status: CSV not applied";
            }
        }

        private void PollSnapshot()
        {
            AbyssalShadowTunerSnapshot snapshot;
            bool ok = _runtime != null
                ? _runtime.TryGetTunerSnapshot(out snapshot)
                : AbyssalShadowCullingRuntime.TryGetActiveSnapshot(out snapshot);
            if (!ok)
                return;

            _evaluatedLabel.text = "Evaluated: " + snapshot.EvaluatedCount;
            _shadowCulledLabel.text = "Shadow culled: " + snapshot.ShadowCulledCount + " / dithered: " + snapshot.DitheredCount;
            _darknessLabel.text = "Darkness culled: " + snapshot.DarknessCulledCount;
            _pointLabel.text = "Point-light culled: " + snapshot.PointLightCulledCount;
            _timingLabel.text = "Timing: " + snapshot.LastBurstWallTimeMs.ToString("0.000") + " ms / " + snapshot.LastUploadMicroseconds.ToString("0.0") + " us upload";
        }

        private void PullSnapshot()
        {
            AbyssalShadowTunerSnapshot snapshot;
            if (_runtime == null || !_runtime.TryGetTunerSnapshot(out snapshot))
                return;

            _baseDistanceSlider.value = snapshot.BaseShadowDistanceMeters;
            _fadeBandSlider.value = snapshot.DitherFadeBand01;
            _darknessSlider.value = snapshot.DarknessThreshold;
            _csvPathField.value = _runtime.GetProfileCsvPath();
        }
    }

    public static class AbyssalShadowLayoutValidator
    {
        [MenuItem("Hecton8/Rendering/Abyssal Shadow/Validate DTO Layout")]
        private static void Validate()
        {
            if (!AbyssalShadowLayoutAudit.ValidateAllLayouts())
                throw new InvalidOperationException("Abyssal shadow DTO layouts must remain explicit: state=32B, instance/counters/telemetry/runtime=64B, HZB=16B, indirect=32B.");

            Debug.Log("Abyssal Shadow DTO layouts valid: state=32B, instance/counters/telemetry/runtime=64B, HZB=16B, indirect=32B.");
        }
    }

    public static class AbyssalShadowLayoutAudit
    {
        public static bool ValidateAllLayouts()
        {
            return ValidateShadowCullStateLayout() &&
                   ValidateShadowCullInstanceLayout() &&
                   ValidateShadowCullCountersLayout() &&
                   ValidateTelemetryLayout() &&
                   ValidateRuntimeStateLayout() &&
                   ValidateHzbTileLayout() &&
                   ValidateIndirectArgsLayout() &&
                   ValidateTunerSnapshotLayout() &&
                   ValidateProfileRuleLayout() &&
                   ValidateCsvParseResultLayout();
        }

        private static bool ValidateShadowCullStateLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullStateDTO>() == AbyssalShadowCullingConstants.ShadowCullStateStrideBytes &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO.InstanceHash)) == 0 &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO.DistanceSq)) == 4 &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO.CullFlags)) == 8 &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO.IlluminationScalar)) == 12 &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO._pad0)) == 16 &&
                   FieldOffset<ShadowCullStateDTO>(nameof(ShadowCullStateDTO._pad15)) == 31;
        }

        private static bool ValidateShadowCullInstanceLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullInstanceDTO>() == 64 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO.CenterAUP)) == 0 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO.Extents)) == 24 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO.BoundsRadius)) == 36 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO.InstanceHash)) == 40 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO.ProfileHash)) == 56 &&
                   FieldOffset<ShadowCullInstanceDTO>(nameof(ShadowCullInstanceDTO._pad0)) == 60;
        }

        private static bool ValidateShadowCullCountersLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullCountersDTO>() == 64 &&
                   FieldOffset<ShadowCullCountersDTO>(nameof(ShadowCullCountersDTO.EvaluatedCount)) == 0 &&
                   FieldOffset<ShadowCullCountersDTO>(nameof(ShadowCullCountersDTO.VisibleShadowCount)) == 40 &&
                   FieldOffset<ShadowCullCountersDTO>(nameof(ShadowCullCountersDTO.StateHash)) == 48 &&
                   FieldOffset<ShadowCullCountersDTO>(nameof(ShadowCullCountersDTO._pad1)) == 56;
        }

        private static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<CullingTelemetryEntry>() == 64 &&
                   FieldOffset<CullingTelemetryEntry>(nameof(CullingTelemetryEntry.Frame)) == 0 &&
                   FieldOffset<CullingTelemetryEntry>(nameof(CullingTelemetryEntry.BurstWallTimeMs)) == 32 &&
                   FieldOffset<CullingTelemetryEntry>(nameof(CullingTelemetryEntry.DitheredCount)) == 60;
        }

        private static bool ValidateRuntimeStateLayout()
        {
            return UnsafeUtility.SizeOf<AbyssalShadowRuntimeStateDTO>() == 64 &&
                   FieldOffset<AbyssalShadowRuntimeStateDTO>(nameof(AbyssalShadowRuntimeStateDTO.BaseShadowDistanceMeters)) == 0 &&
                   FieldOffset<AbyssalShadowRuntimeStateDTO>(nameof(AbyssalShadowRuntimeStateDTO.DirectionalLightDirection)) == 32 &&
                   FieldOffset<AbyssalShadowRuntimeStateDTO>(nameof(AbyssalShadowRuntimeStateDTO._pad0)) == 60;
        }

        private static bool ValidateHzbTileLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullHzbTileDTO>() == 16 &&
                   FieldOffset<ShadowCullHzbTileDTO>(nameof(ShadowCullHzbTileDTO.DepthMeters)) == 0 &&
                   FieldOffset<ShadowCullHzbTileDTO>(nameof(ShadowCullHzbTileDTO.OcclusionBiasMeters)) == 4 &&
                   FieldOffset<ShadowCullHzbTileDTO>(nameof(ShadowCullHzbTileDTO.TileHash)) == 8 &&
                   FieldOffset<ShadowCullHzbTileDTO>(nameof(ShadowCullHzbTileDTO.Flags)) == 12;
        }

        private static bool ValidateIndirectArgsLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullIndirectArgsDTO>() == 32 &&
                   FieldOffset<ShadowCullIndirectArgsDTO>(nameof(ShadowCullIndirectArgsDTO.VertexCountPerInstance)) == 0 &&
                   FieldOffset<ShadowCullIndirectArgsDTO>(nameof(ShadowCullIndirectArgsDTO.InstanceCount)) == 4 &&
                   FieldOffset<ShadowCullIndirectArgsDTO>(nameof(ShadowCullIndirectArgsDTO.StartIndex)) == 16 &&
                   FieldOffset<ShadowCullIndirectArgsDTO>(nameof(ShadowCullIndirectArgsDTO._pad1)) == 28;
        }

        private static bool ValidateTunerSnapshotLayout()
        {
            return UnsafeUtility.SizeOf<AbyssalShadowTunerSnapshot>() == 64 &&
                   FieldOffset<AbyssalShadowTunerSnapshot>(nameof(AbyssalShadowTunerSnapshot.EvaluatedCount)) == 0 &&
                   FieldOffset<AbyssalShadowTunerSnapshot>(nameof(AbyssalShadowTunerSnapshot.GlobalQualityWeight)) == 32 &&
                   FieldOffset<AbyssalShadowTunerSnapshot>(nameof(AbyssalShadowTunerSnapshot.LastUploadCount)) == 60;
        }

        private static bool ValidateProfileRuleLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullProfileRuleDTO>() == 32 &&
                   FieldOffset<ShadowCullProfileRuleDTO>(nameof(ShadowCullProfileRuleDTO.ProfileHash)) == 0 &&
                   FieldOffset<ShadowCullProfileRuleDTO>(nameof(ShadowCullProfileRuleDTO.PointLightBudget01)) == 20 &&
                   FieldOffset<ShadowCullProfileRuleDTO>(nameof(ShadowCullProfileRuleDTO._pad0)) == 28;
        }

        private static bool ValidateCsvParseResultLayout()
        {
            return UnsafeUtility.SizeOf<ShadowCullCsvParseResultDTO>() == 32 &&
                   FieldOffset<ShadowCullCsvParseResultDTO>(nameof(ShadowCullCsvParseResultDTO.ParsedRuleCount)) == 0 &&
                   FieldOffset<ShadowCullCsvParseResultDTO>(nameof(ShadowCullCsvParseResultDTO.LastFadeBandScale)) == 28;
        }

        private static int FieldOffset<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
#endif
