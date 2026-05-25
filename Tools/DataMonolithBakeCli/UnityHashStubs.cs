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
