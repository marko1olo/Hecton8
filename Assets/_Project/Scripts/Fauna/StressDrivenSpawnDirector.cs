using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Data;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using EcosystemSectorDTO = Hecton8.Core.Contracts.EcosystemSectorDTO;

namespace Hecton8.AI
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpawnRuleDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float MinTension;
        [FieldOffset(8)] public float MaxTension;
        [FieldOffset(12)] public float CPUCostScalar;
        [FieldOffset(16)] public uint RequiredBiomeMask;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SpawnRuleLinkDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public uint LootTableHash;
        [FieldOffset(8)] public uint ArchetypeFlags;
        [FieldOffset(12)] public float ThreatWeight;
        [FieldOffset(16)] public float SwarmCountBias;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 208)]
    public struct DirectorInputDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 PlayerAup;
        [FieldOffset(48)] public AbsoluteUniversePositionBlit128 FloatingOriginAup;
        [FieldOffset(96)] public float3 PlayerForward;
        [FieldOffset(108)] public float TensionIndex;
        [FieldOffset(112)] public float TurbidityScalar;
        [FieldOffset(116)] public float GlobalQualityWeight;
        [FieldOffset(120)] public float ThermalPressure01;
        [FieldOffset(124)] public uint CurrentBiomeMask;
        [FieldOffset(128)] public uint SimulationTick;
        [FieldOffset(132)] public uint Frame;
        [FieldOffset(136)] public int BiomeTransitionTicksRemaining;
        [FieldOffset(140)] public float DepthMeters;
        [FieldOffset(144)] public float WeatherSeverity01;
        [FieldOffset(148)] public float SdfClearanceMeters;
        [FieldOffset(152)] public float FrameTimeMs;
        [FieldOffset(156)] public uint WorldSeed;
        [FieldOffset(160)] public float SpawnCooldownSeconds;
        [FieldOffset(164)] public float ExternalStress01;
        [FieldOffset(168)] public uint Flags;
        [FieldOffset(172)] public uint SectorHash;
        [FieldOffset(176)] public float PreyBiomass01;
        [FieldOffset(180)] public float PredatorBiomass01;
        [FieldOffset(184)] public float CarryingCapacity01;
        [FieldOffset(188)] public float LocalTemperature;
        [FieldOffset(192)] public float ToxinLevel01;
        [FieldOffset(196)] public uint MacroEcosystemStateHash;
        [FieldOffset(200)] public uint MacroEcosystemFlags;
        [FieldOffset(204)] public uint OriginShiftSequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DirectorTuningDTO
    {
        [FieldOffset(0)] public float BaseSpawnRatePerMinute;
        [FieldOffset(4)] public float MinHiddenRadiusMeters;
        [FieldOffset(8)] public float MaxHiddenRadiusMeters;
        [FieldOffset(12)] public float FrustumPlaneMarginMeters;
        [FieldOffset(16)] public float DespawnRadiusLowMeters;
        [FieldOffset(20)] public float DespawnRadiusUltraMeters;
        [FieldOffset(24)] public float BudgetLow;
        [FieldOffset(28)] public float BudgetUltra;
        [FieldOffset(32)] public float BiomeTransitionSuppressionSeconds;
        [FieldOffset(36)] public float MinSdfClearanceMeters;
        [FieldOffset(40)] public ushort MaxCandidateRules;
        [FieldOffset(42)] public ushort MaxHiddenProbes;
        [FieldOffset(44)] public ushort MaxSpawnPerColdTick;
        [FieldOffset(46)] public ushort OwnedSlotCapacity;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint TableVersion;
        [FieldOffset(56)] public uint LootValidationMask;
        [FieldOffset(60)] public uint _pad0;

        public static DirectorTuningDTO CreateDefault(float quality)
        {
            DirectorTuningDTO tuning = default;
            tuning.BaseSpawnRatePerMinute = 0.72f;
            tuning.MinHiddenRadiusMeters = 42f;
            tuning.MaxHiddenRadiusMeters = 132f;
            tuning.FrustumPlaneMarginMeters = 8f;
            tuning.DespawnRadiusLowMeters = 126f;
            tuning.DespawnRadiusUltraMeters = 260f;
            tuning.BudgetLow = 0.45f;
            tuning.BudgetUltra = 3.25f;
            tuning.BiomeTransitionSuppressionSeconds = 6f;
            tuning.MinSdfClearanceMeters = 5f;
            tuning.MaxCandidateRules = StressDrivenSpawnDirector.RuleCapacity;
            tuning.MaxHiddenProbes = 19;
            tuning.MaxSpawnPerColdTick = 1;
            tuning.OwnedSlotCapacity = StressDrivenSpawnDirector.OwnedSlotCapacity;
            tuning.Flags = StressDrivenSpawnDirector.TuningFlagEmergencyMock |
                           StressDrivenSpawnDirector.TuningFlagEnableHiddenInjection |
                           StressDrivenSpawnDirector.TuningFlagEnableDistantCull;
            tuning.TableVersion = 1u;
            tuning.LootValidationMask = 0u;
            return StressDrivenSpawnDirectorSanitizer.Sanitize(tuning, quality);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct DirectorCandidateDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public uint LootTableHash;
        [FieldOffset(8)] public uint RequiredBiomeMask;
        [FieldOffset(12)] public float Score;
        [FieldOffset(16)] public float CPUCostScalar;
        [FieldOffset(20)] public float ThreatWeight;
        [FieldOffset(24)] public float SwarmCountBias;
        [FieldOffset(28)] public int RuleIndex;
        [FieldOffset(32)] public uint CandidateHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float MinTension;
        [FieldOffset(44)] public float MaxTension;
        [FieldOffset(48)] public float BudgetFit;
        [FieldOffset(52)] public float SpawnProbability01;
        [FieldOffset(56)] public uint SectorHash;
        [FieldOffset(60)] public uint MacroEcosystemStateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct DirectorSelectionDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 SpawnAup;
        [FieldOffset(48)] public AbsoluteUniversePositionBlit128 PlayerAup;
        [FieldOffset(96)] public float3 RuntimeSpawn;
        [FieldOffset(108)] public float ThreatScore;
        [FieldOffset(112)] public uint SpeciesHash;
        [FieldOffset(116)] public uint LootTableHash;
        [FieldOffset(120)] public int CandidateIndex;
        [FieldOffset(124)] public int RequestSpawn;
        [FieldOffset(128)] public int SpawnSlot;
        [FieldOffset(132)] public uint Flags;
        [FieldOffset(136)] public float SpawnRadiusMeters;
        [FieldOffset(140)] public float Budget;
        [FieldOffset(144)] public float TensionIndex;
        [FieldOffset(148)] public float TurbidityScalar;
        [FieldOffset(152)] public float GlobalQualityWeight;
        [FieldOffset(156)] public uint StateHash;
        [FieldOffset(160)] public uint Frame;
        [FieldOffset(164)] public uint BiomeMask;
        [FieldOffset(168)] public int SuppressTicksRemaining;
        [FieldOffset(172)] public uint SectorHash;
        [FieldOffset(176)] public uint OriginShiftSequence;
        [FieldOffset(180)] public uint _pad0;
        [FieldOffset(184)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct DirectorOwnedSlotDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 LastAup;
        [FieldOffset(48)] public int Slot;
        [FieldOffset(52)] public uint SpeciesHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint LastTick;
        [FieldOffset(64)] public float LastThreatScore;
        [FieldOffset(68)] public uint _pad0;
        [FieldOffset(72)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct DirectorTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public float TensionIndex;
        [FieldOffset(12)] public float TurbidityScalar;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float Budget;
        [FieldOffset(24)] public ushort CandidateCount;
        [FieldOffset(26)] public ushort OwnedSlotCount;
        [FieldOffset(28)] public ushort Spawned;
        [FieldOffset(30)] public ushort Culled;
        [FieldOffset(32)] public float ChainMicroseconds;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public AbsoluteUniversePositionBlit128 PlayerAup;
        [FieldOffset(88)] public AbsoluteUniversePositionBlit128 LastSpawnAup;
        [FieldOffset(136)] public uint DumpReasonHash;
        [FieldOffset(140)] public uint LootTableHash;
        [FieldOffset(144)] public float PreyBiomass01;
        [FieldOffset(148)] public float PredatorBiomass01;
        [FieldOffset(152)] public float CarryingCapacity01;
        [FieldOffset(156)] public uint SectorHash;
        [FieldOffset(160)] public uint MacroEcosystemStateHash;
        [FieldOffset(164)] public float SpawnRadiusMeters;
        [FieldOffset(168)] public uint SpawnSlot;
        [FieldOffset(172)] public uint OriginShiftSequence;
        [FieldOffset(176)] public ulong _pad0;
        [FieldOffset(184)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct InventoryPreloadTicketDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public uint LootTableHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint LastRequestedFrame;
        [FieldOffset(16)] public float BudgetWeight;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct DirectorSpawnDebugDTO
    {
        [FieldOffset(0)] public AbsoluteUniversePositionBlit128 SpawnAup;
        [FieldOffset(48)] public uint SpeciesHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float RadiusMeters;
        [FieldOffset(60)] public float ThreatScore;
        [FieldOffset(64)] public float3 RuntimeSpawn;
        [FieldOffset(76)] public uint Frame;
        [FieldOffset(80)] public uint StateHash;
        [FieldOffset(84)] public float MinHiddenRadiusMeters;
        [FieldOffset(88)] public float MaxHiddenRadiusMeters;
        [FieldOffset(92)] public float DespawnRadiusMeters;
        [FieldOffset(96)] public uint OwnedSlotCount;
        [FieldOffset(100)] public uint SectorHash;
        [FieldOffset(104)] public uint MacroEcosystemStateHash;
        [FieldOffset(108)] public uint _pad0;
        [FieldOffset(112)] public ulong _pad1;
        [FieldOffset(120)] public ulong _pad2;
    }

    public static class StressDrivenSpawnDirectorSanitizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DirectorTuningDTO Sanitize(DirectorTuningDTO tuning, float qualityWeight)
        {
            tuning.BaseSpawnRatePerMinute = math.clamp(FiniteOr(tuning.BaseSpawnRatePerMinute, 0.72f), 0f, 12f);
            tuning.MinHiddenRadiusMeters = math.clamp(FiniteOr(tuning.MinHiddenRadiusMeters, 42f), 12f, 512f);
            tuning.MaxHiddenRadiusMeters = math.max(tuning.MinHiddenRadiusMeters + 1f, math.clamp(FiniteOr(tuning.MaxHiddenRadiusMeters, 132f), 16f, 1024f));
            tuning.FrustumPlaneMarginMeters = math.clamp(FiniteOr(tuning.FrustumPlaneMarginMeters, 8f), 0f, 64f);
            tuning.DespawnRadiusLowMeters = math.clamp(FiniteOr(tuning.DespawnRadiusLowMeters, 126f), 32f, 2048f);
            tuning.DespawnRadiusUltraMeters = math.max(tuning.DespawnRadiusLowMeters, math.clamp(FiniteOr(tuning.DespawnRadiusUltraMeters, 260f), 32f, 4096f));
            tuning.BudgetLow = math.clamp(FiniteOr(tuning.BudgetLow, 0.45f), 0.05f, 16f);
            tuning.BudgetUltra = math.max(tuning.BudgetLow, math.clamp(FiniteOr(tuning.BudgetUltra, 3.25f), 0.05f, 32f));
            tuning.BiomeTransitionSuppressionSeconds = math.clamp(FiniteOr(tuning.BiomeTransitionSuppressionSeconds, 6f), 0f, 60f);
            tuning.MinSdfClearanceMeters = math.clamp(FiniteOr(tuning.MinSdfClearanceMeters, 5f), 0f, 64f);
            tuning.MaxCandidateRules = (ushort)math.clamp(tuning.MaxCandidateRules == 0 ? StressDrivenSpawnDirector.RuleCapacity : tuning.MaxCandidateRules, 1, StressDrivenSpawnDirector.RuleCapacity);
            tuning.MaxHiddenProbes = (ushort)math.clamp(tuning.MaxHiddenProbes == 0 ? 19 : tuning.MaxHiddenProbes, 1, StressDrivenSpawnDirector.MaxHiddenProbeCapacity);
            tuning.MaxSpawnPerColdTick = (ushort)math.clamp(tuning.MaxSpawnPerColdTick == 0 ? 1 : tuning.MaxSpawnPerColdTick, 1, 4);
            tuning.OwnedSlotCapacity = (ushort)math.clamp(tuning.OwnedSlotCapacity == 0 ? StressDrivenSpawnDirector.OwnedSlotCapacity : tuning.OwnedSlotCapacity, 1, StressDrivenSpawnDirector.OwnedSlotCapacity);
            tuning.TableVersion = tuning.TableVersion == 0u ? 1u : tuning.TableVersion;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    public sealed unsafe class StressDrivenSpawnDirector : IColdTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener, IOriginShiftListener, IDisposable
    {
        public const int RuleCapacity = 64;
        public const int CandidateCapacity = 32;
        public const int OwnedSlotCapacity = 64;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 8 * 1024;
        public const int MaxHiddenProbeCapacity = 31;
        private const float AuthoritativeBehaviorWeight = 1f;

        public const uint TuningFlagEmergencyMock = 1u << 0;
        public const uint TuningFlagEnableHiddenInjection = 1u << 1;
        public const uint TuningFlagEnableDistantCull = 1u << 2;
        public const uint TuningFlagExternalInputValid = 1u << 3;
        public const uint TuningFlagMonolithLootMissing = 1u << 4;

        internal const uint InputFlagExternalStress = 1u << 0;
        internal const uint InputFlagOriginInvalid = 1u << 1;
        internal const uint SelectionFlagSpawnHidden = 1u << 0;
        internal const uint SelectionFlagFrustumRejected = 1u << 1;
        internal const uint SelectionFlagBiomeSuppressed = 1u << 2;
        internal const uint SelectionFlagLootMissing = 1u << 3;
        internal const uint SelectionFlagFault = 1u << 4;
        internal const uint OwnedSlotFlagActive = 1u << 0;
        internal const uint OwnedSlotFlagCullRequested = 1u << 1;
        internal const uint TelemetryFlagSpawned = 1u << 0;
        internal const uint TelemetryFlagCulled = 1u << 1;
        internal const uint TelemetryFlagFault = 1u << 2;
        internal const uint TelemetryFlagLootMissing = 1u << 3;
        private const uint DumpReasonNanHash = 0x534E414Eu; // SNAN
        private const uint DumpReasonLootMissingHash = 0x534C4F54u; // SLOT
        private const uint SourceHash = 0x53323533u; // S253
        private const string DumpPath = "Docs/AgentLogs/Dump_1702.bin";
#if UNITY_EDITOR
        private const string RulesCsvName = "director_spawn_rules.csv";
#endif

        private const BufferID RulesBufferId = BufferID.ShinobuStressDirectorRules;
        private const BufferID RuleLinksBufferId = BufferID.ShinobuStressDirectorRuleLinks;
        private const BufferID CandidatesBufferId = BufferID.ShinobuStressDirectorCandidates;
        private const BufferID SelectionBufferId = BufferID.ShinobuStressDirectorSelection;
        private const BufferID InputBufferId = BufferID.ShinobuStressDirectorInput;
        private const BufferID TuningBufferId = BufferID.ShinobuStressDirectorTuning;
        private const BufferID TelemetryBufferId = BufferID.ShinobuStressDirectorTelemetry;
        private const BufferID CountersBufferId = BufferID.ShinobuStressDirectorCounters;
        private const BufferID FrustumPlanesBufferId = BufferID.ShinobuStressDirectorFrustumPlanes;
        private const BufferID OwnedSlotsBufferId = BufferID.ShinobuStressDirectorOwnedSlots;
        private const BufferID InventoryTicketsBufferId = BufferID.ShinobuStressDirectorInventoryTickets;
        private const BufferID SpawnDebugBufferId = BufferID.ShinobuStressDirectorSpawnDebug;
#if UNITY_EDITOR
        private const BufferID CsvScratchBufferId = BufferID.ShinobuStressDirectorCsvScratch;
#endif
        private const BufferID MesofaunaStateDTOsBufferId = BufferID.ShinobuMesofaunaStates;
        private const BufferID MesofaunaMockPreyTargetsBufferId = BufferID.ShinobuMesofaunaMockPreyTargets;
        private const BufferID MesofaunaVisualSyncBufferId = BufferID.ShinobuMesofaunaVisualSync;
        private static readonly ulong JobBufferMutationGuardMask =
            StressDirectorMutationGuardBit(RulesBufferId) |
            StressDirectorMutationGuardBit(RuleLinksBufferId) |
            StressDirectorMutationGuardBit(CandidatesBufferId) |
            StressDirectorMutationGuardBit(SelectionBufferId) |
            StressDirectorMutationGuardBit(InputBufferId) |
            StressDirectorMutationGuardBit(TuningBufferId) |
            StressDirectorMutationGuardBit(TelemetryBufferId) |
            StressDirectorMutationGuardBit(CountersBufferId) |
            StressDirectorMutationGuardBit(FrustumPlanesBufferId) |
            StressDirectorMutationGuardBit(OwnedSlotsBufferId) |
            StressDirectorMutationGuardBit(InventoryTicketsBufferId) |
            StressDirectorMutationGuardBit(SpawnDebugBufferId);
#if UNITY_EDITOR
        private static readonly ulong ReloadMutationGuardMask =
            StressDirectorMutationGuardBit(RulesBufferId) |
            StressDirectorMutationGuardBit(RuleLinksBufferId) |
            StressDirectorMutationGuardBit(CountersBufferId) |
            StressDirectorMutationGuardBit(CsvScratchBufferId);
#endif

        private const int CounterRuleCount = 0;
        private const int CounterCandidateCount = 1;
        private const int CounterTelemetryCursor = 2;
        private const int CounterOwnedSlotCount = 3;
        private const int CounterCulledLastTick = 4;
        private const int CounterSpawnedLastTick = 5;
        private const int CounterInitialized = 6;
        private const int CounterFaults = 7;
        private const int CounterCsvLoadAttempted = 8;
        private const int CounterCsvLoaded = 9;
        private const int CounterBorrowedCognitionReady = 10;
        private const int CounterCapacity = 16;
        private const int CounterInitializedMagic = unchecked((int)0x253D1A0F);

        private static StressDrivenSpawnDirector _instance;

        private IDataVault _vault;
        private VaultGenerationHandle<SpawnRuleDTO> _rulesHandle;
        private VaultGenerationHandle<SpawnRuleLinkDTO> _ruleLinksHandle;
        private VaultGenerationHandle<DirectorCandidateDTO> _candidatesHandle;
        private VaultGenerationHandle<DirectorSelectionDTO> _selectionHandle;
        private VaultGenerationHandle<DirectorInputDTO> _inputHandle;
        private VaultGenerationHandle<DirectorTuningDTO> _tuningHandle;
        private VaultGenerationHandle<DirectorTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<float4> _frustumPlanesHandle;
        private VaultGenerationHandle<DirectorOwnedSlotDTO> _ownedSlotsHandle;
        private VaultGenerationHandle<InventoryPreloadTicketDTO> _inventoryTicketsHandle;
        private VaultGenerationHandle<DirectorSpawnDebugDTO> _spawnDebugHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _csvScratchHandle;
#endif
        private VaultGenerationHandle<MesofaunaStateDTO> _mesofaunaStatesHandle;
        private VaultGenerationHandle<MesofaunaTargetDTO> _mesofaunaTargetsHandle;
        private VaultGenerationHandle<MesofaunaVisualSyncDTO> _mesofaunaVisualHandle;
        private VaultGenerationHandle<CognitionInput> _cognitionInputsHandle;
        private VaultGenerationHandle<WeatherStateDTO> _weatherStateHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _scalabilityStateHandle;
        private VaultGenerationHandle<EcosystemSectorDTO> _macroSectorSnapshotHandle;
        private VaultGenerationHandle<MacroEcosystemSectorIndexRecord> _macroSectorIndexHandle;
        private VaultGenerationHandle<MacroEcosystemTuningVaultRecord> _macroTuningHandle;
        private IEcosystemDirectorService _ecosystemDirector;
        private JobHandle _activeHandle;
        private IDataVault _jobBufferGuardVault;
        private bool _jobBufferGuardHeld;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private bool _registeredOriginShiftListener;
        private bool _jobScheduled;
        private bool _jobBuffersPinned;
        private long _scheduleTicks;
        private int _lastAppliedFrame = -1;
        private int _monolithReady;
        private int _dumpFaultPending;
        private double3 _cachedFloatingOriginOffset;
        private uint _cachedFloatingOriginSequence;
        private bool _floatingOriginSnapshotValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            if (_instance != null)
                _instance.Dispose();
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying)
                return;

            EnsureInstance();
        }

        public static StressDrivenSpawnDirector EnsureInstance()
        {
            if (_instance == null)
                _instance = new StressDrivenSpawnDirector();
            _instance.TryRegisterTicks();
            return _instance;
        }

        public static bool TryGetTuning(out DirectorTuningDTO tuning)
        {
            tuning = default;
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled)
                return false;

            if (!director.TryRead(vault, in director._tuningHandle, TuningBufferId, SystemID.AIEcology, out NativeArray<DirectorTuningDTO> tuningArray) ||
                !tuningArray.IsCreated ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            tuning = tuningArray[0];
            return true;
        }

        public static bool TrySetTuning(in DirectorTuningDTO tuning)
        {
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled ||
                vault.IsCompactionFenceActive ||
                !IsOwnedVaultHandle(in director._tuningHandle, TuningBufferId, SystemID.AIEcology))
            {
                return false;
            }

            float quality = director.ResolveGlobalQualityWeight(vault);
            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in director._tuningHandle, SystemID.AIEcology, out NativeArray<DirectorTuningDTO> tuningArray))
            {
                return false;
            }

            try
            {
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuningArray[0] = StressDrivenSpawnDirectorSanitizer.Sanitize(tuning, quality);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in director._tuningHandle, SystemID.AIEcology);
            }
        }

#if UNITY_EDITOR
        public static bool TryReloadRulesCold()
        {
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !IsOwnedVaultHandle(in director._rulesHandle, RulesBufferId, SystemID.AIEcology) ||
                !IsOwnedVaultHandle(in director._ruleLinksHandle, RuleLinksBufferId, SystemID.AIEcology) ||
                !IsOwnedVaultHandle(in director._countersHandle, CountersBufferId, SystemID.AIEcology) ||
                !IsOwnedVaultHandle(in director._csvScratchHandle, CsvScratchBufferId, SystemID.AIEcology))
            {
                return false;
            }

            if (!director.TryPinReloadBuffers(vault))
            {
                return false;
            }

            try
            {
                return director.TryLoadRulesCsvCold(vault, forceReload: true, locksHeld: true);
            }
            finally
            {
                vault.ReleaseMutationGuard(ReloadMutationGuardMask);
            }
        }
