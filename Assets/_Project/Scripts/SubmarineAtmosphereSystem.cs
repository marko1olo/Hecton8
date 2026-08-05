using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Crafting;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Narrative;
using Hecton8.Visor;
using Hecton8.World;
using NASAPunk.Visor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using PressureImpulseEvent = Hecton8.Core.Contracts.Physics.PressureImpulseEvent;
using SubmarineFluidDynamics = Hecton8.Physics.SubmarineFluidDynamics;

namespace Hecton8.Atmosphere
{
    internal static class SubmarineAtmosphereVaultBufferIds
    {
        public const BufferID HighPressurePendingEvents = BufferID.SubmarineAtmosphereSystem_HighPressurePendingEvents;
        public const BufferID HighPressureNextFrameEvents = BufferID.SubmarineAtmosphereSystem_HighPressureNextFrameEvents;
        public const BufferID FatalPressurePendingEvents = BufferID.SubmarineAtmosphereSystem_FatalPressurePendingEvents;
        public const BufferID FatalPressureNextFrameEvents = BufferID.SubmarineAtmosphereSystem_FatalPressureNextFrameEvents;
        public const BufferID RoomVolumes = BufferID.SubmarineAtmosphereSystem_RoomVolumes;
        public const BufferID FloodVolumes = BufferID.SubmarineAtmosphereSystem_FloodVolumes;
        public const BufferID O2Front = BufferID.SubmarineAtmosphereSystem_O2Front;
        public const BufferID O2Back = BufferID.SubmarineAtmosphereSystem_O2Back;
        public const BufferID Co2Front = BufferID.SubmarineAtmosphereSystem_Co2Front;
        public const BufferID Co2Back = BufferID.SubmarineAtmosphereSystem_Co2Back;
        public const BufferID InertFront = BufferID.SubmarineAtmosphereSystem_InertFront;
        public const BufferID InertBack = BufferID.SubmarineAtmosphereSystem_InertBack;
        public const BufferID PressureFront = BufferID.SubmarineAtmosphereSystem_PressureFront;
        public const BufferID PressureBack = BufferID.SubmarineAtmosphereSystem_PressureBack;
        public const BufferID O2PartialPressureFront = BufferID.SubmarineAtmosphereSystem_O2PartialPressureFront;
        public const BufferID O2PartialPressureBack = BufferID.SubmarineAtmosphereSystem_O2PartialPressureBack;
        public const BufferID Co2PartialPressureFront = BufferID.SubmarineAtmosphereSystem_Co2PartialPressureFront;
        public const BufferID Co2PartialPressureBack = BufferID.SubmarineAtmosphereSystem_Co2PartialPressureBack;
        public const BufferID N2PartialPressureFront = BufferID.SubmarineAtmosphereSystem_N2PartialPressureFront;
        public const BufferID N2PartialPressureBack = BufferID.SubmarineAtmosphereSystem_N2PartialPressureBack;
        public const BufferID GasVolumeFront = BufferID.SubmarineAtmosphereSystem_GasVolumeFront;
        public const BufferID GasVolumeBack = BufferID.SubmarineAtmosphereSystem_GasVolumeBack;
        public const BufferID O2ConsumptionRates = BufferID.SubmarineAtmosphereSystem_O2ConsumptionRates;
        public const BufferID Co2GenerationRates = BufferID.SubmarineAtmosphereSystem_Co2GenerationRates;
        public const BufferID RoomPlayerCounts = BufferID.SubmarineAtmosphereSystem_RoomPlayerCounts;
        public const BufferID TemperatureFront = BufferID.SubmarineAtmosphereSystem_TemperatureFront;
        public const BufferID TemperatureBack = BufferID.SubmarineAtmosphereSystem_TemperatureBack;
        public const BufferID SteamFront = BufferID.SubmarineAtmosphereSystem_SteamFront;
        public const BufferID SteamBack = BufferID.SubmarineAtmosphereSystem_SteamBack;
        public const BufferID HydrogenPocketFront = BufferID.SubmarineAtmosphereSystem_HydrogenPocketFront;
        public const BufferID OxygenPocketFront = BufferID.SubmarineAtmosphereSystem_OxygenPocketFront;
        public const BufferID RoomHeatWatts = BufferID.SubmarineAtmosphereSystem_RoomHeatWatts;
        public const BufferID RoomStatusMaskFront = BufferID.SubmarineAtmosphereSystem_RoomStatusMaskFront;
        public const BufferID RoomStatusMaskBack = BufferID.SubmarineAtmosphereSystem_RoomStatusMaskBack;
        public const BufferID DoorPairs = BufferID.SubmarineAtmosphereSystem_DoorPairs;
        public const BufferID DoorSealed = BufferID.SubmarineAtmosphereSystem_DoorSealed;
        public const BufferID DoorSealedPrevious = BufferID.SubmarineAtmosphereSystem_DoorSealedPrevious;
        public const BufferID TelemetryRing = BufferID.SubmarineAtmosphereSystem_TelemetryRing;
        public const BufferID TelemetryCursor = BufferID.SubmarineAtmosphereSystem_TelemetryCursor;
    }

    internal static class AtmosphereEventPayloadSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteOrZero(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector3 RuntimePositionOrZero(Vector3 runtimePosition)
        {
            float3 value = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            return math.all(math.isfinite(value)) ? runtimePosition : Vector3.zero;
        }
    }

    /// <summary>
    /// Pressure discontinuity emitted when a sealed bulkhead opens into unequal room pressures.
    /// </summary>
    public readonly struct HighPressureEvent
    {
        /// <summary>
        /// Creates a high-pressure door-opening payload.
        /// </summary>
        public HighPressureEvent(int doorIndex, int roomA, int roomB, float pressureAKPa, float pressureBKPa, Vector3 runtimePosition)
        {
            float safePressureA = AtmosphereEventPayloadSanitizer.FiniteNonNegativeOrZero(pressureAKPa);
            float safePressureB = AtmosphereEventPayloadSanitizer.FiniteNonNegativeOrZero(pressureBKPa);
            DoorIndex = doorIndex;
            RoomA = roomA;
            RoomB = roomB;
            PressureAKPa = safePressureA;
            PressureBKPa = safePressureB;
            PressureDeltaKPa = math.abs(safePressureA - safePressureB);
            RuntimePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(runtimePosition);
        }

        /// <summary>Bulkhead edge index inside the compartment graph.</summary>
        public readonly int DoorIndex;

        /// <summary>First room linked by the opened bulkhead.</summary>
        public readonly int RoomA;

        /// <summary>Second room linked by the opened bulkhead.</summary>
        public readonly int RoomB;

        /// <summary>Pressure in room A at the moment of opening.</summary>
        public readonly float PressureAKPa;

        /// <summary>Pressure in room B at the moment of opening.</summary>
        public readonly float PressureBKPa;

        /// <summary>Absolute pressure difference across the opened bulkhead.</summary>
        public readonly float PressureDeltaKPa;

        /// <summary>Runtime-space midpoint for downstream VFX or alarm placement.</summary>
        public readonly Vector3 RuntimePosition;
    }

    /// <summary>
    /// Unmanaged high-pressure warning payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HighPressureEventPayload
    {
        [FieldOffset(0)]
        public float RuntimePositionX;
        [FieldOffset(4)]
        public float RuntimePositionY;
        [FieldOffset(8)]
        public float RuntimePositionZ;
        [FieldOffset(12)]
        public float PressureAKPa;
        [FieldOffset(16)]
        public float PressureBKPa;
        [FieldOffset(20)]
        public int DoorIndex;
        [FieldOffset(24)]
        public int RoomA;
        [FieldOffset(28)]
        public int RoomB;

        public Vector3 RuntimePosition
        {
            readonly get => new Vector3(RuntimePositionX, RuntimePositionY, RuntimePositionZ);
            set
            {
                Vector3 safePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(value);
                RuntimePositionX = safePosition.x;
                RuntimePositionY = safePosition.y;
                RuntimePositionZ = safePosition.z;
            }
        }
    }

    /// <summary>
    /// Listener for deferred high-pressure warnings.
    /// </summary>
    public interface IHighPressureEventListener
    {
        void OnHighPressure(in HighPressureEvent pressureEvent);
    }

    /// <summary>
    /// DataVault-backed high-pressure warning bus for submarine bulkhead events.
    /// </summary>
    public static class HighPressureEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 32;
        private const SystemID EventOwnerSystemId = SystemID.HabitatAtmosphere;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("HighPressureEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("HighPressureEvents"));
        private static readonly uint _duplicateListenerWarningHash = unchecked((uint)LocHash.Compute("HighPressureEvents.DuplicateListener"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)LocHash.Compute("HighPressureEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)LocHash.Compute("HighPressureEvents.ListenerException"));
        private static readonly uint _listenerHash = unchecked((uint)LocHash.Compute("HighPressureEvents.Listener"));

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - high-pressure listeners drained by SystemDispatcher LateUpdate - owner: HighPressureEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - high-pressure listener additions deferred during dispatch - owner: HighPressureEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - high-pressure listener removals deferred during dispatch - owner: HighPressureEvents
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<HighPressureEventPayload> _pendingEventsHandle;
        private static VaultGenerationHandle<HighPressureEventPayload> _nextFrameEventsHandle;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _duplicateListenerRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;
        private static int _lastDuplicateListenerWarningFrame = -1;
        private static int _lastListenerRejectedWarningFrame = -1;
        private static int _lastListenerExceptionWarningFrame = -1;

        /// <summary>Number of high-pressure payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseEventBuffer(ref _pendingEventsHandle);
            ReleaseEventBuffer(ref _nextFrameEventsHandle);

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            System.Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            System.Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _duplicateListenerRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
            _lastDuplicateListenerWarningFrame = -1;
            _lastListenerRejectedWarningFrame = -1;
            _lastListenerExceptionWarningFrame = -1;
            _dataVault = null;
        }

        /// <summary>Registers one high-pressure warning listener.</summary>
        public static void Register(IHighPressureEventListener listener)
        {
            if (listener == null)
                return;

            PrepareCold();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>Unregisters one high-pressure warning listener.</summary>
        public static void Unregister(IHighPressureEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
        }

        internal static void Shutdown()
        {
            ResetStaticState();
        }

        internal static void PrepareCold()
        {
            EnsureInitialized();
        }

        /// <summary>Flushes queued high-pressure warnings.</summary>
        public static void FlushPending()
        {
            if (!TryResolveEventBuffer(in _pendingEventsHandle, out _))
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && _pendingEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryDequeuePending(out HighPressureEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        /// <summary>Emits a high-pressure warning payload.</summary>
        [System.Obsolete("Use TryNotify(in HighPressureEvent) so bounded queue rejection stays visible at the producer.", true)]
        public static void Notify(in HighPressureEvent pressureEvent)
        {
            TryNotify(in pressureEvent);
        }

        public static bool TryNotify(in HighPressureEvent pressureEvent)
        {
            if (_listenerCount <= 0)
                return false;

            return Enqueue(new HighPressureEventPayload
            {
                RuntimePosition = pressureEvent.RuntimePosition,
                PressureAKPa = pressureEvent.PressureAKPa,
                PressureBKPa = pressureEvent.PressureBKPa,
                DoorIndex = pressureEvent.DoorIndex,
                RoomA = pressureEvent.RoomA,
                RoomB = pressureEvent.RoomB
            });
        }

        private static bool EnsureInitialized()
        {
            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return false;

            return EnsureEventBuffer(
                    vault,
                    ref _pendingEventsHandle,
                    SubmarineAtmosphereVaultBufferIds.HighPressurePendingEvents) &&
                EnsureEventBuffer(
                    vault,
                    ref _nextFrameEventsHandle,
                    SubmarineAtmosphereVaultBufferIds.HighPressureNextFrameEvents);
        }

        private static bool Enqueue(in HighPressureEventPayload payload)
        {
            if (_listenerCount <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            if (!IsInitialized())
            {
                RecordDroppedEvent();
                return false;
            }

            VaultGenerationHandle<HighPressureEventPayload> handle = _isDispatching
                ? _nextFrameEventsHandle
                : _pendingEventsHandle;
            int writeIndex = _isDispatching ? _nextFrameEventCount : _pendingEventCount;
            if (!TryWriteEvent(in handle, writeIndex, in payload))
            {
                RecordDroppedEvent();
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEventCount++;
                return true;
            }

            _pendingEventCount++;
            return true;
        }

        private static void Dispatch(in HighPressureEventPayload payload)
        {
            int count = _listenerCount;
            if (count <= 0)
                return;

            HighPressureEvent pressureEvent = new HighPressureEvent(
                payload.DoorIndex,
                payload.RoomA,
                payload.RoomB,
                payload.PressureAKPa,
                payload.PressureBKPa,
                payload.RuntimePosition);

            for (int i = count - 1; i >= 0; i--)
            {
                IHighPressureEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    DispatchToListener(listener, in pressureEvent);
            }
        }

        private static void RegisterImmediate(IHighPressureEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _listeners[_listenerCount].Listener = listener;
            _listenerCount++;
        }

        private static bool TryUnregisterImmediate(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void DispatchToListener(IHighPressureEventListener listener, in HighPressureEvent pressureEvent)
        {
            try
            {
                listener.OnHighPressure(in pressureEvent);
            }
            catch (System.Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IHighPressureEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                CancelDeferredUnregister(listener);
                ReportDuplicateListenerRegistration();
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IHighPressureEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IHighPressureEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IHighPressureEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IHighPressureEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private struct ListenerSlot
        {
            public IHighPressureEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            RecordDroppedEvent();
            PublishWarningOncePerFrame(_overflowWarningHash, _queueHash, PendingEventCapacity, ref _lastOverflowWarningFrame);
        }

        private static void RecordDroppedEvent()
        {
            _droppedEventCount = SaturatingIncrement(_droppedEventCount);
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateListenerRegistrationCount = SaturatingIncrement(_duplicateListenerRegistrationCount);
            PublishWarningOncePerFrame(
                _duplicateListenerWarningHash,
                _listenerHash,
                _duplicateListenerRegistrationCount,
                ref _lastDuplicateListenerWarningFrame);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount = SaturatingIncrement(_listenerRejectCount);
            PublishWarningOncePerFrame(
                _listenerRejectedWarningHash,
                _listenerHash,
                _listenerRejectCount,
                ref _lastListenerRejectedWarningFrame);
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount = SaturatingIncrement(_listenerExceptionCount);
            PublishWarningOncePerFrame(
                _listenerExceptionWarningHash,
                _listenerHash,
                _listenerExceptionCount,
                ref _lastListenerExceptionWarningFrame);
        }

        private static void PublishWarningOncePerFrame(uint warningHash, uint contextHash, float value, ref int lastWarningFrame)
        {
            int frame = ResolveCurrentFrameIndexSafe();
            if (frame >= 0 && lastWarningFrame == frame)
                return;

            lastWarningFrame = frame >= 0 ? frame : int.MinValue;
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (System.Exception exception)
            {
                LogListenerDispatchException(exception);
            }
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static int SaturatingIncrement(int value)
        {
            return value < int.MaxValue ? value + 1 : int.MaxValue;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!HasValidHandle(in _pendingEventsHandle) ||
                !HasValidHandle(in _nextFrameEventsHandle) ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            VaultGenerationHandle<HighPressureEventPayload> swap = _pendingEventsHandle;
            _pendingEventsHandle = _nextFrameEventsHandle;
            _nextFrameEventsHandle = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static bool IsInitialized()
        {
            return _dataVault != null &&
                   HasValidHandle(in _pendingEventsHandle) &&
                   HasValidHandle(in _nextFrameEventsHandle);
        }

        private static IDataVault ResolveDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private static bool EnsureEventBuffer(
            IDataVault vault,
            ref VaultGenerationHandle<HighPressureEventPayload> handle,
            BufferID bufferId)
        {
            if (HasValidHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)EventOwnerSystemId)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<HighPressureEventPayload>(
                bufferId,
                PendingEventCapacity,
                EventOwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            return HasValidHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)EventOwnerSystemId;
        }

        private static bool TryResolveEventBuffer(
            in VaultGenerationHandle<HighPressureEventPayload> handle,
            out NativeArray<HighPressureEventPayload>.ReadOnly buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                HasValidHandle(in handle) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= PendingEventCapacity;
        }

        private static bool TryWriteEvent(
            in VaultGenerationHandle<HighPressureEventPayload> handle,
            int index,
            in HighPressureEventPayload payload)
        {
            if ((uint)index >= (uint)PendingEventCapacity)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, EventOwnerSystemId, out NativeArray<HighPressureEventPayload> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length < PendingEventCapacity)
                    return false;

                buffer[index] = payload;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, EventOwnerSystemId);
            }
        }

        private static bool TryDequeuePending(out HighPressureEventPayload payload)
        {
            payload = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _pendingEventCount <= 0)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _pendingEventsHandle, EventOwnerSystemId, out NativeArray<HighPressureEventPayload> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length < PendingEventCapacity)
                    return false;

                payload = buffer[0];
                int lastIndex = _pendingEventCount - 1;
                for (int i = 0; i < lastIndex; i++)
                    buffer[i] = buffer[i + 1];
                buffer[lastIndex] = default;
                _pendingEventCount = lastIndex;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _pendingEventsHandle, EventOwnerSystemId);
            }
        }

        private static void ReleaseEventBuffer(ref VaultGenerationHandle<HighPressureEventPayload> handle)
        {
            IDataVault vault = _dataVault;
            if (vault != null && HasValidHandle(in handle))
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasValidHandle(in VaultGenerationHandle<HighPressureEventPayload> handle)
        {
            return handle.BufferID != 0u && handle.SystemID != 0u && handle.Generation != 0u;
        }
    }

    /// <summary>
    /// Fatal overload-driven implosion payload emitted when a superheated powered room catastrophically fails.
    /// </summary>
    public readonly struct FatalPressureImplosionEvent
    {
        public FatalPressureImplosionEvent(uint nodeId, int roomIndex, float temperatureCelsius, Vector3 runtimePosition)
        {
            NodeId = nodeId;
            RoomIndex = roomIndex;
            TemperatureCelsius = AtmosphereEventPayloadSanitizer.FiniteOrZero(temperatureCelsius);
            RuntimePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(runtimePosition);
        }

        public readonly uint NodeId;
        public readonly int RoomIndex;
        public readonly float TemperatureCelsius;
        public readonly Vector3 RuntimePosition;
    }

    /// <summary>
    /// Unmanaged fatal pressure implosion payload carried by the deferred event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FatalPressureImplosionEventPayload
    {
        [FieldOffset(0)]
        public float RuntimePositionX;
        [FieldOffset(4)]
        public float RuntimePositionY;
        [FieldOffset(8)]
        public float RuntimePositionZ;
        [FieldOffset(12)]
        public float TemperatureCelsius;
        [FieldOffset(16)]
        public uint NodeId;
        [FieldOffset(20)]
        public int RoomIndex;
        [FieldOffset(24)]
        private byte _pad0;
        [FieldOffset(25)]
        private byte _pad1;
        [FieldOffset(26)]
        private byte _pad2;
        [FieldOffset(27)]
        private byte _pad3;
        [FieldOffset(28)]
        private byte _pad4;
        [FieldOffset(29)]
        private byte _pad5;
        [FieldOffset(30)]
        private byte _pad6;
        [FieldOffset(31)]
        private byte _pad7;

        public Vector3 RuntimePosition
        {
            readonly get => new Vector3(RuntimePositionX, RuntimePositionY, RuntimePositionZ);
            set
            {
                Vector3 safePosition = AtmosphereEventPayloadSanitizer.RuntimePositionOrZero(value);
                RuntimePositionX = safePosition.x;
                RuntimePositionY = safePosition.y;
                RuntimePositionZ = safePosition.z;
            }
        }
    }

    /// <summary>
    /// Listener for deferred fatal pressure implosion events.
    /// </summary>
    public interface IFatalPressureImplosionEventListener
    {
        void OnFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent);
    }

    /// <summary>
    /// DataVault-backed fatal-implosion bus for catastrophic overload failures.
    /// </summary>
    public static class FatalPressureImplosionEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 8;
        private const SystemID EventOwnerSystemId = SystemID.HabitatAtmosphere;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents"));
        private static readonly uint _duplicateListenerWarningHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.DuplicateListener"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.ListenerException"));
        private static readonly uint _listenerHash = unchecked((uint)LocHash.Compute("FatalPressureImplosionEvents.Listener"));

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - fatal implosion listeners drained by SystemDispatcher LateUpdate - owner: FatalPressureImplosionEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - fatal implosion listener additions deferred during dispatch - owner: FatalPressureImplosionEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - fatal implosion listener removals deferred during dispatch - owner: FatalPressureImplosionEvents
        private static IDataVault _dataVault;
        private static VaultGenerationHandle<FatalPressureImplosionEventPayload> _pendingEventsHandle;
        private static VaultGenerationHandle<FatalPressureImplosionEventPayload> _nextFrameEventsHandle;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _duplicateListenerRegistrationCount;
        private static int _listenerRejectCount;
        private static int _listenerExceptionCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;
        private static int _lastDuplicateListenerWarningFrame = -1;
        private static int _lastListenerRejectedWarningFrame = -1;
        private static int _lastListenerExceptionWarningFrame = -1;

        /// <summary>Number of fatal implosion payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;
        public static int ListenerRejectCount => _listenerRejectCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseEventBuffer(ref _pendingEventsHandle);
            ReleaseEventBuffer(ref _nextFrameEventsHandle);

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            System.Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            System.Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _duplicateListenerRegistrationCount = 0;
            _listenerRejectCount = 0;
            _listenerExceptionCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
            _lastDuplicateListenerWarningFrame = -1;
            _lastListenerRejectedWarningFrame = -1;
            _lastListenerExceptionWarningFrame = -1;
            _dataVault = null;
        }

        /// <summary>Registers one fatal pressure implosion listener.</summary>
        public static void Register(IFatalPressureImplosionEventListener listener)
        {
            if (listener == null)
                return;

            PrepareCold();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>Unregisters one fatal pressure implosion listener.</summary>
        public static void Unregister(IFatalPressureImplosionEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
        }

        internal static void Shutdown()
        {
            ResetStaticState();
        }

        internal static void PrepareCold()
        {
            EnsureInitialized();
        }

        /// <summary>Flushes queued fatal pressure implosion payloads.</summary>
        public static void FlushPending()
        {
            if (!TryResolveEventBuffer(in _pendingEventsHandle, out _))
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && _pendingEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!TryDequeuePending(out FatalPressureImplosionEventPayload payload))
                {
                    _pendingEventCount = 0;
                    break;
                }

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        [System.Obsolete("Use TryNotify(in FatalPressureImplosionEvent) so bounded queue rejection stays visible at the producer.", true)]
        public static void Notify(in FatalPressureImplosionEvent implosionEvent)
        {
            TryNotify(in implosionEvent);
        }

        public static bool TryNotify(in FatalPressureImplosionEvent implosionEvent)
        {
            if (_listenerCount <= 0)
                return false;

            return Enqueue(new FatalPressureImplosionEventPayload
            {
                RuntimePosition = implosionEvent.RuntimePosition,
                TemperatureCelsius = implosionEvent.TemperatureCelsius,
                NodeId = implosionEvent.NodeId,
                RoomIndex = implosionEvent.RoomIndex
            });
        }

        private static bool EnsureInitialized()
        {
            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return false;

            return EnsureEventBuffer(
                    vault,
                    ref _pendingEventsHandle,
                    SubmarineAtmosphereVaultBufferIds.FatalPressurePendingEvents) &&
                EnsureEventBuffer(
                    vault,
                    ref _nextFrameEventsHandle,
                    SubmarineAtmosphereVaultBufferIds.FatalPressureNextFrameEvents);
        }

        private static bool Enqueue(in FatalPressureImplosionEventPayload payload)
        {
            if (_listenerCount <= 0)
                return false;

            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            if (!IsInitialized())
            {
                RecordDroppedEvent();
                return false;
            }

            VaultGenerationHandle<FatalPressureImplosionEventPayload> handle = _isDispatching
                ? _nextFrameEventsHandle
                : _pendingEventsHandle;
            int writeIndex = _isDispatching ? _nextFrameEventCount : _pendingEventCount;
            if (!TryWriteEvent(in handle, writeIndex, in payload))
            {
                RecordDroppedEvent();
                return false;
            }

            if (_isDispatching)
            {
                _nextFrameEventCount++;
                return true;
            }

            _pendingEventCount++;
            return true;
        }

        private static void Dispatch(in FatalPressureImplosionEventPayload payload)
        {
            int count = _listenerCount;
            if (count <= 0)
                return;

            FatalPressureImplosionEvent implosionEvent = new FatalPressureImplosionEvent(
                payload.NodeId,
                payload.RoomIndex,
                payload.TemperatureCelsius,
                payload.RuntimePosition);

            for (int i = count - 1; i >= 0; i--)
            {
                IFatalPressureImplosionEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    DispatchToListener(listener, in implosionEvent);
            }
        }

        private static void RegisterImmediate(IFatalPressureImplosionEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                ReportDuplicateListenerRegistration();
                return;
            }

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _listeners[_listenerCount].Listener = listener;
            _listenerCount++;
        }

        private static bool TryUnregisterImmediate(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void DispatchToListener(IFatalPressureImplosionEventListener listener, in FatalPressureImplosionEvent implosionEvent)
        {
            try
            {
                listener.OnFatalPressureImplosion(in implosionEvent);
            }
            catch (System.Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IFatalPressureImplosionEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                CancelDeferredUnregister(listener);
                ReportDuplicateListenerRegistration();
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IFatalPressureImplosionEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IFatalPressureImplosionEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IFatalPressureImplosionEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IFatalPressureImplosionEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private struct ListenerSlot
        {
            public IFatalPressureImplosionEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            RecordDroppedEvent();
            PublishWarningOncePerFrame(_overflowWarningHash, _queueHash, PendingEventCapacity, ref _lastOverflowWarningFrame);
        }

        private static void RecordDroppedEvent()
        {
            _droppedEventCount = SaturatingIncrement(_droppedEventCount);
        }

        private static void ReportDuplicateListenerRegistration()
        {
            _duplicateListenerRegistrationCount = SaturatingIncrement(_duplicateListenerRegistrationCount);
            PublishWarningOncePerFrame(
                _duplicateListenerWarningHash,
                _listenerHash,
                _duplicateListenerRegistrationCount,
                ref _lastDuplicateListenerWarningFrame);
        }

        private static void ReportListenerRejected()
        {
            _listenerRejectCount = SaturatingIncrement(_listenerRejectCount);
            PublishWarningOncePerFrame(
                _listenerRejectedWarningHash,
                _listenerHash,
                _listenerRejectCount,
                ref _lastListenerRejectedWarningFrame);
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount = SaturatingIncrement(_listenerExceptionCount);
            PublishWarningOncePerFrame(
                _listenerExceptionWarningHash,
                _listenerHash,
                _listenerExceptionCount,
                ref _lastListenerExceptionWarningFrame);
        }

        private static void PublishWarningOncePerFrame(uint warningHash, uint contextHash, float value, ref int lastWarningFrame)
        {
            int frame = ResolveCurrentFrameIndexSafe();
            if (frame >= 0 && lastWarningFrame == frame)
                return;

            lastWarningFrame = frame >= 0 ? frame : int.MinValue;
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (System.Exception exception)
            {
                LogListenerDispatchException(exception);
            }
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static int SaturatingIncrement(int value)
        {
            return value < int.MaxValue ? value + 1 : int.MaxValue;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!HasValidHandle(in _pendingEventsHandle) ||
                !HasValidHandle(in _nextFrameEventsHandle) ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            VaultGenerationHandle<FatalPressureImplosionEventPayload> swap = _pendingEventsHandle;
            _pendingEventsHandle = _nextFrameEventsHandle;
            _nextFrameEventsHandle = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static bool IsInitialized()
        {
            return _dataVault != null &&
                   HasValidHandle(in _pendingEventsHandle) &&
                   HasValidHandle(in _nextFrameEventsHandle);
        }

        private static IDataVault ResolveDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private static bool EnsureEventBuffer(
            IDataVault vault,
            ref VaultGenerationHandle<FatalPressureImplosionEventPayload> handle,
            BufferID bufferId)
        {
            if (HasValidHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)EventOwnerSystemId)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<FatalPressureImplosionEventPayload>(
                bufferId,
                PendingEventCapacity,
                EventOwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            return HasValidHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)EventOwnerSystemId;
        }

        private static bool TryResolveEventBuffer(
            in VaultGenerationHandle<FatalPressureImplosionEventPayload> handle,
            out NativeArray<FatalPressureImplosionEventPayload>.ReadOnly buffer)
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                HasValidHandle(in handle) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= PendingEventCapacity;
        }

        private static bool TryWriteEvent(
            in VaultGenerationHandle<FatalPressureImplosionEventPayload> handle,
            int index,
            in FatalPressureImplosionEventPayload payload)
        {
            if ((uint)index >= (uint)PendingEventCapacity)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, EventOwnerSystemId, out NativeArray<FatalPressureImplosionEventPayload> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length < PendingEventCapacity)
                    return false;

                buffer[index] = payload;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, EventOwnerSystemId);
            }
        }

        private static bool TryDequeuePending(out FatalPressureImplosionEventPayload payload)
        {
            payload = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _pendingEventCount <= 0)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _pendingEventsHandle, EventOwnerSystemId, out NativeArray<FatalPressureImplosionEventPayload> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated || buffer.Length < PendingEventCapacity)
                    return false;

                payload = buffer[0];
                int lastIndex = _pendingEventCount - 1;
                for (int i = 0; i < lastIndex; i++)
                    buffer[i] = buffer[i + 1];
                buffer[lastIndex] = default;
                _pendingEventCount = lastIndex;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _pendingEventsHandle, EventOwnerSystemId);
            }
        }

        private static void ReleaseEventBuffer(ref VaultGenerationHandle<FatalPressureImplosionEventPayload> handle)
        {
            IDataVault vault = _dataVault;
            if (vault != null && HasValidHandle(in handle))
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasValidHandle(in VaultGenerationHandle<FatalPressureImplosionEventPayload> handle)
        {
            return handle.BufferID != 0u && handle.SystemID != 0u && handle.Generation != 0u;
        }
    }

    /// <summary>
    /// Fixed-step pressurized interior simulation for submarines.
    /// Tracks room atmosphere state across the compartment graph and converts legacy tank units into Dalton partial-pressure snapshots.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SubmarineFluidDynamics))]
    [AddComponentMenu("Hecton/Atmosphere/Submarine Atmosphere System")]
    public sealed class SubmarineAtmosphereSystem : MonoBehaviour, IColdTickable, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IInteractionSignalConsumer, IGlobalRegistryHotSwapListener, ISubmarineAtmosphereRoomMutationSink
    {
        private const int RoomCapacity = 8;
        private const int DoorCapacity = 7;
        private const float DefaultHighPressureEventThresholdKPa = 150f;
        private const float DefaultReferencePressureKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float DefaultDoorConductance = 0.045f;
        private const float DefaultMaxTransferUnitsPerSecond = 1.5f;
        private const float DefaultMinimumGasVolumeCubicMeters = 0.05f;
        private const float DefaultMaximumPressureKPa = 400f;
        private const float DefaultPressureImpulseRadiusMeters = 2.5f;
        private const float DefaultPressureImpulseDurationSeconds = 0.12f;
        private const float DefaultPressureImpulseFalloffExponent = 1.5f;
        private const float DefaultMaximumPressureImpulseNewtonSeconds = 18000f;
        private const float DefaultInitialOxygenFraction = 0.2095f;
        private const float DefaultInitialCarbonDioxideFraction = 0.0004f;
        private const float DefaultInertFraction = 1f - DefaultInitialOxygenFraction - DefaultInitialCarbonDioxideFraction;
        private const float DefaultOxygenTankCapacity = 100f;
        private const float DefaultLowOxygenThreshold01 = 0.2f;
        private const float DefaultCarbonDioxideToxicityFraction = 0.05f;
        private const float DefaultInvCarbonDioxideToxicitySpan = 20f;
        private const float DefaultPlayerOxygenConsumptionPercentPerSecond = 0.5f;
        private const float DefaultAtmosphereSlowTickSeconds = 1f;
        private const int MaxAtmosphereCatchupTicks = 4;
        private const int MaxFakeAtmospherePlayerCountPerRoom = 4;
        private const float DefaultHeatWattsToCelsiusPerSecond = 0.001f;
        private const float DefaultOverheatBrownoutTemperatureCelsius = 80f;
        private const float DefaultOverheatMinimumVoltage = 0.18f;
        private const float DefaultToxicRoomHazardIntensity = 1f;
        private const float DefaultFireSmokeHazardIntensity = 0.85f;
        private const float DefaultRoomHazardRadiusPaddingMeters = 1.25f;
        private const float DefaultFireSmokeVisorGlitchBias = 1.15f;
        private const float DefaultLowOxygenAudioCooldownSeconds = 45f;
        private const float DefaultFreezingRoomTemperatureCelsius = 0f;
        private const float DefaultPressureScreechCooldownSeconds = 2.75f;
        private const float DefaultPressureScreechVolume = 0.82f;
        private const float DefaultPressureScreechPitchMin = 0.86f;
        private const float DefaultPressureScreechPitchMax = 1.08f;
        private const float DefaultToxicRoomVisorPulseCooldownSeconds = 0.35f;
        private const float DefaultToxicRoomVisorGlitchDurationSeconds = 0.08f;
        private const float DefaultToxicRoomVisorDistortionHoldSeconds = 0.06f;
        private const float DefaultToxicRoomVisorDistortionRecovery = 6f;
        private const float DefaultFireSmokeSootScreenRadiusScale = 0.08f;
        private const float DefaultFireSmokeSootDitherStrength = 0.58f;
        private const float DefaultFireSmokeSootDarkenStrength = 0.42f;
        private const float FakeHazardRadiusBaseMeters = 0.85f;
        private const float FakeHazardRadiusVolumeScale = 0.08f;
        private const float FakeHazardRadiusMaxMeters = 6f;
        private const float BlendFactorPadeInstantThreshold = 8f;
        private const string NativeMemoryOwner = nameof(SubmarineAtmosphereSystem);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const string TelemetryDumpPayloadLabel = "submarineAtmosphereTelemetryDumpPayload";
        private const int PressureImpulseOverlapCapacity = 32;
        private const int HeatEmitterCapacity = 24;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const uint TelemetryDumpMagic = 0x53415442u; // SATB: Submarine Atmosphere Telemetry Blackbox.
        private const ushort TelemetryDumpFormatVersion = 1;
        private const ushort TelemetryFlagNaN = 1 << 0;
        private const string TelemetryDumpFileName = "Dump_1323_SubmarineAtmosphere.bin";
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/" + TelemetryDumpFileName;
        private static readonly ulong AtmospherePhaseMutationGuardMask =
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomVolumes) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.FloodVolumes) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2Front) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2Back) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2Front) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2Back) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.InertFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.InertBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.PressureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.PressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2PartialPressureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2PartialPressureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.N2PartialPressureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.N2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.GasVolumeFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.GasVolumeBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2ConsumptionRates) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2GenerationRates) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomPlayerCounts) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TemperatureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TemperatureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.SteamFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.SteamBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.HydrogenPocketFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.OxygenPocketFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomHeatWatts) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomStatusMaskFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomStatusMaskBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.DoorPairs) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.DoorSealed) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.DoorSealedPrevious);
        private static readonly ulong AtmosphereJobMutationGuardMask =
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2Front) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2Front) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.InertFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.FloodVolumes) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomVolumes) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.PressureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.GasVolumeFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2ConsumptionRates) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2GenerationRates) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomPlayerCounts) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TemperatureFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomHeatWatts) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.SteamFront) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.DoorPairs) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.DoorSealed) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2Back) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2Back) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.InertBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.PressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.GasVolumeBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TemperatureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.SteamBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.O2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.Co2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.N2PartialPressureBack) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.RoomStatusMaskBack);
        private static readonly ulong AtmosphereTelemetryMutationGuardMask =
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TelemetryRing) |
            AtmosphereBufferGuardBit(SubmarineAtmosphereVaultBufferIds.TelemetryCursor);
        private const float DefaultReferenceTemperatureCelsius = 20f;
        private const float DefaultFloodWaterTemperatureCelsius = 4f;
        private const float DefaultMinimumTemperatureCelsius = -5f;
        private const float DefaultMaximumTemperatureCelsius = 90f;
        private const float DefaultDeepFreezeDepthThresholdMeters = 3000f;
        private const float DefaultDeepFreezeSupplyRatioThreshold = 0.1f;
        private const float DefaultDeepFreezeTauSeconds = 8f;
        private const float DefaultDeepFreezeTargetTemperatureCelsius = -3f;
        private const float DefaultBrownoutOxygenSupplyRatioThreshold = 0.40f;
        private const float DefaultBrownoutOccupiedRoomOxygenConsumptionUnitsPerSecond = 0.0008f;
        private const float DefaultAirDensityKilogramsPerCubicMeter = 1.225f;
        private const float DefaultAirSpecificHeatJoulesPerKilogramKelvin = 1005f;
        private const float DefaultWaterDensityKilogramsPerCubicMeter = 1027f;
        private const float DefaultWaterSpecificHeatJoulesPerKilogramKelvin = 3990f;
        private const float DefaultMinimumThermalCapacityJoulesPerKelvin = 400f;
        private const float DefaultBulkheadThermalConductivityWattsPerKelvin = 185f;
        private const float DefaultSealedBulkheadThermalCoupling = 0.35f;
        private const float DefaultOpenBulkheadThermalCoupling = 1f;
        private const float DefaultFabricatorHeatWattsScale = 0.92f;
        private const float DefaultDrillHeatWattsScale = 0.97f;
        private const float DefaultReactorHeatWattsScale = 1.15f;
        private const float DefaultBoilingFloodTemperatureCelsius = 80f;
        private const float DefaultBoilingFloodMinimumFillRatio = 0.15f;
        private const float DefaultBoilingHazardIntensity = 1.1f;
        private const float DefaultBoilingHazardRadiusPaddingMeters = 1.25f;
        private const float DefaultBoilingFaunaDamagePerSecond = 14f;
        private const float DefaultReactorMeltdownTemperatureCelsius = 150f;
        private const float DefaultReactorMeltdownImpulseDurationSeconds = 0.18f;
        private const float DefaultReactorMeltdownImpulsePerWattSecond = 42f;
        private const float DefaultReactorMeltdownMinimumImpulseNewtonSeconds = 3200f;
        private const float DefaultReactorMeltdownMaximumImpulseNewtonSeconds = 28000f;
        private const float DefaultReactorMeltdownUpwardBias = 0.55f;
        private const float DefaultReactorMeltdownFloodAmplification = 1.35f;
        private const float DefaultThermalFatigueThresholdCelsius = 120f;
        private const float DefaultGlassThermalFatigueMultiplier = 5f;
        private const float DefaultTitaniumThermalFatigueMultiplier = 0.1f;
        private const float CelsiusToKelvinOffset = 273.15f;
        private const float DefaultReferenceTemperatureKelvin = 293.15f;
        private const float DefaultSteamExpansionRatio = 1600f;
        private const float DefaultSteamGenerationRateCubicMetersPerSecondPerCelsius = 0.00045f;
        private const float DefaultSteamCondensationCoefficient = 0.012f;
        private const float DefaultSteamVentReleaseFraction = 0.35f;
        private const float DefaultSteamVentImpulsePerKilopascal = 55f;
        private const float DefaultSteamVentMinimumPressureRatio = 0.92f;
        private const float DefaultExplosivePocketDecayPerSecond = 0.35f;
        private const float DefaultExplosionPocketThreshold = 0.15f;
        private const float DefaultExplosionImpulsePerPocketUnit = 24000f;
        private const float DefaultExplosionMaximumImpulseNewtonSeconds = 120000f;
        private const float DefaultExplosionPressureSpikeKPa = 55f;
        private const int BoilingFaunaContactCapacity = 16;
        private const float Epsilon = 0.0001f;
        private const int RoomStatusToxicShift = 0;
        private const int RoomStatusFreezingShift = 8;
        private const int RoomStatusPressureShift = 16;
        private const int RoomStatusFireShift = 24;
        private const uint PressureScreechRngSeed = 0xA511E9B3u;

        private static readonly Vector4 AtmosphereSootDefaultCenter = new Vector4(0.5f, 0.5f, 0f, 0f);

        private enum RoomStructuralMaterial : byte
        {
            Titanium = 0,
            Glass = 1
        }

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes room definitions from submarine authoring data.
        private struct RoomDefinition
        {
            [Tooltip("Override for gas capacity in cubic meters. Zero uses the linked flood-compartment capacity.")]
            [Min(0f)]
            public float gasCapacityOverrideCubicMeters;

            [Tooltip("Initial O2 fraction inside this room. 0.2095 matches dry sea-level air.")]
            [Range(0f, 1f)]
            public float initialOxygenFraction;

            [Tooltip("Initial CO2 fraction inside this room.")]
            [Range(0f, 1f)]
            public float initialCarbonDioxideFraction;

            [Tooltip("Continuous O2 consumption in reference-gas-volume units per second.")]
            [Min(0f)]
            public float oxygenConsumptionUnitsPerSecond;

            [Tooltip("Continuous CO2 generation in reference-gas-volume units per second.")]
            [Min(0f)]
            public float carbonDioxideGenerationUnitsPerSecond;

            [Tooltip("Passive room heat injected every second in watts.")]
            [Min(0f)]
            public float passiveHeatWatts;

            [Tooltip("Initial dry-room temperature in Celsius.")]
            public float initialTemperatureCelsius;

            [Tooltip("Primary structural material used to scale thermal fatigue once the room overheats.")]
            public RoomStructuralMaterial primaryStructuralMaterial;
        }
