using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Core;
using Hecton8.Items;
using Hecton8.Modding;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Tracks cumulative resource extraction and exposes runtime fabrication power multipliers based on scarcity.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6250)]
    [AddComponentMenu("Hecton8/Economy/Resource Scarcity Director")]
    public sealed class ResourceScarcityDirector : MonoBehaviour, ISaveable
    {
        private const int InitialTrackedCapacity = 64;
        private const int UnitsPerScarcityStep = 100;
        private const float ScarcityStepMultiplier = 0.04f;
        private const float MaxIngredientMultiplier = 1.80f;

        private static ResourceScarcityDirector _instance;

        // COLD ALLOC: Dictionary<string,int>[64] - cumulative collected raw-resource counts by stable item ID - owner: ResourceScarcityDirector
        private readonly Dictionary<string, int> _collectedByItemId =
            new Dictionary<string, int>(InitialTrackedCapacity, System.StringComparer.Ordinal);

        private HectonEventSubscription _itemCollectedSubscription;

        /// <summary>
        /// Active runtime owner while the gameplay scene is loaded.
        /// </summary>
        public static ResourceScarcityDirector Instance => _instance;

        /// <summary>
        /// Save priority keeps scarcity state in the world band before player inventory consumers.
        /// </summary>
        public int SavePriority => 40;

        /// <summary>
        /// Load priority keeps scarcity state in the world band before player inventory consumers.
        /// </summary>
        public int LoadPriority => 40;

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

            if (_itemCollectedSubscription == null)
                _itemCollectedSubscription = HectonEventBus.Subscribe<ItemCollectedEvent>(HandleItemCollected, "economy.scarcity");
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;
        }

        private void OnDestroy()
        {
            SaveManager.Instance?.Unregister(this);
            _itemCollectedSubscription?.Dispose();
            _itemCollectedSubscription = null;

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for the specified recipe.
        /// </summary>
        public static float ResolveCraftPowerMultiplier(RecipeData recipe)
        {
            return _instance != null ? _instance.GetCraftPowerMultiplier(recipe) : 1f;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for the specified recipe.
        /// </summary>
        public float GetCraftPowerMultiplier(RecipeData recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count == 0)
                return 1f;

            float weightedSum = 0f;
            int totalIngredientUnits = 0;

            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                float ingredientMultiplier = GetIngredientMultiplier(cost.item.PersistentId);
                weightedSum += ingredientMultiplier * cost.amount;
                totalIngredientUnits += cost.amount;
            }

            if (totalIngredientUnits <= 0)
                return 1f;

            return weightedSum / totalIngredientUnits;
        }

        /// <summary>
        /// Returns the current scarcity multiplier for a single resource item.
        /// </summary>
        public float GetIngredientMultiplier(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 1f;

            if (!_collectedByItemId.TryGetValue(itemId, out int collectedCount) || collectedCount <= 0)
                return 1f;

            int scarcitySteps = collectedCount / UnitsPerScarcityStep;
            if (scarcitySteps <= 0)
                return 1f;

            return Mathf.Clamp(1f + scarcitySteps * ScarcityStepMultiplier, 1f, MaxIngredientMultiplier);
        }

        private void HandleItemCollected(ItemCollectedEvent itemCollectedEvent)
        {
            if (itemCollectedEvent == null || itemCollectedEvent.Item == null || itemCollectedEvent.Quantity <= 0)
                return;

            ItemData item = itemCollectedEvent.Item;
            if (!item.isRawResource && item.category != ItemCategory.Material)
                return;

            string itemId = item.PersistentId;
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            if (_collectedByItemId.TryGetValue(itemId, out int currentCount))
                _collectedByItemId[itemId] = currentCount + itemCollectedEvent.Quantity;
            else
                _collectedByItemId[itemId] = itemCollectedEvent.Quantity;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref ResourceScarcityDTO dto = ref data.resourceScarcity;
            dto.EnsureCapacity();
            dto.entryCount = 0;

            Dictionary<string, int>.Enumerator enumerator = _collectedByItemId.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (dto.entryCount >= ResourceScarcityDTO.MaxTrackedResources)
                    break;

                dto.itemIds[dto.entryCount] = enumerator.Current.Key;
                dto.collectedCounts[dto.entryCount] = Mathf.Max(0, enumerator.Current.Value);
                dto.entryCount++;
            }
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _collectedByItemId.Clear();

            if (data == null)
                return;

            ResourceScarcityDTO dto = data.resourceScarcity;
            if (dto.itemIds == null || dto.collectedCounts == null || dto.entryCount <= 0)
                return;

            int count = Mathf.Min(dto.entryCount, dto.itemIds.Length, dto.collectedCounts.Length);
            for (int i = 0; i < count; i++)
            {
                string itemId = dto.itemIds[i];
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int collectedCount = Mathf.Max(0, dto.collectedCounts[i]);
                if (collectedCount <= 0)
                    continue;

                _collectedByItemId[itemId] = collectedCount;
            }
        }
    }
}
