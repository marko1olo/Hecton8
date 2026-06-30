using NUnit.Framework;
using UnityEngine;
using Hecton8.Narrative;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class AudioLogDataFallbackTests
    {
        private AudioLogData _audioLogData;

        [SetUp]
        public void SetUp()
        {
            _audioLogData = ScriptableObject.CreateInstance<AudioLogData>();
        }

        [Test]
        public void TryWriteDisplayTitleOrFallback_CopiesTextToDestination()
        {
            _audioLogData.displayTitle = "Test Title";
            char[] dest = new char[20];

            bool result = _audioLogData.TryWriteDisplayTitleOrFallback(dest, out int length);

            Assert.That(result, Is.True);
            Assert.That(length, Is.EqualTo(10));
            Assert.That(new string(dest, 0, length), Is.EqualTo("Test Title"));
        }

        [Test]
        public void TryWriteAuthorOrFallback_CopiesTextToDestination()
        {
            _audioLogData.author = "Test Author";
            char[] dest = new char[20];

            bool result = _audioLogData.TryWriteAuthorOrFallback(dest, out int length);

            Assert.That(result, Is.True);
            Assert.That(length, Is.EqualTo(11));
            Assert.That(new string(dest, 0, length), Is.EqualTo("Test Author"));
        }

        [Test]
        public void TryWriteArchiveSummaryOrFallback_CopiesTextToDestination()
        {
            _audioLogData.archiveSummary = "Entry unavailable.";
            char[] dest = new char[50];

            bool result = _audioLogData.TryWriteArchiveSummaryOrFallback(dest, out int length);

            Assert.That(result, Is.True);
            Assert.That(length, Is.EqualTo(18));
            Assert.That(new string(dest, 0, length), Is.EqualTo("Entry unavailable."));
        }

        [Test]
        public void TryWriteRecordDateOrFallback_CopiesTextToDestination()
        {
            _audioLogData.recordDate = "DATE UNKNOWN";
            char[] dest = new char[20];

            bool result = _audioLogData.TryWriteRecordDateOrFallback(dest, out int length);

            Assert.That(result, Is.True);
            Assert.That(length, Is.EqualTo(12));
            Assert.That(new string(dest, 0, length), Is.EqualTo("DATE UNKNOWN"));
        }
    }
}
