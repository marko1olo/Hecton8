using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Hecton8.Core
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeBitmask256
    {
        public ulong Word0;
        public ulong Word1;
        public ulong Word2;
        public ulong Word3;

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Word0 | Word1 | Word2 | Word3) == 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetBit(int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ref ulong word = ref GetWord(bitIndex >> 6);
            word |= 1UL << (bitIndex & 63);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ClearBit(int bitIndex)
        {
            if ((uint)bitIndex >= 256u)
                return false;

            ref ulong word = ref GetWord(bitIndex >> 6);
            word &= ~(1UL << (bitIndex & 63));
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
        private ref ulong GetWord(int wordIndex)
        {
            switch (wordIndex)
            {
                case 0:
                    return ref Word0;
                case 1:
                    return ref Word1;
                case 2:
                    return ref Word2;
                default:
                    return ref Word3;
            }
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
