using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Animation.FaunaProcedural
{
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockAiVelocitySignalJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<MockAiVelocitySignal> Signals;
        public uint SectorHash;
        public uint SimulationFrame;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            if (!Signals.IsCreated || (uint)index >= (uint)Signals.Length)
                return;

            MockAiVelocitySignal signal = Signals[index];
            uint entityHash = signal.EntityHash != 0u ? signal.EntityHash : (uint)(index + 1) * 0x9E3779B9u;
            uint seed = entityHash ^ SectorHash ^ (SimulationFrame * 0x85EBCA6Bu) ^ 0xC2B2AE35u;
            if (seed == 0u)
                seed = 1u;

            Random rng = default;
            rng.InitState(seed);
            float quality = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));
            float cheapNoise = rng.NextFloat(-0.35f, 0.35f) * (1f - quality * 0.35f);
            float highNoise = rng.NextFloat(-0.08f, 0.08f) * quality;
            float speed = math.lerp(1.2f, 6.5f, quality) + cheapNoise + highNoise;

            float3 velocity = ProceduralBoneMath.Float3(cheapNoise, highNoise * 0.35f, speed);
            float deterministicTime = SimulationFrame * 0.016666668f;
            float phaseSeed = ((entityHash ^ SectorHash) & 1023u) * 0.006135923f;
            float swimPhase = deterministicTime * math.lerp(0.5f, 1.8f, quality) + phaseSeed;
            float lateral = ProceduralBoneMath.FastSin(swimPhase + index * 0.173f) * math.lerp(0.25f, 1.1f, quality);
            float3 target = ProceduralBoneMath.Float3(lateral, 0.25f + quality * 0.45f, 2.5f + speed * 0.35f);

            signal.VelocityLocal = velocity;
            signal.IkTargetLocal = target;
            signal.Weight01 = quality;
            signal.JawOpen01 = math.saturate(0.25f + quality * 0.55f);
            signal.EntityHash = entityHash;
            signal.SectorHash = SectorHash;
            signal.SimulationFrame = SimulationFrame;
            signal.Flags = ProceduralBoneBlenderConstants.TelemetryFlagMockSignal;
            signal.NoisePhase = phaseSeed;
            signal.SpeedHint = speed;
            signal._pad0 = 0UL;
            Signals[index] = signal;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProceduralBoneSolveJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<ProceduralBoneRigDTO> Rigs;
        [ReadOnly, NoAlias] public NativeArray<ProceduralBoneFrameInputDTO> Inputs;
        [ReadOnly, NoAlias] public NativeArray<int> ParentIndices;
        [ReadOnly, NoAlias] public NativeArray<float4x4> BindPoses;
        [NoAlias] public NativeArray<BoneStateDTO> BoneStates;
        [NoAlias] public NativeArray<float4x4> BoneMatrices;
        [NoAlias] public NativeArray<ProceduralBoneFrameStatsDTO> Stats;
        [ReadOnly, NoAlias] public NativeArray<MockAiVelocitySignal> MockSignals;
        [ReadOnly, NoAlias] public NativeArray<ProceduralBoneRigTuningDTO> Tuning;
        public float GlobalQualityWeight;
        public float DeltaTime;
        public float SimulationTime;
        public uint SimulationFrame;

        public void Execute(int index)
        {
            if (!Rigs.IsCreated ||
                !Inputs.IsCreated ||
                !ParentIndices.IsCreated ||
                !BindPoses.IsCreated ||
                !BoneStates.IsCreated ||
                !BoneMatrices.IsCreated ||
                !Stats.IsCreated ||
                (uint)index >= (uint)Rigs.Length ||
                (uint)index >= (uint)Inputs.Length ||
                (uint)index >= (uint)Stats.Length)
            {
                return;
            }

            ProceduralBoneRigTuningDTO tuning = ResolveTuning();
            ProceduralBoneRigDTO rig = Rigs[index];
            ProceduralBoneFrameInputDTO input = Inputs[index];
            ProceduralBoneFrameStatsDTO stats = default;
            stats.Frame = SimulationFrame;
            stats.StateHash = rig.SkeletonHash;

            int boneStart = math.max(0, rig.BoneStart);
            int boneCount = math.min(math.max(0, rig.BoneCount), BoneMatrices.Length - boneStart);
            boneCount = math.min(boneCount, ParentIndices.Length - boneStart);
            boneCount = math.min(boneCount, BindPoses.Length - boneStart);
            boneCount = math.min(boneCount, BoneStates.Length - boneStart);
            if (boneCount <= 0 || boneStart < 0)
            {
                stats.Flags = ProceduralBoneBlenderConstants.TelemetryFlagInvalid;
                stats.InvalidMathCount = 1;
                Stats[index] = stats;
                return;
            }

            float visible = math.saturate(math.select(0f, input.Visible01, math.isfinite(input.Visible01)));
            bool rigEnabled = (rig.Flags & ProceduralBoneBlenderConstants.RigFlagVisible) != 0u;
            bool visibleFlag = (input.Flags & ProceduralBoneBlenderConstants.InputFlagVisible) != 0u;
            if (!rigEnabled || !visibleFlag || visible <= 0.001f)
            {
                stats.CulledSkeletons = 1;
                stats.Flags = 0u;
                Stats[index] = stats;
                return;
            }

            float inputQuality = math.isfinite(input.GlobalQualityWeight)
                ? input.GlobalQualityWeight
                : GlobalQualityWeight;
            float quality = math.saturate(math.min(
                math.select(1f, inputQuality, math.isfinite(inputQuality)),
                tuning.GlobalQualityWeight));
            float qualityCurve = ProceduralBoneMath.Smooth01(quality);
            int primaryCount = math.clamp(rig.PrimaryBoneCount, 1, boneCount);
            int secondaryCount = math.max(0, boneCount - primaryCount);
            float secondaryCurve = ProceduralBoneMath.SmoothRange01(quality, tuning.SecondaryBoneStart01, 1f);
            int activeBoneCount = math.clamp(primaryCount + (int)math.floor(secondaryCount * secondaryCurve + 0.0001f), 1, boneCount);
            uint flags = ProceduralBoneBlenderConstants.TelemetryFlagVisible;
            if (activeBoneCount < boneCount)
                flags |= ProceduralBoneBlenderConstants.TelemetryFlagQualityCollapse;

            MockAiVelocitySignal mock = default;
            float mockBlend = 0f;
            if (MockSignals.IsCreated && (uint)index < (uint)MockSignals.Length)
            {
                mock = MockSignals[index];
                mockBlend = math.saturate(tuning.MockSignalWeight * mock.Weight01);
                if (mockBlend > 0.0001f)
                    flags |= ProceduralBoneBlenderConstants.TelemetryFlagMockSignal;
            }

            float3 velocity = ProceduralBoneMath.SanitizeFinite(input.VelocityLocal, float3.zero);
            velocity = math.lerp(velocity, ProceduralBoneMath.SanitizeFinite(mock.VelocityLocal, velocity), mockBlend);
            float speed = ProceduralBoneMath.LengthSafe(velocity);
            float dt = math.clamp(
                math.select(DeltaTime, input.SimulationTickDelta, math.isfinite(input.SimulationTickDelta) && input.SimulationTickDelta > 0f),
                ProceduralBoneBlenderConstants.MinDeltaTime,
                ProceduralBoneBlenderConstants.MaxDeltaTime);
            float localSimulationTime = math.select(
                math.max(0f, SimulationTime),
                input.SimulationTime,
                math.isfinite(input.SimulationTime) && input.SimulationTime > 0f);
            if ((input.Flags & ProceduralBoneBlenderConstants.InputFlagTraumaImpulse) != 0u)
                rig.TraumaSeconds = math.max(rig.TraumaSeconds, 0.5f);

            float targetWaveSpeed = ProceduralBoneSanitizer.SanitizePositive(rig.BaseWaveSpeed, tuning.SineFrequency) +
                                    speed * ProceduralBoneSanitizer.SanitizePositive(rig.VelocityWaveMultiplier, 0.25f);
            float targetAmplitude = ProceduralBoneSanitizer.SanitizePositive(rig.BaseAmplitudeRadians, tuning.WaveAmplitudeRadians) *
                                    math.lerp(0.45f, 1.35f, math.saturate(speed * 0.12f)) *
                                    math.lerp(0.55f, 1f, qualityCurve);
            float oscillatorHz = ProceduralBoneSanitizer.SanitizePositive(rig.NaturalFrequencyHz, tuning.DampingHz);
            float damping = math.clamp(
                math.select(0.82f, rig.DampingRatio, math.isfinite(rig.DampingRatio) && rig.DampingRatio > 0f),
                0.05f,
                2.5f);

            ProceduralBoneMath.StepDampedOscillator(
                ref rig.WaveSpeedState,
                ref rig.WaveSpeedVelocityState,
                targetWaveSpeed,
                oscillatorHz,
                damping,
                dt);
            ProceduralBoneMath.StepDampedOscillator(
                ref rig.AmplitudeState,
                ref rig.AmplitudeVelocityState,
                targetAmplitude,
                oscillatorHz,
                damping,
                dt);
            rig.WaveSpeedState = ProceduralBoneMath.SanitizeScalar(rig.WaveSpeedState, targetWaveSpeed);
            rig.AmplitudeState = ProceduralBoneMath.SanitizeScalar(rig.AmplitudeState, targetAmplitude);
            rig.TraumaSeconds = math.max(0f, rig.TraumaSeconds - dt);

            quaternion rootRotation = ProceduralBoneMath.SanitizeRotation(input.RootRotation);
            if (rig.TraumaSeconds > 0f)
            {
                float trauma01 = math.saturate(rig.TraumaSeconds * 2f);
                float traumaPhase = localSimulationTime * tuning.TraumaFrequencyHz * ProceduralBoneBlenderConstants.TwoPi;
                float traumaAngle = ProceduralBoneMath.FastSin(traumaPhase + rig.StableSeed * 0.00017f) *
                                    tuning.TraumaAmplitudeRadians *
                                    trauma01;
                rootRotation = math.mul(rootRotation, ProceduralBoneMath.FastSmallAngleRotation(ProceduralBoneMath.Forward(), traumaAngle));
                flags |= ProceduralBoneBlenderConstants.RigFlagTrauma;
            }

            float3 rootPosition = ProceduralBoneMath.SanitizeFinite(input.RootLocalPosition, float3.zero);
            float rootScale = input.BaseScaleOverride > 0f && math.isfinite(input.BaseScaleOverride)
                ? input.BaseScaleOverride
                : ProceduralBoneSanitizer.SanitizePositive(rig.BaseScale, 1f);
            float3 scale = ProceduralBoneMath.Float3(rootScale, rootScale, rootScale);
            float4x4 rootMatrix = float4x4.TRS(rootPosition, rootRotation, scale);
            float waveSpeed = math.max(0f, rig.WaveSpeedState);
            float amplitude = math.max(0f, rig.AmplitudeState);
            int matricesComputed = 0;
            int invalidCount = 0;

            for (int relativeIndex = 0; relativeIndex < boneCount; relativeIndex++)
            {
                int boneIndex = boneStart + relativeIndex;
                bool activeBone = relativeIndex < activeBoneCount;
                if (!activeBone)
                {
                    int collapsedParent = ParentIndices[boneIndex];
                    float4x4 collapsedMatrix = collapsedParent >= boneStart && collapsedParent < boneIndex
                        ? BoneMatrices[collapsedParent]
                        : rootMatrix;
                    BoneStateDTO collapsedState = default;
                    collapsedState.LocalMatrix = float4x4.identity;
                    collapsedState.Phase = 0f;
                    collapsedState.BoneHash = rig.SkeletonHash ^ (uint)relativeIndex * 0x9E3779B9u;
                    collapsedState._pad0 = 0UL;
                    BoneStates[boneIndex] = collapsedState;
                    BoneMatrices[boneIndex] = collapsedMatrix;
                    continue;
                }

                float phaseOffset = ProceduralBoneSanitizer.SanitizePositive(rig.PhaseOffset, tuning.PhaseOffset);
                float phase = localSimulationTime * waveSpeed - relativeIndex * phaseOffset + (rig.StableSeed & 1023u) * 0.006135923f;
                float sine = ProceduralBoneMath.FastSin(phase);
                float overkill = ProceduralBoneMath.SmoothRange01(quality, 0.55f, 1f);
                float harmonic = ProceduralBoneMath.FastSin(phase * 2.03f + relativeIndex * 0.37f) * (0.22f * overkill);
                float angle = (sine + harmonic) * amplitude;
                quaternion localRotation = quaternion.EulerXYZ(angle * 0.18f, angle, angle * 0.08f);

                int jawRelative = rig.JawBoneIndex;
                if (jawRelative == relativeIndex && (rig.Flags & ProceduralBoneBlenderConstants.RigFlagHasJaw) != 0u)
                {
                    float jawWeight = math.saturate(tuning.JawIkWeight * ProceduralBoneMath.SmoothRange01(quality, 0.35f, 1f));
                    if (jawWeight > 0.0001f)
                    {
                        float3 target = ProceduralBoneMath.SanitizeFinite(input.JawTargetLocal, ProceduralBoneMath.Forward());
                        target = math.lerp(target, ProceduralBoneMath.SanitizeFinite(mock.IkTargetLocal, target), mockBlend);
                        float3 bindPosition = BindPoses[boneIndex].c3.xyz;
                        float3 direction = ProceduralBoneMath.NormalizeSafe(target - bindPosition, ProceduralBoneMath.Forward());
                        quaternion aim = quaternion.LookRotationSafe(direction, ProceduralBoneMath.Up());
                        float jawOpen = math.saturate(math.lerp(input.JawOpen01, mock.JawOpen01, mockBlend));
                        quaternion open = ProceduralBoneMath.FastSmallAngleRotation(ProceduralBoneMath.Right(), jawOpen * 0.38f * jawWeight);
                        localRotation = ProceduralBoneMath.FastNlerp(localRotation, math.mul(aim, open), jawWeight);
                        flags |= ProceduralBoneBlenderConstants.TelemetryFlagJawSolved;
                    }
                }

                float4x4 localMatrix = relativeIndex == 0
                    ? rootMatrix
                    : math.mul(
                        BindPoses[boneIndex],
                        float4x4.TRS(float3.zero, localRotation, ProceduralBoneMath.Float3(1f, 1f, 1f)));
                int parent = ParentIndices[boneIndex];
                float4x4 globalMatrix = relativeIndex == 0 || parent < boneStart || parent >= boneIndex
                    ? localMatrix
                    : math.mul(BoneMatrices[parent], localMatrix);

                if (!ProceduralBoneMath.IsFinite(globalMatrix))
                {
                    globalMatrix = relativeIndex == 0 ? float4x4.identity : BoneMatrices[math.max(boneStart, boneIndex - 1)];
                    invalidCount++;
                    flags |= ProceduralBoneBlenderConstants.TelemetryFlagInvalid;
                }

                BoneStateDTO state = default;
                state.LocalMatrix = localMatrix;
                state.Phase = phase;
                state.BoneHash = rig.SkeletonHash ^ (uint)relativeIndex * 0x9E3779B9u;
                state._pad0 = 0UL;
                BoneStates[boneIndex] = state;
                BoneMatrices[boneIndex] = globalMatrix;
                matricesComputed++;
            }

            Rigs[index] = rig;

            stats.ActiveSkeletons = 1;
            stats.MatricesComputed = matricesComputed;
            stats.InvalidMathCount = invalidCount;
            stats.CulledSkeletons = 0;
            stats.MaxMatrixIndexPlusOne = boneStart + boneCount;
            stats.Quality = quality;
            uint stateHash = rig.SkeletonHash;
            stateHash = ProceduralBoneMath.Hash(stateHash, (uint)matricesComputed);
            stateHash = ProceduralBoneMath.Hash(stateHash, (uint)activeBoneCount);
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(localSimulationTime));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(waveSpeed));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(amplitude));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(quality));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(rootPosition.x));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(rootPosition.y));
            stateHash = ProceduralBoneMath.Hash(stateHash, math.asuint(rootPosition.z));
            stateHash = ProceduralBoneMath.Hash(stateHash, flags);
            stats.StateHash = stateHash;
            stats.Flags = flags;
            stats.MaxWaveSpeed = waveSpeed;
            stats.AverageActiveBones = activeBoneCount;
            stats.LastRootLocal = rootPosition;
            Stats[index] = stats;
        }

        private ProceduralBoneRigTuningDTO ResolveTuning()
        {
            if (Tuning.IsCreated && Tuning.Length > 0)
                return ProceduralBoneSanitizer.SanitizeTuning(Tuning[0]);

            return ProceduralBoneRigTuningDTO.Default();
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct ProceduralBoneTelemetryReduceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<ProceduralBoneFrameStatsDTO> Stats;
        [NoAlias] public NativeArray<ProceduralBoneTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public int ActiveSkeletonCount;
        public uint SimulationFrame;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Stats.IsCreated ||
                !TelemetryRing.IsCreated ||
                !TelemetryCursor.IsCreated ||
                TelemetryRing.Length <= 0 ||
                TelemetryCursor.Length <= 0)
            {
                return;
            }

            int count = math.min(math.max(0, ActiveSkeletonCount), Stats.Length);
            ProceduralBoneTelemetryEntry entry = default;
            entry.Frame = SimulationFrame;
            entry.GlobalQualityWeight = math.saturate(math.select(1f, GlobalQualityWeight, math.isfinite(GlobalQualityWeight)));

            float qualitySum = 0f;
            float activeBoneSum = 0f;
            uint stateHash = 0x68A11B0Du;
            for (int i = 0; i < count; i++)
            {
                ProceduralBoneFrameStatsDTO stats = Stats[i];
                entry.ActiveSkeletons += stats.ActiveSkeletons;
                entry.MatricesComputed += stats.MatricesComputed;
                entry.InvalidMathCount += stats.InvalidMathCount;
                entry.CulledSkeletons += stats.CulledSkeletons;
                entry.MatrixUploadCount = math.max(entry.MatrixUploadCount, stats.MaxMatrixIndexPlusOne);
                entry.Flags |= stats.Flags;
                entry.MaxWaveSpeed = math.max(entry.MaxWaveSpeed, stats.MaxWaveSpeed);
                activeBoneSum += stats.AverageActiveBones;
                qualitySum += stats.Quality;
                if (stats.ActiveSkeletons > 0)
                    entry.LastRootLocal = stats.LastRootLocal;
                stateHash = ProceduralBoneMath.Hash(stateHash, stats.StateHash);
            }

            float activeDenom = math.max(1f, entry.ActiveSkeletons);
            entry.AverageActiveBones = activeBoneSum / activeDenom;
            entry.GlobalQualityWeight = count > 0 ? math.saturate(qualitySum / math.max(1f, count)) : entry.GlobalQualityWeight;
            entry.KinematicComputeTimeMs = entry.MatricesComputed * 0.000002f;
            entry.StateHash = stateHash;
            int cursor = TelemetryCursor[0];
            int index = ProceduralBoneMath.PositiveModulo(cursor, TelemetryRing.Length);
            TelemetryRing[index] = entry;
            TelemetryCursor[0] = cursor + 1;
        }
    }

    public static class ProceduralBoneMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Float3(float x, float y, float z)
        {
            float3 value = default;
            value.x = x;
            value.y = y;
            value.z = z;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Up()
        {
            return Float3(0f, 1f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Right()
        {
            return Float3(1f, 0f, 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 Forward()
        {
            return Float3(0f, 0f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothRange01(float value, float start, float end)
        {
            float denom = math.max(end - start, ProceduralBoneBlenderConstants.MinDenominator);
            return Smooth01((value - start) / denom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FastSin(float value)
        {
            if (!math.isfinite(value))
                return 0f;

            float wrapped = value - math.floor((value + math.PI) / ProceduralBoneBlenderConstants.TwoPi) * ProceduralBoneBlenderConstants.TwoPi;
            float parabola = 1.2732395447351627f * wrapped - 0.4052847345693511f * wrapped * math.abs(wrapped);
            return 0.225f * (parabola * math.abs(parabola) - parabola) + parabola;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StepDampedOscillator(
            ref float position,
            ref float velocity,
            float target,
            float frequencyHz,
            float dampingRatio,
            float dt)
        {
            float omega = math.max(ProceduralBoneBlenderConstants.MinDenominator, frequencyHz * ProceduralBoneBlenderConstants.TwoPi);
            float damping = math.max(0.01f, dampingRatio);
            float f = 1f + 2f * dt * damping * omega;
            float oo = omega * omega;
            float hoo = dt * oo;
            float hhoo = dt * hoo;
            float detInv = math.rcp(math.max(f + hhoo, ProceduralBoneBlenderConstants.MinDenominator));
            float detX = f * position + dt * velocity + hhoo * target;
            float detV = velocity + hoo * (target - position);
            position = detX * detInv;
            velocity = detV * detInv;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LengthSafe(float3 value)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > ProceduralBoneBlenderConstants.MinDenominator
                ? lengthSq * math.rsqrt(math.max(lengthSq, ProceduralBoneBlenderConstants.MinDenominator))
                : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return lengthSq > ProceduralBoneBlenderConstants.MinDenominator
                ? value * math.rsqrt(math.max(lengthSq, ProceduralBoneBlenderConstants.MinDenominator))
                : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 SanitizeFinite(float3 value, float3 fallback)
        {
            return math.all(math.isfinite(value)) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeScalar(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion SanitizeRotation(quaternion rotation)
        {
            if (!math.all(math.isfinite(rotation.value)) ||
                math.lengthsq(rotation.value) <= ProceduralBoneBlenderConstants.MinDenominator)
            {
                return quaternion.identity;
            }

            return math.normalize(rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FastSmallAngleRotation(float3 axis, float radians)
        {
            float safeRadians = math.select(0f, radians, math.isfinite(radians));
            float half = safeRadians * 0.5f;
            float3 safeAxis = NormalizeSafe(axis, Forward());
            quaternion rotation = default;
            rotation.value = new float4(safeAxis * half, math.max(0f, 1f - (half * half * 0.5f)));
            return SanitizeRotation(rotation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion FastNlerp(quaternion a, quaternion b, float t)
        {
            float4 av = a.value;
            float4 bv = b.value;
            float sign = math.select(-1f, 1f, math.dot(av, bv) >= 0f);
            float4 blended = av + (bv * sign - av) * math.saturate(t);
            float lengthSq = math.lengthsq(blended);
            if (!math.all(math.isfinite(blended)) || lengthSq <= ProceduralBoneBlenderConstants.MinDenominator)
                return SanitizeRotation(a);

            quaternion result = default;
            result.value = blended * math.rsqrt(math.max(lengthSq, ProceduralBoneBlenderConstants.MinDenominator));
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinite(float4x4 value)
        {
            return math.all(math.isfinite(value.c0)) &&
                   math.all(math.isfinite(value.c1)) &&
                   math.all(math.isfinite(value.c2)) &&
                   math.all(math.isfinite(value.c3));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash(uint state, uint value)
        {
            uint hash = state ^ value;
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35u;
            return hash ^ (hash >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PositiveModulo(int value, int length)
        {
            int safeLength = math.max(1, length);
            int result = value % safeLength;
            return result < 0 ? result + safeLength : result;
        }
    }
}
