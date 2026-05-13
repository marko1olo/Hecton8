using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Synthesis
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DepthStressGranularVoice
    {
        public byte Active;
        public byte Reserved0;
        public ushort Reserved1;
        public int StartSample;
        public int LengthSamples;
        public float Cursor;
        public float PlaybackRate;
        public float Gain;
        public uint Seed;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct DepthStressGranularSpawnState
    {
        public float SpawnAccumulator;
        public uint RandomState;
        public int RingCursor;
        public int Reserved0;
    }

    internal static class DepthStressGranularMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteSaturate(float value)
        {
            return math.saturate(FiniteOrZero(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteNonNegative(float value)
        {
            return math.max(0f, FiniteOrZero(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOrZero(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        public static void TrimVoicesToBudget(NativeArray<DepthStressGranularVoice> voices, int voiceLimit)
        {
            int safeLimit = math.clamp(voiceLimit, 0, voices.Length);
            for (int i = safeLimit; i < voices.Length; i++)
            {
                DepthStressGranularVoice voice = voices[i];
                voice.Active = 0;
                voice.Cursor = 0f;
                voice.Gain = 0f;
                voices[i] = voice;
            }
        }
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    public struct DepthStressGranularSpawnJob : IJob
    {
        public NativeArray<DepthStressGranularVoice> Voices;
        public NativeArray<DepthStressGranularSpawnState> State;
        public int VoiceLimit;
        public int GrainBankLength;
        public int SampleRate;
        public float DeltaTimeSeconds;
        public float Stress01;
        public float PressureDelta01;
        public float Depth01;

        public void Execute()
        {
            if (Voices.Length <= 0 || State.Length <= 0 || GrainBankLength <= 128)
                return;

            int voiceLimit = math.clamp(VoiceLimit, 0, Voices.Length);
            DepthStressGranularMath.TrimVoicesToBudget(Voices, voiceLimit);
            if (voiceLimit <= 0)
                return;

            DepthStressGranularSpawnState state = State[0];
            uint seed = state.RandomState != 0u ? state.RandomState : 0x6D2B79F5u;
            Random random = default;
            random.state = seed;

            if (!math.isfinite(state.SpawnAccumulator))
                state.SpawnAccumulator = 0f;
            state.SpawnAccumulator = math.clamp(state.SpawnAccumulator, 0f, voiceLimit);
            if (state.RingCursor < 0 || state.RingCursor >= voiceLimit)
                state.RingCursor = 0;

            float stress = DepthStressGranularMath.FiniteSaturate(Stress01);
            float derivative = DepthStressGranularMath.FiniteSaturate(PressureDelta01);
            float depth = DepthStressGranularMath.FiniteSaturate(Depth01);
            float deltaTimeSeconds = math.min(DepthStressGranularMath.FiniteNonNegative(DeltaTimeSeconds), 0.25f);
            float drive = math.saturate(math.max(stress, derivative) * 0.65f + derivative * 0.55f);
            float eventsPerSecond = math.lerp(0.2f, 28f, drive) * math.lerp(0.7f, 1.55f, depth);

            state.SpawnAccumulator = math.min(
                state.SpawnAccumulator + deltaTimeSeconds * eventsPerSecond,
                voiceLimit);
            int spawnCount = math.min((int)math.floor(state.SpawnAccumulator), voiceLimit);
            state.SpawnAccumulator -= spawnCount;

            int safeSampleRate = math.max(1, SampleRate);
            for (int i = 0; i < spawnCount; i++)
            {
                int voiceIndex = ResolveVoiceIndex(Voices, voiceLimit, ref state);
                float grainSeconds = random.NextFloat(0.01f, 0.05f);
                int lengthSamples = math.clamp((int)(grainSeconds * safeSampleRate + 0.5f), 1, math.max(1, GrainBankLength - 2));
                int maxStart = math.max(1, GrainBankLength - lengthSamples - 1);
                float playbackRate = math.lerp(1.12f, 0.52f, depth) * random.NextFloat(0.92f, 1.08f);
                Voices[voiceIndex] = new DepthStressGranularVoice
                {
                    Active = 1,
                    StartSample = random.NextInt(0, maxStart),
                    LengthSamples = lengthSamples,
                    Cursor = 0f,
                    PlaybackRate = math.max(0.125f, playbackRate),
                    Gain = math.saturate(0.18f + drive * 0.72f) * random.NextFloat(0.75f, 1f),
                    Seed = random.NextUInt()
                };
            }

            state.RandomState = random.state != 0u ? random.state : 0x6D2B79F5u;
            State[0] = state;
        }

        private static int ResolveVoiceIndex(
            NativeArray<DepthStressGranularVoice> voices,
            int voiceLimit,
            ref DepthStressGranularSpawnState state)
        {
            for (int i = 0; i < voiceLimit; i++)
            {
                int voiceIndex = state.RingCursor + i;
                if (voiceIndex >= voiceLimit)
                    voiceIndex -= voiceLimit;
                if (voices[voiceIndex].Active == 0)
                {
                    state.RingCursor = voiceIndex + 1;
                    if (state.RingCursor >= voiceLimit)
                        state.RingCursor = 0;
                    return voiceIndex;
                }
            }

            int stolen = state.RingCursor;
            state.RingCursor = stolen + 1;
            if (state.RingCursor >= voiceLimit)
                state.RingCursor = 0;
            return stolen;
        }
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    public struct DepthStressGranularSynthesisJob : IJob
    {
        [ReadOnly] public NativeArray<float> GrainBank;
        public NativeArray<DepthStressGranularVoice> Voices;
        public NativeArray<float> Output;
        public int VoiceLimit;
        public float Stress01;
        public float PressureDelta01;
        public float Depth01;
        public float OutputGain;

        public void Execute()
        {
            if (Output.Length <= 0 || GrainBank.Length <= 1 || Voices.Length <= 0)
                return;

            int voiceLimit = math.clamp(VoiceLimit, 0, Voices.Length);
            DepthStressGranularMath.TrimVoicesToBudget(Voices, voiceLimit);
            if (voiceLimit <= 0)
                return;

            float stress = DepthStressGranularMath.FiniteSaturate(Stress01);
            float derivative = DepthStressGranularMath.FiniteSaturate(PressureDelta01);
            float depth = DepthStressGranularMath.FiniteSaturate(Depth01);
            float globalGain =
                DepthStressGranularMath.FiniteSaturate(OutputGain) *
                math.saturate(0.18f + stress * 0.54f + derivative * 0.38f);

            for (int sampleIndex = 0; sampleIndex < Output.Length; sampleIndex++)
            {
                float mixed = 0f;
                for (int voiceIndex = 0; voiceIndex < voiceLimit; voiceIndex++)
                {
                    DepthStressGranularVoice voice = Voices[voiceIndex];
                    if (voice.Active == 0)
                        continue;

                    int length = math.clamp(voice.LengthSamples, 1, GrainBank.Length);
                    float cursor = DepthStressGranularMath.FiniteNonNegative(voice.Cursor);
                    if (cursor >= length)
                    {
                        voice.Active = 0;
                        Voices[voiceIndex] = voice;
                        continue;
                    }

                    voice.StartSample = math.clamp(voice.StartSample, 0, GrainBank.Length - 1);
                    voice.Gain = DepthStressGranularMath.FiniteSaturate(voice.Gain);
                    voice.PlaybackRate = math.max(
                        0.125f,
                        DepthStressGranularMath.FiniteOrDefault(voice.PlaybackRate, 1f));

                    float age01 = math.saturate(cursor * math.rcp(length));
                    float window = TriangleWindow(age01);
                    float sourceSample = SampleLinear(GrainBank, voice.StartSample + cursor);
                    mixed += sourceSample * voice.Gain * window;

                    voice.Cursor = cursor + math.max(0.125f, voice.PlaybackRate * math.lerp(1f, 0.52f, depth));
                    Voices[voiceIndex] = voice;
                }

                Output[sampleIndex] = math.clamp(
                    DepthStressGranularMath.FiniteOrZero(Output[sampleIndex]) + mixed * globalGain,
                    -1f,
                    1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWindow(float t)
        {
            return math.saturate(1f - math.abs((t * 2f) - 1f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SampleLinear(NativeArray<float> samples, float cursor)
        {
            int length = samples.Length;
            float wrapped = cursor - math.floor(cursor * math.rcp((float)length)) * length;

            int i0 = math.clamp((int)wrapped, 0, length - 1);
            int i1 = i0 + 1;
            if (i1 >= length)
                i1 = 0;
            return DepthStressGranularMath.FiniteOrZero(
                math.lerp(samples[i0], samples[i1], math.frac(wrapped)));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct KineticImpactSineOscillatorState
    {
        public double Phase;
        public float LowPassState;
        public float AgeSeconds;
        public float Reserved0;
    }

    [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast, CompileSynchronously = true)]
    public struct KineticImpactSineOscillatorJob : IJob
    {
        public NativeArray<float> Output;
        public NativeArray<KineticImpactSineOscillatorState> State;
        public int SampleRate;
        public float Amplitude01;
        public float DurationSeconds;
        public float StartHertz;
        public float EndHertz;
        public float Distortion01;
        public float LowPassCutoffHertz;

        public void Execute()
        {
            if (Output.Length <= 0 || State.Length <= 0)
                return;

            int sampleRate = math.max(1, SampleRate);
            float sampleRateInv = math.rcp((float)sampleRate);
            float duration = math.clamp(
                DepthStressGranularMath.FiniteOrDefault(DurationSeconds, 0.2f),
                0.02f,
                0.2f);
            float amplitude = DepthStressGranularMath.FiniteSaturate(Amplitude01);
            if (amplitude <= 0f)
                return;

            float startHertz = math.clamp(
                DepthStressGranularMath.FiniteOrDefault(StartHertz, 150f),
                8f,
                240f);
            float endHertz = math.clamp(
                DepthStressGranularMath.FiniteOrDefault(EndHertz, 40f),
                8f,
                startHertz);
            float distortion = DepthStressGranularMath.FiniteSaturate(Distortion01);
            float lowPassCutoff = math.clamp(
                DepthStressGranularMath.FiniteOrDefault(LowPassCutoffHertz, 22050f),
                40f,
                sampleRate * 0.45f);
            float lowPassAlpha = math.exp(-6.2831855f * lowPassCutoff * sampleRateInv);

            KineticImpactSineOscillatorState state = State[0];
            if (!math.isfinite(state.Phase))
                state.Phase = 0d;
            if (!math.isfinite(state.LowPassState))
                state.LowPassState = 0f;
            if (!math.isfinite(state.AgeSeconds) || state.AgeSeconds < 0f)
                state.AgeSeconds = 0f;

            for (int sampleIndex = 0; sampleIndex < Output.Length; sampleIndex++)
            {
                float age = state.AgeSeconds;
                if (age >= duration)
                    break;

                float t = math.saturate(age * math.rcp(duration));
                float frequency = math.lerp(startHertz, endHertz, t);
                state.Phase += 6.283185307179586d * frequency * sampleRateInv;
                if (state.Phase >= 6.283185307179586d)
                    state.Phase -= 6.283185307179586d;

                float envelope = math.saturate(age * 200f) * (1f - t) * (1f - t);
                float raw = math.sin((float)state.Phase) * envelope * amplitude;
                float filtered = raw + lowPassAlpha * (state.LowPassState - raw);
                state.LowPassState = filtered;
                float clipped = math.clamp(filtered * math.lerp(1f, 2.85f, distortion), -0.82f, 0.82f);
                float sample = math.lerp(filtered, clipped, distortion);
                Output[sampleIndex] = math.clamp(
                    DepthStressGranularMath.FiniteOrZero(Output[sampleIndex]) + sample,
                    -1f,
                    1f);
                state.AgeSeconds = age + sampleRateInv;
            }

            State[0] = state;
        }
    }
}
