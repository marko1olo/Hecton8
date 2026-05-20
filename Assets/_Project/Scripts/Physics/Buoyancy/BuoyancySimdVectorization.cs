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
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
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
        [FieldOffset(60)] public uint _pad0;
    }

    public static class SimdVectorizationConstants
    {
        public const int HydrodynamicsLaneWidth = 4;
        public const int SpatialQueryLaneWidth = 4;
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
        public const string SimdAgentDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_201.bin";
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
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry._pad0)) == 48 &&
                   OffsetOf<SimdTelemetryEntry>(nameof(SimdTelemetryEntry._pad1)) == 56;
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
                   OffsetOf<SimdHydrodynamicTuningDTO>(nameof(SimdHydrodynamicTuningDTO._pad0)) == 60;
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
            if (fieldName == nameof(SimdTelemetryEntry._pad0)) return 48;
            if (fieldName == nameof(SimdTelemetryEntry._pad1)) return 56;
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
            if (fieldName == nameof(SimdHydrodynamicTuningDTO._pad0)) return 60;
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

            float q = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight)));
            bool hasApproximationWeight = math.isfinite(Tuning.ApproximationQualityWeight) &&
                                           Tuning.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            float approximationWeight = math.saturate(math.select(q, Tuning.ApproximationQualityWeight, hasApproximationWeight));
            float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
            int sinDegree = math.clamp(Tuning.SinPolynomialDegree, 3, 7);
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
            float wave = SimdTranscendentalApproximator.SinPolynomial(phase, approximationWeight, sinDegree);
            float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
            float turbulence = wave * turbulenceAmplitude * q;
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
        // ParallelFor invariant: one scheduled lane owns [laneIndex * 4, laneIndex * 4 + 3].
        // Schedule count is rounded down to Count / 4, so lane ranges are injective and non-overlapping.
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<SimdFloat3Padded> Velocities;
        [ReadOnly, NoAlias] public NativeArray<float> DragCoefficients;
        // Same partition as Velocities; every lane overwrites its four force rows exactly once.
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
            int vectorizedCount = count & ~(SimdVectorizationConstants.HydrodynamicsLaneWidth - 1);
            int baseIndex = laneIndex * SimdVectorizationConstants.HydrodynamicsLaneWidth;
            if ((uint)baseIndex >= (uint)vectorizedCount)
                return;

            SimdFloat3Padded p0 = LocalPositions[baseIndex];
            SimdFloat3Padded p1 = LocalPositions[baseIndex + 1];
            SimdFloat3Padded p2 = LocalPositions[baseIndex + 2];
            SimdFloat3Padded p3 = LocalPositions[baseIndex + 3];
            SimdFloat3Padded v0 = Velocities[baseIndex];
            SimdFloat3Padded v1 = Velocities[baseIndex + 1];
            SimdFloat3Padded v2 = Velocities[baseIndex + 2];
            SimdFloat3Padded v3 = Velocities[baseIndex + 3];

            float4 px = SanitizeFinite(new float4(p0.Value.x, p1.Value.x, p2.Value.x, p3.Value.x));
            float4 pz = SanitizeFinite(new float4(p0.Value.z, p1.Value.z, p2.Value.z, p3.Value.z));
            float4 vx = SanitizeFinite(new float4(v0.Value.x, v1.Value.x, v2.Value.x, v3.Value.x));
            float4 vy = SanitizeFinite(new float4(v0.Value.y, v1.Value.y, v2.Value.y, v3.Value.y));
            float4 vz = SanitizeFinite(new float4(v0.Value.z, v1.Value.z, v2.Value.z, v3.Value.z));
            float4 dragCoefficient = SanitizeFinite(new float4(
                DragCoefficients[baseIndex],
                DragCoefficients[baseIndex + 1],
                DragCoefficients[baseIndex + 2],
                DragCoefficients[baseIndex + 3]));

            float q = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight)));
            bool hasApproximationWeight = math.isfinite(Tuning.ApproximationQualityWeight) &&
                                           Tuning.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
            float approximationWeight = math.saturate(math.select(q, Tuning.ApproximationQualityWeight, hasApproximationWeight));
            float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
            int sinDegree = math.clamp(Tuning.SinPolynomialDegree, 3, 7);
            float baseLinearDrag = math.max(0f, math.select(0f, Tuning.BaseLinearDrag, math.isfinite(Tuning.BaseLinearDrag)));
            float4 drag = math.max(new float4(0f), dragCoefficient + new float4(baseLinearDrag));
            float3 baseFlow = math.select(float3.zero, Tuning.BaseFlowVelocity, math.isfinite(Tuning.BaseFlowVelocity));
            float4 phase = (px * 0.01171875f) + (pz * 0.017578125f) + new float4((float)(Tuning.FrameIndex & 1023u) * 0.0009765625f);
            float4 wave = SimdTranscendentalApproximator.SinPolynomial(phase, approximationWeight, sinDegree);
            float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
            float4 turbulence = wave * new float4(turbulenceAmplitude * q);
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

            StoreLane(baseIndex, finiteX, finiteY, finiteZ, vx, vy, vz, invDt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreLane(int baseIndex, float4 x, float4 y, float4 z, float4 previousX, float4 previousY, float4 previousZ, float4 invDt)
        {
            float3 f0 = new float3(x.x, y.x, z.x);
            float3 f1 = new float3(x.y, y.y, z.y);
            float3 f2 = new float3(x.z, y.z, z.z);
            float3 f3 = new float3(x.w, y.w, z.w);
            Velocities[baseIndex] = SimdFloat3Padded.FromFloat3(f0);
            Velocities[baseIndex + 1] = SimdFloat3Padded.FromFloat3(f1);
            Velocities[baseIndex + 2] = SimdFloat3Padded.FromFloat3(f2);
            Velocities[baseIndex + 3] = SimdFloat3Padded.FromFloat3(f3);
            OutputForces[baseIndex] = SimdFloat3Padded.FromFloat3((f0 - new float3(previousX.x, previousY.x, previousZ.x)) * invDt.x);
            OutputForces[baseIndex + 1] = SimdFloat3Padded.FromFloat3((f1 - new float3(previousX.y, previousY.y, previousZ.y)) * invDt.y);
            OutputForces[baseIndex + 2] = SimdFloat3Padded.FromFloat3((f2 - new float3(previousX.z, previousY.z, previousZ.z)) * invDt.z);
            OutputForces[baseIndex + 3] = SimdFloat3Padded.FromFloat3((f3 - new float3(previousX.w, previousY.w, previousZ.w)) * invDt.w);
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
                float q = math.saturate(math.select(1f, Tuning.GlobalQualityWeight, math.isfinite(Tuning.GlobalQualityWeight)));
                bool hasApproximationWeight = math.isfinite(Tuning.ApproximationQualityWeight) &&
                                               Tuning.ApproximationQualityWeight > BuoyancyDisplacementConstants.Epsilon;
                float approximationWeight = math.saturate(math.select(q, Tuning.ApproximationQualityWeight, hasApproximationWeight));
                float dt = math.clamp(math.select(1f / 60f, Tuning.DeltaTime, math.isfinite(Tuning.DeltaTime)), 0.0001f, 0.1f);
                int sinDegree = math.clamp(Tuning.SinPolynomialDegree, 3, 7);
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
                float wave = SimdTranscendentalApproximator.SinPolynomial(phase, approximationWeight, sinDegree);
                float turbulenceAmplitude = math.max(0f, math.select(0f, Tuning.TurbulenceAmplitude, math.isfinite(Tuning.TurbulenceAmplitude)));
                float turbulence = wave * turbulenceAmplitude * q;
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
        // ParallelFor invariant: one scheduled lane owns [laneIndex * 4, laneIndex * 4 + 3].
        // Callers schedule ceil(Count / 4); tail lanes duplicate-store the last in-range row with the same final mask.
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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
                float4 plane = Planes[math.min(i, lastPlaneSlot)];
                float projectedRadius = math.dot(math.abs(plane.xyz), extents);
                float signedDistance = math.dot(plane.xyz, center) + plane.w;
                float planePass = math.step(0f, signedDistance + projectedRadius);
                float finitePlane = math.select(0f, 1f, math.all(math.isfinite(plane)));
                visible *= math.select(1f, planePass * finitePlane, inRange != 0);
            }

            bool finite = math.all(math.isfinite(rawCenter)) & math.all(math.isfinite(rawExtents));
            VisibleIndexMask[index] = math.select(-1, index, (visible > 0.5f) & finite);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CompactVisibleIndicesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int> VisibleIndexMask;
        [NoAlias] public NativeArray<int> VisibleIndices;
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
                int lastSlot = capacity - 1;
                for (int i = 0; i < count; i++)
                {
                    int value = VisibleIndexMask[i];
                    bool valid = (value >= 0) & (write < capacity);
                    int slot = math.min(write, lastSlot);
                    int preserved = VisibleIndices[slot];
                    VisibleIndices[slot] = math.select(preserved, value, valid);
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
            entry.MaxError = 0f;
            entry.MaxSpeedSq = math.max(0f, math.select(0f, MaxSpeedSq, math.isfinite(MaxSpeedSq)));
            TelemetryRing[slot] = entry;
            TelemetryCursor[0] = cursor + 1;
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
            row.MaxError = math.max(0f, error);
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
}
