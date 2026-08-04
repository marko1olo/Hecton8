#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class VRAMMonitorTests
    {
        private GameObject _go;
        private VRAMMonitor _monitor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestVRAMMonitor");
            _monitor = _go.AddComponent<VRAMMonitor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void GetVRAMBreakdown_ReturnsInternalStateCorrectly()
        {
            // Arrange
            long expectedTexture = 1024L * 1024L * 500L;
            long expectedRenderTexture = 1024L * 1024L * 250L;
            long expectedTotal = 1024L * 1024L * 1000L;

            var type = typeof(VRAMMonitor);
            var textureField = type.GetField("<TextureMemoryBytes>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            var rtField = type.GetField("<RenderTextureMemoryBytes>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            var totalField = type.GetField("<TotalVRAMBytes>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(textureField, Is.Not.Null, "TextureMemoryBytes backing field not found");
            Assert.That(rtField, Is.Not.Null, "RenderTextureMemoryBytes backing field not found");
            Assert.That(totalField, Is.Not.Null, "TotalVRAMBytes backing field not found");

            textureField.SetValue(_monitor, expectedTexture);
            rtField.SetValue(_monitor, expectedRenderTexture);
            totalField.SetValue(_monitor, expectedTotal);

            // Act
            _monitor.GetVRAMBreakdown(out long actualTexture, out long actualRenderTexture, out long actualTotal);

            // Assert
            Assert.That(actualTexture, Is.EqualTo(expectedTexture));
            Assert.That(actualRenderTexture, Is.EqualTo(expectedRenderTexture));
            Assert.That(actualTotal, Is.EqualTo(expectedTotal));
        }
    }
}
#endif
