using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Networking
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8850)]
    public sealed unsafe class HectonRollbackNetcodeRuntime : MonoBehaviour, IDispatcherFixedSystem, IDispatcherFenceDomainProvider, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HectonRollbackNetcodeRuntimeSignalPushDropCount;
        private const uint FixedSystemHash = 0x4E465852u;
        private const uint PauseSourceHash = 0x4E455452u;
        private const uint LegacyProfileMagic = 0x4E455450u;
        private const uint LegacyProfileVersion = 1u;
#if UNITY_EDITOR
        private const int CsvPollIntervalFrames = 300;
#endif
        private const int SimulatedPingFrames200Ms = 12;
        private const float MoveMismatchEpsilon = 0.001f;
        private const float LookMismatchEpsilon = 0.001f;
        private const string LegacyProfileRelativePath = "Docs/Archive/netcode_latency_profiles.h8bin";
#if UNITY_EDITOR
        private const string CsvProfileRelativePath = "netcode_input_profiles.csv";
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_278.bin";
#if UNITY_EDITOR
        private const uint CsvHashMaxRollbackFrames = 0x09632D65u;
        private const uint CsvHashMaxRollbackDepth = 0x5E49FC48u;
        private const uint CsvHashVisualInterpolationFrames = 0xD47FD347u;
        private const uint CsvHashVisualInterpolationSeconds = 0x241D8D82u;
        private const uint CsvHashInputPredictionAggressiveness = 0x9E6F9FA1u;
        private const uint CsvHashMinQualityForLookRollback = 0xE3B8571Au;
        private const uint CsvHashInputDelayTicks = 0x5D051806u;
        private const uint CsvHashInputDelayFrames = 0x3EC0325Eu;
        private const uint CsvHashRedundancyCount = 0x90EDE57Au;
        private const uint CsvHashPacketLossPermille = 0xC5CC60C2u;
        private const uint CsvHashDuplicatePermille = 0x38E0BA0Bu;
        private const uint CsvHashCadenceFrames = 0x155ED66Cu;
        private const uint CsvHashMaxMerkleLeaves = 0x49982615u;
        private const uint CsvHashExtrapolationDecay = 0x1DEF9A6Cu;
        private const uint CsvHashExtrapolationDecayPermille = 0xFD28EC45u;
        private const uint CsvHashPredictionWindow = 0x594C7FE1u;
        private const uint CsvHashPredictionWindowTicks = 0xFE533844u;
        private const uint CsvHashBufferCapacity = 0x34F22EE8u;
        private const uint CsvHashBufferSize = 0xD91F0545u;
        private const uint CsvHashLatencyThresholdFrames = 0xAB55CC6Cu;
        private const uint CsvHashLatencyFrames = 0x4F4C0EB0u;
        private const uint CsvHashActiveProfile = 0xA1F3E155u;
        private const uint CsvHashDefaultProfile = 0x933B5BDEu;
        private const uint CsvHashGlobalProfile = 0x1DFF06AEu;
        private const uint CsvHashGenericProfile = 0x51CCEFFAu;
#endif

        private static HectonRollbackNetcodeRuntime _activeInstance;
        private static uint _modeFlags;
        private static uint _pauseSequence;

        private IDataVault _vault;
        private VaultGenerationHandle<byte> _stateRingHandle;
        private VaultGenerationHandle<FrameSnapshotDTO> _frameSnapshotHandle;
        private VaultGenerationHandle<RollbackRuntimeStateDTO> _runtimeStateHandle;
        private VaultGenerationHandle<RemoteInputFrameDTO> _remoteInputHandle;
        private VaultGenerationHandle<MockTickCommand> _tickCommandHandle;
        private VaultGenerationHandle<VisualStateDTO> _visualStateHandle;
        private VaultGenerationHandle<VisualStateHistoryDTO> _visualHistoryHandle;
        private VaultGenerationHandle<NetTelemetryEntry64> _telemetryHandle;
        private VaultGenerationHandle<RollbackTuningDTO> _tuningHandle;
        private VaultGenerationHandle<RollbackAudioSuppressionDTO> _audioSuppressionHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<RollbackLegacyProfileDTO> _latencyProfileHandle;
        private VaultGenerationHandle<InputStateDTO> _inputJournalHandle;
        private VaultGenerationHandle<PredictedInputDTO> _predictedInputHandle;
        private VaultGenerationHandle<PredictedInputAupTargetDTO> _predictedInputAupTargetHandle;
        private VaultGenerationHandle<InputPredictionTelemetryEntry> _inputPredictionTelemetryHandle;
        private VaultGenerationHandle<RollbackInputJournalSlot64> _rollbackInputJournalHandle;
        private VaultGenerationHandle<H8NetMerkleNodeRecord32> _merkleNodeHandle;
        private VaultGenerationHandle<H8NetMerkleNodeRecord32> _remoteMerkleNodeHandle;
        private VaultGenerationHandle<RollbackVaultBufferDescriptor32> _merkleDescriptorHandle;
        private VaultGenerationHandle<H8NetLeafDeltaRecord64> _leafDeltaHandle;
        private VaultGenerationHandle<MockNetworkJitterPacket64> _mockJitterPacketHandle;
        private VaultGenerationHandle<MockNetworkJitterState64> _mockJitterStateHandle;
        private VaultGenerationHandle<double3> _rigidbodyAupsLiveHandle;
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerStatesLiveHandle;
        private VaultGenerationHandle<RollbackAup48> _entityAupsLiveHandle;
        private VaultGenerationHandle<float3> _entityVelocitiesLiveHandle;
        private VaultGenerationHandle<float> _roomWaterLevelsLiveHandle;
        private VaultGenerationHandle<uint> _entityFlagsLiveHandle;
        private VaultGenerationHandle<uint> _entityItemHashesLiveHandle;
        private VaultGenerationHandle<ushort> _entityQuantitiesLiveHandle;
        private VaultGenerationHandle<uint> _inventoryHashesLiveHandle;
        private VaultGenerationHandle<int> _inventoryQuantitiesLiveHandle;
        private VaultGenerationHandle<float> _inventoryDurabilitiesLiveHandle;
        private VaultGenerationHandle<ulong> _questMasksLiveHandle;
        private VaultGenerationHandle<byte> _predatorChosenStatesLiveHandle;
        private int _snapshotStrideBytes;
        private int _registeredFixedDispatcher;
        private int _registeredLateFrame;
        private int _registeredHotSwapListener;
        private int _buffersReady;
        private uint _rollbackSignalsReady;
#if UNITY_EDITOR
        private uint _nextCsvPollFrame;
#endif
        private int _telemetryWriteIndex;
        private uint _frame;
        private uint _previousScheduledFrame;
        private uint _lastScheduledFrame;
        private int _hasScheduledFrame;
        private uint _lastDumpFrame = uint.MaxValue;
        private string _projectRoot;
        private string _legacyProfilePath;
#if UNITY_EDITOR
        private string _csvProfilePath;
