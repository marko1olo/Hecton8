using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using NUnit.Framework;
using UnityEngine;

public sealed class StormPropagationRuntimeEditTests
{
    [Test]
    public void StormPropagationRuntimeDoesNotClaimRuntimeInEditMode()
    {
        FieldInfo claimField = typeof(ShinobuStormPropagationRuntime).GetField(
            "s_runtimeClaimed",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(claimField);
        claimField.SetValue(null, 0);

        GameObject host = new GameObject("StormPropagationEditModeHost");
        try
        {
            host.AddComponent<ShinobuStormPropagationRuntime>();

            Assert.That((int)claimField.GetValue(null), Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(host);
            claimField.SetValue(null, 0);
        }
    }

    [Test]
    public void StormPropagationRuntimeSeaLevelFallbackMatchesProductionWaterline()
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Assets",
            "_Project",
            "Scripts",
            "Atmosphere",
            "StormPropagation",
            "ShinobuStormPropagationRuntime.cs");
        string source = File.ReadAllText(path);

        Assert.That(source, Does.Contain("private const double DefaultSeaLevelLocalY = 14.02d;"));
        Assert.That(source, Does.Contain("[SerializeField] private double seaLevelAupY = DefaultSeaLevelLocalY;"));
        Assert.That(source, Does.Contain("float seaLevelLocal = ResolveSeaLevelLocalY(seaLevelAupY);"));
        Assert.That(source, Does.Contain("seaLevelLocal = ResolveSeaLevelLocalY(weather.SurfaceScalars.x);"));
        Assert.That(source, Does.Contain("private static float ResolveSeaLevelLocalY(double value)"));
        Assert.That(source, Does.Contain("math.abs(value) <= 1000d"));
        Assert.That(source, Does.Not.Contain("? (float)seaLevelAupY : 0f"));
    }
}
