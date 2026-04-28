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
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
#if UNITY_EDITOR && UNITY_ADDRESSABLES_EDITOR_EXIST
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace Hecton8.SaveSystem
{
    [CreateAssetMenu(
        fileName = "ItemCatalog",
        menuName = "Hecton/Item Catalog",
        order    = 100)]
    public sealed class ItemCatalog : ScriptableObject
    {
#if UNITY_ADDRESSABLES_EXIST
        private enum WorldPrefabLoadState : byte
        {
            Unloaded = 0,
            Loading = 1,
            Loaded = 2,
            Failed = 3
        }

        [Serializable]
        private struct WorldPrefabAddressableEntry
        {
            public int hashId;
            public string persistentId;
            public AssetReferenceGameObject prefabReference;
        }

        private struct WorldPrefabRuntimeRecord
        {
            public AssetReferenceGameObject PrefabReference;
            public AsyncOperationHandle<GameObject> Handle;
            public WorldPrefabLoadState LoadState;
        }

        private readonly struct WorldPrefabGuidFallbackEntry
        {
            public readonly int HashId;
            public readonly string PersistentId;
            public readonly string Guid;

            public WorldPrefabGuidFallbackEntry(string persistentId, string guid)
            {
                PersistentId = persistentId;
                Guid = guid;
                HashId = string.IsNullOrWhiteSpace(persistentId) ? 0 : LocHash.Compute(persistentId);
            }
        }

        private const string WorldHeroPropsGroupName = "World_HeroProps";

        private static readonly WorldPrefabGuidFallbackEntry[] _worldPrefabGuidFallbacks =
        {
            new WorldPrefabGuidFallbackEntry("Item_Tool_BeaconDeployer", "d174d546f879a4742bc018eb043e67b7"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Builder", "a9d920f69f572794da38a80172350742"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_EnvAnalyzer", "f31fbadc22133c74a9c4e0dafbec547e"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Flashlight", "40a67b632626b2b4ca1b22462448c725"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_HarpoonLauncher", "2f2aaf08a7039d74ab54a9f41530b73c"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Knife", "774f5752cc67c7f49916466b60350a64"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_LaserCutter", "5d6d90d471f7ea44291faf2907d11145"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Propulsion", "f9ee01257418ed74696850470ef62d20"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Repair", "fd6fc0a78e6568b4e972561e8b888d34"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_SalvageSampler", "fa20e563eef211a4daf00fe5b0ca6412"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_Scanner", "48435f04343913447adc3ca4573951fc"),
            new WorldPrefabGuidFallbackEntry("Item_Tool_StunPistol", "1cedfa8d3d2816f48afce0afcdbdc9c0")
        };
#endif

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
#if UNITY_ADDRESSABLES_EXIST
        [Header("Addressable world prefabs keyed by item hash")]
        [SerializeField] private List<WorldPrefabAddressableEntry> worldPrefabAddressables = new List<WorldPrefabAddressableEntry>();
#endif

        /// <summary>
        /// Словарь: stable ID / legacy asset name → ItemData. Строится один раз в OnEnable.
        /// Используется для O(1) поиска при загрузке инвентаря и обратной совместимости старых save.
        /// </summary>
        private Dictionary<string, ItemData> _lookup;
        private Dictionary<int, ItemData> _hashLookup;
        private Dictionary<int, ItemRuntimeDescriptor> _runtimeDescriptorLookup;
#if UNITY_ADDRESSABLES_EXIST
        private Dictionary<int, AssetReferenceGameObject> _worldPrefabReferenceLookup;
        private Dictionary<int, WorldPrefabRuntimeRecord> _worldPrefabRuntimeLookup;
        private Queue<int> _pendingWorldPrefabReleaseQueue;
        private HashSet<int> _pendingWorldPrefabReleaseSet;
#endif
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
            RebuildWorldPrefabLookup();
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

        public bool QueueWorldPrefabPrewarm(int hashId)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return FindByHash(hashId)?.worldPrefab != null;
#else
            if (hashId == 0)
                return false;

            if (_worldPrefabReferenceLookup == null || _worldPrefabRuntimeLookup == null)
                RebuildWorldPrefabLookup();

            if (_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
            {
                if (runtimeRecord.LoadState == WorldPrefabLoadState.Loaded)
                    return runtimeRecord.Handle.IsValid() && runtimeRecord.Handle.Result != null;

                if (runtimeRecord.LoadState == WorldPrefabLoadState.Loading)
                    return true;

                if (runtimeRecord.LoadState == WorldPrefabLoadState.Failed)
                    return false;
            }

            if (_worldPrefabReferenceLookup == null ||
                !_worldPrefabReferenceLookup.TryGetValue(hashId, out AssetReferenceGameObject prefabReference) ||
                prefabReference == null ||
                !prefabReference.RuntimeKeyIsValid())
            {
                return false;
            }

            AsyncOperationHandle<GameObject> handle = prefabReference.LoadAssetAsync<GameObject>();
            _worldPrefabRuntimeLookup[hashId] = new WorldPrefabRuntimeRecord
            {
                PrefabReference = prefabReference,
                Handle = handle,
                LoadState = WorldPrefabLoadState.Loading
            };

            return true;
#endif
        }

        public void QueueWorldPrefabPrewarmNonAlloc(List<int> hashIds)
        {
            if (hashIds == null)
                return;

            for (int i = 0; i < hashIds.Count; i++)
                QueueWorldPrefabPrewarm(hashIds[i]);
        }

        public bool TryGetLoadedWorldPrefab(int hashId, out GameObject prefab)
        {
#if !UNITY_ADDRESSABLES_EXIST
            ItemData item = FindByHash(hashId);
            prefab = item != null ? item.worldPrefab : null;
            return prefab != null;
#else
            prefab = null;
            if (hashId == 0)
                return false;

            if (_worldPrefabRuntimeLookup == null)
                RebuildWorldPrefabLookup();

            if (_worldPrefabRuntimeLookup == null || !_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
                return false;

            if (!runtimeRecord.Handle.IsValid())
            {
                runtimeRecord.LoadState = WorldPrefabLoadState.Failed;
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                return false;
            }

            if (runtimeRecord.LoadState == WorldPrefabLoadState.Loading)
            {
                if (!runtimeRecord.Handle.IsDone)
                    return false;

                if (runtimeRecord.Handle.Status != AsyncOperationStatus.Succeeded || runtimeRecord.Handle.Result == null)
                {
                    runtimeRecord.LoadState = WorldPrefabLoadState.Failed;
                    _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                    return false;
                }

                runtimeRecord.LoadState = WorldPrefabLoadState.Loaded;
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
            }

            if (runtimeRecord.LoadState != WorldPrefabLoadState.Loaded || runtimeRecord.Handle.Result == null)
                return false;

            prefab = runtimeRecord.Handle.Result;
            return prefab != null;
#endif
        }

        public bool AreWorldPrefabsReadyNonAlloc(List<int> hashIds)
        {
            if (hashIds == null || hashIds.Count <= 0)
                return true;

            for (int i = 0; i < hashIds.Count; i++)
            {
                if (!TryGetLoadedWorldPrefab(hashIds[i], out _))
                    return false;
            }

            return true;
        }

        public void ReleaseAllWorldPrefabHandles()
        {
#if !UNITY_ADDRESSABLES_EXIST
            return;
#else
            if (_worldPrefabRuntimeLookup == null || _worldPrefabRuntimeLookup.Count == 0)
                return;

            Dictionary<int, WorldPrefabRuntimeRecord>.Enumerator enumerator = _worldPrefabRuntimeLookup.GetEnumerator();
            while (enumerator.MoveNext())
            {
                QueueWorldPrefabRelease(enumerator.Current.Key);
            }

            enumerator.Dispose();
#endif
        }

        public void QueueWorldPrefabRelease(int hashId)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return;
#else
            if (hashId == 0)
                return;

            if (_worldPrefabRuntimeLookup == null || !_worldPrefabRuntimeLookup.ContainsKey(hashId))
                return;

            _pendingWorldPrefabReleaseQueue ??= new Queue<int>(16); // COLD ALLOC: Queue<int>[16] - deferred Addressables release queue for world prefabs - owner: ItemCatalog
            _pendingWorldPrefabReleaseSet ??= new HashSet<int>(); // COLD ALLOC: HashSet<int>[16] - dedupe guard for deferred Addressables release queue - owner: ItemCatalog
            if (_pendingWorldPrefabReleaseSet.Add(hashId))
                _pendingWorldPrefabReleaseQueue.Enqueue(hashId);
#endif
        }

        public void QueueWorldPrefabReleaseNonAlloc(List<int> hashIds)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return;
#else
            if (hashIds == null)
                return;

            for (int i = 0; i < hashIds.Count; i++)
                QueueWorldPrefabRelease(hashIds[i]);
#endif
        }

        public void DrainDeferredWorldPrefabReleases(int maxReleaseCount)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return;
#else
            if (_pendingWorldPrefabReleaseQueue == null ||
                _pendingWorldPrefabReleaseSet == null ||
                _worldPrefabRuntimeLookup == null ||
                _pendingWorldPrefabReleaseQueue.Count <= 0)
            {
                return;
            }

            int releaseBudget = maxReleaseCount <= 0 ? int.MaxValue : maxReleaseCount;
            while (releaseBudget-- > 0 && _pendingWorldPrefabReleaseQueue.Count > 0)
            {
                int hashId = _pendingWorldPrefabReleaseQueue.Dequeue();
                _pendingWorldPrefabReleaseSet.Remove(hashId);

                if (!_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
                    continue;

                if (runtimeRecord.Handle.IsValid())
                    Addressables.Release(runtimeRecord.Handle);

                _worldPrefabRuntimeLookup.Remove(hashId);
            }
#endif
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

        private void RebuildWorldPrefabLookup()
        {
#if !UNITY_ADDRESSABLES_EXIST
            return;
#else
            int entryCount = worldPrefabAddressables != null ? worldPrefabAddressables.Count : 0;
            _worldPrefabReferenceLookup = new Dictionary<int, AssetReferenceGameObject>(Math.Max(16, entryCount));

            if (_worldPrefabRuntimeLookup == null)
                _worldPrefabRuntimeLookup = new Dictionary<int, WorldPrefabRuntimeRecord>(Math.Max(16, entryCount));
            else
                _worldPrefabRuntimeLookup.Clear();

            for (int i = 0; i < entryCount; i++)
            {
                WorldPrefabAddressableEntry entry = worldPrefabAddressables[i];
                if (entry.hashId == 0 || entry.prefabReference == null || !entry.prefabReference.RuntimeKeyIsValid())
                    continue;

                _worldPrefabReferenceLookup[entry.hashId] = entry.prefabReference;
            }

            for (int i = 0; i < _worldPrefabGuidFallbacks.Length; i++)
            {
                WorldPrefabGuidFallbackEntry fallback = _worldPrefabGuidFallbacks[i];
                if (fallback.HashId == 0 ||
                    string.IsNullOrWhiteSpace(fallback.Guid) ||
                    _worldPrefabReferenceLookup.ContainsKey(fallback.HashId))
                {
                    continue;
                }

                AssetReferenceGameObject fallbackReference = new AssetReferenceGameObject(fallback.Guid);
                if (fallbackReference.RuntimeKeyIsValid())
                    _worldPrefabReferenceLookup.Add(fallback.HashId, fallbackReference);
            }
#endif
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
#if UNITY_ADDRESSABLES_EDITOR_EXIST
            SyncAddressableWorldPrefabEntries();
#endif
            RebuildWorldPrefabLookup();
        }

#if UNITY_ADDRESSABLES_EDITOR_EXIST
        private void SyncAddressableWorldPrefabEntries()
        {
            if (allItems == null)
                return;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            bool mutated = false;

            if (worldPrefabAddressables == null)
                worldPrefabAddressables = new List<WorldPrefabAddressableEntry>(allItems.Count);

            for (int i = 0; i < allItems.Count; i++)
            {
                ItemData item = allItems[i];
                if (item == null || item.worldPrefab == null || string.IsNullOrWhiteSpace(item.PersistentId))
                    continue;

                int hashId = LocHash.Compute(item.PersistentId);
                if (hashId == 0)
                    continue;

                string assetPath = AssetDatabase.GetAssetPath(item.worldPrefab);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid) && !TryResolveWorldPrefabGuidFallback(hashId, out guid))
                    continue;

                if (settings != null)
                {
                    AddressableAssetGroup targetGroup = ResolveOrCreateWorldPrefabGroup(settings, item.PersistentId);
                    if (targetGroup != null)
                    {
                        settings.CreateOrMoveEntry(guid, targetGroup);
                        mutated = true;
                    }
                }

                AssetReferenceGameObject prefabReference = new AssetReferenceGameObject(guid);
                int existingIndex = FindWorldPrefabEntryIndex(hashId);
                WorldPrefabAddressableEntry nextEntry = new WorldPrefabAddressableEntry
                {
                    hashId = hashId,
                    persistentId = item.PersistentId,
                    prefabReference = prefabReference
                };

                if (existingIndex >= 0)
                {
                    worldPrefabAddressables[existingIndex] = nextEntry;
                }
                else
                {
                    worldPrefabAddressables.Add(nextEntry);
                }

                mutated = true;
            }

            if (mutated)
                EditorUtility.SetDirty(this);
        }

        private int FindWorldPrefabEntryIndex(int hashId)
        {
            if (worldPrefabAddressables == null)
                return -1;

            for (int i = 0; i < worldPrefabAddressables.Count; i++)
            {
                if (worldPrefabAddressables[i].hashId == hashId)
                    return i;
            }

            return -1;
        }

        private static bool TryResolveWorldPrefabGuidFallback(int hashId, out string guid)
        {
            guid = null;
            if (hashId == 0)
                return false;

            for (int i = 0; i < _worldPrefabGuidFallbacks.Length; i++)
            {
                WorldPrefabGuidFallbackEntry fallback = _worldPrefabGuidFallbacks[i];
                if (fallback.HashId != hashId || string.IsNullOrWhiteSpace(fallback.Guid))
                    continue;

                guid = fallback.Guid;
                return true;
            }

            return false;
        }

        private static AddressableAssetGroup ResolveOrCreateWorldPrefabGroup(AddressableAssetSettings settings, string persistentId)
        {
            if (settings == null)
                return null;

            string groupName = ResolveWorldPrefabGroupName(persistentId);
            if (string.IsNullOrWhiteSpace(groupName))
                return settings.DefaultGroup;

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
                return group;

            return settings.CreateGroup(groupName, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
        }

        private static string ResolveWorldPrefabGroupName(string persistentId)
        {
            if (string.IsNullOrWhiteSpace(persistentId))
                return WorldHeroPropsGroupName;

            if (persistentId.StartsWith("Item_Tool_", StringComparison.Ordinal))
                return WorldHeroPropsGroupName;

            return WorldHeroPropsGroupName;
        }
#endif
#endif
    }
}
