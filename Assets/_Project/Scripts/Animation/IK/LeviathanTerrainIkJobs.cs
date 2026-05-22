using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.IK
{
    public static class LeviathanTerrainIkConstants
    {
        public const int MaxSegments = 20;
        public const int LowTierSegments = 8;
        public const int FallbackMockBoneCount = 10;
        public const int TerrainHugSegmentCount = 5;
        public const int TelemetryCapacity = 300;
        public const float DefaultSegmentLength = 2.5f;
        public const float MinSegmentLength = 0.05f;
        public const float MinTerrainSize = 0.0001f;
        public const uint TelemetryFlagActive = 1u << 0;
        public const uint TelemetryFlagSdf = 1u << 1;
        public const uint TelemetryFlagMapMagic = 1u << 2;
        public const uint TelemetryFlagTailWhip = 1u << 3;
        public const uint TelemetryFlagInvalid = 1u << 31;
        public const uint RuntimeFlagSdfHugging = 1u << 0;
        public const uint RuntimeFlagTerrainFallback = 1u << 1;
    }

    public static class LeviathanTerrainIkLayout
    {
        public const int BoneDtoBytes = 64;
        public const int BoneConstraintDtoBytes = 16;
        public const int ColliderProxyDtoBytes = 64;
        public const int TelemetryEntryBytes = 96;
        public const int MockTargetDtoBytes = 32;
        public const int TunerSnapshotBytes = 16;
        private static readonly int s_boneLocalToWorldOffset = FieldOffset<LeviathanBoneDTO>(nameof(LeviathanBoneDTO.LocalToWorld));
        private static readonly int s_constraintParentIndexOffset = FieldOffset<LeviathanBoneConstraintsDTO>(nameof(LeviathanBoneConstraintsDTO.ParentIndex));
        private static readonly int s_constraintChainIdOffset = FieldOffset<LeviathanBoneConstraintsDTO>(nameof(LeviathanBoneConstraintsDTO.ChainId));
        private static readonly int s_constraintFlagsOffset = FieldOffset<LeviathanBoneConstraintsDTO>(nameof(LeviathanBoneConstraintsDTO.Flags));
        private static readonly int s_constraintSegmentLengthOffset = FieldOffset<LeviathanBoneConstraintsDTO>(nameof(LeviathanBoneConstraintsDTO.SegmentLengthMeters));
        private static readonly int s_constraintMaxBendOffset = FieldOffset<LeviathanBoneConstraintsDTO>(nameof(LeviathanBoneConstraintsDTO.MaxBendRadians));
        private static readonly int s_colliderCenterOffset = FieldOffset<LeviathanCapsuleColliderDTO>(nameof(LeviathanCapsuleColliderDTO.Center));
        private static readonly int s_colliderAxisOffset = FieldOffset<LeviathanCapsuleColliderDTO>(nameof(LeviathanCapsuleColliderDTO.Axis));
        private static readonly int s_colliderAabbExtentsOffset = FieldOffset<LeviathanCapsuleColliderDTO>(nameof(LeviathanCapsuleColliderDTO.AabbExtents));
        private static readonly int s_telemetryRootAupOffset = FieldOffset<LeviathanTerrainIkTelemetryEntry>(nameof(LeviathanTerrainIkTelemetryEntry.RootAup));
        private static readonly int s_telemetryBurstSolveMicrosOffset = FieldOffset<LeviathanTerrainIkTelemetryEntry>(nameof(LeviathanTerrainIkTelemetryEntry.BurstSolveMicros));
        private static readonly int s_mockTargetAupOffset = FieldOffset<LeviathanMockTargetDTO>(nameof(LeviathanMockTargetDTO.TargetAup));

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<LeviathanBoneDTO>() == BoneDtoBytes &&
                   s_boneLocalToWorldOffset == 0 &&
                   UnsafeUtility.SizeOf<LeviathanBoneConstraintsDTO>() == BoneConstraintDtoBytes &&
                   s_constraintParentIndexOffset == 0 &&
                   s_constraintChainIdOffset == 4 &&
                   s_constraintFlagsOffset == 6 &&
                   s_constraintSegmentLengthOffset == 8 &&
                   s_constraintMaxBendOffset == 12 &&
                   UnsafeUtility.SizeOf<LeviathanCapsuleColliderDTO>() == ColliderProxyDtoBytes &&
                   s_colliderCenterOffset == 0 &&
                   s_colliderAxisOffset == 16 &&
                   s_colliderAabbExtentsOffset == 48 &&
                   UnsafeUtility.SizeOf<LeviathanTerrainIkTelemetryEntry>() == TelemetryEntryBytes &&
                   s_telemetryRootAupOffset == 64 &&
                   s_telemetryBurstSolveMicrosOffset == 92 &&
                   UnsafeUtility.SizeOf<LeviathanMockTargetDTO>() == MockTargetDtoBytes &&
                   s_mockTargetAupOffset == 0 &&
                   UnsafeUtility.SizeOf<LeviathanProceduralTunerSnapshot>() == TunerSnapshotBytes;
        }

        private static int FieldOffset<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct LeviathanProceduralTunerSnapshot
    {
        [FieldOffset(0)] public int ActiveSegmentCount;
        [FieldOffset(4)] public int ConstraintIterations;
        [FieldOffset(8)] public float BurstSolveMicros;
        [FieldOffset(12)] public float GlobalQualityWeight;
    }

    public interface ILeviathanProceduralTunerSource
    {
        void GetLeviathanProceduralTunerSnapshot(out LeviathanProceduralTunerSnapshot snapshot);
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LeviathanBoneDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct LeviathanBoneConstraintsDTO
    {
        [FieldOffset(0)] public int ParentIndex;
        [FieldOffset(4)] public ushort ChainId;
        [FieldOffset(6)] public ushort Flags;
        [FieldOffset(8)] public float SegmentLengthMeters;
        [FieldOffset(12)] public float MaxBendRadians;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LeviathanCapsuleColliderDTO
    {
        [FieldOffset(0)] public float3 Center;
        [FieldOffset(12)] public float Radius;
        [FieldOffset(16)] public float3 Axis;
        [FieldOffset(28)] public float HalfHeight;
        [FieldOffset(32)] public uint OwnerHash;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public int BoneIndex;
        [FieldOffset(44)] public int FrameIndex;
        [FieldOffset(48)] public float3 AabbExtents;
        [FieldOffset(60)] public uint Padding0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct LeviathanTerrainIkTelemetryEntry
    {
        [FieldOffset(0)] public int FrameIndex;
        [FieldOffset(4)] public int ActiveSegmentCount;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint StateHash;
        [FieldOffset(16)] public float3 HeadPosition;
        [FieldOffset(28)] public float3 TailPosition;
        [FieldOffset(40)] public float3 IntendedVelocity;
        [FieldOffset(52)] public float MaxTerrainPushMeters;
        [FieldOffset(56)] public float TailWhipSecondsRemaining;
        [FieldOffset(60)] public float GlobalQualityWeight;
        [FieldOffset(64)] public double3 RootAup;
        [FieldOffset(88)] public float AverageFabrikIterations;
        [FieldOffset(92)] public float BurstSolveMicros;
    }

    public static class LeviathanTerrainIkBlackBox
    {
        public const string DefaultDumpPath = "Docs/AgentLogs/Dump_LEVIATHAN_RIGGER.bin";

        public static bool TryDumpTelemetry(
            string path,
            NativeArray<LeviathanTerrainIkTelemetryEntry>.ReadOnly telemetryRing,
            NativeArray<int>.ReadOnly telemetryCursor)
        {
            if (string.IsNullOrEmpty(path) ||
                !LeviathanTerrainIkLayout.Validate() ||
                !telemetryRing.IsCreated ||
                telemetryRing.Length < LeviathanTerrainIkConstants.TelemetryCapacity ||
                !telemetryCursor.IsCreated ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    int cursor = telemetryCursor[0];
                    int ringLength = telemetryRing.Length;
                    int dumpCount = LeviathanTerrainIkConstants.TelemetryCapacity;
                    int startIndex = cursor >= dumpCount
                        ? PositiveModulo(cursor - dumpCount, ringLength)
                        : 0;

                    writer.Write(0x4C54494Bu);
                    writer.Write(1u);
                    writer.Write(LeviathanTerrainIkLayout.TelemetryEntryBytes);
                    writer.Write(dumpCount);
                    writer.Write(cursor);
                    for (int i = 0; i < dumpCount; i++)
                    {
                        int sourceIndex = PositiveModulo(startIndex + i, ringLength);
                        WriteEntry(writer, telemetryRing[sourceIndex]);
                    }
                }

                return true;
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
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public static bool TryDumpTelemetryOnFault(
            string path,
            NativeArray<LeviathanTerrainIkTelemetryEntry>.ReadOnly telemetryRing,
            NativeArray<int>.ReadOnly telemetryCursor)
        {
            if (!telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                telemetryRing.Length < LeviathanTerrainIkConstants.TelemetryCapacity ||
                telemetryCursor.Length <= 0)
            {
                return false;
            }

            int cursor = telemetryCursor[0];
            if (cursor < 0)
                return TryDumpTelemetry(path, telemetryRing, telemetryCursor);
            if (cursor == 0)
                return false;

            int lastIndex = PositiveModulo(cursor - 1, telemetryRing.Length);
            return (telemetryRing[lastIndex].Flags & LeviathanTerrainIkConstants.TelemetryFlagInvalid) != 0u &&
                   TryDumpTelemetry(path, telemetryRing, telemetryCursor);
        }

        private static int PositiveModulo(int value, int length)
        {
            int safeLength = Math.Max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }

        private static void WriteEntry(BinaryWriter writer, LeviathanTerrainIkTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.ActiveSegmentCount);
            writer.Write(entry.Flags);
            writer.Write(entry.StateHash);
            WriteFloat3(writer, entry.HeadPosition);
            WriteFloat3(writer, entry.TailPosition);
            WriteFloat3(writer, entry.IntendedVelocity);
            writer.Write(entry.MaxTerrainPushMeters);
            writer.Write(entry.TailWhipSecondsRemaining);
            writer.Write(entry.GlobalQualityWeight);
            WriteDouble3(writer, entry.RootAup);
            writer.Write(entry.AverageFabrikIterations);
            writer.Write(entry.BurstSolveMicros);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }

        private static void WriteDouble3(BinaryWriter writer, double3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
        }
    }

    public static class LeviathanTerrainIkVault
    {
        private static bool TryResolveBuffers(
            IDataVault vault,
            int requestedSegmentCapacity,
            int requestedSdfVoxelCount,
            int requestedTerrainSampleCount,
            out NativeArray<float3> segmentPositions,
            out NativeArray<float3> previousSegmentPositions,
            out NativeArray<LeviathanBoneDTO> leviathanBones,
            out NativeArray<LeviathanBoneConstraintsDTO> boneConstraints,
            out NativeArray<LeviathanCapsuleColliderDTO> colliderProxies,
            out NativeArray<LeviathanTerrainIkTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor,
            out NativeArray<byte> voxelSdfTexture3D,
            out NativeArray<ushort> terrainHeightSamples)
        {
            segmentPositions = default;
            previousSegmentPositions = default;
            leviathanBones = default;
            boneConstraints = default;
            colliderProxies = default;
            telemetryRing = default;
            telemetryCursor = default;
            voxelSdfTexture3D = default;
            terrainHeightSamples = default;

            if (vault == null)
                return false;
            if (!LeviathanTerrainIkLayout.Validate())
                return false;

            int segmentCapacity = math.clamp(requestedSegmentCapacity, 2, LeviathanTerrainIkConstants.MaxSegments);
            int sdfVoxelCount = math.max(0, requestedSdfVoxelCount);
            int terrainSampleCount = math.max(0, requestedTerrainSampleCount);

            if (!TryResolveLane(vault, BufferID.LeviathanSegmentPositions, segmentCapacity, NativeArrayOptions.ClearMemory, out segmentPositions) ||
                !TryResolveLane(vault, BufferID.LeviathanPreviousSegmentPositions, segmentCapacity, NativeArrayOptions.ClearMemory, out previousSegmentPositions) ||
                !TryResolveLane(vault, BufferID.LeviathanBoneMatrices, segmentCapacity, NativeArrayOptions.ClearMemory, out leviathanBones) ||
                !TryResolveLane(vault, BufferID.LeviathanProceduralBoneConstraints, segmentCapacity, NativeArrayOptions.UninitializedMemory, out boneConstraints) ||
                !TryResolveLane(vault, BufferID.LeviathanCreatureColliderProxies, segmentCapacity, NativeArrayOptions.UninitializedMemory, out colliderProxies) ||
                !TryResolveLane(vault, BufferID.LeviathanTerrainIkTelemetryRing, LeviathanTerrainIkConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory, out telemetryRing) ||
                !TryResolveLane(vault, BufferID.LeviathanTerrainIkTelemetryCursor, 1, NativeArrayOptions.ClearMemory, out telemetryCursor) ||
                (sdfVoxelCount > 0 && !TryResolveLane(vault, BufferID.VoxelSdfTexture3D, sdfVoxelCount, NativeArrayOptions.UninitializedMemory, out voxelSdfTexture3D)) ||
                (terrainSampleCount > 0 && !TryResolveLane(vault, BufferID.TerrainSeamHeightmap, terrainSampleCount, NativeArrayOptions.UninitializedMemory, out terrainHeightSamples)))
            {
                segmentPositions = default;
                previousSegmentPositions = default;
                leviathanBones = default;
                boneConstraints = default;
                colliderProxies = default;
                telemetryRing = default;
                telemetryCursor = default;
                voxelSdfTexture3D = default;
                terrainHeightSamples = default;
                return false;
            }

            return segmentPositions.IsCreated &&
                   previousSegmentPositions.IsCreated &&
                   leviathanBones.IsCreated &&
                   boneConstraints.IsCreated &&
                   colliderProxies.IsCreated &&
                   telemetryRing.IsCreated &&
                   telemetryCursor.IsCreated &&
                   segmentPositions.Length >= segmentCapacity &&
                   previousSegmentPositions.Length >= segmentCapacity &&
                   leviathanBones.Length >= segmentCapacity &&
                   boneConstraints.Length >= segmentCapacity &&
                   colliderProxies.Length >= segmentCapacity &&
                   telemetryRing.Length >= LeviathanTerrainIkConstants.TelemetryCapacity &&
                   telemetryCursor.Length >= 1 &&
                   (sdfVoxelCount == 0 || (voxelSdfTexture3D.IsCreated && voxelSdfTexture3D.Length >= sdfVoxelCount)) &&
                   (terrainSampleCount == 0 || (terrainHeightSamples.IsCreated && terrainHeightSamples.Length >= terrainSampleCount));
        }

        private static bool TryResolveLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            VaultGenerationHandle<T> handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.AnimationFauna,
                options);
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, OptimizeFor = OptimizeFor.Performance)]
    public struct LeviathanTerrainIkJob : IJob
    {
        private const float MinLengthSq = 0.000001f;
        private const float InvEncodedByteMax = 0.0039215686274509803f;

        [NoAlias] public NativeArray<float3> SegmentPositions;
        [NoAlias] public NativeArray<float3> PreviousSegmentPositions;
        [NoAlias] public NativeArray<LeviathanBoneDTO> LeviathanBones;
        [ReadOnly, NoAlias] public NativeArray<LeviathanBoneConstraintsDTO> BoneConstraints;
        [NoAlias] public NativeArray<LeviathanCapsuleColliderDTO> ColliderProxies;
        [NoAlias] public NativeArray<LeviathanTerrainIkTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly VoxelSdfTexture3D;
        [ReadOnly, NoAlias] public NativeArray<ushort> TerrainHeightSamples;
        public int3 VoxelSdfDimensions;
        public float3 VoxelSdfOrigin;
        public float3 VoxelSdfCellSize;
        public float VoxelSdfRange;
        public float3 TerrainOrigin;
        public float3 TerrainSize;
        public int TerrainResolution;
        public float DeltaTime;
        public float Damping;
        public float SegmentLength;
        public float BodyRadius;
        public float SwimWaveFrequencyHz;
        public float SwimWaveAmplitudeMeters;
        public float FabrikToleranceMeters;
        public float TerrainClearance;
        public float TailWhipSecondsRemaining;
        public float TailWhipDurationSeconds;
        public float TailWhipAmplitudeMeters;
        public float GlobalQualityWeight;
        public float3 HeadTargetPosition;
        public float3 IntendedVelocity;
        public float3 OwnerForward;
        public float3 WorldUp;
        public int RequestedSegmentCount;
        public int ConstraintIterations;
        public int FrameIndex;
        public float BurstSolveMicros;
        public double3 RootAup;
        public uint RuntimeFlags;

        public void Execute()
        {
            if (!SegmentPositions.IsCreated ||
                !PreviousSegmentPositions.IsCreated ||
                !LeviathanBones.IsCreated ||
                SegmentPositions.Length < 2 ||
                PreviousSegmentPositions.Length < 2 ||
                LeviathanBones.Length < 2)
            {
                return;
            }

            int maxUsableSegments = math.min(LeviathanTerrainIkConstants.MaxSegments, math.min(SegmentPositions.Length, LeviathanBones.Length));
            const float qualityWeight = 1f;
            const float qualityCurve = 1f;
            int requested = RequestedSegmentCount;
            int activeCount = math.clamp(requested, 2, maxUsableSegments);
            int iterations = math.clamp(math.max(1, ConstraintIterations), 1, 10);
            float dt = math.select(0f, math.min(DeltaTime, 0.05f), math.isfinite(DeltaTime) && DeltaTime > 0f);
            float damping = SanitizeFiniteClamp(Damping, 0.87f, 0f, 1f);
            float segmentLength = SanitizePositiveFinite(SegmentLength, LeviathanTerrainIkConstants.DefaultSegmentLength, LeviathanTerrainIkConstants.MinSegmentLength);
            float bodyRadius = SanitizePositiveFinite(BodyRadius, 1.15f, 0.01f);
            float swimFrequency = SanitizePositiveFinite(SwimWaveFrequencyHz, 0.55f, 0.01f);
            float swimAmplitude = SanitizePositiveFinite(SwimWaveAmplitudeMeters, 1.1f, 0f);
            float clearance = SanitizePositiveFinite(TerrainClearance, 0f, 0f);
            float tailWhipSecondsRemaining = SanitizePositiveFinite(TailWhipSecondsRemaining, 0f, 0f);
            float tailWhipDurationSeconds = SanitizePositiveFinite(TailWhipDurationSeconds, 1f, 0.1f);
            float tailWhipAmplitudeMeters = SanitizePositiveFinite(TailWhipAmplitudeMeters, 0f, 0f);
            float3 ownerForward = NormalizeSafe(OwnerForward, new float3(0f, 0f, 1f));
            float3 up = NormalizeSafe(WorldUp, new float3(0f, 1f, 0f));
            float3 intended = SanitizeFinite(IntendedVelocity, float3.zero);
            float maxTerrainPush = 0f;
            uint telemetryFlags = LeviathanTerrainIkConstants.TelemetryFlagActive;

            MoveHead(dt, segmentLength, intended, ownerForward);
            IntegrateFollowers(activeCount, dt, damping, intended, ownerForward, up, swimFrequency, swimAmplitude, qualityCurve);

            for (int iteration = 0; iteration < iterations; iteration++)
                PullDistanceConstraints(activeCount, segmentLength, ownerForward);

            if (tailWhipSecondsRemaining > 0f)
            {
                telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagTailWhip;
                ApplyTailWhip(activeCount, segmentLength, ownerForward, up, tailWhipSecondsRemaining, tailWhipDurationSeconds, tailWhipAmplitudeMeters);
                PullDistanceConstraints(activeCount, segmentLength, ownerForward);
            }

            float sdfRange = SanitizePositiveFinite(VoxelSdfRange, 0f, 0f);
            float3 sdfCellSize = SanitizePositiveFinite(VoxelSdfCellSize, new float3(0.0001f), new float3(0.0001f));
            float3 sdfGradientStep = math.max(sdfCellSize, new float3(0.05f));
            bool canUseSdf = (RuntimeFlags & LeviathanTerrainIkConstants.RuntimeFlagSdfHugging) != 0u &&
                             VoxelSdfTexture3D.IsCreated &&
                             math.all(math.isfinite(VoxelSdfOrigin)) &&
                             math.all(math.isfinite(sdfCellSize)) &&
                             TryResolveSdfVoxelCount(VoxelSdfDimensions, out int expectedSdfLength) &&
                             VoxelSdfTexture3D.Length >= expectedSdfLength &&
                             sdfRange > 0.0001f;
            float3 sdfInvCellSize = canUseSdf
                ? math.rcp(sdfCellSize)
                : float3.zero;
            bool canUseHeight = (RuntimeFlags & LeviathanTerrainIkConstants.RuntimeFlagTerrainFallback) != 0u &&
                                TerrainHeightSamples.IsCreated &&
                                TryResolveTerrainHeightSampleCount(TerrainResolution, out int expectedTerrainLength) &&
                                TerrainHeightSamples.Length >= expectedTerrainLength &&
                                math.all(math.isfinite(TerrainOrigin)) &&
                                math.all(math.isfinite(TerrainSize)) &&
                                TerrainSize.x > LeviathanTerrainIkConstants.MinTerrainSize &&
                                TerrainSize.y > LeviathanTerrainIkConstants.MinTerrainSize &&
                                TerrainSize.z > LeviathanTerrainIkConstants.MinTerrainSize;

            int terrainStart = math.max(0, activeCount - LeviathanTerrainIkConstants.TerrainHugSegmentCount);
            for (int index = terrainStart; index < activeCount; index++)
            {
                bool tailBypass = tailWhipSecondsRemaining > 0f && index >= activeCount >> 1;
                if (tailBypass)
                    continue;

                float3 fallbackPosition = index > 0
                    ? SanitizeFinite(SegmentPositions[index - 1], float3.zero) - ownerForward * segmentLength
                    : float3.zero;
                float3 position = SanitizeFinite(SegmentPositions[index], fallbackPosition);
                SegmentPositions[index] = position;
                float appliedPush = 0f;
                if (canUseSdf &&
                    TrySampleSdfAdaptive(position, sdfInvCellSize, sdfRange, qualityWeight, out float density) &&
                    density > 0f &&
                    TryResolveSdfGradient(position, sdfInvCellSize, sdfRange, sdfGradientStep, out float3 normal))
                {
                    appliedPush = density + clearance;
                    SegmentPositions[index] = SanitizeFinite(position + normal * appliedPush, position);
                    telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagSdf;
                }
                else if (canUseHeight &&
                         TrySampleTerrainHeight(position.x, position.z, out float height, out float3 terrainNormal))
                {
                    float targetHeight = height + clearance;
                    if (position.y < targetHeight)
                    {
                        appliedPush = targetHeight - position.y;
                        SegmentPositions[index] = SanitizeFinite(position + terrainNormal * appliedPush, new float3(position.x, targetHeight, position.z));
                        telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagMapMagic;
                    }
                }

                maxTerrainPush = math.max(maxTerrainPush, appliedPush);
            }

            PullDistanceConstraints(activeCount, segmentLength, ownerForward);
            bool matrixFallback = WriteMatrices(activeCount, maxUsableSegments, segmentLength, bodyRadius, up, ownerForward);

            bool invalid = matrixFallback || HasInvalidSegment(activeCount);
            if (invalid)
                telemetryFlags |= LeviathanTerrainIkConstants.TelemetryFlagInvalid;

            StageCreatureColliders(activeCount, segmentLength, bodyRadius, ownerForward);
            WriteTelemetry(activeCount, telemetryFlags, intended, maxTerrainPush, tailWhipSecondsRemaining, qualityWeight, iterations);
        }

        private void MoveHead(float dt, float segmentLength, float3 intended, float3 ownerForward)
        {
            float3 current = SanitizeFinite(SegmentPositions[0], HeadTargetPosition);
            float3 target = SanitizeFinite(HeadTargetPosition, current + ownerForward * segmentLength);
            float3 delta = target - current;
            float distanceSq = math.lengthsq(delta);
            float intendedSpeed = ResolveLength(intended);
            float maxStep = math.max(segmentLength * 0.25f, intendedSpeed * dt + segmentLength * 0.5f);
            if (math.isfinite(distanceSq) && distanceSq > maxStep * maxStep && distanceSq > MinLengthSq)
                target = current + delta * math.rsqrt(distanceSq) * maxStep;

            SegmentPositions[0] = SanitizeFinite(target, current);
            PreviousSegmentPositions[0] = SegmentPositions[0];
        }

        private void IntegrateFollowers(
            int activeCount,
            float dt,
            float damping,
            float3 intended,
            float3 ownerForward,
            float3 up,
            float swimFrequency,
            float swimAmplitude,
            float qualityCurve)
        {
            float dtSq = dt * dt;
            float3 side = NormalizeSafe(math.cross(up, ownerForward), new float3(1f, 0f, 0f));
            float intendedSpeed = ResolveLength(intended);
            float velocityAmplitude = math.saturate(intendedSpeed * 0.18f + 0.1f);
            for (int i = 1; i < activeCount; i++)
            {
                float3 current = SanitizeFinite(SegmentPositions[i], SegmentPositions[i - 1]);
                float3 previous = SanitizeFinite(PreviousSegmentPositions[i], current);
                float3 velocity = (current - previous) * damping;
                float taper = 1f - i * math.rcp(math.max(1, activeCount - 1));
                float3 drift = intended * (0.04f * taper) * dtSq;
                float wave = CheapSinSigned(FrameIndex * dt * swimFrequency + i * 0.37f);
                drift += side * (wave * swimAmplitude * velocityAmplitude * qualityCurve * taper * dt);
                PreviousSegmentPositions[i] = current;
                SegmentPositions[i] = SanitizeFinite(current + velocity + drift, current);
            }
        }

        private void PullDistanceConstraints(int activeCount, float segmentLength, float3 ownerForward)
        {
            for (int i = 1; i < activeCount; i++)
            {
                float3 parent = SegmentPositions[i - 1];
                float3 child = SanitizeFinite(SegmentPositions[i], parent - ownerForward * segmentLength);
                float resolvedSegmentLength = ResolveConstraintSegmentLength(i, segmentLength);
                float3 delta = child - parent;
                float lengthSq = math.lengthsq(delta);
                float3 direction = math.isfinite(lengthSq) && lengthSq > MinLengthSq
                    ? delta * math.rsqrt(lengthSq)
                    : -ownerForward;
                SegmentPositions[i] = SanitizeFinite(parent + direction * resolvedSegmentLength, parent - ownerForward * resolvedSegmentLength);
            }
        }

        private void ApplyTailWhip(
            int activeCount,
            float segmentLength,
            float3 ownerForward,
            float3 up,
            float tailWhipSecondsRemaining,
            float tailWhipDurationSeconds,
            float tailWhipAmplitudeMeters)
        {
            float normalizedAge = math.saturate(1f - tailWhipSecondsRemaining * math.rcp(tailWhipDurationSeconds));
            float3 side = NormalizeSafe(math.cross(up, ownerForward), new float3(1f, 0f, 0f));
            int firstTail = math.max(1, activeCount >> 1);
            for (int i = firstTail; i < activeCount; i++)
            {
                float t = (i - firstTail) * math.rcp(math.max(1, activeCount - firstTail));
                float wave = CheapSinSigned((normalizedAge * 3.2f) + t * 1.7f);
                float falloff = t * t;
                float3 impulse = side * (wave * tailWhipAmplitudeMeters * falloff);
                SegmentPositions[i] = SanitizeFinite(SegmentPositions[i] + impulse, SegmentPositions[i - 1] - ownerForward * segmentLength);
            }
        }

        private bool WriteMatrices(int activeCount, int maxUsableSegments, float segmentLength, float bodyRadius, float3 up, float3 ownerForward)
        {
            bool usedFallback = false;
            float3 tailForward = ownerForward;
            float3 safeScale = new float3(bodyRadius, bodyRadius, segmentLength);
            for (int i = 0; i < activeCount; i++)
            {
                float3 rawPosition = SegmentPositions[i];
                float3 position = SanitizeFinite(rawPosition, float3.zero);
                usedFallback |= !math.all(math.isfinite(rawPosition));
                SegmentPositions[i] = position;
                float3 tangent;
                if (i + 1 < activeCount)
                {
                    float3 rawNext = SegmentPositions[i + 1];
                    usedFallback |= !math.all(math.isfinite(rawNext));
                    tangent = position - SanitizeFinite(rawNext, position - tailForward * segmentLength);
                }
                else
                {
                    float3 rawPrevious = SegmentPositions[i - 1];
                    usedFallback |= !math.all(math.isfinite(rawPrevious));
                    tangent = SanitizeFinite(rawPrevious, position + tailForward * segmentLength) - position;
                }

                tangent = NormalizeSafe(tangent, tailForward);
                tailForward = tangent;
                quaternion rotation = quaternion.LookRotationSafe(tangent, up);
                usedFallback |= !IsValidQuaternion(rotation);
                rotation = SanitizeQuaternion(rotation, quaternion.identity);
                WriteBoneMatrix(i, float4x4.TRS(position, rotation, safeScale));
            }

            float3 rawTail = SegmentPositions[activeCount - 1];
            float3 tail = SanitizeFinite(rawTail, float3.zero);
            usedFallback |= !math.all(math.isfinite(rawTail));
            for (int i = activeCount; i < maxUsableSegments; i++)
            {
                float3 nextTail = tail - tailForward * segmentLength;
                usedFallback |= !math.all(math.isfinite(nextTail));
                tail = SanitizeFinite(nextTail, float3.zero);
                SegmentPositions[i] = tail;
                PreviousSegmentPositions[i] = tail;
                quaternion rawRotation = quaternion.LookRotationSafe(tailForward, up);
                usedFallback |= !IsValidQuaternion(rawRotation);
                quaternion rotation = SanitizeQuaternion(rawRotation, quaternion.identity);
                WriteBoneMatrix(i, float4x4.TRS(tail, rotation, safeScale));
            }

            return usedFallback;
        }

        private void WriteBoneMatrix(int index, float4x4 matrix)
        {
            if ((uint)index >= (uint)LeviathanBones.Length)
                return;

            LeviathanBoneDTO dto = default;
            dto.LocalToWorld = matrix;
            LeviathanBones[index] = dto;
        }

        private float ResolveConstraintSegmentLength(int boneIndex, float fallback)
        {
            if (!BoneConstraints.IsCreated || (uint)boneIndex >= (uint)BoneConstraints.Length)
                return fallback;

            float length = BoneConstraints[boneIndex].SegmentLengthMeters;
            return SanitizePositiveFinite(length, fallback, LeviathanTerrainIkConstants.MinSegmentLength);
        }

        private void StageCreatureColliders(int activeCount, float segmentLength, float bodyRadius, float3 ownerForward)
        {
            if (!ColliderProxies.IsCreated || ColliderProxies.Length <= 0)
                return;

            int colliderCount = math.min(ColliderProxies.Length, activeCount);
            for (int i = 0; i < colliderCount; i++)
            {
                float3 position = SanitizeFinite(SegmentPositions[i], float3.zero);
                float3 next = i + 1 < activeCount
                    ? SanitizeFinite(SegmentPositions[i + 1], position - ownerForward * segmentLength)
                    : position - ownerForward * segmentLength;
                float3 axisDelta = next - position;
                float lengthSq = math.lengthsq(axisDelta);
                float length = lengthSq > MinLengthSq && math.isfinite(lengthSq)
                    ? lengthSq * math.rsqrt(lengthSq)
                    : segmentLength;
                float3 axis = NormalizeSafe(axisDelta, -ownerForward);
                LeviathanCapsuleColliderDTO proxy = default;
                proxy.Center = position + axis * (length * 0.5f);
                proxy.Radius = SanitizePositiveFinite(bodyRadius, 1f, 0.01f);
                proxy.Axis = axis;
                proxy.HalfHeight = math.max(proxy.Radius, length * 0.5f);
                proxy.OwnerHash = ComputeTelemetryHash(SegmentPositions[0], SegmentPositions[activeCount - 1], IntendedVelocity, activeCount);
                proxy.Flags = LeviathanTerrainIkConstants.TelemetryFlagActive;
                proxy.BoneIndex = i;
                proxy.FrameIndex = FrameIndex;
                proxy.AabbExtents = new float3(proxy.Radius, proxy.Radius, proxy.HalfHeight);
                proxy.Padding0 = 0u;
                ColliderProxies[i] = proxy;
            }

            for (int i = colliderCount; i < ColliderProxies.Length; i++)
                ColliderProxies[i] = default;
        }

        private bool TrySampleSdfAdaptive(float3 worldPosition, float3 invCellSize, float sdfRange, float qualityWeight, out float density)
        {
            float trilinearWeight = math.step(0.3f, SanitizeQualityWeight(qualityWeight));
            if (trilinearWeight <= 0f)
                return TrySampleSdfNearest(worldPosition, invCellSize, sdfRange, out density);

            return TrySampleSdfTrilinear(worldPosition, invCellSize, sdfRange, out density);
        }

        private bool TrySampleSdfNearest(float3 worldPosition, float3 invCellSize, float sdfRange, out float density)
        {
            density = 0f;
            if (!VoxelSdfTexture3D.IsCreated ||
                VoxelSdfDimensions.x <= 1 ||
                VoxelSdfDimensions.y <= 1 ||
                VoxelSdfDimensions.z <= 1 ||
                !math.all(math.isfinite(worldPosition)) ||
                !math.all(math.isfinite(invCellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= 0.0001f)
            {
                return false;
            }

            float3 sample = (worldPosition - VoxelSdfOrigin) * invCellSize;
            if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                sample.x > VoxelSdfDimensions.x - 1f ||
                sample.y > VoxelSdfDimensions.y - 1f ||
                sample.z > VoxelSdfDimensions.z - 1f)
            {
                return false;
            }

            int x = math.clamp((int)math.round(sample.x), 0, VoxelSdfDimensions.x - 1);
            int y = math.clamp((int)math.round(sample.y), 0, VoxelSdfDimensions.y - 1);
            int z = math.clamp((int)math.round(sample.z), 0, VoxelSdfDimensions.z - 1);
            density = DecodeSdf(SdfIndex(x, y, z), sdfRange);
            return math.isfinite(density);
        }

        private bool TrySampleSdfTrilinear(float3 worldPosition, float3 invCellSize, float sdfRange, out float density)
        {
            density = 0f;
            if (!VoxelSdfTexture3D.IsCreated ||
                VoxelSdfDimensions.x <= 1 ||
                VoxelSdfDimensions.y <= 1 ||
                VoxelSdfDimensions.z <= 1 ||
                !math.all(math.isfinite(worldPosition)) ||
                !math.all(math.isfinite(invCellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= 0.0001f)
            {
                return false;
            }

            float3 sample = (worldPosition - VoxelSdfOrigin) * invCellSize;
            if (sample.x < 0f || sample.y < 0f || sample.z < 0f ||
                sample.x > VoxelSdfDimensions.x - 1f ||
                sample.y > VoxelSdfDimensions.y - 1f ||
                sample.z > VoxelSdfDimensions.z - 1f)
            {
                return false;
            }

            sample = math.clamp(sample, float3.zero, new float3(VoxelSdfDimensions.x - 1.001f, VoxelSdfDimensions.y - 1.001f, VoxelSdfDimensions.z - 1.001f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, VoxelSdfDimensions.x - 1);
            int y1 = math.min(y0 + 1, VoxelSdfDimensions.y - 1);
            int z1 = math.min(z0 + 1, VoxelSdfDimensions.z - 1);
            float3 f = sample - new float3(x0, y0, z0);
            float c000 = DecodeSdf(SdfIndex(x0, y0, z0), sdfRange);
            float c100 = DecodeSdf(SdfIndex(x1, y0, z0), sdfRange);
            float c010 = DecodeSdf(SdfIndex(x0, y1, z0), sdfRange);
            float c110 = DecodeSdf(SdfIndex(x1, y1, z0), sdfRange);
            float c001 = DecodeSdf(SdfIndex(x0, y0, z1), sdfRange);
            float c101 = DecodeSdf(SdfIndex(x1, y0, z1), sdfRange);
            float c011 = DecodeSdf(SdfIndex(x0, y1, z1), sdfRange);
            float c111 = DecodeSdf(SdfIndex(x1, y1, z1), sdfRange);
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            density = math.lerp(c0, c1, f.z);
            return math.isfinite(density);
        }

        private bool TrySampleSdfTrilinearClamped(float3 worldPosition, float3 invCellSize, float sdfRange, out float density)
        {
            density = 0f;
            if (!VoxelSdfTexture3D.IsCreated ||
                VoxelSdfDimensions.x <= 1 ||
                VoxelSdfDimensions.y <= 1 ||
                VoxelSdfDimensions.z <= 1 ||
                !math.all(math.isfinite(worldPosition)) ||
                !math.all(math.isfinite(invCellSize)) ||
                !math.isfinite(sdfRange) ||
                sdfRange <= 0.0001f)
            {
                return false;
            }

            float3 sample = math.clamp(
                (worldPosition - VoxelSdfOrigin) * invCellSize,
                float3.zero,
                new float3(VoxelSdfDimensions.x - 1.001f, VoxelSdfDimensions.y - 1.001f, VoxelSdfDimensions.z - 1.001f));
            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, VoxelSdfDimensions.x - 1);
            int y1 = math.min(y0 + 1, VoxelSdfDimensions.y - 1);
            int z1 = math.min(z0 + 1, VoxelSdfDimensions.z - 1);
            float3 f = sample - new float3(x0, y0, z0);
            float c000 = DecodeSdf(SdfIndex(x0, y0, z0), sdfRange);
            float c100 = DecodeSdf(SdfIndex(x1, y0, z0), sdfRange);
            float c010 = DecodeSdf(SdfIndex(x0, y1, z0), sdfRange);
            float c110 = DecodeSdf(SdfIndex(x1, y1, z0), sdfRange);
            float c001 = DecodeSdf(SdfIndex(x0, y0, z1), sdfRange);
            float c101 = DecodeSdf(SdfIndex(x1, y0, z1), sdfRange);
            float c011 = DecodeSdf(SdfIndex(x0, y1, z1), sdfRange);
            float c111 = DecodeSdf(SdfIndex(x1, y1, z1), sdfRange);
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            density = math.lerp(c0, c1, f.z);
            return math.isfinite(density);
        }

        private bool TryResolveSdfGradient(float3 worldPosition, float3 invCellSize, float sdfRange, float3 step, out float3 normal)
        {
            normal = new float3(0f, 1f, 0f);
            float3 safeStep = SanitizePositiveFinite(step, new float3(0.05f), new float3(0.0001f));
            bool x0 = TrySampleSdfTrilinearClamped(worldPosition - new float3(safeStep.x, 0f, 0f), invCellSize, sdfRange, out float dx0);
            bool x1 = TrySampleSdfTrilinearClamped(worldPosition + new float3(safeStep.x, 0f, 0f), invCellSize, sdfRange, out float dx1);
            bool y0 = TrySampleSdfTrilinearClamped(worldPosition - new float3(0f, safeStep.y, 0f), invCellSize, sdfRange, out float dy0);
            bool y1 = TrySampleSdfTrilinearClamped(worldPosition + new float3(0f, safeStep.y, 0f), invCellSize, sdfRange, out float dy1);
            bool z0 = TrySampleSdfTrilinearClamped(worldPosition - new float3(0f, 0f, safeStep.z), invCellSize, sdfRange, out float dz0);
            bool z1 = TrySampleSdfTrilinearClamped(worldPosition + new float3(0f, 0f, safeStep.z), invCellSize, sdfRange, out float dz1);
            if (!x0 || !x1 || !y0 || !y1 || !z0 || !z1)
                return false;

            float3 invStep = math.rcp(safeStep);
            float3 gradient = new float3((dx0 - dx1) * invStep.x, (dy0 - dy1) * invStep.y, (dz0 - dz1) * invStep.z);
            normal = NormalizeSafe(gradient, new float3(0f, 1f, 0f));
            return math.all(math.isfinite(normal));
        }

        private bool TrySampleTerrainHeight(float worldX, float worldZ, out float height, out float3 normal)
        {
            height = 0f;
            normal = new float3(0f, 1f, 0f);
            if (!TerrainHeightSamples.IsCreated ||
                !TryResolveTerrainHeightSampleCount(TerrainResolution, out int expectedLength) ||
                TerrainHeightSamples.Length < expectedLength ||
                !math.isfinite(worldX) ||
                !math.isfinite(worldZ) ||
                !math.all(math.isfinite(TerrainOrigin)) ||
                !math.all(math.isfinite(TerrainSize)) ||
                TerrainSize.x <= LeviathanTerrainIkConstants.MinTerrainSize ||
                TerrainSize.y <= LeviathanTerrainIkConstants.MinTerrainSize ||
                TerrainSize.z <= LeviathanTerrainIkConstants.MinTerrainSize)
            {
                return false;
            }

            float localX = worldX - TerrainOrigin.x;
            float localZ = worldZ - TerrainOrigin.z;
            if (localX < 0f || localZ < 0f || localX > TerrainSize.x || localZ > TerrainSize.z)
                return false;

            float normalizedX = math.saturate(localX * math.rcp(TerrainSize.x));
            float normalizedZ = math.saturate(localZ * math.rcp(TerrainSize.z));
            float sampleX = normalizedX * (TerrainResolution - 1);
            float sampleZ = normalizedZ * (TerrainResolution - 1);
            int x0 = math.clamp((int)math.floor(sampleX), 0, TerrainResolution - 1);
            int z0 = math.clamp((int)math.floor(sampleZ), 0, TerrainResolution - 1);
            int x1 = math.min(x0 + 1, TerrainResolution - 1);
            int z1 = math.min(z0 + 1, TerrainResolution - 1);
            float fracX = sampleX - x0;
            float fracZ = sampleZ - z0;
            float h00 = DecodeTerrainHeight(x0, z0);
            float h10 = DecodeTerrainHeight(x1, z0);
            float h01 = DecodeTerrainHeight(x0, z1);
            float h11 = DecodeTerrainHeight(x1, z1);
            float h0 = math.lerp(h00, h10, fracX);
            float h1 = math.lerp(h01, h11, fracX);
            height = TerrainOrigin.y + math.lerp(h0, h1, fracZ);
            float gradientX = (h10 - h00) * math.rcp(math.max(0.0001f, TerrainSize.x * math.rcp(TerrainResolution - 1)));
            float gradientZ = (h01 - h00) * math.rcp(math.max(0.0001f, TerrainSize.z * math.rcp(TerrainResolution - 1)));
            normal = NormalizeSafe(new float3(-gradientX, 1f, -gradientZ), new float3(0f, 1f, 0f));
            return math.isfinite(height);
        }

        private float DecodeTerrainHeight(int x, int z)
        {
            int index = math.clamp(z, 0, TerrainResolution - 1) * TerrainResolution + math.clamp(x, 0, TerrainResolution - 1);
            return TerrainHeightSamples[index] * (1f / 65535f) * TerrainSize.y;
        }

        private float DecodeSdf(int index, float sdfRange)
        {
            if ((uint)index >= (uint)VoxelSdfTexture3D.Length)
                return -sdfRange;

            return ((VoxelSdfTexture3D[index] * InvEncodedByteMax) * 2f - 1f) * sdfRange;
        }

        private int SdfIndex(int x, int y, int z)
        {
            return (z * VoxelSdfDimensions.y + y) * VoxelSdfDimensions.x + x;
        }

        private void WriteTelemetry(
            int activeCount,
            uint flags,
            float3 intended,
            float maxTerrainPush,
            float tailWhipSecondsRemaining,
            float qualityWeight,
            int iterations)
        {
            if (!TelemetryRing.IsCreated || !TelemetryCursor.IsCreated || TelemetryRing.Length <= 0 || TelemetryCursor.Length <= 0)
                return;

            int cursor = TelemetryCursor[0];
            int index = cursor % TelemetryRing.Length;
            if (index < 0)
                index += TelemetryRing.Length;

            float3 head = SegmentPositions[0];
            float3 tail = SegmentPositions[activeCount - 1];
            LeviathanTerrainIkTelemetryEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.ActiveSegmentCount = activeCount;
            entry.Flags = flags;
            entry.StateHash = ComputeTelemetryHash(head, tail, intended, activeCount);
            entry.HeadPosition = SanitizeFinite(head, float3.zero);
            entry.TailPosition = SanitizeFinite(tail, float3.zero);
            entry.IntendedVelocity = SanitizeFinite(intended, float3.zero);
            entry.MaxTerrainPushMeters = math.select(0f, maxTerrainPush, math.isfinite(maxTerrainPush));
            entry.TailWhipSecondsRemaining = tailWhipSecondsRemaining;
            entry.GlobalQualityWeight = SanitizeQualityWeight(qualityWeight);
            entry.RootAup = SanitizeFiniteDouble3(RootAup);
            entry.AverageFabrikIterations = iterations;
            entry.BurstSolveMicros = SanitizePositiveFinite(BurstSolveMicros, 0f, 0f);
            TelemetryRing[index] = entry;
            if (cursor == int.MaxValue)
            {
                int nextIndex = index + 1;
                if (nextIndex >= TelemetryRing.Length)
                    nextIndex = 0;

                TelemetryCursor[0] = TelemetryRing.Length + nextIndex;
            }
            else
            {
                TelemetryCursor[0] = cursor + 1;
            }
        }

        private bool HasInvalidSegment(int activeCount)
        {
            for (int i = 0; i < activeCount; i++)
            {
                if (!math.all(math.isfinite(SegmentPositions[i])))
                    return true;
            }

            return false;
        }

        private static uint ComputeTelemetryHash(float3 head, float3 tail, float3 intended, int activeCount)
        {
            uint hash = 2166136261u;
            hash = HashFloat3(hash, head);
            hash = HashFloat3(hash, tail);
            hash = HashFloat3(hash, intended);
            hash = (hash ^ (uint)activeCount) * 16777619u;
            return hash;
        }

        private static uint HashFloat3(uint hash, float3 value)
        {
            hash = (hash ^ (uint)math.asint(value.x)) * 16777619u;
            hash = (hash ^ (uint)math.asint(value.y)) * 16777619u;
            hash = (hash ^ (uint)math.asint(value.z)) * 16777619u;
            return hash;
        }

        private static float ResolveLength(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > MinLengthSq && math.isfinite(lengthSq) ? lengthSq * math.rsqrt(lengthSq) : 0f;
        }

        private static double3 SanitizeFiniteDouble3(double3 value)
        {
            return math.all(math.isfinite(value)) ? value : double3.zero;
        }

        public static bool TryResolveSdfVoxelCount(int3 dimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (dimensions.x <= 1 || dimensions.y <= 1 || dimensions.z <= 1)
                return false;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        public static bool TryResolveTerrainHeightSampleCount(int resolution, out int sampleCount)
        {
            sampleCount = 0;
            if (resolution <= 1)
                return false;

            long count = (long)resolution * resolution;
            if (count <= 0L || count > int.MaxValue)
                return false;

            sampleCount = (int)count;
            return true;
        }

        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= MinLengthSq)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static quaternion SanitizeQuaternion(quaternion value, quaternion fallback)
        {
            float lengthSq = math.lengthsq(value.value);
            return IsValidQuaternion(value)
                ? new quaternion(value.value * math.rsqrt(lengthSq))
                : fallback;
        }

        private static bool IsValidQuaternion(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            return math.all(math.isfinite(value.value)) && math.isfinite(lengthSq) && lengthSq > MinLengthSq;
        }

        private static float SanitizePositiveFinite(float value, float fallback, float minValue)
        {
            return math.isfinite(value) ? math.max(value, minValue) : fallback;
        }

        private static float SanitizeQualityWeight(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float Smooth01(float value)
        {
            float weight = SanitizeQualityWeight(value);
            return weight * weight * (3f - 2f * weight);
        }

        private static float3 SanitizePositiveFinite(float3 value, float3 fallback, float3 minValue)
        {
            return math.all(math.isfinite(value)) ? math.max(value, minValue) : fallback;
        }

        private static float SanitizeFiniteClamp(float value, float fallback, float minValue, float maxValue)
        {
            return math.isfinite(value) ? math.clamp(value, minValue, maxValue) : fallback;
        }

        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        private static float CheapSinSigned(float cycle)
        {
            float triangle = math.abs(math.frac(cycle) * 2f - 1f);
            return 1f - triangle * 2f;
        }
    }
}