#endif
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
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._tuningHandle, out NativeArray<RollbackTuningDTO>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length <= 0)
                return false;

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TrySetTuning(in RollbackTuningDTO tuning)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._tuningHandle, out NativeArray<RollbackTuningDTO> tuningBuffer) ||
                tuningBuffer.Length <= 0)
                return false;

            RollbackTuningDTO sanitized = SanitizeTuning(tuning);
            tuningBuffer[0] = sanitized;
            return true;
        }

        public static bool TryGetRuntimeState(out RollbackRuntimeStateDTO state)
        {
            state = default;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtimeInstance))
                return false;

            if (!runtimeInstance.TryReadOwned(in runtimeInstance._runtimeStateHandle, out NativeArray<RollbackRuntimeStateDTO>.ReadOnly runtime) ||
                runtime.Length <= 0)
                return false;

            state = runtime[0];
            return true;
        }

        public static bool TryGetVisualStates(out NativeArray<VisualStateDTO>.ReadOnly visualStates)
        {
            visualStates = default;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._visualStateHandle, out visualStates))
                return false;

            return visualStates.Length > 0;
        }

        public static bool TryGetVisualHistory(out NativeArray<VisualStateHistoryDTO>.ReadOnly visualHistory)
        {
            visualHistory = default;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._visualHistoryHandle, out visualHistory))
                return false;

            return visualHistory.Length > 0;
        }

        public static bool TryGetTelemetry(out NativeArray<NetTelemetryEntry64>.ReadOnly telemetry)
        {
            telemetry = default;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._telemetryHandle, out telemetry))
                return false;

            return telemetry.Length > 0;
        }

        public static bool TryGetInputPredictionTelemetry(out NativeArray<InputPredictionTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._inputPredictionTelemetryHandle, out telemetry))
                return false;

            return telemetry.Length > 0;
        }

        public static bool TryGetPredictedInputCapacity(out int capacity)
        {
            capacity = 0;
            if (!TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime))
                return false;

            if (!runtime.TryReadOwned(in runtime._predictedInputHandle, out NativeArray<PredictedInputDTO>.ReadOnly predictedInputs))
                return false;

            capacity = predictedInputs.Length;
            return capacity > 0;
        }

        private static bool TryGetReadyActiveInstance(out HectonRollbackNetcodeRuntime runtime)
        {
            runtime = _activeInstance;
            return runtime != null && runtime._buffersReady != 0 && runtime._vault != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveOwned<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _vault != null &&
                   handle.BufferID != 0u &&
                   _vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryReadOwned<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return _vault != null &&
                   handle.BufferID != 0u &&
                   _vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private NativeArray<T> ResolveOwned<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return TryResolveOwned(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        public static bool TrySetMockJitter(uint latencyFrames, uint packetLossPermille, uint duplicatePermille)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._tuningHandle, out NativeArray<RollbackTuningDTO> tuningBuffer) ||
                tuningBuffer.Length <= 0)
                return false;

            RollbackTuningDTO tuning = tuningBuffer[0];
            tuning.InputDelayFrames = math.min(latencyFrames, 30u);
            tuning.PingSimulatedFrames = tuning.InputDelayFrames;
            tuning.PacketLossPermille = math.min(packetLossPermille, 1000u);
            tuning.DuplicatePermille = math.min(duplicatePermille, 1000u);
            tuning.Flags |= RollbackNetcodeFlags.MockJitterActive;
            tuningBuffer[0] = SanitizeTuning(tuning);
            return true;
        }

        public static bool InjectRemoteInput(uint frame, in InputStateDTO input, uint flags = RemoteInputFlags.Received)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            PredictedInputDTO predicted = ToPredictedInput(frame, in input, PredictedInputFlags.Remote | PredictedInputFlags.Authoritative);
            return InjectRemotePredictedInput(frame, in predicted, flags);
        }

        public static bool InjectRemotePredictedInput(uint frame, in PredictedInputDTO input, uint flags = RemoteInputFlags.Received)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._remoteInputHandle, out NativeArray<RemoteInputFrameDTO> remote) ||
                remote.Length <= 0)
                return false;

            int index = (int)(frame % (uint)remote.Length);
            remote[index] = new RemoteInputFrameDTO
            {
                Input = input,
                Frame = frame,
                Flags = flags | RemoteInputFlags.Received | RemoteInputFlags.Valid
            };

            return true;
        }

        public static bool InjectRemoteFrameHash(uint frame, ulong frameHash64)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._runtimeStateHandle, out NativeArray<RollbackRuntimeStateDTO> runtime) ||
                runtime.Length <= 0)
                return false;

            RollbackRuntimeStateDTO state = runtime[0];
            state.LastRemoteFrame = frame;
            state.LastRemoteHash64 = frameHash64;
            state.LastRemoteBranchHash64 = 0UL;
            runtime[0] = state;

            if (_activeInstance.TryResolveOwned(in _activeInstance._remoteMerkleNodeHandle, out NativeArray<H8NetMerkleNodeRecord32> remoteNodes))
            {
                for (int i = 0; i < remoteNodes.Length; i++)
                    remoteNodes[i] = default;
            }
            return true;
        }

        public static bool InjectRemoteMerkleNode(uint frame, int nodeIndex, in H8NetMerkleNodeRecord32 node)
        {
            if (_activeInstance == null || !_activeInstance.TryEnsureBuffers())
                return false;

            if ((uint)nodeIndex >= RollbackNetcodeConstants.MerkleNodeCapacity)
                return false;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._remoteMerkleNodeHandle, out NativeArray<H8NetMerkleNodeRecord32> remoteNodes) ||
                remoteNodes.Length <= nodeIndex)
                return false;

            remoteNodes[nodeIndex] = node;
            if (nodeIndex != RollbackNetcodeConstants.MerkleRootNodeIndex)
                return true;

            if (!_activeInstance.TryResolveOwned(in _activeInstance._runtimeStateHandle, out NativeArray<RollbackRuntimeStateDTO> runtime) ||
                runtime.Length <= 0)
                return true;

            RollbackRuntimeStateDTO state = runtime[0];
            state.LastRemoteFrame = frame;
            state.LastRemoteHash64 = node.HashLo;
            state.LastRemoteBranchHash64 = node.HashHi;
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
            CacheDataVaultCold(GlobalRegistry.DataVault);
            TryEnsureBuffers();
            ApplyModeFlags(_modeFlags);
        }

        private void OnEnable()
        {
            _activeInstance = this;
            TryRegisterHotSwapListener();
            CacheDataVaultCold(GlobalRegistry.DataVault);
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

            TryUnregisterHotSwapListener();
            _rollbackSignalsReady = 0u;

            if (_activeInstance == this)
                _activeInstance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CacheDataVaultCold(currentService as IDataVault);
                if (isActiveAndEnabled)
                    TryEnsureBuffers();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredFixedDispatcher = 0;
                _registeredLateFrame = 0;
                if (currentService != null && isActiveAndEnabled)
                    TryRegisterDispatch();
            }
        }

        public uint GetFixedSystemIdHash()
        {
            return FixedSystemHash;
        }

        public DispatcherFenceDomain GetFenceDomain()
        {
            return DispatcherFenceDomain.Netcode;
        }

        public JobHandle ScheduleFixedSimulation(in DispatcherTimingDTO timing, JobHandle dependsOn)
        {
            if (!TryEnsureBuffers())
                return dependsOn;

            uint currentFrame = _frame++;
            uint previousFrame = _hasScheduledFrame != 0 ? _lastScheduledFrame : currentFrame;
            _previousScheduledFrame = previousFrame;
            _lastScheduledFrame = currentFrame;
            _hasScheduledFrame = 1;
            float quality = ResolveGlobalQualityWeight();
            NativeArray<RollbackTuningDTO> tuningBuffer = ResolveOwned(in _tuningHandle);
            NativeArray<RollbackRuntimeStateDTO> runtime = ResolveOwned(in _runtimeStateHandle);
            if (!tuningBuffer.IsCreated || !runtime.IsCreated)
                return dependsOn;

            RollbackTuningDTO tuning = tuningBuffer[0];
            tuning.GlobalQualityWeight = quality;
            tuningBuffer[0] = tuning;

            NativeArray<PredictedInputDTO> inputJournal = ResolveBoundBuffer(BufferID.ShinobuPredictedInputRing, ref _predictedInputHandle);
            NativeArray<PredictedInputAupTargetDTO> inputTargets = ResolveBoundBuffer(BufferID.ShinobuPredictedInputAupTargets, ref _predictedInputAupTargetHandle);
            NativeArray<RemoteInputFrameDTO> remoteInput = ResolveOwned(in _remoteInputHandle);
            NativeArray<byte> stateRing = ResolveOwned(in _stateRingHandle);
            NativeArray<FrameSnapshotDTO> snapshots = ResolveOwned(in _frameSnapshotHandle);
            NativeArray<MockTickCommand> commands = ResolveOwned(in _tickCommandHandle);
            NativeArray<RollbackAudioSuppressionDTO> audio = ResolveOwned(in _audioSuppressionHandle);
            NativeArray<VisualStateDTO> visualStates = ResolveOwned(in _visualStateHandle);
            NativeArray<VisualStateHistoryDTO> visualHistory = ResolveOwned(in _visualHistoryHandle);
            NativeArray<NetTelemetryEntry64> telemetry = ResolveOwned(in _telemetryHandle);
            NativeArray<InputPredictionTelemetryEntry> inputPredictionTelemetry = ResolveOwned(in _inputPredictionTelemetryHandle);
            NativeArray<RollbackInputJournalSlot64> rollbackInputJournal = ResolveOwned(in _rollbackInputJournalHandle);
            NativeArray<H8NetMerkleNodeRecord32> merkleNodes = ResolveOwned(in _merkleNodeHandle);
            NativeArray<H8NetMerkleNodeRecord32> remoteMerkleNodes = ResolveOwned(in _remoteMerkleNodeHandle);
            NativeArray<RollbackVaultBufferDescriptor32> merkleDescriptors = ResolveOwned(in _merkleDescriptorHandle);
            NativeArray<H8NetLeafDeltaRecord64> leafDeltaRecords = ResolveOwned(in _leafDeltaHandle);
            NativeArray<MockNetworkJitterPacket64> jitterPackets = ResolveOwned(in _mockJitterPacketHandle);
            NativeArray<MockNetworkJitterState64> jitterState = ResolveOwned(in _mockJitterStateHandle);
            NativeArray<double3> rigidbodyAups = ResolveBoundBuffer(BufferID.RigidbodyAUPs, ref _rigidbodyAupsLiveHandle);
            NativeArray<LockstepPlayerKinematicState> playerStates = ResolveBoundBuffer(BufferID.PlayerKinematicState, ref _playerStatesLiveHandle);
            NativeArray<RollbackAup48> entityAups = ResolveBoundBuffer(BufferID.EntityAUPs, ref _entityAupsLiveHandle);
            NativeArray<float3> entityVelocities = ResolveBoundBuffer(BufferID.EntityVelocities, ref _entityVelocitiesLiveHandle);
            NativeArray<float> roomWaterLevels = ResolveBoundBuffer(BufferID.RoomWaterLevels, ref _roomWaterLevelsLiveHandle);
            NativeArray<uint> entityFlags = ResolveBoundBuffer(BufferID.EntityFlags, ref _entityFlagsLiveHandle);
            NativeArray<uint> entityItemHashes = ResolveBoundBuffer(BufferID.EntityItemHashes, ref _entityItemHashesLiveHandle);
            NativeArray<ushort> entityQuantities = ResolveBoundBuffer(BufferID.EntityQuantities, ref _entityQuantitiesLiveHandle);
            NativeArray<uint> inventoryHashes = ResolveBoundBuffer(BufferID.ShinobuInventoryHashes, ref _inventoryHashesLiveHandle);
            NativeArray<int> inventoryQuantities = ResolveBoundBuffer(BufferID.ShinobuInventoryQuantities, ref _inventoryQuantitiesLiveHandle);
            NativeArray<float> inventoryDurabilities = ResolveBoundBuffer(BufferID.ShinobuInventoryDurabilities, ref _inventoryDurabilitiesLiveHandle);
            NativeArray<ulong> questMasks = ResolveBoundBuffer(BufferID.QuestDagGlobalStateMasks, ref _questMasksLiveHandle);
            NativeArray<byte> predatorChosenStates = ResolveBoundBuffer(BufferID.PredatorCognitionChosenStates, ref _predatorChosenStatesLiveHandle);

            int telemetryIndex = _telemetryWriteIndex;
            if (telemetry.IsCreated && telemetry.Length > 0)
            {
                int nextTelemetryIndex = telemetryIndex + 1;
                if (nextTelemetryIndex >= telemetry.Length)
                    nextTelemetryIndex = 0;
                _telemetryWriteIndex = nextTelemetryIndex;
            }

            GenerateMockNetworkJitterJob jitter = new GenerateMockNetworkJitterJob
            {
                PredictedJournal = inputJournal,
                RemoteInputRing = remoteInput,
                Packets = jitterPackets,
                JitterState = jitterState,
                CurrentFrame = currentFrame,
                DelayFrames = tuning.InputDelayFrames == 0u ? tuning.PingSimulatedFrames : tuning.InputDelayFrames,
                PacketLossPermille = tuning.PacketLossPermille,
                DuplicatePermille = tuning.DuplicatePermille,
                Seed = FixedSystemHash
            };
            JobHandle jitterHandle = jitter.Schedule(dependsOn);

            ComputeMerkleRootJob merkle = new ComputeMerkleRootJob
            {
                LeafDescriptors = merkleDescriptors,
                MerkleNodes = merkleNodes,
                RigidbodyAups = rigidbodyAups,
                PlayerStates = playerStates,
                EntityAups = entityAups,
                EntityVelocities = entityVelocities,
                RoomWaterLevels = roomWaterLevels,
                EntityFlags = entityFlags,
                EntityItemHashes = entityItemHashes,
                EntityQuantities = entityQuantities,
                InventoryHashes = inventoryHashes,
                InventoryQuantities = inventoryQuantities,
                InventoryDurabilities = inventoryDurabilities,
                QuestMasks = questMasks,
                PredatorChosenStates = predatorChosenStates,
                QualityLeafBudget = RollbackNetcodeMath.ResolveMerkleLeafBudget(in tuning, quality),
                Frame = currentFrame
            };
            JobHandle merkleLeaves = merkle.Schedule(RollbackNetcodeConstants.MerkleLeafCapacity, 1, jitterHandle);

            FinalizeMerkleRootJob merkleRoot = new FinalizeMerkleRootJob
            {
                MerkleNodes = merkleNodes,
                RuntimeState = runtime,
                Frame = currentFrame,
                QualityLeafBudget = RollbackNetcodeMath.ResolveMerkleLeafBudget(in tuning, quality)
            };
            JobHandle merkleHandle = merkleRoot.Schedule(merkleLeaves);

            uint rollbackSignalsEnabled = _rollbackSignalsReady;
            global::Hecton8.Core.MpscSignalRingBuffer<RollbackRequiredSignal>.ParallelWriter rollbackSignals = default;
            NativeArray<int> rollbackSignalsBudget = default;
            if (rollbackSignalsEnabled != 0u && SignalBus<RollbackRequiredSignal>.HasNativeStorage)
            {
                rollbackSignals = SignalBus<RollbackRequiredSignal>.OpenParallelWriter();
                rollbackSignalsBudget = SignalBus<RollbackRequiredSignal>.ParallelWriterBudget;
            }
            else
            {
                rollbackSignalsEnabled = 0u;
            }

            RollbackFixedPipelineJob pipeline = new RollbackFixedPipelineJob
            {
                Tuning = tuningBuffer,
                RuntimeState = runtime,
                PredictedJournal = inputJournal,
                RemoteInputRing = remoteInput,
                TargetAups = inputTargets,
                InputJournalRing = rollbackInputJournal,
                StateRingBuffer = stateRing,
                FrameSnapshots = snapshots,
                Commands = commands,
                AudioSuppression = audio,
                VisualStates = visualStates,
                VisualHistory = visualHistory,
                MerkleNodes = merkleNodes,
                RemoteMerkleNodes = remoteMerkleNodes,
                LeafDeltaRecords = leafDeltaRecords,
                MockJitterState = jitterState,
                Telemetry = telemetry,
                InputPredictionTelemetry = inputPredictionTelemetry,
                RigidbodyAups = rigidbodyAups,
                PlayerStates = playerStates,
                EntityAups = entityAups,
                EntityVelocities = entityVelocities,
                RoomWaterLevels = roomWaterLevels,
                EntityFlags = entityFlags,
                EntityItemHashes = entityItemHashes,
                EntityQuantities = entityQuantities,
                InventoryHashes = inventoryHashes,
                InventoryQuantities = inventoryQuantities,
                InventoryDurabilities = inventoryDurabilities,
                QuestMasks = questMasks,
                PredatorChosenStates = predatorChosenStates,
                CurrentFrame = currentFrame,
                PreviousFrame = previousFrame,
                ModeFlags = _modeFlags,
                RingFrameCapacity = RollbackNetcodeConstants.StateRingFrameCapacity,
                SnapshotStrideBytes = _snapshotStrideBytes,
                MaxRollbackFrames = RollbackNetcodeMath.ResolveBudgetedRollbackFrames(in tuning, quality),
                MaxRigidbodyAups = RollbackNetcodeConstants.MaxRigidbodyAups,
                MaxPlayerStates = RollbackNetcodeConstants.MaxPlayerStates,
                MaxEntityAups = RollbackNetcodeConstants.MaxEntityAups,
                MaxEntityVelocities = RollbackNetcodeConstants.MaxEntityVelocities,
                MaxRoomWaterLevels = RollbackNetcodeConstants.MaxRoomWaterLevels,
                MaxEntityFlags = RollbackNetcodeConstants.MaxEntityFlags,
                MaxEntityItems = RollbackNetcodeConstants.MaxEntityItems,
                MaxInventoryItems = RollbackNetcodeConstants.MaxInventoryItems,
                MaxQuestMasks = RollbackNetcodeConstants.MaxQuestMasks,
                MaxPredatorChosenStates = RollbackNetcodeConstants.MaxPredatorChosenStates,
                GlobalQualityWeight = quality,
                MoveEpsilon = MoveMismatchEpsilon,
                LookEpsilon = LookMismatchEpsilon,
                TelemetryWriteIndex = telemetryIndex,
                ModQuarantineMask = ResolveModQuarantineMask(),
                RollbackSignals = rollbackSignals,
                RollbackSignalsBudget = rollbackSignalsBudget,
                RollbackSignalsEnabled = rollbackSignalsEnabled
            };

            JobHandle handle = pipeline.Schedule(merkleHandle);
            H8Memory.RegisterActiveJob(RollbackNetcodeVault.OwnerSystem, handle);
            return handle;
        }

        public void PostFixedSimulation(in DispatcherTimingDTO timing)
        {
            if (!TryEnsureBuffers())
                return;

            NativeArray<RollbackRuntimeStateDTO> runtime = ResolveOwned(in _runtimeStateHandle);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return;

            RollbackRuntimeStateDTO state = runtime[0];
            if ((state.Flags & RollbackNetcodeFlags.HardResyncRequired) != 0u &&
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

            if ((state.Flags & (RollbackNetcodeFlags.ResimBudgetExceeded |
                                RollbackNetcodeFlags.InputPredictionSlow |
                                RollbackNetcodeFlags.InputPredictionNonFinite)) != 0u)
                DumpNetcodeBlackBox(state.CurrentFrame, state.Flags);
        }

        public void LateFrameTick()
        {
            if (!TryEnsureBuffers())
                return;

            NativeArray<VisualStateDTO> visualStates = ResolveOwned(in _visualStateHandle);
            NativeArray<VisualStateHistoryDTO> visualHistory = ResolveOwned(in _visualHistoryHandle);
            BlendVisualStates(visualStates, visualHistory, _frame, ResolveGlobalQualityWeight());

#if UNITY_EDITOR
            if (HasFrameReached(_frame, _nextCsvPollFrame))
            {
                _nextCsvPollFrame = _frame + CsvPollIntervalFrames;
                TryApplyCsvOverride();
            }
#endif
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_activeInstance != this || !TryEnsureBuffers())
                return;

            NativeArray<VisualStateDTO> states = ResolveOwned(in _visualStateHandle);
            if (!states.IsCreated)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                VisualStateDTO state = states[i];
                if ((state.Flags & 1u) == 0u)
                    continue;

                Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.9f);
                Gizmos.DrawWireSphere(ToVector3(state.TrueLocalMeters), 0.32f);
                Gizmos.color = new Color(0.05f, 1f, 0.32f, 0.9f);
                Gizmos.DrawWireSphere(ToVector3(state.InterpolatedLocalMeters), 0.24f);
            }

            NativeArray<PredictedInputDTO> predicted = ResolveBoundBuffer(BufferID.ShinobuPredictedInputRing, ref _predictedInputHandle);
            NativeArray<RemoteInputFrameDTO> remote = ResolveOwned(in _remoteInputHandle);
            if (!predicted.IsCreated || predicted.Length <= 0)
                return;

            uint start = _frame > 16u ? _frame - 16u : 0u;
            Vector3 predictedCursor = Vector3.zero;
            Vector3 remoteCursor = Vector3.zero;
            for (uint tick = start; tick < _frame; tick++)
            {
                PredictedInputDTO predictedInput = predicted[(int)(tick % (uint)predicted.Length)];
                if (predictedInput.TickNumber != tick)
                    continue;

                Vector3 nextPredicted = predictedCursor + ToVector3(predictedInput.LocalMoveVector) * 0.25f;
                Gizmos.color = new Color(0.05f, 1f, 0.32f, 0.9f);
                Gizmos.DrawLine(predictedCursor, nextPredicted);
                predictedCursor = nextPredicted;

                if (!remote.IsCreated || remote.Length <= 0)
                    continue;

                RemoteInputFrameDTO remoteInput = remote[(int)(tick % (uint)remote.Length)];
                if (remoteInput.Frame != tick)
                    continue;

                Vector3 nextRemote = remoteCursor + ToVector3(remoteInput.Input.LocalMoveVector) * 0.25f;
                Gizmos.color = new Color(0.12f, 0.35f, 1f, 0.85f);
                Gizmos.DrawLine(remoteCursor, nextRemote);
                if (remoteInput.Input.ActionButtonsMask != predictedInput.ActionButtonsMask)
                {
                    Gizmos.color = new Color(1f, 0.05f, 0.02f, 0.95f);
                    Gizmos.DrawWireSphere(nextPredicted, 0.16f);
                }
                remoteCursor = nextRemote;
            }
        }
