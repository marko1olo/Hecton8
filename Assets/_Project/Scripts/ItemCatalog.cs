// ============================================================================
// HECTON-8 — ItemCatalog.cs
// Katalog vseh ItemData v igre. Nuzhen dlya save/load:
// sohranyaem string ID → zagruzhaem → ischem ItemData po ID.
//
// ScriptableObject. Zapolnyaetsya vruchnuyu ili avtomaticheski
// cherez Editor-skript, sobirayuschiy vse ItemData iz proekta.
// ============================================================================

using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Optimization;
using Hecton8.World;
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
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace Hecton8.SaveSystem
{
    [CreateAssetMenu(
        fileName = "ItemCatalog",
        menuName = "Hecton/Item Catalog",
        order    = 100)]
    public sealed class ItemCatalog : ScriptableObject, IGlobalRegistryHotSwapListener
    {
#if UNITY_ADDRESSABLES_EXIST
        private enum WorldPrefabLoadState : byte
        {
            Unloaded = 0,
            Queued = 1,
            Loading = 2,
            Loaded = 3,
            Failed = 4
        }

        [Serializable]
#pragma warning disable 0649 // Unity serializes this authoring payload; fields are assigned from catalog assets.
        private struct WorldPrefabAddressableEntry
        {
            public int hashId;
            public string persistentId;
            public AssetReferenceGameObject prefabReference;
        }
#pragma warning restore 0649

        private struct WorldPrefabRuntimeRecord
        {
            public AssetReferenceGameObject PrefabReference;
            public AsyncOperationHandle<GameObject> Handle;
            public WorldPrefabLoadState LoadState;
            public int LastAccessFrame;
            public int DispatchRequestId;
            public uint DispatchAssetKey;
            public AbsoluteUniversePosition LastAccessAup;
            public bool HasLastAccessAup;
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
            public readonly ushort StateFlags;
            public readonly float Weight;
            public readonly byte CategoryId;
            public readonly uint VulnerabilityMask;
            public readonly byte AudioMaterialId;
            public readonly byte PhysicsMaterialTag;
            public readonly float MassKg;
            public readonly float VolumeM3;
            public readonly float RadiationSvPerSecond;
            public readonly byte Stackable;
            public readonly byte IsConsumable;
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
                ushort stateFlags,
                float weight,
                byte categoryId,
                uint vulnerabilityMask,
                byte audioMaterialId,
                byte physicsMaterialTag,
                float massKg,
                float volumeM3,
                float radiationSvPerSecond,
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
                StateFlags = stateFlags;
                Weight = weight;
                CategoryId = categoryId;
                VulnerabilityMask = vulnerabilityMask;
                AudioMaterialId = audioMaterialId;
                PhysicsMaterialTag = physicsMaterialTag;
                MassKg = massKg;
                VolumeM3 = volumeM3;
                RadiationSvPerSecond = radiationSvPerSecond;
                Stackable = stackable ? (byte)1 : (byte)0;
                IsConsumable = isConsumable ? (byte)1 : (byte)0;
                OxygenRestore = oxygenRestore;
                EnergyRestore = energyRestore;
                IntegrityRestore = integrityRestore;
                HungerRestore = hungerRestore;
                ThirstRestore = thirstRestore;
                UseDuration = useDuration;
            }

        }

        public static bool IsValidDescriptor(in ItemRuntimeDescriptor descriptor)
        {
            return descriptor.HashId != 0 && descriptor.Width > 0 && descriptor.Height > 0;
        }

        [Header("All item assets in the project")]
        [SerializeField] private List<ItemData> allItems = new List<ItemData>(128);
#if UNITY_ADDRESSABLES_EXIST
        [Header("Addressable world prefabs keyed by item hash")]
        [SerializeField] private List<WorldPrefabAddressableEntry> worldPrefabAddressables = new List<WorldPrefabAddressableEntry>(64);
#endif

        /// <summary>
        /// Slovar: stable ID / legacy asset name → ItemData. Stroitsya odin raz v OnEnable.
        /// Ispolzuetsya dlya O(1) poiska pri zagruzke inventarya i obratnoy sovmestimosti staryh save.
        /// </summary>
        private Dictionary<string, ItemData> _lookup;
        private Dictionary<int, ItemData> _hashLookup;
        private Dictionary<int, ItemRuntimeDescriptor> _runtimeDescriptorLookup;
#if UNITY_ADDRESSABLES_EXIST
        private Dictionary<int, AssetReferenceGameObject> _worldPrefabReferenceLookup;
        private Dictionary<int, WorldPrefabRuntimeRecord> _worldPrefabRuntimeLookup;
        // COLD ALLOC: int[] fixed ring - staged world-prefab releases - owner: ItemCatalog
        private int[] _pendingWorldPrefabReleaseRing;
        private int _pendingWorldPrefabReleaseHead;
        private int _pendingWorldPrefabReleaseTail;
        private int _pendingWorldPrefabReleaseCount;
        // COLD ALLOC: int[] fixed scratch - staged world-prefab dispatch claims from AssetLoadDispatcher - owner: ItemCatalog
        private int[] _worldPrefabDispatchScratch;
        private int _worldPrefabDispatchScratchCount;
#endif
        private bool _hasLookupAmbiguity;
        private string _lookupAmbiguitySummary;
        private List<ItemData> _runtimeItems;
        private bool _registeredHotSwap;
        private IQuestSystem _cachedQuestSystem;
#if UNITY_ADDRESSABLES_EXIST
        private AssetLifecycleGovernor _cachedAssetLifecycleGovernor;
        private AssetLoadDispatcher _cachedAssetLoadDispatcher;
        private IPlayerRuntimeContext _cachedPlayerContext;
#endif
        private const int DefaultWorldPrefabLruIdleFrames = 180;
        private const int DeferredWorldPrefabReleaseCapacity = 16;
        private const int DefaultWorldPrefabDispatchScratchCapacity = 32;

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
            CacheQuestSystemCold();
            CacheAddressableRuntimeServicesCold();
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
#if UNITY_ADDRESSABLES_EXIST
            if (Application.isPlaying)
            {
                ReleaseAllWorldPrefabHandles();
                DrainDeferredWorldPrefabReleases(0);
            }
#endif
            TryUnregisterHotSwap();
            ClearCachedRuntimeServices();
        }

        /// <summary>
        /// Ischet ItemData po strokovomu ID. Podderzhivaet authored stable ID i legacy asset name.
        /// Vozvraschaet null, esli ne nayden.
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
                   IsValidDescriptor(in descriptor);
        }

        public bool QueueWorldPrefabPrewarm(int hashId)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return FindByHash(hashId)?.worldPrefab != null;
