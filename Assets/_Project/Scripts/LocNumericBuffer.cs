using System;
using System.Threading;
using Hecton8.Core;

namespace Hecton.Localization
{
    /// <summary>
    /// Numeric payload wrapper for zero-allocation localized template writes.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
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
                    int cursor = 0;
                    bool wrote = ZeroGCFormatter.FastIntToChars(_intValue, destination, format, ref cursor);
                    charsWritten = cursor;
                    return wrote;
            }
        }
    }

    /// <summary>
    /// Fixed-ring numeric formatter for HUD templates such as "DEPTH: -{N0:F0} m".
    /// </summary>
    public static class LocNumericBuffer
    {
        private const int DefaultBufferSlack = 24;
        private const int MaxNumericBufferChars = 4096;
        private const int NumericBufferSlotCount = 16;
        private const int NumericBufferSlotMask = NumericBufferSlotCount - 1;

        private static readonly char[][] _stagingBufferRing = CreateStagingBufferRing();
        private static int _stagingBufferCursor = -1;

        /// <summary>
        /// Copy a literal template into the fixed-ring staging buffer without heap allocation.
        /// </summary>
        public static void Write(ReadOnlySpan<char> template, out char[] buffer, out int length)
        {
            buffer = GetBuffer(template.Length + 1);
            WriteTemplateFallback(template, ref buffer, out length);
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
            if (TryWriteInternal(template, buffer.AsSpan(), value0, value1, value2, value3, value4, valueCount, out length))
                return;

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

            if (template[cursor] != '{' || cursor + 2 >= template.Length)
                return false;

            int digitIndex = template[cursor + 1] == 'N' ? cursor + 2 : cursor + 1;
            char digit = template[digitIndex];
            if (digit < '0' || digit > '4')
                return false;

            tokenIndex = digit - '0';
            int closeIndex = digitIndex + 1;
            if (closeIndex >= template.Length)
            {
                tokenIndex = -1;
                return false;
            }

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

        private static char[][] CreateStagingBufferRing()
        {
            char[][] ring = new char[NumericBufferSlotCount][]; // COLD ALLOC: char[][16] - numeric formatter ring table - owner: LocNumericBuffer
            for (int i = 0; i < ring.Length; i++)
                ring[i] = new char[MaxNumericBufferChars]; // COLD ALLOC: char[4096] - prewarmed numeric formatter slot - owner: LocNumericBuffer

            return ring;
        }

        private static char[] GetBuffer(int requiredLength)
        {
            int slot = Interlocked.Increment(ref _stagingBufferCursor) & NumericBufferSlotMask;
            char[] buffer = _stagingBufferRing[slot];
            return buffer ?? Array.Empty<char>();
        }

        private static void EnsureCapacity(ref char[] buffer, int requiredLength)
        {
            if (buffer == null || buffer.Length <= 0)
                buffer = GetBuffer(requiredLength);
        }

        private static void WriteTemplateFallback(ReadOnlySpan<char> template, ref char[] buffer, out int length)
        {
            EnsureCapacity(ref buffer, template.Length);
            if (buffer == null || buffer.Length <= 0)
            {
                length = 0;
                return;
            }

            int safeLength = Math.Min(template.Length, buffer.Length);
            if (safeLength > 0)
                template.Slice(0, safeLength).CopyTo(buffer);

            if (template.Length > buffer.Length && buffer.Length >= 3)
            {
                safeLength = Math.Max(0, buffer.Length - 3);
                buffer[safeLength++] = '.';
                buffer[safeLength++] = '.';
                buffer[safeLength++] = '.';
            }

            length = safeLength;
        }
    }
}
