using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;
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
    public sealed class AssetLoadDispatcher : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const int Tier01Slots = 8;
        private const int Tier2Slots = 6;
        private const int Tier34Slots = 4;
        private const int Tier34CriticalSlots = 1;
        private const int Tier56Slots = 2;
        private const int Tier56WarningSlots = 0;
        private const int StarvationFrameThreshold = 60;
        private const long BytesPerMegabyte = 1024L * 1024L;
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
        private static AssetLoadDispatcher s_registeredInstance;

        [Header("Dispatch Budget")]
        [Tooltip("Main-thread dispatch budget in milliseconds per frame.")]
        [SerializeField] private float dispatchBudgetMilliseconds = 2f;

        [Tooltip("Maximum ready tickets retained for backend consumers.")]
        [SerializeField] private int maxReadyTicketCount = 32;

        private bool _registeredTick;
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
        private int _addressableGroupCount;
        private long _lastObservedVramBytes;
        private long _graphicsBudgetBytes;
        private bool _uiMipBiasGateActive;
        private VRAMMonitor _vramMonitor;
        private VRAMPressureMonitor _vramPressure;
        private AssetLifecycleGovernor _assetLifecycle;
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
                dispatcher.EvaluateUiMipBiasGate();
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
            RefreshGraphicsBudgetBytes();
            CacheDependencies();
            TryRegisterHotSwap();
            if (TryRegisterService())
                TryRegister();
        }

        private void Start()
        {
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

            for (int i = 0; i < _inflightCounts.Length; i++)
                _inflightCounts[i] = 0;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            EvaluateUiMipBiasGate();
            AgeQueuedRequests();
            DispatchWithinBudget();
        }

        internal bool Enqueue(uint assetKey, AssetPriorityTier priority, bool isDistantHlod, out int requestId)
        {
            requestId = 0;
            if (IsUiIconGroup(assetKey))
                EvaluateUiMipBiasGate();

            for (int i = 0; i < _queuedRequestCount; i++)
            {
                if (_queuedRequests[i].AssetKey != assetKey)
                    continue;

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
                Priority = priority,
                IsDistantHlod = isDistantHlod,
                AgeFrames = 0
            };
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
                    RemoveQueuedRequestAtSwapBack(i);
            }

            for (int i = _readyTicketCount - 1; i >= 0; i--)
            {
                if (_readyTickets[i].AssetKey == assetKey)
                    RemoveReadyTicketAtSwapBack(i);
            }

            for (int i = _inflightRequestCount - 1; i >= 0; i--)
            {
                if (_inflightRequests[i].AssetKey != assetKey)
                    continue;

                int band = ResolveBand(_inflightRequests[i].Priority);
                if (_inflightCounts[band] > 0)
                    _inflightCounts[band]--;

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
            AssetLifecycleGovernor governor = _assetLifecycle;
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
            VRAMMonitor monitor = _vramMonitor;
            VRAMPressureMonitor pressureMonitor = _vramPressure;
            if (monitor == null || pressureMonitor == null)
                return;

            monitor.GetVRAMBreakdown(out _, out _, out long totalVramBytes);
            _lastObservedVramBytes = totalVramBytes;

            if (_graphicsBudgetBytes <= 0L)
                RefreshGraphicsBudgetBytes();

            long graphicsBudgetBytes = _graphicsBudgetBytes;
            float vramPressure = ResolveVramPressureFactor(totalVramBytes, graphicsBudgetBytes);
            float gateResponse = ResolveUiMipGateResponse(vramPressure);
            int mipDelta = ResolveUiMipGateDelta(gateResponse);

            if (mipDelta > 0)
            {
                ApplyUiMipBiasGate(pressureMonitor, totalVramBytes, gateResponse);
                return;
            }

            if (_uiMipBiasGateActive && vramPressure <= ResolveUiMipRestoreFraction())
            {
                RestoreUiMipBiasGate(pressureMonitor);
                return;
            }

            if (_uiMipBiasGateActive)
                pressureMonitor.SetExternalMipPressureResponse(gateResponse, totalVramBytes);
        }

        private void ApplyUiMipBiasGate(VRAMPressureMonitor pressureMonitor, long observedVramBytes, float gateResponse)
        {
            pressureMonitor.SetExternalMipPressureResponse(gateResponse, observedVramBytes);

            if (_uiMipBiasGateActive)
                return;

            _uiMipBiasGateActive = true;
            GlobalTelemetryBus.PublishPerformanceWarning(UiMipGateHighHash, UiTextureContextHash, observedVramBytes / (float)BytesPerMegabyte);
        }

        private void RestoreUiMipBiasGate(VRAMPressureMonitor pressureMonitor)
        {
            pressureMonitor.SetExternalMipPressureResponse(0f, _lastObservedVramBytes);
            _uiMipBiasGateActive = false;
            GlobalTelemetryBus.PublishPerformanceWarning(UiMipGateRestoreHash, UiTextureContextHash, _lastObservedVramBytes / (float)BytesPerMegabyte);
        }

        private void ClearUiMipBiasGate()
        {
            if (!_uiMipBiasGateActive)
                return;

            VRAMPressureMonitor pressureMonitor = _vramPressure;
            if (pressureMonitor != null)
                pressureMonitor.SetExternalMipPressureResponse(0f, _lastObservedVramBytes);

            _uiMipBiasGateActive = false;
        }

        private void TryRegister()
        {
            if (_registeredTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, this))
                return;

            CacheDependencies();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = GlobalRegistry.Updatables.Contains(this);
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

            AssetLoadDispatcher registered = GlobalRegistry.AssetLoadDispatcher;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterAssetLoadDispatcherRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.AssetLoadDispatcher, this);
            if (_registeredService)
                s_registeredInstance = this;
            return _registeredService;
        }

        private void TryUnregister()
        {
            if (!_registeredTick)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredTick = false;
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
                _vramMonitor = GlobalRegistry.VRAMMonitor;
            if (_vramPressure == null)
                _vramPressure = GlobalRegistry.VRAMPressure;
            if (_assetLifecycle == null)
                _assetLifecycle = GlobalRegistry.AssetLifecycle;
        }

        private void ClearCachedDependencies()
        {
            _vramMonitor = null;
            _vramPressure = null;
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
                    _vramMonitor = currentService as VRAMMonitor;
                    break;
                case GlobalRegistryServiceSlot.VRAMPressureRuntime:
                    _vramPressure = currentService as VRAMPressureMonitor;
                    break;
                case GlobalRegistryServiceSlot.AssetLifecycleRuntime:
                    _assetLifecycle = currentService as AssetLifecycleGovernor;
                    break;
            }
        }

        private void AgeQueuedRequests()
        {
            for (int i = 0; i < _queuedRequestCount; i++)
            {
                AssetDispatchRequest request = _queuedRequests[i];
                request.AgeFrames++;
                _queuedRequests[i] = request;
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

            float dispatchStart = Time.realtimeSinceStartup;
            while (_queuedRequestCount > 0 &&
                   _readyTicketCount < readyTicketLimit &&
                   _inflightRequestCount < _inflightRequests.Length)
            {
                float elapsedMilliseconds = (Time.realtimeSinceStartup - dispatchStart) * 1000f;
                if (elapsedMilliseconds >= dispatchBudgetMilliseconds)
                    break;

                int requestIndex = FindNextDispatchableRequestIndex();
                if (requestIndex < 0)
                    break;

                AssetDispatchRequest request = _queuedRequests[requestIndex];
                RemoveQueuedRequestAtSwapBack(requestIndex);

                _readyTickets[_readyTicketCount++] = new AssetDispatchTicket
                {
                    RequestId = request.RequestId,
                    AssetKey = request.AssetKey,
                    Priority = request.Priority,
                    IsDistantHlod = request.IsDistantHlod
                };

                _inflightRequests[_inflightRequestCount++] = request;
                _inflightCounts[ResolveBand(request.Priority)]++;
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

        private void ClearAddressableGroupMap()
        {
            System.Array.Clear(_addressableGroupKeys, 0, _addressableGroupCount);
            System.Array.Clear(_addressableGroupValues, 0, _addressableGroupCount);
            _addressableGroupCount = 0;
        }

        private int ResolveAllowedConcurrentLoads(AssetPriorityTier priority)
        {
            int band = ResolveBand(priority);
            VRAMPressureMonitor pressureMonitor = _vramPressure;
            float ramPressure = pressureMonitor != null ? pressureMonitor.RamPressureFactor : 0f;
            float totalPressure = pressureMonitor != null ? pressureMonitor.PressureFactor : ramPressure;
            float pressure = math.saturate(math.isfinite(totalPressure) ? totalPressure : 0f);
            float quality = ResolveGlobalQualityWeight();

            switch (band)
            {
                case 0:
                    return ResolveContinuousLoadSlots(Tier01Slots, math.max(1, Tier01Slots >> 1), quality, pressure, 0.90f, 1f);

                case 1:
                    return ResolveContinuousLoadSlots(Tier2Slots, 1, quality, pressure, 0.75f, 0.98f);

                case 2:
                    return ResolveContinuousLoadSlots(Tier34Slots, Tier34CriticalSlots, quality, pressure, 0.55f, 0.95f);

                default:
                    return ResolveContinuousLoadSlots(Tier56Slots, Tier56WarningSlots, quality, pressure, 0.35f, 0.85f);
            }
        }

        private static int ResolveContinuousLoadSlots(int maxSlots, int minSlots, float quality, float pressure, float pressureStart, float pressureEnd)
        {
            float pressureCollapse = math.smoothstep(pressureStart, pressureEnd, pressure);
            float qualityCollapse = 1f - math.smoothstep(0.15f, 0.85f, quality);
            float collapse = math.saturate(math.lerp(pressureCollapse, math.max(pressureCollapse, qualityCollapse), 0.5f));
            float rawSlots = math.lerp(maxSlots, minSlots, collapse);
            return math.max(minSlots, (int)math.round(rawSlots));
        }

        private static long ResolveGraphicsBudgetBytes(int graphicsMemoryMb)
        {
            int budgetMb = graphicsMemoryMb > 0 ? graphicsMemoryMb : UnknownGraphicsBudgetMb;
            return (long)budgetMb * BytesPerMegabyte;
        }

        private static float ResolveVramPressureFactor(long observedVramBytes, long graphicsBudgetBytes)
        {
            if (graphicsBudgetBytes <= 0L)
                return 1f;

            return math.saturate(observedVramBytes / (float)graphicsBudgetBytes);
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
            float start = math.saturate(startFraction);
            if (start >= 1f)
                start = 0.9999f;

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
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }
    }
}
