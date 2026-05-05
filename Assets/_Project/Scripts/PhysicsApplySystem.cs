using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Physics
{
    /// <summary>
    /// Fixed-step global physics frame counter used by staggered physics systems.
    /// </summary>
    public static class PhysicsFrame
    {
        /// <summary>
        /// Current fixed-step frame index.
        /// </summary>
        public static int Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Current = 0;
        }

        internal static void Tick()
        {
            Current++;
        }
    }

    /// <summary>
    /// Deferred force packet priority. Higher values survive contention guards first.
    /// </summary>
    public enum ForcePacketPriority : byte
    {
        Visual = 0,
        Ambient = 1,
        Critical = 2
    }

    /// <summary>
    /// Deferred main-thread force application payload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct ForcePacket
    {
        /// <summary>World-space force vector.</summary>
        public Vector3 Force;

        /// <summary>World-space torque vector.</summary>
        public Vector3 Torque;

        /// <summary>World-space offset from the rigidbody center of mass used by deferred AddForceAtPosition routing.</summary>
        public Vector3 PointOffset;

        /// <summary>Force application mode.</summary>
        public ForceMode Mode;

        /// <summary>Bitfield flags describing packet contents.</summary>
        public byte Flags;

        /// <summary>Priority class used by contention and entanglement guards.</summary>
        public ForcePacketPriority Priority;

        private byte _padding1;
        private byte _padding2;

        /// <summary>Dense rigidbody slot index owned by <see cref="PhysicsApplySystem"/>.</summary>
        public int RigidbodyIndex;
    }

    /// <summary>
    /// Pressure blowout payload emitted when a bulkhead opens across a large pressure differential.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct PressureImpulseEvent
    {
        /// <summary>
        /// Creates a pressure blowout payload.
        /// </summary>
        public PressureImpulseEvent(
            int doorIndex,
            Vector3 runtimePosition,
            Vector3 direction,
            float doorAreaSquareMeters,
            float highPressureKPa,
            float lowPressureKPa,
            Vector3 forceVectorNewtons,
            Vector3 impulseVectorNewtonSeconds,
            float influenceRadiusMeters)
        {
            DoorIndex = doorIndex;
            RuntimePosition = runtimePosition;
            Direction = direction;
            DoorAreaSquareMeters = doorAreaSquareMeters;
            HighPressureKPa = highPressureKPa;
            LowPressureKPa = lowPressureKPa;
            PressureDeltaKPa = math.abs(highPressureKPa - lowPressureKPa);
            ForceVectorNewtons = forceVectorNewtons;
            ImpulseVectorNewtonSeconds = impulseVectorNewtonSeconds;
            InfluenceRadiusMeters = influenceRadiusMeters;
        }

        /// <summary>Bulkhead edge index inside the submarine compartment graph.</summary>
        public int DoorIndex { get; }

        /// <summary>Runtime-space midpoint of the opened bulkhead.</summary>
        public Vector3 RuntimePosition { get; }

        /// <summary>Normalized airflow direction from the high-pressure room toward the low-pressure room.</summary>
        public Vector3 Direction { get; }

        /// <summary>Cross-sectional doorway area used by the blowout force calculation.</summary>
        public float DoorAreaSquareMeters { get; }

        /// <summary>Pressure of the source room at the moment of opening.</summary>
        public float HighPressureKPa { get; }

        /// <summary>Pressure of the destination room at the moment of opening.</summary>
        public float LowPressureKPa { get; }

        /// <summary>Absolute pressure delta across the opened bulkhead.</summary>
        public float PressureDeltaKPa { get; }

        /// <summary>Raw force vector in newtons derived from the pressure differential.</summary>
        public Vector3 ForceVectorNewtons { get; }

        /// <summary>One-shot impulse vector in newton-seconds routed into the deferred physics system.</summary>
        public Vector3 ImpulseVectorNewtonSeconds { get; }

        /// <summary>World-space influence radius used by the local overlap dispatch.</summary>
        public float InfluenceRadiusMeters { get; }
    }

    /// <summary>
    /// Electromagnetic pulse payload emitted by fauna or environmental hazards.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ElectromagneticPulseEvent
    {
        public ElectromagneticPulseEvent(
            Vector3 runtimePosition,
            float radiusMeters,
            float durationSeconds,
            float claritySuppression01,
            uint damageType,
            ushort sourceId)
        {
            RuntimePosition = runtimePosition;
            RadiusMeters = radiusMeters;
            DurationSeconds = durationSeconds;
            ClaritySuppression01 = claritySuppression01;
            DamageType = damageType;
            SourceId = sourceId;
        }

        public Vector3 RuntimePosition { get; }
        public float RadiusMeters { get; }
        public float DurationSeconds { get; }
        public float ClaritySuppression01 { get; }
        public uint DamageType { get; }
        public ushort SourceId { get; }
    }

    /// <summary>
    /// False or authored acoustic ping payload consumed by sonar and PDA signal systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct AcousticPingEvent
    {
        /// <summary>
        /// Creates one transient acoustic ping payload.
        /// </summary>
        public AcousticPingEvent(
            Vector3 runtimePosition,
            float radiusMeters,
            float intensity01,
            float lifetimeSeconds,
            FieldTargetRole signalRole,
            int sourceSpeciesId)
        {
            RuntimePosition = runtimePosition;
            RadiusMeters = math.max(0f, radiusMeters);
            Intensity01 = math.saturate(intensity01);
            LifetimeSeconds = math.max(0f, lifetimeSeconds);
            SignalRole = signalRole;
            SourceSpeciesId = sourceSpeciesId;
        }

        /// <summary>Runtime-space origin of the ping.</summary>
        public Vector3 RuntimePosition { get; }

        /// <summary>World-space radius in authored meters.</summary>
        public float RadiusMeters { get; }

        /// <summary>Normalized signal intensity.</summary>
        public float Intensity01 { get; }

        /// <summary>Transient acoustic lifetime in seconds.</summary>
        public float LifetimeSeconds { get; }

        /// <summary>PDA-facing role label used by signal displays.</summary>
        public FieldTargetRole SignalRole { get; }

        /// <summary>Stable species id of the emitter.</summary>
        public int SourceSpeciesId { get; }
    }

    /// <summary>
    /// Physics event discriminator for <see cref="PhysicsEventPayload"/>.
    /// </summary>
    public enum PhysicsEventType : ushort
    {
        PressureImpulse = 1,
        ElectromagneticPulse = 2,
        AcousticPing = 3
    }

    /// <summary>
    /// Unmanaged event payload carried by the deferred physics event lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsEventPayload
    {
        public Vector3 RuntimePosition;
        public Vector3 Direction;
        public Vector3 ForceVector;
        public Vector3 ImpulseVector;
        public float RadiusMeters;
        public float Scalar0;
        public float Scalar1;
        public float Scalar2;
        public int PrimaryId;
        public uint DataHash;
        public uint StatusBits;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener for deferred pressure impulse events.
    /// </summary>
    public interface IPressureImpulseEventListener
    {
        void OnPressureImpulse(in PressureImpulseEvent pressureEvent);
    }

    /// <summary>
    /// Listener for deferred electromagnetic pulse events.
    /// </summary>
    public interface IElectromagneticPulseEventListener
    {
        void OnElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent);
    }

    /// <summary>
    /// Listener for deferred acoustic ping events.
    /// </summary>
    public interface IAcousticPingEventListener
    {
        void OnAcousticPing(in AcousticPingEvent pingEvent);
    }

    /// <summary>
    /// NativeQueue-backed physics-domain event surface for transient physics signals.
    /// </summary>
    public static class PhysicsEventBus
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 128;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("PhysicsEventBus.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("PhysicsEventBus"));

        // COLD ALLOC: RegistryBucket<IPressureImpulseEventListener>[32] - pressure impulse listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IPressureImpulseEventListener> _pressureListeners = new RegistryBucket<IPressureImpulseEventListener>(ListenerCapacity);
        // COLD ALLOC: RegistryBucket<IElectromagneticPulseEventListener>[32] - EMP listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IElectromagneticPulseEventListener> _empListeners = new RegistryBucket<IElectromagneticPulseEventListener>(ListenerCapacity);
        // COLD ALLOC: RegistryBucket<IAcousticPingEventListener>[32] - acoustic ping listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IAcousticPingEventListener> _acousticListeners = new RegistryBucket<IAcousticPingEventListener>(ListenerCapacity);
        private static NativeQueue<PhysicsEventPayload> _pendingEvents;
        private static NativeQueue<PhysicsEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _lastOverflowWarningFrame = -1;
        private static bool _isDispatching;

        /// <summary>Number of deferred physics event payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PhysicsEventBus), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PhysicsEventBus), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _pressureListeners.Clear();
            _empListeners.Clear();
            _acousticListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _lastOverflowWarningFrame = -1;
            _isDispatching = false;
        }

        /// <summary>Registers a pressure impulse listener.</summary>
        public static void Register(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_pressureListeners.Contains(listener))
                _pressureListeners.Register(listener);
        }

        /// <summary>Unregisters a pressure impulse listener.</summary>
        public static void Unregister(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            if (_pressureListeners.Contains(listener))
                _pressureListeners.Unregister(listener);
        }

        /// <summary>Registers an electromagnetic pulse listener.</summary>
        public static void Register(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_empListeners.Contains(listener))
                _empListeners.Register(listener);
        }

        /// <summary>Unregisters an electromagnetic pulse listener.</summary>
        public static void Unregister(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            if (_empListeners.Contains(listener))
                _empListeners.Unregister(listener);
        }

        /// <summary>Registers an acoustic ping listener.</summary>
        public static void Register(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            if (!_acousticListeners.Contains(listener))
                _acousticListeners.Register(listener);
        }

        /// <summary>Unregisters an acoustic ping listener.</summary>
        public static void Unregister(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            if (_acousticListeners.Contains(listener))
                _acousticListeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued physics events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out PhysicsEventPayload payload))
                        break;

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    Dispatch(in payload);
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        /// <summary>Broadcasts one pressure-impulse payload.</summary>
        public static void NotifyPressureImpulse(in PressureImpulseEvent pressureEvent)
        {
            Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = pressureEvent.RuntimePosition,
                Direction = pressureEvent.Direction,
                ForceVector = pressureEvent.ForceVectorNewtons,
                ImpulseVector = pressureEvent.ImpulseVectorNewtonSeconds,
                RadiusMeters = pressureEvent.InfluenceRadiusMeters,
                Scalar0 = pressureEvent.DoorAreaSquareMeters,
                Scalar1 = pressureEvent.HighPressureKPa,
                Scalar2 = pressureEvent.LowPressureKPa,
                PrimaryId = pressureEvent.DoorIndex,
                DataHash = 0u,
                StatusBits = 0u,
                EventType = (ushort)PhysicsEventType.PressureImpulse,
                Reserved = 0
            });
        }

        /// <summary>Broadcasts one EMP payload.</summary>
        public static void NotifyElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = pulseEvent.RuntimePosition,
                Direction = default,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = pulseEvent.RadiusMeters,
                Scalar0 = pulseEvent.DurationSeconds,
                Scalar1 = pulseEvent.ClaritySuppression01,
                Scalar2 = 0f,
                PrimaryId = 0,
                DataHash = pulseEvent.DamageType,
                StatusBits = pulseEvent.SourceId,
                EventType = (ushort)PhysicsEventType.ElectromagneticPulse,
                Reserved = 0
            });
        }

        /// <summary>Broadcasts one acoustic-ping payload.</summary>
        public static void NotifyAcousticPing(in AcousticPingEvent pingEvent)
        {
            Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = pingEvent.RuntimePosition,
                Direction = default,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = pingEvent.RadiusMeters,
                Scalar0 = pingEvent.Intensity01,
                Scalar1 = pingEvent.LifetimeSeconds,
                Scalar2 = 0f,
                PrimaryId = pingEvent.SourceSpeciesId,
                DataHash = 0u,
                StatusBits = unchecked((uint)pingEvent.SignalRole),
                EventType = (ushort)PhysicsEventType.AcousticPing,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PhysicsEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PhysicsEventPayload>[128] - deferred physics signal event lane flushed by SystemDispatcher LateUpdate - owner: PhysicsEventBus
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PhysicsEventBus),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PhysicsEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PhysicsEventPayload>[128] - next-frame physics events raised by listeners - owner: PhysicsEventBus
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PhysicsEventBus),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool Enqueue(in PhysicsEventPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
            }
            else
            {
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }

            return true;
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out PhysicsEventPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }

        private static void Dispatch(in PhysicsEventPayload payload)
        {
            switch ((PhysicsEventType)payload.EventType)
            {
                case PhysicsEventType.PressureImpulse:
                    DispatchPressureImpulse(in payload);
                    break;
                case PhysicsEventType.ElectromagneticPulse:
                    DispatchElectromagneticPulse(in payload);
                    break;
                case PhysicsEventType.AcousticPing:
                    DispatchAcousticPing(in payload);
                    break;
            }
        }

        private static void DispatchPressureImpulse(in PhysicsEventPayload payload)
        {
            int count = _pressureListeners.Count;
            if (count <= 0)
                return;

            PressureImpulseEvent pressureEvent = new PressureImpulseEvent(
                payload.PrimaryId,
                payload.RuntimePosition,
                payload.Direction,
                payload.Scalar0,
                payload.Scalar1,
                payload.Scalar2,
                payload.ForceVector,
                payload.ImpulseVector,
                payload.RadiusMeters);

            IPressureImpulseEventListener[] rawArray = _pressureListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IPressureImpulseEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnPressureImpulse(in pressureEvent);
            }
        }

        private static void DispatchElectromagneticPulse(in PhysicsEventPayload payload)
        {
            int count = _empListeners.Count;
            if (count <= 0)
                return;

            ElectromagneticPulseEvent pulseEvent = new ElectromagneticPulseEvent(
                payload.RuntimePosition,
                payload.RadiusMeters,
                payload.Scalar0,
                payload.Scalar1,
                payload.DataHash,
                unchecked((ushort)payload.StatusBits));

            IElectromagneticPulseEventListener[] rawArray = _empListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IElectromagneticPulseEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnElectromagneticPulse(in pulseEvent);
            }
        }

        private static void DispatchAcousticPing(in PhysicsEventPayload payload)
        {
            int count = _acousticListeners.Count;
            if (count <= 0)
                return;

            AcousticPingEvent pingEvent = new AcousticPingEvent(
                payload.RuntimePosition,
                payload.RadiusMeters,
                payload.Scalar0,
                payload.Scalar1,
                (FieldTargetRole)payload.StatusBits,
                payload.PrimaryId);

            IAcousticPingEventListener[] rawArray = _acousticListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IAcousticPingEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnAcousticPing(in pingEvent);
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }
    }

    [System.Flags]
    internal enum ForcePacketFlags : byte
    {
        None = 0,
        HasForce = 1 << 0,
        HasTorque = 1 << 1,
        WakeBody = 1 << 2,
        ApplyAtPosition = 1 << 3,
        BiomeBuoyancy = 1 << 4,
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ValidateForcePacketsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<ForcePacket> Packets;
        public NativeArray<byte> ValidityMask;
        public int MaxTrackedBodies;

        public void Execute(int index)
        {
            ForcePacket packet = Packets[index];
            if (packet.RigidbodyIndex < 0 || packet.RigidbodyIndex >= MaxTrackedBodies)
            {
                ValidityMask[index] = 0;
                return;
            }

            int mode = (int)packet.Mode;
            if (mode < (int)ForceMode.Force || mode > (int)ForceMode.VelocityChange)
            {
                ValidityMask[index] = 0;
                return;
            }

            ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
            bool validForce = (flags & ForcePacketFlags.HasForce) == 0 || IsFinite(packet.Force);
            bool validTorque = (flags & ForcePacketFlags.HasTorque) == 0 || IsFinite(packet.Torque);
            bool validPointOffset = IsFinite(packet.PointOffset);
            ValidityMask[index] = validForce && validTorque && validPointOffset ? (byte)1 : (byte)0;
        }

        private static bool IsFinite(Vector3 value)
        {
            float3 vector = new float3(value.x, value.y, value.z);
            return !math.any(math.isnan(vector)) && !math.any(math.isinf(vector)) && math.all(math.isfinite(vector));
        }
    }

    /// <summary>
    /// Authoritative main-thread owner for deferred Rigidbody force and torque application.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class PhysicsApplySystem : MonoBehaviour, IPhysicsService, IFixedTickable, IPostFixedTickable, ILateFrameTickable
    {
        private const int MaxTrackedBodies = 64;
        private const int MaxQueuedPackets = 512;
        private const int MaxQueuedSubmarineImpactSignals = 32;
        private const int MaxActiveDepressurizationVortices = 8;
        private const int DepressurizationVortexContactCapacity = 32;
        private const int ImplosionOverlapCapacity = 64;
        private const float MinMagnitudeSq = 0.000001f;
        private const float DepressurizationVortexDistanceFloorMeters = 0.5f;
        private const float HullYieldThresholdJoules = 225000f;
        private const string NonFiniteForceLog = "[PhysicsApplySystem] Non-finite force packet detected. Zeroing vector.";
        private const string NonFiniteTorqueLog = "[PhysicsApplySystem] Non-finite torque packet detected. Zeroing vector.";
        private const string NonFinitePointOffsetLog = "[PhysicsApplySystem] Non-finite point-offset packet detected. Zeroing offset.";
        private const string InvalidForcePacketLog = "[PhysicsApplySystem] Burst packet validation rejected a non-finite or out-of-range packet.";
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FixedTick");
        private static readonly ProfilerMarker _packetValidationProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.ValidatePackets");
        private static readonly ProfilerMarker _flushFrontBufferProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FlushFrontBuffer");
        // COLD ALLOC: Collider[64] — static implosion overlap query buffer for zero-GC radius impulse dispatch — owner: PhysicsApplySystem
        private static readonly Collider[] s_implosionOverlapBuffer = new Collider[ImplosionOverlapCapacity];

        private static PhysicsApplySystem _instance;

        private struct DeferredSubmarineImpactSignal
        {
            public float PreviousIntegrityNormalized;
            public float NextIntegrityNormalized;
            public DamageSignal Signal;
            public TraumaLevel TraumaLevel;
        }

        private struct DepressurizationVortex
        {
            public Vector3 RoomCenter;
            public Vector3 BreachPosition;
            public float RadiusMeters;
            public float BaseAccelerationMetersPerSecondSquared;
            public float MaximumAccelerationMetersPerSecondSquared;
            public float RemainingSeconds;
        }

        // COLD ALLOC: ForcePacket[512] — end-of-step flush buffer — owner: PhysicsApplySystem
        private ForcePacket[] _frontPackets = new ForcePacket[MaxQueuedPackets];
        // COLD ALLOC: Rigidbody[64] — active rigidbody slot map for deferred packet application — owner: PhysicsApplySystem
        private readonly Rigidbody[] _bodySlots = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: DepressurizationVortex[8] - active breach-vortex slots - owner: PhysicsApplySystem
        private readonly DepressurizationVortex[] _depressurizationVortices = new DepressurizationVortex[MaxActiveDepressurizationVortices];
        // COLD ALLOC: SpatialQueryHit[32] - spatial-hash scratch for breach-vortex loose-body collection - owner: PhysicsApplySystem
        private readonly SpatialQueryHit[] _depressurizationVortexContacts = new SpatialQueryHit[DepressurizationVortexContactCapacity];
        // COLD ALLOC: Rigidbody[32] - unique body scratch for breach-vortex force routing - owner: PhysicsApplySystem
        private readonly Rigidbody[] _depressurizationVortexBodies = new Rigidbody[DepressurizationVortexContactCapacity];
        // COLD ALLOC: List<Collider>[8] â€” submarine hull collider discovery for contact-modification enablement â€” owner: PhysicsApplySystem
        private readonly List<Collider> _submarineColliderScratch = new List<Collider>(8);
        private NativeArray<ForcePacket> _validationPackets;
        private NativeArray<byte> _validationMask;
        private NativeQueue<ForcePacket> _frontPacketQueue;
        private NativeQueue<ForcePacket> _backPacketQueue;
        private NativeQueue<DeferredSubmarineImpactSignal> _submarineImpactSignals;
        private int _submarineImpactSignalCount;

        private int _frontCount;
        private bool _isInitialized;
        private bool _fixedTickRegistered;
        private bool _postFixedTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _frontBufferValidationReady;
        private bool _packetValidationScheduled;
        private JobHandle _packetValidationHandle;
        private bool _contactModifySubscribed;
        private bool _submarineModifiableContactsArmed;
        private ulong _submarineHullEntityId;

        /// <summary>
        /// True once the service is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live physics apply instance.</returns>
        public static PhysicsApplySystem EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject runtimeRoot = new GameObject("[PhysicsApplySystem]");
            PhysicsApplySystem applySystem = runtimeRoot.AddComponent<PhysicsApplySystem>();
            return applySystem;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_isInitialized)
                return;

            GlobalRegistry.RegisterPhysicsService(this);
            _isInitialized = ReferenceEquals(GlobalRegistry.Physics, this);
        }

        /// <summary>
        /// Static fallback clear path used before the service is resolved into <see cref="GlobalRegistry"/>.
        /// </summary>
        public static void ClearQueuedPacketsStatic()
        {
            if (_instance != null)
                _instance.ClearQueuedPackets();
        }

        internal static bool TryGetForcePacketBackWriter(out NativeQueue<ForcePacket>.ParallelWriter writer)
        {
            writer = default;
            PhysicsApplySystem system = EnsureRuntimeInstance();
            if (system == null)
                return false;

            system.EnsureForcePacketQueues();
            if (!system._backPacketQueue.IsCreated)
                return false;

            writer = system._backPacketQueue.AsParallelWriter();
            return true;
        }

        internal static bool TriggerDepressurizationVortex(
            Vector3 roomCenter,
            Vector3 breachPosition,
            float radiusMeters,
            float baseAccelerationMetersPerSecondSquared,
            float maximumAccelerationMetersPerSecondSquared,
            float durationSeconds)
        {
            PhysicsApplySystem system = EnsureRuntimeInstance();
            return system.QueueDepressurizationVortex(
                roomCenter,
                breachPosition,
                radiusMeters,
                baseAccelerationMetersPerSecondSquared,
                maximumAccelerationMetersPerSecondSquared,
                durationSeconds);
        }

        internal static bool TriggerImplosionImpulse(
            Vector3 roomCenter,
            float radiusMeters,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds)
        {
            PhysicsApplySystem system = EnsureRuntimeInstance();
            return system.ApplyImplosionImpulse(
                roomCenter,
                radiusMeters,
                baseImpulseNewtonSeconds,
                maximumImpulseNewtonSeconds);
        }

        internal static void PrepareTrackedBodiesForOriginShift()
        {
            GlobalPhysicsStateManager.PrepareTrackedBodiesForOriginShift();
        }

        internal static void CommitTrackedBodiesForOriginShift(Vector3 shiftOffset)
        {
            GlobalPhysicsStateManager.CommitTrackedBodiesForOriginShift(shiftOffset);
        }

        internal static void FinalizeTrackedBodiesAfterOriginShift()
        {
            GlobalPhysicsStateManager.FinalizeTrackedBodiesAfterOriginShift();
        }

        internal static void ResetTrackedBodiesForSafeTeleport()
        {
            GlobalPhysicsStateManager.ResetTrackedBodiesForSafeTeleport();
        }

        /// <inheritdoc />
        public bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            return QueueForce(body, force, mode, ForcePacketPriority.Critical, wake);
        }

        internal bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, ForcePacketPriority priority, bool wake = true, ForcePacketFlags extraFlags = ForcePacketFlags.None)
        {
            if (!TrySanitizeVector(force, NonFiniteForceLog, out Vector3 sanitizedForce) ||
                sanitizedForce.sqrMagnitude <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;

            ForcePacket packet = new ForcePacket
            {
                Force = sanitizedForce,
                Torque = Vector3.zero,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasForce | extraFlags | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Force packet queue saturated.");
        }

        /// <inheritdoc />
        public bool QueueForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
        {
            return QueueForceAtPosition(body, force, worldPosition, mode, ForcePacketPriority.Critical, wake);
        }

        internal bool QueueForceAtPosition(
            Rigidbody body,
            Vector3 force,
            Vector3 worldPosition,
            ForceMode mode,
            ForcePacketPriority priority,
            bool wake = true,
            ForcePacketFlags extraFlags = ForcePacketFlags.None)
        {
            if (!TrySanitizeVector(force, NonFiniteForceLog, out Vector3 sanitizedForce) ||
                sanitizedForce.sqrMagnitude <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            float3 worldPosition3 = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!math.all(math.isfinite(worldPosition3)))
                return false;

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;

            Vector3 pointOffset = worldPosition - body.worldCenterOfMass;
            if (!IsFiniteNonZero(pointOffset))
                pointOffset = Vector3.zero;

            ForcePacket packet = new ForcePacket
            {
                Force = sanitizedForce,
                Torque = Vector3.zero,
                PointOffset = pointOffset,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasForce | ForcePacketFlags.ApplyAtPosition | extraFlags | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Point-force packet queue saturated.");
        }

        /// <inheritdoc />
        public bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            return QueueTorque(body, torque, mode, ForcePacketPriority.Critical, wake);
        }

        internal bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, ForcePacketPriority priority, bool wake = true)
        {
            if (!TrySanitizeVector(torque, NonFiniteTorqueLog, out Vector3 sanitizedTorque) ||
                sanitizedTorque.sqrMagnitude <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;

            ForcePacket packet = new ForcePacket
            {
                Force = Vector3.zero,
                Torque = sanitizedTorque,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasTorque | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Torque packet queue saturated.");
        }

        /// <summary>
        /// Queues a critically damped tractor-beam pull using reduced-mass PD velocity-change math.
        /// </summary>
        public bool QueueTractorBeamPd(
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            Vector3 targetPosition,
            Vector3 currentPosition,
            float springStiffness,
            float overDampingMultiplier,
            float maxForceMagnitude,
            bool applyReactionForce = true,
            bool wake = true)
        {
            if (payloadBody == null || payloadBody.isKinematic)
                return false;

            float3 targetPosition3 = new float3(targetPosition.x, targetPosition.y, targetPosition.z);
            float3 currentPosition3 = new float3(currentPosition.x, currentPosition.y, currentPosition.z);
            if (!math.all(math.isfinite(targetPosition3)) || !math.all(math.isfinite(currentPosition3)))
                return false;

            float payloadMass = math.max(payloadBody.mass, 0.0001f);
            bool anchorActsAsWorld = anchorBody == null || anchorBody.isKinematic;
            float anchorMass = anchorActsAsWorld ? payloadMass : math.max(anchorBody.mass, 0.0001f);
            float reducedMass = anchorActsAsWorld
                ? payloadMass
                : HectonContactJob.ResolveReducedMass(anchorMass, payloadMass, false);
            float dampingCoefficient = HectonContactJob.ResolveCriticalDamping(
                springStiffness,
                reducedMass,
                math.max(1f, overDampingMultiplier));

            Vector3 targetVelocity = anchorBody != null
                ? HectonPlayerMotor.SafeVelocity(anchorBody.GetPointVelocity(targetPosition))
                : Vector3.zero;
            Vector3 currentVelocity = HectonPlayerMotor.SafeVelocity(payloadBody.GetPointVelocity(currentPosition));
            float maxVelocityChangeMagnitude = math.max(0f, maxForceMagnitude) / math.max(0.0001f, reducedMass);
            float3 velocityChange3 = HectonContactJob.ResolveTractorBeamPdVelocityChange(
                targetPosition3,
                currentPosition3,
                new float3(targetVelocity.x, targetVelocity.y, targetVelocity.z),
                new float3(currentVelocity.x, currentVelocity.y, currentVelocity.z),
                springStiffness,
                dampingCoefficient,
                reducedMass,
                maxVelocityChangeMagnitude);
            Vector3 payloadVelocityChange = new Vector3(velocityChange3.x, velocityChange3.y, velocityChange3.z);
            if (!IsFiniteNonZero(payloadVelocityChange))
                return false;

            bool payloadQueued = PhysicsForceRouter.QueueForceAtPosition(
                payloadBody,
                payloadVelocityChange,
                currentPosition,
                ForceMode.VelocityChange,
                wake);
            if (applyReactionForce && anchorBody != null && !anchorBody.isKinematic)
            {
                float anchorVelocityScale = payloadMass / math.max(0.0001f, anchorMass);
                Vector3 anchorVelocityChange = -payloadVelocityChange * anchorVelocityScale;
                if (IsFiniteNonZero(anchorVelocityChange))
                {
                    PhysicsForceRouter.QueueForceAtPosition(
                        anchorBody,
                        anchorVelocityChange,
                        targetPosition,
                        ForceMode.VelocityChange,
                        wake);
                }
            }

            return payloadQueued;
        }

        private bool TryEnqueueBackPacket(in ForcePacket packet, string saturationMessage)
        {
            EnsureForcePacketQueues();
            if (!_backPacketQueue.IsCreated || _backPacketQueue.Count >= MaxQueuedPackets)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(saturationMessage);
#endif
                return false;
            }

            _backPacketQueue.Enqueue(packet);
            return true;
        }

        private bool QueueDepressurizationVortex(
            Vector3 roomCenter,
            Vector3 breachPosition,
            float radiusMeters,
            float baseAccelerationMetersPerSecondSquared,
            float maximumAccelerationMetersPerSecondSquared,
            float durationSeconds)
        {
            float3 roomCenter3 = new float3(roomCenter.x, roomCenter.y, roomCenter.z);
            float3 breachPosition3 = new float3(breachPosition.x, breachPosition.y, breachPosition.z);
            if (!math.all(math.isfinite(roomCenter3)) ||
                !math.all(math.isfinite(breachPosition3)) ||
                radiusMeters <= 0f ||
                baseAccelerationMetersPerSecondSquared <= 0f ||
                maximumAccelerationMetersPerSecondSquared <= 0f ||
                durationSeconds <= 0f)
            {
                return false;
            }

            int selectedSlot = -1;
            float shortestRemaining = float.MaxValue;
            for (int i = 0; i < _depressurizationVortices.Length; i++)
            {
                float remaining = _depressurizationVortices[i].RemainingSeconds;
                if (remaining <= 0f)
                {
                    selectedSlot = i;
                    break;
                }

                if (remaining < shortestRemaining)
                {
                    shortestRemaining = remaining;
                    selectedSlot = i;
                }
            }

            if (selectedSlot < 0)
                return false;

            _depressurizationVortices[selectedSlot] = new DepressurizationVortex
            {
                RoomCenter = roomCenter,
                BreachPosition = breachPosition,
                RadiusMeters = Mathf.Max(0.5f, radiusMeters),
                BaseAccelerationMetersPerSecondSquared = Mathf.Max(0f, baseAccelerationMetersPerSecondSquared),
                MaximumAccelerationMetersPerSecondSquared = Mathf.Max(0f, maximumAccelerationMetersPerSecondSquared),
                RemainingSeconds = Mathf.Max(0f, durationSeconds)
            };
            return true;
        }

        /// <inheritdoc />
        public void ClearQueuedPackets()
        {
            if (_packetValidationScheduled)
            {
                JobHandle.ScheduleBatchedJobs();
            }

            System.Array.Clear(_frontPackets, 0, _frontCount);
            System.Array.Clear(_bodySlots, 0, _bodySlots.Length);
            System.Array.Clear(_depressurizationVortices, 0, _depressurizationVortices.Length);
            System.Array.Clear(_depressurizationVortexBodies, 0, _depressurizationVortexBodies.Length);
            DrainForcePacketQueue(ref _frontPacketQueue);
            DrainForcePacketQueue(ref _backPacketQueue);
            _frontCount = 0;
            _frontBufferValidationReady = false;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureForcePacketQueues();
            EnsureValidationBuffers();
            if (!_submarineImpactSignals.IsCreated)
            {
                // COLD ALLOC: NativeQueue<DeferredSubmarineImpactSignal>(Persistent) â€” deferred submarine trauma queue flushed after contact modification â€” owner: PhysicsApplySystem
                _submarineImpactSignals = new NativeQueue<DeferredSubmarineImpactSignal>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _submarineImpactSignals,
                    MaxQueuedSubmarineImpactSignals,
                    nameof(PhysicsApplySystem),
                    nameof(_submarineImpactSignals),
                    NativeAllocationLifetime.Session);
            }

        }

        private void OnEnable()
        {
            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_fixedTickRegistered)
            {
                // Flush the previous validated packet snapshot before producers write this fixed step.
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.UI);
                _fixedTickRegistered = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_postFixedTickRegistered)
            {
                // Swap native packet queues only after all fixed-step producers have written to the back queue.
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.UI);
                _postFixedTickRegistered = SystemDispatcher.GetPostFixedLane(PriorityLayer.UI).Contains(this);
            }

            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_lateFrameTickRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameTickRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.UI).Contains(this);
            }

            if (!_contactModifySubscribed)
            {
                UnityEngine.Physics.ContactModifyEvent += HandleContactModifyEvent;
                UnityEngine.Physics.ContactModifyEventCCD += HandleContactModifyEvent;
                _contactModifySubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.UI);
                _fixedTickRegistered = false;
            }

            if (_postFixedTickRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.UI);
                _postFixedTickRegistered = false;
            }

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameTickRegistered = false;
            }

            if (_contactModifySubscribed)
            {
                UnityEngine.Physics.ContactModifyEvent -= HandleContactModifyEvent;
                UnityEngine.Physics.ContactModifyEventCCD -= HandleContactModifyEvent;
                _contactModifySubscribed = false;
            }
        }

        private void OnDestroy()
        {
            if (_fixedTickRegistered)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.UI);
                _fixedTickRegistered = false;
            }

            if (_postFixedTickRegistered)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.UI);
                _postFixedTickRegistered = false;
            }

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameTickRegistered = false;
            }

            if (_isInitialized && ReferenceEquals(GlobalRegistry.Physics, this))
            {
                GlobalRegistry.UnregisterPhysicsService(this);
                _isInitialized = false;
            }
            else
            {
                _isInitialized = false;
            }

            DisposeValidationBuffers();
            DisposeForcePacketQueues();

            if (_submarineImpactSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PhysicsApplySystem), nameof(_submarineImpactSignals));
                _submarineImpactSignals.Dispose();
                _submarineImpactSignalCount = 0;
            }

            if (_instance == this)
                _instance = null;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            using (_fixedTickProfilerMarker.Auto())
            {
                EnsureSubmarineModifiableContacts();
                PhysicsFrame.Tick();
                FlushValidatedFrontBuffer();
                ApplyDepressurizationVortices(fixedDeltaTime);
            }
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            if (_packetValidationScheduled)
                return;

            SwapForcePacketQueues();
            DrainFrontQueueToValidationSnapshot();
            ScheduleFrontPacketValidation();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteFrontPacketValidationInLateFrameSwapWindow();
            FlushDeferredSubmarineImpactSignals();
        }

        private void FlushValidatedFrontBuffer()
        {
            if (!_frontBufferValidationReady)
                return;

            using (_flushFrontBufferProfilerMarker.Auto())
            {
                for (int i = 0; i < _frontCount; i++)
                {
                    if (_validationMask.IsCreated && _validationMask[i] == 0)
                    {
                        ReportNonFinitePacket(InvalidForcePacketLog);
                        continue;
                    }

                    ForcePacket packet = _frontPackets[i];
                    Rigidbody body = ResolveBody(packet.RigidbodyIndex);
                    if (body == null || body.isKinematic)
                        continue;

                    ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
                    if (ShouldDiscardAmbientPacketForEntanglement(body, in packet, flags))
                        continue;

                    if ((flags & ForcePacketFlags.WakeBody) != 0 && body.IsSleeping())
                        body.WakeUp();

                    if ((flags & ForcePacketFlags.HasForce) != 0)
                    {
                        if (!TrySanitizeVector(packet.Force, NonFiniteForceLog, out Vector3 sanitizedForce))
                            sanitizedForce = Vector3.zero;
                        sanitizedForce = ApplyActiveBiomeBuoyancyGravityMultiplier(sanitizedForce, packet.Mode, flags);

                        if ((flags & ForcePacketFlags.ApplyAtPosition) != 0)
                        {
                            if (!TrySanitizeVector(packet.PointOffset, NonFinitePointOffsetLog, out Vector3 sanitizedOffset))
                                sanitizedOffset = Vector3.zero;

                            if (sanitizedForce.sqrMagnitude > MinMagnitudeSq)
                                body.AddForceAtPosition(sanitizedForce, body.worldCenterOfMass + sanitizedOffset, packet.Mode);
                        }
                        else if (sanitizedForce.sqrMagnitude > MinMagnitudeSq)
                        {
                            body.AddForce(sanitizedForce, packet.Mode);
                        }
                    }

                    if ((flags & ForcePacketFlags.HasTorque) != 0)
                    {
                        if (TrySanitizeVector(packet.Torque, NonFiniteTorqueLog, out Vector3 sanitizedTorque) &&
                            sanitizedTorque.sqrMagnitude > MinMagnitudeSq)
                        {
                            body.AddTorque(sanitizedTorque, packet.Mode);
                        }
                    }
                }

                System.Array.Clear(_frontPackets, 0, _frontCount);
                _frontCount = 0;
                _frontBufferValidationReady = false;
            }
        }

        private static bool ShouldDiscardAmbientPacketForEntanglement(Rigidbody body, in ForcePacket packet, ForcePacketFlags flags)
        {
            if (packet.Priority != ForcePacketPriority.Ambient ||
                (flags & ForcePacketFlags.HasForce) == 0 ||
                body == null)
            {
                return false;
            }

            return VehicleMotor.TryResolveForBody(body, out VehicleMotor vehicleMotor) &&
                   vehicleMotor.WouldAmbientForceExtendEntanglement(packet.Force, packet.Mode, math.max(Time.fixedDeltaTime, 0.0001f));
        }

        internal static Vector3 ApplyActiveBiomeBuoyancyGravityMultiplier(Vector3 force, ForceMode mode, ForcePacketFlags flags)
        {
            if ((flags & ForcePacketFlags.BiomeBuoyancy) == 0 ||
                mode != ForceMode.Acceleration ||
                !(force.y > 0f))
            {
                return force;
            }

            float multiplier = ResolveActiveBiomeGravityMultiplier();
            if (!(multiplier > 0f) || !float.IsFinite(multiplier) || Mathf.Abs(multiplier - 1f) <= 0.0001f)
                return force;

            return new Vector3(force.x, force.y * multiplier, force.z);
        }

        private static float ResolveActiveBiomeGravityMultiplier()
        {
            HectonBiomeMatrixProfile profile = BiomeMatrixDirector.ActiveRuntimeInstance != null
                ? BiomeMatrixDirector.ActiveRuntimeInstance.CurrentProfile
                : null;

            return profile != null ? profile.GravityMultiplier : 1f;
        }

        private void ApplyDepressurizationVortices(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f)
                return;

            for (int i = 0; i < _depressurizationVortices.Length; i++)
            {
                DepressurizationVortex vortex = _depressurizationVortices[i];
                if (vortex.RemainingSeconds <= 0f)
                    continue;

                ApplyDepressurizationVortex(in vortex);
                vortex.RemainingSeconds = Mathf.Max(0f, vortex.RemainingSeconds - fixedDeltaTime);
                if (vortex.RemainingSeconds <= 0f)
                    vortex = default;

                _depressurizationVortices[i] = vortex;
            }
        }

        private void ApplyDepressurizationVortex(in DepressurizationVortex vortex)
        {
            if (vortex.RadiusMeters <= 0f ||
                vortex.BaseAccelerationMetersPerSecondSquared <= 0f ||
                vortex.MaximumAccelerationMetersPerSecondSquared <= 0f)
            {
                return;
            }

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            if (playerTransform != null)
            {
                Vector3 playerPosition = playerTransform.position;
                if ((playerPosition - vortex.RoomCenter).sqrMagnitude <= vortex.RadiusMeters * vortex.RadiusMeters)
                {
                    Vector3 acceleration = ResolveDepressurizationVortexAcceleration(
                        playerPosition,
                        vortex.BreachPosition,
                        vortex.BaseAccelerationMetersPerSecondSquared,
                        vortex.MaximumAccelerationMetersPerSecondSquared);
                    if (acceleration.sqrMagnitude > MinMagnitudeSq)
                    {
                        if (playerMovement != null)
                            playerMovement.QueueSubsystemExternalAcceleration(acceleration);
                        else if (playerBody != null)
                            PhysicsForceRouter.QueueForce(playerBody, acceleration, ForceMode.Acceleration);
                    }
                }
            }

            ApplyDepressurizationVortexToLooseBodies(in vortex, playerBody);
        }

        private void ApplyDepressurizationVortexToLooseBodies(in DepressurizationVortex vortex, Rigidbody playerBody)
        {
            SpatialTargetKind kindMask = SpatialTargetKind.Pickup | SpatialTargetKind.Resource | SpatialTargetKind.Scannable;
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                vortex.RoomCenter,
                vortex.RadiusMeters,
                kindMask,
                _depressurizationVortexContacts);
            int uniqueBodyCount = 0;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _depressurizationVortexContacts[hitIndex];
                if (!TryResolveDynamicBody(hit.Owner, hit.Transform, out Rigidbody body) || body == null)
                    continue;

                if (ReferenceEquals(body, playerBody))
                    continue;

                bool duplicateBody = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (!ReferenceEquals(_depressurizationVortexBodies[uniqueIndex], body))
                        continue;

                    duplicateBody = true;
                    break;
                }

                if (duplicateBody)
                    continue;

                _depressurizationVortexBodies[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= _depressurizationVortexBodies.Length)
                    break;
            }

            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _depressurizationVortexBodies[bodyIndex];
                _depressurizationVortexBodies[bodyIndex] = null;
                if (body == null || body.isKinematic)
                    continue;

                Vector3 acceleration = ResolveDepressurizationVortexAcceleration(
                    body.worldCenterOfMass,
                    vortex.BreachPosition,
                    vortex.BaseAccelerationMetersPerSecondSquared,
                    vortex.MaximumAccelerationMetersPerSecondSquared);
                if (acceleration.sqrMagnitude > MinMagnitudeSq)
                    QueueForce(body, acceleration, ForceMode.Acceleration);
            }
        }

        private bool ApplyImplosionImpulse(
            Vector3 roomCenter,
            float radiusMeters,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds)
        {
            float3 roomCenter3 = new float3(roomCenter.x, roomCenter.y, roomCenter.z);
            if (!math.all(math.isfinite(roomCenter3)) ||
                radiusMeters <= 0f ||
                baseImpulseNewtonSeconds <= 0f ||
                maximumImpulseNewtonSeconds <= 0f)
            {
                return false;
            }

            float safeRadius = Mathf.Max(0.5f, radiusMeters);
            float safeBaseImpulse = Mathf.Max(0f, baseImpulseNewtonSeconds);
            float safeMaximumImpulse = Mathf.Max(0f, maximumImpulseNewtonSeconds);
            Rigidbody playerBody = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerContext != null)
                playerBody = playerContext.PlayerRigidbody;

            if (playerTransform != null)
            {
                Vector3 playerPosition = playerTransform.position;
                if ((playerPosition - roomCenter).sqrMagnitude <= safeRadius * safeRadius)
                {
                    Vector3 impulse = ResolveImplosionImpulse(
                        playerPosition,
                        roomCenter,
                        safeBaseImpulse,
                        safeMaximumImpulse);
                    if (impulse.sqrMagnitude > MinMagnitudeSq)
                    {
                        if (playerMovement != null)
                        {
                            float mass = playerBody != null ? Mathf.Max(1f, playerBody.mass) : 80f;
                            playerMovement.QueueSubsystemExternalVelocityChange(impulse / mass);
                        }
                        else if (playerBody != null)
                        {
                            QueueForce(playerBody, impulse, ForceMode.Impulse);
                        }
                    }
                }
            }

            ApplyImplosionImpulseToLooseBodies(roomCenter, safeRadius, safeBaseImpulse, safeMaximumImpulse, playerBody);
            return true;
        }

        private void ApplyImplosionImpulseToLooseBodies(
            Vector3 roomCenter,
            float radiusMeters,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds,
            Rigidbody playerBody)
        {
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                roomCenter,
                radiusMeters,
                s_implosionOverlapBuffer,
                HectonLayerMasks.BaseModuleLayerMask |
                HectonLayerMasks.DroppedItemLayerMask |
                HectonLayerMasks.CreatureLayerMask |
                HectonLayerMasks.VehicleLayerMask |
                HectonLayerMasks.DebrisLayerMask,
                QueryTriggerInteraction.Ignore);
            int uniqueBodyCount = 0;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hitCollider = s_implosionOverlapBuffer[hitIndex];
                if (hitCollider == null)
                    continue;

                Rigidbody body = hitCollider.attachedRigidbody;
                if (body == null || body.isKinematic || ReferenceEquals(body, playerBody))
                    continue;

                bool duplicateBody = false;
                for (int uniqueIndex = 0; uniqueIndex < uniqueBodyCount; uniqueIndex++)
                {
                    if (!ReferenceEquals(_depressurizationVortexBodies[uniqueIndex], body))
                        continue;

                    duplicateBody = true;
                    break;
                }

                if (duplicateBody)
                    continue;

                _depressurizationVortexBodies[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= _depressurizationVortexBodies.Length)
                    break;
            }

            for (int hitIndex = 0; hitIndex < s_implosionOverlapBuffer.Length; hitIndex++)
                s_implosionOverlapBuffer[hitIndex] = null;

            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _depressurizationVortexBodies[bodyIndex];
                _depressurizationVortexBodies[bodyIndex] = null;
                if (body == null || body.isKinematic)
                    continue;

                Vector3 impulse = ResolveImplosionImpulse(
                    body.worldCenterOfMass,
                    roomCenter,
                    baseImpulseNewtonSeconds,
                    maximumImpulseNewtonSeconds);
                if (impulse.sqrMagnitude > MinMagnitudeSq)
                    QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static Vector3 ResolveDepressurizationVortexAcceleration(
            Vector3 bodyPosition,
            Vector3 breachPosition,
            float baseAccelerationMetersPerSecondSquared,
            float maximumAccelerationMetersPerSecondSquared)
        {
            Vector3 toBreach = breachPosition - bodyPosition;
            float distanceMeters = toBreach.magnitude;
            if (distanceMeters <= 0.0001f)
                return Vector3.zero;

            float safeDistance = Mathf.Max(DepressurizationVortexDistanceFloorMeters, distanceMeters);
            float accelerationMagnitude = Mathf.Min(maximumAccelerationMetersPerSecondSquared, baseAccelerationMetersPerSecondSquared / safeDistance);
            return toBreach * (accelerationMagnitude / distanceMeters);
        }

        private static Vector3 ResolveImplosionImpulse(
            Vector3 bodyPosition,
            Vector3 roomCenter,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds)
        {
            Vector3 toCenter = roomCenter - bodyPosition;
            float distanceMeters = toCenter.magnitude;
            if (distanceMeters <= 0.0001f)
                return Vector3.zero;

            float safeDistance = Mathf.Max(DepressurizationVortexDistanceFloorMeters, distanceMeters);
            float impulseMagnitude = Mathf.Min(maximumImpulseNewtonSeconds, baseImpulseNewtonSeconds / safeDistance);
            return toCenter * (impulseMagnitude / distanceMeters);
        }

        private static bool TryResolveDynamicBody(Component owner, Transform runtimeTransform, out Rigidbody body)
        {
            body = null;
            if (owner != null && owner.TryGetComponent(out body))
                return body != null;

            if (runtimeTransform != null && runtimeTransform.TryGetComponent(out body))
                return body != null;

            return false;
        }

        private void SwapForcePacketQueues()
        {
            EnsureForcePacketQueues();
            NativeQueue<ForcePacket> swap = _frontPacketQueue;
            _frontPacketQueue = _backPacketQueue;
            _backPacketQueue = swap;
        }

        private void DrainFrontQueueToValidationSnapshot()
        {
            System.Array.Clear(_frontPackets, 0, _frontCount);
            _frontCount = 0;

            while (_frontPacketQueue.IsCreated &&
                   _frontPacketQueue.TryDequeue(out ForcePacket packet))
            {
                if (_frontCount >= _frontPackets.Length)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[PhysicsApplySystem] Native front packet queue exceeded validation snapshot capacity.");
#endif
                    continue;
                }

                _frontPackets[_frontCount++] = packet;
            }
        }

        private void EnsureForcePacketQueues()
        {
            if (!_frontPacketQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<ForcePacket>(Persistent) - read-side force packet buffer drained only after post-fixed swap - owner: PhysicsApplySystem
                _frontPacketQueue = new NativeQueue<ForcePacket>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _frontPacketQueue,
                    MaxQueuedPackets,
                    nameof(PhysicsApplySystem),
                    nameof(_frontPacketQueue),
                    NativeAllocationLifetime.Session);
            }

            if (!_backPacketQueue.IsCreated)
            {
                // COLD ALLOC: NativeQueue<ForcePacket>(Persistent) - write-side force packet buffer used by fixed-step producers - owner: PhysicsApplySystem
                _backPacketQueue = new NativeQueue<ForcePacket>(Allocator.Persistent);
                NativeMemorySentinel.RegisterNativeQueue(
                    _backPacketQueue,
                    MaxQueuedPackets,
                    nameof(PhysicsApplySystem),
                    nameof(_backPacketQueue),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void DrainForcePacketQueue(ref NativeQueue<ForcePacket> queue)
        {
            if (!queue.IsCreated)
                return;

            while (queue.TryDequeue(out _))
            {
            }
        }

        private void EnsureValidationBuffers()
        {
            if (!_validationPackets.IsCreated)
            {
                // COLD ALLOC: NativeArray<ForcePacket>[512] â€” Burst packet validation staging buffer â€” owner: PhysicsApplySystem
                _validationPackets = new NativeArray<ForcePacket>(MaxQueuedPackets, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _validationPackets,
                    nameof(PhysicsApplySystem),
                    nameof(_validationPackets),
                    NativeAllocationLifetime.Session);
            }

            if (!_validationMask.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[512] â€” Burst packet validation mask â€” owner: PhysicsApplySystem
                _validationMask = new NativeArray<byte>(MaxQueuedPackets, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                NativeMemorySentinel.RegisterNativeArray(
                    _validationMask,
                    nameof(PhysicsApplySystem),
                    nameof(_validationMask),
                    NativeAllocationLifetime.Session);
            }
        }

        private void EnsureSubmarineModifiableContacts()
        {
            ISubmarineRuntimeContext submarineContext = GlobalRegistry.Submarine;
            Rigidbody hullBody = submarineContext != null ? submarineContext.HullRigidbody : null;
            ulong hullEntityId = hullBody != null ? EntityId.ToULong(hullBody.GetEntityId()) : 0ul;
            if (hullBody == null || hullEntityId == 0ul)
            {
                _submarineModifiableContactsArmed = false;
                _submarineHullEntityId = 0ul;
                return;
            }

            if (_submarineModifiableContactsArmed && _submarineHullEntityId == hullEntityId)
                return;

            _submarineHullEntityId = hullEntityId;
            _submarineColliderScratch.Clear();
            hullBody.GetComponentsInChildren(true, _submarineColliderScratch);
            for (int i = 0; i < _submarineColliderScratch.Count; i++)
            {
                Collider collider = _submarineColliderScratch[i];
                if (collider == null)
                    continue;

                collider.hasModifiableContacts = true;
            }

            _submarineModifiableContactsArmed = _submarineColliderScratch.Count > 0;
        }

        private void HandleContactModifyEvent(PhysicsScene scene, NativeArray<ModifiableContactPair> pairs)
        {
            ISubmarineRuntimeContext submarineContext = GlobalRegistry.Submarine;
            if (submarineContext == null || pairs.Length <= 0)
                return;

            Rigidbody hullBody = submarineContext.HullRigidbody;
            SubmarineStructuralGrid structuralGrid = submarineContext.StructuralGrid;
            if (hullBody == null || structuralGrid == null)
                return;

            ulong hullEntityId = EntityId.ToULong(hullBody.GetEntityId());
            if (hullEntityId == 0ul)
                return;

            float hullMass = math.max(hullBody.mass, 0.0001f);
            float depthMeters = submarineContext.FluidDynamics != null
                ? math.max(0f, submarineContext.FluidDynamics.ExternalDepthMeters)
                : 0f;

            for (int pairIndex = 0; pairIndex < pairs.Length; pairIndex++)
            {
                ModifiableContactPair pair = pairs[pairIndex];
                bool submarineIsBody = EntityId.ToULong(pair.bodyEntityId) == hullEntityId;
                bool submarineIsOtherBody = EntityId.ToULong(pair.otherBodyEntityId) == hullEntityId;
                if (!submarineIsBody && !submarineIsOtherBody)
                    continue;

                int contactCount = pair.contactCount;
                if (contactCount <= 0)
                    continue;

                Vector3 point = pair.GetPoint(0);
                Vector3 normal = pair.GetNormal(0);
                if (normal.sqrMagnitude <= MinMagnitudeSq)
                    normal = Vector3.up;

                float3 hullVelocity = submarineIsBody ? (float3)pair.bodyVelocity : (float3)pair.otherBodyVelocity;
                float3 otherVelocity = submarineIsBody ? (float3)pair.otherBodyVelocity : (float3)pair.bodyVelocity;
                HectonContactJob.InelasticImpactResult impact = HectonContactJob.ResolveInelasticImpact(
                    hullMass,
                    hullVelocity,
                    otherVelocity,
                    normal,
                    contactCount,
                    HullYieldThresholdJoules);
                if (!impact.ExceedsYield)
                    continue;

                Vector3 tangentialVelocity = new Vector3(
                    impact.TangentialVelocity.x,
                    impact.TangentialVelocity.y,
                    impact.TangentialVelocity.z);

                for (int contactIndex = 0; contactIndex < contactCount; contactIndex++)
                {
                    pair.SetBounciness(contactIndex, 0f);
                    pair.SetMaxImpulse(contactIndex, impact.MaxImpulsePerContact);
                    pair.SetTargetVelocity(contactIndex, tangentialVelocity);
                }

                Vector3 impactNormalWorld = submarineIsBody ? normal : -normal;
                float3 localPoint = structuralGrid.transform.InverseTransformPoint(point);
                float3 localNormal = math.normalizesafe(
                    (float3)structuralGrid.transform.InverseTransformDirection(impactNormalWorld),
                    new float3(0f, 1f, 0f));
                structuralGrid.QueueImpactLocal(localPoint, impact.RelativeSpeedMetersPerSecond, impact.IntegrityDelta);
                structuralGrid.QueueHullDentLocal(localPoint, localNormal, impact.RelativeSpeedMetersPerSecond, impact.Severity01);
                EnqueueSubmarineImpactSignal(localPoint, impact.RelativeSpeedMetersPerSecond, impact.Severity01, impact.IntegrityDelta, depthMeters);
            }
        }

        private void EnqueueSubmarineImpactSignal(
            float3 localPoint,
            float impactSpeedMetersPerSecond,
            float severity01,
            byte integrityDelta,
            float depthMeters)
        {
            if (!_submarineImpactSignals.IsCreated || _submarineImpactSignalCount >= MaxQueuedSubmarineImpactSignals)
                return;

            DamageSignal signal = default;
            signal.magnitude = math.max(0f, impactSpeedMetersPerSecond);
            signal.localPoint = localPoint;
            signal.damageType = (uint)DamageTypeMask.Impact;
            signal.integrityDelta = integrityDelta;
            signal.depth = math.max(0f, depthMeters);
            signal.sourceID = DamageSourceIds.SubmarineImpact;

            _submarineImpactSignals.Enqueue(new DeferredSubmarineImpactSignal
            {
                PreviousIntegrityNormalized = 1f,
                NextIntegrityNormalized = math.saturate(1f - severity01),
                Signal = signal,
                TraumaLevel = ResolveSubmarineTraumaLevel(severity01)
            });
            _submarineImpactSignalCount++;
        }

        private void FlushDeferredSubmarineImpactSignals()
        {
            if (!_submarineImpactSignals.IsCreated)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            TraumaDispatcher traumaDispatcher = null;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform != null)
                playerTransform.TryGetComponent(out traumaDispatcher);
            if (traumaDispatcher == null)
                return;

            int scanBudget = _submarineImpactSignalCount > 0 ? _submarineImpactSignalCount : MaxQueuedSubmarineImpactSignals;
            while (scanBudget > 0 && !_submarineImpactSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_submarineImpactSignals.TryDequeue(out DeferredSubmarineImpactSignal queuedSignal))
                    break;

                if (_submarineImpactSignalCount > 0)
                    _submarineImpactSignalCount--;
                scanBudget--;
                traumaDispatcher.OnIntegrityChanged(
                    queuedSignal.PreviousIntegrityNormalized,
                    queuedSignal.NextIntegrityNormalized,
                    queuedSignal.Signal);
                traumaDispatcher.OnTraumaThresholdCrossed(queuedSignal.TraumaLevel);
            }

            if (_submarineImpactSignals.IsEmpty())
                _submarineImpactSignalCount = 0;
        }

        private static TraumaLevel ResolveSubmarineTraumaLevel(float severity01)
        {
            if (severity01 >= 0.9f)
                return TraumaLevel.Catastrophic;
            if (severity01 >= 0.65f)
                return TraumaLevel.Critical;
            if (severity01 >= 0.4f)
                return TraumaLevel.Significant;
            if (severity01 >= 0.15f)
                return TraumaLevel.Minor;

            return TraumaLevel.None;
        }

        private void ScheduleFrontPacketValidation()
        {
            if (_frontCount <= 0)
            {
                _frontBufferValidationReady = false;
                return;
            }

            EnsureValidationBuffers();
            for (int i = 0; i < _frontCount; i++)
                _validationPackets[i] = _frontPackets[i];

            ValidateForcePacketsJob validateJob = new ValidateForcePacketsJob
            {
                Packets = _validationPackets,
                ValidityMask = _validationMask,
                MaxTrackedBodies = _bodySlots.Length
            };

            _packetValidationHandle = validateJob.Schedule(_frontCount, 32);
            _packetValidationScheduled = true;
            _frontBufferValidationReady = false;
        }

        private void CompleteFrontPacketValidationInLateFrameSwapWindow()
        {
            if (!_packetValidationScheduled || !_packetValidationHandle.IsCompleted)
                return;

            using (_packetValidationProfilerMarker.Auto())
            {
                if (!DispatcherJobSwap.TryComplete(ref _packetValidationHandle, forceComplete: false))
                    return;

                _packetValidationScheduled = false;
                _frontBufferValidationReady = _frontCount > 0;
            }
        }

        private int ResolveBodyIndex(Rigidbody body)
        {
            for (int i = 0; i < _bodySlots.Length; i++)
            {
                Rigidbody slot = _bodySlots[i];
                if (slot == body)
                    return i;

                if (slot == null)
                {
                    _bodySlots[i] = body;
                    return i;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PhysicsApplySystem] Rigidbody slot capacity exceeded.");
#endif
            return -1;
        }

        private Rigidbody ResolveBody(int rigidbodyIndex)
        {
            if ((uint)rigidbodyIndex >= (uint)_bodySlots.Length)
                return null;

            return _bodySlots[rigidbodyIndex];
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinMagnitudeSq;
        }

        private static bool TrySanitizeVector(Vector3 value, string errorMessage, out Vector3 sanitized)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            if (math.any(math.isnan(value3)) || math.any(math.isinf(value3)) || !math.all(math.isfinite(value3)))
            {
                ReportNonFinitePacket(errorMessage);
                sanitized = Vector3.zero;
                return false;
            }

            sanitized = value;
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void ReportNonFinitePacket(string message)
        {
            NativeAllocationTrackerRuntimeBridge.ReportLeak(message);
            Debug.LogError(message);
        }

        private void DisposeValidationBuffers()
        {
            JobHandle dependency = _packetValidationScheduled ? _packetValidationHandle : default;
            if (_validationPackets.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationPackets);
                dependency = _validationPackets.Dispose(dependency);
                _validationPackets = default;
            }

            if (_validationMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_validationMask);
                dependency = _validationMask.Dispose(dependency);
                _validationMask = default;
            }

            _packetValidationHandle = dependency;
            _packetValidationScheduled = false;
        }

        private void DisposeForcePacketQueues()
        {
            if (_frontPacketQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PhysicsApplySystem), nameof(_frontPacketQueue));
                _frontPacketQueue.Dispose();
                _frontPacketQueue = default;
            }

            if (_backPacketQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PhysicsApplySystem), nameof(_backPacketQueue));
                _backPacketQueue.Dispose();
                _backPacketQueue = default;
            }
        }
    }

    /// <summary>
    /// Common physics routing facade that keeps player-body writes inside <see cref="IMotorForces"/>
    /// and routes all other rigidbody writes through <see cref="PhysicsApplySystem"/>.
    /// </summary>
    public static class PhysicsForceRouter
    {
        private const float MaxSafeAcceleration = 50f;

        internal static bool ApplyKinematicWeldSnap(Rigidbody body, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (body == null ||
                !IsFiniteVector(targetPosition) ||
                !IsFiniteQuaternion(targetRotation))
            {
                return false;
            }

            bool wasKinematic = body.isKinematic;
            Vector3 correction = targetPosition - body.position;
            if (!wasKinematic && correction.sqrMagnitude > 0.000001f)
            {
                float fixedDeltaTime = math.max(Time.fixedDeltaTime, 0.0001f);
                body.AddForce(correction / fixedDeltaTime, ForceMode.VelocityChange);
            }

            body.useGravity = false;
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = targetPosition;
            body.rotation = targetRotation;
            body.Sleep();
            return true;
        }

        /// <summary>
        /// Routes a force request either into the player motor owner or the deferred packet system.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before application.</param>
        /// <returns>True when the request was accepted.</returns>
        public static bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            Vector3 safeForce = ClampUpwardAcceleration(force, mode);
            if (TryRouteToPlayerMotor(body, safeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForce(body, safeForce, mode, wake);
        }

        /// <summary>
        /// Routes an ambient environmental force into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            Vector3 safeForce = ClampUpwardAcceleration(force, mode);
            ForcePacketFlags extraFlags = ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = PhysicsApplySystem.ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToPlayerMotor(body, routeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForce(body, safeForce, mode, ForcePacketPriority.Ambient, wake, extraFlags);
        }

        /// <summary>
        /// Routes a force-at-position request either into the player motor owner or the deferred packet system.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="force">World-space force vector.</param>
        /// <param name="worldPosition">World-space application point.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before application.</param>
        /// <returns>True when the request was accepted.</returns>
        public static bool QueueForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
        {
            Vector3 safeForce = ClampUpwardAcceleration(force, mode);
            if (TryRouteToPlayerMotorAtPosition(body, safeForce, worldPosition))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForceAtPosition(body, safeForce, worldPosition, mode, wake);
        }

        /// <summary>
        /// Routes an ambient environmental force-at-position into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
        {
            Vector3 safeForce = ClampUpwardAcceleration(force, mode);
            ForcePacketFlags extraFlags = ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = PhysicsApplySystem.ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToPlayerMotorAtPosition(body, routeForce, worldPosition))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForceAtPosition(body, safeForce, worldPosition, mode, ForcePacketPriority.Ambient, wake, extraFlags);
        }

        /// <summary>
        /// Routes a reduced-mass critically damped tractor-beam force through deferred physics packets.
        /// </summary>
        public static bool QueueTractorBeamPd(
            Rigidbody anchorBody,
            Rigidbody payloadBody,
            Vector3 targetPosition,
            Vector3 currentPosition,
            float springStiffness,
            float overDampingMultiplier,
            float maxForceMagnitude,
            bool applyReactionForce = true,
            bool wake = true)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueTractorBeamPd(
                anchorBody,
                payloadBody,
                targetPosition,
                currentPosition,
                springStiffness,
                overDampingMultiplier,
                maxForceMagnitude,
                applyReactionForce,
                wake);
        }

        /// <summary>
        /// Routes a torque request into the deferred packet system.
        /// </summary>
        /// <param name="body">Target rigidbody.</param>
        /// <param name="torque">World-space torque vector.</param>
        /// <param name="mode">Force application mode.</param>
        /// <param name="wake">True to wake sleeping bodies before application.</param>
        /// <returns>True when the request was accepted.</returns>
        public static bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueTorque(body, torque, mode, wake);
        }

        /// <summary>
        /// Routes ambient environmental torque into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueTorque(body, torque, mode, ForcePacketPriority.Ambient, wake);
        }

        private static bool TryRouteToPlayerMotor(Rigidbody body, Vector3 force, ForceMode mode)
        {
            if (body == null)
                return false;

            if (body.TryGetComponent(out HectonPlayerMovement playerMovement))
            {
                float bodyMass = math.max(body.mass, 0.0001f);
                switch (mode)
                {
                    case ForceMode.Force:
                        playerMovement.QueueSubsystemExternalAcceleration(force / bodyMass);
                        return true;

                    case ForceMode.Acceleration:
                        playerMovement.QueueSubsystemExternalAcceleration(force);
                        return true;

                    case ForceMode.Impulse:
                        playerMovement.QueueSubsystemExternalVelocityChange(force / bodyMass);
                        return true;

                    case ForceMode.VelocityChange:
                        playerMovement.QueueSubsystemExternalVelocityChange(force);
                        return true;
                }

                return false;
            }

            if (!body.TryGetComponent(out HectonPlayerMotor playerMotor))
                return false;

            float playerMotorMass = math.max(body.mass, 0.0001f);
            switch (mode)
            {
                case ForceMode.Force:
                    playerMotor.AddExternalAcceleration(force / playerMotorMass);
                    return true;

                case ForceMode.Acceleration:
                    playerMotor.AddExternalAcceleration(force);
                    return true;

                case ForceMode.Impulse:
                    playerMotor.AddExternalVelocityChange(force / playerMotorMass);
                    return true;

                case ForceMode.VelocityChange:
                    playerMotor.AddExternalVelocityChange(force);
                    return true;
            }

            return false;
        }

        private static ForcePacketFlags ResolveBiomeBuoyancyFlags(Vector3 force, ForceMode mode)
        {
            return mode == ForceMode.Acceleration && force.y > 0f
                ? ForcePacketFlags.BiomeBuoyancy
                : ForcePacketFlags.None;
        }

        private static Vector3 ClampUpwardAcceleration(Vector3 force, ForceMode mode)
        {
            if (mode == ForceMode.Acceleration && force.y > MaxSafeAcceleration)
                force.y = MaxSafeAcceleration;

            return force;
        }

        private static bool TryRouteToPlayerMotorAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition)
        {
            if (body == null || !body.TryGetComponent(out HectonPlayerMotor playerMotor))
                return false;

            if (!IsFiniteNonZeroForce(force))
                return false;

            playerMotor.ApplyForceAtPositionSplit(force, worldPosition, 1.25f, 45f);
            return true;
        }

        private static bool IsFiniteNonZeroForce(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > 0.000001f;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(value4)) && math.lengthsq(value4) > 0.000001f;
        }
    }
}
