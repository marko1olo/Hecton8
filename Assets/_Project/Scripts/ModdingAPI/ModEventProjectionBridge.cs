using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Modding
{
    /// <summary>
    /// Projects selected first-party native signal snapshots into managed mod callbacks without exposing native ownership.
    /// </summary>
    internal sealed class ModEventProjectionBridge : IModdingBridge, ILateFrameTickable, IHectonEventChannel, IGlobalRegistryHotSwapListener
    {
        private const string NativeMemoryOwner = nameof(ModEventProjectionBridge);
        private const int HighTierProjectionCap = 50;
        private const int LowTierProjectionCap = 10;
        private const float LowProjectionQualityFlagThreshold01 = 0.3f;
        private const int BlackboxCapacity = 300;
        private const Allocator SignalLaneAllocator = Allocator.Persistent;
        private const SystemID NativeArrayOwnerSystem = SystemID.ModSandbox;
        private const long PerFrameManagedAllocationLimitBytes = 1L * 1024L * 1024L;
        private const string TimeoutCullMessage = "[MOD CULLED: TIMEOUT]";
        private const string GcCullMessage = "[MOD CULLED: GC]";
        private const string ExceptionCullMessage = "[ModEventProjectionBridge] projected event subscriber threw.";
        private const string TimeoutDisableReason = "Projected event callback exceeded 2.0ms watchdog.";
        private const string GcDisableReason = "Projected event callback exceeded 1MB managed allocation frame quota.";
        private const string ExceptionDisableReason = "Projected event callback exception.";
        private const string EnvelopeOnlyProjectionDisabledMessage = "Projected managed mod events are disabled in FutureCommandEnvelope-only mode.";
        private const uint GcCullEventHash = 0x4743414Cu; // GCAL
        private const uint ExceptionCullEventHash = 0x45584350u; // EXCP
        private const uint ProjectionJobOverrunWarningHash = 0x4D504A4Fu; // MPJO
        private const uint ProjectionBridgeContextHash = 0x4D504252u; // MPBR
        private static readonly long _watchdogTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.002d));
        private static readonly float _stopwatchTicksToMilliseconds = (float)(1000.0d / Stopwatch.Frequency);
        // COLD ALLOC: ModEventProjectionBridge[1] - registry-owned mod event projection service - owner: ModEventProjectionBridge, lazy so envelope-only UGC does not instantiate the bridge.
        private static ModEventProjectionBridge _globalBridge;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // One-shot latch for the dead WeatherChangedSignal lane advisory in ProjectPostSimulation. That method
        // runs on the dispatcher post-simulation phase every frame (Core/SystemDispatcher.cs:5330), so after the
        // first fire the advisory must cost one static bool read and must never build a string.
        // Reset per play session by ResetStaticState.
        private static bool s_deadWeatherChangedSignalLaneWarned;
#endif

        // COLD ALLOC: List<SubscriptionEntry>[16] - mod projected-event delegate registry - owner: ModEventProjectionBridge
        private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(16);
        private NativeQueue<ModEventDto> _projectedEvents;
        private NativeArray<ModCullTelemetryEntry> _cullTelemetry;
        private int _projectedEventsSentinelId;
        private JobHandle _projectionHandle;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private int _activeSubscriptionCount;
        private int _nextSubscriptionId = 1;
        private int _dispatchDepth;
        private int _queuedProjectedEventCount;
        private int _cullTelemetryCursor;
        private int _tickCount;
        private bool _projectionScheduled;
        private bool _needsCompaction;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;

        public bool IsInitialized { get; private set; }

        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Before the _globalBridge null bail below on purpose: the latch is instance-independent, and the
            // bridge is created lazily, so a return here would leave the advisory suppressed for the next session.
            s_deadWeatherChangedSignalLaneWarned = false;
