using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Memory;
using Hecton8.Core.Memory.Layout;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    public static class ConstructionSocketFlags
    {
        public const uint None = 0u;
        public const uint Connected = 1u << 0;
        public const uint CorridorRoom = 1u << 1;
        public const uint Hatch = 1u << 2;
        public const uint CollisionBlocked = 1u << 3;
        public const uint NonFinite = 1u << 4;
        public const uint ValidSnap = 1u << 5;
        public const uint PendingCommit = 1u << 6;
        public const uint TopologyDirty = 1u << 7;
        public const uint RollbackFence = 1u << 8;
        public const uint DearLieActive = 1u << 9;
        public const uint CapacityExceeded = 1u << 10;
    }

    public static class BuilderGhostValidationFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint Valid = 1u << 1;
        public const uint GridSnapped = 1u << 2;
        public const uint SdfBlocked = 1u << 3;
        public const uint BoundsBlocked = 1u << 4;
        public const uint NonFinite = 1u << 5;
        public const uint SocketSnap = 1u << 6;
        public const uint PresentationOnly = 1u << 7;
        public const uint DearLieActive = 1u << 8;
        public const uint RollbackExcluded = 1u << 9;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct BuilderGhostStateDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
        [FieldOffset(64)] public double3 AUP_TargetPosition;
        [FieldOffset(88)] public uint PrefabHashID;
        [FieldOffset(92)] public uint ValidationFlags;
        [FieldOffset(96)] public float AnimationPhase;
        [FieldOffset(100)] public uint ValidationStateHash;
        [FieldOffset(104)] private byte _pad0;
        [FieldOffset(105)] private byte _pad1;
        [FieldOffset(106)] private byte _pad2;
        [FieldOffset(107)] private byte _pad3;
        [FieldOffset(108)] private byte _pad4;
        [FieldOffset(109)] private byte _pad5;
        [FieldOffset(110)] private byte _pad6;
        [FieldOffset(111)] private byte _pad7;
        [FieldOffset(112)] private byte _pad8;
        [FieldOffset(113)] private byte _pad9;
        [FieldOffset(114)] private byte _pad10;
        [FieldOffset(115)] private byte _pad11;
        [FieldOffset(116)] private byte _pad12;
        [FieldOffset(117)] private byte _pad13;
        [FieldOffset(118)] private byte _pad14;
        [FieldOffset(119)] private byte _pad15;
        [FieldOffset(120)] private byte _pad16;
        [FieldOffset(121)] private byte _pad17;
        [FieldOffset(122)] private byte _pad18;
        [FieldOffset(123)] private byte _pad19;
        [FieldOffset(124)] private byte _pad20;
        [FieldOffset(125)] private byte _pad21;
        [FieldOffset(126)] private byte _pad22;
        [FieldOffset(127)] private byte _pad23;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BuilderGhostVisualDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float DearLieDampen;
        [FieldOffset(8)] public float DearLieWiggleSpeed;
        [FieldOffset(12)] public float Alpha;
        [FieldOffset(16)] public float4 ValidColor;
        [FieldOffset(32)] public float4 InvalidColor;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HolographyTelemetryEntry
    {
        [FieldOffset(0)] public double3 AUP_TargetPosition;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint PrefabHashID;
        [FieldOffset(32)] public uint SdfCornerChecks;
        [FieldOffset(36)] public uint ValidationFlags;
        [FieldOffset(40)] public float SolverMicroseconds;
        [FieldOffset(44)] public float MinSdfDistance;
        [FieldOffset(48)] public uint ValidationStateHash;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] private byte _pad0;
        [FieldOffset(57)] private byte _pad1;
        [FieldOffset(58)] private byte _pad2;
        [FieldOffset(59)] private byte _pad3;
        [FieldOffset(60)] private byte _pad4;
        [FieldOffset(61)] private byte _pad5;
        [FieldOffset(62)] private byte _pad6;
        [FieldOffset(63)] private byte _pad7;
    }

    [BinaryBlittableSafe]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct BuilderGhostIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SocketStateDTO
    {
        [FieldOffset(0)] public double3 LocalOffset;
        [FieldOffset(24)] public float3 NormalDirection;
        [FieldOffset(36)] public uint AllowedConnectionBitmask;
        [FieldOffset(40)] public uint ParentModuleHash;
        [FieldOffset(44)] public uint ConnectionStatus;
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

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct GhostPreviewDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public float3 BoundsScale;
        [FieldOffset(52)] public float SnappingRadius;
        [FieldOffset(56)] public uint ModuleHash;
        [FieldOffset(60)] public int SocketStart;
        [FieldOffset(64)] public int SocketCount;
        [FieldOffset(68)] public float DearLieDampen;
        [FieldOffset(72)] public float GlobalQualityWeight;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public float3 BoundsCenter;
        [FieldOffset(92)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct ConstructionSocketModuleDTO
    {
        [FieldOffset(0)] public double3 RootAup;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public float3 BoundsCenter;
        [FieldOffset(52)] public float3 BoundsExtents;
        [FieldOffset(64)] public uint ModuleHash;
        [FieldOffset(68)] public int SocketStart;
        [FieldOffset(72)] public int SocketCount;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public uint TopologyVersion;
        [FieldOffset(84)] public float DearLieDampen;
        [FieldOffset(88)] public uint ConnectedMask;
        [FieldOffset(92)] public int SceneModuleListIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SocketSnappingResultDTO
    {
        [FieldOffset(0)] public float4x4 SnappingMatrix;
        [FieldOffset(64)] public double3 SnappedRootAup;
        [FieldOffset(88)] public int TargetSocketIndex;
        [FieldOffset(92)] public int GhostSocketIndex;
        [FieldOffset(96)] public float DistanceSq;
        [FieldOffset(100)] public float AlignmentDot;
        [FieldOffset(104)] public uint Flags;
        [FieldOffset(108)] public uint TargetModuleHash;
        [FieldOffset(112)] public uint GhostModuleHash;
        [FieldOffset(116)] public float DearLieDampen;
        [FieldOffset(120)] public uint ResultHash;
        [FieldOffset(124)] public uint EvaluatedCandidates;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SocketConnectionPairDTO
    {
        [FieldOffset(0)] public int TargetSocketIndex;
        [FieldOffset(4)] public int GhostSocketIndex;
        [FieldOffset(8)] public uint TargetModuleHash;
        [FieldOffset(12)] public uint GhostModuleHash;
        [FieldOffset(16)] public uint ConnectionKind;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint ResultHash;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SocketModuleBoundsDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float3 Extents;
        [FieldOffset(36)] public uint ModuleHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public int SdfSampleStart;
        [FieldOffset(48)] public int SdfSampleCount;
        [FieldOffset(52)] public float ClearanceMeters;
        [FieldOffset(56)] public uint ResultHash;
        [FieldOffset(60)] private byte _pad0;
        [FieldOffset(61)] private byte _pad1;
        [FieldOffset(62)] private byte _pad2;
        [FieldOffset(63)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SocketBoundsResultDTO
    {
        [FieldOffset(0)] public uint FailureFlags;
        [FieldOffset(4)] public float MinSeparationMeters;
        [FieldOffset(8)] public int HitModuleIndex;
        [FieldOffset(12)] public int SdfHitIndex;
        [FieldOffset(16)] public uint ResultHash;
        [FieldOffset(20)] public uint EvaluatedBounds;
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
    public struct ConstructionSocketTuningDTO
    {
        [FieldOffset(0)] public float SnappingRadius;
        [FieldOffset(4)] public float UnsnapRadius;
        [FieldOffset(8)] public float AlignmentDotThreshold;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float SearchRadiusLowMeters;
        [FieldOffset(20)] public float SearchRadiusUltraMeters;
        [FieldOffset(24)] public int MinCandidateBudget;
        [FieldOffset(28)] public int MaxCandidateBudget;
        [FieldOffset(32)] public float DearLieShrinkMeters;
        [FieldOffset(36)] public float DearLieWiggleSpeed;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float MagnetForce;
        [FieldOffset(52)] private byte _pad0;
        [FieldOffset(53)] private byte _pad1;
        [FieldOffset(54)] private byte _pad2;
        [FieldOffset(55)] private byte _pad3;
        [FieldOffset(56)] private byte _pad4;
        [FieldOffset(57)] private byte _pad5;
        [FieldOffset(58)] private byte _pad6;
        [FieldOffset(59)] private byte _pad7;
        [FieldOffset(60)] private byte _pad8;
        [FieldOffset(61)] private byte _pad9;
        [FieldOffset(62)] private byte _pad10;
        [FieldOffset(63)] private byte _pad11;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ConstructionSocketTelemetryEntry
    {
        [FieldOffset(0)] public double3 PreviewAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint ActiveSocketCount;
        [FieldOffset(32)] public uint EvaluatedCandidateCount;
        [FieldOffset(36)] public uint AcceptedSnapCount;
        [FieldOffset(40)] public float SolverMicroseconds;
        [FieldOffset(44)] public float BestDistanceSq;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint ResultHash;
        [FieldOffset(56)] public float GlobalQualityWeight;
        [FieldOffset(60)] public uint TopologyVersion;
    }

    public ref struct ConstructionSocketVaultViews
    {
        public NativeArray<SocketStateDTO> SocketStates;
        public NativeArray<double3> SocketAups;
        public NativeArray<SocketStateDTO> GhostSocketStates;
        public NativeArray<double3> GhostSocketAups;
        public NativeArray<GhostPreviewDTO> GhostPreviews;
        public NativeArray<int2> SocketCsrRanges;
        public NativeArray<int> SocketCsrTargetIndices;
        public NativeArray<SocketSnappingResultDTO> SnapResults;
        public NativeArray<ConstructionSocketTelemetryEntry> Telemetry;
        public NativeArray<ConstructionSocketTuningDTO> Tuning;
        public NativeArray<ConstructionSocketModuleDTO> Modules;
        public NativeArray<int> Counters;
        public NativeArray<SocketModuleBoundsDTO> Bounds;
        public NativeArray<SocketConnectionPairDTO> Connections;
        public NativeArray<BuilderGhostStateDTO> BuilderGhostStates;
        public NativeArray<BuilderGhostVisualDTO> BuilderGhostVisuals;
        public NativeArray<HolographyTelemetryEntry> HolographyTelemetry;
        public NativeArray<byte> BuilderGhostSdfSamples;
        public NativeArray<BuilderGhostIndirectArgsDTO> BuilderGhostIndirectArgs;
    }

    public static unsafe class ShinobuSocketConstructionRuntime
    {
        public const int TelemetryCapacity = 300;
        public const int MockModuleCount = 500;
        public const int MockSocketsPerModule = 6;
        public const int MockSocketCount = MockModuleCount * MockSocketsPerModule;
        public const int GhostSocketCapacity = 64;
        public const int SnapResultCapacity = GhostSocketCapacity + 1;
        public const int SocketDirectionCount = 6;
        public const int SocketCsrRangeCapacity = GhostSocketCapacity + SocketDirectionCount;
        public const int SocketCsrTargetIndexCapacity = MockSocketCount;
        public const int BuilderGhostStateCapacity = 128;
        public const int BuilderGhostVisualCapacity = 128;
        public const int BuilderGhostSdfCornerCount = 8;
        public const int BuilderGhostSdfSampleCapacity = BuilderGhostStateCapacity * BuilderGhostSdfCornerCount;
        public const int BuilderGhostMockValidationCount = 10000;
        public const int BuilderGhostProceduralVertexCount = 36;
        public const BufferID GhostPreviewBufferId = (BufferID)70370;
        public const BufferID SocketCsrRangesBufferId = (BufferID)70371;
        public const BufferID SocketCsrTargetIndicesBufferId = (BufferID)70372;
        public const BufferID BuilderGhostStateBufferId = (BufferID)70940;
        public const BufferID BuilderGhostVisualBufferId = (BufferID)70941;
        public const BufferID BuilderGhostTelemetryBufferId = (BufferID)70942;
        public const BufferID BuilderGhostMockStateBufferId = (BufferID)70943;
        public const BufferID BuilderGhostSdfSamplesBufferId = (BufferID)70944;
        public const BufferID BuilderGhostIndirectArgsBufferId = (BufferID)70945;
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_1306_Construction_SocketTelemetry.bin";
        public const string HolographyDumpPath = "Docs/AgentLogs/Dump_1306_Construction_Holography.bin";

        public const int SocketStateSizeBytes = 64;
        public const int GhostPreviewSizeBytes = 96;
        public const int ConstructionSocketModuleSizeBytes = 96;
        public const int SocketSnappingResultSizeBytes = 128;
        public const int SocketConnectionPairSizeBytes = 32;
        public const int SocketModuleBoundsSizeBytes = 64;
        public const int SocketBoundsResultSizeBytes = 32;
        public const int SocketTuningSizeBytes = 64;
        public const int SocketTelemetrySizeBytes = 64;
        public const int BuilderGhostStateSizeBytes = 128;
        public const int BuilderGhostVisualSizeBytes = 64;
        public const int HolographyTelemetrySizeBytes = 64;
        public const int BuilderGhostIndirectArgsSizeBytes = 16;

        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint UniversalCompatibilityHash24 = 0u;
        private const int CounterMagicIndex = 7;
        private const int CounterMagic = 0x534B5431;
        private static ConstructionSocketTuningDTO s_Tuning = CreateDefaultTuning(1f);
        private static IDataVault s_BoundVault;
        private static bool s_TelemetryDumped;
        private static VaultGenerationHandle<SocketStateDTO> s_SocketStatesHandle;
        private static VaultGenerationHandle<double3> s_SocketAupHandle;
        private static VaultGenerationHandle<SocketStateDTO> s_GhostSocketStatesHandle;
        private static VaultGenerationHandle<double3> s_GhostSocketAupHandle;
        private static VaultGenerationHandle<GhostPreviewDTO> s_GhostPreviewHandle;
        private static VaultGenerationHandle<int2> s_SocketCsrRangesHandle;
        private static VaultGenerationHandle<int> s_SocketCsrTargetIndicesHandle;
        private static VaultGenerationHandle<SocketSnappingResultDTO> s_SnapResultsHandle;
        private static VaultGenerationHandle<ConstructionSocketTelemetryEntry> s_TelemetryHandle;
        private static VaultGenerationHandle<ConstructionSocketTuningDTO> s_TuningHandle;
        private static VaultGenerationHandle<ConstructionSocketModuleDTO> s_ModuleHandle;
        private static VaultGenerationHandle<int> s_CountersHandle;
        private static VaultGenerationHandle<SocketModuleBoundsDTO> s_BoundsHandle;
        private static VaultGenerationHandle<SocketConnectionPairDTO> s_ConnectionsHandle;
        private static VaultGenerationHandle<BuilderGhostStateDTO> s_BuilderGhostStateHandle;
        private static VaultGenerationHandle<BuilderGhostVisualDTO> s_BuilderGhostVisualHandle;
        private static VaultGenerationHandle<HolographyTelemetryEntry> s_HolographyTelemetryHandle;
        private static VaultGenerationHandle<BuilderGhostStateDTO> s_BuilderGhostMockStateHandle;
        private static VaultGenerationHandle<byte> s_BuilderGhostSdfSamplesHandle;
        private static VaultGenerationHandle<BuilderGhostIndirectArgsDTO> s_BuilderGhostIndirectArgsHandle;
        private static bool s_HolographyTelemetryDumped;
        private static int s_ModuleReadFenceDepth;
        private static int s_ModuleWriteFence;
        private static ConstructionSocketModuleDTO[] s_MockModules;
        private static SocketStateDTO[] s_MockSockets;
        private static double3[] s_MockSocketAups;
        private static int[] s_MockCounters;
        private static int2[] s_MockCsrRanges;
        private static int[] s_MockCsrTargetIndices;

        public static bool ValidateStructLayout()
        {
            if (UnsafeUtility.SizeOf<SocketStateDTO>() != SocketStateSizeBytes ||
                UnsafeUtility.SizeOf<GhostPreviewDTO>() != GhostPreviewSizeBytes ||
                UnsafeUtility.SizeOf<ConstructionSocketModuleDTO>() != ConstructionSocketModuleSizeBytes ||
                UnsafeUtility.SizeOf<SocketSnappingResultDTO>() != SocketSnappingResultSizeBytes ||
                UnsafeUtility.SizeOf<SocketConnectionPairDTO>() != SocketConnectionPairSizeBytes ||
                UnsafeUtility.SizeOf<SocketModuleBoundsDTO>() != SocketModuleBoundsSizeBytes ||
                UnsafeUtility.SizeOf<SocketBoundsResultDTO>() != SocketBoundsResultSizeBytes ||
                UnsafeUtility.SizeOf<ConstructionSocketTuningDTO>() != SocketTuningSizeBytes ||
                UnsafeUtility.SizeOf<ConstructionSocketTelemetryEntry>() != SocketTelemetrySizeBytes ||
                UnsafeUtility.SizeOf<BuilderGhostStateDTO>() != BuilderGhostStateSizeBytes ||
                UnsafeUtility.SizeOf<BuilderGhostVisualDTO>() != BuilderGhostVisualSizeBytes ||
                UnsafeUtility.SizeOf<HolographyTelemetryEntry>() != HolographyTelemetrySizeBytes ||
                UnsafeUtility.SizeOf<BuilderGhostIndirectArgsDTO>() != BuilderGhostIndirectArgsSizeBytes)
            {
                return false;
            }

#if UNITY_EDITOR
            return ResolveOffset<SocketStateDTO>(nameof(SocketStateDTO.LocalOffset)) == 0 &&
                   ResolveOffset<SocketStateDTO>(nameof(SocketStateDTO.NormalDirection)) == 24 &&
                   ResolveOffset<SocketStateDTO>(nameof(SocketStateDTO.AllowedConnectionBitmask)) == 36 &&
                   ResolveOffset<SocketStateDTO>(nameof(SocketStateDTO.ParentModuleHash)) == 40 &&
                   ResolveOffset<SocketStateDTO>(nameof(SocketStateDTO.ConnectionStatus)) == 44 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.LocalToWorld)) == 0 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition)) == 64 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.PrefabHashID)) == 88 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.ValidationFlags)) == 92 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AnimationPhase)) == 96 &&
                   ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.ValidationStateHash)) == 100;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        public static int ResolveOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref SocketStateDTO SocketRef(NativeArray<SocketStateDTO> sockets, int index)
        {
            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(sockets);
            return ref UnsafeUtility.AsRef<SocketStateDTO>((void*)((byte*)ptr + (index * SocketStateSizeBytes)));
        }

        public static ConstructionSocketTuningDTO CreateDefaultTuning(float globalQualityWeight)
        {
            ConstructionSocketTuningDTO tuning = default;
            tuning.SnappingRadius = 1f;
            tuning.UnsnapRadius = 1.25f;
            tuning.AlignmentDotThreshold = 0.82f;
            tuning.GlobalQualityWeight = SanitizeQuality(globalQualityWeight);
            tuning.SearchRadiusLowMeters = 5f;
            tuning.SearchRadiusUltraMeters = 18f;
            tuning.MinCandidateBudget = 16;
            tuning.MaxCandidateBudget = 256;
            tuning.DearLieShrinkMeters = 0.08f;
            tuning.DearLieWiggleSpeed = 18f;
            tuning.Frame = 0u;
            tuning.Flags = 0u;
            tuning.MagnetForce = 1f;
            return tuning;
        }

        public static ConstructionSocketTuningDTO GetTuning()
        {
            return s_Tuning;
        }

        public static void SetTuning(float snapRadius, float unsnapRadius, float alignmentDot, float searchLow, float searchUltra, float dearLieShrink, float dearLieWiggle, float magnetForce = 1f)
        {
            s_Tuning.SnappingRadius = SanitizePositive(snapRadius, 1f);
            s_Tuning.UnsnapRadius = math.max(SanitizePositive(unsnapRadius, 1.25f), s_Tuning.SnappingRadius + 0.01f);
            s_Tuning.AlignmentDotThreshold = math.clamp(math.isfinite(alignmentDot) ? alignmentDot : 0.82f, -1f, 1f);
            s_Tuning.SearchRadiusLowMeters = SanitizePositive(searchLow, 5f);
            s_Tuning.SearchRadiusUltraMeters = math.max(SanitizePositive(searchUltra, 18f), s_Tuning.SearchRadiusLowMeters);
            s_Tuning.DearLieShrinkMeters = math.clamp(math.isfinite(dearLieShrink) ? dearLieShrink : 0.08f, 0f, 1f);
            s_Tuning.DearLieWiggleSpeed = math.clamp(math.isfinite(dearLieWiggle) ? dearLieWiggle : 18f, 0f, 90f);
            s_Tuning.MagnetForce = math.clamp(math.isfinite(magnetForce) ? magnetForce : 1f, 0f, 4f);
        }

        public static bool InitializeVault(IDataVault vault)
        {
            if (vault == null)
                return false;

            ResetVaultDescriptorsIfOwnerChanged(vault);
            bool resetCounters = ShouldResetCounterLane(vault);
            s_Tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            s_SocketStatesHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketStates, MockSocketCount, ref s_SocketStatesHandle);
            s_SocketAupHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketAup, MockSocketCount, ref s_SocketAupHandle);
            s_GhostSocketStatesHandle = EnsureVaultHandle(vault, BufferID.ConstructionGhostSocketStates, GhostSocketCapacity, ref s_GhostSocketStatesHandle);
            s_GhostSocketAupHandle = EnsureVaultHandle(vault, BufferID.ConstructionGhostSocketAup, GhostSocketCapacity, ref s_GhostSocketAupHandle);
            s_GhostPreviewHandle = EnsureVaultHandle(vault, GhostPreviewBufferId, 1, ref s_GhostPreviewHandle);
            s_SocketCsrRangesHandle = EnsureVaultHandle(vault, SocketCsrRangesBufferId, SocketCsrRangeCapacity, ref s_SocketCsrRangesHandle);
            s_SocketCsrTargetIndicesHandle = EnsureVaultHandle(vault, SocketCsrTargetIndicesBufferId, SocketCsrTargetIndexCapacity, ref s_SocketCsrTargetIndicesHandle);
            s_SnapResultsHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketSnapResults, SnapResultCapacity, ref s_SnapResultsHandle);
            s_TelemetryHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketTelemetry, TelemetryCapacity, ref s_TelemetryHandle);
            s_TuningHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketTuning, 1, ref s_TuningHandle);
            s_ModuleHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketModules, MockModuleCount, ref s_ModuleHandle);
            s_CountersHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketCounters, 8, ref s_CountersHandle);
            s_BoundsHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketBounds, MockModuleCount, ref s_BoundsHandle);
            s_ConnectionsHandle = EnsureVaultHandle(vault, BufferID.ConstructionSocketConnections, MockSocketCount, ref s_ConnectionsHandle);
            s_BuilderGhostStateHandle = EnsureVaultHandle(vault, BuilderGhostStateBufferId, BuilderGhostStateCapacity, ref s_BuilderGhostStateHandle);
            s_BuilderGhostVisualHandle = EnsureVaultHandle(vault, BuilderGhostVisualBufferId, BuilderGhostVisualCapacity, ref s_BuilderGhostVisualHandle);
            s_HolographyTelemetryHandle = EnsureVaultHandle(vault, BuilderGhostTelemetryBufferId, TelemetryCapacity, ref s_HolographyTelemetryHandle);
            s_BuilderGhostMockStateHandle = EnsureVaultHandle(vault, BuilderGhostMockStateBufferId, BuilderGhostMockValidationCount, ref s_BuilderGhostMockStateHandle);
            s_BuilderGhostSdfSamplesHandle = EnsureVaultHandle(vault, BuilderGhostSdfSamplesBufferId, BuilderGhostSdfSampleCapacity, ref s_BuilderGhostSdfSamplesHandle);
            s_BuilderGhostIndirectArgsHandle = EnsureVaultHandle(vault, BuilderGhostIndirectArgsBufferId, 1, ref s_BuilderGhostIndirectArgsHandle);

            if (resetCounters &&
                TryAcquireWriteLane(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters, out NativeArray<int> counters))
            {
                try
                {
                    ClearCounterLane(counters);
                }
                finally
                {
                    ReleaseSocketWrite(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters);
                }
            }

            if (TryAcquireWriteLane(vault, in s_TuningHandle, BufferID.ConstructionSocketTuning, out NativeArray<ConstructionSocketTuningDTO> tuningBuffer))
            {
                try
                {
                    tuningBuffer[0] = s_Tuning;
                }
                finally
                {
                    ReleaseSocketWrite(vault, in s_TuningHandle, BufferID.ConstructionSocketTuning);
                }
            }

            return ValidateStructLayout();
        }

        private static void ResetVaultDescriptorsIfOwnerChanged(IDataVault vault)
        {
            if (ReferenceEquals(s_BoundVault, vault))
                return;

            s_BoundVault = vault;
            s_TelemetryDumped = false;
            s_HolographyTelemetryDumped = false;
            s_SocketStatesHandle = default;
            s_SocketAupHandle = default;
            s_GhostSocketStatesHandle = default;
            s_GhostSocketAupHandle = default;
            s_GhostPreviewHandle = default;
            s_SocketCsrRangesHandle = default;
            s_SocketCsrTargetIndicesHandle = default;
            s_SnapResultsHandle = default;
            s_TelemetryHandle = default;
            s_TuningHandle = default;
            s_ModuleHandle = default;
            s_CountersHandle = default;
            s_BoundsHandle = default;
            s_ConnectionsHandle = default;
            s_BuilderGhostStateHandle = default;
            s_BuilderGhostVisualHandle = default;
            s_HolographyTelemetryHandle = default;
            s_BuilderGhostMockStateHandle = default;
            s_BuilderGhostSdfSamplesHandle = default;
            s_BuilderGhostIndirectArgsHandle = default;
        }

        public static bool TryResolveVaultViews(IDataVault vault, out ConstructionSocketVaultViews views)
        {
            return TryReadVaultViews(vault, out views);
        }

        public static bool TryReadVaultViews(IDataVault vault, out ConstructionSocketVaultViews views)
        {
            views = default;
            if (vault == null)
                return false;

            return TryReadSocketBuffer(vault, in s_SocketStatesHandle, BufferID.ConstructionSocketStates, out views.SocketStates) &&
                   TryReadSocketBuffer(vault, in s_SocketAupHandle, BufferID.ConstructionSocketAup, out views.SocketAups) &&
                   TryReadSocketBuffer(vault, in s_GhostSocketStatesHandle, BufferID.ConstructionGhostSocketStates, out views.GhostSocketStates) &&
                   TryReadSocketBuffer(vault, in s_GhostSocketAupHandle, BufferID.ConstructionGhostSocketAup, out views.GhostSocketAups) &&
                   TryReadSocketBuffer(vault, in s_GhostPreviewHandle, GhostPreviewBufferId, out views.GhostPreviews) &&
                   TryReadSocketBuffer(vault, in s_SocketCsrRangesHandle, SocketCsrRangesBufferId, out views.SocketCsrRanges) &&
                   TryReadSocketBuffer(vault, in s_SocketCsrTargetIndicesHandle, SocketCsrTargetIndicesBufferId, out views.SocketCsrTargetIndices) &&
                   TryReadSocketBuffer(vault, in s_SnapResultsHandle, BufferID.ConstructionSocketSnapResults, out views.SnapResults) &&
                   TryReadSocketBuffer(vault, in s_TelemetryHandle, BufferID.ConstructionSocketTelemetry, out views.Telemetry) &&
                   TryReadSocketBuffer(vault, in s_TuningHandle, BufferID.ConstructionSocketTuning, out views.Tuning) &&
                   TryReadSocketBuffer(vault, in s_ModuleHandle, BufferID.ConstructionSocketModules, out views.Modules) &&
                   TryReadSocketBuffer(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters, out views.Counters) &&
                   TryReadSocketBuffer(vault, in s_BoundsHandle, BufferID.ConstructionSocketBounds, out views.Bounds) &&
                   TryReadSocketBuffer(vault, in s_ConnectionsHandle, BufferID.ConstructionSocketConnections, out views.Connections) &&
                   TryReadSocketBuffer(vault, in s_BuilderGhostStateHandle, BuilderGhostStateBufferId, out views.BuilderGhostStates) &&
                   TryReadSocketBuffer(vault, in s_BuilderGhostVisualHandle, BuilderGhostVisualBufferId, out views.BuilderGhostVisuals) &&
                   TryReadSocketBuffer(vault, in s_HolographyTelemetryHandle, BuilderGhostTelemetryBufferId, out views.HolographyTelemetry) &&
                   TryReadSocketBuffer(vault, in s_BuilderGhostSdfSamplesHandle, BuilderGhostSdfSamplesBufferId, out views.BuilderGhostSdfSamples) &&
                   TryReadSocketBuffer(vault, in s_BuilderGhostIndirectArgsHandle, BuilderGhostIndirectArgsBufferId, out views.BuilderGhostIndirectArgs);
        }

        public static bool TryBeginModuleReadFence()
        {
            if (Volatile.Read(ref s_ModuleWriteFence) != 0)
                return false;

            while (true)
            {
                int observed = Volatile.Read(ref s_ModuleReadFenceDepth);
                if (observed < 0 ||
                    observed == int.MaxValue ||
                    Volatile.Read(ref s_ModuleWriteFence) != 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref s_ModuleReadFenceDepth, observed + 1, observed) == observed)
                    break;
            }

            if (Volatile.Read(ref s_ModuleWriteFence) == 0)
                return true;

            EndModuleReadFence();
            return false;
        }

        public static void EndModuleReadFence()
        {
            while (true)
            {
                int observed = Volatile.Read(ref s_ModuleReadFenceDepth);
                if (observed <= 0)
                    return;

                if (Interlocked.CompareExchange(ref s_ModuleReadFenceDepth, observed - 1, observed) == observed)
                    return;
            }
        }

        public static bool HasActiveModuleReadFence()
        {
            return Volatile.Read(ref s_ModuleReadFenceDepth) > 0;
        }

        public static bool TryBeginModuleWriteFence()
        {
            if (Volatile.Read(ref s_ModuleReadFenceDepth) != 0 ||
                Interlocked.CompareExchange(ref s_ModuleWriteFence, 1, 0) != 0)
            {
                return false;
            }

            if (Volatile.Read(ref s_ModuleReadFenceDepth) == 0)
                return true;

            Interlocked.Exchange(ref s_ModuleWriteFence, 0);
            return false;
        }

        public static void EndModuleWriteFence()
        {
            Interlocked.Exchange(ref s_ModuleWriteFence, 0);
        }

        public static bool GenerateMockBaseConstructionGrid(IDataVault vault)
        {
            if (vault == null)
                return false;

            if (!TryBeginModuleWriteFence())
                return false;

            try
            {
                InitializeVault(vault);
                if (!EnsureMockGenerationScratchCold())
                    return false;

                int moduleCount = math.min(MockModuleCount, s_MockModules.Length);
                int socketCapacity = math.min(MockSocketCount, math.min(s_MockSockets.Length, s_MockSocketAups.Length));
                int csrTargetCapacity = math.min(SocketCsrTargetIndexCapacity, s_MockCsrTargetIndices.Length);
                const float spacing = 6f;

                ClearCounterLane(s_MockCounters);

                for (int i = 0; i < moduleCount; i++)
                {
                    int gridX = i % 25;
                    int gridZ = i / 25;
                    double3 root = new double3(gridX * spacing, -40.0, gridZ * spacing);
                    int socketStart = i * MockSocketsPerModule;
                    ConstructionSocketModuleDTO module;
                    module.RootAup = root;
                    module.Rotation = quaternion.identity;
                    module.BoundsCenter = float3.zero;
                    module.BoundsExtents = new float3(2f, 1.5f, 2f);
                    module.ModuleHash = 0x53484E00u + (uint)i;
                    module.SocketStart = socketStart;
                    module.SocketCount = socketStart + MockSocketsPerModule <= socketCapacity ? MockSocketsPerModule : 0;
                    module.Flags = 0u;
                    module.TopologyVersion = 1u;
                    module.DearLieDampen = 0f;
                    module.ConnectedMask = 0u;
                    module.SceneModuleListIndex = -1;
                    s_MockModules[i] = module;

                    for (int s = 0; s < module.SocketCount; s++)
                    {
                        int socketIndex = socketStart + s;
                        byte direction = (byte)s;
                        float3 normal = DirectionToNormal(direction);
                        double3 local = new double3(normal.x * 2.0, normal.y * 1.5, normal.z * 2.0);
                        SocketStateDTO socket = default;
                        socket.LocalOffset = local;
                        socket.NormalDirection = normal;
                        socket.AllowedConnectionBitmask = PackAllowedConnectionBitmask(direction, UniversalCompatibilityHash24);
                        socket.ParentModuleHash = module.ModuleHash;
                        socket.ConnectionStatus = 0u;
                        s_MockSockets[socketIndex] = socket;
                        s_MockSocketAups[socketIndex] = root + local;
                    }
                }

                int activeSocketCount = math.min(moduleCount * MockSocketsPerModule, socketCapacity);
                if (s_MockCounters.Length >= 4)
                {
                    s_MockCounters[0] = moduleCount;
                    s_MockCounters[1] = activeSocketCount;
                    s_MockCounters[2] = 1;
                    s_MockCounters[3] = 0;
                    if (s_MockCounters.Length > CounterMagicIndex)
                        s_MockCounters[CounterMagicIndex] = CounterMagic;
                }

                if (!BuildSocketDirectionCsrStaged(s_MockSockets, activeSocketCount, s_MockCsrRanges, s_MockCsrTargetIndices))
                    return false;

                if (!TryInvalidateCounterLane(vault))
                    return false;

                return TryPublishWriteLane(vault, in s_ModuleHandle, BufferID.ConstructionSocketModules, s_MockModules, moduleCount) &&
                       TryPublishWriteLane(vault, in s_SocketStatesHandle, BufferID.ConstructionSocketStates, s_MockSockets, socketCapacity) &&
                       TryPublishWriteLane(vault, in s_SocketAupHandle, BufferID.ConstructionSocketAup, s_MockSocketAups, socketCapacity) &&
                       TryPublishWriteLane(vault, in s_SocketCsrRangesHandle, SocketCsrRangesBufferId, s_MockCsrRanges, SocketDirectionCount) &&
                       TryPublishWriteLane(vault, in s_SocketCsrTargetIndicesHandle, SocketCsrTargetIndicesBufferId, s_MockCsrTargetIndices, math.min(activeSocketCount, csrTargetCapacity)) &&
                       TryPublishWriteLane(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters, s_MockCounters, math.min(s_MockCounters.Length, CounterMagicIndex + 1));
            }
            finally
            {
                EndModuleWriteFence();
            }
        }

        private static bool EnsureMockGenerationScratchCold()
        {
            if (s_MockModules == null || s_MockModules.Length < MockModuleCount)
                s_MockModules = new ConstructionSocketModuleDTO[MockModuleCount];
            if (s_MockSockets == null || s_MockSockets.Length < MockSocketCount)
                s_MockSockets = new SocketStateDTO[MockSocketCount];
            if (s_MockSocketAups == null || s_MockSocketAups.Length < MockSocketCount)
                s_MockSocketAups = new double3[MockSocketCount];
            if (s_MockCounters == null || s_MockCounters.Length < CounterMagicIndex + 1)
                s_MockCounters = new int[CounterMagicIndex + 1];
            if (s_MockCsrRanges == null || s_MockCsrRanges.Length < SocketDirectionCount)
                s_MockCsrRanges = new int2[SocketDirectionCount];
            if (s_MockCsrTargetIndices == null || s_MockCsrTargetIndices.Length < SocketCsrTargetIndexCapacity)
                s_MockCsrTargetIndices = new int[SocketCsrTargetIndexCapacity];

            return true;
        }

        public static bool BuildSocketDirectionCsr(
            NativeArray<SocketStateDTO> sockets,
            int targetSocketCount,
            NativeArray<int2> csrRanges,
            NativeArray<int> csrTargetIndices)
        {
            if (!sockets.IsCreated ||
                !csrRanges.IsCreated ||
                !csrTargetIndices.IsCreated ||
                csrRanges.Length < SocketDirectionCount)
            {
                return false;
            }

            int safeCount = math.clamp(targetSocketCount, 0, math.min(sockets.Length, csrTargetIndices.Length));
            int count0 = 0;
            int count1 = 0;
            int count2 = 0;
            int count3 = 0;
            int count4 = 0;
            int count5 = 0;
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsOpenFiniteSocket(sockets[i]))
                    continue;

                switch (ExtractDirection(sockets[i]))
                {
                    case 0: count0++; break;
                    case 1: count1++; break;
                    case 2: count2++; break;
                    case 3: count3++; break;
                    case 4: count4++; break;
                    case 5: count5++; break;
                }
            }

            int start0 = 0;
            int start1 = start0 + count0;
            int start2 = start1 + count1;
            int start3 = start2 + count2;
            int start4 = start3 + count3;
            int start5 = start4 + count4;
            int cursor0 = start0;
            int cursor1 = start1;
            int cursor2 = start2;
            int cursor3 = start3;
            int cursor4 = start4;
            int cursor5 = start5;
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsOpenFiniteSocket(sockets[i]))
                    continue;

                switch (ExtractDirection(sockets[i]))
                {
                    case 0: csrTargetIndices[cursor0++] = i; break;
                    case 1: csrTargetIndices[cursor1++] = i; break;
                    case 2: csrTargetIndices[cursor2++] = i; break;
                    case 3: csrTargetIndices[cursor3++] = i; break;
                    case 4: csrTargetIndices[cursor4++] = i; break;
                    case 5: csrTargetIndices[cursor5++] = i; break;
                }
            }

            csrRanges[0] = new int2(start0, count0);
            csrRanges[1] = new int2(start1, count1);
            csrRanges[2] = new int2(start2, count2);
            csrRanges[3] = new int2(start3, count3);
            csrRanges[4] = new int2(start4, count4);
            csrRanges[5] = new int2(start5, count5);
            return true;
        }

        private static bool BuildSocketDirectionCsrStaged(
            SocketStateDTO[] sockets,
            int targetSocketCount,
            int2[] csrRanges,
            int[] csrTargetIndices)
        {
            if (sockets == null ||
                csrRanges == null ||
                csrTargetIndices == null ||
                csrRanges.Length < SocketDirectionCount)
            {
                return false;
            }

            int safeCount = math.clamp(targetSocketCount, 0, math.min(sockets.Length, csrTargetIndices.Length));
            int count0 = 0;
            int count1 = 0;
            int count2 = 0;
            int count3 = 0;
            int count4 = 0;
            int count5 = 0;
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsOpenFiniteSocket(sockets[i]))
                    continue;

                switch (ExtractDirection(sockets[i]))
                {
                    case 0: count0++; break;
                    case 1: count1++; break;
                    case 2: count2++; break;
                    case 3: count3++; break;
                    case 4: count4++; break;
                    case 5: count5++; break;
                }
            }

            int start0 = 0;
            int start1 = start0 + count0;
            int start2 = start1 + count1;
            int start3 = start2 + count2;
            int start4 = start3 + count3;
            int start5 = start4 + count4;
            int cursor0 = start0;
            int cursor1 = start1;
            int cursor2 = start2;
            int cursor3 = start3;
            int cursor4 = start4;
            int cursor5 = start5;
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsOpenFiniteSocket(sockets[i]))
                    continue;

                switch (ExtractDirection(sockets[i]))
                {
                    case 0: csrTargetIndices[cursor0++] = i; break;
                    case 1: csrTargetIndices[cursor1++] = i; break;
                    case 2: csrTargetIndices[cursor2++] = i; break;
                    case 3: csrTargetIndices[cursor3++] = i; break;
                    case 4: csrTargetIndices[cursor4++] = i; break;
                    case 5: csrTargetIndices[cursor5++] = i; break;
                }
            }

            csrRanges[0] = new int2(start0, count0);
            csrRanges[1] = new int2(start1, count1);
            csrRanges[2] = new int2(start2, count2);
            csrRanges[3] = new int2(start3, count3);
            csrRanges[4] = new int2(start4, count4);
            csrRanges[5] = new int2(start5, count5);
            return true;
        }

        private static bool TryInvalidateCounterLane(IDataVault vault)
        {
            if (!TryAcquireWriteLane(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters, out NativeArray<int> counters))
                return false;

            try
            {
                if (!counters.IsCreated)
                    return false;

                for (int i = 0; i < counters.Length; i++)
                    counters[i] = 0;

                return true;
            }
            finally
            {
                ReleaseSocketWrite(vault, in s_CountersHandle, BufferID.ConstructionSocketCounters);
            }
        }

        private static bool TryPublishWriteLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            T[] source,
            int count)
            where T : struct
        {
            if (source == null || count < 0)
                return false;

            if (!TryAcquireWriteLane(vault, in handle, bufferId, out NativeArray<T> buffer))
                return false;

            try
            {
                if (!buffer.IsCreated)
                    return false;

                int safeCount = math.min(count, math.min(source.Length, buffer.Length));
                for (int i = 0; i < safeCount; i++)
                    buffer[i] = source[i];

                return safeCount == count;
            }
            finally
            {
                ReleaseSocketWrite(vault, in handle, bufferId);
            }
        }

        private static bool ShouldResetCounterLane(IDataVault vault)
        {
            if (!vault.TryGetGenerationHandle<int>(BufferID.ConstructionSocketCounters, out VaultGenerationHandle<int> existingHandle) ||
                !IsSocketVaultHandle(in existingHandle, BufferID.ConstructionSocketCounters) ||
                !vault.TryReadHandle(in existingHandle, out NativeArray<int> counters) ||
                !counters.IsCreated ||
                counters.Length <= CounterMagicIndex)
            {
                return true;
            }

            int moduleCapacity = MockModuleCount;
            int socketCapacity = MockSocketCount;
            int connectionCapacity = MockSocketCount;
            return counters[CounterMagicIndex] != CounterMagic ||
                   counters[0] < 0 ||
                   counters[0] > moduleCapacity ||
                   counters[1] < 0 ||
                   counters[1] > socketCapacity ||
                   counters[4] < 0 ||
                   counters[4] > connectionCapacity;
        }

        private static void ClearCounterLane(NativeArray<int> counters)
        {
            if (!counters.IsCreated)
                return;

            for (int i = 0; i < counters.Length; i++)
                counters[i] = 0;

            if (counters.Length > CounterMagicIndex)
                counters[CounterMagicIndex] = CounterMagic;
        }

        private static void ClearCounterLane(int[] counters)
        {
            if (counters == null)
                return;

            for (int i = 0; i < counters.Length; i++)
                counters[i] = 0;

            if (counters.Length > CounterMagicIndex)
                counters[CounterMagicIndex] = CounterMagic;
        }

        private static bool TryAcquireWriteLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                !IsSocketVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (buffer.IsCreated)
                {
                    releaseOnFailure = false;
                    return true;
                }

                buffer = default;
                return false;
            }
            finally
            {
                if (releaseOnFailure)
                    ReleaseSocketWrite(vault, in handle, bufferId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOpenFiniteSocket(SocketStateDTO socket)
        {
            return (socket.ConnectionStatus & (ConstructionSocketFlags.Connected | ConstructionSocketFlags.CollisionBlocked | ConstructionSocketFlags.NonFinite)) == 0u &&
                   HasValidDirection(socket) &&
                   math.all(math.isfinite(socket.LocalOffset)) &&
                   math.all(math.isfinite(socket.NormalDirection));
        }

        public static void WriteTelemetry(
            NativeArray<ConstructionSocketTelemetryEntry> telemetryRing,
            uint frame,
            double3 previewAup,
            uint activeSockets,
            uint evaluatedCandidates,
            uint acceptedSnaps,
            float solverMicroseconds,
            float bestDistanceSq,
            uint flags,
            uint resultHash,
            float globalQualityWeight,
            uint topologyVersion)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int index = (int)(frame % (uint)math.min(telemetryRing.Length, TelemetryCapacity));
            ConstructionSocketTelemetryEntry entry;
            entry.PreviewAup = previewAup;
            entry.Frame = frame;
            entry.ActiveSocketCount = activeSockets;
            entry.EvaluatedCandidateCount = evaluatedCandidates;
            entry.AcceptedSnapCount = acceptedSnaps;
            entry.SolverMicroseconds = math.isfinite(solverMicroseconds) ? solverMicroseconds : -1f;
            entry.BestDistanceSq = math.isfinite(bestDistanceSq) ? bestDistanceSq : float.MaxValue;
            entry.Flags = flags;
            entry.ResultHash = resultHash;
            entry.GlobalQualityWeight = SanitizeQuality(globalQualityWeight);
            entry.TopologyVersion = topologyVersion;
            telemetryRing[index] = entry;

            bool nonFinite = !math.all(math.isfinite(previewAup)) ||
                             !math.isfinite(entry.SolverMicroseconds) ||
                             !math.isfinite(entry.BestDistanceSq) ||
                             (flags & ConstructionSocketFlags.NonFinite) != 0u;
            if (nonFinite && !s_TelemetryDumped)
            {
                s_TelemetryDumped = true;
                DumpTelemetry(telemetryRing);
            }
        }

        public static void DumpTelemetry(NativeArray<ConstructionSocketTelemetryEntry> telemetryRing, string absolutePath = DefaultDumpPath)
        {
            DumpNativeRingToFile(telemetryRing, absolutePath);
        }

        public static void WriteHolographyTelemetry(
            NativeArray<HolographyTelemetryEntry> telemetryRing,
            uint frame,
            double3 targetAup,
            uint prefabHash,
            uint sdfCornerChecks,
            uint validationFlags,
            float solverMicroseconds,
            float minSdfDistance,
            uint validationStateHash,
            float globalQualityWeight)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int index = (int)(frame % (uint)math.min(telemetryRing.Length, TelemetryCapacity));
            HolographyTelemetryEntry entry = default;
            entry.AUP_TargetPosition = targetAup;
            entry.Frame = frame;
            entry.PrefabHashID = prefabHash;
            entry.SdfCornerChecks = sdfCornerChecks;
            entry.ValidationFlags = validationFlags;
            entry.SolverMicroseconds = math.isfinite(solverMicroseconds) ? solverMicroseconds : -1f;
            entry.MinSdfDistance = math.isfinite(minSdfDistance) ? minSdfDistance : -9999f;
            entry.ValidationStateHash = validationStateHash;
            entry.GlobalQualityWeight = SanitizeQuality(globalQualityWeight);
            telemetryRing[index] = entry;

            bool fault = !math.all(math.isfinite(targetAup)) ||
                         !math.isfinite(entry.SolverMicroseconds) ||
                         entry.SolverMicroseconds > 500f ||
                         (validationFlags & BuilderGhostValidationFlags.NonFinite) != 0u;
            if (fault && !s_HolographyTelemetryDumped)
            {
                s_HolographyTelemetryDumped = true;
                DumpHolographyTelemetry(telemetryRing);
            }
        }

        public static void DumpHolographyTelemetry(NativeArray<HolographyTelemetryEntry> telemetryRing, string absolutePath = HolographyDumpPath)
        {
            DumpNativeRingToFile(telemetryRing, absolutePath);
        }

        private static void DumpNativeRingToFile<T>(NativeArray<T> telemetryRing, string absolutePath)
            where T : unmanaged
        {
            _ = telemetryRing;
            _ = absolutePath;
        }

        private static string ResolveDumpPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return path;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PackAllowedConnectionBitmask(byte direction, uint compatibilityHash24)
        {
            uint directionMask = IsDirectionValid(direction) ? 1u << direction : 0u;
            return directionMask | ((compatibilityHash24 & 0x00FFFFFFu) << 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ExtractDirection(SocketStateDTO socket)
        {
            uint mask = socket.AllowedConnectionBitmask & 0x3Fu;
            if (mask == 0u || (mask & (mask - 1u)) != 0u)
                return byte.MaxValue;

            if ((mask & 1u) != 0u) return 0;
            if ((mask & 2u) != 0u) return 1;
            if ((mask & 4u) != 0u) return 2;
            if ((mask & 8u) != 0u) return 3;
            if ((mask & 16u) != 0u) return 4;
            if ((mask & 32u) != 0u) return 5;
            return byte.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDirectionValid(byte direction)
        {
            return direction < SocketDirectionCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasValidDirection(SocketStateDTO socket)
        {
            return IsDirectionValid(ExtractDirection(socket));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ExtractCompatibilityHash24(SocketStateDTO socket)
        {
            return (socket.AllowedConnectionBitmask >> 8) & 0x00FFFFFFu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCompatible(SocketStateDTO lhs, SocketStateDTO rhs)
        {
            byte lhsDirection = ExtractDirection(lhs);
            byte rhsDirection = ExtractDirection(rhs);
            if (!IsDirectionValid(lhsDirection) ||
                !IsDirectionValid(rhsDirection) ||
                !AreInverseDirections(lhsDirection, rhsDirection))
            {
                return false;
            }

            uint lhsHash = ExtractCompatibilityHash24(lhs);
            uint rhsHash = ExtractCompatibilityHash24(rhs);
            return AreCompatibilityHashesCompatible(lhsHash, rhsHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreCompatibilityHashesCompatible(uint lhsHash, uint rhsHash)
        {
            return lhsHash == UniversalCompatibilityHash24 ||
                   rhsHash == UniversalCompatibilityHash24 ||
                   lhsHash == rhsHash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreInverseDirections(byte lhs, byte rhs)
        {
            return (lhs == 0 && rhs == 1) ||
                   (lhs == 1 && rhs == 0) ||
                   (lhs == 2 && rhs == 3) ||
                   (lhs == 3 && rhs == 2) ||
                   (lhs == 4 && rhs == 5) ||
                   (lhs == 5 && rhs == 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte InvertDirection(byte direction)
        {
            switch (direction)
            {
                case 0: return 1;
                case 1: return 0;
                case 2: return 3;
                case 3: return 2;
                case 4: return 5;
                case 5: return 4;
                default: return byte.MaxValue;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 DirectionToNormal(byte direction)
        {
            switch (direction)
            {
                case 0: return new float3(0f, 0f, 1f);
                case 1: return new float3(0f, 0f, -1f);
                case 2: return new float3(1f, 0f, 0f);
                case 3: return new float3(-1f, 0f, 0f);
                case 4: return new float3(0f, 1f, 0f);
                case 5: return new float3(0f, -1f, 0f);
                default: return float3.zero;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothQuality(float quality)
        {
            float q = SanitizeQuality(quality);
            return q * q * (3f - 2f * q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveCandidateBudget(int minBudget, int maxBudget)
        {
            int safeMin = math.max(1, minBudget);
            int safeMax = math.max(safeMin, maxBudget);
            return safeMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveBuilderGhostCornerIndex(int sequence)
        {
            switch (math.clamp(sequence, 0, BuilderGhostSdfCornerCount - 1))
            {
                case 0: return 0;
                case 1: return 7;
                case 2: return 1;
                case 3: return 6;
                case 4: return 2;
                case 5: return 5;
                case 6: return 3;
                default: return 4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSearchRadius(float lowMeters, float ultraMeters)
        {
            float low = SanitizePositive(lowMeters, 5f);
            float high = math.max(low, SanitizePositive(ultraMeters, 18f));
            return high;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDearLieDampen(float distanceSq, float snapRadius, float quality, float shrinkMeters)
        {
            float safeRadius = SanitizePositive(snapRadius, 1f);
            float dist = math.sqrt(math.max(0f, math.isfinite(distanceSq) ? distanceSq : safeRadius * safeRadius));
            float close01 = 1f - math.saturate(dist / safeRadius);
            return close01 * math.lerp(0.25f, 1f, SmoothQuality(quality)) * math.clamp(shrinkMeters, 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveGlobalQualityWeight()
        {
            float quality = Hecton8.Core.HomeostasisBrain.GlobalQualityWeight;
            return SanitizeQuality(quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQuality(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashCompatibility(string value)
        {
            if (string.IsNullOrEmpty(value))
                return UniversalCompatibilityHash24;

            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 'A' && c <= 'Z')
                    c = (char)(c + 32);
                hash ^= c;
                hash *= FnvPrime;
            }

            uint folded = hash & 0x00FFFFFFu;
            return folded == UniversalCompatibilityHash24 ? 1u : folded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MakeResultHash(uint a, uint b, uint c, uint d)
        {
            uint hash = FnvOffset;
            hash = FoldHash(hash, a);
            hash = FoldHash(hash, b);
            hash = FoldHash(hash, c);
            hash = FoldHash(hash, d);
            return hash;
        }

        private static VaultGenerationHandle<T> EnsureVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsSocketVaultHandle(in handle, bufferId) &&
                vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return handle;
            }

            return vault.EnsureGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private static bool TryReadSocketBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   IsSocketVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static void ReleaseSocketWrite<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsSocketVaultHandle(in handle, bufferId))
                vault.ReleaseWriteLock(in handle, SystemID.Construction);
        }

        private static bool IsSocketVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }
    }
}
