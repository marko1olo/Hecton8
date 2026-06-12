using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World.ShinobuBiomimetic
{
    /// <summary>
    /// Stable hashes and flags used by the matrix-only POI architecture.
    /// </summary>
    public static class ShinobuPoiConstants
    {
        public const uint PrefabHashRuinBase = 0x52554241u;
        public const uint PrefabHashTitaniumStilt = 0x53544C54u;
        public const uint PrefabHashRustedPanel = 0x52504E4Cu;
        public const uint PrefabHashHlodShell = 0x484C4F44u;
        public const uint QuestNodeFloodedHabitat = 0x46484E44u;
        public const uint SectorHashSeed = 2166136261u;
        public const uint FlagMajorPoi = 1u << 0;
        public const uint FlagStilt = 1u << 1;
        public const uint FlagDebris = 1u << 2;
        public const uint FlagRejectedSlope = 1u << 3;
        public const uint FlagMockRules = 1u << 4;
        public const uint FlagNarrative = 1u << 5;
        public const uint FlagVisualAnchor = 1u << 6;
        public const uint FlagNegativeSpaceCulled = 1u << 7;
        public const uint FlagHlodClustered = 1u << 8;
        public const uint FlagOfflineBake = 1u << 9;
        public const uint FlagSectorRouted = 1u << 10;
        public const uint FlagFloraExclusion = 1u << 11;
        public const uint FlagMossAdhesion = 1u << 12;
        public const uint FlagHzbOccluded = 1u << 13;
        public const uint FlagIndirectArgs = 1u << 14;
        public const float GravityUpY = 1f;
        public const float DefaultMaxSlopeDegrees = 10f;
        public const float DefaultClearanceMeters = 1.35f;
        public const float DefaultVisualAnchorScore = 0.42f;
        public const float HlodClusterRadiusMeters = 50f;
        public const float NegativeSpaceMeters = 2000f;
        public const float SectorMeters = 1000f;
        public const int StiltCornerCount = 4;
        public const int EmergencyRuleCount = 4;
    }

    /// <summary>
    /// Single cache-line placement record. Runtime hydration consumes this; SHINOBU never instantiates prefabs.
    /// Layout: AUP 24b, rotation 16b, scale 12b, prefab hash 4b, biome id 4b, narrative hash 4b = 64b.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PoiTransformDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public float3 Scale;
        [FieldOffset(52)] public uint PrefabHash;
        [FieldOffset(56)] public uint BiomeID;
        [FieldOffset(60)] public uint QuestNodeHash;
    }

    /// <summary>
    /// Bounds used by the placement validator to keep matrices out of rocks.
    /// Layout: extents 12b, center offset 12b, clearance 4b, pad 4b = 32b.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StructuralBoundsDTO
    {
        [FieldOffset(0)] public float3 Extents;
        [FieldOffset(12)] public float3 CenterOffset;
        [FieldOffset(24)] public float ClearanceRadius;
        [FieldOffset(28)] public uint _pad0;
    }

    /// <summary>
    /// Cold ruleset row copied from CSV or emergency archaeology fallback into unmanaged memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PoiPlacementRuleDTO
    {
        [FieldOffset(0)] public uint PrefabHash;
        [FieldOffset(4)] public uint BiomeID;
        [FieldOffset(8)] public float MinDepthMeters;
        [FieldOffset(12)] public float MaxDepthMeters;
        [FieldOffset(16)] public float MaxSlopeCos;
        [FieldOffset(20)] public float MinClusterSpacingMeters;
        [FieldOffset(24)] public float ClusterRadiusMeters;
        [FieldOffset(28)] public int BoundsIndex;
        [FieldOffset(32)] public int MaxDebrisMatrices;
        [FieldOffset(36)] public uint StiltPrefabHash;
        [FieldOffset(40)] public uint DebrisPrefabHash;
        [FieldOffset(44)] public uint QuestNodeHash;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint RuleHash;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    /// <summary>
    /// Mock geology signal provided while Agent 41 SDF gradients are not a stable dependency.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockGeologySignal
    {
        [FieldOffset(0)] public double3 SampleAup;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float TerrainHeightMeters;
        [FieldOffset(40)] public float GradientContrast;
        [FieldOffset(44)] public float SignedDistance;
        [FieldOffset(48)] public uint TerrainHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    /// <summary>
    /// Fixed black-box frame for topology placement diagnostics.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PoiPlacementTelemetryEntry
    {
        [FieldOffset(0)] public double3 LastRootAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public int TotalPOIsGenerated;
        [FieldOffset(36)] public int DebrisMatricesCulled;
        [FieldOffset(40)] public int TopologyRejectionWarnings;
        [FieldOffset(44)] public float PlacementComputeTimeMs;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    /// <summary>
    /// Cold diagnostic row for the spatial-syntax solver. Kept unmanaged so editor gizmos can read it without allocations.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VisualAnchorSampleDTO
    {
        [FieldOffset(0)] public double3 RootAup;
        [FieldOffset(24)] public float3 CenterNormal;
        [FieldOffset(36)] public float AnchorScore;
        [FieldOffset(40)] public float GradientContrast;
        [FieldOffset(44)] public float SlopeDot;
        [FieldOffset(48)] public uint RuleHash;
        [FieldOffset(52)] public uint TerrainHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NarrativeBeaconRuleDTO
    {
        [FieldOffset(0)] public uint PrefabHash;
        [FieldOffset(4)] public uint BiomeID;
        [FieldOffset(8)] public uint SectorHash;
        [FieldOffset(12)] public uint QuestNodeHash;
        [FieldOffset(16)] public float MinDepthMeters;
        [FieldOffset(20)] public float MaxDepthMeters;
        [FieldOffset(24)] public uint Priority;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public struct PoiOfflineBakeConfigDTO
    {
        [FieldOffset(0)] public uint Seed;
        [FieldOffset(4)] public int CandidateCount;
        [FieldOffset(8)] public int MaxPoiTransforms;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float DebrisScatterRadiusMeters;
        [FieldOffset(20)] public float AnchorSampleStrideMeters;
        [FieldOffset(24)] public float MinimumAnchorScore;
        [FieldOffset(28)] public float MaxSlopeDegreesOverride;
        [FieldOffset(32)] public uint BiomeAgeHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public int MaxDebrisPerMajor;
        [FieldOffset(44)] public int TelemetryRingLength;
        [FieldOffset(48)] public int SectorHashMapCapacity;
        [FieldOffset(52)] public int SectorGridStrideX;
        [FieldOffset(56)] public float CurrentOverride;
        [FieldOffset(60)] public uint _pad0;
        [FieldOffset(64)] public ulong RequiredBufferMask;
        [FieldOffset(72)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PoiChunkRouteDTO
    {
        [FieldOffset(0)] public uint SectorHash;
        [FieldOffset(4)] public int SourceIndex;
        [FieldOffset(8)] public int SortedIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float2 LocalXZ;
        [FieldOffset(24)] public uint PrefabHash;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PoiSectorRangeDTO
    {
        [FieldOffset(0)] public uint SectorHash;
        [FieldOffset(4)] public int StartIndex;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public double2 SectorOriginXZ;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FloraStructureMaskDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float2 HalfExtentsXZ;
        [FieldOffset(32)] public float ExclusionRadiusMeters;
        [FieldOffset(36)] public float MossInnerRadiusMeters;
        [FieldOffset(40)] public float MossOuterRadiusMeters;
        [FieldOffset(44)] public float AdhesionWeight;
        [FieldOffset(48)] public uint SectorHash;
        [FieldOffset(52)] public uint SourcePoiIndex;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PoiRendererCullProxyDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float3 Extents;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public uint SourceIndex;
        [FieldOffset(44)] public uint SectorHash;
        [FieldOffset(48)] public uint PrefabHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PoiIndirectDrawArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
        [FieldOffset(16)] public uint VisibleCount;
        [FieldOffset(20)] public uint CulledCount;
        [FieldOffset(24)] public uint PrefabHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    /// <summary>
    /// Deterministic fallback prefab bounds used while real base prefabs are not a stable dependency.
    /// </summary>
    public static class MockPrefabBounds
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StructuralBoundsDTO Resolve(int index)
        {
            int safeIndex = math.max(0, index);
            float size = 10f + safeIndex * 6f;
            return new StructuralBoundsDTO
            {
                Extents = new float3(size, 3.5f + safeIndex, size * 0.72f),
                CenterOffset = float3.zero,
                ClearanceRadius = math.max(size, 3f),
                _pad0 = 0u
            };
        }
    }

    /// <summary>
    /// Pointer-backed ref access for native POI arrays. This avoids CS1612 struct-copy mutation traps.
    /// </summary>
    public unsafe ref struct PoiTransformBufferRef
    {
        public void* Ptr;
        public int Length;
        public int Stride;

        /// <summary>
        /// Returns true when the pointer view can address `PoiTransformDTO` rows without stride mismatch.
        /// </summary>
        public bool IsValid()
        {
            return Ptr != null && Length > 0 && Stride == UnsafeUtility.SizeOf<PoiTransformDTO>();
        }

        /// <summary>
        /// Validates an index before callers request a by-ref row through <see cref="ElementAt"/>.
        /// </summary>
        public bool IsValidIndex(int index)
        {
            return IsValid() && (uint)index < (uint)Length;
        }

        public static PoiTransformBufferRef FromNativeArray(NativeArray<PoiTransformDTO> buffer)
        {
            PoiTransformBufferRef view = default;
            if (!buffer.IsCreated || buffer.Length <= 0)
                return view;

            view.Ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
            view.Length = buffer.Length;
            view.Stride = UnsafeUtility.SizeOf<PoiTransformDTO>();
            return view;
        }

        /// <summary>
        /// Returns a mutable reference to an already validated row. Call <see cref="IsValidIndex"/> first.
        /// </summary>
        public ref PoiTransformDTO ElementAt(int index)
        {
            return ref UnsafeUtility.AsRef<PoiTransformDTO>((byte*)Ptr + (index * Stride));
        }
    }

    /// <summary>
    /// Cold helper used when legacy POI binaries are absent or intentionally ignored.
    /// </summary>
    public static class ShinobuPoiEmergencyRules
    {
        public static int GenerateEmergencyMockRules(
            NativeArray<PoiPlacementRuleDTO> rules,
            NativeArray<StructuralBoundsDTO> bounds)
        {
            if (!rules.IsCreated || rules.Length <= 0 || !bounds.IsCreated || bounds.Length <= 0)
                return 0;

            int count = math.min(ShinobuPoiConstants.EmergencyRuleCount, math.min(rules.Length, bounds.Length));
            for (int i = 0; i < count; i++)
            {
                StructuralBoundsDTO mockBounds = MockPrefabBounds.Resolve(i);
                bounds[i] = mockBounds;

                float maxSlopeDegrees = ShinobuPoiConstants.DefaultMaxSlopeDegrees + i * 2f;
                rules[i] = new PoiPlacementRuleDTO
                {
                    PrefabHash = ShinobuPoiConstants.PrefabHashRuinBase + (uint)i,
                    BiomeID = 0xA8000000u | (uint)i,
                    MinDepthMeters = 30f,
                    MaxDepthMeters = 900f,
                    MaxSlopeCos = MathLodApproximation.ApproxCosBhaskara(math.radians(maxSlopeDegrees)),
                    MinClusterSpacingMeters = 2000f,
                    ClusterRadiusMeters = mockBounds.ClearanceRadius + 40f + i * 2f,
                    BoundsIndex = i,
                    MaxDebrisMatrices = 5 + i * 7,
                    StiltPrefabHash = ShinobuPoiConstants.PrefabHashTitaniumStilt,
                    DebrisPrefabHash = ShinobuPoiConstants.PrefabHashRustedPanel,
                    QuestNodeHash = i == 0 ? ShinobuPoiConstants.QuestNodeFloodedHabitat : 0u,
                    Flags = ShinobuPoiConstants.FlagMockRules | (i == 0 ? ShinobuPoiConstants.FlagNarrative : 0u),
                    RuleHash = MixHash(ShinobuPoiConstants.SectorHashSeed, (uint)i),
                    _pad0 = 0u,
                    _pad1 = 0u
                };
            }

            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            hash ^= value >> 16;
            hash *= 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    /// <summary>
    /// Shared unmanaged math for SHINOBU matrix generation jobs.
    /// </summary>
    public static class ShinobuPoiMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PoiTransformDTO CreateTransform(
            double3 aup,
            quaternion rotation,
            float3 scale,
            uint prefabHash,
            uint biomeId,
            uint questNodeHash)
        {
            if (!math.all(math.isfinite(aup)))
                aup = double3.zero;
            if (!math.all(math.isfinite(rotation.value)))
                rotation = quaternion.identity;
            if (!math.all(math.isfinite(scale)))
                scale = new float3(1f, 1f, 1f);

            return new PoiTransformDTO
            {
                AUP = aup,
                Rotation = rotation,
                Scale = math.max(scale, new float3(0.001f, 0.001f, 0.001f)),
                PrefabHash = prefabHash != 0u ? prefabHash : ShinobuPoiConstants.PrefabHashRuinBase,
                BiomeID = biomeId,
                QuestNodeHash = questNodeHash
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion ResolveLevelRotation(float3 terrainNormal, uint seed)
        {
            float3 gravityUp = new float3(0f, ShinobuPoiConstants.GravityUpY, 0f);
            float3 forward = math.cross(gravityUp, terrainNormal);
            if (math.lengthsq(forward) < 0.0001f)
            {
                float yaw = HashToUnit01(seed) * math.PI * 2f;
                MathLodApproximation.ApproxSinCosBhaskara(yaw, out float yawSin, out float yawCos);
                forward = new float3(yawSin, 0f, yawCos);
            }

            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            return quaternion.LookRotationSafe(forward, gravityUp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            hash ^= value >> 16;
            hash *= 16777619u;
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float HashToUnit01(uint hash)
        {
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion RotationYNoTrig(float radians)
        {
            MathLodApproximation.ApproxSinCosBhaskara(math.select(0f, radians, math.isfinite(radians)) * 0.5f, out float sinHalf, out float cosHalf);
            quaternion rotation = new quaternion(0f, sinHalf, 0f, cosHalf);
            float lenSq = math.lengthsq(rotation.value);
            return math.isfinite(lenSq) && lenSq > 0.000001f
                ? new quaternion(rotation.value * math.rsqrt(lenSq))
                : quaternion.identity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMajorPoi(PoiTransformDTO dto)
        {
            return dto.PrefabHash != ShinobuPoiConstants.PrefabHashTitaniumStilt
                && dto.PrefabHash != ShinobuPoiConstants.PrefabHashRustedPanel
                && dto.PrefabHash != ShinobuPoiConstants.PrefabHashHlodShell
                && dto.PrefabHash != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ResolveSectorHash(double3 aup, float sectorMeters)
        {
            float safeSector = math.max(1f, sectorMeters);
            int x = (int)math.floor(aup.x / safeSector);
            int z = (int)math.floor(aup.z / safeSector);
            uint hash = ShinobuPoiConstants.SectorHashSeed;
            hash = MixHash(hash, (uint)x);
            hash = MixHash(hash, (uint)z);
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float2 ResolveSectorLocalXZ(double3 aup, float sectorMeters)
        {
            float safeSector = math.max(1f, sectorMeters);
            double sectorX = math.floor(aup.x / safeSector) * safeSector;
            double sectorZ = math.floor(aup.z / safeSector) * safeSector;
            float2 local = new float2((float)(aup.x - sectorX), (float)(aup.z - sectorZ));
            return math.select(float2.zero, local, math.isfinite(local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToLocalFloat3(double3 aup, double3 localOriginAup)
        {
            double3 delta = aup - localOriginAup;
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            return math.select(float3.zero, local, math.isfinite(local));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float PlanarDistanceSqMeters(double3 a, double3 b)
        {
            float3 local = ToLocalFloat3(a, b);
            local.y = 0f;
            return math.lengthsq(local);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveQualityCurve(float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            return q * q * (3f - 2f * q);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Unity.Mathematics.Random CreateDeterministicRandom(uint sectorHash, uint simulationFrame, uint salt)
        {
            uint seed = MixHash(MixHash(sectorHash, simulationFrame), salt);
            return new Unity.Mathematics.Random(seed != 0u ? seed : 1u);
        }
    }

    /// <summary>
    /// Cold bridge for GlobalRegistry/DataVault ownership. Burst jobs receive only native buffers from this boundary.
    /// </summary>
    public static class ShinobuPoiVaultBridge
    {
        private const SystemID OwnerSystem = SystemID.WorldStreaming;
        public const BufferID PoiTransformsBufferId = BufferID.ShinobuSymbiosisCounters;
        public const BufferID PoiSortedTransformsBufferId = BufferID.ShinobuSymbiosisCsvScratch;
        public const BufferID PoiRoutesBufferId = BufferID.ShinobuSymbiosisScannerVfx;
        public const BufferID PoiSectorRangesBufferId = BufferID.ShinobuSymbiosisOxygenEmitters;
        public const BufferID PoiNarrativeRulesBufferId = BufferID.ShinobuSymbiosisAdherence;
        public const BufferID PoiFloraMasksBufferId = BufferID.ShinobuSymbiosisSeeds;
        public const BufferID PoiTelemetryRingBufferId = BufferID.ShinobuSymbiosisAcousticTaps;
        public const BufferID PoiVisualAnchorsBufferId = BufferID.ShinobuSymbiosisTuning;
        public const BufferID PoiRulesBufferId = BufferID.ShinobuSymbiosisFloraHashBucketHeads;
        public const BufferID PoiBoundsBufferId = BufferID.ShinobuSymbiosisFloraHashNext;
        public const BufferID PoiBakeConfigBufferId = BufferID.ShinobuSymbiosisMockBoids;
        public const BufferID PoiCullProxyBufferId = BufferID.ShinobuSymbiosisLegacyScratch;
        public const BufferID PoiHzbDepthPyramidBufferId = BufferID.ShinobuSymbiosisMockFish;
        public const BufferID PoiVisibleMaskBufferId = BufferID.ShinobuMacroEcosystemSectorFront;
        public const BufferID PoiIndirectArgsBufferId = BufferID.ShinobuMacroEcosystemSectorBack;
        public const BufferID PoiCandidateAupsBufferId = BufferID.ShinobuMacroEcosystemRemainders;
        public const BufferID PoiMockSignalsBufferId = BufferID.ShinobuMacroEcosystemSectorCoords;
        public const BufferID PoiPlacementCountersBufferId = BufferID.ShinobuMacroEcosystemIndexEntries;
        public const BufferID PoiCsvScratchBufferId = BufferID.ShinobuMacroEcosystemBiomeSpecs;
        public const int BlackBoxFrameCount = 300;

        public static bool TryResolveExistingPlacementBuffers(
            out NativeArray<PoiTransformDTO>.ReadOnly poiTransforms,
            out NativeArray<NarrativeBeaconRuleDTO>.ReadOnly narrativeRules,
            out NativeArray<PoiPlacementTelemetryEntry>.ReadOnly telemetryRing)
        {
            poiTransforms = default;
            narrativeRules = default;
            telemetryRing = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return false;

            bool hasPoi = TryReadExistingWorldStreamingBuffer(vault, PoiTransformsBufferId, 1, out poiTransforms);

            TryReadExistingWorldStreamingBuffer(vault, PoiNarrativeRulesBufferId, 1, out narrativeRules);

            TryReadExistingWorldStreamingBuffer(vault, PoiTelemetryRingBufferId, BlackBoxFrameCount, out telemetryRing);

            return hasPoi;
        }

        private static NativeArray<PoiTransformDTO> AcquirePoiTransformBuffer(int capacity)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return AcquireWorldStreamingBuffer<PoiTransformDTO>(vault, PoiTransformsBufferId, capacity);
        }

        private static NativeArray<PoiChunkRouteDTO> AcquireRouteBuffer(int capacity)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return AcquireWorldStreamingBuffer<PoiChunkRouteDTO>(vault, PoiRoutesBufferId, capacity);
        }

        private static NativeArray<PoiPlacementTelemetryEntry> AcquireTelemetryRing()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            return AcquireWorldStreamingBuffer<PoiPlacementTelemetryEntry>(vault, PoiTelemetryRingBufferId, BlackBoxFrameCount);
        }

        private static NativeArray<T> AcquireWorldStreamingBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            int length = math.max(1, requiredLength);
            if (TryOpenExistingWorldStreamingBuffer(vault, bufferId, length, out NativeArray<T> existing))
                return existing;

            if (vault == null || vault.IsAllocationLocked)
                return default;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                OwnerSystem,
                NativeArrayOptions.UninitializedMemory);
            return TryOpenWorldStreamingBuffer(vault, in acquired, bufferId, length, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool TryOpenExistingWorldStreamingBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryOpenWorldStreamingBuffer(vault, in handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryReadExistingWorldStreamingBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)OwnerSystem ||
                handle.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenWorldStreamingBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                handle.BufferID != (uint)bufferId ||
                handle.SystemID != (uint)OwnerSystem ||
                handle.Generation == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }
    }

    public static class ShinobuPoiTelemetryDump
    {
        public const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_42.bin";
        public const string PromptDumpPath = "Docs/AgentLogs/Dump_POI_SCULPTOR.bin";

        public static unsafe bool TryDumpTelemetryRing(NativeArray<PoiPlacementTelemetryEntry> telemetryRing, string path = DumpPath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            try
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                int byteLength = UnsafeUtility.SizeOf<PoiPlacementTelemetryEntry>() * telemetryRing.Length;
                return NativeFaultDumpWriter.TryWriteAll(path, new ReadOnlySpan<byte>(ptr, byteLength), byteLength);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool TryDumpPromptAlias(NativeArray<PoiPlacementTelemetryEntry> telemetryRing)
        {
            return TryDumpTelemetryRing(telemetryRing, PromptDumpPath);
        }
    }

    public static class ShinobuPoiBinaryEndian
    {
        public const uint LegacyRuleMagic = 0x4838504Fu; // H8PO

        public static bool TryReadLegacyRuleHeader(ReadOnlySpan<byte> bytes, out uint magic, out ushort version, out ushort rowCount, out uint schemaHash)
        {
            magic = 0u;
            version = 0;
            rowCount = 0;
            schemaHash = 0u;
            if (bytes.Length < 12)
                return false;

            magic = ReadUInt32Endian(bytes, 0, false);
            bool bigEndian = magic != LegacyRuleMagic && ReverseBytes(magic) == LegacyRuleMagic;
            if (bigEndian)
                magic = LegacyRuleMagic;

            version = ReadUInt16Endian(bytes, 4, bigEndian);
            rowCount = ReadUInt16Endian(bytes, 6, bigEndian);
            schemaHash = ReadUInt32Endian(bytes, 8, bigEndian);
            return magic == LegacyRuleMagic;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReadUInt32Endian(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
        {
            if (offset < 0 || offset + 4 > bytes.Length)
                return 0u;

            uint value = (uint)bytes[offset]
                | ((uint)bytes[offset + 1] << 8)
                | ((uint)bytes[offset + 2] << 16)
                | ((uint)bytes[offset + 3] << 24);
            return bigEndian ? ReverseBytes(value) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort ReadUInt16Endian(ReadOnlySpan<byte> bytes, int offset, bool bigEndian)
        {
            if (offset < 0 || offset + 2 > bytes.Length)
                return 0;

            ushort value = (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
            return bigEndian ? (ushort)((value >> 8) | (value << 8)) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24)
                | ((value & 0x0000FF00u) << 8)
                | ((value & 0x00FF0000u) >> 8)
                | ((value & 0xFF000000u) >> 24);
        }
    }

    public static class ShinobuPoiJobGraph
    {
        public static JobHandle ScheduleMockGeology(MockGeologySignalJob job, int candidateCount, JobHandle dependency)
        {
            return job.Schedule(math.max(0, candidateCount), 64, dependency);
        }

        public static JobHandle ScheduleBlindValidation(PoiBlindDependencyValidationJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle SchedulePlacement(PoiPlacementJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle SchedulePlacementVaultArray(PoiPlacementVaultArrayJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleClusterReduction(PoiDearLieHlodClusterJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleDebris(DebrisScatterJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleNegativeSpace(NegativeSpacePoiCullJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleNarrative(NarrativeBeaconInjectionJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleBakeFence(PoiOfflineBakeFenceJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleSpatialPartition(PoiSpatialPartitioningJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleFloraMasks(FloraStructureMaskJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleRendererCullProxy(PoiRendererCullProxyBuildJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleHzbCull(PoiHzbOcclusionCullJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleIndirectArgs(PoiIndirectDrawArgsJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle ScheduleBlackBox(PoiBlackBoxValidationJob job, JobHandle dependency)
        {
            return job.Schedule(dependency);
        }

        public static JobHandle CombinePostPlacement(JobHandle debrisHandle, JobHandle hlodHandle, JobHandle floraHandle)
        {
            return JobHandle.CombineDependencies(JobHandle.CombineDependencies(debrisHandle, hlodHandle), floraHandle);
        }
    }

    /// <summary>
    /// Deterministic void sampler used until real SDF gradients are registered.
    /// </summary>
    public static class MockGradientSampler
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MockGeologySignal Sample(double3 absolute, uint seed)
        {
            double sectorMeters = ShinobuPoiConstants.SectorMeters;
            double3 sectorOrigin = new double3(
                math.floor(absolute.x / sectorMeters) * sectorMeters,
                0.0,
                math.floor(absolute.z / sectorMeters) * sectorMeters);
            float3 local = ShinobuPoiMath.ToLocalFloat3(absolute, sectorOrigin);
            float sectorPhase = ShinobuPoiMath.HashToUnit01(ShinobuPoiMath.ResolveSectorHash(absolute, ShinobuPoiConstants.SectorMeters)) * math.PI * 2f;
            float x = local.x * 0.0031f + sectorPhase;
            float z = local.z * 0.0027f - sectorPhase * 0.37f;
            float waveA = MathLodApproximation.ApproxSinBhaskara(x + (seed & 31u) * 0.17f);
            float waveB = MathLodApproximation.ApproxCosBhaskara(z - ((seed >> 5) & 31u) * 0.11f);
            float terrainHeight = -120f + (waveA * 16f) + (waveB * 11f);
            float dx = 0.0031f * 16f * MathLodApproximation.ApproxCosBhaskara(x);
            float dz = -0.0027f * 11f * MathLodApproximation.ApproxSinBhaskara(z);
            float3 normal = math.normalizesafe(new float3(-dx, 1f, -dz), new float3(0f, 1f, 0f));
            float slope01 = 1f - math.saturate(math.dot(normal, new float3(0f, 1f, 0f)));
            uint hash = HashAup(absolute, seed);

            return new MockGeologySignal
            {
                SampleAup = absolute,
                Normal = normal,
                TerrainHeightMeters = terrainHeight,
                GradientContrast = slope01,
                SignedDistance = (float)absolute.y - terrainHeight,
                TerrainHash = hash,
                Flags = 0u,
                _pad0 = 0u,
                _pad1 = 0u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SampleHeight(double3 absolute, uint seed)
        {
            return Sample(absolute, seed).TerrainHeightMeters;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ForceSlopeNormalDegrees(float slopeDegrees)
        {
            float radians = math.radians(math.clamp(slopeDegrees, 0f, 85f));
            MathLodApproximation.ApproxSinCosBhaskara(radians, out float sine, out float cosine);
            return math.normalizesafe(new float3(sine, cosine, 0f), new float3(0f, 1f, 0f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashAup(double3 absolute, uint seed)
        {
            uint x = (uint)math.floor(absolute.x * 0.25);
            uint y = (uint)math.floor(absolute.y * 0.25);
            uint z = (uint)math.floor(absolute.z * 0.25);
            uint hash = seed ^ 2166136261u;
            hash = (hash ^ x) * 16777619u;
            hash = (hash ^ y) * 16777619u;
            hash = (hash ^ z) * 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    /// <summary>
    /// Builds mock geology signals for isolated topology testing.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockGeologySignalJob : IJobParallelFor
    {
        [NoAlias, ReadOnly] public NativeArray<double3> CandidateAups;
        [NoAlias] public NativeArray<MockGeologySignal> Signals;
        public uint Seed;
        public float ForcedSlopeDegrees;

        public void Execute(int index)
        {
            if (!CandidateAups.IsCreated || !Signals.IsCreated || (uint)index >= (uint)CandidateAups.Length || (uint)index >= (uint)Signals.Length)
                return;

            double3 absolute = CandidateAups[index];
            MockGeologySignal signal = MockGradientSampler.Sample(absolute, Seed + (uint)index);
            if (ForcedSlopeDegrees >= 0f)
            {
                signal.Normal = MockGradientSampler.ForceSlopeNormalDegrees(ForcedSlopeDegrees);
                signal.GradientContrast = 1f - math.saturate(math.dot(signal.Normal, new float3(0f, 1f, 0f)));
            }

            signal.Flags = math.all(math.isfinite(signal.Normal)) && math.all(math.isfinite(absolute))
                ? 0u
                : ShinobuPoiConstants.FlagRejectedSlope;
            Signals[index] = signal;
        }
    }

    /// <summary>
    /// Proves blind gradient adaptation: flat sites write base matrices; steep sites write a level base plus support stilts.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiBlindDependencyValidationJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<double3> CandidateAups;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias, ReadOnly] public NativeArray<StructuralBoundsDTO> Bounds;
        [NoAlias, ReadOnly] public NativeArray<MockGeologySignal> Signals;
        [NoAlias] public NativeList<PoiTransformDTO> OutputTransforms;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;
        public uint Seed;

        public void Execute()
        {
            if (!OutputTransforms.IsCreated)
                return;

            OutputTransforms.Clear();
            int generated = 0;
            int rejected = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            int candidateCount = CandidateAups.IsCreated ? CandidateAups.Length : 0;
            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            int boundsCount = Bounds.IsCreated ? Bounds.Length : 0;
            int signalCount = Signals.IsCreated ? Signals.Length : 0;

            for (int i = 0; i < candidateCount; i++)
            {
                if (OutputTransforms.Length >= OutputTransforms.Capacity)
                    break;

                int ruleIndex = ruleCount > 0 ? i % ruleCount : -1;
                if (ruleIndex < 0)
                    break;

                PoiPlacementRuleDTO rule = Rules[ruleIndex];
                int boundsIndex = math.clamp(rule.BoundsIndex, 0, math.max(0, boundsCount - 1));
                StructuralBoundsDTO structuralBounds = boundsCount > 0 ? Bounds[boundsIndex] : default;
                MockGeologySignal signal = signalCount > i ? Signals[i] : MockGradientSampler.Sample(CandidateAups[i], Seed + (uint)i);
                if ((signal.Flags & ShinobuPoiConstants.FlagRejectedSlope) != 0u)
                {
                    rejected++;
                    continue;
                }

                float slopeDot = math.saturate(math.dot(signal.Normal, new float3(0f, 1f, 0f)));
                bool steep = slopeDot < rule.MaxSlopeCos;
                double3 baseAbsolute = CandidateAups[i];
                float baseHalfHeight = math.max(0.5f, structuralBounds.Extents.y);
                baseAbsolute.y = math.max(baseAbsolute.y, signal.TerrainHeightMeters + baseHalfHeight + ShinobuPoiConstants.DefaultClearanceMeters);
                quaternion rotation = ResolveLevelRotation(signal.Normal, Seed + (uint)i);
                PoiTransformDTO baseDto = CreateTransform(
                    baseAbsolute,
                    rotation,
                    math.max(new float3(1f, 1f, 1f), structuralBounds.Extents * 2f),
                    rule.PrefabHash,
                    rule.BiomeID,
                    rule.QuestNodeHash);
                OutputTransforms.AddNoResize(baseDto);
                generated++;
                stateHash = MixHash(stateHash, rule.PrefabHash);

                if (!steep)
                    continue;

                int stiltCount = AppendStilts(baseAbsolute, structuralBounds, rule, rotation);
                generated += stiltCount;
            }

            WriteTelemetry(generated, rejected, stateHash);
        }

        private int AppendStilts(
            double3 baseAbsolute,
            StructuralBoundsDTO structuralBounds,
            PoiPlacementRuleDTO rule,
            quaternion baseRotation)
        {
            int written = 0;
            float3 extents = math.max(structuralBounds.Extents, new float3(2f, 1f, 2f));
            for (int corner = 0; corner < ShinobuPoiConstants.StiltCornerCount; corner++)
            {
                if (OutputTransforms.Length >= OutputTransforms.Capacity)
                    break;

                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sz = (corner & 2) == 0 ? -1f : 1f;
                float3 localCorner = new float3(extents.x * sx, -extents.y, extents.z * sz);
                float3 rotatedCorner = math.rotate(baseRotation, localCorner);
                double3 footAbsolute = baseAbsolute + new double3(rotatedCorner.x, rotatedCorner.y, rotatedCorner.z);
                float terrainHeight = MockGradientSampler.SampleHeight(footAbsolute, Seed + (uint)corner);
                float stiltHeight = math.max(0.5f, (float)(baseAbsolute.y + rotatedCorner.y - terrainHeight));
                double3 stiltAbsolute = footAbsolute;
                stiltAbsolute.y = terrainHeight + stiltHeight * 0.5f;
                PoiTransformDTO stilt = CreateTransform(
                    stiltAbsolute,
                    quaternion.identity,
                    new float3(0.45f, stiltHeight, 0.45f),
                    rule.StiltPrefabHash != 0u ? rule.StiltPrefabHash : ShinobuPoiConstants.PrefabHashTitaniumStilt,
                    rule.BiomeID,
                    0u);
                OutputTransforms.AddNoResize(stilt);
                written++;
            }

            return written;
        }

        private void WriteTelemetry(int generated, int rejected, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = CandidateAups.IsCreated && CandidateAups.Length > 0 ? CandidateAups[0] : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = generated,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = rejected,
                PlacementComputeTimeMs = 0f,
                Flags = rejected > 0 ? ShinobuPoiConstants.FlagRejectedSlope : 0u,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static PoiTransformDTO CreateTransform(
            double3 aup,
            quaternion rotation,
            float3 scale,
            uint prefabHash,
            uint biomeId,
            uint questNodeHash)
        {
            if (!math.all(math.isfinite(aup)))
                aup = double3.zero;
            if (!math.all(math.isfinite(rotation.value)))
                rotation = quaternion.identity;
            if (!math.all(math.isfinite(scale)))
                scale = new float3(1f, 1f, 1f);

            return new PoiTransformDTO
            {
                AUP = aup,
                Rotation = rotation,
                Scale = math.max(scale, new float3(0.001f, 0.001f, 0.001f)),
                PrefabHash = prefabHash != 0u ? prefabHash : ShinobuPoiConstants.PrefabHashRuinBase,
                BiomeID = biomeId,
                QuestNodeHash = questNodeHash
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static quaternion ResolveLevelRotation(float3 terrainNormal, uint seed)
        {
            float3 gravityUp = new float3(0f, ShinobuPoiConstants.GravityUpY, 0f);
            float3 forward = math.cross(gravityUp, terrainNormal);
            if (math.lengthsq(forward) < 0.0001f)
            {
                float yaw = ((seed & 1023u) / 1023f) * math.PI * 2f;
                MathLodApproximation.ApproxSinCosBhaskara(yaw, out float yawSin, out float yawCos);
                forward = new float3(yawSin, 0f, yawCos);
            }

            forward = math.normalizesafe(forward, new float3(0f, 0f, 1f));
            return quaternion.LookRotationSafe(forward, gravityUp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash != 0u ? hash : 1u;
        }
    }

    /// <summary>
    /// Offline spatial-syntax pass. It chooses deliberate visual anchors, then emits only matrices.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiPlacementJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<double3> CandidateAups;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias, ReadOnly] public NativeArray<StructuralBoundsDTO> Bounds;
        [NoAlias] public NativeList<PoiTransformDTO> OutputTransforms;
        [NoAlias] public NativeList<VisualAnchorSampleDTO> VisualAnchors;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;
        public uint Seed;
        public float GlobalQualityWeight;
        public float AnchorSampleStrideMeters;
        public float MinimumAnchorScore;

        public void Execute()
        {
            if (!OutputTransforms.IsCreated)
                return;

            OutputTransforms.Clear();
            if (VisualAnchors.IsCreated)
                VisualAnchors.Clear();

            int candidateCount = CandidateAups.IsCreated ? CandidateAups.Length : 0;
            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            int boundsCount = Bounds.IsCreated ? Bounds.Length : 0;
            if (candidateCount <= 0 || ruleCount <= 0)
            {
                WriteTelemetry(0, 0, ShinobuPoiConstants.SectorHashSeed);
                return;
            }

            float quality = math.saturate(GlobalQualityWeight);
            float requestedScore = MinimumAnchorScore > 0f ? MinimumAnchorScore : ShinobuPoiConstants.DefaultVisualAnchorScore;
            float scoreThreshold = math.lerp(0.24f, requestedScore, quality);
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            int generated = 0;
            int rejected = 0;

            for (int i = 0; i < candidateCount; i++)
            {
                if (OutputTransforms.Length >= OutputTransforms.Capacity)
                    break;

                PoiPlacementRuleDTO rule = Rules[i % ruleCount];
                int boundsIndex = math.clamp(rule.BoundsIndex, 0, math.max(0, boundsCount - 1));
                StructuralBoundsDTO structuralBounds = boundsCount > 0 ? Bounds[boundsIndex] : default;
                MockGeologySignal centerSignal;
                VisualAnchorSampleDTO anchor = EvaluateVisualAnchor(CandidateAups[i], rule, structuralBounds, Seed + (uint)i, out centerSignal);
                bool accepted = anchor.AnchorScore >= scoreThreshold;
                anchor.Flags = accepted ? ShinobuPoiConstants.FlagVisualAnchor : ShinobuPoiConstants.FlagRejectedSlope;
                if (VisualAnchors.IsCreated && VisualAnchors.Length < VisualAnchors.Capacity)
                    VisualAnchors.AddNoResize(anchor);

                if (!accepted)
                {
                    rejected++;
                    continue;
                }

                float slopeDot = math.saturate(math.dot(centerSignal.Normal, new float3(0f, 1f, 0f)));
                bool steep = slopeDot < rule.MaxSlopeCos;
                float3 extents = math.max(structuralBounds.Extents, new float3(2f, 0.5f, 2f));
                double3 baseAbsolute = CandidateAups[i];
                baseAbsolute.y = math.max(baseAbsolute.y, centerSignal.TerrainHeightMeters + extents.y + ShinobuPoiConstants.DefaultClearanceMeters);
                quaternion rotation = ShinobuPoiMath.ResolveLevelRotation(centerSignal.Normal, Seed + (uint)i);

                OutputTransforms.AddNoResize(ShinobuPoiMath.CreateTransform(
                    baseAbsolute,
                    rotation,
                    math.max(new float3(1f, 1f, 1f), extents * 2f),
                    rule.PrefabHash,
                    rule.BiomeID,
                    rule.QuestNodeHash));

                generated++;
                stateHash = ShinobuPoiMath.MixHash(stateHash, rule.RuleHash != 0u ? rule.RuleHash : rule.PrefabHash);

                if (steep)
                    generated += AppendStilts(baseAbsolute, extents, rule, rotation, Seed + (uint)i);
            }

            WriteTelemetry(generated, rejected, stateHash);
        }

        private VisualAnchorSampleDTO EvaluateVisualAnchor(
            double3 rootAup,
            PoiPlacementRuleDTO rule,
            StructuralBoundsDTO structuralBounds,
            uint seed,
            out MockGeologySignal centerSignal)
        {
            float stride = AnchorSampleStrideMeters > 0f
                ? AnchorSampleStrideMeters
                : math.max(4f, math.max(structuralBounds.ClearanceRadius, 4f) * 0.5f);

            centerSignal = MockGradientSampler.Sample(rootAup, seed);
            float edgeGradientSum = 0f;
            float maxGradient = 0f;
            uint terrainHash = centerSignal.TerrainHash;

            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    uint sampleHash = ShinobuPoiMath.MixHash(seed, (uint)((z + 1) * 3 + x + 1));
                    double3 sampleAup = rootAup + new double3(x * stride, 0.0, z * stride);
                    MockGeologySignal sample = MockGradientSampler.Sample(sampleAup, sampleHash);
                    if (x == 0 && z == 0)
                    {
                        centerSignal = sample;
                        terrainHash = sample.TerrainHash;
                        continue;
                    }

                    edgeGradientSum += sample.GradientContrast;
                    maxGradient = math.max(maxGradient, sample.GradientContrast);
                    terrainHash = ShinobuPoiMath.MixHash(terrainHash, sample.TerrainHash);
                }
            }

            float slopeDot = math.saturate(math.dot(centerSignal.Normal, new float3(0f, 1f, 0f)));
            float centerFlatness = math.saturate((slopeDot - rule.MaxSlopeCos) / math.max(0.001f, 1f - rule.MaxSlopeCos));
            float edgeMean = edgeGradientSum * 0.125f;
            float terraceContrast = math.saturate((edgeMean - centerSignal.GradientContrast) * 8f + maxGradient * 0.6f);
            float depthMeters = math.abs(centerSignal.TerrainHeightMeters);
            float depthScore = depthMeters >= rule.MinDepthMeters && depthMeters <= rule.MaxDepthMeters ? 1f : 0f;
            float anchorScore = depthScore * math.saturate(centerFlatness * 0.58f + terraceContrast * 0.42f);

            return new VisualAnchorSampleDTO
            {
                RootAup = rootAup,
                CenterNormal = centerSignal.Normal,
                AnchorScore = anchorScore,
                GradientContrast = math.saturate(terraceContrast),
                SlopeDot = slopeDot,
                RuleHash = rule.RuleHash,
                TerrainHash = terrainHash,
                Flags = 0u,
                _pad0 = 0u
            };
        }

        private int AppendStilts(
            double3 baseAbsolute,
            float3 extents,
            PoiPlacementRuleDTO rule,
            quaternion baseRotation,
            uint seed)
        {
            int written = 0;
            uint stiltHash = rule.StiltPrefabHash != 0u ? rule.StiltPrefabHash : ShinobuPoiConstants.PrefabHashTitaniumStilt;
            for (int corner = 0; corner < ShinobuPoiConstants.StiltCornerCount; corner++)
            {
                if (OutputTransforms.Length >= OutputTransforms.Capacity)
                    break;

                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sz = (corner & 2) == 0 ? -1f : 1f;
                float3 localCorner = new float3(extents.x * sx, -extents.y, extents.z * sz);
                float3 rotatedCorner = math.rotate(baseRotation, localCorner);
                double3 footAbsolute = baseAbsolute + new double3(rotatedCorner.x, rotatedCorner.y, rotatedCorner.z);
                float terrainHeight = MockGradientSampler.SampleHeight(footAbsolute, ShinobuPoiMath.MixHash(seed, (uint)corner));
                float topY = (float)(baseAbsolute.y + rotatedCorner.y);
                float stiltHeight = math.max(0.5f, topY - terrainHeight);
                double3 stiltAbsolute = footAbsolute;
                stiltAbsolute.y = terrainHeight + stiltHeight * 0.5f;

                OutputTransforms.AddNoResize(ShinobuPoiMath.CreateTransform(
                    stiltAbsolute,
                    quaternion.identity,
                    new float3(0.45f, stiltHeight, 0.45f),
                    stiltHash,
                    rule.BiomeID,
                    0u));
                written++;
            }

            return written;
        }

        private void WriteTelemetry(int generated, int rejected, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = CandidateAups.IsCreated && CandidateAups.Length > 0 ? CandidateAups[0] : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = generated,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = rejected,
                PlacementComputeTimeMs = 0f,
                Flags = rejected > 0 ? ShinobuPoiConstants.FlagRejectedSlope : ShinobuPoiConstants.FlagVisualAnchor,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Editor/local bake variant that writes directly into DataVault arrays and records explicit counters for gizmos/streaming.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiPlacementVaultArrayJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<double3> CandidateAups;
        [NoAlias, ReadOnly] public NativeArray<MockGeologySignal> Signals;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias, ReadOnly] public NativeArray<StructuralBoundsDTO> Bounds;
        [NoAlias] public NativeArray<PoiTransformDTO> OutputTransforms;
        [NoAlias] public NativeArray<VisualAnchorSampleDTO> VisualAnchors;
        [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;
        public uint Seed;
        public float GlobalQualityWeight;
        public float AnchorSampleStrideMeters;
        public float MinimumAnchorScore;
        public float MaxSlopeDegreesOverride;

        public void Execute()
        {
            ClearCounters();
            if (!OutputTransforms.IsCreated || OutputTransforms.Length <= 0)
                return;

            int candidateCount = CandidateAups.IsCreated ? CandidateAups.Length : 0;
            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            int boundsCount = Bounds.IsCreated ? Bounds.Length : 0;
            if (candidateCount <= 0 || ruleCount <= 0)
            {
                WriteTelemetry(0, 0, 0, ShinobuPoiConstants.SectorHashSeed);
                return;
            }

            float quality = math.saturate(GlobalQualityWeight);
            float requestedScore = MinimumAnchorScore > 0f ? MinimumAnchorScore : ShinobuPoiConstants.DefaultVisualAnchorScore;
            float scoreThreshold = math.lerp(0.24f, requestedScore, quality);
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            int outputCount = 0;
            int anchorCount = 0;
            int rejected = 0;

            for (int i = 0; i < candidateCount; i++)
            {
                if (outputCount >= OutputTransforms.Length)
                    break;

                PoiPlacementRuleDTO rule = Rules[i % ruleCount];
                if (MaxSlopeDegreesOverride > 0f)
                    rule.MaxSlopeCos = MathLodApproximation.ApproxCosBhaskara(math.radians(math.clamp(MaxSlopeDegreesOverride, 1f, 85f)));

                int boundsIndex = math.clamp(rule.BoundsIndex, 0, math.max(0, boundsCount - 1));
                StructuralBoundsDTO structuralBounds = boundsCount > 0 ? Bounds[boundsIndex] : default;
                MockGeologySignal centerSignal;
                VisualAnchorSampleDTO anchor = EvaluateVisualAnchor(i, CandidateAups[i], rule, structuralBounds, Seed + (uint)i, out centerSignal);
                bool accepted = anchor.AnchorScore >= scoreThreshold;
                anchor.Flags = accepted ? ShinobuPoiConstants.FlagVisualAnchor : ShinobuPoiConstants.FlagRejectedSlope;
                if (VisualAnchors.IsCreated && anchorCount < VisualAnchors.Length)
                    VisualAnchors[anchorCount++] = anchor;

                if (!accepted)
                {
                    rejected++;
                    continue;
                }

                float slopeDot = math.saturate(math.dot(centerSignal.Normal, new float3(0f, 1f, 0f)));
                bool steep = slopeDot < rule.MaxSlopeCos;
                float3 extents = math.max(structuralBounds.Extents, new float3(2f, 0.5f, 2f));
                double3 baseAbsolute = CandidateAups[i];
                baseAbsolute.y = math.max(baseAbsolute.y, centerSignal.TerrainHeightMeters + extents.y + ShinobuPoiConstants.DefaultClearanceMeters);
                quaternion rotation = ShinobuPoiMath.ResolveLevelRotation(centerSignal.Normal, Seed + (uint)i);

                OutputTransforms[outputCount++] = ShinobuPoiMath.CreateTransform(
                    baseAbsolute,
                    rotation,
                    math.max(new float3(1f, 1f, 1f), extents * 2f),
                    rule.PrefabHash,
                    rule.BiomeID,
                    rule.QuestNodeHash);

                stateHash = ShinobuPoiMath.MixHash(stateHash, rule.RuleHash != 0u ? rule.RuleHash : rule.PrefabHash);
                if (steep)
                    AppendStilts(baseAbsolute, extents, rule, rotation, Seed + (uint)i, ref outputCount);
            }

            WriteCounters(outputCount, anchorCount, rejected, stateHash);
            WriteTelemetry(outputCount, rejected, anchorCount, stateHash);
        }

        private VisualAnchorSampleDTO EvaluateVisualAnchor(
            int candidateIndex,
            double3 rootAup,
            PoiPlacementRuleDTO rule,
            StructuralBoundsDTO structuralBounds,
            uint seed,
            out MockGeologySignal centerSignal)
        {
            float stride = AnchorSampleStrideMeters > 0f
                ? AnchorSampleStrideMeters
                : math.max(4f, math.max(structuralBounds.ClearanceRadius, 4f) * 0.5f);

            int signalCount = Signals.IsCreated ? Signals.Length : 0;
            centerSignal = signalCount > candidateIndex ? Signals[candidateIndex] : MockGradientSampler.Sample(rootAup, seed);
            float edgeGradientSum = 0f;
            float maxGradient = 0f;
            uint terrainHash = centerSignal.TerrainHash;

            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    uint sampleHash = ShinobuPoiMath.MixHash(seed, (uint)((z + 1) * 3 + x + 1));
                    double3 sampleAup = rootAup + new double3(x * stride, 0.0, z * stride);
                    MockGeologySignal sample = x == 0 && z == 0 && signalCount > candidateIndex
                        ? centerSignal
                        : MockGradientSampler.Sample(sampleAup, sampleHash);
                    if (x == 0 && z == 0)
                    {
                        centerSignal = sample;
                        terrainHash = sample.TerrainHash;
                        continue;
                    }

                    edgeGradientSum += sample.GradientContrast;
                    maxGradient = math.max(maxGradient, sample.GradientContrast);
                    terrainHash = ShinobuPoiMath.MixHash(terrainHash, sample.TerrainHash);
                }
            }

            float slopeDot = math.saturate(math.dot(centerSignal.Normal, new float3(0f, 1f, 0f)));
            float centerFlatness = math.saturate((slopeDot - rule.MaxSlopeCos) / math.max(0.001f, 1f - rule.MaxSlopeCos));
            float edgeMean = edgeGradientSum * 0.125f;
            float terraceContrast = math.saturate((edgeMean - centerSignal.GradientContrast) * 8f + maxGradient * 0.6f);
            float depthMeters = math.abs(centerSignal.TerrainHeightMeters);
            float depthScore = depthMeters >= rule.MinDepthMeters && depthMeters <= rule.MaxDepthMeters ? 1f : 0f;
            float anchorScore = depthScore * math.saturate(centerFlatness * 0.58f + terraceContrast * 0.42f);

            return new VisualAnchorSampleDTO
            {
                RootAup = rootAup,
                CenterNormal = centerSignal.Normal,
                AnchorScore = anchorScore,
                GradientContrast = math.saturate(terraceContrast),
                SlopeDot = slopeDot,
                RuleHash = rule.RuleHash,
                TerrainHash = terrainHash,
                Flags = 0u,
                _pad0 = 0u
            };
        }

        private void AppendStilts(
            double3 baseAbsolute,
            float3 extents,
            PoiPlacementRuleDTO rule,
            quaternion baseRotation,
            uint seed,
            ref int outputCount)
        {
            uint stiltHash = rule.StiltPrefabHash != 0u ? rule.StiltPrefabHash : ShinobuPoiConstants.PrefabHashTitaniumStilt;
            for (int corner = 0; corner < ShinobuPoiConstants.StiltCornerCount; corner++)
            {
                if (outputCount >= OutputTransforms.Length)
                    break;

                float sx = (corner & 1) == 0 ? -1f : 1f;
                float sz = (corner & 2) == 0 ? -1f : 1f;
                float3 localCorner = new float3(extents.x * sx, -extents.y, extents.z * sz);
                float3 rotatedCorner = math.rotate(baseRotation, localCorner);
                double3 footAbsolute = baseAbsolute + new double3(rotatedCorner.x, rotatedCorner.y, rotatedCorner.z);
                float terrainHeight = MockGradientSampler.SampleHeight(footAbsolute, ShinobuPoiMath.MixHash(seed, (uint)corner));
                float topY = (float)(baseAbsolute.y + rotatedCorner.y);
                float stiltHeight = math.max(0.5f, topY - terrainHeight);
                double3 stiltAbsolute = footAbsolute;
                stiltAbsolute.y = terrainHeight + stiltHeight * 0.5f;

                OutputTransforms[outputCount++] = ShinobuPoiMath.CreateTransform(
                    stiltAbsolute,
                    quaternion.identity,
                    new float3(0.45f, stiltHeight, 0.45f),
                    stiltHash,
                    rule.BiomeID,
                    0u);
            }
        }

        private void ClearCounters()
        {
            if (!Counters.IsCreated)
                return;

            int count = math.min(Counters.Length, 4);
            for (int i = 0; i < count; i++)
                Counters[i] = 0;
        }

        private void WriteCounters(int outputCount, int anchorCount, int rejected, uint stateHash)
        {
            if (!Counters.IsCreated)
                return;

            if (Counters.Length > 0)
                Counters[0] = outputCount;
            if (Counters.Length > 1)
                Counters[1] = anchorCount;
            if (Counters.Length > 2)
                Counters[2] = rejected;
            if (Counters.Length > 3)
                Counters[3] = unchecked((int)stateHash);
        }

        private void WriteTelemetry(int generated, int rejected, int anchorCount, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = CandidateAups.IsCreated && CandidateAups.Length > 0 ? CandidateAups[0] : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = generated,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = rejected,
                PlacementComputeTimeMs = 0f,
                Flags = generated > 0 ? ShinobuPoiConstants.FlagOfflineBake | ShinobuPoiConstants.FlagVisualAnchor : ShinobuPoiConstants.FlagRejectedSlope,
                _pad0 = (uint)math.max(0, anchorCount),
                _pad1 = (uint)math.max(0, OutputTransforms.IsCreated ? OutputTransforms.Length : 0),
                _pad2 = (uint)math.max(0, VisualAnchors.IsCreated ? VisualAnchors.Length : 0)
            };
        }
    }

    /// <summary>
    /// Dear-lie reduction pass: distant clusters collapse to one impostor DTO.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiDearLieHlodClusterJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias] public NativeArray<byte> ClusterConsumed;
        [NoAlias] public NativeList<HLOD_ImpostorDTO> OutputImpostors;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public double3 CameraAup;
        public float FarDistanceMeters;
        public float ClusterRadiusMeters;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !OutputImpostors.IsCreated)
                return;

            OutputImpostors.Clear();
            int count = PoiTransforms.Length;
            int maskCount = ClusterConsumed.IsCreated ? ClusterConsumed.Length : 0;
            for (int i = 0; i < maskCount; i++)
                ClusterConsumed[i] = 0;

            float radius = ClusterRadiusMeters > 0f ? ClusterRadiusMeters : ShinobuPoiConstants.HlodClusterRadiusMeters;
            float radiusSq = radius * radius;
            float far = FarDistanceMeters > 0f ? FarDistanceMeters : 450f;
            float farSq = far * far;
            int impostors = 0;

            for (int i = 0; i < count; i++)
            {
                if (OutputImpostors.Length >= OutputImpostors.Capacity)
                    break;
                if (i < maskCount && ClusterConsumed[i] != 0)
                    continue;

                PoiTransformDTO root = PoiTransforms[i];
                if (!ShinobuPoiMath.IsMajorPoi(root))
                    continue;

                if (ShinobuPoiMath.PlanarDistanceSqMeters(root.AUP, CameraAup) < farSq)
                    continue;

                double3 sum = root.AUP;
                int clusterSize = 1;
                for (int j = i + 1; j < count; j++)
                {
                    if (j < maskCount && ClusterConsumed[j] != 0)
                        continue;

                    PoiTransformDTO other = PoiTransforms[j];
                    if (!ShinobuPoiMath.IsMajorPoi(other))
                        continue;

                    if (ShinobuPoiMath.PlanarDistanceSqMeters(other.AUP, root.AUP) > radiusSq)
                        continue;

                    sum += other.AUP;
                    clusterSize++;
                }

                if (clusterSize < 2)
                    continue;

                double3 centroid = sum / clusterSize;
                if (i < maskCount)
                    ClusterConsumed[i] = 1;

                for (int j = i + 1; j < count; j++)
                {
                    if (j >= maskCount || ClusterConsumed[j] != 0)
                        continue;

                    PoiTransformDTO other = PoiTransforms[j];
                    if (!ShinobuPoiMath.IsMajorPoi(other))
                        continue;

                    if (ShinobuPoiMath.PlanarDistanceSqMeters(other.AUP, root.AUP) <= radiusSq)
                        ClusterConsumed[j] = 1;
                }

                OutputImpostors.AddNoResize(new HLOD_ImpostorDTO
                {
                    SectorHash = ShinobuPoiMath.ResolveSectorHash(centroid, ShinobuPoiConstants.SectorMeters),
                    CenterXZ = ShinobuPoiMath.ResolveSectorLocalXZ(centroid, ShinobuPoiConstants.SectorMeters),
                    RadiusMetersQ = (ushort)math.clamp(radius, 1f, 65535f),
                    ImpostorType = 2,
                    Flags = 1
                });
                impostors++;
            }

            WriteTelemetry(impostors);
        }

        private void WriteTelemetry(int impostors)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = CameraAup,
                Frame = Frame,
                StateHash = ShinobuPoiMath.MixHash(ShinobuPoiConstants.SectorHashSeed, (uint)impostors),
                TotalPOIsGenerated = impostors,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagHlodClustered,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Offline ruin pass. Quality weight continuously removes small debris before it reaches rendering.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DebrisScatterJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> MajorPoiTransforms;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias] public NativeList<PoiTransformDTO> DebrisTransforms;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;
        public uint Seed;
        public uint BiomeAgeHash;
        public float GlobalQualityWeight;
        public float ScatterRadiusMeters;

        public void Execute()
        {
            if (!MajorPoiTransforms.IsCreated || !DebrisTransforms.IsCreated)
                return;

            DebrisTransforms.Clear();
            int sourceCount = MajorPoiTransforms.Length;
            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            float quality = math.saturate(GlobalQualityWeight);
            float detailCurve = ShinobuPoiMath.ResolveQualityCurve(quality);
            int written = 0;
            int culled = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;

            for (int i = 0; i < sourceCount; i++)
            {
                if (DebrisTransforms.Length >= DebrisTransforms.Capacity)
                    break;

                PoiTransformDTO major = MajorPoiTransforms[i];
                if (!ShinobuPoiMath.IsMajorPoi(major))
                    continue;

                PoiPlacementRuleDTO rule = ruleCount > 0 ? Rules[FindRuleIndex(major, ruleCount)] : default;
                int maxDebris = rule.MaxDebrisMatrices > 0 ? rule.MaxDebrisMatrices : 50;
                uint baseHash = ShinobuPoiMath.MixHash(ShinobuPoiMath.MixHash(Seed, (uint)i), BiomeAgeHash ^ major.BiomeID);
                uint sectorHash = ShinobuPoiMath.ResolveSectorHash(major.AUP, ShinobuPoiConstants.SectorMeters);
                Unity.Mathematics.Random rng = ShinobuPoiMath.CreateDeterministicRandom(sectorHash, Frame, baseHash);
                int target = math.clamp((int)math.floor(maxDebris * quality + rng.NextFloat()), 0, maxDebris);
                culled += maxDebris - target;

                float radius = ScatterRadiusMeters > 0f
                    ? ScatterRadiusMeters
                    : math.max(6f, rule.ClusterRadiusMeters > 0f ? rule.ClusterRadiusMeters : 24f);
                radius *= math.lerp(0.55f, 1f, detailCurve);
                float age01 = ShinobuPoiMath.HashToUnit01(ShinobuPoiMath.MixHash(BiomeAgeHash, major.BiomeID));
                float2 current = ResolveCurlCurrent(major.AUP, baseHash);
                float2 side = new float2(-current.y, current.x);

                for (int d = 0; d < target; d++)
                {
                    if (DebrisTransforms.Length >= DebrisTransforms.Capacity)
                        break;

                    uint debrisHash = ShinobuPoiMath.MixHash(baseHash, (uint)d);
                    float radial = math.sqrt(rng.NextFloat()) * radius;
                    float smear = radial * math.lerp(0.45f, 1.35f, age01);
                    float lateral = (rng.NextFloat() - 0.5f) * radius * 0.7f;
                    float2 offset = current * smear + side * lateral;
                    double3 debrisAup = major.AUP + new double3(offset.x, 0.0, offset.y);
                    float terrainHeight = MockGradientSampler.SampleHeight(debrisAup, debrisHash);
                    debrisAup.y = terrainHeight + 0.15f;
                    float yaw = rng.NextFloat() * math.PI * 2f;
                    float scale = math.lerp(0.25f, 1.85f, rng.NextFloat());
                    uint debrisPrefab = rule.DebrisPrefabHash != 0u ? rule.DebrisPrefabHash : ShinobuPoiConstants.PrefabHashRustedPanel;

                    DebrisTransforms.AddNoResize(ShinobuPoiMath.CreateTransform(
                        debrisAup,
                        ShinobuPoiMath.RotationYNoTrig(yaw),
                        new float3(scale, math.max(0.04f, scale * 0.08f), scale * 0.55f),
                        debrisPrefab,
                        major.BiomeID,
                        0u));
                    written++;
                    stateHash = ShinobuPoiMath.MixHash(stateHash, debrisHash);
                }
            }

            WriteTelemetry(written, culled, stateHash);
        }

        private int FindRuleIndex(PoiTransformDTO major, int ruleCount)
        {
            for (int i = 0; i < ruleCount; i++)
            {
                PoiPlacementRuleDTO rule = Rules[i];
                if (rule.PrefabHash == major.PrefabHash || rule.BiomeID == major.BiomeID)
                    return i;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 ResolveCurlCurrent(double3 aup, uint seed)
        {
            double sectorMeters = ShinobuPoiConstants.SectorMeters;
            double3 sectorOrigin = new double3(
                math.floor(aup.x / sectorMeters) * sectorMeters,
                0.0,
                math.floor(aup.z / sectorMeters) * sectorMeters);
            float3 local = ShinobuPoiMath.ToLocalFloat3(aup, sectorOrigin);
            float x = local.x * 0.0017f;
            float z = local.z * 0.0013f;
            float phase = ((seed & 1023u) * 0.006135923f)
                + ShinobuPoiMath.HashToUnit01(ShinobuPoiMath.ResolveSectorHash(aup, ShinobuPoiConstants.SectorMeters)) * math.PI * 2f;
            float dPsiDz = -0.00221f * MathLodApproximation.ApproxSinBhaskara(z * 1.7f - phase) + 0.0013f * MathLodApproximation.ApproxCosBhaskara(x * 0.5f + z + phase);
            float dPsiDx = 0.0017f * MathLodApproximation.ApproxCosBhaskara(x + phase) - 0.00085f * MathLodApproximation.ApproxSinBhaskara(x * 0.5f + z + phase);
            return math.normalizesafe(new float2(dPsiDz, -dPsiDx), new float2(1f, 0f));
        }

        private void WriteTelemetry(int written, int culled, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = MajorPoiTransforms.IsCreated && MajorPoiTransforms.Length > 0 ? MajorPoiTransforms[0].AUP : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = written,
                DebrisMatricesCulled = culled,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagDebris,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Deterministic minimum-distance enforcement for major POI solitude.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct NegativeSpacePoiCullJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias] public NativeArray<byte> AliveMask;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public float MinimumDistanceMeters;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !AliveMask.IsCreated)
                return;

            int count = math.min(PoiTransforms.Length, AliveMask.Length);
            for (int i = 0; i < count; i++)
                AliveMask[i] = 1;

            float minDistance = MinimumDistanceMeters > 0f ? MinimumDistanceMeters : ShinobuPoiConstants.NegativeSpaceMeters;
            float minDistanceSq = minDistance * minDistance;
            int culled = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;

            for (int i = 0; i < count; i++)
            {
                if (AliveMask[i] == 0 || !ShinobuPoiMath.IsMajorPoi(PoiTransforms[i]))
                    continue;

                for (int j = i + 1; j < count; j++)
                {
                    if (AliveMask[j] == 0 || !ShinobuPoiMath.IsMajorPoi(PoiTransforms[j]))
                        continue;

                    if (ShinobuPoiMath.PlanarDistanceSqMeters(PoiTransforms[j].AUP, PoiTransforms[i].AUP) > minDistanceSq)
                        continue;

                    uint priorityI = ResolvePriority(PoiTransforms[i]);
                    uint priorityJ = ResolvePriority(PoiTransforms[j]);
                    if (priorityI <= priorityJ)
                    {
                        AliveMask[j] = 0;
                        stateHash = ShinobuPoiMath.MixHash(stateHash, priorityJ);
                    }
                    else
                    {
                        AliveMask[i] = 0;
                        stateHash = ShinobuPoiMath.MixHash(stateHash, priorityI);
                        break;
                    }

                    culled++;
                }
            }

            WriteTelemetry(culled, stateHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolvePriority(PoiTransformDTO dto)
        {
            uint hash = ShinobuPoiMath.MixHash(dto.PrefabHash, dto.BiomeID);
            hash = ShinobuPoiMath.MixHash(hash, MockGradientSampler.HashAup(dto.AUP, dto.QuestNodeHash));
            return hash;
        }

        private void WriteTelemetry(int culled, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = PoiTransforms.IsCreated ? PoiTransforms.Length : 0,
                DebrisMatricesCulled = culled,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = culled > 0 ? ShinobuPoiConstants.FlagNegativeSpaceCulled : 0u,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Registry-exported narrative rules inject quest hashes into already accepted POI matrices.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct NarrativeBeaconInjectionJob : IJob
    {
        [NoAlias] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias, ReadOnly] public NativeArray<NarrativeBeaconRuleDTO> NarrativeRules;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !NarrativeRules.IsCreated)
                return;

            int injected = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            for (int i = 0; i < PoiTransforms.Length; i++)
            {
                PoiTransformDTO dto = PoiTransforms[i];
                if (!ShinobuPoiMath.IsMajorPoi(dto))
                    continue;

                uint selectedQuest = dto.QuestNodeHash;
                uint selectedPriority = selectedQuest != 0u ? 1u : 0u;
                uint sectorHash = ShinobuPoiMath.ResolveSectorHash(dto.AUP, ShinobuPoiConstants.SectorMeters);
                float depthMeters = math.abs((float)dto.AUP.y);
                for (int r = 0; r < NarrativeRules.Length; r++)
                {
                    NarrativeBeaconRuleDTO rule = NarrativeRules[r];
                    if (rule.QuestNodeHash == 0u || !RuleMatches(dto, rule, sectorHash, depthMeters))
                        continue;

                    if (rule.Priority >= selectedPriority)
                    {
                        selectedQuest = rule.QuestNodeHash;
                        selectedPriority = rule.Priority;
                    }
                }

                if (selectedQuest == 0u || selectedQuest == dto.QuestNodeHash)
                    continue;

                dto.QuestNodeHash = selectedQuest;
                PoiTransforms[i] = dto;
                injected++;
                stateHash = ShinobuPoiMath.MixHash(stateHash, selectedQuest);
            }

            WriteTelemetry(injected, stateHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool RuleMatches(PoiTransformDTO dto, NarrativeBeaconRuleDTO rule, uint sectorHash, float depthMeters)
        {
            bool prefabOk = rule.PrefabHash == 0u || rule.PrefabHash == dto.PrefabHash;
            bool biomeOk = rule.BiomeID == 0u || rule.BiomeID == dto.BiomeID;
            bool sectorOk = rule.SectorHash == 0u || rule.SectorHash == sectorHash;
            bool depthOk = rule.MaxDepthMeters <= rule.MinDepthMeters || (depthMeters >= rule.MinDepthMeters && depthMeters <= rule.MaxDepthMeters);
            return prefabOk && biomeOk && sectorOk && depthOk;
        }

        private void WriteTelemetry(int injected, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            double3 lastAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default;
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = lastAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = PoiTransforms.IsCreated ? PoiTransforms.Length : 0,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = injected > 0 ? ShinobuPoiConstants.FlagNarrative : 0u,
                _pad0 = 0u,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Cold sentinel for loading-screen/offline placement bakes. It writes the manifest into the telemetry ring.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiOfflineBakeFenceJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiOfflineBakeConfigDTO> Config;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            PoiOfflineBakeConfigDTO config = Config.IsCreated && Config.Length > 0 ? Config[0] : default;
            uint stateHash = ShinobuPoiMath.MixHash(config.Seed, (uint)math.max(0, config.CandidateCount));
            stateHash = ShinobuPoiMath.MixHash(stateHash, (uint)math.max(0, config.MaxPoiTransforms));
            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = math.max(0, config.MaxPoiTransforms),
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagOfflineBake,
                _pad0 = (uint)math.max(0, config.CandidateCount),
                _pad1 = (uint)math.max(0, config.SectorHashMapCapacity),
                _pad2 = (uint)math.max(0, config.TelemetryRingLength)
            };
        }
    }

    /// <summary>
    /// AUP sector router. Produces contiguous sector blocks and optional multi-hash lookup for streaming.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiSpatialPartitioningJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias] public NativeArray<PoiTransformDTO> SortedTransforms;
        [NoAlias] public NativeArray<PoiChunkRouteDTO> Routes;
        [NoAlias] public NativeArray<PoiSectorRangeDTO> SectorRanges;
        [NoAlias] public NativeArray<int> SectorCounts;
        [NoAlias] public NativeArray<int> SectorWriteCursors;
        [NoAlias] public NativeParallelMultiHashMap<uint, int> SectorToPoiIndices;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public float SectorMeters;
        public int SectorMinX;
        public int SectorMinZ;
        public int SectorStrideX;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !SortedTransforms.IsCreated || !Routes.IsCreated || !SectorCounts.IsCreated || !SectorWriteCursors.IsCreated)
                return;

            int count = math.min(PoiTransforms.Length, math.min(SortedTransforms.Length, Routes.Length));
            int sectorCount = SectorCounts.Length;
            float sectorMeters = SectorMeters > 0f ? SectorMeters : ShinobuPoiConstants.SectorMeters;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            int routed = 0;
            int hashAdds = 0;

            for (int i = 0; i < sectorCount; i++)
            {
                SectorCounts[i] = 0;
                SectorWriteCursors[i] = 0;
                if (SectorRanges.IsCreated && i < SectorRanges.Length)
                    SectorRanges[i] = default;
            }

            if (SectorToPoiIndices.IsCreated)
                SectorToPoiIndices.Clear();

            for (int i = 0; i < count; i++)
            {
                PoiTransformDTO dto = PoiTransforms[i];
                uint sectorHash = ShinobuPoiMath.ResolveSectorHash(dto.AUP, sectorMeters);
                int slot = ResolveSectorSlot(dto.AUP, sectorMeters, sectorCount);
                SectorCounts[slot] = SectorCounts[slot] + 1;
                Routes[i] = new PoiChunkRouteDTO
                {
                    SectorHash = sectorHash,
                    SourceIndex = i,
                    SortedIndex = -1,
                    Flags = ShinobuPoiConstants.FlagSectorRouted,
                    LocalXZ = ResolveLocalXZ(dto.AUP, sectorMeters),
                    PrefabHash = dto.PrefabHash,
                    _pad0 = 0u
                };

                if (SectorToPoiIndices.IsCreated && hashAdds < SectorToPoiIndices.Capacity)
                {
                    SectorToPoiIndices.Add(sectorHash, i);
                    hashAdds++;
                }
            }

            int cursor = 0;
            for (int slot = 0; slot < sectorCount; slot++)
            {
                int slotCount = SectorCounts[slot];
                SectorWriteCursors[slot] = cursor;
                if (SectorRanges.IsCreated && slot < SectorRanges.Length && slotCount > 0)
                {
                    uint sectorHash = ResolveSectorHashFromSlot(slot, sectorMeters);
                    SectorRanges[slot] = new PoiSectorRangeDTO
                    {
                        SectorHash = sectorHash,
                        StartIndex = cursor,
                        Count = slotCount,
                        Flags = ShinobuPoiConstants.FlagSectorRouted,
                        SectorOriginXZ = ResolveSectorOriginFromSlot(slot, sectorMeters)
                    };
                    stateHash = ShinobuPoiMath.MixHash(stateHash, sectorHash);
                }

                cursor += slotCount;
            }

            for (int i = 0; i < count; i++)
            {
                PoiChunkRouteDTO route = Routes[i];
                int slot = ResolveSectorSlot(PoiTransforms[i].AUP, sectorMeters, sectorCount);
                int writeIndex = SectorWriteCursors[slot];
                if ((uint)writeIndex >= (uint)SortedTransforms.Length)
                    continue;

                SortedTransforms[writeIndex] = PoiTransforms[i];
                SectorWriteCursors[slot] = writeIndex + 1;
                route.SortedIndex = writeIndex;
                Routes[i] = route;
                routed++;
            }

            WriteTelemetry(routed, stateHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveSectorSlot(double3 aup, float sectorMeters, int sectorCount)
        {
            if (sectorCount <= 1)
                return 0;

            int stride = math.max(1, SectorStrideX);
            int x = (int)math.floor(aup.x / sectorMeters);
            int z = (int)math.floor(aup.z / sectorMeters);
            int slot = (z - SectorMinZ) * stride + (x - SectorMinX);
            if ((uint)slot < (uint)sectorCount)
                return slot;

            return (int)(ShinobuPoiMath.ResolveSectorHash(aup, sectorMeters) % (uint)sectorCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float2 ResolveLocalXZ(double3 aup, float sectorMeters)
        {
            return ShinobuPoiMath.ResolveSectorLocalXZ(aup, sectorMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint ResolveSectorHashFromSlot(int slot, float sectorMeters)
        {
            int stride = math.max(1, SectorStrideX);
            int x = SectorMinX + slot % stride;
            int z = SectorMinZ + slot / stride;
            return ShinobuPoiMath.ResolveSectorHash(new double3(x * sectorMeters, 0.0, z * sectorMeters), sectorMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double2 ResolveSectorOriginFromSlot(int slot, float sectorMeters)
        {
            int stride = math.max(1, SectorStrideX);
            int x = SectorMinX + slot % stride;
            int z = SectorMinZ + slot / stride;
            return new double2(x * sectorMeters, z * sectorMeters);
        }

        private void WriteTelemetry(int routed, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = routed,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagSectorRouted,
                _pad0 = (uint)(SectorCounts.IsCreated ? SectorCounts.Length : 0),
                _pad1 = (uint)(SectorToPoiIndices.IsCreated ? SectorToPoiIndices.Capacity : 0),
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Produces botany-facing exclusion and moss adhesion masks from base bounds.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct FloraStructureMaskJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias, ReadOnly] public NativeArray<StructuralBoundsDTO> Bounds;
        [NoAlias] public NativeList<FloraStructureMaskDTO> FloraMasks;
        [NoAlias] public NativeParallelMultiHashMap<uint, int> FloraMaskBySector;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public float MossBandMeters;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !FloraMasks.IsCreated)
                return;

            FloraMasks.Clear();
            if (FloraMaskBySector.IsCreated)
                FloraMaskBySector.Clear();

            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            int boundsCount = Bounds.IsCreated ? Bounds.Length : 0;
            int written = 0;
            int hashAdds = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            float mossBand = MossBandMeters > 0f ? MossBandMeters : 2.5f;

            for (int i = 0; i < PoiTransforms.Length; i++)
            {
                if (FloraMasks.Length >= FloraMasks.Capacity)
                    break;

                PoiTransformDTO dto = PoiTransforms[i];
                if (!ShinobuPoiMath.IsMajorPoi(dto))
                    continue;

                PoiPlacementRuleDTO rule = ruleCount > 0 ? Rules[ResolveRuleIndex(dto, ruleCount)] : default;
                int boundsIndex = math.clamp(rule.BoundsIndex, 0, math.max(0, boundsCount - 1));
                StructuralBoundsDTO structuralBounds = boundsCount > 0 ? Bounds[boundsIndex] : default;
                float3 extents = math.max(structuralBounds.Extents, new float3(2f, 0.5f, 2f));
                float radius = math.max(structuralBounds.ClearanceRadius, math.length(new float2(extents.x, extents.z)));
                uint sectorHash = ShinobuPoiMath.ResolveSectorHash(dto.AUP, ShinobuPoiConstants.SectorMeters);

                FloraMasks.AddNoResize(new FloraStructureMaskDTO
                {
                    CenterAup = dto.AUP,
                    HalfExtentsXZ = new float2(extents.x, extents.z),
                    ExclusionRadiusMeters = radius,
                    MossInnerRadiusMeters = math.max(0f, radius - mossBand),
                    MossOuterRadiusMeters = radius + mossBand,
                    AdhesionWeight = math.saturate(0.35f + extents.y * 0.08f),
                    SectorHash = sectorHash,
                    SourcePoiIndex = (uint)i,
                    Flags = ShinobuPoiConstants.FlagFloraExclusion | ShinobuPoiConstants.FlagMossAdhesion,
                    _pad0 = 0u
                });

                if (FloraMaskBySector.IsCreated && hashAdds < FloraMaskBySector.Capacity)
                {
                    FloraMaskBySector.Add(sectorHash, written);
                    hashAdds++;
                }

                written++;
                stateHash = ShinobuPoiMath.MixHash(stateHash, sectorHash);
            }

            WriteTelemetry(written, stateHash);
        }

        private int ResolveRuleIndex(PoiTransformDTO dto, int ruleCount)
        {
            for (int i = 0; i < ruleCount; i++)
            {
                PoiPlacementRuleDTO rule = Rules[i];
                if (rule.PrefabHash == dto.PrefabHash || rule.BiomeID == dto.BiomeID)
                    return i;
            }

            return 0;
        }

        private void WriteTelemetry(int written, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = written,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagFloraExclusion | ShinobuPoiConstants.FlagMossAdhesion,
                _pad0 = (uint)(FloraMaskBySector.IsCreated ? FloraMaskBySector.Capacity : 0),
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// Builds renderer-facing AABB proxies from POI matrices. The renderer owns BRG/Graphics calls; SHINOBU only emits math rows.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiRendererCullProxyBuildJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias, ReadOnly] public NativeArray<PoiPlacementRuleDTO> Rules;
        [NoAlias, ReadOnly] public NativeArray<StructuralBoundsDTO> Bounds;
        [NoAlias] public NativeArray<PoiRendererCullProxyDTO> CullProxies;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated || !CullProxies.IsCreated)
                return;

            int count = math.min(PoiTransforms.Length, CullProxies.Length);
            int ruleCount = Rules.IsCreated ? Rules.Length : 0;
            int boundsCount = Bounds.IsCreated ? Bounds.Length : 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;

            for (int i = 0; i < count; i++)
            {
                PoiTransformDTO dto = PoiTransforms[i];
                float3 extents = math.max(dto.Scale * 0.5f, new float3(0.25f, 0.25f, 0.25f));
                if (ShinobuPoiMath.IsMajorPoi(dto) && ruleCount > 0 && boundsCount > 0)
                {
                    PoiPlacementRuleDTO rule = Rules[ResolveRuleIndex(dto, ruleCount)];
                    int boundsIndex = math.clamp(rule.BoundsIndex, 0, boundsCount - 1);
                    extents = math.max(Bounds[boundsIndex].Extents, extents);
                }

                float radius = math.max(0.5f, math.length(extents));
                uint sectorHash = ShinobuPoiMath.ResolveSectorHash(dto.AUP, ShinobuPoiConstants.SectorMeters);
                CullProxies[i] = new PoiRendererCullProxyDTO
                {
                    CenterAup = dto.AUP,
                    Extents = extents,
                    RadiusMeters = radius,
                    SourceIndex = (uint)i,
                    SectorHash = sectorHash,
                    PrefabHash = dto.PrefabHash,
                    Flags = 0u,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                stateHash = ShinobuPoiMath.MixHash(stateHash, sectorHash);
            }

            WriteTelemetry(count, stateHash);
        }

        private int ResolveRuleIndex(PoiTransformDTO dto, int ruleCount)
        {
            for (int i = 0; i < ruleCount; i++)
            {
                PoiPlacementRuleDTO rule = Rules[i];
                if (rule.PrefabHash == dto.PrefabHash || rule.BiomeID == dto.BiomeID)
                    return i;
            }

            return 0;
        }

        private void WriteTelemetry(int count, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = count,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagIndirectArgs,
                _pad0 = (uint)(CullProxies.IsCreated ? CullProxies.Length : 0),
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }

    /// <summary>
    /// CPU-side HZB cull contract. Consumes a renderer-downloaded depth pyramid and writes a visible mask for BRG/indirect draw.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiHzbOcclusionCullJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiRendererCullProxyDTO> CullProxies;
        [NoAlias, ReadOnly] public NativeArray<float> HzbDepthPyramid;
        [NoAlias] public NativeArray<byte> VisibleMask;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public double3 CameraAup;
        public float3 CameraForward;
        public float3 CameraRight;
        public float3 CameraUp;
        public float NearMeters;
        public float FarMeters;
        public float Aspect;
        public float InvTanHalfFovY;
        public float DepthBiasMeters;
        public float GlobalQualityWeight;
        public int HzbWidth;
        public int HzbHeight;
        public int HzbMipOffset;
        public int HzbMipLength;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!CullProxies.IsCreated || !VisibleMask.IsCreated)
                return;

            int count = math.min(CullProxies.Length, VisibleMask.Length);
            bool hasHzb = HzbDepthPyramid.IsCreated && HzbWidth > 0 && HzbHeight > 0 && HzbMipOffset >= 0 && HzbMipLength > 0;
            float3 forward = math.normalizesafe(CameraForward, new float3(0f, 0f, 1f));
            float3 right = math.normalizesafe(CameraRight, new float3(1f, 0f, 0f));
            float3 up = math.normalizesafe(CameraUp, new float3(0f, 1f, 0f));
            float nearMeters = math.max(0.01f, NearMeters);
            float farMeters = FarMeters > nearMeters ? FarMeters : 2500f;
            float aspect = math.max(0.1f, Aspect);
            float invTanY = InvTanHalfFovY > 0f ? InvTanHalfFovY : 1f;
            float invTanX = invTanY / aspect;
            float depthBias = math.max(0f, DepthBiasMeters);
            float quality = math.saturate(GlobalQualityWeight);
            int tapCount = 1 + (int)math.floor(ShinobuPoiMath.ResolveQualityCurve(quality) * 4f);
            int visible = 0;
            int culled = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;

            for (int i = 0; i < count; i++)
            {
                PoiRendererCullProxyDTO proxy = CullProxies[i];
                float3 local = ShinobuPoiMath.ToLocalFloat3(proxy.CenterAup, CameraAup);
                float depth = math.dot(local, forward);
                float safeDepth = math.max(depth, 0.0001f);
                float radius = math.max(0.25f, proxy.RadiusMeters);
                bool inDepth = depth + radius >= nearMeters && depth - radius <= farMeters;
                float ndcX = 0.5f + 0.5f * (math.dot(local, right) * invTanX / safeDepth);
                float ndcY = 0.5f + 0.5f * (math.dot(local, up) * invTanY / safeDepth);
                bool inFrustum = inDepth && ndcX >= -0.05f && ndcX <= 1.05f && ndcY >= -0.05f && ndcY <= 1.05f;
                bool occluded = inFrustum && hasHzb && IsOccludedByHzb(ndcX, ndcY, depth, radius, depthBias, tapCount);
                byte isVisible = (byte)(inFrustum && !occluded ? 1 : 0);
                VisibleMask[i] = isVisible;
                visible += isVisible;
                culled += isVisible == 0 ? 1 : 0;
                if (occluded)
                    stateHash = ShinobuPoiMath.MixHash(stateHash, proxy.SourceIndex);
            }

            WriteTelemetry(visible, culled, stateHash);
        }

        private bool IsOccludedByHzb(float ndcX, float ndcY, float depth, float radius, float depthBias, int tapCount)
        {
            int occludedTaps = 0;
            int taps = math.clamp(tapCount, 1, 5);
            for (int tap = 0; tap < taps; tap++)
            {
                int2 offset = ResolveTapOffset(tap);
                int x = math.clamp((int)math.floor(ndcX * HzbWidth) + offset.x, 0, HzbWidth - 1);
                int y = math.clamp((int)math.floor(ndcY * HzbHeight) + offset.y, 0, HzbHeight - 1);
                int sampleIndex = HzbMipOffset + y * HzbWidth + x;
                if ((uint)(sampleIndex - HzbMipOffset) >= (uint)HzbMipLength || (uint)sampleIndex >= (uint)HzbDepthPyramid.Length)
                    return false;

                float occluderDepth = HzbDepthPyramid[sampleIndex];
                if (!math.isfinite(occluderDepth) || occluderDepth <= 0f)
                    return false;

                if (depth - radius > occluderDepth + depthBias)
                    occludedTaps++;
            }

            return occludedTaps == taps;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 ResolveTapOffset(int tap)
        {
            switch (tap)
            {
                case 1: return new int2(-1, 0);
                case 2: return new int2(1, 0);
                case 3: return new int2(0, -1);
                case 4: return new int2(0, 1);
                default: return new int2(0, 0);
            }
        }

        private void WriteTelemetry(int visible, int culled, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = CameraAup,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = visible,
                DebrisMatricesCulled = culled,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagHzbOccluded,
                _pad0 = (uint)(HzbDepthPyramid.IsCreated ? HzbDepthPyramid.Length : 0),
                _pad1 = (uint)math.max(0, HzbWidth),
                _pad2 = (uint)math.max(0, HzbHeight)
            };
        }
    }

    /// <summary>
    /// Writes DrawProceduralIndirect-compatible argument rows from the HZB-visible mask.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiIndirectDrawArgsJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiRendererCullProxyDTO> CullProxies;
        [NoAlias, ReadOnly] public NativeArray<byte> VisibleMask;
        [NoAlias] public NativeArray<PoiIndirectDrawArgsDTO> IndirectArgs;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        public uint VertexCountPerInstance;
        public uint StartVertex;
        public uint StartInstance;
        public uint PrefabHash;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!IndirectArgs.IsCreated || IndirectArgs.Length <= 0)
                return;

            int count = CullProxies.IsCreated ? CullProxies.Length : 0;
            int maskCount = VisibleMask.IsCreated ? VisibleMask.Length : 0;
            int visible = 0;
            int culled = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            for (int i = 0; i < count; i++)
            {
                bool draw = maskCount <= i || VisibleMask[i] != 0;
                if (draw)
                {
                    visible++;
                    stateHash = ShinobuPoiMath.MixHash(stateHash, CullProxies[i].SourceIndex);
                }
                else
                {
                    culled++;
                }
            }

            IndirectArgs[0] = new PoiIndirectDrawArgsDTO
            {
                VertexCountPerInstance = VertexCountPerInstance,
                InstanceCount = (uint)visible,
                StartVertex = StartVertex,
                StartInstance = StartInstance,
                VisibleCount = (uint)visible,
                CulledCount = (uint)culled,
                PrefabHash = PrefabHash,
                Flags = ShinobuPoiConstants.FlagIndirectArgs,
                _pad0 = 0UL,
                _pad1 = 0UL,
                _pad2 = 0UL,
                _pad3 = 0UL
            };

            WriteTelemetry(visible, culled, stateHash);
        }

        private void WriteTelemetry(int visible, int culled, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = CullProxies.IsCreated && CullProxies.Length > 0 ? CullProxies[0].CenterAup : default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = visible,
                DebrisMatricesCulled = culled,
                TopologyRejectionWarnings = 0,
                PlacementComputeTimeMs = 0f,
                Flags = ShinobuPoiConstants.FlagIndirectArgs,
                _pad0 = VertexCountPerInstance,
                _pad1 = StartVertex,
                _pad2 = StartInstance
            };
        }
    }

    /// <summary>
    /// NaN tripwire for the fixed 300-frame black-box ring. File dumping is performed by the cold bridge.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct PoiBlackBoxValidationJob : IJob
    {
        [NoAlias, ReadOnly] public NativeArray<PoiTransformDTO> PoiTransforms;
        [NoAlias] public NativeArray<PoiPlacementTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> DumpRequest;
        public uint TelemetryIndex;
        public uint Frame;

        public void Execute()
        {
            if (!PoiTransforms.IsCreated)
                return;

            int invalid = 0;
            uint stateHash = ShinobuPoiConstants.SectorHashSeed;
            for (int i = 0; i < PoiTransforms.Length; i++)
            {
                PoiTransformDTO dto = PoiTransforms[i];
                bool valid = math.all(math.isfinite(dto.AUP))
                    && math.all(math.isfinite(dto.Rotation.value))
                    && math.all(math.isfinite(dto.Scale));
                if (valid)
                    continue;

                invalid++;
                stateHash = ShinobuPoiMath.MixHash(stateHash, (uint)i);
            }

            if (DumpRequest.IsCreated && DumpRequest.Length > 0)
                DumpRequest[0] = invalid > 0 ? 1 : 0;

            WriteTelemetry(invalid, stateHash);
        }

        private void WriteTelemetry(int invalid, uint stateHash)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int index = (int)(TelemetryIndex % (uint)TelemetryRing.Length);
            TelemetryRing[index] = new PoiPlacementTelemetryEntry
            {
                LastRootAup = PoiTransforms.IsCreated && PoiTransforms.Length > 0 ? PoiTransforms[0].AUP : default,
                Frame = Frame,
                StateHash = stateHash,
                TotalPOIsGenerated = PoiTransforms.IsCreated ? PoiTransforms.Length : 0,
                DebrisMatricesCulled = 0,
                TopologyRejectionWarnings = invalid,
                PlacementComputeTimeMs = 0f,
                Flags = invalid > 0 ? ShinobuPoiConstants.FlagRejectedSlope : 0u,
                _pad0 = (uint)ShinobuPoiVaultBridge.BlackBoxFrameCount,
                _pad1 = 0u,
                _pad2 = 0u
            };
        }
    }
}
