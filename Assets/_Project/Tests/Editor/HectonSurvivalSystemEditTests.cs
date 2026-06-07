using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HectonSurvivalSystemEditTests
{
    private const string SurvivalDatabaseRuntimePath = "Assets/_Project/Data/Survival/SurvivalDatabaseRuntime.txt";
    private const string MicroSubPresetPath = "Assets/_Project/Data/Transport/TransportPreset_MicroSub.asset";
    private const string GlobalTelemetryBusBlackboxPath = "Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs";
    private const string BlackboxXRayViewerPath = "Assets/_Project/Scripts/Editor/BlackboxXRayViewer.cs";
    private const string TelemetryDumpValidatorWindowPath = "Assets/_Project/Scripts/Editor/TelemetryDumpValidatorWindow.cs";

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
    public void DaltonPressureSolver_SumsOxygenCarbonDioxideNitrogenAndWaterVapor()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(SubmarineAtmosphereSystem),
            "ResolveDaltonPressureKPa",
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType());

        object[] args =
        {
            100f,
            0.04f,
            79.006f,
            0f,
            10f,
            10f,
            20f,
            101.325f,
            400f,
            20f,
            100f,
            null,
            null,
            null
        };

        float pressure = (float)method.Invoke(null, args);

        Assert.That(pressure, Is.EqualTo(101.325f).Within(0.01f));
        Assert.That((float)args[11], Is.EqualTo(21.23f).Within(0.02f));
        Assert.That((float)args[12], Is.EqualTo(0.0405f).Within(0.001f));
        Assert.That((float)args[13], Is.EqualTo(80.05f).Within(0.03f));
    }

    [Test]
    public void DaltonPressureSolver_CompressesGasWhenFloodVolumeReducesHeadspace()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(SubmarineAtmosphereSystem),
            "ResolveDaltonPressureKPa",
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType());

        object[] args =
        {
            100f,
            0.04f,
            79.006f,
            0f,
            10f,
            5f,
            20f,
            101.325f,
            400f,
            20f,
            100f,
            null,
            null,
            null
        };

        float pressure = (float)method.Invoke(null, args);

        Assert.That(pressure, Is.EqualTo(202.65f).Within(0.03f));
        Assert.That((float)args[11], Is.EqualTo(42.46f).Within(0.04f));
        Assert.That((float)args[12], Is.EqualTo(0.081f).Within(0.002f));
        Assert.That((float)args[13], Is.EqualTo(160.10f).Within(0.06f));
    }

    [Test]
    public void LegacyNitrogenBuildUpDelta_RemainsDisabledUnderShinobuAuthority()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveNitrogenBuildUpDelta",
            typeof(float),
            typeof(float),
            typeof(float));

        object result = method.Invoke(null, new object[] { 12f, 600f, 1f });

        Assert.That((float)result, Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ImmediateDecompressionGate_RemainsDisabledUnderShinobuAuthority()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ShouldApplyImmediateDecompressionDamage",
            typeof(float),
            typeof(float));

        Assert.That((bool)method.Invoke(null, new object[] { 10.1f, 100.1f }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { 9.9f, 100.1f }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { 10.1f, 99.9f }), Is.False);
    }

    [Test]
    public void LegacyBendsDamageGate_RemainsDisabledUnderShinobuAuthority()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ShouldApplyBendsDamage",
            typeof(float),
            typeof(float));

        Assert.That((bool)method.Invoke(null, new object[] { 160f, 160f }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { 0f, 160f }), Is.False);
    }

    [Test]
    public void SurvivalBlackboxSnapshot_UsesStableSixtyFourByteLayout()
    {
        Type type = ResolveType("Hecton8.Gameplay.SurvivalBlackboxSnapshot");

        Assert.That(Marshal.SizeOf(type), Is.EqualTo(64));
        AssertOffset(type, "SourceHash", 0);
        AssertOffset(type, "FrameIndex", 4);
        AssertOffset(type, "PlayerEntityHash", 8);
        AssertOffset(type, "Oxygen01", 12);
        AssertOffset(type, "PressureAtm", 24);
        AssertOffset(type, "DecompressionRisk01", 48);
        AssertOffset(type, "StatusMask", 56);
        AssertOffset(type, "Flags", 60);
    }

    [Test]
    public void BlackboxEditorSourcePayload_UsesStableSixtyFourByteLayout()
    {
        Type type = ResolveType("Hecton8.Core.GlobalTelemetryBus+BlackboxEditorSourcePayload");

        Assert.That(Marshal.SizeOf(type), Is.EqualTo(64));
        AssertOffset(type, "Payload0", 0);
        AssertOffset(type, "Payload1", 8);
        AssertOffset(type, "Payload2", 16);
        AssertOffset(type, "Payload3", 24);
        AssertOffset(type, "Payload4", 32);
        AssertOffset(type, "Payload5", 40);
        AssertOffset(type, "Payload6", 48);
        AssertOffset(type, "Payload7", 56);
    }

    [Test]
    public void BlackboxEditorSourceDescriptor_UsesStableSixtyFourByteLayout()
    {
        Type type = ResolveType("Hecton8.Core.GlobalTelemetryBus+BlackboxEditorSourceDescriptor");

        Assert.That(Marshal.SizeOf(type), Is.EqualTo(64));
        AssertOffset(type, "SourceHash", 0);
        AssertOffset(type, "Flags", 4);
        AssertOffset(type, "PayloadBytes", 8);
        AssertOffset(type, "Slot", 12);
    }

    [Test]
    public void BlackboxEditorSourceCopyApis_ExposeSixtyFourByteSlots()
    {
        Type payloadType = ResolveType("Hecton8.Core.GlobalTelemetryBus+BlackboxEditorSourcePayload");
        Type descriptorType = ResolveType("Hecton8.Core.GlobalTelemetryBus+BlackboxEditorSourceDescriptor");
        MethodInfo payloadMethod = typeof(GlobalTelemetryBus).GetMethod(
            "CopyNewestBlackboxEditorSourcePayloads",
            BindingFlags.Public | BindingFlags.Static);
        MethodInfo descriptorMethod = typeof(GlobalTelemetryBus).GetMethod(
            "CopyBlackboxEditorSourceDescriptors",
            BindingFlags.Public | BindingFlags.Static);

        AssertCopyMethod(payloadMethod, payloadType);
        AssertCopyMethod(descriptorMethod, descriptorType);
        Assert.That(GlobalTelemetryBus.ShinobuBlackboxSourceCapacity, Is.EqualTo(50));
        Assert.That(GlobalTelemetryBus.ShinobuBlackboxSourcePayloadBytes, Is.EqualTo(64));
    }

    [Test]
    public void GlobalTelemetryBlackboxDumpHeader_SourcePublishesSourceDescriptorTable()
    {
        string source = File.ReadAllText(GlobalTelemetryBusBlackboxPath);

        Assert.That(source, Does.Contain("private const uint BlackboxDumpVersion = 2u;"));
        Assert.That(source, Does.Contain("private const int BlackboxDumpSourceDescriptorMetadataIndex = 32;"));
        Assert.That(source, Does.Contain("private const int BlackboxDumpSourceDescriptorUIntStride = 4;"));
        Assert.That(source, Does.Contain("metadata[19] = unchecked((uint)BlackboxDumpSourceDescriptorMetadataIndex);"));
        Assert.That(source, Does.Contain("metadata[20] = unchecked((uint)BlackboxDumpSourceDescriptorUIntStride);"));
        Assert.That(source, Does.Contain("metadata[21] = unchecked((uint)BlackboxMaxSourceCount);"));
        Assert.That(source, Does.Contain("WriteBlackboxDumpSourceDescriptorMetadata(metadata);"));
        Assert.That(source, Does.Contain("metadata[cursor] = source.SourceHash;"));
        Assert.That(source, Does.Contain("metadata[cursor + 1] = source.Flags;"));
        Assert.That(source, Does.Contain("metadata[cursor + 2] = unchecked((uint)source.PayloadBytes);"));
        Assert.That(source, Does.Contain("metadata[cursor + 3] = unchecked((uint)i);"));
    }

    [Test]
    public void GlobalTelemetryBlackboxSources_ClampVolatileCountToResolvedBufferLength()
    {
        string source = File.ReadAllText(GlobalTelemetryBusBlackboxPath);

        Assert.That(source, Does.Contain("int sourceCapacity = math.min(BlackboxMaxSourceCount, sources.Length);"));
        Assert.That(source, Does.Contain("int count = math.min(math.max(0, _blackboxSourceCount), sourceCapacity);"));
        Assert.That(source, Does.Contain("if (count >= sourceCapacity)"));
        Assert.That(source, Does.Contain("math.min(BlackboxMaxSourceCount, sources.Length),"));
        Assert.That(source, Does.Contain("int sourceCount = math.min(math.max(0, Volatile.Read(ref _blackboxSourceCount)), sourceCapacity);"));
        Assert.That(source, Does.Contain("if (!TryReadBlackboxFrameBounds(out int validFrames, out int activeFrames, out int writeIndex))"));
        Assert.That(source, Does.Contain("if (validFrames >= activeFrames)"));
        Assert.That(source, Does.Contain("newestSlot = validFrames - 1;"));
    }

    [Test]
    public void TelemetryDumpValidator_SourceRecognizesGlobalTelemetryBlackboxDumps()
    {
        string source = File.ReadAllText(TelemetryDumpValidatorWindowPath);

        Assert.That(source, Does.Contain("private const uint GlobalTelemetryDumpMagic = 0x4838444Du;"));
        Assert.That(source, Does.Contain("uint metadataMagic = bytes.Length >= GlobalTelemetryMetadataOffset + 4"));
        Assert.That(source, Does.Contain("metadataMagic == GlobalTelemetryDumpMagic"));
        Assert.That(source, Does.Contain("layoutName = \"global-telemetry-blackbox\";"));
        Assert.That(source, Does.Contain("AppendGlobalTelemetrySourceDescriptorRows("));
        Assert.That(source, Does.Contain("BuildGlobalTelemetryFrameLine("));
        Assert.That(source, Does.Contain("GlobalTelemetrySourcePayloadOffsetBytes"));
        Assert.That(source, Does.Contain("int globalSourcePayloadOffsetBytes = GlobalTelemetrySourcePayloadOffsetBytes;"));
        Assert.That(source, Does.Contain("globalSourcePayloadOffsetBytes = globalSourcePayloadOffset > 0u"));
        Assert.That(source, Does.Contain("private const uint GlobalTelemetrySurvivalSourceHash = 0x53555256u;"));
        Assert.That(source, Does.Contain("TryReadGlobalTelemetrySourceDescriptor("));
        Assert.That(source, Does.Contain("ResolveGlobalTelemetrySourceSlot("));
        Assert.That(source, Does.Contain("AppendGlobalTelemetrySurvivalPayload("));
        Assert.That(source, Does.Contain("builder.Append(\" | survSlot=\")"));
        Assert.That(source, Does.Contain("builder.Append(\" name=SURV\")"));
        Assert.That(source, Does.Contain("AppendGlobalTelemetrySurvivalPayload(builder, entry, survivalSourceSlot, sourcePayloadOffsetBytes);"));
        Assert.That(source, Does.Contain("ReadU32(entry, payloadOffset) != GlobalTelemetrySurvivalSourceHash"));
        Assert.That(source, Does.Contain("private const int GlobalTelemetrySurvivalDeathCauseShift = 24;"));
        Assert.That(source, Does.Contain("uint flags = ReadU32(entry, payloadOffset + 60);"));
        Assert.That(source, Does.Contain("ResolveSurvivalDeathCauseLabel(flags)"));
        Assert.That(source, Does.Contain("builder.Append(\" o2=\")"));
        Assert.That(source, Does.Contain("builder.Append(\" deco=\")"));
        Assert.That(source, Does.Contain("builder.Append(\" death=\")"));
    }

    [Test]
    public void BlackboxXRayViewer_SourceDecodesSurvivalDeathCause()
    {
        string source = File.ReadAllText(BlackboxXRayViewerPath);

        Assert.That(source, Does.Contain("private const int SurvivalDeathCauseShift = 24;"));
        Assert.That(source, Does.Contain("ResolveSurvivalDeathCauseLabel(flags)"));
        Assert.That(source, Does.Contain("case 2u:"));
        Assert.That(source, Does.Contain("return \"pressure\";"));
        Assert.That(source, Does.Contain("\" death \""));
    }

    [Test]
    public void CrushDepthAccelerationDamage_FollowsPowerOnePointFive()
    {
        MethodInfo method = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveCrushDepthAccelerationDamage",
            typeof(float));

        object result = method.Invoke(null, new object[] { 16f });

        Assert.That((float)result, Is.EqualTo(64f).Within(0.0001f));
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

    private static Type ResolveType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(typeName, throwOnError: false);
            if (type != null)
                return type;
        }

        Assert.Fail($"Expected type {typeName}.");
        return typeof(void);
    }

    private static void AssertOffset(Type type, string fieldName, int expectedOffset)
    {
        Assert.That(Marshal.OffsetOf(type, fieldName).ToInt32(), Is.EqualTo(expectedOffset));
    }

    private static void AssertCopyMethod(MethodInfo method, Type elementType)
    {
        Assert.IsNotNull(method);
        Assert.That(method.ReturnType, Is.EqualTo(typeof(int)));
        ParameterInfo[] parameters = method.GetParameters();
        Assert.That(parameters.Length, Is.EqualTo(1));
        Assert.That(parameters[0].ParameterType, Is.EqualTo(elementType.MakeArrayType()));
    }
}
