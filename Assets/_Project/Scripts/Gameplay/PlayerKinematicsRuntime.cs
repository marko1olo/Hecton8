using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Inventory;
using Hecton8.Physics;
using Hecton8.Physics.Determinism;
using Hecton8.Physics.KCC;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct PlayerKinematicsRuntimeTelemetryEntry
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public float3 IntendedMovement;
        [FieldOffset(36)] public float DragCoefficient;
        [FieldOffset(40)] public float WaterDensity;
        [FieldOffset(44)] public float SolidDensity;
        [FieldOffset(48)] public uint Frame;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint SyncFenceHash;
        [FieldOffset(60)] public uint AuxFlags;
        [FieldOffset(64)] public float AupMaxDriftErrorMeters;
        [FieldOffset(68)] public uint Reserved0;
        [FieldOffset(72)] public uint Reserved1;
        [FieldOffset(76)] public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PlayerKinematicsSyncState
    {
        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint StateHash;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] public uint Reserved1;
        [FieldOffset(60)] public uint Reserved2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct PlayerKinematicsAccumulatorState
    {
        [FieldOffset(0)] public int FastTickCounter;
        [FieldOffset(4)] public int PreShiftHaltFrames;
        [FieldOffset(8)] public uint LastConsumedPreShiftFrameId;
        [FieldOffset(12)] public uint LastSyncFenceHash;
        [FieldOffset(16)] public uint LastSyncFenceFrame;
        [FieldOffset(20)] public uint LastGpuFlowFrame;
        [FieldOffset(24)] public uint SqueezeInterventions;
        [FieldOffset(28)] public uint Reserved;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlayerKinematicsBodyJob : IJob
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<float3> IntendedMovement;
        public NativeArray<float3> FlowVelocity;
        public NativeArray<float3> LastValidPositions;
        [ReadOnly] public NativeArray<WhirlpoolFlow> ActiveMaelstroms;
        [ReadOnly] public NativeArray<byte> VoxelSdfTexture3D;
        public NativeArray<PlayerKinematicsRuntimeTelemetryEntry> Telemetry;
        public NativeArray<int> TelemetryWriteIndex;
        public NativeArray<int> FaultFlags;
        public int ActiveMaelstromCount;
        public int3 VoxelSdfDimensions;
        public float DeltaTime;
        public float DragCoefficient;
        public float WaterDensity;
        public float EquipmentDragMultiplier;
        public float MaelstromVelocityClamp;
        public float LadderSnapRadiusSq;
        public float3 LadderPoint;
        public float3 VoxelSdfOrigin;
        public float3 VoxelSdfCellSize;
        public float3 TargetAup;
        public float SolidDensity;
        public float VoxelSdfRange;
        public float SdfSampleStepMeters;
        public byte LowMaelstromTier;
        public byte SdfSampleMode;
        public byte SdfGradientProbeRequested;
        public uint Frame;
        public uint RuntimeFlags;

        internal const byte SdfSampleModeAxis6 = 0;
        internal const byte SdfSampleModeTetra4 = 1;
        private const float InvEncodedSdfByteMax = 0.0039215686274509803f;

        public void Execute()
        {
            float dt = SanitizeNonNegative(DeltaTime);
            float3 fallbackPosition = SanitizeFloat3(LastValidPositions[0], float3.zero);
            float3 position = SanitizeFloat3(Positions[0], fallbackPosition);
            float3 velocity = SanitizeFloat3(Velocities[0], float3.zero);
            float3 intended = SanitizeFloat3(IntendedMovement[0], float3.zero);
            uint runtimeFlags = RuntimeFlags;
            float drag = SanitizeNonNegative(DragCoefficient) * SanitizeNonNegative(EquipmentDragMultiplier);
            float density = SanitizeNonNegative(WaterDensity);
            float telemetrySolidDensity = math.select(SolidDensity, 0.0f, !math.isfinite(SolidDensity));

            if ((SdfGradientProbeRequested & 1) != 0 &&
                TryResolveSdfOpenSpaceGradient(
                        VoxelSdfTexture3D,
                        VoxelSdfDimensions,
                        VoxelSdfOrigin,
                        VoxelSdfCellSize,
                        VoxelSdfRange,
                        TargetAup,
                        intended,
                        SdfSampleStepMeters,
                        SdfSampleMode,
                        out _,
                        out float sdfDensity))
            {
                runtimeFlags |= PlayerKinematicsRuntime.BodyFlagSdfGradientValid;
                telemetrySolidDensity = sdfDensity;
                if ((SdfSampleMode & SdfSampleModeTetra4) != 0)
                    runtimeFlags |= PlayerKinematicsRuntime.BodyFlagSdfLowTierGradient;
            }
            else if (TrySampleSdfTrilinear(
                         VoxelSdfTexture3D,
                         VoxelSdfDimensions,
                         VoxelSdfOrigin,
                         VoxelSdfCellSize,
                         VoxelSdfRange,
                         TargetAup,
                         out sdfDensity))
            {
                telemetrySolidDensity = sdfDensity;
            }

            float dragTerm = SanitizeNonNegative(drag * density * dt);
            velocity *= math.rcp(1.0f + dragTerm);
            velocity += SanitizeFloat3(FlowVelocity[0], float3.zero) * dt;
            if (ActiveMaelstromCount > 0)
            {
                float3 maelstromVelocity = HectonAnalyticalFlowField.SampleWhirlpoolVelocity(
                    position,
                    ActiveMaelstroms,
                    ActiveMaelstromCount,
                    LowMaelstromTier,
                    MaelstromVelocityClamp);
                velocity += SanitizeFloat3(maelstromVelocity, float3.zero) * dt;
            }

            if ((runtimeFlags & PlayerKinematicsRuntime.BodyFlagLadderActive) != 0u)
            {
                float3 delta = position - LadderPoint;
                float xzSq = (delta.x * delta.x) + (delta.z * delta.z);
                if (xzSq <= LadderSnapRadiusSq)
                {
                    position.x = LadderPoint.x;
                    position.z = LadderPoint.z;
                    velocity.x = 0.0f;
                    velocity.z = 0.0f;
                }
            }

            int flags = 0;
            bool finite = math.all(math.isfinite(position)) &&
                          math.all(math.isfinite(velocity)) &&
                          math.all(math.isfinite(intended));
            if (!finite)
            {
                flags = PlayerKinematicsRuntime.FaultNaN;
                position = fallbackPosition;
                velocity = float3.zero;
            }
            else if ((runtimeFlags & PlayerKinematicsRuntime.BodyFlagInSolid) != 0u)
            {
                flags = PlayerKinematicsRuntime.FaultSolidTeleport;
                position = fallbackPosition;
                velocity = float3.zero;
            }
            else
            {
                position = SnapMillimeter(position);
                velocity = SnapMillimeter(velocity);
                LastValidPositions[0] = position;
            }

            Positions[0] = position;
            Velocities[0] = velocity;
            FaultFlags[0] = flags;

            if (Telemetry.IsCreated && Telemetry.Length > 0 && TelemetryWriteIndex.IsCreated && TelemetryWriteIndex.Length > 0)
            {
                int wrappedIndex = ReserveTelemetrySlot(TelemetryWriteIndex, Telemetry.Length);
                Telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
                {
                    Position = position,
                    Velocity = velocity,
                    IntendedMovement = intended,
                    DragCoefficient = drag,
                    WaterDensity = density,
                    SolidDensity = telemetrySolidDensity,
                    Frame = Frame,
                    Flags = (uint)flags,
                    SyncFenceHash = 0u,
                    AuxFlags = runtimeFlags,
                    AupMaxDriftErrorMeters = 0.0f
                };
            }
        }

        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                DeterministicPhysicsMath.SnapMillimeter(value.x),
                DeterministicPhysicsMath.SnapMillimeter(value.y),
                DeterministicPhysicsMath.SnapMillimeter(value.z));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static int ReserveTelemetrySlot(NativeArray<int> writeCursor, int telemetryLength)
        {
            int safeLength = math.max(1, telemetryLength);
            int writeIndex = math.max(0, writeCursor[0]);
            int wrappedIndex = writeIndex % safeLength;
            writeCursor[0] = (wrappedIndex + 1) % safeLength;
            return wrappedIndex;
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryResolveSdfOpenSpaceGradient(
            NativeArray<byte> encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 targetPosition,
            float3 intendedMovement,
            float sampleStepMeters,
            byte sampleMode,
            out float3 squeezeDirection,
            out float centerDensity)
        {
            squeezeDirection = float3.zero;
            centerDensity = 0.0f;
            if (!TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition, out centerDensity))
                return false;

            float step = math.max(0.025f, SanitizeNonNegative(math.abs(sampleStepMeters)));
            float3 openGradient;
            if ((sampleMode & SdfSampleModeTetra4) != 0)
            {
                float invRoot3 = 0.57735026919f;
                float3 d0 = new float3(1.0f, 1.0f, 1.0f) * invRoot3;
                float3 d1 = new float3(-1.0f, -1.0f, 1.0f) * invRoot3;
                float3 d2 = new float3(-1.0f, 1.0f, -1.0f) * invRoot3;
                float3 d3 = new float3(1.0f, -1.0f, -1.0f) * invRoot3;
                if (!TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + d0 * step, out float s0) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + d1 * step, out float s1) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + d2 * step, out float s2) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + d3 * step, out float s3))
                {
                    return false;
                }

                openGradient = -((d0 * s0) + (d1 * s1) + (d2 * s2) + (d3 * s3));
            }
            else
            {
                if (!TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + new float3(step, 0.0f, 0.0f), out float px) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition - new float3(step, 0.0f, 0.0f), out float nx) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + new float3(0.0f, step, 0.0f), out float py) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition - new float3(0.0f, step, 0.0f), out float ny) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition + new float3(0.0f, 0.0f, step), out float pz) ||
                    !TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition - new float3(0.0f, 0.0f, step), out float nz))
                {
                    return false;
                }

                openGradient = -new float3(px - nx, py - ny, pz - nz);
            }

            if (!math.all(math.isfinite(openGradient)))
                return false;

            float3 intendedDirection = NormalizeSafe(intendedMovement, float3.zero);
            if (math.lengthsq(intendedDirection) > 0.000001f)
                openGradient -= intendedDirection * math.dot(openGradient, intendedDirection);

            squeezeDirection = NormalizeSafe(openGradient, float3.zero);
            return math.lengthsq(squeezeDirection) > 0.000001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TrySampleSdfTrilinear(
            NativeArray<byte> encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 runtimePosition,
            out float density)
        {
            density = 0.0f;
            if (!encodedSdf.IsCreated ||
                !TryResolveSdfVoxelCount(gridDimensions, out int voxelCount) ||
                encodedSdf.Length < voxelCount ||
                sdfRange <= 0.0f ||
                !math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(volumeOrigin)) ||
                !math.all(math.isfinite(cellSize)))
            {
                return false;
            }

            float3 invCellSize = math.rcp(math.max(math.abs(cellSize), new float3(0.0001f)));
            float3 sample = (runtimePosition - volumeOrigin) * invCellSize;
            float3 minSample = new float3(-0.5f);
            float3 maxSample = new float3(
                gridDimensions.x - 0.5f,
                gridDimensions.y - 0.5f,
                gridDimensions.z - 0.5f);
            if (math.any(sample < minSample) || math.any(sample > maxSample))
                return false;

            sample = math.clamp(
                sample,
                float3.zero,
                new float3(gridDimensions.x - 1.0f, gridDimensions.y - 1.0f, gridDimensions.z - 1.0f));

            int x0 = (int)math.floor(sample.x);
            int y0 = (int)math.floor(sample.y);
            int z0 = (int)math.floor(sample.z);
            int x1 = math.min(x0 + 1, gridDimensions.x - 1);
            int y1 = math.min(y0 + 1, gridDimensions.y - 1);
            int z1 = math.min(z0 + 1, gridDimensions.z - 1);
            float tx = sample.x - x0;
            float ty = sample.y - y0;
            float tz = sample.z - z0;

            float c000 = DecodeSdfAt(encodedSdf, gridDimensions, x0, y0, z0, sdfRange);
            float c100 = DecodeSdfAt(encodedSdf, gridDimensions, x1, y0, z0, sdfRange);
            float c010 = DecodeSdfAt(encodedSdf, gridDimensions, x0, y1, z0, sdfRange);
            float c110 = DecodeSdfAt(encodedSdf, gridDimensions, x1, y1, z0, sdfRange);
            float c001 = DecodeSdfAt(encodedSdf, gridDimensions, x0, y0, z1, sdfRange);
            float c101 = DecodeSdfAt(encodedSdf, gridDimensions, x1, y0, z1, sdfRange);
            float c011 = DecodeSdfAt(encodedSdf, gridDimensions, x0, y1, z1, sdfRange);
            float c111 = DecodeSdfAt(encodedSdf, gridDimensions, x1, y1, z1, sdfRange);
            float c00 = math.lerp(c000, c100, tx);
            float c10 = math.lerp(c010, c110, tx);
            float c01 = math.lerp(c001, c101, tx);
            float c11 = math.lerp(c011, c111, tx);
            float c0 = math.lerp(c00, c10, ty);
            float c1 = math.lerp(c01, c11, ty);
            density = math.lerp(c0, c1, tz);
            return math.isfinite(density);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool TryResolveSdfVoxelCount(int3 gridDimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1)
            {
                return false;
            }

            long count = (long)gridDimensions.x * gridDimensions.y * gridDimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DecodeSdfAt(NativeArray<byte> encodedSdf, int3 gridDimensions, int x, int y, int z, float sdfRange)
        {
            int index = x + gridDimensions.x * (y + gridDimensions.y * z);
            if ((uint)index >= (uint)encodedSdf.Length)
                return 0.0f;

            return ((encodedSdf[index] * InvEncodedSdfByteMax) * 2.0f - 1.0f) * sdfRange;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(math.max(lengthSq, 0.000001f))
                : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct PlayerKinematicsHandPlacementJob : IJob
    {
        public const byte RuntimeFlagLowTier = 1 << 0;
        public const byte RuntimeFlagImpact = 1 << 1;

        [ReadOnly] public NativeArray<RaycastHit> Hits;
        public NativeArray<PlayerKinematicsHandTarget> Targets;
        public float3 SourcePosition;
        public float3 SourceForward;
        public float3 SourceRight;
        public float3 SourceUp;
        public float3 Velocity;
        public float3 ImpactPoint;
        public float3 ImpactNormal;
        public float ContactOffset;
        public float MaxProbeDistance;
        public float BraceDistance;
        public float BraceSpeedThreshold;
        public float BraceBlend;
        public float SqueezeBlend;
        public float Stress01;
        public float Phase;
        public byte RuntimeFlags;

        public void Execute()
        {
            if (!Targets.IsCreated || Targets.Length < 2)
                return;

            float3 forward = SafeNormalize(SourceForward, new float3(0.0f, 0.0f, 1.0f));
            float3 right = SafeNormalize(SourceRight, new float3(1.0f, 0.0f, 0.0f));
            float3 up = SafeNormalize(SourceUp, new float3(0.0f, 1.0f, 0.0f));
            float speedSq = math.lengthsq(Velocity);
            float speed = speedSq > 0.000001f && math.isfinite(speedSq)
                ? speedSq * math.rsqrt(math.max(speedSq, 0.000001f))
                : 0.0f;
            float speedBlend = SanitizeUnit((speed - SanitizeNonNegative(BraceSpeedThreshold)) * 0.25f);
            float braceBaseBlend = math.max(SanitizeUnit(BraceBlend), speedBlend);

            PlayerKinematicsHandTarget leftTarget = default;
            PlayerKinematicsHandTarget rightTarget = default;
            if ((RuntimeFlags & RuntimeFlagLowTier) != 0)
            {
                TryBuildProbeTarget(0, -1.0f, 0.18f, braceBaseBlend, forward, right, up, ref leftTarget);
                TryBuildProbeTarget(0, 1.0f, 0.18f, braceBaseBlend, forward, right, up, ref rightTarget);
            }
            else
            {
                TryBuildProbeTarget(0, -1.0f, 0.04f, braceBaseBlend, forward, right, up, ref leftTarget);
                TryBuildProbeTarget(1, 1.0f, 0.04f, braceBaseBlend, forward, right, up, ref rightTarget);
                if (leftTarget.Hit == 0)
                    TryBuildBestCentralTarget(-1.0f, braceBaseBlend, forward, right, up, ref leftTarget);
                if (rightTarget.Hit == 0)
                    TryBuildBestCentralTarget(1.0f, braceBaseBlend, forward, right, up, ref rightTarget);
            }

            if ((RuntimeFlags & RuntimeFlagImpact) != 0 && braceBaseBlend > 0.0001f)
            {
                ApplyImpactFallback(-1.0f, braceBaseBlend, forward, right, up, ref leftTarget);
                ApplyImpactFallback(1.0f, braceBaseBlend, forward, right, up, ref rightTarget);
            }

            ApplySqueeze(-1.0f, forward, right, up, ref leftTarget);
            ApplySqueeze(1.0f, forward, right, up, ref rightTarget);

            Targets[0] = leftTarget;
            Targets[1] = rightTarget;
        }

        private bool TryBuildProbeTarget(
            int hitIndex,
            float sideSign,
            float sideOffset,
            float braceBaseBlend,
            float3 forward,
            float3 right,
            float3 up,
            ref PlayerKinematicsHandTarget target)
        {
            if (braceBaseBlend <= 0.0001f || !Hits.IsCreated || hitIndex < 0 || hitIndex >= Hits.Length)
                return false;

            RaycastHit hit = Hits[hitIndex];
            float3 hitPoint = ToFloat3(hit.point);
            float3 hitNormal = ToFloat3(hit.normal);
            if (!HasHit(in hit, hitPoint, hitNormal))
            {
                return false;
            }

            float3 normal = SafeNormalize(hitNormal, new float3(0.0f, 1.0f, 0.0f));
            float safeBraceDistance = math.max(0.001f, SanitizeNonNegative(BraceDistance));
            float safeHitDistance = math.clamp(hit.distance, 0.0f, math.max(0.001f, SanitizeNonNegative(MaxProbeDistance)));
            if (safeHitDistance > safeBraceDistance)
                return false;

            float distanceBlend = 1.0f - safeHitDistance * math.rcp(safeBraceDistance);
            float blend = SanitizeUnit(braceBaseBlend * (0.35f + 0.65f * distanceBlend));
            float jitter = SanitizeUnit(Stress01) * blend * 0.012f;
            float lateralJitter = TriangleWaveSigned(Phase + sideSign * 0.31f) * jitter;
            float verticalJitter = TriangleWaveSigned((Phase * 1.71f) + sideSign * 0.17f) * jitter * 0.5f;
            float3 point = hitPoint +
                           normal * SanitizeNonNegative(ContactOffset) +
                           right * (sideSign * SanitizeNonNegative(sideOffset) + lateralJitter) +
                           up * verticalJitter;
            if (!math.all(math.isfinite(point)))
            {
                return false;
            }

            target = new PlayerKinematicsHandTarget
            {
                Position = point,
                Normal = normal,
                Blend = blend,
                Hit = 1,
                Flags = PlayerKinematicsHandTarget.FlagBrace
            };
            return true;
        }

        private void TryBuildBestCentralTarget(
            float sideSign,
            float braceBaseBlend,
            float3 forward,
            float3 right,
            float3 up,
            ref PlayerKinematicsHandTarget target)
        {
            PlayerKinematicsHandTarget candidate = default;
            bool hasCandidate = false;
            if (TryBuildProbeTarget(2, sideSign, 0.16f, braceBaseBlend, forward, right, up, ref candidate))
            {
                target = candidate;
                hasCandidate = true;
            }

            candidate = default;
            if (TryBuildProbeTarget(3, sideSign, 0.14f, braceBaseBlend, forward, right, up, ref candidate) &&
                (!hasCandidate || candidate.Blend > target.Blend))
            {
                target = candidate;
            }
        }

        private void ApplyImpactFallback(
            float sideSign,
            float braceBaseBlend,
            float3 forward,
            float3 right,
            float3 up,
            ref PlayerKinematicsHandTarget target)
        {
            if (!math.all(math.isfinite(ImpactPoint)) || !math.all(math.isfinite(ImpactNormal)))
                return;

            float3 normal = SafeNormalize(ImpactNormal, -forward);
            float3 point = ImpactPoint +
                           normal * SanitizeNonNegative(ContactOffset) +
                           right * sideSign * 0.17f -
                           up * 0.04f;
            if (!math.all(math.isfinite(point)))
                return;

            PlayerKinematicsHandTarget impactTarget = new PlayerKinematicsHandTarget
            {
                Position = point,
                Normal = normal,
                Blend = SanitizeUnit(braceBaseBlend),
                Hit = 1,
                Flags = PlayerKinematicsHandTarget.FlagBrace
            };

            if (target.Hit == 0 || impactTarget.Blend > target.Blend)
                target = impactTarget;
        }

        private void ApplySqueeze(
            float sideSign,
            float3 forward,
            float3 right,
            float3 up,
            ref PlayerKinematicsHandTarget target)
        {
            float squeezeBlend = SanitizeUnit(SqueezeBlend);
            if (squeezeBlend <= 0.0001f)
                return;

            float3 squeezePoint = SourcePosition +
                                  forward * 0.46f +
                                  right * sideSign * 0.11f -
                                  up * 0.16f;
            if (!math.all(math.isfinite(squeezePoint)))
                return;

            float3 squeezeNormal = -forward;
            if (target.Hit != 0 &&
                math.all(math.isfinite(target.Position)) &&
                math.all(math.isfinite(target.Normal)))
            {
                target.Position = math.lerp(target.Position, squeezePoint, squeezeBlend);
                target.Normal = SafeNormalize(math.lerp(target.Normal, squeezeNormal, squeezeBlend), squeezeNormal);
                target.Blend = math.max(target.Blend, squeezeBlend);
                target.Flags |= PlayerKinematicsHandTarget.FlagSqueeze;
                return;
            }

            target = new PlayerKinematicsHandTarget
            {
                Position = squeezePoint,
                Normal = squeezeNormal,
                Blend = squeezeBlend,
                Hit = 1,
                Flags = PlayerKinematicsHandTarget.FlagSqueeze
            };
        }

        private static bool HasHit(in RaycastHit hit, float3 point, float3 normal)
        {
            if (!math.isfinite(hit.distance) ||
                hit.distance < 0.0f ||
                !math.all(math.isfinite(point)) ||
                !math.all(math.isfinite(normal)))
            {
                return false;
            }

            return hit.distance > 0.0f || math.lengthsq(normal) > 0.0001f;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f && math.all(math.isfinite(value))
                ? value * math.rsqrt(math.max(lengthSq, 0.000001f))
                : fallback;
        }

        private static float SanitizeUnit(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static float TriangleWaveSigned(float phase)
        {
            float wrapped = phase - math.floor(phase);
            float triangle01 = 1.0f - math.abs((wrapped * 2.0f) - 1.0f);
            return (triangle01 * 2.0f) - 1.0f;
        }
    }

    /// <summary>
    /// Player kinematic SOA bridge for Burst drag, equipment load, hand probes, AUP sync, and failsafe telemetry.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(HectonPlayerMovement))]
    [RequireComponent(typeof(HectonPlayerMotor))]
    [AddComponentMenu("Hecton8/Gameplay/Player/Player Kinematics Runtime")]
    public sealed class PlayerKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IFastTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener, IScalabilityChangedEventListener
    {
        private struct VaultBufferBinding<T>
            where T : struct
        {
            public VaultBufferHandle<T> Handle;
            public BufferID BufferId;
            public int RequiredLength;
            public SystemID OwnerSystemId;
            private IDataVault _vault;

            public VaultBufferBinding(BufferID bufferId, int requiredLength, SystemID ownerSystemId)
            {
                Handle = default;
                BufferId = bufferId;
                RequiredLength = requiredLength;
                OwnerSystemId = ownerSystemId;
                _vault = null;
            }

            public bool IsCreated
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer.IsCreated;
                }
            }

            public int Length
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer.IsCreated ? buffer.Length : 0;
                }
            }

            public bool Ensure(IDataVault dataVault, NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            {
                if (dataVault == null || RequiredLength <= 0)
                {
                    Handle = default;
                    _vault = null;
                    return false;
                }

                _vault = dataVault;
                if (!Handle.IsCreated || Handle.Length < RequiredLength)
                    Handle = dataVault.GetBufferHandle<T>(BufferId, RequiredLength, OwnerSystemId, options);

                NativeArray<T> buffer = ResolveExisting(dataVault);
                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public void ReleaseView()
            {
                Handle = default;
                _vault = null;
            }

            public NativeArray<T> GetSubArray(int start, int length)
            {
                NativeArray<T> buffer = ResolveExisting();
                return buffer.IsCreated ? buffer.GetSubArray(start, length) : default;
            }

            public T this[int index]
            {
                get
                {
                    NativeArray<T> buffer = ResolveExisting();
                    return buffer[index];
                }
                set
                {
                    NativeArray<T> buffer = ResolveExisting();
                    buffer[index] = value;
                }
            }

            public static implicit operator NativeArray<T>(VaultBufferBinding<T> binding)
            {
                return binding.ResolveExisting();
            }

            NativeArray<T> ResolveExisting()
            {
                return ResolveExisting(_vault);
            }

            NativeArray<T> ResolveExisting(IDataVault dataVault)
            {
                if (dataVault == null || !Handle.IsCreated)
                    return default;

                return Handle.Resolve(dataVault);
            }
        }

        internal const int FaultNaN = 1 << 0;
        internal const int FaultSolidTeleport = 1 << 1;
        internal const int FaultSyncFence = 1 << 2;
        internal const int FaultDesync = 1 << 3;
        internal const int FaultStateCorrection = 1 << 4;
        internal const int FaultInvalidOriginShift = 1 << 5;
        internal const uint BodyFlagLadderActive = 1u << 0;
        internal const uint BodyFlagInSolid = 1u << 1;
        internal const uint BodyFlagMaelstromActive = 1u << 2;
        internal const uint BodyFlagSdfGradientValid = 1u << 3;
        internal const uint BodyFlagSdfLowTierGradient = 1u << 4;
        internal const uint BodyFlagSdfSqueezeIntervention = 1u << 5;
        private const int TelemetrySqueezeInterventionShift = 8;
        private const uint TelemetrySqueezeInterventionMask = 0xFFFFu << TelemetrySqueezeInterventionShift;
        private const uint SyncStateFlagCorrection = 1u << 24;
        private const uint SyncStateFlagApplyRotation = 1u << 25;
        private const int EntityCount = 1;
        private const SystemID OwnerSystemId = SystemID.GameplayPlayer;
        private const int HandTargetCount = 2;
        private const int EnvironmentProbeCount = 4;
        private const int TelemetryFrameCount = 300;
        private const int SyncFenceFrameInterval = 300;
        private const int StateCorrectionDrainLimit = 8;
        private const int IkSignalScanLimit = 8;
        private const int MaxWallContactFrameAge = 2;
        private const int MaxLadderFrameAge = 2;
        private const uint MaxSdfGradientProbeSignalAgeFrames = 2u;
        private const uint MaxEnvironmentIkSignalAgeFrames = 4u;
        private const float ReferenceWaterDensity = 1.0f;
        private const float HeavyInventoryMassKg = 55.0f;
        private const float HeavyInventoryMaskMultiplier = 0.45f;
        private const float DragCoefficientBase = 0.18f;
        private const float DragCoefficientLoadScale = 0.35f;
        private const float AdvectionVelocityScale = 0.55f;
        private const float MaelstromVelocityClamp = 8.5f;
        private const float StaminaDrainPerSecond = 0.025f;
        private const float WallImpactRollThreshold = 4.0f;
        private const float WallImpactRollDegrees = 9.0f;
        private const float WallImpactRollDecay = 8.0f;
        private const float HandProbeDistance = 1.15f;
        private const float HandProbeSideOffset = 0.23f;
        private const float HandProbeDownOffset = 0.14f;
        private const float HandContactOffset = 0.025f;
        private const float HandBraceSpeedThreshold = 5.0f;
        private const float HandBraceDistance = 1.0f;
        private const float HandBraceHoldSeconds = 0.18f;
        private const float SqueezeHoldSeconds = 0.22f;
        private const float HandBraceBlendSharpness = 18.0f;
        private const float SqueezeBlendSharpness = 20.0f;
        private const float BraceHapticCooldownSeconds = 0.28f;
        private const float GloveScrapeCooldownSeconds = 0.16f;
        private const float SdfSqueezeFeedbackCooldownSeconds = 0.12f;
        private const float SdfSqueezeMaxPushOutSpeedMetersPerSecond = 1.0f;
        private const float SdfSqueezeForwardSpeedPenalty01 = 0.6f;
        private const float SdfSqueezeFeedbackSpeedThreshold = 0.05f;
        private const float SdfSqueezeCo2EquivalentPressureKPa = 0.12f;
        private const float SdfSqueezeOxygenDrainScaleBonus = 0.35f;
        private const float SdfSqueezeRollDegrees = 3.0f;
        private const float SdfSqueezeVisualImpulseMinStress01 = 0.35f;
        private const float SdfSqueezeVisualImpulseVelocityScale = 0.35f;
        private const float SdfSqueezeVisualImpulseBaseRadiusMeters = 0.9f;
        private const float SdfSqueezeVisualImpulseExtraRadiusMeters = 1.3f;
        private const float SdfSqueezeVisualImpulseBaseLifetimeSeconds = 0.45f;
        private const float SdfSqueezeVisualImpulseExtraLifetimeSeconds = 0.65f;
        private const float SdfSqueezeSystemStressSlowThreshold01 = 0.8f;
        private const int SdfSqueezeSlowCadenceFrameInterval = 5;
        private const float BracePhaseWrap = 1024.0f;
        private const float LadderSnapRadius = 0.52f;
        private const float SolidDensityThreshold = 0.0f;
        private const int LowTierHandProbeFrameMask = 3;
        private const uint IkBraceTelemetryFlag = 1u << 16;
        private const uint IkSqueezeTelemetryFlag = 1u << 17;
        private const uint IkImpactTelemetryFlag = 1u << 18;
        private const uint IkLowTierTelemetryFlag = 1u << 19;
        private const uint IkScrapeTelemetryFlag = 1u << 20;
        private const uint AupPreShiftHaltTelemetryFlag = 1u << 21;
        private const uint AupDriftTelemetryFlag = 1u << 22;
        private const uint SdfSqueezeSlowTelemetryFlag = 1u << 23;
        private const uint SdfSqueezeNanTelemetryFlag = 1u << 24;
        private const int PreShiftHaltFrameCount = 1;
        private const float InvTwoPi = 0.15915494309f;
        private const float RollSignalEpsilonDegrees = 0.01f;
        private const uint AupWatchdogDumpMagic = 0x41555044u;
        private const uint SdfSqueezeDumpMagic = 0x5344464Bu;
        private const string AupWatchdogDumpFileName = "Dump_AUP_DETERMINISM_WATCHDOG.bin";
        private const string SdfSqueezeDumpFileName = "Dump_KCC_SDF_SQUEEZE_RESOLVER.bin";
        private static readonly int _PlayerSwimVatSpeedId = Shader.PropertyToID("_HectonSwimVatSpeedScalar");
        private static readonly int _PlayerKinematicRollId = Shader.PropertyToID("_H8PlayerKinematicRoll");

        [SerializeField] private LayerMask handProbeLayerMask = UnityEngine.Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.0f)] private float dragCoefficient = DragCoefficientBase;
        [SerializeField, Min(0.0f)] private float waterDensity = ReferenceWaterDensity;
        [SerializeField, Min(0.0f)] private float noClipSolidDensityThreshold = SolidDensityThreshold;

        private VaultBufferBinding<float3> _positions = new VaultBufferBinding<float3>(BufferID.PlayerKinematicPositions, EntityCount, OwnerSystemId);
        private VaultBufferBinding<float3> _velocities = new VaultBufferBinding<float3>(BufferID.PlayerKinematicVelocities, EntityCount, OwnerSystemId);
        private VaultBufferBinding<float3> _intendedMovement = new VaultBufferBinding<float3>(BufferID.PlayerKinematicIntendedMovements, EntityCount, OwnerSystemId);
        private VaultBufferBinding<float3> _flowVelocity = new VaultBufferBinding<float3>(BufferID.PlayerKinematicFlowVelocity, EntityCount, OwnerSystemId);
        private VaultBufferBinding<float3> _lastValidPositions = new VaultBufferBinding<float3>(BufferID.PlayerKinematicLastValidPositions, EntityCount, OwnerSystemId);
        private VaultBufferBinding<PlayerKinematicsSyncState> _stateRead = new VaultBufferBinding<PlayerKinematicsSyncState>(BufferID.PlayerKinematicSyncReadState, EntityCount, OwnerSystemId);
        private VaultBufferBinding<PlayerKinematicsSyncState> _stateWrite = new VaultBufferBinding<PlayerKinematicsSyncState>(BufferID.PlayerKinematicSyncWriteState, EntityCount, OwnerSystemId);
        private VaultBufferBinding<PlayerKinematicsHandTarget> _handTargets = new VaultBufferBinding<PlayerKinematicsHandTarget>(BufferID.PlayerKinematicHandTargets, HandTargetCount, OwnerSystemId);
        private VaultBufferBinding<PlayerKinematicsHandTarget> _smoothedHandTargets = new VaultBufferBinding<PlayerKinematicsHandTarget>(BufferID.PlayerKinematicSmoothedHandTargets, HandTargetCount, OwnerSystemId);
        private VaultBufferBinding<PlayerKinematicsRuntimeTelemetryEntry> _telemetry = new VaultBufferBinding<PlayerKinematicsRuntimeTelemetryEntry>(BufferID.PlayerKinematicRuntimeTelemetryRing, TelemetryFrameCount, OwnerSystemId);
        private VaultBufferBinding<int> _telemetryWriteIndex = new VaultBufferBinding<int>(BufferID.PlayerKinematicRuntimeTelemetryCursor, 1, OwnerSystemId);
        private VaultBufferBinding<int> _faultFlags = new VaultBufferBinding<int>(BufferID.PlayerKinematicFaultFlags, 1, OwnerSystemId);
        private VaultBufferBinding<RaycastCommand> _handProbeCommands = new VaultBufferBinding<RaycastCommand>(BufferID.PlayerKinematicHandProbeCommands, EnvironmentProbeCount, OwnerSystemId);
        private VaultBufferBinding<RaycastHit> _handProbeHits = new VaultBufferBinding<RaycastHit>(BufferID.PlayerKinematicHandProbeHits, EnvironmentProbeCount, OwnerSystemId);
        private VaultBufferBinding<SdfSqueezeResult> _sdfSqueezeResults = new VaultBufferBinding<SdfSqueezeResult>(BufferID.PlayerKinematicSdfSqueezeResults, EntityCount, OwnerSystemId);
        private JobHandle _handProbeHandle;
        private JobHandle _handPlacementHandle;
        private bool _handProbePending;
        private bool _handPlacementPending;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredFast;
        private bool _registeredLate;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _registeredScalability;
        private bool _dumpWrittenForFault;
        private bool _desyncDumpWritten;
        private bool _stateWriteReady;
        private Rigidbody _body;
        private HectonPlayerMovement _movement;
        private HectonPlayerMotor _motor;
        private PlayerInventory _inventory;
        private HectonSurvivalSystem _survival;
        private ContextualPhysicalIkRig _ikRig;
        private IDataVault _dataVault;
        private IGasDynamicsSolver _gasDynamics;
        private HectonFluidEngine _fluid;
        private HectonVoxelEngine _voxelEngine;
        private Transform _cachedTransform;
        private Transform _cameraTransform;
        private float _rollDegrees;
        private float _rollVelocityDegrees;
        private float _rollPhaseRadians;
        private float _lastVatSpeedScalar = -1.0f;
        private float _lastPushedRollDegrees = 99999.0f;
        private int _nextColdRebindFrame;
        private int _cadenceSalt;
        private uint _lastConsumedSqueezeSignalFrame;
        private uint _lastConsumedSqueezeSignalSourceHash;
        private HectonQualityTier _cachedScalabilityTier = HectonQualityTier.Unknown;
        private uint _sourceId;
        private PlayerKinematicsAccumulatorState _accumulatorState;
        private InputStateSignal _lastInputStateSignal;
        private Vector4 _lastGpuFlowResolution;
        private Vector4 _lastGpuFlowCenter;
        private Vector4 _lastGpuFlowSpacing;
        private float3 _impactBracePoint;
        private float3 _impactBraceNormal;
        private float3 _lastProbeSourcePosition;
        private float3 _lastProbeSourceForward;
        private float3 _lastProbeSourceRight;
        private float3 _lastProbeSourceUp;
        private float3 _lastProbeVelocity;
        private float _braceHoldTimer;
        private float _braceBlend;
        private float _squeezeHoldTimer;
        private float _squeezeTargetBlend;
        private float _squeezeBlend;
        private float _cachedStress01;
        private float _bracePhase;
        private float _lastIkDeltaTime = 0.0166667f;
        private float _braceHapticCooldown;
        private float _scrapeAcousticCooldown;
        private float _sdfSqueezeFeedbackCooldown;
        private float _lastSdfSqueezeStress01;
        private float3 _lastSdfSqueezeNormal;
        private SdfSqueezeResult _lastSdfSqueezeResult;
        private int _sdfSqueezeSlowHoldFrames;
        private uint _lastConsumedPlayerStressFrame;
        private uint _lastConsumedSystemStressFrame;
        private float _cachedSystemStress01;
        private bool _hasImpactBracePoint;
        private bool _lastProbeLowTier;
        private bool _wasBraceActive;

        private void Awake()
        {
            _cachedTransform = transform;
            _body = GetComponent<Rigidbody>();
            _movement = GetComponent<HectonPlayerMovement>();
            _motor = GetComponent<HectonPlayerMotor>();
            _sourceId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            _cadenceSalt = unchecked((int)_sourceId);
            TryGetComponent(out _inventory);
            TryGetComponent(out _survival);
            RebindServices(allowHierarchyLookup: true);
            AllocateNativeState();
        }

        private void OnEnable()
        {
            ResetDeterminismSessionState();
            WarmRuntimeStateOnEnable();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            ClearRollSignal();
            UnregisterRuntime();
            PumpHandEnvironmentJobs(forceComplete: true, allowFinalizeOutsideSwap: false);
            ClearHandTargets();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            PumpHandEnvironmentJobs(forceComplete: true, allowFinalizeOutsideSwap: false);
            ClearHandTargets();
            DisposeNativeState();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!HasCoreEntityStorage() || _body == null)
                return;

            if (_cachedTransform == null)
                _cachedTransform = transform;

            RebindColdIfMissing();
            ConsumeAupPreShiftSignals();
            if (_accumulatorState.PreShiftHaltFrames > 0)
            {
                _accumulatorState.PreShiftHaltFrames--;
                PublishAupPreShiftHaltState();
                return;
            }

            HectonQualityTier scalabilityTier = ResolveScalabilityTier();
            if (MovementOwnsKinematicAuthority())
            {
                byte externalFlags = KccVelocitySignal.FlagMovementAuthorityExternal;
                if (IsLowTier(scalabilityTier))
                    externalFlags |= KccVelocitySignal.FlagLowTier;

                float3 rawAuthorityPosition = ToFloat3(_body.position);
                float3 rawAuthorityVelocity = ToFloat3(_body.linearVelocity);
                bool authorityInputInvalid =
                    !math.all(math.isfinite(rawAuthorityPosition)) ||
                    !math.all(math.isfinite(rawAuthorityVelocity));
                float3 authorityPosition = SanitizeFloat3(rawAuthorityPosition, ReadLastValidPosition());
                float3 authorityVelocity = SanitizeFloat3(rawAuthorityVelocity, float3.zero);
                if (authorityInputInvalid)
                    AddFaultFlag(FaultNaN);

                PublishKccVelocitySignal(
                    SnapMillimeter(authorityPosition),
                    SnapMillimeter(authorityVelocity),
                    externalFlags);
                TickInertiaRoll(fixedDeltaTime);
                return;
            }

            SnapshotInputs();
            SnapshotGpuFlow();
            SnapshotVoxelSolid(out byte inSolid, out float solidDensity);
            byte lowTier = IsLowTier(scalabilityTier) ? (byte)1 : (byte)0;
            byte sdfGradientProbeRequested = ResolveSdfGradientProbeRequest();
            NativeArray<byte> sdfTexture3D = default;
            int3 sdfDimensions = default;
            float3 sdfOrigin = float3.zero;
            float3 sdfCellSize = float3.zero;
            float sdfRange = 0.0f;
            byte sdfSampleMode = lowTier != 0
                ? PlayerKinematicsBodyJob.SdfSampleModeTetra4
                : PlayerKinematicsBodyJob.SdfSampleModeAxis6;
            float3 rawBodyPosition = ToFloat3(_body.position);
            float3 rawBodyVelocity = ToFloat3(_body.linearVelocity);
            float3 bodyPosition = SanitizeFloat3(rawBodyPosition, ReadLastValidPosition());
            float3 bodyVelocity = SanitizeFloat3(rawBodyVelocity, float3.zero);
            bool rawBodyStateInvalid = !math.all(math.isfinite(rawBodyPosition)) || !math.all(math.isfinite(rawBodyVelocity));
            Vector3 safeBodyPosition = ToVector3(bodyPosition);
            bool needsSdfPayload = inSolid != 0 || sdfGradientProbeRequested != 0 || lowTier == 0 || _sdfSqueezeSlowHoldFrames > 0;

            if (needsSdfPayload)
            {
                SnapshotSdfPayload(
                    safeBodyPosition,
                    out sdfTexture3D,
                    out sdfDimensions,
                    out sdfOrigin,
                    out sdfCellSize,
                    out sdfRange,
                    out sdfSampleMode);
            }
            SnapshotLadder(out byte ladderActive, out float3 ladderPoint);

            _positions[0] = bodyPosition;
            _velocities[0] = bodyVelocity;
            _flowVelocity[0] = SanitizeFloat3(ResolveCurrentAdvection(safeBodyPosition), float3.zero);
            SdfSqueezeResult sdfSqueezeResult;
            if (TryApplySdfSqueeze(
                    fixedDeltaTime,
                    lowTier,
                    sdfTexture3D,
                    sdfDimensions,
                    sdfOrigin,
                    sdfCellSize,
                    sdfRange,
                    sdfSampleMode,
                    ref inSolid,
                    ref sdfGradientProbeRequested,
                    ref bodyPosition,
                    ref bodyVelocity,
                    ref safeBodyPosition,
                    out sdfSqueezeResult))
            {
                _flowVelocity[0] = SanitizeFloat3(ResolveCurrentAdvection(safeBodyPosition), float3.zero);
            }
            else if ((sdfSqueezeResult.Flags & SdfSqueezeResult.FlagNaNFallback) != 0u)
            {
                AddFaultFlag(FaultNaN);
            }

            NativeArray<WhirlpoolFlow> activeMaelstroms = default;
            int activeMaelstromCount = 0;
            if (_fluid != null &&
                _fluid.TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow> fluidMaelstroms, out int fluidMaelstromCount))
            {
                activeMaelstroms = fluidMaelstroms;
                activeMaelstromCount = fluidMaelstromCount;
            }

            var bodyJob = new PlayerKinematicsBodyJob
            {
                Positions = _positions,
                Velocities = _velocities,
                IntendedMovement = _intendedMovement,
                FlowVelocity = _flowVelocity,
                LastValidPositions = _lastValidPositions,
                ActiveMaelstroms = activeMaelstroms,
                VoxelSdfTexture3D = sdfTexture3D,
                Telemetry = _telemetry,
                TelemetryWriteIndex = _telemetryWriteIndex,
                FaultFlags = _faultFlags,
                ActiveMaelstromCount = activeMaelstromCount,
                VoxelSdfDimensions = sdfDimensions,
                DeltaTime = fixedDeltaTime,
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                EquipmentDragMultiplier = ResolveEquipmentDragMultiplier(),
                MaelstromVelocityClamp = MaelstromVelocityClamp,
                LadderSnapRadiusSq = LadderSnapRadius * LadderSnapRadius,
                LadderPoint = ladderPoint,
                VoxelSdfOrigin = sdfOrigin,
                VoxelSdfCellSize = sdfCellSize,
                TargetAup = bodyPosition,
                SolidDensity = solidDensity,
                VoxelSdfRange = sdfRange,
                SdfSampleStepMeters = ResolveSdfSampleStepMeters(sdfCellSize),
                LowMaelstromTier = lowTier,
                SdfSampleMode = sdfSampleMode,
                SdfGradientProbeRequested = sdfGradientProbeRequested,
                Frame = (uint)Time.frameCount,
                RuntimeFlags = ResolveBodyFlags(ladderActive, inSolid) |
                               math.select(0u, BodyFlagMaelstromActive, activeMaelstromCount > 0) |
                              math.select(0u, BodyFlagSdfSqueezeIntervention, SdfSqueezeResult.IsResultActive(in sdfSqueezeResult))
            };
            bodyJob.Run();
            if (rawBodyStateInvalid)
                AddFaultFlag(FaultNaN);

            float3 resolvedPosition3 = SnapMillimeter(_positions[0]);
            float3 resolvedVelocity3 = SnapMillimeter(_velocities[0]);
            _positions[0] = resolvedPosition3;
            _velocities[0] = resolvedVelocity3;
            Vector3 resolvedPosition = ToVector3(resolvedPosition3);
            Vector3 resolvedVelocity = ToVector3(resolvedVelocity3);
            int faultFlags = ReadFaultFlags();
            StageStateWrite(resolvedPosition3, resolvedVelocity3, _body.rotation, (uint)faultFlags);
            PublishKccVelocitySignal(
                resolvedPosition3,
                resolvedVelocity3,
                lowTier != 0 ? KccVelocitySignal.FlagLowTier : (byte)0);
            if (SdfSqueezeResult.IsResultActive(in sdfSqueezeResult))
                PublishSdfSqueezeSignals(in sdfSqueezeResult, resolvedPosition3, resolvedVelocity3, lowTier);
            if (faultFlags == 0)
                _dumpWrittenForFault = false;

            TickInertiaRoll(fixedDeltaTime);
            PublishMovementAcoustics(resolvedPosition, resolvedVelocity3);
            TickStamina();
            DumpFaultTelemetryIfNeeded();
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ApplyPendingStateCorrections();
            CommitStateWrite();
        }

        public void FastTick(float deltaTime)
        {
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegative(deltaTime));
            _lastIkDeltaTime = safeDeltaTime;
            ConsumeEnvironmentIkSignals();
            TickEnvironmentIkState(safeDeltaTime);
            PumpHandEnvironmentJobs(forceComplete: false, allowFinalizeOutsideSwap: true);
            ScheduleHandProbes();
            ConsumeSqueezeTelemetrySignal();

            _accumulatorState.FastTickCounter++;
            if (_accumulatorState.FastTickCounter < SyncFenceFrameInterval)
                return;

            _accumulatorState.FastTickCounter = 0;
            PublishSyncFence();
        }

        public void LateFrameTick()
        {
            PumpHandEnvironmentJobs(forceComplete: false, allowFinalizeOutsideSwap: false);

            if (!MovementOwnsKinematicAuthority())
                PushVatScalar();
            PushRollSignal();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!HasMotionSoaStorage())
                return;

            float3 offset = ToFloat3(shiftData.ShiftOffset);
            float offsetLengthSq = math.lengthsq(offset);
            if (!math.all(math.isfinite(offset)) ||
                !math.isfinite(offsetLengthSq) ||
                offsetLengthSq > 100000000.0f)
            {
                AddFaultFlag(FaultInvalidOriginShift);
                DumpFaultTelemetryIfNeeded();
                return;
            }

            _positions[0] = SanitizeFloat3(_positions[0] - offset, float3.zero);
            _lastValidPositions[0] = SanitizeFloat3(_lastValidPositions[0] - offset, float3.zero);
            if (HasSyncStateReadStorage())
            {
                PlayerKinematicsSyncState state = _stateRead[0];
                state.Position = SanitizeFloat3(state.Position - offset, ReadLastValidPosition());
                state = RehashState(state);
                _stateRead[0] = state;
            }

            if (HasSyncStateWriteStorage())
            {
                PlayerKinematicsSyncState state = _stateWrite[0];
                state.Position = SanitizeFloat3(state.Position - offset, ReadLastValidPosition());
                state = RehashState(state);
                _stateWrite[0] = state;
            }

            if (_telemetry.IsCreated)
            {
                for (int i = 0; i < _telemetry.Length; i++)
                {
                    PlayerKinematicsRuntimeTelemetryEntry entry = _telemetry[i];
                    entry.Position = SanitizeFloat3(entry.Position - offset, float3.zero);
                    _telemetry[i] = entry;
                }
            }

            if (_handTargets.IsCreated)
            {
                for (int i = 0; i < _handTargets.Length; i++)
                {
                    PlayerKinematicsHandTarget target = _handTargets[i];
                    if (target.Hit != 0)
                    {
                        target.Position = SanitizeFloat3(target.Position - offset, float3.zero);
                        _handTargets[i] = target;
                    }
                }
            }

            if (_smoothedHandTargets.IsCreated)
            {
                for (int i = 0; i < _smoothedHandTargets.Length; i++)
                {
                    PlayerKinematicsHandTarget target = _smoothedHandTargets[i];
                    if (target.Hit != 0)
                    {
                        target.Position = SanitizeFloat3(target.Position - offset, float3.zero);
                        _smoothedHandTargets[i] = target;
                    }
                }
            }

            if (_sdfSqueezeResults.IsCreated)
            {
                for (int i = 0; i < _sdfSqueezeResults.Length; i++)
                {
                    SdfSqueezeResult result = _sdfSqueezeResults[i];
                    if (SdfSqueezeResult.IsResultActive(in result))
                    {
                        result.Position = SanitizeFloat3(result.Position - offset, float3.zero);
                        _sdfSqueezeResults[i] = result;
                    }
                }
            }

            if (SdfSqueezeResult.IsResultActive(in _lastSdfSqueezeResult))
                _lastSdfSqueezeResult.Position = SanitizeFloat3(_lastSdfSqueezeResult.Position - offset, float3.zero);

            if (_hasImpactBracePoint)
                _impactBracePoint = SanitizeFloat3(_impactBracePoint - offset, float3.zero);
            _lastProbeSourcePosition = SanitizeFloat3(_lastProbeSourcePosition - offset, float3.zero);
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                PumpHandEnvironmentJobs(forceComplete: true, allowFinalizeOutsideSwap: false);
                DisposeNativeState();
                if (currentService != null)
                {
                    AllocateNativeState();
                    WarmRuntimeStateOnEnable();
                }
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.Player ||
                serviceSlot == GlobalRegistryServiceSlot.PlayerMotor ||
                serviceSlot == GlobalRegistryServiceSlot.GasDynamicsRuntime)
            {
                RebindServices(allowHierarchyLookup: false);
            }
        }

        public void OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            _cachedScalabilityTier = payload.CurrentQualityTier;
        }

        void IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent payload)
        {
            OnScalabilityChanged(in payload);
        }

        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (!playerRoot.TryGetComponent(out PlayerKinematicsRuntime _))
                playerRoot.AddComponent<PlayerKinematicsRuntime>(); // COLD ALLOC: PlayerKinematicsRuntime[1] - player kinematics bridge install - owner: PlayerRuntimeContextService
        }

        private void AllocateNativeState()
        {
            if (_positions.IsCreated)
                return;

            IDataVault dataVault = _dataVault;
            _ = _positions.Ensure(dataVault);
            _ = _velocities.Ensure(dataVault);
            _ = _intendedMovement.Ensure(dataVault);
            _ = _flowVelocity.Ensure(dataVault);
            _ = _lastValidPositions.Ensure(dataVault);
            _ = _stateRead.Ensure(dataVault);
            _ = _stateWrite.Ensure(dataVault);
            _ = _handTargets.Ensure(dataVault);
            _ = _smoothedHandTargets.Ensure(dataVault);
            _ = _telemetry.Ensure(dataVault);
            _ = _telemetryWriteIndex.Ensure(dataVault);
            _ = _faultFlags.Ensure(dataVault);
            _ = _handProbeCommands.Ensure(dataVault);
            _ = _handProbeHits.Ensure(dataVault);
            _ = _sdfSqueezeResults.Ensure(dataVault);
            if (!HasKinematicsStorage() || !HasSyncStateWriteStorage())
                return;

            float3 start = _body != null ? ToFloat3(_body.position) : ToFloat3(transform.position);
            start = SnapMillimeter(SanitizeFloat3(start, float3.zero));
            _positions[0] = start;
            _lastValidPositions[0] = start;
            quaternion rotation = _body != null
                ? ToQuaternion(_body.rotation)
                : ToQuaternion(transform.rotation);
            StageStateWrite(start, float3.zero, rotation, 0u);
            CommitStateWrite();
        }

        private void DisposeNativeState()
        {
            _positions.ReleaseView();
            _velocities.ReleaseView();
            _intendedMovement.ReleaseView();
            _flowVelocity.ReleaseView();
            _lastValidPositions.ReleaseView();
            _stateRead.ReleaseView();
            _stateWrite.ReleaseView();
            _handTargets.ReleaseView();
            _smoothedHandTargets.ReleaseView();
            _telemetry.ReleaseView();
            _telemetryWriteIndex.ReleaseView();
            _faultFlags.ReleaseView();
            _handProbeCommands.ReleaseView();
            _handProbeHits.ReleaseView();
            _sdfSqueezeResults.ReleaseView();
            ResetDeterminismSessionState();
        }

        private void RegisterRuntime()
        {
            if (!_registeredFixed)
            {
                GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = true;
            }

            if (!_registeredPostFixed)
            {
                GlobalRegistry.RegisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = true;
            }

            if (!_registeredFast)
            {
                GlobalRegistry.RegisterFastTickable(this, PriorityLayer.Player);
                _registeredFast = true;
            }

            if (!_registeredLate)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLate = true;
            }

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap)
            {
                GlobalRegistry.RegisterHotSwapListener(this);
                _registeredHotSwap = true;
            }

            if (!_registeredScalability)
            {
                ScalabilityEvents.Register(this);
                _registeredScalability = true;
            }
        }

        private void UnregisterRuntime()
        {
            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = false;
            }

            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = false;
            }

            if (_registeredFast)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Player);
                _registeredFast = false;
            }

            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLate = false;
            }

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            if (_registeredScalability)
            {
                ScalabilityEvents.Unregister(this);
                _registeredScalability = false;
            }
        }

        private void RebindServices(bool allowHierarchyLookup)
        {
            RebindRegistryServices();
            if (_motor == null)
                _motor = GlobalRegistry.PlayerMotor != null ? GlobalRegistry.PlayerMotor : GetComponent<HectonPlayerMotor>();
            if (_inventory == null)
                TryGetComponent(out _inventory);
            if (_survival == null)
                TryGetComponent(out _survival);
            // Cold-only child lookup; runtime hot-swap rebinds pass false.
            if (allowHierarchyLookup && _ikRig == null)
                _ikRig = GetComponentInChildren<ContextualPhysicalIkRig>(true);

        }

        private void WarmRuntimeStateOnEnable()
        {
            if (!HasCoreEntityStorage())
                return;

            Vector3 runtimePosition = _body != null
                ? _body.position
                : (_cachedTransform != null ? _cachedTransform.position : transform.position);
            float3 position = SanitizeFloat3(ToFloat3(runtimePosition), ReadLastValidPosition());
            if (!math.all(math.isfinite(position)))
                return;
            position = SnapMillimeter(position);

            float3 velocity = _body != null ? ToFloat3(_body.linearVelocity) : float3.zero;
            velocity = SnapMillimeter(SanitizeFloat3(velocity, float3.zero));
            _positions[0] = position;
            _velocities[0] = velocity;
            _lastValidPositions[0] = position;
            StageStateWrite(position, velocity, _body != null ? ToQuaternion(_body.rotation) : quaternion.identity, 0u);
            CommitStateWrite();

            ClearFaultFlags();
            _dumpWrittenForFault = false;
        }

        private void ResetDeterminismSessionState()
        {
            _stateWriteReady = false;
            _accumulatorState = default;
            _lastConsumedSqueezeSignalFrame = 0u;
            _lastConsumedSqueezeSignalSourceHash = 0u;
            _lastInputStateSignal = default;
            _dumpWrittenForFault = false;
            _desyncDumpWritten = false;
            _rollPhaseRadians = 0.0f;
            _lastGpuFlowResolution = Vector4.zero;
            _lastGpuFlowCenter = Vector4.zero;
            _lastGpuFlowSpacing = Vector4.zero;
            _impactBracePoint = float3.zero;
            _impactBraceNormal = float3.zero;
            _lastProbeSourcePosition = float3.zero;
            _lastProbeSourceForward = new float3(0.0f, 0.0f, 1.0f);
            _lastProbeSourceRight = new float3(1.0f, 0.0f, 0.0f);
            _lastProbeSourceUp = new float3(0.0f, 1.0f, 0.0f);
            _lastProbeVelocity = float3.zero;
            _braceHoldTimer = 0.0f;
            _braceBlend = 0.0f;
            _squeezeHoldTimer = 0.0f;
            _squeezeTargetBlend = 0.0f;
            _squeezeBlend = 0.0f;
            _cachedStress01 = 0.0f;
            _bracePhase = 0.0f;
            _braceHapticCooldown = 0.0f;
            _scrapeAcousticCooldown = 0.0f;
            _sdfSqueezeFeedbackCooldown = 0.0f;
            _lastSdfSqueezeStress01 = 0.0f;
            _lastSdfSqueezeNormal = float3.zero;
            _lastSdfSqueezeResult = default;
            _sdfSqueezeSlowHoldFrames = 0;
            _lastConsumedPlayerStressFrame = 0u;
            _lastConsumedSystemStressFrame = 0u;
            _cachedSystemStress01 = 0.0f;
            _hasImpactBracePoint = false;
            _lastProbeLowTier = false;
            _wasBraceActive = false;
            if (_intendedMovement.IsCreated && _intendedMovement.Length >= EntityCount)
                _intendedMovement[0] = float3.zero;
            if (_flowVelocity.IsCreated && _flowVelocity.Length >= EntityCount)
                _flowVelocity[0] = float3.zero;
            ClearFaultFlags();
            if (_telemetryWriteIndex.IsCreated && _telemetryWriteIndex.Length > 0)
                _telemetryWriteIndex[0] = 0;
            if (_telemetry.IsCreated)
            {
                for (int i = 0; i < _telemetry.Length; i++)
                    _telemetry[i] = default;
            }
        }

        private void RebindRegistryServices()
        {
            _dataVault = GlobalRegistry.DataVault;
            _gasDynamics = GlobalRegistry.GasDynamics;
            _fluid = GlobalRegistry.Fluid;
            _voxelEngine = GlobalRegistry.VoxelEngine;
            if (_motor == null && GlobalRegistry.PlayerMotor != null)
                _motor = GlobalRegistry.PlayerMotor;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _cameraTransform = playerContext != null && playerContext.PlayerCamera != null
                ? playerContext.PlayerCamera.transform
                : null;
            _cachedScalabilityTier = GlobalRegistry.ScalabilityTier;
        }

        private void RebindColdIfMissing()
        {
            if (_fluid != null && _voxelEngine != null && _cameraTransform != null)
                return;

            int frame = Time.frameCount;
            if (frame < _nextColdRebindFrame)
                return;

            _nextColdRebindFrame = frame + 64;
            RebindRegistryServices();
        }

        private void SnapshotInputs()
        {
            InputStateSignal inputSignal = _lastInputStateSignal;
            ReadOnlySpan<InputStateSignal> inputSignals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            int inputSignalCount = inputSignals.Length;
            if (inputSignalCount > 0)
            {
                inputSignal = inputSignals[inputSignalCount - 1];
                _lastInputStateSignal = inputSignal;
            }

            float2 planar = inputSignal.State.Move;
            planar = math.select(planar, float2.zero, !math.all(math.isfinite(planar)));
            float planarSq = math.lengthsq(planar);
            if (!math.isfinite(planarSq))
                planar = float2.zero;
            else if (planarSq > 1.0f)
                planar *= math.rsqrt(math.max(planarSq, 0.000001f));

            float3 forward = _cameraTransform != null ? ToFloat3(_cameraTransform.forward) : ToFloat3(_cachedTransform.forward);
            float3 right = _cameraTransform != null ? ToFloat3(_cameraTransform.right) : ToFloat3(_cachedTransform.right);
            forward.y = 0.0f;
            right.y = 0.0f;
            forward = SafeNormalize(forward, new float3(0.0f, 0.0f, 1.0f));
            right = SafeNormalize(right, new float3(1.0f, 0.0f, 0.0f));
            float vertical = SanitizeSignedUnit(inputSignal.State.VerticalAxis);
            float3 intended = (right * planar.x) + (forward * planar.y) + new float3(0.0f, vertical, 0.0f);
            _intendedMovement[0] = SanitizeFloat3(intended, float3.zero);
        }

        private void SnapshotGpuFlow()
        {
            int frame = Time.frameCount;
            if (_accumulatorState.LastGpuFlowFrame != 0u && (frame & ResolveGpuFlowProbeFrameMask()) != 0)
                return;

            if (_fluid == null ||
                !_fluid.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer _,
                    out Vector4 gridResolution,
                    out Vector4 flowCenter,
                    out Vector4 flowSpacing))
            {
                _accumulatorState.LastGpuFlowFrame = 0u;
                return;
            }

            float3 flowResolution = new float3(gridResolution.x, gridResolution.y, gridResolution.z);
            float3 flowSpacingMeters = new float3(flowSpacing.x, flowSpacing.y, flowSpacing.z);
            if (!IsFinite(gridResolution) ||
                !IsFinite(flowCenter) ||
                !IsFinite(flowSpacing) ||
                math.any(flowResolution < new float3(1.0f)) ||
                math.any(math.abs(flowSpacingMeters) <= new float3(0.0001f)))
            {
                _lastGpuFlowResolution = Vector4.zero;
                _lastGpuFlowCenter = Vector4.zero;
                _lastGpuFlowSpacing = Vector4.zero;
                _accumulatorState.LastGpuFlowFrame = 0u;
                return;
            }

            _lastGpuFlowResolution = gridResolution;
            _lastGpuFlowCenter = flowCenter;
            _lastGpuFlowSpacing = flowSpacing;
            _accumulatorState.LastGpuFlowFrame = (uint)frame;
        }

        private void SnapshotVoxelSolid(out byte inSolid, out float density)
        {
            inSolid = 0;
            density = 0.0f;
            if (_voxelEngine == null || _body == null)
                return;

            Vector3 position = _body.position;
            float3 positionFloat = ToFloat3(position);
            if (!math.all(math.isfinite(positionFloat)))
            {
                density = 0.0f;
                return;
            }

            if (!_voxelEngine.TryGetNearestActiveVolume(position, out HectonVoxelVolume volume) || volume == null)
                return;

            if (!IsInsidePublishedVoxelSdfBounds(volume, position))
                return;

            if (volume.TrySampleDensity(position, out density, out float density01))
            {
                density = math.select(density, 0.0f, !math.isfinite(density));
                density01 = SanitizeUnit(density01);
                if (density > noClipSolidDensityThreshold || density01 >= 0.5f)
                    inSolid = 1;
            }
        }

        private void SnapshotSdfPayload(
            Vector3 targetRuntimePosition,
            out NativeArray<byte> sdfTexture3D,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 voxelCellSize,
            out float sdfRange,
            out byte sampleMode)
        {
            sdfTexture3D = default;
            gridDimensions = default;
            volumeOrigin = float3.zero;
            voxelCellSize = float3.zero;
            sdfRange = 0.0f;
            sampleMode = IsLowTier(ResolveScalabilityTier())
                ? PlayerKinematicsBodyJob.SdfSampleModeTetra4
                : PlayerKinematicsBodyJob.SdfSampleModeAxis6;

            if (_voxelEngine == null)
                return;

            if (!_voxelEngine.TryGetNearestActiveVolume(targetRuntimePosition, out HectonVoxelVolume volume) ||
                volume == null ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> publishedSdf,
                    out Vector3Int publishedDimensions,
                    out Vector3 publishedOrigin,
                    out Vector3 publishedCellSize,
                    out float publishedRange,
                    out int _))
            {
                return;
            }

            int3 resolvedDimensions = new int3(publishedDimensions.x, publishedDimensions.y, publishedDimensions.z);
            if (!PlayerKinematicsBodyJob.TryResolveSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                publishedSdf.Length < expectedLength)
            {
                return;
            }

            NativeArray<byte> resolvedSdf = publishedSdf;
            var dataVault = _dataVault;
            if (dataVault != null &&
                dataVault.TryGetBuffer<byte>(BufferID.VoxelSdfTexture3D, out NativeArray<byte> vaultSdf) &&
                vaultSdf.IsCreated &&
                vaultSdf.Length >= expectedLength)
            {
                resolvedSdf = vaultSdf;
            }

            sdfTexture3D = resolvedSdf;
            gridDimensions = resolvedDimensions;
            float3 safeOrigin = ToFloat3(publishedOrigin);
            float3 safeCellSize = ToFloat3(publishedCellSize);
            float safeRange = SanitizeNonNegative(publishedRange);
            if (!math.all(math.isfinite(safeOrigin)) ||
                !math.all(math.isfinite(safeCellSize)) ||
                math.any(math.abs(safeCellSize) <= new float3(0.0001f)) ||
                safeRange <= 0.0001f)
            {
                sdfTexture3D = default;
                gridDimensions = default;
                return;
            }

            volumeOrigin = safeOrigin;
            voxelCellSize = safeCellSize;
            sdfRange = safeRange;
        }

        private bool TryApplySdfSqueeze(
            float fixedDeltaTime,
            byte lowTier,
            NativeArray<byte> sdfTexture3D,
            int3 sdfDimensions,
            float3 sdfOrigin,
            float3 sdfCellSize,
            float sdfRange,
            byte sdfSampleMode,
            ref byte inSolid,
            ref byte sdfGradientProbeRequested,
            ref float3 bodyPosition,
            ref float3 bodyVelocity,
            ref Vector3 safeBodyPosition,
            out SdfSqueezeResult result)
        {
            result = default;
            if (!HasMotionSoaStorage() ||
                !_sdfSqueezeResults.IsCreated ||
                _sdfSqueezeResults.Length <= 0 ||
                (inSolid == 0 && sdfGradientProbeRequested == 0))
            {
                return false;
            }

            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegative(fixedDeltaTime));
            ConsumeSystemStressSignals();
            float systemStress01 = SanitizeUnit(_cachedSystemStress01);
            bool slowCadence = systemStress01 > SdfSqueezeSystemStressSlowThreshold01;
            if (!IsValidSdfPayload(sdfTexture3D, sdfDimensions, sdfOrigin, sdfCellSize, sdfRange))
            {
                return slowCadence &&
                       inSolid != 0 &&
                       TryApplyCachedSdfSqueeze(safeDeltaTime, lowTier, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition, out result);
            }

            bool runSampleNow = !slowCadence ||
                                _sdfSqueezeSlowHoldFrames <= 0 ||
                                ((Time.frameCount + _cadenceSalt) % SdfSqueezeSlowCadenceFrameInterval) == 0;
            if (!runSampleNow &&
                TryApplyCachedSdfSqueeze(safeDeltaTime, lowTier, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition, out result))
            {
                return true;
            }

            AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(safeBodyPosition);
            AbsoluteUniversePosition sampleAup = TryReadPlayerKinematicStateFromVault(out AbsoluteUniversePosition vaultAup)
                ? vaultAup
                : bodyAup;
            WritePlayerKinematicStateToVault(in bodyAup, bodyVelocity);

            _sdfSqueezeResults[0] = default;
            var squeezeJob = new SdfSqueezeJob
            {
                Positions = _positions,
                Velocities = _velocities,
                IntendedMovement = _intendedMovement,
                VoxelSdfTexture3D = sdfTexture3D,
                Results = _sdfSqueezeResults,
                VoxelSdfDimensions = sdfDimensions,
                VoxelSdfOrigin = sdfOrigin,
                VoxelSdfCellSize = sdfCellSize,
                TargetAupAbsolute = sampleAup.ToAbsoluteDouble3(),
                FloatingOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                VoxelSdfRange = sdfRange,
                SdfSampleStepMeters = ResolveSdfSampleStepMeters(sdfCellSize),
                DeltaTime = safeDeltaTime,
                MaxPushOutSpeedMetersPerSecond = SdfSqueezeMaxPushOutSpeedMetersPerSecond,
                SpeedPenalty01 = SdfSqueezeForwardSpeedPenalty01,
                SystemStress01 = systemStress01,
                SampleMode = lowTier != 0 ? (byte)SdfSqueezeSampleMode.Tetra4 : sdfSampleMode,
                LowTier = lowTier,
                SlowCadence = slowCadence ? (byte)1 : (byte)0,
                Frame = unchecked((uint)Time.frameCount)
            };
            squeezeJob.Run();
            result = _sdfSqueezeResults[0];

            if ((result.Flags & SdfSqueezeResult.FlagNaNFallback) != 0u)
                WriteSdfSqueezeTelemetry(in result, bodyPosition, bodyVelocity);

            if (!SdfSqueezeResult.IsResultActive(in result))
                return false;

            _lastSdfSqueezeResult = result;
            _sdfSqueezeSlowHoldFrames = slowCadence ? SdfSqueezeSlowCadenceFrameInterval - 1 : 0;
            ApplySdfSqueezeResultToRuntime(in result, lowTier, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition);
            return true;
        }

        private bool TryApplyCachedSdfSqueeze(
            float fixedDeltaTime,
            byte lowTier,
            ref byte inSolid,
            ref byte sdfGradientProbeRequested,
            ref float3 bodyPosition,
            ref float3 bodyVelocity,
            ref Vector3 safeBodyPosition,
            out SdfSqueezeResult result)
        {
            result = default;
            if (_sdfSqueezeSlowHoldFrames <= 0 ||
                !SdfSqueezeResult.IsResultActive(in _lastSdfSqueezeResult) ||
                inSolid == 0 ||
                !HasMotionSoaStorage())
            {
                return false;
            }

            _sdfSqueezeSlowHoldFrames--;
            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegative(fixedDeltaTime));
            float3 normal = SafeNormalize(_lastSdfSqueezeResult.Normal, float3.zero);
            if (math.lengthsq(normal) <= 0.000001f)
                return false;

            float pushSpeed = math.min(
                SdfSqueezeMaxPushOutSpeedMetersPerSecond,
                SanitizeNonNegative(_lastSdfSqueezeResult.PushSpeed));
            float pushMeters = pushSpeed * safeDeltaTime;
            float3 position = SnapMillimeter(SanitizeFloat3(_positions[0] + normal * pushMeters, _positions[0]));
            float3 velocity = SnapMillimeter(ApplyForwardSpeedPenalty(_velocities[0], ReadIntendedMovementSnapshot(), SdfSqueezeForwardSpeedPenalty01) + normal * pushSpeed);
            _positions[0] = position;
            _velocities[0] = velocity;

            result = _lastSdfSqueezeResult;
            result.Position = position;
            result.Velocity = velocity;
            result.PushMeters = pushMeters;
            result.PushSpeed = pushSpeed;
            result.Frame = unchecked((uint)Time.frameCount);
            result.Flags |= SdfSqueezeResult.FlagSlowCadence;
            if (lowTier != 0)
                result.Flags |= SdfSqueezeResult.FlagLowTier;

            ApplySdfSqueezeResultToRuntime(in result, lowTier, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition);
            return true;
        }

        private void ApplySdfSqueezeResultToRuntime(
            in SdfSqueezeResult result,
            byte lowTier,
            ref byte inSolid,
            ref byte sdfGradientProbeRequested,
            ref float3 bodyPosition,
            ref float3 bodyVelocity,
            ref Vector3 safeBodyPosition)
        {
            bodyPosition = result.Position;
            bodyVelocity = result.Velocity;
            safeBodyPosition = ToVector3(bodyPosition);
            inSolid = 0;
            sdfGradientProbeRequested = 1;
            _lastSdfSqueezeNormal = SafeNormalize(result.Normal, _lastSdfSqueezeNormal);
            _lastSdfSqueezeStress01 = math.max(_lastSdfSqueezeStress01, SanitizeUnit(result.Stress01));
            if (lowTier != 0)
                _lastSdfSqueezeResult.Flags |= SdfSqueezeResult.FlagLowTier;
        }

        private static bool IsValidSdfPayload(
            NativeArray<byte> sdfTexture3D,
            int3 sdfDimensions,
            float3 sdfOrigin,
            float3 sdfCellSize,
            float sdfRange)
        {
            return sdfTexture3D.IsCreated &&
                   PlayerKinematicsBodyJob.TryResolveSdfVoxelCount(sdfDimensions, out int expectedLength) &&
                   sdfTexture3D.Length >= expectedLength &&
                   math.all(math.isfinite(sdfOrigin)) &&
                   math.all(math.isfinite(sdfCellSize)) &&
                   math.all(math.abs(sdfCellSize) > new float3(0.0001f)) &&
                   SanitizeNonNegative(sdfRange) > 0.0001f;
        }

        private bool TryReadPlayerKinematicStateFromVault(out AbsoluteUniversePosition aup)
        {
            aup = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            NativeArray<LockstepPlayerKinematicState> stateBuffer = dataVault.GetBuffer<LockstepPlayerKinematicState>(
                BufferID.PlayerKinematicState,
                EntityCount,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            if (!stateBuffer.IsCreated || stateBuffer.Length <= 0)
                return false;

            LockstepPlayerKinematicState state = stateBuffer[0];
            uint frame = unchecked((uint)Time.frameCount);
            if (state.Frame == 0u ||
                state.Frame > frame ||
                frame - state.Frame > 1u ||
                !math.all(math.isfinite(state.LocalPosition)))
            {
                return false;
            }

            aup = new AbsoluteUniversePosition
            {
                GridX = state.SectorX,
                GridY = state.SectorY,
                GridZ = state.SectorZ,
                LocalX = state.LocalPosition.x,
                LocalY = state.LocalPosition.y,
                LocalZ = state.LocalPosition.z
            };
            return true;
        }

        private void WritePlayerKinematicStateToVault(in AbsoluteUniversePosition aup, float3 velocity)
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return;

            NativeArray<LockstepPlayerKinematicState> stateBuffer = dataVault.GetBuffer<LockstepPlayerKinematicState>(
                BufferID.PlayerKinematicState,
                EntityCount,
                SystemID.GameplayPlayer,
                NativeArrayOptions.ClearMemory);
            if (!stateBuffer.IsCreated || stateBuffer.Length <= 0)
                return;

            stateBuffer[0] = new LockstepPlayerKinematicState
            {
                SectorX = aup.GridX,
                SectorY = aup.GridY,
                SectorZ = aup.GridZ,
                LocalPosition = new float3(aup.LocalX, aup.LocalY, aup.LocalZ),
                Velocity = SanitizeFloat3(velocity, float3.zero),
                Forward = _cachedTransform != null
                    ? SafeNormalize(ToFloat3(_cachedTransform.forward), new float3(0.0f, 0.0f, 1.0f))
                    : new float3(0.0f, 0.0f, 1.0f),
                Frame = unchecked((uint)Time.frameCount),
                Flags = BodyFlagSdfSqueezeIntervention,
                StableId = _sourceId
            };
        }

        private static byte ResolveSdfGradientProbeRequest()
        {
            uint frame = unchecked((uint)Time.frameCount);
            ReadOnlySpan<PlayerStateSignal> playerStates = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            int signalCount = math.min(playerStates.Length, IkSignalScanLimit);
            for (int i = 0; i < signalCount; i++)
            {
                PlayerStateSignal signal = playerStates[i];
                if (signal.State != PlayerStateSignal.StateSqueezing ||
                    (signal.Flags & PlayerStateSignal.FlagSqueezing) == 0)
                {
                    continue;
                }

                if (IsFreshSignalFrame(frame, signal.Frame, MaxSdfGradientProbeSignalAgeFrames))
                    return 1;
            }

            return 0;
        }

        private static bool IsInsidePublishedVoxelSdfBounds(HectonVoxelVolume volume, Vector3 runtimePosition)
        {
            if (volume == null ||
                !volume.TryGetPublishedSonarSdfPayload(
                    out NativeArray<byte> _,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float _,
                    out int _) ||
                gridDimensions.x <= 1 ||
                gridDimensions.y <= 1 ||
                gridDimensions.z <= 1)
            {
                return false;
            }

            float3 sample = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float3 origin = new float3(volumeOrigin.x, volumeOrigin.y, volumeOrigin.z);
            float3 cellSize = new float3(
                math.max(0.0001f, math.abs(voxelCellSize.x)),
                math.max(0.0001f, math.abs(voxelCellSize.y)),
                math.max(0.0001f, math.abs(voxelCellSize.z)));

            if (!math.all(math.isfinite(sample)) ||
                !math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(cellSize)))
            {
                return false;
            }

            float3 min = origin - cellSize * 0.5f;
            float3 max = origin + cellSize * new float3(
                gridDimensions.x - 0.5f,
                gridDimensions.y - 0.5f,
                gridDimensions.z - 0.5f);

            return sample.x >= min.x && sample.x <= max.x &&
                   sample.y >= min.y && sample.y <= max.y &&
                   sample.z >= min.z && sample.z <= max.z;
        }

        private void SnapshotLadder(out byte ladderActive, out float3 ladderPoint)
        {
            ladderActive = 0;
            ladderPoint = ReadPositionSnapshot(float3.zero);
            if (_motor == null ||
                !_motor.TryGetRecentBatchedLadderHit(MaxLadderFrameAge, out RaycastHit ladderHit))
            {
                return;
            }

            float3 safePoint = ToFloat3(ladderHit.point);
            if (!math.all(math.isfinite(safePoint)))
                return;

            ladderPoint = safePoint;
            ladderActive = 1;
        }

        private float3 ResolveCurrentAdvection(Vector3 position)
        {
            float immersion01 = ResolveRuntimeWaterImmersion01();
            if (immersion01 <= 0.001f)
                return float3.zero;

            if (_fluid == null || !_fluid.TrySampleModAbyssalFlow(position, out float3 flow))
                return float3.zero;

            if (!math.all(math.isfinite(flow)))
                return float3.zero;

            float gpuBoost = _accumulatorState.LastGpuFlowFrame != 0u ? 1.0f : 0.65f;
            float tierScale = IsLowTier(ResolveScalabilityTier()) ? 0.75f : 1.0f;
            float3 advection = flow * (AdvectionVelocityScale * gpuBoost * tierScale * immersion01);
            return SanitizeFloat3(advection, float3.zero);
        }

        private float ResolveRuntimeWaterDensityScale()
        {
            return SanitizeNonNegative(waterDensity) * ResolveRuntimeWaterImmersion01();
        }

        private float ResolveRuntimeWaterImmersion01()
        {
            return _movement != null ? SanitizeUnit(_movement.WaterImmersionRatio) : 1.0f;
        }

        private float ResolveEquipmentDragMultiplier()
        {
            if (_inventory == null)
                return 1.0f;

            ulong mask = _inventory.CurrentInventoryMask;
            float load01 = SanitizeUnit(_inventory.CachedInventoryLoad01);
            float heavy = (mask != 0UL && _inventory.TotalMassKg >= HeavyInventoryMassKg) ? HeavyInventoryMaskMultiplier : 0.0f;
            return 1.0f + heavy + load01 * DragCoefficientLoadScale;
        }

        private void TickInertiaRoll(float dt)
        {
            float targetRoll = 0.0f;
            bool rollPhaseAdvanced = false;
            if (_motor != null &&
                _motor.TryGetRecentWallSlideContact(
                    MaxWallContactFrameAge,
                    out Vector3 normal,
                    out _,
                    out float blockedSpeed,
                    out _,
                    out float velocityReduction01,
                    out _))
            {
                float speed01 = SanitizeUnit((blockedSpeed - WallImpactRollThreshold) * 0.2f);
                float sideDot = math.dot(ToFloat3(normal), SafeRight());
                float side = math.sign(math.select(sideDot, 0.0f, !math.isfinite(sideDot)));
                _rollPhaseRadians = DeterministicPhysicsMath.WrapSignedPi(_rollPhaseRadians + SanitizeNonNegative(dt) * 28.0f);
                rollPhaseAdvanced = true;
                float impactWave = IsHighScalabilityTier() ? DeterministicPhysicsMath.SinApprox(_rollPhaseRadians) : SignedTriangleWave(_rollPhaseRadians);
                targetRoll = -side *
                    SanitizeNonNegative(WallImpactRollDegrees) *
                    speed01 *
                    SanitizeUnit(velocityReduction01 + 0.25f) *
                    impactWave;
            }

            float safeDt = SanitizeNonNegative(dt);
            float squeezeStress = SanitizeUnit(_lastSdfSqueezeStress01);
            if (squeezeStress > 0.0001f && IsHighScalabilityTier() && IsFiniteNonZero(_lastSdfSqueezeNormal))
            {
                if (!rollPhaseAdvanced)
                    _rollPhaseRadians = DeterministicPhysicsMath.WrapSignedPi(_rollPhaseRadians + safeDt * 16.0f);

                float sideDot = math.dot(_lastSdfSqueezeNormal, SafeRight());
                float side = math.sign(math.select(sideDot, 0.0f, !math.isfinite(sideDot)));
                float twistWave = 0.65f + 0.35f * DeterministicPhysicsMath.SinApprox(_rollPhaseRadians);
                float squeezeRoll = -side * SdfSqueezeRollDegrees * squeezeStress * twistWave;
                if (math.abs(squeezeRoll) > math.abs(targetRoll))
                    targetRoll = squeezeRoll;
            }

            _lastSdfSqueezeStress01 = math.max(0.0f, squeezeStress - safeDt * 5.0f);
            targetRoll = math.select(targetRoll, 0.0f, !math.isfinite(targetRoll));
            _rollDegrees = math.select(_rollDegrees, 0.0f, !math.isfinite(_rollDegrees));
            _rollVelocityDegrees = math.select(_rollVelocityDegrees, 0.0f, !math.isfinite(_rollVelocityDegrees));
            float spring = ((targetRoll - _rollDegrees) * 64.0f) - (_rollVelocityDegrees * WallImpactRollDecay);
            _rollVelocityDegrees += spring * safeDt;
            _rollDegrees += _rollVelocityDegrees * safeDt;
            float maxRoll = SanitizeNonNegative(WallImpactRollDegrees);
            _rollDegrees = math.clamp(_rollDegrees, -maxRoll, maxRoll);
        }

        private void PublishMovementAcoustics(Vector3 position, float3 velocity)
        {
            float velocitySq = math.lengthsq(velocity);
            if (velocitySq <= 0.0025f || !math.isfinite(velocitySq))
                return;

            float3 safePosition = SanitizeFloat3(
                ToFloat3(position),
                ReadPositionSnapshot(float3.zero));

            MovementAcousticSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(SnapMillimeter(safePosition)));
            signal.Volume = SanitizeUnit(velocitySq * 0.08f);
            signal.VelocitySq = velocitySq;
            signal.SourceId = _sourceId;
            signal.LocomotionMode = ResolveLocomotionModeCode();
            signal.SurfaceMode = (byte)(_movement != null && _movement.IsPlayerSubmerged ? 1 : 0);
            signal.Flags = 0;
            GlobalSignals.Publish(in signal);
        }

        private void PublishKccVelocitySignal(float3 position, float3 velocity, byte flags)
        {
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                return;

            float3 snappedPosition = SnapMillimeter(position);
            float3 snappedVelocity = SnapMillimeter(velocity);
            AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(snappedPosition));
            PhysicsDeterminismSignals.PublishKccVelocity(
                in bodyAup,
                snappedVelocity,
                unchecked((uint)Time.frameCount),
                _sourceId,
                flags);
        }

        private void PublishSdfSqueezeSignals(in SdfSqueezeResult result, float3 position, float3 velocity, byte lowTier)
        {
            float stress01 = SanitizeUnit(result.Stress01);
            float pushSpeed = SanitizeNonNegative(result.PushSpeed);
            if (stress01 <= 0.0001f && pushSpeed <= 0.0001f)
                return;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(SnapMillimeter(position)));
            byte stateFlags = (byte)(PlayerStateSignal.FlagActive |
                                     PlayerStateSignal.FlagSqueezing |
                                     PlayerStateSignal.FlagSdfGradientValid |
                                     PlayerStateSignal.FlagAupShiftSafe);
            if (lowTier != 0 || (result.Flags & SdfSqueezeResult.FlagLowTier) != 0u)
                stateFlags |= PlayerStateSignal.FlagLowTierGradient;

            PlayerStateSignal playerState = default;
            playerState.PositionAup = positionAup;
            playerState.Intensity01 = stress01;
            playerState.SourceHash = _sourceId;
            playerState.Frame = result.Frame != 0u ? result.Frame : unchecked((uint)Time.frameCount);
            playerState.State = PlayerStateSignal.StateSqueezing;
            playerState.Flags = stateFlags;
            GlobalSignals.Publish(in playerState);
            _lastConsumedSqueezeSignalFrame = playerState.Frame;
            _lastConsumedSqueezeSignalSourceHash = playerState.SourceHash;

            unchecked
            {
                _accumulatorState.SqueezeInterventions++;
            }

            PublishSdfSqueezePhysiology(stress01, playerState.Frame);
            PublishSdfSqueezeGasLoad(stress01);
            TryPublishSdfSqueezeFeedback(in positionAup, stress01, pushSpeed, playerState.Frame);
            TryPublishSdfSqueezeVisualImpulse(in positionAup, result.Normal, velocity, stress01, playerState.Frame);
            WriteSdfSqueezeTelemetry(in result, position, velocity);
        }

        private void PublishSdfSqueezePhysiology(float stress01, uint frame)
        {
            float oxygenDrainScale = 1.0f + SanitizeUnit(stress01) * SdfSqueezeOxygenDrainScaleBonus;
            PhysiologyStateSignal physiology = default;
            physiology.PlayerStress01 = stress01;
            physiology.O2DrainMultiplier = oxygenDrainScale;
            physiology.Recovery01 = 0.0f;
            physiology.Frame = frame;
            physiology.Cause = PlayerStateSignal.StateSqueezing;
            physiology.Flags = PlayerStateSignal.FlagSqueezing;
            GlobalSignals.Publish(in physiology);
        }

        private void PublishSdfSqueezeGasLoad(float stress01)
        {
            IGasDynamicsSolver gasDynamics = _gasDynamics;
            if (gasDynamics == null || !gasDynamics.IsInitialized)
                return;

            gasDynamics.TryApplyPlayerRoomCarbonDioxideEquivalentPressure(
                SanitizeUnit(stress01) * SdfSqueezeCo2EquivalentPressureKPa);
        }

        private void TryPublishSdfSqueezeFeedback(in AbsoluteUniversePosition positionAup, float stress01, float pushSpeed, uint frame)
        {
            _sdfSqueezeFeedbackCooldown = math.max(0.0f, _sdfSqueezeFeedbackCooldown);
            if (_sdfSqueezeFeedbackCooldown > 0.0f ||
                pushSpeed < SdfSqueezeFeedbackSpeedThreshold)
            {
                return;
            }

            float intensity = SanitizeUnit(math.max(stress01, pushSpeed * 0.5f));
            HapticRequest haptic = default;
            haptic.Intensity01 = SanitizeUnit(0.12f + intensity * 0.55f);
            haptic.DurationSeconds = 0.035f + intensity * 0.05f;
            haptic.Frequency01 = SanitizeUnit(0.55f + intensity * 0.25f);
            haptic.SourceHash = _sourceId;
            haptic.Frame = frame;
            haptic.Channel = HapticRequest.ChannelGearScrape;
            haptic.Flags = 0;
            GlobalSignals.Publish(in haptic);

            AcousticPingSignal acoustic = default;
            acoustic.PositionAup = positionAup;
            acoustic.RadiusMeters = 0.75f + intensity * 1.35f;
            acoustic.Intensity01 = intensity;
            acoustic.SourceId = _sourceId;
            acoustic.Channel = AcousticPingSignal.ChannelFabricScrape;
            acoustic.Flags = AcousticPingSignal.FlagFabricScrape;
            GlobalSignals.Publish(in acoustic);

            _sdfSqueezeFeedbackCooldown = SdfSqueezeFeedbackCooldownSeconds;
        }

        private void TryPublishSdfSqueezeVisualImpulse(
            in AbsoluteUniversePosition positionAup,
            float3 normal,
            float3 velocity,
            float stress01,
            uint frame)
        {
            float intensity = SanitizeUnit(stress01);
            if (!IsHighScalabilityTier() || intensity < SdfSqueezeVisualImpulseMinStress01)
                return;

            float3 safeNormal = SafeNormalize(normal, float3.zero);
            float3 safeVelocity = SanitizeFloat3(velocity, float3.zero);
            float3 vector = safeNormal * (0.65f + intensity * 1.15f) +
                            safeVelocity * SdfSqueezeVisualImpulseVelocityScale;
            float vectorSq = math.lengthsq(vector);
            if (!math.isfinite(vectorSq) || vectorSq <= 0.000001f)
                return;

            FluidImpulseSignal impulse = default;
            impulse.PositionAup = positionAup;
            impulse.Vector = vector;
            impulse.Radius = SdfSqueezeVisualImpulseBaseRadiusMeters +
                             intensity * SdfSqueezeVisualImpulseExtraRadiusMeters;
            impulse.Lifetime = SdfSqueezeVisualImpulseBaseLifetimeSeconds +
                               intensity * SdfSqueezeVisualImpulseExtraLifetimeSeconds;
            impulse.Frame = frame;
            impulse.SourceHash = _sourceId;
            impulse.Flags = (uint)(PlayerStateSignal.FlagSqueezing | PlayerStateSignal.FlagSdfGradientValid);
            GlobalSignals.Publish(in impulse);
        }

        private void WriteSdfSqueezeTelemetry(in SdfSqueezeResult result, float3 position, float3 velocity)
        {
            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            uint clampedInterventions = _accumulatorState.SqueezeInterventions > 65535u
                ? 65535u
                : _accumulatorState.SqueezeInterventions;
            uint auxFlags = BodyFlagSdfSqueezeIntervention |
                            ((clampedInterventions << TelemetrySqueezeInterventionShift) & TelemetrySqueezeInterventionMask);
            if ((result.Flags & SdfSqueezeResult.FlagGradientValid) != 0u)
                auxFlags |= BodyFlagSdfGradientValid;
            if ((result.Flags & SdfSqueezeResult.FlagLowTier) != 0u)
                auxFlags |= BodyFlagSdfLowTierGradient;
            if ((result.Flags & SdfSqueezeResult.FlagSlowCadence) != 0u)
                auxFlags |= SdfSqueezeSlowTelemetryFlag;
            if ((result.Flags & SdfSqueezeResult.FlagNaNFallback) != 0u)
                auxFlags |= SdfSqueezeNanTelemetryFlag;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = SanitizeFloat3(position, result.Position),
                Velocity = SanitizeFloat3(velocity, result.Velocity),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = math.select(result.CenterDensity, 0.0f, !math.isfinite(result.CenterDensity)),
                Frame = result.Frame != 0u ? result.Frame : unchecked((uint)Time.frameCount),
                Flags = 0u,
                SyncFenceHash = _accumulatorState.LastSyncFenceHash,
                AuxFlags = auxFlags,
                AupMaxDriftErrorMeters = SanitizeNonNegative(result.PushSpeed)
            };
        }

        private void ConsumeAupPreShiftSignals()
        {
            ReadOnlySpan<AupPreShiftSignal> signals = SignalBus<AupPreShiftSignal>.GetFrameSnapshot();
            int count = math.min(signals.Length, StateCorrectionDrainLimit);
            for (int i = 0; i < count; i++)
            {
                AupPreShiftSignal signal = signals[i];
                if (signal.ShiftFrameId == 0u ||
                    signal.ShiftFrameId == _accumulatorState.LastConsumedPreShiftFrameId ||
                    !math.all(math.isfinite(signal.ShiftMeters)))
                {
                    continue;
                }

                _accumulatorState.LastConsumedPreShiftFrameId = signal.ShiftFrameId;
                _accumulatorState.PreShiftHaltFrames = math.max(
                    _accumulatorState.PreShiftHaltFrames,
                    PreShiftHaltFrameCount);
            }
        }

        private void PublishAupPreShiftHaltState()
        {
            _stateWriteReady = false;
            if (_body == null)
                return;

            float3 position = SnapMillimeter(SanitizeFloat3(ToFloat3(_body.position), ReadLastValidPosition()));
            float3 velocity = SnapMillimeter(SanitizeFloat3(ToFloat3(_body.linearVelocity), float3.zero));
            if (HasMotionSoaStorage())
            {
                _positions[0] = position;
                _velocities[0] = velocity;
            }

            PublishKccVelocitySignal(position, velocity, KccVelocitySignal.FlagMovementAuthorityExternal);
            WriteAupPreShiftHaltTelemetry(position, velocity);
        }

        private void WriteAupPreShiftHaltTelemetry(float3 position, float3 velocity)
        {
            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = SanitizeFloat3(position, float3.zero),
                Velocity = SanitizeFloat3(velocity, float3.zero),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = 0.0f,
                Frame = unchecked((uint)Time.frameCount),
                Flags = 0u,
                SyncFenceHash = _accumulatorState.LastSyncFenceHash,
                AuxFlags = AupPreShiftHaltTelemetryFlag | (_accumulatorState.LastConsumedPreShiftFrameId & 0xFFFFu),
                AupMaxDriftErrorMeters = 0.0f
            };
        }

        private void ConsumeEnvironmentIkSignals()
        {
            uint frame = unchecked((uint)Time.frameCount);
            ReadOnlySpan<HighSpeedImpactSignal> impactSignals = SignalBus<HighSpeedImpactSignal>.GetFrameSnapshot();
            int impactCount = math.min(impactSignals.Length, IkSignalScanLimit);
            for (int i = 0; i < impactCount; i++)
            {
                HighSpeedImpactSignal signal = impactSignals[i];
                if (signal.SourceKind != HighSpeedImpactSignal.SourcePlayer)
                    continue;

                if (!IsFreshSignalFrame(frame, signal.Frame, MaxEnvironmentIkSignalAgeFrames) ||
                    signal.ImpactSpeed < HandBraceSpeedThreshold)
                    continue;

                float3 point = signal.PointAup.ToRuntimeFloat3();
                float3 normal = SafeNormalize(signal.Normal, new float3(0.0f, 1.0f, 0.0f));
                if (!math.all(math.isfinite(point)) || !math.all(math.isfinite(normal)))
                    continue;

                _impactBracePoint = point;
                _impactBraceNormal = normal;
                _hasImpactBracePoint = true;
                _braceHoldTimer = math.max(_braceHoldTimer, HandBraceHoldSeconds);
            }

            ReadOnlySpan<PlayerStateSignal> playerStates = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            int playerStateCount = math.min(playerStates.Length, IkSignalScanLimit);
            for (int i = 0; i < playerStateCount; i++)
            {
                PlayerStateSignal signal = playerStates[i];
                if (signal.State != PlayerStateSignal.StateSqueezing ||
                    (signal.Flags & PlayerStateSignal.FlagActive) == 0)
                {
                    continue;
                }

                if (!IsFreshSignalFrame(frame, signal.Frame, MaxEnvironmentIkSignalAgeFrames))
                    continue;

                _squeezeTargetBlend = math.max(SanitizeUnit(_squeezeTargetBlend), SanitizeUnit(signal.Intensity01));
                _squeezeHoldTimer = math.max(_squeezeHoldTimer, SqueezeHoldSeconds);
            }

            ReadOnlySpan<PlayerStressSignal> stressSignals = SignalBus<PlayerStressSignal>.GetFrameSnapshot();
            int stressCount = math.min(stressSignals.Length, IkSignalScanLimit);
            bool hasStressSignal = false;
            PlayerStressSignal latestStressSignal = default;
            for (int i = 0; i < stressCount; i++)
            {
                PlayerStressSignal stressSignal = stressSignals[i];
                if (stressSignal.Frame == _lastConsumedPlayerStressFrame ||
                    stressSignal.Frame > frame)
                {
                    continue;
                }

                if (!hasStressSignal || stressSignal.Frame >= latestStressSignal.Frame)
                {
                    latestStressSignal = stressSignal;
                    hasStressSignal = true;
                }
            }

            if (hasStressSignal)
            {
                _lastConsumedPlayerStressFrame = latestStressSignal.Frame;
                _cachedStress01 = SanitizeUnit(latestStressSignal.Stress01);
            }
        }

        private void TickEnvironmentIkState(float deltaTime)
        {
            float safeDeltaTime = SanitizeNonNegative(deltaTime);
            _braceHoldTimer = math.max(0.0f, _braceHoldTimer - safeDeltaTime);
            _squeezeHoldTimer = math.max(0.0f, _squeezeHoldTimer - safeDeltaTime);
            _braceHapticCooldown = math.max(0.0f, _braceHapticCooldown - safeDeltaTime);
            _scrapeAcousticCooldown = math.max(0.0f, _scrapeAcousticCooldown - safeDeltaTime);
            _sdfSqueezeFeedbackCooldown = math.max(0.0f, _sdfSqueezeFeedbackCooldown - safeDeltaTime);

            float braceTarget = _braceHoldTimer > 0.0f ? 1.0f : 0.0f;
            float squeezeTarget = _squeezeHoldTimer > 0.0f ? math.max(0.35f, _squeezeTargetBlend) : 0.0f;
            _braceBlend = SmoothScalar(_braceBlend, braceTarget, HandBraceBlendSharpness, safeDeltaTime);
            _squeezeBlend = SmoothScalar(_squeezeBlend, squeezeTarget, SqueezeBlendSharpness, safeDeltaTime);
            if (_squeezeHoldTimer <= 0.0f && _squeezeBlend <= 0.0001f)
                _squeezeTargetBlend = 0.0f;
            if (_braceHoldTimer <= 0.0f && _braceBlend <= 0.0001f)
                _hasImpactBracePoint = false;

            _bracePhase = WrapPositivePhase(
                _bracePhase + ((4.0f + _cachedStress01 * 11.0f) * safeDeltaTime),
                BracePhaseWrap);
        }

        private void ConsumeSystemStressSignals()
        {
            uint frame = unchecked((uint)Time.frameCount);
            ReadOnlySpan<SystemHealthIndexSignal> signals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, IkSignalScanLimit);
            bool hasStressSignal = false;
            SystemHealthIndexSignal latestStressSignal = default;
            for (int i = 0; i < signalCount; i++)
            {
                SystemHealthIndexSignal signal = signals[i];
                if (signal.Frame == _lastConsumedSystemStressFrame ||
                    signal.Frame > frame)
                {
                    continue;
                }

                if (!hasStressSignal || signal.Frame >= latestStressSignal.Frame)
                {
                    latestStressSignal = signal;
                    hasStressSignal = true;
                }
            }

            if (hasStressSignal)
            {
                _lastConsumedSystemStressFrame = latestStressSignal.Frame;
                _cachedSystemStress01 = SanitizeUnit(latestStressSignal.Pressure01);
            }
        }

        private void ConsumeSqueezeTelemetrySignal()
        {
            uint frame = unchecked((uint)Time.frameCount);
            ReadOnlySpan<PlayerStateSignal> playerStates = SignalBus<PlayerStateSignal>.GetFrameSnapshot();
            int signalCount = math.min(playerStates.Length, IkSignalScanLimit);
            bool hasSqueezeSignal = false;
            PlayerStateSignal signal = default;
            for (int i = 0; i < signalCount; i++)
            {
                PlayerStateSignal candidate = playerStates[i];
                if (candidate.State != PlayerStateSignal.StateSqueezing ||
                    (candidate.Flags & PlayerStateSignal.FlagSqueezing) == 0)
                {
                    continue;
                }

                if (candidate.Frame == _lastConsumedSqueezeSignalFrame &&
                    candidate.SourceHash == _lastConsumedSqueezeSignalSourceHash)
                {
                    continue;
                }

                if (!IsFreshSignalFrame(frame, candidate.Frame, MaxSdfGradientProbeSignalAgeFrames))
                    continue;

                if (!hasSqueezeSignal || candidate.Frame >= signal.Frame)
                {
                    signal = candidate;
                    hasSqueezeSignal = true;
                }
            }

            if (!hasSqueezeSignal)
                return;

            _lastConsumedSqueezeSignalFrame = signal.Frame;
            _lastConsumedSqueezeSignalSourceHash = signal.SourceHash;

            unchecked
            {
                _accumulatorState.SqueezeInterventions++;
            }

            float stress01 = SanitizeUnit(signal.Intensity01);
            PublishSdfSqueezePhysiology(stress01, signal.Frame);
            PublishSdfSqueezeGasLoad(stress01);
            TryPublishSdfSqueezeFeedback(in signal.PositionAup, stress01, stress01, signal.Frame);
            float3 externalVelocity = _body != null ? ToFloat3(_body.linearVelocity) : ReadVelocitySnapshot(float3.zero);
            TryPublishSdfSqueezeVisualImpulse(in signal.PositionAup, float3.zero, externalVelocity, stress01, signal.Frame);
            WriteSqueezeTelemetry(in signal);
        }

        private void WriteSqueezeTelemetry(in PlayerStateSignal signal)
        {
            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            Vector3 runtimePosition = _body != null ? _body.position : Vector3.zero;
            Vector3 runtimeVelocity = _body != null ? _body.linearVelocity : Vector3.zero;
            uint clampedInterventions = _accumulatorState.SqueezeInterventions > 65535u
                ? 65535u
                : _accumulatorState.SqueezeInterventions;
            uint auxFlags = BodyFlagSdfSqueezeIntervention |
                BodyFlagSdfGradientValid |
                ((clampedInterventions << TelemetrySqueezeInterventionShift) & TelemetrySqueezeInterventionMask);
            if ((signal.Flags & PlayerStateSignal.FlagLowTierGradient) != 0)
                auxFlags |= BodyFlagSdfLowTierGradient;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = SanitizeFloat3(ToFloat3(runtimePosition), float3.zero),
                Velocity = SanitizeFloat3(ToFloat3(runtimeVelocity), float3.zero),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = SanitizeUnit(signal.Intensity01),
                Frame = signal.Frame != 0u ? signal.Frame : unchecked((uint)Time.frameCount),
                Flags = 0u,
                SyncFenceHash = 0u,
                AuxFlags = auxFlags
            };
        }

        private void TickStamina()
        {
            if (!HasIntendedMovementStorage())
                return;

            float intendedSq = math.lengthsq(_intendedMovement[0]);
            if (_survival == null || intendedSq <= 0.0001f)
                return;

            _survival.SetMovementStaminaBurnInput(intendedSq, StaminaDrainPerSecond);
        }

        private byte ResolveLocomotionModeCode()
        {
            return _movement != null ? (byte)_movement.CurrentLocomotionMode : (byte)0;
        }

        private void ScheduleHandProbes()
        {
            if (_handProbePending || _handPlacementPending)
                return;

            if (_cachedTransform == null)
            {
                ClearHandTargets();
                return;
            }

            bool lowTier = IsLowTier(ResolveScalabilityTier());
            int scheduledProbeCount = lowTier ? 1 : EnvironmentProbeCount;
            if (!HasHandProbeStorage(scheduledProbeCount))
            {
                ClearHandTargets();
                return;
            }

            if (lowTier && ((Time.frameCount + _cadenceSalt) & LowTierHandProbeFrameMask) != 0)
                return;

            Transform source = _cameraTransform != null ? _cameraTransform : _cachedTransform;
            float3 sourcePosition = ToFloat3(source.position);
            float3 sourceForward = ToFloat3(source.forward);
            float3 sourceRight = ToFloat3(source.right);
            float3 sourceUp = ToFloat3(source.up);
            float3 velocity = ReadVelocitySnapshot(_body != null ? ToFloat3(_body.linearVelocity) : float3.zero);
            if (!math.all(math.isfinite(sourcePosition)) ||
                !IsFiniteNonZero(sourceForward) ||
                !IsFiniteNonZero(sourceRight) ||
                !IsFiniteNonZero(sourceUp) ||
                !math.all(math.isfinite(velocity)))
            {
                ClearHandTargets();
                return;
            }

            sourceForward = SafeNormalize(sourceForward, new float3(0.0f, 0.0f, 1.0f));
            sourceRight = SafeNormalize(sourceRight, new float3(1.0f, 0.0f, 0.0f));
            sourceUp = SafeNormalize(sourceUp, new float3(0.0f, 1.0f, 0.0f));
            float velocitySq = math.lengthsq(velocity);
            float3 probeDirection = velocitySq > HandBraceSpeedThreshold * HandBraceSpeedThreshold
                ? SafeNormalize(math.lerp(sourceForward, velocity * math.rsqrt(math.max(0.000001f, velocitySq)), 0.35f), sourceForward)
                : sourceForward;
            float3 chestOrigin = sourcePosition + sourceForward * 0.18f - sourceUp * HandProbeDownOffset;
            if (!math.all(math.isfinite(chestOrigin)) || !IsFiniteNonZero(probeDirection))
            {
                ClearHandTargets();
                return;
            }

            _lastProbeSourcePosition = sourcePosition;
            _lastProbeSourceForward = sourceForward;
            _lastProbeSourceRight = sourceRight;
            _lastProbeSourceUp = sourceUp;
            _lastProbeVelocity = velocity;
            _lastProbeLowTier = lowTier;

            Vector3 origin = ToVector3(chestOrigin);
            Vector3 right = ToVector3(sourceRight);
            Vector3 up = ToVector3(sourceUp);
            Vector3 direction = ToVector3(probeDirection);
            QueryParameters parameters = new QueryParameters
            {
                layerMask = handProbeLayerMask.value,
                hitTriggers = QueryTriggerInteraction.Ignore,
                hitBackfaces = false,
                hitMultipleFaces = false
            };

            _handProbeCommands[0] = new RaycastCommand
            {
                from = lowTier ? origin : origin - right * HandProbeSideOffset,
                direction = direction,
                distance = HandProbeDistance,
                queryParameters = parameters
            };

            if (!lowTier)
            {
                _handProbeCommands[1] = new RaycastCommand
                {
                    from = origin + right * HandProbeSideOffset,
                    direction = direction,
                    distance = HandProbeDistance,
                    queryParameters = parameters
                };
                _handProbeCommands[2] = new RaycastCommand
                {
                    from = origin + up * 0.22f,
                    direction = direction,
                    distance = HandBraceDistance,
                    queryParameters = parameters
                };
                _handProbeCommands[3] = new RaycastCommand
                {
                    from = origin - up * 0.52f,
                    direction = direction,
                    distance = HandBraceDistance,
                    queryParameters = parameters
                };
                scheduledProbeCount = EnvironmentProbeCount;
            }

            NativeArray<RaycastCommand> commandBatch = _handProbeCommands.GetSubArray(0, scheduledProbeCount);
            NativeArray<RaycastHit> hitBatch = _handProbeHits.GetSubArray(0, scheduledProbeCount);
            _handProbeHandle = RaycastCommand.ScheduleBatch(commandBatch, hitBatch, scheduledProbeCount, default);
            _handProbePending = true;
        }

        private void PumpHandEnvironmentJobs(bool forceComplete, bool allowFinalizeOutsideSwap)
        {
            if (CompleteHandProbe(forceComplete, allowFinalizeOutsideSwap))
                ScheduleHandPlacement();

            if (CompleteHandPlacement(forceComplete, allowFinalizeOutsideSwap))
                ApplyHandTargets();
        }

        private bool CompleteHandProbe(bool forceComplete, bool allowFinalizeOutsideSwap)
        {
            if (!_handProbePending)
                return false;

            bool completed = forceComplete
                ? DispatcherJobSwap.TryComplete(ref _handProbeHandle, true)
                : allowFinalizeOutsideSwap
                    ? DispatcherJobSwap.TryFinalizeCompleted(ref _handProbeHandle)
                    : DispatcherJobSwap.TryComplete(ref _handProbeHandle, false);
            if (!completed)
                return false;

            _handProbePending = false;
            return true;
        }

        private void ScheduleHandPlacement()
        {
            int requiredProbeCount = _lastProbeLowTier ? 1 : EnvironmentProbeCount;
            if (_handPlacementPending ||
                !HasHandTargetWriteStorage() ||
                !HasHandProbeHitStorage(requiredProbeCount))
            {
                return;
            }

            var placementJob = new PlayerKinematicsHandPlacementJob
            {
                Hits = _handProbeHits,
                Targets = _handTargets,
                SourcePosition = _lastProbeSourcePosition,
                SourceForward = _lastProbeSourceForward,
                SourceRight = _lastProbeSourceRight,
                SourceUp = _lastProbeSourceUp,
                Velocity = _lastProbeVelocity,
                ImpactPoint = _impactBracePoint,
                ImpactNormal = _impactBraceNormal,
                ContactOffset = HandContactOffset,
                MaxProbeDistance = HandProbeDistance,
                BraceDistance = HandBraceDistance,
                BraceSpeedThreshold = HandBraceSpeedThreshold,
                BraceBlend = _braceBlend,
                SqueezeBlend = _squeezeBlend,
                Stress01 = _cachedStress01,
                Phase = _bracePhase,
                RuntimeFlags = (byte)(
                    (_lastProbeLowTier ? PlayerKinematicsHandPlacementJob.RuntimeFlagLowTier : 0) |
                    (_hasImpactBracePoint ? PlayerKinematicsHandPlacementJob.RuntimeFlagImpact : 0))
            };
            _handPlacementHandle = placementJob.Schedule();
            _handPlacementPending = true;
        }

        private bool CompleteHandPlacement(bool forceComplete, bool allowFinalizeOutsideSwap)
        {
            if (!_handPlacementPending)
                return false;

            bool completed = forceComplete
                ? DispatcherJobSwap.TryComplete(ref _handPlacementHandle, true)
                : allowFinalizeOutsideSwap
                    ? DispatcherJobSwap.TryFinalizeCompleted(ref _handPlacementHandle)
                    : DispatcherJobSwap.TryComplete(ref _handPlacementHandle, false);
            if (!completed)
                return false;

            _handPlacementPending = false;
            return true;
        }

        private void ApplyHandTargets()
        {
            if (!HasHandTargetStorage())
            {
                ClearHandTargets();
                return;
            }

            SmoothHandTarget(0, _handTargets[0]);
            SmoothHandTarget(1, _handTargets[1]);
            PlayerKinematicsHandTarget leftTarget = _smoothedHandTargets[0];
            PlayerKinematicsHandTarget rightTarget = _smoothedHandTargets[1];
            float activeBlend = math.max(
                leftTarget.Hit != 0 ? leftTarget.Blend : 0.0f,
                rightTarget.Hit != 0 ? rightTarget.Blend : 0.0f);
            bool active = activeBlend > 0.025f;
            bool scraped = false;

            if (_ikRig != null)
                _ikRig.ApplyExternalWallHandTargets(in leftTarget, in rightTarget);

            if (active)
            {
                if (!_wasBraceActive)
                    EmitBraceHaptic(activeBlend);
                scraped = TryEmitGloveScrape(activeBlend);
            }

            WriteEnvironmentIkTelemetry(in leftTarget, in rightTarget, activeBlend, scraped);
            _wasBraceActive = active;
        }

        private void ClearHandTargets()
        {
            if (_handTargets.IsCreated && _handTargets.Length >= HandTargetCount)
            {
                _handTargets[0] = default;
                _handTargets[1] = default;
            }

            if (_smoothedHandTargets.IsCreated && _smoothedHandTargets.Length >= HandTargetCount)
            {
                _smoothedHandTargets[0] = default;
                _smoothedHandTargets[1] = default;
            }

            _wasBraceActive = false;
            if (_ikRig == null)
                return;

            PlayerKinematicsHandTarget empty = default;
            _ikRig.ApplyExternalWallHandTargets(in empty, in empty);
        }

        private void SmoothHandTarget(int index, in PlayerKinematicsHandTarget rawTarget)
        {
            if (!_smoothedHandTargets.IsCreated || (uint)index >= (uint)_smoothedHandTargets.Length)
                return;

            PlayerKinematicsHandTarget current = _smoothedHandTargets[index];
            bool currentValid = current.Hit != 0 &&
                                math.all(math.isfinite(current.Position)) &&
                                IsFiniteNonZero(current.Normal);
            bool rawTargetValid = rawTarget.Hit != 0 &&
                                  math.all(math.isfinite(rawTarget.Position)) &&
                                  IsFiniteNonZero(rawTarget.Normal);
            float deltaTime = math.max(0.0001f, SanitizeNonNegative(_lastIkDeltaTime));
            float blend = SmoothScalar(
                SanitizeUnit(current.Blend),
                rawTargetValid ? SanitizeUnit(rawTarget.Blend) : 0.0f,
                HandBraceBlendSharpness,
                deltaTime);
            if (!rawTargetValid)
            {
                if (!currentValid)
                    current = default;

                current.Blend = blend;
                current.Flags = rawTarget.Flags;
                if (blend <= 0.0001f)
                    current = default;
                _smoothedHandTargets[index] = current;
                return;
            }

            float t = SanitizeUnit(HandBraceBlendSharpness * deltaTime);
            float3 rawNormal = SafeNormalize(rawTarget.Normal, new float3(0.0f, 1.0f, 0.0f));
            if (current.Hit == 0 || !math.all(math.isfinite(current.Position)))
            {
                current.Position = rawTarget.Position;
                current.Normal = rawNormal;
            }
            else
            {
                current.Position = math.lerp(current.Position, rawTarget.Position, t);
                current.Normal = SafeNormalize(math.lerp(current.Normal, rawNormal, t), rawNormal);
            }

            current.Blend = blend;
            current.Hit = blend > 0.0001f ? (byte)1 : (byte)0;
            current.Flags = rawTarget.Flags;
            _smoothedHandTargets[index] = current;
        }

        private void EmitBraceHaptic(float blend)
        {
            if (_braceHapticCooldown > 0.0f)
                return;

            float safeBlend = SanitizeUnit(blend);
            HapticRequest signal = default;
            signal.Intensity01 = SanitizeUnit(0.18f + safeBlend * 0.42f);
            signal.DurationSeconds = 0.045f + safeBlend * 0.035f;
            signal.Frequency01 = SanitizeUnit(0.35f + safeBlend * 0.35f);
            signal.SourceHash = _sourceId;
            signal.Frame = unchecked((uint)Time.frameCount);
            signal.Channel = HapticRequest.ChannelLightThud;
            signal.Flags = HapticRequest.FlagLightThud;
            GlobalSignals.Publish(in signal);
            _braceHapticCooldown = BraceHapticCooldownSeconds;
        }

        private bool TryEmitGloveScrape(float blend)
        {
            if (_scrapeAcousticCooldown > 0.0f || _motor == null)
                return false;

            if (!_motor.TryGetRecentWallSlideContact(
                MaxWallContactFrameAge,
                out _,
                out Vector3 point,
                out float blockedSpeed,
                out _,
                out float velocityReduction01,
                out _))
            {
                return false;
            }

            float3 pointFloat = ToFloat3(point);
            if (!math.all(math.isfinite(pointFloat)))
                return false;

            float safeBlockedSpeed = SanitizeNonNegative(blockedSpeed);
            float safeVelocityReduction01 = SanitizeUnit(velocityReduction01);
            if (safeBlockedSpeed <= 0.2f || safeVelocityReduction01 <= 0.05f)
                return false;

            AcousticPingSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromRuntimePosition(point);
            signal.RadiusMeters = SanitizeUnit(0.65f + safeBlockedSpeed * 0.12f) * 2.0f;
            signal.Intensity01 = SanitizeUnit(SanitizeUnit(blend) * (0.35f + safeVelocityReduction01) + safeBlockedSpeed * 0.025f);
            signal.SourceId = _sourceId;
            signal.Channel = AcousticPingSignal.ChannelGloveScrape;
            signal.Flags = AcousticPingSignal.FlagGloveScrape;
            GlobalSignals.Publish(in signal);
            _scrapeAcousticCooldown = GloveScrapeCooldownSeconds;
            return true;
        }

        private void WriteEnvironmentIkTelemetry(
            in PlayerKinematicsHandTarget leftTarget,
            in PlayerKinematicsHandTarget rightTarget,
            float activeBlend,
            bool scraped)
        {
            float safeActiveBlend = SanitizeUnit(activeBlend);
            uint auxFlags = 0u;
            if (safeActiveBlend > 0.0001f)
                auxFlags |= IkBraceTelemetryFlag;
            if ((leftTarget.Flags & PlayerKinematicsHandTarget.FlagSqueeze) != 0 ||
                (rightTarget.Flags & PlayerKinematicsHandTarget.FlagSqueeze) != 0)
            {
                auxFlags |= IkSqueezeTelemetryFlag;
            }
            if (_hasImpactBracePoint)
                auxFlags |= IkImpactTelemetryFlag;
            if (_lastProbeLowTier)
                auxFlags |= IkLowTierTelemetryFlag;
            if (scraped)
                auxFlags |= IkScrapeTelemetryFlag;
            if (auxFlags == 0u)
                return;

            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = ReadPositionSnapshot(_lastProbeSourcePosition),
                Velocity = ReadVelocitySnapshot(_lastProbeVelocity),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = safeActiveBlend,
                Frame = unchecked((uint)Time.frameCount),
                Flags = 0u,
                SyncFenceHash = 0u,
                AuxFlags = auxFlags
            };
        }

        private void PushVatScalar()
        {
            float speedSq = HasVelocityStorage() ? math.lengthsq(_velocities[0]) : 0.0f;
            float scalar = SanitizeUnit(speedSq * 0.05f);
            float lastScalar = math.select(_lastVatSpeedScalar, -1.0f, !math.isfinite(_lastVatSpeedScalar));
            if (math.abs(scalar - lastScalar) <= 0.0025f)
                return;

            Shader.SetGlobalFloat(_PlayerSwimVatSpeedId, scalar);
            _lastVatSpeedScalar = scalar;
        }

        private void PushRollSignal()
        {
            bool rollInvalid = !math.isfinite(_rollDegrees);
            float rollDegrees = math.select(_rollDegrees, 0.0f, rollInvalid);
            float lastPushedRollDegrees = math.select(_lastPushedRollDegrees, 99999.0f, !math.isfinite(_lastPushedRollDegrees));
            if (rollInvalid)
                _rollVelocityDegrees = 0.0f;

            if (math.abs(rollDegrees - lastPushedRollDegrees) <= RollSignalEpsilonDegrees)
            {
                _rollDegrees = rollDegrees;
                _lastPushedRollDegrees = lastPushedRollDegrees;
                return;
            }

            if (_movement != null)
                _movement.RequestKinematicInertiaRoll(rollDegrees);
            Shader.SetGlobalFloat(_PlayerKinematicRollId, rollDegrees);
            _rollDegrees = rollDegrees;
            _lastPushedRollDegrees = rollDegrees;
        }

        private void ClearRollSignal()
        {
            _rollDegrees = 0.0f;
            _rollVelocityDegrees = 0.0f;
            _lastPushedRollDegrees = 99999.0f;
            PushRollSignal();
        }

        private bool MovementOwnsKinematicAuthority()
        {
            return _movement != null && _movement.isActiveAndEnabled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasCoreEntityStorage()
        {
            return HasMotionSoaStorage() &&
                   HasIntendedMovementStorage() &&
                   _flowVelocity.IsCreated &&
                   _flowVelocity.Length >= EntityCount &&
                   _telemetry.IsCreated &&
                   _telemetry.Length > 0 &&
                   _telemetryWriteIndex.IsCreated &&
                   _telemetryWriteIndex.Length > 0 &&
                   HasFaultFlagStorage();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasMotionSoaStorage()
        {
            return _positions.IsCreated &&
                   _velocities.IsCreated &&
                   _lastValidPositions.IsCreated &&
                   _positions.Length >= EntityCount &&
                   _velocities.Length >= EntityCount &&
                   _lastValidPositions.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasKinematicsStorage()
        {
            return HasMotionSoaStorage();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasVelocityStorage()
        {
            return _velocities.IsCreated && _velocities.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasIntendedMovementStorage()
        {
            return _intendedMovement.IsCreated && _intendedMovement.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasSyncStateReadStorage()
        {
            return _stateRead.IsCreated && _stateRead.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasSyncStateWriteStorage()
        {
            return _stateWrite.IsCreated && _stateWrite.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasFaultFlagStorage()
        {
            return _faultFlags.IsCreated && _faultFlags.Length >= EntityCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadLastValidPosition()
        {
            return _lastValidPositions.IsCreated && _lastValidPositions.Length >= EntityCount
                ? SanitizeFloat3(_lastValidPositions[0], float3.zero)
                : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadPositionSnapshot(float3 fallback)
        {
            float3 safeFallback = SanitizeFloat3(fallback, float3.zero);
            return _positions.IsCreated && _positions.Length >= EntityCount
                ? SanitizeFloat3(_positions[0], safeFallback)
                : safeFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadVelocitySnapshot(float3 fallback)
        {
            float3 safeFallback = SanitizeFloat3(fallback, float3.zero);
            return HasVelocityStorage()
                ? SanitizeFloat3(_velocities[0], safeFallback)
                : safeFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadIntendedMovementSnapshot()
        {
            return HasIntendedMovementStorage()
                ? SanitizeFloat3(_intendedMovement[0], float3.zero)
                : float3.zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadStatePositionSnapshot(float3 fallback)
        {
            float3 safeFallback = SanitizeFloat3(fallback, float3.zero);
            return HasSyncStateReadStorage()
                ? SanitizeFloat3(_stateRead[0].Position, safeFallback)
                : safeFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float3 ReadStateVelocitySnapshot(float3 fallback)
        {
            float3 safeFallback = SanitizeFloat3(fallback, float3.zero);
            return HasSyncStateReadStorage()
                ? SanitizeFloat3(_stateRead[0].Velocity, safeFallback)
                : safeFallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ReadFaultFlags()
        {
            return HasFaultFlagStorage() ? _faultFlags[0] : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddFaultFlag(int flag)
        {
            if (HasFaultFlagStorage())
                _faultFlags[0] |= flag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearFaultFlags()
        {
            if (HasFaultFlagStorage())
                _faultFlags[0] = 0;
        }

        private void StageStateWrite(float3 position, float3 velocity, Quaternion rotation, uint flags)
        {
            StageStateWrite(position, velocity, ToQuaternion(rotation), flags);
        }

        private void StageStateWrite(float3 position, float3 velocity, quaternion rotation, uint flags)
        {
            if (!HasSyncStateWriteStorage())
                return;

            float3 fallbackPosition = ReadStatePositionSnapshot(ReadLastValidPosition());
            bool inputInvalid =
                !math.all(math.isfinite(position)) ||
                !math.all(math.isfinite(velocity));
            float3 safePosition = SanitizeFloat3(position, fallbackPosition);
            float3 safeVelocity = SanitizeFloat3(velocity, float3.zero);
            float3 snappedPosition = SanitizeFloat3(SnapMillimeter(safePosition), fallbackPosition);
            float3 snappedVelocity = SanitizeFloat3(SnapMillimeter(safeVelocity), float3.zero);
            rotation = CanonicalizeRotation(rotation);
            uint safeFlags = inputInvalid ? flags | (uint)FaultNaN : flags;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(snappedPosition));
            uint hash = BuildSyncFenceHash(in aup, snappedVelocity, rotation);
            _stateWrite[0] = new PlayerKinematicsSyncState
            {
                Position = snappedPosition,
                Velocity = snappedVelocity,
                Rotation = rotation,
                Frame = (uint)Time.frameCount,
                Flags = safeFlags,
                StateHash = hash
            };
            _stateWriteReady = true;
        }

        private void CommitStateWrite()
        {
            if (!_stateWriteReady ||
                !HasSyncStateReadStorage() ||
                !HasSyncStateWriteStorage() ||
                !HasMotionSoaStorage())
            {
                return;
            }

            PlayerKinematicsSyncState state = _stateWrite[0];
            _stateRead[0] = state;
            _positions[0] = state.Position;
            _velocities[0] = state.Velocity;
            if ((state.Flags & (uint)(FaultNaN | FaultSolidTeleport)) == 0u)
                _lastValidPositions[0] = state.Position;

            Vector3 position = ToVector3(state.Position);
            Vector3 velocity = ToVector3(state.Velocity);
            if (_motor != null)
            {
                _motor.MovePosition(position);
                _motor.SetLinearVelocity(velocity);
            }
            else if (_body != null)
            {
                _body.MovePosition(position);
                _body.linearVelocity = velocity;
            }

            if (_body != null && (state.Flags & SyncStateFlagApplyRotation) != 0u)
            {
                Quaternion rotation = ToUnityQuaternion(state.Rotation);
                if (IsFinite(rotation))
                    _body.MoveRotation(rotation);
            }

            _stateWriteReady = false;
        }

        private void ApplyPendingStateCorrections()
        {
            for (int i = 0; i < StateCorrectionDrainLimit; i++)
            {
                if (!PhysicsDeterminismSignals.TryDequeueStateCorrection(out StateCorrectionSignal correction))
                    return;

                if (correction.SourceId != 0u && correction.SourceId != _sourceId)
                    continue;

                uint comparisonHash = correction.ExpectedLocalHash != 0u
                    ? correction.ExpectedLocalHash
                    : correction.AuthoritativeHash;
                uint authoritativeHash = correction.AuthoritativeHash != 0u
                    ? correction.AuthoritativeHash
                    : comparisonHash;
                uint localHash = BuildCurrentSyncFenceHash();
                if (comparisonHash != 0u &&
                    localHash != comparisonHash)
                {
                    EmitDesyncDetected(localHash, authoritativeHash, correction.Frame, correction.Flags);
                }

                float3 correctionPosition = ResolveCorrectionPosition(in correction);
                float3 correctionVelocity = ResolveCorrectionVelocity(in correction);
                quaternion correctionRotation = ResolveCorrectionRotation(in correction);
                bool hasRotationPayload =
                    (correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0;
                uint flags = SyncStateFlagCorrection | (uint)FaultStateCorrection;
                if (hasRotationPayload && IsFinite(ToUnityQuaternion(correctionRotation)))
                    flags |= SyncStateFlagApplyRotation;

                StageStateWrite(correctionPosition, correctionVelocity, correctionRotation, flags);
            }
        }

        private void PublishSyncFence()
        {
            if (_body == null)
                return;

            float3 position = HasSyncStateReadStorage()
                ? _stateRead[0].Position
                : SnapMillimeter(SanitizeFloat3(ToFloat3(_body.position), ReadLastValidPosition()));
            float3 velocity = HasSyncStateReadStorage()
                ? _stateRead[0].Velocity
                : SnapMillimeter(SanitizeFloat3(ToFloat3(_body.linearVelocity), float3.zero));
            position = SanitizeFloat3(position, ReadLastValidPosition());
            velocity = SanitizeFloat3(velocity, float3.zero);
            quaternion rotation = HasSyncStateReadStorage() ? _stateRead[0].Rotation : CanonicalizeRotation(ToQuaternion(_body.rotation));
            Vector3 runtimePosition = ToVector3(position);
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            uint hash = BuildSyncFenceHash(in aup, velocity, rotation);
            float maxDriftErrorMeters = ResolveAupMaxDriftErrorMeters(in aup, position);
            _accumulatorState.LastSyncFenceHash = hash;
            _accumulatorState.LastSyncFenceFrame = (uint)Time.frameCount;

            SyncFenceSignal signal = default;
            signal.PositionAup = aup;
            signal.RuntimePosition = position;
            signal.Velocity = velocity;
            signal.Rotation = rotation;
            signal.StateHash = hash;
            signal.Frame = _accumulatorState.LastSyncFenceFrame;
            signal.SourceId = _sourceId;
            signal.Flags = 0;
            PhysicsDeterminismSignals.Publish(in signal);
            WriteSyncFenceTelemetry(in signal, maxDriftErrorMeters);
            CrashTelemetryBuffer.ReportAupMaxDriftError(runtimePosition, maxDriftErrorMeters);
        }

        private static float ResolveAupMaxDriftErrorMeters(in AbsoluteUniversePosition aup, float3 runtimePosition)
        {
            double3 expectedRuntime = aup.ToAbsoluteDouble3() - HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double3 measuredRuntime = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            double3 drift = math.abs(expectedRuntime - measuredRuntime);
            double maxAxis = math.max(drift.x, math.max(drift.y, drift.z));
            if (!math.isfinite(maxAxis))
                return float.MaxValue;

            return (float)math.min(maxAxis, (double)float.MaxValue);
        }

        private void WriteSyncFenceTelemetry(in SyncFenceSignal signal, float maxDriftErrorMeters)
        {
            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = SanitizeFloat3(signal.RuntimePosition, float3.zero),
                Velocity = SanitizeFloat3(signal.Velocity, float3.zero),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = 0.0f,
                Frame = signal.Frame,
                Flags = FaultSyncFence,
                SyncFenceHash = signal.StateHash,
                AuxFlags = HectonFloatingOrigin.CurrentShiftSequence | AupDriftTelemetryFlag,
                AupMaxDriftErrorMeters = SanitizeNonNegative(maxDriftErrorMeters)
            };
        }

        private void EmitDesyncDetected(uint localHash, uint authoritativeHash, uint frame, byte flags)
        {
            DesyncDetectedSignal signal = default;
            signal.LocalHash = localHash;
            signal.AuthoritativeHash = authoritativeHash;
            signal.Frame = frame != 0u ? frame : (uint)Time.frameCount;
            signal.SourceId = _sourceId;
            signal.LastFenceFrame = _accumulatorState.LastSyncFenceFrame;
            signal.Flags = flags;
            PhysicsDeterminismSignals.Publish(in signal);
            AddFaultFlag(FaultDesync);
            DumpFaultTelemetryIfNeeded();
        }

        private uint BuildCurrentSyncFenceHash()
        {
            if (HasSyncStateReadStorage())
            {
                PlayerKinematicsSyncState state = _stateRead[0];
                float3 position = SanitizeFloat3(state.Position, ReadLastValidPosition());
                float3 velocity = SanitizeFloat3(state.Velocity, float3.zero);
                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(position));
                return BuildSyncFenceHash(in aup, velocity, CanonicalizeRotation(state.Rotation));
            }

            if (_body == null)
                return 0u;

            float3 bodyPosition = SanitizeFloat3(ToFloat3(_body.position), ReadLastValidPosition());
            float3 bodyVelocity = SanitizeFloat3(ToFloat3(_body.linearVelocity), float3.zero);
            AbsoluteUniversePosition bodyAup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(bodyPosition));
            return BuildSyncFenceHash(in bodyAup, bodyVelocity, CanonicalizeRotation(ToQuaternion(_body.rotation)));
        }

        private static PlayerKinematicsSyncState RehashState(PlayerKinematicsSyncState state)
        {
            state.Position = SanitizeFloat3(state.Position, float3.zero);
            state.Velocity = SanitizeFloat3(state.Velocity, float3.zero);
            state.Rotation = CanonicalizeRotation(state.Rotation);
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(ToVector3(state.Position));
            state.StateHash = BuildSyncFenceHash(in aup, state.Velocity, state.Rotation);
            return state;
        }

        private static uint BuildSyncFenceHash(in AbsoluteUniversePosition aup, float3 velocity, quaternion rotation)
        {
            uint hash = DeterministicPhysicsMath.FnvOffsetBasis;
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridX);
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridY);
            hash = DeterministicPhysicsMath.Fnv1a(hash, aup.GridZ);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalX);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalY);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, aup.LocalZ);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.x);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.y);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, velocity.z);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.x);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.y);
            hash = DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.z);
            return DeterministicPhysicsMath.Fnv1aQuantizedMillimeter(hash, rotation.value.w);
        }

        private void DumpFaultTelemetryIfNeeded()
        {
            if ((_dumpWrittenForFault && _desyncDumpWritten) ||
                !HasFaultFlagStorage() ||
                !_telemetry.IsCreated ||
                _telemetry.Length <= 0 ||
                _faultFlags[0] == 0)
            {
                return;
            }

            _dumpWrittenForFault = true;
            if ((_faultFlags[0] & FaultDesync) != 0)
                _desyncDumpWritten = true;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string logDirectory = Path.Combine(projectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(logDirectory);
            string physicsPath = Path.Combine(logDirectory, "Dump_PHYSICS_DETERMINISM_SYNC.bin");
            string ikPath = Path.Combine(logDirectory, "Dump_PLAYER_IK_ENVIRONMENT_ADAPTER.bin");
            string aupWatchdogPath = Path.Combine(logDirectory, AupWatchdogDumpFileName);
            string sdfSqueezePath = Path.Combine(logDirectory, SdfSqueezeDumpFileName);
            WriteTelemetryDump(physicsPath, 0x48503844u);
            WriteTelemetryDump(ikPath, 0x50494B42u);
            WriteTelemetryDump(aupWatchdogPath, AupWatchdogDumpMagic);
            WriteTelemetryDump(sdfSqueezePath, SdfSqueezeDumpMagic);
        }

        private void WriteTelemetryDump(string path, uint magic)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(magic);
                writer.Write(_faultFlags[0]);
                int telemetryHead = ResolveTelemetryHeadIndex();
                writer.Write(telemetryHead);
                writer.Write(_accumulatorState.LastSyncFenceHash);
                writer.Write(_accumulatorState.LastSyncFenceFrame);
                int telemetryLength = _telemetry.Length;
                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = telemetryHead + i;
                    if (telemetryIndex >= telemetryLength)
                        telemetryIndex -= telemetryLength;

                    PlayerKinematicsRuntimeTelemetryEntry entry = _telemetry[telemetryIndex];
                    writer.Write(entry.Position.x);
                    writer.Write(entry.Position.y);
                    writer.Write(entry.Position.z);
                    writer.Write(entry.Velocity.x);
                    writer.Write(entry.Velocity.y);
                    writer.Write(entry.Velocity.z);
                    writer.Write(entry.IntendedMovement.x);
                    writer.Write(entry.IntendedMovement.y);
                    writer.Write(entry.IntendedMovement.z);
                    writer.Write(entry.DragCoefficient);
                    writer.Write(entry.WaterDensity);
                    writer.Write(entry.SolidDensity);
                    writer.Write(entry.Frame);
                    writer.Write(entry.Flags);
                    writer.Write(entry.SyncFenceHash);
                    writer.Write(entry.AuxFlags);
                    writer.Write(entry.AupMaxDriftErrorMeters);
                }
            }
        }

        private float3 SafeRight()
        {
            Transform source = _cameraTransform != null ? _cameraTransform : _cachedTransform;
            return source != null ? SafeNormalize(ToFloat3(source.right), new float3(1.0f, 0.0f, 0.0f)) : new float3(1.0f, 0.0f, 0.0f);
        }

        private static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > 0.000001f && math.all(math.isfinite(value))
                ? value * math.rsqrt(math.max(lengthSq, 0.000001f))
                : fallback;
        }

        private static float3 ApplyForwardSpeedPenalty(float3 velocity, float3 intendedMovement, float penalty01)
        {
            float3 safeVelocity = SanitizeFloat3(velocity, float3.zero);
            float3 intended = SafeNormalize(intendedMovement, float3.zero);
            if (math.lengthsq(intended) <= 0.000001f)
                intended = SafeNormalize(safeVelocity, float3.zero);
            if (math.lengthsq(intended) <= 0.000001f)
                return safeVelocity;

            float forwardSpeed = math.dot(safeVelocity, intended);
            if (!math.isfinite(forwardSpeed) || forwardSpeed <= 0.0f)
                return safeVelocity;

            return safeVelocity - intended * (forwardSpeed * SanitizeUnit(penalty01));
        }

        private static float SanitizeUnit(float value)
        {
            return math.select(math.saturate(value), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeSignedUnit(float value)
        {
            return math.select(math.clamp(value, -1.0f, 1.0f), 0.0f, !math.isfinite(value));
        }

        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        private bool TryReserveTelemetrySlot(out int wrappedIndex)
        {
            wrappedIndex = 0;
            if (!_telemetry.IsCreated ||
                !_telemetryWriteIndex.IsCreated ||
                _telemetry.Length <= 0 ||
                _telemetryWriteIndex.Length <= 0)
            {
                return false;
            }

            int telemetryLength = _telemetry.Length;
            int writeIndex = math.max(0, _telemetryWriteIndex[0]);
            wrappedIndex = writeIndex % telemetryLength;
            _telemetryWriteIndex[0] = (wrappedIndex + 1) % telemetryLength;
            return true;
        }

        private int ResolveTelemetryHeadIndex()
        {
            if (!_telemetry.IsCreated ||
                !_telemetryWriteIndex.IsCreated ||
                _telemetry.Length <= 0 ||
                _telemetryWriteIndex.Length <= 0)
            {
                return 0;
            }

            int writeIndex = math.max(0, _telemetryWriteIndex[0]);
            return writeIndex % _telemetry.Length;
        }

        private static float SmoothScalar(float current, float target, float sharpness, float deltaTime)
        {
            float t = SanitizeUnit(SanitizeNonNegative(sharpness) * SanitizeNonNegative(deltaTime));
            float safeCurrent = math.select(current, 0.0f, !math.isfinite(current));
            float safeTarget = math.select(target, 0.0f, !math.isfinite(target));
            return math.lerp(safeCurrent, safeTarget, t);
        }

        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                DeterministicPhysicsMath.SnapMillimeter(value.x),
                DeterministicPhysicsMath.SnapMillimeter(value.y),
                DeterministicPhysicsMath.SnapMillimeter(value.z));
        }

        private float3 ResolveCorrectionPosition(in StateCorrectionSignal correction)
        {
            bool runtimePositionFlagged =
                (correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRuntimePositionValid) != 0;
            if (runtimePositionFlagged && math.all(math.isfinite(correction.RuntimePosition)))
                return SnapMillimeter(correction.RuntimePosition);

            bool hasAupPayload =
                correction.PositionAup.GridX != 0L ||
                correction.PositionAup.GridY != 0L ||
                correction.PositionAup.GridZ != 0L ||
                correction.PositionAup.LocalX != 0.0f ||
                correction.PositionAup.LocalY != 0.0f ||
                correction.PositionAup.LocalZ != 0.0f;
            if (!hasAupPayload)
                return ReadStatePositionSnapshot(ReadPositionSnapshot(float3.zero));

            return SnapMillimeter(SanitizeFloat3(correction.PositionAup.ToRuntimeFloat3(), ReadStatePositionSnapshot(float3.zero)));
        }

        private float3 ResolveCorrectionVelocity(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagVelocityValid) != 0 &&
                math.all(math.isfinite(correction.Velocity)))
            {
                return SnapMillimeter(correction.Velocity);
            }

            return ReadStateVelocitySnapshot(ReadVelocitySnapshot(float3.zero));
        }

        private quaternion ResolveCorrectionRotation(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & PhysicsDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0)
                return CanonicalizeRotation(correction.Rotation);

            return _body != null ? CanonicalizeRotation(ToQuaternion(_body.rotation)) : quaternion.identity;
        }

        private static bool IsFiniteNonZero(float3 value)
        {
            return math.all(math.isfinite(value)) && math.lengthsq(value) > 0.000001f;
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static quaternion ToQuaternion(Quaternion value)
        {
            return new quaternion(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToUnityQuaternion(quaternion value)
        {
            return new Quaternion(value.value.x, value.value.y, value.value.z, value.value.w);
        }

        private static quaternion CanonicalizeRotation(quaternion value)
        {
            float4 v = value.value;
            float lengthSq = math.lengthsq(v);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return quaternion.identity;

            v *= math.rsqrt(math.max(lengthSq, 0.000001f));
            if (v.w < 0.0f)
                v = -v;
            return new quaternion(v);
        }

        private static bool IsFinite(Quaternion value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static bool IsFinite(Vector4 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static uint ResolveBodyFlags(byte ladderActive, byte inSolid)
        {
            uint flags = 0u;
            flags |= math.select(0u, BodyFlagLadderActive, ladderActive != 0);
            flags |= math.select(0u, BodyFlagInSolid, inSolid != 0);
            return flags;
        }

        private bool IsHighScalabilityTier()
        {
            HectonQualityTier tier = ResolveScalabilityTier();
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private static bool IsLowTier(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
        }

        private static bool IsFreshSignalFrame(uint currentFrame, uint signalFrame, uint maxAgeFrames)
        {
            if (signalFrame > currentFrame)
                return false;

            return currentFrame - signalFrame <= maxAgeFrames;
        }

        private bool HasHandProbeStorage(int requiredProbeCount)
        {
            return _handProbeCommands.IsCreated &&
                   _handProbeHits.IsCreated &&
                   _handProbeCommands.Length >= requiredProbeCount &&
                   _handProbeHits.Length >= requiredProbeCount;
        }

        private bool HasHandProbeHitStorage(int requiredProbeCount)
        {
            return _handProbeHits.IsCreated && _handProbeHits.Length >= requiredProbeCount;
        }

        private bool HasHandTargetWriteStorage()
        {
            return _handTargets.IsCreated && _handTargets.Length >= HandTargetCount;
        }

        private bool HasHandTargetStorage()
        {
            return HasHandTargetWriteStorage() &&
                   _smoothedHandTargets.IsCreated &&
                   _smoothedHandTargets.Length >= HandTargetCount;
        }

        private static float WrapPositivePhase(float phase, float wrap)
        {
            float safeWrap = math.max(SanitizeNonNegative(wrap), 0.0001f);
            if (!math.isfinite(phase))
                return 0.0f;

            phase -= math.floor(phase * math.rcp(safeWrap)) * safeWrap;
            return !math.isfinite(phase) || phase < 0.0f ? 0.0f : phase;
        }

        private int ResolveGpuFlowProbeFrameMask()
        {
            HectonQualityTier tier = ResolveScalabilityTier();
            if (IsLowTier(tier))
                return 3;

            return tier == HectonQualityTier.Mid ? 1 : 0;
        }

        private HectonQualityTier ResolveScalabilityTier()
        {
            return _cachedScalabilityTier;
        }

        private static float ResolveSdfSampleStepMeters(float3 voxelCellSize)
        {
            if (!math.all(math.isfinite(voxelCellSize)))
                return 0.25f;

            float maxCellSize = math.cmax(math.max(math.abs(voxelCellSize), new float3(0.025f)));
            return math.clamp(maxCellSize * 0.75f, 0.08f, 0.45f);
        }

        private static float SignedTriangleWave(float radians)
        {
            radians = math.select(radians, 0.0f, !math.isfinite(radians));
            float unit = math.frac(radians * InvTwoPi);
            return 1.0f - math.abs((unit * 4.0f) - 2.0f);
        }

    }
}
