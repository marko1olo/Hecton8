using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Networking
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8850)]
    public sealed unsafe class HectonRollbackNetcodeRuntime : MonoBehaviour, IDispatcherFixedSystem, ILateFrameTickable
    {
        private const uint FixedSystemHash = 0x4E465852u;
        private const uint PauseSourceHash = 0x4E455452u;
        private const uint LegacyProfileMagic = 0x4E455450u;
        private const uint LegacyProfileVersion = 1u;
        private const int CsvPollIntervalFrames = 300;
        private const int SimulatedPingFrames200Ms = 12;
        private const float MoveMismatchEpsilon = 0.001f;
        private const float LookMismatchEpsilon = 0.001f;
        private const string LegacyProfileRelativePath = "Docs/Archive/netcode_latency_profiles.h8bin";
        private const string CsvProfileRelativePath = "netcode_profiles.csv";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_NETCODE_SURGEON.bin";

        private static HectonRollbackNetcodeRuntime _activeInstance;
        private static uint _modeFlags;
        private static uint _pauseSequence;

        private IDataVault _vault;
        private VaultBufferHandle<byte> _stateRingHandle;
        private VaultBufferHandle<FrameSnapshotDTO> _frameSnapshotHandle;
        private VaultBufferHandle<RollbackRuntimeStateDTO> _runtimeStateHandle;
        private VaultBufferHandle<RemoteInputFrameDTO> _remoteInputHandle;
        private VaultBufferHandle<MockTickCommand> _tickCommandHandle;
        private VaultBufferHandle<VisualStateDTO> _visualStateHandle;
        private VaultBufferHandle<NetcodeTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<RollbackTuningDTO> _tuningHandle;
        private VaultBufferHandle<RollbackAudioSuppressionDTO> _audioSuppressionHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<RollbackLegacyProfileDTO> _latencyProfileHandle;
        private VaultBufferHandle<InputStateDTO> _inputJournalHandle;
        private int _snapshotStrideBytes;
        private int _registeredFixedDispatcher;
        private int _registeredLateFrame;
        private int _buffersReady;
        private int _nextCsvPollFrame;
        private int _telemetryWriteIndex;
        private uint _frame;
        private uint _lastDumpFrame = uint.MaxValue;
        private string _projectRoot;
        private string _legacyProfilePath;
        private string _csvProfilePath;
        private string _dumpPath;

        public static HectonRollbackNetcodeRuntime ActiveInstance => _activeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _activeInstance = null;
            _modeFlags = 0u;
            _pauseSequence = 0u;
        }

        public static bool TrySetMode(bool server, bool client)
        {
            _modeFlags = RollbackNetcodeFlags.Active;
            if (server)
                _modeFlags |= RollbackNetcodeFlags.ServerMode;
            if (client)
                _modeFlags |= RollbackNetcodeFlags.ClientMode;

            if (_activeInstance == null)
                return false;

            return _activeInstance.ApplyModeFlags(_modeFlags);
        }

        public static bool TryStopMode()
        {
            _modeFlags = 0u;
            if (_activeInstance == null)
                return false;

            return _activeInstance.ApplyModeFlags(0u);
        }

        public static bool TryGetTuning(out RollbackTuningDTO tuning)
        {
            tuning = default;
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            NativeArray<RollbackTuningDTO> tuningBuffer = _activeInstance._tuningHandle.Resolve(_activeInstance._vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TrySetTuning(in RollbackTuningDTO tuning)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            NativeArray<RollbackTuningDTO> tuningBuffer = _activeInstance._tuningHandle.Resolve(_activeInstance._vault);
            if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                return false;

            RollbackTuningDTO sanitized = SanitizeTuning(tuning);
            tuningBuffer[0] = sanitized;
            return true;
        }

        public static bool TryGetRuntimeState(out RollbackRuntimeStateDTO state)
        {
            state = default;
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            NativeArray<RollbackRuntimeStateDTO> runtime = _activeInstance._runtimeStateHandle.Resolve(_activeInstance._vault);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return false;

            state = runtime[0];
            return true;
        }

        public static bool TryGetVisualStates(out NativeArray<VisualStateDTO> visualStates)
        {
            visualStates = default;
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            visualStates = _activeInstance._visualStateHandle.Resolve(_activeInstance._vault);
            return visualStates.IsCreated;
        }

        public static bool InjectRemoteInput(uint frame, in InputStateDTO input, uint flags = RemoteInputFlags.Received)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            NativeArray<RemoteInputFrameDTO> remote = _activeInstance._remoteInputHandle.Resolve(_activeInstance._vault);
            if (!remote.IsCreated || remote.Length <= 0)
                return false;

            int index = (int)(frame % (uint)remote.Length);
            remote[index] = new RemoteInputFrameDTO
            {
                Input = input,
                Frame = frame,
                Flags = flags | RemoteInputFlags.Received
            };

            return true;
        }

        public static bool InjectRemoteFrameHash(uint frame, ulong frameHash64)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            NativeArray<RollbackRuntimeStateDTO> runtime = _activeInstance._runtimeStateHandle.Resolve(_activeInstance._vault);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return false;

            RollbackRuntimeStateDTO state = runtime[0];
            state.LastRemoteFrame = frame;
            state.LastRemoteHash64 = frameHash64;
            runtime[0] = state;
            return true;
        }

        public static bool Simulate200MsPing()
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            return _activeInstance.SimulatePingInternal(SimulatedPingFrames200Ms);
        }

        private void Awake()
        {
            if (_activeInstance != null && _activeInstance != this)
            {
                enabled = false;
                return;
            }

            _activeInstance = this;
            ResolveColdPaths();
            TryEnsureBuffers();
            ApplyModeFlags(_modeFlags);
        }

        private void OnEnable()
        {
            _activeInstance = this;
            TryRegisterDispatch();
        }

        private void OnDisable()
        {
            if (_registeredFixedDispatcher != 0)
            {
                GlobalRegistry.UnregisterDispatcherFixedSystem(this);
                _registeredFixedDispatcher = 0;
            }

            if (_registeredLateFrame != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _registeredLateFrame = 0;
            }

            if (_activeInstance == this)
                _activeInstance = null;
        }

        public uint GetFixedSystemIdHash()
        {
            return FixedSystemHash;
        }

        public JobHandle ScheduleFixedSimulation(in DispatcherTimingDTO timing, JobHandle dependsOn)
        {
            if (!TryEnsureBuffers())
                return dependsOn;

            uint currentFrame = _frame++;
            float quality = ResolveGlobalQualityWeight();
            NativeArray<RollbackTuningDTO> tuningBuffer = _tuningHandle.Resolve(_vault);
            NativeArray<RollbackRuntimeStateDTO> runtime = _runtimeStateHandle.Resolve(_vault);
            if (!tuningBuffer.IsCreated || !runtime.IsCreated)
                return dependsOn;

            RollbackTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuningBuffer[0] = tuning;

            NativeArray<InputStateDTO> inputJournal = _inputJournalHandle.Resolve(_vault);
            NativeArray<RemoteInputFrameDTO> remoteInput = _remoteInputHandle.Resolve(_vault);
            NativeArray<byte> stateRing = _stateRingHandle.Resolve(_vault);
            NativeArray<FrameSnapshotDTO> snapshots = _frameSnapshotHandle.Resolve(_vault);
            NativeArray<MockTickCommand> commands = _tickCommandHandle.Resolve(_vault);
            NativeArray<RollbackAudioSuppressionDTO> audio = _audioSuppressionHandle.Resolve(_vault);
            NativeArray<VisualStateDTO> visualStates = _visualStateHandle.Resolve(_vault);
            NativeArray<NetcodeTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);

            int telemetryIndex = _telemetryWriteIndex;
            if (telemetry.IsCreated && telemetry.Length > 0)
            {
                int nextTelemetryIndex = telemetryIndex + 1;
                if (nextTelemetryIndex >= telemetry.Length)
                    nextTelemetryIndex = 0;
                _telemetryWriteIndex = nextTelemetryIndex;
            }

            RollbackFixedPipelineJob pipeline = new RollbackFixedPipelineJob
            {
                Tuning = tuningBuffer,
                RuntimeState = runtime,
                PredictedJournal = inputJournal,
                RemoteInputRing = remoteInput,
                StateRingBuffer = stateRing,
                FrameSnapshots = snapshots,
                Commands = commands,
                AudioSuppression = audio,
                VisualStates = visualStates,
                Telemetry = telemetry,
                RigidbodyAups = ResolveLiveBuffer<AbsoluteUniversePosition>(BufferID.RigidbodyAUPs),
                PlayerStates = ResolveLiveBuffer<LockstepPlayerKinematicState>(BufferID.PlayerKinematicState),
                EntityAups = ResolveLiveBuffer<AbsoluteUniversePosition>(BufferID.EntityAUPs),
                EntityVelocities = ResolveLiveBuffer<float3>(BufferID.EntityVelocities),
                RoomWaterLevels = ResolveLiveBuffer<float>(BufferID.RoomWaterLevels),
                CurrentFrame = currentFrame,
                ModeFlags = _modeFlags,
                RingFrameCapacity = RollbackNetcodeConstants.StateRingFrameCapacity,
                SnapshotStrideBytes = _snapshotStrideBytes,
                MaxRollbackFrames = RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, quality),
                MaxRigidbodyAups = RollbackNetcodeConstants.MaxRigidbodyAups,
                MaxPlayerStates = RollbackNetcodeConstants.MaxPlayerStates,
                MaxEntityAups = RollbackNetcodeConstants.MaxEntityAups,
                MaxEntityVelocities = RollbackNetcodeConstants.MaxEntityVelocities,
                MaxRoomWaterLevels = RollbackNetcodeConstants.MaxRoomWaterLevels,
                GlobalQualityWeight = quality,
                MoveEpsilon = MoveMismatchEpsilon,
                LookEpsilon = LookMismatchEpsilon,
                TelemetryWriteIndex = telemetryIndex,
                ModQuarantineMask = ResolveModQuarantineMask()
            };

            JobHandle handle = pipeline.Schedule(dependsOn);
            H8Memory.RegisterActiveJob(RollbackNetcodeVault.OwnerSystem, handle);
            return handle;
        }

        public void PostFixedSimulation(in DispatcherTimingDTO timing)
        {
            if (!TryEnsureBuffers())
                return;

            NativeArray<RollbackRuntimeStateDTO> runtime = _runtimeStateHandle.Resolve(_vault);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return;

            RollbackRuntimeStateDTO state = runtime[0];
            if ((state.Flags & RollbackNetcodeFlags.HashMismatch) != 0u &&
                state.LastRemoteHash64 != 0UL &&
                state.LastFrameHash64 != 0UL &&
                state.LastRemoteHash64 != state.LastFrameHash64)
            {
                PublishPauseSignal(state.CurrentFrame);
                DumpNetcodeBlackBox(state.CurrentFrame, state.Flags);
                state.LastRemoteHash64 = state.LastFrameHash64;
                runtime[0] = state;
                return;
            }

            if ((state.Flags & RollbackNetcodeFlags.ResimBudgetExceeded) != 0u)
                DumpNetcodeBlackBox(state.CurrentFrame, state.Flags);
        }

        public void LateFrameTick()
        {
            if (!TryEnsureBuffers())
                return;

            NativeArray<VisualStateDTO> visualStates = _visualStateHandle.Resolve(_vault);
            BlendVisualStates(visualStates);

            if ((uint)Time.frameCount >= (uint)_nextCsvPollFrame)
            {
                _nextCsvPollFrame = Time.frameCount + CsvPollIntervalFrames;
                TryApplyCsvOverride();
            }
        }

        private bool ApplyModeFlags(uint flags)
        {
            if (!TryEnsureBuffers())
                return false;

            NativeArray<RollbackRuntimeStateDTO> runtime = _runtimeStateHandle.Resolve(_vault);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return false;

            RollbackRuntimeStateDTO state = runtime[0];
            state.Flags = (state.Flags & ~(RollbackNetcodeFlags.Active | RollbackNetcodeFlags.ServerMode | RollbackNetcodeFlags.ClientMode)) | flags;
            runtime[0] = state;
            return true;
        }

        private bool TryEnsureBuffers()
        {
            if (_buffersReady != 0 && _vault != null)
                return true;

            _vault = GlobalRegistry.DataVault;
            if (_vault == null)
                return false;

            _snapshotStrideBytes = RollbackNetcodeConstants.ResolveSnapshotStrideBytes();
            int stateRingBytes = _snapshotStrideBytes * RollbackNetcodeConstants.StateRingFrameCapacity;
            _stateRingHandle = _vault.GetBufferHandle<byte>(RollbackNetcodeVault.StateRingBuffer, stateRingBytes, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _frameSnapshotHandle = _vault.GetBufferHandle<FrameSnapshotDTO>(RollbackNetcodeVault.FrameSnapshots, RollbackNetcodeConstants.StateRingFrameCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _runtimeStateHandle = _vault.GetBufferHandle<RollbackRuntimeStateDTO>(RollbackNetcodeVault.RuntimeState, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _remoteInputHandle = _vault.GetBufferHandle<RemoteInputFrameDTO>(RollbackNetcodeVault.RemoteInputRing, RollbackNetcodeConstants.InputRingCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _tickCommandHandle = _vault.GetBufferHandle<MockTickCommand>(RollbackNetcodeVault.TickCommands, RollbackNetcodeConstants.CommandCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _visualStateHandle = _vault.GetBufferHandle<VisualStateDTO>(RollbackNetcodeVault.VisualStates, RollbackNetcodeConstants.VisualStateCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _vault.GetBufferHandle<NetcodeTelemetryEntry>(RollbackNetcodeVault.TelemetryRing, RollbackNetcodeConstants.TelemetryFrameCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.GetBufferHandle<RollbackTuningDTO>(RollbackNetcodeVault.Tuning, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _audioSuppressionHandle = _vault.GetBufferHandle<RollbackAudioSuppressionDTO>(RollbackNetcodeVault.AudioSuppression, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _vault.GetBufferHandle<byte>(RollbackNetcodeVault.CsvScratch, RollbackNetcodeConstants.CsvScratchBytes, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _latencyProfileHandle = _vault.GetBufferHandle<RollbackLegacyProfileDTO>(RollbackNetcodeVault.LatencyProfile, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);

            if (!_vault.TryGetBufferHandle(BufferID.ShinobuInputJournalRing, out _inputJournalHandle))
                _inputJournalHandle = _vault.GetBufferHandle<InputStateDTO>(BufferID.ShinobuInputJournalRing, RollbackNetcodeConstants.InputRingCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);

            EnsureDefaultTuning();
            if (!TryLoadLegacyLatencyProfile())
                GenerateEmergencyMockNetcode();

            _buffersReady = 1;
            TryRegisterDispatch();
            return true;
        }

        private void EnsureDefaultTuning()
        {
            NativeArray<RollbackTuningDTO> tuning = _tuningHandle.Resolve(_vault);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            if (tuning[0].MaxRollbackFrames > 0)
                return;

            RollbackTuningDTO defaults = default;
            defaults.MaxRollbackFrames = RollbackNetcodeConstants.MaxRollbackFrames;
            defaults.VisualInterpolationFrames = 3;
            defaults.VisualInterpolationSeconds = RollbackNetcodeConstants.DefaultVisualInterpolationSeconds;
            defaults.InputPredictionAggressiveness = RollbackNetcodeConstants.DefaultPredictionAggressiveness;
            defaults.MinQualityForLookRollback = RollbackNetcodeConstants.DefaultLookRollbackMinQuality;
            defaults.GlobalQualityWeight = 1f;
            defaults.Flags = _modeFlags;
            tuning[0] = defaults;
        }

        private bool TryLoadLegacyLatencyProfile()
        {
            if (string.IsNullOrEmpty(_legacyProfilePath) || !File.Exists(_legacyProfilePath))
                return false;

            NativeArray<RollbackLegacyProfileDTO> profileBuffer = _latencyProfileHandle.Resolve(_vault);
            if (!profileBuffer.IsCreated || profileBuffer.Length <= 0)
                return false;

            RollbackLegacyProfileDTO profile = default;
            try
            {
                using FileStream stream = new FileStream(_legacyProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length < 32L)
                    return false;

                Span<byte> bytes = stackalloc byte[32];
                int read = stream.Read(bytes);
                if (read < 32)
                    return false;

                uint magicLe = ReadUInt32(bytes, 0, bigEndian: false);
                bool bigEndian = magicLe != LegacyProfileMagic &&
                    ReadUInt32(bytes, 0, bigEndian: true) == LegacyProfileMagic;
                profile.Magic = bigEndian ? LegacyProfileMagic : magicLe;
                profile.Version = ReadUInt32(bytes, 4, bigEndian);
                profile.SimulatedPingMs = ReadUInt32(bytes, 8, bigEndian);
                profile.JitterMs = ReadUInt32(bytes, 12, bigEndian);
                profile.PacketLoss01 = math.asfloat(ReadUInt32(bytes, 16, bigEndian));
                profile.PredictionAggressiveness = math.asfloat(ReadUInt32(bytes, 20, bigEndian));
                profile.MaxRollbackFrames = ReadUInt32(bytes, 24, bigEndian);
                profile.Flags = ReadUInt32(bytes, 28, bigEndian);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            if (profile.Magic != LegacyProfileMagic || profile.Version != LegacyProfileVersion)
                return false;

            profileBuffer[0] = profile;
            NativeArray<RollbackTuningDTO> tuningBuffer = _tuningHandle.Resolve(_vault);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                RollbackTuningDTO tuning = tuningBuffer[0];
                tuning.MaxRollbackFrames = (int)math.clamp(profile.MaxRollbackFrames, 1u, (uint)RollbackNetcodeConstants.MaxRollbackFrames);
                tuning.InputPredictionAggressiveness = math.saturate(profile.PredictionAggressiveness);
                tuningBuffer[0] = SanitizeTuning(tuning);
            }

            return true;
        }

        private void GenerateEmergencyMockNetcode()
        {
            NativeArray<RollbackRuntimeStateDTO> runtime = _runtimeStateHandle.Resolve(_vault);
            if (runtime.IsCreated && runtime.Length > 0)
            {
                RollbackRuntimeStateDTO state = runtime[0];
                state.Flags |= RollbackNetcodeFlags.EmergencyMock;
                runtime[0] = state;
            }

            NativeArray<RemoteInputFrameDTO> remote = _remoteInputHandle.Resolve(_vault);
            if (!remote.IsCreated)
                return;

            for (int i = 0; i < math.min(remote.Length, 16); i++)
            {
                remote[i] = new RemoteInputFrameDTO
                {
                    Frame = (uint)i,
                    Flags = RemoteInputFlags.Predicted
                };
            }
        }

        private bool SimulatePingInternal(int delayedFrames)
        {
            NativeArray<InputStateDTO> journal = _inputJournalHandle.Resolve(_vault);
            if (!journal.IsCreated || journal.Length <= 0)
                return false;

            uint frame = _frame > (uint)delayedFrames ? _frame - (uint)delayedFrames : 0u;
            InputStateDTO input = journal[(int)(frame % (uint)journal.Length)];
            input.ButtonMask ^= 1u;
            return InjectRemoteInput(frame, in input, RemoteInputFlags.Received);
        }

        private static void BlendVisualStates(NativeArray<VisualStateDTO> visualStates)
        {
            if (!visualStates.IsCreated)
                return;

            for (int i = 0; i < visualStates.Length; i++)
            {
                VisualStateDTO state = visualStates[i];
                if ((state.Flags & 1u) == 0u)
                    continue;

                state.Blend01 = math.saturate(state.Blend01 + state.BlendStep01);
                state.InterpolatedLocalMeters = math.lerp(state.InterpolatedLocalMeters, state.TrueLocalMeters, state.Blend01);
                if (state.Blend01 >= 0.999f)
                    state.Flags &= ~1u;
                visualStates[i] = state;
            }
        }

        private NativeArray<T> ResolveLiveBuffer<T>(BufferID bufferId) where T : struct
        {
            return _vault != null && _vault.TryGetBuffer(bufferId, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static uint ResolveModQuarantineMask()
        {
            return 0x4D4F4450u;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        private static RollbackTuningDTO SanitizeTuning(in RollbackTuningDTO source)
        {
            RollbackTuningDTO tuning = source;
            tuning.MaxRollbackFrames = math.clamp(tuning.MaxRollbackFrames, 1, RollbackNetcodeConstants.MaxRollbackFrames);
            tuning.VisualInterpolationFrames = math.clamp(tuning.VisualInterpolationFrames, 1, 12);
            tuning.VisualInterpolationSeconds = math.clamp(tuning.VisualInterpolationSeconds, 0.016f, 0.25f);
            tuning.InputPredictionAggressiveness = math.saturate(tuning.InputPredictionAggressiveness);
            tuning.MinQualityForLookRollback = math.saturate(tuning.MinQualityForLookRollback);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            return tuning;
        }

        private void TryRegisterDispatch()
        {
            if (!Application.isPlaying)
                return;

            if (_registeredFixedDispatcher == 0 && GlobalRegistry.TryRegisterDispatcherFixedSystem(this))
                _registeredFixedDispatcher = 1;
            if (_registeredLateFrame == 0 && GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core))
                _registeredLateFrame = 1;
        }

        private void ResolveColdPaths()
        {
            _projectRoot = ResolveProjectRoot();
            _legacyProfilePath = Path.Combine(_projectRoot, LegacyProfileRelativePath);
            _csvProfilePath = Path.Combine(_projectRoot, CsvProfileRelativePath);
            _dumpPath = Path.Combine(_projectRoot, DumpRelativePath);
        }

        private static string ResolveProjectRoot()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(currentDirectory))
                return "C:\\hades\\Hecton8";

            return Path.GetFileName(currentDirectory) == "Hecton8"
                ? currentDirectory
                : Path.Combine(currentDirectory, "Hecton8");
        }

        private void TryApplyCsvOverride()
        {
            if (string.IsNullOrEmpty(_csvProfilePath) || !File.Exists(_csvProfilePath))
                return;

            NativeArray<byte> scratch = _csvScratchHandle.Resolve(_vault);
            NativeArray<RollbackTuningDTO> tuningBuffer = _tuningHandle.Resolve(_vault);
            if (!scratch.IsCreated || !tuningBuffer.IsCreated || scratch.Length <= 0 || tuningBuffer.Length <= 0)
                return;

            int byteCount;
            try
            {
                using FileStream stream = new FileStream(_csvProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                byteCount = stream.Read(new Span<byte>(scratchPtr, scratch.Length));
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            if (byteCount <= 0)
                return;

            RollbackTuningDTO tuning = tuningBuffer[0];
            ParseCsvBytes(scratch, byteCount, ref tuning);
            tuningBuffer[0] = SanitizeTuning(tuning);
        }

        private static void ParseCsvBytes(NativeArray<byte> bytes, int byteCount, ref RollbackTuningDTO tuning)
        {
            uint keyHash = 2166136261u;
            int value = 0;
            int decimalDivisor = 0;
            bool readingValue = false;
            bool negative = false;
            bool hasValue = false;

            for (int i = 0; i <= byteCount; i++)
            {
                byte c = i < byteCount ? bytes[i] : (byte)'\n';
                if (c == '\r')
                    continue;

                if (!readingValue)
                {
                    if (c == ',' || c == '=')
                    {
                        readingValue = true;
                        value = 0;
                        decimalDivisor = 0;
                        negative = false;
                        hasValue = false;
                        continue;
                    }

                    if (c == '\n')
                    {
                        keyHash = 2166136261u;
                        continue;
                    }

                    if (c > 32)
                        keyHash = (keyHash ^ ToLowerAscii(c)) * 16777619u;
                    continue;
                }

                if (c == '-')
                {
                    negative = true;
                    continue;
                }

                if (c == '.')
                {
                    decimalDivisor = 1;
                    continue;
                }

                if (c >= '0' && c <= '9')
                {
                    hasValue = true;
                    value = (value * 10) + (c - '0');
                    if (decimalDivisor > 0)
                        decimalDivisor *= 10;
                    continue;
                }

                if (c == '\n' || c == ';')
                {
                    if (hasValue)
                        ApplyCsvValue(keyHash, negative ? -value : value, decimalDivisor, ref tuning);

                    keyHash = 2166136261u;
                    readingValue = false;
                }
            }
        }

        private static void ApplyCsvValue(uint keyHash, int rawValue, int decimalDivisor, ref RollbackTuningDTO tuning)
        {
            float value = decimalDivisor > 1 ? rawValue / (float)decimalDivisor : rawValue;
            if (keyHash == HashLowerAscii("max_rollback_frames"))
                tuning.MaxRollbackFrames = (int)value;
            else if (keyHash == HashLowerAscii("visual_interpolation_frames"))
                tuning.VisualInterpolationFrames = (int)value;
            else if (keyHash == HashLowerAscii("visual_interpolation_seconds"))
                tuning.VisualInterpolationSeconds = value;
            else if (keyHash == HashLowerAscii("input_prediction_aggressiveness"))
                tuning.InputPredictionAggressiveness = value;
            else if (keyHash == HashLowerAscii("min_quality_for_look_rollback"))
                tuning.MinQualityForLookRollback = value;
        }

        private static uint HashLowerAscii(string key)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < key.Length; i++)
                hash = (hash ^ (byte)key[i]) * 16777619u;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLowerAscii(byte value)
        {
            return value >= 'A' && value <= 'Z' ? (byte)(value + 32) : value;
        }

        private void PublishPauseSignal(uint currentFrame)
        {
            SystemPauseSignal signal = default;
            signal.SourceHash = PauseSourceHash;
            signal.Frame = currentFrame;
            signal.Sequence = ++_pauseSequence;
            signal.Paused = 1;
            signal.Flags = 1;
            signal.RestoreScalar = 1f;
            SignalBus<SystemPauseSignal>.Push(in signal);
        }

        private void DumpNetcodeBlackBox(uint currentFrame, uint flags)
        {
            if (_lastDumpFrame == currentFrame || string.IsNullOrEmpty(_dumpPath))
                return;

            _lastDumpFrame = currentFrame;
            try
            {
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                NativeArray<NetcodeTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(PauseSourceHash);
                writer.Write(currentFrame);
                writer.Write(flags);
                writer.Write(telemetry.IsCreated ? telemetry.Length : 0);
                if (!telemetry.IsCreated)
                    return;

                for (int i = 0; i < telemetry.Length; i++)
                {
                    NetcodeTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.LastRollbackFrame);
                    writer.Write(entry.RollbacksTriggered);
                    writer.Write(entry.FramesResimulated);
                    writer.Write(entry.ResimComputeTimeMs);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.FrameHash64);
                    writer.Write(entry.RemoteHash64);
                    writer.Write(entry.Flags);
                    writer.Write(entry.InputMaskP1);
                    writer.Write(entry.InputMaskP2);
                    writer.Write(entry.StateMemoryOffset);
                    writer.Write(entry.SnapshotBytes);
                    writer.Write(entry.MismatchFrame);
                    writer.Write(entry.Reserved0);
                    writer.Write(entry.Reserved1);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
        {
            uint value = (uint)(bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24));
            return bigEndian ? ReverseBytes(value) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                ((value & 0x0000FF00u) << 8) |
                ((value & 0x00FF0000u) >> 8) |
                ((value & 0xFF000000u) >> 24);
        }
    }
}
