using System;
using Hecton.Localization;
using TMPro;
using Unity.Mathematics;

namespace Hecton8.UI
{
    /// <summary>
    /// TMP_Text sink for caller-owned spans. Uses the project CharBufferPool and never materializes a managed string.
    /// </summary>
    public static class TmpTextNoAlloc
    {
        private static readonly char[] Empty = Array.Empty<char>();

        public static void Set(TMP_Text target, string value)
        {
            Set(target, string.IsNullOrEmpty(value) ? ReadOnlySpan<char>.Empty : value.AsSpan());
        }

        public static void Set(TMP_Text target, ReadOnlySpan<char> value)
        {
            if (target == null)
                return;

            if (value.Length <= 0)
            {
                target.SetCharArray(Empty, 0, 0);
                return;
            }

            if (value.Length <= CharBufferPool.SlotCapacity &&
                CharBufferPool.TryAcquire(out CharBufferPool.Lease lease))
            {
                int length = Copy(value, lease.Buffer);
                target.SetCharArray(lease.Buffer, 0, length);
                CharBufferPool.Release(in lease);
                return;
            }

            if (value.Length <= CharBufferPool.RequiredBabelTextCapacity &&
                CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease babelLease))
            {
                int length = Copy(value, babelLease.TmpBuffer);
                target.SetCharArray(babelLease.TmpBuffer, 0, length);
                CharBufferPool.Release(in babelLease);
                return;
            }

            if (CharBufferPool.TryAcquireEncyclopedia(out CharBufferPool.EncyclopediaLease pageLease))
            {
                int length = Copy(value, pageLease.Buffer);
                target.SetCharArray(pageLease.Buffer, 0, length);
                CharBufferPool.Release(in pageLease);
                return;
            }

            target.SetCharArray(Empty, 0, 0);
        }

        public static bool SetLocalized(TMP_Text target, uint textHash, bool stripRichText = false)
        {
            BabelFormatArgs args = BabelFormatArgs.None();
            return SetLocalized(target, textHash, in args, stripRichText);
        }

        public static bool SetLocalized(TMP_Text target, uint textHash, in BabelFormatArgs formatArgs, bool stripRichText = false)
        {
            if (target == null)
                return false;

            if (!CharBufferPool.TryAcquireBabel(out CharBufferPool.BabelLease lease))
            {
                target.SetCharArray(Empty, 0, 0);
                return false;
            }

            try
            {
                bool found = LocRegistry.TryWriteVisualSpanFromUtf8(
                    textHash,
                    lease.Span,
                    out int length,
                    formatArgs,
                    stripRichText);
                int safeLength = lease.CopyToTmpBuffer(length);
                target.SetCharArray(lease.TmpBuffer, 0, safeLength);
                return found;
            }
            finally
            {
                CharBufferPool.Release(in lease);
            }
        }

        private static int Copy(ReadOnlySpan<char> source, char[] destination)
        {
            if (destination == null || destination.Length == 0 || source.Length <= 0)
                return 0;

            int length = math.min(source.Length, destination.Length);
            source.Slice(0, length).CopyTo(destination.AsSpan(0, length));
            return length;
        }
    }
}
