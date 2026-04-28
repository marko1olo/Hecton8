using System;

namespace Hecton.Localization
{
    /// <summary>
    /// Numeric payload wrapper for zero-allocation localized template writes.
    /// </summary>
    public readonly struct LocNumericArg
    {
        private readonly int _intValue;
        private readonly float _floatValue;
        private readonly NumericKind _kind;

        private enum NumericKind : byte
        {
            Int = 0,
            Float = 1
        }

        private LocNumericArg(int intValue, float floatValue, NumericKind kind)
        {
            _intValue = intValue;
            _floatValue = floatValue;
            _kind = kind;
        }

        /// <summary>
        /// Wrap an integer payload.
        /// </summary>
        public static LocNumericArg Int(int value)
        {
            return new LocNumericArg(value, 0f, NumericKind.Int);
        }

        /// <summary>
        /// Wrap a floating point payload.
        /// </summary>
        public static LocNumericArg Float(float value)
        {
            return new LocNumericArg(0, value, NumericKind.Float);
        }

        internal bool TryFormat(Span<char> destination, ReadOnlySpan<char> format, out int charsWritten)
        {
            switch (_kind)
            {
                case NumericKind.Float:
                    return format.Length == 0
                        ? _floatValue.TryFormat(destination, out charsWritten)
                        : _floatValue.TryFormat(destination, out charsWritten, format);

                default:
                    return format.Length == 0
                        ? _intValue.TryFormat(destination, out charsWritten)
                        : _intValue.TryFormat(destination, out charsWritten, format);
            }
        }
    }

    /// <summary>
    /// Thread-local numeric formatter for HUD templates such as "DEPTH: -{N0:F0} m".
    /// </summary>
    public static class LocNumericBuffer
    {
        private const int DefaultBufferSlack = 24;
        private const int MaxWriteAttempts = 8;
        private const int CapacityGrowthWatchdogLimit = 31;

        [ThreadStatic] private static char[] _stagingBuffer;

        /// <summary>
        /// Copy a literal template into the thread-local staging buffer without heap allocation.
        /// </summary>
        public static void Write(ReadOnlySpan<char> template, out char[] buffer, out int length)
        {
            buffer = GetBuffer(template.Length + 1);
            template.CopyTo(buffer);
            length = template.Length;
        }

        /// <summary>
        /// Copy a literal template into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(ReadOnlySpan<char> template, Span<char> destination, out int length)
        {
            if (template.Length > destination.Length)
            {
                length = 0;
                return false;
            }

            template.CopyTo(destination);
            length = template.Length;
            return true;
        }

        /// <summary>
        /// Write one numeric payload into a localized template without heap allocation.
        /// </summary>
        public static void Write(ReadOnlySpan<char> template, LocNumericArg value0, out char[] buffer, out int length)
        {
            WriteInternal(template, value0, default, default, default, default, 1, out buffer, out length);
        }

        /// <summary>
        /// Write one numeric payload into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(ReadOnlySpan<char> template, Span<char> destination, LocNumericArg value0, out int length)
        {
            return TryWriteInternal(template, destination, value0, default, default, default, default, 1, out length);
        }

        /// <summary>
        /// Write two numeric payloads into a localized template without heap allocation.
        /// </summary>
        public static void Write(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            out char[] buffer,
            out int length)
        {
            WriteInternal(template, value0, value1, default, default, default, 2, out buffer, out length);
        }

        /// <summary>
        /// Write two numeric payloads into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(
            ReadOnlySpan<char> template,
            Span<char> destination,
            LocNumericArg value0,
            LocNumericArg value1,
            out int length)
        {
            return TryWriteInternal(template, destination, value0, value1, default, default, default, 2, out length);
        }

        /// <summary>
        /// Write three numeric payloads into a localized template without heap allocation.
        /// </summary>
        public static void Write(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            out char[] buffer,
            out int length)
        {
            WriteInternal(template, value0, value1, value2, default, default, 3, out buffer, out length);
        }

