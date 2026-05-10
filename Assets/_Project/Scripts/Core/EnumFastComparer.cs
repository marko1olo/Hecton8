using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Core
{
    /// <summary>
    /// Bitwise enum comparer for managed dictionaries where enum boxing is unacceptable.
    /// </summary>
    public sealed class EnumFastComparer<T> : IEqualityComparer<T> where T : unmanaged, Enum
    {
        public static readonly EnumFastComparer<T> Instance = new EnumFastComparer<T>();

        private EnumFastComparer()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T x, T y)
        {
            return ToUInt64(ref x) == ToUInt64(ref y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(T obj)
        {
            ulong bits = ToUInt64(ref obj);
            return (int)(bits ^ (bits >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ToUInt64(ref T value)
        {
            return EnumFast.ToUInt64(ref value);
        }
    }

    /// <summary>
    /// Static enum bit helpers for hot paths that must avoid comparer/interface dispatch.
    /// </summary>
    public static class EnumFast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Equals<T>(T x, T y) where T : unmanaged, Enum
        {
            return ToUInt64(ref x) == ToUInt64(ref y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode<T>(T value) where T : unmanaged, Enum
        {
            ulong bits = ToUInt64(ref value);
            return (int)(bits ^ (bits >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong ToUInt64<T>(ref T value) where T : unmanaged, Enum
        {
            int size = UnsafeUtility.SizeOf<T>();
            switch (size)
            {
                case 1:
                    return UnsafeUtility.As<T, byte>(ref value);
                case 2:
                    return UnsafeUtility.As<T, ushort>(ref value);
                case 4:
                    return unchecked((uint)UnsafeUtility.As<T, int>(ref value));
                default:
                    return UnsafeUtility.As<T, ulong>(ref value);
            }
        }
    }
}
