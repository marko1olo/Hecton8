using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Physics;
using Hecton8.Physics.Determinism;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts.Signals
{
    /// <summary>
    /// Master state hash fence published by the lockstep validator after a completed deterministic sample.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct LockstepSnapshotSignal : ISignal
    {
        public ulong MasterHash;
        public uint Frame;
        public uint HashCadenceFrames;
        public uint Flags;
        public uint MissingMask;
        public uint NonFiniteMask;
        public uint ReplayBlock;
    }

    /// <summary>
    /// Developer-facing glitch pulse emitted when deterministic replay validation fails.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    public struct SystemGlitchSignal : ISignal
    {
        public uint Frame;
        public uint SourceId;
        public uint LocalHash;
        public uint ExpectedHash;
        public float Intensity01;
        public float DurationSeconds;
        public byte Reason;
        public byte Flags;
        public ushort Reserved0;
        public uint Reserved1;
    }
}

namespace Hecton8.Core.Determinism
{
    public sealed class FatalDesyncException : Exception
    {
        public readonly uint Frame;
        public readonly ulong MasterHash;
        public readonly uint Flags;

        public FatalDesyncException(uint frame, ulong masterHash, uint flags)
            : base("Fatal deterministic state divergence.")
        {
            Frame = frame;
            MasterHash = masterHash;
            Flags = flags;
        }
    }

    /// <summary>
    /// Blittable player truth snapshot hashed by the lockstep validator.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 96)]
    public struct LockstepPlayerKinematicState
    {
        public long SectorX;
        public long SectorY;
        public long SectorZ;
        public float3 LocalPosition;
        public float3 Velocity;
        public float3 Forward;
        public uint Frame;
        public uint Flags;
        public uint InputActions;
        public uint StableId;
        public uint HashCadenceFrames;
        public uint Reserved1;
        public uint Reserved2;
        public uint Reserved3;
        public uint Reserved4;
    }

    /// <summary>
    /// Fixed-size replay input frame stored in `.h8replay` blocks.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
    public struct LockstepReplayInputFrame
    {
        public uint Frame;
        public uint ActionsBitmask;
        public float2 MoveDelta;
        public float2 LookDelta;
        public float VerticalDelta;
        public uint CurrentInputSchemeHash;
        public uint Flags;
        public uint Sequence;
        public uint Reserved0;
        public uint Reserved1;
    }

