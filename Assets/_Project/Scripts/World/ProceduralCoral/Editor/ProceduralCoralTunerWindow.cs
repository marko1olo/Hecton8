using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.ProceduralCoral.Editor
{
    public sealed class ProceduralCoralTunerWindow : EditorWindow
    {
        private static ProceduralCoralVaultHandles _handles;
        private static bool _hasHandles;
        private Slider _qualitySlider;
        private Slider _angleSlider;
        private Slider _varianceSlider;
        private Slider _avoidanceSlider;
        private Slider _swaySlider;
        private FloatField _stepField;
        private FloatField _radiusField;
        private IntegerField _maxDepthField;
        private IntegerField _maxBranchesField;
        private IntegerField _maxInstructionsField;
        private Label _telemetryLabel;
        private readonly StringBuilder _telemetryBuilder = new StringBuilder(256);
        private uint _lastTelemetryHash;
        private double _nextCsvPollTime;
        private string _projectRoot;

        [MenuItem("Hecton8/Procedural Coral/Procedural Coral Tuner")]
        public static void Open()
        {
            GetWindow<ProceduralCoralTunerWindow>("Procedural Coral Tuner");
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
            _angleSlider = new Slider("Branch Angle", 0.05f, 1.35f);
            _varianceSlider = new Slider("Angle Variance", 0f, 1f);
            _avoidanceSlider = new Slider("SDF Avoidance Weight", 0f, 1f);
            _swaySlider = new Slider("Shader Sway Amplitude", 0f, 1f);
            _stepField = new FloatField("Base Step Meters");
            _radiusField = new FloatField("Base Radius Meters");
            _maxDepthField = new IntegerField("Max Recursion Depth");
            _maxBranchesField = new IntegerField("Max Branches");
            _maxInstructionsField = new IntegerField("Max Instructions");

            _qualitySlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _angleSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _varianceSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _avoidanceSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _swaySlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _stepField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _radiusField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _maxDepthField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _maxBranchesField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _maxInstructionsField.RegisterValueChangedCallback(_ => ApplyTuningFromFields());

            rootVisualElement.Add(_qualitySlider);
            rootVisualElement.Add(_angleSlider);
            rootVisualElement.Add(_varianceSlider);
            rootVisualElement.Add(_avoidanceSlider);
            rootVisualElement.Add(_swaySlider);
            rootVisualElement.Add(_stepField);
            rootVisualElement.Add(_radiusField);
            rootVisualElement.Add(_maxDepthField);
            rootVisualElement.Add(_maxBranchesField);
            rootVisualElement.Add(_maxInstructionsField);

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
                    ProceduralCoralVault.TryPollCsvRules(vault, ref _handles, ProjectRoot());
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

            _hasHandles = ProceduralCoralVault.TryEnsure(vault, out _handles);
            if (_hasHandles && ProceduralCoralVault.TryGetTuning(vault, ref _handles, out CoralTuningDTO tuning))
                SetFieldsWithoutNotify(tuning);

            UpdateTelemetryReadout();
        }

        private void LoadCsv()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            ProceduralCoralVault.TryLoadCsvRules(vault, ref _handles, ProjectRoot());
            UpdateTelemetryReadout();
        }

        private void LoadBinary()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            ProceduralCoralVault.TryLoadBinaryRules(vault, ref _handles, ProjectRoot());
            UpdateTelemetryReadout();
        }

        private void DumpBlackBox()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles)
                return;

            if (ProceduralCoralVault.TryResolveViews(vault, ref _handles, out ProceduralCoralVaultBuffers buffers))
                ProceduralCoralVault.TryDumpBlackBox(in buffers, ProjectRoot(), ProceduralCoralConstants.FaultRulePayload);
        }

        private void ApplyTuningFromFields()
        {
            if (!_hasHandles)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !ProceduralCoralVault.TryGetTuning(vault, ref _handles, out CoralTuningDTO tuning))
                return;

            tuning.GlobalQualityWeight = _qualitySlider.value;
            tuning.BranchAngleRadians = _angleSlider.value;
            tuning.AngleVarianceRadians = _varianceSlider.value;
            tuning.SdfAvoidanceWeight = _avoidanceSlider.value;
            tuning.CurrentSwayAmplitude = _swaySlider.value;
            tuning.BaseStepMeters = SafePositive(_stepField.value, 1.6f, ProceduralCoralConstants.Epsilon);
            tuning.BaseRadiusMeters = SafePositive(_radiusField.value, 0.32f, ProceduralCoralConstants.Epsilon);
            tuning.MaxDepth = math.clamp(_maxDepthField.value, 1, 12);
            tuning.MaxBranches = math.clamp(_maxBranchesField.value, 1, ProceduralCoralConstants.MaxBranches);
            tuning.MaxInstructions = math.clamp(_maxInstructionsField.value, 1, ProceduralCoralConstants.MaxInstructions);
            tuning.Version++;
            ProceduralCoralVault.TrySetTuning(vault, ref _handles, in tuning);
        }

        private void SetFieldsWithoutNotify(in CoralTuningDTO tuning)
        {
            _qualitySlider?.SetValueWithoutNotify(SafeSaturate(tuning.GlobalQualityWeight, 0.5f));
            _angleSlider?.SetValueWithoutNotify(math.clamp(math.isfinite(tuning.BranchAngleRadians) ? tuning.BranchAngleRadians : 0.52f, 0.05f, 1.35f));
            _varianceSlider?.SetValueWithoutNotify(SafeSaturate(tuning.AngleVarianceRadians, 0.18f));
            _avoidanceSlider?.SetValueWithoutNotify(SafeSaturate(tuning.SdfAvoidanceWeight, 0.55f));
            _swaySlider?.SetValueWithoutNotify(SafeSaturate(tuning.CurrentSwayAmplitude, 0.32f));
            _stepField?.SetValueWithoutNotify(SafePositive(tuning.BaseStepMeters, 1.6f, ProceduralCoralConstants.Epsilon));
            _radiusField?.SetValueWithoutNotify(SafePositive(tuning.BaseRadiusMeters, 0.32f, ProceduralCoralConstants.Epsilon));
            _maxDepthField?.SetValueWithoutNotify(math.clamp(tuning.MaxDepth, 1, 12));
            _maxBranchesField?.SetValueWithoutNotify(math.clamp(tuning.MaxBranches, 1, ProceduralCoralConstants.MaxBranches));
            _maxInstructionsField?.SetValueWithoutNotify(math.clamp(tuning.MaxInstructions, 1, ProceduralCoralConstants.MaxInstructions));
        }

        private void UpdateTelemetryReadout()
        {
            if (_telemetryLabel == null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !_hasHandles ||
                !ProceduralCoralVault.TryResolveViews(vault, ref _handles, out ProceduralCoralVaultBuffers buffers) ||
                !buffers.TelemetryRing.IsCreated ||
                !buffers.Counters.IsCreated ||
                buffers.TelemetryRing.Length <= 0)
            {
                _telemetryLabel.text = "Procedural Coral vault buffers are not ready.";
                return;
            }

            int cursor = buffers.TelemetryCursor.IsCreated && buffers.TelemetryCursor.Length > 0
                ? math.clamp(buffers.TelemetryCursor[0] - 1, 0, buffers.TelemetryRing.Length - 1)
                : 0;
            CoralGenerationTelemetryEntry telemetry = buffers.TelemetryRing[cursor];
            CoralPaddedCounterDTO counters = buffers.Counters[0];
            uint telemetryHash = 2166136261u;
            telemetryHash = (telemetryHash ^ telemetry.SectorHash) * 16777619u;
            telemetryHash = (telemetryHash ^ (uint)counters.BranchCount) * 16777619u;
            telemetryHash = (telemetryHash ^ (uint)counters.RenderMatrixCount) * 16777619u;
            telemetryHash = (telemetryHash ^ counters.TipCount) * 16777619u;
            telemetryHash = (telemetryHash ^ (uint)counters.SyncPulseCount) * 16777619u;
            telemetryHash = (telemetryHash ^ (uint)counters.CollisionProxyCount) * 16777619u;
            telemetryHash = (telemetryHash ^ counters.ActiveRuleCount) * 16777619u;
            telemetryHash = (telemetryHash ^ counters.BinaryRuleCount) * 16777619u;
            telemetryHash = (telemetryHash ^ counters.CsvRuleCount) * 16777619u;
            telemetryHash = (telemetryHash ^ (uint)telemetry.DepthReached) * 16777619u;
            telemetryHash = (telemetryHash ^ math.asuint(telemetry.BurstComputeUs)) * 16777619u;
            if (telemetryHash == _lastTelemetryHash)
                return;

            _lastTelemetryHash = telemetryHash;
            _telemetryBuilder.Length = 0;
            _telemetryBuilder.Append("Sector 0x");
            AppendHex8(_telemetryBuilder, telemetry.SectorHash);
            _telemetryBuilder.Append(" | Branches ");
            _telemetryBuilder.Append(counters.BranchCount);
            _telemetryBuilder.Append(" | Render ");
            _telemetryBuilder.Append(counters.RenderMatrixCount);
            _telemetryBuilder.Append(" | Tips ");
            _telemetryBuilder.Append(counters.TipCount);
            _telemetryBuilder.Append(" | Pulses ");
            _telemetryBuilder.Append(counters.SyncPulseCount);
            _telemetryBuilder.Append(" | Proxies ");
            _telemetryBuilder.Append(counters.CollisionProxyCount);
            _telemetryBuilder.Append(" | Rules ");
            _telemetryBuilder.Append(counters.ActiveRuleCount);
            _telemetryBuilder.Append(" | H8BIN ");
            _telemetryBuilder.Append(counters.BinaryRuleCount);
            _telemetryBuilder.Append(" | CSV ");
            _telemetryBuilder.Append(counters.CsvRuleCount);
            _telemetryBuilder.Append(" | Depth ");
            _telemetryBuilder.Append(telemetry.DepthReached);
            _telemetryBuilder.Append(" | Burst us ");
            AppendFixed2(_telemetryBuilder, telemetry.BurstComputeUs);
            _telemetryLabel.text = _telemetryBuilder.ToString();
        }

        private string ProjectRoot()
        {
            if (string.IsNullOrEmpty(_projectRoot))
                _projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);

            return _projectRoot;
        }

        private static float SafePositive(float value, float fallback, float minimum)
        {
            float selected = math.isfinite(value) && value > 0f ? value : fallback;
            return math.isfinite(selected) ? math.max(selected, minimum) : minimum;
        }

        private static float SafeSaturate(float value, float fallback)
        {
            float selected = math.isfinite(value) ? value : fallback;
            return math.isfinite(selected) ? math.saturate(selected) : 0f;
        }

        private static void AppendHex8(StringBuilder builder, uint value)
        {
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0xFu);
                builder.Append((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
            }
        }

        private static void AppendFixed2(StringBuilder builder, float value)
        {
            if (!math.isfinite(value))
            {
                builder.Append("NaN");
                return;
            }

            if (value < 0f)
            {
                builder.Append('-');
                value = -value;
            }

            int scaled = (int)math.round(value * 100f);
            int whole = scaled / 100;
            int fraction = scaled - (whole * 100);
            builder.Append(whole);
            builder.Append('.');
            if (fraction < 10)
                builder.Append('0');
            builder.Append(fraction);
        }
    }
}
