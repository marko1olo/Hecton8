using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModWorldPersistenceRuntimeOwnerEditTests
    {
        [Test]
        public void ModWorldPersistenceManager_RuntimeOwnerGateClearsStaleRegistryOwnerBeforeSaveAndBootstrapSubscriptions()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ModdingAPI", "ModWorldPersistenceManager.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsModWorldPersistenceRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            Assert.Less(
                awake.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                awake.IndexOf("InitializeService();", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("SceneManager.sceneLoaded += HandleSceneLoaded;", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("GameBootstrapper.Register(this);", StringComparison.Ordinal));
            Assert.Less(
                onEnable.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                onEnable.IndexOf("SaveEvents.Register(this);", StringComparison.Ordinal));
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", initialize);
            Assert.Less(
                initialize.IndexOf("if (TryAbortForUsableExistingRuntime())", StringComparison.Ordinal),
                initialize.IndexOf("GlobalRegistry.RegisterModWorldPersistenceRuntime(this);", StringComparison.Ordinal));
            StringAssert.Contains("ModWorldPersistenceManager registered = GlobalRegistry.ModWorldPersistence", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsModWorldPersistenceRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterModWorldPersistenceRuntime(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("!manager._serviceShuttingDown", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void ModWorldPersistenceManager_DelaysSaveRegistrationUntilSaveOwnerInitializedAndRetriesOnGameReady()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string gameReady = ExtractMethodBody(source, "private void HandleGameReady()");
            string register = ExtractMethodBody(source, "private void TryRegisterWithSaveManager()");
            string unregister = ExtractMethodBody(source, "private void UnregisterFromSaveManager()");
            string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string usable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                gameReady,
                "TryRegisterWithSaveManager();",
                "if (!_restorePending)",
                "RestoreActiveSceneRecords();"));
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", unregister);
            StringAssert.Contains("_registeredSaveService = null;", unregister);
            StringAssert.DoesNotContain("_saveService?.Unregister(this);", unregister);
            AssertTextBefore(hotSwap, "UnregisterFromSaveManager();", "_saveService = currentService as ISaveService;");
            StringAssert.DoesNotContain("previousService is ISaveService previousSave", hotSwap);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        [Test]
        public void ModWorldPersistenceManager_NormalizesSceneKeysBeforeHashingRestoreAndMarkerBinding()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ModdingAPI", "ModWorldPersistenceManager.cs"));
            string spawn = ExtractMethodBody(source, "internal GameObject SpawnPersistentPrefab(");
            string restoreActive = ExtractMethodBody(source, "private void RestoreActiveSceneRecords()");
            string restoreScene = ExtractMethodBody(source, "private void RestoreSceneRecords(string sceneName)");
            string ensureSpatial = ExtractMethodBody(source, "private static void EnsureSpatialFields(ref ModWorldSpawnRecord record)");
            string markerInitialize = ExtractMethodBody(source, "internal void Initialize(string spawnId, uint spawnHash, string modId, string assetName, string sceneName)");

            StringAssert.Contains("string sceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);", spawn);
            Assert.Less(
                spawn.IndexOf("SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name)", StringComparison.Ordinal),
                spawn.IndexOf("ModCommandDispatcher.ComputeModHash(sceneName)", StringComparison.Ordinal));
            StringAssert.Contains("SceneName = sceneName", spawn);

            StringAssert.Contains("string activeSceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);", restoreActive);
            Assert.Less(
                restoreActive.IndexOf("SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name)", StringComparison.Ordinal),
                restoreActive.IndexOf("RestoreSceneRecords(activeSceneName);", StringComparison.Ordinal));
            StringAssert.Contains("string activeSceneName = SaveMetadata.NormalizeSceneName(sceneName);", restoreScene);
            Assert.Less(
                restoreScene.IndexOf("SaveMetadata.NormalizeSceneName(sceneName)", StringComparison.Ordinal),
                restoreScene.IndexOf("ModCommandDispatcher.ComputeModHash(activeSceneName)", StringComparison.Ordinal));
            StringAssert.Contains("uint activeSceneHash = ModCommandDispatcher.ComputeModHash(activeSceneName);", restoreScene);
            StringAssert.Contains("if (!string.Equals(record.SceneName, activeSceneName, StringComparison.Ordinal))", restoreScene);

            StringAssert.Contains("record.SceneName = SaveMetadata.NormalizeSceneName(record.SceneName);", ensureSpatial);
            StringAssert.Contains("uint spawnHash = ModCommandDispatcher.ComputeModHash(record.SpawnId);", ensureSpatial);
            StringAssert.Contains("if (record.SpawnHash != spawnHash)", ensureSpatial);
            StringAssert.Contains("uint sceneHash = ModCommandDispatcher.ComputeModHash(record.SceneName);", ensureSpatial);
            StringAssert.Contains("if (record.SceneHash != sceneHash)", ensureSpatial);
            StringAssert.DoesNotContain("record.SpawnHash == 0u && !string.IsNullOrWhiteSpace(record.SpawnId)", ensureSpatial);
            StringAssert.DoesNotContain("record.SceneHash == 0u && !string.IsNullOrWhiteSpace(record.SceneName)", ensureSpatial);

            StringAssert.Contains("SceneName = SaveMetadata.NormalizeSceneName(sceneName);", markerInitialize);
        }

        [Test]
        public void ModWorldPersistenceManager_LoadFailureRollsBackPendingRecordApply()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");
            string onSaveEvent = ExtractMethodBody(source, "public void OnSaveEvent(in SaveEventPayload payload)");
            string capture = ExtractMethodBody(source, "private void CaptureLoadRollbackSnapshot()");
            string rollback = ExtractMethodBody(source, "private void RollbackLoadApplyIfPending()");
            string commit = ExtractMethodBody(source, "private void CommitLoadApply()");
            string rebuild = ExtractMethodBody(source, "private void RebuildLiveEntityLookupFromScene()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");

            StringAssert.Contains("_loadRollbackRecords", source);
            StringAssert.Contains("_loadApplyPending", source);
            Assert.Less(
                load.IndexOf("CaptureLoadRollbackSnapshot();", StringComparison.Ordinal),
                load.IndexOf("_records.Clear();", StringComparison.Ordinal));
            StringAssert.Contains("_restorePending = false;", load);
            Assert.IsTrue(ContainsTokensInOrder(
                load,
                "catch (Exception exception)",
                "Failed to parse mod world payload",
                "RollbackLoadApplyIfPending();",
                "return;"));

            StringAssert.Contains("case SaveEventType.LoadStarted:", onSaveEvent);
            StringAssert.Contains("case SaveEventType.LoadCompleted:", onSaveEvent);
            StringAssert.Contains("case SaveEventType.LoadFailed:", onSaveEvent);
            Assert.IsTrue(ContainsTokensInOrder(
                onSaveEvent,
                "case SaveEventType.LoadStarted:",
                "_restorePending = false;",
                "return;",
                "case SaveEventType.LoadCompleted:"));
            int firstRollbackAfterLoadStarted = onSaveEvent.IndexOf(
                "RollbackLoadApplyIfPending();",
                onSaveEvent.IndexOf("case SaveEventType.LoadStarted:", StringComparison.Ordinal),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(
                firstRollbackAfterLoadStarted,
                onSaveEvent.IndexOf("case SaveEventType.LoadFailed:", StringComparison.Ordinal));
            StringAssert.Contains("RollbackLoadApplyIfPending();", onSaveEvent);
            StringAssert.Contains("CommitLoadApply();", onSaveEvent);
            StringAssert.Contains("_restorePending = _records.Count > 0;", onSaveEvent);

            StringAssert.Contains("if (_loadApplyPending)", capture);
            StringAssert.Contains("_loadRollbackRecords.Add(_records[i]);", capture);
            StringAssert.Contains("_loadRollbackNextSpawnSequence = _nextSpawnSequence;", capture);
            StringAssert.Contains("_loadApplyPending = true;", capture);

            StringAssert.Contains("_records.Clear();", rollback);
            StringAssert.Contains("_recordIndexByHash.Clear();", rollback);
            StringAssert.Contains("AddOrReplaceRecord(_loadRollbackRecords[i]);", rollback);
            StringAssert.Contains("_nextSpawnSequence = Mathf.Max(1, _loadRollbackNextSpawnSequence);", rollback);
            StringAssert.Contains("_loadApplyPending = false;", rollback);
            StringAssert.Contains("_restorePending = false;", rollback);
            StringAssert.Contains("RebuildLiveEntityLookupFromScene();", rollback);

            StringAssert.Contains("_loadRollbackRecords.Clear();", commit);
            StringAssert.Contains("_loadApplyPending = false;", commit);
            StringAssert.Contains("UnityEngine.Object.FindObjectsByType<ModSpawnedEntity>", rebuild);
            StringAssert.Contains("_recordIndexByHash.ContainsKey(marker.SpawnHash)", rebuild);
            StringAssert.Contains("_liveEntitiesByHash[marker.SpawnHash] = marker;", rebuild);
            StringAssert.Contains("RollbackLoadApplyIfPending();", onDisable);
            StringAssert.Contains("_loadRollbackRecords.Clear();", shutdown);
            StringAssert.Contains("_loadApplyPending = false;", shutdown);
        }

        [Test]
        public void ModWorldPersistenceManager_RetainsRestorePendingUntilObjectPoolArrives()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string restore = ExtractMethodBody(source, "private void RestoreSceneRecords(string sceneName)");
            string cachePool = ExtractMethodBody(source, "private void CacheObjectPoolService(");
            string resolvePool = ExtractMethodBody(source, "private bool TryResolveCachedObjectPool(");

            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)", serviceReplaced);
            StringAssert.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);", serviceReplaced);
            StringAssert.Contains("_restorePending &&", serviceReplaced);
            StringAssert.Contains("TryResolveCachedObjectPool(out pool)", serviceReplaced);
            StringAssert.Contains("isActiveAndEnabled", serviceReplaced);
            StringAssert.Contains("!_serviceShuttingDown", serviceReplaced);
            StringAssert.Contains("RestoreActiveSceneRecords();", serviceReplaced);
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate)", cachePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)", cachePool);
            StringAssert.Contains("ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;", resolvePool);
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)", resolvePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)", resolvePool);
            StringAssert.Contains("_objectPoolService = null;", resolvePool);

            Assert.Less(
                restore.IndexOf("if (!TryResolveCachedObjectPool(out IObjectPoolService pool))", StringComparison.Ordinal),
                restore.IndexOf("bool restoreStillPending = false;", StringComparison.Ordinal));
            StringAssert.Contains("_restorePending = _records.Count > 0;", restore);
            StringAssert.Contains("return;", restore);
            StringAssert.Contains("pool.Spawn(prefab", restore);
            StringAssert.DoesNotContain("if (pool == null)\r\n                    continue;", restore);
            StringAssert.DoesNotContain("if (pool == null)\n                    continue;", restore);
        }

        [Test]
        public void ModWorldPersistenceManager_RetriesPendingSceneRestoreWhenModRegistryChanges()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string sceneLoaded = ExtractMethodBody(source, "private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)");
            string registryEvent = ExtractMethodBody(source, "public void OnModRegistryEvent(in ModRegistryEventPayload payload)");
            string restoreActive = ExtractMethodBody(source, "private void RestoreActiveSceneRecords()");
            string restoreScene = ExtractMethodBody(source, "private void RestoreSceneRecords(string sceneName)");
            string register = ExtractMethodBody(source, "private void TryRegisterModRegistryListener()");
            string unregister = ExtractMethodBody(source, "private void TryUnregisterModRegistryListener()");

            StringAssert.Contains("IModRegistryEventListener", source);
            StringAssert.Contains("TryRegisterModRegistryListener();", onEnable);
            StringAssert.Contains("TryUnregisterModRegistryListener();", onDisable);
            StringAssert.Contains("TryUnregisterModRegistryListener();", shutdown);
            StringAssert.Contains("_modRegistryListenerRegistered = ModRegistryEvents.Register(this);", register);
            StringAssert.DoesNotContain("_modRegistryListenerRegistered = true;", register);
            StringAssert.Contains("ModRegistryEvents.Unregister(this);", unregister);
            StringAssert.Contains("_modRegistryListenerRegistered = false;", unregister);

            StringAssert.Contains("_liveEntitiesByHash.Clear();", sceneLoaded);
            StringAssert.Contains("_restorePending = _records.Count > 0;", sceneLoaded);
            StringAssert.Contains("RestoreSceneRecords(SaveMetadata.NormalizeSceneName(scene.name));", sceneLoaded);

            StringAssert.Contains("ModRegistryEventType.RuntimeRegistryChanged", registryEvent);
            StringAssert.Contains("if (!_restorePending || _serviceShuttingDown || !isActiveAndEnabled)", registryEvent);
            StringAssert.Contains("RestoreActiveSceneRecords();", registryEvent);

            Assert.IsTrue(ContainsTokensInOrder(
                restoreActive,
                "TryRegisterModRegistryListener();",
                "string activeSceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);",
                "RestoreSceneRecords(activeSceneName);"));
            StringAssert.Contains("GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);", restoreScene);
            StringAssert.Contains("restoreStillPending = true;", restoreScene);
            StringAssert.Contains("_restorePending = restoreStillPending;", restoreScene);
        }

        [Test]
        public void ModWorldPersistenceManager_RemovesStaleLiveHandleBeforeRestoreSpawn()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/ModdingAPI/ModWorldPersistenceManager.cs");
            string restoreScene = ExtractMethodBody(source, "private void RestoreSceneRecords(string sceneName)");

            StringAssert.Contains("_liveEntitiesByHash.TryGetValue(record.SpawnHash, out ModSpawnedEntity liveMarker)", restoreScene);
            StringAssert.Contains("if (liveMarker != null)", restoreScene);
            StringAssert.Contains("_liveEntitiesByHash.Remove(record.SpawnHash);", restoreScene);
            Assert.IsTrue(ContainsTokensInOrder(
                restoreScene,
                "_liveEntitiesByHash.TryGetValue(record.SpawnHash, out ModSpawnedEntity liveMarker)",
                "if (liveMarker != null)",
                "continue;",
                "_liveEntitiesByHash.Remove(record.SpawnHash);",
                "GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);"));
            StringAssert.DoesNotContain("if (_liveEntitiesByHash.ContainsKey(record.SpawnHash))", restoreScene);
        }

        [Test]
        public void MacroDatabaseCompactionSwap_ValidatesTempAndPromotedFileDurability()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs");
            string create = ExtractMethodBody(source, "private bool TryCreateEmptyFileCold(string path, long initialSizeBytes, bool requireDatabaseExtension)");
            string swap = ExtractMethodBody(source, "public bool TryCompleteCompactionSwap(MacroDatabaseTier tier, bool persistenceBusy)");
            string dump = ExtractMethodBody(source, "public void DumpBlackBox(string path)");
            string compactionFlush = ExtractMethodBody(source, "private static bool TryFlushAndValidateCompactionDatabaseFile(string path, long expectedBytes)");
            string lengthFlush = ExtractMethodBody(source, "private static bool TryFlushAndValidateFileLength(string path, long expectedBytes)");

            StringAssert.DoesNotContain("using Hecton8.SaveSystem;", source);
            StringAssert.Contains("FileOptions.WriteThrough | FileOptions.RandomAccess", create);
            Assert.IsTrue(ContainsTokensInOrder(
                swap,
                "target.Flush();",
                "long promotedBytes = target._mappedBytes;",
                "target.Shutdown();",
                "TryFlushAndValidateCompactionDatabaseFile(_compactionTempPath, promotedBytes)",
                "CloseFileHandles();",
                "File.Replace(_compactionTempPath, activePath, null, true);",
                "TryFlushAndValidateCompactionDatabaseFile(activePath, promotedBytes)",
                "swapped = TryOpenExistingFile(activePath, true);"));

            StringAssert.Contains("expectedBytes >= MinimumFileBytes", compactionFlush);
            StringAssert.Contains("TryFlushAndValidateFileLength(path, expectedBytes)", compactionFlush);
            StringAssert.Contains("new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.WriteThrough)", lengthFlush);
            StringAssert.Contains("stream.Length != expectedBytes", lengthFlush);
            StringAssert.Contains("stream.Flush(true);", lengthFlush);
            StringAssert.Contains("return stream.Length == expectedBytes;", lengthFlush);
            StringAssert.Contains("catch", lengthFlush);

            StringAssert.Contains("FileOptions.WriteThrough", dump);
            StringAssert.Contains("string tempPath = path + \".tmp\";", dump);
            StringAssert.Contains("TryDeleteBlackBoxDumpTempFile(tempPath);", dump);
            StringAssert.Contains("long expectedBytes;", dump);
            StringAssert.Contains("expectedBytes = (long)entryBytes * blackBox.Length;", dump);
            StringAssert.Contains("new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough)", dump);
            StringAssert.Contains("stream.Flush(true);", dump);
            StringAssert.Contains("stream.Length != expectedBytes", dump);
            StringAssert.Contains("TryFlushAndValidateFileLength(tempPath, expectedBytes)", dump);
            StringAssert.Contains("File.Replace(tempPath, path, null, true);", dump);
            StringAssert.Contains("File.Move(tempPath, path);", dump);
            StringAssert.Contains("TryFlushAndValidateFileLength(path, expectedBytes)", dump);
            StringAssert.DoesNotContain("new FileStream(path, FileMode.Create", dump);
            Assert.IsTrue(ContainsTokensInOrder(
                dump,
                "string tempPath = path + \".tmp\";",
                "TryDeleteBlackBoxDumpTempFile(tempPath);",
                "new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough)",
                "stream.Flush(true);",
                "stream.Length != expectedBytes",
                "TryFlushAndValidateFileLength(tempPath, expectedBytes)",
                "File.Replace(tempPath, path, null, true);",
                "TryFlushAndValidateFileLength(path, expectedBytes)"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
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

        private static void AssertTextBefore(string text, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = text.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = text.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
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
