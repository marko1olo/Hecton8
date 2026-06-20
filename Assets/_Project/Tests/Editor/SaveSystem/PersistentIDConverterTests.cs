#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using NUnit.Framework;
using Hecton8.SaveSystem;
using Hecton.Localization;

namespace Hecton8.Tests.SaveSystem
{
    public class PersistentIDConverterTests
    {
        [Test]
        public void ToPersistentId32_EmptyString_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(""));
        }

        [Test]
        public void ToPersistentId32_NullString_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32((string)null));
        }

        [Test]
        public void ToPersistentId32_EmptySpan_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(ReadOnlySpan<char>.Empty));
        }

        [Test]
        public void ToPersistentId32_WhiteSpaceSpan_ReturnsZero()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32("   ".AsSpan()));
        }

        [Test]
        public void ToPersistentId32_StringAndSpanMatch()
        {
            string testId = "test_object_1";
            uint expectedHash = LocHash.ComputeAsciiLowerInvariant(testId);
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(testId));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(testId.AsSpan()));
        }

        [Test]
        public void ToPersistentId32_SpanTrimsWhiteSpace()
        {
            string testId = "test_object_1";
            uint expectedHash = LocHash.ComputeAsciiLowerInvariant(testId);

            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32($"  {testId}  ".AsSpan()));
            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32($"\n{testId}\t".AsSpan()));
        }

        [Test]
        public void ToPersistentId32_CaseInsensitive()
        {
            string testIdLower = "my_custom_id";
            string testIdUpper = "MY_CUSTOM_ID";
            string testIdMixed = "My_Custom_Id";

            uint hashLower = PersistentIDConverter.ToPersistentId32(testIdLower.AsSpan());
            uint hashUpper = PersistentIDConverter.ToPersistentId32(testIdUpper.AsSpan());
            uint hashMixed = PersistentIDConverter.ToPersistentId32(testIdMixed.AsSpan());

            Assert.AreEqual(hashLower, hashUpper);
            Assert.AreEqual(hashLower, hashMixed);
        }

        [Test]
        public void ToPersistentId32_KnownValues()
        {
            string testStr = "KnownId123";

            // Expected hash logic:
            // hash = FnvOffsetBasis
            // hash ^= char, hash *= FnvPrime for each lowercase char
            uint expectedHash = LocHash.FnvOffsetBasis;
            string lowerCase = "knownid123";
            foreach (char c in lowerCase)
            {
                expectedHash ^= (byte)c;
                expectedHash *= LocHash.FnvPrime;
            }

            Assert.AreEqual(expectedHash, PersistentIDConverter.ToPersistentId32(testStr.AsSpan()));
        }
    }
}
#endif
