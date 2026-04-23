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
        [ThreadStatic] private static char[] _stagingBuffer;

        /// <summary>
        /// Write one numeric payload into a localized template without heap allocation.
        /// </summary>
        public static void Write(ReadOnlySpan<char> template, LocNumericArg value0, out char[] buffer, out int length)
        {
            WriteInternal(template, value0, default, default, default, 1, out buffer, out length);
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
            WriteInternal(template, value0, value1, default, default, 2, out buffer, out length);
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
            WriteInternal(template, value0, value1, value2, default, 3, out buffer, out length);
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
            WriteInternal(template, value0, value1, value2, value3, 4, out buffer, out length);
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

        private static void WriteInternal(
            ReadOnlySpan<char> template,
            LocNumericArg value0,
            LocNumericArg value1,
            LocNumericArg value2,
            LocNumericArg value3,
            int valueCount,
            out char[] buffer,
            out int length)
        {
            buffer = GetBuffer(template.Length + 24);
            int writeIndex = 0;
            int cursor = 0;

            while (cursor < template.Length)
            {
                if (!TryConsumeToken(template, ref cursor, out int tokenIndex, out ReadOnlySpan<char> format))
                {
                    EnsureCapacity(ref buffer, writeIndex + 1);
                    buffer[writeIndex++] = template[cursor++];
                    continue;
                }

                LocNumericArg value = ResolveValue(tokenIndex, value0, value1, value2, value3, valueCount);
                int charsWritten;
                while (!value.TryFormat(buffer.AsSpan(writeIndex), format, out charsWritten))
                {
                    EnsureCapacity(ref buffer, buffer.Length << 1);
                }

                writeIndex += charsWritten;
            }

            length = writeIndex;
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
            if (digit < '0' || digit > '3')
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
            while (capacity < requiredLength)
                capacity <<= 1;

            _stagingBuffer = new char[capacity]; // COLD ALLOC: char[capacity] — thread-local numeric formatter buffer — owner: LocNumericBuffer
            return _stagingBuffer;
        }

        private static void EnsureCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer.Length >= requiredLength)
                return;

            int capacity = buffer.Length;
            while (capacity < requiredLength)
                capacity <<= 1;

            buffer = new char[capacity]; // COLD ALLOC: char[capacity] — expanded thread-local numeric formatter buffer — owner: LocNumericBuffer
            _stagingBuffer = buffer;
        }
    }
}
