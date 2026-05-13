using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Core.Signals;
using Hecton8.Gameplay;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.SceneManagement;

namespace Hecton8.Core
{
    /// <summary>
    /// Manages the world origin shift to maintain precision while preserving an
    /// absolute-universe coordinate space for async systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class HectonFloatingOrigin : MonoBehaviour, ITickable, IUpdatable, IServiceHeartbeat, IServiceShutdown
    {
        private struct OriginShiftTranslateJob : IJobParallelForTransform
        {
            public Vector3 ShiftOffset;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                    return;

                float3 shift = new float3(ShiftOffset.x, ShiftOffset.y, ShiftOffset.z);
                float3 position = new float3(transform.position.x, transform.position.y, transform.position.z);
                if (!math.all(math.isfinite(shift)) || !math.all(math.isfinite(position)))
                    return;

                transform.position -= ShiftOffset;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AupDriftCheckJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<double3> RuntimePositions;
            [ReadOnly] public NativeArray<double3> TrackedAbsolutePositions;
            public double3 CurrentTotalOffset;
            public double MaxDeltaSq;
            [WriteOnly] public NativeArray<byte> InvalidMask;

            public void Execute(int index)
            {
                double3 expectedRuntime = TrackedAbsolutePositions[index] - CurrentTotalOffset;
                double3 delta = expectedRuntime - RuntimePositions[index];
                bool finite =
                    math.all(math.isfinite(expectedRuntime)) &&
                    math.all(math.isfinite(delta)) &&
                    math.all(math.isfinite(RuntimePositions[index]));
                InvalidMask[index] = !finite || math.lengthsq(delta) > MaxDeltaSq ? (byte)1 : (byte)0;
            }
        }

        private struct CriticalAupTracker
        {
            public Transform Transform;
            public double3 AbsolutePosition;
            public Vector3 LastRuntimePosition;
            public bool Initialized;
        }

        private static readonly int _HectonFloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
        private static readonly int _TotalUniverseOffsetId = Shader.PropertyToID("_TotalUniverseOffset");
        private static readonly int _AupJitterMaskId = Shader.PropertyToID("_AupJitterMask");
        private const int OriginShiftListenerCapacity = 128;
        private static readonly RegistryBucket<IOriginShiftListener> _originShiftListeners = new RegistryBucket<IOriginShiftListener>(OriginShiftListenerCapacity);
        private const int PrecisionWatchdogIntervalFrames = 300;
        private const int ShiftStabilityWatchdogFrames = 1200;
        private const float PrecisionWatchdogSafeRadiusMeters = 5000f;
        private const float PrecisionWatchdogSafeRadiusSq = PrecisionWatchdogSafeRadiusMeters * PrecisionWatchdogSafeRadiusMeters;
        private const float MinimumShiftThresholdMeters = 5000f;
        private const float ShiftDeadzoneReleaseMeters = 4500f;
        private const float OutwardMotionSpeedEpsilon = 0.05f;
        private const int DriftCheckEntityCapacity = 2;
        private const int OriginShiftParticleBufferCapacity = 16384;
        private const int ShiftSceneRootCapacity = 1024;
        private const int ShiftParticleSystemCapacity = 4096;
        private const float DriftCheckThresholdMeters = 0.001f;
        private const double DriftCheckThresholdSq = (double)DriftCheckThresholdMeters * DriftCheckThresholdMeters;
        private const int PostShiftUnloadUnusedAssetsMinimumFrames = 300;

        private static OriginShiftEventData _lastShiftEvent;

        // COLD ALLOC: List<GameObject>[1024] - scene root staging for shift target and particle rebases - owner: HectonFloatingOrigin
        private readonly List<GameObject> _sceneRootObjects = new List<GameObject>(ShiftSceneRootCapacity);
        private readonly List<Scene> _pendingLoadedScenes = new List<Scene>(8);
        // COLD ALLOC: List<Transform>[1024] - root transform staging for TransformAccessArray rebuilds - owner: HectonFloatingOrigin
        private readonly List<Transform> _shiftTargetTransforms = new List<Transform>(ShiftSceneRootCapacity);
        private readonly List<MonoBehaviour> _sceneComponentScratch = new List<MonoBehaviour>(512);
        // COLD ALLOC: List<ParticleSystem>[4096] - shift-frame particle system discovery staging - owner: HectonFloatingOrigin
        private readonly List<ParticleSystem> _sceneParticleSystemScratch = new List<ParticleSystem>(ShiftParticleSystemCapacity);
        // COLD ALLOC: ParticleSystem.Particle[16384] - shift-frame world-space particle rebase scratch - owner: HectonFloatingOrigin
        private readonly ParticleSystem.Particle[] _originShiftParticleScratch = new ParticleSystem.Particle[OriginShiftParticleBufferCapacity];

        private TransformAccessArray _shiftTargetAccessArray;
        private Transform[] _shiftTargetArray = Array.Empty<Transform>();
        private bool _shiftTargetsDirty = true;
        private bool _isRegistered;
        private bool _sceneEventsSubscribed;
        private bool _isShiftInProgress;
        private bool _hasPendingShift;
        private bool _physicsPauseActive;
        private bool _shiftDeadzoneArmed = true;
        private bool _hasPreviousAnchorPosition;
        private bool _sceneRebaseTickLockHeld;
        private bool _postShiftUnloadUnusedAssetsRunning;
        private SimulationMode _physicsSimulationModeBeforeShift = SimulationMode.FixedUpdate;
        private bool _driftCheckScheduled;
        private int _physicsResumeFrame = -1;
        private int _pendingShiftFrame = -1;
        private int _aupJitterMaskReleaseFrame = -1;
        private int _lastPostShiftUnloadUnusedAssetsFrame = -PostShiftUnloadUnusedAssetsMinimumFrames;
        private float _thresholdSqr;
        private float _anchorResolveTimer;
        private uint _shiftSequence;
        private int _driftCheckCount;
        private int _lastShiftSceneReadyCount;
        private int _lastShiftSceneTotal;
        private Vector3 _previousAnchorPosition;
        private Vector3 _pendingShiftOffset;
        private Rigidbody _anchorRigidbody;
        private CriticalAupTracker _playerDriftTracker;
        private CriticalAupTracker _submarineDriftTracker;
        private NativeArray<double3> _driftCheckRuntimePositions;
        private NativeArray<double3> _driftCheckAbsolutePositions;
        private NativeArray<byte> _driftCheckInvalidMask;
        private JobHandle _driftCheckHandle;
        private int _precisionWatchdogCountdown;
        private int _precisionWatchdogCachedFrame = -1;
        private bool _precisionWatchdogDueThisFrame;

        private const float AnchorResolveCooldown = 1f;

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Distance from (0,0,0) that triggers a shift.")]
        [SerializeField] private float _threshold = 1000f;

        [Tooltip("Object to follow (normally Player). If null, resolves via GameBootstrapper.")]
        [SerializeField] private Transform _anchor;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalOffset { get; private set; }

        private double3 _totalOffsetDouble;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalUniverseOffset => TotalOffset;

        /// <summary>Current absolute-universe offset committed since startup.</summary>
        public static Vector3 CurrentTotalOffset
        {
            get
            {
                HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
                return origin != null ? origin.TotalOffset : Vector3.zero;
            }
        }

        /// <summary>Current committed origin offset in double precision for AUP authority math.</summary>
        public static double3 CurrentTotalOffsetDouble
        {
            get
            {
                HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
                return origin != null ? origin._totalOffsetDouble : double3.zero;
            }
        }

        /// <summary>Fractional fixed-step interpolation alpha sampled for the current render frame.</summary>
        public static float CurrentFixedInterpolationAlpha => ResolveFixedInterpolationAlpha();

        /// <summary>Current committed shift sequence.</summary>
        public static uint CurrentShiftSequence
        {
            get
            {
                HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
                return origin != null ? origin._shiftSequence : 0u;
            }
        }

        /// <summary>True while the floating-origin shift job is executing.</summary>
        public static bool IsShiftInProgress
        {
            get
            {
                HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
                return origin != null && origin._isShiftInProgress;
            }
        }

        /// <summary>True while PhysX remains paused for the shift window.</summary>
        public static bool IsPhysicsPausedForShift
        {
            get
            {
                HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
                return origin != null && origin._physicsPauseActive;
            }
        }

        /// <summary>Last committed shift event payload.</summary>
        public static OriginShiftEventData LastShiftEvent => _lastShiftEvent;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isRegistered && !_isShiftInProgress ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Booting;

        /// <inheritdoc />
        public bool IsServiceReady => _isRegistered && !_isShiftInProgress;

        /// <summary>
        /// Ensures a live floating-origin owner exists for bootstrap-owned simulation.
        /// </summary>
        /// <returns>Live floating-origin owner.</returns>
        internal static HectonFloatingOrigin EnsureRuntimeInstance()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin != null)
                return origin;

