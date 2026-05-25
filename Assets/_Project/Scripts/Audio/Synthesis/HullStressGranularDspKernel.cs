using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AOT;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio.Synthesis
{
    /// <summary>
    /// Sixty-four-byte hull-stress granular voice consumed by Burst DSP kernels.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct GranularVoiceDTO
    {
        /// <summary>Required byte size for ARM64 cache-line traversal.</summary>
        public const int SizeBytes = 64;

        /// <summary>Absolute universe source epicenter. Subtract listener AUP before float downcast.</summary>
        [FieldOffset(0)] public double3 EpicenterAUP;
        /// <summary>Non-zero FNV-1a material or PCM-bank hash. Zero means inactive voice.</summary>
        [FieldOffset(24)] public uint AudioBankHashID;
        /// <summary>Normalized grain progress in the range 0..1.</summary>
        [FieldOffset(28)] public float PlayheadPosition;
        /// <summary>Grain duration in seconds.</summary>
        [FieldOffset(32)] public float GrainLength;
        /// <summary>Playback speed multiplier.</summary>
        [FieldOffset(36)] public float PitchMultiplier;
        /// <summary>Linear voice amplitude.</summary>
        [FieldOffset(40)] public float Amplitude;
#pragma warning disable 0169
        [FieldOffset(44)] private uint _pad0;
        [FieldOffset(48)] private uint _pad1;
        [FieldOffset(52)] private uint _pad2;
        [FieldOffset(56)] private uint _pad3;
        [FieldOffset(60)] private uint _pad4;
#pragma warning restore 0169
    }

    /// <summary>
    /// Sixty-four-byte audio DSP blackbox telemetry entry.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AudioDspTelemetryEntry
    {
        /// <summary>Required ring capacity for last-frame forensic capture.</summary>
        public const int RingCapacity = 300;
        /// <summary>Required byte size for binary dumps.</summary>
        public const int SizeBytes = 64;

        [FieldOffset(0)] public ulong SampleIndex;
        [FieldOffset(8)] public long DspExecutionTicks;
        [FieldOffset(16)] public float MaxAmplitude;
        [FieldOffset(20)] public int ActiveVoices;
        [FieldOffset(24)] public int StolenVoices;
        [FieldOffset(28)] public int VoiceLimit;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint NanSampleCount;
        [FieldOffset(40)] public float OutputRms;
        [FieldOffset(44)] public float GlobalQualityWeight;
#pragma warning disable 0169
        [FieldOffset(48)] private uint _pad0;
        [FieldOffset(52)] private uint _pad1;
        [FieldOffset(56)] private uint _pad2;
        [FieldOffset(60)] private uint _pad3;
#pragma warning restore 0169
    }

    /// <summary>
    /// Thirty-two-byte cold-boot material profile for hull stress granular synthesis.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HullStressAudioProfileDTO
    {
        [FieldOffset(0)] public uint MaterialHash;
        [FieldOffset(4)] public float PitchMinimum;
        [FieldOffset(8)] public float PitchMaximum;
        [FieldOffset(12)] public float GrainMinimumSeconds;
        [FieldOffset(16)] public float GrainMaximumSeconds;
        [FieldOffset(20)] public float AmplitudeScale;
        [FieldOffset(24)] public uint Flags;
#pragma warning disable 0169
        [FieldOffset(28)] private uint _pad0;
#pragma warning restore 0169
    }

    /// <summary>
    /// Ninety-six-byte audio-block parameters for raw pointer DSP entrypoints.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct HullStressAudioBlockParamsDTO
    {
        public const int SizeBytes = 96;

        [FieldOffset(0)] public double3 ListenerAUP;
        [FieldOffset(24)] public ulong SampleIndexBase;
        [FieldOffset(32)] public long DspExecutionTicks;
        [FieldOffset(40)] public float3 ListenerRight;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public float DistanceRolloff;
        [FieldOffset(60)] public int FrameCount;
        [FieldOffset(64)] public int Channels;
        [FieldOffset(68)] public int SampleRate;
        [FieldOffset(72)] public int StolenVoices;
        [FieldOffset(76)] public uint Flags;
        [FieldOffset(80)] public int OutputSampleCapacity;
#pragma warning disable 0169
        [FieldOffset(84)] private uint _pad0;
        [FieldOffset(88)] private ulong _pad1;
#pragma warning restore 0169
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void EvaluateHullStressGranularAudioDelegate(
        float* outputInterleaved,
        GranularVoiceDTO* voices,
        int voiceCapacity,
        float* pcmBank,
        int pcmBankLength,
        AudioDspTelemetryEntry* telemetryRing,
        int telemetryLength,
        int* telemetryCursor,
        HullStressAudioBlockParamsDTO* blockParams);

    /// <summary>
    /// Shared math for hull-stress granular DSP.
    /// </summary>
    public static class HullStressGranularDspMath
    {
        public const uint DefaultMetalBankHash = 0x4D455441u; // META
        public const uint TelemetryFlagNonFinite = 1u << 0;
        public const uint TelemetryFlagVoiceLimitHit = 1u << 1;
        public const uint TelemetryFlagOutputClamped = 1u << 2;
        public const uint TelemetryFlagNoPcmBank = 1u << 3;
        public const uint TelemetryFlagOutputCapacityInvalid = 1u << 4;

        private const float TwoPi = 6.28318530718f;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        /// <summary>Returns the continuous quality-scaled max polyphony.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolvePolyphonyLimit(float globalQualityWeight, int capacity)
        {
            float q = math.saturate(math.select(1f, globalQualityWeight, math.isfinite(globalQualityWeight)));
            int requested = (int)math.round(math.lerp(8f, 64f, q));
            return math.clamp(requested, 0, math.max(0, capacity));
        }

        /// <summary>Converts contract AUP to absolute double3 without float demotion.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 ToDouble3(in AcousticAup aup)
        {
            double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return math.double3(
                (aup.GridX * cell) + (double)aup.Local.x,
                (aup.GridY * cell) + (double)aup.Local.y,
                (aup.GridZ * cell) + (double)aup.Local.z);
        }

        /// <summary>Computes FNV-1a over lower ASCII bytes.</summary>
        public static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < text.Length; i++)
            {
                byte value = text[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                if (value == (byte)'_' || value == (byte)'-' || value == (byte)' ' || value == (byte)'\t')
                    continue;
                hash = (hash ^ value) * FnvPrime;
            }

            return hash == 0u ? FnvOffset : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteOrZero(float value)
        {
            return math.isfinite(value) ? value : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float FiniteOrDefault(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash == 0u ? FnvOffset : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float Hanning(float t)
        {
            float phase = math.saturate(t);
            float triangle = 1f - math.abs(phase * 2f - 1f);
            return triangle * triangle * (3f - (2f * triangle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float SoftClip(float value)
        {
            return value * math.rcp(1f + math.abs(value));
        }
    }

    /// <summary>
    /// Raw-pointer Burst entrypoints for audio-thread granular mixing.
    /// </summary>
    public static unsafe class HullStressGranularDspKernel
    {
        private static FunctionPointer<EvaluateHullStressGranularAudioDelegate> s_evaluateAudioCallback;

        /// <summary>
        /// Compiles the callback-compatible Burst function pointer during cold bootstrap.
        /// </summary>
        public static FunctionPointer<EvaluateHullStressGranularAudioDelegate> GetOrCreateAudioCallback()
        {
            if (!s_evaluateAudioCallback.IsCreated)
            {
                s_evaluateAudioCallback =
                    BurstCompiler.CompileFunctionPointer<EvaluateHullStressGranularAudioDelegate>(EvaluateAudioCallback);
            }

            return s_evaluateAudioCallback;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(EvaluateHullStressGranularAudioDelegate))]
        public static void EvaluateAudioCallback(
            float* outputInterleaved,
            GranularVoiceDTO* voices,
            int voiceCapacity,
            float* pcmBank,
            int pcmBankLength,
            AudioDspTelemetryEntry* telemetryRing,
            int telemetryLength,
            int* telemetryCursor,
            HullStressAudioBlockParamsDTO* blockParams)
        {
            EvaluateBlock(
                outputInterleaved,
                voices,
                voiceCapacity,
                pcmBank,
                pcmBankLength,
                telemetryRing,
                telemetryLength,
                telemetryCursor,
                blockParams);
        }

        /// <summary>
        /// Mixes granular voices into a caller-owned interleaved buffer without touching managed state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EvaluateBlock(
            float* outputInterleaved,
            GranularVoiceDTO* voices,
            int voiceCapacity,
            float* pcmBank,
            int pcmBankLength,
            AudioDspTelemetryEntry* telemetryRing,
            int telemetryLength,
            int* telemetryCursor,
            HullStressAudioBlockParamsDTO* blockParams)
        {
            if (outputInterleaved == null || blockParams == null)
                return;

            ref HullStressAudioBlockParamsDTO block = ref UnsafeUtility.AsRef<HullStressAudioBlockParamsDTO>(blockParams);
            int outputStride = math.clamp(block.Channels, 1, 8);
            uint flags = block.Flags;
            int outputSampleCapacity = math.max(0, block.OutputSampleCapacity);
            int requestedFrameCount = math.max(0, block.FrameCount);
            int frameCount = outputSampleCapacity > 0
                ? math.min(requestedFrameCount, outputSampleCapacity / outputStride)
                : 0;
            if (frameCount <= 0)
            {
                flags |= HullStressGranularDspMath.TelemetryFlagOutputCapacityInvalid;
                RecordTelemetry(telemetryRing, telemetryLength, telemetryCursor, in block, 0, 0f, 0f, flags, 0u, 0);
                return;
            }

            if (pcmBank == null || pcmBankLength <= 1 || voices == null || voiceCapacity <= 0)
            {
                flags |= HullStressGranularDspMath.TelemetryFlagNoPcmBank;
                RecordTelemetry(telemetryRing, telemetryLength, telemetryCursor, in block, 0, 0f, 0f, flags, 0u, 0);
                return;
            }

            float quality = math.saturate(HullStressGranularDspMath.FiniteOrDefault(block.GlobalQualityWeight, 1f));
            int voiceLimit = HullStressGranularDspMath.ResolvePolyphonyLimit(quality, voiceCapacity);
            float sampleRate = math.max(1f, block.SampleRate);
            float3 listenerRight = NormalizeOrFallback(block.ListenerRight, math.float3(1f, 0f, 0f));
            float rolloff = math.max(0.0001f, HullStressGranularDspMath.FiniteOrDefault(block.DistanceRolloff, 0.5f));
            float interpolationBlend = math.smoothstep(0.18f, 0.72f, quality);
            float maxAbs = 0f;
            float sumSq = 0f;
            uint nanCount = 0u;
            int activeVoices = 0;

            for (int frame = 0; frame < frameCount; frame++)
            {
                float left = 0f;
                float right = 0f;
                for (int voiceIndex = 0; voiceIndex < voiceLimit; voiceIndex++)
                {
                    ref GranularVoiceDTO voice = ref UnsafeUtility.AsRef<GranularVoiceDTO>(voices + voiceIndex);
                    if (voice.AudioBankHashID == 0u ||
                        voice.Amplitude <= 0f ||
                        !math.isfinite(voice.Amplitude))
                        continue;

                    float grainSeconds = math.max(0.001f, HullStressGranularDspMath.FiniteOrDefault(voice.GrainLength, 0.05f));
                    float playhead01 = math.saturate(HullStressGranularDspMath.FiniteOrZero(voice.PlayheadPosition));
                    int grainSamples = math.clamp((int)(grainSeconds * sampleRate + 0.5f), 1, pcmBankLength - 1);
                    int start = ResolveGrainStart(voice.AudioBankHashID, voiceIndex, pcmBankLength, grainSamples);
                    float cursor = start + playhead01 * grainSamples;
                    float nearest = SampleNearest(pcmBank, pcmBankLength, cursor);
                    float linear = SampleLinear(pcmBank, pcmBankLength, cursor);
                    float source = math.lerp(nearest, linear, interpolationBlend);
                    float window = HullStressGranularDspMath.Hanning(playhead01);
                    float amplitude = math.saturate(HullStressGranularDspMath.FiniteOrZero(voice.Amplitude));
                    float3 local = AupPrecisionMath.LocalDeltaFloat3Clamped(
                        voice.EpicenterAUP,
                        block.ListenerAUP,
                        AupPrecisionMath.DefaultMaxLocalCastMeters,
                        float3.zero);
                    float distanceSq = math.max(0.0001f, math.lengthsq(local));
                    float invDistance = math.rsqrt(distanceSq);
                    float3 dir = local * invDistance;
                    float pan = math.clamp(math.dot(dir, listenerRight), -1f, 1f);
                    float attenuation = math.rcp(1f + rolloff * distanceSq * 0.01f);
                    float sample = source * window * amplitude * attenuation;
                    if (!math.isfinite(sample))
                    {
                        sample = 0f;
                        nanCount++;
                        flags |= HullStressGranularDspMath.TelemetryFlagNonFinite;
                    }

                    left += sample * (1f - pan) * 0.5f;
                    right += sample * (1f + pan) * 0.5f;
                    if (frame == 0)
                        activeVoices++;

                    float pitch = math.max(0.000001f, HullStressGranularDspMath.FiniteOrDefault(voice.PitchMultiplier, 1f));
                    voice.PlayheadPosition = playhead01 + pitch * math.rcp(grainSeconds * sampleRate);
                    if (voice.PlayheadPosition >= 1f)
                    {
                        voice.AudioBankHashID = 0u;
                        voice.Amplitude = 0f;
                        voice.PlayheadPosition = 0f;
                    }
                }

                int outputIndex = outputStride == 2 ? frame << 1 : frame * outputStride;
                if (outputStride == 1)
                {
                    float mono = HullStressGranularDspMath.SoftClip((left + right) * 0.5f);
                    outputInterleaved[outputIndex] = math.clamp(
                        HullStressGranularDspMath.FiniteOrZero(outputInterleaved[outputIndex]) + mono,
                        -1f,
                        1f);
                    maxAbs = math.max(maxAbs, math.abs(mono));
                    sumSq += mono * mono;
                }
                else
                {
                    float clippedLeft = HullStressGranularDspMath.SoftClip(left);
                    float clippedRight = HullStressGranularDspMath.SoftClip(right);
                    float frameMax = math.max(math.abs(clippedLeft), math.abs(clippedRight));
                    float frameSumSq = clippedLeft * clippedLeft + clippedRight * clippedRight;
                    outputInterleaved[outputIndex] = math.clamp(
                        HullStressGranularDspMath.FiniteOrZero(outputInterleaved[outputIndex]) + clippedLeft,
                        -1f,
                        1f);
                    outputInterleaved[outputIndex + 1] = math.clamp(
                        HullStressGranularDspMath.FiniteOrZero(outputInterleaved[outputIndex + 1]) + clippedRight,
                        -1f,
                        1f);
                    if (outputStride > 2)
                    {
                        float tail = HullStressGranularDspMath.SoftClip((left + right) * 0.5f);
                        for (int channelIndex = 2; channelIndex < outputStride; channelIndex++)
                        {
                            int channelSampleIndex = outputIndex + channelIndex;
                            outputInterleaved[channelSampleIndex] = math.clamp(
                                HullStressGranularDspMath.FiniteOrZero(outputInterleaved[channelSampleIndex]) + tail,
                                -1f,
                                1f);
                        }

                        frameMax = math.max(frameMax, math.abs(tail));
                        frameSumSq += tail * tail * (outputStride - 2);
                    }

                    maxAbs = math.max(maxAbs, frameMax);
                    sumSq += frameSumSq;
                }
            }

            if (activeVoices >= voiceLimit && voiceLimit > 0)
                flags |= HullStressGranularDspMath.TelemetryFlagVoiceLimitHit;
            if (maxAbs >= 0.999f)
                flags |= HullStressGranularDspMath.TelemetryFlagOutputClamped;

            float rms = frameCount > 0
                ? math.sqrt(math.max(0f, sumSq) * math.rcp(frameCount * outputStride))
                : 0f;
            RecordTelemetry(
                telemetryRing,
                telemetryLength,
                telemetryCursor,
                in block,
                activeVoices,
                maxAbs,
                rms,
                flags,
                nanCount,
                voiceLimit);
        }

        private static void RecordTelemetry(
            AudioDspTelemetryEntry* telemetryRing,
            int telemetryLength,
            int* telemetryCursor,
            in HullStressAudioBlockParamsDTO block,
            int activeVoices,
            float maxAmplitude,
            float rms,
            uint flags,
            uint nanSampleCount,
            int voiceLimit)
        {
            if (telemetryRing == null || telemetryLength <= 0)
                return;

            int cursor = telemetryCursor != null ? *telemetryCursor : 0;
            if ((uint)cursor >= (uint)telemetryLength)
                cursor = 0;

            AudioDspTelemetryEntry entry = default;
            entry.SampleIndex = block.SampleIndexBase;
            entry.DspExecutionTicks = block.DspExecutionTicks;
            entry.MaxAmplitude = math.saturate(HullStressGranularDspMath.FiniteOrZero(maxAmplitude));
            entry.ActiveVoices = math.max(0, activeVoices);
            entry.StolenVoices = math.max(0, block.StolenVoices);
            entry.VoiceLimit = math.max(0, voiceLimit);
            entry.Flags = flags;
            entry.NanSampleCount = nanSampleCount;
            entry.OutputRms = math.saturate(HullStressGranularDspMath.FiniteOrZero(rms));
            entry.GlobalQualityWeight = math.saturate(HullStressGranularDspMath.FiniteOrDefault(block.GlobalQualityWeight, 1f));
            telemetryRing[cursor] = entry;

            cursor++;
            if (cursor >= telemetryLength)
                cursor = 0;
            if (telemetryCursor != null)
                *telemetryCursor = cursor;
        }

        private static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.isfinite(lengthSq) && lengthSq > 0.000001f
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static int ResolveGrainStart(uint hash, int voiceIndex, int bankLength, int grainSamples)
        {
            int range = math.max(1, bankLength - grainSamples - 1);
            uint mixed = HullStressGranularDspMath.MixHash(hash, (uint)(voiceIndex + 1) * 0x9E3779B9u);
            return (int)(mixed % (uint)range);
        }

        private static float SampleNearest(float* samples, int length, float cursor)
        {
            float wrapped = cursor - math.floor(cursor * math.rcp((float)length)) * length;
            int index = math.clamp((int)(wrapped + 0.5f), 0, length - 1);
            return samples[index];
        }

        private static float SampleLinear(float* samples, int length, float cursor)
        {
            float wrapped = cursor - math.floor(cursor * math.rcp((float)length)) * length;
            int i0 = math.clamp((int)wrapped, 0, length - 1);
            int i1 = i0 + 1;
            if (i1 >= length)
                i1 = 0;
            return math.lerp(samples[i0], samples[i1], math.saturate(wrapped - i0));
        }
    }

    /// <summary>
    /// Burst producer for deterministic structural warning stress tests.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockStressAudioJob : IJob
    {
        [NoAlias] public NativeArray<BaseStructuralWarningSignal> OutputSignals;
        [NoAlias] public NativeArray<int> OutputCount;
        public double3 CenterAUP;
        public uint BaseHash;
        public uint Frame;
        public int RequestedSignalCount;

        /// <summary>Writes synthetic structural warning signals into the caller-owned buffer.</summary>
        public void Execute()
        {
            if (!OutputSignals.IsCreated || OutputSignals.Length <= 0)
                return;

            int count = math.clamp(RequestedSignalCount <= 0 ? 100 : RequestedSignalCount, 0, OutputSignals.Length);
            count = math.min(count, 100);
            for (int i = 0; i < count; i++)
            {
                uint hash = HullStressGranularDspMath.MixHash(BaseHash == 0u ? 0x42535744u : BaseHash, (uint)i);
                float angle = (i * 2.3999631f) + (Frame * 0.017f);
                float radius = math.lerp(1.5f, 42f, ((hash >> 8) & 255u) * (1f / 255f));
                Hecton8.Core.MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                float vertical = Hecton8.Core.MathLodApproximation.ApproxSinBhaskara(angle * 0.37f) * 2.5f;
                double3 offset = math.double3(
                    cos * radius,
                    vertical,
                    sin * radius);
                double3 aup = CenterAUP + offset;
                float stress = math.saturate(0.18f + ((hash & 1023u) * (1f / 1023f)) * 0.82f);
                float panic = math.saturate(((hash >> 10) & 1023u) * (1f / 1023f));
                BaseStructuralWarningSignal signal = default;
                signal.EpicenterAup = ToAcousticAup(aup);
                signal.BaseHash = hash;
                signal.Frame = Frame;
                signal.HighestStress01 = stress;
                signal.AudioIntensity01 = math.saturate(0.45f + stress * 0.55f);
                signal.PanicScalar01 = panic;
                signal.CriticalFlags = stress > 0.86f ? BaseStructuralWarningSignal.FlagRedAlert : 0u;
                OutputSignals[i] = signal;
            }

            if (OutputCount.IsCreated && OutputCount.Length > 0)
                OutputCount[0] = count;
        }

        private static AcousticAup ToAcousticAup(double3 absolute)
        {
            double cell = HectonPhysicsContract.AupSectorSizeMetersDouble;
            const double invCell = 1.0d / HectonPhysicsContract.AupSectorSizeMetersDouble;
            long gridX = (long)math.floor(absolute.x * invCell);
            long gridY = (long)math.floor(absolute.y * invCell);
            long gridZ = (long)math.floor(absolute.z * invCell);
            AcousticAup aup = default;
            aup.GridX = gridX;
            aup.GridY = gridY;
            aup.GridZ = gridZ;
            aup.Local = math.float3(
                (float)(absolute.x - gridX * cell),
                (float)(absolute.y - gridY * cell),
                (float)(absolute.z - gridZ * cell));
            return aup;
        }
    }

    /// <summary>
    /// Converts structural warning signals into granular voice DTOs.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct MapStressToAudioParamsJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BaseStructuralWarningSignal> Signals;
        [NoAlias] public NativeArray<GranularVoiceDTO> Voices;
        [NoAlias] public NativeArray<int> Counters;
        public double3 ListenerAUP;
        public int SignalCount;
        public float GlobalQualityWeight;
        public float BaseGrainLengthSeconds;
        public uint DefaultAudioBankHashID;

        /// <summary>Allocates or steals granular voice slots from structural warnings.</summary>
        public void Execute()
        {
            if (!Signals.IsCreated || !Voices.IsCreated || Voices.Length <= 0)
                return;

            int signalCount = math.min(math.max(0, SignalCount), Signals.Length);
            int voiceLimit = HullStressGranularDspMath.ResolvePolyphonyLimit(GlobalQualityWeight, Voices.Length);
            int stolen = 0;
            int active = 0;
            for (int i = voiceLimit; i < Voices.Length; i++)
            {
                GranularVoiceDTO dead = Voices[i];
                dead.AudioBankHashID = 0u;
                dead.Amplitude = 0f;
                Voices[i] = dead;
            }

            GranularVoiceDTO* voicePtr = (GranularVoiceDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Voices);
            for (int signalIndex = 0; signalIndex < signalCount; signalIndex++)
            {
                BaseStructuralWarningSignal signal = Signals[signalIndex];
                if (!AcousticAup.IsFinite(in signal.EpicenterAup))
                    continue;

                float stress = math.saturate(HullStressGranularDspMath.FiniteOrZero(signal.HighestStress01));
                float intensity = math.saturate(HullStressGranularDspMath.FiniteOrZero(signal.AudioIntensity01));
                float amplitude = stress * math.max(0.08f, intensity);
                if (amplitude <= 0.0001f)
                    continue;

                int slot = ResolveVoiceSlot(voicePtr, voiceLimit, amplitude, ListenerAUP, out bool stoleSlot);
                if (slot < 0)
                    continue;

                if (stoleSlot)
                    stolen++;

                ref GranularVoiceDTO voice = ref UnsafeUtility.AsRef<GranularVoiceDTO>(voicePtr + slot);
                voice.EpicenterAUP = HullStressGranularDspMath.ToDouble3(in signal.EpicenterAup);
                voice.AudioBankHashID = signal.BaseHash != 0u
                    ? signal.BaseHash
                    : (DefaultAudioBankHashID == 0u ? HullStressGranularDspMath.DefaultMetalBankHash : DefaultAudioBankHashID);
                voice.PlayheadPosition = 0f;
                float baseGrainLength = math.max(
                    0.006f,
                    HullStressGranularDspMath.FiniteOrDefault(BaseGrainLengthSeconds, 0.05f));
                voice.GrainLength = math.lerp(
                    math.max(0.006f, baseGrainLength * 0.65f),
                    math.max(0.012f, baseGrainLength * 1.85f),
                    stress);
                voice.PitchMultiplier = math.lerp(1.34f, 0.54f, stress);
                voice.Amplitude = math.saturate(amplitude);
            }

            for (int i = 0; i < voiceLimit; i++)
            {
                if (Voices[i].AudioBankHashID != 0u)
                    active++;
            }

            if (Counters.IsCreated && Counters.Length > 0)
            {
                Counters[0] = active;
                if (Counters.Length > 1)
                    Counters[1] = stolen;
                if (Counters.Length > 2)
                    Counters[2] = voiceLimit;
            }
        }

        private static int ResolveVoiceSlot(
            GranularVoiceDTO* voices,
            int voiceLimit,
            float newAmplitude,
            double3 listenerAup,
            out bool stolen)
        {
            stolen = false;
            if (voiceLimit <= 0)
                return -1;

            int victim = -1;
            float victimPriority = float.MaxValue;
            for (int i = 0; i < voiceLimit; i++)
            {
                ref GranularVoiceDTO voice = ref UnsafeUtility.AsRef<GranularVoiceDTO>(voices + i);
                float existingAmplitude = HullStressGranularDspMath.FiniteOrZero(voice.Amplitude);
                if (voice.AudioBankHashID == 0u || existingAmplitude <= 0f)
                    return i;

                double distanceSq = AupPrecisionMath.DistanceSqSafeDouble(voice.EpicenterAUP, listenerAup);
                float distancePenalty = distanceSq >= float.MaxValue ? 1f : (float)math.min(1.0d, distanceSq * 0.000001d);
                float priority = math.saturate(existingAmplitude) * (1f - distancePenalty);
                if (priority < victimPriority)
                {
                    victimPriority = priority;
                    victim = i;
                }
            }

            if (victim >= 0 && newAmplitude > victimPriority)
            {
                stolen = true;
                return victim;
            }

            return -1;
        }
    }

    /// <summary>
    /// Single-writer granular mixer intended for audio-block execution.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateGranularVoicesJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<float> PcmBank;
        [NoAlias] public NativeArray<GranularVoiceDTO> Voices;
        [NoAlias] public NativeArray<float> OutputInterleaved;
        [NoAlias] public NativeArray<AudioDspTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [ReadOnly, NoAlias] public NativeArray<int> Counters;
        public double3 ListenerAUP;
        public float3 ListenerRight;
        public ulong SampleIndexBase;
        public long DspExecutionTicks;
        public int FrameCount;
        public int Channels;
        public int SampleRate;
        public float GlobalQualityWeight;
        public float DistanceRolloff;

        /// <summary>Mixes active voices into an interleaved output buffer.</summary>
        public void Execute()
        {
            if (!OutputInterleaved.IsCreated || FrameCount <= 0)
                return;

            int channels = math.clamp(Channels, 1, 8);
            int outputFrames = math.min(FrameCount, OutputInterleaved.Length / channels);
            int stolen = Counters.IsCreated && Counters.Length > 1 ? Counters[1] : 0;
            HullStressAudioBlockParamsDTO block = default;
            block.ListenerAUP = ListenerAUP;
            block.SampleIndexBase = SampleIndexBase;
            block.DspExecutionTicks = DspExecutionTicks;
            block.ListenerRight = ListenerRight;
            block.GlobalQualityWeight = GlobalQualityWeight;
            block.DistanceRolloff = DistanceRolloff;
            block.FrameCount = outputFrames;
            block.Channels = channels;
            block.SampleRate = SampleRate;
            block.StolenVoices = stolen;
            block.OutputSampleCapacity = OutputInterleaved.Length;
            block.Flags = 0u;

            float* outputPtr = (float*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(OutputInterleaved);
            GranularVoiceDTO* voicePtr = Voices.IsCreated
                ? (GranularVoiceDTO*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(Voices)
                : null;
            float* pcmPtr = PcmBank.IsCreated
                ? (float*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(PcmBank)
                : null;
            AudioDspTelemetryEntry* telemetryPtr = TelemetryRing.IsCreated
                ? (AudioDspTelemetryEntry*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryRing)
                : null;
            int* telemetryCursorPtr = TelemetryCursor.IsCreated && TelemetryCursor.Length > 0
                ? (int*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(TelemetryCursor)
                : null;

            HullStressGranularDspKernel.EvaluateBlock(
                outputPtr,
                voicePtr,
                Voices.IsCreated ? Voices.Length : 0,
                pcmPtr,
                PcmBank.IsCreated ? PcmBank.Length : 0,
                telemetryPtr,
                TelemetryRing.IsCreated ? TelemetryRing.Length : 0,
                telemetryCursorPtr,
                &block);
        }
    }

    /// <summary>
    /// Cold-boot CSV parser for hull stress audio material profiles.
    /// </summary>
#if UNITY_EDITOR
    public static class HullStressAudioProfileCsv
    {
        /// <summary>Parses rows in material,pitchMin,pitchMax,grainMinMs,grainMaxMs,amplitude format.</summary>
        public static int ParseRows(ReadOnlySpan<byte> csv, NativeArray<HullStressAudioProfileDTO> destination)
        {
            if (!destination.IsCreated || destination.Length <= 0 || csv.Length <= 0)
                return 0;

            int cursor = 0;
            int count = 0;
            while (count < destination.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                if (!TryParseRow(line, out HullStressAudioProfileDTO profile))
                    continue;

                destination[count] = profile;
                count++;
            }

            return count;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row, out HullStressAudioProfileDTO profile)
        {
            profile = default;
            int c0 = IndexOf(row, (byte)',', 0);
            if (c0 <= 0)
                return false;
            int c1 = IndexOf(row, (byte)',', c0 + 1);
            int c2 = c1 >= 0 ? IndexOf(row, (byte)',', c1 + 1) : -1;
            int c3 = c2 >= 0 ? IndexOf(row, (byte)',', c2 + 1) : -1;
            int c4 = c3 >= 0 ? IndexOf(row, (byte)',', c3 + 1) : -1;
            if (c1 < 0 || c2 < 0 || c3 < 0 || c4 < 0)
                return false;

            if (!TryParseFloat(Trim(row.Slice(c0 + 1, c1 - c0 - 1)), out float pitchMin) ||
                !TryParseFloat(Trim(row.Slice(c1 + 1, c2 - c1 - 1)), out float pitchMax) ||
                !TryParseFloat(Trim(row.Slice(c2 + 1, c3 - c2 - 1)), out float grainMinMs) ||
                !TryParseFloat(Trim(row.Slice(c3 + 1, c4 - c3 - 1)), out float grainMaxMs) ||
                !TryParseFloat(Trim(row.Slice(c4 + 1)), out float amplitude))
            {
                return false;
            }

            profile.MaterialHash = HullStressGranularDspMath.HashLowerAscii(Trim(row.Slice(0, c0)));
            profile.PitchMinimum = math.clamp(pitchMin, 0.05f, 4f);
            profile.PitchMaximum = math.clamp(math.max(pitchMax, pitchMin), 0.05f, 4f);
            profile.GrainMinimumSeconds = math.clamp(grainMinMs * 0.001f, 0.001f, 1f);
            profile.GrainMaximumSeconds = math.clamp(math.max(grainMaxMs, grainMinMs) * 0.001f, 0.001f, 1f);
            profile.AmplitudeScale = math.clamp(amplitude, 0f, 4f);
            profile.Flags = 1u;
            return profile.MaterialHash != 0u;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            if (cursor >= text.Length)
            {
                line = default;
                return false;
            }

            int start = cursor;
            while (cursor < text.Length && text[cursor] != (byte)'\n' && text[cursor] != (byte)'\r')
                cursor++;

            line = text.Slice(start, cursor - start);
            if (cursor < text.Length && text[cursor] == (byte)'\r')
                cursor++;
            if (cursor < text.Length && text[cursor] == (byte)'\n')
                cursor++;
            return true;
        }

        private static int IndexOf(ReadOnlySpan<byte> text, byte target, int start)
        {
            for (int i = math.max(0, start); i < text.Length; i++)
            {
                if (text[i] == target)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start <= end && IsWhitespace(text[start]))
                start++;
            while (end >= start && IsWhitespace(text[end]))
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> text, out float value)
        {
            value = 0f;
            text = Trim(text);
            if (text.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (text[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (text[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
            {
                integer = integer * 10f + (text[index] - (byte)'0');
                index++;
                any = true;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (index < text.Length && text[index] == (byte)'.')
            {
                index++;
                while (index < text.Length && text[index] >= (byte)'0' && text[index] <= (byte)'9')
                {
                    fraction += (text[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                    any = true;
                }
            }

            if (!any)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }
    }
#endif
}
