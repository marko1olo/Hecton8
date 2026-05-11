using System;
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
            return value.TryFormat(destination, out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryWriteInt(int value, Span<char> destination, out int charsWritten)
        {
            return TryFormatInt(value, destination, out charsWritten);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatInt(
            int value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            out int charsWritten)
        {
            return format.Length == 0
                ? value.TryFormat(destination, out charsWritten)
                : value.TryFormat(destination, out charsWritten, format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFormatFloat(float value, Span<char> destination, out int charsWritten)
        {
            return value.TryFormat(destination, out charsWritten);
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
            int scale = Pow10(safePrecision);
            bool negative = value < 0f;
            float absolute = negative ? -value : value;
            int scaled = (int)((absolute * scale) + 0.5f);
            int integerPart = scale > 1 ? scaled / scale : scaled;
            int fractionPart = scale > 1 ? scaled - (integerPart * scale) : 0;

            int cursor = 0;
            if (negative)
            {
                if ((uint)cursor >= (uint)destination.Length)
                    return false;

                destination[cursor++] = '-';
            }

            if (!AppendPositiveIntDigits(integerPart, destination, ref cursor))
                return false;

            if (safePrecision <= 0)
            {
                charsWritten = cursor;
                return true;
            }

            if ((uint)cursor >= (uint)destination.Length)
                return false;

            destination[cursor++] = '.';
            int divisor = scale / 10;
            for (int i = 0; i < safePrecision; i++)
            {
                if ((uint)cursor >= (uint)destination.Length)
                    return false;

                int digit = divisor > 0 ? fractionPart / divisor : 0;
                destination[cursor++] = (char)('0' + digit);
                fractionPart -= digit * divisor;
                divisor /= 10;
            }

            charsWritten = cursor;
            return true;
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
            return format.Length == 0
                ? value.TryFormat(destination, out charsWritten)
                : value.TryFormat(destination, out charsWritten, format);
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
        private static int Pow10(int power)
        {
            switch (power)
            {
                case 0:
                    return 1;
                case 1:
                    return 10;
                case 2:
                    return 100;
                case 3:
                    return 1000;
                case 4:
                    return 10000;
                case 5:
                    return 100000;
                default:
                    return 1000000;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool AppendPositiveIntDigits(int value, Span<char> destination, ref int cursor)
        {
            if (value == 0)
            {
                if ((uint)cursor >= (uint)destination.Length)
                    return false;

                destination[cursor++] = '0';
                return true;
            }

            int divisor = 1;
            while (value / divisor >= 10)
                divisor *= 10;

            while (divisor > 0)
            {
                if ((uint)cursor >= (uint)destination.Length)
                    return false;

                int digit = value / divisor;
                destination[cursor++] = (char)('0' + digit);
                value -= digit * divisor;
                divisor /= 10;
            }

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
        public static bool AppendInt(int value, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            Span<char> remaining = destination.Slice(cursor);
            if (!TryFormatInt(value, remaining, out int written))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendInt(
            int value,
            Span<char> destination,
            ReadOnlySpan<char> format,
            ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            Span<char> remaining = destination.Slice(cursor);
            if (!TryFormatInt(value, remaining, format, out int written))
                return false;

            cursor += written;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AppendFloat(float value, Span<char> destination, ref int cursor)
        {
            if (cursor < 0 || cursor > destination.Length)
                return false;

            Span<char> remaining = destination.Slice(cursor);
            if (!TryFormatFloat(value, remaining, out int written))
                return false;

            cursor += written;
            return true;
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
