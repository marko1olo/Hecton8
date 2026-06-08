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
        Color[] samples = ReadPrivate<Color[]>(director, "_noirFogLutSamples");
        Assert.IsNotNull(samples, "Noir fog LUT must use fixed sample storage, not a runtime Texture2D.");
        Assert.AreEqual(16, samples.Length, "Noir fog LUT sample count must match shader constant-buffer contract.");
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

    [Test]
    public void BiomeLutDepthRejectsInactiveBiomeMatrixAndFallsBackToPlayerSnapshot()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "Environment", "GlobalWeatherDirector.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        string resolveDependencies = ExtractMethodBody(source, "private void ResolveDependencies()");
        string hotSwap = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
        string resolveDepth = ExtractMethodBody(source, "private float ResolveCurrentBiomeDepthMeters()");

        Assert.That(source, Does.Contain("using Hecton8.World;"));
        Assert.That(source, Does.Contain("private IPlayerRuntimeContext _cachedPlayerContext;"));
        Assert.That(resolveDependencies, Does.Contain("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _cachedBiomeMatrix);"));
        Assert.That(resolveDependencies, Does.Contain("_cachedPlayerContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;"));
        Assert.That(hotSwap, Does.Contain("GlobalRegistryServiceSlot.BiomeMatrixRuntime"));
        Assert.That(hotSwap, Does.Contain("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _cachedBiomeMatrix);"));
        Assert.That(hotSwap, Does.Contain("GlobalRegistryServiceSlot.Player"));
        Assert.That(resolveDepth, Does.Contain("biomeMatrix == null || !biomeMatrix.isActiveAndEnabled"));
        Assert.That(resolveDepth, Does.Contain("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrix);"));
        Assert.That(resolveDepth, Does.Contain("biomeMatrix.isActiveAndEnabled"));
        Assert.That(resolveDepth, Does.Contain("math.isfinite(biomeMatrix.CurrentDepthMeters)"));
        Assert.That(resolveDepth, Does.Contain("playerContext.IsInitialized"));
        Assert.That(resolveDepth, Does.Contain("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)"));
        Assert.That(resolveDepth, Does.Contain("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u"));
        Assert.That(resolveDepth, Does.Contain("return math.max(0f, movementState.DepthMeters);"));
        Assert.That(resolveDepth, Does.Not.Contain("return biomeMatrix != null ? math.max(0f, biomeMatrix.CurrentDepthMeters) : 0f;"));
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

    private static string ExtractMethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(start, 0, $"Missing method: {signature}");
        int brace = source.IndexOf('{', start);
        Assert.GreaterOrEqual(brace, 0, $"Missing method body: {signature}");

        int depth = 0;
        for (int i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(brace, i - brace + 1);
            }
        }

        Assert.Fail("Could not extract method body for " + signature);
        return string.Empty;
    }
}
