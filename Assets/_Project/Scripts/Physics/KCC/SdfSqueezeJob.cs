using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.KCC
{
    public enum SdfSqueezeSampleMode : byte
    {
        Axis6 = 0,
        Tetra4 = 1
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 64)]
    public struct SdfSqueezeResult
    {
        public const uint FlagActive = 1u << 0;
        public const uint FlagGradientValid = 1u << 1;
        public const uint FlagLowTier = 1u << 2;
        public const uint FlagNaNFallback = 1u << 3;
        public const uint FlagSlowCadence = 1u << 4;
        public const uint FlagSpeedPenalty = 1u << 5;

        [FieldOffset(0)] public float3 Position;
        [FieldOffset(12)] public float3 Velocity;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float PushSpeed;
        [FieldOffset(40)] public float PushMeters;
        [FieldOffset(44)] public float CenterDensity;
        [FieldOffset(48)] public float Stress01;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint Reserved;

        public bool IsActive => (Flags & FlagActive) != 0u;
    }

    [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct SdfSqueezeJob : IJob
    {
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<float3> IntendedMovement;
        [ReadOnly] public NativeArray<byte> VoxelSdfTexture3D;
        public NativeArray<SdfSqueezeResult> Results;

        public int3 VoxelSdfDimensions;
        public float3 VoxelSdfOrigin;
        public float3 VoxelSdfCellSize;
        public double3 TargetAupAbsolute;
        public double3 FloatingOriginOffset;
        public float VoxelSdfRange;
        public float SdfSampleStepMeters;
        public float DeltaTime;
        public float MaxPushOutSpeedMetersPerSecond;
        public float SpeedPenalty01;
        public float SystemStress01;
        public byte SampleMode;
        public byte LowTier;
        public byte SlowCadence;
        public uint Frame;

        private const float InvEncodedSdfByteMax = 0.0039215686274509803f;

        public void Execute()
        {
            if (!Positions.IsCreated ||
                !Velocities.IsCreated ||
                !Results.IsCreated ||
                Positions.Length <= 0 ||
                Velocities.Length <= 0 ||
                Results.Length <= 0)
            {
                return;
            }

            float dt = math.max(0.0001f, SanitizeNonNegative(DeltaTime));
            float3 fallbackPosition = SanitizeFloat3(Positions[0], float3.zero);
            float3 position = fallbackPosition;
            float3 velocity = SanitizeFloat3(Velocities[0], float3.zero);
            float3 intended = IntendedMovement.IsCreated && IntendedMovement.Length > 0
                ? SanitizeFloat3(IntendedMovement[0], float3.zero)
                : float3.zero;

            SdfSqueezeResult result = new SdfSqueezeResult
            {
                Position = position,
                Velocity = velocity,
                Frame = Frame
            };

            if (!TryResolveRuntimePositionFromAup(TargetAupAbsolute, FloatingOriginOffset, fallbackPosition, out float3 sdfTarget))
            {
                result.Flags = SdfSqueezeResult.FlagNaNFallback;
                Results[0] = result;
                return;
            }

            if (!TryResolveOpenSpaceNormal(
                    VoxelSdfTexture3D,
                    VoxelSdfDimensions,
                    VoxelSdfOrigin,
                    VoxelSdfCellSize,
                    VoxelSdfRange,
                    sdfTarget,
                    intended,
                    SdfSampleStepMeters,
                    SampleMode,
                    out float3 normal,
                    out float centerDensity))
            {
                result.CenterDensity = math.select(centerDensity, 0.0f, !math.isfinite(centerDensity));
                if (centerDensity > 0.0f)
                    result.Flags |= SdfSqueezeResult.FlagNaNFallback;
                Results[0] = result;
                return;
            }

            result.CenterDensity = centerDensity;
            if (centerDensity <= 0.0f)
            {
                Results[0] = result;
                return;
            }

            float maxPushSpeed = math.max(0.0f, SanitizeNonNegative(MaxPushOutSpeedMetersPerSecond));
            float pushMeters = math.min(SanitizeNonNegative(centerDensity), maxPushSpeed * dt);
            float pushSpeed = pushMeters * math.rcp(dt);
            if (pushSpeed > maxPushSpeed)
            {
                pushSpeed = maxPushSpeed;
                pushMeters = pushSpeed * dt;
            }

            float3 pushVelocity = normal * pushSpeed;
            velocity = ApplyForwardSpeedPenalty(velocity, intended, SpeedPenalty01);
            velocity += pushVelocity;
            position = SanitizeFloat3(position + normal * pushMeters, fallbackPosition);

            uint flags = SdfSqueezeResult.FlagActive |
                         SdfSqueezeResult.FlagGradientValid |
                         SdfSqueezeResult.FlagSpeedPenalty;
            if ((SampleMode & (byte)SdfSqueezeSampleMode.Tetra4) != 0 || LowTier != 0)
                flags |= SdfSqueezeResult.FlagLowTier;
            if (SlowCadence != 0 || SystemStress01 > 0.8f)
                flags |= SdfSqueezeResult.FlagSlowCadence;

            result.Position = SnapMillimeter(position);
            result.Velocity = SnapMillimeter(velocity);
            result.Normal = normal;
            result.PushSpeed = pushSpeed;
            result.PushMeters = pushMeters;
            result.Stress01 = ResolveStress01(centerDensity, VoxelSdfRange, SdfSampleStepMeters);
            result.Flags = flags;
            Results[0] = result;

            Positions[0] = result.Position;
            Velocities[0] = result.Velocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveRuntimePositionFromAup(
            double3 targetAupAbsolute,
            double3 floatingOriginOffset,
            float3 fallbackPosition,
            out float3 runtimePosition)
        {
            runtimePosition = fallbackPosition;
            if (!math.all(math.isfinite(targetAupAbsolute)) ||
                !math.all(math.isfinite(floatingOriginOffset)) ||
                !math.all(math.isfinite(fallbackPosition)))
            {
                return false;
            }

            double3 runtime = targetAupAbsolute - floatingOriginOffset;
            double maxAbs = math.cmax(math.abs(runtime));
            if (!math.isfinite(maxAbs) || maxAbs > 100000000.0)
                return false;

            runtimePosition = new float3((float)runtime.x, (float)runtime.y, (float)runtime.z);
            return math.all(math.isfinite(runtimePosition));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveOpenSpaceNormal(
            NativeArray<byte> encodedSdf,
            int3 gridDimensions,
            float3 volumeOrigin,
            float3 cellSize,
            float sdfRange,
            float3 targetPosition,
            float3 intendedMovement,
            float sampleStepMeters,
            byte sampleMode,
            out float3 normal,
            out float centerDensity)
        {
            normal = float3.zero;
            centerDensity = 0.0f;
            if (!TrySampleSdfTrilinear(encodedSdf, gridDimensions, volumeOrigin, cellSize, sdfRange, targetPosition, out centerDensity))
                return false;

            float step = math.max(0.025f, SanitizeNonNegative(math.abs(sampleStepMeters)));
            float3 openGradient;
            if ((sampleMode & (byte)SdfSqueezeSampleMode.Tetra4) != 0)
            {
                const float invRoot3 = 0.57735026919f;
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

            float3 intendedDirection = NormalizeWithRsqrtGuard(intendedMovement, float3.zero);
            float3 candidate = openGradient;
            if (math.lengthsq(intendedDirection) > 0.000001f)
            {
                float3 lateral = openGradient - intendedDirection * math.dot(openGradient, intendedDirection);
                if (math.lengthsq(lateral) > 0.000001f && math.all(math.isfinite(lateral)))
                    candidate = lateral;
            }

            normal = NormalizeWithRsqrtGuard(candidate, float3.zero);
            return math.lengthsq(normal) > 0.000001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TrySampleSdfTrilinear(
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
                encodedSdf.Length != voxelCount ||
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
        private static bool TryResolveSdfVoxelCount(int3 gridDimensions, out int voxelCount)
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
        private static float3 ApplyForwardSpeedPenalty(float3 velocity, float3 intendedMovement, float penalty01)
        {
            float3 safeVelocity = SanitizeFloat3(velocity, float3.zero);
            float3 intended = NormalizeWithRsqrtGuard(intendedMovement, float3.zero);
            if (math.lengthsq(intended) <= 0.000001f)
                intended = NormalizeWithRsqrtGuard(safeVelocity, float3.zero);
            if (math.lengthsq(intended) <= 0.000001f)
                return safeVelocity;

            float forwardSpeed = math.dot(safeVelocity, intended);
            if (!math.isfinite(forwardSpeed) || forwardSpeed <= 0.0f)
                return safeVelocity;

            float penalty = math.saturate(math.select(penalty01, 0.0f, !math.isfinite(penalty01)));
            return safeVelocity - intended * (forwardSpeed * penalty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveStress01(float centerDensity, float sdfRange, float sampleStepMeters)
        {
            float denominator = math.max(0.05f, math.max(SanitizeNonNegative(sdfRange) * 0.25f, SanitizeNonNegative(sampleStepMeters) * 2.0f));
            return math.saturate(SanitizeNonNegative(centerDensity) * math.rcp(denominator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeWithRsqrtGuard(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000001f)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value)
        {
            return math.select(math.max(0.0f, value), 0.0f, !math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFloat3(float3 value, float3 fallback)
        {
            float3 safeFallback = math.select(fallback, float3.zero, !math.all(math.isfinite(fallback)));
            return math.select(value, safeFallback, !math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SnapMillimeter(float3 value)
        {
            return new float3(
                math.round(value.x * 1000.0f) * 0.001f,
                math.round(value.y * 1000.0f) * 0.001f,
                math.round(value.z * 1000.0f) * 0.001f);
        }
    }
}