#endif
            if (_globalBridge == null)
                return;

            ShutdownGlobal();
            _globalBridge._subscriptions.Clear();
            _globalBridge._nextSubscriptionId = 1;
            _globalBridge._dispatchDepth = 0;
            _globalBridge._activeSubscriptionCount = 0;
            _globalBridge._needsCompaction = false;
            _globalBridge._lateFrameRegistered = false;
        }

        internal static void InstallGlobal()
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return;

            GetOrCreateGlobalBridge().Install();
        }

        internal static void ShutdownGlobal()
        {
            if (_globalBridge == null)
                return;

            _globalBridge.Shutdown();
        }

        internal static HectonEventSubscription SubscribeProjected(Action<ModEventDto> handler, string subscriberId)
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                throw new IllegalContractException(EnvelopeOnlyProjectionDisabledMessage);

            if (handler == null)
                throw new IllegalContractException("Cannot subscribe a null projected mod event handler.");

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("ModEventProjectionBridge.SubscribeProjected requires an active mod execution scope.");

            if (!string.IsNullOrWhiteSpace(subscriberId) &&
                !string.Equals(subscriberId, ModExecutionScope.CurrentModId, StringComparison.Ordinal))
            {
                throw new IllegalContractException("ModEventProjectionBridge.SubscribeProjected subscriber id must match the active mod execution scope.");
            }

            ModEventProjectionBridge bridge = GlobalRegistry.ModdingBridge as ModEventProjectionBridge;
            if (bridge == null)
            {
                bridge = GetOrCreateGlobalBridge();
                bridge.Install();
            }

            return bridge.Subscribe(handler, subscriberId);
        }

        internal static void DisableProjectedSubscriber(string subscriberId)
        {
            if (_globalBridge == null)
                return;

            _globalBridge.DisableSubscriber(subscriberId);
        }

        private static ModEventProjectionBridge GetOrCreateGlobalBridge()
        {
            if (_globalBridge == null)
                _globalBridge = new ModEventProjectionBridge();

            return _globalBridge;
        }

        public void Install()
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return;

            if (IsInitialized)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _projectedEvents = new NativeQueue<ModEventDto>(SignalLaneAllocator); // COLD ALLOC: NativeQueue<ModEventDto>[50] - projected public signal metadata for managed mods - owner: ModEventProjectionBridge
            try
            {
                _projectedEventsSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                    _projectedEvents,
                    HighTierProjectionCap,
                    NativeMemoryOwner,
                    nameof(_projectedEvents),
                    NativeAllocationLifetime.Session);
                if (_projectedEventsSentinelId <= 0)
                    throw new InvalidOperationException("Native memory sentinel registration failed for projected mod events.");
            }
            catch
            {
                ReleaseNativeState();
                throw;
            }

            try
            {
                EnsureCullTelemetryStorage();
            }
            catch
            {
                ReleaseNativeState();
                throw;
            }

            _queuedProjectedEventCount = 0;
            _projectionScheduled = false;
            _tickCount = 0;
            _playerRuntimeContext = GlobalRegistry.Player;

            HectonEventBus.InstallNativeQueueBindings();
            GlobalRegistry.RegisterModdingBridgeRuntime(this);
            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            if (!_lateFrameRegistered)
            {
                RollbackInstalledBridge();
                return;
            }

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            if (!_hotSwapRegistered)
            {
                RollbackInstalledBridge();
                return;
            }

            SystemDispatcher.SetModdingBridgeProjectionRuntime(this);
            IsInitialized = true;
        }

        public void Shutdown()
        {
            if (!IsInitialized)
                return;

            if (_lateFrameRegistered)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
            if (_hotSwapRegistered)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            GlobalRegistry.UnregisterModdingBridgeRuntime(this);
            SystemDispatcher.ClearModdingBridgeProjectionRuntime(this);
            HectonEventBus.UninstallNativeQueueBindings();
            _lateFrameRegistered = false;
            _hotSwapRegistered = false;
            _playerRuntimeContext = null;

            if (_projectionScheduled)
                DispatcherJobSwap.TryComplete(ref _projectionHandle, forceComplete: true);

            ReleaseNativeState();
            IsInitialized = false;
        }

        private void RollbackInstalledBridge()
        {
            if (_lateFrameRegistered)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);

            GlobalRegistry.UnregisterModdingBridgeRuntime(this);
            HectonEventBus.UninstallNativeQueueBindings();
            _lateFrameRegistered = false;
            _hotSwapRegistered = false;
            _playerRuntimeContext = null;
            ReleaseNativeState();
        }

        private void ReleaseNativeState()
        {
            _projectionScheduled = false;
            _queuedProjectedEventCount = 0;
            DisposeProjectedEvents();

            ReleaseCullTelemetryStorage();
        }

        private void DisposeProjectedEvents()
        {
            Exception firstException = null;

            if (_projectedEventsSentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(_projectedEventsSentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    _projectedEventsSentinelId = 0;
                }
            }

            if (_projectedEvents.IsCreated)
            {
                try
                {
                    _projectedEvents.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    _projectedEvents = default;
                }
            }
            else
            {
                _projectedEvents = default;
            }

            if (firstException != null)
                throw firstException;
        }


        private void EnsureCullTelemetryStorage()
        {
            if (_cullTelemetry.IsCreated)
                return;

            _cullTelemetry = H8Memory.Allocate<ModCullTelemetryEntry>(BlackboxCapacity, NativeArrayOwnerSystem, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ModCullTelemetryEntry>[300] - culled mod hash blackbox ring, local bridge-owned memory to avoid DataVault hot writes - owner: ModEventProjectionBridge
        }

        private void ReleaseCullTelemetryStorage()
        {
            if (_cullTelemetry.IsCreated)
            {
                H8Memory.Release(ref _cullTelemetry, NativeArrayOwnerSystem);
            }

            _cullTelemetryCursor = 0;
        }

        public void ProjectPostSimulation()
        {
            if (!IsInitialized ||
                !_lateFrameRegistered ||
                _activeSubscriptionCount <= 0 ||
                _projectionScheduled ||
                _queuedProjectedEventCount > 0 ||
                !_projectedEvents.IsCreated)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Announced here - after the guard above, before the count folds below - on purpose. Above the guard
            // there is no subscriber to harm and the bridge may not even be installed; below the folds the
            // "damageCount <= 0 && weatherCount <= 0" bail would tie this advisory to combat traffic and could
            // suppress it for a whole session in a peaceful scene. Full evidence at the weather read further down.
            if (!s_deadWeatherChangedSignalLaneWarned)
            {
                s_deadWeatherChangedSignalLaneWarned = true;
                Hecton8.Core.H8Debug.LogWarning(
                    "[ModEventProjectionBridge] DEAD SIGNAL LANE: ProjectPostSimulation sizes ProjectWeatherChangedSignalsJob from SignalBus<WeatherChangedSignal>.SnapshotCount and reads GetFrameSnapshotArray, but WeatherChangedSignal has no producer anywhere in the scripts tree. The only code that ever constructs one is GlobalSignals.Publish(in WeatherStrengthSignal) (Core/Signals/GlobalSignals.LegacyFacade.cs:835-843), and that facade carries [Obsolete(\"Legacy publish facade is retired...\", true)] so calling it is a compile error. The frame snapshot is therefore permanently empty, weatherCount is always 0, ProjectWeatherChangedSignalsJob is never scheduled, and NO mod that subscribed through SubscribeProjected has ever received a ModEventKind.WeatherChanged / ModEventDto.WeatherChangedEventHash (WEAT) event. This is a first-party gap, not a modding extension point: the bridge direction is first-party-out (it reads first-party lanes and invokes mod Action<ModEventDto> handlers), Docs/Modding/Signal_Audit_Matrix.md:37 classifies the lane ALLOWED_READ_ONLY_PROJECTION with no weather authority mutation exposed to mods, and Docs/Modding/Runtime_Verification_Playbook.md:635 requires the source WeatherChangedSignal be forced through a first-party owner. For contrast the bridge's other lane is live - CombatDamageSignal is pushed by Fauna/FaunaBrain.cs:2203 and :4145 and Gameplay/Combat/BallisticsRuntime.cs:726 - so exactly half of the advertised projection surface is dead. Weather itself is NOT broken: the live broadcast is a different bus, WeatherEvents.TryRaiseSnapshotUpdated (Environment/WeatherEvents.cs:291) called from GlobalWeatherDirector.PublishWeatherEventIfChanged (Environment/GlobalWeatherDirector.cs:836), an IWeatherEventListener registry rather than a SignalBus lane. Do not wire this without an owner decision on the field mapping: that publish point already owns the change edge and the previous state (_lastWeatherEventStateMask), but WeatherRuntimeSnapshot (Core/GlobalRegistryContracts.cs:629) carries only StateMask, WeatherIntensity, current/wind vectors and Gerstner waves - it has no WeatherHash, no PreviousWeatherHash, no Frame and no FlowFieldScale - so the WeatherState-mask-to-uint-hash convention and the FlowFieldScale source both need naming by the weather owner.");
            }
#endif

            int projectionCap = ResolveProjectionCap();
            int damageCount = math.min(SignalBus<CombatDamageSignal>.SnapshotCount, projectionCap);
            int remaining = projectionCap - damageCount;
            // DEAD LANE - weatherCount IS ALWAYS 0. SignalBus<WeatherChangedSignal> has no producer anywhere in
            // the scripts tree; the sole constructor of the payload is the compile-dead
            // GlobalSignals.Publish(in WeatherStrengthSignal) (Core/Signals/GlobalSignals.LegacyFacade.cs:835-843,
            // [Obsolete(..., error: true)]). Consequence: ProjectWeatherChangedSignalsJob below is guarded by
            // "if (weatherCount > 0)" and has never been scheduled, so this costs one SnapshotCount read per
            // post-simulation phase rather than a zero-length job schedule - the damage is to the mod contract,
            // not to the frame budget. Both reads here are NON-DESTRUCTIVE (SnapshotCount at
            // Core/Signals/SignalBusRuntime.cs:447 and GetFrameSnapshotArray at :792 take no cursor, unlike
            // TryConsumeFrame at :806), so a first-party producer can be added alongside this reader without
            // starving any other consumer of the lane. See the one-shot advisory above for the full evidence.
            int weatherCount = remaining > 0
                ? math.min(SignalBus<WeatherChangedSignal>.SnapshotCount, remaining)
                : 0;

            if (damageCount <= 0 && weatherCount <= 0)
                return;

            float3 playerRuntimePosition = ResolvePlayerRuntimePosition();
            if (!TryResolveRuntimeAup(playerRuntimePosition, out double3 playerAbsolutePosition))
                return;

            float projectionQualityWeight01 = ResolveProjectionQualityWeight01();
            JobHandle handle = default;
            if (damageCount > 0)
            {
                handle = new ProjectCombatDamageSignalsJob
                {
                    Signals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray(),
                    Output = _projectedEvents.AsParallelWriter(),
                    PlayerAbsolutePosition = playerAbsolutePosition,
                    Limit = damageCount,
                    QualityWeight01 = projectionQualityWeight01
                }.Schedule();
            }

            if (weatherCount > 0)
            {
                handle = new ProjectWeatherChangedSignalsJob
                {
                    Signals = SignalBus<WeatherChangedSignal>.GetFrameSnapshotArray(),
                    Output = _projectedEvents.AsParallelWriter(),
                    Limit = weatherCount,
                    QualityWeight01 = projectionQualityWeight01
                }.Schedule(handle);
            }

            _projectionHandle = handle;
            _projectionScheduled = true;
            _queuedProjectedEventCount = damageCount + weatherCount;
        }

        public void LateFrameTick()
        {
            DispatchLateFrame();
        }

        public void DispatchLateFrame()
        {
            if (!IsInitialized)
                return;

            _tickCount++;
            if (_projectionScheduled)
            {
                if (!DispatcherJobSwap.TryComplete(ref _projectionHandle, forceComplete: false))
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        ProjectionJobOverrunWarningHash,
                        ProjectionBridgeContextHash,
                        _queuedProjectedEventCount);
                    return;
                }

                _projectionScheduled = false;
            }

            int dispatchBudget = ResolveProjectionCap();
            int dispatched = 0;
            while (_queuedProjectedEventCount > 0 && dispatched < dispatchBudget)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_projectedEvents.TryDequeue(out ModEventDto dto))
                {
                    _queuedProjectedEventCount = 0;
                    break;
                }

                _queuedProjectedEventCount--;
                dispatched++;
                DispatchToSubscribers(in dto);
            }

            if (_queuedProjectedEventCount <= 0)
                _queuedProjectedEventCount = 0;
        }

        private HectonEventSubscription Subscribe(Action<ModEventDto> handler, string subscriberId)
        {
            string resolvedSubscriberId = string.IsNullOrWhiteSpace(subscriberId)
                ? ModExecutionScope.CurrentModId
                : subscriberId;
            if (string.IsNullOrWhiteSpace(resolvedSubscriberId))
                throw new IllegalContractException("Projected mod event subscriptions require a concrete mod subscriber id.");

            SubscriptionEntry entry = new SubscriptionEntry
            {
                Id = _nextSubscriptionId++,
                Handler = handler,
                SubscriberId = resolvedSubscriberId,
                SubscriberHash = ModCommandDispatcher.ComputeModHash(resolvedSubscriberId),
                IsActive = true,
                AllocationFrame = -1,
                FrameAllocationBytes = 0L
            };

            _subscriptions.Add(entry);
            _activeSubscriptionCount++;
            return new HectonEventSubscription(this, entry.Id, entry.SubscriberId, ModExecutionScope.HasActiveMod);
        }

        public void Unsubscribe(int subscriptionId)
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                SubscriptionEntry entry = _subscriptions[i];
                if (entry.Id != subscriptionId)
                    continue;

                DisableEntry(i, ref entry);
                if (_dispatchDepth == 0)
                    CompactInactiveSubscriptions();
                return;
            }
        }

        private void DisableSubscriber(string subscriberId)
        {
            if (string.IsNullOrWhiteSpace(subscriberId))
                return;

            for (int i = 0; i < _subscriptions.Count; i++)
            {
                SubscriptionEntry entry = _subscriptions[i];
                if (!entry.IsActive || entry.SubscriberId != subscriberId)
                    continue;

                DisableEntry(i, ref entry);
            }

            if (_dispatchDepth == 0 && _needsCompaction)
                CompactInactiveSubscriptions();
        }

        private void DispatchToSubscribers(in ModEventDto dto)
        {
            if (_activeSubscriptionCount <= 0)
                return;

            _dispatchDepth++;
            try
            {
                for (int i = 0; i < _subscriptions.Count; i++)
                {
                    SubscriptionEntry entry = _subscriptions[i];
                    if (!entry.IsActive || entry.Handler == null)
                        continue;

                    long callbackStartTimestamp = Stopwatch.GetTimestamp();
                    long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                    try
                    {
                        if (ModCommandDispatcher.IsRegisteredMod(entry.SubscriberHash))
                        {
                            using (ModExecutionScope.Enter(entry.SubscriberId, entry.SubscriberHash))
                            {
                                entry.Handler(dto);
                            }
                        }
                        else
                        {
                            entry.Handler(dto);
                        }
                    }
                    catch (Exception)
                    {
                        long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                        ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocationDelta);
                        CullEntry(i, ref entry, ExceptionCullEventHash, ModCullReason.Exception, 0f, ExceptionDisableReason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Hecton8.Core.H8Debug.LogError(ExceptionCullMessage);
#endif
                        continue;
                    }

                    long elapsedTicks = Stopwatch.GetTimestamp() - callbackStartTimestamp;
                    long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
                    ModCommandDispatcher.ReportModManagedAllocation(entry.SubscriberHash, allocatedBytes);
                    if (AccountFrameAllocation(ref entry, allocatedBytes))
                    {
                        CullEntry(
                            i,
                            ref entry,
                            GcCullEventHash,
                            ModCullReason.GcQuota,
                            entry.FrameAllocationBytes,
                            GcDisableReason);
                        continue;
                    }

                    if (elapsedTicks > _watchdogTicks)
                    {
                        float elapsedMilliseconds = elapsedTicks * _stopwatchTicksToMilliseconds;
                        CullEntry(
                            i,
                            ref entry,
                            dto.EventHash,
                            ModCullReason.Timeout,
                            elapsedMilliseconds,
                            TimeoutDisableReason);
                        continue;
                    }

                    _subscriptions[i] = entry;
                }
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0 && _needsCompaction)
                    CompactInactiveSubscriptions();
            }
        }

        private static int ResolveProjectionCap()
        {
            float qualityWeight01 = ResolveProjectionQualityWeight01();
            float curve = Smooth01(qualityWeight01);
            int cap = (int)math.round(math.lerp(LowTierProjectionCap, HighTierProjectionCap, curve));
            return math.clamp(cap, LowTierProjectionCap, HighTierProjectionCap);
        }

        private static float ResolveProjectionQualityWeight01()
        {
            float qualityWeight01 = SignalBusRegistry.GlobalQualityWeight01;
            return math.isfinite(qualityWeight01) ? math.saturate(qualityWeight01) : 0f;
        }

        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        private float3 ResolvePlayerRuntimePosition()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                return snapshot.RuntimePosition;
            }

            return float3.zero;
        }

        private static bool TryResolveRuntimeAup(float3 runtimePosition, out double3 absoluteAup)
        {
            absoluteAup = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            if (!positionAup.IsFinite())
                return false;

            absoluteAup = positionAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absoluteAup));
        }

        private bool AccountFrameAllocation(ref SubscriptionEntry entry, long allocatedBytes)
        {
            if (allocatedBytes <= 0L)
                return false;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (entry.AllocationFrame != frame)
            {
                entry.AllocationFrame = frame;
                entry.FrameAllocationBytes = 0L;
            }

            entry.FrameAllocationBytes = long.MaxValue - entry.FrameAllocationBytes < allocatedBytes
                ? long.MaxValue
                : entry.FrameAllocationBytes + allocatedBytes;
            return entry.FrameAllocationBytes > PerFrameManagedAllocationLimitBytes;
        }

        private void CullEntry(
            int index,
            ref SubscriptionEntry entry,
            uint eventHash,
            ModCullReason reason,
            float scalar,
            string disableReason)
        {
            uint modHash = entry.SubscriberHash != 0u
                ? entry.SubscriberHash
                : ModCommandDispatcher.ComputeModHash(entry.SubscriberId);
            WriteCullTelemetry(modHash, eventHash, reason, scalar);
            if (reason == ModCullReason.Timeout)
            {
                GlobalTelemetryBus.PublishModStallWarning(modHash, eventHash, math.max(0f, scalar));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(TimeoutCullMessage);
#endif
            }
            else if (reason == ModCullReason.GcQuota)
            {
                GlobalTelemetryBus.PublishModCriticalMemoryEviction(
                    modHash,
                    (long)math.max(0f, scalar),
                    PerFrameManagedAllocationLimitBytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(GcCullMessage);
#endif
            }

            string subscriberId = entry.SubscriberId;
            DisableEntry(index, ref entry);
            ModLoader.DisableMod(subscriberId, disableReason);
        }

        private void DisableEntry(int index, ref SubscriptionEntry entry)
        {
            if (entry.IsActive && _activeSubscriptionCount > 0)
                _activeSubscriptionCount--;

            entry.IsActive = false;
            entry.Handler = null;
            _subscriptions[index] = entry;
            _needsCompaction = true;
        }

        private void CompactInactiveSubscriptions()
        {
            for (int i = _subscriptions.Count - 1; i >= 0; i--)
            {
                if (!_subscriptions[i].IsActive)
                    _subscriptions.RemoveAt(i);
            }

            _needsCompaction = false;
        }

        private void WriteCullTelemetry(uint modHash, uint eventHash, ModCullReason reason, float scalar)
        {
            if (!_cullTelemetry.IsCreated)
                return;

            int slot = (int)((uint)_cullTelemetryCursor++ % BlackboxCapacity);
            _cullTelemetry[slot] = new ModCullTelemetryEntry
            {
                ModHash = modHash,
                EventHash = eventHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Scalar = math.isfinite(scalar) ? scalar : 0f,
                Reason = (uint)reason,
                ActiveSubscriptions = (uint)math.max(0, _activeSubscriptionCount)
            };
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            HectonAPI.OnGlobalRegistryServiceReplaced(serviceSlot, currentService);
            ModCommandDispatcher.OnGlobalRegistryServiceReplaced(serviceSlot, currentService);
            ModSettingsRegistry.OnGlobalRegistryServiceReplaced(serviceSlot, currentService);
            ModItemRegistry.OnGlobalRegistryServiceReplaced(serviceSlot, currentService);
            ModBuildableRegistry.OnGlobalRegistryServiceReplaced(serviceSlot, currentService);
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
        }

        private struct SubscriptionEntry
        {
            public int Id;
            public Action<ModEventDto> Handler;
            public string SubscriberId;
            public uint SubscriberHash;
            public int AllocationFrame;
            public long FrameAllocationBytes;
            public bool IsActive;
        }

        private enum ModCullReason : uint
        {
            Timeout = 1,
            GcQuota = 2,
            Exception = 3
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 64)]
        private struct ModCullTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public uint ModHash;
            [System.Runtime.InteropServices.FieldOffset(4)]
            public uint EventHash;
            [System.Runtime.InteropServices.FieldOffset(8)]
            public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(12)]
            public float Scalar;
            [System.Runtime.InteropServices.FieldOffset(16)]
            public uint Reason;
            [System.Runtime.InteropServices.FieldOffset(20)]
            public uint ActiveSubscriptions;
            [System.Runtime.InteropServices.FieldOffset(24)]
            private byte _pad0;
            [System.Runtime.InteropServices.FieldOffset(25)]
            private byte _pad1;
            [System.Runtime.InteropServices.FieldOffset(26)]
            private byte _pad2;
            [System.Runtime.InteropServices.FieldOffset(27)]
            private byte _pad3;
            [System.Runtime.InteropServices.FieldOffset(28)]
            private byte _pad4;
            [System.Runtime.InteropServices.FieldOffset(29)]
            private byte _pad5;
            [System.Runtime.InteropServices.FieldOffset(30)]
            private byte _pad6;
            [System.Runtime.InteropServices.FieldOffset(31)]
            private byte _pad7;
            [System.Runtime.InteropServices.FieldOffset(32)]
            private byte _pad8;
            [System.Runtime.InteropServices.FieldOffset(33)]
            private byte _pad9;
            [System.Runtime.InteropServices.FieldOffset(34)]
            private byte _pad10;
            [System.Runtime.InteropServices.FieldOffset(35)]
            private byte _pad11;
            [System.Runtime.InteropServices.FieldOffset(36)]
            private byte _pad12;
            [System.Runtime.InteropServices.FieldOffset(37)]
            private byte _pad13;
            [System.Runtime.InteropServices.FieldOffset(38)]
            private byte _pad14;
            [System.Runtime.InteropServices.FieldOffset(39)]
            private byte _pad15;
            [System.Runtime.InteropServices.FieldOffset(40)]
            private byte _pad16;
            [System.Runtime.InteropServices.FieldOffset(41)]
            private byte _pad17;
            [System.Runtime.InteropServices.FieldOffset(42)]
            private byte _pad18;
            [System.Runtime.InteropServices.FieldOffset(43)]
            private byte _pad19;
            [System.Runtime.InteropServices.FieldOffset(44)]
            private byte _pad20;
            [System.Runtime.InteropServices.FieldOffset(45)]
            private byte _pad21;
            [System.Runtime.InteropServices.FieldOffset(46)]
            private byte _pad22;
            [System.Runtime.InteropServices.FieldOffset(47)]
            private byte _pad23;
            [System.Runtime.InteropServices.FieldOffset(48)]
            private byte _pad24;
            [System.Runtime.InteropServices.FieldOffset(49)]
            private byte _pad25;
            [System.Runtime.InteropServices.FieldOffset(50)]
            private byte _pad26;
            [System.Runtime.InteropServices.FieldOffset(51)]
            private byte _pad27;
            [System.Runtime.InteropServices.FieldOffset(52)]
            private byte _pad28;
            [System.Runtime.InteropServices.FieldOffset(53)]
            private byte _pad29;
            [System.Runtime.InteropServices.FieldOffset(54)]
            private byte _pad30;
            [System.Runtime.InteropServices.FieldOffset(55)]
            private byte _pad31;
            [System.Runtime.InteropServices.FieldOffset(56)]
            private byte _pad32;
            [System.Runtime.InteropServices.FieldOffset(57)]
            private byte _pad33;
            [System.Runtime.InteropServices.FieldOffset(58)]
            private byte _pad34;
            [System.Runtime.InteropServices.FieldOffset(59)]
            private byte _pad35;
            [System.Runtime.InteropServices.FieldOffset(60)]
            private byte _pad36;
            [System.Runtime.InteropServices.FieldOffset(61)]
            private byte _pad37;
            [System.Runtime.InteropServices.FieldOffset(62)]
            private byte _pad38;
            [System.Runtime.InteropServices.FieldOffset(63)]
            private byte _pad39;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProjectCombatDamageSignalsJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<CombatDamageSignal>.ReadOnly Signals;
            [NoAlias] public NativeQueue<ModEventDto>.ParallelWriter Output;
            public double3 PlayerAbsolutePosition;
            public int Limit;
            public float QualityWeight01;

            public void Execute()
            {
                int count = math.min(Limit, Signals.Length);
                float quality = math.saturate(math.select(1f, QualityWeight01, math.isfinite(QualityWeight01)));
                ushort sampleFlags = (ushort)(ModEventDto.LowTierSampleFlag *
                    (Smooth01((LowProjectionQualityFlagThreshold01 - quality) * math.rcp(math.max(0.0001f, LowProjectionQualityFlagThreshold01))) > 0.999f ? 1 : 0));
                for (int i = 0; i < count; i++)
                {
                    CombatDamageSignal signal = Signals[i];
                    float3 relativePosition = AupPrecisionMath.LocalDeltaFloat3(signal.ImpactAup, PlayerAbsolutePosition, float3.zero);
                    if (!math.all(math.isfinite(relativePosition)))
                        relativePosition = float3.zero;

                    float3 direction = signal.Direction;
                    if (!math.all(math.isfinite(direction)))
                        direction = float3.zero;

                    Output.Enqueue(new ModEventDto
                    {
                        EventHash = ModEventDto.CombatDamageEventHash,
                        SubjectHash = signal.TargetHash,
                        ContextHash = signal.DamageType,
                        SourceHash = signal.SourceHash,
                        Frame = signal.Frame,
                        RelativePosition = relativePosition,
                        Direction = direction,
                        Scalar0 = math.isfinite(signal.Magnitude) ? signal.Magnitude : 0f,
                        Scalar1 = signal.IntegrityDelta,
                        Kind = (ushort)ModEventKind.CombatDamage,
                        Flags = (ushort)(signal.Flags | sampleFlags),
                        QualityTier = (byte)math.clamp((int)math.round(quality * 255f), 0, 255),
                        Sequence = (ushort)math.min(i, ushort.MaxValue)
                    });
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct ProjectWeatherChangedSignalsJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<WeatherChangedSignal>.ReadOnly Signals;
            [NoAlias] public NativeQueue<ModEventDto>.ParallelWriter Output;
            public int Limit;
            public float QualityWeight01;

            public void Execute()
            {
                int count = math.min(Limit, Signals.Length);
                float quality = math.saturate(math.select(1f, QualityWeight01, math.isfinite(QualityWeight01)));
                ushort sampleFlags = (ushort)(ModEventDto.LowTierSampleFlag *
                    (Smooth01((LowProjectionQualityFlagThreshold01 - quality) * math.rcp(math.max(0.0001f, LowProjectionQualityFlagThreshold01))) > 0.999f ? 1 : 0));
                for (int i = 0; i < count; i++)
                {
                    WeatherChangedSignal signal = Signals[i];
                    Output.Enqueue(new ModEventDto
                    {
                        EventHash = ModEventDto.WeatherChangedEventHash,
                        SubjectHash = signal.WeatherHash,
                        ContextHash = signal.PreviousWeatherHash,
                        SourceHash = 0u,
                        Frame = signal.Frame,
                        RelativePosition = float3.zero,
                        Direction = float3.zero,
                        Scalar0 = math.isfinite(signal.Strength01) ? math.saturate(signal.Strength01) : 0f,
                        Scalar1 = math.isfinite(signal.FlowFieldScale) ? math.max(0f, signal.FlowFieldScale) : 0f,
                        Kind = (ushort)ModEventKind.WeatherChanged,
                        Flags = (ushort)(signal.Flags | sampleFlags),
                        QualityTier = signal.QualityWeightByte,
                        Sequence = (ushort)math.min(i, ushort.MaxValue)
                    });
                }
            }
        }
    }
}
