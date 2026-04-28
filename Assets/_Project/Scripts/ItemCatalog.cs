// ============================================================================
// HECTON-8 — ItemCatalog.cs
// Каталог всех ItemData в игре. Нужен для save/load:
// сохраняем string ID → загружаем → ищем ItemData по ID.
//
// ScriptableObject. Заполняется вручную или автоматически
// через Editor-скрипт, собирающий все ItemData из проекта.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.SaveSystem
{
    [CreateAssetMenu(
        fileName = "ItemCatalog",
        menuName = "Hecton/Item Catalog",
        order    = 100)]
    public sealed class ItemCatalog : ScriptableObject
    {
        public readonly struct ItemRuntimeDescriptor
        {
            public readonly int HashId;
            public readonly byte Width;
            public readonly byte Height;
            public readonly ushort MaxStack;
            public readonly float Weight;
            public readonly byte CategoryId;
            public readonly bool Stackable;
            public readonly bool IsConsumable;
            public readonly float OxygenRestore;
            public readonly float EnergyRestore;
            public readonly float IntegrityRestore;
            public readonly float HungerRestore;
            public readonly float ThirstRestore;
            public readonly float UseDuration;

            public ItemRuntimeDescriptor(
                int hashId,
                byte width,
                byte height,
                ushort maxStack,
                float weight,
                byte categoryId,
                bool stackable,
                bool isConsumable,
                float oxygenRestore,
                float energyRestore,
                float integrityRestore,
                float hungerRestore,
                float thirstRestore,
                float useDuration)
            {
                HashId = hashId;
                Width = width;
                Height = height;
                MaxStack = maxStack;
                Weight = weight;
                CategoryId = categoryId;
                Stackable = stackable;
                IsConsumable = isConsumable;
                OxygenRestore = oxygenRestore;
                EnergyRestore = energyRestore;
                IntegrityRestore = integrityRestore;
                HungerRestore = hungerRestore;
                ThirstRestore = thirstRestore;
                UseDuration = useDuration;
            }

            public bool IsValid => HashId != 0 && Width > 0 && Height > 0;
        }

        [Header("All item assets in the project")]
        [SerializeField] private List<ItemData> allItems = new List<ItemData>();

        /// <summary>
        /// Словарь: stable ID / legacy asset name → ItemData. Строится один раз в OnEnable.
        /// Используется для O(1) поиска при загрузке инвентаря и обратной совместимости старых save.
        /// </summary>
        private Dictionary<string, ItemData> _lookup;
        private Dictionary<int, ItemData> _hashLookup;
        private Dictionary<int, ItemRuntimeDescriptor> _runtimeDescriptorLookup;
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;
        private List<ItemData> _runtimeItems;

        /// <summary>
        /// True when the catalog detected at least one authored or runtime alias collision.
        /// Runtime registrations should stop when this flag is true because lookup resolution is no longer deterministic.
        /// </summary>
        public bool HasLookupAmbiguity => _hasLookupAmbiguity;

        /// <summary>
        /// First recorded ambiguity summary captured while rebuilding or extending the catalog lookup.
        /// </summary>
        public string LookupAmbiguitySummary => _lookupAmbiguitySummary ?? string.Empty;

        private void OnEnable()
        {
            RebuildLookup();
        }

        /// <summary>
        /// Ищет ItemData по строковому ID. Поддерживает authored stable ID и legacy asset name.
        /// Возвращает null, если не найден.
        /// </summary>
        public ItemData FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup == null) RebuildLookup();

            _lookup.TryGetValue(id, out ItemData result);
            return result;
        }

        /// <summary>
        /// Resolves an item by the stable FNV-1a hash of its PersistentId.
        /// </summary>
        public ItemData FindByHash(int hashId)
        {
            if (hashId == 0)
                return null;

            if (_hashLookup == null)
                RebuildLookup();

            _hashLookup.TryGetValue(hashId, out ItemData result);
            return result;
        }

        public bool TryGetRuntimeDescriptor(int hashId, out ItemRuntimeDescriptor descriptor)
        {
            descriptor = default;
            if (hashId == 0)
                return false;

            if (_runtimeDescriptorLookup == null)
                RebuildLookup();

            return _runtimeDescriptorLookup != null &&
                   _runtimeDescriptorLookup.TryGetValue(hashId, out descriptor) &&
                   descriptor.IsValid;
        }

        /// <summary>
        /// Registers a runtime-only item overlay without mutating the authored ScriptableObject asset list.
        /// This is intended for mod content injection and validates stable-ID collisions before extending the live lookup.
        /// </summary>
        /// <param name="item">Runtime item asset to expose through the active catalog.</param>
        /// <param name="error">Human-readable failure reason when the registration is rejected.</param>
        /// <returns>True when the item was accepted into the runtime lookup overlay.</returns>
        public bool TryRegisterRuntimeItem(ItemData item, out string error)
        {
            error = null;

            if (item == null)
            {
                error = "ItemData is null.";
                return false;
            }

            if (_lookup == null)
                RebuildLookup();

            if (_hasLookupAmbiguity)
            {
                error = LookupAmbiguitySummary;
                return false;
            }

            string persistentId = item.PersistentId;
            if (string.IsNullOrWhiteSpace(persistentId))
            {
                error = "PersistentId is empty.";
                return false;
            }

            if (ContainsRuntimeItem(item))
                return true;

            if (HasAliasConflict(persistentId, item, out error))
                return false;

            string legacyAlias = item.name;
            if (!string.Equals(legacyAlias, persistentId, StringComparison.Ordinal) &&
                HasAliasConflict(legacyAlias, item, out error))
            {
                return false;
            }

            if (HasHashConflict(item, out error))
                return false;

            if (_runtimeItems == null)
                _runtimeItems = new List<ItemData>(16); // COLD ALLOC: List<ItemData>[16] — runtime-only mod item overlay — owner: ItemCatalog

            _runtimeItems.Add(item);
            AddLookupAlias(persistentId, item);
            AddLookupAlias(legacyAlias, item);
            AddHashLookupAlias(item);
            return !_hasLookupAmbiguity;
        }

        internal int GetAllItemsNonAlloc(List<ItemData> results)
        {
            if (results == null)
                return 0;

            results.Clear();

            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    ItemData item = allItems[i];
                    if (item != null)
                        results.Add(item);
                }
            }

            if (_runtimeItems != null)
            {
                for (int i = 0; i < _runtimeItems.Count; i++)
                {
                    ItemData item = _runtimeItems[i];
                    if (item != null)
                        results.Add(item);
                }
            }

            return results.Count;
        }

        private void RebuildLookup()
        {
            int itemCount = allItems != null ? allItems.Count : 0;
            _lookup = new Dictionary<string, ItemData>(itemCount * 2);
            _hashLookup = new Dictionary<int, ItemData>(itemCount * 2);
            _runtimeDescriptorLookup = new Dictionary<int, ItemRuntimeDescriptor>(itemCount * 2);
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;

            if (allItems == null)
                itemCount = 0;

            for (int i = 0; i < itemCount; i++)
            {
                ItemData item = allItems[i];
                if (item == null)
                    continue;

                AddLookupAlias(item.PersistentId, item);
                AddLookupAlias(item.name, item);
                AddHashLookupAlias(item);
            }

            if (_runtimeItems == null)
                return;

            for (int i = 0; i < _runtimeItems.Count; i++)
            {
                ItemData runtimeItem = _runtimeItems[i];
                if (runtimeItem == null)
                    continue;

                AddLookupAlias(runtimeItem.PersistentId, runtimeItem);
                AddLookupAlias(runtimeItem.name, runtimeItem);
                AddHashLookupAlias(runtimeItem);
            }
        }

        private void AddLookupAlias(string id, ItemData item)
        {
            if (string.IsNullOrEmpty(id) || item == null)
                return;

            if (_lookup.TryGetValue(id, out ItemData existing))
            {
                if (!ReferenceEquals(existing, item))
                {
                    RecordAmbiguity(id, existing, item);
                    Debug.LogWarning($"[ItemCatalog] Duplicate ID alias '{id}'. Skipping duplicate entry.", item);
                }

                return;
            }

            _lookup.Add(id, item);
        }

        private bool ContainsRuntimeItem(ItemData item)
        {
            if (_runtimeItems == null || item == null)
                return false;

            for (int i = 0; i < _runtimeItems.Count; i++)
            {
                if (ReferenceEquals(_runtimeItems[i], item))
                    return true;
            }

            return false;
        }

        private bool HasAliasConflict(string alias, ItemData item, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(alias))
                return false;

            if (_lookup.TryGetValue(alias, out ItemData existing) && !ReferenceEquals(existing, item))
            {
                error = $"Alias '{alias}' already belongs to '{existing.name}'.";
                return true;
            }

            return false;
        }

        private bool HasHashConflict(ItemData item, out string error)
        {
            error = null;
            if (item == null)
                return false;

            int hashId = LocHash.Compute(item.PersistentId);
            if (hashId == 0)
            {
                error = "PersistentId hash resolved to zero.";
                return true;
            }

            if (_hashLookup != null &&
                _hashLookup.TryGetValue(hashId, out ItemData existing) &&
                !ReferenceEquals(existing, item))
            {
                error = $"Hash '{hashId}' already belongs to '{existing.name}'.";
                return true;
            }

            return false;
        }

        private void RecordAmbiguity(string id, ItemData existing, ItemData duplicate)
        {
            _hasLookupAmbiguity = true;

            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existing != null ? existing.name : "null";
            string duplicateName = duplicate != null ? duplicate.name : "null";
            _lookupAmbiguitySummary =
                $"ItemCatalog alias collision on '{id}' between '{existingName}' and '{duplicateName}'.";
        }

        private void AddHashLookupAlias(ItemData item)
        {
            if (item == null)
                return;

            int hashId = LocHash.Compute(item.PersistentId);
            if (hashId == 0)
                return;

            if (_hashLookup.TryGetValue(hashId, out ItemData existing))
            {
                if (!ReferenceEquals(existing, item))
                {
                    RecordHashAmbiguity(hashId, existing, item);
                    Debug.LogWarning($"[ItemCatalog] Duplicate hash alias '{hashId}' for '{item.PersistentId}'. Skipping duplicate entry.", item);
                }

                return;
            }

            _hashLookup.Add(hashId, item);
            _runtimeDescriptorLookup.Add(hashId, BuildRuntimeDescriptor(hashId, item));
        }

        private static ItemRuntimeDescriptor BuildRuntimeDescriptor(int hashId, ItemData item)
        {
            if (hashId == 0 || item == null)
                return default;

            return new ItemRuntimeDescriptor(
                hashId,
                (byte)Mathf.Clamp(item.width, 1, byte.MaxValue),
                (byte)Mathf.Clamp(item.height, 1, byte.MaxValue),
                (ushort)Mathf.Clamp(item.maxStack, 1, ushort.MaxValue),
                item.weight,
                (byte)item.category,
                item.stackable && item.maxStack > 1,
                item.isConsumable,
                item.oxygenRestore,
                item.energyRestore,
                item.integrityRestore,
                item.hungerRestore,
                item.thirstRestore,
                item.UseDuration);
        }

        private void RecordHashAmbiguity(int hashId, ItemData existing, ItemData duplicate)
        {
            _hasLookupAmbiguity = true;

            if (!string.IsNullOrEmpty(_lookupAmbiguitySummary))
                return;

            string existingName = existing != null ? existing.name : "null";
            string duplicateName = duplicate != null ? duplicate.name : "null";
            _lookupAmbiguitySummary =
                $"ItemCatalog hash collision on '{hashId}' between '{existingName}' and '{duplicateName}'.";
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            RebuildLookup();
        }
#endif
    }
}
