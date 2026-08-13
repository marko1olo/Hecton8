using System;

namespace Unity.Burst
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class BurstCompileAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class GenerateTestsForBurstCompatibilityAttribute : Attribute
    {
        public Type[] GenericTypeArguments { get; set; }
    }
}

namespace Unity.Burst.Intrinsics
{
    public static class X86
    {
        public static class Avx2
        {
            public static bool IsAvx2Supported => false;
        }
    }
}

namespace Unity.Mathematics
{
    public static class math
    {
        public static uint min(uint x, uint y)
        {
            return x < y ? x : y;
        }
    }

    public struct uint2
    {
        public uint x;
        public uint y;

        public uint2(uint x, uint y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public struct uint4
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;

        public uint4(uint x, uint y, uint z, uint w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
    }

    public static class Common
    {
        public static ulong umul128(ulong lhs, ulong rhs, out ulong high)
        {
            return Math.BigMul(lhs, rhs, out high);
        }
    }
}

namespace Unity.Collections
{
    public static unsafe partial class xxHash3
    {
        public struct StreamingState
        {
            private byte[] _buffer;
            private int _length;

            public StreamingState(bool isHash64)
            {
                _buffer = new byte[64 * 1024];
                _length = 0;
            }

            public void Update(byte* input, int length)
            {
                if (length <= 0)
                    return;

                EnsureCapacity(_length + length);
                fixed (byte* destination = &_buffer[_length])
                    Buffer.MemoryCopy(input, destination, length, length);
                _length += length;
            }

            public Unity.Mathematics.uint2 DigestHash64()
            {
                if (_length == 0)
                    return Hash64(null, 0);

                fixed (byte* ptr = _buffer)
                    return Hash64(ptr, _length);
            }

            private void EnsureCapacity(int required)
            {
                if (_buffer != null && _buffer.Length >= required)
                    return;

                int capacity = _buffer != null && _buffer.Length > 0 ? _buffer.Length : 64 * 1024;
                while (capacity < required)
                    capacity <<= 1;

                byte[] grown = new byte[capacity];
                if (_buffer != null && _length > 0)
                    Array.Copy(_buffer, 0, grown, 0, _length);
                _buffer = grown;
            }
        }

        private static void Avx2HashLongInternalLoop(
            ulong* acc,
            byte* input,
            byte* dest,
            long length,
            byte* secret,
            int isHash64)
        {
            DefaultHashLongInternalLoop(acc, input, dest, length, secret, isHash64);
        }
    }
}
