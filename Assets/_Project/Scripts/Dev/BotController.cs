#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Dev
{
    /// <summary>
    /// Headless expedition driver for chunk-generation and memory soak testing.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Bot Controller")]
    public sealed class BotController : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float DefaultTargetDistanceMeters = 10000f;
        private const float DefaultAccelerationMetersPerSecondSq = 12f;
        private const float ResolveIntervalSeconds = 1f;
        private const float SampleIntervalSeconds = 1f;
        private const float MaxRuntimeSeconds = 1800f;
        private const float MinimumAllowedFps = 45f;
        private const float MaxLowFpsSeconds = 10f;
        private const float BytesToMegabytes = 1f / (1024f * 1024f);
        private const int MaxAllowedLodChangesPerFrame = 50;
        private const int MaxExpeditionSamples = 1802;
        private const int MaxEmergencyTickOperations = 64;
        private const int CsvNumberBufferLength = 32;
        public const int ExpeditionSampleStrideBytes = 64;
        private const string CsvFileName = "bot_expedition.csv";
        private const string FailureNone = "NONE";
        private const string FailureLowFps = "FPS_UNDER_45_FOR_10S";
        private const string FailureLodBurst = "LOD_TRANSITIONS_OVER_50";
        private const string FailureEmergencyTimeout = "BOT_EMERGENCY_TIMEOUT";
        private const string CsvHeader = "elapsed_seconds,distance_est_meters,fps,mono_used_mb,total_allocated_mb,total_reserved_mb,graphics_driver_allocated_mb,gc_thread_allocated_bytes,gc_gen0,gc_gen1,gc_gen2,lod_changes_frame,pos_x,pos_y,pos_z";

        // COLD ALLOC: WaitCallback[1] — background CSV flush entry point — owner: BotController
        private static readonly WaitCallback _csvFlushCallback = ExecuteCsvFlush;
        private static readonly Encoding CsvEncoding = new UTF8Encoding(false);

        [StructLayout(LayoutKind.Explicit, Size = ExpeditionSampleStrideBytes)]
        private struct ExpeditionSample
        {
            [FieldOffset(0)] public float ElapsedSeconds;
            [FieldOffset(4)] public float EstimatedDistanceMeters;
            [FieldOffset(8)] public float Fps;
            [FieldOffset(12)] public float MonoUsedMb;
            [FieldOffset(16)] public float TotalAllocatedMb;
            [FieldOffset(20)] public float TotalReservedMb;
            [FieldOffset(24)] public float GraphicsDriverAllocatedMb;
            [FieldOffset(28)] public int GcThreadAllocatedBytes;
            [FieldOffset(32)] public int GcGen0;
            [FieldOffset(36)] public int GcGen1;
            [FieldOffset(40)] public int GcGen2;
            [FieldOffset(44)] public int LodChangesFrame;
            [FieldOffset(48)] public float PositionX;
            [FieldOffset(52)] public float PositionY;
            [FieldOffset(56)] public float PositionZ;
            [FieldOffset(60)] public int Reserved;
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
        // COLD ALLOC: char[32] — background CSV numeric formatting scratch — owner: BotController
        private readonly char[] _csvNumberBuffer = new char[CsvNumberBufferLength];

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
        private int _emergencyTickOperations;
        private bool _registered;
        private bool _registeredHotSwap;
        private bool _running;
        private bool _csvDirty;
        private bool _hasFailure;
        private bool _hasDriveCommand;
        private IPlayerRuntimeContext _playerRuntime;
        private IPhysicsService _physicsService;
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

        /// <summary>
        /// Runtime-resolved telemetry sample stride for cache-line guard tests.
        /// </summary>
        public static int ResolvedExpeditionSampleStrideBytes => Marshal.SizeOf<ExpeditionSample>();

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _playerBody);
            CachePlayerRuntimeCold();
            CachePhysicsServiceCold();
        }

        private void OnEnable()
        {
            CachePlayerRuntimeCold();
            CachePhysicsServiceCold();
            TryRegisterHotSwapListener();
            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);
            if (_autoStart)
                StartExpedition();
        }

        private void OnDisable()
        {
            StopExpedition();
            TryUnregisterHotSwapListener();
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registered = false;
            }
        }

        /// <summary>
        /// Updates the simulated WASD command. Inputs are branch-sanitized to the keyboard axis range.
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
            if (!TryResolvePlayerRuntimePosition(out _startPosition))
                return;

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
            _csvDirectoryPath = HectonPersistentPathPolicy.RootPath;
            CsvPath = HectonPersistentPathPolicy.CombineFile(CsvFileName);
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

            _emergencyTickOperations = 0;
            if (!TryAdvanceEmergencyTick())
                return;

            float safeDeltaTime = SanitizeNonNegative(deltaTime);
            _elapsedSeconds += safeDeltaTime;
            if (!TryAdvanceEmergencyTick())
                return;

            ResolvePlayerBody(force: false, deltaTime: safeDeltaTime);
            if (_playerBody == null)
            {
                StopExpedition();
                return;
            }

            if (!TryAdvanceEmergencyTick())
                return;

            if (_hasDriveCommand && _accelerationMetersPerSecondSq > 0f)
            {
                float acceleration = _accelerationMetersPerSecondSq * _driveScale;
                Vector3 command = _driveDirection * acceleration;
                _physicsService?.QueueForce(
                    _playerBody,
                    command,
                    ForceMode.Acceleration);
            }

            _sampleFrameCount++;
            TrackLodTransitionPeak();
            _sampleTimer += safeDeltaTime;
            if (!TryAdvanceEmergencyTick())
                return;

            if (_sampleTimer >= SampleIntervalSeconds)
            {
                RecordCsvSample(_sampleTimer, _sampleFrameCount);
                _sampleTimer = 0f;
                _sampleFrameCount = 0;
            }

            if (!TryAdvanceEmergencyTick())
                return;

            if (!TryResolvePlayerRuntimePosition(out Vector3 currentPosition))
            {
                StopExpedition();
                return;
            }

            float traveledSq = DistanceSq(_startPosition, currentPosition);
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
            IPlayerRuntimeContext player = _playerRuntime;
            if (player != null && player.PlayerRigidbody != null)
            {
                _playerBody = player.PlayerRigidbody;
                return;
            }
        }

        private void CachePlayerRuntimeCold()
        {
            _playerRuntime = Hecton8.Core.GlobalRegistry.Player;
        }

        private void CachePhysicsServiceCold()
        {
            _physicsService = GlobalRegistry.Physics;
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntime = currentService as IPlayerRuntimeContext;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Physics)
                _physicsService = currentService as IPhysicsService;
        }

        private void RecordCsvSample(float sampleSeconds, int sampleFrames)
        {
            if (_playerBody == null || _sampleCount >= MaxExpeditionSamples)
                return;

            if (!TryResolvePlayerRuntimePosition(out Vector3 position))
                return;

            ref ExpeditionSample sample = ref _samples[_sampleCount];
            sample.ElapsedSeconds = _elapsedSeconds;
            sample.EstimatedDistanceMeters = DominantAxisDistance(position, _startPosition);
            sample.Fps = sampleSeconds > 0.0001f ? sampleFrames / sampleSeconds : 0f;
            sample.MonoUsedMb = Profiler.GetMonoUsedSizeLong() * BytesToMegabytes;
            sample.TotalAllocatedMb = Profiler.GetTotalAllocatedMemoryLong() * BytesToMegabytes;
            sample.TotalReservedMb = Profiler.GetTotalReservedMemoryLong() * BytesToMegabytes;
            long graphicsDriverBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
            if (graphicsDriverBytes < 0L)
                graphicsDriverBytes = 0L;

            long threadAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - _startThreadAllocatedBytes;
            if (threadAllocatedBytes < 0L)
                threadAllocatedBytes = 0L;

            sample.GraphicsDriverAllocatedMb = graphicsDriverBytes * BytesToMegabytes;
            sample.GcThreadAllocatedBytes = ClampLongToInt(threadAllocatedBytes);
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
            float absX = Abs(_wasdCommand.x);
            float absY = Abs(_wasdCommand.y);
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

            bool useForwardAxis = absY >= absX;
            if (useForwardAxis)
            {
                Vector3 forward = DominantAxisOrFallback(cachedTransform.forward, Vector3.forward);
                _driveDirection = _wasdCommand.y >= 0f ? forward : -forward;
            }
            else
            {
                Vector3 right = DominantAxisOrFallback(cachedTransform.right, Vector3.right);
                _driveDirection = _wasdCommand.x >= 0f ? right : -right;
            }

            _driveScale = math.select(absX, absY, useForwardAxis);
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

                char[] numberBuffer = _csvNumberBuffer;
                using (StreamWriter writer = new StreamWriter(CsvPath, append: false, CsvEncoding))
                {
                    writer.WriteLine(CsvHeader);
                    int sampleCount = _sampleCount;
                    if (sampleCount > MaxExpeditionSamples)
                        sampleCount = MaxExpeditionSamples;

                    for (int i = 0; i < sampleCount; i++)
                    {
                        ExpeditionSample sample = _samples[i];
                        WriteFloatCsv(writer, sample.ElapsedSeconds, "F3", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.EstimatedDistanceMeters, "F3", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.Fps, "F2", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.MonoUsedMb, "F2", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.TotalAllocatedMb, "F2", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.TotalReservedMb, "F2", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.GraphicsDriverAllocatedMb, "F2", numberBuffer);
                        writer.Write(',');
                        WriteIntCsv(writer, sample.GcThreadAllocatedBytes, numberBuffer);
                        writer.Write(',');
                        WriteIntCsv(writer, sample.GcGen0, numberBuffer);
                        writer.Write(',');
                        WriteIntCsv(writer, sample.GcGen1, numberBuffer);
                        writer.Write(',');
                        WriteIntCsv(writer, sample.GcGen2, numberBuffer);
                        writer.Write(',');
                        WriteIntCsv(writer, sample.LodChangesFrame, numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.PositionX, "F3", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.PositionY, "F3", numberBuffer);
                        writer.Write(',');
                        WriteFloatCsv(writer, sample.PositionZ, "F3", numberBuffer);
                        writer.WriteLine();
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

        private bool TryResolvePlayerRuntimePosition(out Vector3 position)
        {
            position = default;
            IPlayerRuntimeContext player = _playerRuntime;
            if (player != null &&
                player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose) &&
                math.all(math.isfinite(pose.RuntimePosition)))
            {
                position = new Vector3(pose.RuntimePosition.x, pose.RuntimePosition.y, pose.RuntimePosition.z);
                return true;
            }

            Transform playerTransform = player != null ? player.PlayerTransform : _cachedTransform;
            if (playerTransform == null)
                return false;

            Vector3 transformPosition = playerTransform.position;
            if (!float.IsFinite(transformPosition.x) || !float.IsFinite(transformPosition.y) || !float.IsFinite(transformPosition.z))
                return false;

            position = transformPosition;
            return true;
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

        private static float DominantAxisDistance(Vector3 value, Vector3 origin)
        {
            float deltaX = value.x - origin.x;
            float deltaY = value.y - origin.y;
            float deltaZ = value.z - origin.z;
            if (!float.IsFinite(deltaX) || !float.IsFinite(deltaY) || !float.IsFinite(deltaZ))
                return 0f;

            float absX = Abs(deltaX);
            float absY = Abs(deltaY);
            float absZ = Abs(deltaZ);
            float max = absX >= absY ? absX : absY;
            return max >= absZ ? max : absZ;
        }

        private static void WriteFloatCsv(StreamWriter writer, float value, string format, char[] buffer)
        {
            if (!float.IsFinite(value))
            {
                writer.Write('0');
                return;
            }

            if (value.TryFormat(buffer.AsSpan(), out int charsWritten, format.AsSpan(), CultureInfo.InvariantCulture))
            {
                writer.Write(buffer, 0, charsWritten);
                return;
            }

            writer.Write('0');
        }

        private static void WriteIntCsv(StreamWriter writer, int value, char[] buffer)
        {
            if (value.TryFormat(buffer.AsSpan(), out int charsWritten, ReadOnlySpan<char>.Empty, CultureInfo.InvariantCulture))
            {
                writer.Write(buffer, 0, charsWritten);
                return;
            }

            writer.Write('0');
        }

        private static Vector3 DominantAxisOrFallback(Vector3 value, Vector3 fallback)
        {
            if (!float.IsFinite(value.x) || !float.IsFinite(value.y) || !float.IsFinite(value.z))
                return fallback;

            float absX = Abs(value.x);
            float absY = Abs(value.y);
            float absZ = Abs(value.z);
            if (absX >= absY && absX >= absZ)
                return value.x >= 0f ? Vector3.right : Vector3.left;

            if (absY >= absZ)
                return value.y >= 0f ? Vector3.up : Vector3.down;

            return value.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private static float Abs(float value)
        {
            return value < 0f ? -value : value;
        }

        private bool TryAdvanceEmergencyTick()
        {
            _emergencyTickOperations++;
            if (_emergencyTickOperations <= MaxEmergencyTickOperations)
                return true;

            _hasFailure = true;
            _failureReason = FailureEmergencyTimeout;
            StopExpedition();
            return false;
        }
    }
}
#endif
