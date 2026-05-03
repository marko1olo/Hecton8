// ============================================================================
// HECTON-8 - ModuleStatusEvents.cs
// NativeQueue-backed BaseModule -> HUD/gameplay status lane.
// ============================================================================

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
[StructLayout(LayoutKind.Sequential)]
public struct ModuleStatusEventPayload
{
    public ulong ModuleEntityId;
    public uint ModuleHashId;
    public float Integrity01;
    public float AirReserve01;
    public float PowerSupply01;
    public int ReferenceSlot;
    public ushort EventType;
    public ushort StatusBits;

    public bool IsEnter => EventType == (ushort)ModuleStatusEventType.Enter;
    public bool IsFlooded => (StatusBits & 1u) != 0u;
    public bool IsBreached => (StatusBits & 2u) != 0u;
    public bool HasPower => (StatusBits & 4u) != 0u;
    public bool IsPlayerInsideInterior => (StatusBits & 8u) != 0u;
    public bool IsAirQualityLow => (StatusBits & 16u) != 0u;
    public bool HasCascadeFailure => (StatusBits & 32u) != 0u;
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
    private const ushort FloodedStatusBit = (ushort)(1 << 0);
    private const ushort BreachedStatusBit = (ushort)(1 << 1);
    private const ushort HasPowerStatusBit = (ushort)(1 << 2);
    private const ushort PlayerInsideStatusBit = (ushort)(1 << 3);
    private const ushort AirQualityLowStatusBit = (ushort)(1 << 4);
    private const ushort CascadeFailureStatusBit = (ushort)(1 << 5);

    private struct ModuleReferenceSlot
    {
        public BaseModule Module;

        public void Clear()
        {
            Module = null;
        }
    }

    // COLD ALLOC: RegistryBucket<IModuleStatusEventListener>[16] - module status listeners drained by SystemDispatcher LateUpdate - owner: ModuleStatusEvents
    private static readonly RegistryBucket<IModuleStatusEventListener> _listeners = new RegistryBucket<IModuleStatusEventListener>(ListenerCapacity);
    // COLD ALLOC: ModuleReferenceSlot[128] - managed BaseModule sidecar for unmanaged module status payloads - owner: ModuleStatusEvents
    private static readonly ModuleReferenceSlot[] _referenceSlots = new ModuleReferenceSlot[ReferenceSlotCapacity];
    // COLD ALLOC: bool[128] - sidecar occupancy map prevents overwrite before deferred dispatch - owner: ModuleStatusEvents
    private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
    private static NativeQueue<ModuleStatusEventPayload> _pendingEvents;
    private static int _referenceWriteIndex;
    private static int _referencePendingCount;
    private static int _pendingEventCount;

    /// <summary>
    /// Pending payload count in the native event lane.
    /// </summary>
    public static int PendingCount => _pendingEvents.IsCreated ? _pendingEventCount : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        if (_pendingEvents.IsCreated)
        {
            NativeMemorySentinel.UnregisterNativeQueue(nameof(ModuleStatusEvents), nameof(_pendingEvents));
            _pendingEvents.Dispose();
            _pendingEvents = default;
        }

