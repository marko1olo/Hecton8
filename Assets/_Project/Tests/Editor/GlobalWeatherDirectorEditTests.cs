using System.IO;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Environment;
using NUnit.Framework;
using UnityEngine;

public sealed class GlobalWeatherDirectorEditTests
{
    private const BindingFlags StaticPrivateFlags = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstancePrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject _host;

    [SetUp]
    public void SetUp()
    {
        ResetWeatherService();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
            Object.DestroyImmediate(_host);

        ResetWeatherService();
    }

    [Test]
    public void GlobalWeatherDirectorDoesNotRegisterOrInitializeRuntimeInEditMode()
    {
        _host = new GameObject("GlobalWeatherDirectorEditModeTest");
        GlobalWeatherDirector director = _host.AddComponent<GlobalWeatherDirector>();

        Assert.IsNull(GlobalRegistry.Weather, "Edit-mode weather component must not claim GlobalRegistry.Weather.");
        Assert.IsFalse(director.IsInitialized, "Runtime weather state must stay cold outside play mode.");
        Assert.IsNull(ReadPrivate<Texture2D>(director, "_noirFogLutTexture"), "Edit mode must not allocate runtime noir fog LUT texture.");
    }

    [Test]
    public void AtmosphericBridgePublishUsesCachedCelestialEngineSnapshot()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "Environment", "GlobalWeatherDirector.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        string bridgeRegion = SliceBetween(
            source,
            "private void PublishAtmosphericBridgeShaderState()",
            "private static void ClearAtmosphericBridgeShaderState()");

        Assert.That(bridgeRegion, Does.Not.Contain("GlobalRegistry.CelestialRuntimeSnapshot"), "Atmospheric shader bridge must not hot-poll global celestial snapshot.");
        Assert.That(bridgeRegion, Does.Contain("ReadCachedCelestialRuntimeSnapshot()"), "Atmospheric shader bridge must read the cached celestial owner snapshot.");
        Assert.That(source, Does.Contain("private HectonCelestialEngine _cachedCelestialEngine;"), "Weather director must cache the celestial engine cold dependency.");
        Assert.That(source, Does.Contain("GlobalRegistryServiceSlot.CelestialEngineRuntime"), "Weather director must receive celestial hot-swap updates.");
    }

    private static T ReadPrivate<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivateFlags);
        Assert.IsNotNull(field, $"Expected private field {fieldName} to exist.");
        return (T)field.GetValue(target);
    }

    private static void ResetWeatherService()
    {
        FieldInfo field = typeof(GlobalRegistry).GetField("_weather", StaticPrivateFlags);
        Assert.IsNotNull(field, "Expected GlobalRegistry._weather to exist.");
        field.SetValue(null, null);
    }

    private static string SliceBetween(string source, string startToken, string endToken)
    {
        int start = source.IndexOf(startToken, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(start, 0, $"Missing start token: {startToken}");
        int end = source.IndexOf(endToken, start, System.StringComparison.Ordinal);
        Assert.Greater(end, start, $"Missing end token: {endToken}");
        return source.Substring(start, end - start);
    }
}
