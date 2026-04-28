using System;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    internal unsafe sealed class AudioFrameSpscRingBuffer : IDisposable
    {
        private NativeArray<float> _frames;
        private NativeArray<int> _sharedState;
        private int _capacityFrames;
        private int _capacityMask;

        public bool IsCreated => _frames.IsCreated && _sharedState.IsCreated;
        public int CapacityFrames => _capacityFrames;

        public int BufferedFrames
        {
            get
            {
                if (!IsCreated)
                    return 0;

                int writeIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
                int readIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
                return (writeIndex - readIndex) & _capacityMask;
            }
        }

        public int WritableFrames => !IsCreated
            ? 0
            : math.max(0, _capacityFrames - BufferedFrames - 1);

        public void GetState(out int bufferedFrames, out int writableFrames)
        {
            if (!IsCreated)
            {
                bufferedFrames = 0;
                writableFrames = 0;
                return;
            }

            int writeIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
            int readIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
            bufferedFrames = (writeIndex - readIndex) & _capacityMask;
            writableFrames = _capacityFrames - bufferedFrames - 1;
        }

        public void Initialize(int capacityFrames)
        {
            int resolvedCapacity = math.max(256, NextPowerOfTwo(capacityFrames));
            if (IsCreated && _frames.Length == resolvedCapacity)
            {
                Clear();
                return;
            }

            Dispose();
            _frames = new NativeArray<float>(resolvedCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacityFrames] - lock-free procedural audio frame ring buffer storage - owner: AudioFrameSpscRingBuffer
            _sharedState = new NativeArray<int>(NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[6] - shared SPSC read/write state and native bridge guards - owner: AudioFrameSpscRingBuffer
            _capacityFrames = resolvedCapacity;
            _capacityMask = resolvedCapacity - 1;
            Clear();
        }

        public void Clear()
        {
            if (!IsCreated)
                return;

            WriteSharedMetadata();
            WriteSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot, 0);
            WriteSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot, 0);
        }

        public bool TryWrite(NativeArray<float> source, int frameCount)
        {
            if (!IsCreated || !source.IsCreated || frameCount <= 0)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length);
            if (safeFrameCount <= 0)
                return false;

            int readIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
            int writeIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
            int availableFrames = (writeIndex - readIndex) & _capacityMask;
            int freeFrames = _capacityFrames - availableFrames - 1;
            if (safeFrameCount > freeFrames)
                return false;

            for (int i = 0; i < safeFrameCount; i++)
                _frames[(writeIndex + i) & _capacityMask] = source[i];

            WriteSharedIndex(
                NativeAudioKernelRingBufferDescriptor.WriteIndexSlot,
                (writeIndex + safeFrameCount) & _capacityMask);
            return true;
        }

        public void MixInterleavedInto(float[] destination, int channels)
        {
            if (!IsCreated || destination == null || channels <= 0)
                return;

            int frameCount = destination.Length / channels;
            if (frameCount <= 0)
                return;

            int readIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
            int writeIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
            int bufferedFrames = (writeIndex - readIndex) & _capacityMask;
            if (bufferedFrames <= 0)
                return;

            int framesToConsume = math.min(frameCount, bufferedFrames);
            int sampleCursor = 0;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                float sample = frameIndex < framesToConsume
                    ? _frames[(readIndex + frameIndex) & _capacityMask]
                    : 0f;

                for (int channelIndex = 0; channelIndex < channels; channelIndex++)
                {
                    float mixedSample = destination[sampleCursor] + sample;
                    destination[sampleCursor] = math.clamp(mixedSample, -1f, 1f);
                    sampleCursor++;
                }
            }

            WriteSharedIndex(
                NativeAudioKernelRingBufferDescriptor.ReadIndexSlot,
                (readIndex + framesToConsume) & _capacityMask);
        }

        public NativeAudioKernelRingBufferDescriptor CreateNativeDescriptor()
        {
            return TryCreateNativeDescriptor(out NativeAudioKernelRingBufferDescriptor descriptor, out _)
                ? descriptor
                : default;
        }

        public bool TryCreateNativeDescriptor(
            out NativeAudioKernelRingBufferDescriptor descriptor,
            out NativeAudioKernelBridgeStatus status)
        {
            descriptor = default;
            if (!IsCreated)
            {
                status = NativeAudioKernelBridgeStatus.SharedStateInvalid;
                return false;
            }

            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_sharedState);
            descriptor = new NativeAudioKernelRingBufferDescriptor
            {
                DescriptorMagic = NativeAudioKernelRingBufferDescriptor.DescriptorMagicValue,
                Frames = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(_frames),
                SharedState = (IntPtr)sharedStatePtr,
                ReadIndex = (IntPtr)(sharedStatePtr + NativeAudioKernelRingBufferDescriptor.ReadIndexSlot),
                WriteIndex = (IntPtr)(sharedStatePtr + NativeAudioKernelRingBufferDescriptor.WriteIndexSlot),
                CapacityFrames = _capacityFrames,
                CapacityMask = _capacityMask,
                SharedStateLengthInts = _sharedState.Length
            };

            return HectonSensoryKernelNativeBridge.IsDescriptorValid(in descriptor, out status);
        }

        public void Dispose()
        {
            if (_frames.IsCreated)
                _frames.Dispose();

            if (_sharedState.IsCreated)
                _sharedState.Dispose();

            _frames = default;
            _sharedState = default;
            _capacityFrames = 0;
            _capacityMask = 0;
        }

        private int ReadSharedIndex(int slot)
        {
            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_sharedState);
            return Volatile.Read(ref sharedStatePtr[slot]);
        }

        private void WriteSharedIndex(int slot, int value)
        {
            int* sharedStatePtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_sharedState);
            Volatile.Write(ref sharedStatePtr[slot], value);
        }

        private void WriteSharedMetadata()
        {
            WriteSharedIndex(NativeAudioKernelRingBufferDescriptor.CapacityFramesSlot, _capacityFrames);
            WriteSharedIndex(NativeAudioKernelRingBufferDescriptor.CapacityMaskSlot, _capacityMask);
            WriteSharedIndex(
                NativeAudioKernelRingBufferDescriptor.GuardValueSlotA,
                NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueA);
            WriteSharedIndex(
                NativeAudioKernelRingBufferDescriptor.GuardValueSlotB,
                NativeAudioKernelRingBufferDescriptor.SharedStateGuardValueB);
        }

        private static int NextPowerOfTwo(int value)
        {
            if (value <= 1)
                return 1;

            int power = 1;
            int growthWatchdog = 31;
            while (power < value && growthWatchdog-- > 0)
            {
                if (power > (int.MaxValue >> 1))
                    return int.MaxValue;

                power <<= 1;
            }

            return power;
        }
    }
}