#pragma warning restore 0649

        private struct FabricatorHeatEmitter
        {
            public Fabricator Fabricator;
            public int RoomIndex;
        }

        private struct DrillHeatEmitter
        {
            public DeepDrillModule Drill;
            public int RoomIndex;
        }

        private struct ReactorHeatEmitter
        {
            public BioReactor Reactor;
            public int RoomIndex;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct PendingAtmosphereMutation
        {
            [FieldOffset(0)]
            public float OxygenUnits;
            [FieldOffset(4)]
            public float TemperatureDeltaCelsius;
            [FieldOffset(8)]
            public float HydrogenPocketUnits;
            [FieldOffset(12)]
            public float OxygenPocketUnits;
            [FieldOffset(16)]
            public float PressureSpikeKPa;
            [FieldOffset(20)]
            private uint _pad0;
            [FieldOffset(24)]
            private ulong _pad1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveDaltonPressureKPa(
            float oxygenUnits,
            float carbonDioxideUnits,
            float nitrogenUnits,
            float waterVaporVolumeCubicMeters,
            float roomVolumeCubicMeters,
            float gasVolumeCubicMeters,
            float temperatureCelsius,
            float referencePressureKPa,
            float maximumPressureKPa,
            float referenceTemperatureCelsius,
            float oxygenTankCapacity,
            out float oxygenPartialPressureKPa,
            out float carbonDioxidePartialPressureKPa,
            out float nitrogenPartialPressureKPa)
        {
            float tankCapacity = math.max(1f, SanitizeFiniteStatic(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float referencePressure = math.max(1f, SanitizeFiniteStatic(referencePressureKPa, DefaultReferencePressureKPa));
            float maximumPressure = math.max(referencePressure, SanitizeFiniteStatic(maximumPressureKPa, DefaultMaximumPressureKPa));
            float roomVolume = math.max(0.001f, SanitizeFiniteStatic(roomVolumeCubicMeters, 0.001f));
            float gasVolume = math.max(0.001f, SanitizeFiniteStatic(gasVolumeCubicMeters, roomVolume));
            float invGasVolume = math.rcp(gasVolume);
            float invTankCapacity = math.rcp(tankCapacity);
            float compressionScale = math.max(1f, roomVolume * invGasVolume);
            float referenceKelvin = math.max(1f, SanitizeFiniteStatic(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius) + CelsiusToKelvinOffset);
            float temperatureKelvin = math.max(1f, SanitizeFiniteStatic(temperatureCelsius, referenceTemperatureCelsius) + CelsiusToKelvinOffset);
            float pressureScale = referencePressure * compressionScale * (temperatureKelvin * math.rcp(referenceKelvin));

            float oxygenFraction = math.saturate(SanitizeNonNegativeStatic(oxygenUnits) * invTankCapacity) * DefaultInitialOxygenFraction;
            float carbonDioxideFraction = math.saturate(SanitizeNonNegativeStatic(carbonDioxideUnits) * invTankCapacity);
            float nitrogenFraction = math.saturate(SanitizeNonNegativeStatic(nitrogenUnits) * invTankCapacity);
            float waterVaporFraction = math.saturate(SanitizeNonNegativeStatic(waterVaporVolumeCubicMeters) * invGasVolume);

            oxygenPartialPressureKPa = pressureScale * oxygenFraction;
            carbonDioxidePartialPressureKPa = pressureScale * carbonDioxideFraction;
            nitrogenPartialPressureKPa = pressureScale * nitrogenFraction;
            float waterVaporPartialPressureKPa = pressureScale * waterVaporFraction;
            float rawPressure = oxygenPartialPressureKPa + carbonDioxidePartialPressureKPa + nitrogenPartialPressureKPa + waterVaporPartialPressureKPa;
            float pressure = math.min(maximumPressure, rawPressure);
            float capScale = rawPressure > Epsilon ? pressure * math.rcp(rawPressure) : 0f;
            oxygenPartialPressureKPa *= capScale;
            carbonDioxidePartialPressureKPa *= capScale;
            nitrogenPartialPressureKPa *= capScale;
            return pressure;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFiniteStatic(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegativeStatic(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveCarbonDioxideToxicity01(float carbonDioxidePressureFraction)
        {
            return math.saturate(
                (SanitizeNonNegativeStatic(carbonDioxidePressureFraction) - DefaultCarbonDioxideToxicityFraction) *
                DefaultInvCarbonDioxideToxicitySpan);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AtmosphereStepJob : IJob
        {
            [NoAlias, ReadOnly] public NativeArray<float> O2Front;
            [NoAlias, ReadOnly] public NativeArray<float> CO2Front;
            [NoAlias, ReadOnly] public NativeArray<float> InertFront;
            [NoAlias, ReadOnly] public NativeArray<float> FloodVolumes;
            [NoAlias, ReadOnly] public NativeArray<float> RoomVolumes;
            [NoAlias, ReadOnly] public NativeArray<float> PressureFront;
            [NoAlias, ReadOnly] public NativeArray<float> GasVolumeFront;
            [NoAlias, ReadOnly] public NativeArray<float> O2ConsumptionRates;
            [NoAlias, ReadOnly] public NativeArray<float> CO2GenerationRates;
            [NoAlias, ReadOnly] public NativeArray<int> RoomPlayerCounts;
            [NoAlias, ReadOnly] public NativeArray<float> TemperatureFront;
            [NoAlias, ReadOnly] public NativeArray<float> RoomHeatWatts;
            [NoAlias, ReadOnly] public NativeArray<float> SteamFront;
            [NoAlias, ReadOnly] public NativeArray<int2> DoorPairs;
            [NoAlias, ReadOnly] public NativeArray<byte> DoorSealed;

            [NoAlias] public NativeArray<float> O2Back;
            [NoAlias] public NativeArray<float> CO2Back;
            [NoAlias] public NativeArray<float> InertBack;
            [NoAlias] public NativeArray<float> PressureBack;
            [NoAlias] public NativeArray<float> GasVolumeBack;
            [NoAlias] public NativeArray<float> TemperatureBack;
            [NoAlias] public NativeArray<float> SteamBack;
            [NoAlias, WriteOnly] public NativeArray<float> O2PartialPressureBack;
            [NoAlias, WriteOnly] public NativeArray<float> CO2PartialPressureBack;
            [NoAlias, WriteOnly] public NativeArray<float> N2PartialPressureBack;
            [NoAlias, WriteOnly] public NativeArray<uint> RoomStatusMaskBack;

            public int RoomCount;
            public int DoorCount;
            public float DeltaTime;
            public float ReferencePressureKPa;
            public float MinimumGasVolumeCubicMeters;
            public float MaximumPressureKPa;
            public float ReferenceTemperatureCelsius;
            public float FloodWaterTemperatureCelsius;
            public float MinimumTemperatureCelsius;
            public float MaximumTemperatureCelsius;
            public float OxygenTankCapacity;
            public float HeatWattsToCelsiusPerSecond;
            public float LowOxygenThresholdUnits;
            public float CarbonDioxideToxicityFraction;
            public float FreezingTemperatureCelsius;
            public float HighPressureStatusKPa;

            public void Execute()
            {
                float deltaTime = SanitizeNonNegative(DeltaTime);
                float tankCapacity = math.max(1f, SanitizeFinite(OxygenTankCapacity, DefaultOxygenTankCapacity));
                float referencePressure = math.max(1f, SanitizeFinite(ReferencePressureKPa, DefaultReferencePressureKPa));
                float maximumPressure = math.max(referencePressure, SanitizeFinite(MaximumPressureKPa, DefaultMaximumPressureKPa));
                float minimumGasVolume = math.max(0.001f, SanitizeFinite(MinimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
                float minimumTemperature = math.min(
                    SanitizeFinite(MinimumTemperatureCelsius, DefaultMinimumTemperatureCelsius),
                    SanitizeFinite(MaximumTemperatureCelsius, DefaultMaximumTemperatureCelsius));
                float maximumTemperature = math.max(
                    SanitizeFinite(MinimumTemperatureCelsius, DefaultMinimumTemperatureCelsius),
                    SanitizeFinite(MaximumTemperatureCelsius, DefaultMaximumTemperatureCelsius));
                float referenceTemperature = SanitizeRange(
                    ReferenceTemperatureCelsius,
                    DefaultReferenceTemperatureCelsius,
                    minimumTemperature,
                    maximumTemperature);
                float floodWaterTemperature = SanitizeRange(
                    FloodWaterTemperatureCelsius,
                    DefaultFloodWaterTemperatureCelsius,
                    minimumTemperature,
                    maximumTemperature);
                for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                {
                    bool activeRoom = roomIndex < RoomCount;
                    float roomVolume = math.max(SanitizeFinite(RoomVolumes[roomIndex], minimumGasVolume), minimumGasVolume);
                    float floodVolume = math.clamp(SanitizeNonNegative(FloodVolumes[roomIndex]), 0f, roomVolume - Epsilon);
                    float gasVolume = math.max(minimumGasVolume, roomVolume - floodVolume);
                    int playerCount = math.clamp(RoomPlayerCounts[roomIndex], 0, MaxFakeAtmospherePlayerCountPerRoom);

                    float oxygenDrain = SanitizeNonNegative(O2ConsumptionRates[roomIndex]) * playerCount * deltaTime;
                    float oxygen = math.clamp(SanitizeFinite(O2Front[roomIndex], tankCapacity) - oxygenDrain, 0f, tankCapacity);
                    float carbonDioxide = math.clamp(
                        SanitizeNonNegative(CO2Front[roomIndex]) + Hecton8.PureLogic.Systems.Co2ScrubberLoadCalculator.Compute(
                            playerCount,
                            0f,
                            0f,
                            SanitizeNonNegative(CO2GenerationRates[roomIndex])
                        ) * deltaTime,
                        0f,
                        tankCapacity);
                    float inert = math.clamp(SanitizeNonNegative(InertFront[roomIndex]), 0f, tankCapacity);
                    float steam = SanitizeNonNegative(SteamFront[roomIndex]);

                    O2Back[roomIndex] = math.select(0f, oxygen, activeRoom);
                    CO2Back[roomIndex] = math.select(0f, carbonDioxide, activeRoom);
                    InertBack[roomIndex] = math.select(0f, inert, activeRoom);
                    SteamBack[roomIndex] = math.select(0f, steam, activeRoom);
                    GasVolumeBack[roomIndex] = math.select(minimumGasVolume, gasVolume, activeRoom);

                    float previousTemperature = SanitizeRange(TemperatureFront[roomIndex], referenceTemperature, minimumTemperature, maximumTemperature);
                    float floodFill01 = math.saturate(floodVolume / math.max(roomVolume, Epsilon));
                    float floodBlend = math.saturate(floodFill01 * deltaTime * 0.1f);
                    float mixedTemperature = math.lerp(previousTemperature, floodWaterTemperature, floodBlend);
                    float temperatureDelta = Hecton8.PureLogic.Systems.ModuleThermalDissipationRate.Calculate(mixedTemperature, SanitizeFinite(RoomHeatWatts[roomIndex], 0f), 0f, roomVolume, deltaTime, 1.2f, 1005f, 0.001f);
                    float roomTemperature = math.clamp(
                        mixedTemperature + temperatureDelta,
                        minimumTemperature,
                        maximumTemperature);
                    TemperatureBack[roomIndex] = math.select(referenceTemperature, roomTemperature, activeRoom);
                    PressureBack[roomIndex] = referencePressure;
                    O2PartialPressureBack[roomIndex] = 0f;
                    CO2PartialPressureBack[roomIndex] = 0f;
                    N2PartialPressureBack[roomIndex] = 0f;
                }

                for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
                {
                    int2 pair = DoorPairs[doorIndex];
                    bool openDoor = doorIndex < DoorCount & DoorSealed[doorIndex] == 0;
                    bool validPair = openDoor & pair.x >= 0 & pair.x < RoomCount & pair.y >= 0 & pair.y < RoomCount;
                    int roomA = math.clamp(pair.x, 0, RoomCapacity - 1);
                    int roomB = math.clamp(pair.y, 0, RoomCapacity - 1);
                    float blend = math.select(0f, 1f, validPair);

                    // HECTON-8 MEGA-UPDATE: Replace simplistic lerp with pure Fick's law mathematical kernel
                    // Note: We use a placeholder area of 2.0f until the swarm tasks complete the engine scaffolding refactor
                    System.Numerics.Vector2 transfer = Hecton8.PureLogic.Systems.AtmosphericRoomGasDiffusionCalculator.Compute(
                        O2Back[roomA],
                        O2Back[roomB],
                        CO2Back[roomA],
                        CO2Back[roomB],
                        2.0f,
                        DeltaTime,
                        DefaultDoorConductance,
                        0.5f);

                    float transferO2 = transfer.X * blend;
                    float transferCO2 = transfer.Y * blend;

                    O2Back[roomA] -= transferO2;
                    O2Back[roomB] += transferO2;
                    CO2Back[roomA] -= transferCO2;
                    CO2Back[roomB] += transferCO2;

                    float averageInert = (InertBack[roomA] + InertBack[roomB]) * 0.5f;
                    float averageSteam = (SteamBack[roomA] + SteamBack[roomB]) * 0.5f;
                    float averageHeat = (TemperatureBack[roomA] + TemperatureBack[roomB]) * 0.5f;
                    InertBack[roomA] = math.lerp(InertBack[roomA], averageInert, blend);
                    InertBack[roomB] = math.lerp(InertBack[roomB], averageInert, blend);
                    SteamBack[roomA] = math.lerp(SteamBack[roomA], averageSteam, blend);
                    SteamBack[roomB] = math.lerp(SteamBack[roomB], averageSteam, blend);
                    TemperatureBack[roomA] = math.lerp(TemperatureBack[roomA], averageHeat, blend);
                    TemperatureBack[roomB] = math.lerp(TemperatureBack[roomB], averageHeat, blend);
                }

                uint statusMask = 0u;
                float lowOxygenThreshold = SanitizeNonNegative(LowOxygenThresholdUnits);
                float carbonDioxideToxicityFraction = math.max(
                    0.0001f,
                    SanitizeFinite(CarbonDioxideToxicityFraction, DefaultCarbonDioxideToxicityFraction));
                float pressureThreshold = math.max(referencePressure, SanitizeFinite(HighPressureStatusKPa, referencePressure));
                for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                {
                    bool activeRoom = roomIndex < RoomCount;
                    float roomVolume = math.max(SanitizeFinite(RoomVolumes[roomIndex], minimumGasVolume), minimumGasVolume);
                    float gasVolume = math.max(minimumGasVolume, SanitizeFinite(GasVolumeBack[roomIndex], minimumGasVolume));
                    float pressure = ResolveDaltonPressureKPa(
                        O2Back[roomIndex],
                        CO2Back[roomIndex],
                        InertBack[roomIndex],
                        SteamBack[roomIndex],
                        roomVolume,
                        gasVolume,
                        TemperatureBack[roomIndex],
                        referencePressure,
                        maximumPressure,
                        referenceTemperature,
                        tankCapacity,
                        out float oxygenPartialPressureKPa,
                        out float carbonDioxidePartialPressureKPa,
                        out float nitrogenPartialPressureKPa);
                    PressureBack[roomIndex] = pressure;
                    O2PartialPressureBack[roomIndex] = oxygenPartialPressureKPa;
                    CO2PartialPressureBack[roomIndex] = carbonDioxidePartialPressureKPa;
                    N2PartialPressureBack[roomIndex] = nitrogenPartialPressureKPa;

                    uint roomBit = 1u << roomIndex;
                    float carbonDioxidePressureFraction = math.select(
                        0f,
                        carbonDioxidePartialPressureKPa * math.rcp(math.max(pressure, Epsilon)),
                        pressure > Epsilon);
                    bool toxicRoom = activeRoom & (O2Back[roomIndex] < lowOxygenThreshold | carbonDioxidePressureFraction >= carbonDioxideToxicityFraction);
                    bool freezingRoom = activeRoom & TemperatureBack[roomIndex] <= FreezingTemperatureCelsius;
                    bool pressureRoom = activeRoom & PressureBack[roomIndex] >= pressureThreshold;
                    statusMask |= math.select(0u, roomBit << RoomStatusToxicShift, toxicRoom);
                    statusMask |= math.select(0u, roomBit << RoomStatusFreezingShift, freezingRoom);
                    statusMask |= math.select(0u, roomBit << RoomStatusPressureShift, pressureRoom);
                }

                RoomStatusMaskBack[0] = statusMask;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeFinite(float value, float fallback)
            {
                return math.select(fallback, value, math.isfinite(value));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeNonNegative(float value)
            {
                return math.select(0f, math.max(0f, value), math.isfinite(value));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeRange(float value, float fallback, float minimum, float maximum)
            {
                return math.clamp(math.select(fallback, value, math.isfinite(value)), minimum, maximum);
            }
        }

        [Header("-- References ------------------")]
        [Tooltip("Flood-compartment owner that provides room capacities, flood displacement, and sealed-door topology.")]
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;

        [Header("-- Atmosphere Rooms ------------------")]
        [Tooltip("Per-room initial fractions and metabolic sources. Entries map 1:1 to the submarine fluid compartments.")]
        [SerializeField] private RoomDefinition[] rooms = new RoomDefinition[RoomCapacity];

        [Header("-- Gas Solver ------------------")]
        [Tooltip("Reference pressure used when a room is dry and filled with its authored gas volume.")]
        [SerializeField, Min(1f)] private float referencePressureKPa = DefaultReferencePressureKPa;

        [Tooltip("Legacy pressure setting retained for authored data compatibility. Cheap solver uses one-pass hatch averaging.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float doorConductance = DefaultDoorConductance;
#pragma warning restore CS0414

        [Tooltip("Legacy transfer cap retained for authored data compatibility. Cheap solver does not iterate gas transfer.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float maxTransferUnitsPerSecond = DefaultMaxTransferUnitsPerSecond;
#pragma warning restore CS0414

        [Tooltip("Gas volume floor used to prevent divide-by-zero when a room is almost fully flooded.")]
        [SerializeField, Min(0.001f)] private float minimumGasVolumeCubicMeters = DefaultMinimumGasVolumeCubicMeters;

        [Tooltip("Maximum simulated room pressure in kPa.")]
        [SerializeField, Min(10f)] private float maximumPressureKPa = DefaultMaximumPressureKPa;

        [Tooltip("Absolute room pressure threshold required before a high-pressure event is emitted.")]
        [SerializeField, Min(0f)] private float highPressureEventThresholdKPa = DefaultHighPressureEventThresholdKPa;

        [Header("Cheap Atmosphere Fakes")]
        [Tooltip("O2 tank capacity per room. Release contract is 0..100.")]
        [SerializeField, Min(1f)] private float oxygenTankCapacity = DefaultOxygenTankCapacity;

        [Tooltip("Room O2 threshold below which low-O2 audio and toxicity hazard signals arm.")]
        [SerializeField, Range(0f, 1f)] private float lowOxygenThreshold01 = DefaultLowOxygenThreshold01;

        [Tooltip("Fallback room O2 drain in percent per second per local player.")]
        [SerializeField, Min(0f)] private float playerOxygenConsumptionPercentPerSecond = DefaultPlayerOxygenConsumptionPercentPerSecond;

        [Tooltip("Atmosphere job cadence. 1.0 seconds is the 1 Hz ColdTick.")]
        [SerializeField, Min(0.02f)] private float atmosphereSlowTickSeconds = DefaultAtmosphereSlowTickSeconds;

        [Tooltip("Fake heat gain in Celsius per second per watt.")]
        [SerializeField, Min(0f)] private float heatWattsToCelsiusPerSecond = DefaultHeatWattsToCelsiusPerSecond;

        [Tooltip("Room temperature where ambient module lights begin overheat brownout flicker.")]
        [SerializeField] private float overheatBrownoutTemperatureCelsius = DefaultOverheatBrownoutTemperatureCelsius;

        [Tooltip("Minimum voltage ratio pushed to module shaders at full overheat.")]
        [SerializeField, Range(0f, 1f)] private float overheatMinimumVoltage = DefaultOverheatMinimumVoltage;

        [Tooltip("Toxicity hazard intensity published for low-O2 rooms.")]
        [SerializeField, Min(0f)] private float toxicRoomHazardIntensity = DefaultToxicRoomHazardIntensity;

        [Tooltip("Toxicity hazard intensity published for rooms with module fire smoke.")]
        [SerializeField, Min(0f)] private float fireSmokeHazardIntensity = DefaultFireSmokeHazardIntensity;

        [Tooltip("Extra room radius for localized fake atmosphere hazard volumes.")]
        [SerializeField, Min(0f)] private float roomHazardRadiusPaddingMeters = DefaultRoomHazardRadiusPaddingMeters;

        [Tooltip("Extra visor glitch bias while standing inside fake smoke.")]
        [SerializeField, Min(0f)] private float fireSmokeVisorGlitchBias = DefaultFireSmokeVisorGlitchBias;

        [Tooltip("Screen-space soot radius scale applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 0.25f)] private float fireSmokeSootScreenRadiusScale = DefaultFireSmokeSootScreenRadiusScale;

        [Tooltip("Screen-space soot dither strength applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 1f)] private float fireSmokeSootDitherStrength = DefaultFireSmokeSootDitherStrength;

        [Tooltip("Screen-space darkening strength applied by the fake smoke fullscreen pass.")]
        [SerializeField, Range(0f, 1f)] private float fireSmokeSootDarkenStrength = DefaultFireSmokeSootDarkenStrength;

        [Tooltip("Audio log played once per cooldown when the occupied room falls below low-O2 threshold.")]
        [SerializeField] private AudioLogData lowOxygenGaspingAudioLog;

        [Tooltip("Minimum seconds between low-O2 gasping audio log triggers.")]
        [SerializeField, Min(0f)] private float lowOxygenAudioCooldownSeconds = DefaultLowOxygenAudioCooldownSeconds;

        [Tooltip("Temperature where the room status bit becomes Freezing.")]
        [SerializeField] private float freezingRoomTemperatureCelsius = DefaultFreezingRoomTemperatureCelsius;

        [Tooltip("Random metal screech clips used when the room Pressure bit is active.")]
        [SerializeField] private AudioClip[] pressureScreechClips;

        [Tooltip("Minimum seconds between high-pressure hull creak fakes.")]
        [SerializeField, Min(0f)] private float pressureScreechCooldownSeconds = DefaultPressureScreechCooldownSeconds;

        [Tooltip("World-space hull screech volume.")]
        [SerializeField, Range(0f, 1f)] private float pressureScreechVolume = DefaultPressureScreechVolume;

        [Tooltip("Minimum random pitch for high-pressure hull screech fakes.")]
        [SerializeField, Range(0.25f, 2f)] private float pressureScreechPitchMin = DefaultPressureScreechPitchMin;

        [Tooltip("Maximum random pitch for high-pressure hull screech fakes.")]
        [SerializeField, Range(0.25f, 2f)] private float pressureScreechPitchMax = DefaultPressureScreechPitchMax;

        [Tooltip("Minimum seconds between Toxic-room visor chromatic pulses.")]
        [SerializeField, Min(0f)] private float toxicRoomVisorPulseCooldownSeconds = DefaultToxicRoomVisorPulseCooldownSeconds;

        [Header("-- Thermodynamics ------------------")]
        [Tooltip("Reference dry-room temperature in Celsius used when room state is reset.")]
        [SerializeField] private float referenceTemperatureCelsius = DefaultReferenceTemperatureCelsius;

        [Tooltip("Incoming flood-water temperature in Celsius. Flooded rooms blend toward this sink.")]
        [SerializeField] private float floodWaterTemperatureCelsius = DefaultFloodWaterTemperatureCelsius;

        [Tooltip("Minimum simulated room temperature in Celsius.")]
        [SerializeField] private float minimumTemperatureCelsius = DefaultMinimumTemperatureCelsius;

        [Tooltip("Maximum simulated room temperature in Celsius.")]
        [SerializeField] private float maximumTemperatureCelsius = DefaultMaximumTemperatureCelsius;

        [Tooltip("Air density used when converting gas volume into thermal mass.")]
        [SerializeField, Min(0.1f)] private float airDensityKilogramsPerCubicMeter = DefaultAirDensityKilogramsPerCubicMeter;

        [Tooltip("Specific heat of air in J/(kg*K).")]
        [SerializeField, Min(1f)] private float airSpecificHeatJoulesPerKilogramKelvin = DefaultAirSpecificHeatJoulesPerKilogramKelvin;

        [Tooltip("Flood-water density used by the room heat sink.")]
        [SerializeField, Min(1f)] private float waterDensityKilogramsPerCubicMeter = DefaultWaterDensityKilogramsPerCubicMeter;

        [Tooltip("Specific heat of seawater in J/(kg*K).")]
        [SerializeField, Min(1f)] private float waterSpecificHeatJoulesPerKilogramKelvin = DefaultWaterSpecificHeatJoulesPerKilogramKelvin;

        [Tooltip("Thermal-capacity floor used to stabilize nearly empty rooms.")]
        [SerializeField, Min(1f)] private float minimumThermalCapacityJoulesPerKelvin = DefaultMinimumThermalCapacityJoulesPerKelvin;

        [Tooltip("Bulkhead thermal conductivity used by the room-to-room Fourier conduction pass in W/K.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float bulkheadThermalConductivityWattsPerKelvin = DefaultBulkheadThermalConductivityWattsPerKelvin;
#pragma warning restore CS0414

        [Tooltip("Fraction of bulkhead conductivity applied while the connecting door is sealed.")]
#pragma warning disable CS0414
        [SerializeField, Range(0f, 1f)] private float sealedBulkheadThermalCoupling = DefaultSealedBulkheadThermalCoupling;
#pragma warning restore CS0414

        [Tooltip("Multiplier applied to bulkhead conductivity while the connecting door is open.")]
#pragma warning disable CS0414
        [SerializeField, Min(0f)] private float openBulkheadThermalCoupling = DefaultOpenBulkheadThermalCoupling;
#pragma warning restore CS0414

        [Header("Steam Phase Change")]
        [Tooltip("Legacy temperature reference retained for authored data compatibility.")]
#pragma warning disable CS0414
        [SerializeField, Min(1f)] private float referenceTemperatureKelvin = DefaultReferenceTemperatureKelvin;
#pragma warning restore CS0414

        [Tooltip("Expansion ratio applied when liquid seawater flashes into steam inside a flooded overheated room.")]
        [SerializeField, Min(1f)] private float steamExpansionRatio = DefaultSteamExpansionRatio;
        [Tooltip("Liquid-water vaporization rate in cubic meters per second for each Celsius above the boiling threshold.")]
        [SerializeField, Min(0f)] private float steamGenerationRateCubicMetersPerSecondPerCelsius = DefaultSteamGenerationRateCubicMetersPerSecondPerCelsius;
        [Tooltip("Condensation coefficient used when steam meets a colder hull boundary. Units: equivalent steam volume per second per Celsius.")]
        [SerializeField, Min(0f)] private float steamCondensationCoefficient = DefaultSteamCondensationCoefficient;
        [Tooltip("Fraction of room gas and steam dumped during one emergency vent burst.")]
        [SerializeField, Range(0.05f, 1f)] private float steamVentReleaseFraction = DefaultSteamVentReleaseFraction;
        [Tooltip("Pressure-ratio threshold relative to the configured pressure cap that arms emergency venting.")]
        [SerializeField, Range(0.1f, 1f)] private float steamVentMinimumPressureRatio = DefaultSteamVentMinimumPressureRatio;
        [Tooltip("Impulse scale applied to the submarine when an overpressured room vents to the abyss.")]
        [SerializeField, Min(0f)] private float steamVentImpulsePerKilopascal = DefaultSteamVentImpulsePerKilopascal;

        [Tooltip("Waste-heat multiplier applied to fabricator electrical draw.")]
        [SerializeField, Min(0f)] private float fabricatorHeatWattsScale = DefaultFabricatorHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to deep-drill electrical draw.")]
        [SerializeField, Min(0f)] private float drillHeatWattsScale = DefaultDrillHeatWattsScale;

        [Tooltip("Waste-heat multiplier applied to reactor electrical output.")]
        [SerializeField, Min(0f)] private float reactorHeatWattsScale = DefaultReactorHeatWattsScale;

        [Header("-- Abyssal Freeze ------------------")]
        [Tooltip("Depth threshold where catastrophic blackout cooling starts forcing flooded rooms toward freezing.")]
        [SerializeField, Min(0f)] private float deepFreezeDepthThresholdMeters = DefaultDeepFreezeDepthThresholdMeters;

        [Tooltip("Below this supply ratio, flooded rooms begin exponential blackout cooling.")]
        [SerializeField, Range(0f, 1f)] private float deepFreezeSupplyRatioThreshold = DefaultDeepFreezeSupplyRatioThreshold;

        [Tooltip("Time constant used by the blackout cooling curve.")]
        [SerializeField, Min(0.1f)] private float deepFreezeTauSeconds = DefaultDeepFreezeTauSeconds;

        [Tooltip("Target flooded-room temperature reached under abyssal blackout conditions.")]
        [SerializeField] private float deepFreezeTargetTemperatureCelsius = DefaultDeepFreezeTargetTemperatureCelsius;

        [Header("Brownout Life Support")]
        [Tooltip("Below this module supply ratio, occupied base rooms stop generation and become O2 sinks.")]
        [SerializeField, Range(0f, 1f)] private float brownoutOxygenSupplyRatioThreshold = DefaultBrownoutOxygenSupplyRatioThreshold;

        [Tooltip("Slow occupied-room O2 drain applied while the connected base module is browned out.")]
        [SerializeField, Min(0f)] private float brownoutOccupiedRoomOxygenConsumptionUnitsPerSecond = DefaultBrownoutOccupiedRoomOxygenConsumptionUnitsPerSecond;

        [Header("-- Boiling Flood Hazard ------------------")]
        [Tooltip("Flooded rooms at or above this temperature register a heat hazard in the surrounding water.")]
        [SerializeField] private float boilingFloodTemperatureCelsius = DefaultBoilingFloodTemperatureCelsius;

        [Tooltip("Minimum flooded fill ratio required before boiling-water hazards become active.")]
        [SerializeField, Range(0f, 1f)] private float boilingFloodMinimumFillRatio = DefaultBoilingFloodMinimumFillRatio;

        [Tooltip("Base heat-hazard intensity registered for boiling flooded rooms.")]
        [SerializeField, Min(0f)] private float boilingHazardIntensity = DefaultBoilingHazardIntensity;

        [Tooltip("Extra radius added to the compartment-derived boiling hazard bounds.")]
        [SerializeField, Min(0f)] private float boilingHazardRadiusPaddingMeters = DefaultBoilingHazardRadiusPaddingMeters;

        [Tooltip("Per-second thermal damage applied to nearby fauna caught in boiling flooded rooms.")]
        [SerializeField, Min(0f)] private float boilingFaunaDamagePerSecond = DefaultBoilingFaunaDamagePerSecond;

        [Header("-- Reactor Meltdown ------------------")]
        [Tooltip("Room temperature threshold in Celsius that triggers a reactor meltdown impulse.")]
        [SerializeField] private float reactorMeltdownTemperatureCelsius = DefaultReactorMeltdownTemperatureCelsius;

        [Tooltip("Seconds used to convert reactor thermal force into a one-shot impulse.")]
        [SerializeField, Min(0.001f)] private float reactorMeltdownImpulseDurationSeconds = DefaultReactorMeltdownImpulseDurationSeconds;

        [Tooltip("Impulse scale in newton-seconds per watt of reactor output.")]
        [SerializeField, Min(0f)] private float reactorMeltdownImpulsePerWattSecond = DefaultReactorMeltdownImpulsePerWattSecond;

        [Tooltip("Minimum reactor meltdown impulse in newton-seconds.")]
        [SerializeField, Min(1f)] private float reactorMeltdownMinimumImpulseNewtonSeconds = DefaultReactorMeltdownMinimumImpulseNewtonSeconds;

        [Tooltip("Maximum reactor meltdown impulse in newton-seconds.")]
        [SerializeField, Min(1f)] private float reactorMeltdownMaximumImpulseNewtonSeconds = DefaultReactorMeltdownMaximumImpulseNewtonSeconds;

        [Tooltip("How much world-up is mixed into the reactor blowout direction.")]
        [SerializeField, Range(0f, 1f)] private float reactorMeltdownUpwardBias = DefaultReactorMeltdownUpwardBias;

        [Tooltip("Extra impulse multiplier applied when the reactor room is flooded.")]
        [SerializeField, Min(1f)] private float reactorMeltdownFloodAmplification = DefaultReactorMeltdownFloodAmplification;
        [Header("Thermal Material Stress")]
        [Tooltip("Room temperature threshold in Celsius where material-specific structural-fatigue scaling begins.")]
        [SerializeField] private float thermalFatigueThresholdCelsius = DefaultThermalFatigueThresholdCelsius;
        [Tooltip("Structural fatigue multiplier applied to glass rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float glassThermalFatigueMultiplier = DefaultGlassThermalFatigueMultiplier;
        [Tooltip("Structural fatigue multiplier applied to titanium rooms above the thermal threshold.")]
        [SerializeField, Min(0f)] private float titaniumThermalFatigueMultiplier = DefaultTitaniumThermalFatigueMultiplier;

        [Header("-- Explosive Electrolysis ------------------")]
        [Tooltip("Per-second decay applied to explosive electrolysis gas pockets.")]
        [SerializeField, Min(0f)] private float explosivePocketDecayPerSecond = DefaultExplosivePocketDecayPerSecond;

        [Tooltip("Minimum combined hydrogen/oxygen pocket intensity required before a spark detonates the compartment.")]
        [SerializeField, Range(0f, 1f)] private float explosivePocketThreshold = DefaultExplosionPocketThreshold;

        [Tooltip("Impulse scale applied to the submarine rigidbody when an explosive pocket detonates.")]
        [SerializeField, Min(0f)] private float explosionImpulsePerPocketUnit = DefaultExplosionImpulsePerPocketUnit;

        [Tooltip("Safety cap on one electrolysis explosion impulse.")]
        [SerializeField, Min(1f)] private float explosionMaximumImpulseNewtonSeconds = DefaultExplosionMaximumImpulseNewtonSeconds;

        [Tooltip("Pressure spike injected into the room when a flooded overloaded node detonates violently.")]
        [FormerlySerializedAs("electrolysisPressureSpikeKPa")]
        [SerializeField, Min(0f)] private float explosionPressureSpikeKPa = DefaultExplosionPressureSpikeKPa;

        [Header("-- Pressure Blowout ------------------")]
        [Tooltip("Radius around an opened bulkhead that receives the pressure blowout impulse.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseRadiusMeters = DefaultPressureImpulseRadiusMeters;

        [Tooltip("Impulse duration used to convert raw pressure force into a one-shot rigidbody impulse.")]
        [SerializeField, Min(0.001f)] private float pressureImpulseDurationSeconds = DefaultPressureImpulseDurationSeconds;

        [Tooltip("Cheap squared-distance falloff bias applied to bodies near the bulkhead opening. Kept serialized under the legacy field name.")]
        [SerializeField, Min(0.25f)] private float pressureImpulseFalloffExponent = DefaultPressureImpulseFalloffExponent;

        [Tooltip("Safety cap on one blowout impulse magnitude in newton-seconds.")]
        [SerializeField, Min(1f)] private float maximumPressureImpulseNewtonSeconds = DefaultMaximumPressureImpulseNewtonSeconds;

        [Tooltip("Rigidbodies on these layers receive the blowout impulse.")]
        [SerializeField] private LayerMask pressureImpulseLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("-- Diagnostics ------------------")]
        [SerializeField] private int _debugRoomCount;
        [SerializeField] private int _debugDoorCount;
        [SerializeField] private float _debugAveragePressureKPa;
        [SerializeField] private float _debugMaxPressureKPa;
        [SerializeField] private float _debugAverageOxygenFraction;
        [SerializeField] private float _debugAverageCarbonDioxideFraction;
        [SerializeField] private float _debugAverageTemperatureCelsius;
        [SerializeField] private float _debugMaxTemperatureCelsius;
        [SerializeField] private float _debugAverageSteamVolumeCubicMeters;
        [SerializeField] private float _debugMaxSteamVolumeCubicMeters;

        private Transform _cachedTransform;
        private Transform _playerTransform;
        private Camera _playerCamera;
        private Rigidbody _submarineBody;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPowerGridService _powerGridService;
        private AudioLogSystem _audioLogs;
        private IPlayerSensoryService _playerSensoryService;
        private IAudioService _audioService;
        private IPhysicsService _physicsService;
        private bool _coldTickRegistered;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _topologySeeded;
        private bool _thermalEmittersSeeded;
        private bool _emergencyVentPipesSeeded;
        private int _topologyRoomCount = -1;
        private int _topologyDoorCount = -1;
        private JobHandle _atmosphereJobHandle;
        private JobHandle _disposeHandle;
        private bool _atmosphereJobRunning;
        private float _scheduledAtmosphereDeltaTime;
        private ulong _atmosphereJobLockMask;
        private IDataVault _atmosphereJobMutationGuardVault;
        private ulong _atmospherePhaseMutationGuardMask;
        private IDataVault _atmospherePhaseMutationGuardVault;
        private IDataVault _dataVault;

        private VaultGenerationHandle<float> _roomVolumesHandle;
        private VaultGenerationHandle<float> _floodVolumesHandle;
        private VaultGenerationHandle<float> _o2FrontHandle;
        private VaultGenerationHandle<float> _o2BackHandle;
        private VaultGenerationHandle<float> _co2FrontHandle;
        private VaultGenerationHandle<float> _co2BackHandle;
        private VaultGenerationHandle<float> _inertFrontHandle;
        private VaultGenerationHandle<float> _inertBackHandle;
        private VaultGenerationHandle<float> _pressureFrontHandle;
        private VaultGenerationHandle<float> _pressureBackHandle;
        private VaultGenerationHandle<float> _o2PartialPressureFrontHandle;
        private VaultGenerationHandle<float> _o2PartialPressureBackHandle;
        private VaultGenerationHandle<float> _co2PartialPressureFrontHandle;
        private VaultGenerationHandle<float> _co2PartialPressureBackHandle;
        private VaultGenerationHandle<float> _n2PartialPressureFrontHandle;
        private VaultGenerationHandle<float> _n2PartialPressureBackHandle;
        private VaultGenerationHandle<float> _gasVolumeFrontHandle;
        private VaultGenerationHandle<float> _gasVolumeBackHandle;
        private VaultGenerationHandle<float> _o2ConsumptionRatesHandle;
        private VaultGenerationHandle<float> _co2GenerationRatesHandle;
        private VaultGenerationHandle<int> _roomPlayerCountsHandle;
        private VaultGenerationHandle<float> _temperatureFrontHandle;
        private VaultGenerationHandle<float> _temperatureBackHandle;
        private VaultGenerationHandle<float> _steamFrontHandle;
        private VaultGenerationHandle<float> _steamBackHandle;
        private VaultGenerationHandle<float> _hydrogenPocketFrontHandle;
        private VaultGenerationHandle<float> _oxygenPocketFrontHandle;
        private VaultGenerationHandle<float> _roomHeatWattsHandle;
        private VaultGenerationHandle<uint> _roomStatusMaskFrontHandle;
        private VaultGenerationHandle<uint> _roomStatusMaskBackHandle;
        private VaultGenerationHandle<int2> _doorPairsHandle;
        private VaultGenerationHandle<byte> _doorSealedHandle;
        private VaultGenerationHandle<byte> _doorSealedPreviousHandle;
        private VaultGenerationHandle<SubmarineAtmosphereTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;

        private NativeArray<float> _roomVolumes => ResolveVaultArray(in _roomVolumesHandle);
        private NativeArray<float> _floodVolumes => ResolveVaultArray(in _floodVolumesHandle);
        private NativeArray<float> _o2Front => ResolveVaultArray(in _o2FrontHandle);
        private NativeArray<float> _o2Back => ResolveVaultArray(in _o2BackHandle);
        private NativeArray<float> _co2Front => ResolveVaultArray(in _co2FrontHandle);
        private NativeArray<float> _co2Back => ResolveVaultArray(in _co2BackHandle);
        private NativeArray<float> _inertFront => ResolveVaultArray(in _inertFrontHandle);
        private NativeArray<float> _inertBack => ResolveVaultArray(in _inertBackHandle);
        private NativeArray<float> _pressureFront => ResolveVaultArray(in _pressureFrontHandle);
        private NativeArray<float> _pressureBack => ResolveVaultArray(in _pressureBackHandle);
        private NativeArray<float> _o2PartialPressureFront => ResolveVaultArray(in _o2PartialPressureFrontHandle);
        private NativeArray<float> _o2PartialPressureBack => ResolveVaultArray(in _o2PartialPressureBackHandle);
        private NativeArray<float> _co2PartialPressureFront => ResolveVaultArray(in _co2PartialPressureFrontHandle);
        private NativeArray<float> _co2PartialPressureBack => ResolveVaultArray(in _co2PartialPressureBackHandle);
        private NativeArray<float> _n2PartialPressureFront => ResolveVaultArray(in _n2PartialPressureFrontHandle);
        private NativeArray<float> _n2PartialPressureBack => ResolveVaultArray(in _n2PartialPressureBackHandle);
        private NativeArray<float> _gasVolumeFront => ResolveVaultArray(in _gasVolumeFrontHandle);
        private NativeArray<float> _gasVolumeBack => ResolveVaultArray(in _gasVolumeBackHandle);
        private NativeArray<float> _o2ConsumptionRates => ResolveVaultArray(in _o2ConsumptionRatesHandle);
        private NativeArray<float> _co2GenerationRates => ResolveVaultArray(in _co2GenerationRatesHandle);
        private NativeArray<int> _roomPlayerCounts => ResolveVaultArray(in _roomPlayerCountsHandle);
        private NativeArray<float> _temperatureFront => ResolveVaultArray(in _temperatureFrontHandle);
        private NativeArray<float> _temperatureBack => ResolveVaultArray(in _temperatureBackHandle);
        private NativeArray<float> _steamFront => ResolveVaultArray(in _steamFrontHandle);
        private NativeArray<float> _steamBack => ResolveVaultArray(in _steamBackHandle);
        private NativeArray<float> _hydrogenPocketFront => ResolveVaultArray(in _hydrogenPocketFrontHandle);
        private NativeArray<float> _oxygenPocketFront => ResolveVaultArray(in _oxygenPocketFrontHandle);
        private NativeArray<float> _roomHeatWatts => ResolveVaultArray(in _roomHeatWattsHandle);
        private NativeArray<uint> _roomStatusMaskFront => ResolveVaultArray(in _roomStatusMaskFrontHandle);
        private NativeArray<uint> _roomStatusMaskBack => ResolveVaultArray(in _roomStatusMaskBackHandle);
        private NativeArray<int2> _doorPairs => ResolveVaultArray(in _doorPairsHandle);
        private NativeArray<byte> _doorSealed => ResolveVaultArray(in _doorSealedHandle);
        private NativeArray<byte> _doorSealedPrevious => ResolveVaultArray(in _doorSealedPreviousHandle);
        // COLD ALLOC: SpatialQueryHit[32] - registered bulkhead blowout contact scratch - owner: SubmarineAtmosphereSystem
        private readonly SpatialQueryHit[] _pressureImpulseContacts = new SpatialQueryHit[PressureImpulseOverlapCapacity];
        // COLD ALLOC: Rigidbody[32] - unique-body scratch for pressure blowout dispatch - owner: SubmarineAtmosphereSystem
        private readonly Rigidbody[] _pressureImpulseBodyBuffer = new Rigidbody[PressureImpulseOverlapCapacity];
        // COLD ALLOC: float[32] - precomputed pressure blowout falloff per unique body - owner: SubmarineAtmosphereSystem
        private readonly float[] _pressureImpulseFalloffBuffer = new float[PressureImpulseOverlapCapacity];
        // COLD ALLOC: int[8] - per-room boiling hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _boilingHazardIds = new int[RoomCapacity];
        private uint _boilingHazardActiveMask;
        private IThermodynamicsService _thermodynamicsService;
        // COLD ALLOC: int[8] - per-room low-O2 toxicity hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _toxicRoomHazardIds = new int[RoomCapacity];
        // COLD ALLOC: int[8] - per-room fake smoke hazard source IDs - owner: SubmarineAtmosphereSystem
        private readonly int[] _fireSmokeHazardIds = new int[RoomCapacity];
        private uint _toxicRoomHazardActiveMask;
        private uint _fireSmokeHazardActiveMask;
        // COLD ALLOC: BaseModule[8] - cached room-to-base brownout links - owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _brownoutRoomModules = new BaseModule[RoomCapacity];
        // COLD ALLOC: BaseModule[8] - cached room-to-base module links for visual atmosphere fakes - owner: SubmarineAtmosphereSystem
        private readonly BaseModule[] _atmosphereRoomModules = new BaseModule[RoomCapacity];
        private uint _overheatVisualActiveMask;
        // COLD ALLOC: SpatialQueryHit[16] - fauna spillover query scratch for boiling rooms - owner: SubmarineAtmosphereSystem
        private readonly SpatialQueryHit[] _boilingFaunaContacts = new SpatialQueryHit[BoilingFaunaContactCapacity];
        // COLD ALLOC: FabricatorHeatEmitter[24] - cached fabricator heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly FabricatorHeatEmitter[] _fabricatorHeatEmitters = new FabricatorHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: DrillHeatEmitter[24] - cached drill heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly DrillHeatEmitter[] _drillHeatEmitters = new DrillHeatEmitter[HeatEmitterCapacity];
        // COLD ALLOC: ReactorHeatEmitter[24] - cached reactor heat sources mapped to rooms - owner: SubmarineAtmosphereSystem
        private readonly ReactorHeatEmitter[] _reactorHeatEmitters = new ReactorHeatEmitter[HeatEmitterCapacity];
        private uint _reactorMeltdownTriggeredMask;
        // COLD ALLOC: PendingAtmosphereMutation[8] - deferred authoritative room writes while Burst atmosphere job owns BackBuffer - owner: SubmarineAtmosphereSystem
        private readonly PendingAtmosphereMutation[] _pendingAtmosphereMutations = new PendingAtmosphereMutation[RoomCapacity];
        // COLD ALLOC: LogisticsPipeNode[8] - room-indexed emergency vent cache, avoids component scans in pressure path - owner: SubmarineAtmosphereSystem
        private readonly LogisticsPipeNode[] _emergencyVentPipesByRoom = new LogisticsPipeNode[RoomCapacity];
        private uint _emergencyVentRoomMask;
        private int _fabricatorHeatEmitterCount;
        private int _drillHeatEmitterCount;
        private int _reactorHeatEmitterCount;
        private int _droppedSignalCount;
        private int _telemetryWriteIndex;
        private uint _atmosphereTickCount;
        private bool _blackBoxDumped;
        private float _atmosphereStepAccumulator;
        private float _lowOxygenAudioCooldownRemaining;
        private float _pressureScreechCooldownRemaining;
        private float _toxicRoomVisorPulseCooldownRemaining;
        private uint _pressureScreechRngState = PressureScreechRngSeed;
        private uint _runtimeRoomStatusMask;
        private uint _pendingAtmosphereMutationMask;
        private bool _smokeOverlayRuntimeActive;
        private bool _smokeOverlayRuntimeDirty = true;
        private Vector4 _lastSmokeOverlayParams;
        private Vector4 _lastSmokeOverlayCenter = AtmosphereSootDefaultCenter;
        private bool _pendingLowOxygenAudioLog;
        private bool _pendingToxicRoomVisorPulse;
        private bool _pendingPressureScreech;
        private bool _pendingRoomOxygenHudDirty;
        private bool _pendingRoomOxygenHudHasValue;
        private bool _pendingSmokeOverlayRuntimeDirty;
        private bool _pendingSmokeOverlayActive;
        private float _pendingToxicRoomVisorDanger01;
        private float _pendingRoomOxygenHud01;
        private AudioClip _pendingPressureScreechClip;
        private Vector3 _pendingPressureScreechPosition;
        private float _pendingPressureScreechVolume;
        private float _pendingPressureScreechPitch;
        private Vector4 _pendingSmokeOverlayParams;
        private Vector4 _pendingSmokeOverlayCenter;
        private uint _pendingOverheatApplyMask;
        private uint _pendingOverheatResetMask;
        private readonly float[] _pendingOverheatVoltages = new float[RoomCapacity];

        public bool IsAtmosphereRuntimeActive => isActiveAndEnabled;

        public int RoomCount => fluidDynamics != null ? math.clamp(fluidDynamics.CompartmentCount, 0, RoomCapacity) : 0;

        public int RuntimeEntityIdHash => unchecked((int)EntityId.ToULong(GetEntityId()));

        /// <summary>Signals refused by bounded pressure/event lanes since this runtime was enabled.</summary>
        public int DroppedSignalCount => _droppedSignalCount;

        internal uint RuntimeRoomStatusMask => _runtimeRoomStatusMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteClampedOr(float value, float fallback, float minimum, float maximum)
        {
            return math.clamp(math.isfinite(value) ? value : fallback, minimum, maximum);
        }

        private void ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature)
        {
            float rawMinimum = FiniteOr(minimumTemperatureCelsius, DefaultMinimumTemperatureCelsius);
            float rawMaximum = FiniteOr(maximumTemperatureCelsius, DefaultMaximumTemperatureCelsius);
            minimumTemperature = math.min(rawMinimum, rawMaximum);
            maximumTemperature = math.max(rawMinimum, rawMaximum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSafeReferencePressureKPa()
        {
            return math.max(1f, FiniteOr(referencePressureKPa, DefaultReferencePressureKPa));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSafeMaximumPressureKPa()
        {
            return math.max(ResolveSafeReferencePressureKPa(), FiniteOr(maximumPressureKPa, DefaultMaximumPressureKPa));
        }

        public float GetAirlockEqualizationTime(float internalPressurePa, float externalPressurePa, float airlockVolumeM3, float valveFlowRateM3PerSec)
        {
            return Hecton8.PureLogic.Systems.PressureEqualizationCalculator.Compute(internalPressurePa, externalPressurePa, airlockVolumeM3, valveFlowRateM3PerSec, Epsilon);
        }

        public float GetRoomPressureKPa(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _pressureFrontHandle, roomIndex, out float pressure))
            {
                return ResolveSafeReferencePressureKPa();
            }

            return FiniteClampedOr(pressure, ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomOxygenFraction(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _o2FrontHandle, roomIndex, out float oxygenUnits))
            {
                return DefaultInitialOxygenFraction;
            }

            return math.saturate(FiniteNonNegativeOrZero(oxygenUnits) / math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity)));
        }

        public float GetRoomCarbonDioxideFraction(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _co2FrontHandle, roomIndex, out float carbonDioxideUnits))
            {
                return DefaultInitialCarbonDioxideFraction;
            }

            return math.saturate(FiniteNonNegativeOrZero(carbonDioxideUnits) / math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity)));
        }

        public float GetRoomOxygenPartialPressureKPa(int roomIndex)
        {
            float fallback = DefaultInitialOxygenFraction * ResolveSafeReferencePressureKPa();
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _o2PartialPressureFrontHandle, roomIndex, out float oxygenPressure))
            {
                return fallback;
            }

            return FiniteClampedOr(oxygenPressure, fallback, 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomCarbonDioxidePartialPressureKPa(int roomIndex)
        {
            float fallback = DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa();
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _co2PartialPressureFrontHandle, roomIndex, out float carbonDioxidePressure))
            {
                return fallback;
            }

            return FiniteClampedOr(carbonDioxidePressure, fallback, 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomNitrogenPartialPressureKPa(int roomIndex)
        {
            float fallback = DefaultInertFraction * ResolveSafeReferencePressureKPa();
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _n2PartialPressureFrontHandle, roomIndex, out float nitrogenPressure))
            {
                return fallback;
            }

            return FiniteClampedOr(nitrogenPressure, fallback, 0f, ResolveSafeMaximumPressureKPa());
        }

        public float GetRoomCarbonDioxidePressureFraction(int roomIndex)
        {
            return ResolveRoomCarbonDioxidePressureFraction(roomIndex);
        }

        public float GetRoomTemperatureCelsius(int roomIndex)
        {
            float fallback = FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _temperatureFrontHandle, roomIndex, out float temperature))
            {
                return fallback;
            }

            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            return FiniteClampedOr(temperature, fallback, minimumTemperature, maximumTemperature);
        }

        public float GetRoomFloodFillRatio(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _roomVolumesHandle, roomIndex, out float rawRoomVolume) ||
                !TryReadVaultValue(in _floodVolumesHandle, roomIndex, out float rawFloodVolume))
            {
                return 0f;
            }

            float roomVolume = math.max(Epsilon, FiniteOr(rawRoomVolume, Epsilon));
            return math.saturate(FiniteNonNegativeOrZero(rawFloodVolume) / roomVolume);
        }

        public void InjectOxygenUnits(int roomIndex, float oxygenUnits)
        {
            InjectOxygenUnitsInternal(roomIndex, oxygenUnits);
        }

        private float InjectOxygenUnitsInternal(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f ||
                !math.isfinite(oxygenUnits) ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return 0f;

            try
            {
                if (!_o2Front.IsCreated ||
                    !_co2Front.IsCreated ||
                    !_inertFront.IsCreated ||
                    !_pressureFront.IsCreated ||
                    !_gasVolumeFront.IsCreated)
                {
                    return 0f;
                }

                if (!TryPrepareAtmosphereFrontForWrite())
                    return QueuePendingOxygenUnits(roomIndex, oxygenUnits);

                return ApplyOxygenUnitsImmediate(roomIndex, oxygenUnits);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        internal float TransferOxygenFromStorage(int roomIndex, float requestedOxygenUnits, ref float storageOxygenUnits)
        {
            if (requestedOxygenUnits <= 0f ||
                !math.isfinite(requestedOxygenUnits) ||
                storageOxygenUnits <= 0f ||
                !math.isfinite(storageOxygenUnits) ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return 0f;

            try
            {
                if (!_o2Front.IsCreated)
                    return 0f;

                if (!TryPrepareAtmosphereFrontForWrite())
                {
                    float queuedTransfer = QueuePendingOxygenUnits(roomIndex, math.min(requestedOxygenUnits, storageOxygenUnits));
                    if (queuedTransfer <= 0f)
                        return 0f;

                    storageOxygenUnits = math.max(0f, storageOxygenUnits - queuedTransfer);
                    return queuedTransfer;
                }

                float transfer = ApplyOxygenUnitsImmediate(roomIndex, math.min(requestedOxygenUnits, storageOxygenUnits));
                if (transfer <= 0f)
                    return 0f;

                storageOxygenUnits = math.max(0f, storageOxygenUnits - transfer);
                return transfer;
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        /// <summary>
        /// Injects oxygen from mature cultivation slots carrying genetics Bit1 into a room atmosphere.
        /// </summary>
        internal float InjectCultivationOxygenFromSlots(
            int roomIndex,
            NativeArray<CultivationManager.CultivationSlotState>.ReadOnly slots,
            float oxygenUnitsPerMaturePlant)
        {
            if (oxygenUnitsPerMaturePlant <= 0f ||
                !math.isfinite(oxygenUnitsPerMaturePlant) ||
                !slots.IsCreated ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return 0f;
            }

            ulong oxygenGeneMask = (ulong)GeneticTraitProfile.GeneticTraitMask.OxygenProducing;
            float oxygenUnits = 0f;
            for (int i = 0; i < slots.Length; i++)
            {
                CultivationManager.CultivationSlotState slot = slots[i];
                if (slot.SeedItemHashId == 0 ||
                    slot.Growth01 < 0.999f ||
                    slot.Quality01 <= 0f ||
                    (slot.GeneticsMask & oxygenGeneMask) == 0UL)
                {
                    continue;
                }

                oxygenUnits += oxygenUnitsPerMaturePlant;
            }

            if (oxygenUnits <= 0f)
                return 0f;

            return InjectOxygenUnitsInternal(roomIndex, oxygenUnits);
        }

        public void InjectRoomTemperatureDeltaCelsius(int roomIndex, float deltaCelsius)
        {
            if (deltaCelsius == 0f ||
                !math.isfinite(deltaCelsius) ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<float> _temperatureFront = this._temperatureFront;
                if (!_temperatureFront.IsCreated)
                    return;

                if (!TryPrepareAtmosphereFrontForWrite())
                {
                    QueuePendingTemperatureDelta(roomIndex, deltaCelsius);
                    return;
                }

                ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
                float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = math.clamp(
                    currentTemperature + deltaCelsius,
                    minTemperature,
                    maxTemperature);
                RefreshRoomPressureImmediate(roomIndex);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        public void InjectRoomHeatEnergyJoules(int roomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f ||
                !math.isfinite(heatEnergyJoules) ||
                roomIndex < 0 ||
                roomIndex >= RoomCount)
            {
                return;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<float> _temperatureFront = this._temperatureFront;
                if (!_temperatureFront.IsCreated)
                    return;

                if (!TryPrepareAtmosphereFrontForWrite())
                {
                    QueuePendingHeatEnergy(roomIndex, heatEnergyJoules);
                    return;
                }

                float thermalCapacity = ResolveInstantThermalCapacity(roomIndex);
                if (thermalCapacity <= Epsilon)
                    return;

                float deltaCelsius = heatEnergyJoules / thermalCapacity;
                if (!math.isfinite(deltaCelsius) || deltaCelsius <= 0f)
                    return;

                ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
                float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = math.clamp(
                    currentTemperature + deltaCelsius,
                    minTemperature,
                    maxTemperature);
                RefreshRoomPressureImmediate(roomIndex);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        public void TransferRoomHeatEnergyJoules(int sourceRoomIndex, int destinationRoomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules <= 0f ||
                !math.isfinite(heatEnergyJoules) ||
                sourceRoomIndex < 0 || sourceRoomIndex >= RoomCount ||
                destinationRoomIndex < 0 || destinationRoomIndex >= RoomCount ||
                sourceRoomIndex == destinationRoomIndex)
            {
                return;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<float> _temperatureFront = this._temperatureFront;
                if (!_temperatureFront.IsCreated)
                    return;

                if (!TryPrepareAtmosphereFrontForWrite())
                {
                    QueuePendingHeatEnergy(sourceRoomIndex, -heatEnergyJoules);
                    QueuePendingHeatEnergy(destinationRoomIndex, heatEnergyJoules);
                    return;
                }

                float sourceCapacity = ResolveInstantThermalCapacity(sourceRoomIndex);
                float destinationCapacity = ResolveInstantThermalCapacity(destinationRoomIndex);
                if (sourceCapacity <= Epsilon || destinationCapacity <= Epsilon)
                    return;

                float sourceDelta = heatEnergyJoules / sourceCapacity;
                float destinationDelta = heatEnergyJoules / destinationCapacity;
                if (!math.isfinite(sourceDelta) || !math.isfinite(destinationDelta) || sourceDelta <= 0f || destinationDelta <= 0f)
                    return;

                ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
                float sourceTemperature = FiniteClampedOr(_temperatureFront[sourceRoomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                float destinationTemperature = FiniteClampedOr(_temperatureFront[destinationRoomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                _temperatureFront[sourceRoomIndex] = math.clamp(
                    sourceTemperature - sourceDelta,
                    minTemperature,
                    maxTemperature);
                _temperatureFront[destinationRoomIndex] = math.clamp(
                    destinationTemperature + destinationDelta,
                    minTemperature,
                    maxTemperature);
                RefreshRoomPressureImmediate(sourceRoomIndex);
                RefreshRoomPressureImmediate(destinationRoomIndex);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        public void InjectElectrolysisGasPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomCount ||
                (!IsPositiveFinite(hydrogenUnits) && !IsPositiveFinite(oxygenUnits) && !IsPositiveFinite(pressureSpikeKPa)))
            {
                return;
            }

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
                NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
                NativeArray<float> _pressureFront = this._pressureFront;
                if (!_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated)
                    return;

                if (!TryPrepareAtmosphereFrontForWrite())
                {
                    QueuePendingElectrolysisPocket(roomIndex, hydrogenUnits, oxygenUnits, pressureSpikeKPa);
                    return;
                }

                if (IsPositiveFinite(hydrogenUnits))
                    _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) + hydrogenUnits;

                if (IsPositiveFinite(oxygenUnits))
                    _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) + oxygenUnits;

                if (_pressureFront.IsCreated && IsPositiveFinite(pressureSpikeKPa))
                {
                    float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                    _pressureFront[roomIndex] = math.clamp(
                        currentPressure + pressureSpikeKPa,
                        0f,
                        ResolveSafeMaximumPressureKPa());
                    RefreshRoomStatusBitsImmediate(roomIndex);
                }
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        private bool TryPrepareAtmosphereFrontForWrite()
        {
            if (_atmosphereJobRunning || _atmospherePhaseMutationGuardMask == 0ul)
                return false;

            ApplyPendingAtmosphereMutations();
            return true;
        }

        private float ApplyOxygenUnitsImmediate(int roomIndex, float oxygenUnits)
        {
            NativeArray<float> _o2Front = this._o2Front;
            if (oxygenUnits <= 0f || !math.isfinite(oxygenUnits) || !_o2Front.IsCreated || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            float currentOxygenUnits = FiniteNonNegativeOrZero(_o2Front[roomIndex]);
            float maximumOxygenUnits = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float clampedOxygenDelta = math.min(oxygenUnits, math.max(0f, maximumOxygenUnits - currentOxygenUnits));
            if (clampedOxygenDelta <= Epsilon)
                return 0f;

            _o2Front[roomIndex] = currentOxygenUnits + clampedOxygenDelta;
            RefreshRoomPressureImmediate(roomIndex);
            return clampedOxygenDelta;
        }

        private float QueuePendingOxygenUnits(int roomIndex, float oxygenUnits)
        {
            if (oxygenUnits <= 0f || !math.isfinite(oxygenUnits) || roomIndex < 0 || roomIndex >= RoomCount || !_o2Front.IsCreated)
                return 0f;

            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            float currentOxygenUnits = FiniteNonNegativeOrZero(_o2Front[roomIndex]);
            float pendingOxygenUnits = math.isfinite(mutation.OxygenUnits) ? math.max(0f, mutation.OxygenUnits) : 0f;
            float maximumOxygenUnits = ResolveRoomMaxOxygenCapacityUnits(roomIndex);
            float queuedOxygenUnits = math.min(
                oxygenUnits,
                math.max(0f, maximumOxygenUnits - currentOxygenUnits - pendingOxygenUnits));
            if (queuedOxygenUnits <= Epsilon)
                return 0f;

            mutation.OxygenUnits = pendingOxygenUnits + queuedOxygenUnits;
            _pendingAtmosphereMutationMask |= 1u << roomIndex;
            return queuedOxygenUnits;
        }

        private void QueuePendingTemperatureDelta(int roomIndex, float deltaCelsius)
        {
            if (deltaCelsius == 0f || !math.isfinite(deltaCelsius) || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float currentTemperature = _temperatureFront.IsCreated
                ? FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature)
                : FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            float pendingTemperatureDelta = math.isfinite(mutation.TemperatureDeltaCelsius)
                ? mutation.TemperatureDeltaCelsius
                : 0f;
            float minDelta = minTemperature - currentTemperature;
            float maxDelta = maxTemperature - currentTemperature;
            float clampedPendingDelta = math.clamp(pendingTemperatureDelta + deltaCelsius, minDelta, maxDelta);
            if (math.abs(clampedPendingDelta) <= Epsilon)
            {
                mutation.TemperatureDeltaCelsius = 0f;
                return;
            }

            mutation.TemperatureDeltaCelsius = clampedPendingDelta;
            _pendingAtmosphereMutationMask |= 1u << roomIndex;
        }

        private void QueuePendingHeatEnergy(int roomIndex, float heatEnergyJoules)
        {
            if (heatEnergyJoules == 0f || !math.isfinite(heatEnergyJoules) || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            float thermalCapacity = ResolveInstantThermalCapacity(roomIndex);
            if (thermalCapacity <= Epsilon)
                return;

            QueuePendingTemperatureDelta(roomIndex, heatEnergyJoules / thermalCapacity);
        }

        private void QueuePendingElectrolysisPocket(int roomIndex, float hydrogenUnits, float oxygenUnits, float pressureSpikeKPa)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomCount ||
                (!IsPositiveFinite(hydrogenUnits) && !IsPositiveFinite(oxygenUnits) && !IsPositiveFinite(pressureSpikeKPa)))
            {
                return;
            }

            ref PendingAtmosphereMutation mutation = ref _pendingAtmosphereMutations[roomIndex];
            bool hasMutation = false;
            if (IsPositiveFinite(hydrogenUnits))
            {
                float pendingHydrogenUnits = math.isfinite(mutation.HydrogenPocketUnits)
                    ? math.max(0f, mutation.HydrogenPocketUnits)
                    : 0f;
                mutation.HydrogenPocketUnits = pendingHydrogenUnits + hydrogenUnits;
                hasMutation = true;
            }

            if (IsPositiveFinite(oxygenUnits))
            {
                float pendingOxygenPocketUnits = math.isfinite(mutation.OxygenPocketUnits)
                    ? math.max(0f, mutation.OxygenPocketUnits)
                    : 0f;
                mutation.OxygenPocketUnits = pendingOxygenPocketUnits + oxygenUnits;
                hasMutation = true;
            }

            if (IsPositiveFinite(pressureSpikeKPa))
            {
                float maximumPressure = ResolveSafeMaximumPressureKPa();
                float currentPressure = _pressureFront.IsCreated
                    ? FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, maximumPressure)
                    : ResolveSafeReferencePressureKPa();
                float pendingPressureSpike = math.isfinite(mutation.PressureSpikeKPa)
                    ? math.max(0f, mutation.PressureSpikeKPa)
                    : 0f;
                float clampedPressureSpike = math.min(
                    pressureSpikeKPa,
                    math.max(0f, maximumPressure - currentPressure - pendingPressureSpike));
                if (clampedPressureSpike > Epsilon)
                {
                    mutation.PressureSpikeKPa = pendingPressureSpike + clampedPressureSpike;
                    hasMutation = true;
                }
            }

            if (hasMutation)
                _pendingAtmosphereMutationMask |= 1u << roomIndex;
        }

        private void ApplyPendingAtmosphereMutations()
        {
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
            NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
            NativeArray<float> _pressureFront = this._pressureFront;
            uint mutationMask = _pendingAtmosphereMutationMask;
            if (mutationMask == 0u)
                return;

            _pendingAtmosphereMutationMask = 0u;
            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                uint roomBit = 1u << roomIndex;
                if ((mutationMask & roomBit) == 0u)
                    continue;

                PendingAtmosphereMutation mutation = _pendingAtmosphereMutations[roomIndex];
                _pendingAtmosphereMutations[roomIndex] = default;
                if (roomIndex >= roomCount)
                    continue;

                if (IsPositiveFinite(mutation.OxygenUnits))
                    ApplyOxygenUnitsImmediate(roomIndex, mutation.OxygenUnits);

                bool pressureDirty = false;
                if (_temperatureFront.IsCreated &&
                    mutation.TemperatureDeltaCelsius != 0f &&
                    math.isfinite(mutation.TemperatureDeltaCelsius))
                {
                    ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
                    float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                    _temperatureFront[roomIndex] = math.clamp(
                        currentTemperature + mutation.TemperatureDeltaCelsius,
                        minTemperature,
                        maxTemperature);
                    pressureDirty = true;
                }

                if (pressureDirty)
                    RefreshRoomPressureImmediate(roomIndex);

                if (_hydrogenPocketFront.IsCreated && IsPositiveFinite(mutation.HydrogenPocketUnits))
                    _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) + mutation.HydrogenPocketUnits;

                if (_oxygenPocketFront.IsCreated && IsPositiveFinite(mutation.OxygenPocketUnits))
                    _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) + mutation.OxygenPocketUnits;

                if (_pressureFront.IsCreated && IsPositiveFinite(mutation.PressureSpikeKPa))
                {
                    float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                    _pressureFront[roomIndex] = math.clamp(
                        currentPressure + mutation.PressureSpikeKPa,
                        0f,
                        ResolveSafeMaximumPressureKPa());
                    RefreshRoomStatusBitsImmediate(roomIndex);
                }
            }
        }

        public float GetRoomSteamVolumeCubicMeters(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount ||
                !TryReadVaultValue(in _steamFrontHandle, roomIndex, out float steamVolume))
            {
                return 0f;
            }

            return FiniteNonNegativeOrZero(steamVolume);
        }

        public void HandleExternalModuleBreach(Vector3 breachWorldPosition, float breachAreaSquareMeters)
        {
            if (fluidDynamics == null)
                return;

            int roomIndex = ResolveNearestRoomIndexForWorldPosition(breachWorldPosition);
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return;

            float sanitizedArea = math.max(0.05f, breachAreaSquareMeters);
            fluidDynamics.TriggerImmediateBreachDepressurization(roomIndex, breachWorldPosition, sanitizedArea);
            SealAdjacentBulkheads(roomIndex);
        }

        public float ResolveThermalFatigueMultiplier(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return 1f;

            float thresholdTemperature = math.max(FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), FiniteOr(thermalFatigueThresholdCelsius, DefaultThermalFatigueThresholdCelsius));
            if (GetRoomTemperatureCelsius(roomIndex) < thresholdTemperature)
                return 1f;

            RoomStructuralMaterial structuralMaterial = roomIndex < rooms.Length
                ? rooms[roomIndex].primaryStructuralMaterial
                : RoomStructuralMaterial.Titanium;

            return structuralMaterial == RoomStructuralMaterial.Glass
                ? FiniteNonNegativeOrZero(glassThermalFatigueMultiplier)
                : FiniteNonNegativeOrZero(titaniumThermalFatigueMultiplier);
        }

        private void SealAdjacentBulkheads(int breachedRoomIndex)
        {
            if (fluidDynamics == null || breachedRoomIndex < 0)
                return;

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<byte> _doorSealed = this._doorSealed;
                NativeArray<byte> _doorSealedPrevious = this._doorSealedPrevious;
                int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
                for (int doorIndex = 0; doorIndex < doorCount; doorIndex++)
                {
                    if (!fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                        continue;

                    if (isSealed || (compartmentA != breachedRoomIndex && compartmentB != breachedRoomIndex))
                        continue;

                    fluidDynamics.SetBulkheadSealed(compartmentA, compartmentB, true);
                    if (_doorSealed.IsCreated && doorIndex < _doorSealed.Length)
                        _doorSealed[doorIndex] = 1;
                    if (_doorSealedPrevious.IsCreated && doorIndex < _doorSealedPrevious.Length)
                        _doorSealedPrevious[doorIndex] = 1;
                }
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        public int ResolveNearestRoomIndexForWorldPosition(Vector3 worldPosition)
        {
            return ResolveNearestRoomIndex(worldPosition);
        }

        internal Vector3 ResolveRoomRuntimePosition(int roomIndex)
        {
            if (fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSubmarineFallbackRuntimePosition();

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localCentroid) : localCentroid;
        }

        private Vector3 ResolveSubmarineFallbackRuntimePosition()
        {
            return _submarineBody != null ? _submarineBody.worldCenterOfMass : Vector3.zero;
        }

        public float ResolveRoomFloodFillNormalized(int roomIndex)
        {
            if (fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.saturate(fluidDynamics.GetCompartmentFillRatio(roomIndex));
        }

        public bool TryResolveRoomFloodFillNormalized(Vector3 worldPosition, out int roomIndex, out float floodFillNormalized)
        {
            roomIndex = ResolveNearestRoomIndex(worldPosition);
            if (roomIndex < 0 || roomIndex >= RoomCount)
            {
                floodFillNormalized = 0f;
                return false;
            }

            floodFillNormalized = ResolveRoomFloodFillNormalized(roomIndex);
            return true;
        }

        public float ResolveExternalDepthMeters()
        {
            return fluidDynamics != null ? FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters) : 0f;
        }

        public void ApplyInteractionSignal(in Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            InteractionEffectType effectType = (InteractionEffectType)signal.EffectType;
            if (effectType != InteractionEffectType.PlasmaCut &&
                effectType != InteractionEffectType.Weld &&
                effectType != InteractionEffectType.Torch)
            {
                return;
            }

            int roomIndex = ResolveNearestRoomIndex(runtimeHitPoint);
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return;

            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                if (!_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated)
                    return;

                float pocketIntensity = math.min(FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]), FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]));
                if (pocketIntensity < math.saturate(FiniteOr(explosivePocketThreshold, DefaultExplosionPocketThreshold)))
                    return;

                TriggerExplosivePocketDetonation(roomIndex, runtimeHitPoint, pocketIntensity);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        private void Awake()
        {
            CacheReferencesCold();
            SeedBoilingHazardIds();
            SeedAtmosphereHazardIds();
            RefreshDebugState();
        }

        private void OnEnable()
        {
            _droppedSignalCount = 0;
            CacheReferencesCold();
            PrepareNativeStateCold();
            if (IsAtmosphereVaultStateReady())
                PrewarmAtmosphereAuthoringCaches();

            TryRegisterHotSwapListener();
            TryRegister();
            RefreshDebugState();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeNativeStateDeferred();
            ClearCachedRuntimeServices();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            DisposeNativeStateDeferred();
            ClearCachedRuntimeServices();
        }

        public void ColdTick()
        {
            if (!Application.isPlaying)
                return;

            PrepareNativeStateCold();
            HighPressureEvents.PrepareCold();
            FatalPressureImplosionEvents.PrepareCold();
            if (IsAtmosphereVaultStateReady())
                PrewarmAtmosphereAuthoringCaches();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            RefreshRuntimeContextFromCache();
            if (fluidDynamics == null)
            {
                _atmosphereStepAccumulator = 0f;
                ClearBoilingFloodHazards();
                ClearAtmosphereFakes();
                RefreshDebugState();
                return;
            }

            if (!IsAtmosphereVaultStateReady())
            {
                RefreshDebugState();
                return;
            }

            if (_atmosphereJobRunning)
            {
                AccumulateAtmosphereStepTime(fixedDeltaTime);
                RefreshDebugState();
                return;
            }

            InvalidateTopologyIfShapeChanged();
            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
            {
                AccumulateAtmosphereStepTime(fixedDeltaTime);
                RefreshDebugState();
                return;
            }

            try
            {
                SyncFluidSnapshot();
                SeedTopologyIfNeeded();
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }

            SeedThermalEmittersIfNeeded();
            SeedEmergencyVentPipesIfNeeded();
            if (!TryEnterAtmosphereWritePhase(out ownsWriteLock))
            {
                AccumulateAtmosphereStepTime(fixedDeltaTime);
                RefreshDebugState();
                return;
            }

            try
            {
                AccumulateRoomHeatSources();
                PublishDoorOpeningPressureEvents();
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }

            AccumulateAtmosphereStepTime(fixedDeltaTime);
            float slowTickSeconds = math.max(0.02f, FiniteOr(atmosphereSlowTickSeconds, DefaultAtmosphereSlowTickSeconds));
            if (_atmosphereStepAccumulator + Epsilon < slowTickSeconds)
            {
                RefreshDebugState();
                return;
            }

            float atmosphereDeltaTime = _atmosphereStepAccumulator;
            _atmosphereStepAccumulator = 0f;
            ScheduleAtmosphereJob(atmosphereDeltaTime);
            RefreshDebugState();
        }

        private void PrewarmAtmosphereAuthoringCaches()
        {
            if (TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
            {
                try
                {
                    SeedTopologyIfNeeded();
                }
                finally
                {
                    ExitAtmosphereWritePhase(ownsWriteLock);
                }
            }

            SeedThermalEmittersIfNeeded();
            SeedEmergencyVentPipesIfNeeded();
        }

        private void AccumulateAtmosphereStepTime(float fixedDeltaTime)
        {
            float slowTickSeconds = math.max(0.02f, FiniteOr(atmosphereSlowTickSeconds, DefaultAtmosphereSlowTickSeconds));
            float maxAccumulatedSeconds = slowTickSeconds * MaxAtmosphereCatchupTicks;
            _atmosphereStepAccumulator = math.min(
                FiniteNonNegativeOrZero(_atmosphereStepAccumulator) + FiniteNonNegativeOrZero(fixedDeltaTime),
                maxAccumulatedSeconds);
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ConsumeCompletedJob(fixedDeltaTime);
        }

        public void LateFrameTick()
        {
            FlushQueuedAtmospherePresentation();
        }

        private void ApplyAbyssalBlackoutFreeze(float fixedDeltaTime)
        {
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _floodVolumes = this._floodVolumes;
            NativeArray<float> _roomVolumes = this._roomVolumes;
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            float depthMeters = FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters);
            if (depthMeters < FiniteNonNegativeOrZero(deepFreezeDepthThresholdMeters))
                return;

            IPowerGridService powerGridService = _powerGridService;
            float totalConsumption = powerGridService != null ? FiniteNonNegativeOrZero(powerGridService.TotalConsumption) : 0f;
            float supplyRatio = totalConsumption > Epsilon
                ? math.saturate(FiniteNonNegativeOrZero(powerGridService.TotalGeneration) / totalConsumption)
                : 1f;
            if (supplyRatio >= math.saturate(FiniteOr(deepFreezeSupplyRatioThreshold, DefaultDeepFreezeSupplyRatioThreshold)))
                return;

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float targetTemperature = FiniteClampedOr(deepFreezeTargetTemperatureCelsius, DefaultDeepFreezeTargetTemperatureCelsius, minTemperature, maxTemperature);
            float alpha = ResolveBlendFactor(math.max(0.1f, FiniteOr(deepFreezeTauSeconds, DefaultDeepFreezeTauSeconds)), fixedDeltaTime);
            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomVolume = math.max(Epsilon, FiniteOr(_roomVolumes[roomIndex], Epsilon));
                float floodFillRatio = math.saturate(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]) / roomVolume);
                if (floodFillRatio <= Epsilon)
                    continue;

                float currentTemperature = FiniteClampedOr(_temperatureFront[roomIndex], FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius), minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = math.clamp(
                    math.lerp(currentTemperature, targetTemperature, alpha),
                    minTemperature,
                    maxTemperature);
            }
        }

        private void ProcessSteamPhaseCycle(float fixedDeltaTime)
        {
            NativeArray<float> _steamFront = this._steamFront;
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _floodVolumes = this._floodVolumes;
            NativeArray<float> _roomVolumes = this._roomVolumes;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            if (fluidDynamics == null ||
                !_steamFront.IsCreated ||
                !_temperatureFront.IsCreated ||
                !_floodVolumes.IsCreated ||
                !_roomVolumes.IsCreated)
            {
                return;
            }

            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            float safeSteamExpansionRatio = math.max(1f, FiniteOr(steamExpansionRatio, DefaultSteamExpansionRatio));
            float vaporizationRate = FiniteNonNegativeOrZero(steamGenerationRateCubicMetersPerSecondPerCelsius);
            float condensationRate = FiniteNonNegativeOrZero(steamCondensationCoefficient);
            float hullShellTemperature = ResolveHullShellTemperatureCelsius();
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));

            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[roomIndex], minimumGasVolume));
                float roomTemperature = GetRoomTemperatureCelsius(roomIndex);
                float floodVolume = math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume);
                float steamVolume = FiniteNonNegativeOrZero(_steamFront[roomIndex]);
                float boilingPoint = ResolveRoomBoilingPointCelsius(roomIndex);

                if (floodVolume > Epsilon && roomTemperature > boilingPoint)
                {
                    float overshootCelsius = roomTemperature - boilingPoint;
                    float liquidVaporized = math.min(
                        floodVolume,
                        overshootCelsius * vaporizationRate * safeDeltaTime);
                    if (liquidVaporized > Epsilon)
                    {
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, -liquidVaporized);
                        _floodVolumes[roomIndex] = math.max(0f, floodVolume - liquidVaporized);
                        floodVolume = _floodVolumes[roomIndex];
                        steamVolume += liquidVaporized * safeSteamExpansionRatio;
                    }
                }

                if (steamVolume > Epsilon && roomTemperature > hullShellTemperature)
                {
                    float condensedSteamVolume = math.min(
                        steamVolume,
                        (roomTemperature - hullShellTemperature) * condensationRate * safeDeltaTime);
                    if (condensedSteamVolume > Epsilon)
                    {
                        steamVolume -= condensedSteamVolume;
                        float returnedLiquidVolume = condensedSteamVolume / safeSteamExpansionRatio;
                        fluidDynamics.AddCompartmentFloodVolumeDelta(roomIndex, returnedLiquidVolume);
                        floodVolume = math.clamp(floodVolume + returnedLiquidVolume, 0f, roomVolume);
                        _floodVolumes[roomIndex] = floodVolume;
                    }
                }

                if (_gasVolumeFront.IsCreated)
                    _gasVolumeFront[roomIndex] = math.max(minimumGasVolume, roomVolume - floodVolume);

                _steamFront[roomIndex] = FiniteNonNegativeOrZero(steamVolume);
                RecomputeInstantRoomPressure(roomIndex);
                RefreshRoomStatusBitsOnly(roomIndex);
            }
        }

        private void TryEmergencyAtmosphericVenting(float fixedDeltaTime)
        {
            NativeArray<float> _pressureFront = this._pressureFront;
            NativeArray<float> _steamFront = this._steamFront;
            NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
            NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
            NativeArray<float> _o2Front = this._o2Front;
            NativeArray<float> _co2Front = this._co2Front;
            NativeArray<float> _inertFront = this._inertFront;
            if (_submarineBody == null || fluidDynamics == null || !_pressureFront.IsCreated || !_steamFront.IsCreated)
                return;

            float ventThresholdPressure = ResolveEmergencyVentThresholdPressureKPa();
            if (ventThresholdPressure <= Epsilon)
                return;

            for (int roomIndex = 0; roomIndex < RoomCount; roomIndex++)
            {
                float roomPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                if (roomPressure <= ventThresholdPressure)
                    continue;

                if (!TryResolveEmergencyVentPipe(roomIndex, out LogisticsPipeNode ventPipe))
                {
                    if (roomPressure >= math.max(ventThresholdPressure, ResolveSafeMaximumPressureKPa() * 0.98f))
                        TriggerSteamOverpressureFailure(roomIndex, roomPressure);
                    continue;
                }

                float releaseFraction = math.saturate(FiniteOr(steamVentReleaseFraction, DefaultSteamVentReleaseFraction));
                if (releaseFraction <= Epsilon)
                    continue;

                _steamFront[roomIndex] = FiniteNonNegativeOrZero(_steamFront[roomIndex]) * (1f - releaseFraction);
                _hydrogenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) * (1f - releaseFraction);
                _oxygenPocketFront[roomIndex] = FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) * (1f - releaseFraction);
                _o2Front[roomIndex] = FiniteNonNegativeOrZero(_o2Front[roomIndex]) * (1f - (releaseFraction * 0.5f));
                _co2Front[roomIndex] = FiniteNonNegativeOrZero(_co2Front[roomIndex]) * (1f - (releaseFraction * 0.5f));
                _inertFront[roomIndex] = FiniteNonNegativeOrZero(_inertFront[roomIndex]) * (1f - (releaseFraction * 0.5f));
                RecomputeInstantRoomPressure(roomIndex);
                RefreshRoomStatusBitsOnly(roomIndex);

                Vector3 ventPosition = ventPipe.ResolveVentRuntimePosition();
                Vector3 ventDirection = ventPipe.ResolveVentDirection(_submarineBody.worldCenterOfMass);
                float overshootKPa = FiniteNonNegativeOrZero(roomPressure - ventThresholdPressure);
                float impulseMagnitude = overshootKPa *
                                         FiniteNonNegativeOrZero(steamVentImpulsePerKilopascal) *
                                         math.max(0.05f, releaseFraction);
                if (impulseMagnitude > Epsilon)
                {
                    _physicsService?.QueueForceAtPosition(
                        _submarineBody,
                        -ventDirection * impulseMagnitude,
                        ventPosition,
                        ForceMode.Impulse);
                }

                ventPipe.RegisterEmergencyVentVisual(math.saturate(overshootKPa / math.max(1f, ventThresholdPressure)));
            }
        }

        private void TriggerSteamOverpressureFailure(int roomIndex, float roomPressure)
        {
            if (_submarineBody == null || fluidDynamics == null || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            Vector3 roomPosition = ResolveRoomRuntimePosition(roomIndex);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float overshoot01 = math.saturate((FiniteOr(roomPressure, referencePressure) - referencePressure) / math.max(1f, maximumPressure - referencePressure));
            float explosionPocket = math.max(
                overshoot01,
                math.min(FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]), FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex])));
            TriggerExplosivePocketDetonation(roomIndex, roomPosition, math.max(math.saturate(FiniteOr(explosivePocketThreshold, DefaultExplosionPocketThreshold)), explosionPocket));
            fluidDynamics.TriggerBreach(roomIndex, math.max(0.25f, FiniteNonNegativeOrZero(overshoot01)));
        }

        private static float ResolveBlendFactor(float tauSeconds, float deltaTime)
        {
            float safeTau = math.max(0.0001f, FiniteOr(tauSeconds, 0.0001f));
            float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);
            float normalizedStep = math.min(safeDeltaTime / safeTau, BlendFactorPadeInstantThreshold);
            return ResolveOneMinusExpPade(normalizedStep);
        }

        private static float ResolveOneMinusExpPade(float normalizedStep)
        {
            float x = FiniteNonNegativeOrZero(normalizedStep);
            float numerator = x * (6f + x);
            float denominator = 6f + (4f * x) + (x * x);
            return math.saturate(numerator / math.max(denominator, 0.0001f));
        }

        private void DecayExplosivePockets(float fixedDeltaTime)
        {
            NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
            NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            if (!_hydrogenPocketFront.IsCreated || !_oxygenPocketFront.IsCreated || safeDeltaTime <= 0f)
                return;

            float decay = FiniteNonNegativeOrZero(explosivePocketDecayPerSecond) * safeDeltaTime;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _hydrogenPocketFront[roomIndex] = math.max(0f, FiniteNonNegativeOrZero(_hydrogenPocketFront[roomIndex]) - decay);
                _oxygenPocketFront[roomIndex] = math.max(0f, FiniteNonNegativeOrZero(_oxygenPocketFront[roomIndex]) - decay);
            }
        }

        private void TriggerExplosivePocketDetonation(int roomIndex, Vector3 runtimeHitPoint, float pocketIntensity)
        {
            NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
            NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
            NativeArray<float> _pressureFront = this._pressureFront;
            if (_submarineBody == null || roomIndex < 0 || roomIndex >= RoomCount)
                return;

            Vector3 roomPosition = ResolveRoomRuntimePosition(roomIndex);
            Vector3 centerDirection = roomPosition - _submarineBody.worldCenterOfMass;
            Vector3 forceDirection = ResolveFakeBlastDirection(centerDirection, 0.35f);
            float impulseMagnitude = math.min(
                FiniteNonNegativeOrZero(pocketIntensity) * FiniteNonNegativeOrZero(explosionImpulsePerPocketUnit),
                math.max(1f, FiniteOr(explosionMaximumImpulseNewtonSeconds, DefaultExplosionMaximumImpulseNewtonSeconds)));

            _physicsService?.QueueForceAtPosition(
                _submarineBody,
                forceDirection * impulseMagnitude,
                runtimeHitPoint,
                ForceMode.Impulse);

            _hydrogenPocketFront[roomIndex] = 0f;
            _oxygenPocketFront[roomIndex] = 0f;
            if (_pressureFront.IsCreated)
            {
                float currentPressure = FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa());
                _pressureFront[roomIndex] = math.min(
                    ResolveSafeMaximumPressureKPa(),
                    currentPressure + FiniteNonNegativeOrZero(explosionPressureSpikeKPa));
                RefreshRoomStatusBitsImmediate(roomIndex);
            }
        }

        private float ResolveHullShellTemperatureCelsius()
        {
            if (fluidDynamics == null)
                return FiniteOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius);

            float depthMeters = FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters);
            float abyssalCooling = math.saturate(depthMeters / 4000f);
            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            float floodTemperature = FiniteClampedOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius, minTemperature, maxTemperature);
            return math.lerp(referenceTemperature, floodTemperature, abyssalCooling);
        }

        private float ResolveRoomBoilingPointCelsius(int roomIndex)
        {
            float externalDepthMeters = fluidDynamics != null ? FiniteNonNegativeOrZero(fluidDynamics.ExternalDepthMeters) : 0f;
            float pressureKPa = roomIndex >= 0 && roomIndex < RoomCount && _pressureFront.IsCreated
                ? math.max(ResolveSafeReferencePressureKPa(), FiniteClampedOr(_pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa()))
                : ResolveSafeReferencePressureKPa();
            float pressureDepthEquivalent = math.max(0f, pressureKPa - ResolveSafeReferencePressureKPa());
            return 100f + ((externalDepthMeters + (pressureDepthEquivalent * 0.1f)) * 0.02f);
        }

        private void RecomputeInstantRoomPressure(int roomIndex)
        {
            NativeArray<float> _pressureFront = this._pressureFront;
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            if (roomIndex < 0 || roomIndex >= RoomCount || !_pressureFront.IsCreated || !_gasVolumeFront.IsCreated)
                return;

            float temperatureCelsius = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            _pressureFront[roomIndex] = ResolveInstantFakePressure(roomIndex, temperatureCelsius);
        }

        private float ResolveRoomCarbonDioxidePressureFraction(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return DefaultInitialCarbonDioxideFraction;

            float pressure = TryReadVaultValue(in _pressureFrontHandle, roomIndex, out float rawPressure)
                ? FiniteClampedOr(rawPressure, ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa())
                : ResolveSafeReferencePressureKPa();
            float fallbackCarbonDioxide = DefaultInitialCarbonDioxideFraction * ResolveSafeReferencePressureKPa();
            float carbonDioxidePartialPressure = TryReadVaultValue(in _co2PartialPressureFrontHandle, roomIndex, out float rawCarbonDioxidePressure)
                ? FiniteClampedOr(rawCarbonDioxidePressure, fallbackCarbonDioxide, 0f, ResolveSafeMaximumPressureKPa())
                : fallbackCarbonDioxide;
            if (pressure <= Epsilon)
                return 0f;

            float inversePressure = math.rcp(math.max(pressure, Epsilon));
            return math.saturate(carbonDioxidePartialPressure * inversePressure);
        }

        private void RefreshRoomPressureImmediate(int roomIndex)
        {
            RecomputeInstantRoomPressure(roomIndex);
            RefreshRoomStatusBitsImmediate(roomIndex);
        }

        private void RefreshRoomStatusBitsOnly(int roomIndex)
        {
            NativeArray<uint> _roomStatusMaskFront = this._roomStatusMaskFront;
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomStatusMaskFront.IsCreated)
                return;

            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = ResolveRoomStatusMask(roomIndex, _roomStatusMaskFront[0]);
            _roomStatusMaskFront[0] = statusMask;
            _runtimeRoomStatusMask = (_runtimeRoomStatusMask & ~roomStatusBits) | (statusMask & roomStatusBits);
        }

        private void RefreshRoomStatusBitsImmediate(int roomIndex)
        {
            NativeArray<uint> _roomStatusMaskFront = this._roomStatusMaskFront;
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomStatusMaskFront.IsCreated)
                return;

            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = ResolveRoomStatusMask(roomIndex, _roomStatusMaskFront[0]);
            _roomStatusMaskFront[0] = statusMask;
            _runtimeRoomStatusMask = (_runtimeRoomStatusMask & ~roomStatusBits) | (statusMask & roomStatusBits);

            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenThreshold = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            bool hasOxygen = _o2Front.IsCreated;
            float oxygenValue = hasOxygen ? FiniteNonNegativeOrZero(_o2Front[roomIndex]) : tankCapacity;
            bool toxicRoom = (statusMask & (roomBit << RoomStatusToxicShift)) != 0u;
            bool lowOxygen = hasOxygen && oxygenValue < lowOxygenThreshold;
            bool pressureStatus = (statusMask & (roomBit << RoomStatusPressureShift)) != 0u;
            float carbonDioxideDanger01 = ResolveCarbonDioxideToxicity01(ResolveRoomCarbonDioxidePressureFraction(roomIndex));

            if (hasOxygen && _roomPlayerCounts.IsCreated && _roomPlayerCounts[roomIndex] > 0)
            {
                float roomOxygen01 = oxygenValue / tankCapacity;
                if (math.isfinite(roomOxygen01))
                    QueueOccupiedRoomOxygenHud(true, math.saturate(roomOxygen01));
                else
                    QueueOccupiedRoomOxygenHud(false, 0f);
            }

            Vector3 worldCenter = default;
            float radius = 0f;
            bool needsBounds = toxicRoom || pressureStatus;
            bool hasBounds = needsBounds && TryResolveRoomHazardBounds(roomIndex, out worldCenter, out radius);
            if (toxicRoom && hasBounds)
            {
                float oxygenDanger01 = math.saturate((lowOxygenThreshold - oxygenValue) / math.max(1f, lowOxygenThreshold));
                float toxicityDanger01 = math.max(0.1f, math.max(oxygenDanger01, carbonDioxideDanger01));
                RegisterToxicRoomHazard(roomIndex, worldCenter, toxicityDanger01 * FiniteNonNegativeOrZero(toxicRoomHazardIntensity), radius);
            }
            else
            {
                UnregisterToxicRoomHazard(roomIndex);
            }

            if (pressureStatus)
                QueuePressureScreech(roomIndex, hasBounds ? worldCenter : ResolveSubmarineFallbackRuntimePosition());

            float roomTemperature = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            if (_temperatureFront.IsCreated &&
                (roomTemperature > FiniteOr(overheatBrownoutTemperatureCelsius, DefaultOverheatBrownoutTemperatureCelsius) ||
                 (_overheatVisualActiveMask & roomBit) != 0u))
            {
                QueueOverheatVoltageFake(roomIndex, ResolveModuleForRoom(roomIndex));
            }
        }

        private uint ResolveRoomStatusMask(int roomIndex, uint sourceStatusMask)
        {
            uint roomBit = 1u << roomIndex;
            uint roomStatusBits = ResolveRoomStatusBits(roomBit);
            uint statusMask = sourceStatusMask & ~roomStatusBits;

            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenThreshold = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            bool hasOxygen = _o2Front.IsCreated;
            float oxygenValue = hasOxygen ? FiniteNonNegativeOrZero(_o2Front[roomIndex]) : tankCapacity;
            bool carbonDioxideToxic = ResolveRoomCarbonDioxidePressureFraction(roomIndex) >= DefaultCarbonDioxideToxicityFraction;
            if ((hasOxygen && oxygenValue < lowOxygenThreshold) || carbonDioxideToxic)
                statusMask |= roomBit << RoomStatusToxicShift;

            float roomTemperature = _temperatureFront.IsCreated ? GetRoomTemperatureCelsius(roomIndex) : FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            if (_temperatureFront.IsCreated && roomTemperature <= FiniteClampedOr(freezingRoomTemperatureCelsius, DefaultFreezingRoomTemperatureCelsius, minTemperature, maxTemperature))
                statusMask |= roomBit << RoomStatusFreezingShift;

            float referencePressure = ResolveSafeReferencePressureKPa();
            float pressureThreshold = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            if (_pressureFront.IsCreated &&
                FiniteClampedOr(_pressureFront[roomIndex], referencePressure, 0f, ResolveSafeMaximumPressureKPa()) >= pressureThreshold)
            {
                statusMask |= roomBit << RoomStatusPressureShift;
            }

            return statusMask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveRoomStatusBits(uint roomBit)
        {
            return (roomBit << RoomStatusToxicShift) |
                   (roomBit << RoomStatusFreezingShift) |
                   (roomBit << RoomStatusPressureShift);
        }

        private float ResolveInstantPressureWithTemperature(float totalGasUnits, float gasVolumeCubicMeters, float temperatureCelsius)
        {
            return ResolveSimplePressureKPa(gasVolumeCubicMeters, 0f, 0f, temperatureCelsius);
        }

        private float ResolveRoomMaxOxygenCapacityUnits(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return 0f;

            return math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
        }

        private float ResolveEmergencyVentThresholdPressureKPa()
        {
            float pressureCap = ResolveSafeMaximumPressureKPa();
            float ratioThreshold = math.saturate(FiniteOr(steamVentMinimumPressureRatio, DefaultSteamVentMinimumPressureRatio));
            float hullThreshold = fluidDynamics != null ? fluidDynamics.HullPressureRatingKPa : pressureCap;
            return math.min(FiniteOr(hullThreshold, pressureCap), pressureCap * math.max(0.1f, ratioThreshold));
        }

        private bool TryResolveEmergencyVentPipe(int roomIndex, out LogisticsPipeNode ventPipe)
        {
            ventPipe = null;
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return false;

            SeedEmergencyVentPipesIfNeeded();

            uint roomBit = 1u << roomIndex;
            if ((_emergencyVentRoomMask & roomBit) == 0u)
                return false;

            LogisticsPipeNode cachedPipe = _emergencyVentPipesByRoom[roomIndex];
            if (cachedPipe == null || !cachedPipe.CanEmergencyVent)
            {
                _emergencyVentRoomMask &= ~roomBit;
                _emergencyVentPipesByRoom[roomIndex] = null;
                return false;
            }

            ventPipe = cachedPipe;
            return true;
        }

        private void SeedEmergencyVentPipesIfNeeded()
        {
            if (_emergencyVentPipesSeeded || fluidDynamics == null || !_topologySeeded)
                return;

            _emergencyVentRoomMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;

            int pipeCount = LogisticsPipeTransportScheduler.ActiveNodeCount;
            for (int pipeIndex = 0; pipeIndex < pipeCount; pipeIndex++)
            {
                LogisticsPipeNode pipe = LogisticsPipeTransportScheduler.GetActiveNodeAt(pipeIndex);
                if (pipe == null || !pipe.CanEmergencyVent || !IsComponentOwnedByThisSubmarine(pipe))
                    continue;

                int roomIndex = pipe.ResolveAmbientRoomIndex();
                if (roomIndex < 0 || roomIndex >= RoomCapacity)
                    continue;

                uint roomBit = 1u << roomIndex;
                if ((_emergencyVentRoomMask & roomBit) != 0u)
                    continue;

                _emergencyVentPipesByRoom[roomIndex] = pipe;
                _emergencyVentRoomMask |= roomBit;
            }

            _emergencyVentPipesSeeded = true;
        }

        private void CacheReferencesCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            _dataVault = GlobalRegistry.DataVault;
            _powerGridService = GlobalRegistry.PowerGrid;
            CacheAudioLogSystem(GlobalRegistry.AudioLogs);
            _playerSensoryService = GlobalRegistry.PlayerSensory;
            CacheAudioService(GlobalRegistry.Audio);
            _physicsService = GlobalRegistry.Physics;
            _thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            CacheComponentReferencesCold();
            RefreshRuntimeContextFromCache();
        }

        private void ClearCachedRuntimeServices()
        {
            ClearPlayerRuntimeContext(_playerRuntimeContext);
            _playerRuntimeContext = null;
            _dataVault = null;
            _powerGridService = null;
            _audioLogs = null;
            _playerSensoryService = null;
            _audioService = null;
            _physicsService = null;
            _thermodynamicsService = null;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CacheAudioLogSystem(AudioLogSystem audioLogs)
        {
            _audioLogs = IsAudioLogSystemUsable(audioLogs) ? audioLogs : null;
        }

        private AudioLogSystem ResolveAudioLogSystem()
        {
            AudioLogSystem audioLogs = _audioLogs;
            if (IsAudioLogSystemUsable(audioLogs))
                return audioLogs;

            _audioLogs = null;
            return null;
        }

        private static bool IsAudioLogSystemUsable(AudioLogSystem audioLogs)
        {
            return audioLogs != null && audioLogs.IsAudioLogRuntimeReady;
        }

        private void CacheComponentReferencesCold()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (fluidDynamics == null)
                TryGetComponent(out fluidDynamics);

            if (_submarineBody == null && fluidDynamics != null)
                fluidDynamics.TryGetComponent(out _submarineBody);
        }

        private void RefreshRuntimeContextFromCache()
        {
            if (_playerTransform == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                {
                    _playerTransform = playerContext.PlayerTransform;
                    _playerCamera = playerContext.PlayerCamera;
                }
            }
            else if (_playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                    _playerCamera = playerContext.PlayerCamera;
            }

            if (IsUnityObjectInvalid(_thermodynamicsService))
                _thermodynamicsService = null;
        }

        private void SeedBoilingHazardIds()
        {
            int instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _boilingHazardIds[roomIndex] = (instanceId * 97) ^ (0x61A0 + roomIndex);
        }

        private void SeedAtmosphereHazardIds()
        {
            int instanceId = unchecked((int)EntityId.ToULong(GetEntityId()));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _toxicRoomHazardIds[roomIndex] = (instanceId * 131) ^ (0xA70C + roomIndex);
                _fireSmokeHazardIds[roomIndex] = (instanceId * 149) ^ (0x5A10 + roomIndex);
            }
        }

        private void PublishAtmosphereFakes(float fixedDeltaTime)
        {
            float safeDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            if (safeDeltaTime > 0f)
            {
                _lowOxygenAudioCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_lowOxygenAudioCooldownRemaining) - safeDeltaTime);
                _pressureScreechCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_pressureScreechCooldownRemaining) - safeDeltaTime);
                _toxicRoomVisorPulseCooldownRemaining = math.max(0f, FiniteNonNegativeOrZero(_toxicRoomVisorPulseCooldownRemaining) - safeDeltaTime);
            }

            if (fluidDynamics == null || !_o2Front.IsCreated || !_temperatureFront.IsCreated || !_roomStatusMaskFront.IsCreated)
            {
                ClearAtmosphereFakes();
                return;
            }

            int roomCount = RoomCount;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float lowOxygenValue = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity;
            uint runtimeStatusMask = _roomStatusMaskFront[0] & ResolveActiveRoomStatusMask(roomCount);
            bool smokeOverlayActive = false;
            Vector4 smokeOverlayParams = Vector4.zero;
            Vector4 smokeOverlayCenter = AtmosphereSootDefaultCenter;
            bool hasOccupiedRoomOxygen = false;
            float occupiedRoomOxygen01 = 1f;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    UnregisterToxicRoomHazard(roomIndex);
                    UnregisterFireSmokeHazard(roomIndex);
                    ResetOverheatVisual(roomIndex);
                    continue;
                }

                float oxygenValue = FiniteNonNegativeOrZero(_o2Front[roomIndex]);

                uint roomBit = 1u << roomIndex;
                bool toxicRoom = (runtimeStatusMask & (roomBit << RoomStatusToxicShift)) != 0u;
                bool lowOxygen = oxygenValue < lowOxygenValue;
                bool pressureStatus = (runtimeStatusMask & (roomBit << RoomStatusPressureShift)) != 0u;
                float carbonDioxideDanger01 = ResolveCarbonDioxideToxicity01(ResolveRoomCarbonDioxidePressureFraction(roomIndex));

                bool playerOccupied = _roomPlayerCounts.IsCreated && _roomPlayerCounts[roomIndex] > 0;
                if (playerOccupied)
                {
                    hasOccupiedRoomOxygen = true;
                    occupiedRoomOxygen01 = math.saturate(oxygenValue / tankCapacity);
                }

                float oxygenDanger01 = lowOxygen
                    ? math.saturate((lowOxygenValue - oxygenValue) / math.max(1f, lowOxygenValue))
                    : 0f;
                float toxicityDanger01 = toxicRoom
                    ? math.max(0.1f, math.max(oxygenDanger01, carbonDioxideDanger01))
                    : 0f;
                if (toxicRoom && playerOccupied)
                {
                    if (lowOxygen)
                        QueueLowOxygenGaspingAudioLog();
                    QueueToxicRoomVisorPulse(toxicityDanger01);
                }

                BaseModule roomModule = ResolveModuleForRoom(roomIndex);
                bool fireSmoke = roomModule != null && roomModule.CurrentFailureMode == BaseModuleFailureMode.Fire;
                if (fireSmoke)
                    runtimeStatusMask |= roomBit << RoomStatusFireShift;

                bool hasBounds = TryResolveRoomHazardBounds(roomIndex, roomModule, out Vector3 worldCenter, out float radius);

                if (toxicRoom && hasBounds)
                {
                    RegisterToxicRoomHazard(roomIndex, worldCenter, toxicityDanger01 * FiniteNonNegativeOrZero(toxicRoomHazardIntensity), radius);
                }
                else
                {
                    UnregisterToxicRoomHazard(roomIndex);
                }

                if (fireSmoke && hasBounds)
                {
                    RegisterFireSmokeHazard(
                        roomIndex,
                        worldCenter,
                        FiniteNonNegativeOrZero(fireSmokeHazardIntensity),
                        radius,
                        math.max(1f, FiniteOr(fireSmokeVisorGlitchBias, DefaultFireSmokeVisorGlitchBias)));

                    if (playerOccupied)
                        AccumulateSmokeOverlayFake(worldCenter, radius, ref smokeOverlayActive, ref smokeOverlayParams, ref smokeOverlayCenter);
                }
                else
                {
                    UnregisterFireSmokeHazard(roomIndex);
                }

                if (pressureStatus)
                    QueuePressureScreech(roomIndex, hasBounds ? worldCenter : ResolveRoomRuntimePosition(roomIndex));

                QueueOverheatVoltageFake(roomIndex, roomModule);
            }

            QueueOccupiedRoomOxygenHud(hasOccupiedRoomOxygen, occupiedRoomOxygen01);
            _runtimeRoomStatusMask = runtimeStatusMask;
            QueueSmokeOverlayRuntimeState(smokeOverlayActive, in smokeOverlayParams, in smokeOverlayCenter);
        }

        private void QueueOccupiedRoomOxygenHud(bool hasOccupiedRoomOxygen, float oxygen01)
        {
            _pendingRoomOxygenHudDirty = true;
            _pendingRoomOxygenHudHasValue = hasOccupiedRoomOxygen && math.isfinite(oxygen01);
            _pendingRoomOxygenHud01 = _pendingRoomOxygenHudHasValue ? math.saturate(oxygen01) : 0f;
        }

        private void PublishQueuedOccupiedRoomOxygenHud()
        {
            if (!_pendingRoomOxygenHudHasValue)
            {
                UIStateStore.ClearValue(UIValueSlotId.RoomOxygen01);
                return;
            }

            UIStateStore.WriteValue(UIValueSlotId.RoomOxygen01, _pendingRoomOxygenHud01, (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds);
        }

        private void ClearAtmosphereFakes()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                UnregisterToxicRoomHazard(roomIndex);
                UnregisterFireSmokeHazard(roomIndex);
                ResetOverheatVisual(roomIndex);
            }

            _runtimeRoomStatusMask = 0u;
            ClearRoomStatusMasks();
            _lowOxygenAudioCooldownRemaining = 0f;
            _pressureScreechCooldownRemaining = 0f;
            _toxicRoomVisorPulseCooldownRemaining = 0f;
            QueueOccupiedRoomOxygenHud(false, 0f);
            QueueSmokeOverlayRuntimeState(false, default, default);
            ClearRoomModuleCache();
            ClearBrownoutRoomModuleCache();
        }

        private void RegisterToxicRoomHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (HectonHazardManager.Register(
                    _toxicRoomHazardIds[roomIndex],
                    worldCenter,
                    FiniteNonNegativeOrZero(intensity),
                    FiniteNonNegativeOrZero(radius),
                    HazardType.Toxicity,
                    1f))
            {
                _toxicRoomHazardActiveMask |= roomBit;
                return;
            }

            UnregisterToxicRoomHazard(roomIndex);
        }

        private void UnregisterToxicRoomHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_toxicRoomHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_toxicRoomHazardIds[roomIndex]);
            _toxicRoomHazardActiveMask &= ~roomBit;
        }

        private void RegisterFireSmokeHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius, float visorGlitchBias)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (HectonHazardManager.Register(
                    _fireSmokeHazardIds[roomIndex],
                    worldCenter,
                    FiniteNonNegativeOrZero(intensity),
                    FiniteNonNegativeOrZero(radius),
                    HazardType.Toxicity,
                    math.max(1f, FiniteOr(visorGlitchBias, DefaultFireSmokeVisorGlitchBias))))
            {
                _fireSmokeHazardActiveMask |= roomBit;
                return;
            }

            UnregisterFireSmokeHazard(roomIndex);
        }

        private void UnregisterFireSmokeHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_fireSmokeHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_fireSmokeHazardIds[roomIndex]);
            _fireSmokeHazardActiveMask &= ~roomBit;
        }

        private void QueueLowOxygenGaspingAudioLog()
        {
            if (_lowOxygenAudioCooldownRemaining > 0f)
                return;

            if (lowOxygenGaspingAudioLog == null)
            {
                _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
                return;
            }

            AudioLogSystem audioLogs = ResolveAudioLogSystem();
            if (audioLogs == null)
            {
                _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
                return;
            }

            _pendingLowOxygenAudioLog = true;
            _lowOxygenAudioCooldownRemaining = FiniteNonNegativeOrZero(lowOxygenAudioCooldownSeconds);
        }

        private void QueueToxicRoomVisorPulse(float oxygenDanger01)
        {
            if (_toxicRoomVisorPulseCooldownRemaining > 0f)
                return;

            float intensity = math.saturate(oxygenDanger01);
            _pendingToxicRoomVisorDanger01 = math.max(_pendingToxicRoomVisorDanger01, intensity);
            _pendingToxicRoomVisorPulse = true;
            _toxicRoomVisorPulseCooldownRemaining = FiniteNonNegativeOrZero(toxicRoomVisorPulseCooldownSeconds);
        }

        private void QueuePressureScreech(int roomIndex, Vector3 worldCenter)
        {
            if (_pressureScreechCooldownRemaining > 0f)
                return;

            AudioClip[] clips = pressureScreechClips;
            int clipCount = clips != null ? clips.Length : 0;
            if (clipCount <= 0)
            {
                _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
                return;
            }

            uint random = NextPressureScreechRandom();
            int clipIndex = (int)(random % (uint)clipCount);
            AudioClip clip = null;
            for (int i = 0; i < clipCount; i++)
            {
                clip = clips[(clipIndex + i) % clipCount];
                if (clip != null)
                    break;
            }

            if (clip == null)
            {
                _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
                return;
            }

            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float pressure = _pressureFront.IsCreated && roomIndex >= 0 && roomIndex < RoomCapacity
                ? FiniteClampedOr(_pressureFront[roomIndex], referencePressure, 0f, maximumPressure)
                : math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            float pressureThreshold = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            float pressureRange = math.max(1f, maximumPressure - pressureThreshold);
            float pressure01 = math.saturate((pressure - pressureThreshold) / pressureRange);
            float resolvedVolume = math.saturate(FiniteOr(pressureScreechVolume, DefaultPressureScreechVolume)) * math.lerp(0.55f, 1f, pressure01);
            float minPitch = math.min(FiniteOr(pressureScreechPitchMin, DefaultPressureScreechPitchMin), FiniteOr(pressureScreechPitchMax, DefaultPressureScreechPitchMax));
            float maxPitch = math.max(FiniteOr(pressureScreechPitchMin, DefaultPressureScreechPitchMin), FiniteOr(pressureScreechPitchMax, DefaultPressureScreechPitchMax));
            float pitchT = (NextPressureScreechRandom() & 0x00FFFFFFu) * (1f / 16777215f);
            _pendingPressureScreechClip = clip;
            _pendingPressureScreechPosition = worldCenter;
            _pendingPressureScreechVolume = resolvedVolume;
            _pendingPressureScreechPitch = math.lerp(minPitch, maxPitch, pitchT);
            _pendingPressureScreech = true;
            _pressureScreechCooldownRemaining = FiniteNonNegativeOrZero(pressureScreechCooldownSeconds);
        }

        private void AccumulateSmokeOverlayFake(
            Vector3 worldCenter,
            float radius,
            ref bool smokeOverlayActive,
            ref Vector4 smokeOverlayParams,
            ref Vector4 smokeOverlayCenter)
        {
            float intensity = math.saturate(FiniteOr(fireSmokeHazardIntensity, DefaultFireSmokeHazardIntensity));
            if (intensity <= 0.001f)
                return;

            float screenRadius = math.saturate(math.max(0.01f, FiniteNonNegativeOrZero(radius) * FiniteNonNegativeOrZero(fireSmokeSootScreenRadiusScale)));
            if (!smokeOverlayActive || intensity > smokeOverlayParams.x)
            {
                smokeOverlayActive = true;
                smokeOverlayParams = new Vector4(
                    intensity,
                    screenRadius,
                    math.saturate(FiniteOr(fireSmokeSootDitherStrength, DefaultFireSmokeSootDitherStrength)),
                    math.saturate(FiniteOr(fireSmokeSootDarkenStrength, DefaultFireSmokeSootDarkenStrength)));
                smokeOverlayCenter = TryResolveSmokeOverlayCenter(worldCenter, out Vector4 viewportCenter)
                    ? viewportCenter
                    : AtmosphereSootDefaultCenter;
            }
        }

        private bool TryResolveSmokeOverlayCenter(Vector3 worldCenter, out Vector4 viewportCenter)
        {
            viewportCenter = default;
            Camera playerCamera = _playerCamera;
            if (playerCamera == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null)
                {
                    playerCamera = playerContext.PlayerCamera;
                    _playerCamera = playerCamera;
                }
            }

            if (playerCamera == null)
                return false;

            Vector3 viewportPoint = playerCamera.WorldToViewportPoint(worldCenter);
            if (!math.isfinite(viewportPoint.x) || !math.isfinite(viewportPoint.y) || viewportPoint.z <= 0f)
                return false;

            viewportCenter = new Vector4(
                math.saturate(viewportPoint.x),
                math.saturate(viewportPoint.y),
                0f,
                0f);
            return true;
        }

        private void QueueSmokeOverlayRuntimeState(bool active, in Vector4 smokeOverlayParams, in Vector4 smokeOverlayCenter)
        {
            _pendingSmokeOverlayRuntimeDirty = true;
            _pendingSmokeOverlayActive = active;
            _pendingSmokeOverlayParams = smokeOverlayParams;
            _pendingSmokeOverlayCenter = smokeOverlayCenter;
        }

        private void PublishSmokeOverlayRuntimeState(bool active, in Vector4 smokeOverlayParams, in Vector4 smokeOverlayCenter)
        {
            if (!active)
            {
                if (!_smokeOverlayRuntimeActive && !_smokeOverlayRuntimeDirty)
                    return;

                HectonAtmosphereSootFeature.PublishRuntimeState(false, default, default);
                _lastSmokeOverlayParams = Vector4.zero;
                _lastSmokeOverlayCenter = AtmosphereSootDefaultCenter;
                _smokeOverlayRuntimeActive = false;
                _smokeOverlayRuntimeDirty = false;
                return;
            }

            if (_smokeOverlayRuntimeActive &&
                !_smokeOverlayRuntimeDirty &&
                Vector4Equals(_lastSmokeOverlayParams, smokeOverlayParams) &&
                Vector4Equals(_lastSmokeOverlayCenter, smokeOverlayCenter))
            {
                return;
            }

            HectonAtmosphereSootFeature.PublishRuntimeState(true, in smokeOverlayParams, in smokeOverlayCenter);
            _lastSmokeOverlayParams = smokeOverlayParams;
            _lastSmokeOverlayCenter = smokeOverlayCenter;
            _smokeOverlayRuntimeActive = true;
            _smokeOverlayRuntimeDirty = false;
        }

        private void FlushQueuedAtmospherePresentation()
        {
            if (_pendingRoomOxygenHudDirty)
            {
                _pendingRoomOxygenHudDirty = false;
                PublishQueuedOccupiedRoomOxygenHud();
            }

            if (_pendingSmokeOverlayRuntimeDirty)
            {
                _pendingSmokeOverlayRuntimeDirty = false;
                PublishSmokeOverlayRuntimeState(
                    _pendingSmokeOverlayActive,
                    in _pendingSmokeOverlayParams,
                    in _pendingSmokeOverlayCenter);
            }

            FlushQueuedOverheatVisuals();
            FlushQueuedAtmosphereAudio();
            FlushQueuedAtmosphereVisorPulse();
        }

        private void FlushQueuedOverheatVisuals()
        {
            uint applyMask = _pendingOverheatApplyMask;
            _pendingOverheatApplyMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                uint roomBit = 1u << roomIndex;
                if ((applyMask & roomBit) == 0u)
                    continue;

                BaseModule module = _atmosphereRoomModules[roomIndex];
                if (module == null)
                    continue;

                module.SetAmbientPowerVisualState(true, math.saturate(_pendingOverheatVoltages[roomIndex]));
                _overheatVisualActiveMask |= roomBit;
            }

            uint resetMask = _pendingOverheatResetMask;
            _pendingOverheatResetMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                uint roomBit = 1u << roomIndex;
                if ((resetMask & roomBit) == 0u || (_overheatVisualActiveMask & roomBit) == 0u)
                    continue;

                BaseModule module = _atmosphereRoomModules[roomIndex];
                if (module != null)
                {
                    bool powerBrownout = module.CachedPowerSupplyRatio < math.saturate(FiniteOr(brownoutOxygenSupplyRatioThreshold, DefaultBrownoutOxygenSupplyRatioThreshold));
                    module.SetAmbientPowerVisualState(powerBrownout, powerBrownout ? module.CachedPowerSupplyRatio : 1f);
                }

                _overheatVisualActiveMask &= ~roomBit;
            }
        }

        private void FlushQueuedAtmosphereAudio()
        {
            if (_pendingLowOxygenAudioLog)
            {
                _pendingLowOxygenAudioLog = false;
                AudioLogSystem audioLogs = ResolveAudioLogSystem();
                if (audioLogs != null && lowOxygenGaspingAudioLog != null)
                    audioLogs.PlayLog(lowOxygenGaspingAudioLog);
            }

            if (!_pendingPressureScreech)
                return;

            _pendingPressureScreech = false;
            IAudioService audioService = ResolveAudioService();
            if (audioService != null && _pendingPressureScreechClip != null)
            {
                audioService.PlayAtPoint(
                    _pendingPressureScreechClip,
                    _pendingPressureScreechPosition,
                    _pendingPressureScreechVolume,
                    _pendingPressureScreechPitch);
            }
        }

        private void FlushQueuedAtmosphereVisorPulse()
        {
            if (!_pendingToxicRoomVisorPulse)
                return;

            _pendingToxicRoomVisorPulse = false;
            float intensity = math.saturate(_pendingToxicRoomVisorDanger01);
            _pendingToxicRoomVisorDanger01 = 0f;
            IPlayerSensoryService sensoryService = _playerSensoryService;
            VisorHUDController visorController = sensoryService != null ? sensoryService.VisorController : null;
            if (visorController == null)
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                visorController = playerContext != null ? playerContext.VisorController : null;
            }

            if (visorController == null)
                return;

            visorController.GlitchPulse(DefaultToxicRoomVisorGlitchDurationSeconds + (intensity * 0.08f));
            visorController.TriggerEnvironmentalDistortion(
                math.saturate(0.24f + (intensity * 0.48f)),
                DefaultToxicRoomVisorDistortionHoldSeconds,
                DefaultToxicRoomVisorDistortionRecovery);
        }

        private uint NextPressureScreechRandom()
        {
            uint state = _pressureScreechRngState != 0u ? _pressureScreechRngState : PressureScreechRngSeed;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _pressureScreechRngState = state != 0u ? state : PressureScreechRngSeed;
            return _pressureScreechRngState;
        }

        private static bool Vector4Equals(Vector4 left, Vector4 right)
        {
            return left.x == right.x &&
                   left.y == right.y &&
                   left.z == right.z &&
                   left.w == right.w;
        }

        private static uint ResolveActiveRoomStatusMask(int roomCount)
        {
            int safeRoomCount = math.clamp(roomCount, 0, RoomCapacity);
            uint roomMask = safeRoomCount > 0 ? ((1u << safeRoomCount) - 1u) : 0u;
            return (roomMask << RoomStatusToxicShift) |
                   (roomMask << RoomStatusFreezingShift) |
                   (roomMask << RoomStatusPressureShift) |
                   (roomMask << RoomStatusFireShift);
        }

        private void ClearRoomStatusMasks()
        {
            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
                return;

            try
            {
                NativeArray<uint> _roomStatusMaskFront = this._roomStatusMaskFront;
                NativeArray<uint> _roomStatusMaskBack = this._roomStatusMaskBack;
                if (_roomStatusMaskFront.IsCreated)
                    _roomStatusMaskFront[0] = 0u;

                if (_roomStatusMaskBack.IsCreated)
                    _roomStatusMaskBack[0] = 0u;
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && math.isfinite(value);
        }

        private void QueueOverheatVoltageFake(int roomIndex, BaseModule roomModule)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity || !_temperatureFront.IsCreated)
                return;

            ResolveSafeTemperatureBounds(out _, out float maximumTemperature);
            float threshold = FiniteOr(overheatBrownoutTemperatureCelsius, DefaultOverheatBrownoutTemperatureCelsius);
            float temperature = GetRoomTemperatureCelsius(roomIndex);
            if (roomModule == null || temperature <= threshold)
            {
                ResetOverheatVisual(roomIndex);
                return;
            }

            float heat01 = math.saturate((temperature - threshold) / math.max(1f, maximumTemperature - threshold));
            float voltage = math.lerp(1f, math.saturate(FiniteOr(overheatMinimumVoltage, DefaultOverheatMinimumVoltage)), math.saturate(heat01));
            _atmosphereRoomModules[roomIndex] = roomModule;
            _pendingOverheatVoltages[roomIndex] = voltage;
            uint roomBit = 1u << roomIndex;
            _pendingOverheatApplyMask |= roomBit;
            _pendingOverheatResetMask &= ~roomBit;
        }

        private void ResetOverheatVisual(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if (((_overheatVisualActiveMask | _pendingOverheatApplyMask) & roomBit) == 0u)
                return;

            _pendingOverheatApplyMask &= ~roomBit;
            _pendingOverheatResetMask |= roomBit;
        }

        private BaseModule ResolveModuleForRoom(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return null;

            BaseModule cachedModule = _atmosphereRoomModules[roomIndex];
            if (IsModuleMappedToRoom(cachedModule, roomIndex))
                return cachedModule;

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule candidate = BaseModule.GetActiveModuleAt(moduleIndex);
                if (!IsModuleMappedToRoom(candidate, roomIndex))
                    continue;

                _atmosphereRoomModules[roomIndex] = candidate;
                return candidate;
            }

            _atmosphereRoomModules[roomIndex] = null;
            return null;
        }

        private bool IsModuleMappedToRoom(BaseModule module, int roomIndex)
        {
            if (module == null || !module.TryGetInteriorAabbBounds(out Vector3 worldCenter, out _))
                return false;

            if (!TryResolveAupFromRuntimeOrigin(worldCenter, out AbsoluteUniversePosition moduleAup))
                return false;

            return ResolveNearestRoomIndex(in moduleAup) == roomIndex;
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying)
                return;

            if (!_coldTickRegistered)
                _coldTickRegistered = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);

            bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            bool lateRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!fixedRegistered || !postFixedRegistered || !lateRegistered)
            {
                if (fixedRegistered)
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                if (postFixedRegistered)
                    GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                if (lateRegistered)
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                return;
            }

            _lateFrameRegistered = lateRegistered;
            _registered = true;
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                if (_lateFrameRegistered)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                    _lateFrameRegistered = false;
                }
                _registered = false;
            }

            if (_coldTickRegistered)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _coldTickRegistered = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    ClearPlayerRuntimeContext(previousService as IPlayerRuntimeContext);
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.PowerGrid:
                    _powerGridService = currentService as IPowerGridService;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    CacheAudioLogSystem(currentService as AudioLogSystem);
                    break;
                case GlobalRegistryServiceSlot.PlayerSensory:
                    _playerSensoryService = currentService as IPlayerSensoryService;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsService:
                    _thermodynamicsService = currentService as IThermodynamicsService;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault previousVault = previousService as IDataVault ?? _dataVault;
                    if (_atmosphereJobRunning)
                        DispatcherJobSwap.TryComplete(ref _atmosphereJobHandle, forceComplete: true);
                    if (_atmosphereJobLockMask != 0ul)
                    {
                        ulong lockMask = _atmosphereJobLockMask;
                        IDataVault guardVault = _atmosphereJobMutationGuardVault;
                        _atmosphereJobLockMask = 0ul;
                        _atmosphereJobMutationGuardVault = null;
                        ReleaseAtmosphereJobBufferLocks(guardVault, lockMask);
                    }

                    if (_atmospherePhaseMutationGuardMask != 0ul)
                    {
                        ulong phaseMask = _atmospherePhaseMutationGuardMask;
                        IDataVault phaseVault = _atmospherePhaseMutationGuardVault;
                        _atmospherePhaseMutationGuardMask = 0ul;
                        _atmospherePhaseMutationGuardVault = null;
                        ReleaseAtmospherePhaseWriteLocks(phaseVault, phaseMask);
                    }

                    _atmosphereJobRunning = false;
                    _scheduledAtmosphereDeltaTime = 0f;
                    ReleaseAtmosphereVaultHandles(previousVault);
                    _dataVault = currentService as IDataVault;
                    _topologySeeded = false;
                    _thermalEmittersSeeded = false;
                    _emergencyVentPipesSeeded = false;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext)
        {
            _playerRuntimeContext = playerContext;
            if (playerContext == null)
                return;

            if (_playerTransform == null)
                _playerTransform = playerContext.PlayerTransform;

            if (_playerCamera == null)
                _playerCamera = playerContext.PlayerCamera;
        }

        private void ClearPlayerRuntimeContext(IPlayerRuntimeContext previousContext)
        {
            if (previousContext == null)
                return;

            if (ReferenceEquals(_playerTransform, previousContext.PlayerTransform))
                _playerTransform = null;

            if (ReferenceEquals(_playerCamera, previousContext.PlayerCamera))
                _playerCamera = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void PrepareNativeStateCold()
        {
            if (IsAtmosphereVaultStateReady())
                return;

            IDataVault vault = ResolveDataVaultCold();
            if (vault == null)
                return;

            EnsureVaultHandle(vault, ref _roomVolumesHandle, SubmarineAtmosphereVaultBufferIds.RoomVolumes, RoomCapacity);
            EnsureVaultHandle(vault, ref _floodVolumesHandle, SubmarineAtmosphereVaultBufferIds.FloodVolumes, RoomCapacity);
            EnsureVaultHandle(vault, ref _o2FrontHandle, SubmarineAtmosphereVaultBufferIds.O2Front, RoomCapacity);
            EnsureVaultHandle(vault, ref _o2BackHandle, SubmarineAtmosphereVaultBufferIds.O2Back, RoomCapacity);
            EnsureVaultHandle(vault, ref _co2FrontHandle, SubmarineAtmosphereVaultBufferIds.Co2Front, RoomCapacity);
            EnsureVaultHandle(vault, ref _co2BackHandle, SubmarineAtmosphereVaultBufferIds.Co2Back, RoomCapacity);
            EnsureVaultHandle(vault, ref _inertFrontHandle, SubmarineAtmosphereVaultBufferIds.InertFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _inertBackHandle, SubmarineAtmosphereVaultBufferIds.InertBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _pressureFrontHandle, SubmarineAtmosphereVaultBufferIds.PressureFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _pressureBackHandle, SubmarineAtmosphereVaultBufferIds.PressureBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _o2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.O2PartialPressureFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _o2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.O2PartialPressureBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _co2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.Co2PartialPressureFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _co2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.Co2PartialPressureBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _n2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.N2PartialPressureFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _n2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.N2PartialPressureBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _gasVolumeFrontHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _gasVolumeBackHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _o2ConsumptionRatesHandle, SubmarineAtmosphereVaultBufferIds.O2ConsumptionRates, RoomCapacity);
            EnsureVaultHandle(vault, ref _co2GenerationRatesHandle, SubmarineAtmosphereVaultBufferIds.Co2GenerationRates, RoomCapacity);
            EnsureVaultHandle(vault, ref _roomPlayerCountsHandle, SubmarineAtmosphereVaultBufferIds.RoomPlayerCounts, RoomCapacity);
            EnsureVaultHandle(vault, ref _temperatureFrontHandle, SubmarineAtmosphereVaultBufferIds.TemperatureFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _temperatureBackHandle, SubmarineAtmosphereVaultBufferIds.TemperatureBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _steamFrontHandle, SubmarineAtmosphereVaultBufferIds.SteamFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _steamBackHandle, SubmarineAtmosphereVaultBufferIds.SteamBack, RoomCapacity);
            EnsureVaultHandle(vault, ref _hydrogenPocketFrontHandle, SubmarineAtmosphereVaultBufferIds.HydrogenPocketFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _oxygenPocketFrontHandle, SubmarineAtmosphereVaultBufferIds.OxygenPocketFront, RoomCapacity);
            EnsureVaultHandle(vault, ref _roomHeatWattsHandle, SubmarineAtmosphereVaultBufferIds.RoomHeatWatts, RoomCapacity);
            EnsureVaultHandle(vault, ref _roomStatusMaskFrontHandle, SubmarineAtmosphereVaultBufferIds.RoomStatusMaskFront, 1);
            EnsureVaultHandle(vault, ref _roomStatusMaskBackHandle, SubmarineAtmosphereVaultBufferIds.RoomStatusMaskBack, 1);
            EnsureVaultHandle(vault, ref _doorPairsHandle, SubmarineAtmosphereVaultBufferIds.DoorPairs, DoorCapacity);
            EnsureVaultHandle(vault, ref _doorSealedHandle, SubmarineAtmosphereVaultBufferIds.DoorSealed, DoorCapacity);
            EnsureVaultHandle(vault, ref _doorSealedPreviousHandle, SubmarineAtmosphereVaultBufferIds.DoorSealedPrevious, DoorCapacity);
            EnsureVaultHandle(vault, ref _telemetryRingHandle, SubmarineAtmosphereVaultBufferIds.TelemetryRing, TelemetryCapacity);
            EnsureVaultHandle(vault, ref _telemetryCursorHandle, SubmarineAtmosphereVaultBufferIds.TelemetryCursor, 1);

            if (IsAtmosphereVaultStateReady() && TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
            {
                try
                {
                    ClearAtmosphereVaultBuffersCold();
                }
                finally
                {
                    ExitAtmosphereWritePhase(ownsWriteLock);
                }
            }
        }

        private IDataVault ResolveDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeArray<T> ResolveVaultArray<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                HasValidVaultHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                    ? buffer
                    : default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveVaultArray<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                HasValidVaultHandle(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadVaultArray<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                HasValidVaultHandle(in handle) &&
                vault.TryReadOnlyHandle(in handle, out buffer) &&
                buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadVaultValue<T>(in VaultGenerationHandle<T> handle, int index, out T value) where T : struct
        {
            value = default;
            if (!TryReadVaultArray(in handle, out NativeArray<T>.ReadOnly buffer) ||
                (uint)index >= (uint)buffer.Length)
            {
                return false;
            }

            value = buffer[index];
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasValidVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.SystemID != 0u && handle.Generation != 0u;
        }

        private static void EnsureVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length) where T : struct
        {
            if (vault == null || length <= 0)
                return;

            if (HasValidVaultHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)SystemID.HabitatAtmosphere)
            {
                return;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                SystemID.HabitatAtmosphere,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool IsAtmosphereVaultStateReady()
        {
            return HasValidVaultHandle(in _roomVolumesHandle) &&
                HasValidVaultHandle(in _floodVolumesHandle) &&
                HasValidVaultHandle(in _o2FrontHandle) &&
                HasValidVaultHandle(in _o2BackHandle) &&
                HasValidVaultHandle(in _co2FrontHandle) &&
                HasValidVaultHandle(in _co2BackHandle) &&
                HasValidVaultHandle(in _inertFrontHandle) &&
                HasValidVaultHandle(in _inertBackHandle) &&
                HasValidVaultHandle(in _pressureFrontHandle) &&
                HasValidVaultHandle(in _pressureBackHandle) &&
                HasValidVaultHandle(in _o2PartialPressureFrontHandle) &&
                HasValidVaultHandle(in _o2PartialPressureBackHandle) &&
                HasValidVaultHandle(in _co2PartialPressureFrontHandle) &&
                HasValidVaultHandle(in _co2PartialPressureBackHandle) &&
                HasValidVaultHandle(in _n2PartialPressureFrontHandle) &&
                HasValidVaultHandle(in _n2PartialPressureBackHandle) &&
                HasValidVaultHandle(in _gasVolumeFrontHandle) &&
                HasValidVaultHandle(in _gasVolumeBackHandle) &&
                HasValidVaultHandle(in _o2ConsumptionRatesHandle) &&
                HasValidVaultHandle(in _co2GenerationRatesHandle) &&
                HasValidVaultHandle(in _roomPlayerCountsHandle) &&
                HasValidVaultHandle(in _temperatureFrontHandle) &&
                HasValidVaultHandle(in _temperatureBackHandle) &&
                HasValidVaultHandle(in _steamFrontHandle) &&
                HasValidVaultHandle(in _steamBackHandle) &&
                HasValidVaultHandle(in _hydrogenPocketFrontHandle) &&
                HasValidVaultHandle(in _oxygenPocketFrontHandle) &&
                HasValidVaultHandle(in _roomHeatWattsHandle) &&
                HasValidVaultHandle(in _roomStatusMaskFrontHandle) &&
                HasValidVaultHandle(in _roomStatusMaskBackHandle) &&
                HasValidVaultHandle(in _doorPairsHandle) &&
                HasValidVaultHandle(in _doorSealedHandle) &&
                HasValidVaultHandle(in _doorSealedPreviousHandle) &&
                IsTelemetryRingReady();
        }

        private bool IsTelemetryRingReady()
        {
            return ValidateAtmosphereTelemetryWriteLanes(_dataVault);
        }

        private void ClearAtmosphereVaultBuffersCold()
        {
            NativeArray<float> _roomVolumes = this._roomVolumes;
            NativeArray<float> _floodVolumes = this._floodVolumes;
            NativeArray<float> _o2Front = this._o2Front;
            NativeArray<float> _o2Back = this._o2Back;
            NativeArray<float> _co2Front = this._co2Front;
            NativeArray<float> _co2Back = this._co2Back;
            NativeArray<float> _inertFront = this._inertFront;
            NativeArray<float> _inertBack = this._inertBack;
            NativeArray<float> _pressureFront = this._pressureFront;
            NativeArray<float> _pressureBack = this._pressureBack;
            NativeArray<float> _o2PartialPressureFront = this._o2PartialPressureFront;
            NativeArray<float> _o2PartialPressureBack = this._o2PartialPressureBack;
            NativeArray<float> _co2PartialPressureFront = this._co2PartialPressureFront;
            NativeArray<float> _co2PartialPressureBack = this._co2PartialPressureBack;
            NativeArray<float> _n2PartialPressureFront = this._n2PartialPressureFront;
            NativeArray<float> _n2PartialPressureBack = this._n2PartialPressureBack;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            NativeArray<float> _gasVolumeBack = this._gasVolumeBack;
            NativeArray<float> _o2ConsumptionRates = this._o2ConsumptionRates;
            NativeArray<float> _co2GenerationRates = this._co2GenerationRates;
            NativeArray<int> _roomPlayerCounts = this._roomPlayerCounts;
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _temperatureBack = this._temperatureBack;
            NativeArray<float> _steamFront = this._steamFront;
            NativeArray<float> _steamBack = this._steamBack;
            NativeArray<float> _hydrogenPocketFront = this._hydrogenPocketFront;
            NativeArray<float> _oxygenPocketFront = this._oxygenPocketFront;
            NativeArray<float> _roomHeatWatts = this._roomHeatWatts;
            NativeArray<uint> _roomStatusMaskFront = this._roomStatusMaskFront;
            NativeArray<uint> _roomStatusMaskBack = this._roomStatusMaskBack;
            NativeArray<int2> _doorPairs = this._doorPairs;
            NativeArray<byte> _doorSealed = this._doorSealed;
            NativeArray<byte> _doorSealedPrevious = this._doorSealedPrevious;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                _roomVolumes[roomIndex] = 0f;
                _floodVolumes[roomIndex] = 0f;
                _o2Front[roomIndex] = 0f;
                _o2Back[roomIndex] = 0f;
                _co2Front[roomIndex] = 0f;
                _co2Back[roomIndex] = 0f;
                _inertFront[roomIndex] = 0f;
                _inertBack[roomIndex] = 0f;
                _pressureFront[roomIndex] = 0f;
                _pressureBack[roomIndex] = 0f;
                _o2PartialPressureFront[roomIndex] = 0f;
                _o2PartialPressureBack[roomIndex] = 0f;
                _co2PartialPressureFront[roomIndex] = 0f;
                _co2PartialPressureBack[roomIndex] = 0f;
                _n2PartialPressureFront[roomIndex] = 0f;
                _n2PartialPressureBack[roomIndex] = 0f;
                _gasVolumeFront[roomIndex] = 0f;
                _gasVolumeBack[roomIndex] = 0f;
                _o2ConsumptionRates[roomIndex] = 0f;
                _co2GenerationRates[roomIndex] = 0f;
                _roomPlayerCounts[roomIndex] = 0;
                _temperatureFront[roomIndex] = 0f;
                _temperatureBack[roomIndex] = 0f;
                _steamFront[roomIndex] = 0f;
                _steamBack[roomIndex] = 0f;
                _hydrogenPocketFront[roomIndex] = 0f;
                _oxygenPocketFront[roomIndex] = 0f;
                _roomHeatWatts[roomIndex] = 0f;
            }

            _roomStatusMaskFront[0] = 0u;
            _roomStatusMaskBack[0] = 0u;
            IDataVault vault = _dataVault;
            if (TryAcquireAtmosphereTelemetryWriteGuard(vault))
            {
                try
                {
                    if (vault.TryResolveHandle(in _telemetryRingHandle, out NativeArray<SubmarineAtmosphereTelemetryEntry> telemetryRing) &&
                        telemetryRing.IsCreated)
                    {
                        for (int i = 0; i < telemetryRing.Length; i++)
                            telemetryRing[i] = default;
                    }

                    if (vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) &&
                        telemetryCursor.IsCreated &&
                        telemetryCursor.Length > 0)
                    {
                        telemetryCursor[0] = 0;
                    }
                }
                finally
                {
                    vault.ReleaseMutationGuard(AtmosphereTelemetryMutationGuardMask);
                }
            }

            _telemetryWriteIndex = 0;
            _atmosphereTickCount = 0u;
            _blackBoxDumped = false;
            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                _doorPairs[doorIndex] = new int2(-1, -1);
                _doorSealed[doorIndex] = 1;
                _doorSealedPrevious[doorIndex] = 1;
            }
        }

        private void SeedTopologyIfNeeded()
        {
            if (_topologySeeded || fluidDynamics == null)
                return;

            NativeArray<float> _roomVolumes = this._roomVolumes;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            NativeArray<float> _pressureFront = this._pressureFront;
            NativeArray<float> _o2Front = this._o2Front;
            NativeArray<float> _co2Front = this._co2Front;
            NativeArray<float> _inertFront = this._inertFront;
            NativeArray<float> _o2PartialPressureFront = this._o2PartialPressureFront;
            NativeArray<float> _co2PartialPressureFront = this._co2PartialPressureFront;
            NativeArray<float> _n2PartialPressureFront = this._n2PartialPressureFront;
            NativeArray<float> _temperatureFront = this._temperatureFront;
            NativeArray<float> _o2ConsumptionRates = this._o2ConsumptionRates;
            NativeArray<float> _co2GenerationRates = this._co2GenerationRates;
            NativeArray<int2> _doorPairs = this._doorPairs;
            NativeArray<byte> _doorSealed = this._doorSealed;
            NativeArray<byte> _doorSealedPrevious = this._doorSealedPrevious;
            int roomCount = RoomCount;
            if (roomCount <= 0)
                return;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);

            ResolveSafeTemperatureBounds(out float minTemperature, out float maxTemperature);
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float seedReferenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minTemperature, maxTemperature);
            float seedReferencePressure = ResolveSafeReferencePressureKPa();
            float seedMaximumPressure = ResolveSafeMaximumPressureKPa();
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomVolumes[roomIndex] = minimumGasVolume;
                    _gasVolumeFront[roomIndex] = minimumGasVolume;
                    _pressureFront[roomIndex] = seedReferencePressure;
                    _o2Front[roomIndex] = 0f;
                    _co2Front[roomIndex] = 0f;
                    _inertFront[roomIndex] = 0f;
                    _o2PartialPressureFront[roomIndex] = 0f;
                    _co2PartialPressureFront[roomIndex] = 0f;
                    _n2PartialPressureFront[roomIndex] = 0f;
                    _temperatureFront[roomIndex] = seedReferenceTemperature;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon && math.isfinite(definition.gasCapacityOverrideCubicMeters)
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                roomVolume = math.max(FiniteOr(roomVolume, minimumGasVolume), minimumGasVolume);

                float oxygenFraction = math.saturate(definition.initialOxygenFraction > Epsilon && math.isfinite(definition.initialOxygenFraction) ? definition.initialOxygenFraction : DefaultInitialOxygenFraction);
                float carbonDioxideFraction = math.saturate(definition.initialCarbonDioxideFraction > 0f && math.isfinite(definition.initialCarbonDioxideFraction) ? definition.initialCarbonDioxideFraction : DefaultInitialCarbonDioxideFraction);
                if (oxygenFraction + carbonDioxideFraction > 0.95f)
                {
                    float scale = 0.95f / math.max(oxygenFraction + carbonDioxideFraction, Epsilon);
                    oxygenFraction *= scale;
                    carbonDioxideFraction *= scale;
                }

                _roomVolumes[roomIndex] = roomVolume;
                _gasVolumeFront[roomIndex] = roomVolume;
                float oxygenUnits = math.saturate(oxygenFraction / math.max(DefaultInitialOxygenFraction, Epsilon)) * tankCapacity;
                float carbonDioxideUnits = math.saturate(carbonDioxideFraction) * tankCapacity;
                float nitrogenUnits = math.saturate(1f - oxygenFraction - carbonDioxideFraction) * tankCapacity;
                _o2Front[roomIndex] = oxygenUnits;
                _co2Front[roomIndex] = carbonDioxideUnits;
                _inertFront[roomIndex] = nitrogenUnits;
                float initialTemperature = definition.initialTemperatureCelsius != 0f && math.isfinite(definition.initialTemperatureCelsius)
                    ? definition.initialTemperatureCelsius
                    : seedReferenceTemperature;
                initialTemperature = math.clamp(initialTemperature, minTemperature, maxTemperature);
                _temperatureFront[roomIndex] = initialTemperature;
                _pressureFront[roomIndex] = ResolveDaltonPressureKPa(
                    oxygenUnits,
                    carbonDioxideUnits,
                    nitrogenUnits,
                    0f,
                    roomVolume,
                    roomVolume,
                    initialTemperature,
                    seedReferencePressure,
                    seedMaximumPressure,
                    seedReferenceTemperature,
                    tankCapacity,
                    out float oxygenPartialPressureKPa,
                    out float carbonDioxidePartialPressureKPa,
                    out float nitrogenPartialPressureKPa);
                _o2PartialPressureFront[roomIndex] = oxygenPartialPressureKPa;
                _co2PartialPressureFront[roomIndex] = carbonDioxidePartialPressureKPa;
                _n2PartialPressureFront[roomIndex] = nitrogenPartialPressureKPa;
                _o2ConsumptionRates[roomIndex] = FiniteNonNegativeOrZero(definition.oxygenConsumptionUnitsPerSecond);
                _co2GenerationRates[roomIndex] = FiniteNonNegativeOrZero(definition.carbonDioxideGenerationUnitsPerSecond);
            }

            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                if (doorIndex < doorCount && fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                {
                    _doorPairs[doorIndex] = new int2(compartmentA, compartmentB);
                    _doorSealed[doorIndex] = isSealed ? (byte)1 : (byte)0;
                    _doorSealedPrevious[doorIndex] = _doorSealed[doorIndex];
                    continue;
                }

                _doorPairs[doorIndex] = new int2(-1, -1);
                _doorSealed[doorIndex] = 1;
                _doorSealedPrevious[doorIndex] = 1;
            }

            _topologyRoomCount = roomCount;
            _topologyDoorCount = doorCount;
            _topologySeeded = true;
        }

        private void InvalidateTopologyIfShapeChanged()
        {
            if (!_topologySeeded || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            if (roomCount == _topologyRoomCount && doorCount == _topologyDoorCount)
                return;

            _topologySeeded = false;
            _thermalEmittersSeeded = false;
            _emergencyVentPipesSeeded = false;
            _emergencyVentRoomMask = 0u;
            _fabricatorHeatEmitterCount = 0;
            _drillHeatEmitterCount = 0;
            _reactorHeatEmitterCount = 0;
            _reactorMeltdownTriggeredMask = 0u;
            _topologyRoomCount = -1;
            _topologyDoorCount = -1;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;
            ClearRoomModuleCache();
            ClearBrownoutRoomModuleCache();
        }

        private void SeedThermalEmittersIfNeeded()
        {
            if (_thermalEmittersSeeded || fluidDynamics == null || !_topologySeeded)
                return;

            _fabricatorHeatEmitterCount = 0;
            _drillHeatEmitterCount = 0;
            _reactorHeatEmitterCount = 0;

            int fabricatorCount = Fabricator.ActiveFabricatorCount;
            for (int i = 0; i < fabricatorCount && _fabricatorHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                Fabricator fabricator = Fabricator.GetActiveFabricatorAt(i);
                if (fabricator == null || !IsComponentOwnedByThisSubmarine(fabricator))
                    continue;

                _fabricatorHeatEmitters[_fabricatorHeatEmitterCount++] = new FabricatorHeatEmitter
                {
                    Fabricator = fabricator,
                    RoomIndex = ResolveThermalEmitterRoomIndex(fabricator)
                };
            }

            int drillCount = DeepDrillModule.ActiveModuleCount;
            for (int i = 0; i < drillCount && _drillHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                DeepDrillModule drill = DeepDrillModule.GetActiveModuleAt(i);
                if (drill == null || !IsComponentOwnedByThisSubmarine(drill))
                    continue;

                _drillHeatEmitters[_drillHeatEmitterCount++] = new DrillHeatEmitter
                {
                    Drill = drill,
                    RoomIndex = ResolveThermalEmitterRoomIndex(drill)
                };
            }

            int reactorCount = BioReactor.ActiveReactorCount;
            for (int i = 0; i < reactorCount && _reactorHeatEmitterCount < HeatEmitterCapacity; i++)
            {
                BioReactor reactor = BioReactor.GetActiveReactorAt(i);
                if (reactor == null || !IsComponentOwnedByThisSubmarine(reactor))
                    continue;

                _reactorHeatEmitters[_reactorHeatEmitterCount++] = new ReactorHeatEmitter
                {
                    Reactor = reactor,
                    RoomIndex = ResolveThermalEmitterRoomIndex(reactor)
                };
            }

            for (int i = _reactorHeatEmitterCount; i < HeatEmitterCapacity; i++)
                _reactorMeltdownTriggeredMask &= ~(1u << i);

            _thermalEmittersSeeded = true;
        }

        private int ResolveThermalEmitterRoomIndex(Component emitter)
        {
            if (emitter == null)
                return -1;

            BaseModule hostModule = ResolveHostModuleForEmitter(emitter);
            if (hostModule != null)
            {
                int roomCount = math.min(RoomCount, RoomCapacity);
                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    if (ReferenceEquals(ResolveModuleForRoom(roomIndex), hostModule))
                        return roomIndex;
                }

                if (hostModule.TryGetInteriorAabbBounds(out Vector3 hostCenter, out _))
                {
                    if (!TryResolveAupFromRuntimeOrigin(hostCenter, out AbsoluteUniversePosition hostAup))
                        return -1;

                    return ResolveNearestRoomIndex(in hostAup);
                }
            }

            return ResolveSubmarineCenterRoomIndex();
        }

        private int ResolveSubmarineCenterRoomIndex()
        {
            if (_submarineBody == null)
                return -1;

            if (!TryResolveAupFromRuntimeOrigin(_submarineBody.worldCenterOfMass, out AbsoluteUniversePosition submarineCenterAup))
                return -1;

            return ResolveNearestRoomIndex(in submarineCenterAup);
        }

        private bool IsComponentOwnedByThisSubmarine(Component component)
        {
            if (component == null)
                return false;

            Transform root = _cachedTransform;
            Transform componentTransform = component.transform;
            return root != null &&
                   componentTransform != null &&
                   (ReferenceEquals(componentTransform, root) || componentTransform.IsChildOf(root));
        }

        private static BaseModule ResolveHostModuleForEmitter(Component emitter)
        {
            if (emitter == null)
                return null;

            Transform emitterTransform = emitter.transform;
            if (emitterTransform == null)
                return null;

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(moduleIndex);
                if (module == null)
                    continue;

                Transform moduleTransform = module.transform;
                if (moduleTransform != null &&
                    (ReferenceEquals(emitterTransform, moduleTransform) || emitterTransform.IsChildOf(moduleTransform)))
                {
                    return module;
                }
            }

            return null;
        }

        private void SyncFluidSnapshot()
        {
            if (fluidDynamics == null)
                return;

            NativeArray<int> _roomPlayerCounts = this._roomPlayerCounts;
            NativeArray<float> _roomVolumes = this._roomVolumes;
            NativeArray<float> _floodVolumes = this._floodVolumes;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            NativeArray<float> _o2ConsumptionRates = this._o2ConsumptionRates;
            NativeArray<float> _co2GenerationRates = this._co2GenerationRates;
            NativeArray<float> _roomHeatWatts = this._roomHeatWatts;
            NativeArray<int2> _doorPairs = this._doorPairs;
            NativeArray<byte> _doorSealed = this._doorSealed;
            int roomCount = RoomCount;
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (_roomPlayerCounts.IsCreated)
                    _roomPlayerCounts[roomIndex] = 0;

                if (roomIndex >= roomCount)
                {
                    _roomVolumes[roomIndex] = minimumGasVolume;
                    _floodVolumes[roomIndex] = 0f;
                    _gasVolumeFront[roomIndex] = minimumGasVolume;
                    _o2ConsumptionRates[roomIndex] = 0f;
                    _co2GenerationRates[roomIndex] = 0f;
                    _roomHeatWatts[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                float roomVolume = definition.gasCapacityOverrideCubicMeters > Epsilon && math.isfinite(definition.gasCapacityOverrideCubicMeters)
                    ? definition.gasCapacityOverrideCubicMeters
                    : fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex);
                _roomVolumes[roomIndex] = math.max(FiniteOr(roomVolume, minimumGasVolume), minimumGasVolume);
                _floodVolumes[roomIndex] = math.clamp(
                    FiniteNonNegativeOrZero(fluidDynamics.GetCompartmentFloodVolumeCubicMeters(roomIndex)),
                    0f,
                    _roomVolumes[roomIndex] - Epsilon);
                if (_gasVolumeFront.IsCreated)
                    _gasVolumeFront[roomIndex] = math.max(minimumGasVolume, _roomVolumes[roomIndex] - _floodVolumes[roomIndex]);
                float oxygenConsumptionRate = math.max(
                    FiniteNonNegativeOrZero(definition.oxygenConsumptionUnitsPerSecond),
                    FiniteNonNegativeOrZero(playerOxygenConsumptionPercentPerSecond));
                ApplyBrownoutOccupiedRoomOxygenDrain(roomIndex, ref oxygenConsumptionRate);
                _o2ConsumptionRates[roomIndex] = oxygenConsumptionRate;
                _co2GenerationRates[roomIndex] = FiniteNonNegativeOrZero(definition.carbonDioxideGenerationUnitsPerSecond);
            }

            if (_roomPlayerCounts.IsCreated && TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                int occupiedRoomIndex = ResolveNearestRoomIndex(in playerAup);
                if (occupiedRoomIndex >= 0 && occupiedRoomIndex < roomCount)
                    _roomPlayerCounts[occupiedRoomIndex] = 1;
            }

            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                if (doorIndex < doorCount && fluidDynamics.TryGetBulkheadDefinition(doorIndex, out int compartmentA, out int compartmentB, out bool isSealed))
                {
                    _doorPairs[doorIndex] = new int2(compartmentA, compartmentB);
                    _doorSealed[doorIndex] = isSealed ? (byte)1 : (byte)0;
                    continue;
                }

                _doorPairs[doorIndex] = new int2(-1, -1);
                _doorSealed[doorIndex] = 1;
            }
        }

        private void ApplyBrownoutOccupiedRoomOxygenDrain(int roomIndex, ref float oxygenConsumptionRate)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            if (!TryResolveBrownoutOccupiedModuleForRoom(roomIndex, out _))
                return;

            oxygenConsumptionRate = ResolveBrownoutOxygenConsumptionRate(
                oxygenConsumptionRate,
                brownoutOccupiedRoomOxygenConsumptionUnitsPerSecond);
        }

        private bool TryResolveBrownoutOccupiedModuleForRoom(int roomIndex, out BaseModule module)
        {
            module = null;
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return false;

            BaseModule cachedModule = _brownoutRoomModules[roomIndex];
            if (IsBrownoutOccupiedModuleCandidate(cachedModule, roomIndex))
            {
                module = cachedModule;
                return true;
            }

            int moduleCount = BaseModule.ActiveModuleCount;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                BaseModule candidate = BaseModule.GetActiveModuleAt(moduleIndex);
                if (!IsBrownoutOccupiedModuleCandidate(candidate, roomIndex))
                    continue;

                _brownoutRoomModules[roomIndex] = candidate;
                module = candidate;
                return true;
            }

            _brownoutRoomModules[roomIndex] = null;
            return false;
        }

        private bool IsBrownoutOccupiedModuleCandidate(BaseModule module, int roomIndex)
        {
            if (module == null)
                return false;

            if (!IsModuleMappedToRoom(module, roomIndex))
                return false;

            return ShouldSiphonOxygenDuringBrownout(
                module.CachedPowerSupplyRatio,
                brownoutOxygenSupplyRatioThreshold,
                IsPlayerInsideModuleAabb(module));
        }

        internal static bool ShouldSiphonOxygenDuringBrownout(float supplyRatio, float threshold, bool playerInsideModule)
        {
            return playerInsideModule && math.saturate(FiniteOr(supplyRatio, 1f)) < math.saturate(FiniteOr(threshold, DefaultBrownoutOxygenSupplyRatioThreshold));
        }

        internal static float ResolveBrownoutOxygenConsumptionRate(float currentConsumptionRate, float brownoutDrainRate)
        {
            return math.max(FiniteNonNegativeOrZero(currentConsumptionRate), FiniteNonNegativeOrZero(brownoutDrainRate));
        }

        private bool IsPlayerInsideModuleAabb(BaseModule module)
        {
            if (module == null)
                return false;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup) ||
                !module.TryGetInteriorAabbBounds(out Vector3 worldCenter, out Vector3 halfExtents))
                return false;

            if (!TryResolveRuntimeDeltaFromCurrentOrigin(in playerAup, out float3 playerRuntime))
                return false;

            Vector3 delta = new Vector3(playerRuntime.x, playerRuntime.y, playerRuntime.z) - worldCenter;
            return math.abs(delta.x) <= FiniteNonNegativeOrZero(halfExtents.x) &&
                   math.abs(delta.y) <= FiniteNonNegativeOrZero(halfExtents.y) &&
                   math.abs(delta.z) <= FiniteNonNegativeOrZero(halfExtents.z);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null)
                return false;

            _playerTransform = playerContext.PlayerTransform;
            _playerCamera = playerContext.PlayerCamera;
            if (!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite())
            {
                return false;
            }

            playerAup = movementState.PredictedAup;
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        private static bool TryResolveRuntimeDeltaFromCurrentOrigin(in AbsoluteUniversePosition targetAup, out float3 runtimePosition)
        {
            runtimePosition = float3.zero;
            if (!AbsoluteUniversePosition.IsFinite(in targetAup))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            double3 deltaAup = AbsoluteUniversePosition.DeltaMetersClamped(in targetAup, in originAup);
            if (!math.all(math.isfinite(deltaAup)))
                return false;

            const double maxRuntimeDeltaMeters = 1048576.0d;
            deltaAup = math.clamp(
                deltaAup,
                new double3(-maxRuntimeDeltaMeters, -maxRuntimeDeltaMeters, -maxRuntimeDeltaMeters),
                new double3(maxRuntimeDeltaMeters, maxRuntimeDeltaMeters, maxRuntimeDeltaMeters));
            runtimePosition = new float3((float)deltaAup.x, (float)deltaAup.y, (float)deltaAup.z);
            return math.all(math.isfinite(runtimePosition));
        }

        private void AccumulateRoomHeatSources()
        {
            NativeArray<float> _roomHeatWatts = this._roomHeatWatts;
            if (!_roomHeatWatts.IsCreated || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    _roomHeatWatts[roomIndex] = 0f;
                    continue;
                }

                RoomDefinition definition = roomIndex < rooms.Length ? rooms[roomIndex] : default;
                _roomHeatWatts[roomIndex] = FiniteNonNegativeOrZero(definition.passiveHeatWatts);
            }

            for (int i = 0; i < _fabricatorHeatEmitterCount; i++)
            {
                FabricatorHeatEmitter emitter = _fabricatorHeatEmitters[i];
                if (emitter.Fabricator == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                if (emitter.Fabricator.IsCrafting)
                    _roomHeatWatts[emitter.RoomIndex] += math.abs(FiniteOr(emitter.Fabricator.PowerRating, 0f)) * FiniteNonNegativeOrZero(fabricatorHeatWattsScale);
            }

            for (int i = 0; i < _drillHeatEmitterCount; i++)
            {
                DrillHeatEmitter emitter = _drillHeatEmitters[i];
                if (emitter.Drill == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += math.abs(FiniteOr(emitter.Drill.PowerRating, 0f)) * FiniteNonNegativeOrZero(drillHeatWattsScale);
            }

            for (int i = 0; i < _reactorHeatEmitterCount; i++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[i];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= roomCount)
                    continue;

                _roomHeatWatts[emitter.RoomIndex] += FiniteNonNegativeOrZero(emitter.Reactor.PowerRating) * FiniteNonNegativeOrZero(reactorHeatWattsScale);
            }
        }

        private void EvaluateReactorMeltdowns()
        {
            if (_submarineBody == null || fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
                return;

            ResolveSafeTemperatureBounds(out _, out float maximumTemperature);
            float thresholdTemperature = math.min(maximumTemperature, math.max(DefaultReactorMeltdownTemperatureCelsius, FiniteOr(reactorMeltdownTemperatureCelsius, DefaultReactorMeltdownTemperatureCelsius)));
            float minimumImpulse = math.max(1f, FiniteOr(reactorMeltdownMinimumImpulseNewtonSeconds, DefaultReactorMeltdownMinimumImpulseNewtonSeconds));
            float maximumImpulse = math.max(minimumImpulse, FiniteOr(reactorMeltdownMaximumImpulseNewtonSeconds, DefaultReactorMeltdownMaximumImpulseNewtonSeconds));
            float upwardBias = math.saturate(FiniteOr(reactorMeltdownUpwardBias, DefaultReactorMeltdownUpwardBias));
            float impulseDuration = math.max(0.001f, FiniteOr(reactorMeltdownImpulseDurationSeconds, DefaultReactorMeltdownImpulseDurationSeconds));
            float impulsePerWattSecond = FiniteNonNegativeOrZero(reactorMeltdownImpulsePerWattSecond);
            float floodAmplification = math.max(1f, FiniteOr(reactorMeltdownFloodAmplification, DefaultReactorMeltdownFloodAmplification));

            for (int emitterIndex = 0; emitterIndex < _reactorHeatEmitterCount; emitterIndex++)
            {
                ReactorHeatEmitter emitter = _reactorHeatEmitters[emitterIndex];
                if (emitter.Reactor == null || emitter.RoomIndex < 0 || emitter.RoomIndex >= RoomCount)
                    continue;

                uint emitterBit = 1u << emitterIndex;
                if ((_reactorMeltdownTriggeredMask & emitterBit) != 0u)
                    continue;

                float roomTemperature = GetRoomTemperatureCelsius(emitter.RoomIndex);
                if (roomTemperature < thresholdTemperature)
                    continue;

                BaseModule roomModule = ResolveModuleForRoom(emitter.RoomIndex);
                Vector3 reactorWorldPosition = TryResolveRoomHazardBounds(emitter.RoomIndex, roomModule, out Vector3 roomWorldCenter, out _)
                    ? roomWorldCenter
                    : _submarineBody.worldCenterOfMass;
                Vector3 centerDirection = _submarineBody.worldCenterOfMass - reactorWorldPosition;
                Vector3 forceDirection = ResolveFakeBlastDirection(centerDirection, upwardBias);

                float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[emitter.RoomIndex], minimumGasVolume));
                float floodRatio = math.saturate(FiniteNonNegativeOrZero(_floodVolumes[emitter.RoomIndex]) / roomVolume);
                float floodMultiplier = math.lerp(1f, floodAmplification, floodRatio);
                float temperatureOvershoot = math.max(0f, roomTemperature - thresholdTemperature);
                float thermalScale = 1f + math.saturate(temperatureOvershoot / math.max(1f, thresholdTemperature));
                float baseImpulseMagnitude = math.max(
                    minimumImpulse,
                    FiniteNonNegativeOrZero(emitter.Reactor.PowerRating) * impulsePerWattSecond * impulseDuration);
                float impulseMagnitude = math.clamp(
                    baseImpulseMagnitude * floodMultiplier * thermalScale,
                    minimumImpulse,
                    maximumImpulse);

                _physicsService?.QueueForceAtPosition(
                    _submarineBody,
                    forceDirection * impulseMagnitude,
                    reactorWorldPosition,
                    ForceMode.Impulse);
                _reactorMeltdownTriggeredMask |= emitterBit;
            }
        }

        private void PublishDoorOpeningPressureEvents()
        {
            NativeArray<float> _pressureFront = this._pressureFront;
            NativeArray<byte> _doorSealedPrevious = this._doorSealedPrevious;
            NativeArray<byte> _doorSealed = this._doorSealed;
            NativeArray<int2> _doorPairs = this._doorPairs;
            if (!_topologySeeded || !_pressureFront.IsCreated || !_doorSealedPrevious.IsCreated || fluidDynamics == null)
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float thresholdKPa = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure));
            for (int doorIndex = 0; doorIndex < DoorCapacity; doorIndex++)
            {
                byte currentState = doorIndex < doorCount ? _doorSealed[doorIndex] : (byte)1;
                byte previousState = _doorSealedPrevious[doorIndex];
                _doorSealedPrevious[doorIndex] = currentState;

                if (doorIndex >= doorCount || previousState == 0 || currentState != 0)
                    continue;

                int2 pair = _doorPairs[doorIndex];
                if (pair.x < 0 || pair.x >= roomCount || pair.y < 0 || pair.y >= roomCount)
                    continue;

                float pressureA = FiniteClampedOr(_pressureFront[pair.x], referencePressure, 0f, maximumPressure);
                float pressureB = FiniteClampedOr(_pressureFront[pair.y], referencePressure, 0f, maximumPressure);
                if (math.abs(pressureA - pressureB) <= Epsilon)
                    continue;

                if (math.max(pressureA, pressureB) < thresholdKPa)
                    continue;

                HighPressureEvent pressureEvent = new HighPressureEvent(
                    doorIndex,
                    pair.x,
                    pair.y,
                    pressureA,
                    pressureB,
                    ResolveDoorRuntimePosition(pair.x, pair.y));
                if (!HighPressureEvents.TryNotify(in pressureEvent))
                    IncrementDroppedSignalCount();
                EmitPressureBlowout(doorIndex, pair.x, pair.y, pressureA, pressureB, pressureEvent.RuntimePosition);
            }
        }

        private Vector3 ResolveDoorRuntimePosition(int roomA, int roomB)
        {
            if (fluidDynamics == null)
                return ResolveSubmarineFallbackRuntimePosition();

            Vector3 centroidA = fluidDynamics.GetCompartmentCentroid(roomA);
            Vector3 centroidB = fluidDynamics.GetCompartmentCentroid(roomB);
            Vector3 localMidpoint = (centroidA + centroidB) * 0.5f;
            return _cachedTransform != null ? _cachedTransform.TransformPoint(localMidpoint) : localMidpoint;
        }

        private void EmitPressureBlowout(int doorIndex, int roomA, int roomB, float pressureA, float pressureB, Vector3 runtimePosition)
        {
            if (fluidDynamics == null)
                return;

            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float highPressureKPa = math.max(
                FiniteClampedOr(pressureA, referencePressure, 0f, maximumPressure),
                FiniteClampedOr(pressureB, referencePressure, 0f, maximumPressure));
            float lowPressureKPa = math.min(
                FiniteClampedOr(pressureA, referencePressure, 0f, maximumPressure),
                FiniteClampedOr(pressureB, referencePressure, 0f, maximumPressure));
            float pressureDeltaKPa = highPressureKPa - lowPressureKPa;
            if (pressureDeltaKPa <= Epsilon)
                return;

            Vector3 direction = ResolveDoorFlowDirection(roomA, roomB, pressureA, pressureB);
            if (direction.sqrMagnitude <= Epsilon)
                return;

            float doorAreaSquareMeters = math.max(Epsilon, FiniteOr(fluidDynamics.GetBulkheadDoorAreaSquareMeters(doorIndex), Epsilon));
            float forceMagnitudeNewtons = pressureDeltaKPa * 1000f * doorAreaSquareMeters;
            float impulseMagnitude = math.min(
                forceMagnitudeNewtons * math.max(0.001f, FiniteOr(pressureImpulseDurationSeconds, DefaultPressureImpulseDurationSeconds)),
                math.max(1f, FiniteOr(maximumPressureImpulseNewtonSeconds, DefaultMaximumPressureImpulseNewtonSeconds)));

            PressureImpulseEvent pressureImpulseEvent = new PressureImpulseEvent(
                doorIndex,
                runtimePosition,
                direction,
                doorAreaSquareMeters,
                highPressureKPa,
                lowPressureKPa,
                direction * forceMagnitudeNewtons,
                direction * impulseMagnitude,
                math.max(0.25f, FiniteOr(pressureImpulseRadiusMeters, DefaultPressureImpulseRadiusMeters)));
            PublishPressureImpulseSignal(in pressureImpulseEvent);
            ApplyPressureBlowoutImpulse(in pressureImpulseEvent);
        }

        private void PublishPressureImpulseSignal(in PressureImpulseEvent pressureImpulseEvent)
        {
            PhysicsEventPayload payload = new PhysicsEventPayload
            {
                RuntimePosition = pressureImpulseEvent.RuntimePosition,
                Direction = pressureImpulseEvent.Direction,
                ForceVector = pressureImpulseEvent.ForceVectorNewtons,
                ImpulseVector = pressureImpulseEvent.ImpulseVectorNewtonSeconds,
                RadiusMeters = pressureImpulseEvent.InfluenceRadiusMeters,
                Scalar0 = pressureImpulseEvent.DoorAreaSquareMeters,
                Scalar1 = pressureImpulseEvent.HighPressureKPa,
                Scalar2 = pressureImpulseEvent.LowPressureKPa,
                PrimaryId = pressureImpulseEvent.DoorIndex,
                DataHash = 0u,
                StatusBits = 0u,
                EventType = (ushort)PhysicsEventType.PressureImpulse,
                Reserved = 0
            };
            SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref _droppedSignalCount);
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;
        }

        private Vector3 ResolveDoorFlowDirection(int roomA, int roomB, float pressureA, float pressureB)
        {
            if (fluidDynamics == null)
                return _cachedTransform != null ? _cachedTransform.forward : Vector3.forward;

            Vector3 centroidA = fluidDynamics.GetCompartmentCentroid(roomA);
            Vector3 centroidB = fluidDynamics.GetCompartmentCentroid(roomB);
            Vector3 localDirection = pressureA >= pressureB ? (centroidB - centroidA) : (centroidA - centroidB);
            Vector3 worldDirection = _cachedTransform != null ? _cachedTransform.TransformDirection(localDirection) : localDirection;
            return ResolveFakeAxisDirection(worldDirection, _cachedTransform != null ? _cachedTransform.forward : Vector3.forward);
        }

        private void ApplyPressureBlowoutImpulse(in PressureImpulseEvent pressureImpulseEvent)
        {
            float radius = math.max(0.25f, FiniteOr(pressureImpulseEvent.InfluenceRadiusMeters, DefaultPressureImpulseRadiusMeters));
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                pressureImpulseEvent.RuntimePosition,
                radius,
                kindMask,
                _pressureImpulseContacts);
            if (hitCount <= 0)
                return;

            float radiusSq = math.max(Epsilon, radius * radius);
            float falloffBias = math.saturate(FiniteOr(pressureImpulseFalloffExponent, DefaultPressureImpulseFalloffExponent) - 1f) * 0.1f;
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _pressureImpulseContacts[hitIndex];
                _pressureImpulseContacts[hitIndex] = default;
                if (!LayerMatchesMask(hit.Layer, pressureImpulseLayers))
                    continue;

                Rigidbody body = hit.Rigidbody;
                if (body == null || body.isKinematic || body == _submarineBody)
                    continue;

                Vector3 toDoor = pressureImpulseEvent.RuntimePosition - body.worldCenterOfMass;
                float normalizedDistance = math.saturate(1f - (toDoor.sqrMagnitude / radiusSq));
                if (normalizedDistance <= 0f)
                    continue;

                float falloff = math.saturate(normalizedDistance - falloffBias);
                if (falloff <= Epsilon)
                    continue;

                bool duplicate = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (_pressureImpulseBodyBuffer[uniqueIndex] != body)
                        continue;

                    _pressureImpulseFalloffBuffer[uniqueIndex] = math.max(_pressureImpulseFalloffBuffer[uniqueIndex], falloff);
                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _pressureImpulseBodyBuffer[uniqueBodyCount] = body;
                _pressureImpulseFalloffBuffer[uniqueBodyCount] = falloff;
                uniqueBodyCount++;
                if (uniqueBodyCount >= PressureImpulseOverlapCapacity)
                    break;
            }

            float impulseMagnitude = math.min(
                FiniteNonNegativeOrZero(pressureImpulseEvent.PressureDeltaKPa) * 1000f * math.max(Epsilon, FiniteOr(pressureImpulseEvent.DoorAreaSquareMeters, Epsilon)) * math.max(0.001f, FiniteOr(pressureImpulseDurationSeconds, DefaultPressureImpulseDurationSeconds)),
                math.max(1f, FiniteOr(maximumPressureImpulseNewtonSeconds, DefaultMaximumPressureImpulseNewtonSeconds)));
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _pressureImpulseBodyBuffer[bodyIndex];
                _pressureImpulseBodyBuffer[bodyIndex] = null;
                float falloff = _pressureImpulseFalloffBuffer[bodyIndex];
                _pressureImpulseFalloffBuffer[bodyIndex] = 0f;
                if (body == null)
                    continue;

                if (falloff <= Epsilon)
                    continue;

                Vector3 direction = pressureImpulseEvent.Direction;
                Vector3 impulse = direction * (impulseMagnitude * falloff);
                _physicsService?.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static bool LayerMatchesMask(int layer, LayerMask mask)
        {
            return layer >= 0 && layer < 32 && (mask.value & (1 << layer)) != 0;
        }

        private static Vector3 ResolveFakeBlastDirection(Vector3 centerDirection, float upwardBias)
        {
            float x = FiniteOr(centerDirection.x, 0f);
            float z = FiniteOr(centerDirection.z, 0f);
            float lateralXSq = x * x;
            float lateralZSq = z * z;
            if (lateralXSq + lateralZSq <= 0.000001f)
                return Vector3.up;

            bool highArc = FiniteOr(upwardBias, 0f) >= 0.5f;
            float lateral = highArc ? 0.6f : 0.94f;
            float vertical = highArc ? 0.8f : 0.35f;
            if (lateralXSq >= lateralZSq)
                return new Vector3(x >= 0f ? lateral : -lateral, vertical, 0f);

            return new Vector3(0f, vertical, z >= 0f ? lateral : -lateral);
        }

        private static Vector3 ResolveFakeAxisDirection(Vector3 value, Vector3 fallback)
        {
            Vector3 safeValue = new Vector3(FiniteOr(value.x, 0f), FiniteOr(value.y, 0f), FiniteOr(value.z, 0f));
            float lengthSq = safeValue.sqrMagnitude;
            if (lengthSq <= 0.000001f)
                return fallback;

            float absX = math.abs(safeValue.x);
            float absY = math.abs(safeValue.y);
            float absZ = math.abs(safeValue.z);
            if (absX >= absY && absX >= absZ)
                return safeValue.x >= 0f ? Vector3.right : Vector3.left;

            if (absY >= absZ)
                return safeValue.y >= 0f ? Vector3.up : Vector3.down;

            return safeValue.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private bool TryEnterAtmosphereWritePhase(out bool ownsWriteLock)
        {
            ownsWriteLock = false;
            if (_atmospherePhaseMutationGuardMask != 0ul)
                return true;

            if (!TryAcquireAtmospherePhaseWriteLocks())
            {
                RecordAtmosphereFailure(4);
                return false;
            }

            ownsWriteLock = true;
            return true;
        }

        private void ExitAtmosphereWritePhase(bool ownsWriteLock)
        {
            if (ownsWriteLock)
                ReleaseAtmospherePhaseWriteLocks();
        }

        private bool TryAcquireAtmospherePhaseWriteLocks()
        {
            if (_atmospherePhaseMutationGuardMask != 0ul)
                return false;

            IDataVault vault = _dataVault;
            ulong mutationGuardMask = AtmospherePhaseMutationGuardMask;
            bool success = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                mutationGuardMask == 0ul ||
                !ValidateAtmospherePhaseWriteLanes(vault) ||
                !vault.TryAcquireMutationGuard(mutationGuardMask))
            {
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !ValidateAtmospherePhaseWriteLanes(vault))
                    return false;

                _atmospherePhaseMutationGuardMask = mutationGuardMask;
                _atmospherePhaseMutationGuardVault = vault;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    vault.ReleaseMutationGuard(mutationGuardMask);
            }
        }

        private bool ValidateAtmospherePhaseWriteLanes(IDataVault vault)
        {
            return ValidateAtmospherePhaseWriteLane(vault, in _roomVolumesHandle, SubmarineAtmosphereVaultBufferIds.RoomVolumes, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _floodVolumesHandle, SubmarineAtmosphereVaultBufferIds.FloodVolumes, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2FrontHandle, SubmarineAtmosphereVaultBufferIds.O2Front, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2BackHandle, SubmarineAtmosphereVaultBufferIds.O2Back, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2FrontHandle, SubmarineAtmosphereVaultBufferIds.Co2Front, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2BackHandle, SubmarineAtmosphereVaultBufferIds.Co2Back, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _inertFrontHandle, SubmarineAtmosphereVaultBufferIds.InertFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _inertBackHandle, SubmarineAtmosphereVaultBufferIds.InertBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _pressureFrontHandle, SubmarineAtmosphereVaultBufferIds.PressureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _pressureBackHandle, SubmarineAtmosphereVaultBufferIds.PressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.O2PartialPressureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.O2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.Co2PartialPressureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.Co2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _n2PartialPressureFrontHandle, SubmarineAtmosphereVaultBufferIds.N2PartialPressureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _n2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.N2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _gasVolumeFrontHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _gasVolumeBackHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2ConsumptionRatesHandle, SubmarineAtmosphereVaultBufferIds.O2ConsumptionRates, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2GenerationRatesHandle, SubmarineAtmosphereVaultBufferIds.Co2GenerationRates, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomPlayerCountsHandle, SubmarineAtmosphereVaultBufferIds.RoomPlayerCounts, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _temperatureFrontHandle, SubmarineAtmosphereVaultBufferIds.TemperatureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _temperatureBackHandle, SubmarineAtmosphereVaultBufferIds.TemperatureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _steamFrontHandle, SubmarineAtmosphereVaultBufferIds.SteamFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _steamBackHandle, SubmarineAtmosphereVaultBufferIds.SteamBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _hydrogenPocketFrontHandle, SubmarineAtmosphereVaultBufferIds.HydrogenPocketFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _oxygenPocketFrontHandle, SubmarineAtmosphereVaultBufferIds.OxygenPocketFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomHeatWattsHandle, SubmarineAtmosphereVaultBufferIds.RoomHeatWatts, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomStatusMaskFrontHandle, SubmarineAtmosphereVaultBufferIds.RoomStatusMaskFront, 1) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomStatusMaskBackHandle, SubmarineAtmosphereVaultBufferIds.RoomStatusMaskBack, 1) &&
                ValidateAtmospherePhaseWriteLane(vault, in _doorPairsHandle, SubmarineAtmosphereVaultBufferIds.DoorPairs, DoorCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _doorSealedHandle, SubmarineAtmosphereVaultBufferIds.DoorSealed, DoorCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _doorSealedPreviousHandle, SubmarineAtmosphereVaultBufferIds.DoorSealedPrevious, DoorCapacity);
        }

        private static bool ValidateAtmospherePhaseWriteLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                !vault.IsCompactionFenceActive &&
                HasValidVaultHandle(in handle) &&
                handle.BufferID == (uint)bufferId &&
                handle.SystemID == (uint)SystemID.HabitatAtmosphere &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                !vault.IsCompactionFenceActive &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong AtmosphereBufferGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ReleaseAtmospherePhaseWriteLocks()
        {
            ulong mask = _atmospherePhaseMutationGuardMask;
            if (mask == 0ul)
                return;

            IDataVault vault = _atmospherePhaseMutationGuardVault;
            _atmospherePhaseMutationGuardMask = 0ul;
            _atmospherePhaseMutationGuardVault = null;
            ReleaseAtmospherePhaseWriteLocks(vault, mask);
        }

        private void ReleaseAtmospherePhaseWriteLocks(IDataVault vault, ulong mask)
        {
            if (vault == null || mask == 0ul)
                return;

            vault.ReleaseMutationGuard(mask);
        }

        private bool TryLockAtmosphereJobBuffers()
        {
            if (_atmosphereJobLockMask != 0ul)
                return false;

            IDataVault vault = _dataVault;
            ulong mask = AtmosphereJobMutationGuardMask;
            bool success = false;
            bool recordPostGuardFailure = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                mask == 0ul ||
                !ValidateAtmosphereJobWriteLanes(vault) ||
                !vault.TryAcquireMutationGuard(mask))
            {
                RecordAtmosphereFailure(3);
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !ValidateAtmosphereJobWriteLanes(vault))
                {
                    recordPostGuardFailure = true;
                    return false;
                }

                _atmosphereJobLockMask = mask;
                _atmosphereJobMutationGuardVault = vault;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    vault.ReleaseMutationGuard(mask);
                    if (recordPostGuardFailure)
                        RecordAtmosphereFailure(3);
                }
            }
        }

        private bool TryAcquireAtmosphereTelemetryWriteGuard(IDataVault vault)
        {
            ulong mask = AtmosphereTelemetryMutationGuardMask;
            bool success = false;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                mask == 0ul ||
                !ValidateAtmosphereTelemetryWriteLanes(vault) ||
                !vault.TryAcquireMutationGuard(mask))
            {
                return false;
            }

            try
            {
                if (vault.IsCompactionFenceActive || !ValidateAtmosphereTelemetryWriteLanes(vault))
                    return false;

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    vault.ReleaseMutationGuard(mask);
            }
        }

        private bool ValidateAtmosphereTelemetryWriteLanes(IDataVault vault)
        {
            return ValidateAtmospherePhaseWriteLane(vault, in _telemetryRingHandle, SubmarineAtmosphereVaultBufferIds.TelemetryRing, TelemetryCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _telemetryCursorHandle, SubmarineAtmosphereVaultBufferIds.TelemetryCursor, 1);
        }

        private bool ValidateAtmosphereJobWriteLanes(IDataVault vault)
        {
            return ValidateAtmospherePhaseWriteLane(vault, in _o2FrontHandle, SubmarineAtmosphereVaultBufferIds.O2Front, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2FrontHandle, SubmarineAtmosphereVaultBufferIds.Co2Front, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _inertFrontHandle, SubmarineAtmosphereVaultBufferIds.InertFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _floodVolumesHandle, SubmarineAtmosphereVaultBufferIds.FloodVolumes, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomVolumesHandle, SubmarineAtmosphereVaultBufferIds.RoomVolumes, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _pressureFrontHandle, SubmarineAtmosphereVaultBufferIds.PressureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _gasVolumeFrontHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2ConsumptionRatesHandle, SubmarineAtmosphereVaultBufferIds.O2ConsumptionRates, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2GenerationRatesHandle, SubmarineAtmosphereVaultBufferIds.Co2GenerationRates, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomPlayerCountsHandle, SubmarineAtmosphereVaultBufferIds.RoomPlayerCounts, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _temperatureFrontHandle, SubmarineAtmosphereVaultBufferIds.TemperatureFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomHeatWattsHandle, SubmarineAtmosphereVaultBufferIds.RoomHeatWatts, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _steamFrontHandle, SubmarineAtmosphereVaultBufferIds.SteamFront, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _doorPairsHandle, SubmarineAtmosphereVaultBufferIds.DoorPairs, DoorCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _doorSealedHandle, SubmarineAtmosphereVaultBufferIds.DoorSealed, DoorCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2BackHandle, SubmarineAtmosphereVaultBufferIds.O2Back, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2BackHandle, SubmarineAtmosphereVaultBufferIds.Co2Back, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _inertBackHandle, SubmarineAtmosphereVaultBufferIds.InertBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _pressureBackHandle, SubmarineAtmosphereVaultBufferIds.PressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _gasVolumeBackHandle, SubmarineAtmosphereVaultBufferIds.GasVolumeBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _temperatureBackHandle, SubmarineAtmosphereVaultBufferIds.TemperatureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _steamBackHandle, SubmarineAtmosphereVaultBufferIds.SteamBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _o2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.O2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _co2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.Co2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _n2PartialPressureBackHandle, SubmarineAtmosphereVaultBufferIds.N2PartialPressureBack, RoomCapacity) &&
                ValidateAtmospherePhaseWriteLane(vault, in _roomStatusMaskBackHandle, SubmarineAtmosphereVaultBufferIds.RoomStatusMaskBack, 1);
        }

        private void ReleaseAtmosphereJobBufferLocks()
        {
            ulong mask = _atmosphereJobLockMask;
            if (mask == 0ul)
                return;

            IDataVault vault = _atmosphereJobMutationGuardVault;
            _atmosphereJobLockMask = 0ul;
            _atmosphereJobMutationGuardVault = null;
            ReleaseAtmosphereJobBufferLocks(vault, mask);
        }

        private void ReleaseAtmosphereJobBufferLocks(IDataVault vault, ulong mask)
        {
            if (vault == null || mask == 0ul)
                return;

            vault.ReleaseMutationGuard(mask);
        }

        private void ScheduleAtmosphereJob(float fixedDeltaTime)
        {
            if (_atmosphereJobRunning || fluidDynamics == null || !IsAtmosphereVaultStateReady())
                return;

            int roomCount = RoomCount;
            int doorCount = math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity);
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referencePressure = ResolveSafeReferencePressureKPa();
            float maximumPressure = ResolveSafeMaximumPressureKPa();
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            if (!TryLockAtmosphereJobBuffers())
                return;

            AtmosphereStepJob job = new AtmosphereStepJob
            {
                O2Front = _o2Front,
                CO2Front = _co2Front,
                InertFront = _inertFront,
                FloodVolumes = _floodVolumes,
                RoomVolumes = _roomVolumes,
                PressureFront = _pressureFront,
                GasVolumeFront = _gasVolumeFront,
                O2ConsumptionRates = _o2ConsumptionRates,
                CO2GenerationRates = _co2GenerationRates,
                RoomPlayerCounts = _roomPlayerCounts,
                TemperatureFront = _temperatureFront,
                RoomHeatWatts = _roomHeatWatts,
                SteamFront = _steamFront,
                DoorPairs = _doorPairs,
                DoorSealed = _doorSealed,
                O2Back = _o2Back,
                CO2Back = _co2Back,
                InertBack = _inertBack,
                PressureBack = _pressureBack,
                GasVolumeBack = _gasVolumeBack,
                TemperatureBack = _temperatureBack,
                SteamBack = _steamBack,
                O2PartialPressureBack = _o2PartialPressureBack,
                CO2PartialPressureBack = _co2PartialPressureBack,
                N2PartialPressureBack = _n2PartialPressureBack,
                RoomStatusMaskBack = _roomStatusMaskBack,
                RoomCount = roomCount,
                DoorCount = doorCount,
                DeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime),
                ReferencePressureKPa = referencePressure,
                MinimumGasVolumeCubicMeters = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)),
                MaximumPressureKPa = maximumPressure,
                ReferenceTemperatureCelsius = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature),
                FloodWaterTemperatureCelsius = FiniteClampedOr(floodWaterTemperatureCelsius, DefaultFloodWaterTemperatureCelsius, minimumTemperature, maximumTemperature),
                MinimumTemperatureCelsius = minimumTemperature,
                MaximumTemperatureCelsius = maximumTemperature,
                OxygenTankCapacity = tankCapacity,
                HeatWattsToCelsiusPerSecond = FiniteNonNegativeOrZero(heatWattsToCelsiusPerSecond),
                LowOxygenThresholdUnits = math.saturate(FiniteOr(lowOxygenThreshold01, DefaultLowOxygenThreshold01)) * tankCapacity,
                CarbonDioxideToxicityFraction = DefaultCarbonDioxideToxicityFraction,
                FreezingTemperatureCelsius = FiniteClampedOr(freezingRoomTemperatureCelsius, DefaultFreezingRoomTemperatureCelsius, minimumTemperature, maximumTemperature),
                HighPressureStatusKPa = math.max(referencePressure, FiniteOr(highPressureEventThresholdKPa, referencePressure))
            };

            _scheduledAtmosphereDeltaTime = FiniteNonNegativeOrZero(fixedDeltaTime);
            _atmosphereJobHandle = job.Schedule();
            _atmosphereJobRunning = true;
        }

        private void ConsumeCompletedJob(float fixedDeltaTime)
        {
            if (!_atmosphereJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _atmosphereJobHandle, false))
                return;

            _atmosphereJobRunning = false;
            ReleaseAtmosphereJobBufferLocks();
            SwapAtmosphereBuffers();
            float atmosphereDeltaTime = ResolveCompletedAtmosphereDeltaTime(fixedDeltaTime);
            if (!TryEnterAtmosphereWritePhase(out bool ownsWriteLock))
            {
                RecordAtmosphereFailure(5);
                return;
            }

            try
            {
                ApplyPendingAtmosphereMutations();
                ApplyCompletedAtmosphereStepSideEffects(atmosphereDeltaTime);
            }
            finally
            {
                ExitAtmosphereWritePhase(ownsWriteLock);
            }

            RecordAtmosphereBlackBox(atmosphereDeltaTime);
        }

        private float ResolveCompletedAtmosphereDeltaTime(float fallbackDeltaTime)
        {
            float deltaTime = _scheduledAtmosphereDeltaTime > 0f
                ? _scheduledAtmosphereDeltaTime
                : FiniteNonNegativeOrZero(fallbackDeltaTime);
            _scheduledAtmosphereDeltaTime = 0f;
            return deltaTime;
        }

        private void ApplyCompletedAtmosphereStepSideEffects(float atmosphereDeltaTime)
        {
            ApplyAbyssalBlackoutFreeze(atmosphereDeltaTime);
            ProcessSteamPhaseCycle(atmosphereDeltaTime);
            TryEmergencyAtmosphericVenting(atmosphereDeltaTime);
            DecayExplosivePockets(atmosphereDeltaTime);
            EvaluateReactorMeltdowns();
            UpdateBoilingFloodHazards(atmosphereDeltaTime);
            PublishCompartmentPartialPressureSnapshot();
            PublishAtmosphereFakes(atmosphereDeltaTime);
        }

        private void RecordAtmosphereBlackBox(float atmosphereDeltaTime)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireAtmosphereTelemetryWriteGuard(vault))
                return;

            int writeIndex = 0;
            int nextIndex = 0;
            bool dumpRequired = false;
            try
            {
                if (!vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) ||
                    !telemetryCursor.IsCreated ||
                    telemetryCursor.Length <= 0)
                {
                    return;
                }

                writeIndex = telemetryCursor[0];
                if ((uint)writeIndex >= (uint)TelemetryCapacity)
                    writeIndex = 0;

                nextIndex = writeIndex + 1;
                if (nextIndex >= TelemetryCapacity)
                    nextIndex = 0;

                telemetryCursor[0] = nextIndex;
                _telemetryWriteIndex = nextIndex;

                if (!vault.TryResolveHandle(in _telemetryRingHandle, out NativeArray<SubmarineAtmosphereTelemetryEntry> telemetryRing) ||
                    !telemetryRing.IsCreated ||
                    telemetryRing.Length < TelemetryCapacity)
                {
                    return;
                }

                if ((uint)writeIndex >= (uint)telemetryRing.Length)
                    writeIndex = 0;

                SubmarineAtmosphereTelemetryEntry entry = BuildAtmosphereTelemetryEntry(atmosphereDeltaTime);
                telemetryRing[writeIndex] = entry;
                _atmosphereTickCount++;

                if ((entry.Flags & TelemetryFlagNaN) != 0)
                    dumpRequired = true;
            }
            finally
            {
                vault.ReleaseMutationGuard(AtmosphereTelemetryMutationGuardMask);
            }

            if (dumpRequired)
                DumpBlackBoxOnce();
        }

        private void RecordAtmosphereFailure(ushort failureCode)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireAtmosphereTelemetryWriteGuard(vault))
                return;

            int writeIndex = 0;
            int nextIndex = 0;
            try
            {
                if (!vault.TryResolveHandle(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) ||
                    !telemetryCursor.IsCreated ||
                    telemetryCursor.Length <= 0)
                {
                    return;
                }

                writeIndex = telemetryCursor[0];
                if ((uint)writeIndex >= (uint)TelemetryCapacity)
                    writeIndex = 0;

                nextIndex = writeIndex + 1;
                if (nextIndex >= TelemetryCapacity)
                    nextIndex = 0;

                telemetryCursor[0] = nextIndex;
                _telemetryWriteIndex = nextIndex;

                if (!vault.TryResolveHandle(in _telemetryRingHandle, out NativeArray<SubmarineAtmosphereTelemetryEntry> telemetryRing) ||
                    !telemetryRing.IsCreated ||
                    telemetryRing.Length < TelemetryCapacity)
                {
                    return;
                }

                if ((uint)writeIndex >= (uint)telemetryRing.Length)
                    writeIndex = 0;

                SubmarineAtmosphereTelemetryEntry entry = BuildAtmosphereTelemetryEntry(0f);
                entry.FailureCode = failureCode;
                telemetryRing[writeIndex] = entry;
            }
            finally
            {
                vault.ReleaseMutationGuard(AtmosphereTelemetryMutationGuardMask);
            }
        }

        private SubmarineAtmosphereTelemetryEntry BuildAtmosphereTelemetryEntry(float atmosphereDeltaTime)
        {
            int roomCount = RoomCount;
            float totalO2KPa = 0f;
            float totalCo2KPa = 0f;
            float totalN2KPa = 0f;
            float maxPressureKPa = 0f;
            ushort flags = 0;
            uint stateHash = 2166136261u;

            bool hasO2 = TryReadVaultArray(in _o2PartialPressureFrontHandle, out NativeArray<float>.ReadOnly o2PartialPressure);
            bool hasCo2 = TryReadVaultArray(in _co2PartialPressureFrontHandle, out NativeArray<float>.ReadOnly co2PartialPressure);
            bool hasN2 = TryReadVaultArray(in _n2PartialPressureFrontHandle, out NativeArray<float>.ReadOnly n2PartialPressure);
            bool hasPressure = TryReadVaultArray(in _pressureFrontHandle, out NativeArray<float>.ReadOnly pressure);
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float o2 = hasO2 && roomIndex < o2PartialPressure.Length ? o2PartialPressure[roomIndex] : 0f;
                float co2 = hasCo2 && roomIndex < co2PartialPressure.Length ? co2PartialPressure[roomIndex] : 0f;
                float n2 = hasN2 && roomIndex < n2PartialPressure.Length ? n2PartialPressure[roomIndex] : 0f;
                float roomPressure = hasPressure && roomIndex < pressure.Length ? pressure[roomIndex] : 0f;

                if (!math.isfinite(o2) || !math.isfinite(co2) || !math.isfinite(n2) || !math.isfinite(roomPressure))
                    flags |= TelemetryFlagNaN;

                o2 = FiniteNonNegativeOrZero(o2);
                co2 = FiniteNonNegativeOrZero(co2);
                n2 = FiniteNonNegativeOrZero(n2);
                roomPressure = FiniteNonNegativeOrZero(roomPressure);
                totalO2KPa += o2;
                totalCo2KPa += co2;
                totalN2KPa += n2;
                maxPressureKPa = math.max(maxPressureKPa, roomPressure);
                stateHash = MixTelemetryHash(stateHash, o2);
                stateHash = MixTelemetryHash(stateHash, co2);
                stateHash = MixTelemetryHash(stateHash, n2);
                stateHash = MixTelemetryHash(stateHash, roomPressure);
            }

            stateHash = MixTelemetryHash(stateHash, (uint)_runtimeRoomStatusMask);
            stateHash = MixTelemetryHash(stateHash, (uint)_droppedSignalCount);
            if (!math.isfinite(atmosphereDeltaTime))
                flags |= TelemetryFlagNaN;

            return new SubmarineAtmosphereTelemetryEntry
            {
                PackedOwner = ((ulong)_telemetryRingHandle.BufferID << 32) | _telemetryRingHandle.SystemID,
                FrameIndex = unchecked((uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex),
                RoomCount = roomCount,
                DeltaTimeSeconds = FiniteNonNegativeOrZero(atmosphereDeltaTime),
                TotalO2KPa = totalO2KPa,
                TotalCO2KPa = totalCo2KPa,
                TotalNitrogenKPa = totalN2KPa,
                MaxPressureKPa = maxPressureKPa,
                StateHash = stateHash,
                BufferId = _telemetryRingHandle.BufferID,
                SystemId = _telemetryRingHandle.SystemID,
                Generation = _telemetryRingHandle.Generation,
                RuntimeRoomStatusMask = _runtimeRoomStatusMask,
                Flags = flags,
                FailureCode = 0,
                DroppedSignals = _droppedSignalCount
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixTelemetryHash(uint hash, float value)
        {
            return MixTelemetryHash(hash, math.asuint(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixTelemetryHash(uint hash, uint value)
        {
            hash ^= value;
            return hash * 16777619u;
        }

        private bool TryReadTelemetryRing(out NativeArray<SubmarineAtmosphereTelemetryEntry>.ReadOnly telemetryRing)
        {
            return TryReadVaultArray(in _telemetryRingHandle, out telemetryRing) &&
                telemetryRing.Length >= TelemetryCapacity;
        }

        private bool TryReadTelemetryCursor(out int cursor)
        {
            cursor = _telemetryWriteIndex;
            if (!TryReadVaultArray(in _telemetryCursorHandle, out NativeArray<int>.ReadOnly telemetryCursor) ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            cursor = telemetryCursor[0];
            return true;
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped ||
                !TryReadTelemetryRing(out NativeArray<SubmarineAtmosphereTelemetryEntry>.ReadOnly telemetryRing))
            {
                return;
            }

            int cursor = _telemetryWriteIndex;
            TryReadTelemetryCursor(out cursor);
            NativeArray<byte> payload = default;
            try
            {
                const int headerBytes = 22;
                int byteCount = headerBytes + telemetryRing.Length * TelemetryEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    NativeMemoryOwner,
                    TelemetryDumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    WriteUInt(bytes, 0, TelemetryDumpMagic);
                    WriteUShort(bytes, 4, TelemetryDumpFormatVersion);
                    WriteInt(bytes, 6, TelemetryEntrySizeBytes);
                    WriteInt(bytes, 10, telemetryRing.Length);
                    WriteInt(bytes, 14, cursor);
                    WriteUInt(bytes, 18, _atmosphereTickCount);

                    int writeCursor = headerBytes;
                    for (int i = 0; i < telemetryRing.Length; i++)
                    {
                        SubmarineAtmosphereTelemetryEntry entry = telemetryRing[i];
                        UnsafeUtility.CopyStructureToPtr(ref entry, bytes + writeCursor);
                        writeCursor += TelemetryEntrySizeBytes;
                    }
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(TelemetryDumpRelativePath, payload, byteCount);
            }
            catch (System.Exception)
            {
                GlobalTelemetryBus.PublishUnityLogFault(TelemetryDumpMagic, 0u, 1u);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, NativeMemoryOwner, TelemetryDumpPayloadLabel);
            }
        }

        private static unsafe void WriteUInt(byte* data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteInt(byte* data, int offset, int value)
        {
            WriteUInt(data, offset, unchecked((uint)value));
        }

        private static unsafe void WriteUShort(byte* data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private void PublishCompartmentPartialPressureSnapshot()
        {
            if (fluidDynamics == null ||
                !_o2PartialPressureFront.IsCreated ||
                !_co2PartialPressureFront.IsCreated ||
                !_n2PartialPressureFront.IsCreated)
            {
                return;
            }

            int roomCount = RoomCount;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                    continue;

                fluidDynamics.SetCompartmentGasPartialPressuresKPa(
                    roomIndex,
                    _o2PartialPressureFront[roomIndex],
                    _co2PartialPressureFront[roomIndex],
                    _n2PartialPressureFront[roomIndex]);
            }
        }

        private void RefreshDebugState()
        {
            int roomCount = RoomCount;
            int doorCount = fluidDynamics != null ? math.min(fluidDynamics.ConfiguredBulkheadCount, DoorCapacity) : 0;
            _debugRoomCount = roomCount;
            _debugDoorCount = doorCount;

            bool hasPressure = TryReadVaultArray(in _pressureFrontHandle, out NativeArray<float>.ReadOnly pressureFront);
            bool hasTemperature = TryReadVaultArray(in _temperatureFrontHandle, out NativeArray<float>.ReadOnly temperatureFront);
            bool hasSteam = TryReadVaultArray(in _steamFrontHandle, out NativeArray<float>.ReadOnly steamFront);
            bool hasOxygen = TryReadVaultArray(in _o2FrontHandle, out NativeArray<float>.ReadOnly o2Front);
            bool hasCarbonDioxide = TryReadVaultArray(in _co2FrontHandle, out NativeArray<float>.ReadOnly co2Front);
            if (!hasPressure || roomCount <= 0)
            {
                _debugAveragePressureKPa = 0f;
                _debugMaxPressureKPa = 0f;
                _debugAverageOxygenFraction = 0f;
                _debugAverageCarbonDioxideFraction = 0f;
                _debugAverageTemperatureCelsius = 0f;
                _debugMaxTemperatureCelsius = 0f;
                _debugAverageSteamVolumeCubicMeters = 0f;
                _debugMaxSteamVolumeCubicMeters = 0f;
                return;
            }

            float pressureSum = 0f;
            float maxPressure = 0f;
            float oxygenFractionSum = 0f;
            float carbonDioxideFractionSum = 0f;
            float temperatureSum = 0f;
            float maxTemperature = minimumTemperatureCelsius;
            float steamSum = 0f;
            float maxSteam = 0f;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            float fallbackTemperature = FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius);
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                float pressure = (uint)roomIndex < (uint)pressureFront.Length
                    ? FiniteClampedOr(pressureFront[roomIndex], ResolveSafeReferencePressureKPa(), 0f, ResolveSafeMaximumPressureKPa())
                    : 0f;
                pressureSum += pressure;
                maxPressure = math.max(maxPressure, pressure);
                float temperature = hasTemperature && (uint)roomIndex < (uint)temperatureFront.Length
                    ? FiniteClampedOr(temperatureFront[roomIndex], fallbackTemperature, minimumTemperatureCelsius, maximumTemperatureCelsius)
                    : fallbackTemperature;
                temperatureSum += temperature;
                maxTemperature = math.max(maxTemperature, temperature);
                float steamVolume = hasSteam && (uint)roomIndex < (uint)steamFront.Length
                    ? FiniteNonNegativeOrZero(steamFront[roomIndex])
                    : 0f;
                steamSum += steamVolume;
                maxSteam = math.max(maxSteam, steamVolume);

                float oxygenUnits = hasOxygen && (uint)roomIndex < (uint)o2Front.Length
                    ? FiniteNonNegativeOrZero(o2Front[roomIndex])
                    : 0f;
                float carbonDioxideUnits = hasCarbonDioxide && (uint)roomIndex < (uint)co2Front.Length
                    ? FiniteNonNegativeOrZero(co2Front[roomIndex])
                    : 0f;
                oxygenFractionSum += math.saturate(oxygenUnits / tankCapacity);
                carbonDioxideFractionSum += math.saturate(carbonDioxideUnits / tankCapacity);
            }

            float inverseRoomCount = 1f / math.max(roomCount, 1);
            _debugAveragePressureKPa = pressureSum * inverseRoomCount;
            _debugMaxPressureKPa = maxPressure;
            _debugAverageOxygenFraction = oxygenFractionSum * inverseRoomCount;
            _debugAverageCarbonDioxideFraction = carbonDioxideFractionSum * inverseRoomCount;
            _debugAverageTemperatureCelsius = temperatureSum * inverseRoomCount;
            _debugMaxTemperatureCelsius = maxTemperature;
            _debugAverageSteamVolumeCubicMeters = steamSum * inverseRoomCount;
            _debugMaxSteamVolumeCubicMeters = maxSteam;
        }

        private void DisposeNativeStateDeferred()
        {
            ClearBoilingFloodHazards();
            ClearAtmosphereFakes();
            FlushQueuedAtmospherePresentation();
            if (_atmosphereJobRunning)
                DispatcherJobSwap.TryComplete(ref _atmosphereJobHandle, forceComplete: true);
            ReleaseAtmosphereJobBufferLocks();
            ReleaseAtmospherePhaseWriteLocks();
            _atmosphereJobRunning = false;
            _scheduledAtmosphereDeltaTime = 0f;
            ReleaseAtmosphereVaultHandles(_dataVault);
            _topologySeeded = false;
            _topologyRoomCount = -1;
            _topologyDoorCount = -1;
            _thermalEmittersSeeded = false;
            _emergencyVentPipesSeeded = false;
            _emergencyVentRoomMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _emergencyVentPipesByRoom[roomIndex] = null;
            ClearPendingAtmosphereMutations();
        }

        private void ReleaseAtmosphereVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _roomVolumesHandle);
            ReleaseVaultHandle(vault, ref _floodVolumesHandle);
            ReleaseVaultHandle(vault, ref _o2FrontHandle);
            ReleaseVaultHandle(vault, ref _o2BackHandle);
            ReleaseVaultHandle(vault, ref _co2FrontHandle);
            ReleaseVaultHandle(vault, ref _co2BackHandle);
            ReleaseVaultHandle(vault, ref _inertFrontHandle);
            ReleaseVaultHandle(vault, ref _inertBackHandle);
            ReleaseVaultHandle(vault, ref _pressureFrontHandle);
            ReleaseVaultHandle(vault, ref _pressureBackHandle);
            ReleaseVaultHandle(vault, ref _o2PartialPressureFrontHandle);
            ReleaseVaultHandle(vault, ref _o2PartialPressureBackHandle);
            ReleaseVaultHandle(vault, ref _co2PartialPressureFrontHandle);
            ReleaseVaultHandle(vault, ref _co2PartialPressureBackHandle);
            ReleaseVaultHandle(vault, ref _n2PartialPressureFrontHandle);
            ReleaseVaultHandle(vault, ref _n2PartialPressureBackHandle);
            ReleaseVaultHandle(vault, ref _gasVolumeFrontHandle);
            ReleaseVaultHandle(vault, ref _gasVolumeBackHandle);
            ReleaseVaultHandle(vault, ref _o2ConsumptionRatesHandle);
            ReleaseVaultHandle(vault, ref _co2GenerationRatesHandle);
            ReleaseVaultHandle(vault, ref _roomPlayerCountsHandle);
            ReleaseVaultHandle(vault, ref _temperatureFrontHandle);
            ReleaseVaultHandle(vault, ref _temperatureBackHandle);
            ReleaseVaultHandle(vault, ref _steamFrontHandle);
            ReleaseVaultHandle(vault, ref _steamBackHandle);
            ReleaseVaultHandle(vault, ref _hydrogenPocketFrontHandle);
            ReleaseVaultHandle(vault, ref _oxygenPocketFrontHandle);
            ReleaseVaultHandle(vault, ref _roomHeatWattsHandle);
            ReleaseVaultHandle(vault, ref _roomStatusMaskFrontHandle);
            ReleaseVaultHandle(vault, ref _roomStatusMaskBackHandle);
            ReleaseVaultHandle(vault, ref _doorPairsHandle);
            ReleaseVaultHandle(vault, ref _doorSealedHandle);
            ReleaseVaultHandle(vault, ref _doorSealedPreviousHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && HasValidVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private void SwapAtmosphereBuffers()
        {
            SwapBuffers(ref _o2FrontHandle, ref _o2BackHandle);
            SwapBuffers(ref _co2FrontHandle, ref _co2BackHandle);
            SwapBuffers(ref _inertFrontHandle, ref _inertBackHandle);
            SwapBuffers(ref _pressureFrontHandle, ref _pressureBackHandle);
            SwapBuffers(ref _o2PartialPressureFrontHandle, ref _o2PartialPressureBackHandle);
            SwapBuffers(ref _co2PartialPressureFrontHandle, ref _co2PartialPressureBackHandle);
            SwapBuffers(ref _n2PartialPressureFrontHandle, ref _n2PartialPressureBackHandle);
            SwapBuffers(ref _gasVolumeFrontHandle, ref _gasVolumeBackHandle);
            SwapBuffers(ref _temperatureFrontHandle, ref _temperatureBackHandle);
            SwapBuffers(ref _steamFrontHandle, ref _steamBackHandle);
            SwapBuffers(ref _roomStatusMaskFrontHandle, ref _roomStatusMaskBackHandle);
        }

        private void ClearPendingAtmosphereMutations()
        {
            _pendingAtmosphereMutationMask = 0u;
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _pendingAtmosphereMutations[roomIndex] = default;
        }

        private void ClearRoomModuleCache()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _atmosphereRoomModules[roomIndex] = null;
        }

        private void ClearBrownoutRoomModuleCache()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                _brownoutRoomModules[roomIndex] = null;
        }

        private float ResolveInstantPressure(float totalGasUnits, float gasVolumeCubicMeters)
        {
            return ResolveInstantPressureWithTemperature(totalGasUnits, gasVolumeCubicMeters, FiniteOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius));
        }

        private float ResolveInstantFakePressure(int roomIndex, float temperatureCelsius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSafeReferencePressureKPa();

            return ResolveInstantDaltonPressure(roomIndex, temperatureCelsius);
        }

        private float ResolveInstantDaltonPressure(int roomIndex, float temperatureCelsius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount)
                return ResolveSafeReferencePressureKPa();

            NativeArray<float> _roomVolumes = this._roomVolumes;
            NativeArray<float> _floodVolumes = this._floodVolumes;
            NativeArray<float> _steamFront = this._steamFront;
            NativeArray<float> _o2Front = this._o2Front;
            NativeArray<float> _co2Front = this._co2Front;
            NativeArray<float> _inertFront = this._inertFront;
            NativeArray<float> _gasVolumeFront = this._gasVolumeFront;
            NativeArray<float> _o2PartialPressureFront = this._o2PartialPressureFront;
            NativeArray<float> _co2PartialPressureFront = this._co2PartialPressureFront;
            NativeArray<float> _n2PartialPressureFront = this._n2PartialPressureFront;
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float roomVolume = _roomVolumes.IsCreated
                ? math.max(FiniteOr(_roomVolumes[roomIndex], minimumGasVolume), minimumGasVolume)
                : minimumGasVolume;
            float floodVolume = _floodVolumes.IsCreated
                ? math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume - Epsilon)
                : 0f;
            float gasVolume = math.max(minimumGasVolume, roomVolume - floodVolume);
            float steamVolume = _steamFront.IsCreated
                ? FiniteNonNegativeOrZero(_steamFront[roomIndex])
                : 0f;
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature);
            float pressure = ResolveDaltonPressureKPa(
                _o2Front.IsCreated ? _o2Front[roomIndex] : tankCapacity,
                _co2Front.IsCreated ? _co2Front[roomIndex] : DefaultInitialCarbonDioxideFraction * tankCapacity,
                _inertFront.IsCreated ? _inertFront[roomIndex] : DefaultInertFraction * tankCapacity,
                steamVolume,
                roomVolume,
                gasVolume,
                temperatureCelsius,
                ResolveSafeReferencePressureKPa(),
                ResolveSafeMaximumPressureKPa(),
                referenceTemperature,
                tankCapacity,
                out float oxygenPartialPressureKPa,
                out float carbonDioxidePartialPressureKPa,
                out float nitrogenPartialPressureKPa);

            if (_gasVolumeFront.IsCreated)
                _gasVolumeFront[roomIndex] = gasVolume;
            if (_o2PartialPressureFront.IsCreated)
                _o2PartialPressureFront[roomIndex] = oxygenPartialPressureKPa;
            if (_co2PartialPressureFront.IsCreated)
                _co2PartialPressureFront[roomIndex] = carbonDioxidePartialPressureKPa;
            if (_n2PartialPressureFront.IsCreated)
                _n2PartialPressureFront[roomIndex] = nitrogenPartialPressureKPa;

            return pressure;
        }

        private float ResolveSimplePressureKPa(float roomVolume, float floodVolume, float steamVolume, float temperatureCelsius)
        {
            ResolveSafeTemperatureBounds(out float minimumTemperature, out float maximumTemperature);
            float referenceTemperature = FiniteClampedOr(referenceTemperatureCelsius, DefaultReferenceTemperatureCelsius, minimumTemperature, maximumTemperature);
            float safeRoomVolume = math.max(math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)), FiniteOr(roomVolume, DefaultMinimumGasVolumeCubicMeters));
            float gasVolume = math.max(math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters)), safeRoomVolume - math.clamp(FiniteNonNegativeOrZero(floodVolume), 0f, safeRoomVolume - Epsilon));
            float safeTemperature = FiniteClampedOr(temperatureCelsius, referenceTemperature, minimumTemperature, maximumTemperature);
            float tankCapacity = math.max(1f, FiniteOr(oxygenTankCapacity, DefaultOxygenTankCapacity));
            return ResolveDaltonPressureKPa(
                tankCapacity,
                DefaultInitialCarbonDioxideFraction * tankCapacity,
                DefaultInertFraction * tankCapacity,
                steamVolume,
                safeRoomVolume,
                gasVolume,
                safeTemperature,
                ResolveSafeReferencePressureKPa(),
                ResolveSafeMaximumPressureKPa(),
                referenceTemperature,
                tankCapacity,
                out _,
                out _,
                out _);
        }

        private void UpdateBoilingFloodHazards(float fixedDeltaTime)
        {
            if (fluidDynamics == null || !_temperatureFront.IsCreated || !_floodVolumes.IsCreated || !_roomVolumes.IsCreated)
            {
                ClearBoilingFloodHazards();
                return;
            }

            int roomCount = RoomCount;
            ResolveSafeTemperatureBounds(out float safeMinimumTemperature, out float safeMaximumTemperature);
            float thresholdTemperature = FiniteClampedOr(boilingFloodTemperatureCelsius, DefaultBoilingFloodTemperatureCelsius, safeMinimumTemperature, safeMaximumTemperature);
            float minimumFillRatio = math.saturate(FiniteOr(boilingFloodMinimumFillRatio, DefaultBoilingFloodMinimumFillRatio));
            float hazardBaseIntensity = FiniteNonNegativeOrZero(boilingHazardIntensity);
            float faunaDamagePerStep = FiniteNonNegativeOrZero(boilingFaunaDamagePerSecond) * FiniteNonNegativeOrZero(fixedDeltaTime);
            float maxTemperature = math.max(thresholdTemperature + 1f, safeMaximumTemperature);
            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));

            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
            {
                if (roomIndex >= roomCount)
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                float roomVolume = math.max(minimumGasVolume, FiniteOr(_roomVolumes[roomIndex], minimumGasVolume));
                float floodVolume = math.clamp(FiniteNonNegativeOrZero(_floodVolumes[roomIndex]), 0f, roomVolume);
                float invRoomVolume = math.rcp(roomVolume);
                float fillRatio = math.saturate(floodVolume * invRoomVolume);
                float temperature = GetRoomTemperatureCelsius(roomIndex);
                if (temperature < thresholdTemperature || fillRatio < minimumFillRatio)
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                if (!TryResolveBoilingHazardBounds(roomIndex, roomVolume, out Vector3 worldCenter, out float radius))
                {
                    UnregisterBoilingHazard(roomIndex);
                    continue;
                }

                float temperature01 = math.saturate((temperature - thresholdTemperature) * math.rcp(math.max(1f, maxTemperature - thresholdTemperature)));
                float fill01 = math.saturate((fillRatio - minimumFillRatio) * math.rcp(math.max(0.01f, 1f - minimumFillRatio)));
                float intensity = hazardBaseIntensity * math.max(0.1f, math.max(temperature01, fill01));

                RegisterBoilingHazard(roomIndex, worldCenter, intensity, radius);
                ApplyBoilingFaunaDamage(worldCenter, radius, intensity * faunaDamagePerStep);
            }
        }

        private void ClearBoilingFloodHazards()
        {
            for (int roomIndex = 0; roomIndex < RoomCapacity; roomIndex++)
                UnregisterBoilingHazard(roomIndex);
        }

        private void RegisterBoilingHazard(int roomIndex, Vector3 worldCenter, float intensity, float radius)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            IThermodynamicsService thermodynamics = ResolveThermodynamicsService();
            if (thermodynamics != null &&
                thermodynamics.IsInitialized &&
                thermodynamics.TryInjectTransientHeatSource(worldCenter, radius, intensity, unchecked((uint)_boilingHazardIds[roomIndex])))
            {
                _boilingHazardActiveMask |= roomBit;
                return;
            }

            UnregisterBoilingHazard(roomIndex);
        }

        private void UnregisterBoilingHazard(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCapacity)
                return;

            uint roomBit = 1u << roomIndex;
            if ((_boilingHazardActiveMask & roomBit) == 0u)
                return;

            HectonHazardManager.Unregister(_boilingHazardIds[roomIndex]);
            _boilingHazardActiveMask &= ~roomBit;
        }

        private IThermodynamicsService ResolveThermodynamicsService()
        {
            if (IsUnityObjectInvalid(_thermodynamicsService))
            {
                _thermodynamicsService = null;
                return null;
            }

            return _thermodynamicsService;
        }

        private static bool IsUnityObjectInvalid(object context)
        {
            return context is UnityEngine.Object unityObject && unityObject == null;
        }

        private float ResolveInstantThermalCapacity(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= RoomCount || !_roomVolumes.IsCreated || !_floodVolumes.IsCreated || !_gasVolumeFront.IsCreated)
                return math.max(Epsilon, FiniteOr(minimumThermalCapacityJoulesPerKelvin, DefaultMinimumThermalCapacityJoulesPerKelvin));

            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float gasVolume = math.max(minimumGasVolume, FiniteOr(_gasVolumeFront[roomIndex], minimumGasVolume));
            float floodVolume = FiniteNonNegativeOrZero(_floodVolumes[roomIndex]);
            float airMass = gasVolume * math.max(Epsilon, FiniteOr(airDensityKilogramsPerCubicMeter, DefaultAirDensityKilogramsPerCubicMeter));
            float waterMass = floodVolume * math.max(Epsilon, FiniteOr(waterDensityKilogramsPerCubicMeter, DefaultWaterDensityKilogramsPerCubicMeter));
            float airCapacity = airMass * math.max(Epsilon, FiniteOr(airSpecificHeatJoulesPerKilogramKelvin, DefaultAirSpecificHeatJoulesPerKilogramKelvin));
            float waterCapacity = waterMass * math.max(Epsilon, FiniteOr(waterSpecificHeatJoulesPerKilogramKelvin, DefaultWaterSpecificHeatJoulesPerKilogramKelvin));
            return math.max(math.max(Epsilon, FiniteOr(minimumThermalCapacityJoulesPerKelvin, DefaultMinimumThermalCapacityJoulesPerKelvin)), airCapacity + waterCapacity);
        }

        private static float ResolveFakeHazardRadius(float roomVolume, float paddingMeters)
        {
            float safePadding = FiniteNonNegativeOrZero(paddingMeters);
            float volumeRadius = FakeHazardRadiusBaseMeters + (FiniteNonNegativeOrZero(roomVolume) * FakeHazardRadiusVolumeScale);
            return math.clamp(volumeRadius + safePadding, 0.5f, FakeHazardRadiusMaxMeters + safePadding);
        }

        private static bool TryResolveModuleInteriorHazardBounds(BaseModule module, float paddingMeters, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (module == null || !module.TryGetInteriorAabbBounds(out worldCenter, out Vector3 halfExtents))
                return false;

            float safePadding = FiniteNonNegativeOrZero(paddingMeters);
            float maxExtent = math.max(
                FiniteNonNegativeOrZero(halfExtents.x),
                math.max(FiniteNonNegativeOrZero(halfExtents.y), FiniteNonNegativeOrZero(halfExtents.z)));
            radius = math.clamp((maxExtent * 1.75f) + safePadding, 0.5f, FakeHazardRadiusMaxMeters + safePadding);
            return radius > 0f;
        }

        private bool TryResolveBoilingHazardBounds(int roomIndex, float roomVolume, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null)
                return false;

            if (TryResolveModuleInteriorHazardBounds(ResolveModuleForRoom(roomIndex), boilingHazardRadiusPaddingMeters, out worldCenter, out radius))
                return true;

            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);

            radius = ResolveFakeHazardRadius(roomVolume, boilingHazardRadiusPaddingMeters);
            return radius > 0f;
        }

        private bool TryResolveRoomHazardBounds(int roomIndex, out Vector3 worldCenter, out float radius)
        {
            return TryResolveRoomHazardBounds(roomIndex, ResolveModuleForRoom(roomIndex), out worldCenter, out radius);
        }

        private bool TryResolveRoomHazardBounds(int roomIndex, BaseModule roomModule, out Vector3 worldCenter, out float radius)
        {
            worldCenter = Vector3.zero;
            radius = 0f;
            if (fluidDynamics == null || _cachedTransform == null || roomIndex < 0 || roomIndex >= RoomCount)
                return false;

            if (TryResolveModuleInteriorHazardBounds(roomModule, roomHazardRadiusPaddingMeters, out worldCenter, out radius))
                return true;

            float minimumGasVolume = math.max(0.001f, FiniteOr(minimumGasVolumeCubicMeters, DefaultMinimumGasVolumeCubicMeters));
            float roomVolume = _roomVolumes.IsCreated
                ? math.max(FiniteOr(_roomVolumes[roomIndex], minimumGasVolume), minimumGasVolume)
                : math.max(FiniteOr(fluidDynamics.GetCompartmentMaxFloodVolumeCubicMeters(roomIndex), minimumGasVolume), minimumGasVolume);
            Vector3 localCentroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
            worldCenter = _cachedTransform.TransformPoint(localCentroid);
            radius = ResolveFakeHazardRadius(roomVolume, roomHazardRadiusPaddingMeters);
            return radius > 0f;
        }

        private void ApplyBoilingFaunaDamage(Vector3 worldCenter, float radius, float damageAmount)
        {
            if (damageAmount <= 0f || radius <= 0f)
                return;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                worldCenter,
                radius,
                SpatialTargetKind.Bioform,
                _boilingFaunaContacts);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _boilingFaunaContacts[hitIndex];
                if (hit.Owner is IDamageReceiver damageReceiver &&
                    hit.Owner is Component damageOwner &&
                    !TryQueueBoilingFaunaDamage(damageReceiver, damageOwner, in hit, worldCenter, damageAmount))
                {
                    ApplyBoilingFaunaOwnerFallbackDamage(damageReceiver, damageOwner, in hit, worldCenter, damageAmount);
                }
            }
        }

        private bool TryQueueBoilingFaunaDamage(
            IDamageReceiver damageReceiver,
            Component damageOwner,
            in SpatialQueryHit hit,
            Vector3 hazardCenter,
            float damageAmount)
        {
            if (damageReceiver == null || damageOwner == null || !(damageAmount > 0f) || !math.isfinite(damageAmount))
                return false;

            int targetId = CombatDamageRuntime.ResolveTargetId(damageOwner.gameObject);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return false;

            Transform faunaTransform = damageOwner.transform;
            Vector3 impactPoint = ResolveFinitePoint(hit.Position, faunaTransform != null ? faunaTransform.position : hazardCenter);
            float3 direction = ResolveBoilingDamageDirection(hazardCenter, impactPoint);
            CombatDamageRequest signal = new CombatDamageRequest
            {
                TargetId = targetId,
                SourceId = DamageSourceIds.SubmarineAtmosphereBoiling,
                Amount = damageAmount,
                ImpulseMagnitude = 0f,
                Direction = direction,
                PackedMeta = CombatDamageRuntime.PackSignalMeta(
                    CombatDamageTypes.Thermal,
                    CombatStatusBits.Burning,
                    CombatWeakspotTier.None)
            };

            CombatDamageSignalDetail detail = new CombatDamageSignalDetail
            {
                LocalPoint = ResolveTargetLocalPoint(faunaTransform, impactPoint),
                ArmorNormal = -direction,
                LocalTemperatureCelsius = 100f,
                StatusDurationSeconds = 0.5f
            };

            TryResolveImpactAup(in hit, impactPoint, out double3 impactAup);
            CombatDamageRuntime.TryQueueDamage(in signal, in detail, impactAup);
            return true;
        }

        private void ApplyBoilingFaunaOwnerFallbackDamage(
            IDamageReceiver damageReceiver,
            Component damageOwner,
            in SpatialQueryHit hit,
            Vector3 hazardCenter,
            float damageAmount)
        {
            if (damageReceiver == null || damageOwner == null || !(damageAmount > 0f) || !math.isfinite(damageAmount))
                return;

            Transform faunaTransform = damageOwner.transform;
            Vector3 impactPoint = ResolveFinitePoint(hit.Position, faunaTransform != null ? faunaTransform.position : hazardCenter);
            DamagePacket packet = new DamagePacket
            {
                Channel = DamageChannel.Integrity,
                PreviousValue = 0f,
                NextValue = 0f,
                Magnitude = damageAmount,
                LocalPoint = ResolveTargetLocalPoint(faunaTransform, impactPoint),
                DamageType = CombatDamageTypes.Thermal,
                IntegrityDelta = 0,
                Depth = 0f,
                SourceId = DamageSourceIds.SubmarineAtmosphereBoiling,
                TraumaLevel = 0
            };
            damageReceiver.ReceiveDamage(in packet);
        }

        private static Vector3 ResolveFinitePoint(Vector3 candidate, Vector3 fallback)
        {
            if (math.isfinite(candidate.x) && math.isfinite(candidate.y) && math.isfinite(candidate.z))
                return candidate;

            return math.isfinite(fallback.x) && math.isfinite(fallback.y) && math.isfinite(fallback.z)
                ? fallback
                : Vector3.zero;
        }

        private static float3 ResolveBoilingDamageDirection(Vector3 hazardCenter, Vector3 impactPoint)
        {
            Vector3 offset = impactPoint - hazardCenter;
            float3 direction = new float3(offset.x, offset.y, offset.z);
            return math.normalizesafe(direction, new float3(0f, 1f, 0f));
        }

        private static float3 ResolveTargetLocalPoint(Transform targetTransform, Vector3 impactPoint)
        {
            if (targetTransform == null ||
                !math.isfinite(impactPoint.x) ||
                !math.isfinite(impactPoint.y) ||
                !math.isfinite(impactPoint.z))
            {
                return float3.zero;
            }

            Vector3 localPoint = targetTransform.InverseTransformPoint(impactPoint);
            float3 localPoint3 = new float3(localPoint.x, localPoint.y, localPoint.z);
            return math.all(math.isfinite(localPoint3)) ? localPoint3 : float3.zero;
        }

        private static bool TryResolveImpactAup(in SpatialQueryHit hit, Vector3 impactPoint, out double3 impactAup)
        {
            impactAup = double3.zero;
            AbsoluteUniversePosition hitAup = hit.AbsolutePosition;
            if (hit.HasAbsolutePosition && AbsoluteUniversePosition.IsFinite(in hitAup))
            {
                double3 resolvedHitAup = hitAup.ToAbsoluteDouble3();
                if (math.all(math.isfinite(resolvedHitAup)))
                {
                    impactAup = resolvedHitAup;
                    return true;
                }
            }

            if (!TryResolveAupFromRuntimeOrigin(impactPoint, out AbsoluteUniversePosition pointAup))
                return false;

            double3 resolvedPointAup = pointAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(resolvedPointAup)))
                return false;

            impactAup = resolvedPointAup;
            return true;
        }

        private int ResolveNearestRoomIndex(Vector3 worldPosition)
        {
            if (fluidDynamics == null || _cachedTransform == null)
                return -1;

            return ResolveNearestRoomIndexLocal(_cachedTransform.InverseTransformPoint(worldPosition));
        }

        private int ResolveNearestRoomIndex(in AbsoluteUniversePosition worldAup)
        {
            if (fluidDynamics == null || _cachedTransform == null)
                return -1;

            if (!TryResolveRuntimeDeltaFromCurrentOrigin(in worldAup, out float3 runtimePosition))
                return -1;

            return ResolveNearestRoomIndex(new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        }

        private int ResolveNearestRoomIndexLocal(Vector3 localPosition)
        {
            int roomCount = RoomCount;
            if (roomCount <= 0)
                return -1;

            int bestRoomIndex = 0;
            float bestDistanceSq = float.MaxValue;
            for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
            {
                Vector3 centroid = fluidDynamics.GetCompartmentCentroid(roomIndex);
                float distanceSq = (centroid - localPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestRoomIndex = roomIndex;
            }

            return bestRoomIndex;
        }

        private static void SwapBuffers<T>(ref VaultGenerationHandle<T> front, ref VaultGenerationHandle<T> back) where T : struct
        {
            VaultGenerationHandle<T> swap = front;
            front = back;
            back = swap;
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        internal struct SubmarineAtmosphereTelemetryEntry
        {
            [FieldOffset(0)]
            public ulong PackedOwner;
            [FieldOffset(8)]
            public uint FrameIndex;
            [FieldOffset(12)]
            public int RoomCount;
            [FieldOffset(16)]
            public float DeltaTimeSeconds;
            [FieldOffset(20)]
            public float TotalO2KPa;
            [FieldOffset(24)]
            public float TotalCO2KPa;
            [FieldOffset(28)]
            public float TotalNitrogenKPa;
            [FieldOffset(32)]
            public float MaxPressureKPa;
            [FieldOffset(36)]
            public uint StateHash;
            [FieldOffset(40)]
            public uint BufferId;
            [FieldOffset(44)]
            public uint SystemId;
            [FieldOffset(48)]
            public uint Generation;
            [FieldOffset(52)]
            public uint RuntimeRoomStatusMask;
            [FieldOffset(56)]
            public int DroppedSignals;
            [FieldOffset(60)]
            public ushort Flags;
            [FieldOffset(62)]
            public ushort FailureCode;
        }
    
        #region JulesLink_FireOxygenConsumptionCalculator
        private static void JulesLink_FireOxygenConsumptionCalculator() { _ = typeof(Hecton8.PureLogic.Systems.FireOxygenConsumptionCalculator); }
        #endregion
}
}
