using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Caves;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Physics.KCC;
using Hecton8.World;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Authoritative kinematic application layer for player locomotion.
    /// All Rigidbody writes route through this component.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Player/Hecton Player Motor")]
    public sealed class HectonPlayerMotor : MonoBehaviour, IMotorForces, IPlayerSeatLockMotorSink, IPlayerKinematicsMotorSyncSink, IPostFixedTickable, ILateFrameTickable, IInventoryEventListener, IGlobalRegistryHotSwapListener
    {
        private const float MinVectorMagnitudeSq = 0.000001f;
        private const int MaxSlideSweepIterations = 2;
        private const uint KccVelocityMotorMaxAgeFrames = 12u;
        private const float DenormalVelocityFlushThresholdMetersPerSecond = 0.001f;
        private const float DenormalVelocityFlushThresholdMetersPerSecondSq =
            DenormalVelocityFlushThresholdMetersPerSecond * DenormalVelocityFlushThresholdMetersPerSecond;
        private const float InventoryLoadMinimumMovementMultiplier = 0.62f;
        private const float WakeSiltEmissionSpeedThresholdMetersPerSecond = 4.5f;
        private const float WakeSiltEmissionSpeedThresholdMetersPerSecondSq =
            WakeSiltEmissionSpeedThresholdMetersPerSecond * WakeSiltEmissionSpeedThresholdMetersPerSecond;
        private const float WakeSiltEmissionCooldownSeconds = 0.35f;
        private const float HydrodynamicMinimumEffectiveMassKg = 0.001f;
        private const float HydrodynamicPlayerEquivalentMassKg = 80f;
        private const float HydrodynamicAddedMassAccelerationScale = 0.45f;
        private const float HydrodynamicAddedMassAccelerationForceScalar =
            1f / (1f + HydrodynamicAddedMassAccelerationScale);
        private const float DirectionalDragDominantAxisThresholdSq = 100f;
        private const float DirectionalDragSpeedSqPolynomialScale = 0.01f;
        private const int KccCollisionHitStride = 8;

        private Rigidbody _body;
        private CapsuleCollider _capsule;
        private HydrodynamicKccRuntime _hydrodynamicKccRuntime;
        private PlayerInventory _encumbranceSource;
        private bool _isGrounded;
        private bool _registeredLateFrameTick;
        private bool _registeredPostFixedTick;
        private bool _registeredMotorService;
        private bool _registeredHotSwap;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IFluidDecalPresentationSink _fluidDecals;
        private IPhysicsService _physicsService;
        private IDataVault _dataVault;
        private VaultGenerationHandle<HydrodynamicKccCollisionHitDTO> _kccCollisionHitsHandle;
        private VaultGenerationHandle<HydrodynamicKccDebugOutputDTO> _kccDebugOutputsHandle;
        private Vector3 _lastKnownLinearVelocity;
        private float _encumbranceMovementMultiplier = 1f;
        private float _wakeSiltEmissionCooldown;
        private Vector3 _lastKccContactNormal;
        private Vector3 _lastKccContactPoint;
        private float _lastKccContactDistance;
        private int _lastKccContactPhysicsFrame = -1;
        private uint _lastKccContactShiftSequence;
        private uint _lastKccContactBodyBindEpoch;
        private bool _lastKccContactIsVoxel;
        private Vector3 _lastWallSlideNormal;
        private Vector3 _lastWallSlidePoint;
        private float _lastWallSlideBlockedSpeed;
        private float _lastWallSlideAngleDegrees;
        private float _lastWallSlideVelocityReduction01;
        private int _lastWallSlidePhysicsFrame = -1;
        private uint _lastWallSlideShiftSequence;
        private uint _lastWallSlideBodyBindEpoch;
        private bool _lastWallSlideIsVoxel;
        private bool _kinematicRepairSnapReady;
        private uint _bodyBindEpoch;
        private uint _kinematicRepairSnapBodyBindEpoch;
        private uint _kinematicRepairSnapShiftSequence;
        private KinematicRepairTargetProbe _kinematicRepairTargetProbe;
        private KinematicRepairSnapPoint _kinematicRepairSnapPoint;

        /// <inheritdoc />
        public Rigidbody Body => _body;

        /// <inheritdoc />
        public CapsuleCollider Capsule => _capsule;

        /// <inheritdoc />
        public bool IsGrounded => _isGrounded;

        /// <summary>Current event-driven carry-load movement scalar.</summary>
        public float EncumbranceMovementMultiplier => _encumbranceMovementMultiplier;

        /// <summary>Returns the most recent KCC wall projection contact if it is still within the requested fixed-frame window.</summary>
        public bool TryGetRecentWallSlideContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float blockedSpeed,
            out float slideAngleDegrees,
            out float velocityReduction01,
            out int physicsFrame)
        {
            return TryGetRecentWallSlideContact(
                maxPhysicsFrameAge,
                out normal,
                out point,
                out blockedSpeed,
                out slideAngleDegrees,
                out velocityReduction01,
                out physicsFrame,
                out _);
        }

        /// <summary>Returns the most recent KCC wall projection contact with voxel-wall classification.</summary>
        public bool TryGetRecentWallSlideContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float blockedSpeed,
            out float slideAngleDegrees,
            out float velocityReduction01,
            out int physicsFrame,
            out bool isVoxelWall)
        {
            normal = Vector3.zero;
            point = Vector3.zero;
            blockedSpeed = 0f;
            slideAngleDegrees = 0f;
            velocityReduction01 = 0f;
            physicsFrame = _lastWallSlidePhysicsFrame;
            isVoxelWall = false;

            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            if (_lastWallSlideShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                return false;

            if (_lastWallSlideBodyBindEpoch != _bodyBindEpoch)
                return false;

            if (_lastWallSlidePhysicsFrame < 0)
                return false;

            int age = SystemDispatcher.CurrentFrameIndex - _lastWallSlidePhysicsFrame;
            if (age < 0 || age > math.max(0, maxPhysicsFrameAge))
                return false;

            if (_lastWallSlideNormal.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            normal = _lastWallSlideNormal;
            point = _lastWallSlidePoint;
            blockedSpeed = _lastWallSlideBlockedSpeed;
            slideAngleDegrees = _lastWallSlideAngleDegrees;
            velocityReduction01 = _lastWallSlideVelocityReduction01;
            isVoxelWall = _lastWallSlideIsVoxel;
            return true;
        }

        internal bool TryGetRecentKinematicCollisionContact(
            int maxPhysicsFrameAge,
            out Vector3 normal,
            out Vector3 point,
            out float distance,
            out int physicsFrame,
            out bool isVoxelContact)
        {
            normal = Vector3.zero;
            point = Vector3.zero;
            distance = 0f;
            physicsFrame = _lastKccContactPhysicsFrame;
            isVoxelContact = false;

            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            if (_lastKccContactShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
                return false;

            if (_lastKccContactBodyBindEpoch != _bodyBindEpoch)
                return false;

            if (_lastKccContactPhysicsFrame < 0)
                return false;

            int age = SystemDispatcher.CurrentFrameIndex - _lastKccContactPhysicsFrame;
            if (age < 0 || age > math.max(0, maxPhysicsFrameAge))
                return false;

            if (_lastKccContactNormal.sqrMagnitude <= MinVectorMagnitudeSq)
                return false;

            normal = _lastKccContactNormal;
            point = _lastKccContactPoint;
            distance = _lastKccContactDistance;
            isVoxelContact = _lastKccContactIsVoxel;
            return true;
        }

        /// <summary>Returns the latest ladder contact point without exposing Unity PhysX hit DTOs to KCC sync consumers.</summary>
        public bool TryGetRecentLadderContact(int maxPhysicsFrameAge, out Vector3 point)
        {
            point = default;
            if (HectonFloatingOrigin.IsShiftInProgress)
                return false;

            uint maxAgeFrames = (uint)math.max(0, maxPhysicsFrameAge);
            System.ReadOnlySpan<PlayerStateSignal> signals = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            for (int i = signals.Length - 1; i >= 0; i--)
            {
                if (TryResolveLadderContactSignal(in signals[i], maxAgeFrames, out point))
                    return true;
            }

            return SignalBus<PlayerStateSignal>.TryGetLatest(out PlayerStateSignal latestSignal, out _) &&
                   TryResolveLadderContactSignal(in latestSignal, maxAgeFrames, out point);
        }

        private static bool TryResolveLadderContactSignal(in PlayerStateSignal signal, uint maxAgeFrames, out Vector3 point)
        {
            point = default;
            const byte RequiredFlags = PlayerStateSignal.FlagActive | PlayerStateSignal.FlagClimbing;
            if (signal.State != PlayerStateSignal.StateClimbing ||
                (signal.Flags & RequiredFlags) != RequiredFlags ||
                !IsFreshSignalFrame(SystemDispatcher.CurrentFrameId, signal.Frame, maxAgeFrames) ||
                !signal.PositionAup.TryToRuntimeFloat3(out float3 runtimePoint) ||
                !math.all(math.isfinite(runtimePoint)))
            {
                return false;
            }

            point = new Vector3(runtimePoint.x, runtimePoint.y, runtimePoint.z);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFreshSignalFrame(uint currentFrame, uint signalFrame, uint maxAgeFrames)
        {
            return signalFrame <= currentFrame && currentFrame - signalFrame <= maxAgeFrames;
        }

        /// <summary>Consumes the most recent hand IK repair snap resolved by the typed KCC repair lane.</summary>
        public bool TryConsumeKinematicRepairSnap(out KinematicRepairSnapPoint snapPoint)
        {
            return TryConsumeKinematicRepairSnap(out _, out snapPoint);
        }

        /// <summary>Consumes the most recent repair probe and snap point pair.</summary>
        public bool TryConsumeKinematicRepairSnap(
            out KinematicRepairTargetProbe probe,
            out KinematicRepairSnapPoint snapPoint)
        {
            probe = default;
            snapPoint = default;
            if (HectonFloatingOrigin.IsShiftInProgress)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (_kinematicRepairSnapShiftSequence != HectonFloatingOrigin.CurrentShiftSequence)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (_kinematicRepairSnapBodyBindEpoch != _bodyBindEpoch)
            {
                _kinematicRepairSnapReady = false;
                _kinematicRepairTargetProbe = default;
                _kinematicRepairSnapPoint = default;
                return false;
            }

            if (!_kinematicRepairSnapReady)
                return false;

            _kinematicRepairSnapReady = false;
            probe = _kinematicRepairTargetProbe;
            snapPoint = _kinematicRepairSnapPoint;
            return snapPoint.ColliderInstanceId != 0;
        }

        /// <summary>Binds authoritative body references owned by the locomotion controller.</summary>
        public void Bind(Rigidbody body, CapsuleCollider capsule)
        {
            bool bodyChanged = _body != body || _capsule != capsule;
            _body = body;
            _capsule = capsule;
            if (body != null && body.TryGetComponent(out HydrodynamicKccRuntime hydrodynamicKccRuntime))
                _hydrodynamicKccRuntime = hydrodynamicKccRuntime;
            else
                _hydrodynamicKccRuntime = null;
            if (bodyChanged)
            {
                unchecked
                {
                    _bodyBindEpoch++;
                }

                ResetBodyBoundCachedResults();
            }

        }

        /// <summary>Binds the inventory source accepted by encumbrance events.</summary>
        public void BindEncumbranceSource(PlayerInventory inventory)
        {
            _encumbranceSource = inventory;
        }

        private void OnEnable()
        {
            if (_hydrodynamicKccRuntime == null)
                TryGetComponent(out _hydrodynamicKccRuntime);
            InventoryEvents.Register(this);
            TryRegisterLateFrameTick();
            TryRegisterPostFixedTick();
            TryRegisterMotorService();
            TryRegisterHotSwap();
        }

        private void OnDisable()
        {
            InventoryEvents.Unregister(this);
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterMotorService();
            TryUnregisterHotSwap();
            ResetKccContactState();
            ResetWallSlideContactState();
            ResetDisabledProbeState();
        }

        private void OnDestroy()
        {
            InventoryEvents.Unregister(this);
            TryUnregisterLateFrameTick();
            TryUnregisterPostFixedTick();
            TryUnregisterMotorService();
            TryUnregisterHotSwap();
            ResetKccContactState();
            ResetWallSlideContactState();
            ResetDisabledProbeState();
        }

        /// <summary>Updates grounded state mirror for external systems.</summary>
        public void SetGroundedState(bool isGrounded)
        {
            _isGrounded = isGrounded;
        }

        /// <summary>Applies a pre-resolved carry-load movement scalar.</summary>
        public void SetEncumbranceMovementMultiplier(float multiplier)
        {
            _encumbranceMovementMultiplier = math.clamp(multiplier, InventoryLoadMinimumMovementMultiplier, 1f);
        }

        /// <inheritdoc />
        public void OnInventoryEvent(in InventoryEventPayload payload)
        {
            if ((InventoryEventType)payload.EventType != InventoryEventType.EncumbranceChanged)
                return;

            if (!InventoryEvents.TryBuildEncumbranceChangedEvent(in payload, out EncumbranceChangedEvent encumbranceEvent))
                return;

            HandleEncumbranceChanged(encumbranceEvent);
        }

        private void HandleEncumbranceChanged(EncumbranceChangedEvent payload)
        {
            if (_encumbranceSource != null && payload.Inventory != _encumbranceSource)
                return;

            float load01 = math.saturate(payload.Load01);
            SetEncumbranceMovementMultiplier(math.lerp(1f, InventoryLoadMinimumMovementMultiplier, load01));
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object newService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterLateFrameTick();
                TryUnregisterPostFixedTick();
                if (newService != null && isActiveAndEnabled)
                {
                    TryRegisterLateFrameTick();
                    TryRegisterPostFixedTick();
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = newService as IDataVault;
                ResetKccContactState();
                ResetDisabledProbeState();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime)
                _fluidDecals = newService as IFluidDecalPresentationSink;
            else if (serviceSlot == GlobalRegistryServiceSlot.Player)
                _playerRuntimeContext = newService as IPlayerRuntimeContext;
            else if (serviceSlot == GlobalRegistryServiceSlot.Physics)
                _physicsService = newService as IPhysicsService;
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            OnGlobalRegistryServiceReplaced(serviceSlot, previousService, currentService);
        }

        /// <inheritdoc />
        public void AddExternalAcceleration(Vector3 acceleration)
        {
            ApplyAcceleration(acceleration);
        }

        /// <inheritdoc />
        public void AddExternalVelocityChange(Vector3 velocityChange)
        {
            ApplyVelocityChange(velocityChange);
        }

        /// <summary>Applies a world-space force through ForceMode.Force after finite validation.</summary>
        public void ApplyForce(Vector3 force)
        {
            if (_body == null || !IsFiniteNonZero(force))
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                Vector3 currentVelocity = ResolveCurrentLinearVelocity(Vector3.zero);
                Vector3 acceleration = ResolveHydrodynamicAddedMassStatelessAcceleration(force, currentVelocity, ResolveCurrentBodyMassKg());
                _hydrodynamicKccRuntime?.TryQueueExternalAcceleration(acceleration);
                return;
            }

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueForce(_body, force, ForceMode.Force);
        }

        /// <summary>Applies a world-space acceleration after finite validation.</summary>
        public void ApplyAcceleration(Vector3 acceleration)
        {
            if (_body == null || !IsFiniteNonZero(acceleration))
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                _hydrodynamicKccRuntime?.TryQueueExternalAcceleration(acceleration);
                return;
            }

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueForce(_body, acceleration, ForceMode.Acceleration);
        }

        /// <summary>Applies a world-space velocity change after finite validation.</summary>
        public void ApplyVelocityChange(Vector3 velocityChange)
        {
            if (_body == null || !IsFiniteNonZero(velocityChange))
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                _hydrodynamicKccRuntime?.TryQueueExternalVelocityChange(velocityChange);
                _lastKnownLinearVelocity = SafeVelocity(ResolveCurrentLinearVelocity(Vector3.zero) + velocityChange, _lastKnownLinearVelocity);
                return;
            }

            _lastKnownLinearVelocity = SafeVelocity(ResolveCurrentLinearVelocity(Vector3.zero) + velocityChange, _lastKnownLinearVelocity);
            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueForce(_body, velocityChange, ForceMode.VelocityChange);
        }

        /// <summary>Applies an impulse after finite validation.</summary>
        public void ApplyImpulse(Vector3 impulse)
        {
            if (_body == null || !IsFiniteNonZero(impulse))
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                Vector3 currentVelocity = ResolveCurrentLinearVelocity(Vector3.zero);
                Vector3 velocityChange = ResolveHydrodynamicAddedMassStatelessAcceleration(impulse, currentVelocity, ResolveCurrentBodyMassKg());
                _hydrodynamicKccRuntime?.TryQueueExternalVelocityChange(velocityChange);
                return;
            }

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueForce(_body, impulse, ForceMode.Impulse);
        }

        /// <summary>
        /// Applies torque using ForceMode.Force after clamping the resulting angular acceleration.
        /// </summary>
        public void ApplyTorque(Vector3 torque, float maxAngularAcceleration)
        {
            if (_body == null || !IsFiniteNonZero(torque))
                return;

            if (HydrodynamicKccOwnsCollision())
                return;

            Vector3 clampedTorque = ClampTorqueByAngularAcceleration(torque, maxAngularAcceleration);
            if (!IsFiniteNonZero(clampedTorque))
                return;

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueTorque(_body, clampedTorque, ForceMode.Force);
        }

        /// <summary>
        /// Applies an angular velocity change while clamping the equivalent angular acceleration.
        /// </summary>
        public void ApplyAngularVelocityChange(
            Vector3 angularVelocityChange,
            float maxAngularAcceleration,
            float fixedDeltaTime)
        {
            if (_body == null || !IsFiniteNonZero(angularVelocityChange))
                return;

            if (HydrodynamicKccOwnsCollision())
                return;

            float safeFixedDeltaTime = math.max(fixedDeltaTime, 0.0001f);
            float allowedAngularVelocityDelta = math.max(0f, maxAngularAcceleration) * safeFixedDeltaTime;
            Vector3 clampedDelta = angularVelocityChange;
            float sqrMagnitude = clampedDelta.sqrMagnitude;
            if (allowedAngularVelocityDelta > 0f && sqrMagnitude > allowedAngularVelocityDelta * allowedAngularVelocityDelta)
                clampedDelta = SafeNormal(clampedDelta, Vector3.zero) * allowedAngularVelocityDelta;

            if (!IsFiniteNonZero(clampedDelta))
                return;

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueTorque(_body, clampedDelta, ForceMode.VelocityChange);
        }

        /// <summary>
        /// Splits an off-center force into linear force plus capped torque around the center of mass.
        /// </summary>
        public void ApplyForceAtPositionSplit(
            Vector3 force,
            Vector3 applicationPoint,
            float maxLeverArm,
            float maxAngularAcceleration)
        {
            if (_body == null || !IsFiniteNonZero(force))
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                ApplyForce(force);
                return;
            }

            Vector3 lever = applicationPoint - _body.worldCenterOfMass;
            float maxLeverArmSq = maxLeverArm * maxLeverArm;
            if (maxLeverArm > 0f && lever.sqrMagnitude > maxLeverArmSq)
                lever = SafeNormal(lever, Vector3.zero) * maxLeverArm;

            ApplyForce(force);

            if (lever.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            Vector3 torque = CrossVector(lever, force);
            ApplyTorque(torque, maxAngularAcceleration);
        }

        /// <summary>Routes legacy absolute velocity targets through the central owner when Hydro KCC is inactive.</summary>
        public void SetLinearVelocity(Vector3 velocity)
        {
            if (_body == null)
                return;

            if (HydrodynamicKccOwnsCollision())
            {
                Vector3 kccCurrentVelocity = ResolveCurrentLinearVelocity(Vector3.zero);
                Vector3 kccTargetVelocity = SafeVelocity(velocity, kccCurrentVelocity);
                _lastKnownLinearVelocity = kccTargetVelocity;
                _hydrodynamicKccRuntime?.TryQueueExternalVelocityTarget(kccTargetVelocity);
                return;
            }

            Vector3 rawCurrentVelocity = ResolveCurrentLinearVelocity(Vector3.zero);
            bool currentVelocityFinite = MathGuard.IsFinite(rawCurrentVelocity);
            Vector3 currentVelocity = SafeVelocity(rawCurrentVelocity);
            Vector3 targetVelocity = SafeVelocity(velocity, currentVelocity);
            _lastKnownLinearVelocity = targetVelocity;
            if (currentVelocityFinite && (targetVelocity - currentVelocity).sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueLinearVelocitySet(_body, targetVelocity);
        }

        /// <summary>Routes legacy angular velocity targets through the motor owner when Hydro KCC is inactive.</summary>
        public void SetAngularVelocity(Vector3 angularVelocity, bool wake = true)
        {
            if (_body == null || HydrodynamicKccOwnsCollision())
                return;

            Vector3 targetAngularVelocity = SafeVelocity(angularVelocity, Vector3.zero);
            IPhysicsService physicsService = ResolvePhysicsService();
            physicsService?.QueueAngularVelocitySet(_body, targetAngularVelocity, wake);
        }

        /// <summary>Projects the current linear velocity onto a collision plane.</summary>
        public void ProjectLinearVelocityOnPlane(Vector3 planeNormal)
        {
            if (_body == null || planeNormal.sqrMagnitude <= MinVectorMagnitudeSq)
                return;

            Vector3 projectedVelocity = ProjectVelocityOnCollisionPlane(ResolveCurrentLinearVelocity(Vector3.zero), planeNormal);
            SetLinearVelocity(projectedVelocity);
        }

        /// <summary>Moves the body kinematically after finite validation.</summary>
        public void MovePosition(Vector3 position)
        {
            if (_body == null)
                return;

            float3 position3 = new float3(position.x, position.y, position.z);
            if (!math.all(math.isfinite(position3)))
                return;

            Vector3 snappedPosition = SnapMillimeter(position);
            if (HydrodynamicKccOwnsCollision())
            {
                _hydrodynamicKccRuntime?.TryQueueExternalPositionTarget(snappedPosition);
                return;
            }

            _body.MovePosition(snappedPosition);
        }

        bool IPlayerSeatLockMotorSink.HasControllableBody => _body != null;

        void IPlayerSeatLockMotorSink.MoveSeatLockPosition(Vector3 position)
        {
            MovePosition(position);
        }

        void IPlayerSeatLockMotorSink.SetSeatLockLinearVelocity(Vector3 velocity)
        {
            SetLinearVelocity(velocity);
        }

        /// <summary>Moves the body rotation kinematically after finite validation.</summary>
        public void MoveRotation(Quaternion rotation)
        {
            if (_body == null || HydrodynamicKccOwnsCollision())
                return;

            if (!TryNormalizeRotation(rotation, out Quaternion normalizedRotation))
                return;

            _body.MoveRotation(normalizedRotation);
        }

        /// <summary>
        /// Applies the carrier-relative position formula using cached platform delta.
        /// </summary>
        public void ApplyCarrierMotion(
            Vector3 previousPlatformPosition,
            Vector3 currentPlatformPosition,
            Quaternion platformDeltaRotation,
            Vector3 localMoveWorld)
        {
            if (_body == null)
                return;

            Vector3 bodyPosition = ResolveCurrentRuntimePosition(previousPlatformPosition);
            Vector3 rotatedOffset = platformDeltaRotation * (bodyPosition - previousPlatformPosition);
            MovePosition(currentPlatformPosition + rotatedOffset + localMoveWorld);
        }

        /// <summary>
        /// Quadratic drag solve. Stable for variable fixed steps and cannot reverse velocity.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(Vector3 velocity, Vector3 dragCoefficient, float dt)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float3 drag3 = new float3(dragCoefficient.x, dragCoefficient.y, dragCoefficient.z);
            float speedMag = ApproximateSpeedMagnitude(velocity3);
            if (speedMag < DenormalVelocityFlushThresholdMetersPerSecond)
                return Vector3.zero;

            float safeDt = math.max(dt, 0.0001f);
            float3 denominator = 1f + math.max(drag3, 0f) * speedMag * safeDt;
            float3 result = velocity3 * math.rcp(math.max(denominator, new float3(0.001f)));
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        /// <summary>
        /// Scalar analytical quadratic drag solve: dv/dt = -k |v| v.
        /// Direction is preserved and reversal is impossible.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(Vector3 velocity, float dragCoefficient, float dt)
        {
            float3 velocity3 = new float3(velocity.x, velocity.y, velocity.z);
            float speedSq = math.lengthsq(velocity3);
            if (speedSq < DenormalVelocityFlushThresholdMetersPerSecondSq)
                return Vector3.zero;

            float speed = ApproximateSpeedMagnitude(velocity3);
            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * speed * safeDt;
            float3 result = velocity3 * math.rcp(math.max(denominator, 0.001f));
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        /// <summary>
        /// Directional analytical drag with a cross-sectional forward area term.
        /// </summary>
        public static Vector3 AnalyticalQuadraticDrag(
            Vector3 velocity,
            float dragCoefficient,
            Vector3 forward,
            float crossSectionalAreaScale,
            float dt)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            float3 velocity3 = new float3(safeVelocity.x, safeVelocity.y, safeVelocity.z);
            float speedSq = math.lengthsq(velocity3);
            if (speedSq < DenormalVelocityFlushThresholdMetersPerSecondSq)
                return Vector3.zero;

            float speed = ApproximateSpeedMagnitude(velocity3);
            float3 velocityDirection = ResolveScalableDragDirection(velocity3, speedSq);
            float3 safeForward = ResolveDominantPlanarAxis(forward, Vector3.forward);
            float drag = math.max(0.2f, math.abs(math.dot(velocityDirection, safeForward)));
            float directionalCrossSection = math.lerp(2.75f, 1f, math.saturate(drag));
            float speedCurve01 = math.saturate(speedSq * DirectionalDragSpeedSqPolynomialScale);
            float nonlinearDragCurve = 1f + (speedCurve01 * (0.35f + (0.65f * speedCurve01)));
            float areaScale = math.max(0.01f, crossSectionalAreaScale) * directionalCrossSection * nonlinearDragCurve;
            float safeDt = math.max(dt, 0.0001f);
            float denominator = 1f + math.max(0f, dragCoefficient) * areaScale * speed * safeDt;
            float3 result = velocity3 * math.rcp(math.max(denominator, 0.001f));
            return SafeVelocity(new Vector3(result.x, result.y, result.z), velocity);
        }

        private static float3 ResolveScalableDragDirection(float3 velocity, float speedSq)
        {
            float3 fallbackAxis = DistanceMath.DominantAxisOrDefault(velocity, new float3(0f, 0f, 1f));
            if (speedSq > DirectionalDragDominantAxisThresholdSq)
            {
                return fallbackAxis;
            }

            return velocity * math.rsqrt(math.max(speedSq, DenormalVelocityFlushThresholdMetersPerSecondSq));
        }

        /// <summary>Clears transient runtime state.</summary>
        public void ResetRuntimeState()
        {
            _isGrounded = false;
            ResetKccContactState();
            ResetWallSlideContactState();
            _kinematicRepairSnapReady = false;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
        }

        /// <summary>Applies added-mass scalar only to acceleration; deceleration remains force / mass.</summary>
        public static Vector3 ResolveHydrodynamicAddedMassStatelessForce(Vector3 force, Vector3 velocity)
        {
            Vector3 safeForce = SafeVelocity(force);
            if (safeForce.sqrMagnitude <= MinVectorMagnitudeSq)
                return safeForce;

            Vector3 safeVelocity = SafeVelocity(velocity);
            float velocitySq = safeVelocity.sqrMagnitude;
            bool accelerating = velocitySq <= MinVectorMagnitudeSq ||
                math.dot((float3)safeForce, (float3)safeVelocity) > 0f;
            float forceScalar = math.select(1f, HydrodynamicAddedMassAccelerationForceScalar, accelerating);
            return SafeVelocity(safeForce * forceScalar, safeForce);
        }

        /// <summary>Converts force to acceleration with stateless added mass and a zero-mass singularity guard.</summary>
        public static Vector3 ResolveHydrodynamicAddedMassStatelessAcceleration(Vector3 force, Vector3 velocity, float mass)
        {
            Vector3 safeForce = SafeVelocity(force);
            if (safeForce.sqrMagnitude <= MinVectorMagnitudeSq)
                return safeForce;

            Vector3 safeVelocity = SafeVelocity(velocity);
            float velocitySq = safeVelocity.sqrMagnitude;
            bool accelerating = velocitySq <= MinVectorMagnitudeSq ||
                math.dot((float3)safeForce, (float3)safeVelocity) > 0f;
            float finiteMass = math.select(0f, mass, math.isfinite(mass));
            float bodyMass = math.max(HydrodynamicMinimumEffectiveMassKg, finiteMass);
            float addedMass = bodyMass * HydrodynamicAddedMassAccelerationScale;
            float safeMass = math.max(HydrodynamicMinimumEffectiveMassKg, bodyMass + addedMass);
            float invMass = math.select(math.rcp(bodyMass), math.rcp(safeMass), accelerating);
            return SafeVelocity(safeForce * invMass, Vector3.zero);
        }

        private void ResetBodyBoundCachedResults()
        {
            ResetKccContactState();
            ResetWallSlideContactState();
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
        }

        /// <summary>
        /// Legacy repair-target bridge is disabled. Presentation must consume KCC/signal state instead.
        /// </summary>
        public bool ScheduleKinematicRepairTargetProbe(
            Vector3 origin,
            Vector3 direction,
            float distance,
            int layerMask,
            float surfaceOffset)
        {
            DiscardKinematicRepairTargetProbe();
            return false;
        }

        /// <inheritdoc />
        public void PostFixedTick(float fixedDeltaTime)
        {
            RefreshKccContactState();
            TryEmitWakeSiltDecal(fixedDeltaTime);
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (_kinematicRepairSnapReady)
                DiscardKinematicRepairTargetProbe();
        }

        private static bool IsFiniteNonZero(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3)) && math.lengthsq(value3) > MinVectorMagnitudeSq;
        }

        public bool HydrodynamicKccOwnsCollisionAuthority => HydrodynamicKccOwnsCollision();

        private bool HydrodynamicKccOwnsCollision()
        {
            HydrodynamicKccRuntime runtime = _hydrodynamicKccRuntime;
            return runtime != null && runtime.IsAuthorityRouteActive;
        }

        private Vector3 ResolveCurrentLinearVelocity(Vector3 fallback)
        {
            if (HydrodynamicKccOwnsCollision() &&
                CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMotorMaxAgeFrames, out Vector3 kccVelocity))
            {
                return SafeVelocity(kccVelocity, fallback);
            }

            return MathGuard.IsFinite(_lastKnownLinearVelocity)
                ? SafeVelocity(_lastKnownLinearVelocity, fallback)
                : fallback;
        }

        private Vector3 ResolveCurrentRuntimePosition(Vector3 fallback)
        {
            if (HydrodynamicKccOwnsCollision())
            {
                IPlayerRuntimeContext playerContext = _playerRuntimeContext;
                if (playerContext != null &&
                    playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                    math.all(math.isfinite(snapshot.RuntimePosition)))
                {
                    float3 runtimePosition = snapshot.RuntimePosition;
                    return SafeVelocity(new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), fallback);
                }
            }

            return _body != null ? SafeVelocity(_body.position, fallback) : fallback;
        }

        private float ResolveCurrentBodyMassKg()
        {
            if (HydrodynamicKccOwnsCollision())
                return HydrodynamicPlayerEquivalentMassKg;

            if (_body == null || !math.isfinite(_body.mass))
                return HydrodynamicPlayerEquivalentMassKg;

            return math.max(HydrodynamicMinimumEffectiveMassKg, _body.mass);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition value)
        {
            return math.isfinite(value.LocalX) &&
                   math.isfinite(value.LocalY) &&
                   math.isfinite(value.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            return TryResolveAupFromRuntimeOrigin(runtimePosition, out positionAup, out _);
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup,
            out AbsoluteUniversePosition originAup)
        {
            positionAup = default;
            originAup = default;
            float3 runtime3 = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtime3)))
                return false;

            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private void ResetWallSlideContactState()
        {
            _lastWallSlideNormal = Vector3.zero;
            _lastWallSlidePoint = Vector3.zero;
            _lastWallSlideBlockedSpeed = 0f;
            _lastWallSlideAngleDegrees = 0f;
            _lastWallSlideVelocityReduction01 = 0f;
            _lastWallSlidePhysicsFrame = -1;
            _lastWallSlideShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastWallSlideBodyBindEpoch = _bodyBindEpoch;
            _lastWallSlideIsVoxel = false;
        }

        private void ResetKccContactState()
        {
            ClearKccContactSample();
            _kccCollisionHitsHandle = default;
            _kccDebugOutputsHandle = default;
        }

        private void ClearKccContactSample()
        {
            _lastKccContactNormal = Vector3.zero;
            _lastKccContactPoint = Vector3.zero;
            _lastKccContactDistance = 0f;
            _lastKccContactPhysicsFrame = -1;
            _lastKccContactShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastKccContactBodyBindEpoch = _bodyBindEpoch;
            _lastKccContactIsVoxel = false;
            ResetWallSlideContactState();
        }

        private void RefreshKccContactState()
        {
            if (!HydrodynamicKccOwnsCollision())
            {
                ResetKccContactState();
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                ResetKccContactState();
                return;
            }

            if (!TryReadKccDebugOutput(vault, out HydrodynamicKccDebugOutputDTO debug) ||
                (debug.Flags & HydrodynamicKccMath.FlagCollision) == 0u ||
                !HydrodynamicKccMath.IsFinite(debug.CollisionNormal) ||
                math.lengthsq(debug.CollisionNormal) <= MinVectorMagnitudeSq)
            {
                ClearKccContactSample();
                return;
            }

            Vector3 normal = SafeNormal(
                new Vector3(debug.CollisionNormal.x, debug.CollisionNormal.y, debug.CollisionNormal.z),
                Vector3.up);
            Vector3 point = ResolveCurrentRuntimePosition(_body != null ? _body.position : Vector3.zero);
            float distance = math.max(0f, math.isfinite(debug.HitDistance) ? debug.HitDistance : 0f);
            bool isVoxel = false;

            if (TryReadNearestKccCollisionHit(vault, out HydrodynamicKccCollisionHitDTO hit))
            {
                normal = SafeNormal(new Vector3(hit.Normal.x, hit.Normal.y, hit.Normal.z), normal);
                point = SafeVelocity(new Vector3(hit.Point.x, hit.Point.y, hit.Point.z), point);
                distance = math.max(0f, math.isfinite(hit.Distance) ? hit.Distance : distance);
                isVoxel = (hit.Flags & HydrodynamicKccMath.HitFlagSdfSpeculative) != 0u;
            }

            _lastKccContactNormal = normal;
            _lastKccContactPoint = point;
            _lastKccContactDistance = distance;
            _lastKccContactPhysicsFrame = SystemDispatcher.CurrentFrameIndex;
            _lastKccContactShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _lastKccContactBodyBindEpoch = _bodyBindEpoch;
            _lastKccContactIsVoxel = isVoxel;

            Vector3 velocity = ResolveCurrentLinearVelocity(Vector3.zero);
            float blockedSpeed = math.max(0f, -math.dot((float3)velocity, (float3)normal));
            bool wallContact = math.abs(normal.y) < 0.65f && blockedSpeed > DenormalVelocityFlushThresholdMetersPerSecond;
            if (wallContact)
            {
                _lastWallSlideNormal = normal;
                _lastWallSlidePoint = point;
                _lastWallSlideBlockedSpeed = blockedSpeed;
                _lastWallSlideAngleDegrees = math.degrees(math.acos(math.clamp(math.abs(normal.y), 0f, 1f)));
                _lastWallSlideVelocityReduction01 = math.saturate(blockedSpeed / math.max(blockedSpeed + velocity.magnitude, 0.001f));
                _lastWallSlidePhysicsFrame = _lastKccContactPhysicsFrame;
                _lastWallSlideShiftSequence = _lastKccContactShiftSequence;
                _lastWallSlideBodyBindEpoch = _lastKccContactBodyBindEpoch;
                _lastWallSlideIsVoxel = isVoxel;
            }
            else
            {
                ResetWallSlideContactState();
            }
        }

        private bool TryReadKccDebugOutput(IDataVault vault, out HydrodynamicKccDebugOutputDTO debug)
        {
            debug = default;
            if (!EnsureKccDebugOutputHandle(vault))
                return false;

            if (!vault.TryReadOnlyHandle(in _kccDebugOutputsHandle, out NativeArray<HydrodynamicKccDebugOutputDTO>.ReadOnly debugOutputs) ||
                !debugOutputs.IsCreated ||
                debugOutputs.Length <= 0)
            {
                _kccDebugOutputsHandle = default;
                return false;
            }

            debug = debugOutputs[0];
            return true;
        }

        private bool TryReadNearestKccCollisionHit(IDataVault vault, out HydrodynamicKccCollisionHitDTO nearestHit)
        {
            nearestHit = default;
            if (!EnsureKccCollisionHitsHandle(vault))
                return false;

            if (!vault.TryReadOnlyHandle(in _kccCollisionHitsHandle, out NativeArray<HydrodynamicKccCollisionHitDTO>.ReadOnly hits) ||
                !hits.IsCreated ||
                hits.Length <= 0)
            {
                _kccCollisionHitsHandle = default;
                return false;
            }

            int hitCount = math.min(KccCollisionHitStride, hits.Length);
            float nearestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < hitCount; i++)
            {
                HydrodynamicKccCollisionHitDTO candidate = hits[i];
                if ((candidate.Flags & HydrodynamicKccMath.HitFlagValid) == 0u ||
                    !HydrodynamicKccMath.IsFinite(candidate.Normal) ||
                    math.lengthsq(candidate.Normal) <= MinVectorMagnitudeSq ||
                    !HydrodynamicKccMath.IsFinite(candidate.Point))
                {
                    continue;
                }

                float distance = math.max(0f, math.isfinite(candidate.Distance) ? candidate.Distance : 0f);
                if (found && distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestHit = candidate;
                found = true;
            }

            return found;
        }

        private bool EnsureKccDebugOutputHandle(IDataVault vault)
        {
            if (IsVaultHandle(in _kccDebugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, SystemID.Physics))
                return true;

            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuHydroKccDebugOutputs, out _kccDebugOutputsHandle) ||
                !IsVaultHandle(in _kccDebugOutputsHandle, BufferID.ShinobuHydroKccDebugOutputs, SystemID.Physics))
            {
                _kccDebugOutputsHandle = default;
                return false;
            }

            return true;
        }

        private bool EnsureKccCollisionHitsHandle(IDataVault vault)
        {
            if (IsVaultHandle(in _kccCollisionHitsHandle, BufferID.ShinobuHydroKccResolvedHits, SystemID.Physics))
                return true;

            if (vault == null ||
                !vault.TryGetGenerationHandle(BufferID.ShinobuHydroKccResolvedHits, out _kccCollisionHitsHandle) ||
                !IsVaultHandle(in _kccCollisionHitsHandle, BufferID.ShinobuHydroKccResolvedHits, SystemID.Physics))
            {
                _kccCollisionHitsHandle = default;
                return false;
            }

            return true;
        }

        private static bool IsVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID systemId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)systemId &&
                   handle.Generation != 0u;
        }

        private void TryEmitWakeSiltDecal(float fixedDeltaTime)
        {
            if (_body == null)
                return;

            float safeDeltaTime = math.max(0f, fixedDeltaTime);
            _wakeSiltEmissionCooldown = math.max(0f, _wakeSiltEmissionCooldown - safeDeltaTime);
            if (_wakeSiltEmissionCooldown > 0f)
                return;

            Vector3 velocity = ResolveCurrentLinearVelocity(Vector3.zero);
            float speedSq = velocity.sqrMagnitude;
            if (speedSq <= WakeSiltEmissionSpeedThresholdMetersPerSecondSq)
                return;

            Vector3 emitPosition = HydrodynamicKccOwnsCollision()
                ? ResolveCurrentRuntimePosition(Vector3.zero)
                : _body.worldCenterOfMass;
            float3 emitPosition3 = new float3(emitPosition.x, emitPosition.y, emitPosition.z);
            if (!math.all(math.isfinite(emitPosition3)))
                return;

            IFluidDecalPresentationSink fluidDecals = _fluidDecals;
            if (fluidDecals == null)
                return;

            if (!TryResolveAupFromRuntimeOrigin(emitPosition, out AbsoluteUniversePosition emitAup))
                return;

            float3 runtimeFromAup = emitAup.ToRuntimeFloat3();
            Vector3 aupRuntimePosition = new Vector3(runtimeFromAup.x, runtimeFromAup.y, runtimeFromAup.z);
            float approximateSpeed = ApproximateSpeedMagnitude(new float3(velocity.x, velocity.y, velocity.z));
            float intensity01 = math.saturate((approximateSpeed - WakeSiltEmissionSpeedThresholdMetersPerSecond) / 8f);
            fluidDecals.RegisterWakeSilt(aupRuntimePosition, velocity, intensity01);
            _wakeSiltEmissionCooldown = WakeSiltEmissionCooldownSeconds;
        }

        internal static Vector3 SafeVelocity(Vector3 velocity, Vector3 fallback = default)
        {
            return MathGuard.SanitizeFinite(velocity, fallback);
        }

        private static bool TryNormalizeRotation(Quaternion rotation, out Quaternion normalizedRotation)
        {
            float4 value = new float4(rotation.x, rotation.y, rotation.z, rotation.w);
            float lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || !math.isfinite(lengthSq) || lengthSq <= MinVectorMagnitudeSq)
            {
                normalizedRotation = Quaternion.identity;
                return false;
            }

            value *= math.rsqrt(math.max(lengthSq, MinVectorMagnitudeSq));
            if (value.w < 0f)
                value = -value;

            normalizedRotation = new Quaternion(value.x, value.y, value.z, value.w);
            return true;
        }

        public static float ResolveStorageBackpressureSpeedMultiplier(float debt01)
        {
            return math.max(0.2f, 1f - (math.saturate(debt01) * 0.8f));
        }

        private static float ApproximateSpeedMagnitude(float3 velocity)
        {
            float3 absolute = math.abs(velocity);
            float maxComponent = math.cmax(absolute);
            float minComponent = math.cmin(absolute);
            float midComponent = absolute.x + absolute.y + absolute.z - maxComponent - minComponent;
            return maxComponent + (midComponent * 0.375f) + (minComponent * 0.125f);
        }

        private static Vector3 SnapMillimeter(Vector3 value)
        {
            value.x = DeterministicContractMath.SnapMillimeter(value.x);
            value.y = DeterministicContractMath.SnapMillimeter(value.y);
            value.z = DeterministicContractMath.SnapMillimeter(value.z);
            return value;
        }

        public static Vector3 SafeNormal(Vector3 value, Vector3 fallback)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            if (!math.all(math.isfinite(value3)))
                return fallback;

            float sqrMagnitude = math.lengthsq(value3);
            if (!math.isfinite(sqrMagnitude) || sqrMagnitude <= MinVectorMagnitudeSq)
                return fallback;

            float inverseMagnitude = math.rsqrt(math.max(sqrMagnitude, MinVectorMagnitudeSq));
            Vector3 normalized = value * inverseMagnitude;
            return SafeVelocity(normalized, fallback);
        }

        private static Vector3 CrossVector(Vector3 a, Vector3 b)
        {
            return new Vector3(
                a.y * b.z - a.z * b.y,
                a.z * b.x - a.x * b.z,
                a.x * b.y - a.y * b.x);
        }

        private static Quaternion ResolveLookRotationNoTrig(Vector3 forward, Vector3 up)
        {
            Vector3 f = SafeNormal(forward, Vector3.forward);
            Vector3 u = SafeNormal(up, Vector3.up);
            if (math.abs(math.dot((float3)f, (float3)u)) > 0.94f)
                u = math.abs(f.y) < 0.94f ? Vector3.up : Vector3.right;

            Vector3 r = SafeNormal(CrossVector(u, f), Vector3.right);
            u = SafeNormal(CrossVector(f, r), Vector3.up);

            float m00 = r.x;
            float m01 = u.x;
            float m02 = f.x;
            float m10 = r.y;
            float m11 = u.y;
            float m12 = f.y;
            float m20 = r.z;
            float m21 = u.z;
            float m22 = f.z;
            float trace = m00 + m11 + m22;

            float4 q;
            if (trace > 0f)
                q = new float4(m21 - m12, m02 - m20, m10 - m01, 1f + trace);
            else if (m00 >= m11 && m00 >= m22)
                q = new float4(1f + m00 - m11 - m22, m01 + m10, m02 + m20, m21 - m12);
            else if (m11 > m22)
                q = new float4(m01 + m10, 1f + m11 - m00 - m22, m12 + m21, m02 - m20);
            else
                q = new float4(m02 + m20, m12 + m21, 1f + m22 - m00 - m11, m10 - m01);

            float lengthSq = math.dot(q, q);
            lengthSq = math.max(math.select(lengthSq, 1.0f, !math.isfinite(lengthSq)), 0.000001f);
            q *= math.rsqrt(math.max(lengthSq, 0.000001f));
            return new Quaternion(q.x, q.y, q.z, q.w);
        }

        internal static Vector3 ProjectVelocityOnCollisionPlane(Vector3 velocity, Vector3 hitNormal)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            Vector3 safeNormal = SafeNormal(hitNormal, Vector3.up);
            float normalVelocity = math.dot((float3)safeVelocity, (float3)safeNormal);
            Vector3 projectedVelocity = safeVelocity - (safeNormal * normalVelocity);
            return SafeVelocity(projectedVelocity, Vector3.zero);
        }

        internal static float ResolveHeavyBrineSinkMultiplier(float fluidDensityKgPerCubicMeter, float referenceSeaWaterDensityKgPerCubicMeter)
        {
            if (!math.isfinite(fluidDensityKgPerCubicMeter) ||
                !math.isfinite(referenceSeaWaterDensityKgPerCubicMeter) ||
                fluidDensityKgPerCubicMeter <= referenceSeaWaterDensityKgPerCubicMeter)
            {
                return 0f;
            }

            float densityExcess01 = math.saturate(
                (fluidDensityKgPerCubicMeter - referenceSeaWaterDensityKgPerCubicMeter) /
                math.max(1f, referenceSeaWaterDensityKgPerCubicMeter * 0.25f));
            return -math.lerp(0.35f, 0.85f, densityExcess01);
        }

        internal static Vector3 ResolveBuoyancyInversionVelocity(
            Vector3 velocity,
            bool insideHeavyBrine,
            bool thrusterActive,
            float sinkMultiplier)
        {
            Vector3 safeVelocity = SafeVelocity(velocity);
            if (!insideHeavyBrine ||
                thrusterActive ||
                safeVelocity.y >= 0f ||
                sinkMultiplier >= 0f)
            {
                return safeVelocity;
            }

            safeVelocity.y *= sinkMultiplier;
            return SafeVelocity(safeVelocity, Vector3.zero);
        }

        private void ResetDisabledProbeState()
        {
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
        }

        private void TryRegisterPostFixedTick()
        {
            if (_registeredPostFixedTick || !Application.isPlaying)
                return;

            _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterPostFixedTick()
        {
            if (!_registeredPostFixedTick)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _registeredPostFixedTick = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterMotorService()
        {
            if (_registeredMotorService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterPlayerMotorService(this);
            _registeredMotorService = ReferenceEquals(GlobalRegistry.PlayerMotor, this);
        }

        private void TryUnregisterMotorService()
        {
            if (!_registeredMotorService)
                return;

            GlobalRegistry.UnregisterPlayerMotorService(this);
            _registeredMotorService = false;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterHotSwapListener(this);
            _registeredHotSwap = true;
            _playerRuntimeContext = GlobalRegistry.Player;
            _fluidDecals = GlobalRegistry.FluidDecalPresentation;
            _physicsService = GlobalRegistry.Physics;
            _dataVault = GlobalRegistry.DataVault;
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.UnregisterHotSwapListener(this);
            _registeredHotSwap = false;
            _playerRuntimeContext = null;
            _fluidDecals = null;
            _physicsService = null;
            _dataVault = null;
        }

        private IPhysicsService ResolvePhysicsService()
        {
            return _physicsService;
        }

        private static Vector3 ResolveDominantPlanarDirection(Vector3 direction, Vector3 fallback)
        {
            if (!math.isfinite(direction.x) || !math.isfinite(direction.z))
                return fallback;

            float absX = math.abs(direction.x);
            float absZ = math.abs(direction.z);
            if (absX <= MinVectorMagnitudeSq && absZ <= MinVectorMagnitudeSq)
                return fallback;

            if (absX > absZ)
                return direction.x >= 0f ? Vector3.right : Vector3.left;

            return direction.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private static float3 ResolveDominantPlanarAxis(Vector3 direction, Vector3 fallback)
        {
            Vector3 dominant = ResolveDominantPlanarDirection(direction, fallback);
            return new float3(dominant.x, dominant.y, dominant.z);
        }

        private void DiscardKinematicRepairTargetProbe()
        {
            _kinematicRepairSnapReady = false;
            _kinematicRepairTargetProbe = default;
            _kinematicRepairSnapPoint = default;
            _kinematicRepairSnapShiftSequence = HectonFloatingOrigin.CurrentShiftSequence;
            _kinematicRepairSnapBodyBindEpoch = _bodyBindEpoch;
        }

        private Vector3 ClampTorqueByAngularAcceleration(Vector3 worldTorque, float maxAngularAcceleration)
        {
            if (_body == null || maxAngularAcceleration <= 0f)
                return worldTorque;

            Quaternion inertiaRotation = _body.rotation * _body.inertiaTensorRotation;
            Quaternion worldToInertia = Quaternion.Inverse(inertiaRotation);
            Vector3 localTorque = worldToInertia * worldTorque;
            Vector3 inertiaTensor = _body.inertiaTensor;
            localTorque.x = ClampTorqueAxis(localTorque.x, inertiaTensor.x, maxAngularAcceleration);
            localTorque.y = ClampTorqueAxis(localTorque.y, inertiaTensor.y, maxAngularAcceleration);
            localTorque.z = ClampTorqueAxis(localTorque.z, inertiaTensor.z, maxAngularAcceleration);
            return inertiaRotation * localTorque;
        }

        private static float ClampTorqueAxis(float torque, float inertiaAxis, float maxAngularAcceleration)
        {
            float safeInertia = math.max(0.0001f, inertiaAxis);
            float angularAcceleration = torque / safeInertia;
            if (!math.isfinite(angularAcceleration))
                return 0f;

            angularAcceleration = math.clamp(angularAcceleration, -maxAngularAcceleration, maxAngularAcceleration);
            return angularAcceleration * safeInertia;
        }

    }
}
