#if UNITY_EDITOR
using System.Globalization;
using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.Biomes.Editor
{
    public sealed class BiomeTransitionTunerWindow : EditorWindow
    {
        private Slider _radiusScaleSlider;
        private Slider _qualityOverrideSlider;
        private Slider _ditherStrengthSlider;
        private Slider _lowCadenceSlider;
        private Slider _ultraCadenceSlider;
        private Slider _scanScaleSlider;
        private Toggle _debugDrawToggle;
        private Toggle _mockTraversalToggle;
        private Label _readout;
        private double _nextReadoutTime;

        [MenuItem("HECTON-8/Biomes/Biome Transition Tuner")]
        public static void Open()
        {
            GetWindow<BiomeTransitionTunerWindow>("Biome Transition Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            Button validateButton = new Button(ValidateLayout) { text = "Validate Native Layout" };
            Button reloadButton = new Button(ReloadCsv) { text = "Reload CSV Rules" };
            Button dumpButton = new Button(DumpBlackBox) { text = "Dump Black Box" };
            root.Add(validateButton);
            root.Add(reloadButton);
            root.Add(dumpButton);

            _radiusScaleSlider = new Slider("Blending Radius Scale", 0.1f, 4f);
            _qualityOverrideSlider = new Slider("Hardware Quality Override", -1f, 1f);
            _ditherStrengthSlider = new Slider("Dither Strength", 0f, 2f);
            _lowCadenceSlider = new Slider("Low Cadence Hz", 1f, 15f);
            _ultraCadenceSlider = new Slider("Ultra Cadence Hz", 15f, 60f);
            _scanScaleSlider = new Slider("Center Scan Scale", 0.25f, 1f);
            _debugDrawToggle = new Toggle("Draw Gizmos");
            _mockTraversalToggle = new Toggle("Mock Traversal");

            _radiusScaleSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _qualityOverrideSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _ditherStrengthSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _lowCadenceSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _ultraCadenceSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _scanScaleSlider.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _debugDrawToggle.RegisterValueChangedCallback(_ => ApplyTuningFromFields());
            _mockTraversalToggle.RegisterValueChangedCallback(_ => ApplyTuningFromFields());

            root.Add(_radiusScaleSlider);
            root.Add(_qualityOverrideSlider);
            root.Add(_ditherStrengthSlider);
            root.Add(_lowCadenceSlider);
            root.Add(_ultraCadenceSlider);
            root.Add(_scanScaleSlider);
            root.Add(_debugDrawToggle);
            root.Add(_mockTraversalToggle);

            _readout = new Label("Vault not resolved.");
            _readout.style.marginTop = 8;
            root.Add(_readout);

            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
            PullTuning();
            UpdateReadout();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorTick;
        }

        private void EditorTick()
        {
            if (EditorApplication.timeSinceStartup < _nextReadoutTime)
                return;

            _nextReadoutTime = EditorApplication.timeSinceStartup + 0.25d;
            UpdateReadout();
        }

        private void PullTuning()
        {
            if (!BiomeTransitionManagerRuntime.TryReadTuning(out BiomeTransitionTuningDTO tuning))
            {
                tuning = new BiomeTransitionTuningDTO
                {
                    RadiusScale = 1f,
                HardwareQualityOverride = -1f,
                LowCadenceHz = 5f,
                UltraCadenceHz = 60f,
                DitherStrength = 1f,
                MaxCenterScanScale = 1f
            };
            }

            _radiusScaleSlider?.SetValueWithoutNotify(math.max(0.1f, tuning.RadiusScale));
            _qualityOverrideSlider?.SetValueWithoutNotify(math.clamp(tuning.HardwareQualityOverride, -1f, 1f));
            _ditherStrengthSlider?.SetValueWithoutNotify(math.max(0f, tuning.DitherStrength));
            _lowCadenceSlider?.SetValueWithoutNotify(math.clamp(tuning.LowCadenceHz, 1f, 15f));
            _ultraCadenceSlider?.SetValueWithoutNotify(math.clamp(tuning.UltraCadenceHz, 15f, 60f));
            _scanScaleSlider?.SetValueWithoutNotify(math.clamp(tuning.MaxCenterScanScale <= 0f ? 1f : tuning.MaxCenterScanScale, 0.25f, 1f));
            _debugDrawToggle?.SetValueWithoutNotify(tuning.DebugDrawEnabled > 0.5f);
            _mockTraversalToggle?.SetValueWithoutNotify(tuning.MockTraversalEnabled > 0.5f);
        }

        private void ApplyTuningFromFields()
        {
            BiomeTransitionTuningDTO tuning = default;
            if (!BiomeTransitionManagerRuntime.TryReadTuning(out tuning))
            {
                tuning.RadiusScale = 1f;
                tuning.HardwareQualityOverride = -1f;
                tuning.LowCadenceHz = 5f;
                tuning.UltraCadenceHz = 60f;
                tuning.DitherStrength = 1f;
                tuning.MaxCenterScanScale = 1f;
            }

            tuning.RadiusScale = math.max(0.1f, _radiusScaleSlider.value);
            tuning.HardwareQualityOverride = math.clamp(_qualityOverrideSlider.value, -1f, 1f);
            tuning.DitherStrength = math.max(0f, _ditherStrengthSlider.value);
            tuning.LowCadenceHz = math.clamp(_lowCadenceSlider.value, 1f, 15f);
            tuning.UltraCadenceHz = math.max(tuning.LowCadenceHz, _ultraCadenceSlider.value);
            tuning.MaxCenterScanScale = math.clamp(_scanScaleSlider.value, 0.25f, 1f);
            tuning.DebugDrawEnabled = _debugDrawToggle.value ? 1f : 0f;
            tuning.MockTraversalEnabled = _mockTraversalToggle.value ? 1f : 0f;
            BiomeTransitionManagerRuntime.TryWriteTuning(in tuning);

            if (tuning.HardwareQualityOverride >= 0f)
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(tuning.HardwareQualityOverride, true);
            else
                HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(0f, false);
        }

        private void UpdateReadout()
        {
            if (_readout == null)
                return;

            if (!BiomeTransitionManagerRuntime.TryReadSnapshot(
                    out CurrentAtmosphereDTO atmosphere,
                    out BiomeBlendMaskDTO mask,
                    out BiomeTransitionCounterDTO counters))
            {
                _readout.text = "Biome transition vault buffers are not ready.";
                return;
            }

            _readout.text =
                "Dominant 0x" + counters.CurrentDominantBiomeHash.ToString("X8") +
                " | Blend " + counters.LastBlendCount +
                " | Weight Sum " + counters.LastWeightSum.ToString("0.000", CultureInfo.InvariantCulture) +
                " | Q " + counters.LastQualityWeight.ToString("0.000", CultureInfo.InvariantCulture) +
                " | Fog " + atmosphere.FogColor.x.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                atmosphere.FogColor.y.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                atmosphere.FogColor.z.ToString("0.000", CultureInfo.InvariantCulture) +
                " | Weights " + mask.Weights.x.ToString("0.000", CultureInfo.InvariantCulture) + "/" +
                mask.Weights.y.ToString("0.000", CultureInfo.InvariantCulture) + "/" +
                mask.Weights.z.ToString("0.000", CultureInfo.InvariantCulture) + "/" +
                mask.Weights.w.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static void ValidateLayout()
        {
            bool layoutOk = BiomeTransitionNativeLayout.Validate();
            bool auditOk = BiomeTransitionManagerRuntime.TryRunSelfAudit(out uint faultFlags, out float weightError);
            if (!layoutOk)
                Debug.LogError("[BiomeTransitionTunerWindow] Native layout validation failed.");
            else if (!auditOk)
                Debug.LogWarning("[BiomeTransitionTunerWindow] Self audit pending/faulted. Flags=0x" + faultFlags.ToString("X8") + " weightError=" + weightError.ToString("0.000000", CultureInfo.InvariantCulture));
            else
                Debug.Log("[BiomeTransitionTunerWindow] Native layout and blend self-audit passed.");
        }

        private static void ReloadCsv()
        {
            if (!BiomeTransitionManagerRuntime.TryReloadCsvFromEditor())
                Debug.LogWarning("[BiomeTransitionTunerWindow] Runtime host is not active; CSV reload skipped.");
        }

        private static void DumpBlackBox()
        {
            if (!BiomeTransitionManagerRuntime.TryDumpBlackBoxFromEditor())
                Debug.LogWarning("[BiomeTransitionTunerWindow] Black-box dump skipped; telemetry is not ready.");
        }
    }

    internal static class BiomeTransitionLayoutEditorGuard
    {
        [InitializeOnLoadMethod]
        private static void ValidateOnLoad()
        {
            if (!BiomeTransitionNativeLayout.Validate())
                Debug.LogError("[BiomeTransitionLayoutEditorGuard] Biome transition native layout mismatch.");
        }
    }
}
#endif
