using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Synthesis
{
    /// <summary>
    /// Sixteen-byte audio parameter DTO copied across the game-thread to DSP-thread boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct SynthParametersDTO
    {
        /// <summary>Required byte size for ARM64-aligned audio-thread loads.</summary>
        public const int SizeBytes = 16;
        /// <summary>Carrier/base frequency in hertz.</summary>
        public float BaseFrequency;
        /// <summary>FM/granular modulation strength.</summary>
        public float ModulationIndex;
        /// <summary>Normalized grain size scalar.</summary>
        public float GrainSize;
        /// <summary>Pressure/stress scalar in normalized 0..1 range.</summary>
        public float PressureScalar;

        /// <summary>
        /// Reinterprets unmanaged memory as a mutable parameter reference without C# property copies.
        /// </summary>
        /// <param name="pointer">Pointer to at least 16 bytes of writable unmanaged memory.</param>
        /// <returns>Mutable reference to the parameter DTO.</returns>
        public static unsafe ref SynthParametersDTO AsRef(void* pointer)
        {
            return ref UnsafeUtility.AsRef<SynthParametersDTO>(pointer);
        }
    }

    /// <summary>
    /// Sixteen-byte grain playback state consumed by allocation-free granular DSP loops.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct GrainPlaybackStateDTO
    {
        /// <summary>Required byte size for one grain voice on ARM64.</summary>
        public const int SizeBytes = 16;
        /// <summary>Current playback phase in samples or normalized LUT space.</summary>
        public float CurrentPhase;
        /// <summary>Playback pitch multiplier.</summary>
        public float Pitch;
        /// <summary>Linear amplitude scalar.</summary>
        public float Amplitude;
        /// <summary>Start index inside the base grain buffer.</summary>
        public uint GrainStartIndex;

        /// <summary>
        /// Reinterprets unmanaged memory as a mutable voice reference without managed wrappers.
        /// </summary>
        /// <param name="pointer">Pointer to at least 16 bytes of writable unmanaged memory.</param>
        /// <returns>Mutable reference to the grain playback state.</returns>
        public static unsafe ref GrainPlaybackStateDTO AsRef(void* pointer)
        {
            return ref UnsafeUtility.AsRef<GrainPlaybackStateDTO>(pointer);
        }
    }

    /// <summary>
    /// Local blind-dependency mock for pressure/tension/depth/speed synth validation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockHullStressSignal
    {
        /// <summary>Oscillating structural stress scalar in normalized 0..1 range.</summary>
        public float MockStress;
        /// <summary>Oscillating cable or hull tension scalar in normalized 0..1 range.</summary>
        public float MockTension;
        /// <summary>Oscillating depth scalar in normalized 0..1 range.</summary>
        public float MockDepth;
        /// <summary>Mock submarine velocity scalar used for pitch wobble.</summary>
        public float MockSubmarineVelocity;
    }

    /// <summary>
    /// Sixteen-byte blind pressure mock for proving the synth without submarine depth systems.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockPressureSignal
    {
        /// <summary>Normalized pressure/stress scalar in 0..1 range.</summary>
        public float PressureScalar;
        /// <summary>Normalized depth scalar used by low-pass muffling tests.</summary>
        public float DepthScalar;
        /// <summary>Normalized velocity scalar used by pitch-wobble tests.</summary>
        public float VelocityScalar;
        /// <summary>Monotonic caller-owned sequence for deterministic validation.</summary>
        public uint Sequence;
    }

    /// <summary>
    /// Sixteen-byte blind tension mock for proving pressure/tension coupling in isolation.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public partial struct MockTensionSignal
    {
        /// <summary>Normalized cable or hull tension scalar in 0..1 range.</summary>
        public float TensionScalar;
        /// <summary>Absolute pressure-minus-tension delta, used as a cheap strain-rate stand-in.</summary>
        public float StrainRateScalar;
        /// <summary>Pressure contribution coupled into the tension fake.</summary>
        public float PressureCouplingScalar;
        /// <summary>Monotonic caller-owned sequence for deterministic validation.</summary>
        public uint Sequence;
    }

    /// <summary>
    /// Burst mock producer for validating synth response without hull-integrity dependencies.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct MockHullStressSignalJob : IJob
    {
        /// <summary>Single-element output signal buffer.</summary>
        [NoAlias] public NativeArray<MockHullStressSignal> Output;
        /// <summary>Optional single-element pressure output buffer for literal task validation.</summary>
        [NoAlias] public NativeArray<MockPressureSignal> PressureOutput;
        /// <summary>Optional single-element tension output buffer for literal task validation.</summary>
        [NoAlias] public NativeArray<MockTensionSignal> TensionOutput;
        /// <summary>Elapsed time in seconds.</summary>
        public float ElapsedSeconds;
        /// <summary>Stress oscillator frequency in hertz.</summary>
        public float StressFrequencyHz;
        /// <summary>Tension oscillator frequency in hertz.</summary>
        public float TensionFrequencyHz;
        /// <summary>Depth oscillator frequency in hertz.</summary>
        public float DepthFrequencyHz;
        /// <summary>Caller-owned validation sequence copied into pressure/tension DTOs.</summary>
        public uint Sequence;

        /// <summary>Writes the current mock signal sample.</summary>
        public void Execute()
        {
            bool hasHullOutput = Output.IsCreated && Output.Length > 0;
            bool hasPressureOutput = PressureOutput.IsCreated && PressureOutput.Length > 0;
            bool hasTensionOutput = TensionOutput.IsCreated && TensionOutput.Length > 0;
            if (!hasHullOutput && !hasPressureOutput && !hasTensionOutput)
                return;

            float time = DepthStressGranularMath.FiniteNonNegative(ElapsedSeconds);
            float stressHz = math.max(0.01f, DepthStressGranularMath.FiniteOrDefault(StressFrequencyHz, 0.21f));
            float tensionHz = math.max(0.01f, DepthStressGranularMath.FiniteOrDefault(TensionFrequencyHz, 0.37f));
            float depthHz = math.max(0.005f, DepthStressGranularMath.FiniteOrDefault(DepthFrequencyHz, 0.047f));
            float stress = 0.5f + 0.5f * math.sin(time * stressHz * 6.28318530718f);
            float tension = 0.5f + 0.5f * math.sin((time * tensionHz * 6.28318530718f) + 1.7f);
            float depth = 0.5f + 0.5f * math.sin((time * depthHz * 6.28318530718f) + 0.42f);
            float safeStress = math.saturate(stress);
            float safeTension = math.saturate(tension);
            float safeDepth = math.saturate(depth);
            float safeVelocity = math.saturate(math.abs(safeStress - safeTension) * 1.35f);
            uint safeSequence = Sequence;

            if (hasHullOutput)
            {
                MockHullStressSignal signal = default;
                signal.MockStress = safeStress;
                signal.MockTension = safeTension;
                signal.MockDepth = safeDepth;
                signal.MockSubmarineVelocity = safeVelocity;
                Output[0] = signal;
            }

            if (hasPressureOutput)
            {
                MockPressureSignal signal = default;
                signal.PressureScalar = safeStress;
                signal.DepthScalar = safeDepth;
                signal.VelocityScalar = safeVelocity;
                signal.Sequence = safeSequence;
                PressureOutput[0] = signal;
            }

            if (hasTensionOutput)
            {
                MockTensionSignal signal = default;
                signal.TensionScalar = safeTension;
                signal.StrainRateScalar = math.saturate(math.abs(safeStress - safeTension));
                signal.PressureCouplingScalar = safeStress;
                signal.Sequence = safeSequence;
                TensionOutput[0] = signal;
            }
        }
    }

    /// <summary>
    /// Precomputes the 512-sample style Hanning window used by grain envelopes.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct HanningWindowBuildJob : IJobParallelFor
    {
        /// <summary>Destination LUT, typically 512 samples.</summary>
        [NoAlias] public NativeArray<float> HanningLut;

        /// <summary>Writes one Hanning window sample.</summary>
        /// <param name="index">LUT index.</param>
        public void Execute(int index)
        {
            if (!HanningLut.IsCreated || HanningLut.Length <= 0)
                return;

            float denominator = math.max(1f, HanningLut.Length - 1f);
            float t = math.saturate(index * math.rcp(denominator));
            HanningLut[index] = 0.5f - 0.5f * math.cos(t * 6.28318530718f);
        }
    }

    /// <summary>
    /// Emergency zero-file grain generator used when no archived or authored grain data is available.
    /// </summary>
    public static class EmergencyMockGrains
    {
        /// <summary>
        /// Fills a base grain buffer with deterministic metallic grit without loading WAV files.
        /// </summary>
        /// <param name="baseGrainBuffer">Destination sample buffer.</param>
        /// <param name="sampleRate">Synthesis sample rate.</param>
        /// <param name="fundamentalHertz">Base metallic frequency.</param>
        public static void GenerateEmergencyMockGrains(
            NativeArray<float> baseGrainBuffer,
            int sampleRate = 48000,
            float fundamentalHertz = 92f)
        {
            if (!baseGrainBuffer.IsCreated || baseGrainBuffer.Length <= 0)
                return;

            int safeSampleRate = math.max(1, sampleRate);
            float frequency = math.max(12f, DepthStressGranularMath.FiniteOrDefault(fundamentalHertz, 92f));
            float invSampleRate = math.rcp((float)safeSampleRate);
            for (int i = 0; i < baseGrainBuffer.Length; i++)
            {
                float t = i * invSampleRate;
                uint seed = (uint)i * 747796405u + 2891336453u;
                float grit = HashSigned(seed) * 0.18f;
                float ring =
                    math.sin(t * frequency * 6.28318530718f) * 0.44f +
                    math.sin(t * frequency * 2.71f * 6.28318530718f) * 0.25f +
                    math.sin(t * frequency * 5.39f * 6.28318530718f) * 0.12f;
                baseGrainBuffer[i] = math.clamp((ring + grit) * ResolveRaisedCosine(i, baseGrainBuffer.Length), -1f, 1f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRaisedCosine(int index, int length)
        {
            if (length <= 1)
                return 1f;

            float t = math.saturate(index * math.rcp(length - 1f));
            return 0.5f - 0.5f * math.cos(t * 6.28318530718f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HashSigned(uint seed)
        {
            seed ^= seed >> 16;
            seed *= 0x7FEB352Du;
            seed ^= seed >> 15;
            seed *= 0x846CA68Bu;
            seed ^= seed >> 16;
            return ((seed & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }
    }

    /// <summary>
    /// Thirty-two-byte isolated granular voice state for Burst test kernels.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct DepthStressGranularVoice
    {
        /// <summary>Current grain cursor in samples.</summary>
        public float Cursor;
        /// <summary>Playback pitch scalar.</summary>
        public float PlaybackRate;
        /// <summary>Linear grain gain.</summary>
        public float Gain;
        /// <summary>Start sample in the base grain bank.</summary>
        public int StartSample;
        /// <summary>Length of the grain in samples.</summary>
        public int LengthSamples;
        /// <summary>Deterministic random seed for this voice.</summary>
        public uint Seed;
        /// <summary>One when the voice is active.</summary>
        public byte Active;
#pragma warning disable 0169
        private byte _pad0;
        private byte _pad1;
        private byte _pad2;
        private byte _pad3;
        private byte _pad4;
        private byte _pad5;
        private byte _pad6;
#pragma warning restore 0169
    }

    /// <summary>
    /// Sixteen-byte granular spawn state with natural 4-byte fields and no packed layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct DepthStressGranularSpawnState
    {
        /// <summary>Fractional spawn accumulator.</summary>
        public float SpawnAccumulator;
        /// <summary>Deterministic random state.</summary>
        public uint RandomState;
        /// <summary>Round-robin voice cursor.</summary>
        public int RingCursor;
#pragma warning disable 0169
        private int _pad0;
#pragma warning restore 0169
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateExpNegPositive(float value)
        {
            float x = math.max(0f, FiniteOrZero(value));
            return math.rcp(1f + x + 0.48f * x * x);
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DepthStressGranularSpawnJob : IJob
    {
        [NoAlias] public NativeArray<DepthStressGranularVoice> Voices;
        [NoAlias] public NativeArray<DepthStressGranularSpawnState> State;
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
                DepthStressGranularVoice voice = default;
                voice.Active = 1;
                voice.StartSample = random.NextInt(0, maxStart);
                voice.LengthSamples = lengthSamples;
                voice.Cursor = 0f;
                voice.PlaybackRate = math.max(0.125f, playbackRate);
                voice.Gain = math.saturate(0.18f + drive * 0.72f) * random.NextFloat(0.75f, 1f);
                voice.Seed = random.NextUInt();
                Voices[voiceIndex] = voice;
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct DepthStressGranularSynthesisJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<float> GrainBank;
        [ReadOnly] [NoAlias] public NativeArray<float> HanningLut;
        [NoAlias] public NativeArray<DepthStressGranularVoice> Voices;
        [NoAlias] public NativeArray<float> Output;
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
                    float window = ResolveWindow(age01);
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
        private float ResolveWindow(float t)
        {
            if (!HanningLut.IsCreated || HanningLut.Length <= 1)
                return TriangleWindow(t);

            float lutCursor = math.saturate(t) * (HanningLut.Length - 1f);
            int i0 = math.clamp((int)lutCursor, 0, HanningLut.Length - 1);
            int i1 = math.min(i0 + 1, HanningLut.Length - 1);
            return math.lerp(HanningLut[i0], HanningLut[i1], math.saturate(lutCursor - i0));
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

    /// <summary>
    /// Twenty-four-byte oscillator state with double phase first and no packed layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct KineticImpactSineOscillatorState
    {
        /// <summary>Oscillator phase in normalized cycles.</summary>
        public double Phase;
        /// <summary>One-pole low-pass state.</summary>
        public float LowPassState;
        /// <summary>Oscillator age in seconds.</summary>
        public float AgeSeconds;
#pragma warning disable 0169
        private float _pad0;
        private float _pad1;
#pragma warning restore 0169
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct KineticImpactSineOscillatorJob : IJob
    {
        [NoAlias] public NativeArray<float> Output;
        [NoAlias] public NativeArray<KineticImpactSineOscillatorState> State;
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
            float lowPassAlpha = DepthStressGranularMath.ApproximateExpNegPositive(6.2831855f * lowPassCutoff * sampleRateInv);

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
