// ============================================================================
// HECTON-8 - ModuleStatusEvents.cs
// NativeQueue-backed BaseModule -> HUD/gameplay status lane.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Module status event discriminator for <see cref="ModuleStatusEventPayload"/>.
/// </summary>
public enum ModuleStatusEventType : byte
{
    Enter = 0,
    Exit = 1
}

/// <summary>
/// Unmanaged module status payload drained by <see cref="SystemDispatcher"/> in LateUpdate.
/// Managed <see cref="BaseModule"/> references are resolved through the sidecar only during dispatch.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct ModuleStatusEventPayload
{
    [FieldOffset(0)] public ulong ModuleEntityId;
    [FieldOffset(8)] public uint ModuleHashId;
    [FieldOffset(12)] public int ReferenceSlot;
    [FieldOffset(16)] public float Integrity01;
    [FieldOffset(20)] public float AirReserve01;
    [FieldOffset(24)] public float PowerSupply01;
    [FieldOffset(28)] public uint StatusFlags;
    [FieldOffset(32)] public ushort EventType;
    [FieldOffset(34)] public ushort Reserved;
    [FieldOffset(36)] private uint _pad0;
    [FieldOffset(40)] private ulong _pad1;
    [FieldOffset(48)] private ulong _pad2;
    [FieldOffset(56)] private ulong _pad3;
}

/// <summary>
/// Listener contract for deferred module status events.
/// </summary>
public interface IModuleStatusEventListener
{
    /// <summary>
    /// Called during SystemDispatcher late-frame event flush.
    /// </summary>
    /// <param name="payload">Unmanaged module status payload.</param>
    void OnModuleStatusEvent(in ModuleStatusEventPayload payload);
}

/// <summary>
/// Queue-backed event bus between <see cref="BaseModule"/> interior triggers and listeners.
/// </summary>
public static class ModuleStatusEvents
{
    private const int ListenerCapacity = 16;
    private const int PendingEventCapacity = 128;
    private const int ReferenceSlotCapacity = 128;
    private const uint FloodedStatusFlag = 1u << 0;
    private const uint BreachedStatusFlag = 1u << 1;
    private const uint HasPowerStatusFlag = 1u << 2;
    private const uint PlayerInsideStatusFlag = 1u << 3;
    private const uint AirQualityLowStatusFlag = 1u << 4;
    private const uint CascadeFailureStatusFlag = 1u << 5;
    private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

    public static bool IsEnterEvent(in ModuleStatusEventPayload payload)
    {
        return payload.EventType == (ushort)ModuleStatusEventType.Enter;
    }