        /// <summary>
        /// Write three numeric payloads into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(
            ReadOnlySpan<char> template,
            Span<char> destination,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            out int length)
        {
            return TryWriteInternal(template, destination, value0, value1, value2, default, default, 3, out length);
        }

        /// <summary>
        /// Write four numeric payloads into a localized template without heap allocation.
        /// </summary>
        public static void Write(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            out char[] buffer,
            out int length)
        {
            WriteInternal(template, value0, value1, value2, value3, default, 4, out buffer, out length);
        }

        /// <summary>
        /// Write four numeric payloads into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(
            ReadOnlySpan<char> template,
            Span<char> destination,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            out int length)
        {
            return TryWriteInternal(template, destination, value0, value1, value2, value3, default, 4, out length);
        }

        /// <summary>
        /// Write five numeric payloads into a localized template without heap allocation.
        /// </summary>
        public static void Write(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            out char[] buffer,
            out int length)
        {
            WriteInternal(template, value0, value1, value2, value3, value4, 5, out buffer, out length);
        }

        /// <summary>
        /// Write five numeric payloads into a caller-owned destination span without heap allocation.
        /// </summary>
        public static bool TryWrite(
            ReadOnlySpan<char> template,
            Span<char> destination,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            out int length)
        {
            return TryWriteInternal(template, destination, value0, value1, value2, value3, value4, 5, out length);
        }

        /// <summary>
        /// Resolve a localized template by FNV-1a key hash and write one numeric payload into it.
        /// </summary>
        public static void Write(int templateKeyHash, LocNumericArg value0, out char[] buffer, out int length)
        {
            Write(LocRegistry.ResolveRaw(templateKeyHash), value0, out buffer, out length);
        }

        /// <summary>
        /// Resolve a localized template by FNV-1a key hash and write two numeric payloads into it.
        /// </summary>
        public static void Write(int templateKeyHash, LocNumericArg value0, LocNumericArg value1, out char[] buffer, out int length)
        {
            Write(LocRegistry.ResolveRaw(templateKeyHash), value0, value1, out buffer, out length);
        }

        /// <summary>
        /// Resolve a localized template by FNV-1a key hash and write three numeric payloads into it.
        /// </summary>
        public static void Write(
            int templateKeyHash,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            out char[] buffer,
            out int length)
        {
            Write(LocRegistry.ResolveRaw(templateKeyHash), value0, value1, value2, out buffer, out length);
        }

        /// <summary>
        /// Resolve a localized template by FNV-1a key hash and write four numeric payloads into it.
        /// </summary>
        public static void Write(
            int templateKeyHash,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            out char[] buffer,
            out int length)
        {
            Write(LocRegistry.ResolveRaw(templateKeyHash), value0, value1, value2, value3, out buffer, out length);
        }

        /// <summary>
        /// Resolve a localized template by FNV-1a key hash and write five numeric payloads into it.
        /// </summary>
        public static void Write(
            int templateKeyHash,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            out char[] buffer,
            out int length)
        {
            Write(LocRegistry.ResolveRaw(templateKeyHash), value0, value1, value2, value3, value4, out buffer, out length);
        }

        private static void WriteInternal(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            int valueCount,
            out char[] buffer,
            out int length)
        {
            buffer = GetBuffer(template.Length + DefaultBufferSlack);
            for (int attempt = 0; attempt < MaxWriteAttempts; attempt++)
            {
                if (TryWriteInternal(template, buffer.AsSpan(), value0, value1, value2, value3, value4, valueCount, out length))
                    return;

                int currentCapacity = buffer.Length;
                int nextRequiredLength = currentCapacity > (int.MaxValue >> 1)
                    ? int.MaxValue
                    : currentCapacity << 1;
                EnsureCapacity(ref buffer, nextRequiredLength);
                if (buffer.Length <= currentCapacity)
                    break;
            }

            WriteTemplateFallback(template, ref buffer, out length);
        }

        private static bool TryWriteInternal(
            ReadOnlySpan<char> template,
            Span<char> destination,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            int valueCount,
            out int length)
        {
            int writeIndex = 0;
            int cursor = 0;

            while (cursor < template.Length)
            {
                if (!TryConsumeToken(template, ref cursor, out int tokenIndex, out ReadOnlySpan<char> format))
                {
                    if ((uint)writeIndex >= (uint)destination.Length)
                    {
                        length = 0;
                        return false;
                    }

                    destination[writeIndex++] = template[cursor++];
                    continue;
                }

                LocNumericArg value = ResolveValue(tokenIndex, value0, value1, value2, value3, value4, valueCount);
                if (!value.TryFormat(destination.Slice(writeIndex), format, out int charsWritten))
                {
                    length = 0;
                    return false;
                }

                writeIndex += charsWritten;
            }

            length = writeIndex;
            return true;
        }

        private static bool TryConsumeToken(
            ReadOnlySpan<char> template,
            ref int cursor,
            out int tokenIndex,
            out ReadOnlySpan<char> format)
        {
            tokenIndex = -1;
            format = default;

            if (template[cursor] != '{' || cursor + 3 >= template.Length || template[cursor + 1] != 'N')
                return false;

            char digit = template[cursor + 2];
            if (digit < '0' || digit > '4')
                return false;

            tokenIndex = digit - '0';
            int closeIndex = cursor + 3;
            if (template[closeIndex] == '}')
            {
                cursor = closeIndex + 1;
                return true;
            }

            if (template[closeIndex] != ':')
            {
                tokenIndex = -1;
                return false;
            }

            int formatStart = closeIndex + 1;
            int scan = formatStart;
            while (scan < template.Length && template[scan] != '}')
                scan++;

            if (scan >= template.Length)
            {
                tokenIndex = -1;
                return false;
            }

            format = template.Slice(formatStart, scan - formatStart);
            cursor = scan + 1;
            return true;
        }

        private static LocNumericArg ResolveValue(
            int tokenIndex,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            LocNumericArg value4,
            int valueCount)
        {
            if (tokenIndex < 0 || tokenIndex >= valueCount)
                return default;

            switch (tokenIndex)
            {
                case 1:
                    return value1;

                case 2:
                    return value2;

                case 3:
                    return value3;

                case 4:
                    return value4;

                default:
                    return value0;
            }
        }

        private static char[] GetBuffer(int requiredLength)
        {
            char[] buffer = _stagingBuffer;
            if (buffer != null && buffer.Length >= requiredLength)
                return buffer;

            int capacity = buffer == null ? 128 : buffer.Length;
            capacity = ResolveExpandedCapacity(capacity, requiredLength);

            _stagingBuffer = new char[capacity]; // COLD ALLOC: char[capacity] — thread-local numeric formatter buffer — owner: LocNumericBuffer
            return _stagingBuffer;
        }

        private static void EnsureCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer.Length >= requiredLength)
                return;

            int capacity = ResolveExpandedCapacity(buffer.Length, requiredLength);

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] — expanded thread-local numeric formatter buffer — owner: LocNumericBuffer
            _stagingBuffer = buffer;
        }

        private static int ResolveExpandedCapacity(int currentCapacity, int requiredLength)
        {
            int capacity = Math.Max(1, currentCapacity);
            int growthWatchdog = CapacityGrowthWatchdogLimit;
            while (capacity < requiredLength && growthWatchdog-- > 0)
            {
                if (capacity > (int.MaxValue >> 1))
                    return requiredLength;

                capacity <<= 1;
            }

            return capacity < requiredLength ? requiredLength : capacity;
        }

        private static void WriteTemplateFallback(ReadOnlySpan<char> template, ref char[] buffer, out int length)
        {
            EnsureCapacity(ref buffer, template.Length);
            template.CopyTo(buffer);
            length = template.Length;
        }
    }
}