#else
            if (hashId == 0)
                return false;

            if (!TryEnsureWorldPrefabLookupReady())
                return TryReadDirectWorldPrefabFallback(hashId, out _);

            if (_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
            {
                runtimeRecord.LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                CaptureCurrentPlayerAup(ref runtimeRecord);
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;

                if (runtimeRecord.LoadState == WorldPrefabLoadState.Loaded)
                    return runtimeRecord.Handle.IsValid() && runtimeRecord.Handle.Result != null;

                if (runtimeRecord.LoadState == WorldPrefabLoadState.Queued)
                    return true;

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
                return TryReadDirectWorldPrefabFallback(hashId, out _);
            }

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            uint dispatchAssetKey = BuildWorldPrefabDispatchKey(hashId);
            if (dispatcher != null &&
                dispatcher.Enqueue(dispatchAssetKey, AssetPriorityTier.Tier2Proximity, false, out int requestId))
            {
                WorldPrefabRuntimeRecord queuedRecord = new WorldPrefabRuntimeRecord
                {
                    PrefabReference = prefabReference,
                    LoadState = WorldPrefabLoadState.Queued,
                    LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                    DispatchRequestId = requestId,
                    DispatchAssetKey = dispatchAssetKey
                };
                CaptureCurrentPlayerAup(ref queuedRecord);
                _worldPrefabRuntimeLookup[hashId] = queuedRecord;
                return true;
            }

            if (!TryAcquireWorldPrefabHandle(dispatchAssetKey, prefabReference, out AsyncOperationHandle<GameObject> handle))
                return TryReadDirectWorldPrefabFallback(hashId, out _);

            WorldPrefabRuntimeRecord loadingRecord = new WorldPrefabRuntimeRecord
            {
                PrefabReference = prefabReference,
                Handle = handle,
                LoadState = WorldPrefabLoadState.Loading,
                LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                DispatchRequestId = 0,
                DispatchAssetKey = dispatchAssetKey
            };
            CaptureCurrentPlayerAup(ref loadingRecord);
            _worldPrefabRuntimeLookup[hashId] = loadingRecord;

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
            return TryReadDirectWorldPrefabFallback(hashId, out prefab);
#else
            prefab = null;
            if (hashId == 0)
                return false;

            if (_worldPrefabRuntimeLookup != null &&
                _worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord) &&
                runtimeRecord.LoadState == WorldPrefabLoadState.Loaded &&
                runtimeRecord.Handle.IsValid() &&
                runtimeRecord.Handle.Result != null)
            {
                prefab = runtimeRecord.Handle.Result;
                return true;
            }

            return TryReadDirectWorldPrefabFallback(hashId, out prefab);
#endif
        }

        public bool PollLoadedWorldPrefab(int hashId, out GameObject prefab)
        {
            return PollLoadedWorldPrefab(hashId, out prefab, true);
        }

        private bool PollLoadedWorldPrefab(int hashId, out GameObject prefab, bool pumpDispatchTickets)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return TryGetLoadedWorldPrefab(hashId, out prefab);
