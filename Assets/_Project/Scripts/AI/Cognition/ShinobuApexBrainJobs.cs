using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Cold/simulation test job that advances a blind mock target without Player Kinematics.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockPlayerAupAdvanceJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MockPlayerAUP> Targets;
        public float SimulationTickDelta;
        public uint Frame;

        /// <inheritdoc />
        public void Execute(int index)
        {
            if (!Targets.IsCreated || (uint)index >= (uint)Targets.Length)
                return;

            MockPlayerAUP target = Targets[index];
            float dt = SanitizePositive(SimulationTickDelta, target.SimulationTickDelta);
            float3 velocity = SanitizeFinite(target.Velocity, float3.zero);
            target.AUP += new double3(velocity.x, velocity.y, velocity.z) * dt;
            target.Forward = NormalizeSafe(target.Forward, NormalizeSafe(velocity, new float3(0f, 0f, 1f)));
            target.SimulationTickDelta = dt;
            target.Flags |= MockPlayerAupFlags.Active | MockPlayerAupFlags.HasForward;
            target._pad0 = (ulong)Frame;
            Targets[index] = target;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float primary, float fallback)
        {
            float selected = math.select(fallback, primary, math.isfinite(primary) & primary > 0f);
            return math.min(math.max(selected, ApexBrainConstants.Epsilon), 0.25f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.select(fallback, value, math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            value = SanitizeFinite(value, fallback);
            float lengthSq = math.lengthsq(value);
            bool valid = lengthSq > ApexBrainConstants.Epsilon;
            return math.select(fallback, value * math.rsqrt(math.max(lengthSq, ApexBrainConstants.Epsilon)), valid);
        }
    }

    /// <summary>
    /// SHINOBU_61 apex hunting kernel: predictive intercept, acoustic memory, SDF slither, sweet-lie LOS, and utility matrix.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ApexBrainJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ApexStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<MockPlayerAUP> MockTargets;
        [ReadOnly, NoAlias] public NativeArray<AcousticEchoTap> AcousticTaps;
        [ReadOnly, NoAlias] public NativeArray<ApexBrainTuning> Tuning;
        [ReadOnly, NoAlias] public NativeArray<ApexEmergencyStats> EmergencyStats;
        [ReadOnly, NoAlias] public NativeArray<MockWorldSampler> WorldSampler;
        [NoAlias] public NativeArray<ApexBrainOutputDTO> Outputs;
        [NoAlias] public NativeArray<ApexProximitySignal> ProximitySignals;
        [NoAlias] public NativeArray<MockCombatDamageSignal> CombatDamageSignals;
        [NoAlias] public NativeArray<GlobalPanicSignal> PanicSignals;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ApexInfluenceNode> InfluenceNodes;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<float3> AmbushNodeScratch;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<ApexTelemetryEntry> TelemetryRing;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // This optional writer is default-initialized on the vault-row schedule path and is accessed only when
        // EnableSignalQueueWrites is non-zero. Unity's safety validation cannot express that external schedule
        // contract for NativeQueue<T>.ParallelWriter fields carried in the same Burst job.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A second signal-emitting job was rejected because it would duplicate the apex kernel and risk divergent
        // proximity truth. A managed post-job relay was rejected because it would allocate or force a main-thread
        // readback. The optional writer keeps the hot path single-kernel and zero-GC.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: only ApexBrainVault.AttachSignalWriters/TryScheduleWithSignalWriters set
        // EnableSignalQueueWrites=1, and those methods receive externally owned queue writers from the Core
        // SignalBus boundary. When the flag is 0, this field is never read or enqueued.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<ApexProximitySignal>.ParallelWriter ProximitySignalWriter;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // This optional writer is default-initialized on schedules that only write DataVault combat signal rows.
        // The safety system cannot prove the EnableSignalQueueWrites guard, so it may reject the no-writer path
        // even though CombatDamageSignalWriter.Enqueue is unreachable there.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A direct Hecton8.Core SignalBus reference was rejected because it widened the runtime compile wall and
        // previously exposed duplicate ISignal identity. Copying combat rows through managed code after the job was
        // rejected because breach signals are rollback-relevant and must remain deterministic.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: when EnableSignalQueueWrites=1, the owning Core/SignalBus bridge has already provided a valid
        // ParallelWriter and owns queue lifetime beyond the returned JobHandle. When the flag is 0, this writer is
        // inert and only the DataVault MockCombatDamageSignal row is written.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<MockCombatDamageSignal>.ParallelWriter CombatDamageSignalWriter;

        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // This optional writer follows the same gated pattern as the proximity and combat lanes. Unity Jobs cannot
        // infer that PanicSignalWriter.Enqueue is guarded by EnableSignalQueueWrites and by panic intensity, so the
        // attribute suppresses a schedule-time false positive for default writer structs.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // Splitting panic broadcast into a separate job would require rereading apex output and increase dependency
        // surface. Direct ecosystem-domain calls are forbidden by compile-wall isolation. The queue writer preserves
        // a pure unmanaged signal lane with no sibling runtime reference.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // Invariant: the queue writer is used only inside WriteSignals after the vault panic row is populated, and
        // only when EnableSignalQueueWrites!=0. The caller owns the NativeQueue and must chain the returned
        // JobHandle before draining or disposing it.
        [NativeDisableContainerSafetyRestriction] public NativeQueue<GlobalPanicSignal>.ParallelWriter PanicSignalWriter;
        public int TargetCount;
        public int AcousticTapCount;
        public uint Frame;
        public byte EnableSignalQueueWrites;

        /// <inheritdoc />
        public void Execute(int index)
        {
            if (!CanExecute(index))
                return;

            ApexBrainTuning tuning = ResolveTuning();
            ApexEmergencyStats stats = ResolveEmergencyStats();
            MockWorldSampler sampler = ResolveSampler(tuning);
            float quality = ResolveQuality(tuning.GlobalQualityWeight);
            float qualityCurve = Smooth01(math.saturate((quality - ApexBrainConstants.LowQualityNodeHold) * math.rcp(1f - ApexBrainConstants.LowQualityNodeHold)));
            int acousticTapLimit = ResolveAcousticTapLimit(qualityCurve);
            int targetIndex = ResolveTargetIndex(index);
            MockPlayerAUP target = MockTargets[targetIndex];
            ApexStateDTO state = States[index];
            ushort slot = (ushort)math.min(index, ushort.MaxValue);
            byte flags = ApexBrainFlags.Active;
            float lowQualityCollapse01 = 1f - math.step(0.3f, quality);
            if (lowQualityCollapse01 > 0f)
                flags = (byte)(flags | ApexBrainFlags.LowQualityCollapse);
            if ((tuning.Flags & ApexBrainFlags.EmergencyMockStats) != 0u)
                flags = (byte)(flags | ApexBrainFlags.EmergencyMockStats);

            bool active = (target.Flags & MockPlayerAupFlags.Active) != 0u;
            if (!active)
            {
                WriteDormantRow(index, slot, target, tuning);
                return;
            }

            bool finiteInput =
                math.all(math.isfinite(state.AUP)) &
                math.all(math.isfinite(target.AUP)) &
                math.all(math.isfinite(state.Velocity)) &
                math.all(math.isfinite(target.Velocity));
            if (!finiteInput)
            {
                flags = (byte)(flags | ApexBrainFlags.Fault);
                WriteFaultRow(index, slot, state, target, tuning, flags, 0x53484E49u);
                return;
            }

            float3 targetLocal = DowncastAupDelta(target.AUP - state.AUP);
            float distanceSq = math.max(math.lengthsq(targetLocal), ApexBrainConstants.Epsilon);
            float distanceMeters = distanceSq * math.rsqrt(distanceSq);
            float3 targetDirection = NormalizeSafe(targetLocal, new float3(0f, 0f, 1f));
            float stamina = math.saturate(math.select(1f, state.Stamina, math.isfinite(state.Stamina)));
            float speed = math.max(ApexBrainConstants.Epsilon, tuning.LeviathanSpeed * math.lerp(0.45f, 1f, stamina));
            float interceptTime = distanceMeters * math.rcp(speed);
            float3 interceptLocal = targetLocal + (SanitizeFinite(target.Velocity, float3.zero) * interceptTime);
            float3 interceptDirection = NormalizeSafe(interceptLocal, targetDirection);

            AcousticSelection acoustic = ResolveAcousticMemory(state.AUP, target, tuning, acousticTapLimit);
            if (acoustic.Score01 > ApexBrainConstants.Epsilon)
                flags = (byte)(flags | ApexBrainFlags.AcousticOverride);
            float3 pursuitLocal = math.select(interceptLocal, acoustic.LocalPosition, acoustic.Score01 > 0.65f);
            float3 pursuitDirection = NormalizeSafe(pursuitLocal, interceptDirection);

            SdfSample centerSample = SampleMockSdf(sampler, float3.zero);
            SdfSample headSample = SampleMockSdf(sampler, pursuitDirection * math.max(tuning.HeadOffsetMeters, sampler.HeadOffsetMeters));
            float midWeight = SmoothStep(ApexBrainConstants.SdfMidsectionStartQuality, 0.62f, quality) *
                              math.step(ApexBrainConstants.SdfMidsectionStartQuality, quality);
            float tailWeight = SmoothStep(ApexBrainConstants.SdfTailStartQuality, 0.94f, quality) *
                               math.step(ApexBrainConstants.SdfTailStartQuality, quality);
            SdfSample midSample = headSample;
            SdfSample tailSample = headSample;
            if (midWeight > ApexBrainConstants.Epsilon)
                midSample = SampleMockSdf(sampler, -pursuitDirection * math.max(tuning.MidOffsetMeters, sampler.MidOffsetMeters));
            if (tailWeight > ApexBrainConstants.Epsilon)
            {
                tailSample = SampleMockSdf(sampler, -pursuitDirection * math.max(tuning.TailOffsetMeters, sampler.TailOffsetMeters));
                flags = (byte)(flags | ApexBrainFlags.TailSdfSampled);
            }

            float3 wallRepulsion =
                ResolveRepulsion(centerSample, sampler) +
                ResolveRepulsion(headSample, sampler) +
                (ResolveRepulsion(midSample, sampler) * midWeight) +
                (ResolveRepulsion(tailSample, sampler) * tailWeight);
            wallRepulsion = SanitizeFinite(wallRepulsion, float3.zero);

            float3 playerForward = NormalizeSafe(target.Forward, -targetDirection);
            float3 playerToLeviathan = NormalizeSafe(-targetLocal, -targetDirection);
            float viewDot = math.saturate(math.dot(playerForward, playerToLeviathan));
            float distanceVisibility = 1f - math.saturate(distanceMeters * math.rcp(math.max(tuning.StalkingDistance * 3f, 1f)));
            float hashShadow = HashToUnit(HashSpatial(targetLocal, math.max(sampler.SpatialCellSizeMeters, 1f)));
            float wallShadow = math.saturate((sampler.SdfSoftMarginMeters - centerSample.Distance) * math.rcp(math.max(sampler.SdfSoftMarginMeters, ApexBrainConstants.Epsilon)));
            float losProbeWeight = SmoothStep(0.28f, 0.85f, quality) * math.step(0.28f, quality);
            SdfSample lineSample = centerSample;
            if (losProbeWeight > ApexBrainConstants.Epsilon)
                lineSample = SampleMockSdf(sampler, targetLocal * 0.5f);
            float lineShadow = math.saturate((sampler.SdfSoftMarginMeters - lineSample.Distance) * math.rcp(math.max(sampler.SdfSoftMarginMeters, ApexBrainConstants.Epsilon)));
            float sdfShadow = math.lerp(wallShadow, math.max(wallShadow, lineShadow), losProbeWeight);
            float canyonHashShadow = hashShadow * sampler.CanyonBias01 * (1f - qualityCurve);
            float sweetLieShadow = math.saturate((sdfShadow * tuning.SweetLieShadowGain) + canyonHashShadow);
            float sweetLieLos01 = math.saturate(viewDot * distanceVisibility * (1f - sweetLieShadow));
            if (sweetLieShadow > 0.5f)
                flags = (byte)(flags | ApexBrainFlags.SweetLieOccluded);

            bool computedFinite =
                math.all(math.isfinite(targetLocal)) &
                math.all(math.isfinite(interceptLocal)) &
                math.all(math.isfinite(wallRepulsion)) &
                math.isfinite(centerSample.Distance) &
                math.isfinite(headSample.Distance) &
                math.isfinite(lineSample.Distance) &
                math.isfinite(sweetLieLos01);
            if (!computedFinite)
            {
                flags = (byte)(flags | ApexBrainFlags.Fault);
                WriteFaultRow(index, slot, state, target, tuning, flags, 0x53484E4Eu);
                return;
            }

            float biomeMultiplier = ResolveBiomeMultiplier(target, tuning);
            float dt = ResolveDeltaTime(tuning, target);
            float acousticAggro = acoustic.Score01 * tuning.AcousticSensitivity;
            float noiseAggro = math.saturate(target.Noise01) * tuning.NoiseAggroGain;
            float lineOfSightRestraint = math.lerp(1.15f, 0.35f, sweetLieLos01);
            float aggressionDelta = dt * tuning.AggressionMultiplier * biomeMultiplier * lineOfSightRestraint * (noiseAggro + acousticAggro + 0.05f);
            float aggression = math.saturate(math.select(0f, state.AggressionLevel, math.isfinite(state.AggressionLevel)) + aggressionDelta);

            float nodeFloat = math.lerp(ApexBrainConstants.MinAmbushNodes, ApexBrainConstants.MaxAmbushNodes, qualityCurve);
            int fullNodeCount = math.clamp((int)math.floor(nodeFloat), ApexBrainConstants.MinAmbushNodes, ApexBrainConstants.MaxAmbushNodes);
            float fractionalNode = math.saturate(nodeFloat - fullNodeCount);
            int evaluatedNodeCount = math.min(ApexBrainConstants.MaxAmbushNodes, fullNodeCount + math.select(0, 1, fractionalNode > ApexBrainConstants.Epsilon));
            AmbushSelection ambush = ResolveAmbushNodes(index, pursuitLocal, pursuitDirection, sampler, tuning, quality, sweetLieShadow, fullNodeCount, evaluatedNodeCount, fractionalNode);

            float stalkUtility = math.saturate(sweetLieLos01 + (sweetLieShadow * 0.35f) + (acoustic.Score01 * 0.2f));
            float ambushUtility = math.saturate(ambush.Score01 * math.lerp(0.6f, 1.25f, quality));
            float strikeUtility = math.saturate((1f - sweetLieLos01) * aggression * math.saturate(1f - (distanceMeters * math.rcp(math.max(tuning.StrikeDistance * 4f, 1f)))));
            bool strike = strikeUtility > math.max(stalkUtility, ambushUtility) & aggression > 0.62f & distanceMeters <= tuning.StrikeDistance * 4f;
            bool hide = stalkUtility >= strikeUtility & sweetLieLos01 > tuning.SweetLieViewDotThreshold;
            int phase = ApexBrainPhase.Ambush;
            phase = math.select(phase, ApexBrainPhase.Strike, strike);
            phase = math.select(phase, ApexBrainPhase.Hide, hide);
            phase = math.select(phase, ApexBrainPhase.Stalk, (!strike) & (!hide) & (stalkUtility > ambushUtility));
            if (phase == ApexBrainPhase.Strike)
                flags = (byte)(flags | ApexBrainFlags.StrikeCommitted);

            float3 desiredDirection = NormalizeSafe(pursuitDirection + wallRepulsion + (ambush.Direction * ambush.Score01 * 0.35f), pursuitDirection);
            float turnRate = math.saturate(tuning.TurnRate * dt);
            float3 previousDirection = NormalizeSafe(state.Velocity, desiredDirection);
            float3 smoothedDirection = NormalizeSafe(math.lerp(previousDirection, desiredDirection, turnRate), desiredDirection);
            float desiredSpeed = math.select(speed * 0.55f, speed, phase == ApexBrainPhase.Strike);
            float3 desiredVelocity = smoothedDirection * desiredSpeed;
            float3 ikBiteTarget = interceptLocal - (smoothedDirection * tuning.BiteHeadLocalOffset);
            float staminaAfter = math.saturate(stamina + (tuning.StaminaRecoveryPerSecond * dt) - math.select(0f, tuning.StaminaStrikeCost, phase == ApexBrainPhase.Strike));

            state.Velocity = desiredVelocity;
            state.AggressionLevel = aggression;
            state.TargetHash = target.TargetHash;
            state.AcousticMemoryHash = math.select(state.AcousticMemoryHash, acoustic.Hash, acoustic.Hash != 0u);
            state.Stamina = staminaAfter;
            States[index] = state;

            uint spatialHash = HashSpatial(interceptLocal, math.max(sampler.SpatialCellSizeMeters, 1f));
            uint stateHash = BuildStateHash(index, (byte)phase, flags, aggression, spatialHash, Frame);
            float visualOverkill = tuning.VisualOverkillGain * qualityCurve;
            float4 visualScalars = stats.VisualOverkillScalars * visualOverkill;

            ApexBrainOutputDTO output = default;
            output.DesiredVelocity = desiredVelocity;
            output.DesiredSpeed = desiredSpeed;
            output.IK_BiteTarget = ikBiteTarget;
            output.AggressionLevel = aggression;
            output.InterceptLocal = interceptLocal;
            output.StalkUtility = stalkUtility;
            output.AcousticMemoryLocal = acoustic.LocalPosition;
            output.AmbushUtility = ambushUtility;
            output.WallRepulsion = wallRepulsion;
            output.StrikeUtility = strikeUtility;
            output.BestAmbushNodeLocal = ambush.LocalPosition;
            output.SweetLieLos01 = sweetLieLos01;
            output.SpatialHash = spatialHash;
            output.StateHash = stateHash;
            output.EvaluatedNodeCount = (uint)evaluatedNodeCount;
            output.FractionalNodeWeight01 = fractionalNode;
            output.DesiredDirection = smoothedDirection;
            output.TerrorRadiusMeters = tuning.TerrorRadius;
            output.VisualOverkillScalars = visualScalars;
            output.Slot = slot;
            output.Phase = (byte)phase;
            output.Flags = flags;
            output.TargetHash = target.TargetHash;
            output.AcousticMemoryHash = state.AcousticMemoryHash;
            Outputs[index] = output;

            WriteSignals(index, slot, state, target, output, tuning, (byte)phase, flags, smoothedDirection);
            WriteTelemetry(index, slot, output, target, tuning, (byte)phase, flags, stateHash, 0u, acousticTapLimit);
        }

        private bool CanExecute(int index)
        {
            return States.IsCreated &&
                   MockTargets.IsCreated &&
                   Outputs.IsCreated &&
                   (uint)index < (uint)States.Length &&
                   (uint)index < (uint)Outputs.Length &&
                   MockTargets.Length > 0;
        }

        private int ResolveTargetIndex(int index)
        {
            int count = math.min(math.max(1, TargetCount), MockTargets.Length);
            return math.select(0, index % count, count > 1);
        }

        private ApexBrainTuning ResolveTuning()
        {
            ApexBrainTuning tuning = ApexBrainDefaults.BuildEmergencyMockTuning();
            if (Tuning.IsCreated && Tuning.Length > 0)
            {
                ApexBrainTuning candidate = Tuning[0];
                if (math.isfinite(candidate.LeviathanSpeed) && candidate.LeviathanSpeed > ApexBrainConstants.Epsilon)
                    tuning = candidate;
            }

            tuning.AggressionMultiplier = SanitizePositive(tuning.AggressionMultiplier, 1f);
            tuning.AcousticSensitivity = SanitizePositive(tuning.AcousticSensitivity, 1f);
            tuning.TurnRate = SanitizePositive(tuning.TurnRate, 0.18f);
            tuning.StalkingDistance = SanitizePositive(tuning.StalkingDistance, 90f);
            tuning.LeviathanSpeed = SanitizePositive(tuning.LeviathanSpeed, 26f);
            tuning.TerrorRadius = SanitizePositive(tuning.TerrorRadius, 160f);
            tuning.BaseDamageMagnitude = SanitizePositive(tuning.BaseDamageMagnitude, 500f);
            tuning.BiomeAggressionMultiplier = SanitizePositive(tuning.BiomeAggressionMultiplier, 2f);
            tuning.SimulationTickDelta = SanitizePositive(tuning.SimulationTickDelta, 1f / 30f);
            tuning.StrikeDistance = SanitizePositive(tuning.StrikeDistance, 24f);
            tuning.NoiseAggroGain = SanitizePositive(tuning.NoiseAggroGain, 0.35f);
            tuning.StaminaRecoveryPerSecond = SanitizePositive(tuning.StaminaRecoveryPerSecond, 0.12f);
            tuning.StaminaStrikeCost = math.saturate(math.select(0.16f, tuning.StaminaStrikeCost, math.isfinite(tuning.StaminaStrikeCost)));
            tuning.SweetLieShadowGain = SanitizePositive(tuning.SweetLieShadowGain, 0.85f);
            tuning.SweetLieViewDotThreshold = math.saturate(math.select(0.58f, tuning.SweetLieViewDotThreshold, math.isfinite(tuning.SweetLieViewDotThreshold)));
            tuning.AmbushNodeRadiusMeters = SanitizePositive(tuning.AmbushNodeRadiusMeters, 38f);
            tuning.VisualOverkillGain = SanitizePositive(tuning.VisualOverkillGain, 1f);
            tuning.BiteHeadLocalOffset = SanitizePositive(tuning.BiteHeadLocalOffset, 9f);
            return tuning;
        }

        private ApexEmergencyStats ResolveEmergencyStats()
        {
            ApexEmergencyStats stats = ApexBrainDefaults.BuildEmergencyMockStats();
            if (EmergencyStats.IsCreated && EmergencyStats.Length > 0)
                stats = EmergencyStats[0];
            return stats;
        }

        private MockWorldSampler ResolveSampler(in ApexBrainTuning tuning)
        {
            MockWorldSampler sampler = ApexBrainDefaults.BuildEmergencyMockWorldSampler();
            if (WorldSampler.IsCreated && WorldSampler.Length > 0)
                sampler = WorldSampler[0];

            sampler.CaveRadiusMeters = SanitizePositive(sampler.CaveRadiusMeters, 36f);
            sampler.GradientProbeMeters = SanitizePositive(sampler.GradientProbeMeters, 2f);
            sampler.SpatialCellSizeMeters = SanitizePositive(sampler.SpatialCellSizeMeters, 16f);
            sampler.WallRepulsionGain = SanitizePositive(sampler.WallRepulsionGain, 0.85f);
            sampler.SdfSoftMarginMeters = SanitizePositive(sampler.SdfSoftMarginMeters, 6f);
            sampler.HeadOffsetMeters = SanitizePositive(sampler.HeadOffsetMeters, tuning.HeadOffsetMeters);
            sampler.MidOffsetMeters = SanitizePositive(sampler.MidOffsetMeters, tuning.MidOffsetMeters);
            sampler.TailOffsetMeters = SanitizePositive(sampler.TailOffsetMeters, tuning.TailOffsetMeters);
            return sampler;
        }

        private AcousticSelection ResolveAcousticMemory(double3 selfAup, in MockPlayerAUP target, in ApexBrainTuning tuning, int acousticTapLimit)
        {
            AcousticSelection result = default;
            result.Hash = target.TargetHash ^ 0xAC0511u;
            int limit = AcousticTaps.IsCreated ? math.min(math.max(0, acousticTapLimit), AcousticTaps.Length) : 0;
            for (int i = 0; i < limit; i++)
            {
                AcousticEchoTap tap = AcousticTaps[i];
                if (!math.all(math.isfinite(tap.AUP)) || !math.isfinite(tap.Magnitude01) || !math.isfinite(tap.AgeSeconds))
                    continue;

                float age = math.max(0f, tap.AgeSeconds);
                float decay = math.saturate(1f - (age * 0.2f));
                float score = math.saturate(tap.Magnitude01 * tuning.AcousticSensitivity * decay);
                if (score <= result.Score01)
                    continue;

                result.Score01 = score;
                result.LocalPosition = DowncastAupDelta(tap.AUP - selfAup);
                result.Hash = math.select(tap.SourceHash ^ 0xAC0511u, tap.AcousticMemoryHash, tap.AcousticMemoryHash != 0u);
            }

            if (result.Score01 <= ApexBrainConstants.Epsilon && target.AcousticMagnitude01 > ApexBrainConstants.Epsilon)
            {
                result.Score01 = math.saturate(target.AcousticMagnitude01 * tuning.AcousticSensitivity);
                result.LocalPosition = DowncastAupDelta(target.AUP - selfAup);
                result.Hash = target.TargetHash ^ 0xAC0511u;
            }

            return result;
        }

        private AmbushSelection ResolveAmbushNodes(
            int slot,
            float3 interceptLocal,
            float3 pursuitDirection,
            in MockWorldSampler sampler,
            in ApexBrainTuning tuning,
            float quality,
            float sweetLieShadow,
            int fullNodeCount,
            int evaluatedNodeCount,
            float fractionalNode)
        {
            AmbushSelection best = default;
            best.Direction = pursuitDirection;
            best.LocalPosition = interceptLocal;
            float3 up = new float3(0f, 1f, 0f);
            float3 lateral = NormalizeSafe(math.cross(up, pursuitDirection), new float3(1f, 0f, 0f));
            float3 vertical = NormalizeSafe(math.cross(pursuitDirection, lateral), up);
            float radius = tuning.AmbushNodeRadiusMeters * math.lerp(0.45f, 1.25f, quality);
            int baseIndex = slot * ApexBrainConstants.MaxAmbushNodes;

            for (int i = 0; i < ApexBrainConstants.MaxAmbushNodes; i++)
            {
                if (i >= evaluatedNodeCount)
                {
                    int staleRow = baseIndex + i;
                    if (AmbushNodeScratch.IsCreated && (uint)staleRow < (uint)AmbushNodeScratch.Length)
                        AmbushNodeScratch[staleRow] = float3.zero;
                    if (InfluenceNodes.IsCreated && (uint)staleRow < (uint)InfluenceNodes.Length)
                        InfluenceNodes[staleRow] = default;
                    continue;
                }

                float fractionalWeight = math.select(1f, fractionalNode, i >= fullNodeCount);
                uint nodeSeed = ((uint)slot * 73856093u) ^ ((uint)i * 19349663u);
                float2 ring = ResolveOctantUnit(i);
                float radialScale = math.lerp(0.82f, 1.18f, HashToUnit(nodeSeed));
                float forwardLane = math.select(-1f, 1f, (i & 8) != 0);
                float forwardOffset = forwardLane * radius * math.lerp(0.08f, 0.22f, quality);
                float3 candidate = interceptLocal +
                                   (lateral * ring.x * radius * radialScale) +
                                   (vertical * ring.y * radius * 0.35f * radialScale) -
                                   (pursuitDirection * forwardOffset);
                SdfSample sdf = SampleMockSdf(sampler, candidate);
                float sdfSafety = math.saturate(sdf.Distance * math.rcp(math.max(sampler.SdfSoftMarginMeters, ApexBrainConstants.Epsilon)));
                float flank = math.saturate(1f - math.abs(math.dot(NormalizeSafe(candidate, pursuitDirection), pursuitDirection)));
                float score = math.saturate(((flank * 0.45f) + (sdfSafety * 0.35f) + (sweetLieShadow * 0.2f)) * fractionalWeight);
                uint hash = HashSpatial(candidate, math.max(sampler.SpatialCellSizeMeters, 1f));
                float3 direction = NormalizeSafe(candidate, pursuitDirection);

                if (AmbushNodeScratch.IsCreated && (uint)(baseIndex + i) < (uint)AmbushNodeScratch.Length)
                    AmbushNodeScratch[baseIndex + i] = candidate;

                if (InfluenceNodes.IsCreated && (uint)(baseIndex + i) < (uint)InfluenceNodes.Length)
                {
                    InfluenceNodes[baseIndex + i] = new ApexInfluenceNode
                    {
                        LocalPosition = candidate,
                        Score = score,
                        Direction = direction,
                        SpatialHash = hash,
                        SdfSafety01 = sdfSafety,
                        SweetLieWeight01 = sweetLieShadow,
                        FractionalWeight01 = fractionalWeight,
                        NodeIndex = (uint)i,
                        Flags = (1f - math.step(0.3f, quality)) > 0f ? ApexBrainFlags.LowQualityCollapse : 0u
                    };
                }

                if (score > best.Score01)
                {
                    best.Score01 = score;
                    best.LocalPosition = candidate;
                    best.Direction = direction;
                    best.Hash = hash;
                }
            }

            return best;
        }

        private void WriteDormantRow(
            int index,
            ushort slot,
            in MockPlayerAUP target,
            in ApexBrainTuning tuning)
        {
            ApexStateDTO state = default;
            state.Stamina = 1f;
            state.TargetHash = target.TargetHash;
            States[index] = state;

            ApexBrainOutputDTO output = default;
            output.Slot = slot;
            output.Phase = ApexBrainPhase.Dormant;
            output.TargetHash = target.TargetHash;
            output.TerrorRadiusMeters = tuning.TerrorRadius;
            output.StateHash = BuildStateHash(index, ApexBrainPhase.Dormant, 0, 0f, 0u, Frame);
            Outputs[index] = output;

            if (ProximitySignals.IsCreated && (uint)index < (uint)ProximitySignals.Length)
                ProximitySignals[index] = default;
            if (CombatDamageSignals.IsCreated && (uint)index < (uint)CombatDamageSignals.Length)
                CombatDamageSignals[index] = default;
            if (PanicSignals.IsCreated && (uint)index < (uint)PanicSignals.Length)
                PanicSignals[index] = default;
            ClearAmbushRows(index);

            WriteTelemetry(index, slot, output, target, tuning, ApexBrainPhase.Dormant, 0, output.StateHash, 0u, 0);
        }

        private void WriteFaultRow(
            int index,
            ushort slot,
            ApexStateDTO state,
            in MockPlayerAUP target,
            in ApexBrainTuning tuning,
            byte flags,
            uint faultCode)
        {
            if (!math.all(math.isfinite(state.AUP)))
                state.AUP = default;
            state.Velocity = float3.zero;
            state.AggressionLevel = 0f;
            state.TargetHash = target.TargetHash;
            state.AcousticMemoryHash = 0u;
            state.Stamina = 1f;
            States[index] = state;

            uint stateHash = BuildStateHash(index, ApexBrainPhase.Dormant, flags, 0f, 0u, Frame);
            ApexBrainOutputDTO output = default;
            output.Slot = slot;
            output.Phase = ApexBrainPhase.Dormant;
            output.Flags = flags;
            output.TargetHash = target.TargetHash;
            output.TerrorRadiusMeters = tuning.TerrorRadius;
            output.StateHash = stateHash;
            Outputs[index] = output;

            if (ProximitySignals.IsCreated && (uint)index < (uint)ProximitySignals.Length)
                ProximitySignals[index] = default;
            if (CombatDamageSignals.IsCreated && (uint)index < (uint)CombatDamageSignals.Length)
                CombatDamageSignals[index] = default;
            if (PanicSignals.IsCreated && (uint)index < (uint)PanicSignals.Length)
                PanicSignals[index] = default;
            ClearAmbushRows(index);

            WriteTelemetry(index, slot, output, target, tuning, ApexBrainPhase.Dormant, flags, stateHash, faultCode, 0);
        }

        private void ClearAmbushRows(int slot)
        {
            int baseIndex = slot * ApexBrainConstants.MaxAmbushNodes;
            for (int i = 0; i < ApexBrainConstants.MaxAmbushNodes; i++)
            {
                int row = baseIndex + i;
                if (AmbushNodeScratch.IsCreated && (uint)row < (uint)AmbushNodeScratch.Length)
                    AmbushNodeScratch[row] = float3.zero;
                if (InfluenceNodes.IsCreated && (uint)row < (uint)InfluenceNodes.Length)
                    InfluenceNodes[row] = default;
            }
        }

        private void WriteSignals(
            int index,
            ushort slot,
            in ApexStateDTO state,
            in MockPlayerAUP target,
            in ApexBrainOutputDTO output,
            in ApexBrainTuning tuning,
            byte phase,
            byte flags,
            float3 desiredDirection)
        {
            if (ProximitySignals.IsCreated && (uint)index < (uint)ProximitySignals.Length)
            {
                ApexProximitySignal proximity = new ApexProximitySignal
                {
                    SourceAup = state.AUP,
                    Aggression01 = output.AggressionLevel,
                    TerrorRadiusMeters = tuning.TerrorRadius,
                    Rumble01 = math.saturate(output.AggressionLevel * (1f - output.SweetLieLos01)),
                    SourceHash = tuning.SourceHash,
                    Frame = Frame,
                    Slot = slot,
                    Phase = phase,
                    Flags = flags
                };
                ProximitySignals[index] = proximity;
                if (EnableSignalQueueWrites != 0 && proximity.Aggression01 > ApexBrainConstants.Epsilon)
                    ProximitySignalWriter.Enqueue(proximity);
            }

            bool strike = phase == ApexBrainPhase.Strike;
            if (CombatDamageSignals.IsCreated && (uint)index < (uint)CombatDamageSignals.Length)
            {
                MockCombatDamageSignal damage = new MockCombatDamageSignal
                {
                    TargetAup = target.AUP,
                    ImpactDirection = desiredDirection,
                    Magnitude = math.select(0f, tuning.BaseDamageMagnitude * output.AggressionLevel, strike & ((target.Flags & MockPlayerAupFlags.WfcBaseTarget) != 0u)),
                    TargetHash = target.TargetHash,
                    SourceHash = tuning.SourceHash,
                    Frame = Frame,
                    Slot = slot,
                    Flags = strike ? ApexBrainFlags.StrikeCommitted : (byte)0
                };
                CombatDamageSignals[index] = damage;
                if (EnableSignalQueueWrites != 0 && damage.Magnitude > ApexBrainConstants.Epsilon)
                    CombatDamageSignalWriter.Enqueue(damage);
            }

            if (PanicSignals.IsCreated && (uint)index < (uint)PanicSignals.Length)
            {
                GlobalPanicSignal panic = new GlobalPanicSignal
                {
                    SourceAup = state.AUP,
                    Direction = desiredDirection,
                    RadiusMeters = tuning.TerrorRadius,
                    Intensity01 = math.select(0f, math.saturate(output.AggressionLevel + output.StrikeUtility), strike),
                    SourceHash = tuning.SourceHash,
                    Frame = Frame,
                    Slot = slot,
                    Flags = strike ? ApexBrainFlags.StrikeCommitted : (byte)0
                };
                PanicSignals[index] = panic;
                if (EnableSignalQueueWrites != 0 && panic.Intensity01 > ApexBrainConstants.Epsilon)
                    PanicSignalWriter.Enqueue(panic);
            }
        }

        private void WriteTelemetry(
            int index,
            ushort slot,
            in ApexBrainOutputDTO output,
            in MockPlayerAUP target,
            in ApexBrainTuning tuning,
            byte phase,
            byte flags,
            uint stateHash,
            uint faultCode,
            int evaluatedAcousticTapCount)
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            int frameIndex = (int)(Frame % ApexBrainConstants.TelemetryFrames);
            int telemetryIndex = (frameIndex * ApexBrainConstants.MaxLeviathans) + math.min(index, ApexBrainConstants.MaxLeviathans - 1);
            if ((uint)telemetryIndex >= (uint)TelemetryRing.Length)
                return;

            float resolvedQuality = ResolveQuality(tuning.GlobalQualityWeight);
            TelemetryRing[telemetryIndex] = new ApexTelemetryEntry
            {
                Frame = Frame,
                StateHash = stateHash,
                SpatialHash = output.SpatialHash,
                AcousticMemoryHash = output.AcousticMemoryHash,
                InterceptLocal = output.InterceptLocal,
                AggressionLevel = output.AggressionLevel,
                DesiredVelocity = output.DesiredVelocity,
                SweetLieLos01 = output.SweetLieLos01,
                WallRepulsion = output.WallRepulsion,
                StrikeUtility = output.StrikeUtility,
                UtilityScores = new float4(output.StalkUtility, output.AmbushUtility, output.StrikeUtility, output.FractionalNodeWeight01),
                TargetHash = target.TargetHash,
                BiomeHash = target.BiomeHash,
                EvaluatedNodeCount = output.EvaluatedNodeCount,
                GlobalQualityWeight = resolvedQuality,
                ActiveLeviathans = math.select(0f, 1f, (flags & ApexBrainFlags.Active) != 0),
                InterceptComputeTimeMs = EstimateComputeTimeMs(output.EvaluatedNodeCount, evaluatedAcousticTapCount, resolvedQuality, flags),
                Slot = slot,
                Phase = phase,
                Flags = flags,
                FaultCode = faultCode
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveQuality(float globalQualityWeight)
        {
            return math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveDeltaTime(in ApexBrainTuning tuning, in MockPlayerAUP target)
        {
            float dt = math.select(tuning.SimulationTickDelta, target.SimulationTickDelta, math.isfinite(target.SimulationTickDelta) & target.SimulationTickDelta > 0f);
            return math.min(math.max(dt, ApexBrainConstants.Epsilon), 0.25f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBiomeMultiplier(in MockPlayerAUP target, in ApexBrainTuning tuning)
        {
            bool preferred = target.BiomeHash == tuning.PreferredBiomeHash ||
                             target.BiomeHash == ApexBrainConstants.AbyssalTrenchBiomeHash ||
                             (target.Flags & MockPlayerAupFlags.AbyssalTrench) != 0u;
            return math.select(1f, math.max(1f, tuning.BiomeAggressionMultiplier), preferred);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ResolveAcousticTapLimit(float qualityCurve)
        {
            if (!AcousticTaps.IsCreated || AcousticTapCount <= 0)
                return 0;

            int maxTaps = math.min(math.max(0, AcousticTapCount), AcousticTaps.Length);
            float tapFloat = math.lerp(4f, ApexBrainConstants.MaxAcousticTaps, math.saturate(qualityCurve));
            int resolved = math.clamp((int)math.round(tapFloat), 1, ApexBrainConstants.MaxAcousticTaps);
            return math.min(maxTaps, resolved);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveRepulsion(in SdfSample sample, in MockWorldSampler sampler)
        {
            float pressure = math.saturate((sampler.SdfSoftMarginMeters - sample.Distance) * math.rcp(math.max(sampler.SdfSoftMarginMeters, ApexBrainConstants.Epsilon)));
            return sample.Gradient * pressure * sampler.WallRepulsionGain;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static SdfSample SampleMockSdf(in MockWorldSampler sampler, float3 localPosition)
        {
            float3 p = localPosition - sampler.OriginLocal;
            float radialSq = math.max((p.x * p.x) + (p.z * p.z), ApexBrainConstants.Epsilon);
            float radial = radialSq * math.rsqrt(radialSq);
            float sideDistance = sampler.CaveRadiusMeters - radial;
            float floorDistance = p.y - sampler.FloorY;
            float ceilingDistance = sampler.CeilingY - p.y;
            float distance = math.min(sideDistance, math.min(floorDistance, ceilingDistance));
            float3 radialGradient = new float3(-p.x, 0f, -p.z) * math.rsqrt(radialSq);
            float3 gradient = radialGradient;
            gradient = math.select(gradient, new float3(0f, 1f, 0f), floorDistance < sideDistance & floorDistance <= ceilingDistance);
            gradient = math.select(gradient, new float3(0f, -1f, 0f), ceilingDistance < sideDistance & ceilingDistance < floorDistance);
            gradient = NormalizeSafe(gradient, -NormalizeSafe(p, new float3(0f, 0f, 1f)));
            return new SdfSample { Distance = distance, Gradient = gradient };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 DowncastAupDelta(double3 delta)
        {
            double maxLocal = 200000d;
            double3 clamped = math.clamp(delta, new double3(-maxLocal), new double3(maxLocal));
            float3 local = new float3((float)clamped.x, (float)clamped.y, (float)clamped.z);
            return math.select(float3.zero, local, math.all(math.isfinite(local)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value) & value > ApexBrainConstants.Epsilon);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.select(fallback, value, math.all(math.isfinite(value)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            value = SanitizeFinite(value, fallback);
            float lengthSq = math.lengthsq(value);
            bool valid = lengthSq > ApexBrainConstants.Epsilon;
            float3 normalized = value * math.rsqrt(math.max(lengthSq, ApexBrainConstants.Epsilon));
            return math.select(fallback, normalized, valid & math.all(math.isfinite(normalized)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SmoothStep(float edge0, float edge1, float value)
        {
            return Smooth01((value - edge0) * math.rcp(math.max(edge1 - edge0, ApexBrainConstants.Epsilon)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EstimateComputeTimeMs(uint evaluatedNodeCount, int acousticTapCount, float quality, byte flags)
        {
            float nodeUs = math.min(evaluatedNodeCount, ApexBrainConstants.MaxAmbushNodes) * 0.72f;
            float acousticUs = math.min(math.max(0, acousticTapCount), ApexBrainConstants.MaxAcousticTaps) * 0.22f;
            float losProbeUs = math.step(0.28f, quality) * 0.38f;
            float midSampleUs = math.step(ApexBrainConstants.SdfMidsectionStartQuality, quality) * 0.75f;
            float tailSampleUs = ((flags & ApexBrainFlags.TailSdfSampled) != 0 ? 0.9f : 0f);
            float baseUs = 5.4f + (quality * 1.8f);
            return (baseUs + nodeUs + acousticUs + losProbeUs + midSampleUs + tailSampleUs) * 0.001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float2 ResolveOctantUnit(int index)
        {
            const float diagonal = 0.70710677f;
            switch (index & 7)
            {
                case 0: return new float2(1f, 0f);
                case 1: return new float2(diagonal, diagonal);
                case 2: return new float2(0f, 1f);
                case 3: return new float2(-diagonal, diagonal);
                case 4: return new float2(-1f, 0f);
                case 5: return new float2(-diagonal, -diagonal);
                case 6: return new float2(0f, -1f);
                default: return new float2(diagonal, -diagonal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashSpatial(float3 localPosition, float cellSize)
        {
            float invCell = math.rcp(math.max(cellSize, ApexBrainConstants.Epsilon));
            int3 cell = (int3)math.floor(localPosition * invCell);
            uint hash = 2166136261u;
            hash = (hash ^ (uint)cell.x) * 16777619u;
            hash = (hash ^ (uint)cell.y) * 16777619u;
            hash = (hash ^ (uint)cell.z) * 16777619u;
            return math.select(1u, hash, hash != 0u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashToUnit(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) * (1f / 16777215f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint BuildStateHash(int index, byte phase, byte flags, float aggression, uint spatialHash, uint frame)
        {
            uint hash = 0x53484E61u;
            hash ^= (uint)(index + 1) * 0x9E3779B9u;
            hash = (hash << 7) | (hash >> 25);
            hash ^= (uint)phase * 0x85EBCA6Bu;
            hash ^= (uint)flags * 0xC2B2AE35u;
            hash ^= (uint)math.asint(math.saturate(aggression)) * 0x27D4EB2Du;
            hash ^= spatialHash;
            hash ^= frame * 0x165667B1u;
            return math.select(0x53484E61u, hash, hash != 0u);
        }

        private struct SdfSample
        {
            public float Distance;
            public float3 Gradient;
        }

        private struct AcousticSelection
        {
            public float3 LocalPosition;
            public float Score01;
            public uint Hash;
        }

        private struct AmbushSelection
        {
            public float3 LocalPosition;
            public float3 Direction;
            public float Score01;
            public uint Hash;
        }
    }
}
