using System.Globalization;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.ProceduralWreckage.Editor
{
    public sealed class ProceduralWreckageTunerWindow : EditorWindow
    {
        private static ProceduralWreckageVaultHandles _handles;
        private static bool _hasHandles;
        private Slider _qualitySlider;
        private Slider _shearSlider;
        private Slider _debrisRadiusSlider;
        private Slider _visibilityMaxSlider;
        private IntegerField _backtrackLimitField;
        private IntegerField _maxNodesField;
        private IntegerField _maxDebrisField;
        private Label _telemetryLabel;
        private double _nextCsvPollTime;

        [MenuItem("HECTON-8/Procedural Wreckage/Procedural Wreckage Tuner")]
        public static void Open()
        {
            GetWindow<ProceduralWreckageTunerWindow>("Procedural Wreckage Tuner");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            Button resolveButton = new Button(ResolveVault) { text = "Resolve Vault" };
            Button loadBinaryButton = new Button(LoadBinary) { text = "Load H8BIN Rules" };
            Button loadCsvButton = new Button(LoadCsv) { text = "Load CSV Rules" };
            Button dumpButton = new Button(DumpBlackBox) { text = "Dump Black Box" };
            rootVisualElement.Add(resolveButton);
            rootVisualElement.Add(loadBinaryButton);
            rootVisualElement.Add(loadCsvButton);
            rootVisualElement.Add(dumpButton);

            _qualitySlider = new Slider("Global Quality Weight", 0f, 1f);
            _shearSlider = new Slider("Shear Damage Severity", 0f, 1f);
            _debrisRadiusSlider = new Slider("Debris Scatter Radius", 1f, 256f);
            _visibilityMaxSlider = new Slider("Visibility Distance Max", 32f, 1000f);
            _backtrackLimitField = new IntegerField("WFC Backtrack Limit");
            _maxNodesField = new IntegerField("Max Structural Nodes");
            _maxDebrisField = new IntegerField("Max Debris Nodes");

            _qualitySlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _shearSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _debrisRadiusSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _visibilityMaxSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _backtrackLimitField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _maxNodesField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _maxDebrisField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_shearSlider);
            rootVisualElement.Add(_debrisRadiusSlider);
            rootVisualElement.Add(_visibilityMaxSlider);
            rootVisualElement.Add(_backtrackLimitField);
            rootVisualElement.Add(_maxNodesField);
            rootVisualElement.Add(_maxDebrisField);

            _telemetryLabel = new Label("Vault not resolved.");
            _telemetryLabel.style.marginTop = 8;
            rootVisualElement.Add(_telemetryLabel);
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            ResolveVault();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        private void EditorTick()
        {
            if (!_hasHandles)
                return;

            if (EditorApplication.timeSinceStartup >= _nextCsvPollTime)
            {
                _nextCsvPollTime = EditorApplication.timeSinceStartup + 1.0;
                IDataVault vault = GlobalRegistry.DataVault;
                if (vault != null)
                    ProceduralWreckageVault.TryPollCsvRules(vault, ref _handles, ProjectRoot());
            }

            UpdateTelemetryReadout();
        }

        private void ResolveVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                if (_telemetryLabel != null)
                    _telemetryLabel.text = "GlobalDataVault is not registered.";
                return;
            }

            _hasHandles = ProceduralWreckageVault.TryEnsure(vault, out _handles);
            if (_hasHandles && ProceduralWreckageVault.TryGetTuning(vault, ref _handles, out WreckageTuningDTO tuning))
                SetFieldsWithoutNotify(tuning);

            UpdateTelemetryReadout();
        }

        private void LoadCsv()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            ProceduralWreckageVault.TryLoadCsvRules(vault, ref _handles, ProjectRoot());
            UpdateTelemetryReadout();
        }

        private void LoadBinary()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            ProceduralWreckageVault.TryLoadBinaryRules(vault, ref _handles, ProjectRoot());
            UpdateTelemetryReadout();
        }

        private void DumpBlackBox()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            if (ProceduralWreckageVault.TryResolveViews(vault, ref _handles, out ProceduralWreckageVaultBuffers buffers))
                ProceduralWreckageVault.TryDumpBlackBox(in buffers, ProjectRoot(), ProceduralWreckageConstants.FaultOpenHull);
        }

        private void ApplyTuningFromFields()
        {
            if (!_hasHandles)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !ProceduralWreckageVault.TryGetTuning(vault, ref _handles, out WreckageTuningDTO tuning))
                return;

            tuning.GlobalQualityWeight = _qualitySlider.value;
            tuning.ShearSeverity = _shearSlider.value;
            tuning.DebrisScatterRadius = _debrisRadiusSlider.value;
            tuning.VisibilityDistanceMax = _visibilityMaxSlider.value;
            tuning.BacktrackLimit = (uint)math.max(1, _backtrackLimitField.value);
            tuning.MaxNodes = math.clamp(_maxNodesField.value, 1, ProceduralWreckageConstants.MaxWreckNodes);
            tuning.MaxDebris = math.clamp(_maxDebrisField.value, 0, ProceduralWreckageConstants.MaxDebrisNodes);
            tuning.Version++;
            ProceduralWreckageVault.TrySetTuning(vault, ref _handles, in tuning);
        }

        private void SetFieldsWithoutNotify(in WreckageTuningDTO tuning)
        {
            _qualitySlider?.SetValueWithoutNotify(math.saturate(tuning.GlobalQualityWeight));
            _shearSlider?.SetValueWithoutNotify(math.saturate(tuning.ShearSeverity));
            _debrisRadiusSlider?.SetValueWithoutNotify(math.max(1f, tuning.DebrisScatterRadius));
            _visibilityMaxSlider?.SetValueWithoutNotify(math.max(32f, tuning.VisibilityDistanceMax));
            _backtrackLimitField?.SetValueWithoutNotify((int)math.max(1u, tuning.BacktrackLimit));
            _maxNodesField?.SetValueWithoutNotify(math.clamp(tuning.MaxNodes, 1, ProceduralWreckageConstants.MaxWreckNodes));
            _maxDebrisField?.SetValueWithoutNotify(math.clamp(tuning.MaxDebris, 0, ProceduralWreckageConstants.MaxDebrisNodes));
        }

        private void UpdateTelemetryReadout()
        {
            if (_telemetryLabel == null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles ||
                !ProceduralWreckageVault.TryResolveViews(vault, ref _handles, out ProceduralWreckageVaultBuffers buffers) ||
                !buffers.TelemetryRing.IsCreated ||
                !buffers.Counters.IsCreated ||
                buffers.TelemetryRing.Length <= 0)
            {
                _telemetryLabel.text = "Procedural Wreckage vault buffers are not ready.";
                return;
            }

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0
                ? math.clamp(buffers.TelemetryCursor[0] - 1, 0, buffers.TelemetryRing.Length - 1)
                : 0;
            WreckageGenerationTelemetryEntry telemetry = buffers.TelemetryRing[cursor];
            WreckagePaddedCounterDTO counters = buffers.Counters[0];
            _telemetryLabel.text =
                "Sector 0x" + telemetry.SectorHash.ToString("X8", CultureInfo.InvariantCulture) +
                " | Modules " + telemetry.CollapsedModules +
                " | Render " + counters.RenderMatrixCount +
                " | Debris " + counters.DebrisCount +
                " | Loot Requests " + counters.LootCount +
                " | Rules " + counters.ActiveRuleCount +
                " | H8BIN " + counters.BinaryRuleCount +
                " | CSV " + counters.CsvRuleCount +
                " | Backtracks " + telemetry.BacktrackIterations +
                " | Est. Burst ms " + telemetry.EstimatedComputeMs.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }
    }
}