#else
            prefab = null;
            if (hashId == 0)
                return false;

            if (!TryEnsureWorldPrefabLookupReady())
                return TryReadDirectWorldPrefabFallback(hashId, out prefab);

            if (pumpDispatchTickets)
                PumpWorldPrefabDispatchTickets();

            if (_worldPrefabRuntimeLookup == null || !_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
                return TryReadDirectWorldPrefabFallback(hashId, out prefab);

            runtimeRecord.LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            CaptureCurrentPlayerAup(ref runtimeRecord);

            if (runtimeRecord.LoadState == WorldPrefabLoadState.Queued)
            {
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                return false;
            }

            if (!runtimeRecord.Handle.IsValid())
            {
                FailWorldPrefabLoad(hashId, ref runtimeRecord);
                return TryReadDirectWorldPrefabFallback(hashId, out prefab);
            }

            if (runtimeRecord.LoadState == WorldPrefabLoadState.Loading)
            {
                if (!runtimeRecord.Handle.IsDone)
                {
                    _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                    return false;
                }

                if (runtimeRecord.Handle.Status != AsyncOperationStatus.Succeeded || runtimeRecord.Handle.Result == null)
                {
                    FailWorldPrefabLoad(hashId, ref runtimeRecord);
                    return TryReadDirectWorldPrefabFallback(hashId, out prefab);
                }

                CompleteWorldPrefabDispatch(ref runtimeRecord, success: true);
                MarkWorldPrefabLoaded(ref runtimeRecord);
                runtimeRecord.LoadState = WorldPrefabLoadState.Loaded;
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
            }

            if (runtimeRecord.LoadState != WorldPrefabLoadState.Loaded || runtimeRecord.Handle.Result == null)
                return false;

            prefab = runtimeRecord.Handle.Result;
            _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
            return prefab != null;
#endif
        }

        private bool TryReadDirectWorldPrefabFallback(int hashId, out GameObject prefab)
        {
            prefab = null;
            if (hashId == 0)
                return false;

            if (_hashLookup != null &&
                _hashLookup.TryGetValue(hashId, out ItemData cachedItem) &&
                cachedItem != null)
            {
                prefab = cachedItem.worldPrefab;
                return prefab != null;
            }

            return TryReadDirectWorldPrefabFallbackLinear(hashId, out prefab);
        }

        private bool TryReadDirectWorldPrefabFallbackLinear(int hashId, out GameObject prefab)
        {
            prefab = null;
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    ItemData item = allItems[i];
                    if (item != null && item.MatchesPersistentHash(hashId))
                    {
                        prefab = item.worldPrefab;
                        return prefab != null;
                    }
                }
            }

            if (_runtimeItems != null)
            {
                for (int i = 0; i < _runtimeItems.Count; i++)
                {
                    ItemData item = _runtimeItems[i];
                    if (item != null && item.MatchesPersistentHash(hashId))
                    {
                        prefab = item.worldPrefab;
                        return prefab != null;
                    }
                }
            }

            return false;
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

        public bool PollWorldPrefabsReadyNonAlloc(List<int> hashIds)
        {
            if (hashIds == null || hashIds.Count <= 0)
                return true;

#if UNITY_ADDRESSABLES_EXIST
            PumpWorldPrefabDispatchTickets();
#endif

            for (int i = 0; i < hashIds.Count; i++)
            {
                if (!PollLoadedWorldPrefab(hashIds[i], out _, false))
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

            TryEnqueuePendingWorldPrefabRelease(hashId);
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
            if (_pendingWorldPrefabReleaseRing == null ||
                _worldPrefabRuntimeLookup == null ||
                _pendingWorldPrefabReleaseCount <= 0)
            {
                return;
            }

            int releaseBudget = maxReleaseCount <= 0 ? int.MaxValue : maxReleaseCount;
            int initialPendingCount = _pendingWorldPrefabReleaseCount;
            int processedCount = 0;
            while (releaseBudget-- > 0 &&
                   processedCount++ < initialPendingCount &&
                   TryDequeuePendingWorldPrefabRelease(out int hashId))
            {
                if (!_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord))
                    continue;

                if (!ReleaseWorldPrefabRuntimeRecord(hashId, runtimeRecord))
                    TryEnqueuePendingWorldPrefabRelease(hashId);
            }
#endif
        }

        public int EvictLeastRecentlyUsedWorldPrefabs(int maxReleaseCount, int minUnusedFrames = DefaultWorldPrefabLruIdleFrames)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return 0;
#else
            if (maxReleaseCount <= 0 || _worldPrefabRuntimeLookup == null || _worldPrefabRuntimeLookup.Count <= 0)
                return 0;

            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            int minimumIdleFrames = minUnusedFrames > 0 ? minUnusedFrames : DefaultWorldPrefabLruIdleFrames;
            int evictedCount = 0;

            while (evictedCount < maxReleaseCount)
            {
                bool foundCandidate = false;
                int candidateHashId = 0;
                int oldestAccessFrame = int.MaxValue;
                Dictionary<int, WorldPrefabRuntimeRecord>.Enumerator enumerator = _worldPrefabRuntimeLookup.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<int, WorldPrefabRuntimeRecord> entry = enumerator.Current;
                    WorldPrefabRuntimeRecord runtimeRecord = entry.Value;
                    if (runtimeRecord.LoadState != WorldPrefabLoadState.Loaded || !runtimeRecord.Handle.IsValid())
                        continue;

                    if (currentFrame - runtimeRecord.LastAccessFrame < minimumIdleFrames)
                        continue;

                    if (!foundCandidate || runtimeRecord.LastAccessFrame < oldestAccessFrame)
                    {
                        foundCandidate = true;
                        candidateHashId = entry.Key;
                        oldestAccessFrame = runtimeRecord.LastAccessFrame;
                    }
                }

                enumerator.Dispose();

                if (!foundCandidate || !_worldPrefabRuntimeLookup.TryGetValue(candidateHashId, out WorldPrefabRuntimeRecord candidateRecord))
                    break;

                if (!ReleaseWorldPrefabRuntimeRecord(candidateHashId, candidateRecord))
                    break;

                evictedCount++;
            }

            return evictedCount;
