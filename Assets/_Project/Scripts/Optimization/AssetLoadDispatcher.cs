using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
#if UNITY_ADDRESSABLES_EXIST
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace Hecton8.Optimization
{
    /// <summary>
    /// Main-thread dispatcher that throttles asset load grants by tier and active pressure.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8011)]
    public sealed class AssetLoadDispatcher : MonoBehaviour, ITickable, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int Tier01Slots = 8;
        private const int Tier2Slots = 6;
        private const int Tier34Slots = 4;
        private const int Tier34CriticalSlots = 1;
        private const int Tier56Slots = 2;
        private const int Tier56WarningSlots = 0;
        private const int StarvationFrameThreshold = 60;
        private const long BytesPerMegabyte = 1024L * 1024L;
        private const long UnknownDispatchPayloadBytes = BytesPerMegabyte;
        private const long MinimumFrameUploadBudgetBytes = 2L * BytesPerMegabyte;
        private const long LowFrameUploadBudgetBytes = 5L * BytesPerMegabyte;
        private const long UltraFrameUploadBudgetBytes = 50L * BytesPerMegabyte;
        private const int UnknownGraphicsBudgetMb = 1800;
        private const float UiMipDowngradePressureFraction = 1700f / 2048f;
        private const float UiMipRestorePressureFraction = 1400f / 2048f;
        private const uint UiMipGateHighHash = 0xB157A301u;
        private const uint UiMipGateRestoreHash = 0xB157A302u;
        private const uint UiTextureContextHash = 0x71C0A11Du;
        private const int AddressableGroupMapCapacity = 512;
        private const int QueuedRequestCapacity = 128;
        private const int ReadyTicketCapacity = 32;
        private const int InflightRequestCapacity = 64;
        private const int ProgressSignalBufferCapacity = AssetLoadProgressSignal.ExpectedCapacity;
        private static AssetLoadDispatcher s_registeredInstance;
        private static int s_x001AssetLoadDispatcherProgressSignalDropCount;

        [Header("Dispatch Budget")]
        [Tooltip("Main-thread dispatch budget in milliseconds per frame.")]
        [SerializeField] private float dispatchBudgetMilliseconds = 2f;

        [Tooltip("Maximum ready tickets retained for backend consumers.")]
        [SerializeField] private int maxReadyTicketCount = 32;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private bool _registeredService;
        private bool _registeredHotSwap;
        private int _nextRequestId = 1;

        // COLD ALLOC: AssetDispatchRequest[128] - fixed queued load requests - owner: AssetLoadDispatcher
        private readonly AssetDispatchRequest[] _queuedRequests = new AssetDispatchRequest[QueuedRequestCapacity];
        private int _queuedRequestCount;
        // COLD ALLOC: AssetDispatchTicket[32] - fixed ready-to-dispatch tickets - owner: AssetLoadDispatcher
        private readonly AssetDispatchTicket[] _readyTickets = new AssetDispatchTicket[ReadyTicketCapacity];
        private int _readyTicketCount;
        // COLD ALLOC: AssetDispatchRequest[64] - fixed active in-flight requests - owner: AssetLoadDispatcher
        private readonly AssetDispatchRequest[] _inflightRequests = new AssetDispatchRequest[InflightRequestCapacity];
        private int _inflightRequestCount;
        // COLD ALLOC: int[4] - tier-band inflight counters - owner: AssetLoadDispatcher
        private readonly int[] _inflightCounts = new int[4];
        // COLD ALLOC: uint[512]/byte[512] - fixed addressable group cache for UI mip gate - owner: AssetLoadDispatcher
        private readonly uint[] _addressableGroupKeys = new uint[AddressableGroupMapCapacity];
        private readonly byte[] _addressableGroupValues = new byte[AddressableGroupMapCapacity];
        // COLD ALLOC: AssetLoadProgressSignal[128] - fixed late-frame handoff buffer - owner: AssetLoadDispatcher
        private readonly AssetLoadProgressSignal[] _progressSignals = new AssetLoadProgressSignal[ProgressSignalBufferCapacity];
        private int _addressableGroupCount;
        private int _progressSignalCount;
        private long _lastObservedVramBytes;
        private long _graphicsBudgetBytes;
        private long _frameUploadBudgetBytes;
        private long _frameUploadGrantedBytes;
        private uint _uploadBudgetFrameId = uint.MaxValue;
        private bool _uiMipBiasGateActive;
        private bool _uiMipBiasGateEvaluationQueued;
        private bool _registeredLateFrameTick;
        private IVramBudgetReadModel _vramMonitor;
        private IVramPressureReadModel _vramPressure;
        private IVramPressureMipBiasSink _vramPressureMipBias;
        private IAssetLifecyclePressureSink _assetLifecycle;
#if UNITY_ADDRESSABLES_EXIST
        private uint _lastAddressableDependencyGroupHash;
        private int _lastAddressableDependencyOrder;
        private int _addressableDependencyGroupReadyCount;
#endif

        internal static bool IsUiMipBiasGateActive
        {
            get
            {
                AssetLoadDispatcher dispatcher = s_registeredInstance;
                return dispatcher != null && dispatcher._uiMipBiasGateActive;
            }
        }

        internal static long LastObservedVramBytes
        {
            get
            {
                AssetLoadDispatcher dispatcher = s_registeredInstance;
                return dispatcher != null ? dispatcher._lastObservedVramBytes : 0L;
            }
        }

        internal static void ForceEvaluateUiMipBiasGate()
        {
            AssetLoadDispatcher dispatcher = s_registeredInstance;
            if (dispatcher != null)
                dispatcher.QueueUiMipBiasGateEvaluation();
        }

        internal static void RegisterAddressableGroup(uint assetKey, AddressableAssetGroupKind group)
        {
            AssetLoadDispatcher dispatcher = s_registeredInstance;
            if (dispatcher == null || assetKey == 0u)
                return;

            dispatcher.RegisterAddressableGroupInternal(assetKey, group);
        }

#if UNITY_ADDRESSABLES_EXIST
        internal void MarkAddressableDependencyGroupReady(
            uint groupHash,
            int dependencyOrder,
            AsyncOperationHandle handle)
        {
            if (groupHash == 0u || !handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
                return;

            RegisterAddressableGroupInternal(groupHash, AddressableAssetGroupKind.Unknown);
            _lastAddressableDependencyGroupHash = groupHash;
            _lastAddressableDependencyOrder = dependencyOrder;
            _addressableDependencyGroupReadyCount++;
        }
#endif

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!TryRegisterService())
                return;

            EnsureProgressSignalLaneCold();
            RefreshGraphicsBudgetBytes();
            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void Start()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredService && !TryRegisterService())
                return;

            EnsureProgressSignalLaneCold();
            RefreshGraphicsBudgetBytes();
            CacheDependencies();
            TryRegisterHotSwap();
            TryRegister();
        }

        private void OnDisable()
        {
            ClearUiMipBiasGate();
            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearProgressSignalBuffer();
            ClearCachedDependencies();
        }

        private void OnDestroy()
        {
            ClearUiMipBiasGate();
            TryUnregister();
            TryUnregisterHotSwap();
            TryUnregisterService();
            ClearCachedDependencies();
            ClearDispatchBuffers();
            ClearAddressableGroupMap();
            ClearProgressSignalBuffer();

            for (int i = 0; i < _inflightCounts.Length; i++)
                _inflightCounts[i] = 0;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            AgeQueuedRequests();
            DispatchWithinBudget();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!_uiMipBiasGateEvaluationQueued && !_uiMipBiasGateActive)
                return;

            _uiMipBiasGateEvaluationQueued = false;
            EvaluateUiMipBiasGate();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            FlushProgressSignalsLateFrame();
        }

        internal bool Enqueue(uint assetKey, AssetPriorityTier priority, bool isDistantHlod, out int requestId)
        {
            return Enqueue(assetKey, priority, isDistantHlod, 0L, out requestId);
        }

        internal bool Enqueue(uint assetKey, AssetPriorityTier priority, bool isDistantHlod, long estimatedBytes, out int requestId)
        {
            requestId = 0;
            long resolvedEstimatedBytes = ResolveDispatchPayloadBytes(estimatedBytes);
            if (IsUiIconGroup(assetKey))
                QueueUiMipBiasGateEvaluation();

            for (int i = 0; i < _queuedRequestCount; i++)
            {
                if (_queuedRequests[i].AssetKey != assetKey)
                    continue;

                AssetDispatchRequest queued = _queuedRequests[i];
                if (resolvedEstimatedBytes > queued.EstimatedBytes)
                {
                    queued.EstimatedBytes = resolvedEstimatedBytes;
                    _queuedRequests[i] = queued;
                }

                requestId = _queuedRequests[i].RequestId;
                return true;
            }

            for (int i = 0; i < _inflightRequestCount; i++)
            {
                if (_inflightRequests[i].AssetKey != assetKey)
                    continue;

                requestId = _inflightRequests[i].RequestId;
                return true;
            }

            if (_readyTicketCount >= ResolveReadyTicketLimit() ||
                _queuedRequestCount >= _queuedRequests.Length)
            {
                return false;
            }

            requestId = _nextRequestId++;
            _queuedRequests[_queuedRequestCount++] = new AssetDispatchRequest
            {
                RequestId = requestId,
                AssetKey = assetKey,
                EstimatedBytes = resolvedEstimatedBytes,
                Priority = priority,
                IsDistantHlod = isDistantHlod ? (byte)1 : (byte)0,
                AgeFrames = 0
            };
            QueueProgressSignal(requestId, assetKey, resolvedEstimatedBytes, priority, AssetLoadProgressSignal.StageQueued, 0);
            return true;
        }

        internal bool TryDequeueReadyTicket(out AssetDispatchTicket ticket)
        {
            if (_readyTicketCount == 0)
            {
                ticket = default;
                return false;
            }

            int lastIndex = --_readyTicketCount;
            ticket = _readyTickets[lastIndex];
            _readyTickets[lastIndex] = default;
            return true;
        }

        internal bool TryConsumeReadyTicketByAssetKey(uint assetKey, out AssetDispatchTicket ticket)
        {
            for (int i = _readyTicketCount - 1; i >= 0; i--)
            {
                if (_readyTickets[i].AssetKey != assetKey)
                    continue;

                ticket = _readyTickets[i];
                RemoveReadyTicketAtSwapBack(i);
                return true;
            }

            ticket = default;
            return false;
        }

        internal bool AcknowledgeDispatchRequest(int requestId, bool success)
        {
            for (int i = 0; i < _inflightRequestCount; i++)
            {
                AssetDispatchRequest request = _inflightRequests[i];
                if (request.RequestId != requestId)
                    continue;

                int band = ResolveBand(request.Priority);
                if (_inflightCounts[band] > 0)
                    _inflightCounts[band]--;

                QueueProgressSignal(
                    request.RequestId,
                    request.AssetKey,
                    request.EstimatedBytes,
                    request.Priority,
                    success ? AssetLoadProgressSignal.StageCompleted : AssetLoadProgressSignal.StageFailed,
                    0);
                RemoveInflightRequestAtSwapBack(i);
                return true;
            }

            return false;
        }

        internal void CancelByAssetKey(uint assetKey)
        {
            for (int i = _queuedRequestCount - 1; i >= 0; i--)
            {
                if (_queuedRequests[i].AssetKey == assetKey)
                {
                    AssetDispatchRequest request = _queuedRequests[i];
                    QueueProgressSignal(request.RequestId, request.AssetKey, request.EstimatedBytes, request.Priority, AssetLoadProgressSignal.StageCancelled, 0);
                    RemoveQueuedRequestAtSwapBack(i);
                }
            }

            for (int i = _readyTicketCount - 1; i >= 0; i--)
            {
                if (_readyTickets[i].AssetKey == assetKey)
                {
                    AssetDispatchTicket ticket = _readyTickets[i];
                    QueueProgressSignal(ticket.RequestId, ticket.AssetKey, ticket.EstimatedBytes, ticket.Priority, AssetLoadProgressSignal.StageCancelled, 0);
                    RemoveReadyTicketAtSwapBack(i);
                }
            }

            for (int i = _inflightRequestCount - 1; i >= 0; i--)
            {
                if (_inflightRequests[i].AssetKey != assetKey)
                    continue;

                int band = ResolveBand(_inflightRequests[i].Priority);
                if (_inflightCounts[band] > 0)
                    _inflightCounts[band]--;

                AssetDispatchRequest request = _inflightRequests[i];
                QueueProgressSignal(request.RequestId, request.AssetKey, request.EstimatedBytes, request.Priority, AssetLoadProgressSignal.StageCancelled, 0);
                RemoveInflightRequestAtSwapBack(i);
            }
        }

        internal static void ForceDrainDeferredReleases()
        {
            AssetLoadDispatcher dispatcher = s_registeredInstance;
            if (dispatcher != null)
                dispatcher.ForceDrainDeferredReleasesCached();
        }

        private void ForceDrainDeferredReleasesCached()
        {
            IAssetLifecyclePressureSink governor = _assetLifecycle;
            if (governor != null)
                governor.ForceDrainPendingReleaseQueue();
        }

        private void RegisterAddressableGroupInternal(uint assetKey, AddressableAssetGroupKind group)
        {
            if (assetKey == 0u)
                return;

            byte groupValue = (byte)group;
            for (int i = 0; i < _addressableGroupCount; i++)
            {
                if (_addressableGroupKeys[i] != assetKey)
                    continue;

                _addressableGroupValues[i] = groupValue;
                return;
            }

            if (_addressableGroupCount < _addressableGroupKeys.Length)
            {
                int writeIndex = _addressableGroupCount++;
                _addressableGroupKeys[writeIndex] = assetKey;
                _addressableGroupValues[writeIndex] = groupValue;
                return;
            }

            if (group != AddressableAssetGroupKind.UIIcons)
                return;

            for (int i = 0; i < _addressableGroupCount; i++)
            {
                if (_addressableGroupValues[i] == (byte)AddressableAssetGroupKind.UIIcons)
                    continue;

                _addressableGroupKeys[i] = assetKey;
                _addressableGroupValues[i] = groupValue;
                return;
            }

            int replacementIndex = (int)(assetKey % (uint)_addressableGroupKeys.Length);
            _addressableGroupKeys[replacementIndex] = assetKey;
            _addressableGroupValues[replacementIndex] = groupValue;
        }

        private bool IsUiIconGroup(uint assetKey)
        {
            if (assetKey == 0u)
                return false;

            for (int i = 0; i < _addressableGroupCount; i++)
            {
                if (_addressableGroupKeys[i] == assetKey)
                    return _addressableGroupValues[i] == (byte)AddressableAssetGroupKind.UIIcons;
            }

            return false;
        }

        private void EvaluateUiMipBiasGate()
        {
            IVramBudgetReadModel monitor = _vramMonitor;
            IVramPressureMipBiasSink pressureMipBias = _vramPressureMipBias;
            if (monitor == null || pressureMipBias == null)
                return;

            monitor.GetVRAMBreakdown(out _, out _, out long totalVramBytes);
            _lastObservedVramBytes = totalVramBytes;

            long graphicsBudgetBytes = _graphicsBudgetBytes > 0L
                ? _graphicsBudgetBytes
                : ResolveGraphicsBudgetBytes(0);
            float vramPressure = ResolveVramPressureFactor(totalVramBytes, graphicsBudgetBytes);
            float gateResponse = ResolveUiMipGateResponse(vramPressure);
            int mipDelta = ResolveUiMipGateDelta(gateResponse);

            if (mipDelta > 0)
            {
                ApplyUiMipBiasGate(pressureMipBias, totalVramBytes, gateResponse);
                return;
            }

            if (_uiMipBiasGateActive && vramPressure <= ResolveUiMipRestoreFraction())
            {
                RestoreUiMipBiasGate(pressureMipBias);
                return;
            }

            if (_uiMipBiasGateActive)
                pressureMipBias.SetExternalMipPressureResponse(gateResponse, totalVramBytes);
        }

        private void QueueUiMipBiasGateEvaluation()
        {
            _uiMipBiasGateEvaluationQueued = true;
        }

        private void ApplyUiMipBiasGate(IVramPressureMipBiasSink pressureMonitor, long observedVramBytes, float gateResponse)
        {
            pressureMonitor.SetExternalMipPressureResponse(gateResponse, observedVramBytes);

            if (_uiMipBiasGateActive)
                return;

            _uiMipBiasGateActive = true;
            GlobalTelemetryBus.PublishPerformanceWarning(UiMipGateHighHash, UiTextureContextHash, observedVramBytes * (1f / BytesPerMegabyte));
        }

        private void RestoreUiMipBiasGate(IVramPressureMipBiasSink pressureMonitor)
        {
            pressureMonitor.SetExternalMipPressureResponse(0f, _lastObservedVramBytes);
            _uiMipBiasGateActive = false;
            GlobalTelemetryBus.PublishPerformanceWarning(UiMipGateRestoreHash, UiTextureContextHash, _lastObservedVramBytes * (1f / BytesPerMegabyte));
        }

        private void ClearUiMipBiasGate()
        {
            _uiMipBiasGateEvaluationQueued = false;
            if (!_uiMipBiasGateActive)
                return;

            IVramPressureMipBiasSink pressureMonitor = _vramPressureMipBias;
            if (pressureMonitor != null)
                pressureMonitor.SetExternalMipPressureResponse(0f, _lastObservedVramBytes);

            _uiMipBiasGateActive = false;
        }

        private void TryRegister()
        {
            if (_registeredTick && _registeredSlowTick && _registeredLateFrameTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, this))
                return;

            CacheDependencies();
            if (!_registeredTick)
                _registeredTick = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
            if (!_registeredLateFrameTick)
            {
                EnsureProgressSignalLaneCold();
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            }
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
            {
                if (ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, this))
                    s_registeredInstance = this;
                return true;
            }
            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterAssetLoadDispatcherRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, this);
            if (_registeredService)
                s_registeredInstance = this;
            return _registeredService;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            AssetLoadDispatcher registered = GlobalRegistry.AssetLoadDispatcher;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                if (IsAssetLoadDispatcherRuntimeUsable(registered))
                {
                    s_registeredInstance = registered;
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_registeredInstance, registered))
                    s_registeredInstance = null;
                GlobalRegistry.UnregisterAssetLoadDispatcherRuntime(registered);
            }

            AssetLoadDispatcher active = s_registeredInstance;
            if (ReferenceEquals(active, null) || ReferenceEquals(active, this))
                return false;

            if (IsAssetLoadDispatcherRuntimeUsable(active))
            {
                if (!ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, active))
                    GlobalRegistry.RegisterAssetLoadDispatcherRuntime(active);
                Destroy(gameObject);
                return true;
            }

            if (ReferenceEquals(s_registeredInstance, active))
                s_registeredInstance = null;
            if (ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, active))
                GlobalRegistry.UnregisterAssetLoadDispatcherRuntime(active);
            return false;
        }

        private static bool IsAssetLoadDispatcherRuntimeUsable(AssetLoadDispatcher dispatcher)
        {
            return dispatcher != null &&
                   dispatcher._registeredService &&
                   dispatcher.isActiveAndEnabled;
        }

        private void TryUnregister()
        {
            if (_registeredTick)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
                _registeredSlowTick = false;
            }

            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrameTick = false;
            }
        }

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

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterAssetLoadDispatcherRuntime(this);
            if (ReferenceEquals(s_registeredInstance, this))
                s_registeredInstance = null;
            _registeredService = false;
        }

        private void CacheDependencies()
        {
            if (_vramMonitor == null)
                _vramMonitor = GlobalRegistry.VRAMBudgetReadModel;
            if (_vramPressure == null)
                _vramPressure = GlobalRegistry.VRAMPressureReadModel;
            if (_vramPressureMipBias == null)
                _vramPressureMipBias = GlobalRegistry.VRAMPressureMipBiasSink;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecyclePressureSink;
        }

        private void ClearCachedDependencies()
        {
            _vramMonitor = null;
            _vramPressure = null;
            _vramPressureMipBias = null;
            _assetLifecycle = null;
        }

        private void RefreshGraphicsBudgetBytes()
        {
            _graphicsBudgetBytes = ResolveGraphicsBudgetBytes(SystemInfo.graphicsMemorySize);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as IVramPressureReadModel;
                    _vramPressureMipBias = currentService as IVramPressureMipBiasSink;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as IAssetLifecyclePressureSink;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void AgeQueuedRequests()
        {
            for (int i = 0; i < _queuedRequestCount; i++)
            {
                ref AssetDispatchRequest request = ref _queuedRequests[i];
                request.AgeFrames++;
            }
        }

        private void DispatchWithinBudget()
        {
            int readyTicketLimit = ResolveReadyTicketLimit();
            if (_queuedRequestCount == 0 ||
                readyTicketLimit <= 0 ||
                _readyTicketCount >= readyTicketLimit ||
                _inflightRequestCount >= _inflightRequests.Length)
            {
                return;
            }

            long dispatchStartTicks = Stopwatch.GetTimestamp();
            BeginFrameUploadBudget();
            while (_queuedRequestCount > 0 &&
                   _readyTicketCount < readyTicketLimit &&
                   _inflightRequestCount < _inflightRequests.Length)
            {
                float elapsedMilliseconds = (float)((Stopwatch.GetTimestamp() - dispatchStartTicks) * 1000.0 / Stopwatch.Frequency);
                if (elapsedMilliseconds >= dispatchBudgetMilliseconds)
                    break;

                int requestIndex = FindNextDispatchableRequestIndex();
                if (requestIndex < 0)
                    break;

                AssetDispatchRequest request = _queuedRequests[requestIndex];
                long requestBytes = ResolveDispatchPayloadBytes(request.EstimatedBytes);
                long nextGrantedBytes = _frameUploadGrantedBytes + requestBytes;
                if (_frameUploadGrantedBytes > 0L && nextGrantedBytes > _frameUploadBudgetBytes)
                    break;

                RemoveQueuedRequestAtSwapBack(requestIndex);
                _frameUploadGrantedBytes = nextGrantedBytes;

                _readyTickets[_readyTicketCount++] = new AssetDispatchTicket
                {
                    RequestId = request.RequestId,
                    AssetKey = request.AssetKey,
                    EstimatedBytes = requestBytes,
                    Priority = request.Priority,
                    IsDistantHlod = request.IsDistantHlod
                };

                request.EstimatedBytes = requestBytes;
                _inflightRequests[_inflightRequestCount++] = request;
                _inflightCounts[ResolveBand(request.Priority)]++;
                QueueProgressSignal(
                    request.RequestId,
                    request.AssetKey,
                    requestBytes,
                    request.Priority,
                    AssetLoadProgressSignal.StageGranted,
                    0);
            }
        }

        private int FindNextDispatchableRequestIndex()
        {
            int bestIndex = -1;
            int bestPriority = int.MaxValue;
            int bestAge = -1;

            for (int i = 0; i < _queuedRequestCount; i++)
            {
                AssetDispatchRequest request = _queuedRequests[i];
                int band = ResolveBand(request.Priority);
                int allowedLoads = ResolveAllowedConcurrentLoads(request.Priority);
                if (_inflightCounts[band] >= allowedLoads)
                    continue;

                int effectivePriority = (int)(byte)request.Priority;
                if (request.AgeFrames > StarvationFrameThreshold && effectivePriority > 0)
                    effectivePriority--;

                if (bestIndex < 0 || effectivePriority < bestPriority ||
                    (effectivePriority == bestPriority && request.AgeFrames > bestAge))
                {
                    bestIndex = i;
                    bestPriority = effectivePriority;
                    bestAge = request.AgeFrames;
                }
            }

            return bestIndex;
        }

        private static int ResolveBand(AssetPriorityTier priority)
        {
            byte priorityByte = (byte)priority;
            if (priorityByte <= (byte)AssetPriorityTier.Tier1Equipped)
                return 0;
            if (priority == AssetPriorityTier.Tier2Proximity)
                return 1;
            if (priorityByte <= (byte)AssetPriorityTier.Tier4MidRange)
                return 2;

            return 3;
        }

        private int ResolveReadyTicketLimit()
        {
            return Mathf.Clamp(maxReadyTicketCount, 0, _readyTickets.Length);
        }

        private void RemoveQueuedRequestAtSwapBack(int index)
        {
            int lastIndex = --_queuedRequestCount;
            _queuedRequests[index] = _queuedRequests[lastIndex];
            _queuedRequests[lastIndex] = default;
        }

        private void RemoveReadyTicketAtSwapBack(int index)
        {
            int lastIndex = --_readyTicketCount;
            _readyTickets[index] = _readyTickets[lastIndex];
            _readyTickets[lastIndex] = default;
        }

        private void RemoveInflightRequestAtSwapBack(int index)
        {
            int lastIndex = --_inflightRequestCount;
            _inflightRequests[index] = _inflightRequests[lastIndex];
            _inflightRequests[lastIndex] = default;
        }

        private void ClearDispatchBuffers()
        {
            System.Array.Clear(_queuedRequests, 0, _queuedRequestCount);
            System.Array.Clear(_readyTickets, 0, _readyTicketCount);
            System.Array.Clear(_inflightRequests, 0, _inflightRequestCount);
            _queuedRequestCount = 0;
            _readyTicketCount = 0;
            _inflightRequestCount = 0;
        }

        private void ClearProgressSignalBuffer()
        {
            System.Array.Clear(_progressSignals, 0, _progressSignalCount);
            _progressSignalCount = 0;
        }

        private void ClearAddressableGroupMap()
        {
            System.Array.Clear(_addressableGroupKeys, 0, _addressableGroupCount);
            System.Array.Clear(_addressableGroupValues, 0, _addressableGroupCount);
            _addressableGroupCount = 0;
        }

        private int ResolveAllowedConcurrentLoads(AssetPriorityTier priority)
        {
            int band = ResolveBand(priority);
            IVramPressureReadModel pressureMonitor = _vramPressure;
            float ramPressure = pressureMonitor != null ? pressureMonitor.RamPressureFactor : 0f;
            float totalPressure = pressureMonitor != null ? pressureMonitor.PressureFactor : ramPressure;
            float pressure = math.saturate(math.select(0f, totalPressure, math.isfinite(totalPressure)));
            float quality = ResolveGlobalQualityWeight();

            switch (band)
            {
                case 0:
                    return ResolveContinuousLoadSlots(new ContinuousLoadConfig
                    {
                        MaxSlots = Tier01Slots,
                        MinSlots = math.max(1, Tier01Slots >> 1),
                        Quality = quality,
                        Pressure = pressure,
                        PressureStart = 0.90f,
                        PressureEnd = 1f
                    });

                case 1:
                    return ResolveContinuousLoadSlots(new ContinuousLoadConfig
                    {
                        MaxSlots = Tier2Slots,
                        MinSlots = 1,
                        Quality = quality,
                        Pressure = pressure,
                        PressureStart = 0.75f,
                        PressureEnd = 0.98f
                    });

                case 2:
                    return ResolveContinuousLoadSlots(new ContinuousLoadConfig
                    {
                        MaxSlots = Tier34Slots,
                        MinSlots = Tier34CriticalSlots,
                        Quality = quality,
                        Pressure = pressure,
                        PressureStart = 0.55f,
                        PressureEnd = 0.95f
                    });

                default:
                    return ResolveContinuousLoadSlots(new ContinuousLoadConfig
                    {
                        MaxSlots = Tier56Slots,
                        MinSlots = Tier56WarningSlots,
                        Quality = quality,
                        Pressure = pressure,
                        PressureStart = 0.35f,
                        PressureEnd = 0.85f
                    });
            }
        }

        private struct ContinuousLoadConfig
        {
            public int MaxSlots;
            public int MinSlots;
            public float Quality;
            public float Pressure;
            public float PressureStart;
            public float PressureEnd;
        }

        private static int ResolveContinuousLoadSlots(in ContinuousLoadConfig config)
        {
            float pressureCollapse = math.smoothstep(config.PressureStart, config.PressureEnd, config.Pressure);
            float qualityCollapse = 1f - math.smoothstep(0.15f, 0.85f, config.Quality);
            float collapse = math.saturate(math.lerp(pressureCollapse, math.max(pressureCollapse, qualityCollapse), 0.5f));
            float rawSlots = math.lerp(config.MaxSlots, config.MinSlots, collapse);
            return math.max(config.MinSlots, (int)math.round(rawSlots));
        }

        public static long ResolveUploadBudgetBytesForAudit(float qualityWeight, float pressureFactor)
        {
            float quality = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
            float pressure = math.saturate(math.select(0f, pressureFactor, math.isfinite(pressureFactor)));
            float qualityCurve = math.smoothstep(0.15f, 0.85f, quality);
            float pressureCollapse = math.smoothstep(0.55f, 0.98f, pressure);
            float qualityBudget = math.lerp((float)LowFrameUploadBudgetBytes, (float)UltraFrameUploadBudgetBytes, qualityCurve);
            float pressureBudget = math.lerp(qualityBudget, (float)MinimumFrameUploadBudgetBytes, pressureCollapse);
            return (long)math.max((float)MinimumFrameUploadBudgetBytes, pressureBudget);
        }

        private void BeginFrameUploadBudget()
        {
            uint frame = SystemDispatcher.CurrentFrameId;
            if (_uploadBudgetFrameId == frame)
                return;

            _uploadBudgetFrameId = frame;
            IVramPressureReadModel pressureMonitor = _vramPressure;
            float pressure = pressureMonitor != null ? pressureMonitor.PressureFactor : 0f;
            _frameUploadBudgetBytes = ResolveUploadBudgetBytesForAudit(ResolveGlobalQualityWeight(), pressure);
            _frameUploadGrantedBytes = 0L;
        }

        private static long ResolveDispatchPayloadBytes(long estimatedBytes)
        {
            if (estimatedBytes <= 0L)
                return UnknownDispatchPayloadBytes;

            return estimatedBytes;
        }

        private static void EnsureProgressSignalLaneCold()
        {
            if (!Application.isPlaying)
                return;

            SignalBus<AssetLoadProgressSignal>.Configure(
                AssetLoadProgressSignal.ExpectedCapacity,
                AssetLoadProgressSignal.MaxFrameSignals,
                AssetLoadProgressSignal.LowTierFrameSignals,
                AssetLoadProgressSignal.LaneHash);
            SignalBus<AssetLoadProgressSignal>.EnsureInitialized();
        }

        private void QueueProgressSignal(
            int requestId,
            uint assetKey,
            long estimatedBytes,
            AssetPriorityTier priority,
            byte stage,
            byte flags)
        {
            if (_progressSignalCount >= _progressSignals.Length)
            {
                IncrementProgressSignalDropCounter();
                return;
            }

            _progressSignals[_progressSignalCount++] = new AssetLoadProgressSignal
            {
                Frame = SystemDispatcher.CurrentFrameId,
                AssetKey = assetKey,
                EstimatedBytes = ResolveDispatchPayloadBytes(estimatedBytes),
                RequestId = requestId,
                UploadBudgetMb = BytesToPositiveMegabytes(_frameUploadBudgetBytes),
                GrantedFrameMb = BytesToPositiveMegabytes(_frameUploadGrantedBytes),
                Stage = stage,
                Priority = (byte)priority,
                Flags = flags
            };
        }

        private void FlushProgressSignalsLateFrame()
        {
            int count = _progressSignalCount;
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                AssetLoadProgressSignal signal = _progressSignals[i];
                _progressSignals[i] = default;
                SignalBus<AssetLoadProgressSignal>.TryPushTracked(
                    in signal,
                    ref s_x001AssetLoadDispatcherProgressSignalDropCount);
            }

            _progressSignalCount = 0;
        }

        private static void IncrementProgressSignalDropCounter()
        {
            if (s_x001AssetLoadDispatcherProgressSignalDropCount < int.MaxValue)
                s_x001AssetLoadDispatcherProgressSignalDropCount++;
        }

        private static uint BytesToPositiveMegabytes(long bytes)
        {
            if (bytes <= 0L)
                return 0u;

            long megabytes = bytes / BytesPerMegabyte;
            return megabytes > uint.MaxValue ? uint.MaxValue : (uint)megabytes;
        }

        private static long ResolveGraphicsBudgetBytes(int graphicsMemoryMb)
        {
            int detectedMb = math.max(graphicsMemoryMb, 0);
            int budgetMb = math.select(UnknownGraphicsBudgetMb, detectedMb, detectedMb > 0);
            return (long)budgetMb * BytesPerMegabyte;
        }

        private static float ResolveVramPressureFactor(long observedVramBytes, long graphicsBudgetBytes)
        {
            float denominator = math.max((float)graphicsBudgetBytes, 1f);
            float pressure = math.saturate(observedVramBytes / denominator);
            return math.select(1f, pressure, graphicsBudgetBytes > 0L);
        }

        private static float ResolveUiMipGateResponse(float vramPressure)
        {
            return ResolvePressureResponse(ResolveUiMipDowngradeFraction(), vramPressure);
        }

        private static int ResolveUiMipGateDelta(float gateResponse)
        {
            return math.clamp((int)math.round(math.lerp(0f, 2f, math.saturate(gateResponse))), 0, 2);
        }

        private static float ResolveUiMipDowngradeFraction()
        {
            return ResolveQualityAdjustedFraction(math.max(0.45f, UiMipDowngradePressureFraction - 0.20f), UiMipDowngradePressureFraction);
        }

        private static float ResolveUiMipRestoreFraction()
        {
            return ResolveQualityAdjustedFraction(math.max(0.25f, UiMipRestorePressureFraction - 0.12f), UiMipRestorePressureFraction);
        }

        private static float ResolvePressureResponse(float startFraction, float pressureFactor)
        {
            float start = math.min(math.saturate(startFraction), 0.9999f);
            return math.smoothstep(start, 1f, math.saturate(pressureFactor));
        }

        private static float ResolveQualityAdjustedFraction(float lowQualityFraction, float highQualityFraction)
        {
            float quality = ResolveGlobalQualityWeight();
            float qualityCurve = math.smoothstep(0.15f, 0.85f, quality);
            return math.saturate(math.lerp(lowQualityFraction, highQualityFraction, qualityCurve));
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }
    }
}
