using System;
using Hecton.Localization;

namespace Hecton8.Core
{
    /// <summary>
    /// Wrapper around a fixed char buffer for zero-allocation string building in hot paths.
    /// Utilizes LocNumericBuffer for localized template formatting.
    /// </summary>
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
            _buffer = new char[size];
            _cursor = 0;
        }

        public char[] Buffer => _buffer;
        public int Length => _cursor;
        public ReadOnlySpan<char> AsSpan() => _buffer != null ? _buffer.AsSpan(0, _cursor) : ReadOnlySpan<char>.Empty;

        public void Clear()
        {
            _cursor = 0;
        }

        public bool Append(ReadOnlySpan<char> text)
        {
            if (_buffer == null || _cursor + text.Length > _buffer.Length)
                return false;

            text.CopyTo(_buffer.AsSpan(_cursor));
            _cursor += text.Length;
            return true;
        }

        public bool Append(in FixedCharBuffer other)
        {
            return Append(other.AsSpan());
        }

        public bool AppendInt(int value)
        {
            if (_buffer == null) return false;
            if (value.TryFormat(_buffer.AsSpan(_cursor), out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendFloat(float value, int decimals = 1)
        {
            if (_buffer == null) return false;
            
            // Simple float formatting helper for zero-GC
            string format = decimals == 0 ? "F0" : (decimals == 1 ? "F1" : "F2");
            if (value.TryFormat(_buffer.AsSpan(_cursor), out int written, format))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0)
        {
            if (_buffer == null) return false;
            if (LocNumericBuffer.TryWrite(template, _buffer.AsSpan(_cursor), arg0, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0, LocNumericArg arg1)
        {
            if (_buffer == null) return false;
            if (LocNumericBuffer.TryWrite(template, _buffer.AsSpan(_cursor), arg0, arg1, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public bool AppendTemplate(ReadOnlySpan<char> template, LocNumericArg arg0, LocNumericArg arg1, LocNumericArg arg2)
        {
            if (_buffer == null) return false;
            if (LocNumericBuffer.TryWrite(template, _buffer.AsSpan(_cursor), arg0, arg1, arg2, out int written))
            {
                _cursor += written;
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (_buffer == null || _cursor == 0) return string.Empty;
            return new string(_buffer, 0, _cursor);
        }
    }
}