#endif
        }

        public int EvictWorldPrefabsBeyondPlayerAup(float maxDistanceMeters, int maxReleaseCount)
        {
#if !UNITY_ADDRESSABLES_EXIST
            return 0;
#else
            if (maxDistanceMeters <= 0f || maxReleaseCount <= 0 || _worldPrefabRuntimeLookup == null || _worldPrefabRuntimeLookup.Count <= 0)
                return 0;

            if (!TryCaptureCurrentPlayerAup(out AbsoluteUniversePosition playerAup))
                return 0;

            double maxDistanceSq = (double)maxDistanceMeters * maxDistanceMeters;
            int evictedCount = 0;

            while (evictedCount < maxReleaseCount)
            {
                bool foundCandidate = false;
                int candidateHashId = 0;
                Dictionary<int, WorldPrefabRuntimeRecord>.Enumerator enumerator = _worldPrefabRuntimeLookup.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<int, WorldPrefabRuntimeRecord> entry = enumerator.Current;
                    WorldPrefabRuntimeRecord runtimeRecord = entry.Value;
                    if (runtimeRecord.LoadState != WorldPrefabLoadState.Loaded ||
                        !runtimeRecord.Handle.IsValid() ||
                        !runtimeRecord.HasLastAccessAup)
                    {
                        continue;
                    }

                    double distanceSq = AbsoluteUniversePosition.DistanceSq(in runtimeRecord.LastAccessAup, in playerAup);
                    if (distanceSq <= maxDistanceSq)
                        continue;

                    foundCandidate = true;
                    candidateHashId = entry.Key;
                    break;
                }

                enumerator.Dispose();

                if (!foundCandidate || !_worldPrefabRuntimeLookup.TryGetValue(candidateHashId, out WorldPrefabRuntimeRecord candidateRecord))
                    break;

                if (!ReleaseWorldPrefabRuntimeRecord(candidateHashId, candidateRecord))
                    break;

                evictedCount++;
            }

            return evictedCount;
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

        internal bool TryCopyAllItemsNonAlloc(List<ItemData> results, out int copiedCount)
        {
            copiedCount = 0;
            if (results == null)
                return false;

            results.Clear();
            int capacity = results.Capacity;
            if (capacity <= 0)
                return false;

            int requiredCount = CountNonNullItems(allItems) + CountNonNullItems(_runtimeItems);
            if (requiredCount > capacity)
                return false;

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

            copiedCount = results.Count;
            return true;
        }

        private static int CountNonNullItems(List<ItemData> source)
        {
            if (source == null)
                return 0;

            int count = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    count++;
            }

            return count;
        }

        private void RebuildLookup()
        {
            int authoredItemCount = allItems != null ? allItems.Count : 0;
            int runtimeItemCount = _runtimeItems != null ? _runtimeItems.Count : 0;
            int totalItemCount = authoredItemCount + runtimeItemCount;
            int stringLookupCapacity = Math.Max(16, totalItemCount * 2);
            int hashLookupCapacity = Math.Max(16, totalItemCount);

            _lookup = new Dictionary<string, ItemData>(stringLookupCapacity);
            _hashLookup = new Dictionary<int, ItemData>(hashLookupCapacity);
            _runtimeDescriptorLookup = new Dictionary<int, ItemRuntimeDescriptor>(hashLookupCapacity);
            _hasLookupAmbiguity = false;
            _lookupAmbiguitySummary = string.Empty;

            for (int i = 0; i < authoredItemCount; i++)
            {
                ItemData item = allItems[i];
                if (item == null)
                    continue;

                AddLookupAlias(item.PersistentId, item);
                AddLookupAlias(item.name, item);
                AddHashLookupAlias(item);
            }

            if (_runtimeItems == null)
            {
                ApplyRuntimeTemplateRegistrySnapshot();
                return;
            }

            for (int i = 0; i < _runtimeItems.Count; i++)
            {
                ItemData runtimeItem = _runtimeItems[i];
                if (runtimeItem == null)
                    continue;

                AddLookupAlias(runtimeItem.PersistentId, runtimeItem);
                AddLookupAlias(runtimeItem.name, runtimeItem);
                AddHashLookupAlias(runtimeItem);
            }

            ApplyRuntimeTemplateRegistrySnapshot();
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

            int fixedScratchCapacity = Math.Max(
                DeferredWorldPrefabReleaseCapacity,
                entryCount + _worldPrefabGuidFallbacks.Length);

            if (_pendingWorldPrefabReleaseRing == null || _pendingWorldPrefabReleaseRing.Length < fixedScratchCapacity)
                _pendingWorldPrefabReleaseRing = new int[fixedScratchCapacity];
            else
                Array.Clear(_pendingWorldPrefabReleaseRing, 0, _pendingWorldPrefabReleaseRing.Length);

            _pendingWorldPrefabReleaseHead = 0;
            _pendingWorldPrefabReleaseTail = 0;
            _pendingWorldPrefabReleaseCount = 0;

            int dispatchScratchCapacity = Math.Max(DefaultWorldPrefabDispatchScratchCapacity, fixedScratchCapacity);
            if (_worldPrefabDispatchScratch == null || _worldPrefabDispatchScratch.Length < dispatchScratchCapacity)
                _worldPrefabDispatchScratch = new int[dispatchScratchCapacity];
            else
                Array.Clear(_worldPrefabDispatchScratch, 0, _worldPrefabDispatchScratch.Length);

            _worldPrefabDispatchScratchCount = 0;

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

#if UNITY_ADDRESSABLES_EXIST
        private bool TryEnsureWorldPrefabLookupReady()
        {
            if (_worldPrefabReferenceLookup != null &&
                _worldPrefabRuntimeLookup != null &&
                _pendingWorldPrefabReleaseRing != null &&
                _worldPrefabDispatchScratch != null)
            {
                return true;
            }

            if (Application.isPlaying)
                return false;

            RebuildWorldPrefabLookup();
            return _worldPrefabReferenceLookup != null &&
                   _worldPrefabRuntimeLookup != null &&
                   _pendingWorldPrefabReleaseRing != null &&
                   _worldPrefabDispatchScratch != null;
        }

        public void PumpWorldPrefabDispatchTickets()
        {
            if (_worldPrefabRuntimeLookup == null || _worldPrefabRuntimeLookup.Count <= 0)
                return;

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            if (dispatcher == null)
                return;

            if (_worldPrefabDispatchScratch == null)
                return;

            _worldPrefabDispatchScratchCount = 0;

            Dictionary<int, WorldPrefabRuntimeRecord>.Enumerator enumerator = _worldPrefabRuntimeLookup.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.LoadState == WorldPrefabLoadState.Queued)
                {
                    if (_worldPrefabDispatchScratchCount >= _worldPrefabDispatchScratch.Length)
                        continue;

                    _worldPrefabDispatchScratch[_worldPrefabDispatchScratchCount++] = enumerator.Current.Key;
                }
            }

            enumerator.Dispose();

            for (int i = 0; i < _worldPrefabDispatchScratchCount; i++)
            {
                int hashId = _worldPrefabDispatchScratch[i];
                _worldPrefabDispatchScratch[i] = 0;
                if (!_worldPrefabRuntimeLookup.TryGetValue(hashId, out WorldPrefabRuntimeRecord runtimeRecord) ||
                    runtimeRecord.LoadState != WorldPrefabLoadState.Queued ||
                    runtimeRecord.DispatchAssetKey == 0u ||
                    !dispatcher.TryConsumeReadyTicketByAssetKey(runtimeRecord.DispatchAssetKey, out AssetDispatchTicket ticket))
                {
                    continue;
                }

                runtimeRecord.DispatchRequestId = ticket.RequestId;
                if (!TryAcquireWorldPrefabHandle(runtimeRecord.DispatchAssetKey, runtimeRecord.PrefabReference, out AsyncOperationHandle<GameObject> handle))
                {
                    dispatcher.AcknowledgeDispatchRequest(ticket.RequestId, false);
                    runtimeRecord.DispatchRequestId = 0;
                    runtimeRecord.LoadState = WorldPrefabLoadState.Failed;
                    _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                    continue;
                }

                runtimeRecord.Handle = handle;
                runtimeRecord.LoadState = WorldPrefabLoadState.Loading;
                runtimeRecord.LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                CaptureCurrentPlayerAup(ref runtimeRecord);
                _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
            }

            _worldPrefabDispatchScratchCount = 0;
        }

        private bool ReleaseWorldPrefabRuntimeRecord(int hashId, WorldPrefabRuntimeRecord runtimeRecord)
        {
            uint assetKey = runtimeRecord.DispatchAssetKey;
            if (runtimeRecord.Handle.IsValid())
            {
                AssetLifecycleGovernor governor = _cachedAssetLifecycleGovernor;
                if (governor != null && assetKey != 0u)
                {
                    governor.ReleaseAddressableAsset(assetKey);
                }
                else if (governor == null || !governor.TryStageExternalAddressableRelease(runtimeRecord.Handle))
                {
                    _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
                    return false;
                }
            }

            CancelPendingWorldPrefabDispatch(ref runtimeRecord);
            RemovePendingWorldPrefabRelease(hashId);
            _worldPrefabRuntimeLookup.Remove(hashId);
            return true;
        }

        private void FailWorldPrefabLoad(int hashId, ref WorldPrefabRuntimeRecord runtimeRecord)
        {
            CompleteWorldPrefabDispatch(ref runtimeRecord, success: false);
            ReleaseFailedWorldPrefabHandle(ref runtimeRecord);
            runtimeRecord.LoadState = WorldPrefabLoadState.Failed;
            _worldPrefabRuntimeLookup[hashId] = runtimeRecord;
        }

        private void ReleaseFailedWorldPrefabHandle(ref WorldPrefabRuntimeRecord runtimeRecord)
        {
            AsyncOperationHandle<GameObject> handle = runtimeRecord.Handle;
            if (!handle.IsValid())
            {
                runtimeRecord.Handle = default;
                runtimeRecord.DispatchRequestId = 0;
                runtimeRecord.DispatchAssetKey = 0u;
                return;
            }

            AssetLifecycleGovernor governor = _cachedAssetLifecycleGovernor;
            if (governor != null && runtimeRecord.DispatchAssetKey != 0u)
            {
                governor.ReleaseAddressableAsset(runtimeRecord.DispatchAssetKey);
            }
            else if (governor != null)
            {
                governor.TryReleaseExternalAddressableFault(handle);
            }
            else
            {
                Addressables.Release(handle);
            }

            runtimeRecord.Handle = default;
            runtimeRecord.DispatchRequestId = 0;
            runtimeRecord.DispatchAssetKey = 0u;
        }

        private bool TryEnqueuePendingWorldPrefabRelease(int hashId)
        {
            if (_pendingWorldPrefabReleaseRing == null ||
                _pendingWorldPrefabReleaseRing.Length == 0 ||
                hashId == 0)
            {
                return false;
            }

            int ringLength = _pendingWorldPrefabReleaseRing.Length;
            for (int i = 0; i < _pendingWorldPrefabReleaseCount; i++)
            {
                int readIndex = (_pendingWorldPrefabReleaseHead + i) % ringLength;
                if (_pendingWorldPrefabReleaseRing[readIndex] == hashId)
                    return true;
            }

            if (_pendingWorldPrefabReleaseCount >= ringLength)
                return false;

            _pendingWorldPrefabReleaseRing[_pendingWorldPrefabReleaseTail] = hashId;
            _pendingWorldPrefabReleaseTail = (_pendingWorldPrefabReleaseTail + 1) % ringLength;
            _pendingWorldPrefabReleaseCount++;
            return true;
        }

        private bool TryDequeuePendingWorldPrefabRelease(out int hashId)
        {
            hashId = 0;
            if (_pendingWorldPrefabReleaseRing == null ||
                _pendingWorldPrefabReleaseRing.Length == 0 ||
                _pendingWorldPrefabReleaseCount <= 0)
            {
                return false;
            }

            int ringLength = _pendingWorldPrefabReleaseRing.Length;
            hashId = _pendingWorldPrefabReleaseRing[_pendingWorldPrefabReleaseHead];
            _pendingWorldPrefabReleaseRing[_pendingWorldPrefabReleaseHead] = 0;
            _pendingWorldPrefabReleaseHead = (_pendingWorldPrefabReleaseHead + 1) % ringLength;
            _pendingWorldPrefabReleaseCount--;

            if (_pendingWorldPrefabReleaseCount == 0)
            {
                _pendingWorldPrefabReleaseHead = 0;
                _pendingWorldPrefabReleaseTail = 0;
            }

            return hashId != 0;
        }

        private void RemovePendingWorldPrefabRelease(int hashId)
        {
            if (_pendingWorldPrefabReleaseRing == null ||
                _pendingWorldPrefabReleaseRing.Length == 0 ||
                _pendingWorldPrefabReleaseCount <= 0 ||
                hashId == 0)
            {
                return;
            }

            int ringLength = _pendingWorldPrefabReleaseRing.Length;
            int originalCount = _pendingWorldPrefabReleaseCount;
            int writeCount = 0;
            for (int i = 0; i < originalCount; i++)
            {
                int readIndex = (_pendingWorldPrefabReleaseHead + i) % ringLength;
                int queuedHashId = _pendingWorldPrefabReleaseRing[readIndex];
                if (queuedHashId == 0 || queuedHashId == hashId)
                    continue;

                _pendingWorldPrefabReleaseRing[writeCount++] = queuedHashId;
            }

            for (int i = writeCount; i < originalCount && i < ringLength; i++)
                _pendingWorldPrefabReleaseRing[i] = 0;

            _pendingWorldPrefabReleaseHead = 0;
            _pendingWorldPrefabReleaseCount = writeCount;
            _pendingWorldPrefabReleaseTail = writeCount == ringLength ? 0 : writeCount;
        }

        private static uint BuildWorldPrefabDispatchKey(int hashId)
        {
            return unchecked((uint)hashId) ^ 0xA77E0001u;
        }

        private bool TryAcquireWorldPrefabHandle(
            uint dispatchAssetKey,
            AssetReferenceGameObject prefabReference,
            out AsyncOperationHandle<GameObject> handle)
        {
            handle = default;
            AssetLifecycleGovernor governor = _cachedAssetLifecycleGovernor;
            if (governor == null)
                return false;

            return governor.TryAcquireAddressableGameObject(
                dispatchAssetKey,
                prefabReference,
                null,
                AssetPriorityTier.Tier2Proximity,
                AssetResidencyKind.Addressable,
                0L,
                false,
                out handle,
                out _);
        }

        private void MarkWorldPrefabLoaded(ref WorldPrefabRuntimeRecord runtimeRecord)
        {
            AssetLifecycleGovernor governor = _cachedAssetLifecycleGovernor;
            if (governor == null ||
                runtimeRecord.DispatchAssetKey == 0u ||
                !runtimeRecord.Handle.IsValid() ||
                runtimeRecord.Handle.Status != AsyncOperationStatus.Succeeded)
            {
                return;
            }

            governor.MarkAddressableLoaded(
                runtimeRecord.DispatchAssetKey,
                runtimeRecord.Handle,
                runtimeRecord.Handle.Result,
                0L,
                false);
        }

        private void CancelPendingWorldPrefabDispatch(ref WorldPrefabRuntimeRecord runtimeRecord)
        {
            if (runtimeRecord.DispatchAssetKey == 0u)
                return;

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            if (dispatcher != null)
            {
                dispatcher.CancelByAssetKey(runtimeRecord.DispatchAssetKey);
                if (runtimeRecord.DispatchRequestId != 0)
                    dispatcher.AcknowledgeDispatchRequest(runtimeRecord.DispatchRequestId, false);
            }

            runtimeRecord.DispatchRequestId = 0;
            runtimeRecord.DispatchAssetKey = 0u;
        }

        private void CompleteWorldPrefabDispatch(ref WorldPrefabRuntimeRecord runtimeRecord, bool success)
        {
            if (runtimeRecord.DispatchRequestId == 0)
                return;

            AssetLoadDispatcher dispatcher = _cachedAssetLoadDispatcher;
            if (dispatcher != null)
                dispatcher.AcknowledgeDispatchRequest(runtimeRecord.DispatchRequestId, success);

            runtimeRecord.DispatchRequestId = 0;
        }

        private void CaptureCurrentPlayerAup(ref WorldPrefabRuntimeRecord runtimeRecord)
        {
            if (!TryCaptureCurrentPlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            runtimeRecord.LastAccessAup = playerAup;
            runtimeRecord.HasLastAccessAup = true;
        }

        private bool TryCaptureCurrentPlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null || !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
                return false;

            playerAup = snapshot.Aup;
            return playerAup.IsFinite();
        }
