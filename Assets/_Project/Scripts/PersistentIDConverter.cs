using System;
using Hecton.Localization;

namespace Hecton8.SaveSystem
{
    public static class PersistentIDConverter
    {
        public const uint Fnv1aOffsetBasis32 = LocHash.FnvOffsetBasis;
        public const uint Fnv1aPrime32 = LocHash.FnvPrime;

        public static uint ToPersistentId32(string persistentId)
        {
            return string.IsNullOrEmpty(persistentId)
                ? 0u
                : unchecked((uint)LocHash.ComputeAsciiLowerInvariant(persistentId));
        }

        public static uint ToPersistentId32(ReadOnlySpan<char> persistentId)
        {
            return LocHash.ComputeAsciiLowerInvariant(persistentId);
        }
    }
}
