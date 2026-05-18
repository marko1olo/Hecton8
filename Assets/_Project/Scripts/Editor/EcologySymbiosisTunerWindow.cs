#if UNITY_EDITOR
using Hecton8.AI.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class EcologySymbiosisTunerWindow : EditorWindow
    {
        private const int MaxGizmoLines = 128;

        private enum TuningField
        {
            FeedingRate,
            ToxinPotency,
            CamouflageRadius,
            ParasiteGrowthSpeed,
            OxygenRateScale,
            MacroThreshold
        }

        private Label _stateLabel;
        private Slider _feedingRateSlider;
        private Slider _toxinPotencySlider;
        private Slider _camouflageRadiusSlider;
        private Slider _parasiteGrowthSlider;
        private Slider _oxygenRateSlider;
        private Slider _macroThresholdSlider;
        private Toggle _drawGizmosToggle;
        private Label _activeExchangesLabel;
        private Label _biomassTransferredLabel;
        private Label _oxygenEmittersLabel;
        private Label _toxemiaLabel;
        private Label _camouflageLabel;
        private Label _seedsLabel;
        private Label _adherenceLabel;
        private Label _overflowLabel;
        private bool _refreshing;

        [MenuItem("HECTON-8/Ecology Symbiosis Tuner")]
        public static void Open()
        {
            GetWindow<EcologySymbiosisTunerWindow>("Ecology Symbiosis Tuner");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnDrawGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnDrawGizmos;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _stateLabel = new Label();
            _stateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_stateLabel);

            _feedingRateSlider = CreateSlider("Feeding Rate", 0.001f, 0.25f, TuningField.FeedingRate);
            _toxinPotencySlider = CreateSlider("Toxin Potency", 0.01f, 4f, TuningField.ToxinPotency);
            _camouflageRadiusSlider = CreateSlider("Camouflage Radius", 0.1f, 12f, TuningField.CamouflageRadius);
            _parasiteGrowthSlider = CreateSlider("Parasite Growth Speed", 0.0005f, 0.2f, TuningField.ParasiteGrowthSpeed);
            _oxygenRateSlider = CreateSlider("Oxygen Rate Scale", 0.001f, 0.4f, TuningField.OxygenRateScale);
            _macroThresholdSlider = CreateSlider("Macro Threshold", 0.05f, 0.8f, TuningField.MacroThreshold);

            root.Add(_feedingRateSlider);
            root.Add(_toxinPotencySlider);
            root.Add(_camouflageRadiusSlider);
            root.Add(_parasiteGrowthSlider);
            root.Add(_oxygenRateSlider);
            root.Add(_macroThresholdSlider);

            _drawGizmosToggle = new Toggle("Draw Symbiosis Lines");
            _drawGizmosToggle.RegisterValueChangedCallback(evt => SetGizmoFlag(evt.newValue));
            root.Add(_drawGizmosToggle);

            _activeExchangesLabel = CreateCounterLabel(root);
            _biomassTransferredLabel = CreateCounterLabel(root);
            _oxygenEmittersLabel = CreateCounterLabel(root);
            _toxemiaLabel = CreateCounterLabel(root);
            _camouflageLabel = CreateCounterLabel(root);
            _seedsLabel = CreateCounterLabel(root);
            _adherenceLabel = CreateCounterLabel(root);
            _overflowLabel = CreateCounterLabel(root);

            root.schedule.Execute(RefreshFromVault).Every(250);
            RefreshFromVault();
        }

        private static Label CreateCounterLabel(VisualElement root)
        {
            Label label = new Label();
            label.style.marginTop = 2f;
            root.Add(label);
            return label;
        }

        private Slider CreateSlider(string label, float min, float max, TuningField field)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt => SetScalar(field, evt.newValue));
            return slider;
        }

        private void RefreshFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
            {
                SetUnavailable("Play Mode DataVault is not available.");
                return;
            }

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                SetUnavailable("Symbiosis tuning buffer is not registered.");
                return;
            }

            ref readonly SymbiosisTuningDTO tuning = ref tuningHandle.GetElementAsReadOnlyRef(vault, 0);
            SyncControls(SymbiosisTuningDTO.Sanitize(tuning));
            DrawCounters(vault);
            _stateLabel.text = "Play Mode DataVault: linked";
        }

        private void SetUnavailable(string message)
        {
            _stateLabel.text = message;
            _refreshing = true;
            SetControlsEnabled(false);
            _refreshing = false;
        }

        private void SetControlsEnabled(bool enabled)
        {
            _feedingRateSlider.SetEnabled(enabled);
            _toxinPotencySlider.SetEnabled(enabled);
            _camouflageRadiusSlider.SetEnabled(enabled);
            _parasiteGrowthSlider.SetEnabled(enabled);
            _oxygenRateSlider.SetEnabled(enabled);
            _macroThresholdSlider.SetEnabled(enabled);
            _drawGizmosToggle.SetEnabled(enabled);
        }

        private void SyncControls(SymbiosisTuningDTO tuning)
        {
            _refreshing = true;
            SetControlsEnabled(true);
            _feedingRateSlider.SetValueWithoutNotify(tuning.FeedingRate);
            _toxinPotencySlider.SetValueWithoutNotify(tuning.ToxinPotency);
            _camouflageRadiusSlider.SetValueWithoutNotify(tuning.CamouflageRadius);
            _parasiteGrowthSlider.SetValueWithoutNotify(tuning.ParasiteGrowthSpeed);
            _oxygenRateSlider.SetValueWithoutNotify(tuning.OxygenRateScale);
            _macroThresholdSlider.SetValueWithoutNotify(tuning.MacroThreshold);
            _drawGizmosToggle.SetValueWithoutNotify((tuning.Flags & ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos) != 0u);
            _refreshing = false;
        }

        private void SetScalar(TuningField field, float value)
        {
            if (_refreshing)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryGetMutableTuning(vault, out SymbiosisTuningDTO tuning))
                return;

            switch (field)
            {
                case TuningField.FeedingRate:
                    tuning.FeedingRate = value;
                    break;
                case TuningField.ToxinPotency:
                    tuning.ToxinPotency = value;
                    break;
                case TuningField.CamouflageRadius:
                    tuning.CamouflageRadius = value;
                    break;
                case TuningField.ParasiteGrowthSpeed:
                    tuning.ParasiteGrowthSpeed = value;
                    break;
                case TuningField.OxygenRateScale:
                    tuning.OxygenRateScale = value;
                    break;
                case TuningField.MacroThreshold:
                    tuning.MacroThreshold = value;
                    break;
            }

            WriteTuning(vault, tuning);
        }

        private void SetGizmoFlag(bool enabled)
        {
            if (_refreshing)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryGetMutableTuning(vault, out SymbiosisTuningDTO tuning))
                return;

            if (enabled)
                tuning.Flags |= ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos;
            else
                tuning.Flags &= ~ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos;

            WriteTuning(vault, tuning);
        }

        private static bool TryGetMutableTuning(IDataVault vault, out SymbiosisTuningDTO tuning)
        {
            tuning = default;
            if (!Application.isPlaying || vault == null ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                return false;
            }

            ref readonly SymbiosisTuningDTO current = ref tuningHandle.GetElementAsReadOnlyRef(vault, 0);
            tuning = SymbiosisTuningDTO.Sanitize(current);
            return true;
        }

        private static void WriteTuning(IDataVault vault, SymbiosisTuningDTO tuning)
        {
            if (vault == null ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                return;
            }

            ref SymbiosisTuningDTO target = ref tuningHandle.GetElementAsRef(vault, 0);
            target = SymbiosisTuningDTO.Sanitize(tuning);
            SceneView.RepaintAll();
        }

        private void DrawCounters(IDataVault vault)
        {
            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisCounters, out VaultBufferHandle<SymbiosisCounterDTO> counterHandle) ||
                !counterHandle.IsCreated)
            {
                return;
            }

            ref readonly SymbiosisCounterDTO counter = ref counterHandle.GetElementAsReadOnlyRef(vault, 0);
            _activeExchangesLabel.text = "Active Exchanges: " + counter.ActiveExchanges;
            _biomassTransferredLabel.text = "Biomass Transferred: " + (counter.BiomassTransferredMilli * 0.001f);
            _oxygenEmittersLabel.text = "Oxygen Emitters: " + counter.OxygenEmitterCount;
            _toxemiaLabel.text = "Toxemia: " + counter.ToxemiaCount;
            _camouflageLabel.text = "Camouflage: " + counter.CamouflageCount;
            _seedsLabel.text = "Seeds: " + counter.SeedCount;
            _adherenceLabel.text = "Adherence: " + counter.AdherenceCount;
            _overflowLabel.text = "Overflow: " + counter.OverflowCount;
        }

        private static void OnDrawGizmos(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!Application.isPlaying || vault == null)
                return;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisTuning, out VaultBufferHandle<SymbiosisTuningDTO> tuningHandle) ||
                !tuningHandle.IsCreated)
            {
                return;
            }

            ref readonly SymbiosisTuningDTO tuning = ref tuningHandle.GetElementAsReadOnlyRef(vault, 0);
            if ((tuning.Flags & ShinobuFloraFaunaSymbiosisSolver.TuningFlagEditorGizmos) == 0u)
                return;

            if (!TryResolveGizmoBuffers(
                    vault,
                    out NativeArray<SymbiosisExchangeDTO> exchanges,
                    out NativeArray<SymbiosisCounterDTO> counters,
                    out NativeArray<SymbiosisFloraDTO> flora,
                    out NativeArray<SymbiosisFloraAupDTO> floraAups,
                    out NativeArray<MockFishSymbiosisDTO> mockFish,
                    out NativeArray<AmbientEntityAupDTO> ambientAups))
            {
                return;
            }

            int count = math.min(math.min(counters[0].ActiveExchanges, exchanges.Length), MaxGizmoLines);
            Handles.color = new Color(0.1f, 1f, 0.35f, 0.9f);
            for (int i = 0; i < count; i++)
            {
                SymbiosisExchangeDTO exchange = exchanges[i];
                if (!TryFindFaunaAup(exchange.FaunaHash, mockFish, ambientAups, out AbsoluteUniversePosition faunaAup) ||
                    !TryFindClosestFloraAup(exchange.FloraHash, in faunaAup, flora, floraAups, out AbsoluteUniversePosition floraAup))
                {
                    continue;
                }

                Handles.DrawLine(ToRuntimeVector3(in floraAup), ToRuntimeVector3(in faunaAup), 2f);
            }
        }

        private static bool TryResolveGizmoBuffers(
            IDataVault vault,
            out NativeArray<SymbiosisExchangeDTO> exchanges,
            out NativeArray<SymbiosisCounterDTO> counters,
            out NativeArray<SymbiosisFloraDTO> flora,
            out NativeArray<SymbiosisFloraAupDTO> floraAups,
            out NativeArray<MockFishSymbiosisDTO> mockFish,
            out NativeArray<AmbientEntityAupDTO> ambientAups)
        {
            exchanges = default;
            counters = default;
            flora = default;
            floraAups = default;
            mockFish = default;
            ambientAups = default;

            if (!vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisExchanges, out VaultBufferHandle<SymbiosisExchangeDTO> exchangeHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisCounters, out VaultBufferHandle<SymbiosisCounterDTO> counterHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisFlora, out VaultBufferHandle<SymbiosisFloraDTO> floraHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisFloraAups, out VaultBufferHandle<SymbiosisFloraAupDTO> floraAupHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuSymbiosisMockFish, out VaultBufferHandle<MockFishSymbiosisDTO> mockFishHandle) ||
                !vault.TryGetBufferHandle(BufferID.ShinobuAmbientAups, out VaultBufferHandle<AmbientEntityAupDTO> ambientAupHandle))
            {
                return false;
            }

            exchanges = exchangeHandle.Resolve(vault);
            counters = counterHandle.Resolve(vault);
            flora = floraHandle.Resolve(vault);
            floraAups = floraAupHandle.Resolve(vault);
            mockFish = mockFishHandle.Resolve(vault);
            ambientAups = ambientAupHandle.Resolve(vault);
            return exchanges.IsCreated &&
                   counters.IsCreated &&
                   counters.Length > 0 &&
                   flora.IsCreated &&
                   floraAups.IsCreated &&
                   mockFish.IsCreated &&
                   ambientAups.IsCreated;
        }

        private static bool TryFindClosestFloraAup(
            uint floraHash,
            in AbsoluteUniversePosition faunaAup,
            NativeArray<SymbiosisFloraDTO> flora,
            NativeArray<SymbiosisFloraAupDTO> floraAups,
            out AbsoluteUniversePosition aup)
        {
            int count = math.min(flora.Length, floraAups.Length);
            float bestDistanceSq = float.MaxValue;
            aup = default;
            for (int i = 0; i < count; i++)
            {
                if (flora[i].FloraHash != floraHash)
                    continue;

                AbsoluteUniversePosition candidate = floraAups[i].PositionAup.ToAup();
                float3 delta = ShinobuFloraFaunaSymbiosisSolver.AupToLocal(in candidate, in faunaAup);
                float distanceSq = math.lengthsq(delta);
                if (!math.isfinite(distanceSq) || distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                aup = candidate;
            }

            return bestDistanceSq < float.MaxValue;
        }

        private static bool TryFindFaunaAup(
            uint faunaHash,
            NativeArray<MockFishSymbiosisDTO> mockFish,
            NativeArray<AmbientEntityAupDTO> ambientAups,
            out AbsoluteUniversePosition aup)
        {
            for (int i = 0; i < mockFish.Length; i++)
            {
                if (mockFish[i].StableSeed == faunaHash)
                {
                    aup = mockFish[i].PositionAup.ToAup();
                    return true;
                }
            }

            for (int i = 0; i < ambientAups.Length; i++)
            {
                if (ambientAups[i].StableSeed == faunaHash)
                {
                    aup = ambientAups[i].PositionAup;
                    return true;
                }
            }

            aup = default;
            return false;
        }

        private static Vector3 ToRuntimeVector3(in AbsoluteUniversePosition aup)
        {
            float3 runtime = aup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }
    }
}
#endif
