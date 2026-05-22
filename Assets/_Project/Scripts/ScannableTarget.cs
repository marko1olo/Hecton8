using System;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Scannable Target")]
    public sealed class ScannableTarget : MonoBehaviour
    {
        [SerializeField] private string entryId = "scannable.unknown";
        [SerializeField] private string entryTitle = "UNIDENTIFIED CONTACT";
        [SerializeField] private string entryCategory = "Unknown";
        [TextArea(2, 5)]
        [SerializeField] private string entrySummary =
            "Passive scan profile has been captured. Manual classification pending.";
        private const int MaxLoreEntityCount = 1024;
        private static readonly ScannableTarget[] s_loreEntityTargets = new ScannableTarget[MaxLoreEntityCount]; // COLD ALLOC: ScannableTarget[1024] - lore scanner owner mirror - owner: ScannableTarget
        private static IDataVault s_loreEntityVault;
        private static VaultGenerationHandle<AbsoluteUniversePosition> s_loreEntityAupsHandle;
        private static VaultGenerationHandle<uint> s_loreEntityHashesHandle;
        private static int s_loreEntityCount;
        private static int s_loreEntitySyncFrame = int.MinValue;
        private static uint s_loreTitleLookupHash;
        private static int s_loreTitleLookupIndex = -1;
        private static int s_loreTitleLookupVersion;
        private int _spatialHandle;
        private int _loreRegistryIndex = -1;
        private string _resolvedEntryId;
        private string _resolvedEntryTitle;
        private string _resolvedEntryCategory;
        private string _resolvedEntrySummary;
        private ScannableCategoryUtility.CategoryKind _resolvedCategoryKind;
        private uint _entityHash;
        private int _runtimeObjectId;

        private static int ResolveScannerFrameInt()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame > int.MaxValue ? int.MaxValue : (int)frame;
        }

        public string EntryId
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryId;
            }
        }

        public string EntryTitle
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryTitle;
            }
        }

        public string EntryCategory
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntryCategory;
            }
        }

        public string EntrySummary
        {
            get
            {
                EnsureResolvedStrings();
                return _resolvedEntrySummary;
            }
        }

        public ScannableCategoryUtility.CategoryKind CachedCategoryKind => _resolvedCategoryKind;

        public uint CachedEntityHash => _entityHash;

        public static int LoreTitleLookupVersion => s_loreTitleLookupVersion;

        public int ReadRuntimeObjectId()
        {
            return _runtimeObjectId;
        }

        /// <summary>Stable FNV-1a entity hash used by zero-GC scanner paths.</summary>
        public uint EntityHash
        {
            get
            {
                EnsureResolvedStrings();
                return _entityHash;
            }
        }

        /// <summary>Signed form of <see cref="EntityHash"/> for native hash maps keyed by int.</summary>
        public int EntityHash32
        {
            get
            {
                EnsureResolvedStrings();
                return unchecked((int)_entityHash);
            }
        }

        public void Configure(string id, string title, string category, string summary)
        {
            entryId = string.IsNullOrWhiteSpace(id) ? gameObject.name : id.Trim();
            entryTitle = string.IsNullOrWhiteSpace(title) ? CachedToUpperInvariant(gameObject.name) : title.Trim();
            entryCategory = string.IsNullOrWhiteSpace(category) ? "Unknown" : category.Trim();
            entrySummary = string.IsNullOrWhiteSpace(summary)
                ? "Passive scan profile has been captured."
                : summary.Trim();
            RefreshResolvedStrings();
        }

        private void Awake()
        {
            CacheRuntimeObjectId();
            RefreshResolvedStrings();
        }

        private void OnEnable()
        {
            CacheRuntimeObjectId();
            EnsureResolvedStrings();
            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterScannable(this);

            if (_loreRegistryIndex < 0)
                _loreRegistryIndex = RegisterLoreEntity(this);
        }

        private void OnDisable()
        {
            UnregisterLoreEntity(this);
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void OnDestroy()
        {
            UnregisterLoreEntity(this);
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (string.IsNullOrWhiteSpace(entryId))
                entryId = gameObject.name.Trim().ToLowerInvariant().Replace(' ', '_');

            if (string.IsNullOrWhiteSpace(entryTitle))
                entryTitle = CachedToUpperInvariant(gameObject.name);

            if (string.IsNullOrWhiteSpace(entryCategory))
                entryCategory = "Unknown";

            RefreshResolvedStrings();
        }
#endif

        // ══════════════════════════════════════════════════════════
        //  ZERO-GC STRING CACHING
        // ══════════════════════════════════════════════════════════

        private static readonly string[] _upperCacheKeys = new string[16]; // COLD ALLOC: string[16] - uppercase fallback key cache - owner: ScannableTarget
        private static readonly string[] _upperCacheValues = new string[16]; // COLD ALLOC: string[16] - uppercase fallback value cache - owner: ScannableTarget

        private void EnsureResolvedStrings()
        {
            if (_resolvedEntryId == null)
                RefreshResolvedStrings();
        }

        private void RefreshResolvedStrings()
        {
            string objectName = gameObject.name;
            _resolvedEntryId = string.IsNullOrWhiteSpace(entryId) ? objectName : entryId.Trim();
            _resolvedEntryTitle = string.IsNullOrWhiteSpace(entryTitle) ? CachedToUpperInvariant(objectName) : entryTitle.Trim();
            _resolvedEntryCategory = string.IsNullOrWhiteSpace(entryCategory) ? "Unknown" : entryCategory.Trim();
            _resolvedEntrySummary = string.IsNullOrWhiteSpace(entrySummary)
                ? "Passive scan profile has been captured."
                : entrySummary.Trim();
            _resolvedCategoryKind = ScannableCategoryUtility.Classify(_resolvedEntryCategory);
            _entityHash = H8DataHash.ComputeFnv1A32(_resolvedEntryId);
            InvalidateLoreTitleLookupCache();
        }

        private void CacheRuntimeObjectId()
        {
            GameObject targetObject = gameObject;
            _runtimeObjectId = targetObject != null ? targetObject.GetInstanceID() : 0;
        }

        public static void PublishLoreEntitySnapshotsFromOwnerPhase()
        {
            SyncLoreEntityVaultAups();
        }

        public static bool TryReadLoreEntityBuffers(
            out NativeArray<AbsoluteUniversePosition>.ReadOnly loreEntityAups,
            out NativeArray<uint>.ReadOnly loreEntityHashes,
            out int count)
        {
            loreEntityAups = default;
            loreEntityHashes = default;
            count = s_loreEntityCount;
            if (count <= 0 ||
                !TryReadLoreEntityVaultBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<uint> hashes) ||
                !aups.IsCreated ||
                !hashes.IsCreated ||
                aups.Length < count ||
                hashes.Length < count)
            {
                return false;
            }

            loreEntityAups = aups.AsReadOnly();
            loreEntityHashes = hashes.AsReadOnly();
            return true;
        }

        public static ScannableTarget ResolveLoreEntityTarget(int index, uint hash)
        {
            if ((uint)index >= (uint)s_loreEntityCount)
                return null;

            ScannableTarget target = s_loreEntityTargets[index];
            if (target == null)
                return null;

            return hash == 0u || target._entityHash == hash ? target : null;
        }

        public static bool TryWriteLoreEntityTitle(uint hash, Span<char> destination, out int written)
        {
            written = 0;
            if (hash == 0u || destination.Length <= 0)
                return false;

            if (s_loreTitleLookupHash == hash &&
                (uint)s_loreTitleLookupIndex < (uint)s_loreEntityCount &&
                TryCopyLoreEntityTitle(s_loreEntityTargets[s_loreTitleLookupIndex], hash, destination, out written))
            {
                return true;
            }

            for (int i = 0; i < s_loreEntityCount; i++)
            {
                ScannableTarget target = s_loreEntityTargets[i];
                if (!TryCopyLoreEntityTitle(target, hash, destination, out written))
                    continue;

                s_loreTitleLookupHash = hash;
                s_loreTitleLookupIndex = i;
                return true;
            }

            return false;
        }

        private static bool TryCopyLoreEntityTitle(
            ScannableTarget target,
            uint hash,
            Span<char> destination,
            out int written)
        {
            written = 0;
            if (target == null || target.EntityHash != hash)
                return false;

            ReadOnlySpan<char> title = target.EntryTitle.AsSpan();
            int length = math.min(title.Length, destination.Length);
            if (length <= 0)
                return false;

            title.Slice(0, length).CopyTo(destination);
            written = length;
            return true;
        }

        private static void InvalidateLoreTitleLookupCache()
        {
            s_loreTitleLookupHash = 0u;
            s_loreTitleLookupIndex = -1;
            unchecked
            {
                s_loreTitleLookupVersion++;
                if (s_loreTitleLookupVersion == 0)
                    s_loreTitleLookupVersion = 1;
            }
        }

        private static int RegisterLoreEntity(ScannableTarget target)
        {
            if (target == null)
                return -1;

            for (int i = 0; i < s_loreEntityCount; i++)
            {
                if (ReferenceEquals(s_loreEntityTargets[i], target))
                    return i;
            }

            if (s_loreEntityCount >= MaxLoreEntityCount)
                return -1;

            int index = s_loreEntityCount++;
            s_loreEntityTargets[index] = target;
            WriteLoreEntitySlot(index, target);
            InvalidateLoreTitleLookupCache();
            PublishLoreEntitySnapshotsFromOwnerPhase();
            return index;
        }

        private static void UnregisterLoreEntity(ScannableTarget target)
        {
            if (target == null)
                return;

            int index = target._loreRegistryIndex;
            if ((uint)index >= (uint)s_loreEntityCount || !ReferenceEquals(s_loreEntityTargets[index], target))
                index = FindLoreEntityIndex(target);

            if (index < 0)
            {
                target._loreRegistryIndex = -1;
                return;
            }

            int lastIndex = s_loreEntityCount - 1;
            ScannableTarget moved = s_loreEntityTargets[lastIndex];
            s_loreEntityTargets[lastIndex] = null;
            s_loreEntityCount = lastIndex;

            if (index != lastIndex)
            {
                s_loreEntityTargets[index] = moved;
                if (moved != null)
                {
                    moved._loreRegistryIndex = index;
                    WriteLoreEntitySlot(index, moved);
                }
            }

            ClearLoreEntitySlot(lastIndex);
            target._loreRegistryIndex = -1;
            InvalidateLoreTitleLookupCache();
            PublishLoreEntitySnapshotsFromOwnerPhase();
        }

        private static int FindLoreEntityIndex(ScannableTarget target)
        {
            for (int i = 0; i < s_loreEntityCount; i++)
            {
                if (ReferenceEquals(s_loreEntityTargets[i], target))
                    return i;
            }

            return -1;
        }

        private static void SyncLoreEntityVaultAups()
        {
            if (!EnsureLoreEntityVaultBuffers())
                return;

            if (Application.isPlaying)
            {
                int frame = ResolveScannerFrameInt();
                if (s_loreEntitySyncFrame == frame)
                    return;

                s_loreEntitySyncFrame = frame;
            }

            for (int i = 0; i < s_loreEntityCount; i++)
            {
                ScannableTarget target = s_loreEntityTargets[i];
                if (target == null)
                {
                    ClearLoreEntitySlot(i);
                    continue;
                }

                WorldSpatialHashGrid.Refresh(target._spatialHandle);
                WriteLoreEntitySlot(i, target);
            }
        }

        private static void WriteLoreEntitySlot(int index, ScannableTarget target)
        {
            if ((uint)index >= MaxLoreEntityCount ||
                target == null ||
                !EnsureLoreEntityVaultBuffers() ||
                !TryReadLoreEntityVaultBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<uint> hashes))
            {
                return;
            }

            if (!target.TryReadLoreEntityAupFromSpatialOwner(out AbsoluteUniversePosition entityAup))
            {
                aups[index] = default;
                hashes[index] = 0u;
                return;
            }

            target.EnsureResolvedStrings();
            aups[index] = entityAup;
            hashes[index] = target._entityHash;
        }

        private bool TryReadLoreEntityAupFromSpatialOwner(out AbsoluteUniversePosition entityAup)
        {
            entityAup = default;
            if (_spatialHandle <= 0 ||
                !WorldSpatialHashGrid.TryGetAbsolutePosition(_spatialHandle, out entityAup) ||
                !entityAup.IsFinite())
            {
                return false;
            }

            return true;
        }

        private static void ClearLoreEntitySlot(int index)
        {
            if ((uint)index >= MaxLoreEntityCount ||
                !EnsureLoreEntityVaultBuffers() ||
                !TryReadLoreEntityVaultBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<uint> hashes))
            {
                return;
            }

            aups[index] = default;
            hashes[index] = 0u;
        }

        private static bool EnsureLoreEntityVaultBuffers()
        {
            IDataVault vault = s_loreEntityVault;
            IDataVault activeVault = GlobalRegistry.DataVault;
            if (vault != null && activeVault != null && !ReferenceEquals(vault, activeVault))
            {
                InvalidateLoreEntityVaultViews(resetHandles: true);
                vault = null;
            }

            if (vault != null &&
                TryReadLoreEntityVaultBuffers(out NativeArray<AbsoluteUniversePosition> existingAups, out NativeArray<uint> existingHashes))
            {
                return existingAups.Length >= MaxLoreEntityCount &&
                       existingHashes.Length >= MaxLoreEntityCount;
            }

            vault = activeVault;
            if (vault == null)
                return false;

            s_loreEntityVault = vault;
            InvalidateLoreEntityVaultViews(resetHandles: false);
            s_loreEntityAupsHandle = vault.GetGenerationHandle<AbsoluteUniversePosition>(
                BufferID.LoreEntityAUPs,
                MaxLoreEntityCount,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            s_loreEntityHashesHandle = vault.GetGenerationHandle<uint>(
                BufferID.LoreEntityHashes,
                MaxLoreEntityCount,
                SystemID.UI,
                NativeArrayOptions.ClearMemory);
            return TryReadLoreEntityVaultBuffers(out NativeArray<AbsoluteUniversePosition> aups, out NativeArray<uint> hashes) &&
                   aups.Length >= MaxLoreEntityCount &&
                   hashes.Length >= MaxLoreEntityCount;
        }

        private static bool TryReadLoreEntityVaultBuffers(
            out NativeArray<AbsoluteUniversePosition> aups,
            out NativeArray<uint> hashes)
        {
            aups = default;
            hashes = default;
            IDataVault vault = s_loreEntityVault;
            if (vault == null ||
                !IsLoreEntityHandleCreated(in s_loreEntityAupsHandle) ||
                !IsLoreEntityHandleCreated(in s_loreEntityHashesHandle) ||
                !vault.TryResolveHandle(in s_loreEntityAupsHandle, out aups) ||
                !vault.TryResolveHandle(in s_loreEntityHashesHandle, out hashes) ||
                !aups.IsCreated ||
                !hashes.IsCreated)
            {
                InvalidateLoreEntityVaultViews(resetHandles: false);
                return false;
            }

            return true;
        }

        private static void InvalidateLoreEntityVaultViews(bool resetHandles)
        {
            if (!resetHandles)
                return;

            s_loreEntityVault = null;
            s_loreEntityAupsHandle = default;
            s_loreEntityHashesHandle = default;
        }

        private static bool IsLoreEntityHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            int hash = input.GetHashCode() & 0xF;
            string cachedKey = _upperCacheKeys[hash];
            if (cachedKey != null && string.Equals(cachedKey, input, System.StringComparison.Ordinal))
                return _upperCacheValues[hash];

            string upper = input.ToUpperInvariant();
            _upperCacheKeys[hash] = input;
            _upperCacheValues[hash] = upper;
            return upper;
        }
    }
}
