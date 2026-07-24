using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    public enum ScanEventType : byte
    {
        ScanTriggered = 0,
        NodeFound = 1,
        EntryDiscovered = 2,
        FaunaFeedingObserved = 3,
        FaunaMatingObserved = 4
    }

    public enum ScanEntryKind : byte
    {
        Unknown = 0,
        ResourceNode = 1,
        Item = 2,
        Module = 3,
        Scannable = 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ScanEventPayload
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public uint EntryHash;
        [FieldOffset(20)] public uint TitleHash;
        [FieldOffset(24)] public uint CategoryHash;
        [FieldOffset(28)] public uint SummaryHash;
        [FieldOffset(32)] public ushort EventType;
        [FieldOffset(34)] public byte EntryKind;
        [FieldOffset(35)] public byte Reserved;
        [FieldOffset(36)] private uint _pad0;
        [FieldOffset(40)] private ulong _pad1;
        [FieldOffset(48)] private ulong _pad2;
        [FieldOffset(56)] private ulong _pad3;
    }

    public readonly struct ScanEntryMetadata
    {
        public ScanEntryMetadata(
            string entryId,
            string title,
            string category,
            string summary,
            ScanEntryKind kind,
            uint entryHash,
            uint titleHash,
            uint categoryHash,
            uint summaryHash)
        {
            EntryId = entryId;
            Title = title;
            Category = category;
            Summary = summary;
            Kind = kind;
            EntryHash = entryHash;
            TitleHash = titleHash;
            CategoryHash = categoryHash;
            SummaryHash = summaryHash;
        }

        public string EntryId { get; }
        public string Title { get; }
        public string Category { get; }
        public string Summary { get; }
        public ScanEntryKind Kind { get; }
        public uint EntryHash { get; }
        public uint TitleHash { get; }
        public uint CategoryHash { get; }
        public uint SummaryHash { get; }
    }

    public interface IScanEventListener
    {
        void OnScanEvent(in ScanEventPayload payload);
    }

    public static class ScanEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private const int DeferredListenerMutationCapacity = 16;
        private const int EntryMetadataCapacity = 128;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const double WreckSignalDebounceSeconds = 5.0d;
        private const byte ListenerMutationRegister = 1;
        private const byte ListenerMutationUnregister = 2;
        private const uint ScanEventOverflowWarningHash = 0x534E514Fu; // SNQO
        private const uint ScanEventOverflowContextHash = 0x534E5143u; // SNQC
        private const uint ScanListenerOverflowWarningHash = 0x534E564Cu; // SNVL
        private const uint ScanListenerMutationContextHash = 0x534E4D54u; // SNMT
        private const uint ScanListenerExceptionWarningHash = 0x534E5645u; // SNVE
        private const uint ScanListenerExceptionContextHash = 0x534E5658u; // SNVX
        public const byte WreckSignalReservedMarker = 1;

        private struct ListenerSlot
        {
            public IScanEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct ScanListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public ScanListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed scan listener slots drained on dispatcher LateUpdate - owner: ScanEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IScanEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IScanEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(IScanEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public IScanEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static ScanListenerRegistry _listeners = new ScanListenerRegistry(ListenerCapacity);
        // COLD ALLOC: Dictionary<uint,ScanEntryMetadata>[128] - bounded hashed scan entry metadata cache for queue listeners that still own authored strings - owner: ScanEvents
        private static readonly Dictionary<uint, ScanEntryMetadata> _entryMetadataByHash = new Dictionary<uint, ScanEntryMetadata>(EntryMetadataCapacity);
        // COLD ALLOC: uint[128] - FIFO eviction ring for ScanEvents entry metadata cache - owner: ScanEvents
        private static readonly uint[] _entryMetadataEvictionRing = new uint[EntryMetadataCapacity];
        // COLD ALLOC: ListenerSlot[16] - deferred listener mutations during scan dispatch - owner: ScanEvents
        private static readonly ListenerSlot[] _deferredListenerMutations = new ListenerSlot[DeferredListenerMutationCapacity];
        // COLD ALLOC: byte[16] - deferred listener mutation op codes during scan dispatch - owner: ScanEvents
        private static readonly byte[] _deferredListenerMutationOps = new byte[DeferredListenerMutationCapacity];
        private static NativeQueue<ScanEventPayload> _pendingEvents;
        private static NativeQueue<ScanEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredListenerMutationCount;
        private static int _entryMetadataEvictionWriteIndex;
        private static int _entryMetadataEvictionCount;
        private static int _droppedEventCount;
        private static int _droppedDeferredListenerMutationCount;
        private static int _listenerExceptionCount;
        private static int _lastEventOverflowTelemetryFrame = -1;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static double _nextWreckSignalTime;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedDeferredListenerMutationCount => _droppedDeferredListenerMutationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        private static void IncrementCounterSaturated(ref int counter)
        {
            if (counter < int.MaxValue)
                counter++;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _listeners.Clear();
            _entryMetadataByHash.Clear();
            ClearEntryMetadataEvictionRing();
            ClearDeferredListenerMutations();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _droppedEventCount = 0;
            _droppedDeferredListenerMutationCount = 0;
            _listenerExceptionCount = 0;
            _lastEventOverflowTelemetryFrame = -1;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _nextWreckSignalTime = 0.0d;
            _isDispatching = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                ResetStaticState();
        }
#endif

        public static void Register(IScanEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredListenerMutation(listener, ListenerMutationRegister);
                return;
            }

            RegisterListenerImmediate(listener);
        }

        public static void EnsureInitializedCold()
        {
            EnsureInitialized();
        }

        public static void Unregister(IScanEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredListenerMutation(listener, ListenerMutationUnregister);
                return;
            }

            UnregisterListenerImmediate(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out ScanEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IScanEventListener listener = _listeners.GetAt(i);
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static uint ComputeEntryHash(string entryId)
        {
            return string.IsNullOrWhiteSpace(entryId)
                ? 0u
                : unchecked((uint)LocHash.Compute(entryId));
        }

        public static bool TryResolveEntryMetadata(uint entryHash, out ScanEntryMetadata metadata)
        {
            return _entryMetadataByHash.TryGetValue(entryHash, out metadata);
        }

        private static void RegisterListenerImmediate(IScanEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerMutationOverflow();
        }

        private static void UnregisterListenerImmediate(IScanEventListener listener)
        {
            _listeners.Unregister(listener);
        }

        private static void QueueDeferredListenerMutation(IScanEventListener listener, byte op)
        {
            for (int i = 0; i < _deferredListenerMutationCount; i++)
            {
                if (!ReferenceEquals(_deferredListenerMutations[i].Listener, listener))
                    continue;

                _deferredListenerMutationOps[i] = op;
                return;
            }

            if (_deferredListenerMutationCount >= DeferredListenerMutationCapacity)
            {
                ReportListenerMutationOverflow();
                return;
            }

            int writeIndex = _deferredListenerMutationCount++;
            _deferredListenerMutations[writeIndex].Listener = listener;
            _deferredListenerMutationOps[writeIndex] = op;
        }

        private static void ApplyDeferredListenerMutations()
        {
            int mutationCount = _deferredListenerMutationCount;
            if (mutationCount <= 0)
                return;

            _deferredListenerMutationCount = 0;
            for (int i = 0; i < mutationCount; i++)
            {
                IScanEventListener listener = _deferredListenerMutations[i].Listener;
                byte op = _deferredListenerMutationOps[i];
                _deferredListenerMutations[i].Clear();
                _deferredListenerMutationOps[i] = 0;

                if (listener == null)
                    continue;

                if (op == ListenerMutationRegister)
                    RegisterListenerImmediate(listener);
                else if (op == ListenerMutationUnregister)
                    UnregisterListenerImmediate(listener);
            }
        }

        private static void ClearDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredListenerMutationCount; i++)
            {
                _deferredListenerMutations[i].Clear();
                _deferredListenerMutationOps[i] = 0;
            }

            _deferredListenerMutationCount = 0;
        }

        private static bool IsDeferredUnregisterPending(IScanEventListener listener)
        {
            for (int i = 0; i < _deferredListenerMutationCount; i++)
            {
                if (_deferredListenerMutationOps[i] != ListenerMutationUnregister)
                    continue;

                if (ReferenceEquals(_deferredListenerMutations[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void DispatchToListener(IScanEventListener listener, in ScanEventPayload payload)
        {
            try
            {
                listener.OnScanEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogException(exception);
#endif
        }

        private static void ReportEventQueueOverflow()
        {
            IncrementCounterSaturated(ref _droppedEventCount);
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastEventOverflowTelemetryFrame == frame)
                return;

            _lastEventOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ScanEventOverflowWarningHash,
                ScanEventOverflowContextHash,
                math.max(1, _droppedEventCount));
        }

        private static void ReportListenerMutationOverflow()
        {
            IncrementCounterSaturated(ref _droppedDeferredListenerMutationCount);
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ScanListenerOverflowWarningHash,
                ScanListenerMutationContextHash,
                math.max(1, _droppedDeferredListenerMutationCount));
        }

        private static void ReportListenerDispatchException()
        {
            IncrementCounterSaturated(ref _listenerExceptionCount);
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ScanListenerExceptionWarningHash,
                ScanListenerExceptionContextHash,
                math.max(1, _listenerExceptionCount));
        }

        [Obsolete("Use TryRaiseScanTriggered(float3,float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseScanTriggered(float3 center, float radius)
        {
            TryRaiseScanTriggered(center, radius);
        }

        public static bool TryRaiseScanTriggered(float3 center, float radius)
        {
            return Enqueue(new ScanEventPayload
            {
                Position = center,
                Radius = radius,
                EntryHash = 0u,
                TitleHash = 0u,
                CategoryHash = 0u,
                SummaryHash = 0u,
                EventType = (ushort)ScanEventType.ScanTriggered,
                EntryKind = (byte)ScanEntryKind.Unknown,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseWreckSignalPing(float3,float) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseWreckSignalPing(float3 center, float radius)
        {
            return TryRaiseWreckSignalPing(center, radius);
        }

        public static bool TryRaiseWreckSignalPing(float3 center, float radius)
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextWreckSignalTime)
                return false;

            bool queued = Enqueue(new ScanEventPayload
            {
                Position = center,
                Radius = radius,
                EntryHash = 0u,
                TitleHash = 0u,
                CategoryHash = 0u,
                SummaryHash = 0u,
                EventType = (ushort)ScanEventType.ScanTriggered,
                EntryKind = (byte)ScanEntryKind.Scannable,
                Reserved = WreckSignalReservedMarker
            });
            if (queued)
                _nextWreckSignalTime = now + WreckSignalDebounceSeconds;

            return queued;
        }

        [Obsolete("Use TryRaiseNodeFound(float3) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseNodeFound(float3 worldPos)
        {
            TryRaiseNodeFound(worldPos);
        }

        public static bool TryRaiseNodeFound(float3 worldPos)
        {
            return Enqueue(new ScanEventPayload
            {
                Position = worldPos,
                Radius = 0f,
                EntryHash = 0u,
                TitleHash = 0u,
                CategoryHash = 0u,
                SummaryHash = 0u,
                EventType = (ushort)ScanEventType.NodeFound,
                EntryKind = (byte)ScanEntryKind.ResourceNode,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseEntryDiscovered(uint,uint,uint,uint,ScanEntryKind). String ingress is not allowed on first-party event lanes.", true)]
        public static void RaiseEntryDiscovered(
            string entryId,
            string title,
            string category,
            string summary,
            ScanEntryKind kind = ScanEntryKind.Unknown)
        {
            TryRaiseEntryDiscoveredFromString(entryId, title, category, summary, kind);
        }

        private static bool TryRaiseEntryDiscoveredFromString(
            string entryId,
            string title,
            string category,
            string summary,
            ScanEntryKind kind)
        {
            uint entryHash = ComputeEntryHash(entryId);
            if (entryHash == 0u)
                return false;

            uint titleHash = string.IsNullOrWhiteSpace(title) ? 0u : unchecked((uint)LocHash.Compute(title));
            uint categoryHash = string.IsNullOrWhiteSpace(category) ? 0u : unchecked((uint)LocHash.Compute(category));
            uint summaryHash = string.IsNullOrWhiteSpace(summary) ? 0u : unchecked((uint)LocHash.Compute(summary));

            if (!TryRaiseEntryDiscovered(entryHash, titleHash, categoryHash, summaryHash, kind))
                return false;

            StoreEntryMetadata(new ScanEntryMetadata(
                entryId,
                title,
                category,
                summary,
                kind,
                entryHash,
                titleHash,
                categoryHash,
                summaryHash));

            return true;
        }

        [Obsolete("Use TryRaiseEntryDiscovered(uint,uint,uint,uint,ScanEntryKind) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool RaiseEntryDiscovered(
            uint entryHash,
            uint titleHash,
            uint categoryHash,
            uint summaryHash,
            ScanEntryKind kind = ScanEntryKind.Unknown)
        {
            return TryRaiseEntryDiscovered(entryHash, titleHash, categoryHash, summaryHash, kind);
        }

        public static bool TryRaiseEntryDiscovered(
            uint entryHash,
            uint titleHash,
            uint categoryHash,
            uint summaryHash,
            ScanEntryKind kind = ScanEntryKind.Unknown)
        {
            if (entryHash == 0u)
                return false;

            return Enqueue(new ScanEventPayload
            {
                Position = default,
                Radius = 0f,
                EntryHash = entryHash,
                TitleHash = titleHash,
                CategoryHash = categoryHash,
                SummaryHash = summaryHash,
                EventType = (ushort)ScanEventType.EntryDiscovered,
                EntryKind = (byte)kind,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseFaunaFeedingObserved(uint,float3) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseFaunaFeedingObserved(uint entryHash, float3 worldPos)
        {
            TryRaiseFaunaFeedingObserved(entryHash, worldPos);
        }

        public static bool TryRaiseFaunaFeedingObserved(uint entryHash, float3 worldPos)
        {
            if (entryHash == 0u)
                return false;

            return Enqueue(new ScanEventPayload
            {
                Position = worldPos,
                Radius = 0f,
                EntryHash = entryHash,
                TitleHash = 0u,
                CategoryHash = 0u,
                SummaryHash = 0u,
                EventType = (ushort)ScanEventType.FaunaFeedingObserved,
                EntryKind = (byte)ScanEntryKind.Scannable,
                Reserved = 0
            });
        }

        [Obsolete("Use TryRaiseFaunaMatingObserved(uint,float3) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseFaunaMatingObserved(uint entryHash, float3 worldPos)
        {
            TryRaiseFaunaMatingObserved(entryHash, worldPos);
        }

        public static bool TryRaiseFaunaMatingObserved(uint entryHash, float3 worldPos)
        {
            if (entryHash == 0u)
                return false;

            return Enqueue(new ScanEventPayload
            {
                Position = worldPos,
                Radius = 0f,
                EntryHash = entryHash,
                TitleHash = 0u,
                CategoryHash = 0u,
                SummaryHash = 0u,
                EventType = (ushort)ScanEventType.FaunaMatingObserved,
                EntryKind = (byte)ScanEntryKind.Scannable,
                Reserved = 0
            });
        }

        private static bool Enqueue(in ScanEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportEventQueueOverflow();
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void EnsureInitialized()
        {
            if (!UnityEngine.Application.isPlaying)
                return;

            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<ScanEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ScanEventPayload>[16] - deferred scan event lane flushed by SystemDispatcher LateUpdate - owner: ScanEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<ScanEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ScanEventPayload>[16] - next-frame scan event lane prevents same-frame reentrant dispatch - owner: ScanEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _entryMetadataByHash.Clear();
                ClearEntryMetadataEvictionRing();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(ScanEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void StoreEntryMetadata(in ScanEntryMetadata metadata)
        {
            uint entryHash = metadata.EntryHash;
            if (entryHash == 0u)
                return;

            if (_entryMetadataByHash.ContainsKey(entryHash))
            {
                _entryMetadataByHash[entryHash] = metadata;
                return;
            }

            if (_entryMetadataEvictionCount >= EntryMetadataCapacity)
            {
                uint evictedHash = _entryMetadataEvictionRing[_entryMetadataEvictionWriteIndex];
                if (evictedHash != 0u && evictedHash != entryHash)
                    _entryMetadataByHash.Remove(evictedHash);
            }
            else
            {
                _entryMetadataEvictionCount++;
            }

            _entryMetadataEvictionRing[_entryMetadataEvictionWriteIndex] = entryHash;
            _entryMetadataEvictionWriteIndex++;
            if (_entryMetadataEvictionWriteIndex >= EntryMetadataCapacity)
                _entryMetadataEvictionWriteIndex = 0;
            _entryMetadataByHash[entryHash] = metadata;
        }

        private static void ClearEntryMetadataEvictionRing()
        {
            for (int i = 0; i < EntryMetadataCapacity; i++)
                _entryMetadataEvictionRing[i] = 0u;

            _entryMetadataEvictionWriteIndex = 0;
            _entryMetadataEvictionCount = 0;
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEvents.IsEmpty())
                PromoteNextFrameEventsIfFrontEmpty();

            if (_pendingEventCount > 0 &&
                !DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            {
                return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<ScanEventPayload> queue,
            ref int pendingCount)
        {
            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            int promoteBudget = _nextFrameEventCount;
            while (promoteBudget-- > 0 && !_nextFrameEvents.IsEmpty())
            {
                if (!_nextFrameEvents.TryDequeue(out ScanEventPayload payload))
                {
                    _nextFrameEventCount = 0;
                    break;
                }

                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
                if (_nextFrameEventCount > 0)
                    _nextFrameEventCount--;
            }

            if (_nextFrameEvents.IsEmpty())
                _nextFrameEventCount = 0;
        }
    }
}