#endif

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void CacheAddressableRuntimeServicesCold()
        {
            if (!Application.isPlaying)
                return;

#if UNITY_ADDRESSABLES_EXIST
            if (_cachedAssetLifecycleGovernor == null)
                _cachedAssetLifecycleGovernor = GlobalRegistry.AssetLifecycle;
            if (_cachedAssetLoadDispatcher == null)
                _cachedAssetLoadDispatcher = GlobalRegistry.AssetLoadDispatcher;
            if (_cachedPlayerContext == null)
                _cachedPlayerContext = GlobalRegistry.Player;
#endif
        }

        private void CacheQuestSystemCold()
        {
            if (!Application.isPlaying)
                return;

            _cachedQuestSystem = GlobalRegistry.QuestSystem;
            ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
        }

        private void ClearCachedRuntimeServices()
        {
            _cachedQuestSystem = null;
            ItemTemplateRegistry.ConfigureQuestSystem(null);

#if UNITY_ADDRESSABLES_EXIST
            _cachedAssetLifecycleGovernor = null;
            _cachedAssetLoadDispatcher = null;
            _cachedPlayerContext = null;
#endif
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.QuestSystem ||
                serviceSlot == GlobalRegistryServiceSlot.QuestRuntime)
            {
                _cachedQuestSystem = currentService as IQuestSystem;
                ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
            }

