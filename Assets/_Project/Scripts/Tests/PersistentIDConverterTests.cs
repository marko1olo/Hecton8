using System;
using NUnit.Framework;
using Hecton8.SaveSystem;
using Hecton.Localization;

namespace Hecton8.Tests.SaveSystem
{
    [TestFixture]
    public class PersistentIDConverterTests
    {
        [Test]
        public void Constants_MatchLocHash()
        {
            Assert.AreEqual(LocHash.FnvOffsetBasis, PersistentIDConverter.Fnv1aOffsetBasis32);
            Assert.AreEqual(LocHash.FnvPrime, PersistentIDConverter.Fnv1aPrime32);
        }

        [Test]
        public void ToPersistentId32_NullString_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32((string)null));
        }

        [Test]
        public void ToPersistentId32_EmptyString_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(""));
        }

        [Test]
        public void ToPersistentId32_WhiteSpaceString_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32("   \t\n  "));
        }

        [Test]
        public void ToPersistentId32_ValidString_ReturnsHash()
        {
            string id = "test_id";
            uint expectedHash = unchecked((uint)LocHash.ComputeAsciiLowerInvariant(id));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(id));
        }

        [Test]
        public void ToPersistentId32_StringWithWhiteSpace_TrimsAndReturnsHash()
        {
            string id = "  test_id  \n";
            uint expectedHash = unchecked((uint)LocHash.ComputeAsciiLowerInvariant("test_id"));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(id));
        }

        [Test]
        public void ToPersistentId32_ReadOnlySpan_Empty_ReturnsZero()
        {
            ReadOnlySpan<char> span = ReadOnlySpan<char>.Empty;
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(span));
        }

        [Test]
        public void ToPersistentId32_ReadOnlySpan_WhiteSpace_ReturnsZero()
        {
            ReadOnlySpan<char> span = "   \t\n  ".AsSpan();
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(span));
        }

        [Test]
        public void ToPersistentId32_ReadOnlySpan_Valid_ReturnsHash()
        {
            ReadOnlySpan<char> span = "test_id".AsSpan();
            uint expectedHash = unchecked((uint)LocHash.ComputeAsciiLowerInvariant("test_id"));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(span));
        }

        [Test]
        public void ToPersistentId32_ReadOnlySpan_WithWhiteSpace_TrimsAndReturnsHash()
        {
            ReadOnlySpan<char> span = "  test_id  \n".AsSpan();
            uint expectedHash = unchecked((uint)LocHash.ComputeAsciiLowerInvariant("test_id"));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(span));
        }
    }
}
