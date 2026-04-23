using System;
using System.Threading;
using Unity.Mathematics;

namespace Hecton8.Audio
{
    internal sealed class AudioFrameSpscRingBuffer : IDisposable
    {
        private float[] _frames;
        private int _capacityFrames;
        private int _capacityMask;
        private long _readFrameCursor;
        private long _writeFrameCursor;

        public bool IsCreated => _frames != null;
        public int CapacityFrames => _capacityFrames;
        public long ReadFrameCursor => Interlocked.Read(ref _readFrameCursor);
        public long WriteFrameCursor => Volatile.Read(ref _writeFrameCursor);

        public int BufferedFrames
        {
            get
            {
                if (_frames == null)
                    return 0;

                long available = Volatile.Read(ref _writeFrameCursor) - Volatile.Read(ref _readFrameCursor);
                if (available <= 0L)
                    return 0;

                if (available >= _capacityFrames)
                    return _capacityFrames - 1;

                return (int)available;
            }
        }

        public int WritableFrames => math.max(0, _capacityFrames - BufferedFrames - 1);

        public void Initialize(int capacityFrames)
        {
            int resolvedCapacity = math.max(256, NextPowerOfTwo(capacityFrames));
            if (_frames != null && _frames.Length == resolvedCapacity)
            {
                Clear();
                return;
            }

            Dispose();
            _frames = new float[resolvedCapacity]; // COLD ALLOC: float[capacityFrames] - lock-free procedural audio frame ring buffer - owner: AudioFrameSpscRingBuffer
            _capacityFrames = resolvedCapacity;
            _capacityMask = resolvedCapacity - 1;
            _readFrameCursor = 0L;
            _writeFrameCursor = 0L;
        }

        public void Clear()
        {
            Interlocked.Exchange(ref _readFrameCursor, 0L);
            Interlocked.Exchange(ref _writeFrameCursor, 0L);
        }

        public bool TryWrite(float[] source, int frameCount)
        {
            if (_frames == null || source == null || frameCount <= 0)
                return false;

            int safeFrameCount = math.min(frameCount, source.Length);
            if (safeFrameCount <= 0)
                return false;

            long readCursor = Volatile.Read(ref _readFrameCursor);
            long writeCursor = _writeFrameCursor;
            long available = writeCursor - readCursor;
            int freeFrames = math.max(0, _capacityFrames - (int)math.min(available, _capacityFrames) - 1);
            if (safeFrameCount > freeFrames)
                return false;

            for (int i = 0; i < safeFrameCount; i++)
                _frames[(int)((writeCursor + i) & _capacityMask)] = source[i];

            Interlocked.MemoryBarrier();
            Volatile.Write(ref _writeFrameCursor, writeCursor + safeFrameCount);
            return true;
        }

        public int AddToInterleaved(float[] destination, int channels, int frameCount)
        {
            if (_frames == null || destination == null || channels <= 0 || frameCount <= 0)
                return 0;

            long readCursor = _readFrameCursor;
            long writeCursor = Volatile.Read(ref _writeFrameCursor);
            long availableFrames = writeCursor - readCursor;
            if (availableFrames <= 0L)
                return 0;

            int readableFrames = (int)math.min(availableFrames, frameCount);
            if (readableFrames <= 0)
                return 0;

            for (int frameIndex = 0; frameIndex < readableFrames; frameIndex++)
            {
                float sample = _frames[(int)((readCursor + frameIndex) & _capacityMask)];
                int channelOffset = frameIndex * channels;
                for (int channelIndex = 0; channelIndex < channels; channelIndex++)
                    destination[channelOffset + channelIndex] += sample;
            }

            Interlocked.MemoryBarrier();
            Volatile.Write(ref _readFrameCursor, readCursor + readableFrames);
            return readableFrames;
        }

        public void Dispose()
        {
            _frames = null;
            _capacityFrames = 0;
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
