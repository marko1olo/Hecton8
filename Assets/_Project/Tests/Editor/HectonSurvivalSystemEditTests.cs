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
    private const string HectonSurvivalSystemPath = "Assets/_Project/Scripts/HectonSurvivalSystem.cs";
    private const string SomaticSurvivalMathPath = "Assets/_Project/Scripts/Gameplay/SomaticSurvivalMath.cs";
    private const string SaveDataPath = "Assets/_Project/Scripts/SaveData.cs";
    private const string SaveDataPlayerSurvivalSanitizerPath = "Assets/_Project/Scripts/SaveDataPlayerSurvivalSanitizer.cs";
    private const string GlobalSignalsRuntimeLifecyclePath = "Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs";
    private const string GlobalSignalsLegacyFacadePath = "Assets/_Project/Scripts/Core/Signals/GlobalSignals.LegacyFacade.cs";
    private const string SignalBridgeRoutesPath = "Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs";
    private const string ShinobuPhysiologyRuntimePath = "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs";
    private const string PdaDeathMemoryDumpPath = "Assets/_Project/Scripts/UI/PDADeathMemoryDump.cs";
    private const string SuitAdvisoryControllerPath = "Assets/_Project/Scripts/UI/SuitAdvisoryController.cs";
    private const string WristHologramHudRuntimePath = "Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs";

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
    public void SharedSomaticMathBoundaries_DropNonFiniteInputsBeforeRuntimeConsumers()
    {
        MethodInfo radiationFatigue = GetPrivateStaticMethod(
            typeof(HectonPlayerHealth),
            "ResolveRadiationFatigueScale",
            typeof(float));
        MethodInfo regeneration = GetPrivateStaticMethod(
            typeof(HectonPlayerHealth),
            "ResolveNaturalHealthRegenerationMultiplier",
            typeof(float));
        MethodInfo thermalSample = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveExternalThermalShockTemperature",
            typeof(float),
            typeof(float));
        MethodInfo thermalDamage = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveThermalShockDamagePerSecond",
            typeof(float),
            typeof(float),
            typeof(float));
        MethodInfo toxicityDamage = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveNutritionalToxicityDamagePerSecond",
            typeof(float),
            typeof(float));
        MethodInfo nitrogenRinging = GetPrivateStaticMethod(
            typeof(HectonSurvivalSystem),
            "ResolveNitrogenWarningRinging01",
            typeof(float));

        Assert.That((float)radiationFatigue.Invoke(null, new object[] { float.NaN }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)radiationFatigue.Invoke(null, new object[] { float.PositiveInfinity }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)regeneration.Invoke(null, new object[] { float.NaN }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)regeneration.Invoke(null, new object[] { float.NegativeInfinity }), Is.EqualTo(1f).Within(0.0001f));
        Assert.That((float)thermalSample.Invoke(null, new object[] { 20f, float.NaN }), Is.EqualTo(20f).Within(0.0001f));
        Assert.That((float)thermalSample.Invoke(null, new object[] { float.NaN, float.PositiveInfinity }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)thermalDamage.Invoke(null, new object[] { 120f, float.NaN, 1f }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)thermalDamage.Invoke(null, new object[] { 120f, 2f, float.NaN }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)toxicityDamage.Invoke(null, new object[] { float.NaN, 2f }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)toxicityDamage.Invoke(null, new object[] { 1f, float.NaN }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)toxicityDamage.Invoke(null, new object[] { 1f, 2f }), Is.EqualTo(0.9f).Within(0.0001f));
        Assert.That((float)nitrogenRinging.Invoke(null, new object[] { float.NaN }), Is.EqualTo(0f).Within(0.0001f));
        Assert.That((float)nitrogenRinging.Invoke(null, new object[] { float.PositiveInfinity }), Is.EqualTo(0f).Within(0.0001f));

        string source = File.ReadAllText(SomaticSurvivalMathPath);
        StringAssert.Contains("float safeExposureSeconds = ResolveNonNegativeFinite(exposureSeconds);", source);
        StringAssert.Contains("return math.lerp(1f, NutritionalToxicityRegenFloor, ResolveFinite01(toxicitySeverity01));", source);
        StringAssert.Contains("return math.isfinite(fallbackTemperatureCelsius) ? fallbackTemperatureCelsius : 0f;", source);
        StringAssert.Contains("return ResolveNonNegativeFinite(baseDamageRate)", source);
        StringAssert.Contains("ResolveFinite01(severity01);", source);
        StringAssert.Contains("private static float ResolveNonNegativeFinite(float value)", source);
        StringAssert.Contains("private static float ResolveFinite01(float value)", source);
    }

    [Test]
    public void SurvivalReadPublishAndLoadBoundariesKeepNonFiniteValuesOutOfConsumers()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string saveData = File.ReadAllText(SaveDataPath);
        string sanitizer = File.ReadAllText(SaveDataPlayerSurvivalSanitizerPath);
        string publishRuntime = ExtractMethodBody(source, "private void PublishRuntimeContextState()");
        string publishUi = ExtractMethodBody(source, "private void PublishHeadlessUIState()");
        string publishDirty = ExtractMethodBody(source, "private void PublishDirty()");
        string publishVitals = ExtractMethodBody(source, "private void PublishSurvivalVitalsChanged(");
        string reportVitalsDrop = ExtractMethodBody(source, "private static void ReportSurvivalVitalsSignalDrop()");
        string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
        string movementPenalty = ExtractMethodBody(source, "private void ApplyNitrogenMovementPenalty()");
        string decompressionVomit = ExtractMethodBody(source, "private void HandleDecompressionSicknessVomit(");
        string effectiveSafeDepth = ExtractMethodBody(source, "private float ResolveEffectiveSafeDepthMeters()");
        string oxygenPressureScale = ExtractMethodBody(source, "private float ResolveOxygenPressureScale()");
        string pressureDamage = ExtractMethodBody(source, "private float ResolveCurrentPressureDamagePerSecond()");
        string pressureExposure = ExtractMethodBody(source, "private float ResolvePressureExposureSeverity01()");
        string overpressureSeverity = ExtractMethodBody(source, "private static float ResolveOverpressureSeverity01(");
        string nitrogenRinging = ExtractMethodBody(source, "internal static float ResolveNitrogenWarningRinging01(");
        string safeRatio = ExtractMethodBody(source, "private static float ResolveSafeRatio01(");
        string safeDouble = ExtractMethodBody(source, "private static double SafeNonNegative(");

        StringAssert.Contains("public const float PlayerEnvironmentTemperatureDefault = 20f;", saveData);
        StringAssert.Contains("environmentTemperature = PlayerEnvironmentTemperatureDefault", saveData);
        StringAssert.Contains("private const float DefaultInternalTemperatureCelsius = SaveData.PlayerEnvironmentTemperatureDefault;", source);
        StringAssert.Contains("value.environmentTemperature = SanitizeFinite(value.environmentTemperature, SaveData.PlayerEnvironmentTemperatureDefault);", sanitizer);
        StringAssert.Contains("public float Oxygen              => SafeNonNegative(oxygen);", source);
        StringAssert.Contains("public float Energy              => SafeNonNegative(energy);", source);
        StringAssert.Contains("public float Depth               => SafeNonNegative(depth);", source);
        StringAssert.Contains("public float Integrity           => SafeNonNegative(integrity);", source);
        StringAssert.Contains("public float Pressure            => FiniteAtLeast(pressure, 1f, 1f);", source);
        StringAssert.Contains("public float Weight              => SafeNonNegative(weight);", source);
        StringAssert.Contains("public float Hunger              => SafeNonNegative(hunger);", source);
        StringAssert.Contains("public float Thirst              => SafeNonNegative(thirst);", source);
        StringAssert.Contains("public double CurrentLifeDurationSeconds => SafeNonNegative(_currentLifeDurationSeconds);", source);
        StringAssert.Contains("public double CurrentLifePeakDepthMeters => SafeNonNegative(_currentLifePeakDepthMeters);", source);
        StringAssert.Contains("public float OxygenNormalized    => ResolveSafeRatio01(oxygen, ResolveRuntimeMaxOxygenCapacity());", source);
        StringAssert.Contains("public float EnergyNormalized    => stats != null ? ResolveSafeRatio01(energy, stats.MaxEnergy) : 0f;", source);
        StringAssert.Contains("public float ThermalStressSeverity01 => math.max(ColdStressSeverity01, HeatStressSeverity01);", source);
        StringAssert.Contains("internal float RapidAscentRisk01 => SafeSaturate(_decompressionRisk01);", source);
        StringAssert.Contains("public float NitrogenBuildUp => SafeNonNegative(_nitrogenBuildUp);", source);
        StringAssert.Contains("public float NitrogenLoad01 => SafeSaturate(_nitrogenLoad * math.rcp(NitrogenTissueLoadBendsThresholdAtm));", source);
        StringAssert.Contains("public float NitrogenNarcosis01 => SafeSaturate(_nitrogenNarcosis01);", source);
        StringAssert.Contains("public float Toxicity01 => SafeSaturate(_toxicity01);", source);
        StringAssert.Contains("public float OxygenGraceVisionBlur01 => SafeSaturate(_oxygenGraceVisionBlur01);", source);
        StringAssert.Contains("public float SafeDepthMarginMeters => stats != null ? ResolveEffectiveSafeDepthMeters() - SafeNonNegative(depth) : 0f;", source);
        StringAssert.Contains("public float OverpressureMeters => stats != null ? SafeNonNegative(SafeNonNegative(depth) - ResolveEffectiveSafeDepthMeters()) : 0f;", source);

        StringAssert.Contains("survivalState.OxygenNormalized = OxygenNormalized;", publishRuntime);
        StringAssert.Contains("survivalState.ThermalStressSeverity01 = ThermalStressSeverity01;", publishRuntime);
        StringAssert.Contains("survivalState.RapidAscentRisk01 = RapidAscentRisk01;", publishRuntime);
        StringAssert.Contains("survivalState.NitrogenBuildUp01 = NitrogenBuildUp01;", publishRuntime);
        StringAssert.Contains("survivalState.NitrogenLoad01 = NitrogenLoad01;", publishRuntime);
        StringAssert.Contains("survivalState.NitrogenNarcosis01 = NitrogenNarcosis01;", publishRuntime);
        StringAssert.Contains("survivalState.RadiationDose = SafeNonNegative(_runtimeContext.RadiationDose);", publishRuntime);
        StringAssert.Contains("survivalState.RadiationIntensity01 = SafeSaturate(_runtimeContext.RadiationIntensity01);", publishRuntime);

        StringAssert.Contains("float safeOxygen = Oxygen;", publishDirty);
        StringAssert.Contains("float safeEnergy = Energy;", publishDirty);
        StringAssert.Contains("float safeDepth = Depth;", publishDirty);
        StringAssert.Contains("float safeIntegrity = Integrity;", publishDirty);
        StringAssert.Contains("float safePressure = Pressure;", publishDirty);
        StringAssert.Contains("if (math.abs(safeOxygen - lastPubOxygen) > Epsilon)", publishDirty);
        StringAssert.Contains("lastPubOxygen = safeOxygen;", publishDirty);
        StringAssert.Contains("float atmosphereTemperature = atmosphere != null ? atmosphere.CurrentTemperature : DefaultInternalTemperatureCelsius;", publishDirty);
        StringAssert.Contains("float baseTemp = math.isfinite(atmosphereTemperature) ? atmosphereTemperature : DefaultInternalTemperatureCelsius;", publishDirty);
        StringAssert.Contains("SafeNonNegative(ResolveHazardIntensity(HazardType.Heat))", publishDirty);
        StringAssert.Contains("float baseRad = atmosphere != null ? SafeNonNegative(atmosphere.CurrentRadiation) : 0f;", publishDirty);
        StringAssert.Contains("float gridRad = SafeSaturate(_runtimeContext.RadiationIntensity01);", publishDirty);
        StringAssert.DoesNotContain("math.abs(oxygen - lastPubOxygen)", publishDirty);

        StringAssert.Contains("ResolveSafeRatio01(oxygen, maxOxygen)", publishUi);
        StringAssert.Contains("SafeNonNegative(depth)", publishUi);
        StringAssert.Contains("FiniteAtLeast(pressure, 1f, 1f)", publishUi);
        StringAssert.Contains("ResolveSafeRatio01(weight, carryCapacityKg)", publishUi);
        StringAssert.DoesNotContain("math.saturate(oxygen / maxOxygen)", publishUi);
        StringAssert.Contains("float maxOxygen = FiniteAtLeast(ResolveRuntimeMaxOxygenCapacity(), 100f, 0.01f);", publishVitals);
        StringAssert.Contains("float maxEnergy = FiniteAtLeast(stats.MaxEnergy, 100f, 0.01f);", publishVitals);
        StringAssert.Contains("float maxIntegrity = FiniteAtLeast(stats.MaxIntegrity, 100f, 0.01f);", publishVitals);
        StringAssert.Contains("signal.Oxygen01 = ResolveSafeRatio01(oxygen, maxOxygen);", publishVitals);
        StringAssert.Contains("signal.Energy01 = ResolveSafeRatio01(energy, maxEnergy);", publishVitals);
        StringAssert.Contains("signal.Integrity01 = ResolveSafeRatio01(integrity, maxIntegrity);", publishVitals);
        StringAssert.Contains("if (!SurvivalSignalRoute.TryQueueVitals(in signal))", publishVitals);
        StringAssert.Contains("ReportSurvivalVitalsSignalDrop();", publishVitals);
        StringAssert.Contains("private static readonly uint _SurvivalVitalsQueueDropWarningHash", source);
        StringAssert.Contains("private static readonly uint _SurvivalVitalsQueueContextHash", source);
        StringAssert.Contains("Interlocked.Increment(ref s_x001HectonSurvivalSystemSignalPushDropCount)", reportVitalsDrop);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportVitalsDrop);
        StringAssert.Contains("_SurvivalVitalsQueueDropWarningHash", reportVitalsDrop);
        StringAssert.Contains("_SurvivalVitalsQueueContextHash", reportVitalsDrop);
        StringAssert.Contains("math.max(1, dropCount)", reportVitalsDrop);
        StringAssert.DoesNotContain("SurvivalSignalRoute.TryQueueVitals(in signal);", publishVitals);
        StringAssert.DoesNotContain("signal.Oxygen01 = math.saturate(oxygen / maxOxygen);", publishVitals);
        StringAssert.Contains("float nitrogenStaminaMultiplier = math.lerp(1f, NitrogenStaminaPenaltyMultiplier, NitrogenNarcosis01);", movementPenalty);
        StringAssert.Contains("float severity01 = RapidAscentRisk01;", decompressionVomit);
        StringAssert.Contains("SafeNonNegative(stats.SafeDepth + ResolveTransportSafeDepthBonusMeters())", effectiveSafeDepth);
        StringAssert.Contains("1f + SafeNonNegative(depth) * 0.1f;", oxygenPressureScale);
        StringAssert.Contains("return SafeNonNegative(pressureDamagePerSecond * DynamicDifficultyDirector.Current.DamageMultiplier);", pressureDamage);
        StringAssert.Contains("float damageSeverity = ResolveSafeRatio01(ResolveCurrentPressureDamagePerSecond(), Mathf.Max(1f, stats.MaxIntegrity * 0.08f));", pressureExposure);
        StringAssert.Contains("return SafeSaturate(overpressureSeverity * 0.65f + damageSeverity * 0.35f);", pressureExposure);
        StringAssert.Contains("overpressureMeters = SafeNonNegative(overpressureMeters);", overpressureSeverity);
        StringAssert.Contains("effectiveSafeDepthMeters = SafeNonNegative(effectiveSafeDepthMeters);", overpressureSeverity);

        StringAssert.Contains("_currentLifeDurationSeconds = hasTelemetryV23 ? SafeNonNegative(dto.currentLifeDurationSeconds) : 0d;", load);
        StringAssert.Contains("_currentLifeLowestOxygenNormalized = hasTelemetryV23 ? SafeSaturate(dto.currentLifeLowestOxygenNormalized) : OxygenNormalized;", load);
        StringAssert.Contains("_environmentTemperature = math.isfinite(dto.environmentTemperature) ? dto.environmentTemperature : DefaultInternalTemperatureCelsius;", load);
        StringAssert.Contains("_coldSeverity01 = SafeSaturate(dto.coldStressSeverity01);", load);
        StringAssert.Contains("_nitrogenBuildUp = math.isfinite(dto.nitrogenBuildUp)", load);
        StringAssert.Contains("SafeNonNegative(dto.lastDeathLifeDurationSeconds)", load);
        StringAssert.Contains("SafeSaturate(dto.lastDeathLowestIntegrityNormalized)", load);
        StringAssert.Contains("float buildUp01 = SafeSaturate(nitrogenBuildUp / NitrogenCriticalBuildUp);", nitrogenRinging);
        StringAssert.Contains("if (!math.isfinite(numerator) || !math.isfinite(denominator) || denominator <= 0f)", safeRatio);
        StringAssert.Contains("!double.IsNaN(value) && !double.IsInfinity(value)", safeDouble);
    }

    [Test]
    public void LegacySurvivalVitalsFacadeRecordsRouteQueueDrops()
    {
        string source = File.ReadAllText(GlobalSignalsLegacyFacadePath);
        string publishBody = ExtractMethodBody(source, "public static void Publish(in SurvivalVitalsChangedSignal signal)");

        StringAssert.Contains("private static bool TryPushLegacy<T>(in T signal)", source);
        StringAssert.Contains("SignalBridgeState.RecordLegacyPublishDrop();", source);
        StringAssert.Contains("EnsureInitialized();", publishBody);
        StringAssert.Contains("if (!SurvivalSignalRoute.TryQueueVitals(in signal))", publishBody);
        StringAssert.Contains("SignalBridgeState.RecordLegacyPublishDrop();", publishBody);
        AssertSourceOrder(publishBody, "EnsureInitialized();", "if (!SurvivalSignalRoute.TryQueueVitals(in signal))");
        AssertSourceOrder(publishBody, "if (!SurvivalSignalRoute.TryQueueVitals(in signal))", "SignalBridgeState.RecordLegacyPublishDrop();");
        Assert.That(publishBody, Does.Not.Contain("SurvivalSignalRoute.TryQueueVitals(in signal);"));
        Assert.That(publishBody, Does.Not.Contain("SignalBus<SurvivalVitalsChangedSignal>.TryPush"));
    }

    [Test]
    public void SurvivalSignalRouteSanitizesBeforeRecordingLatestDeath()
    {
        string source = File.ReadAllText(SignalBridgeRoutesPath);
        string legacySource = File.ReadAllText(GlobalSignalsLegacyFacadePath);
        string latestBody = ExtractMethodBody(source, "public static bool TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence)");
        string queueBody = ExtractMethodBody(source, "public static bool TryQueueVitals(in SurvivalVitalsChangedSignal signal)");
        string legacyLatestBody = ExtractMethodBody(legacySource, "public static bool TryGetLatestSurvivalDeathSignal(out SurvivalVitalsChangedSignal signal, out int sequence)");

        StringAssert.Contains("SignalCorridorRuntime.EnsureInitialized();", latestBody);
        StringAssert.Contains("return SignalBridgeState.TryGetLatestSurvivalDeath(out signal, out sequence);", latestBody);
        AssertSourceOrder(latestBody, "SignalCorridorRuntime.EnsureInitialized();", "return SignalBridgeState.TryGetLatestSurvivalDeath(out signal, out sequence);");
        StringAssert.Contains("return SurvivalSignalRoute.TryGetLatestDeath(out signal, out sequence);", legacyLatestBody);
        StringAssert.DoesNotContain("SignalBridgeState.TryGetLatestSurvivalDeath", legacyLatestBody);
        StringAssert.Contains("SignalCorridorRuntime.EnsureInitialized();", queueBody);
        StringAssert.Contains("SurvivalVitalsChangedSignal sanitizedSignal = signal;", queueBody);
        StringAssert.Contains("int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);", queueBody);
        StringAssert.Contains("GlobalTelemetryBus.PublishMathGuardInvalidNumber(guardCode);", queueBody);
        StringAssert.Contains("if ((sanitizedSignal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)", queueBody);
        StringAssert.Contains("sanitizedSignal.DeathCause = 0;", queueBody);
        StringAssert.Contains("SignalBridgeState.RecordSurvivalVitals(in sanitizedSignal);", queueBody);
        StringAssert.Contains("return SignalBus<SurvivalVitalsChangedSignal>.TryPush(in sanitizedSignal);", queueBody);
        AssertSourceOrder(queueBody, "SignalCorridorRuntime.EnsureInitialized();", "SurvivalVitalsChangedSignal sanitizedSignal = signal;");
        AssertSourceOrder(queueBody, "int guardCode = SignalPayloadFiniteGuards.Sanitize(ref sanitizedSignal);", "SignalBridgeState.RecordSurvivalVitals(in sanitizedSignal);");
        AssertSourceOrder(queueBody, "sanitizedSignal.DeathCause = 0;", "SignalBridgeState.RecordSurvivalVitals(in sanitizedSignal);");
        AssertSourceOrder(queueBody, "SignalBridgeState.RecordSurvivalVitals(in sanitizedSignal);", "return SignalBus<SurvivalVitalsChangedSignal>.TryPush(in sanitizedSignal);");
        Assert.That(queueBody, Does.Not.Contain("SignalBridgeState.RecordSurvivalVitals(in signal);"));
        Assert.That(queueBody, Does.Not.Contain("SignalBus<SurvivalVitalsChangedSignal>.TryPush(in signal);"));
    }

    [Test]
    public void SurvivalLatestDeathBridgeResetsWithGlobalSignalsLifecycle()
    {
        string source = File.ReadAllText(GlobalSignalsRuntimeLifecyclePath);
        string clearLatestBody = ExtractMethodBody(source, "private static void ClearLatestSignals()");

        StringAssert.Contains("SignalBridgeState.Reset();", clearLatestBody);
        AssertSourceOrder(clearLatestBody, "_latestPlayerStateSignal = default;", "SignalBridgeState.Reset();");
        AssertSourceOrder(clearLatestBody, "SignalBridgeState.Reset();", "Volatile.Write(ref _latestStorageDebtMilli, 0);");
    }

    [Test]
    public void ShinobuPhysiologySurvivalVitalsQueueRefusalIsVisibleTelemetry()
    {
        string source = File.ReadAllText(ShinobuPhysiologyRuntimePath);
        string publishVitalsBody = ExtractMethodBody(source, "private void PublishSurvivalVitals(");
        string reportDropBody = ExtractMethodBody(source, "private static void ReportSurvivalVitalsSignalDrop()");

        StringAssert.Contains("private static readonly uint _SurvivalVitalsQueueDropWarningHash", source);
        StringAssert.Contains("private static readonly uint _SurvivalVitalsQueueContextHash", source);
        StringAssert.Contains("if (!SurvivalSignalRoute.TryQueueVitals(in signal))", publishVitalsBody);
        StringAssert.Contains("ReportSurvivalVitalsSignalDrop();", publishVitalsBody);
        StringAssert.Contains("System.Threading.Interlocked.Increment(ref s_x001ShinobuPhysiologyRuntimeSignalPushDropCount)", reportDropBody);
        StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportDropBody);
        StringAssert.Contains("_SurvivalVitalsQueueDropWarningHash", reportDropBody);
        StringAssert.Contains("_SurvivalVitalsQueueContextHash", reportDropBody);
        StringAssert.Contains("math.max(1, dropCount)", reportDropBody);
        StringAssert.DoesNotContain("SurvivalSignalRoute.TryQueueVitals(in signal);", publishVitalsBody);
    }

    [Test]
    public void PdaDeathDumpRequiresDeathFlagBeforePressureCause()
    {
        string source = File.ReadAllText(PdaDeathMemoryDumpPath);
        string consumeBody = ExtractMethodBody(source, "private void ConsumeSurvivalDeathSignal()");

        StringAssert.Contains("SurvivalSignalRoute.TryGetLatestDeath(out SurvivalVitalsChangedSignal signal, out int sequence)", consumeBody);
        StringAssert.Contains("if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)", consumeBody);
        StringAssert.Contains("if (signal.DeathCause != (byte)SurvivalDeathCause.PressureCollapse)", consumeBody);
        AssertSourceOrder(consumeBody, "if ((signal.Flags & SurvivalVitalsChangedSignalFlags.Death) == 0u)", "if (signal.DeathCause != (byte)SurvivalDeathCause.PressureCollapse)");
        AssertSourceOrder(consumeBody, "if (signal.DeathCause != (byte)SurvivalDeathCause.PressureCollapse)", "survival.TryGetLastDeathRecord(out SurvivalDeathRecord deathRecord)");
    }

    [Test]
    public void SurvivalVitalsUiConsumersRequireFiniteUnitValuesBeforeReadModelWrites()
    {
        string suitSource = File.ReadAllText(SuitAdvisoryControllerPath);
        string wristSource = File.ReadAllText(WristHologramHudRuntimePath);
        string suitProcessBody = ExtractMethodBody(suitSource, "private void ProcessSurvivalVitalsSignal(in SurvivalVitalsChangedSignal signal)");
        string suitFiniteUnitBody = ExtractMethodBody(suitSource, "private static bool TryResolveFiniteUnit01(float value, out float safeValue)");
        string wristInjectO2Body = ExtractMethodBody(wristSource, "public void InjectO2Signal(in O2LevelChangedSignal signal)");
        string wristDrainBody = ExtractMethodBody(wristSource, "private void DrainGlobalSignalSnapshots()");
        string wristUiStateBody = ExtractMethodBody(wristSource, "private void RefreshUiStateStoreInputs()");
        string wristBuildBody = ExtractMethodBody(wristSource, "private void BuildTextQuadsOwnerPhase(float deltaTime)");

        StringAssert.Contains("TryResolveFiniteUnit01(signal.Oxygen01, out float oxygen01)", suitProcessBody);
        StringAssert.Contains("HandleOxygenChanged(oxygen01);", suitProcessBody);
        StringAssert.Contains("TryResolveFiniteUnit01(signal.Energy01, out float energy01)", suitProcessBody);
        StringAssert.Contains("HandleEnergyChanged(energy01);", suitProcessBody);
        StringAssert.Contains("TryResolveFiniteUnit01(signal.Integrity01, out float integrity01)", suitProcessBody);
        StringAssert.Contains("HandleIntegrityChanged(integrity01);", suitProcessBody);
        StringAssert.Contains("if (!math.isfinite(value))", suitFiniteUnitBody);
        StringAssert.Contains("safeValue = math.saturate(value);", suitFiniteUnitBody);
        StringAssert.DoesNotContain("HandleOxygenChanged(signal.Oxygen01);", suitProcessBody);
        StringAssert.DoesNotContain("HandleEnergyChanged(signal.Energy01);", suitProcessBody);
        StringAssert.DoesNotContain("HandleIntegrityChanged(signal.Integrity01);", suitProcessBody);

        StringAssert.Contains("_latestVitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);", wristDrainBody);
        StringAssert.Contains("_latestVitals.Power01 = FiniteSaturate(signal.Energy01);", wristDrainBody);
        StringAssert.Contains("_latestVitals.Health01 = FiniteSaturate(signal.Integrity01);", wristDrainBody);
        StringAssert.DoesNotContain("_latestVitals.Oxygen01 = math.saturate(signal.Oxygen01);", wristDrainBody);
        StringAssert.Contains("vitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);", wristInjectO2Body);
        StringAssert.DoesNotContain("vitals.Oxygen01 = math.saturate(signal.Oxygen01);", wristInjectO2Body);
        StringAssert.Contains("_latestVitals.Oxygen01 = FiniteSaturate(oxygen);", wristUiStateBody);
        StringAssert.Contains("_latestVitals.DepthMeters = FiniteNonNegative(depth);", wristUiStateBody);
        StringAssert.Contains("_latestVitals.SafeDepthMeters = math.max(1f, FiniteNonNegative(safeDepth));", wristUiStateBody);
        StringAssert.Contains("Oxygen01 = FiniteSaturate(_latestVitals.Oxygen01)", wristBuildBody);
        StringAssert.Contains("DepthMeters = FiniteNonNegative(_latestVitals.DepthMeters)", wristBuildBody);
        StringAssert.Contains("SafeDepthMeters = math.max(1f, FiniteNonNegative(_latestVitals.SafeDepthMeters))", wristBuildBody);
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
    public void SurvivalSurfaceOverride_RejectsStaleZeroWaterlineFallback()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);

        Assert.That(source, Does.Contain("private const float DefaultWaterSurfaceY = 14.02f;"));
        Assert.That(source, Does.Contain("public void SetSurfaceY(float y) => surfaceWorldY = SanitizeSurfaceY(y);"));
        Assert.That(source, Does.Contain("private static float SanitizeSurfaceY(float y)"));
        Assert.That(source, Does.Contain("math.abs(y) > 0.0001f"));
        Assert.That(source, Does.Not.Contain("public void SetSurfaceY(float y) => surfaceWorldY = math.isfinite(y) ? y : DefaultWaterSurfaceY;"));
    }

    [Test]
    public void SurvivalAupResolversUseRuntimeSnapshotsBeforeMovementFallback()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string absoluteAup = ExtractMethodBody(source, "private bool TryResolveSurvivalAbsoluteAup(out double3 playerAup)");
        string survivalAup = ExtractMethodBody(source, "private bool TryResolveSurvivalAup(out AbsoluteUniversePosition playerAup)");
        string runtimePosition = ExtractMethodBody(source, "private Vector3 ResolveSurvivalRuntimePosition()");

        StringAssert.Contains("IPlayerRuntimeContext playerContext = _playerRuntimeContext;", absoluteAup);
        StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", absoluteAup);
        StringAssert.Contains("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", absoluteAup);
        StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", absoluteAup);
        AssertSourceOrder(absoluteAup, "IPlayerRuntimeContext playerContext = _playerRuntimeContext;", "if (_playerMovement != null)");
        AssertSourceOrder(absoluteAup, "return false;", "if (_playerMovement != null)");

        StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", survivalAup);
        StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", survivalAup);
        StringAssert.Contains("movementState.PredictedAup.IsFinite()", survivalAup);
        AssertSourceOrder(survivalAup, "IPlayerRuntimeContext playerContext = _playerRuntimeContext;", "if (_playerMovement != null)");
        AssertSourceOrder(survivalAup, "return false;", "if (_playerMovement != null)");

        StringAssert.Contains("playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)", runtimePosition);
        StringAssert.Contains("math.all(math.isfinite(snapshot.RuntimePosition))", runtimePosition);
        StringAssert.Contains("playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)", runtimePosition);
        StringAssert.Contains("math.all(math.isfinite(movementState.WorldPosition))", runtimePosition);
        AssertSourceOrder(runtimePosition, "IPlayerRuntimeContext playerContext = _playerRuntimeContext;", "if (_playerMovement != null)");
        AssertSourceOrder(runtimePosition, "return Vector3.zero;", "if (_playerMovement != null)");
    }

    [Test]
    public void SurvivalLoadPublishesRestoredStateEvenWhenDead()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string loadBody = ExtractMethodBody(source, "public void LoadFromSaveData(");
        string populateBody = ExtractMethodBody(source, "public void PopulateSaveData(");
        string persistedIntegrity = ExtractMethodBody(source, "private float ResolvePersistedIntegrityForCurrentLife()");
        string publishLoaded = ExtractMethodBody(source, "private void PublishLoadedSurvivalState()");
        string resolveLoadedCause = ExtractMethodBody(source, "private SurvivalDeathCause ResolveLoadedDeathCause()");
        string slowTick = ExtractMethodBody(source, "public void SlowTick()");
        int normalLoadStart = loadBody.IndexOf("PlayerStatsDTO dto = data.playerStats;", StringComparison.Ordinal);
        Assert.That(normalLoadStart, Is.GreaterThanOrEqualTo(0));
        string normalLoadBody = loadBody.Substring(normalLoadStart);

        AssertSourceOrder(loadBody, "ClearNitrogenLoadNotificationDiagnostics();", "if (data == null)");
        AssertSourceOrder(loadBody, "ClearNitrogenLoadNotificationDiagnostics();", "ClearPendingRespawnReconciliation();");
        AssertSourceOrder(loadBody, "ClearPendingRespawnReconciliation();", "if (data == null)");
        AssertSourceOrder(loadBody, "if (data == null)", "PlayerStatsDTO dto = data.playerStats;");
        AssertSourceOrder(loadBody, "if (stats != null)", "PublishLoadedSurvivalState();");
        AssertSourceOrder(loadBody, "PublishLoadedSurvivalState();", "return;");
        AssertSourceOrder(loadBody, "if (stats == null)", "PlayerStatsDTO dto = data.playerStats;");
        Assert.That(normalLoadBody, Does.Contain("alive     = integrity > 0f;"));
        Assert.That(normalLoadBody, Does.Contain("_pendingIntegrityDeathCause = SurvivalDeathCause.None;"));
        Assert.That(normalLoadBody, Does.Contain("_lastDeathCause = alive ? SurvivalDeathCause.None : ResolveLoadedDeathCause();"));
        Assert.That(normalLoadBody, Does.Contain("ApplyInjuryMovementPenalty();"));
        Assert.That(normalLoadBody, Does.Contain("ApplyNitrogenMovementPenalty();"));
        Assert.That(normalLoadBody, Does.Contain("PublishLoadedSurvivalState();"));
        AssertSourceOrder(normalLoadBody, "alive     = integrity > 0f;", "PublishLoadedSurvivalState();");
        AssertSourceOrder(normalLoadBody, "_pendingIntegrityDeathCause = SurvivalDeathCause.None;", "_lastDeathCause = alive ? SurvivalDeathCause.None : ResolveLoadedDeathCause();");
        AssertSourceOrder(normalLoadBody, "_lastDeathRecord = _hasLastDeathRecord", "_lastDeathCause = alive ? SurvivalDeathCause.None : ResolveLoadedDeathCause();");
        AssertSourceOrder(normalLoadBody, "ApplyNitrogenMovementPenalty();", "PublishLoadedSurvivalState();");
        Assert.That(loadBody, Does.Not.Contain("ForceAllDirty();"));

        AssertSourceOrder(populateBody, "if (data == null)", "ref PlayerStatsDTO dto = ref data.playerStats;");
        Assert.That(populateBody, Does.Contain("dto.integrity = ResolvePersistedIntegrityForCurrentLife();"));
        Assert.That(populateBody, Does.Not.Contain("dto.integrity = integrity;"));
        AssertSourceOrder(populateBody, "dto.integrity = ResolvePersistedIntegrityForCurrentLife();", "SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref dto);");
        Assert.That(persistedIntegrity, Does.Contain("return alive ? integrity : 0f;"));

        Assert.That(resolveLoadedCause, Does.Contain("_hasLastDeathRecord && _lastDeathRecord.Cause != SurvivalDeathCause.None"));
        Assert.That(resolveLoadedCause, Does.Contain("return _lastDeathRecord.Cause;"));
        Assert.That(resolveLoadedCause, Does.Contain("return ResolveDeathCause();"));
        AssertSourceOrder(resolveLoadedCause, "return _lastDeathRecord.Cause;", "return ResolveDeathCause();");

        Assert.That(slowTick, Does.Contain("if (!alive) return;"));
        AssertSourceOrder(publishLoaded, "RefreshSurvivalIdentityCold();", "RefreshSurvivalStatusMask();");
        AssertSourceOrder(publishLoaded, "RefreshSurvivalStatusMask();", "ForceAllDirty();");
        AssertSourceOrder(publishLoaded, "ForceAllDirty();", "PublishRuntimeContextState();");
        AssertSourceOrder(publishLoaded, "PublishRuntimeContextState();", "PublishHeadlessUIState();");
        AssertSourceOrder(publishLoaded, "PublishHeadlessUIState();", "PublishDirty();");
        Assert.That(publishLoaded, Does.Contain("if (!alive)"));
        Assert.That(publishLoaded, Does.Contain("SurvivalVitalsChangedSignalFlags.Death"));
        Assert.That(publishLoaded, Does.Contain("SurvivalVitalsChangedSignalFlags.Integrity"));
        Assert.That(publishLoaded, Does.Contain("SurvivalVitalsChangedSignalFlags.Oxygen"));
        Assert.That(publishLoaded, Does.Contain("SurvivalVitalsChangedSignalFlags.Depth"));
        AssertSourceOrder(publishLoaded, "PublishDirty();", "if (!alive)");
        AssertSourceOrder(publishLoaded, "if (!alive)", "WriteSurvivalBlackboxSnapshot();");
    }

    [Test]
    public void SurvivalRespawnPublishesRuntimeUiDirtyResetAndBlackboxState()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string respawnBody = ExtractMethodBody(source, "private void ApplyRespawnReconciliationSurvival()");
        string publishRespawned = ExtractMethodBody(source, "private void PublishRespawnedSurvivalState()");
        string consumeCommitted = ExtractMethodBody(source, "private void ConsumeCommittedRespawnReconciliationSignals()");
        string slowTick = ExtractMethodBody(source, "public void SlowTick()");
        string lateFrameTick = ExtractMethodBody(source, "public void LateFrameTick()");

        Assert.That(respawnBody, Does.Contain("alive = true;"));
        Assert.That(respawnBody, Does.Contain("PublishRespawnedSurvivalState();"));
        AssertSourceOrder(respawnBody, "alive = true;", "PublishRespawnedSurvivalState();");

        AssertSourceOrder(slowTick, "ConsumeCommittedRespawnReconciliationSignals();", "if (!alive) return;");
        AssertSourceOrder(lateFrameTick, "ConsumeCommittedRespawnReconciliationSignals();", "FlushNarcosisShaderScalar();");
        AssertSourceOrder(consumeCommitted, "uint pendingSequence = _pendingRespawnReconciliationSequence;", "ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();");
        Assert.That(consumeCommitted, Does.Contain("PlayerDeathReconciliationBridge.IsAcceptedCommittedRespawnSignal(in signal, pendingSequence, playerHash)"));
        AssertSourceOrder(consumeCommitted, "PlayerDeathReconciliationBridge.IsAcceptedCommittedRespawnSignal(in signal, pendingSequence, playerHash)", "ApplyRespawnReconciliationSurvival();");
        AssertSourceOrder(consumeCommitted, "ApplyRespawnReconciliationSurvival();", "_lastAppliedRespawnReconciliationSequence = pendingSequence;");
        AssertSourceOrder(consumeCommitted, "_lastAppliedRespawnReconciliationSequence = pendingSequence;", "_pendingRespawnReconciliationSequence = 0u;");

        AssertSourceOrder(publishRespawned, "RefreshSurvivalIdentityCold();", "RefreshSurvivalStatusMask();");
        AssertSourceOrder(publishRespawned, "RefreshSurvivalStatusMask();", "ForceAllDirty();");
        AssertSourceOrder(publishRespawned, "ForceAllDirty();", "PublishRuntimeContextState();");
        AssertSourceOrder(publishRespawned, "PublishRuntimeContextState();", "PublishHeadlessUIState();");
        AssertSourceOrder(publishRespawned, "PublishHeadlessUIState();", "PublishDirty();");
        AssertSourceOrder(publishRespawned, "PublishDirty();", "PublishSurvivalVitalsChanged(");
        Assert.That(publishRespawned, Does.Contain("SurvivalVitalsChangedSignalFlags.Thermal"));
        Assert.That(publishRespawned, Does.Contain("SurvivalVitalsChangedSignalFlags.Injury"));
        AssertSourceOrder(publishRespawned, "PublishSurvivalVitalsChanged(", "WriteSurvivalBlackboxSnapshot();");
    }

    [Test]
    public void SurvivalLethalConditionsQueueFallbackRespawnWhenDeathAupIsMissing()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string lethalBody = ExtractMethodBody(source, "private void CheckLethalConditions()");
        int missingAupBlockStart = lethalBody.IndexOf("if (!hasDeathAup)", StringComparison.Ordinal);
        int normalRespawnStart = lethalBody.IndexOf("bool respawnAccepted = PlayerDeathReconciliationBridge.RequestRespawn(", StringComparison.Ordinal);
        Assert.That(missingAupBlockStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(normalRespawnStart, Is.GreaterThan(missingAupBlockStart));
        string missingAupBlock = lethalBody.Substring(missingAupBlockStart, normalRespawnStart - missingAupBlockStart);

        Assert.That(lethalBody, Does.Contain("bool hasDeathAup = TryResolveSurvivalAbsoluteAup(out double3 deathAup);"));
        Assert.That(lethalBody, Does.Contain("if (!hasDeathAup)"));
        Assert.That(lethalBody, Does.Contain("deathAup = MissingRespawnDeathAup();"));
        Assert.That(lethalBody, Does.Contain("PlayerDeathReconciliationBridge.RequestRespawn("));
        Assert.That(missingAupBlock, Does.Not.Contain("return;"));
        Assert.That(missingAupBlock, Does.Not.Contain("ApplyRespawnReconciliationSurvival();"));
        AssertSourceOrder(lethalBody, "if (!hasDeathAup)", "deathAup = MissingRespawnDeathAup();");
        AssertSourceOrder(lethalBody, "deathAup = MissingRespawnDeathAup();", "PlayerDeathReconciliationBridge.RequestRespawn(");
        AssertSourceOrder(lethalBody, "if (!hasDeathAup)", "bool respawnAccepted = PlayerDeathReconciliationBridge.RequestRespawn(");
        Assert.That(lethalBody, Does.Contain("out uint respawnSequence"));
        AssertSourceOrder(lethalBody, "if (!respawnAccepted)", "_pendingRespawnReconciliationSequence = respawnSequence;");
        Assert.That(lethalBody, Does.Not.Contain("ApplyRespawnReconciliationSurvival();"));
    }

    [Test]
    public void NitrogenLoadWarning_RetriesNotificationUntilRegisteredPushSucceeds()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string updateBody = ExtractMethodBody(source, "private void UpdateNitrogenPreNarcosisWarningState()");
        string missBody = ExtractMethodBody(source, "private void ReportNitrogenLoadNotificationMiss()");
        string clearBody = ExtractMethodBody(source, "private void ClearNitrogenLoadNotificationDiagnostics()");
        string onDisableBody = ExtractMethodBody(source, "private void OnDisable()");
        string onDestroyBody = ExtractMethodBody(source, "private void OnDestroy()");
        string populateBody = ExtractMethodBody(source, "public void PopulateSaveData(");
        string loadBody = ExtractMethodBody(source, "public void LoadFromSaveData(");
        string normalizedUpdateBody = updateBody.Replace("\r\n", "\n");

        Assert.That(source, Does.Contain("private const int NitrogenLoadNotificationRetryFrames = 30;"));
        Assert.That(source, Does.Contain("public int NitrogenLoadNotificationMissCount =>"));
        Assert.That(updateBody, Does.Contain("_nitrogenLoadNotificationRetryFrame = 0;"));
        Assert.That(updateBody, Does.Contain("if (_nitrogenLoadNotificationRetryFrame > frame)"));
        Assert.That(updateBody, Does.Contain("if (NotificationEvents.TryPushRegisteredWarning(_NitrogenLoadWarningMessageHash))"));
        AssertSourceOrder(updateBody, "if (NotificationEvents.TryPushRegisteredWarning(_NitrogenLoadWarningMessageHash))", "_nitrogenLoadWarningIssued = true;");
        Assert.That(updateBody, Does.Contain("_nitrogenLoadNotificationRetryFrame = frame + NitrogenLoadNotificationRetryFrames;"));
        Assert.That(updateBody, Does.Contain("ReportNitrogenLoadNotificationMiss();"));
        Assert.That(normalizedUpdateBody, Does.Not.Contain("_nitrogenLoadWarningIssued = true;\n            NotificationEvents.TryPushRegisteredWarning(_NitrogenLoadWarningMessageHash);"));
        Assert.That(missBody, Does.Contain("_nitrogenLoadNotificationMissCount++"));
        Assert.That(missBody, Does.Contain("_NitrogenLoadNotificationMissWarningHash"));
        Assert.That(missBody, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
        Assert.That(clearBody, Does.Contain("_nitrogenLoadNotificationRetryFrame = 0;"));
        Assert.That(clearBody, Does.Contain("_nitrogenLoadNotificationMissCount = 0;"));
        Assert.That(onDisableBody, Does.Contain("ClearNitrogenLoadNotificationDiagnostics();"));
        Assert.That(onDestroyBody, Does.Contain("ClearNitrogenLoadNotificationDiagnostics();"));
        Assert.That(loadBody, Does.Contain("ClearNitrogenLoadNotificationDiagnostics();"));
        Assert.That(populateBody, Does.Not.Contain("_nitrogenLoadNotificationMissCount"));
        Assert.That(loadBody, Does.Not.Contain("_nitrogenLoadNotificationMissCount"));
    }

    [Test]
    public void ToxicityStatusPublishers_FailClosedOnNonFiniteInputs()
    {
        string source = File.ReadAllText(HectonSurvivalSystemPath);
        string applyBody = ExtractMethodBody(source, "public void ApplyNutritionalToxicity(");
        string nutritionalBody = ExtractMethodBody(source, "private void PublishNutritionalToxicityStatus(");
        string environmentalBody = ExtractMethodBody(source, "private void PublishEnvironmentalToxicityStatus(");
        string toxicityEntityBody = ExtractMethodBody(source, "private uint ResolvePlayerToxicitySignalEntityId(");
        string bodyToxicityBody = ExtractMethodBody(source, "private float ResolveBodyToxicity01(");

        Assert.That(source, Does.Contain("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;"));
        Assert.That(toxicityEntityBody, Does.Not.Contain("return unchecked((uint)combatTargetId);"));
        Assert.That(toxicityEntityBody, Does.Contain("playerObject = playerContext.PlayerObject;"));
        Assert.That(toxicityEntityBody, Does.Contain("playerObject = _playerHealth.gameObject;"));
        Assert.That(toxicityEntityBody, Does.Contain("playerObject = BootstrapState.CurrentPlayerObject;"));
        Assert.That(toxicityEntityBody, Does.Contain("playerObject = gameObject;"));
        Assert.That(toxicityEntityBody, Does.Contain("return playerHash != 0u ? playerHash : PlayerToxicityFallbackEntityHash;"));

        Assert.That(applyBody, Does.Contain("float clampedSeverity = SafeSaturate(severity01);"));
        Assert.That(applyBody, Does.Contain("float clampedDuration = SafeNonNegative(durationSeconds);"));
        AssertSourceOrder(applyBody, "float clampedSeverity = SafeSaturate(severity01);", "PublishNutritionalToxicityStatus(clampedSeverity, clampedDuration);");

        Assert.That(nutritionalBody, Does.Contain("float severity = SafeSaturate(severity01);"));
        Assert.That(nutritionalBody, Does.Contain("float duration = math.max(0.1f, SafeNonNegative(durationSeconds));"));
        Assert.That(nutritionalBody, Does.Contain("uint signalEntityId = ResolvePlayerToxicitySignalEntityId();"));
        Assert.That(nutritionalBody, Does.Contain("CombatDamageRuntime.TryQueueStatusEffect("));
        Assert.That(nutritionalBody, Does.Contain("signal.Exposure01 = severity;"));
        Assert.That(nutritionalBody, Does.Contain("signal.ToxemiaDelta = math.saturate(severity * NutritionalToxicitySignalDeltaScale);"));
        Assert.That(nutritionalBody, Does.Contain("signal.EntityId = signalEntityId;"));
        Assert.That(nutritionalBody, Does.Contain("bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);"));
        Assert.That(nutritionalBody, Does.Contain("if (hasSourceAup)"));
        Assert.That(nutritionalBody, Does.Contain("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;"));
        Assert.That(nutritionalBody, Does.Not.Contain("if (targetId == 0 || !TryResolveSurvivalAbsoluteAup"));
        Assert.That(nutritionalBody, Does.Not.Contain("if (!TryResolveSurvivalAbsoluteAup(out double3 playerAup))"));
        AssertSourceOrder(nutritionalBody, "uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", "if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))");
        AssertSourceOrder(nutritionalBody, "if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))", "bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);");
        AssertSourceOrder(nutritionalBody, "signal.AUP = playerAup;", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
        AssertSourceOrder(nutritionalBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
        AssertSourceOrder(nutritionalBody, "float severity = SafeSaturate(severity01);", "CombatDamageRuntime.TryQueueStatusEffect(");
        AssertSourceOrder(nutritionalBody, "float severity = SafeSaturate(severity01);", "SignalBus<ToxicityExposureSignal>.TryPushTracked");

        Assert.That(environmentalBody, Does.Contain("float toxicity = SafeSaturate(toxicity01);"));
        Assert.That(environmentalBody, Does.Contain("float exposure = SafeNonNegative(exposureScale);"));
        Assert.That(environmentalBody, Does.Contain("float safeDt = SafeNonNegative(dt);"));
        Assert.That(environmentalBody, Does.Contain("float severity = math.saturate(toxicity * exposure);"));
        Assert.That(environmentalBody, Does.Contain("uint signalEntityId = ResolvePlayerToxicitySignalEntityId();"));
        Assert.That(environmentalBody, Does.Contain("float duration = math.max(0.1f, safeDt * 2f);"));
        Assert.That(environmentalBody, Does.Contain("signal.ToxemiaDelta = math.saturate(severity * safeDt * 0.08f);"));
        Assert.That(environmentalBody, Does.Contain("signal.EntityId = signalEntityId;"));
        Assert.That(environmentalBody, Does.Contain("bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);"));
        Assert.That(environmentalBody, Does.Contain("if (hasSourceAup)"));
        Assert.That(environmentalBody, Does.Contain("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;"));
        Assert.That(environmentalBody, Does.Not.Contain("if (targetId == 0 || !TryResolveSurvivalAbsoluteAup"));
        Assert.That(environmentalBody, Does.Not.Contain("if (!TryResolveSurvivalAbsoluteAup(out double3 playerAup))"));
        AssertSourceOrder(environmentalBody, "uint signalEntityId = ResolvePlayerToxicitySignalEntityId();", "if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))");
        AssertSourceOrder(environmentalBody, "if (targetId != 0 && CombatDamageRuntime.IsTargetRegistered(targetId))", "bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);");
        AssertSourceOrder(environmentalBody, "signal.AUP = playerAup;", "signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
        AssertSourceOrder(environmentalBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
        AssertSourceOrder(environmentalBody, "float toxicity = SafeSaturate(toxicity01);", "CombatDamageRuntime.TryQueueStatusEffect(");
        AssertSourceOrder(environmentalBody, "float safeDt = SafeNonNegative(dt);", "signal.ToxemiaDelta = math.saturate(severity * safeDt * 0.08f);");

        Assert.That(source, Does.Contain("survivalState.Toxicity01 = SafeSaturate(_toxicity01);"));
        Assert.That(bodyToxicityBody, Does.Contain("float hazard01 = SafeSaturate(hazardToxicity01);"));
        Assert.That(bodyToxicityBody, Does.Contain("float poison01 = SafeSaturate(ResolvePoisonStatus01());"));
        Assert.That(bodyToxicityBody, Does.Contain("float radiationToxicity01 = _playerHealth != null ? SafeSaturate(_playerHealth.RadiationExposure) : 0f;"));
        Assert.That(bodyToxicityBody, Does.Contain("return math.max(hazard01, math.max(poison01, radiationToxicity01));"));
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

    private static string ExtractMethodBody(string source, string signature)
    {
        int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
        int open = source.IndexOf('{', signatureIndex);
        Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            char c = source[i];
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(open, i - open + 1);
            }
        }

        Assert.Fail("Missing method close brace: " + signature);
        return string.Empty;
    }

    private static void AssertSourceOrder(string source, string before, string after)
    {
        int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
        int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

        Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
        Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
        Assert.Less(beforeIndex, afterIndex);
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
