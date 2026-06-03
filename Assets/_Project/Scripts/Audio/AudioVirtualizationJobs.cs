using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using AcousticAup = Hecton8.Core.Contracts.AcousticAup;

namespace Hecton8.Audio.Virtualization
{
    /// <summary>
    /// Burst ranking pass for virtual acoustic emitters.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct VirtualVoiceSortJob : IJob
    {
        [NoAlias] public NativeArray<VirtualVoice> Voices;
        [NoAlias] public NativeArray<VirtualVoiceSortKey> SortKeys;
        [WriteOnly, NoAlias] public NativeArray<VirtualVoiceSelection> Selections;
        [WriteOnly, NoAlias] public NativeArray<VirtualVoiceStatistics> Statistics;
        public AcousticAup ListenerAup;
        public float3 ListenerVelocityMetersPerSecond;
        public MockSDFSampler SdfSampler;
        public float DefaultSabineRt60Seconds;
        public float DefaultSabineRoomVolumeCubicMeters;
        public float SoundSpeedMetersPerSecond;
        public float GlobalOcclusionPenalty;
        public float OccludedLowPassHertz;
        public float SabineDecayScale;
        public int PhysicalVoiceLimit;
        public int VoiceCount;
        public int DroppedVoiceCount;
        public int Frame;
        public int DisableSdfOcclusion;
        public float MinimumAudibleEnergy;
        public float GlobalQualityWeight;
        public float DepthLowPassHertz;
        public int RollbackActive;

        public void Execute()
        {
            int sortKeyCapacity = SortKeys.IsCreated ? SortKeys.Length : 0;
            int totalVoices = math.clamp(VoiceCount, 0, math.min(Voices.Length, sortKeyCapacity));
            int culledVoices = 0;
            int audibleCount = 0;
            int occludedCount = 0;
            int delayedCount = 0;
            float qualityWeight = math.saturate(VirtualVoiceUtility.SanitizeFinite(GlobalQualityWeight, 0f));
            int budgetLimit = VirtualVoiceUtility.ResolveContinuousVoiceBudget(qualityWeight);
            int safeLimit = math.clamp(math.min(PhysicalVoiceLimit, budgetLimit), 0, Selections.Length);
            float depthLowPass = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(DepthLowPassHertz, VirtualVoiceUtility.OpenLowPassHertz),
                80f,
                VirtualVoiceUtility.OpenLowPassHertz);
            float minAudibleEnergy = math.max(0f, MinimumAudibleEnergy);
            float soundSpeed = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(SoundSpeedMetersPerSecond, VirtualVoiceUtility.DelaySpeedMetersPerSecond),
                250f,
                2000f);
            float occlusionPenalty = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(GlobalOcclusionPenalty, VirtualVoiceUtility.DearLieOccludedGain),
                0.03162278f,
                1f);
            float occludedLowPass = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(OccludedLowPassHertz, VirtualVoiceUtility.OccludedLowPassHertz),
                80f,
                VirtualVoiceUtility.OpenLowPassHertz);
            float sabineScale = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(SabineDecayScale, 1f),
                0.1f,
                4f);
            float rt60Sum = 0f;
            float lowPassSum = 0f;
            float maxDelay = 0f;

            float3 listenerVelocity = math.all(math.isfinite(ListenerVelocityMetersPerSecond))
                ? ListenerVelocityMetersPerSecond
                : float3.zero;

            for (int i = 0; i < totalVoices; i++)
            {
                VirtualVoice voice = Voices[i];
                if (!AcousticAup.IsFinite(in voice.SourceAup) ||
                    voice.FoveatedTier >= VirtualVoiceUtility.FoveatedTierFrozen)
                {
                    culledVoices++;
                    continue;
                }

                float volume = math.saturate(VirtualVoiceUtility.SanitizeFinite(voice.Volume, 0f));
                float importance = math.max(0f, VirtualVoiceUtility.SanitizeFinite(voice.Priority, 0f));
                float3 relative = AcousticAup.RelativeFloat3(in voice.SourceAup, in ListenerAup);
                float distanceSq = math.lengthsq(relative);
                if (!math.isfinite(distanceSq))
                    distanceSq = float.MaxValue * 0.25f;

                float attenuation = math.rcp(math.max(1f, distanceSq));
                float lowPass = math.clamp(
                    VirtualVoiceUtility.SanitizeFinite(voice.LowPassCutoffHz, VirtualVoiceUtility.OpenLowPassHertz),
                    80f,
                    VirtualVoiceUtility.OpenLowPassHertz);

                VirtualVoiceDspFlags flags = voice.DspFlags;
                bool occluded = (flags & VirtualVoiceDspFlags.SdfOccluded) != 0 ||
                    ResolveSdfLineOcclusion(relative, in SdfSampler, DisableSdfOcclusion, qualityWeight);
                if (occluded)
                {
                    volume *= occlusionPenalty;
                    lowPass = math.min(lowPass, occludedLowPass);
                    flags |= VirtualVoiceDspFlags.SdfOccluded;
                    occludedCount++;
                }

                if ((flags & VirtualVoiceDspFlags.InsideSubmarineHull) != 0)
                    lowPass = math.min(lowPass, VirtualVoiceUtility.HullLowPassHertz);
                lowPass = math.min(lowPass, depthLowPass);

                float effectiveVolume = volume * attenuation;
                float weight = effectiveVolume * importance;
                if (RollbackActive != 0)
                {
                    voice.Volume = 0f;
                    voice.Priority = importance;
                    voice.Pitch = math.clamp(VirtualVoiceUtility.SanitizeFinite(voice.Pitch, 1f), 0.1f, 3f);
                    voice.DopplerRatio = 1f;
                    voice.Attenuation = attenuation;
                    voice.DistanceSq = distanceSq;
                    voice.EffectiveVolume = 0f;
                    voice.Weight = 0f;
                    voice.SabineRt60Seconds = 0f;
                    voice.LowPassCutoffHz = lowPass;
                    voice.DelaySeconds = 0f;
                    voice.DspFlags = flags;
                    Voices[i] = voice;
                    culledVoices++;
                    continue;
                }

                if (importance <= 0f || effectiveVolume < minAudibleEnergy)
                {
                    culledVoices++;
                    continue;
                }

                float sabineRt60 = math.clamp(
                    ResolveSabineRt60(in voice) * sabineScale,
                    VirtualVoiceUtility.SabineMinimumRt60Seconds,
                    VirtualVoiceUtility.SabineMaximumRt60Seconds);
                float delaySeconds = math.max(
                    0f,
                    math.max(
                        VirtualVoiceUtility.SanitizeFinite(voice.DelaySeconds, 0f),
                        VirtualVoiceUtility.ComputeDelaySeconds(distanceSq, soundSpeed)));
                if (delaySeconds > 0.0001f)
                {
                    flags |= VirtualVoiceDspFlags.Delayed;
                    delayedCount++;
                }

                float3 sourceVelocity = math.all(math.isfinite(voice.SourceVelocityMetersPerSecond))
                    ? voice.SourceVelocityMetersPerSecond
                    : float3.zero;

                voice.Volume = volume;
                voice.Priority = importance;
                voice.Pitch = math.clamp(VirtualVoiceUtility.SanitizeFinite(voice.Pitch, 1f), 0.1f, 3f);
                voice.DopplerRatio = VirtualVoiceUtility.ComputeDopplerRatio(
                    relative,
                    sourceVelocity,
                    listenerVelocity,
                    voice.DopplerRatio,
                    soundSpeed);
                voice.Attenuation = attenuation;
                voice.DistanceSq = distanceSq;
                voice.EffectiveVolume = effectiveVolume;
                voice.Weight = weight;
                voice.SabineRt60Seconds = sabineRt60;
                voice.LowPassCutoffHz = lowPass;
                voice.DelaySeconds = delaySeconds;
                voice.DspFlags = flags | VirtualVoiceDspFlags.SabineResolved;
                int compactIndex = audibleCount;
                Voices[compactIndex] = voice;
                SortKeys[compactIndex] = new VirtualVoiceSortKey
                {
                    Weight = weight,
                    VoiceIndex = compactIndex,
                    StableKey = voice.StableKey,
                    Padding = 0u
                };
                audibleCount++;
                rt60Sum += sabineRt60;
                lowPassSum += lowPass;
                maxDelay = math.max(maxDelay, delaySeconds);
            }

            SortKeysDescending(SortKeys, audibleCount);
            int selectedCount = math.min(safeLimit, audibleCount);
            for (int i = 0; i < selectedCount; i++)
            {
                VirtualVoiceSortKey key = SortKeys[i];
                int voiceIndex = math.clamp(key.VoiceIndex, 0, math.max(0, audibleCount - 1));
                Selections[i] = CreateSelection(Voices[voiceIndex]);
            }

            for (int i = selectedCount; i < Selections.Length; i++)
                Selections[i] = default;

            if (Statistics.Length > 0)
            {
                float invAudible = audibleCount > 0 ? math.rcp((float)audibleCount) : 0f;
                Statistics[0] = new VirtualVoiceStatistics
                {
                    Frame = Frame,
                    TotalVoices = totalVoices,
                    AudibleVoices = audibleCount,
                    CulledVoices = culledVoices,
                    ActivePhysicalVoices = selectedCount,
                    PhysicalVoiceLimit = safeLimit,
                    StolenVoices = math.max(0, audibleCount - selectedCount),
                    DroppedVoices = math.max(0, DroppedVoiceCount),
                    OccludedVoices = occludedCount,
                    DelayedVoices = delayedCount,
                    SortTimeMs = 0f,
                    LoudestWeight = selectedCount > 0 ? SortKeys[0].Weight : 0f,
                    AverageRt60Seconds = rt60Sum * invAudible,
                    AverageLowPassHertz = lowPassSum * invAudible,
                    MaximumDelaySeconds = maxDelay,
                    AcousticOcclusionTimeMs = 0f
                };
            }
        }

        private static bool ResolveSdfLineOcclusion(float3 listenerToSource, in MockSDFSampler sampler, int disableSdfOcclusion, float qualityWeight)
        {
            if (disableSdfOcclusion != 0 || sampler.Enabled == 0)
                return false;

            int taps = VirtualVoiceUtility.ResolveSdfTapCount(qualityWeight);
            float invTaps = math.rcp(math.max(1f, taps));
            float solid = 0f;
            for (int tap = 0; tap < taps; tap++)
            {
                float t = ((float)tap + 0.5f) * invTaps;
                float signedDistance = sampler.Sample(listenerToSource * t);
                solid += 1f - math.step(0f, signedDistance);
            }

            float solid01 = solid * invTaps;
            return solid01 > math.lerp(0.95f, 0.08f, math.saturate(qualityWeight));
        }

        private float ResolveSabineRt60(in VirtualVoice voice)
        {
            float authoredRt60 = VirtualVoiceUtility.SanitizeFinite(voice.SabineRt60Seconds, 0f);
            if (authoredRt60 > 0f)
                return math.clamp(authoredRt60, VirtualVoiceUtility.SabineMinimumRt60Seconds, VirtualVoiceUtility.SabineMaximumRt60Seconds);

            float defaultRt60 = VirtualVoiceUtility.SanitizeFinite(DefaultSabineRt60Seconds, 0f);
            if (defaultRt60 > 0f)
                return math.clamp(defaultRt60, VirtualVoiceUtility.SabineMinimumRt60Seconds, VirtualVoiceUtility.SabineMaximumRt60Seconds);

            float roomVolume = VirtualVoiceUtility.SanitizeFinite(voice.SabineRoomVolumeCubicMeters, 0f);
            if (roomVolume <= 0f)
                roomVolume = VirtualVoiceUtility.SanitizeFinite(DefaultSabineRoomVolumeCubicMeters, 0f);

            return VirtualVoiceUtility.ComputeSabineRt60(roomVolume, voice.AcousticEnvironment);
        }

        private static void SortKeysDescending(NativeArray<VirtualVoiceSortKey> keys, int count)
        {
            if (count <= 1)
                return;

            unsafe
            {
                const int StackCapacity = 64;
                int* leftStack = stackalloc int[StackCapacity];
                int* rightStack = stackalloc int[StackCapacity];
                int stackTop = 0;
                leftStack[0] = 0;
                rightStack[0] = count - 1;

                while (stackTop >= 0)
                {
                    int left = leftStack[stackTop];
                    int right = rightStack[stackTop];
                    stackTop--;

                    while (left < right)
                    {
                        int i = left;
                        int j = right;
                        VirtualVoiceSortKey pivot = keys[(left + right) >> 1];
                        while (i <= j)
                        {
                            while (i <= right && IsHigherPriority(keys[i], pivot))
                                i++;
                            while (j >= left && IsHigherPriority(pivot, keys[j]))
                                j--;
                            if (i > j)
                                break;

                            VirtualVoiceSortKey swap = keys[i];
                            keys[i] = keys[j];
                            keys[j] = swap;
                            i++;
                            j--;
                        }

                        if (j - left > right - i)
                        {
                            if (left < j)
                            {
                                if (stackTop >= StackCapacity - 1)
                                {
                                    ShellSortKeysDescending(keys, count);
                                    return;
                                }

                                stackTop++;
                                leftStack[stackTop] = left;
                                rightStack[stackTop] = j;
                            }

                            left = i;
                        }
                        else
                        {
                            if (i < right)
                            {
                                if (stackTop >= StackCapacity - 1)
                                {
                                    ShellSortKeysDescending(keys, count);
                                    return;
                                }

                                stackTop++;
                                leftStack[stackTop] = i;
                                rightStack[stackTop] = right;
                            }

                            right = j;
                        }
                    }
                }
            }
        }

        private static void ShellSortKeysDescending(NativeArray<VirtualVoiceSortKey> keys, int count)
        {
            for (int gap = count >> 1; gap > 0; gap >>= 1)
            {
                for (int i = gap; i < count; i++)
                {
                    VirtualVoiceSortKey candidate = keys[i];
                    int j = i;
                    while (j >= gap && IsHigherPriority(candidate, keys[j - gap]))
                    {
                        keys[j] = keys[j - gap];
                        j -= gap;
                    }

                    keys[j] = candidate;
                }
            }
        }

        private static bool IsHigherPriority(VirtualVoiceSortKey lhs, VirtualVoiceSortKey rhs)
        {
            if (lhs.Weight > rhs.Weight)
                return true;
            if (lhs.Weight < rhs.Weight)
                return false;
            return lhs.StableKey < rhs.StableKey;
        }

        private static VirtualVoiceSelection CreateSelection(in VirtualVoice voice)
        {
            return new VirtualVoiceSelection
            {
                EventID = voice.EventID,
                ClipHash = voice.ClipHash,
                StableKey = voice.StableKey,
                SourceEntityID = voice.SourceEntityID,
                SourceAup = voice.SourceAup,
                SourceVelocityMetersPerSecond = voice.SourceVelocityMetersPerSecond,
                Volume = voice.Volume,
                Pitch = voice.Pitch,
                DopplerRatio = voice.DopplerRatio,
                Attenuation = voice.Attenuation,
                Weight = voice.Weight,
                DistanceSq = voice.DistanceSq,
                EffectiveVolume = voice.EffectiveVolume,
                SabineRt60Seconds = voice.SabineRt60Seconds,
                LowPassCutoffHz = voice.LowPassCutoffHz,
                DelaySeconds = voice.DelaySeconds,
                StationaryCacheKey = voice.StationaryCacheKey,
                PortalFlags = voice.PortalFlags,
                FoveatedTier = voice.FoveatedTier,
                AcousticEnvironment = voice.AcousticEnvironment,
                DspFlags = voice.DspFlags
            };
        }
    }

    /// <summary>
    /// Deterministic mock emitter fill for CI and Burst stress tests when baked acoustics are absent.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockAcousticEmitterJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<AcousticSourceDTO> Sources;
        [WriteOnly, NoAlias] public NativeArray<double3> PreviousSourceAup;
        public double3 CenterAup;
        public uint SectorHash;
        public uint SimulationFrame;
        public float RadiusMeters;
        public float GlobalQualityWeight;

        public void Execute(int index)
        {
            uint seed = (SectorHash ^ (SimulationFrame * 747796405u) ^ ((uint)index * 2891336453u)) | 1u;
            var random = new Random(seed);
            float angle = random.NextFloat(0f, 6.2831855f);
            float radius = VirtualVoiceUtility.FastLengthFromSq(random.NextFloat(0f, 1f)) * math.max(1f, RadiusMeters);
            float depth = random.NextFloat(-12f, 12f);
            AudioVirtualizationFastTrig.ApproxSinCosRadians(angle, out float sin, out float cos);
            double3 position = CenterAup + new double3(cos * radius, depth, sin * radius);
            if (PreviousSourceAup.IsCreated && index < PreviousSourceAup.Length)
                PreviousSourceAup[index] = position - new double3(0.05 * sin, 0.0, 0.05 * cos);

            Sources[index] = new AcousticSourceDTO
            {
                SourceHash = seed,
                BaseVolume = math.lerp(0.12f, 1f, random.NextFloat(0f, 1f)),
                BasePitch = math.lerp(0.75f, 1.25f, random.NextFloat(0f, 1f)),
                Flags = 0u,
                AUP_Position = position,
                ComputedOcclusion = 0f,
                ComputedReverb = 0f
            };
        }
    }

    /// <summary>
    /// Parallel analytical SDF occlusion and Sabine DSP parameter kernel for one cache-line source DTO.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct AcousticOcclusionJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<AcousticSourceDTO> Sources;
        [WriteOnly, NoAlias] public NativeArray<AcousticDspOutputDTO> Outputs;
        [ReadOnly, NoAlias] public NativeArray<double3> PreviousSourceAup;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly SdfVoxels;
        [ReadOnly, NoAlias] public NativeArray<AcousticMaterialCoefficientDTO>.ReadOnly Materials;
        public MockSDFSampler FallbackSdf;
        public double3 ListenerAup;
        public double3 PreviousListenerAup;
        public float3 ListenerRight;
        public float3 SdfOriginMeters;
        public float3 SdfCellSizeMeters;
        public int3 SdfDimensions;
        public float SdfDistanceScaleMeters;
        public float SimulationTickDeltaSeconds;
        public float SoundSpeedMetersPerSecond;
        public float ListenerDepthMeters;
        public float GlobalQualityWeight;
        public int RollbackActive;
        public int SourceCount;

        public void Execute(int index)
        {
            if (index >= SourceCount || index >= Sources.Length || index >= Outputs.Length)
                return;

            float quality = math.saturate(VirtualVoiceUtility.SanitizeFinite(GlobalQualityWeight, 0f));
            AcousticSourceDTO source = Sources[index];
            if (!math.all(math.isfinite(source.AUP_Position)) || !math.all(math.isfinite(ListenerAup)))
            {
                Outputs[index] = default;
                return;
            }

            double3 listenerToSourceD = source.AUP_Position - ListenerAup;
            float3 listenerToSource = new float3(
                (float)math.clamp(listenerToSourceD.x, -100000.0, 100000.0),
                (float)math.clamp(listenerToSourceD.y, -100000.0, 100000.0),
                (float)math.clamp(listenerToSourceD.z, -100000.0, 100000.0));
            float distanceSq = math.lengthsq(listenerToSource);
            if (!math.isfinite(distanceSq))
                distanceSq = 0f;

            float occlusion = ResolveLineIntegralOcclusion(listenerToSource, quality);
            float clearance = math.abs(SampleSdf(listenerToSource * 0.08f, quality));
            float absorption = ResolveAbsorption(index);
            float rt60 = VirtualVoiceUtility.ComputeSabineRt60FromClearance(clearance, absorption, quality);
            float lowPass = math.min(
                VirtualVoiceUtility.ResolveDepthLowPassHertz(ListenerDepthMeters, quality),
                math.lerp(VirtualVoiceUtility.OpenLowPassHertz, VirtualVoiceUtility.OccludedLowPassHertz, occlusion));

            float soundSpeed = math.clamp(
                VirtualVoiceUtility.SanitizeFinite(SoundSpeedMetersPerSecond, VirtualVoiceUtility.DelaySpeedMetersPerSecond),
                250f,
                2000f);
            float delay = VirtualVoiceUtility.ComputeDelaySeconds(distanceSq, soundSpeed);
            float doppler = ResolveDoppler(index, listenerToSource, soundSpeed);
            float itd = VirtualVoiceUtility.ComputeUnderwaterItdSeconds(listenerToSource, ListenerRight);
            float side01 = math.saturate(math.abs(itd) * math.rcp(math.max(VirtualVoiceUtility.MaximumUnderwaterItdSeconds, 0.000001f)));
            float ild = math.lerp(1f, 0.82f, side01 * quality);
            float volume = RollbackActive != 0
                ? 0f
                : math.saturate(source.BaseVolume) * math.rcp(1f + distanceSq * 0.015625f) * (1f - occlusion);

            source.ComputedOcclusion = occlusion;
            source.ComputedReverb = rt60;
            Sources[index] = source;
            Outputs[index] = new AcousticDspOutputDTO
            {
                SourceHash = source.SourceHash,
                Volume = volume,
                Pitch = math.clamp(source.BasePitch * doppler, 0.1f, 3f),
                Occlusion01 = occlusion,
                ReverbRt60Seconds = rt60,
                LowPassHertz = lowPass,
                DelaySeconds = delay,
                DopplerRatio = doppler,
                ItdSeconds = itd,
                Ild01 = ild,
                DistanceSq = distanceSq,
                Flags = source.Flags | (RollbackActive != 0 ? 0x80000000u : 0u)
            };
        }

        private float ResolveLineIntegralOcclusion(float3 listenerToSource, float quality)
        {
            int taps = VirtualVoiceUtility.ResolveSdfTapCount(quality);
            float invTaps = math.rcp(math.max(1f, taps));
            float solid = 0f;
            float penetration = 0f;
            for (int tap = 0; tap < taps; tap++)
            {
                float t = ((float)tap + 0.5f) * invTaps;
                float sdf = SampleSdf(listenerToSource * t, quality);
                solid += 1f - math.step(0f, sdf);
                penetration += math.max(0f, -sdf);
            }

            return math.saturate((solid * invTaps) + penetration * invTaps * 0.18f);
        }

        private float SampleSdf(float3 positionMeters, float quality)
        {
            if (!SdfVoxels.IsCreated ||
                SdfDimensions.x <= 1 ||
                SdfDimensions.y <= 1 ||
                SdfDimensions.z <= 1)
            {
                return FallbackSdf.Sample(positionMeters);
            }

            float3 cellSize = math.max(SdfCellSizeMeters, new float3(0.001f));
            float3 grid = (positionMeters - SdfOriginMeters) * math.rcp(cellSize);
            if (!math.all(math.isfinite(grid)) ||
                grid.x < 0f ||
                grid.y < 0f ||
                grid.z < 0f ||
                grid.x > SdfDimensions.x - 1f ||
                grid.y > SdfDimensions.y - 1f ||
                grid.z > SdfDimensions.z - 1f)
            {
                return FallbackSdf.Sample(positionMeters);
            }

            float nearest = SampleNearest(grid);
            float tri = SampleTrilinear(grid);
            float blend = math.saturate((quality - 0.3f) * 1.4285715f);
            return math.lerp(nearest, tri, blend);
        }

        private float SampleNearest(float3 grid)
        {
            int3 p = (int3)math.round(grid);
            return SampleVoxel(p);
        }

        private float SampleTrilinear(float3 grid)
        {
            float3 floorGrid = math.floor(grid);
            int3 p0 = (int3)floorGrid;
            int3 p1 = p0 + 1;
            float3 f = math.saturate(grid - floorGrid);
            float c000 = SampleVoxel(new int3(p0.x, p0.y, p0.z));
            float c100 = SampleVoxel(new int3(p1.x, p0.y, p0.z));
            float c010 = SampleVoxel(new int3(p0.x, p1.y, p0.z));
            float c110 = SampleVoxel(new int3(p1.x, p1.y, p0.z));
            float c001 = SampleVoxel(new int3(p0.x, p0.y, p1.z));
            float c101 = SampleVoxel(new int3(p1.x, p0.y, p1.z));
            float c011 = SampleVoxel(new int3(p0.x, p1.y, p1.z));
            float c111 = SampleVoxel(new int3(p1.x, p1.y, p1.z));
            float x00 = math.lerp(c000, c100, f.x);
            float x10 = math.lerp(c010, c110, f.x);
            float x01 = math.lerp(c001, c101, f.x);
            float x11 = math.lerp(c011, c111, f.x);
            float y0 = math.lerp(x00, x10, f.y);
            float y1 = math.lerp(x01, x11, f.y);
            return math.lerp(y0, y1, f.z);
        }

        private float SampleVoxel(int3 p)
        {
            int x = math.clamp(p.x, 0, SdfDimensions.x - 1);
            int y = math.clamp(p.y, 0, SdfDimensions.y - 1);
            int z = math.clamp(p.z, 0, SdfDimensions.z - 1);
            int index = x + (SdfDimensions.x * (y + SdfDimensions.y * z));
            if ((uint)index >= (uint)SdfVoxels.Length)
                return 1f;

            float range = math.max(0.001f, VirtualVoiceUtility.SanitizeFinite(SdfDistanceScaleMeters, 1f));
            return ((SdfVoxels[index] * VirtualVoiceUtility.InverseByteMax) * 2f - 1f) * range;
        }

        private float ResolveAbsorption(int index)
        {
            if (Materials.IsCreated && Materials.Length > 0)
            {
                AcousticMaterialCoefficientDTO material = Materials[index % Materials.Length];
                return math.clamp(VirtualVoiceUtility.SanitizeFinite(material.Absorption01, 0.35f), 0.03f, 1f);
            }

            return 0.35f;
        }

        private float ResolveDoppler(int index, float3 listenerToSource, float soundSpeed)
        {
            double tick = math.max((double)SimulationTickDeltaSeconds, 0.0001);
            double3 previousSource = PreviousSourceAup.IsCreated && index < PreviousSourceAup.Length
                ? PreviousSourceAup[index]
                : Sources[index].AUP_Position;
            double3 sourceVelocityD = (Sources[index].AUP_Position - previousSource) / tick;
            double3 listenerVelocityD = (ListenerAup - PreviousListenerAup) / tick;
            float3 sourceVelocity = new float3(
                (float)math.clamp(sourceVelocityD.x, -soundSpeed, soundSpeed),
                (float)math.clamp(sourceVelocityD.y, -soundSpeed, soundSpeed),
                (float)math.clamp(sourceVelocityD.z, -soundSpeed, soundSpeed));
            float3 listenerVelocity = new float3(
                (float)math.clamp(listenerVelocityD.x, -soundSpeed, soundSpeed),
                (float)math.clamp(listenerVelocityD.y, -soundSpeed, soundSpeed),
                (float)math.clamp(listenerVelocityD.z, -soundSpeed, soundSpeed));
            return VirtualVoiceUtility.ComputeDopplerRatio(listenerToSource, sourceVelocity, listenerVelocity, 1f, soundSpeed);
        }
    }

    internal static class AudioVirtualizationFastTrig
    {
        public static float ApproxSinRadians(float radians)
        {
            const float epsilon = 0.000001f;
            float angle = math.select(0f, radians, math.isfinite(radians));
            float cycle = angle * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * (2f * math.PI);
            float mirrored = math.select(x, (2f * math.PI) - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float denominator = math.max(epsilon, (5f * math.PI * math.PI) - (4f * shape));
            float sine = sign * (16f * shape) * math.rcp(denominator);
            return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
        }

        public static float ApproxCosRadians(float radians)
        {
            return ApproxSinRadians(radians + (0.5f * math.PI));
        }

        public static void ApproxSinCosRadians(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinRadians(radians);
            cosine = ApproxCosRadians(radians);
        }
    }
}
