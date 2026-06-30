using System;
using Hecton8.Core.Contracts;
using NUnit.Framework;

namespace Hecton8.Tests.Core.Contracts
{
    public sealed class AudioLogDiscoveryBitMaskTests
    {
        [Test]
        public void HasExpectedCapacity_ReturnsTrue_ForValidArray()
        {
            long[] words = new long[AudioLogDiscoveryBitMask.WordCount];
            Assert.IsTrue(AudioLogDiscoveryBitMask.HasExpectedCapacity(words));
        }

        [Test]
        public void HasExpectedCapacity_ReturnsFalse_ForNullArray()
        {
            Assert.IsFalse(AudioLogDiscoveryBitMask.HasExpectedCapacity(null));
        }

        [Test]
        public void HasExpectedCapacity_ReturnsFalse_ForArrayOfIncorrectSize()
        {
            long[] wordsSmall = new long[AudioLogDiscoveryBitMask.WordCount - 1];
            Assert.IsFalse(AudioLogDiscoveryBitMask.HasExpectedCapacity(wordsSmall));

            long[] wordsLarge = new long[AudioLogDiscoveryBitMask.WordCount + 1];
            Assert.IsFalse(AudioLogDiscoveryBitMask.HasExpectedCapacity(wordsLarge));
        }

        [Test]
        public void EnsureCapacity_CreatesArray_WhenNull()
        {
            long[] words = null;
            AudioLogDiscoveryBitMask.EnsureCapacity(ref words);

            Assert.IsNotNull(words);
            Assert.AreEqual(AudioLogDiscoveryBitMask.WordCount, words.Length);
            for (int i = 0; i < words.Length; i++)
            {
                Assert.AreEqual(0L, words[i]);
            }
        }

        [Test]
        public void EnsureCapacity_RecreatesArray_WhenIncorrectSize()
        {
            long[] words = new long[AudioLogDiscoveryBitMask.WordCount - 1];
            words[0] = 123L; // Should be overwritten

            AudioLogDiscoveryBitMask.EnsureCapacity(ref words);

            Assert.IsNotNull(words);
            Assert.AreEqual(AudioLogDiscoveryBitMask.WordCount, words.Length);
            for (int i = 0; i < words.Length; i++)
            {
                Assert.AreEqual(0L, words[i]);
            }
        }

        [Test]
        public void EnsureCapacity_DoesNothing_WhenCorrectSize()
        {
            long[] words = new long[AudioLogDiscoveryBitMask.WordCount];
            words[0] = 123L; // Should be kept

            AudioLogDiscoveryBitMask.EnsureCapacity(ref words);

            Assert.AreEqual(AudioLogDiscoveryBitMask.WordCount, words.Length);
            Assert.AreEqual(123L, words[0]);
        }
    }
}
