using Hecton8.Celestial;
using NUnit.Framework;
using Unity.Mathematics;

public sealed class OrbitalSkyEphemerisDrift1601EditTests
{
    [Test]
    public void AnalyticalSmokeEphemeris_IsDeterministicFiniteAndDrifts()
    {
        const uint seed = 0x1601u;
        const double time = 4096.0d;
        Hecton8.Core.CelestialRuntimeSnapshot first = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(time, seed);
        Hecton8.Core.CelestialRuntimeSnapshot repeat = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(time, seed);
        Hecton8.Core.CelestialRuntimeSnapshot later = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(time + 300.0d, seed);

        AssertSnapshotDeterministic(first, repeat);
        AssertFiniteUnit(first.SunDirection, nameof(first.SunDirection));
        AssertFiniteUnit(first.GasGiantDirection, nameof(first.GasGiantDirection));
        AssertFiniteUnit(later.GasGiantDirection, nameof(later.GasGiantDirection));
        Assert.That(first.EclipseOcclusion01, Is.InRange(0f, 1f));
        Assert.That(later.EclipseOcclusion01, Is.InRange(0f, 1f));
        Assert.That(math.distance(first.GasGiantDirection, later.GasGiantDirection), Is.GreaterThan(0.0001f));
    }

    private static void AssertSnapshotDeterministic(
        Hecton8.Core.CelestialRuntimeSnapshot expected,
        Hecton8.Core.CelestialRuntimeSnapshot actual)
    {
        Assert.That(actual.AbsoluteUniverseTime, Is.EqualTo(expected.AbsoluteUniverseTime));
        Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence));
        Assert.That(actual.Flags, Is.EqualTo(expected.Flags));
        Assert.That(actual.EclipseOcclusion01, Is.EqualTo(expected.EclipseOcclusion01));
        Assert.That(actual.GasGiantDirection.x, Is.EqualTo(expected.GasGiantDirection.x));
        Assert.That(actual.GasGiantDirection.y, Is.EqualTo(expected.GasGiantDirection.y));
        Assert.That(actual.GasGiantDirection.z, Is.EqualTo(expected.GasGiantDirection.z));
    }

    private static void AssertFiniteUnit(float3 value, string label)
    {
        Assert.That(math.all(math.isfinite(value)), Is.True, label);
        Assert.That(math.length(value), Is.EqualTo(1f).Within(0.0005f), label);
    }
}
