using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using AcousticAup = Hecton8.Core.Contracts.AcousticAup;

namespace Hecton8.Audio.Virtualization
{
    /// <summary>
    /// Burst ranking pass for virtual acoustic emitters.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct VirtualVoiceSortJob : IJob
    {
        public NativeArray<VirtualVoice> Voices;
        public NativeArray<VirtualVoiceSortKey> SortKeys;
        public NativeArray<VirtualVoiceSelection> Selections;
        public NativeArray<VirtualVoiceStatistics> Statistics;
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

        public void Execute()
        {
            int sortKeyCapacity = SortKeys.IsCreated ? SortKeys.Length : 0;
            int totalVoices = math.clamp(VoiceCount, 0, math.min(Voices.Length, sortKeyCapacity));
            int culledVoices = 0;
            int audibleCount = 0;
            int occludedCount = 0;
            int delayedCount = 0;
            int safeLimit = math.clamp(PhysicalVoiceLimit, 0, Selections.Length);
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
                    VirtualVoiceUtility.ResolveDearLieOcclusion(relative, in SdfSampler, DisableSdfOcclusion);
                if (occluded)
                {
                    volume *= occlusionPenalty;
                    lowPass = math.min(lowPass, occludedLowPass);
                    flags |= VirtualVoiceDspFlags.SdfOccluded;
                    occludedCount++;
                }

                if ((flags & VirtualVoiceDspFlags.InsideSubmarineHull) != 0)
                    lowPass = math.min(lowPass, VirtualVoiceUtility.HullLowPassHertz);

                float effectiveVolume = volume * attenuation;
                float weight = effectiveVolume * importance;
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
                    MaximumDelaySeconds = maxDelay
                };
            }
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
}
