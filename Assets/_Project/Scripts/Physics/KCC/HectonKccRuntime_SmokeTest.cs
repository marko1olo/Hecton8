using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.KCC
{
    public sealed partial class HydrodynamicKccRuntime
    {
        public const int KccSmokeDefaultPhantomCount = 100;
        public const int KccSmokeDefaultFrameCount = 10000;
        public const int KccSmokeTelemetryFrames = 300;
        public const int KccSmokeSdfDimX = 48;
        public const int KccSmokeSdfDimY = 48;
        public const int KccSmokeSdfDimZ = 48;
        public const int KccSmokeSdfCellCount = KccSmokeSdfDimX * KccSmokeSdfDimY * KccSmokeSdfDimZ;
        public const int KccSmokeMaxFailureRecords = 512;
        public const int KccSmokeMaxSweepIterations = 8;
        public const int KccSmokeRollbackWindowFrames = 30;
        public const int KccSmokeRebaseIntervalFrames = 500;
        public const float KccSmokeFixedDeltaTime = 0.016666667f;
        public const float KccSmokeStrongPenetrationMeters = -0.1f;
        public const float KccSmokeInvalidSdfMeters = -4096f;
        public const uint KccSmokeFailureNone = 0u;
        public const uint KccSmokeFailureNonFinite = 1u;
        public const uint KccSmokeFailureEscape = 1u << 1;
        public const uint KccSmokeFailurePerformance = 1u << 2;
        public const uint KccSmokeFailurePrecisionDrift = 1u << 3;
        public const uint KccSmokeFailureRollbackDesync = 1u << 4;
        public const uint KccSmokeFailureAllocation = 1u << 5;
        public const uint KccSmokeFailureSdfInvalid = 1u << 6;
        public const uint KccSmokeFailureInputSanitized = 1u << 7;
        public const uint KccSmokeFailureLayout = 1u << 8;
        public const uint KccSmokeSourceHash = 0x53483355u;
        public const uint ReplayDeterminismFailureDrift = 12u;
        public const float ReplayDeterminismEpsilonMeters = 0.000001f;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct KccSmokeTestStateDTO
        {
            [FieldOffset(0)] public double3 TestPlayerAUP;
            [FieldOffset(24)] public uint CurrentFrameCount;
            [FieldOffset(28)] public uint MismatchFlags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct KccSmokeProfileDTO
        {
            [FieldOffset(0)] public double3 StartAup;
            [FieldOffset(24)] public float3 StartVelocity;
            [FieldOffset(36)] public float3 InputBias;
            [FieldOffset(48)] public float SpeedScale;
            [FieldOffset(52)] public uint ProfileHash;
            [FieldOffset(56)] public uint Flags;
            [FieldOffset(60)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct KccSmokeVoxelSdfInfoDTO
        {
            [FieldOffset(0)] public double3 OriginAup;
            [FieldOffset(24)] public int3 Dimensions;
            [FieldOffset(36)] public float CellSizeMeters;
            [FieldOffset(40)] public float SurfaceOffsetMeters;
            [FieldOffset(44)] public float CapsuleRadiusMeters;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint ProfileHash;
            [FieldOffset(56)] public uint Frame;
            [FieldOffset(60)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct KccSmokeTestResultDTO
        {
            [FieldOffset(0)] public uint ErrorFlags;
            [FieldOffset(4)] public uint FailureCount;
            [FieldOffset(8)] public uint FirstFailureFrame;
            [FieldOffset(12)] public uint FirstFailureIndex;
            [FieldOffset(16)] public double3 FirstFailureAup;
            [FieldOffset(40)] public float3 FirstFailureVelocity;
            [FieldOffset(52)] public float WorstPenetrationMeters;
            [FieldOffset(56)] public float AverageMicrosecondsPerFrame;
            [FieldOffset(60)] public uint StateHash;
            [FieldOffset(64)] public uint RollbackMismatchFrame;
            [FieldOffset(68)] public uint RollbackMismatchIndex;
            [FieldOffset(72)] public double DriftErrorMillimeters;
            [FieldOffset(80)] public float MaxDriftMillimeters;
            [FieldOffset(84)] public uint RollbackHashA;
            [FieldOffset(88)] public uint RollbackHashB;
            [FieldOffset(92)] public uint SuccessfulRollbackCount;
            [FieldOffset(96)] public uint RebaseCount;
            [FieldOffset(100)] public uint MockDesyncFrame;
            [FieldOffset(104)] public ulong _pad0;
            [FieldOffset(112)] public ulong _pad1;
            [FieldOffset(120)] public ulong _pad2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct KccSmokeFailureRecordDTO
        {
            [FieldOffset(0)] public double3 Aup;
            [FieldOffset(24)] public float3 Velocity;
            [FieldOffset(36)] public float SdfMeters;
            [FieldOffset(40)] public uint Frame;
            [FieldOffset(44)] public uint EntityIndex;
            [FieldOffset(48)] public uint FailureFlags;
            [FieldOffset(52)] public uint StateHash;
            [FieldOffset(56)] public double3 PreviousAup;
            [FieldOffset(80)] public float3 InputVector;
            [FieldOffset(92)] public float SpeedMetersPerSecond;
            [FieldOffset(96)] public ulong _pad0;
            [FieldOffset(104)] public ulong _pad1;
            [FieldOffset(112)] public ulong _pad2;
            [FieldOffset(120)] public ulong _pad3;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct KccSmokeTelemetryEntry
        {
            [FieldOffset(0)] public double3 FirstAup;
            [FieldOffset(24)] public float HighestPenetrationDepth;
            [FieldOffset(28)] public float AverageDriftMillimeters;
            [FieldOffset(32)] public float MaxSpeed;
            [FieldOffset(36)] public float BurstExecutionMicroseconds;
            [FieldOffset(40)] public uint Frame;
            [FieldOffset(44)] public uint StateHash;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint SuccessfulRollbacks;
            [FieldOffset(56)] public ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        public struct KccSmokeDriftProbeDTO
        {
            [FieldOffset(0)] public double3 StartAup;
            [FieldOffset(24)] public double3 CurrentAup;
            [FieldOffset(48)] public double StepMeters;
            [FieldOffset(56)] public uint LastFrame;
            [FieldOffset(60)] public uint Flags;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct GenerateMockTestGeometryJob : IJob
        {
            [WriteOnly, NoAlias] public NativeArray<float> Sdf;
            public KccSmokeVoxelSdfInfoDTO Info;

            public void Execute()
            {
                if (!Sdf.IsCreated)
                    return;

                int3 dim = Info.Dimensions;
                int cellsPerLayer = math.max(1, dim.x * dim.y);
                for (int index = 0; index < Sdf.Length; index++)
                {
                    int z = index / cellsPerLayer;
                    int layer = index - z * cellsPerLayer;
                    int y = layer / math.max(1, dim.x);
                    int x = layer - y * dim.x;
                    float3 center = (new float3(x, y, z) - (new float3(dim.x, dim.y, dim.z) - 1f) * 0.5f) * Info.CellSizeMeters;
                    float radial = HydrodynamicKccMath.LengthSafe(center);
                    float shell = math.abs(radial - 66f) - 4.5f;
                    float wedgeA = math.max(center.y + 82f, center.x * 0.42f - center.z * 0.18f - 13f);
                    float wedgeB = math.max(86f - center.y, -center.x * 0.31f - center.z * 0.27f - 11f);
                    float crevice = math.abs(center.x * 0.74f + center.z * 0.19f) - 2.4f;
                    float columnA = math.sqrt(math.max(0f, math.lengthsq(center.xz - new float2(22f, -18f)))) - 5f;
                    float columnB = math.sqrt(math.max(0f, math.lengthsq(center.xz - new float2(-26f, 30f)))) - 7f;
                    float coneRadius = math.max(0.35f, (70f - center.y) * 0.22f);
                    float voxelCone = math.max(math.sqrt(math.max(0f, math.lengthsq(center.xz))) - coneRadius, center.y - 70f);
                    float jagged = (Noise3(center * 0.071f) * 2f - 1f) * 1.75f;
                    float solid = math.min(shell + jagged, math.min(wedgeA, wedgeB));
                    solid = math.min(solid, math.min(columnA, columnB));
                    solid = math.max(solid, -crevice);
                    solid = math.min(solid, voxelCone);
                    Sdf[index] = math.clamp(solid, -64f, 64f);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Noise3(float3 p)
            {
                float n = MathLodApproximation.ApproxSinBhaskara(math.dot(p, new float3(12.9898f, 78.233f, 37.719f))) * 43758.5453f;
                return n - math.floor(n);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct InitializeSmokePhantomsJob : IJobParallelFor
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // Each job index writes one KinematicStateDTO row and one smoke-state row. Unity cannot prove the
            // pointer row write because the DTO is mutated through UnsafeUtility.AsRef to match the KCC runtime
            // mutation style used by the production integration jobs.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Writing through NativeArray indexer was considered, but the batch mandate explicitly requires
            // ref-style mutation to prevent hidden defensive copies. A managed fixture object was rejected
            // because it would make the headless test scene-dependent.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: schedule count is clamped by the caller to States.Length and SmokeStates.Length.
            // Profiles are read-only, and no other scheduled job writes these rows until this initializer handle
            // completes.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestStateDTO> SmokeStates;
            [ReadOnly, NoAlias] public NativeArray<KccSmokeProfileDTO> Profiles;
            public int ProfileCount;
            public double3 SectorOriginAup;
            public HydrodynamicKccTuningDTO Tuning;

            public void Execute(int index)
            {
                int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
                byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
                ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);
                KccSmokeProfileDTO profile = ResolveProfile(index);
                state = new KinematicStateDTO
                {
                    AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(profile.StartAup),
                    Velocity = HydrodynamicKccMath.Sanitize(profile.StartVelocity, float3.zero),
                    AngularVelocity = float3.zero,
                    Mass = 80f + (index % 7) * 3.5f,
                    Flags = 0u,
                    DragCoefficient = math.max(0f, Tuning.BaseDrag),
                    RestingFrameCount = 0,
                    DeepSleepTickCount = 0,
                    SleepMaterialIndex = 0,
                    _pad0 = 0
                };

                if (SmokeStates.IsCreated && index < SmokeStates.Length)
                {
                    SmokeStates[index] = new KccSmokeTestStateDTO
                    {
                        TestPlayerAUP = state.AUP_Position,
                        CurrentFrameCount = 0u,
                        MismatchFlags = 0u
                    };
                }
            }

            private KccSmokeProfileDTO ResolveProfile(int index)
            {
                int profileLimit = Profiles.IsCreated ? math.min(math.max(0, ProfileCount), Profiles.Length) : 0;
                if ((uint)index < (uint)profileLimit)
                    return Profiles[index];

                float laneX = ((index % 10) - 4.5f) * 3.6f;
                float laneZ = (((index / 10) % 10) - 4.5f) * 3.6f;
                float laneY = ((index % 5) - 2) * 4f;
                laneY = math.select(laneY, 74f, (index % 17) == 0);
                laneY = math.select(laneY, -74f, (index % 19) == 0);
                double3 start = SectorOriginAup + new double3(laneX, laneY, laneZ);
                if (index == 0)
                    start = new double3(99000.0, -1500.0, 99000.0);

                float phase = index * 0.6180339f;
                float3 velocity = new float3(
                    HydrodynamicKccMath.SinPolynomial7(phase) * 640f,
                    HydrodynamicKccMath.SinPolynomial7(phase * 1.7f) * 180f,
                    HydrodynamicKccMath.SinPolynomial7(phase * 2.3f + 1.1f) * 640f);
                if (index == 1)
                {
                    start = SectorOriginAup + new double3(0d, 82d, 0d);
                    velocity = new float3(0f, -100f, 0f);
                }

                return new KccSmokeProfileDTO
                {
                    StartAup = start,
                    StartVelocity = velocity,
                    InputBias = HydrodynamicKccMath.NormalizeSafe(velocity, new float3(0f, 0f, 1f)),
                    SpeedScale = 1f,
                    ProfileHash = KccSmokeSourceHash,
                    Flags = 1u
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public unsafe struct EvaluateHeadlessKccFrameLoopJob : IJob
        {
            // SAFETY_JUSTIFICATION_PARAGRAPH_1:
            // The frame-loop job mutates KCC states, smoke states, result counters, rollback ring, and position
            // history in one single-owner IJob. Unity safety cannot express that all mutable arrays are owned
            // exclusively by this scheduled job for the full 10,000-frame background pass.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_2:
            // Splitting every frame into many small jobs was rejected because it creates same-frame schedule/readback
            // pressure and hides determinism issues behind scheduler order. Managed collections were rejected because
            // the history and rollback state must be flat native memory.
            //
            // SAFETY_JUSTIFICATION_PARAGRAPH_3:
            // Invariant: callers schedule this job only after geometry/init jobs complete and schedule validation jobs
            // only after this handle. The arrays are sized before scheduling; this job never grows or disposes them.
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float3> ProposedVelocities;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
            [ReadOnly, NoAlias] public NativeArray<float> Sdf;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<double3> PositionHistory;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> RollbackStateRing;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestStateDTO> SmokeStates;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestResultDTO> Results;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeFailureRecordDTO> Failures;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTelemetryEntry> Telemetry;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeDriftProbeDTO> DriftProbe;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<DesyncDetectedSignal> MockDesyncSignals;
            public KccSmokeVoxelSdfInfoDTO SdfInfo;
            public HydrodynamicKccTuningDTO Tuning;
            public double3 SectorOriginAup;
            public int EntityCount;
            public int FrameCount;
            public float SimulationTickDelta;
            public uint Seed;

            public void Execute()
            {
                if (!States.IsCreated || !Results.IsCreated || Results.Length == 0)
                    return;

                int count = math.clamp(EntityCount, 0, States.Length);
                int frames = math.max(0, FrameCount);
                int rollbackStride = math.max(1, KccSmokeRollbackWindowFrames + 1);
                KccSmokeTestResultDTO result = Results[0];
                result.WorstPenetrationMeters = 9999f;
                Tuning = SanitizeSmokeTuning(Tuning);
                SdfInfo = SanitizeSmokeSdfInfo(SdfInfo);
                if (!TryResolveSdfLayout(Sdf, SdfInfo, out _, out _, out _))
                    result.ErrorFlags |= KccSmokeFailureSdfInvalid;
                double3 localOriginAup = SectorOriginAup;

                WriteRollbackRing(0, count, rollbackStride);
                for (int frame = 1; frame <= frames; frame++)
                {
                    if ((frame % KccSmokeRebaseIntervalFrames) == 0)
                    {
                        localOriginAup += new double3(5000.0d, 0.0d, 5000.0d);
                        result.RebaseCount++;
                    }

                    ExecutePreSimulation(frame, count);
                    ExecuteSimulation(frame, count, localOriginAup, ref result);
                    ExecutePostSimulation(frame, count, ref result);
                    ExecuteRollbackProbe(frame, count, rollbackStride, ref result);
                    AdvanceDriftProbe(frame, ref result);
                    WriteRollbackRing(frame, count, rollbackStride);
                    if ((result.ErrorFlags & (KccSmokeFailureNonFinite | KccSmokeFailureRollbackDesync)) != 0u)
                    {
                        FillRemainingHistory(frame + 1, frames, count);
                        break;
                    }
                }

                Results[0] = result;
            }

            public static HydrodynamicKccInputDTO BuildHostileInput(int index, uint frame, double3 anchorAup, uint sectorGeneration, uint seed)
            {
                uint state = HydrodynamicKccMath.SeedNonZero(seed ^ ((uint)index * 0x9E3779B9u) ^ (frame * 0x85EBCA6Bu));
                float t = (float)frame * KccSmokeFixedDeltaTime;
                float3 axis = new float3(
                    HydrodynamicKccMath.SinPolynomial7(t * 13.1f + index * 0.37f),
                    HydrodynamicKccMath.SinPolynomial7(t * 5.7f + index * 0.11f) * 0.25f,
                    HydrodynamicKccMath.SinPolynomial7(t * 17.9f + index * 0.53f));
                bool zeroInjection = ((frame + (uint)index) % 97u) == 0u;
                bool infinityInjection = ((frame * 31u + (uint)index * 7u) % 997u) == 0u;
                axis = math.select(axis, float3.zero, zeroInjection);
                axis = math.select(axis, new float3(float.PositiveInfinity, 0f, axis.z), infinityInjection);
                return new HydrodynamicKccInputDTO
                {
                    TargetAup = anchorAup,
                    MoveAxis = axis,
                    LookAxis = new float3(axis.x, 0f, 1f),
                    SimulationFrame = frame,
                    Sequence = (uint)index,
                    Flags = HydrodynamicKccMath.PackInputFlags(HydrodynamicKccMath.FlagMockInput, sectorGeneration),
                    SourceHash = state
                };
            }

            private void ExecutePreSimulation(int frame, int count)
            {
                uint sectorGeneration = HydrodynamicKccMath.ComputeSectorGeneration(SectorOriginAup);
                for (int i = 0; i < count; i++)
                {
                    HydrodynamicKccInputDTO input = BuildHostileInput(i, (uint)frame, SectorOriginAup, sectorGeneration, Seed);
                    bool inputFinite = HydrodynamicKccMath.IsFinite(input.MoveAxis) && HydrodynamicKccMath.IsFinite(input.LookAxis);
                    if (!inputFinite)
                    {
                        input.MoveAxis = float3.zero;
                        input.LookAxis = new float3(0f, 0f, 1f);
                        input.Flags |= KccSmokeFailureInputSanitized;
                    }

                    float moveLenSq = math.lengthsq(input.MoveAxis);
                    if (moveLenSq > 1f)
                        input.MoveAxis *= math.rsqrt(math.max(moveLenSq, 0.000001f));
                    input.LookAxis = HydrodynamicKccMath.NormalizeSafe(input.LookAxis, new float3(0f, 0f, 1f));
                    if (Inputs.IsCreated && i < Inputs.Length)
                        Inputs[i] = input;
                    if (FaultFlags.IsCreated && i < FaultFlags.Length)
                        FaultFlags[i] = default;
                }
            }

            private void ExecuteSimulation(int frame, int count, double3 localOriginAup, ref KccSmokeTestResultDTO result)
            {
                int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
                byte* statesBase = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
                float dt = math.max(HydrodynamicKccMath.MinDenominator, SimulationTickDelta);
                for (int i = 0; i < count; i++)
                {
                    ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesBase + (i * stateSize));
                    double3 previous = state.AUP_Position;
                    HydrodynamicKccInputDTO input = Inputs.IsCreated && i < Inputs.Length ? Inputs[i] : default;
                    StepKcc(ref state, input, frame, i, dt, out float minSdf, out uint collisionFlags);
                    state.Flags |= collisionFlags;

                    if (ProposedVelocities.IsCreated && i < ProposedVelocities.Length)
                        ProposedVelocities[i] = state.Velocity;

                    uint failureFlags = 0u;
                    if (!HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(state.Velocity))
                        failureFlags |= KccSmokeFailureNonFinite;
                    float finalSdf = SampleCapsuleSdf(state.AUP_Position);
                    bool invalidSdf = IsInvalidSdf(finalSdf);
                    if (invalidSdf)
                        failureFlags |= KccSmokeFailureEscape | KccSmokeFailureSdfInvalid;
                    else if (finalSdf < KccSmokeStrongPenetrationMeters)
                        failureFlags |= KccSmokeFailureEscape;

                    if (SmokeStates.IsCreated && i < SmokeStates.Length)
                    {
                        SmokeStates[i] = new KccSmokeTestStateDTO
                        {
                            TestPlayerAUP = state.AUP_Position,
                            CurrentFrameCount = (uint)frame,
                            MismatchFlags = failureFlags
                        };
                    }

                    if (failureFlags != 0u)
                    {
                        if (FaultFlags.IsCreated && i < FaultFlags.Length)
                        {
                            HydrodynamicKccFaultFlagDTO fault = FaultFlags[i];
                            fault.FaultMask |= (int)failureFlags;
                            FaultFlags[i] = fault;
                        }

                        RecordFailure(ref result, (uint)frame, (uint)i, failureFlags, previous, state.AUP_Position, state.Velocity, input.MoveAxis, finalSdf);
                    }

                    int historyIndex = ((frame - 1) * count) + i;
                    if (PositionHistory.IsCreated && (uint)historyIndex < (uint)PositionHistory.Length)
                        PositionHistory[historyIndex] = state.AUP_Position;

                    float3 local = HydrodynamicKccMath.ResolveLocalFloat3(state.AUP_Position, localOriginAup);
                    if (!HydrodynamicKccMath.IsFinite(local))
                        RecordFailure(ref result, (uint)frame, (uint)i, KccSmokeFailureNonFinite, previous, state.AUP_Position, state.Velocity, input.MoveAxis, minSdf);
                }
            }

            private void ExecutePostSimulation(int frame, int count, ref KccSmokeTestResultDTO result)
            {
                float maxSpeed = 0f;
                float minSdf = 9999f;
                float driftSum = 0f;
                uint flags = 0u;
                uint hash = 2166136261u;
                for (int i = 0; i < count; i++)
                {
                    KinematicStateDTO state = States[i];
                    float speed = HydrodynamicKccMath.LengthSafe(state.Velocity);
                    float sdf = SampleCapsuleSdf(state.AUP_Position);
                    maxSpeed = math.max(maxSpeed, speed);
                    minSdf = math.min(minSdf, sdf);
                    flags |= state.Flags;
                    uint entityHash = HydrodynamicKccMath.HashState(state.AUP_Position, state.Velocity, (uint)frame, state.Flags);
                    entityHash ^= (uint)i * 0x9E3779B9u;
                    hash += entityHash;
                    if (i == 0)
                        driftSum = (float)math.abs(States[i].AUP_Position.x - math.round(States[i].AUP_Position.x * 1000.0d) * 0.001d) * 1000f;
                }

                if (Telemetry.IsCreated && Telemetry.Length > 0)
                {
                    int ringIndex = frame % Telemetry.Length;
                    Telemetry[ringIndex] = new KccSmokeTelemetryEntry
                    {
                        FirstAup = count > 0 ? States[0].AUP_Position : double3.zero,
                        HighestPenetrationDepth = math.max(0f, -minSdf),
                        AverageDriftMillimeters = driftSum,
                        MaxSpeed = maxSpeed,
                        BurstExecutionMicroseconds = EstimateSmokeMicroseconds(count, maxSpeed),
                        Frame = (uint)frame,
                        StateHash = hash,
                        Flags = flags,
                        SuccessfulRollbacks = result.SuccessfulRollbackCount
                    };
                }

                result.StateHash = hash;
                result.WorstPenetrationMeters = math.min(result.WorstPenetrationMeters, minSdf);
                result.MaxDriftMillimeters = math.max(result.MaxDriftMillimeters, driftSum);
            }

            private void ExecuteRollbackProbe(int frame, int count, int rollbackStride, ref KccSmokeTestResultDTO result)
            {
                if (frame <= KccSmokeRollbackWindowFrames ||
                    count <= 0 ||
                    !RollbackStateRing.IsCreated ||
                    RollbackStateRing.Length < rollbackStride * count ||
                    !PositionHistory.IsCreated)
                {
                    return;
                }

                if ((frame % 211) != 0)
                    return;

                int entityIndex = (int)(HydrodynamicKccMath.SeedNonZero((uint)frame ^ Seed) % (uint)count);
                int startFrame = frame - KccSmokeRollbackWindowFrames;
                int ringFrame = startFrame % rollbackStride;
                KinematicStateDTO replayA = RollbackStateRing[ringFrame * count + entityIndex];
                KinematicStateDTO replayB = replayA;
                KinematicStateDTO mutatedReplayA = replayA;
                KinematicStateDTO mutatedReplayB = replayA;
                float dt = math.max(HydrodynamicKccMath.MinDenominator, SimulationTickDelta);
                int mutationFrame = startFrame + (KccSmokeRollbackWindowFrames >> 1);
                for (int f = startFrame + 1; f <= frame; f++)
                {
                    HydrodynamicKccInputDTO input = BuildHostileInput(entityIndex, (uint)f, SectorOriginAup, HydrodynamicKccMath.ComputeSectorGeneration(SectorOriginAup), Seed);
                    SanitizeRollbackInput(ref input);
                    StepKcc(ref replayA, input, f, entityIndex, dt, out _, out uint replayCollisionA);
                    replayA.Flags |= replayCollisionA;
                    StepKcc(ref replayB, input, f, entityIndex, dt, out _, out uint replayCollisionB);
                    replayB.Flags |= replayCollisionB;

                    HydrodynamicKccInputDTO modifiedInput = input;
                    if (f == mutationFrame)
                        modifiedInput.MoveAxis = HydrodynamicKccMath.NormalizeSafe(new float3(-input.MoveAxis.z, input.MoveAxis.y + 0.125f, input.MoveAxis.x), input.MoveAxis);
                    StepKcc(ref mutatedReplayA, modifiedInput, f, entityIndex, dt, out _, out uint mutatedCollisionA);
                    mutatedReplayA.Flags |= mutatedCollisionA;
                    StepKcc(ref mutatedReplayB, modifiedInput, f, entityIndex, dt, out _, out uint mutatedCollisionB);
                    mutatedReplayB.Flags |= mutatedCollisionB;
                }

                uint hashA = HydrodynamicKccMath.HashState(replayA.AUP_Position, replayA.Velocity, (uint)frame, replayA.Flags);
                uint hashB = HydrodynamicKccMath.HashState(replayB.AUP_Position, replayB.Velocity, (uint)frame, replayB.Flags);
                uint mutatedHashA = HydrodynamicKccMath.HashState(mutatedReplayA.AUP_Position, mutatedReplayA.Velocity, (uint)frame, mutatedReplayA.Flags);
                uint mutatedHashB = HydrodynamicKccMath.HashState(mutatedReplayB.AUP_Position, mutatedReplayB.Velocity, (uint)frame, mutatedReplayB.Flags);
                int historyIndex = ((frame - 1) * count) + entityIndex;
                double3 originalAup = (uint)historyIndex < (uint)PositionHistory.Length ? PositionHistory[historyIndex] : replayA.AUP_Position;
                bool branchMismatch = hashA != hashB;
                bool mutatedBranchMismatch = mutatedHashA != mutatedHashB;
                KinematicStateDTO originalState = States[entityIndex];
                bool originalMismatch = !AupBitwiseEqual(replayA.AUP_Position, originalAup) ||
                                        !ReplayStateMatches(replayA, originalState);
                if (branchMismatch || mutatedBranchMismatch || originalMismatch)
                {
                    result.ErrorFlags |= KccSmokeFailureRollbackDesync;
                    result.RollbackMismatchFrame = (uint)frame;
                    result.RollbackMismatchIndex = (uint)entityIndex;
                    result.RollbackHashA = branchMismatch ? hashA : mutatedHashA;
                    result.RollbackHashB = branchMismatch ? hashB : mutatedHashB;
                    RecordMockDesyncSignal(frame, entityIndex, hashA, hashB, originalMismatch);
                }
                else
                {
                    result.SuccessfulRollbackCount++;
                }
            }

            private void AdvanceDriftProbe(int frame, ref KccSmokeTestResultDTO result)
            {
                if (!DriftProbe.IsCreated || DriftProbe.Length == 0)
                    return;

                KccSmokeDriftProbeDTO drift = DriftProbe[0];
                drift.CurrentAup.x = HydrodynamicKccMath.QuantizeMillimeter(new double3(drift.CurrentAup.x + drift.StepMeters, 0d, 0d)).x;
                drift.LastFrame = (uint)frame;
                DriftProbe[0] = drift;
                double expected = drift.StartAup.x + drift.StepMeters * frame;
                double errorMm = math.abs(drift.CurrentAup.x - expected) * 1000.0d;
                result.DriftErrorMillimeters = math.max(result.DriftErrorMillimeters, errorMm);
                if (errorMm > 1.0d)
                    result.ErrorFlags |= KccSmokeFailurePrecisionDrift;
            }

            private void WriteRollbackRing(int frame, int count, int rollbackStride)
            {
                if (!RollbackStateRing.IsCreated || RollbackStateRing.Length < rollbackStride * count)
                    return;

                int ringFrame = frame % rollbackStride;
                int baseIndex = ringFrame * count;
                for (int i = 0; i < count; i++)
                    RollbackStateRing[baseIndex + i] = States[i];
            }

            private void FillRemainingHistory(int startFrame, int frames, int count)
            {
                if (!PositionHistory.IsCreated || startFrame > frames)
                    return;

                for (int frame = startFrame; frame <= frames; frame++)
                {
                    int frameBase = (frame - 1) * count;
                    for (int i = 0; i < count; i++)
                    {
                        int index = frameBase + i;
                        if ((uint)index < (uint)PositionHistory.Length)
                            PositionHistory[index] = States[i].AUP_Position;
                    }
                }
            }

            private void StepKcc(ref KinematicStateDTO state, HydrodynamicKccInputDTO input, int frame, int index, float dt, out float minSdf, out uint collisionFlags)
            {
                double3 previous = state.AUP_Position;
                float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
                float3 move = HydrodynamicKccMath.Sanitize(input.MoveAxis, float3.zero);
                float quality = math.saturate(math.isfinite(Tuning.GlobalQualityWeight) ? Tuning.GlobalQualityWeight : 1f);
                float drive = math.lerp(220f, 620f, quality);
                velocity += move * drive * dt;
                velocity += HostileCurrent(frame, index) * dt;

                float speed = HydrodynamicKccMath.LengthSafe(velocity);
                float drag = math.max(0f, Tuning.BaseDrag);
                velocity *= math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * speed * dt));
                float maxSpeed = math.max(10f, Tuning.MaxSpeed);
                float speedSq = math.lengthsq(velocity);
                if (speedSq > maxSpeed * maxSpeed)
                    velocity *= maxSpeed * math.rsqrt(math.max(speedSq, 0.000001f));

                double3 resolvedAup = ResolveSweptAup(previous, ref velocity, dt, out minSdf, out collisionFlags);
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(resolvedAup);
                state.Velocity = HydrodynamicKccMath.Sanitize(velocity, float3.zero);
                state.AngularVelocity = HydrodynamicKccMath.Sanitize(state.AngularVelocity, float3.zero);
            }

            private double3 ResolveSweptAup(double3 start, ref float3 velocity, float dt, out float minSdf, out uint collisionFlags)
            {
                collisionFlags = 0u;
                float skin = math.max(0.001f, math.isfinite(Tuning.SkinWidth) ? Tuning.SkinWidth : 0.02f);
                float3 displacement = velocity * dt;
                minSdf = SampleCapsuleSdf(start);
                if (IsInvalidSdf(minSdf))
                {
                    velocity = float3.zero;
                    collisionFlags = HydrodynamicKccMath.FlagCollision;
                    return start;
                }

                // Already inside solid at the frame start: depenetrate before sweeping
                // (matches production collision residual push + SdfSqueeze open-space recovery).
                double3 resolved = start;
                if (minSdf < skin)
                {
                    resolved = DepenetrateAup(start, skin, out minSdf, out bool depenValid);
                    collisionFlags = HydrodynamicKccMath.FlagCollision;
                    if (!depenValid)
                    {
                        velocity = float3.zero;
                        return start;
                    }

                    float3 startNormal = SampleSdfNormal(resolved);
                    float intoStart = math.dot(velocity, startNormal);
                    if (intoStart < 0f)
                        velocity -= startNormal * intoStart;
                    displacement = velocity * dt;
                }

                float length = HydrodynamicKccMath.LengthSafe(displacement);
                if (length <= 0.000001f)
                    return resolved;

                // Adaptive sweep count: high-speed frames need finer steps so thin shells
                // (mock geometry ~2-9m features) are not tunneled at 950 m/s.
                int sweepSteps = KccSmokeMaxSweepIterations;
                float cell = math.max(0.25f, SdfInfo.CellSizeMeters);
                float stepBudget = math.max(skin, cell * 0.5f);
                int adaptive = (int)math.ceil(length / math.max(0.05f, stepBudget));
                sweepSteps = math.clamp(math.max(sweepSteps, adaptive), KccSmokeMaxSweepIterations, 64);

                double3 safe = resolved;
                double3 hit = resolved;
                bool hasHit = false;
                for (int step = 1; step <= sweepSteps; step++)
                {
                    float fraction = (float)step * math.rcp((float)sweepSteps);
                    double3 candidate = resolved + new double3(displacement.x, displacement.y, displacement.z) * fraction;
                    float sdf = SampleCapsuleSdf(candidate);
                    minSdf = math.min(minSdf, sdf);
                    if (IsInvalidSdf(sdf))
                    {
                        velocity = float3.zero;
                        collisionFlags = HydrodynamicKccMath.FlagCollision;
                        return DepenetrateAup(safe, skin, out minSdf, out _);
                    }

                    if (sdf >= skin)
                    {
                        safe = candidate;
                        continue;
                    }

                    hasHit = true;
                    hit = candidate;
                    break;
                }

                if (!hasHit)
                    return resolved + new double3(displacement.x, displacement.y, displacement.z);

                float3 normal = SampleSdfNormal(hit);
                float intoNormal = math.dot(velocity, normal);
                if (intoNormal < 0f)
                    velocity -= normal * intoNormal;

                // Slide residual motion along the contact plane from the last free sample,
                // then iteratively depenetrate (production-style multi-pass residual push).
                float hitFraction = HydrodynamicKccMath.LengthSafe(
                    new float3((float)(hit.x - resolved.x), (float)(hit.y - resolved.y), (float)(hit.z - resolved.z)));
                float consumed = math.saturate(hitFraction * math.rcp(math.max(length, HydrodynamicKccMath.MinDenominator)));
                float remainingDt = dt * math.max(0f, 1f - consumed);
                double3 slid = safe + new double3(velocity.x, velocity.y, velocity.z) * remainingDt;
                double3 recovered = DepenetrateAup(slid, skin, out float recoveredSdf, out bool recoveredValid);
                minSdf = math.min(minSdf, recoveredSdf);
                collisionFlags = HydrodynamicKccMath.FlagCollision;
                if (!recoveredValid)
                {
                    velocity = float3.zero;
                    return safe;
                }

                return recovered;
            }

            private double3 DepenetrateAup(double3 aup, float skin, out float sdfMeters, out bool valid)
            {
                const int maxIterations = 8;
                double3 position = aup;
                sdfMeters = SampleCapsuleSdf(position);
                valid = !IsInvalidSdf(sdfMeters);
                if (!valid)
                    return aup;

                float radius = math.max(0.05f, SdfInfo.CapsuleRadiusMeters);
                float maxPush = math.max(radius * 4f, math.max(1f, SdfInfo.CellSizeMeters) * 2f);
                for (int i = 0; i < maxIterations; i++)
                {
                    if (sdfMeters >= skin)
                        return position;

                    float3 normal = SampleSdfNormal(position);
                    float push = math.min(maxPush, math.max(skin - sdfMeters, 0f) + skin * 0.25f);
                    if (push <= 0.000001f)
                        break;

                    position = position + new double3(normal.x, normal.y, normal.z) * push;
                    position = HydrodynamicKccMath.QuantizeMillimeter(position);
                    sdfMeters = SampleCapsuleSdf(position);
                    if (IsInvalidSdf(sdfMeters))
                    {
                        valid = false;
                        return aup;
                    }
                }

                // Final safety: if still deeply buried after iterations, snap along gradient harder.
                if (sdfMeters < KccSmokeStrongPenetrationMeters)
                {
                    float3 normal = SampleSdfNormal(position);
                    float emergency = math.min(maxPush * 2f, math.max(0f, skin - sdfMeters) + radius);
                    position = position + new double3(normal.x, normal.y, normal.z) * emergency;
                    position = HydrodynamicKccMath.QuantizeMillimeter(position);
                    sdfMeters = SampleCapsuleSdf(position);
                    if (IsInvalidSdf(sdfMeters))
                    {
                        valid = false;
                        return aup;
                    }
                }

                return position;
            }


            private float SampleCapsuleSdf(double3 aup)
            {
                return SampleSdf(aup) - math.max(0.05f, SdfInfo.CapsuleRadiusMeters);
            }

            private float3 SampleSdfNormal(double3 aup)
            {
                double cell = math.max(0.25f, SdfInfo.CellSizeMeters);
                float xp = SampleSdf(aup + new double3(cell, 0d, 0d));
                float xn = SampleSdf(aup - new double3(cell, 0d, 0d));
                float yp = SampleSdf(aup + new double3(0d, cell, 0d));
                float yn = SampleSdf(aup - new double3(0d, cell, 0d));
                float zp = SampleSdf(aup + new double3(0d, 0d, cell));
                float zn = SampleSdf(aup - new double3(0d, 0d, cell));
                if (IsInvalidSdf(xp) || IsInvalidSdf(xn) || IsInvalidSdf(yp) || IsInvalidSdf(yn) || IsInvalidSdf(zp) || IsInvalidSdf(zn))
                    return new float3(0f, 1f, 0f);

                float dx = xp - xn;
                float dy = yp - yn;
                float dz = zp - zn;
                return HydrodynamicKccMath.NormalizeSafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
            }

            private float SampleSdf(double3 aup)
            {
                return SampleSdfStatic(Sdf, SdfInfo, aup);
            }

            private static float3 HostileCurrent(int frame, int index)
            {
                float t = frame * KccSmokeFixedDeltaTime;
                return new float3(
                    HydrodynamicKccMath.SinPolynomial7(t * 3.1f + index) * 280f,
                    HydrodynamicKccMath.SinPolynomial7(t * 1.7f + index * 0.13f) * 60f,
                    HydrodynamicKccMath.SinPolynomial7(t * 2.3f - index) * 280f);
            }

            private static void SanitizeRollbackInput(ref HydrodynamicKccInputDTO input)
            {
                if (!HydrodynamicKccMath.IsFinite(input.MoveAxis))
                    input.MoveAxis = float3.zero;
                if (!HydrodynamicKccMath.IsFinite(input.LookAxis))
                    input.LookAxis = new float3(0f, 0f, 1f);
                float moveLenSq = math.lengthsq(input.MoveAxis);
                if (moveLenSq > 1f)
                    input.MoveAxis *= math.rsqrt(math.max(moveLenSq, 0.000001f));
            }

            private static bool AupBitwiseEqual(double3 a, double3 b)
            {
                return math.aslong(a.x) == math.aslong(b.x) &&
                       math.aslong(a.y) == math.aslong(b.y) &&
                       math.aslong(a.z) == math.aslong(b.z);
            }

            private static bool ReplayStateMatches(KinematicStateDTO replay, KinematicStateDTO original)
            {
                return AupBitwiseEqual(replay.AUP_Position, original.AUP_Position) &&
                       math.asuint(replay.Velocity.x) == math.asuint(original.Velocity.x) &&
                       math.asuint(replay.Velocity.y) == math.asuint(original.Velocity.y) &&
                       math.asuint(replay.Velocity.z) == math.asuint(original.Velocity.z) &&
                       replay.Flags == original.Flags;
            }

            private void RecordMockDesyncSignal(int frame, int entityIndex, uint hashA, uint hashB, bool originalMismatch)
            {
                if (!MockDesyncSignals.IsCreated || MockDesyncSignals.Length == 0)
                    return;

                MockDesyncSignals[0] = new DesyncDetectedSignal
                {
                    LocalHash = hashA,
                    AuthoritativeHash = hashB,
                    Frame = (uint)frame,
                    SourceId = KccSmokeSourceHash,
                    LastFenceFrame = (uint)math.max(0, frame - KccSmokeRollbackWindowFrames),
                    Flags = (byte)(originalMismatch ? 1 : 2),
                    Reserved2 = (uint)entityIndex
                };
            }

            private void RecordFailure(
                ref KccSmokeTestResultDTO result,
                uint frame,
                uint index,
                uint failureFlags,
                double3 previousAup,
                double3 aup,
                float3 velocity,
                float3 input,
                float sdfMeters)
            {
                if (result.FailureCount == 0u)
                {
                    result.FirstFailureFrame = frame;
                    result.FirstFailureIndex = index;
                    result.FirstFailureAup = aup;
                    result.FirstFailureVelocity = velocity;
                }

                result.ErrorFlags |= failureFlags;
                uint slot = result.FailureCount;
                result.FailureCount++;
                if (!Failures.IsCreated || slot >= Failures.Length)
                    return;

                Failures[(int)slot] = new KccSmokeFailureRecordDTO
                {
                    Aup = aup,
                    Velocity = velocity,
                    SdfMeters = sdfMeters,
                    Frame = frame,
                    EntityIndex = index,
                    FailureFlags = failureFlags,
                    StateHash = HydrodynamicKccMath.HashState(aup, velocity, frame, failureFlags),
                    PreviousAup = previousAup,
                    InputVector = input,
                    SpeedMetersPerSecond = HydrodynamicKccMath.LengthSafe(velocity)
                };
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct VerifyCollisionEscapeJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<double3> PositionHistory;
            [ReadOnly, NoAlias] public NativeArray<float> Sdf;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestResultDTO> Results;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeFailureRecordDTO> Failures;
            public KccSmokeVoxelSdfInfoDTO SdfInfo;
            public int EntityCount;
            public int FrameCount;

            public void Execute()
            {
                if (!Results.IsCreated || Results.Length == 0 || !PositionHistory.IsCreated)
                    return;

                KccSmokeTestResultDTO result = Results[0];
                KccSmokeVoxelSdfInfoDTO safeInfo = SanitizeSmokeSdfInfo(SdfInfo);
                int count = math.max(0, EntityCount);
                int frames = math.max(0, FrameCount);
                for (int frame = 1; frame <= frames; frame++)
                {
                    for (int entity = 0; entity < count; entity++)
                    {
                        int index = ((frame - 1) * count) + entity;
                        if ((uint)index >= (uint)PositionHistory.Length)
                            continue;

                        double3 aup = PositionHistory[index];
                        float rawSdf = SampleSdfStatic(Sdf, safeInfo, aup);
                        bool invalidSdf = IsInvalidSdf(rawSdf);
                        float sdf = rawSdf - math.max(0.05f, safeInfo.CapsuleRadiusMeters);
                        if (!invalidSdf && sdf >= KccSmokeStrongPenetrationMeters)
                            continue;

                        uint failureFlags = KccSmokeFailureEscape | (invalidSdf ? KccSmokeFailureSdfInvalid : 0u);
                        result.ErrorFlags |= failureFlags;
                        if (result.FailureCount == 0u)
                        {
                            result.FirstFailureFrame = (uint)frame;
                            result.FirstFailureIndex = (uint)entity;
                            result.FirstFailureAup = aup;
                        }

                        uint slot = result.FailureCount;
                        result.FailureCount++;
                        if (Failures.IsCreated && slot < Failures.Length)
                        {
                            Failures[(int)slot] = new KccSmokeFailureRecordDTO
                            {
                                Aup = aup,
                                SdfMeters = sdf,
                                Frame = (uint)frame,
                                EntityIndex = (uint)entity,
                                FailureFlags = failureFlags,
                                StateHash = HydrodynamicKccMath.HashState(aup, float3.zero, (uint)frame, failureFlags)
                            };
                        }

                        Results[0] = result;
                        return;
                    }
                }

                Results[0] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct AnalyzePrecisionDriftJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<KccSmokeDriftProbeDTO> DriftProbe;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestResultDTO> Results;
            public int FrameCount;

            public void Execute()
            {
                if (!Results.IsCreated || Results.Length == 0 || !DriftProbe.IsCreated || DriftProbe.Length == 0)
                    return;

                KccSmokeTestResultDTO result = Results[0];
                KccSmokeDriftProbeDTO drift = DriftProbe[0];
                double expected = drift.StartAup.x + drift.StepMeters * math.max(0, FrameCount);
                double errorMm = math.abs(drift.CurrentAup.x - expected) * 1000.0d;
                result.DriftErrorMillimeters = math.max(result.DriftErrorMillimeters, errorMm);
                result.MaxDriftMillimeters = math.max(result.MaxDriftMillimeters, (float)math.min(errorMm, (double)float.MaxValue));
                if (errorMm > 1.0d)
                    result.ErrorFlags |= KccSmokeFailurePrecisionDrift;
                Results[0] = result;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct ValidateReplayDeterminismJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<ReplayFrameDTO> Frames;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<MemoryStateTelemetryEntry> Telemetry;
            [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KccSmokeTestResultDTO> Results;
            public KinematicStateDTO InitialState;
            public HydrodynamicKccTuningDTO Tuning;
            public float ReplayEpsilonMeters;
            public float InjectVelocityErrorMetersPerSecond;
            public int FrameCount;

            public void Execute()
            {
                if (!Results.IsCreated || Results.Length == 0)
                    return;

                KccSmokeTestResultDTO result = Results[0];
                if (!Frames.IsCreated || Frames.Length == 0)
                {
                    result.ErrorFlags |= KccSmokeFailureAllocation;
                    Results[0] = result;
                    return;
                }

                HydrodynamicKccTuningDTO tuning = SanitizeSmokeTuning(Tuning);
                KinematicStateDTO state = InitialState;
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(HydrodynamicKccMath.Sanitize(state.AUP_Position, double3.zero));
                state.Velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
                int limit = math.min(math.max(0, FrameCount), Frames.Length);
                float epsilonValue = math.select(ReplayDeterminismEpsilonMeters, ReplayEpsilonMeters, math.isfinite(ReplayEpsilonMeters));
                float epsilon = math.max(0.0000001f, epsilonValue);
                uint hash = KccSmokeSourceHash ^ 0xA6D5F00Du;
                float maxDriftMeters = 0f;

                for (int frameIndex = 0; frameIndex < limit; frameIndex++)
                {
                    ReplayFrameDTO frame = Frames[frameIndex];
                    float dtValue = math.select(KccSmokeFixedDeltaTime, frame.DeltaTime, math.isfinite(frame.DeltaTime));
                    float dt = math.max(HydrodynamicKccMath.MinDenominator, dtValue);
                    float injectedError = math.select(0f, InjectVelocityErrorMetersPerSecond, frameIndex == 0 && math.isfinite(InjectVelocityErrorMetersPerSecond));
                    float3 velocity = IntegrateReplayVelocity(state.Velocity, frame.InputMoveAxis, tuning, dt, injectedError);
                    double3 deltaAup = default;
                    deltaAup.x = velocity.x * dt;
                    deltaAup.y = velocity.y * dt;
                    deltaAup.z = velocity.z * dt;
                    double3 predictedAup = HydrodynamicKccMath.QuantizeMillimeter(state.AUP_Position + deltaAup);
                    double3 recordedAup = HydrodynamicKccMath.Sanitize(frame.RecordedAup, predictedAup);
                    double3 driftVector = predictedAup - recordedAup;
                    double driftMeters64 = math.cmax(math.abs(driftVector));
                    float driftMeters = (float)math.min(driftMeters64, (double)float.MaxValue);
                    maxDriftMeters = math.max(maxDriftMeters, driftMeters);
                    uint frameHash = HydrodynamicKccMath.HashState(predictedAup, velocity, frame.Frame, frame.InputFlags);
                    hash ^= frameHash + 0x9E3779B9u + (hash << 6) + (hash >> 2);
                    bool invalid = !HydrodynamicKccMath.IsFinite(predictedAup) || !HydrodynamicKccMath.IsFinite(velocity) || !math.isfinite(driftMeters);
                    bool drifted = driftMeters >= epsilon;
                    uint failureCode = math.select(0u, ReplayDeterminismFailureDrift, drifted);
                    failureCode = math.select(failureCode, KccSmokeFailureNonFinite, invalid);
                    WriteReplayTelemetry(Telemetry, frameIndex, predictedAup, velocity, driftMeters, frameHash, failureCode, frame.InputFlags);

                    if (invalid || drifted)
                    {
                        result.ErrorFlags |= math.select(KccSmokeFailurePrecisionDrift, KccSmokeFailureNonFinite, invalid);
                        result.FailureCount++;
                        if (result.FirstFailureFrame == 0u)
                        {
                            result.FirstFailureFrame = frame.Frame;
                            result.FirstFailureIndex = (uint)frameIndex;
                            result.FirstFailureAup = predictedAup;
                            result.FirstFailureVelocity = velocity;
                        }

                        result.MaxDriftMillimeters = math.max(result.MaxDriftMillimeters, driftMeters * 1000f);
                        result.DriftErrorMillimeters = math.max(result.DriftErrorMillimeters, (double)driftMeters * 1000.0d);
                        result.StateHash = hash;
                        Results[0] = result;
                        return;
                    }

                    state.AUP_Position = predictedAup;
                    state.Velocity = velocity;
                }

                result.MaxDriftMillimeters = math.max(result.MaxDriftMillimeters, maxDriftMeters * 1000f);
                result.StateHash = hash;
                Results[0] = result;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float3 IntegrateReplayVelocity(
                float3 currentVelocity,
                float3 inputMoveAxis,
                HydrodynamicKccTuningDTO tuning,
                float dt,
                float injectedErrorMetersPerSecond)
            {
                float qualityValue = math.select(1f, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight));
                float quality = math.saturate(qualityValue);
                float drive = math.lerp(220f, 620f, quality);
                float3 move = HydrodynamicKccMath.Sanitize(inputMoveAxis, float3.zero);
                float3 velocity = HydrodynamicKccMath.Sanitize(currentVelocity, float3.zero) + move * drive * dt;
                velocity.x += injectedErrorMetersPerSecond;
                float speed = HydrodynamicKccMath.LengthSafe(velocity);
                float drag = math.max(0f, tuning.BaseDrag) * math.lerp(0.35f, 1f, quality);
                velocity *= math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * speed * dt));
                float maxSpeed = math.max(1f, tuning.MaxSpeed);
                float speedSq = math.lengthsq(velocity);
                float maxSpeedSq = maxSpeed * maxSpeed;
                float clampScale = maxSpeed * math.rsqrt(math.max(speedSq, HydrodynamicKccMath.MinDenominator));
                velocity = math.select(velocity, velocity * clampScale, speedSq > maxSpeedSq);
                return HydrodynamicKccMath.Sanitize(velocity, float3.zero);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void WriteReplayTelemetry(
                NativeArray<MemoryStateTelemetryEntry> telemetry,
                int frameIndex,
                double3 aup,
                float3 velocity,
                float driftMeters,
                uint stateHash,
                uint failureCode,
                uint flags)
            {
                if (!telemetry.IsCreated || telemetry.Length == 0)
                    return;

                int ringIndex = frameIndex % telemetry.Length;
                MemoryStateTelemetryEntry entry = default;
                entry.Aup = aup;
                entry.Velocity = velocity;
                entry.DriftMeters = driftMeters;
                entry.Frame = (uint)frameIndex;
                entry.FailureCode = failureCode;
                entry.StateHash = stateHash;
                entry.Flags = flags;
                telemetry[ringIndex] = entry;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleSdfStatic(NativeArray<float> sdf, KccSmokeVoxelSdfInfoDTO info, double3 aup)
        {
            if (!TryResolveSdfLayout(sdf, info, out int3 dim, out float cell, out _))
                return KccSmokeInvalidSdfMeters;

            double3 rel = HydrodynamicKccMath.Sanitize(aup - info.OriginAup, double3.zero);
            float3 grid = new float3((float)(rel.x / cell), (float)(rel.y / cell), (float)(rel.z / cell));
            if (!HydrodynamicKccMath.IsFinite(grid))
                return KccSmokeInvalidSdfMeters;

            // Finite streaming volumes only store a local brick. Outside the brick is open water, not a
            // collision failure: extend the edge sample by Euclidean exterior distance (standard SDF
            // domain extension). SdfInvalid is reserved for NaN/layout faults, not leaving the brick.
            // Interior trilinear needs a unit cube, so clamp the query base into [0, dim-2].
            float3 maxBase = new float3(dim.x - 2, dim.y - 2, dim.z - 2);
            maxBase = math.max(maxBase, float3.zero);
            float3 clampedGrid = math.clamp(grid, float3.zero, maxBase);
            float3 gridDelta = grid - clampedGrid;
            float exteriorCells = HydrodynamicKccMath.LengthSafe(gridDelta);
            float exteriorMeters = exteriorCells * cell;

            int3 p0 = (int3)math.floor(clampedGrid);
            p0 = math.clamp(p0, int3.zero, new int3(dim.x - 2, dim.y - 2, dim.z - 2));
            float3 f = math.saturate(clampedGrid - p0);
            int x1 = p0.x + 1;
            int y1 = p0.y + 1;
            int z1 = p0.z + 1;
            float c000 = sdf[Index(p0.x, p0.y, p0.z, dim)];
            float c100 = sdf[Index(x1, p0.y, p0.z, dim)];
            float c010 = sdf[Index(p0.x, y1, p0.z, dim)];
            float c110 = sdf[Index(x1, y1, p0.z, dim)];
            float c001 = sdf[Index(p0.x, p0.y, z1, dim)];
            float c101 = sdf[Index(x1, p0.y, z1, dim)];
            float c011 = sdf[Index(p0.x, y1, z1, dim)];
            float c111 = sdf[Index(x1, y1, z1, dim)];
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            float sample = math.lerp(c0, c1, f.z);
            if (!math.isfinite(sample))
                return KccSmokeInvalidSdfMeters;

            // Open exterior: edge free-space grows with distance outside the brick.
            // If the edge is solid (negative), exterior distance still opens toward free water.
            return sample + exteriorMeters;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSdfLayout(NativeArray<float> sdf, KccSmokeVoxelSdfInfoDTO info, out int3 dim, out float cell, out int requiredCount)
        {
            dim = info.Dimensions;
            cell = info.CellSizeMeters;
            requiredCount = 0;
            if (!sdf.IsCreated ||
                dim.x < 2 ||
                dim.y < 2 ||
                dim.z < 2 ||
                !math.isfinite(cell) ||
                cell <= 0f)
            {
                return false;
            }

            long xy = (long)dim.x * dim.y;
            if (xy <= 0L || xy > int.MaxValue)
                return false;

            long maxZ = int.MaxValue / xy;
            if (dim.z > maxZ)
                return false;

            long required = xy * dim.z;
            if (xy <= 0L || required <= 0L || required > sdf.Length || required > int.MaxValue)
                return false;

            cell = math.max(0.25f, cell);
            requiredCount = (int)required;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInvalidSdf(float sdfMeters)
        {
            return !math.isfinite(sdfMeters) || sdfMeters <= KccSmokeInvalidSdfMeters * 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static HydrodynamicKccTuningDTO SanitizeSmokeTuning(HydrodynamicKccTuningDTO tuning)
        {
            tuning.BaseDrag = math.max(0f, math.isfinite(tuning.BaseDrag) ? tuning.BaseDrag : 0.018f);
            tuning.MaxSpeed = math.max(10f, math.isfinite(tuning.MaxSpeed) ? tuning.MaxSpeed : 86f);
            tuning.SkinWidth = math.clamp(math.isfinite(tuning.SkinWidth) ? tuning.SkinWidth : 0.08f, 0.005f, 1f);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.CapsuleRadius = math.max(0.05f, math.isfinite(tuning.CapsuleRadius) ? tuning.CapsuleRadius : 0.42f);
            tuning.CapsuleHeight = math.max(tuning.CapsuleRadius * 2f, math.isfinite(tuning.CapsuleHeight) ? tuning.CapsuleHeight : 1.8f);
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static KccSmokeVoxelSdfInfoDTO SanitizeSmokeSdfInfo(KccSmokeVoxelSdfInfoDTO info)
        {
            info.OriginAup = HydrodynamicKccMath.Sanitize(info.OriginAup, double3.zero);
            info.CellSizeMeters = math.max(0.25f, math.isfinite(info.CellSizeMeters) ? info.CellSizeMeters : 2f);
            info.SurfaceOffsetMeters = math.isfinite(info.SurfaceOffsetMeters) ? info.SurfaceOffsetMeters : 0f;
            info.CapsuleRadiusMeters = math.max(0.05f, math.isfinite(info.CapsuleRadiusMeters) ? info.CapsuleRadiusMeters : 0.42f);
            return info;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Index(int x, int y, int z, int3 dim)
        {
            return x + y * dim.x + z * dim.x * dim.y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimateSmokeMicroseconds(int count, float maxSpeed)
        {
            float entityCost = math.max(1, count) * 0.0025f;
            float speedCost = math.saturate(maxSpeed * 0.001f) * 0.18f;
            return 0.45f + entityCost + speedCost;
        }
    }
}
