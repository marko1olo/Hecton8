using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Physics.Vehicles;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Audio
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Audio/Vocal Warning System")]
    public sealed class VocalWarningSystem : MonoBehaviour, IVocalWarningSystem, IUpdatable, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001DirectSignalPushDropCount_VocalWarningSystem;
        private const string TelemetryDumpPayloadLabel = "vocalWarningTelemetryDumpPayload";
        private const uint VesselTelemetryHandleRetryMask = 63u;

        internal static int SignalPushDropCount =>
            Volatile.Read(ref s_x001DirectSignalPushDropCount_VocalWarningSystem);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticSignalDiagnostics()
        {
            Volatile.Write(ref s_x001DirectSignalPushDropCount_VocalWarningSystem, 0);
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct VocalWarningDTO
        {
            [FieldOffset(0)] public uint AudioBankHashID;
            [FieldOffset(4)] public float PriorityScore;
            [FieldOffset(8)] public float ExpirationTime;
            [FieldOffset(12)] public uint Flags;
            [FieldOffset(16)] public long SourceAupGridX;
            [FieldOffset(24)] public long SourceAupGridY;
            [FieldOffset(32)] public long SourceAupGridZ;
            [FieldOffset(40)] public float SourceAupLocalX;
            [FieldOffset(44)] public float SourceAupLocalY;
            [FieldOffset(48)] public float SourceAupLocalZ;
            [FieldOffset(52)] public uint SourceId;
            [FieldOffset(56)] private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct AlarmStateDTO
        {
            [FieldOffset(0)] public ulong activeAlarmsMask;
            [FieldOffset(8)] public uint ActivePriorityCount;
            [FieldOffset(12)] public uint DiscardedExpired;
            [FieldOffset(16)] public uint Sequence;
            [FieldOffset(20)] public uint FaultFlags;
            [FieldOffset(24)] public uint LastAcceptedBitIndex;
            [FieldOffset(28)] public uint HighestPriorityBitIndex;
            [FieldOffset(32)] public uint LastRejectedBitIndex;
            [FieldOffset(36)] public uint SaturationCount;
            [FieldOffset(40)] private byte _pad0;
            [FieldOffset(41)] private byte _pad1;
            [FieldOffset(42)] private byte _pad2;
            [FieldOffset(43)] private byte _pad3;
            [FieldOffset(44)] private byte _pad4;
            [FieldOffset(45)] private byte _pad5;
            [FieldOffset(46)] private byte _pad6;
            [FieldOffset(47)] private byte _pad7;
            [FieldOffset(48)] private byte _pad8;
            [FieldOffset(49)] private byte _pad9;
            [FieldOffset(50)] private byte _pad10;
            [FieldOffset(51)] private byte _pad11;
            [FieldOffset(52)] private byte _pad12;
            [FieldOffset(53)] private byte _pad13;
            [FieldOffset(54)] private byte _pad14;
            [FieldOffset(55)] private byte _pad15;
            [FieldOffset(56)] private byte _pad16;
            [FieldOffset(57)] private byte _pad17;
            [FieldOffset(58)] private byte _pad18;
            [FieldOffset(59)] private byte _pad19;
            [FieldOffset(60)] private byte _pad20;
            [FieldOffset(61)] private byte _pad21;
            [FieldOffset(62)] private byte _pad22;
            [FieldOffset(63)] private byte _pad23;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VocalWarningCurrentState
        {
            [FieldOffset(0)] public uint AudioBankHashID;
            [FieldOffset(4)] public float PriorityScore;
            [FieldOffset(8)] public float PlaybackRemainingSeconds;
            [FieldOffset(12)] public uint Flags;
            [FieldOffset(16)] public float LastDurationSeconds;
            [FieldOffset(20)] public float LastRadioDistortion01;
            [FieldOffset(24)] public uint LastDispatchFrame;
            [FieldOffset(28)] public uint LastSubtitleHash;
            [FieldOffset(32)] public uint LastInterruptCount;
            [FieldOffset(36)] public uint LastDirectionHash;
            [FieldOffset(40)] public float QualityWeight01;
            [FieldOffset(44)] private byte _pad0;
            [FieldOffset(45)] private byte _pad1;
            [FieldOffset(46)] private byte _pad2;
            [FieldOffset(47)] private byte _pad3;
            [FieldOffset(48)] private byte _pad4;
            [FieldOffset(49)] private byte _pad5;
            [FieldOffset(50)] private byte _pad6;
            [FieldOffset(51)] private byte _pad7;
            [FieldOffset(52)] private byte _pad8;
            [FieldOffset(53)] private byte _pad9;
            [FieldOffset(54)] private byte _pad10;
            [FieldOffset(55)] private byte _pad11;
            [FieldOffset(56)] private byte _pad12;
            [FieldOffset(57)] private byte _pad13;
            [FieldOffset(58)] private byte _pad14;
            [FieldOffset(59)] private byte _pad15;
            [FieldOffset(60)] private byte _pad16;
            [FieldOffset(61)] private byte _pad17;
            [FieldOffset(62)] private byte _pad18;
            [FieldOffset(63)] private byte _pad19;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct VocalWarningDispatchDTO
        {
            [FieldOffset(0)] public long SourceAupGridX;
            [FieldOffset(8)] public long SourceAupGridY;
            [FieldOffset(16)] public long SourceAupGridZ;
            [FieldOffset(24)] public uint AudioBankHashID;
            [FieldOffset(28)] public int CuePriority;
            [FieldOffset(32)] public float VolumeScalar;
            [FieldOffset(36)] public float PlaybackSpeed;
            [FieldOffset(40)] public float RadioDistortion01;
            [FieldOffset(44)] public float SpatialBlend01;
            [FieldOffset(48)] public float SourceAupLocalX;
            [FieldOffset(52)] public float SourceAupLocalY;
            [FieldOffset(56)] public float SourceAupLocalZ;
            [FieldOffset(60)] public uint Flags;
            [FieldOffset(64)] public float DurationSeconds;
            [FieldOffset(68)] public uint Frame;
            [FieldOffset(72)] public ushort DirectionHash;
            [FieldOffset(74)] public byte SubtitlePriority;
            [FieldOffset(75)] public byte WarningId;
            [FieldOffset(76)] private byte _pad0;
            [FieldOffset(77)] private byte _pad1;
            [FieldOffset(78)] private byte _pad2;
            [FieldOffset(79)] private byte _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VwsTelemetryEntry
        {
            [FieldOffset(0)] public long SourceAupGridX;
            [FieldOffset(8)] public long SourceAupGridY;
            [FieldOffset(16)] public long SourceAupGridZ;
            [FieldOffset(24)] public ulong ActiveAlarmsMask;
            [FieldOffset(32)] public uint Frame;
            [FieldOffset(36)] public uint ActivePriorityCount;
            [FieldOffset(40)] public uint CurrentAudioBankHashID;
            [FieldOffset(44)] public float CurrentPriorityScore;
            [FieldOffset(48)] public float BurstExecutionMicros;
            [FieldOffset(52)] public uint FaultFlags;
            [FieldOffset(56)] public uint HighestPriorityBitIndex;
            [FieldOffset(60)] public ushort DirectionHash;
            [FieldOffset(62)] public byte CurrentWarningId;
            [FieldOffset(63)] public byte LastDispatchedWarningId;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct VwsTelemetryDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint EntryStrideBytes;
            [FieldOffset(12)] public uint Capacity;
            [FieldOffset(16)] public uint Cursor;
            [FieldOffset(20)] public uint EmittedCount;
            [FieldOffset(24)] public uint RingStartIndex;
            [FieldOffset(28)] public uint Reserved0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        internal struct VocalWarningProfileDTO
        {
            [FieldOffset(0)] public uint AudioBankHashID;
            [FieldOffset(4)] public float BasePriority;
            [FieldOffset(8)] public float CooldownSeconds;
            [FieldOffset(12)] public float DurationSeconds;
            [FieldOffset(16)] public uint Flags;
            [FieldOffset(20)] public uint DirectionHash;
            [FieldOffset(24)] private byte _pad0;
            [FieldOffset(25)] private byte _pad1;
            [FieldOffset(26)] private byte _pad2;
            [FieldOffset(27)] private byte _pad3;
            [FieldOffset(28)] private byte _pad4;
            [FieldOffset(29)] private byte _pad5;
            [FieldOffset(30)] private byte _pad6;
            [FieldOffset(31)] private byte _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct VocalWarningTuningDTO
        {
            [FieldOffset(0)] public float BasePriorityHull;
            [FieldOffset(4)] public float BasePriorityCrush;
            [FieldOffset(8)] public float BasePriorityOxygen;
            [FieldOffset(12)] public float BasePriorityRadiation;
            [FieldOffset(16)] public float BasePriorityPower;
            [FieldOffset(20)] public float CriticalBoost;
            [FieldOffset(24)] public float InterruptionThreshold;
            [FieldOffset(28)] public float ProducerPriorityScale;
            [FieldOffset(32)] public float SeverityBoost;
            [FieldOffset(36)] public float DefaultBasePriority;
            [FieldOffset(40)] public uint Flags;
            [FieldOffset(44)] public uint Revision;
            [FieldOffset(48)] private byte _pad0;
            [FieldOffset(49)] private byte _pad1;
            [FieldOffset(50)] private byte _pad2;
            [FieldOffset(51)] private byte _pad3;
            [FieldOffset(52)] private byte _pad4;
            [FieldOffset(53)] private byte _pad5;
            [FieldOffset(54)] private byte _pad6;
            [FieldOffset(55)] private byte _pad7;
            [FieldOffset(56)] private byte _pad8;
            [FieldOffset(57)] private byte _pad9;
            [FieldOffset(58)] private byte _pad10;
            [FieldOffset(59)] private byte _pad11;
            [FieldOffset(60)] private byte _pad12;
            [FieldOffset(61)] private byte _pad13;
            [FieldOffset(62)] private byte _pad14;
            [FieldOffset(63)] private byte _pad15;
        }

        [StructLayout(LayoutKind.Explicit, Size = 48)]
        public struct VocalWarningTelemetrySnapshot
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint ActivePriorityCount;
            [FieldOffset(8)]
            public ulong ActiveAlarmsMask;
            [FieldOffset(8)]
            public ulong ActivePriorityWord;
            [FieldOffset(16)]
            public uint CurrentAudioBankHashID;
            [FieldOffset(20)]
            public float CurrentPriorityScore;
            [FieldOffset(24)]
            public float BurstExecutionMicros;
            [FieldOffset(28)]
            public uint ExpiredDiscardCount;
            [FieldOffset(32)]
            public uint FaultFlags;
            [FieldOffset(36)]
            public uint InterruptCount;
            [FieldOffset(40)]
            private ulong _pad0;
        }

        private ref struct VwsVaultViews
        {
            public NativeArray<VocalWarningDTO> Queue;
            public NativeArray<AlarmStateDTO> PriorityState;
            public NativeArray<byte> WarningFlags;
            public NativeArray<float> Cooldowns;
            public NativeArray<float> WarningSeverity;
            public NativeArray<uint> WarningSourceIds;
            public NativeArray<VocalWarningCurrentState> CurrentState;
            public NativeArray<VocalWarningDispatchDTO> Dispatch;
            public NativeArray<VocalWarningProfileDTO> Profiles;
            public NativeArray<VocalWarningTuningDTO> Tuning;
            public NativeArray<VwsTelemetryEntry> TelemetryRing;
        }

        private const int QueueCapacity = 64;
        private const int WarningStateLength = VocalWarningHashes.CanonicalWarningCount + 1;
        private const int DispatchLength = 1;
        private const int ProfileCapacity = 8;
        private const int TelemetryCapacity = 300;
        private const float DefaultCooldownSeconds = 4f;
        private const float DefaultGain = 0.85f;
        private const float ToxicityWarningMinSeverity01 = 0.08f;
        private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;
        private const uint VocalWarningSystemHash = 0x56333532u; // V352
        private const uint VaultOwnerSignalHash = 0x41565753u; // AVWS
        private const BufferID AlarmStateBufferId = BufferID.SpatialAudioVirtualVoiceTuning;
        private const BufferID VocalWarningCurrentStateBufferId = BufferID.SpatialAudioVirtualVoiceWritePool;
        private const BufferID VocalWarningDispatchBufferId = BufferID.SpatialAudioVirtualVoiceSortPool;
        private const BufferID VocalWarningProfilesBufferId = BufferID.SpatialAudioVirtualVoiceDtoPool;
        private const BufferID VocalWarningTuningBufferId = BufferID.SpatialAudioAcousticSourceWritePool;
        private const uint QueueFlagCritical = 1u << 0;
        private const uint QueueFlagInterrupt = 1u << 1;
        private const uint QueueFlagHabitatIntegrity = 1u << 2;
        private const uint QueueFlagDirectional = 1u << 3;
        private const uint QueueFlagMock = 1u << 4;
        private const uint QueueFlagPreempted = 1u << 5;
        private const int AlarmBitCount = 64;
        private const int NoPriorityBitIndex = -1;
        private const int LowestCanonicalWarningId = VocalWarningHashes.LowestCanonicalWarningId;
        private const int HighestCanonicalWarningId = VocalWarningHashes.HighestCanonicalWarningId;
        private const int CanonicalWarningCount = VocalWarningHashes.CanonicalWarningCount;
        private const int CuePriorityBandSize = 255 / CanonicalWarningCount;
        private const byte SubtitleCueFlagInterrupt = 1 << 0;
        private const byte SubtitleCueFlagDirectionLeft = 1 << 1;
        private const byte SubtitleCueFlagDirectionRight = 1 << 2;
        private const byte SubtitleCueFlagDirectionBehind = 1 << 3;
        private const uint FaultFlagTelemetryInvalid = 1u << 0;
        private const uint FaultFlagPriorityInvalid = 1u << 1;
        private const uint FaultFlagPriorityInputInvalid = 1u << 2;
        private const uint FaultFlagVocalCueRejected = 1u << 3;
        private const uint FaultFlagSubtitleRejected = 1u << 4;
        private const uint FaultFlagAlarmMaskOverflow = 1u << 5;
        private const uint FaultFlagVocalWarningSignalRejected = 1u << 6;
        private const uint PackedWarningIdShift = 8;
        private const uint PackedDirectionShift = 16;
        private const uint PackedDirectionMask = 0xFFFFu << (int)PackedDirectionShift;
        private const SystemID VaultOwner = SystemID.AudioVocalWarning;
        private static readonly ulong VocalWarningTuningMutationGuardMask =
            VocalWarningMutationGuardBit(VocalWarningTuningBufferId);
        private static readonly ulong VocalWarningFrameMutationGuardMask =
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningQueue) |
            VocalWarningMutationGuardBit(AlarmStateBufferId) |
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningFlags) |
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningCooldowns) |
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningSeverity) |
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningSourceIds) |
            VocalWarningMutationGuardBit(VocalWarningCurrentStateBufferId) |
            VocalWarningMutationGuardBit(VocalWarningDispatchBufferId) |
            VocalWarningMutationGuardBit(VocalWarningProfilesBufferId) |
            VocalWarningTuningMutationGuardMask |
            VocalWarningMutationGuardBit(BufferID.AudioVocalWarningTelemetry);
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_352_VWS.bin";
        private const string AgentTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_X_011.bin";

        [Header("Mix")]
        [Tooltip("Voice gain applied before the procedural renderer safety limiter.")]
        [SerializeField, Range(0f, 1f)] private float voiceGain = DefaultGain;
        [Tooltip("Cooldown used when a producer does not provide a positive finite cooldown.")]
        [SerializeField, Min(0f)] private float fallbackCooldownSeconds = DefaultCooldownSeconds;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VocalWarningDTO> _vwsQueueHandle;
        private VaultGenerationHandle<AlarmStateDTO> _priorityStateHandle;
        private VaultGenerationHandle<byte> _warningFlagsHandle;
        private VaultGenerationHandle<float> _cooldownsHandle;
        private VaultGenerationHandle<float> _warningSeverityHandle;
        private VaultGenerationHandle<uint> _warningSourceIdsHandle;
        private VaultGenerationHandle<VocalWarningCurrentState> _currentStateHandle;
        private VaultGenerationHandle<VocalWarningDispatchDTO> _dispatchHandle;
        private VaultGenerationHandle<VocalWarningProfileDTO> _profilesHandle;
        private VaultGenerationHandle<VocalWarningTuningDTO> _tuningHandle;
        private VaultGenerationHandle<VwsTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<VesselTelemetryEntry> _vesselTelemetryHandle;
        private SimulationPhaseSystem _simulationSystem;
        private VisualSyncPhaseSystem _visualSyncSystem;
        private JobHandle _pendingVocalWarningJobHandle;
        private IDataVault _pendingVocalWarningGuardVault;
        private int _telemetryCursor;
        private int _queueCount;
        private int _registeredUpdate;
        private int _registeredSlowTick;
        private int _registeredLateFrameTick;
        private int _registeredHotSwap;
        private int _registeredRuntime;
        private int _registeredPostSimulation;
        private int _runtimeOwnerAborted;
        private int _nativeAllocated;
        private int _vocalWarningJobsPending;
        private int _telemetryDumpRequested;
        private int _telemetryDumped;
        private int _telemetrySamplesWritten;
        private int _visualSyncPresentationPending;
        private int _pendingExternalFaultFlags;
        private int _pendingCancelRequest;
        private uint _ownerFrameCounter;
        private uint _lastProcessedFrame = uint.MaxValue;
        private uint _pendingPresentationFrame;
        private float _globalQualityWeight01 = 1f;
        private float _vesselCareTone01;
        private float _vwsClockSeconds;
        private float _warningPlaybackRemainingSeconds;
        private float _currentPriorityScore;
        private float _lastBurstExecutionMicros;
        private float _pendingVocalWarningScheduleMicros;
        private GameObject _playerToxicityTargetObject;
        private uint _playerToxicityTargetHash = PlayerToxicityFallbackEntityHash;
        private uint _playerSurvivalVitalsSourceId;
        private int _lastToxicityExposureSnapshotGeneration;
        private uint _currentAudioBankHashID;
        private uint _lastDispatchedAudioBankHashID;
        private uint _lastInterruptCount;
        private long _lastSourceAupGridX;
        private long _lastSourceAupGridY;
        private long _lastSourceAupGridZ;
        private float _lastSourceAupLocalX;
        private float _lastSourceAupLocalY;
        private float _lastSourceAupLocalZ;
        private ushort _lastDirectionHash;
        private byte _currentWarningId;
        private byte _lastDispatchedWarningId;

        public bool IsInitialized => Volatile.Read(ref _nativeAllocated) != 0 &&
                                     Volatile.Read(ref _runtimeOwnerAborted) == 0;

        public bool IsVocalWarningRuntimeReady => Volatile.Read(ref _nativeAllocated) != 0 &&
                                                  Volatile.Read(ref _runtimeOwnerAborted) == 0 &&
                                                  Volatile.Read(ref _registeredRuntime) != 0 &&
                                                  isActiveAndEnabled;

        public int PendingCount => Volatile.Read(ref _runtimeOwnerAborted) != 0 ? 0 : math.max(0, _queueCount);

        public byte CurrentWarningId => Volatile.Read(ref _runtimeOwnerAborted) != 0 ? (byte)0 : _currentWarningId;

        public bool IsWarningActive => Volatile.Read(ref _runtimeOwnerAborted) == 0 && _warningPlaybackRemainingSeconds > 0f;

#if UNITY_EDITOR
        public int EditorQueueCapacity => QueueCapacity;
        public float EditorCurrentPriorityScore => _currentPriorityScore;
        public float EditorLastBurstExecutionMicros => _lastBurstExecutionMicros;
        public ushort EditorLastDirectionHash => _lastDirectionHash;
        public static int EditorVocalWarningDtoSizeBytes => UnsafeUtility.SizeOf<VocalWarningDTO>();
        public static int EditorVocalWarningTuningDtoSizeBytes => UnsafeUtility.SizeOf<VocalWarningTuningDTO>();
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe ref T NativeElementRef<T>(NativeArray<T> array, int index)
            where T : struct
        {
            void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(array);
            return ref UnsafeUtility.AsRef<T>((byte*)pointer + (index * UnsafeUtility.SizeOf<T>()));
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            if (!TryRegisterRuntimeService())
                return;

            EnsureNativeStorage();
            RefreshCachedServicesCold();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            if (!TryRegisterRuntimeService())
                return;

            EnsureNativeStorage();
            RefreshCachedServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterPostSimulation();
        }

        private void OnDisable()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            UnregisterRuntime();
            DisposeNativeStorage();
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _registeredPostSimulation) != 0)
                return;

            RunVocalWarningFrame(deltaTime, NextOwnerFrameId());
        }

        public void SlowTick()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _registeredPostSimulation) != 0 ||
                Volatile.Read(ref _registeredUpdate) != 0)
                return;

            RunVocalWarningFrame(0.1f, NextOwnerFrameId());
        }

        public void LateFrameTick()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _registeredPostSimulation) != 0)
                return;

            VisualSyncPresentationTick();
        }

        public bool TryQueueWarning(byte warningId, float severity01, float cooldownSeconds, byte flags, uint sourceId)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || Volatile.Read(ref _registeredRuntime) == 0)
                return false;

            byte normalized = NormalizeWarningId(warningId);
            if (normalized == 0)
            {
                AccumulatePendingFault(FaultFlagPriorityInputInvalid);
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            uint hash = VocalWarningHashes.FromWarningId(normalized);
            if (hash == 0u)
                AccumulatePendingFault(FaultFlagPriorityInputInvalid);
            if (hash == 0u)
                return false;

            VocalWarningSignal signal = default;
            signal.WarningHash = hash;
            signal.SourceId = sourceId;
            signal.Severity01 = ResolveSeverity01(severity01);
            signal.CooldownSeconds = ResolveCooldownSeconds(cooldownSeconds);
            signal.Priority = normalized;
            signal.Flags = flags;
            bool accepted = SignalBus<VocalWarningSignal>.TryPushTracked(
                in signal,
                ref s_x001DirectSignalPushDropCount_VocalWarningSystem);
            if (!accepted)
            {
                AccumulatePendingFault(FaultFlagVocalWarningSignalRejected);
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            return true;
        }

        private void AccumulatePendingFault(uint faultFlags)
        {
            if (faultFlags == 0u)
                return;

            int mask = unchecked((int)faultFlags);
            int observed;
            int next;
            do
            {
                observed = Volatile.Read(ref _pendingExternalFaultFlags);
                next = observed | mask;
                if (next == observed)
                    return;
            }
            while (Interlocked.CompareExchange(ref _pendingExternalFaultFlags, next, observed) != observed);
        }

        public bool TryReadActiveAlarmsMask(out ulong activeAlarmsMask)
        {
            activeAlarmsMask = 0UL;
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !vault.TryReadOnlyHandle(in _priorityStateHandle, out NativeArray<AlarmStateDTO>.ReadOnly priorityState) ||
                !priorityState.IsCreated ||
                priorityState.Length <= 0)
            {
                return false;
            }

            activeAlarmsMask = priorityState[0].activeAlarmsMask;
            return true;
        }

        public void CancelCurrentWarning()
        {
            Interlocked.Exchange(ref _pendingCancelRequest, 1);
            Interlocked.Exchange(ref _visualSyncPresentationPending, 0);
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                RefreshPlayerToxicityTargetHash(playerContext);
                RefreshPlayerSurvivalVitalsSourceId(playerContext);
                _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                RebindDataVault(nextVault);
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                RefreshPlayerToxicityTargetHash(playerContext);
                RefreshPlayerSurvivalVitalsSourceId(playerContext);
                _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault &&
                !ReferenceEquals(previousService, currentService))
            {
                IDataVault nextVault = currentService is IDataVault vault ? vault : null;
                RebindDataVault(nextVault);
            }
        }

