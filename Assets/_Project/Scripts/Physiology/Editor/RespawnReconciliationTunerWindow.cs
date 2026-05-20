#if UNITY_EDITOR
using Hecton8.Physiology;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physiology.Editor
{
    public sealed class RespawnReconciliationTunerWindow : EditorWindow
    {
        private const string UnavailableReadout = "DeathFadeIntensity: unavailable";
        private static readonly string[] s_fadeReadoutLut = CreateFadeReadoutLut();

        private Label _fadeReadout;
        private Slider _highQualityFadeRate;
        private Slider _lowQualityFadeRate;
        private Slider _penaltyMultiplier;
        private Slider _clearanceMeters;
        private Vector3Field _fallbackAup;

        [MenuItem("Hecton8/Survival/Reconciliation Tuner")]
        public static void Open()
        {
            GetWindow<RespawnReconciliationTunerWindow>("Reconciliation Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();

            _fadeReadout = new Label("DeathFadeIntensity: --");
            _highQualityFadeRate = new Slider("High quality fade rate", 0.0001f, 16f);
            _lowQualityFadeRate = new Slider("Low quality fade rate", 0.0001f, 16f);
            _penaltyMultiplier = new Slider("Penalty multiplier", 0f, 1f);
            _clearanceMeters = new Slider("Medical bay clearance", 0.25f, 16f);
            _fallbackAup = new Vector3Field("Fallback lifepod AUP");

            Button apply = new Button(ApplyTuning) { text = "Apply Vault Tuning" };
            Button reloadCsv = new Button(() => ShinobuRespawnReconciliationRuntime.TryReloadPenaltyCsvFromEditor()) { text = "Reload Penalty CSV" };
            Button dump = new Button(() => ShinobuRespawnReconciliationRuntime.TryDumpBlackBoxForEditor()) { text = "Dump Black Box" };

            root.Add(_fadeReadout);
            root.Add(_highQualityFadeRate);
            root.Add(_lowQualityFadeRate);
            root.Add(_penaltyMultiplier);
            root.Add(_clearanceMeters);
            root.Add(_fallbackAup);
            root.Add(apply);
            root.Add(reloadCsv);
            root.Add(dump);

            RefreshFromRuntime();
            root.schedule.Execute(RefreshFromRuntime).Every(250);
        }

        private void RefreshFromRuntime()
        {
            if (!ShinobuRespawnReconciliationRuntime.TryReadEditorState(out RespawnFadeDTO fade, out RespawnTuningDTO tuning))
            {
                _fadeReadout.text = UnavailableReadout;
                return;
            }

            int fadeBucket = math.clamp((int)math.round(math.saturate(fade.DeathFadeIntensity) * 1000f), 0, 1000);
            _fadeReadout.text = s_fadeReadoutLut[fadeBucket];
            _highQualityFadeRate.SetValueWithoutNotify(tuning.HighQualityFadeRate);
            _lowQualityFadeRate.SetValueWithoutNotify(tuning.LowQualityFadeRate);
            _penaltyMultiplier.SetValueWithoutNotify(tuning.PenaltyMultiplier);
            _clearanceMeters.SetValueWithoutNotify(tuning.ValidationClearanceMeters);
            _fallbackAup.SetValueWithoutNotify(new Vector3(
                (float)tuning.FallbackLifepodAUP.x,
                (float)tuning.FallbackLifepodAUP.y,
                (float)tuning.FallbackLifepodAUP.z));
        }

        private void ApplyTuning()
        {
            if (!ShinobuRespawnReconciliationRuntime.TryReadEditorState(out _, out RespawnTuningDTO tuning))
                tuning = default;

            Vector3 fallback = _fallbackAup.value;
            tuning.HighQualityFadeRate = _highQualityFadeRate.value;
            tuning.LowQualityFadeRate = _lowQualityFadeRate.value;
            tuning.PenaltyMultiplier = _penaltyMultiplier.value;
            tuning.ValidationClearanceMeters = _clearanceMeters.value;
            tuning.FallbackLifepodAUP = new double3(fallback.x, fallback.y, fallback.z);
            ShinobuRespawnReconciliationRuntime.TryWriteEditorTuning(in tuning);
        }

        private static string[] CreateFadeReadoutLut()
        {
            string[] values = new string[1001]; // COLD ALLOC: string[1001] - editor fade readout LUT - owner: RespawnReconciliationTunerWindow
            for (int i = 0; i < values.Length; i++)
            {
                int whole = i / 1000;
                int decimals = i - (whole * 1000);
                values[i] = "DeathFadeIntensity: " + whole + "." + decimals.ToString("000");
            }

            return values;
        }
    }
}
#endif
