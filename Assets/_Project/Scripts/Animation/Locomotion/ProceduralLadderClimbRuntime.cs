using System;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Animation.Locomotion
{
    [DisallowMultipleComponent]
    // Registers before ladder interaction adapters can request climb setup during player interaction bootstrap.
    [DefaultExecutionOrder(-9921)]
    internal sealed class ProceduralLadderClimbRuntime : MonoBehaviour, IFastTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001ProceduralLadderClimbRuntimeSignalPushDropCount;
        private const float DefaultPcSlideSpeedMetersPerSecond = 1.35f;
        private const float StaminaDrainPerMeter = 0.18f;
        private const float SlipVelocityMetersPerSecond = -2.25f;
        private const float ShoulderWidthMeters = 0.44f;
        private const float ShoulderHeightMeters = 1.36f;
        private const float ShoulderBacksetMeters = 0.14f;
        private const float ElbowPoleMeters = 0.38f;
        private const float FastClimbStressSpeedMetersPerSecond = 1.4f;
        private const float ClimbStressOxygenDrainBonus = 0.28f;
        private const float LookDownGripReleaseDotThreshold = 0.9848077f;
        private const float HeadStabilizationSharpness = 12f;
        private const uint DefaultGripActionMask = (uint)(PlayerInputAction.Interact | PlayerInputAction.SecondaryFire);
        private const uint LegacySerializedGripActionMask = 1u << 6;
        private const byte GripMaskLeft = 1 << 0;
        private const byte GripMaskRight = 1 << 1;
        private const SystemID OwnerSystemId = SystemID.AnimationLocomotion;
        private const string BlackBoxDumpPath = "Docs/AgentLogs/Dump_LADDER_CLIMB_IK.bin";
        private const int BlackBoxDumpHeaderBytes = 24;
        private const int BlackBoxDumpEntryBytes = 128;
        private const uint BlackBoxDumpMagic = 0x4C43494Bu; // LCIK
        private const uint BlackBoxDumpVersion = 1u;
        private const string BlackBoxDumpPayloadLabel = "ladderClimbIkBlackBoxPayload";
        private const uint SolvePinInput = 1u << 0;
        private const uint SolvePinLadderAups = 1u << 1;
        private const uint SolvePinOutput = 1u << 2;
        private const uint SolvePinTelemetryRing = 1u << 3;
        private const uint SolvePinTelemetryCursor = 1u << 4;
        private static readonly ulong LadderAupMutationGuardMask = LadderMutationGuardBit(BufferID.LadderAUPs);

        [Header("IK Targets")]
        [SerializeField] private Transform leftHandIkTarget;
        [SerializeField] private Transform rightHandIkTarget;
        [SerializeField] private Transform leftElbowIkTarget;
        [SerializeField] private Transform rightElbowIkTarget;
        [SerializeField] private Transform cameraSlideTarget;

        [Header("Runtime")]
        [SerializeField] private float pcSlideSpeedMetersPerSecond = DefaultPcSlideSpeedMetersPerSecond;
        [SerializeField] private bool forceVrGripPullMode;
        [SerializeField] private uint universalGripActionMask = DefaultGripActionMask;

        private IDataVault _dataVault;
        private VaultGenerationHandle<LadderClimbIkInput> _inputHandle;
        private VaultGenerationHandle<LadderClimbIkOutput> _outputHandle;
        private VaultGenerationHandle<AbsoluteUniversePosition> _ladderAupHandle;
        private VaultGenerationHandle<LadderClimbTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;

        private JobHandle _solveHandle;
        private IDataVault _solveBufferPinVault;
        private uint _solveBufferPinMask;
        private bool _solveScheduled;
        private bool _registeredFastTick;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _serviceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _active;
        private bool _pendingFinish;
        private bool _pendingSlip;
        private bool _matchRotation;
        private bool _vrGripRequired;
        private bool _cameraSlidePresentationActive;
        private Transform _playerRoot;
        private Transform _ladderTransform;
        private Transform _entryPoint;
        private Transform _exitPoint;
        private IPlayerMovementForceSink _cachedMovementForceSink;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IPlayerMovementForceSink _movementForceSink;
        private IPlayerRuntimeContext _playerContext;
        private float3 _ladderUp;
        private float3 _ladderForward;
        private float _climbDirection;
        private float _climbProgressMeters;
        private float _climbHeightMeters;
        private float _stamina01;
        private float _pendingGripPullMeters;
        private Vector3 _cameraSlideEntryPosition;
        private Vector3 _cameraSlideExitPosition;
        private Quaternion _headStabilizedRotation = Quaternion.identity;
        private byte _pendingGripMask;
        private byte _lastResolvedGripMask;
        private int _lastLeftRung = -1;
        private int _lastRightRung = -1;
        private uint _lastConsumedInputSequence;
        private int _lastPublishedClimbFrame = -1;
        private int _lastPublishedClimbProgressMillimeters;
        private bool _hasConsumedInputSequence;
        private bool _currentInputGripHeld;
        private bool _hasPublishedClimbState;
        private byte _lastPublishedClimbState;
        private byte _lastPublishedClimbFlags;
        private bool _headStabilizationInitialized;

        /// <summary>
        /// Ensures a live ProceduralLadderClimbRuntime is registered for climb requests.
        /// Player builds must construct here: GUID has zero scene/prefab hits and ClimbableLadder
        /// routes exclusively through TryBeginClimb → EnsureRuntimeInstance.
        /// </summary>
        internal static ProceduralLadderClimbRuntime EnsureRuntimeInstance()
        {
            ProceduralLadderClimbRuntime registered = GlobalRegistry.ProceduralLadderClimbRuntime;
            if (IsLadderClimbRuntimeUsable(registered))
                return registered;

            if (!ReferenceEquals(registered, null))
                GlobalRegistry.ClearProceduralLadderClimbRuntime(registered);

            if (!Application.isPlaying)
                return null;

            // Player-build construction path (not editor/dev-only): zero authored scene/prefab hits.
            GameObject runtimeRoot = new GameObject("[ProceduralLadderClimbRuntime]"); // COLD ALLOC: GameObject[1] - scene-local animation locomotion runtime root - owner: ProceduralLadderClimbRuntime
            return runtimeRoot.AddComponent<ProceduralLadderClimbRuntime>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapRuntimeAfterSceneLoad()
        {
            if (!Application.isPlaying)
                return;

            EnsureRuntimeInstance();
        }

        internal static bool TryBeginClimb(
            Transform ladderTransform,
            Transform entryPoint,
            Transform exitPoint,
            Transform player,
            bool goingUp,
            bool matchRotation)
        {
            ProceduralLadderClimbRuntime runtime = EnsureRuntimeInstance();
            return runtime != null &&
                   runtime.TryBeginClimbInstance(ladderTransform, entryPoint, exitPoint, player, goingUp, matchRotation);
        }

        internal void ConfigureIkTargets(
            Transform leftHand,
            Transform rightHand,
            Transform leftElbow,
            Transform rightElbow,
            Transform slideTarget)
        {
            leftHandIkTarget = leftHand;
            rightHandIkTarget = rightHand;
            leftElbowIkTarget = leftElbow;
            rightElbowIkTarget = rightElbow;
            cameraSlideTarget = slideTarget;
        }

        internal void SetVrGripPullMode(bool required)
        {
            forceVrGripPullMode = required;
        }

        internal void SubmitUniversalInputState(uint actionsBitmask, float leftHandDeltaAlongLadderMeters, float rightHandDeltaAlongLadderMeters)
        {
            if ((actionsBitmask & ResolveGripActionMask()) == 0u)
            {
                _currentInputGripHeld = false;
                _pendingGripPullMeters = 0f;
                _pendingGripMask = 0;
                return;
            }

            _currentInputGripHeld = true;
            SubmitGripPullDelta(leftHandDeltaAlongLadderMeters, rightHandDeltaAlongLadderMeters, (byte)(GripMaskLeft | GripMaskRight));
        }

        internal void SubmitGripPullDelta(float leftHandDeltaAlongLadderMeters, float rightHandDeltaAlongLadderMeters, byte gripMask)
        {
            if (!_active)
                return;

            if (gripMask == 0)
            {
                _currentInputGripHeld = false;
                _pendingGripPullMeters = 0f;
                _pendingGripMask = 0;
                return;
            }

            _currentInputGripHeld = true;
            float pullMeters = 0f;
            int samples = 0;
            if ((gripMask & GripMaskLeft) != 0 && IsFinite(leftHandDeltaAlongLadderMeters))
            {
                pullMeters += leftHandDeltaAlongLadderMeters;
                samples++;
            }

            if ((gripMask & GripMaskRight) != 0 && IsFinite(rightHandDeltaAlongLadderMeters))
            {
                pullMeters += rightHandDeltaAlongLadderMeters;
                samples++;
            }

            if (samples <= 0)
                return;

            float averagedPull = pullMeters * math.rcp((float)math.max(1, samples));
            _pendingGripPullMeters = math.clamp(_pendingGripPullMeters + math.clamp(averagedPull, -0.45f, 0.45f), -0.45f, 0.45f);
            _pendingGripMask |= gripMask;
        }

        private void OnEnable()
        {
            if (!TryRegisterService())
                return;

            CacheColdDependencies();
            TryRegisterHotSwapListener();
            OpenOrAcquireVaultBuffersForOwnerRoute();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            StopClimb(false, false);
            CompleteOutstandingJobForBarrier();
            UnregisterTickables();
            TryUnregisterHotSwapListener();
            ReleaseVaultHandles();
            ClearVaultHandles();
            ClearCachedServices();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
                return;

            StopClimb(false, false);
            CompleteOutstandingJobForBarrier();
            UnregisterTickables();
            TryUnregisterHotSwapListener();
            ReleaseVaultHandles();
            ClearVaultHandles();
            ClearCachedServices();
            TryUnregisterService();
        }

        public void FastTick(float deltaTime)
        {
            if (!_active)
                return;

            if (_solveScheduled)
                return;

            float safeDeltaTime = SanitizeDeltaTime(deltaTime);
            float previousProgress = _climbProgressMeters;
            ConsumeInputStateSignals();
            float progressDelta = ResolveProgressDelta(safeDeltaTime);
            _climbProgressMeters = math.clamp(_climbProgressMeters + progressDelta, 0f, _climbHeightMeters);
            float appliedProgressDelta = _climbProgressMeters - previousProgress;
            DrainStamina(appliedProgressDelta);

            if ((_stamina01 <= 0.0001f && math.abs(appliedProgressDelta) > 0.0001f) ||
                ShouldDropFromLookDownGripRelease())
            {
                _pendingSlip = true;
                _pendingFinish = true;
            }

            PublishClimbPhysiology(math.abs(appliedProgressDelta), safeDeltaTime);

            if ((_climbDirection > 0f && _climbProgressMeters >= _climbHeightMeters - 0.0001f) ||
                (_climbDirection < 0f && _climbProgressMeters <= 0.0001f))
            {
                _pendingFinish = true;
            }

            ApplyPresentationDelta(appliedProgressDelta, safeDeltaTime);
            PublishClimbState(false);
            ScheduleSolve();
        }

        public void LateFrameTick()
        {
            if (!_solveScheduled)
            {
                if (_pendingFinish)
                    StopClimb(true, _pendingSlip);
                return;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _solveHandle))
                return;

            _solveScheduled = false;
            bool hasOutput;
            LadderClimbIkOutput output;
            try
            {
                hasOutput = TryReadOutput(out output);
            }
            finally
            {
                ReleaseSolveBufferPins();
            }

            if (!hasOutput)
            {
                StopClimb(false, true);
                return;
            }

            if (!IsFinite(output.LeftHandTarget) ||
                !IsFinite(output.RightHandTarget) ||
                !IsFinite(output.LeftElbowTarget) ||
                !IsFinite(output.RightElbowTarget))
            {
                DumpBlackBox();
                StopClimb(false, true);
                return;
            }

            ApplyIkTargets(in output);
            EmitRungContactHaptics(in output);
            PublishClimbState(_pendingSlip);

            if (_pendingFinish)
                StopClimb(true, _pendingSlip);
        }

        private bool TryBeginClimbInstance(
            Transform ladderTransform,
            Transform entryPoint,
            Transform exitPoint,
            Transform player,
            bool goingUp,
            bool matchRotation)
        {
            if (ladderTransform == null || entryPoint == null || exitPoint == null || player == null)
                return false;

            if (_active || _pendingFinish || _solveScheduled)
                return false;

            if (!CacheVaultDependency() ||
                !OpenOrAcquireVaultBuffersForOwnerRoute())
            {
                return false;
            }

            CompleteOutstandingJobForBarrier();
            _playerRoot = player;
            _ladderTransform = ladderTransform;
            _entryPoint = entryPoint;
            _exitPoint = exitPoint;
            _matchRotation = matchRotation;
            _movementForceSink = _cachedMovementForceSink;
            _playerContext = _cachedPlayerContext;
            _vrGripRequired = forceVrGripPullMode || UnityEngine.XR.XRSettings.enabled;
            _cameraSlidePresentationActive = !_vrGripRequired;
            _climbDirection = goingUp ? 1f : -1f;
            _stamina01 = 1f;
            _pendingGripPullMeters = 0f;
            _pendingGripMask = 0;
            _lastResolvedGripMask = 0;
            _lastConsumedInputSequence = 0u;
            _hasConsumedInputSequence = false;
            _currentInputGripHeld = false;
            _pendingFinish = false;
            _pendingSlip = false;
            _lastLeftRung = -1;
            _lastRightRung = -1;
            ResetClimbStatePublishCache();

            ResolveLadderFrame(entryPoint.position, exitPoint.position, ladderTransform);
            InitializePresentationAnchors(entryPoint.position, exitPoint.position);
            _climbProgressMeters = goingUp ? 0f : _climbHeightMeters;
            if (!TryResolveEntryPointAup(entryPoint.position, out AbsoluteUniversePosition entryAup) ||
                !TryWriteLadderAup(entryAup))
                return false;

            if (!TryRegisterTickables())
            {
                UnregisterTickables();
                _playerRoot = null;
                _ladderTransform = null;
                _entryPoint = null;
                _exitPoint = null;
                _movementForceSink = null;
                _playerContext = null;
                return false;
            }

            _active = true;
            PublishClimbState(false);
            ScheduleSolve();
            return true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService is IDataVault currentVault ? currentVault : null, ensureBuffers: true);
                    break;
                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    _cachedMovementForceSink = currentService as IPlayerMovementForceSink;
                    if (_active)
                        _movementForceSink = _cachedMovementForceSink;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (_active)
                        _playerContext = _cachedPlayerContext;
                    break;
            }
        }

        private void CacheColdDependencies()
        {
            RebindDataVault(GlobalRegistry.DataVault, ensureBuffers: false);
            _cachedMovementForceSink = GlobalRegistry.PlayerMovementContracts as IPlayerMovementForceSink;
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
        }

        private bool CacheVaultDependency()
        {
            return _dataVault != null;
        }

        private void RebindDataVault(IDataVault current, bool ensureBuffers)
        {
            if (ReferenceEquals(_dataVault, current))
                return;

            CompleteOutstandingJobForBarrier();
            ReleaseVaultHandles();
            ClearVaultHandles();
            _dataVault = current;
            if (ensureBuffers && _dataVault != null && isActiveAndEnabled)
            {
                OpenOrAcquireVaultBuffersForOwnerRoute();
            }
        }

        private bool OpenOrAcquireVaultBuffersForOwnerRoute()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return OpenOrAcquireVaultBufferForOwnerRoute(
                       vault,
                       BufferID.LadderClimbIkInput,
                       1,
                       ref _inputHandle,
                       out _) &&
                   OpenOrAcquireVaultBufferForOwnerRoute(
                       vault,
                       BufferID.LadderClimbIkOutput,
                       1,
                       ref _outputHandle,
                       out _) &&
                   OpenOrAcquireVaultBufferForOwnerRoute(
                       vault,
                       BufferID.LadderAUPs,
                       LadderClimbIkConstants.MaxActiveLadders,
                       ref _ladderAupHandle,
                       out _) &&
                   OpenOrAcquireVaultBufferForOwnerRoute(
                       vault,
                       BufferID.LadderClimbIkTelemetryRing,
                       LadderClimbIkConstants.BlackBoxFrameCapacity,
                       ref _telemetryRingHandle,
                       out _) &&
                   OpenOrAcquireVaultBufferForOwnerRoute(
                       vault,
                       BufferID.LadderClimbIkTelemetryCursor,
                       LadderClimbIkConstants.TelemetryCursorElementCount,
                       ref _telemetryCursorHandle,
                       out _);
        }

        private bool TryResolveVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            return TryResolveVaultBuffer(_dataVault, in handle, expectedBufferId, requiredLength, out buffer);
        }

        private static bool TryResolveVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   IsLadderVaultHandle(in handle, expectedBufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool OpenOrAcquireVaultBufferForOwnerRoute<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null)
                return false;

            if (IsLadderVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!IsLadderVaultHandle(in acquired, bufferId) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                if (IsLadderVaultHandle(in acquired, bufferId))
                    vault.ReleaseBuffer(in acquired);
                return false;
            }

            handle = acquired;
            return true;
        }

        private bool TryResolveVaultViews(out LadderClimbIkVaultViews views)
        {
            return TryResolveVaultViews(_dataVault, out views);
        }

        private bool TryResolveVaultViews(IDataVault vault, out LadderClimbIkVaultViews views)
        {
            views = default;

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool hasInputs = TryResolveVaultBuffer(vault, in _inputHandle, BufferID.LadderClimbIkInput, 1, out NativeArray<LadderClimbIkInput> inputs);
            bool hasOutputs = TryResolveVaultBuffer(vault, in _outputHandle, BufferID.LadderClimbIkOutput, 1, out NativeArray<LadderClimbIkOutput> outputs);
            bool hasLadderAups = TryResolveVaultBuffer(vault, in _ladderAupHandle, BufferID.LadderAUPs, LadderClimbIkConstants.MaxActiveLadders, out NativeArray<AbsoluteUniversePosition> ladderAups);
            bool hasTelemetryRing = TryResolveVaultBuffer(vault, in _telemetryRingHandle, BufferID.LadderClimbIkTelemetryRing, LadderClimbIkConstants.BlackBoxFrameCapacity, out NativeArray<LadderClimbTelemetryEntry> telemetryRing);
            bool hasTelemetryCursor = TryResolveVaultBuffer(vault, in _telemetryCursorHandle, BufferID.LadderClimbIkTelemetryCursor, LadderClimbIkConstants.TelemetryCursorElementCount, out NativeArray<int> telemetryCursor);

            views = new LadderClimbIkVaultViews
            {
                Inputs = hasInputs ? inputs : default,
                Outputs = hasOutputs ? outputs : default,
                LadderAups = hasLadderAups ? ladderAups : default,
                TelemetryRing = hasTelemetryRing ? telemetryRing : default,
                TelemetryCursor = hasTelemetryCursor ? telemetryCursor : default
            };

            return views.HasSolveCapacity;
        }

        private bool TryReadOutput(out LadderClimbIkOutput output)
        {
            output = default;
            if (!TryResolveVaultViews(out LadderClimbIkVaultViews views) || !views.HasOutput)
                return false;

            output = views.Outputs[0];
            return true;
        }

        private bool TryWriteLadderAup(AbsoluteUniversePosition aup)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsLadderVaultHandle(in _ladderAupHandle, BufferID.LadderAUPs) ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(LadderAupMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryResolveVaultBuffer(vault, in _ladderAupHandle, BufferID.LadderAUPs, 1, out NativeArray<AbsoluteUniversePosition> ladderAups))
                    return false;

                ladderAups[0] = aup;
                return true;
            }
            finally
            {
                vault.ReleaseMutationGuard(LadderAupMutationGuardMask);
            }
        }

        private bool TryResolveEntryPointAup(Vector3 entryRuntimePosition, out AbsoluteUniversePosition entryAup)
        {
            entryAup = default;
            float3 entryRuntime = (float3)(entryRuntimePosition);
            if (!IsFinite(entryRuntime))
                return false;

            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite() &&
                IsFinite(snapshot.RuntimePosition))
            {
                return TryOffsetAupByRuntimeDelta(
                    in snapshot.Aup,
                    snapshot.RuntimePosition,
                    entryRuntime,
                    out entryAup);
            }

            if (!playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !movementState.PredictedAup.IsFinite() ||
                !IsFinite(movementState.WorldPosition))
            {
                return false;
            }

            AbsoluteUniversePosition playerAup = movementState.PredictedAup;
            float3 playerRuntime = movementState.WorldPosition;
            return TryOffsetAupByRuntimeDelta(
                in playerAup,
                playerRuntime,
                entryRuntime,
                out entryAup);
        }

        private static bool TryOffsetAupByRuntimeDelta(
            in AbsoluteUniversePosition referenceAup,
            float3 referenceRuntimePosition,
            float3 targetRuntimePosition,
            out AbsoluteUniversePosition resolvedAup)
        {
            resolvedAup = default;
            if (!IsFinite(referenceRuntimePosition) || !IsFinite(targetRuntimePosition))
                return false;

            double3 localDelta = new double3(
                (double)targetRuntimePosition.x - referenceRuntimePosition.x,
                (double)targetRuntimePosition.y - referenceRuntimePosition.y,
                (double)targetRuntimePosition.z - referenceRuntimePosition.z);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            resolvedAup = AbsoluteUniversePosition.OffsetMeters(in referenceAup, localDelta);
            return resolvedAup.IsFinite();
        }

        private bool TryReadLadderAup(out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!TryResolveVaultViews(out LadderClimbIkVaultViews views) || !views.HasLadderAup)
                return false;

            aup = views.LadderAups[0];
            return true;
        }

        private void ClearVaultHandles()
        {
            _inputHandle = default;
            _outputHandle = default;
            _ladderAupHandle = default;
            _telemetryRingHandle = default;
            _telemetryCursorHandle = default;
            _dataVault = null;
        }

        private void ClearCachedServices()
        {
            _cachedMovementForceSink = null;
            _cachedPlayerContext = null;
            _movementForceSink = null;
            _playerContext = null;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseSolveBufferPins();
            ReleaseVaultHandle(vault, BufferID.LadderClimbIkInput, ref _inputHandle);
            ReleaseVaultHandle(vault, BufferID.LadderClimbIkOutput, ref _outputHandle);
            ReleaseVaultHandle(vault, BufferID.LadderAUPs, ref _ladderAupHandle);
            ReleaseVaultHandle(vault, BufferID.LadderClimbIkTelemetryRing, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, BufferID.LadderClimbIkTelemetryCursor, ref _telemetryCursorHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, BufferID expectedBufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (!IsLadderVaultHandle(in handle, expectedBufferId))
            {
                handle = default;
                return;
            }

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private bool TryLockSolveBuffersAndResolveViews(out LadderClimbIkVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _solveBufferPinMask != 0u ||
                !IsLadderVaultHandle(in _inputHandle, BufferID.LadderClimbIkInput) ||
                !IsLadderVaultHandle(in _ladderAupHandle, BufferID.LadderAUPs) ||
                !IsLadderVaultHandle(in _outputHandle, BufferID.LadderClimbIkOutput) ||
                !IsLadderVaultHandle(in _telemetryRingHandle, BufferID.LadderClimbIkTelemetryRing) ||
                !IsLadderVaultHandle(in _telemetryCursorHandle, BufferID.LadderClimbIkTelemetryCursor))
            {
                return false;
            }

            _solveBufferPinVault = vault;
            bool resolved = false;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !TryLockSolveBuffer(BufferID.LadderClimbIkInput, SolvePinInput) ||
                    !TryLockSolveBuffer(BufferID.LadderAUPs, SolvePinLadderAups) ||
                    !TryLockSolveBuffer(BufferID.LadderClimbIkOutput, SolvePinOutput) ||
                    !TryLockSolveBuffer(BufferID.LadderClimbIkTelemetryRing, SolvePinTelemetryRing) ||
                    !TryLockSolveBuffer(BufferID.LadderClimbIkTelemetryCursor, SolvePinTelemetryCursor))
                {
                    return false;
                }

                if (!TryResolveVaultViews(vault, out views))
                    return false;

                resolved = true;
                return true;
            }
            finally
            {
                if (!resolved)
                    ReleaseSolveBufferPins();
            }
        }

        private bool TryLockSolveBuffer(BufferID bufferId, uint pinBit)
        {
            IDataVault vault = _solveBufferPinVault;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if ((_solveBufferPinMask & pinBit) != 0u)
                return true;

            if (!vault.TryLockBuffer(bufferId, OwnerSystemId))
                return false;

            _solveBufferPinMask |= pinBit;
            return true;
        }

        private void ReleaseSolveBufferPins()
        {
            IDataVault vault = _solveBufferPinVault;
            uint mask = _solveBufferPinMask;
            _solveBufferPinVault = null;
            _solveBufferPinMask = 0u;
            if (vault == null || mask == 0u)
                return;

            TryUnlockSolvePin(vault, mask, SolvePinTelemetryCursor, BufferID.LadderClimbIkTelemetryCursor);
            TryUnlockSolvePin(vault, mask, SolvePinTelemetryRing, BufferID.LadderClimbIkTelemetryRing);
            TryUnlockSolvePin(vault, mask, SolvePinOutput, BufferID.LadderClimbIkOutput);
            TryUnlockSolvePin(vault, mask, SolvePinLadderAups, BufferID.LadderAUPs);
            TryUnlockSolvePin(vault, mask, SolvePinInput, BufferID.LadderClimbIkInput);
        }

        private static void TryUnlockSolvePin(IDataVault vault, uint mask, uint pinBit, BufferID bufferId)
        {
            if ((mask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystemId);
        }

        private static bool IsLadderVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static ulong LadderMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryRegisterTickables()
        {
            if (!_registeredFastTick)
                _registeredFastTick = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);

            return _registeredFastTick && _registeredLateFrame;
        }

        private void UnregisterTickables()
        {
            if (_registeredFastTick)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredFastTick = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterProceduralLadderClimbRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ProceduralLadderClimbRuntime, this);
            if (_serviceRegistered)
                _runtimeOwnerAborted = false;
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.ClearProceduralLadderClimbRuntime(this);
            _serviceRegistered = false;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (!Application.isPlaying)
                return false;

            ProceduralLadderClimbRuntime registered = GlobalRegistry.ProceduralLadderClimbRuntime;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsLadderClimbRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.ClearProceduralLadderClimbRuntime(registered);
            return false;
        }

        private static bool IsLadderClimbRuntimeUsable(ProceduralLadderClimbRuntime runtime)
        {
            return !ReferenceEquals(runtime, null) &&
                   runtime != null &&
                   runtime._serviceRegistered &&
                   runtime.isActiveAndEnabled;
        }

        private void ScheduleSolve()
        {
            if (!_active)
            {
                return;
            }

            float3 playerRoot = SanitizeFinite((float3)(_playerRoot.position), float3.zero);
            BuildShoulders(playerRoot, out float3 leftShoulder, out float3 rightShoulder, out float3 leftPole, out float3 rightPole);
            uint flags = LadderClimbIkConstants.FlagActive;
            if (_cameraSlidePresentationActive)
                flags |= LadderClimbIkConstants.FlagCameraSlideFake;
            if (_vrGripRequired)
                flags |= LadderClimbIkConstants.FlagVrGrip;
            if (_pendingSlip)
                flags |= LadderClimbIkConstants.FlagSlip;

            if (!TryLockSolveBuffersAndResolveViews(out LadderClimbIkVaultViews views))
                return;

            bool scheduled = false;
            try
            {
                views.Inputs[0] = new LadderClimbIkInput
                {
                    PlayerRoot = playerRoot,
                    LadderUp = _ladderUp,
                    LadderForward = _ladderForward,
                    LeftShoulder = leftShoulder,
                    RightShoulder = rightShoulder,
                    LeftPole = leftPole,
                    RightPole = rightPole,
                    ProgressMeters = _climbProgressMeters,
                    LadderHeightMeters = _climbHeightMeters,
                    RungSpacingMeters = LadderClimbIkConstants.DefaultRungSpacingMeters,
                    UpperArmMeters = LadderClimbIkConstants.DefaultUpperArmMeters,
                    LowerArmMeters = LadderClimbIkConstants.DefaultLowerArmMeters,
                    Stamina01 = _stamina01,
                    LadderIndex = 0,
                    Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                    Flags = flags
                };

                _solveHandle = new LadderClimbIkSolveJob
                {
                    Inputs = views.Inputs,
                    LadderAups = views.LadderAups,
                    Outputs = views.Outputs,
                    TelemetryRing = views.TelemetryRing,
                    TelemetryCursor = views.TelemetryCursor,
                    CommittedOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble
                }.Schedule();

                _solveScheduled = true;
                scheduled = true;
                H8Memory.RegisterActiveJob(OwnerSystemId, _solveHandle);
                JobHandle.ScheduleBatchedJobs();
            }
            finally
            {
                if (!scheduled)
                    ReleaseSolveBufferPins();
            }
        }

        private float ResolveProgressDelta(float deltaTime)
        {
            if (_vrGripRequired)
            {
                byte gripMask = _pendingGripMask;
                bool hasGrip = gripMask != 0 || _currentInputGripHeld;
                float pull = gripMask != 0 ? -_pendingGripPullMeters : 0f;
                _lastResolvedGripMask = hasGrip
                    ? (gripMask != 0 ? gripMask : (byte)(GripMaskLeft | GripMaskRight))
                    : (byte)0;
                _pendingGripPullMeters = 0f;
                _pendingGripMask = 0;
                return math.clamp(pull, -0.35f, 0.35f);
            }

            _lastResolvedGripMask = (byte)(GripMaskLeft | GripMaskRight);
            float speed = SanitizePositive(pcSlideSpeedMetersPerSecond, DefaultPcSlideSpeedMetersPerSecond);
            return _climbDirection * speed * deltaTime;
        }

        private void ConsumeInputStateSignals()
        {
            if (!_vrGripRequired)
                return;

            ReadOnlySpan<InputStateSignal> signals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            uint latestSequence = _lastConsumedInputSequence;
            uint latestActions = 0u;
            bool hasNewSignal = false;
            for (int i = 0; i < signals.Length; i++)
            {
                InputStateSignal signal = signals[i];
                uint sequence = signal.State.Sequence;
                if (_hasConsumedInputSequence && sequence == _lastConsumedInputSequence)
                    continue;

                latestSequence = sequence;
                latestActions = signal.State.ButtonsBitmask;
                hasNewSignal = true;
            }

            if (!hasNewSignal)
                return;

            _lastConsumedInputSequence = latestSequence;
            _hasConsumedInputSequence = true;
            if ((latestActions & ResolveGripActionMask()) != 0u)
            {
                _currentInputGripHeld = true;
                return;
            }

            _currentInputGripHeld = false;
            _pendingGripPullMeters = 0f;
            _pendingGripMask = 0;
        }

        private void DrainStamina(float appliedProgressDelta)
        {
            float drain = math.abs(appliedProgressDelta) * StaminaDrainPerMeter;
            _stamina01 = math.max(0f, _stamina01 - drain);
        }

        private void ApplyPresentationDelta(float appliedProgressDelta, float deltaTime)
        {
            if (math.abs(appliedProgressDelta) <= 0.000001f || !IsFinite(appliedProgressDelta))
                return;

            float invDeltaTime = math.rcp(math.max(deltaTime, 0.0001f));
            float3 delta = SanitizeFinite(_ladderUp * appliedProgressDelta, float3.zero);
            Vector3 velocityDelta = ToVector3(delta * invDeltaTime);
            if (_movementForceSink != null)
            {
                _movementForceSink.QueueExternalVelocityChange(velocityDelta);
                if (_cameraSlidePresentationActive)
                    ApplyCameraSlidePresentationFake(deltaTime);
                return;
            }

            if (_cameraSlidePresentationActive)
            {
                ApplyCameraSlidePresentationFake(deltaTime);
                return;
            }

            if (cameraSlideTarget != null)
                cameraSlideTarget.Translate(ToVector3(delta), Space.World);
        }

        private void ApplyCameraSlidePresentationFake(float deltaTime)
        {
            if (cameraSlideTarget == null)
                return;

            float progress01 = _climbHeightMeters > 0.0001f
                ? math.saturate(_climbProgressMeters * math.rcp(_climbHeightMeters))
                : 0f;
            cameraSlideTarget.position = Vector3.Lerp(_cameraSlideEntryPosition, _cameraSlideExitPosition, progress01);
            ApplyHeadStabilization(deltaTime);
        }

        private void ApplyHeadStabilization(float deltaTime)
        {
            if (_vrGripRequired || cameraSlideTarget == null)
                return;

            if (!_headStabilizationInitialized)
            {
                _headStabilizedRotation = cameraSlideTarget.rotation;
                _headStabilizationInitialized = true;
            }

            float blend = math.saturate(deltaTime * HeadStabilizationSharpness);
            _headStabilizedRotation = CinematicMath.FastNlerp(
                _headStabilizedRotation,
                ToVector3(_ladderForward),
                blend,
                ToVector3(_ladderUp));
            cameraSlideTarget.rotation = _headStabilizedRotation;
        }

        private void PublishClimbPhysiology(float climbedMeters, float deltaTime)
        {
            float speed = climbedMeters * math.rcp(math.max(deltaTime, 0.0001f));
            float safeSpeed = math.isfinite(speed) ? speed : 0f;
            float stress01 = math.saturate(safeSpeed * math.rcp(FastClimbStressSpeedMetersPerSecond));
            if (stress01 <= 0.0001f && !_pendingSlip)
                return;

            if (_pendingSlip)
                stress01 = math.max(stress01, 0.65f);

            byte flags = (byte)(PlayerStateSignal.FlagActive | PlayerStateSignal.FlagClimbing);
            if (_vrGripRequired)
                flags |= PlayerStateSignal.FlagVrGrip;
            if (_pendingSlip)
                flags |= PlayerStateSignal.FlagLadderSlip;

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            float oxygenDrainScale = 1f + stress01 * ClimbStressOxygenDrainBonus;
            PlayerStressSignal stress = new PlayerStressSignal
            {
                Stress01 = stress01,
                OxygenDrainScale = oxygenDrainScale,
                AggressionScale = 1f,
                Frame = frame,
                Cause = PlayerStateSignal.StateClimbing,
                Flags = flags
            };
            SignalBus<PlayerStressSignal>.TryPushTracked(in stress, ref s_x001ProceduralLadderClimbRuntimeSignalPushDropCount);
        }

        private bool ShouldDropFromLookDownGripRelease()
        {
            if (!_vrGripRequired || _lastResolvedGripMask != 0)
                return false;

            if (!TryResolveLookForward(out float3 lookForward))
                return false;

            float3 forward = NormalizeSafe(lookForward, _ladderForward);
            return math.dot(forward, -_ladderUp) >= LookDownGripReleaseDotThreshold;
        }

        private bool TryResolveLookForward(out float3 lookForward)
        {
            IPlayerRuntimeContext playerContext = _playerContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                IsFinite(snapshot.Forward))
            {
                lookForward = snapshot.Forward;
                return true;
            }

            if (cameraSlideTarget != null)
            {
                lookForward = (float3)(cameraSlideTarget.forward);
                return IsFinite(lookForward);
            }

            if (_playerRoot != null)
            {
                lookForward = (float3)(_playerRoot.forward);
                return IsFinite(lookForward);
            }

            lookForward = default;
            return false;
        }

        private void ApplyIkTargets(in LadderClimbIkOutput output)
        {
            SetTargetPosition(leftHandIkTarget, output.LeftHandTarget);
            SetTargetPosition(rightHandIkTarget, output.RightHandTarget);
            SetTargetPosition(leftElbowIkTarget, output.LeftElbowTarget);
            SetTargetPosition(rightElbowIkTarget, output.RightElbowTarget);
        }

        private void EmitRungContactHaptics(in LadderClimbIkOutput output)
        {
            bool leftChanged = false;
            bool rightChanged = false;

            if (output.LeftRungIndex != _lastLeftRung)
            {
                _lastLeftRung = output.LeftRungIndex;
                leftChanged = true;
            }

            if (output.RightRungIndex != _lastRightRung)
            {
                _lastRightRung = output.RightRungIndex;
                rightChanged = true;
            }

            if (!leftChanged && !rightChanged)
                return;

            float intensity = leftChanged && rightChanged
                ? 0.55f
                : (leftChanged ? 0.45f : 0.4f);
            EmitHapticThud(intensity);
        }

        private static void EmitHapticThud(float intensity01)
        {
            HapticRequest request = new HapticRequest
            {
                Intensity01 = math.saturate(intensity01),
                DurationSeconds = 0.045f,
                Frequency01 = 0.62f,
                SourceHash = LadderClimbIkConstants.SourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Channel = HapticRequest.ChannelLightThud,
                Flags = HapticRequest.FlagLightThud
            };
            SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001ProceduralLadderClimbRuntimeSignalPushDropCount);
        }

        private void PublishClimbState(bool slip)
        {
            if (!TryReadLadderAup(out AbsoluteUniversePosition ladderAup))
                return;

            AbsoluteUniversePosition currentClimbAup = ResolveCurrentClimbAup(in ladderAup);
            bool climbing = _active && !slip;
            bool terminalSlip = slip;
            byte flags = PlayerStateSignal.FlagAupShiftSafe;
            if (climbing)
                flags |= PlayerStateSignal.FlagActive;
            if (climbing || terminalSlip)
                flags |= PlayerStateSignal.FlagClimbing;
            if (climbing || terminalSlip)
            {
                if (_vrGripRequired)
                    flags |= PlayerStateSignal.FlagVrGrip;
                if (_cameraSlidePresentationActive)
                    flags |= PlayerStateSignal.FlagLowTierCameraSlide;
            }

            if (terminalSlip)
                flags |= PlayerStateSignal.FlagLadderSlip;

            float intensity01 = _climbHeightMeters > 0.0001f
                ? math.saturate(_climbProgressMeters * math.rcp(_climbHeightMeters))
                : 0f;
            byte state = climbing || terminalSlip ? PlayerStateSignal.StateClimbing : PlayerStateSignal.StateNone;
            int frame = SystemDispatcher.CurrentFrameIndex;
            int progressMillimeters = QuantizeProgressMillimeters();
            if (_hasPublishedClimbState &&
                _lastPublishedClimbFrame == frame &&
                _lastPublishedClimbState == state &&
                _lastPublishedClimbFlags == flags &&
                _lastPublishedClimbProgressMillimeters == progressMillimeters)
            {
                return;
            }

            PlayerStateSignal signal = new PlayerStateSignal
            {
                PositionAup = currentClimbAup,
                Intensity01 = intensity01,
                SourceHash = LadderClimbIkConstants.SourceHash,
                Frame = (uint)frame,
                State = state,
                Flags = flags
            };
            SignalBus<PlayerStateSignal>.TryPushTracked(in signal, ref s_x001ProceduralLadderClimbRuntimeSignalPushDropCount);
            _hasPublishedClimbState = true;
            _lastPublishedClimbFrame = frame;
            _lastPublishedClimbState = state;
            _lastPublishedClimbFlags = flags;
            _lastPublishedClimbProgressMillimeters = progressMillimeters;
        }

        private int QuantizeProgressMillimeters()
        {
            float safeProgress = math.clamp(
                SanitizeFinite(_climbProgressMeters, 0f),
                0f,
                SanitizePositive(_climbHeightMeters, LadderClimbIkConstants.DefaultRungSpacingMeters));
            return (int)math.round(safeProgress * 1000f);
        }

        private void ResetClimbStatePublishCache()
        {
            _hasPublishedClimbState = false;
            _lastPublishedClimbFrame = -1;
            _lastPublishedClimbState = PlayerStateSignal.StateNone;
            _lastPublishedClimbFlags = 0;
            _lastPublishedClimbProgressMillimeters = 0;
        }

        private AbsoluteUniversePosition ResolveCurrentClimbAup(in AbsoluteUniversePosition ladderAup)
        {
            float safeProgress = math.clamp(
                SanitizeFinite(_climbProgressMeters, 0f),
                0f,
                SanitizePositive(_climbHeightMeters, LadderClimbIkConstants.DefaultRungSpacingMeters));
            float3 safeUp = NormalizeSafe(_ladderUp, new float3(0f, 1f, 0f));
            double3 offsetMeters = new double3(safeUp.x, safeUp.y, safeUp.z) * (double)safeProgress;
            return AbsoluteUniversePosition.OffsetMeters(in ladderAup, offsetMeters);
        }

        private void StopClimb(bool finished, bool slipped)
        {
            if (!_active && !_pendingFinish && !_pendingSlip)
                return;

            _active = false;
            _pendingFinish = false;
            _pendingSlip = false;
            if (finished && !slipped && !_vrGripRequired && _matchRotation && _playerRoot != null)
            {
                Transform target = _climbDirection > 0f ? _exitPoint : _entryPoint;
                if (target != null)
                    _playerRoot.rotation = target.rotation;
            }

            if (slipped && _movementForceSink != null)
                _movementForceSink.QueueExternalVelocityChange(new Vector3(0f, SlipVelocityMetersPerSecond, 0f));

            PublishClimbState(slipped);
            _playerRoot = null;
            _ladderTransform = null;
            _entryPoint = null;
            _exitPoint = null;
            _movementForceSink = null;
            _playerContext = null;
            _pendingGripPullMeters = 0f;
            _pendingGripMask = 0;
            _lastResolvedGripMask = 0;
            _lastConsumedInputSequence = 0u;
            _hasConsumedInputSequence = false;
            _currentInputGripHeld = false;
            _headStabilizationInitialized = false;
            UnregisterTickables();
        }

        private void CompleteOutstandingJobForBarrier()
        {
            if (!_solveScheduled)
            {
                ReleaseSolveBufferPins();
                return;
            }

            try
            {
                DispatcherJobFence.BeginLateFrameSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _solveHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndLateFrameSwapWindow();
                }
            }
            finally
            {
                _solveScheduled = false;
                ReleaseSolveBufferPins();
            }
        }

        private void ResolveLadderFrame(Vector3 entryPosition, Vector3 exitPosition, Transform ladderTransform)
        {
            float3 entry = (float3)(entryPosition);
            float3 exit = (float3)(exitPosition);
            float3 axis = exit - entry;
            float lengthSq = math.lengthsq(axis);
            if (lengthSq <= 0.0001f || !math.isfinite(lengthSq))
            {
                _ladderUp = NormalizeSafe((float3)(ladderTransform.up), new float3(0f, 1f, 0f));
                _climbHeightMeters = 2f;
            }
            else
            {
                _climbHeightMeters = lengthSq * math.rsqrt(lengthSq);
                _ladderUp = axis * math.rcp(math.max(_climbHeightMeters, 0.0001f));
            }

            float3 forward = (float3)(ladderTransform.forward);
            forward -= _ladderUp * math.dot(forward, _ladderUp);
            _ladderForward = NormalizeSafe(forward, ResolvePerpendicular(_ladderUp));
        }

        private void InitializePresentationAnchors(Vector3 entryPosition, Vector3 exitPosition)
        {
            _cameraSlideEntryPosition = entryPosition;
            _cameraSlideExitPosition = exitPosition;
            if (cameraSlideTarget != null)
            {
                _headStabilizedRotation = cameraSlideTarget.rotation;
                _headStabilizationInitialized = true;
            }
            else
            {
                _headStabilizedRotation = Quaternion.identity;
                _headStabilizationInitialized = false;
            }
        }

        private void BuildShoulders(
            float3 playerRoot,
            out float3 leftShoulder,
            out float3 rightShoulder,
            out float3 leftPole,
            out float3 rightPole)
        {
            float3 right = NormalizeSafe(math.cross(_ladderForward, _ladderUp), new float3(1f, 0f, 0f));
            float3 center = playerRoot + (_ladderUp * ShoulderHeightMeters) - (_ladderForward * ShoulderBacksetMeters);
            leftShoulder = center - right * (ShoulderWidthMeters * 0.5f);
            rightShoulder = center + right * (ShoulderWidthMeters * 0.5f);
            leftPole = leftShoulder + _ladderForward * ElbowPoleMeters;
            rightPole = rightShoulder + _ladderForward * ElbowPoleMeters;
        }

        private void DumpBlackBox()
        {
            if (!TryResolveVaultViews(out LadderClimbIkVaultViews views) || !views.HasTelemetry)
                return;

            NativeArray<LadderClimbTelemetryEntry> telemetryRing = views.TelemetryRing;
            NativeArray<int> telemetryCursor = views.TelemetryCursor;
            if (!telemetryRing.IsCreated)
                return;

            int capacity = math.min(telemetryRing.Length, LadderClimbIkConstants.BlackBoxFrameCapacity);
            if (capacity <= 0)
                return;

            int retainedCount = telemetryCursor.IsCreated &&
                                telemetryCursor.Length >= LadderClimbIkConstants.TelemetryCursorElementCount
                ? math.clamp(telemetryCursor[LadderClimbIkConstants.TelemetryCursorRetainedCountIndex], 0, capacity)
                : capacity;
            if (retainedCount <= 0)
                return;

            int cursor = telemetryCursor.IsCreated &&
                         telemetryCursor.Length >= LadderClimbIkConstants.TelemetryCursorElementCount
                ? PositiveModulo(telemetryCursor[LadderClimbIkConstants.TelemetryCursorNextWriteIndex], capacity)
                : 0;
            int start = retainedCount >= capacity ? cursor : 0;
            int payloadBytes = BlackBoxDumpHeaderBytes + retainedCount * BlackBoxDumpEntryBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    payloadBytes,
                    nameof(ProceduralLadderClimbRuntime),
                    BlackBoxDumpPayloadLabel,
                    NativeArrayOptions.ClearMemory);
                int writeCursor = 0;
                WriteUInt32LittleEndian(payload, ref writeCursor, BlackBoxDumpMagic);
                WriteUInt32LittleEndian(payload, ref writeCursor, BlackBoxDumpVersion);
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)retainedCount));
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)BlackBoxDumpEntryBytes));
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)cursor));
                WriteUInt32LittleEndian(payload, ref writeCursor, unchecked((uint)start));

                for (int i = 0; i < retainedCount; i++)
                {
                    int index = PositiveModulo(start + i, capacity);
                    int rowEnd = writeCursor + BlackBoxDumpEntryBytes;
                    LadderClimbTelemetryEntry entry = telemetryRing[index];
                    WriteTelemetryEntry(payload, ref writeCursor, in entry);
                    if (writeCursor > rowEnd)
                        return;

                    writeCursor = rowEnd;
                }

                NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpPath, payload, writeCursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ProceduralLadderClimbRuntime),
                    BlackBoxDumpPayloadLabel);
            }
        }
        private static void SetTargetPosition(Transform target, float3 position)
        {
            if (target != null)
                target.position = ToVector3(position);
        }

        private static float SanitizeDeltaTime(float deltaTime)
        {
            return deltaTime > 0f && math.isfinite(deltaTime) ? math.min(deltaTime, 0.05f) : 0.0166667f;
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static void WriteTelemetryEntry(NativeArray<byte> destination, ref int cursor, in LadderClimbTelemetryEntry entry)
        {
            WriteFloat3LittleEndian(destination, ref cursor, entry.PlayerRoot);
            WriteFloat3LittleEndian(destination, ref cursor, entry.LeftHandTarget);
            WriteFloat3LittleEndian(destination, ref cursor, entry.RightHandTarget);
            WriteFloat3LittleEndian(destination, ref cursor, entry.LeftElbowTarget);
            WriteFloat3LittleEndian(destination, ref cursor, entry.RightElbowTarget);
            WriteFloatLittleEndian(destination, ref cursor, entry.ProgressMeters);
            WriteFloatLittleEndian(destination, ref cursor, entry.Stamina01);
            WriteInt32LittleEndian(destination, ref cursor, entry.LeftRungIndex);
            WriteInt32LittleEndian(destination, ref cursor, entry.RightRungIndex);
            WriteInt32LittleEndian(destination, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Hash);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
        }

        private static void WriteFloat3LittleEndian(NativeArray<byte> destination, ref int cursor, float3 value)
        {
            WriteFloatLittleEndian(destination, ref cursor, value.x);
            WriteFloatLittleEndian(destination, ref cursor, value.y);
            WriteFloatLittleEndian(destination, ref cursor, value.z);
        }

        private static void WriteFloatLittleEndian(NativeArray<byte> destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += sizeof(uint);
        }

        private static float SanitizePositive(float value, float fallback)
        {
            return value > 0.0001f && math.isfinite(value) ? value : fallback;
        }

        private uint ResolveGripActionMask()
        {
            uint configuredMask = universalGripActionMask;
            if (configuredMask == 0u || configuredMask == LegacySerializedGripActionMask)
                return DefaultGripActionMask;

            return configuredMask | DefaultGripActionMask;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (lengthSq <= 0.0000001f || !math.isfinite(lengthSq))
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float3 ResolvePerpendicular(float3 direction)
        {
            float3 axis = math.abs(direction.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
            return NormalizeSafe(math.cross(direction, axis), new float3(1f, 0f, 0f));
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
    }
}
