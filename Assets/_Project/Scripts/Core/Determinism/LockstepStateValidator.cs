using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

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
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LockstepPlayerKinematicState
    {
        private const uint StablePlayerHash = 0x504C5952u;

        [FieldOffset(0)] public double3 PositionAup;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float3 InputVector;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint InputActions;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;

        public long SectorX
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ResolveSector(PositionAup.x);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 local = LocalPosition;
                PositionAup.x = ComposeAup(value, local.x);
            }
        }

        public long SectorY
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ResolveSector(PositionAup.y);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 local = LocalPosition;
                PositionAup.y = ComposeAup(value, local.y);
            }
        }

        public long SectorZ
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ResolveSector(PositionAup.z);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                float3 local = LocalPosition;
                PositionAup.z = ComposeAup(value, local.z);
            }
        }

        public float3 LocalPosition
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                double cellSize = ResolveCellSize();
                return new float3(
                    (float)(PositionAup.x - (ResolveSector(PositionAup.x) * cellSize)),
                    (float)(PositionAup.y - (ResolveSector(PositionAup.y) * cellSize)),
                    (float)(PositionAup.z - (ResolveSector(PositionAup.z) * cellSize)));
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                PositionAup = new double3(
                    ComposeAup(SectorX, value.x),
                    ComposeAup(SectorY, value.y),
                    ComposeAup(SectorZ, value.z));
            }
        }

        public float3 Forward
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => InputVector;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => InputVector = value;
        }

        public uint StableId
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => StablePlayerHash;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { }
        }

        public uint HashCadenceFrames
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 0u;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ResolveCellSize()
        {
            return math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long ResolveSector(double absolute)
        {
            return math.isfinite(absolute) ? (long)math.floor(absolute / ResolveCellSize()) : 0L;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ComposeAup(long sector, float local)
        {
            double safeLocal = math.isfinite(local) ? local : 0d;
            return (sector * ResolveCellSize()) + safeLocal;
        }
    }

    /// <summary>
    /// Fixed-size replay input frame stored in `.h8replay` blocks.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct LockstepReplayInputFrame
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActionsBitmask;
        [FieldOffset(8)] public float2 MoveDelta;
        [FieldOffset(16)] public float2 LookDelta;
        [FieldOffset(24)] public float VerticalDelta;
        [FieldOffset(28)] public uint CurrentInputSchemeHash;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint Sequence;
        [FieldOffset(40)] public uint Reserved0;
        [FieldOffset(44)] public uint Reserved1;
    }

    /// <summary>
    /// Fixed-size replay block header followed by 300 input frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct LockstepReplayBlockHeader
    {
        [FieldOffset(0)] public ulong Magic;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint HeaderSizeBytes;
        [FieldOffset(16)] public uint StartFrame;
        [FieldOffset(20)] public uint HashFrame;
        [FieldOffset(24)] public uint InputCount;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong MasterHash;
        [FieldOffset(40)] public uint RigidbodyHash;
        [FieldOffset(44)] public uint PlayerHash;
        [FieldOffset(48)] public uint RoomHash;
        [FieldOffset(52)] public uint EntityHash;
        [FieldOffset(56)] public uint RigidbodyCount;
        [FieldOffset(60)] public uint PlayerCount;
        [FieldOffset(64)] public uint RoomCount;
        [FieldOffset(68)] public uint EntityCount;
        [FieldOffset(72)] public uint MissingMask;
        [FieldOffset(76)] public uint NonFiniteMask;
        [FieldOffset(80)] public uint BlockSequence;
        [FieldOffset(84)] public uint HashCadenceFrames;
        [FieldOffset(88)] public ulong Reserved1;
        [FieldOffset(96)] public ulong Reserved2;
        [FieldOffset(104)] public ulong Reserved3;
        [FieldOffset(112)] public ulong Reserved4;
        [FieldOffset(120)] public ulong Reserved5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct LockstepArrayHash
    {
        [FieldOffset(0)] public uint CategoryId;
        [FieldOffset(4)] public uint Hash;
        [FieldOffset(8)] public uint Count;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint FirstElementHash;
        [FieldOffset(20)] public uint LastElementHash;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct LockstepTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint HashLo;
        [FieldOffset(8)] public uint HashHi;
        [FieldOffset(12)] public uint RigidbodyHash;
        [FieldOffset(16)] public uint PlayerHash;
        [FieldOffset(20)] public uint RoomHash;
        [FieldOffset(24)] public uint EntityHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint RigidbodyCount;
        [FieldOffset(36)] public uint PlayerCount;
        [FieldOffset(40)] public uint RoomCount;
        [FieldOffset(44)] public uint EntityCount;
        [FieldOffset(48)] public uint MissingMask;
        [FieldOffset(52)] public uint NonFiniteMask;
        [FieldOffset(56)] public uint ReplayBlock;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct LockstepMasterHashHistoryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint HashLo;
        [FieldOffset(8)] public uint HashHi;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint MissingMask;
        [FieldOffset(20)] public uint NonFiniteMask;
        [FieldOffset(24)] public uint ReplayBlock;
        [FieldOffset(28)] public uint Reserved0;
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
    public sealed unsafe class LockstepStateValidator : MonoBehaviour, IPostFixedTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001LockstepStateValidatorSignalPushDropCount;
        private const int HashCadenceFrames = 300;
        private const int PrecisionHashCadenceFrames = 60;
        private const int HighStressHashCadenceFrames = 1200;
        private const int ReplayInputFrameCapacity = 300;
        private const int TelemetryFrameCapacity = 300;
        private const int MasterHashHistoryCapacity = 10;
        private const int PlayerKinematicStateBytes = 64;
        private const int PlayerKinematicPositionAupOffset = 0;
        private const int PlayerKinematicVelocityOffset = 24;
        private const int PlayerKinematicInputVectorOffset = 36;
        private const int PlayerKinematicFrameOffset = 48;
        private const int PlayerKinematicFlagsOffset = 52;
        private const int PlayerKinematicInputActionsOffset = 56;
        private const int ReplayHeaderBytes = 128;
        private const int ReplayInputBytes = 48;
        private const int ArrayHashBytes = 32;
        private const int TelemetryEntryBytes = 64;
        private const int MasterHashHistoryEntryBytes = 32;
        private const int SignalPayloadBytes = 32;
        private const int LockstepBlackBoxHeaderBytes = 32;
        private const int LockstepSnapshotSignalCapacity = 16;
        private const int SystemGlitchSignalCapacity = 8;
        private const int MaxHashElements = 8192;
        private const int RoomWaterLevelsMirrorCapacity = 256;
        private const int MaxGhostReplayBlocks = 128;
        private const int ReplayBlockBytes = ReplayHeaderBytes + (ReplayInputFrameCapacity * ReplayInputBytes);
        private const ulong ReplayMagic = 0x48384C4F434B5354ul;
        private const ulong LockstepBlackBoxMagic = 0x504D5544534C3848ul;
        private const uint ReplayVersion = 2u;
        private const uint LockstepBlackBoxVersion = 1u;
        private const uint StablePlayerId = 0x504C5952u;
        private const uint ReasonDesyncHash = 0x4453594Eu;
        private const uint ReasonGhostReplayHash = 0x47525350u;
        private const uint LockstepSnapshotLaneHash = 0x4C535348u;
        private const uint SystemGlitchLaneHash = 0x5359474Cu;
        private const string LockstepBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1403_LOCKSTEP_STATE_VALIDATOR.bin";
        private const uint TelemetryFlagHashExecuted = 1u << 0;
        private const uint TelemetryFlagMissingData = 1u << 1;
        private const uint TelemetryFlagTruncated = 1u << 2;
        private const uint TelemetryFlagNonFinite = 1u << 3;
        private const uint TelemetryFlagReplayMode = 1u << 4;
        private const uint TelemetryFlagDesync = 1u << 6;
        private const uint TelemetryFlagLayoutInvalid = 1u << 8;
        private const uint ArrayFlagMissing = 1u << 0;
        private const uint ArrayFlagTruncated = 1u << 1;
        private const uint ArrayFlagNonFinite = 1u << 2;
        private const uint ReplayInputFlagNonFinite = 1u << 31;
        private const uint PlayerStateFlagNonFinite = 1u << 31;
        private const float DesyncGlitchIntensity01 = 1f;
        private const float DesyncGlitchDurationSeconds = 1f;

        private static LockstepStateValidator _activeInstance;

        private readonly byte[] _replayReadScratch = new byte[ReplayBlockBytes]; // COLD ALLOC: byte[14528] - replay block load buffer - owner: LockstepStateValidator
        private IDataVault _dataVault;
        private IPlayerRuntimeContext _player;
        private IHabitatGraphService _habitat;
        private SystemDispatcher _dispatcher;
        private float _cachedQualityWeight01 = 1f;
        private uint _postSimulationFrame;
        private uint _lastReplayBlockSequence;
        private uint _lastMasterHashLo;
        private uint _lastMasterHashHi;
        private int _telemetryWriteIndex;
        private int _inputWriteIndex;
        private int _inputFrameCount;
        private int _registeredPostFixed;
        private int _registeredHotSwap;
        private int _binaryLayoutInvalid;
        private int _binaryLayoutDumped;
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
                return TryGetReadVaultBuffer(BufferID.LockstepMasterStateHash, 1, out NativeArray<ulong>.ReadOnly masterHash)
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
            _binaryLayoutInvalid = ValidateBinaryLayout() ? 0 : 1;
            _binaryLayoutDumped = 0;
            if (_binaryLayoutInvalid != 0)
                GlobalTelemetryBus.PublishModTelemetry(ReasonDesyncHash, 0x4C41594Fu, 0u);
            EnsureNativeState();
            EnsureHashNativeState();
            EnsurePostFixedMirrorState();
            RestoreTelemetryCursorFromVault();
            EnsureReplayWriterCold();
            TryRegisterHotSwapListener();
            if (_dispatcher != null && GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Core))
                _registeredPostFixed = 1;
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            if (_registeredPostFixed != 0)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
                _registeredPostFixed = 0;
            }

            DisposeNativeState();
            if (ReferenceEquals(_activeInstance, this))
                _activeInstance = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcher = currentService as SystemDispatcher;
                    if (_registeredPostFixed != 0)
                    {
                        GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Core);
                        _registeredPostFixed = 0;
                    }

                    if (currentService != null && GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Core))
                        _registeredPostFixed = 1;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _dataVault = currentService as IDataVault;
                    EnsureNativeState();
                    EnsureHashNativeState();
                    EnsurePostFixedMirrorState();
                    RestoreTelemetryCursorFromVault();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _player = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.Logistics:
                    IHabitatGraphService habitat = currentService as IHabitatGraphService;
                    if (habitat != null)
                    {
                        _habitat = habitat;
                        EnsurePostFixedMirrorState();
                    }
                    break;
            }
        }

        /// <summary>
        /// Dispatcher POST_SIMULATION tick entrypoint.
        /// </summary>
        public void PostFixedTick(float fixedDeltaTime)
        {
            uint frame = ++_postSimulationFrame;
            bool ghostReplayActive = Volatile.Read(ref _ghostReplayActive) != 0;
            uint flags = ghostReplayActive ? TelemetryFlagReplayMode : 0u;
            if (_binaryLayoutInvalid != 0)
            {
                flags |= TelemetryFlagMissingData | TelemetryFlagLayoutInvalid;
                _lastMasterHashLo = 0u;
                _lastMasterHashHi = 0u;
                WriteTelemetry(frame, flags);
                if (Interlocked.Exchange(ref _binaryLayoutDumped, 1) == 0)
                    DumpBlackBox();
                return;
            }

            InputStateSignal inputSignal = default;
            bool hasInputSignal = false;
            if (ghostReplayActive)
            {
                bool replayInputFaulted = ApplyGhostReplayInput(frame);
                if (replayInputFaulted)
                {
                    flags |= TelemetryFlagDesync;
                    WriteTelemetry(frame, flags);
                    DumpBlackBox();
                    return;
                }
            }
            else
            {
                hasInputSignal = CaptureInputFrame(frame, out inputSignal);
            }

            int hashCadenceFrames = ResolveHashCadenceFrames();
            if ((frame % (uint)hashCadenceFrames) != 0u)
            {
                WriteTelemetry(frame, flags);
                return;
            }

            flags |= TelemetryFlagHashExecuted;
            MirrorPlayerStateToVault(frame, hasInputSignal, in inputSignal);
            bool roomWaterHadNonFinite = MirrorRoomWaterLevelsToVault();
            ExecuteHashJobs(frame, hashCadenceFrames, roomWaterHadNonFinite, ref flags);
            bool replayFaulted = ValidateReplayHash(frame, ref flags);
            if (replayFaulted)
            {
                WriteTelemetry(frame, flags);
                DumpBlackBox();
                if ((flags & TelemetryFlagNonFinite) != 0u)
                    ThrowFatalDesync(frame, flags);
                return;
            }

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
            CoreDeterminismSignals.ClearInputOverride();
            validator._dispatcher?.RequestTimeDilation(1f, ReasonGhostReplayHash);
        }

        private void RefreshDependenciesFromRegistry()
        {
            _dataVault = GlobalRegistry.DataVault;
            _player = GlobalRegistry.Player;
            _habitat = GlobalRegistry.HabitatGraph;
            _dispatcher = GlobalRegistry.Dispatcher;
            RefreshCachedQualityWeight01();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap != 0 || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this) ? 1 : 0;
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwap == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = 0;
        }

        private static void ConfigureSignalLanes()
        {
            SignalCorridorRuntime.EnsureInitialized();
            SignalBus<LockstepSnapshotSignal>.EnsureInitialized();
            SignalBus<SystemGlitchSignal>.EnsureInitialized();
        }

        private static bool ValidateBinaryLayout()
        {
            return UnsafeUtility.SizeOf<LockstepPlayerKinematicState>() == PlayerKinematicStateBytes &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.PositionAup)) == PlayerKinematicPositionAupOffset &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Velocity)) == PlayerKinematicVelocityOffset &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputVector)) == PlayerKinematicInputVectorOffset &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Frame)) == PlayerKinematicFrameOffset &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.Flags)) == PlayerKinematicFlagsOffset &&
                FieldOffsetOf<LockstepPlayerKinematicState>(nameof(LockstepPlayerKinematicState.InputActions)) == PlayerKinematicInputActionsOffset &&
                UnsafeUtility.SizeOf<LockstepReplayBlockHeader>() == ReplayHeaderBytes &&
                UnsafeUtility.SizeOf<LockstepReplayInputFrame>() == ReplayInputBytes &&
                UnsafeUtility.SizeOf<LockstepArrayHash>() == ArrayHashBytes &&
                UnsafeUtility.SizeOf<LockstepTelemetryEntry>() == TelemetryEntryBytes &&
                UnsafeUtility.SizeOf<LockstepMasterHashHistoryEntry>() == MasterHashHistoryEntryBytes &&
                UnsafeUtility.SizeOf<LockstepSnapshotSignal>() == SignalPayloadBytes &&
                UnsafeUtility.SizeOf<SystemGlitchSignal>() == SignalPayloadBytes;
        }

        private static int FieldOffsetOf<T>(string fieldName)
            where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        private int ResolveHashCadenceFrames()
        {
            float qualityWeight01 = RefreshCachedQualityWeight01();
            float systemStress01 = ResolveSystemStress01();
            float qualityCurve01 = SmoothStep01(qualityWeight01);
            float stressCurve01 = SmoothStep01(systemStress01);
            float qualityCadenceFrames = math.lerp(HashCadenceFrames, PrecisionHashCadenceFrames, qualityCurve01);
            float cadenceFrames = math.lerp(qualityCadenceFrames, HighStressHashCadenceFrames, stressCurve01);
            return math.clamp((int)math.round(cadenceFrames), PrecisionHashCadenceFrames, HighStressHashCadenceFrames);
        }

        private float RefreshCachedQualityWeight01()
        {
            _cachedQualityWeight01 = ResolveGlobalQualityWeight01();
            return _cachedQualityWeight01;
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(qualityWeight) ? math.saturate(qualityWeight) : 1.0f;
        }

        private static float ResolveSystemStress01()
        {
            float systemStress01 = HomeostasisBrain.SystemHealthIndex01;
            return math.isfinite(systemStress01) ? math.saturate(systemStress01) : 1.0f;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3.0f - (2.0f * t));
        }

        private bool CaptureInputFrame(uint frame, out InputStateSignal signal)
        {
            signal = default;
            NativeArray<LockstepReplayInputFrame> inputRing = OpenOrAcquireVaultBufferView<LockstepReplayInputFrame>(
                BufferID.LockstepReplayInputRing,
                ReplayInputFrameCapacity,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            if (!inputRing.IsCreated)
                return false;

            LockstepReplayInputFrame replayInput = default;
            replayInput.Frame = frame;
            bool hasInputSignal = TryGetLatestInputStateSignal(out signal);
            if (hasInputSignal)
            {
                InputState state = signal.State;
                uint replayFlags = state.Flags;
                float2 moveDelta = new float2(
                    state.MoveX * InputState.AxisInvQuantizeScale,
                    state.MoveY * InputState.AxisInvQuantizeScale);
                float2 lookDelta = new float2(
                    state.LookX * InputState.LookInvQuantizeScale,
                    state.LookY * InputState.LookInvQuantizeScale);
                replayInput.ActionsBitmask = state.ButtonsBitmask;
                replayInput.MoveDelta = SanitizeReplayInput(moveDelta, float2.zero, ref replayFlags);
                replayInput.LookDelta = SanitizeReplayInput(lookDelta, float2.zero, ref replayFlags);
                replayInput.VerticalDelta = SanitizeReplayInput(state.Vertical * InputState.AxisInvQuantizeScale, 0f, ref replayFlags);
                replayInput.CurrentInputSchemeHash = signal.CurrentInputSchemeHash;
                replayInput.Flags = replayFlags;
                replayInput.Sequence = state.Sequence;
            }

            int index = _inputWriteIndex;
            if ((uint)index >= ReplayInputFrameCapacity)
                index = 0;

            inputRing[index] = replayInput;
            _inputWriteIndex = (index + 1) % ReplayInputFrameCapacity;
            if (_inputFrameCount < ReplayInputFrameCapacity)
                _inputFrameCount++;
            return hasInputSignal;
        }

        private bool ApplyGhostReplayInput(uint frame)
        {
            if (Volatile.Read(ref _ghostReplayActive) == 0)
                return false;

            int ghostInputCursor = _ghostInputCursor;
            if (ghostInputCursor < 0 || ghostInputCursor >= _ghostInputCount)
            {
                ReportGhostInputFrameMismatch(frame);
                return true;
            }

            if (!TryGetVaultBuffer(BufferID.LockstepGhostReplayInputs, ghostInputCursor + 1, out NativeArray<LockstepReplayInputFrame> ghostInputs) ||
                ghostInputCursor >= ghostInputs.Length)
            {
                ReportGhostInputFrameMismatch(frame);
                return true;
            }

            LockstepReplayInputFrame ghost = ghostInputs[ghostInputCursor];
            if (ghost.Frame != frame)
            {
                ReportGhostInputFrameMismatch(frame);
                return true;
            }

            _ghostInputCursor = ghostInputCursor + 1;
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
            CoreDeterminismSignals.TryPublishInputOverride(in state, SystemDispatcher.CurrentFrameId);
            return false;
        }

        private void MirrorPlayerStateToVault(uint frame, bool hasInputSignal, in InputStateSignal inputSignal)
        {
            NativeArray<LockstepPlayerKinematicState> buffer = OpenOrAcquireVaultBufferView<LockstepPlayerKinematicState>(
                BufferID.PlayerKinematicState,
                1,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            if (!buffer.IsCreated)
                return;

            LockstepPlayerKinematicState state = default;
            state.Frame = frame;
            IPlayerRuntimeContext player = _player;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                state.Flags = pose.Flags;
                double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
                state.PositionAup = new double3(
                    (pose.Aup.GridX * cellSize) + pose.Aup.LocalX,
                    (pose.Aup.GridY * cellSize) + pose.Aup.LocalY,
                    (pose.Aup.GridZ * cellSize) + pose.Aup.LocalZ);
                if (!math.all(math.isfinite(state.PositionAup)))
                {
                    state.PositionAup = double3.zero;
                    state.Flags |= PlayerStateFlagNonFinite;
                }

                state.InputVector = SanitizeFinite(pose.Forward, new float3(0f, 0f, 1f), ref state.Flags);
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

        private bool MirrorRoomWaterLevelsToVault()
        {
            IHabitatGraphService habitat = _habitat;
            if (habitat == null || !habitat.IsInitialized)
                return false;

            NativeArray<float>.ReadOnly source = habitat.RoomWaterLevels;
            int count = math.min(math.min(habitat.RoomCount, source.Length), RoomWaterLevelsMirrorCapacity);
            if (count <= 0)
                return false;

            NativeArray<float> destination = OpenOrAcquireVaultBufferView<float>(
                BufferID.RoomWaterLevels,
                RoomWaterLevelsMirrorCapacity,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            if (!destination.IsCreated)
                return false;

            bool nonFinite = false;
            for (int i = 0; i < count; i++)
            {
                float value = source[i];
                bool finite = math.isfinite(value);
                nonFinite |= !finite;
                destination[i] = finite ? value : 0f;
            }

            return nonFinite;
        }

        private void ExecuteHashJobs(uint frame, int hashCadenceFrames, bool roomWaterHadNonFinite, ref uint telemetryFlags)
        {
            NativeArray<uint> rigidbodyElementHashes = OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepRigidbodyElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<uint> playerElementHashes = OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepPlayerElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<uint> roomElementHashes = OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepRoomElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<uint> entityElementHashes = OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepEntityElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<byte> rigidbodyElementFlags = OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepRigidbodyElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<byte> playerElementFlags = OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepPlayerElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<byte> roomElementFlags = OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepRoomElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<byte> entityElementFlags = OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepEntityElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory, allowColdInitialization: false);
            NativeArray<LockstepArrayHash> arrayHashes = OpenOrAcquireVaultBufferView<LockstepArrayHash>(BufferID.LockstepArrayHashes, (int)LockstepHashCategory.Count, NativeArrayOptions.ClearMemory, allowColdInitialization: false);
            NativeArray<ulong> masterHash = OpenOrAcquireVaultBufferView<ulong>(BufferID.LockstepMasterStateHash, 1, NativeArrayOptions.ClearMemory, allowColdInitialization: false);
            NativeArray<uint> masterFlags = OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepMasterFlags, 1, NativeArrayOptions.ClearMemory, allowColdInitialization: false);

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
                TryGetHashSourceBuffer(BufferID.RigidbodyAUPs, out rigidbodyAups);
                TryGetHashSourceBuffer(BufferID.PlayerKinematicState, out playerStates);
                TryGetHashSourceBuffer(BufferID.RoomWaterLevels, out roomWaterLevels);
                TryGetHashSourceBuffer(BufferID.EntityAUPs, out entityAups);
            }

            int rigidbodyCount = ResolveHashCount(rigidbodyAups, ref telemetryFlags, out bool rigidbodyTruncated);
            int playerCount = ResolveHashCount(playerStates, ref telemetryFlags, out bool playerTruncated);
            int roomCount = ResolveRoomHashCount(roomWaterLevels, ref telemetryFlags, out bool roomTruncated);
            int entityCount = ResolveHashCount(entityAups, ref telemetryFlags, out bool entityTruncated);

            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.RigidbodyAups, rigidbodyCount, rigidbodyAups.IsCreated, rigidbodyTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.PlayerKinematicState, playerCount, playerStates.IsCreated, playerTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.RoomWaterLevels, roomCount, roomWaterLevels.IsCreated, roomTruncated);
            SetDefaultArrayHash(arrayHashes, LockstepHashCategory.EntityAups, entityCount, entityAups.IsCreated, entityTruncated);
            if (roomWaterHadNonFinite)
            {
                LockstepArrayHash roomHash = arrayHashes[(int)LockstepHashCategory.RoomWaterLevels];
                roomHash.Flags |= ArrayFlagNonFinite;
                arrayHashes[(int)LockstepHashCategory.RoomWaterLevels] = roomHash;
            }

            HashDouble3ArrayDirect(
                rigidbodyAups,
                rigidbodyElementHashes,
                rigidbodyElementFlags,
                arrayHashes,
                LockstepHashCategory.RigidbodyAups,
                rigidbodyCount);
            HashPlayerKinematicArrayDirect(playerStates, playerElementHashes, playerElementFlags, arrayHashes, playerCount);
            HashFloatArrayDirect(roomWaterLevels, roomElementHashes, roomElementFlags, arrayHashes, roomCount);
            HashFloat3ArrayDirect(
                entityAups,
                entityElementHashes,
                entityElementFlags,
                arrayHashes,
                LockstepHashCategory.EntityAups,
                entityCount);
            BuildMasterStateHashDirect(arrayHashes, masterHash, masterFlags, frame);

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
            uint combinedFlags = telemetryFlags;
            if ((combinedFlags & (TelemetryFlagMissingData | TelemetryFlagTruncated | TelemetryFlagNonFinite)) == 0u)
                RecordMasterHashHistory(frame, master, combinedFlags, arrayHashes);
            GlobalTelemetryBus.PublishModTelemetry(ReasonDesyncHash, _lastMasterHashLo, _lastMasterHashHi);
            PublishLockstepSnapshot(frame, master, hashCadenceFrames, combinedFlags, arrayHashes);
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

            int sourceLength = source.Length;
            if (sourceLength > MaxHashElements)
            {
                truncated = true;
                telemetryFlags |= TelemetryFlagTruncated;
            }

            return math.select(sourceLength, MaxHashElements, sourceLength > MaxHashElements);
        }

        private int ResolveRoomHashCount(NativeArray<float> source, ref uint telemetryFlags, out bool truncated)
        {
            truncated = false;
            if (!source.IsCreated)
            {
                telemetryFlags |= TelemetryFlagMissingData;
                return 0;
            }

            IHabitatGraphService habitat = _habitat;
            if (habitat == null || !habitat.IsInitialized || habitat.RoomCount <= 0)
            {
                telemetryFlags |= TelemetryFlagMissingData;
                return 0;
            }

            int count = math.min(math.min(source.Length, habitat.RoomCount), RoomWaterLevelsMirrorCapacity);
            if (habitat.RoomCount > RoomWaterLevelsMirrorCapacity)
            {
                truncated = true;
                telemetryFlags |= TelemetryFlagTruncated;
            }

            if (count > MaxHashElements)
            {
                truncated = true;
                telemetryFlags |= TelemetryFlagTruncated;
            }

            return math.select(count, MaxHashElements, count > MaxHashElements);
        }

        private static int ResolveDirectHashCount<T>(NativeArray<T> source, int count)
            where T : struct
        {
            int sourceLength = source.IsCreated ? source.Length : 0;
            int positiveCount = math.select(0, count, count > 0);
            int boundedCount = math.min(positiveCount, sourceLength);
            return math.select(0, boundedCount, source.IsCreated && boundedCount > 0);
        }

        private static void HashFloat3ArrayDirect(
            NativeArray<float3> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            LockstepHashCategory category,
            int count)
        {
            int hashCount = ResolveDirectHashCount(source, count);
            for (int i = 0; i < hashCount; i++)
            {
                float3 value = source[i];
                bool finite = math.all(math.isfinite(value));
                uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)category);
                hash = LockstepHashMath.Fnv1A(hash, i);
                hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, value) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
                elementHashes[i] = hash;
                elementFlags[i] = finite ? (byte)0 : (byte)1;
            }

            CombineElementHashesDirect(elementHashes, elementFlags, arrayHashes, (int)category, hashCount);
        }

        private static void HashDouble3ArrayDirect(
            NativeArray<double3> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            LockstepHashCategory category,
            int count)
        {
            int hashCount = ResolveDirectHashCount(source, count);
            for (int i = 0; i < hashCount; i++)
            {
                double3 value = source[i];
                bool finite = math.all(math.isfinite(value));
                uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)category);
                hash = LockstepHashMath.Fnv1A(hash, i);
                hash = finite ? LockstepHashMath.Fnv1ADouble3(hash, value) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
                elementHashes[i] = hash;
                elementFlags[i] = finite ? (byte)0 : (byte)1;
            }

            CombineElementHashesDirect(elementHashes, elementFlags, arrayHashes, (int)category, hashCount);
        }

        private static void HashFloatArrayDirect(
            NativeArray<float> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            int count)
        {
            int hashCount = ResolveDirectHashCount(source, count);
            for (int i = 0; i < hashCount; i++)
            {
                float value = source[i];
                bool finite = math.isfinite(value);
                uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)LockstepHashCategory.RoomWaterLevels);
                hash = LockstepHashMath.Fnv1A(hash, i);
                hash = LockstepHashMath.Fnv1A(hash, finite ? LockstepHashMath.QuantizeWaterLevel(value) : 0xBADF10A7u);
                elementHashes[i] = hash;
                elementFlags[i] = finite ? (byte)0 : (byte)1;
            }

            CombineElementHashesDirect(elementHashes, elementFlags, arrayHashes, (int)LockstepHashCategory.RoomWaterLevels, hashCount);
        }

        private static void HashPlayerKinematicArrayDirect(
            NativeArray<LockstepPlayerKinematicState> source,
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            int count)
        {
            int hashCount = ResolveDirectHashCount(source, count);
            for (int i = 0; i < hashCount; i++)
            {
                LockstepPlayerKinematicState state = source[i];
                bool finite =
                    math.all(math.isfinite(state.PositionAup)) &&
                    math.all(math.isfinite(state.Velocity)) &&
                    math.all(math.isfinite(state.InputVector)) &&
                    (state.Flags & LockstepHashMath.NonFiniteSourceFlag) == 0u;

                uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)LockstepHashCategory.PlayerKinematicState);
                hash = LockstepHashMath.Fnv1A(hash, i);
                hash = finite ? LockstepHashMath.Fnv1ADouble3(hash, state.PositionAup) : LockstepHashMath.Fnv1A(hash, 0xBADF10A7u);
                hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, state.Velocity) : hash;
                hash = finite ? LockstepHashMath.Fnv1AFloat3(hash, state.InputVector) : hash;
                hash = LockstepHashMath.Fnv1A(hash, state.Frame);
                hash = LockstepHashMath.Fnv1A(hash, state.Flags);
                hash = LockstepHashMath.Fnv1A(hash, state.InputActions);
                hash = LockstepHashMath.Fnv1A(hash, state.StableId);
                elementHashes[i] = hash;
                elementFlags[i] = finite ? (byte)0 : (byte)1;
            }

            CombineElementHashesDirect(elementHashes, elementFlags, arrayHashes, (int)LockstepHashCategory.PlayerKinematicState, hashCount);
        }

        private static void CombineElementHashesDirect(
            NativeArray<uint> elementHashes,
            NativeArray<byte> elementFlags,
            NativeArray<LockstepArrayHash> arrayHashes,
            int categoryIndex,
            int count)
        {
            uint hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)categoryIndex);
            uint flags = 0u;
            uint first = 0u;
            uint last = 0u;
            for (int i = 0; i < count; i++)
            {
                uint elementHash = elementHashes[i];
                if (i == 0)
                    first = elementHash;
                last = elementHash;
                hash = LockstepHashMath.Fnv1A(hash, elementHash);
                if (elementFlags[i] != 0)
                    flags |= 1u;
            }

            LockstepArrayHash arrayHash = arrayHashes[categoryIndex];
            arrayHash.Hash = hash;
            arrayHash.Count = (uint)count;
            arrayHash.FirstElementHash = first;
            arrayHash.LastElementHash = last;
            if (flags != 0u)
                arrayHash.Flags |= ArrayFlagNonFinite;
            arrayHashes[categoryIndex] = arrayHash;
        }

        private static void BuildMasterStateHashDirect(
            NativeArray<LockstepArrayHash> arrayHashes,
            NativeArray<ulong> masterHash,
            NativeArray<uint> masterFlags,
            uint frame)
        {
            uint flags = 0u;
            for (int i = 0; i < (int)LockstepHashCategory.Count; i++)
                flags |= arrayHashes[i].Flags;

            masterHash[0] = LockstepHashMath.BuildMasterHash(arrayHashes, frame);
            masterFlags[0] = flags;
        }

        private void SetDefaultArrayHash(NativeArray<LockstepArrayHash> arrayHashes, LockstepHashCategory category, int count, bool present, bool truncated)
        {
            uint flags = present ? 0u : ArrayFlagMissing;
            if (present && truncated)
                flags |= ArrayFlagTruncated;

            int safeCount = math.select(0, count, count > 0);
            arrayHashes[(int)category] = new LockstepArrayHash
            {
                CategoryId = (uint)category,
                Hash = LockstepHashMath.Fnv1A(LockstepHashMath.FnvOffset32, (uint)category),
                Count = (uint)safeCount,
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
            SignalBus<LockstepSnapshotSignal>.TryPushTracked(in signal, ref s_x001LockstepStateValidatorSignalPushDropCount);
        }

        private void RecordMasterHashHistory(
            uint frame,
            ulong masterHash,
            uint flags,
            NativeArray<LockstepArrayHash> arrayHashes)
        {
            NativeArray<LockstepMasterHashHistoryEntry> history = OpenOrAcquireVaultBufferView<LockstepMasterHashHistoryEntry>(
                BufferID.LockstepMasterHashHistory,
                MasterHashHistoryCapacity,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            NativeArray<int> cursor = OpenOrAcquireVaultBufferView<int>(
                BufferID.LockstepMasterHashHistoryCursor,
                1,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            if (!HasRequiredLength(history, MasterHashHistoryCapacity) || !HasRequiredLength(cursor, 1))
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

        private bool ValidateReplayHash(uint frame, ref uint telemetryFlags)
        {
            if (Volatile.Read(ref _ghostReplayActive) == 0 ||
                !TryGetReadVaultBuffer(BufferID.LockstepGhostReplayHeaders, out NativeArray<LockstepReplayBlockHeader>.ReadOnly ghostHeaders) ||
                !TryGetReadVaultBuffer(BufferID.LockstepMasterStateHash, 1, out NativeArray<ulong>.ReadOnly masterHash))
                return false;

            int blockIndex = _ghostExpectedBlockIndex;
            if (blockIndex < 0 || blockIndex >= ghostHeaders.Length)
                return false;

            LockstepReplayBlockHeader expected = ghostHeaders[blockIndex];
            if (expected.Magic != ReplayMagic)
                return false;

            if (expected.HashFrame != frame)
            {
                telemetryFlags |= TelemetryFlagDesync;
                ReportDesync(frame, in expected, 2u);
                _ghostExpectedBlockIndex = blockIndex + 1;
                return true;
            }

            if (expected.MasterHash == masterHash[0])
            {
                _ghostExpectedBlockIndex = blockIndex + 1;
                return false;
            }

            telemetryFlags |= TelemetryFlagDesync;
            ReportDesync(frame, in expected, 1u);
            _ghostExpectedBlockIndex = blockIndex + 1;
            return true;
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
            CoreDeterminismSignals.TryPublish(in signal);
            PublishSystemGlitchSignal(frame, in expected, (byte)flags);
            _dispatcher?.RequestSimulationPause(true, ReasonDesyncHash);
            StopGhostReplayAfterFault();
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
            SignalBus<SystemGlitchSignal>.TryPushTracked(in signal, ref s_x001LockstepStateValidatorSignalPushDropCount);
            SystemDispatcher.RequestVisualStaticGlitch(DesyncGlitchDurationSeconds);
        }

        private void ReportGhostInputFrameMismatch(uint frame)
        {
            LockstepReplayBlockHeader expected = default;
            int blockIndex = _ghostExpectedBlockIndex;
            if (TryGetReadVaultBuffer(BufferID.LockstepGhostReplayHeaders, out NativeArray<LockstepReplayBlockHeader>.ReadOnly ghostHeaders) &&
                blockIndex >= 0 &&
                blockIndex < ghostHeaders.Length)
            {
                expected = ghostHeaders[blockIndex];
            }

            if (expected.Magic != ReplayMagic)
            {
                expected.Magic = ReplayMagic;
                expected.HashFrame = frame;
                expected.MasterHash = TryGetReadVaultBuffer(BufferID.LockstepMasterStateHash, 1, out NativeArray<ulong>.ReadOnly masterHash) ? masterHash[0] : 0UL;
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
            CoreDeterminismSignals.ClearInputOverride();
        }

        private void StageReplayWrite(uint frame, int hashCadenceFrames, ref uint telemetryFlags)
        {
            if (Volatile.Read(ref _ghostReplayActive) != 0)
                return;

            if (_inputFrameCount < ReplayInputFrameCapacity)
                return;
        }

        private static uint BuildCategoryMask(NativeArray<LockstepArrayHash> arrayHashes, uint flag)
        {
            if (!HasRequiredLength(arrayHashes, (int)LockstepHashCategory.Count))
                return 0u;

            uint mask = 0u;
            for (int i = 0; i < (int)LockstepHashCategory.Count; i++)
            {
                if ((arrayHashes[i].Flags & flag) != 0u)
                    mask |= 1u << i;
            }

            return mask;
        }

        private static uint BuildCategoryMask(NativeArray<LockstepArrayHash>.ReadOnly arrayHashes, uint flag)
        {
            if (!HasRequiredLength(arrayHashes, (int)LockstepHashCategory.Count))
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
            NativeArray<LockstepTelemetryEntry> telemetryRing = OpenOrAcquireVaultBufferView<LockstepTelemetryEntry>(
                BufferID.LockstepTelemetryRing,
                TelemetryFrameCapacity,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            if (!telemetryRing.IsCreated)
                return;

            NativeArray<LockstepArrayHash> arrayHashes = OpenOrAcquireVaultBufferView<LockstepArrayHash>(
                BufferID.LockstepArrayHashes,
                (int)LockstepHashCategory.Count,
                NativeArrayOptions.ClearMemory,
                allowColdInitialization: false);
            LockstepArrayHash rigidbody = ReadArrayHash(arrayHashes, LockstepHashCategory.RigidbodyAups);
            LockstepArrayHash player = ReadArrayHash(arrayHashes, LockstepHashCategory.PlayerKinematicState);
            LockstepArrayHash room = ReadArrayHash(arrayHashes, LockstepHashCategory.RoomWaterLevels);
            LockstepArrayHash entity = ReadArrayHash(arrayHashes, LockstepHashCategory.EntityAups);
            int index = _telemetryWriteIndex;
            if ((uint)index >= TelemetryFrameCapacity)
                index = 0;

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
            int index = (int)category;
            return HasRequiredLength(arrayHashes, index + 1) ? arrayHashes[index] : default;
        }

        private void RestoreTelemetryCursorFromVault()
        {
            _telemetryWriteIndex = 0;
            if (!TryGetReadVaultBuffer(BufferID.LockstepTelemetryRing, out NativeArray<LockstepTelemetryEntry>.ReadOnly telemetryRing))
                return;

            int count = math.min(TelemetryFrameCapacity, telemetryRing.Length);
            if (count <= 0)
                return;

            uint newestFrame = 0u;
            int newestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                uint frame = telemetryRing[i].Frame;
                if (frame == 0u)
                    continue;

                if (newestIndex < 0 || IsFrameNewer(frame, newestFrame))
                {
                    newestFrame = frame;
                    newestIndex = i;
                }
            }

            if (newestIndex < 0)
                return;

            _postSimulationFrame = newestFrame;
            _telemetryWriteIndex = (newestIndex + 1) % count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFrameNewer(uint candidate, uint current)
        {
            return candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void DumpBlackBox()
        {
            if (!TryGetReadVaultBuffer(BufferID.LockstepTelemetryRing, out NativeArray<LockstepTelemetryEntry>.ReadOnly telemetryRing))
                return;

            int entryCount = math.min(TelemetryFrameCapacity, telemetryRing.Length);
            if (entryCount <= 0)
                return;

            NativeArray<byte> payload = default;
            const string dumpPayloadLabel = "lockstepStateValidatorBlackBoxDumpPayload";
            try
            {
                int byteCount = LockstepBlackBoxHeaderBytes + (entryCount * TelemetryEntryBytes);
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(LockstepStateValidator),
                    dumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);
                int cursor = 0;
                ulong masterHash = ((ulong)_lastMasterHashHi << 32) | _lastMasterHashLo;

                WriteUInt64LittleEndian(payload, ref cursor, LockstepBlackBoxMagic);
                WriteUInt32LittleEndian(payload, ref cursor, LockstepBlackBoxVersion);
                WriteUInt32LittleEndian(payload, ref cursor, (uint)entryCount);
                WriteUInt32LittleEndian(payload, ref cursor, TelemetryEntryBytes);
                WriteUInt32LittleEndian(payload, ref cursor, (uint)_telemetryWriteIndex);
                WriteUInt64LittleEndian(payload, ref cursor, masterHash);

                int readStart = _telemetryWriteIndex;
                if ((uint)readStart >= (uint)entryCount)
                    readStart = 0;

                for (int i = 0; i < entryCount; i++)
                {
                    int index = readStart + i;
                    if (index >= entryCount)
                        index -= entryCount;

                    WriteLockstepTelemetryEntry(payload, ref cursor, telemetryRing[index]);
                }

                NativeFaultDumpWriter.TryWriteAll(LockstepBlackBoxDumpRelativePath, payload, cursor);
            }
            catch (Exception)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(LockstepStateValidator), dumpPayloadLabel);
            }
        }

        private static void WriteLockstepTelemetryEntry(NativeArray<byte> target, ref int cursor, in LockstepTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(target, ref cursor, entry.Frame);
            WriteUInt32LittleEndian(target, ref cursor, entry.HashLo);
            WriteUInt32LittleEndian(target, ref cursor, entry.HashHi);
            WriteUInt32LittleEndian(target, ref cursor, entry.RigidbodyHash);
            WriteUInt32LittleEndian(target, ref cursor, entry.PlayerHash);
            WriteUInt32LittleEndian(target, ref cursor, entry.RoomHash);
            WriteUInt32LittleEndian(target, ref cursor, entry.EntityHash);
            WriteUInt32LittleEndian(target, ref cursor, entry.Flags);
            WriteUInt32LittleEndian(target, ref cursor, entry.RigidbodyCount);
            WriteUInt32LittleEndian(target, ref cursor, entry.PlayerCount);
            WriteUInt32LittleEndian(target, ref cursor, entry.RoomCount);
            WriteUInt32LittleEndian(target, ref cursor, entry.EntityCount);
            WriteUInt32LittleEndian(target, ref cursor, entry.MissingMask);
            WriteUInt32LittleEndian(target, ref cursor, entry.NonFiniteMask);
            WriteUInt32LittleEndian(target, ref cursor, entry.ReplayBlock);
            WriteUInt32LittleEndian(target, ref cursor, entry.Reserved0);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> target, ref int cursor, ulong value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
            target[cursor++] = (byte)(value >> 32);
            target[cursor++] = (byte)(value >> 40);
            target[cursor++] = (byte)(value >> 48);
            target[cursor++] = (byte)(value >> 56);
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> target, ref int cursor, uint value)
        {
            target[cursor++] = (byte)value;
            target[cursor++] = (byte)(value >> 8);
            target[cursor++] = (byte)(value >> 16);
            target[cursor++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ThrowFatalDesync(uint frame, uint flags)
        {
            ulong masterHash = ((ulong)_lastMasterHashHi << 32) | _lastMasterHashLo;
            throw new FatalDesyncException(frame, masterHash, flags);
        }

        private bool LoadGhostReplay(string path)
        {
            if (_binaryLayoutInvalid != 0)
                return false;

            CoreDeterminismSignals.ClearInputOverride();
            Volatile.Write(ref _ghostReplayActive, 0);
            EnsureGhostReplayBuffers();
            if (!TryGetVaultBuffer(BufferID.LockstepGhostReplayHeaders, MaxGhostReplayBlocks, out NativeArray<LockstepReplayBlockHeader> ghostHeaders) ||
                !TryGetVaultBuffer(BufferID.LockstepGhostReplayInputs, MaxGhostReplayBlocks * ReplayInputFrameCapacity, out NativeArray<LockstepReplayInputFrame> ghostInputs))
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
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ReplayBlockBytes * 4, FileOptions.SequentialScan))
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
                        int offset = ReplayHeaderBytes;
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
                            offset += ReplayInputBytes;
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
                header.HeaderSizeBytes != ReplayHeaderBytes ||
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
            return _dataVault;
        }

        private NativeArray<T> OpenOrAcquireVaultBufferView<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory,
            bool allowColdInitialization = true)
            where T : struct
        {
            if (!OpenOrAcquireVaultBuffer(bufferId, requiredLength, options, out NativeArray<T> buffer, allowColdInitialization))
                return default;

            return HasRequiredLength(buffer, requiredLength) ? buffer : default;
        }

        private bool TryGetVaultBuffer<T>(BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            return TryOpenExistingVaultBuffer(bufferId, 0, out buffer);
        }

        private bool TryGetVaultBuffer<T>(BufferID bufferId, int requiredLength, out NativeArray<T> buffer)
            where T : struct
        {
            return TryOpenExistingVaultBuffer(bufferId, requiredLength, out buffer);
        }

        private bool TryGetReadVaultBuffer<T>(BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            return TryOpenExistingReadVaultBuffer(bufferId, 0, out buffer);
        }

        private bool TryGetReadVaultBuffer<T>(BufferID bufferId, int requiredLength, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            return TryOpenExistingReadVaultBuffer(bufferId, requiredLength, out buffer);
        }

        private bool TryGetHashSourceBuffer<T>(BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            if (!TryOpenExistingVaultBuffer(bufferId, 0, out buffer))
                return false;

            unsafe
            {
                if (!IsAlignedForNativeView<T>(buffer.GetUnsafeReadOnlyPtr()))
                {
                    buffer = default;
                    return false;
                }
            }

            return true;
        }

        private bool OpenOrAcquireVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            bool allowColdInitialization = true)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !IsMatchingVaultHandle(in handle, bufferId))
            {
                if (!allowColdInitialization || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    return false;

                handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.CoreDeterminism, options);
            }

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenExistingVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryOpenVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenExistingReadVaultBuffer<T>(
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = ResolveDataVault();
            if (vault == null || requiredLength < 0)
                return false;

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            return TryReadVaultBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryOpenVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryResolveHandle(in handle, out buffer) || !buffer.IsCreated)
            {
                buffer = default;
                return false;
            }

            if (requiredLength > 0 && buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryReadVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength < 0 || !IsMatchingVaultHandle(in handle, bufferId))
                return false;

            if (!vault.TryReadOnlyHandle(in handle, out buffer) || !buffer.IsCreated)
            {
                buffer = default;
                return false;
            }

            if (requiredLength > 0 && buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsMatchingVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) && handle.Generation != 0u;
        }

        private static bool IsAlignedForNativeView<T>(void* pointer)
            where T : struct
        {
            if (pointer == null)
                return false;

            int alignment = UnsafeUtility.AlignOf<T>();
            if (alignment <= 1)
                return true;

            ulong address = unchecked((ulong)new IntPtr(pointer).ToInt64());
            return (address & (ulong)(alignment - 1)) == 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasRequiredLength<T>(NativeArray<T> buffer, int requiredLength)
            where T : struct
        {
            return buffer.IsCreated && requiredLength >= 0 && buffer.Length >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasRequiredLength<T>(NativeArray<T>.ReadOnly buffer, int requiredLength)
            where T : struct
        {
            return buffer.IsCreated && requiredLength >= 0 && buffer.Length >= requiredLength;
        }

        private void EnsureNativeState()
        {
            OpenOrAcquireVaultBufferView<LockstepArrayHash>(BufferID.LockstepArrayHashes, (int)LockstepHashCategory.Count, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<ulong>(BufferID.LockstepMasterStateHash, 1, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepMasterFlags, 1, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<LockstepTelemetryEntry>(BufferID.LockstepTelemetryRing, TelemetryFrameCapacity, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<LockstepMasterHashHistoryEntry>(BufferID.LockstepMasterHashHistory, MasterHashHistoryCapacity, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<int>(BufferID.LockstepMasterHashHistoryCursor, 1, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<LockstepReplayInputFrame>(BufferID.LockstepReplayInputRing, ReplayInputFrameCapacity, NativeArrayOptions.ClearMemory);
        }

        private void EnsureHashNativeState()
        {
            OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepRigidbodyElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepPlayerElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepRoomElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<uint>(BufferID.LockstepEntityElementHashes, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepRigidbodyElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepPlayerElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepRoomElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
            OpenOrAcquireVaultBufferView<byte>(BufferID.LockstepEntityElementFlags, MaxHashElements, NativeArrayOptions.UninitializedMemory);
        }

        private void EnsurePostFixedMirrorState()
        {
            OpenOrAcquireVaultBufferView<LockstepPlayerKinematicState>(BufferID.PlayerKinematicState, 1, NativeArrayOptions.ClearMemory);
            IHabitatGraphService habitat = _habitat;
            if (habitat != null && habitat.IsInitialized)
                OpenOrAcquireVaultBufferView<float>(BufferID.RoomWaterLevels, RoomWaterLevelsMirrorCapacity, NativeArrayOptions.ClearMemory);
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
            OpenOrAcquireVaultBufferView<LockstepReplayBlockHeader>(BufferID.LockstepGhostReplayHeaders, MaxGhostReplayBlocks, NativeArrayOptions.ClearMemory);
            OpenOrAcquireVaultBufferView<LockstepReplayInputFrame>(BufferID.LockstepGhostReplayInputs, MaxGhostReplayBlocks * ReplayInputFrameCapacity, NativeArrayOptions.ClearMemory);
        }

        private static void DisposeNativeState()
        {
            // DataVault owns lockstep buffers and preserves the latest hash/blackbox across component lifetime churn.
        }

        private void EnsureReplayWriterCold()
        {
            _lastReplayBlockSequence = 0u;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogException(Exception ex)
        {
            Hecton8.Core.H8Debug.LogException(ex);
        }

    }

    internal static class LockstepHashMath
    {
        public const uint NonFiniteSourceFlag = 1u << 31;
        public const uint FnvOffset32 = 2166136261u;
        private const uint FnvPrime32 = 16777619u;
        private const ulong FnvOffset64 = 14695981039346656037UL;
        private const ulong FnvPrime64 = 1099511628211UL;
        private const float MillimeterScale = HectonPhysicsContract.DeterministicMillimeterScale;
        private const float MaxQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMaxQuantizedMillimeterFloat;
        private const float MinQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMinQuantizedMillimeterFloat;
        private const int MaxQuantizedMillimeter = HectonPhysicsContract.DeterministicMaxQuantizedMillimeter;
        private const int MinQuantizedMillimeter = HectonPhysicsContract.DeterministicMinQuantizedMillimeter;

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
            return Fnv1A(hash, QuantizeMillimeter(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int QuantizeMillimeter(float value)
        {
            if (!(value <= float.MaxValue && value >= -float.MaxValue))
                return 0;

            float scaled = value * MillimeterScale;
            if (scaled >= MaxQuantizedMillimeterFloat)
                return MaxQuantizedMillimeter;
            if (scaled <= MinQuantizedMillimeterFloat)
                return MinQuantizedMillimeter;

            return scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
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
            if (!hashes.IsCreated || hashes.Length < (int)LockstepHashCategory.Count)
                return Fnv1A64(hash, 0x4D495353u);

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
}
