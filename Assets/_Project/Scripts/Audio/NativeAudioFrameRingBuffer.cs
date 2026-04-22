using System;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    internal sealed class NativeAudioFrameRingBuffer : IDisposable
    {
        private NativeArray<float> _frames;
        private int _capacityMask;
        private long _readFrameCursor;
        private long _writeFrameCursor;

        public bool IsCreated => _frames.IsCreated;
        public int CapacityFrames => _frames.IsCreated ? _frames.Length : 0;
        public long ReadFrameCursor => Interlocked.Read(ref _readFrameCursor);
        public long WriteFrameCursor => Interlocked.Read(ref _writeFrameCursor);

        public int BufferedFrames
        {
            get
            {
                long available = WriteFrameCursor - ReadFrameCursor;
                if (available <= 0L)
                    return 0;

                int capacityFrames = CapacityFrames;
                if (available >= capacityFrames)
                    return capacityFrames;

                return (int)available;
            }
        }

        public int WritableFrames => math.max(0, CapacityFrames - BufferedFrames);

        public void Initialize(int capacityFrames)
        {
            int resolvedCapacity = math.max(256, NextPowerOfTwo(capacityFrames));
            if (_frames.IsCreated && _frames.Length == resolvedCapacity)
            {
                Clear();
                return;
            }

            Dispose();
            _frames = new NativeArray<float>(resolvedCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[capacityFrames] - lock-free procedural audio frame ring buffer - owner: NativeAudioFrameRingBuffer
            _capacityMask = resolvedCapacity - 1;
            _readFrameCursor = 0L;
            _writeFrameCursor = 0L;
        }

        public void Clear()
        {
            Interlocked.Exchange(ref _readFrameCursor, 0L);
            Interlocked.Exchange(ref _writeFrameCursor, 0L);
        }

        public bool TryWrite(NativeArray<float> source, int frameCount)
        {
            if (!_frames.IsCreated || !source.IsCreated || frameCount <= 0)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length);
            if (safeFrameCount <= 0 || safeFrameCount > WritableFrames)
                return false;

            long writeCursor = WriteFrameCursor;
            for (int i = 0; i < safeFrameCount; i++)
                _frames[(int)((writeCursor + i) & _capacityMask)] = source[i];

            Thread.MemoryBarrier();
            Volatile.Write(ref _writeFrameCursor, writeCursor + safeFrameCount);
            return true;
        }

        public int AddToInterleaved(float[] destination, int channels, int frameCount)
        {
            if (!_frames.IsCreated || destination == null || channels <= 0 || frameCount <= 0)
                return 0;

            long readCursor = ReadFrameCursor;
            long writeCursor = WriteFrameCursor;
            long availableFrames = writeCursor - readCursor;
            if (availableFrames <= 0L)
                return 0;

            int readableFrames = (int)math.min(availableFrames, frameCount);
            if (readableFrames <= 0)
                return 0;

            Thread.MemoryBarrier();
            for (int frameIndex = 0; frameIndex < readableFrames; frameIndex++)
            {
                float sample = _frames[(int)((readCursor + frameIndex) & _capacityMask)];
                int channelOffset = frameIndex * channels;
                for (int channelIndex = 0; channelIndex < channels; channelIndex++)
                    destination[channelOffset + channelIndex] += sample;
            }

            Thread.MemoryBarrier();
            Volatile.Write(ref _readFrameCursor, readCursor + readableFrames);
            return readableFrames;
        }

        public void Dispose()
        {
            if (_frames.IsCreated)
                _frames.Dispose();

            _capacityMask = 0;
            _readFrameCursor = 0L;
            _writeFrameCursor = 0L;
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
