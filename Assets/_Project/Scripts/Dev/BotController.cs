#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Dev
{
    /// <summary>
    /// Headless expedition driver for chunk-generation and memory soak testing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Bot Controller")]
    public sealed class BotController : MonoBehaviour, IUpdatable
    {
        private const float DefaultTargetDistanceMeters = 10000f;
        private const float DefaultAccelerationMetersPerSecondSq = 12f;
        private const float ResolveIntervalSeconds = 1f;
        private const float SampleIntervalSeconds = 1f;
        private const float MaxRuntimeSeconds = 1800f;
        private const float MinimumAllowedFps = 45f;
        private const float MaxLowFpsSeconds = 10f;
        private const int MaxAllowedLodChangesPerFrame = 50;
        private const int MaxExpeditionSamples = 1802;
        private const string CsvFileName = "bot_expedition.csv";
        private const string FailureNone = "NONE";
        private const string FailureLowFps = "FPS_UNDER_45_FOR_10S";
        private const string FailureLodBurst = "LOD_TRANSITIONS_OVER_50";
        private const string CsvHeader = "elapsed_seconds,distance_est_meters,fps,mono_used_mb,total_allocated_mb,total_reserved_mb,graphics_driver_allocated_mb,gc_thread_allocated_bytes,gc_gen0,gc_gen1,gc_gen2,lod_changes_frame,pos_x,pos_y,pos_z";

        // COLD ALLOC: WaitCallback[1] — background CSV flush entry point — owner: BotController
        private static readonly WaitCallback _csvFlushCallback = ExecuteCsvFlush;

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        private struct ExpeditionSample
        {
            public float ElapsedSeconds;
            public float EstimatedDistanceMeters;
            public float Fps;
            public float MonoUsedMb;
            public float TotalAllocatedMb;
            public float TotalReservedMb;
            public float GraphicsDriverAllocatedMb;
            public int GcThreadAllocatedBytes;
            public int GcGen0;
            public int GcGen1;
            public int GcGen2;
            public int LodChangesFrame;
            public float PositionX;
            public float PositionY;
            public float PositionZ;
            public int Reserved;
        }

        [SerializeField, Tooltip("Starts the QA expedition automatically when the component registers.")]
        private bool _autoStart;

        [SerializeField, Min(1f), Tooltip("Target swim distance before the bot stops the expedition.")]
        private float _targetDistanceMeters = DefaultTargetDistanceMeters;

        [SerializeField, Min(0f), Tooltip("Acceleration applied through the physics force router.")]
        private float _accelerationMetersPerSecondSq = DefaultAccelerationMetersPerSecondSq;

        [SerializeField, Tooltip("Simulated WASD command. X is strafe, Y is forward.")]
        private Vector2 _wasdCommand = new Vector2(0f, 1f);

        // COLD ALLOC: ExpeditionSample[1802] — 1Hz QA expedition telemetry buffer — owner: BotController
        private readonly ExpeditionSample[] _samples = new ExpeditionSample[MaxExpeditionSamples];

        private Transform _cachedTransform;
        private Rigidbody _playerBody;
        private LODSystemManager _lodSystem;
        private Vector3 _startPosition;
        private Vector3 _driveDirection;
        private float _elapsedSeconds;
        private float _sampleTimer;
        private float _resolveTimer;
        private float _lowFpsSeconds;
        private float _driveScale;
        private float _targetDistanceMetersSq;
        private long _startThreadAllocatedBytes;
        private int _sampleCount;
        private int _sampleFrameCount;
        private int _csvFlushQueued;
        private int _startGen0;
        private int _startGen1;
        private int _startGen2;
        private int _maxLodChangesSinceSample;
        private bool _registered;
        private bool _running;
        private bool _csvDirty;
        private bool _hasFailure;
        private bool _hasDriveCommand;
        private string _csvDirectoryPath;
        private string _failureReason = FailureNone;

        /// <summary>
        /// True while the headless expedition is driving the registered player body.
        /// </summary>
        public bool IsRunning => _running;

        /// <summary>
        /// True when the last expedition violated a stability threshold.
        /// </summary>
        public bool HasFailure => _hasFailure;

        /// <summary>
        /// Stable failure code for automation.
        /// </summary>
        public string FailureReason => _failureReason;

        /// <summary>
        /// Number of telemetry samples captured for the current or last expedition.
        /// </summary>
        public int SampleCount => _sampleCount;

        /// <summary>
        /// Absolute path where the cold-flushed expedition CSV is written.
        /// </summary>
        public string CsvPath { get; private set; }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _playerBody);
        }

        private void OnEnable()
        {
            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (_autoStart)
                StartExpedition();
        }

        private void OnDisable()
        {
            StopExpedition();
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registered = false;
            }
        }

        /// <summary>
        /// Updates the simulated WASD command. Inputs are clamped to the keyboard axis range.
        /// </summary>
        public void SetMoveCommand(float horizontal, float vertical)
        {
            _wasdCommand = new Vector2(SanitizeAxis(horizontal), SanitizeAxis(vertical));
            ResolveDriveCommand();
        }

        /// <summary>
        /// Overrides the target expedition distance for shorter automation runs.
        /// </summary>
        public void SetTargetDistanceMeters(float targetDistanceMeters)
        {
            _targetDistanceMeters = SanitizePositive(targetDistanceMeters, DefaultTargetDistanceMeters);
            _targetDistanceMetersSq = _targetDistanceMeters * _targetDistanceMeters;
        }

        /// <summary>
        /// Starts the 10km chunk-generation soak run if a player body is registered.
        /// </summary>
        public void StartExpedition()
        {
            if (_running || Volatile.Read(ref _csvFlushQueued) != 0)
                return;

            ResolvePlayerBody(force: true, deltaTime: 0f);
            if (_playerBody == null)
                return;

            ResolveDriveCommand();
            _lodSystem = LODSystemManager.Instance;
            _startPosition = _playerBody.position;
            _targetDistanceMetersSq = ResolveTargetDistanceMetersSq();
            _elapsedSeconds = 0f;
            _sampleTimer = 0f;
            _resolveTimer = 0f;
            _lowFpsSeconds = 0f;
            _startThreadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            _sampleCount = 0;
            _sampleFrameCount = 0;
            _startGen0 = GC.CollectionCount(0);
            _startGen1 = GC.CollectionCount(1);
            _startGen2 = GC.CollectionCount(2);
            _maxLodChangesSinceSample = 0;
            _csvDirty = false;
            _hasFailure = false;
            _hasDriveCommand = _driveScale > 0f;
            _failureReason = FailureNone;
            _csvDirectoryPath = Application.persistentDataPath;
            CsvPath = Path.Combine(_csvDirectoryPath, CsvFileName);
            _running = true;
        }

        /// <summary>
        /// Stops the expedition and writes buffered samples to disk outside the gameplay tick.
        /// </summary>
        public void StopExpedition()
        {
            _running = false;
            QueueCsvFlushCold();
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_running)
                return;

            float safeDeltaTime = SanitizeNonNegative(deltaTime);
            _elapsedSeconds += safeDeltaTime;
            ResolvePlayerBody(force: false, deltaTime: safeDeltaTime);
            if (_playerBody == null)
            {
                StopExpedition();
                return;
            }

            if (_hasDriveCommand && _accelerationMetersPerSecondSq > 0f)
            {
                float acceleration = _accelerationMetersPerSecondSq * _driveScale;
                Vector3 command = _driveDirection * acceleration;
                PhysicsForceRouter.QueueForce(
                    _playerBody,
                    command,
                    ForceMode.Acceleration);
            }

            _sampleFrameCount++;
            TrackLodTransitionPeak();
            _sampleTimer += safeDeltaTime;
            if (_sampleTimer >= SampleIntervalSeconds)
            {
                RecordCsvSample(_sampleTimer, _sampleFrameCount);
                _sampleTimer = 0f;
                _sampleFrameCount = 0;
            }

            float traveledSq = DistanceSq(_startPosition, _playerBody.position);
            if (traveledSq >= _targetDistanceMetersSq || _elapsedSeconds >= MaxRuntimeSeconds)
                StopExpedition();
        }

        private void ResolvePlayerBody(bool force, float deltaTime)
        {
            if (_playerBody != null)
                return;

            if (!force)
            {
                _resolveTimer -= deltaTime;
                if (_resolveTimer > 0f)
                    return;
            }

            _resolveTimer = ResolveIntervalSeconds;
            IPlayerRuntimeContext player = GlobalRegistry.Player;
            if (player != null && player.PlayerRigidbody != null)
            {
                _playerBody = player.PlayerRigidbody;
                return;
            }
        }

        private void RecordCsvSample(float sampleSeconds, int sampleFrames)
        {
            if (_playerBody == null || _sampleCount >= _samples.Length)
                return;

            Vector3 position = _playerBody.position;
            ref ExpeditionSample sample = ref _samples[_sampleCount];
            sample.ElapsedSeconds = _elapsedSeconds;
            sample.EstimatedDistanceMeters = ApproximateMagnitude(position - _startPosition);
            sample.Fps = sampleSeconds > 0.0001f ? sampleFrames / sampleSeconds : 0f;
            sample.MonoUsedMb = Profiler.GetMonoUsedSizeLong() * (1f / (1024f * 1024f));
            sample.TotalAllocatedMb = Profiler.GetTotalAllocatedMemoryLong() * (1f / (1024f * 1024f));
            sample.TotalReservedMb = Profiler.GetTotalReservedMemoryLong() * (1f / (1024f * 1024f));
            sample.GraphicsDriverAllocatedMb = Math.Max(0L, Profiler.GetAllocatedMemoryForGraphicsDriver()) * (1f / (1024f * 1024f));
            sample.GcThreadAllocatedBytes = ClampLongToInt(Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - _startThreadAllocatedBytes));
            sample.GcGen0 = GC.CollectionCount(0) - _startGen0;
            sample.GcGen1 = GC.CollectionCount(1) - _startGen1;
            sample.GcGen2 = GC.CollectionCount(2) - _startGen2;
            sample.LodChangesFrame = _maxLodChangesSinceSample;
            sample.PositionX = position.x;
            sample.PositionY = position.y;
            sample.PositionZ = position.z;
            _sampleCount++;
            _csvDirty = true;

            TrackFrameRateFailure(sample.Fps, sampleSeconds);
            TrackLodTransitionFailure(sample.LodChangesFrame);
            _maxLodChangesSinceSample = 0;
        }

        private void TrackLodTransitionPeak()
        {
            LODSystemManager lodSystem = _lodSystem;
            int lodChanges = lodSystem != null ? lodSystem.LastFrameTransitionCount : 0;
            if (lodChanges > _maxLodChangesSinceSample)
                _maxLodChangesSinceSample = lodChanges;
        }

        private void TrackFrameRateFailure(float fps, float sampleSeconds)
        {
            if (fps < MinimumAllowedFps)
            {
                _lowFpsSeconds += sampleSeconds;
                if (_lowFpsSeconds > MaxLowFpsSeconds)
                {
                    _hasFailure = true;
                    _failureReason = FailureLowFps;
                    StopExpedition();
                }

                return;
            }

            _lowFpsSeconds = 0f;
        }

        private void TrackLodTransitionFailure(int lodChangesFrame)
        {
            if (lodChangesFrame <= MaxAllowedLodChangesPerFrame)
                return;

            _hasFailure = true;
            _failureReason = FailureLodBurst;
            StopExpedition();
        }

        private void ResolveDriveCommand()
        {
            float absX = Mathf.Abs(_wasdCommand.x);
            float absY = Mathf.Abs(_wasdCommand.y);
            if (absX <= 0.000001f && absY <= 0.000001f)
            {
                _driveDirection = Vector3.zero;
                _driveScale = 0f;
                _hasDriveCommand = false;
                return;
            }

            Transform cachedTransform = _cachedTransform;
            if (cachedTransform == null)
            {
                _driveDirection = Vector3.zero;
                _driveScale = 0f;
                _hasDriveCommand = false;
                return;
            }

            if (absY >= absX)
            {
                _driveDirection = _wasdCommand.y >= 0f ? cachedTransform.forward : -cachedTransform.forward;
                _driveScale = absY;
            }
            else
            {
                _driveDirection = _wasdCommand.x >= 0f ? cachedTransform.right : -cachedTransform.right;
                _driveScale = absX;
            }

            _hasDriveCommand = true;
        }

        private void QueueCsvFlushCold()
        {
            if (!_csvDirty || _sampleCount <= 0)
                return;

            if (Interlocked.CompareExchange(ref _csvFlushQueued, 1, 0) != 0)
                return;

            if (!ThreadPool.UnsafeQueueUserWorkItem(_csvFlushCallback, this))
                Interlocked.Exchange(ref _csvFlushQueued, 0);
        }

        private static void ExecuteCsvFlush(object state)
        {
            BotController controller = state as BotController;
            if (controller == null)
                return;

            controller.FlushCsvSamplesCold();
        }

        private void FlushCsvSamplesCold()
        {
            if (!_csvDirty || _sampleCount <= 0)
            {
                Interlocked.Exchange(ref _csvFlushQueued, 0);
                return;
            }

            try
            {
                string directory = _csvDirectoryPath;
                if (string.IsNullOrEmpty(directory))
                    return;

                Directory.CreateDirectory(directory);
                if (string.IsNullOrEmpty(CsvPath))
                    CsvPath = Path.Combine(directory, CsvFileName);

                using (StreamWriter writer = new StreamWriter(CsvPath, append: false))
                {
                    writer.WriteLine(CsvHeader);
                    for (int i = 0; i < _sampleCount; i++)
                    {
                        ExpeditionSample sample = _samples[i];
                        writer.Write(sample.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.EstimatedDistanceMeters.ToString("F3", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.Fps.ToString("F2", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.MonoUsedMb.ToString("F2", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.TotalAllocatedMb.ToString("F2", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.TotalReservedMb.ToString("F2", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.GraphicsDriverAllocatedMb.ToString("F2", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.GcThreadAllocatedBytes.ToString(CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.GcGen0.ToString(CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.GcGen1.ToString(CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.GcGen2.ToString(CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.LodChangesFrame.ToString(CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.PositionX.ToString("F3", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.Write(sample.PositionY.ToString("F3", CultureInfo.InvariantCulture));
                        writer.Write(',');
                        writer.WriteLine(sample.PositionZ.ToString("F3", CultureInfo.InvariantCulture));
                    }
                }

                _csvDirty = false;
            }
            catch (Exception)
            {
            }
            finally
            {
                Interlocked.Exchange(ref _csvFlushQueued, 0);
            }
        }

        private static float DistanceSq(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            return delta.sqrMagnitude;
        }

        private float ResolveTargetDistanceMetersSq()
        {
            float targetDistance = SanitizePositive(_targetDistanceMeters, DefaultTargetDistanceMeters);
            return targetDistance * targetDistance;
        }

        private static int ClampLongToInt(long value)
        {
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static float SanitizeAxis(float value)
        {
            if (!float.IsFinite(value))
                return 0f;

            if (value > 1f)
                return 1f;

            return value < -1f ? -1f : value;
        }

        private static float SanitizePositive(float value, float fallback)
        {
            if (!float.IsFinite(value) || value < 1f)
                return fallback >= 1f ? fallback : 1f;

            return value;
        }

        private static float SanitizeNonNegative(float value)
        {
            if (!float.IsFinite(value) || value <= 0f)
                return 0f;

            return value;
        }

        private static float ApproximateMagnitude(Vector3 value)
        {
            float absX = Mathf.Abs(value.x);
            float absY = Mathf.Abs(value.y);
            float absZ = Mathf.Abs(value.z);
            float max = Mathf.Max(absX, Mathf.Max(absY, absZ));
            float min = Mathf.Min(absX, Mathf.Min(absY, absZ));
            float mid = absX + absY + absZ - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }
    }
}
#endif
