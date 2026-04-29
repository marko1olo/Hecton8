using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Bootstrap;
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
    public sealed class HectonFloatingOrigin : MonoBehaviour, ITickable, IUpdatable
    {
        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct OriginShiftTranslateJob : IJobParallelForTransform
        {
            public Vector3 ShiftOffset;

            public void Execute(int index, TransformAccess transform)
            {
                if (!transform.isValid)
                    return;

                transform.position -= ShiftOffset;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
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
                InvalidMask[index] = math.lengthsq(delta) > MaxDeltaSq ? (byte)1 : (byte)0;
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
        private static readonly List<IOriginShiftListener> _originShiftListeners = new List<IOriginShiftListener>(16);
        private const int PrecisionWatchdogIntervalFrames = 300;
        private const int ShiftStabilityWatchdogFrames = 50000;
        private const float PrecisionWatchdogSafeRadiusMeters = 5000f;
        private const float PrecisionWatchdogSafeRadiusSq = PrecisionWatchdogSafeRadiusMeters * PrecisionWatchdogSafeRadiusMeters;
        private const float MinimumShiftThresholdMeters = 5000f;
        private const float ShiftDeadzoneReleaseMeters = 4500f;
        private const float OutwardMotionSpeedEpsilon = 0.05f;
        private const int DriftCheckEntityCapacity = 2;
        private const double DriftCheckThresholdSq = 0.0001d;

        private static HectonFloatingOrigin _instance;
        private static OriginShiftEventData _lastShiftEvent;

        private readonly List<GameObject> _sceneRootObjects = new List<GameObject>(256);
        private readonly List<Transform> _shiftTargetTransforms = new List<Transform>(256);

        private TransformAccessArray _shiftTargetAccessArray;
        private Transform[] _shiftTargetArray = Array.Empty<Transform>();
        private bool _shiftTargetsDirty = true;
        private bool _isRegistered;
        private bool _sceneEventsSubscribed;
        private bool _isShiftInProgress;
        private bool _physicsPauseActive;
        private bool _shiftDeadzoneArmed = true;
        private bool _hasPreviousAnchorPosition;
        private SimulationMode _physicsSimulationModeBeforeShift = SimulationMode.FixedUpdate;
        private bool _driftCheckScheduled;
        private int _physicsResumeFrame = -1;
        private float _thresholdSqr;
        private float _anchorResolveTimer;
        private uint _shiftSequence;
        private int _driftCheckCount;
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

        /// <summary>Singleton instance.</summary>
        public static HectonFloatingOrigin Instance => _instance;

        /// <summary>Legacy shift event. Offset equals the world-space shift applied to roots.</summary>
        public static event Action<Vector3> OnWorldShift;

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalOffset { get; private set; }

        /// <summary>Cumulative absolute-universe offset committed since startup.</summary>
        public Vector3 TotalUniverseOffset => TotalOffset;

        /// <summary>Current absolute-universe offset committed since startup.</summary>
        public static Vector3 CurrentTotalOffset => _instance != null ? _instance.TotalOffset : Vector3.zero;

        /// <summary>Current committed shift sequence.</summary>
        public static uint CurrentShiftSequence => _instance != null ? _instance._shiftSequence : 0u;

        /// <summary>True while the floating-origin shift job is executing.</summary>
        public static bool IsShiftInProgress => _instance != null && _instance._isShiftInProgress;

        /// <summary>True while PhysX remains paused for the shift window.</summary>
        public static bool IsPhysicsPausedForShift => _instance != null && _instance._physicsPauseActive;

        /// <summary>Last committed shift event payload.</summary>
        public static OriginShiftEventData LastShiftEvent => _lastShiftEvent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            _lastShiftEvent = default;
            _originShiftListeners.Clear();
            OnWorldShift = null;
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

        /// <summary>
        /// Registers a listener for committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void RegisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _originShiftListeners.Count; i++)
            {
                if (ReferenceEquals(_originShiftListeners[i], listener))
                    return;
            }

            _originShiftListeners.Add(listener);
        }

        /// <summary>
        /// Unregisters a listener from committed floating-origin shifts.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void UnregisterListener(IOriginShiftListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _originShiftListeners.Count; i++)
            {
                if (!ReferenceEquals(_originShiftListeners[i], listener))
                    continue;

                _originShiftListeners.RemoveAt(i);
                break;
            }
        }

        /// <summary>
        /// Marks the root-transform cache dirty so the next shift rebuilds it on the cold path.
        /// </summary>
        public static void MarkShiftTargetsDirty()
        {
            if (_instance == null)
                return;

            _instance._shiftTargetsDirty = true;
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
            while (_instance != null && (_instance._isShiftInProgress || _instance._physicsPauseActive))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (watchdog++ > ShiftStabilityWatchdogFrames)
                {
                    Debug.LogError(
                        $"[FloatingOrigin] WaitForShiftStabilityAsync timed out after {ShiftStabilityWatchdogFrames} frames. " +
                        $"shiftInProgress={_instance._isShiftInProgress} physicsPause={_instance._physicsPauseActive}");
                    break;
                }

                await Awaitable.NextFrameAsync(cancellationToken: cancellationToken);
            }

            Vector3 currentOffset = CurrentTotalOffset;
            uint currentSequence = CurrentShiftSequence;
            return new OriginShiftEventData(Vector3.zero, currentOffset, currentOffset, currentSequence, Time.frameCount);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
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

            if (_instance == this)
            {
                if (_physicsPauseActive)
                {
                    UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
                }

                _originShiftListeners.Clear();
                OnWorldShift = null;
                _instance = null;
            }
        }

        /// <summary>
        /// Monitors anchor distance and commits a synchronized floating-origin shift.
        /// </summary>
        /// <param name="deltaTime">Scaled tick delta supplied by the tick manager.</param>
        public void Tick(float deltaTime)
        {
            if (_physicsPauseActive)
            {
                if (Time.frameCount >= _physicsResumeFrame)
                    ResumePhysicsAfterShift();
                else
                    return;
            }

            if (_isShiftInProgress)
                return;

            if (_driftCheckScheduled && _driftCheckHandle.IsCompleted && ConsumeCompletedDriftCheck())
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
                ShiftWorld(anchorPosition);
                return;
            }

            if (anchorDistanceSqr > _thresholdSqr && _shiftDeadzoneArmed && isMovingAwayFromCenter)
            {
                _shiftDeadzoneArmed = false;
                ShiftWorld(anchorPosition);
            }
        }

        private void ShiftWorld(Vector3 shiftOffset)
        {
            if (shiftOffset.sqrMagnitude <= 0.0001f)
                return;

            _isShiftInProgress = true;
            bool trackedBodiesPrepared = false;
            bool trackedBodiesFinalized = false;
            PausePhysicsForShift();
            try
            {
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
                    handle.Complete();
                }

                PhysicsApplySystem.CommitTrackedBodiesForOriginShift(shiftOffset);

                Vector3 previousTotalOffset = TotalOffset;
                TotalOffset += shiftOffset;
                _shiftSequence++;
                _lastShiftEvent = new OriginShiftEventData(shiftOffset, previousTotalOffset, TotalOffset, _shiftSequence, Time.frameCount);

                PublishGlobalOffsets();
                ResyncCriticalEntityTrackersAfterShift();
                PhysicsApplySystem.FinalizeTrackedBodiesAfterOriginShift();
                trackedBodiesFinalized = true;
                WorldSpatialHashGrid.HandleOriginShift(_lastShiftEvent);
                BroadcastOriginShift(_lastShiftEvent);
                OnWorldShift?.Invoke(shiftOffset);
            }
            finally
            {
                if (trackedBodiesPrepared && !trackedBodiesFinalized)
                    PhysicsApplySystem.FinalizeTrackedBodiesAfterOriginShift();

                _isShiftInProgress = false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FloatingOrigin] shift={shiftOffset} seq={_shiftSequence} total={TotalOffset}");