#if UNITY_EDITOR
        public bool EditorInjectMockThreats(int count)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                !TryAcquireVocalWarningFrameGuard(out IDataVault guardVault))
                return false;

            try
            {
                if (!TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views))
                    return false;

                GenerateMockVocalThreatsJob job = new GenerateMockVocalThreatsJob
                {
                    Queue = views.Queue,
                    PriorityState = views.PriorityState,
                    Tuning = views.Tuning,
                    Profiles = views.Profiles,
                    TimeSeconds = _vwsClockSeconds,
                    Seed = NextOwnerFrameId() ^ 0x9E3779B9u,
                    Count = math.clamp(count, 1, 50)
                };
                job.Run();
                _queueCount = ResolveActivePriorityCount(ref views);
                return true;
            }
            finally
            {
                ReleaseVocalWarningFrameGuard(guardVault);
            }
        }

        public bool EditorTryReadTuning(out VocalWarningTuningDTO tuning)
        {
            tuning = CreateDefaultTuning();
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !vault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<VocalWarningTuningDTO>.ReadOnly tuningView) ||
                !tuningView.IsCreated ||
                tuningView.Length <= 0)
                return false;

            tuning = ResolveTuning(tuningView);
            return true;
        }

        public unsafe bool EditorTryWriteTuning(in VocalWarningTuningDTO tuning)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                !TryAcquireTuningMutationView(out NativeArray<VocalWarningTuningDTO> tuningView, out IDataVault guardVault))
            {
                return false;
            }

            try
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuningView);
                ref VocalWarningTuningDTO target = ref UnsafeUtility.AsRef<VocalWarningTuningDTO>(pointer);
                target = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                ReleaseVocalWarningMutationGuard(guardVault, VocalWarningTuningMutationGuardMask);
            }
        }

        public bool EditorTryGetTelemetrySample(int offsetFromNewest, out VocalWarningTelemetrySnapshot snapshot)
        {
            snapshot = default;
            IDataVault vault = _dataVault;
            if (Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<VwsTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length <= 0)
                return false;

            int cursor = _telemetryCursor - 1 - math.max(0, offsetFromNewest);
            while (cursor < 0)
                cursor += telemetryRing.Length;

            VwsTelemetryEntry entry = telemetryRing[cursor % telemetryRing.Length];
            snapshot.Frame = entry.Frame;
            snapshot.ActivePriorityCount = entry.ActivePriorityCount;
            snapshot.ActiveAlarmsMask = entry.ActiveAlarmsMask;
            snapshot.ActivePriorityWord = entry.ActiveAlarmsMask;
            snapshot.CurrentAudioBankHashID = entry.CurrentAudioBankHashID;
            snapshot.CurrentPriorityScore = entry.CurrentPriorityScore;
            snapshot.BurstExecutionMicros = entry.BurstExecutionMicros;
            snapshot.ExpiredDiscardCount = 0u;
            snapshot.FaultFlags = entry.FaultFlags;
            snapshot.InterruptCount = _lastInterruptCount;
            return entry.Frame != 0u || entry.ActivePriorityCount != 0u || entry.CurrentAudioBankHashID != 0u;
        }

        public bool EditorTryGetPriorityEntry(int priorityOrderIndex, out uint audioBankHashID, out float priorityScore)
        {
            audioBankHashID = 0u;
            priorityScore = 0f;
            IDataVault vault = _dataVault;
            if (priorityOrderIndex < 0 ||
                Volatile.Read(ref _nativeAllocated) == 0 ||
                vault == null ||
                !vault.TryReadOnlyHandle(in _vwsQueueHandle, out NativeArray<VocalWarningDTO>.ReadOnly queue) ||
                !vault.TryReadOnlyHandle(in _priorityStateHandle, out NativeArray<AlarmStateDTO>.ReadOnly priorityState) ||
                !queue.IsCreated ||
                !priorityState.IsCreated ||
                priorityState.Length <= 0)
                return false;

            int count = math.clamp((int)priorityState[0].ActivePriorityCount, 0, QueueCapacity);
            if (priorityOrderIndex >= count ||
                !AlarmBitmaskOps.TryGetByPriorityOrder(queue, priorityState, priorityOrderIndex, out VocalWarningDTO dto))
            {
                return false;
            }
            audioBankHashID = dto.AudioBankHashID;
            priorityScore = dto.PriorityScore;
            return audioBankHashID != 0u;
        }
