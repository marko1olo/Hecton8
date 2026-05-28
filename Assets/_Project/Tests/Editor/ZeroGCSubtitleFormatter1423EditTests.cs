using System;
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ZeroGCSubtitleFormatter1423EditTests
    {
        [Test]
        public void NumericFormatter_UsesInvariantCulture_WhenThreadCultureUsesCommaDecimal()
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
                Span<char> buffer = stackalloc char[16];

                bool wrote = ZeroGCFormatter.TryFormatFloat(12.5f, buffer, "F1".AsSpan(), out int length);

                Assert.IsTrue(wrote);
                Assert.AreEqual(4, length);
                Assert.AreEqual('1', buffer[0]);
                Assert.AreEqual('2', buffer[1]);
                Assert.AreEqual('.', buffer[2]);
                Assert.AreEqual('5', buffer[3]);
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        [Test]
        public void TruncatedAppend_ClampsCursor_AndAppliesAsciiEllipsis()
        {
            Span<char> buffer = stackalloc char[5];
            int cursor = 0;

            bool fullWrite = ZeroGCFormatter.AppendToSpanTruncated(
                "ABCDEFGH".AsSpan(),
                buffer,
                ref cursor,
                out bool truncated);
            ZeroGCFormatter.AppendAsciiEllipsis(buffer, ref cursor);

            Assert.IsFalse(fullWrite);
            Assert.IsTrue(truncated);
            Assert.AreEqual(5, cursor);
            Assert.AreEqual('A', buffer[0]);
            Assert.AreEqual('B', buffer[1]);
            Assert.AreEqual('.', buffer[2]);
            Assert.AreEqual('.', buffer[3]);
            Assert.AreEqual('.', buffer[4]);
        }

        [Test]
        public void MockSubtitleSpamFormatter_StaysInsideFixedBuffer_ForFiveHundredWarnings()
        {
            ReadOnlySpan<char> template = "VWS O2 {N0:F1}%".AsSpan();
            Span<char> buffer = stackalloc char[32];

            for (int i = 0; i < 500; i++)
            {
                float value = (i % 101) * 0.1f;
                bool wrote = LocNumericBuffer.TryWrite(template, buffer, LocNumericArg.Float(value), out int length);

                Assert.IsTrue(wrote);
                Assert.Greater(length, 0);
                Assert.LessOrEqual(length, buffer.Length);
                for (int c = 0; c < length; c++)
                    Assert.AreNotEqual(',', buffer[c]);
            }
        }

        [Test]
        public void MockSubtitleOverflowFormatter_FailsClosedWithoutMovingCursorPastCapacity()
        {
            Span<char> buffer = stackalloc char[8];
            int cursor = 0;

            bool fullWrite = ZeroGCFormatter.AppendToSpanTruncated(
                "EXTREMELY_LONG_LOCALIZED_WARNING_LINE".AsSpan(),
                buffer,
                ref cursor,
                out bool truncated);
            ZeroGCFormatter.AppendAsciiEllipsis(buffer, ref cursor);

            Assert.IsFalse(fullWrite);
            Assert.IsTrue(truncated);
            Assert.AreEqual(buffer.Length, cursor);
            Assert.AreEqual('.', buffer[buffer.Length - 1]);
        }
    }
}