#endif

        private bool ApplyModeFlags(uint flags)
        {
            if (!TryEnsureBuffers())
                return false;

            NativeArray<RollbackRuntimeStateDTO> runtime = ResolveOwned(in _runtimeStateHandle);
            if (!runtime.IsCreated || runtime.Length <= 0)
                return false;

            RollbackRuntimeStateDTO state = runtime[0];
            state.Flags = (state.Flags & ~(RollbackNetcodeFlags.Active | RollbackNetcodeFlags.ServerMode | RollbackNetcodeFlags.ClientMode)) | flags;
            runtime[0] = state;
            return true;
        }

        private void CacheDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            _vault = vault;
            ClearBufferHandles();
        }

        private void ClearBufferHandles()
        {
            _stateRingHandle = default;
            _frameSnapshotHandle = default;
            _runtimeStateHandle = default;
            _remoteInputHandle = default;
            _tickCommandHandle = default;
            _visualStateHandle = default;
            _visualHistoryHandle = default;
            _telemetryHandle = default;
            _tuningHandle = default;
            _audioSuppressionHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
            _latencyProfileHandle = default;
            _inputJournalHandle = default;
            _predictedInputHandle = default;
            _predictedInputAupTargetHandle = default;
            _inputPredictionTelemetryHandle = default;
            _rollbackInputJournalHandle = default;
            _merkleNodeHandle = default;
            _remoteMerkleNodeHandle = default;
            _merkleDescriptorHandle = default;
            _leafDeltaHandle = default;
            _mockJitterPacketHandle = default;
            _mockJitterStateHandle = default;
            _rigidbodyAupsLiveHandle = default;
            _playerStatesLiveHandle = default;
            _entityAupsLiveHandle = default;
            _entityVelocitiesLiveHandle = default;
            _roomWaterLevelsLiveHandle = default;
            _entityFlagsLiveHandle = default;
            _entityItemHashesLiveHandle = default;
            _entityQuantitiesLiveHandle = default;
            _inventoryHashesLiveHandle = default;
            _inventoryQuantitiesLiveHandle = default;
            _inventoryDurabilitiesLiveHandle = default;
            _questMasksLiveHandle = default;
            _predatorChosenStatesLiveHandle = default;
            _snapshotStrideBytes = 0;
            _rollbackSignalsReady = 0u;
            _buffersReady = 0;
        }

        private bool TryEnsureBuffers()
        {
            if (_buffersReady != 0 && _vault != null)
            {
                TryBindInputTruthHandles();
                TryBindBorrowedSnapshotHandles();
                return TryCacheRollbackSignalWriterCold();
            }

            if (_vault == null)
                return false;

            if (RollbackNetcodeLayoutGuard.Validate() != 0u)
                return false;

            _snapshotStrideBytes = RollbackNetcodeConstants.ResolveSnapshotStrideBytes();
            int stateRingBytes = _snapshotStrideBytes * RollbackNetcodeConstants.StateRingFrameCapacity;
            _stateRingHandle = _vault.EnsureGenerationHandle<byte>(RollbackNetcodeVault.StateRingBuffer, stateRingBytes, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _frameSnapshotHandle = _vault.EnsureGenerationHandle<FrameSnapshotDTO>(RollbackNetcodeVault.FrameSnapshots, RollbackNetcodeConstants.StateRingFrameCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _runtimeStateHandle = _vault.EnsureGenerationHandle<RollbackRuntimeStateDTO>(RollbackNetcodeVault.RuntimeState, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _remoteInputHandle = _vault.EnsureGenerationHandle<RemoteInputFrameDTO>(RollbackNetcodeVault.RemoteInputRing, RollbackNetcodeConstants.InputRingCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _tickCommandHandle = _vault.EnsureGenerationHandle<MockTickCommand>(RollbackNetcodeVault.TickCommands, RollbackNetcodeConstants.CommandCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _visualStateHandle = _vault.EnsureGenerationHandle<VisualStateDTO>(RollbackNetcodeVault.VisualStates, RollbackNetcodeConstants.VisualStateCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _visualHistoryHandle = _vault.EnsureGenerationHandle<VisualStateHistoryDTO>(RollbackNetcodeVault.VisualHistory, RollbackNetcodeConstants.VisualHistoryCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _vault.EnsureGenerationHandle<NetTelemetryEntry64>(RollbackNetcodeVault.TelemetryRing, RollbackNetcodeConstants.TelemetryFrameCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _inputPredictionTelemetryHandle = _vault.EnsureGenerationHandle<InputPredictionTelemetryEntry>(RollbackNetcodeVault.InputPredictionTelemetry, RollbackNetcodeConstants.TelemetryFrameCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.EnsureGenerationHandle<RollbackTuningDTO>(RollbackNetcodeVault.Tuning, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _audioSuppressionHandle = _vault.EnsureGenerationHandle<RollbackAudioSuppressionDTO>(RollbackNetcodeVault.AudioSuppression, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            _csvScratchHandle = _vault.EnsureGenerationHandle<byte>(RollbackNetcodeVault.CsvScratch, RollbackNetcodeConstants.CsvScratchBytes, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
#endif
            _latencyProfileHandle = _vault.EnsureGenerationHandle<RollbackLegacyProfileDTO>(RollbackNetcodeVault.LatencyProfile, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _merkleNodeHandle = _vault.EnsureGenerationHandle<H8NetMerkleNodeRecord32>(RollbackNetcodeVault.MerkleNodes, RollbackNetcodeConstants.MerkleNodeCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _remoteMerkleNodeHandle = _vault.EnsureGenerationHandle<H8NetMerkleNodeRecord32>(RollbackNetcodeVault.RemoteMerkleNodes, RollbackNetcodeConstants.MerkleNodeCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _merkleDescriptorHandle = _vault.EnsureGenerationHandle<RollbackVaultBufferDescriptor32>(RollbackNetcodeVault.MerkleLeafDescriptors, RollbackNetcodeConstants.MerkleLeafCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);
            _leafDeltaHandle = _vault.EnsureGenerationHandle<H8NetLeafDeltaRecord64>(RollbackNetcodeVault.LeafDeltaRecords, RollbackNetcodeConstants.LeafDeltaCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _rollbackInputJournalHandle = _vault.EnsureGenerationHandle<RollbackInputJournalSlot64>(RollbackNetcodeVault.InputJournalRing, RollbackNetcodeConstants.InputRingCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _mockJitterPacketHandle = _vault.EnsureGenerationHandle<MockNetworkJitterPacket64>(RollbackNetcodeVault.MockJitterPackets, RollbackNetcodeConstants.MockJitterPacketCapacity, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _mockJitterStateHandle = _vault.EnsureGenerationHandle<MockNetworkJitterState64>(RollbackNetcodeVault.MockJitterState, 1, RollbackNetcodeVault.OwnerSystem, NativeArrayOptions.ClearMemory);

            TryBindInputTruthHandles();
            TryBindBorrowedSnapshotHandles();

            if (!TryCacheRollbackSignalWriterCold())
                return false;

            InitializeAuthoritativeMerkleDescriptors();
            EnsureDefaultTuning();
            if (!TryLoadLegacyLatencyProfile())
                GenerateEmergencyMockNetcode();

            _buffersReady = 1;
            TryRegisterDispatch();
            return true;
        }

        private bool TryCacheRollbackSignalWriterCold()
        {
            if (_rollbackSignalsReady != 0u)
                return true;

            if (RollbackNetcodeLayoutGuard.Validate() != 0u)
                return false;

            SignalBus<RollbackRequiredSignal>.Configure(32, maxFrameSignals: 64, lowTierFrameSignals: 8, laneHash: 0x52425153u);
            SignalBus<RollbackRequiredSignal>.EnsureInitialized();
            if (!SignalBus<RollbackRequiredSignal>.HasNativeStorage)
                return false;

            _rollbackSignalsReady = 1u;
            return true;
        }

        private void TryBindInputTruthHandles()
        {
            TryBindExistingIfMissing(BufferID.ShinobuInputJournalRing, ref _inputJournalHandle);
            TryBindExistingIfMissing(BufferID.ShinobuPredictedInputRing, ref _predictedInputHandle);
            TryBindExistingIfMissing(BufferID.ShinobuPredictedInputAupTargets, ref _predictedInputAupTargetHandle);
        }

        private void InitializeAuthoritativeMerkleDescriptors()
        {
            NativeArray<RollbackVaultBufferDescriptor32> descriptors = ResolveOwned(in _merkleDescriptorHandle);
            if (!descriptors.IsCreated || descriptors.Length < RollbackNetcodeConstants.MerkleLeafCapacity)
                return;

            WriteMerkleDescriptor(descriptors, 0, BufferID.RigidbodyAUPs, UnsafeUtility.SizeOf<double3>(), RollbackNetcodeConstants.MaxRigidbodyAups, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.AupExactDouble3);
            WriteMerkleDescriptor(descriptors, 1, BufferID.PlayerKinematicState, UnsafeUtility.SizeOf<LockstepPlayerKinematicState>(), RollbackNetcodeConstants.MaxPlayerStates, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 2, BufferID.EntityAUPs, UnsafeUtility.SizeOf<double3>(), RollbackNetcodeConstants.MaxEntityAups, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.AupExactDouble3);
            WriteMerkleDescriptor(descriptors, 3, BufferID.EntityVelocities, UnsafeUtility.SizeOf<float3>(), RollbackNetcodeConstants.MaxEntityVelocities, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 4, BufferID.RoomWaterLevels, UnsafeUtility.SizeOf<float>(), RollbackNetcodeConstants.MaxRoomWaterLevels, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 5, BufferID.EntityFlags, UnsafeUtility.SizeOf<uint>(), RollbackNetcodeConstants.MaxEntityFlags, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 6, BufferID.EntityItemHashes, UnsafeUtility.SizeOf<uint>(), RollbackNetcodeConstants.MaxEntityItems, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 7, BufferID.EntityQuantities, UnsafeUtility.SizeOf<ushort>(), RollbackNetcodeConstants.MaxEntityItems, RollbackMerkleFlags.Authoritative);
            WriteMerkleDescriptor(descriptors, 8, BufferID.ShinobuInventoryHashes, UnsafeUtility.SizeOf<uint>(), RollbackNetcodeConstants.MaxInventoryItems, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.OptionalQualityLeaf);
            WriteMerkleDescriptor(descriptors, 9, BufferID.ShinobuInventoryQuantities, UnsafeUtility.SizeOf<int>(), RollbackNetcodeConstants.MaxInventoryItems, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.OptionalQualityLeaf);
            WriteMerkleDescriptor(descriptors, 10, BufferID.ShinobuInventoryDurabilities, UnsafeUtility.SizeOf<float>(), RollbackNetcodeConstants.MaxInventoryItems, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.OptionalQualityLeaf);
            WriteMerkleDescriptor(descriptors, 11, BufferID.QuestDagGlobalStateMasks, UnsafeUtility.SizeOf<ulong>(), RollbackNetcodeConstants.MaxQuestMasks, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.OptionalQualityLeaf);
            WriteMerkleDescriptor(descriptors, 12, BufferID.PredatorCognitionChosenStates, UnsafeUtility.SizeOf<byte>(), RollbackNetcodeConstants.MaxPredatorChosenStates, RollbackMerkleFlags.Authoritative | RollbackMerkleFlags.OptionalQualityLeaf);

            for (int i = 13; i < descriptors.Length; i++)
            {
                RollbackVaultBufferDescriptor32 descriptor = default;
                descriptor.LeafIndex = (uint)i;
                descriptor.Flags = RollbackMerkleFlags.PresentationExcluded | RollbackMerkleFlags.SkippedByQuality;
                descriptors[i] = descriptor;
            }
        }

        private static void WriteMerkleDescriptor(
            NativeArray<RollbackVaultBufferDescriptor32> descriptors,
            int index,
            BufferID bufferId,
            int elementStride,
            int elementCount,
            uint flags)
        {
            if ((uint)index >= (uint)descriptors.Length)
                return;

            RollbackVaultBufferDescriptor32 descriptor = default;
            descriptor.BufferId = (uint)bufferId;
            descriptor.ByteOffset = 0u;
            descriptor.ElementStride = (uint)math.max(1, elementStride);
            descriptor.ElementCount = (uint)math.max(0, elementCount);
            descriptor.ByteLength = descriptor.ElementStride * descriptor.ElementCount;
            descriptor.Flags = flags;
            descriptor.LeafIndex = (uint)index;
            descriptor.Generation = 1u;
            descriptors[index] = descriptor;
        }

        private void EnsureDefaultTuning()
        {
            NativeArray<RollbackTuningDTO> tuning = ResolveOwned(in _tuningHandle);
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
            defaults.HashCadenceFrames = RollbackNetcodeConstants.DesyncHashCadenceFrames;
            defaults.MaxMerkleLeaves = RollbackNetcodeConstants.MerkleLeafCapacity;
            defaults.RedundancyCount = 1u;
            defaults.ExtrapolationDecayPermille = RollbackNetcodeConstants.DefaultExtrapolationDecayPermille;
            defaults.PredictionWindowTicks = 30u;
            defaults.InputDelayFrames = 0u;
            defaults.PacketLossPermille = 0u;
            defaults.DuplicatePermille = 0u;
            tuning[0] = defaults;
        }

        private bool TryLoadLegacyLatencyProfile()
        {
            if (string.IsNullOrEmpty(_legacyProfilePath) || !File.Exists(_legacyProfilePath))
                return false;

            NativeArray<RollbackLegacyProfileDTO> profileBuffer = ResolveOwned(in _latencyProfileHandle);
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
            NativeArray<RollbackTuningDTO> tuningBuffer = ResolveOwned(in _tuningHandle);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                RollbackTuningDTO tuning = tuningBuffer[0];
                tuning.MaxRollbackFrames = (int)math.clamp(profile.MaxRollbackFrames, 1u, (uint)RollbackNetcodeConstants.MaxRollbackFrames);
                tuning.InputPredictionAggressiveness = math.saturate(profile.PredictionAggressiveness);
                tuning.InputDelayFrames = (uint)math.clamp((int)math.round(profile.SimulatedPingMs / 16.6667f), 0, 30);
                tuning.PingSimulatedFrames = tuning.InputDelayFrames;
                tuning.PacketLossPermille = (uint)math.clamp((int)math.round(math.saturate(profile.PacketLoss01) * 1000f), 0, 1000);
                tuning.DuplicatePermille = 0u;
                tuningBuffer[0] = SanitizeTuning(tuning);
            }

            return true;
        }

        private void GenerateEmergencyMockNetcode()
        {
            NativeArray<RollbackRuntimeStateDTO> runtime = ResolveOwned(in _runtimeStateHandle);
            if (runtime.IsCreated && runtime.Length > 0)
            {
                RollbackRuntimeStateDTO state = runtime[0];
                state.Flags |= RollbackNetcodeFlags.EmergencyMock | RollbackNetcodeFlags.MockJitterActive;
                runtime[0] = state;
            }

            NativeArray<RollbackTuningDTO> tuningBuffer = ResolveOwned(in _tuningHandle);
            if (tuningBuffer.IsCreated && tuningBuffer.Length > 0)
            {
                RollbackTuningDTO tuning = tuningBuffer[0];
                tuning.Flags |= RollbackNetcodeFlags.EmergencyMock | RollbackNetcodeFlags.MockJitterActive;
                tuning.InputDelayFrames = SimulatedPingFrames200Ms;
                tuning.PingSimulatedFrames = SimulatedPingFrames200Ms;
                tuning.PacketLossPermille = 50u;
                tuning.DuplicatePermille = 20u;
                tuning.RedundancyCount = math.max(1u, tuning.RedundancyCount);
                tuning.ExtrapolationDecayPermille = RollbackNetcodeConstants.DefaultExtrapolationDecayPermille;
                tuning.PredictionWindowTicks = RollbackNetcodeMath.ResolvePredictionWindowTicks(in tuning, ResolveGlobalQualityWeight(), tuning.PingSimulatedFrames);
                tuningBuffer[0] = SanitizeTuning(tuning);
            }

            NativeArray<MockNetworkJitterState64> jitter = ResolveOwned(in _mockJitterStateHandle);
            if (jitter.IsCreated && jitter.Length > 0)
            {
                MockNetworkJitterState64 state = jitter[0];
                state.PacketLossPermille = 50u;
                state.DuplicatePermille = 20u;
                state.DelayFrames = SimulatedPingFrames200Ms;
                state.Flags = RollbackNetcodeFlags.MockJitterActive;
                state.RngState = FixedSystemHash;
                jitter[0] = state;
            }

            NativeArray<RemoteInputFrameDTO> remote = ResolveOwned(in _remoteInputHandle);
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
            TrySetMockJitter((uint)math.max(0, delayedFrames), 50u, 20u);
            NativeArray<PredictedInputDTO> journal = ResolveBoundBuffer(BufferID.ShinobuPredictedInputRing, ref _predictedInputHandle);
            if (!journal.IsCreated || journal.Length <= 0)
                return false;

            uint age = (uint)math.max(0, delayedFrames);
            uint previousFrame = _hasScheduledFrame != 0 ? _previousScheduledFrame : _frame;
            if (!RollbackNetcodeMath.TryResolveHistoricalFrame(_frame, previousFrame, age, out uint frame))
                frame = 0u;

            PredictedInputDTO input = journal[(int)(frame % (uint)journal.Length)];
            input.ActionButtonsMask ^= 1u;
            return InjectRemotePredictedInput(frame, in input, RemoteInputFlags.Received);
        }

        private static void BlendVisualStates(NativeArray<VisualStateDTO> visualStates, NativeArray<VisualStateHistoryDTO> visualHistory, uint frame, float quality)
        {
            if (!visualStates.IsCreated)
                return;

            VisualStateInterpolatorJob job = new VisualStateInterpolatorJob
            {
                VisualStates = visualStates,
                VisualHistory = visualHistory,
                CurrentFrame = frame,
                GlobalQualityWeight = quality
            };
            job.Execute();
        }

        private void TryBindBorrowedSnapshotHandles()
        {
            TryBindExistingIfMissing(BufferID.RigidbodyAUPs, ref _rigidbodyAupsLiveHandle);
            TryBindExistingIfMissing(BufferID.PlayerKinematicState, ref _playerStatesLiveHandle);
            TryBindExistingIfMissing(BufferID.EntityAUPs, ref _entityAupsLiveHandle);
            TryBindExistingIfMissing(BufferID.EntityVelocities, ref _entityVelocitiesLiveHandle);
            TryBindExistingIfMissing(BufferID.RoomWaterLevels, ref _roomWaterLevelsLiveHandle);
            TryBindExistingIfMissing(BufferID.EntityFlags, ref _entityFlagsLiveHandle);
            TryBindExistingIfMissing(BufferID.EntityItemHashes, ref _entityItemHashesLiveHandle);
            TryBindExistingIfMissing(BufferID.EntityQuantities, ref _entityQuantitiesLiveHandle);
            TryBindExistingIfMissing(BufferID.ShinobuInventoryHashes, ref _inventoryHashesLiveHandle);
            TryBindExistingIfMissing(BufferID.ShinobuInventoryQuantities, ref _inventoryQuantitiesLiveHandle);
            TryBindExistingIfMissing(BufferID.ShinobuInventoryDurabilities, ref _inventoryDurabilitiesLiveHandle);
            TryBindExistingIfMissing(BufferID.QuestDagGlobalStateMasks, ref _questMasksLiveHandle);
            TryBindExistingIfMissing(BufferID.PredatorCognitionChosenStates, ref _predatorChosenStatesLiveHandle);
        }

        private bool TryBindExistingIfMissing<T>(BufferID bufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u)
                return true;

            return _vault != null && _vault.TryGetGenerationHandle<T>(bufferId, out handle);
        }

        private NativeArray<T> ResolveBoundBuffer<T>(BufferID bufferId, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (_vault == null)
                return default;

            uint expectedBufferId = unchecked((uint)bufferId);
            if (handle.BufferID != 0u && handle.BufferID != expectedBufferId)
            {
                handle = default;
                return default;
            }

            if (handle.BufferID == 0u && !_vault.TryGetGenerationHandle<T>(bufferId, out handle))
                return default;

            if (_vault.TryResolveHandle(in handle, out NativeArray<T> buffer) && buffer.IsCreated)
                return buffer;

            if (!_vault.TryGetGenerationHandle<T>(bufferId, out handle))
            {
                handle = default;
                return default;
            }

            return _vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated ? buffer : default;
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

        private static PredictedInputDTO ToPredictedInput(uint frame, in InputStateDTO input, uint flags)
        {
            PredictedInputDTO predicted = default;
            predicted.TickNumber = frame;
            predicted.LocalMoveVector = new float3(input.MoveAxis.x, 0f, input.MoveAxis.y);
            predicted.LookDelta = input.LookDelta;
            predicted.ActionButtonsMask = input.ButtonMask;
            predicted._pad0 = flags | PredictedInputFlags.Valid;
            if (!math.all(math.isfinite(predicted.LocalMoveVector)) || !math.all(math.isfinite(predicted.LookDelta)))
            {
                predicted.LocalMoveVector = float3.zero;
                predicted.LookDelta = float2.zero;
                predicted._pad0 |= PredictedInputFlags.NonFiniteSanitized;
            }

            return predicted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFrameReached(uint currentFrame, uint targetFrame)
        {
            return RollbackNetcodeMath.HasFrameReached(currentFrame, targetFrame);
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
            tuning.PacketLossPermille = math.min(tuning.PacketLossPermille, 1000u);
            tuning.DuplicatePermille = math.min(tuning.DuplicatePermille, 1000u);
            tuning.RedundancyCount = math.min(math.max(1u, tuning.RedundancyCount), 5u);
            tuning.HashCadenceFrames = tuning.HashCadenceFrames == 0u
                ? RollbackNetcodeConstants.DesyncHashCadenceFrames
                : math.clamp(tuning.HashCadenceFrames, 15u, 180u);
            tuning.MaxMerkleLeaves = tuning.MaxMerkleLeaves == 0u
                ? RollbackNetcodeConstants.MerkleLeafCapacity
                : math.clamp(tuning.MaxMerkleLeaves, 1u, (uint)RollbackNetcodeConstants.MerkleLeafCapacity);
            tuning.InputDelayFrames = math.min(tuning.InputDelayFrames, 30u);
            tuning.PingSimulatedFrames = math.min(tuning.PingSimulatedFrames, 30u);
            tuning.ExtrapolationDecayPermille = tuning.ExtrapolationDecayPermille == 0u
                ? RollbackNetcodeConstants.DefaultExtrapolationDecayPermille
                : math.min(tuning.ExtrapolationDecayPermille, 2000u);
            tuning.PredictionWindowTicks = tuning.PredictionWindowTicks == 0u
                ? RollbackNetcodeMath.ResolvePredictionWindowTicks(in tuning, tuning.GlobalQualityWeight, tuning.PingSimulatedFrames)
                : math.clamp(tuning.PredictionWindowTicks, 5u, 30u);
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

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener != 0)
                return;

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwapListener = 1;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwapListener == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = 0;
        }

#if UNITY_EDITOR
        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }
#endif

        private void ResolveColdPaths()
        {
            _projectRoot = ResolveProjectRoot();
            _legacyProfilePath = Path.Combine(_projectRoot, LegacyProfileRelativePath);
#if UNITY_EDITOR
            _csvProfilePath = Path.Combine(_projectRoot, CsvProfileRelativePath);
#endif
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

#if UNITY_EDITOR
        private void TryApplyCsvOverride()
        {
            if (string.IsNullOrEmpty(_csvProfilePath) || !File.Exists(_csvProfilePath))
                return;

            NativeArray<byte> scratch = ResolveOwned(in _csvScratchHandle);
            NativeArray<RollbackTuningDTO> tuningBuffer = ResolveOwned(in _tuningHandle);
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
            uint activeProfileHash = 0u;
            uint previousTextHash = 0u;
            uint lastTextHash = 0u;
            uint tokenHash = 2166136261u;
            int value = 0;
            int decimalDivisor = 0;
            bool negative = false;
            bool hasText = false;
            bool hasNumeric = false;
            bool hasDigits = false;

            for (int i = 0; i <= byteCount; i++)
            {
                byte c = i < byteCount ? bytes[i] : (byte)'\n';
                if (c == ',' || c == '=' || c == ';' || c == '\n')
                {
                    if (c == '\n')
                        FlushCsvToken(ref activeProfileHash, ref previousTextHash, ref lastTextHash, ref tokenHash, ref value, ref decimalDivisor, ref negative, ref hasText, ref hasNumeric, ref hasDigits, ref tuning, true);
                    else
                        FlushCsvToken(ref activeProfileHash, ref previousTextHash, ref lastTextHash, ref tokenHash, ref value, ref decimalDivisor, ref negative, ref hasText, ref hasNumeric, ref hasDigits, ref tuning, false);
                    continue;
                }

                if (c == '\r' || c <= 32)
                    continue;

                if (c >= '0' && c <= '9')
                {
                    if (hasText)
                    {
                        tokenHash = (tokenHash ^ c) * 16777619u;
                        continue;
                    }

                    hasNumeric = true;
                    hasDigits = true;
                    value = (value * 10) + (c - '0');
                    if (decimalDivisor > 0)
                        decimalDivisor *= 10;
                    continue;
                }

                if (c == '-' && !hasText && !hasNumeric)
                {
                    negative = true;
                    hasNumeric = true;
                    continue;
                }

                if (c == '.' && !hasText)
                {
                    hasNumeric = true;
                    if (decimalDivisor == 0)
                        decimalDivisor = 1;
                    continue;
                }

                hasText = true;
                tokenHash = (tokenHash ^ ToLowerAscii(c)) * 16777619u;
            }
        }

        private static void FlushCsvToken(
            ref uint activeProfileHash,
            ref uint previousTextHash,
            ref uint lastTextHash,
            ref uint tokenHash,
            ref int value,
            ref int decimalDivisor,
            ref bool negative,
            ref bool hasText,
            ref bool hasNumeric,
            ref bool hasDigits,
            ref RollbackTuningDTO tuning,
            bool endOfLine)
        {
            if (hasText)
            {
                previousTextHash = lastTextHash;
                lastTextHash = tokenHash;
            }
            else if (hasNumeric && hasDigits && lastTextHash != 0u &&
                     ProfileMatches(previousTextHash, activeProfileHash))
            {
                ApplyCsvValue(lastTextHash, negative ? -value : value, decimalDivisor, ref tuning);
            }

            tokenHash = 2166136261u;
            value = 0;
            decimalDivisor = 0;
            negative = false;
            hasText = false;
            hasNumeric = false;
            hasDigits = false;
            if (endOfLine)
            {
                if (previousTextHash == CsvHashActiveProfile && lastTextHash != 0u)
                    activeProfileHash = lastTextHash;

                previousTextHash = 0u;
                lastTextHash = 0u;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ProfileMatches(uint profileHash, uint activeProfileHash)
        {
            return profileHash == 0u ||
                   profileHash == CsvHashDefaultProfile ||
                   profileHash == CsvHashGlobalProfile ||
                   profileHash == CsvHashGenericProfile ||
                   (activeProfileHash != 0u && profileHash == activeProfileHash);
        }

        private static void ApplyCsvValue(uint keyHash, int rawValue, int decimalDivisor, ref RollbackTuningDTO tuning)
        {
            float value = decimalDivisor > 1 ? rawValue / (float)decimalDivisor : rawValue;
            if (keyHash == CsvHashMaxRollbackFrames || keyHash == CsvHashMaxRollbackDepth)
                tuning.MaxRollbackFrames = (int)value;
            else if (keyHash == CsvHashVisualInterpolationFrames)
                tuning.VisualInterpolationFrames = (int)value;
            else if (keyHash == CsvHashVisualInterpolationSeconds)
                tuning.VisualInterpolationSeconds = value;
            else if (keyHash == CsvHashInputPredictionAggressiveness)
                tuning.InputPredictionAggressiveness = value;
            else if (keyHash == CsvHashMinQualityForLookRollback)
                tuning.MinQualityForLookRollback = value;
            else if (keyHash == CsvHashInputDelayTicks || keyHash == CsvHashInputDelayFrames)
                tuning.InputDelayFrames = (uint)math.max(0, (int)value);
            else if (keyHash == CsvHashRedundancyCount)
                tuning.RedundancyCount = (uint)math.max(1, (int)value);
            else if (keyHash == CsvHashPacketLossPermille)
                tuning.PacketLossPermille = (uint)math.max(0, (int)value);
            else if (keyHash == CsvHashDuplicatePermille)
                tuning.DuplicatePermille = (uint)math.max(0, (int)value);
            else if (keyHash == CsvHashCadenceFrames)
                tuning.HashCadenceFrames = (uint)math.max(1, (int)value);
            else if (keyHash == CsvHashMaxMerkleLeaves)
                tuning.MaxMerkleLeaves = (uint)math.max(1, (int)value);
            else if (keyHash == CsvHashExtrapolationDecay)
                tuning.ExtrapolationDecayPermille = (uint)math.clamp((int)math.round(math.max(0f, value) * 1000f), 1, 2000);
            else if (keyHash == CsvHashExtrapolationDecayPermille)
                tuning.ExtrapolationDecayPermille = (uint)math.clamp((int)value, 1, 2000);
            else if (keyHash == CsvHashPredictionWindow ||
                     keyHash == CsvHashPredictionWindowTicks ||
                     keyHash == CsvHashBufferCapacity ||
                     keyHash == CsvHashBufferSize)
                tuning.PredictionWindowTicks = (uint)math.clamp((int)value, 5, 30);
            else if (keyHash == CsvHashLatencyThresholdFrames || keyHash == CsvHashLatencyFrames)
                tuning.InputDelayFrames = (uint)math.max(0, (int)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLowerAscii(byte value)
        {
            return value >= 'A' && value <= 'Z' ? (byte)(value + 32) : value;
        }
#endif

        private void PublishPauseSignal(uint currentFrame)
        {
            SystemPauseSignal signal = default;
            signal.SourceHash = PauseSourceHash;
            signal.Frame = currentFrame;
            signal.Sequence = ++_pauseSequence;
            signal.Paused = 1;
            signal.Flags = 1;
            signal.RestoreScalar = 1f;
            SignalBus<SystemPauseSignal>.TryPushTracked(in signal, ref s_x001HectonRollbackNetcodeRuntimeSignalPushDropCount);
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

                NativeArray<NetTelemetryEntry64> telemetry = ResolveOwned(in _telemetryHandle);
                using FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                RollbackBlackBoxDumpHeader32 header = default;
                header.Magic = RollbackNetcodeConstants.BlackBoxDumpMagic;
                header.SourceHash = PauseSourceHash;
                header.CurrentFrame = currentFrame;
                header.Flags = flags;
                header.EntryCount = telemetry.IsCreated ? (uint)telemetry.Length : 0u;
                header.EntrySizeBytes = (uint)UnsafeUtility.SizeOf<NetTelemetryEntry64>();
                header.Version = RollbackNetcodeConstants.BlackBoxDumpVersion;

                byte* headerPtr = (byte*)UnsafeUtility.AddressOf(ref header);
                stream.Write(new ReadOnlySpan<byte>(headerPtr, UnsafeUtility.SizeOf<RollbackBlackBoxDumpHeader32>()));
                if (!telemetry.IsCreated)
                    return;

                void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int telemetryBytes = telemetry.Length * UnsafeUtility.SizeOf<NetTelemetryEntry64>();
                stream.Write(new ReadOnlySpan<byte>(telemetryPtr, telemetryBytes));
                NativeArray<InputPredictionTelemetryEntry> inputTelemetry = ResolveOwned(in _inputPredictionTelemetryHandle);
                if (inputTelemetry.IsCreated)
                {
                    void* inputPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputTelemetry);
                    int inputBytes = inputTelemetry.Length * UnsafeUtility.SizeOf<InputPredictionTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(inputPtr, inputBytes));
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
