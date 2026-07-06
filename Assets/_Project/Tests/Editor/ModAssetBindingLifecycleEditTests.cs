using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModAssetBindingLifecycleEditTests
    {
        [Test]
        public void ModAssetManager_UnloadsCachedBundleWhenBindingChangesOrIsRemoved()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs");
            string register = ExtractMethodBody(source, "internal static void RegisterCatalogPath(string modId, string catalogPath)");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterCatalogPath(string modId)");
            string unloadModAssets = ExtractMethodBody(source, "private static void UnloadModAssets(uint modHash)");
            string unloadAll = ExtractMethodBody(source, "private static void UnloadAllCatalogs()");

            StringAssert.Contains("UnloadModAssets(modHash);", register);
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "if (ModLoader.GetIsFutureCommandEnvelopeOnly())",
                "UnloadModAssets(modHash);",
                "return;"));
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))",
                "UnloadModAssets(modHash);",
                "return;"));
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "if (_loadedCatalogs.ContainsKey(modHash))",
                "UnloadModAssets(modHash);"));

            StringAssert.Contains("UnloadModAssets(modHash);", unregister);
            StringAssert.Contains("Addressables.Release(catalogHandle);", unloadModAssets);
            StringAssert.Contains("_loadedCatalogs.Remove(modHash);", unloadModAssets);
            StringAssert.Contains("Addressables.Release(kvp.Value);", unloadAll);
            StringAssert.Contains("_loadedCatalogs.Clear();", unloadAll);
        }

        [Test]
        public void ModAssetManager_EvictsRawTextureCacheForRemovedModBinding()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModAssetManager.cs");

            // Verify security: ModAssetManager must not support loose file loading or raw texture caching
            StringAssert.DoesNotContain("LoadRawTexture", source);
            StringAssert.DoesNotContain("UnloadRawTexturesForMod", source);
            StringAssert.DoesNotContain("_rawTextures", source);
            StringAssert.DoesNotContain("_rawTextureModHashes", source);
            StringAssert.DoesNotContain("File.ReadAllBytes", source);
            StringAssert.DoesNotContain("LoadImage", source);
        }

        [Test]
        public void ModLoader_UnregistersAssetBindingWhenModIsDisabledOrShutdown()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModLoader.cs");
            string disableCandidate = ExtractMethodBody(source, "private static void DisableCandidate(ModCandidate candidate, string reason)");
            string disableMod = ExtractMethodBody(source, "internal static void DisableMod(string modId, string reason)");

            Assert.IsTrue(ContainsTokensInOrder(
                disableCandidate,
                "candidate.IsDisabled = true;",
                "candidate.DisabledReason = reason;",
                "ModAssetManager.UnregisterCatalogPath(candidate.Metadata.Id);",
                "ModResourceRegistry.UnregisterModResources(candidate.Metadata.Id);",
                "ModSettingsRegistry.UnregisterModSettings(candidate.Metadata.Id);",
                "ModItemRegistry.UnregisterModItems(candidate.Metadata.Id);",
                "ModRecipeRegistry.UnregisterModRecipes(candidate.Metadata.Id);",
                "ModRecycleRegistry.UnregisterModRecycleYields(candidate.Metadata.Id);",
                "ModEcosystemRegistry.UnregisterModBiomeMutations(candidate.Metadata.Id);",
                "ModBuildableRegistry.UnregisterModBuildables(candidate.Metadata.Id);",
                "RecordRuntimeInfo(new ModRuntimeInfo"));

            Assert.IsTrue(ContainsTokensInOrder(
                disableMod,
                "HectonEventBus.DisableSubscriber(modId);",
                "ModCommandDispatcher.QuarantineMod(modId);",
                "ModAssetManager.UnregisterCatalogPath(modId);",
                "ModResourceRegistry.UnregisterModResources(modId);",
                "ModSettingsRegistry.UnregisterModSettings(modId);",
                "ModItemRegistry.UnregisterModItems(modId);",
                "ModRecipeRegistry.UnregisterModRecipes(modId);",
                "ModRecycleRegistry.UnregisterModRecycleYields(modId);",
                "ModEcosystemRegistry.UnregisterModBiomeMutations(modId);",
                "ModBuildableRegistry.UnregisterModBuildables(modId);"));
        }

        [Test]
        public void ModItemRegistry_DropsPendingAndLiveCatalogItemsForDisabledOwnerBeforeGameplayReadersResolve()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string itemCatalog = ReadProjectFile("Assets/_Project/Scripts/ItemCatalog.cs");
            string playerInventory = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string pdaInventory = ReadProjectFile("Assets/_Project/Scripts/PDAInventoryTab.cs");
            string register = ExtractMethodBody(source, "internal static bool TryRegister(ItemData itemData, out string error)");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterModItems(string modId)");
            string flush = ExtractMethodBody(source, "internal static void FlushPendingRegistrations()");
            string serviceReplaced = ExtractMethodBody(source, "internal static void OnGlobalRegistryServiceReplaced(");
            string ownerGuard = ExtractMethodBody(source, "private static bool IsPendingOwnerStillRegistered(uint modHash)");
            string activeOwner = ExtractMethodBody(source, "private static string ResolveActiveOwnerId()");
            string replay = ExtractMethodBody(source, "private static void ReplayLiveRegistrationsToActiveCatalog()");
            string addLive = ExtractMethodBody(source, "private static void AddOrReplaceLiveItemRegistration(ItemData itemData, string modId, uint modHash)");
            string removeLive = ExtractMethodBody(source, "private static bool RemoveLiveItemRegistrationsForMod(string modId)");
            string trackCatalog = ExtractMethodBody(source, "private static void TrackLiveCatalog(ItemCatalog catalog)");
            string knownCatalog = ExtractMethodBody(source, "private static bool ContainsKnownLiveCatalog(ItemCatalog catalog)");
            string unregisterKnownCatalogs = ExtractMethodBody(source, "private static bool UnregisterRuntimeItemsFromKnownCatalogs(string modId)");
            string promoteKnownCatalogOwners = ExtractMethodBody(source, "private static void PromoteKnownItemCatalogOwnersIfUnownedOrSameMod(ItemData itemData)");
            string containsLive = ExtractMethodBody(source, "private static bool ContainsLiveItem(ItemData itemData)");
            string findLive = ExtractMethodBody(source, "private static bool TryFindLiveItem(ItemData itemData, out int index)");
            string contains = ExtractMethodBody(source, "private static bool ContainsPendingItem(ItemData itemData)");
            string findPending = ExtractMethodBody(source, "private static bool TryFindPendingItem(ItemData itemData, out int index)");
            string promoteRegistrationOwner = ExtractMethodBody(source, "private static void PromoteItemRegistrationOwnerIfUnownedOrSameMod(");
            string publicCatalogRegister = ExtractMethodBody(itemCatalog, "public bool TryRegisterRuntimeItem(ItemData item, out string error)");
            string ownedCatalogRegister = ExtractMethodBody(itemCatalog, "internal bool TryRegisterRuntimeItem(ItemData item, string ownerId, out string error)");
            string catalogUnregister = ExtractMethodBody(itemCatalog, "internal bool UnregisterRuntimeItemsForOwner(string ownerId)");
            string catalogPromoteOwner = ExtractMethodBody(itemCatalog, "internal bool TryPromoteRuntimeItemOwnerIfPresent(ItemData item, string ownerId)");
            string ownerRecorder = ExtractMethodBody(itemCatalog, "private void RecordRuntimeItemOwner(string persistentId, string ownerId)");
            string ownerPromoter = ExtractMethodBody(itemCatalog, "private void RecordRuntimeItemOwnerIfUnownedOrSameOwner(string persistentId, string ownerId)");

            StringAssert.Contains("private struct PendingItemRegistration", source);
            StringAssert.Contains("private static readonly List<PendingItemRegistration> _pendingItems", source);
            StringAssert.Contains("private static readonly List<PendingItemRegistration> _liveItems", source);
            StringAssert.Contains("private static readonly List<ItemCatalog> _liveItemCatalogs", source);
            StringAssert.Contains("private Dictionary<string, string> _runtimeItemOwnerByPersistentId;", itemCatalog);
            StringAssert.Contains("return TryRegisterRuntimeItem(item, string.Empty, out error);", publicCatalogRegister);
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "uint modHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u;",
                "string modId = ResolveActiveOwnerId();",
                "bool success = catalog.TryRegisterRuntimeItem(itemData, modId, out error);",
                "if (success)",
                "AddOrReplaceLiveItemRegistration(itemData, modId, modHash);",
                "TrackLiveCatalog(catalog);",
                "ModRegistryEvents.NotifyRuntimeRegistryChanged(modHash);",
                "int existingLiveItemIndex;",
                "if (TryFindLiveItem(itemData, out existingLiveItemIndex))",
                "PromoteItemRegistrationOwnerIfUnownedOrSameMod(_liveItems, existingLiveItemIndex);",
                "PromoteKnownItemCatalogOwnersIfUnownedOrSameMod(itemData);",
                "return true;",
                "int existingPendingItemIndex;",
                "if (TryFindPendingItem(itemData, out existingPendingItemIndex))",
                "PromoteItemRegistrationOwnerIfUnownedOrSameMod(_pendingItems, existingPendingItemIndex);",
                "return true;",
                "_pendingItems.Add(new PendingItemRegistration",
                "Data = itemData,",
                "ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,",
                "ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u"));
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "bool removed = false;",
                "if (RemoveLiveItemRegistrationsForMod(modId))",
                "removed = true;",
                "if (UnregisterRuntimeItemsFromKnownCatalogs(modId))",
                "removed = true;",
                "for (int i = _pendingItems.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_pendingItems[i].ModId, modId, System.StringComparison.Ordinal))",
                "continue;",
                "_pendingItems.RemoveAt(i);",
                "removed = true;",
                "ItemCatalog catalog = ResolveActiveCatalog();",
                "if (catalog != null && !ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeItemsForOwner(modId))",
                "removed = true;",
                "if (removed)",
                "ModRegistryEvents.NotifyRuntimeRegistryChanged(ModCommandDispatcher.ComputeModHash(modId));"));
            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "PendingItemRegistration registration = _pendingItems[i];",
                "if (!IsPendingOwnerStillRegistered(registration.ModHash))",
                "_pendingItems.RemoveAt(i);",
                "continue;",
                "ItemData itemData = registration.Data;",
                "catalog.TryRegisterRuntimeItem(itemData, registration.ModId, out string error)",
                "AddOrReplaceLiveItemRegistration(registration.Data, registration.ModId, registration.ModHash);",
                "TrackLiveCatalog(catalog);",
                "changed = true;",
                "if (changed)",
                "ModRegistryEvents.NotifyRuntimeRegistryChanged(0u);"));
            Assert.IsTrue(ContainsTokensInOrder(
                serviceReplaced,
                "if (serviceSlot != GlobalRegistryServiceSlot.PlayerInventory)",
                "return;",
                "s_playerInventoryService = currentService as IPlayerInventoryService;",
                "ReplayLiveRegistrationsToActiveCatalog();",
                "FlushPendingRegistrations();"));
            StringAssert.Contains("return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);", ownerGuard);
            StringAssert.Contains("return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;", activeOwner);
            Assert.IsTrue(ContainsTokensInOrder(
                replay,
                "ItemCatalog catalog = ResolveActiveCatalog();",
                "for (int i = _liveItems.Count - 1; i >= 0; i--)",
                "PendingItemRegistration registration = _liveItems[i];",
                "if (!IsPendingOwnerStillRegistered(registration.ModHash))",
                "_liveItems.RemoveAt(i);",
                "if (catalog.TryRegisterRuntimeItem(registration.Data, registration.ModId, out string error))",
                "TrackLiveCatalog(catalog);",
                "changed = true;",
                "Hecton8.Core.H8Debug.LogWarning(",
                "_liveItems.RemoveAt(i);",
                "if (changed)",
                "ModRegistryEvents.NotifyRuntimeRegistryChanged(0u);"));
            Assert.IsTrue(ContainsTokensInOrder(
                addLive,
                "for (int i = 0; i < _liveItems.Count; i++)",
                "ItemData liveItem = registration.Data;",
                "string.Equals(liveItem.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal)",
                "registration.Data = itemData;",
                "registration.ModId = modId;",
                "registration.ModHash = modHash;",
                "_liveItems[i] = registration;",
                "_liveItems.Add(new PendingItemRegistration"));
            Assert.IsTrue(ContainsTokensInOrder(
                removeLive,
                "for (int i = _liveItems.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_liveItems[i].ModId, modId, System.StringComparison.Ordinal))",
                "_liveItems.RemoveAt(i);",
                "removed = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                trackCatalog,
                "for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)",
                "ItemCatalog existing = _liveItemCatalogs[i];",
                "_liveItemCatalogs.RemoveAt(i);",
                "if (ReferenceEquals(existing, catalog))",
                "return;",
                "_liveItemCatalogs.Add(catalog);"));
            Assert.IsTrue(ContainsTokensInOrder(
                knownCatalog,
                "for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)",
                "ItemCatalog existing = _liveItemCatalogs[i];",
                "_liveItemCatalogs.RemoveAt(i);",
                "if (ReferenceEquals(existing, catalog))",
                "return true;",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                unregisterKnownCatalogs,
                "for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)",
                "ItemCatalog catalog = _liveItemCatalogs[i];",
                "_liveItemCatalogs.RemoveAt(i);",
                "if (catalog.UnregisterRuntimeItemsForOwner(modId))",
                "removed = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                promoteKnownCatalogOwners,
                "if (!ModExecutionScope.HasActiveMod || itemData == null)",
                "return;",
                "string modId = ModExecutionScope.CurrentModId;",
                "for (int i = _liveItemCatalogs.Count - 1; i >= 0; i--)",
                "ItemCatalog catalog = _liveItemCatalogs[i];",
                "_liveItemCatalogs.RemoveAt(i);",
                "catalog.TryPromoteRuntimeItemOwnerIfPresent(itemData, modId);"));
            StringAssert.Contains("return TryFindLiveItem(itemData, out unusedIndex);", containsLive);
            Assert.IsTrue(ContainsTokensInOrder(
                findLive,
                "index = -1;",
                "for (int i = 0; i < _liveItems.Count; i++)",
                "ItemData live = _liveItems[i].Data;",
                "if (ReferenceEquals(live, itemData))",
                "index = i;",
                "return true;",
                "string.Equals(live.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal)",
                "index = i;",
                "return true;",
                "return false;"));
            StringAssert.Contains("return TryFindPendingItem(itemData, out unusedIndex);", contains);
            Assert.IsTrue(ContainsTokensInOrder(
                findPending,
                "index = -1;",
                "for (int i = 0; i < _pendingItems.Count; i++)",
                "ItemData pending = _pendingItems[i].Data;",
                "if (ReferenceEquals(pending, itemData))",
                "index = i;",
                "return true;",
                "string.Equals(pending.PersistentId, itemData.PersistentId, System.StringComparison.Ordinal)",
                "index = i;",
                "return true;",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                promoteRegistrationOwner,
                "if (!ModExecutionScope.HasActiveMod ||",
                "registrations == null ||",
                "(uint)index >= (uint)registrations.Count)",
                "return;",
                "PendingItemRegistration registration = registrations[index];",
                "if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)",
                "return;",
                "registration.ModId = ModExecutionScope.CurrentModId;",
                "registration.ModHash = ModExecutionScope.CurrentModHash;",
                "registrations[index] = registration;"));
            Assert.IsTrue(ContainsTokensInOrder(
                ownedCatalogRegister,
                "if (ContainsRuntimeItem(item))",
                "RecordRuntimeItemOwnerIfUnownedOrSameOwner(persistentId, ownerId);",
                "return true;",
                "if (HasAliasConflict(persistentId, item, out error))",
                "_runtimeItems.Add(item);",
                "RecordRuntimeItemOwner(persistentId, ownerId);",
                "AddLookupAlias(persistentId, item);",
                "AddHashLookupAlias(item);"));
            Assert.IsTrue(ContainsTokensInOrder(
                catalogUnregister,
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "for (int i = _runtimeItems.Count - 1; i >= 0; i--)",
                "string persistentId = NormalizeRuntimeItemPersistentId(item);",
                "_runtimeItemOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner)",
                "_runtimeItemOwnerByPersistentId.Remove(persistentId);",
                "_runtimeItems.RemoveAt(i);",
                "if (removed)",
                "RebuildLookup();"));
            Assert.IsTrue(ContainsTokensInOrder(
                ownerRecorder,
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "if (string.IsNullOrEmpty(ownerId))",
                "_runtimeItemOwnerByPersistentId?.Remove(persistentId);",
                "return;",
                "_runtimeItemOwnerByPersistentId[persistentId] = ownerId;"));
            Assert.IsTrue(ContainsTokensInOrder(
                catalogPromoteOwner,
                "string persistentId = NormalizeRuntimeItemPersistentId(item);",
                "if (string.IsNullOrEmpty(persistentId) || !ContainsRuntimeItem(item))",
                "return false;",
                "RecordRuntimeItemOwnerIfUnownedOrSameOwner(persistentId, ownerId);",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                ownerPromoter,
                "persistentId = NormalizeRuntimeItemPersistentId(persistentId);",
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "if (string.IsNullOrEmpty(persistentId) || string.IsNullOrEmpty(ownerId))",
                "return;",
                "_runtimeItemOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner)",
                "!string.Equals(registeredOwner, ownerId, StringComparison.Ordinal)",
                "return;",
                "RecordRuntimeItemOwner(persistentId, ownerId);"));
            Assert.IsTrue(ContainsTokensInOrder(
                itemCatalog,
                "private void RebuildLookup()",
                "_runtimeDescriptorLookup = new Dictionary<int, ItemRuntimeDescriptor>(hashLookupCapacity);",
                "ApplyRuntimeTemplateRegistrySnapshot();"));
            StringAssert.Contains("itemCatalog.TryGetRuntimeDescriptor(itemHashId, out runtimeDescriptor)", playerInventory);
            StringAssert.Contains("playerInventory.ItemCatalog.FindByHash(itemHashId)", pdaInventory);
        }

        [Test]
        public void RecyclingRegistry_RemovesDisabledOwnerYieldOverridesBeforeRecyclerSnapshot()
        {
            string recyclingRegistry = ReadProjectFile("Assets/_Project/Scripts/Economy/RecyclingRegistry.cs");
            string modRuntime = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string modRegistryEvents = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRegistryEvents.cs");
            string scrapManager = ReadProjectFile("Assets/_Project/Scripts/Economy/ScrapManager.cs");
            string registerById = ExtractMethodBody(recyclingRegistry, "public static bool TryRegister(string legacyItemId, IList<ResourceStack> yield, out string error)");
            string registerByHash = ExtractMethodBody(recyclingRegistry, "public static bool TryRegister(uint targetHashId, IList<ResourceStack> yield, out string error)");
            string resolveOwnedYield = ExtractMethodBody(recyclingRegistry, "internal static bool TryGetYield(uint targetHashId, out ResourceStack[] yield, out uint ownerHash)");
            string clearById = ExtractMethodBody(recyclingRegistry, "public static void Clear(string legacyItemId)");
            string clearByHash = ExtractMethodBody(recyclingRegistry, "public static void Clear(uint targetHashId)");
            string clearOwner = ExtractMethodBody(recyclingRegistry, "internal static bool ClearOwner(string ownerId)");
            string computeStableItemHash = ExtractMethodBody(recyclingRegistry, "internal static uint ComputeStableItemHash(string legacyItemId)");
            string resolveOwner = ExtractMethodBody(recyclingRegistry, "private static string ResolveActiveOwnerId()");
            string recordOwner = ExtractMethodBody(recyclingRegistry, "private static void RecordOwner<TKey>(Dictionary<TKey, string> ownerIndex, TKey key, string ownerId)");
            string resolveOwnerHash = ExtractMethodBody(recyclingRegistry, "private static bool TryResolveOwnerHash<TKey>(Dictionary<TKey, string> ownerIndex, TKey key, out uint ownerHash)");
            string notifyRecycleSource = ExtractMethodBody(recyclingRegistry, "private static void NotifyRecycleRegistryChanged()");
            string registerModRecycle = ExtractMethodBody(modRuntime, "internal static bool TryRegister(string itemId, IList<ResourceStack> yield, out string error)");
            string unregisterModRecycle = ExtractMethodBody(modRuntime, "internal static void UnregisterModRecycleYields(string modId)");
            string notifyRecycle = ExtractMethodBody(modRegistryEvents, "internal static void NotifyRecycleRegistryChanged()");
            string replayOverflowed = ExtractMethodBody(modRegistryEvents, "private static void ReplayOverflowedEvents()");
            string markOverflowed = ExtractMethodBody(modRegistryEvents, "private static void MarkOverflowedIfNotAlreadyQueued(ModRegistryEventType eventType)");
            string tryMarkQueued = ExtractMethodBody(modRegistryEvents, "private static bool TryMarkQueued(ModRegistryEventType eventType)");
            string clearQueued = ExtractMethodBody(modRegistryEvents, "private static void ClearQueuedFlag(ushort eventType)");
            string recycleSnapshot = ExtractMethodBody(scrapManager, "internal static bool TryBuildRecycleYieldSnapshot(ItemData sourceItem, ResourceStack[] destination, out int resolvedCount)");

            StringAssert.Contains("using Hecton8.Modding;", recyclingRegistry);
            StringAssert.Contains("private static readonly Dictionary<string, string> _customYieldOwnerById", recyclingRegistry);
            StringAssert.Contains("private static readonly Dictionary<uint, string> _customYieldOwnerByHash", recyclingRegistry);
            StringAssert.Contains("private static readonly List<string> _stableIdRemovalScratch", recyclingRegistry);
            StringAssert.Contains("private static readonly List<uint> _hashRemovalScratch", recyclingRegistry);
            StringAssert.Contains("internal static uint RegistryRevision => _registryRevision;", recyclingRegistry);
            StringAssert.Contains("_registryRevision = 0u;", recyclingRegistry);
            StringAssert.Contains("RecycleRegistryChanged = 5", modRegistryEvents);
            StringAssert.Contains("private const int PendingEventCapacity = 5;", modRegistryEvents);
            StringAssert.Contains("private static bool _recycleRegistryChangeQueued;", modRegistryEvents);
            StringAssert.Contains("private static bool _recycleRegistryChangeOverflowed;", modRegistryEvents);
            Assert.IsTrue(ContainsTokensInOrder(
                registerById,
                "string ownerId = ResolveActiveOwnerId();",
                "_customYields[stableId] = clonedStacks;",
                "RecordOwner(_customYieldOwnerById, stableId, ownerId);",
                "_customYieldsByHash[itemHash] = clonedStacks;",
                "RecordOwner(_customYieldOwnerByHash, itemHash, ownerId);",
                "NotifyRecycleRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                registerByHash,
                "_customYieldsByHash[targetHashId] = clonedStacks;",
                "RecordOwner(_customYieldOwnerByHash, targetHashId, ResolveActiveOwnerId());",
                "NotifyRecycleRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                resolveOwnedYield,
                "ownerHash = 0u;",
                "if (!TryGetYield(targetHashId, out yield))",
                "return false;",
                "TryResolveOwnerHash(_customYieldOwnerByHash, targetHashId, out ownerHash);",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                clearById,
                "bool removed = _customYields.Remove(stableId);",
                "if (_customYieldOwnerById.Remove(stableId))",
                "removed = true;",
                "if (_customYieldsByHash.Remove(itemHash))",
                "removed = true;",
                "if (_customYieldOwnerByHash.Remove(itemHash))",
                "removed = true;",
                "if (removed)",
                "NotifyRecycleRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                clearByHash,
                "bool removed = _customYieldsByHash.Remove(targetHashId);",
                "if (_customYieldOwnerByHash.Remove(targetHashId))",
                "removed = true;",
                "if (removed)",
                "NotifyRecycleRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                clearOwner,
                "_stableIdRemovalScratch.Clear();",
                "Dictionary<string, string>.Enumerator idEnumerator = _customYieldOwnerById.GetEnumerator();",
                "if (string.Equals(idEnumerator.Current.Value, ownerId, System.StringComparison.Ordinal))",
                "_stableIdRemovalScratch.Add(idEnumerator.Current.Key);",
                "_customYields.Remove(stableId);",
                "_customYieldOwnerById.Remove(stableId);",
                "_hashRemovalScratch.Clear();",
                "Dictionary<uint, string>.Enumerator hashEnumerator = _customYieldOwnerByHash.GetEnumerator();",
                "_hashRemovalScratch.Add(hashEnumerator.Current.Key);",
                "_customYieldsByHash.Remove(itemHash);",
                "_customYieldOwnerByHash.Remove(itemHash);",
                "if (removed)",
                "NotifyRecycleRegistryChanged();",
                "return removed;"));
            Assert.IsTrue(ContainsTokensInOrder(
                computeStableItemHash,
                "if (string.IsNullOrWhiteSpace(legacyItemId))",
                "return 0u;",
                "return unchecked((uint)LocHash.Compute(legacyItemId.Trim()));"));
            StringAssert.Contains("return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;", resolveOwner);
            Assert.IsTrue(ContainsTokensInOrder(
                recordOwner,
                "if (string.IsNullOrWhiteSpace(ownerId))",
                "return;",
                "ownerIndex[key] = ownerId;"));
            StringAssert.DoesNotContain("ownerIndex.Remove(key);", recordOwner);
            Assert.IsTrue(ContainsTokensInOrder(
                resolveOwnerHash,
                "ownerHash = 0u;",
                "!ownerIndex.TryGetValue(key, out string ownerId)",
                "string.IsNullOrWhiteSpace(ownerId)",
                "return false;",
                "ownerHash = ModCommandDispatcher.ComputeModHash(ownerId);",
                "return ownerHash != 0u;"));
            StringAssert.Contains("ModRegistryEvents.NotifyRecycleRegistryChanged();", notifyRecycleSource);
            Assert.IsTrue(ContainsTokensInOrder(
                notifyRecycleSource,
                "unchecked",
                "_registryRevision++;",
                "if (_registryRevision == 0u)",
                "_registryRevision = 1u;",
                "ModRegistryEvents.NotifyRecycleRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                registerModRecycle,
                "return RecyclingRegistry.TryRegister(itemId, yield, out error);"));
            Assert.IsTrue(ContainsTokensInOrder(
                unregisterModRecycle,
                "RecyclingRegistry.ClearOwner(modId);"));
            StringAssert.Contains("Enqueue(ModRegistryEventType.RecycleRegistryChanged, 0u, 0u, 0);", notifyRecycle);
            StringAssert.Contains("TryReplayOverflowedEvent(ModRegistryEventType.RecycleRegistryChanged, ref _recycleRegistryChangeOverflowed);", replayOverflowed);
            StringAssert.Contains("case ModRegistryEventType.RecycleRegistryChanged:", markOverflowed);
            StringAssert.Contains("_recycleRegistryChangeOverflowed = true;", markOverflowed);
            StringAssert.Contains("case ModRegistryEventType.RecycleRegistryChanged:", tryMarkQueued);
            StringAssert.Contains("_recycleRegistryChangeQueued = true;", tryMarkQueued);
            StringAssert.Contains("case ModRegistryEventType.RecycleRegistryChanged:", clearQueued);
            StringAssert.Contains("_recycleRegistryChangeQueued = false;", clearQueued);
            Assert.IsTrue(ContainsTokensInOrder(
                recycleSnapshot,
                "uint unusedOverlayOwnerHash;",
                "return TryBuildRecycleYieldSnapshot(sourceItem, destination, out resolvedCount, out unusedOverlayOwnerHash);"));
            Assert.IsTrue(ContainsTokensInOrder(
                scrapManager,
                "out uint overlayOwnerHash",
                "out bool usedRegisteredOverlay",
                "overlayOwnerHash = 0u;",
                "usedRegisteredOverlay = false;",
                "if (RecyclingRegistry.TryGetYield(",
                "out overlayOwnerHash",
                "usedRegisteredOverlay = true;",
                "return CopyYieldSnapshotNonAlloc(registeredYield, destination, out resolvedCount);",
                "RecipeData recipe;"));
        }

        [Test]
        public void ResourceRecyclerModule_ListensToModRegistryAndHandlesEventOverflow()
        {
            string resourceRecycler = ReadProjectFile("Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs");
            string recyclerEnable = ExtractMethodBody(resourceRecycler, "private void OnEnable()");
            string recyclerDisable = ExtractMethodBody(resourceRecycler, "private void OnDisable()");
            string recyclerHandleEvent = ExtractMethodBody(resourceRecycler, "private static void HandleModRegistryEvent(in ModRegistryEventPayload payload)");
            string recyclerRegisterModule = ExtractMethodBody(resourceRecycler, "private void RegisterModuleInstance()");
            string recyclerStart = ExtractMethodBody(resourceRecycler, "private bool TryStartBufferedRecycle()");
            string recyclerDeliver = ExtractMethodBody(resourceRecycler, "private bool TryDeliverPendingYield(PlayerInventory inventory)");
            string recyclerRegisterListener = ExtractMethodBody(resourceRecycler, "private static void TryRegisterModRegistryListener()");
            string recyclerUnregisterListener = ExtractMethodBody(resourceRecycler, "private static void TryUnregisterModRegistryListenerIfNoActiveModules()");
            string recyclerMarkDirty = ExtractMethodBody(resourceRecycler, "private void MarkPendingRecycleSnapshotDirtyIfAffected(uint modHash, uint sourceItemHash)");
            string recyclerRefresh = ExtractMethodBody(resourceRecycler, "private bool TryRefreshInvalidatedPendingYield()");
            string recyclerReportOverflow = ExtractMethodBody(resourceRecycler, "private static void ReportActiveModuleRegistrationOverflow()");
            string recyclerTelemetryBestEffort = ExtractMethodBody(resourceRecycler, "private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)");
            string recyclerClearPending = ExtractMethodBody(resourceRecycler, "private void ClearPendingOutput()");

            StringAssert.Contains("ActiveModuleRegistrationOverflowWarningHash = 0x5252434Fu", resourceRecycler);
            StringAssert.Contains("ActiveModuleRegistrationOverflowContextHash = 0x52524D4Fu", resourceRecycler);
            StringAssert.Contains("private static int s_DroppedActiveModuleRegistrationCount;", resourceRecycler);
            StringAssert.Contains("internal static int DroppedActiveModuleRegistrationCount => s_DroppedActiveModuleRegistrationCount;", resourceRecycler);
            StringAssert.Contains("s_DroppedActiveModuleRegistrationCount = 0;", resourceRecycler);
            StringAssert.DoesNotContain("BaseLogisticsNetwork.RegisterRecycler", resourceRecycler);
            StringAssert.DoesNotContain("BaseLogisticsNetwork.UnregisterRecycler", resourceRecycler);
            StringAssert.DoesNotContain("using Hecton8.Construction;", resourceRecycler);
            StringAssert.Contains("private static bool s_ModRegistryEventRegistered;", resourceRecycler);
            StringAssert.Contains("private static ModRegistryEventAdapter s_ModRegistryEventAdapter;", resourceRecycler);
            StringAssert.Contains("s_ModRegistryEventRegistered = false;", resourceRecycler);
            StringAssert.Contains("s_ModRegistryEventAdapter = null;", resourceRecycler);
            StringAssert.Contains("private uint _pendingRecycleRegistryRevision;", resourceRecycler);
            StringAssert.Contains("private bool _pendingRecycleUsesOverlay;", resourceRecycler);
            StringAssert.Contains("private sealed class ModRegistryEventAdapter : IModRegistryEventListener", resourceRecycler);
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerEnable,
                "CacheRuntimeServicesCold();",
                "GlobalRegistry.TryRegisterHotSwapListener(this);",
                "RegisterModuleInstance();",
                "TryRegisterModRegistryListener();",
                "TryRegister();"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerDisable,
                "UnregisterModuleInstance();",
                "TryUnregisterModRegistryListenerIfNoActiveModules();"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerRegisterListener,
                "if (s_ModRegistryEventRegistered || !Application.isPlaying || s_ActiveModuleCount <= 0)",
                "return;",
                "s_ModRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerUnregisterListener,
                "if (!s_ModRegistryEventRegistered || s_ActiveModuleCount > 0)",
                "return;",
                "if (s_ModRegistryEventAdapter != null)",
                "ModRegistryEvents.Unregister(s_ModRegistryEventAdapter);",
                "s_ModRegistryEventRegistered = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerHandleEvent,
                "if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RecycleRegistryChanged)",
                "return;",
                "for (int i = s_ActiveModuleCount - 1; i >= 0; i--)",
                "ResourceRecyclerModule module = s_ActiveModules[i];",
                "if (module == null || !module.isActiveAndEnabled)",
                "continue;",
                "module.MarkPendingRecycleSnapshotDirtyIfAffected(payload.ModHash, payload.SubjectHash);"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerRegisterModule,
                "for (int i = 0; i < s_ActiveModuleCount; i++)",
                "if (ReferenceEquals(s_ActiveModules[i], this))",
                "return;",
                "if (s_ActiveModuleCount >= s_ActiveModules.Length)",
                "ReportActiveModuleRegistrationOverflow();",
                "return;",
                "s_ActiveModules[s_ActiveModuleCount] = this;",
                "s_ActiveModuleCount++;"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerReportOverflow,
                "s_DroppedActiveModuleRegistrationCount++;",
                "PublishPerformanceWarningBestEffort(",
                "ActiveModuleRegistrationOverflowWarningHash,",
                "ActiveModuleRegistrationOverflowContextHash,",
                "s_DroppedActiveModuleRegistrationCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerTelemetryBestEffort,
                "try",
                "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);",
                "catch (System.Exception)"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerStart,
                "ScrapManager.TryBuildRecycleYieldSnapshot(",
                "out uint overlayOwnerHash",
                "out bool usedRegisteredOverlay",
                "_pendingRecycleOverlayOwnerHash = overlayOwnerHash;",
                "_pendingRecycleSubjectHash = unchecked((uint)sourceItem.PersistentHashId);",
                "_pendingRecycleRegistryRevision = RecyclingRegistry.RegistryRevision;",
                "_pendingRecycleUsesOverlay = usedRegisteredOverlay;",
                "_pendingRecycleSnapshotInvalidated = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerMarkDirty,
                "if (!_isProcessing &&",
                "!_hasPendingOutput)",
                "return;",
                "if (!_pendingRecycleUsesOverlay || _activeSourceItem == null)",
                "return;",
                "if (modHash != 0u && modHash != _pendingRecycleOverlayOwnerHash)",
                "return;",
                "if (sourceItemHash != 0u && sourceItemHash != _pendingRecycleSubjectHash)",
                "return;",
                "_pendingRecycleSnapshotInvalidated = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerDeliver,
                "if (_pendingRecycleUsesOverlay &&",
                "_pendingRecycleRegistryRevision != RecyclingRegistry.RegistryRevision)",
                "_pendingRecycleSnapshotInvalidated = true;",
                "if (_pendingRecycleSnapshotInvalidated && !TryRefreshInvalidatedPendingYield())",
                "return false;",
                "if (_pendingYield == null || _pendingYieldCount <= 0)",
                "return false;",
                "ScrapManager.GrantYield(inventory, _pendingYield, _pendingYieldCount, ref grantedStackCount)"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerRefresh,
                "ScrapManager.ClearYieldScratch(_pendingYieldScratch, _pendingYieldCount);",
                "_pendingYield = null;",
                "_pendingYieldCount = 0;",
                "if (ScrapManager.TryBuildRecycleYieldSnapshot(",
                "out uint overlayOwnerHash",
                "out bool usedRegisteredOverlay",
                "_pendingYield = _pendingYieldScratch;",
                "_pendingRecycleRegistryRevision = RecyclingRegistry.RegistryRevision;",
                "_pendingRecycleUsesOverlay = usedRegisteredOverlay;",
                "_pendingRecycleSnapshotInvalidated = false;",
                "return true;",
                "if (TryBufferItem(sourceItem))",
                "ClearPendingOutput();",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                recyclerClearPending,
                "_pendingRecycleOverlayOwnerHash = 0u;",
                "_pendingRecycleSubjectHash = 0u;",
                "_pendingRecycleRegistryRevision = 0u;",
                "_pendingRecycleUsesOverlay = false;",
                "_pendingRecycleSnapshotInvalidated = false;"));
        }

        [Test]
        public void BaseLogisticsNetwork_DropsEndpointRegistrationOverflowsAndValidatesReservations()
        {
            string baseLogisticsNetwork = ReadProjectFile("Assets/_Project/Scripts/Construction/BaseLogisticsNetwork.cs");
            string fabricatorRuntime = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string maintenanceStationModule = ReadProjectFile("Assets/_Project/Scripts/Construction/MaintenanceStationModule.cs");
            string registerStorageEndpoint = ExtractMethodBody(baseLogisticsNetwork, "public static void RegisterStorage(StorageCrate crate, PowerNode node)");
            string registerFabricatorEndpoint = ExtractMethodBody(baseLogisticsNetwork, "public static void RegisterFabricator(Fabricator fabricator, PowerNode node)");
            string storageEndpointReportOverflow = ExtractMethodBody(baseLogisticsNetwork, "private static void ReportStorageEndpointRegistrationOverflow()");
            string fabricatorEndpointReportOverflow = ExtractMethodBody(baseLogisticsNetwork, "private static void ReportFabricatorEndpointRegistrationOverflow()");
            string reservationPoolExhausted = ExtractMethodBody(baseLogisticsNetwork, "private static void ReportReservationPoolExhausted()");
            string reservationPoolInvalidSlot = ExtractMethodBody(baseLogisticsNetwork, "private static void ReportReservationPoolInvalidSlot()");
            string reservationPoolReturnOverflow = ExtractMethodBody(baseLogisticsNetwork, "private static void ReportReservationPoolReturnOverflow()");
            string logisticsTelemetryBestEffort = ExtractMethodBody(baseLogisticsNetwork, "private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)");
            string tryRentReservation = ExtractMethodBody(baseLogisticsNetwork, "private static bool TryRentReservation(PowerGrid grid, out LogisticsReservation reservation)");
            string returnReservation = ExtractMethodBody(baseLogisticsNetwork, "private static void ReturnReservation(LogisticsReservation reservation)");
            string maintenancePrepareRepairReservation = ExtractMethodBody(maintenanceStationModule, "private bool TryPrepareRepairReservation(");

            StringAssert.Contains("StorageEndpointRegistrationOverflowWarningHash = 0x424C534Fu", baseLogisticsNetwork);
            StringAssert.Contains("StorageEndpointRegistrationOverflowContextHash = 0x424C5343u", baseLogisticsNetwork);
            StringAssert.Contains("FabricatorEndpointRegistrationOverflowWarningHash = 0x424C464Fu", baseLogisticsNetwork);
            StringAssert.Contains("FabricatorEndpointRegistrationOverflowContextHash = 0x424C4643u", baseLogisticsNetwork);
            StringAssert.Contains("ReservationPoolExhaustedWarningHash = 0x424C5258u", baseLogisticsNetwork);
            StringAssert.Contains("ReservationPoolInvalidSlotWarningHash = 0x424C524Eu", baseLogisticsNetwork);
            StringAssert.Contains("ReservationPoolReturnOverflowWarningHash = 0x424C5252u", baseLogisticsNetwork);
            StringAssert.Contains("ReservationPoolContextHash = 0x424C5250u", baseLogisticsNetwork);
            StringAssert.Contains("private static int s_DroppedStorageEndpointRegistrationCount;", baseLogisticsNetwork);
            StringAssert.Contains("private static int s_DroppedFabricatorEndpointRegistrationCount;", baseLogisticsNetwork);
            StringAssert.Contains("private static int s_ReservationPoolExhaustionCount;", baseLogisticsNetwork);
            StringAssert.Contains("private static int s_ReservationPoolInvalidSlotCount;", baseLogisticsNetwork);
            StringAssert.Contains("private static int s_ReservationPoolReturnOverflowCount;", baseLogisticsNetwork);
            StringAssert.Contains("internal static int DroppedStorageEndpointRegistrationCount => s_DroppedStorageEndpointRegistrationCount;", baseLogisticsNetwork);
            StringAssert.Contains("internal static int DroppedFabricatorEndpointRegistrationCount => s_DroppedFabricatorEndpointRegistrationCount;", baseLogisticsNetwork);
            StringAssert.Contains("internal static int ReservationPoolExhaustionCount => s_ReservationPoolExhaustionCount;", baseLogisticsNetwork);
            StringAssert.Contains("internal static int ReservationPoolInvalidSlotCount => s_ReservationPoolInvalidSlotCount;", baseLogisticsNetwork);
            StringAssert.Contains("internal static int ReservationPoolReturnOverflowCount => s_ReservationPoolReturnOverflowCount;", baseLogisticsNetwork);
            StringAssert.Contains("s_DroppedStorageEndpointRegistrationCount = 0;", baseLogisticsNetwork);
            StringAssert.Contains("s_DroppedFabricatorEndpointRegistrationCount = 0;", baseLogisticsNetwork);
            StringAssert.Contains("s_ReservationPoolExhaustionCount = 0;", baseLogisticsNetwork);
            StringAssert.Contains("s_ReservationPoolInvalidSlotCount = 0;", baseLogisticsNetwork);
            StringAssert.Contains("s_ReservationPoolReturnOverflowCount = 0;", baseLogisticsNetwork);
            StringAssert.DoesNotContain("RecyclerEndpoint", baseLogisticsNetwork);
            StringAssert.Contains("BaseLogisticsNetwork.TryReserveResources(", fabricatorRuntime);
            StringAssert.Contains("out _networkReservation", fabricatorRuntime);
            StringAssert.Contains("BaseLogisticsNetwork.RollbackReserved(_networkReservation);", fabricatorRuntime);
            StringAssert.Contains("BaseLogisticsNetwork.CommitReserved(_networkReservation);", fabricatorRuntime);
            StringAssert.Contains("BaseLogisticsNetwork.TryReserveResources(", maintenanceStationModule);
            StringAssert.Contains("out _activeReservation", maintenanceStationModule);
            StringAssert.Contains("BaseLogisticsNetwork.CommitReserved(_activeReservation);", maintenanceStationModule);
            StringAssert.Contains("BaseLogisticsNetwork.RollbackReserved(_activeReservation);", maintenanceStationModule);
            Assert.IsTrue(ContainsTokensInOrder(
                maintenancePrepareRepairReservation,
                "out _activeReservation",
                "BaseLogisticsNetwork.CommitReserved(_activeReservation);",
                "_activeReservation = null;",
                "_repairTargetDurability = maxDurability;"));
            Assert.IsTrue(ContainsTokensInOrder(
                registerStorageEndpoint,
                "if (crate == null || node == null)",
                "return;",
                "for (int i = 0; i < s_StorageEndpointCount; i++)",
                "if (ReferenceEquals(s_StorageEndpoints[i].Crate, crate))",
                "return;",
                "if (s_StorageEndpointCount >= StorageEndpointCapacity)",
                "ReportStorageEndpointRegistrationOverflow();",
                "return;",
                "s_StorageEndpoints[s_StorageEndpointCount++] = new StorageEndpoint",
                "Crate = crate,",
                "Node = node"));
            Assert.IsTrue(ContainsTokensInOrder(
                registerFabricatorEndpoint,
                "if (fabricator == null || node == null)",
                "return;",
                "for (int i = 0; i < s_FabricatorEndpointCount; i++)",
                "if (ReferenceEquals(s_FabricatorEndpoints[i].Fabricator, fabricator))",
                "return;",
                "if (s_FabricatorEndpointCount >= FabricatorEndpointCapacity)",
                "ReportFabricatorEndpointRegistrationOverflow();",
                "return;",
                "s_FabricatorEndpoints[s_FabricatorEndpointCount++] = new FabricatorEndpoint",
                "Fabricator = fabricator,",
                "Node = node"));
            Assert.IsTrue(ContainsTokensInOrder(
                storageEndpointReportOverflow,
                "s_DroppedStorageEndpointRegistrationCount++;",
                "PublishPerformanceWarningBestEffort(",
                "StorageEndpointRegistrationOverflowWarningHash,",
                "StorageEndpointRegistrationOverflowContextHash,",
                "s_DroppedStorageEndpointRegistrationCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorEndpointReportOverflow,
                "s_DroppedFabricatorEndpointRegistrationCount++;",
                "PublishPerformanceWarningBestEffort(",
                "FabricatorEndpointRegistrationOverflowWarningHash,",
                "FabricatorEndpointRegistrationOverflowContextHash,",
                "s_DroppedFabricatorEndpointRegistrationCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                tryRentReservation,
                "reservation = null;",
                "if (grid == null)",
                "return false;",
                "if (s_ReservationPoolCount <= 0)",
                "ReportReservationPoolExhausted();",
                "return false;",
                "int poolIndex = --s_ReservationPoolCount;",
                "reservation = s_ReservationPool[poolIndex];",
                "if (reservation == null)",
                "s_ReservationPoolCount++;",
                "ReportReservationPoolInvalidSlot();",
                "return false;",
                "reservation.Initialize(GetNextReservationId(), grid);",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                returnReservation,
                "if (reservation == null)",
                "return;",
                "reservation.Release();",
                "if (s_ReservationPoolCount >= ReservationPoolCapacity)",
                "ReportReservationPoolReturnOverflow();",
                "return;",
                "s_ReservationPool[s_ReservationPoolCount++] = reservation;"));
            Assert.IsTrue(ContainsTokensInOrder(
                reservationPoolExhausted,
                "s_ReservationPoolExhaustionCount++;",
                "PublishPerformanceWarningBestEffort(",
                "ReservationPoolExhaustedWarningHash,",
                "ReservationPoolContextHash,",
                "s_ReservationPoolExhaustionCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                reservationPoolInvalidSlot,
                "s_ReservationPoolInvalidSlotCount++;",
                "PublishPerformanceWarningBestEffort(",
                "ReservationPoolInvalidSlotWarningHash,",
                "ReservationPoolContextHash,",
                "s_ReservationPoolInvalidSlotCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                reservationPoolReturnOverflow,
                "s_ReservationPoolReturnOverflowCount++;",
                "PublishPerformanceWarningBestEffort(",
                "ReservationPoolReturnOverflowWarningHash,",
                "ReservationPoolContextHash,",
                "s_ReservationPoolReturnOverflowCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                logisticsTelemetryBestEffort,
                "try",
                "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);",
                "catch (System.Exception exception) when (!(exception is FatalArchitectureException))",
                "LogPerformanceWarningTelemetryException(exception);"));
        }

        [Test]
        public void DroneFleetManager_HandlesReservationCommitsAndAppliesLiveState()
        {
            string repairDroneHub = ReadProjectFile("Assets/_Project/Scripts/Construction/RepairDroneHub.cs");
            string droneFleetManager = ReadProjectFile("Assets/_Project/Scripts/Construction/DroneFleetManager.cs");
            string threadSafeCommandQueue = ReadProjectFile("Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs");
            string droneStorageAck = ExtractMethodBody(droneFleetManager, "private static void HandleStorageReservationCommitResolved(int requesterId, int reservationId, bool committed)");
            string droneAckLiveApply = ExtractMethodBody(droneFleetManager, "private static bool TryApplyResolvedResupplyCommitToLiveSlot(int slot, bool committed)");
            string droneAckConsume = ExtractMethodBody(droneFleetManager, "private static bool TryConsumeResolvedResupplyCommitAck(");
            string droneAckClear = ExtractMethodBody(droneFleetManager, "private static void ClearPendingResupplyCommitAck(");
            string droneApplyPendingControls = ExtractMethodBody(droneFleetManager, "private static void ApplyPendingControls(");
            string droneRefreshFleetStatus = ExtractMethodBody(droneFleetManager, "private static void RefreshFleetStatusSnapshotFromDroneStates(");
            string droneReportStaleAck = ExtractMethodBody(droneFleetManager, "private static void ReportStorageReservationStaleAck(int requesterId)");
            string droneReportMismatchAck = ExtractMethodBody(droneFleetManager, "private static void ReportStorageReservationMismatchAck(int reservationId)");
            string dronePublishAckWarning = ExtractMethodBody(droneFleetManager, "private static void PublishStorageReservationAckWarningBestEffort(uint warningHash, float value)");
            string queueDroneResupply = ExtractMethodBody(repairDroneHub, "internal bool TryQueueDroneResupplyCommit(int requestedUnits, int droneId, out bool committedImmediately, out int queuedReservationId)");
            string applyHeadlessResupply = ExtractMethodBody(droneFleetManager, "private static void ApplyHeadlessResupply(int slot, ref HeadlessDroneState drone)");

            StringAssert.Contains("private static int s_StorageReservationStaleAckCount;", droneFleetManager);
            StringAssert.Contains("private static int s_StorageReservationMismatchAckCount;", droneFleetManager);
            StringAssert.Contains("s_StorageReservationStaleAckWarningHash", droneFleetManager);
            StringAssert.Contains("s_StorageReservationMismatchAckWarningHash", droneFleetManager);
            StringAssert.Contains("s_StorageReservationAckContextHash", droneFleetManager);
            StringAssert.Contains("internal static int StorageReservationStaleAckCount =>", droneFleetManager);
            StringAssert.Contains("internal static int StorageReservationMismatchAckCount =>", droneFleetManager);
            StringAssert.Contains("s_StorageReservationStaleAckCount = 0;", droneFleetManager);
            StringAssert.Contains("s_StorageReservationMismatchAckCount = 0;", droneFleetManager);
            StringAssert.Contains("ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);", droneFleetManager);
            StringAssert.Contains("EnsureStorageReservationCommitResolvedBridge();", droneFleetManager);
            StringAssert.Contains("s_StorageReservationCommitResolvedListenerGeneration = -1;", droneFleetManager);
            StringAssert.Contains("ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration", droneFleetManager);
            StringAssert.Contains("ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge)", droneFleetManager);
            StringAssert.Contains("BaseLogisticsNetwork.TryReserveResources(grid, _repairSupplyHashIds, _repairSupplyAmounts, 1, out BaseLogisticsNetwork.LogisticsReservation reservation)", repairDroneHub);
            StringAssert.Contains("queuedReservationId = reservation.ReservationId;", repairDroneHub);
            StringAssert.Contains("BaseLogisticsNetwork.TryCommitReservedViaCommandQueue(reservation, requesterId, out committedImmediately)", repairDroneHub);
            StringAssert.Contains("BaseLogisticsNetwork.CommitReserved(reservation);", repairDroneHub);
            StringAssert.Contains("hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId)", droneFleetManager);
            Assert.IsTrue(ContainsTokensInOrder(
                droneStorageAck,
                "int slot = ResolveHeadlessSlot(requesterId);",
                "slot >= s_PendingResupplyReservationIdsBySlot.Length)",
                "ReportStorageReservationStaleAck(requesterId);",
                "return;",
                "int expectedReservationId = s_PendingResupplyReservationIdsBySlot[slot];",
                "if (expectedReservationId <= 0)",
                "ReportStorageReservationStaleAck(requesterId);",
                "return;",
                "if (reservationId != expectedReservationId)",
                "ReportStorageReservationMismatchAck(reservationId);",
                "return;",
                "bool commitSucceeded = committed && reservationId > 0;",
                "if (TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded))",
                "return;",
                "if (commitSucceeded)",
                "s_PendingResupplyGrantBySlot[slot] = true;",
                "s_PendingResupplyFailureBySlot[slot] = false;",
                "else"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneAckLiveApply,
                "TryAcquireDroneCoreMirrorMutationViews(",
                "TryConsumeResolvedResupplyCommitAck(slot, committed, ref drone, out bool droneChanged)",
                "droneStates[slot] = drone;",
                "MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);",
                "RefreshHeadlessCounters(droneStates);",
                "RefreshFleetStatusSnapshotFromDroneStates(droneStates);",
                "UpdateDrawBounds();",
                "ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);",
                "PublishSnapshot();",
                "return consumedAck;"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneRefreshFleetStatus,
                "int activeCount = 0;",
                "int solderReserve = 0;",
                "solderReserve += math.max(0, drone.SolderUnits);",
                "hostileCount++;",
                "s_LastFleetStatusSnapshot = new FleetStatusSnapshot("));
            Assert.IsTrue(ContainsTokensInOrder(
                droneAckConsume,
                "drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending",
                "ClearPendingResupplyCommitAck(slot);",
                "return true;",
                "if (committed)",
                "GrantDroneResupply(ref drone, 1);",
                "else",
                "ReturnDroneToHub(ref drone);",
                "ClearPendingResupplyCommitAck(slot);",
                "droneChanged = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneAckClear,
                "s_PendingResupplyGrantBySlot[slot] = false;",
                "s_PendingResupplyFailureBySlot[slot] = false;",
                "s_PendingResupplyReservationIdsBySlot[slot] = 0;"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneApplyPendingControls,
                "TryConsumeResolvedResupplyCommitAck(slot, true, ref drone, out bool resupplyDroneChanged)",
                "TryConsumeResolvedResupplyCommitAck(slot, false, ref drone, out bool resupplyDroneChanged)",
                "ClearPendingResupplyCommitAck(slot);",
                "MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneReportStaleAck,
                "System.Threading.Interlocked.Increment(ref s_StorageReservationStaleAckCount);",
                "PublishStorageReservationAckWarningBestEffort(",
                "s_StorageReservationStaleAckWarningHash,",
                "math.max(0, requesterId));"));
            Assert.IsTrue(ContainsTokensInOrder(
                droneReportMismatchAck,
                "System.Threading.Interlocked.Increment(ref s_StorageReservationMismatchAckCount);",
                "PublishStorageReservationAckWarningBestEffort(",
                "s_StorageReservationMismatchAckWarningHash,",
                "math.max(0, reservationId));"));
            Assert.IsTrue(ContainsTokensInOrder(
                dronePublishAckWarning,
                "try",
                "GlobalTelemetryBus.PublishPerformanceWarning(warningHash, s_StorageReservationAckContextHash, value);",
                "catch (System.Exception exception) when (!(exception is FatalArchitectureException))",
                "LogStorageReservationAckTelemetryException(exception);"));
            Assert.IsTrue(ContainsTokensInOrder(
                queueDroneResupply,
                "if (droneId <= 0)",
                "return false;",
                "int safeRequestedUnits = 1;",
                "return TryConsumeRepairSupplyInternal(",
                "safeRequestedUnits,",
                "commitViaCommandQueue: true,",
                "requesterId: droneId,",
                "out committedImmediately,",
                "out queuedReservationId);"));
            Assert.IsTrue(ContainsTokensInOrder(
                applyHeadlessResupply,
                "hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId)",
                "if (committedImmediately)",
                "GrantDroneResupply(ref drone, 1);",
                "s_PendingResupplyReservationIdsBySlot[slot] = queuedReservationId;",
                "drone.State = (byte)HeadlessDroneRuntimeState.ResupplyCommitPending;"));
        }

        [Test]
        public void ThreadSafeCommandQueue_DrainsAbandonedStorageReservationsOnPersistence()
        {
            string threadSafeCommandQueue = ReadProjectFile("Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs");
            string saveManager = ReadProjectFile("Assets/_Project/Scripts/SaveManager.cs");
            string commandQueueRegisterStorageAckListener = ExtractMethodBody(threadSafeCommandQueue, "public static bool Register(IStorageReservationCommitResolvedListener listener)");
            string commandQueueReportStorageAckListenerCapacity = ExtractMethodBody(threadSafeCommandQueue, "private static void ReportStorageReservationCommitListenerCapacityExceeded()");
            string commandQueueIncrementStorageAckListenerCapacity = ExtractMethodBody(threadSafeCommandQueue, "private static void IncrementStorageReservationCommitListenerCapacityExceededCount()");

            StringAssert.Contains("StorageReservationCommitResolvedPayload", threadSafeCommandQueue);
            StringAssert.Contains("IStorageReservationCommitResolvedListener", threadSafeCommandQueue);
            StringAssert.Contains("void ReleaseReservation(int reservationId);", threadSafeCommandQueue);
            StringAssert.Contains("PrepareStorageReservationCommitBridgeForPersistenceSnapshot();", threadSafeCommandQueue);
            StringAssert.Contains("_persistenceSnapshotCommandBuffer", threadSafeCommandQueue);
            StringAssert.Contains("DrainPendingStorageReservationCommitsForPersistenceSnapshot();", threadSafeCommandQueue);
            StringAssert.Contains("DrainAbandonedPendingCommands(dispatchStorageReservationFailures: false);", threadSafeCommandQueue);
            StringAssert.Contains("DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);", threadSafeCommandQueue);
            StringAssert.Contains("DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: false);", threadSafeCommandQueue);
            StringAssert.Contains("DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);", threadSafeCommandQueue);
            StringAssert.Contains("target.ReleaseReservation(command.IntValue);", threadSafeCommandQueue);
            StringAssert.Contains("DispatchStorageReservationCommitResolvedFailure(command.SecondaryToken, command.IntValue);", threadSafeCommandQueue);
            StringAssert.Contains("StorageReservationCommitListenerGeneration =>", threadSafeCommandQueue);
            StringAssert.Contains("AdvanceStorageReservationCommitListenerGeneration();", threadSafeCommandQueue);
            StringAssert.Contains("RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, committed);", threadSafeCommandQueue);
            StringAssert.Contains("_storageCommitListenerCapacityWarningHash", threadSafeCommandQueue);
            StringAssert.Contains("private static int _storageReservationCommitListenerCapacityExceededCount;", threadSafeCommandQueue);
            StringAssert.Contains("public static int StorageReservationCommitListenerCapacityExceededCount =>", threadSafeCommandQueue);
            StringAssert.Contains("_storageReservationCommitListenerCapacityExceededCount = 0;", threadSafeCommandQueue);
            StringAssert.Contains("ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();", saveManager);
            Assert.IsTrue(ContainsTokensInOrder(
                commandQueueRegisterStorageAckListener,
                "if (listener == null)",
                "return false;",
                "bool capacityExceeded = false;",
                "bool registered = false;",
                "if (IndexOfStorageReservationCommitListener(listener) >= 0)",
                "registered = true;",
                "if (_storageReservationCommitListenerCount >= StorageReservationCommitListenerCapacity)",
                "capacityExceeded = true;",
                "else",
                "_storageReservationCommitListeners[_storageReservationCommitListenerCount] = listener;",
                "_storageReservationCommitListenerCount++;",
                "AdvanceStorageReservationCommitListenerGeneration();",
                "registered = true;",
                "if (capacityExceeded)",
                "ReportStorageReservationCommitListenerCapacityExceeded();",
                "return registered;"));
            Assert.IsTrue(ContainsTokensInOrder(
                commandQueueReportStorageAckListenerCapacity,
                "IncrementStorageReservationCommitListenerCapacityExceededCount();",
                "PublishQueuePerformanceWarningBestEffort(",
                "_storageCommitListenerCapacityWarningHash,",
                "_storageCommitQueueHash,",
                "StorageReservationCommitListenerCapacity);",
                "LogStorageReservationCommitListenerCapacityExceeded();"));
            Assert.IsTrue(ContainsTokensInOrder(
                commandQueueIncrementStorageAckListenerCapacity,
                "currentCount = Volatile.Read(ref _storageReservationCommitListenerCapacityExceededCount);",
                "if (currentCount >= int.MaxValue)",
                "return;",
                "Interlocked.CompareExchange(",
                "ref _storageReservationCommitListenerCapacityExceededCount,",
                "currentCount + 1,",
                "currentCount) != currentCount);"));
        }

        [Test]
        public void PersistentDroppedItemRegistry_PreflightsHarvestableYieldBeforeOutcropBreak()
        {
            string contracts = ReadProjectFile("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");
            string persistentWorldRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string harvestableOutcrop = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HarvestableOutcrop.cs");
            string breakBody = ExtractMethodBody(harvestableOutcrop, "private void Break(Vector3 hitPoint, Vector3 hitNormal, float toolPower)");
            string canDispatchYield = ExtractMethodBody(harvestableOutcrop, "private bool CanDispatchYield(");
            string dispatchYield = ExtractMethodBody(harvestableOutcrop, "private void DispatchYield(");

            StringAssert.Contains("bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition);", contracts);
            StringAssert.Contains("bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition);", contracts);
            StringAssert.Contains("bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)", persistentWorldRegistry);
            StringAssert.Contains("bool IPersistentDroppedItemRegistry.CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition)", persistentWorldRegistry);
            Assert.IsTrue(ContainsTokensInOrder(
                breakBody,
                "if (!CanDispatchYield(toolPower, hitPoint))",
                "_currentHealth = math.max(_currentHealth, MinimumToolPower);",
                "return;",
                "_isBroken = true;",
                "DispatchYield(toolPower, hitPoint);",
                "QueueComponentDisable();"));
            Assert.IsTrue(ContainsTokensInOrder(
                canDispatchYield,
                "if (!TryResolveYield(toolPower, out ItemData item, out int quantity))",
                "return true;",
                "int itemHashId = ItemData.ResolvePersistentHashId(item);",
                "playerInventory.CanAcceptItemQuantity(itemHashId, quantity)",
                "return true;",
                "registry.CanRegisterDroppedItem(item, quantity, dropPoint)",
                "return true;",
                "ReportYieldDeliveryBlocked(itemHashId, quantity);",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                dispatchYield,
                "if (!TryResolveYield(toolPower, out ItemData item, out int quantity))",
                "return;",
                "PlayerInventory.ScavengeAttemptResult result = playerInventory.ScavengeAttempt(itemHashId, quantity, inventoryTransform);",
                "if (result.IsSuccess)",
                "return;",
                "rejectedQuantity = result.RejectedQuantity;",
                "registry.TryRegisterDroppedItem(item, rejectedQuantity, dropPoint);"));
            StringAssert.Contains("private static void ReportYieldDeliveryBlocked(int itemHashId, int quantity)", harvestableOutcrop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", harvestableOutcrop);
        }

        [Test]
        public void ResourceRecyclerModule_PersistsAndEjectsBufferedAndPendingOutput()
        {
            string saveData = ReadProjectFile("Assets/_Project/Scripts/SaveData.cs");
            string saveCodec = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string saveMigration = ReadProjectFile("Assets/_Project/Scripts/SaveDataMigration.cs");
            string constructionManager = ReadProjectFile("Assets/_Project/Scripts/ConstructionManager.cs");
            string baseModule = ReadProjectFile("Assets/_Project/Scripts/BaseModule.cs");
            string persistentWorldRegistry = ReadProjectFile("Assets/_Project/Scripts/World/PersistentWorldRegistry.cs");
            string deepDrill = ReadProjectFile("Assets/_Project/Scripts/Construction/DeepDrillModule.cs");
            string resourceRecycler = ReadProjectFile("Assets/_Project/Scripts/Economy/ResourceRecyclerModule.cs");
            string fabricator = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string cultivationManager = ReadProjectFile("Assets/_Project/Scripts/Construction/CultivationManager.cs");
            string playerInventory = ReadProjectFile("Assets/_Project/Scripts/PlayerInventory.cs");
            string populate = ExtractMethodBody(resourceRecycler, "internal void PopulateSaveData(ref ModuleDTO dto)");
            string restore = ExtractMethodBody(resourceRecycler, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)");
            string deepDrillRestore = ExtractMethodBody(deepDrill, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)");
            string canEject = ExtractMethodBody(resourceRecycler, "internal bool CanEjectBufferedContents(BaseModule owner, PlayerInventory inventory, IObjectPoolService pool, Vector3 dropPosition)");
            string eject = ExtractMethodBody(resourceRecycler, "internal bool EjectBufferedContents(BaseModule owner, PlayerInventory inventory, IObjectPoolService pool, ref Vector3 dropPosition)");
            string countPersistentDropCandidate = ExtractMethodBody(resourceRecycler, "private static int CountPersistentWorldDropCandidate(");
            string populateFabricator = ExtractMethodBody(fabricator, "internal void PopulateSaveData(ref ModuleDTO dto)");
            string restoreFabricator = ExtractMethodBody(fabricator, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)");
            string populateCultivation = ExtractMethodBody(cultivationManager, "public void PopulateSaveData(ref ModuleDTO moduleDto, ItemCatalog itemCatalog)");
            string restoreCultivation = ExtractMethodBody(cultivationManager, "public void RestoreFromSaveData(ModuleDTO moduleDto, ItemCatalog itemCatalog)");
            string canEjectFabricator = ExtractMethodBody(fabricator, "internal bool CanEjectPendingCraftOutput(");
            string ejectFabricator = ExtractMethodBody(fabricator, "internal bool EjectPendingCraftOutput(");
            string canEjectCultivation = ExtractMethodBody(cultivationManager, "internal bool CanEjectCultivationContents(");
            string ejectCultivation = ExtractMethodBody(cultivationManager, "internal bool EjectCultivationContents(");
            string buildCultivationBatch = ExtractMethodBody(cultivationManager, "private int BuildCultivationEjectionBatch(");
            string canAcceptStateBatch = ExtractMethodBody(playerInventory, "private bool CanAcceptQuantityWithStateBatch(");
            string canStackStatefulItemAt = ExtractMethodBody(playerInventory, "private bool CanStackStatefulItemAt(");
            string append = ExtractMethodBody(resourceRecycler, "private void AppendRecyclerBufferedSaveSlot(ref ModuleDTO dto, ItemData item, int quantity)");
            string clearBuffer = ExtractMethodBody(resourceRecycler, "private void ClearBufferedInputState()");
            string constructionSave = ExtractMethodBody(constructionManager, "public void PopulateSaveData(SaveData data)");
            string constructionLoad = ExtractMethodBody(constructionManager, "public void LoadFromSaveData(SaveData data)");
            string deconstructFlow = ExtractMethodBody(constructionManager, "private void ProcessDeconstructionRequestAfterRayValidated");
            string deconstructTransaction = ExtractMethodBody(constructionManager, "private bool ExecuteDeconstructionTransaction(", 1);
            string baseEject = ExtractMethodBody(baseModule, "private bool EjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, ref Vector3 dropPosition)");
            string baseCanDrop = ExtractMethodBody(baseModule, "internal bool CanDropItemQuantityToInventoryOrWorld(", 1);
            string baseCanSpawnPooledWorldItemFallback = ExtractMethodBody(baseModule, "internal bool CanSpawnPooledWorldItemFallback(");
            string baseDrop = ExtractMethodBody(baseModule, "internal int DropItemQuantityToInventoryOrWorld(");
            string baseRegisterPersistentDrop = ExtractMethodBody(baseModule, "private bool TryRegisterPersistentDroppedItemQuantity(");
            string baseSpawnPooledWorldItem = ExtractMethodBody(baseModule, "private bool SpawnPooledWorldItem(");
            string worldCanRegisterDrop = ExtractMethodBody(persistentWorldRegistry, "internal bool CanRegisterDroppedItem(ItemData itemData, int quantity)");
            string worldCanRegisterDropAtPosition = ExtractMethodBody(persistentWorldRegistry, "internal bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition)");
            string worldCanRegisterDropByHash = ExtractMethodBody(persistentWorldRegistry, "internal bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity)");
            string worldTryRegisterDropStateful = ExtractMethodBody(persistentWorldRegistry, "private bool TryRegisterDroppedItemStateful(");
            string worldCanResolveDropRuntimePosition = ExtractMethodBody(persistentWorldRegistry, "private bool CanResolveDroppedItemRuntimePosition(Vector3 runtimePosition)");
            string worldCanResolveDropScatterEnvelope = ExtractMethodBody(persistentWorldRegistry, "private bool CanResolveDroppedItemScatterEnvelope(Vector3 runtimePosition)");
            string worldCanResolveDropLiftedSample = ExtractMethodBody(persistentWorldRegistry, "private bool CanResolveDroppedItemLiftedSample(");
            string writeModule = ExtractMethodBody(saveCodec, "private static bool WriteModule(ref BufferWriter writer, in ModuleDTO value)");
            string readModule = ExtractMethodBody(saveCodec, "private static bool ReadModule(ref BufferReader reader, int version, out ModuleDTO value)");

            StringAssert.Contains("public const int ResourceRecyclerModulePersistenceVersion = 79;", saveData);
            StringAssert.Contains("public const int FabricatorPendingOutputPersistenceVersion = 81;", saveData);
            StringAssert.Contains("public const int CultivationSeedHashPersistenceVersion = 82;", saveData);
            StringAssert.Contains("public const int CelestialLightPhasePersistenceVersion = 84;", saveData);
            StringAssert.Contains("public const int ProceduralTerrainIdentityContractPersistenceVersion = 85;", saveData);
            StringAssert.Contains("CurrentVersion = ProceduralTerrainIdentityContractPersistenceVersion", saveData);
            StringAssert.Contains("public const int MaxRecyclerBufferedSlots = 8;", saveData);
            StringAssert.Contains("public const int MaxRecyclerPendingYieldSlots = 16;", saveData);
            StringAssert.Contains("public int recyclerBufferedSlotCount;", saveData);
            StringAssert.Contains("public string[] recyclerBufferedItemIds;", saveData);
            StringAssert.Contains("public int[] recyclerBufferedQuantities;", saveData);
            StringAssert.Contains("public string recyclerActiveSourceItemId;", saveData);
            StringAssert.Contains("public int recyclerPendingYieldSlotCount;", saveData);
            StringAssert.Contains("public string[] recyclerPendingYieldItemIds;", saveData);
            StringAssert.Contains("public int[] recyclerPendingYieldQuantities;", saveData);
            StringAssert.Contains("public int[] cultivationSeedItemHashIds;", saveData);
            StringAssert.Contains("public string fabricatorPendingOutputItemId;", saveData);
            StringAssert.Contains("public int fabricatorPendingOutputQuantity;", saveData);
            StringAssert.Contains("public int fabricatorPendingOutputTotalQuantity;", saveData);
            StringAssert.Contains("public bool HasRecyclerSaveCapacity()", saveData);
            StringAssert.Contains("ResolveRecyclerBufferPersistenceSlotCount", saveData);
            StringAssert.Contains("ResolveRecyclerPendingYieldPersistenceSlotCount", saveData);
            StringAssert.Contains("SanitizeRecyclerBufferedQuantitiesCopyOnWrite", saveData);
            StringAssert.Contains("SanitizeRecyclerPendingYieldQuantitiesInPlace", saveData);

            StringAssert.Contains("private const int ResourceRecyclerModuleSaveVersion = SaveData.ResourceRecyclerModulePersistenceVersion;", saveCodec);
            StringAssert.Contains("private const int FabricatorPendingOutputSaveVersion = SaveData.FabricatorPendingOutputPersistenceVersion;", saveCodec);
            StringAssert.Contains("private const int CultivationSeedHashSaveVersion = SaveData.CultivationSeedHashPersistenceVersion;", saveCodec);
            StringAssert.Contains("private const int ModuleRecyclerBufferSlotMax = 8;", saveCodec);
            StringAssert.Contains("private const int ModuleRecyclerPendingYieldSlotMax = 16;", saveCodec);
            Assert.IsTrue(ContainsTokensInOrder(
                writeModule,
                "int recyclerBufferSlotCount = ClampPairedCollectionCount(",
                "safeValue.recyclerBufferedSlotCount",
                "ModuleRecyclerBufferSlotMax",
                "int recyclerPendingYieldSlotCount = ClampPairedCollectionCount(",
                "safeValue.recyclerPendingYieldSlotCount",
                "ModuleRecyclerPendingYieldSlotMax",
                "writer.WriteInt(recyclerBufferSlotCount)",
                "safeValue.recyclerBufferedItemIds",
                "safeValue.recyclerBufferedQuantities",
                "writer.WriteString(safeValue.recyclerActiveSourceItemId)",
                "writer.WriteInt(recyclerPendingYieldSlotCount)",
                "safeValue.recyclerPendingYieldItemIds",
                "safeValue.recyclerPendingYieldQuantities",
                "writer.WriteFloat(safeValue.posX)"));
            Assert.IsTrue(ContainsTokensInOrder(
                writeModule,
                "safeValue.cultivationQuality01",
                "writer.WriteStructArraySlice(safeValue.cultivationSeedItemHashIds, cultivationSlotCount)",
                "writer.WriteString(safeValue.fabricatorPendingOutputItemId)",
                "writer.WriteInt(safeValue.fabricatorPendingOutputQuantity)",
                "writer.WriteInt(safeValue.fabricatorPendingOutputTotalQuantity);"));
            Assert.IsTrue(ContainsTokensInOrder(
                readModule,
                "if (version >= ResourceRecyclerModuleSaveVersion)",
                "reader.ReadInt(out value.recyclerBufferedSlotCount)",
                "out value.recyclerBufferedItemIds",
                "out value.recyclerBufferedQuantities",
                "reader.ReadString(out value.recyclerActiveSourceItemId)",
                "reader.ReadInt(out value.recyclerPendingYieldSlotCount)",
                "out value.recyclerPendingYieldItemIds",
                "out value.recyclerPendingYieldQuantities",
                "else",
                "value.recyclerBufferedSlotCount = 0;",
                "value.recyclerActiveSourceItemId = string.Empty;",
                "value.recyclerPendingYieldSlotCount = 0;",
                "ok = reader.ReadFloat(out value.posX)"));
            Assert.IsTrue(ContainsTokensInOrder(
                readModule,
                "if (version >= CultivationSeedHashSaveVersion)",
                "out value.cultivationSeedItemHashIds",
                "if (version >= FabricatorPendingOutputSaveVersion",
                "reader.ReadString(out value.fabricatorPendingOutputItemId)",
                "reader.ReadInt(out value.fabricatorPendingOutputQuantity)",
                "reader.ReadInt(out value.fabricatorPendingOutputTotalQuantity)",
                "ModuleDTO.SanitizeForPersistenceInPlace(ref value);"));
            StringAssert.Contains("module.recyclerBufferedItemIds.Length == ModuleDTO.MaxRecyclerBufferedSlots", saveMigration);
            StringAssert.Contains("module.recyclerPendingYieldQuantities.Length == ModuleDTO.MaxRecyclerPendingYieldSlots", saveMigration);
            StringAssert.Contains("private static bool BackfillCultivationSeedHashIds(ref ModuleDTO module)", saveMigration);
            StringAssert.Contains("module.cultivationSeedItemHashIds[i] = seedHashId;", saveMigration);
            StringAssert.Contains("steps.Add(\"construction cultivation seed hashes repaired\");", saveMigration);
            StringAssert.Contains("private void ClearRecyclerRuntimeStateForRestore()", resourceRecycler);
            StringAssert.Contains("private static bool HasSavedRecyclerState(in ModuleDTO dto)", resourceRecycler);
            StringAssert.Contains("private bool CanResolveRecyclerRestoreState(in ModuleDTO dto, ItemCatalog itemCatalog)", resourceRecycler);
            Assert.IsTrue(ContainsTokensInOrder(
                restore,
                "bool hasSavedRecyclerState = HasSavedRecyclerState(in dto);",
                "if (!dto.HasRecyclerSaveCapacity() || itemCatalog == null)",
                "if (!hasSavedRecyclerState)",
                "ClearRecyclerRuntimeStateForRestore();",
                "return;",
                "if (!CanResolveRecyclerRestoreState(in dto, itemCatalog))",
                "return;",
                "ClearRecyclerRuntimeStateForRestore();",
                "int bufferSlotCountToRestore = Mathf.Min(",
                "_bufferItems[i] = item;",
                "_bufferQuantities[i] = quantity;"));
            int recyclerResolveIndex = restore.IndexOf("CanResolveRecyclerRestoreState(in dto, itemCatalog)", StringComparison.Ordinal);
            int recyclerCommitClearIndex = restore.IndexOf("ClearRecyclerRuntimeStateForRestore();", recyclerResolveIndex, StringComparison.Ordinal);
            Assert.That(recyclerCommitClearIndex, Is.GreaterThan(recyclerResolveIndex));
            Assert.That(restore, Does.Not.Contain("ClearBufferedInputState();\r\n            ClearPendingOutput();"));

            StringAssert.Contains("using Hecton8.Economy;", constructionManager);
            Assert.IsTrue(ContainsTokensInOrder(
                constructionSave,
                "if (module.TryGetComponent(out DeepDrillModule deepDrill))",
                "deepDrill.PopulateSaveData(ref moduleDto);",
                "if (module.TryGetComponent(out ResourceRecyclerModule resourceRecycler))",
                "resourceRecycler.PopulateSaveData(ref moduleDto);",
                "if (module.TryGetComponent(out Fabricator fabricator))",
                "fabricator.PopulateSaveData(ref moduleDto);",
                "if (module.TryGetComponent(out CultivationManager cultivationManager))"));
            Assert.IsTrue(ContainsTokensInOrder(
                constructionLoad,
                "if (!CanResolveConstructionItemReferencesForLoad(in dto, data.version, count, itemCatalog))",
                "return;",
                "TryResolveCachedObjectPool(out IObjectPoolService pool);",
                "ClearAllModules();",
                "if (module.TryGetComponent(out DeepDrillModule deepDrill))",
                "deepDrill.RestoreFromSaveData(moduleDto, itemCatalog);",
                "if (module.TryGetComponent(out ResourceRecyclerModule resourceRecycler))",
                "resourceRecycler.RestoreFromSaveData(moduleDto, itemCatalog);",
                "if (module.TryGetComponent(out Fabricator fabricator))",
                "fabricator.RestoreFromSaveData(moduleDto, itemCatalog);",
                "if (module.TryGetComponent(out CultivationManager cultivationManager))"));
            StringAssert.Contains("private static bool CanResolveConstructionItemReferencesForLoad(", constructionManager);
            StringAssert.Contains("private static bool CanResolveModuleItemReferencesForLoad(", constructionManager);
            StringAssert.Contains("private static bool ModuleRequiresItemCatalogForLoad(in ModuleDTO dto, int version)", constructionManager);
            StringAssert.Contains("private static bool CanResolveSavedItemArray(", constructionManager);
            StringAssert.Contains("private static bool HasCultivationSeedItemsRequiringCatalog(in ModuleDTO dto, int version)", constructionManager);
            StringAssert.Contains("private static bool CanResolveCultivationSeedItems(ItemCatalog itemCatalog, in ModuleDTO dto, int version)", constructionManager);
            StringAssert.Contains("private static bool HasSavedCultivationSeedHashId(in ModuleDTO dto, int slotIndex, int version)", constructionManager);
            StringAssert.Contains("!CanResolveOptionalItemId(itemCatalog, dto.pipeInFlightItemId, dto.pipeInFlightAmount)", constructionManager);
            StringAssert.Contains("!CanResolveOptionalItemId(itemCatalog, dto.fabricatorPendingOutputItemId, dto.fabricatorPendingOutputQuantity)", constructionManager);
            StringAssert.Contains("dto.storageCrateContentsSerialized &&", constructionManager);
            StringAssert.Contains("HasSavedItemArrayEntries(", constructionManager);
            StringAssert.Contains("dto.storageCrateItemIds", constructionManager);
            Assert.IsTrue(ContainsTokensInOrder(
                deepDrillRestore,
                "float restoredCycleTimer = Mathf.Clamp(",
                "bool hasSavedBufferedOutput =",
                "dto.drillBufferedAmount > 0",
                "!string.IsNullOrWhiteSpace(dto.drillBufferedItemId);",
                "if (!hasSavedBufferedOutput)",
                "ClearBufferedOutputState();",
                "_cycleTimer = restoredCycleTimer;",
                "if (itemCatalog == null)",
                "return;",
                "ItemData item = itemCatalog.FindById(dto.drillBufferedItemId);",
                "if (item == null)",
                "return;",
                "ClearBufferedOutputState();",
                "_cycleTimer = restoredCycleTimer;",
                "_bufferedItem = item;"));
            Assert.IsTrue(ContainsTokensInOrder(
                deconstructFlow,
                "PlayerInventory hostedContentInventory = null;",
                "if (!module.CanEjectHostedContentsForDeconstruction(hostedContentInventory, pool))",
                "if (!module.TryBeginAuthoritativeDeconstruction())",
                "if (!ExecuteDeconstructionTransaction(",
                "hostedContentInventory,",
                "pool,",
                "module.CancelAuthoritativeDeconstruction();",
                "if (targetNodeIndex >= 0)",
                "MarkDeconstructionEdgesSevered(targetNodeIndex)",
                "PublishDeconstructionVfx(in request);",
                "module.PrepareForDeconstructionPoolReturn();",
                "pool.Despawn(module.gameObject);"));
            Assert.That(deconstructFlow, Does.Not.Contain("if (!module.EjectHostedContentsForDeconstruction(hostedContentInventory, pool))"));
            Assert.IsTrue(ContainsTokensInOrder(
                deconstructTransaction,
                "int refundCommandCount = counters[DeconstructionRefundCommandCountIndex];",
                "if (module == null ||",
                "!module.CanEjectHostedContentsForDeconstruction(hostedContentInventory, pool) ||",
                "!module.EjectHostedContentsForDeconstruction(hostedContentInventory, pool))",
                "return false;",
                "int returnedCount = ApplyRefundCommandsOrOverflow(in request, inventory, refundCommandCount, refundCommands, lootCaches, counters);",
                "int publishedOverflowLootCacheCount = PublishOverflowLootCaches(lootCaches, counters);",
                "int rejectedOverflowLootCacheCount = math.max(0, overflowLootCacheCount - publishedOverflowLootCacheCount);",
                "ReadLastDeconstructionTelemetry(",
                "rejectedOverflowLootCacheCount,"));
            StringAssert.Contains("using Hecton8.Economy;", baseModule);
            Assert.IsTrue(ContainsTokensInOrder(
                baseCanDrop,
                "if (itemHashId == 0 || quantity <= 0)",
                "return false;",
                "ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);",
                "if (itemCatalog == null)",
                "return false;",
                "ItemData itemData = itemCatalog.FindByHash(itemHashId);",
                "if (itemData == null)",
                "return false;",
                "if (playerInventory != null &&",
                "playerInventory.CanAcceptItemQuantity(itemHashId, quantity))",
                "return true;",
                "if (!IsFiniteRuntimePosition(dropPosition))",
                "return false;",
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "persistentWorldRegistry.CanRegisterDroppedItem(itemData, quantity, dropPosition)",
                "return true;",
                "return false;"));
            Assert.That(baseCanDrop, Does.Not.Contain("playerInventory.Grid != null"));
            Assert.That(baseCanDrop, Does.Not.Contain("CanSpawnPooledWorldItemFallback"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseCanSpawnPooledWorldItemFallback,
                "if (itemHashId == 0 || pool == null || !IsFiniteRuntimePosition(position))",
                "return false;",
                "ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);",
                "itemCatalog.FindByHash(itemHashId) != null",
                "worldItemPrefab != null",
                "worldItemPrefab.TryGetComponent(out HectonItem _)"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseDrop,
                "bool persistentDropUnavailable = false;",
                "int remainingQuantity = quantity - delivered;",
                "TryRegisterPersistentDroppedItemQuantity(itemHashId, remainingQuantity, dropPosition, playerInventory)",
                "delivered += remainingQuantity;",
                "persistentDropUnavailable = true;",
                "SpawnPooledWorldItem(itemHashId, dropPosition, pool, playerInventory)"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseRegisterPersistentDrop,
                "if (itemHashId == 0 || quantity <= 0)",
                "return false;",
                "ItemCatalog itemCatalog = ResolveItemCatalog(playerInventory);",
                "if (itemCatalog == null)",
                "return false;",
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "persistentWorldRegistry.TryRegisterDroppedItem(itemHashId, itemCatalog, quantity, position);"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseSpawnPooledWorldItem,
                "if (!IsFiniteRuntimePosition(position))",
                "return false;",
                "if (worldItemPrefab == null)",
                "return false;",
                "if (pool == null)",
                "return false;"));
            Assert.That(baseSpawnPooledWorldItem, Does.Not.Contain("TryRegisterDroppedItem"));
            Assert.IsTrue(ContainsTokensInOrder(
                canEject,
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "int persistentWorldDropCandidateCount = 0;",
                "if (!CanDropItemDataQuantity(owner, _bufferItems[i], _bufferQuantities[i], inventory, pool, dropPosition))",
                "return false;",
                "persistentWorldDropCandidateCount += CountPersistentWorldDropCandidate(",
                "owner",
                "_bufferItems[i]",
                "_bufferQuantities[i]",
                "inventory",
                "pool",
                "dropPosition",
                "persistentWorldRegistry);",
                "persistentWorldDropCandidateCount += CountPersistentWorldDropCandidate(",
                "owner",
                "_activeSourceItem",
                "1",
                "pool",
                "dropPosition",
                "persistentWorldRegistry);",
                "ResourceStack stack = _pendingYield[i];",
                "persistentWorldDropCandidateCount += CountPersistentWorldDropCandidate(",
                "owner",
                "stack.Item",
                "stack.Amount",
                "pool",
                "dropPosition",
                "persistentWorldRegistry);",
                "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                countPersistentDropCandidate,
                "if (owner == null || persistentWorldRegistry == null || item == null || quantity <= 0)",
                "return 0;",
                "int itemHashId = ItemData.ResolvePersistentHashId(item);",
                "return itemHashId != 0 &&",
                "(inventory == null || !inventory.CanAcceptItemQuantity(itemHashId, quantity))",
                "persistentWorldRegistry.CanRegisterDroppedItem(item, quantity, dropPosition)",
                "? 1",
                ": 0;"));
            Assert.IsTrue(ContainsTokensInOrder(
                populateFabricator,
                "dto.fabricatorPendingOutputItemId = string.Empty;",
                "dto.fabricatorPendingOutputQuantity = 0;",
                "dto.fabricatorPendingOutputTotalQuantity = 0;",
                "if (!HasPendingCraftOutput)",
                "return;",
                "ItemData result = _pendingCraftOutputItem;",
                "dto.fabricatorPendingOutputItemId = persistentId;",
                "dto.fabricatorPendingOutputQuantity = quantity;",
                "dto.fabricatorPendingOutputTotalQuantity = math.max(quantity, _pendingCraftOutputTotalQuantity);"));
            Assert.IsTrue(ContainsTokensInOrder(
                restoreFabricator,
                "int quantity = math.max(0, dto.fabricatorPendingOutputQuantity);",
                "if (quantity <= 0)",
                "ClearPendingCraftOutput();",
                "return;",
                "if (itemCatalog == null || string.IsNullOrWhiteSpace(itemId))",
                "return;",
                "ItemData result = itemCatalog.FindById(itemId);",
                "if (result == null)",
                "return;",
                "ClearPendingCraftOutput();",
                "_pendingCraftOutputItem = result;",
                "_pendingCraftOutputQuantity = quantity;",
                "_pendingCraftOutputTotalQuantity = math.max(quantity, dto.fabricatorPendingOutputTotalQuantity);"));
            Assert.IsTrue(ContainsTokensInOrder(
                populateCultivation,
                "int[] seedHashIds = moduleDto.cultivationSeedItemHashIds;",
                "ItemData item = itemCatalog != null ? itemCatalog.FindByHash(slot.SeedItemHashId) : null;",
                "seedIds[writeIndex] = item != null && !string.IsNullOrWhiteSpace(item.PersistentId)",
                "seedHashIds[writeIndex] = slot.SeedItemHashId;",
                "moduleDto.cultivationSlotCount = writeIndex;"));
            Assert.IsTrue(ContainsTokensInOrder(
                restoreCultivation,
                "int safeCount = ResolveCultivationRestoreCount(in moduleDto);",
                "if (safeCount <= 0 || !HasSavedCultivationRestoreState(in moduleDto, safeCount))",
                "ClearSlots();",
                "if (!CanResolveCultivationRestoreState(in moduleDto, itemCatalog, safeCount))",
                "return;",
                "ClearSlots();",
                "int itemHashId = ResolveSavedCultivationSeedHashId(in moduleDto, itemCatalog, i, persistentId);"));
            Assert.IsTrue(ContainsTokensInOrder(
                canEjectFabricator,
                "if (!HasPendingCraftOutput)",
                "return true;",
                "ItemData result = _pendingCraftOutputItem;",
                "int itemHashId = ComputeItemHash(result);",
                "int quantity = math.max(1, _pendingCraftOutputQuantity);",
                "inventory.CanAcceptItemQuantity(itemHashId, quantity)",
                "PersistentWorldRegistry registry = _persistentWorldRegistry;",
                "IsFiniteRuntimePosition(dropPosition)",
                "registry.CanRegisterDroppedItem(result, quantity, dropPosition);"));
            Assert.IsTrue(ContainsTokensInOrder(
                ejectFabricator,
                "if (!CanEjectPendingCraftOutput(inventory, dropPosition))",
                "return false;",
                "inventory.CanAcceptItemQuantity(itemHashId, quantity)",
                "inventory.TryAddItem(itemHashId, quantity)",
                "ClearPendingCraftOutput();",
                "registry.TryRegisterDroppedItem(result, quantity, dropPosition)",
                "ClearPendingCraftOutput();",
                "dropPosition.x += 0.3f;"));
            Assert.IsTrue(ContainsTokensInOrder(
                canEjectCultivation,
                "Span<int> itemHashIds = stackalloc int[MaxCultivationSlots];",
                "Span<int> quantities = stackalloc int[MaxCultivationSlots];",
                "Span<ulong> geneticsMasks = stackalloc ulong[MaxCultivationSlots];",
                "Span<ushort> qualityMillis = stackalloc ushort[MaxCultivationSlots];",
                "int occupiedCount = BuildCultivationEjectionBatch(itemHashIds, quantities, geneticsMasks, qualityMillis);",
                "inventory.CanAcceptItemWithStateBatch(itemHashIds, geneticsMasks, qualityMillis, occupiedCount)",
                "ItemCatalog itemCatalog = ResolveEjectionItemCatalog(inventory);",
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "ItemData item = itemCatalog.FindByHash(itemHashIds[i]);",
                "persistentWorldRegistry.CanRegisterDroppedItem(item, quantities[i], dropPosition)",
                "persistentWorldRegistry.CanRegisterDroppedItemBatch(occupiedCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                ejectCultivation,
                "if (!CanEjectCultivationContents(owner, inventory, dropPosition))",
                "return false;",
                "ulong geneticsMask = SanitizeGeneticsMask(slot.GeneticsMask);",
                "ushort qualityMilli = ResolveCultivationQualityMilli(slot.Quality01);",
                "inventory.TryAddItemWithState(slot.SeedItemHashId, geneticsMask, qualityMilli)",
                "_slots[i] = default;",
                "persistentWorldRegistry.TryRegisterDroppedItemWithState(",
                "geneticsMask",
                "qualityMilli",
                "_slots[i] = default;",
                "dropPosition.x += 0.3f;"));
            Assert.IsTrue(ContainsTokensInOrder(
                buildCultivationBatch,
                "Span<ulong> geneticsMasks",
                "Span<ushort> qualityMillis",
                "CultivationSlotState slot = _slots[i];",
                "int itemHashId = slot.SeedItemHashId;",
                "if (itemHashId == 0)",
                "continue;",
                "itemHashIds[count] = itemHashId;",
                "quantities[count] = 1;",
                "geneticsMasks[count] = SanitizeGeneticsMask(slot.GeneticsMask);",
                "qualityMillis[count] = ResolveCultivationQualityMilli(slot.Quality01);",
                "count++;"));
            StringAssert.Contains("public bool CanAcceptItemWithStateBatch(", playerInventory);
            Assert.IsTrue(ContainsTokensInOrder(
                canAcceptStateBatch,
                "CopyNativeArray(_stackCounts, _scavengeSimStackCounts);",
                "_grid.CopyOccupiedMask(_simulationOccupiedCells);",
                "byte compressedGenetics = CompressItemGenetics(geneticsMasks[groupIndex]);",
                "ushort resolvedQualityMilli = NormalizeQualityMilli(qualityMillis[groupIndex]);",
                "!CanStackStatefulItemAt(anchorIndex, resolvedStateFlags, compressedGenetics, resolvedQualityMilli)",
                "TryReservePlacementInSimulation(in descriptor)",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                canStackStatefulItemAt,
                "_itemStateFlags[anchorIndex] == itemStateFlags",
                "_itemGenetics[anchorIndex] == geneticsMask",
                "NormalizeQualityMilli(_qualityMilli[anchorIndex]) == qualityMilli"));
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanRegisterDrop,
                "if (!CanRegisterDroppedItemData(itemData, quantity, out string persistentId))",
                "return false;",
                "return ComputePersistentIdHash(persistentId) != 0UL &&",
                "CanAppendDroppedItemState();"));
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanRegisterDropAtPosition,
                "if (!CanRegisterDroppedItemData(itemData, quantity, out string persistentId))",
                "return false;",
                "return ComputePersistentIdHash(persistentId) != 0UL &&",
                "CanAppendDroppedItemState() &&",
                "CanResolveDroppedItemRuntimePosition(runtimePosition);"));
            StringAssert.Contains("private bool CanAppendDroppedItemState()", persistentWorldRegistry);
            StringAssert.Contains("internal bool CanRegisterDroppedItemBatch(int recordCount)", persistentWorldRegistry);
            StringAssert.Contains("public int Count => ReadCount();", persistentWorldRegistry);
            StringAssert.Contains("private bool CanAppendDroppedItemState(int recordCount)", persistentWorldRegistry);
            StringAssert.Contains("long requiredRecordCount = nextRecordIndex + recordCount;", persistentWorldRegistry);
            StringAssert.Contains("CanGenerateDroppedItemInstanceUidBatch(recordCount)", persistentWorldRegistry);
            StringAssert.Contains("requiredRecordCount <= _records.Capacity", persistentWorldRegistry);
            StringAssert.Contains("(long)_recordsByChunk.Count + recordCount <= _recordsByChunk.Capacity", persistentWorldRegistry);
            StringAssert.Contains("(long)_deltaRecords.Length + recordCount <= _deltaRecords.Capacity", persistentWorldRegistry);
            StringAssert.Contains("(long)_deltaRecordIndexByEntityId.Count + recordCount <= _deltaRecordIndexByEntityId.Capacity", persistentWorldRegistry);
            StringAssert.Contains("(long)_guidToPoolIndex.Count + recordCount <= _guidToPoolIndex.Capacity", persistentWorldRegistry);
            StringAssert.Contains("(long)_entityStateByInstanceUid.Count + recordCount <= _entityStateByInstanceUid.Capacity", persistentWorldRegistry);
            StringAssert.Contains("private static bool CanGenerateDroppedItemInstanceUidBatch(int recordCount)", persistentWorldRegistry);
            StringAssert.Contains("int counterSnapshot = Volatile.Read(ref _nextInstanceUidCounter);", persistentWorldRegistry);
            StringAssert.Contains("long requiredSequence = (long)counterSnapshot + recordCount;", persistentWorldRegistry);
            StringAssert.Contains("requiredSequence <= InstanceUidCounterMask", persistentWorldRegistry);
            Assert.IsTrue(ContainsTokensInOrder(
                worldTryRegisterDropStateful,
                "!CanRegisterDroppedItemData(itemData, quantity, out string persistentId)",
                "!CanAppendDroppedItemState()",
                "ulong persistentIdHash = ComputePersistentIdHash(persistentId);",
                "TryGenerateInstanceUid(itemData, persistentIdHash, out uint instanceUid)"));
            StringAssert.Contains("private bool CanResolveDroppedItemRuntimePosition(Vector3 runtimePosition)", persistentWorldRegistry);
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanResolveDropRuntimePosition,
                "if (!CanResolveDroppedItemRuntimePositionSample(runtimePosition))",
                "return false;",
                "return CanResolveDroppedItemScatterEnvelope(runtimePosition);"));
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanResolveDropScatterEnvelope,
                "DropScatterMinLiftMeters",
                "DropScatterMaxLiftMeters",
                "for (uint directionIndex = 0u; directionIndex < 8u; directionIndex++)",
                "ResolveScatterPlanarDirection(directionIndex << 29)",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanResolveDropLiftedSample,
                "sample.x += directionX * DropScatterRadiusMeters;",
                "sample.y += liftMeters;",
                "sample.z += directionZ * DropScatterRadiusMeters;",
                "return CanResolveDroppedItemRuntimePositionSample(sample);"));
            StringAssert.Contains("private bool CanResolveDroppedItemRuntimePositionSample(Vector3 runtimePosition)", persistentWorldRegistry);
            StringAssert.Contains("AbsoluteUniversePosition.ResolveChunkId(in position, chunkSizeMeters)", persistentWorldRegistry);
            StringAssert.Contains("AbsoluteUniversePosition.IsValidChunkId(chunkId)", persistentWorldRegistry);
            Assert.That(worldCanRegisterDropAtPosition, Does.Not.Contain("TryGenerateInstanceUid"));
            Assert.IsTrue(ContainsTokensInOrder(
                worldCanRegisterDropByHash,
                "if (itemHashId == 0 || itemCatalog == null)",
                "return false;",
                "return CanRegisterDroppedItem(itemCatalog.FindByHash(itemHashId), quantity);"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseEject,
                "bool allDelivered = true;",
                "if (TryGetComponent(out LogisticsSorterModule sorterModule))",
                "allDelivered &= sorterModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);",
                "if (TryGetComponent(out ResourceRecyclerModule recyclerModule))",
                "allDelivered &= recyclerModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);",
                "if (TryGetComponent(out Fabricator fabricator))",
                "allDelivered &= fabricator.EjectPendingCraftOutput(playerInventory, ref dropPosition);",
                "if (TryGetComponent(out CultivationManager cultivationManager))",
                "allDelivered &= cultivationManager.EjectCultivationContents(this, playerInventory, ref dropPosition);",
                "if (TryGetComponent(out LogisticsPipeNode pipeNode)",
                "return allDelivered;"));

            StringAssert.Contains("using Hecton8.SaveSystem;", resourceRecycler);
            StringAssert.Contains("using Hecton8.Gameplay;", resourceRecycler);
            Assert.IsTrue(ContainsTokensInOrder(
                populate,
                "dto.recyclerBufferedSlotCount = 0;",
                "dto.recyclerActiveSourceItemId = string.Empty;",
                "dto.recyclerPendingYieldSlotCount = 0;",
                "if (!dto.HasRecyclerSaveCapacity())",
                "AppendRecyclerBufferedSaveSlot(ref dto, item, quantity);",
                "if (_isProcessing && _activeSourceItem != null)",
                "AppendRecyclerBufferedSaveSlot(ref dto, _activeSourceItem, 1);",
                "if (!_hasPendingOutput || _pendingYield == null || _pendingYieldCount <= 0)",
                "dto.recyclerActiveSourceItemId = _activeSourceItem != null ? _activeSourceItem.PersistentId : string.Empty;",
                "dto.recyclerPendingYieldItemIds[slot] = stack.Item.PersistentId;",
                "dto.recyclerPendingYieldQuantities[slot] = stack.Amount;"));
            Assert.IsTrue(ContainsTokensInOrder(
                append,
                "string itemId = item.PersistentId;",
                "for (int i = 0; i < dto.recyclerBufferedSlotCount; i++)",
                "string.Equals(dto.recyclerBufferedItemIds[i], itemId, System.StringComparison.Ordinal)",
                "dto.recyclerBufferedQuantities[i] += quantity;",
                "return;",
                "if (slot >= ModuleDTO.MaxRecyclerBufferedSlots)",
                "return;",
                "dto.recyclerBufferedItemIds[slot] = itemId;",
                "dto.recyclerBufferedQuantities[slot] = quantity;",
                "dto.recyclerBufferedSlotCount++;"));
            Assert.IsTrue(ContainsTokensInOrder(
                restore,
                "ClearBufferedInputState();",
                "ClearPendingOutput();",
                "_isProcessing = false;",
                "if (itemCatalog == null || !dto.HasRecyclerSaveCapacity())",
                "int bufferSlotCountToRestore = Mathf.Min(",
                "_bufferItems[i] = item;",
                "_bufferQuantities[i] = quantity;",
                "_bufferedItemCount += quantity;",
                "int pendingYieldSlotsToRestore = Mathf.Min(",
                "_pendingYieldScratch[restoredYieldCount] = new ResourceStack",
                "if (restoredYieldCount > 0)",
                "_activeSourceItem = !string.IsNullOrWhiteSpace(dto.recyclerActiveSourceItemId)",
                "_pendingYield = _pendingYieldScratch;",
                "_hasPendingOutput = true;",
                "_debugHasPendingOutput = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                eject,
                "bool stoppedProcessingAfterSourceEject = false;",
                "bool allDelivered = true;",
                "for (int i = 0; i < bufferSlotCount; i++)",
                "int quantity = _bufferQuantities[i];",
                "int delivered = DropItemDataQuantity(owner, _bufferItems[i], quantity, inventory, pool, ref dropPosition);",
                "if (delivered >= quantity)",
                "_bufferItems[i] = null;",
                "_bufferQuantities[i] = 0;",
                "int safeDelivered = Mathf.Max(0, delivered);",
                "_bufferQuantities[i] = quantity - safeDelivered;",
                "allDelivered = false;",
                "if (_isProcessing && _activeSourceItem != null)",
                "int delivered = DropItemDataQuantity(owner, _activeSourceItem, 1, inventory, pool, ref dropPosition);",
                "_activeSourceItem = null;",
                "_isProcessing = false;",
                "_debugIsProcessing = false;",
                "stoppedProcessingAfterSourceEject = true;",
                "else",
                "allDelivered = false;",
                "if (_hasPendingOutput && _pendingYield != null)",
                "int delivered = DropItemDataQuantity(owner, stack.Item, stack.Amount, inventory, pool, ref dropPosition);",
                "if (delivered >= stack.Amount)",
                "stack.Amount -= Mathf.Max(0, delivered);",
                "allDelivered = false;",
                "if (allDelivered)",
                "ClearBufferedInputState();",
                "ClearPendingOutput();",
                "_isProcessing = false;",
                "if (wasProcessing && (allDelivered || stoppedProcessingAfterSourceEject))",
                "NotifyGridBalanceChanged();",
                "return allDelivered;"));
            Assert.IsTrue(ContainsTokensInOrder(
                clearBuffer,
                "for (int i = 0; i < MaxBufferSlots; i++)",
                "_bufferItems[i] = null;",
                "_bufferQuantities[i] = 0;",
                "_bufferedItemCount = 0;",
                "_debugBufferedItemCount = 0;"));
        }

        [Test]
        public void StorageCrateModule_PersistsConstructedCrateContentsThroughConstructionSave()
        {
            string saveData = ReadProjectFile("Assets/_Project/Scripts/SaveData.cs");
            string saveCodec = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string saveMigration = ReadProjectFile("Assets/_Project/Scripts/SaveDataMigration.cs");
            string baseModule = ReadProjectFile("Assets/_Project/Scripts/BaseModule.cs");
            string constructionManager = ReadProjectFile("Assets/_Project/Scripts/ConstructionManager.cs");
            string storageCrate = ReadProjectFile("Assets/_Project/Scripts/Gameplay/StorageCrate.cs");
            string logisticsSorter = ReadProjectFile("Assets/_Project/Scripts/Construction/LogisticsSorterModule.cs");
            string logisticsPipe = ReadProjectFile("Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs");
            string populate = ExtractMethodBody(storageCrate, "internal void PopulateSaveData(ref ModuleDTO dto)");
            string restore = ExtractMethodBody(storageCrate, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)");
            string legacyClear = ExtractMethodBody(storageCrate, "internal void ClearRuntimeContentsForLegacyLoad()");
            string canEjectContents = ExtractMethodBody(storageCrate, "internal bool CanEjectContainedContents(");
            string ejectContents = ExtractMethodBody(storageCrate, "internal bool EjectContainedContents(");
            string sorterCanEject = ExtractMethodBody(logisticsSorter, "internal bool CanEjectBufferedContents(BaseModule owner, PlayerInventory inventory, IObjectPoolService pool, Vector3 dropPosition)");
            string pipePopulate = ExtractMethodBody(logisticsPipe, "internal void PopulateSaveData(ref ModuleDTO dto)");
            string pipeRestore = ExtractMethodBody(logisticsPipe, "internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)");
            string pipeResolveInFlightLoss = ExtractMethodBody(logisticsPipe, "private void ResolveInFlightLossToWorldOrRollback(");
            string pipeReturnCommittedInFlight = ExtractMethodBody(logisticsPipe, "private bool TryReturnCommittedInFlightItemToSource()");
            string append = ExtractMethodBody(storageCrate, "private static bool TryAppendStorageCrateSaveEntry(");
            string countRestoreSlots = ExtractMethodBody(storageCrate, "private static int CountStorageCrateRestoreSlots(");
            string ensureCapacity = ExtractMethodBody(storageCrate, "private void EnsureContainedItemStorageCapacityForRestore(");
            string takeToInventory = ExtractMethodBody(storageCrate, "public bool TakeItemToInventory(int itemIndex, PlayerInventory playerInventory)");
            string baseEject = ExtractMethodBody(baseModule, "private bool EjectHostedModuleContents(PlayerInventory playerInventory, IObjectPoolService pool, ref Vector3 dropPosition)");
            string constructionSave = ExtractMethodBody(constructionManager, "public void PopulateSaveData(SaveData data)");
            string constructionLoad = ExtractMethodBody(constructionManager, "public void LoadFromSaveData(SaveData data)");
            string writeModule = ExtractMethodBody(saveCodec, "private static bool WriteModule(ref BufferWriter writer, in ModuleDTO value)");
            string readModule = ExtractMethodBody(saveCodec, "private static bool ReadModule(ref BufferReader reader, int version, out ModuleDTO value)");

            StringAssert.Contains("public const int StorageCrateModulePersistenceVersion = 80;", saveData);
            StringAssert.Contains("public const int CelestialLightPhasePersistenceVersion = 84;", saveData);
            StringAssert.Contains("public const int ProceduralTerrainIdentityContractPersistenceVersion = 85;", saveData);
            StringAssert.Contains("CurrentVersion = ProceduralTerrainIdentityContractPersistenceVersion", saveData);
            StringAssert.Contains("public const int MaxStorageCrateSlots = 32;", saveData);
            StringAssert.Contains("public bool storageCrateContentsSerialized;", saveData);
            StringAssert.Contains("public int storageCrateSlotCount;", saveData);
            StringAssert.Contains("public string[] storageCrateItemIds;", saveData);
            StringAssert.Contains("public int[] storageCrateQuantities;", saveData);
            StringAssert.Contains("HasStorageCrateSaveCapacity()", saveData);
            StringAssert.Contains("ResolveStorageCratePersistenceSlotCount", saveData);
            StringAssert.Contains("SanitizeStorageCrateQuantitiesCopyOnWrite", saveData);
            StringAssert.Contains("SanitizeStorageCrateQuantitiesInPlace", saveData);
            StringAssert.Contains("module.storageCrateItemIds.Length == ModuleDTO.MaxStorageCrateSlots", saveMigration);
            StringAssert.Contains("module.storageCrateQuantities.Length == ModuleDTO.MaxStorageCrateSlots", saveMigration);

            StringAssert.Contains("private const int StorageCrateModuleSaveVersion = SaveData.StorageCrateModulePersistenceVersion;", saveCodec);
            StringAssert.Contains("private const int ModuleStorageCrateSlotMax = 32;", saveCodec);
            Assert.IsTrue(ContainsTokensInOrder(
                writeModule,
                "int storageCrateSlotCount = safeValue.storageCrateContentsSerialized",
                "safeValue.storageCrateSlotCount",
                "ModuleStorageCrateSlotMax",
                "safeValue.storageCrateItemIds",
                "safeValue.storageCrateQuantities",
                "writer.WriteBool(safeValue.storageCrateContentsSerialized)",
                "writer.WriteInt(storageCrateSlotCount)",
                "safeValue.storageCrateItemIds",
                "safeValue.storageCrateQuantities",
                "writer.WriteFloat(safeValue.posX)"));
            Assert.IsTrue(ContainsTokensInOrder(
                readModule,
                "if (version >= StorageCrateModuleSaveVersion)",
                "reader.ReadBool(out value.storageCrateContentsSerialized)",
                "reader.ReadInt(out value.storageCrateSlotCount)",
                "out value.storageCrateItemIds",
                "out value.storageCrateQuantities",
                "else",
                "value.storageCrateContentsSerialized = false;",
                "value.storageCrateSlotCount = 0;",
                "value.storageCrateItemIds = null;",
                "value.storageCrateQuantities = null;"));

            Assert.IsTrue(ContainsTokensInOrder(
                constructionSave,
                "if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))",
                "logisticsPipe.PopulateSaveData(ref moduleDto);",
                "if (module.TryGetComponent(out StorageCrate storageCrate))",
                "storageCrate.PopulateSaveData(ref moduleDto);"));
            Assert.IsTrue(ContainsTokensInOrder(
                constructionLoad,
                "if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))",
                "logisticsPipe.RestoreFromSaveData(moduleDto, itemCatalog);",
                "if (hasLegacyModuleState && module.TryGetComponent(out StorageCrate storageCrate))",
                "if (data.version >= SaveData.StorageCrateModulePersistenceVersion)",
                "storageCrate.RestoreFromSaveData(moduleDto, itemCatalog);",
                "else",
                "storageCrate.ClearRuntimeContentsForLegacyLoad();",
                "RegisterModule(module, buildData);"));
            Assert.IsTrue(ContainsTokensInOrder(
                pipePopulate,
                "if (_activeReservationId > 0)",
                "sourceCrate?.CommitReservation(_activeReservationId);",
                "_activeReservationId = 0;",
                "dto.pipeInFlightItemId = _inFlightItem.PersistentId;",
                "dto.pipeInFlightAmount = 1;"));
            Assert.IsTrue(ContainsTokensInOrder(
                pipeRestore,
                "float restoredExportTimer = math.clamp(",
                "bool hasSavedInFlightItem =",
                "dto.pipeInFlightAmount > 0",
                "!string.IsNullOrWhiteSpace(dto.pipeInFlightItemId);",
                "if (!hasSavedInFlightItem)",
                "ClearInFlightState();",
                "_exportTimer = restoredExportTimer;",
                "if (itemCatalog == null)",
                "return;",
                "ItemData item = itemCatalog.FindById(dto.pipeInFlightItemId);",
                "if (item == null)",
                "return;",
                "ClearInFlightState();",
                "_exportTimer = restoredExportTimer;",
                "_inFlightItem = item;"));
            Assert.IsTrue(ContainsTokensInOrder(
                pipeResolveInFlightLoss,
                "if (TrySpillInFlightItemToWorld(spillPosition))",
                "return;",
                "if (TryReturnCommittedInFlightItemToSource())",
                "return;",
                "RollbackInFlightTransfer();"));
            Assert.IsTrue(ContainsTokensInOrder(
                pipeReturnCommittedInFlight,
                "if (_inFlightItem == null ||",
                "_activeReservationId > 0 ||",
                "sourceCrate == null ||",
                "!sourceCrate.HasAutomatedCapacity())",
                "return false;",
                "if (!sourceCrate.TryAddAutomatedItem(_inFlightItem))",
                "return false;",
                "ClearInFlightState();",
                "NotifyGridBalanceChanged();",
                "return true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                baseEject,
                "bool allDelivered = true;",
                "if (TryGetComponent(out ResourceRecyclerModule recyclerModule))",
                "allDelivered &= recyclerModule.EjectBufferedContents(this, playerInventory, pool, ref dropPosition);",
                "if (TryGetComponent(out Fabricator fabricator))",
                "allDelivered &= fabricator.EjectPendingCraftOutput(playerInventory, ref dropPosition);",
                "if (TryGetComponent(out CultivationManager cultivationManager))",
                "allDelivered &= cultivationManager.EjectCultivationContents(this, playerInventory, ref dropPosition);",
                "if (TryGetComponent(out StorageCrate storageCrate))",
                "allDelivered &= storageCrate.EjectContainedContents(this, playerInventory, pool, ref dropPosition);",
                "if (TryGetComponent(out LogisticsPipeNode pipeNode)",
                "return allDelivered;"));

            Assert.IsTrue(ContainsTokensInOrder(
                populate,
                "dto.storageCrateContentsSerialized = true;",
                "dto.storageCrateSlotCount = 0;",
                "string[] itemIds = dto.storageCrateItemIds;",
                "int[] quantities = dto.storageCrateQuantities;",
                "if (itemIds == null || quantities == null)",
                "return;",
                "itemIds[i] = string.Empty;",
                "quantities[i] = 0;",
                "if (items == null || items.Length == 0)",
                "return;",
                "EnsureReservationCapacity();",
                "ItemData item = items[i];",
                "if (item == null)",
                "continue;",
                "if (IsReservedSlot(i))",
                "continue;",
                "string persistentId = item.PersistentId;",
                "if (string.IsNullOrWhiteSpace(persistentId))",
                "continue;",
                "if (!TryAppendStorageCrateSaveEntry(itemIds, quantities, ref writeCount, persistentId))",
                "break;",
                "dto.storageCrateSlotCount = writeCount;"));
            Assert.IsTrue(ContainsTokensInOrder(
                takeToInventory,
                "if (containedItems == null || itemIndex < 0 || itemIndex >= containedItems.Length) return false;",
                "EnsureReservationCapacity();",
                "if (IsReservedSlot(itemIndex)) return false;",
                "ItemData item = containedItems[itemIndex];",
                "if (playerInventory == null)",
                "if (itemHashId == 0 || !playerInventory.TryAddItem(itemHashId, 1))",
                "if (removeItemsOnTake)",
                "containedItems[itemIndex] = null;",
                "_reservedSlotIds[itemIndex] = 0;",
                "SetContainedItemHash(itemIndex, null);"));
            Assert.IsTrue(ContainsTokensInOrder(
                restore,
                "if (!dto.storageCrateContentsSerialized)",
                "return;",
                "if (!CanResolveStorageCrateRestoreState(in dto, itemCatalog))",
                "return;",
                "int requiredSlotCount = CountStorageCrateRestoreSlots(in dto);",
                "EnsureContainedItemStorageCapacityForRestore(requiredSlotCount);",
                "ClearContainedItemsForRestore();",
                "if (requiredSlotCount <= 0",
                "return;",
                "int entryCount = Mathf.Clamp(",
                "dto.storageCrateSlotCount",
                "ModuleDTO.MaxStorageCrateSlots",
                "int quantity = Mathf.Clamp(dto.storageCrateQuantities[entryIndex], 0, ModuleDTO.MaxStorageCrateSlots);",
                "if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))",
                "continue;",
                "ItemData item = itemCatalog.FindById(itemId);",
                "if (item == null)",
                "continue;",
                "containedItems[writeIndex] = item;",
                "SetContainedItemHash(writeIndex, item);"));
            StringAssert.Contains("private static bool CanResolveStorageCrateRestoreState(in ModuleDTO dto, ItemCatalog itemCatalog)", storageCrate);
            StringAssert.Contains("dto.storageCrateItemIds.Length < entryCount", storageCrate);
            StringAssert.Contains("if (itemCatalog == null || itemCatalog.FindById(itemId.Trim()) == null)", storageCrate);
            Assert.IsTrue(ContainsTokensInOrder(
                legacyClear,
                "ClearContainedItemsForRestore();"));
            Assert.IsTrue(ContainsTokensInOrder(
                canEjectContents,
                "if (owner == null || containedItems == null || containedItems.Length == 0)",
                "return true;",
                "EnsureReservationCapacity();",
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "int persistentWorldDropCandidateCount = 0;",
                "ItemData item = containedItems[i];",
                "if (item == null)",
                "continue;",
                "if (IsReservedSlot(i))",
                "continue;",
                "int itemHashId = ItemData.ResolvePersistentHashId(item);",
                "!owner.CanDropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, dropPosition)",
                "return false;",
                "persistentWorldRegistry.CanRegisterDroppedItem(item, 1, dropPosition)",
                "persistentWorldDropCandidateCount++;",
                "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                sorterCanEject,
                "PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;",
                "int persistentWorldDropCandidateCount = 0;",
                "int itemHashId = ItemData.ResolvePersistentHashId(item);",
                "if (!owner.CanDropItemQuantityToInventoryOrWorld(itemHashId, quantity, inventory, pool, dropPosition))",
                "return false;",
                "if (persistentWorldRegistry != null &&",
                "(inventory == null || !inventory.CanAcceptItemQuantity(itemHashId, quantity)) &&",
                "persistentWorldRegistry.CanRegisterDroppedItem(item, quantity, dropPosition))",
                "persistentWorldDropCandidateCount++;",
                "persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount);"));
            Assert.IsTrue(ContainsTokensInOrder(
                append,
                "if (!string.Equals(itemIds[i], persistentId, StringComparison.Ordinal))",
                "continue;",
                "quantities[i] = Mathf.Min(ModuleDTO.MaxStorageCrateSlots, quantities[i] + 1);",
                "return true;",
                "if (writeCount >= capacity)",
                "return false;",
                "itemIds[writeCount] = persistentId;",
                "quantities[writeCount] = 1;",
                "writeCount++;"));
            Assert.IsTrue(ContainsTokensInOrder(
                countRestoreSlots,
                "if (!dto.storageCrateContentsSerialized",
                "return 0;",
                "int entryCount = Mathf.Clamp(",
                "if (string.IsNullOrWhiteSpace(dto.storageCrateItemIds[i]))",
                "continue;",
                "int quantity = Mathf.Clamp(dto.storageCrateQuantities[i], 0, ModuleDTO.MaxStorageCrateSlots);",
                "totalQuantity = Mathf.Min(ModuleDTO.MaxStorageCrateSlots, totalQuantity + quantity);"));
            Assert.IsTrue(ContainsTokensInOrder(
                ensureCapacity,
                "int safeRequiredSlotCount = Mathf.Clamp(requiredSlotCount, 0, ModuleDTO.MaxStorageCrateSlots);",
                "if (currentSlotCount < safeRequiredSlotCount)",
                "System.Array.Resize(ref containedItems, safeRequiredSlotCount);",
                "EnsureReservationCapacity();"));
            Assert.IsTrue(ContainsTokensInOrder(
                ejectContents,
                "if (owner == null || containedItems == null || containedItems.Length == 0)",
                "return true;",
                "EnsureReservationCapacity();",
                "bool allDelivered = true;",
                "ItemData item = containedItems[i];",
                "if (item == null)",
                "continue;",
                "if (IsReservedSlot(i))",
                "continue;",
                "int itemHashId = ItemData.ResolvePersistentHashId(item);",
                "if (itemHashId == 0 ||",
                "owner.DropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, ref dropPosition) != 1)",
                "allDelivered = false;",
                "continue;",
                "containedItems[i] = null;",
                "_reservedSlotIds[i] = 0;",
                "SetContainedItemHash(i, null);",
                "anyRemoved = true;",
                "if (anyRemoved)",
                "OnEmpty?.Invoke();",
                "return allDelivered;"));
        }

        [Test]
        public void ModRecipeRegistry_RemovesDisabledOwnerRecipesAndInvalidatesFabricatorReaders()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string fabricator = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string register = ExtractMethodBody(source, "internal static bool TryRegister(RecipeData recipeData, out string error)");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterModRecipes(string modId)");
            string flush = ExtractMethodBody(source, "internal static void FlushPendingRegistrations()", 1);
            string getAt = ExtractMethodBody(source, "internal static RecipeData GetAt(int index)");
            string contains = ExtractMethodBody(source, "private static bool ContainsRecipeReference(RecipeData recipeData)");
            string find = ExtractMethodBody(source, "private static bool TryFindRecipeReference(RecipeData recipeData, out int index)");
            string promoteOwner = ExtractMethodBody(source, "private static void PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(int index)");
            string removeStale = ExtractMethodBody(source, "private static bool RemoveStaleOwnerRecipes()");
            string ownerGuard = ExtractMethodBody(source, "private static bool IsRuntimeOwnerStillRegistered(uint modHash)");
            string fabricatorEvent = ExtractMethodBody(fabricator, "private void HandleModRegistryEvent(in ModRegistryEventPayload payload)");

            StringAssert.Contains("private struct RuntimeRecipeRegistration", source);
            StringAssert.Contains("private static readonly List<RuntimeRecipeRegistration> _runtimeRecipes", source);
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "bool removedStaleOwnerRecipes = RemoveStaleOwnerRecipes();",
                "int existingRecipeIndex;",
                "if (TryFindRecipeReference(recipeData, out existingRecipeIndex))",
                "PromoteRuntimeRecipeOwnerIfUnownedOrSameMod(existingRecipeIndex);",
                "if (removedStaleOwnerRecipes)",
                "ModRegistryEvents.NotifyRecipeRegistryChanged();",
                "return true;",
                "if (_runtimeRecipes.Count >= MaxRuntimeRecipeCount)",
                "if (removedStaleOwnerRecipes)",
                "ModRegistryEvents.NotifyRecipeRegistryChanged();",
                "_runtimeRecipes.Add(new RuntimeRecipeRegistration",
                "Data = recipeData,",
                "ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,",
                "ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u",
                "ModRegistryEvents.NotifyRecipeRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "bool removed = false;",
                "for (int i = _runtimeRecipes.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_runtimeRecipes[i].ModId, modId, System.StringComparison.Ordinal))",
                "continue;",
                "_runtimeRecipes.RemoveAt(i);",
                "removed = true;",
                "if (removed)",
                "ModRegistryEvents.NotifyRecipeRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "RemoveStaleOwnerRecipes();",
                "ModRegistryEvents.NotifyRecipeRegistryChanged();"));
            Assert.IsTrue(ContainsTokensInOrder(
                removeStale,
                "for (int i = _runtimeRecipes.Count - 1; i >= 0; i--)",
                "if (IsRuntimeOwnerStillRegistered(_runtimeRecipes[i].ModHash))",
                "continue;",
                "_runtimeRecipes.RemoveAt(i);",
                "removed = true;"));
            StringAssert.Contains("return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);", ownerGuard);
            StringAssert.Contains("return _runtimeRecipes[index].Data;", getAt);
            StringAssert.Contains("return TryFindRecipeReference(recipeData, out unusedIndex);", contains);
            Assert.IsTrue(ContainsTokensInOrder(
                find,
                "index = -1;",
                "for (int i = 0; i < _runtimeRecipes.Count; i++)",
                "if (ReferenceEquals(_runtimeRecipes[i].Data, recipeData))",
                "index = i;",
                "return true;",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                promoteOwner,
                "if (!ModExecutionScope.HasActiveMod || (uint)index >= (uint)_runtimeRecipes.Count)",
                "return;",
                "RuntimeRecipeRegistration registration = _runtimeRecipes[index];",
                "if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)",
                "return;",
                "registration.ModId = ModExecutionScope.CurrentModId;",
                "registration.ModHash = ModExecutionScope.CurrentModHash;",
                "_runtimeRecipes[index] = registration;"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorEvent,
                "if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RecipeRegistryChanged)",
                "return;",
                "MarkRecipeCacheDirty();",
                "RebuildAssemblySourceCacheCold();",
                "EnsureRecipeCache();"));
        }

        [Test]
        public void ModRegistryEventConsumers_RetryListenerRegistrationAndOnlyUnregisterWhenRegistered()
        {
            string menu = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs");
            string fabricator = ReadProjectFile("Assets/_Project/Scripts/Fabricator.cs");
            string menuEnable = ExtractMethodBody(menu, "private void OnEnable()");
            string menuDisable = ExtractMethodBody(menu, "private void OnDisable()");
            string menuRefresh = ExtractMethodBody(menu, "public void RefreshView()");
            string menuTryRegister = ExtractMethodBody(menu, "private void TryRegisterModRegistryListener()");
            string fabricatorEnable = ExtractMethodBody(fabricator, "private void OnEnable()");
            string fabricatorDisable = ExtractMethodBody(fabricator, "private void OnDisable()");
            string fabricatorEnsureRecipeCache = ExtractMethodBody(fabricator, "private void EnsureRecipeCache()");
            string fabricatorTryRegister = ExtractMethodBody(fabricator, "private void TryRegisterModRegistryListener()");

            StringAssert.Contains("private bool _modRegistryEventRegistered;", menu);
            StringAssert.Contains("private bool _modRegistryEventRegistered;", fabricator);
            Assert.IsTrue(ContainsTokensInOrder(
                menuEnable,
                "TryRegisterModRegistryListener();",
                "RefreshView();"));
            Assert.IsTrue(ContainsTokensInOrder(
                menuDisable,
                "if (_modRegistryEventRegistered && _modRegistryEventAdapter != null)",
                "ModRegistryEvents.Unregister(_modRegistryEventAdapter);",
                "_modRegistryEventRegistered = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                menuRefresh,
                "TryRegisterModRegistryListener();",
                "ModLoader.CollectRuntimeInfo(_mods);",
                "ModSettingsRegistry.CollectSettings(_settings);"));
            Assert.IsTrue(ContainsTokensInOrder(
                menuTryRegister,
                "if (_modRegistryEventRegistered || !isActiveAndEnabled)",
                "return;",
                "_modRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorEnable,
                "TryRegisterModRegistryListener();",
                "RebuildInteractText();",
                "TryRegister();"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorDisable,
                "if (_modRegistryEventRegistered && _modRegistryEventAdapter != null)",
                "ModRegistryEvents.Unregister(_modRegistryEventAdapter);",
                "_modRegistryEventRegistered = false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorEnsureRecipeCache,
                "TryRegisterModRegistryListener();",
                "RefreshScanLogRevision();",
                "if (!_recipeCacheDirty)"));
            Assert.IsTrue(ContainsTokensInOrder(
                fabricatorTryRegister,
                "if (_modRegistryEventRegistered || !isActiveAndEnabled)",
                "return;",
                "_modRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());"));
        }

        [Test]
        public void ModEcosystemRegistry_RemovesDisabledOwnerBiomeMutationsBeforeFaunaGeneticsReads()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string faunaGenetics = ReadProjectFile("Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs");
            string register = ExtractMethodBody(source, "internal static bool TryRegister(FaunaBiomeMutationDefinition definition, out string error)");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterModBiomeMutations(string modId)");
            string getAt = ExtractMethodBody(source, "internal static FaunaBiomeMutationDefinition GetAt(int index)");
            string contains = ExtractMethodBody(source, "private static bool ContainsMatchingDefinition(FaunaBiomeMutationDefinition definition)");
            string find = ExtractMethodBody(source, "private static bool TryFindMatchingDefinition(FaunaBiomeMutationDefinition definition, out int index)");
            string promoteOwner = ExtractMethodBody(source, "private static void PromoteRuntimeMutationOwnerIfUnownedOrSameMod(int index)");
            string removeStale = ExtractMethodBody(source, "private static void RemoveStaleOwnerMutations()");
            string ownerGuard = ExtractMethodBody(source, "private static bool IsRuntimeOwnerStillRegistered(uint modHash)", 1);
            string applyOverlays = ExtractMethodBody(faunaGenetics, "private void ApplyMutationOverlays(");

            StringAssert.Contains("private const int MaxRuntimeMutationCount = 16;", source);
            StringAssert.Contains("private struct RuntimeBiomeMutationRegistration", source);
            StringAssert.Contains("private static readonly List<RuntimeBiomeMutationRegistration> _runtimeMutations", source);
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "RemoveStaleOwnerMutations();",
                "int existingMutationIndex;",
                "if (TryFindMatchingDefinition(definition, out existingMutationIndex))",
                "PromoteRuntimeMutationOwnerIfUnownedOrSameMod(existingMutationIndex);",
                "return true;",
                "if (_runtimeMutations.Count >= MaxRuntimeMutationCount)",
                "error = \"Runtime biome mutation registry capacity exceeded.\";",
                "_runtimeMutations.Add(new RuntimeBiomeMutationRegistration",
                "Data = CloneDefinition(definition),",
                "ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,",
                "ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u"));
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "for (int i = _runtimeMutations.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_runtimeMutations[i].ModId, modId, System.StringComparison.Ordinal))",
                "continue;",
                "_runtimeMutations.RemoveAt(i);"));
            StringAssert.Contains("return _runtimeMutations[index].Data;", getAt);
            Assert.IsTrue(ContainsTokensInOrder(
                removeStale,
                "for (int i = _runtimeMutations.Count - 1; i >= 0; i--)",
                "if (IsRuntimeOwnerStillRegistered(_runtimeMutations[i].ModHash))",
                "continue;",
                "_runtimeMutations.RemoveAt(i);"));
            StringAssert.Contains("return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);", ownerGuard);
            StringAssert.Contains("return TryFindMatchingDefinition(definition, out unusedIndex);", contains);
            Assert.IsTrue(ContainsTokensInOrder(
                find,
                "index = -1;",
                "for (int i = 0; i < _runtimeMutations.Count; i++)",
                "FaunaBiomeMutationDefinition existing = _runtimeMutations[i].Data;",
                "if (existing == null)",
                "continue;",
                "if (existing.BiomeId != definition.BiomeId)",
                "continue;",
                "index = i;",
                "return true;",
                "return false;"));
            Assert.IsTrue(ContainsTokensInOrder(
                promoteOwner,
                "if (!ModExecutionScope.HasActiveMod || (uint)index >= (uint)_runtimeMutations.Count)",
                "return;",
                "RuntimeBiomeMutationRegistration registration = _runtimeMutations[index];",
                "if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)",
                "return;",
                "registration.ModId = ModExecutionScope.CurrentModId;",
                "registration.ModHash = ModExecutionScope.CurrentModHash;",
                "_runtimeMutations[index] = registration;"));
            Assert.IsTrue(ContainsTokensInOrder(
                applyOverlays,
                "for (int i = 0; i < ModEcosystemRegistry.Count; i++)",
                "FaunaBiomeMutationDefinition definition = ModEcosystemRegistry.GetAt(i);",
                "if (definition == null || definition.BiomeId != biomeIndex)",
                "scale *= overlayScale;",
                "speed *= definition.SpeedMultiplier;",
                "health *= definition.HealthMultiplier;"));
        }

        [Test]
        public void ModBuildableRegistry_RegisterAndUnregisterHandlesPendingAndLiveStates()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string register = ExtractMethodBody(source, "internal static bool TryRegister(BuildableData buildableData, string customCategory, out string error)");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterModBuildables(string modId)");
            string flush = ExtractMethodBody(source, "internal static void FlushPendingRegistrations()", 2);

            StringAssert.Contains("public string ModId;", source);
            StringAssert.Contains("public uint ModHash;", source);
            StringAssert.Contains("private static readonly List<PendingBuildableRegistration> _liveBuildables", source);
            StringAssert.Contains("private static readonly List<ModuleCatalog> _liveModuleCatalogs", source);

            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "string normalizedCategory = NormalizeCategory(customCategory);",
                "string modId = ResolveActiveOwnerId();",
                "uint modHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u;",
                "catalog.TryRegisterRuntimeModule(buildableData, normalizedCategory, modId, out error);",
                "if (success)",
                "AddOrReplaceLiveBuildableRegistration(buildableData, normalizedCategory, modId, modHash);",
                "TrackLiveCatalog(catalog);",
                "ModRegistryEvents.NotifyBuildableRegistryChanged();",
                "int existingLiveBuildableIndex;",
                "if (TryFindLiveBuildable(buildableData, out existingLiveBuildableIndex))",
                "PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_liveBuildables, existingLiveBuildableIndex, customCategory);",
                "PromoteKnownModuleCatalogOwnersIfUnownedOrSameMod(buildableData, customCategory);",
                "return true;",
                "int existingPendingBuildableIndex;",
                "if (TryFindPendingBuildable(buildableData, out existingPendingBuildableIndex))",
                "PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(_pendingBuildables, existingPendingBuildableIndex, customCategory);",
                "return true;",
                "_pendingBuildables.Add(new PendingBuildableRegistration",
                "Data = buildableData,",
                "CustomCategory = NormalizeCategory(customCategory),",
                "ModId = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty,",
                "ModHash = ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModHash : 0u",
                "ModRegistryEvents.NotifyBuildableRegistryChanged();"));

            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "bool removed = false;",
                "if (RemoveLiveBuildableRegistrationsForMod(modId))",
                "removed = true;",
                "if (UnregisterRuntimeBuildablesFromKnownCatalogs(modId))",
                "removed = true;",
                "for (int i = _pendingBuildables.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_pendingBuildables[i].ModId, modId, System.StringComparison.Ordinal))",
                "continue;",
                "_pendingBuildables.RemoveAt(i);",
                "removed = true;",
                "ModuleCatalog catalog = ResolveActiveCatalog();",
                "if (catalog != null && !ContainsKnownLiveCatalog(catalog) && catalog.UnregisterRuntimeModulesForOwner(modId))",
                "removed = true;",
                "if (removed)",
                "ModRegistryEvents.NotifyBuildableRegistryChanged();"));

            Assert.IsTrue(ContainsTokensInOrder(
                flush,
                "PendingBuildableRegistration registration = _pendingBuildables[i];",
                "if (!IsPendingOwnerStillRegistered(registration.ModHash))",
                "_pendingBuildables.RemoveAt(i);",
                "changed = true;",
                "continue;",
                "catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, registration.ModId, out string error)",
                "AddOrReplaceLiveBuildableRegistration(registration.Data, registration.CustomCategory, registration.ModId, registration.ModHash);",
                "TrackLiveCatalog(catalog);"));
        }

        [Test]
        public void ModBuildableRegistry_GlobalServiceReplacementFlushesAndReplaysRegistrations()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string serviceReplaced = ExtractMethodBody(source, "internal static void OnGlobalRegistryServiceReplaced(", 1);
            string replay = ExtractMethodBody(source, "private static void ReplayLiveRegistrationsToActiveCatalog()", 1);
            string ownerGuard = ExtractMethodBody(source, "private static bool IsPendingOwnerStillRegistered(uint modHash)", 1);
            string activeOwner = ExtractMethodBody(source, "private static string ResolveActiveOwnerId()", 1);

            Assert.IsTrue(ContainsTokensInOrder(
                serviceReplaced,
                "if (serviceSlot != GlobalRegistryServiceSlot.Logistics)",
                "return;",
                "s_logisticsService = currentService as ILogisticsService;",
                "ReplayLiveRegistrationsToActiveCatalog();",
                "FlushPendingRegistrations();"));

            StringAssert.Contains("return modHash == 0u || ModCommandDispatcher.IsRegisteredMod(modHash);", ownerGuard);
            StringAssert.Contains("return ModExecutionScope.HasActiveMod ? ModExecutionScope.CurrentModId : string.Empty;", activeOwner);

            Assert.IsTrue(ContainsTokensInOrder(
                replay,
                "ModuleCatalog catalog = ResolveActiveCatalog();",
                "for (int i = _liveBuildables.Count - 1; i >= 0; i--)",
                "PendingBuildableRegistration registration = _liveBuildables[i];",
                "if (!IsPendingOwnerStillRegistered(registration.ModHash))",
                "_liveBuildables.RemoveAt(i);",
                "if (catalog.TryRegisterRuntimeModule(registration.Data, registration.CustomCategory, registration.ModId, out string error))",
                "TrackLiveCatalog(catalog);",
                "changed = true;",
                "Hecton8.Core.H8Debug.LogWarning(",
                "_liveBuildables.RemoveAt(i);",
                "if (changed)",
                "ModRegistryEvents.NotifyBuildableRegistryChanged();"));
        }

        [Test]
        public void ModBuildableRegistry_TracksAndPromotesLiveBuildableRegistrations()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string addLive = ExtractMethodBody(source, "private static void AddOrReplaceLiveBuildableRegistration(");
            string removeLive = ExtractMethodBody(source, "private static bool RemoveLiveBuildableRegistrationsForMod(string modId)");
            string containsLive = ExtractMethodBody(source, "private static bool ContainsLiveBuildable(BuildableData buildableData)");
            string findLive = ExtractMethodBody(source, "private static bool TryFindLiveBuildable(BuildableData buildableData, out int index)");
            string containsPending = ExtractMethodBody(source, "private static bool ContainsPendingBuildable(BuildableData buildableData)");
            string findPending = ExtractMethodBody(source, "private static bool TryFindPendingBuildable(BuildableData buildableData, out int index)");
            string promoteRegistrationOwner = ExtractMethodBody(source, "private static void PromoteBuildableRegistrationOwnerIfUnownedOrSameMod(");

            Assert.IsTrue(ContainsTokensInOrder(
                addLive,
                "for (int i = 0; i < _liveBuildables.Count; i++)",
                "BuildableData liveBuildable = registration.Data;",
                "string.Equals(liveBuildable.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal)",
                "registration.Data = buildableData;",
                "registration.CustomCategory = NormalizeCategory(customCategory);",
                "registration.ModId = modId;",
                "registration.ModHash = modHash;",
                "_liveBuildables[i] = registration;",
                "_liveBuildables.Add(new PendingBuildableRegistration"));

            Assert.IsTrue(ContainsTokensInOrder(
                removeLive,
                "for (int i = _liveBuildables.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_liveBuildables[i].ModId, modId, System.StringComparison.Ordinal))",
                "_liveBuildables.RemoveAt(i);",
                "removed = true;"));

            StringAssert.Contains("return TryFindLiveBuildable(buildableData, out unusedIndex);", containsLive);
            Assert.IsTrue(ContainsTokensInOrder(
                findLive,
                "index = -1;",
                "for (int i = 0; i < _liveBuildables.Count; i++)",
                "BuildableData live = _liveBuildables[i].Data;",
                "if (ReferenceEquals(live, buildableData))",
                "index = i;",
                "return true;",
                "string.Equals(live.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal)",
                "index = i;",
                "return true;",
                "return false;"));

            StringAssert.Contains("return TryFindPendingBuildable(buildableData, out unusedIndex);", containsPending);
            Assert.IsTrue(ContainsTokensInOrder(
                findPending,
                "index = -1;",
                "for (int i = 0; i < _pendingBuildables.Count; i++)",
                "PendingBuildableRegistration pending = _pendingBuildables[i];",
                "if (ReferenceEquals(pending.Data, buildableData))",
                "index = i;",
                "return true;",
                "string.Equals(pending.Data.PersistentId, buildableData.PersistentId, System.StringComparison.Ordinal)",
                "index = i;",
                "return true;",
                "return false;"));

            Assert.IsTrue(ContainsTokensInOrder(
                promoteRegistrationOwner,
                "if (!ModExecutionScope.HasActiveMod ||",
                "registrations == null ||",
                "(uint)index >= (uint)registrations.Count)",
                "return;",
                "PendingBuildableRegistration registration = registrations[index];",
                "if (registration.ModHash != 0u && registration.ModHash != ModExecutionScope.CurrentModHash)",
                "return;",
                "registration.CustomCategory = NormalizeCategory(customCategory);",
                "registration.ModId = ModExecutionScope.CurrentModId;",
                "registration.ModHash = ModExecutionScope.CurrentModHash;",
                "registrations[index] = registration;"));
        }

        [Test]
        public void ModBuildableRegistry_TracksAndPromotesKnownLiveCatalogs()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModRuntimeState.cs");
            string trackCatalog = ExtractMethodBody(source, "private static void TrackLiveCatalog(ModuleCatalog catalog)");
            string knownCatalog = ExtractMethodBody(source, "private static bool ContainsKnownLiveCatalog(ModuleCatalog catalog)");
            string unregisterKnownCatalogs = ExtractMethodBody(source, "private static bool UnregisterRuntimeBuildablesFromKnownCatalogs(string modId)");
            string promoteKnownCatalogOwners = ExtractMethodBody(source, "private static void PromoteKnownModuleCatalogOwnersIfUnownedOrSameMod(BuildableData buildableData, string customCategory)");

            Assert.IsTrue(ContainsTokensInOrder(
                trackCatalog,
                "for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)",
                "ModuleCatalog existing = _liveModuleCatalogs[i];",
                "_liveModuleCatalogs.RemoveAt(i);",
                "if (ReferenceEquals(existing, catalog))",
                "return;",
                "_liveModuleCatalogs.Add(catalog);"));

            Assert.IsTrue(ContainsTokensInOrder(
                knownCatalog,
                "for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)",
                "ModuleCatalog existing = _liveModuleCatalogs[i];",
                "_liveModuleCatalogs.RemoveAt(i);",
                "if (ReferenceEquals(existing, catalog))",
                "return true;",
                "return false;"));

            Assert.IsTrue(ContainsTokensInOrder(
                unregisterKnownCatalogs,
                "for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)",
                "ModuleCatalog catalog = _liveModuleCatalogs[i];",
                "_liveModuleCatalogs.RemoveAt(i);",
                "if (catalog.UnregisterRuntimeModulesForOwner(modId))",
                "removed = true;"));

            Assert.IsTrue(ContainsTokensInOrder(
                promoteKnownCatalogOwners,
                "if (!ModExecutionScope.HasActiveMod || buildableData == null)",
                "return;",
                "string modId = ModExecutionScope.CurrentModId;",
                "string normalizedCategory = NormalizeCategory(customCategory);",
                "for (int i = _liveModuleCatalogs.Count - 1; i >= 0; i--)",
                "ModuleCatalog catalog = _liveModuleCatalogs[i];",
                "_liveModuleCatalogs.RemoveAt(i);",
                "catalog.TryPromoteRuntimeModuleOwnerIfPresent(buildableData, normalizedCategory, modId);"));
        }

        [Test]
        public void ModBuildableRegistry_ModuleCatalogPromotesAndRecordsOwnership()
        {
            string moduleCatalog = ReadProjectFile("Assets/_Project/Scripts/ModuleCatalog.cs");
            string publicCatalogRegister = ExtractMethodBody(moduleCatalog, "public bool TryRegisterRuntimeModule(BuildableData data, string customCategory, out string error)");
            string ownedCatalogRegister = ExtractMethodBody(moduleCatalog, "internal bool TryRegisterRuntimeModule(BuildableData data, string customCategory, string ownerId, out string error)");
            string catalogUnregister = ExtractMethodBody(moduleCatalog, "internal bool UnregisterRuntimeModulesForOwner(string ownerId)");
            string catalogPromoteOwner = ExtractMethodBody(moduleCatalog, "internal bool TryPromoteRuntimeModuleOwnerIfPresent(BuildableData data, string customCategory, string ownerId)");
            string ownerRecorder = ExtractMethodBody(moduleCatalog, "private void RecordRuntimeModuleOwner(string persistentId, string ownerId)");
            string ownerPromoter = ExtractMethodBody(moduleCatalog, "private bool RecordRuntimeModuleOwnerIfUnownedOrSameOwner(string persistentId, string ownerId)");
            string moduleCatalogRebuild = ExtractMethodBody(moduleCatalog, "private void RebuildLookup()");

            StringAssert.Contains("private Dictionary<string, string> _runtimeModuleOwnerByPersistentId;", moduleCatalog);
            StringAssert.Contains("return TryRegisterRuntimeModule(data, customCategory, string.Empty, out error);", publicCatalogRegister);

            Assert.IsTrue(ContainsTokensInOrder(
                ownedCatalogRegister,
                "if (ContainsRuntimeModule(data))",
                "RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId);",
                "return true;",
                "if (HasAliasConflict(persistentId, data, out error))",
                "_runtimeModules.Add(data);",
                "_runtimeCategoryByPersistentId[persistentId] = NormalizeRuntimeCategory(customCategory);",
                "RecordRuntimeModuleOwner(persistentId, ownerId);",
                "AddLookupAlias(persistentId, data);",
                "_combinedModulesDirty = true;"));

            Assert.IsTrue(ContainsTokensInOrder(
                catalogUnregister,
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "for (int i = _runtimeModules.Count - 1; i >= 0; i--)",
                "string persistentId = NormalizeRuntimeModulePersistentId(data);",
                "_runtimeModuleOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner)",
                "_runtimeModuleOwnerByPersistentId.Remove(persistentId);",
                "_runtimeCategoryByPersistentId?.Remove(persistentId);",
                "_runtimeModules.RemoveAt(i);",
                "if (removed)",
                "RebuildLookup();"));

            Assert.IsTrue(ContainsTokensInOrder(
                ownerRecorder,
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "if (string.IsNullOrEmpty(ownerId))",
                "_runtimeModuleOwnerByPersistentId?.Remove(persistentId);",
                "return;",
                "_runtimeModuleOwnerByPersistentId[persistentId] = ownerId;"));

            Assert.IsTrue(ContainsTokensInOrder(
                catalogPromoteOwner,
                "string persistentId = NormalizeRuntimeModulePersistentId(data);",
                "if (string.IsNullOrEmpty(persistentId) || !ContainsRuntimeModule(data))",
                "return false;",
                "if (RecordRuntimeModuleOwnerIfUnownedOrSameOwner(persistentId, ownerId))",
                "_runtimeCategoryByPersistentId[persistentId] = NormalizeRuntimeCategory(customCategory);",
                "_combinedModulesDirty = true;",
                "return true;"));

            Assert.IsTrue(ContainsTokensInOrder(
                ownerPromoter,
                "persistentId = NormalizeRuntimeModulePersistentId(persistentId);",
                "ownerId = NormalizeRuntimeOwnerId(ownerId);",
                "if (string.IsNullOrEmpty(persistentId) || string.IsNullOrEmpty(ownerId))",
                "return false;",
                "_runtimeModuleOwnerByPersistentId.TryGetValue(persistentId, out string registeredOwner)",
                "!string.Equals(registeredOwner, ownerId, StringComparison.Ordinal)",
                "return false;",
                "RecordRuntimeModuleOwner(persistentId, ownerId);",
                "return true;"));

            Assert.IsTrue(ContainsTokensInOrder(
                moduleCatalogRebuild,
                "private void RebuildLookup()",
                "_combinedModulesDirty = true;",
                "AddHashAlias(runtimeModule.ModuleHashId, runtimeModule);"));
        }

        [Test]
        public void ModBuildableRegistry_PlayerBuilderAndConstructionTabRespectModuleCatalog()
        {
            string playerBuilder = ReadProjectFile("Assets/_Project/Scripts/PlayerBuilder.cs");
            string constructionTab = ReadProjectFile("Assets/_Project/Scripts/UI/PDAConstructionTab.cs");

            StringAssert.Contains("_buildCatalog.GetViewableCount(_cachedQuestSystem)", playerBuilder);
            StringAssert.Contains("BuildableData data = catalog.GetViewableAt(i, _cachedQuestSystem);", constructionTab);
        }

        [Test]
        public void ModSettingsRegistry_RemovesDisabledModSettingsAndNotifiesUi()
        {
            string settings = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs");
            string menu = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModMenuUIController.cs");
            string unregister = ExtractMethodBody(settings, "internal static void UnregisterModSettings(string modId)");
            string remove = ExtractMethodBody(settings, "private static void RemoveEntryAt(int index)");
            string collect = ExtractMethodBody(settings, "internal static void CollectSettings(List<ModSettingView> destination)");
            string handleUiEvent = ExtractMethodBody(menu, "private void HandleModRegistryEvent(in ModRegistryEventPayload payload)");

            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "for (int i = _entries.Count - 1; i >= 0; i--)",
                "if (!string.Equals(_entries[i].ModId, modId, StringComparison.Ordinal))",
                "continue;",
                "RemoveEntryAt(i);",
                "removed = true;",
                "if (removed)",
                "ModRegistryEvents.NotifySettingsRegistryChanged(modHash, 0u);"));
            StringAssert.DoesNotContain("InvokeToggleCallback", unregister);
            StringAssert.DoesNotContain("InvokeSliderCallback", unregister);
            Assert.IsTrue(ContainsTokensInOrder(
                remove,
                "SettingEntry removed = _entries[index];",
                "_entryIndexByHash.Remove(removed.KeyHash);",
                "int lastIndex = _entries.Count - 1;",
                "SettingEntry moved = _entries[lastIndex];",
                "_entries[index] = moved;",
                "_entryIndexByHash[moved.KeyHash] = index;",
                "_entries.RemoveAt(lastIndex);"));
            Assert.IsTrue(ContainsTokensInOrder(
                collect,
                "destination.Clear();",
                "for (int i = 0; i < _entries.Count; i++)",
                "SettingEntry entry = _entries[i];"));
            Assert.IsTrue(ContainsTokensInOrder(
                handleUiEvent,
                "eventType != ModRegistryEventType.RuntimeRegistryChanged",
                "eventType != ModRegistryEventType.SettingsRegistryChanged",
                "return;",
                "RefreshView();"));
        }

        [Test]
        public void ModSettingsRegistry_CallbackFailurePublishesTelemetryAndDisablesOwner()
        {
            string settings = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs");
            string toggle = ExtractMethodBody(settings, "private static void InvokeToggleCallback(");
            string slider = ExtractMethodBody(settings, "private static void InvokeSliderCallback(");
            string report = ExtractMethodBody(settings, "private static void ReportSettingCallbackException(");
            string publish = ExtractMethodBody(settings, "private static void PublishPerformanceWarningBestEffort(");

            StringAssert.Contains("ModSettingCallbackExceptionWarningHash = 0x4D534346u", settings);
            StringAssert.Contains("ModSettingCallbackExceptionDisableReason = \"Mod setting callback threw.\"", settings);
            Assert.IsTrue(ContainsTokensInOrder(
                toggle,
                "using (ModExecutionScope.Enter(modId, modHash))",
                "callback(value);",
                "catch (Exception exception)",
                "ReportSettingCallbackException(modId, modHash, exception);"));
            Assert.IsTrue(ContainsTokensInOrder(
                slider,
                "using (ModExecutionScope.Enter(modId, modHash))",
                "callback(value);",
                "catch (Exception exception)",
                "ReportSettingCallbackException(modId, modHash, exception);"));
            Assert.IsTrue(ContainsTokensInOrder(
                report,
                "PublishPerformanceWarningBestEffort(ModSettingCallbackExceptionWarningHash, modHash, 1f);",
                "Hecton8.Core.H8Debug.LogWarning",
                "ModLoader.DisableManagedMod(modId, ModSettingCallbackExceptionDisableReason);"));
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", publish);
            StringAssert.Contains("catch (Exception telemetryException)", publish);
            StringAssert.Contains("Hecton8.Core.H8Debug.LogWarning(\"[ModSettingsRegistry] telemetry failed: \" + telemetryException.Message);", publish);
        }

        [Test]
        public void ModSettingsRegistry_HydratesFromLateUserOptionsOwnerWithoutDroppingRuntimePending()
        {
            string settings = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModSettingsRegistry.cs");
            string cache = ExtractMethodBody(settings, "private static void CacheUserOptions(UserOptionsPersistence options)");
            string hydrate = ExtractMethodBody(settings, "private static void HydrateEntriesFromUserOptions(UserOptionsPersistence options)");
            string tryHydrate = ExtractMethodBody(settings, "private static bool TryHydrateEntryFromUserOptions(UserOptionsPersistence options, ref SettingEntry entry)");
            string save = ExtractMethodBody(settings, "private static bool TrySaveUserOptions(UserOptionsPersistence options, string storageKey)");

            Assert.IsTrue(ContainsTokensInOrder(
                cache,
                "s_userOptions = IsUserOptionsRuntimeUsable(options) ? options : null;",
                "if (s_userOptions == null)",
                "return;",
                "if (s_pendingFullStage)",
                "TrySaveUserOptions(s_userOptions, \"pending\");",
                "return;",
                "HydrateEntriesFromUserOptions(s_userOptions);"));
            Assert.IsTrue(ContainsTokensInOrder(
                hydrate,
                "int index = 0;",
                "while (index < _entries.Count)",
                "SettingEntry entry = _entries[index];",
                "TryHydrateEntryFromUserOptions(options, ref entry)",
                "_entries[index] = entry;",
                "InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, entry.BoolValue);",
                "InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, entry.FloatValue);",
                "ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);",
                "if (index < _entries.Count && _entries[index].KeyHash == entry.KeyHash)",
                "index++;"));
            Assert.IsTrue(ContainsTokensInOrder(
                tryHydrate,
                "bool storedValue = options.GetBool(entry.StorageKey, entry.DefaultBoolValue);",
                "if (entry.BoolValue == storedValue)",
                "return false;",
                "entry.BoolValue = storedValue;",
                "float storedValue = Mathf.Clamp(",
                "options.GetFloat(entry.StorageKey, entry.DefaultFloatValue)",
                "entry.MinValue",
                "entry.MaxValue",
                "if (Mathf.Approximately(entry.FloatValue, storedValue))",
                "return false;",
                "entry.FloatValue = storedValue;"));
            Assert.IsTrue(ContainsTokensInOrder(
                save,
                "if (s_pendingFullStage)",
                "StageAllEntries(options);",
                "if (options.TrySave())",
                "s_pendingFullStage = false;",
                "return true;",
                "s_pendingFullStage = true;"));
        }

        [Test]
        public void ModResourceRegistry_RemovesDisabledModResourcesAndReindexesMovedRecords()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs");
            string unregister = ExtractMethodBody(source, "internal static void UnregisterModResources(string modId)");
            string remove = ExtractMethodBody(source, "private static void RemoveRecordAt(int index)");
            string removeIndex = ExtractMethodBody(source, "private static void RemoveResourceIndex(in ResourceRecord record)");
            string addIndex = ExtractMethodBody(source, "private static void AddResourceIndex(in ResourceRecord record, int index)");
            string resolve = ExtractMethodBody(source, "private static bool TryResolve(uint hashId, ModResourceKind expectedKind, out ResourceRecord record)");

            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "for (int i = _recordCount - 1; i >= 0; i--)",
                "if (!string.Equals(_records[i].ModId, modId, StringComparison.Ordinal))",
                "continue;",
                "RemoveRecordAt(i);"));
            Assert.IsTrue(ContainsTokensInOrder(
                remove,
                "ResourceRecord removed = _records[index];",
                "RemoveResourceIndex(removed);",
                "int lastIndex = _recordCount - 1;",
                "ResourceRecord moved = _records[lastIndex];",
                "_records[index] = moved;",
                "RemoveResourceIndex(moved);",
                "AddResourceIndex(moved, index);",
                "_records[lastIndex] = default;",
                "_recordCount--;"));
            StringAssert.Contains("_resourceIndexByHash.Remove(hash);", removeIndex);
            StringAssert.Contains("_resourceIndexByHash.Add(hash, index);", addIndex);
            Assert.IsTrue(ContainsTokensInOrder(
                resolve,
                "if (hashId == 0u || !_resourceIndexByHash.IsCreated)",
                "return false;",
                "if (!_resourceIndexByHash.TryGetValue(hashId, out int index) ||",
                "(uint)index >= (uint)_recordCount)",
                "return false;"));
        }

        [Test]
        public void ModResourceRegistry_RegistrationOverflowPublishesTelemetry()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/IModResourceProxy.cs");
            string register = ExtractMethodBody(source, "internal static bool TryRegister(");
            string report = ExtractMethodBody(source, "private static void ReportResourceRegistrationOverflow(");
            string publish = ExtractMethodBody(source, "private static void PublishPerformanceWarningBestEffort(");
            string shutdown = ExtractMethodBody(source, "internal static void Shutdown()");

            StringAssert.Contains("ResourceRegistrationOverflowWarningHash = 0x4D525246u", source);
            StringAssert.Contains("ResourceRegistrationOverflowContextHash = 0x4D525251u", source);
            StringAssert.Contains("internal static int DroppedResourceRegistrationCount => _droppedResourceRegistrationCount;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "if (_recordCount >= ResourceCapacity)",
                "ReportResourceRegistrationOverflow(kind);",
                "return false;",
                "_records[_recordCount] = new ResourceRecord"));
            Assert.IsTrue(ContainsTokensInOrder(
                report,
                "_droppedResourceRegistrationCount++;",
                "int frame = ResolveCurrentFrameIndexSafe();",
                "if (_lastResourceRegistrationOverflowTelemetryFrame == frame)",
                "return;",
                "_lastResourceRegistrationOverflowTelemetryFrame = frame;",
                "PublishPerformanceWarningBestEffort(",
                "ResourceRegistrationOverflowWarningHash",
                "ResourceRegistrationOverflowContextHash ^ ((uint)kind << 24)",
                "_droppedResourceRegistrationCount);"));
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);", publish);
            StringAssert.Contains("catch (Exception exception)", publish);
            StringAssert.Contains("H8Debug.LogWarning(\"[ModResourceRegistry] telemetry failed: \" + exception.Message);", publish);
            StringAssert.Contains("_droppedResourceRegistrationCount = 0;", shutdown);
            StringAssert.Contains("_lastResourceRegistrationOverflowTelemetryFrame = -1;", shutdown);
        }

        [Test]
        public void ModLoader_DisablesCandidateWhenOnLoadFailsBeforeLoadedListOwnership()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModLoader.cs");
            string tryLoad = ExtractMethodBody(source, "private static void TryLoadCandidate(ModCandidate candidate)");

            Assert.IsTrue(ContainsTokensInOrder(
                tryLoad,
                "ModAssetManager.RegisterBundlePath(candidate.Metadata.Id, candidate.BundlePath);",
                "if (!ExecuteModCallback(loadedMod.Metadata.Id, loadedMod.Instance.OnLoad, \"OnLoad\"))",
                "ModCommandDispatcher.UnregisterMod(loadedMod.Metadata.Id);",
                "DisableCandidate(candidate, \"OnLoad failed.\");",
                "return;"));
            Assert.Less(
                tryLoad.IndexOf("DisableCandidate(candidate, \"OnLoad failed.\");", StringComparison.Ordinal),
                tryLoad.IndexOf("_loadedMods.Add(loadedMod);", StringComparison.Ordinal));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature, int occurrence = 0)
        {
            int signatureIndex = -1;
            int searchIndex = 0;
            for (int i = 0; i <= occurrence; i++)
            {
                signatureIndex = source.IndexOf(signature, searchIndex, StringComparison.Ordinal);
                Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
                searchIndex = signatureIndex + signature.Length;
            }

            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }
    }
}
