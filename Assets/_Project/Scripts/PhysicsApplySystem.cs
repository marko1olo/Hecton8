using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
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
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
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
            int sourceSpeciesId,
            float energyJoules = 0f)
        {
            RuntimePosition = runtimePosition;
            RadiusMeters = math.max(0f, radiusMeters);
            Intensity01 = math.saturate(intensity01);
            LifetimeSeconds = math.max(0f, lifetimeSeconds);
            SignalRole = signalRole;
            SourceSpeciesId = sourceSpeciesId;
            EnergyJoules = math.max(0f, energyJoules);
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

        /// <summary>Authored or measured acoustic energy used to reject spoof pings.</summary>
        public float EnergyJoules { get; }
    }

    /// <summary>
    /// Flags carried by kinetic acoustic impulses raised from validated critical force packets.
    /// </summary>
    [System.Flags]
    public enum AcousticImpulseFlags : byte
    {
        None = 0,
        Critical = 1 << 0,
        Leviathan = 1 << 1,
        PlayerCollision = 1 << 2,
        Large = 1 << 3
    }

    /// <summary>
    /// Kinetic acoustic impulse payload emitted by physics and consumed by audio, HUD, haptics, and passive radar.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public readonly struct AcousticImpulseEvent
    {
        public AcousticImpulseEvent(
            Vector3 runtimePosition,
            Vector3 direction,
            float kineticEnergyJoules,
            float volume01,
            float pitchScale,
            float radiusMeters,
            int sourceBodyInstanceId,
            byte audioMaterialId,
            AcousticImpulseFlags flags)
        {
            RuntimePosition = runtimePosition;
            Direction = DominantAxisOrDefault(direction);
            KineticEnergyJoules = math.max(0f, kineticEnergyJoules);
            Volume01 = math.saturate(volume01);
            PitchScale = math.clamp(pitchScale, 0.05f, 4f);
            RadiusMeters = math.max(0f, radiusMeters);
            SourceBodyInstanceId = sourceBodyInstanceId;
            AudioMaterialId = audioMaterialId;
            Flags = flags;
        }

        public Vector3 RuntimePosition { get; }
        public Vector3 Direction { get; }
        public float KineticEnergyJoules { get; }
        public float Volume01 { get; }
        public float PitchScale { get; }
        public float RadiusMeters { get; }
        public int SourceBodyInstanceId { get; }
        public byte AudioMaterialId { get; }
        public AcousticImpulseFlags Flags { get; }

        private static Vector3 DominantAxisOrDefault(Vector3 value)
        {
            float3 vector = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(vector)))
                return Vector3.forward;

            float ax = math.abs(vector.x);
            float ay = math.abs(vector.y);
            float az = math.abs(vector.z);
            if ((ax + ay + az) <= 0.000001f)
                return Vector3.forward;

            if (ax >= ay && ax >= az)
                return vector.x < 0f ? Vector3.left : Vector3.right;

            if (ay >= az)
                return vector.y < 0f ? Vector3.down : Vector3.up;

            return vector.z < 0f ? Vector3.back : Vector3.forward;
        }
    }

    /// <summary>
    /// Active-sonar danger payload routed through the acoustic impulse lane with the Large flag forced on.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public readonly struct LargeAcousticImpulseEvent
    {
        public LargeAcousticImpulseEvent(
            Vector3 runtimePosition,
            Vector3 direction,
            float kineticEnergyJoules,
            float volume01,
            float pitchScale,
            float radiusMeters,
            int sourceBodyInstanceId,
            byte audioMaterialId,
            AcousticImpulseFlags flags)
        {
            RuntimePosition = runtimePosition;
            Direction = direction;
            KineticEnergyJoules = math.max(0f, kineticEnergyJoules);
            Volume01 = math.saturate(volume01);
            PitchScale = math.clamp(pitchScale, 0.05f, 4f);
            RadiusMeters = math.max(0f, radiusMeters);
            SourceBodyInstanceId = sourceBodyInstanceId;
            AudioMaterialId = audioMaterialId;
            Flags = flags | AcousticImpulseFlags.Large;
        }

        public readonly Vector3 RuntimePosition;
        public readonly Vector3 Direction;
        public readonly float KineticEnergyJoules;
        public readonly float Volume01;
        public readonly float PitchScale;
        public readonly float RadiusMeters;
        public readonly int SourceBodyInstanceId;
        public readonly byte AudioMaterialId;
        public readonly AcousticImpulseFlags Flags;

        public AcousticImpulseEvent ToAcousticImpulseEvent()
        {
            return new AcousticImpulseEvent(
                RuntimePosition,
                Direction,
                KineticEnergyJoules,
                Volume01,
                PitchScale,
                RadiusMeters,
                SourceBodyInstanceId,
                AudioMaterialId,
                Flags);
        }
    }

    /// <summary>
    /// Physics event discriminator for <see cref="PhysicsEventPayload"/>.
    /// </summary>
    internal enum RemovedPhysicsEventType : ushort
    {
        PressureImpulse = 1,
        ElectromagneticPulse = 2,
        AcousticPing = 3,
        AcousticImpulse = 4
    }

    /// <summary>
    /// Unmanaged event payload carried by the deferred physics event lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 80)]
    internal struct RemovedPhysicsEventPayload
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
    /// Listener for deferred kinetic acoustic impulse events.
    /// </summary>
    public interface IPhysicsAcousticImpulseEventListener
    {
        void OnAcousticImpulse(in AcousticImpulseEvent impulseEvent);
    }

    /// <summary>
    /// Typed signal-lane physics-domain event surface for transient physics signals.
    /// </summary>
    public static class PhysicsEventBus
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 128;
        private const ushort EventCircuitBreakerDepthLimit = 5;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("PhysicsEventBus.Overflow"));
        private static readonly uint _circuitBreakerWarningHash = unchecked((uint)LocHash.Compute("PhysicsEventBus.CircuitBreaker"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute(nameof(PhysicsEventPayload)));

        // COLD ALLOC: RegistryBucket<IPressureImpulseEventListener>[32] - pressure impulse listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IPressureImpulseEventListener> _pressureListeners = new RegistryBucket<IPressureImpulseEventListener>(ListenerCapacity);
        // COLD ALLOC: RegistryBucket<IElectromagneticPulseEventListener>[32] - EMP listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IElectromagneticPulseEventListener> _empListeners = new RegistryBucket<IElectromagneticPulseEventListener>(ListenerCapacity);
        // COLD ALLOC: RegistryBucket<IAcousticPingEventListener>[32] - acoustic ping listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IAcousticPingEventListener> _acousticListeners = new RegistryBucket<IAcousticPingEventListener>(ListenerCapacity);
        // COLD ALLOC: RegistryBucket<IPhysicsAcousticImpulseEventListener>[32] - kinetic acoustic impulse listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly RegistryBucket<IPhysicsAcousticImpulseEventListener> _acousticImpulseListeners = new RegistryBucket<IPhysicsAcousticImpulseEventListener>(ListenerCapacity);
        private static int _snapshotReadFrame = -1;
        private static int _snapshotReadCursor;
        private static int _lastOverflowWarningFrame = -1;
        private static int _lastCircuitBreakerWarningFrame = -1;
        private static int _activeDispatchDepth;
        private static bool _initialized;

        /// <summary>Number of deferred physics event payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount
        {
            get
            {
                int snapshotCount = SignalBus<PhysicsEventPayload>.SnapshotCount;
                if (snapshotCount <= 0)
                    return 0;

                return Math.Max(0, snapshotCount - _snapshotReadCursor);
            }
        }

        /// <summary>Prewarms native event queues from runtime initialization paths before gameplay producers emit.</summary>
        public static void EnsureReady()
        {
            if (!Application.isPlaying)
                return;

            EnsureInitialized();
        }

        /// <summary>Releases persistent physics event lanes during explicit bootstrap teardown.</summary>
        public static void Shutdown()
        {
            ResetStaticState();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pressureListeners.Clear();
            _empListeners.Clear();
            _acousticListeners.Clear();
            _acousticImpulseListeners.Clear();
            _snapshotReadFrame = -1;
            _snapshotReadCursor = 0;
            _lastOverflowWarningFrame = -1;
            _lastCircuitBreakerWarningFrame = -1;
            _activeDispatchDepth = 0;
            _initialized = false;
        }

        /// <summary>Registers a pressure impulse listener.</summary>
        public static void Register(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            if (!_pressureListeners.Contains(listener))
                _pressureListeners.Register(listener);
        }

        /// <summary>Unregisters a pressure impulse listener.</summary>
        public static void Unregister(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_pressureListeners.Contains(listener))
                return;

            _pressureListeners.Unregister(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.PressureImpulse, _pressureListeners.Count);
        }

        /// <summary>Registers an electromagnetic pulse listener.</summary>
        public static void Register(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            if (!_empListeners.Contains(listener))
                _empListeners.Register(listener);
        }

        /// <summary>Unregisters an electromagnetic pulse listener.</summary>
        public static void Unregister(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_empListeners.Contains(listener))
                return;

            _empListeners.Unregister(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.ElectromagneticPulse, _empListeners.Count);
        }

        /// <summary>Registers an acoustic ping listener.</summary>
        public static void Register(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            if (!_acousticListeners.Contains(listener))
                _acousticListeners.Register(listener);
        }

        /// <summary>Unregisters an acoustic ping listener.</summary>
        public static void Unregister(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            if (!_acousticListeners.Contains(listener))
                return;

            _acousticListeners.Unregister(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.AcousticPing, _acousticListeners.Count);
        }

        /// <summary>Registers a kinetic acoustic impulse listener.</summary>
        public static void Register(IPhysicsAcousticImpulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            if (!_acousticImpulseListeners.Contains(listener))
                _acousticImpulseListeners.Register(listener);
        }

        /// <summary>Unregisters a kinetic acoustic impulse listener.</summary>
        public static void Unregister(IPhysicsAcousticImpulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_acousticImpulseListeners.Contains(listener))
                return;

            _acousticImpulseListeners.Unregister(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.AcousticImpulse, _acousticImpulseListeners.Count);
        }

        /// <summary>
        /// Flushes queued physics events to registered listeners.
        /// Called by <see cref="SystemDispatcher"/> from LateUpdate.
        /// </summary>
        public static void FlushPending()
        {
            EnsureInitialized();
            if (SignalBus<PhysicsEventPayload>.DroppedLastFlush > 0)
                ReportOverflowOncePerFrame();

            if (!HasAnyListener())
            {
                DropQueuedPayloads();
                return;
            }

            int currentFrame = Time.frameCount;
            if (_snapshotReadFrame != currentFrame)
            {
                _snapshotReadFrame = currentFrame;
                _snapshotReadCursor = 0;
            }

            ReadOnlySpan<PhysicsEventPayload> snapshot = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            while (_snapshotReadCursor < snapshot.Length)
            {
                int signalIndex = _snapshotReadCursor;
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    RequeueRemainingSnapshot(snapshot, signalIndex);
                    _snapshotReadCursor = snapshot.Length;
                    return;
                }

                PhysicsEventPayload payload = snapshot[signalIndex];
                _snapshotReadCursor = signalIndex + 1;
                Dispatch(in payload);
            }
        }

        /// <summary>Broadcasts one pressure-impulse payload.</summary>
        public static void NotifyPressureImpulse(in PressureImpulseEvent pressureEvent)
        {
            if (_pressureListeners.Count <= 0)
                return;

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
            if (_empListeners.Count <= 0)
                return;

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
            if (_acousticListeners.Count <= 0)
                return;

            Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = pingEvent.RuntimePosition,
                Direction = default,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = pingEvent.RadiusMeters,
                Scalar0 = pingEvent.Intensity01,
                Scalar1 = pingEvent.LifetimeSeconds,
                Scalar2 = pingEvent.EnergyJoules,
                PrimaryId = pingEvent.SourceSpeciesId,
                DataHash = 0u,
                StatusBits = unchecked((uint)pingEvent.SignalRole),
                EventType = (ushort)PhysicsEventType.AcousticPing,
                Reserved = 0
            });
        }

        /// <summary>Broadcasts one kinetic acoustic impulse payload.</summary>
        public static void NotifyAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = impulseEvent.RuntimePosition,
                Direction = impulseEvent.Direction,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = impulseEvent.RadiusMeters,
                Scalar0 = impulseEvent.KineticEnergyJoules,
                Scalar1 = impulseEvent.Volume01,
                Scalar2 = impulseEvent.PitchScale,
                PrimaryId = impulseEvent.SourceBodyInstanceId,
                DataHash = impulseEvent.AudioMaterialId,
                StatusBits = unchecked((uint)impulseEvent.Flags),
                EventType = (ushort)PhysicsEventType.AcousticImpulse,
                Reserved = 0
            });
        }

        /// <summary>Broadcasts one large acoustic impulse payload through the deferred acoustic impulse lane.</summary>
        public static void NotifyLargeAcousticImpulse(in LargeAcousticImpulseEvent impulseEvent)
        {
            AcousticImpulseEvent acousticImpulseEvent = impulseEvent.ToAcousticImpulseEvent();
            NotifyAcousticImpulse(in acousticImpulseEvent);
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            GlobalSignals.InitializeAllQueues();
            SignalBus<PhysicsEventPayload>.EnsureInitialized();
            _initialized = true;
        }

        private static bool Enqueue(in PhysicsEventPayload payload)
        {
            int queuedDepth = _activeDispatchDepth > 0 ? _activeDispatchDepth + 1 : 1;
            if (queuedDepth >= EventCircuitBreakerDepthLimit)
            {
                ReportCircuitBreakerOncePerFrame(queuedDepth);
                return false;
            }

            EnsureInitialized();
            PhysicsEventPayload queuedPayload = payload;
            queuedPayload.Reserved = (ushort)math.max(1, queuedDepth);
            SignalBus<PhysicsEventPayload>.Push(in queuedPayload);
            return true;
        }

        private static bool HasAnyListener()
        {
            return _pressureListeners.Count > 0 ||
                   _empListeners.Count > 0 ||
                   _acousticListeners.Count > 0 ||
                   _acousticImpulseListeners.Count > 0;
        }

        private static void DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType eventType, int listenerCount)
        {
            if (listenerCount > 0)
                return;

            if (!HasAnyListener())
            {
                DropQueuedPayloads();
                return;
            }
        }

        private static void DropQueuedPayloads()
        {
            _snapshotReadCursor = SignalBus<PhysicsEventPayload>.SnapshotCount;
        }

        private static void RequeueRemainingSnapshot(ReadOnlySpan<PhysicsEventPayload> snapshot, int startIndex)
        {
            for (int i = startIndex; i < snapshot.Length; i++)
            {
                PhysicsEventPayload payload = snapshot[i];
                SignalBus<PhysicsEventPayload>.Push(in payload);
            }
        }

        private static void Dispatch(in PhysicsEventPayload payload)
        {
            int previousDepth = _activeDispatchDepth;
            _activeDispatchDepth = math.max(1, payload.Reserved);
            try
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
                    case PhysicsEventType.AcousticImpulse:
                        DispatchAcousticImpulse(in payload);
                        break;
                }
            }
            finally
            {
                _activeDispatchDepth = previousDepth;
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
                payload.PrimaryId,
                payload.Scalar2);

            IAcousticPingEventListener[] rawArray = _acousticListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IAcousticPingEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnAcousticPing(in pingEvent);
            }
        }

        private static void DispatchAcousticImpulse(in PhysicsEventPayload payload)
        {
            int count = _acousticImpulseListeners.Count;
            if (count <= 0)
                return;

            AcousticImpulseEvent impulseEvent = new AcousticImpulseEvent(
                payload.RuntimePosition,
                payload.Direction,
                payload.Scalar0,
                payload.Scalar1,
                payload.Scalar2,
                payload.RadiusMeters,
                payload.PrimaryId,
                unchecked((byte)payload.DataHash),
                (AcousticImpulseFlags)payload.StatusBits);

            IPhysicsAcousticImpulseEventListener[] rawArray = _acousticImpulseListeners.RawArray;
            for (int i = count - 1; i >= 0; i--)
            {
                IPhysicsAcousticImpulseEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnAcousticImpulse(in impulseEvent);
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

        private static void ReportCircuitBreakerOncePerFrame(int chainDepth)
        {
            int frame = Time.frameCount;
            if (_lastCircuitBreakerWarningFrame == frame)
                return;

            _lastCircuitBreakerWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_circuitBreakerWarningHash, _queueHash, chainDepth);
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
    public sealed class PhysicsApplySystem : MonoBehaviour, IPhysicsService, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const int MaxTrackedBodies = 64;
        private const int MaxQueuedPackets = 64;
        private const int MaxForcePacketsAppliedPerFixedTick = 64;
        private const int MaxQueuedSubmarineImpactSignals = 32;
        private const int MaxActiveDepressurizationVortices = 8;
        private const int MaxActiveImpactProxyLights = 4;
        private const int DepressurizationVortexContactCapacity = 32;
        private const int ImplosionOverlapCapacity = 64;
        private const SystemID OwnerSystemId = SystemID.Physics;
        private const float MinMagnitudeSq = 0.000001f;
        private const float DepressurizationVortexDistanceFloorMeters = 0.5f;
        private const float HullYieldThresholdJoules = 225000f;
        private const float AcousticImpulseReferenceEnergyJoules = HullYieldThresholdJoules;
        private const float AcousticImpulseVolumeEnergyScale = 0.00001f;
        private const float AcousticImpulseMaximumVolumeEnergyJoules = 100000f;
        private const float AcousticImpulseMinimumRadiusMeters = 8f;
        private const float AcousticImpulseMaximumRadiusMeters = 64f;
        private const float MechanicalSparkProxyLightDurationSeconds = 0.05f;
        private const float MechanicalSparkProxyLightMinimumVolume = 0.34f;
        private const string NonFiniteForceLog = "[PhysicsApplySystem] Non-finite force packet detected. Zeroing vector.";
        private const string NonFiniteTorqueLog = "[PhysicsApplySystem] Non-finite torque packet detected. Zeroing vector.";
        private const string NonFinitePointOffsetLog = "[PhysicsApplySystem] Non-finite point-offset packet detected. Zeroing offset.";
        private const string InvalidForcePacketLog = "[PhysicsApplySystem] Burst packet validation rejected a non-finite or out-of-range packet.";
        private const string ToxicVectorLog = "TOXIC_VECTOR detected; payload stored in CrashTelemetryBuffer.";
        private const double FlushBudgetWarningMilliseconds = 0.2d;
        private const int FlushBudgetWarningCooldownFrames = 30;
        private const int ForcePacketWarningCooldownFrames = 30;
        private static readonly uint ForcePacketClipWarningHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.ForcePacketClip"));
        private static readonly uint ForcePacketQueueHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.ForcePacketQueue"));
        private static readonly uint PhysicsFlushBudgetWarningHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.FlushBudget"));
        private static readonly uint PhysicsFlushContextHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.FlushFrontBuffer"));
        private static readonly uint NanRecoverySystemHash = unchecked((uint)LocHash.Compute(nameof(PhysicsApplySystem)));
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FixedTick");
        private static readonly ProfilerMarker _packetValidationProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.ValidatePackets");
        private static readonly ProfilerMarker _flushFrontBufferProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FlushFrontBuffer");
        // COLD ALLOC: Collider[64] - static implosion overlap query buffer for zero-GC radius impulse dispatch - owner: PhysicsApplySystem
        private static readonly Collider[] s_implosionOverlapBuffer = new Collider[ImplosionOverlapCapacity];

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
        private struct RemovedDeferredSubmarineImpactSignal
        {
            public float3 LocalPoint;
            public float Magnitude;
            public float Depth;
            public uint DamageType;
            public float PreviousIntegrityNormalized;
            public float NextIntegrityNormalized;
            public ushort SourceId;
            public byte IntegrityDelta;
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

        private struct TransientProxyLightHandle
        {
            public int Key;
            public float ExpireAtUnscaledTime;
        }

        // COLD ALLOC: Rigidbody[64] - active rigidbody slot map for deferred packet application - owner: PhysicsApplySystem
        private readonly Rigidbody[] _bodySlots = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: AbsoluteUniversePosition[64] - last finite Rigidbody AUP for NaN recovery - owner: PhysicsApplySystem
        private readonly AbsoluteUniversePosition[] _lastFiniteBodyAups = new AbsoluteUniversePosition[MaxTrackedBodies];
        // COLD ALLOC: byte[64] - validity mask for last finite Rigidbody AUP cache - owner: PhysicsApplySystem
        private readonly byte[] _lastFiniteBodyAupValid = new byte[MaxTrackedBodies];
        // COLD ALLOC: DepressurizationVortex[8] - active breach-vortex slots - owner: PhysicsApplySystem
        private readonly DepressurizationVortex[] _depressurizationVortices = new DepressurizationVortex[MaxActiveDepressurizationVortices];
        // COLD ALLOC: SpatialQueryHit[32] - spatial-hash scratch for breach-vortex loose-body collection - owner: PhysicsApplySystem
        private readonly SpatialQueryHit[] _depressurizationVortexContacts = new SpatialQueryHit[DepressurizationVortexContactCapacity];
        // COLD ALLOC: Rigidbody[32] - unique body scratch for breach-vortex force routing - owner: PhysicsApplySystem
        private readonly Rigidbody[] _depressurizationVortexBodies = new Rigidbody[DepressurizationVortexContactCapacity];
        // COLD ALLOC: TransientProxyLightHandle[4] - bounded 0.05s impact proxy-light handles, no GameObject sparks - owner: PhysicsApplySystem
        private readonly TransientProxyLightHandle[] _impactProxyLights = new TransientProxyLightHandle[MaxActiveImpactProxyLights];
        // COLD ALLOC: List<Collider>[8] - submarine hull collider discovery for contact-modification enablement - owner: PhysicsApplySystem
        private readonly List<Collider> _submarineColliderScratch = new List<Collider>(8);
        private VaultBufferHandle<ForcePacket> _frontPacketBufferHandle;
        private VaultBufferHandle<ForcePacket> _backPacketBufferHandle;
        private VaultBufferHandle<ForcePacket> _validationPacketBufferHandle;
        private VaultBufferHandle<byte> _validationMaskBufferHandle;
        private int _submarineImpactSnapshotReadFrame = -1;
        private int _submarineImpactSnapshotReadCursor;
        private bool _submarineImpactSignalLaneInitialized;

        private int _frontCount;
        private int _backCount;
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
        private int _nextFlushBudgetWarningFrame;
        private int _nextForcePacketClipWarningFrame;
        private int _nextForcePacketSaturationWarningFrame;

        /// <summary>
        /// True once the service is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registry-backed physics apply runtime. No local singleton state.
        /// </summary>
        public static PhysicsApplySystem Instance => GlobalRegistry.Physics as PhysicsApplySystem;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <summary>
        /// Ensures a live runtime instance exists.
        /// </summary>
        /// <returns>Live physics apply instance.</returns>
        public static PhysicsApplySystem EnsureRuntimeInstance()
        {
            return Instance;
        }

        /// <summary>
        /// Explicitly initializes the service and registers it into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (!Application.isPlaying)
                return;

            EnsureRuntimeResources();

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
            PhysicsApplySystem system = Instance;
            if (system != null)
                system.ClearQueuedPackets();
        }

        internal static bool QueueSharedRaycast(
            IDispatcherRaycastReceiver receiver,
            int requestId,
            in RaycastCommand command)
        {
            return SystemDispatcher.QueueDispatcherRaycast(receiver, requestId, in command);
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
            return system != null && system.QueueDepressurizationVortex(
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
            return system != null && system.ApplyImplosionImpulse(
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

        internal static void ArmSafeTeleportSpeculativeCcdForSafeTeleport()
        {
            GlobalPhysicsStateManager.ArmSafeTeleportSpeculativeCcdForSafeTeleport();
        }

        /// <inheritdoc />
        public bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            return QueueForce(body, force, mode, ForcePacketPriority.Critical, wake);
        }

        internal bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, ForcePacketPriority priority, bool wake = true, ForcePacketFlags extraFlags = ForcePacketFlags.None)
        {
            if (!TrySanitizeVector(force, NonFiniteForceLog, out Vector3 sanitizedForce) ||
                VectorLengthSq(sanitizedForce) <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

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
                VectorLengthSq(sanitizedForce) <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            float3 worldPosition3 = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!math.all(math.isfinite(worldPosition3)))
            {
                ReportNonFinitePacket(NonFinitePointOffsetLog);
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

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
                VectorLengthSq(sanitizedTorque) <= MinMagnitudeSq ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBody(body);
            int rigidbodyIndex = ResolveBodyIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

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
            if (!TryResolveVaultBuffer(
                    ref _backPacketBufferHandle,
                    BufferID.PhysicsForceCommandBack,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> backPackets) ||
                _backCount >= MaxQueuedPackets)
            {
                ReportForcePacketSaturationWarningIfNeeded(saturationMessage);
                return false;
            }

            backPackets[_backCount++] = packet;
            return true;
        }

        private void ReportForcePacketSaturationWarningIfNeeded(string saturationMessage)
        {
            int frame = Time.frameCount;
            if (frame < _nextForcePacketSaturationWarningFrame)
                return;

            _nextForcePacketSaturationWarningFrame = frame + ForcePacketWarningCooldownFrames;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(saturationMessage);
#endif
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
                RadiusMeters = math.max(0.5f, radiusMeters),
                BaseAccelerationMetersPerSecondSquared = math.max(0f, baseAccelerationMetersPerSecondSquared),
                MaximumAccelerationMetersPerSecondSquared = math.max(0f, maximumAccelerationMetersPerSecondSquared),
                RemainingSeconds = math.max(0f, durationSeconds)
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

            ClearForcePacketBuffer(ref _frontPacketBufferHandle, BufferID.PhysicsForceCommandFront);
            ClearForcePacketBuffer(ref _backPacketBufferHandle, BufferID.PhysicsForceCommandBack);
            if (!_packetValidationScheduled)
            {
                ClearForcePacketBuffer(ref _validationPacketBufferHandle, BufferID.PhysicsForceValidationPackets);
                ClearByteBuffer(ref _validationMaskBufferHandle, BufferID.PhysicsForceValidationMask);
            }

            System.Array.Clear(_bodySlots, 0, _bodySlots.Length);
            System.Array.Clear(_lastFiniteBodyAups, 0, _lastFiniteBodyAups.Length);
            System.Array.Clear(_lastFiniteBodyAupValid, 0, _lastFiniteBodyAupValid.Length);
            System.Array.Clear(_depressurizationVortices, 0, _depressurizationVortices.Length);
            System.Array.Clear(_depressurizationVortexBodies, 0, _depressurizationVortexBodies.Length);
            ClearTransientImpactProxyLights();
            _frontCount = 0;
            _backCount = 0;
            _frontBufferValidationReady = false;
        }

        private void Awake()
        {
            PhysicsApplySystem registeredSystem = Instance;
            if (registeredSystem != null && registeredSystem != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureRuntimeResources();

            if (GlobalRegistry.Dispatcher != null && !_fixedTickRegistered)
            {
                // Flush the previous validated packet snapshot before producers write this fixed step.
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.UI);
                _fixedTickRegistered = GlobalRegistry.FixedTickables.Contains(this);
            }

            if (GlobalRegistry.Dispatcher != null && !_postFixedTickRegistered)
            {
                // Swap vault packet buffers only after all fixed-step producers have written to the back buffer.
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.UI);
                _postFixedTickRegistered = SystemDispatcher.GetPostFixedLane(PriorityLayer.UI).Contains(this);
            }

            if (GlobalRegistry.Dispatcher != null && !_lateFrameTickRegistered)
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

        private void EnsureRuntimeResources()
        {
            if (!Application.isPlaying)
                return;

            PhysicsEventBus.EnsureReady();
            EnsureForcePacketBuffers();
            EnsureValidationBuffers();
            EnsureSubmarineImpactSignalLane();
        }

        private void EnsureSubmarineImpactSignalLane()
        {
            if (!Application.isPlaying)
                return;

            if (_submarineImpactSignalLaneInitialized)
                return;

            GlobalSignals.InitializeAllQueues();
            SignalBus<DeferredSubmarineImpactSignal>.EnsureInitialized();
            _submarineImpactSignalLaneInitialized = true;
        }

        private void OnDisable()
        {
            UnregisterRuntimeHooks();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            UnregisterRuntimeHooks();

            if (_isInitialized && ReferenceEquals(GlobalRegistry.Physics, this))
            {
                GlobalRegistry.UnregisterPhysicsService(this);
                _isInitialized = false;
            }
            else
            {
                _isInitialized = false;
            }

            ClearQueuedPackets();
            ReleaseValidationBufferViews();
            ReleaseForcePacketBufferViews();
            ClearTransientImpactProxyLights();

            _submarineImpactSnapshotReadFrame = -1;
            _submarineImpactSnapshotReadCursor = 0;
            _submarineImpactSignalLaneInitialized = false;

            PhysicsEventBus.Shutdown();
        }

        private void UnregisterRuntimeHooks()
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

        public void FixedTick(float fixedDeltaTime)
        {
            using (_fixedTickProfilerMarker.Auto())
            {
                EnsureSubmarineModifiableContacts();
                PhysicsFrame.Tick();
                FlushValidatedFrontBuffer(fixedDeltaTime);
                ApplyDepressurizationVortices(fixedDeltaTime);
            }
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            if (_packetValidationScheduled)
                return;

            SwapForcePacketBuffers();
            ClampFrontBufferCountToCapacity();
            ScheduleFrontPacketValidation();
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            CompleteFrontPacketValidationInLateFrameSwapWindow();
            FlushDeferredSubmarineImpactSignals();
            ExpireTransientImpactProxyLights();
        }

        private void FlushValidatedFrontBuffer(float fixedDeltaTime)
        {
            if (!_frontBufferValidationReady)
                return;

            if (!TryResolveVaultBuffer(
                    ref _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> frontPackets) ||
                !TryResolveVaultBuffer(
                    ref _validationMaskBufferHandle,
                    BufferID.PhysicsForceValidationMask,
                    MaxQueuedPackets,
                    out NativeArray<byte> validationMask))
            {
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                using (_flushFrontBufferProfilerMarker.Auto())
                {
                    int applyCount = math.min(_frontCount, MaxForcePacketsAppliedPerFixedTick);
                    if (_frontCount > applyCount)
                        PublishForcePacketClipWarningIfNeeded(_frontCount);

                    for (int i = 0; i < applyCount; i++)
                    {
                        if (validationMask[i] == 0)
                        {
                            ReportNonFinitePacket(InvalidForcePacketLog);
                            continue;
                        }

                        ForcePacket packet = frontPackets[i];
                        Rigidbody body = ResolveBody(packet.RigidbodyIndex);
                        if (body == null)
                            continue;

                        if (!EnsureFiniteBodyState(body, packet.RigidbodyIndex))
                            continue;

                        if (body.isKinematic)
                            continue;

                        ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
                        if (ShouldDiscardAmbientPacket(body, in packet, flags, fixedDeltaTime))
                            continue;

                        if ((flags & ForcePacketFlags.WakeBody) != 0 && body.IsSleeping())
                            body.WakeUp();

                        Vector3 appliedForce = Vector3.zero;
                        Vector3 appliedTorque = Vector3.zero;
                        Vector3 impulsePosition = body.position;
                        Vector3 preApplyVelocity = HectonPlayerMotor.SafeVelocity(body.linearVelocity);
                        bool appliedAny = false;

                        if ((flags & ForcePacketFlags.HasForce) != 0)
                        {
                            if (!TrySanitizeVector(packet.Force, NonFiniteForceLog, out Vector3 sanitizedForce))
                                sanitizedForce = Vector3.zero;
                            sanitizedForce = ApplyActiveBiomeBuoyancyGravityMultiplier(sanitizedForce, packet.Mode, flags);

                            if ((flags & ForcePacketFlags.ApplyAtPosition) != 0)
                            {
                                if (!TrySanitizeVector(packet.PointOffset, NonFinitePointOffsetLog, out Vector3 sanitizedOffset))
                                    sanitizedOffset = Vector3.zero;

                                Vector3 applicationAnchor = ResolveForceApplicationAnchor(body, packet.Priority);
                                impulsePosition = applicationAnchor + sanitizedOffset;
                                if (VectorLengthSq(sanitizedForce) > MinMagnitudeSq)
                                {
                                    body.AddForceAtPosition(sanitizedForce, impulsePosition, packet.Mode);
                                    appliedForce = sanitizedForce;
                                    appliedAny = true;
                                }
                            }
                            else if (VectorLengthSq(sanitizedForce) > MinMagnitudeSq)
                            {
                                body.AddForce(sanitizedForce, packet.Mode);
                                appliedForce = sanitizedForce;
                                appliedAny = true;
                            }
                        }

                        if ((flags & ForcePacketFlags.HasTorque) != 0)
                        {
                            if (TrySanitizeVector(packet.Torque, NonFiniteTorqueLog, out Vector3 sanitizedTorque) &&
                                VectorLengthSq(sanitizedTorque) > MinMagnitudeSq)
                            {
                                body.AddTorque(sanitizedTorque, packet.Mode);
                                appliedTorque = sanitizedTorque;
                                appliedAny = true;
                            }
                        }

                        if (packet.Priority == ForcePacketPriority.Critical && appliedAny)
                            EmitCriticalAcousticImpulse(
                                body,
                                appliedForce,
                                appliedTorque,
                                impulsePosition,
                                packet.Mode,
                                fixedDeltaTime,
                                preApplyVelocity);
                    }

                    ClearForcePacketRange(frontPackets, _frontCount);
                    ClearByteRange(validationMask, _frontCount);
                    _frontCount = 0;
                    _frontBufferValidationReady = false;
                }
            }
            finally
            {
                PublishFlushBudgetWarningIfNeeded(startTimestamp);
            }
        }

        private static Vector3 ResolveForceApplicationAnchor(Rigidbody body, ForcePacketPriority priority)
        {
            return priority == ForcePacketPriority.Critical
                ? body.worldCenterOfMass
                : body.position;
        }

        private void PublishFlushBudgetWarningIfNeeded(long startTimestamp)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
            if (elapsedMilliseconds <= FlushBudgetWarningMilliseconds || Time.frameCount < _nextFlushBudgetWarningFrame)
                return;

            _nextFlushBudgetWarningFrame = Time.frameCount + FlushBudgetWarningCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PhysicsFlushBudgetWarningHash,
                PhysicsFlushContextHash,
                (float)elapsedMilliseconds);
        }

        private void PublishForcePacketClipWarningIfNeeded(int packetCount)
        {
            int frame = Time.frameCount;
            if (frame < _nextForcePacketClipWarningFrame)
                return;

            _nextForcePacketClipWarningFrame = frame + ForcePacketWarningCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ForcePacketClipWarningHash,
                ForcePacketQueueHash,
                packetCount);
        }

        private void EmitCriticalAcousticImpulse(
            Rigidbody body,
            Vector3 appliedForce,
            Vector3 appliedTorque,
            Vector3 impulsePosition,
            ForceMode forceMode,
            float fixedDeltaTime,
            Vector3 preApplyVelocity)
        {
            Vector3 predictedVelocity = preApplyVelocity + ResolvePacketVelocityDelta(appliedForce, forceMode, body.mass, fixedDeltaTime);
            float kineticEnergyJoules = ResolveKineticEnergyJoules(body.mass, predictedVelocity);
            float volume01 = ResolveAcousticImpulseVolume01(kineticEnergyJoules);
            float pitchScale = ResolveAcousticImpulsePitchScale(kineticEnergyJoules);
            float radiusMeters = math.lerp(AcousticImpulseMinimumRadiusMeters, AcousticImpulseMaximumRadiusMeters, volume01);
            Vector3 direction = VectorLengthSq(appliedForce) > MinMagnitudeSq
                ? appliedForce
                : VectorLengthSq(appliedTorque) > MinMagnitudeSq
                    ? appliedTorque
                    : predictedVelocity;
            int sourceBodyEntityId = unchecked((int)EntityId.ToULong(body.GetEntityId()));

            AcousticImpulseEvent impulseEvent = new AcousticImpulseEvent(
                impulsePosition,
                direction,
                kineticEnergyJoules,
                volume01,
                pitchScale,
                radiusMeters,
                sourceBodyEntityId,
                0,
                AcousticImpulseFlags.Critical);
            PhysicsEventBus.NotifyAcousticImpulse(in impulseEvent);
            TryRegisterMechanicalSparkProxyLight(impulsePosition, sourceBodyEntityId, volume01);
        }

        private static Vector3 ResolvePacketVelocityDelta(Vector3 force, ForceMode mode, float mass, float fixedDeltaTime)
        {
            Vector3 safeForce = HectonPlayerMotor.SafeVelocity(force);
            switch (mode)
            {
                case ForceMode.Force:
                    return safeForce * (fixedDeltaTime / math.max(mass, 0.0001f));

                case ForceMode.Acceleration:
                    return safeForce * fixedDeltaTime;

                case ForceMode.Impulse:
                    return safeForce / math.max(mass, 0.0001f);

                case ForceMode.VelocityChange:
                    return safeForce;

                default:
                    return Vector3.zero;
            }
        }

        private static float ResolveKineticEnergyJoules(float massKg, Vector3 velocityMetersPerSecond)
        {
            float3 velocity = new float3(velocityMetersPerSecond.x, velocityMetersPerSecond.y, velocityMetersPerSecond.z);
            if (!math.all(math.isfinite(velocity)))
                return 0f;

            return math.max(0f, 0.5f * math.max(0.0001f, massKg) * math.lengthsq(velocity));
        }

        internal static float ResolveAcousticImpulseVolume01(float kineticEnergyJoules)
        {
            float cappedEnergy = math.min(math.max(0f, kineticEnergyJoules), AcousticImpulseMaximumVolumeEnergyJoules);
            return math.saturate(cappedEnergy * AcousticImpulseVolumeEnergyScale);
        }

        internal static float ResolveAcousticImpulsePitchScale(float kineticEnergyJoules)
        {
            float normalizedEnergy = math.saturate(math.max(0f, kineticEnergyJoules) / AcousticImpulseReferenceEnergyJoules);
            return math.clamp(0.72f + normalizedEnergy * 0.56f, 0.65f, 1.45f);
        }

        private void TryRegisterMechanicalSparkProxyLight(Vector3 runtimePosition, int sourceBodyInstanceId, float volume01)
        {
            float intensity01 = math.saturate(volume01);
            if (intensity01 < MechanicalSparkProxyLightMinimumVolume)
                return;

            float now = Time.unscaledTime;
            int selectedIndex = -1;
            for (int i = 0; i < _impactProxyLights.Length; i++)
            {
                if (_impactProxyLights[i].ExpireAtUnscaledTime <= now)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
                return;

            int key = unchecked((sourceBodyInstanceId * 397) ^ PhysicsFrame.Current ^ 0x5EC7A11);
            ProxyLightData light = ProxyLightData.CreateTransientPoint(
                AbsoluteUniversePosition.FromRuntimePosition(runtimePosition),
                new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                new Color(1f, 0.68f, 0.32f, 1f),
                math.lerp(2.2f, 5.8f, intensity01),
                math.lerp(0.8f, 2.6f, intensity01),
                now);
            ProxyLightRegistry.RegisterOrUpdate(key, in light);

            _impactProxyLights[selectedIndex] = new TransientProxyLightHandle
            {
                Key = key,
                ExpireAtUnscaledTime = now + MechanicalSparkProxyLightDurationSeconds
            };
        }

        private void ExpireTransientImpactProxyLights()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _impactProxyLights.Length; i++)
            {
                TransientProxyLightHandle handle = _impactProxyLights[i];
                if (handle.Key == 0 || handle.ExpireAtUnscaledTime > now)
                    continue;

                ProxyLightRegistry.Unregister(handle.Key);
                _impactProxyLights[i] = default;
            }
        }

        private void ClearTransientImpactProxyLights()
        {
            for (int i = 0; i < _impactProxyLights.Length; i++)
            {
                int key = _impactProxyLights[i].Key;
                if (key != 0)
                    ProxyLightRegistry.Unregister(key);

                _impactProxyLights[i] = default;
            }
        }

        private static bool ShouldDiscardAmbientPacket(Rigidbody body, in ForcePacket packet, ForcePacketFlags flags, float fixedDeltaTime)
        {
            if (packet.Priority != ForcePacketPriority.Ambient ||
                body == null)
            {
                return false;
            }

            IPhysicsCullingOverseer physicsCulling = GlobalRegistry.PhysicsCullingOverseer;
            if ((flags & ForcePacketFlags.WakeBody) == 0 &&
                physicsCulling != null &&
                physicsCulling.IsBodyCulled(body))
            {
                return true;
            }

            if ((flags & ForcePacketFlags.HasForce) == 0)
                return false;

            return VehicleMotor.TryResolveForBody(body, out VehicleMotor vehicleMotor) &&
                   vehicleMotor.WouldAmbientForceExtendEntanglement(packet.Force, packet.Mode, math.max(fixedDeltaTime, 0.0001f));
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
            if (!(multiplier > 0f) || !float.IsFinite(multiplier) || math.abs(multiplier - 1f) <= 0.0001f)
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
                vortex.RemainingSeconds = math.max(0f, vortex.RemainingSeconds - fixedDeltaTime);
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
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;

            if (TryResolvePlayerRuntimePosition(playerMovement, out Vector3 playerPosition))
            {
                if (VectorLengthSq(playerPosition - vortex.RoomCenter) <= vortex.RadiusMeters * vortex.RadiusMeters)
                {
                    Vector3 acceleration = ResolveDepressurizationVortexAcceleration(
                        playerPosition,
                        vortex.BreachPosition,
                        vortex.BaseAccelerationMetersPerSecondSquared,
                        vortex.MaximumAccelerationMetersPerSecondSquared);
                    if (VectorLengthSq(acceleration) > MinMagnitudeSq)
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
                Rigidbody body = hit.Rigidbody;
                if (body == null)
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
                if (VectorLengthSq(acceleration) > MinMagnitudeSq)
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

            float safeRadius = math.max(0.5f, radiusMeters);
            float safeBaseImpulse = math.max(0f, baseImpulseNewtonSeconds);
            float safeMaximumImpulse = math.max(0f, maximumImpulseNewtonSeconds);
            Rigidbody playerBody = null;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerContext != null)
                playerBody = playerContext.PlayerRigidbody;

            if (TryResolvePlayerRuntimePosition(playerMovement, out Vector3 playerPosition))
            {
                if (VectorLengthSq(playerPosition - roomCenter) <= safeRadius * safeRadius)
                {
                    Vector3 impulse = ResolveImplosionImpulse(
                        playerPosition,
                        roomCenter,
                        safeBaseImpulse,
                        safeMaximumImpulse);
                    if (VectorLengthSq(impulse) > MinMagnitudeSq)
                    {
                        if (playerMovement != null)
                        {
                            float mass = playerBody != null ? math.max(1f, playerBody.mass) : 80f;
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
                if (VectorLengthSq(impulse) > MinMagnitudeSq)
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
            if (!TryResolveDominantAxisAndDistance(toBreach, out Vector3 axis, out float distanceMeters))
                return Vector3.zero;

            float safeDistance = math.max(DepressurizationVortexDistanceFloorMeters, distanceMeters);
            float accelerationMagnitude = math.min(maximumAccelerationMetersPerSecondSquared, baseAccelerationMetersPerSecondSquared / safeDistance);
            return axis * accelerationMagnitude;
        }

        private static Vector3 ResolveImplosionImpulse(
            Vector3 bodyPosition,
            Vector3 roomCenter,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds)
        {
            Vector3 toCenter = roomCenter - bodyPosition;
            if (!TryResolveDominantAxisAndDistance(toCenter, out Vector3 axis, out float distanceMeters))
                return Vector3.zero;

            float safeDistance = math.max(DepressurizationVortexDistanceFloorMeters, distanceMeters);
            float impulseMagnitude = math.min(maximumImpulseNewtonSeconds, baseImpulseNewtonSeconds / safeDistance);
            return axis * impulseMagnitude;
        }

        private static bool TryResolveDominantAxisAndDistance(
            Vector3 vector,
            out Vector3 axis,
            out float distanceMeters)
        {
            float ax = math.abs(vector.x);
            float ay = math.abs(vector.y);
            float az = math.abs(vector.z);
            distanceMeters = math.max(ax, math.max(ay, az));
            if (distanceMeters <= 0.00000001f)
            {
                axis = Vector3.zero;
                return false;
            }

            if (ax >= ay && ax >= az)
                axis = vector.x < 0f ? Vector3.left : Vector3.right;
            else if (ay >= az)
                axis = vector.y < 0f ? Vector3.down : Vector3.up;
            else
                axis = vector.z < 0f ? Vector3.back : Vector3.forward;

            return true;
        }

        private void SwapForcePacketBuffers()
        {
            EnsureForcePacketBuffers();
            VaultBufferHandle<ForcePacket> swapHandle = _frontPacketBufferHandle;
            _frontPacketBufferHandle = _backPacketBufferHandle;
            _backPacketBufferHandle = swapHandle;

            _frontCount = _backCount;
            _backCount = 0;
        }

        private void ClampFrontBufferCountToCapacity()
        {
            if (_frontCount <= MaxQueuedPackets)
                return;

            PublishForcePacketClipWarningIfNeeded(_frontCount);
            if (TryResolveVaultBuffer(
                    ref _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> frontPackets))
            {
                ClearForcePacketRange(frontPackets, _frontCount, MaxQueuedPackets);
            }

            _frontCount = MaxQueuedPackets;
        }

        private void EnsureForcePacketBuffers()
        {
            if (!Application.isPlaying)
                return;

            TryResolveVaultBuffer(
                ref _frontPacketBufferHandle,
                BufferID.PhysicsForceCommandFront,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
            TryResolveVaultBuffer(
                ref _backPacketBufferHandle,
                BufferID.PhysicsForceCommandBack,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
        }

        private bool TryResolveVaultBuffer<T>(
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (requiredLength <= 0)
                return false;

            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault == null)
                return false;

            if (!handle.IsCreated || handle.Length < requiredLength)
            {
                handle = dataVault.GetBufferHandle<T>(
                    bufferId,
                    requiredLength,
                    OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
                if (!handle.IsCreated)
                    return false;
            }

            buffer = handle.Resolve(dataVault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
        }

        private void ClearForcePacketBuffer(ref VaultBufferHandle<ForcePacket> handle, BufferID bufferId)
        {
            if (!TryResolveVaultBuffer(ref handle, bufferId, MaxQueuedPackets, out NativeArray<ForcePacket> buffer))
                return;

            ClearForcePacketRange(buffer, MaxQueuedPackets);
        }

        private void ClearByteBuffer(ref VaultBufferHandle<byte> handle, BufferID bufferId)
        {
            if (!TryResolveVaultBuffer(ref handle, bufferId, MaxQueuedPackets, out NativeArray<byte> buffer))
                return;

            ClearByteRange(buffer, MaxQueuedPackets);
        }

        private static void ClearForcePacketRange(NativeArray<ForcePacket> buffer, int count, int startIndex = 0)
        {
            if (!buffer.IsCreated || count <= 0 || startIndex >= buffer.Length)
                return;

            int clampedCount = math.min(count, buffer.Length - startIndex);
            for (int i = 0; i < clampedCount; i++)
                buffer[startIndex + i] = default;
        }

        private static void ClearByteRange(NativeArray<byte> buffer, int count, int startIndex = 0)
        {
            if (!buffer.IsCreated || count <= 0 || startIndex >= buffer.Length)
                return;

            int clampedCount = math.min(count, buffer.Length - startIndex);
            for (int i = 0; i < clampedCount; i++)
                buffer[startIndex + i] = 0;
        }

        private void EnsureValidationBuffers()
        {
            if (!Application.isPlaying)
                return;

            TryResolveVaultBuffer(
                ref _validationPacketBufferHandle,
                BufferID.PhysicsForceValidationPackets,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
            TryResolveVaultBuffer(
                ref _validationMaskBufferHandle,
                BufferID.PhysicsForceValidationMask,
                MaxQueuedPackets,
                out NativeArray<byte> _);
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
            Transform structuralGridTransform = structuralGrid.transform;
            Matrix4x4 structuralGridWorldToLocal = structuralGridTransform.worldToLocalMatrix;

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
                if (VectorLengthSq(normal) <= MinMagnitudeSq)
                    normal = Vector3.up;

                float3 hullVelocity = submarineIsBody ? ToFloat3(pair.bodyVelocity) : ToFloat3(pair.otherBodyVelocity);
                float3 otherVelocity = submarineIsBody ? ToFloat3(pair.otherBodyVelocity) : ToFloat3(pair.bodyVelocity);
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
                float3 localPoint = ToFloat3(structuralGridWorldToLocal.MultiplyPoint3x4(point));
                float3 localNormal = ToFloat3(ResolveDominantAxisDirection(
                    structuralGridWorldToLocal.MultiplyVector(impactNormalWorld)));
                structuralGrid.QueueImpactLocal(localPoint, impact.RelativeSpeedMetersPerSecond, impact.IntegrityDelta);
                structuralGrid.QueueHullImpactDecalLocal(localPoint, localNormal, impact.RelativeSpeedMetersPerSecond, impact.Severity01);
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
            if (!Application.isPlaying)
                return;

            EnsureSubmarineImpactSignalLane();
            DeferredSubmarineImpactSignal signal = new DeferredSubmarineImpactSignal
            {
                LocalPoint = localPoint,
                Magnitude = math.max(0f, impactSpeedMetersPerSecond),
                Depth = math.max(0f, depthMeters),
                DamageType = (uint)DamageTypeMask.Impact,
                PreviousIntegrityNormalized = 1f,
                NextIntegrityNormalized = math.saturate(1f - severity01),
                SourceId = DamageSourceIds.SubmarineImpact,
                IntegrityDelta = integrityDelta,
                TraumaLevel = (byte)ResolveSubmarineTraumaLevel(severity01)
            };
            SignalBus<DeferredSubmarineImpactSignal>.Push(in signal);
        }

        private void FlushDeferredSubmarineImpactSignals()
        {
            EnsureSubmarineImpactSignalLane();
            int currentFrame = Time.frameCount;
            if (_submarineImpactSnapshotReadFrame != currentFrame)
            {
                _submarineImpactSnapshotReadFrame = currentFrame;
                _submarineImpactSnapshotReadCursor = 0;
            }

            ReadOnlySpan<DeferredSubmarineImpactSignal> snapshot = SignalBus<DeferredSubmarineImpactSignal>.GetFrameSnapshot();
            if (_submarineImpactSnapshotReadCursor >= snapshot.Length)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            TraumaDispatcher traumaDispatcher = playerContext != null ? playerContext.TraumaDispatcher : null;
            if (traumaDispatcher == null)
            {
                RequeueRemainingSubmarineImpactSignals(snapshot, _submarineImpactSnapshotReadCursor);
                _submarineImpactSnapshotReadCursor = snapshot.Length;
                return;
            }

            while (_submarineImpactSnapshotReadCursor < snapshot.Length)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    RequeueRemainingSubmarineImpactSignals(snapshot, _submarineImpactSnapshotReadCursor);
                    _submarineImpactSnapshotReadCursor = snapshot.Length;
                    return;
                }

                DeferredSubmarineImpactSignal queuedSignal = snapshot[_submarineImpactSnapshotReadCursor++];

                Hecton8.Gameplay.HabitatDamageSignal signal = default;
                signal.magnitude = queuedSignal.Magnitude;
                signal.localPoint = queuedSignal.LocalPoint;
                signal.damageType = queuedSignal.DamageType;
                signal.integrityDelta = queuedSignal.IntegrityDelta;
                signal.depth = queuedSignal.Depth;
                signal.sourceID = queuedSignal.SourceId;

                traumaDispatcher.OnIntegrityChanged(
                    queuedSignal.PreviousIntegrityNormalized,
                    queuedSignal.NextIntegrityNormalized,
                    signal);
                traumaDispatcher.OnTraumaThresholdCrossed((TraumaLevel)queuedSignal.TraumaLevel);
            }
        }

        private static void RequeueRemainingSubmarineImpactSignals(ReadOnlySpan<DeferredSubmarineImpactSignal> snapshot, int startIndex)
        {
            for (int i = startIndex; i < snapshot.Length; i++)
            {
                DeferredSubmarineImpactSignal signal = snapshot[i];
                SignalBus<DeferredSubmarineImpactSignal>.Push(in signal);
            }
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
            if (!TryResolveVaultBuffer(
                    ref _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> frontPackets) ||
                !TryResolveVaultBuffer(
                    ref _validationPacketBufferHandle,
                    BufferID.PhysicsForceValidationPackets,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> validationPackets) ||
                !TryResolveVaultBuffer(
                    ref _validationMaskBufferHandle,
                    BufferID.PhysicsForceValidationMask,
                    MaxQueuedPackets,
                    out NativeArray<byte> validationMask))
            {
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            for (int i = 0; i < _frontCount; i++)
            {
                validationPackets[i] = frontPackets[i];
                validationMask[i] = 0;
            }

            ValidateForcePacketsJob validateJob = new ValidateForcePacketsJob
            {
                Packets = validationPackets,
                ValidityMask = validationMask,
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
                    _lastFiniteBodyAups[i] = default;
                    _lastFiniteBodyAupValid[i] = 0;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float VectorLengthSq(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.lengthsq(value3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ResolveDominantAxisDirection(Vector3 direction)
        {
            float3 value3 = new float3(direction.x, direction.y, direction.z);
            if (!math.all(math.isfinite(value3)) || math.lengthsq(value3) <= MinMagnitudeSq)
                return Vector3.up;

            float3 absolute = math.abs(value3);
            if (absolute.x >= absolute.y && absolute.x >= absolute.z)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absolute.y >= absolute.z)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private static bool TryResolvePlayerRuntimePosition(
            HectonPlayerMovement playerMovement,
            out Vector3 playerPosition)
        {
            if (playerMovement == null)
            {
                playerPosition = default;
                return false;
            }

            float3 playerRuntime3 = playerMovement.CurrentAup.ToRuntimeFloat3();
            playerPosition = new Vector3(playerRuntime3.x, playerRuntime3.y, playerRuntime3.z);
            return IsFiniteVector(playerPosition);
        }

        private bool EnsureFiniteBodyState(Rigidbody body, int rigidbodyIndex)
        {
            Vector3 position = body.position;
            Vector3 linearVelocity = body.linearVelocity;
            Vector3 angularVelocity = body.angularVelocity;
            Quaternion rotation = body.rotation;
            bool finitePosition = IsFiniteVector(position);
            bool finiteLinearVelocity = IsFiniteVector(linearVelocity);
            bool finiteAngularVelocity = IsFiniteVector(angularVelocity);
            bool finiteRotation = IsFiniteQuaternion(rotation);
            if (finitePosition &&
                finiteLinearVelocity &&
                finiteAngularVelocity &&
                finiteRotation)
            {
                CacheLastFiniteBodyAup(rigidbodyIndex, position);
                return true;
            }

            Vector3 toxicVector = !finitePosition
                ? position
                : !finiteLinearVelocity
                    ? linearVelocity
                    : !finiteAngularVelocity
                        ? angularVelocity
                        : ResolveToxicRotationVector(rotation);
            if (finitePosition)
                CacheLastFiniteBodyAup(rigidbodyIndex, position);

            FreezeToxicBody(body, toxicVector, rigidbodyIndex);
            return false;
        }

        private static Vector3 ResolveToxicRotationVector(Quaternion rotation)
        {
            Vector3 xyz = new Vector3(rotation.x, rotation.y, rotation.z);
            return IsFiniteVector(xyz)
                ? new Vector3(rotation.w, 0f, 0f)
                : xyz;
        }

        private void FreezeToxicBody(Rigidbody body, Vector3 toxicVector, int rigidbodyIndex)
        {
            Vector3 currentPosition = body.position;
            Vector3 recoveredPosition = RuntimeWatchdog.ReportRigidbodyNanRecovery(
                NanRecoverySystemHash,
                toxicVector,
                ResolveLastFiniteBodyRuntimePosition(rigidbodyIndex, currentPosition));

            float3 zeroVelocity3 = float3.zero;
            Vector3 zeroVelocity = new Vector3(zeroVelocity3.x, zeroVelocity3.y, zeroVelocity3.z);
            body.linearVelocity = zeroVelocity;
            body.angularVelocity = zeroVelocity;
            if (IsFiniteVector(recoveredPosition))
            {
                body.position = recoveredPosition;
                CacheLastFiniteBodyAup(rigidbodyIndex, recoveredPosition);
            }

            body.detectCollisions = false;
            body.isKinematic = true;
            body.Sleep();
            int entityId = body.gameObject != null
                ? unchecked((int)EntityId.ToULong(body.gameObject.GetEntityId()))
                : 0;
            ReportToxicVector(entityId, toxicVector);
        }

        private void CacheLastFiniteBodyAup(int rigidbodyIndex, Vector3 position)
        {
            if ((uint)rigidbodyIndex >= (uint)_lastFiniteBodyAups.Length || !IsFiniteVector(position))
                return;

            _lastFiniteBodyAups[rigidbodyIndex] = AbsoluteUniversePosition.FromRuntimePosition(position);
            _lastFiniteBodyAupValid[rigidbodyIndex] = 1;
        }

        private Vector3 ResolveLastFiniteBodyRuntimePosition(int rigidbodyIndex, Vector3 currentPosition)
        {
            if ((uint)rigidbodyIndex < (uint)_lastFiniteBodyAups.Length &&
                _lastFiniteBodyAupValid[rigidbodyIndex] != 0)
            {
                float3 runtime = _lastFiniteBodyAups[rigidbodyIndex].ToRuntimeFloat3();
                Vector3 cachedPosition = new Vector3(runtime.x, runtime.y, runtime.z);
                if (IsFiniteVector(cachedPosition))
                    return cachedPosition;
            }

            return IsFiniteVector(currentPosition) ? currentPosition : Vector3.zero;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            return math.all(math.isfinite(value4));
        }

        private static bool TrySanitizeVector(Vector3 value, string errorMessage, out Vector3 sanitized)
        {
            if (!MathGuard.IsFinite(value))
            {
                ReportNonFinitePacket(errorMessage);
                sanitized = Vector3.zero;
                return false;
            }

            sanitized = value;
            return true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportNonFinitePacket(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NativeAllocationTrackerRuntimeBridge.ReportLeak(message);
            Debug.LogError(message);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportToxicVector(int bodyId, Vector3 toxicVector)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NativeAllocationTrackerRuntimeBridge.ReportLeak(ToxicVectorLog);
            Debug.LogError(ToxicVectorLog);
#endif
        }

        private void ReleaseValidationBufferViews()
        {
            if (_packetValidationScheduled)
                JobHandle.ScheduleBatchedJobs();

            _validationPacketBufferHandle = default;
            _validationMaskBufferHandle = default;
            _packetValidationHandle = default;
            _packetValidationScheduled = false;
        }

        private void ReleaseForcePacketBufferViews()
        {
            _frontPacketBufferHandle = default;
            _backPacketBufferHandle = default;
            _frontCount = 0;
            _backCount = 0;
        }
    }

    /// <summary>
    /// Common physics routing facade that keeps player-body writes inside <see cref="IMotorForces"/>
    /// and routes all other rigidbody writes through <see cref="PhysicsApplySystem"/>.
    /// </summary>
    public static class PhysicsForceRouter
    {
        private const float FixedStepSeconds = 0.02f;
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
            float3 correction3 = new float3(correction.x, correction.y, correction.z);
            if (!wasKinematic && math.lengthsq(correction3) > 0.000001f)
            {
                body.AddForce(correction / FixedStepSeconds, ForceMode.VelocityChange);
            }

            body.useGravity = false;
            body.isKinematic = true;
            float3 zeroVelocity3 = float3.zero;
            Vector3 zeroVelocity = new Vector3(zeroVelocity3.x, zeroVelocity3.y, zeroVelocity3.z);
            body.linearVelocity = zeroVelocity;
            body.angularVelocity = zeroVelocity;
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
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce))
                return false;

            Vector3 safeForce = ClampUpwardAcceleration(acceptedForce, mode);
            if (TryRouteToPlayerMotor(body, safeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForce(body, safeForce, mode, wake);
        }

        /// <summary>
        /// Routes an ambient environmental force into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce))
                return false;

            Vector3 safeForce = ClampUpwardAcceleration(acceptedForce, mode);
            ForcePacketFlags extraFlags = ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = PhysicsApplySystem.ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToPlayerMotor(body, routeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForce(body, safeForce, mode, ForcePacketPriority.Ambient, wake, extraFlags);
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
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce) ||
                !MathGuard.TryAcceptFinite(worldPosition, out Vector3 acceptedWorldPosition))
                return false;

            Vector3 safeForce = ClampUpwardAcceleration(acceptedForce, mode);
            if (TryRouteToPlayerMotorAtPosition(body, safeForce, acceptedWorldPosition))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForceAtPosition(body, safeForce, acceptedWorldPosition, mode, wake);
        }

        /// <summary>
        /// Routes an ambient environmental force-at-position into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce) ||
                !MathGuard.TryAcceptFinite(worldPosition, out Vector3 acceptedWorldPosition))
                return false;

            Vector3 safeForce = ClampUpwardAcceleration(acceptedForce, mode);
            ForcePacketFlags extraFlags = ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = PhysicsApplySystem.ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToPlayerMotorAtPosition(body, routeForce, acceptedWorldPosition))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForceAtPosition(body, safeForce, acceptedWorldPosition, mode, ForcePacketPriority.Ambient, wake, extraFlags);
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
            return system != null && system.QueueTractorBeamPd(
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
            if (!MathGuard.TryAcceptFinite(torque, out Vector3 acceptedTorque))
                return false;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueTorque(body, acceptedTorque, mode, wake);
        }

        /// <summary>
        /// Routes ambient environmental torque into the deferred packet system.
        /// </summary>
        public static bool QueueAmbientTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(torque, out Vector3 acceptedTorque))
                return false;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueTorque(body, acceptedTorque, mode, ForcePacketPriority.Ambient, wake);
        }

        private static bool TryRouteToPlayerMotor(Rigidbody body, Vector3 force, ForceMode mode)
        {
            if (body == null)
                return false;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null || !ReferenceEquals(body, playerContext.PlayerRigidbody))
                return false;

            HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
            if (playerMovement != null)
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

            HectonPlayerMotor playerMotor = GlobalRegistry.PlayerMotor;
            if (playerMotor == null)
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
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (body == null ||
                playerContext == null ||
                !ReferenceEquals(body, playerContext.PlayerRigidbody) ||
                !IsFiniteVector(worldPosition))
            {
                return false;
            }

            HectonPlayerMotor playerMotor = GlobalRegistry.PlayerMotor;
            if (playerMotor == null)
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