#endif

        public static bool TryGetLatestTelemetry(out DirectorTelemetryEntry entry)
        {
            entry = default;
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled)
                return false;

            if (!director.TryRead(vault, in director._telemetryHandle, TelemetryBufferId, SystemID.AIEcology, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !director.TryRead(vault, in director._countersHandle, CountersBufferId, SystemID.AIEcology, out NativeArray<int> counters) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                counters.Length <= CounterTelemetryCursor)
            {
                return false;
            }

            int index = counters[CounterTelemetryCursor] - 1;
            if (index < 0)
                index += telemetry.Length;
            if ((uint)index >= (uint)telemetry.Length)
                return false;

            entry = telemetry[index];
            return entry.Frame != 0u || entry.StateHash != 0u;
        }

        public static int CopyTelemetrySnapshot(DirectorTelemetryEntry[] destination)
        {
            if (destination == null ||
                destination.Length == 0 ||
                !TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled ||
                !director.TryRead(vault, in director._telemetryHandle, TelemetryBufferId, SystemID.AIEcology, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return 0;
            }

            int count = math.min(destination.Length, telemetry.Length);
            for (int i = 0; i < count; i++)
                destination[i] = telemetry[i];
            return count;
        }

        public static bool TryGetLatestSpawnDebug(out DirectorSpawnDebugDTO debug)
        {
            debug = default;
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled)
                return false;

            if (!director.TryRead(vault, in director._spawnDebugHandle, SpawnDebugBufferId, SystemID.AIEcology, out NativeArray<DirectorSpawnDebugDTO> debugArray) ||
                !debugArray.IsCreated ||
                debugArray.Length <= 0)
            {
                return false;
            }

            debug = debugArray[0];
            return debug.Frame != 0u && debug.SpeciesHash != 0u;
        }

        public static bool PublishDirectorInput(
            double3 playerAup,
            float3 playerForward,
            float tensionIndex,
            float turbidityScalar,
            float weatherSeverity01,
            uint biomeMask,
            int biomeTransitionTicksRemaining)
        {
            if (!TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault) ||
                director._jobScheduled ||
                !IsOwnedVaultHandle(in director._inputHandle, InputBufferId, SystemID.AIEcology))
            {
                return false;
            }

            bool hasPlayerAup = math.all(math.isfinite(playerAup));
            AbsoluteUniversePositionBlit128 packedPlayerAup = hasPlayerAup ? PackAbsoluteAup(playerAup) : default;
            bool hasPlayerForward = math.all(math.isfinite(playerForward)) && math.lengthsq(playerForward) > 0.0001f;
            float3 packedPlayerForward = hasPlayerForward ? ResolveDirection(playerForward, new float3(0f, 0f, 1f)) : default;
            bool hasTensionIndex = math.isfinite(tensionIndex);
            float safeTensionIndex = hasTensionIndex ? math.saturate(tensionIndex) : 0f;
            bool hasTurbidityScalar = math.isfinite(turbidityScalar);
            float safeTurbidityScalar = hasTurbidityScalar ? math.max(0f, turbidityScalar) : 0f;
            bool hasWeatherSeverity = math.isfinite(weatherSeverity01);
            float safeWeatherSeverity01 = hasWeatherSeverity ? math.saturate(weatherSeverity01) : 0f;
            int safeBiomeTransitionTicks = math.max(0, biomeTransitionTicksRemaining);

            if (!vault.TryAcquireWriteLock(in director._inputHandle, SystemID.AIEcology, out NativeArray<DirectorInputDTO> inputs))
            {
                return false;
            }

            try
            {
                if (!inputs.IsCreated || inputs.Length <= 0)
                    return false;

                DirectorInputDTO input = inputs[0];
                if (hasPlayerAup)
                    input.PlayerAup = packedPlayerAup;
                if (hasPlayerForward)
                    input.PlayerForward = packedPlayerForward;
                if (hasTensionIndex)
                    input.TensionIndex = safeTensionIndex;
                if (hasTurbidityScalar)
                    input.TurbidityScalar = safeTurbidityScalar;
                if (hasWeatherSeverity)
                    input.WeatherSeverity01 = safeWeatherSeverity01;
                input.CurrentBiomeMask = biomeMask == 0u ? input.CurrentBiomeMask : biomeMask;
                input.BiomeTransitionTicksRemaining = safeBiomeTransitionTicks;
                input.Flags |= InputFlagExternalStress;
                inputs[0] = input;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in director._inputHandle, SystemID.AIEcology);
            }
        }

        private StressDrivenSpawnDirector()
        {
            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            RefreshFloatingOriginSnapshotCold();
            if (_vault != null && !_vault.IsAllocationLocked && !_vault.IsCompactionFenceActive)
                EnsureVaultState(_vault);
            TryRegisterTicks();
        }

        public void ColdTick()
        {
            IDataVault vault = _vault;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return;

            _vault = vault;
            if (_jobScheduled)
                return;

            if (!EnsureVaultState(vault))
                return;

            RefreshColdInputs(vault);
#if UNITY_EDITOR
            TryLoadRulesCsvCold(vault, forceReload: false, locksHeld: false);
#endif
            if (!TryGuardJobBuffers(vault) ||
                !TryResolveJobBuffers(
                    vault,
                    out NativeArray<SpawnRuleDTO> rules,
                    out NativeArray<SpawnRuleLinkDTO> links,
                    out NativeArray<DirectorCandidateDTO> candidates,
                    out NativeArray<DirectorSelectionDTO> selection,
                    out NativeArray<DirectorInputDTO> inputs,
                    out NativeArray<DirectorTuningDTO> tuning,
                    out NativeArray<DirectorTelemetryEntry> telemetry,
                    out NativeArray<int> counters,
                    out NativeArray<float4> frustumPlanes,
                    out NativeArray<DirectorOwnedSlotDTO> ownedSlots,
                    out NativeArray<InventoryPreloadTicketDTO> inventoryTickets,
                    out NativeArray<DirectorSpawnDebugDTO> spawnDebug))
            {
                ReleaseJobBufferPins();
                return;
            }

            bool keepPinsForScheduledJob = false;
            try
            {
                DirectorTuningDTO activeTuning = StressDrivenSpawnDirectorSanitizer.Sanitize(tuning[0], ResolveGlobalQualityWeight(vault));
                if (_monolithReady == 0)
                    activeTuning.Flags |= TuningFlagMonolithLootMissing;
                else
                    activeTuning.Flags &= ~TuningFlagMonolithLootMissing;
                tuning[0] = activeTuning;

                int ruleCount = math.min(math.max(counters[CounterRuleCount], 0), math.min(rules.Length, RuleCapacity));
                _scheduleTicks = System.Diagnostics.Stopwatch.GetTimestamp();

                JobHandle handle = default;
                var mockJob = new GenerateMockTensionJob
                {
                    Inputs = inputs,
                    Tuning = tuning,
                    Frame = inputs[0].SimulationTick,
                    WorldSeed = inputs[0].WorldSeed
                };
                handle = mockJob.Schedule(handle);

                var evaluateJob = new EvaluateSpawnConditionsJob
                {
                    Rules = (SpawnRuleDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rules),
                    Links = (SpawnRuleLinkDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(links),
                    Candidates = (DirectorCandidateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(candidates),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    Tuning = (DirectorTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuning),
                    RuleCount = ruleCount,
                    CandidateCapacity = candidates.Length
                };
                handle = evaluateJob.Schedule(handle);

                var selectionJob = new AllocateThreatBudgetJob
                {
                    Candidates = (DirectorCandidateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(candidates),
                    Selection = (DirectorSelectionDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(selection),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    Tuning = (DirectorTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuning),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    CandidateCapacity = candidates.Length,
                    MonolithReady = _monolithReady
                };
                handle = selectionJob.Schedule(handle);

                var hiddenJob = new CalculateHiddenSpawnAupJob
                {
                    FrustumPlanes = (float4*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(frustumPlanes),
                    Selection = (DirectorSelectionDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(selection),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    Tuning = (DirectorTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuning),
                    PlaneCount = math.min(frustumPlanes.Length, 6),
                    ProbeCapacity = MaxHiddenProbeCapacity
                };
                handle = hiddenJob.Schedule(handle);

                var cullJob = new CullDistantDirectorSlotsJob
                {
                    OwnedSlots = (DirectorOwnedSlotDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(ownedSlots),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    Tuning = (DirectorTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuning),
                    OwnedSlotCapacity = ownedSlots.Length
                };
                handle = cullJob.Schedule(handle);

                var inventoryJob = new AsyncInventoryPreloadTicketJob
                {
                    Selection = (DirectorSelectionDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(selection),
                    Tickets = (InventoryPreloadTicketDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(inventoryTickets),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    TicketCapacity = inventoryTickets.Length,
                    MonolithReady = _monolithReady
                };
                handle = inventoryJob.Schedule(handle);

                var telemetryJob = new RecordDirectorTelemetryJob
                {
                    Selection = (DirectorSelectionDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(selection),
                    Inputs = (DirectorInputDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(inputs),
                    Tuning = (DirectorTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(tuning),
                    Telemetry = (DirectorTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafePtr(telemetry),
                    Counters = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(counters),
                    SpawnDebug = (DirectorSpawnDebugDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(spawnDebug),
                    TelemetryCapacity = telemetry.Length
                };
                handle = telemetryJob.Schedule(handle);

                _activeHandle = handle;
                _jobScheduled = true;
                keepPinsForScheduledJob = true;
            }
            finally
            {
                if (!keepPinsForScheduledJob)
                    ReleaseJobBufferPins();
            }
        }

        public void LateFrameTick()
        {
            if (!_jobScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _activeHandle, forceComplete: false))
                return;

            IDataVault lockedVault = _jobBufferGuardVault;
            bool canCommit = lockedVault != null && ReferenceEquals(lockedVault, _vault);
            string pendingDumpPath = null;
            NativeArray<byte> pendingDumpPayload = default;
            int pendingDumpByteCount = 0;
            try
            {
                try
                {
                    if (!canCommit)
                    {
                        _dumpFaultPending = 0;
                        return;
                    }

                    float micros = ResolveElapsedMicroseconds();
                    PatchLatestTelemetryMicros(lockedVault, micros);
                    ApplyCullRequests(lockedVault);
                    ApplyCompletedSelection(lockedVault);
                    if (_dumpFaultPending != 0)
                    {
                        TryStageBlackBoxDumpCold(
                            lockedVault,
                            _dumpFaultPending == 2 ? DumpReasonLootMissingHash : DumpReasonNanHash,
                            out pendingDumpPath,
                            out pendingDumpPayload,
                            out pendingDumpByteCount);
                        _dumpFaultPending = 0;
                    }
                }
                finally
                {
                    ReleaseJobBufferPins();
                    _jobScheduled = false;
                }

                if (pendingDumpPayload.IsCreated)
                    TrySubmitBlackBoxDump(pendingDumpPath, pendingDumpPayload, pendingDumpByteCount);
            }
            finally
            {
                if (pendingDumpPayload.IsCreated)
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref pendingDumpPayload,
                        nameof(StressDrivenSpawnDirector),
                        "DirectorTelemetryDumpPayload");
                }
            }
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            _ = previousService;
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.EcosystemDirector)
            {
                _ecosystemDirector = currentService as IEcosystemDirectorService;
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.FloatingOriginRuntime)
            {
                RefreshFloatingOriginSnapshotCold();
                TryRegisterOriginShiftListener();
            }
            else if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregisterDispatcherTicks();
                if (currentService != null)
                    TryRegisterTicks();
            }
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            float3 shiftOffset = new float3(shiftData.ShiftOffset.x, shiftData.ShiftOffset.y, shiftData.ShiftOffset.z);
            float shiftSqrMagnitude = math.lengthsq(shiftOffset);
            if (!math.all(math.isfinite(shiftOffset)) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f)
            {
                return;
            }

            double3 origin = shiftData.NewTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
            {
                _floatingOriginSnapshotValid = false;
                _dumpFaultPending = 1;
                return;
            }

            _cachedFloatingOriginOffset = origin;
            _cachedFloatingOriginSequence = shiftData.Sequence;
            _floatingOriginSnapshotValid = true;
        }

        public void Dispose()
        {
            TryUnregisterTicks();
            RebindDataVaultForLifecycle(null);
        }

        private void CompleteScheduledJobAndReleaseLocksForLifecycle()
        {
            if (_jobScheduled)
                DispatcherJobFence.TryComplete(ref _activeHandle, forceComplete: true);

            ReleaseJobBufferPins();

            _activeHandle = default;
            _jobScheduled = false;
        }

        private void RebindDataVaultForLifecycle(IDataVault currentVault)
        {
            if (ReferenceEquals(_vault, currentVault))
                return;

            CompleteScheduledJobAndReleaseLocksForLifecycle();
            ReleaseOwnedVaultHandles(_vault);
            ClearHandlesCold();
            _vault = currentVault;
            _lastAppliedFrame = -1;
            _monolithReady = 0;
            _dumpFaultPending = 0;
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (vault == null)
                return false;

            _rulesHandle = EnsureHandle(vault, _rulesHandle, RulesBufferId, RuleCapacity, SystemID.AIEcology);
            _ruleLinksHandle = EnsureHandle(vault, _ruleLinksHandle, RuleLinksBufferId, RuleCapacity, SystemID.AIEcology);
            _candidatesHandle = EnsureHandle(vault, _candidatesHandle, CandidatesBufferId, CandidateCapacity, SystemID.AIEcology);
            _selectionHandle = EnsureHandle(vault, _selectionHandle, SelectionBufferId, 1, SystemID.AIEcology);
            _inputHandle = EnsureHandle(vault, _inputHandle, InputBufferId, 1, SystemID.AIEcology);
            _tuningHandle = EnsureHandle(vault, _tuningHandle, TuningBufferId, 1, SystemID.AIEcology);
            _telemetryHandle = EnsureHandle(vault, _telemetryHandle, TelemetryBufferId, TelemetryCapacity, SystemID.AIEcology);
            _countersHandle = EnsureHandle(vault, _countersHandle, CountersBufferId, CounterCapacity, SystemID.AIEcology, NativeArrayOptions.ClearMemory);
#if UNITY_EDITOR
            _csvScratchHandle = EnsureHandle(vault, _csvScratchHandle, CsvScratchBufferId, CsvScratchBytes, SystemID.AIEcology);
#endif
            _frustumPlanesHandle = EnsureHandle(vault, _frustumPlanesHandle, FrustumPlanesBufferId, 6, SystemID.AIEcology);
            _ownedSlotsHandle = EnsureHandle(vault, _ownedSlotsHandle, OwnedSlotsBufferId, OwnedSlotCapacity, SystemID.AIEcology);
            _inventoryTicketsHandle = EnsureHandle(vault, _inventoryTicketsHandle, InventoryTicketsBufferId, RuleCapacity, SystemID.AIEcology);
            _spawnDebugHandle = EnsureHandle(vault, _spawnDebugHandle, SpawnDebugBufferId, 1, SystemID.AIEcology);

            if (!TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) ||
                !TryResolve(vault, in _tuningHandle, TuningBufferId, out NativeArray<DirectorTuningDTO> tuning) ||
                !TryResolve(vault, in _frustumPlanesHandle, FrustumPlanesBufferId, out NativeArray<float4> frustumPlanes) ||
                !TryResolve(vault, in _rulesHandle, RulesBufferId, out NativeArray<SpawnRuleDTO> rules) ||
                !TryResolve(vault, in _ruleLinksHandle, RuleLinksBufferId, out NativeArray<SpawnRuleLinkDTO> links) ||
                !TryResolve(vault, in _candidatesHandle, CandidatesBufferId, out NativeArray<DirectorCandidateDTO> candidates) ||
                !TryResolve(vault, in _selectionHandle, SelectionBufferId, out NativeArray<DirectorSelectionDTO> selection) ||
                !TryResolve(vault, in _inputHandle, InputBufferId, out NativeArray<DirectorInputDTO> inputs) ||
                !TryResolve(vault, in _telemetryHandle, TelemetryBufferId, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !TryResolve(vault, in _ownedSlotsHandle, OwnedSlotsBufferId, out NativeArray<DirectorOwnedSlotDTO> ownedSlots) ||
                !TryResolve(vault, in _inventoryTicketsHandle, InventoryTicketsBufferId, out NativeArray<InventoryPreloadTicketDTO> inventoryTickets) ||
                !TryResolve(vault, in _spawnDebugHandle, SpawnDebugBufferId, out NativeArray<DirectorSpawnDebugDTO> spawnDebug))
            {
                return false;
            }

            if (counters.Length <= CounterInitialized)
                return false;

            if (counters[CounterInitialized] != CounterInitializedMagic)
            {
                if (!TryGuardJobBuffers(vault) ||
                    !TryResolveJobBuffers(
                        vault,
                        out rules,
                        out links,
                        out candidates,
                        out selection,
                        out inputs,
                        out tuning,
                        out telemetry,
                        out counters,
                        out frustumPlanes,
                        out ownedSlots,
                        out inventoryTickets,
                        out spawnDebug))
                {
                    ReleaseJobBufferPins();
                    return false;
                }

                try
                {
                    InitializeColdDefaults(
                        vault,
                        counters,
                        tuning,
                        frustumPlanes,
                        rules,
                        links,
                        candidates,
                        selection,
                        inputs,
                        telemetry,
                        ownedSlots,
                        inventoryTickets,
                        spawnDebug);
                    counters[CounterInitialized] = CounterInitializedMagic;
                }
                finally
                {
                    ReleaseJobBufferPins();
                }
            }

            RefreshBorrowedCognitionHandles(vault);
            _monolithReady = ResolveDataMonolithReadyCold() ? 1 : 0;
            return true;
        }

        private void RefreshBorrowedCognitionHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            bool statesReady = TryRefreshBorrowedHandle(vault, MesofaunaStateDTOsBufferId, SystemID.AICognition, ref _mesofaunaStatesHandle);
            bool targetsReady = TryRefreshBorrowedHandle(vault, MesofaunaMockPreyTargetsBufferId, SystemID.AICognition, ref _mesofaunaTargetsHandle);
            bool visualsReady = TryRefreshBorrowedHandle(vault, MesofaunaVisualSyncBufferId, SystemID.AICognition, ref _mesofaunaVisualHandle);
            bool inputsReady = TryRefreshBorrowedHandle(vault, BufferID.PredatorCognitionInputs, SystemID.AICognition, ref _cognitionInputsHandle);

            if (!IsOwnedVaultHandle(in _countersHandle, CountersBufferId, SystemID.AIEcology) ||
                !vault.TryAcquireWriteLock(in _countersHandle, SystemID.AIEcology, out NativeArray<int> counters))
                return;

            try
            {
                if (counters.IsCreated && counters.Length > CounterBorrowedCognitionReady)
                    counters[CounterBorrowedCognitionReady] = statesReady && targetsReady && visualsReady && inputsReady ? 1 : 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _countersHandle, SystemID.AIEcology);
            }
        }

        private static bool TryRefreshBorrowedHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            SystemID owner,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId, owner) &&
                vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length > 0)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> refreshed))
                return false;

            handle = refreshed;
            return IsOwnedVaultHandle(in handle, bufferId, owner) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static VaultGenerationHandle<T> EnsureHandle<T>(
            IDataVault vault,
            VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int length,
            SystemID owner,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : struct
        {
            if (IsOwnedVaultHandle(in handle, bufferId, owner) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= length)
            {
                return handle;
            }

            if (IsOwnedVaultHandle(in handle, bufferId, owner))
                vault.ReleaseBuffer(in handle);

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(bufferId, length, owner, options);
            if (IsOwnedVaultHandle(in acquired, bufferId, owner))
                return acquired;

            return default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, SystemID expectedOwner) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)expectedOwner;
        }

        private static ulong StressDirectorMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryResolve<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> array) where T : struct
        {
            array = default;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId, SystemID.AIEcology) &&
                   vault.TryResolveHandle(in handle, out array) &&
                   array.IsCreated;
        }

        private bool TryRead<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            SystemID expectedOwner,
            out NativeArray<T> array) where T : struct
        {
            array = default;
            return vault != null &&
                   IsOwnedVaultHandle(in handle, expectedBufferId, expectedOwner) &&
                   vault.TryReadHandle(in handle, out array) &&
                   array.IsCreated;
        }

        private void InitializeColdDefaults(
            IDataVault vault,
            NativeArray<int> counters,
            NativeArray<DirectorTuningDTO> tuning,
            NativeArray<float4> frustumPlanes,
            NativeArray<SpawnRuleDTO> rules,
            NativeArray<SpawnRuleLinkDTO> links,
            NativeArray<DirectorCandidateDTO> candidates,
            NativeArray<DirectorSelectionDTO> selection,
            NativeArray<DirectorInputDTO> inputs,
            NativeArray<DirectorTelemetryEntry> telemetry,
            NativeArray<DirectorOwnedSlotDTO> ownedSlots,
            NativeArray<InventoryPreloadTicketDTO> inventoryTickets,
            NativeArray<DirectorSpawnDebugDTO> spawnDebug)
        {
            for (int i = 0; i < counters.Length; i++)
                counters[i] = 0;

            if (tuning.Length > 0)
                tuning[0] = DirectorTuningDTO.CreateDefault(ResolveGlobalQualityWeight(vault));

            InitializeDefaultFrustumPlanes(frustumPlanes);
            int count = InitializeDefaultRules(rules, links);
            ClearArray(candidates);
            ClearArray(selection);
            ClearArray(telemetry);
            ClearArray(ownedSlots);
            ClearArray(inventoryTickets);
            ClearArray(spawnDebug);
            InitializeInputDefaults(vault, inputs);
            counters[CounterRuleCount] = count;
            counters[CounterTelemetryCursor] = 0;
            counters[CounterOwnedSlotCount] = 0;
        }

        private static void ClearArray<T>(NativeArray<T> array) where T : unmanaged
        {
            if (!array.IsCreated)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = default;
        }

        private void InitializeInputDefaults(IDataVault vault, NativeArray<DirectorInputDTO> inputs)
        {
            if (!inputs.IsCreated || inputs.Length <= 0)
                return;

            DirectorInputDTO input = default;
            double3 origin = _floatingOriginSnapshotValid ? _cachedFloatingOriginOffset : double3.zero;
            AbsoluteUniversePositionBlit128 originAup = PackAbsoluteAup(origin);
            input.PlayerAup = originAup;
            input.FloatingOriginAup = originAup;
            input.PlayerForward = new float3(0f, 0f, 1f);
            input.TurbidityScalar = 1f;
            input.GlobalQualityWeight = ResolveGlobalQualityWeight(vault);
            input.WorldSeed = ResolveWorldSeed(0u);
            input.OriginShiftSequence = _cachedFloatingOriginSequence;
            if (!_floatingOriginSnapshotValid)
                input.Flags |= InputFlagOriginInvalid;
            inputs[0] = input;
        }

        private static void InitializeDefaultFrustumPlanes(NativeArray<float4> planes)
        {
            if (!planes.IsCreated || planes.Length < 6)
                return;

            planes[0] = new float4(1f, 0f, 0f, 18f);
            planes[1] = new float4(-1f, 0f, 0f, 18f);
            planes[2] = new float4(0f, 1f, 0f, 14f);
            planes[3] = new float4(0f, -1f, 0f, 14f);
            planes[4] = new float4(0f, 0f, 1f, 8f);
            planes[5] = new float4(0f, 0f, -1f, 160f);
        }

        private static int InitializeDefaultRules(NativeArray<SpawnRuleDTO> rules, NativeArray<SpawnRuleLinkDTO> links)
        {
            if (!rules.IsCreated || !links.IsCreated || rules.Length <= 0 || links.Length <= 0)
                return 0;

            for (int i = 0; i < rules.Length; i++)
            {
                rules[i] = default;
                links[i] = default;
            }

            int count = math.min(4, math.min(rules.Length, links.Length));
            WriteRule(rules, links, 0, HashLower("hadal_stalker"), HashLower("loot_hadal_stalker"), 0.46f, 1.0f, 2.85f, 0xFFFFFFFFu, 1.4f, 0.15f);
            WriteRule(rules, links, 1, HashLower("reef_eel_swarm"), HashLower("loot_reef_eel"), 0.25f, 0.72f, 0.42f, 0xFFFFFFFFu, 0.72f, 4.5f);
            WriteRule(rules, links, 2, HashLower("thermal_maw"), HashLower("loot_thermal_maw"), 0.62f, 1.0f, 3.6f, 0xFFFFFFFFu, 1.75f, 0.05f);
            WriteRule(rules, links, 3, HashLower("silt_needle_pack"), HashLower("loot_silt_needle"), 0.35f, 0.88f, 0.86f, 0xFFFFFFFFu, 1.0f, 2.0f);
            return count;
        }

        private static void WriteRule(
            NativeArray<SpawnRuleDTO> rules,
            NativeArray<SpawnRuleLinkDTO> links,
            int index,
            uint speciesHash,
            uint lootHash,
            float minTension,
            float maxTension,
            float cpuCost,
            uint biomeMask,
            float threatWeight,
            float swarmBias)
        {
            SpawnRuleDTO rule = default;
            rule.SpeciesHash = speciesHash;
            rule.MinTension = math.saturate(minTension);
            rule.MaxTension = math.max(rule.MinTension, math.saturate(maxTension));
            rule.CPUCostScalar = math.max(0.01f, cpuCost);
            rule.RequiredBiomeMask = biomeMask;
            rules[index] = rule;

            SpawnRuleLinkDTO link = default;
            link.SpeciesHash = speciesHash;
            link.LootTableHash = lootHash;
            link.ArchetypeFlags = 0u;
            link.ThreatWeight = math.max(0.01f, threatWeight);
            link.SwarmCountBias = math.max(0f, swarmBias);
            links[index] = link;
        }

        private void RefreshColdInputs(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            if (!TryRead(vault, in _inputHandle, InputBufferId, SystemID.AIEcology, out NativeArray<DirectorInputDTO> snapshotInputs) ||
                !snapshotInputs.IsCreated ||
                snapshotInputs.Length <= 0)
            {
                return;
            }

            DirectorInputDTO input = snapshotInputs[0];
            bool originValid = _floatingOriginSnapshotValid && math.all(math.isfinite(_cachedFloatingOriginOffset));
            double3 origin = originValid ? _cachedFloatingOriginOffset : double3.zero;
            if (!originValid)
            {
                input.Flags |= InputFlagOriginInvalid;
            }
            else
            {
                input.Flags &= ~InputFlagOriginInvalid;
            }

            AbsoluteUniversePositionBlit128 originAup = PackAbsoluteAup(origin);
            if (!IsFiniteAup(in input.PlayerAup) || IsZeroAup(in input.PlayerAup))
                input.PlayerAup = originAup;
            input.FloatingOriginAup = originAup;
            input.OriginShiftSequence = _cachedFloatingOriginSequence;
            input.PlayerForward = ResolveDirection(input.PlayerForward, new float3(0f, 0f, 1f));
            input.GlobalQualityWeight = ResolveGlobalQualityWeight(vault);
            input.ThermalPressure01 = ResolveThermalPressure(vault);
            input.FrameTimeMs = ResolveFrameTimeMs(input.FrameTimeMs);
            input.SimulationTick = input.SimulationTick == uint.MaxValue ? 1u : input.SimulationTick + 1u;
            input.Frame = ResolveFrameId(input.SimulationTick);
            input.SectorHash = ResolveSectorHash32(input.PlayerAup);
            input.WorldSeed = ResolveWorldSeed(input.SectorHash);
            RefreshWeatherInputs(vault, ref input);
            RefreshMacroEcosystemInputs(vault, ref input);

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireWriteLock(in _inputHandle, SystemID.AIEcology, out NativeArray<DirectorInputDTO> inputs))
            {
                return;
            }

            try
            {
                if (!inputs.IsCreated || inputs.Length <= 0)
                    return;

                inputs[0] = input;
            }
            finally
            {
                vault.ReleaseWriteLock(in _inputHandle, SystemID.AIEcology);
            }
        }

        private void RefreshWeatherInputs(IDataVault vault, ref DirectorInputDTO input)
        {
            if (TryRefreshBorrowedHandle(vault, BufferID.ShinobuOceanWeatherState, SystemID.HabitatAtmosphere, ref _weatherStateHandle) &&
                TryRead(vault, in _weatherStateHandle, BufferID.ShinobuOceanWeatherState, SystemID.HabitatAtmosphere, out NativeArray<WeatherStateDTO> weather) &&
                weather.IsCreated &&
                weather.Length > 0)
            {
                WeatherStateDTO state = weather[0];
                float storm = math.saturate(math.select(input.WeatherSeverity01, state.WindDirectionSpeedStorm.w, math.isfinite(state.WindDirectionSpeedStorm.w)));
                float disturbance = math.saturate(math.select(0f, state.SurfaceScalars.w, math.isfinite(state.SurfaceScalars.w)));
                input.WeatherSeverity01 = math.max(input.WeatherSeverity01, math.max(storm, disturbance));
                input.TurbidityScalar = math.max(input.TurbidityScalar, 1f + input.WeatherSeverity01 * 1.6f);
            }
        }

        private void RefreshMacroEcosystemInputs(IDataVault vault, ref DirectorInputDTO input)
        {
            bool previousMacroValid = input.MacroEcosystemFlags != 0u;
            input.PreyBiomass01 = math.saturate(math.select(0.5f, input.PreyBiomass01, previousMacroValid & math.isfinite(input.PreyBiomass01)));
            input.PredatorBiomass01 = math.saturate(math.select(0.18f, input.PredatorBiomass01, previousMacroValid & math.isfinite(input.PredatorBiomass01)));
            input.CarryingCapacity01 = math.saturate(math.select(1f, input.CarryingCapacity01, previousMacroValid & math.isfinite(input.CarryingCapacity01) & input.CarryingCapacity01 > 0f));
            input.LocalTemperature = math.select(4f, input.LocalTemperature, math.isfinite(input.LocalTemperature));
            input.ToxinLevel01 = math.saturate(math.select(0f, input.ToxinLevel01, math.isfinite(input.ToxinLevel01)));
            input.MacroEcosystemFlags = 0u;

            if (TryApplyMacroEcosystemContractsSnapshot(vault, ref input))
                return;

            if (TryRefreshMacroEcosystemServiceCold(ref input))
                return;

            input.MacroEcosystemStateHash = 0u;
        }

        private bool TryApplyMacroEcosystemContractsSnapshot(IDataVault vault, ref DirectorInputDTO input)
        {
            if (!TryRefreshBorrowedHandle(vault, BufferID.ShinobuMacroEcosystemSectorFront, SystemID.AIEcology, ref _macroSectorSnapshotHandle) ||
                !TryRefreshBorrowedHandle(vault, BufferID.ShinobuMacroEcosystemIndexEntries, SystemID.AIEcology, ref _macroSectorIndexHandle) ||
                !TryRefreshBorrowedHandle(vault, BufferID.ShinobuMacroEcosystemTuning, SystemID.AIEcology, ref _macroTuningHandle) ||
                !TryRead(vault, in _macroSectorSnapshotHandle, BufferID.ShinobuMacroEcosystemSectorFront, SystemID.AIEcology, out NativeArray<EcosystemSectorDTO> sectors) ||
                !TryRead(vault, in _macroSectorIndexHandle, BufferID.ShinobuMacroEcosystemIndexEntries, SystemID.AIEcology, out NativeArray<MacroEcosystemSectorIndexRecord> entries) ||
                !TryRead(vault, in _macroTuningHandle, BufferID.ShinobuMacroEcosystemTuning, SystemID.AIEcology, out NativeArray<MacroEcosystemTuningVaultRecord> tuning) ||
                tuning.Length <= 0)
            {
                return false;
            }

            MacroEcosystemTuningVaultRecord tune = tuning[0];
            if ((tune.Flags & MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight) != 0u)
                return false;

            ulong sectorHash = ResolveSectorHash64(input.PlayerAup);
            if (!MacroEcosystemVaultContract.TryResolveSectorIndex(entries, sectorHash, out int index) ||
                (uint)index >= (uint)sectors.Length)
            {
                return false;
            }

            EcosystemSectorDTO sector = sectors[index];
            MacroEcosystemTuningVaultRecord postRead = tuning[0];
            if (sector.SectorHash != sectorHash ||
                postRead.Flags != tune.Flags ||
                postRead.StateHash != tune.StateHash ||
                (postRead.Flags & MacroEcosystemVaultContract.TuningFlagSnapshotWriteInFlight) != 0u)
            {
                return false;
            }

            float preyCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPrey,
                tune.CarryingCapacityPrey,
                math.isfinite(tune.CarryingCapacityPrey) & tune.CarryingCapacityPrey > 0f));
            float predatorCapacity = math.max(1f, math.select(
                MacroEcosystemVaultContract.DefaultCarryingCapacityPredator,
                tune.CarryingCapacityPredator,
                math.isfinite(tune.CarryingCapacityPredator) & tune.CarryingCapacityPredator > 0f));
            float defaultCapacity = MacroEcosystemVaultContract.DefaultCarryingCapacityPrey + MacroEcosystemVaultContract.DefaultCarryingCapacityPredator;

            float sectorCapacity = math.max(1f, math.select(preyCapacity + predatorCapacity, sector.CarryingCapacity, math.isfinite(sector.CarryingCapacity) & sector.CarryingCapacity > 0f));
            input.PreyBiomass01 = math.saturate(sector.PreyBiomass * math.rcp(sectorCapacity));
            input.PredatorBiomass01 = math.saturate(sector.PredatorBiomass * math.rcp(sectorCapacity));
            input.CarryingCapacity01 = math.saturate(sectorCapacity * math.rcp(math.max(1f, defaultCapacity)));
            input.MacroEcosystemStateHash = tune.StateHash;
            input.MacroEcosystemFlags = 1u;
            input.SectorHash = (uint)(sectorHash ^ (sectorHash >> 32));
            return true;
        }

        private bool TryRefreshMacroEcosystemServiceCold(ref DirectorInputDTO input)
        {
            IEcosystemDirectorService ecosystem = _ecosystemDirector;
            if (ecosystem == null || !ecosystem.IsInitialized)
                return false;

            Vector3 runtime = ToRuntimeVector3(in input.PlayerAup, in input.FloatingOriginAup);
            if (!ecosystem.TryGetBiomassAvailability(runtime, out float prey, out float predator, out float capacity))
                return false;

            input.PreyBiomass01 = math.saturate(math.select(input.PreyBiomass01, prey, math.isfinite(prey)));
            input.PredatorBiomass01 = math.saturate(math.select(input.PredatorBiomass01, predator, math.isfinite(predator)));
            input.CarryingCapacity01 = math.saturate(math.select(input.CarryingCapacity01, capacity, math.isfinite(capacity)));
            input.MacroEcosystemStateHash = Hash3(input.SectorHash, input.SimulationTick, 0x4D414352u);
            input.MacroEcosystemFlags = 2u;
            return true;
        }

        private float ResolveGlobalQualityWeight(IDataVault vault)
        {
            if (TryRefreshBorrowedHandle(vault, BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability, ref _scalabilityStateHandle) &&
                TryRead(vault, in _scalabilityStateHandle, BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability, out NativeArray<ScalabilityStateDTO> state) &&
                state.IsCreated &&
                state.Length > 0 &&
                math.isfinite(state[0].GlobalQualityWeight))
            {
                return math.saturate(state[0].GlobalQualityWeight);
            }

            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, weight, math.isfinite(weight)));
        }

        private float ResolveThermalPressure(IDataVault vault)
        {
            if (TryRefreshBorrowedHandle(vault, BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability, ref _scalabilityStateHandle) &&
                TryRead(vault, in _scalabilityStateHandle, BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability, out NativeArray<ScalabilityStateDTO> state) &&
                state.IsCreated &&
                state.Length > 0 &&
                math.isfinite(state[0].ThermalIndex))
            {
                return math.saturate(state[0].ThermalIndex);
            }

            return 0f;
        }

        private bool TryResolveJobBuffers(
            IDataVault vault,
            out NativeArray<SpawnRuleDTO> rules,
            out NativeArray<SpawnRuleLinkDTO> links,
            out NativeArray<DirectorCandidateDTO> candidates,
            out NativeArray<DirectorSelectionDTO> selection,
            out NativeArray<DirectorInputDTO> inputs,
            out NativeArray<DirectorTuningDTO> tuning,
            out NativeArray<DirectorTelemetryEntry> telemetry,
            out NativeArray<int> counters,
            out NativeArray<float4> frustumPlanes,
            out NativeArray<DirectorOwnedSlotDTO> ownedSlots,
            out NativeArray<InventoryPreloadTicketDTO> inventoryTickets,
            out NativeArray<DirectorSpawnDebugDTO> spawnDebug)
        {
            rules = default;
            links = default;
            candidates = default;
            selection = default;
            inputs = default;
            tuning = default;
            telemetry = default;
            counters = default;
            frustumPlanes = default;
            ownedSlots = default;
            inventoryTickets = default;
            spawnDebug = default;

            return TryResolve(vault, in _rulesHandle, RulesBufferId, out rules) &&
                   TryResolve(vault, in _ruleLinksHandle, RuleLinksBufferId, out links) &&
                   TryResolve(vault, in _candidatesHandle, CandidatesBufferId, out candidates) &&
                   TryResolve(vault, in _selectionHandle, SelectionBufferId, out selection) &&
                   TryResolve(vault, in _inputHandle, InputBufferId, out inputs) &&
                   TryResolve(vault, in _tuningHandle, TuningBufferId, out tuning) &&
                   TryResolve(vault, in _telemetryHandle, TelemetryBufferId, out telemetry) &&
                   TryResolve(vault, in _countersHandle, CountersBufferId, out counters) &&
                   TryResolve(vault, in _frustumPlanesHandle, FrustumPlanesBufferId, out frustumPlanes) &&
                   TryResolve(vault, in _ownedSlotsHandle, OwnedSlotsBufferId, out ownedSlots) &&
                   TryResolve(vault, in _inventoryTicketsHandle, InventoryTicketsBufferId, out inventoryTickets) &&
                   TryResolve(vault, in _spawnDebugHandle, SpawnDebugBufferId, out spawnDebug);
        }

        private bool TryValidateJobBuffers(IDataVault vault)
        {
            return TryResolveJobBuffers(
                vault,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private bool TryGuardJobBuffers(IDataVault vault)
        {
            ReleaseJobBufferPins();
            if (vault == null || vault.IsCompactionFenceActive || !TryValidateJobBuffers(vault))
                return false;

            bool pinned = false;
            try
            {
                if (!TryAcquireJobBufferMutationGuard(vault))
                    return false;

                if (!TryValidateJobBuffers(vault))
                    return false;

                _jobBuffersPinned = true;
                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseJobBufferPins();
            }
        }

        private void ReleaseJobBufferPins()
        {
            if (!_jobBuffersPinned)
            {
                ReleaseJobBufferGuard();
                return;
            }

            ReleaseJobBufferGuard();
            _jobBuffersPinned = false;
        }

        private bool TryAcquireJobBufferMutationGuard(IDataVault vault)
        {
            if (_jobBufferGuardHeld)
                return ReferenceEquals(_jobBufferGuardVault, vault);

            if (vault == null || !vault.TryAcquireMutationGuard(JobBufferMutationGuardMask))
                return false;

            _jobBufferGuardVault = vault;
            _jobBufferGuardHeld = true;
            return true;
        }

        private void ReleaseJobBufferGuard()
        {
            IDataVault vault = _jobBufferGuardVault;
            bool held = _jobBufferGuardHeld;
            _jobBufferGuardVault = null;
            _jobBufferGuardHeld = false;

            if (held && vault != null)
                vault.ReleaseMutationGuard(JobBufferMutationGuardMask);
        }

#if UNITY_EDITOR
        private bool TryPinReloadBuffers(IDataVault vault)
        {
            if (vault == null || vault.IsCompactionFenceActive || !TryValidateReloadBuffers(vault))
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireMutationGuard(ReloadMutationGuardMask))
                    return false;

                acquired = true;
                if (!TryValidateReloadBuffers(vault))
                    return false;

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                    vault.ReleaseMutationGuard(ReloadMutationGuardMask);
            }
        }

        private bool TryValidateReloadBuffers(IDataVault vault)
        {
            return TryResolve(vault, in _rulesHandle, RulesBufferId, out NativeArray<SpawnRuleDTO> rules) &&
                   TryResolve(vault, in _ruleLinksHandle, RuleLinksBufferId, out NativeArray<SpawnRuleLinkDTO> links) &&
                   TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) &&
                   TryResolve(vault, in _csvScratchHandle, CsvScratchBufferId, out NativeArray<byte> scratch) &&
                   rules.IsCreated &&
                   links.IsCreated &&
                   counters.IsCreated &&
                   scratch.IsCreated;
        }
