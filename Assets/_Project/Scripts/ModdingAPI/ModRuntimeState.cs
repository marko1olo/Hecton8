using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Crafting;
using Hecton8.Economy;
using Hecton8.Ecosystem;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Modding
{
    internal static class ModExecutionScope
    {
        [System.ThreadStatic] private static string _currentModId;
        [System.ThreadStatic] private static uint _currentModHash;
        [System.ThreadStatic] private static int _scopeDepth;

        internal static string CurrentModId => _currentModId ?? string.Empty;
        internal static uint CurrentModHash => _currentModHash;
        internal static bool HasActiveMod =>
            _scopeDepth > 0 &&
            !string.IsNullOrWhiteSpace(_currentModId) &&
            _currentModHash != 0u;

        internal static Scope Enter(string modId)
        {
            return new Scope(modId, 0u);
        }

        internal static Scope Enter(string modId, uint modHash)
        {
            return new Scope(modId, modHash);
        }

        internal readonly struct Scope : System.IDisposable
        {
            private readonly string _previousModId;
            private readonly uint _previousModHash;
            private readonly int _previousScopeDepth;

            internal Scope(string modId, uint modHash)
            {
                if (string.IsNullOrWhiteSpace(modId))
                    throw new IllegalContractException("Mod execution scope requires a non-empty owner id.");

                uint resolvedModHash = modHash != 0u
                    ? modHash
                    : ModCommandDispatcher.ComputeModHash(modId);
                if (resolvedModHash == 0u)
                    throw new IllegalContractException("Mod execution scope requires a non-zero owner hash.");

                _previousModId = _currentModId;
                _previousModHash = _currentModHash;
                _previousScopeDepth = _scopeDepth;
                _currentModId = modId;
                _currentModHash = resolvedModHash;
                _scopeDepth = _previousScopeDepth + 1;
            }

            public void Dispose()
            {
                _currentModId = _previousModId;
                _currentModHash = _previousModHash;
                _scopeDepth = _previousScopeDepth;
            }
        }
    }

    internal static class ModSaveStateStore
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const string SaveDictionaryPrefix = "m8v1:";
        private const string EngineStorageKeyPrefix = "hecton.internal.";
        private const string EngineStorageOwnerId = "hecton.internal.engine_save_owner";
        private const SystemID NativeArrayOwnerSystem = SystemID.ModSandbox;
        private const string ModPayloadWriteBufferLabel = "modPayloadWriteBuffer";
        private const string ModPayloadReadBufferLabel = "modPayloadReadBuffer";
        private const string NativeMemoryAllocationFailureMessage = "H8Memory allocation failed for ModSaveStateStore temp buffer.";
        private const string NativeMemoryReleaseFailureMessage = "H8Memory release failed for ModSaveStateStore temp buffer.";
        private static readonly uint EngineStorageOwnerHash = ModCommandDispatcher.ComputeModHash(EngineStorageOwnerId);

        // COLD ALLOC: Dictionary<string,string>[64] — custom mod save payload map persisted inside SaveData — owner: ModSaveStateStore
        private struct ModSaveEntry
        {
            public string Key;
            public string Value;
            public string SerializedKey;
            public uint KeyHash;
            public uint ModHash;
            public uint CompoundHash;
        }

        private static readonly List<ModSaveEntry> _customModData = new List<ModSaveEntry>(64);
        // COLD ALLOC: List<ModSaveEntry>[64] - MMF load rollback snapshot - owner: ModSaveStateStore
        private static readonly List<ModSaveEntry> _mmfLoadRollbackData = new List<ModSaveEntry>(64);
        private static readonly Dictionary<uint, int> _customModIndexByHash = new Dictionary<uint, int>(64);
        // COLD ALLOC: char[ModPayloadMaxBytes/2] - reusable UTF-16 decode scratch for mod save payload load - owner: ModSaveStateStore
        private static readonly char[] _decodeCharScratch =
            new char[SaveBinaryStorage.ModPayloadMaxBytes / sizeof(char)];
        // COLD ALLOC: ModPayloadReadHandler[1] - cached batch MMF payload visitor - owner: ModSaveStateStore
        private static readonly SaveBinaryStorage.ModPayloadReadHandler _modPayloadReadHandler = LoadMmfPayload;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _customModData.Clear();
            _mmfLoadRollbackData.Clear();
            _customModIndexByHash.Clear();
        }

        internal static void SetModString(string key, string value)
        {
            SetStringForOwner(key, value, RequireActivePersistenceOwnerHash("SetModString"));
        }

        internal static string GetModString(string key, string defaultValue)
        {
            if (TryGetStringForOwner(key, RequireActivePersistenceOwnerHash("GetModString"), out string value))
                return value;

            return defaultValue ?? string.Empty;
        }

        internal static void SetEngineString(string key, string value)
        {
            SetStringForOwner(key, value, RequireEnginePersistenceOwnerHash(key));
        }

        internal static string GetEngineString(string key, string defaultValue)
        {
            uint ownerHash = RequireEnginePersistenceOwnerHash(key);
            if (TryGetStringForOwner(key, ownerHash, out string value))
                return value;

            uint legacyOwnerHash = ModCommandDispatcher.ComputeModHash(key);
            if (legacyOwnerHash != ownerHash && TryGetStringForOwner(key, legacyOwnerHash, out value))
                return value;

            return defaultValue ?? string.Empty;
        }

        private static void SetStringForOwner(string key, string value, uint ownerHash)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Hecton8.Core.H8Debug.LogError("[ModSaveStateStore] Refused to write mod save data with an empty key.");
                return;
            }

            uint keyHash = ModCommandDispatcher.ComputeModHash(key);
            uint compoundHash = ComputeCompoundPersistenceHash(ownerHash, keyHash);
            if (_customModIndexByHash.TryGetValue(compoundHash, out int index) && index >= 0 && index < _customModData.Count)
            {
                ModSaveEntry entry = _customModData[index];
                if (entry.Key == key || string.IsNullOrEmpty(entry.Key) || entry.KeyHash == keyHash)
                {
                    entry.Key = key;
                    entry.Value = value ?? string.Empty;
                    entry.SerializedKey = BuildSerializedStorageKey(ownerHash, keyHash);
                    entry.KeyHash = keyHash;
                    entry.ModHash = ownerHash;
                    entry.CompoundHash = compoundHash;
                    _customModData[index] = entry;
                    return;
                }
            }

            for (int i = 0; i < _customModData.Count; i++)
            {
                ModSaveEntry entry = _customModData[i];
                if (!MatchesPersistenceEntry(in entry, key, keyHash, ownerHash, compoundHash))
                    continue;

                if (entry.CompoundHash != 0u && entry.CompoundHash != compoundHash)
                    _customModIndexByHash.Remove(entry.CompoundHash);

                entry.Key = key;
                entry.Value = value ?? string.Empty;
                entry.SerializedKey = BuildSerializedStorageKey(ownerHash, keyHash);
                entry.KeyHash = keyHash;
                entry.ModHash = ownerHash;
                entry.CompoundHash = compoundHash;
                _customModData[i] = entry;
                _customModIndexByHash[compoundHash] = i;
                return;
            }

            _customModIndexByHash[compoundHash] = _customModData.Count;
            _customModData.Add(new ModSaveEntry
            {
                Key = key,
                Value = value ?? string.Empty,
                SerializedKey = BuildSerializedStorageKey(ownerHash, keyHash),
                KeyHash = keyHash,
                ModHash = ownerHash,
                CompoundHash = compoundHash
            });
        }

        private static bool TryGetStringForOwner(string key, uint ownerHash, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            uint keyHash = ModCommandDispatcher.ComputeModHash(key);
            uint compoundHash = ComputeCompoundPersistenceHash(ownerHash, keyHash);
            if (_customModIndexByHash.TryGetValue(compoundHash, out int index) && index >= 0 && index < _customModData.Count)
            {
                ModSaveEntry entry = _customModData[index];
                if (MatchesPersistenceEntry(in entry, key, keyHash, ownerHash, compoundHash))
                {
                    value = entry.Value ?? string.Empty;
                    return true;
                }
            }

            for (int i = 0; i < _customModData.Count; i++)
            {
                ModSaveEntry entry = _customModData[i];
                if (MatchesPersistenceEntry(in entry, key, keyHash, ownerHash, compoundHash))
                {
                    value = entry.Value ?? string.Empty;
                    return true;
                }
            }

            return false;
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

            for (int i = 0; i < _customModData.Count; i++)
            {
                ModSaveEntry entry = _customModData[i];
                if (entry.ModHash != 0u && entry.KeyHash != 0u)
                {
                    string storageKey = entry.SerializedKey;
                    if (string.IsNullOrEmpty(storageKey))
                    {
                        storageKey = BuildSerializedStorageKey(entry.ModHash, entry.KeyHash);
                        entry.SerializedKey = storageKey;
                        _customModData[i] = entry;
                    }

                    data.CustomModData[storageKey] = entry.Value ?? string.Empty;
                }
                else if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    data.CustomModData[entry.Key] = entry.Value ?? string.Empty;
                }
            }
        }

        internal static void LoadFromSaveData(SaveData data)
        {
            _customModData.Clear();
            _customModIndexByHash.Clear();

            if (data == null || data.CustomModData == null || data.CustomModData.Count == 0)
                return;

            Dictionary<string, string>.Enumerator enumerator = data.CustomModData.GetEnumerator();
            while (enumerator.MoveNext())
            {
                string key = enumerator.Current.Key;
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                bool isNamespaced = TryParseSerializedStorageKey(key, out uint modHash, out uint keyHash);
                uint compoundHash = isNamespaced
                    ? ComputeCompoundPersistenceHash(modHash, keyHash)
                    : keyHash;
                AddOrReplaceLoadedSaveEntry(
                    isNamespaced ? string.Empty : key,
                    enumerator.Current.Value ?? string.Empty,
                    isNamespaced ? BuildSerializedStorageKey(modHash, keyHash) : string.Empty,
                    keyHash,
                    modHash,
                    compoundHash);
            }
        }

        internal static bool TryCommitMmfPayloads(string absoluteSavePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath) || _customModData.Count == 0)
                return true;

            NativeArray<byte> payloadBytes = default;
            try
            {
                payloadBytes = CreateTempNativeArrayBuffer(
                    math.max(1, SaveBinaryStorage.ModPayloadMaxBytes),
                    ModPayloadWriteBufferLabel);

                for (int i = 0; i < _customModData.Count; i++)
                {
                    ModSaveEntry entry = _customModData[i];
                    if (entry.ModHash == 0u || entry.KeyHash == 0u)
                        continue;

                    string value = entry.Value ?? string.Empty;
                    int payloadLength = value.Length > SaveBinaryStorage.ModPayloadMaxBytes ? SaveBinaryStorage.ModPayloadMaxBytes + sizeof(char) : value.Length * sizeof(char);
                    string tempOverridePath = BuildModPayloadTempOverridePath(absoluteSavePath, entry.ModHash, entry.KeyHash);
                    if (payloadLength > SaveBinaryStorage.ModPayloadMaxBytes)
                    {
                        if (!SaveBinaryStorage.TryCommitModPayloadSubSector(
                                absoluteSavePath,
                                tempOverridePath,
                                entry.ModHash,
                                entry.KeyHash,
                                payloadBytes,
                                payloadLength,
                                out string oversizeCommitError))
                        {
                            error = oversizeCommitError;
                        }

                        continue;
                    }

                    for (int charIndex = 0; charIndex < value.Length; charIndex++)
                    {
                        char character = value[charIndex];
                        int byteIndex = charIndex * sizeof(char);
                        payloadBytes[byteIndex] = (byte)(character & 0xFF);
                        payloadBytes[byteIndex + 1] = (byte)(character >> 8);
                    }

                    if (!SaveBinaryStorage.TryCommitModPayloadSubSector(
                            absoluteSavePath,
                            tempOverridePath,
                            entry.ModHash,
                            entry.KeyHash,
                            payloadBytes,
                            payloadLength,
                            out string commitError))
                    {
                        error = commitError;
                    }
                }
            }
            finally
            {
                DisposeTempNativeArrayBuffer(ref payloadBytes, ModPayloadWriteBufferLabel);
            }

            return string.IsNullOrEmpty(error);
        }

        private static string BuildModPayloadTempOverridePath(string absoluteSavePath, uint modHash, uint keyHash)
        {
            return absoluteSavePath +
                   ".mod_" +
                   modHash.ToString("X8") +
                   "_" +
                   keyHash.ToString("X8") +
                   ".sectmp";
        }

        internal static bool TryLoadMmfPayloads(string absoluteSavePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath))
                return true;

            NativeArray<byte> payloadBytes = default;
            bool keepLoadedPayloads = false;
            CaptureMmfLoadRollbackSnapshot();
            try
            {
                payloadBytes = CreateTempNativeArrayBuffer(
                    math.max(1, SaveBinaryStorage.ModPayloadMaxBytes),
                    ModPayloadReadBufferLabel);

                bool loaded = SaveBinaryStorage.TryReadIndexedModPayloads(
                    absoluteSavePath,
                    null,
                    payloadBytes,
                    _modPayloadReadHandler,
                    out error);
                if (!loaded)
                {
                    RestoreMmfLoadRollbackSnapshot();
                    return false;
                }

                keepLoadedPayloads = true;
                return true;
            }
            catch
            {
                RestoreMmfLoadRollbackSnapshot();
                throw;
            }
            finally
            {
                try
                {
                    DisposeTempNativeArrayBuffer(ref payloadBytes, ModPayloadReadBufferLabel);
                }
                catch
                {
                    if (keepLoadedPayloads)
                        RestoreMmfLoadRollbackSnapshot();
                    else
                        DiscardMmfLoadRollbackSnapshot();

                    throw;
                }

                if (keepLoadedPayloads)
                    DiscardMmfLoadRollbackSnapshot();
            }
        }

        private static void CaptureMmfLoadRollbackSnapshot()
        {
            _mmfLoadRollbackData.Clear();
            for (int i = 0; i < _customModData.Count; i++)
                _mmfLoadRollbackData.Add(_customModData[i]);
        }

        private static void RestoreMmfLoadRollbackSnapshot()
        {
            _customModData.Clear();
            for (int i = 0; i < _mmfLoadRollbackData.Count; i++)
                _customModData.Add(_mmfLoadRollbackData[i]);

            RebuildCustomModIndex();
            _mmfLoadRollbackData.Clear();
        }

        private static void DiscardMmfLoadRollbackSnapshot()
        {
            _mmfLoadRollbackData.Clear();
        }

        private static void RebuildCustomModIndex()
        {
            _customModIndexByHash.Clear();
            for (int i = 0; i < _customModData.Count; i++)
            {
                uint compoundHash = _customModData[i].CompoundHash;
                if (compoundHash != 0u)
                    _customModIndexByHash[compoundHash] = i;
            }
        }

        private static void AddOrReplaceLoadedSaveEntry(
            string key,
            string value,
            string serializedKey,
            uint keyHash,
            uint modHash,
            uint compoundHash)
        {
            if (_customModIndexByHash.TryGetValue(compoundHash, out int existingIndex) &&
                existingIndex >= 0 &&
                existingIndex < _customModData.Count)
            {
                _customModData[existingIndex] = new ModSaveEntry
                {
                    Key = key ?? string.Empty,
                    Value = value ?? string.Empty,
                    SerializedKey = serializedKey ?? string.Empty,
                    KeyHash = keyHash,
                    ModHash = modHash,
                    CompoundHash = compoundHash
                };
                return;
            }

            _customModIndexByHash[compoundHash] = _customModData.Count;
            _customModData.Add(new ModSaveEntry
            {
                Key = key ?? string.Empty,
                Value = value ?? string.Empty,
                SerializedKey = serializedKey ?? string.Empty,
                KeyHash = keyHash,
                ModHash = modHash,
                CompoundHash = compoundHash
            });
        }

        private static bool LoadMmfPayload(
            in SaveBinaryStorage.ModPayloadSectorInfo sector,
            NativeArray<byte> payloadBytes,
            int payloadLength,
            out string error)
        {
            error = string.Empty;
            if (sector.ModHash == 0u || sector.PagedSectorHash == 0L || payloadLength < 0)
                return true;

            if (!payloadBytes.IsCreated || payloadLength > payloadBytes.Length)
            {
                error = "Mod payload length exceeds decode buffer capacity.";
                return false;
            }

            if ((payloadLength & 1) != 0)
            {
                error = "Mod payload UTF-16 byte length is invalid.";
                return false;
            }

            string value = DecodeUtf16Payload(payloadBytes, payloadLength);
            uint keyHash = unchecked((uint)sector.PagedSectorHash);
            uint compoundHash = ComputeCompoundPersistenceHash(sector.ModHash, keyHash);
            if (_customModIndexByHash.TryGetValue(compoundHash, out int existingIndex) &&
                existingIndex >= 0 &&
                existingIndex < _customModData.Count)
            {
                ModSaveEntry existing = _customModData[existingIndex];
                existing.Value = value;
                existing.SerializedKey = BuildSerializedStorageKey(sector.ModHash, keyHash);
                existing.KeyHash = keyHash;
                existing.ModHash = sector.ModHash;
                existing.CompoundHash = compoundHash;
                _customModData[existingIndex] = existing;
                return true;
            }

            _customModIndexByHash[compoundHash] = _customModData.Count;
            _customModData.Add(new ModSaveEntry
            {
                Key = string.Empty,
                Value = value,
                SerializedKey = BuildSerializedStorageKey(sector.ModHash, keyHash),
                KeyHash = keyHash,
                ModHash = sector.ModHash,
                CompoundHash = compoundHash
            });

            return true;
        }

        private static uint RequireActivePersistenceOwnerHash(string surface)
        {
            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("ModSaveStateStore." + surface + " requires an active mod execution scope. Engine-owned save payloads must use SetEngineString or GetEngineString.");

            return ModExecutionScope.CurrentModHash;
        }

        private static uint RequireEnginePersistenceOwnerHash(string key)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                !key.StartsWith(EngineStorageKeyPrefix, System.StringComparison.Ordinal))
            {
                throw new IllegalContractException("Engine-owned mod save payload keys must use the hecton.internal. prefix.");
            }

            if (EngineStorageOwnerHash == 0u)
                throw new IllegalContractException("Engine-owned mod save payload route requires a non-zero owner hash.");

            return EngineStorageOwnerHash;
        }

        private static bool MatchesPersistenceEntry(
            in ModSaveEntry entry,
            string key,
            uint keyHash,
            uint modHash,
            uint compoundHash)
        {
            if (entry.CompoundHash == compoundHash)
                return true;

            if (entry.ModHash == modHash && entry.KeyHash == keyHash)
                return true;

            return entry.ModHash == 0u && entry.KeyHash == keyHash && entry.Key == key;
        }

        private static uint ComputeCompoundPersistenceHash(uint modHash, uint keyHash)
        {
            uint hash = FnvOffsetBasis;
            hash = AccumulateFnv(hash, modHash);
            hash = AccumulateFnv(hash, 0x9E3779B9u);
            return AccumulateFnv(hash, keyHash);
        }

        private static uint AccumulateFnv(uint hash, uint value)
        {
            unchecked
            {
                hash ^= (byte)value;
                hash *= FnvPrime;
                hash ^= (byte)(value >> 8);
                hash *= FnvPrime;
                hash ^= (byte)(value >> 16);
                hash *= FnvPrime;
                hash ^= (byte)(value >> 24);
                hash *= FnvPrime;
                return hash;
            }
        }

        private static string BuildSerializedStorageKey(uint modHash, uint keyHash)
        {
            return SaveDictionaryPrefix + modHash.ToString("X8") + ":" + keyHash.ToString("X8");
        }

        private static bool TryParseSerializedStorageKey(string storageKey, out uint modHash, out uint keyHash)
        {
            modHash = 0u;
            keyHash = 0u;
            if (string.IsNullOrEmpty(storageKey) ||
                storageKey.Length != SaveDictionaryPrefix.Length + 17 ||
                !storageKey.StartsWith(SaveDictionaryPrefix, System.StringComparison.Ordinal))
            {
                keyHash = ModCommandDispatcher.ComputeModHash(storageKey);
                return false;
            }

            int modOffset = SaveDictionaryPrefix.Length;
            int separatorOffset = modOffset + 8;
            if (storageKey[separatorOffset] != ':')
            {
                keyHash = ModCommandDispatcher.ComputeModHash(storageKey);
                return false;
            }

            if (!TryParseHexUInt(storageKey, modOffset, out modHash) ||
                !TryParseHexUInt(storageKey, separatorOffset + 1, out keyHash) ||
                modHash == 0u ||
                keyHash == 0u)
            {
                modHash = 0u;
                keyHash = ModCommandDispatcher.ComputeModHash(storageKey);
                return false;
            }

            return true;
        }

        private static bool TryParseHexUInt(string value, int offset, out uint result)
        {
            result = 0u;
            if (value == null || offset < 0 || offset + 8 > value.Length)
                return false;

            for (int i = 0; i < 8; i++)
            {
                char c = value[offset + i];
                uint nibble;
                if (c >= '0' && c <= '9')
                    nibble = (uint)(c - '0');
                else if (c >= 'A' && c <= 'F')
                    nibble = (uint)(c - 'A' + 10);
                else if (c >= 'a' && c <= 'f')
                    nibble = (uint)(c - 'a' + 10);
                else
                    return false;

                result = (result << 4) | nibble;
            }

            return true;
        }

        private static string DecodeUtf16Payload(NativeArray<byte> payloadBytes, int payloadLength)
        {
            if (!payloadBytes.IsCreated || payloadLength <= 0)
                return string.Empty;

            int charCount = payloadLength / sizeof(char);
            for (int i = 0; i < charCount; i++)
            {
                int byteIndex = i * sizeof(char);
                _decodeCharScratch[i] = (char)(payloadBytes[byteIndex] | (payloadBytes[byteIndex + 1] << 8));
            }

            return new string(_decodeCharScratch, 0, charCount);
        }

        private static NativeArray<byte> CreateTempNativeArrayBuffer(int length, string label)
        {
            int safeLength = math.max(1, length);
            NativeArray<byte> buffer = H8Memory.Allocate<byte>(
                safeLength,
                NativeArrayOwnerSystem,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[length] - isolated mod save payload staging buffer - owner: ModSaveStateStore

            if (!buffer.IsCreated || buffer.Length != safeLength)
                throw new System.InvalidOperationException($"{NativeMemoryAllocationFailureMessage} Label={label}.");

            return buffer;
        }

        private static void DisposeTempNativeArrayBuffer(ref NativeArray<byte> buffer, string label)
        {
            if (!buffer.IsCreated)
                return;

            H8Memory.Release(ref buffer, NativeArrayOwnerSystem);

            if (buffer.IsCreated)
                throw new System.InvalidOperationException($"{NativeMemoryReleaseFailureMessage} Label={label}.");
        }
    }

    internal static class ModItemRegistry
    {
        private struct PendingItemRegistration
        {
            public ItemData Data;
            public string ModId;
            public uint ModHash;
        }

        // COLD ALLOC: List<PendingItemRegistration>[16] — deferred item registrations until the runtime item catalog exists — owner: ModItemRegistry
        private static readonly List<PendingItemRegistration> _pendingItems = new List<PendingItemRegistration>(16);
        // COLD ALLOC: List<PendingItemRegistration>[16] — mod-owned item registrations replayed into replacement runtime catalogs — owner: ModItemRegistry
        private static readonly List<PendingItemRegistration> _liveItems = new List<PendingItemRegistration>(16);
        // COLD ALLOC: List<ItemCatalog>[4] — catalogs that received mod-owned runtime items and must be owner-cleaned on disable — owner: ModItemRegistry
        private static readonly List<ItemCatalog> _liveItemCatalogs = new List<ItemCatalog>(4);
        private static IPlayerInventoryService s_playerInventoryService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingItems.Clear();
            _liveItems.Clear();
            _liveItemCatalogs.Clear();
            s_playerInventoryService = null;
        }

        internal static void BindRegistryServicesCold()
        {
            s_playerInventoryService = GlobalRegistry.PlayerInventory;
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.PlayerInventory)
                return;

            s_playerInventoryService = currentService as IPlayerInventoryService;
            ReplayLiveRegistrationsToActiveCatalog();
            FlushPendingRegistrations();
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
            {
                uint modHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u;
                string modId = ResolveActiveOwnerId();
                bool success = catalog.TryRegisterRuntimeItem(itemData, modId, out error);
                if (success)
                {
                    AddOrReplaceLiveItemRegistration(itemData, modId, modHash);
                    TrackLiveCatalog(catalog);
                    ModRegistryEvents.NotifyRuntimeRegistryChanged(modHash);
                }

                return success;
            }

            int existingLiveItemIndex;
            if (TryFindLiveItem(itemData, out existingLiveItemIndex))
            {
                PromoteItemRegistrationOwnerIfUnownedOrSameMod(_liveItems, existingLiveItemIndex);
                PromoteKnownItemCatalogOwnersIfUnownedOrSameMod(itemData);
                return true;
            }

            int existingPendingItemIndex;
            if (TryFindPendingItem(itemData, out existingPendingItemIndex))
            {
                PromoteItemRegistrationOwnerIfUnownedOrSameMod(_pendingItems, existingPendingItemIndex);
                return true;
            }

            _pendingItems.Add(new PendingItemRegistration
            {
                Data = itemData,
                ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,
                ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u
            });
            return true;
        }

        internal static void UnregisterModItems(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            bool removed = false;
            if (RemoveLiveItemRegistrationsForMod(modId))
                removed = true;

            if (UnregisterRuntimeItemsFromKnownCatalogs(modId))
                removed = true;

            for (int i = _pendingItems.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_pendingItems[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _pendingItems.RemoveAt(i);
                removed = true;
            }

            ItemCatalog catalog = ResolveActiveCatalog();
            if (catalog != null && !ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeItemsForOwner(modId))
                removed = true;

            if (removed)
                ModRegistryEvents.NotifyRuntimeRegistryChanged(ModCommandDispatcher.ComputeModHash(modId));
        }

        internal static void FlushPendingRegistrations()
        {
            ItemCatalog catalog = ResolveActiveCatalog();
            if (catalog == null || _pendingItems.Count == 0)
                return;

            bool changed = false;
            for (int i = _pendingItems.Count - 1; i >= 0; i--)
            {
                PendingItemRegistration registration = _pendingItems[i];
                if (!IsPendingOwnerStillRegistered(registration.ModHash))
                {
                    _pendingItems.RemoveAt(i);
                    continue;
                }

                ItemData itemData = registration.Data;
                if (catalog.TryRegisterRuntimeItem(itemData, registration.ModId, out string error))
                {
                    _pendingItems.RemoveAt(i);
                    AddOrReplaceLiveItemRegistration(registration.Data, registration.ModId, registration.ModHash);
                    TrackLiveCatalog(catalog);
                    changed = true;
                    continue;
                }

                Hecton8.Core.H8Debug.LogWarning(
                    $"[ModItemRegistry] Failed to register pending runtime item '{(itemData != null ? itemData.name : "null")}': {error}");
                _pendingItems.RemoveAt(i);
            }

            if (changed)
                ModRegistryEvents.NotifyRuntimeRegistryChanged(0u);
        }

        internal static ItemCatalog ResolveActiveCatalog()
        {
            IPlayerInventoryService inventoryService = s_playerInventoryService;
            PlayerInventory playerInventory = inventoryService != null ? inventoryService.Inventory : null;
            return playerInventory != null ? playerInventory.ItemCatalog : null;
        }

        private static bool IsPendingOwnerStillRegistered(uint modHash)
        {
            return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);
        }

        private static string ResolveActiveOwnerId()
        {
            return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;
        }

        private static void ReplayLiveRegistrationsToActiveCatalog()
        {
            ItemCatalog catalog = ResolveActiveCatalog();
            if (catalog == null || _liveItems.Count == 0)
                return;

            bool changed = false;
            for (int i = _liveItems.Count - 1; i >= 0; i--)
            {
                PendingItemRegistration registration = _liveItems[i];
                if (!IsPendingOwnerStillRegistered(registration.ModHash))
                {
                    _liveItems.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (catalog.TryRegisterRuntimeItem(registration.Data, registration.ModId, out string error))
                {
                    TrackLiveCatalog(catalog);
                    changed = true;
                    continue;
                }

                Hecton8.Core.H8Debug.LogWarning(
                    $"[ModItemRegistry] Failed to replay runtime item '{(registration.Data != null ? registration.Data.name : "null")}': {error}");
                _liveItems.RemoveAt(i);
                changed = true;
            }

            if (changed)
                ModRegistryEvents.NotifyRuntimeRegistryChanged(0u);
        }

        private static void TrackLiveCatalog(ItemCatalog catalog)
        {
            if (catalog == null)
                return;

            for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)
            {
                ItemCatalog existing = _liveItemCatalogs[i];
                if (existing == null)
                {
                    _liveItemCatalogs.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, catalog))
                    return;
            }

            _liveItemCatalogs.Add(catalog);
        }

        private static bool ContainsKnownLiveCatalog(ItemCatalog catalog)
        {
            if (catalog == null)
                return false;

            for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)
            {
                ItemCatalog existing = _liveItemCatalogs[i];
                if (existing == null)
                {
                    _liveItemCatalogs.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, catalog))
                    return true;
            }

            return false;
        }

        private static void AddOrReplaceLiveItemRegistration(ItemData itemData, string modId, uint modHash)
        {
            for (int i = 0; i < _liveItems.Count; i++)
            {
                PendingItemRegistration registration = _liveItems[i];
                ItemData liveItem = registration.Data;
                if (!ReferenceEquals(liveItem, itemData) &&
                    (liveItem == null ||
                     itemData == null ||
                     !string.Equals(liveItem.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal)))
                {
                    continue;
                }

                registration.Data = itemData;
                registration.ModId = modId;
                registration.ModHash = modHash;
                _liveItems[i] = registration;
                return;
            }

            _liveItems.Add(new PendingItemRegistration
            {
                Data = itemData,
                ModId = modId,
                ModHash = modHash
            });
        }

        private static bool RemoveLiveItemRegistrationsForMod(string modId)
        {
            bool removed = false;
            for (int i = _liveItems.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_liveItems[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _liveItems.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private static bool UnregisterRuntimeItemsFromKnownCatalogs(string modId)
        {
            bool removed = false;
            for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)
            {
                ItemCatalog catalog = _liveItemCatalogs[i];
                if (catalog == null)
                {
                    _liveItemCatalogs.RemoveAt(i);
                    continue;
                }

                if (catalog.UnregisterRuntimeItemsForOwner(modId))
                    removed = true;
            }

            return removed;
        }

        private static void PromoteKnownItemCatalogOwnersIfUnownedOrSameMod(ItemData itemData)
        {
            if (!ModExecutionScope.HasActiveMod || itemData == null)
                return;

            string modId = ModExecutionScope.CurrentModId;
            for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)
            {
                ItemCatalog catalog = _liveItemCatalogs[i];
                if (catalog == null)
                {
                    _liveItemCatalogs.RemoveAt(i);
                    continue;
                }

                catalog.TryPromoteRuntimeItemOwnerIfPresent(itemData, modId);
            }
        }

        private static bool ContainsLiveItem(ItemData itemData)
        {
            int unusedIndex;
            return TryFindLiveItem(itemData, out unusedIndex);
        }

        private static bool TryFindLiveItem(ItemData itemData, out int index)
        {
            index = -1;
            for (int i = 0; i < _liveItems.Count; i++)
            {
                ItemData live = _liveItems[i].Data;
                if (ReferenceEquals(live, itemData))
                {
                    index = i;
                    return true;
                }

                if (live != null &&
                    itemData != null &&
                    string.Equals(live.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPendingItem(ItemData itemData)
        {
            int unusedIndex;
            return TryFindPendingItem(itemData, out unusedIndex);
        }

        private static bool TryFindPendingItem(ItemData itemData, out int index)
        {
            index = -1;
            for (int i = 0; i < _pendingItems.Count; i++)
            {
                ItemData pending = _pendingItems[i].Data;
                if (ReferenceEquals(pending, itemData))
                {
                    index = i;
                    return true;
                }

                if (pending != null &&
                    itemData != null &&
                    string.Equals(pending.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static void PromoteItemRegistrationOwnerIfUnownedOrSameMod(List<PendingItemRegistration> registrations, int index)
        {
            if (!ModExecutionScope.HasActiveMod ||
                registrations == null ||
                (uint)index >= (uint)registrations.Count)
            {
                return;
            }

            PendingItemRegistration registration = registrations[index];
            if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)
                return;

            registration.ModId = ModExecutionScope.CurrentModId;
            registration.ModHash = ModExecutionScope.CurrentModHash;
            registrations[index] = registration;
        }
    }

    internal static class ModRecipeRegistry
    {
        private const int MaxRuntimeRecipeCount = Fabricator.MaxRecipeCacheEntries;
        // COLD ALLOC: List<RuntimeRecipeRegistration>[Fabricator.MaxRecipeCacheEntries] — bounded runtime crafting recipe overlay — owner: ModRecipeRegistry
        private struct RuntimeRecipeRegistration
        {
            public RecipeData Data;
            public string ModId;
            public uint ModHash;
        }

        private static readonly List<RuntimeRecipeRegistration> _runtimeRecipes = new List<RuntimeRecipeRegistration>(MaxRuntimeRecipeCount);

        internal static int Count => _runtimeRecipes.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _runtimeRecipes.Clear();
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

            bool removedStaleOwnerRecipes = RemoveStaleOwnerRecipes();

            int existingRecipeIndex;
            if (TryFindRecipeReference(recipeData, out existingRecipeIndex))
            {
                PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(existingRecipeIndex);
                if (removedStaleOwnerRecipes)
                    ModRegistryEvents.NotifyRecipeRegistryChanged();

                return true;
            }

            if (_runtimeRecipes.Count >= MaxRuntimeRecipeCount)
            {
                if (removedStaleOwnerRecipes)
                    ModRegistryEvents.NotifyRecipeRegistryChanged();

                error = "Runtime recipe registry capacity exceeded.";
                return false;
            }

            _runtimeRecipes.Add(new RuntimeRecipeRegistration
            {
                Data = recipeData,
                ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,
                ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u
            });
            ModRegistryEvents.NotifyRecipeRegistryChanged();
            return true;
        }

        internal static void UnregisterModRecipes(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            bool removed = false;
            for (int i = _runtimeRecipes.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_runtimeRecipes[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _runtimeRecipes.RemoveAt(i);
                removed = true;
            }

            if (removed)
                ModRegistryEvents.NotifyRecipeRegistryChanged();
        }

        internal static void FlushPendingRegistrations()
        {
            RemoveStaleOwnerRecipes();
            ModRegistryEvents.NotifyRecipeRegistryChanged();
        }

        internal static RecipeData GetAt(int index)
        {
            if ((uint)index >= (uint)_runtimeRecipes.Count)
                return null;

            return _runtimeRecipes[index].Data;
        }

        private static bool ContainsRecipeReference(RecipeData recipeData)
        {
            int unusedIndex;
            return TryFindRecipeReference(recipeData, out unusedIndex);
        }

        private static bool TryFindRecipeReference(RecipeData recipeData, out int index)
        {
            index = -1;
            for (int i = 0; i < _runtimeRecipes.Count; i++)
            {
                if (ReferenceEquals(_runtimeRecipes[i].Data, recipeData))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static void PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(int index)
        {
            if (!ModExecutionScope.HasActiveMod || (uint)index >= (uint)_runtimeRecipes.Count)
                return;

            RuntimeRecipeRegistration registration = _runtimeRecipes[index];
            if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)
                return;

            registration.ModId = ModExecutionScope.CurrentModId;
            registration.ModHash = ModExecutionScope.CurrentModHash;
            _runtimeRecipes[index] = registration;
        }

        private static bool RemoveStaleOwnerRecipes()
        {
            bool removed = false;
            for (int i = _runtimeRecipes.Count - 1; i >= 0; i--)
            {
                if (IsRuntimeOwnerStillRegistered(_runtimeRecipes[i].ModHash))
                    continue;

                _runtimeRecipes.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private static bool IsRuntimeOwnerStillRegistered(uint modHash)
        {
            return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);
        }
    }

    internal static class ModBuildableRegistry
    {
        private const string DefaultCategory = "Mods";

        private struct PendingBuildableRegistration
        {
            public BuildableData Data;
            public string CustomCategory;
            public string ModId;
            public uint ModHash;
        }

        // COLD ALLOC: List<PendingBuildableRegistration>[16] — deferred buildable registrations until the live module catalog exists — owner: ModBuildableRegistry
        private static readonly List<PendingBuildableRegistration> _pendingBuildables = new List<PendingBuildableRegistration>(16);
        // COLD ALLOC: List<PendingBuildableRegistration>[16] — mod-owned buildable registrations replayed into replacement runtime catalogs — owner: ModBuildableRegistry
        private static readonly List<PendingBuildableRegistration> _liveBuildables = new List<PendingBuildableRegistration>(16);
        // COLD ALLOC: List<ModuleCatalog>[4] — catalogs that received mod-owned runtime buildables and must be owner-cleaned on disable — owner: ModBuildableRegistry
        private static readonly List<ModuleCatalog> _liveModuleCatalogs = new List<ModuleCatalog>(4);
        private static ILogisticsService s_logisticsService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingBuildables.Clear();
            _liveBuildables.Clear();
            _liveModuleCatalogs.Clear();
            s_logisticsService = null;
        }

        internal static void BindRegistryServicesCold()
        {
            s_logisticsService = GlobalRegistry.Logistics;
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Logistics)
                return;

            s_logisticsService = currentService as ILogisticsService;
            ReplayLiveRegistrationsToActiveCatalog();
            FlushPendingRegistrations();
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
                string normalizedCategory = NormalizeCategory(customCategory);
                string modId = ResolveActiveOwnerId();
                uint modHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u;
                bool success = catalog.TryRegisterRuntimeModule(buildableData, normalizedCategory, modId, out error);
                if (success)
                {
                    AddOrReplaceLiveBuildableRegistration(buildableData, normalizedCategory, modId, modHash);
                    TrackLiveCatalog(catalog);
                    ModRegistryEvents.NotifyBuildableRegistryChanged();
                }

                return success;
            }

            int existingLiveBuildableIndex;
            if (TryFindLiveBuildable(buildableData, out existingLiveBuildableIndex))
            {
                PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_liveBuildables, existingLiveBuildableIndex, customCategory);
                PromoteKnownModuleCatalogOwnersIfUnownedOrSameMod(buildableData, customCategory);
                return true;
            }

            int existingPendingBuildableIndex;
            if (TryFindPendingBuildable(buildableData, out existingPendingBuildableIndex))
            {
                PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_pendingBuildables, existingPendingBuildableIndex, customCategory);
                return true;
            }

            if (HasPendingAliasConflict(buildableData, out error))
                return false;

            _pendingBuildables.Add(new PendingBuildableRegistration
            {
                Data = buildableData,
                CustomCategory = NormalizeCategory(customCategory),
                ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,
                ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u
            });

            ModRegistryEvents.NotifyBuildableRegistryChanged();
            return true;
        }

        internal static void UnregisterModBuildables(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            bool removed = false;
            if (RemoveLiveBuildableRegistrationsForMod(modId))
                removed = true;

            if (UnregisterRuntimeBuildablesFromKnownCatalogs(modId))
                removed = true;

            for (int i = _pendingBuildables.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_pendingBuildables[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _pendingBuildables.RemoveAt(i);
                removed = true;
            }

            ModuleCatalog catalog = ResolveActiveCatalog();
            if (catalog != null && !ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeModulesForOwner(modId))
                removed = true;

            if (removed)
                ModRegistryEvents.NotifyBuildableRegistryChanged();
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
                if (!IsPendingOwnerStillRegistered(registration.ModHash))
                {
                    _pendingBuildables.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, registration.ModId, out string error))
                {
                    _pendingBuildables.RemoveAt(i);
                    AddOrReplaceLiveBuildableRegistration(registration.Data, registration.CustomCategory, registration.ModId, registration.ModHash);
                    TrackLiveCatalog(catalog);
                    changed = true;
                    continue;
                }

                Hecton8.Core.H8Debug.LogWarning(
                    $"[ModBuildableRegistry] Failed to register pending buildable '{(registration.Data != null ? registration.Data.name : "null")}': {error}");
                _pendingBuildables.RemoveAt(i);
            }

            if (changed)
                ModRegistryEvents.NotifyBuildableRegistryChanged();
        }

        internal static ModuleCatalog ResolveActiveCatalog()
        {
            ILogisticsService logistics = s_logisticsService;
            return logistics != null ? logistics.Catalog : null;
        }

        private static bool IsPendingOwnerStillRegistered(uint modHash)
        {
            return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);
        }

        private static string ResolveActiveOwnerId()
        {
            return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;
        }

        private static void ReplayLiveRegistrationsToActiveCatalog()
        {
            ModuleCatalog catalog = ResolveActiveCatalog();
            if (catalog == null || _liveBuildables.Count == 0)
                return;

            bool changed = false;
            for (int i = _liveBuildables.Count - 1; i >= 0; i--)
            {
                PendingBuildableRegistration registration = _liveBuildables[i];
                if (!IsPendingOwnerStillRegistered(registration.ModHash))
                {
                    _liveBuildables.RemoveAt(i);
                    changed = true;
                    continue;
                }

                if (catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, registration.ModId, out string error))
                {
                    TrackLiveCatalog(catalog);
                    changed = true;
                    continue;
                }

                Hecton8.Core.H8Debug.LogWarning(
                    $"[ModBuildableRegistry] Failed to replay runtime buildable '{(registration.Data != null ? registration.Data.name : "null")}': {error}");
                _liveBuildables.RemoveAt(i);
                changed = true;
            }

            if (changed)
                ModRegistryEvents.NotifyBuildableRegistryChanged();
        }

        private static void TrackLiveCatalog(ModuleCatalog catalog)
        {
            if (catalog == null)
                return;

            for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)
            {
                ModuleCatalog existing = _liveModuleCatalogs[i];
                if (existing == null)
                {
                    _liveModuleCatalogs.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, catalog))
                    return;
            }

            _liveModuleCatalogs.Add(catalog);
        }

        private static bool ContainsKnownLiveCatalog(ModuleCatalog catalog)
        {
            if (catalog == null)
                return false;

            for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)
            {
                ModuleCatalog existing = _liveModuleCatalogs[i];
                if (existing == null)
                {
                    _liveModuleCatalogs.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(existing, catalog))
                    return true;
            }

            return false;
        }

        private static void AddOrReplaceLiveBuildableRegistration(
            BuildableData buildableData,
            string customCategory,
            string modId,
            uint modHash)
        {
            for (int i = 0; i < _liveBuildables.Count; i++)
            {
                PendingBuildableRegistration registration = _liveBuildables[i];
                BuildableData liveBuildable = registration.Data;
                if (!ReferenceEquals(liveBuildable, buildableData) &&
                    (liveBuildable == null ||
                     buildableData == null ||
                     !string.Equals(liveBuildable.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal)))
                {
                    continue;
                }

                registration.Data = buildableData;
                registration.CustomCategory = NormalizeCategory(customCategory);
                registration.ModId = modId;
                registration.ModHash = modHash;
                _liveBuildables[i] = registration;
                return;
            }

            _liveBuildables.Add(new PendingBuildableRegistration
            {
                Data = buildableData,
                CustomCategory = NormalizeCategory(customCategory),
                ModId = modId,
                ModHash = modHash
            });
        }

        private static bool RemoveLiveBuildableRegistrationsForMod(string modId)
        {
            bool removed = false;
            for (int i = _liveBuildables.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_liveBuildables[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _liveBuildables.RemoveAt(i);
                removed = true;
            }

            return removed;
        }

        private static bool UnregisterRuntimeBuildablesFromKnownCatalogs(string modId)
        {
            bool removed = false;
            for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)
            {
                ModuleCatalog catalog = _liveModuleCatalogs[i];
                if (catalog == null)
                {
                    _liveModuleCatalogs.RemoveAt(i);
                    continue;
                }

                if (catalog.UnregisterRuntimeModulesForOwner(modId))
                    removed = true;
            }

            return removed;
        }

        private static void PromoteKnownModuleCatalogOwnersIfUnownedOrSameMod(BuildableData buildableData, string customCategory)
        {
            if (!ModExecutionScope.HasActiveMod || buildableData == null)
                return;

            string modId = ModExecutionScope.CurrentModId;
            string normalizedCategory = NormalizeCategory(customCategory);
            for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)
            {
                ModuleCatalog catalog = _liveModuleCatalogs[i];
                if (catalog == null)
                {
                    _liveModuleCatalogs.RemoveAt(i);
                    continue;
                }

                catalog.TryPromoteRuntimeModuleOwnerIfPresent(buildableData, normalizedCategory, modId);
            }
        }

        private static bool ContainsLiveBuildable(BuildableData buildableData)
        {
            int unusedIndex;
            return TryFindLiveBuildable(buildableData, out unusedIndex);
        }

        private static bool TryFindLiveBuildable(BuildableData buildableData, out int index)
        {
            index = -1;
            for (int i = 0; i < _liveBuildables.Count; i++)
            {
                BuildableData live = _liveBuildables[i].Data;
                if (ReferenceEquals(live, buildableData))
                {
                    index = i;
                    return true;
                }

                if (live != null &&
                    buildableData != null &&
                    string.Equals(live.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeCategory(string customCategory)
        {
            return string.IsNullOrWhiteSpace(customCategory) ? DefaultCategory : customCategory.Trim();
        }

        private static bool ContainsPendingBuildable(BuildableData buildableData)
        {
            int unusedIndex;
            return TryFindPendingBuildable(buildableData, out unusedIndex);
        }

        private static bool TryFindPendingBuildable(BuildableData buildableData, out int index)
        {
            index = -1;
            for (int i = 0; i < _pendingBuildables.Count; i++)
            {
                PendingBuildableRegistration pending = _pendingBuildables[i];
                if (ReferenceEquals(pending.Data, buildableData))
                {
                    index = i;
                    return true;
                }

                if (pending.Data != null &&
                    string.Equals(pending.Data.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        private static void PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(
            List<PendingBuildableRegistration> registrations,
            int index,
            string customCategory)
        {
            if (!ModExecutionScope.HasActiveMod ||
                registrations == null ||
                (uint)index >= (uint)registrations.Count)
            {
                return;
            }

            PendingBuildableRegistration registration = registrations[index];
            if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)
                return;

            registration.CustomCategory = NormalizeCategory(customCategory);
            registration.ModId = ModExecutionScope.CurrentModId;
            registration.ModHash = ModExecutionScope.CurrentModHash;
            registrations[index] = registration;
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

        internal static void UnregisterModRecycleYields(string modId)
        {
            RecyclingRegistry.ClearOwner(modId);
        }
    }

    internal static class ModEcosystemRegistry
    {
        private const int MaxRuntimeMutationCount = 16;

        private struct RuntimeBiomeMutationRegistration
        {
            public FaunaBiomeMutationDefinition Data;
            public string ModId;
            public uint ModHash;
        }

        // COLD ALLOC: List<RuntimeBiomeMutationRegistration>[16] - runtime-only biome mutation overlay registry - owner: ModEcosystemRegistry
        private static readonly List<RuntimeBiomeMutationRegistration> _runtimeMutations = new List<RuntimeBiomeMutationRegistration>(MaxRuntimeMutationCount);

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

            RemoveStaleOwnerMutations();

            int existingMutationIndex;
            if (TryFindMatchingDefinition(definition, out existingMutationIndex))
            {
                PromoteRuntimeMutationOwnerIfUnownedOrSameMod(existingMutationIndex);
                return true;
            }

            if (_runtimeMutations.Count >= MaxRuntimeMutationCount)
            {
                error = "Runtime biome mutation registry capacity exceeded.";
                return false;
            }

            _runtimeMutations.Add(new RuntimeBiomeMutationRegistration
            {
                Data = CloneDefinition(definition),
                ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,
                ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u
            });
            return true;
        }

        internal static void UnregisterModBiomeMutations(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return;

            for (int i = _runtimeMutations.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_runtimeMutations[i].ModId, modId, System.StringComparison.Ordinal))
                    continue;

                _runtimeMutations.RemoveAt(i);
            }
        }

        internal static FaunaBiomeMutationDefinition GetAt(int index)
        {
            if ((uint)index >= (uint)_runtimeMutations.Count)
                return null;

            return _runtimeMutations[index].Data;
        }

        private static bool ContainsMatchingDefinition(FaunaBiomeMutationDefinition definition)
        {
            int unusedIndex;
            return TryFindMatchingDefinition(definition, out unusedIndex);
        }

        private static bool TryFindMatchingDefinition(FaunaBiomeMutationDefinition definition, out int index)
        {
            index = -1;
            for (int i = 0; i < _runtimeMutations.Count; i++)
            {
                FaunaBiomeMutationDefinition existing = _runtimeMutations[i].Data;
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

                index = i;
                return true;
            }

            return false;
        }

        private static void PromoteRuntimeMutationOwnerIfUnownedOrSameMod(int index)
        {
            if (!ModExecutionScope.HasActiveMod || (uint)index >= (uint)_runtimeMutations.Count)
                return;

            RuntimeBiomeMutationRegistration registration = _runtimeMutations[index];
            if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)
                return;

            registration.ModId = ModExecutionScope.CurrentModId;
            registration.ModHash = ModExecutionScope.CurrentModHash;
            _runtimeMutations[index] = registration;
        }

        private static void RemoveStaleOwnerMutations()
        {
            for (int i = _runtimeMutations.Count - 1; i >= 0; i--)
            {
                if (IsRuntimeOwnerStillRegistered(_runtimeMutations[i].ModHash))
                    continue;

                _runtimeMutations.RemoveAt(i);
            }
        }

        private static bool IsRuntimeOwnerStillRegistered(uint modHash)
        {
            return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);
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
