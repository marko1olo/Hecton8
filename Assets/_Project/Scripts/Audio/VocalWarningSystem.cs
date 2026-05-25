using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
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
    public sealed class VocalWarningSystem : MonoBehaviour, IVocalWarningSystem, IUpdatable, ISlowTickable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        internal struct VocalWarningDTO
        {
            [FieldOffset(0)] public uint AudioBankHashID;
            [FieldOffset(4)] public float PriorityScore;
            [FieldOffset(8)] public float ExpirationTime;
            [FieldOffset(12)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VocalWarningPriorityState
        {
            [FieldOffset(0)] public ulong VwsPriorityWord;
            [FieldOffset(8)] public uint ActivePriorityCount;
            [FieldOffset(12)] public uint DiscardedExpired;
            [FieldOffset(16)] public uint Sequence;
            [FieldOffset(20)] public uint FaultFlags;
            [FieldOffset(24)] public uint LastAcceptedBitIndex;
            [FieldOffset(28)] public uint HighestPriorityBitIndex;
            [FieldOffset(32)] public uint LastRejectedBitIndex;
            [FieldOffset(36)] public uint SaturationCount;
            [FieldOffset(40)] public ulong _pad0;
            [FieldOffset(48)] public ulong _pad1;
            [FieldOffset(56)] public ulong _pad2;
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
            [FieldOffset(44)] public uint _pad0;
            [FieldOffset(48)] public ulong _pad1;
            [FieldOffset(56)] public ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 80)]
        private struct VocalWarningDispatchDTO
        {
            [FieldOffset(0)] public uint AudioBankHashID;
            [FieldOffset(4)] public int CuePriority;
            [FieldOffset(8)] public float VolumeScalar;
            [FieldOffset(12)] public float PlaybackSpeed;
            [FieldOffset(16)] public float RadioDistortion01;
            [FieldOffset(20)] public float SpatialBlend01;
            [FieldOffset(24)] public long SourceAupGridX;
            [FieldOffset(32)] public long SourceAupGridY;
            [FieldOffset(40)] public long SourceAupGridZ;
            [FieldOffset(48)] public float SourceAupLocalX;
            [FieldOffset(52)] public float SourceAupLocalY;
            [FieldOffset(56)] public float SourceAupLocalZ;
            [FieldOffset(60)] public uint Flags;
            [FieldOffset(64)] public float DurationSeconds;
            [FieldOffset(68)] public byte SubtitlePriority;
            [FieldOffset(69)] public byte WarningId;
            [FieldOffset(70)] public ushort DirectionHash;
            [FieldOffset(72)] public uint Frame;
            [FieldOffset(76)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct VwsTelemetryEntry
        {
            [FieldOffset(0)] public uint Frame;
            [FieldOffset(4)] public uint ActivePriorityCount;
            [FieldOffset(8)] public ulong ActivePriorityWord;
            [FieldOffset(16)] public uint CurrentAudioBankHashID;
            [FieldOffset(20)] public uint LastDispatchedAudioBankHashID;
            [FieldOffset(24)] public float CurrentPriorityScore;
            [FieldOffset(28)] public float ActiveRemainingSeconds;
            [FieldOffset(32)] public float BurstExecutionMicros;
            [FieldOffset(36)] public uint ExpiredDiscardCount;
            [FieldOffset(40)] public uint FaultFlags;
            [FieldOffset(44)] public uint InterruptCount;
            [FieldOffset(48)] public float QualityWeight01;
            [FieldOffset(52)] public byte CurrentWarningId;
            [FieldOffset(53)] public byte LastDispatchedWarningId;
            [FieldOffset(54)] public ushort DirectionHash;
            [FieldOffset(56)] public uint HighestPriorityBitIndex;
            [FieldOffset(60)] public uint SubtitleFrameLatency;
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
            [FieldOffset(24)] public ulong _pad0;
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
            [FieldOffset(48)] private ulong _pad0;
            [FieldOffset(56)] private ulong _pad1;
        }

        public struct VocalWarningTelemetrySnapshot
        {
            public uint Frame;
            public uint ActivePriorityCount;
            public ulong ActivePriorityWord;
            public uint CurrentAudioBankHashID;
            public float CurrentPriorityScore;
            public float BurstExecutionMicros;
            public uint ExpiredDiscardCount;
            public uint FaultFlags;
            public uint InterruptCount;
        }

        private struct VwsVaultViews
        {
            public NativeArray<VocalWarningDTO> Queue;
            public NativeArray<VocalWarningPriorityState> PriorityState;
            public NativeArray<byte> WarningFlags;
            public NativeArray<float> Cooldowns;
            public NativeArray<float> WarningSeverity;
            public NativeArray<uint> WarningSourceIds;
            public NativeArray<VocalWarningCurrentState> CurrentState;
            public NativeArray<VocalWarningDispatchDTO> Dispatch;
            public NativeArray<VocalWarningProfileDTO> Profiles;
            public NativeArray<VocalWarningTuningDTO> Tuning;
#if UNITY_EDITOR
            public NativeArray<byte> CsvScratch;
#endif
            public NativeArray<VwsTelemetryEntry> TelemetryRing;
        }

        private const int QueueCapacity = 64;
        private const int WarningStateLength = 6;
        private const int DispatchLength = 1;
        private const int ProfileCapacity = 8;
#if UNITY_EDITOR
        private const int CsvScratchCapacity = 4096;
#endif
        private const int TelemetryCapacity = 300;
        private const float DefaultCooldownSeconds = 4f;
        private const float DefaultGain = 0.85f;
        private const uint VocalWarningSystemHash = 0x56333532u; // V352
        private const uint VaultOwnerSignalHash = 0x41565753u; // AVWS
        private const BufferID VocalWarningPriorityStateBufferId = (BufferID)72430;
        private const BufferID VocalWarningCurrentStateBufferId = (BufferID)72431;
        private const BufferID VocalWarningDispatchBufferId = (BufferID)72432;
        private const BufferID VocalWarningProfilesBufferId = (BufferID)72433;
#if UNITY_EDITOR
        private const BufferID VocalWarningCsvScratchBufferId = (BufferID)72434;
#endif
        private const BufferID VocalWarningTuningBufferId = (BufferID)72435;
        private const uint QueueFlagCritical = 1u << 0;
        private const uint QueueFlagInterrupt = 1u << 1;
        private const uint QueueFlagHabitatIntegrity = 1u << 2;
        private const uint QueueFlagDirectional = 1u << 3;
        private const uint QueueFlagMock = 1u << 4;
        private const uint QueueFlagPreempted = 1u << 5;
        private const int PriorityWordBitCount = 64;
        private const int NoPriorityBitIndex = -1;
        private const int LowestCanonicalWarningId = (int)VocalWarningId.CrushDepth;
        private const int HighestCanonicalWarningId = (int)VocalWarningId.PowerLow;
        private const int CanonicalWarningCount = HighestCanonicalWarningId - LowestCanonicalWarningId + 1;
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
        private const uint PackedWarningIdShift = 8;
        private const uint PackedDirectionShift = 16;
        private const uint PackedDirectionMask = 0xFFFFu << (int)PackedDirectionShift;
        private const SystemID VaultOwner = SystemID.AudioVocalWarning;
        private const string TelemetryDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_352_VWS.bin";
        private const string AgentTelemetryDumpRelativePath = "Docs/AgentLogs/Dump_X_011.bin";

        [Header("Mix")]
        [Tooltip("Voice gain applied before the procedural renderer safety limiter.")]
        [SerializeField, Range(0f, 1f)] private float voiceGain = DefaultGain;
        [Tooltip("Cooldown used when a producer does not provide a positive finite cooldown.")]
        [SerializeField, Min(0f)] private float fallbackCooldownSeconds = DefaultCooldownSeconds;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VocalWarningDTO> _vwsQueueHandle;
        private VaultGenerationHandle<VocalWarningPriorityState> _priorityStateHandle;
        private VaultGenerationHandle<byte> _warningFlagsHandle;
        private VaultGenerationHandle<float> _cooldownsHandle;
        private VaultGenerationHandle<float> _warningSeverityHandle;
        private VaultGenerationHandle<uint> _warningSourceIdsHandle;
        private VaultGenerationHandle<VocalWarningCurrentState> _currentStateHandle;
        private VaultGenerationHandle<VocalWarningDispatchDTO> _dispatchHandle;
        private VaultGenerationHandle<VocalWarningProfileDTO> _profilesHandle;
        private VaultGenerationHandle<VocalWarningTuningDTO> _tuningHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<VwsTelemetryEntry> _telemetryRingHandle;
        private PostSimulationPhaseSystem _postSimulationSystem;
        private int _telemetryCursor;
        private int _queueCount;
        private int _registeredUpdate;
        private int _registeredSlowTick;
        private int _registeredHotSwap;
        private int _registeredRuntime;
        private int _registeredPostSimulation;
        private int _nativeAllocated;
        private int _telemetryDumpRequested;
        private int _telemetryDumped;
        private int _telemetrySamplesWritten;
        private uint _ownerFrameCounter;
        private uint _lastProcessedFrame = uint.MaxValue;
        private float _globalQualityWeight01 = 1f;
        private float _vwsClockSeconds;
        private float _warningPlaybackRemainingSeconds;
        private float _currentPriorityScore;
        private float _lastBurstExecutionMicros;
        private uint _currentAudioBankHashID;
        private uint _lastDispatchedAudioBankHashID;
        private uint _lastInterruptCount;
        private ushort _lastDirectionHash;
        private byte _currentWarningId;
        private byte _lastDispatchedWarningId;

        public bool IsInitialized => Volatile.Read(ref _nativeAllocated) != 0;

        public int PendingCount => math.max(0, _queueCount);

        public byte CurrentWarningId => _currentWarningId;

        public bool IsWarningActive => _warningPlaybackRemainingSeconds > 0f;

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
            EnsureNativeStorage();
            RefreshCachedServicesCold();
        }

        private void OnEnable()
        {
            EnsureNativeStorage();
            TryRegisterHotSwapListener();
            RefreshCachedServicesCold();
            GlobalRegistry.RegisterVocalWarningRuntime(this);
            Volatile.Write(ref _registeredRuntime, 1);
            TryRegisterPostSimulation();
        }

        private void OnDisable()
        {
            UnregisterRuntime();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            DisposeNativeStorage();
        }

        public void Tick(float deltaTime)
        {
            if (Volatile.Read(ref _registeredPostSimulation) != 0)
                return;

            RunVocalWarningFrame(deltaTime, NextOwnerFrameId());
        }

        public void SlowTick()
        {
            if (Volatile.Read(ref _registeredPostSimulation) != 0 ||
                Volatile.Read(ref _registeredUpdate) != 0)
                return;

            RunVocalWarningFrame(0.1f, NextOwnerFrameId());
        }

        public bool TryQueueWarning(byte warningId, float severity01, float cooldownSeconds, byte flags, uint sourceId)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || Volatile.Read(ref _registeredRuntime) == 0)
                return false;

            byte normalized = NormalizeWarningId(warningId);
            if (normalized == 0)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            if (!TryResolveVwsViews(out VwsVaultViews views) ||
                normalized >= views.Cooldowns.Length ||
                normalized >= views.WarningFlags.Length ||
                normalized >= views.WarningSeverity.Length ||
                normalized >= views.WarningSourceIds.Length)
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            float cooldown;
            unsafe
            {
                cooldown = NativeElementRef(views.Cooldowns, normalized);
            }
            if (cooldown > 0f && !IsCriticalWarningId(normalized))
                return false;

            uint hash = VocalWarningHashes.FromWarningId(normalized);
            if (hash == 0u)
                return false;

            float severity = ResolveSeverity01(severity01);
            uint packedFlags = PackFlags(normalized, flags, 0, false);
            VocalWarningTuningDTO tuning = ResolveTuning(views.Tuning);
            float priorityScore = ResolvePriorityScore(hash, severity, 0, packedFlags, in tuning);
            if (!math.isfinite(priorityScore))
            {
                Interlocked.Exchange(ref _telemetryDumpRequested, 1);
                return false;
            }

            float resolvedCooldown = ResolveCooldownSeconds(cooldownSeconds);
            unsafe
            {
                NativeElementRef(views.Cooldowns, normalized) = resolvedCooldown;
                NativeElementRef(views.WarningFlags, normalized) = flags;
                NativeElementRef(views.WarningSeverity, normalized) = severity;
                NativeElementRef(views.WarningSourceIds, normalized) = sourceId;
            }

            VocalWarningDTO dto = new VocalWarningDTO
            {
                AudioBankHashID = hash,
                PriorityScore = priorityScore,
                ExpirationTime = _vwsClockSeconds + ResolveExpirationSeconds(hash, severity),
                Flags = packedFlags
            };

            bool accepted = VocalWarningPriorityWordOps.Insert(views.Queue, views.PriorityState, in dto);
            _queueCount = ResolveActivePriorityCount(ref views);
            return accepted;
        }

        public void CancelCurrentWarning()
        {
            CancelRendererPlaybackAndClearQueues();
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                RebindDataVault(currentService as IDataVault);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault &&
                !ReferenceEquals(previousService, currentService))
                RebindDataVault(currentService as IDataVault);
        }