#endif

        private float ResolveElapsedMicroseconds()
        {
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - _scheduleTicks;
            double ticksPerSecond = System.Diagnostics.Stopwatch.Frequency;
            if (elapsed <= 0L || ticksPerSecond <= 0d)
                return 0f;
            return (float)(elapsed * 1000000d / ticksPerSecond);
        }

        private void PatchLatestTelemetryMicros(IDataVault vault, float micros)
        {
            if (!TryResolve(vault, in _telemetryHandle, TelemetryBufferId, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return;
            }

            int cursor = counters[CounterTelemetryCursor] - 1;
            if (cursor < 0)
                cursor += telemetry.Length;
            if ((uint)cursor >= (uint)telemetry.Length)
                return;

            DirectorTelemetryEntry entry = telemetry[cursor];
            entry.ChainMicroseconds = math.max(0f, micros);
            telemetry[cursor] = entry;
        }

        private void PatchLatestTelemetryAfterApply(IDataVault vault, bool spawned, int slot)
        {
            if (!TryResolve(vault, in _telemetryHandle, TelemetryBufferId, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return;
            }

            int cursor = counters[CounterTelemetryCursor] - 1;
            if (cursor < 0)
                cursor += telemetry.Length;
            if ((uint)cursor >= (uint)telemetry.Length)
                return;

            DirectorTelemetryEntry entry = telemetry[cursor];
            entry.Spawned = (ushort)math.select(0, 1, spawned);
            entry.Flags = spawned ? (entry.Flags | TelemetryFlagSpawned) : (entry.Flags & ~TelemetryFlagSpawned);
            entry.SpawnSlot = (uint)math.max(0, slot);
            telemetry[cursor] = entry;
        }

        private void ApplyCullRequests(IDataVault vault)
        {
            if (!TryResolve(vault, in _ownedSlotsHandle, OwnedSlotsBufferId, out NativeArray<DirectorOwnedSlotDTO> ownedSlots) ||
                !TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) ||
                !ownedSlots.IsCreated ||
                !counters.IsCreated ||
                counters.Length <= CounterOwnedSlotCount)
            {
                return;
            }

            int count = math.min(math.max(0, counters[CounterOwnedSlotCount]), ownedSlots.Length);
            int culled = 0;
            for (int i = 0; i < count;)
            {
                DirectorOwnedSlotDTO owned = ownedSlots[i];
                if ((owned.Flags & OwnedSlotFlagCullRequested) == 0u)
                {
                    i++;
                    continue;
                }

                if (owned.Slot >= 0)
                    PredatorCognitionDomain.Unregister(owned.Slot);

                count--;
                ownedSlots[i] = ownedSlots[count];
                ownedSlots[count] = default;
                culled++;
            }

            counters[CounterOwnedSlotCount] = count;
            counters[CounterCulledLastTick] = culled;
        }

        private void ApplyCompletedSelection(IDataVault vault)
        {
            if (!TryResolve(vault, in _selectionHandle, SelectionBufferId, out NativeArray<DirectorSelectionDTO> selectionArray) ||
                !TryResolve(vault, in _ownedSlotsHandle, OwnedSlotsBufferId, out NativeArray<DirectorOwnedSlotDTO> ownedSlots) ||
                !TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters) ||
                selectionArray.Length <= 0 ||
                counters.Length <= CounterOwnedSlotCount)
            {
                return;
            }

            DirectorSelectionDTO selection = selectionArray[0];
            int selectionFrame = unchecked((int)selection.Frame);
            if (selectionFrame == _lastAppliedFrame || selection.RequestSpawn == 0)
                return;

            if ((selection.Flags & SelectionFlagFault) != 0u)
            {
                _lastAppliedFrame = selectionFrame;
                _dumpFaultPending = 1;
                counters[CounterFaults]++;
                return;
            }

            if (counters.Length <= CounterBorrowedCognitionReady || counters[CounterBorrowedCognitionReady] == 0)
                return;

            int ownedCount = math.min(math.max(0, counters[CounterOwnedSlotCount]), ownedSlots.Length);
            if (ownedCount >= ownedSlots.Length)
                return;

            if (!IsFiniteAup(in selection.SpawnAup))
                return;

            double3 currentOrigin = _cachedFloatingOriginOffset;
            if (!_floatingOriginSnapshotValid || !math.all(math.isfinite(currentOrigin)))
                return;

            float3 runtimeSpawn = selection.RuntimeSpawn;
            if (selection.OriginShiftSequence != _cachedFloatingOriginSequence)
                runtimeSpawn = ToLocalDeltaFloat3(in selection.SpawnAup, currentOrigin);
            if (!math.all(math.isfinite(runtimeSpawn)))
                return;

            int slot = PredatorCognitionDomain.Register();
            if (slot < 0)
                return;

            _lastAppliedFrame = selectionFrame;
            if ((selection.Flags & SelectionFlagLootMissing) != 0u)
                _dumpFaultPending = 2;

            int speciesId = unchecked((int)(selection.SpeciesHash & 0x7FFFFFFFu));
            PredatorCognitionDomain.ResetSlot(slot, runtimeSpawn, speciesId);
            CognitionInput input = BuildCognitionInput(in selection, runtimeSpawn, speciesId);
            PredatorCognitionDomain.SubmitInput(slot, in input);
            PredatorCognitionDomain.SetSlotActive(slot, true);

            DirectorOwnedSlotDTO owned = default;
            owned.LastAup = selection.SpawnAup;
            owned.Slot = slot;
            owned.SpeciesHash = selection.SpeciesHash;
            owned.Flags = OwnedSlotFlagActive;
            owned.LastTick = selection.Frame;
            owned.LastThreatScore = selection.ThreatScore;
            ownedSlots[ownedCount] = owned;
            counters[CounterOwnedSlotCount] = ownedCount + 1;
            counters[CounterSpawnedLastTick] = 1;
            PatchLatestTelemetryAfterApply(vault, true, slot);

            selection.SpawnSlot = slot;
            selection.RequestSpawn = 0;
            selectionArray[0] = selection;
        }

        private CognitionInput BuildCognitionInput(in DirectorSelectionDTO selection, float3 runtimeSpawn, int speciesId)
        {
            double3 origin = ToAbsoluteDouble3(in selection.SpawnAup) - new double3(runtimeSpawn.x, runtimeSpawn.y, runtimeSpawn.z);
            if (!math.all(math.isfinite(origin)))
                origin = double3.zero;
            float3 playerRuntime = runtimeSpawn + ToLocalDeltaFloat3(in selection.PlayerAup, in selection.SpawnAup);
            if (!math.all(math.isfinite(playerRuntime)))
                playerRuntime = runtimeSpawn;
            float3 forwardToPlayer = ResolveDirection(playerRuntime - runtimeSpawn, new float3(0f, 0f, 1f));
            float behaviorWeight = AuthoritativeBehaviorWeight;
            float tension = math.saturate(selection.TensionIndex);

            CognitionInput input = default;
            input.FloatingOriginOffset = origin;
            input.PlayerTargetAup = selection.PlayerAup;
            input.PackTargetAup = input.PlayerTargetAup;
            input.Position = runtimeSpawn;
            input.Forward = forwardToPlayer;
            input.PlayerPosition = playerRuntime;
            input.PlayerForward = forwardToPlayer;
            input.PreyPosition = playerRuntime;
            input.PackTargetPosition = playerRuntime;
            input.PackTargetVelocity = float3.zero;
            input.DistanceToPlayerSqr = math.lengthsq(playerRuntime - runtimeSpawn);
            input.AttackRange = math.lerp(3.5f, 9.5f, tension);
            input.HealthNormalized = 1f;
            input.FleeHealthThreshold = 0.1f;
            input.DeltaTime = 1f / 30f;
            input.MetabolicDeltaTime = 1f;
            input.CurrentTime = selection.Frame * (1f / 30f);
            input.AcousticPingStrength01 = math.saturate(tension + ResolveWeatherSeverityFromTurbidity(in selection));
            input.AcousticTransmission01 = math.saturate(1f / math.max(1f, selection.TurbidityScalar));
            input.ChemicalSignal01 = tension;
            input.ChemicalSensitivity = math.lerp(0.65f, 1.4f, behaviorWeight);
            input.HungerWeight = math.lerp(0.25f, 0.82f, tension);
            input.ThreatWeight = tension;
            input.FearWeight = 0.08f + selection.TurbidityScalar * 0.035f;
            input.CuriosityWeight = math.lerp(0.35f, 0.9f, behaviorWeight);
            input.AggressionWeight = math.saturate(selection.ThreatScore);
            input.EscapeDistance = math.lerp(18f, 42f, behaviorWeight);
            input.EscapeSafeDistance = math.lerp(32f, 72f, behaviorWeight);
            input.WanderRadius = math.lerp(12f, 44f, behaviorWeight);
            input.PatrolRadius = math.lerp(24f, 96f, behaviorWeight);
            input.FogEndDistanceMeters = math.max(12f, math.lerp(34f, 118f, behaviorWeight) / math.max(1f, selection.TurbidityScalar));
            input.BaseMaxSpeedMetersPerSecond = math.lerp(3.2f, 9.0f, math.saturate(behaviorWeight + tension * 0.35f));
            input.ImportanceScore = selection.ThreatScore;
            input.SpeciesId = speciesId;
            input.ClaimedBoidIndex = -1;
            input.FlockCount = math.max(1, (int)math.round(math.lerp(1f, 5f, math.saturate(selection.ThreatScore * 0.35f))));
            input.Flags = (int)(CognitionInputFlags.Active |
                                CognitionInputFlags.PredatorRole |
                                CognitionInputFlags.CanFlee |
                                CognitionInputFlags.HasPlayerTarget |
                                CognitionInputFlags.HasPreyTarget |
                                CognitionInputFlags.IsAggressive);
            return input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ToRuntimeVector3(in AbsoluteUniversePositionBlit128 aup, in AbsoluteUniversePositionBlit128 floatingOriginAup)
        {
            float3 runtime = ToLocalDeltaFloat3(in aup, in floatingOriginAup);
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToLocalDeltaFloat3(in AbsoluteUniversePositionBlit128 aup, double3 origin)
        {
            double3 delta = ToAbsoluteDouble3(in aup) - origin;
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ToLocalDeltaFloat3(in AbsoluteUniversePositionBlit128 aup, in AbsoluteUniversePositionBlit128 origin)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 delta = new double3(
                ((aup.GridX - origin.GridX) * cellSize) + ((double)aup.Local.x - origin.Local.x),
                ((aup.GridY - origin.GridY) * cellSize) + ((double)aup.Local.y - origin.Local.y),
                ((aup.GridZ - origin.GridZ) * cellSize) + ((double)aup.Local.z - origin.Local.z));
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static AbsoluteUniversePositionBlit128 PackAbsoluteAup(double3 absolutePosition)
        {
            if (!math.all(math.isfinite(absolutePosition)))
                return default;

            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            long gridX = (long)math.floor(absolutePosition.x * (1.0d / cellSize));
            long gridY = (long)math.floor(absolutePosition.y * (1.0d / cellSize));
            long gridZ = (long)math.floor(absolutePosition.z * (1.0d / cellSize));

            return new AbsoluteUniversePositionBlit128
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                Local = new float4(
                    (float)(absolutePosition.x - (gridX * cellSize)),
                    (float)(absolutePosition.y - (gridY * cellSize)),
                    (float)(absolutePosition.z - (gridZ * cellSize)),
                    0f),
                Reserved = 0UL
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit128 position)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                (position.GridX * cellSize) + position.Local.x,
                (position.GridY * cellSize) + position.Local.y,
                (position.GridZ * cellSize) + position.Local.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFiniteAup(in AbsoluteUniversePositionBlit128 position)
        {
            return math.all(math.isfinite(position.Local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsZeroAup(in AbsoluteUniversePositionBlit128 position)
        {
            return position.GridX == 0L &&
                   position.GridY == 0L &&
                   position.GridZ == 0L &&
                   math.all(position.Local == float4.zero);
        }

        private static bool TryGetExistingInstanceVault(out StressDrivenSpawnDirector director, out IDataVault vault)
        {
            director = _instance;
            vault = director != null ? director._vault : null;
            return director != null && vault != null;
        }

        private void TryRegisterTicks()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);

            if (GlobalRegistry.Dispatcher == null)
            {
                TryRegisterOriginShiftListener();
                return;
            }

            if (!_registeredCold)
                _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            TryRegisterOriginShiftListener();
        }

        private void TryUnregisterTicks()
        {
            TryUnregisterDispatcherTicks();

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            TryUnregisterOriginShiftListener();
        }

        private void TryUnregisterDispatcherTicks()
        {
            if (_registeredCold)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredCold = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = false;
            }
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
            RefreshFloatingOriginSnapshotCold();
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_registeredOriginShiftListener)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _registeredOriginShiftListener = false;
        }

        private void RefreshFloatingOriginSnapshotCold()
        {
            OriginShiftEventData shift = HectonFloatingOrigin.LastShiftEvent;
            double3 origin = shift.NewTotalOffsetDouble;
            uint sequence = shift.Sequence;
            uint currentSequence = HectonFloatingOrigin.CurrentShiftSequence;
            if (sequence == 0u || !math.all(math.isfinite(origin)))
            {
                origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                sequence = currentSequence;
            }
            else if (currentSequence != 0u && currentSequence != sequence)
            {
                origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                sequence = currentSequence;
            }

            if (!math.all(math.isfinite(origin)))
            {
                origin = double3.zero;
                _floatingOriginSnapshotValid = false;
            }
            else
            {
                _floatingOriginSnapshotValid = true;
            }

            _cachedFloatingOriginOffset = origin;
            _cachedFloatingOriginSequence = sequence;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseOwnedVaultHandle(vault, ref _rulesHandle, RulesBufferId);
            ReleaseOwnedVaultHandle(vault, ref _ruleLinksHandle, RuleLinksBufferId);
            ReleaseOwnedVaultHandle(vault, ref _candidatesHandle, CandidatesBufferId);
            ReleaseOwnedVaultHandle(vault, ref _selectionHandle, SelectionBufferId);
            ReleaseOwnedVaultHandle(vault, ref _inputHandle, InputBufferId);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle, TuningBufferId);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle, TelemetryBufferId);
            ReleaseOwnedVaultHandle(vault, ref _countersHandle, CountersBufferId);
#if UNITY_EDITOR
            ReleaseOwnedVaultHandle(vault, ref _csvScratchHandle, CsvScratchBufferId);
#endif
            ReleaseOwnedVaultHandle(vault, ref _frustumPlanesHandle, FrustumPlanesBufferId);
            ReleaseOwnedVaultHandle(vault, ref _ownedSlotsHandle, OwnedSlotsBufferId);
            ReleaseOwnedVaultHandle(vault, ref _inventoryTicketsHandle, InventoryTicketsBufferId);
            ReleaseOwnedVaultHandle(vault, ref _spawnDebugHandle, SpawnDebugBufferId);
        }

        private static void ReleaseOwnedVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            if (IsOwnedVaultHandle(in handle, expectedBufferId, SystemID.AIEcology))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearHandlesCold()
        {
            _rulesHandle = default;
            _ruleLinksHandle = default;
            _candidatesHandle = default;
            _selectionHandle = default;
            _inputHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _countersHandle = default;
#if UNITY_EDITOR
            _csvScratchHandle = default;
#endif
            _frustumPlanesHandle = default;
            _ownedSlotsHandle = default;
            _inventoryTicketsHandle = default;
            _spawnDebugHandle = default;
            _mesofaunaStatesHandle = default;
            _mesofaunaTargetsHandle = default;
            _mesofaunaVisualHandle = default;
            _cognitionInputsHandle = default;
            _weatherStateHandle = default;
            _scalabilityStateHandle = default;
            _macroSectorSnapshotHandle = default;
            _macroSectorIndexHandle = default;
            _macroTuningHandle = default;
        }

        // Runtime helper, deliberately OUTSIDE the editor CSV block below.
        // InitializeDefaultRules() hashes the four built-in species/loot keys with this, and that
        // default table is the ONLY spawn-rule source a player build has - the CSV loader is
        // editor-only. Guarding this with the CSV loader stripped the default table from the player.
        private static uint HashLower(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    char ch = text[i];
                    byte b = (byte)(ch >= 'A' && ch <= 'Z' ? ch + 32 : ch);
                    hash = (hash ^ b) * 16777619u;
                }
                return hash == 0u ? 1u : hash;
            }
        }

