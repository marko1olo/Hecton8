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

        private static EnvironmentalStrainManager _instance;

        private HectonEventSubscription _itemRecycledSubscription;
        private HectonEventSubscription _itemDiscardedSubscription;

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
            SaveManager.Instance?.Register(this);

            if (_itemRecycledSubscription == null)
                _itemRecycledSubscription = HectonEventBus.Subscribe<ItemRecycledEvent>(HandleItemRecycled, "world.environment");

            if (_itemDiscardedSubscription == null)
                _itemDiscardedSubscription = HectonEventBus.Subscribe<ItemDiscardedEvent>(HandleItemDiscarded, "world.environment");
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
            _itemRecycledSubscription?.Dispose();
            _itemRecycledSubscription = null;
            _itemDiscardedSubscription?.Dispose();
            _itemDiscardedSubscription = null;
        }

        private void OnDestroy()
        {
            SaveManager.Instance?.Unregister(this);
            _itemRecycledSubscription?.Dispose();
            _itemRecycledSubscription = null;
            _itemDiscardedSubscription?.Dispose();
            _itemDiscardedSubscription = null;

            if (_instance == this)
                _instance = null;
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
