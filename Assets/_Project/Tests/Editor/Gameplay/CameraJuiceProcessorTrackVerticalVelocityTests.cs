using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests.Editor.Gameplay
{
    public class CameraJuiceProcessorTrackVerticalVelocityTests
    {
        [Test]
        public void TrackVerticalVelocity_SetsInternalField()
        {
            var processor = new CameraJuiceProcessor();
            processor.TrackVerticalVelocity(-5f);

            var type = typeof(CameraJuiceProcessor);
            var field = type.GetField("_preLandingVerticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(-5f, field.GetValue(processor));
        }

        [Test]
        public void TrackVerticalVelocity_PositiveVelocity_SetsInternalField()
        {
            var processor = new CameraJuiceProcessor();
            processor.TrackVerticalVelocity(10f);

            var type = typeof(CameraJuiceProcessor);
            var field = type.GetField("_preLandingVerticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(10f, field.GetValue(processor));
        }

        [Test]
        public void TrackVerticalVelocity_ZeroVelocity_SetsInternalField()
        {
            var processor = new CameraJuiceProcessor();
            processor.TrackVerticalVelocity(0f);

            var type = typeof(CameraJuiceProcessor);
            var field = type.GetField("_preLandingVerticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(0f, field.GetValue(processor));
        }
    }
}