#endif
        }

        private void PausePhysicsForShift()
        {
            _physicsSimulationModeBeforeShift = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            _physicsPauseActive = true;
            _physicsResumeFrame = Time.frameCount + 1;
        }

        private void ResumePhysicsAfterShift()
        {
            if (!_physicsPauseActive)
                return;

            UnityEngine.Physics.simulationMode = _physicsSimulationModeBeforeShift;
            _physicsPauseActive = false;
            _physicsResumeFrame = -1;
        }

        private void BroadcastOriginShift(in OriginShiftEventData shiftData)
        {
            for (int i = _originShiftListeners.Count - 1; i >= 0; i--)
            {
                IOriginShiftListener listener = _originShiftListeners[i];
                UnityEngine.Object unityListener = listener as UnityEngine.Object;
                if (listener == null || unityListener == null)
                {
                    _originShiftListeners.RemoveAt(i);
                    continue;
                }

                listener.OnOriginShift(in shiftData);
            }
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
            _driftCheckRuntimePositions[writeIndex] = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            _driftCheckAbsolutePositions[writeIndex] = tracker.AbsolutePosition;
            return writeIndex + 1;
        }

        private bool ConsumeCompletedDriftCheck()
        {
            _driftCheckHandle.Complete();
            _driftCheckHandle = default;
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

            _shiftDeadzoneArmed = false;
            ShiftWorld(forcedShiftOffset);
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
        }

        private void DisposeDriftCheckState()
        {
            JobHandle dependency = _driftCheckScheduled ? _driftCheckHandle : default;
            if (_driftCheckRuntimePositions.IsCreated)
            {
                _driftCheckRuntimePositions.Dispose(dependency);
                _driftCheckRuntimePositions = default;
            }

            if (_driftCheckAbsolutePositions.IsCreated)
            {
                _driftCheckAbsolutePositions.Dispose(dependency);
                _driftCheckAbsolutePositions = default;
            }

            if (_driftCheckInvalidMask.IsCreated)
            {
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

            if (!Application.isPlaying)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _isRegistered = true;
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
            TryPrepareShiftTargets();
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            _shiftTargetsDirty = true;
            TryPrepareShiftTargets();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            _shiftTargetsDirty = true;
            TryPrepareShiftTargets();
        }

        private void TryPrepareShiftTargets()
        {
            if (!Application.isPlaying || _isShiftInProgress || _physicsPauseActive || !_shiftTargetsDirty)
                return;

            RebuildShiftTargetCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshThresholdCache();
        }
#endif
    }
}
