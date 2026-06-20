using System;
using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Gameplay;

public class CameraJuiceProcessorEditTests
{
    [Test]
    public void TrackVerticalVelocity_SetsInternalField()
    {
        // Arrange
        var processor = new CameraJuiceProcessor();
        var fieldInfo = typeof(CameraJuiceProcessor).GetField("_preLandingVerticalVelocity", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        processor.TrackVerticalVelocity(-15.2f);

        // Assert
        float storedValue = (float)fieldInfo.GetValue(processor);
        Assert.AreEqual(-15.2f, storedValue, "TrackVerticalVelocity should set the internal _preLandingVerticalVelocity field.");
    }
}
