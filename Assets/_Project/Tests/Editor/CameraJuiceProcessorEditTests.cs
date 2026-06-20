using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    public sealed class CameraJuiceProcessorEditTests
    {
        private CameraJuiceProcessor _processor;
        private FieldInfo _splashDipCurrentField;
        private FieldInfo _splashDipVelocityField;

        [SetUp]
        public void Setup()
        {
            _processor = new CameraJuiceProcessor();

            // Reflection to access private fields
            var type = typeof(CameraJuiceProcessor);
            _splashDipCurrentField = type.GetField("_splashDipCurrent", BindingFlags.NonPublic | BindingFlags.Instance);
            _splashDipVelocityField = type.GetField("_splashDipVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(_splashDipCurrentField, "Failed to find _splashDipCurrent field.");
            Assert.IsNotNull(_splashDipVelocityField, "Failed to find _splashDipVelocity field.");
        }

        [Test]
        public void RegisterSplash_WithNullSuit_DoesNothing()
        {
            // Arrange
            float initialDip = 5f;
            float initialVelocity = 10f;

            _splashDipCurrentField.SetValue(_processor, initialDip);
            _splashDipVelocityField.SetValue(_processor, initialVelocity);

            // Act
            _processor.RegisterSplash(1.0f, null);

            // Assert
            float currentDip = (float)_splashDipCurrentField.GetValue(_processor);
            float currentVelocity = (float)_splashDipVelocityField.GetValue(_processor);

            Assert.AreEqual(initialDip, currentDip, "Dip should not change when suit is null.");
            Assert.AreEqual(initialVelocity, currentVelocity, "Velocity should not change when suit is null.");
        }

        [Test]
        public void RegisterSplash_WithValidSuit_CalculatesCorrectDipAndVelocity()
        {
            // Arrange
            float testIntensity = 2.5f;
            float expectedSplashCameraDip = 0.5f;

            SuitData mockSuit = ScriptableObject.CreateInstance<SuitData>();
            mockSuit.splashCameraDip = expectedSplashCameraDip;

            // _splashDipCurrent = -intensity * suit.splashCameraDip
            float expectedDip = -testIntensity * expectedSplashCameraDip;
            // _splashDipVelocity = -dip * 2f
            float expectedVelocity = -expectedDip * 2f;

            // Act
            _processor.RegisterSplash(testIntensity, mockSuit);

            // Assert
            float currentDip = (float)_splashDipCurrentField.GetValue(_processor);
            float currentVelocity = (float)_splashDipVelocityField.GetValue(_processor);

            Assert.AreEqual(expectedDip, currentDip, 0.0001f, "Calculated _splashDipCurrent is incorrect.");
            Assert.AreEqual(expectedVelocity, currentVelocity, 0.0001f, "Calculated _splashDipVelocity is incorrect.");

            // Clean up
            UnityEngine.Object.DestroyImmediate(mockSuit);
        }
    }
}