using System.Collections.Generic;
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

    [StructLayout(LayoutKind.Sequential)]
    public struct ScanEventPayload
    {
        public float3 Position;
        public float Radius;
        public uint EntryHash;
        public uint TitleHash;
        public uint CategoryHash;
        public uint SummaryHash;
        public ushort EventType;
        public byte EntryKind;
        public byte Reserved;
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
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<IScanEventListener>[16] - scan event listener registry drained on dispatcher LateUpdate - owner: ScanEvents
        private static readonly RegistryBucket<IScanEventListener> _listeners = new RegistryBucket<IScanEventListener>(16);
        // COLD ALLOC: Dictionary<uint,ScanEntryMetadata>[128] - hashed scan entry metadata cache for queue listeners that still own authored strings - owner: ScanEvents
        private static readonly Dictionary<uint, ScanEntryMetadata> _entryMetadataByHash = new Dictionary<uint, ScanEntryMetadata>(128);
        private static NativeQueue<ScanEventPayload> _pendingEvents;
        private static NativeQueue<ScanEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ScanEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ScanEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _entryMetadataByHash.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(IScanEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IScanEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
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
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IScanEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                        rawArray[i].OnScanEvent(in payload);
                }
                finally
                {
                    _isDispatching = false;
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

        public static void RaiseScanTriggered(float3 center, float radius)
        {
            Enqueue(new ScanEventPayload
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

        public static void RaiseNodeFound(float3 worldPos)
        {
            Enqueue(new ScanEventPayload
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

        public static void RaiseEntryDiscovered(
            string entryId,
            string title,
            string category,
            string summary,
            ScanEntryKind kind = ScanEntryKind.Unknown)
        {
            uint entryHash = ComputeEntryHash(entryId);
            if (entryHash == 0u)
                return;

            uint titleHash = string.IsNullOrWhiteSpace(title) ? 0u : unchecked((uint)LocHash.Compute(title));
            uint categoryHash = string.IsNullOrWhiteSpace(category) ? 0u : unchecked((uint)LocHash.Compute(category));
            uint summaryHash = string.IsNullOrWhiteSpace(summary) ? 0u : unchecked((uint)LocHash.Compute(summary));

            if (!Enqueue(new ScanEventPayload
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
            }))
            {
                return;
            }

            _entryMetadataByHash[entryHash] = new ScanEntryMetadata(
                entryId,
                title,
                category,
                summary,
                kind,
                entryHash,
                titleHash,
                categoryHash,
                summaryHash);
        }

        public static void RaiseFaunaFeedingObserved(uint entryHash, float3 worldPos)
        {
            if (entryHash == 0u)
                return;

            Enqueue(new ScanEventPayload
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

        public static void RaiseFaunaMatingObserved(uint entryHash, float3 worldPos)
        {
            if (entryHash == 0u)
                return;

            Enqueue(new ScanEventPayload
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
                return false;

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
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ScanEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ScanEventPayload>[16] - deferred scan event lane flushed by SystemDispatcher LateUpdate - owner: ScanEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(ScanEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<ScanEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ScanEventPayload>[16] - next-frame scan event lane prevents same-frame reentrant dispatch - owner: ScanEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(ScanEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
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
                    break;

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

            NativeQueue<ScanEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