    public static bool IsFlooded(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & FloodedStatusFlag) != 0u;
    }

    public static bool IsBreached(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & BreachedStatusFlag) != 0u;
    }

    public static bool HasPower(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & HasPowerStatusFlag) != 0u;
    }

    public static bool IsPlayerInsideInterior(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & PlayerInsideStatusFlag) != 0u;
    }

    public static bool IsAirQualityLow(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & AirQualityLowStatusFlag) != 0u;
    }

    public static bool HasCascadeFailure(in ModuleStatusEventPayload payload)
    {
        return (payload.StatusFlags & CascadeFailureStatusFlag) != 0u;
    }

    private struct ModuleReferenceSlot
    {
        public BaseModule Module;

        public void Clear()
        {
            Module = null;
        }
    }

    private struct ListenerSlot
    {
        public IModuleStatusEventListener Listener;

        public void Clear()
        {
            Listener = null;
        }
    }

    private struct ModuleStatusListenerRegistry
    {
        private readonly ListenerSlot[] _slots;
        private int _count;

        public ModuleStatusListenerRegistry(int capacity)
        {
            _slots = new ListenerSlot[capacity];
            _count = 0;
        }

        public int Count => _count;

        public void Clear()
        {
            for (int i = 0; i < _count; i++)
                _slots[i].Clear();

            _count = 0;
        }

        public bool Contains(IModuleStatusEventListener listener)
        {
            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(_slots[i].Listener, listener))
                    return true;
            }

            return false;
        }

        public bool TryRegister(IModuleStatusEventListener listener)
        {
            if (listener == null || _count >= _slots.Length)
                return false;

            _slots[_count++].Listener = listener;
            return true;
        }

        public void Unregister(IModuleStatusEventListener listener)
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

        public IModuleStatusEventListener GetAt(int index)
        {
            return (uint)index < (uint)_count ? _slots[index].Listener : null;
        }
    }

    // COLD ALLOC: ListenerSlot[16] — module status listeners drained by SystemDispatcher LateUpdate — owner: ModuleStatusEvents
    private static ModuleStatusListenerRegistry _listeners = new ModuleStatusListenerRegistry(ListenerCapacity);
    // COLD ALLOC: ModuleReferenceSlot[128] — managed BaseModule sidecar for unmanaged module status payloads — owner: ModuleStatusEvents
    private static readonly ModuleReferenceSlot[] _referenceSlots = new ModuleReferenceSlot[ReferenceSlotCapacity];
    // COLD ALLOC: bool[128] — sidecar occupancy map prevents overwrite before deferred dispatch — owner: ModuleStatusEvents
    private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
    // COLD ALLOC: ushort[128] - reference slot generations invalidate stale payload handles after sidecar reuse - owner: ModuleStatusEvents
    private static readonly ushort[] _referenceSlotGenerations = new ushort[ReferenceSlotCapacity];
    private static NativeQueue<ModuleStatusEventPayload> _pendingEvents;
    private static NativeQueue<ModuleStatusEventPayload> _nextFrameEvents;
    private static int _pendingEventsSentinelId;
    private static int _nextFrameEventsSentinelId;
    private static int _referenceWriteIndex;
    private static int _referencePendingCount;
    private static int _pendingEventCount;
    private static int _nextFrameEventCount;
    private static int _droppedEventCount;
    private static int _droppedReferenceSlotCount;
    private static bool _isDispatching;

    /// <summary>
    /// Pending payload count in the native event lane.
    /// </summary>
    public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
    public static int DroppedEventCount => _droppedEventCount;
    public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ReleaseNativeQueues();

        _listeners.Clear();
        ClearReferenceSlots();
        _referenceWriteIndex = 0;
        _referencePendingCount = 0;
        _pendingEventCount = 0;
        _nextFrameEventCount = 0;
        _droppedEventCount = 0;
        _droppedReferenceSlotCount = 0;
        _isDispatching = false;
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterEditorPlayModeTeardown()
    {
        UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
        UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
        UnityEditor.EditorApplication.quitting -= ResetStaticState;
        UnityEditor.EditorApplication.quitting += ResetStaticState;
    }

    private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
    {
        if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
            change == UnityEditor.PlayModeStateChange.EnteredEditMode)
        {
            ResetStaticState();
        }
    }
