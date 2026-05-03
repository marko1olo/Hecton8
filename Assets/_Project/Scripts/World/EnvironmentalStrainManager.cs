using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.Modding;
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
    public sealed class EnvironmentalStrainManager : MonoBehaviour, ISaveable
    {
        private const float BasePlasticRecycleStrain = 1.2f;
        private const float BaseDiscardPollution = 1.0f;
        private const int MaxTrackedSectorStrainSlots = 128;
        private const float SectorEdgeLengthMeters = 1000f;
        private const float HarvestedResourceSectorStrainPerUnit = 0.02f;
        private const float PreyKillSectorStrainPerUnit = 0.08f;
        private const float EcologicalCollapseThreshold = 0.8f;

        private static EnvironmentalStrainManager _instance;

        private HectonEventSubscription _itemCollectedSubscription;
        private HectonEventSubscription _itemRecycledSubscription;
        private HectonEventSubscription _itemDiscardedSubscription;
        private bool _serviceRegistered;

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
        public static EnvironmentalStrainManager Instance => _instance;

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
        public static float CurrentPredatorAggressionScale => _instance != null ? _instance.GetPredatorAggressionScale() : 1f;

        /// <summary>
        /// Resolves the normalized ecological strain carried by the 1 km sector containing the supplied world position.
        /// </summary>
        public static bool TryGetSectorStrain01(Vector3 worldPosition, out float strain01)
        {
            if (_instance != null)
                return _instance.TryResolveSectorStrain(worldPosition, out strain01);

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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            TryRegisterService();
            GlobalRegistry.Save?.Register(this);

            if (_itemCollectedSubscription == null)
                _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, "world.environment");

            if (_itemRecycledSubscription == null)
                _itemRecycledSubscription = HectonEventBus.Subscribe<ItemRecycledEvent>(HandleItemRecycled, "world.environment");

            if (_itemDiscardedSubscription == null)
                _itemDiscardedSubscription = HectonEventBus.Subscribe<ItemDiscardedEvent>(HandleItemDiscarded, "world.environment");
        }

        private void OnDisable()
        {
            GlobalRegistry.Save?.Unregister(this);
            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;
            _itemRecycledSubscription?.Dispose();
            _itemRecycledSubscription = null;
            _itemDiscardedSubscription?.Dispose();
            _itemDiscardedSubscription = null;
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            GlobalRegistry.Save?.Unregister(this);
            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;
            _itemRecycledSubscription?.Dispose();
            _itemRecycledSubscription = null;
            _itemDiscardedSubscription?.Dispose();
            _itemDiscardedSubscription = null;
            TryUnregisterService();

            if (_instance == this)
                _instance = null;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterEnvironmentalStrainRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EnvironmentalStrain, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterEnvironmentalStrainRuntime(this);
            _serviceRegistered = false;
        }

        private void HandleItemCollected(ItemCollectedEvent itemCollectedEvent)
        {
            if (itemCollectedEvent == null || itemCollectedEvent.Item == null || itemCollectedEvent.Quantity <= 0)
                return;

            if (!itemCollectedEvent.HasInteractorPosition)
                return;

            ItemData item = itemCollectedEvent.Item;
            if (!item.isRawResource && item.category != ItemCategory.Material)
                return;

            AccumulateSectorStrain(
                itemCollectedEvent.InteractorPosition,
                itemCollectedEvent.Quantity * HarvestedResourceSectorStrainPerUnit);
        }

        private void HandleItemRecycled(ItemRecycledEvent itemRecycledEvent)
        {
            if (itemRecycledEvent == null || itemRecycledEvent.Item == null || itemRecycledEvent.Quantity <= 0)
                return;

            if (!IsPlasticLike(itemRecycledEvent.Item))
                return;

            float strainDelta = ApplyGreenTechReduction(BasePlasticRecycleStrain * itemRecycledEvent.Quantity);
            _microplasticStrain += strainDelta;
            _recycledPlasticItemCount += itemRecycledEvent.Quantity;
        }

        private void HandleItemDiscarded(ItemDiscardedEvent itemDiscardedEvent)
        {
            if (itemDiscardedEvent == null || itemDiscardedEvent.Item == null || itemDiscardedEvent.Quantity <= 0)
                return;

            float pollutionDelta = ApplyGreenTechReduction(ResolveDiscardPollution(itemDiscardedEvent.Item) * itemDiscardedEvent.Quantity);
            _generalPollution += pollutionDelta;
            _discardedItemCount += itemDiscardedEvent.Quantity;
        }

        private float GetPredatorAggressionScale()
        {
            float weightedDebt = _generalPollution + _microplasticStrain * 1.35f;
            float normalizedDebt = Mathf.Clamp01(weightedDebt / 250f);
            return Mathf.Lerp(1f, 1.35f, normalizedDebt);
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

        private static float ResolveDiscardPollution(ItemData item)
        {
            if (item == null)
                return BaseDiscardPollution;

            float categoryWeight;
            switch (item.category)
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

            return BaseDiscardPollution + Mathf.Clamp(item.weight * 0.35f, 0f, 1.5f) + (categoryWeight - 1f);
        }

        private static bool IsPlasticLike(ItemData item)
        {
            if (item == null)
                return false;

            string persistentId = item.PersistentId ?? string.Empty;
            string displayName = item.itemName ?? string.Empty;

            if (ContainsKeyword(persistentId, "Resin") || ContainsKeyword(displayName, "Resin"))
                return true;

            if (ContainsKeyword(persistentId, "Poly") || ContainsKeyword(displayName, "Poly"))
                return true;

            if (ContainsKeyword(persistentId, "FiberMesh") || ContainsKeyword(displayName, "Fiber Mesh"))
                return true;

            if (ContainsKeyword(persistentId, "Sealant") || ContainsKeyword(displayName, "Sealant"))
                return true;

            return false;
        }

        private static bool ContainsKeyword(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
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
