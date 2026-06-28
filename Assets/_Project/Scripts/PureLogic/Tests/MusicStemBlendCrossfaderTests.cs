using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class MusicStemBlendCrossfaderTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentVolume = 0.5f;
            float targetVolume = 0.8f;
            float crossfadeDurationSec = 1.0f;
            float startTime = 0.0f;

            // Act: evaluate at different times
            float startVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.0f, startTime);
            float midVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.5f, startTime);
            float endVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 1.0f, startTime);
            float pastEndVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 2.0f, startTime);

            // Assert: Verify expected output behaviour
            Assert.That(startVol, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(midVol, Is.EqualTo(0.65f).Within(0.001f)); // Linear interpolation since both > 0.0001f
            Assert.That(endVol, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(pastEndVol, Is.EqualTo(0.8f).Within(0.001f)); // Clamped at 1
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentVolume = -0.5f; // Should clamp to 0
            float targetVolume = 1.5f;  // Should clamp to 1
            float crossfadeDurationSec = 1.0f;
            float startTime = 0.0f;

            // Act
            float startVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.0f, startTime);
            float midVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.5f, startTime);
            float endVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 1.0f, startTime);

            // Assert
            Assert.That(startVol, Is.EqualTo(0.0f).Within(0.001f));
            // Since safeCurrentVolume = 0 <= 0.0001, we use formula: safeTargetVolume * (t * (2f - t))
            // safeTargetVolume = 1.0, t = 0.5
            // 1.0 * (0.5 * (2 - 0.5)) = 0.5 * 1.5 = 0.75
            Assert.That(midVol, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(endVol, Is.EqualTo(1.0f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentVolume = 0.0f;
            float targetVolume = 0.0f;
            float crossfadeDurationSec = 0.0f; // Should use min duration 0.01f
            float startTime = 0.0f;
            float currentTime = 0.5f;

            // Act
            float vol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, currentTime, startTime);

            // Assert
            Assert.That(vol, Is.EqualTo(0.0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentVolume = 0.8f;
            float targetVolume = 0.0f; // Fading out to 0
            float crossfadeDurationSec = 2.0f;
            float startTime = 10.0f;
            float currentTime = 5.0f; // negative elapsed time

            // Act
            float vol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, currentTime, startTime);

            // Assert
            // t = 0
            Assert.That(vol, Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentVolume = float.NaN; // Should become 0
            float targetVolume = float.PositiveInfinity; // Should become 0, then clamp to 0
            float crossfadeDurationSec = 1.0f;
            float startTime = 0.0f;

            // Act
            float startVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.0f, startTime);
            float midVol = MusicStemBlendCrossfader.Calculate(currentVolume, targetVolume, crossfadeDurationSec, 0.5f, startTime);

            // Assert
            Assert.That(startVol, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(midVol, Is.EqualTo(0.0f).Within(0.001f));

            // Fading out
            float fadeOutCurrent = 1.0f;
            float fadeOutTarget = 0.0f;
            float fadeOutMidVol = MusicStemBlendCrossfader.Calculate(fadeOutCurrent, fadeOutTarget, 1.0f, 0.5f, 0.0f);
            // target <= 0.0001f => currentVolume * (1 - t^2)
            // 1.0 * (1 - 0.25) = 0.75
            Assert.That(fadeOutMidVol, Is.EqualTo(0.75f).Within(0.001f));
        }
    }
}
