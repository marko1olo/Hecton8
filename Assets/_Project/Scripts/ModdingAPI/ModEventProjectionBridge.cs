using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Modding
{
    /// <summary>
    /// Projects selected first-party native signal snapshots into managed mod callbacks without exposing native ownership.
    /// </summary>
    internal sealed class ModEventProjectionBridge : IModdingBridge, ILateFrameTickable, IHectonEventChannel
    {
        private const string NativeMemoryOwner = nameof(ModEventProjectionBridge);
        private const int HighTierProjectionCap = 50;
        private const int LowTierProjectionCap = 10;
        private const int BlackboxCapacity = 300;
        private const long PerFrameManagedAllocationLimitBytes = 1L * 1024L * 1024L;
        private const string TimeoutCullMessage = "[MOD CULLED: TIMEOUT]";
        private const string GcCullMessage = "[MOD CULLED: GC]";
        private const string ExceptionCullMessage = "[ModEventProjectionBridge] projected event subscriber threw.";
        private const string TimeoutDisableReason = "Projected event callback exceeded 2.0ms watchdog.";
        private const string GcDisableReason = "Projected event callback exceeded 1MB managed allocation frame quota.";
        private const string ExceptionDisableReason = "Projected event callback exception.";
        private const uint GcCullEventHash = 0x4743414Cu; // GCAL
        private const uint ExceptionCullEventHash = 0x45584350u; // EXCP
        private const uint ProjectionJobOverrunWarningHash = 0x4D504A4Fu; // MPJO
        private const uint ProjectionBridgeContextHash = 0x4D504252u; // MPBR
        private static readonly long _watchdogTicks = Math.Max(1L, (long)(Stopwatch.Frequency * 0.002d));
        private static readonly float _stopwatchTicksToMilliseconds = (float)(1000.0d / Stopwatch.Frequency);
        // COLD ALLOC: ModEventProjectionBridge[1] - registry-owned mod event projection service - owner: ModEventProjectionBridge
        private static readonly ModEventProjectionBridge _globalBridge = new ModEventProjectionBridge();

        // COLD ALLOC: List<SubscriptionEntry>[16] - mod projected-event delegate registry - owner: ModEventProjectionBridge
        private readonly List<SubscriptionEntry> _subscriptions = new List<SubscriptionEntry>(16);
        private NativeQueue<ModEventDto> _projectedEvents;
        private NativeArray<ModCullTelemetryEntry> _cullTelemetry;
        private JobHandle _projectionHandle;
        private int _activeSubscriptionCount;
        private int _nextSubscriptionId = 1;
        private int _dispatchDepth;
        private int _queuedProjectedEventCount;
        private int _cullTelemetryCursor;
        private int _tickCount;
        private bool _projectionScheduled;
        private bool _needsCompaction;
        private bool _lateFrameRegistered;

        public bool IsInitialized { get; private set; }

        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
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
            _globalBridge.Install();
        }

        internal static void ShutdownGlobal()
        {
            _globalBridge.Shutdown();
        }

        internal static HectonEventSubscription SubscribeProjected(Action<ModEventDto> handler, string subscriberId)
        {
            if (handler == null)
                throw new IllegalContractException("Cannot subscribe a null projected mod event handler.");

            ModEventProjectionBridge bridge = GlobalRegistry.ModdingBridge as ModEventProjectionBridge;
            if (bridge == null)
            {
                _globalBridge.Install();
                bridge = _globalBridge;
            }

            return bridge.Subscribe(handler, subscriberId);
        }

        internal static void DisableProjectedSubscriber(string subscriberId)
        {
            _globalBridge.DisableSubscriber(subscriberId);
        }

        public void Install()
        {
            if (IsInitialized)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _projectedEvents = new NativeQueue<ModEventDto>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModEventDto>[50] - projected public signal metadata for managed mods - owner: ModEventProjectionBridge
            NativeMemorySentinel.RegisterNativeQueue(_projectedEvents, HighTierProjectionCap, NativeMemoryOwner, nameof(_projectedEvents), NativeAllocationLifetime.Session);
            _cullTelemetry = new NativeArray<ModCullTelemetryEntry>(BlackboxCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<ModCullTelemetryEntry>[300] - culled mod hash blackbox ring - owner: ModEventProjectionBridge
            NativeMemorySentinel.RegisterNativeArray(_cullTelemetry, NativeMemoryOwner, nameof(_cullTelemetry), NativeAllocationLifetime.Session);
            _queuedProjectedEventCount = 0;
            _projectionScheduled = false;
            _tickCount = 0;

            HectonEventBus.InstallNativeQueueBindings();
            GlobalRegistry.RegisterModdingBridgeRuntime(this);
            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            if (!_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterModdingBridgeRuntime(this);
                HectonEventBus.UninstallNativeQueueBindings();
                ReleaseNativeState();
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
            GlobalRegistry.UnregisterModdingBridgeRuntime(this);
            SystemDispatcher.ClearModdingBridgeProjectionRuntime(this);
            HectonEventBus.UninstallNativeQueueBindings();
            _lateFrameRegistered = false;

            if (_projectionScheduled)
                DispatcherJobSwap.TryComplete(ref _projectionHandle, forceComplete: true);

            ReleaseNativeState();
            IsInitialized = false;
        }

        private void ReleaseNativeState()
        {
            _projectionScheduled = false;
            _queuedProjectedEventCount = 0;
            if (_projectedEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, nameof(_projectedEvents));
                _projectedEvents.Dispose();
                _projectedEvents = default;
            }

            if (_cullTelemetry.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_cullTelemetry);
                _cullTelemetry.Dispose();
                _cullTelemetry = default;
            }
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

            int projectionCap = ResolveProjectionCap();
            int damageCount = math.min(SignalBus<CombatDamageSignal>.SnapshotCount, projectionCap);
            int remaining = projectionCap - damageCount;
            int weatherCount = remaining > 0
                ? math.min(SignalBus<WeatherChangedSignal>.SnapshotCount, remaining)
                : 0;

            if (damageCount <= 0 && weatherCount <= 0)
                return;

            float3 playerRuntimePosition = ResolvePlayerRuntimePosition();
            bool lowTier = GlobalRegistry.ScalabilityTierProfileByte == 0;
            JobHandle handle = default;
            if (damageCount > 0)
            {
                handle = new ProjectCombatDamageSignalsJob
                {
                    Signals = SignalBus<CombatDamageSignal>.GetFrameSnapshotArray(),
                    Output = _projectedEvents.AsParallelWriter(),
                    PlayerRuntimePosition = playerRuntimePosition,
                    Limit = damageCount,
                    LowTier = lowTier ? (byte)1 : (byte)0
                }.Schedule();
            }

            if (weatherCount > 0)
            {
                handle = new ProjectWeatherChangedSignalsJob
                {
                    Signals = SignalBus<WeatherChangedSignal>.GetFrameSnapshotArray(),
                    Output = _projectedEvents.AsParallelWriter(),
                    Limit = weatherCount,
                    LowTier = lowTier ? (byte)1 : (byte)0
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
                    DispatcherJobSwap.TryComplete(ref _projectionHandle, forceComplete: true);
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        ProjectionJobOverrunWarningHash,
                        ProjectionBridgeContextHash,
                        _queuedProjectedEventCount);
                }
            }

            _projectionScheduled = false;
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
                resolvedSubscriberId = "anonymous";

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
            return new HectonEventSubscription(this, entry.Id, entry.SubscriberId);
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
                        Debug.LogError(ExceptionCullMessage);
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
            return GlobalRegistry.ScalabilityTierProfileByte == 0 ? LowTierProjectionCap : HighTierProjectionCap;
        }

        private static float3 ResolvePlayerRuntimePosition()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                return snapshot.RuntimePosition;
            }

            return float3.zero;
        }

        private bool AccountFrameAllocation(ref SubscriptionEntry entry, long allocatedBytes)
        {
            if (allocatedBytes <= 0L)
                return false;

            int frame = Time.frameCount;
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
                Debug.LogWarning(TimeoutCullMessage);
#endif
            }
            else if (reason == ModCullReason.GcQuota)
            {
                GlobalTelemetryBus.PublishModCriticalMemoryEviction(
                    modHash,
                    (long)math.max(0f, scalar),
                    PerFrameManagedAllocationLimitBytes);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(GcCullMessage);
#endif
            }

            string subscriberId = entry.SubscriberId;
            DisableEntry(index, ref entry);
            ModLoader.DisableManagedMod(subscriberId, disableReason);
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

            int slot = _cullTelemetryCursor++ % BlackboxCapacity;
            _cullTelemetry[slot] = new ModCullTelemetryEntry
            {
                ModHash = modHash,
                EventHash = eventHash,
                Frame = unchecked((uint)Time.frameCount),
                Scalar = math.isfinite(scalar) ? scalar : 0f,
                Reason = (uint)reason,
                ActiveSubscriptions = (uint)math.max(0, _activeSubscriptionCount)
            };
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

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 32)]
        private struct ModCullTelemetryEntry
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public uint ModHash;
            [System.Runtime.InteropServices.FieldOffset(4)] public uint EventHash;
            [System.Runtime.InteropServices.FieldOffset(8)] public uint Frame;
            [System.Runtime.InteropServices.FieldOffset(12)] public float Scalar;
            [System.Runtime.InteropServices.FieldOffset(16)] public uint Reason;
            [System.Runtime.InteropServices.FieldOffset(20)] public uint ActiveSubscriptions;
        }

        [BurstCompile]
        private struct ProjectCombatDamageSignalsJob : IJob
        {
            [ReadOnly] public NativeArray<CombatDamageSignal>.ReadOnly Signals;
            public NativeQueue<ModEventDto>.ParallelWriter Output;
            public float3 PlayerRuntimePosition;
            public int Limit;
            public byte LowTier;

            public void Execute()
            {
                int count = math.min(Limit, Signals.Length);
                ushort sampleFlags = LowTier != 0 ? ModEventDto.LowTierSampleFlag : (ushort)0;
                for (int i = 0; i < count; i++)
                {
                    CombatDamageSignal signal = Signals[i];
                    float3 relativePosition = signal.WorldPoint - PlayerRuntimePosition;
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
                        QualityTier = 0,
                        Sequence = (ushort)math.min(i, ushort.MaxValue)
                    });
                }
            }
        }

        [BurstCompile]
        private struct ProjectWeatherChangedSignalsJob : IJob
        {
            [ReadOnly] public NativeArray<WeatherChangedSignal>.ReadOnly Signals;
            public NativeQueue<ModEventDto>.ParallelWriter Output;
            public int Limit;
            public byte LowTier;

            public void Execute()
            {
                int count = math.min(Limit, Signals.Length);
                ushort sampleFlags = LowTier != 0 ? ModEventDto.LowTierSampleFlag : (ushort)0;
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
                        QualityTier = signal.QualityTier,
                        Sequence = (ushort)math.min(i, ushort.MaxValue)
                    });
                }
            }
        }
    }
}
