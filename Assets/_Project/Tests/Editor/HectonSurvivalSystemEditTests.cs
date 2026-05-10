using System;
using System.Reflection;
using Hecton8.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HectonSurvivalSystemEditTests
{
    private const string SurvivalDatabaseRuntimePath = "Assets/_Project/Data/Survival/SurvivalDatabaseRuntime.txt";
    private const string MicroSubPresetPath = "Assets/_Project/Data/Transport/TransportPreset_MicroSub.asset";

    [Test]
    public void MultiplicativeOxygenDrain_UsesExactPressureMovementStressLeakProduct()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveMultiplicativeOxygenDrain",
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float));

        object result = method.Invoke(null, new object[] { 1.5f, 1.2f, 1.1f, 1.5f, 1.05f, 1f });

        Assert.That((float)result, Is.EqualTo(3.1185f).Within(0.0001f));
    }

    [Test]
    public void ExponentialTemperatureStep_FollowsNewtonCoolingCurve()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveExponentialTemperatureStep",
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float));

        const float environmentTemperature = -25f;
        const float startingInternalTemperature = 20f;
        const float deltaTime = 5f;
        const float tau = 45f;
        float expected =
            environmentTemperature +
            (startingInternalTemperature - environmentTemperature) * Mathf.Exp(-deltaTime / tau);

        object result = method.Invoke(
            null,
            new object[] { environmentTemperature, startingInternalTemperature, deltaTime, tau });

        Assert.That((float)result, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void OverpressureSeverity_UsesSafeDepthNormalizedCarrier()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveOverpressureSeverity01",
            typeof(float),
            typeof(float));

        object result = method.Invoke(null, new object[] { 35f, 100f });

        Assert.That((float)result, Is.EqualTo(35f / 150f).Within(0.0001f));
    }

    [Test]
    public void PressureDamageScale_FloorBlocksTransportImmunity()
    {
        PlayerTransportPreset preset = ScriptableObject.CreateInstance<PlayerTransportPreset>();
        try
        {
            SetPrivateField(preset, "pressureDamageScale", 0f);
            Assert.That(preset.PressureDamageScale, Is.EqualTo(0.25f).Within(0.0001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(preset);
        }
    }

    [Test]
    public void MicroSubPreset_ExplicitlyKeepsPressureTransferAboveZero()
    {
        PlayerTransportPreset preset = AssetDatabase.LoadAssetAtPath<PlayerTransportPreset>(MicroSubPresetPath);
        Assert.IsNotNull(preset, "MicroSub preset asset must exist for pressure floor verification.");
        Assert.That(preset.PressureDamageScale, Is.EqualTo(0.25f).Within(0.0001f));
    }

    [Test]
    public void RuntimeSurvivalDatabase_ParsesAll220RowsIntoFlatRecords()
    {
        TextAsset runtimeAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SurvivalDatabaseRuntimePath);
        Assert.IsNotNull(runtimeAsset, "Runtime survival database asset must exist.");
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "TryParseSurvivalDatabase",
            typeof(string),
            typeof(SurvivalDatabaseItemParameters[]).MakeByRefType(),
            typeof(System.Collections.Generic.Dictionary<string, int>).MakeByRefType());

        object[] args = { runtimeAsset.text, null, null };
        object result = method.Invoke(null, args);

        Assert.That((bool)result, Is.True);
        SurvivalDatabaseItemParameters[] rows = args[1] as SurvivalDatabaseItemParameters[];
        Assert.IsNotNull(rows);
        Assert.That(rows.Length, Is.EqualTo(220));
        Assert.IsNotNull(args[2]);

        SurvivalDatabaseItemParameters firstRow = rows[0];
        Assert.That(firstRow.StableHash, Is.EqualTo(0x59F4F85Fu));
        Assert.That(firstRow.MassKilograms, Is.EqualTo(2.40f).Within(0.0001f));
        Assert.That(firstRow.VolumeLiters, Is.EqualTo(1.00f).Within(0.0001f));
        Assert.That(firstRow.BaseDurability, Is.EqualTo(36));
    }

    private static MethodInfo GetPrivateStaticMethod(Type ownerType, string methodName, params Type[] parameterTypes)
    {
        MethodInfo method = ownerType.GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            parameterTypes,
            null);
        Assert.IsNotNull(method, $"Expected private static method {ownerType.Name}.{methodName}.");
        return method;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected private field {target.GetType().Name}.{fieldName}.");
        field.SetValue(target, value);
    }

    private static object GetFieldValue(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Expected field {target.GetType().Name}.{fieldName}.");
        return field.GetValue(target);
    }
}
