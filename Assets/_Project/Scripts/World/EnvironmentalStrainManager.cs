using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Tracks ecological debt caused by wasteful player actions and exposes predator-aggression pressure from pollution.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6240)]
    [AddComponentMenu("Hecton8/World/Environmental Strain Manager")]
    public sealed class EnvironmentalStrainManager : MonoBehaviour, ISaveable, IUpdatable, IEnvironmentalStrainReadModel, IGlobalRegistryHotSwapListener
    {
        private const float BasePlasticRecycleStrain = 1.2f;
        private const float BaseDiscardPollution = 1.0f;
        private const int MaxTrackedSectorStrainSlots = 128;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float HarvestedResourceSectorStrainPerUnit = 0.02f;
        private const float PreyKillSectorStrainPerUnit = 0.08f;
        private const float EcologicalCollapseThreshold = 0.8f;

        private bool _serviceRegistered;
        private bool _tickRegistered;
        private bool _hotSwapRegistered;
        private bool _duplicateServiceSuppressed;
        private uint _lastProcessedItemLifecycleSequence;
        private ISaveService _saveService;

        // COLD ALLOC: long[128] — packed 1 km sector keys for local ecological strain lookup — owner: EnvironmentalStrainManager
        private readonly long[] _sectorStrainKeys = new long[MaxTrackedSectorStrainSlots];
        // COLD ALLOC: float[128] — normalized local ecological strain values per tracked sector — owner: EnvironmentalStrainManager
        private readonly float[] _sectorStrainValues = new float[MaxTrackedSectorStrainSlots];
        private int _trackedSectorStrainCount;

        [SerializeField] private float _microplasticStrain;
        [SerializeField] private float _generalPollution;
        [SerializeField] private int _recycledPlasticItemCount;
        [SerializeField] private int _discardedItemCount;

        /// <summary>
        /// Active runtime owner while the gameplay scene is loaded.
        /// </summary>
        public static EnvironmentalStrainManager Instance => GlobalRegistry.EnvironmentalStrain;

        /// <summary>
        /// Save priority keeps environmental state in the world band before player-facing consumers.
        /// </summary>
        public int SavePriority => 41;

        /// <summary>
        /// Load priority keeps environmental state in the world band before player-facing consumers.
        /// </summary>
        public int LoadPriority => 41;

        /// <summary>
        /// Current accumulated microplastic burden.
        /// </summary>
        public float MicroplasticStrain => _microplasticStrain;

        /// <summary>
        /// Current accumulated general pollution burden.
        /// </summary>
        public float GeneralPollution => _generalPollution;

        /// <summary>
        /// Aggression multiplier exported to the dynamic difficulty director.
        /// </summary>
        public static float CurrentPredatorAggressionScale
        {
            get
            {
                EnvironmentalStrainManager registered = GlobalRegistry.EnvironmentalStrain;
                return registered != null ? registered.GetPredatorAggressionScale() : 1f;
            }
        }

        /// <summary>
        /// Resolves the normalized ecological strain carried by the 1 km sector containing the supplied world position.
        /// </summary>
        public static bool TryGetSectorStrain01(Vector3 worldPosition, out float strain01)
        {
            EnvironmentalStrainManager registered = GlobalRegistry.EnvironmentalStrain;
            if (registered != null)
                return registered.TryResolveSectorStrain(worldPosition, out strain01);

            strain01 = 0f;
            return false;
        }

        /// <summary>
        /// True when the containing sector crossed the authored collapse threshold.
        /// </summary>
        public static bool IsSectorEcologicallyCollapsed(Vector3 worldPosition)
        {
            return TryGetSectorStrain01(worldPosition, out float strain01) && strain01 >= EcologicalCollapseThreshold;
        }

        private void Awake()
        {
            EnvironmentalStrainManager registered = GlobalRegistry.EnvironmentalStrain;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
            }
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            TryRegisterService();
            if (_duplicateServiceSuppressed)
                return;

            CacheSaveServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterTick();
            _saveService?.Register(this);
        }

        private void Start()
        {
            TryRegisterTick();
        }

        private void OnDisable()
        {
            _saveService?.Unregister(this);
            TryUnregisterHotSwapListener();
            _saveService = null;
            TryUnregisterTick();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            _saveService?.Unregister(this);
            TryUnregisterHotSwapListener();
            _saveService = null;
            TryUnregisterTick();
            TryUnregisterService();
        }

        public void Tick(float deltaTime)
        {
            DrainItemLifecycleSignals();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            EnvironmentalStrainManager registered = GlobalRegistry.EnvironmentalStrain;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
                return;
            }

            GlobalRegistry.RegisterEnvironmentalStrainRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EnvironmentalStrain, this);
        }

        private void SuppressDuplicateService()
        {
            _duplicateServiceSuppressed = true;
            _serviceRegistered = false;
            _tickRegistered = false;
            enabled = false;
            Destroy(gameObject);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterEnvironmentalStrainRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void CacheSaveServiceCold()
        {
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            if (Application.isPlaying && previousService is ISaveService previousSave)
                previousSave.Unregister(this);

            _saveService = currentService as ISaveService;

            if (Application.isPlaying && _saveService != null && isActiveAndEnabled && !_duplicateServiceSuppressed)
                _saveService.Register(this);
        }

        private void DrainItemLifecycleSignals()
        {
            global::System.ReadOnlySpan<ItemLifecycleSignal> signals = SignalBus<ItemLifecycleSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ItemLifecycleSignal signal = signals[i];
                if (!IsNewerSequence(signal.Sequence, _lastProcessedItemLifecycleSequence))
                    continue;

                _lastProcessedItemLifecycleSequence = signal.Sequence;
                HandleItemLifecycleSignal(in signal);
            }
        }

        private void HandleItemLifecycleSignal(in ItemLifecycleSignal signal)
        {
            int quantity = Mathf.Max(0, signal.Quantity);
            if (quantity <= 0)
                return;

            switch (signal.Action)
            {
                case ItemLifecycleSignal.ActionCollected:
                    HandleItemCollectedSignal(in signal, quantity);
                    break;
                case ItemLifecycleSignal.ActionRecycled:
                    HandleItemRecycledSignal(in signal, quantity);
                    break;
                case ItemLifecycleSignal.ActionDiscarded:
                    HandleItemDiscardedSignal(in signal, quantity);
                    break;
            }
        }

        private void HandleItemCollectedSignal(in ItemLifecycleSignal signal, int quantity)
        {
            if ((signal.Flags & ItemLifecycleSignal.FlagHasRuntimePosition) == 0)
                return;

            if ((signal.Flags & (ItemLifecycleSignal.FlagRawResource | ItemLifecycleSignal.FlagMaterialCategory)) == 0)
                return;

            AccumulateSectorStrain(
                new Vector3(signal.RuntimePosition.x, signal.RuntimePosition.y, signal.RuntimePosition.z),
                quantity * HarvestedResourceSectorStrainPerUnit);
        }

        private void HandleItemRecycledSignal(in ItemLifecycleSignal signal, int quantity)
        {
            if ((signal.Flags & ItemLifecycleSignal.FlagPlasticLike) == 0)
                return;

            float strainDelta = ApplyGreenTechReduction(BasePlasticRecycleStrain * quantity);
            _microplasticStrain += strainDelta;
            _recycledPlasticItemCount += quantity;
        }

        private void HandleItemDiscardedSignal(in ItemLifecycleSignal signal, int quantity)
        {
            float pollutionDelta = ApplyGreenTechReduction(ResolveDiscardPollution(signal) * quantity);
            _generalPollution += pollutionDelta;
            _discardedItemCount += quantity;
        }

        private static bool IsNewerSequence(uint candidate, uint lastProcessed)
        {
            return candidate != 0u && (lastProcessed == 0u || unchecked((int)(candidate - lastProcessed)) > 0);
        }

        private float GetPredatorAggressionScale()
        {
            float weightedDebt = _generalPollution + _microplasticStrain * 1.35f;
            float normalizedDebt = Mathf.Clamp01(weightedDebt / 250f);
            return 1f + normalizedDebt * 0.35f;
        }

        internal void AccumulateIndustrialStrain(float generalPollutionDelta, float microplasticDelta)
        {
            if (generalPollutionDelta > 0f)
                _generalPollution += ApplyGreenTechReduction(generalPollutionDelta);

            if (microplasticDelta > 0f)
                _microplasticStrain += ApplyGreenTechReduction(microplasticDelta);
        }

        internal void AccumulatePredationStrain(Vector3 worldPosition, int preyRemoved)
        {
            if (preyRemoved <= 0)
                return;

            AccumulateSectorStrain(worldPosition, preyRemoved * PreyKillSectorStrainPerUnit);
        }

        private bool TryResolveSectorStrain(Vector3 worldPosition, out float strain01)
        {
            int slot = FindSectorStrainSlot(PackSectorKey(worldPosition));
            if (slot >= 0)
            {
                strain01 = _sectorStrainValues[slot];
                return true;
            }

            strain01 = 0f;
            return false;
        }

        private void AccumulateSectorStrain(Vector3 worldPosition, float strainDelta)
        {
            if (strainDelta <= 0f)
                return;

            long packedKey = PackSectorKey(worldPosition);
            int slot = FindSectorStrainSlot(packedKey);
            if (slot < 0)
            {
                if (_trackedSectorStrainCount >= MaxTrackedSectorStrainSlots)
                    slot = FindLowestStrainSlot();
                else
                    slot = _trackedSectorStrainCount++;

                if (slot < 0)
                    return;

                _sectorStrainKeys[slot] = packedKey;
                _sectorStrainValues[slot] = 0f;
            }

            _sectorStrainValues[slot] = Mathf.Clamp01(_sectorStrainValues[slot] + strainDelta);
        }

        private int FindSectorStrainSlot(long packedKey)
        {
            for (int i = 0; i < _trackedSectorStrainCount; i++)
            {
                if (_sectorStrainKeys[i] == packedKey)
                    return i;
            }

            return -1;
        }

        private int FindLowestStrainSlot()
        {
            if (_trackedSectorStrainCount <= 0)
                return -1;

            int bestIndex = 0;
            float bestStrain = _sectorStrainValues[0];
            for (int i = 1; i < _trackedSectorStrainCount; i++)
            {
                if (_sectorStrainValues[i] >= bestStrain)
                    continue;

                bestStrain = _sectorStrainValues[i];
                bestIndex = i;
            }

            return bestIndex;
        }

        private static long PackSectorKey(Vector3 worldPosition)
        {
            int sectorX = Mathf.FloorToInt(worldPosition.x / SectorEdgeLengthMeters);
            int sectorZ = Mathf.FloorToInt(worldPosition.z / SectorEdgeLengthMeters);
            return ((long)sectorX << 32) | (uint)sectorZ;
        }

        private static float ResolveDiscardPollution(in ItemLifecycleSignal signal)
        {
            float categoryWeight;
            switch ((ItemCategory)signal.Category)
            {
                case ItemCategory.Material:
                    categoryWeight = 0.8f;
                    break;
                case ItemCategory.Component:
                    categoryWeight = 1.1f;
                    break;
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                    categoryWeight = 1.4f;
                    break;
                default:
                    categoryWeight = 1f;
                    break;
            }

            if (signal.PollutionMilli != 0u)
                return signal.PollutionMilli * 0.001f;

            return BaseDiscardPollution + Mathf.Clamp(signal.UnitWeightKg * 0.35f, 0f, 1.5f) + (categoryWeight - 1f);
        }

        private static float ApplyGreenTechReduction(float baseAmount)
        {
            if (baseAmount <= 0f)
                return 0f;

            int greenTechLevel = MetaProfileUtility.ResolveUpgradeLevel(MetaUpgradeRegistry.GreenTechId);
            float reductionPerLevel = 0.10f;
            if (MetaUpgradeRegistry.TryGetDefinition(MetaUpgradeRegistry.GreenTechId, out MetaUpgradeRegistry.MetaUpgradeDefinition definition) &&
                definition.PollutionReductionPerLevel > 0f)
            {
                reductionPerLevel = definition.PollutionReductionPerLevel;
            }

            float reduction = Mathf.Clamp01(greenTechLevel * reductionPerLevel);
            return baseAmount * (1f - reduction);
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.environmentalStrain.microplasticStrain = _microplasticStrain;
            data.environmentalStrain.generalPollution = _generalPollution;
            data.environmentalStrain.recycledPlasticItemCount = Mathf.Max(0, _recycledPlasticItemCount);
            data.environmentalStrain.discardedItemCount = Mathf.Max(0, _discardedItemCount);
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
                return;

            EnvironmentalStrainDTO dto = data.environmentalStrain;
            _microplasticStrain = Mathf.Max(0f, dto.microplasticStrain);
            _generalPollution = Mathf.Max(0f, dto.generalPollution);
            _recycledPlasticItemCount = Mathf.Max(0, dto.recycledPlasticItemCount);
            _discardedItemCount = Mathf.Max(0, dto.discardedItemCount);
        }
    }
}
