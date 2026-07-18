using NUnit.Framework;
using System;
using Hecton8.Optimization;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class AssetRecordTests
    {
        [Test]
        public void Constructor_EmptyGuid_ThrowsArgumentException()
        {
            var emptyGuid = Guid.Empty;
            string validName = "ValidAssetName";
            long validSize = 1024;

            var ex = Assert.Throws<ArgumentException>(() => new AssetRecord(emptyGuid, validName, validSize));
            Assert.That(ex.Message, Does.Contain("Asset guid cannot be empty"));
            Assert.That(ex.ParamName, Is.EqualTo("guid"));
        }

        [Test]
        public void Constructor_NullName_ThrowsArgumentException()
        {
            var validGuid = Guid.NewGuid();
            string nullName = null;
            long validSize = 1024;

            var ex = Assert.Throws<ArgumentException>(() => new AssetRecord(validGuid, nullName, validSize));
            Assert.That(ex.Message, Does.Contain("Asset name cannot be null or empty"));
            Assert.That(ex.ParamName, Is.EqualTo("name"));
        }

        [Test]
        public void Constructor_EmptyName_ThrowsArgumentException()
        {
            var validGuid = Guid.NewGuid();
            string emptyName = string.Empty;
            long validSize = 1024;

            var ex = Assert.Throws<ArgumentException>(() => new AssetRecord(validGuid, emptyName, validSize));
            Assert.That(ex.Message, Does.Contain("Asset name cannot be null or empty"));
            Assert.That(ex.ParamName, Is.EqualTo("name"));
        }

        [Test]
        public void Constructor_ValidArguments_CreatesAssetRecord()
        {
            var validGuid = Guid.NewGuid();
            string validName = "ValidAssetName";
            long validSize = 1024;

            var record = new AssetRecord(validGuid, validName, validSize);

            Assert.That(record.AssetGuid, Is.EqualTo(validGuid.ToString()));
            Assert.That(record.Address, Is.EqualTo(validName));
            Assert.That(record.SizeBytes, Is.EqualTo(validSize));
            Assert.That(record.RefCount, Is.EqualTo(0));
        }
    }
}
