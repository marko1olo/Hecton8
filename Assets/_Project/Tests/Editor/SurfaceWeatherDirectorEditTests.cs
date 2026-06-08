using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class SurfaceWeatherDirectorEditTests
{
    private const BindingFlags StaticPrivateFlags = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstancePrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private GameObject _host;

    [SetUp]
    public void SetUp()
    {
        ResetSurfaceWeatherRuntime();
        ResetOceanKinematicsRuntime();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
        {
            HectonSurfaceWeatherDirector director = _host.GetComponent<HectonSurfaceWeatherDirector>();
            HectonFloatingOrigin.UnregisterListener(director);
            SurfaceWeatherVfxRig rig = _host.GetComponent<SurfaceWeatherVfxRig>();
            HectonFloatingOrigin.UnregisterListener(rig);
            Object.DestroyImmediate(_host);
        }

        ResetSurfaceWeatherRuntime();
        ResetOceanKinematicsRuntime();
    }

    [Test]
    public void SurfaceWeatherDirectorDoesNotRegisterOrInitializeRuntimeInEditMode()
    {
        _host = new GameObject("SurfaceWeatherDirectorEditModeRuntimeGateTest");
        HectonSurfaceWeatherDirector director = _host.AddComponent<HectonSurfaceWeatherDirector>();

        Assert.IsNull(GlobalRegistry.SurfaceWeather, "Edit-mode surface weather must not claim GlobalRegistry.SurfaceWeather.");
        Assert.IsFalse(HectonFloatingOrigin.IsListenerRegistered(director), "Edit mode must not register surface weather as an origin-shift listener.");
        Assert.IsFalse(ReadPrivate<bool>(director, "_runtimeStateInitialized"), "Runtime weather state must stay cold outside play mode.");
        Assert.IsNull(ReadPrivate<object>(director, "_dataVault"), "Edit mode must not cache DataVault for surface weather runtime buffers.");
    }

    [Test]
    public void SurfaceWeatherVfxRigDoesNotRegisterOriginListenerInEditMode()
    {
        _host = new GameObject("SurfaceWeatherVfxRigEditModeRuntimeGateTest");
        LogAssert.Expect(
            LogType.Log,
            "[SurfaceWeatherVfxRig] Missing authored LineRenderer. Lightning presentation disabled until authoredBoltRenderer is assigned.");

        SurfaceWeatherVfxRig rig = _host.AddComponent<SurfaceWeatherVfxRig>();

        Assert.IsFalse(HectonFloatingOrigin.IsListenerRegistered(rig), "Edit-mode weather VFX must not register as an origin-shift listener.");
    }

    [Test]
    public void OceanSurfaceAtmosphereRuntimeDoesNotStartRuntimeFromEditMode()
    {
        _host = new GameObject("OceanSurfaceAtmosphereEditModeRuntimeGateTest");
        ShinobuOceanSurfaceAtmosphereRuntime runtime = _host.AddComponent<ShinobuOceanSurfaceAtmosphereRuntime>();

        Assert.IsNull(ReadGlobalRegistryPrivate<object>("_oceanKinematicsRuntime"), "Edit-mode ocean atmosphere must not create or claim OceanKinematicsRuntimeService.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_registeredOcean"), "Edit-mode ocean atmosphere must not register as an ocean provider.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_registeredUpdate"), "Edit-mode ocean atmosphere must not register into the update dispatcher.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_registeredSlow"), "Edit-mode ocean atmosphere must not register into the slow dispatcher.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_registeredLate"), "Edit-mode ocean atmosphere must not register into the late-frame dispatcher.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_vaultBuffersReady"), "Edit-mode ocean atmosphere must not hydrate DataVault buffers.");
        Assert.IsFalse(ReadPrivate<bool>(runtime, "_readbackDispatchEnabled"), "Edit-mode ocean atmosphere must keep GPU readback dispatch disabled.");
        Assert.IsNull(ReadPrivate<object>(runtime, "_waveGraphicsBufferA"), "Edit-mode ocean atmosphere must not allocate wave GraphicsBuffers.");
        Assert.IsNull(ReadPrivate<object>(runtime, "_waveSampleQueryBuffer0"), "Edit-mode ocean atmosphere must not allocate readback query GraphicsBuffers.");
    }

    [Test]
    public void SurfaceWeatherDirectorTeardownForcesPendingWeatherJobFence()
    {
        string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Atmosphere", "HectonSurfaceWeatherDirector.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");

        StringAssert.Contains("private bool DisposeWeatherMathBuffers(bool forceCompletePendingJob)", source);
        StringAssert.Contains("DisposeWeatherMathBuffers(forceCompletePendingJob: true);\n            ClearWeatherBindings();", source);
        StringAssert.Contains("DisposeWeatherMathBuffers(forceCompletePendingJob: true);\n\n        }", source);
        StringAssert.Contains("DisposeWeatherMathBuffers(forceCompletePendingJob: true);\n            _runtimeStateInitialized = false;", source);
        StringAssert.DoesNotContain("DisposeWeatherMathBuffers();", source);
        StringAssert.DoesNotContain("_weatherJobPrimed = false;\n            _weatherJobScheduled = false;\n            _weatherShaderDirty = false;", source);
    }

    [Test]
    public void SurfaceWeatherDirectorSurfaceYRejectsDivergentFluidProviderBeforeShaderWaterline()
    {
        string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Atmosphere", "HectonSurfaceWeatherDirector.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        string surfaceBody = ExtractMethodBody(source, "private float ResolveSurfaceY()");

        StringAssert.Contains("private const float DefaultSurfaceWaterLevelY = 14.02f;", source);
        StringAssert.Contains("float referenceSurfaceY = DefaultSurfaceWaterLevelY;", surfaceBody);
        StringAssert.Contains("playerMovement != null && TryResolveSurfaceY(playerMovement.CurrentWaterSurfaceY, out float playerSurfaceY)", surfaceBody);
        StringAssert.Contains("oceanKinematics != null && TryResolveSurfaceY(oceanKinematics.SeaLevel, out float oceanSurfaceY)", surfaceBody);
        StringAssert.Contains("fluidSurface != null && TryResolveSurfaceY(fluidSurface.CurrentWaterLevelY, out float fluidSurfaceY)", surfaceBody);
        StringAssert.Contains("private static bool TryResolveSurfaceY(float candidateSurfaceY, out float surfaceY)", source);
        StringAssert.Contains("math.abs(candidateSurfaceY) > 0.0001f", source);
        StringAssert.Contains("math.abs(candidateSurfaceY) <= 1000f", source);
        StringAssert.Contains("surfaceY = DefaultSurfaceWaterLevelY;", source);
        StringAssert.Contains("math.abs(fluidSurfaceY - referenceSurfaceY) <= 128f", surfaceBody);
        StringAssert.Contains("return fluidSurfaceY;", surfaceBody);
        StringAssert.Contains("return referenceSurfaceY;", surfaceBody);
        StringAssert.DoesNotContain("math.isfinite(playerMovement.CurrentWaterSurfaceY)", surfaceBody);
        StringAssert.DoesNotContain("math.isfinite(oceanKinematics.SeaLevel)", surfaceBody);
        StringAssert.DoesNotContain("math.isfinite(fluidSurface.CurrentWaterLevelY)", surfaceBody);
        StringAssert.DoesNotContain("!hasReferenceSurface ||", surfaceBody);
        StringAssert.DoesNotContain("bool hasReferenceSurface", surfaceBody);
        StringAssert.DoesNotContain("ResolveSurfaceY(followPosition)", source);
        AssertTextBefore(surfaceBody, "math.abs(fluidSurfaceY - referenceSurfaceY) <= 128f", "return fluidSurfaceY;");
    }

    [Test]
    public void SurfaceWeatherDirectorDepthUsesPlayerRuntimeSnapshotBeforeDirectMovementFallback()
    {
        string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Atmosphere", "HectonSurfaceWeatherDirector.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        string depthBody = ExtractMethodBody(source, "private float ResolveCurrentDepth()");

        StringAssert.Contains("private IPlayerRuntimeContext _playerRuntimeContext;", source);
        StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", depthBody);
        StringAssert.Contains("playerContext.IsInitialized", depthBody);
        StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", depthBody);
        StringAssert.Contains("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", depthBody);
        StringAssert.Contains("math.isfinite(movementState.DepthMeters)", depthBody);
        StringAssert.Contains("return math.max(0f, movementState.DepthMeters);", depthBody);
        StringAssert.Contains("if (playerContext != null)", depthBody);
        StringAssert.Contains("return 0f;", depthBody);
        StringAssert.Contains("playerMovement != null && math.isfinite(playerMovement.CurrentDepth)", depthBody);
        StringAssert.Contains("? math.max(0f, playerMovement.CurrentDepth)", depthBody);
        AssertTextBefore(depthBody, "playerContext.TryGetMovementRuntimeState", "playerMovement != null && math.isfinite(playerMovement.CurrentDepth)");
        AssertTextBefore(depthBody, "if (playerContext != null)", "playerMovement != null && math.isfinite(playerMovement.CurrentDepth)");
        StringAssert.DoesNotContain("return math.max(0f, playerMovement.CurrentDepth);", depthBody);
    }

    private static T ReadPrivate<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivateFlags);
        Assert.IsNotNull(field, $"Expected private field {fieldName} to exist.");
        return (T)field.GetValue(target);
    }

    private static T ReadGlobalRegistryPrivate<T>(string fieldName)
    {
        FieldInfo field = typeof(GlobalRegistry).GetField(fieldName, StaticPrivateFlags);
        Assert.IsNotNull(field, $"Expected GlobalRegistry.{fieldName} to exist.");
        return (T)field.GetValue(null);
    }

    private static void ResetSurfaceWeatherRuntime()
    {
        FieldInfo field = typeof(GlobalRegistry).GetField("_surfaceWeatherRuntime", StaticPrivateFlags);
        Assert.IsNotNull(field, "Expected GlobalRegistry._surfaceWeatherRuntime to exist.");
        field.SetValue(null, null);
    }

    private static void ResetOceanKinematicsRuntime()
    {
        FieldInfo runtimeField = typeof(GlobalRegistry).GetField("_oceanKinematicsRuntime", StaticPrivateFlags);
        Assert.IsNotNull(runtimeField, "Expected GlobalRegistry._oceanKinematicsRuntime to exist.");
        runtimeField.SetValue(null, null);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int start = source.IndexOf(signature, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(start, 0, $"Missing method signature: {signature}");
        int openBrace = source.IndexOf('{', start);
        Assert.GreaterOrEqual(openBrace, 0, $"Missing opening brace for: {signature}");
        int depth = 0;
        for (int i = openBrace; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(openBrace, i - openBrace + 1);
            }
        }

        Assert.Fail($"Missing closing brace for: {signature}");
        return string.Empty;
    }

    private static void AssertTextBefore(string text, string first, string second)
    {
        int firstIndex = text.IndexOf(first, System.StringComparison.Ordinal);
        int secondIndex = text.IndexOf(second, System.StringComparison.Ordinal);
        Assert.GreaterOrEqual(firstIndex, 0, $"Missing first marker: {first}");
        Assert.GreaterOrEqual(secondIndex, 0, $"Missing second marker: {second}");
        Assert.Less(firstIndex, secondIndex, $"Expected '{first}' before '{second}'.");
    }
}
