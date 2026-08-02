using System.Runtime.InteropServices;
using System;
using System.Threading;
using static Hecton8.Core.UnityMathematicsExtensions;
using Hecton8.Audio;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Scavenging;
using Hecton8.SaveSystem;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Zero-allocation hand IK snap target resolved from the active indirect-flora lanes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct FloraHarvestInteractionPoint
    {
        [FieldOffset(0)]
        public readonly AbsoluteUniversePosition AnchorAup;
        [FieldOffset(48)]
        public readonly Vector3 RuntimePosition;
        [FieldOffset(60)]
        public readonly Vector3 SurfaceNormal;
        [FieldOffset(72)]
        public readonly uint InstanceUid;
        [FieldOffset(76)]
        public readonly int TemplateIndex;
        [FieldOffset(80)]
        public readonly float BlendWeight;
        [FieldOffset(84)]
        public readonly HarvestableTemplate.MaterialClass MaterialClass;
        [FieldOffset(85)] private readonly byte _pad0;
        [FieldOffset(86)] private readonly byte _pad1;
        [FieldOffset(87)] private readonly byte _pad2;
        [FieldOffset(88)] private readonly byte _pad3;
        [FieldOffset(89)] private readonly byte _pad4;
        [FieldOffset(90)] private readonly byte _pad5;
        [FieldOffset(91)] private readonly byte _pad6;
        [FieldOffset(92)] private readonly byte _pad7;
        [FieldOffset(93)] private readonly byte _pad8;
        [FieldOffset(94)] private readonly byte _pad9;
        [FieldOffset(95)] private readonly byte _pad10;

        public FloraHarvestInteractionPoint(
            uint instanceUid,
            AbsoluteUniversePosition anchorAup,
            Vector3 runtimePosition,
            Vector3 surfaceNormal,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            float blendWeight)
        {
            AnchorAup = anchorAup;
            RuntimePosition = runtimePosition;
            SurfaceNormal = surfaceNormal;
            InstanceUid = instanceUid;
            TemplateIndex = templateIndex;
            BlendWeight = blendWeight;
            MaterialClass = materialClass;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0;
            _pad3 = 0;
            _pad4 = 0;
            _pad5 = 0;
            _pad6 = 0;
            _pad7 = 0;
            _pad8 = 0;
            _pad9 = 0;
            _pad10 = 0;
        }
    }

    /// <summary>
    /// Runtime owner for indirect-flora harvest health, destruction, debris, and yield routing.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-120)] // Manager order must stay ahead of gameplay consumers that read/wire destruction state.
    public sealed class DestructibleOrganicManager : MonoBehaviour, ITickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IOrganicToolHitService
    {
        private static int s_x001DirectSignalPushDropCount_DestructibleOrganicManager;

        private static int s_x001DestructibleOrganicManagerSignalPushDropCount;
        private static DestructibleOrganicManager _activeRuntimeInstance;

        private const int DefaultTrackedDestroyedCapacity = 2048;
        private const int DefaultTrackedHealthCapacity = 4096;
        private const int DefaultPendingYieldCapacity = 1024;
        private const int DefaultDropBufferCapacity = 256;
        private const int MaxOrganicDropRecordsPerFrame = 256;
        private const int MaxOrganicDropDrainStackBatch = 8;
        private const int MaxOrganicYieldNavDispatchStackBatch = 4;
        private const int MaxOrganicPassiveDecompositionStackBatch = 8;
        private const int DropBudgetRemainingIndex = 0;
        private const int DropBudgetDroppedIndex = 1;
        private const int DropBudgetLength = 2;
        private const int MaxOrganicCacheSyncRegistryBatch = 8;
        private const float HiddenInstanceWorldY = -100000f;
        private const float MinimumSearchRadius = 0.8f;
        private const float KelpRadiusBias = 0.65f;
        private const float OrganicBurstVelocityScale = 3f;
        private const float OrganicWiltDurationSeconds = 0.85f;
        private const float OrganicDecompositionDurationSeconds = 10f * 60f;
        private const float MinimumDecomposedHeightScale = 0.05f;
        private const float MinimumDecomposedWidthScale = 0.12f;
        private const float HarvestStatePartialThreshold01 = 0.999f;
        private const float HarvestStateBareThreshold01 = 0.3f;
        private const float MatureSporeGrowthThreshold01 = 0.999f;
        private const float MinimumSporePulseFrequencyHz = 0.01f;
        private const float SporePulsePeakPhase01 = 0.25f;
        private const float SporeShaderPhasePositionX = 0.07f;
        private const float SporeShaderPhasePositionZ = 0.05f;
        private const float InvTwoPi = 0.15915494309189535f;
        private const float SoftBareHealthFloor01 = 0.05f;
        private const float LightStarvationDamagePerSlowTick01 = 0.035f;
        private const float LightStarvationDeathHealth01 = 0.015f;
        private const float AllelopathicBareHealth01 = 0.08f;
        private const float AllelopathicDeathThreshold01 = 0.85f;
        private const float OvergrowthUntouchedSeconds = 3f * 24f * 60f * 60f;
        private const float OvergrowthExpansionMeters = 2f;
        private const int OvergrowthMinChecksPerSlowTick = 8;
        private const int OvergrowthMaxChecksPerSlowTick = 64;
        private const int OrganicVisualMinChecksPerTick = 96;
        private const int OrganicVisualMaxChecksPerTick = 512;
        private const int MatureSporeMinChecksPerTick = 8;
        private const int AllelopathicMinCoralChecksPerSlowTick = 4;
        private const int AllelopathicMaxCoralChecksPerSlowTick = 24;
        private const int AllelopathicMinKelpChecksPerCoral = 24;
        private const int AllelopathicMaxKelpChecksPerCoral = 128;
        private const float TitanRootMoundRadiusMeters = 5f;
        private const float TitanRootMoundStrengthMeters = 2.25f;
        private const float TitanRootMoundMatureThreshold01 = 0.999f;
        private const byte TitanRootMoundPending = 1;
        private const byte TitanRootMoundApplied = 2;
        private const byte FloraRuntimeFlagHasParasite = (byte)HectonVegetationRuntimeFlags.Parasite;
        private const byte FloraRuntimeFlagDead = 1 << 6;
        private const int DefaultCorpseNodeCapacity = 96;
        private const float DefaultCorpseBloodIntensity = 6f;
        private const float CorpseDiseaseActivationSeconds = 120f;
        private const float CorpseDiseaseRadiusMeters = 22f;
        private const float CorpseDiseaseSeverity = 1f;
        private const double CorpseNodeIdCollisionDistanceSq = 0.000001d;
        private const int MaterialClassCount = 5;
        private const int DearLieMaxDamageSignalsPerFrame = 128;
        private const int DearLieMockDamageSignalCount = 100;
        private const int DearLieMaxResultsPerFrame = DearLieMaxDamageSignalsPerFrame * 2;
        private const int DearLieMaxRegenRecords = 2048;
        private const int DearLieTelemetryFrameCount = 300;
        private const int DearLieSpatialHashCapacity = 8192;
        private const int DearLieJobBatchSize = 64;
        private const float DearLieQueryRadiusMeters = 2.25f;
        private const float DearLieSpatialCellSizeMeters = 3f;
        private const float DearLieRegenerationDelaySeconds = 300f;
        private const float DearLieRegenerationRetryDelaySeconds = 1f;
        private const float DearLieMinimumMagnitude = 0.001f;
        private const double OrganicClockMaxSeconds = 16777215d;
        private const uint DearLieSignalHashFlora = 0x464C4F52u; // FLOR
        private const uint DearLieSignalHashOrganic = 0x4F524741u; // ORGA
        private const uint DearLiePostSimulationSystemHash = 0x444F5053u; // DOPS
        private const byte DearLieFloraDamageFlag = 1 << 6;
        private const BufferID DearLieSurfaceClaimsBufferId = BufferID.DestructibleOrganicManager_DearLieSurfaceClaimsBufferId;
        private const BufferID DearLieUnderwaterClaimsBufferId = BufferID.DestructibleOrganicManager_DearLieUnderwaterClaimsBufferId;
        private const BufferID DearLieDamageEventsBufferId = BufferID.DestructibleOrganicManager_DearLieDamageEventsBufferId;
        private const BufferID DearLieResultsBufferId = BufferID.DestructibleOrganicManager_DearLieResultsBufferId;
        private const BufferID DearLieCountersBufferId = BufferID.DestructibleOrganicManager_DearLieCountersBufferId;
        private const BufferID DearLieRegenRecordsBufferId = BufferID.DestructibleOrganicManager_DearLieRegenRecordsBufferId;
        private const BufferID DearLieTelemetryRingBufferId = BufferID.DestructibleOrganicManager_DearLieTelemetryRingBufferId;
        private const BufferID DearLieSurfaceBucketHeadsBufferId = BufferID.DestructibleOrganicManager_DearLieSurfaceBucketHeadsBufferId;
        private const BufferID DearLieSurfaceBucketNextBufferId = BufferID.DestructibleOrganicManager_DearLieSurfaceBucketNextBufferId;
        private const BufferID DearLieUnderwaterBucketHeadsBufferId = BufferID.DestructibleOrganicManager_DearLieUnderwaterBucketHeadsBufferId;
        private const BufferID DearLieUnderwaterBucketNextBufferId = BufferID.DestructibleOrganicManager_DearLieUnderwaterBucketNextBufferId;
        private const BufferID OrganicSurfaceInstanceUidsBufferId = BufferID.DestructibleOrganicManager_OrganicSurfaceInstanceUidsBufferId;
        private const BufferID OrganicUnderwaterInstanceUidsBufferId = BufferID.DestructibleOrganicManager_OrganicUnderwaterInstanceUidsBufferId;
        private const BufferID OrganicSurfaceMaterialClassesBufferId = BufferID.DestructibleOrganicManager_OrganicSurfaceMaterialClassesBufferId;
        private const BufferID OrganicUnderwaterMaterialClassesBufferId = BufferID.DestructibleOrganicManager_OrganicUnderwaterMaterialClassesBufferId;
        private const BufferID OrganicSurfaceHealthBufferId = BufferID.DestructibleOrganicManager_OrganicSurfaceHealthBufferId;
        private const BufferID OrganicUnderwaterHealthBufferId = BufferID.DestructibleOrganicManager_OrganicUnderwaterHealthBufferId;
        private const BufferID OrganicHealthByUidBufferId = BufferID.DestructibleOrganicManager_OrganicHealthByUidBufferId;
        private const BufferID OrganicDestroyedByUidBufferId = BufferID.DestructibleOrganicManager_OrganicDestroyedByUidBufferId;
        private const BufferID OrganicPendingWiltEndTimeByUidBufferId = BufferID.DestructibleOrganicManager_OrganicPendingWiltEndTimeByUidBufferId;
        private const BufferID OrganicDamageVisualProgressByUidBufferId = BufferID.DestructibleOrganicManager_OrganicDamageVisualProgressByUidBufferId;
        private const BufferID OrganicDecompositionStartTimeByUidBufferId = BufferID.DestructibleOrganicManager_OrganicDecompositionStartTimeByUidBufferId;
        private const BufferID OrganicRegrowthProgressByUidBufferId = BufferID.DestructibleOrganicManager_OrganicRegrowthProgressByUidBufferId;
        private const BufferID OrganicRegrowthPositionByUidBufferId = BufferID.DestructibleOrganicManager_OrganicRegrowthPositionByUidBufferId;
        private const BufferID OrganicMaturationScaleByUidBufferId = BufferID.DestructibleOrganicManager_OrganicMaturationScaleByUidBufferId;
        private const BufferID OrganicMaturationYieldByUidBufferId = BufferID.DestructibleOrganicManager_OrganicMaturationYieldByUidBufferId;
        private const BufferID OrganicNextSporeAcousticTimeByUidBufferId = BufferID.DestructibleOrganicManager_OrganicNextSporeAcousticTimeByUidBufferId;
        private const BufferID OrganicBaseScaleByUidBufferId = BufferID.DestructibleOrganicManager_OrganicBaseScaleByUidBufferId;
        private const BufferID OrganicRuntimeFlagsByUidBufferId = BufferID.DestructibleOrganicManager_OrganicRuntimeFlagsByUidBufferId;
        private const BufferID OrganicLastTouchTimeByUidBufferId = BufferID.DestructibleOrganicManager_OrganicLastTouchTimeByUidBufferId;
        private const BufferID OrganicOvergrownByUidBufferId = BufferID.DestructibleOrganicManager_OrganicOvergrownByUidBufferId;
        private const BufferID OrganicRootMoundAppliedByUidBufferId = BufferID.DestructibleOrganicManager_OrganicRootMoundAppliedByUidBufferId;
        private const BufferID OrganicDestroyedFloraScratchBufferId = BufferID.DestructibleOrganicManager_OrganicDestroyedFloraScratchBufferId;
        private const BufferID OrganicFloraStateOverrideScratchBufferId = BufferID.DestructibleOrganicManager_OrganicFloraStateOverrideScratchBufferId;
        private const BufferID OrganicPersistedHealth01ByUidBufferId = BufferID.DestructibleOrganicManager_OrganicPersistedHealth01ByUidBufferId;
        private const BufferID OrganicPersistedHeightScale01ByUidBufferId = BufferID.DestructibleOrganicManager_OrganicPersistedHeightScale01ByUidBufferId;
        private const BufferID OrganicPendingYieldEventsBufferId = BufferID.DestructibleOrganicManager_OrganicPendingYieldEventsBufferId;
        private const BufferID OrganicYieldJobInputBufferId = BufferID.DestructibleOrganicManager_OrganicYieldJobInputBufferId;
        private const BufferID OrganicTemplateDescriptorsBufferId = BufferID.DestructibleOrganicManager_OrganicTemplateDescriptorsBufferId;
        private const BufferID OrganicLootEntriesBufferId = BufferID.DestructibleOrganicManager_OrganicLootEntriesBufferId;
        private const BufferID OrganicYieldMaterialLutBufferId = BufferID.DestructibleOrganicManager_OrganicYieldMaterialLutBufferId;
        private const BufferID OrganicDropDebugScratchBufferId = BufferID.DestructibleOrganicManager_OrganicDropDebugScratchBufferId;
        private const BufferID OrganicDropOutputBufferId = BufferID.DestructibleOrganicManager_OrganicDropOutputBufferId;
        private const BufferID OrganicDropBudgetBufferId = BufferID.DestructibleOrganicManager_OrganicDropBudgetBufferId;
        private const int DearLieVaultJobBufferCount = 33;
        private const int OrganicRegrowthMutationBufferCount = 21;
        private const int OrganicMaturationMutationBufferCount = 3;
        private const int OrganicOvergrowthMutationBufferCount = 11;
        private const int OrganicParasiteExposureReadBufferCount = 5;
        private const int OrganicLifecycleReadBufferCount = 9;
        private const int YieldJobBufferCount = 7;
        private const int DearLieMaxPendingDebrisSignalsPerFrame = 16;
        private const int ParasiteExposureMinScanBudgetPerLane = 16;
        private const int ParasiteExposureMaxScanBudgetPerLane = 96;
        private const float ParasiteExposureMinRefreshIntervalSeconds = 0.05f;
        private const float ParasiteExposureMaxRefreshIntervalSeconds = 0.25f;
        private const float ParasiteExposureHoldSeconds = 0.45f;
        private const float ParasiteExposureQueryResetDistanceSq = 2.25f;
        private const SystemID OrganicVaultSystemId = SystemID.FloraGenomics;
        private const string NativeMemoryOwner = nameof(DestructibleOrganicManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const string TemplateLootBuildScratchLabel = nameof(TemplateLootBuildScratchLabel);

        private enum HarvestState : byte
        {
            Pristine = 0,
            PartiallyHarvested = 1,
            Bare = 2,
            Dead = 3
        }

        private struct CorpseResourceNodeRecord
        {
            public uint NodeId;
            public uint ContaminatedItemHash;
            public int SpeciesId;
            public AbsoluteUniversePosition PositionAup;
            public Vector3 Position;
            public float InitialUnits;
            public float RemainingUnits;
            public float BloodIntensity;
            public float SpawnTime;
            public float ExpireTime;
            public byte Active;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct FloraDestructionEventDTO
        {
            [FieldOffset(0)] public double3 ImpactAUP;
            [FieldOffset(24)] public uint FloraTypeHash;
            [FieldOffset(28)] public uint MagnitudeBits;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct FloraDearLieDestructionResult
        {
            [FieldOffset(0)] public double3 ImpactAUP;
            [FieldOffset(24)] public Matrix4x4 OriginalMatrix;
            [FieldOffset(88)] public uint InstanceUid;
            [FieldOffset(92)] public int ActiveIndex;
            [FieldOffset(96)] public uint FloraTypeHash;
            [FieldOffset(100)] public uint MagnitudeBits;
            [FieldOffset(104)] public ushort VfxQuantity;
            [FieldOffset(106)] public byte EmitVfx;
            [FieldOffset(107)] public byte MaterialClass;
            [FieldOffset(108)] private byte _pad0;
            [FieldOffset(109)] private byte _pad1;
            [FieldOffset(110)] private byte _pad2;
            [FieldOffset(111)] private byte _pad3;
            [FieldOffset(112)] private byte _pad4;
            [FieldOffset(113)] private byte _pad5;
            [FieldOffset(114)] private byte _pad6;
            [FieldOffset(115)] private byte _pad7;
            [FieldOffset(116)] private byte _pad8;
            [FieldOffset(117)] private byte _pad9;
            [FieldOffset(118)] private byte _pad10;
            [FieldOffset(119)] private byte _pad11;
            [FieldOffset(120)] private byte _pad12;
            [FieldOffset(121)] private byte _pad13;
            [FieldOffset(122)] private byte _pad14;
            [FieldOffset(123)] private byte _pad15;
            [FieldOffset(124)] private byte _pad16;
            [FieldOffset(125)] private byte _pad17;
            [FieldOffset(126)] private byte _pad18;
            [FieldOffset(127)] private byte _pad19;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieCounter64
        {
            [FieldOffset(0)] public int Value;
            [FieldOffset(4)] private byte _pad0;
            [FieldOffset(5)] private byte _pad1;
            [FieldOffset(6)] private byte _pad2;
            [FieldOffset(7)] private byte _pad3;
            [FieldOffset(8)] private byte _pad4;
            [FieldOffset(9)] private byte _pad5;
            [FieldOffset(10)] private byte _pad6;
            [FieldOffset(11)] private byte _pad7;
            [FieldOffset(12)] private byte _pad8;
            [FieldOffset(13)] private byte _pad9;
            [FieldOffset(14)] private byte _pad10;
            [FieldOffset(15)] private byte _pad11;
            [FieldOffset(16)] private byte _pad12;
            [FieldOffset(17)] private byte _pad13;
            [FieldOffset(18)] private byte _pad14;
            [FieldOffset(19)] private byte _pad15;
            [FieldOffset(20)] private byte _pad16;
            [FieldOffset(21)] private byte _pad17;
            [FieldOffset(22)] private byte _pad18;
            [FieldOffset(23)] private byte _pad19;
            [FieldOffset(24)] private byte _pad20;
            [FieldOffset(25)] private byte _pad21;
            [FieldOffset(26)] private byte _pad22;
            [FieldOffset(27)] private byte _pad23;
            [FieldOffset(28)] private byte _pad24;
            [FieldOffset(29)] private byte _pad25;
            [FieldOffset(30)] private byte _pad26;
            [FieldOffset(31)] private byte _pad27;
            [FieldOffset(32)] private byte _pad28;
            [FieldOffset(33)] private byte _pad29;
            [FieldOffset(34)] private byte _pad30;
            [FieldOffset(35)] private byte _pad31;
            [FieldOffset(36)] private byte _pad32;
            [FieldOffset(37)] private byte _pad33;
            [FieldOffset(38)] private byte _pad34;
            [FieldOffset(39)] private byte _pad35;
            [FieldOffset(40)] private byte _pad36;
            [FieldOffset(41)] private byte _pad37;
            [FieldOffset(42)] private byte _pad38;
            [FieldOffset(43)] private byte _pad39;
            [FieldOffset(44)] private byte _pad40;
            [FieldOffset(45)] private byte _pad41;
            [FieldOffset(46)] private byte _pad42;
            [FieldOffset(47)] private byte _pad43;
            [FieldOffset(48)] private byte _pad44;
            [FieldOffset(49)] private byte _pad45;
            [FieldOffset(50)] private byte _pad46;
            [FieldOffset(51)] private byte _pad47;
            [FieldOffset(52)] private byte _pad48;
            [FieldOffset(53)] private byte _pad49;
            [FieldOffset(54)] private byte _pad50;
            [FieldOffset(55)] private byte _pad51;
            [FieldOffset(56)] private byte _pad52;
            [FieldOffset(57)] private byte _pad53;
            [FieldOffset(58)] private byte _pad54;
            [FieldOffset(59)] private byte _pad55;
            [FieldOffset(60)] private byte _pad56;
            [FieldOffset(61)] private byte _pad57;
            [FieldOffset(62)] private byte _pad58;
            [FieldOffset(63)] private byte _pad59;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieClaim64
        {
            [FieldOffset(0)] public int Claimed;
            [FieldOffset(4)] private byte _pad0;
            [FieldOffset(5)] private byte _pad1;
            [FieldOffset(6)] private byte _pad2;
            [FieldOffset(7)] private byte _pad3;
            [FieldOffset(8)] private byte _pad4;
            [FieldOffset(9)] private byte _pad5;
            [FieldOffset(10)] private byte _pad6;
            [FieldOffset(11)] private byte _pad7;
            [FieldOffset(12)] private byte _pad8;
            [FieldOffset(13)] private byte _pad9;
            [FieldOffset(14)] private byte _pad10;
            [FieldOffset(15)] private byte _pad11;
            [FieldOffset(16)] private byte _pad12;
            [FieldOffset(17)] private byte _pad13;
            [FieldOffset(18)] private byte _pad14;
            [FieldOffset(19)] private byte _pad15;
            [FieldOffset(20)] private byte _pad16;
            [FieldOffset(21)] private byte _pad17;
            [FieldOffset(22)] private byte _pad18;
            [FieldOffset(23)] private byte _pad19;
            [FieldOffset(24)] private byte _pad20;
            [FieldOffset(25)] private byte _pad21;
            [FieldOffset(26)] private byte _pad22;
            [FieldOffset(27)] private byte _pad23;
            [FieldOffset(28)] private byte _pad24;
            [FieldOffset(29)] private byte _pad25;
            [FieldOffset(30)] private byte _pad26;
            [FieldOffset(31)] private byte _pad27;
            [FieldOffset(32)] private byte _pad28;
            [FieldOffset(33)] private byte _pad29;
            [FieldOffset(34)] private byte _pad30;
            [FieldOffset(35)] private byte _pad31;
            [FieldOffset(36)] private byte _pad32;
            [FieldOffset(37)] private byte _pad33;
            [FieldOffset(38)] private byte _pad34;
            [FieldOffset(39)] private byte _pad35;
            [FieldOffset(40)] private byte _pad36;
            [FieldOffset(41)] private byte _pad37;
            [FieldOffset(42)] private byte _pad38;
            [FieldOffset(43)] private byte _pad39;
            [FieldOffset(44)] private byte _pad40;
            [FieldOffset(45)] private byte _pad41;
            [FieldOffset(46)] private byte _pad42;
            [FieldOffset(47)] private byte _pad43;
            [FieldOffset(48)] private byte _pad44;
            [FieldOffset(49)] private byte _pad45;
            [FieldOffset(50)] private byte _pad46;
            [FieldOffset(51)] private byte _pad47;
            [FieldOffset(52)] private byte _pad48;
            [FieldOffset(53)] private byte _pad49;
            [FieldOffset(54)] private byte _pad50;
            [FieldOffset(55)] private byte _pad51;
            [FieldOffset(56)] private byte _pad52;
            [FieldOffset(57)] private byte _pad53;
            [FieldOffset(58)] private byte _pad54;
            [FieldOffset(59)] private byte _pad55;
            [FieldOffset(60)] private byte _pad56;
            [FieldOffset(61)] private byte _pad57;
            [FieldOffset(62)] private byte _pad58;
            [FieldOffset(63)] private byte _pad59;
        }

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        private struct FloraDearLieRegenRecord
        {
            [FieldOffset(0)] public Matrix4x4 OriginalMatrix;
            [FieldOffset(64)] public uint InstanceUid;
            [FieldOffset(68)] public int ActiveIndex;
            [FieldOffset(72)] public float RestoreTimeSeconds;
            [FieldOffset(76)] public float3 RuntimePosition;
            [FieldOffset(88)] public byte Underwater;
            [FieldOffset(89)] private byte _pad0;
            [FieldOffset(90)] private byte _pad1;
            [FieldOffset(91)] private byte _pad2;
            [FieldOffset(92)] private byte _pad3;
            [FieldOffset(93)] private byte _pad4;
            [FieldOffset(94)] private byte _pad5;
            [FieldOffset(95)] private byte _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FloraDearLieTelemetryEntry
        {
            [FieldOffset(0)] public int FrameIndex;
            [FieldOffset(4)] public int SurfaceCount;
            [FieldOffset(8)] public int UnderwaterCount;
            [FieldOffset(12)] public int DamageSignalCount;
            [FieldOffset(16)] public int DestroyedCount;
            [FieldOffset(20)] public int VfxSignalCount;
            [FieldOffset(24)] public int RegenQueuedCount;
            [FieldOffset(28)] public int RecoveredCount;
            [FieldOffset(32)] public int RejectedSignalCount;
            [FieldOffset(36)] public int NanRejectCount;
            [FieldOffset(40)] public float GlobalQualityWeight;
            [FieldOffset(44)] public uint Hash;
            [FieldOffset(48)] public uint LastInstanceUid;
            [FieldOffset(52)] public float QueryMicroseconds;
            [FieldOffset(56)] public byte Flags;
            [FieldOffset(57)] private byte _pad0;
            [FieldOffset(58)] private byte _pad1;
            [FieldOffset(59)] private byte _pad2;
            [FieldOffset(60)] private byte _pad3;
            [FieldOffset(61)] private byte _pad4;
            [FieldOffset(62)] private byte _pad5;
            [FieldOffset(63)] private byte _pad6;
        }

        private struct SporeAcousticEvent
        {
            public AbsoluteUniversePosition PositionAup;
            public Vector3 RuntimePosition;
            public AudioClip Clip;
            public float PulseFrequencyHz;
            public float Volume;
            public float Pitch;
            public float SimulationTimeSeconds;
            public float PhaseOffset01;
            public bool HasAup;
        }

        private struct HarvestAudioEvent
        {
            public AbsoluteUniversePosition PositionAup;
            public Vector3 RuntimePosition;
            public AudioClip Clip;
            public float Volume;
            public float Pitch;
            public bool HasAup;
        }

        private struct PassiveDecompositionCandidate
        {
            public byte Underwater;
            public int ActiveIndex;
            public uint InstanceUid;
            public HarvestableTemplate.MaterialClass MaterialClass;
            public int TemplateIndex;
            public Vector3 RuntimePosition;
        }

        private interface IOrganicUidMapEntry<TValue>
            where TValue : unmanaged
        {
            uint Key { get; set; }
            TValue Value { get; set; }
            byte State { get; set; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct OrganicHalfMapEntry : IOrganicUidMapEntry<Unity.Mathematics.half>
        {
            [FieldOffset(0)] private uint _key;
            [FieldOffset(4)] private Unity.Mathematics.half _value;
            [FieldOffset(6)] private byte _state;
            [FieldOffset(7)] private byte _pad0;
            [FieldOffset(8)] private byte _pad1;
            [FieldOffset(9)] private byte _pad2;
            [FieldOffset(10)] private byte _pad3;
            [FieldOffset(11)] private byte _pad4;
            [FieldOffset(12)] private byte _pad5;
            [FieldOffset(13)] private byte _pad6;
            [FieldOffset(14)] private byte _pad7;
            [FieldOffset(15)] private byte _pad8;

            public uint Key { get => _key; set => _key = value; }
            public Unity.Mathematics.half Value { get => _value; set => _value = value; }
            public byte State { get => _state; set => _state = value; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct OrganicByteMapEntry : IOrganicUidMapEntry<byte>
        {
            [FieldOffset(0)] private uint _key;
            [FieldOffset(4)] private byte _value;
            [FieldOffset(5)] private byte _state;
            [FieldOffset(6)] private byte _pad0;
            [FieldOffset(7)] private byte _pad1;
            [FieldOffset(8)] private byte _pad2;
            [FieldOffset(9)] private byte _pad3;
            [FieldOffset(10)] private byte _pad4;
            [FieldOffset(11)] private byte _pad5;
            [FieldOffset(12)] private byte _pad6;
            [FieldOffset(13)] private byte _pad7;
            [FieldOffset(14)] private byte _pad8;
            [FieldOffset(15)] private byte _pad9;

            public uint Key { get => _key; set => _key = value; }
            public byte Value { get => _value; set => _value = value; }
            public byte State { get => _state; set => _state = value; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct OrganicFloatMapEntry : IOrganicUidMapEntry<float>
        {
            [FieldOffset(0)] private uint _key;
            [FieldOffset(4)] private float _value;
            [FieldOffset(8)] private byte _state;
            [FieldOffset(9)] private byte _pad0;
            [FieldOffset(10)] private byte _pad1;
            [FieldOffset(11)] private byte _pad2;
            [FieldOffset(12)] private byte _pad3;
            [FieldOffset(13)] private byte _pad4;
            [FieldOffset(14)] private byte _pad5;
            [FieldOffset(15)] private byte _pad6;

            public uint Key { get => _key; set => _key = value; }
            public float Value { get => _value; set => _value = value; }
            public byte State { get => _state; set => _state = value; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct OrganicFloat2MapEntry : IOrganicUidMapEntry<float2>
        {
            [FieldOffset(0)] private uint _key;
            [FieldOffset(4)] private float2 _value;
            [FieldOffset(12)] private byte _state;
            [FieldOffset(13)] private byte _pad0;
            [FieldOffset(14)] private byte _pad1;
            [FieldOffset(15)] private byte _pad2;

            public uint Key { get => _key; set => _key = value; }
            public float2 Value { get => _value; set => _value = value; }
            public byte State { get => _state; set => _state = value; }
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct OrganicFloat3MapEntry : IOrganicUidMapEntry<float3>
        {
            [FieldOffset(0)] private uint _key;
            [FieldOffset(4)] private float3 _value;
            [FieldOffset(16)] private byte _state;
            [FieldOffset(17)] private byte _pad0;
            [FieldOffset(18)] private byte _pad1;
            [FieldOffset(19)] private byte _pad2;
            [FieldOffset(20)] private byte _pad3;
            [FieldOffset(21)] private byte _pad4;
            [FieldOffset(22)] private byte _pad5;
            [FieldOffset(23)] private byte _pad6;

            public uint Key { get => _key; set => _key = value; }
            public float3 Value { get => _value; set => _value = value; }
            public byte State { get => _state; set => _state = value; }
        }

        private struct VaultArray<T>
            where T : struct
        {
            private IDataVault _vault;
            private VaultGenerationHandle<T> _handle;
            private BufferID _bufferId;
            private SystemID _owner;
            private int _requestedLength;

            public bool IsCreated => TryResolve(out NativeArray<T> buffer) && buffer.IsCreated;

            public int Length
            {
                get
                {
                    return TryResolve(out NativeArray<T> buffer) && buffer.IsCreated ? buffer.Length : 0;
                }
            }

            public T this[int index]
            {
                get
                {
                    return TryResolve(out NativeArray<T> buffer) && index >= 0 && index < buffer.Length
                        ? buffer[index]
                        : default;
                }
                set
                {
                    if (TryResolve(out NativeArray<T> buffer) && index >= 0 && index < buffer.Length)
                        buffer[index] = value;
                }
            }

            public bool Ensure(IDataVault vault, BufferID bufferId, int requiredLength, SystemID owner, NativeArrayOptions options)
            {
                if (vault == null || requiredLength <= 0)
                    return false;

                _vault = vault;
                _bufferId = bufferId;
                _owner = owner;
                _requestedLength = math.max(_requestedLength, requiredLength);

                if (_handle.BufferID != 0u &&
                    vault.TryResolveHandle(in _handle, out NativeArray<T> existing) &&
                    existing.IsCreated &&
                    existing.Length >= requiredLength)
                {
                    return true;
                }

                _handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, owner, options);
                return TryResolve(out NativeArray<T> resolved) && resolved.IsCreated && resolved.Length >= requiredLength;
            }

            public bool TryResolve(out NativeArray<T> buffer)
            {
                buffer = default;
                return _vault != null &&
                       _handle.BufferID != 0u &&
                       _handle.SystemID == (uint)_owner &&
                       _handle.BufferID == unchecked((uint)(int)_bufferId) &&
                       _vault.TryResolveHandle(in _handle, out buffer) &&
                       buffer.IsCreated;
            }

            public bool TryReadOnly(out NativeArray<T>.ReadOnly buffer)
            {
                buffer = default;
                return _vault != null &&
                       _handle.BufferID != 0u &&
                       _handle.SystemID == (uint)_owner &&
                       _handle.BufferID == unchecked((uint)(int)_bufferId) &&
                       _vault.TryReadOnlyHandle(in _handle, out buffer);
            }

            public void Clear()
            {
                if (!TryResolve(out NativeArray<T> buffer))
                    return;

                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = default;
            }

            public void Release()
            {
                if (_vault != null && _handle.BufferID != 0u)
                    _vault.ReleaseBuffer(in _handle);

                _handle = default;
                _vault = null;
                _bufferId = default;
                _owner = SystemID.Unknown;
                _requestedLength = 0;
            }

            public static implicit operator NativeArray<T>(VaultArray<T> array)
            {
                return array.TryResolve(out NativeArray<T> buffer) ? buffer : default;
            }
        }

        private struct VaultList<T>
            where T : struct
        {
            private VaultArray<T> _array;
            private int _length;
            private int _capacity;

            public bool IsCreated => _array.IsCreated;
            public int Length => _length;
            public int Capacity => _capacity;

            public T this[int index]
            {
                get => index >= 0 && index < _length ? _array[index] : default;
                set
                {
                    if (index >= 0 && index < _length)
                        _array[index] = value;
                }
            }

            public bool Ensure(IDataVault vault, BufferID bufferId, int capacity, SystemID owner, NativeArrayOptions options)
            {
                _capacity = math.max(1, capacity);
                if (!_array.Ensure(vault, bufferId, _capacity, owner, options))
                {
                    _length = 0;
                    return false;
                }

                if (_length > _capacity)
                    _length = _capacity;

                return true;
            }

            public void Clear()
            {
                _length = 0;
            }

            public void AddNoResize(T value)
            {
                if (_length < 0 || _length >= _capacity)
                    return;

                _array[_length] = value;
                _length++;
            }

            public void ResizeUninitialized(int length)
            {
                _length = math.clamp(length, 0, _capacity);
            }

            public bool TryResolveArray(out NativeArray<T> buffer)
            {
                return _array.TryResolve(out buffer);
            }

            public void Release()
            {
                _array.Release();
                _length = 0;
                _capacity = 0;
            }
        }

        private struct VaultUidMap<TEntry, TValue>
            where TEntry : struct, IOrganicUidMapEntry<TValue>
            where TValue : unmanaged
        {
            private const byte EntryStateEmpty = 0;
            private const byte EntryStateOccupied = 1;
            private const byte EntryStateTombstone = 2;

            private VaultArray<TEntry> _entries;
            private int _capacity;
            private int _count;

            public bool IsCreated => _entries.IsCreated;
            public int Count => _count;

            public bool Ensure(IDataVault vault, BufferID bufferId, int capacity, SystemID owner, NativeArrayOptions options)
            {
                _capacity = math.max(1, math.ceilpow2(capacity));
                if (!_entries.Ensure(vault, bufferId, _capacity, owner, options))
                {
                    _count = 0;
                    return false;
                }

                Recount();
                return true;
            }

            public bool ContainsKey(uint key)
            {
                return TryFindIndex(key, out _, out _);
            }

            public bool TryGetValue(uint key, out TValue value)
            {
                value = default;
                if (!TryFindIndex(key, out int index, out NativeArray<TEntry> entries))
                    return false;

                value = entries[index].Value;
                return true;
            }

            public bool TryAdd(uint key, TValue value)
            {
                if (key == 0u || !_entries.TryResolve(out NativeArray<TEntry> entries) || entries.Length <= 0)
                    return false;

                int mask = entries.Length - 1;
                int start = (int)(HashKey(key) & (uint)mask);
                int firstTombstone = -1;
                for (int probe = 0; probe < entries.Length; probe++)
                {
                    int index = (start + probe) & mask;
                    TEntry entry = entries[index];
                    if (entry.State == EntryStateOccupied)
                    {
                        if (entry.Key == key)
                            return false;

                        continue;
                    }

                    if (entry.State == EntryStateTombstone)
                    {
                        if (firstTombstone < 0)
                            firstTombstone = index;

                        continue;
                    }

                    int writeIndex = firstTombstone >= 0 ? firstTombstone : index;
                    WriteEntry(entries, writeIndex, key, value);
                    _count++;
                    return true;
                }

                if (firstTombstone >= 0)
                {
                    WriteEntry(entries, firstTombstone, key, value);
                    _count++;
                    return true;
                }

                return false;
            }

            public bool TryPut(uint key, TValue value)
            {
                if (key == 0u)
                    return false;

                if (TryFindIndex(key, out int index, out NativeArray<TEntry> entries))
                {
                    TEntry entry = entries[index];
                    entry.Value = value;
                    entry.State = EntryStateOccupied;
                    entries[index] = entry;
                    return true;
                }

                return TryAdd(key, value);
            }

            public bool Remove(uint key)
            {
                if (!TryFindIndex(key, out int index, out NativeArray<TEntry> entries))
                    return false;

                TEntry entry = entries[index];
                entry.Key = 0u;
                entry.Value = default;
                entry.State = EntryStateTombstone;
                entries[index] = entry;
                _count = math.max(0, _count - 1);
                return true;
            }

            public void Clear()
            {
                if (!_entries.TryResolve(out NativeArray<TEntry> entries))
                {
                    _count = 0;
                    return;
                }

                for (int i = 0; i < entries.Length; i++)
                    entries[i] = default;

                _count = 0;
            }

            public void Release()
            {
                _entries.Release();
                _capacity = 0;
                _count = 0;
            }

            private void Recount()
            {
                _count = 0;
                if (!_entries.TryResolve(out NativeArray<TEntry> entries))
                    return;

                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].State == EntryStateOccupied)
                        _count++;
                }
            }

            private bool TryFindIndex(uint key, out int index, out NativeArray<TEntry> entries)
            {
                index = -1;
                entries = default;
                if (key == 0u || !_entries.TryResolve(out entries) || entries.Length <= 0)
                    return false;

                int mask = entries.Length - 1;
                int start = (int)(HashKey(key) & (uint)mask);
                for (int probe = 0; probe < entries.Length; probe++)
                {
                    int candidate = (start + probe) & mask;
                    TEntry entry = entries[candidate];
                    if (entry.State == EntryStateEmpty)
                        return false;

                    if (entry.State == EntryStateOccupied && entry.Key == key)
                    {
                        index = candidate;
                        return true;
                    }
                }

                return false;
            }

            private static void WriteEntry(NativeArray<TEntry> entries, int index, uint key, TValue value)
            {
                TEntry entry = default;
                entry.Key = key;
                entry.Value = value;
                entry.State = EntryStateOccupied;
                entries[index] = entry;
            }

            private static uint HashKey(uint key)
            {
                uint value = key;
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return value;
            }
        }

        private readonly struct BridgeMatrixLane
        {
            private readonly DestructibleOrganicManager _owner;
            private readonly bool _underwater;

            public BridgeMatrixLane(DestructibleOrganicManager owner, bool underwater)
            {
                _owner = owner;
                _underwater = underwater;
            }

            public bool IsCreated => TryResolve(out NativeArray<Matrix4x4> buffer) && buffer.IsCreated;
            public int Length => TryResolve(out NativeArray<Matrix4x4> buffer) && buffer.IsCreated ? buffer.Length : 0;

            public Matrix4x4 this[int index]
            {
                get => TryResolve(out NativeArray<Matrix4x4> buffer) && index >= 0 && index < buffer.Length ? buffer[index] : default;
                set
                {
                    if (TryResolve(out NativeArray<Matrix4x4> buffer) && index >= 0 && index < buffer.Length)
                        buffer[index] = value;
                }
            }

            public bool TryResolve(out NativeArray<Matrix4x4> buffer)
            {
                buffer = default;
                return _owner != null &&
                       _owner.TryResolveVegetationBridgePayload(_underwater, out buffer, out _, out _, out _, out _, out _);
            }

            public static implicit operator NativeArray<Matrix4x4>(BridgeMatrixLane lane)
            {
                return lane.TryResolve(out NativeArray<Matrix4x4> buffer) ? buffer : default;
            }
        }

        private readonly struct BridgeMetadataLane
        {
            private readonly DestructibleOrganicManager _owner;
            private readonly bool _underwater;

            public BridgeMetadataLane(DestructibleOrganicManager owner, bool underwater)
            {
                _owner = owner;
                _underwater = underwater;
            }

            public bool IsCreated => TryResolve(out NativeArray<HectonVegetationInstanceData> buffer) && buffer.IsCreated;
            public int Length => TryResolve(out NativeArray<HectonVegetationInstanceData> buffer) && buffer.IsCreated ? buffer.Length : 0;

            public HectonVegetationInstanceData this[int index]
            {
                get => TryResolve(out NativeArray<HectonVegetationInstanceData> buffer) && index >= 0 && index < buffer.Length ? buffer[index] : default;
                set
                {
                    if (TryResolve(out NativeArray<HectonVegetationInstanceData> buffer) && index >= 0 && index < buffer.Length)
                        buffer[index] = value;
                }
            }

            public bool TryResolve(out NativeArray<HectonVegetationInstanceData> buffer)
            {
                buffer = default;
                return _owner != null &&
                       _owner.TryResolveVegetationBridgePayload(_underwater, out _, out buffer, out _, out _, out _, out _);
            }

            public static implicit operator NativeArray<HectonVegetationInstanceData>(BridgeMetadataLane lane)
            {
                return lane.TryResolve(out NativeArray<HectonVegetationInstanceData> buffer) ? buffer : default;
            }
        }

        private readonly struct BridgeTypeLane
        {
            private readonly DestructibleOrganicManager _owner;
            private readonly bool _underwater;

            public BridgeTypeLane(DestructibleOrganicManager owner, bool underwater)
            {
                _owner = owner;
                _underwater = underwater;
            }

            public bool IsCreated => TryResolve(out NativeArray<int> buffer) && buffer.IsCreated;
            public int Length => TryResolve(out NativeArray<int> buffer) && buffer.IsCreated ? buffer.Length : 0;

            public int this[int index]
            {
                get => TryResolve(out NativeArray<int> buffer) && index >= 0 && index < buffer.Length ? buffer[index] : default;
            }

            public bool TryResolve(out NativeArray<int> buffer)
            {
                buffer = default;
                return _owner != null &&
                       _owner.TryResolveVegetationBridgePayload(_underwater, out _, out _, out buffer, out _, out _, out _);
            }

            public static implicit operator NativeArray<int>(BridgeTypeLane lane)
            {
                return lane.TryResolve(out NativeArray<int> buffer) ? buffer : default;
            }
        }

        private readonly struct BridgeSemanticTypeLane
        {
            private readonly DestructibleOrganicManager _owner;
            private readonly bool _underwater;

            public BridgeSemanticTypeLane(DestructibleOrganicManager owner, bool underwater)
            {
                _owner = owner;
                _underwater = underwater;
            }

            public bool IsCreated => TryResolve(out NativeArray<int>.ReadOnly buffer) && buffer.IsCreated;
            public int Length => TryResolve(out NativeArray<int>.ReadOnly buffer) && buffer.IsCreated ? buffer.Length : 0;

            public int this[int index]
            {
                get => TryResolve(out NativeArray<int>.ReadOnly buffer) && index >= 0 && index < buffer.Length ? buffer[index] : default;
            }

            public bool TryResolve(out NativeArray<int>.ReadOnly buffer)
            {
                buffer = default;
                return _owner != null &&
                       _owner.TryResolveVegetationBridgePayload(_underwater, out _, out _, out _, out buffer, out _, out _);
            }

            public static implicit operator NativeArray<int>.ReadOnly(BridgeSemanticTypeLane lane)
            {
                return lane.TryResolve(out NativeArray<int>.ReadOnly buffer) ? buffer : default;
            }
        }

        private void EvaluateAggressiveOvergrowth(float currentTime)
        {
            EvaluateAggressiveOvergrowthInLane(false, currentTime, ref _surfaceOvergrowthScanCursor);
            EvaluateAggressiveOvergrowthInLane(true, currentTime, ref _underwaterOvergrowthScanCursor);
        }

        private void EvaluateAggressiveOvergrowthInLane(bool underwater, float currentTime, ref int cursor)
        {
            int checks = ResolveOvergrowthScanBudget(ResolveDearLieGlobalQualityWeight());
            for (int step = 0; step < checks; step++)
            {
                if (!TryEvaluateAggressiveOvergrowthStep(
                        underwater,
                        currentTime,
                        ref cursor,
                        out int safeCount,
                        out bool laneUnavailable,
                        out uint instanceUid,
                        out bool overgrowthMarked,
                        out float3 navObstacleCenter,
                        out float3 navObstacleExtents,
                        out bool applyTitanRootMound,
                        out Vector3 titanRootMoundPosition))
                {
                    if (laneUnavailable)
                        return;

                    if (safeCount > 0 && step + 1 >= safeCount)
                        return;

                    continue;
                }

                if (overgrowthMarked)
                {
                    VoxelDynamicNavGridRuntime.EnqueueDynamicObstacleGrowth(
                        navObstacleCenter,
                        navObstacleExtents,
                        OvergrowthExpansionMeters);
                }

                if (applyTitanRootMound)
                    TryApplyPreparedTitanRootMound(instanceUid, titanRootMoundPosition);

                if (safeCount > 0 && step + 1 >= safeCount)
                    return;
            }
        }

        private bool TryEvaluateAggressiveOvergrowthStep(
            bool underwater,
            float currentTime,
            ref int cursor,
            out int safeCount,
            out bool laneUnavailable,
            out uint instanceUid,
            out bool overgrowthMarked,
            out float3 navObstacleCenter,
            out float3 navObstacleExtents,
            out bool applyTitanRootMound,
            out Vector3 titanRootMoundPosition)
        {
            safeCount = 0;
            laneUnavailable = false;
            instanceUid = 0u;
            overgrowthMarked = false;
            navObstacleCenter = float3.zero;
            navObstacleExtents = float3.zero;
            applyTitanRootMound = false;
            titanRootMoundPosition = default;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicOvergrowthMutationGuard(vault, out int lockedMask))
            {
                laneUnavailable = true;
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return false;
            }

            bool touchPrimeFailed = false;
            bool overgrowthWriteFailed = false;
            bool titanRootMoundWriteFailed = false;
            try
            {
                NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
                NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
                NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
                NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
                NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
                NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
                int count = underwater ? _underwaterCount : _surfaceCount;
                if (!matrices.IsCreated ||
                    !metadata.IsCreated ||
                    !types.IsCreated ||
                    !semanticTypes.IsCreated ||
                    !instanceUids.IsCreated ||
                    !materialClasses.IsCreated ||
                    !health.IsCreated ||
                    !_lastOrganicTouchTimeByInstanceUid.IsCreated ||
                    !_overgrownByInstanceUid.IsCreated ||
                    count <= 0)
                {
                    cursor = 0;
                    laneUnavailable = true;
                    return false;
                }

                safeCount = math.min(
                    count,
                    math.min(
                        math.min(matrices.Length, metadata.Length),
                        math.min(
                            math.min(types.Length, semanticTypes.Length),
                            math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length)))));
                if (safeCount <= 0)
                {
                    cursor = 0;
                    laneUnavailable = true;
                    return false;
                }

                if ((uint)cursor >= (uint)safeCount)
                    cursor = 0;

                int activeIndex = cursor;
                cursor++;
                if (cursor >= safeCount)
                    cursor = 0;

                instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u || (float)health[activeIndex] <= 0.0001f)
                    return false;

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
                if (!IsConsumableFloraMaterialClass(materialClass))
                    return false;

                bool alreadyOvergrown = _overgrownByInstanceUid.ContainsKey(instanceUid);
                bool pendingTitanRootMound =
                    alreadyOvergrown &&
                    _rootMoundAppliedByInstanceUid.IsCreated &&
                    _rootMoundAppliedByInstanceUid.TryGetValue(instanceUid, out byte rootMoundState) &&
                    rootMoundState == TitanRootMoundPending;
                bool skipInstance =
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (alreadyOvergrown && !pendingTitanRootMound);
                if (skipInstance)
                    return false;

                if (!_lastOrganicTouchTimeByInstanceUid.TryGetValue(instanceUid, out float lastTouchTime))
                {
                    if (!_lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime))
                        touchPrimeFailed = true;
                }
                else if (currentTime - lastTouchTime >= OvergrowthUntouchedSeconds &&
                         TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out navObstacleCenter, out navObstacleExtents))
                {
                    alreadyOvergrown = _overgrownByInstanceUid.ContainsKey(instanceUid);
                    if (!alreadyOvergrown)
                    {
                        if (_overgrownByInstanceUid.TryAdd(instanceUid, 1))
                        {
                            overgrowthMarked = true;
                            applyTitanRootMound = TryPrepareTitanRootMoundRequest(underwater, activeIndex, instanceUid, out titanRootMoundPosition, out titanRootMoundWriteFailed);
                        }
                        else
                        {
                            overgrowthWriteFailed = true;
                        }
                    }
                    else if (_rootMoundAppliedByInstanceUid.IsCreated &&
                             _rootMoundAppliedByInstanceUid.TryGetValue(instanceUid, out rootMoundState) &&
                             rootMoundState == TitanRootMoundPending)
                    {
                        applyTitanRootMound = TryPrepareTitanRootMoundRequest(underwater, activeIndex, instanceUid, out titanRootMoundPosition, out titanRootMoundWriteFailed);
                    }
                }
            }
            finally
            {
                ReleaseOrganicOvergrowthMutationGuard(vault, lockedMask);
            }

            if (touchPrimeFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            if (overgrowthWriteFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            if (titanRootMoundWriteFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            return overgrowthMarked || applyTitanRootMound;
        }

        private static int ResolveOvergrowthScanBudget(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return math.max(
                OvergrowthMinChecksPerSlowTick,
                (int)math.round(math.lerp(OvergrowthMinChecksPerSlowTick, OvergrowthMaxChecksPerSlowTick, q * q)));
        }

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Authoritative indirect-flora bridge that owns the streamed native instance payloads.")]
        private HectonMapMagicVegetationBridge vegetationBridge;

        [SerializeField]
        [Tooltip("Optional flora interaction manager used to publish localized tool-impact bend bursts.")]
        private FloraInteractionManager floraInteractionManager;

        [Header("Templates")]
        [SerializeField]
        [Tooltip("Authored harvest templates resolved by material class.")]
        private HarvestableTemplate[] harvestTemplates;

        [Header("Debris")]
        [SerializeField]
        [Tooltip("Burst debris profile used for kelp-family destruction.")]
        private OrganicDebrisProfile kelpDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for coral-family destruction.")]
        private OrganicDebrisProfile coralDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for metallic outcrop destruction.")]
        private OrganicDebrisProfile titaniumDebrisProfile;

        [SerializeField]
        [Tooltip("Burst debris profile used for surface sargassum destruction.")]
        private OrganicDebrisProfile sargassumDebrisProfile;

        [Header("Harvest Query")]
        [SerializeField, Min(MinimumSearchRadius)]
        [Tooltip("Base world-space radius used when resolving a tool hit against the active indirect flora arrays.")]
        private float hitSearchRadius = 1.25f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Extra world-space radius added when resolving tall kelp silhouettes.")]
        private float kelpHeightTolerance = 0.4f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Radius of the published flora-interaction burst when a tool hits a harvestable instance.")]
        private float interactionBurstRadius = 1.4f;

        [Header("Allelopathy")]
        [SerializeField, Min(1f)]
        [Tooltip("Planar kelp-density cell radius used when evaluating overcrowding-driven allelopathic coral suppression.")]
        private float allelopathicCellRadius = 14f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum macro-kelp count treated as a full cell when evaluating the 95% overcrowding threshold.")]
        private int allelopathicKelpCapacity = 20;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Normalized kelp occupancy threshold above which competing coral in the same cell is forced into decomposition.")]
        private float allelopathicThreshold01 = 0.95f;

        [Header("Harvest Audio")]
        [SerializeField]
        [Tooltip("Organic-impact clip used when a soft flora harvest transition occurs.")]
        private AudioClip organicHarvestClip;

        [SerializeField]
        [Tooltip("Brittle snap/crack clip used when carbonate-like flora changes harvest state.")]
        private AudioClip brittleHarvestClip;

        [SerializeField]
        [Tooltip("Fibrous tear clip used when kelp- or vine-like flora changes harvest state.")]
        private AudioClip fibrousHarvestClip;

        [SerializeField]
        [Tooltip("Metallic fallback clip used when a flora template routes through the metallic acoustic lane.")]
        private AudioClip metallicHarvestClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Base harvest audio volume applied to partial state changes before state-specific scaling.")]
        private float harvestAudioBaseVolume = 0.72f;

        [Header("Spore Acoustics")]
        [SerializeField]
        [Tooltip("Fallback hostile spore pulse clip used when a mature spore flora template has no authored clip.")]
        private AudioClip sporeAcousticFallbackClip;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fallback volume for mature spore acoustic pulses when the flora template leaves volume at zero.")]
        private float sporeAcousticFallbackVolume = 0.65f;

        [SerializeField, Min(0.05f)]
        [Tooltip("Lower cadence guard for mature spore acoustic pulses. Actual cadence remains locked to 1 / PulseFrequency unless clamped by this value.")]
        private float sporeAcousticMinimumIntervalSeconds = 0.2f;

        [SerializeField, Min(1)]
        [Tooltip("Maximum active flora instances checked per lane each Tick for mature spore acoustic cadence. This keeps large fields bounded.")]
        private int matureSporeAcousticScanBudgetPerTick = 64;
        private const int MaxPendingSporeAcousticEvents = 8;
        private const int MaxPendingHarvestAudioEvents = 8;

        [Header("Dear Lie Destruction")]
        [SerializeField]
        [Tooltip("Editor/runtime smoke hook: one frame generates 100 deterministic SignalBus-equivalent flora damage events around this component.")]
        private bool dearLieGenerateMockDamageBurst;

        [SerializeField]
        [Tooltip("Runtime-local center used by the mock Dear Lie damage generator.")]
        private Vector3 dearLieMockDamageCenter;

        [SerializeField, Range(0.25f, 8f)]
        [Tooltip("Dear Lie spatial damage epsilon in meters. Editor tuning surface; copied into Burst jobs as a scalar.")]
        private float dearLieDamageRadiusEpsilon = DearLieQueryRadiusMeters;

        [SerializeField, Range(5f, 900f)]
        [Tooltip("Visual-only Dear Lie regeneration delay in seconds. Editor tuning surface; copied into the native regen queue as a scalar timestamp.")]
        private float dearLieRegenerationDelaySeconds = DearLieRegenerationDelaySeconds;

        [SerializeField, Range(-1f, 1f)]
        [Tooltip("-1 uses HomeostasisBrain.GlobalQualityWeight. 0..1 overrides Dear Lie VFX gating for editor stress tests.")]
        private float dearLieQualityOverride = -1f;

        private VaultArray<uint> _surfaceInstanceUids;
        private VaultArray<uint> _underwaterInstanceUids;
        private VaultArray<byte> _surfaceMaterialClasses;
        private VaultArray<byte> _underwaterMaterialClasses;
        private VaultArray<Unity.Mathematics.half> _surfaceHealth;
        private VaultArray<Unity.Mathematics.half> _underwaterHealth;
        private VaultArray<int> _surfaceDearLieBucketHeads;
        private VaultArray<int> _surfaceDearLieBucketNext;
        private VaultArray<int> _underwaterDearLieBucketHeads;
        private VaultArray<int> _underwaterDearLieBucketNext;
        private VaultArray<FloraDearLieClaim64> _surfaceDearLieClaims;
        private VaultArray<FloraDearLieClaim64> _underwaterDearLieClaims;
        private VaultArray<FloraDestructionEventDTO> _dearLieDamageEvents;
        private VaultArray<FloraDearLieDestructionResult> _dearLieResults;
        private VaultArray<FloraDearLieCounter64> _dearLieCounters;
        private VaultArray<FloraDearLieRegenRecord> _dearLieRegenRecords;
        private VaultArray<FloraDearLieTelemetryEntry> _dearLieTelemetryRing;
        private IDataVault _dearLieVault;
        private VaultUidMap<OrganicHalfMapEntry, Unity.Mathematics.half> _healthByInstanceUid;
        private VaultUidMap<OrganicByteMapEntry, byte> _destroyedByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _pendingWiltEndTimeByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _damageVisualProgressByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _decompositionStartTimeByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _regrowthProgressByInstanceUid;
        private VaultUidMap<OrganicFloat3MapEntry, float3> _regrowthPositionByInstanceUid;
        private VaultUidMap<OrganicHalfMapEntry, Unity.Mathematics.half> _maturationScaleByInstanceUid;
        private VaultUidMap<OrganicHalfMapEntry, Unity.Mathematics.half> _maturationYieldByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _nextSporeAcousticTimeByInstanceUid;
        private VaultUidMap<OrganicFloat2MapEntry, float2> _baseScaleByInstanceUid;
        private VaultUidMap<OrganicByteMapEntry, byte> _runtimeFlagsByInstanceUid;
        private VaultUidMap<OrganicFloatMapEntry, float> _lastOrganicTouchTimeByInstanceUid;
        private VaultUidMap<OrganicByteMapEntry, byte> _overgrownByInstanceUid;
        private VaultUidMap<OrganicByteMapEntry, byte> _rootMoundAppliedByInstanceUid;
        private VaultList<PersistentWorldDeltaRecord> _destroyedFloraScratch;
        private VaultList<PersistentWorldDeltaRecord> _floraStateOverrideScratch;
        private readonly PersistentWorldDeltaRecord[] _destroyedFloraPersistenceScratch = new PersistentWorldDeltaRecord[DefaultTrackedDestroyedCapacity]; // COLD ALLOC: managed persistence import mirror; avoids holding DataVault scratch locks with lifecycle mutation lanes.
        private readonly PersistentWorldDeltaRecord[] _floraStateOverridePersistenceScratch = new PersistentWorldDeltaRecord[DefaultTrackedHealthCapacity]; // COLD ALLOC: managed persistence import mirror; avoids DataVault scratch/lifecycle multi-lock during override sync.
        private VaultUidMap<OrganicHalfMapEntry, Unity.Mathematics.half> _persistedHealth01ByInstanceUid;
        private VaultUidMap<OrganicHalfMapEntry, Unity.Mathematics.half> _persistedHeightScale01ByInstanceUid;
        private VaultList<DestroyedOrganicEvent> _pendingYieldEvents;
        private VaultArray<DestroyedOrganicEvent> _yieldJobInput;
        private VaultArray<ItemDropData> _dropOutput;
        private VaultArray<int> _dropBudget;
        private VaultArray<HarvestableTemplate.RuntimeDescriptor> _templateDescriptors;
        private VaultArray<HarvestableTemplate.LootRuntimeEntry> _lootEntries;
        private VaultArray<EntropyYieldMaterialLutEntry> _yieldMaterialLut;
        private VaultArray<Vector3> _dropDebugScratch;
        private JobHandle _dearLieJobHandle;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private int _dearLieScheduledDamageCount;
        private int _dearLieJobScheduleFrame = -1;
        private double _dearLieJobStartTimeSeconds;
        private int _deferredYieldScheduleFrame = -1;
        private int _surfaceRevision = -1;
        private int _underwaterRevision = -1;
        private int _surfaceCount;
        private int _underwaterCount;
        private int _dearLieRegenCount;
        private int _dearLieTelemetryCursor;
        private int _dearLieLastDamageFrame = -1;
        private int _dearLieLastDestroyedCount;
        private int _dearLieLastVfxCount;
        private float _dearLieLastQualityWeight;
        private float _dearLieFallbackQualityWeight = 0.25f;
        private double _organicClockSeconds;
        private Vector3 _dearLieLastImpactRuntimePosition;
        private Vector3 _dearLieLastTargetRuntimePosition;
        private byte _dearLieHasLastDebugHit;
        private bool _tickRegistered;
        private bool _slowTickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _registeredPostSimulationDispatcher;
        private bool _originShiftListenerRegistered;
        private bool _dearLieJobScheduled;
        private bool _dearLieVaultReady;
        private bool _dearLieVaultJobGuardHeld;
        private bool _templateCacheReady;
        private bool _yieldMaterialLutReady;
        private ulong _dearLieVaultJobGuardMask;
        private IDataVault _dearLieVaultJobGuardVault;

        private BridgeMatrixLane _surfaceMatrices;
        private BridgeMetadataLane _surfaceMetadata;
        private BridgeTypeLane _surfaceTypes;
        private BridgeSemanticTypeLane _surfaceSemanticTypes;
        private BridgeMatrixLane _underwaterMatrices;
        private BridgeMetadataLane _underwaterMetadata;
        private BridgeTypeLane _underwaterTypes;
        private BridgeSemanticTypeLane _underwaterSemanticTypes;

        private int[] _templateIndexByMaterialClass;
        private int[] _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
        private HarvestableTemplate[] _descriptorHarvestTemplates = Array.Empty<HarvestableTemplate>();
        private byte[] _floraCategoryByDescriptorIndex = Array.Empty<byte>();
        private byte[] _audioMaterialByDescriptorIndex = Array.Empty<byte>();
        private float[] _growthTimeSecondsByDescriptorIndex = Array.Empty<float>();
        private byte[] _sporeAcousticEmitterByDescriptorIndex = Array.Empty<byte>();
        private AudioClip[] _sporeAcousticClipByDescriptorIndex = Array.Empty<AudioClip>();
        private float[] _sporePulseFrequencyByDescriptorIndex = Array.Empty<float>();
        private float[] _sporeAcousticVolumeByDescriptorIndex = Array.Empty<float>();
        private int _surfaceRegrowthVisualScanCursor;
        private int _underwaterRegrowthVisualScanCursor;
        private int _surfaceDecompositionVisualScanCursor;
        private int _underwaterDecompositionVisualScanCursor;
        private int _surfaceDamageVisualScanCursor;
        private int _underwaterDamageVisualScanCursor;
        private int _surfaceWiltVisualScanCursor;
        private int _underwaterWiltVisualScanCursor;
        private int _surfaceMatureSporeScanCursor;
        private int _underwaterMatureSporeScanCursor;
        private int _surfaceOvergrowthScanCursor;
        private int _underwaterOvergrowthScanCursor;
        private int _underwaterAllelopathicCoralScanCursor;
        private int _underwaterAllelopathicKelpScanCursor;
        private int _surfaceParasiteExposureScanCursor;
        private int _underwaterParasiteExposureScanCursor;
        private float _nextParasiteExposureSampleTime;
        private float _lastParasiteExposureSampleTime;
        private float _lastParasiteExposure01;
        private Vector3 _lastParasiteExposureQueryPosition;
        private IPlayerInventoryService _playerInventoryService;
        private PersistentWorldRegistry _persistentWorldRegistry;
        private IAudioService _audioService;
        private ISpatialAudioHarvestPlaybackSink _harvestAudioSink;
        // COLD ALLOC: DebrisSpawnSignal[16] - bounded Dear Lie debris signal staging flushed after Vault unlock - owner: DestructibleOrganicManager
        private readonly DebrisSpawnSignal[] _pendingDearLieDebrisSignals = new DebrisSpawnSignal[DearLieMaxPendingDebrisSignalsPerFrame];
        // COLD ALLOC: FloraDearLieTelemetryEntry[300] - crash-only dump snapshot copied under lock, serialized after unlock - owner: DestructibleOrganicManager
        private readonly FloraDearLieTelemetryEntry[] _dearLieTelemetryDumpSnapshot = new FloraDearLieTelemetryEntry[DearLieTelemetryFrameCount];
        // COLD ALLOC: bounded registry publish staging for cache-sync defoliant kills flushed after organic locks release - owner: DestructibleOrganicManager
        private readonly uint[] _cacheSyncDestroyedRegistryUids = new uint[MaxOrganicCacheSyncRegistryBatch];
        private readonly ulong[] _cacheSyncDestroyedRegistryHashes = new ulong[MaxOrganicCacheSyncRegistryBatch];
        private readonly Vector3[] _cacheSyncDestroyedRegistryPositions = new Vector3[MaxOrganicCacheSyncRegistryBatch];
        private readonly uint[] _staleDestroyedRegistryClearUids = new uint[16];
        private readonly uint[] _staleFloraStateRegistryClearUids = new uint[16];
        // COLD ALLOC: HarvestAudioEvent[8] - bounded VISUAL_SYNC audio queue for harvest transitions - owner: DestructibleOrganicManager
        private readonly HarvestAudioEvent[] _pendingHarvestAudioEvents = new HarvestAudioEvent[MaxPendingHarvestAudioEvents];
        // COLD ALLOC: SporeAcousticEvent[8] - bounded VISUAL_SYNC audio queue for mature spore pulses - owner: DestructibleOrganicManager
        private readonly SporeAcousticEvent[] _pendingSporeAcousticEvents = new SporeAcousticEvent[MaxPendingSporeAcousticEvents];
        private int _pendingDearLieDebrisSignalCount;
        private int _pendingDearLieDebrisOverflowCount;
        private int _pendingHarvestAudioEventCount;
        private int _pendingSporeAcousticEventCount;
        private bool _hotSwapRegistered;
        private bool _organicToolHitServiceRegistered;
        private bool _runtimeOwnerAborted;
        // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
        private CorpseResourceNodeRecord[] _corpseResourceNodes = Array.Empty<CorpseResourceNodeRecord>();
        private int _corpseResourceNodeCount;

        /// <summary>Currently enabled runtime organic entropy owner.</summary>
        public static DestructibleOrganicManager ActiveRuntimeInstance => _activeRuntimeInstance;

        public int DearLieRegenQueueCount => _dearLieRegenCount;
        public int DearLieLastDamageFrame => _dearLieLastDamageFrame;
        public int DearLieLastDestroyedCount => _dearLieLastDestroyedCount;
        public int DearLieLastVfxCount => _dearLieLastVfxCount;
        public int DearLieSurfaceInstanceCount => _surfaceCount;
        public int DearLieUnderwaterInstanceCount => _underwaterCount;
        public float DearLieQualityWeight => _dearLieLastQualityWeight;
        public float DearLieDamageRadiusEpsilon => ResolveDearLieQueryRadius();
        public float DearLieRegenerationDelayTuningSeconds => ResolveDearLieRegenerationDelaySeconds();
        public float DearLieQualityOverride => dearLieQualityOverride;

#if UNITY_EDITOR
        public void EditorSetDearLieTuning(float damageRadiusEpsilon, float regenerationDelaySeconds, float qualityOverride)
        {
            dearLieDamageRadiusEpsilon = math.clamp(
                math.select(DearLieQueryRadiusMeters, damageRadiusEpsilon, math.isfinite(damageRadiusEpsilon)),
                0.25f,
                8f);
            dearLieRegenerationDelaySeconds = math.clamp(
                math.select(DearLieRegenerationDelaySeconds, regenerationDelaySeconds, math.isfinite(regenerationDelaySeconds)),
                5f,
                900f);
            dearLieQualityOverride = math.clamp(
                math.select(-1f, qualityOverride, math.isfinite(qualityOverride)),
                -1f,
                1f);
        }

        public void EditorRequestDearLieMockBurst()
        {
            dearLieGenerateMockDamageBurst = true;
        }

        public int EditorCopyDearLieTelemetry(
            Span<int> frameIndices,
            Span<int> destroyedCounts,
            Span<int> vfxCounts,
            Span<int> regenCounts)
        {
            if (!_dearLieTelemetryRing.IsCreated || _dearLieTelemetryRing.Length == 0)
                return 0;

            int telemetryCursor = math.max(0, _dearLieTelemetryCursor);
            int copyCount = math.min(telemetryCursor, _dearLieTelemetryRing.Length);
            copyCount = math.min(copyCount, math.min(frameIndices.Length, math.min(destroyedCounts.Length, math.min(vfxCounts.Length, regenCounts.Length))));
            int start = telemetryCursor - copyCount;
            for (int i = 0; i < copyCount; i++)
            {
                int ringIndex = (start + i) % _dearLieTelemetryRing.Length;
                if (ringIndex < 0)
                    ringIndex += _dearLieTelemetryRing.Length;

                FloraDearLieTelemetryEntry entry = _dearLieTelemetryRing[ringIndex];
                frameIndices[i] = entry.FrameIndex;
                destroyedCounts[i] = entry.DestroyedCount;
                vfxCounts[i] = entry.VfxSignalCount;
                regenCounts[i] = entry.RegenQueuedCount;
            }

            return copyCount;
        }
#endif

        internal bool RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits)
        {
            return RegisterCorpseResourceNode(worldPosition, speciesId, capacityUnits, 0u);
        }

        internal bool RegisterCorpseResourceNode(Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition positionAup))
                return false;

            return RegisterCorpseResourceNode(in positionAup, worldPosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        internal bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits)
        {
            return RegisterCorpseResourceNode(in positionAup, speciesId, capacityUnits, 0u);
        }

        internal bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            double3 committedOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            Vector3 runtimePosition = (Vector3)AUPMath.ToRuntimeFloat3(in positionAup, committedOriginOffset);
            return RegisterCorpseResourceNode(in positionAup, runtimePosition, speciesId, capacityUnits, contaminatedItemHash);
        }

        private bool RegisterCorpseResourceNode(in AbsoluteUniversePosition positionAup, Vector3 worldPosition, int speciesId, float capacityUnits, uint contaminatedItemHash)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0 || capacityUnits <= 0f)
                return false;

            int writeIndex = -1;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                if (_corpseResourceNodes[i].Active != 0)
                    continue;

                writeIndex = i;
                break;
            }

            if (writeIndex < 0)
                writeIndex = FindWeakestCorpseNodeIndex();

            if (writeIndex < 0)
                return false;

            float initialUnits = Mathf.Max(0.25f, capacityUnits);
            float currentTime = ResolveOrganicClockSeconds();
            uint nodeId = ComputeUniqueCorpseNodeId(in positionAup, speciesId, writeIndex);
            if (nodeId == 0u)
                return false;

            CorpseResourceNodeRecord record = default;
            record.NodeId = nodeId;
            record.ContaminatedItemHash = contaminatedItemHash;
            record.SpeciesId = speciesId;
            record.PositionAup = positionAup;
            record.Position = worldPosition;
            record.InitialUnits = initialUnits;
            record.RemainingUnits = initialUnits;
            record.BloodIntensity = DefaultCorpseBloodIntensity;
            record.SpawnTime = currentTime;
            record.ExpireTime = currentTime + OrganicDecompositionDurationSeconds;
            record.Active = 1;
            _corpseResourceNodes[writeIndex] = record;
            if (writeIndex >= _corpseResourceNodeCount)
                _corpseResourceNodeCount = writeIndex + 1;

            ChemicalInfluenceGrid.QueueBloodScent(worldPosition, record.BloodIntensity);
            return true;
        }

        internal bool TryResolveNearestCorpseResourceNode(Vector3 worldPosition, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
            {
                corpsePosition = default;
                corpseNodeId = 0u;
                return false;
            }

            return TryResolveNearestCorpseResourceNode(in queryAup, searchRadius, out corpsePosition, out corpseNodeId);
        }

        internal bool TryResolveNearestCorpseResourceNode(in AbsoluteUniversePosition queryAup, float searchRadius, out Vector3 corpsePosition, out uint corpseNodeId)
        {
            corpsePosition = default;
            corpseNodeId = 0u;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double bestDistanceSq = (double)searchRadius * searchRadius;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                corpsePosition = record.Position;
                corpseNodeId = record.NodeId;
            }

            return corpseNodeId != 0u;
        }

        internal bool TryConsumeCorpseResourceNode(uint corpseNodeId, float consumeUnits)
        {
            if (corpseNodeId == 0u || consumeUnits <= 0f || _corpseResourceNodes == null)
                return false;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != corpseNodeId)
                    continue;

                record.RemainingUnits = Mathf.Max(0f, record.RemainingUnits - consumeUnits);
                if (record.RemainingUnits <= 0.001f)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                }
                else
                {
                    record.BloodIntensity = math.lerp(0.35f, DefaultCorpseBloodIntensity, ResolveCorpseCapacityFraction01(in record));
                }

                _corpseResourceNodes[i] = record;
                TrimTrailingCorpseNodes();
                return true;
            }

            return false;
        }

        internal bool TryResolveCorpseContaminatedItemHash(uint corpseNodeId, out uint itemHash)
        {
            itemHash = 0u;
            if (corpseNodeId == 0u || _corpseResourceNodes == null)
                return false;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != corpseNodeId)
                    continue;

                itemHash = record.ContaminatedItemHash;
                return itemHash != 0u;
            }

            return false;
        }

        internal float ResolveCorpseSpawnInfluence01(Vector3 worldPosition, float searchRadius)
        {
            if (!TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition queryAup))
                return 0f;

            return ResolveCorpseSpawnInfluence01(in queryAup, searchRadius);
        }

        internal float ResolveCorpseSpawnInfluence01(in AbsoluteUniversePosition queryAup, float searchRadius)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0 || searchRadius <= 0f)
                return 0f;

            double maxDistanceSq = (double)searchRadius * searchRadius;
            float bestInfluence01 = 0f;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.RemainingUnits <= 0f)
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > maxDistanceSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / maxDistanceSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                float influence01 = distance01 * mass01;
                if (influence01 > bestInfluence01)
                    bestInfluence01 = influence01;
            }

            return bestInfluence01;
        }

        internal bool TryResolveCorpseDiseaseExposure(
            in AbsoluteUniversePosition queryAup,
            float currentTimeSeconds,
            out float severity01,
            out Vector3 sourcePosition)
        {
            severity01 = 0f;
            sourcePosition = default;
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return false;

            double radiusSq = (double)CorpseDiseaseRadiusMeters * CorpseDiseaseRadiusMeters;
            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 ||
                    record.RemainingUnits <= 0f ||
                    currentTimeSeconds - record.SpawnTime < CorpseDiseaseActivationSeconds)
                {
                    continue;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in queryAup, in record.PositionAup);
                if (distanceSq > radiusSq)
                    continue;

                float distance01 = 1f - math.saturate((float)(distanceSq / radiusSq));
                float mass01 = ResolveCorpseCapacityFraction01(in record);
                severity01 = Mathf.Max(severity01, distance01 * mass01 * CorpseDiseaseSeverity);
                sourcePosition = record.Position;
            }

            return severity01 > 0.001f;
        }

        private void Awake()
        {
            if (Application.isPlaying && TryAbortForUsableExistingRuntime())
                return;

            _activeRuntimeInstance = this;
            _surfaceMatrices = new BridgeMatrixLane(this, underwater: false);
            _surfaceMetadata = new BridgeMetadataLane(this, underwater: false);
            _surfaceTypes = new BridgeTypeLane(this, underwater: false);
            _surfaceSemanticTypes = new BridgeSemanticTypeLane(this, underwater: false);
            _underwaterMatrices = new BridgeMatrixLane(this, underwater: true);
            _underwaterMetadata = new BridgeMetadataLane(this, underwater: true);
            _underwaterTypes = new BridgeTypeLane(this, underwater: true);
            _underwaterSemanticTypes = new BridgeSemanticTypeLane(this, underwater: true);

            if (vegetationBridge == null)
                TryGetComponent(out vegetationBridge);

            if (floraInteractionManager == null)
                TryGetComponent(out floraInteractionManager);

            hitSearchRadius = Mathf.Max(MinimumSearchRadius, hitSearchRadius);
            kelpHeightTolerance = Mathf.Max(0.05f, kelpHeightTolerance);
            interactionBurstRadius = Mathf.Max(0.05f, interactionBurstRadius);
            allelopathicCellRadius = Mathf.Max(1f, allelopathicCellRadius);
            allelopathicKelpCapacity = Mathf.Max(1, allelopathicKelpCapacity);
            allelopathicThreshold01 = Mathf.Clamp(allelopathicThreshold01, 0.5f, 1f);
            harvestAudioBaseVolume = Mathf.Clamp01(harvestAudioBaseVolume);
            sporeAcousticFallbackVolume = Mathf.Clamp01(sporeAcousticFallbackVolume);
            sporeAcousticMinimumIntervalSeconds = Mathf.Max(0.05f, sporeAcousticMinimumIntervalSeconds);
            matureSporeAcousticScanBudgetPerTick = Mathf.Max(1, matureSporeAcousticScanBudgetPerTick);

            // COLD ALLOC: CorpseResourceNodeRecord[96] - bounded ecological corpse-resource nodes used by scavenger AI and blood-scent routing - owner: DestructibleOrganicManager
            _corpseResourceNodes = new CorpseResourceNodeRecord[DefaultCorpseNodeCapacity];
            _corpseResourceNodeCount = 0;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            _activeRuntimeInstance = this;
            if (!TryRegisterOrganicToolHitService())
                return;

            CacheRegistryServicesCold();
            TryBootstrapDearLieVault(clearExisting: true);
            EnsureOrganicVaultBuffers(clearExisting: true);
            BuildTemplateCaches();
            BuildYieldMaterialLut();
            TryRegisterHotSwapListener();
            RegisterOriginShiftListener();

            if (GlobalRegistry.Dispatcher == null)
                return;

            TryRegisterDispatcherPhases();
            if (!_registeredPostSimulationDispatcher)
                return;

            TryRegisterTickLanes();

            SyncDestroyedFloraFromPersistence();
            SyncFloraStateOverridesFromPersistence();
            BuildFloraTemplateHarvestMap();
            RefreshActiveCachesIfNeeded(force: true);
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(_activeRuntimeInstance, this))
                    _activeRuntimeInstance = null;
                return;
            }

            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;

            TryUnregisterTickLanes();
            TryUnregisterDispatcherPhases();

            CompleteDearLieJobForLifecycleBarrier();
            UnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregisterOrganicToolHitService();
            ReleaseOrganicVaultBuffers(_dearLieVault);
            ReleaseDearLieVaultBuffers(_dearLieVault);
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(_activeRuntimeInstance, this))
                    _activeRuntimeInstance = null;
                return;
            }

            if (_activeRuntimeInstance == this)
                _activeRuntimeInstance = null;

            TryUnregisterTickLanes();
            TryUnregisterDispatcherPhases();
            CompleteDearLieJobForLifecycleBarrier();
            UnregisterOriginShiftListener();
            TryUnregisterHotSwapListener();
            TryUnregisterOrganicToolHitService();
            ReleaseOrganicVaultBuffers(_dearLieVault);
            ReleaseDearLieVaultBuffers(_dearLieVault);
            ClearCachedRegistryServices();
        }

        /// <summary>
        /// Rebuilds live corpse attractor runtime caches from authoritative Absolute Universe Positions after a committed origin shift.
        /// </summary>
        /// <param name="shiftData">Committed floating-origin shift data.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFiniteVector(shiftOffset) ||
                !math.isfinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.000001f ||
                !math.all(math.isfinite(shiftData.NewTotalOffsetDouble)))
            {
                return;
            }

            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            double3 committedOriginOffset = shiftData.NewTotalOffsetDouble;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                float3 runtimePosition = AUPMath.ToRuntimeFloat3(in record.PositionAup, committedOriginOffset);
                Vector3 resolvedRuntimePosition = ToRuntimeVector3(runtimePosition);
                if (!IsFiniteVector(resolvedRuntimePosition))
                    continue;

                record.Position = resolvedRuntimePosition;
                _corpseResourceNodes[i] = record;
            }
        }

        private void TryRegisterDispatcherPhases()
        {
            if (_registeredPostSimulationDispatcher)
                return;

            if (_postSimulationPhase == null)
                _postSimulationPhase = new PostSimulationPhaseSystem(this); // COLD ALLOC: IDispatcherSystem[1] - organic truth post-simulation fence bridge - owner: DestructibleOrganicManager

            _registeredPostSimulationDispatcher = GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase);
        }

        private void TryUnregisterDispatcherPhases()
        {
            if (!_registeredPostSimulationDispatcher)
                return;

            GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
            _registeredPostSimulationDispatcher = false;
        }

        private void TryRegisterTickLanes()
        {
            if (!_registeredPostSimulationDispatcher)
                return;

            if (!_tickRegistered)
                _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_slowTickRegistered)
                _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);

            if (!_lateFrameTickRegistered)
                _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTickLanes()
        {
            if (_tickRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _tickRegistered = false;
            }

            if (_slowTickRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _slowTickRegistered = false;
            }

            if (_lateFrameTickRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameTickRegistered = false;
            }
        }

        private void RegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void UnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as PersistentWorldRegistry;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterTickLanes();
                    TryUnregisterDispatcherPhases();
                    TryRegisterDispatcherPhases();
                    if (_registeredPostSimulationDispatcher)
                        TryRegisterTickLanes();
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault currentVault = currentService as IDataVault;
                    if (currentVault != null && ReferenceEquals(_dearLieVault, currentVault))
                    {
                        TryBootstrapDearLieVault(clearExisting: false);
                        EnsureOrganicVaultBuffers(clearExisting: false);
                        break;
                    }

                    if (_dearLieVault != null)
                    {
                        ReleaseOrganicVaultBuffers(_dearLieVault);
                        ReleaseDearLieVaultBuffers(_dearLieVault);
                    }

                    _dearLieVault = currentVault;
                    _dearLieVaultReady = false;
                    if (_dearLieVault != null)
                    {
                        TryBootstrapDearLieVault(clearExisting: true);
                        EnsureOrganicVaultBuffers(clearExisting: true);
                        BuildTemplateCaches();
                        BuildYieldMaterialLut();
                    }
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private bool TryRegisterOrganicToolHitService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_organicToolHitServiceRegistered || !Application.isPlaying)
                return _organicToolHitServiceRegistered || !Application.isPlaying;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IOrganicToolHitService registered = GlobalRegistry.OrganicToolHits;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                DestructibleOrganicManager staleManager = registered as DestructibleOrganicManager;
                if (ReferenceEquals(staleManager, null))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                GlobalRegistry.UnregisterOrganicToolHitService(registered);
                staleManager._organicToolHitServiceRegistered = false;
                if (ReferenceEquals(_activeRuntimeInstance, staleManager))
                    _activeRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterOrganicToolHitService(this);
            _organicToolHitServiceRegistered = ReferenceEquals(GlobalRegistry.OrganicToolHits, this);
            if (!_organicToolHitServiceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            _runtimeOwnerAborted = false;
            return true;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            DestructibleOrganicManager active = _activeRuntimeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsDestructibleOrganicRuntimeUsable(active))
                {
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                _activeRuntimeInstance = null;
            }

            IOrganicToolHitService registeredService = GlobalRegistry.OrganicToolHits;
            if (ReferenceEquals(registeredService, null) || ReferenceEquals(registeredService, this))
                return false;

            if (IsOrganicToolHitServiceUsable(registeredService))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            DestructibleOrganicManager staleManager = registeredService as DestructibleOrganicManager;
            if (!ReferenceEquals(staleManager, null))
            {
                GlobalRegistry.UnregisterOrganicToolHitService(registeredService);
                staleManager._organicToolHitServiceRegistered = false;
                if (ReferenceEquals(_activeRuntimeInstance, staleManager))
                    _activeRuntimeInstance = null;
            }

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            _runtimeOwnerAborted = true;
            if (ReferenceEquals(_activeRuntimeInstance, this))
                _activeRuntimeInstance = null;
            enabled = false;
        }

        private static bool IsOrganicToolHitServiceUsable(IOrganicToolHitService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            DestructibleOrganicManager manager = service as DestructibleOrganicManager;
            return ReferenceEquals(manager, null) ||
                   (manager._organicToolHitServiceRegistered &&
                    IsDestructibleOrganicRuntimeUsable(manager));
        }


        /// <summary>
        /// Resolve-or-create the sole DestructibleOrganicManager runtime owner for
        /// GlobalRegistry.OrganicToolHits (indirect-flora harvest / tool-hit service).
        /// Script GUID e21070ca5e8272b4aa0678faa365a3e1 has ZERO live scene/prefab hits.
        /// No Ensure existed; OnEnable only registers when already present.
        /// Tool-hit consumers and flora harvest sinks hit permanent null without this path.
        /// </summary>
        public static DestructibleOrganicManager EnsureRuntimeInstance()
        {
            DestructibleOrganicManager active = _activeRuntimeInstance;
            if (IsDestructibleOrganicRuntimeUsable(active))
                return active;

            IOrganicToolHitService registered = GlobalRegistry.OrganicToolHits;
            DestructibleOrganicManager registeredManager = registered as DestructibleOrganicManager;
            if (IsDestructibleOrganicRuntimeUsable(registeredManager))
                return registeredManager;

            if (!ReferenceEquals(registered, null))
            {
                GlobalRegistry.UnregisterOrganicToolHitService(registered);
                if (!ReferenceEquals(registeredManager, null))
                    registeredManager._organicToolHitServiceRegistered = false;
            }

            if (!ReferenceEquals(active, null) && active == null)
                _activeRuntimeInstance = null;

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Sole OrganicToolHits owner; must construct when bootstrap reorders.
            GameObject runtimeRoot = new GameObject("[DestructibleOrganicManager]"); // COLD ALLOC
            return runtimeRoot.AddComponent<DestructibleOrganicManager>();
        }

        private static bool IsDestructibleOrganicRuntimeUsable(DestructibleOrganicManager manager)
        {
            return manager != null &&
                   ReferenceEquals(_activeRuntimeInstance, manager) &&
                   manager.isActiveAndEnabled &&
                   !manager._runtimeOwnerAborted;
        }

        private void TryUnregisterOrganicToolHitService()
        {
            if (!_organicToolHitServiceRegistered)
                return;

            GlobalRegistry.UnregisterOrganicToolHitService(this);
            _organicToolHitServiceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _dearLieVault = GlobalRegistry.DataVault;
            CacheAudioService(GlobalRegistry.Audio);
            CacheDearLieFallbackQualityWeightCold();
        }

        private bool TryBootstrapDearLieVault(bool clearExisting)
        {
            IDataVault vault = _dearLieVault;
            if (vault == null)
            {
                _dearLieVaultReady = false;
                return false;
            }

            _dearLieVaultReady = EnsureDearLieVaultBuffers(vault, clearExisting) && TryResolveDearLieVaultBuffers(vault);
            if (clearExisting && _dearLieVaultReady)
                ClearDearLieVaultRuntimeState();

            return _dearLieVaultReady;
        }

        private bool EnsureOrganicVaultBuffers(bool clearExisting)
        {
            IDataVault vault = _dearLieVault;
            if (vault == null)
                return false;

            NativeArrayOptions options = clearExisting ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory;
            bool ok =
                _healthByInstanceUid.Ensure(vault, OrganicHealthByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _destroyedByInstanceUid.Ensure(vault, OrganicDestroyedByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _pendingWiltEndTimeByInstanceUid.Ensure(vault, OrganicPendingWiltEndTimeByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _damageVisualProgressByInstanceUid.Ensure(vault, OrganicDamageVisualProgressByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _decompositionStartTimeByInstanceUid.Ensure(vault, OrganicDecompositionStartTimeByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _regrowthProgressByInstanceUid.Ensure(vault, OrganicRegrowthProgressByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _regrowthPositionByInstanceUid.Ensure(vault, OrganicRegrowthPositionByUidBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _maturationScaleByInstanceUid.Ensure(vault, OrganicMaturationScaleByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _maturationYieldByInstanceUid.Ensure(vault, OrganicMaturationYieldByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _nextSporeAcousticTimeByInstanceUid.Ensure(vault, OrganicNextSporeAcousticTimeByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _baseScaleByInstanceUid.Ensure(vault, OrganicBaseScaleByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _runtimeFlagsByInstanceUid.Ensure(vault, OrganicRuntimeFlagsByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _lastOrganicTouchTimeByInstanceUid.Ensure(vault, OrganicLastTouchTimeByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _overgrownByInstanceUid.Ensure(vault, OrganicOvergrownByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _rootMoundAppliedByInstanceUid.Ensure(vault, OrganicRootMoundAppliedByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _destroyedFloraScratch.Ensure(vault, OrganicDestroyedFloraScratchBufferId, DefaultTrackedDestroyedCapacity, OrganicVaultSystemId, options) &&
                _floraStateOverrideScratch.Ensure(vault, OrganicFloraStateOverrideScratchBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _persistedHealth01ByInstanceUid.Ensure(vault, OrganicPersistedHealth01ByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _persistedHeightScale01ByInstanceUid.Ensure(vault, OrganicPersistedHeightScale01ByUidBufferId, DefaultTrackedHealthCapacity, OrganicVaultSystemId, options) &&
                _pendingYieldEvents.Ensure(vault, OrganicPendingYieldEventsBufferId, DefaultPendingYieldCapacity, OrganicVaultSystemId, options) &&
                _yieldJobInput.Ensure(vault, OrganicYieldJobInputBufferId, DefaultPendingYieldCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory) &&
                _dropOutput.Ensure(vault, OrganicDropOutputBufferId, DefaultDropBufferCapacity, OrganicVaultSystemId, NativeArrayOptions.ClearMemory) &&
                _dropBudget.Ensure(vault, OrganicDropBudgetBufferId, DropBudgetLength, OrganicVaultSystemId, NativeArrayOptions.ClearMemory) &&
                _dropDebugScratch.Ensure(vault, OrganicDropDebugScratchBufferId, 1, OrganicVaultSystemId, NativeArrayOptions.ClearMemory);

            if (clearExisting)
            {
                _healthByInstanceUid.Clear();
                _destroyedByInstanceUid.Clear();
                _pendingWiltEndTimeByInstanceUid.Clear();
                _damageVisualProgressByInstanceUid.Clear();
                _decompositionStartTimeByInstanceUid.Clear();
                _regrowthProgressByInstanceUid.Clear();
                _regrowthPositionByInstanceUid.Clear();
                _maturationScaleByInstanceUid.Clear();
                _maturationYieldByInstanceUid.Clear();
                _nextSporeAcousticTimeByInstanceUid.Clear();
                _baseScaleByInstanceUid.Clear();
                _runtimeFlagsByInstanceUid.Clear();
                _lastOrganicTouchTimeByInstanceUid.Clear();
                _overgrownByInstanceUid.Clear();
                _rootMoundAppliedByInstanceUid.Clear();
                _destroyedFloraScratch.Clear();
                _floraStateOverrideScratch.Clear();
                _persistedHealth01ByInstanceUid.Clear();
                _persistedHeightScale01ByInstanceUid.Clear();
                _pendingYieldEvents.Clear();
                _dropOutput.Clear();
                ResetDropOutputBudget();
            }

            return ok;
        }

        private void ReleaseOrganicVaultBuffers(IDataVault vault)
        {
            if (vault == null)
                return;

            _surfaceInstanceUids.Release();
            _underwaterInstanceUids.Release();
            _surfaceMaterialClasses.Release();
            _underwaterMaterialClasses.Release();
            _surfaceHealth.Release();
            _underwaterHealth.Release();
            _healthByInstanceUid.Release();
            _destroyedByInstanceUid.Release();
            _pendingWiltEndTimeByInstanceUid.Release();
            _damageVisualProgressByInstanceUid.Release();
            _decompositionStartTimeByInstanceUid.Release();
            _regrowthProgressByInstanceUid.Release();
            _regrowthPositionByInstanceUid.Release();
            _maturationScaleByInstanceUid.Release();
            _maturationYieldByInstanceUid.Release();
            _nextSporeAcousticTimeByInstanceUid.Release();
            _baseScaleByInstanceUid.Release();
            _runtimeFlagsByInstanceUid.Release();
            _lastOrganicTouchTimeByInstanceUid.Release();
            _overgrownByInstanceUid.Release();
            _rootMoundAppliedByInstanceUid.Release();
            _destroyedFloraScratch.Release();
            _floraStateOverrideScratch.Release();
            _persistedHealth01ByInstanceUid.Release();
            _persistedHeightScale01ByInstanceUid.Release();
            _pendingYieldEvents.Release();
            _yieldJobInput.Release();
            _dropOutput.Release();
            _dropBudget.Release();
            _templateDescriptors.Release();
            _lootEntries.Release();
            _yieldMaterialLut.Release();
            _dropDebugScratch.Release();
            _templateCacheReady = false;
            _yieldMaterialLutReady = false;
        }

        private bool TryResolveVegetationBridgePayload(
            bool underwater,
            out NativeArray<Matrix4x4> matrices,
            out NativeArray<HectonVegetationInstanceData> metadata,
            out NativeArray<int> types,
            out NativeArray<int>.ReadOnly semanticTypes,
            out int count,
            out int semanticCount)
        {
            matrices = default;
            metadata = default;
            types = default;
            semanticTypes = default;
            count = 0;
            semanticCount = 0;

            HectonMapMagicVegetationBridge bridge = vegetationBridge;
            if (bridge == null)
                return false;

            bool nativeOk;
            bool semanticOk;
            if (underwater)
            {
                nativeOk = bridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count);
                semanticOk = bridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount);
            }
            else
            {
                nativeOk = bridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                semanticOk = bridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            }

            return nativeOk &&
                   semanticOk &&
                   count > 0 &&
                   semanticCount >= count &&
                   matrices.IsCreated &&
                   metadata.IsCreated &&
                   types.IsCreated &&
                   matrices.Length >= count &&
                   metadata.Length >= count &&
                   types.Length >= count &&
                   semanticTypes.Length >= count;
        }

        private bool EnsureDearLieVaultBuffers(IDataVault vault, bool clearExisting)
        {
            if (vault == null)
                return false;

            NativeArrayOptions fixedOptions = clearExisting ? NativeArrayOptions.ClearMemory : NativeArrayOptions.UninitializedMemory;
            return _surfaceDearLieClaims.Ensure(vault, DearLieSurfaceClaimsBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions) &&
                   _underwaterDearLieClaims.Ensure(vault, DearLieUnderwaterClaimsBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions) &&
                   _dearLieDamageEvents.Ensure(vault, DearLieDamageEventsBufferId, DearLieMaxDamageSignalsPerFrame, OrganicVaultSystemId, fixedOptions) &&
                   _dearLieResults.Ensure(vault, DearLieResultsBufferId, DearLieMaxResultsPerFrame, OrganicVaultSystemId, fixedOptions) &&
                   _dearLieCounters.Ensure(vault, DearLieCountersBufferId, 8, OrganicVaultSystemId, NativeArrayOptions.ClearMemory) &&
                   _dearLieRegenRecords.Ensure(vault, DearLieRegenRecordsBufferId, DearLieMaxRegenRecords, OrganicVaultSystemId, fixedOptions) &&
                   _dearLieTelemetryRing.Ensure(vault, DearLieTelemetryRingBufferId, DearLieTelemetryFrameCount, OrganicVaultSystemId, NativeArrayOptions.ClearMemory) &&
                   _surfaceDearLieBucketHeads.Ensure(vault, DearLieSurfaceBucketHeadsBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions) &&
                   _surfaceDearLieBucketNext.Ensure(vault, DearLieSurfaceBucketNextBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions) &&
                   _underwaterDearLieBucketHeads.Ensure(vault, DearLieUnderwaterBucketHeadsBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions) &&
                   _underwaterDearLieBucketNext.Ensure(vault, DearLieUnderwaterBucketNextBufferId, DearLieSpatialHashCapacity, OrganicVaultSystemId, fixedOptions);
        }

        private bool TryResolveDearLieVaultBuffers(IDataVault vault)
        {
            return vault != null &&
                   _surfaceDearLieClaims.Length >= DearLieSpatialHashCapacity &&
                   _underwaterDearLieClaims.Length >= DearLieSpatialHashCapacity &&
                   _dearLieDamageEvents.Length >= DearLieMaxDamageSignalsPerFrame &&
                   _dearLieResults.Length >= DearLieMaxResultsPerFrame &&
                   _dearLieCounters.Length >= 8 &&
                   _dearLieRegenRecords.Length >= DearLieMaxRegenRecords &&
                   _dearLieTelemetryRing.Length >= DearLieTelemetryFrameCount &&
                   _surfaceDearLieBucketHeads.Length >= DearLieSpatialHashCapacity &&
                   _surfaceDearLieBucketNext.Length >= DearLieSpatialHashCapacity &&
                   _underwaterDearLieBucketHeads.Length >= DearLieSpatialHashCapacity &&
                   _underwaterDearLieBucketNext.Length >= DearLieSpatialHashCapacity;
        }

        private bool EnsureDearLieVaultLaneCapacity(bool underwater, int requiredCount)
        {
            if (requiredCount <= 0)
                return true;

            IDataVault vault = _dearLieVault;
            if (vault == null || !_dearLieVaultReady || _dearLieJobScheduled)
                return false;

            int requiredCapacity = math.max(DearLieSpatialHashCapacity, math.ceilpow2(requiredCount));
            if (underwater)
            {
                return _underwaterDearLieClaims.Ensure(vault, DearLieUnderwaterClaimsBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory) &&
                       _underwaterDearLieBucketHeads.Ensure(vault, DearLieUnderwaterBucketHeadsBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory) &&
                       _underwaterDearLieBucketNext.Ensure(vault, DearLieUnderwaterBucketNextBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory);
            }

            return _surfaceDearLieClaims.Ensure(vault, DearLieSurfaceClaimsBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory) &&
                   _surfaceDearLieBucketHeads.Ensure(vault, DearLieSurfaceBucketHeadsBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory) &&
                   _surfaceDearLieBucketNext.Ensure(vault, DearLieSurfaceBucketNextBufferId, requiredCapacity, OrganicVaultSystemId, NativeArrayOptions.UninitializedMemory);
        }

        private void ClearDearLieVaultRuntimeState()
        {
            ClearDearLieCounters();
            if (_dearLieTelemetryRing.IsCreated)
            {
                for (int i = 0; i < _dearLieTelemetryRing.Length; i++)
                    _dearLieTelemetryRing[i] = default;
            }

            if (_surfaceDearLieBucketHeads.IsCreated)
            {
                for (int i = 0; i < _surfaceDearLieBucketHeads.Length; i++)
                    _surfaceDearLieBucketHeads[i] = -1;
            }

            if (_underwaterDearLieBucketHeads.IsCreated)
            {
                for (int i = 0; i < _underwaterDearLieBucketHeads.Length; i++)
                    _underwaterDearLieBucketHeads[i] = -1;
            }

            _dearLieRegenCount = 0;
            _dearLieTelemetryCursor = 0;
        }

        private bool TryAcquireDearLieVaultJobGuard()
        {
            IDataVault vault = _dearLieVault;
            if (vault == null || _dearLieVaultJobGuardHeld || _dearLieVaultJobGuardMask != 0UL)
                return false;

            ulong guardMask = ResolveDearLieVaultJobGuardMask();
            if (guardMask == 0UL || !vault.TryAcquireMutationGuard(guardMask))
                return false;

            _dearLieVaultJobGuardMask = guardMask;
            _dearLieVaultJobGuardVault = vault;
            _dearLieVaultJobGuardHeld = true;
            return true;
        }

        private static ulong ResolveDearLieVaultJobGuardMask()
        {
            ulong guardMask = 0UL;
            for (int i = 0; i < DearLieVaultJobBufferCount; i++)
            {
                BufferID bufferId = GetDearLieVaultJobBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private void ReleaseDearLieVaultJobGuard()
        {
            IDataVault vault = _dearLieVaultJobGuardVault;
            ulong guardMask = _dearLieVaultJobGuardMask;
            _dearLieVaultJobGuardMask = 0UL;
            _dearLieVaultJobGuardVault = null;
            _dearLieVaultJobGuardHeld = false;
            ReleaseOrganicGuard(vault, guardMask);
        }

        private static BufferID GetDearLieVaultJobBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return DearLieSurfaceClaimsBufferId;
                case 1:
                    return DearLieUnderwaterClaimsBufferId;
                case 2:
                    return DearLieDamageEventsBufferId;
                case 3:
                    return DearLieResultsBufferId;
                case 4:
                    return DearLieCountersBufferId;
                case 5:
                    return DearLieRegenRecordsBufferId;
                case 6:
                    return DearLieSurfaceBucketHeadsBufferId;
                case 7:
                    return DearLieSurfaceBucketNextBufferId;
                case 8:
                    return DearLieUnderwaterBucketHeadsBufferId;
                case 9:
                    return DearLieUnderwaterBucketNextBufferId;
                case 10:
                    return OrganicSurfaceInstanceUidsBufferId;
                case 11:
                    return OrganicUnderwaterInstanceUidsBufferId;
                case 12:
                    return OrganicSurfaceMaterialClassesBufferId;
                case 13:
                    return OrganicUnderwaterMaterialClassesBufferId;
                case 14:
                    return OrganicSurfaceHealthBufferId;
                case 15:
                    return OrganicUnderwaterHealthBufferId;
                case 16:
                    return OrganicHealthByUidBufferId;
                case 17:
                    return OrganicDestroyedByUidBufferId;
                case 18:
                    return OrganicPendingWiltEndTimeByUidBufferId;
                case 19:
                    return OrganicDamageVisualProgressByUidBufferId;
                case 20:
                    return OrganicDecompositionStartTimeByUidBufferId;
                case 21:
                    return OrganicRegrowthProgressByUidBufferId;
                case 22:
                    return OrganicRegrowthPositionByUidBufferId;
                case 23:
                    return OrganicRuntimeFlagsByUidBufferId;
                case 24:
                    return OrganicLastTouchTimeByUidBufferId;
                case 25:
                    return OrganicOvergrownByUidBufferId;
                case 26:
                    return OrganicRootMoundAppliedByUidBufferId;
                case 27:
                    return OrganicBaseScaleByUidBufferId;
                case 28:
                    return OrganicPersistedHealth01ByUidBufferId;
                case 29:
                    return OrganicPersistedHeightScale01ByUidBufferId;
                case 30:
                    return OrganicMaturationScaleByUidBufferId;
                case 31:
                    return OrganicMaturationYieldByUidBufferId;
                case 32:
                    return OrganicNextSporeAcousticTimeByUidBufferId;
                default:
                    return default;
            }
        }

        private static bool TryAcquireOrganicBufferGuard(IDataVault vault, BufferID bufferId, out ulong guardMask)
        {
            guardMask = OrganicMutationGuardBit(bufferId);
            return vault != null && guardMask != 0UL && vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseOrganicGuard(IDataVault vault, ulong guardMask)
        {
            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private static ulong OrganicMutationGuardBit(BufferID bufferId)
        {
            return bufferId == default ? 0UL : 1UL << ((int)bufferId & 31);
        }

        private bool TryAcquireOrganicLifecycleMutationGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;

            int regrowthMask = 0;
            for (int i = 0; i < OrganicRegrowthMutationBufferCount; i++)
            {
                if (ShouldLockOrganicRegrowthMutationBuffer(i))
                    regrowthMask |= 1 << i;
            }

            int maturationMask = 0;
            for (int i = 0; i < OrganicMaturationMutationBufferCount; i++)
            {
                if (ShouldLockOrganicMaturationMutationBuffer(i))
                    maturationMask |= 1 << i;
            }

            ulong guardMask =
                ResolveOrganicRegrowthMutationGuardMask(regrowthMask) |
                ResolveOrganicMaturationMutationGuardMask(maturationMask);
            if (guardMask == 0UL)
                return true;

            if (vault == null || !vault.TryAcquireMutationGuard(guardMask))
                return false;

            lockedMask = regrowthMask | (maturationMask << OrganicRegrowthMutationBufferCount);
            return true;
        }

        private uint ComputeUniqueCorpseNodeId(in AbsoluteUniversePosition positionAup, int speciesId, int replacementIndex)
        {
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in positionAup);
            uint candidate = ComputeNonZeroCorpseNodeId(tombstoneId);
            uint salt = unchecked((uint)speciesId * 2246822519u);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                if (!HasActiveCorpseNodeIdCollision(candidate, in positionAup, replacementIndex))
                    return candidate;

                candidate = MixDearLieHash(
                    candidate ^ (uint)(tombstoneId >> 32),
                    unchecked(salt ^ ((uint)(attempt + 1) * 0x9E3779B9u)));
                if (candidate == 0u)
                    candidate = 0xC0A5E001u ^ (uint)(attempt + 1);
            }

            return HasActiveCorpseNodeIdCollision(candidate, in positionAup, replacementIndex) ? 0u : candidate;
        }

        private static uint ComputeNonZeroCorpseNodeId(ulong tombstoneId)
        {
            uint candidate = (uint)tombstoneId;
            if (candidate != 0u)
                return candidate;

            candidate = (uint)(tombstoneId >> 32);
            return candidate != 0u ? candidate : 0xC0A5E001u;
        }

        private bool HasActiveCorpseNodeIdCollision(uint nodeId, in AbsoluteUniversePosition positionAup, int replacementIndex)
        {
            if (nodeId == 0u || _corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return nodeId == 0u;

            int safeCount = math.min(_corpseResourceNodeCount, _corpseResourceNodes.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (i == replacementIndex)
                    continue;

                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0 || record.NodeId != nodeId)
                    continue;

                if (AbsoluteUniversePosition.DistanceSq(in record.PositionAup, in positionAup) <= CorpseNodeIdCollisionDistanceSq)
                    continue;

                return true;
            }

            return false;
        }

        private static void ReleaseOrganicLifecycleMutationGuard(IDataVault vault, int lockedMask)
        {
            int regrowthMask = lockedMask & ((1 << OrganicRegrowthMutationBufferCount) - 1);
            int maturationMask = (lockedMask >> OrganicRegrowthMutationBufferCount) & ((1 << OrganicMaturationMutationBufferCount) - 1);
            ReleaseOrganicGuard(
                vault,
                ResolveOrganicRegrowthMutationGuardMask(regrowthMask) |
                ResolveOrganicMaturationMutationGuardMask(maturationMask));
        }

        private bool TryAcquireOrganicRegrowthMutationGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;
            if (vault == null)
                return false;

            ulong guardMask = 0UL;
            for (int i = 0; i < OrganicRegrowthMutationBufferCount; i++)
            {
                if (!ShouldLockOrganicRegrowthMutationBuffer(i))
                    continue;

                BufferID bufferId = GetOrganicRegrowthMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
                lockedMask |= 1 << i;
            }

            if (guardMask == 0UL)
                return true;

            if (vault.TryAcquireMutationGuard(guardMask))
                return true;

            lockedMask = 0;
            return false;
        }

        private static void ReleaseOrganicRegrowthMutationGuard(IDataVault vault, int lockedMask)
        {
            ReleaseOrganicGuard(vault, ResolveOrganicRegrowthMutationGuardMask(lockedMask));
        }

        private static ulong ResolveOrganicRegrowthMutationGuardMask(int lockedMask)
        {
            ulong guardMask = 0UL;
            for (int i = OrganicRegrowthMutationBufferCount - 1; i >= 0; i--)
            {
                if ((lockedMask & (1 << i)) == 0)
                    continue;

                BufferID bufferId = GetOrganicRegrowthMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private bool ShouldLockOrganicRegrowthMutationBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    return _surfaceHealth.IsCreated;
                case 1:
                    return _underwaterHealth.IsCreated;
                case 2:
                    return _healthByInstanceUid.IsCreated;
                case 3:
                    return _destroyedByInstanceUid.IsCreated;
                case 4:
                    return _pendingWiltEndTimeByInstanceUid.IsCreated;
                case 5:
                    return _damageVisualProgressByInstanceUid.IsCreated;
                case 6:
                    return _decompositionStartTimeByInstanceUid.IsCreated;
                case 7:
                    return _regrowthProgressByInstanceUid.IsCreated;
                case 8:
                    return _regrowthPositionByInstanceUid.IsCreated;
                case 9:
                    return _runtimeFlagsByInstanceUid.IsCreated;
                case 10:
                    return _lastOrganicTouchTimeByInstanceUid.IsCreated;
                case 11:
                    return _overgrownByInstanceUid.IsCreated;
                case 12:
                    return _rootMoundAppliedByInstanceUid.IsCreated;
                case 13:
                    return _baseScaleByInstanceUid.IsCreated;
                case 14:
                    return _persistedHealth01ByInstanceUid.IsCreated;
                case 15:
                    return _persistedHeightScale01ByInstanceUid.IsCreated;
                case 16:
                    return _surfaceInstanceUids.IsCreated;
                case 17:
                    return _underwaterInstanceUids.IsCreated;
                case 18:
                    return _surfaceMaterialClasses.IsCreated;
                case 19:
                    return _underwaterMaterialClasses.IsCreated;
                case 20:
                    return _templateDescriptors.IsCreated;
                default:
                    return false;
            }
        }

        private static BufferID GetOrganicRegrowthMutationBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicSurfaceHealthBufferId;
                case 1:
                    return OrganicUnderwaterHealthBufferId;
                case 2:
                    return OrganicHealthByUidBufferId;
                case 3:
                    return OrganicDestroyedByUidBufferId;
                case 4:
                    return OrganicPendingWiltEndTimeByUidBufferId;
                case 5:
                    return OrganicDamageVisualProgressByUidBufferId;
                case 6:
                    return OrganicDecompositionStartTimeByUidBufferId;
                case 7:
                    return OrganicRegrowthProgressByUidBufferId;
                case 8:
                    return OrganicRegrowthPositionByUidBufferId;
                case 9:
                    return OrganicRuntimeFlagsByUidBufferId;
                case 10:
                    return OrganicLastTouchTimeByUidBufferId;
                case 11:
                    return OrganicOvergrownByUidBufferId;
                case 12:
                    return OrganicRootMoundAppliedByUidBufferId;
                case 13:
                    return OrganicBaseScaleByUidBufferId;
                case 14:
                    return OrganicPersistedHealth01ByUidBufferId;
                case 15:
                    return OrganicPersistedHeightScale01ByUidBufferId;
                case 16:
                    return OrganicSurfaceInstanceUidsBufferId;
                case 17:
                    return OrganicUnderwaterInstanceUidsBufferId;
                case 18:
                    return OrganicSurfaceMaterialClassesBufferId;
                case 19:
                    return OrganicUnderwaterMaterialClassesBufferId;
                case 20:
                    return OrganicTemplateDescriptorsBufferId;
                default:
                    return default;
            }
        }

        private bool TryAcquireOrganicMaturationMutationGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;
            if (vault == null)
                return false;

            ulong guardMask = 0UL;
            for (int i = 0; i < OrganicMaturationMutationBufferCount; i++)
            {
                if (!ShouldLockOrganicMaturationMutationBuffer(i))
                    continue;

                BufferID bufferId = GetOrganicMaturationMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
                lockedMask |= 1 << i;
            }

            if (guardMask == 0UL)
                return true;

            if (vault.TryAcquireMutationGuard(guardMask))
                return true;

            lockedMask = 0;
            return false;
        }

        private static void ReleaseOrganicMaturationMutationGuard(IDataVault vault, int lockedMask)
        {
            ReleaseOrganicGuard(vault, ResolveOrganicMaturationMutationGuardMask(lockedMask));
        }

        private static ulong ResolveOrganicMaturationMutationGuardMask(int lockedMask)
        {
            ulong guardMask = 0UL;
            for (int i = OrganicMaturationMutationBufferCount - 1; i >= 0; i--)
            {
                if ((lockedMask & (1 << i)) == 0)
                    continue;

                BufferID bufferId = GetOrganicMaturationMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private bool ShouldLockOrganicMaturationMutationBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    return _maturationScaleByInstanceUid.IsCreated;
                case 1:
                    return _maturationYieldByInstanceUid.IsCreated;
                case 2:
                    return _nextSporeAcousticTimeByInstanceUid.IsCreated;
                default:
                    return false;
            }
        }

        private static BufferID GetOrganicMaturationMutationBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicMaturationScaleByUidBufferId;
                case 1:
                    return OrganicMaturationYieldByUidBufferId;
                case 2:
                    return OrganicNextSporeAcousticTimeByUidBufferId;
                default:
                    return default;
            }
        }

        private bool TryAcquireOrganicOvergrowthMutationGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;
            if (vault == null)
                return false;

            ulong guardMask = 0UL;
            for (int i = 0; i < OrganicOvergrowthMutationBufferCount; i++)
            {
                if (!ShouldLockOrganicOvergrowthMutationBuffer(i))
                    continue;

                BufferID bufferId = GetOrganicOvergrowthMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
                lockedMask |= 1 << i;
            }

            if (guardMask == 0UL)
                return true;

            if (vault.TryAcquireMutationGuard(guardMask))
                return true;

            lockedMask = 0;
            return false;
        }

        private static void ReleaseOrganicOvergrowthMutationGuard(IDataVault vault, int lockedMask)
        {
            ReleaseOrganicGuard(vault, ResolveOrganicOvergrowthMutationGuardMask(lockedMask));
        }

        private static ulong ResolveOrganicOvergrowthMutationGuardMask(int lockedMask)
        {
            ulong guardMask = 0UL;
            for (int i = OrganicOvergrowthMutationBufferCount - 1; i >= 0; i--)
            {
                if ((lockedMask & (1 << i)) == 0)
                    continue;

                BufferID bufferId = GetOrganicOvergrowthMutationBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private bool ShouldLockOrganicOvergrowthMutationBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    return _destroyedByInstanceUid.IsCreated;
                case 1:
                    return _regrowthProgressByInstanceUid.IsCreated;
                case 2:
                    return _lastOrganicTouchTimeByInstanceUid.IsCreated;
                case 3:
                    return _overgrownByInstanceUid.IsCreated;
                case 4:
                    return _rootMoundAppliedByInstanceUid.IsCreated;
                case 5:
                    return _surfaceInstanceUids.IsCreated;
                case 6:
                    return _underwaterInstanceUids.IsCreated;
                case 7:
                    return _surfaceMaterialClasses.IsCreated;
                case 8:
                    return _underwaterMaterialClasses.IsCreated;
                case 9:
                    return _surfaceHealth.IsCreated;
                case 10:
                    return _underwaterHealth.IsCreated;
                default:
                    return false;
            }
        }

        private static BufferID GetOrganicOvergrowthMutationBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicDestroyedByUidBufferId;
                case 1:
                    return OrganicRegrowthProgressByUidBufferId;
                case 2:
                    return OrganicLastTouchTimeByUidBufferId;
                case 3:
                    return OrganicOvergrownByUidBufferId;
                case 4:
                    return OrganicRootMoundAppliedByUidBufferId;
                case 5:
                    return OrganicSurfaceInstanceUidsBufferId;
                case 6:
                    return OrganicUnderwaterInstanceUidsBufferId;
                case 7:
                    return OrganicSurfaceMaterialClassesBufferId;
                case 8:
                    return OrganicUnderwaterMaterialClassesBufferId;
                case 9:
                    return OrganicSurfaceHealthBufferId;
                case 10:
                    return OrganicUnderwaterHealthBufferId;
                default:
                    return default;
            }
        }

        private bool TryAcquireOrganicParasiteExposureReadGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;
            if (vault == null)
                return false;

            ulong guardMask = 0UL;
            for (int i = 0; i < OrganicParasiteExposureReadBufferCount; i++)
            {
                if (!ShouldLockOrganicParasiteExposureReadBuffer(i))
                    continue;

                BufferID bufferId = GetOrganicParasiteExposureReadBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
                lockedMask |= 1 << i;
            }

            if (guardMask == 0UL)
                return true;

            if (vault.TryAcquireMutationGuard(guardMask))
                return true;

            lockedMask = 0;
            return false;
        }

        private static void ReleaseOrganicParasiteExposureReadGuard(IDataVault vault, int lockedMask)
        {
            ReleaseOrganicGuard(vault, ResolveOrganicParasiteExposureReadGuardMask(lockedMask));
        }

        private static ulong ResolveOrganicParasiteExposureReadGuardMask(int lockedMask)
        {
            ulong guardMask = 0UL;
            for (int i = OrganicParasiteExposureReadBufferCount - 1; i >= 0; i--)
            {
                if ((lockedMask & (1 << i)) == 0)
                    continue;

                BufferID bufferId = GetOrganicParasiteExposureReadBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private bool ShouldLockOrganicParasiteExposureReadBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    return _surfaceInstanceUids.IsCreated;
                case 1:
                    return _underwaterInstanceUids.IsCreated;
                case 2:
                    return _surfaceHealth.IsCreated;
                case 3:
                    return _underwaterHealth.IsCreated;
                case 4:
                    return _runtimeFlagsByInstanceUid.IsCreated;
                default:
                    return false;
            }
        }

        private static BufferID GetOrganicParasiteExposureReadBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicSurfaceInstanceUidsBufferId;
                case 1:
                    return OrganicUnderwaterInstanceUidsBufferId;
                case 2:
                    return OrganicSurfaceHealthBufferId;
                case 3:
                    return OrganicUnderwaterHealthBufferId;
                case 4:
                    return OrganicRuntimeFlagsByUidBufferId;
                default:
                    return default;
            }
        }

        private bool TryAcquireOrganicLifecycleReadGuard(IDataVault vault, out int lockedMask)
        {
            lockedMask = 0;
            ulong guardMask = 0UL;
            for (int i = 0; i < OrganicLifecycleReadBufferCount; i++)
            {
                if (!ShouldLockOrganicLifecycleReadBuffer(i))
                    continue;

                BufferID bufferId = GetOrganicLifecycleReadBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
                lockedMask |= 1 << i;
            }

            if (guardMask == 0UL)
                return true;

            if (vault != null && vault.TryAcquireMutationGuard(guardMask))
                return true;

            lockedMask = 0;
            return false;
        }

        private static void ReleaseOrganicLifecycleReadGuard(IDataVault vault, int lockedMask)
        {
            ReleaseOrganicGuard(vault, ResolveOrganicLifecycleReadGuardMask(lockedMask));
        }

        private static ulong ResolveOrganicLifecycleReadGuardMask(int lockedMask)
        {
            ulong guardMask = 0UL;
            for (int i = OrganicLifecycleReadBufferCount - 1; i >= 0; i--)
            {
                if ((lockedMask & (1 << i)) == 0)
                    continue;

                BufferID bufferId = GetOrganicLifecycleReadBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            return guardMask;
        }

        private bool ShouldLockOrganicLifecycleReadBuffer(int index)
        {
            switch (index)
            {
                case 0:
                    return _destroyedByInstanceUid.IsCreated;
                case 1:
                    return _regrowthProgressByInstanceUid.IsCreated;
                case 2:
                    return _surfaceInstanceUids.IsCreated;
                case 3:
                    return _underwaterInstanceUids.IsCreated;
                case 4:
                    return _surfaceMaterialClasses.IsCreated;
                case 5:
                    return _underwaterMaterialClasses.IsCreated;
                case 6:
                    return _surfaceHealth.IsCreated;
                case 7:
                    return _underwaterHealth.IsCreated;
                case 8:
                    return _baseScaleByInstanceUid.IsCreated;
                default:
                    return false;
            }
        }

        private static BufferID GetOrganicLifecycleReadBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicDestroyedByUidBufferId;
                case 1:
                    return OrganicRegrowthProgressByUidBufferId;
                case 2:
                    return OrganicSurfaceInstanceUidsBufferId;
                case 3:
                    return OrganicUnderwaterInstanceUidsBufferId;
                case 4:
                    return OrganicSurfaceMaterialClassesBufferId;
                case 5:
                    return OrganicUnderwaterMaterialClassesBufferId;
                case 6:
                    return OrganicSurfaceHealthBufferId;
                case 7:
                    return OrganicUnderwaterHealthBufferId;
                case 8:
                    return OrganicBaseScaleByUidBufferId;
                default:
                    return default;
            }
        }

        private void ReleaseDearLieVaultBuffers(IDataVault vault)
        {
            if (_dearLieJobScheduled)
                CompleteDearLieJobForLifecycleBarrier();

            if (_dearLieVaultJobGuardHeld)
                ReleaseDearLieVaultJobGuard();

            _surfaceDearLieClaims.Release();
            _underwaterDearLieClaims.Release();
            _dearLieDamageEvents.Release();
            _dearLieResults.Release();
            _dearLieCounters.Release();
            _dearLieRegenRecords.Release();
            _dearLieTelemetryRing.Release();
            _surfaceDearLieBucketHeads.Release();
            _surfaceDearLieBucketNext.Release();
            _underwaterDearLieBucketHeads.Release();
            _underwaterDearLieBucketNext.Release();
            _dearLieVault = null;
            _dearLieVaultReady = false;
            _dearLieVaultJobGuardHeld = false;
            _dearLieVaultJobGuardMask = 0UL;
            _dearLieVaultJobGuardVault = null;
        }

        private void CompleteDearLieJobForLifecycleBarrier()
        {
            if (!_dearLieJobScheduled)
                return;

            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                CompleteDearLieJobIfNeeded(ResolveOrganicClockSeconds(), force: true);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }
        }

        private void CacheDearLieFallbackQualityWeightCold()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            _dearLieFallbackQualityWeight = math.saturate(math.isfinite(quality) ? quality : 0.5f);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            if (!IsAudioServiceUsable(audioService))
            {
                ClearCachedAudioService();
                return;
            }

            _audioService = audioService;
            _harvestAudioSink = _audioService as ISpatialAudioHarvestPlaybackSink;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            ClearCachedAudioService();
            return null;
        }

        private ISpatialAudioHarvestPlaybackSink ResolveHarvestAudioSink()
        {
            IAudioService audioService = ResolveAudioService();
            if (audioService == null)
                return null;

            ISpatialAudioHarvestPlaybackSink harvestAudioSink = _harvestAudioSink;
            if (ReferenceEquals(harvestAudioSink, audioService))
                return harvestAudioSink;

            harvestAudioSink = audioService as ISpatialAudioHarvestPlaybackSink;
            _harvestAudioSink = harvestAudioSink;
            return harvestAudioSink;
        }

        private void ClearCachedAudioService()
        {
            _audioService = null;
            _harvestAudioSink = null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void ClearCachedRegistryServices()
        {
            _playerInventoryService = null;
            _persistentWorldRegistry = null;
            ClearCachedAudioService();
        }

        private void AdvanceOrganicClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            double nextTime = _organicClockSeconds + deltaTime;
            _organicClockSeconds = nextTime >= 0d && nextTime < OrganicClockMaxSeconds
                ? nextTime
                : OrganicClockMaxSeconds;
        }

        private float ResolveOrganicClockSeconds()
        {
            double currentTime = _organicClockSeconds;
            if (!(currentTime > 0d))
                return 0f;

            return currentTime < OrganicClockMaxSeconds
                ? (float)currentTime
                : (float)OrganicClockMaxSeconds;
        }

        /// <summary>
        /// Processes pending entropy jobs and drop routing.
        /// </summary>
        public void Tick(float deltaTime)
        {
            AdvanceOrganicClock(deltaTime);
            if (_dearLieJobScheduled)
                return;

            RefreshActiveCachesIfNeeded(force: false, allowMutation: false);
        }

        public void LateFrameTick()
        {
            float currentTime = ResolveOrganicClockSeconds();
            UpdateOrganicPresentationState(currentTime);
            FlushPendingDearLieDebrisSignals();
            FlushPendingHarvestAudioEvents();
            FlushPendingSporeAcousticEvents();
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            float currentTime = ResolveOrganicClockSeconds();
            DispatcherJobSwap.BeginPostSimulationSwapWindow();
            try
            {
                ProcessDearLieDestructionSignals(currentTime);
                CompleteDearLieJobIfNeeded(currentTime, force: true);
                ProcessDearLieRegeneration(currentTime);
            }
            finally
            {
                DispatcherJobSwap.EndPostSimulationSwapWindow();
            }

            bool dropBufferDrained = DrainDropBuffer();
            if (dropBufferDrained &&
                (_deferredYieldScheduleFrame < 0 ||
                 Hecton8.Core.SystemDispatcher.CurrentFrameIndex >= _deferredYieldScheduleFrame))
            {
                _deferredYieldScheduleFrame = -1;
                DispatcherJobSwap.BeginPostSimulationSwapWindow();
                try
                {
                    ProcessYieldBatchIfNeeded();
                }
                finally
                {
                    DispatcherJobSwap.EndPostSimulationSwapWindow();
                }

                VoxelDynamicNavGridRuntime.SchedulePendingDynamicObstacleUpdates();
            }
        }

        private void UpdateOrganicPresentationState(float currentTime)
        {
            if (_dearLieJobScheduled || !RefreshActiveCachesIfNeeded(force: false, allowMutation: false))
                return;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            try
            {
                UpdateDecompositionVisuals(currentTime);
                UpdateRegrowthVisuals();
                UpdateMatureSporeAcoustics(currentTime);
                UpdateDamageVisuals(currentTime);
                UpdateWiltInstances(currentTime);
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }
        }

        /// <summary>
        /// Restores destroyed flora tombstones from persistence and re-applies active suppression after world paging.
        /// </summary>
        public void SlowTick()
        {
            if (_dearLieJobScheduled)
                return;

            bool destroyedSyncChanged = SyncDestroyedFloraFromPersistence();
            bool floraStateSyncChanged = SyncFloraStateOverridesFromPersistence();
            RefreshActiveCachesIfNeeded(force: destroyedSyncChanged || floraStateSyncChanged);
            float currentTime = ResolveOrganicClockSeconds();
            RefreshCorpseResourceNodes(currentTime);
            EvaluateAllelopathicRelease();
            EvaluateAggressiveOvergrowth(currentTime);
        }

        private void ProcessDearLieDestructionSignals(float currentTime)
        {
            if (_dearLieJobScheduled)
                return;

            if (!_dearLieVaultReady ||
                !_dearLieDamageEvents.IsCreated ||
                !_dearLieResults.IsCreated ||
                !_dearLieCounters.IsCreated ||
                !_surfaceDearLieBucketHeads.IsCreated ||
                !_surfaceDearLieBucketNext.IsCreated ||
                !_underwaterDearLieBucketHeads.IsCreated ||
                !_underwaterDearLieBucketNext.IsCreated)
            {
                return;
            }

            if (!HasAnyAuthoritativeDearLieDamageSignal() && !dearLieGenerateMockDamageBurst)
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 0, 0, 0f, 0u, 0);
                return;
            }

            if (!TryAcquireDearLieVaultJobGuard())
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            bool scheduled = false;
            bool recordStageTelemetry = false;
            bool dumpStageTelemetry = false;
            int stageRejectedCount = 0;
            int stageNanRejectCount = 0;
            byte stageTelemetryFlags = 0;
            try
            {
                ClearDearLieCounters();

                int damageCount = StageDearLieDamageEvents(out JobHandle stageHandle);
                if (damageCount <= 0)
                {
                    stageRejectedCount = math.max(0, ReadDearLieCounter(4));
                    stageNanRejectCount = math.max(0, ReadDearLieCounter(5));
                    stageTelemetryFlags = stageNanRejectCount > 0 ? (byte)1 : (byte)0;
                    dumpStageTelemetry = stageNanRejectCount > 0;
                    recordStageTelemetry = true;
                }
                else
                {
                    JobHandle surfaceHandle = ScheduleDearLieLane(false, damageCount, stageHandle);
                    JobHandle underwaterHandle = ScheduleDearLieLane(true, damageCount, stageHandle);
                    _dearLieJobHandle = JobHandle.CombineDependencies(stageHandle, JobHandle.CombineDependencies(surfaceHandle, underwaterHandle));
                    _dearLieScheduledDamageCount = damageCount;
                    _dearLieJobScheduleFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
                    _dearLieJobStartTimeSeconds = Time.realtimeSinceStartupAsDouble;
                    _dearLieJobScheduled = true;
                    scheduled = true;
                    CompleteDearLieJobIfNeeded(currentTime, force: true);
                }
            }
            finally
            {
                if (!scheduled)
                    ReleaseDearLieVaultJobGuard();
            }

            if (recordStageTelemetry)
            {
                RecordDearLieTelemetry(
                    Hecton8.Core.SystemDispatcher.CurrentFrameIndex,
                    0,
                    0,
                    0,
                    0,
                    stageRejectedCount,
                    stageNanRejectCount,
                    0f,
                    0u,
                    stageTelemetryFlags);
                if (dumpStageTelemetry)
                    DumpDearLieTelemetry();
            }
        }

        private static bool HasAnyAuthoritativeDearLieDamageSignal()
        {
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < signalCount; i++)
            {
                if ((signals[i].Flags & CombatDamageSignal.VisualOnlyFlag) == 0)
                    return true;
            }

            return false;
        }

        private void ClearDearLieCounters()
        {
            if (!_dearLieCounters.IsCreated)
                return;

            for (int i = 0; i < _dearLieCounters.Length; i++)
                _dearLieCounters[i] = default;
        }

        private int ReadDearLieCounter(int index)
        {
            if (!_dearLieCounters.IsCreated || (uint)index >= (uint)_dearLieCounters.Length)
                return 0;

            return _dearLieCounters[index].Value;
        }

        private void WriteDearLieCounter(int index, int value)
        {
            if (!_dearLieCounters.IsCreated || (uint)index >= (uint)_dearLieCounters.Length)
                return;

            FloraDearLieCounter64 counter = _dearLieCounters[index];
            counter.Value = value;
            _dearLieCounters[index] = counter;
        }

        private bool CompleteDearLieJobIfNeeded(float currentTime, bool force = true)
        {
            if (!_dearLieJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _dearLieJobHandle, force))
                return false;

            _dearLieJobScheduled = false;
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            bool sameFrameCompletion = _dearLieJobScheduleFrame == currentFrame;
            float queryMicroseconds = 0f;
            if (_dearLieJobStartTimeSeconds > 0d)
            {
                double elapsedSeconds = Time.realtimeSinceStartupAsDouble - _dearLieJobStartTimeSeconds;
                if (elapsedSeconds > 0d && math.isfinite(elapsedSeconds))
                    queryMicroseconds = (float)math.min(1000000d, elapsedSeconds * 1000000d);
            }

            _dearLieJobScheduleFrame = -1;
            _dearLieJobStartTimeSeconds = 0d;
            int damageCount = math.max(0, _dearLieScheduledDamageCount);
            _dearLieScheduledDamageCount = 0;
            bool recordCompletionTelemetry = false;
            bool dumpCompletionTelemetry = false;
            int telemetryDamageCount = damageCount;
            int telemetryDestroyedCount = 0;
            int telemetryVfxCount = 0;
            int telemetryRejectedCount = 0;
            int telemetryNanRejectCount = 0;
            float telemetryQueryMicroseconds = queryMicroseconds;
            uint telemetryLastInstanceUid = 0u;
            byte telemetryFlags = 0;
            try
            {
                int destroyedCount = ApplyDearLieDestructionResults(currentTime, out uint lastInstanceUid, out int vfxCount);
                int stagedDebrisOverflowCount = math.max(0, _pendingDearLieDebrisOverflowCount);
                _dearLieLastDamageFrame = currentFrame;
                _dearLieLastDestroyedCount = destroyedCount;
                _dearLieLastVfxCount = vfxCount;

                int overflowCount = math.max(0, ReadDearLieCounter(6)) + stagedDebrisOverflowCount;
                int rejectedCount = math.max(0, ReadDearLieCounter(4)) + overflowCount;
                int nanRejectCount = math.max(0, ReadDearLieCounter(5));
                byte flags = 0;
                if (nanRejectCount > 0)
                    flags |= 1;
                if (destroyedCount > 0)
                    flags |= 2;
                if (sameFrameCompletion && queryMicroseconds > 500f)
                    flags |= 8;
                if (overflowCount > 0)
                    flags |= 16;
                telemetryDestroyedCount = destroyedCount;
                telemetryVfxCount = vfxCount;
                telemetryRejectedCount = rejectedCount;
                telemetryNanRejectCount = nanRejectCount;
                telemetryLastInstanceUid = lastInstanceUid;
                telemetryFlags = flags;
                dumpCompletionTelemetry = nanRejectCount > 0 || overflowCount > 0 || (sameFrameCompletion && queryMicroseconds > 500f);
                recordCompletionTelemetry = true;
            }
            finally
            {
                ReleaseDearLieVaultJobGuard();
            }

            if (recordCompletionTelemetry)
            {
                RecordDearLieTelemetry(
                    currentFrame,
                    telemetryDamageCount,
                    telemetryDestroyedCount,
                    telemetryVfxCount,
                    0,
                    telemetryRejectedCount,
                    telemetryNanRejectCount,
                    telemetryQueryMicroseconds,
                    telemetryLastInstanceUid,
                    telemetryFlags);
                if (dumpCompletionTelemetry)
                    DumpDearLieTelemetry();
            }

            return true;
        }

        private int StageDearLieDamageEvents(out JobHandle stageHandle)
        {
            stageHandle = default;
            int writeCount = 0;
            int rejectedCount = 0;
            int nanRejectCount = 0;
            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < signalCount && writeCount < DearLieMaxDamageSignalsPerFrame; i++)
            {
                CombatDamageSignal signal = signals[i];
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (!TryBuildDearLieEvent(in signal, out FloraDestructionEventDTO dearLieEvent, ref nanRejectCount))
                {
                    rejectedCount++;
                    continue;
                }

                _dearLieDamageEvents[writeCount++] = dearLieEvent;
            }

            if (dearLieGenerateMockDamageBurst && writeCount < DearLieMaxDamageSignalsPerFrame)
            {
                dearLieGenerateMockDamageBurst = false;
                int mockCount = math.min(DearLieMockDamageSignalCount, DearLieMaxDamageSignalsPerFrame - writeCount);
                double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                double3 centerAup = originAup + global::Hecton8.World.AUPMath.ToDouble3(dearLieMockDamageCenter);
                var mockJob = new GenerateMockFloraDamageJob
                {
                    Events = _dearLieDamageEvents,
                    Offset = writeCount,
                    Count = mockCount,
                    CenterAUP = centerAup,
                    Seed = unchecked((uint)(Hecton8.Core.SystemDispatcher.CurrentFrameIndex * 268 + 0x51A268u))
                };
                stageHandle = mockJob.Schedule(mockCount, DearLieJobBatchSize);
                writeCount += mockCount;
            }

            WriteDearLieCounter(4, rejectedCount);
            WriteDearLieCounter(5, nanRejectCount);

            return writeCount;
        }

        private static bool TryBuildDearLieEvent(
            in CombatDamageSignal signal,
            out FloraDestructionEventDTO dearLieEvent,
            ref int nanRejectCount)
        {
            dearLieEvent = default;
            if (!CombatDamageSignalCodec.IsFiniteAup(signal.ImpactAup) ||
                !math.isfinite(signal.Magnitude) ||
                !math.all(math.isfinite(signal.Direction)))
            {
                nanRejectCount++;
                return false;
            }

            if (signal.Magnitude <= DearLieMinimumMagnitude)
                return false;

            if ((signal.Flags & CombatDamageSignal.LegacyMirrorFlag) != 0)
                return false;

            bool explicitFloraRoute = (signal.Flags & DearLieFloraDamageFlag) != 0;
            bool areaRoute = signal.TargetId == 0 && (signal.TargetHash == 0u || signal.TargetHash == DearLieSignalHashFlora || signal.TargetHash == DearLieSignalHashOrganic);
            if (!explicitFloraRoute && !areaRoute)
                return false;

            dearLieEvent = new FloraDestructionEventDTO
            {
                ImpactAUP = signal.ImpactAup,
                FloraTypeHash = signal.TargetHash == 0u ? DearLieSignalHashFlora : signal.TargetHash,
                MagnitudeBits = math.asuint(math.saturate(signal.Magnitude))
            };
            return true;
        }

        private JobHandle ScheduleDearLieLane(bool underwater, int damageCount, JobHandle inputDependency)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<FloraDearLieClaim64> claims = underwater ? _underwaterDearLieClaims : _surfaceDearLieClaims;
            NativeArray<int> bucketHeads = underwater ? _underwaterDearLieBucketHeads : _surfaceDearLieBucketHeads;
            NativeArray<int> bucketNext = underwater ? _underwaterDearLieBucketNext : _surfaceDearLieBucketNext;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                !claims.IsCreated ||
                !bucketHeads.IsCreated ||
                !bucketNext.IsCreated ||
                count <= 0)
            {
                return default;
            }

            int safeCount = math.min(count, math.min(matrices.Length, math.min(metadata.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, math.min(health.Length, math.min(claims.Length, bucketNext.Length)))))));
            if (safeCount <= 0)
                return default;

            float qualityWeight = ResolveDearLieGlobalQualityWeight();
            double3 originAup = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            var clearJob = new ClearDearLieClaimsJob
            {
                Claims = claims,
                Count = safeCount
            };
            JobHandle clearHandle = clearJob.Schedule(safeCount, DearLieJobBatchSize, inputDependency);
            var clearBucketsJob = new ClearDearLieBucketsJob
            {
                BucketHeads = bucketHeads,
                Count = bucketHeads.Length
            };
            JobHandle clearBucketsHandle = clearBucketsJob.Schedule(bucketHeads.Length, DearLieJobBatchSize, inputDependency);
            JobHandle clearAllHandle = JobHandle.CombineDependencies(clearHandle, clearBucketsHandle);

            var buildJob = new BuildDearLieSpatialHashJob
            {
                Matrices = matrices,
                InstanceUids = instanceUids,
                Health = health,
                BucketHeads = bucketHeads,
                BucketNext = bucketNext,
                Count = safeCount,
                BucketCount = bucketHeads.Length,
                RuntimeOriginAUP = originAup,
                CellSizeMeters = DearLieSpatialCellSizeMeters
            };
            JobHandle buildHandle = buildJob.Schedule(safeCount, DearLieJobBatchSize, clearAllHandle);

            var resolveJob = new ResolveDearLieDamageJob
            {
                Matrices = matrices,
                Metadata = metadata,
                InstanceUids = instanceUids,
                MaterialClasses = materialClasses,
                Health = health,
                Claims = claims,
                Events = _dearLieDamageEvents,
                Results = _dearLieResults,
                Counters = _dearLieCounters,
                BucketHeads = bucketHeads,
                BucketNext = bucketNext,
                Count = safeCount,
                BucketCount = bucketHeads.Length,
                EventCount = math.min(damageCount, DearLieMaxDamageSignalsPerFrame),
                RuntimeOriginAUP = originAup,
                CellSizeMeters = DearLieSpatialCellSizeMeters,
                QueryRadiusMeters = ResolveDearLieQueryRadius(),
                GlobalQualityWeight = qualityWeight,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                LaneSalt = underwater ? 0xA2680002u : 0xA2680001u
            };
            return resolveJob.Schedule(damageCount, 1, buildHandle);
        }

        private int ApplyDearLieDestructionResults(float currentTime, out uint lastInstanceUid, out int vfxCount)
        {
            lastInstanceUid = 0u;
            vfxCount = 0;
            _pendingDearLieDebrisSignalCount = 0;
            _pendingDearLieDebrisOverflowCount = 0;
            if (!_dearLieResults.IsCreated ||
                !_dearLieCounters.IsCreated ||
                !_destroyedByInstanceUid.IsCreated)
            {
                return 0;
            }

            int resultCount = math.min(math.max(0, ReadDearLieCounter(0)), _dearLieResults.Length);
            int appliedCount = 0;
            for (int i = 0; i < resultCount; i++)
            {
                FloraDearLieDestructionResult result = _dearLieResults[i];
                uint instanceUid = result.InstanceUid;
                if (instanceUid == 0u ||
                    _destroyedByInstanceUid.ContainsKey(instanceUid) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)))
                {
                    continue;
                }

                if (!TryFindActiveInstanceByUidPinned(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
                    continue;

                NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                    continue;

                Vector3 runtimePosition = ExtractTranslation(result.OriginalMatrix);
                if (!IsFiniteVector(runtimePosition))
                    runtimePosition = ExtractTranslation(matrices[activeIndex]);

                AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(result.ImpactAUP);
                Vector3 impactRuntimePosition = (Vector3)AUPMath.ToRuntimeFloat3(in impactAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
                if (IsFiniteVector(impactRuntimePosition) && IsFiniteVector(runtimePosition))
                {
                    _dearLieLastImpactRuntimePosition = impactRuntimePosition;
                    _dearLieLastTargetRuntimePosition = runtimePosition;
                    _dearLieHasLastDebugHit = 1;
                }

                if (!_destroyedByInstanceUid.TryAdd(instanceUid, 1))
                {
                    _pendingDearLieDebrisOverflowCount++;
                    continue;
                }

                if (_healthByInstanceUid.IsCreated)
                    _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)0f);

                ClearOrganicLifecycleState(instanceUid);
                PrimeDecompositionState(instanceUid, currentTime);
                SetLaneHealth(underwater, activeIndex, 0f);
                if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                if (_damageVisualProgressByInstanceUid.IsCreated)
                    _damageVisualProgressByInstanceUid.Remove(instanceUid);

                ClearPersistedFloraStateOverride(instanceUid);
                byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
                ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
                QueueDearLieRegeneration(instanceUid, underwater, activeIndex, runtimePosition, currentTime + ResolveDearLieRegenerationDelaySeconds(), in result.OriginalMatrix);
                if (TryQueueDearLieDebrisSignal(in result))
                    vfxCount++;
                appliedCount++;
                lastInstanceUid = instanceUid;
            }

            return appliedCount;
        }

        private bool TryQueueDearLieDebrisSignal(in FloraDearLieDestructionResult result)
        {
            if (result.EmitVfx == 0 || result.InstanceUid == 0u || result.VfxQuantity == 0)
                return false;

            float intensity = math.saturate(math.asfloat(result.MagnitudeBits));
            if (!math.isfinite(intensity) || intensity <= 0f || !CombatDamageSignalCodec.IsFiniteAup(result.ImpactAUP))
                return false;

            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(result.ImpactAUP),
                SpeciesHash = result.FloraTypeHash ^ ((uint)result.MaterialClass * 2246822519u),
                SourceEntityId = result.InstanceUid ^ 0x7F4A7C15u,
                Intensity01 = intensity,
                DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = result.VfxQuantity
            };
            if (_pendingDearLieDebrisSignalCount >= _pendingDearLieDebrisSignals.Length)
            {
                _pendingDearLieDebrisOverflowCount++;
                return false;
            }

            _pendingDearLieDebrisSignals[_pendingDearLieDebrisSignalCount++] = signal;
            return true;
        }

        private void FlushPendingDearLieDebrisSignals()
        {
            int count = math.min(_pendingDearLieDebrisSignalCount, _pendingDearLieDebrisSignals.Length);
            for (int i = 0; i < count; i++)
            {
                DebrisSpawnSignal signal = _pendingDearLieDebrisSignals[i];
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_DestructibleOrganicManager);
                _pendingDearLieDebrisSignals[i] = default;
            }

            _pendingDearLieDebrisSignalCount = 0;
        }

        private void QueueDearLieRegeneration(uint instanceUid, bool underwater, int activeIndex, Vector3 runtimePosition, float restoreTimeSeconds, in Matrix4x4 originalMatrix)
        {
            if (!_dearLieRegenRecords.IsCreated || instanceUid == 0u || _dearLieRegenCount >= _dearLieRegenRecords.Length)
                return;

            FloraDearLieRegenRecord record = default;
            record.OriginalMatrix = originalMatrix;
            record.InstanceUid = instanceUid;
            record.ActiveIndex = activeIndex;
            record.RestoreTimeSeconds = restoreTimeSeconds;
            record.RuntimePosition = ToFloat3(runtimePosition);
            record.Underwater = underwater ? (byte)1 : (byte)0;
            _dearLieRegenRecords[_dearLieRegenCount++] = record;
        }

        private void ProcessDearLieRegeneration(float currentTime)
        {
            if (!_dearLieRegenRecords.IsCreated || _dearLieRegenCount <= 0)
                return;

            int recoveredCount = 0;
            bool popLockFailed = false;
            bool anyLockFailed = false;
            bool requeueFailed = false;
            while (TryPopReadyDearLieRegeneration(currentTime, out FloraDearLieRegenRecord record, out popLockFailed))
            {
                Vector3 runtimePosition = ToRuntimeVector3(record.RuntimePosition);
                if (!IsFiniteVector(runtimePosition) && IsFiniteMatrix(in record.OriginalMatrix))
                    runtimePosition = ExtractTranslation(record.OriginalMatrix);

                if (!IsFiniteVector(runtimePosition) &&
                    TrySnapshotActiveInstanceByUidWithLock(
                        record.InstanceUid,
                        out _,
                        out _,
                        out _,
                        out _,
                        out Matrix4x4 matrix,
                        out _,
                        out _))
                {
                    runtimePosition = ExtractTranslation(matrix);
                }

                if (!IsFiniteVector(runtimePosition) || !IsFiniteMatrix(in record.OriginalMatrix))
                    continue;

                if (TrySetRegrowthProgress(record.InstanceUid, runtimePosition, 1f, true, in record.OriginalMatrix, out bool regrowthLockFailed))
                {
                    recoveredCount++;
                }
                else if (regrowthLockFailed)
                {
                    anyLockFailed = true;
                    float retryTimeSeconds = (float)math.min(OrganicClockMaxSeconds, (double)currentTime + DearLieRegenerationRetryDelaySeconds);
                    requeueFailed |= !TryRequeueDearLieRegeneration(in record, retryTimeSeconds);
                }
            }

            anyLockFailed |= popLockFailed;
            if (anyLockFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);

            if (requeueFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 128);

            if (recoveredCount > 0)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, recoveredCount, 0, 0, 0f, 0u, 4);
        }

        private bool TryPopReadyDearLieRegeneration(float currentTime, out FloraDearLieRegenRecord record, out bool lockFailed)
        {
            record = default;
            lockFailed = false;
            if (!_dearLieRegenRecords.IsCreated || _dearLieRegenCount <= 0)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, DearLieRegenRecordsBufferId, out ulong guardMask))
            {
                lockFailed = true;
                return false;
            }

            try
            {
                for (int i = _dearLieRegenCount - 1; i >= 0; i--)
                {
                    FloraDearLieRegenRecord candidate = _dearLieRegenRecords[i];
                    if (candidate.InstanceUid == 0u || currentTime < candidate.RestoreTimeSeconds)
                        continue;

                    int lastIndex = _dearLieRegenCount - 1;
                    _dearLieRegenRecords[i] = _dearLieRegenRecords[lastIndex];
                    _dearLieRegenRecords[lastIndex] = default;
                    _dearLieRegenCount = lastIndex;
                    record = candidate;
                    return true;
                }

                return false;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool TryRequeueDearLieRegeneration(in FloraDearLieRegenRecord record, float restoreTimeSeconds)
        {
            if (!_dearLieRegenRecords.IsCreated || record.InstanceUid == 0u)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, DearLieRegenRecordsBufferId, out ulong guardMask))
                return false;

            try
            {
                if (_dearLieRegenCount >= _dearLieRegenRecords.Length)
                    return false;

                FloraDearLieRegenRecord retryRecord = record;
                retryRecord.RestoreTimeSeconds = restoreTimeSeconds;
                _dearLieRegenRecords[_dearLieRegenCount++] = retryRecord;
                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private void RecordDearLieTelemetry(
            int frameIndex,
            int damageSignalCount,
            int destroyedCount,
            int vfxSignalCount,
            int recoveredCount,
            int rejectedSignalCount,
            int nanRejectCount,
            float queryMicroseconds,
            uint lastInstanceUid,
            byte flags)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, DearLieTelemetryRingBufferId, out ulong guardMask))
                return;

            try
            {
                if (!_dearLieTelemetryRing.IsCreated || _dearLieTelemetryRing.Length == 0)
                    return;

                int telemetryCursor = math.max(0, _dearLieTelemetryCursor);
                int index = telemetryCursor % _dearLieTelemetryRing.Length;
                _dearLieTelemetryCursor = telemetryCursor >= int.MaxValue - 1
                    ? _dearLieTelemetryRing.Length
                    : telemetryCursor + 1;
                float qualityWeight = ResolveDearLieGlobalQualityWeight();
                _dearLieLastQualityWeight = qualityWeight;
                float safeQueryMicroseconds = math.select(0f, queryMicroseconds, math.isfinite(queryMicroseconds));
                uint hash = 2166136261u;
                hash = MixDearLieHash(hash, (uint)frameIndex);
                hash = MixDearLieHash(hash, (uint)damageSignalCount);
                hash = MixDearLieHash(hash, (uint)destroyedCount);
                hash = MixDearLieHash(hash, lastInstanceUid);
                hash = MixDearLieHash(hash, math.asuint(safeQueryMicroseconds));
                FloraDearLieTelemetryEntry entry = default;
                entry.FrameIndex = frameIndex;
                entry.SurfaceCount = _surfaceCount;
                entry.UnderwaterCount = _underwaterCount;
                entry.DamageSignalCount = damageSignalCount;
                entry.DestroyedCount = destroyedCount;
                entry.VfxSignalCount = vfxSignalCount;
                entry.RegenQueuedCount = _dearLieRegenCount;
                entry.RecoveredCount = recoveredCount;
                entry.RejectedSignalCount = rejectedSignalCount;
                entry.NanRejectCount = nanRejectCount;
                entry.GlobalQualityWeight = qualityWeight;
                entry.Hash = hash;
                entry.LastInstanceUid = lastInstanceUid;
                entry.Flags = flags;
                entry.QueryMicroseconds = safeQueryMicroseconds;
                _dearLieTelemetryRing[index] = entry;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private unsafe void DumpDearLieTelemetry()
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, DearLieTelemetryRingBufferId, out ulong guardMask))
                return;

            int snapshotCount = 0;
            try
            {
                if (_dearLieTelemetryRing.IsCreated)
                {
                    snapshotCount = math.min(_dearLieTelemetryRing.Length, _dearLieTelemetryDumpSnapshot.Length);
                    for (int i = 0; i < snapshotCount; i++)
                        _dearLieTelemetryDumpSnapshot[i] = _dearLieTelemetryRing[i];
                }
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }

            if (snapshotCount <= 0)
                return;

            NativeArray<byte> payload = default;
            try
            {
                int stride = UnsafeUtility.SizeOf<FloraDearLieTelemetryEntry>();
                int byteCount = snapshotCount * stride;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(DestructibleOrganicManager),
                    "DestructibleOrganicTelemetryDumpPayload");

                unsafe
                {
                    byte* bytes = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                    for (int i = 0; i < snapshotCount; i++)
                    {
                        FloraDearLieTelemetryEntry entry = _dearLieTelemetryDumpSnapshot[i];
                        UnsafeUtility.CopyStructureToPtr(ref entry, bytes + i * stride);
                    }
                }

                NativeFaultDumpWriter.TryWriteAll("Docs/AgentLogs/Dump_1318_Organics.bin", payload, byteCount);
            }
            catch (global::System.IO.IOException)
            {
            }
            catch (global::System.UnauthorizedAccessException)
            {
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(DestructibleOrganicManager),
                    "DestructibleOrganicTelemetryDumpPayload");
            }
        }

        private float ResolveDearLieGlobalQualityWeight()
        {
            if (math.isfinite(dearLieQualityOverride) && dearLieQualityOverride >= 0f)
                return math.saturate(dearLieQualityOverride);

            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(weight))
                weight = _dearLieFallbackQualityWeight;

            return math.saturate(weight);
        }

        private float ResolveDearLieQueryRadius()
        {
            return math.clamp(
                math.select(DearLieQueryRadiusMeters, dearLieDamageRadiusEpsilon, math.isfinite(dearLieDamageRadiusEpsilon)),
                0.25f,
                8f);
        }

        private float ResolveDearLieRegenerationDelaySeconds()
        {
            return math.clamp(
                math.select(DearLieRegenerationDelaySeconds, dearLieRegenerationDelaySeconds, math.isfinite(dearLieRegenerationDelaySeconds)),
                5f,
                900f);
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        private static float2 MakeFloat2(float x, float y)
        {
            float2 value = default;
            value.x = x;
            value.y = y;
            return value;
        }

        private static float2 UnitFloat2()
        {
            float2 value = default;
            value.x = 1f;
            value.y = 1f;
            return value;
        }

        private static bool AreHalfValuesEquivalent(Unity.Mathematics.half a, Unity.Mathematics.half b)
        {
            return math.abs((float)a - (float)b) <= 0.0001f;
        }

        private static uint MixDearLieHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static int ComputeDearLieCellHash(double3 positionAup, double cellSizeMeters)
        {
            double safeCellSize = math.max(0.25d, cellSizeMeters);
            long x = (long)math.floor(positionAup.x / safeCellSize);
            long y = (long)math.floor(positionAup.y / safeCellSize);
            long z = (long)math.floor(positionAup.z / safeCellSize);
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (ulong)x) * 1099511628211UL;
            hash = (hash ^ (ulong)y) * 1099511628211UL;
            hash = (hash ^ (ulong)z) * 1099511628211UL;
            return unchecked((int)(hash ^ (hash >> 32)));
        }

        private static int ComputeDearLieCellHash(long x, long y, long z)
        {
            ulong hash = 1469598103934665603UL;
            hash = (hash ^ (ulong)x) * 1099511628211UL;
            hash = (hash ^ (ulong)y) * 1099511628211UL;
            hash = (hash ^ (ulong)z) * 1099511628211UL;
            return unchecked((int)(hash ^ (hash >> 32)));
        }

        private static double3 ExtractMatrixTranslationDouble(Matrix4x4 matrix)
        {
            double3 result = default;
            result.x = matrix.m03;
            result.y = matrix.m13;
            result.z = matrix.m23;
            return result;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearDearLieClaimsJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1: Claims is a Vault-backed 64-byte claim array sized to the visible flora lane before scheduling; each worker writes only its own index during the clear pass.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2: The clear pass is chained before spatial hash build and resolve jobs, so no concurrent writer reads stale claim state while this pass runs.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3: NativeDisableParallelForRestriction is required because the same claim array is later used for atomic CompareExchange claims; the owner holds Vault job locks until dispatcher completion.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDearLieClaim64> Claims;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || index >= Claims.Length)
                    return;

                Claims[index] = default;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ClearDearLieBucketsJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketHeads;
            public int Count;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || index >= BucketHeads.Length)
                    return;

                BucketHeads[index] = -1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct BuildDearLieSpatialHashJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly, NoAlias] public NativeArray<uint> InstanceUids;
            [ReadOnly, NoAlias] public NativeArray<Unity.Mathematics.half> Health;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketHeads;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> BucketNext;
            public int Count;
            public int BucketCount;
            public double3 RuntimeOriginAUP;
            public double CellSizeMeters;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count ||
                    index >= Matrices.Length ||
                    index >= InstanceUids.Length ||
                    index >= Health.Length ||
                    index >= BucketNext.Length)
                {
                    return;
                }

                int bucketCount = math.min(BucketCount, BucketHeads.Length);
                if (bucketCount <= 0 || (bucketCount & (bucketCount - 1)) != 0)
                    return;

                BucketNext[index] = -1;

                if (InstanceUids[index] == 0u || (float)Health[index] <= 0.0001f)
                    return;

                Matrix4x4 matrix = Matrices[index];
                double3 positionAup = RuntimeOriginAUP + ExtractMatrixTranslationDouble(matrix);
                if (!math.all(math.isfinite(positionAup)))
                    return;

                int hash = ComputeDearLieCellHash(positionAup, CellSizeMeters);
                int bucketIndex = (int)((uint)hash & (uint)(bucketCount - 1));
                int* heads = (int*)BucketHeads.GetUnsafePtr();
                int* next = (int*)BucketNext.GetUnsafePtr();
                int oldHead = Interlocked.Exchange(ref heads[bucketIndex], index);
                next[index] = oldHead;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ResolveDearLieDamageJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1: Matrices, Metadata, Health, Claims, Results, Counters, Events, and bucket arrays are distinct native lanes; Dear Lie transient lanes are Vault-backed and locked while jobs hold pointers.
            // SAFETY_JUSTIFICATION_PARAGRAPH_2: Cross-worker mutation is limited to atomic claim slots and 64-byte padded counters; result rows are allocated by atomic counter and have 128-byte stride to avoid cache-line overlap.
            // SAFETY_JUSTIFICATION_PARAGRAPH_3: The job emits only staged result rows. SignalBus publication happens after DispatcherJobSwap completion in the owner phase, avoiding legacy writer lifetime races.
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> Matrices;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<HectonVegetationInstanceData> Metadata;
            [ReadOnly, NoAlias] public NativeArray<uint> InstanceUids;
            [ReadOnly, NoAlias] public NativeArray<byte> MaterialClasses;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<Unity.Mathematics.half> Health;
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDearLieClaim64> Claims;
            [ReadOnly, NoAlias] public NativeArray<FloraDestructionEventDTO> Events;
            // SAFETY_JUSTIFICATION_PARAGRAPH_4: Results and Counters are intentionally shared by surface/underwater resolve jobs; rows are claimed only by atomic 64-byte padded counters.
            // SAFETY_JUSTIFICATION_PARAGRAPH_5: Each result row is 128 bytes and written once by the worker that owns the returned atomic index; readers are fenced by DispatcherJobSwap.
            // SAFETY_JUSTIFICATION_PARAGRAPH_6: Native container safety is disabled only on these two cross-lane aggregation buffers, not on lane source arrays or flat spatial buckets.
            [NoAlias, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<FloraDearLieDestructionResult> Results;
            [NoAlias, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction] public NativeArray<FloraDearLieCounter64> Counters;
            [ReadOnly, NoAlias] public NativeArray<int> BucketHeads;
            [ReadOnly, NoAlias] public NativeArray<int> BucketNext;
            public int Count;
            public int BucketCount;
            public int EventCount;
            public double3 RuntimeOriginAUP;
            public double CellSizeMeters;
            public float QueryRadiusMeters;
            public float GlobalQualityWeight;
            public uint Frame;
            public uint LaneSalt;

            public void Execute(int eventIndex)
            {
                if ((uint)eventIndex >= (uint)EventCount || eventIndex >= Events.Length)
                    return;

                FloraDestructionEventDTO damageEvent = Events[eventIndex];
                float magnitude01 = math.asfloat(damageEvent.MagnitudeBits);
                if (!math.all(math.isfinite(damageEvent.ImpactAUP)) || !math.isfinite(magnitude01))
                {
                    IncrementCounter(5);
                    return;
                }

                double safeCellSize = math.max(0.25d, CellSizeMeters);
                double3 cellPosition = damageEvent.ImpactAUP / safeCellSize;
                long baseX = (long)math.floor(cellPosition.x);
                long baseY = (long)math.floor(cellPosition.y);
                long baseZ = (long)math.floor(cellPosition.z);
                float queryRadius = math.max(0.05f, QueryRadiusMeters);
                float queryRadiusSq = queryRadius * queryRadius;
                int bestIndex = -1;
                float bestDistanceSq = queryRadiusSq;
                int bucketCount = math.min(BucketCount, BucketHeads.Length);
                if (bucketCount <= 0 || (bucketCount & (bucketCount - 1)) != 0 || !BucketNext.IsCreated)
                    return;

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int cellHash = ComputeDearLieCellHash(baseX + dx, baseY + dy, baseZ + dz);
                            int bucketIndex = (int)((uint)cellHash & (uint)(bucketCount - 1));
                            int candidateIndex = BucketHeads[bucketIndex];
                            int guard = 0;
                            while (candidateIndex >= 0 && guard++ < Count)
                            {
                                int currentIndex = candidateIndex;
                                candidateIndex = currentIndex < BucketNext.Length ? BucketNext[currentIndex] : -1;
                                if ((uint)currentIndex >= (uint)Count ||
                                    currentIndex >= Matrices.Length ||
                                    currentIndex >= InstanceUids.Length ||
                                    currentIndex >= Health.Length)
                                {
                                    continue;
                                }

                                if (InstanceUids[currentIndex] == 0u || (float)Health[currentIndex] <= 0.0001f)
                                    continue;

                                double3 candidateAup = RuntimeOriginAUP + ExtractMatrixTranslationDouble(Matrices[currentIndex]);
                                if (!math.all(math.isfinite(candidateAup)))
                                    continue;

                                double3 localDeltaAup = candidateAup - damageEvent.ImpactAUP;
                                double floatCastClampMeters = math.max((double)queryRadius * 4d, 1d);
                                localDeltaAup.x = math.clamp(localDeltaAup.x, -floatCastClampMeters, floatCastClampMeters);
                                localDeltaAup.y = math.clamp(localDeltaAup.y, -floatCastClampMeters, floatCastClampMeters);
                                localDeltaAup.z = math.clamp(localDeltaAup.z, -floatCastClampMeters, floatCastClampMeters);
                                float3 localDelta = default;
                                localDelta.x = (float)localDeltaAup.x;
                                localDelta.y = (float)localDeltaAup.y;
                                localDelta.z = (float)localDeltaAup.z;
                                if (!math.all(math.isfinite(localDelta)))
                                    continue;

                                float distanceSq = math.lengthsq(localDelta);
                                if (distanceSq < bestDistanceSq)
                                {
                                    bestDistanceSq = distanceSq;
                                    bestIndex = currentIndex;
                                }
                            }
                        }
                    }
                }

                if (bestIndex < 0 || !TryClaim(bestIndex))
                    return;

                uint instanceUid = InstanceUids[bestIndex];
                int resultIndex = IncrementCounter(0) - 1;
                if ((uint)resultIndex >= (uint)Results.Length)
                {
                    IncrementCounter(6);
                    return;
                }

                Matrix4x4* matrixPtr = (Matrix4x4*)Matrices.GetUnsafePtr();
                Unity.Mathematics.half* healthPtr = (Unity.Mathematics.half*)Health.GetUnsafePtr();
                ref Matrix4x4 matrixRef = ref UnsafeUtility.AsRef<Matrix4x4>(matrixPtr + bestIndex);
                ref Unity.Mathematics.half healthRef = ref UnsafeUtility.AsRef<Unity.Mathematics.half>(healthPtr + bestIndex);
                Matrix4x4 originalMatrix = matrixRef;
                ScaleMatrixColumnsToZero(ref matrixRef);
                healthRef = (Unity.Mathematics.half)0f;

                if (bestIndex < Metadata.Length)
                {
                    HectonVegetationInstanceData* metadataPtr = (HectonVegetationInstanceData*)Metadata.GetUnsafePtr();
                    ref HectonVegetationInstanceData data = ref UnsafeUtility.AsRef<HectonVegetationInstanceData>(metadataPtr + bestIndex);
                    data.HeightScale = 0f;
                    data.WidthScale = 0f;
                    data.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
                    data.RuntimeFlags = FloraRuntimeFlagDead;
                    data.HealthNormalized = 0f;
                    data.Reserved0 = -1f;
                }

                ResolveDebrisEmission(in damageEvent, instanceUid, bestIndex, out byte emitVfx, out ushort vfxQuantity, out byte materialClass);
                FloraDearLieDestructionResult result = default;
                result.ImpactAUP = damageEvent.ImpactAUP;
                result.OriginalMatrix = originalMatrix;
                result.InstanceUid = instanceUid;
                result.ActiveIndex = bestIndex;
                result.FloraTypeHash = damageEvent.FloraTypeHash;
                result.MagnitudeBits = damageEvent.MagnitudeBits;
                result.VfxQuantity = vfxQuantity;
                result.EmitVfx = emitVfx;
                result.MaterialClass = materialClass;
                Results[resultIndex] = result;
            }

            private bool TryClaim(int index)
            {
                if ((uint)index >= (uint)Claims.Length)
                    return false;

                FloraDearLieClaim64* claimPtr = (FloraDearLieClaim64*)Claims.GetUnsafePtr();
                return Interlocked.CompareExchange(ref claimPtr[index].Claimed, 1, 0) == 0;
            }

            private int IncrementCounter(int index)
            {
                if ((uint)index >= (uint)Counters.Length)
                    return 0;

                FloraDearLieCounter64* ptr = (FloraDearLieCounter64*)Counters.GetUnsafePtr();
                return Interlocked.Increment(ref ptr[index].Value);
            }

            private void ResolveDebrisEmission(in FloraDestructionEventDTO damageEvent, uint instanceUid, int activeIndex, out byte emitVfx, out ushort quantity, out byte materialClass)
            {
                emitVfx = 0;
                quantity = 0;
                float q = math.saturate(GlobalQualityWeight);
                float intensity = math.saturate(math.asfloat(damageEvent.MagnitudeBits));
                materialClass = activeIndex >= 0 && activeIndex < MaterialClasses.Length ? MaterialClasses[activeIndex] : (byte)0;
                float emissionProbability = math.saturate((0.12f + (q * 0.88f)) * math.max(0.2f, intensity));
                uint hash = instanceUid ^ Frame ^ LaneSalt ^ ((uint)materialClass * 2654435761u);
                if (Hash01(hash) > emissionProbability)
                    return;

                quantity = (ushort)math.clamp((int)math.round(math.lerp(1f, 24f, SmoothStep01(q)) * math.max(0.25f, intensity)), 1, 64);
                emitVfx = 1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct GenerateMockFloraDamageJob : IJobParallelFor
        {
            [NoAlias, NativeDisableParallelForRestriction] public NativeArray<FloraDestructionEventDTO> Events;
            public int Offset;
            public int Count;
            public double3 CenterAUP;
            public uint Seed;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Count || Offset + index >= Events.Length)
                    return;

                uint h = Seed ^ ((uint)index * 747796405u);
                float angle = Hash01(h) * 6.28318530718f;
                float radius01 = Hash01(h ^ 0x9E3779B9u);
                float radius = (radius01 * (2f - radius01)) * 7f;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                double3 offset = default;
                offset.x = cos * radius;
                offset.y = math.lerp(-0.8f, 0.8f, Hash01(h ^ 0x85EBCA6Bu));
                offset.z = sin * radius;
                FloraDestructionEventDTO eventDto = default;
                eventDto.ImpactAUP = CenterAUP + offset;
                eventDto.FloraTypeHash = DearLieSignalHashFlora;
                eventDto.MagnitudeBits = math.asuint(math.lerp(0.35f, 1f, Hash01(h ^ 0xC2B2AE35u)));
                Events[Offset + index] = eventDto;
            }
        }

        /// <summary>
        /// Applies one tool hit against the nearest active harvestable indirect-flora instance.
        /// </summary>
        public bool TryApplyToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (_dearLieJobScheduled ||
                deliveredDamage <= 0f ||
                vegetationBridge == null ||
                !_templateCacheReady)
                return false;

            if (!RefreshActiveCachesIfNeeded(force: false))
                return false;
            if (!TrySnapshotNearestHarvestTargetWithLock(
                hitPoint,
                Mathf.Max(hitSearchRadius, interactionBurstRadius),
                toolCapabilityMask,
                out bool underwater,
                out int activeIndex,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out Matrix4x4 instanceMatrix,
                out Vector3 instancePosition,
                out _,
                out _,
                out _,
                out _))
            {
                return false;
            }

            if (!TrySnapshotTemplateDescriptorWithLock(templateIndex, out HarvestableTemplate.RuntimeDescriptor hitTemplateDescriptor))
                return false;

            float baseHealth = Mathf.Max(0.1f, hitTemplateDescriptor.BaseHealth);
            float toolResistance = math.max(0.01f, hitTemplateDescriptor.ToolResistance);
            float damageAmount = deliveredDamage / toolResistance;
            bool emitDefensiveSporeBurst = ShouldDetonateDefensiveSporeBurst(templateIndex, toolCapabilityMask);
            float currentTime = ResolveOrganicClockSeconds();
            bool harvestStateChanged = false;
            bool destroyAfterHealthRecheck = false;
            HarvestState previousHarvestState = HarvestState.Pristine;
            HarvestState nextHarvestState = HarvestState.Pristine;
            bool hasStateOverrideRequest = false;
            bool clearStateOverrideRequest = false;
            float stateOverrideNormalizedHealth = 0f;
            byte stateOverrideHarvestState = 0;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            try
            {
                if ((_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
                {
                    return false;
                }

                if (!IsPinnedActiveLaneSlot(instanceUid, underwater, activeIndex))
                    return false;

                float pinnedCurrentHealth = GetLaneHealth(underwater, activeIndex);
                if (pinnedCurrentHealth <= 0.0001f)
                    return false;

                float previousHeightScale = ResolveCurrentNormalizedHeightScale(
                    underwater,
                    activeIndex,
                    instanceUid,
                    math.saturate(pinnedCurrentHealth / math.max(0.0001f, baseHealth)));
                previousHarvestState = ResolveHarvestState(templateIndex, baseHealth, pinnedCurrentHealth, previousHeightScale);
                float nextHealth = emitDefensiveSporeBurst
                    ? 0f
                    : Mathf.Max(0f, pinnedCurrentHealth - damageAmount);
                float nextHeightScale = ResolveNormalizedHeightScale(templateIndex, baseHealth, nextHealth);
                nextHarvestState = nextHealth > 0.0001f
                    ? ResolveHarvestState(templateIndex, baseHealth, nextHealth, nextHeightScale)
                    : HarvestState.Dead;
                harvestStateChanged = previousHarvestState != nextHarvestState;
                if (nextHealth <= 0.0001f)
                {
                    destroyAfterHealthRecheck = true;
                }
                else
                {
                    SetLaneHealth(underwater, activeIndex, nextHealth);
                    _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)nextHealth);
                    MarkOrganicTouched(instanceUid, currentTime);
                    ApplyDamageVisualState(instanceUid, underwater, activeIndex, templateIndex, baseHealth, nextHealth, nextHeightScale, harvestStateChanged, currentTime);
                    hasStateOverrideRequest = TryCacheFloraStateOverride(
                        instanceUid,
                        templateIndex,
                        underwater,
                        activeIndex,
                        baseHealth,
                        nextHealth,
                        out stateOverrideNormalizedHealth,
                        out stateOverrideHarvestState,
                        out clearStateOverrideRequest);
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (destroyAfterHealthRecheck)
            {
                bool destroyed = DestroyResolvedInstance(
                    underwater,
                    activeIndex,
                    instanceUid,
                    materialClass,
                    templateIndex,
                    instanceMatrix,
                    instancePosition,
                    hitPoint,
                    hitNormal,
                    normalizedPower);
                if (!destroyed)
                    return false;

                if (emitDefensiveSporeBurst)
                    floraInteractionManager?.RegisterDefensiveSporeBurst(instancePosition, Mathf.Max(0.35f, normalizedPower));

                if (harvestStateChanged)
                    DispatchHarvestAudioTransition(instanceUid, templateIndex, previousHarvestState, nextHarvestState, instancePosition);

                return true;
            }

            PublishExternalInteraction(hitPoint, direction * Mathf.Max(0.25f, normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius);
            if (harvestStateChanged)
                DispatchHarvestAudioTransition(instanceUid, templateIndex, previousHarvestState, nextHarvestState, instancePosition);

            if (hasStateOverrideRequest)
            {
                PublishFloraStateOverride(
                    instanceUid,
                    templateIndex,
                    instancePosition,
                    stateOverrideNormalizedHealth,
                    stateOverrideHarvestState,
                    clearStateOverrideRequest);
            }

            return true;
        }

        /// <summary>
        /// Consumes one nearby flora instance without spawning debris or loot, using the passive decomposition/tombstone path.
        /// </summary>
        internal bool TryConsumeFloraAtPosition(Vector3 worldPosition, float searchRadius, out uint instanceUid)
        {
            instanceUid = 0u;
            if (_dearLieJobScheduled || vegetationBridge == null || !_templateCacheReady)
                return false;

            if (!RefreshActiveCachesIfNeeded(force: false))
                return false;
            if (!TrySnapshotNearestHarvestTargetWithLock(
                worldPosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                0u,
                out bool underwater,
                out int activeIndex,
                out instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition,
                out _,
                out _,
                out _,
                out _))
            {
                return false;
            }

            if (materialClass == HarvestableTemplate.MaterialClass.None)
            {
                instanceUid = 0u;
                return false;
            }

            if (!ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition))
            {
                instanceUid = 0u;
                return false;
            }

            PublishExternalInteraction(instancePosition, Vector3.up * 0.15f, interactionBurstRadius);
            return true;
        }

        /// <summary>
        /// Applies non-harvest decomposition to any active indirect flora intersecting a newly placed construction envelope.
        /// </summary>
        internal int ApplyConstructionDecomposition(Vector3 runtimePosition, float radiusMeters)
        {
            if (_dearLieJobScheduled || !math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                return 0;

            if (!RefreshActiveCachesIfNeeded(force: false))
                return 0;
            double3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(runtimePosition);
            double radiusSq = (double)radiusMeters * radiusMeters;
            int decomposedCount = 0;
            decomposedCount += ApplyConstructionDecompositionInLane(false, universePosition, radiusSq);
            decomposedCount += ApplyConstructionDecompositionInLane(true, universePosition, radiusSq);
            return decomposedCount;
        }

        /// <summary>
        /// Instantly tombstones active consumable flora inside a persistent chemical dead zone.
        /// </summary>
        internal int ApplyDefoliantDeadZone(Vector3 runtimePosition, float radiusMeters)
        {
            if (_dearLieJobScheduled || !math.isfinite(radiusMeters) || radiusMeters <= 0f)
                return 0;

            if (vegetationBridge == null)
                return 0;

            if (!RefreshActiveCachesIfNeeded(force: false))
                return 0;
            double3 universePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(runtimePosition);
            double radiusSq = (double)radiusMeters * radiusMeters;
            int killedCount = 0;
            killedCount += ApplyDefoliantDeadZoneInLane(false, universePosition, radiusSq);
            killedCount += ApplyDefoliantDeadZoneInLane(true, universePosition, radiusSq);
            return killedCount;
        }

        private bool ShouldDetonateDefensiveSporeBurst(int templateIndex, uint toolCapabilityMask)
        {
            if (floraInteractionManager == null || templateIndex < 0)
                return false;

            uint burstTriggerMask = (uint)FloraDataTemplate.VulnerabilityMask.Cut | (uint)FloraDataTemplate.VulnerabilityMask.Drill;
            return (toolCapabilityMask & burstTriggerMask) != 0u &&
                   floraInteractionManager.IsDefensiveSporeBurstTemplateIndex(templateIndex);
        }

        private void EvaluateAllelopathicRelease()
        {
            Span<PassiveDecompositionCandidate> candidates = stackalloc PassiveDecompositionCandidate[MaxOrganicPassiveDecompositionStackBatch];
            int candidateCount = 0;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            try
            {
                NativeArray<Matrix4x4> matrices = _underwaterMatrices;
                NativeArray<HectonVegetationInstanceData> metadata = _underwaterMetadata;
                NativeArray<int> types = _underwaterTypes;
                NativeArray<int>.ReadOnly semanticTypes = _underwaterSemanticTypes;
                NativeArray<uint> instanceUids = _underwaterInstanceUids;
                NativeArray<byte> materialClasses = _underwaterMaterialClasses;
                int count = _underwaterCount;
                if (!matrices.IsCreated ||
                    !metadata.IsCreated ||
                    !types.IsCreated ||
                    !semanticTypes.IsCreated ||
                    !instanceUids.IsCreated ||
                    !materialClasses.IsCreated ||
                    count <= 0)
                {
                    _underwaterAllelopathicCoralScanCursor = 0;
                    _underwaterAllelopathicKelpScanCursor = 0;
                    return;
                }

                int safeCount = math.min(
                    count,
                    math.min(
                        math.min(matrices.Length, metadata.Length),
                        math.min(
                            math.min(types.Length, semanticTypes.Length),
                            math.min(instanceUids.Length, materialClasses.Length))));
                if (safeCount <= 0)
                {
                    _underwaterAllelopathicCoralScanCursor = 0;
                    _underwaterAllelopathicKelpScanCursor = 0;
                    return;
                }

                if ((uint)_underwaterAllelopathicCoralScanCursor >= (uint)safeCount)
                    _underwaterAllelopathicCoralScanCursor = 0;
                if ((uint)_underwaterAllelopathicKelpScanCursor >= (uint)safeCount)
                    _underwaterAllelopathicKelpScanCursor = 0;

                float qualityWeight = ResolveDearLieGlobalQualityWeight();
                int coralBudget = math.min(safeCount, ResolveAllelopathicCoralScanBudget(qualityWeight));
                int overcrowdingThreshold = Mathf.Max(1, Mathf.CeilToInt(allelopathicKelpCapacity * allelopathicThreshold01));
                int kelpBudget = math.min(safeCount, math.max(overcrowdingThreshold, ResolveAllelopathicKelpScanBudget(qualityWeight)));
                float radiusSq = allelopathicCellRadius * allelopathicCellRadius;
                for (int checkedCorals = 0; checkedCorals < coralBudget; checkedCorals++)
                {
                    int coralIndex = _underwaterAllelopathicCoralScanCursor;
                    _underwaterAllelopathicCoralScanCursor++;
                    if (_underwaterAllelopathicCoralScanCursor >= safeCount)
                        _underwaterAllelopathicCoralScanCursor = 0;

                    uint instanceUid = instanceUids[coralIndex];
                    if (IsLifecycleReadBlocked(instanceUid) ||
                        metadata[coralIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f)
                    {
                        continue;
                    }

                    HectonMapMagicVegetationBridge.VegetationSemanticType semanticType =
                        (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticTypes[coralIndex];
                    if (!HectonMapMagicVegetationBridge.IsColonyCoralSemanticType(semanticType))
                        continue;

                    Vector3 coralPosition = ExtractTranslation(matrices[coralIndex]);
                    int kelpCount = 0;
                    for (int checkedKelp = 0; checkedKelp < kelpBudget; checkedKelp++)
                    {
                        int kelpIndex = _underwaterAllelopathicKelpScanCursor;
                        _underwaterAllelopathicKelpScanCursor++;
                        if (_underwaterAllelopathicKelpScanCursor >= safeCount)
                            _underwaterAllelopathicKelpScanCursor = 0;

                        if (kelpIndex == coralIndex)
                            continue;

                        uint kelpInstanceUid = instanceUids[kelpIndex];
                        if (IsLifecycleReadBlocked(kelpInstanceUid) ||
                            types[kelpIndex] != (int)HectonVegetationInstanceType.GiantKelp ||
                            metadata[kelpIndex].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                            math.abs(metadata[kelpIndex].HeightScale) <= 0.0001f)
                        {
                            continue;
                        }

                        Vector3 kelpPosition = ExtractTranslation(matrices[kelpIndex]);
                        float deltaX = kelpPosition.x - coralPosition.x;
                        float deltaZ = kelpPosition.z - coralPosition.z;
                        if (deltaX * deltaX + deltaZ * deltaZ > radiusSq)
                            continue;

                        kelpCount++;
                        if (kelpCount < overcrowdingThreshold)
                            continue;

                        HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[coralIndex];
                        if (materialClass == HarvestableTemplate.MaterialClass.None)
                            break;

                        PassiveDecompositionCandidate candidate = default;
                        candidate.Underwater = 1;
                        candidate.ActiveIndex = coralIndex;
                        candidate.InstanceUid = instanceUid;
                        candidate.MaterialClass = materialClass;
                        candidate.TemplateIndex = ResolveTemplateIndex(metadata[coralIndex], materialClass);
                        candidate.RuntimePosition = coralPosition;
                        candidates[candidateCount] = candidate;
                        candidateCount++;
                        break;
                    }

                    if (candidateCount >= candidates.Length)
                        break;
                }
            }
            finally
            {
                ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
            }

            ApplyPassiveDecompositionCandidates(candidates, candidateCount);
        }

        private static int ResolveAllelopathicCoralScanBudget(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return math.max(
                AllelopathicMinCoralChecksPerSlowTick,
                (int)math.round(math.lerp(AllelopathicMinCoralChecksPerSlowTick, AllelopathicMaxCoralChecksPerSlowTick, q)));
        }

        private static int ResolveAllelopathicKelpScanBudget(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return math.max(
                AllelopathicMinKelpChecksPerCoral,
                (int)math.round(math.lerp(AllelopathicMinKelpChecksPerCoral, AllelopathicMaxKelpChecksPerCoral, q * q)));
        }

        private void BuildTemplateCaches()
        {
            FloraDataTemplate[] floraTemplates = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            bool hasFloraTemplates = floraTemplates != null && floraTemplates.Length > 0;
            int validTemplateCount = 0;
            int totalLootEntries = 0;

            if (hasFloraTemplates)
            {
                for (int i = 0; i < floraTemplates.Length; i++)
                {
                    FloraDataTemplate floraTemplate = floraTemplates[i];
                    HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                    if (floraTemplate == null || template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }
            else if (harvestTemplates != null)
            {
                for (int i = 0; i < harvestTemplates.Length; i++)
                {
                    HarvestableTemplate template = harvestTemplates[i];
                    if (template == null)
                        continue;

                    validTemplateCount++;
                    totalLootEntries += CountTemplateLootEntries(template);
                }
            }

            IDataVault vault = _dearLieVault;
            NativeArray<HarvestableTemplate.RuntimeDescriptor> existingTemplateDescriptors = default;
            NativeArray<HarvestableTemplate.LootRuntimeEntry> existingLootEntries = default;
            bool existingReady =
                _templateCacheReady &&
                _templateDescriptors.TryResolve(out existingTemplateDescriptors) &&
                existingTemplateDescriptors.IsCreated &&
                _lootEntries.TryResolve(out existingLootEntries) &&
                existingLootEntries.IsCreated;

            int descriptorCapacity = math.max(1, validTemplateCount);
            int lootCapacity = math.max(1, totalLootEntries);
            bool descriptorsLargeEnough = existingReady && existingTemplateDescriptors.Length >= descriptorCapacity;
            bool lootLargeEnough = existingReady && existingLootEntries.Length >= lootCapacity;
            bool canPreserveExistingCache = existingReady && descriptorsLargeEnough && lootLargeEnough;
            if (!canPreserveExistingCache)
                _templateCacheReady = false;

            if (validTemplateCount <= 0)
            {
                _templateCacheReady = false;
                _templateIndexByMaterialClass = Array.Empty<int>();
                _descriptorHarvestTemplates = Array.Empty<HarvestableTemplate>();
                _floraCategoryByDescriptorIndex = Array.Empty<byte>();
                _audioMaterialByDescriptorIndex = Array.Empty<byte>();
                _growthTimeSecondsByDescriptorIndex = Array.Empty<float>();
                _sporeAcousticEmitterByDescriptorIndex = Array.Empty<byte>();
                _sporeAcousticClipByDescriptorIndex = Array.Empty<AudioClip>();
                _sporePulseFrequencyByDescriptorIndex = Array.Empty<float>();
                _sporeAcousticVolumeByDescriptorIndex = Array.Empty<float>();
                _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();

                return;
            }

            if (vault == null)
            {
                _templateCacheReady = canPreserveExistingCache;
                return;
            }

            if (!_templateDescriptors.Ensure(vault, OrganicTemplateDescriptorsBufferId, descriptorCapacity, OrganicVaultSystemId, NativeArrayOptions.ClearMemory) ||
                !_lootEntries.Ensure(vault, OrganicLootEntriesBufferId, lootCapacity, OrganicVaultSystemId, NativeArrayOptions.ClearMemory))
            {
                _templateCacheReady = false;
                return;
            }

            int[] nextTemplateIndexByMaterialClass = new int[MaterialClassCount]; // COLD ALLOC: int[MaterialClassCount] - material-class to template-index lookup table - owner: DestructibleOrganicManager
            for (int i = 0; i < nextTemplateIndexByMaterialClass.Length; i++)
                nextTemplateIndexByMaterialClass[i] = -1;

            HarvestableTemplate[] nextDescriptorHarvestTemplates = new HarvestableTemplate[descriptorCapacity]; // COLD ALLOC: HarvestableTemplate[templateCount] - descriptor-to-authoring lookup for flora template harvest routing - owner: DestructibleOrganicManager
            byte[] nextFloraCategoryByDescriptorIndex = new byte[descriptorCapacity]; // COLD ALLOC: byte[templateCount] - flora-category cache used by harvest-state thresholds - owner: DestructibleOrganicManager
            byte[] nextAudioMaterialByDescriptorIndex = new byte[descriptorCapacity]; // COLD ALLOC: byte[templateCount] - flora audio-material routing cache used by harvest-state audio dispatch - owner: DestructibleOrganicManager
            float[] nextGrowthTimeSecondsByDescriptorIndex = new float[descriptorCapacity]; // COLD ALLOC: float[templateCount] - authored flora growth durations - owner: DestructibleOrganicManager
            byte[] nextSporeAcousticEmitterByDescriptorIndex = new byte[descriptorCapacity]; // COLD ALLOC: byte[templateCount] - mature spore acoustic emitter flags - owner: DestructibleOrganicManager
            AudioClip[] nextSporeAcousticClipByDescriptorIndex = new AudioClip[descriptorCapacity]; // COLD ALLOC: AudioClip[templateCount] - mature spore acoustic clip refs - owner: DestructibleOrganicManager
            float[] nextSporePulseFrequencyByDescriptorIndex = new float[descriptorCapacity]; // COLD ALLOC: float[templateCount] - mature spore pulse cadence copied from VAT authoring - owner: DestructibleOrganicManager
            float[] nextSporeAcousticVolumeByDescriptorIndex = new float[descriptorCapacity]; // COLD ALLOC: float[templateCount] - mature spore acoustic volume per descriptor - owner: DestructibleOrganicManager
            int[] nextHarvestDescriptorIndexByFloraTemplateIndex = hasFloraTemplates
                ? new int[floraTemplates.Length]
                : Array.Empty<int>(); // COLD ALLOC: int[floraTemplates.Length] - flora-template to descriptor mapping - owner: DestructibleOrganicManager

            if (hasFloraTemplates)
            {
                for (int i = 0; i < nextHarvestDescriptorIndexByFloraTemplateIndex.Length; i++)
                    nextHarvestDescriptorIndexByFloraTemplateIndex[i] = -1;
            }

            HarvestableTemplate.RuntimeDescriptor[] descriptorScratch = new HarvestableTemplate.RuntimeDescriptor[descriptorCapacity]; // COLD ALLOC: RuntimeDescriptor[templateCount] - prebuilt descriptor payload copied into vault under short lock - owner: DestructibleOrganicManager
            HarvestableTemplate.LootRuntimeEntry[] lootEntryScratch = new HarvestableTemplate.LootRuntimeEntry[lootCapacity]; // COLD ALLOC: LootRuntimeEntry[lootCount] - prebuilt loot payload copied into vault under short lock - owner: DestructibleOrganicManager
            int descriptorWriteIndex = 0;
            int lootWriteIndex = 0;
            NativeList<HarvestableTemplate.LootRuntimeEntry> lootScratch =
                new NativeList<HarvestableTemplate.LootRuntimeEntry>(byte.MaxValue, Allocator.Temp);
            int lootScratchSentinelId = 0;
            try
            {
                lootScratchSentinelId = NativeMemorySentinel.RegisterNativeListInstance(
                    lootScratch,
                    NativeMemoryOwner,
                    TemplateLootBuildScratchLabel,
                    NativeAllocationLifetime.Temp);
                if (lootScratchSentinelId <= 0)
                    throw new InvalidOperationException($"Native memory sentinel registration failed for {TemplateLootBuildScratchLabel}.");

                if (hasFloraTemplates)
                {
                    for (int i = 0; i < floraTemplates.Length; i++)
                    {
                        FloraDataTemplate floraTemplate = floraTemplates[i];
                        HarvestableTemplate template = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                        if (floraTemplate == null || template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < lootEntryScratch.Length; lootIndex++)
                        {
                            lootEntryScratch[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        int copiedLootCount = lootWriteIndex - lootStartIndex;
                        if (descriptorWriteIndex >= descriptorScratch.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        FloraDataTemplate.RuntimeDescriptor floraRuntimeDescriptor = floraTemplate.BuildRuntimeDescriptor();
                        descriptor.StableHashId = floraRuntimeDescriptor.StableHashId;
                        descriptor.BaseHealth = floraTemplate.MaxHealth;
                        descriptor.LootCount = (byte)math.min(byte.MaxValue, copiedLootCount);
                        descriptorScratch[descriptorWriteIndex] = descriptor;
                        nextDescriptorHarvestTemplates[descriptorWriteIndex] = template;
                        nextFloraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)floraTemplate.Category;
                        nextAudioMaterialByDescriptorIndex[descriptorWriteIndex] = floraTemplate.AudioMaterialID;
                        nextGrowthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = floraTemplate.GrowthTimeSeconds;
                        nextSporeAcousticEmitterByDescriptorIndex[descriptorWriteIndex] = floraTemplate.EmitsMatureSporeAcoustic ? (byte)1 : (byte)0;
                        nextSporeAcousticClipByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticClip;
                        nextSporePulseFrequencyByDescriptorIndex[descriptorWriteIndex] = floraTemplate.PulseFrequency;
                        nextSporeAcousticVolumeByDescriptorIndex[descriptorWriteIndex] = floraTemplate.MatureSporeAcousticVolume;
                        nextHarvestDescriptorIndexByFloraTemplateIndex[i] = descriptorWriteIndex;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)nextTemplateIndexByMaterialClass.Length && nextTemplateIndexByMaterialClass[materialIndex] < 0)
                            nextTemplateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
                else if (harvestTemplates != null)
                {
                    for (int i = 0; i < harvestTemplates.Length; i++)
                    {
                        HarvestableTemplate template = harvestTemplates[i];
                        if (template == null)
                            continue;

                        int lootStartIndex = lootWriteIndex;
                        lootScratch.Clear();
                        template.CopyLootTableNonAlloc(lootScratch);
                        for (int lootIndex = 0; lootIndex < lootScratch.Length && lootWriteIndex < lootEntryScratch.Length; lootIndex++)
                        {
                            lootEntryScratch[lootWriteIndex] = lootScratch[lootIndex];
                            lootWriteIndex++;
                        }

                        int copiedLootCount = lootWriteIndex - lootStartIndex;
                        if (descriptorWriteIndex >= descriptorScratch.Length)
                            continue;

                        HarvestableTemplate.RuntimeDescriptor descriptor = template.BuildRuntimeDescriptor(lootStartIndex);
                        descriptor.LootCount = (byte)math.min(byte.MaxValue, copiedLootCount);
                        descriptorScratch[descriptorWriteIndex] = descriptor;
                        nextDescriptorHarvestTemplates[descriptorWriteIndex] = template;
                        nextFloraCategoryByDescriptorIndex[descriptorWriteIndex] = (byte)InferCategoryFromMaterialClass(template.TemplateMaterialClass);
                        nextGrowthTimeSecondsByDescriptorIndex[descriptorWriteIndex] = 480f;

                        int materialIndex = descriptor.MaterialClassId;
                        if ((uint)materialIndex < (uint)nextTemplateIndexByMaterialClass.Length && nextTemplateIndexByMaterialClass[materialIndex] < 0)
                            nextTemplateIndexByMaterialClass[materialIndex] = descriptorWriteIndex;

                        descriptorWriteIndex++;
                    }
                }
            }
            finally
            {
                Exception cleanupException = null;

                if (lootScratchSentinelId > 0)
                {
                    try
                    {
                        NativeMemorySentinel.Unregister(lootScratchSentinelId);
                    }
                    catch (Exception exception)
                    {
                        cleanupException = exception;
                    }
                    finally
                    {
                        lootScratchSentinelId = 0;
                    }
                }

                if (lootScratch.IsCreated)
                {
                    try
                    {
                        lootScratch.Dispose();
                    }
                    catch (Exception exception)
                    {
                        if (cleanupException == null)
                            cleanupException = exception;
                    }
                    finally
                    {
                        lootScratch = default;
                    }
                }
                else
                {
                    lootScratch = default;
                }

                if (cleanupException != null)
                    throw cleanupException;
            }

            bool cacheBuilt = descriptorWriteIndex > 0;
            if (!cacheBuilt)
            {
                _templateCacheReady = canPreserveExistingCache;
                return;
            }

            cacheBuilt = false;
            bool wroteTemplateLane = false;
            bool wroteLootLane = false;
            bool lootCacheWritten = WriteLootEntryCacheGuarded(
                vault,
                lootEntryScratch,
                lootWriteIndex,
                lootCapacity,
                out wroteLootLane);
            bool descriptorCacheWritten =
                lootCacheWritten &&
                WriteTemplateDescriptorCacheGuarded(
                    vault,
                    descriptorScratch,
                    descriptorWriteIndex,
                    descriptorCapacity,
                    out wroteTemplateLane);
            cacheBuilt = lootCacheWritten && descriptorCacheWritten;

            if (!cacheBuilt)
            {
                _templateCacheReady = (wroteLootLane || wroteTemplateLane) ? false : canPreserveExistingCache;
                return;
            }

            _templateIndexByMaterialClass = nextTemplateIndexByMaterialClass;
            _descriptorHarvestTemplates = nextDescriptorHarvestTemplates;
            _floraCategoryByDescriptorIndex = nextFloraCategoryByDescriptorIndex;
            _audioMaterialByDescriptorIndex = nextAudioMaterialByDescriptorIndex;
            _growthTimeSecondsByDescriptorIndex = nextGrowthTimeSecondsByDescriptorIndex;
            _sporeAcousticEmitterByDescriptorIndex = nextSporeAcousticEmitterByDescriptorIndex;
            _sporeAcousticClipByDescriptorIndex = nextSporeAcousticClipByDescriptorIndex;
            _sporePulseFrequencyByDescriptorIndex = nextSporePulseFrequencyByDescriptorIndex;
            _sporeAcousticVolumeByDescriptorIndex = nextSporeAcousticVolumeByDescriptorIndex;
            _harvestDescriptorIndexByFloraTemplateIndex = nextHarvestDescriptorIndexByFloraTemplateIndex;
            _templateCacheReady = true;
        }

        private bool WriteTemplateDescriptorCacheGuarded(
            IDataVault vault,
            HarvestableTemplate.RuntimeDescriptor[] descriptorScratch,
            int descriptorWriteIndex,
            int descriptorCapacity,
            out bool wroteLane)
        {
            wroteLane = false;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicTemplateDescriptorsBufferId, out ulong guardMask))
                return false;

            try
            {
                if (!_templateDescriptors.TryResolve(out NativeArray<HarvestableTemplate.RuntimeDescriptor> templateDescriptors) ||
                    !templateDescriptors.IsCreated ||
                    templateDescriptors.Length < descriptorCapacity)
                {
                    return false;
                }

                for (int i = 0; i < templateDescriptors.Length; i++)
                    templateDescriptors[i] = default;

                int safeDescriptorCount = math.min(descriptorWriteIndex, templateDescriptors.Length);
                for (int i = 0; i < safeDescriptorCount; i++)
                    templateDescriptors[i] = descriptorScratch[i];

                wroteLane = true;
                return safeDescriptorCount > 0;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool WriteLootEntryCacheGuarded(
            IDataVault vault,
            HarvestableTemplate.LootRuntimeEntry[] lootEntryScratch,
            int lootWriteIndex,
            int lootCapacity,
            out bool wroteLane)
        {
            wroteLane = false;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicLootEntriesBufferId, out ulong guardMask))
                return false;

            try
            {
                if (!_lootEntries.TryResolve(out NativeArray<HarvestableTemplate.LootRuntimeEntry> lootEntries) ||
                    !lootEntries.IsCreated ||
                    lootEntries.Length < lootCapacity)
                {
                    return false;
                }

                for (int i = 0; i < lootEntries.Length; i++)
                    lootEntries[i] = default;

                int safeLootCount = math.min(lootWriteIndex, lootEntries.Length);
                for (int i = 0; i < safeLootCount; i++)
                    lootEntries[i] = lootEntryScratch[i];

                wroteLane = true;
                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private int ApplyConstructionDecompositionInLane(bool underwater, double3 centerUniversePosition, double radiusSq)
        {
            Span<PassiveDecompositionCandidate> candidates = stackalloc PassiveDecompositionCandidate[MaxOrganicPassiveDecompositionStackBatch];
            int decomposedCount = 0;
            int startIndex = 0;
            while (true)
            {
                int candidateCount = 0;
                int safeCount = 0;
                IDataVault vault = _dearLieVault;
                if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                {
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                    return decomposedCount;
                }

                try
                {
                    NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                    NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
                    NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
                    NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
                    NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
                    NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
                    int count = underwater ? _underwaterCount : _surfaceCount;
                    if (!matrices.IsCreated ||
                        !metadata.IsCreated ||
                        !types.IsCreated ||
                        !semanticTypes.IsCreated ||
                        !instanceUids.IsCreated ||
                        !materialClasses.IsCreated ||
                        count <= 0)
                    {
                        return decomposedCount;
                    }

                    safeCount = math.min(
                        count,
                        math.min(
                            math.min(matrices.Length, metadata.Length),
                            math.min(
                                math.min(types.Length, semanticTypes.Length),
                                math.min(instanceUids.Length, materialClasses.Length))));
                    if (startIndex >= safeCount)
                        return decomposedCount;

                    for (int i = startIndex; i < safeCount; i++)
                    {
                        startIndex = i + 1;
                        uint instanceUid = instanceUids[i];
                        if (IsLifecycleReadBlocked(instanceUid))
                            continue;

                        HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                        if (materialClass == HarvestableTemplate.MaterialClass.None)
                            continue;

                        Vector3 rootPosition = ExtractTranslation(matrices[i]);
                        double distanceSq = ResolveConstructionDistanceSq(centerUniversePosition, rootPosition, metadata[i], types[i]);
                        if (distanceSq > radiusSq)
                            continue;

                        PassiveDecompositionCandidate candidate = default;
                        candidate.Underwater = underwater ? (byte)1 : (byte)0;
                        candidate.ActiveIndex = i;
                        candidate.InstanceUid = instanceUid;
                        candidate.MaterialClass = materialClass;
                        candidate.TemplateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                        candidate.RuntimePosition = rootPosition;
                        candidates[candidateCount] = candidate;
                        candidateCount++;
                        if (candidateCount >= candidates.Length)
                            break;
                    }
                }
                finally
                {
                    ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
                }

                if (candidateCount <= 0)
                    return decomposedCount;

                decomposedCount += ApplyPassiveDecompositionCandidates(candidates, candidateCount);
                if (startIndex >= safeCount)
                    return decomposedCount;
            }
        }

        private int ApplyDefoliantDeadZoneInLane(bool underwater, double3 centerUniversePosition, double radiusSq)
        {
            Span<PassiveDecompositionCandidate> candidates = stackalloc PassiveDecompositionCandidate[MaxOrganicPassiveDecompositionStackBatch];
            int killedCount = 0;
            int startIndex = 0;
            while (true)
            {
                int candidateCount = 0;
                int safeCount = 0;
                IDataVault vault = _dearLieVault;
                if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                {
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                    return killedCount;
                }

                try
                {
                    NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                    NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
                    NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
                    NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
                    NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
                    NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
                    int count = underwater ? _underwaterCount : _surfaceCount;
                    if (!matrices.IsCreated ||
                        !metadata.IsCreated ||
                        !types.IsCreated ||
                        !semanticTypes.IsCreated ||
                        !instanceUids.IsCreated ||
                        !materialClasses.IsCreated ||
                        count <= 0)
                    {
                        return killedCount;
                    }

                    safeCount = math.min(
                        count,
                        math.min(
                            math.min(matrices.Length, metadata.Length),
                            math.min(
                                math.min(types.Length, semanticTypes.Length),
                                math.min(instanceUids.Length, materialClasses.Length))));
                    if (startIndex >= safeCount)
                        return killedCount;

                    for (int i = startIndex; i < safeCount; i++)
                    {
                        startIndex = i + 1;
                        uint instanceUid = instanceUids[i];
                        if (IsLifecycleReadBlocked(instanceUid))
                            continue;

                        HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                        if (!IsConsumableFloraMaterialClass(materialClass))
                            continue;

                        Vector3 rootPosition = ExtractTranslation(matrices[i]);
                        if (!IsFiniteVector(rootPosition))
                            continue;

                        double3 rootUniversePosition = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(rootPosition);
                        if (math.lengthsq(rootUniversePosition - centerUniversePosition) > radiusSq)
                            continue;

                        PassiveDecompositionCandidate candidate = default;
                        candidate.Underwater = underwater ? (byte)1 : (byte)0;
                        candidate.ActiveIndex = i;
                        candidate.InstanceUid = instanceUid;
                        candidate.MaterialClass = materialClass;
                        candidate.TemplateIndex = ResolveTemplateIndex(metadata[i], materialClass);
                        candidate.RuntimePosition = rootPosition;
                        candidates[candidateCount] = candidate;
                        candidateCount++;
                        if (candidateCount >= candidates.Length)
                            break;
                    }
                }
                finally
                {
                    ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
                }

                if (candidateCount <= 0)
                    return killedCount;

                killedCount += ApplyPassiveDecompositionCandidates(candidates, candidateCount);
                if (startIndex >= safeCount)
                    return killedCount;
            }
        }

        private void BuildFloraTemplateHarvestMap()
        {
            if (_harvestDescriptorIndexByFloraTemplateIndex != null && _harvestDescriptorIndexByFloraTemplateIndex.Length > 0)
                return;

            FloraDataTemplate[] floraTemplateAssets = vegetationBridge != null ? vegetationBridge.FloraTemplates : null;
            if (floraTemplateAssets == null || floraTemplateAssets.Length == 0)
            {
                _harvestDescriptorIndexByFloraTemplateIndex = Array.Empty<int>();
                return;
            }

            // COLD ALLOC: int[floraTemplateAssets.Length] - flora-template to harvest-descriptor lookup for instance-specific loot routing - owner: DestructibleOrganicManager
            int[] mapping = new int[floraTemplateAssets.Length];
            for (int i = 0; i < mapping.Length; i++)
                mapping[i] = -1;

            for (int i = 0; i < floraTemplateAssets.Length; i++)
            {
                FloraDataTemplate floraTemplate = floraTemplateAssets[i];
                HarvestableTemplate harvestTemplate = floraTemplate != null ? floraTemplate.HarvestTemplate : null;
                if (harvestTemplate == null || _descriptorHarvestTemplates == null)
                    continue;

                for (int descriptorIndex = 0; descriptorIndex < _descriptorHarvestTemplates.Length; descriptorIndex++)
                {
                    if (_descriptorHarvestTemplates[descriptorIndex] != harvestTemplate)
                        continue;

                    mapping[i] = descriptorIndex;
                    break;
                }
            }

            _harvestDescriptorIndexByFloraTemplateIndex = mapping;
        }

        private static FloraDataTemplate.FloraCategory InferCategoryFromMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            switch (materialClass)
            {
                case HarvestableTemplate.MaterialClass.Kelp:
                    return FloraDataTemplate.FloraCategory.HarvestableKelp;
                case HarvestableTemplate.MaterialClass.Coral:
                case HarvestableTemplate.MaterialClass.TitaniumOutcrop:
                    return FloraDataTemplate.FloraCategory.HardCoral;
                case HarvestableTemplate.MaterialClass.Sargassum:
                    return FloraDataTemplate.FloraCategory.GiantSargassum;
                default:
                    return FloraDataTemplate.FloraCategory.MicroGrass;
            }
        }

        private void BuildYieldMaterialLut()
        {
            IDataVault vault = _dearLieVault;
            if (vault == null)
            {
                _yieldMaterialLutReady = _yieldMaterialLutReady &&
                    _yieldMaterialLut.TryResolve(out NativeArray<EntropyYieldMaterialLutEntry> existingWithoutVault) &&
                    existingWithoutVault.IsCreated &&
                    existingWithoutVault.Length >= MaterialClassCount;
                return;
            }

            bool existingReady =
                _yieldMaterialLutReady &&
                _yieldMaterialLut.TryResolve(out NativeArray<EntropyYieldMaterialLutEntry> existingMaterialLut) &&
                existingMaterialLut.IsCreated &&
                existingMaterialLut.Length >= MaterialClassCount;

            if (!_yieldMaterialLut.Ensure(vault, OrganicYieldMaterialLutBufferId, math.max(1, MaterialClassCount), OrganicVaultSystemId, NativeArrayOptions.ClearMemory))
            {
                _yieldMaterialLutReady = false;
                return;
            }

            if (!TryAcquireOrganicBufferGuard(vault, OrganicYieldMaterialLutBufferId, out ulong guardMask))
            {
                _yieldMaterialLutReady = existingReady;
                return;
            }

            bool built = false;
            try
            {
                if (!_yieldMaterialLut.TryResolve(out NativeArray<EntropyYieldMaterialLutEntry> materialLut))
                {
                    built = false;
                }
                else
                {
                    for (int i = 0; i < materialLut.Length; i++)
                        materialLut[i] = default;

                    WriteYieldMaterialLut(materialLut, HarvestableTemplate.MaterialClass.None, 1000f, 1f, 0.5f, 0f);
                    WriteYieldMaterialLut(materialLut, HarvestableTemplate.MaterialClass.Kelp, 460f, 1.2f, 0.58f, 0.08f);
                    WriteYieldMaterialLut(materialLut, HarvestableTemplate.MaterialClass.Coral, 1320f, 2.5f, 0.65f, 0.16f);
                    WriteYieldMaterialLut(materialLut, HarvestableTemplate.MaterialClass.TitaniumOutcrop, 4480f, 4.5f, 0.78f, 0.22f);
                    WriteYieldMaterialLut(materialLut, HarvestableTemplate.MaterialClass.Sargassum, 310f, 1.0f, 0.52f, 0.05f);
                    built = true;
                }
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }

            _yieldMaterialLutReady = built;
        }

        private static void WriteYieldMaterialLut(
            NativeArray<EntropyYieldMaterialLutEntry> materialLut,
            HarvestableTemplate.MaterialClass materialClass,
            float densityKgPerM3,
            float unitItemMassKg,
            float minimumRecovery,
            float qualityBias)
        {
            if (!materialLut.IsCreated)
                return;

            int materialIndex = (int)materialClass;
            if (materialIndex < 0 || materialIndex >= materialLut.Length)
                return;

            EntropyYieldMaterialLutEntry entry = default;
            entry.DensityKgPerM3 = Mathf.Max(0.01f, densityKgPerM3);
            entry.UnitItemMassKg = Mathf.Max(0.01f, unitItemMassKg);
            entry.MinimumRecovery = Mathf.Clamp01(minimumRecovery);
            entry.QualityBias = Mathf.Clamp01(qualityBias);
            materialLut[materialIndex] = entry;
        }

        private static int CountTemplateLootEntries(HarvestableTemplate template)
        {
            return template != null ? template.CountRuntimeLootEntries(byte.MaxValue) : 0;
        }

        private bool RefreshActiveCachesIfNeeded(bool force, bool allowMutation = true)
        {
            if (vegetationBridge == null)
                return false;

            bool syncSurface = force || _surfaceRevision != vegetationBridge.ActiveSurfaceAggregateRevision;
            bool syncUnderwater = force || _underwaterRevision != vegetationBridge.ActiveUnderwaterAggregateRevision;
            if (!syncSurface && !syncUnderwater)
                return true;

            if (!allowMutation)
                return false;

            if (syncSurface)
                SyncLane(false);

            if (syncUnderwater)
                SyncLane(true);

            return true;
        }

        private void SyncLane(bool underwater)
        {
            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            NativeArray<int>.ReadOnly semanticTypes;
            int count;
            int semanticCount;
            bool hasNativePayload;
            bool hasSemanticPayload;
            if (underwater)
            {
                hasNativePayload = vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveUnderwaterSemanticPayload(out semanticTypes, out _, out semanticCount);
            }
            else
            {
                hasNativePayload = vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
                hasSemanticPayload = vegetationBridge.TryGetActiveSurfaceSemanticPayload(out semanticTypes, out _, out semanticCount);
            }

            int safeCount = hasNativePayload && hasSemanticPayload && count > 0
                ? math.min(
                    count,
                    math.min(
                        math.min(matrices.Length, metadata.Length),
                        math.min(types.Length, math.min(semanticTypes.Length, semanticCount))))
                : 0;
            if (!hasNativePayload || !hasSemanticPayload || count <= 0 || safeCount != count)
            {
                if (underwater)
                {
                    _underwaterCount = 0;
                    _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
                }
                else
                {
                    _surfaceCount = 0;
                    _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
                }

                if (count > 0 && safeCount != count)
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, math.max(1, count - math.max(0, safeCount)), 0, 0f, 0u, 128);

                return;
            }

            NativeArray<uint> instanceUids = underwater
                ? EnsureLaneCapacity(ref _underwaterInstanceUids, OrganicUnderwaterInstanceUidsBufferId, safeCount, nameof(_underwaterInstanceUids))
                : EnsureLaneCapacity(ref _surfaceInstanceUids, OrganicSurfaceInstanceUidsBufferId, safeCount, nameof(_surfaceInstanceUids));
            NativeArray<byte> materialClasses = underwater
                ? EnsureLaneCapacity(ref _underwaterMaterialClasses, OrganicUnderwaterMaterialClassesBufferId, safeCount, nameof(_underwaterMaterialClasses))
                : EnsureLaneCapacity(ref _surfaceMaterialClasses, OrganicSurfaceMaterialClassesBufferId, safeCount, nameof(_surfaceMaterialClasses));
            NativeArray<Unity.Mathematics.half> health = underwater
                ? EnsureLaneCapacity(ref _underwaterHealth, OrganicUnderwaterHealthBufferId, safeCount, nameof(_underwaterHealth))
                : EnsureLaneCapacity(ref _surfaceHealth, OrganicSurfaceHealthBufferId, safeCount, nameof(_surfaceHealth));
            bool dearLieLaneReady = underwater
                ? EnsureDearLieVaultLaneCapacity(true, safeCount)
                : EnsureDearLieVaultLaneCapacity(false, safeCount);
            if (!dearLieLaneReady ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                instanceUids.Length < safeCount ||
                materialClasses.Length < safeCount ||
                health.Length < safeCount)
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return;
            }

            int defoliantRegistryCount = 0;
            int defoliantRegistryOverflow = 0;
            try
            {
                float currentTime = ResolveOrganicClockSeconds();

                for (int i = 0; i < safeCount; i++)
                {
                    uint instanceUid = ComputeStableInstanceUid(matrices[i], metadata[i], types[i], semanticTypes[i]);
                    HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(types[i], semanticTypes[i]);
                    int templateIndex = ResolveTemplateIndex(metadata[i], fallbackMaterialClass);
                    bool hasLaneDescriptor = TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor laneDescriptor);
                    HarvestableTemplate.MaterialClass materialClass = hasLaneDescriptor
                        ? (HarvestableTemplate.MaterialClass)laneDescriptor.MaterialClassId
                        : fallbackMaterialClass;
                    instanceUids[i] = instanceUid;
                    materialClasses[i] = (byte)materialClass;
                    CacheBaseScale(instanceUid, metadata[i]);
                    PrimeUntouchedClock(instanceUid, currentTime);
                    byte runtimeFlags = EnsureRuntimeFlags(instanceUid, materialClass, semanticTypes[i], metadata[i].RuntimeFlags);
                    ApplyRuntimeFlags(ref metadata, i, runtimeFlags);
                    SetRuntimeState(ref metadata, i, HectonVegetationInstanceData.RuntimeStateIdle);
                    float defaultHealth = hasLaneDescriptor
                        ? Mathf.Max(0.1f, laneDescriptor.BaseHealth)
                        : 1f;
                    float resolvedHealth = defaultHealth;
                    bool hasPersistedFloraState = TryResolvePersistedFloraState(instanceUid, out float persistedHealth01, out float persistedHeightScale01);
                    Vector3 rootPosition = ExtractTranslation(matrices[i]);
                    float regrowthProgress = 0f;
                    bool isRegrowing = _regrowthProgressByInstanceUid.IsCreated &&
                                       _regrowthProgressByInstanceUid.TryGetValue(instanceUid, out regrowthProgress);
                    bool isDestroyed = _destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid);
                    bool isDefoliantSuppressed = !isDestroyed &&
                                                 !isRegrowing &&
                                                 IsConsumableFloraMaterialClass(materialClass) &&
                                                 ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZone(rootPosition);
                    if (isDefoliantSuppressed)
                    {
                        bool defoliantRegistered = false;
                        if (TryRegisterDefoliantDestroyedInstance(instanceUid, templateIndex, out ulong defoliantTemplateHash, out bool defoliantDestroyedMapWriteFailed))
                        {
                            defoliantRegistered = true;
                            if (defoliantRegistryCount < MaxOrganicCacheSyncRegistryBatch)
                            {
                                _cacheSyncDestroyedRegistryUids[defoliantRegistryCount] = instanceUid;
                                _cacheSyncDestroyedRegistryHashes[defoliantRegistryCount] = defoliantTemplateHash;
                                _cacheSyncDestroyedRegistryPositions[defoliantRegistryCount] = rootPosition;
                                defoliantRegistryCount++;
                            }
                            else
                            {
                                defoliantRegistryOverflow++;
                            }
                        }
                        else if (defoliantDestroyedMapWriteFailed)
                        {
                            defoliantRegistryOverflow++;
                        }

                        if (defoliantRegistered)
                        {
                            ApplyRuntimeFlags(ref metadata, i, MarkDeadRuntimeFlag(instanceUid));
                            isDestroyed = true;
                        }
                    }
                    if (_healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half savedHealth))
                        resolvedHealth = math.max(0f, (float)savedHealth);
                    else if (hasPersistedFloraState && templateIndex >= 0)
                        resolvedHealth = Mathf.Max(0f, defaultHealth * Mathf.Clamp01(persistedHealth01));

                    if (isDestroyed && !isRegrowing)
                        resolvedHealth = 0f;

                    if (_healthByInstanceUid.IsCreated)
                        _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)resolvedHealth);

                    health[i] = (Unity.Mathematics.half)resolvedHealth;
                    if (isRegrowing)
                    {
                        if (_damageVisualProgressByInstanceUid.IsCreated)
                            _damageVisualProgressByInstanceUid.Remove(instanceUid);

                        ApplyRegrowthVisualToLaneInstance(underwater, i, instanceUid, regrowthProgress);
                    }
                    else if (isDestroyed || resolvedHealth <= 0.0001f)
                    {
                        if (_damageVisualProgressByInstanceUid.IsCreated)
                            _damageVisualProgressByInstanceUid.Remove(instanceUid);
                        float entropy01 = EnsureDecompositionProgress(instanceUid, currentTime);
                        ApplyDearLieMatrixScaleZero(ref matrices, i);
                        ApplyDecompositionMetadata(ref metadata, i, instanceUid, entropy01);
                    }
                    else if (templateIndex >= 0 && resolvedHealth < defaultHealth)
                    {
                        float damage01 = ResolveDamageProgress(defaultHealth, resolvedHealth);
                        UpdateDamageProgressCache(instanceUid, damage01);
                        float normalizedHeightScale = hasPersistedFloraState
                            ? Mathf.Clamp01(persistedHeightScale01)
                            : ResolveNormalizedHeightScale(templateIndex, defaultHealth, resolvedHealth);
                        ApplyPersistedDamageMetadata(ref metadata, i, instanceUid, templateIndex, persistedHealth01, normalizedHeightScale, damage01, currentTime);
                    }
                    else if (_damageVisualProgressByInstanceUid.IsCreated)
                    {
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    }

                    if (!isRegrowing && !isDestroyed && resolvedHealth > 0.0001f)
                    {
                        float maturationScale = ResolveMaturationScaleMultiplier(instanceUid);
                        if (maturationScale < 0.9999f)
                            ApplyMaturationVisualToLaneInstance(underwater, i, instanceUid, ResolveMaturationYieldMultiplier(instanceUid), maturationScale);
                    }
                }

                if (underwater)
                {
                    _underwaterCount = safeCount;
                    _underwaterRevision = vegetationBridge.ActiveUnderwaterAggregateRevision;
                }
                else
                {
                    _surfaceCount = safeCount;
                    _surfaceRevision = vegetationBridge.ActiveSurfaceAggregateRevision;
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            FlushCacheSyncDestroyedRegistry(defoliantRegistryCount);
            if (defoliantRegistryOverflow > 0)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, defoliantRegistryOverflow, 0, 0f, 0u, 128);
        }

        private void FlushCacheSyncDestroyedRegistry(int count)
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            int safeCount = math.min(math.max(0, count), _cacheSyncDestroyedRegistryUids.Length);
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = _cacheSyncDestroyedRegistryUids[i];
                if (registry != null && instanceUid != 0u)
                {
                    registry.TryClearFloraStateOverride(instanceUid);
                    registry.TryRegisterDestroyedFlora(_cacheSyncDestroyedRegistryHashes[i], instanceUid, _cacheSyncDestroyedRegistryPositions[i]);
                }

                _cacheSyncDestroyedRegistryUids[i] = 0u;
                _cacheSyncDestroyedRegistryHashes[i] = 0UL;
                _cacheSyncDestroyedRegistryPositions[i] = default;
            }
        }

        private bool SyncDestroyedFloraFromPersistence()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null || !_destroyedByInstanceUid.IsCreated)
                return false;

            int copiedDestroyedCount = registry.CopyDestroyedFloraDeltas(
                _destroyedFloraPersistenceScratch,
                _destroyedFloraPersistenceScratch.Length);
            bool destroyedScratchSaturated = copiedDestroyedCount >= _destroyedFloraPersistenceScratch.Length;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lifecycleMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return false;
            }

            int staleDestroyedCount = 0;
            int staleDestroyedOverflow = 0;
            bool destroyedStateChanged = false;
            try
            {
                if (destroyedScratchSaturated)
                    staleDestroyedOverflow++;

                int safeDestroyedCount = math.min(copiedDestroyedCount, _destroyedFloraPersistenceScratch.Length);
                for (int i = 0; i < safeDestroyedCount; i++)
                {
                    PersistentWorldDeltaRecord record = _destroyedFloraPersistenceScratch[i];
                    if (record.InstanceUid == 0u)
                        continue;

                    if (!TryFindPinnedTemplateDescriptorByPersistentHash(record.ItemPersistentIdHash, out _, out _))
                    {
                        if (staleDestroyedCount < _staleDestroyedRegistryClearUids.Length)
                            _staleDestroyedRegistryClearUids[staleDestroyedCount++] = record.InstanceUid;
                        else
                            staleDestroyedOverflow++;
                        continue;
                    }

                    if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid))
                    {
                        if (staleDestroyedCount < _staleDestroyedRegistryClearUids.Length)
                            _staleDestroyedRegistryClearUids[staleDestroyedCount++] = record.InstanceUid;
                        else
                            staleDestroyedOverflow++;
                        continue;
                    }

                    if (destroyedScratchSaturated)
                        continue;

                    bool hadPersistedOverride =
                        (_persistedHealth01ByInstanceUid.IsCreated && _persistedHealth01ByInstanceUid.ContainsKey(record.InstanceUid)) ||
                        (_persistedHeightScale01ByInstanceUid.IsCreated && _persistedHeightScale01ByInstanceUid.ContainsKey(record.InstanceUid));
                    if (hadPersistedOverride)
                    {
                        ClearPersistedFloraStateOverride(record.InstanceUid);
                        destroyedStateChanged = true;
                    }

                    if (!_destroyedByInstanceUid.ContainsKey(record.InstanceUid))
                    {
                        if (!_destroyedByInstanceUid.TryAdd(record.InstanceUid, 1))
                        {
                            staleDestroyedOverflow++;
                            continue;
                        }

                        destroyedStateChanged = true;
                        ClearOrganicLifecycleState(record.InstanceUid);
                        PrimeDecompositionState(record.InstanceUid, ResolveOrganicClockSeconds() - OrganicDecompositionDurationSeconds);
                    }

                    if (!_healthByInstanceUid.TryPut(record.InstanceUid, (Unity.Mathematics.half)0f))
                        staleDestroyedOverflow++;
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lifecycleMask);
            }

            for (int i = 0; i < staleDestroyedCount; i++)
            {
                registry.TryClearDestroyedFlora(_staleDestroyedRegistryClearUids[i]);
                _staleDestroyedRegistryClearUids[i] = 0u;
            }

            if (staleDestroyedOverflow > 0)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, staleDestroyedOverflow, 0, 0f, 0u, 128);

            return destroyedStateChanged;
        }

        private bool SyncFloraStateOverridesFromPersistence()
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null ||
                !_persistedHealth01ByInstanceUid.IsCreated ||
                !_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                return false;
            }

            int copiedOverrideCount = registry.CopyFloraStateOverrideDeltas(
                _floraStateOverridePersistenceScratch,
                _floraStateOverridePersistenceScratch.Length);
            bool overrideScratchSaturated = copiedOverrideCount >= _floraStateOverridePersistenceScratch.Length;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lifecycleMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 32);
                return false;
            }

            int staleOverrideCount = 0;
            int staleOverrideOverflow = 0;
            bool overrideStateChanged = false;
            try
            {
                if (overrideScratchSaturated)
                    staleOverrideOverflow++;

                int safeOverrideCount = math.min(copiedOverrideCount, _floraStateOverridePersistenceScratch.Length);
                for (int i = 0; i < safeOverrideCount; i++)
                {
                    PersistentWorldDeltaRecord record = _floraStateOverridePersistenceScratch[i];
                    if (record.InstanceUid == 0u)
                        continue;

                    if ((_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(record.InstanceUid)) ||
                        (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(record.InstanceUid)))
                    {
                        bool hadBlockedOverride =
                            _persistedHealth01ByInstanceUid.ContainsKey(record.InstanceUid) ||
                            _persistedHeightScale01ByInstanceUid.ContainsKey(record.InstanceUid);
                        ClearPersistedFloraStateOverride(record.InstanceUid);
                        if (hadBlockedOverride)
                            overrideStateChanged = true;

                        if (staleOverrideCount < _staleFloraStateRegistryClearUids.Length)
                            _staleFloraStateRegistryClearUids[staleOverrideCount++] = record.InstanceUid;
                        else
                            staleOverrideOverflow++;
                        continue;
                    }

                    if (!TryFindPinnedTemplateDescriptorByPersistentHash(record.ItemPersistentIdHash, out int descriptorIndex, out _))
                    {
                        if (staleOverrideCount < _staleFloraStateRegistryClearUids.Length)
                            _staleFloraStateRegistryClearUids[staleOverrideCount++] = record.InstanceUid;
                        else
                            staleOverrideOverflow++;
                        continue;
                    }

                    PersistentWorldRegistry.UnpackFloraStateOverride(record.Quantity, out float persistedHealth01, out byte persistedHarvestState);
                    float normalizedHealth = math.saturate(persistedHealth01);
                    float normalizedHeightScale = ResolveNormalizedHeightScaleFromHarvestState(
                        descriptorIndex,
                        normalizedHealth,
                        ResolvePersistedHarvestState(persistedHarvestState));
                    Unity.Mathematics.half nextHealth = (Unity.Mathematics.half)normalizedHealth;
                    Unity.Mathematics.half nextHeight = (Unity.Mathematics.half)math.saturate(normalizedHeightScale);
                    if (overrideScratchSaturated)
                        continue;

                    if (!_persistedHealth01ByInstanceUid.TryGetValue(record.InstanceUid, out Unity.Mathematics.half existingHealth) ||
                        !_persistedHeightScale01ByInstanceUid.TryGetValue(record.InstanceUid, out Unity.Mathematics.half existingHeight) ||
                        !AreHalfValuesEquivalent(existingHealth, nextHealth) ||
                        !AreHalfValuesEquivalent(existingHeight, nextHeight))
                    {
                        bool hadHealth = _persistedHealth01ByInstanceUid.TryGetValue(record.InstanceUid, out Unity.Mathematics.half previousHealth);
                        bool hadHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(record.InstanceUid, out Unity.Mathematics.half previousHeight);
                        bool healthStored = _persistedHealth01ByInstanceUid.TryPut(record.InstanceUid, nextHealth);
                        bool heightStored = _persistedHeightScale01ByInstanceUid.TryPut(record.InstanceUid, nextHeight);
                        if (!healthStored || !heightStored)
                        {
                            RestorePersistedFloraStateOverridePair(record.InstanceUid, hadHealth, previousHealth, hadHeight, previousHeight);
                            staleOverrideOverflow++;
                            continue;
                        }

                        overrideStateChanged = true;
                    }
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lifecycleMask);
            }

            for (int i = 0; i < staleOverrideCount; i++)
            {
                registry.TryClearFloraStateOverride(_staleFloraStateRegistryClearUids[i]);
                _staleFloraStateRegistryClearUids[i] = 0u;
            }

            if (staleOverrideOverflow > 0)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, staleOverrideOverflow, 0, 0f, 0u, 128);

            return overrideStateChanged;
        }

        private void ProcessYieldBatchIfNeeded()
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireYieldJobGuard(vault, out ulong guardMask))
                return;

            Span<DestroyedOrganicEvent> navDispatchEvents = stackalloc DestroyedOrganicEvent[MaxOrganicYieldNavDispatchStackBatch];
            int navDispatchCount = 0;
            try
            {
                if (!_pendingYieldEvents.TryResolveArray(out NativeArray<DestroyedOrganicEvent> pendingEvents))
                    return;

                int pendingCount = math.min(_pendingYieldEvents.Length, pendingEvents.Length);
                if (pendingCount <= 0)
                    return;

                int eventCount = math.min(
                    pendingCount,
                    math.min(MaxOrganicYieldNavDispatchStackBatch, math.min(DefaultDropBufferCapacity, MaxOrganicDropRecordsPerFrame)));

                NativeArray<DestroyedOrganicEvent> yieldJobInput = _yieldJobInput;
                NativeArray<HarvestableTemplate.RuntimeDescriptor> templateDescriptors = _templateDescriptors;
                NativeArray<HarvestableTemplate.LootRuntimeEntry> lootEntries = _lootEntries;
                NativeArray<EntropyYieldMaterialLutEntry> materialLut = _yieldMaterialLut;
                NativeArray<ItemDropData> dropOutput = _dropOutput;
                NativeArray<int> dropBudget = _dropBudget;
                if (!_templateCacheReady ||
                    !_yieldMaterialLutReady ||
                    !yieldJobInput.IsCreated ||
                    yieldJobInput.Length < eventCount ||
                    !templateDescriptors.IsCreated ||
                    !lootEntries.IsCreated ||
                    !materialLut.IsCreated ||
                    !dropOutput.IsCreated ||
                    !dropBudget.IsCreated ||
                    dropOutput.Length <= 0 ||
                    dropBudget.Length < DropBudgetLength)
                    return;

                if (ResolveDropOutputCount(dropBudget, dropOutput.Length) > 0)
                    return;

                ResetDropOutputBudget(dropBudget, dropOutput.Length);
                for (int i = 0; i < eventCount; i++)
                {
                    DestroyedOrganicEvent pendingEvent = pendingEvents[i];
                    yieldJobInput[i] = pendingEvent;
                    navDispatchEvents[i] = pendingEvent;
                }

                int remainderCount = pendingCount - eventCount;
                if (remainderCount > 0)
                {
                    for (int i = 0; i < remainderCount; i++)
                        pendingEvents[i] = pendingEvents[eventCount + i];

                    _pendingYieldEvents.ResizeUninitialized(remainderCount);
                    _deferredYieldScheduleFrame = math.max(_deferredYieldScheduleFrame, Hecton8.Core.SystemDispatcher.CurrentFrameIndex + 1);
                }
                else
                {
                    _pendingYieldEvents.Clear();
                    _deferredYieldScheduleFrame = -1;
                }

                EntropyYieldJob yieldJob = default;
                yieldJob.Events = yieldJobInput;
                yieldJob.TemplateDescriptors = templateDescriptors;
                yieldJob.LootEntries = lootEntries;
                yieldJob.MaterialLut = materialLut;
                yieldJob.DropOutput = dropOutput;
                yieldJob.DropBudget = dropBudget;
                yieldJob.EventCount = eventCount;

                for (int i = 0; i < eventCount; i++)
                    yieldJob.Execute(i);

                navDispatchCount = eventCount;
            }
            finally
            {
                ReleaseYieldJobGuard(vault, guardMask);
            }

            if (navDispatchCount > 0)
                VoxelDynamicNavGridRuntime.EnqueueDestroyedOrganicEvents(navDispatchEvents.Slice(0, navDispatchCount));
        }

        private static bool TryAcquireYieldJobGuard(IDataVault vault, out ulong guardMask)
        {
            guardMask = 0UL;
            if (vault == null)
                return false;

            for (int i = 0; i < YieldJobBufferCount; i++)
            {
                BufferID bufferId = GetYieldJobBufferId(i);
                guardMask |= OrganicMutationGuardBit(bufferId);
            }

            if (guardMask == 0UL)
                return true;

            if (vault.TryAcquireMutationGuard(guardMask))
                return true;

            guardMask = 0UL;
            return false;
        }

        private static void ReleaseYieldJobGuard(IDataVault vault, ulong guardMask)
        {
            ReleaseOrganicGuard(vault, guardMask);
        }

        private static BufferID GetYieldJobBufferId(int index)
        {
            switch (index)
            {
                case 0:
                    return OrganicPendingYieldEventsBufferId;
                case 1:
                    return OrganicYieldJobInputBufferId;
                case 2:
                    return OrganicTemplateDescriptorsBufferId;
                case 3:
                    return OrganicLootEntriesBufferId;
                case 4:
                    return OrganicYieldMaterialLutBufferId;
                case 5:
                    return OrganicDropOutputBufferId;
                case 6:
                    return OrganicDropBudgetBufferId;
                default:
                    return default;
            }
        }

        private void ResetDropOutputBudget()
        {
            NativeArray<int> dropBudget = _dropBudget;
            NativeArray<ItemDropData> dropOutput = _dropOutput;
            if (dropBudget.IsCreated && dropOutput.IsCreated)
                ResetDropOutputBudget(dropBudget, dropOutput.Length);
        }

        private static void ResetDropOutputBudget(NativeArray<int> dropBudget, int capacity)
        {
            if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength)
                return;

            dropBudget[DropBudgetRemainingIndex] = math.max(0, capacity);
            dropBudget[DropBudgetDroppedIndex] = 0;
        }

        private static int ResolveDropOutputCount(NativeArray<int> dropBudget, int capacity)
        {
            if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength || capacity <= 0)
                return 0;

            return math.clamp(capacity - math.max(0, dropBudget[DropBudgetRemainingIndex]), 0, capacity);
        }

        private static void SetDropOutputCount(NativeArray<int> dropBudget, int capacity, int count)
        {
            if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength)
                return;

            int clampedCount = math.clamp(count, 0, math.max(0, capacity));
            dropBudget[DropBudgetRemainingIndex] = math.max(0, capacity - clampedCount);
            dropBudget[DropBudgetDroppedIndex] = 0;
        }

        private bool DrainDropBuffer()
        {
            IPlayerInventoryService playerInventoryService = _playerInventoryService;
            PlayerInventory playerInventory = playerInventoryService != null ? playerInventoryService.Inventory : null;
            Hecton8.SaveSystem.ItemCatalog itemCatalog = playerInventory != null ? playerInventory.ItemCatalog : null;
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            bool hasLosslessDropRoute = playerInventory != null && registry != null && itemCatalog != null;
            if (!hasLosslessDropRoute)
            {
                if (!TrySnapshotDropOutputStateWithLock(out int producedCount, out int droppedCount))
                {
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 64);
                    return false;
                }

                if (droppedCount > 0)
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, droppedCount, 0, 0f, 0u, 128);

                if (producedCount > 0)
                {
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, producedCount, 0, 0f, 0u, 128);
                    return false;
                }

                return true;
            }

            Span<ItemDropData> drainedDrops = stackalloc ItemDropData[MaxOrganicDropDrainStackBatch];
            int processedCount = 0;
            int remainingCount = 0;
            bool routeFailure = false;
            while (processedCount < MaxOrganicDropRecordsPerFrame)
            {
                int batchLimit = math.min(MaxOrganicDropDrainStackBatch, MaxOrganicDropRecordsPerFrame - processedCount);
                if (!TryDrainDropBatch(drainedDrops, batchLimit, out int drainCount, out remainingCount, out int droppedCount))
                {
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 64);
                    return false;
                }

                if (droppedCount > 0)
                    RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, droppedCount, 0, 0f, 0u, 128);

                if (drainCount <= 0)
                    return remainingCount <= 0;

                processedCount += drainCount;
                for (int i = 0; i < drainCount; i++)
                {
                    ItemDropData drop = drainedDrops[i];
                    if (drop.ItemHashId == 0 || drop.Quantity == 0)
                        continue;

                    int rejectedQuantity = drop.Quantity;
                    if (playerInventory != null)
                    {
                        PlayerInventory.ScavengeAttemptResult result =
                            playerInventory.ScavengeAttempt(drop.ItemHashId, drop.Quantity, playerInventory.transform);
                        rejectedQuantity = result.RejectedQuantity;
                        PublishOrganicDropLifecycleCollected(
                            itemCatalog,
                            drop.ItemHashId,
                            drop.Quantity - rejectedQuantity,
                            playerInventory.transform,
                            ToRuntimeVector3(drop.Position));
                    }

                    if (rejectedQuantity > 0 && registry != null && itemCatalog != null)
                    {
                        Vector3 runtimePosition = ToRuntimeVector3(drop.Position);
                        if (!registry.TryRegisterDroppedItem(drop.ItemHashId, itemCatalog, rejectedQuantity, runtimePosition))
                        {
                            ItemDropData rejectedDrop = drop;
                            rejectedDrop.Quantity = (ushort)math.clamp(rejectedQuantity, 0, (int)ushort.MaxValue);
                            routeFailure = true;
                            if (!TryReturnDropToOutput(rejectedDrop))
                                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, rejectedQuantity, 0, 0f, 0u, 128);
                            else
                                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, 0u, 128);
                        }
                    }
                }

                if (routeFailure)
                    return false;

                if (remainingCount <= 0)
                    return true;
            }

            return !routeFailure && remainingCount <= 0;
        }

        private static void PublishOrganicDropLifecycleCollected(
            Hecton8.SaveSystem.ItemCatalog itemCatalog,
            int itemHashId,
            int acceptedQuantity,
            Transform interactor,
            Vector3 runtimePosition)
        {
            if (itemCatalog == null || itemHashId == 0 || acceptedQuantity <= 0)
                return;

            ItemData item = itemCatalog.FindByHash(itemHashId);
            if (item == null)
                return;

            bool hasInteractorPosition = interactor != null && IsFiniteVector(interactor.position);
            ulong interactorEntityId = hasInteractorPosition ? EntityId.ToULong(interactor.GetEntityId()) : 0ul;
            Vector3 signalPosition = IsFiniteVector(runtimePosition)
                ? runtimePosition
                : (hasInteractorPosition ? interactor.position : Vector3.zero);
            bool hasRuntimePosition = IsFiniteVector(signalPosition);

            ItemLifecycleSignalRoute.TryPublishCollected(
                item,
                acceptedQuantity,
                interactorEntityId,
                signalPosition,
                hasRuntimePosition);
        }

        private bool TrySnapshotDropOutputStateWithLock(out int producedCount, out int droppedCount)
        {
            producedCount = 0;
            droppedCount = 0;
            NativeArray<ItemDropData> dropOutput = _dropOutput;
            if (!dropOutput.IsCreated || dropOutput.Length <= 0)
                return true;

            return TryCaptureDropBudgetGuarded(dropOutput.Length, out producedCount, out droppedCount);
        }

        private bool TryReturnDropToOutput(ItemDropData drop)
        {
            if (drop.ItemHashId == 0 || drop.Quantity == 0)
                return true;

            NativeArray<ItemDropData> dropOutput = _dropOutput;
            if (!dropOutput.IsCreated || dropOutput.Length <= 0)
                return false;

            if (!TryCaptureDropBudgetGuarded(dropOutput.Length, out int producedCount, out _))
                return false;

            if (producedCount < 0 || producedCount >= dropOutput.Length)
                return false;

            if (!WriteDropOutputSlotGuarded(producedCount, drop))
                return false;

            return TrySetDropOutputCountGuarded(dropOutput.Length, producedCount + 1);
        }

        private bool TryDrainDropBatch(
            Span<ItemDropData> destination,
            int maxDrainCount,
            out int drainCount,
            out int remainingCount,
            out int droppedCount)
        {
            drainCount = 0;
            remainingCount = 0;
            droppedCount = 0;
            if (maxDrainCount <= 0 || destination.Length <= 0)
                return true;

            NativeArray<ItemDropData> dropOutput = _dropOutput;
            if (!dropOutput.IsCreated || dropOutput.Length <= 0)
                return true;

            if (!TryCaptureDropBudgetGuarded(dropOutput.Length, out int producedCount, out droppedCount))
                return false;

            drainCount = math.min(producedCount, math.min(maxDrainCount, destination.Length));
            remainingCount = producedCount - drainCount;
            if (drainCount <= 0)
                return true;

            int tailStart = producedCount - drainCount;
            if (!CopyDropOutputTailGuarded(destination, tailStart, drainCount))
                return false;

            if (!TrySetDropOutputCountGuarded(dropOutput.Length, remainingCount))
                return false;

            ClearDropOutputRangeGuarded(remainingCount, drainCount);
            return true;
        }

        private static int ResolveDropDroppedCount(NativeArray<int> dropBudget)
        {
            if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength)
                return 0;

            return math.max(0, dropBudget[DropBudgetDroppedIndex]);
        }

        private bool TryCaptureDropBudgetGuarded(int capacity, out int producedCount, out int droppedCount)
        {
            producedCount = 0;
            droppedCount = 0;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicDropBudgetBufferId, out ulong guardMask))
                return false;

            try
            {
                NativeArray<int> dropBudget = _dropBudget;
                if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength || capacity <= 0)
                    return true;

                producedCount = ResolveDropOutputCount(dropBudget, capacity);
                droppedCount = ResolveDropDroppedCount(dropBudget);
                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool TrySetDropOutputCountGuarded(int capacity, int count)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicDropBudgetBufferId, out ulong guardMask))
                return false;

            try
            {
                NativeArray<int> dropBudget = _dropBudget;
                if (!dropBudget.IsCreated || dropBudget.Length < DropBudgetLength || capacity <= 0)
                    return false;

                SetDropOutputCount(dropBudget, capacity, count);
                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool WriteDropOutputSlotGuarded(int index, ItemDropData drop)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicDropOutputBufferId, out ulong guardMask))
                return false;

            try
            {
                NativeArray<ItemDropData> dropOutput = _dropOutput;
                if (!dropOutput.IsCreated || (uint)index >= (uint)dropOutput.Length)
                    return false;

                dropOutput[index] = drop;
                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool CopyDropOutputTailGuarded(Span<ItemDropData> destination, int tailStart, int drainCount)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicDropOutputBufferId, out ulong guardMask))
                return false;

            try
            {
                NativeArray<ItemDropData> dropOutput = _dropOutput;
                if (!dropOutput.IsCreated ||
                    tailStart < 0 ||
                    drainCount < 0 ||
                    drainCount > destination.Length ||
                    tailStart + drainCount > dropOutput.Length)
                {
                    return false;
                }

                for (int i = 0; i < drainCount; i++)
                    destination[i] = dropOutput[tailStart + i];

                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool ClearDropOutputRangeGuarded(int startIndex, int count)
        {
            if (count <= 0)
                return true;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicDropOutputBufferId, out ulong guardMask))
                return false;

            try
            {
                NativeArray<ItemDropData> dropOutput = _dropOutput;
                if (!dropOutput.IsCreated ||
                    startIndex < 0 ||
                    count < 0 ||
                    startIndex + count > dropOutput.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                    dropOutput[startIndex + i] = default;

                return true;
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private void RefreshCorpseResourceNodes(float currentTime)
        {
            if (_corpseResourceNodes == null || _corpseResourceNodeCount <= 0)
                return;

            for (int i = 0; i < _corpseResourceNodeCount; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                if (record.Active == 0)
                    continue;

                if (record.RemainingUnits <= 0f || currentTime >= record.ExpireTime)
                {
                    record.Active = 0;
                    record.RemainingUnits = 0f;
                    _corpseResourceNodes[i] = record;
                    continue;
                }

                float normalizedDecay = ResolveCorpseCapacityFraction01(in record);
                float bloodIntensity = math.lerp(0.35f, record.BloodIntensity, normalizedDecay);
                ChemicalInfluenceGrid.QueueBloodScent(record.Position, bloodIntensity);
            }

            TrimTrailingCorpseNodes();
        }

        private static float ResolveCorpseCapacityFraction01(in CorpseResourceNodeRecord record)
        {
            float initialUnits = record.InitialUnits > 0f ? record.InitialUnits : record.RemainingUnits;
            return initialUnits > 0f
                ? Mathf.Clamp01(record.RemainingUnits / initialUnits)
                : 0f;
        }

        private int FindWeakestCorpseNodeIndex()
        {
            if (_corpseResourceNodes == null || _corpseResourceNodes.Length == 0)
                return -1;

            int weakestIndex = -1;
            float weakestScore = float.MaxValue;
            for (int i = 0; i < _corpseResourceNodes.Length; i++)
            {
                CorpseResourceNodeRecord record = _corpseResourceNodes[i];
                float score = record.Active == 0 ? float.MinValue : record.RemainingUnits;
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                weakestIndex = i;
            }

            return weakestIndex;
        }

        private void TrimTrailingCorpseNodes()
        {
            while (_corpseResourceNodeCount > 0 && _corpseResourceNodes[_corpseResourceNodeCount - 1].Active == 0)
                _corpseResourceNodeCount--;
        }

        internal bool TryResolveNearestConsumableFlora(Vector3 runtimePosition, float searchRadius, out Vector3 floraPosition, out uint instanceUid)
        {
            return TrySnapshotNearestConsumableFloraWithLock(runtimePosition, searchRadius, out floraPosition, out instanceUid);
        }

        internal bool TrySnapshotNearestConsumableFloraWithLock(Vector3 runtimePosition, float searchRadius, out Vector3 floraPosition, out uint instanceUid)
        {
            floraPosition = Vector3.zero;
            instanceUid = 0u;
            if (_dearLieJobScheduled)
                return false;

            float bestDistanceSq = searchRadius * searchRadius;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                return false;

            bool found;
            try
            {
                found = TryFindNearestConsumableFloraInPinnedLane(runtimePosition, true, ref bestDistanceSq, ref floraPosition, ref instanceUid);
                if (TryFindNearestConsumableFloraInPinnedLane(runtimePosition, false, ref bestDistanceSq, ref floraPosition, ref instanceUid))
                    found = true;
            }
            finally
            {
                ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
            }

            return found;
        }

        public bool TryResolveNearestHarvestInteractionPoint(
            Vector3 handRuntimePosition,
            float searchRadius,
            uint toolCapabilityMask,
            out FloraHarvestInteractionPoint interactionPoint)
        {
            interactionPoint = default;
            if (_dearLieJobScheduled)
                return false;

            if (searchRadius <= 0f || vegetationBridge == null || !_templateCacheReady)
                return false;

            if (!TrySnapshotNearestHarvestTargetWithLock(
                handRuntimePosition,
                Mathf.Max(MinimumSearchRadius, searchRadius),
                toolCapabilityMask,
                out _,
                out _,
                out uint instanceUid,
                out HarvestableTemplate.MaterialClass materialClass,
                out int templateIndex,
                out _,
                out Vector3 instancePosition,
                out HectonVegetationInstanceData instanceMetadata,
                out int instanceType,
                out _,
                out _))
            {
                return false;
            }

            Vector3 snapPosition = ResolveHarvestSnapPosition(
                handRuntimePosition,
                instancePosition,
                instanceMetadata,
                instanceType);
            Vector3 normal = handRuntimePosition - snapPosition;
            normal = NormalizeVector3Fast(normal, Vector3.up);

            if (!TryResolveAupFromRuntimeOrigin(snapPosition, out AbsoluteUniversePosition snapAup))
                return false;

            interactionPoint = new FloraHarvestInteractionPoint(
                instanceUid,
                snapAup,
                snapPosition,
                normal,
                materialClass,
                templateIndex,
                1f);
            return true;
        }

        internal int CollectNearestConsumableFlora(
            Vector3 runtimePosition,
            float searchRadius,
            uint[] instanceUids,
            Vector3[] positions)
        {
            if (_dearLieJobScheduled || instanceUids == null || positions == null)
                return 0;

            int capacity = math.min(instanceUids.Length, positions.Length);
            if (capacity <= 0)
                return 0;

            for (int i = 0; i < capacity; i++)
            {
                instanceUids[i] = 0u;
                positions[i] = Vector3.zero;
            }

            Span<float> bestDistanceSq = stackalloc float[4];
            int boundedCapacity = math.min(capacity, 4);
            for (int i = 0; i < boundedCapacity; i++)
                bestDistanceSq[i] = float.MaxValue;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                return 0;

            int collectedCount = 0;
            try
            {
                CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, true, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
                CollectNearestConsumableFloraInLane(runtimePosition, searchRadius, false, instanceUids, positions, bestDistanceSq, boundedCapacity, ref collectedCount);
            }
            finally
            {
                ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
            }

            return collectedCount;
        }

        internal bool AreTrackedFloraDestroyed(uint[] instanceUids, int trackedCount)
        {
            if (instanceUids == null || trackedCount <= 0)
                return false;

            IDataVault vault = _dearLieVault;
            if (!_destroyedByInstanceUid.IsCreated || vault == null)
                return false;

            if (!TryAcquireOrganicBufferGuard(vault, OrganicDestroyedByUidBufferId, out ulong guardMask))
                return false;

            int upperBound = math.min(trackedCount, instanceUids.Length);
            bool hasTrackedInstance = false;
            try
            {
                for (int i = 0; i < upperBound; i++)
                {
                    uint instanceUid = instanceUids[i];
                    if (instanceUid == 0u)
                        continue;

                    hasTrackedInstance = true;
                    if (!_destroyedByInstanceUid.ContainsKey(instanceUid))
                        return false;
                }
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }

            return hasTrackedInstance;
        }

        internal bool TryConsumeFlora(uint instanceUid)
        {
            if (_dearLieJobScheduled ||
                !TrySnapshotActiveInstanceByUidWithLock(
                    instanceUid,
                    out bool underwater,
                    out int activeIndex,
                    out int templateIndex,
                    out HarvestableTemplate.MaterialClass materialClass,
                    out _,
                    out Vector3 instancePosition,
                    out float currentHealth))
                return false;

            if (!IsConsumableFloraMaterialClass(materialClass) || currentHealth <= 0.0001f)
                return false;

            return ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);
        }

        private bool TrySnapshotNearestHarvestTargetWithLock(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            out bool underwater,
            out int activeIndex,
            out uint instanceUid,
            out HarvestableTemplate.MaterialClass materialClass,
            out int templateIndex,
            out Matrix4x4 instanceMatrix,
            out Vector3 instancePosition,
            out HectonVegetationInstanceData instanceMetadata,
            out int instanceType,
            out float instanceHealth,
            out float instanceNormalizedHeightScale)
        {
            underwater = false;
            activeIndex = -1;
            instanceUid = 0u;
            materialClass = HarvestableTemplate.MaterialClass.None;
            templateIndex = -1;
            instanceMatrix = Matrix4x4.identity;
            instancePosition = Vector3.zero;
            instanceMetadata = default;
            instanceType = 0;
            instanceHealth = 0f;
            instanceNormalizedHeightScale = 1f;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                return false;

            float bestDistanceSq = float.MaxValue;
            try
            {
                if (TryFindNearestHarvestTargetInPinnedLane(
                    hitPoint,
                    searchRadius,
                    toolCapabilityMask,
                    true,
                    ref bestDistanceSq,
                    ref activeIndex,
                    ref instanceUid,
                    ref materialClass,
                    ref templateIndex,
                    ref instanceMatrix,
                    ref instancePosition,
                    ref instanceMetadata,
                    ref instanceType,
                    ref instanceHealth,
                    ref instanceNormalizedHeightScale))
                {
                    underwater = true;
                }

                int surfaceIndex = -1;
                uint surfaceUid = 0u;
                HarvestableTemplate.MaterialClass surfaceMaterial = HarvestableTemplate.MaterialClass.None;
                int surfaceTemplateIndex = -1;
                Matrix4x4 surfaceMatrix = Matrix4x4.identity;
                Vector3 surfacePosition = Vector3.zero;
                HectonVegetationInstanceData surfaceMetadata = default;
                int surfaceType = 0;
                float surfaceHealth = 0f;
                float surfaceNormalizedHeightScale = 1f;
                if (TryFindNearestHarvestTargetInPinnedLane(
                    hitPoint,
                    searchRadius,
                    toolCapabilityMask,
                    false,
                    ref bestDistanceSq,
                    ref surfaceIndex,
                    ref surfaceUid,
                    ref surfaceMaterial,
                    ref surfaceTemplateIndex,
                    ref surfaceMatrix,
                    ref surfacePosition,
                    ref surfaceMetadata,
                    ref surfaceType,
                    ref surfaceHealth,
                    ref surfaceNormalizedHeightScale))
                {
                    underwater = false;
                    activeIndex = surfaceIndex;
                    instanceUid = surfaceUid;
                    materialClass = surfaceMaterial;
                    templateIndex = surfaceTemplateIndex;
                    instanceMatrix = surfaceMatrix;
                    instancePosition = surfacePosition;
                    instanceMetadata = surfaceMetadata;
                    instanceType = surfaceType;
                    instanceHealth = surfaceHealth;
                    instanceNormalizedHeightScale = surfaceNormalizedHeightScale;
                }

                return activeIndex >= 0 && instanceUid != 0u && templateIndex >= 0;
            }
            finally
            {
                ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
            }
        }

        private static Vector3 ResolveHarvestSnapPosition(
            Vector3 handRuntimePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(math.abs(metadata.HeightScale)));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                return ClosestPointOnSegment(rootPosition, top, handRuntimePosition);
            }

            float height01 = math.saturate(math.abs(metadata.HeightScale));
            float verticalBias = vegetationType == HectonVegetationInstanceType.Sargassum
                ? math.lerp(0.18f, 0.85f, height01)
                : math.lerp(0.12f, 0.65f, height01);
            return rootPosition + Vector3.up * verticalBias;
        }

        private bool TryFindNearestConsumableFloraInPinnedLane(
            Vector3 runtimePosition,
            bool underwater,
            ref float bestDistanceSq,
            ref Vector3 bestPosition,
            ref uint bestInstanceUid)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return false;
            }

            bool found = false;
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (IsLifecycleReadBlocked(candidateUid))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestPosition = candidatePosition;
                bestInstanceUid = candidateUid;
                found = true;
            }

            return found;
        }

        private void CollectNearestConsumableFloraInLane(
            Vector3 runtimePosition,
            float searchRadius,
            bool underwater,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (capacity <= 0)
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated ||
                !instanceUids.IsCreated ||
                !materialClasses.IsCreated ||
                !health.IsCreated ||
                count <= 0)
            {
                return;
            }

            float searchRadiusSq = math.max(0.0001f, searchRadius * searchRadius);
            int upperBound = math.min(count, math.min(matrices.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length))));
            for (int i = 0; i < upperBound; i++)
            {
                uint candidateUid = instanceUids[i];
                if (IsLifecycleReadBlocked(candidateUid))
                {
                    continue;
                }

                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                if (!IsConsumableFloraMaterialClass(materialClass) || (float)health[i] <= 0.0001f)
                    continue;

                Vector3 candidatePosition = ExtractTranslation(matrices[i]);
                float distanceSq = (candidatePosition - runtimePosition).sqrMagnitude;
                if (distanceSq > searchRadiusSq)
                    continue;

                TryInsertConsumableCandidate(
                    candidateUid,
                    candidatePosition,
                    distanceSq,
                    bestInstanceUids,
                    bestPositions,
                    bestDistanceSq,
                    capacity,
                    ref collectedCount);
            }
        }

        private static void TryInsertConsumableCandidate(
            uint candidateUid,
            Vector3 candidatePosition,
            float distanceSq,
            uint[] bestInstanceUids,
            Vector3[] bestPositions,
            Span<float> bestDistanceSq,
            int capacity,
            ref int collectedCount)
        {
            if (candidateUid == 0u || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == candidateUid)
                    return;
            }

            int insertIndex = -1;
            for (int i = 0; i < capacity; i++)
            {
                if (bestInstanceUids[i] == 0u || distanceSq < bestDistanceSq[i])
                {
                    insertIndex = i;
                    break;
                }
            }

            if (insertIndex < 0)
                return;

            for (int i = capacity - 1; i > insertIndex; i--)
            {
                bestInstanceUids[i] = bestInstanceUids[i - 1];
                bestPositions[i] = bestPositions[i - 1];
                bestDistanceSq[i] = bestDistanceSq[i - 1];
            }

            bestInstanceUids[insertIndex] = candidateUid;
            bestPositions[insertIndex] = candidatePosition;
            bestDistanceSq[insertIndex] = distanceSq;
            collectedCount = math.min(collectedCount + 1, capacity);
        }

        private bool IsLifecycleReadBlocked(uint instanceUid)
        {
            return instanceUid == 0u ||
                   (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                   (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid));
        }

        private bool TryFindNearestHarvestTargetInPinnedLane(
            Vector3 hitPoint,
            float searchRadius,
            uint toolCapabilityMask,
            bool underwater,
            ref float bestDistanceSq,
            ref int bestIndex,
            ref uint bestUid,
            ref HarvestableTemplate.MaterialClass bestMaterialClass,
            ref int bestTemplateIndex,
            ref Matrix4x4 bestMatrix,
            ref Vector3 bestPosition,
            ref HectonVegetationInstanceData bestMetadata,
            ref int bestType,
            ref float bestHealth,
            ref float bestNormalizedHeightScale)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!matrices.IsCreated || !metadata.IsCreated || !types.IsCreated || !instanceUids.IsCreated || !materialClasses.IsCreated || !health.IsCreated || count <= 0)
                return false;

            float searchRadiusSq = searchRadius * searchRadius;
            bool found = false;
            int safeCount = math.min(
                count,
                math.min(
                    math.min(matrices.Length, metadata.Length),
                    math.min(types.Length, math.min(instanceUids.Length, math.min(materialClasses.Length, health.Length)))));
            for (int i = 0; i < safeCount; i++)
            {
                uint instanceUid = instanceUids[i];
                if (IsLifecycleReadBlocked(instanceUid) || (float)health[i] <= 0.0001f)
                    continue;

                HectonVegetationInstanceData instanceMetadata = metadata[i];
                HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)materialClasses[i];
                int templateIndex = ResolveTemplateIndex(instanceMetadata, materialClass);
                if (materialClass == HarvestableTemplate.MaterialClass.None || templateIndex < 0)
                    continue;

                if (!IsToolCompatible(instanceMetadata, toolCapabilityMask))
                    continue;

                Vector3 rootPosition = ExtractTranslation(matrices[i]);
                float distanceSq = ResolveHarvestDistanceSq(hitPoint, rootPosition, instanceMetadata, types[i], searchRadiusSq, kelpHeightTolerance);
                if (distanceSq > searchRadiusSq || distanceSq >= bestDistanceSq)
                    continue;

                found = true;
                bestDistanceSq = distanceSq;
                bestIndex = i;
                bestUid = instanceUid;
                bestMaterialClass = materialClass;
                bestTemplateIndex = templateIndex;
                bestMatrix = matrices[i];
                bestPosition = rootPosition;
                bestMetadata = instanceMetadata;
                bestType = types[i];
                bestHealth = (float)health[i];
                bestNormalizedHeightScale = ResolveRuntimeNormalizedHeightScale(instanceUid, instanceMetadata);
            }

            return found;
        }

        private bool DestroyResolvedInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower)
        {
            if (!_destroyedByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasNavObstacleBounds = false;
            float3 navObstacleCenter = float3.zero;
            float3 navObstacleExtents = float3.zero;
            float parentMassKg = 0f;
            bool applied = false;
            bool registerDestroyedFlora = false;
            bool destroyedMapInsertFailed = false;
            ulong templateStableHash = 0UL;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            try
            {
                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (!IsPinnedActiveLaneSlot(instanceUid, underwater, activeIndex))
                    return false;

                if (!_destroyedByInstanceUid.TryAdd(instanceUid, 1))
                {
                    destroyedMapInsertFailed = true;
                }
                else
                {
                    byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
                    ClearOrganicLifecycleState(instanceUid);

                    _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)0f);
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds());
                    SetLaneHealth(underwater, activeIndex, 0f);
                    if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                        _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

                    parentMassKg = ComputePinnedParentMassKg(underwater, activeIndex, instanceUid, materialClass, templateIndex);
                    ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                    ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
                    ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);
                    ClearPersistedFloraStateOverride(instanceUid);
                    if (TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor destructionDescriptor))
                    {
                        registerDestroyedFlora = true;
                        templateStableHash = (ulong)(uint)destructionDescriptor.StableHashId;
                    }

                    hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out navObstacleCenter, out navObstacleExtents);
                    applied = true;
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (destroyedMapInsertFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            if (!applied)
                return false;

            PublishExternalInteraction(instancePosition, NormalizeVector3Fast(hitNormal, Vector3.up) * (normalizedPower * OrganicBurstVelocityScale), interactionBurstRadius * 1.25f);
            SpawnDebris(materialClass, instanceMatrix, instancePosition, hitPoint, hitNormal, normalizedPower, instanceUid);
            QueueYieldEvent(
                instancePosition,
                normalizedPower,
                instanceUid,
                templateIndex,
                materialClass,
                parentMassKg,
                1f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            if (registry != null && registerDestroyedFlora)
                registry.TryRegisterDestroyedFlora(templateStableHash, instanceUid, instancePosition);

            return true;
        }

        private int ApplyPassiveDecompositionCandidates(ReadOnlySpan<PassiveDecompositionCandidate> candidates, int candidateCount)
        {
            int appliedCount = 0;
            int upperBound = math.min(candidateCount, candidates.Length);
            for (int i = 0; i < upperBound; i++)
            {
                PassiveDecompositionCandidate candidate = candidates[i];
                if (ApplyPassiveDecomposition(
                    candidate.Underwater != 0,
                    candidate.ActiveIndex,
                    candidate.InstanceUid,
                    candidate.MaterialClass,
                    candidate.TemplateIndex,
                    candidate.RuntimePosition))
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }

        private bool ApplyPassiveDecomposition(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Vector3 instancePosition)
        {
            if (!_destroyedByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasNavObstacleBounds = false;
            float3 navObstacleCenter = float3.zero;
            float3 navObstacleExtents = float3.zero;
            float parentMassKg = 0f;
            bool applied = false;
            bool registerDestroyedFlora = false;
            bool destroyedMapInsertFailed = false;
            ulong templateStableHash = 0UL;
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            try
            {
                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (!IsPinnedActiveLaneSlot(instanceUid, underwater, activeIndex))
                    return false;

                if (!_destroyedByInstanceUid.TryAdd(instanceUid, 1))
                {
                    destroyedMapInsertFailed = true;
                }
                else
                {
                    byte runtimeFlags = MarkDeadRuntimeFlag(instanceUid);
                    ClearOrganicLifecycleState(instanceUid);

                    _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)0f);
                    if (_damageVisualProgressByInstanceUid.IsCreated)
                        _damageVisualProgressByInstanceUid.Remove(instanceUid);
                    if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                        _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

                    PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds());
                    SetLaneHealth(underwater, activeIndex, 0f);
                    parentMassKg = ComputePinnedParentMassKg(underwater, activeIndex, instanceUid, materialClass, templateIndex);
                    ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                    ApplyDearLieMatrixScaleZeroToLaneInstance(underwater, activeIndex);
                    ApplyDecompositionToLaneInstance(underwater, activeIndex, instanceUid, 0f);
                    ClearPersistedFloraStateOverride(instanceUid);
                    if (TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor passiveDescriptor))
                    {
                        registerDestroyedFlora = true;
                        templateStableHash = (ulong)(uint)passiveDescriptor.StableHashId;
                    }

                    hasNavObstacleBounds = TryResolveNavObstacleForLaneInstance(underwater, activeIndex, out navObstacleCenter, out navObstacleExtents);
                    applied = true;
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (destroyedMapInsertFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            if (!applied)
                return false;

            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry != null)
                registry.TryClearFloraStateOverride(instanceUid);

            if (registry != null && registerDestroyedFlora)
                registry.TryRegisterDestroyedFlora(templateStableHash, instanceUid, instancePosition);

            QueueYieldEvent(
                instancePosition,
                0.1f,
                instanceUid,
                templateIndex,
                materialClass,
                parentMassKg,
                0f,
                hasNavObstacleBounds ? navObstacleCenter : float3.zero,
                hasNavObstacleBounds ? navObstacleExtents : float3.zero);
            return true;
        }

        private void SpawnDebris(
            HarvestableTemplate.MaterialClass materialClass,
            Matrix4x4 instanceMatrix,
            Vector3 instancePosition,
            Vector3 hitPoint,
            Vector3 hitNormal,
            float normalizedPower,
            uint instanceUid)
        {
            OrganicDebrisProfile profile = ResolveDebrisProfile(materialClass);
            if (profile == null || !profile.IsValid)
                return;

            float3 spawnPosition = ToFloat3(hitPoint);
            if (!math.all(math.isfinite(spawnPosition)))
                spawnPosition = ToFloat3(instancePosition);

            if (!math.all(math.isfinite(spawnPosition)))
                return;

            Vector3 debrisRuntimePosition = ToRuntimeVector3(spawnPosition);
            if (!TryResolveAupFromRuntimeOrigin(debrisRuntimePosition, out AbsoluteUniversePosition debrisAup))
                return;

            float safePower = math.isfinite(normalizedPower) ? math.max(0.1f, normalizedPower) : 0.1f;
            DebrisSpawnSignal signal = new DebrisSpawnSignal
            {
                PositionAup = debrisAup,
                SpeciesHash = unchecked((uint)materialClass) ^ 0x4F524741u,
                SourceEntityId = instanceUid ^ 0x7F4A7C15u,
                Intensity01 = math.saturate(safePower),
                DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                Flags = DebrisSpawnSignal.FlagComputeShard,
                Quantity = 0
            };
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001DestructibleOrganicManagerSignalPushDropCount);
        }

        private void QueueYieldEvent(
            Vector3 instancePosition,
            float normalizedPower,
            uint instanceUid,
            int templateIndex,
            HarvestableTemplate.MaterialClass materialClass,
            float parentMassKg,
            float damage01,
            float3 navObstacleCenter,
            float3 navObstacleExtents)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicPendingYieldEventsBufferId, out ulong guardMask))
                return;

            try
            {
                if (!_pendingYieldEvents.IsCreated || _pendingYieldEvents.Length >= _pendingYieldEvents.Capacity)
                    return;

                DestroyedOrganicEvent organicEvent = default;
                organicEvent.Position = ToFloat3(instancePosition);
                organicEvent.NavObstacleCenter = navObstacleCenter;
                organicEvent.NavObstacleExtents = navObstacleExtents;
                organicEvent.ToolPower = Mathf.Max(0.1f, normalizedPower);
                organicEvent.ParentMassKg = parentMassKg <= 0.0001f ? 0f : Mathf.Max(0.05f, parentMassKg);
                organicEvent.Damage01 = Mathf.Clamp01(damage01);
                organicEvent.InstanceUid = instanceUid;
                organicEvent.TemplateIndex = templateIndex;
                organicEvent.MaterialClassId = (int)materialClass;
                _pendingYieldEvents.AddNoResize(organicEvent);
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private void PublishExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            floraInteractionManager?.RegisterExternalInteraction(positionWS, velocityWS, radius);
        }

        private OrganicDebrisProfile ResolveDebrisProfile(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => kelpDebrisProfile,
                HarvestableTemplate.MaterialClass.Coral => coralDebrisProfile,
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => titaniumDebrisProfile,
                HarvestableTemplate.MaterialClass.Sargassum => sargassumDebrisProfile,
                _ => null
            };
        }

        private int ResolveTemplateIndex(HarvestableTemplate.MaterialClass materialClass)
        {
            int materialIndex = (int)materialClass;
            if (!_templateCacheReady ||
                _templateIndexByMaterialClass == null ||
                materialIndex < 0 ||
                materialIndex >= _templateIndexByMaterialClass.Length)
                return -1;

            return _templateIndexByMaterialClass[materialIndex];
        }

        private int ResolveTemplateIndex(HectonVegetationInstanceData metadata, HarvestableTemplate.MaterialClass fallbackMaterialClass)
        {
            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (_harvestDescriptorIndexByFloraTemplateIndex != null &&
                floraTemplateIndex >= 0 &&
                floraTemplateIndex < _harvestDescriptorIndexByFloraTemplateIndex.Length)
            {
                int mappedDescriptorIndex = _harvestDescriptorIndexByFloraTemplateIndex[floraTemplateIndex];
                if (mappedDescriptorIndex >= 0)
                    return mappedDescriptorIndex;
            }

            return ResolveTemplateIndex(fallbackMaterialClass);
        }

        private bool TrySnapshotTemplateDescriptorWithLock(int templateIndex, out HarvestableTemplate.RuntimeDescriptor descriptor)
        {
            descriptor = default;
            if (!_templateCacheReady || templateIndex < 0)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicTemplateDescriptorsBufferId, out ulong guardMask))
                return false;

            try
            {
                return TryCopyPinnedTemplateDescriptor(templateIndex, out descriptor);
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool TryCopyPinnedTemplateDescriptor(int templateIndex, out HarvestableTemplate.RuntimeDescriptor descriptor)
        {
            descriptor = default;
            if (!_templateCacheReady || templateIndex < 0)
                return false;

            if (!_templateDescriptors.TryResolve(out NativeArray<HarvestableTemplate.RuntimeDescriptor> descriptors) ||
                !descriptors.IsCreated ||
                templateIndex >= descriptors.Length)
            {
                return false;
            }

            descriptor = descriptors[templateIndex];
            return true;
        }

        private bool TryFindTemplateDescriptorByPersistentHashWithLock(
            ulong floraPersistentIdHash,
            out int descriptorIndex,
            out HarvestableTemplate.RuntimeDescriptor descriptor)
        {
            descriptorIndex = -1;
            descriptor = default;
            if (!_templateCacheReady)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicTemplateDescriptorsBufferId, out ulong guardMask))
                return false;

            try
            {
                return TryFindPinnedTemplateDescriptorByPersistentHash(floraPersistentIdHash, out descriptorIndex, out descriptor);
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }
        }

        private bool TryFindPinnedTemplateDescriptorByPersistentHash(
            ulong floraPersistentIdHash,
            out int descriptorIndex,
            out HarvestableTemplate.RuntimeDescriptor descriptor)
        {
            descriptorIndex = -1;
            descriptor = default;
            if (!_templateCacheReady ||
                !_templateDescriptors.TryResolve(out NativeArray<HarvestableTemplate.RuntimeDescriptor> descriptors) ||
                !descriptors.IsCreated)
            {
                return false;
            }

            for (int i = 0; i < descriptors.Length; i++)
            {
                HarvestableTemplate.RuntimeDescriptor candidate = descriptors[i];
                if ((ulong)(uint)candidate.StableHashId != floraPersistentIdHash)
                    continue;

                descriptorIndex = i;
                descriptor = candidate;
                return true;
            }

            return false;
        }

        private bool IsToolCompatible(HectonVegetationInstanceData metadata, uint toolCapabilityMask)
        {
            if (toolCapabilityMask == 0u || vegetationBridge == null)
                return true;

            int floraTemplateIndex = Mathf.RoundToInt(metadata.TemplateIndex);
            if (!vegetationBridge.TryGetFloraTemplateRuntimeDescriptor(floraTemplateIndex, out FloraDataTemplate.RuntimeDescriptor descriptor))
                return true;

            return descriptor.VulnerabilityMask == 0u || (descriptor.VulnerabilityMask & toolCapabilityMask) != 0u;
        }

        private void CacheBaseScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated || instanceUid == 0u || _baseScaleByInstanceUid.ContainsKey(instanceUid))
                return;

            _baseScaleByInstanceUid.TryAdd(
                instanceUid,
                MakeFloat2(
                    Mathf.Max(MinimumDecomposedHeightScale, Mathf.Abs(metadata.HeightScale)),
                    Mathf.Max(MinimumDecomposedWidthScale, Mathf.Clamp01(Mathf.Abs(metadata.WidthScale)))));
        }

        private void PrimeUntouchedClock(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated || _lastOrganicTouchTimeByInstanceUid.ContainsKey(instanceUid))
                return;

            _lastOrganicTouchTimeByInstanceUid.TryAdd(instanceUid, currentTime);
        }

        private void MarkOrganicTouched(uint instanceUid, float currentTime)
        {
            if (instanceUid == 0u || !_lastOrganicTouchTimeByInstanceUid.IsCreated)
                return;

            if (!_lastOrganicTouchTimeByInstanceUid.TryPut(instanceUid, currentTime))
                return;

            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
        }

        private void ClearOrganicLifecycleState(uint instanceUid)
        {
            if (instanceUid == 0u)
                return;

            if (_lastOrganicTouchTimeByInstanceUid.IsCreated)
                _lastOrganicTouchTimeByInstanceUid.Remove(instanceUid);
            if (_overgrownByInstanceUid.IsCreated)
                _overgrownByInstanceUid.Remove(instanceUid);
            if (_rootMoundAppliedByInstanceUid.IsCreated)
                _rootMoundAppliedByInstanceUid.Remove(instanceUid);
            if (_maturationScaleByInstanceUid.IsCreated)
                _maturationScaleByInstanceUid.Remove(instanceUid);
            if (_maturationYieldByInstanceUid.IsCreated)
                _maturationYieldByInstanceUid.Remove(instanceUid);
            if (_nextSporeAcousticTimeByInstanceUid.IsCreated)
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
        }

        private bool TryRegisterDefoliantDestroyedInstance(uint instanceUid, int templateIndex, out ulong templateStableHash, out bool destroyedMapInsertFailed)
        {
            templateStableHash = 0UL;
            destroyedMapInsertFailed = false;
            if (instanceUid == 0u || !_destroyedByInstanceUid.IsCreated)
                return false;

            if (!TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor descriptor))
                return false;

            if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                return false;

            if (_destroyedByInstanceUid.ContainsKey(instanceUid))
                return false;

            templateStableHash = (ulong)(uint)descriptor.StableHashId;
            if (!_destroyedByInstanceUid.TryAdd(instanceUid, 1))
            {
                destroyedMapInsertFailed = true;
                return false;
            }

            _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)0f);
            PrimeDecompositionState(instanceUid, ResolveOrganicClockSeconds() - OrganicDecompositionDurationSeconds);
            ClearOrganicLifecycleState(instanceUid);

            ClearPersistedFloraStateOverride(instanceUid);
            return true;
        }

        private byte EnsureRuntimeFlags(uint instanceUid, HarvestableTemplate.MaterialClass materialClass, int semanticType, float existingRuntimeFlags)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);

            if (_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte existingFlags))
                return existingFlags;

            byte resolvedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(existingRuntimeFlags);
            bool parasiteEligible = materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                                    materialClass == HarvestableTemplate.MaterialClass.Sargassum;
            if (parasiteEligible)
            {
                uint parasiteHash = instanceUid ^ (uint)(semanticType + 17) * 2246822519u;
                if ((parasiteHash & 0x0Fu) <= 1u)
                    resolvedFlags |= FloraRuntimeFlagHasParasite;
            }

            _runtimeFlagsByInstanceUid.TryAdd(instanceUid, resolvedFlags);
            return resolvedFlags;
        }

        private static void ApplyRuntimeFlags(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, byte runtimeFlags)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData flaggedMetadata = metadata[activeIndex];
            flaggedMetadata.RuntimeFlags = HectonVegetationRuntimeFlagEncoding.WithRuntimeFlags(flaggedMetadata.RuntimeFlags, runtimeFlags);
            metadata[activeIndex] = flaggedMetadata;
        }

        private static void ApplyRuntimeFlags(ref BridgeMetadataLane metadata, int activeIndex, byte runtimeFlags)
        {
            NativeArray<HectonVegetationInstanceData> buffer = metadata;
            ApplyRuntimeFlags(ref buffer, activeIndex, runtimeFlags);
        }

        private void ApplyRuntimeFlagsToLaneInstance(bool underwater, int activeIndex, byte runtimeFlags)
        {
            if (underwater)
                ApplyRuntimeFlags(ref _underwaterMetadata, activeIndex, runtimeFlags);
            else
                ApplyRuntimeFlags(ref _surfaceMetadata, activeIndex, runtimeFlags);
        }

        private byte MarkDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return 0;

            byte runtimeFlags = 0;
            _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);
            runtimeFlags |= FloraRuntimeFlagDead;
            _runtimeFlagsByInstanceUid.TryPut(instanceUid, runtimeFlags);
            return runtimeFlags;
        }

        private void ClearDeadRuntimeFlag(uint instanceUid)
        {
            if (!_runtimeFlagsByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            if (!_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags))
                return;

            runtimeFlags &= unchecked((byte)~FloraRuntimeFlagDead);
            if (runtimeFlags != 0)
            {
                _runtimeFlagsByInstanceUid.TryPut(instanceUid, runtimeFlags);
                return;
            }

            _runtimeFlagsByInstanceUid.Remove(instanceUid);
        }

        private bool TryResolveNavObstacleForLaneInstance(bool underwater, int activeIndex, out float3 center, out float3 extents)
        {
            center = float3.zero;
            extents = float3.zero;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            NativeArray<int>.ReadOnly semanticTypes = underwater ? _underwaterSemanticTypes : _surfaceSemanticTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                !semanticTypes.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length ||
                activeIndex >= semanticTypes.Length)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryResolveMacroFloraObstacleWorldBounds(
                matrices[activeIndex],
                metadata[activeIndex],
                types[activeIndex],
                semanticTypes[activeIndex],
                out center,
                out extents);
        }

        private static void SetRuntimeState(ref NativeArray<HectonVegetationInstanceData> metadata, int activeIndex, float runtimeState)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData stateMetadata = metadata[activeIndex];
            stateMetadata.RuntimeState = runtimeState;
            metadata[activeIndex] = stateMetadata;
        }

        private void PrimeDecompositionState(uint instanceUid, float decompositionStartTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            _decompositionStartTimeByInstanceUid.TryPut(instanceUid, decompositionStartTime);
        }

        private float EnsureDecompositionProgress(uint instanceUid, float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || instanceUid == 0u)
                return 1f;

            if (!_decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out float startTime))
            {
                startTime = currentTime - OrganicDecompositionDurationSeconds;
                _decompositionStartTimeByInstanceUid.TryAdd(instanceUid, startTime);
            }

            return math.saturate((currentTime - startTime) / OrganicDecompositionDurationSeconds);
        }

        private void ApplyDecompositionToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float entropy01)
        {
            if (underwater)
                ApplyDecompositionMetadata(ref _underwaterMetadata, activeIndex, instanceUid, entropy01);
            else
                ApplyDecompositionMetadata(ref _surfaceMetadata, activeIndex, instanceUid, entropy01);
        }

        internal bool TryEvaluateParasiteExposure(Vector3 runtimePosition, out float exposure01)
        {
            exposure01 = 0f;
            if (_dearLieJobScheduled ||
                !IsFiniteVector(runtimePosition) ||
                !_runtimeFlagsByInstanceUid.IsCreated)
            {
                return false;
            }

            float currentTime = ResolveOrganicClockSeconds();
            if (_lastParasiteExposureSampleTime > 0f)
            {
                Vector3 queryDelta = runtimePosition - _lastParasiteExposureQueryPosition;
                if (queryDelta.sqrMagnitude > ParasiteExposureQueryResetDistanceSq)
                {
                    _surfaceParasiteExposureScanCursor = 0;
                    _underwaterParasiteExposureScanCursor = 0;
                    _lastParasiteExposure01 = 0f;
                    _lastParasiteExposureSampleTime = 0f;
                    _nextParasiteExposureSampleTime = currentTime;
                }
            }

            if (currentTime < _nextParasiteExposureSampleTime)
            {
                exposure01 = Mathf.Clamp01(_lastParasiteExposure01);
                return exposure01 > 0.0001f;
            }

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicParasiteExposureReadGuard(vault, out int lockedMask))
            {
                if (_lastParasiteExposure01 > 0.0001f &&
                    currentTime - _lastParasiteExposureSampleTime <= ParasiteExposureHoldSeconds)
                {
                    exposure01 = Mathf.Clamp01(_lastParasiteExposure01);
                    return true;
                }

                return false;
            }

            float bestExposure = 0f;
            float qualityWeight = ResolveDearLieGlobalQualityWeight();
            int scanBudgetPerLane = ResolveParasiteExposureScanBudgetPerLane(qualityWeight);
            try
            {
                EvaluateParasiteExposureInLane(runtimePosition, false, scanBudgetPerLane, ref _surfaceParasiteExposureScanCursor, ref bestExposure);
                EvaluateParasiteExposureInLane(runtimePosition, true, scanBudgetPerLane, ref _underwaterParasiteExposureScanCursor, ref bestExposure);
            }
            finally
            {
                ReleaseOrganicParasiteExposureReadGuard(vault, lockedMask);
            }

            if (bestExposure > 0.0001f)
            {
                _lastParasiteExposure01 = Mathf.Clamp01(bestExposure);
                _lastParasiteExposureSampleTime = currentTime;
                _lastParasiteExposureQueryPosition = runtimePosition;
            }
            else if (_lastParasiteExposure01 > 0.0001f &&
                     currentTime - _lastParasiteExposureSampleTime > ParasiteExposureHoldSeconds)
            {
                _lastParasiteExposure01 = 0f;
                _lastParasiteExposureSampleTime = currentTime;
                _lastParasiteExposureQueryPosition = runtimePosition;
            }
            else if (_lastParasiteExposureSampleTime <= 0f)
            {
                _lastParasiteExposureSampleTime = currentTime;
                _lastParasiteExposureQueryPosition = runtimePosition;
            }

            _nextParasiteExposureSampleTime = currentTime + ResolveParasiteExposureRefreshIntervalSeconds(qualityWeight);
            exposure01 = Mathf.Clamp01(_lastParasiteExposure01);
            return exposure01 > 0.0001f;
        }

        private static int ResolveParasiteExposureScanBudgetPerLane(float qualityWeight)
        {
            float q = math.saturate(qualityWeight);
            return math.max(
                ParasiteExposureMinScanBudgetPerLane,
                (int)math.round(math.lerp(ParasiteExposureMinScanBudgetPerLane, ParasiteExposureMaxScanBudgetPerLane, q * q)));
        }

        private static float ResolveParasiteExposureRefreshIntervalSeconds(float qualityWeight)
        {
            return math.lerp(
                ParasiteExposureMaxRefreshIntervalSeconds,
                ParasiteExposureMinRefreshIntervalSeconds,
                math.saturate(qualityWeight));
        }

        private void ApplyDamageVisualState(
            uint instanceUid,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float baseHealth,
            float currentHealth,
            float transitionHeightScale,
            bool harvestStateChanged,
            float currentTime)
        {
            float damage01 = ResolveDamageProgress(baseHealth, currentHealth);
            UpdateDamageProgressCache(instanceUid, damage01);
            if (damage01 <= 0.0001f && !harvestStateChanged)
                return;

            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = harvestStateChanged
                ? math.saturate(transitionHeightScale)
                : ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            ApplyDamageToLaneInstance(underwater, activeIndex, instanceUid, templateIndex, normalizedHealth, damage01, normalizedHeightScale, currentTime);
        }

        private void UpdateDamageProgressCache(uint instanceUid, float damage01)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || instanceUid == 0u)
                return;

            if (damage01 > 0.0001f)
            {
                _damageVisualProgressByInstanceUid.TryPut(instanceUid, damage01);
                return;
            }

            _damageVisualProgressByInstanceUid.Remove(instanceUid);
        }

        private static float ResolveDamageProgress(float baseHealth, float currentHealth)
        {
            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            return math.saturate((0.5f - normalizedHealth) * 2f);
        }

        private HarvestState ResolveHarvestState(int templateIndex, float baseHealth, float currentHealth, float normalizedHeightScale)
        {
            if (currentHealth <= 0.0001f || normalizedHeightScale <= 0.0001f)
                return HarvestState.Dead;

            float normalizedHealth = currentHealth / math.max(0.0001f, baseHealth);
            if (normalizedHealth >= HarvestStatePartialThreshold01 && normalizedHeightScale >= HarvestStatePartialThreshold01)
                return HarvestState.Pristine;

            float bareThreshold = ResolveBareThreshold01(templateIndex);
            return normalizedHealth <= bareThreshold || normalizedHeightScale <= bareThreshold
                ? HarvestState.Bare
                : HarvestState.PartiallyHarvested;
        }

        private float ResolveNormalizedHeightScale(int templateIndex, float baseHealth, float currentHealth)
        {
            float normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            HarvestState state = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHealth);
            switch (state)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        private float ResolveBareThreshold01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.42f;
                case FloraDataTemplate.FloraCategory.MicroGrass:
                    return 0.22f;
                default:
                    return HarvestStateBareThreshold01;
            }
        }

        private float ResolvePartialHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.68f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.74f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.90f;
                default:
                    return 0.82f;
            }
        }

        private float ResolveBareHeightCeiling01(int templateIndex)
        {
            FloraDataTemplate.FloraCategory category = ResolveDescriptorCategory(templateIndex);
            switch (category)
            {
                case FloraDataTemplate.FloraCategory.HarvestableKelp:
                    return 0.18f;
                case FloraDataTemplate.FloraCategory.GiantSargassum:
                    return 0.24f;
                case FloraDataTemplate.FloraCategory.HardCoral:
                    return 0.58f;
                default:
                    return 0.20f;
            }
        }

        private FloraDataTemplate.FloraCategory ResolveDescriptorCategory(int templateIndex)
        {
            if (_floraCategoryByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _floraCategoryByDescriptorIndex.Length)
                return FloraDataTemplate.FloraCategory.MicroGrass;

            return (FloraDataTemplate.FloraCategory)_floraCategoryByDescriptorIndex[templateIndex];
        }

        private static float ResolveHarvestStateRuntimeState(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                case HarvestState.Dead:
                    return HectonVegetationInstanceData.RuntimeStateDying;
                case HarvestState.PartiallyHarvested:
                    return HectonVegetationInstanceData.RuntimeStateAgitated;
                default:
                    return HectonVegetationInstanceData.RuntimeStateIdle;
            }
        }

        private byte ResolveDescriptorAudioMaterialId(int templateIndex)
        {
            if (_audioMaterialByDescriptorIndex == null || templateIndex < 0 || templateIndex >= _audioMaterialByDescriptorIndex.Length)
                return (byte)FloraDataTemplate.AudioMaterialId.Organic;

            return _audioMaterialByDescriptorIndex[templateIndex];
        }

        private float ResolveNormalizedHeightScaleFromHarvestState(int templateIndex, float normalizedHealth, HarvestState harvestState)
        {
            normalizedHealth = Mathf.Clamp01(normalizedHealth);
            switch (harvestState)
            {
                case HarvestState.Pristine:
                    return 1f;
                case HarvestState.PartiallyHarvested:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolvePartialHeightCeiling01(templateIndex)), MinimumDecomposedHeightScale, 1f);
                case HarvestState.Bare:
                    return Mathf.Clamp(Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)), SoftBareHealthFloor01, 1f);
                default:
                    return MinimumDecomposedHeightScale;
            }
        }

        internal bool IsBareHarvestState(byte packedHarvestState)
        {
            return packedHarvestState == (byte)HarvestState.Bare;
        }

        private void DispatchHarvestAudioTransition(
            uint instanceUid,
            int templateIndex,
            HarvestState previousState,
            HarvestState nextState,
            Vector3 instancePosition)
        {
            if (templateIndex < 0 ||
                instanceUid == 0u ||
                previousState == nextState ||
                !_templateCacheReady)
            {
                return;
            }

            AudioClip clip = ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), nextState);
            if (clip == null)
                return;

            float volume = ResolveHarvestAudioVolume(nextState);
            float pitch = ResolveHarvestAudioPitch(nextState);
            HarvestAudioEvent audioEvent = default;
            audioEvent.RuntimePosition = instancePosition;
            audioEvent.Clip = clip;
            audioEvent.Volume = volume;
            audioEvent.Pitch = pitch;
            if (TryResolveAupFromRuntimeOrigin(instancePosition, out AbsoluteUniversePosition soundAup) &&
                ResolveHarvestAudioSink() != null)
            {
                audioEvent.PositionAup = soundAup;
                audioEvent.HasAup = true;
                QueueHarvestAudioEvent(in audioEvent);
                return;
            }

            audioEvent.PositionAup = default;
            audioEvent.HasAup = false;
            QueueHarvestAudioEvent(in audioEvent);
        }

        private AudioClip ResolveHarvestAudioClip(byte audioMaterialId, HarvestState harvestState)
        {
            switch ((FloraDataTemplate.AudioMaterialId)audioMaterialId)
            {
                case FloraDataTemplate.AudioMaterialId.Brittle:
                    return brittleHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Fibrous:
                    return fibrousHarvestClip != null ? fibrousHarvestClip : organicHarvestClip;
                case FloraDataTemplate.AudioMaterialId.Metallic:
                    return metallicHarvestClip != null ? metallicHarvestClip : brittleHarvestClip;
                default:
                    return harvestState == HarvestState.PartiallyHarvested && fibrousHarvestClip != null
                        ? fibrousHarvestClip
                        : organicHarvestClip;
            }
        }

        private float ResolveHarvestAudioVolume(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.15f);
                case HarvestState.Dead:
                    return Mathf.Clamp01(harvestAudioBaseVolume * 1.25f);
                default:
                    return harvestAudioBaseVolume;
            }
        }

        private static float ResolveHarvestAudioPitch(HarvestState harvestState)
        {
            switch (harvestState)
            {
                case HarvestState.Bare:
                    return 0.9f;
                case HarvestState.Dead:
                    return 0.82f;
                default:
                    return 1f;
            }
        }

        private void TryDispatchMatureSporeAcoustic(
            uint instanceUid,
            float progress01,
            bool underwater,
            int activeIndex,
            int templateIndex,
            float currentTime)
        {
            if (instanceUid == 0u || !_nextSporeAcousticTimeByInstanceUid.IsCreated)
                return;

            if (progress01 < MatureSporeGrowthThreshold01)
            {
                _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
                return;
            }

            if (!IsMatureSporeAcousticEmitter(templateIndex))
                return;

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return;

            AudioClip clip = ResolveMatureSporeAcousticClip(templateIndex);
            if (clip == null)
                return;

            float pulseFrequency = ResolveMatureSporePulseFrequency(templateIndex);
            Vector3 instancePosition = ExtractTranslation(matrices[activeIndex]);
            float phaseOffset01 = ResolveSporeShaderPhaseOffset01(instancePosition);
            if (!_nextSporeAcousticTimeByInstanceUid.TryGetValue(instanceUid, out float nextAllowedTime))
            {
                nextAllowedTime = ResolveNextSporePulseTime(currentTime, pulseFrequency, phaseOffset01);
                _nextSporeAcousticTimeByInstanceUid.TryPut(instanceUid, nextAllowedTime);
                if (currentTime < nextAllowedTime)
                    return;
            }
            else if (currentTime < nextAllowedTime)
            {
                return;
            }

            float volume = ResolveMatureSporeAcousticVolume(templateIndex);
            float pitch = ResolveMatureSporeAcousticPitch(pulseFrequency);
            SporeAcousticEvent acousticEvent = default;
            acousticEvent.RuntimePosition = instancePosition;
            acousticEvent.Clip = clip;
            acousticEvent.PulseFrequencyHz = pulseFrequency;
            acousticEvent.Volume = volume;
            acousticEvent.Pitch = pitch;
            acousticEvent.SimulationTimeSeconds = nextAllowedTime;
            acousticEvent.PhaseOffset01 = phaseOffset01;
            if (TryResolveAupFromRuntimeOrigin(instancePosition, out AbsoluteUniversePosition soundAup))
            {
                acousticEvent.PositionAup = soundAup;
                acousticEvent.HasAup = true;
                QueueSporeAcousticEvent(in acousticEvent);
            }
            else
            {
                acousticEvent.PositionAup = default;
                acousticEvent.HasAup = false;
                QueueSporeAcousticEvent(in acousticEvent);
            }

            _nextSporeAcousticTimeByInstanceUid.TryPut(instanceUid, ResolveNextSporePulseTime(currentTime + 0.0001f, pulseFrequency, phaseOffset01));
        }

        private void DispatchSporeAcousticEvent(in SporeAcousticEvent acousticEvent)
        {
            ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink();
            if (harvestAudioSink != null && acousticEvent.HasAup)
            {
                AbsoluteUniversePosition positionAup = acousticEvent.PositionAup;
                harvestAudioSink.PlaySporeEmissionAtAup(
                    in positionAup,
                    acousticEvent.Clip,
                    acousticEvent.PulseFrequencyHz,
                    acousticEvent.SimulationTimeSeconds,
                    acousticEvent.PhaseOffset01,
                    acousticEvent.Volume);
                return;
            }

            IAudioService audioService = ResolveAudioService();
            if (audioService != null)
                audioService.PlayAtPoint(
                    acousticEvent.Clip,
                    acousticEvent.RuntimePosition,
                    acousticEvent.Volume,
                    acousticEvent.Pitch);
        }

        private void QueueSporeAcousticEvent(in SporeAcousticEvent acousticEvent)
        {
            if (acousticEvent.Clip == null ||
                _pendingSporeAcousticEventCount >= _pendingSporeAcousticEvents.Length)
            {
                return;
            }

            _pendingSporeAcousticEvents[_pendingSporeAcousticEventCount] = acousticEvent;
            _pendingSporeAcousticEventCount++;
        }

        private void QueueHarvestAudioEvent(in HarvestAudioEvent audioEvent)
        {
            if (audioEvent.Clip == null ||
                _pendingHarvestAudioEventCount >= _pendingHarvestAudioEvents.Length)
            {
                return;
            }

            _pendingHarvestAudioEvents[_pendingHarvestAudioEventCount] = audioEvent;
            _pendingHarvestAudioEventCount++;
        }

        private void FlushPendingHarvestAudioEvents()
        {
            int count = math.min(_pendingHarvestAudioEventCount, _pendingHarvestAudioEvents.Length);
            if (count <= 0)
                return;

            _pendingHarvestAudioEventCount = 0;
            for (int i = 0; i < count; i++)
            {
                HarvestAudioEvent audioEvent = _pendingHarvestAudioEvents[i];
                _pendingHarvestAudioEvents[i] = default;
                ISpatialAudioHarvestPlaybackSink harvestAudioSink = ResolveHarvestAudioSink();
                if (harvestAudioSink != null && audioEvent.HasAup)
                {
                    AbsoluteUniversePosition positionAup = audioEvent.PositionAup;
                    harvestAudioSink.PlayHarvestAtAup(in positionAup, audioEvent.Clip, audioEvent.Volume, audioEvent.Pitch);
                    continue;
                }

                IAudioService audioService = ResolveAudioService();
                if (audioService != null)
                    audioService.PlayAtPoint(audioEvent.Clip, audioEvent.RuntimePosition, audioEvent.Volume, audioEvent.Pitch);
            }
        }

        private void FlushPendingSporeAcousticEvents()
        {
            int count = math.min(_pendingSporeAcousticEventCount, _pendingSporeAcousticEvents.Length);
            if (count <= 0)
                return;

            _pendingSporeAcousticEventCount = 0;
            for (int i = 0; i < count; i++)
            {
                SporeAcousticEvent acousticEvent = _pendingSporeAcousticEvents[i];
                _pendingSporeAcousticEvents[i] = default;
                DispatchSporeAcousticEvent(in acousticEvent);
            }
        }

        private static float ResolveSporeShaderPhaseOffset01(Vector3 instancePosition)
        {
            return math.frac((instancePosition.x * SporeShaderPhasePositionX + instancePosition.z * SporeShaderPhasePositionZ) * InvTwoPi);
        }

        private static float ResolveNextSporePulseTime(float simulationTimeSeconds, float pulseFrequencyHz, float phaseOffset01)
        {
            float safePulseFrequency = math.max(MinimumSporePulseFrequencyHz, pulseFrequencyHz);
            float currentCycle = simulationTimeSeconds * safePulseFrequency + phaseOffset01 - SporePulsePeakPhase01;
            float nextCycle = math.floor(currentCycle) + 1f;
            return (nextCycle + SporePulsePeakPhase01 - phaseOffset01) / safePulseFrequency;
        }

        private static bool IsMatureGrowth(HectonVegetationInstanceData metadata)
        {
            return metadata.Reserved0 <= 0.0001f || metadata.Reserved0 >= MatureSporeGrowthThreshold01;
        }

        private bool IsMatureSporeAcousticEmitter(int templateIndex)
        {
            return _sporeAcousticEmitterByDescriptorIndex != null &&
                   templateIndex >= 0 &&
                   templateIndex < _sporeAcousticEmitterByDescriptorIndex.Length &&
                   _sporeAcousticEmitterByDescriptorIndex[templateIndex] != 0;
        }

        private AudioClip ResolveMatureSporeAcousticClip(int templateIndex)
        {
            if (_sporeAcousticClipByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticClipByDescriptorIndex.Length &&
                _sporeAcousticClipByDescriptorIndex[templateIndex] != null)
            {
                return _sporeAcousticClipByDescriptorIndex[templateIndex];
            }

            return sporeAcousticFallbackClip != null
                ? sporeAcousticFallbackClip
                : ResolveHarvestAudioClip(ResolveDescriptorAudioMaterialId(templateIndex), HarvestState.PartiallyHarvested);
        }

        private float ResolveMatureSporePulseFrequency(int templateIndex)
        {
            if (_sporePulseFrequencyByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporePulseFrequencyByDescriptorIndex.Length)
            {
                return Mathf.Max(MinimumSporePulseFrequencyHz, _sporePulseFrequencyByDescriptorIndex[templateIndex]);
            }

            return 1f;
        }

        private float ResolveMatureSporeAcousticVolume(int templateIndex)
        {
            if (_sporeAcousticVolumeByDescriptorIndex != null &&
                templateIndex >= 0 &&
                templateIndex < _sporeAcousticVolumeByDescriptorIndex.Length &&
                _sporeAcousticVolumeByDescriptorIndex[templateIndex] > 0.0001f)
            {
                return Mathf.Clamp01(_sporeAcousticVolumeByDescriptorIndex[templateIndex]);
            }

            return sporeAcousticFallbackVolume;
        }

        private static float ResolveMatureSporeAcousticPitch(float pulseFrequency)
        {
            return Mathf.Clamp(pulseFrequency, 0.1f, 3f);
        }

        private bool TryResolvePersistedFloraState(uint instanceUid, out float normalizedHealth, out float normalizedHeightScale)
        {
            normalizedHealth = 1f;
            normalizedHeightScale = 1f;
            if (!_persistedHealth01ByInstanceUid.IsCreated || !_persistedHeightScale01ByInstanceUid.IsCreated || instanceUid == 0u)
                return false;

            bool hasHealth = _persistedHealth01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHealth);
            bool hasHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight);
            if (!hasHealth || !hasHeight)
                return false;

            normalizedHealth = Mathf.Clamp01((float)persistedHealth);
            normalizedHeightScale = Mathf.Clamp01((float)persistedHeight);
            return true;
        }

        private float ResolvePersistedNormalizedHeightScale(uint instanceUid)
        {
            if (!_persistedHeightScale01ByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half persistedHeight))
            {
                return 0f;
            }

            return Mathf.Clamp01((float)persistedHeight);
        }

        private float ResolveRuntimeNormalizedHeightScale(uint instanceUid, HectonVegetationInstanceData metadata)
        {
            if (!_baseScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale));
            }

            return Mathf.Clamp01(Mathf.Abs(metadata.HeightScale) / math.max(0.0001f, baseScale.x));
        }

        private void ClearPersistedFloraStateOverride(uint instanceUid)
        {
            if (_persistedHealth01ByInstanceUid.IsCreated)
                _persistedHealth01ByInstanceUid.Remove(instanceUid);

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
                _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
        }

        private void PersistFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth)
        {
            if (TryCacheFloraStateOverride(
                    instanceUid,
                    templateIndex,
                    underwater,
                    activeIndex,
                    baseHealth,
                    currentHealth,
                    out float normalizedHealth,
                    out byte harvestState,
                    out bool clearOverride))
            {
                PublishFloraStateOverride(instanceUid, templateIndex, instancePosition, normalizedHealth, harvestState, clearOverride);
            }
        }

        private bool TryCacheFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            bool underwater,
            int activeIndex,
            float baseHealth,
            float currentHealth,
            out float normalizedHealth,
            out byte harvestState,
            out bool clearOverride)
        {
            normalizedHealth = 0f;
            harvestState = 0;
            clearOverride = false;
            if (instanceUid == 0u || !_templateCacheReady || templateIndex < 0)
                return false;

            normalizedHealth = math.saturate(currentHealth / math.max(0.0001f, baseHealth));
            float normalizedHeightScale = ResolveCurrentNormalizedHeightScale(underwater, activeIndex, instanceUid, normalizedHealth);
            HarvestState resolvedHarvestState = ResolveHarvestState(templateIndex, baseHealth, currentHealth, normalizedHeightScale);
            harvestState = (byte)resolvedHarvestState;
            if (PersistentWorldRegistry.IsPristineFloraState(normalizedHealth, normalizedHeightScale))
            {
                ClearPersistedFloraStateOverride(instanceUid);
                clearOverride = true;
                return true;
            }

            if (!_persistedHealth01ByInstanceUid.IsCreated || !_persistedHeightScale01ByInstanceUid.IsCreated)
                return false;

            bool hadHealth = _persistedHealth01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half previousHealth);
            bool hadHeight = _persistedHeightScale01ByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half previousHeight);
            bool healthAdded = _persistedHealth01ByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)normalizedHealth);
            bool heightAdded = _persistedHeightScale01ByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)normalizedHeightScale);
            if (!healthAdded || !heightAdded)
            {
                RestorePersistedFloraStateOverridePair(instanceUid, hadHealth, previousHealth, hadHeight, previousHeight);
                return false;
            }

            return true;
        }

        private void RestorePersistedFloraStateOverridePair(
            uint instanceUid,
            bool hadHealth,
            Unity.Mathematics.half previousHealth,
            bool hadHeight,
            Unity.Mathematics.half previousHeight)
        {
            if (_persistedHealth01ByInstanceUid.IsCreated)
            {
                if (hadHealth)
                    _persistedHealth01ByInstanceUid.TryPut(instanceUid, previousHealth);
                else
                    _persistedHealth01ByInstanceUid.Remove(instanceUid);
            }

            if (_persistedHeightScale01ByInstanceUid.IsCreated)
            {
                if (hadHeight)
                    _persistedHeightScale01ByInstanceUid.TryPut(instanceUid, previousHeight);
                else
                    _persistedHeightScale01ByInstanceUid.Remove(instanceUid);
            }
        }

        private void PublishFloraStateOverride(
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            float normalizedHealth,
            byte harvestState,
            bool clearOverride)
        {
            PersistentWorldRegistry registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            if (clearOverride)
            {
                registry.TryClearFloraStateOverride(instanceUid);
                return;
            }

            if (instanceUid == 0u ||
                !TrySnapshotTemplateDescriptorWithLock(templateIndex, out HarvestableTemplate.RuntimeDescriptor descriptor))
            {
                return;
            }

            registry.TryRegisterFloraStateOverride(
                (ulong)(uint)descriptor.StableHashId,
                instanceUid,
                instancePosition,
                normalizedHealth,
                (byte)harvestState);
        }

        private float ResolveCurrentNormalizedHeightScale(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            float fallbackNormalizedHeightScale)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return Mathf.Clamp01(fallbackNormalizedHeightScale);

            return ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[activeIndex]);
        }

        private float GetLaneHealth(bool underwater, int activeIndex)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            return laneHealth.IsCreated && (uint)activeIndex < (uint)laneHealth.Length ? (float)laneHealth[activeIndex] : 0f;
        }

        private void SetLaneHealth(bool underwater, int activeIndex, float value)
        {
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            if (!laneHealth.IsCreated || activeIndex < 0 || activeIndex >= laneHealth.Length)
                return;

            laneHealth[activeIndex] = (Unity.Mathematics.half)Mathf.Max(0f, value);
        }

        private void SuppressActiveInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                SuppressActiveInstance(ref _underwaterMatrices, ref _underwaterMetadata, activeIndex);
            else
                SuppressActiveInstance(ref _surfaceMatrices, ref _surfaceMetadata, activeIndex);
        }

        private void ApplyWiltToLaneInstance(bool underwater, int activeIndex, float wiltEndTime)
        {
            float wiltStartTime = wiltEndTime - OrganicWiltDurationSeconds;
            if (underwater)
                ApplyWiltMetadata(ref _underwaterMetadata, activeIndex, wiltStartTime);
            else
                ApplyWiltMetadata(ref _surfaceMetadata, activeIndex, wiltStartTime);
        }

        private void ApplyDamageToLaneInstance(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float damage01,
            float normalizedHeightScale,
            float currentTime)
        {
            if (underwater)
                ApplyPersistedDamageMetadata(ref _underwaterMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            else
                ApplyPersistedDamageMetadata(ref _surfaceMetadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
        }

        private void UpdateRegrowthVisuals()
        {
            if (!_regrowthProgressByInstanceUid.IsCreated || _regrowthProgressByInstanceUid.Count <= 0)
                return;

            UpdateRegrowthLane(false, ref _surfaceRegrowthVisualScanCursor);
            UpdateRegrowthLane(true, ref _underwaterRegrowthVisualScanCursor);
        }

        private void UpdateRegrowthLane(bool underwater, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, instanceUids.Length);
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = ResolveOrganicVisualScanBudget(safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u || !_regrowthProgressByInstanceUid.TryGetValue(instanceUid, out float progress01))
                    continue;

                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, progress01);
            }
        }

        private void UpdateMatureSporeAcoustics(float currentTime)
        {
            if (!_nextSporeAcousticTimeByInstanceUid.IsCreated ||
                _sporeAcousticEmitterByDescriptorIndex == null ||
                _sporeAcousticEmitterByDescriptorIndex.Length == 0)
            {
                return;
            }

            UpdateMatureSporeAcousticLane(false, currentTime, ref _surfaceMatureSporeScanCursor);
            UpdateMatureSporeAcousticLane(true, currentTime, ref _underwaterMatureSporeScanCursor);
        }

        private void UpdateMatureSporeAcousticLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(metadata.Length, materialClasses.Length)));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = ResolveMatureSporeAcousticScanBudget(safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)))
                {
                    continue;
                }

                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                if (!IsMatureGrowth(instanceData) ||
                    instanceData.HealthNormalized < MatureSporeGrowthThreshold01 ||
                    instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(instanceData.HeightScale) <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = ResolveTemplateIndex(instanceData, (HarvestableTemplate.MaterialClass)materialClasses[activeIndex]);
                if (!IsMatureSporeAcousticEmitter(templateIndex))
                    continue;

                TryDispatchMatureSporeAcoustic(instanceUid, 1f, underwater, activeIndex, templateIndex, currentTime);
            }
        }

        private int ResolveMatureSporeAcousticScanBudget(int safeCount)
        {
            if (safeCount <= 0)
                return 0;

            int maxBudget = math.max(1, matureSporeAcousticScanBudgetPerTick);
            int minBudget = math.min(maxBudget, MatureSporeMinChecksPerTick);
            float q = ResolveDearLieGlobalQualityWeight();
            int resolvedBudget = math.max(
                minBudget,
                (int)math.round(math.lerp(minBudget, maxBudget, q * q)));
            return math.min(safeCount, resolvedBudget);
        }

        private int ResolveOrganicVisualScanBudget(int safeCount)
        {
            if (safeCount <= 0)
                return 0;

            float q = ResolveDearLieGlobalQualityWeight();
            int resolvedBudget = math.max(
                OrganicVisualMinChecksPerTick,
                (int)math.round(math.lerp(OrganicVisualMinChecksPerTick, OrganicVisualMaxChecksPerTick, q * q)));
            return math.min(safeCount, resolvedBudget);
        }

        private void UpdateDecompositionVisuals(float currentTime)
        {
            if (!_decompositionStartTimeByInstanceUid.IsCreated || _decompositionStartTimeByInstanceUid.Count <= 0)
                return;

            UpdateDecompositionLane(false, currentTime, ref _surfaceDecompositionVisualScanCursor);
            UpdateDecompositionLane(true, currentTime, ref _underwaterDecompositionVisualScanCursor);
        }

        private void UpdateDecompositionLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, metadata.Length));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = ResolveOrganicVisualScanBudget(safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    !_destroyedByInstanceUid.IsCreated ||
                    !_destroyedByInstanceUid.ContainsKey(instanceUid))
                {
                    continue;
                }

                float entropy01 = EnsureDecompositionProgress(instanceUid, currentTime);
                ApplyDecompositionMetadata(ref metadata, activeIndex, instanceUid, entropy01);
            }
        }

        private void UpdateDamageVisuals(float currentTime)
        {
            if (!_damageVisualProgressByInstanceUid.IsCreated || _damageVisualProgressByInstanceUid.Count <= 0)
                return;

            UpdateDamageLane(false, currentTime, ref _surfaceDamageVisualScanCursor);
            UpdateDamageLane(true, currentTime, ref _underwaterDamageVisualScanCursor);
        }

        private void UpdateDamageLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || !materialClasses.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(metadata.Length, materialClasses.Length)));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = ResolveOrganicVisualScanBudget(safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)) ||
                    (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_pendingWiltEndTimeByInstanceUid.IsCreated && _pendingWiltEndTimeByInstanceUid.ContainsKey(instanceUid)) ||
                    !_damageVisualProgressByInstanceUid.TryGetValue(instanceUid, out float damage01))
                {
                    continue;
                }

                float normalizedHeightScale = ResolvePersistedNormalizedHeightScale(instanceUid);
                if (normalizedHeightScale <= 0.0001f)
                    normalizedHeightScale = ResolveRuntimeNormalizedHeightScale(instanceUid, metadata[activeIndex]);

                int templateIndex = ResolveTemplateIndex(metadata[activeIndex], (HarvestableTemplate.MaterialClass)materialClasses[activeIndex]);
                float baseHealth = TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor damageDescriptor)
                    ? Mathf.Max(0.1f, damageDescriptor.BaseHealth)
                    : 1f;
                float normalizedHealth = _healthByInstanceUid.IsCreated && _healthByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half trackedHealth)
                    ? Mathf.Clamp01((float)trackedHealth / baseHealth)
                    : Mathf.Clamp01(1f - (damage01 * 0.5f));
                ApplyPersistedDamageMetadata(ref metadata, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
            }
        }

        private void UpdateWiltInstances(float currentTime)
        {
            if (!_pendingWiltEndTimeByInstanceUid.IsCreated || _pendingWiltEndTimeByInstanceUid.Count <= 0)
                return;

            UpdateWiltLane(false, currentTime, ref _surfaceWiltVisualScanCursor);
            UpdateWiltLane(true, currentTime, ref _underwaterWiltVisualScanCursor);
        }

        private void UpdateWiltLane(bool underwater, float currentTime, ref int scanCursor)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !metadata.IsCreated || count <= 0)
            {
                scanCursor = 0;
                return;
            }

            int safeCount = math.min(count, math.min(instanceUids.Length, metadata.Length));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int budget = ResolveOrganicVisualScanBudget(safeCount);
            for (int checkedCount = 0; checkedCount < budget; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u || !_pendingWiltEndTimeByInstanceUid.TryGetValue(instanceUid, out float wiltEndTime))
                    continue;

                if (currentTime >= wiltEndTime)
                {
                    SuppressActiveInstance(underwater, activeIndex);
                    _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);
                    continue;
                }

                ApplyWiltMetadata(ref metadata, activeIndex, wiltEndTime - OrganicWiltDurationSeconds);
            }
        }

        private static void SuppressActiveInstance(
            ref NativeArray<Matrix4x4> matrices,
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex)
        {
            if (!matrices.IsCreated || !metadata.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length || activeIndex >= metadata.Length)
                return;

            Matrix4x4 hiddenMatrix = matrices[activeIndex];
            hiddenMatrix.m03 = 0f;
            hiddenMatrix.m13 = HiddenInstanceWorldY;
            hiddenMatrix.m23 = 0f;
            matrices[activeIndex] = hiddenMatrix;

            HectonVegetationInstanceData hiddenMetadata = metadata[activeIndex];
            hiddenMetadata.Type = 0f;
            hiddenMetadata.HeightScale = 0f;
            hiddenMetadata.WidthScale = 0f;
            hiddenMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateIdle;
            hiddenMetadata.RuntimeFlags = 0f;
            hiddenMetadata.HealthNormalized = 0f;
            hiddenMetadata.Reserved0 = -1f;
            metadata[activeIndex] = hiddenMetadata;
        }

        private static void SuppressActiveInstance(
            ref BridgeMatrixLane matrices,
            ref BridgeMetadataLane metadata,
            int activeIndex)
        {
            NativeArray<Matrix4x4> matrixBuffer = matrices;
            NativeArray<HectonVegetationInstanceData> metadataBuffer = metadata;
            SuppressActiveInstance(ref matrixBuffer, ref metadataBuffer, activeIndex);
        }

        private void ApplyDearLieMatrixScaleZeroToLaneInstance(bool underwater, int activeIndex)
        {
            if (underwater)
                ApplyDearLieMatrixScaleZero(ref _underwaterMatrices, activeIndex);
            else
                ApplyDearLieMatrixScaleZero(ref _surfaceMatrices, activeIndex);
        }

        private static void ApplyDearLieMatrixScaleZero(ref NativeArray<Matrix4x4> matrices, int activeIndex)
        {
            if (!matrices.IsCreated || activeIndex < 0 || activeIndex >= matrices.Length)
                return;

            Matrix4x4 matrix = matrices[activeIndex];
            ScaleMatrixColumnsToZero(ref matrix);
            matrices[activeIndex] = matrix;
        }

        private static void ApplyDearLieMatrixScaleZero(ref BridgeMatrixLane matrices, int activeIndex)
        {
            NativeArray<Matrix4x4> buffer = matrices;
            ApplyDearLieMatrixScaleZero(ref buffer, activeIndex);
        }

        private static void ScaleMatrixColumnsToZero(ref Matrix4x4 matrix)
        {
            matrix.m00 = 0f;
            matrix.m01 = 0f;
            matrix.m02 = 0f;
            matrix.m10 = 0f;
            matrix.m11 = 0f;
            matrix.m12 = 0f;
            matrix.m20 = 0f;
            matrix.m21 = 0f;
            matrix.m22 = 0f;
        }

        private static void ApplyWiltMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float wiltStartTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HectonVegetationInstanceData wiltMetadata = metadata[activeIndex];
            wiltMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(wiltMetadata.HeightScale));
            wiltMetadata.WidthScale = Mathf.Max(0.001f, wiltStartTime);
            wiltMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            wiltMetadata.HealthNormalized = 0f;
            metadata[activeIndex] = wiltMetadata;
        }

        private static void ApplyWiltMetadata(
            ref BridgeMetadataLane metadata,
            int activeIndex,
            float wiltStartTime)
        {
            NativeArray<HectonVegetationInstanceData> buffer = metadata;
            ApplyWiltMetadata(ref buffer, activeIndex, wiltStartTime);
        }

        private void ApplyDecompositionMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            float entropy01)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : UnitFloat2();
            float smoothEntropy = entropy01 * entropy01 * (3f - (2f * entropy01));
            float decompositionStartTime = 0f;
            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.TryGetValue(instanceUid, out decompositionStartTime);

            HectonVegetationInstanceData decompositionMetadata = metadata[activeIndex];
            decompositionMetadata.HeightScale = -math.lerp(baseScale.x, MinimumDecomposedHeightScale, smoothEntropy);
            decompositionMetadata.WidthScale = -Mathf.Max(0.001f, decompositionStartTime);
            decompositionMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateDying;
            decompositionMetadata.HealthNormalized = math.lerp(1f, 0f, smoothEntropy);
            decompositionMetadata.Reserved0 = -1f;
            metadata[activeIndex] = decompositionMetadata;
        }

        private void ApplyDecompositionMetadata(
            ref BridgeMetadataLane metadata,
            int activeIndex,
            uint instanceUid,
            float entropy01)
        {
            NativeArray<HectonVegetationInstanceData> buffer = metadata;
            ApplyDecompositionMetadata(ref buffer, activeIndex, instanceUid, entropy01);
        }

        private static void ApplyDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length || damage01 <= 0.0001f)
                return;

            HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
            damageMetadata.HeightScale = -Mathf.Max(0.05f, Mathf.Abs(damageMetadata.HeightScale));
            damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
            damageMetadata.RuntimeState = HectonVegetationInstanceData.RuntimeStateAgitated;
            damageMetadata.HealthNormalized = Mathf.Clamp01(1f - damage01);
            metadata[activeIndex] = damageMetadata;
        }

        private void ApplyPersistedDamageMetadata(
            ref NativeArray<HectonVegetationInstanceData> metadata,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float normalizedHeightScale,
            float damage01,
            float currentTime)
        {
            if (!metadata.IsCreated || activeIndex < 0 || activeIndex >= metadata.Length)
                return;

            HarvestState harvestState = ResolveHarvestState(
                templateIndex,
                1f,
                Mathf.Clamp01(normalizedHeightScale),
                Mathf.Clamp01(normalizedHeightScale));
            if (_baseScaleByInstanceUid.IsCreated &&
                _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 baseScale))
            {
                float clampedHeight01 = math.saturate(normalizedHeightScale);
                harvestState = ResolveHarvestState(templateIndex, baseScale.x, baseScale.x * clampedHeight01, clampedHeight01);
                HectonVegetationInstanceData damageMetadata = metadata[activeIndex];
                damageMetadata.HeightScale = -Mathf.Max(MinimumDecomposedHeightScale, baseScale.x * clampedHeight01);
                damageMetadata.WidthScale = currentTime - (Mathf.Clamp01(damage01) * OrganicWiltDurationSeconds);
                damageMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
                damageMetadata.HealthNormalized = math.saturate(normalizedHealth);
                metadata[activeIndex] = damageMetadata;
                return;
            }

            ApplyDamageMetadata(ref metadata, activeIndex, damage01, currentTime);
            HectonVegetationInstanceData fallbackMetadata = metadata[activeIndex];
            fallbackMetadata.RuntimeState = ResolveHarvestStateRuntimeState(harvestState);
            fallbackMetadata.HealthNormalized = math.saturate(normalizedHealth);
            metadata[activeIndex] = fallbackMetadata;
        }

        private void ApplyPersistedDamageMetadata(
            ref BridgeMetadataLane metadata,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            float normalizedHealth,
            float normalizedHeightScale,
            float damage01,
            float currentTime)
        {
            NativeArray<HectonVegetationInstanceData> buffer = metadata;
            ApplyPersistedDamageMetadata(ref buffer, activeIndex, instanceUid, templateIndex, normalizedHealth, normalizedHeightScale, damage01, currentTime);
        }

        private void EvaluateParasiteExposureInLane(
            Vector3 runtimePosition,
            bool underwater,
            int scanBudget,
            ref int scanCursor,
            ref float bestExposure)
        {
            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            NativeArray<Unity.Mathematics.half> laneHealth = underwater ? _underwaterHealth : _surfaceHealth;
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            int count = underwater ? _underwaterCount : _surfaceCount;
            if (!instanceUids.IsCreated || !laneHealth.IsCreated || !matrices.IsCreated || count <= 0 || !_runtimeFlagsByInstanceUid.IsCreated)
            {
                scanCursor = 0;
                return;
            }

            const float parasiteRadius = 3.25f;
            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(laneHealth.Length, matrices.Length)));
            if (safeCount <= 0)
            {
                scanCursor = 0;
                return;
            }

            if ((uint)scanCursor >= (uint)safeCount)
                scanCursor = 0;

            int checks = math.min(math.max(1, scanBudget), safeCount);
            for (int checkedCount = 0; checkedCount < checks; checkedCount++)
            {
                int activeIndex = scanCursor;
                scanCursor++;
                if (scanCursor >= safeCount)
                    scanCursor = 0;

                uint instanceUid = instanceUids[activeIndex];
                if (instanceUid == 0u ||
                    (float)laneHealth[activeIndex] <= 0.0001f ||
                    !_runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out byte runtimeFlags) ||
                    (runtimeFlags & FloraRuntimeFlagHasParasite) == 0)
                {
                    continue;
                }

                Vector3 delta = ExtractTranslation(matrices[activeIndex]) - runtimePosition;
                float distanceSq = delta.sqrMagnitude;
                float radiusSq = parasiteRadius * parasiteRadius;
                if (distanceSq >= radiusSq)
                    continue;

                float exposure = 1f - math.saturate(distanceSq / radiusSq);
                if (exposure > bestExposure)
                    bestExposure = exposure;
            }
        }

        private bool TryPrepareTitanRootMoundRequest(bool underwater, int activeIndex, uint instanceUid, out Vector3 runtimePosition, out bool rootMoundWriteFailed)
        {
            runtimePosition = default;
            rootMoundWriteFailed = false;
            if (instanceUid == 0u ||
                !_rootMoundAppliedByInstanceUid.IsCreated)
            {
                return false;
            }

            if (_rootMoundAppliedByInstanceUid.TryGetValue(instanceUid, out byte rootMoundState) &&
                rootMoundState >= TitanRootMoundApplied)
            {
                return false;
            }

            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return false;
            }

            if ((HectonVegetationInstanceType)types[activeIndex] != HectonVegetationInstanceType.GiantKelp)
                return false;

            HectonVegetationInstanceData instanceData = metadata[activeIndex];
            if (math.saturate(instanceData.Reserved0) < TitanRootMoundMatureThreshold01 &&
                math.saturate(instanceData.HealthNormalized) < TitanRootMoundMatureThreshold01 &&
                math.saturate(math.abs(instanceData.HeightScale)) < TitanRootMoundMatureThreshold01)
            {
                return false;
            }

            runtimePosition = ExtractTranslation(matrices[activeIndex]);
            if (!IsFiniteVector(runtimePosition))
                return false;

            if (rootMoundState < TitanRootMoundPending && !_rootMoundAppliedByInstanceUid.TryPut(instanceUid, TitanRootMoundPending))
            {
                rootMoundWriteFailed = true;
                return false;
            }

            return true;
        }

        private bool TryApplyPreparedTitanRootMound(uint instanceUid, Vector3 runtimePosition)
        {
            if (instanceUid == 0u || !TryApplyTitanRootMoundRequest(runtimePosition))
                return false;

            return TryMarkTitanRootMoundApplied(instanceUid);
        }

        private bool TryMarkTitanRootMoundApplied(uint instanceUid)
        {
            if (instanceUid == 0u || !_rootMoundAppliedByInstanceUid.IsCreated)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicBufferGuard(vault, OrganicRootMoundAppliedByUidBufferId, out ulong guardMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            bool markFailed = false;
            try
            {
                markFailed = !_rootMoundAppliedByInstanceUid.TryPut(instanceUid, TitanRootMoundApplied);
            }
            finally
            {
                ReleaseOrganicGuard(vault, guardMask);
            }

            if (markFailed)
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);
                return false;
            }

            return true;
        }

        private static bool TryApplyTitanRootMoundRequest(Vector3 runtimePosition)
        {
            HectonVoxelEngine voxelEngine = null;
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
            if (voxelEngine == null)
                return false;

            if (!voxelEngine.TryGetNearestActiveVolume(runtimePosition, out HectonVoxelVolume volume) || volume == null)
                return false;

            volume.ApplyOrganicRootMound(runtimePosition, TitanRootMoundRadiusMeters, TitanRootMoundStrengthMeters);
            return true;
        }

        internal bool IsMaterialClassRegrowable(ulong floraPersistentIdHash)
        {
            if (!TryFindTemplateDescriptorByPersistentHashWithLock(floraPersistentIdHash, out _, out HarvestableTemplate.RuntimeDescriptor descriptor))
                return false;

            HarvestableTemplate.MaterialClass materialClass = (HarvestableTemplate.MaterialClass)descriptor.MaterialClassId;
            return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                   materialClass == HarvestableTemplate.MaterialClass.Sargassum;
        }

        internal float ResolveGrowthTimeSeconds(ulong floraPersistentIdHash)
        {
            if (!TryFindTemplateDescriptorByPersistentHashWithLock(floraPersistentIdHash, out int descriptorIndex, out _))
                return 480f;

            if (_growthTimeSecondsByDescriptorIndex != null && descriptorIndex < _growthTimeSecondsByDescriptorIndex.Length)
                return Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[descriptorIndex]);

            return 480f;
        }

        internal bool TryResolveFloraGrowthDescriptor(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            out uint instanceUid,
            out ulong floraPersistentIdHash,
            out float growthTimeSeconds)
        {
            instanceUid = ComputeStableInstanceUid(matrix, metadata, typeId, semanticType);
            floraPersistentIdHash = 0UL;
            growthTimeSeconds = 0f;

            HarvestableTemplate.MaterialClass fallbackMaterialClass = ResolveMaterialClass(typeId, semanticType);
            int templateIndex = ResolveTemplateIndex(metadata, fallbackMaterialClass);
            if (!TrySnapshotTemplateDescriptorWithLock(templateIndex, out HarvestableTemplate.RuntimeDescriptor descriptor))
                return false;

            floraPersistentIdHash = (ulong)(uint)descriptor.StableHashId;
            growthTimeSeconds = _growthTimeSecondsByDescriptorIndex != null && templateIndex < _growthTimeSecondsByDescriptorIndex.Length
                ? Mathf.Max(1f, _growthTimeSecondsByDescriptorIndex[templateIndex])
                : 480f;
            return floraPersistentIdHash != 0UL;
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01)
        {
            float multiplier = EvaluateMaturationScaleMultiplier(progress01);
            return TrySetMaturationProgress(instanceUid, progress01, multiplier, Mathf.Clamp01(progress01));
        }

        internal bool TrySetMaturationProgress(uint instanceUid, float progress01, float scaleMultiplier, float resourceYieldMultiplier)
        {
            if (_dearLieJobScheduled || instanceUid == 0u || !_maturationScaleByInstanceUid.IsCreated)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            bool applyTitanRootMound = false;
            bool titanRootMoundWriteFailed = false;
            Vector3 titanRootMoundPosition = default;
            try
            {
                float clampedProgress = Mathf.Clamp01(progress01);
                scaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
                resourceYieldMultiplier = clampedProgress < 0.2f ? 0f : Mathf.Clamp01(resourceYieldMultiplier);
                if (scaleMultiplier < 0.9999f)
                    _maturationScaleByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)scaleMultiplier);
                else
                    _maturationScaleByInstanceUid.Remove(instanceUid);

                if (_maturationYieldByInstanceUid.IsCreated)
                {
                    if (resourceYieldMultiplier < 0.9999f)
                        _maturationYieldByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)resourceYieldMultiplier);
                    else
                        _maturationYieldByInstanceUid.Remove(instanceUid);
                }

                if (TryFindActiveInstanceByUidPinned(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
                {
                    ApplyMaturationVisualToLaneInstance(underwater, activeIndex, instanceUid, clampedProgress, scaleMultiplier);
                    TryDispatchMatureSporeAcoustic(instanceUid, clampedProgress, underwater, activeIndex, templateIndex, ResolveOrganicClockSeconds());
                    if (clampedProgress >= TitanRootMoundMatureThreshold01)
                        applyTitanRootMound = TryPrepareTitanRootMoundRequest(underwater, activeIndex, instanceUid, out titanRootMoundPosition, out titanRootMoundWriteFailed);
                }
                else if (clampedProgress < MatureSporeGrowthThreshold01 && _nextSporeAcousticTimeByInstanceUid.IsCreated)
                {
                    _nextSporeAcousticTimeByInstanceUid.Remove(instanceUid);
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (applyTitanRootMound)
                TryApplyPreparedTitanRootMound(instanceUid, titanRootMoundPosition);

            if (titanRootMoundWriteFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            return true;
        }

        bool IOrganicToolHitService.TryApplyOrganicToolHit(
            Vector3 hitPoint,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            return TryApplyToolHit(hitPoint, hitNormal, direction, deliveredDamage, normalizedPower, toolCapabilityMask);
        }

        bool IOrganicToolHitService.TryApplyAttachedFloraToolHit(
            Vector3 hitPoint,
            float searchRadius,
            Vector3 hitNormal,
            Vector3 direction,
            float deliveredDamage,
            float normalizedPower,
            uint toolCapabilityMask)
        {
            if (!TrySnapshotNearestConsumableFloraWithLock(
                    hitPoint,
                    Mathf.Max(0.0001f, searchRadius),
                    out Vector3 floraPosition,
                    out _))
            {
                return false;
            }

            FloraInteractionManager interactionManager = floraInteractionManager;
            if (interactionManager != null)
            {
                interactionManager.TryApplyModuleParasiteCut(
                    floraPosition,
                    hitNormal,
                    direction,
                    deliveredDamage,
                    normalizedPower,
                    toolCapabilityMask);
            }

            return true;
        }

        internal bool TryApplyLightStarvation(uint instanceUid, float starvation01)
        {
            if (_dearLieJobScheduled ||
                !TrySnapshotActiveInstanceByUidWithLock(
                    instanceUid,
                    out bool underwater,
                    out int activeIndex,
                    out int templateIndex,
                    out HarvestableTemplate.MaterialClass materialClass,
                    out _,
                    out Vector3 instancePosition,
                    out _))
            {
                return false;
            }

            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            if (!TrySnapshotTemplateDescriptorWithLock(templateIndex, out HarvestableTemplate.RuntimeDescriptor starvationDescriptor))
                return false;

            float clampedStarvation01 = Mathf.Clamp01(starvation01);
            float baseHealth = Mathf.Max(0.1f, starvationDescriptor.BaseHealth);
            return ApplyLightStarvationState(
                underwater,
                activeIndex,
                instanceUid,
                materialClass,
                templateIndex,
                instancePosition,
                baseHealth,
                clampedStarvation01);
        }

        internal bool TryApplyAllelopathicToxinSuppression(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType,
            float toxicity01)
        {
            if (_dearLieJobScheduled ||
                !TryResolveFloraGrowthDescriptor(
                    matrix,
                    metadata,
                    typeId,
                    semanticType,
                    out uint instanceUid,
                out _,
                out _) ||
                instanceUid == 0u ||
                !TrySnapshotActiveInstanceByUidWithLock(
                    instanceUid,
                    out bool underwater,
                    out int activeIndex,
                    out int templateIndex,
                    out HarvestableTemplate.MaterialClass materialClass,
                    out _,
                    out Vector3 instancePosition,
                    out _))
            {
                return false;
            }

            if (materialClass == HarvestableTemplate.MaterialClass.None)
                return false;

            if (!TrySnapshotTemplateDescriptorWithLock(templateIndex, out HarvestableTemplate.RuntimeDescriptor toxinDescriptor))
                return false;

            float clampedToxicity01 = Mathf.Clamp01(toxicity01);
            if (clampedToxicity01 >= AllelopathicDeathThreshold01)
                return ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);

            float baseHealth = Mathf.Max(0.1f, toxinDescriptor.BaseHealth);
            float normalizedHealth = math.lerp(ResolveBareThreshold01(templateIndex), AllelopathicBareHealth01, clampedToxicity01);
            float normalizedHeightScale = Mathf.Clamp(
                Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                SoftBareHealthFloor01,
                1f);
            float nextHealth = baseHealth * normalizedHealth;
            return ApplySuppressionState(
                underwater,
                activeIndex,
                instanceUid,
                templateIndex,
                instancePosition,
                baseHealth,
                nextHealth,
                normalizedHeightScale);
        }

        internal static float EvaluateMaturationScaleMultiplier(float progress01)
        {
            float clampedProgress = math.saturate(progress01);
            float smoothProgress = clampedProgress * clampedProgress * (3f - (2f * clampedProgress));
            return math.lerp(0.1f, 1f, smoothProgress);
        }

        private bool ApplySuppressionState(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            int templateIndex,
            Vector3 instancePosition,
            float baseHealth,
            float currentHealth,
            float normalizedHeightScale)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            bool hasStateOverrideRequest = false;
            bool clearStateOverrideRequest = false;
            float stateOverrideNormalizedHealth = 0f;
            byte stateOverrideHarvestState = 0;
            try
            {
                if (_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid))
                    return false;

                if (!IsPinnedActiveLaneSlot(instanceUid, underwater, activeIndex))
                    return false;

                float pinnedCurrentHealth = GetLaneHealth(underwater, activeIndex);
                if (pinnedCurrentHealth <= 0.0001f)
                    return false;

                float appliedHealth = Mathf.Min(Mathf.Max(0f, currentHealth), pinnedCurrentHealth);
                if (appliedHealth >= pinnedCurrentHealth - 0.0001f)
                    return false;

                float appliedNormalizedHealth = Mathf.Clamp01(appliedHealth / Mathf.Max(0.0001f, baseHealth));
                float appliedNormalizedHeightScale = Mathf.Clamp(
                    Mathf.Min(appliedNormalizedHealth, Mathf.Clamp01(normalizedHeightScale)),
                    SoftBareHealthFloor01,
                    1f);

                SetLaneHealth(underwater, activeIndex, appliedHealth);
                if (_healthByInstanceUid.IsCreated)
                    _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)appliedHealth);

                float damage01 = ResolveDamageProgress(baseHealth, appliedHealth);
                UpdateDamageProgressCache(instanceUid, damage01);
                ApplyDamageToLaneInstance(
                    underwater,
                    activeIndex,
                    instanceUid,
                    templateIndex,
                    appliedNormalizedHealth,
                    damage01,
                    appliedNormalizedHeightScale,
                    ResolveOrganicClockSeconds());
                hasStateOverrideRequest = TryCacheFloraStateOverride(
                    instanceUid,
                    templateIndex,
                    underwater,
                    activeIndex,
                    baseHealth,
                    appliedHealth,
                    out stateOverrideNormalizedHealth,
                    out stateOverrideHarvestState,
                    out clearStateOverrideRequest);
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (hasStateOverrideRequest)
            {
                PublishFloraStateOverride(
                    instanceUid,
                    templateIndex,
                    instancePosition,
                    stateOverrideNormalizedHealth,
                    stateOverrideHarvestState,
                    clearStateOverrideRequest);
            }

            return true;
        }

        private bool ApplyLightStarvationState(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex,
            Vector3 instancePosition,
            float baseHealth,
            float clampedStarvation01)
        {
            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleMutationGuard(vault, out int lockedMask))
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            bool decomposeAfterUnlock = false;
            bool hasStateOverrideRequest = false;
            bool clearStateOverrideRequest = false;
            float stateOverrideNormalizedHealth = 0f;
            byte stateOverrideHarvestState = 0;
            try
            {
                if ((_destroyedByInstanceUid.IsCreated && _destroyedByInstanceUid.ContainsKey(instanceUid)) ||
                    (_regrowthProgressByInstanceUid.IsCreated && _regrowthProgressByInstanceUid.ContainsKey(instanceUid)))
                {
                    return false;
                }

                if (!IsPinnedActiveLaneSlot(instanceUid, underwater, activeIndex))
                    return false;

                float pinnedCurrentHealth = GetLaneHealth(underwater, activeIndex);
                if (pinnedCurrentHealth <= 0.0001f)
                    return false;

                float nextHealth = Mathf.Max(0f, pinnedCurrentHealth - (baseHealth * LightStarvationDamagePerSlowTick01 * clampedStarvation01));
                if (nextHealth <= baseHealth * LightStarvationDeathHealth01)
                {
                    decomposeAfterUnlock = true;
                }
                else
                {
                    float normalizedHealth = Mathf.Clamp01(nextHealth / Mathf.Max(0.0001f, baseHealth));
                    float normalizedHeightScale = Mathf.Clamp(
                        Mathf.Min(normalizedHealth, ResolveBareHeightCeiling01(templateIndex)),
                        SoftBareHealthFloor01,
                        1f);

                    SetLaneHealth(underwater, activeIndex, nextHealth);
                    if (_healthByInstanceUid.IsCreated)
                        _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)Mathf.Max(0f, nextHealth));

                    float damage01 = ResolveDamageProgress(baseHealth, nextHealth);
                    UpdateDamageProgressCache(instanceUid, damage01);
                    ApplyDamageToLaneInstance(
                        underwater,
                        activeIndex,
                        instanceUid,
                        templateIndex,
                        normalizedHealth,
                        damage01,
                        normalizedHeightScale,
                        ResolveOrganicClockSeconds());
                    hasStateOverrideRequest = TryCacheFloraStateOverride(
                        instanceUid,
                        templateIndex,
                        underwater,
                        activeIndex,
                        baseHealth,
                        nextHealth,
                        out stateOverrideNormalizedHealth,
                        out stateOverrideHarvestState,
                        out clearStateOverrideRequest);
                }
            }
            finally
            {
                ReleaseOrganicLifecycleMutationGuard(vault, lockedMask);
            }

            if (decomposeAfterUnlock)
                return ApplyPassiveDecomposition(underwater, activeIndex, instanceUid, materialClass, templateIndex, instancePosition);

            if (hasStateOverrideRequest)
            {
                PublishFloraStateOverride(
                    instanceUid,
                    templateIndex,
                    instancePosition,
                    stateOverrideNormalizedHealth,
                    stateOverrideHarvestState,
                    clearStateOverrideRequest);
            }

            return true;
        }

        private int FindTemplateDescriptorIndexByPersistentHashWithLock(ulong floraPersistentIdHash)
        {
            return TryFindTemplateDescriptorByPersistentHashWithLock(floraPersistentIdHash, out int descriptorIndex, out _)
                ? descriptorIndex
                : -1;
        }

        internal bool HasTemplatePersistentIdHash(ulong floraPersistentIdHash)
        {
            return FindTemplateDescriptorIndexByPersistentHashWithLock(floraPersistentIdHash) >= 0;
        }

        private static HarvestState ResolvePersistedHarvestState(byte packedHarvestState)
        {
            if (packedHarvestState > (byte)HarvestState.Dead)
                return HarvestState.PartiallyHarvested;

            return (HarvestState)packedHarvestState;
        }

        internal bool IsTemplateMaterialClass(ulong floraPersistentIdHash, HarvestableTemplate.MaterialClass materialClass)
        {
            return TryFindTemplateDescriptorByPersistentHashWithLock(floraPersistentIdHash, out _, out HarvestableTemplate.RuntimeDescriptor descriptor) &&
                   descriptor.MaterialClassId == (byte)materialClass;
        }

        internal bool TrySetRegrowthProgress(uint instanceUid, Vector3 runtimePosition, float progress01)
        {
            Matrix4x4 originalMatrix = default;
            return TrySetRegrowthProgress(instanceUid, runtimePosition, progress01, false, in originalMatrix, out _);
        }

        private bool TrySetRegrowthProgress(
            uint instanceUid,
            Vector3 runtimePosition,
            float progress01,
            bool restoreOriginalMatrix,
            in Matrix4x4 originalMatrix,
            out bool lockFailed)
        {
            lockFailed = false;
            if (_dearLieJobScheduled ||
                instanceUid == 0u ||
                !_regrowthProgressByInstanceUid.IsCreated ||
                !_regrowthPositionByInstanceUid.IsCreated ||
                (restoreOriginalMatrix && !IsFiniteMatrix(in originalMatrix)))
            {
                return false;
            }

            if (ChemicalInfluenceGrid.IsInsidePermanentDefoliantDeadZone(runtimePosition))
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicRegrowthMutationGuard(vault, out int lockedMask))
            {
                lockFailed = true;
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 32);
                return false;
            }

            bool clearRegistryStateOverride = false;
            bool clearRegistryDestroyedFlora = false;
            bool applyTitanRootMound = false;
            bool titanRootMoundWriteFailed = false;
            bool regrowthStateWriteFailed = false;
            Vector3 titanRootMoundPosition = default;
            try
            {
                progress01 = math.saturate(progress01);
                bool hadProgress = _regrowthProgressByInstanceUid.TryGetValue(instanceUid, out float previousProgress);
                bool hadPosition = _regrowthPositionByInstanceUid.TryGetValue(instanceUid, out float3 previousPosition);
                bool progressStored = _regrowthProgressByInstanceUid.TryPut(instanceUid, progress01);
                bool positionStored = _regrowthPositionByInstanceUid.TryPut(instanceUid, ToFloat3(runtimePosition));
                if (!progressStored || !positionStored)
                {
                    RestoreRegrowthState(instanceUid, hadProgress, previousProgress, hadPosition, previousPosition);
                    regrowthStateWriteFailed = true;
                }
                else
                {
                    bool hasActiveSlot = TryFindActiveInstanceByUidPinned(instanceUid, out bool underwater, out int activeIndex, out int templateIndex);
                    bool restoredOriginalMatrix = !restoreOriginalMatrix;
                    if (restoreOriginalMatrix && hasActiveSlot)
                    {
                        NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                        if (matrices.IsCreated && (uint)activeIndex < (uint)matrices.Length)
                        {
                            matrices[activeIndex] = originalMatrix;
                            restoredOriginalMatrix = true;
                        }
                    }

                    if (restoreOriginalMatrix && !restoredOriginalMatrix)
                    {
                        RestoreRegrowthState(instanceUid, hadProgress, previousProgress, hadPosition, previousPosition);
                        regrowthStateWriteFailed = true;
                    }
                    else
                    {
                        if (_destroyedByInstanceUid.IsCreated)
                            _destroyedByInstanceUid.Remove(instanceUid);
                        clearRegistryDestroyedFlora = true;
                        ClearDeadRuntimeFlag(instanceUid);
                        MarkOrganicTouched(instanceUid, ResolveOrganicClockSeconds());

                        if (_pendingWiltEndTimeByInstanceUid.IsCreated)
                            _pendingWiltEndTimeByInstanceUid.Remove(instanceUid);

                        if (_damageVisualProgressByInstanceUid.IsCreated)
                            _damageVisualProgressByInstanceUid.Remove(instanceUid);

                        if (_decompositionStartTimeByInstanceUid.IsCreated)
                            _decompositionStartTimeByInstanceUid.Remove(instanceUid);

                        if (hasActiveSlot)
                        {
                            byte runtimeFlags = 0;
                            if (_runtimeFlagsByInstanceUid.IsCreated)
                                _runtimeFlagsByInstanceUid.TryGetValue(instanceUid, out runtimeFlags);

                            ApplyRuntimeFlagsToLaneInstance(underwater, activeIndex, runtimeFlags);
                            ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, progress01);
                            float health = ResolveRegrowthHealth(progress01, templateIndex);
                            SetLaneHealth(underwater, activeIndex, health);
                            _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)health);
                            if (progress01 >= TitanRootMoundMatureThreshold01)
                                applyTitanRootMound = TryPrepareTitanRootMoundRequest(underwater, activeIndex, instanceUid, out titanRootMoundPosition, out titanRootMoundWriteFailed);
                        }

                        if (progress01 >= 0.9999f)
                            FinalizeRegrowth(instanceUid, out clearRegistryStateOverride);
                    }
                }
            }
            finally
            {
                ReleaseOrganicRegrowthMutationGuard(vault, lockedMask);
            }

            if (regrowthStateWriteFailed)
            {
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);
                return false;
            }

            if (clearRegistryDestroyedFlora || clearRegistryStateOverride)
            {
                PersistentWorldRegistry registry = _persistentWorldRegistry;
                if (registry != null)
                {
                    if (clearRegistryDestroyedFlora)
                        registry.TryClearDestroyedFlora(instanceUid);

                    if (clearRegistryStateOverride)
                        registry.TryClearFloraStateOverride(instanceUid);
                }
            }

            if (applyTitanRootMound)
                TryApplyPreparedTitanRootMound(instanceUid, titanRootMoundPosition);

            if (titanRootMoundWriteFailed)
                RecordDearLieTelemetry(Hecton8.Core.SystemDispatcher.CurrentFrameIndex, 0, 0, 0, 0, 1, 0, 0f, instanceUid, 128);

            return true;
        }

        private void RestoreRegrowthState(
            uint instanceUid,
            bool hadProgress,
            float previousProgress,
            bool hadPosition,
            float3 previousPosition)
        {
            if (_regrowthProgressByInstanceUid.IsCreated)
            {
                if (hadProgress)
                    _regrowthProgressByInstanceUid.TryPut(instanceUid, previousProgress);
                else
                    _regrowthProgressByInstanceUid.Remove(instanceUid);
            }

            if (_regrowthPositionByInstanceUid.IsCreated)
            {
                if (hadPosition)
                    _regrowthPositionByInstanceUid.TryPut(instanceUid, previousPosition);
                else
                    _regrowthPositionByInstanceUid.Remove(instanceUid);
            }
        }

        private void FinalizeRegrowth(uint instanceUid, out bool clearRegistryStateOverride)
        {
            clearRegistryStateOverride = false;
            if (_regrowthProgressByInstanceUid.IsCreated)
                _regrowthProgressByInstanceUid.Remove(instanceUid);

            if (_regrowthPositionByInstanceUid.IsCreated)
                _regrowthPositionByInstanceUid.Remove(instanceUid);

            if (_decompositionStartTimeByInstanceUid.IsCreated)
                _decompositionStartTimeByInstanceUid.Remove(instanceUid);
            MarkOrganicTouched(instanceUid, ResolveOrganicClockSeconds());

            clearRegistryStateOverride = true;
            ClearPersistedFloraStateOverride(instanceUid);

            if (TryFindActiveInstanceByUidPinned(instanceUid, out bool underwater, out int activeIndex, out int templateIndex))
            {
                float baseHealth = TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor regrowthDescriptor)
                    ? Mathf.Max(0.1f, regrowthDescriptor.BaseHealth)
                    : 1f;
                SetLaneHealth(underwater, activeIndex, baseHealth);
                _healthByInstanceUid.TryPut(instanceUid, (Unity.Mathematics.half)baseHealth);
                ApplyRegrowthVisualToLaneInstance(underwater, activeIndex, instanceUid, 1f);
                return;
            }

            _healthByInstanceUid.Remove(instanceUid);
        }

        private float ResolveRegrowthHealth(float progress01, int templateIndex)
        {
            float baseHealth = TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor regrowthDescriptor)
                ? Mathf.Max(0.1f, regrowthDescriptor.BaseHealth)
                : 1f;
            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            return Mathf.Max(0.05f, math.lerp(baseHealth * 0.1f, baseHealth, smoothProgress));
        }

        private float ResolveMaturationScaleMultiplier(uint instanceUid)
        {
            if (!_maturationScaleByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationScaleByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedScale))
            {
                return 1f;
            }

            return Mathf.Clamp((float)storedScale, 0.1f, 1f);
        }

        private float ResolveMaturationYieldMultiplier(uint instanceUid)
        {
            if (!_maturationYieldByInstanceUid.IsCreated ||
                instanceUid == 0u ||
                !_maturationYieldByInstanceUid.TryGetValue(instanceUid, out Unity.Mathematics.half storedYield))
            {
                return ResolveMaturationScaleMultiplier(instanceUid);
            }

            return Mathf.Clamp01((float)storedYield);
        }

        private void ApplyMaturationVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01, float scaleMultiplier)
        {
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            float clampedScale = Mathf.Clamp(scaleMultiplier, 0.1f, 1f);
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : UnitFloat2();
            HectonVegetationInstanceData maturationMetadata = metadata[activeIndex];
            maturationMetadata.Type = types[activeIndex];
            maturationMetadata.HeightScale = baseScale.x * clampedScale;
            maturationMetadata.WidthScale = baseScale.y * clampedScale;
            maturationMetadata.Reserved0 = EncodeAuthoredGrowthAge01(progress01);
            metadata[activeIndex] = maturationMetadata;
        }

        private void ApplyRegrowthVisualToLaneInstance(bool underwater, int activeIndex, uint instanceUid, float progress01)
        {
            NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            NativeArray<int> types = underwater ? _underwaterTypes : _surfaceTypes;
            if (!matrices.IsCreated ||
                !metadata.IsCreated ||
                !types.IsCreated ||
                activeIndex < 0 ||
                activeIndex >= matrices.Length ||
                activeIndex >= metadata.Length ||
                activeIndex >= types.Length)
            {
                return;
            }

            if (_regrowthPositionByInstanceUid.IsCreated &&
                _regrowthPositionByInstanceUid.TryGetValue(instanceUid, out float3 regrowthPosition))
            {
                Matrix4x4 visibleMatrix = matrices[activeIndex];
                visibleMatrix.m03 = regrowthPosition.x;
                visibleMatrix.m13 = regrowthPosition.y;
                visibleMatrix.m23 = regrowthPosition.z;
                matrices[activeIndex] = visibleMatrix;
            }

            float smoothProgress = progress01 * progress01 * (3f - (2f * progress01));
            float2 baseScale = _baseScaleByInstanceUid.IsCreated && _baseScaleByInstanceUid.TryGetValue(instanceUid, out float2 cachedBaseScale)
                ? cachedBaseScale
                : UnitFloat2();
            HectonVegetationInstanceData regrowthMetadata = metadata[activeIndex];
            regrowthMetadata.Type = types[activeIndex];
            regrowthMetadata.HeightScale = math.lerp(MinimumDecomposedHeightScale, baseScale.x, smoothProgress);
            regrowthMetadata.WidthScale = math.lerp(MinimumDecomposedWidthScale, baseScale.y, smoothProgress);
            regrowthMetadata.RuntimeState = progress01 >= 0.995f
                ? HectonVegetationInstanceData.RuntimeStateIdle
                : HectonVegetationInstanceData.RuntimeStateAgitated;
            regrowthMetadata.HealthNormalized = Mathf.Clamp01(progress01);
            regrowthMetadata.Reserved0 = EncodeAuthoredGrowthAge01(progress01);
            metadata[activeIndex] = regrowthMetadata;
        }

        private bool TrySnapshotActiveInstanceByUidWithLock(
            uint instanceUid,
            out bool underwater,
            out int activeIndex,
            out int templateIndex,
            out HarvestableTemplate.MaterialClass materialClass,
            out Matrix4x4 instanceMatrix,
            out Vector3 instancePosition,
            out float currentHealth)
        {
            underwater = false;
            activeIndex = -1;
            templateIndex = -1;
            materialClass = HarvestableTemplate.MaterialClass.None;
            instanceMatrix = Matrix4x4.identity;
            instancePosition = Vector3.zero;
            currentHealth = 0f;
            if (instanceUid == 0u)
                return false;

            IDataVault vault = _dearLieVault;
            if (!TryAcquireOrganicLifecycleReadGuard(vault, out int lockedMask))
                return false;

            try
            {
                if (!TryFindActiveInstanceByUidPinned(instanceUid, out underwater, out activeIndex, out templateIndex))
                    return false;

                NativeArray<Matrix4x4> matrices = underwater ? _underwaterMatrices : _surfaceMatrices;
                NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
                NativeArray<byte> materialClasses = underwater ? _underwaterMaterialClasses : _surfaceMaterialClasses;
                NativeArray<Unity.Mathematics.half> health = underwater ? _underwaterHealth : _surfaceHealth;
                int count = underwater ? _underwaterCount : _surfaceCount;
                if (!matrices.IsCreated ||
                    !instanceUids.IsCreated ||
                    !materialClasses.IsCreated ||
                    !health.IsCreated ||
                    activeIndex < 0 ||
                    activeIndex >= count ||
                    activeIndex >= matrices.Length ||
                    activeIndex >= instanceUids.Length ||
                    activeIndex >= materialClasses.Length ||
                    activeIndex >= health.Length ||
                    instanceUids[activeIndex] != instanceUid)
                {
                    return false;
                }

                materialClass = (HarvestableTemplate.MaterialClass)materialClasses[activeIndex];
                instanceMatrix = matrices[activeIndex];
                instancePosition = ExtractTranslation(instanceMatrix);
                currentHealth = (float)health[activeIndex];
                return true;
            }
            finally
            {
                ReleaseOrganicLifecycleReadGuard(vault, lockedMask);
            }
        }

        private bool TryFindActiveInstanceByUidPinned(uint instanceUid, out bool underwater, out int activeIndex, out int templateIndex)
        {
            if (TryFindActiveInstanceByUidPinned(instanceUid, _surfaceInstanceUids, _surfaceCount, _surfaceMaterialClasses, _surfaceMetadata, out activeIndex, out templateIndex))
            {
                underwater = false;
                return true;
            }

            if (TryFindActiveInstanceByUidPinned(instanceUid, _underwaterInstanceUids, _underwaterCount, _underwaterMaterialClasses, _underwaterMetadata, out activeIndex, out templateIndex))
            {
                underwater = true;
                return true;
            }

            underwater = false;
            activeIndex = -1;
            templateIndex = -1;
            return false;
        }

        private bool IsPinnedActiveLaneSlot(uint instanceUid, bool underwater, int activeIndex)
        {
            if (instanceUid == 0u || activeIndex < 0)
                return false;

            NativeArray<uint> instanceUids = underwater ? _underwaterInstanceUids : _surfaceInstanceUids;
            int count = underwater ? _underwaterCount : _surfaceCount;
            return instanceUids.IsCreated &&
                   activeIndex < count &&
                   activeIndex < instanceUids.Length &&
                   instanceUids[activeIndex] == instanceUid;
        }

        private bool TryFindActiveInstanceByUidPinned(
            uint instanceUid,
            NativeArray<uint> instanceUids,
            int count,
            NativeArray<byte> materialClasses,
            NativeArray<HectonVegetationInstanceData> metadata,
            out int activeIndex,
            out int templateIndex)
        {
            activeIndex = -1;
            templateIndex = -1;
            if (!instanceUids.IsCreated || !materialClasses.IsCreated || !metadata.IsCreated || count <= 0)
                return false;

            int safeCount = math.min(count, math.min(instanceUids.Length, math.min(materialClasses.Length, metadata.Length)));
            for (int i = 0; i < safeCount; i++)
            {
                if (instanceUids[i] != instanceUid)
                    continue;

                activeIndex = i;
                templateIndex = ResolveTemplateIndex(metadata[i], (HarvestableTemplate.MaterialClass)materialClasses[i]);
                return true;
            }

            return false;
        }

        private float ComputePinnedParentMassKg(
            bool underwater,
            int activeIndex,
            uint instanceUid,
            HarvestableTemplate.MaterialClass materialClass,
            int templateIndex)
        {
            float baseHealth = TryCopyPinnedTemplateDescriptor(templateIndex, out HarvestableTemplate.RuntimeDescriptor massDescriptor)
                ? Mathf.Max(0.1f, massDescriptor.BaseHealth)
                : 1f;
            float height01 = 1f;
            float width01 = 1f;
            float metadataAge01 = 1f;
            NativeArray<HectonVegetationInstanceData> metadata = underwater ? _underwaterMetadata : _surfaceMetadata;
            if (metadata.IsCreated && activeIndex >= 0 && activeIndex < metadata.Length)
            {
                HectonVegetationInstanceData instanceData = metadata[activeIndex];
                height01 = Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale));
                width01 = Mathf.Clamp01(instanceData.WidthScale);
                metadataAge01 = ResolveHarvestAge01(instanceData);
            }

            float maturationMultiplier = ResolveMaturationYieldMultiplier(instanceUid);

            float resolvedMassKg = materialClass switch
            {
                HarvestableTemplate.MaterialClass.Kelp => Mathf.Max(1f, baseHealth * math.lerp(0.28f, 0.52f, height01) * math.lerp(0.9f, 1.15f, width01)),
                HarvestableTemplate.MaterialClass.Coral => Mathf.Max(2f, baseHealth * math.lerp(0.55f, 0.8f, height01)),
                HarvestableTemplate.MaterialClass.TitaniumOutcrop => Mathf.Max(4f, baseHealth * math.lerp(0.82f, 1.08f, height01)),
                HarvestableTemplate.MaterialClass.Sargassum => Mathf.Max(0.75f, baseHealth * math.lerp(0.22f, 0.38f, height01) * math.lerp(0.85f, 1.1f, width01)),
                _ => Mathf.Max(1f, baseHealth * 0.4f)
            };

            float harvestAge01 = metadataAge01 < 0.999f ? metadataAge01 : maturationMultiplier;
            if (harvestAge01 < 0.2f)
                return 0f;

            return resolvedMassKg * harvestAge01;
        }

        private static float ResolveHarvestAge01(in HectonVegetationInstanceData instanceData)
        {
            if (instanceData.Reserved0 < 0f)
                return -1f;

            if (instanceData.Reserved0 > 0.0001f)
                return Mathf.Clamp01(instanceData.Reserved0);

            return 1f;
        }

        private static float EncodeAuthoredGrowthAge01(float progress01)
        {
            float clampedProgress = Mathf.Clamp01(progress01);
            return clampedProgress <= 0.0001f ? 0.0002f : clampedProgress;
        }

        private static HarvestableTemplate.MaterialClass ResolveMaterialClass(int typeId, int semanticType)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            HectonMapMagicVegetationBridge.VegetationSemanticType semantic = (HectonMapMagicVegetationBridge.VegetationSemanticType)semanticType;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.OrganicKelp)
                return HarvestableTemplate.MaterialClass.Kelp;

            if (vegetationType == HectonVegetationInstanceType.Sargassum || semantic == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum)
                return HarvestableTemplate.MaterialClass.Sargassum;

            return HarvestableTemplate.MaterialClass.None;
        }

        private static bool IsConsumableFloraMaterialClass(HarvestableTemplate.MaterialClass materialClass)
        {
            return materialClass == HarvestableTemplate.MaterialClass.Kelp ||
                   materialClass == HarvestableTemplate.MaterialClass.Sargassum;
        }

        private static double ResolveConstructionDistanceSq(
            double3 centerUniversePosition,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId)
        {
            if (!IsFiniteVector(rootPosition))
                return double.PositiveInfinity;

            double3 rootPositionDouble = HectonMapMagicVegetationBridge.ToUniverseSpaceDouble3(rootPosition);
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                double kelpHeight = math.lerp(10d, 20d, (double)math.saturate(metadata.HeightScale));
                double3 topOffset = default;
                topOffset.y = math.max(0.5d, kelpHeight + KelpRadiusBias);
                double3 top = rootPositionDouble + topOffset;
                double3 closest = ClosestPointOnSegment(rootPositionDouble, top, centerUniversePosition);
                return math.lengthsq(closest - centerUniversePosition);
            }

            return math.lengthsq(rootPositionDouble - centerUniversePosition);
        }

        private static float ResolveHarvestDistanceSq(
            Vector3 hitPoint,
            Vector3 rootPosition,
            HectonVegetationInstanceData metadata,
            int typeId,
            float fallbackDistanceSq,
            float heightTolerance)
        {
            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)typeId;
            if (vegetationType == HectonVegetationInstanceType.GiantKelp)
            {
                float kelpHeight = math.lerp(10f, 20f, math.saturate(metadata.HeightScale));
                Vector3 top = rootPosition + Vector3.up * Mathf.Max(0.5f, kelpHeight + KelpRadiusBias);
                Vector3 closest = ClosestPointOnSegment(rootPosition, top, hitPoint);
                return (closest - hitPoint).sqrMagnitude;
            }

            return Mathf.Min((rootPosition - hitPoint).sqrMagnitude, fallbackDistanceSq + heightTolerance);
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float segmentLengthSq = segment.sqrMagnitude;
            if (segmentLengthSq <= 0.0001f)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSq);
            return start + segment * t;
        }

        private static double3 ClosestPointOnSegment(double3 start, double3 end, double3 point)
        {
            double3 segment = end - start;
            double segmentLengthSq = math.lengthsq(segment);
            if (segmentLengthSq <= 0.0001d)
                return start;

            double t = math.clamp(math.dot(point - start, segment) * math.rcp(segmentLengthSq), 0d, 1d);
            return start + segment * t;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return ToRuntimeVector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private static Vector3 ToRuntimeVector3(float3 value)
        {
            return ToRuntimeVector3(value.x, value.y, value.z);
        }

        private static Vector3 ToRuntimeVector3(float x, float y, float z)
        {
            Vector3 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            return result;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFiniteMatrix(in Matrix4x4 matrix)
        {
            return math.isfinite(matrix.m00) &&
                   math.isfinite(matrix.m01) &&
                   math.isfinite(matrix.m02) &&
                   math.isfinite(matrix.m03) &&
                   math.isfinite(matrix.m10) &&
                   math.isfinite(matrix.m11) &&
                   math.isfinite(matrix.m12) &&
                   math.isfinite(matrix.m13) &&
                   math.isfinite(matrix.m20) &&
                   math.isfinite(matrix.m21) &&
                   math.isfinite(matrix.m22) &&
                   math.isfinite(matrix.m23) &&
                   math.isfinite(matrix.m30) &&
                   math.isfinite(matrix.m31) &&
                   math.isfinite(matrix.m32) &&
                   math.isfinite(matrix.m33);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                global::Hecton8.World.AUPMath.ToDouble3(runtimePosition));
            return IsFiniteAup(in positionAup);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            double3 result = default;
            result.x = value.x;
            result.y = value.y;
            result.z = value.z;
            return result;
        }

        private static float ResolveFractionalVariation(float encodedVariation)
        {
            return Mathf.Repeat(encodedVariation, 1f);
        }

        internal static uint ComputeStableInstanceUid(
            Matrix4x4 matrix,
            HectonVegetationInstanceData metadata,
            int typeId,
            int semanticType)
        {
            int x = Mathf.RoundToInt(matrix.m03 * 100f);
            int y = Mathf.RoundToInt(matrix.m13 * 100f);
            int z = Mathf.RoundToInt(matrix.m23 * 100f);
            uint hx = (uint)x * 73856093u;
            uint hy = (uint)y * 19349663u;
            uint hz = (uint)z * 83492791u;
            uint hv = (uint)Mathf.RoundToInt(ResolveFractionalVariation(metadata.Variation) * 10000f) * 2654435761u;
            uint hs = (uint)(semanticType + 1) * 2246822519u;
            uint ht = (uint)(typeId + 1) * 3266489917u;
            uint mixed = hx ^ hy ^ hz ^ hv ^ hs ^ ht;
            return mixed == 0u ? 1u : mixed;
        }

        private NativeArray<T> EnsureLaneCapacity<T>(ref VaultArray<T> array, BufferID bufferId, int requiredCount, string label) where T : struct
        {
            EnsureVaultArrayCapacity(ref array, bufferId, requiredCount, label, NativeArrayOptions.UninitializedMemory);
            return array;
        }

        private bool EnsureVaultArrayCapacity<T>(
            ref VaultArray<T> array,
            BufferID bufferId,
            int requiredCount,
            string label,
            NativeArrayOptions options) where T : struct
        {
            if (requiredCount <= 0)
                return array.IsCreated;

            IDataVault vault = _dearLieVault;
            return vault != null && array.Ensure(vault, bufferId, requiredCount, OrganicVaultSystemId, options);
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly DestructibleOrganicManager _owner;

            public PostSimulationPhaseSystem(DestructibleOrganicManager owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => DearLiePostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.95f, 0.55f, 0.42f);
            Gizmos.DrawWireSphere(dearLieMockDamageCenter, ResolveDearLieQueryRadius());
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireCube(dearLieMockDamageCenter, Vector3.one * DearLieSpatialCellSizeMeters);

            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int sampleCount = math.min(signals.Length, DearLieMaxDamageSignalsPerFrame);
            for (int i = 0; i < sampleCount; i++)
            {
                if ((signals[i].Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                int nanRejectCount = 0;
                if (!TryBuildDearLieEvent(in signals[i], out FloraDestructionEventDTO eventDto, ref nanRejectCount))
                    continue;

                AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(eventDto.ImpactAUP);
                Vector3 impactRuntime = (Vector3)AUPMath.ToRuntimeFloat3(in impactAup, HectonFloatingOrigin.CurrentTotalOffsetDouble);
                if (!IsFiniteVector(impactRuntime))
                    continue;

                Gizmos.color = new Color(1f, 0.08f, 0.02f, 0.72f);
                Gizmos.DrawWireSphere(impactRuntime, ResolveDearLieQueryRadius());
                break;
            }

            if (_dearLieHasLastDebugHit != 0 &&
                IsFiniteVector(_dearLieLastImpactRuntimePosition) &&
                IsFiniteVector(_dearLieLastTargetRuntimePosition))
            {
                Gizmos.color = new Color(1f, 0.95f, 0.1f, 0.8f);
                Gizmos.DrawLine(_dearLieLastImpactRuntimePosition, _dearLieLastTargetRuntimePosition);
                Gizmos.DrawWireSphere(_dearLieLastTargetRuntimePosition, 0.25f);
            }
        }
#endif

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            return magnitudeSq > 0.0001f ? vector * math.rsqrt(magnitudeSq) : fallback;
        }
    }
}
