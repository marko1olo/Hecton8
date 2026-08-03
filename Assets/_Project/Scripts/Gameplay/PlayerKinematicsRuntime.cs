using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Physics.KCC;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using HydrodynamicKccMath = Hecton8.Physics.KCC.HydrodynamicKccMath;
using HydrodynamicKccRuntime = Hecton8.Physics.KCC.HydrodynamicKccRuntime;

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
        [NoAlias] public NativeArray<float3> Positions;
        [NoAlias] public NativeArray<float3> Velocities;
        [NoAlias] public NativeArray<float3> IntendedMovement;
        [NoAlias] public NativeArray<float3> FlowVelocity;
        [NoAlias] public NativeArray<float3> LastValidPositions;
        [ReadOnly, NoAlias] public NativeArray<WhirlpoolFlow>.ReadOnly ActiveMaelstroms;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly VoxelSdfTexture3D;
        [NoAlias] public NativeArray<PlayerKinematicsRuntimeTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryWriteIndex;
        [NoAlias] public NativeArray<int> FaultFlags;
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
        public float DepthMeters;
        public float SuitIntegrity01;
        public float DragDepthScaleMax;
        public float DragBrokenSuitMultiplier;
        public float MaxDragClamp;
        public float DragMathEpsilon;

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
                    runtimeFlags |= PlayerKinematicsRuntime.BodyFlagSdfReducedGradientSamples;
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

            float speed = math.length(velocity);
            float dragDecel = Hecton8.PureLogic.Kinematics.SomaticDragCurveCalculator.Compute(speed, DepthMeters, SuitIntegrity01, drag, DragDepthScaleMax, DragBrokenSuitMultiplier, MaxDragClamp, DragMathEpsilon);
            float dragTerm = SanitizeNonNegative(dragDecel * density * dt);
            velocity *= math.rcp(1.0f + dragTerm);
            velocity += SanitizeFloat3(FlowVelocity[0], float3.zero) * dt;
            if (ActiveMaelstromCount > 0)
            {
                float3 maelstromVelocity = FluidAnalyticalContractMath.SampleWhirlpoolVelocity(
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
                DeterministicContractMath.SnapMillimeter(value.x),
                DeterministicContractMath.SnapMillimeter(value.y),
                DeterministicContractMath.SnapMillimeter(value.z));
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
            NativeArray<byte>.ReadOnly encodedSdf,
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
            NativeArray<byte>.ReadOnly encodedSdf,
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
        private static float DecodeSdfAt(NativeArray<byte>.ReadOnly encodedSdf, int3 gridDimensions, int x, int y, int z, float sdfRange)
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

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct PlayerKinematicsProbeHit
    {
        public const uint FlagHit = 1u;

        [FieldOffset(0)] public float3 Point;
        [FieldOffset(12)] public float3 Normal;
        [FieldOffset(24)] public float Distance;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public int ColliderInstanceId;
        [FieldOffset(36)] public int MaterialId;
        [FieldOffset(40)] public float3 ReservedVector;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public ulong RouteHash;
    }

    internal ref struct PlayerKinematicsHandPlacementSolver
    {
        public const byte RuntimeFlagReducedProbeSet = 1 << 0;
        public const byte RuntimeFlagImpact = 1 << 1;

        [ReadOnly, NoAlias] public NativeArray<PlayerKinematicsProbeHit> Hits;
        [NoAlias] public NativeArray<PlayerKinematicsHandTarget> Targets;
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
        public byte ProbeCount;
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
            int probeCount = math.clamp((int)ProbeCount, 1, 4);
            if ((RuntimeFlags & RuntimeFlagReducedProbeSet) != 0 || probeCount <= 1)
            {
                TryBuildProbeTarget(0, -1.0f, 0.18f, braceBaseBlend, forward, right, up, ref leftTarget);
                TryBuildProbeTarget(0, 1.0f, 0.18f, braceBaseBlend, forward, right, up, ref rightTarget);
            }
            else
            {
                TryBuildProbeTarget(0, -1.0f, 0.04f, braceBaseBlend, forward, right, up, ref leftTarget);
                TryBuildProbeTarget(math.min(1, probeCount - 1), 1.0f, 0.04f, braceBaseBlend, forward, right, up, ref rightTarget);
                if (leftTarget.Hit == 0 && probeCount > 2)
                    TryBuildBestCentralTarget(-1.0f, braceBaseBlend, forward, right, up, ref leftTarget);
                if (rightTarget.Hit == 0 && probeCount > 2)
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

            PlayerKinematicsProbeHit hit = Hits[hitIndex];
            float3 hitPoint = hit.Point;
            float3 hitNormal = hit.Normal;
            if (!HasHit(in hit, hitPoint, hitNormal))
            {
                return false;
            }

            float3 normal = SafeNormalize(hitNormal, new float3(0.0f, 1.0f, 0.0f));
            float safeBraceDistance = math.max(0.001f, SanitizeNonNegative(BraceDistance));
            float safeHitDistance = math.clamp(hit.Distance, 0.0f, math.max(0.001f, SanitizeNonNegative(MaxProbeDistance)));
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

        private static bool HasHit(in PlayerKinematicsProbeHit hit, float3 point, float3 normal)
        {
            if ((hit.Flags & PlayerKinematicsProbeHit.FlagHit) == 0u ||
                !math.isfinite(hit.Distance) ||
                hit.Distance < 0.0f ||
                !math.all(math.isfinite(point)) ||
                !math.all(math.isfinite(normal)))
            {
                return false;
            }

            return hit.Distance > 0.0f || math.lengthsq(normal) > 0.0001f;
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
    public sealed partial class PlayerKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IFastTickable, ILateFrameTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private int _signalPushDropCount;
        private struct VaultBufferBinding<T>
            where T : struct
        {
            public VaultGenerationHandle<T> Handle;
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
                if (!TryResolveRequired(dataVault, in Handle, RequiredLength))
                {
                    if (!dataVault.TryGetGenerationHandle(BufferId, out VaultGenerationHandle<T> existing) ||
                        !TryResolveRequired(dataVault, in existing, RequiredLength))
                    {
                        existing = dataVault.EnsureGenerationHandle<T>(BufferId, RequiredLength, OwnerSystemId, options);
                    }

                    Handle = existing;
                }

                NativeArray<T> buffer = ResolveExisting(dataVault);
                return buffer.IsCreated && buffer.Length >= RequiredLength;
            }

            public bool TryBindExisting(IDataVault dataVault)
            {
                if (dataVault == null || RequiredLength <= 0)
                {
                    Handle = default;
                    _vault = null;
                    return false;
                }

                _vault = dataVault;
                if (TryResolveRequired(dataVault, in Handle, RequiredLength))
                    return true;

                if (!dataVault.TryGetGenerationHandle(BufferId, out VaultGenerationHandle<T> existing) ||
                    !TryResolveRequired(dataVault, in existing, RequiredLength))
                {
                    Handle = default;
                    return false;
                }

                Handle = existing;
                return true;
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
                if (dataVault == null || !IsHandleCreated(in Handle))
                    return default;

                return dataVault.TryResolveHandle(in Handle, out NativeArray<T> buffer)
                    ? buffer
                    : default;
            }

            private static bool TryResolveRequired(IDataVault dataVault, in VaultGenerationHandle<T> handle, int requiredLength)
            {
                if (dataVault == null || !IsHandleCreated(in handle))
                    return false;

                return dataVault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                       buffer.IsCreated &&
                       buffer.Length >= requiredLength;
            }

            private static bool IsHandleCreated(in VaultGenerationHandle<T> handle)
            {
                return handle.BufferID != 0u && handle.Generation != 0u;
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
        internal const uint BodyFlagSdfReducedGradientSamples = 1u << 4;
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
        private const int MinimumQualityHandProbeFrameMask = 3;
        private const uint IkBraceTelemetryFlag = 1u << 16;
        private const uint IkSqueezeTelemetryFlag = 1u << 17;
        private const uint IkImpactTelemetryFlag = 1u << 18;
        private const uint IkReducedProbeTelemetryFlag = 1u << 19;
        private const uint IkScrapeTelemetryFlag = 1u << 20;
        private const uint AupPreShiftHaltTelemetryFlag = 1u << 21;
        private const uint AupDriftTelemetryFlag = 1u << 22;
        private const uint SdfSqueezeSlowTelemetryFlag = 1u << 23;
        private const uint SdfSqueezeNanTelemetryFlag = 1u << 24;
        private const uint HydrodynamicAuthorityTelemetryFlag = 1u << 25;
        private const int PreShiftHaltFrameCount = 1;
        private const float InvTwoPi = 0.15915494309f;
        private const float RollSignalEpsilonDegrees = 0.01f;
        private const uint AupWatchdogDumpMagic = 0x41555044u;
        private const uint SdfSqueezeDumpMagic = 0x5344464Bu;
        private const byte PlayerStateReducedGradientCompatibilityFlag = 1 << 2;
        private const string AupWatchdogDumpFileName = "Dump_AUP_DETERMINISM_WATCHDOG.bin";
        private const string SdfSqueezeDumpFileName = "Dump_KCC_SDF_SQUEEZE_RESOLVER.bin";
        private static readonly int _PlayerSwimVatSpeedId = Shader.PropertyToID("_HectonSwimVatSpeedScalar");
        private static readonly int _PlayerKinematicRollId = Shader.PropertyToID("_H8PlayerKinematicRoll");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // One-shot latch for the dead-publisher advisory in ApplyPendingStateCorrections. That method runs on
        // the IPostFixedTickable fixed-step cadence, so after the first fire the advisory must cost one static
        // bool read and nothing else. Reset per play session by ResetStateCorrectionLaneDiagnostics so the
        // warning still appears on the second play when Enter Play Mode without domain reload is enabled.
        private static bool s_stateCorrectionLaneDeadWarned;
#endif

        [SerializeField] private LayerMask handProbeLayerMask = HectonLayerMasks.StrictInteractionLayerMask;
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
        private VaultBufferBinding<PlayerKinematicsProbeHit> _handProbeHits = new VaultBufferBinding<PlayerKinematicsProbeHit>(BufferID.PlayerKinematicHandProbeHits, EnvironmentProbeCount, OwnerSystemId);
        private VaultBufferBinding<SdfSqueezeResult> _sdfSqueezeResults = new VaultBufferBinding<SdfSqueezeResult>(BufferID.PlayerKinematicSdfSqueezeResults, EntityCount, OwnerSystemId);
        private VaultGenerationHandle<LockstepPlayerKinematicState> _playerKinematicStateHandle;
        private VaultGenerationHandle<BulkheadCollisionResultDTO> _bulkheadCollisionResultsHandle;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredFast;
        private bool _registeredLate;
        private bool _registeredOriginShift;
        private bool _registeredHotSwap;
        private bool _dumpWrittenForFault;
        private bool _desyncDumpWritten;
        private bool _stateWriteReady;
        private bool _hasAuthoritativePoseSnapshot;
        private Rigidbody _body;
        private IPlayerKinematicsMovementRuntime _movement;
        private IPlayerKinematicsMotorSyncSink _motor;
        private IPlayerKinematicsMotorSyncSink _localMotor;
        private HydrodynamicKccRuntime _hydrodynamicKccRuntime;
        private HydrodynamicKccRuntime _localHydrodynamicKccRuntime;
        private PlayerInventory _inventory;
        private PlayerInventory _localInventory;
        private HectonSurvivalSystem _survival;
        private HectonSurvivalSystem _localSurvival;
        private IDataVault _dataVault;
        private IGasDynamicsSolver _gasDynamics;
        private IAbyssalFlowGpuReadModel _fluidGpuReadModel;
        private IAnalyticalFlowReadModel _analyticalFlowReadModel;
        private HectonVoxelEngine _voxelEngine;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private Transform _cachedTransform;
        private Transform _cameraTransform;
        private float _rollDegrees;
        private float _rollVelocityDegrees;
        private float _rollPhaseRadians;
        private float _lastVatSpeedScalar = -1.0f;
        private float _lastPushedRollDegrees = 99999.0f;
        private int _nextColdRebindFrame;
        private uint _nextBulkheadCollisionHandleBindFrame;
        private int _cadenceSalt;
        private float _cachedGlobalQualityWeight01 = 1.0f;
        private uint _lastConsumedSqueezeSignalFrame;
        private uint _lastConsumedSqueezeSignalSourceHash;
        private uint _sourceId;
        private PlayerKinematicsAccumulatorState _accumulatorState;

        private Vector3 ResolveBodyRuntimePosition()
        {
            if (TryReadAuthoritativePositionSnapshot(out float3 snapshotPosition))
                return ToVector3(snapshotPosition);

            if (_cachedTransform != null)
                return _cachedTransform.position;

            if (_body != null)
                return _body.position;

            return transform.position;
        }
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
        private bool _pendingMovementAcousticDirty;
        private MovementAcousticSignal _pendingMovementAcoustic;
        private bool _pendingSdfSqueezeHapticDirty;
        private HapticRequest _pendingSdfSqueezeHaptic;
        private bool _pendingSdfSqueezeAcousticDirty;
        private AcousticPingSignal _pendingSdfSqueezeAcoustic;
        private bool _pendingBraceHapticDirty;
        private HapticRequest _pendingBraceHaptic;
        private bool _pendingGloveScrapeAcousticDirty;
        private AcousticPingSignal _pendingGloveScrapeAcoustic;
        private bool _hasImpactBracePoint;
        private bool _lastProbeReduced;
        private int _lastProbeCount;
        private bool _wasBraceActive;

        private void Awake()
        {
            _cachedTransform = transform;
            CacheLocalComponentsCold();
            _sourceId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            _cadenceSalt = unchecked((int)_sourceId);
            RebindServices(allowHierarchyLookup: true);
            RefreshHandIkFloatingOriginSnapshotCold(HectonFloatingOrigin.CurrentTotalOffsetDouble);
            AllocateNativeState();
        }

        private void OnEnable()
        {
            ResetDeterminismSessionState();
            RefreshHandIkFloatingOriginSnapshotCold(HectonFloatingOrigin.CurrentTotalOffsetDouble);
            RebindServices(allowHierarchyLookup: false);
            WarmRuntimeStateOnEnable();
            RegisterRuntime();
        }

        private void OnDisable()
        {
            ClearRollSignal();
            UnregisterRuntime();
            CompleteHandFabrikIkForTeardown();
            ClearHandTargets();
            ClearQueuedFeedbackSignals();
        }

        private void OnDestroy()
        {
            UnregisterRuntime();
            CompleteHandFabrikIkForTeardown();
            ClearHandTargets();
            ClearQueuedFeedbackSignals();
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

            float qualityWeight01 = RefreshGlobalQualityWeight01();
            if (HydrodynamicKccOwnsAuthority())
            {
                ConsumeHydrodynamicKccAuthoritySnapshot(qualityWeight01);
                TickInertiaRoll(fixedDeltaTime);
                return;
            }

            if (MovementOwnsKinematicAuthority())
            {
                byte externalFlags = KccVelocitySignal.FlagMovementAuthorityExternal;

                Vector3 authorityBodyPosition = _body.position;
                Vector3 authorityBodyVelocity = _body.linearVelocity;
                float3 rawAuthorityPosition = (float3)(authorityBodyPosition);
                float3 rawAuthorityVelocity = (float3)(authorityBodyVelocity);
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
            byte sdfGradientProbeRequested = ResolveSdfGradientProbeRequest();
            NativeArray<byte>.ReadOnly sdfTexture3D = default;
            int3 sdfDimensions = default;
            float3 sdfOrigin = float3.zero;
            float3 sdfCellSize = float3.zero;
            float sdfRange = 0.0f;
            byte sdfSampleMode = ResolveSdfSampleMode(qualityWeight01, Hecton8.Core.SystemDispatcher.CurrentFrameId);
            HectonVoxelVolume sdfLeaseVolume = null;
            HectonVoxelVolume.PublishedSonarSdfReadLease sdfReadLease = default;
            bool sdfReadLeaseLocked = false;
            float3 rawBodyPosition = (float3)(ResolveBodyRuntimePosition());
            float3 rawBodyVelocity = ReadVelocitySnapshot(float3.zero);
            float3 bodyPosition = SanitizeFloat3(rawBodyPosition, ReadLastValidPosition());
            float3 bodyVelocity = SanitizeFloat3(rawBodyVelocity, float3.zero);
            bool rawBodyStateInvalid = !math.all(math.isfinite(rawBodyPosition)) || !math.all(math.isfinite(rawBodyVelocity));
            Vector3 safeBodyPosition = ToVector3(bodyPosition);
            uint frameId = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            bool needsSdfPayload =
                inSolid != 0 ||
                sdfGradientProbeRequested != 0 ||
                _sdfSqueezeSlowHoldFrames > 0 ||
                ShouldRefreshPassiveSdfPayload(qualityWeight01, frameId);

            if (needsSdfPayload)
            {
                SnapshotSdfPayload(
                    safeBodyPosition,
                    out sdfTexture3D,
                    out sdfDimensions,
                    out sdfOrigin,
                    out sdfCellSize,
                    out sdfRange,
                    out sdfSampleMode,
                    out sdfLeaseVolume,
                    out sdfReadLease,
                    out sdfReadLeaseLocked);
            }
            SdfSqueezeResult sdfSqueezeResult = default;
            try
            {
                SnapshotLadder(out byte ladderActive, out float3 ladderPoint);

                _positions[0] = bodyPosition;
                _velocities[0] = bodyVelocity;
                _flowVelocity[0] = SanitizeFloat3(ResolveCurrentAdvection(safeBodyPosition), float3.zero);
                if (TryApplySdfSqueeze(
                        fixedDeltaTime,
                        qualityWeight01,
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

                NativeArray<WhirlpoolFlow>.ReadOnly activeMaelstroms = default;
                int activeMaelstromCount = 0;
                IAnalyticalFlowReadModel analyticalFlow = _analyticalFlowReadModel;
                if (analyticalFlow != null &&
                    analyticalFlow.TryGetActiveWhirlpoolFlows(out NativeArray<WhirlpoolFlow>.ReadOnly fluidMaelstroms, out int fluidMaelstromCount))
                {
                    activeMaelstroms = fluidMaelstroms;
                    activeMaelstromCount = fluidMaelstromCount;
                }

                float suitIntegrity = 1f;
                if (_dataVault != null)
                {
                    if (_dataVault.TryGetGenerationHandle<Hecton8.Core.Contracts.Physiology.SuitIntegrityDTO>(BufferID.ShinobuSuitIntegrityStates, out var handle))
                    {
                        if (_dataVault.TryReadOnlyHandle(in handle, out NativeArray<Hecton8.Core.Contracts.Physiology.SuitIntegrityDTO>.ReadOnly states) && states.Length > 0)
                        {
                            suitIntegrity = states[0].CurrentIntegrity01;
                        }
                    }
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
                    LowMaelstromTier = IsReducedSdfSampleMode(sdfSampleMode) ? (byte)1 : (byte)0,
                    SdfSampleMode = sdfSampleMode,
                    SdfGradientProbeRequested = sdfGradientProbeRequested,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    RuntimeFlags = ResolveBodyFlags(ladderActive, inSolid) |
                                   math.select(0u, BodyFlagMaelstromActive, activeMaelstromCount > 0) |
                                   math.select(0u, BodyFlagSdfSqueezeIntervention, SdfSqueezeResult.IsResultActive(in sdfSqueezeResult)),
                    DepthMeters = math.max(0f, -bodyPosition.y),
                    SuitIntegrity01 = suitIntegrity,
                    DragDepthScaleMax = 100f,
                    DragBrokenSuitMultiplier = 2f,
                    MaxDragClamp = 100000f,
                    DragMathEpsilon = 0.0001f
                };
                // HOT SCALAR CONTROL KERNEL: one-player KCC truth is consumed this fixed tick.
                // Direct Execute removes IJob.Run scheduler sync without the fake schedule-then-complete anti-pattern.
                bodyJob.Execute();
            }
            finally
            {
                ReleasePublishedSdfPayloadLease(sdfLeaseVolume, ref sdfReadLease, ref sdfReadLeaseLocked);
            }
            if (rawBodyStateInvalid)
                AddFaultFlag(FaultNaN);

            float3 bulkheadResolvedPosition = _positions[0];
            float3 bulkheadResolvedVelocity = _velocities[0];
            if (TryApplyBulkheadCollisionResult(ref bulkheadResolvedPosition, ref bulkheadResolvedVelocity))
            {
                _positions[0] = bulkheadResolvedPosition;
                _velocities[0] = bulkheadResolvedVelocity;
            }

            float3 resolvedPosition3 = SnapMillimeter(_positions[0]);
            float3 resolvedVelocity3 = SnapMillimeter(_velocities[0]);
            _positions[0] = resolvedPosition3;
            _velocities[0] = resolvedVelocity3;
            _lastValidPositions[0] = resolvedPosition3;
            Vector3 resolvedPosition = ToVector3(resolvedPosition3);
            Vector3 resolvedVelocity = ToVector3(resolvedVelocity3);
            int faultFlags = ReadFaultFlags();
            StageStateWrite(resolvedPosition3, resolvedVelocity3, ResolveAuthoritativeRotationSnapshot(), (uint)faultFlags);
            _hasAuthoritativePoseSnapshot = true;
            PublishKccVelocitySignal(
                resolvedPosition3,
                resolvedVelocity3,
                0);
            if (SdfSqueezeResult.IsResultActive(in sdfSqueezeResult))
                PublishSdfSqueezeSignals(in sdfSqueezeResult, resolvedPosition3, resolvedVelocity3);
            if (faultFlags == 0)
                _dumpWrittenForFault = false;

            TickInertiaRoll(fixedDeltaTime);
            PublishMovementAcoustics(resolvedPosition, resolvedVelocity3);
            TickStamina();
            DumpFaultTelemetryIfNeeded();
        }

        private bool TryApplyBulkheadCollisionResult(ref float3 position, ref float3 velocity)
        {
            if (!TryResolveBulkheadCollisionResults(out NativeArray<BulkheadCollisionResultDTO> collisions))
            {
                return false;
            }

            BulkheadCollisionResultDTO result = collisions[0];
            uint currentFrame = SystemDispatcher.CurrentFrameId;
            if ((result.Flags & BulkheadCollisionFlags.Blocked) == 0u ||
                result.Frame == 0u ||
                currentFrame == 0u ||
                result.Frame > currentFrame ||
                currentFrame - result.Frame > 1u ||
                !math.isfinite(result.DepthMeters) ||
                result.DepthMeters <= 0.0001f)
            {
                return false;
            }

            float3 normal = SanitizeFloat3(result.Normal, new float3(0f, 1f, 0f));
            float lenSq = math.lengthsq(normal);
            if (lenSq <= 0.0001f || !math.all(math.isfinite(normal)))
                return false;

            normal *= math.rsqrt(lenSq);
            float depth = math.min(result.DepthMeters, 0.85f);
            position = SnapMillimeter(position + normal * depth);
            float inwardVelocity = math.dot(velocity, normal);
            if (inwardVelocity < 0f)
                velocity = SnapMillimeter(velocity - normal * inwardVelocity);

            return true;
        }

        private bool TryResolveBulkheadCollisionResults(out NativeArray<BulkheadCollisionResultDTO> collisions)
        {
            collisions = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (IsVaultHandleCreated(in _bulkheadCollisionResultsHandle) &&
                vault.TryResolveHandle(in _bulkheadCollisionResultsHandle, out collisions) &&
                collisions.IsCreated &&
                collisions.Length > 0)
            {
                return true;
            }

            uint frame = SystemDispatcher.CurrentFrameId;
            if (frame == 0u)
            {
                if (_nextBulkheadCollisionHandleBindFrame != 0u)
                    return false;
                _nextBulkheadCollisionHandleBindFrame = 16u;
            }
            else
            {
                if (frame < _nextBulkheadCollisionHandleBindFrame)
                    return false;
                _nextBulkheadCollisionHandleBindFrame = unchecked(frame + 16u);
            }

            return TryBindBulkheadCollisionResultHandle(vault) &&
                   vault.TryResolveHandle(in _bulkheadCollisionResultsHandle, out collisions) &&
                   collisions.IsCreated &&
                   collisions.Length > 0;
        }

        private bool TryBindBulkheadCollisionResultHandle(IDataVault dataVault)
        {
            if (dataVault == null)
            {
                _bulkheadCollisionResultsHandle = default;
                return false;
            }

            if (!dataVault.TryGetGenerationHandle(
                    BufferID.Shinobu220BulkheadCollisionResults,
                    out VaultGenerationHandle<BulkheadCollisionResultDTO> handle))
            {
                _bulkheadCollisionResultsHandle = default;
                return false;
            }

            _bulkheadCollisionResultsHandle = handle;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            ApplyPendingStateCorrections();
            CommitStateWrite();
            // Batchmode headless probes: HandFabrik IK is presentation-only and can touch
            // Animator/GPU paths that native-crash under BehaviourManager Update after WORLDDRIVER.
            if (Application.isBatchMode)
                return;
            ScheduleHandFabrikIk(fixedDeltaTime);
        }

        public void FastTick(float deltaTime)
        {
            // Batchmode: skip IK probes / environment-IK presentation. Keep sync fence so
            // hop/input fixed-step consumers still see a live kinematics owner.
            if (Application.isBatchMode)
            {
                _accumulatorState.FastTickCounter++;
                if (_accumulatorState.FastTickCounter < SyncFenceFrameInterval)
                    return;
                _accumulatorState.FastTickCounter = 0;
                PublishSyncFence();
                return;
            }

            float safeDeltaTime = math.max(0.0001f, SanitizeNonNegative(deltaTime));
            _lastIkDeltaTime = safeDeltaTime;
            ConsumeEnvironmentIkSignals();
            TickEnvironmentIkState(safeDeltaTime);
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
            // Batchmode: skip HandFabrik finalize/GPU upload and VAT/roll feedback presentation.
            if (Application.isBatchMode)
                return;

            TryFinalizeHandFabrikIkJob();
            UploadHandFabrikIkGpuBuffers();

            if (!MovementOwnsKinematicAuthority())
                PushVatScalar();
            PushRollSignal();
            FlushQueuedFeedbackSignals();
        }


        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            CaptureHandIkOriginShiftSnapshot(in shiftData);

            if (!HasMotionSoaStorage())
                return;

            float3 offset = (float3)(shiftData.ShiftOffset);
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

            if (_handProbeHits.IsCreated)
            {
                for (int i = 0; i < _handProbeHits.Length; i++)
                {
                    PlayerKinematicsProbeHit hit = _handProbeHits[i];
                    if ((hit.Flags & PlayerKinematicsProbeHit.FlagHit) == 0u)
                        continue;

                    float3 shiftedPoint = hit.Point - offset;
                    if (math.all(math.isfinite(shiftedPoint)))
                    {
                        hit.Point = shiftedPoint;
                    }
                    else
                    {
                        hit.Flags &= ~PlayerKinematicsProbeHit.FlagHit;
                    }

                    _handProbeHits[i] = hit;
                }
            }

            _lastProbeSourcePosition = SanitizeFloat3(_lastProbeSourcePosition - offset, float3.zero);
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _dataVault = currentService as IDataVault;
                DisposeNativeState();
                if (currentService != null)
                {
                    AllocateNativeState();
                    WarmRuntimeStateOnEnable();
                }
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterDispatcherTicks();
                if (currentService != null && isActiveAndEnabled)
                    RegisterDispatcherTicks();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _fluidGpuReadModel = currentService as IAbyssalFlowGpuReadModel;
                _analyticalFlowReadModel = currentService as IAnalyticalFlowReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.VoxelEngineRuntime)
            {
                _voxelEngine = currentService as HectonVoxelEngine;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                RefreshCameraTransformFromPlayerContext();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.PlayerMotor)
            {
                _motor = currentService as IPlayerKinematicsMotorSyncSink;
                if (_motor == null)
                    _motor = _localMotor;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.GasDynamicsRuntime)
            {
                _gasDynamics = currentService as IGasDynamicsSolver;
            }
        }

        internal static void EnsureOnPlayerRoot(GameObject playerRoot)
        {
            if (playerRoot == null)
                return;

            if (!playerRoot.TryGetComponent(out PlayerKinematicsRuntime _))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                playerRoot.AddComponent<PlayerKinematicsRuntime>(); // COLD ALLOC: PlayerKinematicsRuntime[1] - player kinematics bridge install - owner: PlayerRuntimeContextService
            }
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
            _ = _handProbeHits.Ensure(dataVault);
            _ = _sdfSqueezeResults.Ensure(dataVault);
            AllocateHandIkNativeState(dataVault);
            _ = TryOpenPlayerKinematicStateView(allowAllocate: true, readOnly: false, out _);
            TryBindBulkheadCollisionResultHandle(dataVault);
            if (!HasKinematicsStorage() || !HasSyncStateWriteStorage())
                return;

            float3 start = (float3)(ResolveBodyRuntimePosition());
            start = SnapMillimeter(SanitizeFloat3(start, float3.zero));
            _positions[0] = start;
            _lastValidPositions[0] = start;
            quaternion rotation = ResolveAuthoritativeRotationSnapshot();
            StageStateWrite(start, float3.zero, rotation, 0u);
            CommitStateWrite();
            _hasAuthoritativePoseSnapshot = true;
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
            _handProbeHits.ReleaseView();
            _sdfSqueezeResults.ReleaseView();
            ReleaseHandIkNativeState();
            _playerKinematicStateHandle = default;
            _bulkheadCollisionResultsHandle = default;
            _nextBulkheadCollisionHandleBindFrame = 0u;
            ResetDeterminismSessionState();
        }

        private void RegisterRuntime()
        {
            RegisterDispatcherTicks();

            if (!_registeredOriginShift)
            {
                HectonFloatingOrigin.RegisterListener(this);
                _registeredOriginShift = true;
            }

            if (!_registeredHotSwap)
            {
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
            }

        }

        private void RegisterDispatcherTicks()
        {
            if (!_registeredFixed)
            {
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            }

            if (!_registeredPostFixed)
            {
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
            }

            if (!_registeredFast)
            {
                _registeredFast = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Player);
            }

            if (!_registeredLate)
            {
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            }
        }

        private void UnregisterRuntime()
        {
            UnregisterDispatcherTicks();

            if (_registeredOriginShift)
            {
                HectonFloatingOrigin.UnregisterListener(this);
                _registeredOriginShift = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

        }

        private void UnregisterDispatcherTicks()
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
        }

        private void RebindServices(bool allowHierarchyLookup)
        {
            RebindRegistryServices();
            if (_motor == null)
                _motor = _localMotor;
            if (_hydrodynamicKccRuntime == null)
                _hydrodynamicKccRuntime = _localHydrodynamicKccRuntime;
            if (_inventory == null)
                _inventory = _localInventory;
            if (_survival == null)
                _survival = _localSurvival;

            if (!allowHierarchyLookup)
                return;

            CacheLocalComponentsCold();
            if (_motor == null)
                _motor = _localMotor;
            if (_hydrodynamicKccRuntime == null)
                _hydrodynamicKccRuntime = _localHydrodynamicKccRuntime;
            if (_inventory == null)
                _inventory = _localInventory;
            if (_survival == null)
                _survival = _localSurvival;
        }

        private void CacheLocalComponentsCold()
        {
            TryGetComponent(out _body);
            TryGetComponent<IPlayerKinematicsMovementRuntime>(out _movement);
            TryGetComponent(out _localHydrodynamicKccRuntime);
            TryGetComponent(out _localInventory);
            TryGetComponent(out _localSurvival);
            TryGetComponent<IPlayerKinematicsMotorSyncSink>(out _localMotor);

            if (_motor == null)
                _motor = _localMotor;
            if (_hydrodynamicKccRuntime == null)
                _hydrodynamicKccRuntime = _localHydrodynamicKccRuntime;
            if (_inventory == null)
                _inventory = _localInventory;
            if (_survival == null)
                _survival = _localSurvival;
        }

        private void WarmRuntimeStateOnEnable()
        {
            if (!HasCoreEntityStorage())
                return;

            Vector3 runtimePosition = ResolveBodyRuntimePosition();
            float3 position = SanitizeFloat3((float3)(runtimePosition), ReadLastValidPosition());
            if (!math.all(math.isfinite(position)))
                return;
            position = SnapMillimeter(position);

            float3 velocity = ReadVelocitySnapshot(float3.zero);
            velocity = SnapMillimeter(SanitizeFloat3(velocity, float3.zero));
            _positions[0] = position;
            _velocities[0] = velocity;
            _lastValidPositions[0] = position;
            StageStateWrite(position, velocity, ResolveAuthoritativeRotationSnapshot(), 0u);
            CommitStateWrite();
            _hasAuthoritativePoseSnapshot = true;

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
            _hasAuthoritativePoseSnapshot = false;
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
            _lastProbeReduced = false;
            _lastProbeCount = 0;
            _wasBraceActive = false;
            ResetHandIkSessionState();
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
            CacheDataVaultCold();
            CacheRegistryServicesCold();
            RefreshCameraTransformFromPlayerContext();
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01(_cachedGlobalQualityWeight01);
        }

        private void CacheRegistryServicesCold()
        {
            if (_registeredHotSwap)
                return;

            _gasDynamics = GlobalRegistry.GasDynamics;
            _fluidGpuReadModel = GlobalRegistry.AbyssalFlowGpu;
            _analyticalFlowReadModel = GlobalRegistry.AnalyticalFlow;
            _voxelEngine = GlobalRegistry.VoxelEngine;
            if (_motor == null && GlobalRegistry.PlayerMotor != null)
                _motor = GlobalRegistry.PlayerMotor;
            _playerRuntimeContext = GlobalRegistry.Player;
        }

        private void RefreshCameraTransformFromPlayerContext()
        {
            Camera playerCamera = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerCamera : null;
            _cameraTransform = playerCamera != null ? playerCamera.transform : null;
        }

        private void CacheDataVaultCold()
        {
            if (_registeredHotSwap)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (ReferenceEquals(_dataVault, vault))
                return;

            if (_dataVault != null)
            {
                DisposeNativeState();
            }

            _dataVault = vault;
        }

        private void RebindColdIfMissing()
        {
            if (_gasDynamics != null &&
                _fluidGpuReadModel != null &&
                _analyticalFlowReadModel != null &&
                _voxelEngine != null &&
                _motor != null &&
                _playerRuntimeContext != null &&
                _cameraTransform != null)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextColdRebindFrame)
                return;

            _nextColdRebindFrame = frame + 64;
            RefreshMissingRegistryServicesCold();
        }

        private void RefreshMissingRegistryServicesCold()
        {
            if (_gasDynamics == null)
                _gasDynamics = GlobalRegistry.GasDynamics;
            if (_fluidGpuReadModel == null)
                _fluidGpuReadModel = GlobalRegistry.AbyssalFlowGpu;
            if (_analyticalFlowReadModel == null)
                _analyticalFlowReadModel = GlobalRegistry.AnalyticalFlow;
            if (_voxelEngine == null)
                _voxelEngine = GlobalRegistry.VoxelEngine;
            if (_motor == null)
            {
                _motor = GlobalRegistry.PlayerMotor;
                if (_motor == null)
                    _motor = _localMotor;
            }
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            RefreshCameraTransformFromPlayerContext();
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

            float2 planar = new float2(
                inputSignal.State.MoveX * InputState.AxisInvQuantizeScale,
                inputSignal.State.MoveY * InputState.AxisInvQuantizeScale);
            planar = math.select(planar, float2.zero, !math.all(math.isfinite(planar)));
            float planarSq = math.lengthsq(planar);
            if (!math.isfinite(planarSq))
                planar = float2.zero;
            else if (planarSq > 1.0f)
                planar *= math.rsqrt(math.max(planarSq, 0.000001f));

            float3 forward = _cameraTransform != null ? (float3)(_cameraTransform.forward) : (float3)(_cachedTransform.forward);
            float3 right = _cameraTransform != null ? (float3)(_cameraTransform.right) : (float3)(_cachedTransform.right);
            forward.y = 0.0f;
            right.y = 0.0f;
            forward = SafeNormalize(forward, new float3(0.0f, 0.0f, 1.0f));
            right = SafeNormalize(right, new float3(1.0f, 0.0f, 0.0f));
            float vertical = SanitizeSignedUnit(inputSignal.State.Vertical * InputState.AxisInvQuantizeScale);
            float3 intended = (right * planar.x) + (forward * planar.y) + new float3(0.0f, vertical, 0.0f);
            _intendedMovement[0] = SanitizeFloat3(intended, float3.zero);
        }

        private void SnapshotGpuFlow()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_accumulatorState.LastGpuFlowFrame != 0u && (frame & ResolveGpuFlowProbeFrameMask()) != 0)
                return;

            IAbyssalFlowGpuReadModel fluid = _fluidGpuReadModel;
            if (fluid == null ||
                !fluid.TryGetGpuAbyssalFlowFieldBuffer(
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

            Vector3 position = ResolveBodyRuntimePosition();
            float3 positionFloat = (float3)(position);
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
            out NativeArray<byte>.ReadOnly sdfTexture3D,
            out int3 gridDimensions,
            out float3 volumeOrigin,
            out float3 voxelCellSize,
            out float sdfRange,
            out byte sampleMode,
            out HectonVoxelVolume leaseVolume,
            out HectonVoxelVolume.PublishedSonarSdfReadLease lease,
            out bool leaseLocked)
        {
            sdfTexture3D = default;
            gridDimensions = default;
            volumeOrigin = float3.zero;
            voxelCellSize = float3.zero;
            sdfRange = 0.0f;
            sampleMode = ResolveSdfSampleMode(ReadCachedGlobalQualityWeight01(), Hecton8.Core.SystemDispatcher.CurrentFrameId);
            leaseVolume = null;
            lease = default;
            leaseLocked = false;

            if (_voxelEngine == null)
                return;

            if (!_voxelEngine.TryGetNearestActiveVolume(targetRuntimePosition, out HectonVoxelVolume volume) ||
                volume == null ||
                !volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly publishedSdf,
                    out Vector3Int publishedDimensions,
                    out Vector3 publishedOrigin,
                    out Vector3 publishedCellSize,
                    out float publishedRange,
                    out int _,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease publishedLease))
            {
                return;
            }

            bool accepted = false;
            try
            {
                int3 resolvedDimensions = new int3(publishedDimensions.x, publishedDimensions.y, publishedDimensions.z);
                if (!PlayerKinematicsBodyJob.TryResolveSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                    publishedSdf.Length < expectedLength)
                {
                    return;
                }

                NativeArray<byte>.ReadOnly resolvedSdf = publishedSdf;

                sdfTexture3D = resolvedSdf;
                gridDimensions = resolvedDimensions;
                float3 safeOrigin = (float3)(publishedOrigin);
                float3 safeCellSize = (float3)(publishedCellSize);
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
                leaseVolume = volume;
                lease = publishedLease;
                leaseLocked = true;
                accepted = true;
            }
            finally
            {
                if (!accepted)
                    volume.ReleasePublishedSonarSdfPayloadReadLease(in publishedLease);
            }
        }

        private static void ReleasePublishedSdfPayloadLease(
            HectonVoxelVolume leaseVolume,
            ref HectonVoxelVolume.PublishedSonarSdfReadLease lease,
            ref bool leaseLocked)
        {
            if (!leaseLocked)
                return;

            if (leaseVolume != null)
                leaseVolume.ReleasePublishedSonarSdfPayloadReadLease(in lease);

            lease = default;
            leaseLocked = false;
        }

        private bool TryApplySdfSqueeze(
            float fixedDeltaTime,
            float qualityWeight01,
            NativeArray<byte>.ReadOnly sdfTexture3D,
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
                       TryApplyCachedSdfSqueeze(safeDeltaTime, sdfSampleMode, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition, out result);
            }

            bool runSampleNow = !slowCadence ||
                                _sdfSqueezeSlowHoldFrames <= 0 ||
                                ((Hecton8.Core.SystemDispatcher.CurrentFrameIndex + _cadenceSalt) % SdfSqueezeSlowCadenceFrameInterval) == 0;
            if (!runSampleNow &&
                TryApplyCachedSdfSqueeze(safeDeltaTime, sdfSampleMode, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition, out result))
            {
                return true;
            }

            if (!TryConvertRuntimePositionToAup(safeBodyPosition, out AbsoluteUniversePosition bodyAup))
                return false;

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
                TargetAupAbsolute = ToAbsoluteDouble3(in sampleAup),
                FloatingOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble,
                VoxelSdfRange = sdfRange,
                SdfSampleStepMeters = ResolveSdfSampleStepMeters(sdfCellSize),
                DeltaTime = safeDeltaTime,
                MaxPushOutSpeedMetersPerSecond = SdfSqueezeMaxPushOutSpeedMetersPerSecond,
                SpeedPenalty01 = SdfSqueezeForwardSpeedPenalty01,
                SystemStress01 = systemStress01,
                QualityWeight = qualityWeight01,
                SampleMode = IsReducedSdfSampleMode(sdfSampleMode) ? (byte)SdfSqueezeSampleMode.Tetra4 : (byte)SdfSqueezeSampleMode.Axis6,
                SlowCadence = slowCadence ? (byte)1 : (byte)0,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            };
            // HOT SCALAR CONTROL KERNEL: squeeze intervention is same-tick KCC safety truth.
            // Direct Execute removes IJob.Run scheduler sync; async staging belongs to the KCC owner rewrite.
            squeezeJob.Execute();
            result = _sdfSqueezeResults[0];

            if ((result.Flags & SdfSqueezeResult.FlagNaNFallback) != 0u)
                WriteSdfSqueezeTelemetry(in result, bodyPosition, bodyVelocity);

            if (!SdfSqueezeResult.IsResultActive(in result))
                return false;

            _lastSdfSqueezeResult = result;
            _sdfSqueezeSlowHoldFrames = slowCadence ? SdfSqueezeSlowCadenceFrameInterval - 1 : 0;
            ApplySdfSqueezeResultToRuntime(in result, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition);
            return true;
        }

        private bool TryApplyCachedSdfSqueeze(
            float fixedDeltaTime,
            byte sdfSampleMode,
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
            result.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            result.Flags |= SdfSqueezeResult.FlagSlowCadence;
            if (IsReducedSdfSampleMode(sdfSampleMode))
                result.Flags |= SdfSqueezeResult.FlagReducedGradientSamples;

            ApplySdfSqueezeResultToRuntime(in result, ref inSolid, ref sdfGradientProbeRequested, ref bodyPosition, ref bodyVelocity, ref safeBodyPosition);
            return true;
        }

        private void ApplySdfSqueezeResultToRuntime(
            in SdfSqueezeResult result,
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
            if ((result.Flags & SdfSqueezeResult.FlagReducedGradientSamples) != 0u)
                _lastSdfSqueezeResult.Flags |= SdfSqueezeResult.FlagReducedGradientSamples;
        }

        private static bool IsValidSdfPayload(
            NativeArray<byte>.ReadOnly sdfTexture3D,
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
            if (!TryOpenPlayerKinematicStateView(allowAllocate: false, readOnly: true, out NativeArray<LockstepPlayerKinematicState> stateBuffer))
                return false;

            LockstepPlayerKinematicState state = stateBuffer[0];
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
            if (!TryOpenPlayerKinematicStateView(allowAllocate: true, readOnly: false, out NativeArray<LockstepPlayerKinematicState> stateBuffer))
                return;

            stateBuffer[0] = new LockstepPlayerKinematicState
            {
                SectorX = aup.GridX,
                SectorY = aup.GridY,
                SectorZ = aup.GridZ,
                LocalPosition = new float3(aup.LocalX, aup.LocalY, aup.LocalZ),
                Velocity = SanitizeFloat3(velocity, float3.zero),
                Forward = _cachedTransform != null
                    ? SafeNormalize((float3)(_cachedTransform.forward), new float3(0.0f, 0.0f, 1.0f))
                    : new float3(0.0f, 0.0f, 1.0f),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = BodyFlagSdfSqueezeIntervention,
                StableId = _sourceId
            };
        }

        private bool TryOpenPlayerKinematicStateView(
            bool allowAllocate,
            bool readOnly,
            out NativeArray<LockstepPlayerKinematicState> stateBuffer)
        {
            stateBuffer = default;
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
                return false;

            if (TryOpenVaultView(
                    dataVault,
                    in _playerKinematicStateHandle,
                    BufferID.PlayerKinematicState,
                    SystemID.Unknown,
                    EntityCount,
                    readOnly,
                    out stateBuffer))
            {
                return true;
            }

            if (!allowAllocate)
            {
                return TryReadExistingVaultView(
                    dataVault,
                    BufferID.PlayerKinematicState,
                    SystemID.Unknown,
                    EntityCount,
                    out stateBuffer);
            }

            if (!dataVault.TryGetGenerationHandle(
                    BufferID.PlayerKinematicState,
                    out _playerKinematicStateHandle) ||
                !TryOpenVaultView(
                    dataVault,
                    in _playerKinematicStateHandle,
                    BufferID.PlayerKinematicState,
                    SystemID.Unknown,
                    EntityCount,
                    readOnly,
                    out stateBuffer))
            {
                _playerKinematicStateHandle = dataVault.EnsureGenerationHandle<LockstepPlayerKinematicState>(
                    BufferID.PlayerKinematicState,
                    EntityCount,
                    OwnerSystemId,
                    NativeArrayOptions.ClearMemory);
            }

            return TryOpenVaultView(
                dataVault,
                in _playerKinematicStateHandle,
                BufferID.PlayerKinematicState,
                SystemID.Unknown,
                EntityCount,
                readOnly,
                out stateBuffer);
        }

        private static bool TryReadExistingVaultView<T>(
            IDataVault dataVault,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength < 0 ||
                !dataVault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            return TryOpenVaultView(
                dataVault,
                in handle,
                bufferId,
                expectedOwner,
                requiredLength,
                readOnly: true,
                out buffer);
        }

        private static bool TryOpenVaultView<T>(
            IDataVault dataVault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID expectedOwner,
            int requiredLength,
            bool readOnly,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength < 0 ||
                !IsExpectedVaultDescriptor(in handle, bufferId, expectedOwner))
            {
                return false;
            }

            bool opened = readOnly
                ? dataVault.TryReadHandle(in handle, out buffer)
                : dataVault.TryResolveHandle(in handle, out buffer);

            return opened &&
                   buffer.IsCreated &&
                   (requiredLength == 0 || buffer.Length >= requiredLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsExpectedVaultDescriptor<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID expectedOwner)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   (expectedOwner == SystemID.Unknown || handle.SystemID == (uint)expectedOwner);
        }

        private static byte ResolveSdfGradientProbeRequest()
        {
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
                !volume.TryAcquirePublishedSonarSdfPayloadReadLease(
                    out NativeArray<byte>.ReadOnly _,
                    out Vector3Int gridDimensions,
                    out Vector3 volumeOrigin,
                    out Vector3 voxelCellSize,
                    out float _,
                    out int _,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease lease))
            {
                return false;
            }

            try
            {
                if (gridDimensions.x <= 1 ||
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
            finally
            {
                volume.ReleasePublishedSonarSdfPayloadReadLease(in lease);
            }
        }

        private void SnapshotLadder(out byte ladderActive, out float3 ladderPoint)
        {
            ladderActive = 0;
            ladderPoint = ReadPositionSnapshot(float3.zero);
            if (_motor == null ||
                !_motor.TryGetRecentLadderContact(MaxLadderFrameAge, out Vector3 contactPoint))
            {
                return;
            }

            float3 safePoint = (float3)(contactPoint);
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

            IAbyssalFlowGpuReadModel fluid = _fluidGpuReadModel;
            if (fluid == null || !fluid.TrySampleModAbyssalFlow(position, out float3 flow))
                return float3.zero;

            if (!math.all(math.isfinite(flow)))
                return float3.zero;

            float gpuBoost = _accumulatorState.LastGpuFlowFrame != 0u ? 1.0f : 0.65f;
            float qualityScale = math.lerp(0.75f, 1.0f, SmoothQuality01(ReadCachedGlobalQualityWeight01()));
            float3 advection = flow * (AdvectionVelocityScale * gpuBoost * qualityScale * immersion01);
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
                float sideDot = math.dot((float3)(normal), SafeRight());
                float side = math.sign(math.select(sideDot, 0.0f, !math.isfinite(sideDot)));
                _rollPhaseRadians = DeterministicContractMath.WrapSignedPi(_rollPhaseRadians + SanitizeNonNegative(dt) * 28.0f);
                rollPhaseAdvanced = true;
                float qualityCurve01 = SmoothQuality01(ReadCachedGlobalQualityWeight01());
                float impactWave = math.lerp(SignedTriangleWave(_rollPhaseRadians), DeterministicContractMath.SinApprox(_rollPhaseRadians), qualityCurve01);
                targetRoll = -side *
                    SanitizeNonNegative(WallImpactRollDegrees) *
                    speed01 *
                    SanitizeUnit(velocityReduction01 + 0.25f) *
                    impactWave;
            }

            float safeDt = SanitizeNonNegative(dt);
            float squeezeStress = SanitizeUnit(_lastSdfSqueezeStress01);
            float squeezeRollWeight01 = SmoothQuality01(math.saturate((ReadCachedGlobalQualityWeight01() - 0.45f) * 1.8181819f));
            if (squeezeStress > 0.0001f && squeezeRollWeight01 > 0.0001f && IsFiniteNonZero(_lastSdfSqueezeNormal))
            {
                if (!rollPhaseAdvanced)
                    _rollPhaseRadians = DeterministicContractMath.WrapSignedPi(_rollPhaseRadians + safeDt * 16.0f);

                float sideDot = math.dot(_lastSdfSqueezeNormal, SafeRight());
                float side = math.sign(math.select(sideDot, 0.0f, !math.isfinite(sideDot)));
                float twistWave = math.lerp(
                    SignedTriangleWave(_rollPhaseRadians),
                    0.65f + 0.35f * DeterministicContractMath.SinApprox(_rollPhaseRadians),
                    squeezeRollWeight01);
                float squeezeRoll = -side * SdfSqueezeRollDegrees * squeezeStress * squeezeRollWeight01 * twistWave;
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
                (float3)(position),
                ReadPositionSnapshot(float3.zero));
            if (!TryConvertRuntimePositionToAup(SnapMillimeter(safePosition), out AbsoluteUniversePosition positionAup))
                return;

            MovementAcousticSignal signal = default;
            signal.PositionAup = positionAup;
            signal.Volume = SanitizeUnit(velocitySq * 0.08f);
            signal.VelocitySq = velocitySq;
            signal.SourceId = _sourceId;
            signal.LocomotionMode = ResolveLocomotionModeCode();
            signal.SurfaceMode = (byte)(_movement != null && _movement.IsPlayerSubmerged ? 1 : 0);
            signal.Flags = 0;
            QueueMovementAcoustic(in signal);
        }

        private void PublishKccVelocitySignal(float3 position, float3 velocity, byte flags)
        {
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                return;

            float3 snappedPosition = SnapMillimeter(position);
            float3 snappedVelocity = SnapMillimeter(velocity);
            if (!TryConvertRuntimePositionToAup(snappedPosition, out AbsoluteUniversePosition bodyAup))
                return;

            KccVelocitySignal signal = default;
            signal.BodyAup = bodyAup;
            signal.Velocity = snappedVelocity;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceId = _sourceId;
            signal.Flags = flags;
            signal.QualityPressureQ8 = ResolveQualityPressureQ8(ReadCachedGlobalQualityWeight01());
            CoreDeterminismSignals.TryPublishKccVelocity(in signal);
        }

        private void PublishSdfSqueezeSignals(in SdfSqueezeResult result, float3 position, float3 velocity)
        {
            float stress01 = SanitizeUnit(result.Stress01);
            float pushSpeed = SanitizeNonNegative(result.PushSpeed);
            if (stress01 <= 0.0001f && pushSpeed <= 0.0001f)
                return;

            if (!TryConvertRuntimePositionToAup(SnapMillimeter(position), out AbsoluteUniversePosition positionAup))
                return;

            byte stateFlags = (byte)(PlayerStateSignal.FlagActive |
                                     PlayerStateSignal.FlagSqueezing |
                                     PlayerStateSignal.FlagSdfGradientValid |
                                     PlayerStateSignal.FlagAupShiftSafe);
            if ((result.Flags & SdfSqueezeResult.FlagReducedGradientSamples) != 0u)
                stateFlags |= PlayerStateReducedGradientCompatibilityFlag;

            PlayerStateSignal playerState = default;
            playerState.PositionAup = positionAup;
            playerState.Intensity01 = stress01;
            playerState.SourceHash = _sourceId;
            playerState.Frame = result.Frame != 0u ? result.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            playerState.State = PlayerStateSignal.StateSqueezing;
            playerState.Flags = stateFlags;
            SignalBus<PlayerStateSignal>.TryPushTracked(in playerState, ref _signalPushDropCount);
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
            PlayerStressSignal stress = default;
            stress.Stress01 = stress01;
            stress.OxygenDrainScale = oxygenDrainScale;
            stress.AggressionScale = 1.0f;
            stress.Frame = frame;
            stress.Cause = PlayerStateSignal.StateSqueezing;
            stress.Flags = PlayerStateSignal.FlagSqueezing;
            SignalBus<PlayerStressSignal>.TryPushTracked(in stress, ref _signalPushDropCount);
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
            QueueSdfSqueezeHaptic(in haptic);

            AcousticPingSignal acoustic = default;
            acoustic.PositionAup = positionAup;
            acoustic.RadiusMeters = 0.75f + intensity * 1.35f;
            acoustic.Intensity01 = intensity;
            acoustic.SourceId = _sourceId;
            acoustic.Channel = AcousticPingSignal.ChannelFabricScrape;
            acoustic.Flags = AcousticPingSignal.FlagFabricScrape;
            QueueSdfSqueezeAcoustic(in acoustic);

            _sdfSqueezeFeedbackCooldown = SdfSqueezeFeedbackCooldownSeconds;
        }

        private void QueueMovementAcoustic(in MovementAcousticSignal signal)
        {
            if (_pendingMovementAcousticDirty && _pendingMovementAcoustic.Volume > signal.Volume)
                return;

            _pendingMovementAcoustic = signal;
            _pendingMovementAcousticDirty = true;
        }

        private void QueueSdfSqueezeHaptic(in HapticRequest signal)
        {
            if (_pendingSdfSqueezeHapticDirty && _pendingSdfSqueezeHaptic.Intensity01 > signal.Intensity01)
                return;

            _pendingSdfSqueezeHaptic = signal;
            _pendingSdfSqueezeHapticDirty = true;
        }

        private void QueueSdfSqueezeAcoustic(in AcousticPingSignal signal)
        {
            if (_pendingSdfSqueezeAcousticDirty && _pendingSdfSqueezeAcoustic.Intensity01 > signal.Intensity01)
                return;

            _pendingSdfSqueezeAcoustic = signal;
            _pendingSdfSqueezeAcousticDirty = true;
        }

        private void QueueBraceHaptic(in HapticRequest signal)
        {
            if (_pendingBraceHapticDirty && _pendingBraceHaptic.Intensity01 > signal.Intensity01)
                return;

            _pendingBraceHaptic = signal;
            _pendingBraceHapticDirty = true;
        }

        private void QueueGloveScrapeAcoustic(in AcousticPingSignal signal)
        {
            if (_pendingGloveScrapeAcousticDirty && _pendingGloveScrapeAcoustic.Intensity01 > signal.Intensity01)
                return;

            _pendingGloveScrapeAcoustic = signal;
            _pendingGloveScrapeAcousticDirty = true;
        }

        private void FlushQueuedFeedbackSignals()
        {
            if (_pendingMovementAcousticDirty)
            {
                _pendingMovementAcousticDirty = false;
                SignalBus<MovementAcousticSignal>.TryPushTracked(in _pendingMovementAcoustic, ref _signalPushDropCount);
            }

            if (_pendingSdfSqueezeHapticDirty)
            {
                _pendingSdfSqueezeHapticDirty = false;
                SignalBus<HapticRequest>.TryPushTracked(in _pendingSdfSqueezeHaptic, ref _signalPushDropCount);
            }

            if (_pendingSdfSqueezeAcousticDirty)
            {
                _pendingSdfSqueezeAcousticDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingSdfSqueezeAcoustic, ref _signalPushDropCount);
            }

            if (_pendingBraceHapticDirty)
            {
                _pendingBraceHapticDirty = false;
                SignalBus<HapticRequest>.TryPushTracked(in _pendingBraceHaptic, ref _signalPushDropCount);
            }

            if (_pendingGloveScrapeAcousticDirty)
            {
                _pendingGloveScrapeAcousticDirty = false;
                SignalBus<AcousticPingSignal>.TryPushTracked(in _pendingGloveScrapeAcoustic, ref _signalPushDropCount);
            }
        }

        private void ClearQueuedFeedbackSignals()
        {
            _pendingMovementAcousticDirty = false;
            _pendingSdfSqueezeHapticDirty = false;
            _pendingSdfSqueezeAcousticDirty = false;
            _pendingBraceHapticDirty = false;
            _pendingGloveScrapeAcousticDirty = false;
        }

        private void TryPublishSdfSqueezeVisualImpulse(
            in AbsoluteUniversePosition positionAup,
            float3 normal,
            float3 velocity,
            float stress01,
            uint frame)
        {
            float intensity = SanitizeUnit(stress01);
            float visualWeight01 = SmoothQuality01(math.saturate((ReadCachedGlobalQualityWeight01() - 0.45f) * 1.8181819f));
            if (visualWeight01 <= 0.0001f || intensity < SdfSqueezeVisualImpulseMinStress01)
                return;

            float3 safeNormal = SafeNormalize(normal, float3.zero);
            float3 safeVelocity = SanitizeFloat3(velocity, float3.zero);
            float3 vector = (safeNormal * (0.65f + intensity * 1.15f) +
                             safeVelocity * SdfSqueezeVisualImpulseVelocityScale) * visualWeight01;
            float vectorSq = math.lengthsq(vector);
            if (!math.isfinite(vectorSq) || vectorSq <= 0.000001f)
                return;

            FluidImpulseSignal impulse = default;
            impulse.PositionAup = positionAup;
            impulse.Vector = vector;
            impulse.Radius = SdfSqueezeVisualImpulseBaseRadiusMeters +
                             intensity * visualWeight01 * SdfSqueezeVisualImpulseExtraRadiusMeters;
            impulse.Lifetime = SdfSqueezeVisualImpulseBaseLifetimeSeconds +
                               intensity * visualWeight01 * SdfSqueezeVisualImpulseExtraLifetimeSeconds;
            impulse.Frame = frame;
            impulse.SourceHash = _sourceId;
            impulse.Flags = (uint)(PlayerStateSignal.FlagSqueezing | PlayerStateSignal.FlagSdfGradientValid);
            SignalBus<FluidImpulseSignal>.TryPushTracked(in impulse, ref _signalPushDropCount);
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
            if ((result.Flags & SdfSqueezeResult.FlagReducedGradientSamples) != 0u)
                auxFlags |= BodyFlagSdfReducedGradientSamples;
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
                Frame = result.Frame != 0u ? result.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId,
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

            float3 position = SnapMillimeter(SanitizeFloat3((float3)(ResolveBodyRuntimePosition()), ReadLastValidPosition()));
            float3 velocity = SnapMillimeter(ReadVelocitySnapshot(float3.zero));
            if (HasMotionSoaStorage())
            {
                _positions[0] = position;
                _velocities[0] = velocity;
                _lastValidPositions[0] = position;
                _hasAuthoritativePoseSnapshot = true;
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
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Flags = 0u,
                SyncFenceHash = _accumulatorState.LastSyncFenceHash,
                AuxFlags = AupPreShiftHaltTelemetryFlag | (_accumulatorState.LastConsumedPreShiftFrameId & 0xFFFFu),
                AupMaxDriftErrorMeters = 0.0f
            };
        }

        private void ConsumeEnvironmentIkSignals()
        {
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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

                if (!TryConvertAupToRuntimePosition(in signal.PointAup, out float3 point))
                    continue;

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
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
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
            float3 externalVelocity = ReadVelocitySnapshot(float3.zero);
            TryPublishSdfSqueezeVisualImpulse(in signal.PositionAup, float3.zero, externalVelocity, stress01, signal.Frame);
            WriteSqueezeTelemetry(in signal);
        }

        private void WriteSqueezeTelemetry(in PlayerStateSignal signal)
        {
            if (!TryReserveTelemetrySlot(out int wrappedIndex))
                return;

            Vector3 runtimePosition = ResolveBodyRuntimePosition();
            Vector3 runtimeVelocity = ToVector3(ReadVelocitySnapshot(float3.zero));
            uint clampedInterventions = _accumulatorState.SqueezeInterventions > 65535u
                ? 65535u
                : _accumulatorState.SqueezeInterventions;
            uint auxFlags = BodyFlagSdfSqueezeIntervention |
                BodyFlagSdfGradientValid |
                ((clampedInterventions << TelemetrySqueezeInterventionShift) & TelemetrySqueezeInterventionMask);
            if ((signal.Flags & PlayerStateReducedGradientCompatibilityFlag) != 0)
                auxFlags |= BodyFlagSdfReducedGradientSamples;

            _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
            {
                Position = SanitizeFloat3((float3)(runtimePosition), float3.zero),
                Velocity = SanitizeFloat3((float3)(runtimeVelocity), float3.zero),
                IntendedMovement = ReadIntendedMovementSnapshot(),
                DragCoefficient = SanitizeNonNegative(dragCoefficient),
                WaterDensity = ResolveRuntimeWaterDensityScale(),
                SolidDensity = SanitizeUnit(signal.Intensity01),
                Frame = signal.Frame != 0u ? signal.Frame : Hecton8.Core.SystemDispatcher.CurrentFrameId,
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
            return _movement != null ? _movement.CurrentLocomotionModeCode : (byte)0;
        }

        private void ScheduleHandProbes()
        {
            ClearHandTargets();
            ResolveHandPlacementDirect();
            ApplyHandTargets();
        }

        private void ResolveHandPlacementDirect()
        {
            int requiredProbeCount = math.clamp(_lastProbeCount, 1, EnvironmentProbeCount);
            if (!HasHandTargetWriteStorage() ||
                !HasHandProbeHitStorage(requiredProbeCount))
            {
                return;
            }

            var placementSolver = new PlayerKinematicsHandPlacementSolver
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
                ProbeCount = (byte)requiredProbeCount,
                RuntimeFlags = (byte)(
                    (_lastProbeReduced ? PlayerKinematicsHandPlacementSolver.RuntimeFlagReducedProbeSet : 0) |
                    (_hasImpactBracePoint ? PlayerKinematicsHandPlacementSolver.RuntimeFlagImpact : 0))
            };
            placementSolver.Execute();
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
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Channel = HapticRequest.ChannelLightThud;
            signal.Flags = HapticRequest.FlagLightThud;
            QueueBraceHaptic(in signal);
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

            float3 pointFloat = (float3)(point);
            if (!math.all(math.isfinite(pointFloat)))
                return false;

            float safeBlockedSpeed = SanitizeNonNegative(blockedSpeed);
            float safeVelocityReduction01 = SanitizeUnit(velocityReduction01);
            if (safeBlockedSpeed <= 0.2f || safeVelocityReduction01 <= 0.05f)
                return false;

            if (!TryConvertRuntimePositionToAup(point, out AbsoluteUniversePosition pointAup))
                return false;

            AcousticPingSignal signal = default;
            signal.PositionAup = pointAup;
            signal.RadiusMeters = SanitizeUnit(0.65f + safeBlockedSpeed * 0.12f) * 2.0f;
            signal.Intensity01 = SanitizeUnit(SanitizeUnit(blend) * (0.35f + safeVelocityReduction01) + safeBlockedSpeed * 0.025f);
            signal.SourceId = _sourceId;
            signal.Channel = AcousticPingSignal.ChannelGloveScrape;
            signal.Flags = AcousticPingSignal.FlagGloveScrape;
            QueueGloveScrapeAcoustic(in signal);
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
            if (_lastProbeReduced)
                auxFlags |= IkReducedProbeTelemetryFlag;
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
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
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
            return _movement != null && _movement.IsKinematicMovementActive;
        }

        private bool HydrodynamicKccOwnsAuthority()
        {
            HydrodynamicKccRuntime runtime = _hydrodynamicKccRuntime;
            return runtime != null && runtime.IsAuthorityRouteActive;
        }

        private void ConsumeHydrodynamicKccAuthoritySnapshot(float qualityWeight01)
        {
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
                signal.SourceId != HydrodynamicKccMath.SourceHash ||
                signal.Sequence == 0u)
            {
                return;
            }

            double3 runtimePosition64 = signal.BodyAup.ToAbsoluteDouble3() - HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(runtimePosition64)) || !math.all(math.isfinite(signal.Velocity)))
            {
                AddFaultFlag(FaultNaN);
                return;
            }

            float3 position = SnapMillimeter(new float3((float)runtimePosition64.x, (float)runtimePosition64.y, (float)runtimePosition64.z));
            float3 velocity = SnapMillimeter(SanitizeFloat3(signal.Velocity, float3.zero));
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
            {
                AddFaultFlag(FaultNaN);
                return;
            }

            if (HasMotionSoaStorage())
            {
                _positions[0] = position;
                _velocities[0] = velocity;
                _lastValidPositions[0] = position;
            }

            quaternion rotation = ResolveAuthoritativeRotationSnapshot();
            uint hash = BuildSyncFenceHash(in signal.BodyAup, velocity, rotation);
            if (HasSyncStateReadStorage())
            {
                _stateRead[0] = new PlayerKinematicsSyncState
                {
                    Position = position,
                    Velocity = velocity,
                    Rotation = rotation,
                    Frame = signal.Frame,
                    Flags = HydrodynamicAuthorityTelemetryFlag,
                    StateHash = hash
                };
            }
            _hasAuthoritativePoseSnapshot = true;

            _accumulatorState.LastSyncFenceHash = hash;
            _accumulatorState.LastSyncFenceFrame = signal.Frame;
            if (TryReserveTelemetrySlot(out int wrappedIndex))
            {
                _telemetry[wrappedIndex] = new PlayerKinematicsRuntimeTelemetryEntry
                {
                    Position = position,
                    Velocity = velocity,
                    IntendedMovement = ReadIntendedMovementSnapshot(),
                    DragCoefficient = SanitizeNonNegative(dragCoefficient),
                    WaterDensity = ResolveRuntimeWaterDensityScale(),
                    SolidDensity = 0.0f,
                    Frame = signal.Frame,
                    Flags = 0u,
                    SyncFenceHash = hash,
                    AuxFlags = HydrodynamicAuthorityTelemetryFlag | ((uint)signal.Flags << 16),
                    AupMaxDriftErrorMeters = SanitizeNonNegative(1f - math.saturate(qualityWeight01))
                };
            }
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
        private bool TryReadAuthoritativePositionSnapshot(out float3 position)
        {
            if (!_hasAuthoritativePoseSnapshot)
            {
                position = float3.zero;
                return false;
            }

            if (HasSyncStateReadStorage())
            {
                position = SanitizeFloat3(_stateRead[0].Position, float3.zero);
                if (math.all(math.isfinite(position)))
                    return true;
            }

            if (_positions.IsCreated && _positions.Length >= EntityCount)
            {
                position = SanitizeFloat3(_positions[0], float3.zero);
                if (math.all(math.isfinite(position)))
                    return true;
            }

            if (_lastValidPositions.IsCreated && _lastValidPositions.Length >= EntityCount)
            {
                position = SanitizeFloat3(_lastValidPositions[0], float3.zero);
                return math.all(math.isfinite(position));
            }

            position = float3.zero;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private quaternion ResolveAuthoritativeRotationSnapshot()
        {
            if (_hasAuthoritativePoseSnapshot && HasSyncStateReadStorage())
                return CanonicalizeRotation(_stateRead[0].Rotation);

            if (_cachedTransform != null)
                return CanonicalizeRotation(ToQuaternion(_cachedTransform.rotation));

            if (_body != null)
                return CanonicalizeRotation(ToQuaternion(_body.rotation));

            return quaternion.identity;
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

            if (!TryConvertRuntimePositionToAup(snappedPosition, out AbsoluteUniversePosition aup))
            {
                AddFaultFlag(FaultNaN);
                return;
            }

            uint hash = BuildSyncFenceHash(in aup, snappedVelocity, rotation);
            _stateWrite[0] = new PlayerKinematicsSyncState
            {
                Position = snappedPosition,
                Velocity = snappedVelocity,
                Rotation = rotation,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
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

            if (HydrodynamicKccOwnsAuthority())
                return;

            PlayerKinematicsSyncState state = _stateWrite[0];
            _stateRead[0] = state;
            _positions[0] = state.Position;
            _velocities[0] = state.Velocity;
            if ((state.Flags & (uint)(FaultNaN | FaultSolidTeleport)) == 0u)
                _lastValidPositions[0] = state.Position;
            _hasAuthoritativePoseSnapshot = true;

            Vector3 position = ToVector3(state.Position);
            Vector3 velocity = ToVector3(state.Velocity);
            if (_motor != null)
            {
                _motor.MovePosition(position);
                _motor.SetLinearVelocity(velocity);
            }

            if (_motor != null && (state.Flags & SyncStateFlagApplyRotation) != 0u)
            {
                Quaternion rotation = ToUnityQuaternion(state.Rotation);
                if (IsFinite(rotation))
                    _motor.MoveRotation(rotation);
            }

            _stateWriteReady = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStateCorrectionLaneDiagnostics()
        {
            s_stateCorrectionLaneDeadWarned = false;
        }
#endif

        private void ApplyPendingStateCorrections()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Announced unconditionally on the first PostFixedTick rather than on "the drain came back empty",
            // because an empty drain is indistinguishable from a quiet frame. The claim below is static
            // reachability, not an observation of this run, so it must not wait for a runtime symptom.
            // The message is built from adjacent string literals, which Roslyn folds into one literal at compile
            // time, so nothing here concatenates or allocates at runtime. Unity's own logging still allocates
            // internally on the single first fire; steady state is one static bool read and a branch not taken.
            if (!s_stateCorrectionLaneDeadWarned)
            {
                s_stateCorrectionLaneDeadWarned = true;
                Hecton8.Core.H8Debug.LogWarning(
                    "[PlayerKinematicsRuntime] DEAD PUBLISHER, LIVE DESTRUCTIVE DRAIN: ApplyPendingStateCorrections " +
                    "drains SignalBus<StateCorrectionSignal> on every PostFixedTick (fixed-step cadence), but that " +
                    "lane has no reachable publisher, so the drain is always empty. The only push into the lane is " +
                    "CoreDeterminismSignals.TryPublish(in StateCorrectionSignal) (Core/Signals/CoreDeterminismSignals" +
                    ".cs:100), whose only caller is PhysicsDeterminismSignals.TryPublish(in StateCorrectionSignal) " +
                    "(Physics/PhysicsDeterminismSignals.cs:126), and PhysicsDeterminismSignals has zero invoking " +
                    "callers anywhere in the scripts tree - its only textual reference outside itself is an edit " +
                    "test that reads the file as a string (Tests/Editor/KelpShaderScalability1427EditTests.cs:4622). " +
                    "MISSING PLAYER-VISIBLE BEHAVIOUR: authoritative reconciliation of the player never happens. A " +
                    "correction would resolve position, velocity and rotation (ResolveCorrectionPosition/Velocity/" +
                    "Rotation, this file :4393/:4416/:4427) and stage them with SyncStateFlagCorrection, which " +
                    "CommitStateWrite applies the same tick through _motor.MovePosition / SetLinearVelocity and " +
                    "optionally MoveRotation (this file :3948-3956). With no publisher the player capsule is never " +
                    "snapped or rewound to an authoritative pose: a client that has drifted or desynced stays " +
                    "drifted, and the hash-mismatch desync report at :4031 never fires from this path. FIX " +
                    "CONSTRAINT - the read is destructive and its cursor is SHARED, not per-reader: SignalBus<T>" +
                    ".TryConsumeFrame (Core/Signals/SignalBusRuntime.cs:806) advances one static _legacyReadCursor " +
                    "per closed generic type (:414), reset each frame by FlushPostSimulation (:901). Adding a " +
                    "publisher plus a second TryConsumeFrame reader would therefore PARTITION each frame's " +
                    "corrections between the readers by dispatcher order instead of delivering them to both. This " +
                    "drain also discards corrections addressed to other sources - the SourceId mismatch continues " +
                    "at :4018-4019 after the shared cursor has already advanced past the entry - so with more than " +
                    "one PlayerKinematicsRuntime instance the first to tick eats the others' corrections. And " +
                    "StateCorrectionDrainLimit is 8 while the lane is configured for 16 (Core/Signals/GlobalSignals" +
                    ".State.cs:124), so a full frame would leave 8 corrections unread before the flush clears them. " +
                    "Any fix must either keep exactly one draining owner that fans out, or move readers to the " +
                    "non-destructive GetFrameSnapshot (Core/Signals/SignalBusRuntime.cs:773) and filter per instance.",
                    this);
            }
#endif
            for (int i = 0; i < StateCorrectionDrainLimit; i++)
            {
                if (!CoreDeterminismSignals.TryDequeueStateCorrection(out StateCorrectionSignal correction))
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
                    (correction.Flags & CoreDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0;
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

            bool hasReadableStateSnapshot = _hasAuthoritativePoseSnapshot && HasSyncStateReadStorage();
            float3 position = hasReadableStateSnapshot
                ? _stateRead[0].Position
                : SnapMillimeter(SanitizeFloat3((float3)(ResolveBodyRuntimePosition()), ReadLastValidPosition()));
            float3 velocity = hasReadableStateSnapshot
                ? _stateRead[0].Velocity
                : SnapMillimeter(ReadVelocitySnapshot(float3.zero));
            position = SanitizeFloat3(position, ReadLastValidPosition());
            velocity = SanitizeFloat3(velocity, float3.zero);
            quaternion rotation = hasReadableStateSnapshot ? _stateRead[0].Rotation : ResolveAuthoritativeRotationSnapshot();
            Vector3 runtimePosition = ToVector3(position);
            if (!TryConvertRuntimePositionToAup(position, out AbsoluteUniversePosition aup))
                return;

            uint hash = BuildSyncFenceHash(in aup, velocity, rotation);
            float maxDriftErrorMeters = ResolveAupMaxDriftErrorMeters(in aup, position);
            _accumulatorState.LastSyncFenceHash = hash;
            _accumulatorState.LastSyncFenceFrame = Hecton8.Core.SystemDispatcher.CurrentFrameId;

            SyncFenceSignal signal = default;
            signal.PositionAup = aup;
            signal.RuntimePosition = position;
            signal.Velocity = velocity;
            signal.Rotation = rotation;
            signal.StateHash = hash;
            signal.Frame = _accumulatorState.LastSyncFenceFrame;
            signal.SourceId = _sourceId;
            signal.Flags = 0;
            CoreDeterminismSignals.TryPublish(in signal);
            WriteSyncFenceTelemetry(in signal, maxDriftErrorMeters);
            CrashTelemetryBuffer.ReportAupMaxDriftError(runtimePosition, maxDriftErrorMeters);
        }

        private static float ResolveAupMaxDriftErrorMeters(in AbsoluteUniversePosition aup, float3 runtimePosition)
        {
            double3 expectedRuntime = ToAbsoluteDouble3(in aup) - HectonFloatingOrigin.CurrentTotalOffsetDouble;
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
            signal.Frame = frame != 0u ? frame : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceId = _sourceId;
            signal.LastFenceFrame = _accumulatorState.LastSyncFenceFrame;
            signal.Flags = flags;
            CoreDeterminismSignals.TryPublish(in signal);
            AddFaultFlag(FaultDesync);
            DumpFaultTelemetryIfNeeded();
        }

        private uint BuildCurrentSyncFenceHash()
        {
            if (_hasAuthoritativePoseSnapshot && HasSyncStateReadStorage())
            {
                PlayerKinematicsSyncState state = _stateRead[0];
                float3 position = SanitizeFloat3(state.Position, ReadLastValidPosition());
                float3 velocity = SanitizeFloat3(state.Velocity, float3.zero);
                if (!TryConvertRuntimePositionToAup(position, out AbsoluteUniversePosition aup))
                    return 0u;

                return BuildSyncFenceHash(in aup, velocity, CanonicalizeRotation(state.Rotation));
            }

            if (_body == null)
                return 0u;

            float3 bodyPosition = SanitizeFloat3((float3)(ResolveBodyRuntimePosition()), ReadLastValidPosition());
            float3 bodyVelocity = ReadVelocitySnapshot(float3.zero);
            if (!TryConvertRuntimePositionToAup(bodyPosition, out AbsoluteUniversePosition bodyAup))
                return 0u;

            return BuildSyncFenceHash(in bodyAup, bodyVelocity, ResolveAuthoritativeRotationSnapshot());
        }

        private static PlayerKinematicsSyncState RehashState(PlayerKinematicsSyncState state)
        {
            state.Position = SanitizeFloat3(state.Position, float3.zero);
            state.Velocity = SanitizeFloat3(state.Velocity, float3.zero);
            state.Rotation = CanonicalizeRotation(state.Rotation);
            state.StateHash = TryConvertRuntimePositionToAup(state.Position, out AbsoluteUniversePosition aup)
                ? BuildSyncFenceHash(in aup, state.Velocity, state.Rotation)
                : 0u;
            return state;
        }

        private static uint BuildSyncFenceHash(in AbsoluteUniversePosition aup, float3 velocity, quaternion rotation)
        {
            uint hash = DeterministicContractMath.FnvOffsetBasis;
            hash = DeterministicContractMath.Fnv1a(hash, aup.GridX);
            hash = DeterministicContractMath.Fnv1a(hash, aup.GridY);
            hash = DeterministicContractMath.Fnv1a(hash, aup.GridZ);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, aup.LocalX);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, aup.LocalY);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, aup.LocalZ);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, velocity.x);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, velocity.y);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, velocity.z);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, rotation.value.x);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, rotation.value.y);
            hash = DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, rotation.value.z);
            return DeterministicContractMath.Fnv1aQuantizedMillimeter(hash, rotation.value.w);
        }

        private void DumpFaultTelemetryIfNeeded()
        {
            if (!HasFaultFlagStorage() ||
                !_telemetry.IsCreated ||
                _telemetry.Length <= 0 ||
                _faultFlags[0] == 0)
            {
                return;
            }

            int faultFlags = _faultFlags[0];
            bool requiresDesyncDump = (faultFlags & FaultDesync) != 0;
            if (_dumpWrittenForFault && (!requiresDesyncDump || _desyncDumpWritten))
                return;

            const string physicsPath = "Docs/AgentLogs/Dump_PHYSICS_DETERMINISM_SYNC.bin";
            const string ikPath = "Docs/AgentLogs/Dump_PLAYER_IK_ENVIRONMENT_ADAPTER.bin";
            string aupWatchdogPath = "Docs/AgentLogs/" + AupWatchdogDumpFileName;
            string sdfSqueezePath = "Docs/AgentLogs/" + SdfSqueezeDumpFileName;
            bool wroteAll =
                WriteTelemetryDump(physicsPath, 0x48503844u, faultFlags) &
                WriteTelemetryDump(ikPath, 0x50494B42u, faultFlags) &
                WriteTelemetryDump(aupWatchdogPath, AupWatchdogDumpMagic, faultFlags) &
                WriteTelemetryDump(sdfSqueezePath, SdfSqueezeDumpMagic, faultFlags);

            if (!wroteAll)
                return;

            _dumpWrittenForFault = true;
            if (requiresDesyncDump)
                _desyncDumpWritten = true;
        }

        private bool WriteTelemetryDump(string path, uint magic, int faultFlags)
        {
            const int HeaderBytes = 20;
            const int RowBytes = 68;
            int telemetryHead = ResolveTelemetryHeadIndex();
            int telemetryLength = _telemetry.Length;
            int totalBytes = HeaderBytes + telemetryLength * RowBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(PlayerKinematicsRuntime),
                "PlayerKinematicsRuntimeTelemetryDumpPayload");
            try
            {
                WriteUInt32LittleEndian(payload, 0, magic);
                WriteInt32LittleEndian(payload, 4, faultFlags);
                WriteInt32LittleEndian(payload, 8, telemetryHead);
                WriteUInt32LittleEndian(payload, 12, _accumulatorState.LastSyncFenceHash);
                WriteUInt32LittleEndian(payload, 16, _accumulatorState.LastSyncFenceFrame);

                int offset = HeaderBytes;
                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = telemetryHead + i;
                    if (telemetryIndex >= telemetryLength)
                        telemetryIndex -= telemetryLength;

                    PlayerKinematicsRuntimeTelemetryEntry entry = _telemetry[telemetryIndex];
                    WriteFloat32LittleEndian(payload, offset, entry.Position.x);
                    WriteFloat32LittleEndian(payload, offset + 4, entry.Position.y);
                    WriteFloat32LittleEndian(payload, offset + 8, entry.Position.z);
                    WriteFloat32LittleEndian(payload, offset + 12, entry.Velocity.x);
                    WriteFloat32LittleEndian(payload, offset + 16, entry.Velocity.y);
                    WriteFloat32LittleEndian(payload, offset + 20, entry.Velocity.z);
                    WriteFloat32LittleEndian(payload, offset + 24, entry.IntendedMovement.x);
                    WriteFloat32LittleEndian(payload, offset + 28, entry.IntendedMovement.y);
                    WriteFloat32LittleEndian(payload, offset + 32, entry.IntendedMovement.z);
                    WriteFloat32LittleEndian(payload, offset + 36, entry.DragCoefficient);
                    WriteFloat32LittleEndian(payload, offset + 40, entry.WaterDensity);
                    WriteFloat32LittleEndian(payload, offset + 44, entry.SolidDensity);
                    WriteUInt32LittleEndian(payload, offset + 48, entry.Frame);
                    WriteUInt32LittleEndian(payload, offset + 52, entry.Flags);
                    WriteUInt32LittleEndian(payload, offset + 56, entry.SyncFenceHash);
                    WriteUInt32LittleEndian(payload, offset + 60, entry.AuxFlags);
                    WriteFloat32LittleEndian(payload, offset + 64, entry.AupMaxDriftErrorMeters);
                    offset += RowBytes;
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(PlayerKinematicsRuntime),
                    "PlayerKinematicsRuntimeTelemetryDumpPayload");
            }
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private float3 SafeRight()
        {
            Transform source = _cameraTransform != null ? _cameraTransform : _cachedTransform;
            return source != null ? SafeNormalize((float3)(source.right), new float3(1.0f, 0.0f, 0.0f)) : new float3(1.0f, 0.0f, 0.0f);
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
                DeterministicContractMath.SnapMillimeter(value.x),
                DeterministicContractMath.SnapMillimeter(value.y),
                DeterministicContractMath.SnapMillimeter(value.z));
        }

        private float3 ResolveCorrectionPosition(in StateCorrectionSignal correction)
        {
            bool runtimePositionFlagged =
                (correction.Flags & CoreDeterminismSignals.StateCorrectionSignalFlagRuntimePositionValid) != 0;
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

            float3 fallback = ReadStatePositionSnapshot(float3.zero);
            return TryConvertAupToRuntimePosition(in correction.PositionAup, out float3 runtimePosition)
                ? SnapMillimeter(SanitizeFloat3(runtimePosition, fallback))
                : fallback;
        }

        private float3 ResolveCorrectionVelocity(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & CoreDeterminismSignals.StateCorrectionSignalFlagVelocityValid) != 0 &&
                math.all(math.isfinite(correction.Velocity)))
            {
                return SnapMillimeter(correction.Velocity);
            }

            return ReadStateVelocitySnapshot(ReadVelocitySnapshot(float3.zero));
        }

        private quaternion ResolveCorrectionRotation(in StateCorrectionSignal correction)
        {
            if ((correction.Flags & CoreDeterminismSignals.StateCorrectionSignalFlagRotationValid) != 0)
                return CanonicalizeRotation(correction.Rotation);

            return ResolveAuthoritativeRotationSnapshot();
        }

        private static bool IsFiniteNonZero(float3 value)
        {
            return math.all(math.isfinite(value)) && math.lengthsq(value) > 0.000001f;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition value)
        {
            return math.all(math.isfinite(new double3(value.LocalX, value.LocalY, value.LocalZ)));
        }

        private static bool TryConvertRuntimePositionToAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            return TryConvertRuntimePositionToAup((float3)(runtimePosition), out positionAup);
        }

        private static bool TryConvertRuntimePositionToAup(float3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.all(math.isfinite(runtimePosition)))
                return false;

            double3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(runtimePosition);
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            positionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePosition);
            return IsFiniteAup(in positionAup);
        }

        private static bool TryConvertAupToRuntimePosition(in AbsoluteUniversePosition positionAup, out float3 runtimePosition)
        {
            runtimePosition = default;
            if (!IsFiniteAup(in positionAup))
                return false;

            double3 absolutePosition = ToAbsoluteDouble3(in positionAup);
            if (!math.all(math.isfinite(absolutePosition)))
                return false;

            Vector3 candidate = HectonFloatingOrigin.ToRuntimePosition(absolutePosition);
            runtimePosition = (float3)(candidate);
            return math.all(math.isfinite(runtimePosition));
        }

        private static double3 ToAbsoluteDouble3(in AbsoluteUniversePosition position)
        {
            const double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                position.GridX * cell + position.LocalX,
                position.GridY * cell + position.LocalY,
                position.GridZ * cell + position.LocalZ);
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

        private float RefreshGlobalQualityWeight01()
        {
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01(_cachedGlobalQualityWeight01);
            return _cachedGlobalQualityWeight01;
        }

        private float ReadCachedGlobalQualityWeight01()
        {
            float cached = _cachedGlobalQualityWeight01;
            return math.saturate(math.select(1.0f, cached, math.isfinite(cached)));
        }

        private static float ResolveGlobalQualityWeight01(float fallback)
        {
            float qualityWeight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(fallback, qualityWeight, math.isfinite(qualityWeight)));
        }

        private static float SmoothQuality01(float value)
        {
            float t = math.saturate(math.select(1.0f, value, math.isfinite(value)));
            return t * t * (3.0f - 2.0f * t);
        }

        private static byte ResolveQualityPressureQ8(float qualityWeight01)
        {
            float pressure01 = 1.0f - SmoothQuality01(qualityWeight01);
            return (byte)math.clamp((int)math.round(pressure01 * 255.0f), 0, 255);
        }

        private byte ResolveSdfSampleMode(float qualityWeight01, uint frame)
        {
            float fullGradientWeight01 = SmoothQuality01(qualityWeight01);
            float phase01 = ResolveDeterministicQualityPhase01(frame, unchecked((uint)_cadenceSalt) ^ 0xA511E9B3u);
            return (byte)math.select(
                (int)PlayerKinematicsBodyJob.SdfSampleModeTetra4,
                (int)PlayerKinematicsBodyJob.SdfSampleModeAxis6,
                phase01 < fullGradientWeight01);
        }

        private bool ShouldRefreshPassiveSdfPayload(float qualityWeight01, uint frame)
        {
            float refreshWeight01 = SmoothQuality01(qualityWeight01);
            float phase01 = ResolveDeterministicQualityPhase01(frame, unchecked((uint)_cadenceSalt) ^ 0x6C8E9CF5u);
            return phase01 < refreshWeight01;
        }

        private static bool IsReducedSdfSampleMode(byte sampleMode)
        {
            return (sampleMode & PlayerKinematicsBodyJob.SdfSampleModeTetra4) != 0;
        }

        private static int ResolveHandProbeCount(float qualityWeight01)
        {
            float probeCount = math.lerp(1.0f, (float)EnvironmentProbeCount, SmoothQuality01(qualityWeight01));
            return math.clamp((int)math.round(probeCount), 1, EnvironmentProbeCount);
        }

        private static int ResolveHandProbeFrameMask(float qualityWeight01)
        {
            float cadenceMask = math.lerp((float)MinimumQualityHandProbeFrameMask, 0.0f, SmoothQuality01(qualityWeight01));
            return math.clamp((int)math.round(cadenceMask), 0, MinimumQualityHandProbeFrameMask);
        }

        private static float ResolveDeterministicQualityPhase01(uint frame, uint salt)
        {
            uint hash = frame ^ salt;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * 0.0000000596046483f;
        }

        private static bool IsFreshSignalFrame(uint currentFrame, uint signalFrame, uint maxAgeFrames)
        {
            if (signalFrame > currentFrame)
                return false;

            return currentFrame - signalFrame <= maxAgeFrames;
        }

        private bool HasHandProbeStorage(int requiredProbeCount)
        {
            return _handProbeHits.IsCreated &&
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
            float cadenceMask = math.lerp(3.0f, 0.0f, SmoothQuality01(ReadCachedGlobalQualityWeight01()));
            return math.clamp((int)math.round(cadenceMask), 0, 3);
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

    
        #region JulesLink_StrafeAngleBlendWeightCalculator
        private static void JulesLink_StrafeAngleBlendWeightCalculator() { _ = typeof(Hecton8.PureLogic.Kinematics.StrafeAngleBlendWeightCalculator); }
        #endregion
}
}
