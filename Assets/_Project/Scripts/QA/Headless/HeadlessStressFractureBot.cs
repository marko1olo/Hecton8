using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.QA.Headless
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9100)]
    public sealed class HeadlessStressFractureBot : MonoBehaviour, IFastTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HeadlessStressFractureBotSignalPushDropCount;
        private const string AgentName = "HEADLESS_STRESS_FRACTURE_BOT";
        private const string RuntimeRootName = "[HeadlessStressFractureBot]";
        private const string CommandLineArg = "-h8fracturetest";
        private const string CommandLineFramesArg = "-h8fractureFrames";
        private const string CommandLineScratchMegabytesArg = "-h8fractureScratchMb";
        private const string CommandLineStartupTimeoutSecondsArg = "-h8fractureStartupTimeoutSeconds";
        private const string EnvironmentFlagName = "H8_FRACTURE_TEST";
        private const string EnvironmentFramesName = "H8_FRACTURE_FRAMES";
        private const string EnvironmentScratchMegabytesName = "H8_FRACTURE_SCRATCH_MB";
        private const string EnvironmentStartupTimeoutSecondsName = "H8_FRACTURE_STARTUP_TIMEOUT_SECONDS";
        private const string FlagRelativePath = "Temp/H8_FRACTURE_TEST.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/HeadlessStressFractureResult_HEADLESS_STRESS_FRACTURE_BOT.json";
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.bin";
        private const string BlackboxManifestRelativePath = "Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.json";
        private const string H8MemoryDumpRelativePath = "Docs/AgentLogs/H8Memory_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxHeaderSizeBytes = 16;
        private const int BlackboxHeaderOffsetMagic = 0;
        private const int BlackboxHeaderOffsetValidEntryCount = 4;
        private const int BlackboxHeaderOffsetEntrySizeBytes = 8;
        private const int BlackboxHeaderOffsetCursor = 12;
        private const int BlackboxEntrySizeBytes = 64;
        private const int BlackboxEntryOffsetFrame = 0;
        private const int BlackboxEntryOffsetExtremeFrame = 4;
        private const int BlackboxEntryOffsetShiftSequence = 8;
        private const int BlackboxEntryOffsetEventHash = 12;
        private const int BlackboxEntryOffsetNativeBytes = 16;
        private const int BlackboxEntryOffsetH8Bytes = 24;
        private const int BlackboxEntryOffsetNativeAllocations = 32;
        private const int BlackboxEntryOffsetH8Allocations = 36;
        private const int BlackboxEntryOffsetDispatcherPhaseMs = 40;
        private const int BlackboxEntryOffsetDataVaultFragmentation = 44;
        private const int BlackboxEntryOffsetLastShiftMetersX = 48;
        private const int BlackboxEntryOffsetLastShiftMetersY = 52;
        private const int BlackboxEntryOffsetLastShiftMetersZ = 56;
        private const int BlackboxEntryOffsetFlags = 60;
        private const int DefaultTargetFrames = 50000;
        private const int BytesPerMegabyte = 1024 * 1024;
        private const int DefaultScratchMegabytes = 50;
        private const int MinScratchMegabytes = 8;
        private const int MaxScratchMegabytes = 256;
        private const int ResultSchemaVersion = 8;
        private const int DefaultStartupTimeoutSeconds = 60;
        private const int MinStartupTimeoutSeconds = 5;
        private const int MaxStartupTimeoutSeconds = 600;
        private const int ActivationSourceNone = 0;
        private const int ActivationSourceCommandLine = 1;
        private const int ActivationSourceEnvironment = 2;
        private const int ActivationSourceFlagFile = 3;
        private const int WarmupFrames = 120;
        private const int AupShiftIntervalFrames = 15;
        private const int AupSnapFenceFrames = 300;
        private const int BlackboxMemorySnapshotIntervalFrames = 30;
        private const int ChunkUnloadIntervalFrames = 900;
        private const int ChunkLeakGraceFrames = 180;
        private const int BlackboxFlagScratchActiveBit = 0;
        private const int BlackboxFlagBaselineCapturedBit = 1;
        private const int BlackboxFlagChunkUnloadPendingBit = 2;
        private const int BlackboxFlagEcosystemStressIssuedBit = 3;
        private const int BlackboxFlagDataVaultMissingBit = 4;
        private const int BlackboxFlagAupSnapFenceActiveBit = 5;
        private const int BlackboxFlagMemorySampleFreshBit = 6;
        private const long LeakToleranceBytes = 1024L * 1024L;
        private const SystemID OwnerSystemId = SystemID.QAHeadless;
        private const BufferID BlackboxBufferId = BufferID.QAHeadlessStressFractureBlackBoxRing;
        private const BufferID ScratchBlockBufferId = BufferID.QAHeadlessStressFractureScratchBlock;
        private const double FlagMaxAgeSeconds = 10800.0;
        private const double FlagFutureSkewToleranceSeconds = 300.0;
        private const float TimeDilationScalar = 100f;
        private const float StallThresholdMilliseconds = 16f;
        private const float NativeBytesToMegabytes = 1f / (1024f * 1024f);
        private const ushort RequestedBoidCount = 10000;
        private const uint RunnerHash = 0x48534642u;
        private const uint BlackboxMagic = 0x48534642u;
        private const uint SuccessHash = 0x53554343u;
        private const uint StallHash = 0x4a53544cu;
        private const uint NanHash = 0x4e414e50u;
        private const uint LeakHash = 0x4c45414bu;
        private const uint AllocationDeniedHash = 0x414c444eu;
        private const uint TimeoutHash = 0x54494d45u;
        private const uint AupShiftHash = 0x41555053u;
        private const uint EcosystemStressHash = 0x45434f53u;
        private const uint FrameHash = 0x4652414du;
        private const uint DataVaultApiGapHash = 0x44564741u;
        private const string JobStallToken = "[FRACTURE_DETECTED: JOB_STALL]";
        private const string NanPoisoningToken = "[FRACTURE_DETECTED: NAN_POISONING]";
        private const string NativeLeakToken = "[FRACTURE_DETECTED: NATIVE_LEAK]";
        private const string AllocationDeniedToken = "[FRACTURE_DETECTED: MEMORY_ALLOC_DENIED]";
        private const string TimeoutToken = "[FRACTURE_DETECTED: BOOTSTRAP_TIMEOUT]";
        private const string SuccessToken = "SUCCESS";
        private const PriorityLayer LateSamplingLayer = PriorityLayer.UI;

        private static HeadlessStressFractureBot _instance;
        private static readonly double StopwatchTickToMilliseconds = 1000.0 / Stopwatch.Frequency;

        private VaultGenerationHandle<FractureTelemetryEntry> _blackboxHandle;
        private VaultGenerationHandle<byte> _scratchBlockHandle;
        private CancellationTokenSource _shutdownCts;
        private IDataVault _dataVault;
        private IEcosystemDirectorService _ecosystemDirector;
        private ITickDispatcher _dispatcher;
        private Camera[] _cameraScratch;
        private int[] _cameraCullingMaskScratch;
        private bool[] _cameraEnabledScratch;
        private string _resultPath;
        private string _blackboxPath;
        private string _blackboxManifestPath;
        private string _h8MemoryDumpPath;
        private int _targetFrames;
        private int _scratchBlockBytes;
        private int _startupTimeoutSeconds;
        private int _activationSource;
        private int _blackboxCursor;
        private int _extremeFrame;
        private int _phaseFrame;
        private int _originShiftCount;
        private int _rigidbodyScanMissCount;
        private int _rigidbodyNanIndex = -1;
        private int _dataVaultApiGapLogged;
        private int _scratchBlockActive;
        private int _ecosystemStressIssued;
        private int _ecosystemDirectorReadyAtIssue;
        private int _chunkUnloadCheckFrame;
        private int _cameraScratchCount;
        private int _lastAupShiftExtremeFrame;
        private int _nativeAllocationBaselineCount;
        private int _h8AllocationBaselineCount;
        private int _scratchBaselineH8AllocationCount;
        private int _blackboxBinaryDumpSucceeded;
        private int _blackboxBinaryExistsAfterDump;
        private int _blackboxManifestDumpSucceeded;
        private uint _shiftSequence;
        private uint _lastFractureHash;
        private long _phaseStartTimestamp;
        private long _nativeBytesBaseline;
        private long _h8BytesBaseline;
        private long _dataVaultBytesBaseline;
        private long _scratchBaselineH8Bytes;
        private long _chunkUnloadNativeBytesBaseline;
        private long _chunkUnloadH8BytesBaseline;
        private long _chunkUnloadDataVaultBytesBaseline;
        private double _startupTime;
        private float _lastSimulationPhaseMs;
        private float _previousTimeDilationScalar = 1f;
        private float _staticHPhiMetric;
        private float _staticHPhiAupPrecisionIntegrity = 1f;
        private int _staticHPhiAupPrecisionSafe;
        private int _staticHPhiAupPrecisionRisk;
        private int _staticHPhiDispatcherContracts;
        private int _staticHPhiUnityUpdateMethods;
        private int _lastMemorySnapshotExtremeFrame;
        private float3 _lastShiftMeters;
        private MemorySnapshot _lastMemorySnapshot;
        private bool _started;
        private bool _finished;
        private bool _registeredFast;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _originListenerRegistered;
        private bool _runtimePolicyApplied;
        private bool _headlessTimeDilationApplied;
        private bool _baselineCaptured;
        private bool _chunkUnloadPending;
        private bool _memorySnapshotFreshForEntry;
        private bool _previousRunInBackground;
        private bool _previousAudioPause;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private float _previousAudioVolume;
        private LogType _previousLogFilter;
        private int _activationFlagDeletedAtStartup;
        private int _headlessTimeDilationRestored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (_instance != null || !ShouldRunStatic())
                return;

            GameObject root = new GameObject(RuntimeRootName);
            _instance = root.AddComponent<HeadlessStressFractureBot>();
            DontDestroyOnLoad(root);
        }

        private void Start()
        {
            if (!ShouldRunStatic())
            {
                Destroy(gameObject);
                return;
            }

            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            // COLD ALLOC: CancellationTokenSource[1] - startup await cancellation owner - owner: HeadlessStressFractureBot
            _shutdownCts = new CancellationTokenSource();
            _ = RunStartupAsync(_shutdownCts.Token);
        }

        private async Awaitable RunStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                InitializeColdState();
                await WaitForDispatcherAndStart(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (!_finished)
                    FailAndQuit(1, TimeoutHash, exception.GetType().Name);
            }
        }

        private void OnDestroy()
        {
            if (_shutdownCts != null)
            {
                _shutdownCts.Cancel();
                _shutdownCts.Dispose();
                _shutdownCts = null;
            }

            ReleaseScratchBlock();
            UnregisterRuntimeHooks();
            RestoreRuntimePolicy();
            ReleaseBlackbox();
            _cameraScratch = null;
            _cameraCullingMaskScratch = null;
            _cameraEnabledScratch = null;
            if (_instance == this)
                _instance = null;
        }

        public void FastTick(float deltaTime)
        {
            if (!_started || _finished)
                return;

            long phaseTimestamp = Stopwatch.GetTimestamp();
            int dispatcherFrame = SystemDispatcher.CurrentFrameIndex;
            if (_phaseStartTimestamp == 0L || _phaseFrame != dispatcherFrame)
            {
                _phaseStartTimestamp = phaseTimestamp;
                _phaseFrame = dispatcherFrame;
            }

            _extremeFrame++;

            if (_ecosystemStressIssued == 0)
                IssueEcosystemStressRequest();

            if (!_baselineCaptured && _extremeFrame >= WarmupFrames && !IsScratchBlockActive())
                CaptureNativeBaselines();

            if (_baselineCaptured)
            {
                PulseScratchMemory();
                if (_finished)
                    return;

                CheckChunkLeakWindow();
                if (_finished)
                    return;
            }

            if (_extremeFrame % AupShiftIntervalFrames == 0)
                EmitAupShift();

            if (_extremeFrame % ChunkUnloadIntervalFrames == 0)
                EmitSyntheticChunkUnload();

            ScanRigidbodyAups();
            if (_finished)
                return;

            RecordBlackbox(FrameHash);

            if (_extremeFrame >= _targetFrames)
                CompleteAndQuit();
        }

        public void ColdTick()
        {
            if (!_started || _finished)
                return;

            CacheServices();
            if (!_baselineCaptured && Time.realtimeSinceStartupAsDouble - _startupTime > _startupTimeoutSeconds)
                FailAndQuit(1, TimeoutHash, TimeoutToken);
        }

        public void LateFrameTick()
        {
            if (!_started || _finished)
                return;

            if (_phaseFrame != SystemDispatcher.CurrentFrameIndex || _phaseStartTimestamp == 0L)
                return;

            long elapsedTicks = Stopwatch.GetTimestamp() - _phaseStartTimestamp;
            _lastSimulationPhaseMs = TicksToMilliseconds(elapsedTicks);
            if (_lastSimulationPhaseMs > StallThresholdMilliseconds)
                FailAndQuit(1, StallHash, JobStallToken);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            _originShiftCount++;
        }

        private async Awaitable WaitForDispatcherAndStart(CancellationToken cancellationToken)
        {
            _startupTime = Time.realtimeSinceStartupAsDouble;
            CacheServices();
            while (_dispatcher == null && Time.realtimeSinceStartupAsDouble - _startupTime <= _startupTimeoutSeconds)
            {
                if (cancellationToken.IsCancellationRequested || _finished)
                    return;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                CacheServices();
            }

            if (cancellationToken.IsCancellationRequested || _finished)
                return;

            if (_dispatcher == null)
            {
                FailAndQuit(1, TimeoutHash, TimeoutToken);
                return;
            }

            ForceHeadlessRuntimePolicy();
            CacheServices();
            EnsureBlackboxCold();
            RegisterRuntimeLanes();
            TryRegisterHotSwapListener();
            HectonFloatingOrigin.RegisterListener(this);
            _originListenerRegistered = true;
            ApplyHeadlessTimeDilation();
            if (!_started)
                FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
        }

        private void RegisterRuntimeLanes()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_registeredFast || _registeredCold || _registeredLate)
                UnregisterRuntimeLanes();

            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Core);
            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, LateSamplingLayer);
            _started = _registeredFast && _registeredCold && _registeredLate;
            if (!_started)
                UnregisterRuntimeLanes();
        }

        private void UnregisterRuntimeLanes()
        {
            if (_registeredFast)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Core);
                _registeredFast = false;
            }

            if (_registeredCold)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Core);
                _registeredCold = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, LateSamplingLayer);
                _registeredLate = false;
            }

            _started = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _dispatcher = currentService as ITickDispatcher;
                if (currentService == null)
                {
                    _registeredFast = false;
                    _registeredCold = false;
                    _registeredLate = false;
                    _started = false;
                    return;
                }

                if (!_finished && isActiveAndEnabled)
                {
                    RegisterRuntimeLanes();
                    if (_started)
                        ApplyHeadlessTimeDilation();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                if (!ReferenceEquals(_dataVault, currentService))
                {
                    ReleaseScratchBlock();
                    ReleaseBlackbox();
                    _dataVault = currentService as IDataVault;
                    if (!_finished)
                        EnsureBlackboxCold();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.EcosystemDirector)
                _ecosystemDirector = currentService as IEcosystemDirectorService;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
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

        private void InitializeColdState()
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            int frameFallback = TryReadEnvironmentInt(EnvironmentFramesName, DefaultTargetFrames);
            int scratchFallbackMegabytes = TryReadEnvironmentInt(EnvironmentScratchMegabytesName, DefaultScratchMegabytes);
            int startupTimeoutFallbackSeconds = TryReadEnvironmentInt(EnvironmentStartupTimeoutSecondsName, DefaultStartupTimeoutSeconds);
            _activationSource = ResolveActivationSourceStatic();
            _activationFlagDeletedAtStartup = TryDeleteActivationFlagCold() ? 1 : 0;
            _targetFrames = math.max(1, TryReadInt(args, CommandLineFramesArg, frameFallback));
            int scratchMegabytes = math.clamp(
                TryReadInt(args, CommandLineScratchMegabytesArg, scratchFallbackMegabytes),
                MinScratchMegabytes,
                MaxScratchMegabytes);
            _scratchBlockBytes = scratchMegabytes * BytesPerMegabyte;
            _startupTimeoutSeconds = math.clamp(
                TryReadInt(args, CommandLineStartupTimeoutSecondsArg, startupTimeoutFallbackSeconds),
                MinStartupTimeoutSeconds,
                MaxStartupTimeoutSeconds);
            _resultPath = ResolveProjectPath(ResultRelativePath);
            _blackboxPath = ResolveProjectPath(BlackboxRelativePath);
            _blackboxManifestPath = ResolveProjectPath(BlackboxManifestRelativePath);
            _h8MemoryDumpPath = ResolveProjectPath(H8MemoryDumpRelativePath);
            _lastMemorySnapshotExtremeFrame = int.MinValue;
            EnsureParentDirectory(_resultPath);
            EnsureParentDirectory(_blackboxPath);
            EnsureParentDirectory(_blackboxManifestPath);
            EnsureParentDirectory(_h8MemoryDumpPath);
            TryDeleteFile(_resultPath);
            TryDeleteFile(_resultPath + ".tmp");
            TryDeleteFile(_blackboxPath);
            TryDeleteFile(_blackboxManifestPath);
            TryDeleteFile(_h8MemoryDumpPath);
            EnsureBlackboxCold();
            SignalBus<SectorDehydratedSignal>.EnsureInitialized();
            SignalBus<SectorResidencyHydratedSignal>.EnsureInitialized();
            SignalBus<SwarmDispersedSignal>.EnsureInitialized();
            HPhiStaticCounters staticHPhiCounters;
#if UNITY_EDITOR
            _staticHPhiMetric = ComputeStaticHPhiMetric(out staticHPhiCounters);
#else
            staticHPhiCounters = default;
            _staticHPhiMetric = 0f;
#endif
            _staticHPhiAupPrecisionSafe = staticHPhiCounters.AupPrecisionSafe;
            _staticHPhiAupPrecisionRisk = staticHPhiCounters.AupPrecisionRisk;
            _staticHPhiAupPrecisionIntegrity = CalculateAupPrecisionIntegrity(in staticHPhiCounters);
            _staticHPhiDispatcherContracts = CountDispatcherContracts(in staticHPhiCounters);
            _staticHPhiUnityUpdateMethods = staticHPhiCounters.UnityUpdateMethods;
            Hecton8.Core.H8Debug.LogWarning(FormatStaticHPhiLog(
                _staticHPhiMetric,
                _staticHPhiAupPrecisionIntegrity,
                _staticHPhiAupPrecisionSafe,
                _staticHPhiAupPrecisionRisk,
                _staticHPhiDispatcherContracts,
                _staticHPhiUnityUpdateMethods,
                _targetFrames,
                scratchMegabytes,
                _startupTimeoutSeconds));
        }

        private void ForceHeadlessRuntimePolicy()
        {
            if (_runtimePolicyApplied)
                return;

            _previousRunInBackground = Application.runInBackground;
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousAudioVolume = AudioListener.volume;
            _previousAudioPause = AudioListener.pause;
            _previousLogFilter = Debug.unityLogger.filterLogType;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            Debug.unityLogger.filterLogType = LogType.Warning;
            GlobalRegistry.RegisterScalabilityTierOverride(0);
            DisableActiveCamerasCold();
            _runtimePolicyApplied = true;
        }

        private void RestoreRuntimePolicy()
        {
            if (!_runtimePolicyApplied)
                return;

            Application.runInBackground = _previousRunInBackground;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
            AudioListener.volume = _previousAudioVolume;
            AudioListener.pause = _previousAudioPause;
            Debug.unityLogger.filterLogType = _previousLogFilter;
            RestoreActiveCamerasCold();
            GlobalRegistry.ClearScalabilityTierOverride();
            _runtimePolicyApplied = false;
        }

        private void DisableActiveCamerasCold()
        {
            int count = Camera.allCamerasCount;
            if (count <= 0)
                return;

            // COLD ALLOC: Camera[count] - reversible headless camera policy snapshot - owner: HeadlessStressFractureBot
            _cameraScratch = new Camera[count];
            // COLD ALLOC: int[count] - camera culling-mask restore snapshot - owner: HeadlessStressFractureBot
            _cameraCullingMaskScratch = new int[count];
            // COLD ALLOC: bool[count] - camera enabled-state restore snapshot - owner: HeadlessStressFractureBot
            _cameraEnabledScratch = new bool[count];
            int written = Camera.GetAllCameras(_cameraScratch);
            _cameraScratchCount = written;
            for (int i = 0; i < written; i++)
            {
                Camera camera = _cameraScratch[i];
                if (camera == null)
                    continue;

                _cameraCullingMaskScratch[i] = camera.cullingMask;
                _cameraEnabledScratch[i] = camera.enabled;
                camera.cullingMask = 0;
                camera.enabled = false;
            }
        }

        private void RestoreActiveCamerasCold()
        {
            if (_cameraScratch == null || _cameraCullingMaskScratch == null || _cameraEnabledScratch == null)
                return;

            int count = math.min(_cameraScratchCount, math.min(_cameraScratch.Length, math.min(_cameraCullingMaskScratch.Length, _cameraEnabledScratch.Length)));
            for (int i = 0; i < count; i++)
            {
                Camera camera = _cameraScratch[i];
                if (camera == null)
                    continue;

                camera.cullingMask = _cameraCullingMaskScratch[i];
                camera.enabled = _cameraEnabledScratch[i];
            }
        }

        private void CacheServices()
        {
            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.TickDispatcher;
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            if (_ecosystemDirector == null)
                _ecosystemDirector = GlobalRegistry.EcosystemDirector;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private void EnsureBlackboxCold()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            if (IsVaultHandleCreated(in _blackboxHandle) &&
                vault.TryReadOnlyHandle(in _blackboxHandle, out NativeArray<FractureTelemetryEntry>.ReadOnly blackbox) &&
                blackbox.IsCreated &&
                blackbox.Length >= BlackboxFrameCapacity)
            {
                return;
            }

            _blackboxHandle = vault.EnsureGenerationHandle<FractureTelemetryEntry>(
                BlackboxBufferId,
                BlackboxFrameCapacity,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
        }

        private void ReleaseBlackbox()
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in _blackboxHandle))
                vault.ReleaseBuffer(in _blackboxHandle);

            _blackboxHandle = default;
            _blackboxCursor = 0;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void CaptureNativeBaselines()
        {
            MemorySnapshot snapshot = CaptureMemorySnapshot();
            _nativeBytesBaseline = snapshot.NativeBytes;
            _nativeAllocationBaselineCount = snapshot.NativeAllocations;
            _h8BytesBaseline = snapshot.H8Bytes;
            _h8AllocationBaselineCount = snapshot.H8Allocations;
            _dataVaultBytesBaseline = snapshot.DataVaultBytes;
            _baselineCaptured = true;
        }

        private void IssueEcosystemStressRequest()
        {
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -128d, 0d));
            uint frame = SystemDispatcher.CurrentFrameId;
            long chunkId = unchecked((long)0x4853464200010001UL);
            SignalBus<SectorResidencyHydratedSignal>.TryPushTracked(new SectorResidencyHydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = frame,
                RadiusMetersQ = 1000,
                Flags = SectorResidencyHydratedSignal.FlagPinned,
                ResidencyState = 1
            }, ref s_x001HeadlessStressFractureBotSignalPushDropCount);
            SwarmDispersedSignal swarmSignal = new SwarmDispersedSignal
            {
                PositionAup = centerAup,
                RadiusMeters = 250f,
                Intensity01 = 1f,
                SourceId = RunnerHash,
                EstimatedBoidCount = RequestedBoidCount,
                Flags = 1,
                QualityTier = 255
            };
            SignalBus<SwarmDispersedSignal>.TryPushTracked(in swarmSignal, ref s_x001HeadlessStressFractureBotSignalPushDropCount);
            _ecosystemDirectorReadyAtIssue = _ecosystemDirector != null && _ecosystemDirector.IsInitialized ? 1 : 0;
            _ecosystemStressIssued = 1;
            RecordBlackbox(EcosystemStressHash);
        }

        private void EmitSyntheticChunkUnload()
        {
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -128d, 0d));
            uint frame = SystemDispatcher.CurrentFrameId;
            long chunkId = unchecked((long)(0x4853464200020000UL | (uint)_extremeFrame));
            SignalBus<SectorDehydratedSignal>.TryPushTracked(new SectorDehydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = frame,
                RadiusMetersQ = 1000,
                Flags = SectorDehydratedSignal.FlagPinned,
                ResidencyState = 0
            }, ref s_x001HeadlessStressFractureBotSignalPushDropCount);
            MemorySnapshot snapshot = CaptureMemorySnapshot();
            _chunkUnloadNativeBytesBaseline = snapshot.NativeBytes;
            _chunkUnloadH8BytesBaseline = snapshot.H8Bytes;
            _chunkUnloadDataVaultBytesBaseline = snapshot.DataVaultBytes;
            _chunkUnloadCheckFrame = _extremeFrame + ChunkLeakGraceFrames;
            _chunkUnloadPending = true;
        }

        private void CheckChunkLeakWindow()
        {
            if (!_chunkUnloadPending || _extremeFrame < _chunkUnloadCheckFrame)
                return;

            MemorySnapshot snapshot = CaptureMemorySnapshot();
            if (snapshot.NativeBytes > _chunkUnloadNativeBytesBaseline + LeakToleranceBytes ||
                snapshot.H8Bytes > _chunkUnloadH8BytesBaseline + LeakToleranceBytes ||
                snapshot.DataVaultBytes > _chunkUnloadDataVaultBytesBaseline + _scratchBlockBytes)
            {
                FailAndQuit(1, LeakHash, NativeLeakToken);
                return;
            }

            _chunkUnloadPending = false;
        }

        private void PulseScratchMemory()
        {
            if (!IsScratchBlockActive())
            {
                _scratchBaselineH8Bytes = H8Memory.TotalBytes;
                _scratchBaselineH8AllocationCount = H8Memory.ActiveAllocationCount;
                if (!AcquireScratchBlock())
                {
                    FailAndQuit(1, AllocationDeniedHash, AllocationDeniedToken);
                    return;
                }

                if (_dataVaultApiGapLogged == 0)
                {
                    _dataVaultApiGapLogged = 1;
                    RecordBlackbox(DataVaultApiGapHash);
                }

                return;
            }

            ReleaseScratchBlock();
            if (H8Memory.TotalBytes > _scratchBaselineH8Bytes + LeakToleranceBytes ||
                H8Memory.ActiveAllocationCount > _scratchBaselineH8AllocationCount)
            {
                FailAndQuit(1, LeakHash, NativeLeakToken);
            }
        }

        private void ReleaseScratchBlock()
        {
            if (!IsScratchBlockActive() && !IsVaultHandleCreated(in _scratchBlockHandle))
                return;

            IDataVault vault = _dataVault;
            if (vault != null && IsVaultHandleCreated(in _scratchBlockHandle) && vault.ReleaseBuffer(in _scratchBlockHandle))
            {
                _scratchBlockHandle = default;
                _scratchBlockActive = 0;
            }
        }

        private bool AcquireScratchBlock()
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || _scratchBlockBytes <= 0)
                return false;

            _scratchBlockHandle = vault.EnsureGenerationHandle<byte>(
                ScratchBlockBufferId,
                _scratchBlockBytes,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);

            if (!IsVaultHandleCreated(in _scratchBlockHandle) ||
                !vault.TryAcquireWriteLock(in _scratchBlockHandle, OwnerSystemId, out NativeArray<byte> scratch))
            {
                return false;
            }

            try
            {
                if (!scratch.IsCreated || scratch.Length < _scratchBlockBytes)
                    return false;

                _scratchBlockActive = 1;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _scratchBlockHandle, OwnerSystemId);
            }
        }

        private bool IsScratchBlockActive()
        {
            return _scratchBlockActive != 0;
        }

        private void EmitAupShift()
        {
            _shiftSequence++;
            uint sequence = _shiftSequence == 0u ? 1u : _shiftSequence;
            int sign = (sequence & 1u) == 0u ? -1 : 1;
            int3 delta = new int3(sign, 0, 0);
            float3 shiftMeters = new float3(sign * 1000f, 0f, 0f);
            _lastShiftMeters = shiftMeters;
            _lastAupShiftExtremeFrame = _extremeFrame;
            _dispatcher?.RequestAupPreShiftPause(sequence);
            AupSignalRoute.TryQueuePreShift(new AupPreShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            SignalBus<RebaseSignal>.TryPushTracked(new RebaseSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                GridDelta = delta,
                Flags = 1u
            }, ref s_x001HeadlessStressFractureBotSignalPushDropCount);
            AupSignalRoute.TryQueueShift(new AupShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            RecordBlackbox(AupShiftHash);
        }

        private void ScanRigidbodyAups()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _rigidbodyScanMissCount++;
                return;
            }

            if (!TryReadRigidbodyAupBuffer(vault, out NativeArray<double3> rigidbodyAups))
            {
                _rigidbodyScanMissCount++;
                return;
            }

            int length = rigidbodyAups.Length;
            for (int i = 0; i < length; i++)
            {
                double3 value = rigidbodyAups[i];
                if (math.all(math.isfinite(value)))
                    continue;

                _rigidbodyNanIndex = i;
                FailAndQuit(1, NanHash, NanPoisoningToken);
                return;
            }
        }

        private static bool TryReadRigidbodyAupBuffer(IDataVault vault, out NativeArray<double3> rigidbodyAups)
        {
            rigidbodyAups = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryGetGenerationHandle<double3>(BufferID.RigidbodyAUPs, out VaultGenerationHandle<double3> handle) ||
                handle.BufferID != (uint)BufferID.RigidbodyAUPs ||
                handle.SystemID != (uint)SystemID.GlobalPhysicsStateManager ||
                handle.Generation == 0u ||
                !vault.TryReadHandle(in handle, out NativeArray<double3> resolved) ||
                !resolved.IsCreated ||
                resolved.Length <= 0)
            {
                return false;
            }

            rigidbodyAups = resolved;
            return true;
        }

        private void CompleteAndQuit()
        {
            if (_finished)
                return;

            _finished = true;
            _lastFractureHash = SuccessHash;
            ReleaseScratchBlock();
            RecordBlackbox(SuccessHash);
            UnregisterRuntimeHooks();
            PublishCrashSignal(0, SuccessHash, 0);
            TryDumpBlackbox();
            TryWriteResult(0, SuccessToken);
            Application.Quit(0);
        }

        private void FailAndQuit(int exitCode, uint reasonHash, string status)
        {
            if (_finished)
                return;

            _finished = true;
            _lastFractureHash = reasonHash;
            Hecton8.Core.H8Debug.LogError(status);
            RecordBlackbox(reasonHash);
            UnregisterRuntimeHooks();
            ReleaseScratchBlock();
            PublishCrashSignal(exitCode, reasonHash, 2);
            TryDumpBlackbox();
            TryDumpH8Memory();
            TryWriteResult(exitCode, status);
            Application.Quit(exitCode);
        }

        private void UnregisterRuntimeHooks()
        {
            TryUnregisterHotSwapListener();
            UnregisterRuntimeLanes();

            if (_originListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originListenerRegistered = false;
            }

            RestoreHeadlessTimeDilation();
        }

        private void ApplyHeadlessTimeDilation()
        {
            ITickDispatcher dispatcher = _dispatcher;
            if (dispatcher == null)
                return;

            _previousTimeDilationScalar = dispatcher.TimeDilationScalar;
            dispatcher.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
            _headlessTimeDilationApplied = true;
            _headlessTimeDilationRestored = 0;
        }

        private void RestoreHeadlessTimeDilation()
        {
            if (!_headlessTimeDilationApplied)
                return;

            ITickDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
            {
                dispatcher.RequestTimeDilation(_previousTimeDilationScalar, RunnerHash);
                _headlessTimeDilationRestored = 1;
            }

            _headlessTimeDilationApplied = false;
        }

        private void PublishCrashSignal(int exitCode, uint reasonHash, byte severity)
        {
            MemorySnapshot snapshot = CaptureMemorySnapshot();
            SignalBus<CrashTelemetrySignal>.TryPushTracked(new CrashTelemetrySignal
            {
                SystemHash = RunnerHash,
                ReasonHash = reasonHash,
                Frame = SystemDispatcher.CurrentFrameId,
                ExitCode = exitCode,
                NativeAllocationCount = snapshot.NativeAllocations,
                NativeTrackedBytesMb = snapshot.NativeBytes * NativeBytesToMegabytes,
                Severity = severity,
                Flags = exitCode == 0 ? (byte)0 : (byte)1
            }, ref s_x001HeadlessStressFractureBotSignalPushDropCount);
        }

        private void RecordBlackbox(uint eventHash)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsVaultHandleCreated(in _blackboxHandle) ||
                !vault.TryAcquireWriteLock(in _blackboxHandle, OwnerSystemId, out NativeArray<FractureTelemetryEntry> blackbox))
            {
                return;
            }

            try
            {
                if (!blackbox.IsCreated || blackbox.Length <= 0)
                    return;

                MemorySnapshot snapshot = CaptureBlackboxMemorySnapshot(eventHash);
                int index = _blackboxCursor % blackbox.Length;
                blackbox[index] = new FractureTelemetryEntry
                {
                    Frame = SystemDispatcher.CurrentFrameId,
                    ExtremeFrame = unchecked((uint)_extremeFrame),
                    ShiftSequence = _shiftSequence,
                    EventHash = eventHash,
                    NativeBytes = snapshot.NativeBytes,
                    H8Bytes = snapshot.H8Bytes,
                    NativeAllocations = snapshot.NativeAllocations,
                    H8Allocations = snapshot.H8Allocations,
                    DispatcherPhaseMs = _lastSimulationPhaseMs,
                    DataVaultFragmentation = snapshot.DataVaultFragmentation,
                    LastShiftMeters = _lastShiftMeters,
                    Flags = ComposeBlackboxFlags()
                };
                _blackboxCursor++;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackboxHandle, OwnerSystemId);
            }
        }

        private MemorySnapshot CaptureMemorySnapshot()
        {
            IDataVault vault = _dataVault;
            return new MemorySnapshot
            {
                NativeBytes = GlobalRegistry.NativeTrackedBytes,
                H8Bytes = H8Memory.TotalBytes,
                DataVaultBytes = vault != null ? vault.AllocatedBytes : 0L,
                NativeAllocations = GlobalRegistry.NativeAllocationCount,
                H8Allocations = H8Memory.ActiveAllocationCount,
                DataVaultFragmentation = vault != null ? vault.HeapFragmentationRatio : 0f
            };
        }

        private MemorySnapshot CaptureBlackboxMemorySnapshot(uint eventHash)
        {
            bool forceFresh = eventHash != FrameHash || _lastMemorySnapshotExtremeFrame == int.MinValue;
            if (forceFresh || _extremeFrame - _lastMemorySnapshotExtremeFrame >= BlackboxMemorySnapshotIntervalFrames)
            {
                _lastMemorySnapshot = CaptureMemorySnapshot();
                _lastMemorySnapshotExtremeFrame = _extremeFrame;
                _memorySnapshotFreshForEntry = true;
                return _lastMemorySnapshot;
            }

            _memorySnapshotFreshForEntry = false;
            return _lastMemorySnapshot;
        }

        private uint ComposeBlackboxFlags()
        {
            uint flags = 0u;
            if (IsScratchBlockActive())
                flags |= 1u << BlackboxFlagScratchActiveBit;
            if (_baselineCaptured)
                flags |= 1u << BlackboxFlagBaselineCapturedBit;
            if (_chunkUnloadPending)
                flags |= 1u << BlackboxFlagChunkUnloadPendingBit;
            if (_ecosystemStressIssued != 0)
                flags |= 1u << BlackboxFlagEcosystemStressIssuedBit;
            if (_dataVault == null)
                flags |= 1u << BlackboxFlagDataVaultMissingBit;
            if (IsAupSnapFenceActive())
                flags |= 1u << BlackboxFlagAupSnapFenceActiveBit;
            if (_memorySnapshotFreshForEntry)
                flags |= 1u << BlackboxFlagMemorySampleFreshBit;
            return flags;
        }

        private bool IsAupSnapFenceActive()
        {
            return _lastAupShiftExtremeFrame > 0 &&
                _extremeFrame - _lastAupShiftExtremeFrame < AupSnapFenceFrames;
        }

        private void TryDumpBlackbox()
        {
            _blackboxBinaryDumpSucceeded = 0;
            _blackboxBinaryExistsAfterDump = 0;
            _blackboxManifestDumpSucceeded = 0;

            try
            {
                DumpBlackbox();
                _blackboxBinaryDumpSucceeded = FileExistsCold(_blackboxPath) ? 1 : 0;
            }
            catch (Exception)
            {
                _blackboxBinaryDumpSucceeded = 0;
            }

            _blackboxBinaryExistsAfterDump = FileExistsCold(_blackboxPath) ? 1 : 0;

            try
            {
                DumpBlackboxManifest();
                _blackboxManifestDumpSucceeded = FileExistsCold(_blackboxManifestPath) ? 1 : 0;
            }
            catch (Exception)
            {
                _blackboxManifestDumpSucceeded = 0;
            }
        }

        private bool TryReadBlackbox(out NativeArray<FractureTelemetryEntry>.ReadOnly blackbox)
        {
            blackbox = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                IsVaultHandleCreated(in _blackboxHandle) &&
                vault.TryReadOnlyHandle(in _blackboxHandle, out blackbox) &&
                blackbox.IsCreated &&
                blackbox.Length > 0;
        }

        private void DumpBlackbox()
        {
            if (!TryReadBlackbox(out NativeArray<FractureTelemetryEntry>.ReadOnly blackbox) ||
                string.IsNullOrEmpty(_blackboxPath))
            {
                return;
            }

            EnsureParentDirectory(_blackboxPath);
            using (FileStream stream = new FileStream(_blackboxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(BlackboxMagic);
                int validCount = math.min(_blackboxCursor, blackbox.Length);
                int start = _blackboxCursor >= blackbox.Length ? _blackboxCursor % blackbox.Length : 0;
                writer.Write(validCount);
                writer.Write(BlackboxEntrySizeBytes);
                writer.Write(_blackboxCursor);
                for (int i = 0; i < validCount; i++)
                {
                    int index = (start + i) % blackbox.Length;
                    FractureTelemetryEntry entry = blackbox[index];
                    writer.Write(entry.Frame);
                    writer.Write(entry.ExtremeFrame);
                    writer.Write(entry.ShiftSequence);
                    writer.Write(entry.EventHash);
                    writer.Write(entry.NativeBytes);
                    writer.Write(entry.H8Bytes);
                    writer.Write(entry.NativeAllocations);
                    writer.Write(entry.H8Allocations);
                    writer.Write(entry.DispatcherPhaseMs);
                    writer.Write(entry.DataVaultFragmentation);
                    writer.Write(entry.LastShiftMeters.x);
                    writer.Write(entry.LastShiftMeters.y);
                    writer.Write(entry.LastShiftMeters.z);
                    writer.Write(entry.Flags);
                }
            }
        }

        private void DumpBlackboxManifest()
        {
            if (string.IsNullOrEmpty(_blackboxManifestPath))
                return;

            EnsureParentDirectory(_blackboxManifestPath);
            using (StreamWriter writer = new StreamWriter(_blackboxManifestPath, false))
            {
                int validCount = TryReadBlackbox(out NativeArray<FractureTelemetryEntry>.ReadOnly blackbox)
                    ? math.min(_blackboxCursor, blackbox.Length)
                    : 0;
                writer.Write('{');
                writer.Write("\"agent\":\"");
                writer.Write(AgentName);
                writer.Write("\",\"resultSchemaVersion\":");
                WriteInvariant(writer, ResultSchemaVersion);
                writer.Write(",\"generatedUtc\":\"");
                WriteJsonEscaped(writer, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                writer.Write('"');
                writer.Write(",\"blackboxBinaryRelativePath\":\"");
                WriteJsonEscaped(writer, BlackboxRelativePath);
                writer.Write("\",\"blackboxBinaryPath\":\"");
                WriteJsonEscaped(writer, _blackboxPath);
                writer.Write("\",\"blackboxBinaryDumpSucceeded\":");
                WriteInvariant(writer, _blackboxBinaryDumpSucceeded);
                writer.Write(",\"blackboxBinaryExistsAfterDump\":");
                WriteInvariant(writer, _blackboxBinaryExistsAfterDump);
                writer.Write(",\"blackboxMagic\":");
                WriteInvariant(writer, BlackboxMagic);
                writer.Write(",\"blackboxFrameCapacity\":");
                WriteInvariant(writer, BlackboxFrameCapacity);
                writer.Write(",\"blackboxEntrySizeBytes\":");
                WriteInvariant(writer, BlackboxEntrySizeBytes);
                writer.Write(",\"blackboxHeaderSizeBytes\":");
                WriteInvariant(writer, BlackboxHeaderSizeBytes);
                WriteBlackboxHeaderLayout(writer);
                WriteBlackboxEntryOffsets(writer);
                writer.Write(",\"blackboxValidEntryCount\":");
                WriteInvariant(writer, validCount);
                writer.Write(",\"blackboxCursor\":");
                WriteInvariant(writer, _blackboxCursor);
                writer.Write(",\"blackboxMemorySnapshotIntervalFrames\":");
                WriteInvariant(writer, BlackboxMemorySnapshotIntervalFrames);
                writer.Write(",\"blackboxFlagScratchActiveBit\":");
                WriteInvariant(writer, BlackboxFlagScratchActiveBit);
                writer.Write(",\"blackboxFlagBaselineCapturedBit\":");
                WriteInvariant(writer, BlackboxFlagBaselineCapturedBit);
                writer.Write(",\"blackboxFlagChunkUnloadPendingBit\":");
                WriteInvariant(writer, BlackboxFlagChunkUnloadPendingBit);
                writer.Write(",\"blackboxFlagEcosystemStressIssuedBit\":");
                WriteInvariant(writer, BlackboxFlagEcosystemStressIssuedBit);
                writer.Write(",\"blackboxFlagDataVaultMissingBit\":");
                WriteInvariant(writer, BlackboxFlagDataVaultMissingBit);
                writer.Write(",\"blackboxFlagAupSnapFenceBit\":");
                WriteInvariant(writer, BlackboxFlagAupSnapFenceActiveBit);
                writer.Write(",\"blackboxFlagMemorySampleFreshBit\":");
                WriteInvariant(writer, BlackboxFlagMemorySampleFreshBit);
                writer.Write(",\"blackboxAupSnapFenceFrames\":");
                WriteInvariant(writer, AupSnapFenceFrames);
                writer.Write(",\"lastFractureHash\":");
                WriteInvariant(writer, _lastFractureHash);
                writer.Write(",\"eventHashFrame\":");
                WriteInvariant(writer, FrameHash);
                writer.Write(",\"eventHashSuccess\":");
                WriteInvariant(writer, SuccessHash);
                writer.Write(",\"eventHashJobStall\":");
                WriteInvariant(writer, StallHash);
                writer.Write(",\"eventHashNanPoisoning\":");
                WriteInvariant(writer, NanHash);
                writer.Write(",\"eventHashNativeLeak\":");
                WriteInvariant(writer, LeakHash);
                writer.Write(",\"eventHashAllocationDenied\":");
                WriteInvariant(writer, AllocationDeniedHash);
                writer.Write(",\"eventHashTimeout\":");
                WriteInvariant(writer, TimeoutHash);
                writer.Write(",\"eventHashAupShift\":");
                WriteInvariant(writer, AupShiftHash);
                writer.Write(",\"eventHashEcosystemStress\":");
                WriteInvariant(writer, EcosystemStressHash);
                writer.Write(",\"eventHashDataVaultApiGap\":");
                WriteInvariant(writer, DataVaultApiGapHash);
                writer.Write(",\"extremeFrames\":");
                WriteInvariant(writer, _extremeFrame);
                writer.Write(",\"shiftSequence\":");
                WriteInvariant(writer, _shiftSequence);
                writer.Write(",\"activationSource\":");
                WriteInvariant(writer, _activationSource);
                writer.Write(",\"activationSourceName\":\"");
                WriteActivationSourceName(writer, _activationSource);
                writer.Write("\",\"headlessTimeDilationRestored\":");
                WriteInvariant(writer, _headlessTimeDilationRestored);
                writer.Write('}');
            }
        }

        private void TryDumpH8Memory()
        {
            try
            {
                H8Memory.DumpAllocationTableText(_h8MemoryDumpPath);
            }
            catch (Exception)
            {
            }
        }

        private void TryWriteResult(int exitCode, string status)
        {
            try
            {
                string tempPath = _resultPath + ".tmp";
                MemorySnapshot finalSnapshot = CaptureMemorySnapshot();
                EnsureParentDirectory(_resultPath);
                using (StreamWriter writer = new StreamWriter(tempPath, false))
                {
                    writer.Write('{');
                    writer.Write("\"agent\":\"");
                    writer.Write(AgentName);
                    writer.Write("\",\"resultSchemaVersion\":");
                    WriteInvariant(writer, ResultSchemaVersion);
                    writer.Write(",\"status\":\"");
                    WriteJsonEscaped(writer, status);
                    writer.Write("\",\"exitCode\":");
                    WriteInvariant(writer, exitCode);
                    writer.Write(",\"extremeFrames\":");
                    WriteInvariant(writer, _extremeFrame);
                    writer.Write(",\"targetFrames\":");
                    WriteInvariant(writer, _targetFrames);
                    writer.Write(",\"activationSource\":");
                    WriteInvariant(writer, _activationSource);
                    writer.Write(",\"activationSourceName\":\"");
                    WriteActivationSourceName(writer, _activationSource);
                    writer.Write('"');
                    writer.Write(",\"activationFlagDeletedAtStartup\":");
                    WriteInvariant(writer, _activationFlagDeletedAtStartup);
                    writer.Write(",\"scratchBlockBytes\":");
                    WriteInvariant(writer, _scratchBlockBytes);
                    writer.Write(",\"startupTimeoutSeconds\":");
                    WriteInvariant(writer, _startupTimeoutSeconds);
                    writer.Write(",\"headlessTimeDilationScalar\":");
                    WriteInvariant(writer, TimeDilationScalar);
                    writer.Write(",\"previousTimeDilationScalar\":");
                    WriteInvariant(writer, _previousTimeDilationScalar);
                    writer.Write(",\"headlessTimeDilationRestored\":");
                    WriteInvariant(writer, _headlessTimeDilationRestored);
                    writer.Write(",\"aupShiftCount\":");
                    WriteInvariant(writer, _shiftSequence);
                    writer.Write(",\"originShiftCallbacks\":");
                    WriteInvariant(writer, _originShiftCount);
                    writer.Write(",\"blackboxAupSnapFenceFrames\":");
                    WriteInvariant(writer, AupSnapFenceFrames);
                    writer.Write(",\"blackboxFlagAupSnapFenceBit\":");
                    WriteInvariant(writer, BlackboxFlagAupSnapFenceActiveBit);
                    writer.Write(",\"blackboxMagic\":");
                    WriteInvariant(writer, BlackboxMagic);
                    writer.Write(",\"blackboxFrameCapacity\":");
                    WriteInvariant(writer, BlackboxFrameCapacity);
                    writer.Write(",\"blackboxHeaderSizeBytes\":");
                    WriteInvariant(writer, BlackboxHeaderSizeBytes);
                    writer.Write(",\"blackboxEntrySizeBytes\":");
                    WriteInvariant(writer, BlackboxEntrySizeBytes);
                    WriteBlackboxHeaderLayout(writer);
                    writer.Write(",\"blackboxManifestRelativePath\":\"");
                    WriteJsonEscaped(writer, BlackboxManifestRelativePath);
                    writer.Write("\",\"blackboxManifestPath\":\"");
                    WriteJsonEscaped(writer, _blackboxManifestPath);
                    writer.Write('"');
                    writer.Write(",\"blackboxBinaryRelativePath\":\"");
                    WriteJsonEscaped(writer, BlackboxRelativePath);
                    writer.Write("\",\"blackboxBinaryPath\":\"");
                    WriteJsonEscaped(writer, _blackboxPath);
                    writer.Write('"');
                    writer.Write(",\"blackboxBinaryDumpSucceeded\":");
                    WriteInvariant(writer, _blackboxBinaryDumpSucceeded);
                    writer.Write(",\"blackboxBinaryExistsAfterDump\":");
                    WriteInvariant(writer, _blackboxBinaryExistsAfterDump);
                    writer.Write(",\"blackboxManifestDumpSucceeded\":");
                    WriteInvariant(writer, _blackboxManifestDumpSucceeded);
                    writer.Write(",\"blackboxMemorySnapshotIntervalFrames\":");
                    WriteInvariant(writer, BlackboxMemorySnapshotIntervalFrames);
                    writer.Write(",\"blackboxFlagMemorySampleFreshBit\":");
                    WriteInvariant(writer, BlackboxFlagMemorySampleFreshBit);
                    WriteBlackboxEntryOffsets(writer);
                    writer.Write(",\"simulationPhaseMs\":");
                    WriteInvariant(writer, _lastSimulationPhaseMs);
                    writer.Write(",\"nativeBytesBaseline\":");
                    WriteInvariant(writer, _nativeBytesBaseline);
                    writer.Write(",\"nativeBytesFinal\":");
                    WriteInvariant(writer, finalSnapshot.NativeBytes);
                    writer.Write(",\"h8BytesBaseline\":");
                    WriteInvariant(writer, _h8BytesBaseline);
                    writer.Write(",\"h8BytesFinal\":");
                    WriteInvariant(writer, finalSnapshot.H8Bytes);
                    writer.Write(",\"dataVaultBytesBaseline\":");
                    WriteInvariant(writer, _dataVaultBytesBaseline);
                    writer.Write(",\"dataVaultBytesFinal\":");
                    WriteInvariant(writer, finalSnapshot.DataVaultBytes);
                    writer.Write(",\"nativeAllocationBaselineCount\":");
                    WriteInvariant(writer, _nativeAllocationBaselineCount);
                    writer.Write(",\"nativeAllocationFinalCount\":");
                    WriteInvariant(writer, finalSnapshot.NativeAllocations);
                    writer.Write(",\"h8AllocationBaselineCount\":");
                    WriteInvariant(writer, _h8AllocationBaselineCount);
                    writer.Write(",\"h8AllocationFinalCount\":");
                    WriteInvariant(writer, finalSnapshot.H8Allocations);
                    writer.Write(",\"rigidbodyScanMissCount\":");
                    WriteInvariant(writer, _rigidbodyScanMissCount);
                    writer.Write(",\"rigidbodyNanIndex\":");
                    WriteInvariant(writer, _rigidbodyNanIndex);
                    writer.Write(",\"ecosystemStressIssued\":");
                    WriteInvariant(writer, _ecosystemStressIssued);
                    writer.Write(",\"ecosystemDirectorReadyAtIssue\":");
                    WriteInvariant(writer, _ecosystemDirectorReadyAtIssue);
                    writer.Write(",\"staticHPhi\":");
                    WriteInvariant(writer, _staticHPhiMetric);
                    writer.Write(",\"staticHPhiModel\":\"runtime_aup_risk_adjusted\"");
                    writer.Write(",\"staticHPhiAupPrecisionIntegrity\":");
                    WriteInvariant(writer, _staticHPhiAupPrecisionIntegrity);
                    writer.Write(",\"staticHPhiAupPrecisionSafe\":");
                    WriteInvariant(writer, _staticHPhiAupPrecisionSafe);
                    writer.Write(",\"staticHPhiAupPrecisionRisk\":");
                    WriteInvariant(writer, _staticHPhiAupPrecisionRisk);
                    writer.Write(",\"staticHPhiDispatcherContracts\":");
                    WriteInvariant(writer, _staticHPhiDispatcherContracts);
                    writer.Write(",\"staticHPhiUnityUpdateMethods\":");
                    WriteInvariant(writer, _staticHPhiUnityUpdateMethods);
                    writer.Write(",\"lastFractureHash\":");
                    WriteInvariant(writer, _lastFractureHash);
                    writer.Write(",\"dataVaultFreeApi\":\"ABSENT_IDataVault_RELEASE\"");
                    writer.Write('}');
                }

                if (File.Exists(_resultPath))
                    File.Delete(_resultPath);
                File.Move(tempPath, _resultPath);
            }
            catch (Exception)
            {
            }
        }

        private static void WriteJsonEscaped(StreamWriter writer, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        writer.Write("\\\\");
                        break;
                    case '"':
                        writer.Write("\\\"");
                        break;
                    case '\n':
                        writer.Write("\\n");
                        break;
                    case '\r':
                        writer.Write("\\r");
                        break;
                    case '\t':
                        writer.Write("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            writer.Write("\\u00");
                            WriteJsonHexNibble(writer, c >> 4);
                            WriteJsonHexNibble(writer, c);
                        }
                        else
                        {
                            writer.Write(c);
                        }

                        break;
                }
            }
        }

        private static void WriteActivationSourceName(StreamWriter writer, int source)
        {
            switch (source)
            {
                case ActivationSourceCommandLine:
                    writer.Write("command_line");
                    break;
                case ActivationSourceEnvironment:
                    writer.Write("environment");
                    break;
                case ActivationSourceFlagFile:
                    writer.Write("flag_file");
                    break;
                default:
                    writer.Write("none");
                    break;
            }
        }

        private static void WriteBlackboxEntryOffsets(StreamWriter writer)
        {
            writer.Write(",\"blackboxEntryOffsetFrame\":");
            WriteInvariant(writer, BlackboxEntryOffsetFrame);
            writer.Write(",\"blackboxEntryOffsetExtremeFrame\":");
            WriteInvariant(writer, BlackboxEntryOffsetExtremeFrame);
            writer.Write(",\"blackboxEntryOffsetShiftSequence\":");
            WriteInvariant(writer, BlackboxEntryOffsetShiftSequence);
            writer.Write(",\"blackboxEntryOffsetEventHash\":");
            WriteInvariant(writer, BlackboxEntryOffsetEventHash);
            writer.Write(",\"blackboxEntryOffsetNativeBytes\":");
            WriteInvariant(writer, BlackboxEntryOffsetNativeBytes);
            writer.Write(",\"blackboxEntryOffsetH8Bytes\":");
            WriteInvariant(writer, BlackboxEntryOffsetH8Bytes);
            writer.Write(",\"blackboxEntryOffsetNativeAllocations\":");
            WriteInvariant(writer, BlackboxEntryOffsetNativeAllocations);
            writer.Write(",\"blackboxEntryOffsetH8Allocations\":");
            WriteInvariant(writer, BlackboxEntryOffsetH8Allocations);
            writer.Write(",\"blackboxEntryOffsetDispatcherPhaseMs\":");
            WriteInvariant(writer, BlackboxEntryOffsetDispatcherPhaseMs);
            writer.Write(",\"blackboxEntryOffsetDataVaultFragmentation\":");
            WriteInvariant(writer, BlackboxEntryOffsetDataVaultFragmentation);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersX\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersX);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersY\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersY);
            writer.Write(",\"blackboxEntryOffsetLastShiftMetersZ\":");
            WriteInvariant(writer, BlackboxEntryOffsetLastShiftMetersZ);
            writer.Write(",\"blackboxEntryOffsetFlags\":");
            WriteInvariant(writer, BlackboxEntryOffsetFlags);
        }

        private static void WriteBlackboxHeaderLayout(StreamWriter writer)
        {
            writer.Write(",\"blackboxByteOrder\":\"little_endian\"");
            writer.Write(",\"blackboxFloatFormat\":\"ieee754_binary32\"");
            writer.Write(",\"blackboxHeaderOffsetMagic\":");
            WriteInvariant(writer, BlackboxHeaderOffsetMagic);
            writer.Write(",\"blackboxHeaderOffsetValidEntryCount\":");
            WriteInvariant(writer, BlackboxHeaderOffsetValidEntryCount);
            writer.Write(",\"blackboxHeaderOffsetEntrySizeBytes\":");
            WriteInvariant(writer, BlackboxHeaderOffsetEntrySizeBytes);
            writer.Write(",\"blackboxHeaderOffsetCursor\":");
            WriteInvariant(writer, BlackboxHeaderOffsetCursor);
        }

        private static bool FileExistsCold(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WriteJsonHexNibble(StreamWriter writer, int value)
        {
            int nibble = value & 0xF;
            writer.Write((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
        }

        private static bool ShouldRunStatic()
        {
            return ResolveActivationSourceStatic() != ActivationSourceNone;
        }

        private static int ResolveActivationSourceStatic()
        {
            if (HasCommandLineArg(CommandLineArg))
                return ActivationSourceCommandLine;

            string value = global::System.Environment.GetEnvironmentVariable(EnvironmentFlagName);
            if (string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return ActivationSourceEnvironment;

            return HasFreshFlagFile(ResolveProjectPathStatic(FlagRelativePath))
                ? ActivationSourceFlagFile
                : ActivationSourceNone;
        }

        private static bool HasFreshFlagFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                DateTime nowUtc = DateTime.UtcNow;
                double ageSeconds = (nowUtc - lastWriteUtc).TotalSeconds;
                if (ageSeconds < -FlagFutureSkewToleranceSeconds)
                {
                    TryDeleteFile(path);
                    return false;
                }

                if (ageSeconds < 0.0)
                    return true;

                if (ageSeconds <= FlagMaxAgeSeconds)
                    return true;

                TryDeleteFile(path);
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool TryDeleteActivationFlagCold()
        {
            string path = ResolveProjectPathStatic(FlagRelativePath);
            try
            {
                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasCommandLineArg(string commandLineArg)
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], commandLineArg, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static int TryReadInt(string[] args, string name, int fallback)
        {
            if (args == null || string.IsNullOrEmpty(name))
                return fallback;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.IsNullOrEmpty(arg))
                    continue;

                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i < args.Length - 1 &&
                        TryParseIntInvariant(args[i + 1].AsSpan(), out int separatedValue))
                    {
                        return separatedValue;
                    }

                    continue;
                }

                int separatorIndex = name.Length;
                if (arg.Length > separatorIndex + 1 &&
                    arg[separatorIndex] == '=' &&
                    string.Compare(arg, 0, name, 0, separatorIndex, StringComparison.OrdinalIgnoreCase) == 0 &&
                    TryParseIntInvariant(arg.AsSpan(separatorIndex + 1), out int inlineValue))
                {
                    return inlineValue;
                }
            }

            return fallback;
        }

        private static int TryReadEnvironmentInt(string name, int fallback)
        {
            string value = global::System.Environment.GetEnvironmentVariable(name);
            return TryParseIntInvariant(value.AsSpan(), out int parsed)
                ? parsed
                : fallback;
        }

#if UNITY_EDITOR
        private static float ComputeStaticHPhiMetric(out HPhiStaticCounters counters)
        {
            counters = default;
            try
            {
                string scriptsRoot = Path.Combine(ResolveProjectRootStatic(), "Assets", "_Project", "Scripts");
                if (!Directory.Exists(scriptsRoot))
                    return 0f;

                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    if (IsEditorScriptPath(file))
                        continue;

                    string text;
                    try
                    {
                        text = File.ReadAllText(file);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    AccumulateHPhiCounters(text, ref counters);
                }

                return CalculateHPhiRisk(in counters);
            }
            catch (Exception)
            {
                return 0f;
            }
        }
#endif

        private static bool TryParseIntInvariant(ReadOnlySpan<char> value, out int parsed)
        {
            parsed = 0;
            if (value.Length == 0)
                return false;

            int index = 0;
            int sign = 1;
            if (value[index] == '-')
            {
                sign = -1;
                index++;
            }
            else if (value[index] == '+')
            {
                index++;
            }

            if (index >= value.Length)
                return false;

            int result = 0;
            for (; index < value.Length; index++)
            {
                char c = value[index];
                if (c < '0' || c > '9')
                    return false;

                result = (result * 10) + (c - '0');
            }

            parsed = result * sign;
            return true;
        }

        private static void AccumulateHPhiCounters(string text, ref HPhiStaticCounters counters)
        {
            counters.SignalBusPush += CountSignalBusPush(text);
            counters.GlobalRegistryGet += CountOrdinal(text, "Global" + "Registry.Get<");
            counters.GlobalRegistrySurface += CountOrdinal(text, "Global" + "Registry.");
            counters.EventPublish += CountOrdinal(text, "Pub" + "lish(");
            counters.UnityUpdateMethods += CountUnityUpdateMethodDeclarations(text);
            counters.IUpdatable += CountOrdinal(text, "IUp" + "datable");
            counters.IFastTickable += CountOrdinal(text, "IFast" + "Tickable");
            counters.IFixedTickable += CountOrdinal(text, "IFixed" + "Tickable");
            counters.ISlowTickable += CountOrdinal(text, "ISlow" + "Tickable");
            counters.IColdTickable += CountOrdinal(text, "ICold" + "Tickable");
            counters.IFrostTickable += CountOrdinal(text, "IFrost" + "Tickable");
            counters.ILateFrameTickable += CountOrdinal(text, "ILate" + "FrameTickable");
            counters.IPostFixedTickable += CountOrdinal(text, "IPost" + "FixedTickable");
            counters.IJob += CountOrdinal(text, "IJ" + "ob");
            counters.ITickable += CountOrdinal(text, "ITick" + "able");
            counters.DataVaultRefs += CountDataVaultRefs(text);
            counters.NativeArrayRefs += CountOrdinal(text, "Native" + "Array<");
            counters.StructDeclarations += CountOrdinal(text, " str" + "uct ");
            counters.StructLayoutAttributes += CountOrdinal(text, "[Struct" + "Layout(");
            counters.StaticInstance += CountOrdinal(text, "Instance {");
            counters.StaticInstance += CountOrdinal(text, "Instance{");
            counters.SceneLookupCalls += CountSceneLookupCalls(text);
            counters.ComponentLookupCalls += CountComponentLookupCalls(text);
            counters.AupPrecisionSafe += CountAupPrecisionSafe(text);
            counters.AupPrecisionRisk += CountAupPrecisionRisk(text);
        }

        private static float CalculateHPhiRisk(in HPhiStaticCounters counters)
        {
            int dispatcherContracts = CountDispatcherContracts(in counters);
            float riskIntegration = DivideOrZero(
                counters.SignalBusPush,
                counters.SignalBusPush + counters.GlobalRegistrySurface + counters.EventPublish + counters.StaticInstance + counters.SceneLookupCalls + counters.ComponentLookupCalls);
            float architecturalPurity = DivideOrZero(
                dispatcherContracts + counters.IJob,
                counters.UnityUpdateMethods + dispatcherContracts + counters.IJob);
            float dataSovereignty = DivideOrZero(
                counters.DataVaultRefs,
                counters.DataVaultRefs + counters.NativeArrayRefs);
            float memoryAlignment = DivideOrZero(counters.StructLayoutAttributes, counters.StructDeclarations);
            float aupPrecisionIntegrity = CalculateAupPrecisionIntegrity(in counters);
            return riskIntegration * architecturalPurity * dataSovereignty * memoryAlignment * aupPrecisionIntegrity;
        }

        private static int CountDispatcherContracts(in HPhiStaticCounters counters)
        {
            return counters.IUpdatable +
                counters.ITickable +
                counters.IFastTickable +
                counters.IFixedTickable +
                counters.ISlowTickable +
                counters.IColdTickable +
                counters.IFrostTickable +
                counters.ILateFrameTickable +
                counters.IPostFixedTickable;
        }

        private static float CalculateAupPrecisionIntegrity(in HPhiStaticCounters counters)
        {
            return DivideOrOne(
                counters.AupPrecisionSafe,
                counters.AupPrecisionSafe + counters.AupPrecisionRisk);
        }

        private static float DivideOrZero(int numerator, int denominator)
        {
            return denominator > 0 ? (float)numerator / denominator : 0f;
        }

        private static float DivideOrOne(int numerator, int denominator)
        {
            return denominator > 0 ? (float)numerator / denominator : 1f;
        }

        private static bool IsEditorScriptPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static int CountSignalBusPush(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf("Signal" + "Bus<", index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                int lineEnd = FindStatementEnd(text, found);
                if (text.IndexOf(".P" + "ush", found, lineEnd - found, StringComparison.Ordinal) >= 0)
                    count++;

                index = found + 10;
            }

            return count;
        }

        private static int CountUnityUpdateMethodDeclarations(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int count = 0;
            int lineStart = 0;
            while (lineStart < text.Length)
            {
                int lineEnd = FindLineEnd(text, lineStart);
                if (ContainsInRange(text, lineStart, lineEnd, "void " + "Up" + "date(") ||
                    ContainsInRange(text, lineStart, lineEnd, "void " + "Late" + "Up" + "date(") ||
                    ContainsInRange(text, lineStart, lineEnd, "void " + "Fixed" + "Up" + "date("))
                {
                    count++;
                }

                lineStart = lineEnd + 1;
            }

            return count;
        }

        private static int CountDataVaultRefs(string text)
        {
            int count = CountOrdinal(text, "Global" + "DataVault");
            count += CountOrdinal(text, "IData" + "Vault");
            count += CountOrdinal(text, "Vault" + "BufferHandle<");
            count += CountOrdinal(text, "Get" + "Buffer<");
            count += CountOrdinal(text, "Try" + "GetBuffer<");
            count += CountOrdinal(text, "Try" + "GetBuffer(");
            count += CountOrdinal(text, "Get" + "BufferHandle<");
            count += CountOrdinal(text, "Try" + "GetBufferHandle<");
            count += CountOrdinal(text, "Resolve" + "Buffer<");
            return count;
        }

        private static int CountSceneLookupCalls(string text)
        {
            int count = CountOrdinal(text, "Find" + "ObjectOfType");
            count += CountOrdinal(text, "Find" + "Object" + "sOfType");
            count += CountOrdinal(text, "FindFirst" + "ObjectByType");
            count += CountOrdinal(text, "FindAny" + "ObjectByType");
            count += CountOrdinal(text, "Find" + "Object" + "sByType");
            count += CountOrdinal(text, "Find" + "WithTag");
            count += CountOrdinal(text, "GameObject." + "Find");
            count += CountOrdinal(text, "Resources." + "Find" + "Object" + "sOfTypeAll");
            return count;
        }

        private static int CountComponentLookupCalls(string text)
        {
            int count = CountOrdinal(text, "Get" + "Component<");
            count += CountOrdinal(text, "Get" + "Components<");
            count += CountOrdinal(text, "Get" + "ComponentInChildren<");
            count += CountOrdinal(text, "Get" + "ComponentInParent<");
            return count;
        }

        private static int CountAupPrecisionSafe(string text)
        {
            int count = CountOrdinal(text, "Current" + "TotalOffsetDouble");
            count += CountOrdinal(text, "ToAbsolute" + "UniversePositionDouble3");
            count += CountOrdinal(text, "ToUniverse" + "SpaceDouble3");
            count += CountOrdinal(text, "ToRuntime" + "SpaceDouble3");
            count += CountOrdinal(text, "ToRuntime" + "Space(double3");
            count += CountOrdinal(text, "FromAbsolute" + "Position");
            count += CountOrdinal(text, "Distance" + "Sq(");
            return count;
        }

        private static int CountAupPrecisionRisk(string text)
        {
            int count = CountOrdinal(text, "Current" + "TotalOffset;");
            count += CountOrdinal(text, "Current" + "TotalOffset.");
            count += CountOrdinal(text, "New" + "TotalOffset.");
            count += CountOrdinal(text, "Previous" + "TotalOffset.");
            count += CountOrdinal(text, "Hecton" + "FloatingOrigin.ToAbsolute" + "UniversePosition(");
            count += CountOrdinal(text, "Hecton" + "MapMagicVegetationBridge.ToUniverse" + "Space(");
            count += CountOrdinal(text, "(float3)" + "AU" + "P");
            count += CountOrdinal(text, "Vector3 " + "uni" + "versePosition");
            count += CountOrdinal(text, "Vector3 stable" + "UniverseRoot");
            return count;
        }

        private static int FindStatementEnd(string text, int start)
        {
            int index = start;
            while (index < text.Length)
            {
                char c = text[index];
                if (c == ';' || c == '\n' || c == '\r')
                    break;

                index++;
            }

            return index;
        }

        private static int FindLineEnd(string text, int start)
        {
            int index = start;
            while (index < text.Length)
            {
                char c = text[index];
                if (c == '\n' || c == '\r')
                    break;

                index++;
            }

            return index;
        }

        private static bool ContainsInRange(string text, int start, int end, string pattern)
        {
            return end > start && text.IndexOf(pattern, start, end - start, StringComparison.Ordinal) >= 0;
        }

        private static int CountOrdinal(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
                return 0;

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(pattern, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + pattern.Length;
            }

            return count;
        }

        private static string FormatStaticHPhiLog(
            float metric,
            float aupPrecisionIntegrity,
            int aupPrecisionSafe,
            int aupPrecisionRisk,
            int dispatcherContracts,
            int unityUpdateMethods,
            int targetFrames,
            int scratchMegabytes,
            int startupTimeoutSeconds)
        {
            return "[H-PHI_STATIC] " + AgentName +
                " value=" + metric.ToString("F6", CultureInfo.InvariantCulture) +
                " model=runtime_aup_risk_adjusted" +
                " aupIntegrity=" + aupPrecisionIntegrity.ToString("F6", CultureInfo.InvariantCulture) +
                " aupSafe=" + aupPrecisionSafe.ToString(CultureInfo.InvariantCulture) +
                " aupRisk=" + aupPrecisionRisk.ToString(CultureInfo.InvariantCulture) +
                " dispatcherContracts=" + dispatcherContracts.ToString(CultureInfo.InvariantCulture) +
                " unityUpdateMethods=" + unityUpdateMethods.ToString(CultureInfo.InvariantCulture) +
                " requestedBoids=10000 frames=" + targetFrames.ToString(CultureInfo.InvariantCulture) +
                " scratchMb=" + scratchMegabytes.ToString(CultureInfo.InvariantCulture) +
                " startupTimeoutSec=" + startupTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        }

        private static float TicksToMilliseconds(long ticks)
        {
            return (float)(ticks * StopwatchTickToMilliseconds);
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return ResolveProjectPathStatic(relativePath);
        }

        private static string ResolveProjectPathStatic(string relativePath)
        {
            return Path.Combine(ResolveProjectRootStatic(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ResolveProjectRootStatic()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
            }
        }

        private static void WriteInvariant(StreamWriter writer, int value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, uint value)
        {
            Span<char> scratch = stackalloc char[16];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, long value)
        {
            Span<char> scratch = stackalloc char[32];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        private static void WriteInvariant(StreamWriter writer, float value)
        {
            if (!math.isfinite(value))
            {
                writer.Write('0');
                return;
            }

            Span<char> scratch = stackalloc char[32];
            if (value.TryFormat(scratch, out int written, default, CultureInfo.InvariantCulture))
                writer.Write(scratch.Slice(0, written));
            else
                writer.Write('0');
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        private struct MemorySnapshot
        {
            [FieldOffset(0)] public long NativeBytes;
            [FieldOffset(8)] public long H8Bytes;
            [FieldOffset(16)] public long DataVaultBytes;
            [FieldOffset(24)] public int NativeAllocations;
            [FieldOffset(28)] public int H8Allocations;
            [FieldOffset(32)] public float DataVaultFragmentation;
            [FieldOffset(36)] private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct HPhiStaticCounters
        {
            [FieldOffset(0)] public int SignalBusPush;
            [FieldOffset(4)] public int GlobalRegistryGet;
            [FieldOffset(8)] public int GlobalRegistrySurface;
            [FieldOffset(12)] public int EventPublish;
            [FieldOffset(16)] public int UnityUpdateMethods;
            [FieldOffset(20)] public int IUpdatable;
            [FieldOffset(24)] public int IFastTickable;
            [FieldOffset(28)] public int IFixedTickable;
            [FieldOffset(32)] public int ISlowTickable;
            [FieldOffset(36)] public int IColdTickable;
            [FieldOffset(40)] public int IFrostTickable;
            [FieldOffset(44)] public int ILateFrameTickable;
            [FieldOffset(48)] public int IPostFixedTickable;
            [FieldOffset(52)] public int IJob;
            [FieldOffset(56)] public int ITickable;
            [FieldOffset(60)] public int DataVaultRefs;
            [FieldOffset(64)] public int NativeArrayRefs;
            [FieldOffset(68)] public int StructDeclarations;
            [FieldOffset(72)] public int StructLayoutAttributes;
            [FieldOffset(76)] public int StaticInstance;
            [FieldOffset(80)] public int SceneLookupCalls;
            [FieldOffset(84)] public int ComponentLookupCalls;
            [FieldOffset(88)] public int AupPrecisionSafe;
            [FieldOffset(92)] public int AupPrecisionRisk;
        }

        [StructLayout(LayoutKind.Explicit, Size = BlackboxEntrySizeBytes)]
        private struct FractureTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint ExtremeFrame;
            [FieldOffset(8)] public uint ShiftSequence;
            [FieldOffset(12)] public uint EventHash;
            [FieldOffset(16)] public long NativeBytes;
            [FieldOffset(24)] public long H8Bytes;
            [FieldOffset(32)] public int NativeAllocations;
            [FieldOffset(36)] public int H8Allocations;
            [FieldOffset(40)] public float DispatcherPhaseMs;
            [FieldOffset(44)] public float DataVaultFragmentation;
            [FieldOffset(48)] public float3 LastShiftMeters;
            [FieldOffset(60)] public uint Flags;
        }
    }
}
