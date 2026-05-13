using Hecton8.Audio.Propagation;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Virtualization
{
    /// <summary>
    /// Burst ranking pass for virtual acoustic emitters.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
    public struct VirtualVoiceSortJob : IJob
    {
        private const int FixedSortStackSafeLimit = 120;

        public NativeList<VirtualVoice> Voices;
        public NativeArray<VirtualVoiceSelection> Selections;
        public NativeArray<VirtualVoiceStatistics> Statistics;
        public AcousticAup ListenerAup;
        public int PhysicalVoiceLimit;
        public int DroppedVoiceCount;
        public int Frame;
        public float MinimumAudibleEnergy;

        public void Execute()
        {
            int totalVoices = Voices.Length;
            int culledVoices = 0;
            int audibleCount = 0;
            float minAudibleEnergy = math.max(0f, MinimumAudibleEnergy);

            for (int i = 0; i < totalVoices; i++)
            {
                VirtualVoice voice = Voices[i];
                if (!AcousticAup.IsFinite(in voice.SourceAup) ||
                    voice.FoveatedTier >= VirtualVoiceUtility.FoveatedTierFrozen)
                {
                    culledVoices++;
                    continue;
                }

                float volume = math.saturate(SanitizeFinite(voice.Volume, 0f));
                float priority = math.max(0f, SanitizeFinite(voice.Priority, 0f));
                float3 relative = AcousticAup.RelativeFloat3(in voice.SourceAup, in ListenerAup);
                float distanceSq = math.lengthsq(relative);
                if (!math.isfinite(distanceSq))
                    distanceSq = float.MaxValue * 0.25f;

                float attenuation = math.rcp(distanceSq + 1f);
                float audibleEnergy = volume * attenuation;
                if (priority <= 0f || audibleEnergy < minAudibleEnergy)
                {
                    culledVoices++;
                    continue;
                }

                voice.Volume = volume;
                voice.Priority = priority;
                voice.Pitch = math.clamp(SanitizeFinite(voice.Pitch, 1f), 0.1f, 3f);
                voice.DopplerRatio = math.clamp(
                    SanitizeFinite(voice.DopplerRatio, 1f),
                    VirtualVoiceUtility.MinimumDopplerRatio,
                    VirtualVoiceUtility.MaximumDopplerRatio);
                voice.Attenuation = attenuation;
                voice.DistanceSq = distanceSq;
                voice.Weight = priority * attenuation;
                Voices[audibleCount] = voice;
                audibleCount++;
            }

            if (audibleCount < totalVoices)
                Voices.ResizeUninitialized(audibleCount);

            if (audibleCount > 1)
                QuickSortByWeightDescending(ref Voices, audibleCount);

            int safeLimit = math.clamp(PhysicalVoiceLimit, 0, Selections.Length);
            int activePhysical = math.min(safeLimit, audibleCount);
            for (int i = 0; i < activePhysical; i++)
            {
                VirtualVoice voice = Voices[i];
                Selections[i] = new VirtualVoiceSelection
                {
                    EventID = voice.EventID,
                    ClipHash = voice.ClipHash,
                    StableKey = voice.StableKey,
                    SourceAup = voice.SourceAup,
                    Volume = voice.Volume,
                    Pitch = voice.Pitch,
                    DopplerRatio = voice.DopplerRatio,
                    Attenuation = voice.Attenuation,
                    Weight = voice.Weight,
                    DistanceSq = voice.DistanceSq,
                    StationaryCacheKey = voice.StationaryCacheKey,
                    PortalFlags = voice.PortalFlags,
                    FoveatedTier = voice.FoveatedTier
                };
            }

            for (int i = activePhysical; i < safeLimit; i++)
                Selections[i] = default;

            if (Statistics.Length > 0)
            {
                Statistics[0] = new VirtualVoiceStatistics
                {
                    Frame = Frame,
                    TotalVoices = totalVoices,
                    AudibleVoices = audibleCount,
                    CulledVoices = culledVoices,
                    ActivePhysicalVoices = activePhysical,
                    PhysicalVoiceLimit = safeLimit,
                    StolenVoices = math.max(0, audibleCount - activePhysical),
                    DroppedVoices = math.max(0, DroppedVoiceCount)
                };
            }
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static void QuickSortByWeightDescending(ref NativeList<VirtualVoice> voices, int count)
        {
            FixedList512Bytes<int> stack = default;
            PushRange(ref stack, 0, count - 1);

            while (stack.Length >= 2)
            {
                int rightIndex = stack.Length - 1;
                int right = stack[rightIndex];
                stack.RemoveAt(rightIndex);
                int leftIndex = stack.Length - 1;
                int left = stack[leftIndex];
                stack.RemoveAt(leftIndex);

                if (right <= left)
                    continue;

                if (right - left <= 12)
                {
                    InsertionSortRange(ref voices, left, right);
                    continue;
                }

                int i = left;
                int j = right;
                float pivot = voices[(left + right) >> 1].Weight;
                while (i <= j)
                {
                    while (voices[i].Weight > pivot)
                        i++;
                    while (voices[j].Weight < pivot)
                        j--;

                    if (i > j)
                        continue;

                    Swap(ref voices, i, j);
                    i++;
                    j--;
                }

                if (left < j)
                    PushRangeOrSort(ref stack, ref voices, left, j);
                if (i < right)
                    PushRangeOrSort(ref stack, ref voices, i, right);
            }
        }

        private static void PushRangeOrSort(
            ref FixedList512Bytes<int> stack,
            ref NativeList<VirtualVoice> voices,
            int left,
            int right)
        {
            if (stack.Length + 2 <= FixedSortStackSafeLimit)
                PushRange(ref stack, left, right);
            else
                InsertionSortRange(ref voices, left, right);
        }

        private static void PushRange(ref FixedList512Bytes<int> stack, int left, int right)
        {
            int leftValue = left;
            int rightValue = right;
            stack.Add(leftValue);
            stack.Add(rightValue);
        }

        private static void InsertionSortRange(ref NativeList<VirtualVoice> voices, int left, int right)
        {
            for (int i = left + 1; i <= right; i++)
            {
                VirtualVoice value = voices[i];
                int j = i - 1;
                while (j >= left && voices[j].Weight < value.Weight)
                {
                    voices[j + 1] = voices[j];
                    j--;
                }

                voices[j + 1] = value;
            }
        }

        private static void Swap(ref NativeList<VirtualVoice> voices, int a, int b)
        {
            if (a == b)
                return;

            VirtualVoice tmp = voices[a];
            voices[a] = voices[b];
            voices[b] = tmp;
        }
    }
}
