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

    [Test]
    public void SampleCurrent_EdgeCases_HandledCorrectly()
    {
        float3 pos = new float3(1f, 2f, 3f);
        float time = 5f;

        // Edge Case 1: Zero strength should result in zero current
        float3 zeroStrength = CurrentManager.SampleCurrent(pos, time, noiseScale: 1f, timeScale: 1f, strength: 0f, verticalFactor: 1f);
        Assert.That(zeroStrength.x, Is.EqualTo(0f));
        Assert.That(zeroStrength.y, Is.EqualTo(0f));
        Assert.That(zeroStrength.z, Is.EqualTo(0f));

        // Edge Case 2: Zero vertical factor should result in zero Y current
        float3 zeroVertical = CurrentManager.SampleCurrent(pos, time, noiseScale: 1f, timeScale: 1f, strength: 10f, verticalFactor: 0f);
        Assert.That(zeroVertical.y, Is.EqualTo(0f));

        // Edge Case 3: Zero noise scale makes current homogeneous across space
        float3 pos1 = new float3(10f, 20f, 30f);
        float3 pos2 = new float3(-50f, 100f, -200f);
        float3 homogeneous1 = CurrentManager.SampleCurrent(pos1, time, noiseScale: 0f, timeScale: 1f, strength: 10f, verticalFactor: 1f);
        float3 homogeneous2 = CurrentManager.SampleCurrent(pos2, time, noiseScale: 0f, timeScale: 1f, strength: 10f, verticalFactor: 1f);
        Assert.That(homogeneous1.x, Is.EqualTo(homogeneous2.x).Within(0.000001f));
        Assert.That(homogeneous1.y, Is.EqualTo(homogeneous2.y).Within(0.000001f));
        Assert.That(homogeneous1.z, Is.EqualTo(homogeneous2.z).Within(0.000001f));

        // Edge Case 4: Negative noise scale still produces bounded results
        float3 negativeNoise = CurrentManager.SampleCurrent(pos, time, noiseScale: -1f, timeScale: 1f, strength: 10f, verticalFactor: 1f);
        Assert.That(math.abs(negativeNoise.x), Is.LessThanOrEqualTo(10f));
        Assert.That(math.abs(negativeNoise.y), Is.LessThanOrEqualTo(10f));
        Assert.That(math.abs(negativeNoise.z), Is.LessThanOrEqualTo(10f));

        // Edge Case 5: Zero time scale makes current static over time
        float3 static1 = CurrentManager.SampleCurrent(pos, time: 10f, noiseScale: 1f, timeScale: 0f, strength: 10f, verticalFactor: 1f);
        float3 static2 = CurrentManager.SampleCurrent(pos, time: 50f, noiseScale: 1f, timeScale: 0f, strength: 10f, verticalFactor: 1f);
        Assert.That(static1.x, Is.EqualTo(static2.x).Within(0.000001f));
        Assert.That(static1.y, Is.EqualTo(static2.y).Within(0.000001f));
        Assert.That(static1.z, Is.EqualTo(static2.z).Within(0.000001f));
    }

    [Test]
    public void SampleHorizontal_EdgeCases_HandledCorrectly()
    {
        float3 pos = new float3(1f, 2f, 3f);
        float time = 5f;

        // Zero strength
        float3 zeroStrength = CurrentManager.SampleHorizontal(pos, time, noiseScale: 1f, timeScale: 1f, strength: 0f);
        Assert.That(zeroStrength.x, Is.EqualTo(0f));
        Assert.That(zeroStrength.y, Is.EqualTo(0f));
        Assert.That(zeroStrength.z, Is.EqualTo(0f));

        // Zero noise scale
        float3 pos1 = new float3(10f, 20f, 30f);
        float3 pos2 = new float3(-50f, 100f, -200f);
        float3 homogeneous1 = CurrentManager.SampleHorizontal(pos1, time, noiseScale: 0f, timeScale: 1f, strength: 10f);
        float3 homogeneous2 = CurrentManager.SampleHorizontal(pos2, time, noiseScale: 0f, timeScale: 1f, strength: 10f);
        Assert.That(homogeneous1.x, Is.EqualTo(homogeneous2.x).Within(0.000001f));
        Assert.That(homogeneous1.y, Is.EqualTo(0f));
        Assert.That(homogeneous1.z, Is.EqualTo(homogeneous2.z).Within(0.000001f));

        // Negative noise scale
        float3 negativeNoise = CurrentManager.SampleHorizontal(pos, time, noiseScale: -1f, timeScale: 1f, strength: 10f);
        Assert.That(math.abs(negativeNoise.x), Is.LessThanOrEqualTo(10f));
        Assert.That(math.abs(negativeNoise.y), Is.EqualTo(0f));
        Assert.That(math.abs(negativeNoise.z), Is.LessThanOrEqualTo(10f));

        // Zero time scale
        float3 static1 = CurrentManager.SampleHorizontal(pos, time: 10f, noiseScale: 1f, timeScale: 0f, strength: 10f);
        float3 static2 = CurrentManager.SampleHorizontal(pos, time: 50f, noiseScale: 1f, timeScale: 0f, strength: 10f);
        Assert.That(static1.x, Is.EqualTo(static2.x).Within(0.000001f));
        Assert.That(static1.y, Is.EqualTo(0f));
        Assert.That(static1.z, Is.EqualTo(static2.z).Within(0.000001f));
    }
}