            GameObject runtimeRoot = new GameObject("[HectonFloatingOrigin]"); // COLD ALLOC: GameObject[1] - bootstrap-owned AUP/floating-origin authority - owner: HectonFloatingOrigin
            return runtimeRoot.AddComponent<HectonFloatingOrigin>();
        }

        /// <summary>
        /// Freezes physics integration and clears transient kinematic state before instantaneous AUP travel.
        /// </summary>
        public static void BeginSafeTeleportProtocol()
        {
            PhysicsApplySystem.ClearQueuedPacketsStatic();
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin != null)
                BeginSafeTeleportProtocolInternal(origin);
        }

        /// <summary>
        /// Releases the one-frame physics pause requested by <see cref="BeginSafeTeleportProtocol"/>.
        /// </summary>
        public static void EndSafeTeleportProtocol()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin != null)
                origin._physicsResumeFrame = Time.frameCount + 1;
        }

        internal static bool TryFlushInitialSceneRebaseBeforeTicks()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin == null)
                return true;

            if (origin._isShiftInProgress || origin._physicsPauseActive)
                return false;

            if (origin._pendingLoadedScenes.Count > 0 || origin._shiftTargetsDirty)
                origin.ProcessPendingSceneSynchronization();

            return origin._pendingLoadedScenes.Count == 0 &&
                   !origin._shiftTargetsDirty &&
                   !origin._sceneRebaseTickLockHeld;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _lastShiftEvent = default;
            _originShiftListeners.Clear();
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, Vector4.zero);
            Shader.SetGlobalFloat(_AupJitterMaskId, 0f);
            HectonXRRuntimeState.ResetShaderGlobals();
        }

        /// <summary>
        /// Converts the supplied runtime-space position into absolute-universe space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space position.</param>
        /// <returns>Absolute-universe position.</returns>
        public static Vector3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            double3 absolutePosition = ToAbsoluteUniversePositionDouble3(runtimePosition);
            return ToVector3(absolutePosition);
        }

        /// <summary>
        /// Converts runtime-space to absolute-universe coordinates without reducing the committed offset to float.
        /// </summary>
        public static double3 ToAbsoluteUniversePositionDouble3(Vector3 runtimePosition)
        {
            return new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + CurrentTotalOffsetDouble;
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition)
        {
            return ToRuntimePosition(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                CurrentTotalOffsetDouble);
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(double3 absoluteUniversePosition)
        {
            return ToRuntimePosition(absoluteUniversePosition, CurrentTotalOffsetDouble);
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using an explicit committed total offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <param name="committedTotalOffset">Committed absolute-universe offset.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition, Vector3 committedTotalOffset)
        {
            return ToRuntimePosition(
                new double3(absoluteUniversePosition.x, absoluteUniversePosition.y, absoluteUniversePosition.z),
                new double3(committedTotalOffset.x, committedTotalOffset.y, committedTotalOffset.z));
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using an explicit committed total offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <param name="committedTotalOffset">Committed absolute-universe offset.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(double3 absoluteUniversePosition, double3 committedTotalOffset)
        {
            return ToVector3(absoluteUniversePosition - committedTotalOffset);
        }

        internal static void ResyncBody(Rigidbody body, in AbsoluteUniversePosition absolutePosition)
        {
            if (body == null)
                return;

            float3 runtimePosition3 = absolutePosition.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtimePosition3)))
                return;

            Vector3 runtimePosition = new Vector3(
                runtimePosition3.x,
                runtimePosition3.y,
                runtimePosition3.z);
            Vector3 linearVelocity = math.all(math.isfinite((float3)body.linearVelocity))
                ? body.linearVelocity
                : Vector3.zero;
            Vector3 angularVelocity = math.all(math.isfinite((float3)body.angularVelocity))
                ? body.angularVelocity
                : Vector3.zero;
            bool wasSleeping = body.IsSleeping();

            body.position = runtimePosition;
            body.MovePosition(runtimePosition);
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
            if (wasSleeping)
                body.Sleep();
            else
                body.WakeUp();

            body.PublishTransform();
        }

        /// <summary>
        /// Registers a listener for committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void RegisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            if (_originShiftListeners.Contains(listener))
                return;

            _originShiftListeners.Register(listener);
        }

        /// <summary>
        /// Checks whether a listener is currently registered in the fixed origin-shift bucket.
        /// </summary>
        /// <param name="listener">Listener instance to test.</param>
        /// <returns>True when the listener is present.</returns>
        internal static bool IsListenerRegistered(IOriginShiftListener listener)
        {
            return listener != null && _originShiftListeners.Contains(listener);
        }

        /// <summary>
        /// Unregisters a listener from committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void UnregisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            if (!_originShiftListeners.Contains(listener))
                return;

            _originShiftListeners.Unregister(listener);
        }

        /// <summary>
        /// Marks the root-transform cache dirty so the next shift rebuilds it on the cold path.
        /// </summary>
        public static void MarkShiftTargetsDirty()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin == null)
                return;

            origin._shiftTargetsDirty = true;
        }

        /// <summary>
        /// Waits until no shift job is executing and the atomic physics pause gate has ended.
        /// Async systems must call this before writing runtime transforms that depend on
        /// the current floating-origin offset.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Stable committed shift payload for the current frame.</returns>
        public static async Awaitable<OriginShiftEventData> WaitForShiftStabilityAsync(CancellationToken cancellationToken = default)
        {
            int watchdog = 0;
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            while (origin != null &&
                   (origin._isShiftInProgress ||
                    origin._physicsPauseActive ||
                    HasPendingSceneRebaseBarrier(origin)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (watchdog++ > ShiftStabilityWatchdogFrames)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[FloatingOrigin] WaitForShiftStabilityAsync timed out.", origin);
#endif
                    break;
                }

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                origin = GlobalRegistry.FloatingOrigin;
            }

            double3 currentOffsetDouble = CurrentTotalOffsetDouble;
            Vector3 currentOffset = ToVector3(currentOffsetDouble);
            uint currentSequence = CurrentShiftSequence;
            return new OriginShiftEventData(
                Vector3.zero,
                currentOffset,
                currentOffset,
                currentOffsetDouble,
                currentOffsetDouble,
                currentSequence,
                Time.frameCount);
        }

        private void Awake()
        {
            HectonFloatingOrigin registeredOrigin = GlobalRegistry.FloatingOrigin;
            if (registeredOrigin != null && registeredOrigin != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterFloatingOriginRuntime(this);
            RefreshThresholdCache();
            TryResolveAnchor(force: true);
            EnsureDriftCheckBuffers();
            PublishGlobalOffsets();
            SubscribeSceneEvents();
        }

        private void OnEnable()
        {
            TryRegister();
            MarkShiftTargetsDirty();
            TryPrepareShiftTargets();
        }

        private void OnDisable()
        {
            TryUnregister();
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
            ReleaseSceneRebaseTickLock();
            TryUnregister();
            UnsubscribeSceneEvents();
            DisposeShiftTargetAccessArray();
            DisposeDriftCheckState();

            if (GlobalRegistry.FloatingOrigin == this)
            {
                if (_physicsPauseActive)
                {
                    UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
                }

                _originShiftListeners.Clear();
                GlobalRegistry.UnregisterFloatingOriginRuntime(this);
            }
        }

        /// <summary>
        /// Explicit bootstrap registration pass after <see cref="GlobalRegistry.Dispatcher"/> exists.
        /// </summary>
        internal void InitializeService()
        {
            if (GlobalRegistry.FloatingOrigin != this)
                GlobalRegistry.RegisterFloatingOriginRuntime(this);

            RefreshThresholdCache();
            TryResolveAnchor(force: true);
            EnsureDriftCheckBuffers();
            PublishGlobalOffsets();
            SubscribeSceneEvents();
            TryRegister();
            MarkShiftTargetsDirty();
            TryPrepareShiftTargets();
        }

        /// <summary>
        /// Monitors anchor distance and commits a synchronized floating-origin shift.
        /// </summary>
        /// <param name="deltaTime">Scaled tick delta supplied by the tick manager.</param>
        public void Tick(float deltaTime)
        {
            UpdateAupJitterMaskRelease();
            if (_isShiftInProgress)
                return;

            if (_physicsPauseActive)
            {
                if (Time.frameCount >= _physicsResumeFrame)
                    ResumePhysicsAfterShift();
                else
                    return;
            }

            if (_shiftTargetsDirty || _pendingLoadedScenes.Count > 0)
                ProcessPendingSceneSynchronization();

            if (_hasPendingShift)
            {
                if (Time.frameCount >= _pendingShiftFrame)
                {
                    Vector3 pendingShiftOffset = _pendingShiftOffset;
                    _pendingShiftOffset = Vector3.zero;
                    _pendingShiftFrame = -1;
                    _hasPendingShift = false;
                    BeginShiftWorldImmediate(pendingShiftOffset);
                }

                return;
            }

            if (_driftCheckScheduled && ConsumeCompletedDriftCheck())
                return;

            UpdateCriticalEntityTrackers();
            bool precisionWatchdogFrame = ShouldRunPrecisionWatchdogFrame();
            if (!_driftCheckScheduled && precisionWatchdogFrame)
                ScheduleAupDriftCheck();

            if (_anchor == null)
            {
                _anchorResolveTimer -= deltaTime;
                if (_anchorResolveTimer > 0f)
                    return;

                TryResolveAnchor(force: false);
                if (_anchor == null)
                    return;
            }

            Vector3 anchorPosition = _anchor.position;
            float anchorDistanceSqr = VectorLengthSq(anchorPosition);
            if (anchorDistanceSqr <= ShiftDeadzoneReleaseMeters * ShiftDeadzoneReleaseMeters)
                _shiftDeadzoneArmed = true;

            bool isMovingAwayFromCenter = IsAnchorMovingAwayFromCenter(anchorPosition, deltaTime);
            if (precisionWatchdogFrame &&
                anchorDistanceSqr >= PrecisionWatchdogSafeRadiusSq &&
                _shiftDeadzoneArmed &&
                isMovingAwayFromCenter)
            {
                _shiftDeadzoneArmed = false;
                BeginShiftWorld(anchorPosition);
                return;
            }

            if (anchorDistanceSqr > _thresholdSqr && _shiftDeadzoneArmed && isMovingAwayFromCenter)
            {
                _shiftDeadzoneArmed = false;
                BeginShiftWorld(anchorPosition);
            }
        }

        private bool ShouldRunPrecisionWatchdogFrame()
        {
            int frame = Time.frameCount;
            if (_precisionWatchdogCachedFrame == frame)
                return _precisionWatchdogDueThisFrame;

            _precisionWatchdogCachedFrame = frame;
            if (_precisionWatchdogCountdown <= 0)
            {
                _precisionWatchdogCountdown = PrecisionWatchdogIntervalFrames - 1;
                _precisionWatchdogDueThisFrame = true;
                return true;
            }

            _precisionWatchdogCountdown--;
            _precisionWatchdogDueThisFrame = false;
            return false;
        }

        private void BeginShiftWorld(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                return;
            }

            if (VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            if (_isShiftInProgress)
                return;

            if (_hasPendingShift)
                return;

            _pendingShiftOffset = shiftOffset;
            _pendingShiftFrame = Time.frameCount + 1;
            _hasPendingShift = true;
            PublishAupPreShiftSignal(shiftOffset, _shiftSequence + 1u);
        }

        private void BeginShiftWorldImmediate(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                return;
            }

            if (VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            if (_isShiftInProgress)
                return;

            _isShiftInProgress = true;
            _ = ShiftWorldAsync(shiftOffset, destroyCancellationToken);
        }

        private async Awaitable ShiftWorldAsync(Vector3 shiftOffset, CancellationToken cancellationToken)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                _isShiftInProgress = false;
                return;
            }

            _isShiftInProgress = true;
            SystemDispatcher.RequestOriginShiftFrameLock(Time.frameCount);
            bool trackedBodiesPrepared = false;
            bool trackedBodiesFinalized = false;
            bool xrPoseLockActive = false;
            int gen0CollectionCountBeforeShift = GC.CollectionCount(0);
            HectonXRRuntimeState.BeginOriginShiftPoseLock();
            xrPoseLockActive = HectonXRRuntimeState.IsXRActive;
            PausePhysicsForShift();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                PhysicsApplySystem.PrepareTrackedBodiesForOriginShift();
                trackedBodiesPrepared = true;

                if (_shiftTargetsDirty)
                    RebuildShiftTargetCache();

                if (_shiftTargetAccessArray.isCreated && _shiftTargetAccessArray.length > 0)
                {
                    OriginShiftTranslateJob shiftJob = new OriginShiftTranslateJob
                    {
                        ShiftOffset = shiftOffset
                    };

                    JobHandle handle = UnityEngine.Jobs.IJobParallelForTransformExtensions.ScheduleByRef(ref shiftJob, _shiftTargetAccessArray, default);
                    await AwaitTransformShiftJobAsync(handle, cancellationToken);
                }

                PhysicsApplySystem.CommitTrackedBodiesForOriginShift(shiftOffset);

                double3 previousTotalOffsetDouble = _totalOffsetDouble;
                Vector3 previousTotalOffset = TotalOffset;
                _totalOffsetDouble += new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
                TotalOffset = ToVector3(_totalOffsetDouble);
                _shiftSequence++;
                float fixedInterpolationAlpha = ResolveFixedInterpolationAlpha();
                _lastShiftEvent = new OriginShiftEventData(
                    shiftOffset,
                    previousTotalOffset,
                    TotalOffset,
                    previousTotalOffsetDouble,
                    _totalOffsetDouble,
                    _shiftSequence,
                    Time.frameCount,
                    fixedInterpolationAlpha);

                ArmAupJitterMask(Time.frameCount);
                RebaseParticleSystemsForOriginShift(in _lastShiftEvent);
                PublishAupShiftSignal(in _lastShiftEvent);
                CrashTelemetryBuffer.ReportOriginShift(shiftOffset, _shiftSequence);
                PublishGlobalOffsets();
                HectonXRRuntimeState.EndOriginShiftPoseLock(_shiftSequence, fixedInterpolationAlpha);
                xrPoseLockActive = false;
                ResyncCriticalEntityTrackersAfterShift();
                PhysicsApplySystem.FinalizeTrackedBodiesAfterOriginShift();
                trackedBodiesFinalized = true;
                WorldSpatialHashGrid.HandleOriginShift(_lastShiftEvent);
                await BroadcastOriginShiftAsync(_lastShiftEvent, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CompleteSceneRebaseBarrier();
                _physicsResumeFrame = Time.frameCount;
            }
            catch (Exception exception)
            {
                CompleteSceneRebaseBarrier();
                _physicsResumeFrame = Time.frameCount;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
            finally
            {
                if (xrPoseLockActive)
                    HectonXRRuntimeState.EndOriginShiftPoseLock(_shiftSequence, ResolveFixedInterpolationAlpha());

                if (trackedBodiesPrepared && !trackedBodiesFinalized)
                    PhysicsApplySystem.FinalizeTrackedBodiesAfterOriginShift();

                _isShiftInProgress = false;
            }

            if (trackedBodiesFinalized)
                _ = RunPostShiftUnusedAssetUnloadGuardAsync(gen0CollectionCountBeforeShift, cancellationToken);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (trackedBodiesFinalized)
                Debug.Log("[FloatingOrigin] shift committed.");
#endif
        }

        private static void PublishAupShiftSignal(in OriginShiftEventData shiftData)
        {
            int3 sectorDelta = ResolveAupSectorDelta(shiftData.PreviousTotalOffsetDouble, shiftData.NewTotalOffsetDouble);
            AupShiftSignal signal = new AupShiftSignal
            {
                ShiftMeters = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z),
                ShiftFrameId = shiftData.Sequence,
                SectorDelta = sectorDelta,
                Flags = shiftData.IsSafeTeleport ? 1u : 0u
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishAupPreShiftSignal(Vector3 shiftOffset, uint nextShiftSequence)
        {
            double3 previousOffset = _totalOffsetDouble;
            double3 newOffset = previousOffset + new double3(shiftOffset.x, shiftOffset.y, shiftOffset.z);
            AupPreShiftSignal signal = new AupPreShiftSignal
            {
                ShiftMeters = new float3(shiftOffset.x, shiftOffset.y, shiftOffset.z),
                ShiftFrameId = nextShiftSequence,
                SectorDelta = ResolveAupSectorDelta(previousOffset, newOffset),
                Flags = 0u
            };
            GlobalSignals.Publish(in signal);
        }

        private static int3 ResolveAupSectorDelta(double3 previousOffset, double3 newOffset)
        {
            AbsoluteUniversePosition previousAup = AbsoluteUniversePosition.FromAbsolutePosition(previousOffset);
            AbsoluteUniversePosition newAup = AbsoluteUniversePosition.FromAbsolutePosition(newOffset);

            return new int3(
                ClampLongToInt(newAup.GridX - previousAup.GridX),
                ClampLongToInt(newAup.GridY - previousAup.GridY),
                ClampLongToInt(newAup.GridZ - previousAup.GridZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ClampLongToInt(long value)
        {
            if (value > int.MaxValue)
                return int.MaxValue;
            if (value < int.MinValue)
                return int.MinValue;
            return (int)value;
        }

        private async Awaitable RunPostShiftUnusedAssetUnloadGuardAsync(
            int gen0CollectionCountBeforeShift,
            CancellationToken cancellationToken)
        {
            if (_postShiftUnloadUnusedAssetsRunning)
                return;

            int currentFrame = Time.frameCount;
            if (currentFrame - _lastPostShiftUnloadUnusedAssetsFrame < PostShiftUnloadUnusedAssetsMinimumFrames)
                return;

            _postShiftUnloadUnusedAssetsRunning = true;
            try
            {
                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (GC.CollectionCount(0) != gen0CollectionCountBeforeShift)
                    return;

                _lastPostShiftUnloadUnusedAssetsFrame = Time.frameCount;
                AssetLifecycleGovernor governor = GlobalRegistry.AssetLifecycle;
                governor?.ForceDrainPendingReleaseQueue();

                RenderTexturePool pool = GlobalRegistry.RenderTexturePool;
                pool?.ClearAllPools();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _postShiftUnloadUnusedAssetsRunning = false;
            }
        }

        private void PausePhysicsForShift()
        {
            _physicsSimulationModeBeforeShift = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            _physicsPauseActive = true;
            _physicsResumeFrame = Time.frameCount + 1;
        }

        private static void BeginSafeTeleportProtocolInternal(HectonFloatingOrigin origin)
        {
            if (!origin._physicsPauseActive)
                origin.PausePhysicsForShift();

            PhysicsApplySystem.ResetTrackedBodiesForSafeTeleport();
            PhysicsApplySystem.ArmSafeTeleportSpeculativeCcdForSafeTeleport();

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            playerContext?.PlayerMovement?.ResetKinematicTransientStateForTeleport();

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            MonoBehaviour submarineBehaviour = submarine as MonoBehaviour;
            if (submarineBehaviour != null && submarineBehaviour.TryGetComponent(out VehicleMotor vehicleMotor))
                vehicleMotor.ResetHydrodynamicPresentationState();

            double3 currentOffsetDouble = origin._totalOffsetDouble;
            Vector3 currentOffset = ToVector3(currentOffsetDouble);
            _lastShiftEvent = new OriginShiftEventData(
                Vector3.zero,
                currentOffset,
                currentOffset,
                currentOffsetDouble,
                currentOffsetDouble,
                origin._shiftSequence,
                Time.frameCount,
                ResolveFixedInterpolationAlpha(),
                isSafeTeleport: true);
        }

        private void ResumePhysicsAfterShift()
        {
            if (!_physicsPauseActive)
                return;

            if (HasPendingSceneRebaseBarrier(this))
                return;

            UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
            _physicsPauseActive = false;
            _physicsResumeFrame = -1;
        }

        private static float ResolveFixedInterpolationAlpha()
        {
            return SystemDispatcher.CurrentFixedInterpolationAlpha;
        }

        private static async Awaitable AwaitTransformShiftJobAsync(JobHandle handle, CancellationToken cancellationToken)
        {
            int watchdog = 0;
            try
            {
                while (!handle.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (watchdog++ > ShiftStabilityWatchdogFrames)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogError("[FloatingOrigin] Transform shift job timed out. Forcing completion before physics resumes.");
#endif
                        break;
                    }

                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                }
            }
            finally
            {
                DispatcherJobSwap.TryComplete(ref handle, true);
            }
        }

        private async Awaitable BroadcastOriginShiftAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            int totalLoadedScenes = CountLoadedScenes();
            Interlocked.Exchange(ref _lastShiftSceneReadyCount, 0);
            Interlocked.Exchange(ref _lastShiftSceneTotal, totalLoadedScenes);

            if (totalLoadedScenes > 0)
            {
                int sceneCount = SceneManager.sceneCount;
                for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
                {
                    Scene scene = SceneManager.GetSceneAt(sceneIndex);
                    if (!scene.IsValid() || !scene.isLoaded)
                        continue;

                    await BroadcastOriginShiftForSceneAsync(scene, shiftData, cancellationToken);
                    Interlocked.Increment(ref _lastShiftSceneReadyCount);
                }
            }

            await BroadcastNonSceneOriginShiftListenersAsync(shiftData, cancellationToken);

            int watchdog = 0;
            while (HasPendingSceneRebaseBarrier(this))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (watchdog++ > ShiftStabilityWatchdogFrames)
                    break;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }
        }

        private async Awaitable BroadcastOriginShiftForSceneAsync(Scene scene, OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            _sceneRootObjects.Clear();
            scene.GetRootGameObjects(_sceneRootObjects);
            for (int rootIndex = 0; rootIndex < _sceneRootObjects.Count; rootIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameObject rootObject = _sceneRootObjects[rootIndex];
                if (rootObject == null)
                    continue;

                _sceneComponentScratch.Clear();
                rootObject.GetComponentsInChildren(true, _sceneComponentScratch);
                for (int componentIndex = 0; componentIndex < _sceneComponentScratch.Count; componentIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    IOriginShiftListener listener = _sceneComponentScratch[componentIndex] as IOriginShiftListener;
                    if (listener == null)
                        continue;

                    RegisterListener(listener);
                    await DispatchOriginShiftListenerAsync(listener, shiftData, cancellationToken);
                }
            }

            _sceneComponentScratch.Clear();
            _sceneRootObjects.Clear();
        }

        private void RebaseParticleSystemsForOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!IsFiniteVector(shiftOffset) || VectorLengthSq(shiftOffset) <= 0.0001f)
                return;

            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                _sceneRootObjects.Clear();
                scene.GetRootGameObjects(_sceneRootObjects);
                for (int rootIndex = 0; rootIndex < _sceneRootObjects.Count; rootIndex++)
                {
                    GameObject rootObject = _sceneRootObjects[rootIndex];
                    if (rootObject == null)
                        continue;

                    _sceneParticleSystemScratch.Clear();
                    rootObject.GetComponentsInChildren(true, _sceneParticleSystemScratch);
                    int particleSystemCount = _sceneParticleSystemScratch.Count;
                    for (int particleSystemIndex = 0; particleSystemIndex < particleSystemCount; particleSystemIndex++)
                        RebaseParticleSystemForOriginShift(_sceneParticleSystemScratch[particleSystemIndex], shiftOffset);
                }
            }

            _sceneParticleSystemScratch.Clear();
            _sceneRootObjects.Clear();
        }

        private void RebaseParticleSystemForOriginShift(ParticleSystem particleSystem, Vector3 shiftOffset)
        {
            if (particleSystem == null)
                return;

            ParticleSystem.MainModule mainModule = particleSystem.main;
            if (mainModule.simulationSpace != ParticleSystemSimulationSpace.World)
            {
                particleSystem.Simulate(0f, false, false);
                return;
            }

            int totalParticles = particleSystem.particleCount;
            int offset = 0;
            while (offset < totalParticles)
            {
                int requestedCount = math.min(_originShiftParticleScratch.Length, totalParticles - offset);
                int particleCount = particleSystem.GetParticles(_originShiftParticleScratch, requestedCount, offset);
                if (particleCount <= 0)
                    break;

                for (int particleIndex = 0; particleIndex < particleCount; particleIndex++)
                {
                    ParticleSystem.Particle particle = _originShiftParticleScratch[particleIndex];
                    Vector3 rebasedPosition = particle.position - shiftOffset;
                    if (IsFiniteVector(rebasedPosition))
                        particle.position = rebasedPosition;
                    _originShiftParticleScratch[particleIndex] = particle;
                }

                particleSystem.SetParticles(_originShiftParticleScratch, particleCount, offset);
                offset += particleCount;
            }

            particleSystem.Simulate(0f, false, false);
        }

        private static async Awaitable DispatchOriginShiftListenerAsync(
            IOriginShiftListener listener,
            OriginShiftEventData shiftData,
            CancellationToken cancellationToken)
        {
            if (listener is IAwaitableOriginShiftListener awaitableListener)
                await awaitableListener.OnOriginShiftAsync(shiftData, cancellationToken);
            else
                listener.OnOriginShift(in shiftData);
        }

        private async Awaitable BroadcastNonSceneOriginShiftListenersAsync(OriginShiftEventData shiftData, CancellationToken cancellationToken)
        {
            IOriginShiftListener[] listeners = _originShiftListeners.RawArray;
            for (int i = _originShiftListeners.Count - 1; i >= 0; i--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IOriginShiftListener listener = listeners[i];
                if (listener == null)
                {
                    _originShiftListeners.Unregister(listener);
                    continue;
                }

                UnityEngine.Object unityListener = listener as UnityEngine.Object;
                if (!ReferenceEquals(unityListener, null) && unityListener == null)
                {
                    _originShiftListeners.Unregister(listener);
                    continue;
                }

                if (IsSceneResidentOriginShiftListener(listener))
                    continue;

                await DispatchOriginShiftListenerAsync(listener, shiftData, cancellationToken);
            }
        }

        private static bool IsSceneResidentOriginShiftListener(IOriginShiftListener listener)
        {
            Component component = listener as Component;
            if (component == null)
                return false;

            Scene scene = component.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static int CountLoadedScenes()
        {
            int loadedSceneCount = 0;
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (scene.IsValid() && scene.isLoaded)
                    loadedSceneCount++;
            }

            return loadedSceneCount;
        }

        private static bool HasPendingSceneRebaseBarrier(HectonFloatingOrigin origin)
        {
            if (origin == null)
                return false;

            int totalCount = Volatile.Read(ref origin._lastShiftSceneTotal);
            return totalCount > 0 && Volatile.Read(ref origin._lastShiftSceneReadyCount) < totalCount;
        }

        private void CompleteSceneRebaseBarrier()
        {
            int totalCount = Volatile.Read(ref _lastShiftSceneTotal);
            Interlocked.Exchange(ref _lastShiftSceneReadyCount, totalCount);
        }

        private void SynchronizeLoadedSceneOriginShiftListeners()
        {
            int sceneCount = SceneManager.sceneCount;
            for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                _sceneRootObjects.Clear();
                scene.GetRootGameObjects(_sceneRootObjects);
                for (int rootIndex = 0; rootIndex < _sceneRootObjects.Count; rootIndex++)
                {
                    GameObject rootObject = _sceneRootObjects[rootIndex];
                    if (rootObject == null)
                        continue;

                    _sceneComponentScratch.Clear();
                    rootObject.GetComponentsInChildren(true, _sceneComponentScratch);
                    for (int componentIndex = 0; componentIndex < _sceneComponentScratch.Count; componentIndex++)
                    {
                        if (_sceneComponentScratch[componentIndex] is IOriginShiftListener listener)
                            RegisterListener(listener);
                    }
                }
            }

            _sceneComponentScratch.Clear();
            _sceneRootObjects.Clear();
        }

        private void UpdateCriticalEntityTrackers()
        {
            if (_anchor == null)
                TryResolveAnchor(force: false);

            UpdateCriticalEntityTracker(ref _playerDriftTracker, _anchor);

            Transform submarineTransform = GlobalRegistry.Submarine != null ? GlobalRegistry.Submarine.PlatformTransform : null;
            UpdateCriticalEntityTracker(ref _submarineDriftTracker, submarineTransform);
        }

        private static void UpdateCriticalEntityTracker(ref CriticalAupTracker tracker, Transform target)
        {
            tracker.Transform = target;
            if (target == null)
            {
                tracker.Initialized = false;
                tracker.LastRuntimePosition = Vector3.zero;
                return;
            }

            Vector3 runtimePosition = target.position;
            float3 runtimePosition3 = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtimePosition3)))
                return;

            if (!tracker.Initialized)
            {
                tracker.AbsolutePosition = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + CurrentTotalOffsetDouble;
                tracker.LastRuntimePosition = runtimePosition;
                tracker.Initialized = true;
                return;
            }

            Vector3 runtimeDelta = runtimePosition - tracker.LastRuntimePosition;
            float3 runtimeDelta3 = new float3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);
            if (math.all(math.isfinite(runtimeDelta3)))
                tracker.AbsolutePosition += new double3(runtimeDelta.x, runtimeDelta.y, runtimeDelta.z);

            tracker.LastRuntimePosition = runtimePosition;
        }

        private void ScheduleAupDriftCheck()
        {
            EnsureDriftCheckBuffers();
            int writeIndex = 0;
            writeIndex = StageCriticalEntityForDriftCheck(_playerDriftTracker, writeIndex);
            writeIndex = StageCriticalEntityForDriftCheck(_submarineDriftTracker, writeIndex);
            if (writeIndex <= 0)
                return;

            _driftCheckCount = writeIndex;
            _driftCheckHandle = new AupDriftCheckJob
            {
                RuntimePositions = _driftCheckRuntimePositions,
                TrackedAbsolutePositions = _driftCheckAbsolutePositions,
                CurrentTotalOffset = _totalOffsetDouble,
                MaxDeltaSq = DriftCheckThresholdSq,
                InvalidMask = _driftCheckInvalidMask
            }.Schedule(writeIndex, 1);
            _driftCheckScheduled = true;
        }

        private int StageCriticalEntityForDriftCheck(in CriticalAupTracker tracker, int writeIndex)
        {
            if (!tracker.Initialized || tracker.Transform == null || writeIndex >= DriftCheckEntityCapacity)
                return writeIndex;

            Vector3 runtimePosition = tracker.Transform.position;
            if (!IsFiniteVector(runtimePosition) || !math.all(math.isfinite(tracker.AbsolutePosition)))
                return writeIndex;

            _driftCheckRuntimePositions[writeIndex] = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            _driftCheckAbsolutePositions[writeIndex] = tracker.AbsolutePosition;
            return writeIndex + 1;
        }

        private bool ConsumeCompletedDriftCheck()
        {
            if (!DispatcherJobSwap.TryComplete(ref _driftCheckHandle, false))
                return false;

            _driftCheckScheduled = false;

            double maxDriftErrorSq = ResolveMaxDriftErrorSq();
            Vector3 telemetryPosition = _anchor != null ? _anchor.position : Vector3.zero;
            CrashTelemetryBuffer.ReportAupMaxDriftError(telemetryPosition, ResolveDriftErrorMeters(maxDriftErrorSq));

            bool hasInvalidEntity = false;
            for (int i = 0; i < _driftCheckCount; i++)
            {
                if (_driftCheckInvalidMask[i] != 0)
                {
                    hasInvalidEntity = true;
                    break;
                }
            }

            _driftCheckCount = 0;
            if (!hasInvalidEntity || _anchor == null)
                return false;

            Vector3 forcedShiftOffset = ResolveForcedDriftShiftOffset();
            if (VectorLengthSq(forcedShiftOffset) <= 0.0001f)
                forcedShiftOffset = _anchor.position;

            if (!IsFiniteVector(forcedShiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(forcedShiftOffset, Vector3.zero);
                return false;
            }

            CrashTelemetryBuffer.ReportAupJitterCorrection(forcedShiftOffset, DriftCheckThresholdMeters);
            _shiftDeadzoneArmed = false;
            BeginShiftWorld(forcedShiftOffset);
            return true;
        }

        private double ResolveMaxDriftErrorSq()
        {
            double maxDriftErrorSq = 0d;
            for (int i = 0; i < _driftCheckCount; i++)
            {
                double driftErrorSq = ResolveDriftErrorSq(i);
                if (!math.isfinite(driftErrorSq))
                    return double.PositiveInfinity;

                maxDriftErrorSq = math.max(maxDriftErrorSq, driftErrorSq);
            }

            return maxDriftErrorSq;
        }

        private double ResolveDriftErrorSq(int index)
        {
            double3 expectedRuntime = _driftCheckAbsolutePositions[index] - _totalOffsetDouble;
            double3 delta = expectedRuntime - _driftCheckRuntimePositions[index];
            double driftErrorSq = math.lengthsq(delta);
            return math.all(math.isfinite(delta)) && math.isfinite(driftErrorSq) ? driftErrorSq : double.PositiveInfinity;
        }

        private static float ResolveDriftErrorMeters(double driftErrorSq)
        {
            if (driftErrorSq <= 0d)
                return 0f;

            if (!math.isfinite(driftErrorSq))
                return float.MaxValue;

            double driftErrorMeters = driftErrorSq * math.rsqrt(driftErrorSq);
            if (!math.isfinite(driftErrorMeters))
                return float.MaxValue;

            return driftErrorMeters >= float.MaxValue ? float.MaxValue : (float)driftErrorMeters;
        }

        private Vector3 ResolveForcedDriftShiftOffset()
        {
            if (TryResolveForcedDriftShiftOffset(in _playerDriftTracker, 0, out Vector3 shiftOffset))
                return shiftOffset;

            if (TryResolveForcedDriftShiftOffset(in _submarineDriftTracker, 1, out shiftOffset))
                return shiftOffset;

            return Vector3.zero;
        }

        private bool TryResolveForcedDriftShiftOffset(in CriticalAupTracker tracker, int maskIndex, out Vector3 shiftOffset)
        {
            shiftOffset = Vector3.zero;
            if (maskIndex < 0 ||
                maskIndex >= DriftCheckEntityCapacity ||
                maskIndex >= _driftCheckInvalidMask.Length ||
                _driftCheckInvalidMask[maskIndex] == 0 ||
                !tracker.Initialized ||
                tracker.Transform == null)
            {
                return false;
            }

            double3 expectedRuntime = tracker.AbsolutePosition - _totalOffsetDouble;
            if (!math.all(math.isfinite(expectedRuntime)) || math.lengthsq(expectedRuntime) <= 0.0001d)
                return false;

            float3 expectedRuntime3 = new float3((float)expectedRuntime.x, (float)expectedRuntime.y, (float)expectedRuntime.z);
            if (!math.all(math.isfinite(expectedRuntime3)))
                return false;

            shiftOffset = new Vector3(expectedRuntime3.x, expectedRuntime3.y, expectedRuntime3.z);
            return true;
        }

        private void ResyncCriticalEntityTrackersAfterShift()
        {
            ResyncCriticalEntityTrackerAfterShift(ref _playerDriftTracker);
            ResyncCriticalEntityTrackerAfterShift(ref _submarineDriftTracker);
        }

        private static void ResyncCriticalEntityTrackerAfterShift(ref CriticalAupTracker tracker)
        {
            if (!tracker.Initialized || tracker.Transform == null)
                return;

            tracker.LastRuntimePosition = tracker.Transform.position;
        }

        private void EnsureDriftCheckBuffers()
        {
            if (_driftCheckRuntimePositions.IsCreated && _driftCheckRuntimePositions.Length == DriftCheckEntityCapacity)
                return;

            DisposeDriftCheckState();
            _driftCheckRuntimePositions = new NativeArray<double3>(DriftCheckEntityCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _driftCheckAbsolutePositions = new NativeArray<double3>(DriftCheckEntityCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _driftCheckInvalidMask = new NativeArray<byte>(DriftCheckEntityCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _driftCheckRuntimePositions,
                nameof(HectonFloatingOrigin),
                nameof(_driftCheckRuntimePositions),
                NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(
                _driftCheckAbsolutePositions,
                nameof(HectonFloatingOrigin),
                nameof(_driftCheckAbsolutePositions),
                NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(
                _driftCheckInvalidMask,
                nameof(HectonFloatingOrigin),
                nameof(_driftCheckInvalidMask),
                NativeAllocationLifetime.Session);
        }

        private void DisposeDriftCheckState()
        {
            JobHandle dependency = _driftCheckScheduled ? _driftCheckHandle : default;
            if (_driftCheckRuntimePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_driftCheckRuntimePositions);
                _driftCheckRuntimePositions.Dispose(dependency);
                _driftCheckRuntimePositions = default;
            }

            if (_driftCheckAbsolutePositions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_driftCheckAbsolutePositions);
                _driftCheckAbsolutePositions.Dispose(dependency);
                _driftCheckAbsolutePositions = default;
            }

            if (_driftCheckInvalidMask.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_driftCheckInvalidMask);
                _driftCheckInvalidMask.Dispose(dependency);
                _driftCheckInvalidMask = default;
            }

            _driftCheckHandle = default;
            _driftCheckScheduled = false;
            _driftCheckCount = 0;
        }

        private void RebuildShiftTargetCache()
        {
            _shiftTargetTransforms.Clear();

            int sceneCount = SceneManager.sceneCount;
            for (int i = 0; i < sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                _sceneRootObjects.Clear();
                scene.GetRootGameObjects(_sceneRootObjects);
                for (int j = 0; j < _sceneRootObjects.Count; j++)
                {
                    GameObject rootObject = _sceneRootObjects[j];
                    if (rootObject == null)
                        continue;

                    _shiftTargetTransforms.Add(rootObject.transform);
                }
            }

            int transformCount = _shiftTargetTransforms.Count;
            if (_shiftTargetArray.Length != transformCount)
            {
                _shiftTargetArray = transformCount == 0
                    ? Array.Empty<Transform>()
                    : new Transform[transformCount]; // COLD ALLOC: Transform[transformCount] — cached root transform snapshot for atomic origin shifts — owner: HectonFloatingOrigin
            }

            for (int i = 0; i < transformCount; i++)
                _shiftTargetArray[i] = _shiftTargetTransforms[i];

            DisposeShiftTargetAccessArray();
            if (transformCount > 0)
            {
                TransformAccessArray.Allocate(transformCount, -1, out _shiftTargetAccessArray);
                _shiftTargetAccessArray.SetTransforms(_shiftTargetArray);
            }

            _shiftTargetsDirty = false;
        }

        private void DisposeShiftTargetAccessArray()
        {
            if (_shiftTargetAccessArray.isCreated)
                _shiftTargetAccessArray.Dispose();
        }

        private void TryResolveAnchor(bool force)
        {
            if (_anchor != null)
                return;

            if (!force && _anchorResolveTimer > 0f)
                return;

            _anchorResolveTimer = AnchorResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
                _anchor = playerTransform;
                _anchor.TryGetComponent(out _anchorRigidbody);
                _previousAnchorPosition = _anchor.position;
                _hasPreviousAnchorPosition = true;
            }
        }

        private void RefreshThresholdCache()
        {
            if (_threshold < MinimumShiftThresholdMeters)
                _threshold = MinimumShiftThresholdMeters;

            _thresholdSqr = _threshold * _threshold;
        }

        private bool IsAnchorMovingAwayFromCenter(Vector3 anchorPosition, float deltaTime)
        {
            Vector3 anchorVelocity = Vector3.zero;
            if (_anchorRigidbody != null)
            {
                anchorVelocity = _anchorRigidbody.linearVelocity;
            }
            else if (_hasPreviousAnchorPosition)
            {
                float safeDeltaTime = math.max(deltaTime, 0.0001f);
                anchorVelocity = (anchorPosition - _previousAnchorPosition) * math.rcp(safeDeltaTime);
            }

            _previousAnchorPosition = anchorPosition;
            _hasPreviousAnchorPosition = true;
            if (VectorLengthSq(anchorPosition) <= 0.0001f)
                return false;

            float3 anchorPosition3 = new float3(anchorPosition.x, anchorPosition.y, anchorPosition.z);
            float3 anchorVelocity3 = new float3(anchorVelocity.x, anchorVelocity.y, anchorVelocity.z);
            float3 radialDirection = anchorPosition3 * math.rsqrt(math.lengthsq(anchorPosition3));
            float radialVelocity = math.dot(radialDirection, anchorVelocity3);
            return radialVelocity > OutwardMotionSpeedEpsilon;
        }

        private void PublishGlobalOffsets()
        {
            Vector4 offset = new Vector4(TotalOffset.x, TotalOffset.y, TotalOffset.z, 0f);
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, offset);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, offset);
            Shader.SetGlobalFloat(_AupJitterMaskId, ResolveAupJitterMask());
            HectonXRRuntimeState.PublishOriginShiftState(_shiftSequence, ResolveFixedInterpolationAlpha());
        }

        internal static void PublishCurrentGlobalOffsetsForRenderLoop()
        {
            HectonFloatingOrigin origin = GlobalRegistry.FloatingOrigin;
            if (origin != null)
            {
                origin.PublishGlobalOffsets();
                return;
            }

            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, Vector4.zero);
            Shader.SetGlobalFloat(_AupJitterMaskId, 0f);
            HectonXRRuntimeState.PublishOriginShiftState(0u, 0f);
        }

        private void ArmAupJitterMask(int frame)
        {
            _aupJitterMaskReleaseFrame = frame + 1;
            Shader.SetGlobalFloat(_AupJitterMaskId, 1f);
        }

        private void UpdateAupJitterMaskRelease()
        {
            if (_aupJitterMaskReleaseFrame < 0 || Time.frameCount <= _aupJitterMaskReleaseFrame)
                return;

            _aupJitterMaskReleaseFrame = -1;
            Shader.SetGlobalFloat(_AupJitterMaskId, 0f);
        }

        private float ResolveAupJitterMask()
        {
            return _aupJitterMaskReleaseFrame >= 0 && Time.frameCount <= _aupJitterMaskReleaseFrame ? 1f : 0f;
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private void TryRegister()
        {
            if (_isRegistered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _isRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _isRegistered = false;
        }

        private void SubscribeSceneEvents()
        {
            if (_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            _sceneEventsSubscribed = true;
        }

        private void UnsubscribeSceneEvents()
        {
            if (!_sceneEventsSubscribed)
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            _sceneEventsSubscribed = false;
        }

        private void AcquireSceneRebaseTickLock()
        {
            if (_sceneRebaseTickLockHeld)
                return;

            SystemDispatcher.RequestOriginShiftBootstrapLock();
            _sceneRebaseTickLockHeld = true;
        }

        private void ReleaseSceneRebaseTickLock()
        {
            if (!_sceneRebaseTickLockHeld)
                return;

            _sceneRebaseTickLockHeld = false;
            SystemDispatcher.ReleaseOriginShiftBootstrapLock();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _shiftTargetsDirty = true;
            QueuePendingLoadedScene(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            _shiftTargetsDirty = true;
            RemovePendingLoadedScene(scene);
            TryPrepareShiftTargets();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            _shiftTargetsDirty = true;
            TryPrepareShiftTargets();
        }

        private void TryPrepareShiftTargets()
        {
            if (!Application.isPlaying ||
                _isShiftInProgress ||
                _physicsPauseActive ||
                _pendingLoadedScenes.Count > 0 ||
                !_shiftTargetsDirty)
            {
                return;
            }

            RebuildShiftTargetCache();
            if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                ReleaseSceneRebaseTickLock();
        }

        private void ProcessPendingSceneSynchronization()
        {
            if (_isShiftInProgress || _physicsPauseActive)
                return;

            try
            {
                if (_pendingLoadedScenes.Count > 0)
                {
                    Vector3 committedTotalOffset = TotalOffset;
                    for (int i = 0; i < _pendingLoadedScenes.Count; i++)
                    {
                        Scene loadedScene = _pendingLoadedScenes[i];
                        if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                            continue;

                        ApplyCommittedOffsetToLoadedScene(loadedScene, committedTotalOffset);
                    }

                    _pendingLoadedScenes.Clear();
                }

                TryPrepareShiftTargets();
            }
            finally
            {
                if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                    ReleaseSceneRebaseTickLock();
            }
        }

        private void QueuePendingLoadedScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            for (int i = 0; i < _pendingLoadedScenes.Count; i++)
            {
                if (_pendingLoadedScenes[i].handle == scene.handle)
                    return;
            }

            _pendingLoadedScenes.Add(scene);
            AcquireSceneRebaseTickLock();
        }

        private void RemovePendingLoadedScene(Scene scene)
        {
            if (!scene.IsValid())
                return;

            for (int i = _pendingLoadedScenes.Count - 1; i >= 0; i--)
            {
                if (_pendingLoadedScenes[i].handle != scene.handle)
                    continue;

                _pendingLoadedScenes.RemoveAt(i);
            }

            if (_pendingLoadedScenes.Count == 0 && !_shiftTargetsDirty)
                ReleaseSceneRebaseTickLock();
        }

        private void ApplyCommittedOffsetToLoadedScene(Scene scene, Vector3 committedTotalOffset)
        {
            if (!IsFiniteVector(committedTotalOffset) || VectorLengthSq(committedTotalOffset) <= 0.0001f)
                return;

            _sceneRootObjects.Clear();
            scene.GetRootGameObjects(_sceneRootObjects);
            for (int i = 0; i < _sceneRootObjects.Count; i++)
            {
                GameObject rootObject = _sceneRootObjects[i];
                if (rootObject == null)
                    continue;

                Transform rootTransform = rootObject.transform;
                Vector3 rootPosition = rootTransform.position;
                if (!IsFiniteVector(rootPosition))
                {
                    CrashTelemetryBuffer.ReportNanPhysicsRecovery(rootPosition, Vector3.zero);
                    continue;
                }

                rootTransform.position = rootPosition - committedTotalOffset;
            }
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.all(math.isfinite(value3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float VectorLengthSq(Vector3 value)
        {
            float3 value3 = new float3(value.x, value.y, value.z);
            return math.lengthsq(value3);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshThresholdCache();
        }
#endif
    }
}
