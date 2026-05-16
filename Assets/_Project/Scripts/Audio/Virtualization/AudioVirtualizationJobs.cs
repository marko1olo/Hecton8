using Hecton8.Audio.Propagation;
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
            int safeLimit = math.clamp(PhysicalVoiceLimit, 0, Selections.Length);
            int selectedCount = 0;
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
                selectedCount = InsertSelectionCandidate(in voice, Selections, safeLimit, selectedCount);
            }

            if (audibleCount < totalVoices)
                Voices.ResizeUninitialized(audibleCount);

            for (int i = selectedCount; i < Selections.Length; i++)
                Selections[i] = default;

            if (Statistics.Length > 0)
            {
                Statistics[0] = new VirtualVoiceStatistics
                {
                    Frame = Frame,
                    TotalVoices = totalVoices,
                    AudibleVoices = audibleCount,
                    CulledVoices = culledVoices,
                    ActivePhysicalVoices = selectedCount,
                    PhysicalVoiceLimit = safeLimit,
                    StolenVoices = math.max(0, audibleCount - selectedCount),
                    DroppedVoices = math.max(0, DroppedVoiceCount)
                };
            }
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static int InsertSelectionCandidate(
            in VirtualVoice voice,
            NativeArray<VirtualVoiceSelection> selections,
            int safeLimit,
            int selectedCount)
        {
            if (safeLimit <= 0)
                return selectedCount;

            if (selectedCount == safeLimit && voice.Weight <= selections[safeLimit - 1].Weight)
                return selectedCount;

            int insertIndex = selectedCount;
            while (insertIndex > 0 && selections[insertIndex - 1].Weight < voice.Weight)
                insertIndex--;

            if (insertIndex >= safeLimit)
                return selectedCount;

            int moveStart = math.min(selectedCount, safeLimit - 1);
            for (int moveIndex = moveStart; moveIndex > insertIndex; moveIndex--)
                selections[moveIndex] = selections[moveIndex - 1];

            selections[insertIndex] = CreateSelection(in voice);
            if (selectedCount < safeLimit)
                selectedCount++;

            return selectedCount;
        }

        private static VirtualVoiceSelection CreateSelection(in VirtualVoice voice)
        {
            return new VirtualVoiceSelection
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
    }
}
