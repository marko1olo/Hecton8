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
        EntryDiscovered = 2
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
        // COLD ALLOC: RegistryBucket<IScanEventListener>[16] - scan event listener registry drained on dispatcher LateUpdate - owner: ScanEvents
        private static readonly RegistryBucket<IScanEventListener> _listeners = new RegistryBucket<IScanEventListener>(16);
        // COLD ALLOC: Dictionary<uint,ScanEntryMetadata>[128] - hashed scan entry metadata cache for queue listeners that still own authored strings - owner: ScanEvents
        private static readonly Dictionary<uint, ScanEntryMetadata> _entryMetadataByHash = new Dictionary<uint, ScanEntryMetadata>(128);
        private static NativeQueue<ScanEventPayload> _pendingEvents;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _entryMetadataByHash.Clear();
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

            while (_pendingEvents.TryDequeue(out ScanEventPayload payload))
            {
                IScanEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnScanEvent(in payload);
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
            EnsureInitialized();
            _pendingEvents.Enqueue(new ScanEventPayload
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
            EnsureInitialized();
            _pendingEvents.Enqueue(new ScanEventPayload
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

            EnsureInitialized();
            _pendingEvents.Enqueue(new ScanEventPayload
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ScanEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ScanEventPayload>[16] - deferred scan event lane flushed by SystemDispatcher LateUpdate - owner: ScanEvents
            }
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}
