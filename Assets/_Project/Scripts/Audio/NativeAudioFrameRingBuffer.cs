using System;
using System.Threading;
using Hecton8.Core;
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
        private int _sourceChannels = 1;
        private int _overflowDropCount;
        private int _lastTelemetryOverflowDropCount;

        public bool IsCreated => _frames.IsCreated && _sharedState.IsCreated;
        public int CapacityFrames => _capacityFrames;
        public int SourceChannels => _sourceChannels;
        public int OverflowDropCount => Volatile.Read(ref _overflowDropCount);

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

        public void Initialize(int capacityFrames, int sourceChannels = 1)
        {
            int resolvedCapacity = math.max(256, NextPowerOfTwo(capacityFrames));
            int resolvedChannels = math.clamp(sourceChannels, 1, 2);
            if (IsCreated && _capacityFrames == resolvedCapacity && _sourceChannels == resolvedChannels)
            {
                Clear();
                return;
            }

            Dispose();
            _frames = new NativeArray<float>(resolvedCapacity * resolvedChannels, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacityFrames*channels] - lock-free procedural audio frame ring buffer storage - owner: AudioFrameSpscRingBuffer
            _sharedState = new NativeArray<int>(NativeAudioKernelRingBufferDescriptor.SharedStateSlotCount, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[6] - shared SPSC read/write state and native bridge guards - owner: AudioFrameSpscRingBuffer
            NativeMemorySentinel.RegisterNativeArray(_frames, nameof(AudioFrameSpscRingBuffer), nameof(_frames), NativeAllocationLifetime.Session);
            NativeMemorySentinel.RegisterNativeArray(_sharedState, nameof(AudioFrameSpscRingBuffer), nameof(_sharedState), NativeAllocationLifetime.Session);
            _capacityFrames = resolvedCapacity;
            _capacityMask = resolvedCapacity - 1;
            _sourceChannels = resolvedChannels;
            Volatile.Write(ref _overflowDropCount, 0);
            Volatile.Write(ref _lastTelemetryOverflowDropCount, 0);
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
            return TryWriteInterleaved(source, frameCount, 1);
        }

        public bool TryWriteInterleaved(NativeArray<float> source, int frameCount, int sourceChannels)
        {
            if (!IsCreated || !source.IsCreated || frameCount <= 0)
                return false;

            int safeChannels = math.clamp(sourceChannels, 1, 2);
            if (safeChannels != _sourceChannels)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length / safeChannels);
            if (safeFrameCount <= 0)
                return false;

            int readIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.ReadIndexSlot);
            int writeIndex = ReadSharedIndex(NativeAudioKernelRingBufferDescriptor.WriteIndexSlot);
            int availableFrames = (writeIndex - readIndex) & _capacityMask;
            int freeFrames = _capacityFrames - availableFrames - 1;
            if (safeFrameCount > freeFrames)
            {
                int overflowDropCount = Interlocked.Increment(ref _overflowDropCount);
                bool shouldReport = overflowDropCount == 1 ||
                                    (overflowDropCount & (overflowDropCount - 1)) == 0;
                if (shouldReport &&
                    overflowDropCount != Volatile.Read(ref _lastTelemetryOverflowDropCount) &&
                    Interlocked.Exchange(ref _lastTelemetryOverflowDropCount, overflowDropCount) != overflowDropCount)
                {
                    CrashTelemetryBuffer.ReportAudioOverflowDropWarning(
                        overflowDropCount,
                        availableFrames,
                        freeFrames);
                }

                return false;
            }

            for (int i = 0; i < safeFrameCount; i++)
            {
                int frameWriteIndex = ((writeIndex + i) & _capacityMask) * _sourceChannels;
                int frameSourceIndex = i * safeChannels;
                for (int channelIndex = 0; channelIndex < safeChannels; channelIndex++)
                    _frames[frameWriteIndex + channelIndex] = source[frameSourceIndex + channelIndex];
            }

            WriteSharedIndex(
                NativeAudioKernelRingBufferDescriptor.WriteIndexSlot,
                (writeIndex + safeFrameCount) & _capacityMask);
            return true;
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
            {
                NativeMemorySentinel.UnregisterNativeArray(_frames);
                _frames.Dispose();
            }

            if (_sharedState.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_sharedState);
                _sharedState.Dispose();
            }

            _frames = default;
            _sharedState = default;
            _capacityFrames = 0;
            _capacityMask = 0;
            _sourceChannels = 1;
            Volatile.Write(ref _overflowDropCount, 0);
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
