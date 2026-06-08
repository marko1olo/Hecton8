using System;
using System.IO;
using System.Reflection;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed unsafe class PlayerHealthSaveBridgeEditTests
    {
        private const int BinaryPayloadScratchBytes = 1024 * 1024;

        [Test]
        public void LegacyPlayerStatsDefaultHealthDuringMigration()
        {
            SaveData data = SaveData.CreateNew(0d);
            data.version = SaveData.PlayerHealthPersistenceVersion - 1;
            data.playerStats.health = 0f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed);
            Assert.AreEqual(SaveData.PlayerHealthPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(SaveData.PlayerHealthDefault, data.playerStats.health);
            StringAssert.Contains("player health defaulted", summary);
        }

        [Test]
        public void ModernPlayerStatsZeroHealthSurvivesMigrationAsDeathState()
        {
            SaveData data = SaveData.CreateNew(0d);
            data.version = SaveData.PlayerHealthPersistenceVersion;
            data.playerStats.health = 0f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed);
            Assert.AreEqual(SaveData.PlayerHealthPersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.playerStats.health);
            StringAssert.DoesNotContain("player health defaulted", summary);
        }

        [Test]
        public void BinaryPlayerStatsHealthRoundTripsCurrentPayload()
        {
            SaveData data = SaveData.CreateNew(0d);
            data.playerStats.health = 37.25f;

            SaveData restoredData = WriteReadBinary(data);

            Assert.AreEqual(37.25f, restoredData.playerStats.health, 0.0001f);
        }

        [Test]
        public void BinaryPlayerStatsCodecDefaultsNonFiniteHealthBeforeSharedDtoReentry()
        {
            SaveData data = SaveData.CreateNew(0d);
            data.playerStats.health = float.NaN;

            SaveData restoredData = WriteReadBinary(data);

            Assert.AreEqual(SaveData.PlayerHealthDefault, restoredData.playerStats.health, 0.0001f);
        }

        [Test]
        public void PlayerStatsSanitizerDefaultsBadEnvironmentTemperatureToNeutralRuntimeTemperature()
        {
            PlayerStatsDTO stats = default;
            stats.environmentTemperature = float.NaN;

            SaveDataPlayerSurvivalSanitizer.SanitizePlayerStats(ref stats);

            Assert.AreEqual(SaveData.PlayerEnvironmentTemperatureDefault, stats.environmentTemperature, 0.0001f);
        }

        [Test]
        public void FreshSaveDataSeedsNeutralPlayerEnvironmentTemperature()
        {
            SaveData data = SaveData.CreateNew(0d);

            Assert.AreEqual(SaveData.PlayerEnvironmentTemperatureDefault, data.playerStats.environmentTemperature, 0.0001f);
        }

        [Test]
        public void HectonPlayerHealthPopulateSaveDataClampsBadRuntimeHealthBeforeSharedDto()
        {
            GameObject gameObject = new GameObject("PlayerHealth_SaveBridge_Test");
            try
            {
                gameObject.AddComponent<BoxCollider>();
                HectonPlayerHealth health = gameObject.AddComponent<HectonPlayerHealth>();
                SetPrivateFloat(health, "maxHealth", 150f);

                SaveData data = SaveData.CreateNew(0d);

                SetPrivateFloat(health, "currentHealth", float.NaN);
                health.PopulateSaveData(data);
                Assert.AreEqual(150f, data.playerStats.health, 0.0001f);

                SetPrivateFloat(health, "currentHealth", -5f);
                health.PopulateSaveData(data);
                Assert.AreEqual(0f, data.playerStats.health, 0.0001f);

                SetPrivateFloat(health, "currentHealth", 250f);
                health.PopulateSaveData(data);
                Assert.AreEqual(150f, data.playerStats.health, 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HectonPlayerHealthLoadClearsPendingRespawnGateAndDefaultsBadHealth()
        {
            GameObject gameObject = new GameObject("PlayerHealth_LoadBridge_Test");
            try
            {
                gameObject.AddComponent<BoxCollider>();
                HectonPlayerHealth health = gameObject.AddComponent<HectonPlayerHealth>();
                SetPrivateFloat(health, "maxHealth", 125f);
                SetPrivateUInt(health, "_pendingRespawnReconciliationSequence", 9u);

                SaveData data = SaveData.CreateNew(0d);
                data.playerStats.health = float.PositiveInfinity;

                health.LoadFromSaveData(data);

                Assert.AreEqual(125f, health.CurrentHealth, 0.0001f);
                Assert.AreEqual(0u, GetPrivateUInt(health, "_pendingRespawnReconciliationSequence"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void HectonPlayerHealthRuntimeInputsAndOutputsStayFiniteBeforeExternalReaders()
        {
            GameObject gameObject = new GameObject("PlayerHealth_RuntimeFiniteBridge_Test");
            try
            {
                gameObject.AddComponent<BoxCollider>();
                HectonPlayerHealth health = gameObject.AddComponent<HectonPlayerHealth>();
                SetPrivateFloat(health, "maxHealth", 150f);

                SetPrivateFloat(health, "currentHealth", float.NaN);
                Assert.AreEqual(150f, health.CurrentHealth, 0.0001f);
                Assert.IsTrue(health.IsAlive);
                Assert.AreEqual(1f, health.HealthPercent, 0.0001f);

                SetPrivateFloat(health, "currentHealth", -5f);
                Assert.AreEqual(0f, health.CurrentHealth, 0.0001f);
                Assert.IsFalse(health.IsAlive);
                Assert.AreEqual(0f, health.HealthPercent, 0.0001f);

                SetPrivateFloat(health, "currentHealth", 250f);
                Assert.AreEqual(150f, health.CurrentHealth, 0.0001f);
                Assert.AreEqual(1f, health.HealthPercent, 0.0001f);

                SetPrivateFloat(health, "currentHealth", 80f);
                Assert.IsTrue(health.TakeDamage(float.NaN, ignoreInvulnerability: true));
                Assert.AreEqual(80f, health.CurrentHealth, 0.0001f);

                Assert.AreEqual(0f, health.Heal(float.NaN), 0.0001f);
                Assert.AreEqual(80f, health.CurrentHealth, 0.0001f);

                SetPrivateFloat(health, "maxHealth", float.NaN);
                SetPrivateFloat(health, "currentHealth", float.NaN);
                Assert.AreEqual(1f, health.MaxHealth, 0.0001f);
                Assert.AreEqual(1f, health.CurrentHealth, 0.0001f);
                Assert.AreEqual(1f, health.HealthPercent, 0.0001f);

                SetPrivateFloat(health, "_baseMaxHealth", float.NaN);
                SetPrivateFloat(health, "maxHealth", float.NaN);
                SetPrivateFloat(health, "currentHealth", float.NaN);
                InvokePrivateFloat(health, "SetRuntimeMaxHealthScaleInternal", float.NaN);
                Assert.AreEqual(1f, GetPrivateFloat(health, "_baseMaxHealth"), 0.0001f);
                Assert.AreEqual(1f, GetPrivateFloat(health, "maxHealth"), 0.0001f);
                Assert.AreEqual(1f, GetPrivateFloat(health, "currentHealth"), 0.0001f);

                SetPrivateFloat(health, "_gasPhysiologyStress01", float.NaN);
                SetPrivateFloat(health, "_gasPhysiologyToxicity01", float.NaN);
                SetPrivateFloat(health, "_radiationExposureSeconds", float.NaN);
                Assert.AreEqual(0f, health.GasPhysiologyStress01, 0.0001f);
                Assert.AreEqual(0f, health.RadiationExposureSeconds, 0.0001f);
                Assert.AreEqual(0f, health.RadiationExposure, 0.0001f);
                Assert.AreEqual(0f, health.BloodToxicity01, 0.0001f);
                Assert.AreEqual(0f, health.Stress, 0.0001f);

                InvokePrivateFloat(health, "SetRadiationExposure", float.NaN);
                Assert.AreEqual(0f, GetPrivateFloat(health, "_radiationExposureSeconds"), 0.0001f);

                SetPrivateFloat(health, "_radiationExposureSeconds", float.NaN);
                InvokePrivateFloat(health, "ApplyRadiationExposure", 12f);
                Assert.AreEqual(12f, GetPrivateFloat(health, "_radiationExposureSeconds"), 0.0001f);

                Assert.AreEqual(1f, HectonPlayerHealth.ResolveRadiationFatigueScale(float.NaN), 0.0001f);
                Assert.AreEqual(1f, HectonPlayerHealth.ResolveRadiationFatigueScale(float.PositiveInfinity), 0.0001f);
                Assert.AreEqual(1f, HectonPlayerHealth.ResolveNaturalHealthRegenerationMultiplier(float.NaN), 0.0001f);
                Assert.AreEqual(1f, HectonPlayerHealth.ResolveNaturalHealthRegenerationMultiplier(float.NegativeInfinity), 0.0001f);
                Assert.AreEqual(0.35f, HectonPlayerHealth.ResolveNaturalHealthRegenerationMultiplier(1f), 0.0001f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void BinaryPlayerStatsCodecVersionGatesHealthField()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs");
            string writeBody = ExtractMethodBody(source, "private static bool WritePlayerStats(ref BufferWriter writer, PlayerStatsDTO value)");
            string readBody = ExtractMethodBody(source, "private static bool ReadPlayerStats(ref BufferReader reader, int version, out PlayerStatsDTO value)");
            string readHealthBody = ExtractMethodBody(source, "private static bool ReadPlayerHealth(ref BufferReader reader, int version, ref PlayerStatsDTO value)");

            int integrityWrite = writeBody.IndexOf("writer.WriteFloat(value.integrity)", StringComparison.Ordinal);
            int healthWrite = writeBody.IndexOf("writer.WriteFloat(value.health)", StringComparison.Ordinal);
            int weightWrite = writeBody.IndexOf("writer.WriteFloat(value.weight)", StringComparison.Ordinal);
            Assert.Greater(healthWrite, integrityWrite);
            Assert.Greater(weightWrite, healthWrite);

            StringAssert.Contains("ReadPlayerHealth(ref reader, version, ref value)", readBody);
            StringAssert.Contains("version < SaveData.PlayerHealthPersistenceVersion", readHealthBody);
            StringAssert.Contains("value.health = SaveData.PlayerHealthDefault;", readHealthBody);
            StringAssert.Contains("reader.ReadFloat(out value.health)", readHealthBody);
        }

        [Test]
        public void HectonPlayerHealthOwnsPlayerStatsHealthAndRegistersWithSaveService()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            StringAssert.Contains("public float CurrentHealth => ResolveSafeRuntimeHealth(currentHealth, ResolveSafeRuntimeMaxHealth(maxHealth));", source);
            StringAssert.Contains("public float MaxHealth => ResolveSafeRuntimeMaxHealth(maxHealth);", source);
            StringAssert.Contains("public bool IsAlive => CurrentHealth > 0f;", source);
            StringAssert.Contains("public float RadiationExposureSeconds => ResolveNonNegativeRuntimeValue(_radiationExposureSeconds);", source);

            string awake = ExtractMethodBody(source, "private void Awake()");
            string applyRadiationExposure = ExtractMethodBody(source, "internal void ApplyRadiationExposure(");
            string setRadiationExposure = ExtractMethodBody(source, "internal void SetRadiationExposure(");
            string applyRadiationExposureExact = ExtractMethodBody(source, "private void ApplyRadiationExposureExact(");
            string onEnable = ExtractMethodBody(source, "private void OnEnable()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string fullHeal = ExtractMethodBody(source, "public void FullHeal()");
            string setRuntimeMaxHealthScale = ExtractMethodBody(source, "private void SetRuntimeMaxHealthScaleInternal(");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");
            string hotSwap = ExtractMethodBody(source, "private void OnRegistryServiceReplaced(");
            string saveRegister = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");
            string saveUnregister = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsable = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");
            string combatHealthSync = ExtractMethodBody(source, "private bool TrySyncCombatDamageTargetHealth()");
            string survivalGraceEligibility = ExtractMethodBody(source, "internal static bool ShouldActivateSurvivalGrace(");
            string gasPhysiologyBridge = ExtractMethodBody(source, "private void UpdateGasPhysiologyBridge(");
            string radiationFatigueScale = ExtractMethodBody(source, "internal static float ResolveRadiationFatigueScale(");
            string naturalRegeneration = ExtractMethodBody(source, "internal static float ResolveNaturalHealthRegenerationMultiplier(");

            StringAssert.Contains("_baseMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);", awake);
            StringAssert.Contains("TryRegisterSaveParticipant();", onEnable);
            StringAssert.Contains("TryUnregisterSaveParticipant();", onDisable);
            StringAssert.Contains("TryUnregisterSaveParticipant();", onDestroy);
            StringAssert.Contains("Heal(ResolveSafeRuntimeMaxHealth(maxHealth));", fullHeal);
            StringAssert.Contains("float safeBaseMaxHealth = ResolveSafeRuntimeMaxHealth(_baseMaxHealth);", setRuntimeMaxHealthScale);
            StringAssert.Contains("float safeScale = math.isfinite(scale) ? scale : 1f;", setRuntimeMaxHealthScale);
            StringAssert.Contains("float nextCurrentHealth = ResolveSafeRuntimeHealth(currentHealth, nextMaxHealth);", setRuntimeMaxHealthScale);
            StringAssert.Contains("ResolveNonNegativeRuntimeValue(_radiationExposureSeconds)", applyRadiationExposure);
            StringAssert.Contains("ResolveNonNegativeRuntimeValue(exposureSeconds)", applyRadiationExposure);
            StringAssert.Contains("_radiationExposureSeconds = ResolveNonNegativeRuntimeValue(exposureSeconds);", setRadiationExposure);
            StringAssert.Contains("float safeExposureSeconds = ResolveNonNegativeRuntimeValue(exposureSeconds);", applyRadiationExposureExact);
            StringAssert.Contains("SomaticSurvivalMath.ResolveRadiationFatigueScale(ResolveNonNegativeRuntimeValue(exposureSeconds));", radiationFatigueScale);
            StringAssert.Contains("SomaticSurvivalMath.ResolveNaturalHealthRegenerationMultiplier(ResolveUnit01(toxicitySeverity01));", naturalRegeneration);
            StringAssert.Contains("case GlobalRegistryServiceSlot.Save:", hotSwap);
            StringAssert.Contains("TryRegisterSaveParticipant();", hotSwap);
            StringAssert.DoesNotContain("_saveService.Register(this);", hotSwap);
            AssertInitializedSaveOwnerRegistrationGate(
                saveRegister,
                saveUsable,
                "_saveService",
                "saveService.Register(this);",
                "_saveRegistered = true;");
            StringAssert.Contains("_registeredSaveService = saveService;", saveRegister);
            AssertRegisteredSaveOwnerUnregister(source, saveUnregister, "_saveService", "_saveRegistered");

            StringAssert.Contains("ref PlayerStatsDTO dto = ref data.playerStats;", populate);
            StringAssert.Contains("float runtimeMaxHealth = ResolveSafeRuntimeMaxHealth(maxHealth);", populate);
            StringAssert.Contains("dto.health = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);", populate);
            StringAssert.Contains("currentHealth = ResolveSafeRuntimeHealth(data.playerStats.health, runtimeMaxHealth);", load);
            StringAssert.Contains("MarkCombatDamageSyncDirty();", load);
            StringAssert.Contains("RefreshVitalWarningSignalReset();", load);
            StringAssert.Contains("TryIssueVitalWarningSignal();", load);
            StringAssert.Contains("return ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth) * math.rcp(runtimeMaxHealth);", source);
            StringAssert.Contains("float runtimeHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);", combatHealthSync);
            StringAssert.Contains("CombatDamageRuntime.SyncTargetHealth(_combatDamageTargetId, runtimeHealth, runtimeMaxHealth);", combatHealthSync);
            StringAssert.Contains("float runtimeHealth = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);", survivalGraceEligibility);
            StringAssert.Contains("float safeIncomingDamage = ResolveNonNegativeRuntimeValue(incomingDamage);", survivalGraceEligibility);
            StringAssert.Contains("float healthPercent = runtimeHealth * math.rcp(runtimeMaxHealth);", survivalGraceEligibility);
            StringAssert.Contains("public float RadiationExposure => ResolveUnit01(", source);
            StringAssert.Contains("public float GasPhysiologyStress01 => ResolveUnit01(_gasPhysiologyStress01);", source);
            StringAssert.Contains("ResolveUnit01(_gasPhysiologyToxicity01)", source);
            StringAssert.Contains("stress01 = Mathf.Max(stress01, ResolveUnit01(signal.PlayerStress01));", gasPhysiologyBridge);
            StringAssert.Contains("float signalStress01 = ResolveUnit01(signal.PlayerStress01);", gasPhysiologyBridge);
            StringAssert.Contains("float narcosis01 = ResolveUnit01(signal.Narcosis01);", gasPhysiologyBridge);
            StringAssert.Contains("float decay = ResolveNonNegativeRuntimeValue(deltaTime) * 0.5f;", gasPhysiologyBridge);
            StringAssert.DoesNotContain("CombatDamageRuntime.SyncTargetHealth(_combatDamageTargetId, currentHealth, maxHealth)", source);
        }

        [Test]
        public void HectonPlayerHealthMutationNotificationRefusalStaysDiagnosticAndDoesNotGateMutationState()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string evaluate = ExtractMethodBody(source, "private void EvaluateMutationThresholds()");
            string push = ExtractMethodBody(source, "private void TryPushMutationDetectedNotification()");
            string report = ExtractMethodBody(source, "private void ReportMutationDetectedNotificationMiss()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("using Hecton.Localization;", source);
            StringAssert.Contains("private static readonly uint _MutationDetectedNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _PlayerHealthNotificationContextHash", source);
            StringAssert.Contains("public int MutationDetectedNotificationMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                evaluate,
                "_mutationFlags |= threshold.MutationBit;",
                "ApplyMutationRuntimeEffects();",
                "TryPushMutationDetectedNotification();"));
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(_mutationDetectedMessageHash);", evaluate);
            StringAssert.Contains("if (NotificationEvents.TryPushRegisteredWarning(_mutationDetectedMessageHash))", push);
            StringAssert.Contains("ReportMutationDetectedNotificationMiss();", push);
            StringAssert.Contains("_mutationDetectedNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_MutationDetectedNotificationMissWarningHash", report);
            StringAssert.Contains("_PlayerHealthNotificationContextHash", report);
            StringAssert.Contains("math.max(1, _mutationDetectedNotificationMissCount)", report);
            StringAssert.Contains("ClearPlayerHealthNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_mutationDetectedNotificationMissCount", populate);
            StringAssert.DoesNotContain("_mutationDetectedNotificationMissCount", load);
        }

        [Test]
        public void HectonPlayerHealthSurvivalGraceNotificationRefusalStaysDiagnosticAndDoesNotGateOverride()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string activate = ExtractMethodBody(source, "private bool TryActivateSurvivalGrace(");
            string push = ExtractMethodBody(source, "private void TryPushSurvivalGraceNotification()");
            string report = ExtractMethodBody(source, "private void ReportSurvivalGraceNotificationMiss()");
            string clear = ExtractMethodBody(source, "private void ClearPlayerHealthNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("private const string SurvivalGraceNotification = \"CARDIAC OVERRIDE\";", source);
            StringAssert.Contains("private static readonly uint _SurvivalGraceNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _SurvivalGraceNotificationContextHash", source);
            StringAssert.Contains("public int SurvivalGraceNotificationMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                activate,
                "clampedDamage = Mathf.Max(0f, currentHealth - SurvivalGraceHealthFloor);",
                "ExtendInvulnerability(SurvivalGraceInvulnerabilitySeconds, now);",
                "_survivalGraceLockoutExpiresAt = ResolveExpirySeconds(now, SurvivalGraceLockoutSeconds);",
                "PlaySurvivalGraceHeartbeatPulse();",
                "TryPushSurvivalGraceNotification();",
                "return true;"));
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(\"CARDIAC OVERRIDE\".AsSpan());", activate);
            StringAssert.Contains("if (NotificationEvents.TryPushCritical(SurvivalGraceNotification.AsSpan()))", push);
            StringAssert.Contains("ReportSurvivalGraceNotificationMiss();", push);
            StringAssert.Contains("_survivalGraceNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_SurvivalGraceNotificationMissWarningHash", report);
            StringAssert.Contains("_PlayerHealthNotificationContextHash ^ _SurvivalGraceNotificationContextHash", report);
            StringAssert.Contains("math.max(1, _survivalGraceNotificationMissCount)", report);
            StringAssert.Contains("_mutationDetectedNotificationMissCount = 0;", clear);
            StringAssert.Contains("_survivalGraceNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearPlayerHealthNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearPlayerHealthNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearPlayerHealthNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_survivalGraceNotificationMissCount", populate);
            StringAssert.DoesNotContain("_survivalGraceNotificationMissCount", load);
        }

        [Test]
        public void HectonPlayerHealthRadiationAdvisoryFallbackRefusalStaysDiagnosticAfterNarrativeAndTrauma()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string advisory = ExtractMethodBody(source, "private void TryIssueRadiationAdvisory(");
            string show = ExtractMethodBody(source, "private void ShowRadiationAdvisory(");
            string push = ExtractMethodBody(source, "private void TryPushRadiationAdvisoryFallbackNotification(");
            string report = ExtractMethodBody(source, "private void ReportRadiationAdvisoryNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearPlayerHealthNotificationDiagnostics()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("private static readonly uint _RadiationAdvisoryNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _RadiationAdvisoryNotificationContextHash", source);
            StringAssert.Contains("public int RadiationAdvisoryNotificationMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                advisory,
                "issued = true;",
                "NarrativeEvents.TryRaiseDiscoveryMade(discoveryHash);",
                "TryRaiseTraumaHudSignal(",
                "ShowRadiationAdvisory(message, fallbackMessage, discoveryHash);"));
            StringAssert.Contains("audioLogs.NotifyAtmosphericWarningStarted(glitchDuration)", advisory);
            StringAssert.Contains("TryPushRadiationAdvisoryFallbackNotification(fallbackMessage.AsSpan(), discoveryHash);", show);
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(fallbackMessage.AsSpan());", show);
            StringAssert.Contains("if (NotificationEvents.TryPushCritical(message))", push);
            StringAssert.Contains("ReportRadiationAdvisoryNotificationMiss(discoveryHash);", push);
            StringAssert.Contains("_radiationAdvisoryNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_RadiationAdvisoryNotificationMissWarningHash", report);
            StringAssert.Contains("_PlayerHealthNotificationContextHash ^ _RadiationAdvisoryNotificationContextHash ^ discoveryHash", report);
            StringAssert.Contains("math.max(1, _radiationAdvisoryNotificationMissCount)", report);
            StringAssert.Contains("_radiationAdvisoryNotificationMissCount = 0;", clear);
            StringAssert.DoesNotContain("_radiationAdvisoryNotificationMissCount", populate);
            StringAssert.DoesNotContain("_radiationAdvisoryNotificationMissCount", load);
        }

        [Test]
        public void HectonPlayerHealthTraumaSignalRefusalStaysDiagnosticWithoutNoListenerNoise()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string advisory = ExtractMethodBody(source, "private void TryIssueRadiationAdvisory(");
            string vital = ExtractMethodBody(source, "private void TryIssueVitalWarningSignal()");
            string damage = ExtractMethodBody(source, "private void PublishDamageFeedback(");
            string leviathan = ExtractMethodBody(source, "private void TryIssueLeviathanTraumaAdvisory(");
            string raise = ExtractMethodBody(source, "private void TryRaiseTraumaHudSignal(");
            string report = ExtractMethodBody(source, "private void ReportPlayerSignalEventLaneDropIfBackpressured(");
            string clear = ExtractMethodBody(source, "private void ClearPlayerHealthSignalDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");

            StringAssert.Contains("private static readonly uint _PlayerSignalEventLaneDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _PlayerSignalEventLaneContextHash", source);
            StringAssert.Contains("private static readonly uint _RadiationAdvisoryTraumaSignalContextHash", source);
            StringAssert.Contains("private static readonly uint _VitalWarningTraumaSignalContextHash", source);
            StringAssert.Contains("private static readonly uint _DamageFeedbackTraumaSignalContextHash", source);
            StringAssert.Contains("private static readonly uint _LeviathanTraumaSignalContextHash", source);
            StringAssert.Contains("public int PlayerSignalEventLaneDropCount =>", source);

            StringAssert.Contains("TryRaiseTraumaHudSignal(", advisory);
            StringAssert.Contains("_RadiationAdvisoryTraumaSignalContextHash ^ discoveryHash", advisory);
            StringAssert.DoesNotContain("PlayerSignalEvents.TryRaiseTraumaHudSignal(", advisory);
            StringAssert.Contains("TryRaiseTraumaHudSignal(", vital);
            StringAssert.Contains("_VitalWarningTraumaSignalContextHash", vital);
            StringAssert.DoesNotContain("PlayerSignalEvents.TryRaiseTraumaHudSignal(", vital);
            StringAssert.Contains("TryRaiseTraumaHudSignal(", damage);
            StringAssert.Contains("_DamageFeedbackTraumaSignalContextHash ^ unchecked((uint)packet.SourceId)", damage);
            StringAssert.DoesNotContain("PlayerSignalEvents.TryRaiseTraumaHudSignal(", damage);
            StringAssert.Contains("TryRaiseTraumaHudSignal(", leviathan);
            StringAssert.Contains("_LeviathanTraumaSignalContextHash", leviathan);
            StringAssert.DoesNotContain("PlayerSignalEvents.TryRaiseTraumaHudSignal(", leviathan);

            StringAssert.Contains("if (PlayerSignalEvents.TryRaiseTraumaHudSignal(in signal))", raise);
            StringAssert.Contains("ReportPlayerSignalEventLaneDropIfBackpressured(contextHash);", raise);
            StringAssert.Contains("if (PlayerSignalEvents.PendingCount <= 0)", report);
            StringAssert.Contains("_playerSignalEventLaneDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_PlayerSignalEventLaneDropWarningHash", report);
            StringAssert.Contains("_PlayerSignalEventLaneContextHash ^ contextHash", report);
            StringAssert.Contains("math.max(1, _playerSignalEventLaneDropCount)", report);
            AssertTextBefore(report, "if (PlayerSignalEvents.PendingCount <= 0)", "_playerSignalEventLaneDropCount++;");

            StringAssert.Contains("_playerSignalEventLaneDropCount = 0;", clear);
            StringAssert.Contains("ClearPlayerHealthSignalDiagnostics();", onDisable);
            StringAssert.Contains("ClearPlayerHealthSignalDiagnostics();", onDestroy);
            StringAssert.Contains("ClearPlayerHealthSignalDiagnostics();", load);
            StringAssert.DoesNotContain("_playerSignalEventLaneDropCount", populate);
            StringAssert.DoesNotContain("_playerSignalEventLaneDropCount", load);
        }

        [Test]
        public void SharedPlayerStatsDtoKeepsSurvivalAndHealthOwnersSeparated()
        {
            string survivalSource = ReadProjectFile("Assets/_Project/Scripts/HectonSurvivalSystem.cs");
            string healthSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string survivalPopulate = ExtractMethodBody(survivalSource, "public void PopulateSaveData(SaveData data)");
            string survivalLoad = ExtractMethodBody(survivalSource, "public void LoadFromSaveData(SaveData data)");
            string healthPopulate = ExtractMethodBody(healthSource, "public void PopulateSaveData(SaveData data)");
            string healthLoad = ExtractMethodBody(healthSource, "public void LoadFromSaveData(SaveData data)");
            string playerKinematicApply = ExtractMethodBody(
                ReadProjectFile("Assets/_Project/Scripts/SaveData.cs"),
                "public void ApplyTo(ref PlayerStatsDTO stats)");

            StringAssert.Contains("public int SavePriority => 10;", survivalSource);
            StringAssert.Contains("public int LoadPriority => 10;", survivalSource);
            StringAssert.Contains("public int SavePriority => 100;", healthSource);
            StringAssert.Contains("public int LoadPriority => 100;", healthSource);
            StringAssert.DoesNotContain("dto.health", survivalPopulate);
            StringAssert.DoesNotContain("data.playerStats.health", survivalLoad);
            StringAssert.DoesNotContain("PlayerHealthDefault", survivalSource);

            StringAssert.Contains("dto.health = ResolveSafeRuntimeHealth(currentHealth, runtimeMaxHealth);", healthPopulate);
            StringAssert.Contains("currentHealth = ResolveSafeRuntimeHealth(data.playerStats.health, runtimeMaxHealth);", healthLoad);
            StringAssert.DoesNotContain("dto.oxygen", healthPopulate);
            StringAssert.DoesNotContain("dto.energy", healthPopulate);
            StringAssert.DoesNotContain("dto.integrity", healthPopulate);
            StringAssert.DoesNotContain("dto.SetPosition", healthPopulate);
            StringAssert.DoesNotContain("dto.SetVelocity", healthPopulate);
            StringAssert.DoesNotContain("stats.health", playerKinematicApply);
        }

        [Test]
        public void FatalHealthPathsDoNotHealWhenRespawnSignalPublicationFails()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs");
            string bridgeSource = ReadProjectFile("Assets/_Project/Scripts/Gameplay/PlayerDeathReconciliationBridge.cs");
            string takeDamage = ExtractMethodBody(source, "public bool TakeDamage(");
            string kill = ExtractMethodBody(source, "public void Kill()");
            string combatPacket = ExtractMethodBody(source, "private bool TryApplyAuthoritativeCombatDamagePacket(");
            string slowTick = ExtractMethodBody(source, "public void SlowTick()");
            string lateFrameTick = ExtractMethodBody(source, "public void LateFrameTick()");
            string flushPresentation = ExtractMethodBody(source, "private void FlushQueuedPresentationFeedback()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");
            string registryReplaced = ExtractMethodBody(source, "private void OnRegistryServiceReplaced(");
            string respawn = ExtractMethodBody(source, "private bool TryApplyRespawnReconciliation(");
            string consumeCommitted = ExtractMethodBody(source, "private void ConsumeCommittedRespawnReconciliationSignals()");
            string bridgeRequest = ExtractMethodBody(bridgeSource, "internal static bool RequestRespawn(double3 deathAup, uint damageHash, uint playerHash)");
            string bridgeRequestWithSequence = ExtractMethodBody(bridgeSource, "internal static bool RequestRespawn(double3 deathAup, uint damageHash, uint playerHash, out uint sequence)");
            string bridgeCommitted = ExtractMethodBody(bridgeSource, "internal static bool IsAcceptedCommittedRespawnSignal(");
            string healthReset = ExtractMethodBody(source, "private static void ResetStaticRuntimeState()");
            string bridgeReset = ExtractMethodBody(bridgeSource, "private static void ResetStaticState()");

            StringAssert.Contains(
                "_lastDamageTriggeredRespawnReconciliation = TryApplyRespawnReconciliation(HealthRespawnDamageHash);",
                takeDamage);
            StringAssert.Contains(
                "_lastDamageTriggeredRespawnReconciliation = TryApplyRespawnReconciliation(HealthRespawnDamageHash);",
                combatPacket);
            StringAssert.Contains("TryApplyRespawnReconciliation(HealthRespawnDamageHash);", kill);
            StringAssert.DoesNotContain("ApplyRespawnReconciliationHealth(1f);", takeDamage);
            StringAssert.DoesNotContain("ApplyRespawnReconciliationHealth(1f);", kill);
            StringAssert.DoesNotContain("ApplyRespawnReconciliationHealth(1f);", combatPacket);
            Assert.IsTrue(ContainsTokensInOrder(slowTick, "ConsumeCommittedRespawnReconciliationSignals();", "TryRegisterCombatDamageTarget();"));
            Assert.IsTrue(ContainsTokensInOrder(lateFrameTick, "ConsumeCommittedRespawnReconciliationSignals();", "FlushQueuedPresentationFeedback();"));
            Assert.IsTrue(ContainsTokensInOrder(flushPresentation, "_pendingRespawnReconciliationSequence == 0u", "TryUnregisterLateFrameTickable();"));
            StringAssert.Contains("ClearPendingRespawnReconciliation();", onDisable);
            StringAssert.Contains("ClearPendingRespawnReconciliation();", load);
            StringAssert.Contains("_pendingRespawnReconciliationSequence != 0u", registryReplaced);

            Assert.IsTrue(ContainsTokensInOrder(
                respawn,
                "bool hasDeathAup = TryResolveRespawnDeathAup(out double3 deathAup);",
                "if (!hasDeathAup)",
                "deathAup = MissingRespawnDeathAup();",
                "bool accepted = PlayerDeathReconciliationBridge.RequestRespawn(deathAup, damageHash, playerHash, out uint sequence);",
                "if (accepted)",
                "_pendingRespawnReconciliationSequence = sequence;",
                "TryRegisterLateFrameTickable();",
                "return true;",
                "return false;"));
            StringAssert.DoesNotContain("ApplyRespawnReconciliationHealth(1f);", respawn);
            Assert.IsTrue(ContainsTokensInOrder(
                consumeCommitted,
                "uint pendingSequence = _pendingRespawnReconciliationSequence;",
                "ReadOnlySpan<PlayerRespawnSignal> signals = SignalBus<PlayerRespawnSignal>.GetFrameSnapshot();",
                "PlayerDeathReconciliationBridge.IsAcceptedCommittedRespawnSignal(in signal, pendingSequence, playerHash)",
                "ApplyRespawnReconciliationHealth(1f);",
                "_lastAppliedRespawnReconciliationSequence = pendingSequence;",
                "_pendingRespawnReconciliationSequence = 0u;"));
            Assert.IsTrue(ContainsTokensInOrder(
                bridgeRequest,
                "return RequestRespawn(deathAup, damageHash, playerHash, out _);"));
            Assert.IsTrue(ContainsTokensInOrder(
                bridgeRequestWithSequence,
                "sequence = 0u;",
                "uint nextSequence = ++s_sequence;",
                "sequence = nextSequence;",
                "bool deathAupFinite = math.all(math.isfinite(deathAup));",
                "if (!deathAupFinite)",
                "signal.Flags |= PlayerRespawnSignalFlags.InvalidDeathAup | PlayerRespawnSignalFlags.InvalidTargetAup;",
                "bool pushed = SignalBus<PlayerRespawnSignal>.TryPushTracked(",
                "if (!pushed)",
                "pushed = SignalBus<PlayerRespawnSignal>.TryPushTracked(",
                "return pushed;"));
            Assert.IsTrue(ContainsTokensInOrder(
                bridgeCommitted,
                "signal.Phase != PlayerRespawnSignalPhase.Committed",
                "(flags & PlayerRespawnSignalFlags.Committed) == 0u",
                "!math.all(math.isfinite(signal.DeathAUP))",
                "!math.all(math.isfinite(signal.RespawnAUP))",
                "return playerHash == 0u || signal.PlayerHash == 0u || signal.PlayerHash == playerHash;"));
            StringAssert.DoesNotContain("return deathAupFinite && pushed;", bridgeRequestWithSequence);
            StringAssert.DoesNotContain("(flags & PlayerRespawnSignalFlags.InvalidDeathAup) != 0u", bridgeCommitted);
            StringAssert.DoesNotContain("return true;", bridgeRequestWithSequence);
            StringAssert.Contains("s_x001HectonPlayerHealthSignalPushDropCount = 0;", healthReset);
            StringAssert.Contains(
                "s_x001DirectSignalPushDropCount_PlayerDeathReconciliationBridge = 0;",
                bridgeReset);
        }

        private static SaveData WriteReadBinary(SaveData data)
        {
            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);

                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restoredData,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                return restoredData;
            }
        }

        private static void SetPrivateFloat(object target, string fieldName, float value)
        {
            ResolvePrivateField(target, fieldName).SetValue(target, value);
        }

        private static float GetPrivateFloat(object target, string fieldName)
        {
            return (float)ResolvePrivateField(target, fieldName).GetValue(target);
        }

        private static void SetPrivateUInt(object target, string fieldName, uint value)
        {
            ResolvePrivateField(target, fieldName).SetValue(target, value);
        }

        private static uint GetPrivateUInt(object target, string fieldName)
        {
            return (uint)ResolvePrivateField(target, fieldName).GetValue(target);
        }

        private static FieldInfo ResolvePrivateField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing private field: " + fieldName);
            return field;
        }

        private static void InvokePrivateFloat(object target, string methodName, float value)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, "Missing private method: " + methodName);
            method.Invoke(target, new object[] { value });
        }

        private static string ReadProjectFile(string relativePath)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(root, relativePath));
        }

        private static void AssertInitializedSaveOwnerRegistrationGate(
            string register,
            string usable,
            string saveServiceField,
            string registerCall,
            string registeredFlagAssignment)
        {
            Assert.IsTrue(ContainsTokensInOrder(
                register,
                "ISaveService saveService = " + saveServiceField + ";",
                "if (!IsSaveServiceUsable(saveService))",
                "saveService = GlobalRegistry.Save;",
                saveServiceField + " = saveService;",
                "if (!IsSaveServiceUsable(saveService))",
                "return;",
                registerCall,
                registeredFlagAssignment));
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", usable);
            StringAssert.DoesNotContain("if (" + saveServiceField + " == null)", register);
            StringAssert.DoesNotContain("if (saveService == null)", register);
        }

        private static void AssertRegisteredSaveOwnerUnregister(
            string source,
            string unregister,
            string saveServiceField,
            string registeredFlagName)
        {
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            Assert.IsTrue(ContainsTokensInOrder(
                unregister,
                "if (!" + registeredFlagName + " && _registeredSaveService == null)",
                "return;",
                "ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : " + saveServiceField + ";",
                "if (saveService != null)",
                "saveService.Unregister(this);",
                "_registeredSaveService = null;",
                registeredFlagName + " = false;"));
            StringAssert.DoesNotContain("ISaveService saveService = " + saveServiceField + ";", unregister);
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
        }

        private static void AssertTextBefore(string text, string expectedEarlier, string expectedLater)
        {
            int earlier = text.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int later = text.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlier, 0, "Missing earlier token: " + expectedEarlier);
            Assert.GreaterOrEqual(later, 0, "Missing later token: " + expectedLater);
            Assert.Less(earlier, later, expectedEarlier + " must appear before " + expectedLater);
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
    }
}
