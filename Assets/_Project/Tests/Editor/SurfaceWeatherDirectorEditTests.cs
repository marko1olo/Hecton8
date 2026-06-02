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
}
