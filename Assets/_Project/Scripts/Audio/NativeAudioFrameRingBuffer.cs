using System;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    internal sealed class AudioFrameSpscRingBuffer : IDisposable
    {
        private NativeArray<float> _frames;
        private int _capacityFrames;
        private int _capacityMask;
        private int _readFrameIndex;
        private int _writeFrameIndex;

        public bool IsCreated => _frames.IsCreated;
        public int CapacityFrames => _capacityFrames;

        public int BufferedFrames
        {
            get
            {
                if (!_frames.IsCreated)
                    return 0;

                int writeIndex = Volatile.Read(ref _writeFrameIndex);
                int readIndex = Volatile.Read(ref _readFrameIndex);
                return (writeIndex - readIndex) & _capacityMask;
            }
        }

        public int WritableFrames => !_frames.IsCreated
            ? 0
            : math.max(0, _capacityFrames - BufferedFrames - 1);

        public void GetState(out int bufferedFrames, out int writableFrames)
        {
            if (!_frames.IsCreated)
            {
                bufferedFrames = 0;
                writableFrames = 0;
                return;
            }

            int writeIndex = Volatile.Read(ref _writeFrameIndex);
            int readIndex = Volatile.Read(ref _readFrameIndex);
            bufferedFrames = (writeIndex - readIndex) & _capacityMask;
            writableFrames = _capacityFrames - bufferedFrames - 1;
        }

        public void Initialize(int capacityFrames)
        {
            int resolvedCapacity = math.max(256, NextPowerOfTwo(capacityFrames));
            if (_frames.IsCreated && _frames.Length == resolvedCapacity)
            {
                Clear();
                return;
            }

            Dispose();
            _frames = new NativeArray<float>(resolvedCapacity, Allocator.AudioKernel, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacityFrames] - lock-free procedural audio frame ring buffer - owner: AudioFrameSpscRingBuffer
            _capacityFrames = resolvedCapacity;
            _capacityMask = resolvedCapacity - 1;
            _readFrameIndex = 0;
            _writeFrameIndex = 0;
        }

        public void Clear()
        {
            Volatile.Write(ref _readFrameIndex, 0);
            Volatile.Write(ref _writeFrameIndex, 0);
        }

        public bool TryWrite(NativeArray<float> source, int frameCount)
        {
            if (!_frames.IsCreated || !source.IsCreated || frameCount <= 0)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length);
            if (safeFrameCount <= 0)
                return false;

            int readIndex = Volatile.Read(ref _readFrameIndex);
            int writeIndex = _writeFrameIndex;
            int availableFrames = (writeIndex - readIndex) & _capacityMask;
            int freeFrames = _capacityFrames - availableFrames - 1;
            if (safeFrameCount > freeFrames)
                return false;

            for (int i = 0; i < safeFrameCount; i++)
                _frames[(writeIndex + i) & _capacityMask] = source[i];

            Volatile.Write(ref _writeFrameIndex, (writeIndex + safeFrameCount) & _capacityMask);
            return true;
        }

        public int AddToInterleaved(float[] destination, int channels, int frameCount)
        {
            if (!_frames.IsCreated || destination == null || channels <= 0 || frameCount <= 0)
                return 0;

            int readIndex = _readFrameIndex;
            int writeIndex = Volatile.Read(ref _writeFrameIndex);
            int availableFrames = (writeIndex - readIndex) & _capacityMask;
            if (availableFrames <= 0)
                return 0;

            int readableFrames = math.min(availableFrames, frameCount);
            if (readableFrames <= 0)
                return 0;

            for (int frameIndex = 0; frameIndex < readableFrames; frameIndex++)
            {
                float sample = _frames[(readIndex + frameIndex) & _capacityMask];
                int channelOffset = frameIndex * channels;
                for (int channelIndex = 0; channelIndex < channels; channelIndex++)
                    destination[channelOffset + channelIndex] += sample;
            }

            Volatile.Write(ref _readFrameIndex, (readIndex + readableFrames) & _capacityMask);
            return readableFrames;
        }

        public void Dispose()
        {
            if (_frames.IsCreated)
                _frames.Dispose();

            _frames = default;
            _capacityFrames = 0;
            _capacityMask = 0;
            _readFrameIndex = 0;
            _writeFrameIndex = 0;
        }

        private static int NextPowerOfTwo(int value)
        {
            int power = 1;
            while (power < value)
                power <<= 1;
            return power;
        }
    }
}
