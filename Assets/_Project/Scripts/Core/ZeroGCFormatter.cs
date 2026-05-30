using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Hecton.Localization;

namespace Hecton8.Core
{
    /// <summary>
    /// Caller-owned span formatter for TMP SetCharArray lanes.
    /// </summary>
    public static class ZeroGCFormatter
    {
        public const int HudMetricBufferCapacity = 64;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt(int value, Span<char> destination, out int charsWritten)
        {
            return value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWriteInt(int value, Span<char> destination, out int charsWritten)
        {
            return TryFormatInt(value, destination, out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIntToChars(int value, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            if (!value.TryFormat(destination.Slice(cursor), out int written, default, CultureInfo.InvariantCulture))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastIntToChars(
            int value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            Span<char> remaining = destination.Slice(cursor);
            bool wrote = format.Length == 0
                ? value.TryFormat(remaining, out int written, default, CultureInfo.InvariantCulture)
                : value.TryFormat(remaining, out written, format, CultureInfo.InvariantCulture);
            if (!wrote)
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt(
            int value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            out int charsWritten)
        {
            return format.Length == 0
                ? value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture)
                : value.TryFormat(destination, out charsWritten, format, CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatFloat(float value, Span<char> destination, out int charsWritten)
        {
            if (!TryWriteFiniteFloatFallback(value, destination, out charsWritten))
                return false;

            if (charsWritten != 0)
                return true;

            return value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastFloatToChars(float value, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            if (!TryFormatFloat(value, destination.Slice(cursor), out int written))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FastFloatToChars(float value, int precision, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            if (!TryFormatFloat(value, destination.Slice(cursor), precision, out int written))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatFloat(float value, Span<char> destination, int precision, out int charsWritten)
        {
            charsWritten = 0;
            if (destination.Length == 0)
                return false;

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                destination[0] = '0';
                charsWritten = 1;
                return true;
            }

            int safePrecision = precision < 0 ? 0 : precision > 6 ? 6 : precision;
            switch (safePrecision)
            {
                case 0:
                    return value.TryFormat(destination, out charsWritten, "F0".AsSpan(), CultureInfo.InvariantCulture);
                case 1:
                    return value.TryFormat(destination, out charsWritten, "F1".AsSpan(), CultureInfo.InvariantCulture);
                case 2:
                    return value.TryFormat(destination, out charsWritten, "F2".AsSpan(), CultureInfo.InvariantCulture);
                case 3:
                    return value.TryFormat(destination, out charsWritten, "F3".AsSpan(), CultureInfo.InvariantCulture);
                case 4:
                    return value.TryFormat(destination, out charsWritten, "F4".AsSpan(), CultureInfo.InvariantCulture);
                case 5:
                    return value.TryFormat(destination, out charsWritten, "F5".AsSpan(), CultureInfo.InvariantCulture);
                default:
                    return value.TryFormat(destination, out charsWritten, "F6".AsSpan(), CultureInfo.InvariantCulture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWriteFloat(
            float value,
            ReadOnlySpan<char> format,
            Span<char> destination,
            out int charsWritten)
        {
            return TryFormatFloat(value, destination, format, out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatFloat(
            float value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            out int charsWritten)
        {
            if (!TryWriteFiniteFloatFallback(value, destination, out charsWritten))
                return false;

            if (charsWritten != 0)
                return true;

            return format.Length == 0
                ? value.TryFormat(destination, out charsWritten, default, CultureInfo.InvariantCulture)
                : value.TryFormat(destination, out charsWritten, format, CultureInfo.InvariantCulture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryWriteFiniteFloatFallback(float value, Span<char> destination, out int charsWritten)
        {
            charsWritten = 0;
            if (!float.IsNaN(value) && !float.IsInfinity(value))
                return true;

            if (destination.Length == 0)
                return false;

            destination[0] = '0';
            charsWritten = 1;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendToSpan(ReadOnlySpan<char> source, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || source.Length > destination.Length - cursor)
                return false;

            source.CopyTo(destination.Slice(cursor));
            cursor += source.Length;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendChar(char value, Span<char> destination, ref int cursor)
        {
            if ((uint)cursor >= (uint)destination.Length)
                return false;

            destination[cursor] = value;
            cursor++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendToSpanTruncated(
            ReadOnlySpan<char> source,
            Span<char> destination,
            ref int cursor,
            out bool truncated)
        {
            truncated = false;
            if (cursor < 0)
            {
                cursor = 0;
                truncated = true;
                return false;
            }

            if (cursor > destination.Length)
            {
                cursor = destination.Length;
                truncated = true;
                return false;
            }

            int remaining = destination.Length - cursor;
            if (source.Length <= remaining)
            {
                source.CopyTo(destination.Slice(cursor));
                cursor += source.Length;
                return true;
            }

            if (remaining > 0)
                source.Slice(0, remaining).CopyTo(destination.Slice(cursor));

            cursor = destination.Length;
            truncated = true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendAsciiEllipsis(Span<char> destination, ref int length)
        {
            if (destination.Length <= 0)
            {
                length = 0;
                return;
            }

            int safeLength = length;
            if (safeLength < 0)
                safeLength = 0;
            if (safeLength > destination.Length)
                safeLength = destination.Length;

            if (destination.Length < 3)
            {
                length = safeLength;
                return;
            }

            safeLength = Math.Min(safeLength, destination.Length - 3);
            if (safeLength > 0 && char.IsHighSurrogate(destination[safeLength - 1]))
                safeLength--;

            destination[safeLength++] = '.';
            destination[safeLength++] = '.';
            destination[safeLength++] = '.';
            length = safeLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendInt(int value, Span<char> destination, ref int cursor)
        {
            return FastIntToChars(value, destination, ref cursor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendInt(
            int value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            ref int cursor)
        {
            return FastIntToChars(value, destination, format, ref cursor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendFloat(float value, Span<char> destination, ref int cursor)
        {
            return FastFloatToChars(value, destination, ref cursor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendFloat(
            float value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            Span<char> remaining = destination.Slice(cursor);
            if (!TryFormatFloat(value, remaining, format, out int written))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWriteMetricTemplateInt(
            ReadOnlySpan<char> template,
            int value,
            Span<char> destination,
            out int charsWritten)
        {
            return LocNumericBuffer.TryWrite(template, destination, LocNumericArg.Int(value), out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWriteMetricTemplateFloatTenths(
            ReadOnlySpan<char> template,
            int roundedTenths,
            Span<char> destination,
            out int charsWritten)
        {
            return LocNumericBuffer.TryWrite(template, destination, LocNumericArg.Float(roundedTenths * 0.1f), out charsWritten);
        }

        public static bool TryWriteCompassHeading(
            int headingDegrees,
            ReadOnlySpan<char> cardinal,
            Span<char> destination,
            out int charsWritten)
        {
            int normalizedHeading = headingDegrees % 360;
            if (normalizedHeading < 0)
                normalizedHeading += 360;

            int cursor = 0;
            if (!AppendToSpan("HEADING ".AsSpan(), destination, ref cursor) ||
                !AppendInt(normalizedHeading, destination, "D3".AsSpan(), ref cursor) ||
                !AppendToSpan(" / ".AsSpan(), destination, ref cursor) ||
                !AppendToSpan(cardinal, destination, ref cursor))
            {
                charsWritten = 0;
                return false;
            }

            charsWritten = cursor;
            return true;
        }
    }
}
