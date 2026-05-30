using System;
using Hecton.Localization;

namespace Hecton8.Core
{
    /// <summary>
    /// Wrapper around a fixed char buffer for zero-allocation string building in hot paths.
    /// Utilizes LocNumericBuffer for localized template formatting.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FixedCharBuffer
    {
        private readonly char[] _buffer;
        private int _cursor;

        public FixedCharBuffer(char[] buffer)
        {
            _buffer = buffer;
            _cursor = 0;
        }

        public FixedCharBuffer(int size)
        {
            _buffer = size > 0 ? new char[size] : Array.Empty<char>();
            _cursor = 0;
        }

        public char[] Buffer => _buffer;
        public int Length => ResolveSafeLength();
        public ReadOnlySpan<char> AsSpan()
        {
            if (_buffer == null || _cursor <= 0)
                return ReadOnlySpan<char>.Empty;

            int safeLength = _cursor > _buffer.Length ? _buffer.Length : _cursor;
            return _buffer.AsSpan(0, safeLength);
        }

        public void Clear()
        {
            _cursor = 0;
        }

        public bool Append(ReadOnlySpan<char> text)
        {
            if (!TryGetRemainingSpan(text.Length, out Span<char> remaining))
                return false;

            text.CopyTo(remaining);
            _cursor += text.Length;
            return true;
        }

        public bool Append(char value)
        {
            if (!TryGetRemainingSpan(1, out Span<char> remaining))
                return false;

            remaining[0] = value;
            _cursor++;
            return true;
        }

        public bool Append(in FixedCharBuffer other)
        {
            return Append(other.AsSpan());
        }

        public bool AppendInt(int value)
        {
            if (!TryGetRemainingSpan(0, out Span<char> remaining)) return false;

            int written = 0;
            if (!ZeroGCFormatter.FastIntToChars(value, remaining, ref written))
                return false;

            _cursor += written;
            return true;
        }

        public bool AppendFloat(float value, int decimals = 1)
        {
            if (!TryGetRemainingSpan(0, out Span<char> remaining)) return false;

            int written = 0;
            if (!ZeroGCFormatter.FastFloatToChars(value, decimals, remaining, ref written))
                return false;

            _cursor += written;
            return true;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0)
        {
            if (!TryGetRemainingSpan(0, out Span<char> remaining)) return false;
            if (LocNumericBuffer.TryWrite(template, remaining, arg0, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0, LocNumericArg arg1)
        {
            if (!TryGetRemainingSpan(0, out Span<char> remaining)) return false;
            if (LocNumericBuffer.TryWrite(template, remaining, arg0, arg1, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0, LocNumericArg arg1, LocNumericArg arg2)
        {
            if (!TryGetRemainingSpan(0, out Span<char> remaining)) return false;
            if (LocNumericBuffer.TryWrite(template, remaining, arg0, arg1, arg2, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        private bool TryGetRemainingSpan(int requiredLength, out Span<char> remaining)
        {
            remaining = default;
            if (_buffer == null || requiredLength < 0 || _cursor < 0 || _cursor > _buffer.Length)
                return false;

            int remainingLength = _buffer.Length - _cursor;
            if (requiredLength > remainingLength)
                return false;

            remaining = _buffer.AsSpan(_cursor, remainingLength);
            return true;
        }

        private int ResolveSafeLength()
        {
            if (_buffer == null || _cursor <= 0)
                return 0;

            return _cursor > _buffer.Length ? _buffer.Length : _cursor;
        }

        public override string ToString()
        {
            int safeLength = ResolveSafeLength();
            if (safeLength == 0) return string.Empty;
            return new string(_buffer, 0, safeLength);
        }
    }
}