#if UNITY_ADDRESSABLES_EXIST
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _cachedAssetLifecycleGovernor = currentService as AssetLifecycleGovernor;
                    break;
                case GlobalRegistryServiceSlot.AssetLoadDispatcherRuntime:
                    _cachedAssetLoadDispatcher = currentService as AssetLoadDispatcher;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    break;
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
                    Hecton8.Core.H8Debug.LogWarning("[ItemCatalog] Duplicate ID alias. Skipping duplicate entry.", item);
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
                    Hecton8.Core.H8Debug.LogWarning("[ItemCatalog] Duplicate hash alias. Skipping duplicate entry.", item);
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
                BuildStateFlags(item),
                item.weight,
                (byte)item.category,
                item.VulnerabilityMask,
                item.AudioMaterialByte,
                (byte)item.PhysicsMaterialTag,
                item.MassKg,
                item.VolumeM3,
                item.RadiationSvPerSecond,
                item.stackable && item.maxStack > 1,
                item.isConsumable,
                item.oxygenRestore,
                item.energyRestore,
                item.integrityRestore,
                item.hungerRestore,
                item.thirstRestore,
                item.UseDuration);
        }

        private static ushort BuildStateFlags(ItemData item)
        {
            if (item == null)
                return 0;

            ushort flags = 0;
            if (item.stackable && item.maxStack > 1)
                flags |= ItemRuntimeStateFlags.Stackable;

            if (item.isConsumable)
                flags |= ItemRuntimeStateFlags.Consumable;

            if (item.category == ItemCategory.Tool)
                flags |= ItemRuntimeStateFlags.Tool;

            if (item.IsRadioactive)
                flags |= ItemRuntimeStateFlags.Radioactive;

            if (item.category == ItemCategory.Consumable ||
                item.category == ItemCategory.Organic ||
                item.resourceFamily == ResourceFamily.Organic)
            {
                flags |= ItemRuntimeStateFlags.Biological;
            }

            if (ItemPhysicalMetadataUtility.IsFlammable(item.category, item.resourceFamily, item.PersistentId))
                flags |= ItemRuntimeStateFlags.Flammable;

            return flags;
        }

        private void ApplyRuntimeTemplateRegistrySnapshot()
        {
            if (!Application.isPlaying)
            {
                ItemTemplateRegistry.Clear();
                ItemTemplateRegistry.ConfigureQuestSystem(null);
                return;
            }

            if (_hashLookup == null || _runtimeDescriptorLookup == null || _hashLookup.Count <= 0)
            {
                ItemTemplateRegistry.Configure(null);
                ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
                return;
            }

            ItemTemplate[] templates = new ItemTemplate[_hashLookup.Count]; // COLD ALLOC: ItemTemplate[_hashLookup.Count] - runtime compact item template snapshot rebuilt from ItemCatalog - owner: ItemCatalog
            int templateCount = 0;
            if (allItems != null)
                templateCount = AppendTemplateSnapshotEntries(allItems, templates, templateCount);

            if (_runtimeItems != null)
                templateCount = AppendTemplateSnapshotEntries(_runtimeItems, templates, templateCount);

            if (templateCount <= 0)
            {
                ItemTemplateRegistry.Configure(null);
                ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
                return;
            }

            if (templateCount != templates.Length)
            {
                ItemTemplate[] compactTemplates = new ItemTemplate[templateCount]; // COLD ALLOC: ItemTemplate[templateCount] - trimmed compact template snapshot after dedupe - owner: ItemCatalog
                Array.Copy(templates, compactTemplates, templateCount);
                ItemTemplateRegistry.Configure(compactTemplates);
                ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
                return;
            }

            ItemTemplateRegistry.Configure(templates);
            ItemTemplateRegistry.ConfigureQuestSystem(_cachedQuestSystem);
        }

        private int AppendTemplateSnapshotEntries(List<ItemData> sourceItems, ItemTemplate[] destination, int writeIndex)
        {
            if (sourceItems == null || destination == null)
                return writeIndex;

            for (int i = 0; i < sourceItems.Count && writeIndex < destination.Length; i++)
            {
                ItemData item = sourceItems[i];
                if (item == null)
                    continue;

                int hashId = LocHash.Compute(item.PersistentId);
                if (!_runtimeDescriptorLookup.TryGetValue(hashId, out ItemRuntimeDescriptor descriptor) || !IsValidDescriptor(in descriptor))
                    continue;

                if (TryFindTemplateIndex(destination, writeIndex, hashId) >= 0)
                    continue;

                destination[writeIndex++] = BuildRuntimeTemplate(descriptor, item);
            }

            return writeIndex;
        }

        private static int TryFindTemplateIndex(ItemTemplate[] templates, int count, int hashId)
        {
            uint unsignedHashId = unchecked((uint)hashId);
            for (int i = 0; i < count; i++)
            {
                if (templates[i].HashID == unsignedHashId)
                    return i;
            }

            return -1;
        }

        private static ItemTemplate BuildRuntimeTemplate(ItemRuntimeDescriptor descriptor, ItemData item)
        {
            ItemCategoryMask categoryMask = ResolveCategoryMask((ItemCategory)descriptor.CategoryId, item);
            float resolvedBaseDurability = Mathf.Max(1f, descriptor.MassKg * 10f);
            float resolvedWearMultiplier = ResolveWearMultiplier(descriptor.AudioMaterialId);
            return new ItemTemplate(
                unchecked((uint)descriptor.HashId),
                categoryMask,
                resolvedBaseDurability,
                resolvedWearMultiplier,
                descriptor.MaxStack,
                0,
                0,
                0,
                descriptor.VulnerabilityMask,
                descriptor.AudioMaterialId,
                descriptor.PhysicsMaterialTag,
                descriptor.MassKg,
                descriptor.VolumeM3);
        }

        private static ItemCategoryMask ResolveCategoryMask(ItemCategory category, ItemData item)
        {
            switch (category)
            {
                case ItemCategory.Material:
                    return item != null && item.resourceFamily == ResourceFamily.Organic
                        ? ItemCategoryMask.Biological
                        : ItemCategoryMask.Mineral;

                case ItemCategory.Tool:
                    return ItemCategoryMask.Tool;

                case ItemCategory.Equipment:
                case ItemCategory.Component:
                    return ItemCategoryMask.Tech | ItemCategoryMask.Craft;

                case ItemCategory.Consumable:
                    return ItemCategoryMask.Food;

                case ItemCategory.Organic:
                    return ItemCategoryMask.Biological;
            }

            return ItemCategoryMask.Craft;
        }

        private static float ResolveWearMultiplier(byte audioMaterialId)
        {
            switch ((ItemAudioMaterialId)audioMaterialId)
            {
                case ItemAudioMaterialId.Metal:
                    return 0.8f;

                case ItemAudioMaterialId.Glass:
                    return 1.25f;

                default:
                    return 1f;
            }
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
            {
                ConfigureBundledLoadMode(settings.DefaultGroup);
                return settings.DefaultGroup;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
            {
                ConfigureBundledLoadMode(group);
                return group;
            }

            group = settings.CreateGroup(groupName, false, false, false, null, typeof(BundledAssetGroupSchema));
            ConfigureBundledLoadMode(group);
            return group;
        }

        private static void ConfigureBundledLoadMode(AddressableAssetGroup group)
        {
            BundledAssetGroupSchema schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
            if (schema == null)
                return;

            if (schema.AssetLoadMode != AssetLoadMode.RequestedAssetAndDependencies)
            {
                schema.AssetLoadMode = AssetLoadMode.RequestedAssetAndDependencies;
                EditorUtility.SetDirty(group);
            }
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
