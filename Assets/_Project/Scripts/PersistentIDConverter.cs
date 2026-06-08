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
            return string.IsNullOrWhiteSpace(persistentId)
                ? 0u
                : ToPersistentId32(persistentId.AsSpan());
        }

        public static uint ToPersistentId32(ReadOnlySpan<char> persistentId)
        {
            persistentId = TrimWhiteSpace(persistentId);
            if (persistentId.Length == 0)
                return 0u;

            return LocHash.ComputeAsciiLowerInvariant(persistentId);
        }

        private static ReadOnlySpan<char> TrimWhiteSpace(ReadOnlySpan<char> value)
        {
            int start = 0;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
                start++;

            int end = value.Length - 1;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

    }
}