    /// <summary>
    /// Fixed-size replay block header followed by 300 input frames.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 128)]
    public struct LockstepReplayBlockHeader
    {
        public ulong Magic;
        public uint Version;
        public uint HeaderSizeBytes;
        public uint StartFrame;
        public uint HashFrame;
        public uint InputCount;
        public uint Flags;
        public ulong MasterHash;
        public uint RigidbodyHash;
        public uint PlayerHash;
        public uint RoomHash;
        public uint EntityHash;
        public uint RigidbodyCount;
        public uint PlayerCount;
        public uint RoomCount;
        public uint EntityCount;
        public uint MissingMask;
        public uint NonFiniteMask;
        public uint BlockSequence;
        public uint HashCadenceFrames;
        public ulong Reserved1;
        public ulong Reserved2;
        public ulong Reserved3;
        public ulong Reserved4;
        public ulong Reserved5;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    internal struct LockstepArrayHash
    {
        public uint CategoryId;
        public uint Hash;
        public uint Count;
        public uint Flags;
        public uint FirstElementHash;
        public uint LastElementHash;
        public uint Reserved0;
        public uint Reserved1;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
    internal struct LockstepTelemetryEntry
    {
        public uint Frame;
        public uint HashLo;
        public uint HashHi;
        public uint RigidbodyHash;
        public uint PlayerHash;
        public uint RoomHash;
        public uint EntityHash;
        public uint Flags;
        public uint RigidbodyCount;
        public uint PlayerCount;
        public uint RoomCount;
        public uint EntityCount;
        public uint MissingMask;
        public uint NonFiniteMask;
        public uint ReplayBlock;
        public uint Reserved0;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
    internal struct LockstepMasterHashHistoryEntry
    {
        public uint Frame;
        public uint HashLo;
        public uint HashHi;
        public uint Flags;
        public uint MissingMask;
        public uint NonFiniteMask;
        public uint ReplayBlock;
        public uint Reserved0;
    }

    internal enum LockstepHashCategory : int
    {
        RigidbodyAups = 0,
        PlayerKinematicState = 1,
        RoomWaterLevels = 2,
        EntityAups = 3,
        Count = 4
    }

    /// <summary>
    /// Post-simulation validator that hashes deterministic truth arrays every 300 frames.
    /// </summary>
    /// <remarks>
    /// Runtime creation is a cold bootstrap bridge; all recurring execution is routed through `SystemDispatcher` POST_SIMULATION.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8900)]
    public sealed unsafe class LockstepStateValidator : MonoBehaviour, IPostFixedTickable, IScalabilityChangedEventListener
    {
        private const int HashCadenceFrames = 300;
        private const int HighEndHashCadenceFrames = 60;
        private const int HighStressHashCadenceFrames = 1200;
        private const int ReplayInputFrameCapacity = 300;
        private const int TelemetryFrameCapacity = 300;
        private const int MasterHashHistoryCapacity = 10;
        private const int MaxHashElements = 8192;
        private const int MaxGhostReplayBlocks = 128;
        private const int ReplayBlockBytes = 128 + (ReplayInputFrameCapacity * 48);
        private const ulong ReplayMagic = 0x48384C4F434B5354ul;
        private const uint ReplayVersion = 1u;
        private const uint StablePlayerId = 0x504C5952u;
        private const uint ReasonDesyncHash = 0x4453594Eu;
        private const uint ReasonGhostReplayHash = 0x47525350u;
        private const uint TelemetryFlagHashExecuted = 1u << 0;
        private const uint TelemetryFlagMissingData = 1u << 1;
        private const uint TelemetryFlagTruncated = 1u << 2;
        private const uint TelemetryFlagNonFinite = 1u << 3;
        private const uint TelemetryFlagReplayMode = 1u << 4;
        private const uint TelemetryFlagLowTierSkipped = 1u << 5;
        private const uint TelemetryFlagDesync = 1u << 6;
        private const uint TelemetryFlagWriterBusy = 1u << 7;
        private const uint ArrayFlagMissing = 1u << 0;
        private const uint ArrayFlagTruncated = 1u << 1;
        private const uint ArrayFlagNonFinite = 1u << 2;
        private const uint ReplayInputFlagNonFinite = 1u << 31;
        private const uint PlayerStateFlagNonFinite = 1u << 31;
        private const float DesyncGlitchIntensity01 = 1f;
        private const float DesyncGlitchDurationSeconds = 1f;
        private const float HashStressDeferralThreshold = 0.9f;
        private const string ReplayFileName = "lockstep_state.h8replay";
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_LOCKSTEP_STATE_VALIDATOR.bin";

        private static LockstepStateValidator _activeInstance;

        private readonly byte[] _replayWriteScratch = new byte[ReplayBlockBytes]; // COLD ALLOC: byte[14528] - replay block staging buffer - owner: LockstepStateValidator
        private readonly byte[] _replayReadScratch = new byte[ReplayBlockBytes]; // COLD ALLOC: byte[14528] - replay block load buffer - owner: LockstepStateValidator
        private readonly object _writerGate = new object(); // COLD ALLOC: object[1] - replay writer state gate - owner: LockstepStateValidator
        private Thread _writerThread;
        private AutoResetEvent _writerSignal;
        private FileStream _replayStream;
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _player;
        private IHabitatGraphService _habitat;
        private SystemDispatcher _dispatcher;
        private HectonQualityTier _cachedScalabilityTier;
        private uint _postSimulationFrame;
        private uint _lastReplayBlockSequence;
        private uint _lastMasterHashLo;
        private uint _lastMasterHashHi;
        private int _telemetryWriteIndex;
        private int _inputWriteIndex;
        private int _inputFrameCount;
        private int _registeredPostFixed;
        private int _writerShouldStop;
        private int _writeInProgress;
        private int _pendingWriteBytes;
        private int _writerFaulted;
        private int _ghostReplayActive;
        private int _ghostInputCursor;
        private int _ghostInputCount;
        private int _ghostExpectedBlockIndex;
        private uint _lastAppliedInputActions;

        /// <summary>
        /// Most recent 64-bit master simulation hash, or zero before the first sampled frame.
        /// </summary>
        public ulong LastMasterStateHash
        {
            get
            {
                return TryGetVaultBuffer(BufferID.LockstepMasterStateHash, out NativeArray<ulong> masterHash)
                    ? masterHash[0]
                    : 0UL;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (!Application.isPlaying || _activeInstance != null)
                return;

            GameObject owner = new GameObject("Lockstep State Validator"); // COLD ALLOC: GameObject[1] - core determinism post-simulation owner - owner: LockstepStateValidator
            owner.hideFlags = HideFlags.HideInHierarchy;
            owner.AddComponent<LockstepStateValidator>();
        }

        private void OnEnable()
        {
            _activeInstance = this;
            RefreshDependenciesFromRegistry();
            ConfigureSignalLanes();
            EnsureNativeState();
            ScalabilityEvents.Register(this);
            if (GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Core))
                _registeredPostFixed = 1;
        }

        private void OnDisable()
        {
            if (_registeredPostFixed != 0)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
                _registeredPostFixed = 0;
            }

            StopReplayWriter();
            ScalabilityEvents.Unregister(this);
            DisposeNativeState();
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
        }

        /// <summary>
        /// Dispatcher POST_SIMULATION tick entrypoint.
        /// </summary>
        public void PostFixedTick(float fixedDeltaTime)
        {
            uint frame = ++_postSimulationFrame;
            bool ghostReplayActive = Volatile.Read(ref _ghostReplayActive) != 0;
            uint flags = ghostReplayActive ? TelemetryFlagReplayMode : 0u;
            if (IsLowTierDisabledForNormalPlay())
            {
                WriteTelemetry(frame, flags | TelemetryFlagLowTierSkipped);
                return;
            }

            InputStateSignal inputSignal = default;
            bool hasInputSignal = false;
            if (ghostReplayActive)
                ApplyGhostReplayInput(frame);
            else
                hasInputSignal = CaptureInputFrame(frame, out inputSignal);

            int hashCadenceFrames = ResolveHashCadenceFrames();
            if ((frame % (uint)hashCadenceFrames) != 0u)
            {
                WriteTelemetry(frame, flags);
                return;
            }

            flags |= TelemetryFlagHashExecuted;
            MirrorPlayerStateToVault(frame, hasInputSignal, in inputSignal);
            MirrorRoomWaterLevelsToVault();
            ExecuteHashJobs(frame, hashCadenceFrames, ref flags);
            ValidateReplayHash(frame, ref flags);
            StageReplayWrite(frame, hashCadenceFrames, ref flags);
            WriteTelemetry(frame, flags);

            if ((flags & TelemetryFlagNonFinite) != 0u)
            {
                DumpBlackBox();
                ThrowFatalDesync(frame, flags);
            }
        }

        /// <summary>
        /// Loads a fixed `.h8replay` file and begins ghost replay input override.
        /// </summary>
        public static bool TryBeginGhostReplay(string path)
        {
            LockstepStateValidator validator = _activeInstance;
            if (validator == null || string.IsNullOrEmpty(path))
                return false;

            validator.RefreshDependenciesFromRegistry();
            return validator.LoadGhostReplay(path);
        }

        /// <summary>
        /// Stops ghost replay input override and restores normal time dilation.
        /// </summary>
        public static void EndGhostReplay()
        {
            LockstepStateValidator validator = _activeInstance;
            if (validator == null)
                return;

            Volatile.Write(ref validator._ghostReplayActive, 0);
            validator._ghostInputCursor = 0;
            validator._ghostInputCount = 0;
            validator._ghostExpectedBlockIndex = 0;
            validator._lastAppliedInputActions = 0u;
            PhysicsDeterminismSignals.ClearInputOverride();
            validator._dispatcher?.RequestTimeDilation(1f, ReasonGhostReplayHash);
        }

        /// <inheritdoc />
        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedScalabilityTier = payload.CurrentQualityTier;
        }

        private void RefreshDependenciesFromRegistry()
        {
            _dataVault = GlobalRegistry.DataVault;
            _player = GlobalRegistry.Player;
            _habitat = GlobalRegistry.HabitatGraph;
            _dispatcher = GlobalRegistry.Dispatcher;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<LockstepSnapshotSignal>.Configure(
                LockstepSnapshotSignalCapacity,
                LockstepSnapshotSignalCapacity,
                LockstepSnapshotSignalCapacity,
                LockstepSnapshotLaneHash);
            SignalBus<LockstepSnapshotSignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.Configure(
                SystemGlitchSignalCapacity,
                SystemGlitchSignalCapacity,
                SystemGlitchSignalCapacity,
                SystemGlitchLaneHash);
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
        }

        private bool IsLowTierDisabledForNormalPlay()
        {
            if (Volatile.Read(ref _ghostReplayActive) != 0)
                return false;

            return _cachedScalabilityTier == HectonQualityTier.Low || _cachedScalabilityTier == HectonQualityTier.Mx350;
        }

        private int ResolveHashCadenceFrames()
        {
            float systemStress01 = HomeostasisBrain.SystemHealthIndex01;
            if (!math.isfinite(systemStress01))
                systemStress01 = 1f;

            if (systemStress01 > HashStressDeferralThreshold)
                return HighStressHashCadenceFrames;

            HectonQualityTier tier = _cachedScalabilityTier;
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra
                ? HighEndHashCadenceFrames
                : HashCadenceFrames;
        }

        private bool CaptureInputFrame(uint frame, out InputStateSignal signal)
        {
            signal = default;
            NativeArray<LockstepReplayInputFrame> inputRing = GetVaultBuffer<LockstepReplayInputFrame>(
                BufferID.LockstepReplayInputRing,
                ReplayInputFrameCapacity,
                NativeArrayOptions.ClearMemory);
            if (!inputRing.IsCreated)
                return false;

            LockstepReplayInputFrame replayInput = default;
            replayInput.Frame = frame;
            bool hasInputSignal = TryGetLatestInputStateSignal(out signal);
            if (hasInputSignal)
            {
                InputState state = signal.State;
                uint replayFlags = state.Flags;
                replayInput.ActionsBitmask = state.ButtonsBitmask;
                replayInput.MoveDelta = SanitizeReplayInput(state.Move, float2.zero, ref replayFlags);
                replayInput.LookDelta = SanitizeReplayInput(state.Look, float2.zero, ref replayFlags);
                replayInput.VerticalDelta = SanitizeReplayInput(state.VerticalAxis, 0f, ref replayFlags);
                replayInput.CurrentInputSchemeHash = signal.CurrentInputSchemeHash;
                replayInput.Flags = replayFlags;
                replayInput.Sequence = state.Sequence;
            }

            int index = _inputWriteIndex;
            inputRing[index] = replayInput;
            _inputWriteIndex = (index + 1) % ReplayInputFrameCapacity;
            if (_inputFrameCount < ReplayInputFrameCapacity)
                _inputFrameCount++;
            return hasInputSignal;
        }

        private void ApplyGhostReplayInput(uint frame)
        {
            if (Volatile.Read(ref _ghostReplayActive) == 0 ||
                !TryGetVaultBuffer(BufferID.LockstepGhostReplayInputs, out NativeArray<LockstepReplayInputFrame> ghostInputs))
                return;

            if (_ghostInputCursor >= _ghostInputCount)
            {
                EndGhostReplay();
                return;
            }

            LockstepReplayInputFrame ghost = ghostInputs[_ghostInputCursor];
            if (ghost.Frame != frame)
            {
                ReportGhostInputFrameMismatch(frame);
                return;
            }

            _ghostInputCursor++;
            uint ghostFlags = ghost.Flags;
            PlayerInputState state = default;
            float2 move = SanitizeReplayInput(ghost.MoveDelta, float2.zero, ref ghostFlags);
            float2 look = SanitizeReplayInput(ghost.LookDelta, float2.zero, ref ghostFlags);
            state.MoveDelta = new Vector2(move.x, move.y);
            state.LookDelta = new Vector2(look.x, look.y);
            state.VerticalDelta = math.clamp(SanitizeReplayInput(ghost.VerticalDelta, 0f, ref ghostFlags), -1f, 1f);
            state.ActionsBitmask = ghost.ActionsBitmask;
            state.CurrentInputSchemeHash = ghost.CurrentInputSchemeHash;
            _lastAppliedInputActions = ghost.ActionsBitmask;
            PhysicsDeterminismSignals.PublishInputOverride(in state, (uint)Time.frameCount);
        }

        private void MirrorPlayerStateToVault(uint frame, bool hasInputSignal, in InputStateSignal inputSignal)
        {
            NativeArray<LockstepPlayerKinematicState> buffer = GetVaultBuffer<LockstepPlayerKinematicState>(
                BufferID.PlayerKinematicState,
                1,
                NativeArrayOptions.ClearMemory);
            if (!buffer.IsCreated)
                return;

            LockstepPlayerKinematicState state = default;
            state.Frame = frame;
            state.StableId = StablePlayerId;
            IPlayerRuntimeContext player = _player;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                state.SectorX = pose.Aup.GridX;
                state.SectorY = pose.Aup.GridY;
                state.SectorZ = pose.Aup.GridZ;
                state.Flags = pose.Flags;
                state.LocalPosition = SanitizeFinite(new float3(pose.Aup.LocalX, pose.Aup.LocalY, pose.Aup.LocalZ), float3.zero, ref state.Flags);
                state.Forward = SanitizeFinite(pose.Forward, new float3(0f, 0f, 1f), ref state.Flags);
            }

            if (TryGetVaultBuffer(BufferID.PlayerKinematicVelocities, out NativeArray<float3> velocities) && velocities.Length > 0)
                state.Velocity = SanitizeFinite(velocities[0], float3.zero, ref state.Flags);

            if (Volatile.Read(ref _ghostReplayActive) != 0)
                state.InputActions = _lastAppliedInputActions;
            else if (hasInputSignal)
                state.InputActions = inputSignal.State.ButtonsBitmask;

            buffer[0] = state;
        }

