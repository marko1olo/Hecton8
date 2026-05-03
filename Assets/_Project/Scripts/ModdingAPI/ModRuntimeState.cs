using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Crafting;
using Hecton8.Economy;
using Hecton8.Ecosystem;
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

        internal static string CurrentModId => string.IsNullOrWhiteSpace(_currentModId) ? "anonymous" : _currentModId;
        internal static uint CurrentModHash => _currentModHash;
        internal static bool HasActiveMod => _scopeDepth > 0;

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
                _previousModId = _currentModId;
                _previousModHash = _currentModHash;
                _previousScopeDepth = _scopeDepth;
                _currentModId = string.IsNullOrWhiteSpace(modId) ? "anonymous" : modId;
                _currentModHash = modHash != 0u ? modHash : ModCommandDispatcher.ComputeModHash(_currentModId);
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
            _customModIndexByHash.Clear();
        }

        internal static void SetModString(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[ModSaveStateStore] Refused to write mod save data with an empty key.");
                return;
            }

            uint keyHash = ModCommandDispatcher.ComputeModHash(key);
            uint modHash = ResolvePersistenceOwnerHash(key);
            uint compoundHash = ComputeCompoundPersistenceHash(modHash, keyHash);
            if (_customModIndexByHash.TryGetValue(compoundHash, out int index) && index >= 0 && index < _customModData.Count)
            {
                ModSaveEntry entry = _customModData[index];
                if (entry.Key == key || string.IsNullOrEmpty(entry.Key) || entry.KeyHash == keyHash)
                {
                    entry.Key = key;
                    entry.Value = value ?? string.Empty;
                    entry.SerializedKey = BuildSerializedStorageKey(modHash, keyHash);
                    entry.KeyHash = keyHash;
                    entry.ModHash = modHash;
                    entry.CompoundHash = compoundHash;
                    _customModData[index] = entry;
                    return;
                }
            }

            for (int i = 0; i < _customModData.Count; i++)
            {
                ModSaveEntry entry = _customModData[i];
                if (!MatchesPersistenceEntry(in entry, key, keyHash, modHash, compoundHash))
                    continue;

                if (entry.CompoundHash != 0u && entry.CompoundHash != compoundHash)
                    _customModIndexByHash.Remove(entry.CompoundHash);

                entry.Key = key;
                entry.Value = value ?? string.Empty;
                entry.SerializedKey = BuildSerializedStorageKey(modHash, keyHash);
                entry.KeyHash = keyHash;
                entry.ModHash = modHash;
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
                SerializedKey = BuildSerializedStorageKey(modHash, keyHash),
                KeyHash = keyHash,
                ModHash = modHash,
                CompoundHash = compoundHash
            });
        }

        internal static string GetModString(string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
                return defaultValue ?? string.Empty;

            uint keyHash = ModCommandDispatcher.ComputeModHash(key);
            uint modHash = ResolvePersistenceOwnerHash(key);
            uint compoundHash = ComputeCompoundPersistenceHash(modHash, keyHash);
            if (_customModIndexByHash.TryGetValue(compoundHash, out int index) && index >= 0 && index < _customModData.Count)
            {
                ModSaveEntry entry = _customModData[index];
                if (MatchesPersistenceEntry(in entry, key, keyHash, modHash, compoundHash))
                    return entry.Value ?? string.Empty;
            }

            for (int i = 0; i < _customModData.Count; i++)
            {
                ModSaveEntry entry = _customModData[i];
                if (MatchesPersistenceEntry(in entry, key, keyHash, modHash, compoundHash))
                    return entry.Value ?? string.Empty;
            }

            return defaultValue ?? string.Empty;
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
                _customModIndexByHash[compoundHash] = _customModData.Count;
                _customModData.Add(new ModSaveEntry
                {
                    Key = isNamespaced ? string.Empty : key,
                    Value = enumerator.Current.Value ?? string.Empty,
                    SerializedKey = isNamespaced ? key : string.Empty,
                    KeyHash = keyHash,
                    ModHash = modHash,
                    CompoundHash = compoundHash
                });
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
                payloadBytes = new NativeArray<byte>(
                    math.max(1, SaveBinaryStorage.ModPayloadMaxBytes),
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < _customModData.Count; i++)
                {
                    ModSaveEntry entry = _customModData[i];
                    if (entry.ModHash == 0u || entry.KeyHash == 0u)
                        continue;

                    string value = entry.Value ?? string.Empty;
                    int payloadLength = value.Length * sizeof(char);
                    if (payloadLength > SaveBinaryStorage.ModPayloadMaxBytes)
                    {
                        error = "Mod payload exceeds isolated sub-sector budget.";
                        continue;
                    }

                    for (int charIndex = 0; charIndex < value.Length; charIndex++)
                    {
                        char character = value[charIndex];
                        int byteIndex = charIndex * sizeof(char);
                        payloadBytes[byteIndex] = (byte)(character & 0xFF);
                        payloadBytes[byteIndex + 1] = (byte)(character >> 8);
                    }

                    string tempOverridePath = absoluteSavePath +
                                              ".mod_" +
                                              entry.ModHash.ToString("X8") +
                                              "_" +
                                              entry.KeyHash.ToString("X8") +
                                              ".sectmp";

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
                if (payloadBytes.IsCreated)
                    payloadBytes.Dispose();
            }

            return string.IsNullOrEmpty(error);
        }

        internal static bool TryLoadMmfPayloads(string absoluteSavePath, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(absoluteSavePath))
                return true;

            NativeArray<byte> payloadBytes = default;
            try
            {
                payloadBytes = new NativeArray<byte>(
                    math.max(1, SaveBinaryStorage.ModPayloadMaxBytes),
                    Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

                return SaveBinaryStorage.TryReadIndexedModPayloads(
                    absoluteSavePath,
                    null,
                    payloadBytes,
                    _modPayloadReadHandler,
                    out error);
            }
            finally
            {
                if (payloadBytes.IsCreated)
                    payloadBytes.Dispose();
            }
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

        private static uint ResolvePersistenceOwnerHash(string key)
        {
            uint currentModHash = ModExecutionScope.CurrentModHash;
            return currentModHash != 0u
                ? currentModHash
                : ModCommandDispatcher.ComputeModHash(key);
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
            PlayerInventory playerInventory = Hecton8.Core.GlobalRegistry.PlayerInventoryRuntime;
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

            if (ContainsRecipeReference(recipeData))
                return true;

            _runtimeRecipes.Add(recipeData);
            ModRegistryEvents.NotifyRecipeRegistryChanged();
            return true;
        }

        internal static void FlushPendingRegistrations()
        {
            ModRegistryEvents.NotifyRecipeRegistryChanged();
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingBuildables.Clear();
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
                    ModRegistryEvents.NotifyBuildableRegistryChanged();

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

            ModRegistryEvents.NotifyBuildableRegistryChanged();
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
                ModRegistryEvents.NotifyBuildableRegistryChanged();
        }

        internal static ModuleCatalog ResolveActiveCatalog()
        {
            ConstructionManager constructionManager = Hecton8.Core.GlobalRegistry.ConstructionRuntime;
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
