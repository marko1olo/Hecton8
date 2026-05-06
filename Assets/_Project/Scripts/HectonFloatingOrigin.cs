using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Bootstrap;
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
    public sealed class HectonFloatingOrigin : MonoBehaviour, ITickable, IUpdatable, IServiceHeartbeat
    {
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
            [ReadOnly] public NativeArray<float3> RuntimePositions;
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
        private const double DriftCheckThresholdSq = 0.000001d;
        private const int PostShiftUnloadUnusedAssetsMinimumFrames = 300;

        private static OriginShiftEventData _lastShiftEvent;

        private readonly List<GameObject> _sceneRootObjects = new List<GameObject>(256);
        private readonly List<Scene> _pendingLoadedScenes = new List<Scene>(8);
        private readonly List<Transform> _shiftTargetTransforms = new List<Transform>(256);
        private readonly List<MonoBehaviour> _sceneComponentScratch = new List<MonoBehaviour>(512);

        private TransformAccessArray _shiftTargetAccessArray;
        private Transform[] _shiftTargetArray = Array.Empty<Transform>();
        private bool _shiftTargetsDirty = true;
        private bool _isRegistered;
        private bool _sceneEventsSubscribed;
        private bool _isShiftInProgress;
        private bool _physicsPauseActive;
        private bool _shiftDeadzoneArmed = true;
        private bool _hasPreviousAnchorPosition;
        private bool _postShiftUnloadUnusedAssetsRunning;
        private SimulationMode _physicsSimulationModeBeforeShift = SimulationMode.FixedUpdate;
        private bool _driftCheckScheduled;
        private int _physicsResumeFrame = -1;
        private int _lastPostShiftUnloadUnusedAssetsFrame = -PostShiftUnloadUnusedAssetsMinimumFrames;
        private float _thresholdSqr;
        private float _anchorResolveTimer;
        private uint _shiftSequence;
        private int _driftCheckCount;
        private int _lastShiftSceneReadyCount;
        private int _lastShiftSceneTotal;
        private Vector3 _previousAnchorPosition;
        private Rigidbody _anchorRigidbody;
        private CriticalAupTracker _playerDriftTracker;
        private CriticalAupTracker _submarineDriftTracker;
        private NativeArray<float3> _driftCheckRuntimePositions;
        private NativeArray<double3> _driftCheckAbsolutePositions;
        private NativeArray<byte> _driftCheckInvalidMask;
        private JobHandle _driftCheckHandle;

        private const float AnchorResolveCooldown = 1f;

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Distance from (0,0,0) that triggers a shift.")]
        [SerializeField] private float _threshold = 1000f;

        [Tooltip("Object to follow (normally Player). If null, resolves via SceneBootstrap.")]
        [SerializeField] private Transform _anchor;

        /// <summary>Registry-backed floating-origin owner.</summary>
        public static HectonFloatingOrigin Instance => GlobalRegistry.FloatingOrigin;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalOffset { get; private set; }

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _lastShiftEvent = default;
            _originShiftListeners.Clear();
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, Vector4.zero);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, Vector4.zero);
        }

        /// <summary>
        /// Converts the supplied runtime-space position into absolute-universe space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="runtimePosition">Runtime-space position.</param>
        /// <returns>Absolute-universe position.</returns>
        public static Vector3 ToAbsoluteUniversePosition(Vector3 runtimePosition)
        {
            return runtimePosition + CurrentTotalOffset;
        }

        /// <summary>
        /// Converts the supplied absolute-universe position into runtime space
        /// using the currently committed offset.
        /// </summary>
        /// <param name="absoluteUniversePosition">Absolute-universe position.</param>
        /// <returns>Runtime-space position.</returns>
        public static Vector3 ToRuntimePosition(Vector3 absoluteUniversePosition)
        {
            return absoluteUniversePosition - CurrentTotalOffset;
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
            return absoluteUniversePosition - committedTotalOffset;
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
                    Debug.LogError("[FloatingOrigin] WaitForShiftStabilityAsync timed out.", origin);
                    break;
                }

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
                origin = GlobalRegistry.FloatingOrigin;
            }

            Vector3 currentOffset = CurrentTotalOffset;
            uint currentSequence = CurrentShiftSequence;
            return new OriginShiftEventData(Vector3.zero, currentOffset, currentOffset, currentSequence, Time.frameCount);
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

            if (_driftCheckScheduled && ConsumeCompletedDriftCheck())
                return;

            UpdateCriticalEntityTrackers();
            if (!_driftCheckScheduled && (Time.frameCount % PrecisionWatchdogIntervalFrames) == 0)
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
            float anchorDistanceSqr = anchorPosition.sqrMagnitude;
            if (anchorDistanceSqr <= ShiftDeadzoneReleaseMeters * ShiftDeadzoneReleaseMeters)
                _shiftDeadzoneArmed = true;

            bool isMovingAwayFromCenter = IsAnchorMovingAwayFromCenter(anchorPosition, deltaTime);
            if ((Time.frameCount % PrecisionWatchdogIntervalFrames) == 0 &&
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

        private void BeginShiftWorld(Vector3 shiftOffset)
        {
            if (!IsFiniteVector(shiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(shiftOffset, Vector3.zero);
                return;
            }

            if (shiftOffset.sqrMagnitude <= 0.0001f)
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
            bool trackedBodiesPrepared = false;
            bool trackedBodiesFinalized = false;
            int gen0CollectionCountBeforeShift = GC.CollectionCount(0);
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

                Vector3 previousTotalOffset = TotalOffset;
                TotalOffset += shiftOffset;
                _shiftSequence++;
                float fixedInterpolationAlpha = ResolveFixedInterpolationAlpha();
                _lastShiftEvent = new OriginShiftEventData(
                    shiftOffset,
                    previousTotalOffset,
                    TotalOffset,
                    _shiftSequence,
                    Time.frameCount,
                    fixedInterpolationAlpha);

                CrashTelemetryBuffer.ReportOriginShift(shiftOffset, _shiftSequence);
                PublishGlobalOffsets();
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
                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
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

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            playerContext?.PlayerMovement?.ResetKinematicTransientStateForTeleport();

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            MonoBehaviour submarineBehaviour = submarine as MonoBehaviour;
            if (submarineBehaviour != null && submarineBehaviour.TryGetComponent(out VehicleMotor vehicleMotor))
                vehicleMotor.ResetHydrodynamicPresentationState();

            Vector3 currentOffset = origin.TotalOffset;
            _lastShiftEvent = new OriginShiftEventData(
                Vector3.zero,
                currentOffset,
                currentOffset,
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
            float fixedDeltaTime = Time.fixedDeltaTime;
            if (fixedDeltaTime <= 0.000001f)
                return 0f;

            return math.saturate((Time.time - Time.fixedTime) / fixedDeltaTime);
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

                    await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
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

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
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
                tracker.AbsolutePosition = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z) + new double3(CurrentTotalOffset.x, CurrentTotalOffset.y, CurrentTotalOffset.z);
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
                CurrentTotalOffset = new double3(TotalOffset.x, TotalOffset.y, TotalOffset.z),
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

            _driftCheckRuntimePositions[writeIndex] = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            _driftCheckAbsolutePositions[writeIndex] = tracker.AbsolutePosition;
            return writeIndex + 1;
        }

        private bool ConsumeCompletedDriftCheck()
        {
            if (!DispatcherJobSwap.TryComplete(ref _driftCheckHandle, false))
                return false;

            _driftCheckScheduled = false;

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
            if (forcedShiftOffset.sqrMagnitude <= 0.0001f)
                forcedShiftOffset = _anchor.position;

            if (!IsFiniteVector(forcedShiftOffset))
            {
                CrashTelemetryBuffer.ReportNanPhysicsRecovery(forcedShiftOffset, Vector3.zero);
                return false;
            }

            CrashTelemetryBuffer.ReportAupJitterCorrection(forcedShiftOffset, math.sqrt((float)DriftCheckThresholdSq));
            _shiftDeadzoneArmed = false;
            BeginShiftWorld(forcedShiftOffset);
            return true;
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

            double3 expectedRuntime = tracker.AbsolutePosition - new double3(TotalOffset.x, TotalOffset.y, TotalOffset.z);
            float3 expectedRuntime3 = new float3((float)expectedRuntime.x, (float)expectedRuntime.y, (float)expectedRuntime.z);
            if (!math.all(math.isfinite(expectedRuntime3)) || math.lengthsq(expectedRuntime3) <= 0.0001f)
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
            _driftCheckRuntimePositions = new NativeArray<float3>(DriftCheckEntityCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
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
                anchorVelocity = (anchorPosition - _previousAnchorPosition) / safeDeltaTime;
            }

            _previousAnchorPosition = anchorPosition;
            _hasPreviousAnchorPosition = true;
            if (anchorPosition.sqrMagnitude <= 0.0001f)
                return false;

            Vector3 radialDirection = anchorPosition.normalized;
            float radialVelocity = Vector3.Dot(radialDirection, anchorVelocity);
            return radialVelocity > OutwardMotionSpeedEpsilon;
        }

        private void PublishGlobalOffsets()
        {
            Vector4 offset = new Vector4(TotalOffset.x, TotalOffset.y, TotalOffset.z, 0f);
            Shader.SetGlobalVector(_HectonFloatingOriginOffsetId, offset);
            Shader.SetGlobalVector(_TotalUniverseOffsetId, offset);
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
        }

        private void ProcessPendingSceneSynchronization()
        {
            if (_isShiftInProgress || _physicsPauseActive)
                return;

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
        }

        private void ApplyCommittedOffsetToLoadedScene(Scene scene, Vector3 committedTotalOffset)
        {
            if (!IsFiniteVector(committedTotalOffset) || committedTotalOffset.sqrMagnitude <= 0.0001f)
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshThresholdCache();
        }
#endif
    }
}
