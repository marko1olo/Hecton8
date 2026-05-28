using System;
using System.Globalization;
using System.IO;
using Hecton.Localization;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void BabelPlaceholderOverflow_DoesNotPromoteCursorToCapacity()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("charCursor = maxGlyphs", source);
            StringAssert.Contains("charCursor = math.clamp(charCursor, 0, maxGlyphs)", source);
        }

        [Test]
        public void LocalizedSpanReadPath_DoesNotRefreshVaultBackedBytes()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/LocRegistry.cs");
            string source = File.ReadAllText(sourcePath);

            int methodStart = source.IndexOf("public static bool TryGetLocalizedSpan", StringComparison.Ordinal);
            Assert.GreaterOrEqual(methodStart, 0);
            int methodEnd = source.IndexOf("/// <summary>", methodStart + 1, StringComparison.Ordinal);
            Assert.Greater(methodEnd, methodStart);
            string methodBody = source.Substring(methodStart, methodEnd - methodStart);

            StringAssert.Contains("IsValidUtf8SliceNoRefresh(slice)", methodBody);
            StringAssert.DoesNotContain("RefreshUtf8BytesFromVault", methodBody);
            StringAssert.DoesNotContain("IsValidUtf8Slice(slice)", methodBody);
        }

        [Test]
        public void PdaDecryptLabel_DoesNotDoubleDecodeLengthBeforeBufferFetch()
        {
            string sourcePath = Path.Combine(Application.dataPath, "_Project/Scripts/UI/PDADataArchaeologyDecryptLabel.cs");
            string source = File.ReadAllText(sourcePath);

            StringAssert.DoesNotContain("LocRegistry.GetLength(hash)", source);
            StringAssert.Contains("LocRegistry.TryGetVisualBuffer(hash, out char[] source, out int length)", source);
        }
    }
}
