using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Gameplay;
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

        private byte _padding0;
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
    /// Static physics-domain event surface for transient pressure impulses.
    /// </summary>
    public static class PhysicsEventBus
    {
        /// <summary>Delegate used by pressure-impulse subscribers.</summary>
        public delegate void PressureImpulseEventHandler(in PressureImpulseEvent pressureEvent);

        /// <summary>Delegate used by EMP subscribers.</summary>
        public delegate void ElectromagneticPulseEventHandler(in ElectromagneticPulseEvent pulseEvent);

        /// <summary>Fired when a bulkhead blowout impulse is emitted.</summary>
        public static event PressureImpulseEventHandler OnPressureImpulse;

        /// <summary>Fired when an EMP pulse is emitted.</summary>
        public static event ElectromagneticPulseEventHandler OnElectromagneticPulse;

        /// <summary>Broadcasts one pressure-impulse payload.</summary>
        public static void NotifyPressureImpulse(in PressureImpulseEvent pressureEvent)
        {
            OnPressureImpulse?.Invoke(pressureEvent);
        }

        /// <summary>Broadcasts one EMP payload.</summary>
        public static void NotifyElectromagneticPulse(in ElectromagneticPulseEvent pulseEvent)
        {
            OnElectromagneticPulse?.Invoke(pulseEvent);
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
    }

    [BurstCompile(FloatMode = FloatMode.Fast)]
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
    public sealed class PhysicsApplySystem : MonoBehaviour, IPhysicsService, IFixedTickable, ILateFrameTickable
    {
        private const int MaxTrackedBodies = 64;
        private const int MaxQueuedPackets = 512;
        private const int MaxQueuedSubmarineImpactSignals = 32;
        private const float MinMagnitudeSq = 0.000001f;
        private const float HullYieldThresholdJoules = 225000f;
        private const string NonFiniteForceLog = "[PhysicsApplySystem] Non-finite force packet detected. Zeroing vector.";
        private const string NonFiniteTorqueLog = "[PhysicsApplySystem] Non-finite torque packet detected. Zeroing vector.";
        private const string NonFinitePointOffsetLog = "[PhysicsApplySystem] Non-finite point-offset packet detected. Zeroing offset.";
        private const string InvalidForcePacketLog = "[PhysicsApplySystem] Burst packet validation rejected a non-finite or out-of-range packet.";
        private static readonly ProfilerMarker _fixedTickProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FixedTick");
        private static readonly ProfilerMarker _packetValidationProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.ValidatePackets");
        private static readonly ProfilerMarker _flushFrontBufferProfilerMarker = new ProfilerMarker("H8.PhysicsApplySystem.FlushFrontBuffer");

        private static PhysicsApplySystem _instance;

        private struct DeferredSubmarineImpactSignal
        {
            public float PreviousIntegrityNormalized;
            public float NextIntegrityNormalized;
            public DamageSignal Signal;
            public TraumaLevel TraumaLevel;
        }

        // COLD ALLOC: ForcePacket[512] — end-of-step flush buffer — owner: PhysicsApplySystem
        private ForcePacket[] _frontPackets = new ForcePacket[MaxQueuedPackets];
        // COLD ALLOC: ForcePacket[512] — current-step gather buffer — owner: PhysicsApplySystem
        private ForcePacket[] _backPackets = new ForcePacket[MaxQueuedPackets];
        // COLD ALLOC: Rigidbody[64] — active rigidbody slot map for deferred packet application — owner: PhysicsApplySystem
        private readonly Rigidbody[] _bodySlots = new Rigidbody[MaxTrackedBodies];
        // COLD ALLOC: List<Collider>[8] â€” submarine hull collider discovery for contact-modification enablement â€” owner: PhysicsApplySystem
        private readonly List<Collider> _submarineColliderScratch = new List<Collider>(8);
        private NativeArray<ForcePacket> _validationPackets;
        private NativeArray<byte> _validationMask;
        private NativeQueue<DeferredSubmarineImpactSignal> _submarineImpactSignals;

        private int _frontCount;
        private int _backCount;
        private bool _isInitialized;
        private bool _fixedTickRegistered;
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
            _isInitialized = true;
        }

        /// <summary>
        /// Static fallback clear path used before the service is resolved into <see cref="GlobalRegistry"/>.
        /// </summary>
        public static void ClearQueuedPacketsStatic()
        {
            if (_instance != null)
                _instance.ClearQueuedPackets();
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

        /// <inheritdoc />
        public bool QueueForce(Rigidbody body, Vector3 force, ForceMode mode, bool wake = true)
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
            if (rigidbodyIndex < 0 || _backCount >= _backPackets.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PhysicsApplySystem] Force packet queue saturated.");
#endif
                return false;
            }

            _backPackets[_backCount++] = new ForcePacket
            {
                Force = sanitizedForce,
                Torque = Vector3.zero,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasForce | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                RigidbodyIndex = rigidbodyIndex
            };
            return true;
        }

        /// <inheritdoc />
        public bool QueueForceAtPosition(Rigidbody body, Vector3 force, Vector3 worldPosition, ForceMode mode, bool wake = true)
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
            if (rigidbodyIndex < 0 || _backCount >= _backPackets.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PhysicsApplySystem] Point-force packet queue saturated.");
