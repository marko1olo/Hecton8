using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.KCC
{
    /// <summary>
    /// Bit definitions used by the KCC-owned SHINOBU_249 sleep-state kernel artifact.
    /// </summary>
    /// <remarks>
    /// Buoyancy runtime does not schedule or mutate these KCC rows; active debris sleep authority stays in BuoyancyStateDTO.
    /// </remarks>
    public static class KinematicSleepStateFlags
    {
        /// <summary>Marks a KCC SDF config row as usable.</summary>
        public const uint ConfigActive = 1u;
        /// <summary>Marks a kinematic row as sleeping.</summary>
        public const uint Sleeping = 1u << 13;
        /// <summary>Marks a kinematic row as SDF-grounded.</summary>
        public const uint SdfGrounded = 1u << 14;
        /// <summary>Marks a kinematic row as eligible for deep-sleep presentation promotion.</summary>
        public const uint DeepSleeping = 1u << 15;
        /// <summary>Marks a kinematic row woken by an external wake signal.</summary>
        public const uint WakeSignal = 1u << 16;
        /// <summary>Marks a row that contained non-finite input.</summary>
        public const uint NonFinite = 1u << 31;
    }

    /// <summary>
    /// 64-byte KCC-local SDF sampling config for sleep contact checks.
    /// </summary>
    /// <remarks>
    /// Explicit layout keeps the config ARM64-safe and independent from Buoyancy DTO ownership.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct KinematicSleepSdfConfigDTO
    {
        /// <summary>SDF grid origin in absolute universe coordinates.</summary>
        [FieldOffset(0)] public double3 SdfOriginAUP;
        /// <summary>Grid cell size in meters.</summary>
        [FieldOffset(24)] public float CellSizeMeters;
        /// <summary>Signed-byte-to-distance decode scale.</summary>
        [FieldOffset(28)] public float DensityDecodeScale;
        /// <summary>Accepted absolute contact distance for grounded checks.</summary>
        [FieldOffset(32)] public float ContactEpsilonMeters;
        /// <summary>SDF grid width in cells.</summary>
        [FieldOffset(36)] public int Width;
        /// <summary>SDF grid height in cells.</summary>
        [FieldOffset(40)] public int Height;
        /// <summary>SDF grid depth in cells.</summary>
        [FieldOffset(44)] public int Depth;
        /// <summary>Y stride in cells.</summary>
        [FieldOffset(48)] public int StrideY;
        /// <summary>Z stride in cells.</summary>
        [FieldOffset(52)] public int StrideZ;
        /// <summary>Configuration flags.</summary>
        [FieldOffset(56)] public uint Flags;
        /// <summary>Explicit padding to 64 bytes.</summary>
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>
    /// Seeds deterministic seabed debris rows for isolated kinematic sleep stress testing.
    /// </summary>
    /// <remarks>
    /// Writes fixed rows only; no managed allocation, no active/inactive list movement.
    /// </remarks>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockSettlingDebrisJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute lane overwrites exactly one KinematicStateDTO row after StateCount bounds validation.
        // Unity cannot prove that the pointer/ref store maps only to States[index], so the parallel write
        // restriction is disabled for this deterministic mock seeding lane.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Clearing or constructing managed mock debris before the job was rejected because it would allocate
        // and serialize deterministic fallback data. The job writes every active row from stable frame/hash
        // inputs and leaves no uninitialized gameplay truth.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: the mock seeding schedule owns States exclusively until its returned JobHandle completes.
        // No consumer reads the seeded KCC state rows until that dependency is chained by the dispatcher.
        /// <summary>Writable kinematic state rows.</summary>
        [WriteOnly, NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        /// <summary>Maximum rows to touch.</summary>
        public int StateCount;
        /// <summary>Rows to seed as active mock debris.</summary>
        public int ActiveMockCount;
        /// <summary>Seafloor origin in absolute universe coordinates.</summary>
        public double3 SeafloorOriginAUP;
        /// <summary>Deterministic simulation frame used to phase the mock motion.</summary>
        public uint SimulationFrame;

        /// <summary>Seeds one deterministic mock debris row.</summary>
        /// <param name="index">Row index.</param>
        public void Execute(int index)
        {
            if (!States.IsCreated)
                return;

            int stateCount = math.min(math.max(0, StateCount), States.Length);
            if ((uint)index >= (uint)stateCount)
                return;

            int activeCount = math.clamp(ActiveMockCount, 0, stateCount);
            bool active = index < activeCount;
            double3 origin = math.select(double3.zero, SeafloorOriginAUP, math.isfinite(SeafloorOriginAUP));
            float lane = (index & 63) - 31.5f;
            float row = ((index >> 6) & 1023) - 511.5f;
            float phase = (index * 0.017453292f) + (SimulationFrame * 0.00390625f);
            float decayInput = 0.00022f * math.max(0f, (float)index);
            float decay = math.rcp(1f + decayInput + (0.5f * decayInput * decayInput));
            float lateral = TriangleSigned(phase) * decay;
            float angular = TriangleSigned(phase * 1.6180339f) * decay;

            KinematicStateDTO state = default;
            state.AUP_Position = math.select(double3.zero, origin + new double3(lane * 1.25f, 0.02f, row * 1.25f), active);
            state.Velocity = math.select(float3.zero, new float3(lateral * 0.018f, -0.00075f, -lateral * 0.014f), active);
            state.AngularVelocity = math.select(float3.zero, new float3(angular * 0.01f, angular * 0.006f, -angular * 0.008f), active);
            state.Mass = math.select(0f, 0.5f + ((index * 17) & 127) * 0.05f, active);
            state.Flags = 0u;
            state.DragCoefficient = math.select(0f, 0.08f + ((index * 31) & 15) * 0.004f, active);
            state.RestingFrameCount = (byte)math.select(0, math.min(255, index & 31), active);

            KinematicStateDTO* statesPtr = (KinematicStateDTO*)States.GetUnsafePtr();
            ref KinematicStateDTO stateRef = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesPtr + index);
            stateRef = state;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleSigned(float phase)
        {
            phase -= math.floor(phase);
            return (2f * math.abs((2f * phase) - 1f)) - 1f;
        }
    }

    /// <summary>
    /// Evaluates KCC-owned kinematic sleep state from kinetic energy and SDF contact.
    /// </summary>
    /// <remarks>
    /// This kernel is isolated from Buoyancy scheduling to avoid split authority.
    /// </remarks>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateKinematicSleepStateJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute lane evaluates and mutates exactly one KinematicStateDTO row. The mutation goes through
        // UnsafeUtility.AsRef to avoid large-DTO copies, and Unity cannot statically prove row exclusivity from
        // that pointer access.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A temporary sleep-state output buffer was rejected because it duplicates authority flags and needs an
        // extra merge pass. Mutating the owner row in place keeps one fact in one route and preserves rollback
        // snapshot layout.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: SleepSdfDensity is read-only, and States is exclusively owned by this sleep evaluation
        // phase until its JobHandle is returned to the scheduler. Wake-trigger jobs are chained after it.
        /// <summary>Mutable kinematic state rows.</summary>
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        /// <summary>Signed-byte SDF distance field.</summary>
        [ReadOnly, NoAlias] public NativeArray<sbyte> SleepSdfDensity;
        /// <summary>SDF sampling config.</summary>
        public KinematicSleepSdfConfigDTO SleepSdfConfig;
        /// <summary>Sector fallback origin in absolute universe coordinates.</summary>
        public double3 SectorAUP;
        /// <summary>Rows eligible for evaluation.</summary>
        public int ActiveStateCount;
        /// <summary>Base squared linear sleep speed.</summary>
        public float BaseLinearSleepSpeedSq;
        /// <summary>Base squared angular sleep speed.</summary>
        public float BaseAngularSleepSpeedSq;
        /// <summary>Continuous quality weight controlling sleep aggressiveness.</summary>
        public float GlobalQualityWeight;
        /// <summary>Frames below threshold before sleep.</summary>
        public byte RequiredRestFrames;
        /// <summary>Sleeping frames before deep-sleep flag.</summary>
        public byte RequiredDeepSleepFrames;
        /// <summary>Optional override for the sleeping bit.</summary>
        public uint SleepingBit;
        /// <summary>Optional override for the SDF-grounded bit.</summary>
        public uint SdfGroundedBit;
        /// <summary>Optional override for the deep-sleep bit.</summary>
        public uint DeepSleepingBit;
        /// <summary>Optional override for the non-finite bit.</summary>
        public uint NonFiniteBit;

        /// <summary>Evaluates one kinematic row.</summary>
        /// <param name="index">Row index.</param>
        public void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)math.min(math.max(0, ActiveStateCount), States.Length))
                return;

            uint sleepingBit = math.select(KinematicSleepStateFlags.Sleeping, SleepingBit, SleepingBit != 0u);
            uint groundedBit = math.select(KinematicSleepStateFlags.SdfGrounded, SdfGroundedBit, SdfGroundedBit != 0u);
            uint deepSleepingBit = math.select(KinematicSleepStateFlags.DeepSleeping, DeepSleepingBit, DeepSleepingBit != 0u);
            uint nonFiniteBit = math.select(KinematicSleepStateFlags.NonFinite, NonFiniteBit, NonFiniteBit != 0u);

            KinematicStateDTO* statesPtr = (KinematicStateDTO*)States.GetUnsafePtr();
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesPtr + index);
            bool finite = math.all(math.isfinite(state.AUP_Position)) &
                          math.all(math.isfinite(state.Velocity)) &
                          math.all(math.isfinite(state.AngularVelocity)) &
                          math.isfinite(state.Mass);
            state.Flags &= ~(groundedBit | sleepingBit | deepSleepingBit | nonFiniteBit);
            state.Flags |= math.select(0u, nonFiniteBit, !finite);
            state.Velocity = math.select(float3.zero, state.Velocity, math.isfinite(state.Velocity));
            state.AngularVelocity = math.select(float3.zero, state.AngularVelocity, math.isfinite(state.AngularVelocity));
            state.Mass = math.max(0f, math.select(0f, state.Mass, math.isfinite(state.Mass)));

            float q = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float curve = q * q * (3f - 2f * q);
            float linearThresholdSq = math.max(0.000001f, math.lerp(0.1f, math.max(0.000001f, BaseLinearSleepSpeedSq), curve));
            float angularThresholdSq = math.max(0.000001f, math.lerp(0.05f, math.max(0.000001f, BaseAngularSleepSpeedSq), curve));
            int restFrames = math.clamp((int)math.round(math.lerp(2f, math.max(1, RequiredRestFrames), curve)), 1, 255);
            int deepFrames = math.clamp(math.max(1, RequiredDeepSleepFrames), 1, 255);

            float velocitySq = math.lengthsq(state.Velocity);
            float angularSq = math.lengthsq(state.AngularVelocity);
            bool finiteKineticSq = math.isfinite(velocitySq) & math.isfinite(angularSq);
            float energy = 0.5f * math.max(1f, state.Mass) * (velocitySq + angularSq);
            bool finiteEnergy = math.isfinite(energy);
            state.Flags |= math.select(0u, nonFiniteBit, !(finiteKineticSq & finiteEnergy));
            bool grounded = SampleGroundingSdf(state.AUP_Position, SectorAUP, SleepSdfConfig, out _);
            bool canSleep = finite & finiteKineticSq & finiteEnergy & grounded & (velocitySq <= linearThresholdSq) & (angularSq <= angularThresholdSq) &
                            (energy <= math.max(linearThresholdSq, angularThresholdSq) * math.max(1f, state.Mass));

            state.RestingFrameCount = canSleep ? IncrementByteSaturated(state.RestingFrameCount) : (byte)0;
            bool sleepNow = canSleep & state.RestingFrameCount >= restFrames;
            state.DeepSleepTickCount = sleepNow ? IncrementByteSaturated(state.DeepSleepTickCount) : (byte)0;
            bool deepSleep = sleepNow & state.DeepSleepTickCount >= deepFrames;
            state.Flags |= math.select(0u, groundedBit, grounded);
            state.Flags |= math.select(0u, sleepingBit, sleepNow);
            state.Flags |= math.select(0u, deepSleepingBit, deepSleep);
            state.Velocity = math.select(state.Velocity, float3.zero, sleepNow);
            state.AngularVelocity = math.select(state.AngularVelocity, float3.zero, sleepNow);
        }

        private bool SampleGroundingSdf(
            double3 objectAup,
            double3 sectorAup,
            KinematicSleepSdfConfigDTO config,
            out float signedDistance)
        {
            signedDistance = 1000000f;
            if (!SleepSdfDensity.IsCreated ||
                SleepSdfDensity.Length <= 0 ||
                (config.Flags & KinematicSleepStateFlags.ConfigActive) == 0u ||
                config.Width <= 1 ||
                config.Height <= 1 ||
                config.Depth <= 1)
            {
                return false;
            }

            double3 originAup = math.select(sectorAup, config.SdfOriginAUP, math.all(math.isfinite(config.SdfOriginAUP)));
            double3 localAup = objectAup - originAup;
            float3 rawLocal = new float3((float)localAup.x, (float)localAup.y, (float)localAup.z);
            float3 local = math.select(float3.zero, rawLocal, math.isfinite(rawLocal));
            float cellSize = math.max(0.001f, math.select(1f, config.CellSizeMeters, math.isfinite(config.CellSizeMeters)));
            int ix = (int)math.floor(local.x * math.rcp(cellSize));
            int iy = (int)math.floor(local.y * math.rcp(cellSize));
            int iz = (int)math.floor(local.z * math.rcp(cellSize));
            if ((uint)ix >= (uint)config.Width ||
                (uint)iy >= (uint)config.Height ||
                (uint)iz >= (uint)config.Depth)
            {
                return false;
            }

            long strideY = math.select(config.Width, config.StrideY, config.StrideY > 0);
            long strideZ = config.StrideZ > 0 ? config.StrideZ : (long)config.Width * config.Height;
            long densityIndex = ix + ((long)iy * strideY) + ((long)iz * strideZ);
            if (densityIndex < 0L || densityIndex >= SleepSdfDensity.Length)
                return false;

            float decodeScale = math.max(0.0001f, math.select(0.05f, config.DensityDecodeScale, math.isfinite(config.DensityDecodeScale)));
            signedDistance = SleepSdfDensity[(int)densityIndex] * decodeScale;
            float contactEpsilon = math.max(0.001f, math.select(0.2f, config.ContactEpsilonMeters, math.isfinite(config.ContactEpsilonMeters)));
            return math.abs(signedDistance) <= contactEpsilon;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte IncrementByteSaturated(byte value)
        {
            return (byte)math.min(255, value + 1);
        }
    }

    /// <summary>
    /// Clears KCC sleep bits when wake requests overlap a sleeping row.
    /// </summary>
    /// <remarks>
    /// Passive wake listening avoids collider polling on dormant rows.
    /// </remarks>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ProcessKinematicSleepWakeTriggersJob : IJobParallelFor
    {
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // Each Execute lane mutates one KinematicStateDTO row after the ActiveStateCount guard while reading a
        // separate immutable wake request stream. Unity cannot prove the pointer/ref row write is non-overlap.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Emitting wake commands into a queue and applying them later was rejected because the sleeping flag is
        // row authority and the extra pass would widen the dependency graph. Direct mutation is the cheaper
        // deterministic route.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: the KCC scheduler chains this job after sleep evaluation and before downstream KCC state
        // reads. WakeRequests is read-only, and States has no concurrent writer during this phase.
        /// <summary>Mutable kinematic state rows.</summary>
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        /// <summary>Frame-local wake request snapshot.</summary>
        [ReadOnly, NoAlias] public NativeArray<WakeRequestSignal>.ReadOnly WakeRequests;
        /// <summary>Rows eligible for wake processing.</summary>
        public int ActiveStateCount;
        /// <summary>Wake request count to read from the snapshot.</summary>
        public int WakeRequestCount;
        /// <summary>Optional override for the sleeping bit.</summary>
        public uint SleepingBit;
        /// <summary>Optional override for the deep-sleep bit.</summary>
        public uint DeepSleepingBit;
        /// <summary>Optional override for the wake-signal bit.</summary>
        public uint WakeSignalBit;

        /// <summary>Processes wake overlap for one kinematic row.</summary>
        /// <param name="index">Row index.</param>
        public void Execute(int index)
        {
            if (!States.IsCreated || (uint)index >= (uint)math.min(math.max(0, ActiveStateCount), States.Length))
                return;

            int wakeCount = WakeRequests.IsCreated
                ? math.min(math.max(0, WakeRequestCount), WakeRequests.Length)
                : 0;
            if (wakeCount <= 0)
                return;

            uint sleepingBit = math.select(KinematicSleepStateFlags.Sleeping, SleepingBit, SleepingBit != 0u);
            uint deepSleepingBit = math.select(KinematicSleepStateFlags.DeepSleeping, DeepSleepingBit, DeepSleepingBit != 0u);
            uint wakeSignalBit = math.select(KinematicSleepStateFlags.WakeSignal, WakeSignalBit, WakeSignalBit != 0u);
            KinematicStateDTO* statesPtr = (KinematicStateDTO*)States.GetUnsafePtr();
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesPtr + index);
            if ((state.Flags & sleepingBit) == 0u)
                return;

            bool wake = false;
            for (int i = 0; i < wakeCount; i++)
            {
                WakeRequestSignal request = WakeRequests[i];
                if (!math.all(math.isfinite(request.OriginAup)) ||
                    !math.isfinite(request.RadiusMeters) ||
                    request.RadiusMeters <= 0f)
                {
                    continue;
                }

                double3 delta = state.AUP_Position - request.OriginAup;
                float3 rawDelta = new float3((float)delta.x, (float)delta.y, (float)delta.z);
                float3 localDelta = math.select(float3.zero, rawDelta, math.isfinite(rawDelta));
                float radius = math.min(10000f, math.max(0.01f, request.RadiusMeters));
                wake |= math.lengthsq(localDelta) <= radius * radius;
            }

            if (!wake)
                return;

            state.Flags = (state.Flags & ~(sleepingBit | deepSleepingBit)) | wakeSignalBit;
            state.RestingFrameCount = 0;
            state.DeepSleepTickCount = 0;
        }
    }
}