#if UNITY_EDITOR
        public bool EditorInjectMockThreats(int count)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveVwsViews(out VwsVaultViews views))
                return false;

            GenerateMockVocalThreatsJob job = new GenerateMockVocalThreatsJob
            {
                Queue = views.Queue,
                PriorityState = views.PriorityState,
                Tuning = views.Tuning,
                TimeSeconds = _vwsClockSeconds,
                Seed = NextOwnerFrameId() ^ 0x9E3779B9u,
                Count = math.clamp(count, 1, 50)
            };
            job.Run();
            _queueCount = ResolveActivePriorityCount(ref views);
            return true;
        }

        public bool EditorTryReadTuning(out VocalWarningTuningDTO tuning)
        {
            tuning = CreateDefaultTuning();
            if (Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveVwsViews(out VwsVaultViews views))
                return false;

            tuning = ResolveTuning(views.Tuning);
            return true;
        }

        public unsafe bool EditorTryWriteTuning(in VocalWarningTuningDTO tuning)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveVwsViews(out VwsVaultViews views) ||
                !views.Tuning.IsCreated || views.Tuning.Length <= 0)
                return false;

            void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Tuning);
            ref VocalWarningTuningDTO target = ref UnsafeUtility.AsRef<VocalWarningTuningDTO>(pointer);
            target = SanitizeTuning(tuning);
            return true;
        }

        public bool EditorTryGetTelemetrySample(int offsetFromNewest, out VocalWarningTelemetrySnapshot snapshot)
        {
            snapshot = default;
            if (Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveVwsViews(out VwsVaultViews views) ||
                !views.TelemetryRing.IsCreated || views.TelemetryRing.Length <= 0)
                return false;

            int cursor = _telemetryCursor - 1 - math.max(0, offsetFromNewest);
            while (cursor < 0)
                cursor += views.TelemetryRing.Length;

            VwsTelemetryEntry entry;
            unsafe
            {
                entry = NativeElementRef(views.TelemetryRing, cursor % views.TelemetryRing.Length);
            }
            snapshot.Frame = entry.Frame;
            snapshot.ActivePriorityCount = entry.ActivePriorityCount;
            snapshot.ActivePriorityWord = entry.ActivePriorityWord;
            snapshot.CurrentAudioBankHashID = entry.CurrentAudioBankHashID;
            snapshot.CurrentPriorityScore = entry.CurrentPriorityScore;
            snapshot.BurstExecutionMicros = entry.BurstExecutionMicros;
            snapshot.ExpiredDiscardCount = entry.ExpiredDiscardCount;
            snapshot.FaultFlags = entry.FaultFlags;
            snapshot.InterruptCount = entry.InterruptCount;
            return entry.Frame != 0u || entry.ActivePriorityCount != 0u || entry.CurrentAudioBankHashID != 0u;
        }

        public bool EditorTryGetPriorityEntry(int priorityOrderIndex, out uint audioBankHashID, out float priorityScore)
        {
            audioBankHashID = 0u;
            priorityScore = 0f;
            if (priorityOrderIndex < 0 || Volatile.Read(ref _nativeAllocated) == 0 || !TryResolveVwsViews(out VwsVaultViews views))
                return false;

            int count = ResolveActivePriorityCount(ref views);
            if (priorityOrderIndex >= count ||
                !VocalWarningPriorityWordOps.TryGetByPriorityOrder(views.Queue, views.PriorityState, priorityOrderIndex, out VocalWarningDTO dto))
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
            if (Volatile.Read(ref _registeredPostSimulation) != 0)
                return;

            if (_postSimulationSystem == null)
                _postSimulationSystem = new PostSimulationPhaseSystem(this);

            if (GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationSystem))
            {
                Volatile.Write(ref _registeredPostSimulation, 1);
                return;
            }

            if (GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment))
                _registeredSlowTick = 1;
        }

        private void UnregisterRuntime()
        {
            CancelRendererPlaybackAndClearQueues();
            if (Interlocked.Exchange(ref _registeredPostSimulation, 0) != 0 && _postSimulationSystem != null)
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationSystem);
            if (Interlocked.Exchange(ref _registeredHotSwap, 0) != 0)
                GlobalRegistry.UnregisterHotSwapListener(this);
            if (Interlocked.Exchange(ref _registeredSlowTick, 0) != 0)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredUpdate, 0) != 0)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            if (Interlocked.Exchange(ref _registeredRuntime, 0) != 0)
                GlobalRegistry.UnregisterVocalWarningRuntime(this);
        }

        private void EnsureNativeStorage()
        {
            if (Volatile.Read(ref _nativeAllocated) != 0)
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            BindVaultStorage(vault);
            if (!TryResolveVwsViews(out VwsVaultViews views))
            {
                ClearVaultDescriptors();
                return;
            }

            InitializeVaultStorage(ref views);
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
            _priorityStateHandle = vault.EnsureGenerationHandle<VocalWarningPriorityState>(
                VocalWarningPriorityStateBufferId,
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
#if UNITY_EDITOR
            _csvScratchHandle = vault.EnsureGenerationHandle<byte>(
                VocalWarningCsvScratchBufferId,
                CsvScratchCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
#endif
            _telemetryRingHandle = vault.EnsureGenerationHandle<VwsTelemetryEntry>(
                BufferID.AudioVocalWarningTelemetry,
                TelemetryCapacity,
                VaultOwner,
                NativeArrayOptions.UninitializedMemory);
        }

        private void InitializeVaultStorage(ref VwsVaultViews views)
        {
            _telemetryCursor = 0;
            _telemetrySamplesWritten = 0;
            _ownerFrameCounter = 0u;
            _lastProcessedFrame = uint.MaxValue;
            Interlocked.Exchange(ref _telemetryDumpRequested, 0);
            Interlocked.Exchange(ref _telemetryDumped, 0);
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
                for (int i = 0; i < views.TelemetryRing.Length; i++)
                    NativeElementRef(views.TelemetryRing, i) = default;
            }
        }

        private void RebindDataVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultBackedStorage();
            _dataVault = vault;
            Volatile.Write(ref _nativeAllocated, 0);
            _queueCount = 0;
            _currentWarningId = 0;
            _currentAudioBankHashID = 0u;
            if (vault != null)
                EnsureNativeStorage();
        }

        private void ReleaseVaultBackedStorage()
        {
            IDataVault vault = _dataVault;
            ReleaseVaultBuffer(vault, ref _vwsQueueHandle);
            ReleaseVaultBuffer(vault, ref _priorityStateHandle);
            ReleaseVaultBuffer(vault, ref _warningFlagsHandle);
            ReleaseVaultBuffer(vault, ref _cooldownsHandle);
            ReleaseVaultBuffer(vault, ref _warningSeverityHandle);
            ReleaseVaultBuffer(vault, ref _warningSourceIdsHandle);
            ReleaseVaultBuffer(vault, ref _currentStateHandle);
            ReleaseVaultBuffer(vault, ref _dispatchHandle);
            ReleaseVaultBuffer(vault, ref _profilesHandle);
            ReleaseVaultBuffer(vault, ref _tuningHandle);
#if UNITY_EDITOR
            ReleaseVaultBuffer(vault, ref _csvScratchHandle);
#endif
            ReleaseVaultBuffer(vault, ref _telemetryRingHandle);
            ClearVaultDescriptors();
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
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
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
            _telemetryRingHandle = default;
        }

        private bool TryResolveVwsViews(out VwsVaultViews views)
        {
            views = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!vault.TryResolveHandle(in _vwsQueueHandle, out views.Queue) ||
                !vault.TryResolveHandle(in _priorityStateHandle, out views.PriorityState) ||
                !vault.TryResolveHandle(in _warningFlagsHandle, out views.WarningFlags) ||
                !vault.TryResolveHandle(in _cooldownsHandle, out views.Cooldowns) ||
                !vault.TryResolveHandle(in _warningSeverityHandle, out views.WarningSeverity) ||
                !vault.TryResolveHandle(in _warningSourceIdsHandle, out views.WarningSourceIds) ||
                !vault.TryResolveHandle(in _currentStateHandle, out views.CurrentState) ||
                !vault.TryResolveHandle(in _dispatchHandle, out views.Dispatch) ||
                !vault.TryResolveHandle(in _profilesHandle, out views.Profiles) ||
                !vault.TryResolveHandle(in _tuningHandle, out views.Tuning) ||
#if UNITY_EDITOR
                !vault.TryResolveHandle(in _csvScratchHandle, out views.CsvScratch) ||
#endif
                !vault.TryResolveHandle(in _telemetryRingHandle, out views.TelemetryRing) ||
                !views.Queue.IsCreated ||
                !views.PriorityState.IsCreated ||
                !views.WarningFlags.IsCreated ||
                !views.Cooldowns.IsCreated ||
                !views.WarningSeverity.IsCreated ||
                !views.WarningSourceIds.IsCreated ||
                !views.CurrentState.IsCreated ||
                !views.Dispatch.IsCreated ||
                !views.Profiles.IsCreated ||
                !views.Tuning.IsCreated ||
#if UNITY_EDITOR
                !views.CsvScratch.IsCreated ||
#endif
                !views.TelemetryRing.IsCreated)
            {
                views = default;
                return false;
            }

            return true;
        }

        private void RefreshCachedServicesCold()
        {
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
        }

        private void TryRegisterHotSwapListener()
        {
            if (Volatile.Read(ref _registeredHotSwap) != 0)
                return;

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                Volatile.Write(ref _registeredHotSwap, 1);
        }

        private void DisposeNativeStorage()
        {
            if (Interlocked.Exchange(ref _nativeAllocated, 0) == 0 && _dataVault == null)
                return;

            ReleaseVaultBackedStorage();
            _dataVault = null;
            _queueCount = 0;
            _currentWarningId = 0;
            _currentAudioBankHashID = 0u;
        }

        private void RunVocalWarningFrame(float deltaTime, uint frame)
        {
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            if (_lastProcessedFrame == frame)
                return;
            _lastProcessedFrame = frame;

            if (!TryResolveVwsViews(out VwsVaultViews views))
                return;

            float dt = math.max(0f, math.select(0f, deltaTime, math.isfinite(deltaTime)));
            _vwsClockSeconds += dt;
            _globalQualityWeight01 = ResolveGlobalQualityWeight01();
            int maxEvaluations = ResolveMaxEvaluations(_globalQualityWeight01, views.Queue.Length);
            AbsoluteUniversePosition listenerAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();

            EvaluateWarningPrioritiesJob evaluateJob = new EvaluateWarningPrioritiesJob
            {
                Queue = views.Queue,
                PriorityState = views.PriorityState,
                Cooldowns = views.Cooldowns,
                WarningFlags = views.WarningFlags,
                WarningSeverity = views.WarningSeverity,
                WarningSourceIds = views.WarningSourceIds,
                Tuning = views.Tuning,
                VocalWarnings = SignalBus<VocalWarningSignal>.GetFrameSnapshotArray(),
                VitalWarnings = SignalBus<VitalWarningSignal>.GetFrameSnapshotArray(),
                CrushWarnings = SignalBus<CrushWarningSignal>.GetFrameSnapshotArray(),
                Brownouts = SignalBus<BrownoutSignal>.GetFrameSnapshotArray(),
                HealthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshotArray(),
                RadiationSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshotArray(),
                OxygenSignals = SignalBus<OxygenCriticalSignal>.GetFrameSnapshotArray(),
                FloodSignals = SignalBus<SubmarineFloodStateSignal>.GetFrameSnapshotArray(),
                FluidSignals = SignalBus<FluidIncursionSignal>.GetFrameSnapshotArray(),
                PipeSignals = SignalBus<PipeRuptureSignal>.GetFrameSnapshotArray(),
                BatterySignals = SignalBus<BatteryLevelSignal>.GetFrameSnapshotArray(),
                SurvivalSignals = SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshotArray(),
                ListenerAup = listenerAup,
                TimeSeconds = _vwsClockSeconds,
                DeltaSeconds = dt,
                FallbackCooldownSeconds = ResolveCooldownSeconds(fallbackCooldownSeconds),
                MaxEvaluations = maxEvaluations
            };

            long startTicks = Stopwatch.GetTimestamp();
            evaluateJob.Run();

            DispatchVoiceOverJob dispatchJob = new DispatchVoiceOverJob
            {
                Queue = views.Queue,
                PriorityState = views.PriorityState,
                CurrentState = views.CurrentState,
                Dispatch = views.Dispatch,
                Tuning = views.Tuning,
                TimeSeconds = _vwsClockSeconds,
                DeltaSeconds = dt,
                QualityWeight01 = _globalQualityWeight01,
                VoiceGain = voiceGain,
                Frame = frame
            };
            dispatchJob.Run();
            long endTicks = Stopwatch.GetTimestamp();

            _lastBurstExecutionMicros = (float)((endTicks - startTicks) * 1000000.0 / Stopwatch.Frequency);
            PublishDispatchIfNeeded(ref views, frame);
            PullCurrentState(ref views);
            WriteTelemetry(ref views, frame);
            FlushTelemetryDumpRequest();
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
            bool cueAccepted = SignalBus<VocalCueSignal>.TryPush(in cue);
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
                if (!SignalBus<SubtitleCueSignal>.TryPush(in subtitle))
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
                ref VocalWarningPriorityState state = ref NativeElementRef(views.PriorityState, 0);
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
            _currentWarningId = 0;
            _currentAudioBankHashID = 0u;
            _currentPriorityScore = 0f;
            _warningPlaybackRemainingSeconds = 0f;
            if (Volatile.Read(ref _nativeAllocated) == 0)
                return;

            ClearQueuedWarnings();
        }

        private void ClearQueuedWarnings()
        {
            if (TryResolveVwsViews(out VwsVaultViews views))
            {
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

            _queueCount = 0;
        }

        private void WriteTelemetry(ref VwsVaultViews views, uint frame)
        {
            NativeArray<VwsTelemetryEntry> telemetryRing = views.TelemetryRing;
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int cursor = _telemetryCursor;
            if ((uint)cursor >= (uint)telemetryRing.Length)
                cursor = 0;

            VocalWarningPriorityState priorityState = default;
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
                    ActivePriorityWord = priorityState.VwsPriorityWord,
                    CurrentAudioBankHashID = current.AudioBankHashID,
                    LastDispatchedAudioBankHashID = _lastDispatchedAudioBankHashID,
                    CurrentPriorityScore = current.PriorityScore,
                    ActiveRemainingSeconds = current.PlaybackRemainingSeconds,
                    BurstExecutionMicros = _lastBurstExecutionMicros,
                    ExpiredDiscardCount = (uint)math.max(0, priorityState.DiscardedExpired),
                    FaultFlags = faultFlags,
                    InterruptCount = current.LastInterruptCount,
                    QualityWeight01 = _globalQualityWeight01,
                    CurrentWarningId = VocalWarningHashes.ToWarningId(current.AudioBankHashID),
                    LastDispatchedWarningId = _lastDispatchedWarningId,
                    DirectionHash = _lastDirectionHash,
                    HighestPriorityBitIndex = priorityState.HighestPriorityBitIndex,
                    SubtitleFrameLatency = 0u
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
            if (!TryResolveVwsViews(out VwsVaultViews views))
                return;

            if (Volatile.Read(ref _telemetryDumped) != 0)
                return;

            try
            {
                NativeArray<VwsTelemetryEntry> telemetryRing = views.TelemetryRing;
                if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                    return;

                int entryStride = UnsafeUtility.SizeOf<VwsTelemetryEntry>();
                int count = math.clamp(_telemetrySamplesWritten, 0, telemetryRing.Length);
                int cursor = math.clamp(_telemetryCursor, 0, telemetryRing.Length - 1);
                int startIndex = count < telemetryRing.Length ? 0 : cursor;
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(root, TelemetryDumpRelativePath);
                string agentPath = Path.Combine(root, AgentTelemetryDumpRelativePath);
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

                unsafe
                {
                    byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    WriteTelemetryDump(path, in header, telemetryPtr, entryStride, count, startIndex, telemetryRing.Length);
                    WriteTelemetryDump(agentPath, in header, telemetryPtr, entryStride, count, startIndex, telemetryRing.Length);
                }

                Interlocked.Exchange(ref _telemetryDumped, 1);
            }
            catch (Exception)
            {
            }
        }

        private static unsafe void WriteTelemetryDump(
            string path,
            in VwsTelemetryDumpHeader header,
            byte* telemetryPtr,
            int entryStride,
            int count,
            int startIndex,
            int capacity)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            VwsTelemetryDumpHeader localHeader = header;
            stream.Write(new ReadOnlySpan<byte>(&localHeader, UnsafeUtility.SizeOf<VwsTelemetryDumpHeader>()));
            if (count <= 0)
                return;

            int firstCount = math.min(count, capacity - startIndex);
            stream.Write(new ReadOnlySpan<byte>(telemetryPtr + startIndex * entryStride, firstCount * entryStride));
            int secondCount = count - firstCount;
            if (secondCount > 0)
                stream.Write(new ReadOnlySpan<byte>(telemetryPtr, secondCount * entryStride));
        }

        private static int ResolveActivePriorityCount(ref VwsVaultViews views)
        {
            if (!views.PriorityState.IsCreated || views.PriorityState.Length <= 0)
                return 0;

            VocalWarningPriorityState state;
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
            return warningId >= (byte)VocalWarningId.CrushDepth && warningId <= (byte)VocalWarningId.PowerLow
                ? warningId
                : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolvePriorityBitIndex(byte warningId)
        {
            byte normalized = NormalizeWarningId(warningId);
            return normalized == 0 ? NoPriorityBitIndex : PriorityWordBitCount - normalized;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VocalWarningTuningDTO ResolveTuning(NativeArray<VocalWarningTuningDTO> tuning)
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
                default:
                    basePriority = resolved.DefaultBasePriority;
                    break;
            }

            float severity = math.saturate(math.select(0f, severity01, math.isfinite(severity01)));
            float criticalBoost = (packedFlags & QueueFlagCritical) != 0u ? resolved.CriticalBoost : 0f;
            float producerBoost = math.clamp(producerPriority, 0, 255) * resolved.ProducerPriorityScale;
            return basePriority + severity * resolved.SeverityBoost + criticalBoost + producerBoost;
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
        private static int ResolveCuePriority(int priorityBitIndex, float priorityScore)
        {
            int firstPriorityBit = PriorityWordBitCount - HighestCanonicalWarningId;
            int canonicalRank = math.clamp(priorityBitIndex - firstPriorityBit + 1, 1, CanonicalWarningCount);
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
            public NativeArray<VocalWarningPriorityState> PriorityState;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningTuningDTO> Tuning;
            public float TimeSeconds;
            public uint Seed;
            public int Count;

            public unsafe void Execute()
            {
                uint state = math.max(1u, Seed);
                int count = math.clamp(Count, 1, 50);
                VocalWarningTuningDTO tuning = ResolveTuning(Tuning);
                for (int i = 0; i < count; i++)
                {
                    state = state * 1664525u + 1013904223u;
                    byte warningId = (byte)(1 + (state % 5u));
                    uint hash = VocalWarningHashes.FromWarningId(warningId);
                    float severity = ((state >> 8) & 1023u) * (1f / 1023f);
                    uint flags = PackFlags(warningId, 0, 0, true);
                    VocalWarningDTO dto = new VocalWarningDTO
                    {
                        AudioBankHashID = hash,
                        PriorityScore = ResolvePriorityScore(hash, severity, 0, flags, in tuning),
                        ExpirationTime = TimeSeconds + ResolveExpirationSeconds(hash, severity),
                        Flags = flags
                    };
                    VocalWarningPriorityWordOps.Insert(Queue, PriorityState, in dto);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateWarningPrioritiesJob : IJob
        {
            [NoAlias]
            public NativeArray<VocalWarningDTO> Queue;
            [NoAlias]
            public NativeArray<VocalWarningPriorityState> PriorityState;
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
            [ReadOnly, NoAlias] public NativeArray<VocalWarningSignal>.ReadOnly VocalWarnings;
            [ReadOnly, NoAlias] public NativeArray<VitalWarningSignal>.ReadOnly VitalWarnings;
            [ReadOnly, NoAlias] public NativeArray<CrushWarningSignal>.ReadOnly CrushWarnings;
            [ReadOnly, NoAlias] public NativeArray<BrownoutSignal>.ReadOnly Brownouts;
            [ReadOnly, NoAlias] public NativeArray<SystemHealthIndexSignal>.ReadOnly HealthSignals;
            [ReadOnly, NoAlias] public NativeArray<RadiationDoseSignal>.ReadOnly RadiationSignals;
            [ReadOnly, NoAlias] public NativeArray<OxygenCriticalSignal>.ReadOnly OxygenSignals;
            [ReadOnly, NoAlias] public NativeArray<SubmarineFloodStateSignal>.ReadOnly FloodSignals;
            [ReadOnly, NoAlias] public NativeArray<FluidIncursionSignal>.ReadOnly FluidSignals;
            [ReadOnly, NoAlias] public NativeArray<PipeRuptureSignal>.ReadOnly PipeSignals;
            [ReadOnly, NoAlias] public NativeArray<BatteryLevelSignal>.ReadOnly BatterySignals;
            [ReadOnly, NoAlias] public NativeArray<SurvivalVitalsChangedSignal>.ReadOnly SurvivalSignals;
            public AbsoluteUniversePosition ListenerAup;
            public float TimeSeconds;
            public float DeltaSeconds;
            public float FallbackCooldownSeconds;
            public int MaxEvaluations;

            public unsafe void Execute()
            {
                if (!Queue.IsCreated || !PriorityState.IsCreated || PriorityState.Length <= 0)
                    return;

                VocalWarningPriorityWordOps.DiscardExpired(Queue, PriorityState, TimeSeconds);
                DecayCooldowns();
                VocalWarningTuningDTO tuning = ResolveTuning(Tuning);

                int evaluations = 0;
                for (int i = 0; i < FloodSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    SubmarineFloodStateSignal signal = FloodSignals[i];
                    if ((signal.Flags & SubmarineFloodStateSignal.FlagCriticalFlood) == 0 && signal.FillRatio01 < 0.18f)
                        continue;

                    float severity = math.saturate(math.max(signal.FillRatio01, signal.TotalWaterMassKg / math.max(1f, signal.BaseMassKg)));
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.SourceBodyId, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < FluidSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    FluidIncursionSignal signal = FluidSignals[i];
                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.LeakAup);
                    float severity = math.max(signal.FloodLevel01, signal.FlowRate01);
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.CompartmentId, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < PipeSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    PipeRuptureSignal signal = PipeSignals[i];
                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.RuptureAup);
                    float severity = math.saturate(signal.PressureKPa * (1f / 2000f));
                    if (TryQueue(VocalWarningHashes.HullBreach, (byte)VocalWarningId.HullBreach, severity, FallbackCooldownSeconds, VocalWarningSignalFlags.HabitatIntegrityCompromised, signal.NetworkId, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < OxygenSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    OxygenCriticalSignal signal = OxygenSignals[i];
                    float severity = math.max(1f - math.saturate(signal.Oxygen01), signal.Severity * (1f / 255f));
                    if (TryQueue(VocalWarningHashes.OxygenLow, (byte)VocalWarningId.OxygenLow, severity, FallbackCooldownSeconds, signal.Flags, signal.SourceId, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < CrushWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    CrushWarningSignal signal = CrushWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, (byte)VocalWarningId.CrushDepth);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    if (TryQueue(hash, warningId, signal.Severity01, FallbackCooldownSeconds, signal.Flags, signal.SourceId, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < VocalWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    VocalWarningSignal signal = VocalWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, signal.Priority);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    if (TryQueue(hash, warningId, signal.Severity01, signal.CooldownSeconds, signal.Flags, signal.SourceId, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < VitalWarnings.Length && evaluations < MaxEvaluations; i++)
                {
                    VitalWarningSignal signal = VitalWarnings[i];
                    uint hash = ResolveHashFromIdOrHash(signal.WarningHash, (byte)VocalWarningId.OxygenLow);
                    byte warningId = VocalWarningHashes.ToWarningId(hash);
                    float severity = math.max(signal.Vital01, signal.Severity01);
                    if (TryQueue(hash, warningId, severity, FallbackCooldownSeconds, signal.Flags, signal.SourceId, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < Brownouts.Length && evaluations < MaxEvaluations; i++)
                {
                    BrownoutSignal signal = Brownouts[i];
                    float severity = math.max(signal.Severity01, 1f - math.saturate(signal.SupplyRatio));
                    if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, signal.Flags, signal.NetworkId, 0, false, in tuning))
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
                    if (TryQueue(hash, warningId, severity, FallbackCooldownSeconds, flags, signal.SourceHash, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < RadiationSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    RadiationDoseSignal signal = RadiationSignals[i];
                    ushort direction = ResolveCompassDirectionHash(in ListenerAup, in signal.PositionAup);
                    if (TryQueue(VocalWarningHashes.Radiation, (byte)VocalWarningId.Radiation, signal.Intensity01, FallbackCooldownSeconds, 0, signal.SourceId, direction, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < BatterySignals.Length && evaluations < MaxEvaluations; i++)
                {
                    BatteryLevelSignal signal = BatterySignals[i];
                    if (signal.BatteryPercent > 25)
                        continue;

                    float severity = math.saturate((25f - signal.BatteryPercent) * (1f / 25f));
                    if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, 0, signal.SourceHash, 0, false, in tuning))
                        evaluations++;
                }

                for (int i = 0; i < SurvivalSignals.Length && evaluations < MaxEvaluations; i++)
                {
                    SurvivalVitalsChangedSignal signal = SurvivalSignals[i];
                    if ((signal.Flags & SurvivalVitalsChangedSignalFlags.OxygenCritical) != 0u || signal.Oxygen01 < 0.22f)
                    {
                        float severity = 1f - math.saturate(signal.Oxygen01);
                        if (TryQueue(VocalWarningHashes.OxygenLow, (byte)VocalWarningId.OxygenLow, severity, FallbackCooldownSeconds, 0, signal.SourceId, 0, false, in tuning))
                            evaluations++;
                    }

                    if (evaluations >= MaxEvaluations)
                        break;

                    if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Energy) != 0u && signal.Energy01 < 0.18f)
                    {
                        float severity = 1f - math.saturate(signal.Energy01);
                        if (TryQueue(VocalWarningHashes.PowerLow, (byte)VocalWarningId.PowerLow, severity, FallbackCooldownSeconds, 0, signal.SourceId, 0, false, in tuning))
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

            private unsafe bool TryQueue(uint hash, byte warningId, float severity01, float cooldownSeconds, byte signalFlags, uint sourceId, ushort directionHash, bool mock, in VocalWarningTuningDTO tuning)
            {
                warningId = NormalizeWarningId(warningId);
                if (hash == 0u || warningId == 0)
                    return false;

                if (!Cooldowns.IsCreated || warningId >= Cooldowns.Length)
                    return false;

                uint packedFlags = PackFlags(warningId, signalFlags, directionHash, mock);
                float severity = ResolveSeverity01(severity01);
                float priorityScore = ResolvePriorityScore(hash, severity, 0, packedFlags, in tuning);
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

                VocalWarningDTO dto = new VocalWarningDTO
                {
                    AudioBankHashID = hash,
                    PriorityScore = priorityScore,
                    ExpirationTime = TimeSeconds + ResolveExpirationSeconds(hash, severity),
                    Flags = packedFlags
                };
                return VocalWarningPriorityWordOps.Insert(Queue, PriorityState, in dto);
            }

            private unsafe void MarkFault(uint fault)
            {
                if (!PriorityState.IsCreated || PriorityState.Length <= 0)
                    return;

                ref VocalWarningPriorityState state = ref PriorityStateRef();
                state.FaultFlags |= fault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe ref VocalWarningPriorityState PriorityStateRef()
            {
                void* pointer = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(PriorityState);
                return ref UnsafeUtility.AsRef<VocalWarningPriorityState>(pointer);
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
        private struct DispatchVoiceOverJob : IJob
        {
            [NoAlias]
            public NativeArray<VocalWarningDTO> Queue;
            [NoAlias]
            public NativeArray<VocalWarningPriorityState> PriorityState;
            [NoAlias]
            public NativeArray<VocalWarningCurrentState> CurrentState;
            [NoAlias]
            public NativeArray<VocalWarningDispatchDTO> Dispatch;
            [ReadOnly, NoAlias]
            public NativeArray<VocalWarningTuningDTO> Tuning;
            public float TimeSeconds;
            public float DeltaSeconds;
            public float QualityWeight01;
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
                VocalWarningPriorityWordOps.DiscardExpired(Queue, PriorityState, TimeSeconds);

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
                if (!VocalWarningPriorityWordOps.Peek(Queue, PriorityState, out VocalWarningDTO candidate, out int candidateBitIndex))
                {
                    currentSlot = current;
                    return;
                }

                bool active = current.AudioBankHashID != 0u && current.PlaybackRemainingSeconds > 0f;
                int currentBitIndex = VocalWarningSystem.ResolvePriorityBitIndex(VocalWarningHashes.ToWarningId(current.AudioBankHashID));
                bool higherPriorityBit = active && candidateBitIndex > currentBitIndex;
                bool canInterrupt = active &&
                                    (higherPriorityBit ||
                                     (candidate.PriorityScore > current.PriorityScore + tuning.InterruptionThreshold &&
                                      (candidate.Flags & (QueueFlagCritical | QueueFlagInterrupt)) != 0u));
                if (active && !canInterrupt)
                {
                    currentSlot = current;
                    return;
                }

                if (!VocalWarningPriorityWordOps.Pop(Queue, PriorityState, out candidate, out candidateBitIndex))
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
                float distortion = ResolveRadioDistortion01(candidate.AudioBankHashID, flags, QualityWeight01);
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
                    PlaybackSpeed = 1f,
                    RadioDistortion01 = distortion,
                    SpatialBlend01 = (flags & QueueFlagDirectional) != 0u ? math.lerp(0.18f, 0.35f, SmoothQuality01(QualityWeight01)) : 0f,
                    SourceAupGridX = 0,
                    SourceAupGridY = 0,
                    SourceAupGridZ = 0,
                    SourceAupLocalX = 0f,
                    SourceAupLocalY = 0f,
                    SourceAupLocalZ = 0f,
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

        private static class VocalWarningPriorityWordOps
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Insert(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<VocalWarningPriorityState> priorityState,
                in VocalWarningDTO value)
            {
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < PriorityWordBitCount)
                    return false;

                if (value.AudioBankHashID == 0u || !math.isfinite(value.PriorityScore) || !math.isfinite(value.ExpirationTime))
                {
                    MarkFault(priorityState, FaultFlagPriorityInputInvalid);
                    return false;
                }

                int bitIndex = ResolvePriorityBitIndex(ExtractWarningId(value.Flags));
                if (bitIndex == NoPriorityBitIndex)
                    bitIndex = ResolvePriorityBitIndex(VocalWarningHashes.ToWarningId(value.AudioBankHashID));
                if ((uint)bitIndex >= PriorityWordBitCount)
                {
                    MarkFault(priorityState, FaultFlagPriorityInputInvalid);
                    return false;
                }

                ref VocalWarningPriorityState state = ref StateRef(priorityState);
                ulong bitMask = 1UL << bitIndex;
                ref VocalWarningDTO slot = ref NodeRef(queue, bitIndex);
                bool occupied = (state.VwsPriorityWord & bitMask) != 0UL && slot.AudioBankHashID != 0u;
                if (!occupied || HigherPriorityThan(in value, in slot))
                {
                    slot = value;
                }
                else
                {
                    slot.ExpirationTime = math.max(slot.ExpirationTime, value.ExpirationTime);
                    slot.Flags |= value.Flags & (QueueFlagCritical | QueueFlagInterrupt | QueueFlagHabitatIntegrity | QueueFlagDirectional | QueueFlagMock);
                }

                state.VwsPriorityWord |= bitMask;
                state.ActivePriorityCount = (uint)CountBits64(state.VwsPriorityWord);
                state.LastAcceptedBitIndex = (uint)bitIndex;
                state.HighestPriorityBitIndex = (uint)ResolveHighestPriorityBitIndex(state.VwsPriorityWord);
                state.Sequence++;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Peek(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<VocalWarningPriorityState> priorityState,
                out VocalWarningDTO value,
                out int bitIndex)
            {
                value = default;
                bitIndex = NoPriorityBitIndex;
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < PriorityWordBitCount)
                    return false;

                VocalWarningPriorityState state = StateRef(priorityState);
                bitIndex = ResolveHighestPriorityBitIndex(state.VwsPriorityWord);
                if ((uint)bitIndex >= PriorityWordBitCount)
                    return false;

                value = NodeRef(queue, bitIndex);
                return value.AudioBankHashID != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool Pop(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<VocalWarningPriorityState> priorityState,
                out VocalWarningDTO value,
                out int bitIndex)
            {
                value = default;
                bitIndex = NoPriorityBitIndex;
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < PriorityWordBitCount)
                    return false;

                ref VocalWarningPriorityState state = ref StateRef(priorityState);
                bitIndex = ResolveHighestPriorityBitIndex(state.VwsPriorityWord);
                if ((uint)bitIndex >= PriorityWordBitCount)
                    return false;

                ulong bitMask = 1UL << bitIndex;
                value = NodeRef(queue, bitIndex);
                NodeRef(queue, bitIndex) = default;
                state.VwsPriorityWord &= ~bitMask;
                state.ActivePriorityCount = (uint)CountBits64(state.VwsPriorityWord);
                int highestBitIndex = ResolveHighestPriorityBitIndex(state.VwsPriorityWord);
                state.HighestPriorityBitIndex = highestBitIndex >= 0 ? (uint)highestBitIndex : uint.MaxValue;
                state.Sequence++;
                return value.AudioBankHashID != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void DiscardExpired(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<VocalWarningPriorityState> priorityState,
                float timeSeconds)
            {
                if (!queue.IsCreated || !priorityState.IsCreated || priorityState.Length <= 0 || queue.Length < PriorityWordBitCount)
                    return;

                ref VocalWarningPriorityState state = ref StateRef(priorityState);
                ulong activeWord = state.VwsPriorityWord;
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

                state.VwsPriorityWord = activeWord;
                state.ActivePriorityCount = (uint)CountBits64(activeWord);
                state.DiscardedExpired += discarded;
                int highestBitIndex = ResolveHighestPriorityBitIndex(activeWord);
                state.HighestPriorityBitIndex = highestBitIndex >= 0 ? (uint)highestBitIndex : uint.MaxValue;
                state.Sequence++;
            }

            public static bool TryGetByPriorityOrder(
                NativeArray<VocalWarningDTO> queue,
                NativeArray<VocalWarningPriorityState> priorityState,
                int priorityOrderIndex,
                out VocalWarningDTO value)
            {
                value = default;
                if (priorityOrderIndex < 0 ||
                    !queue.IsCreated ||
                    !priorityState.IsCreated ||
                    priorityState.Length <= 0 ||
                    queue.Length < PriorityWordBitCount)
                {
                    return false;
                }

                ulong scanWord = StateRef(priorityState).VwsPriorityWord;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void MarkFault(NativeArray<VocalWarningPriorityState> priorityState, uint fault)
            {
                if (!priorityState.IsCreated || priorityState.Length <= 0)
                    return;

                ref VocalWarningPriorityState state = ref StateRef(priorityState);
                state.FaultFlags |= fault;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int ResolveHighestPriorityBitIndex(ulong priorityWord)
            {
                uint high = (uint)(priorityWord >> 32);
                uint low = (uint)priorityWord;
                bool useHigh = high != 0u;
                uint selected = math.select(low | 1u, high, useHigh);
                int baseIndex = math.select(0, 32, useHigh);
                int candidateIndex = baseIndex + (31 - math.lzcnt(selected));
                return math.select(NoPriorityBitIndex, candidateIndex, priorityWord != 0UL);
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
            private static ref VocalWarningPriorityState StateRef(NativeArray<VocalWarningPriorityState> priorityState)
            {
                unsafe
                {
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(priorityState);
                    return ref UnsafeUtility.AsRef<VocalWarningPriorityState>(basePtr);
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

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly VocalWarningSystem _owner;

            public PostSimulationPhaseSystem(VocalWarningSystem owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => VocalWarningSystemHash;
            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;
            public byte GetBucketId() => 0;
            public int GetDependencyCount() => 0;
            public uint GetDependencyHash(int dependencyIndex) => 0u;
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) => dependsOn;
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner.RunVocalWarningFrame(timing.FrameDelta, timing.FrameId);
            }
        }
    }
}