#endif
                return false;
            }

            Vector3 pointOffset = worldPosition - body.worldCenterOfMass;
            if (!IsFiniteNonZero(pointOffset))
                pointOffset = Vector3.zero;

            _backPackets[_backCount++] = new ForcePacket
            {
                Force = sanitizedForce,
                Torque = Vector3.zero,
                PointOffset = pointOffset,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasForce | ForcePacketFlags.ApplyAtPosition | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                RigidbodyIndex = rigidbodyIndex
            };
            return true;
        }

        /// <inheritdoc />
        public bool QueueTorque(Rigidbody body, Vector3 torque, ForceMode mode, bool wake = true)
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
            if (rigidbodyIndex < 0 || _backCount >= _backPackets.Length)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[PhysicsApplySystem] Torque packet queue saturated.");
#endif
                return false;
            }

            _backPackets[_backCount++] = new ForcePacket
            {
                Force = Vector3.zero,
                Torque = sanitizedTorque,
                PointOffset = Vector3.zero,
                Mode = mode,
                Flags = (byte)(ForcePacketFlags.HasTorque | (wake ? ForcePacketFlags.WakeBody : ForcePacketFlags.None)),
                RigidbodyIndex = rigidbodyIndex
            };
            return true;
        }

        /// <inheritdoc />
        public void ClearQueuedPackets()
        {
            System.Array.Clear(_frontPackets, 0, _frontCount);
            System.Array.Clear(_backPackets, 0, _backCount);
            System.Array.Clear(_bodySlots, 0, _bodySlots.Length);
            _frontCount = 0;
            _backCount = 0;
            _frontBufferValidationReady = false;
            _packetValidationScheduled = false;
            _packetValidationHandle = default;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            EnsureValidationBuffers();
            if (!_submarineImpactSignals.IsCreated)
            {
                // COLD ALLOC: NativeQueue<DeferredSubmarineImpactSignal>(Persistent) â€” deferred submarine trauma queue flushed after contact modification â€” owner: PhysicsApplySystem
                _submarineImpactSignals = new NativeQueue<DeferredSubmarineImpactSignal>(Allocator.Persistent);
            }

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_fixedTickRegistered)
            {
                // Flush after world/player fixed lanes so deferred packets apply in the same simulation step.
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.UI);
                _fixedTickRegistered = true;
            }

            if (Application.isPlaying && GlobalRegistry.Dispatcher != null && !_lateFrameTickRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameTickRegistered = true;
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

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameTickRegistered = false;
            }

            if (_isInitialized)
            {
                GlobalRegistry.UnregisterPhysicsService(this);
                _isInitialized = false;
            }

            DisposeValidationBuffers();

            if (_submarineImpactSignals.IsCreated)
                _submarineImpactSignals.Dispose();

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
                if (_packetValidationScheduled)
                    return;

                SwapBuffers();
                ScheduleFrontPacketValidation();
            }
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
                    if ((flags & ForcePacketFlags.WakeBody) != 0 && body.IsSleeping())
                        body.WakeUp();

                    if ((flags & ForcePacketFlags.HasForce) != 0)
                    {
                        if (!TrySanitizeVector(packet.Force, NonFiniteForceLog, out Vector3 sanitizedForce))
                            sanitizedForce = Vector3.zero;

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

        private void SwapBuffers()
        {
            ForcePacket[] swap = _frontPackets;
            _frontPackets = _backPackets;
            _backPackets = swap;
            _frontCount = _backCount;
            _backCount = 0;
        }

        private void EnsureValidationBuffers()
        {
            if (!_validationPackets.IsCreated)
            {
                // COLD ALLOC: NativeArray<ForcePacket>[512] â€” Burst packet validation staging buffer â€” owner: PhysicsApplySystem
                _validationPackets = new NativeArray<ForcePacket>(MaxQueuedPackets, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_validationMask.IsCreated)
            {
                // COLD ALLOC: NativeArray<byte>[512] â€” Burst packet validation mask â€” owner: PhysicsApplySystem
                _validationMask = new NativeArray<byte>(MaxQueuedPackets, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
            if (!_submarineImpactSignals.IsCreated || _submarineImpactSignals.Count >= MaxQueuedSubmarineImpactSignals)
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

            while (_submarineImpactSignals.TryDequeue(out DeferredSubmarineImpactSignal queuedSignal))
            {
                traumaDispatcher.OnIntegrityChanged(
                    queuedSignal.PreviousIntegrityNormalized,
                    queuedSignal.NextIntegrityNormalized,
                    queuedSignal.Signal);
                traumaDispatcher.OnTraumaThresholdCrossed(queuedSignal.TraumaLevel);
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
            if (!_packetValidationScheduled)
                return;

            using (_packetValidationProfilerMarker.Auto())
            {
                _packetValidationHandle.Complete();
                _packetValidationHandle = default;
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
                dependency = _validationPackets.Dispose(dependency);
                _validationPackets = default;
            }

            if (_validationMask.IsCreated)
            {
                dependency = _validationMask.Dispose(dependency);
                _validationMask = default;
            }

            _packetValidationHandle = dependency;
            _packetValidationScheduled = false;
        }
    }

    /// <summary>
    /// Common physics routing facade that keeps player-body writes inside <see cref="IMotorForces"/>
    /// and routes all other rigidbody writes through <see cref="PhysicsApplySystem"/>.
    /// </summary>
    public static class PhysicsForceRouter
    {
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
            if (TryRouteToPlayerMotor(body, force, mode))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForce(body, force, mode, wake);
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
            if (TryRouteToPlayerMotorAtPosition(body, force, worldPosition))
                return true;

            PhysicsApplySystem system = PhysicsApplySystem.EnsureRuntimeInstance();
            return system.QueueForceAtPosition(body, force, worldPosition, mode, wake);
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
    }
}
