using NUnit.Framework;
using Unity.Mathematics;
using System.Reflection;
using System;

public sealed class CurrentManagerEditTests
{
    private const BindingFlags StaticMethodFlags = BindingFlags.Static | BindingFlags.NonPublic;

    [Test]
    public void FastTriangleSigned_ReturnsExpectedDeterministicValues()
    {
        Type managerType = typeof(CurrentManager);
        MethodInfo method = managerType.GetMethod("FastTriangleSigned", StaticMethodFlags);
        Assert.IsNotNull(method, "Expected CurrentManager.FastTriangleSigned to exist.");

        float InvokeFastTriangle(float phase)
        {
            return (float)method.Invoke(null, new object[] { phase });
        }

        // Test determinism and specific known points of the triangle wave
        float phaseZero = InvokeFastTriangle(0f);
        Assert.That(phaseZero, Is.InRange(-1f, 1f));

        float val1 = InvokeFastTriangle(10f);
        float val2 = InvokeFastTriangle(10f);
        Assert.That(val1, Is.EqualTo(val2).Within(0.000001f), "Function is not deterministic.");

        float val3 = InvokeFastTriangle(10.1f);
        Assert.That(val1, Is.Not.EqualTo(val3), "Function should vary with phase.");

        // Output must be strictly bounded between -1 and 1
        for (float p = -100f; p <= 100f; p += 0.5f)
        {
            float val = InvokeFastTriangle(p);
            Assert.That(val, Is.GreaterThanOrEqualTo(-1f));
            Assert.That(val, Is.LessThanOrEqualTo(1f));
        }
    }

    [Test]
    public void SampleCurrent_IsDeterministicAndBoundedByStrength()
    {
        float3 pos = new float3(1f, 2f, 3f);
        float time = 5f;
        float noiseScale = 0.1f;
        float timeScale = 1f;
        float strength = 10f;
        float verticalFactor = 0.5f;

        float3 current1 = CurrentManager.SampleCurrent(pos, time, noiseScale, timeScale, strength, verticalFactor);
        float3 current2 = CurrentManager.SampleCurrent(pos, time, noiseScale, timeScale, strength, verticalFactor);

        Assert.That(current1.x, Is.EqualTo(current2.x).Within(0.000001f));
        Assert.That(current1.y, Is.EqualTo(current2.y).Within(0.000001f));
        Assert.That(current1.z, Is.EqualTo(current2.z).Within(0.000001f));

        Assert.That(math.abs(current1.x), Is.LessThanOrEqualTo(strength));
        Assert.That(math.abs(current1.z), Is.LessThanOrEqualTo(strength));
        Assert.That(math.abs(current1.y), Is.LessThanOrEqualTo(strength * verticalFactor));
    }

    [Test]
    public void SampleHorizontal_IsDeterministicAndYIsZero()
    {
        float3 pos = new float3(1f, 2f, 3f);
        float time = 5f;
        float noiseScale = 0.1f;
        float timeScale = 1f;
        float strength = 10f;

        float3 currentH1 = CurrentManager.SampleHorizontal(pos, time, noiseScale, timeScale, strength);
        float3 currentH2 = CurrentManager.SampleHorizontal(pos, time, noiseScale, timeScale, strength);

        Assert.That(currentH1.x, Is.EqualTo(currentH2.x).Within(0.000001f));
        Assert.That(currentH1.y, Is.EqualTo(0f));
        Assert.That(currentH1.z, Is.EqualTo(currentH2.z).Within(0.000001f));

        // It should match SampleCurrent's X and Z for the same parameters
        float3 currentFull = CurrentManager.SampleCurrent(pos, time, noiseScale, timeScale, strength, 1.0f);
        Assert.That(currentH1.x, Is.EqualTo(currentFull.x).Within(0.000001f));
        Assert.That(currentH1.z, Is.EqualTo(currentFull.z).Within(0.000001f));
    }
}