#endif

        private void TryRegisterPostSimulation()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _registeredPostSimulation) != 0)
                return;

            if (_simulationSystem == null)
                _simulationSystem = new SimulationPhaseSystem(this);
            if (_visualSyncSystem == null)
                _visualSyncSystem = new VisualSyncPhaseSystem(this);

            if (GlobalRegistry.TryRegisterDispatcherSystem(_simulationSystem))
            {
                if (GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncSystem))
                {
                    Volatile.Write(ref _registeredPostSimulation, 1);
                    return;
                }

                GlobalRegistry.UnregisterDispatcherSystem(_simulationSystem);
            }

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
            if (GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLateFrameTick = 1;
        }

        private void UnregisterRuntime()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            CompletePendingVocalWarningJobsForTeardown();
            CancelRendererPlaybackAndClearQueues();
            if (Interlocked.Exchange(ref _registeredPostSimulation, 0) != 0)
            {
                if (_simulationSystem != null)
                    GlobalRegistry.UnregisterDispatcherSystem(_simulationSystem);
                if (_visualSyncSystem != null)
                    GlobalRegistry.UnregisterDispatcherSystem(_visualSyncSystem);
            }
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.TryUnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredLateFrameTick, 0) != 0)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredRuntime, 0) != 0)
                GlobalRegistry.UnregisterVocalWarningRuntime(this);
            _playerSurvivalVitalsSourceId = 0u;
        }

        private void EnsureNativeStorage()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _nativeAllocated) != 0)
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            BindVaultStorage(vault);
            if (!TryAcquireVocalWarningFrameGuard(vault, out IDataVault guardVault))
            {
                ClearVaultDescriptors();
                return;
            }

            try
            {
                if (!TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views))
                {
                    ClearVaultDescriptors();
                    return;
                }

                InitializeVaultStorage(ref views);
            }
            finally
            {
                ReleaseVocalWarningFrameGuard(guardVault);
            }

            Volatile.Write(ref _nativeAllocated, 1);
        }

        private IDataVault CacheDataVaultCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                vault = GlobalRegistry.DataVault;
                _dataVault = vault;
            }

            return vault;
        }

        private void BindVaultStorage(IDataVault vault)
        {
            _dataVault = vault;
            _vwsQueueHandle = vault.EnsureGenerationHandle<VocalWarningDTO>(
                BufferID.AudioVocalWarningQueue,
                QueueCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _priorityStateHandle = vault.EnsureGenerationHandle<AlarmStateDTO>(
                AlarmStateBufferId,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _warningFlagsHandle = vault.EnsureGenerationHandle<byte>(
                BufferID.AudioVocalWarningFlags,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _cooldownsHandle = vault.EnsureGenerationHandle<float>(
                BufferID.AudioVocalWarningCooldowns,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _warningSeverityHandle = vault.EnsureGenerationHandle<float>(
                BufferID.AudioVocalWarningSeverity,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _warningSourceIdsHandle = vault.EnsureGenerationHandle<uint>(
                BufferID.AudioVocalWarningSourceIds,
                WarningStateLength,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _currentStateHandle = vault.EnsureGenerationHandle<VocalWarningCurrentState>(
                VocalWarningCurrentStateBufferId,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _dispatchHandle = vault.EnsureGenerationHandle<VocalWarningDispatchDTO>(
                VocalWarningDispatchBufferId,
                DispatchLength,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _profilesHandle = vault.EnsureGenerationHandle<VocalWarningProfileDTO>(
                VocalWarningProfilesBufferId,
                ProfileCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = vault.EnsureGenerationHandle<VocalWarningTuningDTO>(
                VocalWarningTuningBufferId,
                1,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            _telemetryRingHandle = vault.EnsureGenerationHandle<VwsTelemetryEntry>(
                BufferID.AudioVocalWarningTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
            RefreshVesselTelemetryHandleCold(vault);
        }

        private void RefreshVesselTelemetryHandleCold(IDataVault vault)
        {
            if (vault == null)
            {
                _vesselTelemetryHandle = default;
                return;
            }

            if (!vault.TryGetGenerationHandle<VesselTelemetryEntry>(
                    SubmarineBallastBufferIds.VesselTelemetry,
                    out _vesselTelemetryHandle))
            {
                _vesselTelemetryHandle = default;
            }
        }

        private void RefreshVesselTelemetryHandleIfMissing(uint frame)
        {
            if (IsExternalVaultHandle(in _vesselTelemetryHandle, SubmarineBallastBufferIds.VesselTelemetry) ||
                (frame & VesselTelemetryHandleRetryMask) != 0u)
            {
                return;
            }

            RefreshVesselTelemetryHandleCold(_dataVault);
        }

        private float ReadVesselCareTone01()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return math.saturate(_vesselCareTone01);

            if (!IsExternalVaultHandle(in _vesselTelemetryHandle, SubmarineBallastBufferIds.VesselTelemetry) ||
                !vault.TryReadOnlyHandle(in _vesselTelemetryHandle, out NativeArray<VesselTelemetryEntry>.ReadOnly vesselTelemetry) ||
                !vesselTelemetry.IsCreated ||
                vesselTelemetry.Length <= 0)
            {
                return math.saturate(_vesselCareTone01);
            }

            VesselTelemetryEntry entry = vesselTelemetry[0];
            return VesselTelemetryEntry.ResolveToneWeight01(entry.TotalCareActionsCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExternalVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)SystemID.VehiclesPhysics &&
                   handle.Generation != 0u;
        }

        private void InitializeVaultStorage(ref VwsVaultViews views)
        {
            _telemetryCursor = 0;
            _telemetrySamplesWritten = 0;
            _ownerFrameCounter = 0u;
            _lastProcessedFrame = uint.MaxValue;
            Interlocked.Exchange(ref _telemetryDumpRequested, 0);
            Interlocked.Exchange(ref _telemetryDumped, 0);
            Interlocked.Exchange(ref _visualSyncPresentationPending, 0);
            Interlocked.Exchange(ref _pendingExternalFaultFlags, 0);
            Interlocked.Exchange(ref _pendingCancelRequest, 0);
            _pendingPresentationFrame = 0u;
            unsafe
            {
                if (views.PriorityState.IsCreated && views.PriorityState.Length > 0)
                    NativeElementRef(views.PriorityState, 0) = default;
                if (views.CurrentState.IsCreated && views.CurrentState.Length > 0)
                    NativeElementRef(views.CurrentState, 0) = default;
                if (views.Dispatch.IsCreated && views.Dispatch.Length > 0)
                    NativeElementRef(views.Dispatch, 0) = default;
                if (views.Tuning.IsCreated && views.Tuning.Length > 0)
                    NativeElementRef(views.Tuning, 0) = CreateDefaultTuning();

                for (int i = 0; i < views.Queue.Length; i++)
                    NativeElementRef(views.Queue, i) = default;
                for (int i = 0; i < views.WarningFlags.Length; i++)
                    NativeElementRef(views.WarningFlags, i) = 0;
                for (int i = 0; i < views.Cooldowns.Length; i++)
                    NativeElementRef(views.Cooldowns, i) = 0f;
                for (int i = 0; i < views.WarningSeverity.Length; i++)
                    NativeElementRef(views.WarningSeverity, i) = 0f;
                for (int i = 0; i < views.WarningSourceIds.Length; i++)
                    NativeElementRef(views.WarningSourceIds, i) = 0u;
                for (int i = 0; i < views.Profiles.Length; i++)
                    NativeElementRef(views.Profiles, i) = default;
                InitializeDefaultProfiles(ref views);
                for (int i = 0; i < views.TelemetryRing.Length; i++)
                    NativeElementRef(views.TelemetryRing, i) = default;
            }
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            CompletePendingVocalWarningJobsForTeardown();
            ReleaseVaultBackedStorage();
            _dataVault = vault;
            Volatile.Write(ref _nativeAllocated, 0);
            ClearPresentationState(true);
            ClearLastDispatchRoute();
            if (vault != null)
                EnsureNativeStorage();
            _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
        }

        private void ReleaseVaultBackedStorage()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _vwsQueueHandle, BufferID.AudioVocalWarningQueue);
            ReleaseVaultBuffer(vault, ref _priorityStateHandle, AlarmStateBufferId);
            ReleaseVaultBuffer(vault, ref _warningFlagsHandle, BufferID.AudioVocalWarningFlags);
            ReleaseVaultBuffer(vault, ref _cooldownsHandle, BufferID.AudioVocalWarningCooldowns);
            ReleaseVaultBuffer(vault, ref _warningSeverityHandle, BufferID.AudioVocalWarningSeverity);
            ReleaseVaultBuffer(vault, ref _warningSourceIdsHandle, BufferID.AudioVocalWarningSourceIds);
            ReleaseVaultBuffer(vault, ref _currentStateHandle, VocalWarningCurrentStateBufferId);
            ReleaseVaultBuffer(vault, ref _dispatchHandle, VocalWarningDispatchBufferId);
            ReleaseVaultBuffer(vault, ref _profilesHandle, VocalWarningProfilesBufferId);
            ReleaseVaultBuffer(vault, ref _tuningHandle, VocalWarningTuningBufferId);
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle, BufferID.AudioVocalWarningTelemetry);
            ClearVaultDescriptors();
        }

        private static void ReleaseVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            if (vault != null && IsVocalWarningVaultHandle(in handle, expectedBufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVocalWarningVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.SystemID == (uint)VaultOwner &&
                   handle.Generation != 0u;
        }

        private void ClearVaultDescriptors()
        {
            _vwsQueueHandle = default;
            _priorityStateHandle = default;
            _warningFlagsHandle = default;
            _cooldownsHandle = default;
            _warningSeverityHandle = default;
            _warningSourceIdsHandle = default;
            _currentStateHandle = default;
            _dispatchHandle = default;
            _profilesHandle = default;
            _tuningHandle = default;
            _telemetryRingHandle = default;
            _vesselTelemetryHandle = default;
            _vesselCareTone01 = 0f;
        }

        private bool TryResolveVwsOwnerViews(out VwsVaultViews views)
        {
            return TryResolveVwsOwnerViews(_dataVault, out views);
        }

        private bool TryResolveVwsOwnerViews(IDataVault vault, out VwsVaultViews views)
        {
            views = default;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool success =
                vault.TryResolveHandle(in _vwsQueueHandle, out views.Queue) &&
                vault.TryResolveHandle(in _priorityStateHandle, out views.PriorityState) &&
                vault.TryResolveHandle(in _warningFlagsHandle, out views.WarningFlags) &&
                vault.TryResolveHandle(in _cooldownsHandle, out views.Cooldowns) &&
                vault.TryResolveHandle(in _warningSeverityHandle, out views.WarningSeverity) &&
                vault.TryResolveHandle(in _warningSourceIdsHandle, out views.WarningSourceIds) &&
                vault.TryResolveHandle(in _currentStateHandle, out views.CurrentState) &&
                vault.TryResolveHandle(in _dispatchHandle, out views.Dispatch) &&
                vault.TryResolveHandle(in _profilesHandle, out views.Profiles) &&
                vault.TryResolveHandle(in _tuningHandle, out views.Tuning) &&
                vault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) &&
                views.Queue.IsCreated &&
                views.PriorityState.IsCreated &&
                views.WarningFlags.IsCreated &&
                views.Cooldowns.IsCreated &&
                views.WarningSeverity.IsCreated &&
                views.WarningSourceIds.IsCreated &&
                views.CurrentState.IsCreated &&
                views.Dispatch.IsCreated &&
                views.Profiles.IsCreated &&
                views.Tuning.IsCreated &&
                views.TelemetryRing.IsCreated;
            if (!success)
                views = default;
            return success;
        }

        private bool TryAcquireTuningMutationView(out NativeArray<VocalWarningTuningDTO> tuningView, out IDataVault guardVault)
        {
            tuningView = default;
            guardVault = _dataVault;
            if (guardVault == null ||
                !IsVocalWarningVaultHandle(in _tuningHandle, VocalWarningTuningBufferId) ||
                guardVault.IsCompactionFenceActive ||
                !guardVault.TryAcquireMutationGuard(VocalWarningTuningMutationGuardMask))
            {
                guardVault = null;
                return false;
            }

            bool acquired = true;
            try
            {
                if (guardVault.IsCompactionFenceActive ||
                    !guardVault.TryResolveHandle(in _tuningHandle, out tuningView) ||
                    !tuningView.IsCreated ||
                    tuningView.Length <= 0)
                {
                    return false;
                }

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    ReleaseVocalWarningMutationGuard(guardVault, VocalWarningTuningMutationGuardMask);
                    tuningView = default;
                    guardVault = null;
                }
            }
        }

        private static void ReleaseVocalWarningMutationGuard(IDataVault guardVault, ulong mutationGuardMask)
        {
            guardVault?.ReleaseMutationGuard(mutationGuardMask);
        }

        private void RefreshCachedServicesCold()
        {
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);
            RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);
            _lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
        }

        private void RefreshPlayerToxicityTargetHash(IPlayerRuntimeContext playerContext)
        {
            GameObject playerObject = playerContext != null ? playerContext.PlayerObject : null;
            if (playerObject == null)
                playerObject = BootstrapState.CurrentPlayerObject;

            if (ReferenceEquals(playerObject, _playerToxicityTargetObject) && _playerToxicityTargetHash != 0u)
                return;

            _playerToxicityTargetObject = playerObject;
            uint targetHash = playerObject != null ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId())) : 0u;
            _playerToxicityTargetHash = targetHash != 0u ? targetHash : PlayerToxicityFallbackEntityHash;
        }

        private void RefreshPlayerSurvivalVitalsSourceId(IPlayerRuntimeContext playerContext)
        {
            var survival = playerContext != null && playerContext.IsInitialized
                ? playerContext.SurvivalSystem
                : null;
            _playerSurvivalVitalsSourceId = survival != null
                ? RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))
                : 0u;
        }

        private bool TryRegisterRuntimeService()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return false;

            if (Volatile.Read(ref _registeredRuntime) != 0 || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings;
            if (!ReferenceEquals(registeredVocalWarnings, null) && !ReferenceEquals(registeredVocalWarnings, this))
            {
                if (IsVocalWarningSystemUsable(registeredVocalWarnings))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);
            }

            GlobalRegistry.RegisterVocalWarningRuntime(this);
            bool registered = ReferenceEquals(GlobalRegistry.VocalWarnings, this);
            Volatile.Write(ref _registeredRuntime, registered ? 1 : 0);
            if (!registered)
                AbortDuplicateRuntimeOwner();
            return registered;
        }

        private static bool IsVocalWarningSystemUsable(IVocalWarningSystem vocalWarningSystem)
        {
            if (ReferenceEquals(vocalWarningSystem, null))
                return false;

            if (vocalWarningSystem is VocalWarningSystem runtime)
                return runtime.IsVocalWarningRuntimeReady;

            if (vocalWarningSystem is Behaviour behaviour && (behaviour == null || !behaviour.isActiveAndEnabled))
                return false;

            return vocalWarningSystem.IsVocalWarningRuntimeReady;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return true;

            if (!Application.isPlaying)
                return false;

            IVocalWarningSystem registeredVocalWarnings = GlobalRegistry.VocalWarnings;
            if (ReferenceEquals(registeredVocalWarnings, null) || ReferenceEquals(registeredVocalWarnings, this))
                return false;

            if (IsVocalWarningSystemUsable(registeredVocalWarnings))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterVocalWarningRuntime(registeredVocalWarnings);
            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            Volatile.Write(ref _runtimeOwnerAborted, 1);
            Volatile.Write(ref _registeredRuntime, 0);
            Volatile.Write(ref _registeredPostSimulation, 0);
            Volatile.Write(ref _registeredHotSwap, 0);
            Volatile.Write(ref _registeredUpdate, 0);
            Volatile.Write(ref _registeredSlowTick, 0);
            Volatile.Write(ref _registeredLateFrameTick, 0);
            _playerSurvivalVitalsSourceId = 0u;
            DisposeNativeStorage();
            enabled = false;
            Destroy(this);
        }

        private void TryRegisterHotSwapListener()
        {
            if (Volatile.Read(ref _runtimeOwnerAborted) != 0)
                return;

            if (Volatile.Read(ref _registeredHotSwap) != 0)
                return;

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                Volatile.Write(ref _registeredHotSwap, 1);
        }

        private void DisposeNativeStorage()
        {
            if (Interlocked.Exchange(ref _nativeAllocated, 0) == 0 && _dataVault == null)
                return;

            CompletePendingVocalWarningJobsForTeardown();
            ReleaseVaultBackedStorage();
            _dataVault = null;
            _playerSurvivalVitalsSourceId = 0u;
            ClearPresentationState(true);
            ClearLastDispatchRoute();
        }

        private void RunVocalWarningFrame(float deltaTime, uint frame)
        {
            ScheduleVocalWarningFrame(deltaTime, frame, default);
        }

        private JobHandle ScheduleVocalWarningFrame(float deltaTime, uint frame, JobHandle dependsOn)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return dependsOn;

            if (Volatile.Read(ref _visualSyncPresentationPending) != 0)
                return dependsOn;

            if (Volatile.Read(ref _vocalWarningJobsPending) != 0)
                return dependsOn;

            if (_lastProcessedFrame == frame)
                return dependsOn;
            _lastProcessedFrame = frame;

            if (!TryAcquireVocalWarningFrameGuard(out IDataVault guardVault))
                return dependsOn;

            bool guardTransferred = false;
            if (!TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views))
            {
                ReleaseVocalWarningFrameGuard(guardVault);
                return dependsOn;
            }

            try
            {
                bool cancelRequested = Interlocked.Exchange(ref _pendingCancelRequest, 0) != 0;
                uint pendingFaultFlags = (uint)Interlocked.Exchange(ref _pendingExternalFaultFlags, 0);
                if (cancelRequested)
                    CancelRendererPlaybackAndClearQueues(ref views, false);
                if (pendingFaultFlags != 0u)
                    MarkPriorityFault(ref views, pendingFaultFlags);
                if (cancelRequested)
                {
                    _pendingPresentationFrame = frame;
                    Volatile.Write(ref _visualSyncPresentationPending, 1);
                    return dependsOn;
                }

                float dt = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
                _vwsClockSeconds += dt;
                _globalQualityWeight01 = ResolveGlobalQualityWeight01();
                RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);
                RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);
                RefreshVesselTelemetryHandleIfMissing(frame);
                _vesselCareTone01 = ReadVesselCareTone01();
                float vesselCareTone01 = _vesselCareTone01;
                int maxEvaluations = ResolveMaxEvaluations(_globalQualityWeight01, views.Queue.Length);
                AbsoluteUniversePosition listenerAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
                NativeArray<ToxicityExposureSignal>.ReadOnly toxicitySignals = default;
                int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;
                if (toxicitySnapshotGeneration != _lastToxicityExposureSnapshotGeneration)
                {
                    _lastToxicityExposureSnapshotGeneration = toxicitySnapshotGeneration;
                    toxicitySignals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshotArray();
                }

                EvaluateWarningPrioritiesJob evaluateJob = new EvaluateWarningPrioritiesJob
                {
                    Queue = views.Queue,
                    PriorityState = views.PriorityState,
                    Cooldowns = views.Cooldowns,
                    WarningFlags = views.WarningFlags,
                    WarningSeverity = views.WarningSeverity,
                    WarningSourceIds = views.WarningSourceIds,
                    Tuning = views.Tuning,
                    Profiles = views.Profiles,
                    VocalWarnings = SignalBus<VocalWarningSignal>.GetFrameSnapshotArray(),
                    VitalWarnings = SignalBus<VitalWarningSignal>.GetFrameSnapshotArray(),
                    CrushWarnings = SignalBus<CrushWarningSignal>.GetFrameSnapshotArray(),
                    Brownouts = SignalBus<BrownoutSignal>.GetFrameSnapshotArray(),
                    HealthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshotArray(),
                    RadiationSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshotArray(),
                    ToxicitySignals = toxicitySignals,
                    OxygenSignals = SignalBus<OxygenCriticalSignal>.GetFrameSnapshotArray(),
                    FloodSignals = SignalBus<SubmarineFloodStateSignal>.GetFrameSnapshotArray(),
                    FluidSignals = SignalBus<FluidIncursionSignal>.GetFrameSnapshotArray(),
                    PipeSignals = SignalBus<PipeRuptureSignal>.GetFrameSnapshotArray(),
                    BatterySignals = SignalBus<BatteryLevelSignal>.GetFrameSnapshotArray(),
                    SurvivalSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshotArray(),
                    ListenerAup = listenerAup,
                    PlayerToxicityTargetHash = _playerToxicityTargetHash != 0u ? _playerToxicityTargetHash : PlayerToxicityFallbackEntityHash,
                    PlayerSurvivalVitalsSourceId = _playerSurvivalVitalsSourceId,
                    TimeSeconds = _vwsClockSeconds,
                    DeltaSeconds = dt,
                    FallbackCooldownSeconds = ResolveCooldownSeconds(fallbackCooldownSeconds),
                    MaxEvaluations = maxEvaluations
                };

                long startTicks = Stopwatch.GetTimestamp();
                JobHandle evaluateHandle = evaluateJob.Schedule(dependsOn);

                EvaluateAlarmPriorityJob dispatchJob = new EvaluateAlarmPriorityJob
                {
                    Queue = views.Queue,
                    PriorityState = views.PriorityState,
                    CurrentState = views.CurrentState,
                    Dispatch = views.Dispatch,
                    Tuning = views.Tuning,
                    TimeSeconds = _vwsClockSeconds,
                    DeltaSeconds = dt,
                    QualityWeight01 = _globalQualityWeight01,
                    VesselCareTone01 = vesselCareTone01,
                    VoiceGain = voiceGain,
                    Frame = frame
                };
                JobHandle dispatchHandle = dispatchJob.Schedule(evaluateHandle);
                long scheduledTicks = Stopwatch.GetTimestamp();

                _pendingVocalWarningScheduleMicros = (float)((scheduledTicks - startTicks) * 1000000.0 / Stopwatch.Frequency);
                _pendingPresentationFrame = frame;
                _pendingVocalWarningJobHandle = dispatchHandle;
                _pendingVocalWarningGuardVault = guardVault;
                guardTransferred = true;
                Volatile.Write(ref _vocalWarningJobsPending, 1);
                Volatile.Write(ref _visualSyncPresentationPending, 1);
                H8Memory.RegisterActiveJob(VaultOwner, dispatchHandle);
                return dispatchHandle;
            }
            finally
            {
                if (!guardTransferred)
                    ReleaseVocalWarningFrameGuard(guardVault);
            }
        }

        private bool TryFinalizePendingVocalWarningJobsForPresentation()
        {
            if (Volatile.Read(ref _vocalWarningJobsPending) == 0)
                return true;

            JobHandle handle = _pendingVocalWarningJobHandle;
            if (!DispatcherJobFence.TryFinalizeCompleted(ref handle))
            {
                _pendingVocalWarningJobHandle = handle;
                return false;
            }

            _pendingVocalWarningJobHandle = default;
            Volatile.Write(ref _vocalWarningJobsPending, 0);
            ReleasePendingVocalWarningFrameGuard();
            _lastBurstExecutionMicros = math.max(0f, _pendingVocalWarningScheduleMicros);
            _pendingVocalWarningScheduleMicros = 0f;
            Volatile.Write(ref _visualSyncPresentationPending, 1);
            return true;
        }

        private void CompletePendingVocalWarningJobsForTeardown()
        {
            if (Interlocked.Exchange(ref _vocalWarningJobsPending, 0) == 0)
                return;

            JobHandle handle = _pendingVocalWarningJobHandle;
            DispatcherJobFence.BeginLateFrameSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndLateFrameSwapWindow();
            }

            _pendingVocalWarningJobHandle = default;
            _pendingVocalWarningScheduleMicros = 0f;
            ReleasePendingVocalWarningFrameGuard();
        }

        private bool TryAcquireVocalWarningFrameGuard(out IDataVault guardVault)
        {
            return TryAcquireVocalWarningFrameGuard(_dataVault, out guardVault);
        }

        private static bool TryAcquireVocalWarningFrameGuard(IDataVault vault, out IDataVault guardVault)
        {
            guardVault = vault;
            return guardVault != null &&
                   !guardVault.IsCompactionFenceActive &&
                   guardVault.TryAcquireMutationGuard(VocalWarningFrameMutationGuardMask);
        }

        private void ReleasePendingVocalWarningFrameGuard()
        {
            IDataVault guardVault = _pendingVocalWarningGuardVault;
            _pendingVocalWarningGuardVault = null;
            ReleaseVocalWarningFrameGuard(guardVault);
        }

        private static void ReleaseVocalWarningFrameGuard(IDataVault guardVault)
        {
            if (guardVault != null)
                guardVault.ReleaseMutationGuard(VocalWarningFrameMutationGuardMask);
        }

        private static ulong VocalWarningMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void CompletePresentationPhase(ref VwsVaultViews views, uint frame)
        {
            PublishDispatchIfNeeded(ref views, frame);
            PullCurrentState(ref views);
            WriteTelemetry(ref views, frame);

            FlushTelemetryDumpRequest();
        }

        private void VisualSyncPresentationTick()
        {
            if (!TryFinalizePendingVocalWarningJobsForPresentation())
                return;

            if (Interlocked.Exchange(ref _visualSyncPresentationPending, 0) == 0)
                return;

            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            if (!TryAcquireVocalWarningFrameGuard(out IDataVault guardVault))
            {
                Volatile.Write(ref _visualSyncPresentationPending, 1);
                return;
            }

            try
            {
                if (!TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views))
                {
                    Volatile.Write(ref _visualSyncPresentationPending, 1);
                    return;
                }

                uint presentationFrame = _pendingPresentationFrame;
                if (Interlocked.Exchange(ref _pendingCancelRequest, 0) != 0)
                    CancelRendererPlaybackAndClearQueues(ref views, false);

                CompletePresentationPhase(ref views, presentationFrame);
            }
            finally
            {
                ReleaseVocalWarningFrameGuard(guardVault);
            }
        }

        private uint NextOwnerFrameId()
        {
            unchecked
            {
                _ownerFrameCounter++;
                if (_ownerFrameCounter == 0u)
                    _ownerFrameCounter = 1u;
                return _ownerFrameCounter;
            }
        }

        private void PublishDispatchIfNeeded(ref VwsVaultViews views, uint frame)
        {
            if (!views.Dispatch.IsCreated || views.Dispatch.Length <= 0)
                return;

            VocalWarningDispatchDTO dispatch;
            unsafe
            {
                dispatch = NativeElementRef(views.Dispatch, 0);
            }
            if (dispatch.AudioBankHashID == 0u)
                return;

            VocalCueSignal cue = default;
            cue.PhraseHashID = dispatch.AudioBankHashID;
            cue.Priority = dispatch.CuePriority;
            cue.VolumeScalar = dispatch.VolumeScalar;
            cue.PlaybackSpeed = dispatch.PlaybackSpeed;
            cue.RadioDistortion01 = dispatch.RadioDistortion01;
            cue.SpatialBlend01 = dispatch.SpatialBlend01;
            cue.SourceAupGridX = dispatch.SourceAupGridX;
            cue.SourceAupGridY = dispatch.SourceAupGridY;
            cue.SourceAupGridZ = dispatch.SourceAupGridZ;
            cue.SourceAupLocalX = dispatch.SourceAupLocalX;
            cue.SourceAupLocalY = dispatch.SourceAupLocalY;
            cue.SourceAupLocalZ = dispatch.SourceAupLocalZ;
            cue.Flags = dispatch.Flags;
            bool cueAccepted = SignalBus<VocalCueSignal>.TryPushTracked(in cue, ref s_x001DirectSignalPushDropCount_VocalWarningSystem);
            uint publishFaults = cueAccepted ? 0u : FaultFlagVocalCueRejected;

            if (cueAccepted)
            {
                SubtitleCueSignal subtitle = default;
                subtitle.TokenHash = dispatch.AudioBankHashID;
                subtitle.StartAudioFrame = 0u;
                subtitle.DurationMilliseconds = ResolveSubtitleDurationMilliseconds(dispatch.DurationSeconds);
                subtitle.Priority = dispatch.SubtitlePriority;
                subtitle.Flags = ResolveSubtitleCueFlags(dispatch.Flags);
                subtitle.SourceHash = VaultOwnerSignalHash;
                if (!SignalBus<SubtitleCueSignal>.TryPushTracked(in subtitle, ref s_x001DirectSignalPushDropCount_VocalWarningSystem))
                    publishFaults |= FaultFlagSubtitleRejected;
            }

            if (publishFaults != 0u)
                MarkPriorityFault(ref views, publishFaults);

            if (!cueAccepted)
            {
                ClearCurrentState(ref views);
                unsafe
                {
                    NativeElementRef(views.Dispatch, 0) = default;
                }
                return;
            }

            _lastDispatchedAudioBankHashID = dispatch.AudioBankHashID;
            _lastDispatchedWarningId = dispatch.WarningId;
            _lastDirectionHash = dispatch.DirectionHash;
            _lastSourceAupGridX = dispatch.SourceAupGridX;
            _lastSourceAupGridY = dispatch.SourceAupGridY;
            _lastSourceAupGridZ = dispatch.SourceAupGridZ;
            _lastSourceAupLocalX = dispatch.SourceAupLocalX;
            _lastSourceAupLocalY = dispatch.SourceAupLocalY;
            _lastSourceAupLocalZ = dispatch.SourceAupLocalZ;
            unsafe
            {
                NativeElementRef(views.Dispatch, 0) = default;
            }
        }

        private static void MarkPriorityFault(ref VwsVaultViews views, uint faultFlags)
        {
            if (!views.PriorityState.IsCreated || views.PriorityState.Length <= 0 || faultFlags == 0u)
                return;

            unsafe
            {
                ref AlarmStateDTO state = ref NativeElementRef(views.PriorityState, 0);
                state.FaultFlags |= faultFlags;
            }
        }

        private static void ClearCurrentState(ref VwsVaultViews views)
        {
            if (!views.CurrentState.IsCreated || views.CurrentState.Length <= 0)
                return;

            unsafe
            {
                NativeElementRef(views.CurrentState, 0) = default;
            }
        }

        private void PullCurrentState(ref VwsVaultViews views)
        {
            _queueCount = ResolveActivePriorityCount(ref views);
            if (!views.CurrentState.IsCreated || views.CurrentState.Length <= 0)
            {
                _currentWarningId = 0;
                _currentAudioBankHashID = 0u;
                _currentPriorityScore = 0f;
                _warningPlaybackRemainingSeconds = 0f;
                return;
            }

            VocalWarningCurrentState state;
            unsafe
            {
                state = NativeElementRef(views.CurrentState, 0);
            }
            _currentAudioBankHashID = state.AudioBankHashID;
            _currentPriorityScore = state.PriorityScore;
            _warningPlaybackRemainingSeconds = math.max(0f, state.PlaybackRemainingSeconds);
            _currentWarningId = VocalWarningHashes.ToWarningId(state.AudioBankHashID);
            _lastInterruptCount = state.LastInterruptCount;
            if (_warningPlaybackRemainingSeconds <= 0f)
            {
                _currentWarningId = 0;
                _currentAudioBankHashID = 0u;
                _currentPriorityScore = 0f;
            }
        }

        private void CancelRendererPlaybackAndClearQueues()
        {
            if (!TryAcquireVocalWarningFrameGuard(out IDataVault guardVault))
            {
                ClearPresentationState(true);
                return;
            }

            try
            {
                if (TryResolveVwsOwnerViews(guardVault, out VwsVaultViews views))
                {
                    CancelRendererPlaybackAndClearQueues(ref views, true);
                    return;
                }

                ClearPresentationState(true);
            }
            finally
            {
                ReleaseVocalWarningFrameGuard(guardVault);
            }
        }

        private void CancelRendererPlaybackAndClearQueues(ref VwsVaultViews views, bool clearPendingFaults)
        {
            ClearPresentationState(clearPendingFaults);
            ClearQueuedWarnings(ref views);
        }

        private void ClearPresentationState(bool clearPendingFaults)
        {
            _queueCount = 0;
            _currentWarningId = 0;
            _currentAudioBankHashID = 0u;
            _currentPriorityScore = 0f;
            _warningPlaybackRemainingSeconds = 0f;
            Interlocked.Exchange(ref _pendingCancelRequest, 0);
            Interlocked.Exchange(ref _visualSyncPresentationPending, 0);
            if (clearPendingFaults)
                Interlocked.Exchange(ref _pendingExternalFaultFlags, 0);
            _pendingPresentationFrame = 0u;
            _lastSourceAupGridX = 0L;
            _lastSourceAupGridY = 0L;
            _lastSourceAupGridZ = 0L;
            _lastSourceAupLocalX = 0f;
            _lastSourceAupLocalY = 0f;
            _lastSourceAupLocalZ = 0f;
        }

        private void ClearLastDispatchRoute()
        {
            _lastDispatchedAudioBankHashID = 0u;
            _lastDispatchedWarningId = 0;
            _lastDirectionHash = 0;
            _lastSourceAupGridX = 0L;
            _lastSourceAupGridY = 0L;
            _lastSourceAupGridZ = 0L;
            _lastSourceAupLocalX = 0f;
            _lastSourceAupLocalY = 0f;
            _lastSourceAupLocalZ = 0f;
        }

        private static void ClearQueuedWarnings(ref VwsVaultViews views)
        {
            if (!views.Queue.IsCreated)
                return;

            unsafe
            {
                for (int i = 0; i < views.Queue.Length; i++)
                    NativeElementRef(views.Queue, i) = default;
                if (views.PriorityState.IsCreated && views.PriorityState.Length > 0)
                    NativeElementRef(views.PriorityState, 0) = default;
                if (views.CurrentState.IsCreated && views.CurrentState.Length > 0)
                    NativeElementRef(views.CurrentState, 0) = default;
                if (views.Dispatch.IsCreated && views.Dispatch.Length > 0)
                    NativeElementRef(views.Dispatch, 0) = default;
            }
        }

        private void WriteTelemetry(ref VwsVaultViews views, uint frame)
        {
            NativeArray<VwsTelemetryEntry> telemetryRing = views.TelemetryRing;
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int cursor = _telemetryCursor;
            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            AlarmStateDTO priorityState = default;
            VocalWarningCurrentState current = default;
            unsafe
            {
                if (views.PriorityState.IsCreated && views.PriorityState.Length > 0)
                    priorityState = NativeElementRef(views.PriorityState, 0);
                if (views.CurrentState.IsCreated && views.CurrentState.Length > 0)
                    current = NativeElementRef(views.CurrentState, 0);
            }
            uint faultFlags = priorityState.FaultFlags;
            if (!math.isfinite(_lastBurstExecutionMicros) ||
                !math.isfinite(current.PriorityScore) ||
                !math.isfinite(current.PlaybackRemainingSeconds))
            {
                faultFlags |= FaultFlagTelemetryInvalid;
            }

            if (faultFlags != 0u || _lastBurstExecutionMicros > 100f)
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);

            unsafe
            {
                NativeElementRef(telemetryRing, cursor) = new VwsTelemetryEntry
                {
                    Frame = frame,
                    ActivePriorityCount = priorityState.ActivePriorityCount,
                    ActiveAlarmsMask = priorityState.activeAlarmsMask,
                    CurrentAudioBankHashID = current.AudioBankHashID,
                    CurrentPriorityScore = current.PriorityScore,
                    BurstExecutionMicros = _lastBurstExecutionMicros,
                    FaultFlags = faultFlags,
                    CurrentWarningId = VocalWarningHashes.ToWarningId(current.AudioBankHashID),
                    LastDispatchedWarningId = _lastDispatchedWarningId,
                    DirectionHash = _lastDirectionHash,
                    HighestPriorityBitIndex = priorityState.HighestPriorityBitIndex,
                    SourceAupGridX = _lastSourceAupGridX,
                    SourceAupGridY = _lastSourceAupGridY,
                    SourceAupGridZ = _lastSourceAupGridZ
                };
            }

            cursor++;
            if (cursor >= telemetryRing.Length)
                cursor = 0;
            _telemetryCursor = cursor;
            if (_telemetrySamplesWritten < telemetryRing.Length)
                _telemetrySamplesWritten++;
        }

        private void FlushTelemetryDumpRequest()
        {
            if (Interlocked.Exchange(ref _telemetryDumpRequested, 0) == 0)
                return;

            DumpTelemetryCold();
        }

        private void DumpTelemetryCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryReadOnlyHandle(in _telemetryRingHandle, out NativeArray<VwsTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length <= 0)
                return;

            if (Volatile.Read(ref _telemetryDumped) != 0)
                return;

            try
            {
                int entryStride = UnsafeUtility.SizeOf<VwsTelemetryEntry>();
                int count = math.clamp(_telemetrySamplesWritten, 0, telemetryRing.Length);
                int cursor = math.clamp(_telemetryCursor, 0, telemetryRing.Length - 1);
                int startIndex = count < telemetryRing.Length ? 0 : cursor;
                VwsTelemetryDumpHeader header = new VwsTelemetryDumpHeader
                {
                    Magic = VocalWarningSystemHash,
                    Version = 2u,
                    EntryStrideBytes = (uint)entryStride,
                    Capacity = (uint)telemetryRing.Length,
                    Cursor = (uint)cursor,
                    EmittedCount = (uint)count,
                    RingStartIndex = (uint)startIndex,
                    Reserved0 = 0u
                };

                bool primaryWritten = WriteTelemetryDump(TelemetryDumpRelativePath, in header, telemetryRing, entryStride, count, startIndex, telemetryRing.Length);
                bool agentWritten = WriteTelemetryDump(AgentTelemetryDumpRelativePath, in header, telemetryRing, entryStride, count, startIndex, telemetryRing.Length);

                if (primaryWritten || agentWritten)
                    Interlocked.Exchange(ref _telemetryDumped, 1);
            }
            catch (Exception)
            {
            }
        }

        private static unsafe bool WriteTelemetryDump(
            string path,
            in VwsTelemetryDumpHeader header,
            NativeArray<VwsTelemetryEntry>.ReadOnly telemetryRing,
            int entryStride,
            int count,
            int startIndex,
            int capacity)
        {
            int headerBytes = UnsafeUtility.SizeOf<VwsTelemetryDumpHeader>();
            int byteCount = headerBytes + math.max(0, count) * entryStride;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(VocalWarningSystem),
                    TelemetryDumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                VwsTelemetryDumpHeader localHeader = header;
                UnsafeUtility.MemCpy(target, &localHeader, headerBytes);
                if (count > 0)
                {
                    byte* source = (byte*)telemetryRing.GetUnsafeReadOnlyPtr();
                    int firstCount = math.min(count, capacity - startIndex);
                    UnsafeUtility.MemCpy(target + headerBytes, source + startIndex * entryStride, firstCount * entryStride);

                    int secondCount = count - firstCount;
                    if (secondCount > 0)
                        UnsafeUtility.MemCpy(target + headerBytes + firstCount * entryStride, source, secondCount * entryStride);
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(VocalWarningSystem),
                    TelemetryDumpPayloadLabel);
            }
        }

        private static int ResolveActivePriorityCount(ref VwsVaultViews views)
        {
            if (!views.PriorityState.IsCreated || views.PriorityState.Length <= 0)
                return 0;

            AlarmStateDTO state;
            unsafe
            {
                state = NativeElementRef(views.PriorityState, 0);
            }
            return math.clamp((int)state.ActivePriorityCount, 0, views.Queue.IsCreated ? views.Queue.Length : 0);
        }

        private static int ResolveMaxEvaluations(float qualityWeight01, int capacity)
        {
            float t = math.saturate(math.select(1f, qualityWeight01, math.isfinite(qualityWeight01)));
            int value = (int)math.round(math.lerp(8f, 64f, t));
            return math.clamp(value, 0, math.max(0, capacity));
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float value = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        private static float SmoothQuality01(float quality)
        {
            float t = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            return t * t * (3f - 2f * t);
        }

        private float ResolveCooldownSeconds(float requestedCooldownSeconds)
        {
            float authoredFallback = math.isfinite(fallbackCooldownSeconds)
                ? fallbackCooldownSeconds
                : DefaultCooldownSeconds;
            float fallback = math.max(0f, authoredFallback);
            float value = requestedCooldownSeconds > 0f ? requestedCooldownSeconds : fallback;
            return math.isfinite(value) ? value : fallback;
        }

        private static float ResolveSeverity01(float severity01)
        {
            return math.isfinite(severity01) ? math.saturate(severity01) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint PackFlags(byte warningId, byte signalFlags, ushort directionHash, bool mock)
        {
            uint flags = ((uint)warningId << (int)PackedWarningIdShift) |
                         ((uint)directionHash << (int)PackedDirectionShift);
            if ((signalFlags & VocalWarningSignalFlags.HabitatIntegrityCompromised) != 0)
                flags |= QueueFlagHabitatIntegrity | QueueFlagCritical | QueueFlagInterrupt;
            if (warningId == (byte)VocalWarningId.HullBreach ||
                warningId == (byte)VocalWarningId.CrushDepth ||
                warningId == (byte)VocalWarningId.OxygenLow)
                flags |= QueueFlagCritical | QueueFlagInterrupt;
            if (directionHash != 0)
                flags |= QueueFlagDirectional;
            if (mock)
                flags |= QueueFlagMock;

            return flags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ExtractWarningId(uint flags)
        {
            return (byte)((flags >> (int)PackedWarningIdShift) & 0xFFu);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ExtractDirectionHash(uint flags)
        {
            return (ushort)((flags & PackedDirectionMask) >> (int)PackedDirectionShift);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ResolveSubtitleDurationMilliseconds(float durationSeconds)
        {
            float safeSeconds = math.max(0.001f, math.select(0.001f, durationSeconds, math.isfinite(durationSeconds)));
            float milliseconds = math.clamp(safeSeconds * 1000f, 1f, ushort.MaxValue);
            return (ushort)math.round(milliseconds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ResolveSubtitleCueFlags(uint queueFlags)
        {
            byte result = 0;
            if ((queueFlags & (QueueFlagCritical | QueueFlagInterrupt | QueueFlagPreempted)) != 0u)
                result |= SubtitleCueFlagInterrupt;

            ushort direction = ExtractDirectionHash(queueFlags);
            if (direction >= 0x4430u && direction <= 0x4437u)
            {
                int sector = (int)direction - 0x4430;
                if (sector == 0 || sector == 7)
                    result |= SubtitleCueFlagDirectionBehind;
                if (sector >= 1 && sector <= 3)
                    result |= SubtitleCueFlagDirectionLeft;
                if (sector >= 5 && sector <= 7)
                    result |= SubtitleCueFlagDirectionRight;
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte NormalizeWarningId(byte warningId)
        {
            return warningId >= (byte)VocalWarningId.CrushDepth && warningId <= (byte)VocalWarningId.Toxicity
                ? warningId
                : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolvePriorityBitIndex(byte warningId)
        {
            byte normalized = NormalizeWarningId(warningId);
            return normalized == 0 ? NoPriorityBitIndex : normalized - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsCriticalWarningId(byte warningId)
        {
            return warningId == (byte)VocalWarningId.HullBreach ||
                   warningId == (byte)VocalWarningId.CrushDepth ||
                   warningId == (byte)VocalWarningId.OxygenLow;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveHashFromIdOrHash(uint warningHash, byte idOrPriority)
        {
            if (warningHash != 0u)
                return warningHash;

            byte normalized = NormalizeWarningId(idOrPriority);
            return normalized == 0 ? 0u : VocalWarningHashes.FromWarningId(normalized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VocalWarningTuningDTO CreateDefaultTuning()
        {
            return new VocalWarningTuningDTO
            {
                BasePriorityHull = 1000f,
                BasePriorityCrush = 940f,
                BasePriorityOxygen = 820f,
                BasePriorityRadiation = 430f,
                BasePriorityPower = 120f,
                CriticalBoost = 220f,
                InterruptionThreshold = 180f,
                ProducerPriorityScale = 0.25f,
                SeverityBoost = 160f,
                DefaultBasePriority = 64f,
                Flags = 0u,
                Revision = 1u
            };
        }

        private static void InitializeDefaultProfiles(ref VwsVaultViews views)
        {
            if (!views.Profiles.IsCreated || views.Profiles.Length < CanonicalWarningCount)
                return;

            unsafe
            {
                NativeElementRef(views.Profiles, 0) = CreateDefaultProfile(
                    (byte)VocalWarningId.CrushDepth,
                    VocalWarningHashes.CrushDepth,
                    940f,
                    2.5f,
                    1.9f);
                NativeElementRef(views.Profiles, 1) = CreateDefaultProfile(
                    (byte)VocalWarningId.HullBreach,
                    VocalWarningHashes.HullBreach,
                    1000f,
                    3.5f,
                    2.1f);
                NativeElementRef(views.Profiles, 2) = CreateDefaultProfile(
                    (byte)VocalWarningId.OxygenLow,
                    VocalWarningHashes.OxygenLow,
                    820f,
                    2.5f,
                    1.65f);
                NativeElementRef(views.Profiles, 3) = CreateDefaultProfile(
                    (byte)VocalWarningId.Radiation,
                    VocalWarningHashes.Radiation,
                    430f,
                    4f,
                    1.35f);
                NativeElementRef(views.Profiles, 4) = CreateDefaultProfile(
                    (byte)VocalWarningId.PowerLow,
                    VocalWarningHashes.PowerLow,
                    120f,
                    6f,
                    1.15f);
                NativeElementRef(views.Profiles, 5) = CreateDefaultProfile(
                    (byte)VocalWarningId.Toxicity,
                    VocalWarningHashes.Toxicity,
                    360f,
                    4.5f,
                    1.25f);
            }
        }

        private static VocalWarningProfileDTO CreateDefaultProfile(
            byte warningId,
            uint audioBankHashID,
            float basePriority,
            float cooldownSeconds,
            float durationSeconds)
        {
            return new VocalWarningProfileDTO
            {
                AudioBankHashID = audioBankHashID,
                BasePriority = basePriority,
                CooldownSeconds = cooldownSeconds,
                DurationSeconds = durationSeconds,
                Flags = warningId
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VocalWarningTuningDTO ResolveTuning(NativeArray<VocalWarningTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return CreateDefaultTuning();

            return SanitizeTuning(tuning[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VocalWarningTuningDTO ResolveTuning(NativeArray<VocalWarningTuningDTO>.ReadOnly tuning)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return CreateDefaultTuning();

            return SanitizeTuning(tuning[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VocalWarningTuningDTO SanitizeTuning(in VocalWarningTuningDTO tuning)
        {
            VocalWarningTuningDTO fallback = CreateDefaultTuning();
            VocalWarningTuningDTO result = tuning;
            result.BasePriorityHull = math.isfinite(result.BasePriorityHull) && result.BasePriorityHull > 0f ? result.BasePriorityHull : fallback.BasePriorityHull;
            result.BasePriorityCrush = math.isfinite(result.BasePriorityCrush) && result.BasePriorityCrush > 0f ? result.BasePriorityCrush : fallback.BasePriorityCrush;
            result.BasePriorityOxygen = math.isfinite(result.BasePriorityOxygen) && result.BasePriorityOxygen > 0f ? result.BasePriorityOxygen : fallback.BasePriorityOxygen;
            result.BasePriorityRadiation = math.isfinite(result.BasePriorityRadiation) && result.BasePriorityRadiation > 0f ? result.BasePriorityRadiation : fallback.BasePriorityRadiation;
            result.BasePriorityPower = math.isfinite(result.BasePriorityPower) && result.BasePriorityPower > 0f ? result.BasePriorityPower : fallback.BasePriorityPower;
            result.CriticalBoost = math.isfinite(result.CriticalBoost) ? math.max(0f, result.CriticalBoost) : fallback.CriticalBoost;
            result.InterruptionThreshold = math.isfinite(result.InterruptionThreshold) ? math.max(0f, result.InterruptionThreshold) : fallback.InterruptionThreshold;
            result.ProducerPriorityScale = math.isfinite(result.ProducerPriorityScale) ? math.max(0f, result.ProducerPriorityScale) : fallback.ProducerPriorityScale;
            result.SeverityBoost = math.isfinite(result.SeverityBoost) ? math.max(0f, result.SeverityBoost) : fallback.SeverityBoost;
            result.DefaultBasePriority = math.isfinite(result.DefaultBasePriority) && result.DefaultBasePriority > 0f ? result.DefaultBasePriority : fallback.DefaultBasePriority;
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePriorityScore(uint warningHash, float severity01, int producerPriority, uint packedFlags, in VocalWarningTuningDTO tuning)
        {
            VocalWarningTuningDTO resolved = tuning;
            float basePriority;
            switch (warningHash)
            {
                case VocalWarningHashes.HullBreach:
                case VocalWarningHashes.HullTempCritical:
                    basePriority = resolved.BasePriorityHull;
                    break;
                case VocalWarningHashes.CrushDepth:
                    basePriority = resolved.BasePriorityCrush;
                    break;
                case VocalWarningHashes.OxygenLow:
                    basePriority = resolved.BasePriorityOxygen;
                    break;
                case VocalWarningHashes.Radiation:
                    basePriority = resolved.BasePriorityRadiation;
                    break;
                case VocalWarningHashes.PowerLow:
                    basePriority = resolved.BasePriorityPower;
                    break;
                case VocalWarningHashes.Toxicity:
                    basePriority = math.max(resolved.DefaultBasePriority, resolved.BasePriorityRadiation * 0.65f);
                    break;
                default:
                    basePriority = resolved.DefaultBasePriority;
                    break;
            }

            return ResolvePriorityScoreWithBase(basePriority, severity01, producerPriority, packedFlags, in resolved);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePriorityScoreFromProfiles(
            uint warningHash,
            float severity01,
            int producerPriority,
            uint packedFlags,
            in VocalWarningTuningDTO tuning,
            NativeArray<VocalWarningProfileDTO> profiles)
        {
            if (TryResolveProfile(warningHash, profiles, out VocalWarningProfileDTO profile) &&
                math.isfinite(profile.BasePriority) &&
                profile.BasePriority > 0f)
            {
                return ResolvePriorityScoreWithBase(profile.BasePriority, severity01, producerPriority, packedFlags, in tuning);
            }

            return ResolvePriorityScore(warningHash, severity01, producerPriority, packedFlags, in tuning);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveProfile(
            uint warningHash,
            NativeArray<VocalWarningProfileDTO> profiles,
            out VocalWarningProfileDTO profile)
        {
            profile = default;
            if (warningHash == 0u || !profiles.IsCreated)
                return false;

            int index = ResolvePriorityBitIndex(VocalWarningHashes.ToWarningId(warningHash));
            if ((uint)index >= (uint)profiles.Length)
                return false;

            VocalWarningProfileDTO candidate = profiles[index];
            if (candidate.AudioBankHashID != warningHash)
                return false;

            profile = candidate;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePriorityScoreWithBase(float basePriority, float severity01, int producerPriority, uint packedFlags, in VocalWarningTuningDTO tuning)
        {
            float severity = math.saturate(math.select(0f, severity01, math.isfinite(severity01)));
            float criticalBoost = (packedFlags & QueueFlagCritical) != 0u ? tuning.CriticalBoost : 0f;
            float producerBoost = math.clamp(producerPriority, 0, 255) * tuning.ProducerPriorityScale;
            return basePriority + severity * tuning.SeverityBoost + criticalBoost + producerBoost;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveExpirationSeconds(uint warningHash, float severity01)
        {
            float severity = math.saturate(math.select(0.5f, severity01, math.isfinite(severity01)));
            switch (warningHash)
            {
                case VocalWarningHashes.HullBreach:
                case VocalWarningHashes.HullTempCritical:
                    return math.lerp(3.5f, 6f, severity);
                case VocalWarningHashes.CrushDepth:
                case VocalWarningHashes.OxygenLow:
                    return math.lerp(2.5f, 5f, severity);
                case VocalWarningHashes.Toxicity:
                    return math.lerp(4f, 7.5f, severity);
                case VocalWarningHashes.PowerLow:
                    return math.lerp(6f, 12f, severity);
                default:
                    return math.lerp(4f, 8f, severity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDurationSeconds(uint warningHash, float priorityScore, float qualityWeight01)
        {
            float quality = SmoothQuality01(qualityWeight01);
            float normalizedPriority = math.saturate(priorityScore / 1400f);
            float baseDuration = math.lerp(1.05f, 2.2f, normalizedPriority);
            if (warningHash == VocalWarningHashes.HullBreach || warningHash == VocalWarningHashes.HullTempCritical)
                baseDuration += math.lerp(0.2f, 0.55f, quality);
            return baseDuration;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRadioDistortion01(uint warningHash, uint packedFlags, float qualityWeight01)
        {
            float quality = SmoothQuality01(qualityWeight01);
            if ((packedFlags & QueueFlagHabitatIntegrity) != 0u ||
                warningHash == VocalWarningHashes.HullBreach ||
                warningHash == VocalWarningHashes.HullTempCritical)
                return math.lerp(0.48f, 0.82f, quality);

            return math.lerp(0.22f, 0.38f, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveVwsSpatialBlend01(uint packedFlags, float qualityWeight01, in VocalWarningDTO warning)
        {
            if ((packedFlags & QueueFlagDirectional) == 0u || !HasFiniteSourceAup(in warning))
                return 0f;

            float quality = SmoothQuality01(qualityWeight01);
            return math.lerp(0f, 0.18f, quality * quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasFiniteSourceAup(in VocalWarningDTO warning)
        {
            float3 local = new float3(warning.SourceAupLocalX, warning.SourceAupLocalY, warning.SourceAupLocalZ);
            bool finite = math.all(math.isfinite(local));
            bool hasGrid = warning.SourceAupGridX != 0L || warning.SourceAupGridY != 0L || warning.SourceAupGridZ != 0L;
            bool hasLocal = math.lengthsq(local) > 0.000001f;
            return finite && (hasGrid || hasLocal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AbsoluteUniversePosition SanitizeSourceAup(in AbsoluteUniversePosition sourceAup)
        {
            float3 local = new float3(sourceAup.LocalX, sourceAup.LocalY, sourceAup.LocalZ);
            if (!math.all(math.isfinite(local)))
                return default;

            return sourceAup;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveCuePriority(int priorityBitIndex, float priorityScore)
        {
            int canonicalRank = math.clamp(priorityBitIndex + 1, 1, CanonicalWarningCount);
            int bandBase = ((canonicalRank - 1) * CuePriorityBandSize) + 1;
            int bandOffset = math.clamp(
                (int)math.round(math.saturate(priorityScore / 1400f) * (CuePriorityBandSize - 1)),
                0,
                CuePriorityBandSize - 1);
            return math.clamp(bandBase + bandOffset, 1, 255);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ushort ResolveCompassDirectionHash(in AbsoluteUniversePosition listenerAup, in AbsoluteUniversePosition threatAup)
        {
            double cell = AbsoluteUniversePosition.CellSizeMeters;
            double3 delta = new double3(
                (threatAup.GridX - listenerAup.GridX) * cell + ((double)threatAup.LocalX - listenerAup.LocalX),
                (threatAup.GridY - listenerAup.GridY) * cell + ((double)threatAup.LocalY - listenerAup.LocalY),
                (threatAup.GridZ - listenerAup.GridZ) * cell + ((double)threatAup.LocalZ - listenerAup.LocalZ));
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            if (!math.all(math.isfinite(local)) || math.lengthsq(local.xz) < 0.0001f)
                return 0;

            float angle = global::Hecton8.Core.MathLodApproximation.ApproxAtan2Fast(local.x, local.z);
            int sector = (int)math.floor((angle + math.PI) * (8.0f / (2.0f * math.PI)));
            sector = math.clamp(sector, 0, 7);
            return (ushort)(0x4430u + (uint)sector);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRadiationWarningSeverity01(in RadiationDoseSignal signal)
        {
            float intensity = ResolveSeverity01(signal.Intensity01);
            float dose = RadiationDoseSignal.DoseToUnit01(signal.Dose);
            return math.max(intensity, dose);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveToxicityWarningSeverity01(in ToxicityExposureSignal signal)
        {
            float exposure = math.saturate(math.select(0f, signal.Exposure01, math.isfinite(signal.Exposure01)));
            float toxemia = math.saturate(math.select(0f, signal.ToxemiaDelta, math.isfinite(signal.ToxemiaDelta)));
            return math.max(exposure, toxemia);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveToxicitySignalSourceAup(in ToxicityExposureSignal signal, out AbsoluteUniversePosition sourceAup)
        {
            sourceAup = default;
            if ((signal.Flags & ToxicityExposureSignal.FlagHasSourceAup) == 0)
                return false;
            if (!math.all(math.isfinite(signal.AUP)) || math.lengthsq(signal.AUP) <= 0.000001d)
                return false;

            sourceAup = AbsoluteUniversePosition.FromAbsolutePosition(signal.AUP);
            return AbsoluteUniversePosition.IsFinite(in sourceAup);
        }

#if UNITY_EDITOR
        internal static int ParseWarningProfiles(ReadOnlySpan<byte> bytes, NativeArray<VocalWarningProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length <= 0 || bytes.Length <= 0)
                return 0;

            int written = 0;
            int lineStart = 0;
            for (int cursor = 0; cursor <= bytes.Length && written < profiles.Length; cursor++)
            {
                if (cursor < bytes.Length && bytes[cursor] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> row = TrimAscii(bytes.Slice(lineStart, cursor - lineStart));
                lineStart = cursor + 1;
                if (row.Length == 0 || LooksLikeHeader(row))
                    continue;

                if (TryParseProfileRow(row, out VocalWarningProfileDTO profile))
                    profiles[written++] = profile;
            }

            return written;
        }

        private static bool TryParseProfileRow(ReadOnlySpan<byte> row, out VocalWarningProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadField(row, ref cursor, out ReadOnlySpan<byte> hashField) ||
                !TryReadField(row, ref cursor, out ReadOnlySpan<byte> priorityField) ||
                !TryReadField(row, ref cursor, out ReadOnlySpan<byte> cooldownField) ||
                !TryReadField(row, ref cursor, out ReadOnlySpan<byte> durationField))
                return false;

            if (!TryParseHexOrUInt(hashField, out uint hash) ||
                !TryParseFloat(priorityField, out float priority) ||
                !TryParseFloat(cooldownField, out float cooldown) ||
                !TryParseFloat(durationField, out float duration))
                return false;

            profile.AudioBankHashID = hash;
            profile.BasePriority = priority;
            profile.CooldownSeconds = cooldown;
            profile.DurationSeconds = duration;
            profile.Flags = 0u;
            return true;
        }

        private static bool TryReadField(ReadOnlySpan<byte> row, ref int cursor, out ReadOnlySpan<byte> field)
        {
            if (cursor > row.Length)
            {
                field = ReadOnlySpan<byte>.Empty;
                return false;
            }

            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;

            field = TrimAscii(row.Slice(start, cursor - start));
            cursor++;
            return true;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool LooksLikeHeader(ReadOnlySpan<byte> row)
        {
            if (row.Length <= 0)
                return false;
            byte first = row[0];
            return (first >= (byte)'A' && first <= (byte)'Z') ||
                   (first >= (byte)'a' && first <= (byte)'z');
        }

        private static bool TryParseHexOrUInt(ReadOnlySpan<byte> field, out uint value)
        {
            value = 0u;
            int start = field.Length > 2 && field[0] == (byte)'0' && (field[1] == (byte)'x' || field[1] == (byte)'X') ? 2 : 0;
            bool hex = start == 2;
            for (int i = start; i < field.Length; i++)
            {
                byte b = field[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (hex && b >= (byte)'A' && b <= (byte)'F')
                    digit = (uint)(10 + b - (byte)'A');
                else if (hex && b >= (byte)'a' && b <= (byte)'f')
                    digit = (uint)(10 + b - (byte)'a');
                else
                    return false;

                value = hex ? (value << 4) | digit : value * 10u + digit;
            }

            return field.Length > start;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> field, out float value)
        {
            value = 0f;
            if (field.Length <= 0)
                return false;

            int cursor = 0;
            float sign = 1f;
            if (field[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float whole = 0f;
            bool any = false;
            while (cursor < field.Length && field[cursor] >= (byte)'0' && field[cursor] <= (byte)'9')
            {
                any = true;
                whole = whole * 10f + (field[cursor] - (byte)'0');
                cursor++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (cursor < field.Length && field[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < field.Length && field[cursor] >= (byte)'0' && field[cursor] <= (byte)'9')
                {
                    any = true;
                    fraction = fraction * 10f + (field[cursor] - (byte)'0');
                    divisor *= 10f;
                    cursor++;
                }
            }

            if (!any || cursor != field.Length)
                return false;

            value = sign * (whole + fraction / divisor);
            return math.isfinite(value);
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockVocalThreatsJob : IJob
        {
            [NoAlias]
            public NativeArray<VocalWarningDTO> Queue;
            [NoAlias]
            public NativeArray<AlarmStateDTO> PriorityState;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningTuningDTO> Tuning;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningProfileDTO> Profiles;
            public float TimeSeconds;
            public uint Seed;
            public int Count;

            public unsafe void Execute()
            {
                uint state = math.max(1u, Seed);
                int count = math.clamp(Count, 1, 50);
                VocalWarningTuningDTO tuning = ResolveTuning(Tuning);
                AbsoluteUniversePosition sourceAup = default;
                for (int i = 0; i < count; i++)
                {
                    state = state * 1664525u + 1013904223u;
                    byte warningId = (byte)(1 + (state % (uint)CanonicalWarningCount));
                    uint hash = VocalWarningHashes.FromWarningId(warningId);
                    float severity = ((state >> 8) & 1023u) * (1f / 1023f);
                    uint flags = PackFlags(warningId, 0, 0, true);
                    VocalWarningDTO dto = new VocalWarningDTO
                    {
                        AudioBankHashID = hash,
                        PriorityScore = ResolvePriorityScoreFromProfiles(hash, severity, 0, flags, in tuning, Profiles),
                        ExpirationTime = TimeSeconds + ResolveExpirationSeconds(hash, severity),
                        Flags = flags,
                        SourceAupGridX = sourceAup.GridX,
                        SourceAupGridY = sourceAup.GridY,
                        SourceAupGridZ = sourceAup.GridZ,
                        SourceAupLocalX = sourceAup.LocalX,
                        SourceAupLocalY = sourceAup.LocalY,
                        SourceAupLocalZ = sourceAup.LocalZ
                    };
                    AlarmBitmaskOps.Insert(Queue, PriorityState, in dto);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateWarningPrioritiesJob : IJob
        {
            [NoAlias]
            public NativeArray<VocalWarningDTO> Queue;
            [NoAlias]
            public NativeArray<AlarmStateDTO> PriorityState;
            [NoAlias]
            public NativeArray<float> Cooldowns;
            [NoAlias]
            public NativeArray<byte> WarningFlags;
            [NoAlias]
            public NativeArray<float> WarningSeverity;
            [NoAlias]
            public NativeArray<uint> WarningSourceIds;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningTuningDTO> Tuning;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningProfileDTO> Profiles;
            [ReadOnly, NoAlias] public NativeArray<VocalWarningSignal>.ReadOnly VocalWarnings;
            [ReadOnly, NoAlias] public NativeArray<VitalWarningSignal>.ReadOnly VitalWarnings;
            [ReadOnly, NoAlias] public NativeArray<CrushWarningSignal>.ReadOnly CrushWarnings;
            [ReadOnly, NoAlias] public NativeArray<BrownoutSignal>.ReadOnly Brownouts;
            [ReadOnly, NoAlias] public NativeArray<SystemHealthIndexSignal>.ReadOnly HealthSignals;
            [ReadOnly, NoAlias] public NativeArray<RadiationDoseSignal>.ReadOnly RadiationSignals;
            [ReadOnly, NoAlias] public NativeArray<ToxicityExposureSignal>.ReadOnly ToxicitySignals;
            [ReadOnly, NoAlias] public NativeArray<OxygenCriticalSignal>.ReadOnly OxygenSignals;
            [ReadOnly, NoAlias] public NativeArray<SubmarineFloodStateSignal>.ReadOnly FloodSignals;
            [ReadOnly, NoAlias] public NativeArray<FluidIncursionSignal>.ReadOnly FluidSignals;
            [ReadOnly, NoAlias] public NativeArray<PipeRuptureSignal>.ReadOnly PipeSignals;
            [ReadOnly, NoAlias] public NativeArray<BatteryLevelSignal>.ReadOnly BatterySignals;
            [ReadOnly, NoAlias] public NativeArray<SurvivalVitalsChangedSignal>.ReadOnly SurvivalSignals;
            public AbsoluteUniversePosition ListenerAup;
            public uint PlayerToxicityTargetHash;
            public uint PlayerSurvivalVitalsSourceId;
            public float TimeSeconds;
            public float DeltaSeconds;
            public float FallbackCooldownSeconds;
            public int MaxEvaluations;

            public unsafe void Execute()
            {
                if (!Queue.IsCreated || !PriorityState.IsCreated || PriorityState.Length <= 0)
                    return;

                AlarmBitmaskOps.DiscardExpired(Queue, PriorityState, TimeSeconds);
                DecayCooldowns();
                VocalWarningTuningDTO tuning = ResolveTuning(Tuning);
                AbsoluteUniversePosition defaultSourceAup = default;

                int evaluations = 0;
                for (int i = 0; i < FloodSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    SubmarineFloodStateSignal signal = FloodSignals[i];
                    if ((signal.Flags & SubmarineFloodStateSignal.FlagCriticalFlood) == 0 && signal.FillRatio01 < 0.18f)
                        continue;

                    float severity = math.saturate(math.max(signal.FillRatio01, signal.TotalWaterMassKg / math.max(1f, signal.BaseMassKg)));
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.SourceBodyId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < FluidSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    FluidIncursionSignal signal = FluidSignals[i];
                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.LeakAup);
                    float severity = math.max(signal.FloodLevel01, signal.FlowRate01);
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.CompartmentId, in signal.LeakAup, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < PipeSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    PipeRuptureSignal signal = PipeSignals[i];
                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.RuptureAup);
                    float severity = math.saturate(signal.PressureKPa * (1f / 2000f));
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.NetworkId, in signal.RuptureAup, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < OxygenSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    OxygenCriticalSignal signal = OxygenSignals[i];
                    float severity = math.max(1f - math.saturate(signal.Oxygen01), signal.Severity * (1f / 255f));
                    if (TryQueue(VocalWarningHashes.OxygenLow, (byte)VocalWarningId.OxygenLow, severity, FallbackCooldownSeconds, signal.Flags, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < CrushWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    CrushWarningSignal signal = CrushWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, (byte)VocalWarningId.CrushDepth);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    if (TryQueue(hash, warningId, signal.Severity01, FallbackCooldownSeconds, signal.Flags, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < VocalWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    VocalWarningSignal signal = VocalWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, signal.Priority);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    if (TryQueue(hash, warningId, signal.Severity01, signal.CooldownSeconds, signal.Flags, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < VitalWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    VitalWarningSignal signal = VitalWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, (byte)VocalWarningId.OxygenLow);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    float severity = math.max(signal.Vital01, signal.Severity01);
                    if (TryQueue(hash, warningId, severity, FallbackCooldownSeconds, signal.Flags, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < Brownouts.Length && evaluations < MaxEvaluations; i++)
                {
                    BrownoutSignal signal = Brownouts[i];
                    float severity = math.max(signal.Severity01, 1f - math.saturate(signal.SupplyRatio));
                    if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, signal.Flags, signal.NetworkId, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < HealthSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    SystemHealthIndexSignal signal = HealthSignals[i];
                    if (signal.State != SystemHealthIndexSignal.StateCritical && signal.Pressure01 < 0.82f && signal.Health01 > 0.18f)
                        continue;

                    uint hash = signal.Pressure01 > 0.82f ? VocalWarningHashes.CrushDepth : VocalWarningHashes.PowerLow;
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    byte flags = signal.State == SystemHealthIndexSignal.StateCritical ? VocalWarningSignalFlags.HabitatIntegrityCompromised : (byte)0;
                    float severity = math.max(1f - math.saturate(signal.Health01), math.saturate(signal.Pressure01));
                    if (TryQueue(hash, warningId, severity, FallbackCooldownSeconds, flags, signal.SourceHash, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < RadiationSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    RadiationDoseSignal signal = RadiationSignals[i];
                    float severity = ResolveRadiationWarningSeverity01(in signal);
                    if (severity <= 0f)
                        continue;

                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.PositionAup);
                    if (TryQueue(VocalWarningHashes.Radiation, (byte)VocalWarningId.Radiation, severity, FallbackCooldownSeconds, 0, signal.SourceId, in signal.PositionAup, direction, false, in tuning))
                        evaluations++;
                }

                uint playerToxicityTargetHash = PlayerToxicityTargetHash != 0u ? PlayerToxicityTargetHash : PlayerToxicityFallbackEntityHash;
                for (int i = 0; i < ToxicitySignals.Length && evaluations < MaxEvaluations; i++)
                {
                    ToxicityExposureSignal signal = ToxicitySignals[i];
                    if (signal.EntityId == 0u)
                        continue;
                    if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)
                        continue;

                    float severity = ResolveToxicityWarningSeverity01(in signal);
                    if (severity <= ToxicityWarningMinSeverity01)
                        continue;

                    AbsoluteUniversePosition sourceAup = default;
                    ushort direction = TryResolveToxicitySignalSourceAup(in signal, out sourceAup)
                        ? ResolveCompassDirectionHash(in ListenerAup, in sourceAup)
                        : (ushort)0;
                    if (TryQueue(VocalWarningHashes.Toxicity, (byte)VocalWarningId.Toxicity, severity, FallbackCooldownSeconds, 0, signal.EntityId, in sourceAup, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < BatterySignals.Length && evaluations < MaxEvaluations; i++)
                {
                    BatteryLevelSignal signal = BatterySignals[i];
                    if (signal.BatteryPercent > 25)
                        continue;

                    float severity = math.saturate((25f - signal.BatteryPercent) * (1f / 25f));
                    if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, 0, signal.SourceHash, in defaultSourceAup, 0, false, in tuning))
                        evaluations++;
                }

                uint playerSurvivalVitalsSourceId = PlayerSurvivalVitalsSourceId;
                for (int i = 0; playerSurvivalVitalsSourceId != 0u && i < SurvivalSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    SurvivalVitalsChangedSignal signal = SurvivalSignals[i];
                    if (signal.SourceId != playerSurvivalVitalsSourceId)
                        continue;

                    uint survivalFlags = signal.Flags;
                    float oxygen01 = math.saturate(math.select(0f, signal.Oxygen01, math.isfinite(signal.Oxygen01)));
                    bool oxygenLow =
                        (survivalFlags & SurvivalVitalsChangedSignalFlags.OxygenCritical) != 0u ||
                        ((survivalFlags & SurvivalVitalsChangedSignalFlags.Oxygen) != 0u && oxygen01 < 0.22f);
                    if (oxygenLow)
                    {
                        float severity = 1f - oxygen01;
                        if (TryQueue(VocalWarningHashes.OxygenLow, (byte)VocalWarningId.OxygenLow, severity, FallbackCooldownSeconds, 0, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                            evaluations++;
                    }

                    if (evaluations >= MaxEvaluations)
                        break;

                    float energy01 = math.saturate(math.select(0f, signal.Energy01, math.isfinite(signal.Energy01)));
                    if ((survivalFlags & SurvivalVitalsChangedSignalFlags.Energy) != 0u && energy01 < 0.18f)
                    {
                        float severity = 1f - energy01;
                        if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, 0, signal.SourceId, in defaultSourceAup, 0, false, in tuning))
                            evaluations++;
                    }
                }
            }

            private unsafe void DecayCooldowns()
            {
                if (!Cooldowns.IsCreated)
                    return;

                float dt = math.max(0f, DeltaSeconds);
                for (int i = 0; i < Cooldowns.Length; i++)
                {
                    ref float cooldown = ref CooldownRef(i);
                    cooldown = math.max(0f, cooldown - dt);
                }
            }

            private unsafe bool TryQueue(uint hash, byte warningId, float severity01, float cooldownSeconds, byte signalFlags, uint sourceId, in AbsoluteUniversePosition sourceAup, ushort directionHash, bool mock, in VocalWarningTuningDTO tuning)
            {
                warningId = NormalizeWarningId(warningId);
                if (hash == 0u || warningId == 0)
                    return false;

                if (!Cooldowns.IsCreated || warningId >= Cooldowns.Length)
                    return false;

                uint packedFlags = PackFlags(warningId, signalFlags, directionHash, mock);
                float severity = ResolveSeverity01(severity01);
                float priorityScore = ResolvePriorityScoreFromProfiles(hash, severity, 0, packedFlags, in tuning, Profiles);
                if (!math.isfinite(priorityScore))
                {
                    MarkFault(FaultFlagPriorityInvalid);
                    return false;
                }

                ref float cooldown = ref CooldownRef(warningId);
                if (cooldown > 0f && !IsCriticalWarningId(warningId))
                    return false;

                cooldown = math.max(0f, math.select(FallbackCooldownSeconds, cooldownSeconds, cooldownSeconds > 0f && math.isfinite(cooldownSeconds)));
                if (WarningFlags.IsCreated && warningId < WarningFlags.Length)
                    WarningFlagRef(warningId) = signalFlags;
                if (WarningSeverity.IsCreated && warningId < WarningSeverity.Length)
                    WarningSeverityRef(warningId) = severity;
                if (WarningSourceIds.IsCreated && warningId < WarningSourceIds.Length)
                    WarningSourceIdRef(warningId) = sourceId;

                AbsoluteUniversePosition safeSourceAup = SanitizeSourceAup(in sourceAup);
                VocalWarningDTO dto = new VocalWarningDTO
                {
                    AudioBankHashID = hash,
                    PriorityScore = priorityScore,
                    ExpirationTime = TimeSeconds + ResolveExpirationSeconds(hash, severity),
                    Flags = packedFlags,
                    SourceAupGridX = safeSourceAup.GridX,
                    SourceAupGridY = safeSourceAup.GridY,
                    SourceAupGridZ = safeSourceAup.GridZ,
                    SourceAupLocalX = safeSourceAup.LocalX,
                    SourceAupLocalY = safeSourceAup.LocalY,
                    SourceAupLocalZ = safeSourceAup.LocalZ,
                    SourceId = sourceId
                };
                return AlarmBitmaskOps.Insert(Queue, PriorityState, in dto);
            }

            private unsafe void MarkFault(uint fault)
            {
                if (!PriorityState.IsCreated || PriorityState.Length <= 0)
                    return;

                ref AlarmStateDTO state = ref PriorityStateRef();
                state.FaultFlags |= fault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref AlarmStateDTO PriorityStateRef()
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(PriorityState);
                return ref UnsafeUtility.AsRef<AlarmStateDTO>(pointer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref float CooldownRef(int index)
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Cooldowns);
                return ref UnsafeUtility.AsRef<float>((byte*)pointer + (index * UnsafeUtility.SizeOf<float>()));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref byte WarningFlagRef(int index)
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(WarningFlags);
                return ref UnsafeUtility.AsRef<byte>((byte*)pointer + index);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref float WarningSeverityRef(int index)
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(WarningSeverity);
                return ref UnsafeUtility.AsRef<float>((byte*)pointer + (index * UnsafeUtility.SizeOf<float>()));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref uint WarningSourceIdRef(int index)
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(WarningSourceIds);
                return ref UnsafeUtility.AsRef<uint>((byte*)pointer + (index * UnsafeUtility.SizeOf<uint>()));
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateAlarmPriorityJob : IJob
        {
            [NoAlias]
            public NativeArray<VocalWarningDTO> Queue;
            [NoAlias]
            public NativeArray<AlarmStateDTO> PriorityState;
            [NoAlias]
            public NativeArray<VocalWarningCurrentState> CurrentState;
            [NoAlias]
            public NativeArray<VocalWarningDispatchDTO> Dispatch;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningTuningDTO> Tuning;
            public float TimeSeconds;
            public float DeltaSeconds;
            public float QualityWeight01;
            public float VesselCareTone01;
            public float VoiceGain;
            public uint Frame;

            public unsafe void Execute()
            {
                if (!Queue.IsCreated || !PriorityState.IsCreated || PriorityState.Length <= 0 ||
                    !CurrentState.IsCreated || CurrentState.Length <= 0 ||
                    !Dispatch.IsCreated || Dispatch.Length <= 0)
                    return;

                ref VocalWarningDispatchDTO dispatch = ref DispatchRef();
                dispatch = default;
                AlarmBitmaskOps.DiscardExpired(Queue, PriorityState, TimeSeconds);

                ref VocalWarningCurrentState currentSlot = ref CurrentStateRef();
                VocalWarningCurrentState current = currentSlot;
                current.PlaybackRemainingSeconds = math.max(0f, current.PlaybackRemainingSeconds - math.max(0f, DeltaSeconds));
                if (current.PlaybackRemainingSeconds <= 0f)
                {
                    current.AudioBankHashID = 0u;
                    current.PriorityScore = 0f;
                    current.Flags = 0u;
                }

                VocalWarningTuningDTO tuning = ResolveTuning(Tuning);
                if (!AlarmBitmaskOps.Peek(Queue, PriorityState, out VocalWarningDTO candidate, out int candidateBitIndex))
                {
                    currentSlot = current;
                    return;
                }

                bool active = current.AudioBankHashID != 0u && current.PlaybackRemainingSeconds > 0f;
                int currentBitIndex = VocalWarningSystem.ResolvePriorityBitIndex(VocalWarningHashes.ToWarningId(current.AudioBankHashID));
                bool higherPriorityBit = active && candidateBitIndex < currentBitIndex;
                bool canInterrupt = active &&
                                    (higherPriorityBit ||
                                     (candidate.PriorityScore > current.PriorityScore + tuning.InterruptionThreshold &&
                                      (candidate.Flags & (QueueFlagCritical | QueueFlagInterrupt)) != 0u));
                if (active && !canInterrupt)
                {
                    currentSlot = current;
                    return;
                }

                if (!AlarmBitmaskOps.Pop(Queue, PriorityState, out candidate, out candidateBitIndex))
                {
                    currentSlot = current;
                    return;
                }

                uint flags = candidate.Flags;
                if (canInterrupt)
                {
                    flags |= QueueFlagPreempted;
                    current.LastInterruptCount++;
                }

                float duration = ResolveDurationSeconds(candidate.AudioBankHashID, candidate.PriorityScore, QualityWeight01);
                float vesselCareTone = math.saturate(math.select(0f, VesselCareTone01, math.isfinite(VesselCareTone01)));
                float distortion = ResolveRadioDistortion01(candidate.AudioBankHashID, flags, QualityWeight01) *
                                   math.lerp(1f, 0.72f, vesselCareTone);
                float playbackSpeed = math.lerp(0.985f, 1.015f, vesselCareTone);
                byte warningId = ExtractWarningId(candidate.Flags);
                ushort directionHash = ExtractDirectionHash(candidate.Flags);
                current.AudioBankHashID = candidate.AudioBankHashID;
                current.PriorityScore = candidate.PriorityScore;
                current.PlaybackRemainingSeconds = duration;
                current.Flags = flags;
                current.LastDurationSeconds = duration;
                current.LastRadioDistortion01 = distortion;
                current.LastDispatchFrame = Frame;
                current.LastSubtitleHash = candidate.AudioBankHashID;
                current.LastDirectionHash = directionHash;
                current.QualityWeight01 = QualityWeight01;
                currentSlot = current;

                dispatch = new VocalWarningDispatchDTO
                {
                    AudioBankHashID = candidate.AudioBankHashID,
                    CuePriority = ResolveCuePriority(candidateBitIndex, candidate.PriorityScore),
                    VolumeScalar = math.saturate(math.select(DefaultGain, VoiceGain, math.isfinite(VoiceGain))),
                    PlaybackSpeed = playbackSpeed,
                    RadioDistortion01 = distortion,
                    SpatialBlend01 = ResolveVwsSpatialBlend01(flags, QualityWeight01, in candidate),
                    SourceAupGridX = candidate.SourceAupGridX,
                    SourceAupGridY = candidate.SourceAupGridY,
                    SourceAupGridZ = candidate.SourceAupGridZ,
                    SourceAupLocalX = candidate.SourceAupLocalX,
                    SourceAupLocalY = candidate.SourceAupLocalY,
                    SourceAupLocalZ = candidate.SourceAupLocalZ,
                    Flags = flags,
                    DurationSeconds = duration,
                    SubtitlePriority = (byte)math.clamp(ResolveCuePriority(candidateBitIndex, candidate.PriorityScore), 0, 255),
                    WarningId = warningId,
                    DirectionHash = directionHash,
                    Frame = Frame
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref VocalWarningCurrentState CurrentStateRef()
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(CurrentState);
                return ref UnsafeUtility.AsRef<VocalWarningCurrentState>(pointer);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref VocalWarningDispatchDTO DispatchRef()
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Dispatch);
                return ref UnsafeUtility.AsRef<VocalWarningDispatchDTO>(pointer);
            }
        }

        private static class AlarmBitmaskOps
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Insert(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<AlarmStateDTO> priorityState,
                in VocalWarningDTO value)
            {
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < AlarmBitCount)
                    return false;

                if (value.AudioBankHashID == 0u || !math.isfinite(value.PriorityScore) || !math.isfinite(value.ExpirationTime))
                {
                    MarkFault(priorityState, FaultFlagPriorityInputInvalid);
                    return false;
                }

                int bitIndex = ResolvePriorityBitIndex(ExtractWarningId(value.Flags));
                if (bitIndex == NoPriorityBitIndex)
                    bitIndex = ResolvePriorityBitIndex(VocalWarningHashes.ToWarningId(value.AudioBankHashID));
                if ((uint)bitIndex >= AlarmBitCount)
                {
                    MarkFault(priorityState, FaultFlagPriorityInputInvalid | FaultFlagAlarmMaskOverflow);
                    return false;
                }

                ref AlarmStateDTO state = ref StateRef(priorityState);
                ulong bitMask = 1UL << bitIndex;
                ref VocalWarningDTO slot = ref NodeRef(queue, bitIndex);
                bool occupied = (state.activeAlarmsMask & bitMask) != 0UL && slot.AudioBankHashID != 0u;
                if (!occupied || HigherPriorityThan(in value, in slot))
                {
                    slot = value;
                }
                else
                {
                    slot.ExpirationTime = math.max(slot.ExpirationTime, value.ExpirationTime);
                    slot.Flags |= value.Flags & (QueueFlagCritical | QueueFlagInterrupt | QueueFlagHabitatIntegrity | QueueFlagDirectional | QueueFlagMock);
                }

                state.activeAlarmsMask |= bitMask;
                state.ActivePriorityCount = (uint)CountBits64(state.activeAlarmsMask);
                state.LastAcceptedBitIndex = (uint)bitIndex;
                state.HighestPriorityBitIndex = (uint)ResolveHighestPriorityBitIndex(state.activeAlarmsMask);
                state.Sequence++;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Peek(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<AlarmStateDTO> priorityState,
                out VocalWarningDTO value,
                out int bitIndex)
            {
                value = default;
                bitIndex = NoPriorityBitIndex;
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < AlarmBitCount)
                    return false;

                AlarmStateDTO state = StateRef(priorityState);
                bitIndex = ResolveHighestPriorityBitIndex(state.activeAlarmsMask);
                if ((uint)bitIndex >= AlarmBitCount)
                    return false;

                value = NodeRef(queue, bitIndex);
                return value.AudioBankHashID != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Pop(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<AlarmStateDTO> priorityState,
                out VocalWarningDTO value,
                out int bitIndex)
            {
                value = default;
                bitIndex = NoPriorityBitIndex;
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < AlarmBitCount)
                    return false;

                ref AlarmStateDTO state = ref StateRef(priorityState);
                bitIndex = ResolveHighestPriorityBitIndex(state.activeAlarmsMask);
                if ((uint)bitIndex >= AlarmBitCount)
                    return false;

                ulong bitMask = 1UL << bitIndex;
                value = NodeRef(queue, bitIndex);
                NodeRef(queue, bitIndex) = default;
                state.activeAlarmsMask &= ~bitMask;
                state.ActivePriorityCount = (uint)CountBits64(state.activeAlarmsMask);
                state.HighestPriorityBitIndex = ResolveHighestPriorityBitIndexOrMax(state.activeAlarmsMask);
                state.Sequence++;
                return value.AudioBankHashID != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DiscardExpired(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<AlarmStateDTO> priorityState,
                float timeSeconds)
            {
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < AlarmBitCount)
                    return;

                ref AlarmStateDTO state = ref StateRef(priorityState);
                ulong activeWord = state.activeAlarmsMask;
                ulong scanWord = activeWord;
                uint discarded = 0u;
                while (scanWord != 0UL)
                {
                    int bitIndex = ResolveHighestPriorityBitIndex(scanWord);
                    ulong bitMask = 1UL << bitIndex;
                    scanWord &= ~bitMask;
                    VocalWarningDTO value = NodeRef(queue, bitIndex);
                    if (value.AudioBankHashID != 0u && value.ExpirationTime > timeSeconds)
                        continue;

                    NodeRef(queue, bitIndex) = default;
                    activeWord &= ~bitMask;
                    discarded++;
                }

                if (discarded == 0u)
                    return;

                state.activeAlarmsMask = activeWord;
                state.ActivePriorityCount = (uint)CountBits64(activeWord);
                state.DiscardedExpired += discarded;
                state.HighestPriorityBitIndex = ResolveHighestPriorityBitIndexOrMax(activeWord);
                state.Sequence++;
            }

            public static bool TryGetByPriorityOrder(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<AlarmStateDTO> priorityState,
                int priorityOrderIndex,
                out VocalWarningDTO value)
            {
                value = default;
                if (priorityOrderIndex < 0 ||
                    !queue.IsCreated ||
                    !priorityState.IsCreated ||
                    priorityState.Length <= 0 ||
                    queue.Length < AlarmBitCount)
                {
                    return false;
                }

                ulong scanWord = StateRef(priorityState).activeAlarmsMask;
                int order = 0;
                while (scanWord != 0UL)
                {
                    int bitIndex = ResolveHighestPriorityBitIndex(scanWord);
                    ulong bitMask = 1UL << bitIndex;
                    scanWord &= ~bitMask;
                    if (order == priorityOrderIndex)
                    {
                        value = NodeRef(queue, bitIndex);
                        return value.AudioBankHashID != 0u;
                    }

                    order++;
                }

                return false;
            }

            public static bool TryGetByPriorityOrder(
                NativeArray<VocalWarningDTO>.ReadOnly queue,
                NativeArray<AlarmStateDTO>.ReadOnly priorityState,
                int priorityOrderIndex,
                out VocalWarningDTO value)
            {
                value = default;
                if (priorityOrderIndex < 0 ||
                    !queue.IsCreated ||
                    !priorityState.IsCreated ||
                    priorityState.Length <= 0 ||
                    queue.Length < AlarmBitCount)
                {
                    return false;
                }

                ulong scanWord = priorityState[0].activeAlarmsMask;
                int order = 0;
                while (scanWord != 0UL)
                {
                    int bitIndex = ResolveHighestPriorityBitIndex(scanWord);
                    ulong bitMask = 1UL << bitIndex;
                    scanWord &= ~bitMask;
                    if (order == priorityOrderIndex)
                    {
                        value = queue[bitIndex];
                        return value.AudioBankHashID != 0u;
                    }

                    order++;
                }

                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void MarkFault(NativeArray<AlarmStateDTO> priorityState, uint fault)
            {
                if (!priorityState.IsCreated || priorityState.Length <= 0)
                    return;

                ref AlarmStateDTO state = ref StateRef(priorityState);
                state.FaultFlags |= fault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveHighestPriorityBitIndex(ulong activeAlarmsMask)
            {
                uint low = (uint)activeAlarmsMask;
                uint high = (uint)(activeAlarmsMask >> 32);
                bool useLow = low != 0u;
                bool hasAny = activeAlarmsMask != 0UL;
                uint selected = math.select(1u, math.select(high, low, useLow), hasAny);
                int baseIndex = math.select(32, 0, useLow);
                int candidateIndex = baseIndex + math.tzcnt(selected);
                return math.select(NoPriorityBitIndex, candidateIndex, hasAny);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static uint ResolveHighestPriorityBitIndexOrMax(ulong activeAlarmsMask)
            {
                int bitIndex = ResolveHighestPriorityBitIndex(activeAlarmsMask);
                return (uint)math.select(-1, bitIndex, activeAlarmsMask != 0UL);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int CountBits64(ulong value)
            {
                return math.countbits((uint)value) + math.countbits((uint)(value >> 32));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ref VocalWarningDTO NodeRef(NativeArray<VocalWarningDTO> queue, int index)
            {
                unsafe
                {
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(queue);
                    return ref UnsafeUtility.AsRef<VocalWarningDTO>(basePtr + index * UnsafeUtility.SizeOf<VocalWarningDTO>());
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static ref AlarmStateDTO StateRef(NativeArray<AlarmStateDTO> priorityState)
            {
                unsafe
                {
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(priorityState);
                    return ref UnsafeUtility.AsRef<AlarmStateDTO>(basePtr);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HigherPriorityThan(in VocalWarningDTO a, in VocalWarningDTO b)
            {
                if (a.PriorityScore > b.PriorityScore)
                    return true;
                if (a.PriorityScore < b.PriorityScore)
                    return false;
                return a.ExpirationTime < b.ExpirationTime;
            }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly VocalWarningSystem _owner;

            public SimulationPhaseSystem(VocalWarningSystem owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VocalWarningSystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
            {
                return _owner.ScheduleVocalWarningFrame(timing.FrameDelta, timing.FrameId, dependsOn);
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly VocalWarningSystem _owner;

            public VisualSyncPhaseSystem(VocalWarningSystem owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VocalWarningSystemHash ^ 0x5653594Eu; // VSYN
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.VisualSync;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
                _owner.VisualSyncPresentationTick();
            }
        }
    }
}
