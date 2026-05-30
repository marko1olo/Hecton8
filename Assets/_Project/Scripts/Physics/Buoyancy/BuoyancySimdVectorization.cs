using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SimdFloat3Padded
    {
        [FieldOffset(0)] public float3 Value;
        [FieldOffset(12)] private float _pad0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SimdFloat3Padded FromFloat3(float3 value)
        {
            SimdFloat3Padded padded = default;
            padded.Value = math.select(float3.zero, value, math.isfinite(value));
            padded._pad0 = 0f;
            return padded;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SimdMathToleranceDTO
    {
        [FieldOffset(0)] public uint FormulaHash;
        [FieldOffset(4)] public int PolynomialDegree;
        [FieldOffset(8)] public float MaxError;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SimdTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint KernelHash;
        [FieldOffset(8)] public int EntityCount;
        [FieldOffset(12)] public float VectorMicros;
        [FieldOffset(16)] public float ScalarMicros;
        [FieldOffset(20)] public float EntitiesPerMillisecond;
        [FieldOffset(24)] public float ThroughputDrop01;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint LastStateHash;
        [FieldOffset(40)] public float MaxError;
        [FieldOffset(44)] public float MaxSpeedSq;
        [FieldOffset(48)] private ulong _pad0;
        [FieldOffset(56)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SimdHydrodynamicTuningDTO
    {
        [FieldOffset(0)] public float DeltaTime;
        [FieldOffset(4)] public float GlobalQualityWeight;
        [FieldOffset(8)] public float BaseLinearDrag;
        [FieldOffset(12)] public float BuoyancyAccelerationY;
        [FieldOffset(16)] public float3 BaseFlowVelocity;
        [FieldOffset(28)] public float TurbulenceAmplitude;
        [FieldOffset(32)] public float MaxSpeed;
        [FieldOffset(36)] public uint FrameIndex;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float ScalarFallbackWeight01;
        [FieldOffset(48)] public float ApproximationQualityWeight;
        [FieldOffset(52)] public float MaxApproximationError;
        [FieldOffset(56)] public int SinPolynomialDegree;
        [FieldOffset(60)] private uint _pad0;
    }

    public static class SimdVectorizationConstants
    {
        public const int HydrodynamicsLaneWidth = 4;
        public const int SpatialQueryLaneWidth = 4;
        public const int FrustumCullLaneWidth = 8;
        public const int BenchmarkEntityCount = 250000;
        public const int TelemetryCapacity = 300;
        public const int ToleranceCapacity = 64;
        public const uint HydrodynamicsKernelHash = 0x53323031u;
        public const uint SpatialQueryKernelHash = 0x53323151u;
        public const uint FrustumCullKernelHash = 0x53324643u;
        public const uint FlagActive = 1u << 0;
        public const uint FlagNonFinite = 1u << 31;
        public const uint SinPolynomialFormulaHash = 0x7D809260u;
        public const uint HydrodynamicTurbulenceFormulaHash = 0x47C3A66Au;
    }

    public static class SimdVectorizationLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        public static bool Validate()
        {
            return s_validateOnce;
        }

        private static bool ValidateInternal()
        {
            return UnsafeUtility.SizeOf<SimdFloat3Padded>() == 16 &&
                   UnsafeUtility.SizeOf<SimdMathToleranceDTO>() == 16 &&
                   UnsafeUtility.SizeOf<SimdTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<SimdHydrodynamicTuningDTO>() == 64 &&
                   ValidateFloat3PaddedOffsets() &&
                   ValidateMathToleranceOffsets() &&
                   ValidateTelemetryOffsets() &&
                   ValidateHydrodynamicTuningOffsets();
        }

        private static bool ValidateFloat3PaddedOffsets()
        {
            return OffsetOf<SimdFloat3Padded>(nameof(SimdFloat3Padded.Value)) == 0 &&
                   OffsetOf<SimdFloat3Padded>("_pad0") == 12;
        }

        private static bool ValidateMathToleranceOffsets()
        {
            return OffsetOf<SimdMathToleranceDTO>(nameof(SimdMathToleranceDTO.FormulaHash)) == 0 &&
                   OffsetOf<SimdMathToleranceDTO>(nameof(SimdMathToleranceDTO.PolynomialDegree)) == 4 &&
                   OffsetOf<SimdMathToleranceDTO>(nameof(SimdMathToleranceDTO.MaxError)) == 8 &&
                   OffsetOf<SimdMathToleranceDTO>(nameof(SimdMathToleranceDTO.Flags)) == 12;
        }

        private static bool ValidateTelemetryOffsets()
        {
            return OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.KernelHash)) == 4 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.EntityCount)) == 8 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.VectorMicros)) == 12 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.ScalarMicros)) == 16 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.EntitiesPerMillisecond)) == 20 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.ThroughputDrop01)) == 24 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.GlobalQualityWeight)) == 28 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.Flags)) == 32 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.LastStateHash)) == 36 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.MaxError)) == 40 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry.MaxSpeedSq)) == 44 &&
                   OffsetOf<SimdTelemetryEntry>("_pad0") == 48 &&
                   OffsetOf<SimdTelemetryEntry>("_pad1") == 56;
        }

        private static bool ValidateHydrodynamicTuningOffsets()
        {
            return OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.DeltaTime)) == 0 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.GlobalQualityWeight)) == 4 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.BaseLinearDrag)) == 8 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.BuoyancyAccelerationY)) == 12 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.BaseFlowVelocity)) == 16 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.TurbulenceAmplitude)) == 28 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.MaxSpeed)) == 32 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.FrameIndex)) == 36 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.Flags)) == 40 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.ScalarFallbackWeight01)) == 44 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.ApproximationQualityWeight)) == 48 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.MaxApproximationError)) == 52 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO.SinPolynomialDegree)) == 56 &&
                   OffsetOf<SimdHydrodynamicTuningDTO>("_pad0") == 60;
        }

        public static int OffsetOf<T>(string fieldName) where T : struct
        {
            Type type = typeof(T);
            if (type == typeof(SimdFloat3Padded))
                return OffsetOfFloat3Padded(fieldName);
            if (type == typeof(SimdMathToleranceDTO))
                return OffsetOfMathTolerance(fieldName);
            if (type == typeof(SimdTelemetryEntry))
                return OffsetOfTelemetry(fieldName);
            if (type == typeof(SimdHydrodynamicTuningDTO))
                return OffsetOfHydrodynamicTuning(fieldName);

            return -1;
        }

        private static int OffsetOfFloat3Padded(string fieldName)
        {
            if (fieldName == nameof(SimdFloat3Padded.Value))
                return 0;
            if (fieldName == "_pad0")
                return 12;

            return -1;
        }

        private static int OffsetOfMathTolerance(string fieldName)
        {
            if (fieldName == nameof(SimdMathToleranceDTO.FormulaHash)) return 0;
            if (fieldName == nameof(SimdMathToleranceDTO.PolynomialDegree)) return 4;
            if (fieldName == nameof(SimdMathToleranceDTO.MaxError)) return 8;
            if (fieldName == nameof(SimdMathToleranceDTO.Flags)) return 12;
            return -1;
        }

        private static int OffsetOfTelemetry(string fieldName)
        {
            if (fieldName == nameof(SimdTelemetryEntry.FrameIndex)) return 0;
            if (fieldName == nameof(SimdTelemetryEntry.KernelHash)) return 4;
            if (fieldName == nameof(SimdTelemetryEntry.EntityCount)) return 8;
            if (fieldName == nameof(SimdTelemetryEntry.VectorMicros)) return 12;
            if (fieldName == nameof(SimdTelemetryEntry.ScalarMicros)) return 16;
            if (fieldName == nameof(SimdTelemetryEntry.EntitiesPerMillisecond)) return 20;
            if (fieldName == nameof(SimdTelemetryEntry.ThroughputDrop01)) return 24;
            if (fieldName == nameof(SimdTelemetryEntry.GlobalQualityWeight)) return 28;
            if (fieldName == nameof(SimdTelemetryEntry.Flags)) return 32;
            if (fieldName == nameof(SimdTelemetryEntry.LastStateHash)) return 36;
            if (fieldName == nameof(SimdTelemetryEntry.MaxError)) return 40;
            if (fieldName == nameof(SimdTelemetryEntry.MaxSpeedSq)) return 44;
            if (fieldName == "_pad0") return 48;
            if (fieldName == "_pad1") return 56;
            return -1;
        }

        private static int OffsetOfHydrodynamicTuning(string fieldName)
        {
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.DeltaTime)) return 0;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.GlobalQualityWeight)) return 4;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.BaseLinearDrag)) return 8;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.BuoyancyAccelerationY)) return 12;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.BaseFlowVelocity)) return 16;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.TurbulenceAmplitude)) return 28;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.MaxSpeed)) return 32;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.FrameIndex)) return 36;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.Flags)) return 40;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.ScalarFallbackWeight01)) return 44;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.ApproximationQualityWeight)) return 48;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.MaxApproximationError)) return 52;
            if (fieldName == nameof(SimdHydrodynamicTuningDTO.SinPolynomialDegree)) return 56;
            if (fieldName == "_pad0") return 60;
            return -1;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockSimdBenchmarkJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> LocalPositions;
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [WriteOnly, NoAlias] public NativeArray<float> DragCoefficients;
        public int Count;
        public uint Seed;
        public uint FrameIndex;

        public void Execute(int index)
        {
            if (!LocalPositions.IsCreated || !Velocities.IsCreated || !DragCoefficients.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(LocalPositions.Length, math.min(Velocities.Length, DragCoefficients.Length)));
            if ((uint)index >= (uint)count)
                return;

            uint hash = Hash32((uint)index ^ Seed ^ (FrameIndex * 747796405u));
            float lane = (index & 511) - 255.5f;
            float row = ((index >> 9) & 511) - 255.5f;
            float layer = ((index >> 18) & 7) - 3.5f;
            float a = HashToSigned01(hash);
            float b = HashToSigned01(hash ^ 0x68E31DA4u);
            float c = HashToSigned01(hash ^ 0xB5297A4Du);
            float drag = math.lerp(0.018f, 0.45f, HashToUnit01(hash ^ 0x1B56C4E9u));

            LocalPositions[index] = SimdFloat3Padded.FromFloat3(new float3(lane * 0.45f, layer * 2.0f + a, row * 0.45f));
            Velocities[index] = SimdFloat3Padded.FromFloat3(new float3(a * 4.25f, b * 1.5f, c * 4.25f));
            DragCoefficients[index] = drag;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Hash32(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return math.select(1u, x, x != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnit01(uint x)
        {
            return (Hash32(x) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToSigned01(uint x)
        {
            return HashToUnit01(x) * 2f - 1f;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct HydrodynamicStateToSoAJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<BuoyancyStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [WriteOnly, NoAlias] public NativeArray<float> DragCoefficients;
        public int Count;
        public float BaseDragCoefficient;

        public void Execute(int index)
        {
            if (!States.IsCreated || !Velocities.IsCreated || !DragCoefficients.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(States.Length, math.min(Velocities.Length, DragCoefficients.Length)));
            if ((uint)index >= (uint)count)
                return;

            BuoyancyStateDTO state = States[index];
            float active = math.select(0f, 1f, state.EntityHashID != 0u);
            float safeMass = math.max(
                BuoyancyDisplacementConstants.Epsilon,
                math.select(BuoyancyDisplacementConstants.Epsilon, state.MassKg, math.isfinite(state.MassKg)));
            float safeVolume = math.max(0f, math.select(0f, state.VolumeCubicMeters, math.isfinite(state.VolumeCubicMeters)));
            float safeBaseDrag = math.max(0f, math.select(0f, BaseDragCoefficient, math.isfinite(BaseDragCoefficient)));
            float volumeScale = math.saturate(safeVolume * 128f);
            float drag = safeBaseDrag * (1f + volumeScale) * math.rcp(safeMass) * active;
            float3 velocity = math.select(float3.zero, state.Velocity, math.isfinite(state.Velocity));
            Velocities[index] = SimdFloat3Padded.FromFloat3(velocity * active);
            DragCoefficients[index] = math.select(0f, drag, math.isfinite(drag));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct HydrodynamicSoAToStateJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<BuoyancyStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        public int Count;

        public void Execute(int index)
        {
            if (!States.IsCreated || !Velocities.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(States.Length, Velocities.Length));
            if ((uint)index >= (uint)count)
                return;

            BuoyancyStateDTO state = States[index];
            float active = math.select(0f, 1f, state.EntityHashID != 0u);
            float3 rawSimdVelocity = Velocities[index].Value;
            float3 existingVelocity = math.select(float3.zero, state.Velocity, math.isfinite(state.Velocity));
            float3 simdVelocity = math.select(float3.zero, rawSimdVelocity, math.isfinite(rawSimdVelocity));
            state.Velocity = math.select(existingVelocity, simdVelocity, active > 0f);
            States[index] = state;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedHydrodynamicsJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> LocalPositions;
        [NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [ReadOnly, NoAlias] public NativeArray<float> DragCoefficients;
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> OutputForces;
        public SimdHydrodynamicTuningDTO Tuning;
        public int Count;

        public void Execute(int index)
        {
            if (!LocalPositions.IsCreated ||
                !Velocities.IsCreated ||
                !DragCoefficients.IsCreated ||
                !OutputForces.IsCreated)
            {
                return;
            }

            int count = math.min(
                math.max(0, Count),
                math.min(
                    LocalPositions.Length,
                    math.min(
                        Velocities.Length,
                        math.min(DragCoefficients.Length, OutputForces.Length))));
            if ((uint)index >= (uint)count)
                return;

            float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
            float3 rawPosition = LocalPositions[index].Value;
            float3 rawVelocity = Velocities[index].Value;
            float rawDragCoefficient = DragCoefficients[index];
            float3 position = math.select(float3.zero, rawPosition, math.isfinite(rawPosition));
            float3 velocity = math.select(float3.zero, rawVelocity, math.isfinite(rawVelocity));
            float dragCoefficient = math.select(0f, rawDragCoefficient, math.isfinite(rawDragCoefficient));
            float baseLinearDrag = math.max(0f, math.select(0f, Tuning.BaseLinearDrag, math.isfinite(Tuning.BaseLinearDrag)));
            float drag = math.max(0f, dragCoefficient + baseLinearDrag);
            float3 baseFlow = math.select(float3.zero, Tuning.BaseFlowVelocity, math.isfinite(Tuning.BaseFlowVelocity));
            float phase = (position.x * 0.01171875f) + (position.z * 0.017578125f) + ((Tuning.FrameIndex & 1023u) * 0.0009765625f);
            float wave = SimdTranscendentalApproximator.SinPolynomial(phase, 1f, 7);
            float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
            float turbulence = wave * turbulenceAmplitude;
            float buoyancyY = math.select(0f, Tuning.BuoyancyAccelerationY, math.isfinite(Tuning.BuoyancyAccelerationY));
            float3 acceleration = new float3(baseFlow.x + turbulence, buoyancyY, baseFlow.z - turbulence * 0.65f);
            float denominator = 1f + drag * dt;
            float3 integrated = (velocity + acceleration * dt) * math.rcp(math.max(denominator, BuoyancyDisplacementConstants.Epsilon));
            float maxSpeed = math.max(0f, math.select(0f, Tuning.MaxSpeed, math.isfinite(Tuning.MaxSpeed)));
            float speedSq = math.lengthsq(integrated);
            float maxSq = maxSpeed * maxSpeed;
            float clampMask = math.step(maxSq, speedSq) * math.step(BuoyancyDisplacementConstants.Epsilon, maxSpeed);
            float invSpeed = math.rsqrt(math.max(speedSq, BuoyancyDisplacementConstants.Epsilon));
            float3 clamped = integrated * (maxSpeed * invSpeed);
            float3 finite = math.select(integrated, clamped, clampMask > 0f);
            finite = math.select(float3.zero, finite, math.isfinite(finite));
            Velocities[index] = SimdFloat3Padded.FromFloat3(finite);
            OutputForces[index] = SimdFloat3Padded.FromFloat3((finite - velocity) * math.rcp(dt));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedHydrodynamicsLane4Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> LocalPositions;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's ParallelFor safety assumes Execute(i) writes only element i. This lane-packed job
        // writes Velocities rows [laneIndex * 4, laneIndex * 4 + 3], so the warning is a partition
        // mismatch, not a real cross-worker overlap. [NoAlias] separately proves this output is not
        // the same native allocation as LocalPositions, DragCoefficients, or OutputForces.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected one-entity Execute scheduling because it removes the Task 06 lane-4 proof. Rejected
        // a scalar tail cleanup job because it creates an extra dependency edge and lets adopters forget
        // the cleanup pass. Rejected copying to a temporary lane array because that would add bandwidth
        // and native lifetime surface without improving the mathematical contract.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Callers schedule ceil(Count / 4). Lane k owns logical rows [k * 4, min(k * 4 + 3, Count - 1)].
        // Tail reads and writes clamp to Count - 1; duplicate tail stores happen inside one Execute and
        // write the identical final velocity value, so two workers never write the same row.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [ReadOnly, NoAlias] public NativeArray<float> DragCoefficients;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // OutputForces uses the same four-row lane partition as Velocities. Unity cannot infer that
        // Execute(laneIndex) owns a closed range rather than a single index, so the ParallelFor restriction
        // is disabled only for this partitioned output. [NoAlias] keeps force output independent from
        // LocalPositions, DragCoefficients, and Velocities.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected per-row force scheduling because it would split velocity and force math into divergent
        // jobs. Rejected post-pass force reconstruction because it doubles memory traffic over the velocity
        // lane. Rejected leaving the force lane write-restricted because it fails Unity safety in editor.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The write range is identical to the velocity range: lane k owns [k * 4, min(k * 4 + 3, Count - 1)].
        // Tail duplicate stores write the same final force for the clamped last row within one Execute.
        // No other lane can compute or store that final row because lane ranges are monotonically disjoint.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<SimdFloat3Padded> OutputForces;
        public SimdHydrodynamicTuningDTO Tuning;
        public int Count;

        public void Execute(int laneIndex)
        {
            if (!LocalPositions.IsCreated ||
                !Velocities.IsCreated ||
                !DragCoefficients.IsCreated ||
                !OutputForces.IsCreated)
            {
                return;
            }

            int count = math.min(
                math.max(0, Count),
                math.min(
                    LocalPositions.Length,
                    math.min(
                        Velocities.Length,
                        math.min(DragCoefficients.Length, OutputForces.Length))));
            int baseIndex = laneIndex * SimdVectorizationConstants.HydrodynamicsLaneWidth;
            if ((uint)baseIndex >= (uint)count)
                return;
            int lastIndex = count - 1;
            int index1 = math.min(baseIndex + 1, lastIndex);
            int index2 = math.min(baseIndex + 2, lastIndex);
            int index3 = math.min(baseIndex + 3, lastIndex);

            SimdFloat3Padded p0 = LocalPositions[baseIndex];
            SimdFloat3Padded p1 = LocalPositions[index1];
            SimdFloat3Padded p2 = LocalPositions[index2];
            SimdFloat3Padded p3 = LocalPositions[index3];
            SimdFloat3Padded v0 = Velocities[baseIndex];
            SimdFloat3Padded v1 = Velocities[index1];
            SimdFloat3Padded v2 = Velocities[index2];
            SimdFloat3Padded v3 = Velocities[index3];

            float4 px = SanitizeFinite(new float4(p0.Value.x, p1.Value.x, p2.Value.x, p3.Value.x));
            float4 pz = SanitizeFinite(new float4(p0.Value.z, p1.Value.z, p2.Value.z, p3.Value.z));
            float4 vx = SanitizeFinite(new float4(v0.Value.x, v1.Value.x, v2.Value.x, v3.Value.x));
            float4 vy = SanitizeFinite(new float4(v0.Value.y, v1.Value.y, v2.Value.y, v3.Value.y));
            float4 vz = SanitizeFinite(new float4(v0.Value.z, v1.Value.z, v2.Value.z, v3.Value.z));
            float4 dragCoefficient = SanitizeFinite(new float4(
                DragCoefficients[baseIndex],
                DragCoefficients[index1],
                DragCoefficients[index2],
                DragCoefficients[index3]));

            float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
            float baseLinearDrag = math.max(0f, math.select(0f, Tuning.BaseLinearDrag, math.isfinite(Tuning.BaseLinearDrag)));
            float4 drag = math.max(new float4(0f), dragCoefficient + new float4(baseLinearDrag));
            float3 baseFlow = math.select(float3.zero, Tuning.BaseFlowVelocity, math.isfinite(Tuning.BaseFlowVelocity));
            float4 phase = (px * 0.01171875f) + (pz * 0.017578125f) + new float4((float)(Tuning.FrameIndex & 1023u) * 0.0009765625f);
            float4 wave = SimdTranscendentalApproximator.SinPolynomial(phase, 1f, 7);
            float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
            float4 turbulence = wave * new float4(turbulenceAmplitude);
            float buoyancyY = math.select(0f, Tuning.BuoyancyAccelerationY, math.isfinite(Tuning.BuoyancyAccelerationY));
            float4 ax = new float4(baseFlow.x) + turbulence;
            float4 ay = new float4(buoyancyY);
            float4 az = new float4(baseFlow.z) - turbulence * 0.65f;
            float4 denominator = new float4(1f) + drag * dt;
            float4 safeDenominator = math.max(denominator, new float4(BuoyancyDisplacementConstants.Epsilon));
            float4 invDenominator = math.rcp(safeDenominator);
            float4 ix = (vx + ax * dt) * invDenominator;
            float4 iy = (vy + ay * dt) * invDenominator;
            float4 iz = (vz + az * dt) * invDenominator;
            float maxSpeed = math.max(0f, math.select(0f, Tuning.MaxSpeed, math.isfinite(Tuning.MaxSpeed)));
            float4 speedSq = ix * ix + iy * iy + iz * iz;
            float4 maxSq = new float4(maxSpeed * maxSpeed);
            float4 clampMask = math.step(maxSq, speedSq) * math.step(new float4(BuoyancyDisplacementConstants.Epsilon), new float4(maxSpeed));
            float4 invSpeed = math.rsqrt(math.max(speedSq, new float4(BuoyancyDisplacementConstants.Epsilon)));
            float4 speedScale = new float4(maxSpeed) * invSpeed;
            float4 finiteX = math.select(ix, ix * speedScale, clampMask > new float4(0f));
            float4 finiteY = math.select(iy, iy * speedScale, clampMask > new float4(0f));
            float4 finiteZ = math.select(iz, iz * speedScale, clampMask > new float4(0f));
            bool4 finiteMask = math.isfinite(finiteX) & math.isfinite(finiteY) & math.isfinite(finiteZ);
            finiteX = math.select(new float4(0f), finiteX, finiteMask);
            finiteY = math.select(new float4(0f), finiteY, finiteMask);
            finiteZ = math.select(new float4(0f), finiteZ, finiteMask);
            float4 invDt = new float4(math.rcp(dt));

            StoreLane(baseIndex, index1, index2, index3, finiteX, finiteY, finiteZ, vx, vy, vz, invDt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreLane(
            int baseIndex,
            int index1,
            int index2,
            int index3,
            float4 x,
            float4 y,
            float4 z,
            float4 previousX,
            float4 previousY,
            float4 previousZ,
            float4 invDt)
        {
            float3 f0 = new float3(x.x, y.x, z.x);
            float3 f1 = new float3(x.y, y.y, z.y);
            float3 f2 = new float3(x.z, y.z, z.z);
            float3 f3 = new float3(x.w, y.w, z.w);
            Velocities[baseIndex] = SimdFloat3Padded.FromFloat3(f0);
            Velocities[index1] = SimdFloat3Padded.FromFloat3(f1);
            Velocities[index2] = SimdFloat3Padded.FromFloat3(f2);
            Velocities[index3] = SimdFloat3Padded.FromFloat3(f3);
            OutputForces[baseIndex] = SimdFloat3Padded.FromFloat3((f0 - new float3(previousX.x, previousY.x, previousZ.x)) * invDt.x);
            OutputForces[index1] = SimdFloat3Padded.FromFloat3((f1 - new float3(previousX.y, previousY.y, previousZ.y)) * invDt.y);
            OutputForces[index2] = SimdFloat3Padded.FromFloat3((f2 - new float3(previousX.z, previousY.z, previousZ.z)) * invDt.z);
            OutputForces[index3] = SimdFloat3Padded.FromFloat3((f3 - new float3(previousX.w, previousY.w, previousZ.w)) * invDt.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 SanitizeFinite(float4 value)
        {
            return math.select(new float4(0f), value, math.isfinite(value));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ScalarHydrodynamicsReferenceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> LocalPositions;
        [NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [ReadOnly, NoAlias] public NativeArray<float> DragCoefficients;
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> OutputForces;
        public SimdHydrodynamicTuningDTO Tuning;
        public int Count;

        public void Execute()
        {
            if (!LocalPositions.IsCreated ||
                !Velocities.IsCreated ||
                !DragCoefficients.IsCreated ||
                !OutputForces.IsCreated)
            {
                return;
            }

            int count = math.min(
                math.max(0, Count),
                math.min(
                    LocalPositions.Length,
                    math.min(
                        Velocities.Length,
                        math.min(DragCoefficients.Length, OutputForces.Length))));
            for (int index = 0; index < count; index++)
            {
                float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
                float3 rawPosition = LocalPositions[index].Value;
                float3 rawVelocity = Velocities[index].Value;
                float rawDragCoefficient = DragCoefficients[index];
                float3 position = math.select(float3.zero, rawPosition, math.isfinite(rawPosition));
                float3 velocity = math.select(float3.zero, rawVelocity, math.isfinite(rawVelocity));
                float dragCoefficient = math.select(0f, rawDragCoefficient, math.isfinite(rawDragCoefficient));
                float baseLinearDrag = math.max(0f, math.select(0f, Tuning.BaseLinearDrag, math.isfinite(Tuning.BaseLinearDrag)));
                float drag = math.max(0f, dragCoefficient + baseLinearDrag);
                float3 baseFlow = math.select(float3.zero, Tuning.BaseFlowVelocity, math.isfinite(Tuning.BaseFlowVelocity));
                float phase = (position.x * 0.01171875f) + (position.z * 0.017578125f) + ((Tuning.FrameIndex & 1023u) * 0.0009765625f);
                float wave = SimdTranscendentalApproximator.SinPolynomial(phase, 1f, 7);
                float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
                float turbulence = wave * turbulenceAmplitude;
                float buoyancyY = math.select(0f, Tuning.BuoyancyAccelerationY, math.isfinite(Tuning.BuoyancyAccelerationY));
                float3 acceleration = new float3(baseFlow.x + turbulence, buoyancyY, baseFlow.z - turbulence * 0.65f);
                float denominator = 1f + drag * dt;
                float3 integrated = (velocity + acceleration * dt) * math.rcp(math.max(denominator, BuoyancyDisplacementConstants.Epsilon));
                float maxSpeed = math.max(0f, math.select(0f, Tuning.MaxSpeed, math.isfinite(Tuning.MaxSpeed)));
                float speedSq = math.lengthsq(integrated);
                float maxSq = maxSpeed * maxSpeed;
                float clampMask = math.step(maxSq, speedSq) * math.step(BuoyancyDisplacementConstants.Epsilon, maxSpeed);
                float invSpeed = math.rsqrt(math.max(speedSq, BuoyancyDisplacementConstants.Epsilon));
                float3 clamped = integrated * (maxSpeed * invSpeed);
                float3 finite = math.select(integrated, clamped, clampMask > 0f);
                finite = math.select(float3.zero, finite, math.isfinite(finite));
                Velocities[index] = SimdFloat3Padded.FromFloat3(finite);
                OutputForces[index] = SimdFloat3Padded.FromFloat3((finite - velocity) * math.rcp(dt));
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedAupLocalizationJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<double3> AbsoluteAups;
        [WriteOnly, NoAlias] public NativeArray<SimdFloat3Padded> LocalPositions;
        public double3 OriginAup;
        public int Count;

        public void Execute(int index)
        {
            if (!AbsoluteAups.IsCreated || !LocalPositions.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(AbsoluteAups.Length, LocalPositions.Length));
            if ((uint)index >= (uint)count)
                return;

            double3 safeAup = math.select(double3.zero, AbsoluteAups[index], math.isfinite(AbsoluteAups[index]));
            double3 safeOrigin = math.select(double3.zero, OriginAup, math.isfinite(OriginAup));
            double3 delta = safeAup - safeOrigin;
            delta = math.clamp(delta, new double3(-262144.0), new double3(262144.0));
            float3 local = new float3((float)delta.x, (float)delta.y, (float)delta.z);
            LocalPositions[index] = SimdFloat3Padded.FromFloat3(local);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedSpatialQueryJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> PreyPositions;
        [WriteOnly, NoAlias] public NativeArray<int> ValidMask;
        public float3 PredatorPosition;
        public float RadiusSq;
        public int Count;

        public void Execute(int index)
        {
            if (!PreyPositions.IsCreated || !ValidMask.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(PreyPositions.Length, ValidMask.Length));
            if ((uint)index >= (uint)count)
                return;

            float3 rawPreyPosition = PreyPositions[index].Value;
            bool preyFinite = math.all(math.isfinite(rawPreyPosition));
            bool predatorFinite = math.all(math.isfinite(PredatorPosition));
            float3 preyPosition = math.select(float3.zero, rawPreyPosition, preyFinite);
            float3 predatorPosition = math.select(float3.zero, PredatorPosition, predatorFinite);
            float radiusSq = math.max(0f, math.select(0f, RadiusSq, math.isfinite(RadiusSq)));
            float3 delta = preyPosition - predatorPosition;
            float distanceSq = math.lengthsq(delta);
            bool valid = math.isfinite(distanceSq) & preyFinite & predatorFinite & distanceSq <= radiusSq;
            ValidMask[index] = math.select(0, 1, valid);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedSpatialQueryLane4Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> PreyPositions;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's ParallelFor restriction expects ValidMask[laneIndex]. This job deliberately writes four
        // contiguous mask rows per scheduled lane to expose prey distance tests as float4 math. The safety
        // warning is a false positive for the lane partition; [NoAlias] proves ValidMask is not aliased
        // with PreyPositions.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected one prey row per Execute because it fails Task 07 packed query intent. Rejected a scalar
        // tail pass because it makes correctness depend on owner scheduling discipline. Rejected an output
        // bitfield because it creates atomics or cross-lane merge work for adopters.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Callers schedule ceil(Count / 4). Lane k owns rows [k * 4, min(k * 4 + 3, Count - 1)]. Tail lanes
        // clamp duplicate indices to Count - 1 and use cascading math.select masks, so duplicate stores land
        // on the same row with the same final mask inside one Execute only.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> ValidMask;
        public float3 PredatorPosition;
        public float RadiusSq;
        public int Count;

        public void Execute(int laneIndex)
        {
            if (!PreyPositions.IsCreated || !ValidMask.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(PreyPositions.Length, ValidMask.Length));
            int baseIndex = laneIndex * SimdVectorizationConstants.SpatialQueryLaneWidth;
            if ((uint)baseIndex >= (uint)count)
                return;

            int lastIndex = count - 1;
            int index0 = baseIndex;
            int index1 = math.min(baseIndex + 1, lastIndex);
            int index2 = math.min(baseIndex + 2, lastIndex);
            int index3 = math.min(baseIndex + 3, lastIndex);
            bool lane1InRange = baseIndex + 1 < count;
            bool lane2InRange = baseIndex + 2 < count;
            bool lane3InRange = baseIndex + 3 < count;
            bool4 inRange = new bool4(true, lane1InRange, lane2InRange, lane3InRange);
            SimdFloat3Padded p0 = PreyPositions[index0];
            SimdFloat3Padded p1 = PreyPositions[index1];
            SimdFloat3Padded p2 = PreyPositions[index2];
            SimdFloat3Padded p3 = PreyPositions[index3];
            float4 px = new float4(p0.Value.x, p1.Value.x, p2.Value.x, p3.Value.x);
            float4 py = new float4(p0.Value.y, p1.Value.y, p2.Value.y, p3.Value.y);
            float4 pz = new float4(p0.Value.z, p1.Value.z, p2.Value.z, p3.Value.z);
            bool4 preyFinite = math.isfinite(px) & math.isfinite(py) & math.isfinite(pz) & inRange;
            bool predatorFinite = math.all(math.isfinite(PredatorPosition));
            float3 predator = math.select(float3.zero, PredatorPosition, predatorFinite);
            float radiusSq = math.max(0f, math.select(0f, RadiusSq, math.isfinite(RadiusSq)));
            float4 safePx = math.select(new float4(0f), px, preyFinite);
            float4 safePy = math.select(new float4(0f), py, preyFinite);
            float4 safePz = math.select(new float4(0f), pz, preyFinite);
            float4 dx = safePx - new float4(predator.x);
            float4 dy = safePy - new float4(predator.y);
            float4 dz = safePz - new float4(predator.z);
            float4 distanceSq = dx * dx + dy * dy + dz * dz;
            bool4 valid = math.isfinite(distanceSq) &
                          preyFinite &
                          new bool4(predatorFinite) &
                          (distanceSq <= new float4(radiusSq));

            int mask0 = math.select(0, 1, valid.x);
            int mask1 = math.select(mask0, math.select(0, 1, valid.y), lane1InRange);
            int mask2 = math.select(mask1, math.select(0, 1, valid.z), lane2InRange);
            int mask3 = math.select(mask2, math.select(0, 1, valid.w), lane3InRange);
            ValidMask[index0] = mask0;
            ValidMask[index1] = mask1;
            ValidMask[index2] = mask2;
            ValidMask[index3] = mask3;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedFrustumCullJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> Centers;
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> Extents;
        [ReadOnly, NoAlias] public NativeArray<float4> Planes;
        [WriteOnly, NoAlias] public NativeArray<int> VisibleIndexMask;
        public int PlaneCount;
        public int Count;

        public void Execute(int index)
        {
            if (!Centers.IsCreated || !Extents.IsCreated || !VisibleIndexMask.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(Centers.Length, math.min(Extents.Length, VisibleIndexMask.Length)));
            if ((uint)index >= (uint)count)
                return;

            float3 rawCenter = Centers[index].Value;
            float3 rawExtents = Extents[index].Value;
            float3 center = math.select(float3.zero, rawCenter, math.isfinite(rawCenter));
            float3 extents = math.max(math.select(float3.zero, rawExtents, math.isfinite(rawExtents)), new float3(0.001f));
            if (!Planes.IsCreated)
            {
                VisibleIndexMask[index] = -1;
                return;
            }

            int planeCapacity = Planes.Length;
            if (planeCapacity <= 0)
            {
                VisibleIndexMask[index] = -1;
                return;
            }

            float visible = 1f;
            int requestedPlanes = math.select(6, PlaneCount, PlaneCount > 0);
            int planeCount = math.min(math.max(0, requestedPlanes), math.min(planeCapacity, 6));
            int lastPlaneSlot = planeCapacity - 1;
            for (int i = 0; i < 6; i++)
            {
                int inRange = math.select(0, 1, i < planeCount);
                float4 rawPlane = Planes[math.min(i, lastPlaneSlot)];
                bool finitePlaneMask = math.all(math.isfinite(rawPlane));
                float4 plane = math.select(float4.zero, rawPlane, finitePlaneMask);
                float projectedRadius = math.dot(math.abs(plane.xyz), extents);
                float signedDistance = math.dot(plane.xyz, center) + plane.w;
                float planePass = math.step(0f, signedDistance + projectedRadius);
                float finitePlane = math.select(0f, 1f, finitePlaneMask);
                visible *= math.select(1f, planePass * finitePlane, inRange != 0);
            }

            bool finite = math.all(math.isfinite(rawCenter)) & math.all(math.isfinite(rawExtents));
            VisibleIndexMask[index] = math.select(-1, index, (visible > 0.5f) & finite);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VectorizedFrustumCullLane8Job : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> Centers;
        [ReadOnly, NoAlias] public NativeArray<SimdFloat3Padded> Extents;
        [ReadOnly, NoAlias] public NativeArray<float4> Planes;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Unity's ParallelFor restriction expects VisibleIndexMask[laneIndex]. This cull kernel writes eight
        // contiguous visibility rows per scheduled lane because Task 08 requires two float4 AABB groups per
        // Execute. The suppression is limited to this partition mismatch; [NoAlias] proves the output mask
        // is separate from Centers, Extents, and Planes.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Rejected scalar one-AABB scheduling because it does not prove the requested lane-8 cull surface.
        // Rejected a secondary compaction-only cull because it would duplicate plane math. Rejected renderer
        // domain integration here because SHINOBU owns reusable kernels, not BRG submission truth.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Callers schedule ceil(Count / 8). Lane k owns rows [k * 8, min(k * 8 + 7, Count - 1)]. Tail lanes
        // clamp duplicate indices to Count - 1 and use cascading math.select masks, so duplicate stores write
        // the same final visibility value inside one Execute and never overlap another worker lane.
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<int> VisibleIndexMask;
        public int PlaneCount;
        public int Count;

        public void Execute(int laneIndex)
        {
            if (!Centers.IsCreated || !Extents.IsCreated || !VisibleIndexMask.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(Centers.Length, math.min(Extents.Length, VisibleIndexMask.Length)));
            int baseIndex = laneIndex * SimdVectorizationConstants.FrustumCullLaneWidth;
            if ((uint)baseIndex >= (uint)count)
                return;

            int lastIndex = count - 1;
            int index0 = baseIndex;
            int index1 = math.min(baseIndex + 1, lastIndex);
            int index2 = math.min(baseIndex + 2, lastIndex);
            int index3 = math.min(baseIndex + 3, lastIndex);
            int index4 = math.min(baseIndex + 4, lastIndex);
            int index5 = math.min(baseIndex + 5, lastIndex);
            int index6 = math.min(baseIndex + 6, lastIndex);
            int index7 = math.min(baseIndex + 7, lastIndex);
            bool lane1InRange = baseIndex + 1 < count;
            bool lane2InRange = baseIndex + 2 < count;
            bool lane3InRange = baseIndex + 3 < count;
            bool lane4InRange = baseIndex + 4 < count;
            bool lane5InRange = baseIndex + 5 < count;
            bool lane6InRange = baseIndex + 6 < count;
            bool lane7InRange = baseIndex + 7 < count;
            bool4 inRangeA = new bool4(true, lane1InRange, lane2InRange, lane3InRange);
            bool4 inRangeB = new bool4(lane4InRange, lane5InRange, lane6InRange, lane7InRange);

            if (!Planes.IsCreated || Planes.Length <= 0)
            {
                StoreLaneMasks(
                    index0,
                    index1,
                    index2,
                    index3,
                    index4,
                    index5,
                    index6,
                    index7,
                    lane1InRange,
                    lane2InRange,
                    lane3InRange,
                    lane4InRange,
                    lane5InRange,
                    lane6InRange,
                    lane7InRange,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false);
                return;
            }

            SimdFloat3Padded center0 = Centers[index0];
            SimdFloat3Padded center1 = Centers[index1];
            SimdFloat3Padded center2 = Centers[index2];
            SimdFloat3Padded center3 = Centers[index3];
            SimdFloat3Padded center4 = Centers[index4];
            SimdFloat3Padded center5 = Centers[index5];
            SimdFloat3Padded center6 = Centers[index6];
            SimdFloat3Padded center7 = Centers[index7];
            SimdFloat3Padded extent0 = Extents[index0];
            SimdFloat3Padded extent1 = Extents[index1];
            SimdFloat3Padded extent2 = Extents[index2];
            SimdFloat3Padded extent3 = Extents[index3];
            SimdFloat3Padded extent4 = Extents[index4];
            SimdFloat3Padded extent5 = Extents[index5];
            SimdFloat3Padded extent6 = Extents[index6];
            SimdFloat3Padded extent7 = Extents[index7];

            float4 cxA = new float4(center0.Value.x, center1.Value.x, center2.Value.x, center3.Value.x);
            float4 cyA = new float4(center0.Value.y, center1.Value.y, center2.Value.y, center3.Value.y);
            float4 czA = new float4(center0.Value.z, center1.Value.z, center2.Value.z, center3.Value.z);
            float4 cxB = new float4(center4.Value.x, center5.Value.x, center6.Value.x, center7.Value.x);
            float4 cyB = new float4(center4.Value.y, center5.Value.y, center6.Value.y, center7.Value.y);
            float4 czB = new float4(center4.Value.z, center5.Value.z, center6.Value.z, center7.Value.z);
            float4 exA = new float4(extent0.Value.x, extent1.Value.x, extent2.Value.x, extent3.Value.x);
            float4 eyA = new float4(extent0.Value.y, extent1.Value.y, extent2.Value.y, extent3.Value.y);
            float4 ezA = new float4(extent0.Value.z, extent1.Value.z, extent2.Value.z, extent3.Value.z);
            float4 exB = new float4(extent4.Value.x, extent5.Value.x, extent6.Value.x, extent7.Value.x);
            float4 eyB = new float4(extent4.Value.y, extent5.Value.y, extent6.Value.y, extent7.Value.y);
            float4 ezB = new float4(extent4.Value.z, extent5.Value.z, extent6.Value.z, extent7.Value.z);

            bool4 centerFiniteA = math.isfinite(cxA) & math.isfinite(cyA) & math.isfinite(czA) & inRangeA;
            bool4 centerFiniteB = math.isfinite(cxB) & math.isfinite(cyB) & math.isfinite(czB) & inRangeB;
            bool4 extentFiniteA = math.isfinite(exA) & math.isfinite(eyA) & math.isfinite(ezA) & inRangeA;
            bool4 extentFiniteB = math.isfinite(exB) & math.isfinite(eyB) & math.isfinite(ezB) & inRangeB;
            bool4 finiteA = centerFiniteA & extentFiniteA;
            bool4 finiteB = centerFiniteB & extentFiniteB;
            cxA = math.select(new float4(0f), cxA, centerFiniteA);
            cyA = math.select(new float4(0f), cyA, centerFiniteA);
            czA = math.select(new float4(0f), czA, centerFiniteA);
            cxB = math.select(new float4(0f), cxB, centerFiniteB);
            cyB = math.select(new float4(0f), cyB, centerFiniteB);
            czB = math.select(new float4(0f), czB, centerFiniteB);
            exA = math.max(math.select(new float4(0f), exA, extentFiniteA), new float4(0.001f));
            eyA = math.max(math.select(new float4(0f), eyA, extentFiniteA), new float4(0.001f));
            ezA = math.max(math.select(new float4(0f), ezA, extentFiniteA), new float4(0.001f));
            exB = math.max(math.select(new float4(0f), exB, extentFiniteB), new float4(0.001f));
            eyB = math.max(math.select(new float4(0f), eyB, extentFiniteB), new float4(0.001f));
            ezB = math.max(math.select(new float4(0f), ezB, extentFiniteB), new float4(0.001f));

            float4 visibleA = new float4(1f);
            float4 visibleB = new float4(1f);
            int planeCapacity = Planes.Length;
            int requestedPlanes = math.select(6, PlaneCount, PlaneCount > 0);
            int planeCount = math.min(math.max(0, requestedPlanes), math.min(planeCapacity, 6));
            int lastPlaneSlot = planeCapacity - 1;
            for (int i = 0; i < 6; i++)
            {
                bool planeActive = i < planeCount;
                float4 rawPlane = Planes[math.min(i, lastPlaneSlot)];
                bool finitePlaneMask = math.all(math.isfinite(rawPlane));
                float4 plane = math.select(float4.zero, rawPlane, finitePlaneMask);
                float4 planeAbsX = new float4(math.abs(plane.x));
                float4 planeAbsY = new float4(math.abs(plane.y));
                float4 planeAbsZ = new float4(math.abs(plane.z));
                float4 planeX = new float4(plane.x);
                float4 planeY = new float4(plane.y);
                float4 planeZ = new float4(plane.z);
                float4 planeW = new float4(plane.w);
                float4 projectedRadiusA = planeAbsX * exA + planeAbsY * eyA + planeAbsZ * ezA;
                float4 projectedRadiusB = planeAbsX * exB + planeAbsY * eyB + planeAbsZ * ezB;
                float4 signedDistanceA = planeX * cxA + planeY * cyA + planeZ * czA + planeW;
                float4 signedDistanceB = planeX * cxB + planeY * cyB + planeZ * czB + planeW;
                float4 planePassA = math.step(new float4(0f), signedDistanceA + projectedRadiusA);
                float4 planePassB = math.step(new float4(0f), signedDistanceB + projectedRadiusB);
                float finitePlane = math.select(0f, 1f, finitePlaneMask);
                visibleA *= math.select(new float4(1f), planePassA * finitePlane, planeActive);
                visibleB *= math.select(new float4(1f), planePassB * finitePlane, planeActive);
            }

            bool4 validA = (visibleA > new float4(0.5f)) & finiteA;
            bool4 validB = (visibleB > new float4(0.5f)) & finiteB;
            StoreLaneMasks(
                index0,
                index1,
                index2,
                index3,
                index4,
                index5,
                index6,
                index7,
                lane1InRange,
                lane2InRange,
                lane3InRange,
                lane4InRange,
                lane5InRange,
                lane6InRange,
                lane7InRange,
                validA.x,
                validA.y,
                validA.z,
                validA.w,
                validB.x,
                validB.y,
                validB.z,
                validB.w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreLaneMasks(
            int index0,
            int index1,
            int index2,
            int index3,
            int index4,
            int index5,
            int index6,
            int index7,
            bool lane1InRange,
            bool lane2InRange,
            bool lane3InRange,
            bool lane4InRange,
            bool lane5InRange,
            bool lane6InRange,
            bool lane7InRange,
            bool valid0,
            bool valid1,
            bool valid2,
            bool valid3,
            bool valid4,
            bool valid5,
            bool valid6,
            bool valid7)
        {
            int mask0 = math.select(-1, index0, valid0);
            int mask1 = math.select(mask0, math.select(-1, index1, valid1), lane1InRange);
            int mask2 = math.select(mask1, math.select(-1, index2, valid2), lane2InRange);
            int mask3 = math.select(mask2, math.select(-1, index3, valid3), lane3InRange);
            int mask4 = math.select(mask3, math.select(-1, index4, valid4), lane4InRange);
            int mask5 = math.select(mask4, math.select(-1, index5, valid5), lane5InRange);
            int mask6 = math.select(mask5, math.select(-1, index6, valid6), lane6InRange);
            int mask7 = math.select(mask6, math.select(-1, index7, valid7), lane7InRange);
            VisibleIndexMask[index0] = mask0;
            VisibleIndexMask[index1] = mask1;
            VisibleIndexMask[index2] = mask2;
            VisibleIndexMask[index3] = mask3;
            VisibleIndexMask[index4] = mask4;
            VisibleIndexMask[index5] = mask5;
            VisibleIndexMask[index6] = mask6;
            VisibleIndexMask[index7] = mask7;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct CompactVisibleIndicesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int> VisibleIndexMask;
        [WriteOnly, NoAlias] public NativeArray<int> VisibleIndices;
        [WriteOnly, NoAlias] public NativeArray<int> VisibleCount;
        public int Count;

        public void Execute()
        {
            int maskLength = 0;
            if (VisibleIndexMask.IsCreated)
                maskLength = VisibleIndexMask.Length;

            int capacity = 0;
            if (VisibleIndices.IsCreated)
                capacity = VisibleIndices.Length;

            int count = math.min(math.max(0, Count), maskLength);
            int write = 0;
            if (capacity > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if ((uint)write >= (uint)capacity)
                        break;

                    int value = VisibleIndexMask[i];
                    bool valid = (uint)value < (uint)count;
                    // Invalid rows can overwrite the next excluded output slot. The final VisibleCount
                    // excludes that slot unless a later valid row overwrites it before count is published.
                    VisibleIndices[write] = math.select(-1, value, valid);
                    write += math.select(0, 1, valid);
                }
            }

            if (VisibleCount.IsCreated && VisibleCount.Length > 0)
                VisibleCount[0] = write;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LocalResourceDeltaJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<float> Inputs;
        [WriteOnly, NoAlias] public NativeArray<float> LocalDeltas;
        public float Scale;
        public int Count;

        public void Execute(int index)
        {
            if (!Inputs.IsCreated || !LocalDeltas.IsCreated)
                return;

            int count = math.min(math.max(0, Count), math.min(Inputs.Length, LocalDeltas.Length));
            if ((uint)index >= (uint)count)
                return;

            float rawInput = Inputs[index];
            float input = math.select(0f, rawInput, math.isfinite(rawInput));
            float scale = math.select(0f, Scale, math.isfinite(Scale));
            float delta = input * scale;
            LocalDeltas[index] = math.select(0f, delta, math.isfinite(delta));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ReduceResourceDeltaJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float> LocalDeltas;
        [WriteOnly, NoAlias] public NativeArray<float> Output;
        public int Count;

        public void Execute()
        {
            if (!LocalDeltas.IsCreated)
                return;

            int count = math.min(math.max(0, Count), LocalDeltas.Length);
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float value = math.select(0f, LocalDeltas[i], math.isfinite(LocalDeltas[i]));
                float next = sum + value;
                sum = math.select(sum, next, math.isfinite(next));
            }

            if (Output.IsCreated && Output.Length > 0)
                Output[0] = math.select(0f, sum, math.isfinite(sum));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordSimdTelemetryJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<SimdTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint FrameIndex;
        public uint KernelHash;
        public int EntityCount;
        public float VectorMicros;
        public float ScalarMicros;
        public float GlobalQualityWeight;
        public uint StateHash;
        public float MaxApproximationError;
        public float MaxSpeedSq;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0 || !TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return;

            int cursor = math.max(0, TelemetryCursor[0]);
            int slot = cursor % TelemetryRing.Length;
            float vectorMicros = math.max(0f, math.select(0f, VectorMicros, math.isfinite(VectorMicros)));
            float scalar = math.max(0f, math.select(0f, ScalarMicros, math.isfinite(ScalarMicros)));
            float vectorMs = math.max(0.0001f, vectorMicros * 0.001f);
            float rawDrop = math.saturate(1f - (scalar * math.rcp(math.max(vectorMicros, 0.0001f))));
            float drop = math.select(0f, rawDrop, (scalar > 0.0001f) & math.isfinite(rawDrop));
            float throughput = math.max(0, EntityCount) * math.rcp(vectorMs);
            bool nonFiniteTelemetry = !math.isfinite(VectorMicros) |
                                      !math.isfinite(ScalarMicros) |
                                      !math.isfinite(GlobalQualityWeight) |
                                      !math.isfinite(MaxApproximationError) |
                                      !math.isfinite(MaxSpeedSq) |
                                      !math.isfinite(throughput) |
                                      !math.isfinite(drop);
            SimdTelemetryEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.KernelHash = math.select(SimdVectorizationConstants.HydrodynamicsKernelHash, KernelHash, KernelHash != 0u);
            entry.EntityCount = math.max(0, EntityCount);
            entry.VectorMicros = vectorMicros;
            entry.ScalarMicros = scalar;
            entry.EntitiesPerMillisecond = math.select(0f, throughput, math.isfinite(throughput));
            entry.ThroughputDrop01 = drop;
            entry.GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            entry.Flags = math.select(0u, SimdVectorizationConstants.FlagNonFinite, nonFiniteTelemetry);
            entry.LastStateHash = math.select(1u, StateHash, StateHash != 0u);
            entry.MaxError = math.max(0f, math.select(0f, MaxApproximationError, math.isfinite(MaxApproximationError)));
            entry.MaxSpeedSq = math.max(0f, math.select(0f, MaxSpeedSq, math.isfinite(MaxSpeedSq)));
            TelemetryRing[slot] = entry;
            int nextCursor = slot + 1;
            TelemetryCursor[0] = math.select(nextCursor, 0, nextCursor >= TelemetryRing.Length);
        }
    }

    public static class SimdTranscendentalApproximator
    {
        private const float TwoPi = 6.28318530718f;
        private const float InvTwoPi = 0.15915494309f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial(float radians)
        {
            return SinPolynomial(radians, 1f, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinPolynomial(float radians, float qualityWeight, int polynomialDegree)
        {
            float safeRadians = math.select(0f, radians, math.isfinite(radians));
            float x = safeRadians - math.floor((safeRadians + math.PI) * InvTwoPi) * TwoPi;
            x = math.select(x, math.PI - x, x > 1.57079632679f);
            x = math.select(x, -math.PI - x, x < -1.57079632679f);
            float x2 = x * x;
            float p3 = x * (1f + x2 * -0.16666667f);
            float p5 = x * (1f + x2 * (-0.16666667f + x2 * 0.008333331f));
            float p7 = x * (1f + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.00019840874f)));
            float q = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
            int degree = math.clamp(polynomialDegree, 3, 7);
            float midWeight = SmoothStep01((q - 0.25f) * math.rcp(0.4f)) * math.step(5f, degree);
            float highWeight = SmoothStep01((q - 0.6f) * math.rcp(0.35f)) * math.step(7f, degree);
            return math.lerp(math.lerp(p3, p5, midWeight), p7, highWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 SinPolynomial(float4 radians, float qualityWeight, int polynomialDegree)
        {
            float4 safeRadians = math.select(new float4(0f), radians, math.isfinite(radians));
            float4 x = safeRadians - math.floor((safeRadians + new float4(math.PI)) * InvTwoPi) * TwoPi;
            x = math.select(x, new float4(math.PI) - x, x > new float4(1.57079632679f));
            x = math.select(x, new float4(-math.PI) - x, x < new float4(-1.57079632679f));
            float4 x2 = x * x;
            float4 p3 = x * (new float4(1f) + x2 * -0.16666667f);
            float4 p5 = x * (new float4(1f) + x2 * (-0.16666667f + x2 * 0.008333331f));
            float4 p7 = x * (new float4(1f) + x2 * (-0.16666667f + x2 * (0.008333331f + x2 * -0.00019840874f)));
            float q = math.saturate(math.select(1f, qualityWeight, math.isfinite(qualityWeight)));
            int degree = math.clamp(polynomialDegree, 3, 7);
            float midWeight = SmoothStep01((q - 0.25f) * math.rcp(0.4f)) * math.step(5f, degree);
            float highWeight = SmoothStep01((q - 0.6f) * math.rcp(0.35f)) * math.step(7f, degree);
            return math.lerp(math.lerp(p3, p5, new float4(midWeight)), p7, new float4(highWeight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial(float radians)
        {
            return SinPolynomial(radians + 1.57079632679f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosPolynomial(float radians, float qualityWeight, int polynomialDegree)
        {
            return SinPolynomial(radians + 1.57079632679f, qualityWeight, polynomialDegree);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ExpNegPolynomial01(float value)
        {
            float x = math.saturate(math.select(0f, value, math.isfinite(value)));
            float x2 = x * x;
            float approx = 1f - x + x2 * 0.5f - x2 * x * 0.16666667f + x2 * x2 * 0.04166667f;
            return math.saturate(approx);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }
    }

    #if UNITY_EDITOR
    public static class SimdToleranceCsvParser
    {
        public static bool TryApply(ReadOnlySpan<byte> csv, NativeArray<SimdMathToleranceDTO> output, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || !output.IsCreated || output.Length <= 0)
                return false;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out SimdMathToleranceDTO row) &&
                    rowsWritten < output.Length)
                {
                    output[rowsWritten++] = row;
                }
            }

            return rowsWritten > 0;
        }

        public static bool TryApply(ReadOnlySpan<byte> csv, Span<SimdMathToleranceDTO> output, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || output.Length <= 0)
                return false;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int cursor = 0;
            while (cursor < csv.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out SimdMathToleranceDTO row) &&
                    rowsWritten < output.Length)
                {
                    output[rowsWritten++] = row;
                }
            }

            return rowsWritten > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out SimdMathToleranceDTO row)
        {
            row = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int comma0 = IndexOf(line, (byte)',', 0);
            int comma1 = IndexOf(line, (byte)',', comma0 + 1);
            if (comma0 <= 0 || comma1 <= comma0)
                return false;

            ReadOnlySpan<byte> name = Trim(line.Slice(0, comma0));
            ReadOnlySpan<byte> degreeBytes = Trim(line.Slice(comma0 + 1, comma1 - comma0 - 1));
            ReadOnlySpan<byte> errorBytes = Trim(line.Slice(comma1 + 1));
            if (!TryParseInt(degreeBytes, out int degree) || !TryParseFloat(errorBytes, out float error))
                return false;

            row.FormulaHash = Fnv1A32(name);
            row.PolynomialDegree = math.clamp(degree, 1, 9);
            row.MaxError = math.max(0f, math.select(0f, error, math.isfinite(error)));
            row.Flags = SimdVectorizationConstants.FlagActive;
            return row.FormulaHash != 0u;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> value, byte target, int start)
        {
            for (int i = math.max(0, start); i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool TryParseInt(ReadOnlySpan<byte> value, out int parsed)
        {
            parsed = 0;
            value = Trim(value);
            if (value.Length <= 0)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                parsed = parsed * 10 + c - (byte)'0';
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> value, out float parsed)
        {
            parsed = 0f;
            value = Trim(value);
            if (value.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = integer * 10f + value[index] - (byte)'0';
                index++;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction += (value[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            parsed = sign * (integer + fraction);
            return hasDigit && index == value.Length && math.isfinite(parsed);
        }

        private static uint Fnv1A32(ReadOnlySpan<byte> span)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return math.select(1u, hash, hash != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }
    }
    #endif
}
