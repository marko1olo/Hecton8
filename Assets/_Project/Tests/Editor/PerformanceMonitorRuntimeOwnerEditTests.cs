using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PerformanceMonitorRuntimeOwnerEditTests
    {
        [Test]
        public void PerformanceMonitor_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeSamplingAndRouting()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "PerformanceMonitor.cs"));
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PerformanceMonitor ResolveActiveRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPerformanceMonitorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_frameStopwatch = new System.Diagnostics.Stopwatch();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_frameTimeHistory = new float[historyLength];");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterToDispatcher();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPerformanceMonitorRuntime(this);");
            StringAssert.Contains("if (_serviceRegistered)", register);
            StringAssert.Contains("s_currentRuntime = this;", register);

            StringAssert.Contains("PerformanceMonitor active = s_currentRuntime", gate);
            StringAssert.Contains("PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor", gate);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(active))", gate);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("s_currentRuntime = null", gate);
            StringAssert.Contains("s_currentRuntime = registered", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);", gate);

            StringAssert.Contains("PerformanceMonitor active = s_currentRuntime", resolver);
            StringAssert.Contains("PerformanceMonitor registered = GlobalRegistry.PerformanceMonitor", resolver);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(active))", resolver);
            StringAssert.Contains("if (IsPerformanceMonitorRuntimeUsable(registered))", resolver);
            StringAssert.Contains("s_currentRuntime = registered", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterPerformanceMonitorRuntime(registered);", resolver);
            StringAssert.Contains("return null;", resolver);

            StringAssert.Contains("monitor._serviceRegistered", usable);
            StringAssert.Contains("monitor.isActiveAndEnabled", usable);
            StringAssert.Contains("PerformanceMonitor runtime = ResolveActiveRuntime();", source);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
            StringAssert.DoesNotContain("PerformanceMonitor runtime = s_currentRuntime", source);
        }

        [Test]
        public void PerformanceEvents_NativeQueueTrackingUsesStoredSentinelIds()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "PerformanceMonitor.cs"));
            string ensureInitialized = ExtractMethodBody(source, "internal static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethodBody(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethodBody(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethodBody(source, "private static void ReleaseNativeQueue<T>");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref queue, ref sentinelId);", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.DoesNotContain("disposed = true;", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(PerformanceEvents)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
        }

        [Test]
        public void ObjectPoolDiagnostics_NativeQueueTrackingKeepsIdsWithQueueSwap()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "ObjectPoolDiagnostics.cs"));
            string ensureInitialized = ExtractMethodBody(source, "private static void EnsureInitialized()");
            string registerNativeQueue = ExtractMethodBody(source, "private static void RegisterNativeQueue<T>");
            string releaseNativeQueues = ExtractMethodBody(source, "private static void ReleaseNativeQueues()");
            string releaseNativeQueue = ExtractMethodBody(source, "private static void ReleaseNativeQueue<T>");
            string promote = ExtractMethodBody(source, "private static void PromoteNextFrameEventsIfFrontEmpty()");

            StringAssert.Contains("private static int _pendingEventsSentinelId;", source);
            StringAssert.Contains("private static int _nextFrameEventsSentinelId;", source);
            StringAssert.Contains("out _pendingEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out _nextFrameEventsSentinelId", ensureInitialized);
            StringAssert.Contains("out int sentinelId", registerNativeQueue);
            StringAssert.Contains("sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref queue, ref sentinelId);", registerNativeQueue);
            StringAssert.Contains("ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);", releaseNativeQueues);
            StringAssert.Contains("Exception firstException = null;", releaseNativeQueue);
            StringAssert.DoesNotContain("bool disposed = !", releaseNativeQueue);
            StringAssert.Contains("queue.Dispose();", releaseNativeQueue);
            StringAssert.DoesNotContain("disposed = true;", releaseNativeQueue);
            StringAssert.Contains("NativeMemorySentinel.Unregister(sentinelId);", releaseNativeQueue);
            StringAssert.Contains("catch (Exception exception)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed)", releaseNativeQueue);
            StringAssert.DoesNotContain("if (disposed &&", releaseNativeQueue);
            StringAssert.Contains("sentinelId = 0;", releaseNativeQueue);
            StringAssert.Contains("queue = default;", releaseNativeQueue);
            StringAssert.Contains("throw firstException;", releaseNativeQueue);
            StringAssert.Contains("int sentinelIdSwap = _pendingEventsSentinelId;", promote);
            StringAssert.Contains("_pendingEventsSentinelId = _nextFrameEventsSentinelId;", promote);
            StringAssert.Contains("_nextFrameEventsSentinelId = sentinelIdSwap;", promote);
            StringAssert.DoesNotContain("NativeMemorySentinel.RegisterNativeQueue(", source);
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(ObjectPoolDiagnostics)", source);
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("queue.Dispose();", StringComparison.Ordinal));
            Assert.Less(
                releaseNativeQueue.IndexOf("NativeMemorySentinel.Unregister(sentinelId);", StringComparison.Ordinal),
                releaseNativeQueue.IndexOf("sentinelId = 0;", StringComparison.Ordinal));
            Assert.Less(
                promote.IndexOf("_nextFrameEvents = swap;", StringComparison.Ordinal),
                promote.IndexOf("int sentinelIdSwap = _pendingEventsSentinelId;", StringComparison.Ordinal));
        }

        [Test]
        public void SaveManager_RuntimeOwnerGateClearsStaleSaveServiceBeforeNativePagerAndDispatcher()
        {
            string source = ReadScript(string.Empty, "SaveManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string duplicate = ExtractMethodBody(source, "private bool TryDeactivateDuplicateRuntimeOwner()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveRuntimeUsable(");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string onQuit = ExtractMethodBody(source, "private void OnApplicationQuit()");
            string serviceShutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string dispatcher = ExtractMethodBody(source, "private void TryRegisterDispatcherLanes()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string persistWfc = ExtractMethodBody(source, "public unsafe bool TryPersistWfcOutpostStateSnapshot(");
            string applyWfc = ExtractMethodBody(source, "public unsafe bool TryApplyWfcOutpostStateOverride(");
            string enqueuePage = ExtractMethodBody(source, "public bool TryEnqueueChunkPageWrite(");
            string requestPage = ExtractMethodBody(source, "public bool TryRequestChunkPageRead(");
            string copyPage = ExtractMethodBody(source, "public bool TryCopyCompletedChunkPage(");
            string retirePage = ExtractMethodBody(source, "public bool TryRetireCompletedChunkPage(");
            string flushPager = ExtractMethodBody(source, "public void FlushWorldPager()");
            string requestCompaction = ExtractMethodBody(source, "public bool TryRequestMacroDatabaseCompaction(");
            string completeCompaction = ExtractMethodBody(source, "public bool TryCompleteMacroDatabaseCompaction(");
            string ensurePager = ExtractMethodBody(source, "private H8BinaryWorldPager EnsureWorldPager()");
            string initializeNative = ExtractMethodBody(source, "private void InitializeNativeBuffers()");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string tryRequestSave = ExtractMethodBody(source, "public bool TryRequestSave(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string frostTick = ExtractMethodBody(source, "public void FrostTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string processRequest = ExtractMethodBody(source, "private void ProcessSaveRequest(");
            string registerSaveable = ExtractMethodBody(source, "public void Register(ISaveable saveable)");
            string unregisterSaveable = ExtractMethodBody(source, "public void Unregister(ISaveable saveable)");
            string saveGame = ExtractMethodBody(source, "private async Awaitable SaveGameAsyncInternal(");
            string loadGame = ExtractMethodBody(source, "public async Awaitable LoadGameAsync(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("public bool IsInitialized => _serviceRegistered && !_runtimeOwnerAborted;", source);
            StringAssert.Contains("public float CurrentPlayTimeSeconds => _runtimeOwnerAborted ? 0f : (float)ResolveCurrentPlayTimeSeconds();", source);
            AssertTextBefore(awake, "if (TryDeactivateDuplicateRuntimeOwner())", "InitializeNativeBuffers();");
            AssertTextBefore(awake, "if (TryDeactivateDuplicateRuntimeOwner())", "EnsureWorldPagerCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", duplicate);

            StringAssert.Contains("ISaveService registeredService = GlobalRegistry.Save;", gate);
            StringAssert.Contains("SaveManager registeredRuntime = GlobalRegistry.SaveRuntime;", gate);
            StringAssert.Contains("if (IsSaveRuntimeUsable(registeredService) || IsSaveRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSaveService(registeredService);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSaveService(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "UnregisterDispatcherLanes();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_isBusy = false;", abort);
            StringAssert.Contains("_worldPager?.Dispose();", abort);
            StringAssert.Contains("_nativeBuffers.Dispose();", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("SaveManager manager = service as SaveManager;", usable);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", usable);
            StringAssert.Contains("service is Behaviour behaviour", usable);
            StringAssert.Contains("return service.IsInitialized;", usable);

            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !_serviceRegistered || !Application.isPlaying)", "TryRegisterHotSwapListener();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(onQuit, "if (_runtimeOwnerAborted)", "FlushWorldPager();");
            AssertTextBefore(serviceShutdown, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || TryDeactivateDuplicateRuntimeOwner())", "InitializeNativeBuffers();");
            AssertTextBefore(initialize, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterAsyncPersistenceService(this);");
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.SaveRuntime, this);", initialize);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", initialize);
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "if (serviceSlot == GlobalRegistryServiceSlot.Save)");
            AssertTextBefore(dispatcher, "if (_runtimeOwnerAborted || !_serviceRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !_serviceRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");

            AssertTextBefore(persistWfc, "if (_runtimeOwnerAborted || !_serviceRegistered)", "EnsureWfcOutpostBlackBoxRing();");
            StringAssert.Contains("status = WfcOutpostPersistenceStatus.ServiceUnavailable;", persistWfc);
            AssertTextBefore(applyWfc, "if (_runtimeOwnerAborted || !_serviceRegistered)", "EnsureWfcOutpostBlackBoxRing();");
            StringAssert.Contains("status = WfcOutpostPersistenceStatus.ServiceUnavailable;", applyWfc);
            AssertTextBefore(enqueuePage, "if (_runtimeOwnerAborted || !_serviceRegistered)", "H8BinaryWorldPager pager = EnsureWorldPager();");
            AssertTextBefore(requestPage, "if (_runtimeOwnerAborted || !_serviceRegistered)", "H8BinaryWorldPager pager = EnsureWorldPager();");
            AssertTextBefore(copyPage, "if (_runtimeOwnerAborted || !_serviceRegistered)", "H8BinaryWorldPager pager = _worldPager;");
            AssertTextBefore(retirePage, "if (_runtimeOwnerAborted || !_serviceRegistered)", "H8BinaryWorldPager pager = _worldPager;");
            AssertTextBefore(flushPager, "if (_runtimeOwnerAborted || !_serviceRegistered)", "_worldPager?.Flush();");
            AssertTextBefore(requestCompaction, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (_isBusy)");
            AssertTextBefore(completeCompaction, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (_isBusy)");
            AssertTextBefore(ensurePager, "if (_runtimeOwnerAborted || !_serviceRegistered)", "EnsureWorldPagerCold();");
            AssertTextBefore(initializeNative, "if (_runtimeOwnerAborted)", "_nativeBuffers.EnsureInitial();");

            AssertTextBefore(tick, "if (_runtimeOwnerAborted || !_serviceRegistered)", "RecordWfcOutpostFrameBlackBox(");
            AssertTextBefore(tryRequestSave, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (slotIndex >= SaveEvents.ManualSlotCount)");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted || !_serviceRegistered)", "unchecked");
            AssertTextBefore(frostTick, "if (_runtimeOwnerAborted || !_serviceRegistered)", "MacroDatabaseTier tier = ResolveMacroDatabaseCompactionTier();");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted ||", "_compressionThrottleLateFrameArmed = false;");
            AssertTextBefore(processRequest, "if (_runtimeOwnerAborted || !_serviceRegistered)", "byte slotIndex = signal.SlotIndex;");
            AssertTextBefore(registerSaveable, "if (_runtimeOwnerAborted || !_serviceRegistered || !IsAlive(saveable)) return;", "for (int i = 0; i < _saveableCount; i++)");
            AssertTextBefore(unregisterSaveable, "if (_runtimeOwnerAborted || saveable == null) return;", "for (int i = 0; i < _saveableCount; i++)");
            AssertTextBefore(saveGame, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (!TryResolveSafeSlotName(slotName, out slotName))");
            AssertTextBefore(loadGame, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (!TryResolveSafeSlotName(slotName, out slotName))");

            StringAssert.DoesNotContain("GlobalRegistry.Save != null && !ReferenceEquals(GlobalRegistry.Save, this)", source);
            StringAssert.DoesNotContain("GlobalRegistry.SaveRuntime != null && !ReferenceEquals(GlobalRegistry.SaveRuntime, this)", source);
        }

        [Test]
        public void AudioLogSystem_RuntimeOwnerGateClearsStaleRuntimeBeforeVaultSaveTicksAndPlayback()
        {
            string source = ReadScript("AudioLog", "AudioLogSystem.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string discover = ExtractMethodBody(source, "public void DiscoverLog(");
            string play = ExtractMethodBody(source, "public void PlayLog(");
            string tryPlay = ExtractMethodBody(source, "public bool TryPlayLogByHash(");
            string recovered = ExtractMethodBody(source, "public uint GetRecoveredEncryptedBits(");
            string recover = ExtractMethodBody(source, "public bool RecoverEncryptedFragment(");
            string notifyWarningStarted = ExtractMethodBody(source, "public void NotifyAtmosphericWarningStarted(");
            string notifyWarningCompleted = ExtractMethodBody(source, "public void NotifyAtmosphericWarningCompleted()");
            string stop = ExtractMethodBody(source, "public void StopPlayback()");
            string clearTransientPlaybackState = ExtractMethodBody(source, "private void ClearTransientPlaybackState()");
            string isDiscoveredString = ExtractMethodBody(source, "public bool IsDiscovered(string logId)");
            string isDiscoveredHash = ExtractMethodBody(source, "public bool IsDiscovered(uint logHash)");
            string playHash = ExtractMethodBody(source, "private void PlayLogByHash(");
            string partialPreview = ExtractMethodBody(source, "private void PlayEncryptedPartialPreview(");
            string playbackSync = ExtractMethodBody(source, "private bool QueuePlaybackVisualSync(");
            string flushPlaybackSync = ExtractMethodBody(source, "private void FlushPendingPlaybackVisualSync()");
            string beginGlitch = ExtractMethodBody(source, "private void BeginNarrativeRadioGlitch(");
            string refreshGlitch = ExtractMethodBody(source, "private void RefreshActiveNarrativeRadioGlitchVisualSync()");
            string queueGlitchReset = ExtractMethodBody(source, "private void QueueNarrativeRadioGlitchReset()");
            string flushGlitchReset = ExtractMethodBody(source, "private void FlushPendingNarrativeRadioGlitchReset()");
            string registerTick = ExtractMethodBody(source, "private void TryRegister()");
            string registerLate = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string cacheAudio = ExtractMethodBody(source, "private void CacheAudioService(");
            string resolveAudio = ExtractMethodBody(source, "private IAudioService ResolveAudioService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string usable = ExtractMethodBody(source, "private static bool IsAudioLogSystemUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string unregister = ExtractMethodBody(source, "private void TryUnregisterService()");
            string rebindVault = ExtractMethodBody(source, "private void RebindDataVaultCold(");
            string ensureVault = ExtractMethodBody(source, "private void EnsureVaultBuffersCold()");
            string readVault = ExtractMethodBody(source, "private bool TryReadVaultBuffer<T>(");
            string acquireVault = ExtractMethodBody(source, "private bool TryAcquireVaultMutation<T>(");
            string readEncrypted = ExtractMethodBody(source, "private bool TryReadEncryptedFragmentState(");
            string clearEncrypted = ExtractMethodBody(source, "private void ClearEncryptedFragmentState()");
            string acquireEncrypted = ExtractMethodBody(source, "private bool TryAcquireEncryptedFragmentMutationView(");
            string recordTelemetry = ExtractMethodBody(source, "private void RecordVaultTelemetry(");
            string releaseVault = ExtractMethodBody(source, "private void ReleaseVaultBuffers(");
            string tryPushDiscoveryNotification = ExtractMethodBody(source, "private void TryPushDiscoveryNotification(");
            string reportDiscoveryNotificationMiss = ExtractMethodBody(source, "private void ReportDiscoveryNotificationMiss(");
            string clearDiscoveryNotificationDiagnostics = ExtractMethodBody(source, "private void ClearDiscoveryNotificationDiagnostics()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string recoverFacade = ExtractMethodBody(source, "public bool RecoverEncryptedAudioLogFragment(");
            string loadEncrypted = ExtractMethodBody(source, "private void LoadEncryptedFragmentState(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("public bool IsPlaying => !_runtimeOwnerAborted && _isPlaying;", source);
            StringAssert.Contains("public bool IsNarrativeQueueBlocked => !_runtimeOwnerAborted &&", source);
            StringAssert.Contains("public AudioLogData CurrentLog => _runtimeOwnerAborted ? null : _currentLog;", source);
            StringAssert.Contains("public int DiscoveredCount => _runtimeOwnerAborted ? 0 : _discoveredLogHashes.Count;", source);
            StringAssert.Contains("public bool CurrentPlaybackBitCrushed => !_runtimeOwnerAborted && _currentPlaybackBitCrushed;", source);

            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "EnsureVaultBuffersCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterSaveParticipant();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ReleaseVaultBuffers(_dataVault);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "bool queuedPlaybackStarted");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "FlushPendingPlaybackVisualSync();");

            AssertTextBefore(discover, "if (_runtimeOwnerAborted)", "if (data == null");
            AssertTextBefore(play, "if (_runtimeOwnerAborted)", "if (data == null");
            AssertTextBefore(tryPlay, "if (_runtimeOwnerAborted)", "if (logHash == 0u)");
            AssertTextBefore(recovered, "if (_runtimeOwnerAborted)", "if (logHash == 0u)");
            AssertTextBefore(recover, "if (_runtimeOwnerAborted)", "if (logHash == 0u");
            AssertTextBefore(notifyWarningStarted, "if (_runtimeOwnerAborted)", "_atmosphericWarningActive = true;");
            AssertTextBefore(notifyWarningCompleted, "if (_runtimeOwnerAborted)", "if (!_atmosphericWarningActive)");
            AssertTextBefore(stop, "if (_runtimeOwnerAborted)", "AudioLogData stoppedLog = _currentLog;");
            AssertTextBefore(isDiscoveredString, "if (_runtimeOwnerAborted)", "return IsDiscovered(ComputeAudioLogHash(logId));");
            AssertTextBefore(isDiscoveredHash, "if (_runtimeOwnerAborted)", "return logHash != 0u");
            AssertTextBefore(playHash, "if (_runtimeOwnerAborted)", "TrackResolvedLogHash(logHash);");
            AssertTextBefore(partialPreview, "if (_runtimeOwnerAborted)", "TrackResolvedLogHash(logHash);");
            AssertTextBefore(playbackSync, "if (_runtimeOwnerAborted)", "if (clip == null)");
            AssertTextBefore(flushPlaybackSync, "if (_runtimeOwnerAborted)", "if (!_pendingPlaybackDirty)");
            AssertTextBefore(beginGlitch, "if (_runtimeOwnerAborted)", "if (!_audioGlitchParametersLayoutValid)");
            AssertTextBefore(refreshGlitch, "if (_runtimeOwnerAborted)", "if (!_isPlaying)");
            AssertTextBefore(queueGlitchReset, "if (_runtimeOwnerAborted)", "_currentPlaybackGlitch = default;");
            AssertTextBefore(flushGlitchReset, "if (_runtimeOwnerAborted)", "if (!_pendingGlitchResetDirty)");

            AssertTextBefore(registerTick, "if (_runtimeOwnerAborted || !_serviceRegistered", "GlobalRegistry.TryRegisterSlowTickable");
            AssertTextBefore(registerLate, "if (_runtimeOwnerAborted || !_serviceRegistered", "GlobalRegistry.TryRegisterLateFrameTickable");
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "CacheAudioService(GlobalRegistry.Audio);");
            AssertTextBefore(cacheAudio, "if (_runtimeOwnerAborted)", "if (!IsAudioServiceUsable(audioService))");
            AssertTextBefore(resolveAudio, "if (_runtimeOwnerAborted)", "IAudioService audioService = _cachedAudioService;");
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_cachedSaveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_cachedSaveService", "_saveRegistered");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || !_serviceRegistered", "GlobalRegistry.TryRegisterHotSwapListener(this);");

            AssertTextBefore(register, "if (_runtimeOwnerAborted)", "if (_serviceRegistered)");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs;");
            AssertTextBefore(register, "if (IsAudioLogSystemUsable(registeredAudioLogs))", "GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);");
            AssertTextBefore(register, "GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", "GlobalRegistry.RegisterAudioLogRuntime(this);");
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.AudioLogs, this);", register);
            StringAssert.Contains("return true;", register);

            StringAssert.Contains("audioLogSystem._serviceRegistered", usable);
            StringAssert.Contains("audioLogSystem.isActiveAndEnabled", usable);
            StringAssert.Contains("!audioLogSystem._runtimeOwnerAborted", usable);

            StringAssert.Contains("AudioLogSystem registeredAudioLogs = GlobalRegistry.AudioLogs;", gate);
            StringAssert.Contains("if (!Application.isPlaying)", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredAudioLogs, this))", gate);
            StringAssert.Contains("if (IsAudioLogSystemUsable(registeredAudioLogs))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioLogRuntime(registeredAudioLogs);", gate);

            AssertTextBefore(abort, "TryUnregisterSaveParticipant();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterService();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("ClearTransientPlaybackState();", abort);
            StringAssert.Contains("ReleaseVaultBuffers(_dataVault);", abort);
            StringAssert.Contains("_dataVault = null;", abort);
            StringAssert.Contains("_cachedAudioService = null;", abort);
            StringAssert.Contains("_cachedSaveService = null;", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);
            StringAssert.Contains("_isPlaying = false;", clearTransientPlaybackState);
            StringAssert.Contains("_currentLog = null;", clearTransientPlaybackState);
            StringAssert.Contains("_currentLogHash = 0u;", clearTransientPlaybackState);
            StringAssert.Contains("ClearPendingPlaybackSync();", clearTransientPlaybackState);
            StringAssert.Contains("ClearPlaybackQueue();", clearTransientPlaybackState);
            StringAssert.Contains("ClearAtmosphericWarningBlocker();", clearTransientPlaybackState);

            AssertTextBefore(unregister, "if (_runtimeOwnerAborted || !_serviceRegistered)", "GlobalRegistry.UnregisterAudioLogRuntime(this);");
            AssertTextBefore(rebindVault, "if (_runtimeOwnerAborted)", "ReleaseVaultBuffers(_dataVault);");
            AssertTextBefore(ensureVault, "if (_runtimeOwnerAborted || !_serviceRegistered)", "IDataVault vault = _dataVault;");
            AssertTextBefore(readVault, "if (_runtimeOwnerAborted)", "IDataVault vault = _dataVault;");
            AssertTextBefore(acquireVault, "if (_runtimeOwnerAborted)", "if (guardVault == null");
            StringAssert.Contains("guardVault = null;", acquireVault);
            AssertTextBefore(readEncrypted, "if (_runtimeOwnerAborted)", "TryReadVaultBuffer");
            AssertTextBefore(clearEncrypted, "if (_runtimeOwnerAborted)", "int count = _encryptedFragmentStateCount;");
            AssertTextBefore(acquireEncrypted, "if (_runtimeOwnerAborted)", "if (guardVault == null");
            StringAssert.Contains("guardVault = null;", acquireEncrypted);
            AssertTextBefore(recordTelemetry, "if (_runtimeOwnerAborted)", "IDataVault vault = _dataVault;");
            StringAssert.Contains("if (notificationHash == 0u)", tryPushDiscoveryNotification);
            StringAssert.Contains("ReportDiscoveryNotificationMiss(logHash);", tryPushDiscoveryNotification);
            AssertTextBefore(tryPushDiscoveryNotification, "ReportDiscoveryNotificationMiss(logHash);", "return;");
            StringAssert.Contains("_discoveryNotificationMissCount++", reportDiscoveryNotificationMiss);
            StringAssert.Contains("_DiscoveryNotificationMissWarningHash", reportDiscoveryNotificationMiss);
            StringAssert.Contains("_discoveryNotificationMissCount = 0;", clearDiscoveryNotificationDiagnostics);
            StringAssert.Contains("ClearDiscoveryNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearDiscoveryNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearDiscoveryNotificationDiagnostics();", releaseVault);
            StringAssert.DoesNotContain("_discoveryNotificationMissCount", populate);
            AssertTextBefore(populate, "if (_runtimeOwnerAborted || (Application.isPlaying && !_serviceRegistered))", "data.DiscoveredAudioLogHashes.Clear();");
            AssertTextBefore(load, "ClearDiscoveryNotificationDiagnostics();", "_discoveredLogHashes.Clear();");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || (Application.isPlaying && !_serviceRegistered))", "ClearTransientPlaybackState();");
            AssertTextBefore(load, "ClearTransientPlaybackState();", "_discoveredLogHashes.Clear();");
            AssertTextBefore(recoverFacade, "if (_runtimeOwnerAborted)", "return RecoverEncryptedFragment(logHash, fragmentHash);");
            AssertTextBefore(loadEncrypted, "if (_runtimeOwnerAborted)", "ClearEncryptedFragmentState();");
        }

        [Test]
        public void DynamicResolutionScaler_RuntimeOwnerGateClearsStaleRegistryBeforeRenderScaleAndRouting()
        {
            string source = ReadScript("World", "DynamicResolutionScaler.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsDynamicResolutionRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_runtimeRenderScaleQueueActive = Application.isPlaying;");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_urpAsset = UniversalRenderPipeline.asset;");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "ApplyRenderScale();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "TryRegisterSaveParticipant();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterDynamicResolutionRuntime(this);");

            AssertRuntimeGate(gate, "DynamicResolutionScaler registered = GlobalRegistry.DynamicResolution", "DynamicResolutionScaler active = s_activeRuntime", "IsDynamicResolutionRuntimeUsable", "GlobalRegistry.UnregisterDynamicResolutionRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterDynamicResolutionRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntime = registered", gate);
            StringAssert.Contains("s_activeRuntime = active", gate);
            StringAssert.Contains("scaler._serviceRegistered", usable);
            StringAssert.Contains("scaler.isActiveAndEnabled", usable);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", cache);
            StringAssert.DoesNotContain("if (_saveService == null)", cache);
            StringAssert.Contains("if (!IsSaveServiceUsable(saveService))", saveRegister);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_saveRegistered = true;"));
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            AssertTextBefore(saveRegister, "if (!IsSaveServiceUsable(saveService))", "saveService.Register(this);");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService == null)", saveRegister);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void CullingManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeCullRouting()
        {
            string source = ReadScript("World", "CullingManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsCullingRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "H8Debug.Log(\"[CullingManager] Initialized.");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "ApplyLayerCullDistances();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CachePlayerRuntimeContext(GlobalRegistry.Player);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterLateFrame();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterCullingRuntime(this);");

            AssertRuntimeGate(gate, "CullingManager active = s_activeRuntimeInstance", "CullingManager registered = GlobalRegistry.Culling", "IsCullingRuntimeUsable", "GlobalRegistry.UnregisterCullingRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterCullingRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void LodSystemManager_RuntimeOwnerGateClearsStaleRegistryBeforeLodCachesAndRouting()
        {
            string source = ReadScript("World", "LODSystemManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsLodSystemRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureDistanceScratchAllocated();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "ApplyQualityPreset(_qualityPreset);");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "TryRegisterSaveParticipant();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureDistanceScratchAllocated();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterLODSystemRuntime(this);");

            AssertRuntimeGate(gate, "LODSystemManager registered = GlobalRegistry.LODSystem", "LODSystemManager active = s_activeRuntime", "IsLodSystemRuntimeUsable", "GlobalRegistry.UnregisterLODSystemRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterLODSystemRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntime = registered", gate);
            StringAssert.Contains("s_activeRuntime = active", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_saveRegistered = true;"));
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService == null)", saveRegister);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void HectonBiolumController_RuntimeOwnerGateClearsStaleRegistryBeforeEventsAndRouting()
        {
            string source = ReadScript("World", "HectonBiolumController.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntime()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsBiolumControllerRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CachePlayerRuntimeContext(GlobalRegistry.Player, null);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "if (!TryRegisterRuntime())");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EclipseGameplayEvents.Register(this);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheLocalProxyLightBaselines();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterBiolumControllerRuntime(this);");

            StringAssert.Contains("HectonBiolumController registered = GlobalRegistry.BiolumController", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (IsBiolumControllerRuntimeUsable(registered))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterBiolumControllerRuntime(registered);", gate);
            StringAssert.Contains("controller._runtimeRegistered", usable);
            StringAssert.Contains("controller.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void ImpostorSystem_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeAtlasResourcesAndRouting()
        {
            string source = ReadScript("World", "ImpostorSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsImpostorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "PrewarmAtlasDrawResourcesCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterLateFrame();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterImpostorRuntime(this);");

            AssertRuntimeGate(gate, "ImpostorSystem active = s_activeRuntimeInstance", "ImpostorSystem registered = GlobalRegistry.Impostors", "IsImpostorRuntimeUsable", "GlobalRegistry.UnregisterImpostorRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterImpostorRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void SargassumCutManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeResourcesAndRouting()
        {
            string source = ReadScript("World", "SargassumCutManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSargassumCutRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "InitializeRuntimeResourceBudgets(force: true);");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CreateResources();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "PublishGlobals();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CreateResources();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSargassumCutRuntime(this);");

            AssertRuntimeGate(
                gate,
                "SargassumCutManager active = s_activeRuntimeInstance",
                "SargassumCutManager registered = GlobalRegistry.SargassumCut",
                "IsSargassumCutRuntimeUsable",
                "GlobalRegistry.UnregisterSargassumCutRuntime",
                "Destroy(this);");
            StringAssert.Contains("GlobalRegistry.RegisterSargassumCutRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("Destroy(this);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void SargassumGlobalDragManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeResourcesAndRouting()
        {
            string source = ReadScript("World", "SargassumGlobalDragManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveOwner()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveOwner()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSargassumDragRuntimeUsable(");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheDataVaultCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CreateDensityTexture();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureScavengerRenderResources();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "PublishShaderGlobals();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "RefreshRenderLayerCache();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureScavengerRenderResources();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "HectonFloatingOrigin.RegisterListener(this);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSargassumDragRuntime(this);");

            AssertRuntimeGate(
                gate,
                "SargassumGlobalDragManager active = s_activeRuntimeInstance",
                "SargassumGlobalDragManager registered = GlobalRegistry.SargassumDrag",
                "IsSargassumDragRuntimeUsable",
                "GlobalRegistry.UnregisterSargassumDragRuntime",
                "Destroy(this);");
            StringAssert.Contains("GlobalRegistry.RegisterSargassumDragRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("Destroy(this);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", saveUnregister);
            StringAssert.Contains("_registeredSaveService = null;", saveUnregister);
            StringAssert.DoesNotContain("_saveService?.Unregister(this);", saveUnregister);
            AssertTextBefore(replaced, "TryUnregisterSaveOwner();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveOwner();");
            StringAssert.DoesNotContain("if (saveService == null)", saveRegister);
            StringAssert.DoesNotContain("if (_saveRegistered && previousService is ISaveService previousSave)", source);
            StringAssert.DoesNotContain("registered != null && registered != this", source);
        }

        [Test]
        public void AbyssalThermalManager_RuntimeOwnerGateClearsStaleMirrorBeforeBuffersEventsAndRouting()
        {
            string source = ReadScript("World", "AbyssalThermalManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegister()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsAbyssalThermalRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureBuffers();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "PrepareThermalMapResourcesCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "LaserCutterEvents.Register(this);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "HectonFloatingOrigin.RegisterListener(this);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureBuffers();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterThermodynamicsRuntime(this);");

            StringAssert.Contains("AbyssalThermalManager active = s_activeRuntimeInstance", gate);
            StringAssert.Contains("if (IsAbyssalThermalRuntimeUsable(active))", gate);
            StringAssert.Contains("Destroy(this);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterThermodynamicsRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("manager._registeredThermodynamicsRuntime", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registeredThermodynamics != null && registeredThermodynamics != this", source);
        }

        [Test]
        public void SpectrumSystem_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeVisorGlobalsAndRouting()
        {
            string source = ReadScript("Visor", "SpectrumSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSpectrumRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "SonarGridOverlay.ApplyGlobals(");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "SubscribeAcousticPingEvents();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureAupDiscoveryGrid();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "ApplyShaderMode();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSpectrumRuntime(this);");

            AssertRuntimeGate(gate, "SpectrumSystem active = s_activeRuntimeInstance", "SpectrumSystem registered = GlobalRegistry.Spectrum", "IsSpectrumRuntimeUsable", "GlobalRegistry.UnregisterSpectrumRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterSpectrumRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("enabled = false;", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("activeRuntime != null && activeRuntime != this", source);
        }

        [Test]
        public void HectonNarrativeDirector_RuntimeOwnerGateClearsStaleRegistryBeforeEventsSaveAndRouting()
        {
            string source = ReadScript(string.Empty, "HectonNarrativeDirector.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private void TryRegister()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsNarrativeDirectorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryReadModelsCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "InitializeAupNarrativePoiVaultStorage();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegister();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "_saveService = GlobalRegistry.Save;");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "NarrativeEvents.Register(this);");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "HectonFloatingOrigin.RegisterListener(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterNarrativeDirectorRuntime(this);");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSpatialTriggerSystem(this);");

            StringAssert.Contains("HectonNarrativeDirector active = GlobalRegistry.NarrativeDirector", gate);
            StringAssert.Contains("if (IsNarrativeDirectorRuntimeUsable(active))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterNarrativeDirectorRuntime(active);", gate);
            StringAssert.Contains("director._registeredNarrativeRuntime", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.DoesNotContain("activeRuntime != null && activeRuntime != this", source);
        }

        [Test]
        public void ConnectionSplineBatchRenderer_RuntimeOwnerGateClearsStaleRegistryAndStaticServiceBeforeBatchResources()
        {
            string source = ReadScript("Core", "ConnectionSplineBatchRenderer.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string initializeService = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string candidate = ExtractMethodBody(source, "private bool TryAbortForRuntimeCandidate(");
            string usable = ExtractMethodBody(source, "private static bool IsConnectionSplineRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "s_activeRuntimeInstance = this;");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "InitializeBatch((int)BatchKind.PipesNear");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", initializeService);
            AssertTextBefore(initializeService, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterConnectionSplineBatchRendererRuntime(this);");
            AssertTextBefore(initializeService, "if (TryAbortForUsableExistingRuntime())", "EnsureRuntimeRegistrations();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "EnsureRuntimeRegistrations();");

            StringAssert.Contains("TryAbortForRuntimeCandidate(GlobalRegistry.ConnectionSplineBatchRenderer)", gate);
            StringAssert.Contains("TryAbortForRuntimeCandidate(s_activeRuntimeInstance)", gate);
            StringAssert.Contains("TryAbortForRuntimeCandidate(s_activeService as ConnectionSplineBatchRenderer)", gate);
            StringAssert.Contains("if (IsConnectionSplineRuntimeUsable(active))", candidate);
            StringAssert.Contains("Destroy(gameObject);", candidate);
            StringAssert.Contains("GlobalRegistry.UnregisterConnectionSplineBatchRendererRuntime(active);", candidate);
            StringAssert.Contains("s_activeService = null", candidate);
            StringAssert.Contains("s_activeRuntimeInstance = null", candidate);
            StringAssert.Contains("renderer._serviceRegistered", usable);
            StringAssert.Contains("renderer.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("activeRuntime != null && activeRuntime != this", source);
        }

        [Test]
        public void PDAIntrusionManager_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeInputEventsAndRouting()
        {
            string source = ReadScript("UI", "PDAIntrusionManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPDAIntrusionRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "ResolveRuntimeOwners();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "BindInputActionOwnerCold();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "RebuildTextDriftTargetsCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "RegisterToTickManager();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "DirectorAIEvents.Register(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "ResolveRuntimeOwners();");
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "RegisterToTickManager();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPDAIntrusionRuntime(this);");

            AssertRuntimeGate(gate, "PDAIntrusionManager active = s_activeRuntimeInstance", "PDAIntrusionManager registered = GlobalRegistry.PDAIntrusion", "IsPDAIntrusionRuntimeUsable", "GlobalRegistry.UnregisterPDAIntrusionRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterPDAIntrusionRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("enabled = false;", gate);
            StringAssert.Contains("Destroy(this);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("activeRuntime != null && activeRuntime != this", source);
        }

        [Test]
        public void ModalWindow_RuntimeOwnerGateClearsStaleModalServiceBeforeBindingsAndSubscriptions()
        {
            string source = ReadScript(string.Empty, "ModalWindow.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string claim = ExtractMethodBody(source, "private bool TryClaimInstance()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsModalWindowRuntimeUsable(");
            string release = ExtractMethodBody(source, "private void ReleaseServiceIfOwner()");

            StringAssert.Contains("if (!TryClaimInstance())", awake);
            AssertTextBefore(awake, "if (!TryClaimInstance())", "EnsureRuntimeBindings(hideAfterBinding: true);");
            StringAssert.Contains("if (!TryClaimInstance())", onEnable);
            AssertTextBefore(onEnable, "if (!TryClaimInstance())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryClaimInstance())", "LocalizationEvents.RegisterLanguageListener(this);");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", claim);
            AssertTextBefore(claim, "if (TryAbortForUsableExistingRuntime())", "Hecton8.Core.GlobalRegistry.RegisterModalWindowService(this);");
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.ModalWindow, this);", claim);

            StringAssert.Contains("Hecton8.Core.IModalWindowService existing = Hecton8.Core.GlobalRegistry.ModalWindow", gate);
            StringAssert.Contains("ModalWindow active = existing as ModalWindow", gate);
            StringAssert.Contains("if (IsModalWindowRuntimeUsable(active))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterModalWindowService(existing);", gate);
            StringAssert.Contains("window._serviceRegistered", usable);
            StringAssert.Contains("window.isActiveAndEnabled", usable);
            StringAssert.Contains("_serviceRegistered = false;", release);
            StringAssert.DoesNotContain("Duplicate detected", source);
        }

        [Test]
        public void PDALogbookManager_RuntimeOwnerGateClearsStaleServiceBeforeSaveSignalsAndTickRouting()
        {
            string source = ReadScript("PDA", "PDALogbookManager.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterLogbookService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPDALogbookRuntimeUsable(");
            string unregister = ExtractMethodBody(source, "private void UnregisterLogbookService()");

            StringAssert.Contains("TryRegisterLogbookService();", onEnable);
            AssertTextBefore(onEnable, "TryRegisterLogbookService();", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!enabled)", "TryRegisterWithSaveManager();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "TryRegisterWithSaveManager();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPDALogbookService(this);");
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.PDALogbook, this);", register);

            StringAssert.Contains("IPDALogbookService registered = GlobalRegistry.PDALogbook", gate);
            StringAssert.Contains("PDALogbookManager active = registered as PDALogbookManager", gate);
            StringAssert.Contains("if (IsPDALogbookRuntimeUsable(active))", gate);
            StringAssert.Contains("enabled = false;", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPDALogbookService(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("_serviceRegistered = false;", unregister);
            StringAssert.DoesNotContain("Duplicate logbook service detected", source);
        }

        [Test]
        public void PdaAndProgressionSaveableOwners_DelaySaveRegistrationUntilSaveOwnerInitialized()
        {
            string achievementRegistry = ReadScript("Progression", "PlayerAchievementRegistry.cs");
            string contextualAdvisory = ReadScript("Progression", "PDAContextualAdvisorySystem.cs");

            AssertInitializedSaveOwnerRegistrationGate(
                ReadScript("PDA", "PDALogbookManager.cs"),
                "saveService.Register(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                ReadScript("PDA", "PlayerExplorationTracker.cs"),
                "saveService.Register(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                ReadScript("PDA", "PDAMarkerRegistry.cs"),
                "saveService.Register(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                achievementRegistry,
                "saveService.Register(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                contextualAdvisory,
                "saveService.Register(this);");

            string achievementCache = ExtractMethodBody(achievementRegistry, "private void ResolveOwnersCold()");
            string advisoryCache = ExtractMethodBody(contextualAdvisory, "private bool CacheOwnersCold()");
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", achievementCache);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", advisoryCache);
            StringAssert.DoesNotContain("if (_saveService == null)", achievementCache);
            StringAssert.DoesNotContain("if (_saveService == null)", advisoryCache);
        }

        [Test]
        public void PlayerAchievementNotificationRefusalClearsDiagnosticsOnLifecycleAndDoesNotPersist()
        {
            string source = ReadScript("Progression", "PlayerAchievementRegistry.cs");
            string push = ExtractMethodBody(source, "private void TryPushAchievementNotification(");
            string report = ExtractMethodBody(source, "private void ReportAchievementNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearAchievementNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("public int AchievementNotificationMissCount => _achievementNotificationMissCount", source);
            StringAssert.Contains("if (NotificationEvents.TryPushRegisteredInfo(notificationHash))", push);
            AssertTextBefore(push, "NotificationEvents.TryPushRegisteredInfo(notificationHash)", "ReportAchievementNotificationMiss(achievementHash);");
            StringAssert.Contains("_achievementNotificationMissCount++;", report);
            StringAssert.Contains("AchievementNotificationMissWarningHash", report);
            StringAssert.Contains("_achievementNotificationMissCount = 0;", clear);
            StringAssert.Contains("_lastAchievementNotificationMissTelemetryFrame = 0;", clear);
            StringAssert.Contains("ClearAchievementNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearAchievementNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearAchievementNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_achievementNotificationMissCount", populate);
            StringAssert.DoesNotContain("_achievementNotificationMissCount", load);
        }

        [Test]
        public void PdaContextualAdvisoryFallbackNotificationRefusalUsesSameMissTelemetry()
        {
            string source = ReadScript("Progression", "PDAContextualAdvisorySystem.cs");
            string pushAdvisory = ExtractMethodBody(source, "private void PushAdvisory(uint advisoryHash, string id, string message)");
            string pushSpan = ExtractMethodBody(source, "private void PushAdvisorySpan(");
            string report = ExtractMethodBody(source, "private void ReportAdvisoryNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearAdvisoryNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("if (!TryPushRegisteredAdvisoryNotification(advisoryHash))", pushAdvisory);
            StringAssert.Contains("PushAdvisorySpan(advisoryHash, localizedMessage);", pushAdvisory);
            StringAssert.DoesNotContain("PushAdvisorySpan(localizedMessage);", pushAdvisory);
            StringAssert.Contains("if (messageHash != 0u && NotificationEvents.TryPushRegisteredWarning(messageHash))", pushSpan);
            StringAssert.Contains("return;", pushSpan);
            StringAssert.Contains("ReportAdvisoryNotificationMiss(advisoryHash);", pushSpan);
            AssertTextBefore(pushSpan, "NotificationEvents.TryPushRegisteredWarning(messageHash)", "ReportAdvisoryNotificationMiss(advisoryHash);");
            StringAssert.Contains("_advisoryNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_advisoryNotificationMissCount = 0;", clear);
            StringAssert.Contains("_lastAdvisoryNotificationMissTelemetryFrame = 0;", clear);
            StringAssert.Contains("ClearAdvisoryNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearAdvisoryNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearAdvisoryNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_advisoryNotificationMissCount", populate);
            StringAssert.DoesNotContain("_advisoryNotificationMissCount", load);
        }

        [Test]
        public void PersistentWorldRegistry_RuntimeOwnerGateClearsStaleMirrorAndRegistryBeforeVaultStorageAndLoops()
        {
            string source = ReadScript("World", "PersistentWorldRegistry.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private void TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPersistentWorldRuntimeUsable(");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string cachePool = ExtractMethodBody(source, "private void CacheObjectPoolService(");
            string resolvePool = ExtractMethodBody(source, "private bool TryResolveCachedObjectPool(");
            string prefabPoolsReady = ExtractMethodBody(source, "public bool AreResidentWorldPrefabPoolsReady()");
            string hydrate = ExtractMethodBody(source, "private bool HydrateRecord(");
            string dehydrate = ExtractMethodBody(source, "private void DehydrateRecord(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(awake, "if (!_serviceRegistered)", "InitializeVaultBackedStorage(_dataVault, maxTrackedItems);");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "RegisterNativeMemorySentinelAllocations();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (!_serviceRegistered)", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!_serviceRegistered)", "TryRegisterRuntimeLoops();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", start);
            AssertTextBefore(start, "if (TryAbortForUsableExistingRuntime())", "TryRegisterRuntimeLoops();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPersistentWorldRegistry(this);");
            StringAssert.Contains("PersistentWorldRegistry registered = GlobalRegistry.PersistentWorldRegistry", register);
            StringAssert.Contains("s_activeRuntimeInstance = this", register);

            AssertRuntimeGate(gate, "PersistentWorldRegistry active = s_activeRuntimeInstance", "PersistentWorldRegistry registered = GlobalRegistry.PersistentWorldRegistry", "IsPersistentWorldRuntimeUsable", "GlobalRegistry.UnregisterPersistentWorldRegistry");
            StringAssert.Contains("GlobalRegistry.RegisterPersistentWorldRegistry(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("registry._serviceRegistered", usable);
            StringAssert.Contains("registry.isActiveAndEnabled", usable);
            StringAssert.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);", serviceReplaced);
            StringAssert.Contains("CacheObjectPoolService(null);", cache);
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate)", cachePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)", cachePool);
            StringAssert.Contains("ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;", resolvePool);
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)", resolvePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)", resolvePool);
            StringAssert.Contains("_objectPoolService = null;", resolvePool);
            StringAssert.Contains("if (!TryResolveCachedObjectPool(out IObjectPoolService pool))", prefabPoolsReady);
            StringAssert.Contains("if (!TryResolveCachedObjectPool(out IObjectPoolService pool))", hydrate);
            StringAssert.Contains("TryResolveCachedObjectPool(out IObjectPoolService pool);", dehydrate);
            StringAssert.DoesNotContain("IObjectPoolService pool = _objectPoolService;", prefabPoolsReady);
            StringAssert.DoesNotContain("IObjectPoolService pool = _objectPoolService;", hydrate);
            StringAssert.DoesNotContain("registeredRegistry != null && registeredRegistry != this", source);
            StringAssert.DoesNotContain("Duplicate registry owner detected", source);
        }

        [Test]
        public void WorldSaveTimeConsumers_RequireInitializedSaveServiceBeforePlaytimeReads()
        {
            string floraRegrowth = ReadScript("World", "FloraRegrowthDirector.cs");
            string floraRegrowthTime = ExtractMethodBody(floraRegrowth, "private float GetCurrentPlayTimeSeconds()");
            string floraRegrowthSaveUsable = ExtractMethodBody(floraRegrowth, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                floraRegrowthTime,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "return IsSaveServiceUsable(saveService)",
                "? saveService.CurrentPlayTimeSeconds",
                ": (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", floraRegrowthSaveUsable);
            StringAssert.DoesNotContain("_saveService != null", floraRegrowthTime);

            string floraInteraction = ReadScript("World", "FloraInteractionManager.cs");
            string floraInteractionCache = ExtractMethodBody(floraInteraction, "private void CacheEnvironmentRuntimeServicesCold()");
            string floraSimulationTime = ExtractMethodBody(floraInteraction, "private float GetCurrentSimulationTimeSeconds()");
            string floraInteractionSaveUsable = ExtractMethodBody(floraInteraction, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                floraSimulationTime,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (IsSaveServiceUsable(saveService))",
                "return Mathf.Max(0f, saveService.CurrentPlayTimeSeconds);",
                "SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;"));
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", floraInteractionCache);
            StringAssert.DoesNotContain("if (_saveService == null)", floraInteractionCache);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", floraInteractionSaveUsable);
            StringAssert.DoesNotContain("if (saveService != null)", floraSimulationTime);

            string persistentWorld = ReadScript("World", "PersistentWorldRegistry.cs");
            string tombstoneDay = ExtractMethodBody(persistentWorld, "private int ResolveTombstoneDayIndex()");
            string persistentWorldSaveUsable = ExtractMethodBody(persistentWorld, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                tombstoneDay,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "double playSeconds = IsSaveServiceUsable(saveService)",
                "? saveService.CurrentPlayTimeSeconds",
                ": Time.timeAsDouble;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", persistentWorldSaveUsable);
            StringAssert.DoesNotContain("saveService != null", tombstoneDay);
        }

        [Test]
        public void WorldProceduralStateRegistry_SaveBridgeRequiresInitializedOwnerBeforeRegistrationAndPlaytime()
        {
            string source = ReadScript(string.Empty, "WorldProceduralStateRegistry.cs");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string register = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string playTime = ExtractMethodBody(source, "private float GetCurrentPlayTimeSeconds()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            AssertTextBefore(replaced, "TryUnregisterSaveParticipant();", "_saveService = currentService as ISaveService;");
            AssertTextBefore(replaced, "_saveService = currentService as ISaveService;", "TryRegisterSaveParticipant();");
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_saveRegistered = true;"));
            Assert.IsTrue(ContainsTokensInOrder(
                playTime,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "return IsSaveServiceUsable(saveService)",
                "? saveService.CurrentPlayTimeSeconds",
                ": (float)SystemDispatcher.CurrentUnscaledTimeSeconds;"));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (_saveService == null)", register);
            StringAssert.DoesNotContain("if (saveService != null)", playTime);
            StringAssert.DoesNotContain("_saveService.Register(this)", source);
        }

        [Test]
        public void SoundscapeSystem_RuntimeOwnerGateClearsStaleRegistryBeforeAudioMusicAndTickRouting()
        {
            string source = ReadScript("World", "SoundscapeSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string slowRegister = ExtractMethodBody(source, "private void TryRegister()");
            string lateRegister = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsSoundscapeRuntimeUsable(");
            string unregister = ExtractMethodBody(source, "private void TryUnregisterService()");
            string biomeRegister = ExtractMethodBody(source, "private void TryRegisterBiomeMatrixEvents()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string drain = ExtractMethodBody(source, "private void DrainSignals()");
            string queueShader = ExtractMethodBody(source, "private void QueueSoundscapeShaderTier(");
            string resolveSurvival = ExtractMethodBody(source, "private bool ResolveSurvivalSystem()");
            string matrixBiome = ExtractMethodBody(source, "void IBiomeMatrixEventListener.OnMatrixBiomeChanged(");
            string matrixDepth = ExtractMethodBody(source, "void IBiomeMatrixEventListener.OnDepthTierChanged(");
            string rebound = ExtractMethodBody(source, "void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(");
            string replaced = ExtractMethodBody(source, "void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(");
            string resolveMusic = ExtractMethodBody(source, "private bool TryResolveMusicDirector(");
            string syncMusic = ExtractMethodBody(source, "private void SyncMusicDirectorSoundscapeContext(");
            string syncCachedMusic = ExtractMethodBody(source, "private void SyncCachedMusicDirectorSoundscapeContext(");
            string cacheAudio = ExtractMethodBody(source, "private void CacheAudioService(");
            string resolveAudio = ExtractMethodBody(source, "private IAudioService ResolveAudioService()");
            string cacheMusic = ExtractMethodBody(source, "private void CacheMusicDirector(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("public SoundscapeTier CurrentTier => _runtimeOwnerAborted ? SoundscapeTier.Surface : _currentTier;", source);
            StringAssert.Contains("byte ISoundscapeTierReadModel.CurrentTierCode => _runtimeOwnerAborted ? (byte)SoundscapeTier.Surface : (byte)_currentTier;", source);
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheAudioService(GlobalRegistry.Audio);");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheMusicDirector(GlobalRegistry.MusicDirector);");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterBiomeMatrixEvents();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "QueueSoundscapeShaderTier(_currentTier);");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");

            AssertTextBefore(register, "if (_runtimeOwnerAborted)", "if (_serviceRegistered)");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSoundscapeRuntime(this);");
            StringAssert.Contains("SoundscapeSystem registered = GlobalRegistry.Soundscape", register);
            StringAssert.Contains("s_activeRuntimeInstance = this", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("return true;", register);
            StringAssert.Contains("return false;", register);
            StringAssert.Contains("_runtimeOwnerAborted || _registered || !_serviceRegistered", slowRegister);
            StringAssert.Contains("_runtimeOwnerAborted || _registeredLateFrame || !_serviceRegistered", lateRegister);

            StringAssert.Contains("if (_runtimeOwnerAborted)", gate);
            StringAssert.Contains("SoundscapeSystem registered = GlobalRegistry.Soundscape", gate);
            StringAssert.Contains("SoundscapeSystem active = s_activeRuntimeInstance", gate);
            StringAssert.Contains("if (IsSoundscapeRuntimeUsable(registered))", gate);
            StringAssert.Contains("if (IsSoundscapeRuntimeUsable(active))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSoundscapeRuntime(registered);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSoundscapeRuntime(active);", gate);
            StringAssert.Contains("GlobalRegistry.RegisterSoundscapeRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = active", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);

            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterService();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_audioService = null;", abort);
            StringAssert.Contains("_musicDirector = null;", abort);
            StringAssert.Contains("_soundscapeTierShaderDirty = false;", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("!system._runtimeOwnerAborted", usable);
            AssertTextBefore(unregister, "if (_runtimeOwnerAborted || !_serviceRegistered)", "GlobalRegistry.UnregisterSoundscapeRuntime(this);");
            StringAssert.Contains("_runtimeOwnerAborted || !_serviceRegistered || _biomeMatrixRegistered", biomeRegister);
            StringAssert.Contains("_runtimeOwnerAborted || !_serviceRegistered || _hotSwapRegistered", hotSwap);
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted || !_serviceRegistered)", "DrainSignals();");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (!_soundscapeTierShaderDirty)");
            AssertTextBefore(drain, "if (_runtimeOwnerAborted || !_serviceRegistered)", "IAudioService audio = ResolveAudioService();");
            AssertTextBefore(queueShader, "if (_runtimeOwnerAborted || !_serviceRegistered)", "_pendingShaderTier = tier;");
            AssertTextBefore(resolveSurvival, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (survivalSystem != null)");
            AssertTextBefore(matrixBiome, "if (_runtimeOwnerAborted || !_serviceRegistered)", "int matrixBiomeId = profile != null");
            AssertTextBefore(matrixDepth, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (!TryResolveMusicDirector");
            AssertTextBefore(rebound, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (serviceSlot == GlobalRegistryServiceSlot.Audio)");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (serviceSlot == GlobalRegistryServiceSlot.Audio)");
            AssertTextBefore(resolveMusic, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (_musicDirector != null");
            AssertTextBefore(syncMusic, "if (_runtimeOwnerAborted || !_serviceRegistered)", "if (!TryResolveMusicDirector");
            AssertTextBefore(syncCachedMusic, "if (_runtimeOwnerAborted || !_serviceRegistered)", "HectonMusicDirector director = _musicDirector;");
            AssertTextBefore(cacheAudio, "if (_runtimeOwnerAborted || !_serviceRegistered)", "_audioService = IsAudioServiceUsable(audioService)");
            AssertTextBefore(resolveAudio, "if (_runtimeOwnerAborted || !_serviceRegistered)", "IAudioService audioService = _audioService;");
            AssertTextBefore(cacheMusic, "if (_runtimeOwnerAborted || !_serviceRegistered)", "_musicDirector = musicDirector != null");
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [TestCase(
            "CameraRTManager.cs",
            "CameraRTManager registered = GlobalRegistry.CameraRT",
            "GlobalRegistry.RegisterCameraRTRuntime(this);",
            "GlobalRegistry.UnregisterCameraRTRuntime(registered);",
            "private static bool IsCameraRTRuntimeUsable(",
            "manager._serviceRegistered")]
        [TestCase(
            "VisorRTManager.cs",
            "VisorRTManager registered = GlobalRegistry.VisorRT",
            "GlobalRegistry.RegisterVisorRTRuntime(this);",
            "GlobalRegistry.UnregisterVisorRTRuntime(registered);",
            "private static bool IsVisorRTRuntimeUsable(",
            "manager._serviceRegistered")]
        [TestCase(
            "UIRTManager.cs",
            "UIRTManager registered = GlobalRegistry.UIRT",
            "GlobalRegistry.RegisterUIRTRuntime(this);",
            "GlobalRegistry.UnregisterUIRTRuntime(registered);",
            "private static bool IsUIRTRuntimeUsable(",
            "manager._serviceRegistered")]
        [TestCase(
            "PostFXRTManager.cs",
            "PostFXRTManager registered = GlobalRegistry.PostFXRT",
            "GlobalRegistry.RegisterPostFXRTRuntime(this);",
            "GlobalRegistry.UnregisterPostFXRTRuntime(registered);",
            "private static bool IsPostFXRTRuntimeUsable(",
            "manager._serviceRegistered")]
        public void RenderTextureBudgetManagers_RuntimeOwnerGateClearsStaleRegistryBeforeBudgetRouting(
            string fileName,
            string ownerRead,
            string registerCall,
            string unregisterCall,
            string usableSignature,
            string serviceField)
        {
            string source = ReadScript("Optimization", fileName);
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, usableSignature);

            AssertTextBefore(onEnable, "if (TryRegisterService())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (TryRegisterService())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", registerCall);

            StringAssert.Contains("if (!Application.isPlaying)", gate);
            StringAssert.Contains(ownerRead, gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("ReferenceEquals(registered, this)", gate);
            StringAssert.Contains("if (Is", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains(unregisterCall, gate);
            StringAssert.Contains(serviceField, usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void RenderTextureLifecycleTracker_RuntimeOwnerGateClearsStaleRegistryBeforeLeakTickRouting()
        {
            string source = ReadScript("Optimization", "RenderTextureLifecycleTracker.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRenderTextureLifecycleRuntimeUsable(");

            AssertTextBefore(onEnable, "if (TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (TryRegisterService())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterRenderTextureLifecycleRuntime(this);");
            StringAssert.Contains("RenderTextureLifecycleTracker registered = GlobalRegistry.RenderTextureLifecycle", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRenderTextureLifecycleRuntime(registered);", gate);
            StringAssert.Contains("tracker._registeredService", usable);
            StringAssert.Contains("tracker.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void RenderTexturePool_RuntimeOwnerGateClaimsBeforePrewarmAndSceneHooks()
        {
            string source = ReadScript("Optimization", "RenderTexturePool.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRenderTexturePoolRuntimeUsable(");

            StringAssert.Contains("if (!TryRegisterService())", onEnable);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CaptureScreenSetup();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "PrewarmCurrentScreenQueues();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "SceneManager.sceneUnloaded += HandleSceneUnloaded;");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterRenderTexturePoolRuntime(this);");
            StringAssert.Contains("RenderTexturePool registered = GlobalRegistry.RenderTexturePool", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterRenderTexturePoolRuntime(registered);", gate);
            StringAssert.Contains("pool._registeredService", usable);
            StringAssert.Contains("pool.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void VRAMMonitor_RuntimeOwnerGateClearsStaleRegistryBeforeProfilerRecordersAndTelemetryRouting()
        {
            string source = ReadScript("Optimization", "VRAMMonitor.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsVRAMMonitorRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "StartRecorders();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterService();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "CacheRegistryServicesCold();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterVRAMMonitorRuntime(this);");
            StringAssert.Contains("VRAMMonitor registered = GlobalRegistry.VRAMMonitor", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterVRAMMonitorRuntime(registered);", gate);
            StringAssert.Contains("monitor._registeredService", usable);
            StringAssert.Contains("monitor.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void VRAMPressureMonitor_RuntimeOwnerGateAvoidsDuplicateQualityRestoreAndRoutesOnlyClaimedOwner()
        {
            string source = ReadScript("Optimization", "VRAMPressureMonitor.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsVRAMPressureRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "BrgLodDistanceScalar = 1f;");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "if (!TryRegisterService())");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheDependencies();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted)", "if (!_registeredService && !TryRegisterService())");
            AssertTextBefore(start, "if (!_registeredService && !TryRegisterService())", "CacheDependencies();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "RestoreGlobalQualityOverrides();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "RestoreGlobalQualityOverrides();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterVRAMPressureRuntime(this);");
            StringAssert.Contains("VRAMPressureMonitor registered = GlobalRegistry.VRAMPressure", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterVRAMPressureRuntime(registered);", gate);
            StringAssert.Contains("monitor._registeredService", usable);
            StringAssert.Contains("monitor.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void AssetLoadDispatcher_RuntimeOwnerGateReconcilesStaleRegistryAndStaticMirrorBeforeDispatchRouting()
        {
            string source = ReadScript("Optimization", "AssetLoadDispatcher.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsAssetLoadDispatcherRuntimeUsable(");

            AssertTextBefore(onEnable, "if (!TryRegisterService())", "EnsureProgressSignalLaneCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwap();");
            AssertTextBefore(start, "if (!_registeredService && !TryRegisterService())", "EnsureProgressSignalLaneCold();");
            AssertTextBefore(start, "if (!_registeredService && !TryRegisterService())", "TryRegister();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterAssetLoadDispatcherRuntime(this);");

            StringAssert.Contains("AssetLoadDispatcher registered = GlobalRegistry.AssetLoadDispatcher", gate);
            StringAssert.Contains("AssetLoadDispatcher active = s_registeredInstance", gate);
            StringAssert.Contains("s_registeredInstance = registered", gate);
            StringAssert.Contains("s_registeredInstance = null", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAssetLoadDispatcherRuntime(registered);", gate);
            StringAssert.Contains("GlobalRegistry.RegisterAssetLoadDispatcherRuntime(active);", gate);
            StringAssert.Contains("dispatcher._registeredService", usable);
            StringAssert.Contains("dispatcher.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void AssetLifecycleGovernor_RuntimeOwnerGateClearsStaleRegistryBeforeNativeStorageAndTickRouting()
        {
            string source = ReadScript("Optimization", "AssetLifecycleGovernor.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsAssetLifecycleRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureManagedRecordStorage();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureNativeHandleStorage();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "if (!TryRegisterService())");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheDependencies();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwap();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted)", "if (!_registeredService && !TryRegisterService())");
            AssertTextBefore(start, "if (!_registeredService && !TryRegisterService())", "EnsureFallbackAssets();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ResetAddressableHeapRuntimeState(false);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ResetAddressableHeapRuntimeState(true);");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterAssetLifecycleRuntime(this);");
            StringAssert.Contains("AssetLifecycleGovernor registered = GlobalRegistry.AssetLifecycle", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAssetLifecycleRuntime(registered);", gate);
            StringAssert.Contains("governor._registeredService", usable);
            StringAssert.Contains("governor.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void MissionManager_RuntimeOwnerGateReconcilesStaleRegistryAndStaticMirrorBeforeQuestEvents()
        {
            string source = ReadScript("Gameplay", "MissionManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsMissionRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "QuestEvents.Register(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterMissionRuntime(this);");
            StringAssert.Contains("return _serviceRegistered;", register);

            AssertRuntimeGate(gate, "MissionManager registered = GlobalRegistry.Missions", "MissionManager active = s_activeRuntime", "IsMissionRuntimeUsable", "GlobalRegistry.UnregisterMissionRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterMissionRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntime = registered", gate);
            StringAssert.Contains("s_activeRuntime = null", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void PDAExchangeSystem_RuntimeOwnerGateClearsStaleRegistryBeforeSaveTickAndLiabilityEvents()
        {
            string source = ReadScript("Gameplay", "PDAExchangeSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPDAExchangeRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "_signalSourceId = RuntimeOriginRoute.FoldEntityIdToSourceId");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "RefreshColdRegistryReferences();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterLiabilityEvents();");
            AssertTextBefore(start, "if (!_serviceRegistered && !TryRegisterService())", "TryRegister();");
            AssertTextBefore(start, "if (!_serviceRegistered && !TryRegisterService())", "TryRegisterLiabilityEvents();");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPDAExchangeRuntime(this);");

            StringAssert.Contains("PDAExchangeSystem registered = GlobalRegistry.PDAExchange", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPDAExchangeRuntime(registered);", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void SuitUpgradeManager_RuntimeOwnerGateReconcilesStaleRegistryAndStaticMirrorBeforeVaultAndSaveRouting()
        {
            string source = ReadScript("Gameplay", "SuitUpgradeManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSuitUpgradeRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "if (baseStats == null)");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "Instantiate(baseStats)");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureSuitVaultBuffers();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "NarrativeEvents.Register(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "Hecton8.Core.GlobalRegistry.RegisterSuitUpgradeRuntime(this);");

            AssertRuntimeGate(gate, "SuitUpgradeManager registered = Hecton8.Core.GlobalRegistry.SuitUpgrades", "SuitUpgradeManager active = s_activeRuntimeInstance", "IsSuitUpgradeRuntimeUsable", "Hecton8.Core.GlobalRegistry.UnregisterSuitUpgradeRuntime");
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.RegisterSuitUpgradeRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void SuitUpgradeManager_NotificationQueueRefusalStaysDiagnosticAndDoesNotGateUpgradeState()
        {
            string source = ReadScript("Gameplay", "SuitUpgradeManager.cs");
            string install = ExtractMethodBody(source, "public bool InstallUpgrade(");
            string breakUpgrade = ExtractMethodBody(source, "public bool TryBreakRandomInstalledUpgrade(");
            string repair = ExtractMethodBody(source, "public bool RepairUpgrade(");
            string narrative = ExtractMethodBody(source, "public void OnNarrativeEvent(");
            string push = ExtractMethodBody(source, "private void PushSuitNotification(");
            string tryPush = ExtractMethodBody(source, "private void TryPushSuitNotificationMessage(");
            string report = ExtractMethodBody(source, "private void ReportSuitNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearSuitNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("private static readonly uint _SuitNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _SuitNotificationContextHash", source);
            StringAssert.Contains("public int SuitNotificationMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                install,
                "_installedUpgrades.Add(upgrade.upgradeId);",
                "RebuildRuntimeStats();",
                "PushSuitNotification("));
            Assert.IsTrue(ContainsTokensInOrder(
                breakUpgrade,
                "_brokenUpgrades.Add(upgrade.upgradeId);",
                "RebuildRuntimeStats();",
                "PushSuitNotification("));
            Assert.IsTrue(ContainsTokensInOrder(
                repair,
                "_brokenUpgrades.Remove(upgradeId)",
                "RebuildRuntimeStats();",
                "PushSuitNotification("));
            Assert.IsTrue(ContainsTokensInOrder(
                narrative,
                "_unlockedBlueprints.Add(u.requiredBlueprintId)",
                "PushSuitNotification("));
            StringAssert.Contains("TryPushSuitNotificationMessage(messageHash, warning);", push);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(messageHash);", push);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(messageHash);", push);
            StringAssert.Contains("ReportSuitNotificationMiss(0u);", push);
            AssertTextBefore(push, "ReportSuitNotificationMiss(0u);", "return;");
            StringAssert.Contains("? NotificationEvents.TryPushRegisteredWarning(messageHash)", tryPush);
            StringAssert.Contains(": NotificationEvents.TryPushRegisteredInfo(messageHash)", tryPush);
            StringAssert.Contains("ReportSuitNotificationMiss(messageHash);", tryPush);
            StringAssert.Contains("_suitNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_SuitNotificationMissWarningHash", report);
            StringAssert.Contains("_SuitNotificationContextHash ^ messageHash", report);
            StringAssert.Contains("math.max(1, _suitNotificationMissCount)", report);
            StringAssert.Contains("_suitNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearSuitNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearSuitNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearSuitNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_suitNotificationMissCount", populate);
            StringAssert.DoesNotContain("_suitNotificationMissCount", load);
        }

        [Test]
        public void PlayerExplorationTracker_RuntimeOwnerGateReconcilesStaleRegistryAndStaticMirrorBeforeCartographyRouting()
        {
            string source = ReadScript("PDA", "PlayerExplorationTracker.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPlayerExplorationRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "movementSampleDistance = math.max");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "InitializeExplorationMask();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterWithTickManager();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "MapMagicBiomeEvents.Register(this);");
            AssertTextBefore(start, "if (!_serviceRegistered && !TryRegisterService())", "TryRegisterCartographyDispatcher();");
            AssertTextBefore(start, "if (!_serviceRegistered && !TryRegisterService())", "SampleCurrentChunk(force: true);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerExplorationRuntime(this);");

            AssertRuntimeGate(gate, "PlayerExplorationTracker registered = GlobalRegistry.PlayerExploration", "PlayerExplorationTracker active = s_activeRuntimeInstance", "IsPlayerExplorationRuntimeUsable", "GlobalRegistry.UnregisterPlayerExplorationRuntime", "Destroy(this);");
            StringAssert.Contains("GlobalRegistry.RegisterPlayerExplorationRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("tracker._serviceRegistered", usable);
            StringAssert.Contains("tracker.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void InputManager_RuntimeOwnerGateReconcilesBridgeSlotAndStaticMirrorBeforeActionAssetsAndDeviceSubscriptions()
        {
            string source = ReadScript("Input", "InputManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string register = ExtractMethodBody(source, "private bool RegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsInputManagerRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "if (!RegisterService())");
            AssertTextBefore(awake, "if (!RegisterService())", "InitializeInputActions();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || _serviceShuttingDown)", "if (!RegisterService())");
            AssertTextBefore(onEnable, "if (!RegisterService())", "SubscribeToDeviceChanges();");
            AssertTextBefore(onEnable, "if (!RegisterService())", "EnsureInputActionsInitialized();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || (!_serviceRegistered && !RegisterService()))", "EnablePlayerInput();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, this);");
            StringAssert.Contains("return _serviceRegistered;", register);

            StringAssert.Contains("BootstrapRegistryBridge.TryResolve(", gate);
            StringAssert.Contains("InputManager registered = registeredRuntime as InputManager", gate);
            StringAssert.Contains("ReferenceEquals(registered, null)", gate);
            StringAssert.Contains("IsInputManagerRuntimeUsable(registered)", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("BootstrapRegistryBridge.Unregister(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, registeredRuntime);", gate);
            StringAssert.Contains("BootstrapRegistryBridge.Register(BootstrapRegistryBridgeSlot.NativeInputManagerRuntime, active);", gate);
            StringAssert.Contains("ActiveRuntimeInstance = null;", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._serviceShuttingDown", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void DepthZoneDirector_RuntimeOwnerGateReconcilesStaleRegistryAndStaticMirrorBeforeDepthEventsAndLocalization()
        {
            string source = ReadScript("World", "DepthZoneDirector.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsDepthZoneRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "EnsureMessageBuffers();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "LocalizationEvents.RegisterLanguageListener(this);");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterDepthZoneRuntime(this);");

            AssertRuntimeGate(gate, "DepthZoneDirector registered = GlobalRegistry.DepthZone", "DepthZoneDirector active = s_activeRuntimeInstance", "IsDepthZoneRuntimeUsable", "GlobalRegistry.UnregisterDepthZoneRuntime");
            StringAssert.Contains("GlobalRegistry.RegisterDepthZoneRuntime(active);", gate);
            StringAssert.Contains("s_activeRuntimeInstance = registered", gate);
            StringAssert.Contains("s_activeRuntimeInstance = null", gate);
            StringAssert.Contains("director._serviceRegistered", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void DepthZoneDirector_LateFrameBridgeReportsEventDropsAndNotificationRefusals()
        {
            string source = ReadScript("World", "DepthZoneDirector.cs");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string raise = ExtractMethodBody(source, "private void TryRaiseDepthZoneEvent(");
            string push = ExtractMethodBody(source, "private void TryPushDepthZoneNotification(");
            string eventDrop = ExtractMethodBody(source, "private void ReportDepthZoneEventDrop(");
            string notificationMiss = ExtractMethodBody(source, "private void ReportDepthZoneNotificationMiss(");
            string context = ExtractMethodBody(source, "private static uint ResolveDepthZoneTelemetryContext(");
            string clear = ExtractMethodBody(source, "private void ClearPendingPresentationEvents()");

            StringAssert.Contains("private static readonly uint _DepthZoneEventDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _DepthZoneNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _DepthZoneRuntimeContextHash", source);
            StringAssert.Contains("public int DepthZoneEventDropCount =>", source);
            StringAssert.Contains("public int DepthZoneNotificationMissCount =>", source);

            StringAssert.Contains("TryRaiseDepthZoneEvent(_pendingZoneExited, entered: false);", lateTick);
            StringAssert.Contains("TryRaiseDepthZoneEvent(_pendingZoneEntered, entered: true);", lateTick);
            StringAssert.Contains("TryPushDepthZoneNotification(", lateTick);
            StringAssert.DoesNotContain("DepthZoneEvents.TryRaiseZoneExited(_pendingZoneExited);", lateTick);
            StringAssert.DoesNotContain("DepthZoneEvents.TryRaiseZoneEntered(_pendingZoneEntered);", lateTick);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(GetZoneEnterMessageSpan(_pendingZoneNotification));", lateTick);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(GetHullWarningMessageSpan(_pendingHullWarningNotification));", lateTick);

            StringAssert.Contains("DepthZoneEvents.TryRaiseZoneEntered(zone)", raise);
            StringAssert.Contains("DepthZoneEvents.TryRaiseZoneExited(zone)", raise);
            StringAssert.Contains("ReportDepthZoneEventDrop(zone", raise);
            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportDepthZoneNotificationMiss(", push);

            StringAssert.Contains("_depthZoneEventDropCount++;", eventDrop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", eventDrop);
            StringAssert.Contains("_DepthZoneEventDropWarningHash", eventDrop);
            StringAssert.Contains("math.max(1, _depthZoneEventDropCount)", eventDrop);
            StringAssert.Contains("_depthZoneNotificationMissCount++;", notificationMiss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", notificationMiss);
            StringAssert.Contains("_DepthZoneNotificationMissWarningHash", notificationMiss);
            StringAssert.Contains("math.max(1, _depthZoneNotificationMissCount)", notificationMiss);
            StringAssert.Contains("_DepthZoneRuntimeContextHash ^ contextHash ^ zoneHash", context);
            StringAssert.Contains("_depthZoneEventDropCount = 0;", clear);
            StringAssert.Contains("_depthZoneNotificationMissCount = 0;", clear);
        }

        [Test]
        public void EclipseGameplaySystem_NotificationPushRefusalsStayVisible()
        {
            string source = ReadScript("Gameplay", "EclipseGameplaySystem.cs");
            string start = ExtractMethodBody(source, "private void HandleEclipseStart()");
            string end = ExtractMethodBody(source, "private void HandleEclipseEnd()");
            string push = ExtractMethodBody(source, "private void TryPushEclipseNotification(");
            string report = ExtractMethodBody(source, "private void ReportEclipseNotificationMiss(");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");

            StringAssert.Contains("private static readonly uint _EclipseNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _EclipseNotificationContextHash", source);
            StringAssert.Contains("private int _eclipseNotificationMissCount;", source);
            StringAssert.Contains("public int EclipseNotificationMissCount => _eclipseNotificationMissCount;", source);

            StringAssert.Contains("TryPushEclipseNotification(", start);
            StringAssert.Contains("warning: true", start);
            StringAssert.Contains("TryPushEclipseNotification(", end);
            StringAssert.Contains("warning: false", end);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", start);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedSpan(", end);
            AssertTextBefore(start, "TryRaiseEclipsePhaseChanged(true);", "TryPushEclipseNotification(");
            AssertTextBefore(end, "TryRaiseEclipsePhaseChanged(false);", "TryPushEclipseNotification(");

            StringAssert.Contains("? NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains(": NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportEclipseNotificationMiss(warning);", push);
            StringAssert.Contains("_eclipseNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_EclipseNotificationMissWarningHash", report);
            StringAssert.Contains("_EclipseNotificationContextHash ^ severityHash", report);
            StringAssert.Contains("Mathf.Max(1, _eclipseNotificationMissCount)", report);
            StringAssert.Contains("_eclipseNotificationMissCount = 0;", onDisable);
        }

        [Test]
        public void EclipseGameplaySystem_EventLaneBackpressureStaysVisibleWithoutNoListenerNoise()
        {
            string source = ReadScript("Gameplay", "EclipseGameplaySystem.cs");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string start = ExtractMethodBody(source, "private void HandleEclipseStart()");
            string end = ExtractMethodBody(source, "private void HandleEclipseEnd()");
            string publishBiolum = ExtractMethodBody(source, "private void PublishBiolumMultiplier(");
            string phase = ExtractMethodBody(source, "private void TryRaiseEclipsePhaseChanged(");
            string predators = ExtractMethodBody(source, "private void TryRaiseEclipseNightPredatorsRising(");
            string temperature = ExtractMethodBody(source, "private void TryRaiseEclipseTemperatureDelta(");
            string biolum = ExtractMethodBody(source, "private void TryRaiseEclipseBiolumMultiplierChanged(");
            string report = ExtractMethodBody(source, "private void ReportEclipseEventDropIfBackpressured(");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");

            StringAssert.Contains("private static readonly uint _EclipseEventDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _EclipsePhaseStartedContextHash", source);
            StringAssert.Contains("private static readonly uint _EclipsePhaseEndedContextHash", source);
            StringAssert.Contains("private static readonly uint _EclipsePredatorRiseContextHash", source);
            StringAssert.Contains("private static readonly uint _EclipseTemperatureDeltaContextHash", source);
            StringAssert.Contains("private static readonly uint _EclipseBiolumMultiplierContextHash", source);
            StringAssert.Contains("private int _eclipseEventDropCount;", source);
            StringAssert.Contains("public int EclipseEventDropCount => _eclipseEventDropCount;", source);

            StringAssert.Contains("TryRaiseEclipseTemperatureDelta(-_currentTempDrop);", slowTick);
            StringAssert.Contains("TryRaiseEclipseNightPredatorsRising(predatorRiseIntensity);", slowTick);
            StringAssert.Contains("TryRaiseEclipsePhaseChanged(true);", start);
            StringAssert.Contains("TryRaiseEclipsePhaseChanged(false);", end);
            StringAssert.Contains("TryRaiseEclipseBiolumMultiplierChanged(clampedMultiplier);", publishBiolum);
            StringAssert.DoesNotContain("EclipseGameplayEvents.TryRaiseTemperatureDelta(-_currentTempDrop);", slowTick);
            StringAssert.DoesNotContain("EclipseGameplayEvents.TryRaiseNightPredatorsRising(predatorRiseIntensity);", slowTick);
            StringAssert.DoesNotContain("EclipseGameplayEvents.TryRaisePhaseChanged(true);", start);
            StringAssert.DoesNotContain("EclipseGameplayEvents.TryRaisePhaseChanged(false);", end);
            StringAssert.DoesNotContain("EclipseGameplayEvents.TryRaiseBiolumMultiplierChanged(clampedMultiplier);", publishBiolum);

            StringAssert.Contains("if (EclipseGameplayEvents.TryRaisePhaseChanged(active))", phase);
            StringAssert.Contains("ReportEclipseEventDropIfBackpressured(active ? _EclipsePhaseStartedContextHash : _EclipsePhaseEndedContextHash);", phase);
            StringAssert.Contains("if (EclipseGameplayEvents.TryRaiseNightPredatorsRising(intensity))", predators);
            StringAssert.Contains("ReportEclipseEventDropIfBackpressured(_EclipsePredatorRiseContextHash);", predators);
            StringAssert.Contains("if (EclipseGameplayEvents.TryRaiseTemperatureDelta(delta))", temperature);
            StringAssert.Contains("ReportEclipseEventDropIfBackpressured(_EclipseTemperatureDeltaContextHash);", temperature);
            StringAssert.Contains("if (EclipseGameplayEvents.TryRaiseBiolumMultiplierChanged(multiplier))", biolum);
            StringAssert.Contains("ReportEclipseEventDropIfBackpressured(_EclipseBiolumMultiplierContextHash);", biolum);

            StringAssert.Contains("if (EclipseGameplayEvents.PendingCount <= 0)", report);
            StringAssert.Contains("return;", report);
            StringAssert.Contains("_eclipseEventDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_EclipseEventDropWarningHash", report);
            StringAssert.Contains("_EclipseGameplayContextHash ^ contextHash", report);
            StringAssert.Contains("Mathf.Max(1, _eclipseEventDropCount)", report);
            AssertTextBefore(report, "if (EclipseGameplayEvents.PendingCount <= 0)", "_eclipseEventDropCount++;");
            StringAssert.Contains("_eclipseEventDropCount = 0;", onDisable);
        }

        [Test]
        public void HectonBiolumManager_RuntimeOwnerGateClearsStaleRegistryBeforeRuntimeResourcesAndVisualSubscriptions()
        {
            string source = ReadScript(Path.Combine("World", "Biolum"), "HectonBiolumManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsBiolumManagerRuntimeUsable(");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "if (!TryRegisterService())");
            AssertTextBefore(awake, "if (!TryRegisterService())", "EnsureRuntimeResources();");
            AssertTextBefore(awake, "if (!TryRegisterService())", "ResetFloraShaderGlobals();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "HectonFloatingOrigin.RegisterListener(this);");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "SpectrumEvents.RegisterSonarPulseListener(this);");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || (!_serviceRegistered && !TryRegisterService()))", "Initialize();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ResetFloraShaderGlobals();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ResetFloraShaderGlobals();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GameBootstrapper.RegisterBiolumDirector(this)");
            StringAssert.Contains("return _serviceRegistered;", register);
            StringAssert.Contains("HectonBiolumManager registered = GlobalRegistry.BiolumManager", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("GameBootstrapper.UnregisterBiolumDirector(registered);", gate);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._disposed", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void ProceduralLadderClimbRuntime_RuntimeOwnerGateClearsStaleRegistryBeforeVaultBuffersAndIkRouting()
        {
            string source = ReadScript(Path.Combine("Animation", "Locomotion"), "ProceduralLadderClimbRuntime.cs");
            string ensure = ExtractMethodBody(source, "internal static ProceduralLadderClimbRuntime EnsureRuntimeInstance()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsLadderClimbRuntimeUsable(");

            StringAssert.Contains("if (IsLadderClimbRuntimeUsable(registered))", ensure);
            AssertTextBefore(ensure, "if (IsLadderClimbRuntimeUsable(registered))", "new GameObject(\"[ProceduralLadderClimbRuntime]\")");
            StringAssert.Contains("GlobalRegistry.ClearProceduralLadderClimbRuntime(registered);", ensure);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "OpenOrAcquireVaultBuffersForOwnerRoute();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "CompleteOutstandingJobForBarrier();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "CompleteOutstandingJobForBarrier();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterProceduralLadderClimbRuntime(this);");
            StringAssert.Contains("return _serviceRegistered;", register);
            StringAssert.Contains("ProceduralLadderClimbRuntime registered = GlobalRegistry.ProceduralLadderClimbRuntime", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("GlobalRegistry.ClearProceduralLadderClimbRuntime(registered);", gate);
            StringAssert.Contains("runtime._serviceRegistered", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void AbyssalDeferredCausticsRuntime_RuntimeOwnerGateReconcilesRegistryPublishedAndRuntimeMirrorsBeforeGpuResources()
        {
            string source = ReadScript(Path.Combine("Rendering", "AbyssalCaustics"), "AbyssalDeferredCausticsRuntime.cs");
            string ensure = ExtractMethodBody(source, "public static AbyssalDeferredCausticsRuntime EnsureRuntimeInstance()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static AbyssalDeferredCausticsRuntime ResolveUsableRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsCausticsRuntimeUsable(");

            StringAssert.Contains("AbyssalDeferredCausticsRuntime runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[AbyssalDeferredCausticsRuntime]\")");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", awake);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "s_runtimeInstance = this;");
            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", onEnable);
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "s_runtimeInstance = this;");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this)", ownership);
            StringAssert.Contains("_runtimeOwnerAborted = false;", ownership);

            StringAssert.Contains("ICausticsService registeredService = GlobalRegistry.Caustics", gate);
            StringAssert.Contains("AbyssalDeferredCausticsRuntime registeredRuntime = registeredService as AbyssalDeferredCausticsRuntime", gate);
            StringAssert.Contains("IsCausticsRuntimeUsable(registeredRuntime)", gate);
            StringAssert.Contains("registeredRuntime.ClearPublishedConstantBufferIfOwnedByThis();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterCausticsService(registeredService);", gate);
            StringAssert.Contains("GlobalRegistry.RegisterCausticsService(active);", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);

            StringAssert.Contains("GlobalRegistry.Caustics is AbyssalDeferredCausticsRuntime registeredRuntime", resolver);
            StringAssert.Contains("s_publishedRuntime = null;", resolver);
            StringAssert.Contains("s_runtimeInstance = null;", resolver);
            StringAssert.Contains("runtime._ownsRegistrySlot", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void PlayerRuntimeContextService_RuntimeOwnerGateReconcilesRuntimeMirrorAndContextBeforePlayerSyncAndTicks()
        {
            string source = ReadScript("Core", "PlayerRuntimeContextService.cs");
            string ensure = ExtractMethodBody(source, "public static PlayerRuntimeContextService EnsureRuntimeInstance()");
            string tryBind = ExtractMethodBody(source, "public static bool TryBindPlayerRoot(");
            string initialize = ExtractMethodBody(source, "private void InitializeServiceInternal(");
            string refresh = ExtractMethodBody(source, "internal void RefreshRuntimeContext()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string register = ExtractMethodBody(source, "private bool TryRegisterContext()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PlayerRuntimeContextService ResolveUsableRuntime()");
            string contextUsable = ExtractMethodBody(source, "private static bool IsPlayerContextUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsPlayerRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("PlayerRuntimeContextService runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[PlayerRuntimeContextService]\")");
            StringAssert.Contains("if (IsPlayerContextUsable(registeredContext)", ensure);
            StringAssert.Contains("return null;", ensure);
            StringAssert.Contains("PlayerRuntimeContextService runtimeService = EnsureRuntimeInstance();", tryBind);
            StringAssert.Contains("if (runtimeService == null)", tryBind);
            AssertTextBefore(tryBind, "if (runtimeService == null)", "runtimeService.BindPlayerRoot(playerRoot);");

            AssertTextBefore(refresh, "if (_runtimeOwnerAborted || !_isInitialized)", "SyncPlayerContext();");
            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "TryRegisterUpdatable();");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "SyncPlayerContext();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterContext())", "SyncPlayerContext();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.ClearPlayerRuntimeContextRuntime(this);");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("GlobalRegistry.ClearPlayerRuntimeContextRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterPlayerRuntimeContextRuntime(this);", ownership);
            StringAssert.Contains("return ReferenceEquals(GlobalRegistry.PlayerRuntimeContextRuntime, this);", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerRuntimeContext(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);", register);
            StringAssert.Contains("GlobalRegistry.ClearPlayerRuntimeContextRuntime(staleRuntime);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredContext;", register);
            StringAssert.Contains("return _registeredContext;", register);

            StringAssert.Contains("PlayerRuntimeContextService runtime = GlobalRegistry.PlayerRuntimeContextRuntime;", gate);
            StringAssert.Contains("if (IsPlayerRuntimeUsable(runtime))", gate);
            StringAssert.Contains("IPlayerRuntimeContext registeredContext = GlobalRegistry.RegisteredPlayer;", gate);
            StringAssert.Contains("if (IsPlayerContextUsable(registeredContext))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);", gate);

            StringAssert.Contains("if (IsPlayerRuntimeUsable(runtime))", resolver);
            StringAssert.Contains("GlobalRegistry.ClearPlayerRuntimeContextRuntime(runtime);", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerRuntimeContext(registeredContext);", resolver);
            StringAssert.Contains("PlayerRuntimeContextService runtime = context as PlayerRuntimeContextService;", contextUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", contextUsable);
            StringAssert.Contains("runtime._registeredContext", contextUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
            StringAssert.DoesNotContain("IServiceShutdown staleContextShutdown", source);
        }

        [Test]
        public void PlayerInventoryManager_RuntimeOwnerGateReconcilesActiveMirrorAndServiceBeforeInventorySyncAndTicks()
        {
            string source = ReadScript("Core", "PlayerInventoryManager.cs");
            string ensure = ExtractMethodBody(source, "public static PlayerInventoryManager EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PlayerInventoryManager ResolveUsableRuntime()");
            string serviceUsable = ExtractMethodBody(source, "private static bool IsInventoryServiceUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsInventoryRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("PlayerInventoryManager runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[PlayerInventoryManager]\")");
            StringAssert.Contains("if (IsInventoryServiceUsable(registeredService)", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterSlowTickable();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "SyncInventoryContextCold();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSlowTickable();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "SyncInventoryContextCold();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterSlowTickable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("ActiveRuntimeInstance = null;", ownership);
            StringAssert.Contains("ActiveRuntimeInstance = this;", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerInventoryService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerInventoryService(registeredService);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredService;", register);
            StringAssert.Contains("return _registeredService;", register);

            StringAssert.Contains("PlayerInventoryManager runtime = ActiveRuntimeInstance;", gate);
            StringAssert.Contains("if (IsInventoryRuntimeUsable(runtime))", gate);
            StringAssert.Contains("IPlayerInventoryService registeredService = GlobalRegistry.RegisteredPlayerInventory;", gate);
            StringAssert.Contains("if (IsInventoryServiceUsable(registeredService))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerInventoryService(registeredService);", gate);

            StringAssert.Contains("if (IsInventoryRuntimeUsable(runtime))", resolver);
            StringAssert.Contains("ActiveRuntimeInstance = null;", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerInventoryService(registeredService);", resolver);
            StringAssert.Contains("PlayerInventoryManager runtime = service as PlayerInventoryManager;", serviceUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", serviceUsable);
            StringAssert.Contains("runtime._registeredService", serviceUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
            StringAssert.DoesNotContain("registeredService != null && !ReferenceEquals(registeredService, this)", source);
        }

        [Test]
        public void PlayerSensoryManager_RuntimeOwnerGateReconcilesActiveRuntimeAndServiceBeforeSensorySyncAndTicks()
        {
            string source = ReadScript("Core", "PlayerSensoryManager.cs");
            string ensure = ExtractMethodBody(source, "public static PlayerSensoryManager EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PlayerSensoryManager ResolveUsableRuntime()");
            string serviceUsable = ExtractMethodBody(source, "private static bool IsSensoryServiceUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsSensoryRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("PlayerSensoryManager runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[PlayerSensoryManager]\")");
            StringAssert.Contains("if (IsSensoryServiceUsable(registeredService)", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterUpdatable();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "SyncSensoryContextCold();");
            AssertTextBefore(tick, "if (_runtimeOwnerAborted)", "RefreshSensoryContextHot();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterUpdatable();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "SyncSensoryContextCold();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("PlayerSensoryManager runtime = s_activeRuntime;", ownership);
            StringAssert.Contains("runtime = GlobalRegistry.PlayerSensoryRuntime;", ownership);
            StringAssert.Contains("GlobalRegistry.ClearPlayerSensoryRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterPlayerSensoryRuntime(this);", ownership);
            StringAssert.Contains("return ReferenceEquals(s_activeRuntime, this)", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerSensoryService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerSensoryService(registeredService);", register);
            StringAssert.Contains("GlobalRegistry.ClearPlayerSensoryRuntime(staleRuntime);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredService;", register);
            StringAssert.Contains("return _registeredService;", register);

            StringAssert.Contains("PlayerSensoryManager runtime = s_activeRuntime;", gate);
            StringAssert.Contains("runtime = GlobalRegistry.PlayerSensoryRuntime;", gate);
            StringAssert.Contains("if (IsSensoryRuntimeUsable(runtime))", gate);
            StringAssert.Contains("IPlayerSensoryService registeredService = GlobalRegistry.RegisteredPlayerSensory;", gate);
            StringAssert.Contains("if (IsSensoryServiceUsable(registeredService))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerSensoryService(registeredService);", gate);

            StringAssert.Contains("PlayerSensoryManager runtime = s_activeRuntime;", resolver);
            StringAssert.Contains("runtime = GlobalRegistry.PlayerSensoryRuntime;", resolver);
            StringAssert.Contains("s_activeRuntime = runtime;", resolver);
            StringAssert.Contains("GlobalRegistry.ClearPlayerSensoryRuntime(runtime);", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerSensoryService(registeredService);", resolver);
            StringAssert.Contains("PlayerSensoryManager runtime = service as PlayerSensoryManager;", serviceUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", serviceUsable);
            StringAssert.Contains("runtime._registeredService", serviceUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
            StringAssert.DoesNotContain("registeredService != null && !ReferenceEquals(registeredService, this)", source);
        }

        [Test]
        public void GCMonitor_RuntimeOwnerGateClearsStaleRegistryBeforeSamplingAndPostFixedLane()
        {
            string source = ReadScript("Core", "GCMonitor.cs");
            string ensure = ExtractMethodBody(source, "public static GCMonitor EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string postFixed = ExtractMethodBody(source, "public void PostFixedTick(");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryRejectForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsGCMonitorRuntimeUsable(");

            StringAssert.Contains("GCMonitor runtime = GlobalRegistry.GCMonitorRuntime;", ensure);
            StringAssert.Contains("if (IsGCMonitorRuntimeUsable(runtime))", ensure);
            AssertTextBefore(ensure, "if (IsGCMonitorRuntimeUsable(runtime))", "new GameObject(\"[GCMonitor]\")");
            StringAssert.Contains("GlobalRegistry.ClearGCMonitorRuntime(runtime);", ensure);

            AssertTextBefore(initialize, "if (_runtimeOwnerRejected || !EnsureRuntimeOwnership())", "RefreshPhysicalMemorySnapshotCold();");
            AssertTextBefore(initialize, "if (_runtimeOwnerRejected || !EnsureRuntimeOwnership())", "TryRegisterPostFixed();");
            AssertTextBefore(awake, "if (TryRejectForUsableExistingRuntime())", "RefreshPhysicalMemorySnapshotCold();");
            AssertTextBefore(awake, "if (TryRejectForUsableExistingRuntime())", "GlobalRegistry.RegisterGCMonitorRuntime(this);");
            StringAssert.Contains("if (!ReferenceEquals(GlobalRegistry.GCMonitorRuntime, this))", awake);
            AssertTextBefore(onEnable, "if (_runtimeOwnerRejected || !EnsureRuntimeOwnership())", "RefreshPhysicalMemorySnapshotCold();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerRejected || !EnsureRuntimeOwnership())", "TryRegisterPostFixed();");
            AssertTextBefore(start, "if (_runtimeOwnerRejected || !EnsureRuntimeOwnership())", "TryRegisterPostFixed();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerRejected)", "TryUnregisterPostFixed();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerRejected)", "OnDisable();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerRejected)", "OnDisable();");
            AssertTextBefore(rebind, "if (_runtimeOwnerRejected)", "TryUnregisterPostFixed();");
            AssertTextBefore(postFixed, "if (_runtimeOwnerRejected)", "SystemDispatcher.CurrentFrameIndex");

            StringAssert.Contains("if (TryRejectForUsableExistingRuntime())", ownership);
            StringAssert.Contains("GlobalRegistry.ClearGCMonitorRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterGCMonitorRuntime(this);", ownership);
            StringAssert.Contains("_runtimeOwnerRejected = !ownsRuntime;", ownership);
            StringAssert.Contains("return ownsRuntime;", ownership);

            StringAssert.Contains("GCMonitor runtime = GlobalRegistry.GCMonitorRuntime;", gate);
            StringAssert.Contains("if (IsGCMonitorRuntimeUsable(runtime))", gate);
            StringAssert.Contains("_runtimeOwnerRejected = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.ClearGCMonitorRuntime(runtime);", gate);
            StringAssert.Contains("runtime._registeredPostFixed = false;", gate);

            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerRejected", usable);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
        }

        [Test]
        public void SceneInstantiationGate_RuntimeOwnerGateReconcilesActiveAndRegistryBeforeCacheAndHotSwap()
        {
            string source = ReadScript("Bootstrap", "SceneInstantiationGate.cs");
            string reset = ExtractMethodBody(source, "private static void ResetStaticState()");
            string ensure = ExtractMethodBody(source, "internal static SceneInstantiationGate EnsureRuntimeInstance()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static SceneInstantiationGate ResolveUsableRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSceneInstantiationGateRuntimeUsable(");

            StringAssert.Contains("GlobalRegistry.ClearSceneInstantiationGateRuntime(null);", reset);
            StringAssert.Contains("SceneInstantiationGate runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[SceneInstantiationGate]\")");
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "CacheRegistryServicesCold();");
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "_vramPressure = currentService as IVramPressureReadModel;");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("SceneInstantiationGate runtime = s_activeRuntime;", ownership);
            StringAssert.Contains("runtime = GlobalRegistry.SceneInstantiationGateRuntime;", ownership);
            StringAssert.Contains("GlobalRegistry.ClearSceneInstantiationGateRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterSceneInstantiationGateRuntime(this);", ownership);
            StringAssert.Contains("_runtimeOwnerAborted = !ownsRuntime;", ownership);
            StringAssert.Contains("return ownsRuntime;", ownership);

            StringAssert.Contains("SceneInstantiationGate runtime = s_activeRuntime;", gate);
            StringAssert.Contains("runtime = GlobalRegistry.SceneInstantiationGateRuntime;", gate);
            StringAssert.Contains("if (IsSceneInstantiationGateRuntimeUsable(runtime))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.ClearSceneInstantiationGateRuntime(runtime);", gate);

            StringAssert.Contains("SceneInstantiationGate runtime = s_activeRuntime;", resolver);
            StringAssert.Contains("runtime = GlobalRegistry.SceneInstantiationGateRuntime;", resolver);
            StringAssert.Contains("s_activeRuntime = runtime;", resolver);
            StringAssert.Contains("GlobalRegistry.ClearSceneInstantiationGateRuntime(runtime);", resolver);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
        }

        [Test]
        public void PrefabRegistry_RuntimeOwnerGateReconcilesActiveAndRegistryBeforeFallbackCreation()
        {
            string source = ReadScript(string.Empty, "PrefabRegistry.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string ensure = ExtractMethodBody(source, "private static PrefabRegistry EnsureRuntimeInstance()");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static PrefabRegistry ResolveUsableRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsPrefabRegistryRuntimeUsable(");
            string clear = ExtractMethodBody(source, "private static void ClearRuntimeMirrorIfOwnedBy(");

            StringAssert.Contains("public static PrefabRegistry ActiveRuntimeInstance =>", source);
            StringAssert.Contains("IsPrefabRegistryRuntimeUsable(s_activeRuntimeInstance)", source);
            StringAssert.Contains(": ResolveUsableRuntime();", source);
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "return;");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "if (GlobalRegistry.PrefabRegistryRuntime == this)");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "if (GlobalRegistry.PrefabRegistryRuntime != this)");
            StringAssert.Contains("PrefabRegistry runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[PrefabRegistry]\")");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("PrefabRegistry runtime = s_activeRuntimeInstance;", ownership);
            StringAssert.Contains("runtime = GlobalRegistry.PrefabRegistryRuntime;", ownership);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterPrefabRegistryRuntime(this);", ownership);
            StringAssert.Contains("_runtimeOwnerAborted = !ownsRuntime;", ownership);
            StringAssert.Contains("return ownsRuntime;", ownership);

            StringAssert.Contains("PrefabRegistry runtime = s_activeRuntimeInstance;", gate);
            StringAssert.Contains("runtime = GlobalRegistry.PrefabRegistryRuntime;", gate);
            StringAssert.Contains("if (IsPrefabRegistryRuntimeUsable(runtime))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", gate);

            StringAssert.Contains("PrefabRegistry runtime = s_activeRuntimeInstance;", resolver);
            StringAssert.Contains("runtime = GlobalRegistry.PrefabRegistryRuntime;", resolver);
            StringAssert.Contains("s_activeRuntimeInstance = runtime;", resolver);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", resolver);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            StringAssert.Contains("GlobalRegistry.ClearPrefabRegistryRuntime(runtime);", clear);
            StringAssert.Contains("s_activeRuntimeInstance = null;", clear);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
            StringAssert.DoesNotContain("s_activeRuntimeInstance ?? GlobalRegistry.PrefabRegistryRuntime", source);
        }

        [Test]
        public void RuntimePerformanceProfiler_RuntimeOwnerGateReconcilesActiveAndRegistryBeforeRecordersAndTickLanes()
        {
            string source = ReadScript(string.Empty, "RuntimePerformanceProfiler.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string activeOwner = ExtractMethodBody(source, "private bool IsActiveRuntimeOwner()");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static RuntimePerformanceProfiler ResolveUsableRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsRuntimePerformanceProfilerRuntimeUsable(");
            string clear = ExtractMethodBody(source, "private static void ClearRuntimeMirrorIfOwnedBy(");
            string startProfiling = ExtractMethodBody(source, "public void StartProfiling()");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string registerTicks = ExtractMethodBody(source, "private void RegisterWithTickManager()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("internal static RuntimePerformanceProfiler ActiveRuntime =>", source);
            StringAssert.Contains("IsRuntimePerformanceProfilerRuntimeUsable(s_activeRuntime)", source);
            StringAssert.Contains(": ResolveUsableRuntime();", source);
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "ClampSettings();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "SceneManager.sceneLoaded +=");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "RegisterWithTickManager();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "StartProfiling();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "RegisterWithTickManager();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "if (!Application.isPlaying)");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "if (!IsActiveRuntimeOwner())");
            StringAssert.Contains("return !_runtimeOwnerAborted &&", activeOwner);
            AssertTextBefore(startProfiling, "if (_runtimeOwnerAborted)", "StopProfiling();");
            AssertTextBefore(tick, "if (_runtimeOwnerAborted)", "if (!_debugProfilingActive)");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "if (!_debugProfilingActive)");
            AssertTextBefore(lateFrame, "if (_runtimeOwnerAborted)", "PumpPendingRuntimeRoutes(sampleDeltaTime);");
            AssertTextBefore(registerTicks, "if (_runtimeOwnerAborted || !IsActiveRuntimeOwner()", "GlobalRegistry.TryRegisterUpdatable(this");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted || !IsActiveRuntimeOwner())", "RegisterWithTickManager();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("RuntimePerformanceProfiler runtime = s_activeRuntime;", ownership);
            StringAssert.Contains("runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;", ownership);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterRuntimePerformanceProfilerRuntime(this);", ownership);
            StringAssert.Contains("_runtimeOwnerAborted = !ownsRuntime;", ownership);
            StringAssert.Contains("return ownsRuntime;", ownership);

            StringAssert.Contains("RuntimePerformanceProfiler runtime = s_activeRuntime;", gate);
            StringAssert.Contains("runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;", gate);
            StringAssert.Contains("if (IsRuntimePerformanceProfilerRuntimeUsable(runtime))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", gate);

            StringAssert.Contains("RuntimePerformanceProfiler runtime = s_activeRuntime;", resolver);
            StringAssert.Contains("runtime = GlobalRegistry.RuntimePerformanceProfilerRuntime;", resolver);
            StringAssert.Contains("s_activeRuntime = runtime;", resolver);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", resolver);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            StringAssert.Contains("GlobalRegistry.ClearRuntimePerformanceProfilerRuntime(runtime);", clear);
            StringAssert.Contains("s_activeRuntime = null;", clear);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
        }

        [Test]
        public void GameBootstrapper_RuntimeOwnerGateMakesDuplicateAbortInertBeforeGlobalTeardown()
        {
            string source = ReadScript("Bootstrap", "GameBootstrapper.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string resume = ExtractMethodBody(source, "private void EnsureBootstrapProgressAfterLifecycleResume()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegisterBootstrapSlowTickable()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string beginBootstrap = ExtractMethodBody(source, "public void BeginBootstrap()");
            string claim = ExtractMethodBody(source, "private static bool ClaimRuntimeBootstrapInstance(");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner(");
            string resolver = ExtractMethodBody(source, "private static GameBootstrapper ResolveUsableRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsBootstrapperRuntimeUsable(");
            string clear = ExtractMethodBody(source, "private static void ClearRuntimeMirrorIfOwnedBy(");

            StringAssert.Contains("public static GameBootstrapper ActiveInstance => ResolveUsableRuntime();", source);
            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("GameBootstrapper runtimeBootstrapper = ResolveUsableRuntime();", awake);
            AssertTextBefore(awake, "AbortDuplicateRuntimeOwner(destroyComponent: true);", "RuntimeShaderReferenceCatalog.Register(runtimeShaderReferenceCatalog);");
            AssertTextBefore(awake, "if (!ClaimRuntimeBootstrapInstance(this))", "RuntimeShaderReferenceCatalog.Register(runtimeShaderReferenceCatalog);");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterHotSwapListener();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted)", "EnsureBootstrapProgressAfterLifecycleResume();");
            AssertTextBefore(resume, "if (_runtimeOwnerAborted)", "RecoverReloadDisabledStaleBootstrapRun();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServicesInReverseBootstrapOrder();");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "if (!_isBootstrapComplete");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterSlowTickable(this");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "RebindBootstrapSchedulerVaults(");
            AssertTextBefore(beginBootstrap, "if (_runtimeOwnerAborted)", "if (!ClaimRuntimeBootstrapInstance(this))");

            StringAssert.Contains("GameBootstrapper registeredBootstrapper = ResolveUsableRuntime();", claim);
            StringAssert.Contains("ReferenceEquals(registeredBootstrapper.gameObject, instance.gameObject)", claim);
            StringAssert.Contains("registeredBootstrapper.AbortDuplicateRuntimeOwner(destroyComponent: false);", claim);
            StringAssert.Contains("instance.AbortDuplicateRuntimeOwner(destroyComponent: false);", claim);
            StringAssert.Contains("GlobalRegistry.RegisterBootstrapperRuntime(instance);", claim);
            StringAssert.Contains("GlobalRegistry.Phase != GlobalRegistry.RegistryPhase.Registering", claim);
            StringAssert.Contains("return ownsRuntime;", claim);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_bootstrapRunInProgress = false;", abort);
            StringAssert.Contains("_sceneActivationStarted = false;", abort);
            StringAssert.Contains("_slowTickableRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);

            StringAssert.Contains("GameBootstrapper runtime = s_activeRuntimeInstance;", resolver);
            StringAssert.Contains("runtime = GlobalRegistry.BootstrapperRuntime;", resolver);
            StringAssert.Contains("s_activeRuntimeInstance = runtime;", resolver);
            StringAssert.Contains("ClearRuntimeMirrorIfOwnedBy(runtime);", resolver);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            StringAssert.Contains("GlobalRegistry.ClearBootstrapperRuntime(runtime);", clear);
            StringAssert.Contains("s_activeRuntimeInstance = null;", clear);
            StringAssert.DoesNotContain("s_activeRuntimeInstance ?? GlobalRegistry.BootstrapperRuntime", source);
        }

        [Test]
        public void SpatialAudioManager_RuntimeOwnerGateClaimsAudioServicesBeforeResourcesEventsAndTicks()
        {
            string source = ReadScript(string.Empty, "SpatialAudioManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string serviceShutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState(");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string register = ExtractMethodBody(source, "private bool TryRegisterAudioRuntimeServices()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string subscribe = ExtractMethodBody(source, "private void TrySubscribeAudioEvents()");
            string physicsRebind = ExtractMethodBody(source, "private void RebindPhysicsStateEventService(");
            string physicsUsable = ExtractMethodBody(source, "private static bool IsPhysicsStateEventServiceUsable(");
            string repairDroneAcoustic = ExtractMethodBody(source, "private void HandleRepairDroneTorchAcoustic(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsSpatialAudioRuntimeUsable(");
            string audioUsable = ExtractMethodBody(source, "private static bool IsAudioServiceOwnerUsable(");
            string virtualizationUsable = ExtractMethodBody(source, "private static bool IsAudioVirtualizationOwnerUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "ActiveRuntimeInstance = this;");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "RefreshMixerParameterAvailability();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "AcousticOcclusionUtility.AcquireRuntime();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ShutdownServiceState(releaseRuntimeResources: false);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState(releaseRuntimeResources: true);");
            AssertTextBefore(serviceShutdown, "if (_runtimeOwnerAborted)", "ShutdownServiceState(releaseRuntimeResources: true);");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.UnregisterAudioVirtualizationService(this);");

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted)", "if (!TryRegisterAudioRuntimeServices())");
            AssertTextBefore(initialize, "if (!TryRegisterAudioRuntimeServices())", "EnsureRuntimeResourcesInitialized();");
            AssertTextBefore(initialize, "if (!TryRegisterAudioRuntimeServices())", "TrySubscribeAudioEvents();");
            AssertTextBefore(initialize, "if (!TryRegisterAudioRuntimeServices())", "TryRegisterUpdatable();");

            AssertTextBefore(register, "if (_runtimeOwnerAborted)", "GlobalRegistry.RegisterAudioService(this);");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterAudioService(this);");
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioService(registeredAudioService);", register);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", register);
            StringAssert.Contains("bool ownsServices =", register);
            StringAssert.Contains("return ownsServices;", register);
            AssertTextBefore(subscribe, "FatalPressureImplosionEvents.Unregister(this);", "FatalPressureImplosionEvents.Register(this);");
            AssertTextBefore(subscribe, "RepairDroneTorchAcousticEvents.Unregister(this);", "RepairDroneTorchAcousticEvents.Register(this);");
            AssertTextBefore(physicsRebind, "!IsPhysicsStateEventServiceUsable(_physicsStateEvents)", "_physicsStateEvents.RegisterImpactListener(this);");
            StringAssert.Contains("return physicsStateEvents != null && physicsStateEvents.IsInitialized;", physicsUsable);
            StringAssert.Contains("acousticEvent.Clip == null ||", repairDroneAcoustic);
            StringAssert.Contains("!IsFinite(acousticEvent.Position)", repairDroneAcoustic);
            StringAssert.Contains("float volume = math.saturate(SanitizeFinite(acousticEvent.Volume, 0f));", repairDroneAcoustic);
            StringAssert.Contains("if (volume <= 0f)", repairDroneAcoustic);
            StringAssert.Contains("float pitch = math.clamp(SanitizeFinite(acousticEvent.Pitch, 1f), 0.1f, 3f);", repairDroneAcoustic);
            AssertTextBefore(repairDroneAcoustic, "!IsFinite(acousticEvent.Position)", "PlayAtPoint(");
            AssertTextBefore(repairDroneAcoustic, "float volume = math.saturate", "PlayAtPoint(");
            AssertTextBefore(repairDroneAcoustic, "float pitch = math.clamp", "PlayAtPoint(");

            StringAssert.Contains("SpatialAudioManager activeRuntime = ActiveRuntimeInstance;", gate);
            StringAssert.Contains("if (IsSpatialAudioRuntimeUsable(activeRuntime))", gate);
            StringAssert.Contains("IAudioService registeredAudioService = GlobalRegistry.Audio;", gate);
            StringAssert.Contains("IAudioVirtualizationService registeredVirtualization = GlobalRegistry.AudioVirtualization;", gate);
            StringAssert.Contains("RestoreActiveRuntimeInstanceFromOwner(registeredAudioService);", gate);
            StringAssert.Contains("RestoreActiveRuntimeInstanceFromOwner(registeredVirtualization);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioService(registeredAudioService);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);", gate);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            AssertTextBefore(abort, "TryUnsubscribeAudioEvents();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "FatalPressureImplosionEvents.Unregister(this);", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "RepairDroneTorchAcousticEvents.Unregister(this);", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "GlobalRegistry.UnregisterAudioVirtualizationService(this);", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "GlobalRegistry.UnregisterAudioService(this);", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_isInitialized = false;", abort);
            StringAssert.Contains("_registeredUpdatable = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);
            StringAssert.Contains("manager.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", runtimeUsable);
            StringAssert.Contains("manager._runtimeOwnerAborted", audioUsable);
            StringAssert.Contains("manager._runtimeOwnerAborted", virtualizationUsable);
        }

        [Test]
        public void PlayerCriticalProceduralAudioRenderer_RuntimeOwnerGateClaimsRuntimeBeforeAudioConfigAndProducer()
        {
            string source = ReadScript("Audio", "PlayerCriticalProceduralAudioRenderer.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceRebound(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntimeService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsPlayerCriticalAudioRuntimeUsable(");
            string physicsRebind = ExtractMethodBody(source, "private void RebindPhysicsStateEventService(");
            string physicsUsable = ExtractMethodBody(source, "private static bool IsPhysicsStateEventServiceUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "RefreshAudioConfiguration();");
            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "CacheColdRegistryReferences();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "if (!TryRegisterRuntimeService())");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "AcousticOcclusionUtility.AcquireRuntime();");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "AudioSettings.OnAudioConfigurationChanged += HandleAudioConfigurationChanged;");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "StartAudioProducerThread();");

            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterRuntimeService();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "StopAudioProducerThread();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "StopAudioProducerThread();");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "CacheRegistryServiceReference(serviceSlot, currentService);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "CacheRegistryServiceReference(serviceSlot, currentService);");

            AssertTextBefore(register, "if (_runtimeOwnerAborted)", "if (_runtimeRegistered || !Application.isPlaying)");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);");
            StringAssert.Contains("PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio;", register);
            StringAssert.Contains("if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", register);
            StringAssert.Contains("GlobalRegistry.RegisterPlayerCriticalAudioRuntime(this);", register);
            StringAssert.Contains("return _runtimeRegistered;", register);

            StringAssert.Contains("if (_runtimeOwnerAborted)", gate);
            StringAssert.Contains("if (!Application.isPlaying)", gate);
            StringAssert.Contains("PlayerCriticalProceduralAudioRenderer registeredInstance = GlobalRegistry.PlayerCriticalAudio;", gate);
            StringAssert.Contains("if (IsPlayerCriticalAudioRuntimeUsable(registeredInstance))", gate);
            StringAssert.Contains("Volatile.Write(ref s_runtimeInstalled, 1);", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterPlayerCriticalAudioRuntime(registeredInstance);", gate);
            AssertTextBefore(physicsRebind, "!IsPhysicsStateEventServiceUsable(_physicsStateEvents)", "_physicsStateEvents.RegisterImpactListener(this);");
            StringAssert.Contains("return physicsStateEvents != null && physicsStateEvents.IsInitialized;", physicsUsable);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_runtimeRegistered = false;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_slowTickRegistered = false;", abort);
            StringAssert.Contains("_lateFrameRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);
            StringAssert.Contains("renderer._runtimeRegistered", usable);
            StringAssert.Contains("renderer.isActiveAndEnabled", usable);
            StringAssert.Contains("!renderer._runtimeOwnerAborted", usable);
        }

        [Test]
        public void VocalWarningSystem_RuntimeOwnerGateClaimsRuntimeBeforeNativeStorageAndTickLanes()
        {
            string source = ReadScript("Audio", "VocalWarningSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceRebound(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string postSimulation = ExtractMethodBody(source, "private void TryRegisterPostSimulation()");
            string unregister = ExtractMethodBody(source, "private void UnregisterRuntime()");
            string ensureNative = ExtractMethodBody(source, "private void EnsureNativeStorage()");
            string register = ExtractMethodBody(source, "private bool TryRegisterRuntimeService()");
            string usable = ExtractMethodBody(source, "private static bool IsVocalWarningSystemUsable(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");

            StringAssert.Contains("private int _runtimeOwnerAborted;", source);
            StringAssert.Contains("public bool IsInitialized => Volatile.Read(ref _nativeAllocated) != 0 &&", source);
            StringAssert.Contains("Volatile.Read(ref _runtimeOwnerAborted) == 0;", source);
            StringAssert.Contains("public int PendingCount => Volatile.Read(ref _runtimeOwnerAborted) != 0 ? 0 : math.max(0, _queueCount);", source);
            StringAssert.Contains("public byte CurrentWarningId => Volatile.Read(ref _runtimeOwnerAborted) != 0 ? (byte)0 : _currentWarningId;", source);
            StringAssert.Contains("public bool IsWarningActive => Volatile.Read(ref _runtimeOwnerAborted) == 0 && _warningPlaybackRemainingSeconds > 0f;", source);

            AssertTextBefore(awake, "if (TryAbortForUsableExistingRuntime())", "if (!TryRegisterRuntimeService())");
            AssertTextBefore(awake, "if (!TryRegisterRuntimeService())", "EnsureNativeStorage();");
            AssertTextBefore(awake, "if (!TryRegisterRuntimeService())", "RefreshCachedServicesCold();");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "if (!TryRegisterRuntimeService())");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "EnsureNativeStorage();");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterRuntimeService())", "TryRegisterPostSimulation();");

            AssertTextBefore(onDisable, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "UnregisterRuntime();");
            AssertTextBefore(onDestroy, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "UnregisterRuntime();");
            AssertTextBefore(onDestroy, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "DisposeNativeStorage();");
            AssertTextBefore(tick, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "RunVocalWarningFrame(deltaTime, NextOwnerFrameId());");
            AssertTextBefore(slowTick, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "RunVocalWarningFrame(0.1f, NextOwnerFrameId());");
            AssertTextBefore(lateTick, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "VisualSyncPresentationTick();");
            AssertTextBefore(rebind, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "RebindDataVault(nextVault);");
            AssertTextBefore(replaced, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "RebindDataVault(nextVault);");

            AssertTextBefore(postSimulation, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "GlobalRegistry.TryRegisterDispatcherSystem(_simulationSystem)");
            AssertTextBefore(unregister, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "CompletePendingVocalWarningJobsForTeardown();");
            AssertTextBefore(unregister, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "GlobalRegistry.UnregisterVocalWarningRuntime(this);");
            AssertTextBefore(ensureNative, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "CacheDataVaultCold();");
            AssertTextBefore(hotSwap, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "GlobalRegistry.TryRegisterHotSwapListener(this)");

            AssertTextBefore(register, "if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", "if (Volatile.Read(ref _registeredRuntime) != 0 || !Application.isPlaying)");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings;");
            StringAssert.Contains("if (IsVocalWarningSystemUsable(registeredVocalWarnings))", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", register);
            StringAssert.Contains("GlobalRegistry.RegisterVocalWarningRuntime(this);", register);
            StringAssert.Contains("Volatile.Write(ref _registeredRuntime, registered ? 1 : 0);", register);
            StringAssert.Contains("return registered;", register);
            StringAssert.DoesNotContain("Destroy(this);", register);

            StringAssert.Contains("vocalWarningSystem is VocalWarningSystem runtime", usable);
            StringAssert.Contains("Volatile.Read(ref runtime._runtimeOwnerAborted) == 0", usable);
            StringAssert.Contains("Volatile.Read(ref runtime._registeredRuntime) != 0", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("return vocalWarningSystem.IsInitialized;", usable);

            StringAssert.Contains("if (Volatile.Read(ref _runtimeOwnerAborted) != 0)", gate);
            StringAssert.Contains("if (!Application.isPlaying)", gate);
            StringAssert.Contains("IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings;", gate);
            StringAssert.Contains("if (IsVocalWarningSystemUsable(registeredVocalWarnings))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);", gate);

            StringAssert.Contains("Volatile.Write(ref _runtimeOwnerAborted, 1);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredRuntime, 0);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredPostSimulation, 0);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredHotSwap, 0);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredUpdate, 0);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredSlowTick, 0);", abort);
            StringAssert.Contains("Volatile.Write(ref _registeredLateFrameTick, 0);", abort);
            StringAssert.Contains("DisposeNativeStorage();", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);
        }

        [Test]
        public void HectonAtmosphereManager_RuntimeOwnerGateClaimsRuntimeBeforeRenderGlobalsAndTickLanes()
        {
            string source = ReadScript(string.Empty, "HectonAtmosphereManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string tryRegister = ExtractMethodBody(source, "private void TryRegister()");
            string tryRegisterLate = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string tryRegisterHotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsAtmosphereRuntimeUsable(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string explicitLateTick = ExtractMethodBody(source, "void ILateFrameTickable.LateFrameTick()");
            string flush = ExtractMethodBody(source, "private void FlushLateFramePresentation()");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterService())", "ValidateAbyssAtmospherePresentationLayout();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterService())", "_biomeProfileDict = new Dictionary<int, AtmosphereProfile>(16);");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterService())", "EnsureAegirRingShadowCookie();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "MapMagicBiomeEvents.Register(this);");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "ApplyCurrentMatrixAtmosphereOverride();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted) return;", "TryRegister();");

            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ResetCycleShaderGlobals();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ResetCycleShaderGlobals();");
            AssertTextBefore(tryRegister, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(tryRegisterLate, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(tryRegisterHotSwap, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterHotSwapListener(this);");

            AssertTextBefore(register, "if (_registeredAtmosphereRuntime || !Application.isPlaying)", "if (_runtimeOwnerAborted || TryAbortForUsableExistingRuntime())");
            AssertTextBefore(register, "if (_runtimeOwnerAborted || TryAbortForUsableExistingRuntime())", "HectonAtmosphereManager registeredAtmosphere = GlobalRegistry.Atmosphere;");
            StringAssert.Contains("if (IsAtmosphereRuntimeUsable(registeredAtmosphere))", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("GlobalRegistry.UnregisterAtmosphereRuntime(registeredAtmosphere);", register);
            StringAssert.Contains("GlobalRegistry.RegisterAtmosphereRuntime(this);", register);
            StringAssert.Contains("_registeredAtmosphereRuntime = ReferenceEquals(GlobalRegistry.Atmosphere, this);", register);
            StringAssert.Contains("return _registeredAtmosphereRuntime;", register);
            StringAssert.DoesNotContain("Destroy(this);", register);

            StringAssert.Contains("HectonAtmosphereManager registeredAtmosphere = GlobalRegistry.Atmosphere;", gate);
            StringAssert.Contains("if (IsAtmosphereRuntimeUsable(registeredAtmosphere))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAtmosphereRuntime(registeredAtmosphere);", gate);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registeredAtmosphereRuntime = false;", abort);
            StringAssert.Contains("_registeredHotSwapListener = false;", abort);
            StringAssert.Contains("_registeredToTickManager = false;", abort);
            StringAssert.Contains("_registeredLateFrameTick = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);

            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("manager._registeredAtmosphereRuntime", usable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", usable);
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "RunAtmosphereTimeline(AtmosphereTimelineStepSeconds);");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "FlushLateFramePresentation();");
            AssertTextBefore(explicitLateTick, "if (_runtimeOwnerAborted)", "FlushLateFramePresentation();");
            AssertTextBefore(flush, "if (_runtimeOwnerAborted)", "FlushCycleShaderGlobals();");
        }

        [Test]
        public void FoveatedRenderCommander_RuntimeOwnerGateClearsStaleStaticBeforeTelemetryAndXrPolicy()
        {
            string source = ReadScript(Path.Combine("Graphics", "VR"), "FoveatedRenderCommander.cs");
            string bootstrap = ExtractMethodBody(source, "private static void BootstrapAfterSceneLoad()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string dispose = ExtractMethodBody(source, "public void Dispose()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string requestDump = ExtractMethodBody(source, "internal void RequestBlackBoxDump()");
            string inactive = ExtractMethodBody(source, "private bool IsInactiveCommander()");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsCommanderRuntimeUsable(");
            string registerTick = ExtractMethodBody(source, "private void TryRegisterTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegisterSlowTick()");
            string registerHotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwap()");
            string registerRenderable = ExtractMethodBody(source, "private void TryRegisterRenderable()");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(bootstrap, "IsCommanderRuntimeUsable(s_activeCommander)", "new GameObject(RuntimeObjectName)");
            AssertTextBefore(bootstrap, "s_activeCommander = null;", "new GameObject(RuntimeObjectName)");
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "EnsureTelemetry();");
            AssertTextBefore(awake, "if (!EnsureRuntimeOwnership())", "CacheRuntimeCapabilitySnapshotCold();");
            AssertTextBefore(onEnable, "if (!EnsureRuntimeOwnership())", "RebindDataVaultForLifecycle(GlobalRegistry.DataVault);");
            AssertTextBefore(onEnable, "if (!EnsureRuntimeOwnership())", "ApplyPolicy(force: true);");
            AssertTextBefore(start, "if (!EnsureRuntimeOwnership())", "TryRegisterRenderable();");
            AssertTextBefore(start, "if (!EnsureRuntimeOwnership())", "ApplyPolicy(force: true);");

            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ClearHardwareFoveation();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ReleaseTelemetryBuffer();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "Dispose();");
            AssertTextBefore(dispose, "if (_runtimeOwnerAborted || _disposed)", "ClearHardwareFoveation();");
            AssertTextBefore(dispose, "if (_runtimeOwnerAborted || _disposed)", "ReleaseTelemetryBuffer();");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "TryQueueDetachIfInactiveCommander()");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "TryDetachIfInactiveCommander()");
            StringAssert.Contains("if (_runtimeOwnerAborted || !ReferenceEquals(s_activeCommander, this) || _disposed)", requestDump);
            StringAssert.Contains("return _runtimeOwnerAborted || !ReferenceEquals(s_activeCommander, this) || _disposed || _detachRequested;", inactive);

            AssertTextBefore(ownership, "if (_runtimeOwnerAborted)", "if (TryAbortForUsableExistingRuntime())");
            StringAssert.Contains("s_activeCommander = this;", ownership);
            StringAssert.Contains("FoveatedRenderCommander activeCommander = s_activeCommander;", gate);
            StringAssert.Contains("if (IsCommanderRuntimeUsable(activeCommander))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("s_activeCommander = null;", gate);

            StringAssert.Contains("TryUnregisterRenderable();", abort);
            StringAssert.Contains("TryUnregisterHotSwap();", abort);
            StringAssert.Contains("TryUnregisterTick();", abort);
            StringAssert.Contains("TryUnregisterSlowTick();", abort);
            StringAssert.Contains("ReleaseTelemetryBuffer();", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_disposed = true;", abort);
            StringAssert.Contains("_registeredRenderable = false;", abort);
            StringAssert.Contains("_registeredHotSwap = false;", abort);
            StringAssert.Contains("_registeredLateFrame = false;", abort);
            StringAssert.Contains("_registeredSlowTick = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);
            StringAssert.DoesNotContain("ClearHardwareFoveation();", abort);

            StringAssert.Contains("commander.isActiveAndEnabled", usable);
            StringAssert.Contains("!commander._runtimeOwnerAborted", usable);
            StringAssert.Contains("!commander._disposed", usable);
            AssertTextBefore(registerTick, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);");
            AssertTextBefore(registerHotSwap, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(registerRenderable, "if (_runtimeOwnerAborted)", "GlobalRegistry.Renderables.TryRegister(this);");
            StringAssert.DoesNotContain("s_activeCommander != null", source);
        }

        [Test]
        public void PDAMarkerRegistry_RuntimeOwnerGateClaimsServiceBeforeSaveOriginAndHotSwap()
        {
            string source = ReadScript("PDA", "PDAMarkerRegistry.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterWithSaveManager()");
            string saveUnregister = ExtractMethodBody(source, "private void UnregisterFromSaveManager()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsPdaMarkerRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "_saveService = GlobalRegistry.Save;");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "TryRegisterWithSaveManager();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "HectonFloatingOrigin.RegisterListener(this);");
            AssertTextBefore(start, "if (_runtimeOwnerAborted)", "TryRegisterWithSaveManager();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "HectonFloatingOrigin.UnregisterListener(this);");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "HectonFloatingOrigin.UnregisterListener(this);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(saveRegister, "if (_runtimeOwnerAborted)", "saveService.Register(this);");
            StringAssert.Contains("if (!IsSaveServiceUsable(saveService))", saveRegister);
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            StringAssert.DoesNotContain("_saveService.Register(this);", saveRegister);
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", saveUnregister);
            StringAssert.Contains("_registeredSaveService = null;", saveUnregister);
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "UnregisterFromSaveManager();");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterHotSwapListener(this);");

            AssertTextBefore(register, "if (_serviceRegistered || !Application.isPlaying)", "if (_runtimeOwnerAborted || TryAbortForUsableExistingRuntime())");
            AssertTextBefore(register, "if (_runtimeOwnerAborted || TryAbortForUsableExistingRuntime())", "PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;");
            StringAssert.Contains("if (IsPdaMarkerRuntimeUsable(registeredRuntime))", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(registeredRuntime);", register);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.RegisterPDAMarkerRuntime(this);", register);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.PDAMarkers, this);", register);
            StringAssert.Contains("return _serviceRegistered;", register);
            StringAssert.DoesNotContain("Destroy(this);", register);

            StringAssert.Contains("PDAMarkerRegistry registeredRuntime = Hecton8.Core.GlobalRegistry.PDAMarkers;", gate);
            StringAssert.Contains("if (IsPdaMarkerRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterPDAMarkerRuntime(registeredRuntime);", gate);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_registeredToSave = false;", abort);
            StringAssert.Contains("_registeredSaveService = null;", abort);
            StringAssert.Contains("_registeredHotSwapListener = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);
            StringAssert.Contains("registry.isActiveAndEnabled", usable);
            StringAssert.Contains("registry._serviceRegistered", usable);
            StringAssert.Contains("!registry._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registeredRuntime != null", source);
        }

        [Test]
        public void Atlas6CorporateLiabilityManager_RuntimeOwnerGateClaimsActiveBeforeEventsSaveAndTicks()
        {
            string source = ReadScript(Path.Combine("Gameplay", "Atlas6Liability"), "Atlas6CorporateLiabilityManager.cs");
            string staticReport = ExtractMethodBody(source, "public static bool TryReportXenonOmegaExtracted(");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string audioEvent = ExtractMethodBody(source, "public void OnAudioLogEvent(");
            string narrativeEvent = ExtractMethodBody(source, "public void OnNarrativeEvent(");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string registerUpdatable = ExtractMethodBody(source, "private void RegisterWithGlobalRegistry()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string audioRegister = ExtractMethodBody(source, "private void TryRegisterAudioLogEvents()");
            string narrativeRegister = ExtractMethodBody(source, "private void TryRegisterNarrativeEvents()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string syncAudioLog = ExtractMethodBody(source, "private void TrySyncDisasterEvidenceFromAudioLogRuntime()");
            string syncNarrative = ExtractMethodBody(source, "private void TrySyncWorkerTagsFromNarrativeDiscoveryReadModel(");
            string ensureSubsystems = ExtractMethodBody(source, "private void EnsureSubsystemsInitialized()");
            string wire = ExtractMethodBody(source, "private void WireSubsystemEvents()");
            string registerActive = ExtractMethodBody(source, "private bool TryRegisterActiveRuntimeInstance()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsLiabilityRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(staticReport, "if (!IsLiabilityRuntimeUsable(activeRuntime))", "activeRuntime.ReportXenonOmegaExtracted(amount);");
            AssertTextBefore(awake, "if (!TryRegisterActiveRuntimeInstance())", "Telemetry = new Atlas6LiabilityTelemetry();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "EnsureSubsystemsInitialized();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "TryRegisterAudioLogEvents();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "TryRegisterNarrativeEvents();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "RegisterWithGlobalRegistry();");
            AssertTextBefore(onEnable, "if (!TryRegisterActiveRuntimeInstance())", "WireSubsystemEvents();");

            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "UnregisterFromGlobalRegistry();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "UnregisterFromGlobalRegistry();");
            AssertTextBefore(tick, "if (_runtimeOwnerAborted || !_isRegistered)", "SanitizeSectorXenonOmegaYield();");
            AssertTextBefore(audioEvent, "if (_runtimeOwnerAborted)", "if (payload.Type != AudioLogEventType.Discovered");
            AssertTextBefore(narrativeEvent, "if (_runtimeOwnerAborted)", "if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade");
            AssertTextBefore(save, "if (_runtimeOwnerAborted || data == null)", "data.atlas6LiabilitySectorXenonOmegaYield");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || data == null)", "EnsureSubsystemsInitialized();");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)");
            AssertTextBefore(registerUpdatable, "if (_runtimeOwnerAborted || !Application.isPlaying)", "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _registeredHotSwapListener || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(audioRegister, "if (_runtimeOwnerAborted || _registeredAudioLogEvents || !Application.isPlaying)", "AudioLogEvents.Register(this);");
            AssertTextBefore(narrativeRegister, "if (_runtimeOwnerAborted || _registeredNarrativeEvents || !Application.isPlaying)", "NarrativeEvents.Register(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            AssertTextBefore(syncAudioLog, "if (_runtimeOwnerAborted || !Application.isPlaying)", "ReportDisasterEvidenceCollected();");
            AssertTextBefore(syncNarrative, "if (_runtimeOwnerAborted || !Application.isPlaying)", "ReportWorkerTagScannedHash(ChenMWorkerTagHash);");
            AssertTextBefore(ensureSubsystems, "if (_runtimeOwnerAborted)", "Telemetry = new Atlas6LiabilityTelemetry();");
            AssertTextBefore(wire, "if (_runtimeOwnerAborted)", "if (!ReferenceEquals(_wiredActuarialLiability, ActuarialLiability))");

            AssertTextBefore(registerActive, "if (_runtimeOwnerAborted)", "if (!Application.isPlaying)");
            StringAssert.Contains("Atlas6CorporateLiabilityManager activeRuntime = ActiveRuntimeInstance;", registerActive);
            StringAssert.Contains("if (IsLiabilityRuntimeUsable(activeRuntime))", registerActive);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerActive);
            StringAssert.Contains("ActiveRuntimeInstance = null;", registerActive);
            StringAssert.Contains("ActiveRuntimeInstance = this;", registerActive);
            StringAssert.Contains("ActiveRuntimeInstanceChanged?.Invoke(this);", registerActive);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_isRegistered = false;", abort);
            StringAssert.Contains("_registeredHotSwapListener = false;", abort);
            StringAssert.Contains("_registeredAudioLogEvents = false;", abort);
            StringAssert.Contains("_registeredNarrativeEvents = false;", abort);
            StringAssert.Contains("_saveRegistered = false;", abort);
            StringAssert.Contains("_saveService = null;", abort);
            StringAssert.Contains("_audioLogs = null;", abort);
            StringAssert.Contains("UnwireSubsystemEvents();", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(this);", abort);

            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("activeRuntime != null && !ReferenceEquals(activeRuntime, this)", source);
        }

        [Test]
        public void AtlasSignalSystem_RuntimeOwnerGateClaimsServiceBeforeSignalTicksSaveAndShader()
        {
            string source = ReadScript("AtlasSignal", "AtlasSignalSystem.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string slowTickCore = ExtractMethodBody(source, "private void SlowTickCore()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string readCore = ExtractMethodBody(source, "public bool TryReadAtlasSignalCoreAup(");
            string readSnapshot = ExtractMethodBody(source, "public bool TryReadAtlasSignalSnapshot(");
            string decode = ExtractMethodBody(source, "public void DecodeSignal(uint messageHash)");
            string resolvePlayer = ExtractMethodBody(source, "private void ResolvePlayer()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegister()");
            string registerLateTick = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string queueShader = ExtractMethodBody(source, "private void QueueShaderStrength(");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterService()");
            string unregisterService = ExtractMethodBody(source, "private void TryUnregisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsAtlasSignalRuntimeUsable(");
            string cache = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string populateSave = ExtractMethodBody(source, "public void PopulateSaveData(");
            string loadSave = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheEncryptedLogHashes();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRuntimeDependencies();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterLateFrame();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "ResolvePlayer();");

            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterService();");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "long solveStartTicks = Stopwatch.GetTimestamp();");
            AssertTextBefore(slowTickCore, "if (_runtimeOwnerAborted)", "if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted || !_pendingShaderStrengthDirty)", "Shader.SetGlobalFloat(_ShaderSignalStrength, _pendingShaderStrength);");
            AssertTextBefore(readCore, "if (_runtimeOwnerAborted)", "return TryResolveAtlasCoreAup(out coreAup);");
            AssertTextBefore(readSnapshot, "if (_runtimeOwnerAborted || !observerAup.IsFinite())", "if (!TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup))");
            AssertTextBefore(decode, "if (_runtimeOwnerAborted || messageHash == 0u)", "AtlasSignalEvents.TryRaiseDecoded(messageHash);");
            AssertTextBefore(resolvePlayer, "if (_runtimeOwnerAborted)", "_playerMovement = null;");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);");
            AssertTextBefore(registerLateTick, "if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);");
            AssertTextBefore(queueShader, "if (_runtimeOwnerAborted)", "_pendingShaderStrength = math.isfinite(strength01)");
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "_playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "if (_runtimeOwnerAborted || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)",
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = Hecton8.Core.GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("_saveService.Register(this)", saveRegister);
            StringAssert.DoesNotContain("if (_saveService == null)", saveRegister);
            AssertTextBefore(populateSave, "if (_runtimeOwnerAborted || data == null) return;", "data.atlasSignalDetected = _signalEverDetected;");
            AssertTextBefore(loadSave, "if (_runtimeOwnerAborted || data == null) return;", "_signalEverDetected = data.atlasSignalDetected;");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "AtlasSignalSystem registeredRuntime = GlobalRegistry.AtlasSignal;");
            StringAssert.Contains("if (IsAtlasSignalRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterAtlasSignalRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterAtlasSignalRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignal, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);
            StringAssert.DoesNotContain("Destroy(gameObject);", registerService);
            AssertTextBefore(unregisterService, "if (_runtimeOwnerAborted || !_serviceRegistered)", "GlobalRegistry.UnregisterAtlasSignalRuntime(this);");

            StringAssert.Contains("AtlasSignalSystem registeredRuntime = GlobalRegistry.AtlasSignal;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsAtlasSignalRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(_DuplicateRuntimeWarningHash, _AtlasSignalContextHash, 1f);", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAtlasSignalRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregister();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterLateFrame();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterSaveParticipant();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_saveRegistered = false;", abort);
            StringAssert.Contains("_lateFrameRegistered = false;", abort);
            StringAssert.Contains("_pendingShaderStrengthDirty = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("!ReferenceEquals(system, null)", usable);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("!system._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("GlobalRegistry.AtlasSignal != null", source);
            StringAssert.DoesNotContain("registeredRuntime != null && !ReferenceEquals(registeredRuntime, this)", source);
        }

        [Test]
        public void AtlasSignalSystem_RevealNotificationRefusalStaysDiagnosticAndDoesNotGateRevealState()
        {
            string source = ReadScript("AtlasSignal", "AtlasSignalSystem.cs");
            string reveal = ExtractMethodBody(source, "private void HandleRevealStageUnlocked(");
            string push = ExtractMethodBody(source, "private void TryPushRevealNotification(");
            string report = ExtractMethodBody(source, "private void ReportRevealNotificationMiss(");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _RevealNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _RevealNotificationContextHash", source);
            StringAssert.Contains("public int RevealNotificationMissCount =>", source);

            Assert.IsTrue(ContainsTokensInOrder(
                reveal,
                "case 2:",
                "TryQueueEncryptedLog(2);",
                "TryPushRevealNotification(",
                "warning: false",
                "revealStage);"));
            Assert.IsTrue(ContainsTokensInOrder(
                reveal,
                "case 3:",
                "TryEnsureIdentityDiscoveryPublished();",
                "TryQueueEncryptedLog(3);",
                "TryPushRevealNotification(",
                "warning: true",
                "revealStage);"));
            Assert.IsTrue(ContainsTokensInOrder(
                reveal,
                "case 4:",
                "TryEnsureFullDecodeDiscoveryPublished();",
                "TryQueueEncryptedLog(4);",
                "TryPushRevealNotification(",
                "warning: true",
                "revealStage);"));
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedSpan(", reveal);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", reveal);

            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportRevealNotificationMiss(revealStage);", push);
            StringAssert.Contains("_revealNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_RevealNotificationMissWarningHash", report);
            StringAssert.Contains("_AtlasSignalContextHash ^ _RevealNotificationContextHash ^ unchecked((uint)revealStage)", report);
            StringAssert.Contains("math.max(1, _revealNotificationMissCount)", report);
            StringAssert.Contains("ClearRevealNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearRevealNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearRevealNotificationDiagnostics();", abort);
            StringAssert.Contains("ClearRevealNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_revealNotificationMissCount", populate);
            StringAssert.DoesNotContain("_revealNotificationMissCount", load);
        }

        [Test]
        public void AtlasSignalDecoder_RuntimeOwnerGateClearsStaleRegistryBeforeSlowTickAndSignalEvents()
        {
            string source = ReadScript("AtlasSignal", "AtlasSignalDecoder.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegister()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(source, "private void CacheAtlasSignalCold()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterToGlobalRegistry()");
            string registerEvents = ExtractMethodBody(source, "private void TryRegisterAtlasSignalEvents()");
            string signalEvent = ExtractMethodBody(source, "public void OnAtlasSignalEvent(");
            string pulse = ExtractMethodBody(source, "private void HandleSignalPulse(");
            string synchronize = ExtractMethodBody(source, "private void TrySynchronizePhaseFromSignal()");
            string tryAdvance = ExtractMethodBody(source, "internal bool TryAdvanceDecode(");
            string submitWave = ExtractMethodBody(source, "public float SubmitWaveMatch(");
            string advance = ExtractMethodBody(source, "private bool AdvanceDecodeProgress(");
            string complete = ExtractMethodBody(source, "private void CompleteDecode()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsAtlasSignalDecoderRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(onEnable, "if (!TryRegisterToGlobalRegistry())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterToGlobalRegistry())", "CacheAtlasSignalCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterToGlobalRegistry())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterToGlobalRegistry())", "TryRegisterAtlasSignalEvents();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted) return;", "if (_fullyDecoded) return;");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "if (serviceSlot == GlobalRegistryServiceSlot.AtlasSignalRuntime)");
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "_atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(registerEvents, "if (_runtimeOwnerAborted || _atlasSignalEventRegistered)", "AtlasSignalEvents.Register(this);");
            AssertTextBefore(signalEvent, "if (_runtimeOwnerAborted)", "if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Pulse)");
            AssertTextBefore(pulse, "if (_runtimeOwnerAborted)", "if (_fullyDecoded) return;");
            AssertTextBefore(synchronize, "if (_runtimeOwnerAborted)", "if (_fullyDecoded)");
            AssertTextBefore(tryAdvance, "if (_runtimeOwnerAborted)", "return _decodeWindowOpen && AdvanceDecodeProgress(dt);");
            AssertTextBefore(submitWave, "if (_runtimeOwnerAborted)", "_submittedCarrierFrequencyHz = SanitizeFrequencyHz(carrierFrequencyHz);");
            AssertTextBefore(advance, "if (_runtimeOwnerAborted || _fullyDecoded || !_decodeWindowOpen)", "float unlockThreshold01 = ResolveWaveMatchUnlockThreshold01();");
            AssertTextBefore(complete, "if (_runtimeOwnerAborted || _fullyDecoded)", "_fullyDecoded = true;");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "AtlasSignalDecoder registeredRuntime = GlobalRegistry.AtlasSignalDecoder;");
            StringAssert.Contains("if (IsAtlasSignalDecoderRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterAtlasSignalDecoderRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.AtlasSignalDecoder, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);
            StringAssert.DoesNotContain("Destroy(gameObject);", registerService);

            StringAssert.Contains("AtlasSignalDecoder registeredRuntime = GlobalRegistry.AtlasSignalDecoder;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsAtlasSignalDecoderRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterAtlasSignalDecoderRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregister();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterAtlasSignalEvents();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_atlasSignalEventRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_atlasSignal = null;", abort);
            StringAssert.Contains("_firstHourDirector = null;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("!ReferenceEquals(decoder, null)", usable);
            StringAssert.Contains("decoder._serviceRegistered", usable);
            StringAssert.Contains("decoder.isActiveAndEnabled", usable);
            StringAssert.Contains("!decoder._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registeredRuntime != null && !ReferenceEquals(registeredRuntime, this)", source);
        }

        [Test]
        public void Atlas6DirectiveSystem_RuntimeOwnerGateClaimsServiceBeforeDirectiveEventsSaveAndTicks()
        {
            string source = ReadScript("AtlasSignal", "Atlas6DirectiveSystem.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegister()");
            string registerLateTick = ExtractMethodBody(source, "private void TryRegisterLateFrameTick()");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterService()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(source, "private void CacheAtlasDependenciesCold()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string narrativeRegister = ExtractMethodBody(source, "private void TryRegisterNarrativeEvents()");
            string atlasRegister = ExtractMethodBody(source, "private void TryRegisterAtlas6Events()");
            string narrativeEvent = ExtractMethodBody(source, "public void OnNarrativeEvent(");
            string atlasEvent = ExtractMethodBody(source, "public void OnAtlas6Event(");
            string barter = ExtractMethodBody(source, "public void RegisterBarterTransaction()");
            string resolvePlayer = ExtractMethodBody(source, "private void ResolvePlayer()");
            string resolveAup = ExtractMethodBody(source, "private bool TryResolvePlayerAup(");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsAtlas6DirectiveRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("private bool _narrativeEventRegistered;", source);
            StringAssert.Contains("private bool _atlas6EventRegistered;", source);

            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheAtlasDependenciesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterNarrativeEvents();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterAtlas6Events();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(onDisable, "TryUnregisterNarrativeEvents();", "TryUnregisterAtlas6Events();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregister();");
            StringAssert.Contains("TryUnregisterNarrativeEvents();", onDestroy);
            StringAssert.Contains("TryUnregisterAtlas6Events();", onDestroy);

            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "IAtlasSignalReadModel signal = _atlasSignal;");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "FlushQueuedNotifications();");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted || _registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);");
            AssertTextBefore(registerLateTick, "if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);");
            AssertTextBefore(queue, "if (_runtimeOwnerAborted)", "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)");
            AssertTextBefore(flush, "if (_runtimeOwnerAborted)", "int count = _pendingNotificationCount;");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "Atlas6DirectiveSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Atlas6Directive;");
            StringAssert.Contains("if (IsAtlas6DirectiveRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.RegisterAtlas6DirectiveRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Atlas6Directive, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);
            StringAssert.DoesNotContain("Destroy(gameObject);", registerService);

            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "_atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;");
            Assert.IsTrue(ContainsTokensInOrder(
                saveRegister,
                "if (_runtimeOwnerAborted || _saveRegistered || !Application.isPlaying || !isActiveAndEnabled)",
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                "saveService.Register(this);",
                "_registeredSaveService = saveService;",
                "_saveRegistered = true;"));
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("_saveService.Register(this)", saveRegister);
            StringAssert.DoesNotContain("if (_saveService == null)", saveRegister);
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(narrativeRegister, "if (_runtimeOwnerAborted || _narrativeEventRegistered)", "NarrativeEvents.Register(this);");
            AssertTextBefore(atlasRegister, "if (_runtimeOwnerAborted || _atlas6EventRegistered)", "Atlas6Events.Register(this);");
            AssertTextBefore(barter, "if (_runtimeOwnerAborted)", "int safeCount = BarterTransactionCount;");
            AssertTextBefore(narrativeEvent, "if (_runtimeOwnerAborted)", "if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)");
            AssertTextBefore(atlasEvent, "if (_runtimeOwnerAborted)", "Atlas6EventType eventType = (Atlas6EventType)payload.EventType;");
            AssertTextBefore(resolvePlayer, "if (_runtimeOwnerAborted)", "_playerMovement = null;");
            AssertTextBefore(resolveAup, "if (_runtimeOwnerAborted)", "IPlayerRuntimeContext playerContext = _playerRuntimeContext;");
            StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", resolveAup);
            StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u", resolveAup);
            StringAssert.Contains("playerAup = movementState.PredictedAup;", resolveAup);
            StringAssert.DoesNotContain("_playerMovement.CurrentAup", resolveAup);
            AssertTextBefore(save, "if (_runtimeOwnerAborted || data == null)", "data.atlas6PlayerStatus = (int)_playerStatus;");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || data == null)", "_playerStatus = SanitizePlayerStatus(data.atlas6PlayerStatus);");

            StringAssert.Contains("Atlas6DirectiveSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Atlas6Directive;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsAtlas6DirectiveRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterAtlas6DirectiveRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregister();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterNarrativeEvents();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterAtlas6Events();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("ClearAtlasDependencies();", abort);
            StringAssert.Contains("ClearQueuedNotifications();", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_lateFrameRegistered = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_saveRegistered = false;", abort);
            StringAssert.Contains("_narrativeEventRegistered = false;", abort);
            StringAssert.Contains("_atlas6EventRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("!ReferenceEquals(system, null)", usable);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("!system._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registeredRuntime != null && registeredRuntime != this", source);
        }

        [Test]
        public void Atlas6DirectiveSystem_QueuedNotificationDropsAndPushRefusalsStayVisible()
        {
            string source = ReadScript("AtlasSignal", "Atlas6DirectiveSystem.cs");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string tryPush = ExtractMethodBody(source, "private void TryPushQueuedNotification(");
            string drop = ExtractMethodBody(source, "private void ReportNotificationQueueDrop(");
            string miss = ExtractMethodBody(source, "private void ReportNotificationPushMiss(");
            string clear = ExtractMethodBody(source, "private void ClearQueuedNotifications()");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _NotificationQueueDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationPushMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationContextHash", source);
            StringAssert.Contains("public int NotificationQueueDropCount =>", source);
            StringAssert.Contains("public int NotificationPushMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                queue,
                "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;",
                "if (_pendingNotificationCount >= PendingNotificationCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;"));
            StringAssert.Contains("TryPushQueuedNotification(messageHash, severity);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredCritical(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(messageHash);", flush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredWarning(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredCritical(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredInfo(messageHash);", tryPush);
            StringAssert.Contains("ReportNotificationPushMiss(messageHash);", tryPush);
            StringAssert.Contains("_notificationQueueDropCount++;", drop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", drop);
            StringAssert.Contains("_NotificationQueueDropWarningHash", drop);
            StringAssert.Contains("math.max(1, _notificationQueueDropCount)", drop);
            StringAssert.Contains("_notificationPushMissCount++;", miss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", miss);
            StringAssert.Contains("_NotificationPushMissWarningHash", miss);
            StringAssert.Contains("math.max(1, _notificationPushMissCount)", miss);
            StringAssert.Contains("_notificationQueueDropCount = 0;", clear);
            StringAssert.Contains("_notificationPushMissCount = 0;", clear);
            StringAssert.Contains("ClearQueuedNotifications();", load);
            StringAssert.DoesNotContain("_notificationQueueDropCount", save);
            StringAssert.DoesNotContain("_notificationPushMissCount", save);
            StringAssert.DoesNotContain("_notificationQueueDropCount", load);
            StringAssert.DoesNotContain("_notificationPushMissCount", load);
        }

        [Test]
        public void FirstHourDirector_RuntimeOwnerGateClaimsServiceBeforeRouteEventsSaveAndTicks()
        {
            string source = ReadScript("Gameplay", "FirstHourDirector.cs");
            string routeContact = ExtractMethodBody(source, "public void RegisterServiceRelayRouteContact()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegister()");
            string registerLateTick = ExtractMethodBody(source, "private void TryRegisterLateFrameTick()");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterService()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(source, "private void CacheRuntimeServices()");
            string cacheAudio = ExtractMethodBody(source, "private void CacheAudioLogSystem(");
            string cachePlayer = ExtractMethodBody(source, "private void CachePlayerContext(");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string registerEvents = ExtractMethodBody(source, "private void TryRegisterRuntimeEventListeners()");
            string unregisterEvents = ExtractMethodBody(source, "private void TryUnregisterRuntimeEventListeners()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string narrativeEvent = ExtractMethodBody(source, "public void OnNarrativeEvent(");
            string scanEvent = ExtractMethodBody(source, "public void OnScanEvent(");
            string questEvent = ExtractMethodBody(source, "public void OnQuestEvent(");
            string audioEvent = ExtractMethodBody(source, "public void OnAudioLogEvent(");
            string craftingEvent = ExtractMethodBody(source, "public void OnCraftingEvent(");
            string interactionEvent = ExtractMethodBody(source, "public void OnInteractionEvent(");
            string resolveSurvival = ExtractMethodBody(source, "private bool ResolveSurvivalSystem()");
            string resolveWorld = ExtractMethodBody(source, "private void ResolveWorldContext(");
            string synchronize = ExtractMethodBody(source, "private void SynchronizeContextFromRuntimeSystems()");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsFirstHourRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("private bool _craftingEventRegistered;", source);
            StringAssert.Contains("private bool _audioLogEventRegistered;", source);
            AssertTextBefore(routeContact, "if (_runtimeOwnerAborted)", "_hasLoreRouteContact = true;");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRuntimeServices();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterRuntimeEventListeners();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || !TryRegisterService())", "TryRegister();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregister();");
            StringAssert.Contains("TryUnregisterRuntimeEventListeners();", onDisable);
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregister();");
            StringAssert.Contains("TryUnregisterRuntimeEventListeners();", onDestroy);
            StringAssert.Contains("ClearCachedRuntimeServices();", onDestroy);

            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "ConsumeCraftingCompletedSignals();");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted || _registered)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(registerLateTick, "if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);");
            AssertTextBefore(queue, "if (_runtimeOwnerAborted)", "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)");
            AssertTextBefore(flush, "if (_runtimeOwnerAborted)", "int count = _pendingNotificationCount;");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "FirstHourDirector registeredRuntime = GlobalRegistry.FirstHour;");
            StringAssert.Contains("if (IsFirstHourRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterFirstHourRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterFirstHourRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.FirstHour, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);
            StringAssert.DoesNotContain("Destroy(gameObject);", registerService);

            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "_cachedQuestManager = GlobalRegistry.QuestSystem;");
            AssertTextBefore(cacheAudio, "if (_runtimeOwnerAborted)", "_cachedAudioLogSystem = IsAudioLogRuntimeUsable(audioLogSystem) ? audioLogSystem : null;");
            AssertTextBefore(cachePlayer, "if (_runtimeOwnerAborted)", "_cachedPlayerContext = playerContext;");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");

            AssertTextBefore(registerEvents, "if (_runtimeOwnerAborted)", "CraftingEvents.Register(this);");
            StringAssert.Contains("_craftingEventRegistered = true;", registerEvents);
            StringAssert.Contains("_narrativeEventRegistered = true;", registerEvents);
            StringAssert.Contains("_questEventRegistered = true;", registerEvents);
            StringAssert.Contains("_scanEventRegistered = true;", registerEvents);
            StringAssert.Contains("_interactionEventRegistered = true;", registerEvents);
            StringAssert.Contains("_audioLogEventRegistered = true;", registerEvents);
            StringAssert.Contains("CraftingEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("NarrativeEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("QuestEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("ScanEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("InteractionEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("AudioLogEvents.Unregister(this);", unregisterEvents);

            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "if (IsFirstHourComplete) return;");
            AssertTextBefore(narrativeEvent, "if (_runtimeOwnerAborted)", "if ((NarrativeEventType)payload.EventType != NarrativeEventType.DiscoveryMade)");
            AssertTextBefore(scanEvent, "if (_runtimeOwnerAborted)", "if ((ScanEventType)payload.EventType != ScanEventType.EntryDiscovered");
            AssertTextBefore(questEvent, "if (_runtimeOwnerAborted)", "if ((QuestEventType)payload.EventType != QuestEventType.Completed)");
            AssertTextBefore(audioEvent, "if (_runtimeOwnerAborted)", "if (payload.Type == AudioLogEventType.Discovered && payload.LogHash != 0u)");
            AssertTextBefore(craftingEvent, "if (_runtimeOwnerAborted)", "if ((CraftingEventType)payload.EventType != CraftingEventType.CraftCompleted)");
            AssertTextBefore(interactionEvent, "if (_runtimeOwnerAborted)", "if ((InteractionEventType)payload.EventType != InteractionEventType.ItemCollected)");
            AssertTextBefore(resolveSurvival, "if (_runtimeOwnerAborted)", "if (_survivalSystem != null)");
            AssertTextBefore(resolveWorld, "if (_runtimeOwnerAborted)", "if (force || _worldZoneDirector == null)");
            AssertTextBefore(synchronize, "if (_runtimeOwnerAborted)", "IAudioLogRuntime audioLogSystem = ResolveAudioLogSystem();");
            AssertTextBefore(save, "if (_runtimeOwnerAborted || data == null)", "data.firstHourSessionTime = _sessionTime;");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || data == null)", "_sessionTime          = data.firstHourSessionTime;");

            StringAssert.Contains("FirstHourDirector registeredRuntime = GlobalRegistry.FirstHour;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsFirstHourRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterFirstHourRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregister();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterRuntimeEventListeners();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("ClearCachedRuntimeServices();", abort);
            StringAssert.Contains("ClearQueuedNotifications();", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_lateFrameRegistered = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_saveRegistered = false;", abort);
            StringAssert.Contains("_craftingEventRegistered = false;", abort);
            StringAssert.Contains("_audioLogEventRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("!ReferenceEquals(director, null)", usable);
            StringAssert.Contains("director._serviceRegistered", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            StringAssert.Contains("!director._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registeredRuntime != null && !ReferenceEquals(registeredRuntime, this)", source);
        }

        [Test]
        public void FirstHourDirector_QueuedNotificationDropsAndPushRefusalsStayVisible()
        {
            string source = ReadScript("Gameplay", "FirstHourDirector.cs");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string tryPush = ExtractMethodBody(source, "private void TryPushQueuedNotification(");
            string drop = ExtractMethodBody(source, "private void ReportNotificationQueueDrop(");
            string miss = ExtractMethodBody(source, "private void ReportNotificationPushMiss(");
            string clear = ExtractMethodBody(source, "private void ClearQueuedNotifications()");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _NotificationQueueDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationPushMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationContextHash", source);
            StringAssert.Contains("public int NotificationQueueDropCount =>", source);
            StringAssert.Contains("public int NotificationPushMissCount =>", source);
            StringAssert.Contains("ReportNotificationQueueDrop((uint)severity);", queue);
            Assert.IsTrue(ContainsTokensInOrder(
                queue,
                "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;",
                "if (_pendingNotificationCount >= PendingNotificationCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;"));
            StringAssert.Contains("TryPushQueuedNotification(messageHash, severity);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredCritical(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(messageHash);", flush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredWarning(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredCritical(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredInfo(messageHash);", tryPush);
            StringAssert.Contains("ReportNotificationPushMiss(messageHash);", tryPush);
            StringAssert.Contains("_notificationQueueDropCount++;", drop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", drop);
            StringAssert.Contains("_NotificationQueueDropWarningHash", drop);
            StringAssert.Contains("math.max(1, _notificationQueueDropCount)", drop);
            StringAssert.Contains("_notificationPushMissCount++;", miss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", miss);
            StringAssert.Contains("_NotificationPushMissWarningHash", miss);
            StringAssert.Contains("math.max(1, _notificationPushMissCount)", miss);
            StringAssert.Contains("_notificationQueueDropCount = 0;", clear);
            StringAssert.Contains("_notificationPushMissCount = 0;", clear);
            StringAssert.Contains("ClearQueuedNotifications();", load);
            StringAssert.DoesNotContain("_notificationQueueDropCount", save);
            StringAssert.DoesNotContain("_notificationPushMissCount", save);
            StringAssert.DoesNotContain("_notificationQueueDropCount", load);
            StringAssert.DoesNotContain("_notificationPushMissCount", load);
        }

        [Test]
        public void EndingSystem_RuntimeOwnerGateClaimsServiceBeforeEndingEventsSaveAndPresentation()
        {
            string source = ReadScript("Gameplay", "EndingSystem.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string registerSlowTick = ExtractMethodBody(source, "private void TryRegister()");
            string registerLateTick = ExtractMethodBody(source, "private void TryRegisterLateFrameTick()");
            string queueSignal = ExtractMethodBody(source, "private void QueueAtlasSignalStrength(");
            string flushSignal = ExtractMethodBody(source, "private void FlushQueuedAtlasSignalStrength()");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterService()");
            string cache = ExtractMethodBody(source, "private void CacheRuntimeDependencies()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string registerEvents = ExtractMethodBody(source, "private void TryRegisterAtlasSignalEvents()");
            string unregisterEvents = ExtractMethodBody(source, "private void TryUnregisterAtlasSignalEvents()");
            string force = ExtractMethodBody(source, "public void ForceConditionMetFromQuestDAG()");
            string choose = ExtractMethodBody(source, "public void ChooseEnding(EndingChoice choice)");
            string resolve = ExtractMethodBody(source, "private bool ResolveSurvivalSystem()");
            string atlasEvent = ExtractMethodBody(source, "public void OnAtlasSignalEvent(");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsEndingRuntimeUsable(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("private bool _atlasSignalEventRegistered;", source);
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegister();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRuntimeDependencies();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterSaveParticipant();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterAtlasSignalEvents();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            StringAssert.Contains("TryUnregisterAtlasSignalEvents();", onDisable);
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            StringAssert.Contains("TryUnregisterAtlasSignalEvents();", onDestroy);
            StringAssert.Contains("ClearRuntimeDependencies();", onDestroy);

            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "if (_conditionMet || _endingComplete) return;");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "FlushQueuedAtlasSignalStrength();");
            AssertTextBefore(registerSlowTick, "if (_runtimeOwnerAborted || _registered)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(registerLateTick, "if (_runtimeOwnerAborted || _lateFrameRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);");
            AssertTextBefore(queueSignal, "if (_runtimeOwnerAborted)", "_pendingAtlasSignalStrength = Mathf.Clamp01(strength01);");
            AssertTextBefore(flushSignal, "if (_runtimeOwnerAborted)", "if (!_pendingAtlasSignalStrengthDirty)");
            AssertTextBefore(queue, "if (_runtimeOwnerAborted)", "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)");
            AssertTextBefore(flush, "if (_runtimeOwnerAborted)", "int count = _pendingNotificationCount;");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "EndingSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Ending;");
            StringAssert.Contains("if (IsEndingRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.RegisterEndingRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(Hecton8.Core.GlobalRegistry.Ending, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);
            StringAssert.DoesNotContain("Destroy(gameObject);", registerService);

            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "_atlasSignal = Hecton8.Core.GlobalRegistry.AtlasSignalReadModel;");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            AssertTextBefore(registerEvents, "if (_runtimeOwnerAborted || _atlasSignalEventRegistered)", "AtlasSignalEvents.Register(this);");
            StringAssert.Contains("_atlasSignalEventRegistered = true;", registerEvents);
            StringAssert.Contains("AtlasSignalEvents.Unregister(this);", unregisterEvents);
            StringAssert.Contains("_atlasSignalEventRegistered = false;", unregisterEvents);
            AssertTextBefore(force, "if (_runtimeOwnerAborted)", "if (_conditionMet || _endingComplete)");
            AssertTextBefore(choose, "if (_runtimeOwnerAborted)", "if (!CanChooseEnding)");
            AssertTextBefore(resolve, "if (_runtimeOwnerAborted)", "if (_survivalSystem != null)");
            AssertTextBefore(atlasEvent, "if (_runtimeOwnerAborted)", "if ((AtlasSignalEventType)payload.EventType == AtlasSignalEventType.Decoded)");
            AssertTextBefore(save, "if (_runtimeOwnerAborted || data == null)", "EndingChoice safeChoice = SanitizeEndingChoice((int)_chosenEnding);");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || data == null)", "_chosenEnding = SanitizeEndingChoice(data.endingChoice);");

            StringAssert.Contains("EndingSystem registeredRuntime = Hecton8.Core.GlobalRegistry.Ending;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsEndingRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("Hecton8.Core.GlobalRegistry.UnregisterEndingRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterAtlasSignalEvents();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("ClearRuntimeDependencies();", abort);
            StringAssert.Contains("ClearQueuedPresentation();", abort);
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_lateFrameRegistered = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_saveRegistered = false;", abort);
            StringAssert.Contains("_atlasSignalEventRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("!ReferenceEquals(system, null)", usable);
            StringAssert.Contains("system._serviceRegistered", usable);
            StringAssert.Contains("system.isActiveAndEnabled", usable);
            StringAssert.Contains("!system._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registeredRuntime != null && !ReferenceEquals(registeredRuntime, this)", source);
        }

        [Test]
        public void EndingSystem_QueuedNotificationDropsAndPushRefusalsStayVisible()
        {
            string source = ReadScript("Gameplay", "EndingSystem.cs");
            string queue = ExtractMethodBody(source, "private unsafe bool QueueNotification(");
            string flush = ExtractMethodBody(source, "private unsafe void FlushQueuedNotifications()");
            string tryPush = ExtractMethodBody(source, "private void TryPushQueuedNotification(");
            string drop = ExtractMethodBody(source, "private void ReportNotificationQueueDrop(");
            string miss = ExtractMethodBody(source, "private void ReportNotificationPushMiss(");
            string clear = ExtractMethodBody(source, "private void ClearQueuedPresentation()");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _NotificationQueueDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationPushMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _NotificationContextHash", source);
            StringAssert.Contains("public int NotificationQueueDropCount =>", source);
            StringAssert.Contains("public int NotificationPushMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                queue,
                "if (message.IsEmpty || message.Length > PendingNotificationCharCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;",
                "if (_pendingNotificationCount >= PendingNotificationCapacity)",
                "ReportNotificationQueueDrop((uint)severity);",
                "return false;"));
            StringAssert.Contains("TryPushQueuedNotification(messageHash, severity);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredCritical(messageHash);", flush);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(messageHash);", flush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredWarning(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredCritical(messageHash);", tryPush);
            StringAssert.Contains("pushed = NotificationEvents.TryPushRegisteredInfo(messageHash);", tryPush);
            StringAssert.Contains("ReportNotificationPushMiss(messageHash);", tryPush);
            StringAssert.Contains("_notificationQueueDropCount++;", drop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", drop);
            StringAssert.Contains("_NotificationQueueDropWarningHash", drop);
            StringAssert.Contains("math.max(1, _notificationQueueDropCount)", drop);
            StringAssert.Contains("_notificationPushMissCount++;", miss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", miss);
            StringAssert.Contains("_NotificationPushMissWarningHash", miss);
            StringAssert.Contains("math.max(1, _notificationPushMissCount)", miss);
            StringAssert.Contains("_notificationQueueDropCount = 0;", clear);
            StringAssert.Contains("_notificationPushMissCount = 0;", clear);
            StringAssert.Contains("ClearQueuedPresentation();", load);
            StringAssert.DoesNotContain("_notificationQueueDropCount", save);
            StringAssert.DoesNotContain("_notificationPushMissCount", save);
            StringAssert.DoesNotContain("_notificationQueueDropCount", load);
            StringAssert.DoesNotContain("_notificationPushMissCount", load);
        }

        [Test]
        public void ScavengePopulator_RuntimeOwnerGateClaimsServiceBeforeSpawnTicksAndDirectApi()
        {
            string source = ReadScript(string.Empty, "ScavengePopulator.cs");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string cache = ExtractMethodBody(source, "private void CacheRegistryServicesCold()");
            string cachePool = ExtractMethodBody(source, "private void CacheObjectPoolService(");
            string resolvePool = ExtractMethodBody(source, "private bool TryResolveCachedObjectPool(");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsScavengePopulatorRuntimeUsable(");
            string registerSpawn = ExtractMethodBody(source, "public void RegisterSpawnPoint(");
            string prepareChunk = ExtractMethodBody(source, "public void PrepareChunkForReload(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string processSpawn = ExtractMethodBody(source, "private void ProcessSpawnQueue()");
            string reservePresentation = ExtractMethodBody(source, "private bool TryReservePendingPresentationOperation(");
            string flushPresentation = ExtractMethodBody(source, "private void FlushPendingPresentationOperations()");
            string cull = ExtractMethodBody(source, "private void CullDistantChunks()");
            string findPlayer = ExtractMethodBody(source, "private void FindPlayer()");
            string setBudget = ExtractMethodBody(source, "public void SetRuntimeBudget(");
            string reload = ExtractMethodBody(source, "public void ReloadChunk(");
            string unload = ExtractMethodBody(source, "public void UnloadAll()");
            string highlight = ExtractMethodBody(source, "public void HighlightNearbyResource(");
            string setProfile = ExtractMethodBody(source, "public void SetChunkStreamingProfile(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("public bool IsServiceReady => _initialized && !_runtimeOwnerAborted && !_isDuplicateInstance && _serviceRegistered && enabled;", source);
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)", "if (!TryRegisterService())");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);");
            StringAssert.DoesNotContain("GlobalRegistry.RegisterScavengePopulatorRuntime(this);", onEnable);
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)", "GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            StringAssert.Contains("CacheObjectPoolService(currentService as ObjectPoolManager);", replaced);
            AssertTextBefore(cache, "if (_runtimeOwnerAborted)", "CacheObjectPoolService(null);");
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate)", cachePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref pool)", cachePool);
            StringAssert.Contains("ObjectPoolManager cached = _objectPool as ObjectPoolManager;", resolvePool);
            StringAssert.Contains("ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached)", resolvePool);
            StringAssert.Contains("ObjectPoolManager.TryResolveActiveRuntime(ref resolved)", resolvePool);
            StringAssert.Contains("_objectPool = null;", resolvePool);
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "ScavengePopulator registeredRuntime = GlobalRegistry.ScavengePopulator;");
            StringAssert.Contains("if (IsScavengePopulatorRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterScavengePopulatorRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterScavengePopulatorRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.ScavengePopulator, this);", registerService);
            StringAssert.Contains("return _serviceRegistered;", registerService);

            StringAssert.Contains("ScavengePopulator registeredRuntime = GlobalRegistry.ScavengePopulator;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsScavengePopulatorRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterScavengePopulatorRuntime(registeredRuntime);", gate);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_isDuplicateInstance = true;", abort);
            StringAssert.Contains("_registeredToSlowTickManager = false;", abort);
            StringAssert.Contains("_registeredToLateFrame = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_pendingScavengeVisualSync = false;", abort);
            StringAssert.Contains("ClearPendingPresentationOperations();", abort);
            StringAssert.Contains("ClearCachedRegistryServices();", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.DoesNotContain("Destroy(", abort);

            StringAssert.Contains("populator._serviceRegistered", usable);
            StringAssert.Contains("populator.isActiveAndEnabled", usable);
            StringAssert.Contains("!populator._runtimeOwnerAborted", usable);
            StringAssert.Contains("!populator._isDuplicateInstance", usable);

            AssertTextBefore(registerSpawn, "if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)", "EnsureChunk(chunkCoord, 256);");
            AssertTextBefore(prepareChunk, "if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)", "if (_chunks.TryGetValue(chunkCoord, out ChunkData existing))");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized || _spawnQueue == null || _chunks == null || _chunksToUnload == null)", "RefreshRuntimeStreamingSettings();");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "if (!_pendingScavengeVisualSync && _pendingPresentationOperationCount == 0)");
            AssertTextBefore(processSpawn, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "if (_spawnQueue.Count == 0) return;");
            StringAssert.Contains("if (!TryResolveCachedObjectPool(out IObjectPoolService pool)) return;", processSpawn);
            StringAssert.DoesNotContain("IObjectPoolService pool = _objectPool;", processSpawn);
            AssertTextBefore(reservePresentation, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "index = _pendingPresentationOperationCount;");
            StringAssert.Contains("TryResolveCachedObjectPool(out IObjectPoolService pool);", flushPresentation);
            AssertTextBefore(cull, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "if (_playerTransform == null)");
            AssertTextBefore(findPlayer, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;");
            AssertTextBefore(setBudget, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "unloadDistance = Mathf.Max(50f, newUnloadDistance);");
            AssertTextBefore(reload, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "DespawnChunk(coord);");
            AssertTextBefore(unload, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "DespawnAllChunks();");
            AssertTextBefore(highlight, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "float bestDistSqr");
            AssertTextBefore(setProfile, "if (_runtimeOwnerAborted || _isDuplicateInstance)", "chunkStreamingProfile = profile;");
            StringAssert.DoesNotContain("activeRuntime != null && !ReferenceEquals(activeRuntime, this)", source);
        }

        [Test]
        public void CameraJuiceSystem_RuntimeOwnerGateClaimsServiceBeforeTelemetryBuffersTicksAndPhysics()
        {
            string source = ReadScript("VFX", "CameraJuiceSystem.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string registerDispatcher = ExtractMethodBody(source, "private void TryRegisterDispatcherTicks()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterToGlobalRegistry()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsCameraJuiceRuntimeUsable(");
            string registerLate = ExtractMethodBody(source, "private void TryRegisterLateFrame()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string rebound = ExtractMethodBody(source, "void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(");
            string replaced = ExtractMethodBody(source, "void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(");
            string refresh = ExtractMethodBody(source, "private void RefreshCachedRegistryServices()");
            string applyRebind = ExtractMethodBody(source, "private void ApplyRegistryServiceRebind(");
            string physicsRegister = ExtractMethodBody(source, "private void TryRegisterPhysicsImpactListener()");
            string physicsRebind = ExtractMethodBody(source, "private void RebindPhysicsStateEventService(");
            string physicsUsable = ExtractMethodBody(source, "private static bool IsPhysicsStateEventServiceUsable(");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string save = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string pause = ExtractMethodBody(source, "public void ApplyPauseDepthOfFieldWeight(");
            string reclaim = ExtractMethodBody(source, "public void BeginInputReclaimFov(");
            string shake = ExtractMethodBody(source, "public void TriggerShake(");
            string impactShake = ExtractMethodBody(source, "public void TriggerSubmarineImpactShake(");
            string fov = ExtractMethodBody(source, "public void TriggerFOVKick(");
            string biome = ExtractMethodBody(source, "public void TransitionToBiome(");
            string interactionEvent = ExtractMethodBody(source, "public void OnInteractionEvent(");
            string physicsEvent = ExtractMethodBody(source, "void IPhysicsImpactEventListener.OnPhysicsImpact(");
            string recover = ExtractMethodBody(source, "private void RecoverCameraJuiceVaultBindings()");
            string ensureTelemetry = ExtractMethodBody(source, "private bool EnsureCameraJuiceTelemetry()");
            string ensureSpeedLines = ExtractMethodBody(source, "private void EnsureCameraSpeedLineParticles()");
            string resolveSpeed = ExtractMethodBody(source, "private float ResolveCurrentCameraSpeed()");
            string resolveDeps = ExtractMethodBody(source, "private void TryResolveGameplayDependencies()");
            string refreshDeps = ExtractMethodBody(source, "private void RefreshGameplayDependenciesFromCachedRuntime()");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "RefreshCachedRegistryServices();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "EnsureCameraJuiceTelemetry();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "EnsureProceduralCameraJuiceBuffers();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "EnsureCameraSpeedLineParticles();");
            AssertTextBefore(awake, "TryUnregister();", "enabled = false;");
            AssertTextBefore(awake, "TryUnregisterFromGlobalRegistry();", "enabled = false;");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "CameraJuiceSignals.EnsurePrewarmed();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterDispatcherTicks();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterLateFrame();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "EnsureProceduralCameraJuiceBuffers();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "InteractionEvents.Register(this);");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterPhysicsImpactListener();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregister();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregister();");

            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered || !Application.isPlaying)");
            StringAssert.Contains("return true;", registerService);
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;");
            StringAssert.Contains("if (IsCameraJuiceRuntimeUsable(registeredRuntime))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterCameraJuiceRuntime(registeredRuntime);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterCameraJuiceRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.CameraJuice, this);", registerService);
            StringAssert.Contains("return true;", registerService);

            StringAssert.Contains("ICameraJuiceSystem registeredRuntime = GlobalRegistry.CameraJuice;", gate);
            StringAssert.Contains("if (ReferenceEquals(registeredRuntime, null) || ReferenceEquals(registeredRuntime, this))", gate);
            StringAssert.Contains("if (IsCameraJuiceRuntimeUsable(registeredRuntime))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterCameraJuiceRuntime(registeredRuntime);", gate);

            AssertTextBefore(abort, "TryUnregister();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "InteractionEvents.Unregister(this);", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterPhysicsImpactListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "ReleaseProceduralCameraJuiceBuffers();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "ReleaseCameraJuiceTelemetry();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_registered = false;", abort);
            StringAssert.Contains("_registeredLateFrame = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_physicsImpactRegistered = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("CameraJuiceSystem runtime = service as CameraJuiceSystem;", usable);
            StringAssert.Contains("runtime._serviceRegistered", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);

            AssertTextBefore(registerDispatcher, "if (_runtimeOwnerAborted || _registered || !Application.isPlaying || _dispatcher == null)", "GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);");
            AssertTextBefore(registerLate, "if (_runtimeOwnerAborted || _registeredLateFrame || !Application.isPlaying || _dispatcher == null)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(rebound, "if (_runtimeOwnerAborted)", "ApplyRegistryServiceRebind(serviceSlot, currentService);");
            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "ApplyRegistryServiceRebind(serviceSlot, currentService);");
            AssertTextBefore(refresh, "if (_runtimeOwnerAborted)", "ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);");
            AssertTextBefore(applyRebind, "if (_runtimeOwnerAborted)", "switch (serviceSlot)");
            AssertTextBefore(physicsRegister, "if (_runtimeOwnerAborted || _physicsImpactRegistered)", "RebindPhysicsStateEventService(GlobalRegistry.PhysicsStateEvents);");
            AssertTextBefore(physicsRebind, "if (_runtimeOwnerAborted)", "if (ReferenceEquals(_physicsStateEvents, physicsStateEvents) && _physicsImpactRegistered)");
            AssertTextBefore(physicsRebind, "!IsPhysicsStateEventServiceUsable(_physicsStateEvents)", "_physicsStateEvents.RegisterImpactListener(this);");
            StringAssert.Contains("return physicsStateEvents != null && physicsStateEvents.IsInitialized;", physicsUsable);
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "AdvanceCameraJuicePresentation(SystemDispatcher.CurrentFrameDeltaTime);");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "RefreshCachedPresentationMotionScale();");

            AssertTextBefore(save, "if (_runtimeOwnerAborted || data == null)", "// CameraJuiceSystem settings stored as public fields in SaveData");
            AssertTextBefore(load, "if (_runtimeOwnerAborted || data == null)", "// Load settings from SaveData public fields");
            AssertTextBefore(pause, "if (_runtimeOwnerAborted)", "_pauseDepthOfFieldWeight = math.saturate(weight);");
            AssertTextBefore(reclaim, "if (_runtimeOwnerAborted || !_fovEnabled || _mainCamera == null || HectonXRRuntimeState.IsXRActive)", "_inputReclaimFovStart = math.clamp(startFov, MIN_FOV, MAX_FOV);");
            AssertTextBefore(shake, "if (_runtimeOwnerAborted)", "if (profile == null)");
            AssertTextBefore(impactShake, "if (_runtimeOwnerAborted || !_shakeEnabled)", "float safeSeverity = math.saturate(severity01);");
            AssertTextBefore(fov, "if (_runtimeOwnerAborted || !_fovEnabled || HectonXRRuntimeState.IsXRActive)", "_fovBlendStart = _currentFOVOffset;");
            AssertTextBefore(biome, "if (_runtimeOwnerAborted)", "if (biome == null)");
            AssertTextBefore(interactionEvent, "if (_runtimeOwnerAborted)", "if ((InteractionEventType)payload.EventType != InteractionEventType.HoverChanged)");
            AssertTextBefore(physicsEvent, "if (_runtimeOwnerAborted || !_shakeEnabled)", "float severity = ResolvePhysicsImpactSeverity(in impactSignal);");
            AssertTextBefore(recover, "if (_runtimeOwnerAborted)", "IDataVault vault = _dataVault;");
            AssertTextBefore(ensureTelemetry, "if (_runtimeOwnerAborted)", "if (!ValidateCameraJuiceTelemetryLayout())");
            AssertTextBefore(ensureSpeedLines, "if (_runtimeOwnerAborted)", "if (_speedLineParticles != null || _cameraTransform == null)");
            AssertTextBefore(resolveSpeed, "if (_runtimeOwnerAborted)", "float speed = 0f;");
            AssertTextBefore(resolveDeps, "if (_runtimeOwnerAborted)", "_submarineHullRigidbody = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;");
            AssertTextBefore(refreshDeps, "if (_runtimeOwnerAborted)", "_submarineHullRigidbody = _submarineRuntimeContext != null ? _submarineRuntimeContext.HullRigidbody : null;");
            StringAssert.DoesNotContain("registeredRuntime != null && !ReferenceEquals(registeredRuntime, this)", source);
        }

        [Test]
        public void HectonMusicDirector_RuntimeOwnerGateClaimsServiceBeforeVoicesSynthAndTicks()
        {
            string source = ReadScript("Audio", "HectonMusicDirector.cs");
            string ensure = ExtractMethodBody(source, "private static void EnsureRuntimeInstance()");
            string ensureScene = ExtractMethodBody(source, "internal static void EnsureRuntimeInstanceForScene(");
            string resolver = ExtractMethodBody(source, "private static HectonMusicDirector ResolveUsableRuntime()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string replaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string tick = ExtractMethodBody(source, "public void Tick(");
            string lateTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string runTick = ExtractMethodBody(source, "private void RunMusicTick(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string runSlow = ExtractMethodBody(source, "private void RunMusicSlowTick()");
            string setManual = ExtractMethodBody(source, "public void SetManualBiomeProfile(");
            string setMatrix = ExtractMethodBody(source, "public void SetMatrixBiomeProfile(");
            string setTier = ExtractMethodBody(source, "public void SetSoundscapeTierContext(");
            string clearManual = ExtractMethodBody(source, "public void ClearManualBiomeProfile()");
            string setTension = ExtractMethodBody(source, "public void SetManualTension01(");
            string clearTension = ExtractMethodBody(source, "public void ClearManualTensionOverride()");
            string forceOverride = ExtractMethodBody(source, "public void ForceOverrideTrack(");
            string clearOverride = ExtractMethodBody(source, "public void ClearForcedOverride(");
            string discovery = ExtractMethodBody(source, "public void PlayDiscoveryStinger()");
            string danger = ExtractMethodBody(source, "public void PlayDangerStinger()");
            string recovery = ExtractMethodBody(source, "public void PlayRecoveryStinger()");
            string stop = ExtractMethodBody(source, "public void StopMusic(");
            string registerTicks = ExtractMethodBody(source, "private void TryRegisterTickHandlers()");
            string registerLate = ExtractMethodBody(source, "private void TryRegisterLateFrameTick()");
            string hotSwap = ExtractMethodBody(source, "private void TryRegisterHotSwapListener()");
            string registerService = ExtractMethodBody(source, "private bool TryRegisterToGlobalRegistry()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string usable = ExtractMethodBody(source, "private static bool IsMusicDirectorRuntimeUsable(");
            string bindVoice = ExtractMethodBody(source, "private void BindAuthoredVoicePool()");
            string resolveVoice = ExtractMethodBody(source, "private void ResolveVoicePool()");
            string rebind = ExtractMethodBody(source, "private void CacheReboundRuntimeService(");
            string refresh = ExtractMethodBody(source, "private void RefreshCachedRuntimeServicesCold()");
            string resolveCold = ExtractMethodBody(source, "private void ResolveDependenciesCold()");
            string worldListener = ExtractMethodBody(source, "private void TryRegisterWorldZoneDirectorListenerCold()");
            string worldChanged = ExtractMethodBody(source, "private void HandleWorldZoneDirectorChanged(");
            string cacheWorld = ExtractMethodBody(source, "private void CacheRuntimeWorldZoneDirectorCold(");
            string resolveScene = ExtractMethodBody(source, "private void ResolveDependenciesForSceneCold(");
            string resolveDeps = ExtractMethodBody(source, "private void ResolveDependencies()");
            string acoustic = ExtractMethodBody(source, "private void HandleAcousticZoneChanged(");
            string matrixChanged = ExtractMethodBody(source, "private void HandleMatrixBiomeChanged(");
            string depthTier = ExtractMethodBody(source, "private void HandleDepthTierChanged(");
            string depthEntered = ExtractMethodBody(source, "private void HandleDepthZoneEntered(");
            string activeScene = ExtractMethodBody(source, "private void HandleActiveSceneChanged(");

            StringAssert.Contains("private bool _runtimeOwnerAborted;", source);
            StringAssert.Contains("public HectonMusicBiomeProfile ActiveResolvedProfile => _runtimeOwnerAborted ? null : _resolvedProfile;", source);
            StringAssert.Contains("public bool IsOverrideActive => !_runtimeOwnerAborted && _overrideActive;", source);
            StringAssert.Contains("public float CurrentMusicActivity01 => _runtimeOwnerAborted ? 0f : math.saturate(_proceduralMusicActivity01);", source);
            StringAssert.Contains("public MusicActivityReason CurrentMusicActivityReason => _runtimeOwnerAborted ? MusicActivityReason.Silent : _musicActivityReason;", source);
            StringAssert.Contains("public float CurrentSoundscapePressure01 => _runtimeOwnerAborted ? 0f : ResolveSoundscapePressure01(_currentSoundscapeTier);", source);
            StringAssert.Contains("public AudioMixerGroup CurrentMusicMixerGroup => _runtimeOwnerAborted ? null : ResolveMusicMixerGroup();", source);
            AssertTextBefore(ensure, "ResolveUsableRuntime() != null", "TryInstantiateConfiguredRuntimeDirector(SceneManager.GetActiveScene(), false);");
            AssertTextBefore(ensureScene, "ResolveUsableRuntime() != null", "TryInstantiateConfiguredRuntimeDirector(scene, true);");
            StringAssert.Contains("HectonMusicDirector registered = GlobalRegistry.MusicDirector;", resolver);
            StringAssert.Contains("if (IsMusicDirectorRuntimeUsable(registered))", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterMusicDirectorRuntime(registered);", resolver);
            StringAssert.Contains("HectonMusicDirector active = s_activeRuntimeInstance;", resolver);
            StringAssert.Contains("GlobalRegistry.RegisterMusicDirectorRuntime(active);", resolver);

            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "_musicSources = new AudioSource[MusicVoiceCount];");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "EnsureProceduralSynthRuntime();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "BindAuthoredVoicePool();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterToGlobalRegistry())", "ResolveDependenciesCold();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterTickHandlers();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "TryRegisterTickHandlers();");
            AssertTextBefore(start, "if (_runtimeOwnerAborted || !TryRegisterToGlobalRegistry())", "ResolveDependenciesCold();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "StopMusicInternal(0f);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "StopMusicInternal(0f);");

            AssertTextBefore(replaced, "if (_runtimeOwnerAborted)", "CacheReboundRuntimeService(serviceSlot, previousService, currentService);");
            AssertTextBefore(tick, "if (_runtimeOwnerAborted)", "_pendingMusicTickDeltaTime += math.max(0f, deltaTime);");
            AssertTextBefore(lateTick, "if (_runtimeOwnerAborted)", "if (_pendingMusicSlowTickDirty)");
            AssertTextBefore(runTick, "if (_runtimeOwnerAborted)", "DrainAcousticZoneSignal();");
            AssertTextBefore(slowTick, "if (_runtimeOwnerAborted)", "_pendingMusicSlowTickDirty = true;");
            AssertTextBefore(runSlow, "if (_runtimeOwnerAborted)", "DrainAcousticZoneSignal();");

            AssertTextBefore(setManual, "if (_runtimeOwnerAborted)", "_manualProfile = profile;");
            AssertTextBefore(setMatrix, "if (_runtimeOwnerAborted)", "HectonMusicBiomeProfile resolvedProfile = ResolveMatrixBiomeMusicProfile(matrixProfile);");
            AssertTextBefore(setTier, "if (_runtimeOwnerAborted)", "SoundscapeTier safeTier = SanitizeSoundscapeTier(tier);");
            AssertTextBefore(clearManual, "if (_runtimeOwnerAborted)", "if (_manualProfile == null)");
            AssertTextBefore(setTension, "if (_runtimeOwnerAborted)", "_manualTensionOverride = true;");
            AssertTextBefore(clearTension, "if (_runtimeOwnerAborted)", "if (!_manualTensionOverride)");
            AssertTextBefore(forceOverride, "if (_runtimeOwnerAborted)", "if (clip == null)");
            AssertTextBefore(clearOverride, "if (_runtimeOwnerAborted)", "ClearForcedOverrideInternal(immediate);");
            AssertTextBefore(discovery, "if (_runtimeOwnerAborted)", "RefreshForegroundSpeechMusicDucking();");
            AssertTextBefore(danger, "if (_runtimeOwnerAborted)", "RefreshForegroundSpeechMusicDucking();");
            AssertTextBefore(recovery, "if (_runtimeOwnerAborted)", "RefreshForegroundSpeechMusicDucking();");
            AssertTextBefore(stop, "if (_runtimeOwnerAborted)", "StopMusicInternal(fadeOutSeconds);");

            AssertTextBefore(registerTicks, "if (_runtimeOwnerAborted || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);");
            AssertTextBefore(registerLate, "if (_runtimeOwnerAborted || _registeredLateFrameTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)", "GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);");
            AssertTextBefore(hotSwap, "if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(registerService, "if (_runtimeOwnerAborted)", "if (_serviceRegistered)");
            AssertTextBefore(registerService, "if (TryAbortForUsableExistingRuntime())", "HectonMusicDirector activeDirector = GlobalRegistry.MusicDirector;");
            StringAssert.Contains("if (IsMusicDirectorRuntimeUsable(activeDirector))", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);
            StringAssert.Contains("GlobalRegistry.UnregisterMusicDirectorRuntime(activeDirector);", registerService);
            StringAssert.Contains("GlobalRegistry.RegisterMusicDirectorRuntime(this);", registerService);
            StringAssert.Contains("_serviceRegistered = ReferenceEquals(GlobalRegistry.MusicDirector, this);", registerService);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", registerService);

            StringAssert.Contains("if (_runtimeOwnerAborted)", gate);
            StringAssert.Contains("if (!Application.isPlaying)", gate);
            StringAssert.Contains("HectonMusicDirector registered = GlobalRegistry.MusicDirector;", gate);
            StringAssert.Contains("if (IsMusicDirectorRuntimeUsable(registered))", gate);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterMusicDirectorRuntime(registered);", gate);
            StringAssert.Contains("HectonMusicDirector active = s_activeRuntimeInstance;", gate);
            StringAssert.Contains("GlobalRegistry.RegisterMusicDirectorRuntime(active);", gate);

            AssertTextBefore(abort, "StopMusicInternal(0f);", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "SceneManager.activeSceneChanged -= HandleActiveSceneChanged;", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterHotSwapListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterWorldZoneDirectorListener();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "TryUnregisterTickHandlers();", "_runtimeOwnerAborted = true;");
            AssertTextBefore(abort, "ClearCachedRuntimeServices();", "_runtimeOwnerAborted = true;");
            StringAssert.Contains("_registeredTick = false;", abort);
            StringAssert.Contains("_registeredSlowTick = false;", abort);
            StringAssert.Contains("_registeredLateFrameTick = false;", abort);
            StringAssert.Contains("_serviceRegistered = false;", abort);
            StringAssert.Contains("_hotSwapRegistered = false;", abort);
            StringAssert.Contains("_pendingDiscoveryStinger = false;", abort);
            StringAssert.Contains("enabled = false;", abort);
            StringAssert.Contains("Destroy(gameObject);", abort);

            StringAssert.Contains("director._serviceRegistered", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            StringAssert.Contains("!director._runtimeOwnerAborted", usable);
            AssertTextBefore(bindVoice, "if (_runtimeOwnerAborted)", "if (_musicSources == null)");
            AssertTextBefore(resolveVoice, "if (_runtimeOwnerAborted)", "if (_voicePool != null)");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;");
            AssertTextBefore(refresh, "if (_runtimeOwnerAborted)", "int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;");
            AssertTextBefore(resolveCold, "if (_runtimeOwnerAborted)", "ResolveDependenciesForSceneCold(SceneManager.GetActiveScene());");
            AssertTextBefore(worldListener, "if (_runtimeOwnerAborted)", "if (_worldZoneDirector == null || _worldZoneDirectorRuntimeOwned)");
            AssertTextBefore(worldChanged, "if (_runtimeOwnerAborted)", "CacheRuntimeWorldZoneDirectorCold(director);");
            AssertTextBefore(cacheWorld, "if (_runtimeOwnerAborted)", "if (_worldZoneDirector != null && !_worldZoneDirectorRuntimeOwned)");
            AssertTextBefore(resolveScene, "if (_runtimeOwnerAborted)", "RefreshCachedRuntimeServicesCold();");
            AssertTextBefore(resolveDeps, "if (_runtimeOwnerAborted)", "RefreshVocalWarningRuntimeIfStale();");
            AssertTextBefore(acoustic, "if (_runtimeOwnerAborted)", "if (_hasLastAcousticInteriorState && _lastAcousticInteriorState == isInterior)");
            AssertTextBefore(matrixChanged, "if (_runtimeOwnerAborted)", "SetMatrixBiomeProfile(profile);");
            AssertTextBefore(depthTier, "if (_runtimeOwnerAborted)", "ReevaluateContext(true);");
            AssertTextBefore(depthEntered, "if (_runtimeOwnerAborted)", "ReevaluateContext(true);");
            AssertTextBefore(activeScene, "if (_runtimeOwnerAborted)", "ResolveDependenciesForSceneCold(nextScene);");
            StringAssert.DoesNotContain("!Application.isPlaying || GlobalRegistry.MusicDirector != null", source);
        }

        [Test]
        public void OceanKinematicsRuntimeService_RuntimeOwnerGateReconcilesRuntimeMirrorAndServiceBeforeProviderTicks()
        {
            string source = ReadScript("Core", "OceanKinematicsRuntimeService.cs");
            string ensure = ExtractMethodBody(source, "public static OceanKinematicsRuntimeService EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string registerProvider = ExtractMethodBody(source, "public static void RegisterProvider(");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static OceanKinematicsRuntimeService ResolveUsableRuntime()");
            string serviceUsable = ExtractMethodBody(source, "private static bool IsOceanKinematicsServiceUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsOceanKinematicsRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("OceanKinematicsRuntimeService runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[OceanKinematicsRuntimeService]\")");
            StringAssert.Contains("if (IsOceanKinematicsServiceUsable(registeredService)", ensure);
            StringAssert.Contains("return null;", ensure);
            StringAssert.Contains("OceanKinematicsRuntimeService runtime = EnsureRuntimeInstance();", registerProvider);
            StringAssert.Contains("if (runtime == null)", registerProvider);
            AssertTextBefore(registerProvider, "if (runtime == null)", "runtime.RegisterProviderInternal(provider);");

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterUpdatable();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "RefreshActiveProvider();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterUpdatable();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.ClearOceanKinematicsRuntime(this);");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("GlobalRegistry.ClearOceanKinematicsRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterOceanKinematicsRuntime(this);", ownership);
            StringAssert.Contains("return ReferenceEquals(GlobalRegistry.OceanKinematicsRuntime, this);", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterOceanKinematicsService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterOceanKinematicsService(registeredService);", register);
            StringAssert.Contains("GlobalRegistry.ClearOceanKinematicsRuntime(staleRuntime);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredService;", register);
            StringAssert.Contains("return _registeredService;", register);

            StringAssert.Contains("OceanKinematicsRuntimeService runtime = GlobalRegistry.OceanKinematicsRuntime;", gate);
            StringAssert.Contains("if (IsOceanKinematicsRuntimeUsable(runtime))", gate);
            StringAssert.Contains("IHectonOceanKinematicsService registeredService = GlobalRegistry.OceanKinematics;", gate);
            StringAssert.Contains("if (IsOceanKinematicsServiceUsable(registeredService))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterOceanKinematicsService(registeredService);", gate);

            StringAssert.Contains("if (IsOceanKinematicsRuntimeUsable(runtime))", resolver);
            StringAssert.Contains("GlobalRegistry.ClearOceanKinematicsRuntime(runtime);", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterOceanKinematicsService(registeredService);", resolver);
            StringAssert.Contains("OceanKinematicsRuntimeService runtime = service as OceanKinematicsRuntimeService;", serviceUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", serviceUsable);
            StringAssert.Contains("runtime._registeredService", serviceUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("GlobalRegistry.OceanKinematics as OceanKinematicsRuntimeService", source);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
        }

        [Test]
        public void SceneRuntimeService_RuntimeOwnerGateReconcilesRuntimeMirrorAndSceneServiceBeforeMemoryTicksAndCallbacks()
        {
            string source = ReadScript("Core", "SceneRuntimeService.cs");
            string ensure = ExtractMethodBody(source, "public static SceneRuntimeService EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string register = ExtractMethodBody(source, "private bool TryRegisterSceneService()");
            string ownership = ExtractMethodBody(source, "private bool EnsureRuntimeOwnership()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static SceneRuntimeService ResolveUsableRuntime()");
            string serviceUsable = ExtractMethodBody(source, "private static bool IsSceneServiceUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsSceneRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("SceneRuntimeService runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[SceneRuntimeService]\")");
            StringAssert.Contains("if (IsSceneServiceUsable(registeredScene)", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "if (!TryRegisterSceneService())");
            AssertTextBefore(initialize, "if (!TryRegisterSceneService())", "H8Memory.Initialize();");
            AssertTextBefore(initialize, "if (!TryRegisterSceneService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterSceneService())", "TryRegisterUpdatable();");
            AssertTextBefore(initialize, "if (!TryRegisterSceneService())", "TryRegisterSceneCallbacks();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !EnsureRuntimeOwnership())", "TryRegisterUpdatable();");
            AssertTextBefore(onEnable, "if (!TryRegisterSceneService())", "TryRegisterSceneCallbacks();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwapListener();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.ClearSceneRuntime(this);");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("GlobalRegistry.ClearSceneRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterSceneRuntime(this);", ownership);
            StringAssert.Contains("return ReferenceEquals(GlobalRegistry.SceneRuntime, this);", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSceneService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterSceneService(registeredScene);", register);
            StringAssert.Contains("GlobalRegistry.ClearSceneRuntime(staleRuntime);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredSceneService;", register);
            StringAssert.Contains("return _registeredSceneService;", register);

            StringAssert.Contains("SceneRuntimeService runtime = GlobalRegistry.SceneRuntime;", gate);
            StringAssert.Contains("if (IsSceneRuntimeUsable(runtime))", gate);
            StringAssert.Contains("ISceneService registeredScene = GlobalRegistry.Scene;", gate);
            StringAssert.Contains("if (IsSceneServiceUsable(registeredScene))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSceneService(registeredScene);", gate);

            StringAssert.Contains("if (IsSceneRuntimeUsable(runtime))", resolver);
            StringAssert.Contains("GlobalRegistry.ClearSceneRuntime(runtime);", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterSceneService(registeredScene);", resolver);
            StringAssert.Contains("SceneRuntimeService runtime = service as SceneRuntimeService;", serviceUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", serviceUsable);
            StringAssert.Contains("runtime._registeredSceneService", serviceUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("GlobalRegistry.Scene as SceneRuntimeService", source);
            StringAssert.DoesNotContain("RejectDuplicateRuntimeOwner", source);
        }

        [Test]
        public void EnvironmentRuntimeContextService_RuntimeOwnerGateReconcilesRuntimeMirrorAndContextBeforeHazardAndTicks()
        {
            string source = ReadScript("Core", "EnvironmentRuntimeContextService.cs");
            string ensure = ExtractMethodBody(source, "public static EnvironmentRuntimeContextService EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string ownership = ExtractMethodBody(source, "private bool EnsureSingletonOwnership()");
            string register = ExtractMethodBody(source, "private bool TryRegisterContext()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string resolver = ExtractMethodBody(source, "private static EnvironmentRuntimeContextService ResolveUsableRuntime()");
            string contextUsable = ExtractMethodBody(source, "private static bool IsEnvironmentContextUsable(");
            string runtimeUsable = ExtractMethodBody(source, "private static bool IsEnvironmentRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("EnvironmentRuntimeContextService runtime = ResolveUsableRuntime();", ensure);
            AssertTextBefore(ensure, "ResolveUsableRuntime()", "new GameObject(\"[EnvironmentRuntimeContextService]\")");
            StringAssert.Contains("IsEnvironmentContextUsable(registeredContext)", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (_runtimeOwnerAborted || !EnsureSingletonOwnership())", "if (_isInitialized)");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "TryRegisterHotSwapListener();");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "TryRegisterUpdatable();");
            AssertTextBefore(initialize, "if (!TryRegisterContext())", "EnsureHazardZoneManager();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterContext())", "TryRegisterUpdatable();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(this);");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "TryUnregisterUpdatable();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", ownership);
            StringAssert.Contains("GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(runtime);", ownership);
            StringAssert.Contains("GlobalRegistry.RegisterEnvironmentRuntimeContextRuntime(this);", ownership);
            StringAssert.Contains("return ReferenceEquals(GlobalRegistry.EnvironmentRuntimeContextRuntime, this);", ownership);

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterEnvironmentRuntimeContext(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);", register);
            StringAssert.Contains("GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(staleContext);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredContext;", register);
            StringAssert.Contains("return _registeredContext;", register);

            StringAssert.Contains("EnvironmentRuntimeContextService runtime = GlobalRegistry.EnvironmentRuntimeContextRuntime;", gate);
            StringAssert.Contains("if (IsEnvironmentRuntimeUsable(runtime))", gate);
            StringAssert.Contains("IEnvironmentRuntimeContext registeredContext = GlobalRegistry.Environment;", gate);
            StringAssert.Contains("if (IsEnvironmentContextUsable(registeredContext))", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);", gate);

            StringAssert.Contains("if (IsEnvironmentRuntimeUsable(runtime))", resolver);
            StringAssert.Contains("GlobalRegistry.ClearEnvironmentRuntimeContextRuntime(runtime);", resolver);
            StringAssert.Contains("GlobalRegistry.UnregisterEnvironmentRuntimeContext(registeredContext);", resolver);
            StringAssert.Contains("EnvironmentRuntimeContextService runtime = context as EnvironmentRuntimeContextService;", contextUsable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", contextUsable);
            StringAssert.Contains("runtime._registeredContext", contextUsable);
            StringAssert.Contains("runtime.isActiveAndEnabled", runtimeUsable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", runtimeUsable);
            StringAssert.DoesNotContain("registeredContext != null && !ReferenceEquals(registeredContext, this)", source);
            StringAssert.DoesNotContain("runtime != null && runtime != this", source);
        }

        [Test]
        public void DebrisManager_RuntimeOwnerGateClearsStaleRegistryBeforeVaultResourcesHooksAndRebinds()
        {
            string source = ReadScript("Gameplay", "DebrisManager.cs");
            string ensure = ExtractMethodBody(source, "public static DebrisManager EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsDebrisRuntimeUsable(");
            string rebind = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string unregisterRuntimeHooks = ExtractMethodBody(source, "private void UnregisterRuntimeHooks()");

            StringAssert.Contains("IDebrisService registeredService = GlobalRegistry.Debris;", ensure);
            StringAssert.Contains("if (IsDebrisRuntimeUsable(registeredService))", ensure);
            AssertTextBefore(ensure, "if (IsDebrisRuntimeUsable(registeredService))", "new GameObject(\"[DebrisManager]\")");
            StringAssert.Contains("GlobalRegistry.UnregisterDebrisService(registeredService);", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (!TryRegisterService())", "RefreshColdRegistryReferences();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "EnsureRuntimeResources();");
            StringAssert.Contains("_isInitialized = _serviceRegistered;", initialize);
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterService())", "RefreshColdRegistryReferences();");
            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterService())", "EnsureRuntimeResources();");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterService())", "EnsureRuntimeResources();");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterService())", "GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment)");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterService())", "HectonFloatingOrigin.RegisterListener(this);");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "UnregisterRuntimeHooks();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "ReleaseNativeState();");
            AssertTextBefore(rebind, "if (_runtimeOwnerAborted)", "ReleaseNativeState();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterDebrisService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterDebrisService(registeredService);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_serviceRegistered;", register);
            StringAssert.Contains("Destroy(gameObject);", register);
            StringAssert.Contains("return _serviceRegistered;", register);

            StringAssert.Contains("IDebrisService registeredService = GlobalRegistry.Debris;", gate);
            StringAssert.Contains("if (IsDebrisRuntimeUsable(registeredService))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterDebrisService(registeredService);", gate);

            StringAssert.Contains("DebrisManager manager = service as DebrisManager;", usable);
            StringAssert.Contains("ReferenceEquals(manager, null) ||", usable);
            StringAssert.Contains("manager._serviceRegistered", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", usable);
            StringAssert.Contains("GlobalRegistry.TryUnregisterHotSwapListener(this);", unregisterRuntimeHooks);
            StringAssert.DoesNotContain("GlobalRegistry.UnregisterHotSwapListener(this);", unregisterRuntimeHooks);
            StringAssert.DoesNotContain("registeredManager != this", source);
        }

        [Test]
        public void MetaCampaignService_RuntimeOwnerGateSeparatesServiceClaimFromTickLanesBeforeRuntimeState()
        {
            string source = ReadScript(Path.Combine("Narrative", "Campaign"), "MetaCampaignService.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveService()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string tickLanes = ExtractMethodBody(source, "private void TryRegisterTickLanes()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsMetaCampaignRuntimeUsable(");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");

            AssertTextBefore(awake, "if (!TryRegisterService())", "AllocateRuntimeState();");
            AssertTextBefore(awake, "if (!TryRegisterService())", "SeedDefaultState();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "AllocateRuntimeState();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "EnsureDefaultVariables();");
            AssertTextBefore(onEnable, "TryRegisterTickLanes();", "TryRegisterHotSwapListener();");
            AssertTextBefore(start, "if (!TryRegisterService())", "TryRegisterTickLanes();");
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", start);
            StringAssert.DoesNotContain("if (_saveService == null)", start);
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "ReleaseRuntimeState(_dataVault);");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterMetaCampaignService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterMetaCampaignService(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = true;", register);
            StringAssert.Contains("Destroy(this);", register);
            StringAssert.Contains("return true;", register);

            StringAssert.Contains("if (!_serviceRegistered || _runtimeOwnerAborted || _shutdown)", tickLanes);
            StringAssert.Contains("GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core)", tickLanes);
            StringAssert.Contains("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core)", tickLanes);
            StringAssert.Contains("_serviceReady = _updatableRegistered && _lateFrameRegistered;", tickLanes);

            StringAssert.Contains("IMetaCampaignService registered = GlobalRegistry.MetaCampaign;", gate);
            StringAssert.Contains("if (IsMetaCampaignRuntimeUsable(registered))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(this);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterMetaCampaignService(registered);", gate);

            StringAssert.Contains("MetaCampaignService runtime = service as MetaCampaignService;", usable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", usable);
            StringAssert.Contains("runtime._serviceRegistered", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._shutdown", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveServiceRegistered = true;");
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void GlobalProfileManager_RuntimeOwnerGatePreservesProfileFileBeforeLoadFlushAndTickSubscriptions()
        {
            string source = ReadScript("Meta", "GlobalProfileManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string onApplicationQuit = ExtractMethodBody(source, "private void OnApplicationQuit()");
            string onApplicationPause = ExtractMethodBody(source, "private void OnApplicationPause(");
            string register = ExtractMethodBody(source, "private bool TryRegisterProfileService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsProfileRuntimeUsable(");
            string resolveOwnersCold = ExtractMethodBody(source, "private bool ResolveOwnersCold()");
            string cacheRegistryOwnersCold = ExtractMethodBody(source, "private void CacheRegistryOwnersCold()");
            string elapsed = ExtractMethodBody(source, "private float ResolveCurrentRunElapsedSeconds()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            AssertTextBefore(awake, "if (!TryRegisterProfileService())", "LoadProfile();");
            AssertTextBefore(onEnable, "if (!TryRegisterProfileService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterProfileService())", "TryRegisterWithTickManager();");
            AssertTextBefore(onEnable, "if (!TryRegisterProfileService())", "TryRegisterWithUpdateDispatcher();");
            AssertTextBefore(start, "if (!TryRegisterProfileService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(start, "if (!TryRegisterProfileService())", "TryRegisterWithTickManager();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "FlushCurrentRunRecords();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "FlushIfDirtyCold();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "FlushCurrentRunRecords();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "FlushIfDirtyCold();");
            AssertTextBefore(onApplicationQuit, "if (_runtimeOwnerAborted)", "FlushIfDirtyCold();");
            AssertTextBefore(onApplicationPause, "if (_runtimeOwnerAborted)", "FlushIfDirtyCold();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterProfileService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterProfileService(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredProfileService;", register);
            StringAssert.Contains("Destroy(gameObject);", register);
            StringAssert.Contains("return _registeredProfileService;", register);

            StringAssert.Contains("IProfileService registered = GlobalRegistry.Profile;", gate);
            StringAssert.Contains("if (IsProfileRuntimeUsable(registered))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterProfileService(registered);", gate);

            StringAssert.Contains("GlobalProfileManager manager = service as GlobalProfileManager;", usable);
            StringAssert.Contains("ReferenceEquals(manager, null) ||", usable);
            StringAssert.Contains("manager._registeredProfileService", usable);
            StringAssert.Contains("manager.isActiveAndEnabled", usable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", usable);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", resolveOwnersCold);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", cacheRegistryOwnersCold);
            StringAssert.DoesNotContain("if (_saveService == null)", resolveOwnersCold);
            StringAssert.DoesNotContain("if (_saveService == null)", cacheRegistryOwnersCold);
            AssertTextBefore(elapsed, "if (!IsSaveServiceUsable(saveService))", "saveService = GlobalRegistry.Save;");
            AssertTextBefore(elapsed, "saveService = GlobalRegistry.Save;", "_saveService = saveService;");
            AssertTextBefore(elapsed, "_saveService = saveService;", "if (IsSaveServiceUsable(saveService))");
            StringAssert.Contains("if (IsSaveServiceUsable(saveService))", elapsed);
            AssertTextBefore(elapsed, "if (IsSaveServiceUsable(saveService))", "return Mathf.Max(0f, saveService.CurrentPlayTimeSeconds);");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService != null)", elapsed);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void DynamicDifficultyDirector_TelemetryTimeIgnoresStaleSaveService()
        {
            string source = ReadScript("Meta", "DynamicDifficultyDirector.cs");
            string resolveOwnersCold = ExtractMethodBody(source, "private bool ResolveOwnersCold()");
            string cacheRegistryOwnersCold = ExtractMethodBody(source, "private void CacheRegistryOwnersCold()");
            string resolveTelemetryTime = ExtractMethodBody(source, "private float ResolveTelemetryTimeSeconds()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", resolveOwnersCold);
            StringAssert.Contains("if (!IsSaveServiceUsable(_saveService))", cacheRegistryOwnersCold);
            StringAssert.DoesNotContain("if (_saveService == null)", resolveOwnersCold);
            StringAssert.DoesNotContain("if (_saveService == null)", cacheRegistryOwnersCold);
            StringAssert.Contains("if (IsSaveServiceUsable(saveService))", resolveTelemetryTime);
            AssertTextBefore(resolveTelemetryTime, "if (!IsSaveServiceUsable(saveService))", "saveService = GlobalRegistry.Save;");
            AssertTextBefore(resolveTelemetryTime, "saveService = GlobalRegistry.Save;", "_saveService = saveService;");
            AssertTextBefore(resolveTelemetryTime, "_saveService = saveService;", "if (IsSaveServiceUsable(saveService))");
            AssertTextBefore(resolveTelemetryTime, "if (IsSaveServiceUsable(saveService))", "return math.max(0f, saveService.CurrentPlayTimeSeconds);");
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsable);
            StringAssert.DoesNotContain("if (saveService != null)", resolveTelemetryTime);
            StringAssert.Contains("Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds", resolveTelemetryTime);
        }

        [Test]
        public void ModularEquipmentEngine_RuntimeOwnerGateClearsStaleRegistryBeforeNativeStateHotSwapAndTicks()
        {
            string source = ReadScript(string.Empty, "ModularEquipmentEngine.cs");
            string ensure = ExtractMethodBody(source, "public static ModularEquipmentEngine EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsModularEquipmentRuntimeUsable(");

            StringAssert.Contains("IModularEquipmentService registered = GlobalRegistry.ModularEquipment;", ensure);
            StringAssert.Contains("if (IsModularEquipmentRuntimeUsable(registered))", ensure);
            AssertTextBefore(ensure, "if (IsModularEquipmentRuntimeUsable(registered))", "new GameObject(\"[ModularEquipmentEngine]\")");
            StringAssert.Contains("GlobalRegistry.UnregisterModularEquipmentService(registered);", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (!TryRegisterService())", "CacheRegistryDependenciesCold();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterHotSwap();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "InitializeActiveEquipmentNativeState();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryAcquireEquipmentViewsWriteLock(out EquipmentVaultViews views)");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "CacheRegistryDependenciesCold();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "TryRegisterHotSwap();");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted || !TryRegisterService())", "SceneManager.sceneUnloaded +=");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "DrainEquipmentIntegrationLocksForLifecycle()");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterHotSwap();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "DisposeNativeState();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterModularEquipmentService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterModularEquipmentService(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredService;", register);
            StringAssert.Contains("Destroy(gameObject);", register);
            StringAssert.Contains("return _registeredService;", register);

            StringAssert.Contains("IModularEquipmentService registered = GlobalRegistry.ModularEquipment;", gate);
            StringAssert.Contains("if (IsModularEquipmentRuntimeUsable(registered))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterModularEquipmentService(registered);", gate);

            StringAssert.Contains("ModularEquipmentEngine engine = service as ModularEquipmentEngine;", usable);
            StringAssert.Contains("ReferenceEquals(engine, null) ||", usable);
            StringAssert.Contains("engine._registeredService", usable);
            StringAssert.Contains("engine.isActiveAndEnabled", usable);
            StringAssert.Contains("!engine._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void DestructibleOrganicManager_RuntimeOwnerGateReconcilesActiveMirrorAndOrganicToolHitServiceBeforeVaultsAndDispatcher()
        {
            string source = ReadScript("World", "DestructibleOrganicManager.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string register = ExtractMethodBody(source, "private bool TryRegisterOrganicToolHitService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");
            string serviceUsable = ExtractMethodBody(source, "private static bool IsOrganicToolHitServiceUsable(");
            string managerUsable = ExtractMethodBody(source, "private static bool IsDestructibleOrganicRuntimeUsable(");

            AssertTextBefore(awake, "if (Application.isPlaying && TryAbortForUsableExistingRuntime())", "_activeRuntimeInstance = this;");
            AssertTextBefore(awake, "if (Application.isPlaying && TryAbortForUsableExistingRuntime())", "_surfaceMatrices = new BridgeMatrixLane");
            AssertTextBefore(onEnable, "if (TryAbortForUsableExistingRuntime())", "CacheRegistryServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterOrganicToolHitService())", "TryBootstrapDearLieVault(clearExisting: true);");
            AssertTextBefore(onEnable, "if (!TryRegisterOrganicToolHitService())", "EnsureOrganicVaultBuffers(clearExisting: true);");
            AssertTextBefore(onEnable, "if (!TryRegisterOrganicToolHitService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (!TryRegisterOrganicToolHitService())", "TryRegisterDispatcherPhases();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "TryUnregisterTickLanes();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ReleaseOrganicVaultBuffers(_dearLieVault);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "TryUnregisterTickLanes();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ReleaseOrganicVaultBuffers(_dearLieVault);");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterOrganicToolHitService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterOrganicToolHitService(registered);", register);
            StringAssert.Contains("AbortDuplicateRuntimeOwner();", register);
            StringAssert.Contains("return _organicToolHitServiceRegistered;", register);

            StringAssert.Contains("DestructibleOrganicManager active = _activeRuntimeInstance;", gate);
            StringAssert.Contains("if (IsDestructibleOrganicRuntimeUsable(active))", gate);
            StringAssert.Contains("IOrganicToolHitService registeredService = GlobalRegistry.OrganicToolHits;", gate);
            StringAssert.Contains("if (IsOrganicToolHitServiceUsable(registeredService))", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterOrganicToolHitService(registeredService);", gate);

            StringAssert.Contains("_runtimeOwnerAborted = true;", abort);
            StringAssert.Contains("_activeRuntimeInstance = null;", abort);
            StringAssert.Contains("enabled = false;", abort);

            StringAssert.Contains("DestructibleOrganicManager manager = service as DestructibleOrganicManager;", serviceUsable);
            StringAssert.Contains("ReferenceEquals(manager, null) ||", serviceUsable);
            StringAssert.Contains("manager._organicToolHitServiceRegistered", serviceUsable);
            StringAssert.Contains("IsDestructibleOrganicRuntimeUsable(manager)", serviceUsable);
            StringAssert.Contains("ReferenceEquals(_activeRuntimeInstance, manager)", managerUsable);
            StringAssert.Contains("manager.isActiveAndEnabled", managerUsable);
            StringAssert.Contains("!manager._runtimeOwnerAborted", managerUsable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void HectonSeismicTideDirector_RuntimeOwnerGateClearsStaleRegistryBeforeTelemetryVaultsAndTickLanes()
        {
            string source = ReadScript("Environment", "HectonSeismicTideDirector.cs");
            string ensure = ExtractMethodBody(source, "public static HectonSeismicTideDirector EnsureRuntimeInstance()");
            string initialize = ExtractMethodBody(source, "public void InitializeService()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsSeismicRuntimeUsable(");

            StringAssert.Contains("ISeismicDirector registeredService = GlobalRegistry.SeismicDirector;", ensure);
            StringAssert.Contains("if (IsSeismicRuntimeUsable(registeredService))", ensure);
            AssertTextBefore(ensure, "if (IsSeismicRuntimeUsable(registeredService))", "new GameObject(\"[HectonSeismicTideDirector]\")");
            StringAssert.Contains("GlobalRegistry.UnregisterSeismicDirector(registeredService);", ensure);
            StringAssert.Contains("return null;", ensure);

            AssertTextBefore(initialize, "if (!TryRegisterService())", "RefreshCachedRuntimeState();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "EnsureTelemetryRing();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "EnsureSeismicVaultBuffers();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "PrewarmSeismicSignalLanes();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "TryRegisterTickLanes();");
            AssertTextBefore(initialize, "if (!TryRegisterService())", "EvaluateAndPublish(");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "GlobalRegistry.TryRegisterHotSwapListener(this);");
            AssertTextBefore(onEnable, "if (_runtimeOwnerAborted)", "TryRegisterTickLanes();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "CompleteSeismicEvaluationJob(force: true);");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "PushWorldShake(Vector4.zero);");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "ShutdownServiceState();");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "CompleteSeismicEvaluationJob(force: true);");
            AssertTextBefore(shutdown, "if (_runtimeOwnerAborted)", "DisposeTelemetryRing();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterSeismicDirector(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterSeismicDirector(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_registeredService;", register);
            StringAssert.Contains("enabled = false;", register);
            StringAssert.Contains("return _registeredService;", register);

            StringAssert.Contains("ISeismicDirector registered = GlobalRegistry.SeismicDirector;", gate);
            StringAssert.Contains("if (IsSeismicRuntimeUsable(registered))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("enabled = false;", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterSeismicDirector(registered);", gate);

            StringAssert.Contains("HectonSeismicTideDirector director = service as HectonSeismicTideDirector;", usable);
            StringAssert.Contains("ReferenceEquals(director, null) ||", usable);
            StringAssert.Contains("director._registeredService", usable);
            StringAssert.Contains("director.isActiveAndEnabled", usable);
            StringAssert.Contains("!director._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void CarveDebrisComputeRenderer_RuntimeOwnerGatePreservesRegisteredComputeServiceBeforeFallbackGpuAndTicks()
        {
            string source = ReadScript(Path.Combine("VFX", "Debris"), "CarveDebrisComputeRenderer.cs");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string start = ExtractMethodBody(source, "private void Start()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string register = ExtractMethodBody(source, "private bool TryRegisterComputeService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsDebrisComputeRuntimeUsable(");

            AssertTextBefore(awake, "if (Application.isPlaying && !TryRegisterComputeService())", "EnsureFallbackRenderResources();");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterComputeService())", "EnsureFallbackRenderResources();");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterComputeService())", "TryRegisterHotSwapListener();");
            AssertTextBefore(onEnable, "if (Application.isPlaying && !TryRegisterComputeService())", "TryEnsureGpuState();");
            AssertTextBefore(start, "if (!TryRegisterComputeService())", "TryEnsureGpuState();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "ReleaseGpuState();");

            StringAssert.Contains("if (Application.isPlaying && TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (Application.isPlaying && TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterDebrisComputeService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterDebrisComputeService(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = Application.isPlaying && !_computeServiceRegistered;", register);
            StringAssert.Contains("return _computeServiceRegistered;", register);

            StringAssert.Contains("IDebrisComputeService registered = GlobalRegistry.DebrisCompute;", gate);
            StringAssert.Contains("if (IsDebrisComputeRuntimeUsable(registered))", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterDebrisComputeService(registered);", gate);

            StringAssert.Contains("CarveDebrisComputeRenderer renderer = service as CarveDebrisComputeRenderer;", usable);
            StringAssert.Contains("ReferenceEquals(renderer, null) ||", usable);
            StringAssert.Contains("renderer._computeServiceRegistered", usable);
            StringAssert.Contains("renderer.isActiveAndEnabled", usable);
            StringAssert.Contains("!renderer._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }

        [Test]
        public void HardwareThermalService_RuntimeOwnerGatePreservesPoliciesWhenDuplicateAbortsBeforeNativeAndTicks()
        {
            string source = ReadScript(Path.Combine("Core", "Hardware"), "HardwareThermalService.cs");
            string ensure = ExtractMethodBody(source, "private static void EnsureRuntimeInstanceCold()");
            string awake = ExtractMethodBody(source, "private void Awake()");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string dispose = ExtractMethodBody(source, "public void Dispose()");
            string register = ExtractMethodBody(source, "private bool TryRegisterService()");
            string gate = ExtractMethodBody(source, "private bool TryAbortForUsableExistingRuntime()");
            string usable = ExtractMethodBody(source, "private static bool IsHardwareThermalRuntimeUsable(");

            StringAssert.Contains("IHardwareThermalService registered = GlobalRegistry.HardwareThermal;", ensure);
            StringAssert.Contains("if (IsHardwareThermalRuntimeUsable(registered))", ensure);
            AssertTextBefore(ensure, "if (IsHardwareThermalRuntimeUsable(registered))", "new GameObject(\"[HardwareThermalService]\")");
            StringAssert.Contains("GlobalRegistry.UnregisterHardwareThermalService(registered);", ensure);
            StringAssert.Contains("else if (!ReferenceEquals(registered, null))", ensure);

            AssertTextBefore(awake, "if (!TryRegisterService())", "EnsureNativeState();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "RebindCachedServicesCold();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "TryRegisterHotSwap();");
            AssertTextBefore(onEnable, "if (!TryRegisterService())", "SampleAndApplyCold();");
            AssertTextBefore(onDisable, "if (_runtimeOwnerAborted)", "Dispose();");
            AssertTextBefore(onDestroy, "if (_runtimeOwnerAborted)", "Dispose();");
            AssertTextBefore(dispose, "if (_runtimeOwnerAborted)", "ReleaseThermalPolicies();");

            StringAssert.Contains("if (TryAbortForUsableExistingRuntime())", register);
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.ReplaceHardwareThermalService(this);");
            AssertTextBefore(register, "if (TryAbortForUsableExistingRuntime())", "GlobalRegistry.RegisterHardwareThermalService(this);");
            StringAssert.Contains("GlobalRegistry.UnregisterHardwareThermalService(registered);", register);
            StringAssert.Contains("_runtimeOwnerAborted = !_serviceRegistered;", register);
            StringAssert.Contains("return _serviceRegistered;", register);

            StringAssert.Contains("IHardwareThermalService registered = GlobalRegistry.HardwareThermal;", gate);
            StringAssert.Contains("IsHardwareThermalRuntimeUsable(registered)", gate);
            StringAssert.Contains("_runtimeOwnerAborted = true;", gate);
            StringAssert.Contains("Destroy(gameObject);", gate);
            StringAssert.Contains("GlobalRegistry.UnregisterHardwareThermalService(registered);", gate);

            StringAssert.Contains("HardwareThermalService runtime = service as HardwareThermalService;", usable);
            StringAssert.Contains("ReferenceEquals(runtime, null) ||", usable);
            StringAssert.Contains("runtime._serviceRegistered", usable);
            StringAssert.Contains("runtime.isActiveAndEnabled", usable);
            StringAssert.Contains("!runtime._runtimeOwnerAborted", usable);
            StringAssert.DoesNotContain("registered != null && !ReferenceEquals(registered, this)", source);
        }
        private static string ReadScript(string folder, string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", folder, fileName));
        }

        private static void AssertRuntimeGate(
            string gate,
            string firstOwner,
            string secondOwner,
            string usabilityCheck,
            string unregisterCall,
            string destroyCall = "Destroy(gameObject);")
        {
            StringAssert.Contains(firstOwner, gate);
            StringAssert.Contains(secondOwner, gate);
            StringAssert.Contains("if (" + usabilityCheck + "(", gate);
            StringAssert.Contains(destroyCall, gate);
            StringAssert.Contains("= null", gate);
            StringAssert.Contains(unregisterCall + "(", gate);
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
        }

        private static void AssertInitializedSaveOwnerRegistrationGate(string source, string registerCall)
        {
            string register = ExtractMethodBody(source, "private void TryRegisterWithSaveManager()");
            string unregister = ExtractMethodBody(source, "private void UnregisterFromSaveManager()");
            string usable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = _saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                "_saveService = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                registerCall,
                "_registeredSaveService = saveService;",
                "_registeredToSave = true;"));
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            StringAssert.Contains("if (!_registeredToSave && _registeredSaveService == null)", unregister);
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", unregister);
            AssertTextBefore(unregister, "saveService.Unregister(this);", "_registeredSaveService = null;");
            StringAssert.Contains("_registeredSaveService = null;", unregister);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (_saveService == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
            StringAssert.DoesNotContain("ISaveService saveService = _saveService;", unregister);
        }

        private static void AssertInitializedSaveOwnerRegistrationGate(
            string register,
            string usable,
            string saveServiceField,
            string registerCall,
            string registeredFlagAssignment)
        {
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = " + saveServiceField + ";",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                saveServiceField + " = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                registerCall,
                registeredFlagAssignment));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (" + saveServiceField + " == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        private static void AssertRegisteredSaveOwnerUnregister(
            string source,
            string unregister,
            string saveServiceField,
            string registeredFlagName)
        {
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "if (!" + registeredFlagName + " && _registeredSaveService == null)",
                "return;",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : " + saveServiceField + ";",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                registeredFlagName + " = false;"));
            StringAssert.DoesNotContain("ISaveService saveService = " + saveServiceField + ";", unregister);
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
    }
}
