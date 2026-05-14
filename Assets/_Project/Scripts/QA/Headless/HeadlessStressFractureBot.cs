using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.QA.Headless
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9100)]
    public sealed class HeadlessStressFractureBot : MonoBehaviour, IFastTickable, IColdTickable, ILateFrameTickable, IOriginShiftListener
    {
        private const string AgentName = "HEADLESS_STRESS_FRACTURE_BOT";
        private const string RuntimeRootName = "[HeadlessStressFractureBot]";
        private const string CommandLineArg = "-h8fracturetest";
        private const string EnvironmentFlagName = "H8_FRACTURE_TEST";
        private const string EnvironmentFramesName = "H8_FRACTURE_FRAMES";
        private const string FlagRelativePath = "Temp/H8_FRACTURE_TEST.flag";
        private const string ResultRelativePath = "Docs/AgentLogs/HeadlessStressFractureResult_HEADLESS_STRESS_FRACTURE_BOT.json";
        private const string BlackboxRelativePath = "Docs/AgentLogs/Dump_HEADLESS_STRESS_FRACTURE_BOT.bin";
        private const string H8MemoryDumpRelativePath = "Docs/AgentLogs/H8Memory_HEADLESS_STRESS_FRACTURE_BOT.txt";
        private const int BlackboxFrameCapacity = 300;
        private const int BlackboxEntrySizeBytes = 64;
        private const int DefaultTargetFrames = 50000;
        private const int WarmupFrames = 120;
        private const int AupShiftIntervalFrames = 15;
        private const int ChunkUnloadIntervalFrames = 900;
        private const int ChunkLeakGraceFrames = 180;
        private const int ScratchBlockBytes = 50 * 1024 * 1024;
        private const long LeakToleranceBytes = 1024L * 1024L;
        private const double FlagMaxAgeSeconds = 10800.0;
        private const double FlagFutureSkewToleranceSeconds = 300.0;
        private const double StartupTimeoutSeconds = 60.0;
        private const float TimeDilationScalar = 100f;
        private const float StallThresholdMilliseconds = 16f;
        private const float NativeBytesToMegabytes = 1f / (1024f * 1024f);
        private const ushort RequestedBoidCount = 10000;
        private const uint RunnerHash = 0x48534642u;
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

        private NativeArray<FractureTelemetryEntry> _blackbox;
        private NativeArray<byte> _scratchBlock;
        private CancellationTokenSource _shutdownCts;
        private IDataVault _dataVault;
        private IEcosystemDirectorService _ecosystemDirector;
        private ITickDispatcher _dispatcher;
        private Camera[] _cameraScratch;
        private int[] _cameraCullingMaskScratch;
        private bool[] _cameraEnabledScratch;
        private string _resultPath;
        private string _blackboxPath;
        private string _h8MemoryDumpPath;
        private int _targetFrames;
        private int _blackboxCursor;
        private int _extremeFrame;
        private int _phaseFrame;
        private int _originShiftCount;
        private int _rigidbodyScanMissCount;
        private int _rigidbodyNanIndex;
        private int _dataVaultApiGapLogged;
        private int _ecosystemStressIssued;
        private int _ecosystemDirectorReadyAtIssue;
        private int _chunkUnloadCheckFrame;
        private int _cameraScratchCount;
        private int _nativeAllocationBaselineCount;
        private int _h8AllocationBaselineCount;
        private int _scratchBaselineH8AllocationCount;
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
        private float _staticHPhiMetric;
        private float3 _lastShiftMeters;
        private bool _started;
        private bool _finished;
        private bool _registeredFast;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _originListenerRegistered;
        private bool _runtimePolicyApplied;
        private bool _baselineCaptured;
        private bool _chunkUnloadPending;
        private bool _previousRunInBackground;
        private bool _previousAudioPause;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;
        private float _previousAudioVolume;
        private LogType _previousLogFilter;

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
            DisposeNativeArray(ref _blackbox);
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
            int unityFrame = Time.frameCount;
            if (_phaseStartTimestamp == 0L || _phaseFrame != unityFrame)
            {
                _phaseStartTimestamp = phaseTimestamp;
                _phaseFrame = unityFrame;
            }

            _extremeFrame++;

            CacheServices();

            if (_ecosystemStressIssued == 0)
                IssueEcosystemStressRequest();

            if (!_baselineCaptured && _extremeFrame >= WarmupFrames && !_scratchBlock.IsCreated)
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
            if (!_baselineCaptured && Time.realtimeSinceStartupAsDouble - _startupTime > StartupTimeoutSeconds)
                FailAndQuit(1, TimeoutHash, TimeoutToken);
        }

        public void LateFrameTick()
        {
            if (!_started || _finished)
                return;

            if (_phaseFrame != Time.frameCount || _phaseStartTimestamp == 0L)
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
            while (GlobalRegistry.Dispatcher == null && Time.realtimeSinceStartupAsDouble - _startupTime <= StartupTimeoutSeconds)
            {
                if (cancellationToken.IsCancellationRequested || _finished)
                    return;

                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested || _finished)
                return;

            if (GlobalRegistry.Dispatcher == null)
            {
                FailAndQuit(1, TimeoutHash, TimeoutToken);
                return;
            }

            ForceHeadlessRuntimePolicy();
            CacheServices();
            _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Core);
            _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Core);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, LateSamplingLayer);
            HectonFloatingOrigin.RegisterListener(this);
            _originListenerRegistered = true;
            _dispatcher?.RequestHeadlessTimeDilation(TimeDilationScalar, RunnerHash);
            _started = _registeredFast && _registeredCold && _registeredLate;
            if (!_started)
                FailAndQuit(1, TimeoutHash, "[RUNNER_REGISTRATION_FAILED]");
        }

        private void InitializeColdState()
        {
            string[] args = global::System.Environment.GetCommandLineArgs();
            int frameFallback = TryReadEnvironmentInt(EnvironmentFramesName, DefaultTargetFrames);
            _targetFrames = math.max(1, TryReadInt(args, "-h8fractureFrames", frameFallback));
            _resultPath = ResolveProjectPath(ResultRelativePath);
            _blackboxPath = ResolveProjectPath(BlackboxRelativePath);
            _h8MemoryDumpPath = ResolveProjectPath(H8MemoryDumpRelativePath);
            EnsureParentDirectory(_resultPath);
            EnsureParentDirectory(_blackboxPath);
            EnsureParentDirectory(_h8MemoryDumpPath);
            TryDeleteFile(_resultPath);
            TryDeleteFile(_resultPath + ".tmp");
            TryDeleteFile(_blackboxPath);
            TryDeleteFile(_h8MemoryDumpPath);
            _blackbox = new NativeArray<FractureTelemetryEntry>(BlackboxFrameCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_blackbox, nameof(_blackbox));
            SignalBus<SectorDehydratedSignal>.EnsureInitialized();
            SignalBus<SectorResidencyHydratedSignal>.EnsureInitialized();
            SignalBus<SwarmDispersedSignal>.EnsureInitialized();
            _staticHPhiMetric = ComputeStaticHPhiMetric();
            Debug.LogWarning(FormatStaticHPhiLog(_staticHPhiMetric, _targetFrames));
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

            _cameraScratch = new Camera[count];
            _cameraCullingMaskScratch = new int[count];
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
            uint frame = unchecked((uint)Time.frameCount);
            long chunkId = unchecked((long)0x4853464200010001UL);
            SignalBus<SectorResidencyHydratedSignal>.Push(new SectorResidencyHydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = frame,
                RadiusMetersQ = 1000,
                Flags = SectorResidencyHydratedSignal.FlagPinned,
                ResidencyState = 1
            });
            SwarmDispersedSignal swarmSignal = new SwarmDispersedSignal
            {
                PositionAup = centerAup,
                RadiusMeters = 250f,
                Intensity01 = 1f,
                SourceId = RunnerHash,
                EstimatedBoidCount = RequestedBoidCount,
                Flags = 1,
                QualityTier = 0
            };
            SignalBus<SwarmDispersedSignal>.Push(in swarmSignal);
            _ecosystemDirectorReadyAtIssue = _ecosystemDirector != null && _ecosystemDirector.IsInitialized ? 1 : 0;
            _ecosystemStressIssued = 1;
            RecordBlackbox(EcosystemStressHash);
        }

        private void EmitSyntheticChunkUnload()
        {
            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -128d, 0d));
            uint frame = unchecked((uint)Time.frameCount);
            long chunkId = unchecked((long)(0x4853464200020000UL | (uint)_extremeFrame));
            SignalBus<SectorDehydratedSignal>.Push(new SectorDehydratedSignal
            {
                CenterAup = centerAup,
                ChunkId = chunkId,
                Frame = frame,
                RadiusMetersQ = 1000,
                Flags = SectorDehydratedSignal.FlagPinned,
                ResidencyState = 0
            });
            _chunkUnloadNativeBytesBaseline = GlobalRegistry.NativeTrackedBytes;
            _chunkUnloadH8BytesBaseline = H8Memory.TotalBytes;
            _chunkUnloadDataVaultBytesBaseline = _dataVault != null ? _dataVault.AllocatedBytes : 0L;
            _chunkUnloadCheckFrame = _extremeFrame + ChunkLeakGraceFrames;
            _chunkUnloadPending = true;
        }

        private void CheckChunkLeakWindow()
        {
            if (!_chunkUnloadPending || _extremeFrame < _chunkUnloadCheckFrame)
                return;

            long nativeBytes = GlobalRegistry.NativeTrackedBytes;
            long h8Bytes = H8Memory.TotalBytes;
            long dataVaultBytes = _dataVault != null ? _dataVault.AllocatedBytes : 0L;
            if (nativeBytes > _chunkUnloadNativeBytesBaseline + LeakToleranceBytes ||
                h8Bytes > _chunkUnloadH8BytesBaseline + LeakToleranceBytes ||
                dataVaultBytes > _chunkUnloadDataVaultBytesBaseline + ScratchBlockBytes)
            {
                FailAndQuit(1, LeakHash, NativeLeakToken);
                return;
            }

            _chunkUnloadPending = false;
        }

        private void PulseScratchMemory()
        {
            if (!_scratchBlock.IsCreated)
            {
                _scratchBaselineH8Bytes = H8Memory.TotalBytes;
                _scratchBaselineH8AllocationCount = H8Memory.ActiveAllocationCount;
                _scratchBlock = H8Memory.Allocate<byte>(
                    ScratchBlockBytes,
                    SystemID.External,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!_scratchBlock.IsCreated)
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
            if (!_scratchBlock.IsCreated)
                return;

            H8Memory.Release(ref _scratchBlock, SystemID.External);
        }

        private void EmitAupShift()
        {
            _shiftSequence++;
            uint sequence = _shiftSequence == 0u ? 1u : _shiftSequence;
            int sign = (sequence & 1u) == 0u ? -1 : 1;
            int3 delta = new int3(sign, 0, 0);
            float3 shiftMeters = new float3(sign * 1000f, 0f, 0f);
            _lastShiftMeters = shiftMeters;
            _dispatcher?.RequestAupPreShiftPause(sequence);
            GlobalSignals.Publish(new AupPreShiftSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                SectorDelta = delta,
                Flags = 1u
            });
            GlobalSignals.Publish(new RebaseSignal
            {
                ShiftMeters = shiftMeters,
                ShiftFrameId = sequence,
                GridDelta = delta,
                Flags = 1u
            });
            GlobalSignals.Publish(new AupShiftSignal
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

            if (!vault.TryGetBuffer<float3>(BufferID.RigidbodyAUPs, out NativeArray<float3> rigidbodyAups) || !rigidbodyAups.IsCreated)
            {
                _rigidbodyScanMissCount++;
                return;
            }

            int length = rigidbodyAups.Length;
            for (int i = 0; i < length; i++)
            {
                float3 value = rigidbodyAups[i];
                if (math.all(math.isfinite(value)))
                    continue;

                _rigidbodyNanIndex = i;
                FailAndQuit(1, NanHash, NanPoisoningToken);
                return;
            }
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
            UnityEngine.Debug.LogError(status);
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

            if (_originListenerRegistered)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _originListenerRegistered = false;
            }
        }

        private void PublishCrashSignal(int exitCode, uint reasonHash, byte severity)
        {
            GlobalSignals.Publish(new CrashTelemetrySignal
            {
                SystemHash = RunnerHash,
                ReasonHash = reasonHash,
                Frame = unchecked((uint)Time.frameCount),
                ExitCode = exitCode,
                NativeAllocationCount = GlobalRegistry.NativeAllocationCount,
                NativeTrackedBytesMb = GlobalRegistry.NativeTrackedBytes * NativeBytesToMegabytes,
                Severity = severity,
                Flags = exitCode == 0 ? (byte)0 : (byte)1
            });
        }

        private void RecordBlackbox(uint eventHash)
        {
            if (!_blackbox.IsCreated)
                return;

            int index = _blackboxCursor % _blackbox.Length;
            _blackbox[index] = new FractureTelemetryEntry
            {
                Frame = unchecked((uint)Time.frameCount),
                ExtremeFrame = unchecked((uint)_extremeFrame),
                ShiftSequence = _shiftSequence,
                EventHash = eventHash,
                NativeBytes = GlobalRegistry.NativeTrackedBytes,
                H8Bytes = H8Memory.TotalBytes,
                NativeAllocations = GlobalRegistry.NativeAllocationCount,
                H8Allocations = H8Memory.ActiveAllocationCount,
                DispatcherPhaseMs = _lastSimulationPhaseMs,
                DataVaultFragmentation = _dataVault != null ? _dataVault.HeapFragmentationRatio : 0f,
                LastShiftMeters = _lastShiftMeters,
                Flags = ComposeBlackboxFlags()
            };
            _blackboxCursor++;
        }

        private uint ComposeBlackboxFlags()
        {
            uint flags = 0u;
            if (_scratchBlock.IsCreated)
                flags |= 1u;
            if (_baselineCaptured)
                flags |= 1u << 1;
            if (_chunkUnloadPending)
                flags |= 1u << 2;
            if (_ecosystemStressIssued != 0)
                flags |= 1u << 3;
            if (_dataVault == null)
                flags |= 1u << 4;
            return flags;
        }

        private void TryDumpBlackbox()
        {
            try
            {
                DumpBlackbox();
            }
            catch (Exception)
            {
            }
        }

        private void DumpBlackbox()
        {
            if (!_blackbox.IsCreated || string.IsNullOrEmpty(_blackboxPath))
                return;

            EnsureParentDirectory(_blackboxPath);
            using (FileStream stream = new FileStream(_blackboxPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(0x48534642u);
                int validCount = math.min(_blackboxCursor, _blackbox.Length);
                int start = _blackboxCursor >= _blackbox.Length ? _blackboxCursor % _blackbox.Length : 0;
                writer.Write(validCount);
                writer.Write(BlackboxEntrySizeBytes);
                writer.Write(_blackboxCursor);
                for (int i = 0; i < validCount; i++)
                {
                    int index = (start + i) % _blackbox.Length;
                    FractureTelemetryEntry entry = _blackbox[index];
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
                EnsureParentDirectory(_resultPath);
                using (StreamWriter writer = new StreamWriter(tempPath, false))
                {
                    writer.Write('{');
                    writer.Write("\"agent\":\"");
                    writer.Write(AgentName);
                    writer.Write("\",\"status\":\"");
                    WriteJsonEscaped(writer, status);
                    writer.Write("\",\"exitCode\":");
                    WriteInvariant(writer, exitCode);
                    writer.Write(",\"extremeFrames\":");
                    WriteInvariant(writer, _extremeFrame);
                    writer.Write(",\"targetFrames\":");
                    WriteInvariant(writer, _targetFrames);
                    writer.Write(",\"aupShiftCount\":");
                    WriteInvariant(writer, _shiftSequence);
                    writer.Write(",\"originShiftCallbacks\":");
                    WriteInvariant(writer, _originShiftCount);
                    writer.Write(",\"simulationPhaseMs\":");
                    WriteInvariant(writer, _lastSimulationPhaseMs);
                    writer.Write(",\"nativeBytesBaseline\":");
                    WriteInvariant(writer, _nativeBytesBaseline);
                    writer.Write(",\"nativeBytesFinal\":");
                    WriteInvariant(writer, GlobalRegistry.NativeTrackedBytes);
                    writer.Write(",\"h8BytesBaseline\":");
                    WriteInvariant(writer, _h8BytesBaseline);
                    writer.Write(",\"h8BytesFinal\":");
                    WriteInvariant(writer, H8Memory.TotalBytes);
                    writer.Write(",\"dataVaultBytesBaseline\":");
                    WriteInvariant(writer, _dataVaultBytesBaseline);
                    writer.Write(",\"dataVaultBytesFinal\":");
                    WriteInvariant(writer, _dataVault != null ? _dataVault.AllocatedBytes : 0L);
                    writer.Write(",\"nativeAllocationBaselineCount\":");
                    WriteInvariant(writer, _nativeAllocationBaselineCount);
                    writer.Write(",\"nativeAllocationFinalCount\":");
                    WriteInvariant(writer, GlobalRegistry.NativeAllocationCount);
                    writer.Write(",\"h8AllocationBaselineCount\":");
                    WriteInvariant(writer, _h8AllocationBaselineCount);
                    writer.Write(",\"h8AllocationFinalCount\":");
                    WriteInvariant(writer, H8Memory.ActiveAllocationCount);
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

        private static void WriteJsonHexNibble(StreamWriter writer, int value)
        {
            int nibble = value & 0xF;
            writer.Write((char)(nibble < 10 ? '0' + nibble : 'A' + (nibble - 10)));
        }

        private static bool ShouldRunStatic()
        {
            if (HasCommandLineArg(CommandLineArg))
                return true;

            string value = global::System.Environment.GetEnvironmentVariable(EnvironmentFlagName);
            if (string.Equals(value, "1", StringComparison.Ordinal) || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                return true;

            return HasFreshFlagFile(ResolveProjectPathStatic(FlagRelativePath));
        }

        private static bool HasFreshFlagFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                DateTime lastWriteUtc = File.GetLastWriteTimeUtc(path);
                DateTime nowUtc = DateTime.UtcNow;
                if (lastWriteUtc > nowUtc)
                    return true;

                return (nowUtc - lastWriteUtc).TotalSeconds <= FlagMaxAgeSeconds;
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
                        int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int separatedValue))
                    {
                        return separatedValue;
                    }

                    continue;
                }

                int separatorIndex = name.Length;
                if (arg.Length > separatorIndex + 1 &&
                    arg[separatorIndex] == '=' &&
                    string.Compare(arg, 0, name, 0, separatorIndex, StringComparison.OrdinalIgnoreCase) == 0 &&
                    int.TryParse(arg.AsSpan(separatorIndex + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int inlineValue))
                {
                    return inlineValue;
                }
            }

            return fallback;
        }

        private static int TryReadEnvironmentInt(string name, int fallback)
        {
            string value = global::System.Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static float ComputeStaticHPhiMetric()
        {
            try
            {
                string scriptsRoot = Path.Combine(ResolveProjectRootStatic(), "Assets", "_Project", "Scripts");
                if (!Directory.Exists(scriptsRoot))
                    return 0f;

                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                int nativeRefs = 0;
                int signalRefs = 0;
                int vaultRefs = 0;
                int aupRefs = 0;
                int burstRefs = 0;
                for (int i = 0; i < files.Length; i++)
                {
                    string text;
                    try
                    {
                        text = File.ReadAllText(files[i]);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    nativeRefs += CountOrdinal(text, "NativeArray<");
                    nativeRefs += CountOrdinal(text, "NativeQueue<");
                    signalRefs += CountOrdinal(text, "SignalBus<");
                    signalRefs += CountOrdinal(text, "GlobalSignals.Publish");
                    vaultRefs += CountOrdinal(text, "GlobalDataVault");
                    vaultRefs += CountOrdinal(text, "IDataVault");
                    aupRefs += CountOrdinal(text, "AbsoluteUniversePosition");
                    burstRefs += CountOrdinal(text, "BurstCompile");
                }

                float numerator = nativeRefs * 3f + signalRefs * 2f + vaultRefs * 5f + aupRefs * 2f + burstRefs;
                return files.Length > 0 ? numerator / (files.Length * 1000f) : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
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

        private static string FormatStaticHPhiLog(float metric, int targetFrames)
        {
            return "[H-PHI_STATIC] " + AgentName + " value=" + metric.ToString("F6", CultureInfo.InvariantCulture) + " requestedBoids=10000 frames=" + targetFrames.ToString(CultureInfo.InvariantCulture);
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

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, AgentName, label, NativeAllocationLifetime.Session);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
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

        [StructLayout(LayoutKind.Sequential, Size = BlackboxEntrySizeBytes)]
        private struct FractureTelemetryEntry
        {
            public uint Frame;
            public uint ExtremeFrame;
            public uint ShiftSequence;
            public uint EventHash;
            public long NativeBytes;
            public long H8Bytes;
            public int NativeAllocations;
            public int H8Allocations;
            public float DispatcherPhaseMs;
            public float DataVaultFragmentation;
            public float3 LastShiftMeters;
            public uint Flags;
        }
    }
}