#if UNITY_EDITOR
        private bool TryLoadRulesCsvCold(IDataVault vault, bool forceReload, bool locksHeld)
        {
            if (!TryResolve(vault, in _countersHandle, CountersBufferId, out NativeArray<int> counters))
                return false;

            if (counters.Length <= CounterCsvLoaded)
                return false;

            if (!forceReload && counters[CounterCsvLoadAttempted] != 0)
                return counters[CounterCsvLoaded] != 0;

            if (!locksHeld)
            {
                if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                    return false;

                if (!TryPinReloadBuffers(vault))
                {
                    return false;
                }

                try
                {
                    return TryLoadRulesCsvCold(vault, forceReload, locksHeld: true);
                }
                finally
                {
                    vault.ReleaseMutationGuard(ReloadMutationGuardMask);
                }
            }

            if (!TryResolve(vault, in _csvScratchHandle, CsvScratchBufferId, out NativeArray<byte> scratch) ||
                !TryResolve(vault, in _rulesHandle, RulesBufferId, out NativeArray<SpawnRuleDTO> rules) ||
                !TryResolve(vault, in _ruleLinksHandle, RuleLinksBufferId, out NativeArray<SpawnRuleLinkDTO> links) ||
                !TryResolve(vault, in _countersHandle, CountersBufferId, out counters))
            {
                return false;
            }

            if (!forceReload)
                counters[CounterCsvLoadAttempted] = 1;

            string path = ResolveRulesPathCold();
            if (string.IsNullOrEmpty(path))
                return false;

            int byteCount = ReadFileIntoScratchCold(path, scratch);
            if (byteCount <= 0)
                return false;

#if UNITY_EDITOR
            void* pointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            int parsed = ParseSpawnRulesCsv(new ReadOnlySpan<byte>(pointer, byteCount), rules, links);
            if (parsed <= 0)
                return false;

            counters[CounterRuleCount] = parsed;
            counters[CounterCsvLoadAttempted] = 1;
            counters[CounterCsvLoaded] = 1;
            return true;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        public static int ParseSpawnRulesCsv(ReadOnlySpan<byte> csv, NativeArray<SpawnRuleDTO> rules, NativeArray<SpawnRuleLinkDTO> links)
        {
            if (!rules.IsCreated || !links.IsCreated || csv.Length <= 0)
                return 0;

            int capacity = math.min(rules.Length, links.Length);
            if (CountSpawnRulesCsv(csv, capacity) <= 0)
                return 0;

            for (int i = 0; i < capacity; i++)
            {
                rules[i] = default;
                links[i] = default;
            }

            int count = 0;
            int lineStart = 0;
            while (lineStart < csv.Length && count < capacity)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n' && csv[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = Trim(csv.Slice(lineStart, lineEnd - lineStart));
                if (TryParseRuleLine(line, out SpawnRuleDTO rule, out SpawnRuleLinkDTO link))
                {
                    rules[count] = rule;
                    links[count] = link;
                    count++;
                }

                lineStart = lineEnd + 1;
                while (lineStart < csv.Length && (csv[lineStart] == (byte)'\n' || csv[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return count;
        }

        private static int CountSpawnRulesCsv(ReadOnlySpan<byte> csv, int capacity)
        {
            if (csv.Length <= 0 || capacity <= 0)
                return 0;

            int count = 0;
            int lineStart = 0;
            while (lineStart < csv.Length && count < capacity)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n' && csv[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = Trim(csv.Slice(lineStart, lineEnd - lineStart));
                if (TryParseRuleLine(line, out SpawnRuleDTO _, out SpawnRuleLinkDTO _))
                    count++;

                lineStart = lineEnd + 1;
                while (lineStart < csv.Length && (csv[lineStart] == (byte)'\n' || csv[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return count;
        }

        private static bool TryParseRuleLine(ReadOnlySpan<byte> line, out SpawnRuleDTO rule, out SpawnRuleLinkDTO link)
        {
            rule = default;
            link = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            if (!TryReadField(line, ref cursor, out ReadOnlySpan<byte> speciesToken) ||
                !TryParseHash(speciesToken, out uint speciesHash) ||
                speciesHash == 0u)
            {
                return false;
            }

            if (IsHeader(speciesToken))
                return false;

            if (!TryReadFloatField(line, ref cursor, out float minTension) ||
                !TryReadFloatField(line, ref cursor, out float maxTension) ||
                !TryReadFloatField(line, ref cursor, out float cpuCost))
            {
                return false;
            }

            uint biomeMask = 0xFFFFFFFFu;
            uint lootHash = HashLower("loot_default_predator");
            float threatWeight = 1f;
            float swarmBias = 1f;
            if (TryReadField(line, ref cursor, out ReadOnlySpan<byte> biomeToken))
                TryParseHashOrAll(biomeToken, out biomeMask);
            if (TryReadField(line, ref cursor, out ReadOnlySpan<byte> lootToken))
                TryParseHash(lootToken, out lootHash);
            if (TryReadFloatField(line, ref cursor, out float parsedThreat))
                threatWeight = parsedThreat;
            if (TryReadFloatField(line, ref cursor, out float parsedSwarm))
                swarmBias = parsedSwarm;

            rule.SpeciesHash = speciesHash;
            rule.MinTension = math.saturate(minTension);
            rule.MaxTension = math.max(rule.MinTension, math.saturate(maxTension));
            rule.CPUCostScalar = math.clamp(cpuCost, 0.01f, 32f);
            rule.RequiredBiomeMask = biomeMask == 0u ? 0xFFFFFFFFu : biomeMask;

            link.SpeciesHash = speciesHash;
            link.LootTableHash = lootHash == 0u ? HashLower("loot_default_predator") : lootHash;
            link.ThreatWeight = math.clamp(threatWeight, 0.01f, 8f);
            link.SwarmCountBias = math.clamp(swarmBias, 0f, 16f);
            return true;
        }

        private static bool IsHeader(ReadOnlySpan<byte> token)
        {
            token = Trim(token);
            return token.Length == 7 &&
                   ToLower(token[0]) == (byte)'s' &&
                   ToLower(token[1]) == (byte)'p' &&
                   ToLower(token[2]) == (byte)'e' &&
                   ToLower(token[3]) == (byte)'c' &&
                   ToLower(token[4]) == (byte)'i' &&
                   ToLower(token[5]) == (byte)'e' &&
                   ToLower(token[6]) == (byte)'s';
        }

        private static bool TryReadFloatField(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            return TryReadField(line, ref cursor, out ReadOnlySpan<byte> token) && TryParseFloat(token, out value);
        }

        private static bool TryReadField(ReadOnlySpan<byte> line, ref int cursor, out ReadOnlySpan<byte> token)
        {
            token = default;
            while (cursor < line.Length && (line[cursor] == (byte)',' || line[cursor] == (byte)';' || IsWhitespace(line[cursor])))
                cursor++;
            if (cursor >= line.Length)
                return false;

            int start = cursor;
            bool quoted = line[cursor] == (byte)'"';
            if (quoted)
                cursor++;
            while (cursor < line.Length)
            {
                byte b = line[cursor];
                if (quoted)
                {
                    if (b == (byte)'"')
                    {
                        cursor++;
                        break;
                    }
                }
                else if (b == (byte)',' || b == (byte)';')
                {
                    break;
                }
                cursor++;
            }

            int end = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',' && line[cursor] != (byte)';')
                cursor++;
            if (cursor < line.Length)
                cursor++;

            token = Trim(line.Slice(start, end - start));
            return token.Length > 0;
        }

        private static bool TryParseHashOrAll(ReadOnlySpan<byte> token, out uint value)
        {
            token = Trim(token);
            if (token.Length == 3 &&
                ToLower(token[0]) == (byte)'a' &&
                ToLower(token[1]) == (byte)'l' &&
                ToLower(token[2]) == (byte)'l')
            {
                value = 0xFFFFFFFFu;
                return true;
            }

            return TryParseHash(token, out value);
        }

        private static bool TryParseHash(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            token = Trim(token);
            if (token.Length <= 0)
                return false;

            if (token.Length > 2 && token[0] == (byte)'0' && (token[1] == (byte)'x' || token[1] == (byte)'X'))
                return TryParseHex(token.Slice(2), out value);

            if (TryParseUint(token, out value))
                return true;

            value = Fnv1aLower(token);
            return value != 0u;
        }

        private static bool TryParseUint(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                    return false;
                uint next = (value * 10u) + (uint)(b - (byte)'0');
                value = next < value ? uint.MaxValue : next;
            }
            return token.Length > 0;
        }

        private static bool TryParseHex(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9') digit = (uint)(b - (byte)'0');
                else if (b >= (byte)'a' && b <= (byte)'f') digit = (uint)(10 + b - (byte)'a');
                else if (b >= (byte)'A' && b <= (byte)'F') digit = (uint)(10 + b - (byte)'A');
                else return false;
                value = (value << 4) | digit;
            }
            return token.Length > 0;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            token = Trim(token);
            if (token.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (token[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (token[index] == (byte)'+')
            {
                index++;
            }

            double result = 0d;
            bool hasDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                result = (result * 10d) + (token[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigit = true;
                }
            }

            value = (float)(result * sign);
            return hasDigit && math.isfinite(value);
        }
#endif

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> token)
        {
            int start = 0;
            int end = token.Length - 1;
            while (start <= end && IsWhitespace(token[start]))
                start++;
            while (end >= start && IsWhitespace(token[end]))
                end--;
            if (start <= end && token[start] == (byte)'"' && token[end] == (byte)'"')
            {
                start++;
                end--;
            }
            return start <= end ? token.Slice(start, end - start + 1) : default;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static uint Fnv1aLower(ReadOnlySpan<byte> token)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < token.Length; i++)
                {
                    hash = (hash ^ ToLower(token[i])) * 16777619u;
                }
                return hash == 0u ? 1u : hash;
            }
        }

        private static int ReadFileIntoScratchCold(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || string.IsNullOrEmpty(path))
                return 0;

            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length <= 0L || stream.Length > scratch.Length)
                        return 0;
                    int length = (int)stream.Length;
                    void* destination = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                    Span<byte> target = new Span<byte>(destination, length);
                    int total = 0;
                    while (total < length)
                    {
                        int read = stream.Read(target.Slice(total));
                        if (read <= 0)
                            break;
                        total += read;
                    }
                    return total;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
        }

        private static string ResolveRulesPathCold()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            string projectRoot = dataDirectory != null ? dataDirectory.FullName : Application.dataPath;
            string path = Path.Combine(projectRoot, "Assets", "_SourceData", "Fauna", RulesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Data", "AI", RulesCsvName);
            if (File.Exists(path))
                return path;

            path = Path.Combine(projectRoot, "Data", "Balance", RulesCsvName);
            return File.Exists(path) ? path : null;
        }
#endif

        private bool ResolveDataMonolithReadyCold()
        {
            if (H8StaticDataArena.IsLoaded &&
                H8StaticDataArena.TryGetSectionSpan(H8DataSectionId.LootCdf, out ReadOnlySpan<H8LootCdfRecord> lootRecords) &&
                lootRecords.Length > 0 &&
                lootRecords[0].TableHash != 0u)
            {
                return true;
            }

            return false;
        }

        private unsafe bool TryStageBlackBoxDumpCold(
            IDataVault vault,
            uint reasonHash,
            out string path,
            out NativeArray<byte> payload,
            out int byteCount)
        {
            path = null;
            payload = default;
            byteCount = 0;
            if (!TryResolve(vault, in _telemetryHandle, TelemetryBufferId, out NativeArray<DirectorTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
            {
                return false;
            }

            try
            {
                path = DumpPath;
                int rowSize = UnsafeUtility.SizeOf<DirectorTelemetryEntry>();
                int rowCount = telemetry.Length;
                byteCount = 16 + rowCount * rowSize;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(StressDrivenSpawnDirector),
                    "DirectorTelemetryDumpPayload",
                    NativeArrayOptions.ClearMemory);
                int offset = 0;

                WriteUInt32LittleEndian(payload, ref offset, SourceHash);
                WriteUInt32LittleEndian(payload, ref offset, reasonHash);
                WriteInt32LittleEndian(payload, ref offset, TelemetryCapacity);
                WriteInt32LittleEndian(payload, ref offset, rowSize);

                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                void* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload) + offset;
                UnsafeUtility.MemCpy(destination, source, rowCount * rowSize);
                return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }

            if (payload.IsCreated)
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(StressDrivenSpawnDirector),
                    "DirectorTelemetryDumpPayload");
                payload = default;
            }

            path = null;
            byteCount = 0;
            return false;
        }

        private static bool TrySubmitBlackBoxDump(string path, NativeArray<byte> payload, int byteCount)
        {
            try
            {
                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static float ResolveFrameTimeMs(float fallback)
        {
            float unscaled = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            if (math.isfinite(unscaled) && unscaled > 0f)
                return unscaled * 1000f;
            return math.max(0f, math.select(16.6667f, fallback, math.isfinite(fallback)));
        }

        private static uint ResolveFrameId(uint fallbackTick)
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame == 0u ? fallbackTick : frame;
        }

        private static uint ResolveWorldSeed(uint sectorHash)
        {
            return Hash3(SourceHash, sectorHash == 0u ? 1u : sectorHash, 0x574F524Cu);
        }

        private static uint ResolveSectorHash32(double3 aup)
        {
            ulong hash = ResolveSectorHash64(aup);
            return (uint)(hash ^ (hash >> 32));
        }

        private static uint ResolveSectorHash32(in AbsoluteUniversePositionBlit128 aup)
        {
            ulong hash = ResolveSectorHash64(in aup);
            return (uint)(hash ^ (hash >> 32));
        }

        private static ulong ResolveSectorHash64(double3 aup)
        {
            double invSector = 1.0 / math.max(1.0, MacroEcosystemVaultContract.SectorSizeMeters);
            long sectorX = (long)math.floor(math.select(0.0, aup.x * invSector, math.isfinite(aup.x)));
            long sectorZ = (long)math.floor(math.select(0.0, aup.z * invSector, math.isfinite(aup.z)));
            return MacroEcosystemVaultContract.ComputeSectorHash(sectorX, 0L, sectorZ);
        }

        private static ulong ResolveSectorHash64(in AbsoluteUniversePositionBlit128 aup)
        {
            return ResolveSectorHash64(ToAbsoluteDouble3(in aup));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, ref int offset, int value)
        {
            WriteUInt32LittleEndian(payload, ref offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, ref int offset, uint value)
        {
            payload[offset++] = (byte)value;
            payload[offset++] = (byte)(value >> 8);
            payload[offset++] = (byte)(value >> 16);
            payload[offset++] = (byte)(value >> 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash3(uint a, uint b, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ a) * 16777619u;
                hash = (hash ^ b) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return hash == 0u ? SourceHash : hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveWeatherSeverityFromTurbidity(in DirectorSelectionDTO selection)
        {
            return math.saturate((math.max(1f, selection.TurbidityScalar) - 1f) * 0.3125f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveDirection(float3 direction, float3 fallback)
        {
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.0001f)
                direction = fallback;
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.0001f)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(math.lengthsq(direction), 0.0001f));
        }

        public static bool ValidateLayout()
        {
            return UnsafeUtility.SizeOf<SpawnRuleDTO>() == 32 &&
                   Marshal.OffsetOf(typeof(SpawnRuleDTO), nameof(SpawnRuleDTO.SpeciesHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf(typeof(SpawnRuleDTO), nameof(SpawnRuleDTO.MinTension)).ToInt32() == 4 &&
                   Marshal.OffsetOf(typeof(SpawnRuleDTO), nameof(SpawnRuleDTO.MaxTension)).ToInt32() == 8 &&
                   Marshal.OffsetOf(typeof(SpawnRuleDTO), nameof(SpawnRuleDTO.CPUCostScalar)).ToInt32() == 12 &&
                   Marshal.OffsetOf(typeof(SpawnRuleDTO), nameof(SpawnRuleDTO.RequiredBiomeMask)).ToInt32() == 16 &&
                   UnsafeUtility.SizeOf<DirectorInputDTO>() == 208 &&
                   Marshal.OffsetOf(typeof(DirectorInputDTO), nameof(DirectorInputDTO.PlayerAup)).ToInt32() == 0 &&
                   Marshal.OffsetOf(typeof(DirectorInputDTO), nameof(DirectorInputDTO.FloatingOriginAup)).ToInt32() == 48 &&
                   Marshal.OffsetOf(typeof(DirectorInputDTO), nameof(DirectorInputDTO.SectorHash)).ToInt32() == 172 &&
                   Marshal.OffsetOf(typeof(DirectorInputDTO), nameof(DirectorInputDTO.PreyBiomass01)).ToInt32() == 176 &&
                   Marshal.OffsetOf(typeof(DirectorInputDTO), nameof(DirectorInputDTO.OriginShiftSequence)).ToInt32() == 204 &&
                   UnsafeUtility.SizeOf<DirectorCandidateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<DirectorTelemetryEntry>() == 192 &&
                   Marshal.OffsetOf(typeof(DirectorTelemetryEntry), nameof(DirectorTelemetryEntry.PlayerAup)).ToInt32() == 40 &&
                   Marshal.OffsetOf(typeof(DirectorTelemetryEntry), nameof(DirectorTelemetryEntry.LastSpawnAup)).ToInt32() == 88 &&
                   Marshal.OffsetOf(typeof(DirectorTelemetryEntry), nameof(DirectorTelemetryEntry.PreyBiomass01)).ToInt32() == 144 &&
                   Marshal.OffsetOf(typeof(DirectorTelemetryEntry), nameof(DirectorTelemetryEntry.OriginShiftSequence)).ToInt32() == 172 &&
                   UnsafeUtility.SizeOf<DirectorSpawnDebugDTO>() == 128 &&
                   Marshal.OffsetOf(typeof(DirectorSpawnDebugDTO), nameof(DirectorSpawnDebugDTO.SpawnAup)).ToInt32() == 0 &&
                   UnsafeUtility.SizeOf<DirectorSelectionDTO>() == 192 &&
                   Marshal.OffsetOf(typeof(DirectorSelectionDTO), nameof(DirectorSelectionDTO.PlayerAup)).ToInt32() == 48 &&
                   Marshal.OffsetOf(typeof(DirectorSelectionDTO), nameof(DirectorSelectionDTO.OriginShiftSequence)).ToInt32() == 176 &&
                   UnsafeUtility.SizeOf<DirectorOwnedSlotDTO>() == 80;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockTensionJob : IJob
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DirectorInputDTO> Inputs;
        [ReadOnly, NoAlias] public NativeArray<DirectorTuningDTO> Tuning;
        public uint Frame;
        public uint WorldSeed;

        public void Execute()
        {
            if (!Inputs.IsCreated || Inputs.Length <= 0 || !Tuning.IsCreated || Tuning.Length <= 0)
                return;

            DirectorInputDTO input = Inputs[0];
            DirectorTuningDTO tuning = Tuning[0];
            input.GlobalQualityWeight = math.saturate(math.select(1f, input.GlobalQualityWeight, math.isfinite(input.GlobalQualityWeight)));
            input.TurbidityScalar = math.max(1f, math.select(1f, input.TurbidityScalar, math.isfinite(input.TurbidityScalar)));
            input.TensionIndex = math.saturate(math.select(0f, input.TensionIndex, math.isfinite(input.TensionIndex)));

            if ((tuning.Flags & StressDrivenSpawnDirector.TuningFlagEmergencyMock) != 0u &&
                (input.Flags & StressDrivenSpawnDirector.InputFlagExternalStress) == 0u)
            {
                uint hash = Hash(Frame, WorldSeed, 0x54454E53u);
                uint jitterHash = Hash(input.SectorHash, WorldSeed, Frame);
                float wave = Triangle01((Frame & 2047u) * (1f / 2047f));
                float weatherPulse = Triangle01(((Frame + (hash & 255u)) & 1023u) * (1f / 1023f));
                float jitter = ((jitterHash >> 8) & 65535u) * (1f / 65535f) * 0.1f;
                input.TensionIndex = math.saturate(math.lerp(0.18f, 0.92f, wave) + jitter);
                input.WeatherSeverity01 = math.max(input.WeatherSeverity01, weatherPulse * 0.65f);
                input.TurbidityScalar = math.max(input.TurbidityScalar, 1f + input.WeatherSeverity01 * 1.85f);
                input.CurrentBiomeMask = input.CurrentBiomeMask == 0u ? 0xFFFFFFFFu : input.CurrentBiomeMask;
            }

            if (!math.all(math.isfinite(input.PlayerForward)) || math.lengthsq(input.PlayerForward) <= 0.0001f)
                input.PlayerForward = new float3(0f, 0f, 1f);
            else
                input.PlayerForward = input.PlayerForward * math.rsqrt(math.max(math.lengthsq(input.PlayerForward), 0.0001f));

            Inputs[0] = input;
        }

        private static float Triangle01(float value)
        {
            float x = math.frac(value);
            return 1f - math.abs((x * 2f) - 1f);
        }

        private static uint Hash(uint a, uint b, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ a) * 16777619u;
                hash = (hash ^ b) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return hash == 0u ? 1u : hash;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct EvaluateSpawnConditionsJob : IJob
    {
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public SpawnRuleDTO* Rules;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public SpawnRuleLinkDTO* Links;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorCandidateDTO* Candidates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTuningDTO* Tuning;
        public int RuleCount;
        public int CandidateCapacity;

        public void Execute()
        {
            if (Rules == null || Links == null || Candidates == null || Counters == null || Inputs == null || Tuning == null)
                return;

            for (int i = 0; i < CandidateCapacity; i++)
                Candidates[i] = default;

            DirectorInputDTO input = UnsafeUtility.AsRef<DirectorInputDTO>(Inputs);
            DirectorTuningDTO tuning = StressDrivenSpawnDirectorSanitizer.Sanitize(UnsafeUtility.AsRef<DirectorTuningDTO>(Tuning), input.GlobalQualityWeight);
            float quality = Smooth01(input.GlobalQualityWeight);
            int count = 0;

            if ((input.Flags & StressDrivenSpawnDirector.InputFlagOriginInvalid) != 0u)
            {
                Counters[1] = 0;
                return;
            }

            if (input.BiomeTransitionTicksRemaining > 0)
            {
                Counters[1] = 0;
                return;
            }

            float tension = math.saturate(input.TensionIndex);
            float weather = math.saturate(input.WeatherSeverity01);
            float preyBiomass = math.saturate(input.PreyBiomass01);
            float predatorBiomass = math.saturate(input.PredatorBiomass01);
            float carryingCapacity = math.saturate(math.max(0.15f, input.CarryingCapacity01));
            float toxin = math.saturate(input.ToxinLevel01);
            float ecosystemFit = math.saturate(
                math.lerp(0.38f, 1.32f, preyBiomass) *
                math.lerp(1.12f, 0.55f, predatorBiomass) *
                math.lerp(0.72f, 1.18f, carryingCapacity) *
                math.lerp(1f, 0.68f, toxin));
            int rules = math.min(math.max(0, RuleCount), math.min((int)tuning.MaxCandidateRules, CandidateCapacity));
            for (int i = 0; i < rules && count < CandidateCapacity; i++)
            {
                ref SpawnRuleDTO rule = ref UnsafeUtility.AsRef<SpawnRuleDTO>(Rules + i);
                if (rule.SpeciesHash == 0u)
                    continue;

                bool biomeMatch = rule.RequiredBiomeMask == 0u ||
                                  rule.RequiredBiomeMask == 0xFFFFFFFFu ||
                                  (rule.RequiredBiomeMask & input.CurrentBiomeMask) != 0u;
                if (!biomeMatch)
                    continue;

                float minTension = math.saturate(rule.MinTension);
                float maxTension = math.max(minTension, math.saturate(rule.MaxTension));
                float inRange = math.saturate((tension - minTension) * math.rcp(math.max(0.001f, maxTension - minTension)));
                if (inRange <= 0f && tension < minTension)
                    continue;

                ref SpawnRuleLinkDTO link = ref UnsafeUtility.AsRef<SpawnRuleLinkDTO>(Links + i);
                float threatWeight = math.max(0.01f, link.ThreatWeight);
                float weatherBoost = math.lerp(0.85f, 1.25f, weather);
                float cpuCost = math.max(0.01f, rule.CPUCostScalar);
                float score = inRange * threatWeight * weatherBoost * ecosystemFit *
                              math.lerp(0.8f, 1.2f, quality);

                DirectorCandidateDTO candidate = default;
                candidate.SpeciesHash = rule.SpeciesHash;
                candidate.LootTableHash = link.LootTableHash;
                candidate.RequiredBiomeMask = rule.RequiredBiomeMask;
                candidate.Score = score;
                candidate.CPUCostScalar = cpuCost;
                candidate.ThreatWeight = threatWeight;
                candidate.SwarmCountBias = link.SwarmCountBias;
                candidate.RuleIndex = i;
                candidate.CandidateHash = Hash(rule.SpeciesHash, input.SimulationTick, (uint)i);
                candidate.MinTension = minTension;
                candidate.MaxTension = maxTension;
                candidate.SpawnProbability01 = math.saturate(score * math.rcp(math.max(0.05f, cpuCost)));
                candidate.SectorHash = input.SectorHash;
                candidate.MacroEcosystemStateHash = input.MacroEcosystemStateHash;
                Candidates[count++] = candidate;
            }

            Counters[1] = count;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static uint Hash(uint speciesHash, uint tick, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ speciesHash) * 16777619u;
                hash = (hash ^ tick) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? speciesHash : hash;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AllocateThreatBudgetJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorCandidateDTO* Candidates;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorSelectionDTO* Selection;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTuningDTO* Tuning;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
        public int CandidateCapacity;
        public int MonolithReady;

        public void Execute()
        {
            if (Candidates == null || Selection == null || Inputs == null || Tuning == null || Counters == null)
                return;

            DirectorInputDTO input = UnsafeUtility.AsRef<DirectorInputDTO>(Inputs);
            DirectorTuningDTO tuning = StressDrivenSpawnDirectorSanitizer.Sanitize(UnsafeUtility.AsRef<DirectorTuningDTO>(Tuning), input.GlobalQualityWeight);
            DirectorSelectionDTO selection = default;
            selection.PlayerAup = input.PlayerAup;
            selection.GlobalQualityWeight = math.saturate(input.GlobalQualityWeight);
            selection.TensionIndex = math.saturate(input.TensionIndex);
            selection.TurbidityScalar = math.max(1f, input.TurbidityScalar);
            selection.Frame = input.Frame;
            selection.BiomeMask = input.CurrentBiomeMask;
            selection.SuppressTicksRemaining = input.BiomeTransitionTicksRemaining;
            selection.SectorHash = input.SectorHash;
            selection.OriginShiftSequence = input.OriginShiftSequence;

            int candidateCount = math.min(math.max(0, Counters[1]), CandidateCapacity);
            if (candidateCount <= 0 || input.BiomeTransitionTicksRemaining > 0 || input.SpawnCooldownSeconds > 0f)
            {
                selection.Flags |= input.BiomeTransitionTicksRemaining > 0 ? StressDrivenSpawnDirector.SelectionFlagBiomeSuppressed : 0u;
                Selection[0] = selection;
                return;
            }

            float quality = Smooth01(input.GlobalQualityWeight);
            float thermal = math.saturate(input.ThermalPressure01);
            float budget = math.lerp(tuning.BudgetLow, tuning.BudgetUltra, quality) * math.lerp(1f, 0.35f, thermal);
            selection.Budget = budget;

            int bestIndex = -1;
            float bestScore = -1f;
            for (int i = 0; i < candidateCount; i++)
            {
                DirectorCandidateDTO candidate = Candidates[i];
                float cost = math.max(0.01f, candidate.CPUCostScalar);
                float budgetFit = math.saturate(budget * math.rcp(cost));
                float lowBudgetApexBias = math.saturate((cost - budget) * 0.5f) * math.saturate(input.TensionIndex);
                float score = candidate.Score * (0.35f + budgetFit) + lowBudgetApexBias;
                candidate.BudgetFit = budgetFit;
                Candidates[i] = candidate;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                Selection[0] = selection;
                return;
            }

            DirectorCandidateDTO best = Candidates[bestIndex];
            selection.SpeciesHash = best.SpeciesHash;
            selection.LootTableHash = best.LootTableHash;
            selection.CandidateIndex = bestIndex;
            selection.ThreatScore = math.saturate(best.Score);
            selection.StateHash = Hash(best.SpeciesHash, input.SimulationTick, best.CandidateHash);
            float spawnRatePerColdTick = math.saturate(tuning.BaseSpawnRatePerMinute * (1f / 60f));
            float qualityBias = math.lerp(0.55f, 1.25f, quality);
            float tensionBias = Hecton8.PureLogic.Ecosystem.StressSpawnEscalationCalculator.Compute(math.saturate(input.TensionIndex), 0.45f, 1.2f, 1.65f);
            float probability = math.saturate(best.SpawnProbability01 * spawnRatePerColdTick * qualityBias * tensionBias * math.max(1f, tuning.MaxSpawnPerColdTick));
            if (Roll01(Hash(best.SpeciesHash, input.SimulationTick, input.WorldSeed ^ input.SectorHash)) > probability)
            {
                Selection[0] = selection;
                return;
            }

            selection.RequestSpawn = 1;
            if (MonolithReady == 0 || best.LootTableHash == 0u)
                selection.Flags |= StressDrivenSpawnDirector.SelectionFlagLootMissing;
            Selection[0] = selection;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static uint Hash(uint species, uint tick, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ species) * 16777619u;
                hash = (hash ^ tick) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? 0x53323533u : hash;
            }
        }

        private static float Roll01(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CalculateHiddenSpawnAupJob : IJob
    {
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public float4* FrustumPlanes;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorSelectionDTO* Selection;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTuningDTO* Tuning;
        public int PlaneCount;
        public int ProbeCapacity;

        public void Execute()
        {
            if (FrustumPlanes == null || Selection == null || Inputs == null || Tuning == null)
                return;

            DirectorSelectionDTO selection = Selection[0];
            if (selection.RequestSpawn == 0)
            {
                Selection[0] = selection;
                return;
            }

            DirectorInputDTO input = UnsafeUtility.AsRef<DirectorInputDTO>(Inputs);
            DirectorTuningDTO tuning = StressDrivenSpawnDirectorSanitizer.Sanitize(UnsafeUtility.AsRef<DirectorTuningDTO>(Tuning), input.GlobalQualityWeight);
            if ((tuning.Flags & StressDrivenSpawnDirector.TuningFlagEnableHiddenInjection) == 0u)
            {
                selection.RequestSpawn = 0;
                Selection[0] = selection;
                return;
            }

            float quality = Smooth01(input.GlobalQualityWeight);
            float turbidity = math.max(1f, input.TurbidityScalar);
            float minRadius = tuning.MinHiddenRadiusMeters;
            float maxRadius = math.lerp(tuning.MaxHiddenRadiusMeters * 0.72f, tuning.MaxHiddenRadiusMeters, quality);
            float fogHide = math.saturate((turbidity - 1f) * 0.45f);
            maxRadius = math.lerp(maxRadius, minRadius * 1.35f, fogHide);
            uint seed = Hash(selection.SpeciesHash, input.SimulationTick, selection.StateHash);
            float3 forward = ResolveDirection(input.PlayerForward, new float3(0f, 0f, 1f));
            float3 right = ResolveDirection(new float3(forward.z, 0f, -forward.x), new float3(1f, 0f, 0f));
            float3 up = ResolveDirection(math.cross(right, forward), new float3(0f, 1f, 0f));
            int maxProbes = math.min(math.max(1, (int)tuning.MaxHiddenProbes), ProbeCapacity);
            int probes = math.clamp((int)math.round(math.lerp(3f, maxProbes, quality)), 1, maxProbes);
            bool found = false;
            AbsoluteUniversePositionBlit128 bestAup = input.PlayerAup;
            float bestRadius = minRadius;

            for (int probe = 0; probe < probes; probe++)
            {
                float t = (probe + 0.5f) * math.rcp(math.max(1f, probes));
                float radius = math.lerp(minRadius, maxRadius, t);
                float angle = ((seed & 4095u) * 0.0015339808f) + probe * 2.39996323f;
                float vertical = math.lerp(-0.28f, 0.22f, math.frac(t * 5.0f + (seed & 127u) * 0.0078125f));
                float rearBias = math.lerp(0.55f, 0.92f, math.saturate(input.TensionIndex + fogHide * 0.35f));
                MathLodApproximation.ApproxSinCosBhaskara(angle, out _, out float angleCos);
                float3 direction = ResolveDirection(
                    (-forward * rearBias) +
                    (right * angleCos * (1f - rearBias)) +
                    (up * vertical),
                    -forward);
                float3 offset = direction * radius;
                if (!IsOutsideFrustum(offset, tuning.FrustumPlaneMarginMeters))
                    continue;
                if (!PassesCheapSdf(offset, tuning.MinSdfClearanceMeters, seed + (uint)probe))
                    continue;

                bestAup = AddMeters(in input.PlayerAup, offset);
                bestRadius = radius;
                found = true;
                break;
            }

            if (!found || !IsFiniteAup(in bestAup))
            {
                selection.RequestSpawn = 0;
                selection.Flags |= StressDrivenSpawnDirector.SelectionFlagFrustumRejected;
                Selection[0] = selection;
                return;
            }

            selection.SpawnAup = bestAup;
            selection.SpawnRadiusMeters = bestRadius;
            selection.OriginShiftSequence = input.OriginShiftSequence;
            selection.RuntimeSpawn = ToLocalDeltaFloat3(in bestAup, in input.FloatingOriginAup);
            selection.Flags |= StressDrivenSpawnDirector.SelectionFlagSpawnHidden;
            if (!math.all(math.isfinite(selection.RuntimeSpawn)))
            {
                selection.RequestSpawn = 0;
                selection.Flags |= StressDrivenSpawnDirector.SelectionFlagFault;
            }
            Selection[0] = selection;
        }

        private bool IsOutsideFrustum(float3 offset, float margin)
        {
            if (FrustumPlanes == null || PlaneCount <= 0)
                return math.lengthsq(offset) > 1f;

            for (int i = 0; i < PlaneCount; i++)
            {
                float4 plane = FrustumPlanes[i];
                float d = math.dot(plane.xyz, offset) + plane.w;
                if (d < -margin)
                    return true;
            }

            return false;
        }

        private static bool PassesCheapSdf(float3 offset, float clearance, uint seed)
        {
            float r = math.length(offset.xz);
            float fakeCaveWall = 18f + ((seed & 31u) * 0.75f);
            float ceiling = 64f + ((seed >> 5) & 31u);
            float signedDistance = math.min(r - fakeCaveWall, ceiling - math.abs(offset.y));
            return signedDistance >= math.max(0f, clearance);
        }

        private static AbsoluteUniversePositionBlit128 AddMeters(in AbsoluteUniversePositionBlit128 origin, float3 offset)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 absolute = new double3(
                (origin.GridX * cellSize) + origin.Local.x + offset.x,
                (origin.GridY * cellSize) + origin.Local.y + offset.y,
                (origin.GridZ * cellSize) + origin.Local.z + offset.z);
            if (!math.all(math.isfinite(absolute)))
                return default;

            long gridX = (long)math.floor(absolute.x / cellSize);
            long gridY = (long)math.floor(absolute.y / cellSize);
            long gridZ = (long)math.floor(absolute.z / cellSize);
            return new AbsoluteUniversePositionBlit128
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                Local = new float4(
                    (float)(absolute.x - (gridX * cellSize)),
                    (float)(absolute.y - (gridY * cellSize)),
                    (float)(absolute.z - (gridZ * cellSize)),
                    0f),
                Reserved = 0UL
            };
        }

        private static float3 ToLocalDeltaFloat3(in AbsoluteUniversePositionBlit128 aup, in AbsoluteUniversePositionBlit128 origin)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 delta = new double3(
                ((aup.GridX - origin.GridX) * cellSize) + ((double)aup.Local.x - origin.Local.x),
                ((aup.GridY - origin.GridY) * cellSize) + ((double)aup.Local.y - origin.Local.y),
                ((aup.GridZ - origin.GridZ) * cellSize) + ((double)aup.Local.z - origin.Local.z));
            if (!math.all(math.isfinite(delta)))
                return float3.zero;
            return new float3((float)delta.x, (float)delta.y, (float)delta.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePositionBlit128 position)
        {
            return math.all(math.isfinite(position.Local));
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static uint Hash(uint species, uint tick, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ species) * 16777619u;
                hash = (hash ^ tick) * 16777619u;
                hash = (hash ^ salt) * 16777619u;
                return hash == 0u ? 0x53323533u : hash;
            }
        }

        private static float3 ResolveDirection(float3 direction, float3 fallback)
        {
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.0001f)
                direction = fallback;
            if (!math.all(math.isfinite(direction)) || math.lengthsq(direction) <= 0.0001f)
                return new float3(0f, 0f, 1f);
            return direction * math.rsqrt(math.max(math.lengthsq(direction), 0.0001f));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct CullDistantDirectorSlotsJob : IJob
    {
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorOwnedSlotDTO* OwnedSlots;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTuningDTO* Tuning;
        public int OwnedSlotCapacity;

        public void Execute()
        {
            if (OwnedSlots == null || Counters == null || Inputs == null || Tuning == null)
                return;

            DirectorInputDTO input = UnsafeUtility.AsRef<DirectorInputDTO>(Inputs);
            DirectorTuningDTO tuning = StressDrivenSpawnDirectorSanitizer.Sanitize(UnsafeUtility.AsRef<DirectorTuningDTO>(Tuning), input.GlobalQualityWeight);
            if ((tuning.Flags & StressDrivenSpawnDirector.TuningFlagEnableDistantCull) == 0u)
                return;

            int count = math.min(math.max(0, Counters[3]), OwnedSlotCapacity);
            float quality = Smooth01(input.GlobalQualityWeight);
            float radius = math.lerp(
                tuning.DespawnRadiusLowMeters,
                tuning.DespawnRadiusUltraMeters,
                quality);
            double radiusSq = (double)radius * radius;
            int requested = 0;
            for (int i = 0; i < count; i++)
            {
                DirectorOwnedSlotDTO owned = OwnedSlots[i];
                if ((owned.Flags & StressDrivenSpawnDirector.OwnedSlotFlagActive) == 0u)
                    continue;

                double distanceSq = DistanceSqMeters(in owned.LastAup, in input.PlayerAup);
                if (!math.isfinite(distanceSq) || distanceSq > radiusSq)
                {
                    owned.Flags |= StressDrivenSpawnDirector.OwnedSlotFlagCullRequested;
                    OwnedSlots[i] = owned;
                    requested++;
                }
            }

            Counters[4] = requested;
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }

        private static double DistanceSqMeters(in AbsoluteUniversePositionBlit128 a, in AbsoluteUniversePositionBlit128 b)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 delta = new double3(
                ((a.GridX - b.GridX) * cellSize) + ((double)a.Local.x - b.Local.x),
                ((a.GridY - b.GridY) * cellSize) + ((double)a.Local.y - b.Local.y),
                ((a.GridZ - b.GridZ) * cellSize) + ((double)a.Local.z - b.Local.z));
            return math.all(math.isfinite(delta)) ? math.lengthsq(delta) : double.PositiveInfinity;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct AsyncInventoryPreloadTicketJob : IJob
    {
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorSelectionDTO* Selection;
        [NativeDisableUnsafePtrRestriction, NoAlias] public InventoryPreloadTicketDTO* Tickets;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        public int TicketCapacity;
        public int MonolithReady;

        public void Execute()
        {
            if (Selection == null || Tickets == null || Inputs == null || TicketCapacity <= 0)
                return;

            DirectorSelectionDTO selection = Selection[0];
            if (selection.SpeciesHash == 0u)
                return;

            int index = (int)(selection.SpeciesHash % (uint)TicketCapacity);
            InventoryPreloadTicketDTO ticket = default;
            ticket.SpeciesHash = selection.SpeciesHash;
            ticket.LootTableHash = selection.LootTableHash;
            ticket.Flags = MonolithReady != 0 && selection.LootTableHash != 0u ? 1u : 2u;
            ticket.LastRequestedFrame = Inputs[0].Frame;
            ticket.BudgetWeight = math.max(0f, selection.Budget);
            Tickets[index] = ticket;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct RecordDirectorTelemetryJob : IJob
    {
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorSelectionDTO* Selection;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorInputDTO* Inputs;
        [ReadOnly, NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTuningDTO* Tuning;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorTelemetryEntry* Telemetry;
        [NativeDisableUnsafePtrRestriction, NoAlias] public int* Counters;
        [NativeDisableUnsafePtrRestriction, NoAlias] public DirectorSpawnDebugDTO* SpawnDebug;
        public int TelemetryCapacity;

        public void Execute()
        {
            if (Selection == null || Inputs == null || Telemetry == null || Counters == null || TelemetryCapacity <= 0)
                return;

            DirectorSelectionDTO selection = Selection[0];
            DirectorInputDTO input = Inputs[0];
            DirectorTuningDTO tuning = Tuning != null
                ? StressDrivenSpawnDirectorSanitizer.Sanitize(UnsafeUtility.AsRef<DirectorTuningDTO>(Tuning), input.GlobalQualityWeight)
                : DirectorTuningDTO.CreateDefault(input.GlobalQualityWeight);
            Counters[5] = 0;
            int cursor = Counters[2];
            if ((uint)cursor >= (uint)TelemetryCapacity)
                cursor = 0;

            DirectorTelemetryEntry entry = default;
            entry.Frame = input.Frame;
            entry.StateHash = selection.StateHash;
            entry.TensionIndex = math.saturate(input.TensionIndex);
            entry.TurbidityScalar = math.max(1f, input.TurbidityScalar);
            entry.GlobalQualityWeight = math.saturate(input.GlobalQualityWeight);
            entry.Budget = selection.Budget;
            entry.CandidateCount = (ushort)math.clamp(Counters[1], 0, ushort.MaxValue);
            entry.OwnedSlotCount = (ushort)math.clamp(Counters[3], 0, ushort.MaxValue);
            entry.Spawned = 0;
            entry.Culled = (ushort)math.clamp(Counters[4], 0, ushort.MaxValue);
            entry.Flags = 0u;
            entry.Flags |= Counters[4] > 0 ? StressDrivenSpawnDirector.TelemetryFlagCulled : 0u;
            entry.Flags |= (selection.Flags & StressDrivenSpawnDirector.SelectionFlagFault) != 0u ? StressDrivenSpawnDirector.TelemetryFlagFault : 0u;
            entry.Flags |= (selection.Flags & StressDrivenSpawnDirector.SelectionFlagLootMissing) != 0u ? StressDrivenSpawnDirector.TelemetryFlagLootMissing : 0u;
            entry.PlayerAup = input.PlayerAup;
            entry.LastSpawnAup = selection.SpawnAup;
            entry.DumpReasonHash = (selection.Flags & StressDrivenSpawnDirector.SelectionFlagFault) != 0u
                ? 0x534E414Eu
                : ((selection.Flags & StressDrivenSpawnDirector.SelectionFlagLootMissing) != 0u ? 0x534C4F54u : 0u);
            entry.LootTableHash = selection.LootTableHash;
            entry.PreyBiomass01 = math.saturate(input.PreyBiomass01);
            entry.PredatorBiomass01 = math.saturate(input.PredatorBiomass01);
            entry.CarryingCapacity01 = math.saturate(input.CarryingCapacity01);
            entry.SectorHash = input.SectorHash;
            entry.MacroEcosystemStateHash = input.MacroEcosystemStateHash;
            entry.SpawnRadiusMeters = selection.SpawnRadiusMeters;
            entry.SpawnSlot = 0u;
            entry.OriginShiftSequence = input.OriginShiftSequence;
            Telemetry[cursor] = entry;
            Counters[2] = (cursor + 1) % TelemetryCapacity;

            if (SpawnDebug != null)
            {
                DirectorSpawnDebugDTO debug = default;
                debug.SpawnAup = selection.SpawnAup;
                debug.SpeciesHash = selection.SpeciesHash;
                debug.Flags = selection.Flags;
                debug.RadiusMeters = selection.SpawnRadiusMeters;
                debug.ThreatScore = selection.ThreatScore;
                debug.RuntimeSpawn = selection.RuntimeSpawn;
                debug.Frame = input.Frame;
                debug.StateHash = selection.StateHash;
                debug.MinHiddenRadiusMeters = tuning.MinHiddenRadiusMeters;
                debug.MaxHiddenRadiusMeters = tuning.MaxHiddenRadiusMeters;
                debug.DespawnRadiusMeters = math.lerp(
                    tuning.DespawnRadiusLowMeters,
                    tuning.DespawnRadiusUltraMeters,
                    Smooth01(input.GlobalQualityWeight));
                debug.OwnedSlotCount = (uint)math.max(0, Counters[3]);
                debug.SectorHash = input.SectorHash;
                debug.MacroEcosystemStateHash = input.MacroEcosystemStateHash;
                SpawnDebug[0] = debug;
            }
        }

        private static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - (2f * x));
        }
    }
}
