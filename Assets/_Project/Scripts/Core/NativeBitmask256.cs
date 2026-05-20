using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct NativeBitmask256
    {
        [FieldOffset(0)]
        public ulong Word0;

        [FieldOffset(8)]
        public ulong Word1;

        [FieldOffset(16)]
        public ulong Word2;

        [FieldOffset(24)]
        public ulong Word3;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty()
        {
            return (Word0 | Word1 | Word2 | Word3) == 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(in NativeBitmask256 mask)
        {
            return (mask.Word0 | mask.Word1 | mask.Word2 | mask.Word3) == 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetBit(int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ulong bit = 1UL << (bitIndex & 63);
            switch (bitIndex >> 6)
            {
                case 0:
                    Word0 |= bit;
                    break;
                case 1:
                    Word1 |= bit;
                    break;
                case 2:
                    Word2 |= bit;
                    break;
                default:
                    Word3 |= bit;
                    break;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ClearBit(int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ulong mask = ~(1UL << (bitIndex & 63));
            switch (bitIndex >> 6)
            {
                case 0:
                    Word0 &= mask;
                    break;
                case 1:
                    Word1 &= mask;
                    break;
                case 2:
                    Word2 &= mask;
                    break;
                default:
                    Word3 &= mask;
                    break;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasBit(int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ulong word = GetWordValue(bitIndex >> 6);
            return (word & (1UL << (bitIndex & 63))) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Word0 = 0UL;
            Word1 = 0UL;
            Word2 = 0UL;
            Word3 = 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Or(in NativeBitmask256 other)
        {
            Word0 |= other.Word0;
            Word1 |= other.Word1;
            Word2 |= other.Word2;
            Word3 |= other.Word3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsAll(in NativeBitmask256 required)
        {
            return (Word0 & required.Word0) == required.Word0 &&
                   (Word1 & required.Word1) == required.Word1 &&
                   (Word2 & required.Word2) == required.Word2 &&
                   (Word3 & required.Word3) == required.Word3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountLeadingZeros(ref NativeBitmask256 mask)
        {
            if (mask.Word3 != 0UL)
                return math.lzcnt(mask.Word3);

            if (mask.Word2 != 0UL)
                return 64 + math.lzcnt(mask.Word2);

            if (mask.Word1 != 0UL)
                return 128 + math.lzcnt(mask.Word1);

            return mask.Word0 != 0UL ? 192 + math.lzcnt(mask.Word0) : 256;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FindFirstEmptySlot(ref NativeBitmask256 mask)
        {
            ulong inverse0 = ~mask.Word0;
            if (inverse0 != 0UL)
                return math.tzcnt(inverse0);

            ulong inverse1 = ~mask.Word1;
            if (inverse1 != 0UL)
                return 64 + math.tzcnt(inverse1);

            ulong inverse2 = ~mask.Word2;
            if (inverse2 != 0UL)
                return 128 + math.tzcnt(inverse2);

            ulong inverse3 = ~mask.Word3;
            return inverse3 != 0UL ? 192 + math.tzcnt(inverse3) : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasBit(ref NativeBitmask256 mask, int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ulong word;
            switch (bitIndex >> 6)
            {
                case 0:
                    word = mask.Word0;
                    break;
                case 1:
                    word = mask.Word1;
                    break;
                case 2:
                    word = mask.Word2;
                    break;
                default:
                    word = mask.Word3;
                    break;
            }

            return (word & (1UL << (bitIndex & 63))) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Or(ref NativeBitmask256 mask, in NativeBitmask256 other)
        {
            mask.Word0 |= other.Word0;
            mask.Word1 |= other.Word1;
            mask.Word2 |= other.Word2;
            mask.Word3 |= other.Word3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetWordValue(int wordIndex)
        {
            switch (wordIndex)
            {
                case 0:
                    return Word0;
                case 1:
                    return Word1;
                case 2:
                    return Word2;
                default:
                    return Word3;
            }
        }
    }
}
