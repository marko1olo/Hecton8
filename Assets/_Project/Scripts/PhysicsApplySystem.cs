using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
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
    internal readonly struct QueueForceArgs
    {
        public readonly Vector3 Force;
        public readonly ForceMode Mode;
        public readonly ForcePacketPriority Priority;
        public readonly bool Wake;
        public readonly ForcePacketFlags ExtraFlags;

        public QueueForceArgs(Vector3 force, ForceMode mode, ForcePacketPriority priority, bool wake, ForcePacketFlags extraFlags = ForcePacketFlags.None)
        {
            Force = force;
            Mode = mode;
            Priority = priority;
            Wake = wake;
            ExtraFlags = extraFlags;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ForcePacket
    {
        /// <summary>World-space force vector.</summary>
        [FieldOffset(0)]
        public Vector3 Force;

        /// <summary>World-space torque vector.</summary>
        [FieldOffset(12)]
        public Vector3 Torque;

        /// <summary>World-space offset from the rigidbody center of mass used by deferred AddForceAtPosition routing.</summary>
        [FieldOffset(24)]
        public Vector3 PointOffset;

        /// <summary>Force application mode.</summary>
        [FieldOffset(36)]
        public ForceMode Mode;

        /// <summary>Dense rigidbody slot index owned by <see cref="PhysicsApplySystem"/>.</summary>
        [FieldOffset(40)]
        public int RigidbodyIndex;

        /// <summary>Bitfield flags describing packet contents.</summary>
        [FieldOffset(44)]
        public byte Flags;

        /// <summary>Priority class used by contention and entanglement guards.</summary>
        [FieldOffset(45)]
        public ForcePacketPriority Priority;

        [FieldOffset(46)]
        private byte _padding0;
        [FieldOffset(47)]
        private byte _padding1;
        [FieldOffset(48)]
        private byte _padding2;
        [FieldOffset(49)]
        private byte _padding3;
        [FieldOffset(50)]
        private byte _padding4;
        [FieldOffset(51)]
        private byte _padding5;
        [FieldOffset(52)]
        private byte _padding6;
        [FieldOffset(53)]
        private byte _padding7;
        [FieldOffset(54)]
        private byte _padding8;
        [FieldOffset(55)]
        private byte _padding9;
        [FieldOffset(56)]
        private byte _padding10;
        [FieldOffset(57)]
        private byte _padding11;
        [FieldOffset(58)]
        private byte _padding12;
        [FieldOffset(59)]
        private byte _padding13;
        [FieldOffset(60)]
        private byte _padding14;
        [FieldOffset(61)]
        private byte _padding15;
        [FieldOffset(62)]
        private byte _padding16;
        [FieldOffset(63)]
        private byte _padding17;
    }

    /// <summary>
    /// False or authored acoustic ping payload consumed by sonar and PDA signal systems.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticPingEvent
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
            this = default;
            RuntimePosition = runtimePosition;
            RadiusMeters = math.max(0f, radiusMeters);
            Intensity01 = math.saturate(intensity01);
            LifetimeSeconds = math.max(0f, lifetimeSeconds);
            SignalRole = signalRole;
            SourceSpeciesId = sourceSpeciesId;
            EnergyJoules = math.max(0f, energyJoules);
        }

        /// <summary>Runtime-space origin of the ping.</summary>
        [FieldOffset(0)]
        public Vector3 RuntimePosition;

        /// <summary>World-space radius in authored meters.</summary>
        [FieldOffset(12)]
        public float RadiusMeters;

        /// <summary>Normalized signal intensity.</summary>
        [FieldOffset(16)]
        public float Intensity01;

        /// <summary>Transient acoustic lifetime in seconds.</summary>
        [FieldOffset(20)]
        public float LifetimeSeconds;

        /// <summary>PDA-facing role label used by signal displays.</summary>
        [FieldOffset(24)]
        public FieldTargetRole SignalRole;

        /// <summary>Stable species id of the emitter.</summary>
        [FieldOffset(28)]
        public int SourceSpeciesId;

        /// <summary>Authored or measured acoustic energy used to reject spoof pings.</summary>
        [FieldOffset(32)]
        public float EnergyJoules;
        [FieldOffset(36)]
        private byte _pad0;
        [FieldOffset(37)]
        private byte _pad1;
        [FieldOffset(38)]
        private byte _pad2;
        [FieldOffset(39)]
        private byte _pad3;
        [FieldOffset(40)]
        private byte _pad4;
        [FieldOffset(41)]
        private byte _pad5;
        [FieldOffset(42)]
        private byte _pad6;
        [FieldOffset(43)]
        private byte _pad7;
        [FieldOffset(44)]
        private byte _pad8;
        [FieldOffset(45)]
        private byte _pad9;
        [FieldOffset(46)]
        private byte _pad10;
        [FieldOffset(47)]
        private byte _pad11;
        [FieldOffset(48)]
        private byte _pad12;
        [FieldOffset(49)]
        private byte _pad13;
        [FieldOffset(50)]
        private byte _pad14;
        [FieldOffset(51)]
        private byte _pad15;
        [FieldOffset(52)]
        private byte _pad16;
        [FieldOffset(53)]
        private byte _pad17;
        [FieldOffset(54)]
        private byte _pad18;
        [FieldOffset(55)]
        private byte _pad19;
        [FieldOffset(56)]
        private byte _pad20;
        [FieldOffset(57)]
        private byte _pad21;
        [FieldOffset(58)]
        private byte _pad22;
        [FieldOffset(59)]
        private byte _pad23;
        [FieldOffset(60)]
        private byte _pad24;
        [FieldOffset(61)]
        private byte _pad25;
        [FieldOffset(62)]
        private byte _pad26;
        [FieldOffset(63)]
        private byte _pad27;
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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AcousticImpulseEvent
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
            this = default;
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

        [FieldOffset(0)]
        public Vector3 RuntimePosition;
        [FieldOffset(12)]
        public Vector3 Direction;
        [FieldOffset(24)]
        public float KineticEnergyJoules;
        [FieldOffset(28)]
        public float Volume01;
        [FieldOffset(32)]
        public float PitchScale;
        [FieldOffset(36)]
        public float RadiusMeters;
        [FieldOffset(40)]
        public int SourceBodyInstanceId;
        [FieldOffset(44)]
        public byte AudioMaterialId;
        [FieldOffset(45)]
        public AcousticImpulseFlags Flags;
        [FieldOffset(46)]
        private byte _pad0;
        [FieldOffset(47)]
        private byte _pad1;
        [FieldOffset(48)]
        private byte _pad2;
        [FieldOffset(49)]
        private byte _pad3;
        [FieldOffset(50)]
        private byte _pad4;
        [FieldOffset(51)]
        private byte _pad5;
        [FieldOffset(52)]
        private byte _pad6;
        [FieldOffset(53)]
        private byte _pad7;
        [FieldOffset(54)]
        private byte _pad8;
        [FieldOffset(55)]
        private byte _pad9;
        [FieldOffset(56)]
        private byte _pad10;
        [FieldOffset(57)]
        private byte _pad11;
        [FieldOffset(58)]
        private byte _pad12;
        [FieldOffset(59)]
        private byte _pad13;
        [FieldOffset(60)]
        private byte _pad14;
        [FieldOffset(61)]
        private byte _pad15;
        [FieldOffset(62)]
        private byte _pad16;
        [FieldOffset(63)]
        private byte _pad17;

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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LargeAcousticImpulseEvent
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
            this = default;
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

        [FieldOffset(0)]
        public Vector3 RuntimePosition;
        [FieldOffset(12)]
        public Vector3 Direction;
        [FieldOffset(24)]
        public float KineticEnergyJoules;
        [FieldOffset(28)]
        public float Volume01;
        [FieldOffset(32)]
        public float PitchScale;
        [FieldOffset(36)]
        public float RadiusMeters;
        [FieldOffset(40)]
        public int SourceBodyInstanceId;
        [FieldOffset(44)]
        public byte AudioMaterialId;
        [FieldOffset(45)]
        public AcousticImpulseFlags Flags;
        [FieldOffset(46)]
        private byte _pad0;
        [FieldOffset(47)]
        private byte _pad1;
        [FieldOffset(48)]
        private byte _pad2;
        [FieldOffset(49)]
        private byte _pad3;
        [FieldOffset(50)]
        private byte _pad4;
        [FieldOffset(51)]
        private byte _pad5;
        [FieldOffset(52)]
        private byte _pad6;
        [FieldOffset(53)]
        private byte _pad7;
        [FieldOffset(54)]
        private byte _pad8;
        [FieldOffset(55)]
        private byte _pad9;
        [FieldOffset(56)]
        private byte _pad10;
        [FieldOffset(57)]
        private byte _pad11;
        [FieldOffset(58)]
        private byte _pad12;
        [FieldOffset(59)]
        private byte _pad13;
        [FieldOffset(60)]
        private byte _pad14;
        [FieldOffset(61)]
        private byte _pad15;
        [FieldOffset(62)]
        private byte _pad16;
        [FieldOffset(63)]
        private byte _pad17;

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
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct RemovedPhysicsEventPayload
    {
        [FieldOffset(0)]
        public Vector3 RuntimePosition;
        [FieldOffset(12)]
        public Vector3 Direction;
        [FieldOffset(24)]
        public Vector3 ForceVector;
        [FieldOffset(36)]
        public Vector3 ImpulseVector;
        [FieldOffset(48)]
        public float RadiusMeters;
        [FieldOffset(52)]
        public float Scalar0;
        [FieldOffset(56)]
        public float Scalar1;
        [FieldOffset(60)]
        public float Scalar2;
        [FieldOffset(64)]
        public int PrimaryId;
        [FieldOffset(68)]
        public uint DataHash;
        [FieldOffset(72)]
        public uint StatusBits;
        [FieldOffset(76)]
        public ushort EventType;
        [FieldOffset(78)]
        public ushort Reserved;
        [FieldOffset(80)]
        private byte _pad0;
        [FieldOffset(81)]
        private byte _pad1;
        [FieldOffset(82)]
        private byte _pad2;
        [FieldOffset(83)]
        private byte _pad3;
        [FieldOffset(84)]
        private byte _pad4;
        [FieldOffset(85)]
        private byte _pad5;
        [FieldOffset(86)]
        private byte _pad6;
        [FieldOffset(87)]
        private byte _pad7;
        [FieldOffset(88)]
        private byte _pad8;
        [FieldOffset(89)]
        private byte _pad9;
        [FieldOffset(90)]
        private byte _pad10;
        [FieldOffset(91)]
        private byte _pad11;
        [FieldOffset(92)]
        private byte _pad12;
        [FieldOffset(93)]
        private byte _pad13;
        [FieldOffset(94)]
        private byte _pad14;
        [FieldOffset(95)]
        private byte _pad15;
        [FieldOffset(96)]
        private byte _pad16;
        [FieldOffset(97)]
        private byte _pad17;
        [FieldOffset(98)]
        private byte _pad18;
        [FieldOffset(99)]
        private byte _pad19;
        [FieldOffset(100)]
        private byte _pad20;
        [FieldOffset(101)]
        private byte _pad21;
        [FieldOffset(102)]
        private byte _pad22;
        [FieldOffset(103)]
        private byte _pad23;
        [FieldOffset(104)]
        private byte _pad24;
        [FieldOffset(105)]
        private byte _pad25;
        [FieldOffset(106)]
        private byte _pad26;
        [FieldOffset(107)]
        private byte _pad27;
        [FieldOffset(108)]
        private byte _pad28;
        [FieldOffset(109)]
        private byte _pad29;
        [FieldOffset(110)]
        private byte _pad30;
        [FieldOffset(111)]
        private byte _pad31;
        [FieldOffset(112)]
        private byte _pad32;
        [FieldOffset(113)]
        private byte _pad33;
        [FieldOffset(114)]
        private byte _pad34;
        [FieldOffset(115)]
        private byte _pad35;
        [FieldOffset(116)]
        private byte _pad36;
        [FieldOffset(117)]
        private byte _pad37;
        [FieldOffset(118)]
        private byte _pad38;
        [FieldOffset(119)]
        private byte _pad39;
        [FieldOffset(120)]
        private byte _pad40;
        [FieldOffset(121)]
        private byte _pad41;
        [FieldOffset(122)]
        private byte _pad42;
        [FieldOffset(123)]
        private byte _pad43;
        [FieldOffset(124)]
        private byte _pad44;
        [FieldOffset(125)]
        private byte _pad45;
        [FieldOffset(126)]
        private byte _pad46;
        [FieldOffset(127)]
        private byte _pad47;
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
    /// Water-mass mutation payload published by scalar flood solvers.
    /// </summary>
    public readonly struct FloodMassShiftEvent
    {
        public readonly Vector3 DynamicCenterOfMassLocal;
        public readonly Vector3 CenterOfMassOffsetLocal;
        public readonly float TotalWaterMassKg;
        public readonly float FillRatio01;
        public readonly float AngularDragMultiplier;
        public readonly int SourceBodyInstanceId;
        public readonly uint Frame;
        public readonly byte MathLod;
        public readonly byte Flags;

        public FloodMassShiftEvent(
            Vector3 dynamicCenterOfMassLocal,
            Vector3 centerOfMassOffsetLocal,
            float totalWaterMassKg,
            float fillRatio01,
            float angularDragMultiplier,
            int sourceBodyInstanceId,
            uint frame,
            byte mathLod,
            byte flags)
        {
            DynamicCenterOfMassLocal = dynamicCenterOfMassLocal;
            CenterOfMassOffsetLocal = centerOfMassOffsetLocal;
            TotalWaterMassKg = totalWaterMassKg;
            FillRatio01 = fillRatio01;
            AngularDragMultiplier = angularDragMultiplier;
            SourceBodyInstanceId = sourceBodyInstanceId;
            Frame = frame;
            MathLod = mathLod;
            Flags = flags;
        }
    }

    /// <summary>
    /// Typed signal-lane physics-domain event surface for transient physics signals.
    /// </summary>
    public static class PhysicsEventBus
    {
        private static int s_x001DirectSignalPushDropCount_PhysicsApplySystem;

        private static int s_x001PhysicsApplySystemSignalPushDropCount;
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 128;
        private const ushort EventCircuitBreakerDepthLimit = 5;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("PhysicsEventBus.Overflow"));
        private static readonly uint _circuitBreakerWarningHash = unchecked((uint)LocHash.Compute("PhysicsEventBus.CircuitBreaker"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute(nameof(PhysicsEventPayload)));

        private static readonly PressureListenerSlot[] _pressureListeners = new PressureListenerSlot[ListenerCapacity]; // COLD ALLOC: PressureListenerSlot[32] - pressure impulse listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly EmpListenerSlot[] _empListeners = new EmpListenerSlot[ListenerCapacity]; // COLD ALLOC: EmpListenerSlot[32] - EMP listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly AcousticListenerSlot[] _acousticListeners = new AcousticListenerSlot[ListenerCapacity]; // COLD ALLOC: AcousticListenerSlot[32] - acoustic ping listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static readonly AcousticImpulseListenerSlot[] _acousticImpulseListeners = new AcousticImpulseListenerSlot[ListenerCapacity]; // COLD ALLOC: AcousticImpulseListenerSlot[32] - kinetic acoustic impulse listeners drained by SystemDispatcher LateUpdate - owner: PhysicsEventBus
        private static int _pressureListenerCount;
        private static int _empListenerCount;
        private static int _acousticListenerCount;
        private static int _acousticImpulseListenerCount;
        private static int _snapshotReadFrame = -1;
        private static int _snapshotReadCursor;
        private static int _lastOverflowWarningFrame = -1;
        private static int _lastCircuitBreakerWarningFrame = -1;
        private static int _droppedEventCount;
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

        public static int DroppedEventCount => _droppedEventCount;

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
            for (int i = 0; i < _pressureListenerCount; i++)
                _pressureListeners[i].Clear();
            for (int i = 0; i < _empListenerCount; i++)
                _empListeners[i].Clear();
            for (int i = 0; i < _acousticListenerCount; i++)
                _acousticListeners[i].Clear();
            for (int i = 0; i < _acousticImpulseListenerCount; i++)
                _acousticImpulseListeners[i].Clear();
            _pressureListenerCount = 0;
            _empListenerCount = 0;
            _acousticListenerCount = 0;
            _acousticImpulseListenerCount = 0;
            _snapshotReadFrame = -1;
            _snapshotReadCursor = 0;
            _lastOverflowWarningFrame = -1;
            _lastCircuitBreakerWarningFrame = -1;
            _droppedEventCount = 0;
            _activeDispatchDepth = 0;
            _initialized = false;
        }

        /// <summary>Registers a pressure impulse listener.</summary>
        public static void Register(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            RegisterPressureListenerImmediate(listener);
        }

        /// <summary>Unregisters a pressure impulse listener.</summary>
        public static void Unregister(IPressureImpulseEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterPressureListenerImmediate(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.PressureImpulse, _pressureListenerCount);
        }

        /// <summary>Registers an electromagnetic pulse listener.</summary>
        public static void Register(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            RegisterEmpListenerImmediate(listener);
        }

        /// <summary>Unregisters an electromagnetic pulse listener.</summary>
        public static void Unregister(IElectromagneticPulseEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterEmpListenerImmediate(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.ElectromagneticPulse, _empListenerCount);
        }

        /// <summary>Registers an acoustic ping listener.</summary>
        public static void Register(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            RegisterAcousticListenerImmediate(listener);
        }

        /// <summary>Unregisters an acoustic ping listener.</summary>
        public static void Unregister(IAcousticPingEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterAcousticListenerImmediate(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.AcousticPing, _acousticListenerCount);
        }

        /// <summary>Registers a kinetic acoustic impulse listener.</summary>
        public static void Register(IPhysicsAcousticImpulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureReady();
            RegisterAcousticImpulseListenerImmediate(listener);
        }

        /// <summary>Unregisters a kinetic acoustic impulse listener.</summary>
        public static void Unregister(IPhysicsAcousticImpulseEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterAcousticImpulseListenerImmediate(listener);
            DropQueuedPayloadsForTypeIfNoListeners(PhysicsEventType.AcousticImpulse, _acousticImpulseListenerCount);
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

            int currentFrame = CurrentDispatcherFrameIndex();
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

        public static bool TryNotifyPressureImpulse(in PressureImpulseEvent pressureEvent)
        {
            if (_pressureListenerCount <= 0)
                return false;

            return Enqueue(new PhysicsEventPayload
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
        [Obsolete("Use TryNotifyElectromagneticPulse(in ElectromagneticPulseEvent) so bounded rejection stays visible at the producer.", true)]
        public static void NotifyElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            TryNotifyElectromagneticPulse(in pulseEvent);
        }

        public static bool TryNotifyElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            return Enqueue(new PhysicsEventPayload
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
        [Obsolete("Use TryNotifyAcousticPing(in AcousticPingEvent) so bounded rejection stays visible at the producer.", true)]
        public static void NotifyAcousticPing(in AcousticPingEvent pingEvent)
        {
            TryNotifyAcousticPing(in pingEvent);
        }

        public static bool TryNotifyAcousticPing(in AcousticPingEvent pingEvent)
        {
            return Enqueue(new PhysicsEventPayload
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
        [Obsolete("Use TryNotifyAcousticImpulse(in AcousticImpulseEvent) so bounded rejection stays visible at the producer.", true)]
        public static void NotifyAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            TryNotifyAcousticImpulse(in impulseEvent);
        }

        public static bool TryNotifyAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            return Enqueue(new PhysicsEventPayload
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
        [Obsolete("Use TryNotifyLargeAcousticImpulse(in LargeAcousticImpulseEvent) so bounded rejection stays visible at the producer.", true)]
        public static void NotifyLargeAcousticImpulse(in LargeAcousticImpulseEvent impulseEvent)
        {
            AcousticImpulseEvent acousticImpulseEvent = impulseEvent.ToAcousticImpulseEvent();
            TryNotifyAcousticImpulse(in acousticImpulseEvent);
        }

        public static bool TryNotifyLargeAcousticImpulse(in LargeAcousticImpulseEvent impulseEvent)
        {
            AcousticImpulseEvent acousticImpulseEvent = impulseEvent.ToAcousticImpulseEvent();
            return TryNotifyAcousticImpulse(in acousticImpulseEvent);
        }

        /// <summary>Broadcasts one scalar flood mass/center-of-mass mutation payload.</summary>
        [Obsolete("Use TryNotifyFloodMassShift(in FloodMassShiftEvent) so bounded rejection stays visible at the producer.", true)]
        public static void NotifyFloodMassShift(in FloodMassShiftEvent massShiftEvent)
        {
            TryNotifyFloodMassShift(in massShiftEvent);
        }

        public static bool TryNotifyFloodMassShift(in FloodMassShiftEvent massShiftEvent)
        {
            return Enqueue(new PhysicsEventPayload
            {
                RuntimePosition = massShiftEvent.DynamicCenterOfMassLocal,
                Direction = massShiftEvent.CenterOfMassOffsetLocal,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = math.saturate(massShiftEvent.FillRatio01),
                Scalar0 = math.max(0f, massShiftEvent.TotalWaterMassKg),
                Scalar1 = math.max(0f, massShiftEvent.AngularDragMultiplier),
                Scalar2 = math.saturate(massShiftEvent.FillRatio01),
                PrimaryId = massShiftEvent.SourceBodyInstanceId,
                DataHash = massShiftEvent.Frame,
                StatusBits = PackFloodMassStatusBits(massShiftEvent.MathLod, massShiftEvent.Flags),
                EventType = (ushort)PhysicsEventType.FloodMassShift,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            SignalBus<PhysicsEventPayload>.EnsureInitialized();
            _initialized = true;
        }

        private static bool Enqueue(in PhysicsEventPayload payload)
        {
            int queuedDepth = _activeDispatchDepth > 0 ? _activeDispatchDepth + 1 : 1;
            if (queuedDepth >= EventCircuitBreakerDepthLimit)
            {
                ReportCircuitBreakerOncePerFrame(queuedDepth);
                IncrementDroppedEventCount();
                return false;
            }

            EnsureInitialized();
            PhysicsEventPayload queuedPayload = payload;
            queuedPayload.Reserved = (ushort)math.max(1, queuedDepth);
            if (SignalBus<PhysicsEventPayload>.TryPushTracked(in queuedPayload, ref s_x001DirectSignalPushDropCount_PhysicsApplySystem))
                return true;

            IncrementDroppedEventCount();
            return false;
        }

        private static void IncrementDroppedEventCount()
        {
            if (_droppedEventCount < 0x3FFFFFFF)
                _droppedEventCount++;
        }

        private static bool HasAnyListener()
        {
            return _pressureListenerCount > 0 ||
                   _empListenerCount > 0 ||
                   _acousticListenerCount > 0 ||
                   _acousticImpulseListenerCount > 0;
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
                SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001PhysicsApplySystemSignalPushDropCount);
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
                    case PhysicsEventType.FloodMassShift:
                        // Flood mass truth is consumed through SubmarineFloodStateSignal or raw PhysicsEventPayload snapshots.
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
            int count = _pressureListenerCount;
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

            for (int i = count - 1; i >= 0; i--)
            {
                IPressureImpulseEventListener listener = _pressureListeners[i].Listener;
                if (listener != null)
                    listener.OnPressureImpulse(in pressureEvent);
            }
        }

        private static void DispatchElectromagneticPulse(in PhysicsEventPayload payload)
        {
            int count = _empListenerCount;
            if (count <= 0)
                return;

            ElectromagneticPulseEvent pulseEvent = new ElectromagneticPulseEvent(
                payload.RuntimePosition,
                payload.RadiusMeters,
                payload.Scalar0,
                payload.Scalar1,
                payload.DataHash,
                unchecked((ushort)payload.StatusBits));

            for (int i = count - 1; i >= 0; i--)
            {
                IElectromagneticPulseEventListener listener = _empListeners[i].Listener;
                if (listener != null)
                    listener.OnElectromagneticPulse(in pulseEvent);
            }
        }

        private static void DispatchAcousticPing(in PhysicsEventPayload payload)
        {
            int count = _acousticListenerCount;
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

            for (int i = count - 1; i >= 0; i--)
            {
                IAcousticPingEventListener listener = _acousticListeners[i].Listener;
                if (listener != null)
                    listener.OnAcousticPing(in pingEvent);
            }
        }

        private static void DispatchAcousticImpulse(in PhysicsEventPayload payload)
        {
            int count = _acousticImpulseListenerCount;
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

            for (int i = count - 1; i >= 0; i--)
            {
                IPhysicsAcousticImpulseEventListener listener = _acousticImpulseListeners[i].Listener;
                if (listener != null)
                    listener.OnAcousticImpulse(in impulseEvent);
            }
        }

        private static void RegisterPressureListenerImmediate(IPressureImpulseEventListener listener)
        {
            if (ContainsPressureListenerImmediate(listener) || _pressureListenerCount >= ListenerCapacity)
                return;

            _pressureListeners[_pressureListenerCount].Listener = listener;
            _pressureListenerCount++;
        }

        private static bool TryUnregisterPressureListenerImmediate(IPressureImpulseEventListener listener)
        {
            for (int i = 0; i < _pressureListenerCount; i++)
            {
                if (!ReferenceEquals(_pressureListeners[i].Listener, listener))
                    continue;

                int lastIndex = _pressureListenerCount - 1;
                _pressureListeners[i] = _pressureListeners[lastIndex];
                _pressureListeners[lastIndex].Clear();
                _pressureListenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsPressureListenerImmediate(IPressureImpulseEventListener listener)
        {
            for (int i = 0; i < _pressureListenerCount; i++)
            {
                if (ReferenceEquals(_pressureListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void RegisterEmpListenerImmediate(IElectromagneticPulseEventListener listener)
        {
            if (ContainsEmpListenerImmediate(listener) || _empListenerCount >= ListenerCapacity)
                return;

            _empListeners[_empListenerCount].Listener = listener;
            _empListenerCount++;
        }

        private static bool TryUnregisterEmpListenerImmediate(IElectromagneticPulseEventListener listener)
        {
            for (int i = 0; i < _empListenerCount; i++)
            {
                if (!ReferenceEquals(_empListeners[i].Listener, listener))
                    continue;

                int lastIndex = _empListenerCount - 1;
                _empListeners[i] = _empListeners[lastIndex];
                _empListeners[lastIndex].Clear();
                _empListenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsEmpListenerImmediate(IElectromagneticPulseEventListener listener)
        {
            for (int i = 0; i < _empListenerCount; i++)
            {
                if (ReferenceEquals(_empListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void RegisterAcousticListenerImmediate(IAcousticPingEventListener listener)
        {
            if (ContainsAcousticListenerImmediate(listener) || _acousticListenerCount >= ListenerCapacity)
                return;

            _acousticListeners[_acousticListenerCount].Listener = listener;
            _acousticListenerCount++;
        }

        private static bool TryUnregisterAcousticListenerImmediate(IAcousticPingEventListener listener)
        {
            for (int i = 0; i < _acousticListenerCount; i++)
            {
                if (!ReferenceEquals(_acousticListeners[i].Listener, listener))
                    continue;

                int lastIndex = _acousticListenerCount - 1;
                _acousticListeners[i] = _acousticListeners[lastIndex];
                _acousticListeners[lastIndex].Clear();
                _acousticListenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsAcousticListenerImmediate(IAcousticPingEventListener listener)
        {
            for (int i = 0; i < _acousticListenerCount; i++)
            {
                if (ReferenceEquals(_acousticListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void RegisterAcousticImpulseListenerImmediate(IPhysicsAcousticImpulseEventListener listener)
        {
            if (ContainsAcousticImpulseListenerImmediate(listener) || _acousticImpulseListenerCount >= ListenerCapacity)
                return;

            _acousticImpulseListeners[_acousticImpulseListenerCount].Listener = listener;
            _acousticImpulseListenerCount++;
        }

        private static bool TryUnregisterAcousticImpulseListenerImmediate(IPhysicsAcousticImpulseEventListener listener)
        {
            for (int i = 0; i < _acousticImpulseListenerCount; i++)
            {
                if (!ReferenceEquals(_acousticImpulseListeners[i].Listener, listener))
                    continue;

                int lastIndex = _acousticImpulseListenerCount - 1;
                _acousticImpulseListeners[i] = _acousticImpulseListeners[lastIndex];
                _acousticImpulseListeners[lastIndex].Clear();
                _acousticImpulseListenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsAcousticImpulseListenerImmediate(IPhysicsAcousticImpulseEventListener listener)
        {
            for (int i = 0; i < _acousticImpulseListenerCount; i++)
            {
                if (ReferenceEquals(_acousticImpulseListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private struct PressureListenerSlot
        {
            public IPressureImpulseEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct EmpListenerSlot
        {
            public IElectromagneticPulseEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct AcousticListenerSlot
        {
            public IAcousticPingEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct AcousticImpulseListenerSlot
        {
            public IPhysicsAcousticImpulseEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private static uint PackFloodMassStatusBits(byte mathLod, byte flags)
        {
            return (uint)(mathLod | (flags << 8));
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = CurrentDispatcherFrameIndex();
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }

        private static void ReportCircuitBreakerOncePerFrame(int chainDepth)
        {
            int frame = CurrentDispatcherFrameIndex();
            if (_lastCircuitBreakerWarningFrame == frame)
                return;

            _lastCircuitBreakerWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_circuitBreakerWarningHash, _queueHash, chainDepth);
        }

        private static int CurrentDispatcherFrameIndex()
        {
            uint frame = SystemDispatcher.CurrentFrameId;
            return unchecked((int)(frame != 0u ? frame : 1u));
        }
    }

    [System.Flags]
    public enum ForcePacketFlags : byte
    {
        None = 0,
        HasForce = 1 << 0,
        HasTorque = 1 << 1,
        WakeBody = 1 << 2,
        ApplyAtPosition = 1 << 3,
        BiomeBuoyancy = 1 << 4,
        SetLinearVelocity = 1 << 5,
        SetAngularVelocity = 1 << 6,
        SetPose = 1 << 7,
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct ValidateForcePacketsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<ForcePacket> Packets;
        [NoAlias] public NativeArray<byte> ValidityMask;
        public int MaxTrackedBodies;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)Packets.Length || (uint)index >= (uint)ValidityMask.Length)
                return;

            ForcePacket packet = Packets[index];
            ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
            bool setsPose = (flags & ForcePacketFlags.SetPose) != 0;
            bool validBodyIndex = packet.RigidbodyIndex >= 0 & packet.RigidbodyIndex < MaxTrackedBodies;
            bool validMode = packet.Mode == ForceMode.Force |
                             packet.Mode == ForceMode.Acceleration |
                             packet.Mode == ForceMode.Impulse |
                             packet.Mode == ForceMode.VelocityChange;
            bool requiresForce = (flags & (ForcePacketFlags.HasForce | ForcePacketFlags.SetLinearVelocity | ForcePacketFlags.SetPose)) != 0;
            bool requiresTorque = (flags & (ForcePacketFlags.HasTorque | ForcePacketFlags.SetAngularVelocity | ForcePacketFlags.SetPose)) != 0;
            bool validForce = !requiresForce | IsFinite(packet.Force);
            bool validTorque = !requiresTorque | IsFinite(packet.Torque);
            bool validPointOffset = IsFinite(packet.PointOffset);
            float4 poseRotation = new float4(packet.Torque.x, packet.Torque.y, packet.Torque.z, packet.PointOffset.x);
            float poseRotationLengthSq = math.lengthsq(poseRotation);
            bool validPoseRotation = !setsPose |
                                     (math.isfinite(poseRotationLengthSq) &
                                      poseRotationLengthSq > 0.000001f);
            bool validPacket = validBodyIndex & validMode & validForce & validTorque & validPointOffset & validPoseRotation;
            ValidityMask[index] = (byte)math.select(0, 1, validPacket);
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
    public sealed partial class PhysicsApplySystem : MonoBehaviour, IPhysicsService, ISceneTransitionPhysicsBridge, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PhysicsApplySystemSignalPushDropCount;
        private const int MaxTrackedBodies = 64;
        private const int MaxQueuedPackets = 64;
        private const int MaxForcePacketsAppliedPerFixedTick = 64;
        private const int MaxQueuedSubmarineImpactSignals = 32;
        private const int MaxActiveDepressurizationVortices = 8;
        private const int MaxActiveImpactProxyLights = 4;
        private const int DepressurizationVortexContactCapacity = 32;
        private const int ImplosionOverlapCapacity = 64;
        private const int SubmarineColliderScratchCapacity = 64;
        private const SystemID OwnerSystemId = SystemID.Physics;
        private const float MinMagnitudeSq = 0.000001f;
        private const float HydrodynamicPlayerEquivalentMassKg = 80f;
        private const uint KccVelocityForceSinkMaxAgeFrames = 12u;
        private const float DepressurizationVortexDistanceFloorMeters = 0.5f;
        private const float HullYieldThresholdJoules = 225000f;
        private const float AcousticImpulseReferenceEnergyJoules = HullYieldThresholdJoules;
        private const float AcousticImpulseVolumeEnergyScale = 0.00001f;
        private const float AcousticImpulseMaximumVolumeEnergyJoules = 100000f;
        private const float AcousticImpulseMinimumRadiusMeters = 8f;
        private const float AcousticImpulseMaximumRadiusMeters = 64f;
        private const float MechanicalSparkProxyLightDurationSeconds = 0.05f;
        private const float MechanicalSparkProxyLightMinimumVolume = 0.34f;
        private const float MechanicalSparkProxyLightFrameSeconds = 0.02f;
        private const string NonFiniteForceLog = "[PhysicsApplySystem] Non-finite force packet detected. Zeroing vector.";
        private const string NonFiniteTorqueLog = "[PhysicsApplySystem] Non-finite torque packet detected. Zeroing vector.";
        private const string NonFinitePointOffsetLog = "[PhysicsApplySystem] Non-finite point-offset packet detected. Zeroing offset.";
        private const string InvalidForcePacketLog = "[PhysicsApplySystem] Burst packet validation rejected a non-finite or out-of-range packet.";
        private const string ToxicVectorLog = "TOXIC_VECTOR detected; payload stored in CrashTelemetryBuffer.";
        private const double FlushBudgetWarningMilliseconds = 0.2d;
        private const int FlushBudgetWarningCooldownFrames = 30;
        private const int ForcePacketWarningCooldownFrames = 30;
        private const uint ValidationSchedulePinPackets = 1u << 0;
        private const uint ValidationSchedulePinMask = 1u << 1;
        private static readonly uint ForcePacketClipWarningHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.ForcePacketClip"));
        private static readonly uint ForcePacketQueueHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.ForcePacketQueue"));
        private static readonly uint PhysicsFlushBudgetWarningHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.FlushBudget"));
        private static readonly uint PhysicsFlushContextHash = unchecked((uint)LocHash.Compute("PhysicsApplySystem.FlushFrontBuffer"));
        private static readonly uint NanRecoverySystemHash = unchecked((uint)LocHash.Compute(nameof(PhysicsApplySystem)));
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FixedTick");
        private static readonly ProfilerMarker _packetValidationProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.ValidatePackets");
        private static readonly ProfilerMarker _flushFrontBufferProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FlushFrontBuffer");
        private static PhysicsApplySystem s_runtimeInstance;
        // COLD ALLOC: SpatialQueryHit[64] - registered implosion contact scratch for zero-GC radius impulse dispatch - owner: PhysicsApplySystem
        private readonly SpatialQueryHit[] _implosionContacts = new SpatialQueryHit[ImplosionOverlapCapacity];

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
            public float ExpireAtDispatcherSeconds;
        }

        // COLD ALLOC: Rigidbody[64] - active rigidbody slot map for deferred packet application - owner: PhysicsApplySystem
        private readonly Rigidbody[] _bodySlots = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: AbsoluteUniversePosition[64] - last finite Rigidbody AUP for NaN recovery - owner: PhysicsApplySystem
        private readonly AbsoluteUniversePosition[] _lastFiniteBodyAups = new AbsoluteUniversePosition[MaxTrackedBodies];
        // COLD ALLOC: byte[64] - validity mask for last finite Rigidbody AUP cache - owner: PhysicsApplySystem
        private readonly byte[] _lastFiniteBodyAupValid = new byte[MaxTrackedBodies];
        // COLD ALLOC: ForcePacket[64] - front-buffer apply snapshot so Vault locks never cover Unity Rigidbody API calls - owner: PhysicsApplySystem
        private readonly ForcePacket[] _forcePacketApplyScratch = new ForcePacket[MaxForcePacketsAppliedPerFixedTick];
        // COLD ALLOC: byte[64] - front-buffer validation snapshot paired with `_forcePacketApplyScratch` - owner: PhysicsApplySystem
        private readonly byte[] _forcePacketApplyValidityScratch = new byte[MaxForcePacketsAppliedPerFixedTick];
        // COLD ALLOC: DepressurizationVortex[8] - active breach-vortex slots - owner: PhysicsApplySystem
        private readonly DepressurizationVortex[] _depressurizationVortices = new DepressurizationVortex[MaxActiveDepressurizationVortices];
        // COLD ALLOC: SpatialQueryHit[32] - spatial-hash scratch for breach-vortex loose-body collection - owner: PhysicsApplySystem
        private readonly SpatialQueryHit[] _depressurizationVortexContacts = new SpatialQueryHit[DepressurizationVortexContactCapacity];
        // COLD ALLOC: Rigidbody[32] - unique body scratch for breach-vortex force routing - owner: PhysicsApplySystem
        private readonly Rigidbody[] _depressurizationVortexBodies = new Rigidbody[DepressurizationVortexContactCapacity];
        // COLD ALLOC: TransientProxyLightHandle[4] - bounded 0.05s impact proxy-light handles, no GameObject sparks - owner: PhysicsApplySystem
        private readonly TransientProxyLightHandle[] _impactProxyLights = new TransientProxyLightHandle[MaxActiveImpactProxyLights];
        // COLD ALLOC: List<Collider>[64] - submarine hull collider discovery for contact-modification enablement - owner: PhysicsApplySystem
        private readonly List<Collider> _submarineColliderScratch = new List<Collider>(SubmarineColliderScratchCapacity);
        private VaultGenerationHandle<ForcePacket> _frontPacketBufferHandle;
        private VaultGenerationHandle<ForcePacket> _backPacketBufferHandle;
        private VaultGenerationHandle<ForcePacket> _validationPacketBufferHandle;
        private VaultGenerationHandle<byte> _validationMaskBufferHandle;
        private IDataVault _dataVault;
        private IPhysicsCullingOverseer _physicsCullingOverseer;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerMovementForceSink _playerMovementForceSink;
        private IPlayerMovementPoseReadModel _playerMovementPoseReadModel;
        private Rigidbody _submarineHullBody;
        private SubmarineFluidDynamics _submarineFluidDynamics;
        private SubmarineStructuralGrid _submarineStructuralGrid;
        private int _submarineImpactSnapshotReadFrame = -1;
        private int _submarineImpactSnapshotReadCursor;
        private bool _submarineImpactSignalLaneInitialized;

        private int _frontCount;
        private int _backCount;
        private bool _isInitialized;
        private bool _fixedTickRegistered;
        private bool _postFixedTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _hotSwapRegistered;
        private bool _frontBufferValidationReady;
        private bool _packetValidationScheduled;
        private JobHandle _packetValidationHandle;
        private IDataVault _validationSchedulePinVault;
        private uint _validationSchedulePinMask;
        private bool _contactModifySubscribed;
        private bool _submarineModifiableContactsArmed;
        private ulong _submarineHullEntityId;
        private int _nextFlushBudgetWarningFrame;
        private int _nextForcePacketClipWarningFrame;
        private int _nextForcePacketSaturationWarningFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeInstance()
        {
            s_runtimeInstance = null;
        }

        /// <summary>
        /// True once the service is registered into <see cref="GlobalRegistry"/>.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Cold-published physics apply runtime. Hot static routes must not poll GlobalRegistry.
        /// </summary>
        public static PhysicsApplySystem Instance => s_runtimeInstance;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.Physics / physics-apply owner.
        /// Hot static routes (TriggerDepressurizationVortex, TriggerImplosionImpulse, packet
        /// clear, origin-shift helpers) previously called a resolve-only Ensure that returned
        /// null whenever bootstrap had not yet published s_runtimeInstance. Player builds that
        /// skip or reorder EnsurePhysicsApplySystemRegistered permanently lose impulse/vortex
        /// apply and tracked-body origin-shift handling.
        /// </summary>
        /// <returns>Live physics apply instance.</returns>
        public static PhysicsApplySystem EnsureRuntimeInstance()
        {
            if (s_runtimeInstance != null)
                return s_runtimeInstance;

            if (!Application.isPlaying)
                return null;

            IPhysicsService registered = GlobalRegistry.Physics;
            PhysicsApplySystem asSystem = registered as PhysicsApplySystem;
            if (asSystem != null)
            {
                s_runtimeInstance = asSystem;
                return asSystem;
            }

            PhysicsApplySystem existing =
                UnityEngine.Object.FindFirstObjectByType<PhysicsApplySystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_runtimeInstance = existing;
                if (!existing._isInitialized)
                    existing.InitializeService();
                return existing;
            }

            // Player-build construction path: no authored/bootstrap instance reachable.
            GameObject runtimeRoot = new GameObject("[PhysicsApplySystem]"); // COLD ALLOC
            PhysicsApplySystem created = runtimeRoot.AddComponent<PhysicsApplySystem>();
            created.InitializeService();
            return created;
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
            if (_isInitialized)
                s_runtimeInstance = this;
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

        /// <inheritdoc />
        public void PrepareTrackedBodiesForOriginShift()
        {
            GlobalPhysicsStateManager.PrepareTrackedBodiesForOriginShift();
        }

        /// <inheritdoc />
        public void CommitTrackedBodiesForOriginShift(Vector3 shiftOffset)
        {
            GlobalPhysicsStateManager.CommitTrackedBodiesForOriginShift(shiftOffset);
        }

        /// <inheritdoc />
        public void FinalizeTrackedBodiesAfterOriginShift()
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
        public void ResetTrackedBodiesForSafeTeleportState()
        {
            GlobalPhysicsStateManager.ResetTrackedBodiesForSafeTeleport();
        }

        /// <inheritdoc />
        public void ArmSafeTeleportSpeculativeCcd()
        {
            GlobalPhysicsStateManager.ArmSafeTeleportSpeculativeCcdForSafeTeleport();
        }

        /// <inheritdoc />
        public bool ApplyKinematicWeldSnap(Rigidbody body, Vector3 targetPosition, Quaternion targetRotation)
        {
            return PhysicsForceRouter.ApplyKinematicWeldSnap(body, targetPosition, targetRotation);
        }

        /// <inheritdoc />
        public bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            return QueueForce(body, new QueueForceArgs(force: force, mode: mode, priority: ForcePacketPriority.Critical, wake: wake));
        }

        internal bool QueueForce(Rigidbody body, in QueueForceArgs args)
        {
            if (!TrySanitizeVector(args.Force, NonFiniteForceLog, out Vector3 sanitizedForce) ||
                VectorLengthSq(sanitizedForce) <= MinMagnitudeSq ||
                body == null)
            {
                return false;
            }

            if (TryRouteToCachedPlayerForceSink(body, sanitizedForce, args.Mode))
                return true;

            if (body.isKinematic)
                return false;

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

            ForcePacket packet = new ForcePacket
            {
                Force = sanitizedForce,
                Torque = Vector3.zero,
                PointOffset = Vector3.zero,
                Mode = args.Mode,
                Flags = (byte)(ForcePacketFlags.HasForce | args.ExtraFlags | (args.Wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = args.Priority,
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
                body == null)
            {
                return false;
            }

            float3 worldPosition3 = new float3(worldPosition.x, worldPosition.y, worldPosition.z);
            if (!math.all(math.isfinite(worldPosition3)))
            {
                ReportNonFinitePacket(NonFinitePointOffsetLog);
                return false;
            }

            if (TryRouteToCachedPlayerForceSinkAtPosition(body, sanitizedForce, worldPosition, mode))
                return true;

            if (body.isKinematic)
                return false;

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
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
                body == null)
            {
                return false;
            }

            if (TrySuppressCachedPlayerAngularVelocitySet(body))
                return true;

            if (body.isKinematic)
                return false;

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
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

        internal bool QueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, ForcePacketPriority priority, bool wake = true)
        {
            if (!TrySanitizeVector(linearVelocity, NonFiniteForceLog, out Vector3 sanitizedVelocity) ||
                body == null)
            {
                return false;
            }

            if (TryRouteToCachedPlayerLinearVelocitySet(body, sanitizedVelocity))
                return true;

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

            ForcePacket packet = new ForcePacket
            {
                Force = sanitizedVelocity,
                Torque = Vector3.zero,
                PointOffset = Vector3.zero,
                Mode = ForceMode.VelocityChange,
                Flags = (byte)(ForcePacketFlags.SetLinearVelocity | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Linear velocity packet queue saturated.");
        }

        public bool QueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true)
        {
            return QueueLinearVelocitySet(body, linearVelocity, ForcePacketPriority.Critical, wake);
        }

        internal bool QueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, ForcePacketPriority priority, bool wake = true)
        {
            if (!TrySanitizeVector(angularVelocity, NonFiniteTorqueLog, out Vector3 sanitizedVelocity) ||
                body == null)
            {
                return false;
            }

            if (TrySuppressCachedPlayerAngularVelocitySet(body))
                return true;

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

            ForcePacket packet = new ForcePacket
            {
                Force = Vector3.zero,
                Torque = sanitizedVelocity,
                PointOffset = Vector3.zero,
                Mode = ForceMode.VelocityChange,
                Flags = (byte)(ForcePacketFlags.SetAngularVelocity | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Angular velocity packet queue saturated.");
        }

        public bool QueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, bool wake = true)
        {
            return QueueAngularVelocitySet(body, angularVelocity, ForcePacketPriority.Critical, wake);
        }

        internal bool QueuePoseSet(Rigidbody body, Vector3 position, Quaternion rotation, ForcePacketPriority priority, bool wake = true)
        {
            if (!TrySanitizeVector(position, NonFinitePointOffsetLog, out Vector3 sanitizedPosition) ||
                !TryNormalizeQuaternion(rotation, out Quaternion sanitizedRotation) ||
                body == null)
            {
                return false;
            }

            GlobalPhysicsStateManager.RegisterTrackedBodyIfMissing(body);
            int rigidbodyIndex = AcquireBodySlotIndex(body);
            if (rigidbodyIndex < 0)
                return false;
            CacheLastFiniteBodyAup(rigidbodyIndex, body.position);

            ForcePacket packet = new ForcePacket
            {
                Force = sanitizedPosition,
                Torque = new Vector3(sanitizedRotation.x, sanitizedRotation.y, sanitizedRotation.z),
                PointOffset = new Vector3(sanitizedRotation.w, 0f, 0f),
                Mode = ForceMode.VelocityChange,
                Flags = (byte)(ForcePacketFlags.SetPose | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                Priority = priority,
                RigidbodyIndex = rigidbodyIndex
            };
            return TryEnqueueBackPacket(in packet, "[PhysicsApplySystem] Pose packet queue saturated.");
        }

        public bool QueuePoseSet(Rigidbody body, Vector3 position, Quaternion rotation, bool wake = true)
        {
            return QueuePoseSet(body, position, rotation, ForcePacketPriority.Critical, wake);
        }

        /// <inheritdoc />
        public bool QueueAmbientForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce))
                return false;

            Vector3 safeForce = PhysicsForceRouter.ClampUpwardAcceleration(acceptedForce, mode);
            ForcePacketFlags extraFlags = PhysicsForceRouter.ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToCachedPlayerForceSink(body, routeForce, mode))
                return true;

            return QueueForce(body, new QueueForceArgs(force: safeForce, mode: mode, priority: ForcePacketPriority.Ambient, wake: wake, extraFlags: extraFlags));
        }

        /// <inheritdoc />
        public bool QueueAmbientForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(force, out Vector3 acceptedForce) ||
                !MathGuard.TryAcceptFinite(worldPosition, out Vector3 acceptedWorldPosition))
            {
                return false;
            }

            Vector3 safeForce = PhysicsForceRouter.ClampUpwardAcceleration(acceptedForce, mode);
            ForcePacketFlags extraFlags = PhysicsForceRouter.ResolveBiomeBuoyancyFlags(safeForce, mode);
            Vector3 routeForce = ApplyActiveBiomeBuoyancyGravityMultiplier(safeForce, mode, extraFlags);
            if (TryRouteToCachedPlayerForceSinkAtPosition(body, routeForce, acceptedWorldPosition, mode))
                return true;

            return QueueForceAtPosition(body, safeForce, acceptedWorldPosition, mode, ForcePacketPriority.Ambient, wake, extraFlags);
        }

        /// <inheritdoc />
        public bool QueueAmbientTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(torque, out Vector3 acceptedTorque))
                return false;

            return QueueTorque(body, acceptedTorque, mode, ForcePacketPriority.Ambient, wake);
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
                ? SanitizeFiniteVector(anchorBody.GetPointVelocity(targetPosition))
                : Vector3.zero;
            Vector3 currentVelocity = SanitizeFiniteVector(payloadBody.GetPointVelocity(currentPosition));
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

            bool payloadQueued = QueueForceAtPosition(
                payloadBody,
                payloadVelocityChange,
                currentPosition,
                ForceMode.VelocityChange,
                ForcePacketPriority.Critical,
                wake);
            if (applyReactionForce && anchorBody != null && !anchorBody.isKinematic)
            {
                float anchorVelocityScale = payloadMass / math.max(0.0001f, anchorMass);
                Vector3 anchorVelocityChange = -payloadVelocityChange * anchorVelocityScale;
                if (IsFiniteNonZero(anchorVelocityChange))
                {
                    QueueForceAtPosition(
                        anchorBody,
                        anchorVelocityChange,
                        targetPosition,
                        ForceMode.VelocityChange,
                        ForcePacketPriority.Critical,
                        wake);
                }
            }

            return payloadQueued;
        }

        /// <inheritdoc />
        public int PendingLateFrameEventCount => PhysicsEventBus.PendingCount + FluidFeedbackEvents.PendingCount;

        /// <inheritdoc />
        public void FlushLateFrameEvents()
        {
            PhysicsEventBus.FlushPending();
            FluidFeedbackEvents.FlushPending();
        }

        /// <inheritdoc />
        public void RegisterElectromagneticPulseListener(IElectromagneticPulseEventListener listener)
        {
            PhysicsEventBus.Register(listener);
        }

        /// <inheritdoc />
        public void UnregisterElectromagneticPulseListener(IElectromagneticPulseEventListener listener)
        {
            PhysicsEventBus.Unregister(listener);
        }

        private bool TryEnqueueBackPacket(in ForcePacket packet, string saturationMessage)
        {
            if (!TryAcquireForcePacketBufferWriteLock(
                    in _backPacketBufferHandle,
                    BufferID.PhysicsForceCommandBack,
                    out NativeArray<ForcePacket> backPackets,
                    out IDataVault backPacketsVault))
            {
                ReportForcePacketSaturationWarningIfNeeded(saturationMessage);
                return false;
            }

            try
            {
                if (TryReplaceQueuedPosePacket(backPackets, _backCount, in packet))
                    return true;

                if (_backCount >= MaxQueuedPackets)
                {
                    ReportForcePacketSaturationWarningIfNeeded(saturationMessage);
                    return false;
                }

                backPackets[_backCount++] = packet;
                return true;
            }
            finally
            {
                ReleaseForcePacketBufferWriteLock(backPacketsVault, in _backPacketBufferHandle);
            }
        }

        private static bool TryReplaceQueuedPosePacket(NativeArray<ForcePacket> backPackets, int count, in ForcePacket packet)
        {
            ForcePacketFlags packetFlags = (ForcePacketFlags)packet.Flags;
            if ((packetFlags & ForcePacketFlags.SetPose) == 0 ||
                packet.RigidbodyIndex < 0 ||
                !backPackets.IsCreated)
            {
                return false;
            }

            int safeCount = math.min(count, backPackets.Length);
            for (int i = safeCount - 1; i >= 0; i--)
            {
                ForcePacket existing = backPackets[i];
                ForcePacketFlags existingFlags = (ForcePacketFlags)existing.Flags;
                if (existing.RigidbodyIndex != packet.RigidbodyIndex ||
                    (existingFlags & ForcePacketFlags.SetPose) == 0)
                {
                    continue;
                }

                ForcePacket replacement = packet;
                replacement.Flags = (byte)(packetFlags | (existingFlags & ForcePacketFlags.WakeBody));
                if ((byte)existing.Priority > (byte)replacement.Priority)
                    replacement.Priority = existing.Priority;
                backPackets[i] = replacement;
                return true;
            }

            return false;
        }

        private void ReportForcePacketSaturationWarningIfNeeded(string saturationMessage)
        {
            int frame = CurrentDispatcherFrameIndex();
            if (frame < _nextForcePacketSaturationWarningFrame)
                return;

            _nextForcePacketSaturationWarningFrame = frame + ForcePacketWarningCooldownFrames;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(saturationMessage);
#endif
        }

        public bool QueueDepressurizationVortex(
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
        public bool QueueImplosionImpulse(
            Vector3 roomCenter,
            float radiusMeters,
            float baseImpulseNewtonSeconds,
            float maximumImpulseNewtonSeconds)
        {
            return ApplyImplosionImpulse(
                roomCenter,
                radiusMeters,
                baseImpulseNewtonSeconds,
                maximumImpulseNewtonSeconds);
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

        /// <inheritdoc />
        public void ClearSceneTransitionRuntimeState()
        {
            ClearQueuedPackets();
            GlobalPhysicsStateManager.ClearRuntimeStateStatic();
        }

        private void Awake()
        {
            PhysicsApplySystem registeredSystem = s_runtimeInstance;
            if (registeredSystem != null && registeredSystem != this)
            {
                Destroy(gameObject);
                return;
            }

            s_runtimeInstance = this;
            EnsureRuntimeResources();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureRuntimeResources();

            TryRegisterRuntimeLanes();
            TryRegisterHotSwapListener();

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
            CacheColdRuntimeDependencies();
            EnsureForcePacketBuffers();
            EnsureValidationBuffers();
            EnsureSubmarineImpactSignalLane();
        }

        private void CacheColdRuntimeDependencies()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_physicsCullingOverseer == null)
                _physicsCullingOverseer = GlobalRegistry.PhysicsCullingOverseer;
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;
            IPlayerMovementContracts playerMovementContracts = GlobalRegistry.PlayerMovementContracts;
            if (_playerMovementForceSink == null)
                _playerMovementForceSink = playerMovementContracts;
            if (_playerMovementPoseReadModel == null)
                _playerMovementPoseReadModel = playerMovementContracts;
            if (_submarineHullBody == null || _submarineFluidDynamics == null || _submarineStructuralGrid == null)
            {
                var submarineContext = GlobalRegistry.Submarine;
                if (submarineContext != null)
                {
                    if (_submarineHullBody == null)
                        _submarineHullBody = submarineContext.HullRigidbody;
                    if (_submarineFluidDynamics == null)
                        _submarineFluidDynamics = submarineContext.FluidDynamics;
                    if (_submarineStructuralGrid == null)
                        _submarineStructuralGrid = submarineContext.StructuralGrid;
                }
            }
        }

        private void EnsureSubmarineImpactSignalLane()
        {
            if (!Application.isPlaying)
                return;

            if (_submarineImpactSignalLaneInitialized)
                return;

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
            ShutdownBuoyancyForceQueue();
            ReleaseValidationBufferViews();
            ReleaseForcePacketBufferViews();
            ClearTransientImpactProxyLights();

            _submarineImpactSnapshotReadFrame = -1;
            _submarineImpactSnapshotReadCursor = 0;
            _submarineImpactSignalLaneInitialized = false;
            _dataVault = null;
            _physicsCullingOverseer = null;
            _playerRuntimeContext = null;
            _playerMovementForceSink = null;
            _playerMovementPoseReadModel = null;
            _submarineHullBody = null;
            _submarineFluidDynamics = null;
            _submarineStructuralGrid = null;
            if (ReferenceEquals(s_runtimeInstance, this))
                s_runtimeInstance = null;

            PhysicsEventBus.Shutdown();
        }

        private void UnregisterRuntimeHooks()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();

            if (_contactModifySubscribed)
            {
                UnityEngine.Physics.ContactModifyEvent -= HandleContactModifyEvent;
                UnityEngine.Physics.ContactModifyEventCCD -= HandleContactModifyEvent;
                _contactModifySubscribed = false;
            }
        }

        private void TryRegisterRuntimeLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_fixedTickRegistered)
            {
                // Flush the previous validated packet snapshot before producers write this fixed step.
                _fixedTickRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.UI);
            }

            if (!_postFixedTickRegistered)
            {
                // Swap vault packet buffers only after all fixed-step producers have written to the back buffer.
                _postFixedTickRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.UI);
            }

            if (!_lateFrameTickRegistered)
                _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterRuntimeLanes()
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
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterRuntimeLanes();
                    if (currentService == null)
                        return;

                    TryRegisterRuntimeLanes();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    ReleaseValidationBufferViews();
                    ReleaseForcePacketBufferViews();
                    _dataVault = currentService as IDataVault;
                    if (_dataVault != null)
                    {
                        EnsureForcePacketBuffers();
                        EnsureValidationBuffers();
                    }
                    break;
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    _physicsCullingOverseer = currentService as IPhysicsCullingOverseer;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    IPlayerMovementContracts playerMovementContracts = currentService as IPlayerMovementContracts;
                    _playerMovementForceSink = playerMovementContracts;
                    _playerMovementPoseReadModel = playerMovementContracts;
                    break;
                case GlobalRegistryServiceSlot.Submarine:
                    ISubmarineRuntimeContext submarineContext = currentService as ISubmarineRuntimeContext;
                    _submarineHullBody = submarineContext?.HullRigidbody;
                    _submarineFluidDynamics = submarineContext?.FluidDynamics;
                    _submarineStructuralGrid = submarineContext?.StructuralGrid;
                    break;
            }
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

            if (!TrySwapForcePacketBuffers())
                return;

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

            if (!TryAcquireByteBufferWriteLock(
                    in _validationMaskBufferHandle,
                    BufferID.PhysicsForceValidationMask,
                    out NativeArray<byte> validationMask,
                    out IDataVault validationMaskVault))
            {
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            int queuedCount = _frontCount;
            int sourceCount = math.min(queuedCount, MaxQueuedPackets);
            if (sourceCount < 0)
                sourceCount = 0;
            int applyCount = math.min(sourceCount, MaxForcePacketsAppliedPerFixedTick);
            bool clipped = queuedCount > applyCount;

            try
            {
                for (int i = 0; i < applyCount; i++)
                    _forcePacketApplyValidityScratch[i] = validationMask[i];
            }
            finally
            {
                ReleaseByteBufferWriteLock(validationMaskVault, in _validationMaskBufferHandle);
            }

            if (!TryAcquireForcePacketBufferWriteLock(
                    in _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    out NativeArray<ForcePacket> frontPackets,
                    out IDataVault frontPacketsVault))
            {
                ClearForcePacketApplyScratch(applyCount);
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            try
            {
                for (int i = 0; i < applyCount; i++)
                    _forcePacketApplyScratch[i] = frontPackets[i];

                ClearForcePacketRange(frontPackets, sourceCount);
                _frontCount = 0;
                _frontBufferValidationReady = false;
            }
            finally
            {
                ReleaseForcePacketBufferWriteLock(frontPacketsVault, in _frontPacketBufferHandle);
            }

            if (TryAcquireByteBufferWriteLock(
                    in _validationMaskBufferHandle,
                    BufferID.PhysicsForceValidationMask,
                    out validationMask,
                    out validationMaskVault))
            {
                try
                {
                    ClearByteRange(validationMask, sourceCount);
                }
                finally
                {
                    ReleaseByteBufferWriteLock(validationMaskVault, in _validationMaskBufferHandle);
                }
            }

            if (clipped)
                PublishForcePacketClipWarningIfNeeded(queuedCount);

            long startTimestamp = Stopwatch.GetTimestamp();
            try
            {
                using (_flushFrontBufferProfilerMarker.Auto())
                {
                    for (int i = 0; i < applyCount; i++)
                    {
                        byte valid = _forcePacketApplyValidityScratch[i];
                        ForcePacket packet = _forcePacketApplyScratch[i];
                        _forcePacketApplyValidityScratch[i] = 0;
                        _forcePacketApplyScratch[i] = default;

                        if (valid == 0)
                        {
                            ReportNonFinitePacket(InvalidForcePacketLog);
                            continue;
                        }

                        Rigidbody body = ResolveBody(packet.RigidbodyIndex);
                        if (body == null)
                            continue;

                        if (!EnsureFiniteBodyState(body, packet.RigidbodyIndex))
                            continue;

                        ForcePacketFlags flags = (ForcePacketFlags)packet.Flags;
                        bool setsLinearVelocity = (flags & ForcePacketFlags.SetLinearVelocity) != 0;
                        bool setsAngularVelocity = (flags & ForcePacketFlags.SetAngularVelocity) != 0;
                        bool setsPose = (flags & ForcePacketFlags.SetPose) != 0;

                        if (ShouldDiscardAmbientPacket(body, in packet, flags, fixedDeltaTime))
                            continue;

                        if ((flags & ForcePacketFlags.WakeBody) != 0 && body.IsSleeping())
                            body.WakeUp();

                        Vector3 appliedForce = Vector3.zero;
                        Vector3 appliedTorque = Vector3.zero;
                        Vector3 impulsePosition = body.position;
                        Vector3 preApplyVelocity = SanitizeFiniteVector(body.linearVelocity);
                        bool appliedForceOrTorque = false;

                        if (setsPose)
                        {
                            Vector3 targetPosition = TrySanitizeVector(packet.Force, NonFinitePointOffsetLog, out Vector3 sanitizedPosition)
                                ? sanitizedPosition
                                : body.position;
                            Quaternion packedRotation = new Quaternion(packet.Torque.x, packet.Torque.y, packet.Torque.z, packet.PointOffset.x);
                            Quaternion targetRotation = TryNormalizeQuaternion(packedRotation, out Quaternion sanitizedRotation)
                                ? sanitizedRotation
                                : body.rotation;

                            body.MovePosition(targetPosition);
                            body.MoveRotation(targetRotation);
                            CacheLastFiniteBodyAup(packet.RigidbodyIndex, targetPosition);
                        }

                        if (setsLinearVelocity)
                        {
                            if (!TrySanitizeVector(packet.Force, NonFiniteForceLog, out Vector3 sanitizedLinearVelocity))
                                sanitizedLinearVelocity = Vector3.zero;

                            body.linearVelocity = sanitizedLinearVelocity;
                        }

                        if (setsAngularVelocity)
                        {
                            if (!TrySanitizeVector(packet.Torque, NonFiniteTorqueLog, out Vector3 sanitizedAngularVelocity))
                                sanitizedAngularVelocity = Vector3.zero;

                            body.angularVelocity = sanitizedAngularVelocity;
                        }

                        if (body.isKinematic)
                            continue;

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
                                    appliedForceOrTorque = true;
                                }
                            }
                            else if (VectorLengthSq(sanitizedForce) > MinMagnitudeSq)
                            {
                                body.AddForce(sanitizedForce, packet.Mode);
                                appliedForce = sanitizedForce;
                                appliedForceOrTorque = true;
                            }
                        }

                        if ((flags & ForcePacketFlags.HasTorque) != 0)
                        {
                            if (TrySanitizeVector(packet.Torque, NonFiniteTorqueLog, out Vector3 sanitizedTorque) &&
                                VectorLengthSq(sanitizedTorque) > MinMagnitudeSq)
                            {
                                body.AddTorque(sanitizedTorque, packet.Mode);
                                appliedTorque = sanitizedTorque;
                                appliedForceOrTorque = true;
                            }
                        }

                        if (packet.Priority == ForcePacketPriority.Critical && appliedForceOrTorque)
                            EmitCriticalAcousticImpulse(
                                body,
                                appliedForce,
                                appliedTorque,
                                impulsePosition,
                                packet.Mode,
                                fixedDeltaTime,
                                preApplyVelocity);
                    }
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
            int frame = CurrentDispatcherFrameIndex();
            if (elapsedMilliseconds <= FlushBudgetWarningMilliseconds || frame < _nextFlushBudgetWarningFrame)
                return;

            _nextFlushBudgetWarningFrame = frame + FlushBudgetWarningCooldownFrames;
            GlobalTelemetryBus.PublishPerformanceWarning(
                PhysicsFlushBudgetWarningHash,
                PhysicsFlushContextHash,
                (float)elapsedMilliseconds);
        }

        private void PublishForcePacketClipWarningIfNeeded(int packetCount)
        {
            int frame = CurrentDispatcherFrameIndex();
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
            PhysicsEventBus.TryNotifyAcousticImpulse(in impulseEvent);
            TryRegisterMechanicalSparkProxyLight(impulsePosition, sourceBodyEntityId, volume01);
        }

        private static Vector3 ResolvePacketVelocityDelta(Vector3 force, ForceMode mode, float mass, float fixedDeltaTime)
        {
            Vector3 safeForce = SanitizeFiniteVector(force);
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

            float now = CurrentDispatcherFrameSeconds();
            int selectedIndex = -1;
            for (int i = 0; i < _impactProxyLights.Length; i++)
            {
                if (_impactProxyLights[i].ExpireAtDispatcherSeconds <= now)
                {
                    selectedIndex = i;
                    break;
                }
            }

            if (selectedIndex < 0)
                return;

            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition lightAup))
                return;

            int key = unchecked((sourceBodyInstanceId * 397) ^ SystemDispatcher.CurrentFrameIndex ^ 0x5EC7A11);
            ProxyLightData light = ProxyLightData.CreateTransientPoint(
                lightAup,
                new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                new Color(1f, 0.68f, 0.32f, 1f),
                math.lerp(2.2f, 5.8f, intensity01),
                math.lerp(0.8f, 2.6f, intensity01),
                now);
            ProxyLightRegistry.RegisterOrUpdate(key, in light);

            _impactProxyLights[selectedIndex] = new TransientProxyLightHandle
            {
                Key = key,
                ExpireAtDispatcherSeconds = now + MechanicalSparkProxyLightDurationSeconds
            };
        }

        private void ExpireTransientImpactProxyLights()
        {
            float now = CurrentDispatcherFrameSeconds();
            for (int i = 0; i < _impactProxyLights.Length; i++)
            {
                TransientProxyLightHandle handle = _impactProxyLights[i];
                if (handle.Key == 0 || handle.ExpireAtDispatcherSeconds > now)
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

        private bool ShouldDiscardAmbientPacket(Rigidbody body, in ForcePacket packet, ForcePacketFlags flags, float fixedDeltaTime)
        {
            if (packet.Priority != ForcePacketPriority.Ambient ||
                body == null)
            {
                return false;
            }

            IPhysicsCullingOverseer physicsCulling = _physicsCullingOverseer;
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
            BiomeMatrixDirector biomeMatrixDirector = null;
            WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
            HectonBiomeMatrixProfile profile = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;

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

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Rigidbody playerBody = playerContext != null ? playerContext.PlayerRigidbody : null;
            IPlayerMovementForceSink forceSink = _playerMovementForceSink;

            if (TryReadPlayerRuntimePosition(playerContext, _playerMovementPoseReadModel, out Vector3 playerPosition))
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
                        if (forceSink != null)
                            forceSink.QueueExternalAcceleration(acceleration);
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
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            IPlayerMovementForceSink forceSink = _playerMovementForceSink;
            if (playerContext != null)
                playerBody = playerContext.PlayerRigidbody;

            if (TryReadPlayerRuntimePosition(playerContext, _playerMovementPoseReadModel, out Vector3 playerPosition))
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
                        if (forceSink != null)
                        {
                            forceSink.QueueExternalVelocityChange(impulse / HydrodynamicPlayerEquivalentMassKg);
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
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                roomCenter,
                radiusMeters,
                kindMask,
                _implosionContacts);
            int uniqueBodyCount = 0;

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _implosionContacts[hitIndex];
                _implosionContacts[hitIndex] = default;

                Rigidbody body = hit.Rigidbody;
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

        private bool TrySwapForcePacketBuffers()
        {
            if (!TryGetExistingVaultBuffer(
                    ref _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> _) ||
                !TryGetExistingVaultBuffer(
                    ref _backPacketBufferHandle,
                    BufferID.PhysicsForceCommandBack,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> _))
            {
                _frontCount = 0;
                _backCount = 0;
                _frontBufferValidationReady = false;
                return false;
            }

            VaultGenerationHandle<ForcePacket> swapHandle = _frontPacketBufferHandle;
            _frontPacketBufferHandle = _backPacketBufferHandle;
            _backPacketBufferHandle = swapHandle;

            _frontCount = _backCount;
            _backCount = 0;
            return true;
        }

        private void ClampFrontBufferCountToCapacity()
        {
            if (_frontCount <= MaxQueuedPackets)
                return;

            PublishForcePacketClipWarningIfNeeded(_frontCount);
            if (TryAcquireForcePacketBufferWriteLock(
                    in _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    out NativeArray<ForcePacket> frontPackets,
                    out IDataVault frontPacketsVault))
            {
                try
                {
                    ClearForcePacketRange(frontPackets, _frontCount, MaxQueuedPackets);
                }
                finally
                {
                    ReleaseForcePacketBufferWriteLock(frontPacketsVault, in _frontPacketBufferHandle);
                }
            }

            _frontCount = MaxQueuedPackets;
        }

        private void EnsureForcePacketBuffers()
        {
            if (!Application.isPlaying)
                return;

            EnsureVaultBufferView(
                ref _frontPacketBufferHandle,
                BufferID.PhysicsForceCommandFront,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
            EnsureVaultBufferView(
                ref _backPacketBufferHandle,
                BufferID.PhysicsForceCommandBack,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
        }

        private bool EnsureVaultBufferView<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryGetExistingVaultBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            IDataVault dataVault = _dataVault;
            if (dataVault == null || requiredLength <= 0)
                return false;

            if (dataVault.IsAllocationLocked)
            {
                if (!dataVault.TryGetGenerationHandle(bufferId, out handle))
                    return false;

                return TryGetExistingVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
            }

            handle = dataVault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return TryGetExistingVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryGetExistingVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                requiredLength <= 0 ||
                !IsPhysicsVaultHandle(in handle, bufferId) ||
                !dataVault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsPhysicsVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void ClearForcePacketBuffer(ref VaultGenerationHandle<ForcePacket> handle, BufferID bufferId)
        {
            if (!TryAcquireForcePacketBufferWriteLock(in handle, bufferId, out NativeArray<ForcePacket> buffer, out IDataVault writeVault))
                return;

            try
            {
                ClearForcePacketRange(buffer, MaxQueuedPackets);
            }
            finally
            {
                ReleaseForcePacketBufferWriteLock(writeVault, in handle);
            }
        }

        private void ClearByteBuffer(ref VaultGenerationHandle<byte> handle, BufferID bufferId)
        {
            if (!TryAcquireByteBufferWriteLock(in handle, bufferId, out NativeArray<byte> buffer, out IDataVault writeVault))
                return;

            try
            {
                ClearByteRange(buffer, MaxQueuedPackets);
            }
            finally
            {
                ReleaseByteBufferWriteLock(writeVault, in handle);
            }
        }

        private bool TryAcquireForcePacketBufferWriteLock(
            in VaultGenerationHandle<ForcePacket> handle,
            BufferID bufferId,
            out NativeArray<ForcePacket> buffer,
            out IDataVault writeVault)
        {
            buffer = default;
            writeVault = null;
            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                !IsPhysicsVaultHandle(in handle, bufferId) ||
                !dataVault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= MaxQueuedPackets)
                {
                    writeVault = dataVault;
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    dataVault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private bool TryAcquireByteBufferWriteLock(
            in VaultGenerationHandle<byte> handle,
            BufferID bufferId,
            out NativeArray<byte> buffer,
            out IDataVault writeVault)
        {
            buffer = default;
            writeVault = null;
            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                !IsPhysicsVaultHandle(in handle, bufferId) ||
                !dataVault.TryAcquireWriteLock(in handle, OwnerSystemId, out buffer))
            {
                return false;
            }

            bool ownershipTransferred = false;
            try
            {
                if (buffer.IsCreated && buffer.Length >= MaxQueuedPackets)
                {
                    writeVault = dataVault;
                    ownershipTransferred = true;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (!ownershipTransferred)
                    dataVault.ReleaseWriteLock(in handle, OwnerSystemId);
            }
        }

        private static void ReleaseForcePacketBufferWriteLock(IDataVault dataVault, in VaultGenerationHandle<ForcePacket> handle)
        {
            dataVault?.ReleaseWriteLock(in handle, OwnerSystemId);
        }

        private static void ReleaseByteBufferWriteLock(IDataVault dataVault, in VaultGenerationHandle<byte> handle)
        {
            dataVault?.ReleaseWriteLock(in handle, OwnerSystemId);
        }

        private bool TryPinValidationScheduleBuffers(
            out NativeArray<ForcePacket> validationPackets,
            out NativeArray<byte> validationMask)
        {
            validationPackets = default;
            validationMask = default;
            if (_validationSchedulePinMask != 0u)
                return false;

            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                dataVault.IsCompactionFenceActive)
                return false;

            bool success = false;
            _validationSchedulePinVault = dataVault;
            try
            {
                if (!TryLockValidationScheduleBuffer(dataVault, BufferID.PhysicsForceValidationPackets, ValidationSchedulePinPackets) ||
                    !TryLockValidationScheduleBuffer(dataVault, BufferID.PhysicsForceValidationMask, ValidationSchedulePinMask))
                    return false;

                if (!TryResolveValidationScheduleBuffer(
                        in _validationPacketBufferHandle,
                        BufferID.PhysicsForceValidationPackets,
                        MaxQueuedPackets,
                        out validationPackets))
                    return false;

                if (!TryResolveValidationScheduleBuffer(
                        in _validationMaskBufferHandle,
                        BufferID.PhysicsForceValidationMask,
                        MaxQueuedPackets,
                        out validationMask))
                    return false;

                success = true;
                return true;
            }
            finally
            {
                if (!success)
                    ReleaseValidationScheduleBufferPins();
            }
        }

        private bool TryResolveValidationScheduleBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null ||
                dataVault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsPhysicsVaultHandle(in handle, bufferId))
            {
                return false;
            }

            if (!dataVault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private void ReleaseValidationScheduleBufferPins()
        {
            IDataVault dataVault = _validationSchedulePinVault;
            uint pinMask = _validationSchedulePinMask;
            _validationSchedulePinVault = null;
            _validationSchedulePinMask = 0u;
            if (dataVault == null || pinMask == 0u)
                return;

            TryUnlockValidationScheduleBuffer(dataVault, pinMask, ValidationSchedulePinMask, BufferID.PhysicsForceValidationMask);
            TryUnlockValidationScheduleBuffer(dataVault, pinMask, ValidationSchedulePinPackets, BufferID.PhysicsForceValidationPackets);
        }

        private bool TryLockValidationScheduleBuffer(IDataVault dataVault, BufferID bufferId, uint pinBit)
        {
            if ((_validationSchedulePinMask & pinBit) != 0u)
                return true;

            if (dataVault == null || !dataVault.TryLockBuffer(bufferId, OwnerSystemId))
                return false;

            _validationSchedulePinMask |= pinBit;
            return true;
        }

        private static void TryUnlockValidationScheduleBuffer(IDataVault dataVault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                dataVault.TryUnlockBuffer(bufferId, OwnerSystemId);
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

        private void ClearForcePacketApplyScratch(int count)
        {
            int clampedCount = math.clamp(count, 0, MaxForcePacketsAppliedPerFixedTick);
            for (int i = 0; i < clampedCount; i++)
            {
                _forcePacketApplyScratch[i] = default;
                _forcePacketApplyValidityScratch[i] = 0;
            }
        }

        private void EnsureValidationBuffers()
        {
            if (!Application.isPlaying)
                return;

            EnsureVaultBufferView(
                ref _validationPacketBufferHandle,
                BufferID.PhysicsForceValidationPackets,
                MaxQueuedPackets,
                out NativeArray<ForcePacket> _);
            EnsureVaultBufferView(
                ref _validationMaskBufferHandle,
                BufferID.PhysicsForceValidationMask,
                MaxQueuedPackets,
                out NativeArray<byte> _);
        }

        private void EnsureSubmarineModifiableContacts()
        {
            Rigidbody hullBody = _submarineHullBody;
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
            if (pairs.Length <= 0)
                return;

            Rigidbody hullBody = _submarineHullBody;
            SubmarineStructuralGrid structuralGrid = _submarineStructuralGrid;
            if (hullBody == null || structuralGrid == null)
                return;

            ulong hullEntityId = EntityId.ToULong(hullBody.GetEntityId());
            if (hullEntityId == 0ul)
                return;

            float hullMass = math.max(hullBody.mass, 0.0001f);
            SubmarineFluidDynamics fluidDynamics = _submarineFluidDynamics;
            float depthMeters = fluidDynamics != null
                ? math.max(0f, fluidDynamics.ExternalDepthMeters)
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

                float3 hullVelocity = submarineIsBody ? (float3)(pair.bodyVelocity) : (float3)(pair.otherBodyVelocity);
                float3 otherVelocity = submarineIsBody ? (float3)(pair.otherBodyVelocity) : (float3)(pair.bodyVelocity);
                HectonContactJob.InelasticImpactResult impact = HectonContactJob.ResolveInelasticImpact(
                    hullMass,
                    hullVelocity,
                    otherVelocity,
                    normal,
                    contactCount,
                    HullYieldThresholdJoules);
                if (impact.ExceedsYield == 0)
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
                float3 localPoint = (float3)(structuralGridWorldToLocal.MultiplyPoint3x4(point));
                float3 localNormal = (float3)(ResolveDominantAxisDirection(
                    structuralGridWorldToLocal.MultiplyVector(impactNormalWorld)));
                structuralGrid.QueueImpactLocal(localPoint, impact.RelativeSpeedMetersPerSecond, impact.IntegrityDelta);
                structuralGrid.QueueHullImpactFeedbackLocal(localPoint, localNormal, impact.RelativeSpeedMetersPerSecond, impact.Severity01);
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
            SignalBus<DeferredSubmarineImpactSignal>.TryPushTracked(in signal, ref s_x001PhysicsApplySystemSignalPushDropCount);
        }

        private void FlushDeferredSubmarineImpactSignals()
        {
            EnsureSubmarineImpactSignalLane();
            int currentFrame = CurrentDispatcherFrameIndex();
            if (_submarineImpactSnapshotReadFrame != currentFrame)
            {
                _submarineImpactSnapshotReadFrame = currentFrame;
                _submarineImpactSnapshotReadCursor = 0;
            }

            ReadOnlySpan<DeferredSubmarineImpactSignal> snapshot = SignalBus<DeferredSubmarineImpactSignal>.GetFrameSnapshot();
            if (_submarineImpactSnapshotReadCursor >= snapshot.Length)
                return;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
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
                traumaDispatcher.OnTraumaThresholdCrossed(ResolveSubmarineTraumaLevel(queuedSignal.TraumaLevel));
            }
        }

        private static void RequeueRemainingSubmarineImpactSignals(ReadOnlySpan<DeferredSubmarineImpactSignal> snapshot, int startIndex)
        {
            for (int i = startIndex; i < snapshot.Length; i++)
            {
                DeferredSubmarineImpactSignal signal = snapshot[i];
                SignalBus<DeferredSubmarineImpactSignal>.TryPushTracked(in signal, ref s_x001PhysicsApplySystemSignalPushDropCount);
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

        private static TraumaLevel ResolveSubmarineTraumaLevel(byte levelCode)
        {
            switch (levelCode)
            {
                case (byte)TraumaLevel.Minor:
                    return TraumaLevel.Minor;
                case (byte)TraumaLevel.Significant:
                    return TraumaLevel.Significant;
                case (byte)TraumaLevel.Critical:
                    return TraumaLevel.Critical;
                case (byte)TraumaLevel.Catastrophic:
                    return TraumaLevel.Catastrophic;
                default:
                    return TraumaLevel.None;
            }
        }

        private void ScheduleFrontPacketValidation()
        {
            int queuedCount = _frontCount;
            if (queuedCount <= 0)
            {
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            int validationCount = math.min(queuedCount, MaxQueuedPackets);
            bool clipped = queuedCount > validationCount;
            if (!TryPinValidationScheduleBuffers(
                    out NativeArray<ForcePacket> validationPackets,
                    out NativeArray<byte> validationMask))
            {
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            if (!TryGetExistingVaultBuffer(
                    ref _frontPacketBufferHandle,
                    BufferID.PhysicsForceCommandFront,
                    MaxQueuedPackets,
                    out NativeArray<ForcePacket> frontPackets))
            {
                ReleaseValidationScheduleBufferPins();
                _frontBufferValidationReady = false;
                _frontCount = 0;
                return;
            }

            bool scheduled = false;
            try
            {
                for (int i = 0; i < validationCount; i++)
                {
                    validationPackets[i] = frontPackets[i];
                    validationMask[i] = 0;
                }

                _frontCount = validationCount;
                ValidateForcePacketsJob validateJob = new ValidateForcePacketsJob
                {
                    Packets = validationPackets,
                    ValidityMask = validationMask,
                    MaxTrackedBodies = _bodySlots.Length
                };

                _packetValidationHandle = validateJob.Schedule(validationCount, 32);
                _packetValidationScheduled = true;
                scheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystemId, _packetValidationHandle);
                _frontBufferValidationReady = false;
            }
            finally
            {
                if (!scheduled)
                    ReleaseValidationScheduleBufferPins();
            }

            if (clipped)
                PublishForcePacketClipWarningIfNeeded(queuedCount);
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
                ReleaseValidationScheduleBufferPins();
                _frontBufferValidationReady = _frontCount > 0;
                H8Memory.RegisterActiveJob(OwnerSystemId, default);
            }
        }

        private int AcquireBodySlotIndex(Rigidbody body)
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
            Hecton8.Core.H8Debug.LogWarning("[PhysicsApplySystem] Rigidbody slot capacity exceeded.");
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
        private static Vector3 SanitizeFiniteVector(Vector3 value, Vector3 fallback = default)
        {
            if (IsFiniteVector(value))
                return value;

            return IsFiniteVector(fallback) ? fallback : Vector3.zero;
        }

        private static int CurrentDispatcherFrameIndex()
        {
            uint frame = SystemDispatcher.CurrentFrameId;
            return unchecked((int)(frame != 0u ? frame : 1u));
        }

        private static float CurrentDispatcherFrameSeconds()
        {
            return math.max(0f, CurrentDispatcherFrameIndex() * MechanicalSparkProxyLightFrameSeconds);
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

        private static bool TryReadPlayerRuntimePosition(
            IPlayerRuntimeContext playerContext,
            IPlayerMovementPoseReadModel poseReadModel,
            out Vector3 playerPosition)
        {
            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                if (math.all(math.isfinite(movementState.WorldPosition)))
                {
                    float3 runtimePosition = movementState.WorldPosition;
                    playerPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                    return true;
                }

                if (math.all(math.isfinite(movementState.PredictedWorldPosition)))
                {
                    float3 predictedRuntimePosition = movementState.PredictedWorldPosition;
                    playerPosition = new Vector3(
                        predictedRuntimePosition.x,
                        predictedRuntimePosition.y,
                        predictedRuntimePosition.z);
                    return true;
                }

                if (movementState.PredictedAup.IsFinite())
                {
                    float3 aupRuntimePosition = movementState.PredictedAup.ToRuntimeFloat3();
                    if (math.all(math.isfinite(aupRuntimePosition)))
                    {
                        playerPosition = new Vector3(aupRuntimePosition.x, aupRuntimePosition.y, aupRuntimePosition.z);
                        return true;
                    }
                }
            }

            if (poseReadModel != null && poseReadModel.TryGetRuntimePosition(out playerPosition))
                return IsFiniteVector(playerPosition);

            playerPosition = default;
            return false;
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
            Quaternion currentRotation = body.rotation;
            Vector3 recoveredPosition = RuntimeWatchdog.ReportRigidbodyNanRecovery(
                NanRecoverySystemHash,
                toxicVector,
                ResolveLastFiniteBodyRuntimePosition(rigidbodyIndex, currentPosition));

            float3 zeroVelocity3 = float3.zero;
            Vector3 zeroVelocity = new Vector3(zeroVelocity3.x, zeroVelocity3.y, zeroVelocity3.z);
            body.linearVelocity = zeroVelocity;
            body.angularVelocity = zeroVelocity;
            body.isKinematic = true;
            body.detectCollisions = false;
            if (IsFiniteVector(recoveredPosition))
            {
                body.position = recoveredPosition;
                CacheLastFiniteBodyAup(rigidbodyIndex, recoveredPosition);
            }

            body.rotation = TryNormalizeQuaternion(currentRotation, out Quaternion recoveredRotation)
                ? recoveredRotation
                : Quaternion.identity;
            body.PublishTransform();
            body.Sleep();
            int entityId = body.gameObject != null
                ? unchecked((int)EntityId.ToULong(body.gameObject.GetEntityId()))
                : 0;
            ReportToxicVector(entityId, toxicVector);
        }

        internal bool TryRouteToCachedPlayerForceSink(Rigidbody body, Vector3 force, ForceMode mode)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            IPlayerMovementForceSink forceSink = _playerMovementForceSink;
            if (body == null ||
                playerContext == null ||
                forceSink == null ||
                !ReferenceEquals(body, playerContext.PlayerRigidbody) ||
                !IsFiniteNonZero(force))
            {
                return false;
            }

            // Player capsule torque is presentation-noise; route off-center requests as center velocity/acceleration.
            return QueuePlayerForceSink(forceSink, force, mode);
        }

        internal bool TryRouteToCachedPlayerForceSinkAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            IPlayerMovementForceSink forceSink = _playerMovementForceSink;
            if (body == null ||
                playerContext == null ||
                forceSink == null ||
                !ReferenceEquals(body, playerContext.PlayerRigidbody) ||
                !IsFiniteVector(worldPosition) ||
                !IsFiniteNonZero(force))
            {
                return false;
            }

            return QueuePlayerForceSink(forceSink, force, mode);
        }

        internal bool TryRouteToCachedPlayerLinearVelocitySet(Rigidbody body, Vector3 targetVelocity)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            IPlayerMovementForceSink forceSink = _playerMovementForceSink;
            if (body == null ||
                playerContext == null ||
                forceSink == null ||
                !ReferenceEquals(body, playerContext.PlayerRigidbody) ||
                !IsFiniteVector(targetVelocity))
            {
                return false;
            }

            Vector3 currentVelocity = TryResolveCachedPlayerVelocity(playerContext, out Vector3 resolvedVelocity)
                ? resolvedVelocity
                : Vector3.zero;
            Vector3 velocityDelta = targetVelocity - currentVelocity;
            if (VectorLengthSq(velocityDelta) > MinMagnitudeSq)
                forceSink.QueueExternalVelocityChange(velocityDelta);

            return true;
        }

        internal bool TrySuppressCachedPlayerAngularVelocitySet(Rigidbody body)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            return body != null &&
                   playerContext != null &&
                   _playerMovementForceSink != null &&
                   ReferenceEquals(body, playerContext.PlayerRigidbody);
        }

        private static bool TryResolveCachedPlayerVelocity(IPlayerRuntimeContext playerContext, out Vector3 velocity)
        {
            if (CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityForceSinkMaxAgeFrames, out velocity))
                return true;

            if (playerContext != null &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                float3 stateVelocity = movementState.Velocity;
                if (math.all(math.isfinite(stateVelocity)))
                {
                    velocity = new Vector3(stateVelocity.x, stateVelocity.y, stateVelocity.z);
                    return true;
                }
            }

            velocity = Vector3.zero;
            return false;
        }

        private static bool QueuePlayerForceSink(
            IPlayerMovementForceSink forceSink,
            Vector3 force,
            ForceMode mode)
        {
            const float safeMass = HydrodynamicPlayerEquivalentMassKg;
            switch (mode)
            {
                case ForceMode.Force:
                    forceSink.QueueExternalAcceleration(force / safeMass);
                    return true;

                case ForceMode.Acceleration:
                    forceSink.QueueExternalAcceleration(force);
                    return true;

                case ForceMode.Impulse:
                    forceSink.QueueExternalVelocityChange(force / safeMass);
                    return true;

                case ForceMode.VelocityChange:
                    forceSink.QueueExternalVelocityChange(force);
                    return true;
            }

            return false;
        }

        private void CacheLastFiniteBodyAup(int rigidbodyIndex, Vector3 position)
        {
            if ((uint)rigidbodyIndex >= (uint)_lastFiniteBodyAups.Length || !IsFiniteVector(position))
                return;

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition bodyAup))
                return;

            _lastFiniteBodyAups[rigidbodyIndex] = bodyAup;
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

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return aup.IsFinite();
        }

        private static bool IsFiniteQuaternion(Quaternion value)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(value4);
            return math.all(math.isfinite(value4)) &&
                   math.isfinite(lengthSq) &&
                   lengthSq > MinMagnitudeSq;
        }

        private static bool TryNormalizeQuaternion(Quaternion value, out Quaternion normalized)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(value4);
            if (!math.all(math.isfinite(value4)) || !math.isfinite(lengthSq) || lengthSq <= MinMagnitudeSq)
            {
                normalized = Quaternion.identity;
                return false;
            }

            value4 *= math.rsqrt(math.max(lengthSq, MinMagnitudeSq));
            if (value4.w < 0f)
                value4 = -value4;

            normalized = new Quaternion(value4.x, value4.y, value4.z, value4.w);
            return true;
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
            Hecton8.Core.H8Debug.LogError(message);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ReportToxicVector(int bodyId, Vector3 toxicVector)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            NativeAllocationTrackerRuntimeBridge.ReportLeak(ToxicVectorLog);
            Hecton8.Core.H8Debug.LogError(ToxicVectorLog);
#endif
        }

        private void ReleaseValidationBufferViews()
        {
            if (_packetValidationScheduled)
            {
                if (!DispatcherJobSwap.TryComplete(ref _packetValidationHandle, forceComplete: true))
                    return;

                _packetValidationScheduled = false;
                H8Memory.RegisterActiveJob(OwnerSystemId, default);
            }

            ReleaseValidationScheduleBufferPins();
            ReleaseVaultBufferView(ref _validationPacketBufferHandle);
            ReleaseVaultBufferView(ref _validationMaskBufferHandle);
            _validationPacketBufferHandle = default;
            _validationMaskBufferHandle = default;
            _packetValidationHandle = default;
            _packetValidationScheduled = false;
        }

        private void ReleaseForcePacketBufferViews()
        {
            ReleaseVaultBufferView(ref _frontPacketBufferHandle);
            ReleaseVaultBufferView(ref _backPacketBufferHandle);
            _frontPacketBufferHandle = default;
            _backPacketBufferHandle = default;
            _frontCount = 0;
            _backCount = 0;
        }

        private void ReleaseVaultBufferView<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault dataVault = _dataVault;
            if (dataVault != null && handle.BufferID != 0u)
                dataVault.ReleaseBuffer(in handle);

            handle = default;
        }
    }

    /// <summary>
    /// Common physics routing facade that keeps player-body writes inside <see cref="IMotorForces"/>
    /// and routes all other rigidbody writes through <see cref="PhysicsApplySystem"/>.
    /// </summary>
    public static class PhysicsForceRouter
    {
        private const float MaxSafeAcceleration = 50f;
        private const float QuaternionMagnitudeEpsilonSq = 0.000001f;

        internal static bool ApplyKinematicWeldSnap(Rigidbody body, Vector3 targetPosition, Quaternion targetRotation)
        {
            if (body == null ||
                !IsFiniteVector(targetPosition) ||
                !TryNormalizeQuaternion(targetRotation, out Quaternion normalizedRotation))
            {
                return false;
            }

            bool restoreDetectCollisions = body.detectCollisions;
            body.detectCollisions = false;
            body.useGravity = false;
            body.isKinematic = true;
            float3 zeroVelocity3 = float3.zero;
            Vector3 zeroVelocity = new Vector3(zeroVelocity3.x, zeroVelocity3.y, zeroVelocity3.z);
            body.linearVelocity = zeroVelocity;
            body.angularVelocity = zeroVelocity;
            body.position = targetPosition;
            body.rotation = normalizedRotation;
            body.PublishTransform();
            body.detectCollisions = restoreDetectCollisions;
            body.Sleep();
            return true;
        }

        /// <summary>
        /// Routes a force request either into the player movement force sink or the deferred packet system.
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
            if (TryRouteToPlayerForceSink(body, safeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForce(body, safeForce, mode, wake);
        }

        public static bool QueueLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(linearVelocity, out Vector3 acceptedVelocity))
                return false;

            if (TryRouteToPlayerLinearVelocitySet(body, acceptedVelocity))
                return true;

            return QueueOwnedLinearVelocitySet(body, acceptedVelocity, wake);
        }

        internal static bool QueueOwnedLinearVelocitySet(Rigidbody body, Vector3 linearVelocity, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(linearVelocity, out Vector3 acceptedVelocity))
                return false;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueLinearVelocitySet(body, acceptedVelocity, ForcePacketPriority.Critical, wake);
        }

        public static bool QueueAngularVelocitySet(Rigidbody body, Vector3 angularVelocity, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(angularVelocity, out Vector3 acceptedVelocity))
                return false;

            if (TrySuppressPlayerAngularVelocitySet(body))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueAngularVelocitySet(body, acceptedVelocity, ForcePacketPriority.Critical, wake);
        }

        public static bool QueuePoseSet(Rigidbody body, Vector3 position, Quaternion rotation, bool wake = true)
        {
            if (!MathGuard.TryAcceptFinite(position, out Vector3 acceptedPosition) ||
                !IsFiniteQuaternion(rotation))
            {
                return false;
            }

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueuePoseSet(body, acceptedPosition, rotation, ForcePacketPriority.Critical, wake);
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
            if (TryRouteToPlayerForceSink(body, routeForce, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.QueueForce(body, new QueueForceArgs(force: safeForce, mode: mode, priority: ForcePacketPriority.Ambient, wake: wake, extraFlags: extraFlags));
        }

        /// <summary>
        /// Routes a force-at-position request either into the player movement force sink or the deferred packet system.
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
            if (TryRouteToPlayerForceSinkAtPosition(body, safeForce, acceptedWorldPosition, mode))
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
            if (TryRouteToPlayerForceSinkAtPosition(body, routeForce, acceptedWorldPosition, mode))
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

        private static bool TryRouteToPlayerForceSink(Rigidbody body, Vector3 force, ForceMode mode)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.TryRouteToCachedPlayerForceSink(body, force, mode);
        }

        private static bool TryRouteToPlayerLinearVelocitySet(Rigidbody body, Vector3 targetVelocity)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.TryRouteToCachedPlayerLinearVelocitySet(body, targetVelocity);
        }

        private static bool TrySuppressPlayerAngularVelocitySet(Rigidbody body)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.TrySuppressCachedPlayerAngularVelocitySet(body);
        }

        internal static ForcePacketFlags ResolveBiomeBuoyancyFlags(Vector3 force, ForceMode mode)
        {
            return mode == ForceMode.Acceleration && force.y > 0f
                ? ForcePacketFlags.BiomeBuoyancy
                : ForcePacketFlags.None;
        }

        internal static Vector3 ClampUpwardAcceleration(Vector3 force, ForceMode mode)
        {
            if (mode == ForceMode.Acceleration && force.y > MaxSafeAcceleration)
                force.y = MaxSafeAcceleration;

            return force;
        }

        private static bool TryRouteToPlayerForceSinkAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode)
        {
            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system != null && system.TryRouteToCachedPlayerForceSinkAtPosition(body, force, worldPosition, mode);
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
            return TryNormalizeQuaternion(value, out _);
        }

        private static bool TryNormalizeQuaternion(Quaternion value, out Quaternion normalized)
        {
            float4 value4 = new float4(value.x, value.y, value.z, value.w);
            float lengthSq = math.lengthsq(value4);
            if (!math.all(math.isfinite(value4)) ||
                !math.isfinite(lengthSq) ||
                lengthSq <= QuaternionMagnitudeEpsilonSq)
            {
                normalized = Quaternion.identity;
                return false;
            }

            value4 *= math.rsqrt(math.max(lengthSq, QuaternionMagnitudeEpsilonSq));
            if (value4.w < 0f)
                value4 = -value4;

            normalized = new Quaternion(value4.x, value4.y, value4.z, value4.w);
            return true;
        }
    }
}
