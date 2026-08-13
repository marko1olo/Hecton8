using NUnit.Framework;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class VRAMBudgetThresholdsTests
    {
        private const long BytesPerMegabyte = 1024L * 1024L;

        [Test]
        public void Default_ReturnsExpectedValues()
        {
            // Act
            var defaults = VRAMBudgetThresholds.Default;

            // Assert
            Assert.That(defaults.TotalVRAMBudgetBytes, Is.EqualTo(1800 * BytesPerMegabyte));
            Assert.That(defaults.TextureMemoryBudgetBytes, Is.EqualTo(900 * BytesPerMegabyte));
            Assert.That(defaults.RenderTextureMemoryBudgetBytes, Is.EqualTo(320 * BytesPerMegabyte));
            Assert.That(defaults.VisorRTBudgetBytes, Is.EqualTo(64 * BytesPerMegabyte));
            Assert.That(defaults.CameraRTBudgetBytes, Is.EqualTo(160 * BytesPerMegabyte));
            Assert.That(defaults.PostFXRTBudgetBytes, Is.EqualTo(64 * BytesPerMegabyte));
            Assert.That(defaults.UIRTBudgetBytes, Is.EqualTo(32 * BytesPerMegabyte));
        }

        [Test]
        public void ResolveRuntimeBudget_WithUnsetBudget_ReturnsRuntimeDefault()
        {
            // Arrange
            var unset = new VRAMBudgetThresholds();

            // Act
            var resolved = VRAMBudgetThresholds.ResolveRuntimeBudget(unset);
            var expected = VRAMBudgetThresholds.RuntimeDefault;

            // Assert
            Assert.That(resolved.TotalVRAMBudgetBytes, Is.EqualTo(expected.TotalVRAMBudgetBytes));
            Assert.That(resolved.TextureMemoryBudgetBytes, Is.EqualTo(expected.TextureMemoryBudgetBytes));
            Assert.That(resolved.RenderTextureMemoryBudgetBytes, Is.EqualTo(expected.RenderTextureMemoryBudgetBytes));
        }

        [Test]
        public void ResolveRuntimeBudget_WithDefaultBudget_ReturnsRuntimeDefault()
        {
            // Arrange
            var defaults = VRAMBudgetThresholds.Default;

            // Act
            var resolved = VRAMBudgetThresholds.ResolveRuntimeBudget(defaults);
            var expected = VRAMBudgetThresholds.RuntimeDefault;

            // Assert
            Assert.That(resolved.TotalVRAMBudgetBytes, Is.EqualTo(expected.TotalVRAMBudgetBytes));
            Assert.That(resolved.TextureMemoryBudgetBytes, Is.EqualTo(expected.TextureMemoryBudgetBytes));
            Assert.That(resolved.RenderTextureMemoryBudgetBytes, Is.EqualTo(expected.RenderTextureMemoryBudgetBytes));
        }

        [Test]
        public void ResolveRuntimeBudget_WithCustomBudget_ReturnsCustomBudget()
        {
            // Arrange
            var custom = new VRAMBudgetThresholds
            {
                TotalVRAMBudgetBytes = 4000 * BytesPerMegabyte,
                TextureMemoryBudgetBytes = 2000 * BytesPerMegabyte,
                RenderTextureMemoryBudgetBytes = 1000 * BytesPerMegabyte,
                VisorRTBudgetBytes = 200 * BytesPerMegabyte,
                CameraRTBudgetBytes = 400 * BytesPerMegabyte,
                PostFXRTBudgetBytes = 300 * BytesPerMegabyte,
                UIRTBudgetBytes = 100 * BytesPerMegabyte
            };

            // Act
            var resolved = VRAMBudgetThresholds.ResolveRuntimeBudget(custom);

            // Assert
            Assert.That(resolved.TotalVRAMBudgetBytes, Is.EqualTo(custom.TotalVRAMBudgetBytes));
            Assert.That(resolved.TextureMemoryBudgetBytes, Is.EqualTo(custom.TextureMemoryBudgetBytes));
            Assert.That(resolved.RenderTextureMemoryBudgetBytes, Is.EqualTo(custom.RenderTextureMemoryBudgetBytes));
            Assert.That(resolved.VisorRTBudgetBytes, Is.EqualTo(custom.VisorRTBudgetBytes));
            Assert.That(resolved.CameraRTBudgetBytes, Is.EqualTo(custom.CameraRTBudgetBytes));
            Assert.That(resolved.PostFXRTBudgetBytes, Is.EqualTo(custom.PostFXRTBudgetBytes));
            Assert.That(resolved.UIRTBudgetBytes, Is.EqualTo(custom.UIRTBudgetBytes));
        }
    }
}