        private static bool TryGetLatestInputStateSignal(out InputStateSignal signal)
        {
            ReadOnlySpan<InputStateSignal> inputSignals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            if (inputSignals.Length <= 0)
            {
                signal = default;
                return false;
            }

            signal = inputSignals[inputSignals.Length - 1];
            return signal.State.Sequence != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback, ref uint flags)
        {
            bool finite = math.all(math.isfinite(value));
            if (!finite)
                flags |= PlayerStateFlagNonFinite;

            return finite ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 SanitizeReplayInput(float2 value, float2 fallback, ref uint flags)
        {
            bool finite = math.all(math.isfinite(value));
            if (!finite)
                flags |= ReplayInputFlagNonFinite;

            return finite ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeReplayInput(float value, float fallback, ref uint flags)
        {
            bool finite = math.isfinite(value);
            if (!finite)
                flags |= ReplayInputFlagNonFinite;

            return finite ? value : fallback;
        }

        private void MirrorRoomWaterLevelsToVault()
        {
            IHabitatGraphService habitat = _habitat;
            if (habitat == null || !habitat.IsInitialized)
                return;

            NativeArray<float>.ReadOnly source = habitat.RoomWaterLevels;
            int count = math.min(habitat.RoomCount, source.Length);
            if (count <= 0)
                return;

            NativeArray<float> destination = GetVaultBuffer<float>(
                BufferID.RoomWaterLevels,
                count,
                NativeArrayOptions.ClearMemory);
            if (!destination.IsCreated)
                return;

            for (int i = 0; i < count; i++)
            {
                float value = source[i];
                destination[i] = math.isfinite(value) ? value : 0f;
            }
        }

        private void ExecuteHashJobs(uint frame, int hashCadenceFrames, ref uint telemetryFlags)
        {
            EnsureNativeState();
            EnsureHashNativeState();

            NativeArray<uint> rigidbodyElementHashes = GetVaultBuffer<uint>(BufferID.LockstepRigidbodyElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<uint> playerElementHashes = GetVaultBuffer<uint>(BufferID.LockstepPlayerElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<uint> roomElementHashes = GetVaultBuffer<uint>(BufferID.LockstepRoomElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<uint> entityElementHashes = GetVaultBuffer<uint>(BufferID.LockstepEntityElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> rigidbodyElementFlags = GetVaultBuffer<byte>(BufferID.LockstepRigidbodyElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> playerElementFlags = GetVaultBuffer<byte>(BufferID.LockstepPlayerElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> roomElementFlags = GetVaultBuffer<byte>(BufferID.LockstepRoomElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<byte> entityElementFlags = GetVaultBuffer<byte>(BufferID.LockstepEntityElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            NativeArray<LockstepArrayHash> arrayHashes = GetVaultBuffer<LockstepArrayHash>(BufferID.LockstepArrayHashes, (int)LockstepHashCategory.Count, NativeArrayOptions.ClearMemory);
            NativeArray<ulong> masterHash = GetVaultBuffer<ulong>(BufferID.LockstepMasterStateHash, 1, NativeArrayOptions.ClearMemory);
            NativeArray<uint> masterFlags = GetVaultBuffer<uint>(BufferID.LockstepMasterFlags, 1, NativeArrayOptions.ClearMemory);

            if (!HashNativeStateReady(
                rigidbodyElementHashes,
                playerElementHashes,
                roomElementHashes,
                entityElementHashes,
                rigidbodyElementFlags,
                playerElementFlags,
                roomElementFlags,
                entityElementFlags,
                arrayHashes,
                masterHash,
                masterFlags))
            {
                telemetryFlags |= TelemetryFlagMissingData;
                if (masterHash.IsCreated)
                    masterHash[0] = 0UL;
                if (masterFlags.IsCreated)
                    masterFlags[0] = ArrayFlagMissing;
                _lastMasterHashLo = 0u;
                _lastMasterHashHi = 0u;
                GlobalTelemetryBus.PublishModTelemetry(ReasonDesyncHash, 0u, 0u);
                return;
            }

            IDataVault vault = _dataVault;
            NativeArray<double3> rigidbodyAups = default;
            NativeArray<LockstepPlayerKinematicState> playerStates = default;
            NativeArray<float> roomWaterLevels = default;
            NativeArray<float3> entityAups = default;

            if (vault != null)
            {
                vault.TryGetBuffer(BufferID.RigidbodyAUPs, out rigidbodyAups);
                vault.TryGetBuffer(BufferID.PlayerKinematicState, out playerStates);
                vault.TryGetBuffer(BufferID.RoomWaterLevels, out roomWaterLevels);
                vault.TryGetBuffer(BufferID.EntityAUPs, out entityAups);
            }

            int rigidbodyCount = ResolveHashCount(rigidbodyAups, ref telemetryFlags, out bool rigidbodyTruncated);
            int playerCount = ResolveHashCount(playerStates, ref telemetryFlags, out bool playerTruncated);
            int roomCount = ResolveRoomHashCount(roomWaterLevels, ref telemetryFlags, out bool roomTruncated);
            int entityCount = ResolveHashCount(entityAups, ref telemetryFlags, out bool entityTruncated);

            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.RigidbodyAups, rigidbodyCount, rigidbodyAups.IsCreated, rigidbodyTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.PlayerKinematicState, playerCount, playerStates.IsCreated, playerTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.RoomWaterLevels, roomCount, roomWaterLevels.IsCreated, roomTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.EntityAups, entityCount, entityAups.IsCreated, entityTruncated);

            JobHandle combineHandle = default;
            combineHandle = ScheduleDouble3Hash(
                rigidbodyAups,
                rigidbodyElementHashes,
                rigidbodyElementFlags,
                arrayHashes,
                LockstepHashCategory.RigidbodyAups,
                rigidbodyCount,
                combineHandle);
            combineHandle = SchedulePlayerHash(playerStates, playerElementHashes, playerElementFlags, arrayHashes, playerCount, combineHandle);
            combineHandle = ScheduleFloatHash(roomWaterLevels, roomElementHashes, roomElementFlags, arrayHashes, roomCount, combineHandle);
            combineHandle = ScheduleFloat3Hash(
                entityAups,
                entityElementHashes,
                entityElementFlags,
                arrayHashes,
                LockstepHashCategory.EntityAups,
                entityCount,
                combineHandle);
            JobHandle masterHandle = new MasterStateHashJob
            {
                ArrayHashes = arrayHashes,
                MasterHash = masterHash,
                MasterFlags = masterFlags,
                Frame = frame
            }.Schedule(combineHandle);

            // [BLOCKING_SYNC_POINT] 300-frame POST_SIMULATION hash fence.
            // The replay block must contain frame-N truth before any owner can mutate the sampled DataVault arrays.
            masterHandle.Complete();

            uint flags = masterFlags[0];
            if ((flags & ArrayFlagMissing) != 0u)
                telemetryFlags |= TelemetryFlagMissingData;
            if ((flags & ArrayFlagTruncated) != 0u)
                telemetryFlags |= TelemetryFlagTruncated;
            if ((flags & ArrayFlagNonFinite) != 0u)
                telemetryFlags |= TelemetryFlagNonFinite;

            ulong master = masterHash[0];
            _lastMasterHashLo = (uint)master;
            _lastMasterHashHi = (uint)(master >> 32);
            RecordMasterHashHistory(frame, master, flags | telemetryFlags, arrayHashes);
            GlobalTelemetryBus.PublishModTelemetry(ReasonDesyncHash, _lastMasterHashLo, _lastMasterHashHi);
            PublishLockstepSnapshot(frame, master, hashCadenceFrames, flags | telemetryFlags, arrayHashes);
        }

        private int ResolveHashCount<T>(NativeArray<T> source, ref uint telemetryFlags, out bool truncated)
            where T : struct
        {
            truncated = false;
            if (!source.IsCreated)
            {
                telemetryFlags |= TelemetryFlagMissingData;
                return 0;
            }

            if (source.Length > MaxHashElements)
            {
                truncated = true;
                telemetryFlags |= TelemetryFlagTruncated;
            }

            return math.min(source.Length, MaxHashElements);
        }

        private int ResolveRoomHashCount(NativeArray<float> source, ref uint telemetryFlags, out bool truncated)
        {
            truncated = false;
            if (!source.IsCreated)
            {
                telemetryFlags |= TelemetryFlagMissingData;
                return 0;
            }

            int count = source.Length;
            IHabitatGraphService habitat = _habitat;
            if (habitat != null && habitat.IsInitialized && habitat.RoomCount > 0)
                count = math.min(count, habitat.RoomCount);

            if (count > MaxHashElements)
            {
                truncated = true;
                telemetryFlags |= TelemetryFlagTruncated;
            }

            return math.min(count, MaxHashElements);
        }

        private JobHandle ScheduleFloat3Hash(
            NativeArray<float3> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            LockstepHashCategory category,
            int count,
            JobHandle combineDependency)
        {
            if (count <= 0 || !source.IsCreated)
                return combineDependency;

            JobHandle hashHandle = new HashFloat3ArrayJob
            {
                Source = source,
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                CategorySalt = (uint)category
            }.Schedule(count, 64);

            return new CombineElementHashesJob
            {
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                ArrayHashes = arrayHashes,
                CategoryIndex = (int)category,
                Count = count
            }.Schedule(JobHandle.CombineDependencies(hashHandle, combineDependency));
        }

        private JobHandle ScheduleDouble3Hash(
            NativeArray<double3> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            LockstepHashCategory category,
            int count,
            JobHandle combineDependency)
        {
            if (count <= 0 || !source.IsCreated)
                return combineDependency;

            JobHandle hashHandle = new HashDouble3ArrayJob
            {
                Source = source,
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                CategorySalt = (uint)category
            }.Schedule(count, 64);

            return new CombineElementHashesJob
            {
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                ArrayHashes = arrayHashes,
                CategoryIndex = (int)category,
                Count = count
            }.Schedule(JobHandle.CombineDependencies(hashHandle, combineDependency));
        }

        private JobHandle ScheduleFloatHash(
            NativeArray<float> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            int count,
            JobHandle combineDependency)
        {
            if (count <= 0 || !source.IsCreated)
                return combineDependency;

            JobHandle hashHandle = new HashFloatArrayJob
            {
                Source = source,
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                CategorySalt = (uint)LockstepHashCategory.RoomWaterLevels
            }.Schedule(count, 64);

            return new CombineElementHashesJob
            {
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                ArrayHashes = arrayHashes,
                CategoryIndex = (int)LockstepHashCategory.RoomWaterLevels,
                Count = count
            }.Schedule(JobHandle.CombineDependencies(hashHandle, combineDependency));
        }

        private JobHandle SchedulePlayerHash(
            NativeArray<LockstepPlayerKinematicState> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            int count,
            JobHandle combineDependency)
        {
            if (count <= 0 || !source.IsCreated)
                return combineDependency;

            JobHandle hashHandle = new HashPlayerKinematicArrayJob
            {
                Source = source,
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                CategorySalt = (uint)LockstepHashCategory.PlayerKinematicState
            }.Schedule(count, 32);

            return new CombineElementHashesJob
            {
                ElementHashes = elementHashes,
                ElementFlags = elementFlags,
                ArrayHashes = arrayHashes,
                CategoryIndex = (int)LockstepHashCategory.PlayerKinematicState,
                Count = count
            }.Schedule(JobHandle.CombineDependencies(hashHandle, combineDependency));
        }

        private void SetDefaultArrayHash(NativeArray<LockstepArrayHash> arrayHashes, LockstepHashCategory category, int count, bool present, bool truncated)
        {
            uint flags = present ? 0u : ArrayFlagMissing;
            if (present && truncated)
                flags |= ArrayFlagTruncated;

            arrayHashes[(int)category] = new LockstepArrayHash
            {
                CategoryId = (uint)category,
                Hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)category),
                Count = (uint)math.max(0, count),
                Flags = flags
            };
        }

        private void PublishLockstepSnapshot(
            uint frame,
            ulong masterHash,
            int hashCadenceFrames,
            uint flags,
            NativeArray<LockstepArrayHash> arrayHashes)
        {
            LockstepSnapshotSignal signal = default;
            signal.MasterHash = masterHash;
            signal.Frame = frame;
            signal.HashCadenceFrames = (uint)hashCadenceFrames;
            signal.Flags = flags;
            signal.MissingMask = BuildCategoryMask(arrayHashes, ArrayFlagMissing);
            signal.NonFiniteMask = BuildCategoryMask(arrayHashes, ArrayFlagNonFinite);
            signal.ReplayBlock = _lastReplayBlockSequence;
            SignalBus<LockstepSnapshotSignal>.Push(in signal);
        }

        private void RecordMasterHashHistory(
            uint frame,
            ulong masterHash,
            uint flags,
            NativeArray<LockstepArrayHash> arrayHashes)
        {
            NativeArray<LockstepMasterHashHistoryEntry> history = GetVaultBuffer<LockstepMasterHashHistoryEntry>(
                BufferID.LockstepMasterHashHistory,
                MasterHashHistoryCapacity,
                NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = GetVaultBuffer<int>(
                BufferID.LockstepMasterHashHistoryCursor,
                1,
                NativeArrayOptions.ClearMemory);
            if (!history.IsCreated || !cursor.IsCreated)
                return;

            int index = cursor[0];
            if ((uint)index >= MasterHashHistoryCapacity)
                index = 0;

            history[index] = new LockstepMasterHashHistoryEntry
            {
                Frame = frame,
                HashLo = (uint)masterHash,
                HashHi = (uint)(masterHash >> 32),
                Flags = flags,
                MissingMask = BuildCategoryMask(arrayHashes, ArrayFlagMissing),
                NonFiniteMask = BuildCategoryMask(arrayHashes, ArrayFlagNonFinite),
                ReplayBlock = _lastReplayBlockSequence
            };

            cursor[0] = (index + 1) % MasterHashHistoryCapacity;
        }

        private void ValidateReplayHash(uint frame, ref uint telemetryFlags)
        {
            if (Volatile.Read(ref _ghostReplayActive) == 0 ||
                !TryGetVaultBuffer(BufferID.LockstepGhostReplayHeaders, out NativeArray<LockstepReplayBlockHeader> ghostHeaders) ||
                !TryGetVaultBuffer(BufferID.LockstepMasterStateHash, out NativeArray<ulong> masterHash))
                return;

            int blockIndex = _ghostExpectedBlockIndex;
            if (blockIndex < 0 || blockIndex >= ghostHeaders.Length)
                return;

            LockstepReplayBlockHeader expected = ghostHeaders[blockIndex];
            if (expected.Magic != ReplayMagic)
                return;

            if (expected.HashFrame != frame)
            {
                telemetryFlags |= TelemetryFlagDesync;
                ReportDesync(frame, in expected, 2u);
                _ghostExpectedBlockIndex = blockIndex + 1;
                return;
            }

            if (expected.MasterHash == masterHash[0])
            {
                _ghostExpectedBlockIndex = blockIndex + 1;
                return;
            }

            telemetryFlags |= TelemetryFlagDesync;
            ReportDesync(frame, in expected, 1u);
            _ghostExpectedBlockIndex = blockIndex + 1;
        }

        private void ReportDesync(uint frame, in LockstepReplayBlockHeader expected, uint flags)
        {
            DesyncDetectedSignal signal = default;
            signal.LocalHash = _lastMasterHashLo;
            signal.AuthoritativeHash = (uint)expected.MasterHash;
            signal.Frame = frame;
            signal.SourceId = ReasonDesyncHash;
            signal.LastFenceFrame = expected.HashFrame;
            signal.Flags = (byte)flags;
            PhysicsDeterminismSignals.Publish(in signal);
            PublishSystemGlitchSignal(frame, in expected, (byte)flags);
            _dispatcher?.RequestSimulationPause(true, ReasonDesyncHash);
            StopGhostReplayAfterFault();
            DumpBlackBox();
        }

        private void PublishSystemGlitchSignal(uint frame, in LockstepReplayBlockHeader expected, byte reason)
        {
            SystemGlitchSignal signal = default;
            signal.Frame = frame;
            signal.SourceId = ReasonDesyncHash;
            signal.LocalHash = _lastMasterHashLo;
            signal.ExpectedHash = (uint)expected.MasterHash;
            signal.Intensity01 = DesyncGlitchIntensity01;
            signal.DurationSeconds = DesyncGlitchDurationSeconds;
            signal.Reason = reason;
            SignalBus<SystemGlitchSignal>.Push(in signal);
            SystemDispatcher.RequestVisualStaticGlitch(DesyncGlitchDurationSeconds);
        }

        private void ReportGhostInputFrameMismatch(uint frame)
        {
            LockstepReplayBlockHeader expected = default;
            int blockIndex = _ghostExpectedBlockIndex;
            if (TryGetVaultBuffer(BufferID.LockstepGhostReplayHeaders, out NativeArray<LockstepReplayBlockHeader> ghostHeaders) &&
                blockIndex >= 0 &&
                blockIndex < ghostHeaders.Length)
            {
                expected = ghostHeaders[blockIndex];
            }

            if (expected.Magic != ReplayMagic)
            {
                expected.Magic = ReplayMagic;
                expected.HashFrame = frame;
                expected.MasterHash = TryGetVaultBuffer(BufferID.LockstepMasterStateHash, out NativeArray<ulong> masterHash) ? masterHash[0] : 0UL;
            }

            ReportDesync(frame, in expected, 3u);
        }

        private void StopGhostReplayAfterFault()
        {
            Volatile.Write(ref _ghostReplayActive, 0);
            _ghostInputCursor = 0;
            _ghostInputCount = 0;
            _ghostExpectedBlockIndex = 0;
            _lastAppliedInputActions = 0u;
            PhysicsDeterminismSignals.ClearInputOverride();
        }

        private void StageReplayWrite(uint frame, int hashCadenceFrames, ref uint telemetryFlags)
        {
            if (Volatile.Read(ref _ghostReplayActive) != 0)
                return;

            if (_inputFrameCount < ReplayInputFrameCapacity)
                return;

            EnsureReplayWriter();
            if (_replayStream == null || _writerSignal == null)
                return;

            if (Volatile.Read(ref _writerFaulted) != 0)
            {
                telemetryFlags |= TelemetryFlagWriterBusy;
                StopReplayWriter();
                return;
            }

            if (Volatile.Read(ref _writeInProgress) != 0)
            {
                telemetryFlags |= TelemetryFlagWriterBusy;
                return;
            }

            int byteCount = BuildReplayBlock(frame, _replayWriteScratch, telemetryFlags, hashCadenceFrames);
            if (byteCount <= 0)
                return;

            Volatile.Write(ref _pendingWriteBytes, byteCount);
            Volatile.Write(ref _writeInProgress, 1);
            _writerSignal.Set();
        }

        private int BuildReplayBlock(uint frame, byte[] destination, uint telemetryFlags, int hashCadenceFrames)
        {
            if (destination == null || destination.Length < ReplayBlockBytes)
                return 0;

            if (!TryGetVaultBuffer(BufferID.LockstepArrayHashes, out NativeArray<LockstepArrayHash> arrayHashes) ||
                !TryGetVaultBuffer(BufferID.LockstepMasterStateHash, out NativeArray<ulong> masterHash) ||
                !TryGetVaultBuffer(BufferID.LockstepReplayInputRing, out NativeArray<LockstepReplayInputFrame> inputRing))
            {
                return 0;
            }

            LockstepArrayHash rigidbody = arrayHashes[(int)LockstepHashCategory.RigidbodyAups];
            LockstepArrayHash player = arrayHashes[(int)LockstepHashCategory.PlayerKinematicState];
            LockstepArrayHash room = arrayHashes[(int)LockstepHashCategory.RoomWaterLevels];
            LockstepArrayHash entity = arrayHashes[(int)LockstepHashCategory.EntityAups];
            LockstepReplayBlockHeader header = default;
            header.Magic = ReplayMagic;
            header.Version = ReplayVersion;
            header.HeaderSizeBytes = 128u;
            header.StartFrame = frame - (ReplayInputFrameCapacity - 1u);
            header.HashFrame = frame;
            header.InputCount = ReplayInputFrameCapacity;
            header.Flags = telemetryFlags;
            header.MasterHash = masterHash[0];
            header.RigidbodyHash = rigidbody.Hash;
            header.PlayerHash = player.Hash;
            header.RoomHash = room.Hash;
            header.EntityHash = entity.Hash;
            header.RigidbodyCount = rigidbody.Count;
            header.PlayerCount = player.Count;
            header.RoomCount = room.Count;
            header.EntityCount = entity.Count;
            header.MissingMask = BuildCategoryMask(arrayHashes, ArrayFlagMissing);
            header.NonFiniteMask = BuildCategoryMask(arrayHashes, ArrayFlagNonFinite);
            header.HashCadenceFrames = (uint)hashCadenceFrames;
            header.BlockSequence = ++_lastReplayBlockSequence;

            fixed (byte* rawDestination = destination)
            {
                UnsafeUtility.CopyStructureToPtr(ref header, rawDestination);
                int offset = 128;
                int start = _inputWriteIndex;
                for (int i = 0; i < ReplayInputFrameCapacity; i++)
                {
                    int index = (start + i) % ReplayInputFrameCapacity;
                    LockstepReplayInputFrame input = inputRing[index];
                    UnsafeUtility.CopyStructureToPtr(ref input, rawDestination + offset);
                    offset += 48;
                }
            }

            return ReplayBlockBytes;
        }

        private static uint BuildCategoryMask(NativeArray<LockstepArrayHash> arrayHashes, uint flag)
        {
            if (!arrayHashes.IsCreated)
                return 0u;

            uint mask = 0u;
            for (int i = 0; i < (int)LockstepHashCategory.Count; i++)
            {
                if ((arrayHashes[i].Flags & flag) != 0u)
                    mask |= 1u << i;
            }

            return mask;
        }

        private void WriteTelemetry(uint frame, uint flags)
        {
            NativeArray<LockstepTelemetryEntry> telemetryRing = GetVaultBuffer<LockstepTelemetryEntry>(
                BufferID.LockstepTelemetryRing,
                TelemetryFrameCapacity,
                NativeArrayOptions.ClearMemory);
            if (!telemetryRing.IsCreated)
                return;

            NativeArray<LockstepArrayHash> arrayHashes = GetVaultBuffer<LockstepArrayHash>(
                BufferID.LockstepArrayHashes,
                (int)LockstepHashCategory.Count,
                NativeArrayOptions.ClearMemory);
            LockstepArrayHash rigidbody = ReadArrayHash(arrayHashes, LockstepHashCategory.RigidbodyAups);
            LockstepArrayHash player = ReadArrayHash(arrayHashes, LockstepHashCategory.PlayerKinematicState);
            LockstepArrayHash room = ReadArrayHash(arrayHashes, LockstepHashCategory.RoomWaterLevels);
            LockstepArrayHash entity = ReadArrayHash(arrayHashes, LockstepHashCategory.EntityAups);
            int index = _telemetryWriteIndex;
            telemetryRing[index] = new LockstepTelemetryEntry
            {
                Frame = frame,
                HashLo = _lastMasterHashLo,
                HashHi = _lastMasterHashHi,
                RigidbodyHash = rigidbody.Hash,
                PlayerHash = player.Hash,
                RoomHash = room.Hash,
                EntityHash = entity.Hash,
                Flags = flags,
                RigidbodyCount = rigidbody.Count,
                PlayerCount = player.Count,
                RoomCount = room.Count,
                EntityCount = entity.Count,
                MissingMask = BuildCategoryMask(arrayHashes, ArrayFlagMissing),
                NonFiniteMask = BuildCategoryMask(arrayHashes, ArrayFlagNonFinite),
                ReplayBlock = _lastReplayBlockSequence
            };
            _telemetryWriteIndex = (index + 1) % TelemetryFrameCapacity;
        }

        private static LockstepArrayHash ReadArrayHash(NativeArray<LockstepArrayHash> arrayHashes, LockstepHashCategory category)
        {
            return arrayHashes.IsCreated ? arrayHashes[(int)category] : default;
        }

        private void DumpBlackBox()
        {
            if (!TryGetVaultBuffer(BufferID.LockstepTelemetryRing, out NativeArray<LockstepTelemetryEntry> telemetryRing))
                return;

            try
            {
                string projectRoot = Application.dataPath;
                if (!string.IsNullOrEmpty(projectRoot))
                    projectRoot = Directory.GetParent(projectRoot)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string path = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    uint magic = 0x4C535456u;
                    uint count = TelemetryFrameCapacity;
                    WriteStruct(stream, ref magic);
                    WriteStruct(stream, ref count);
                    int start = _telemetryWriteIndex;
                    for (int i = 0; i < TelemetryFrameCapacity; i++)
                    {
                        int index = (start + i) % TelemetryFrameCapacity;
                        LockstepTelemetryEntry entry = telemetryRing[index];
                        WriteStruct(stream, ref entry);
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowFatalDesync(uint frame, uint flags)
        {
            ulong masterHash = ((ulong)_lastMasterHashHi << 32) | _lastMasterHashLo;
            throw new FatalDesyncException(frame, masterHash, flags);
        }

        private static void WriteStruct<T>(Stream stream, ref T value)
            where T : unmanaged
        {
            int size = UnsafeUtility.SizeOf<T>();
            byte* buffer = stackalloc byte[size];
            UnsafeUtility.CopyStructureToPtr(ref value, buffer);
            for (int i = 0; i < size; i++)
                stream.WriteByte(buffer[i]);
        }

        private bool LoadGhostReplay(string path)
        {
            StopReplayWriter();
            PhysicsDeterminismSignals.ClearInputOverride();
            Volatile.Write(ref _ghostReplayActive, 0);
            EnsureGhostReplayBuffers();
            if (!TryGetVaultBuffer(BufferID.LockstepGhostReplayHeaders, out NativeArray<LockstepReplayBlockHeader> ghostHeaders) ||
                !TryGetVaultBuffer(BufferID.LockstepGhostReplayInputs, out NativeArray<LockstepReplayInputFrame> ghostInputs))
            {
                return false;
            }

            _ghostInputCursor = 0;
            _ghostInputCount = 0;
            _ghostExpectedBlockIndex = 0;
            _lastAppliedInputActions = 0u;
            _lastReplayBlockSequence = 0u;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int blockIndex = 0;
                    while (blockIndex < MaxGhostReplayBlocks)
                    {
                        int read = ReadFullBlock(stream, _replayReadScratch);
                        if (read == 0)
                            break;
                        if (read != ReplayBlockBytes)
                            break;

                        LockstepReplayBlockHeader header = ReadStructFromBuffer<LockstepReplayBlockHeader>(_replayReadScratch, 0);
                        if (!IsValidReplayHeader(in header))
                            break;

                        if (header.BlockSequence != (uint)(blockIndex + 1))
                            break;

                        if (blockIndex > 0)
                        {
                            LockstepReplayBlockHeader previous = ghostHeaders[blockIndex - 1];
                            if (header.StartFrame != previous.HashFrame + 1u)
                                break;
                        }

                        ghostHeaders[blockIndex] = header;
                        int inputBase = blockIndex * ReplayInputFrameCapacity;
                        int offset = 128;
                        bool inputFramesValid = true;
                        for (int i = 0; i < ReplayInputFrameCapacity; i++)
                        {
                            LockstepReplayInputFrame input = ReadStructFromBuffer<LockstepReplayInputFrame>(_replayReadScratch, offset);
                            if (input.Frame != header.StartFrame + (uint)i)
                            {
                                inputFramesValid = false;
                                break;
                            }

                            ghostInputs[inputBase + i] = input;
                            offset += 48;
                        }

                        if (!inputFramesValid)
                            break;

                        blockIndex++;
                    }

                    _ghostInputCount = blockIndex * ReplayInputFrameCapacity;
                    if (_ghostInputCount <= 0)
                        return false;

                    LockstepReplayBlockHeader firstHeader = ghostHeaders[0];
                    _postSimulationFrame = firstHeader.StartFrame > 0u ? firstHeader.StartFrame - 1u : 0u;
                    _inputWriteIndex = 0;
                    _inputFrameCount = 0;
                    _lastAppliedInputActions = 0u;
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                return false;
            }

            Volatile.Write(ref _ghostReplayActive, 1);
            _dispatcher?.RequestHeadlessTimeDilation(10f, ReasonGhostReplayHash);
            return true;
        }

        private static bool IsValidReplayHeader(in LockstepReplayBlockHeader header)
        {
            if (header.Magic != ReplayMagic ||
                header.Version != ReplayVersion ||
                header.HeaderSizeBytes != 128u ||
                header.InputCount != ReplayInputFrameCapacity)
                return false;

            if (header.HashFrame < header.StartFrame)
                return false;

            uint cadenceFrames = header.HashCadenceFrames != 0u ? header.HashCadenceFrames : HashCadenceFrames;
            if (cadenceFrames == 0u || (header.HashFrame % cadenceFrames) != 0u)
                return false;

            return (header.HashFrame - header.StartFrame + 1u) == ReplayInputFrameCapacity;
        }

        private static int ReadFullBlock(Stream stream, byte[] destination)
        {
            int total = 0;
            while (total < destination.Length)
            {
                int read = stream.Read(destination, total, destination.Length - total);
                if (read <= 0)
                    break;

                total += read;
            }

            return total;
        }

        private static T ReadStructFromBuffer<T>(byte[] source, int offset)
            where T : unmanaged
        {
            fixed (byte* rawSource = source)
            {
                return UnsafeUtility.ReadArrayElement<T>(rawSource + offset, 0);
            }
        }

        private IDataVault ResolveDataVault()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
                return vault;

            RefreshDependenciesFromRegistry();
            return _dataVault;
        }

        private NativeArray<T> GetVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            where T : struct
        {
            IDataVault vault = ResolveDataVault();
            return vault != null
                ? vault.GetBuffer<T>(bufferId, requiredLength, SystemID.CoreDeterminism, options)
                : default;
        }

        private bool TryGetVaultBuffer<T>(BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            IDataVault vault = ResolveDataVault();
            if (vault != null && vault.TryGetBuffer(bufferId, out buffer) && buffer.IsCreated)
                return true;

            buffer = default;
            return false;
        }

        private void EnsureNativeState()
        {
            GetVaultBuffer<LockstepArrayHash>(BufferID.LockstepArrayHashes, (int)LockstepHashCategory.Count, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<ulong>(BufferID.LockstepMasterStateHash, 1, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<uint>(BufferID.LockstepMasterFlags, 1, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<LockstepTelemetryEntry>(BufferID.LockstepTelemetryRing, TelemetryFrameCapacity, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<LockstepMasterHashHistoryEntry>(BufferID.LockstepMasterHashHistory, MasterHashHistoryCapacity, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<int>(BufferID.LockstepMasterHashHistoryCursor, 1, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<LockstepReplayInputFrame>(BufferID.LockstepReplayInputRing, ReplayInputFrameCapacity, NativeArrayOptions.ClearMemory);
        }

        private void EnsureHashNativeState()
        {
            GetVaultBuffer<uint>(BufferID.LockstepRigidbodyElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<uint>(BufferID.LockstepPlayerElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<uint>(BufferID.LockstepRoomElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<uint>(BufferID.LockstepEntityElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<byte>(BufferID.LockstepRigidbodyElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<byte>(BufferID.LockstepPlayerElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<byte>(BufferID.LockstepRoomElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            GetVaultBuffer<byte>(BufferID.LockstepEntityElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
        }

        private static bool HashNativeStateReady(
            NativeArray<uint> rigidbodyElementHashes,
            NativeArray<uint> playerElementHashes,
            NativeArray<uint> roomElementHashes,
            NativeArray<uint> entityElementHashes,
            NativeArray<byte> rigidbodyElementFlags,
            NativeArray<byte> playerElementFlags,
            NativeArray<byte> roomElementFlags,
            NativeArray<byte> entityElementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            NativeArray<ulong> masterHash,
            NativeArray<uint> masterFlags)
        {
            return rigidbodyElementHashes.IsCreated &&
                playerElementHashes.IsCreated &&
                roomElementHashes.IsCreated &&
                entityElementHashes.IsCreated &&
                rigidbodyElementFlags.IsCreated &&
                playerElementFlags.IsCreated &&
                roomElementFlags.IsCreated &&
                entityElementFlags.IsCreated &&
                arrayHashes.IsCreated &&
                masterHash.IsCreated &&
                masterFlags.IsCreated;
        }

        private void EnsureGhostReplayBuffers()
        {
            GetVaultBuffer<LockstepReplayBlockHeader>(BufferID.LockstepGhostReplayHeaders, MaxGhostReplayBlocks, NativeArrayOptions.ClearMemory);
            GetVaultBuffer<LockstepReplayInputFrame>(BufferID.LockstepGhostReplayInputs, MaxGhostReplayBlocks * ReplayInputFrameCapacity, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeNativeState()
        {
            // DataVault owns lockstep buffers and preserves the latest hash/blackbox across component lifetime churn.
        }

        private void EnsureReplayWriter()
        {
            if (_writerSignal != null)
                return;

            try
            {
                string replayPath = Path.Combine(Application.persistentDataPath, ReplayFileName);
                _replayStream = new FileStream(replayPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                _writerSignal = new AutoResetEvent(false);
                _lastReplayBlockSequence = 0u;
                Volatile.Write(ref _writerFaulted, 0);
                _writerThread = new Thread(ReplayWriterLoop)
                {
                    IsBackground = true,
                    Name = "H8.LockstepReplayWriter",
                    Priority = HectonThreadPriorityPolicy.Resolve(HectonThreadRole.BackgroundIo)
                };
                _writerThread.Start();
            }
            catch (Exception ex)
            {
                LogException(ex);
                StopReplayWriter();
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogException(Exception ex)
        {
            Debug.LogError(ex);
        }

        private void ReplayWriterLoop()
        {
            while (Volatile.Read(ref _writerShouldStop) == 0)
            {
                _writerSignal.WaitOne();
                if (Volatile.Read(ref _writerShouldStop) != 0)
                    return;

                int byteCount = Volatile.Read(ref _pendingWriteBytes);
                if (byteCount <= 0)
                {
                    Volatile.Write(ref _writeInProgress, 0);
                    continue;
                }

                try
                {
                    lock (_writerGate)
                    {
                        _replayStream?.Write(_replayWriteScratch, 0, byteCount);
                        _replayStream?.Flush(false);
                    }
                }
                catch
                {
                    Volatile.Write(ref _writerFaulted, 1);
                    return;
                }
                finally
                {
                    Volatile.Write(ref _pendingWriteBytes, 0);
                    Volatile.Write(ref _writeInProgress, 0);
                }
            }
        }

        private void StopReplayWriter()
        {
            Volatile.Write(ref _writerShouldStop, 1);
            _writerSignal?.Set();
            if (_writerThread != null && _writerThread.IsAlive)
                _writerThread.Join(250);
            _writerThread = null;
            _writerSignal?.Dispose();
            _writerSignal = null;
            _replayStream?.Dispose();
            _replayStream = null;
            Volatile.Write(ref _writerShouldStop, 0);
            Volatile.Write(ref _writeInProgress, 0);
            Volatile.Write(ref _pendingWriteBytes, 0);
            Volatile.Write(ref _writerFaulted, 0);
        }
    }

    internal static class LockstepHashMath
    {
        public const uint FnvOffset32 = 2166136261u;
        private const uint FnvPrime32 = 16777619u;
        private const ulong FnvOffset64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(uint hash, uint value)
        {
            hash ^= value;
            return hash * FnvPrime32;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(uint hash, int value)
        {
            return Fnv1A(hash, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1A(uint hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            hash = Fnv1A(hash, (uint)bits);
            return Fnv1A(hash, (uint)(bits >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1AQuantized(uint hash, float value)
        {
            return Fnv1A(hash, DeterministicPhysicsMath.QuantizeMillimeter(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1AFloat3(uint hash, float3 value)
        {
            hash = Fnv1AQuantized(hash, value.x);
            hash = Fnv1AQuantized(hash, value.y);
            return Fnv1AQuantized(hash, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1AQuantized(uint hash, double value)
        {
            double clamped = math.clamp(value, -1000000000000d, 1000000000000d);
            long millimeters = (long)math.round(clamped * 1000d);
            return Fnv1A(hash, millimeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1ADouble3(uint hash, double3 value)
        {
            hash = Fnv1AQuantized(hash, value.x);
            hash = Fnv1AQuantized(hash, value.y);
            return Fnv1AQuantized(hash, value.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint QuantizeWaterLevel(float value)
        {
            if (!math.isfinite(value))
                return 0u;

            float clamped = math.clamp(value, -10000f, 10000f);
            return unchecked((uint)(int)(clamped * 10000f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Fnv1A64(ulong hash, uint value)
        {
            hash ^= value;
            return hash * FnvPrime64;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong BuildMasterHash(NativeArray<LockstepArrayHash> hashes, uint frame)
        {
            ulong hash = Fnv1A64(FnvOffset64, frame);
            for (int i = 0; i < (int)LockstepHashCategory.Count; i++)
            {
                LockstepArrayHash arrayHash = hashes[i];
                hash = Fnv1A64(hash, arrayHash.CategoryId);
                hash = Fnv1A64(hash, arrayHash.Hash);
                hash = Fnv1A64(hash, arrayHash.Count);
                hash = Fnv1A64(hash, arrayHash.Flags);
            }

            return hash;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct HashFloat3ArrayJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Source;
        [WriteOnly] public NativeArray<uint> ElementHashes;
        [WriteOnly] public NativeArray<byte> ElementFlags;
        public uint CategorySalt;

        public void Execute(int index)
        {
            float3 value = Source[index];
            bool finite = math.all(math.isfinite(value));
            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, CategorySalt);
            hash = LockstepHashMath.Fnv1A(hash, index);
            hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, value) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
            ElementHashes[index] = hash;
            ElementFlags[index] = finite ? (byte)0 : (byte)1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct HashDouble3ArrayJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<double3> Source;
        [WriteOnly] public NativeArray<uint> ElementHashes;
        [WriteOnly] public NativeArray<byte> ElementFlags;
        public uint CategorySalt;

        public void Execute(int index)
        {
            double3 value = Source[index];
            bool finite = math.all(math.isfinite(value));
            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, CategorySalt);
            hash = LockstepHashMath.Fnv1A(hash, index);
            hash = finite ? LockstepHashMath.Fnv1ADouble3(hash, value) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
            ElementHashes[index] = hash;
            ElementFlags[index] = finite ? (byte)0 : (byte)1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct HashFloatArrayJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> Source;
        [WriteOnly] public NativeArray<uint> ElementHashes;
        [WriteOnly] public NativeArray<byte> ElementFlags;
        public uint CategorySalt;

        public void Execute(int index)
        {
            float value = Source[index];
            bool finite = math.isfinite(value);
            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, CategorySalt);
            hash = LockstepHashMath.Fnv1A(hash, index);
            hash = LockstepHashMath.Fnv1A(hash, finite ? LockstepHashMath.QuantizeWaterLevel(value) : 0xBADF10A7u);
            ElementHashes[index] = hash;
            ElementFlags[index] = finite ? (byte)0 : (byte)1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct HashPlayerKinematicArrayJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<LockstepPlayerKinematicState> Source;
        [WriteOnly] public NativeArray<uint> ElementHashes;
        [WriteOnly] public NativeArray<byte> ElementFlags;
        public uint CategorySalt;

        public void Execute(int index)
        {
            LockstepPlayerKinematicState state = Source[index];
            bool finite =
                math.all(math.isfinite(state.LocalPosition)) &&
                math.all(math.isfinite(state.Velocity)) &&
                math.all(math.isfinite(state.Forward));

            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, CategorySalt);
            hash = LockstepHashMath.Fnv1A(hash, index);
            hash = LockstepHashMath.Fnv1A(hash, state.SectorX);
            hash = LockstepHashMath.Fnv1A(hash, state.SectorY);
            hash = LockstepHashMath.Fnv1A(hash, state.SectorZ);
            hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, state.LocalPosition) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
            hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, state.Velocity) : hash;
            hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, state.Forward) : hash;
            hash = LockstepHashMath.Fnv1A(hash, state.Frame);
            hash = LockstepHashMath.Fnv1A(hash, state.Flags);
            hash = LockstepHashMath.Fnv1A(hash, state.InputActions);
            hash = LockstepHashMath.Fnv1A(hash, state.StableId);
            ElementHashes[index] = hash;
            ElementFlags[index] = finite ? (byte)0 : (byte)1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CombineElementHashesJob : IJob
    {
        [ReadOnly] public NativeArray<uint> ElementHashes;
        [ReadOnly] public NativeArray<byte> ElementFlags;
        public NativeArray<LockstepArrayHash> ArrayHashes;
        public int CategoryIndex;
        public int Count;

        public void Execute()
        {
            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)CategoryIndex);
            uint flags = 0u;
            uint first = 0u;
            uint last = 0u;
            for (int i = 0; i < Count; i++)
            {
                uint elementHash = ElementHashes[i];
                if (i == 0)
                    first = elementHash;
                last = elementHash;
                hash = LockstepHashMath.Fnv1A(hash, elementHash);
                if (ElementFlags[i] != 0)
                    flags |= 1u;
            }

            LockstepArrayHash arrayHash = ArrayHashes[CategoryIndex];
            arrayHash.Hash = hash;
            arrayHash.Count = (uint)Count;
            arrayHash.FirstElementHash = first;
            arrayHash.LastElementHash = last;
            if (flags != 0u)
                arrayHash.Flags |= 4u;
            ArrayHashes[CategoryIndex] = arrayHash;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct MasterStateHashJob : IJob
    {
        [ReadOnly] public NativeArray<LockstepArrayHash> ArrayHashes;
        public NativeArray<ulong> MasterHash;
        public NativeArray<uint> MasterFlags;
        public uint Frame;

        public void Execute()
        {
            uint flags = 0u;
            for (int i = 0; i < (int)LockstepHashCategory.Count; i++)
                flags |= ArrayHashes[i].Flags;

            MasterHash[0] = LockstepHashMath.BuildMasterHash(ArrayHashes, Frame);
            MasterFlags[0] = flags;
        }
    }
}
