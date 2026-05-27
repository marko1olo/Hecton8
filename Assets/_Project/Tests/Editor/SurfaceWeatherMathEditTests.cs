using System;
using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Core.Contracts;
using NUnit.Framework;

public sealed class SurfaceWeatherMathEditTests
{
    private const BindingFlags StaticMethodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    [Test]
    public void ThunderDelayUsesAuthoredScaleAndClamp()
    {
        float soundSpeed = HectonPhysicsContract.SoundSpeedAirMetersPerSecondConst;

        float nearDelay = InvokeSurfaceThunderFloat(
            "ResolveThunderDelaySeconds",
            30f,
            soundSpeed,
            0.7f,
            2.2f,
            3f);
        Assert.That(nearDelay, Is.EqualTo(0.7f).Within(0.0001f));

        float midDistance = 150f;
        float midDelay = InvokeSurfaceThunderFloat(
            "ResolveThunderDelaySeconds",
            midDistance,
            soundSpeed,
            0.7f,
            2.2f,
            3f);
        Assert.That(midDelay, Is.EqualTo((midDistance * 3f) / soundSpeed).Within(0.0001f));

        float farDelay = InvokeSurfaceThunderFloat(
            "ResolveThunderDelaySeconds",
            300f,
            soundSpeed,
            0.7f,
            2.2f,
            3f);
        Assert.That(farDelay, Is.EqualTo(2.2f).Within(0.0001f));
    }

    [Test]
    public void LightningFlashDurationUsesAuthoredDurationWithSafeFallback()
    {
        float authoredDuration = InvokeSurfaceThunderFloat("ResolveLightningFlashDurationSeconds", 0.16f);
        Assert.That(authoredDuration, Is.EqualTo(0.16f).Within(0.0001f));

        float invalidDuration = InvokeSurfaceThunderFloat("ResolveLightningFlashDurationSeconds", float.NaN);
        Assert.That(invalidDuration, Is.EqualTo(0.1f).Within(0.0001f));

        float overlongDuration = InvokeSurfaceThunderFloat("ResolveLightningFlashDurationSeconds", 4f);
        Assert.That(overlongDuration, Is.EqualTo(0.5f).Within(0.0001f));
    }

    private static float InvokeSurfaceThunderFloat(string methodName, params object[] args)
    {
        Type helperType = typeof(HectonSurfaceWeatherDirector).Assembly.GetType(
            "Hecton8.Atmosphere.SurfaceThunderMath",
            throwOnError: true);
        MethodInfo method = helperType.GetMethod(methodName, StaticMethodFlags);
        Assert.IsNotNull(method, $"Expected SurfaceThunderMath.{methodName} to exist.");
        return (float)method.Invoke(null, args);
    }
}