#endif

    /// <summary>
    /// Registers a module status listener.
    /// </summary>
    /// <param name="listener">Listener instance.</param>
    public static void Register(IModuleStatusEventListener listener)
    {
        if (listener == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        EnsureInitialized();
        if (!_listeners.Contains(listener))
            _listeners.TryRegister(listener);
    }

    /// <summary>
    /// Unregisters a module status listener.
    /// </summary>
    /// <param name="listener">Listener instance.</param>
    public static void Unregister(IModuleStatusEventListener listener)
    {
        if (listener == null)
            return;

        if (_listeners.Contains(listener))
            _listeners.Unregister(listener);
    }

    /// <summary>
    /// Flushes queued module events to registered listeners.
    /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
    /// </summary>
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

            if (!_pendingEvents.TryDequeue(out ModuleStatusEventPayload payload))
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
                    IModuleStatusEventListener listener = _listeners.GetAt(i);
                    if (listener != null)
                        listener.OnModuleStatusEvent(in payload);
                }
            }
            finally
            {
                _isDispatching = false;
            }

            ReleaseReferenceSlotForPayload(in payload);
        }

        if (_pendingEvents.IsEmpty())
        {
            _pendingEventCount = 0;
            PromoteNextFrameEventsIfFrontEmpty();
        }
    }

    /// <summary>
    /// Resolves the module reference attached to a payload.
    /// Valid only during listener dispatch.
    /// </summary>
    public static bool TryResolveModule(in ModuleStatusEventPayload payload, out BaseModule module)
    {
        module = null;
        if (!IsReferenceSlotPayloadCurrent(in payload))
            return false;

        module = _referenceSlots[payload.ReferenceSlot].Module;
        return module != null;
    }

    /// <summary>
    /// Enqueues a module enter notification. Called from <see cref="BaseModule"/>.
    /// </summary>
    /// <param name="module">Entered module.</param>
    [System.Obsolete("Use TryNotifyEnter(BaseModule) so bounded enqueue refusal is visible.", true)]
    public static void NotifyEnter(BaseModule module)
    {
        TryNotifyEnter(module);
    }

    public static bool TryNotifyEnter(BaseModule module)
    {
        return Enqueue(ModuleStatusEventType.Enter, module);
    }

    /// <summary>
    /// Enqueues a module exit notification. Called from <see cref="BaseModule"/>.
    /// </summary>
    /// <param name="module">Exited module.</param>
    [System.Obsolete("Use TryNotifyExit(BaseModule) so bounded enqueue refusal is visible.", true)]
    public static void NotifyExit(BaseModule module)
    {
        TryNotifyExit(module);
    }

    public static bool TryNotifyExit(BaseModule module)
    {
        return Enqueue(ModuleStatusEventType.Exit, module);
    }

    private static bool Enqueue(ModuleStatusEventType eventType, BaseModule module)
    {
        if (module == null)
            return false;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return false;
#endif

        if (!TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration))
        {
            _droppedReferenceSlotCount++;
            return false;
        }

        _referenceSlots[referenceSlot].Module = module;

        return Enqueue(new ModuleStatusEventPayload
        {
            ModuleEntityId = EntityId.ToULong(module.GetEntityId()),
            ModuleHashId = ComputeModuleHash(module),
            Integrity01 = module.IntegrityStateNormalized,
            AirReserve01 = module.AirReserveNormalized,
            PowerSupply01 = module.PowerSupplyRatio,
            ReferenceSlot = referenceSlot,
            EventType = (ushort)eventType,
            Reserved = referenceGeneration,
            StatusFlags = ComputeStatusFlags(module)
        });
    }

    private static void EnsureInitialized()
    {
        try
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ModuleStatusEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModuleStatusEventPayload>[128] — deferred module status event lane flushed by SystemDispatcher LateUpdate — owner: ModuleStatusEvents
                RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<ModuleStatusEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModuleStatusEventPayload>[128] — next-frame module status lane prevents same-frame reentrant dispatch — owner: ModuleStatusEvents
                RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
        }
        catch
        {
            ReleaseNativeQueues();
            ClearReferenceSlots();
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
            nameof(ModuleStatusEvents),
            label,
            NativeAllocationLifetime.Session);
        if (sentinelId > 0)
            return;

        ReleaseNativeQueue(ref queue, ref sentinelId);
        throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
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

    private static bool Enqueue(in ModuleStatusEventPayload payload)
    {
        EnsureInitialized();
        if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
        {
            _droppedEventCount++;
            ReleaseReferenceSlotForPayload(in payload);
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

    private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)
    {
        referenceSlot = -1;
        referenceGeneration = 0;
        if (_referencePendingCount >= ReferenceSlotCapacity)
            return false;

        for (int probe = 0; probe < ReferenceSlotCapacity; probe++)
        {
            int candidateSlot = _referenceWriteIndex;
            _referenceWriteIndex++;
            if (_referenceWriteIndex >= ReferenceSlotCapacity)
                _referenceWriteIndex = 0;

            if (_referenceSlotOccupied[candidateSlot])
                continue;

            referenceSlot = candidateSlot;
            referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot);
            _referenceSlotOccupied[referenceSlot] = true;
            _referencePendingCount++;
            return true;
        }

        return false;
    }

    private static ushort AdvanceReferenceSlotGeneration(int referenceSlot)
    {
        ushort generation = unchecked((ushort)(_referenceSlotGenerations[referenceSlot] + 1));
        if (generation == 0)
            generation = 1;

        _referenceSlotGenerations[referenceSlot] = generation;
        return generation;
    }

    private static void ReleaseReferenceSlotForPayload(in ModuleStatusEventPayload payload)
    {
        if (IsReferenceSlotPayloadCurrent(in payload))
            ReleaseReferenceSlot(payload.ReferenceSlot);
    }

    private static void ReleaseReferenceSlot(int referenceSlot)
    {
        if (!IsValidReferenceSlot(referenceSlot))
            return;

        if (!_referenceSlotOccupied[referenceSlot])
            return;

        _referenceSlots[referenceSlot].Clear();
        _referenceSlotOccupied[referenceSlot] = false;
        if (_referencePendingCount > 0)
            _referencePendingCount--;
    }

    private static bool IsValidReferenceSlot(int referenceSlot)
    {
        return (uint)referenceSlot < ReferenceSlotCapacity;
    }

    private static bool IsReferenceSlotPayloadCurrent(in ModuleStatusEventPayload payload)
    {
        int referenceSlot = payload.ReferenceSlot;
        return IsValidReferenceSlot(referenceSlot) &&
               _referenceSlotOccupied[referenceSlot] &&
               payload.Reserved != 0 &&
               _referenceSlotGenerations[referenceSlot] == payload.Reserved;
    }

    private static void DrainWithoutDispatch()
    {
        if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
            return;

        if (_pendingEventCount <= 0)
        {
            PromoteNextFrameEventsIfFrontEmpty();
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;
        }

        if (_nextFrameEvents.IsCreated)
            DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
    }

    private static bool DrainQueueWithoutDispatch(
        ref NativeQueue<ModuleStatusEventPayload> queue,
        ref int pendingCount)
    {
        if (!queue.IsCreated)
            return true;

        int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
        while (scanBudget-- > 0 && !queue.IsEmpty())
        {
            if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                return false;

            if (!queue.TryDequeue(out ModuleStatusEventPayload payload))
            {
                pendingCount = 0;
                break;
            }

            if (pendingCount > 0)
                pendingCount--;

            ReleaseReferenceSlotForPayload(in payload);
        }

        if (queue.IsEmpty())
            pendingCount = 0;

        return true;
    }

    private static void PromoteNextFrameEventsIfFrontEmpty()
    {
        if (!_pendingEvents.IsCreated ||
            !_nextFrameEvents.IsCreated ||
            _pendingEventCount > 0 ||
            _nextFrameEventCount <= 0)
        {
            return;
        }

        NativeQueue<ModuleStatusEventPayload> swap = _pendingEvents;
        _pendingEvents = _nextFrameEvents;
        _nextFrameEvents = swap;
        int sentinelIdSwap = _pendingEventsSentinelId;
        _pendingEventsSentinelId = _nextFrameEventsSentinelId;
        _nextFrameEventsSentinelId = sentinelIdSwap;
        _pendingEventCount = _nextFrameEventCount;
        _nextFrameEventCount = 0;
    }

    private static void ClearReferenceSlots()
    {
        for (int i = 0; i < ReferenceSlotCapacity; i++)
        {
            _referenceSlots[i].Clear();
            _referenceSlotOccupied[i] = false;
            AdvanceReferenceSlotGeneration(i);
        }
    }

    private static uint ComputeModuleHash(BaseModule module)
    {
        var moduleTemplate = module.ModuleTemplate;
        return moduleTemplate != null
            ? unchecked((uint)moduleTemplate.ResolvePersistentHashId())
            : 0u;
    }

    private static uint ComputeStatusFlags(BaseModule module)
    {
        uint statusFlags = 0u;
        if (module.IsFlooded)
            statusFlags |= FloodedStatusFlag;
        if (module.IsBreached)
            statusFlags |= BreachedStatusFlag;
        if (module.HasPower)
            statusFlags |= HasPowerStatusFlag;
        if (module.IsPlayerInsideInterior)
            statusFlags |= PlayerInsideStatusFlag;
        if (module.IsAirQualityLow)
            statusFlags |= AirQualityLowStatusFlag;
        if (module.HasCascadeFailure)
            statusFlags |= CascadeFailureStatusFlag;

        return statusFlags;
    }
}