        _listeners.Clear();
        ClearReferenceSlots();
        _referenceWriteIndex = 0;
        _referencePendingCount = 0;
        _pendingEventCount = 0;
    }

    /// <summary>
    /// Registers a module status listener.
    /// </summary>
    /// <param name="listener">Listener instance.</param>
    public static void Register(IModuleStatusEventListener listener)
    {
        if (listener == null)
            return;

        EnsureInitialized();
        if (!_listeners.Contains(listener))
            _listeners.Register(listener);
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

        int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
        while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
        {
            if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                return;

            if (!_pendingEvents.TryDequeue(out ModuleStatusEventPayload payload))
                break;

            if (_pendingEventCount > 0)
                _pendingEventCount--;

            IModuleStatusEventListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            for (int i = count - 1; i >= 0; i--)
                rawArray[i].OnModuleStatusEvent(in payload);

            ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        if (_pendingEvents.IsEmpty())
            _pendingEventCount = 0;
    }

    /// <summary>
    /// Resolves the module reference attached to a payload.
    /// Valid only during listener dispatch.
    /// </summary>
    public static bool TryResolveModule(in ModuleStatusEventPayload payload, out BaseModule module)
    {
        module = null;
        if (!IsValidReferenceSlot(payload.ReferenceSlot))
            return false;

        module = _referenceSlots[payload.ReferenceSlot].Module;
        return module != null;
    }

    /// <summary>
    /// Enqueues a module enter notification. Called from <see cref="BaseModule"/>.
    /// </summary>
    /// <param name="module">Entered module.</param>
    public static void NotifyEnter(BaseModule module)
    {
        Enqueue(ModuleStatusEventType.Enter, module);
    }

    /// <summary>
    /// Enqueues a module exit notification. Called from <see cref="BaseModule"/>.
    /// </summary>
    /// <param name="module">Exited module.</param>
    public static void NotifyExit(BaseModule module)
    {
        Enqueue(ModuleStatusEventType.Exit, module);
    }

    private static void Enqueue(ModuleStatusEventType eventType, BaseModule module)
    {
        if (module == null)
            return;

        if (!TryReserveReferenceSlot(out int referenceSlot))
            return;

        _referenceSlots[referenceSlot].Module = module;

        Enqueue(new ModuleStatusEventPayload
        {
            ModuleEntityId = EntityId.ToULong(module.GetEntityId()),
            ModuleHashId = ComputeModuleHash(module),
            Integrity01 = module.IntegrityStateNormalized,
            AirReserve01 = module.AirReserveNormalized,
            PowerSupply01 = module.PowerSupplyRatio,
            ReferenceSlot = referenceSlot,
            EventType = (ushort)eventType,
            StatusBits = ComputeStatusBits(module)
        });
    }

    private static void EnsureInitialized()
    {
        if (!_pendingEvents.IsCreated)
        {
            _pendingEvents = new NativeQueue<ModuleStatusEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModuleStatusEventPayload>[128] - deferred module status event lane flushed by SystemDispatcher LateUpdate - owner: ModuleStatusEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(ModuleStatusEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
        }
    }

    private static void Enqueue(in ModuleStatusEventPayload payload)
    {
        EnsureInitialized();
        if (_pendingEventCount >= PendingEventCapacity)
        {
            ReleaseReferenceSlot(payload.ReferenceSlot);
            return;
        }

        _pendingEvents.Enqueue(payload);
        _pendingEventCount++;
    }

    private static bool TryReserveReferenceSlot(out int referenceSlot)
    {
        referenceSlot = -1;
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
            _referenceSlotOccupied[referenceSlot] = true;
            _referencePendingCount++;
            return true;
        }

        return false;
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

    private static void DrainWithoutDispatch()
    {
        if (!_pendingEvents.IsCreated)
            return;

        int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
        while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
        {
            if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                return;

            if (!_pendingEvents.TryDequeue(out ModuleStatusEventPayload payload))
                break;

            if (_pendingEventCount > 0)
                _pendingEventCount--;

            ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        if (_pendingEvents.IsEmpty())
            _pendingEventCount = 0;
    }

    private static void ClearReferenceSlots()
    {
        for (int i = 0; i < ReferenceSlotCapacity; i++)
        {
            _referenceSlots[i].Clear();
            _referenceSlotOccupied[i] = false;
        }
    }

    private static uint ComputeModuleHash(BaseModule module)
    {
        var moduleTemplate = module.ModuleTemplate;
        return moduleTemplate != null
            ? unchecked((uint)moduleTemplate.TemplateHashId)
            : 0u;
    }

    private static ushort ComputeStatusBits(BaseModule module)
    {
        ushort statusBits = 0;
        if (module.IsFlooded)
            statusBits |= FloodedStatusBit;
        if (module.IsBreached)
            statusBits |= BreachedStatusBit;
        if (module.HasPower)
            statusBits |= HasPowerStatusBit;
        if (module.IsPlayerInsideInterior)
            statusBits |= PlayerInsideStatusBit;
        if (module.IsAirQualityLow)
            statusBits |= AirQualityLowStatusBit;
        if (module.HasCascadeFailure)
            statusBits |= CascadeFailureStatusBit;

        return statusBits;
    }
}
