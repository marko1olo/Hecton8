using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;

namespace Hecton8.Physics.Determinism
{
    /// <summary>
    /// Primitive-only deterministic math helpers. No Unity assembly dependency; usable from isolated determinism packages.
    /// </summary>
    public static class DeterministicPhysicsMath
    {
        public const uint FnvOffsetBasis = 2166136261u;
        public const uint FnvPrime = 16777619u;
        private const float MillimeterScale = HectonPhysicsContract.DeterministicMillimeterScale;
        private const float InvMillimeterScale = HectonPhysicsContract.DeterministicInvMillimeterScale;
        private const float MaxQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMaxQuantizedMillimeterFloat;
        private const float MinQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMinQuantizedMillimeterFloat;
        private const int MaxQuantizedMillimeter = HectonPhysicsContract.DeterministicMaxQuantizedMillimeter;
        private const int MinQuantizedMillimeter = HectonPhysicsContract.DeterministicMinQuantizedMillimeter;
        private const float Pi = HectonPhysicsContract.DeterministicPi;
        private const float TwoPi = HectonPhysicsContract.DeterministicTwoPi;
        private const float InvTwoPi = HectonPhysicsContract.DeterministicInvTwoPi;
        private const float MaxWrapInput = HectonPhysicsContract.DeterministicMaxWrapInput;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SnapMillimeter(float value)
        {
            if (!(value <= float.MaxValue && value >= -float.MaxValue))
                return 0f;

            return QuantizeMillimeter(value) * InvMillimeterScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int QuantizeMillimeter(float value)
        {
            if (!(value <= float.MaxValue && value >= -float.MaxValue))
                return 0;

            float scaled = value * MillimeterScale;
            if (scaled >= MaxQuantizedMillimeterFloat)
                return MaxQuantizedMillimeter;
            if (scaled <= MinQuantizedMillimeterFloat)
                return MinQuantizedMillimeter;

            return scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, uint value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, int value)
        {
            return Fnv1a(hash, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            hash = Fnv1a(hash, (uint)bits);
            return Fnv1a(hash, (uint)(bits >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1aQuantizedMillimeter(uint hash, float value)
        {
            return Fnv1a(hash, QuantizeMillimeter(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinApprox(float radians)
        {
            float x = WrapSignedPi(radians);
            float sign = 1f;
            if (x < 0f)
            {
                x = -x;
                sign = -1f;
            }

            if (x > Pi)
            {
                x = TwoPi - x;
                sign = -sign;
            }

            float y = x * (Pi - x);
            float denominator = (5f * Pi * Pi) - (4f * y);
            return sign * ((16f * y) / (denominator > 0.000001f ? denominator : 0.000001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapSignedPi(float radians)
        {
            if (!(radians <= MaxWrapInput && radians >= -MaxWrapInput))
                return 0f;

            return WrapPi(radians);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPi(float radians)
        {
            int turns = (int)(radians * InvTwoPi);
            float x = radians - (turns * TwoPi);
            if (x > Pi)
                x -= TwoPi;
            else if (x < -Pi)
                x += TwoPi;

            return x;
        }
    }
}
