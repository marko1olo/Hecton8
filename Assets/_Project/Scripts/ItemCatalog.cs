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
        [Header("All item assets in the project")]
        [SerializeField] private List<ItemData> allItems = new List<ItemData>();

        /// <summary>
        /// Словарь: stable ID / legacy asset name → ItemData. Строится один раз в OnEnable.
        /// Используется для O(1) поиска при загрузке инвентаря и обратной совместимости старых save.
        /// </summary>
        private Dictionary<string, ItemData> _lookup;
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

            if (_runtimeItems == null)
                _runtimeItems = new List<ItemData>(16); // COLD ALLOC: List<ItemData>[16] — runtime-only mod item overlay — owner: ItemCatalog

            _runtimeItems.Add(item);
            AddLookupAlias(persistentId, item);
            AddLookupAlias(legacyAlias, item);
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
