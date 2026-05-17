using System;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class HectonContractValidator
    {
        public static void RequireFinite(float value, string name)
        {
            if (!math.isfinite(value))
                throw new InvalidOperationException(name + " is non-finite.");
        }

        public static void RequireFinite(double value, string name)
        {
            if (!math.isfinite(value))
                throw new InvalidOperationException(name + " is non-finite.");
        }

        public static void RequirePositive(float value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0f)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0.0d)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePositive(int value, string name)
        {
            if (value <= 0)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePowerOfTwo(int value, string name)
        {
            RequirePositive(value, name);
            if ((value & (value - 1)) != 0)
                throw new InvalidOperationException(name + " must be a power of two.");
        }

        public static void RequireLessOrEqual(int value, int maximum, string name)
        {
            if (value > maximum)
                throw new InvalidOperationException(name + " exceeds maximum.");
        }

        public static void RequireGreaterOrEqual(int value, int minimum, string name)
        {
            if (value < minimum)
                throw new InvalidOperationException(name + " is below minimum.");
        }

        public static void RequireUnit(float value, string name)
        {
            RequireFinite(value, name);
            if (value < 0f || value > 1f)
                throw new InvalidOperationException(name + " must be within 0..1.");
        }
    }

}
