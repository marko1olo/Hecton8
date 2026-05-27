using System.IO;
using System.Reflection;
using Hecton8.Atmosphere;
using NUnit.Framework;
using UnityEngine;

public sealed class AtmosphereManagerEditorPreviewTests
{
    [Test]
    public void AtmosphereManagerOnEnableMarksEditorPreviewDirtyInEditMode()
    {
        GameObject host = new GameObject("AtmosphereManagerOnEnableEditModeHost");
        try
        {
            HectonAtmosphereManager manager = host.AddComponent<HectonAtmosphereManager>();
            SetField(manager, "_editorPreviewDirty", false);

            Invoke(manager, "OnEnable");

            Assert.That((bool)GetField(manager, "_editorPreviewDirty"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void AtmosphereManagerOnValidateRunsInEditMode()
    {
        GameObject host = new GameObject("AtmosphereManagerOnValidateEditModeHost");
        try
        {
            HectonAtmosphereManager manager = host.AddComponent<HectonAtmosphereManager>();
            SetField(manager, "_cycleDuration", -4f);
            SetField(manager, "_editorPreviewDirty", false);
            SetField(manager, "_editorInitialized", true);

            Invoke(manager, "OnValidate");

            Assert.That((float)GetField(manager, "_cycleDuration"), Is.EqualTo(1f));
            Assert.That((bool)GetField(manager, "_editorPreviewDirty"), Is.True);
            Assert.That((bool)GetField(manager, "_editorInitialized"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void AtmosphereManagerRuntimeBiomeRoutesUseCachedRegistryServices()
    {
        string path = Path.Combine("Assets", "_Project", "Scripts", "HectonAtmosphereManager.cs");
        string source = File.ReadAllText(path).Replace("\r\n", "\n");
        string biomeRefreshRegion = SliceBetween(
            source,
            "private void RefreshProceduralBiomeInfluenceSnapshotIfNeeded()",
            "private bool ShouldCommitProceduralBiomeInfluence");

        Assert.That(biomeRefreshRegion, Does.Not.Contain("WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler"), "Atmosphere SlowTick must not lazy-resolve procedural field sampler.");
        Assert.That(source, Does.Not.Contain("WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref _biomeMatrixDirector)"), "Atmosphere runtime must not resolve biome matrix through active-instance fallback.");
        Assert.That(source, Does.Contain("_proceduralFieldSampler = GlobalRegistry.ProceduralFieldSampler"), "Atmosphere must cache procedural field sampler from registry cold route.");
        Assert.That(source, Does.Contain("_biomeMatrixDirector = GlobalRegistry.BiomeMatrix"), "Atmosphere must cache biome matrix from registry cold route.");
        Assert.That(source, Does.Contain("case GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime:"), "Atmosphere must receive procedural field sampler hot-swap updates.");
        Assert.That(source, Does.Contain("case GlobalRegistryServiceSlot.BiomeMatrixRuntime:"), "Atmosphere must receive biome matrix hot-swap updates.");
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        field.SetValue(target, value);
    }

    private static object GetField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}' to exist.");
        return field.GetValue(target);
    }

    private static void Invoke(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected private method '{methodName}' to exist.");
        method.Invoke(target, null);
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
