using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Crafting;
using Hecton8.Economy;
using Hecton8.Ecosystem;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Modding
{
    internal static class ModExecutionScope
    {
        [System.ThreadStatic] private static string _currentModId;

        internal static string CurrentModId => string.IsNullOrWhiteSpace(_currentModId) ? "anonymous" : _currentModId;

        internal static Scope Enter(string modId)
        {
            return new Scope(modId);
        }

        internal readonly struct Scope : System.IDisposable
        {
            private readonly string _previousModId;

            internal Scope(string modId)
            {
                _previousModId = _currentModId;
                _currentModId = string.IsNullOrWhiteSpace(modId) ? "anonymous" : modId;
            }

            public void Dispose()
            {
                _currentModId = _previousModId;
            }
        }
    }

    internal static class ModSaveStateStore
    {
        // COLD ALLOC: Dictionary<string,string>[64] — custom mod save payload map persisted inside SaveData — owner: ModSaveStateStore
        private static readonly Dictionary<string, string> _customModData = new Dictionary<string, string>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _customModData.Clear();
        }

        internal static void SetModString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[ModSaveStateStore] Refused to write mod save data with an empty key.");
                return;
            }

            _customModData[key] = value ?? string.Empty;
        }

        internal static string GetModString(string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue ?? string.Empty;

            return _customModData.TryGetValue(key, out string value)
                ? value
                : (defaultValue ?? string.Empty);
        }

        internal static void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            if (data.CustomModData == null)
            {
                // COLD ALLOC: Dictionary<string,string>[64] — serialized mod save payload map — owner: SaveData
                data.CustomModData = new Dictionary<string, string>(64);
            }
            else
            {
                data.CustomModData.Clear();
            }

            Dictionary<string, string>.Enumerator enumerator = _customModData.GetEnumerator();
            while (enumerator.MoveNext())
                data.CustomModData[enumerator.Current.Key] = enumerator.Current.Value;
        }

        internal static void LoadFromSaveData(SaveData data)
        {
            _customModData.Clear();

            if (data == null || data.CustomModData == null || data.CustomModData.Count == 0)
                return;

            Dictionary<string, string>.Enumerator enumerator = data.CustomModData.GetEnumerator();
            while (enumerator.MoveNext())
                _customModData[enumerator.Current.Key] = enumerator.Current.Value;
        }
    }

    internal static class ModItemRegistry
    {
        // COLD ALLOC: List<ItemData>[16] — deferred item registrations until the runtime item catalog exists — owner: ModItemRegistry
        private static readonly List<ItemData> _pendingItems = new List<ItemData>(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingItems.Clear();
        }

        internal static bool TryRegister(ItemData itemData, out string error)
        {
            error = null;

            if (itemData == null)
            {
                error = "ItemData is null.";
                return false;
            }

            ItemCatalog catalog = ResolveActiveCatalog();
            if (catalog != null)
                return catalog.TryRegisterRuntimeItem(itemData, out error);

            if (ContainsPendingItem(itemData))
                return true;

            _pendingItems.Add(itemData);
            return true;
        }

        internal static void FlushPendingRegistrations()
        {
            ItemCatalog catalog = ResolveActiveCatalog();
            if (catalog == null || _pendingItems.Count == 0)
                return;

            for (int i = _pendingItems.Count - 1; i >= 0; i--)
            {
                ItemData itemData = _pendingItems[i];
                if (catalog.TryRegisterRuntimeItem(itemData, out string error))
                {
                    _pendingItems.RemoveAt(i);
                    continue;
                }

                Debug.LogWarning(
                    $"[ModItemRegistry] Failed to register pending runtime item '{(itemData != null ? itemData.name : "null")}': {error}");
                _pendingItems.RemoveAt(i);
            }
        }

        internal static ItemCatalog ResolveActiveCatalog()
        {
            PlayerInventory playerInventory = PlayerInventory.Instance;
            return playerInventory != null ? playerInventory.ItemCatalog : null;
        }

        private static bool ContainsPendingItem(ItemData itemData)
        {
            for (int i = 0; i < _pendingItems.Count; i++)
            {
                ItemData pending = _pendingItems[i];
                if (ReferenceEquals(pending, itemData))
                    return true;

                if (pending != null &&
                    itemData != null &&
                    string.Equals(pending.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class ModRecipeRegistry
    {
        // COLD ALLOC: List<RecipeData>[32] — runtime-only crafting recipe overlay — owner: ModRecipeRegistry
        private static readonly List<RecipeData> _runtimeRecipes = new List<RecipeData>(32);

        internal static event System.Action RegistryChanged;

        internal static int Count => _runtimeRecipes.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeRecipes.Clear();
            RegistryChanged = null;
        }

        internal static bool TryRegister(RecipeData recipeData, out string error)
        {
            error = null;

            if (recipeData == null)
            {
                error = "RecipeData is null.";
                return false;
            }

            if (recipeData.resultItem == null)
            {
                error = "Recipe result item is null.";
                return false;
            }

            if (recipeData.resultQuantity <= 0)
            {
                error = "Recipe result quantity must be greater than zero.";
                return false;
            }

            if (recipeData.ingredients == null || recipeData.ingredients.Count == 0)
            {
                error = "Recipe ingredients are empty.";
                return false;
            }

            if (ContainsRecipeReference(recipeData))
                return true;

            _runtimeRecipes.Add(recipeData);
            RegistryChanged?.Invoke();
            return true;
        }

        internal static void FlushPendingRegistrations()
        {
            RegistryChanged?.Invoke();
        }

        internal static RecipeData GetAt(int index)
        {
            if ((uint)index >= (uint)_runtimeRecipes.Count)
                return null;

            return _runtimeRecipes[index];
        }

        private static bool ContainsRecipeReference(RecipeData recipeData)
        {
            for (int i = 0; i < _runtimeRecipes.Count; i++)
            {
                if (ReferenceEquals(_runtimeRecipes[i], recipeData))
                    return true;
            }

            return false;
        }
    }

    internal static class ModBuildableRegistry
    {
        private const string DefaultCategory = "Mods";

        private struct PendingBuildableRegistration
        {
            public BuildableData Data;
            public string CustomCategory;
        }

        // COLD ALLOC: List<PendingBuildableRegistration>[16] — deferred buildable registrations until the live module catalog exists — owner: ModBuildableRegistry
        private static readonly List<PendingBuildableRegistration> _pendingBuildables = new List<PendingBuildableRegistration>(16);

        internal static event System.Action RegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingBuildables.Clear();
            RegistryChanged = null;
        }

        internal static bool TryRegister(BuildableData buildableData, string customCategory, out string error)
        {
            error = null;

            if (buildableData == null)
            {
                error = "BuildableData is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(buildableData.PersistentId))
            {
                error = "BuildableData.PersistentId is empty.";
                return false;
            }

            ModuleCatalog catalog = ResolveActiveCatalog();
            if (catalog != null)
            {
                bool success = catalog.TryRegisterRuntimeModule(buildableData, NormalizeCategory(customCategory), out error);
                if (success)
                    RegistryChanged?.Invoke();

                return success;
            }

            if (ContainsPendingBuildable(buildableData))
                return true;

            if (HasPendingAliasConflict(buildableData, out error))
                return false;

            _pendingBuildables.Add(new PendingBuildableRegistration
            {
                Data = buildableData,
                CustomCategory = NormalizeCategory(customCategory)
            });

            RegistryChanged?.Invoke();
            return true;
        }

        internal static void FlushPendingRegistrations()
        {
            ModuleCatalog catalog = ResolveActiveCatalog();
            if (catalog == null || _pendingBuildables.Count == 0)
                return;

            bool changed = false;
            for (int i = _pendingBuildables.Count - 1; i >= 0; i--)
            {
                PendingBuildableRegistration registration = _pendingBuildables[i];
                if (catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, out string error))
                {
                    _pendingBuildables.RemoveAt(i);
                    changed = true;
                    continue;
                }

                Debug.LogWarning(
                    $"[ModBuildableRegistry] Failed to register pending buildable '{(registration.Data != null ? registration.Data.name : "null")}': {error}");
                _pendingBuildables.RemoveAt(i);
            }

            if (changed)
                RegistryChanged?.Invoke();
        }

        internal static ModuleCatalog ResolveActiveCatalog()
        {
            ConstructionManager constructionManager = ConstructionManager.Instance;
            return constructionManager != null ? constructionManager.Catalog : null;
        }

        private static string NormalizeCategory(string customCategory)
        {
            return string.IsNullOrWhiteSpace(customCategory) ? DefaultCategory : customCategory.Trim();
        }

        private static bool ContainsPendingBuildable(BuildableData buildableData)
        {
            for (int i = 0; i < _pendingBuildables.Count; i++)
            {
                PendingBuildableRegistration pending = _pendingBuildables[i];
                if (ReferenceEquals(pending.Data, buildableData))
                    return true;

                if (pending.Data != null &&
                    string.Equals(pending.Data.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasPendingAliasConflict(BuildableData buildableData, out string error)
        {
            error = null;

            string persistentId = buildableData.PersistentId;
            string legacyAlias = buildableData.name;

            for (int i = 0; i < _pendingBuildables.Count; i++)
            {
                BuildableData pendingData = _pendingBuildables[i].Data;
                if (pendingData == null || ReferenceEquals(pendingData, buildableData))
                    continue;

                if (string.Equals(pendingData.PersistentId, persistentId, System.StringComparison.Ordinal))
                {
                    error = $"PersistentId '{persistentId}' already belongs to '{pendingData.name}'.";
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(legacyAlias) &&
                    string.Equals(pendingData.name, legacyAlias, System.StringComparison.Ordinal))
                {
                    error = $"Legacy alias '{legacyAlias}' already belongs to '{pendingData.name}'.";
                    return true;
                }
            }

            return false;
        }
    }

    internal static class ModRecycleRegistry
    {
        internal static bool TryRegister(string itemId, IList<ResourceStack> yield, out string error)
        {
            return RecyclingRegistry.TryRegister(itemId, yield, out error);
        }
    }

    internal static class ModEcosystemRegistry
    {
        // COLD ALLOC: List<FaunaBiomeMutationDefinition>[16] - runtime-only biome mutation overlay registry - owner: ModEcosystemRegistry
        private static readonly List<FaunaBiomeMutationDefinition> _runtimeMutations = new List<FaunaBiomeMutationDefinition>(16);

        internal static int Count => _runtimeMutations.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeMutations.Clear();
        }

        internal static bool TryRegister(FaunaBiomeMutationDefinition definition, out string error)
        {
            error = null;

            if (definition == null)
            {
                error = "Mutation definition is null.";
                return false;
            }

            if (definition.BiomeId <= 0)
            {
                error = "BiomeId must be greater than zero.";
                return false;
            }

            if (definition.MinScaleMultiplier <= 0f || definition.MaxScaleMultiplier <= 0f)
            {
                error = "Scale multipliers must be greater than zero.";
                return false;
            }

            if (definition.MaxScaleMultiplier < definition.MinScaleMultiplier)
            {
                error = "MaxScaleMultiplier must be greater than or equal to MinScaleMultiplier.";
                return false;
            }

            if (definition.SpeedMultiplier <= 0f)
            {
                error = "SpeedMultiplier must be greater than zero.";
                return false;
            }

            if (definition.HealthMultiplier <= 0f)
            {
                error = "HealthMultiplier must be greater than zero.";
                return false;
            }

            if (ContainsMatchingDefinition(definition))
                return true;

            _runtimeMutations.Add(CloneDefinition(definition));
            return true;
        }

        internal static FaunaBiomeMutationDefinition GetAt(int index)
        {
            if ((uint)index >= (uint)_runtimeMutations.Count)
                return null;

            return _runtimeMutations[index];
        }

        private static bool ContainsMatchingDefinition(FaunaBiomeMutationDefinition definition)
        {
            for (int i = 0; i < _runtimeMutations.Count; i++)
            {
                FaunaBiomeMutationDefinition existing = _runtimeMutations[i];
                if (existing == null)
                    continue;

                if (existing.BiomeId != definition.BiomeId)
                    continue;

                if (!string.Equals(existing.SpeciesId ?? string.Empty, definition.SpeciesId ?? string.Empty, System.StringComparison.Ordinal))
                    continue;

                if (Mathf.Abs(existing.MinScaleMultiplier - definition.MinScaleMultiplier) > 0.0001f)
                    continue;

                if (Mathf.Abs(existing.MaxScaleMultiplier - definition.MaxScaleMultiplier) > 0.0001f)
                    continue;

                if (Mathf.Abs(existing.SpeedMultiplier - definition.SpeedMultiplier) > 0.0001f)
                    continue;

                if (Mathf.Abs(existing.HealthMultiplier - definition.HealthMultiplier) > 0.0001f)
                    continue;

                return true;
            }

            return false;
        }

        private static FaunaBiomeMutationDefinition CloneDefinition(FaunaBiomeMutationDefinition definition)
        {
            return new FaunaBiomeMutationDefinition
            {
                BiomeId = definition.BiomeId,
                SpeciesId = definition.SpeciesId ?? string.Empty,
                MinScaleMultiplier = definition.MinScaleMultiplier,
                MaxScaleMultiplier = definition.MaxScaleMultiplier,
                SpeedMultiplier = definition.SpeedMultiplier,
                HealthMultiplier = definition.HealthMultiplier
            };
        }
    }
}
