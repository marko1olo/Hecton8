using System;
using NUnit.Framework;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class PersistentIDConverterEditTests
    {
        [Test]
        public void PersistentIDConverter_ToPersistentId32_ReturnsZeroForNullEmptyOrWhitespace()
        {
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32((string)null));
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32(string.Empty));
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32("   "));
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32("\t"));
            Assert.AreEqual(0u, PersistentIDConverter.ToPersistentId32("\n"));
        }
    }
}
