using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    /// <summary>
    /// Cold-path native buffer maintenance for player-critical DSP buffers.
    /// </summary>
    internal static class PlayerCriticalBufferJobs
    {
        [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]
        [StructLayout(LayoutKind.Sequential)]
        public struct DopplerShiftBatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> SourceFrequencies;
            [ReadOnly] public NativeArray<float> RelativeVelocitiesMetersPerSecond;
            [WriteOnly] public NativeArray<float> ShiftedFrequencies;
            public float SpeedOfSoundMetersPerSecond;
            public float SpeedOfSoundMetersPerSecondInv;

            public void Execute(int index)
            {
                if (!ShiftedFrequencies.IsCreated || (uint)index >= (uint)ShiftedFrequencies.Length)
                    return;

                float sourceFrequency = SourceFrequencies.IsCreated && (uint)index < (uint)SourceFrequencies.Length
                    ? SourceFrequencies[index]
                    : 0f;
                float relativeVelocity =
                    RelativeVelocitiesMetersPerSecond.IsCreated &&
                    (uint)index < (uint)RelativeVelocitiesMetersPerSecond.Length
                        ? RelativeVelocitiesMetersPerSecond[index]
                        : 0f;
                sourceFrequency = math.isfinite(sourceFrequency) ? sourceFrequency : 0f;
                relativeVelocity = math.isfinite(relativeVelocity) ? relativeVelocity : 0f;
                float soundSpeed = math.max(1f, SpeedOfSoundMetersPerSecond);
                float soundSpeedInv = SpeedOfSoundMetersPerSecondInv > 0f
                    ? SpeedOfSoundMetersPerSecondInv
                    : math.rcp(soundSpeed);
                float velocityLimit = soundSpeed * 0.9f;
                float clampedRelativeVelocity = math.clamp(relativeVelocity, -velocityLimit, velocityLimit);
                float pitchRatio = math.clamp(
                    1f + (clampedRelativeVelocity * soundSpeedInv),
                    0.1f,
                    1.9f);
                float shiftedFrequency = sourceFrequency * pitchRatio;
                ShiftedFrequencies[index] = math.isfinite(shiftedFrequency)
                    ? shiftedFrequency
                    : sourceFrequency;
            }
        }

        [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]
        [StructLayout(LayoutKind.Sequential)]
        public struct BinauralVoxelAcousticsOutputJob : IJob
        {
            [ReadOnly] public NativeArray<float> MonoInput;
            public NativeArray<float> StereoOutput;
            public NativeArray<float> DelayRing;
            public NativeArray<int> DelayWriteIndexState;
            public NativeArray<float> ShadowHistory;
            public int FrameCount;
            public int DelayMask;
            public int DelayWriteIndex;
            public float LeftDelaySamples;
            public float RightDelaySamples;
            public float BinauralMix01;
            public float ContralateralGain;
            public float ShadowAlpha;
            public int ShadowLeft;
            public int ShadowRight;

            public void Execute()
            {
                if (!StereoOutput.IsCreated || FrameCount <= 0)
                    return;

                if (!MonoInput.IsCreated ||
                    !DelayRing.IsCreated ||
                    !ShadowHistory.IsCreated ||
                    DelayRing.Length <= 1 ||
                    ShadowHistory.Length < 2)
                {
                    ClearStereoOutput();
                    return;
                }

                int delayLength = DelayRing.Length;
                if (!IsPowerOfTwo(delayLength))
                {
                    ClearStereoOutput();
                    return;
                }

                int safeFrameCount = math.min(FrameCount, math.min(MonoInput.Length, StereoOutput.Length >> 1));
                int delayMask = DelayMask == delayLength - 1 ? DelayMask : delayLength - 1;
                int writeIndex = DelayWriteIndexState.IsCreated && DelayWriteIndexState.Length > 0
                    ? DelayWriteIndexState[0] & delayMask
                    : DelayWriteIndex & delayMask;
                float maxDelaySamples = math.max(0f, delayLength - 2f);
                float leftDelay = math.clamp(LeftDelaySamples, 0f, maxDelaySamples);
                float rightDelay = math.clamp(RightDelaySamples, 0f, maxDelaySamples);
                float mix = math.saturate(BinauralMix01);
                float contraGain = math.saturate(ContralateralGain);
                float alpha = math.saturate(ShadowAlpha);
                for (int frameIndex = 0; frameIndex < safeFrameCount; frameIndex++)
                {
                    float mono = MonoInput[frameIndex];
                    mono = math.isfinite(mono) ? mono : 0f;
                    DelayRing[writeIndex] = mono;
                    float left = leftDelay > 0f
                        ? SampleDelay(DelayRing, delayMask, writeIndex, leftDelay)
                        : mono;
                    float right = rightDelay > 0f
                        ? SampleDelay(DelayRing, delayMask, writeIndex, rightDelay)
                        : mono;

                    if (ShadowLeft != 0)
                        left = ApplyShadow(ShadowHistory, 0, left * contraGain, alpha);
                    if (ShadowRight != 0)
                        right = ApplyShadow(ShadowHistory, 1, right * contraGain, alpha);

                    int stereoIndex = frameIndex << 1;
                    StereoOutput[stereoIndex] = math.clamp(math.lerp(mono, left, mix), -1f, 1f);
                    StereoOutput[stereoIndex + 1] = math.clamp(math.lerp(mono, right, mix), -1f, 1f);
                    writeIndex = (writeIndex + 1) & delayMask;
                }

                ClearStereoTail(safeFrameCount);

                if (DelayWriteIndexState.IsCreated && DelayWriteIndexState.Length > 0)
                    DelayWriteIndexState[0] = writeIndex;
            }

            private void ClearStereoOutput()
            {
                int outputCount = math.min(math.max(0, FrameCount << 1), StereoOutput.Length);
                for (int i = 0; i < outputCount; i++)
                    StereoOutput[i] = 0f;
            }

            private void ClearStereoTail(int processedFrameCount)
            {
                int tailStart = math.max(0, processedFrameCount << 1);
                int outputCount = math.min(math.max(0, FrameCount << 1), StereoOutput.Length);
                for (int i = tailStart; i < outputCount; i++)
                    StereoOutput[i] = 0f;
            }

            private static bool IsPowerOfTwo(int value)
            {
                return value > 0 && (value & (value - 1)) == 0;
            }

            private static float SampleDelay(NativeArray<float> delayRing, int delayMask, int writeIndex, float delaySamples)
            {
                float clampedDelay = math.max(0f, delaySamples);
                int baseDelay = (int)clampedDelay;
                float fraction = clampedDelay - baseDelay;
                float sample0 = delayRing[(writeIndex - baseDelay) & delayMask];
                float sample1 = delayRing[(writeIndex - baseDelay - 1) & delayMask];
                return math.lerp(sample0, sample1, fraction);
            }

            private static float ApplyShadow(NativeArray<float> shadowHistory, int earIndex, float sample, float alpha)
            {
                float previous = shadowHistory[earIndex];
                float filtered = sample + alpha * (previous - sample);
                shadowHistory[earIndex] = filtered;
                return filtered;
            }
        }

        [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]
        [StructLayout(LayoutKind.Sequential)]
        public struct GranularSynthesisBlockJob : IJob
        {
            [ReadOnly] public NativeArray<float> GrainBank;
            [WriteOnly] public NativeArray<float> Output;
            public NativeArray<int> VoiceActive;
            public NativeArray<int> VoiceElapsed;
            public NativeArray<int> VoiceLength;
            public NativeArray<int> VoiceStart;
            public NativeArray<float> VoiceCursor;
            public NativeArray<float> VoicePlaybackRate;
            public NativeArray<float> VoiceGain;
            public int FrameCount;
            public int VoiceCount;
            public int LinearEnvelope;
            public int HermiteInterpolation;

            public void Execute()
            {
                if (!Output.IsCreated || FrameCount <= 0)
                    return;

                int safeFrameCount = math.min(FrameCount, Output.Length);
                if (!GrainBank.IsCreated ||
                    GrainBank.Length <= 1 ||
                    !VoiceActive.IsCreated ||
                    !VoiceElapsed.IsCreated ||
                    !VoiceLength.IsCreated ||
                    !VoiceStart.IsCreated ||
                    !VoiceCursor.IsCreated ||
                    !VoicePlaybackRate.IsCreated ||
                    !VoiceGain.IsCreated)
                {
                    ClearOutput(safeFrameCount);
                    return;
                }

                int safeVoiceCount = math.min(
                    math.max(0, VoiceCount),
                    math.min(
                        VoiceActive.Length,
                        math.min(
                            VoiceElapsed.Length,
                            math.min(
                                VoiceLength.Length,
                                math.min(
                                    VoiceStart.Length,
                                    math.min(
                                        VoiceCursor.Length,
                                        math.min(VoicePlaybackRate.Length, VoiceGain.Length)))))));

                for (int frameIndex = 0; frameIndex < safeFrameCount; frameIndex++)
                {
                    float mixed = 0f;
                    for (int voiceIndex = 0; voiceIndex < safeVoiceCount; voiceIndex++)
                    {
                        if (VoiceActive[voiceIndex] == 0)
                            continue;

                        int elapsed = VoiceElapsed[voiceIndex];
                        int length = math.max(1, VoiceLength[voiceIndex]);
                        if (elapsed >= length)
                        {
                            VoiceActive[voiceIndex] = 0;
                            continue;
                        }

                        float cursor = VoiceCursor[voiceIndex];
                        if (!math.isfinite(cursor))
                            cursor = 0f;

                        float sample = HermiteInterpolation != 0
                            ? HermiteSampleGrainWindow(GrainBank, VoiceStart[voiceIndex], length, cursor)
                            : LinearSampleGrainWindow(GrainBank, VoiceStart[voiceIndex], length, cursor);
                        float envelope = LinearEnvelope != 0
                            ? ResolveLinearGrainEnvelope(elapsed, length)
                            : ResolveParabolicGrainEnvelope(elapsed, length);
                        mixed += sample * envelope * VoiceGain[voiceIndex];

                        cursor += math.max(0.05f, VoicePlaybackRate[voiceIndex]);
                        if (cursor >= length)
                            cursor -= length;

                        elapsed++;
                        if (elapsed >= length)
                        {
                            VoiceActive[voiceIndex] = 0;
                            continue;
                        }

                        VoiceCursor[voiceIndex] = cursor;
                        VoiceElapsed[voiceIndex] = elapsed;
                    }

                    Output[frameIndex] = FastSoftClip(mixed);
                }
            }

            private void ClearOutput(int safeFrameCount)
            {
                for (int i = 0; i < safeFrameCount; i++)
                    Output[i] = 0f;
            }

            private static float LinearSampleGrainWindow(NativeArray<float> buffer, int grainStartIndex, int grainLength, float cursor)
            {
                if (!buffer.IsCreated || buffer.Length <= 0 || grainLength <= 0)
                    return 0f;

                int safeLength = math.min(math.max(1, grainLength), buffer.Length);
                if (!math.isfinite(cursor))
                    cursor = 0f;

                int baseIndex = (int)cursor;
                float t = cursor - baseIndex;
                if (t < 0f)
                {
                    baseIndex--;
                    t += 1f;
                }

                if (baseIndex < 0)
                {
                    baseIndex = 0;
                    t = 0f;
                }
                else if (baseIndex >= safeLength)
                {
                    baseIndex = safeLength - 1;
                    t = 0f;
                }

                int nextIndex = baseIndex + 1;
                if (nextIndex >= safeLength)
                    nextIndex = 0;

                int source0 = WrapIndex(grainStartIndex + baseIndex, buffer.Length);
                int source1 = WrapIndex(grainStartIndex + nextIndex, buffer.Length);
                return math.lerp(buffer[source0], buffer[source1], t);
            }

            private static float HermiteSampleGrainWindow(NativeArray<float> buffer, int grainStartIndex, int grainLength, float cursor)
            {
                if (!buffer.IsCreated || buffer.Length <= 0 || grainLength <= 0)
                    return 0f;

                int safeLength = math.min(math.max(1, grainLength), buffer.Length);
                if (!math.isfinite(cursor))
                    cursor = 0f;

                int baseIndex = (int)cursor;
                float t = cursor - baseIndex;
                if (t < 0f)
                {
                    baseIndex--;
                    t += 1f;
                }

                if (baseIndex < 0)
                {
                    baseIndex = 0;
                    t = 0f;
                }
                else if (baseIndex >= safeLength)
                {
                    baseIndex = safeLength - 1;
                    t = 0f;
                }

                int prevIndex = WrapLocalIndex(baseIndex - 1, safeLength);
                int nextIndex = WrapLocalIndex(baseIndex + 1, safeLength);
                int nextNextIndex = WrapLocalIndex(baseIndex + 2, safeLength);
                float p0 = buffer[WrapIndex(grainStartIndex + prevIndex, buffer.Length)];
                float p1 = buffer[WrapIndex(grainStartIndex + baseIndex, buffer.Length)];
                float p2 = buffer[WrapIndex(grainStartIndex + nextIndex, buffer.Length)];
                float p3 = buffer[WrapIndex(grainStartIndex + nextNextIndex, buffer.Length)];
                float t2 = t * t;
                float t3 = t2 * t;
                return 0.5f * (
                    (2f * p1) +
                    ((p2 - p0) * t) +
                    (((2f * p0) - (5f * p1) + (4f * p2) - p3) * t2) +
                    (((-p0) + (3f * p1) - (3f * p2) + p3) * t3));
            }

            private static float ResolveLinearGrainEnvelope(int elapsed, int length)
            {
                if (length <= 1)
                    return 1f;

                float phase = math.saturate(elapsed * math.rcp((float)(length - 1)));
                return math.saturate(1f - math.abs((phase * 2f) - 1f));
            }

            private static float ResolveParabolicGrainEnvelope(int elapsed, int length)
            {
                if (length <= 1)
                    return 1f;

                float phase = math.saturate(elapsed * math.rcp((float)(length - 1)));
                float x = (phase * 2f) - 1f;
                return math.saturate(1f - (x * x));
            }

            private static int WrapIndex(int index, int length)
            {
                if (length <= 0)
                    return 0;

                if (index < 0)
                    return 0;
                if (index >= length)
                    return length - 1;
                return index;
            }

            private static int WrapLocalIndex(int index, int length)
            {
                if (length <= 1)
                    return 0;
                if (index < 0)
                    return length - 1;
                if (index >= length)
                    return math.min(index - length, length - 1);
                return index;
            }

            private static float FastSoftClip(float value)
            {
                return value * math.rcp(1f + math.abs(value));
            }
        }

        [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]
        [StructLayout(LayoutKind.Sequential)]
        public struct VwsCooldownDecayJob : IJob
        {
            public NativeArray<float> Cooldowns;
            public float DeltaSeconds;

            public void Execute()
            {
                if (!Cooldowns.IsCreated)
                    return;

                float delta = math.max(0f, DeltaSeconds);
                for (int i = 1; i < Cooldowns.Length; i++)
                {
                    float current = Cooldowns[i];
                    current = math.isfinite(current) ? current : 0f;
                    Cooldowns[i] = math.max(0f, current - delta);
                }
            }
        }

        [BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]
        [StructLayout(LayoutKind.Sequential)]
        public struct VwsPrioritySortJob : IJob
        {
            public NativeArray<byte> Queue;
            public int QueueCount;

            public void Execute()
            {
                if (!Queue.IsCreated)
                    return;

                int count = math.clamp(QueueCount, 0, Queue.Length);
                for (int i = 1; i < count; i++)
                {
                    byte value = Queue[i];
                    int j = i - 1;
                    while (j >= 0 && IsLowerPriority(Queue[j], value))
                    {
                        Queue[j + 1] = Queue[j];
                        j--;
                    }

                    Queue[j + 1] = value;
                }
            }

            private static bool IsLowerPriority(byte existing, byte incoming)
            {
                if (existing == 0)
                    return incoming != 0;
                if (incoming == 0)
                    return false;

                return existing > incoming;
            }
        }

        public static void Clear(NativeArray<float> buffer, int count)
        {
            if (!buffer.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, buffer.Length);
            if (safeCount <= 0)
                return;

            // COLD NATIVE CLEAR: audio configuration/reset path only; producer thread is stopped before this is called.
            unsafe
            {
                void* bufferPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
                long byteCount = (long)safeCount * UnsafeUtility.SizeOf<float>();
                UnsafeUtility.MemClear(bufferPtr, byteCount);
            }
        }
    }
}
