using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Narrative;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class Atlas6LiabilityEditTests
    {
        [Test]
        public void HaldaneLockout_IsOneShotUntilDecontaminated()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ExtractionGatingSystem extraction = new ExtractionGatingSystem(telemetry);
            int lockoutEvents = 0;
            int carrierArrivals = 0;
            extraction.OnQuarantineLockoutHaldane += () => lockoutEvents++;
            extraction.OnCarrierArrived += () => carrierArrivals++;

            Assert.IsTrue(extraction.RequestExtractionTether(500f, false));
            extraction.AddBiomatterExposure(20f);

            Assert.IsFalse(extraction.AttemptBoardingSequence());
            Assert.IsFalse(extraction.AttemptBoardingSequence());
            Assert.AreEqual(1, lockoutEvents);
            Assert.IsTrue(extraction.IsHaldaneLockoutActive);

            extraction.ProcessMakeshiftDecontamination(20f);
            Assert.IsFalse(extraction.IsHaldaneLockoutActive);
            Assert.IsTrue(extraction.AttemptBoardingSequence());
            Assert.AreEqual(1, carrierArrivals);
        }

        [Test]
        public void ExtractionGating_RejectsInvalidNumericInputs()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ExtractionGatingSystem extraction = new ExtractionGatingSystem(telemetry);

            extraction.AddBiomatterExposure(float.NaN);
            extraction.AddBiomatterExposure(-5f);
            Assert.AreEqual(0f, extraction.BiomatterExposureLevel);

            Assert.IsFalse(extraction.RequestExtractionTether(float.NaN, false));
            Assert.AreEqual(ExtractionCarrierState.Offline, extraction.CarrierState);

            extraction.AddBiomatterExposure(20f);
            extraction.ProcessMakeshiftDecontamination(float.PositiveInfinity);
            extraction.ProcessMakeshiftDecontamination(-20f);
            Assert.AreEqual(20f, extraction.BiomatterExposureLevel);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidDecontaminationReported, latest.EventCode);
        }

        [Test]
        public void ActuarialThreat_IsRaisedOnce()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            actuarial.Initialize(5000f);
            int threatEvents = 0;
            int droneHaltEvents = 0;
            actuarial.OnPlayerFlaggedAsActuarialThreat += () => threatEvents++;
            actuarial.OnDroneRepairCyclesHalted += () => droneHaltEvents++;

            for (int i = 0; i < 7; i++)
                actuarial.RegisterWorkerTagRecoveryHash((uint)(100 + i));

            Assert.IsTrue(actuarial.IsPlayerActuarialThreat);
            Assert.AreEqual(1, threatEvents);
            Assert.AreEqual(1, droneHaltEvents);
        }

        [Test]
        public void ActuarialWorkerTagRecovery_DeduplicatesWorkerHash()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            actuarial.Initialize(5000f);
            uint workerHash = Atlas6LiabilityTelemetry.ComputeStableHash("worker");

            Assert.IsTrue(actuarial.RegisterWorkerTagRecoveryHash(workerHash));
            Assert.IsFalse(actuarial.RegisterWorkerTagRecoveryHash(workerHash));

            Assert.AreEqual(1, actuarial.RecoveredWorkerTags);
            Assert.AreEqual(15.5f, actuarial.CorporateHostilityIndex);
            Assert.AreEqual(1u, telemetry.LatestSequence);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual(workerHash, latest.SubjectHash);
        }

        [Test]
        public void ActuarialWorkerTagRecovery_RestoreKeepsDedupe()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            uint workerHash = Atlas6CorporateLiabilityManager.ChenMWorkerTagHash;
            uint[] restoredHashes = new uint[SaveData.MaxAtlas6LiabilityWorkerTags];
            restoredHashes[0] = workerHash;

            actuarial.RestoreState(4700f, 15.5f, restoredHashes, 1);

            Assert.AreEqual(1, actuarial.RecoveredWorkerTags);
            Assert.AreEqual(4700f, actuarial.CorporateCreditBalance);
            Assert.AreEqual(15.5f, actuarial.CorporateHostilityIndex);
            Assert.IsFalse(actuarial.RegisterWorkerTagRecoveryHash(workerHash));
            Assert.AreEqual(1, actuarial.RecoveredWorkerTags);
            Assert.AreEqual(0u, telemetry.LatestSequence);
        }

        [Test]
        public void Atlas6Directive_ExternalThreatCannotDowngrade()
        {
            GameObject host = new GameObject("Atlas6Directive_ExternalThreatCannotDowngrade");
            host.SetActive(false);
            Atlas6DirectiveSystem directive = host.AddComponent<Atlas6DirectiveSystem>();
            try
            {
                directive.OnAtlas6Event(new Atlas6EventPayload
                {
                    EventType = (ushort)Atlas6EventType.PlayerStatusChanged,
                    StatusValue = (ushort)Atlas6PlayerStatus.Threat
                });

                Assert.AreEqual(Atlas6PlayerStatus.Threat, directive.PlayerStatus);

                directive.OnAtlas6Event(new Atlas6EventPayload
                {
                    EventType = (ushort)Atlas6EventType.PlayerStatusChanged,
                    StatusValue = (ushort)Atlas6PlayerStatus.Neutral
                });

                Assert.AreEqual(Atlas6PlayerStatus.Threat, directive.PlayerStatus);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Atlas6Events_RejectInvalidStatusAndBarterIngress()
        {
            Assert.IsFalse(Atlas6Events.TryRaisePlayerStatusChanged((Atlas6PlayerStatus)(-1)));
            Assert.IsFalse(Atlas6Events.TryRaisePlayerStatusChanged((Atlas6PlayerStatus)99));
            Assert.IsFalse(Atlas6Events.TryRaiseBarterAccepted(0));
            Assert.IsFalse(Atlas6Events.TryRaiseBarterAccepted(-1));
        }

        [Test]
        public void AtlasSignalEvents_SanitizesFinitePayloadIngress()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalEvents.cs"));
            string pulse = ExtractMethodBody(source, "public static bool TryRaisePulse(float intensity)");
            string detected = ExtractMethodBody(source, "public static bool TryRaiseDetected(Vector3 sourcePos)");
            string strength = ExtractMethodBody(source, "public static bool TryRaiseStrengthChanged(float strength)");
            string enqueue = ExtractMethodBody(source, "private static bool Enqueue(in AtlasSignalEventPayload payload)");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizePayload(");
            string eventTypeGuard = ExtractMethodBody(source, "private static bool IsKnownEventType(ushort eventType)");
            string vectorGuard = ExtractMethodBody(source, "private static bool IsFinite(Vector3 value)");

            StringAssert.Contains("!math.isfinite(intensity)", pulse);
            StringAssert.Contains("math.saturate(intensity)", pulse);
            StringAssert.Contains("!IsFinite(sourcePos)", detected);
            StringAssert.Contains("!math.isfinite(strength)", strength);
            StringAssert.Contains("math.saturate(strength)", strength);
            StringAssert.Contains("TrySanitizePayload(in payload, out AtlasSignalEventPayload safePayload)", enqueue);
            StringAssert.Contains("ReportQueueOverflow(safePayload.EventType)", enqueue);
            StringAssert.Contains("IsKnownEventType(payload.EventType)", sanitize);
            StringAssert.Contains("math.saturate(payload.SignalStrength)", sanitize);
            StringAssert.Contains("IsFinite(payload.SourcePosition)", sanitize);
            StringAssert.Contains("payload.MessageHash == 0u", sanitize);
            StringAssert.Contains("return eventType <= (ushort)AtlasSignalEventType.Decoded;", eventTypeGuard);
            StringAssert.Contains("math.isfinite(value.x)", vectorGuard);
            StringAssert.Contains("math.isfinite(value.y)", vectorGuard);
            StringAssert.Contains("math.isfinite(value.z)", vectorGuard);
        }

        [Test]
        public void Atlas6Directive_LoadFromSaveDataSanitizesInvalidStatusAndBarterCount()
        {
            GameObject host = new GameObject("Atlas6Directive_LoadSanitizesInvalidStatus");
            host.SetActive(false);
            Atlas6DirectiveSystem directive = host.AddComponent<Atlas6DirectiveSystem>();
            try
            {
                SaveData data = SaveData.CreateNew(0d);
                data.atlas6PlayerStatus = 99;
                data.atlas6BarterCount = -12;

                directive.LoadFromSaveData(data);

                Assert.AreEqual(Atlas6PlayerStatus.Unknown, directive.PlayerStatus);
                Assert.AreEqual(0, directive.BarterTransactionCount);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Atlas6Directive_RuntimeBarterCountSanitizesCorruptValues()
        {
            GameObject host = new GameObject("Atlas6Directive_RuntimeBarterSanitize");
            host.SetActive(false);
            Atlas6DirectiveSystem directive = host.AddComponent<Atlas6DirectiveSystem>();
            try
            {
                SetPrivateInstanceField(directive, "_barterTransactionCount", int.MaxValue);
                directive.RegisterBarterTransaction();
                Assert.AreEqual(int.MaxValue, directive.BarterTransactionCount);

                SetPrivateInstanceField(directive, "_barterTransactionCount", -7);
                SetPrivateInstanceField(directive, "collaboratorThreshold", 0);
                SetPrivateInstanceField(directive, "_playerStatus", Atlas6PlayerStatus.Collaborator);
                Assert.AreEqual(0, directive.BarterTransactionCount);
                Assert.AreEqual(0f, directive.TrustLevel);

                directive.RegisterBarterTransaction();
                Assert.AreEqual(1, directive.BarterTransactionCount);

                SaveData data = SaveData.CreateNew(0d);
                directive.PopulateSaveData(data);
                Assert.AreEqual(1, data.atlas6BarterCount);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtlasSignal_LoadFromSaveDataSanitizesPulseTimerAndRevealStage()
        {
            GameObject host = new GameObject("AtlasSignal_LoadSanitizesPulseTimer");
            host.SetActive(false);
            AtlasSignalSystem signal = host.AddComponent<AtlasSignalSystem>();
            try
            {
                SaveData data = SaveData.CreateNew(0d);
                data.atlasSignalDetected = true;
                data.atlasSignalPulseTimer = float.NaN;
                data.atlasSignalRevealStage = 99;

                signal.LoadFromSaveData(data);

                SaveData output = SaveData.CreateNew(0d);
                signal.PopulateSaveData(output);
                Assert.AreEqual(0f, output.atlasSignalPulseTimer);
                Assert.AreEqual(4, output.atlasSignalRevealStage);
                Assert.IsTrue(output.atlasSignalDetected);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtlasSignal_ReadModelClampsCorruptRevealStage()
        {
            GameObject host = new GameObject("AtlasSignal_ReadModelSanitizesReveal");
            host.SetActive(false);
            AtlasSignalSystem signal = host.AddComponent<AtlasSignalSystem>();
            try
            {
                SetPrivateInstanceField(signal, "_maxRevealStageUnlocked", 99);

                Assert.AreEqual(4, signal.CurrentAtlasSignalRevealStage);
                Assert.AreEqual(4, signal.CurrentRevealStage);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtlasSignal_ReadModelFailsClosedForCorruptStrength()
        {
            GameObject host = new GameObject("AtlasSignal_ReadModelSanitizesStrength");
            host.SetActive(false);
            AtlasSignalSystem signal = host.AddComponent<AtlasSignalSystem>();
            try
            {
                SetPrivateInstanceField(signal, "_maxRevealStageUnlocked", 4);
                SetPrivateInstanceField(signal, "_currentStrength", float.PositiveInfinity);
                SetPrivateInstanceField(signal, "_currentStrengthBand", 99);

                Assert.AreEqual(0f, signal.CurrentStrength);
                Assert.AreEqual(0f, signal.CurrentAtlasSignalStrength01);
                Assert.AreEqual(4, signal.CurrentStrengthBand);
                Assert.IsFalse(signal.IsDetected);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtlasSignal_CoreAupFailsClosedForCorruptAuthoredPosition()
        {
            GameObject host = new GameObject("AtlasSignal_CoreAupSanitizesPosition");
            host.SetActive(false);
            AtlasSignalSystem signal = host.AddComponent<AtlasSignalSystem>();
            try
            {
                SetPrivateInstanceField(signal, "atlasCorePosWorld", new Vector3(float.NaN, -5000f, 0f));

                Assert.IsFalse(signal.TryReadAtlasSignalCoreAup(out _));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AtlasSignal_CoreAupValidityRoutesAllNavigationReads()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"));
            string tryReadCore = ExtractMethodBody(source, "public bool TryReadAtlasSignalCoreAup(out AbsoluteUniversePosition coreAup)");
            string snapshot = ExtractMethodBody(source, "public bool TryReadAtlasSignalSnapshot(");
            string direction = ExtractMethodBody(source, "public Vector3 DirectionToCore");
            string slowTick = ExtractMethodBody(source, "private void SlowTickCore()");
            string tryResolve = ExtractMethodBody(source, "private bool TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup)");

            StringAssert.Contains("return TryResolveAtlasCoreAup(out coreAup);", tryReadCore);
            StringAssert.Contains("TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup)", snapshot);
            StringAssert.Contains("TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup)", direction);
            StringAssert.Contains("TryResolveAtlasCoreAup(out AbsoluteUniversePosition coreAup)", slowTick);
            StringAssert.Contains("_atlasCoreAupValid && coreAup.IsFinite()", tryResolve);
        }

        [Test]
        public void AtlasSignalStrength_FailsClosedForNonFiniteInputs()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"));
            string calculateStrength = ExtractMethodBody(source, "public static float CalculateStrength(");
            string strengthToBand = ExtractMethodBody(source, "public static int StrengthToBand(float strength01)");

            StringAssert.Contains("!math.isfinite(maxRangeMeters)", calculateStrength);
            StringAssert.Contains("maxRangeMeters <= 0f", calculateStrength);
            StringAssert.Contains("!math.isfinite(distanceSq)", calculateStrength);
            StringAssert.Contains("math.isfinite(strength) ? math.saturate(strength) : 0f", calculateStrength);
            StringAssert.Contains("!math.isfinite(strength01)", strengthToBand);
            StringAssert.Contains("return 0;", strengthToBand);
        }

        [Test]
        public void AtlasSignalPulseTimer_FailsClosedForNonFiniteRuntimeState()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"));
            string slowTick = ExtractMethodBody(source, "private void SlowTickCore()");
            string resolvePeriod = ExtractMethodBody(source, "private float ResolvePulsePeriodSeconds()");

            StringAssert.Contains("private const float DefaultPulsePeriodSeconds = 683f;", source);
            StringAssert.Contains("_pulseTimer = math.isfinite(_pulseTimer)", slowTick);
            StringAssert.Contains("ResolvePulsePeriodSeconds()", slowTick);
            StringAssert.Contains("math.isfinite(pulsePeriodSeconds) && pulsePeriodSeconds > 0f", resolvePeriod);
            StringAssert.Contains("DefaultPulsePeriodSeconds", resolvePeriod);
        }

        [Test]
        public void AtlasSignalDetectionThreshold_UsesFiniteDefaultResolver()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"));
            string resolveThreshold = ExtractMethodBody(source, "private float ResolveDetectionThreshold()");

            StringAssert.Contains("private const float DefaultDetectionThreshold = 0.05f;", source);
            StringAssert.Contains("_currentStrength >= ResolveDetectionThreshold()", source);
            StringAssert.Contains("strength >= detectionThreshold01", source);
            StringAssert.Contains("newStrength >= detectionThreshold01", source);
            StringAssert.Contains("manifestedStrength >= ResolveDetectionThreshold()", source);
            StringAssert.Contains("math.isfinite(detectionThreshold)", resolveThreshold);
            StringAssert.Contains("DefaultDetectionThreshold", resolveThreshold);
            StringAssert.DoesNotContain(">= detectionThreshold)", source);
        }

        [Test]
        public void AtlasRuntimePlayerAupResolvers_FailClosedForNonFiniteCurrentAup()
        {
            string signalSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs"));
            string directiveSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs"));
            string signalPlayerAup = ExtractMethodBody(signalSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");
            string directivePlayerAup = ExtractMethodBody(directiveSource, "private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");

            StringAssert.Contains("playerAup = _playerMovement.CurrentAup;", signalPlayerAup);
            StringAssert.Contains("return playerAup.IsFinite();", signalPlayerAup);
            StringAssert.DoesNotContain("return true;", signalPlayerAup);
            StringAssert.Contains("playerAup = _playerMovement.CurrentAup;", directivePlayerAup);
            StringAssert.Contains("return playerAup.IsFinite();", directivePlayerAup);
            StringAssert.DoesNotContain("return true;", directivePlayerAup);
        }

        [Test]
        public void ActuarialGhostUpload_RejectsInvalidDataSize()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            actuarial.Initialize(5000f);

            actuarial.UploadGhostPDAData(float.NaN);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord nonFiniteRecord));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidGhostPDADataReported, nonFiniteRecord.EventCode);
            Assert.AreNotEqual(0u, nonFiniteRecord.FaultFlags & (uint)Atlas6LiabilityFaultFlags.NonFiniteInput);

            actuarial.UploadGhostPDAData(-1f);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord negativeRecord));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidGhostPDADataReported, negativeRecord.EventCode);
            Assert.AreNotEqual(0u, negativeRecord.FaultFlags & (uint)Atlas6LiabilityFaultFlags.InvalidRangeInput);

            actuarial.UploadGhostPDAData(float.MaxValue);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord overflowRecord));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidGhostPDADataReported, overflowRecord.EventCode);
            Assert.AreNotEqual(0u, overflowRecord.FaultFlags & (uint)Atlas6LiabilityFaultFlags.InvalidRangeInput);

            Assert.AreEqual(5000f, actuarial.CorporateCreditBalance);
        }

        [Test]
        public void ActuarialGhostUpload_DeductsOnlyAvailableCorporateCredit()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            float deducted = -1f;
            actuarial.Initialize(10f);
            actuarial.OnCorporateCreditDeducted += value => deducted = value;

            actuarial.UploadGhostPDAData(1f);

            Assert.AreEqual(0f, actuarial.CorporateCreditBalance);
            Assert.AreEqual(10f, deducted);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.CorporateCreditDeducted, latest.EventCode);
            Assert.AreEqual(10f, latest.Value0);
            Assert.AreEqual(0f, latest.Value1);
        }

        [Test]
        public void CorporateLiability_ClampsAccumulatedXenonOmegaYield()
        {
            GameObject host = new GameObject("CorporateLiability_ClampsAccumulatedXenonOmegaYield");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                manager.ReportXenonOmegaExtracted(Atlas6CorporateLiabilityManager.MaximumTrackedSectorXenonOmegaYield);
                manager.ReportXenonOmegaExtracted(Atlas6CorporateLiabilityManager.MaximumTrackedSectorXenonOmegaYield);

                Assert.AreEqual(
                    Atlas6CorporateLiabilityManager.MaximumTrackedSectorXenonOmegaYield,
                    manager.SectorXenonOmegaYield);
                Assert.IsTrue(manager.TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord latest));
                Assert.AreEqual((ushort)Atlas6LiabilityEventCode.XenonOmegaYieldReported, latest.EventCode);
                Assert.AreNotEqual(0u, latest.FaultFlags & (uint)Atlas6LiabilityFaultFlags.InvalidRangeInput);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorporateLiability_XenonOmegaExtractionAddsBiomatterExposure()
        {
            GameObject host = new GameObject("CorporateLiability_XenonOmegaExtractionAddsBiomatterExposure");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                manager.ReportXenonOmegaExtracted(100f);

                Assert.AreEqual(100f, manager.SectorXenonOmegaYield);
                Assert.AreEqual(
                    100f * Atlas6CorporateLiabilityManager.XenonOmegaBiomatterExposurePerYieldUnit,
                    manager.ExtractionGating.BiomatterExposureLevel);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorporateLiability_RecognizesOnlyXenonOmegaTemplateHash()
        {
            int xenonOmegaHash = Atlas6CorporateLiabilityManager.XenonOmegaVentCacheStableHashId;

            Assert.AreNotEqual(0, xenonOmegaHash);
            Assert.IsTrue(Atlas6CorporateLiabilityManager.IsXenonOmegaResourceTemplateHash(xenonOmegaHash));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsXenonOmegaResourceTemplateHash(0));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsXenonOmegaResourceTemplateHash(xenonOmegaHash ^ 0x5A5A));
        }

        [Test]
        public void CorporateLiability_RecognizesOnlyAtlas6EvidenceAudioLogHash()
        {
            uint evidenceHash = Atlas6CorporateLiabilityManager.Atlas6TerminalSector3AudioLogHash;

            Assert.AreNotEqual(0u, evidenceHash);
            Assert.IsTrue(Atlas6CorporateLiabilityManager.IsAtlas6DisasterEvidenceAudioLogHash(evidenceHash));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsAtlas6DisasterEvidenceAudioLogHash(0u));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsAtlas6DisasterEvidenceAudioLogHash(evidenceHash ^ 0xA5A5u));
        }

        [Test]
        public void CorporateLiability_RecognizesOnlyChenWorkerTagDiscoveryHash()
        {
            uint workerTagDiscoveryHash = Atlas6CorporateLiabilityManager.ChenMSuitDiscoveryHash;

            Assert.AreNotEqual(0u, workerTagDiscoveryHash);
            Assert.IsTrue(Atlas6CorporateLiabilityManager.IsAtlas6WorkerTagDiscoveryHash(workerTagDiscoveryHash));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsAtlas6WorkerTagDiscoveryHash(0u));
            Assert.IsFalse(Atlas6CorporateLiabilityManager.IsAtlas6WorkerTagDiscoveryHash(workerTagDiscoveryHash ^ 0xA5A5u));
        }

        [Test]
        public void CorporateLiability_AudioLogDiscoveryCollectsEvidenceOnce()
        {
            GameObject host = new GameObject("CorporateLiability_AudioLogDiscoveryCollectsEvidenceOnce");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                manager.OnAudioLogEvent(new AudioLogEventPayload
                {
                    Type = AudioLogEventType.PlaybackStarted,
                    LogHash = Atlas6CorporateLiabilityManager.Atlas6TerminalSector3AudioLogHash
                });

                Assert.IsFalse(manager.HasDisasterEvidenceInInventory);
                Assert.AreEqual(0u, manager.Telemetry.LatestSequence);

                manager.OnAudioLogEvent(new AudioLogEventPayload
                {
                    Type = AudioLogEventType.Discovered,
                    LogHash = Atlas6CorporateLiabilityManager.Atlas6TerminalSector3AudioLogHash
                });

                Assert.IsTrue(manager.HasDisasterEvidenceInInventory);
                Assert.IsTrue(manager.TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord latest));
                Assert.AreEqual((ushort)Atlas6LiabilityEventCode.DisasterEvidenceCollected, latest.EventCode);
                Assert.AreEqual(1u, manager.Telemetry.LatestSequence);

                manager.OnAudioLogEvent(new AudioLogEventPayload
                {
                    Type = AudioLogEventType.Discovered,
                    LogHash = Atlas6CorporateLiabilityManager.Atlas6TerminalSector3AudioLogHash
                });

                Assert.AreEqual(1u, manager.Telemetry.LatestSequence);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorporateLiability_NarrativeDiscoveryScansChenWorkerTagOnce()
        {
            GameObject host = new GameObject("CorporateLiability_NarrativeDiscoveryScansChenWorkerTagOnce");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                manager.OnNarrativeEvent(new NarrativeEventPayload
                {
                    EventType = (ushort)NarrativeEventType.DepthTierReached,
                    DiscoveryHash = Atlas6CorporateLiabilityManager.ChenMSuitDiscoveryHash
                });

                Assert.AreEqual(0, manager.ActuarialLiability.RecoveredWorkerTags);
                Assert.AreEqual(0u, manager.Telemetry.LatestSequence);

                manager.OnNarrativeEvent(new NarrativeEventPayload
                {
                    EventType = (ushort)NarrativeEventType.DiscoveryMade,
                    DiscoveryHash = Atlas6CorporateLiabilityManager.ChenMSuitDiscoveryHash
                });

                Assert.AreEqual(1, manager.ActuarialLiability.RecoveredWorkerTags);
                Assert.IsTrue(manager.TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord latest));
                Assert.AreEqual((ushort)Atlas6LiabilityEventCode.WorkerTagRecovered, latest.EventCode);
                Assert.AreEqual(Atlas6CorporateLiabilityManager.ChenMWorkerTagHash, latest.SubjectHash);
                Assert.AreEqual(1u, manager.Telemetry.LatestSequence);

                manager.OnNarrativeEvent(new NarrativeEventPayload
                {
                    EventType = (ushort)NarrativeEventType.DiscoveryMade,
                    DiscoveryHash = Atlas6CorporateLiabilityManager.ChenMSuitDiscoveryHash
                });

                Assert.AreEqual(1, manager.ActuarialLiability.RecoveredWorkerTags);
                Assert.AreEqual(1u, manager.Telemetry.LatestSequence);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorporateLiability_SaveLoadRestoresRuntimeStateAndWorkerDedupe()
        {
            GameObject sourceHost = new GameObject("CorporateLiability_SaveLoad_Source");
            Atlas6CorporateLiabilityManager source = sourceHost.AddComponent<Atlas6CorporateLiabilityManager>();
            SaveData data = SaveData.CreateNew(0d);
            try
            {
                source.ReportXenonOmegaExtracted(750f);
                source.ReportWorkerTagScannedHash(Atlas6CorporateLiabilityManager.ChenMWorkerTagHash);
                source.ReportGhostPDADataUploaded(6f);
                source.ReportDisasterEvidenceCollected();
                Assert.IsFalse(source.AttemptCarrierTether());

                source.PopulateSaveData(data);
            }
            finally
            {
                Object.DestroyImmediate(sourceHost);
            }

            GameObject targetHost = new GameObject("CorporateLiability_SaveLoad_Target");
            Atlas6CorporateLiabilityManager target = targetHost.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                target.LoadFromSaveData(data);

                Assert.AreEqual(750f, target.SectorXenonOmegaYield);
                Assert.IsTrue(target.HasDisasterEvidenceInInventory);
                Assert.AreEqual(1, target.ActuarialLiability.RecoveredWorkerTags);
                Assert.AreEqual(15.5f, target.ActuarialLiability.CorporateHostilityIndex);
                Assert.AreEqual(4700f, target.ActuarialLiability.CorporateCreditBalance);
                Assert.AreEqual(ExtractionCarrierState.TetherSevered, target.ExtractionGating.CarrierState);
                Assert.AreEqual(
                    750f * Atlas6CorporateLiabilityManager.XenonOmegaBiomatterExposurePerYieldUnit,
                    target.ExtractionGating.BiomatterExposureLevel);

                target.OnNarrativeEvent(new NarrativeEventPayload
                {
                    EventType = (ushort)NarrativeEventType.DiscoveryMade,
                    DiscoveryHash = Atlas6CorporateLiabilityManager.ChenMSuitDiscoveryHash
                });

                Assert.AreEqual(1, target.ActuarialLiability.RecoveredWorkerTags);
            }
            finally
            {
                Object.DestroyImmediate(targetHost);
            }
        }

        [Test]
        public void CorporateLiability_LazyActuarialRewirePublishesManagerThreat()
        {
            GameObject host = new GameObject("CorporateLiability_LazyActuarialRewire");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                SetPrivateInstanceProperty(manager, nameof(Atlas6CorporateLiabilityManager.ActuarialLiability), null);
                InvokePrivateInstanceMethod(manager, "EnsureSubsystemsInitialized");

                for (int i = 0; i < 5; i++)
                    manager.ReportWorkerTagScannedHash((uint)(1000 + i));

                Assert.IsTrue(manager.TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord latest));
                Assert.AreEqual((ushort)Atlas6LiabilityEventCode.ActuarialThreatRaised, latest.EventCode);
                Assert.AreEqual(Atlas6LiabilityTelemetry.ManagerContextHash, latest.ContextHash);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CorporateLiability_LazyExtractionRewirePublishesManagerSatoRen()
        {
            GameObject host = new GameObject("CorporateLiability_LazyExtractionRewire");
            Atlas6CorporateLiabilityManager manager = host.AddComponent<Atlas6CorporateLiabilityManager>();
            try
            {
                manager.ReportXenonOmegaExtracted(750f);
                manager.ReportDisasterEvidenceCollected();
                SetPrivateInstanceProperty(manager, nameof(Atlas6CorporateLiabilityManager.ExtractionGating), null);
                InvokePrivateInstanceMethod(manager, "EnsureSubsystemsInitialized");

                Assert.IsFalse(manager.AttemptCarrierTether());
                Assert.IsTrue(manager.TryCopyLatestTelemetry(out Atlas6LiabilityTelemetryRecord latest));
                Assert.AreEqual((ushort)Atlas6LiabilityEventCode.TetherSeveredSatoRen, latest.EventCode);
                Assert.AreEqual(Atlas6LiabilityTelemetry.ManagerContextHash, latest.ContextHash);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DirectiveWeighting_RejectsInvalidTickInputs()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            DirectiveWeightingSystem directive = new DirectiveWeightingSystem(telemetry);
            directive.Initialize(0.5f);

            directive.Tick(-1f, 2000f);
            directive.Tick(float.NaN, 2000f);
            directive.Tick(1f, float.PositiveInfinity);

            Assert.AreEqual(0.5f, directive.PressureSealIntegrity);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidDirectiveWeightingInput, latest.EventCode);
        }

        [Test]
        public void ThermalSheer_MasksCriticalReadoutNearDrillSite()
        {
            ThermalSheerManager thermalSheer = new ThermalSheerManager();

            ThermalSheerManager.TelemetryReadout readout = thermalSheer.CalculateTelemetry(1f, 25f);

            Assert.IsTrue(readout.IsMasked);
            Assert.AreEqual(1f, readout.TrueSheer);
            Assert.Less(readout.ReportedSheer, readout.TrueSheer);
            Assert.Greater(readout.MaskDelta01, 0f);
            Assert.AreEqual(ThermalSheerManager.AlertClassDowngraded, readout.AlertClass);
            Assert.AreNotEqual(0u, readout.Flags & ThermalSheerManager.TelemetryFlagCriticalDowngraded);
        }

        [Test]
        public void ThermalSheer_CoercesInvalidReadoutInputs()
        {
            ThermalSheerManager thermalSheer = new ThermalSheerManager();

            ThermalSheerManager.TelemetryReadout readout = thermalSheer.CalculateTelemetry(float.NaN, float.NegativeInfinity);

            Assert.AreEqual(0f, readout.TrueSheer);
            Assert.AreEqual(0f, readout.ReportedSheer);
            Assert.AreEqual(float.MaxValue, readout.DistanceToDrillSiteMeters);
            Assert.AreEqual(0u, readout.Flags);
            Assert.AreEqual(ThermalSheerManager.AlertClassNominal, readout.AlertClass);
            Assert.AreEqual(0f, thermalSheer.GetTrueSensoryFeedback(float.PositiveInfinity));
        }

        [Test]
        public void SubmarineOsSnapshot_CarriesAtlasThermalTelemetry()
        {
            HectonSubmarineOsSnapshot snapshot = new HectonSubmarineOsSnapshot(
                SubsystemStatus.Engines,
                SubmarineEmergencyLevel.Caution,
                1f,
                0.9f,
                0.1f,
                100f,
                4f,
                0.32f,
                0.66f,
                0.34f,
                ThermalSheerManager.TelemetryFlagMasked,
                2,
                140,
                SubmarineVwsFlags.ThermalStress,
                false,
                false,
                true,
                true);

            Assert.AreEqual(0.32f, snapshot.EngineHeat01);
            Assert.AreEqual(0.66f, snapshot.EngineHeatTrue01);
            Assert.AreEqual(0.34f, snapshot.EngineHeatMaskDelta01);
            Assert.AreEqual(ThermalSheerManager.TelemetryFlagMasked, snapshot.AtlasTelemetryFlags);
            Assert.IsTrue(snapshot.IsEngineTelemetryMasked);
            Assert.AreEqual(64, Marshal.SizeOf<HectonSubmarineOsSnapshot>());
        }

        [Test]
        public void SubmarineOsEventPayload_CarriesAtlasThermalTelemetry()
        {
            SubmarineOsEventPayload payload = new SubmarineOsEventPayload
            {
                EventType = (ushort)SubmarineOsEventType.SnapshotUpdated,
                StatusBits = (uint)SubsystemStatus.Engines,
                EmergencyLevel = (ushort)SubmarineEmergencyLevel.Caution,
                PowerNormalized = 1f,
                OxygenNormalized = 0.9f,
                CarbonDioxideNormalized = 0.1f,
                MaxPressureKPa = 100f,
                SpeedKnots = 4f,
                EngineHeat01 = 0.32f,
                EngineHeatTrue01 = 0.66f,
                EngineHeatMaskDelta01 = 0.34f,
                AtlasTelemetryFlags = ThermalSheerManager.TelemetryFlagMasked,
                SonarContactCount = 2,
                NearestSonarContactMeters = 140,
                VocalWarningFlags = (ushort)SubmarineVwsFlags.ThermalStress
            };

            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot));
            Assert.AreEqual(0.32f, snapshot.EngineHeat01);
            Assert.AreEqual(0.66f, snapshot.EngineHeatTrue01);
            Assert.AreEqual(0.34f, snapshot.EngineHeatMaskDelta01);
            Assert.AreEqual(ThermalSheerManager.TelemetryFlagMasked, snapshot.AtlasTelemetryFlags);
            Assert.IsTrue(snapshot.IsEngineTelemetryMasked);
            Assert.AreEqual(64, Marshal.SizeOf<SubmarineOsEventPayload>());
        }

        [Test]
        public void SubmarineOsLogRequest_CarriesAtlasThermalMaskTransitions()
        {
            SubmarineOsEventPayload maskedPayload = new SubmarineOsEventPayload
            {
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryMasked,
                Priority = 2
            };
            SubmarineOsEventPayload restoredPayload = new SubmarineOsEventPayload
            {
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryRestored,
                Priority = 1
            };

            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildLogRequest(in maskedPayload, out HectonSubmarineOsLogRequest maskedRequest));
            Assert.AreEqual(HectonSubmarineOsLogCode.EngineTelemetryMasked, maskedRequest.Code);
            Assert.AreEqual(2, maskedRequest.Priority);
            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildLogRequest(in restoredPayload, out HectonSubmarineOsLogRequest restoredRequest));
            Assert.AreEqual(HectonSubmarineOsLogCode.EngineTelemetryRestored, restoredRequest.Code);
            Assert.AreEqual(1, restoredRequest.Priority);
        }

        [Test]
        public void PdaExchange_BindsRestoredSatoRenSeveranceAsTransmissionLockout()
        {
            GameObject managerHost = new GameObject("PdaExchange_RestoredSatoRen_Manager");
            GameObject exchangeHost = new GameObject("PdaExchange_RestoredSatoRen_Exchange");
            Atlas6CorporateLiabilityManager manager = managerHost.AddComponent<Atlas6CorporateLiabilityManager>();
            PDAExchangeSystem exchange = exchangeHost.AddComponent<PDAExchangeSystem>();
            try
            {
                SaveData data = SaveData.CreateNew(0d);
                data.atlas6LiabilityExtractionCarrierState = (int)ExtractionCarrierState.TetherSevered;
                manager.LoadFromSaveData(data);

                SetPrivateInstanceField(exchange, "liabilityManager", manager);
                InvokePrivateInstanceMethod(exchange, "TryRegisterLiabilityEvents");

                Assert.IsFalse(exchange.CanTransmit);
            }
            finally
            {
                Object.DestroyImmediate(exchangeHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void PdaExchange_LoadFromSaveDataDerivesRestoredSatoRenTransmissionLockout()
        {
            GameObject managerHost = new GameObject("PdaExchange_LoadRestoredSatoRen_Manager");
            GameObject exchangeHost = new GameObject("PdaExchange_LoadRestoredSatoRen_Exchange");
            Atlas6CorporateLiabilityManager manager = managerHost.AddComponent<Atlas6CorporateLiabilityManager>();
            PDAExchangeSystem exchange = exchangeHost.AddComponent<PDAExchangeSystem>();
            try
            {
                SaveData atlas6Data = SaveData.CreateNew(0d);
                atlas6Data.atlas6LiabilityExtractionCarrierState = (int)ExtractionCarrierState.TetherSevered;
                manager.LoadFromSaveData(atlas6Data);

                SetPrivateInstanceField(exchange, "liabilityManager", manager);
                exchange.LoadFromSaveData(SaveData.CreateNew(0d));

                Assert.IsFalse(exchange.CanTransmit);
            }
            finally
            {
                Object.DestroyImmediate(exchangeHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void PdaExchange_LoadFromSaveDataResolvesActiveRestoredSatoRenTransmissionLockout()
        {
            GameObject managerHost = new GameObject("PdaExchange_LoadActiveSatoRen_Manager");
            GameObject exchangeHost = new GameObject("PdaExchange_LoadActiveSatoRen_Exchange");
            Atlas6CorporateLiabilityManager manager = managerHost.AddComponent<Atlas6CorporateLiabilityManager>();
            PDAExchangeSystem exchange = exchangeHost.AddComponent<PDAExchangeSystem>();
            try
            {
                SaveData atlas6Data = SaveData.CreateNew(0d);
                atlas6Data.atlas6LiabilityExtractionCarrierState = (int)ExtractionCarrierState.TetherSevered;
                manager.LoadFromSaveData(atlas6Data);
                SetPrivateStaticProperty(
                    typeof(Atlas6CorporateLiabilityManager),
                    nameof(Atlas6CorporateLiabilityManager.ActiveRuntimeInstance),
                    manager);

                exchange.LoadFromSaveData(SaveData.CreateNew(0d));

                Assert.IsFalse(exchange.CanTransmit);
            }
            finally
            {
                SetPrivateStaticProperty(
                    typeof(Atlas6CorporateLiabilityManager),
                    nameof(Atlas6CorporateLiabilityManager.ActiveRuntimeInstance),
                    null);
                Object.DestroyImmediate(exchangeHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void PdaExchange_LoadFromSaveDataClearsStaleSatoRenTransmissionLockout()
        {
            GameObject managerHost = new GameObject("PdaExchange_ClearStaleSatoRen_Manager");
            GameObject exchangeHost = new GameObject("PdaExchange_ClearStaleSatoRen_Exchange");
            Atlas6CorporateLiabilityManager manager = managerHost.AddComponent<Atlas6CorporateLiabilityManager>();
            PDAExchangeSystem exchange = exchangeHost.AddComponent<PDAExchangeSystem>();
            try
            {
                SaveData severedData = SaveData.CreateNew(0d);
                severedData.atlas6LiabilityExtractionCarrierState = (int)ExtractionCarrierState.TetherSevered;
                manager.LoadFromSaveData(severedData);
                SetPrivateInstanceField(exchange, "liabilityManager", manager);
                exchange.LoadFromSaveData(SaveData.CreateNew(0d));
                Assert.IsFalse(exchange.CanTransmit);

                SaveData restoredData = SaveData.CreateNew(0d);
                restoredData.atlas6LiabilityExtractionCarrierState = (int)ExtractionCarrierState.Offline;
                manager.LoadFromSaveData(restoredData);
                exchange.LoadFromSaveData(SaveData.CreateNew(0d));

                Assert.IsTrue(exchange.CanTransmit);
            }
            finally
            {
                Object.DestroyImmediate(exchangeHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void PdaExchange_ExecutionCountSaturatesAtIntMax()
        {
            GameObject host = new GameObject("PdaExchange_ExecutionCountSaturates");
            host.SetActive(false);
            PDAExchangeSystem exchange = host.AddComponent<PDAExchangeSystem>();
            try
            {
                int[] offerHashes = GetPrivateInstanceField<int[]>(exchange, "_executionOfferHashes");
                int[] executionCounts = GetPrivateInstanceField<int[]>(exchange, "_executionCounts");
                offerHashes[0] = 123;
                executionCounts[0] = int.MaxValue;
                SetPrivateInstanceField(exchange, "_executionStateCount", 1);

                InvokePrivateInstanceMethod(exchange, "IncrementExecutionCount", 123);

                Assert.AreEqual(int.MaxValue, executionCounts[0]);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EndingSystem_RejectsInvalidEndingChoiceAtRuntime()
        {
            GameObject host = new GameObject("EndingSystem_RejectsInvalidChoice");
            host.SetActive(false);
            EndingSystem ending = host.AddComponent<EndingSystem>();
            try
            {
                SetPrivateInstanceField(ending, "_conditionMet", true);

                ending.ChooseEnding((EndingChoice)99);

                Assert.AreEqual(EndingChoice.None, ending.ChosenEnding);
                Assert.IsFalse(ending.IsEndingComplete);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EndingSystem_SaveLoadSanitizesInvalidChoiceCompletion()
        {
            GameObject host = new GameObject("EndingSystem_SaveLoadSanitizesChoice");
            host.SetActive(false);
            EndingSystem ending = host.AddComponent<EndingSystem>();
            try
            {
                SaveData corruptData = SaveData.CreateNew(0d);
                corruptData.endingChoice = 99;
                corruptData.endingComplete = true;
                corruptData.endingConditionMet = false;

                ending.LoadFromSaveData(corruptData);

                Assert.AreEqual(EndingChoice.None, ending.ChosenEnding);
                Assert.IsFalse(ending.IsEndingComplete);
                Assert.IsFalse(ending.IsConditionMet);

                SaveData completedData = SaveData.CreateNew(0d);
                completedData.endingChoice = (int)EndingChoice.Leave;
                completedData.endingComplete = true;
                completedData.endingConditionMet = false;

                ending.LoadFromSaveData(completedData);

                Assert.AreEqual(EndingChoice.Leave, ending.ChosenEnding);
                Assert.IsTrue(ending.IsEndingComplete);
                Assert.IsTrue(ending.IsConditionMet);

                SaveData incompleteChoiceData = SaveData.CreateNew(0d);
                incompleteChoiceData.endingChoice = (int)EndingChoice.Amplify;
                incompleteChoiceData.endingComplete = false;
                incompleteChoiceData.endingConditionMet = true;

                ending.LoadFromSaveData(incompleteChoiceData);

                Assert.AreEqual(EndingChoice.None, ending.ChosenEnding);
                Assert.IsFalse(ending.IsEndingComplete);
                Assert.IsTrue(ending.IsConditionMet);

                SetPrivateInstanceField(ending, "_chosenEnding", (EndingChoice)99);
                SetPrivateInstanceField(ending, "_endingComplete", true);
                SetPrivateInstanceField(ending, "_conditionMet", false);
                SaveData output = SaveData.CreateNew(0d);
                ending.PopulateSaveData(output);

                Assert.AreEqual((int)EndingChoice.None, output.endingChoice);
                Assert.IsFalse(output.endingComplete);
                Assert.IsFalse(output.endingConditionMet);

                SetPrivateInstanceField(ending, "_chosenEnding", EndingChoice.Leave);
                SetPrivateInstanceField(ending, "_endingComplete", false);
                SetPrivateInstanceField(ending, "_conditionMet", true);
                ending.PopulateSaveData(output);

                Assert.AreEqual((int)EndingChoice.None, output.endingChoice);
                Assert.IsFalse(output.endingComplete);
                Assert.IsTrue(output.endingConditionMet);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EndingEvents_GuardsChoiceAndPayloadEnums()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/EndingSystem.cs"));
            string chosen = ExtractMethodBody(source, "public static bool TryRaiseChosen(EndingChoice choice)");
            string sequence = ExtractMethodBody(source, "public static bool TryRaiseSequenceComplete(EndingChoice choice)");
            string enqueue = ExtractMethodBody(source, "private static bool Enqueue(EndingEventType type, EndingChoice choice)");
            string buildPayload = ExtractMethodBody(source, "private static bool TryBuildPayload(");
            string chooseEnding = ExtractMethodBody(source, "public void ChooseEnding(EndingChoice choice)");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(SaveData data)");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(SaveData data)");
            string sanitizeChoice = ExtractMethodBody(source, "private static EndingChoice SanitizeEndingChoice(int endingChoice)");

            StringAssert.Contains("!IsActionChoice(choice)", chosen);
            StringAssert.Contains("!IsActionChoice(choice)", sequence);
            StringAssert.Contains("TryBuildPayload(type, choice, out EndingEventPayload payload)", enqueue);
            StringAssert.Contains("!IsKnownEventType(type)", buildPayload);
            StringAssert.Contains("type == EndingEventType.ConditionMet", buildPayload);
            StringAssert.Contains("!IsActionChoice(choice)", buildPayload);
            StringAssert.Contains("!IsActionEndingChoice(choice)", chooseEnding);
            StringAssert.Contains("EndingChoice safeChoice = SanitizeEndingChoice((int)_chosenEnding);", populate);
            StringAssert.Contains("safeComplete = _endingComplete && safeChoice != EndingChoice.None", populate);
            StringAssert.Contains("if (!safeComplete)", populate);
            StringAssert.Contains("_chosenEnding = SanitizeEndingChoice(data.endingChoice)", load);
            StringAssert.Contains("_endingComplete = data.endingComplete && _chosenEnding != EndingChoice.None", load);
            StringAssert.Contains("_conditionMet = data.endingConditionMet || _endingComplete", load);
            StringAssert.Contains("if (!_endingComplete)", load);
            StringAssert.Contains("endingChoice <= (int)EndingChoice.Amplify", sanitizeChoice);
        }

        [Test]
        public void EndingTerminalInteractable_GuardsDirectEventAndChoiceIngress()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/EndingTerminalInteractable.cs"));
            string onEndingEvent = ExtractMethodBody(source, "public void OnEndingEvent(in EndingEventPayload payload)");
            string submit = ExtractMethodBody(source, "private void SubmitTerminalChoice(EndingChoice choice)");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeEndingEvent(");
            string actionChoice = ExtractMethodBody(source, "private static bool IsActionEndingChoice(EndingChoice choice)");

            StringAssert.Contains("TrySanitizeEndingEvent(in payload, out EndingEventType eventType, out EndingChoice choice)", onEndingEvent);
            StringAssert.Contains("switch (eventType)", onEndingEvent);
            StringAssert.Contains("HandleEndingChosen(choice)", onEndingEvent);
            StringAssert.Contains("!IsActionEndingChoice(choice)", submit);
            StringAssert.Contains("eventType = (EndingEventType)payload.EventType", sanitize);
            StringAssert.Contains("choice = EndingChoice.None", sanitize);
            StringAssert.Contains("return IsActionEndingChoice(choice)", sanitize);
            StringAssert.Contains("return false", sanitize);
            StringAssert.Contains("choice >= EndingChoice.ShutDown", actionChoice);
            StringAssert.Contains("choice <= EndingChoice.Amplify", actionChoice);
        }

        [Test]
        public void QuestGraphEvaluator_SanitizesSignalIngressPayloads()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestGraphEvaluator.cs"));
            string updateDepthContext = ExtractMethodBody(source, "public void UpdateDepthContext(float depthMeters, uint zoneHash, bool isThermalZone)");
            string depthTier = ExtractMethodBody(source, "public void OnDepthTierChanged(int depthTier, float depthMeters)");
            string atlasSignal = ExtractMethodBody(source, "public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)");
            string enqueue = ExtractMethodBody(source, "private void EnqueueSignal(in QuestSignalPayload payload)");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeSignalPayload(");
            string signalKind = ExtractMethodBody(source, "private static bool IsKnownSignalKind(ushort eventType)");

            StringAssert.Contains("!math.isfinite(depthMeters) || depthMeters < 0f", updateDepthContext);
            StringAssert.Contains("math.isfinite(depthMeters) && depthMeters > 0f", depthTier);
            StringAssert.Contains("resolvedDepth <= 0f", depthTier);
            StringAssert.Contains("payload.MessageHash == 0u", atlasSignal);
            StringAssert.Contains("TrySanitizeSignalPayload(in payload, out QuestSignalPayload safePayload)", enqueue);
            StringAssert.Contains("ReportPendingSignalOverflow(safePayload.EventType)", enqueue);
            StringAssert.Contains("_pendingSignals.Enqueue(safePayload)", enqueue);
            StringAssert.Contains("!math.isfinite(payload.Timestamp) || payload.Timestamp < 0d", sanitize);
            StringAssert.Contains("!math.all(math.isfinite(payload.Position))", sanitize);
            StringAssert.Contains("!IsKnownSignalKind(payload.EventType)", sanitize);
            StringAssert.Contains("payload.Flags & (uint)(QuestSignalContextFlags.ThermalPhase | QuestSignalContextFlags.AbyssalPhase)", sanitize);
            StringAssert.Contains("payload.EntityHash == 0u || !math.isfinite(payload.NumericValue) || payload.NumericValue <= 0f", sanitize);
            StringAssert.Contains("payload.EntityHash == 0u", sanitize);
            StringAssert.Contains("!math.isfinite(payload.NumericValue) || payload.NumericValue < 0f", sanitize);
            StringAssert.Contains("math.floor(payload.NumericValue)", sanitize);
            StringAssert.Contains("QuestSignalKind.CraftCompleted", signalKind);
        }

        [Test]
        public void QuestStateManager_MasksPackedRestoreToRegisteredFlags()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestStateManager.cs"));
            string initialize = ExtractMethodBody(source, "public bool Initialize(QuestData[] allQuests, ILocalizationTextReadModel localizationManager)");
            string snapshot = ExtractMethodBody(source, "public unsafe bool TryCopyPackedStateSnapshot(");
            string restore = ExtractMethodBody(source, "public void RestorePackedState(in QuestSaveHeader header, uint[] packedWords)");
            string register = ExtractMethodBody(source, "private QuestBitAddress RegisterStateBit(");
            string registerMask = ExtractMethodBody(source, "private void RegisterValidPackedWordMask(QuestBitAddress address)");
            string applyMask = ExtractMethodBody(source, "private void ApplyValidPackedWordMasks()");

            StringAssert.Contains("private NativeArray<uint> _validPackedWordMasks", source);
            StringAssert.Contains("H8Memory.Release(ref _validPackedWordMasks, NativeArrayOwnerSystem)", source);
            StringAssert.Contains("_validPackedWordMasks = H8Memory.Allocate<uint>", initialize);
            StringAssert.Contains("long questArrayLengthLong = (long)_authoredQuestCount + ProceduralQuestCapacity;", initialize);
            StringAssert.Contains("TryResolveQuestStateManagedCapacity(questArrayLength, 6, 16, out int bitAddressCapacity)", initialize);
            StringAssert.Contains("TryResolveQuestStateManagedCapacity(questArrayLength, 2, 8, out int nodeBuilderCapacity)", initialize);
            StringAssert.Contains("TryResolveQuestStateManagedCapacity(questArrayLength, 3, 8, out int prerequisiteBuilderCapacity)", initialize);
            StringAssert.Contains("TryResolveQuestStateManagedCapacity(questArrayLength, 6, 16, out int hashLabelCapacity)", initialize);
            StringAssert.Contains("private static bool TryResolveQuestStateManagedCapacity(", source);
            StringAssert.Contains("long capacityLong = (long)baseCount * multiplier;", source);
            StringAssert.DoesNotContain("Math.Max(questArrayLength * 6, 16)", initialize);
            StringAssert.DoesNotContain("Math.Max(questArrayLength * 3, 8)", initialize);
            StringAssert.DoesNotContain("Math.Max(questArrayLength * 2, 8)", initialize);
            StringAssert.Contains("long nodeCapacityLong = (long)nodeBuilder.Count + ProceduralQuestCapacity;", initialize);
            StringAssert.Contains("long runtimeResultCapacityLong = (long)nodeCapacity + revertBuilder.Count;", initialize);
            StringAssert.Contains("int runtimeResultCapacity = (int)runtimeResultCapacityLong;", initialize);
            StringAssert.DoesNotContain("if (_runtimeResults.Capacity < nodeCapacity + revertBuilder.Count)", initialize);
            StringAssert.DoesNotContain("_runtimeResults.Capacity = nodeCapacity + revertBuilder.Count;", initialize);
            int uncreatedSnapshotIndex = snapshot.IndexOf("if (!_globalPrerequisites.IsCreated)", StringComparison.Ordinal);
            int uncreatedReturnFalseIndex = snapshot.IndexOf("return false;", uncreatedSnapshotIndex, StringComparison.Ordinal);
            Assert.GreaterOrEqual(uncreatedSnapshotIndex, 0, snapshot);
            Assert.GreaterOrEqual(uncreatedReturnFalseIndex, 0, snapshot);
            Assert.Less(uncreatedSnapshotIndex, uncreatedReturnFalseIndex, snapshot);
            StringAssert.Contains("UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));", snapshot);
            StringAssert.Contains("return false;", snapshot.Substring(snapshot.IndexOf("UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(QuestStateManager));", StringComparison.Ordinal)));
            StringAssert.Contains("ApplyValidPackedWordMasks();", restore);
            StringAssert.Contains("restoredChecksum = ComputePackedStateChecksum(_globalPrerequisites)", restore);
            StringAssert.Contains("bool copiedPackedState = false;", restore);
            StringAssert.Contains("copiedPackedState = false;", restore);
            StringAssert.Contains("copiedPackedState = true;", restore);
            StringAssert.Contains("UnsafeUtility.MemClear(destinationPtr, destinationBytes);", restore);
            StringAssert.Contains("bool trustedHeader", restore);
            StringAssert.Contains("header.Version != 0u", restore);
            StringAssert.Contains("copiedPackedState", restore);
            StringAssert.Contains("header.Checksum == restoredChecksum", restore);
            StringAssert.Contains("_stateVersion = trustedHeader", restore);
            StringAssert.Contains("_stateChecksum = trustedHeader", restore);
            StringAssert.Contains("if (!trustedHeader)", restore);
            StringAssert.Contains("RefreshStateMetadata(resetVersion: false)", restore);
            StringAssert.Contains("RegisterValidPackedWordMask(address)", register);
            StringAssert.Contains("_validPackedWordMasks[address.WordIndex] |= address.BitMask", registerMask);
            StringAssert.Contains("_globalPrerequisites[i] &= _validPackedWordMasks[i]", applyMask);
        }

        [Test]
        public void QuestManager_ClampsStagedPackedStateBuffer()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestManager.cs"));
            string snapshot = ExtractMethodBody(source, "internal unsafe bool TryCopyPackedStateSnapshot(");
            string stage = ExtractMethodBody(source, "internal static void StageLoadedPackedState(in QuestSaveHeader header, uint[] packedWords)");

            StringAssert.Contains("header = default", snapshot);
            StringAssert.Contains("QuestSaveHeader candidateHeader = _stateManager.BuildSaveHeader(timestamp)", snapshot);
            StringAssert.Contains("if (!_stateManager.TryCopyPackedStateSnapshot(destinationPtr, destinationWordCapacity))", snapshot);
            StringAssert.Contains("header = candidateHeader", snapshot);
            StringAssert.Contains("s_stagedLoadedQuestHeader = default", stage);
            StringAssert.Contains("s_stagedLoadedQuestHeader = header", stage);
            StringAssert.Contains("wordCount = Math.Min(packedWords.Length, QuestRuntimeLayout.WordCapacity)", stage);
            StringAssert.Contains("s_stagedLoadedPackedState.Length != wordCount", stage);
            StringAssert.Contains("s_stagedLoadedPackedState = new uint[wordCount]", stage);
            StringAssert.Contains("Array.Copy(packedWords, s_stagedLoadedPackedState, wordCount)", stage);
            StringAssert.DoesNotContain("new uint[packedWords.Length]", stage);
            StringAssert.DoesNotContain("Array.Copy(packedWords, s_stagedLoadedPackedState, packedWords.Length)", stage);

            int nullBodyIndex = stage.IndexOf("if (packedWords == null || packedWords.Length <= 0)", StringComparison.Ordinal);
            int clearHeaderIndex = stage.IndexOf("s_stagedLoadedQuestHeader = default", nullBodyIndex, StringComparison.Ordinal);
            int assignHeaderIndex = stage.IndexOf("s_stagedLoadedQuestHeader = header", StringComparison.Ordinal);
            Assert.GreaterOrEqual(nullBodyIndex, 0, stage);
            Assert.GreaterOrEqual(clearHeaderIndex, 0, stage);
            Assert.GreaterOrEqual(assignHeaderIndex, 0, stage);
            Assert.Less(clearHeaderIndex, assignHeaderIndex, stage);
        }

        [Test]
        public void QuestStateManager_SaveHeaderSanitizesTimestamp()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestStateManager.cs"));
            string buildHeader = ExtractMethodBody(source, "public QuestSaveHeader BuildSaveHeader(double timestamp)");
            string sanitize = ExtractMethodBody(source, "private static double SanitizeNonNegativeFiniteTimestamp(double timestamp)");

            StringAssert.Contains("header.Timestamp = SanitizeNonNegativeFiniteTimestamp(timestamp);", buildHeader);
            StringAssert.Contains("math.isfinite(timestamp) && timestamp >= 0d", sanitize);
            StringAssert.Contains("? timestamp", sanitize);
            StringAssert.Contains(": 0d", sanitize);
            StringAssert.DoesNotContain("header.Timestamp = timestamp;", buildHeader);
        }

        [Test]
        public void QuestStateManager_AuditAndRevertSanitizeTimestamps()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestStateManager.cs"));
            string revert = ExtractMethodBody(source, "public bool TryRevertCriticalItem(");
            string audit = ExtractMethodBody(source, "private void AppendTransitionAudit(");
            string resolveAudit = ExtractMethodBody(source, "private static double ResolveAuditTimestamp(double timestamp)");

            StringAssert.Contains("double safeTimestamp = ResolveAuditTimestamp(timestamp);", revert);
            StringAssert.Contains("_bitAddressByHash == null", revert);
            StringAssert.Contains("_questHashesByQuestIndex == null", revert);
            StringAssert.Contains("descriptor.QuestIndex < 0", revert);
            StringAssert.Contains("descriptor.QuestIndex >= _questHashesByQuestIndex.Length", revert);
            StringAssert.Contains("Timestamp = safeTimestamp", revert);
            StringAssert.Contains("_runtimeResults.Clear();", revert);
            StringAssert.Contains("double auditTimestamp = ResolveAuditTimestamp(signal.Timestamp);", audit);
            StringAssert.Contains("Timestamp = auditTimestamp", audit);
            StringAssert.Contains("math.isfinite(timestamp) && timestamp > 0d", resolveAudit);
            StringAssert.Contains("SanitizeNonNegativeFiniteTimestamp(Time.timeAsDouble)", resolveAudit);
            StringAssert.DoesNotContain("Timestamp = timestamp", revert);
            StringAssert.DoesNotContain("Timestamp = signal.Timestamp > 0d ? signal.Timestamp : Time.timeAsDouble", audit);

            int mutationIndex = revert.IndexOf("ApplyQuestRevertMutation(", StringComparison.Ordinal);
            int clearIndex = revert.IndexOf("_runtimeResults.Clear();", StringComparison.Ordinal);
            int addIndex = revert.IndexOf("_runtimeResults.Add", StringComparison.Ordinal);
            Assert.GreaterOrEqual(mutationIndex, 0, revert);
            Assert.GreaterOrEqual(clearIndex, 0, revert);
            Assert.GreaterOrEqual(addIndex, 0, revert);
            Assert.Less(mutationIndex, clearIndex, revert);
            Assert.Less(clearIndex, addIndex, revert);
        }

        [Test]
        public void SaveLoad_DialogueChoiceFlagsIgnoreRawPackedWordsForCurrentSaves()
        {
            string storageSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string managerSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveManager.cs"));
            string terminalSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs"));
            string extract = ExtractMethodBody(storageSource, "internal static ushort ExtractPlayerDialogueChoiceFlags(uint[] packedQuestStateWords)");
            string decode = ExtractMethodBody(storageSource, "internal static ushort DecodePlayerDialogueChoiceFlags(");
            string sanitize = ExtractMethodBody(storageSource, "internal static ushort SanitizePlayerDialogueChoiceFlags(");
            string resolver = ExtractMethodBody(storageSource, "internal static ushort ResolveLoadedPlayerDialogueChoiceFlags(");
            string encode = ExtractMethodBody(storageSource, "private static bool TryEncodeHeaderDeltaCount(");
            string layoutValidation = ExtractMethodBody(storageSource, "private static bool TryValidateCurrentBinaryLayouts(");

            int headerReturnIndex = resolver.IndexOf("return decodedHeaderFlags;", StringComparison.Ordinal);
            int packedFallbackIndex = resolver.IndexOf("ExtractPlayerDialogueChoiceFlags(packedQuestStateWords)", StringComparison.Ordinal);

            StringAssert.Contains("internal const ushort PlayerDialogueChoiceSaveFacilityMask = 1 << 0;", storageSource);
            StringAssert.Contains("internal const ushort PlayerDialogueChoiceKnownFlagsMask = PlayerDialogueChoiceSaveFacilityMask;", storageSource);
            StringAssert.Contains("DialogueDecisionSaveFacilityMask = SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask", terminalSource);
            StringAssert.Contains("SanitizePlayerDialogueChoiceFlags(", extract);
            StringAssert.Contains("SanitizePlayerDialogueChoiceFlags(", decode);
            StringAssert.Contains("flags & PlayerDialogueChoiceKnownFlagsMask", sanitize);
            StringAssert.Contains("decodedHeaderFlags = SanitizePlayerDialogueChoiceFlags(decodedHeaderFlags);", resolver);
            StringAssert.Contains("saveFileVersion >= AlignedSectionHeaderVersion", resolver);
            Assert.GreaterOrEqual(headerReturnIndex, 0, resolver);
            Assert.GreaterOrEqual(packedFallbackIndex, 0, resolver);
            Assert.Less(headerReturnIndex, packedFallbackIndex, resolver);
            StringAssert.Contains("playerDialogueChoiceFlags = SanitizePlayerDialogueChoiceFlags(playerDialogueChoiceFlags);", encode);
            Assert.GreaterOrEqual(
                CountOccurrences(storageSource, "playerDialogueChoiceFlags = ResolveLoadedPlayerDialogueChoiceFlags("),
                2);
            StringAssert.Contains("SaveBinaryStorage.SanitizePlayerDialogueChoiceFlags((ushort)(Volatile.Read", managerSource);
            StringAssert.Contains("decisionMask = SaveBinaryStorage.SanitizePlayerDialogueChoiceFlags(decisionMask);", managerSource);
            StringAssert.Contains("playerDialogueChoiceSaveFacilityMask != 1u << 0", layoutValidation);
            StringAssert.Contains("playerDialogueChoiceKnownFlagsMask != playerDialogueChoiceSaveFacilityMask", layoutValidation);
            StringAssert.Contains("playerDialogueChoiceKnownFlagsMask & ~headerPackedQuestWordCountMask", layoutValidation);
            StringAssert.Contains("loadedPlayerDialogueChoiceFlags);", managerSource);
            StringAssert.DoesNotContain(
                "loadedPlayerDialogueChoiceFlags | SaveBinaryStorage.ExtractPlayerDialogueChoiceFlags(loadedQuestStateWords)",
                managerSource);
            StringAssert.DoesNotContain(
                "playerDialogueChoiceFlags | SaveBinaryStorage.ExtractPlayerDialogueChoiceFlags(packedQuestStateWords)",
                managerSource);
        }

        [Test]
        public void SaveBinaryStorage_ValidatesPackedQuestWordCountBeforeReadAllocation()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string indexedLoad = ExtractMethodBody(source, "private static bool TryLoadSaveDataIndexedV8(");
            string indexedMeasure = ExtractMethodBody(source, "private static bool TryMeasureIndexedV8VoxelDeltaSnapshotByteLength(");
            string packedRead = ExtractMethodBody(source, "private static bool TryReadPackedQuestStateWords(");

            StringAssert.Contains("TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error)", indexedLoad);
            StringAssert.Contains("TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error)", indexedMeasure);
            StringAssert.Contains("TryDecodePackedQuestWordCount(in header, out int packedQuestWordCount, out error)", packedRead);
            StringAssert.Contains("packedQuestStateWords = new uint[packedQuestWordCount]", indexedLoad);
            StringAssert.Contains("packedQuestStateWords = new uint[packedQuestWordCount]", packedRead);
            StringAssert.DoesNotContain("int packedQuestWordCount = DecodePackedQuestWordCount(in header);", indexedLoad);
            StringAssert.DoesNotContain("int packedQuestWordCount = DecodePackedQuestWordCount(in header);", indexedMeasure);
            StringAssert.DoesNotContain("int packedQuestWordCount = DecodePackedQuestWordCount(in header);", packedRead);
        }

        [Test]
        public void SaveBinaryStorage_ThermalGridRleByteCountUsesTryContract()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string byteCount = ExtractMethodBody(source, "private static bool TryComputeThermalGridRleByteCount(");

            Assert.GreaterOrEqual(CountOccurrences(source, "return TryComputeThermalGridRleByteCount(safeCount, out byteCount);"), 2);
            StringAssert.Contains("long byteCountLong = (long)runCount * UnsafeUtility.SizeOf<ThermalGridRleRun>();", byteCount);
            StringAssert.Contains("byteCountLong > int.MaxValue", byteCount);
            StringAssert.DoesNotContain("byteCount = checked(safeCount * UnsafeUtility.SizeOf<ThermalGridRleRun>());", source);
        }

        [Test]
        public void SaveBinaryStorage_PersistentWorldSectionWriterPropagatesCopyFailures()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));

            Assert.GreaterOrEqual(CountOccurrences(source, "if (!WritePersistentWorldSection("), 2);
            StringAssert.DoesNotContain("private static void WritePersistentWorldSection(", source);
            StringAssert.Contains("private static bool WritePersistentWorldSection", source);
            Assert.GreaterOrEqual(CountOccurrences(source, "out string error"), 2);
            StringAssert.Contains("Persistent-world section destination is null.", source);
            StringAssert.Contains("Persistent-world chunk table write exceeded section bounds.", source);
            StringAssert.Contains("Persistent-world item hash table write exceeded section bounds.", source);
            StringAssert.Contains("Persistent-world section record range is invalid.", source);
            StringAssert.Contains("TryComputePersistentWorldSectionLength(", source);
            StringAssert.Contains("Persistent-world section exceeds supported bounds.", source);
            StringAssert.Contains("Indexed persistent-world sector section exceeds supported bounds.", source);
            StringAssert.Contains("Sector override persistent-world section exceeds supported bounds.", source);
            StringAssert.Contains("bool wroteRecord = false;", source);
            StringAssert.Contains("Persistent-world section record lookup failed.", source);
            StringAssert.Contains("return false", source);
            StringAssert.Contains("return true", source);
        }

        [Test]
        public void SaveBinaryStorage_MetadataStringCopyPropagatesFailures()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string copy = ExtractMethodBody(source, "private static bool TryCopyUtf16StringToUnmanaged(");

            StringAssert.Contains("long sceneBytesLengthLong = (long)sceneName.Length * sizeof(char);", source);
            StringAssert.Contains("long versionBytesLengthLong = (long)gameVersion.Length * sizeof(char);", source);
            StringAssert.Contains("sceneBytesLengthLong > ushort.MaxValue || versionBytesLengthLong > ushort.MaxValue", source);
            StringAssert.DoesNotContain("int sceneBytesLength = checked(sceneName.Length * sizeof(char));", source);
            StringAssert.DoesNotContain("int versionBytesLength = checked(gameVersion.Length * sizeof(char));", source);
            StringAssert.Contains("if (!TryCopyUtf16StringToUnmanaged(sceneName", source);
            StringAssert.Contains("if (!TryCopyUtf16StringToUnmanaged(gameVersion", source);
            StringAssert.DoesNotContain("private static void CopyUtf16StringToUnmanaged", source);
            StringAssert.Contains("out string error", copy);
            StringAssert.Contains("return true", copy);
            StringAssert.Contains("UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SaveBinaryStorage));", copy);
            StringAssert.Contains("UTF-16 metadata string write exceeded destination bounds.", copy);
            StringAssert.Contains("return false", copy);
        }

        [Test]
        public void SaveBinaryStorage_EcosystemSectionWriterPropagatesFailures()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string writer = ExtractMethodBody(source, "private static bool WriteEcosystemSection(");

            StringAssert.Contains("if (!WriteEcosystemSection(AddByteOffset(rawPtr, metadataCursor), ecosystemSectorStates, out error))", source);
            StringAssert.DoesNotContain("private static void WriteEcosystemSection(", source);
            StringAssert.Contains("TryComputeEcosystemSectionLength(ecosystemSectorCount, CurrentVersion, out int ecosystemSectionLength)", source);
            StringAssert.Contains("out string error", writer);
            StringAssert.Contains("Ecosystem section destination is null.", writer);
            StringAssert.Contains("TryComputeEcosystemSectionLength(recordCount, CurrentVersion, out int sectionLength)", writer);
            StringAssert.Contains("Ecosystem section exceeds supported bounds.", writer);
            StringAssert.Contains("long recordBytes = (long)recordCount * recordSize;", writer);
            StringAssert.Contains("Ecosystem section write exceeded section bounds.", writer);
            StringAssert.Contains("return false", writer);
            StringAssert.Contains("return true", writer);
        }

        [Test]
        public void SaveBinaryStorage_CompressedWriteCapacityUsesBoundedLongArithmetic()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));

            StringAssert.Contains("long compressedCapacityLong", source);
            StringAssert.Contains("long fileCapacityLong", source);
            StringAssert.Contains("Sector override compressed buffer exceeds supported bounds.", source);
            StringAssert.Contains("long rawByteLengthLong = (long)recordCount * UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>();", source);
            StringAssert.Contains("Sector entity-state raw buffer exceeds supported bounds.", source);
            StringAssert.Contains("Sector entity-state compressed buffer exceeds supported bounds.", source);
            StringAssert.DoesNotContain("int compressedCapacity = rawSectionLength +", source);
            StringAssert.DoesNotContain("int compressedCapacity = rawByteLength +", source);
            StringAssert.DoesNotContain("int rawByteLength = checked(recordCount * UnsafeUtility.SizeOf<SectorCompactEntityStateRecord16>());", source);
        }

        [Test]
        public void SaveBinaryStorage_LegacyEntityCountReadsUseBoundedConversion()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string readDeltas = ExtractMethodBody(source, "private static bool TryReadPersistentWorldDeltas(");
            string resolveLength = ExtractMethodBody(source, "private static bool TryResolvePersistentWorldSectionLength(");

            StringAssert.Contains("TryConvertSectionCount(header.EntityCount, out int entityCount)", readDeltas);
            StringAssert.Contains("TryConvertSectionCount(header.EntityCount, out int entityCount)", resolveLength);
            StringAssert.Contains("Entity count exceeds supported bounds.", readDeltas);
            StringAssert.Contains("Entity count exceeds supported bounds.", resolveLength);
            StringAssert.Contains("long entitySectionLengthLong", resolveLength);
            StringAssert.DoesNotContain("int entityCount = checked((int)header.EntityCount);", readDeltas);
            StringAssert.DoesNotContain("int entityCount = checked((int)header.EntityCount);", resolveLength);
            StringAssert.DoesNotContain("checked(entityCount * UnsafeUtility.SizeOf<PersistentWorldDeltaRecordLegacy64>())", resolveLength);
        }

        [Test]
        public void SaveBinaryStorage_TokenizedExpandedLengthUsesBoundedConversion()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string expand = ExtractMethodBody(source, "private static bool TryExpandTokenizedPayloadInPlace(");

            StringAssert.Contains("header.ExpandedPayloadLength > int.MaxValue", expand);
            StringAssert.Contains("Tokenized payload declared an unsupported expanded length.", expand);
            StringAssert.Contains("expandedPayloadLength = (int)header.ExpandedPayloadLength;", expand);
            StringAssert.DoesNotContain("expandedPayloadLength = checked((int)header.ExpandedPayloadLength);", expand);
        }

        [Test]
        public void SaveBinaryStorage_HeaderOffsetsUseBoundedConversion()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string validate = ExtractMethodBody(source, "private static bool TryValidateHeader(");
            string offsets = ExtractMethodBody(source, "private static bool TryResolvePayloadOffsets(");
            string shift = ExtractMethodBody(source, "private static bool TryShiftPayloadOffset(");

            StringAssert.Contains("header.PlayerOffset > int.MaxValue", validate);
            StringAssert.Contains("header.DeltaOffset > int.MaxValue", validate);
            StringAssert.Contains("header.EntityOffset > int.MaxValue", validate);
            StringAssert.Contains("long maxPayloadOffset = (long)header.PlayerOffset + RawPayloadCapacityBytes;", validate);
            StringAssert.Contains("header.DeltaOffset > maxPayloadOffset", validate);
            StringAssert.Contains("header.EntityOffset > maxPayloadOffset", validate);
            StringAssert.Contains("Save payload offset exceeds the supported decoder range.", offsets);
            StringAssert.Contains("payloadBaseOffset = (int)header.PlayerOffset;", offsets);
            StringAssert.Contains("deltaSectionOffset = (int)header.DeltaOffset - payloadBaseOffset;", offsets);
            StringAssert.Contains("entitySectionOffset = (int)header.EntityOffset - payloadBaseOffset;", offsets);
            StringAssert.Contains("long shifted = (long)offset + byteShift;", shift);
            StringAssert.Contains("Migrated save payload offset exceeds the supported decoder range.", shift);
            StringAssert.DoesNotContain("checked((int)header.PlayerOffset)", source);
            StringAssert.DoesNotContain("checked((int)header.DeltaOffset)", source);
            StringAssert.DoesNotContain("checked((int)header.EntityOffset)", source);
            StringAssert.DoesNotContain("checked((uint)((int)header.DeltaOffset + payloadByteShift))", source);
            StringAssert.DoesNotContain("checked((uint)((int)header.EntityOffset + payloadByteShift))", source);
        }

        [Test]
        public void SaveBinaryStorage_IndexedSectorCountUsesBoundedConversion()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string decode = ExtractMethodBody(source, "private static bool TryDecodeIndexedSectorCount(");
            string directoryRead = ExtractMethodBody(source, "private static bool TryReadIndexedDirectory(");
            string mappedScan = ExtractMethodBody(source, "private static bool TryReadIndexedDirectoryHeaderForMappedScan(");
            string commit = ExtractMethodBody(source, "private static bool TryCommitIndexedPersistentWorldSectorOverride(");

            StringAssert.Contains("directoryHeader.SectorCount > IndexedSectorDirectorySlotCount", decode);
            StringAssert.Contains("sectorCount = (int)directoryHeader.SectorCount;", decode);
            StringAssert.Contains("TryDecodeIndexedSectorCount(in directoryHeader, out int sectorCount, out error)", directoryRead);
            StringAssert.Contains("TryDecodeIndexedSectorCount(in directoryHeader, out _, out error)", mappedScan);
            StringAssert.Contains("long directoryBytesLong = (long)saveHeader.PlayerOffset - headerSizeBytes;", commit);
            StringAssert.Contains("Indexed sector directory length exceeds the supported range.", commit);
            StringAssert.Contains("long updatedSectorCount = (long)directoryHeader.SectorCount + sectorCountDelta;", commit);
            StringAssert.Contains("updatedSectorCount < 0L || updatedSectorCount > IndexedSectorDirectorySlotCount", commit);
            StringAssert.DoesNotContain("checked((int)directoryHeader.SectorCount)", source);
            StringAssert.DoesNotContain("(int)directoryHeader.SectorCount)", source);
            StringAssert.DoesNotContain("checked((int)(saveHeader.PlayerOffset - headerSizeBytes))", source);
            StringAssert.DoesNotContain("checked((uint)(directoryHeader.SectorCount + sectorCountDelta))", source);
        }

        [Test]
        public void SaveBinaryStorage_IndexedSectorRecordCountsUseTryContract()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string count = ExtractMethodBody(source, "private static bool TryCountIndexedSectorRecords(");

            StringAssert.Contains("TryCountIndexedSectorRecords(sectorGroups, sectorCount, out int totalEntityCount)", source);
            StringAssert.Contains("Indexed persistent-world entity count exceeds supported bounds.", source);
            StringAssert.Contains("long total = 0L;", count);
            StringAssert.Contains("total > int.MaxValue", count);
            StringAssert.Contains("totalRecords = (int)total;", count);
            StringAssert.DoesNotContain("private static int CountIndexedSectorRecords(", source);
            StringAssert.DoesNotContain("total = checked(total + group.Count);", source);
            StringAssert.DoesNotContain("recordOffset = checked(recordOffset + group.Count);", source);
            StringAssert.DoesNotContain("private static int ComputePersistentWorldSectionLength(", source);
            StringAssert.DoesNotContain("private static int ComputeEcosystemSectionLength(", source);
        }

        [Test]
        public void SaveBinaryStorage_SaveDataByteLengthUsesBoundedConversion()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string decode = ExtractMethodBody(source, "private static bool TryDecodeSaveDataByteLength(");
            string payloadRead = ExtractMethodBody(source, "private static bool TryReadPayload(");

            StringAssert.Contains("prefix.SaveDataByteLength > int.MaxValue", decode);
            StringAssert.Contains("Serialized save data length exceeds the supported decoder range.", decode);
            StringAssert.Contains("saveDataLength = (int)prefix.SaveDataByteLength;", decode);
            Assert.GreaterOrEqual(CountOccurrences(source, "TryDecodeSaveDataByteLength(in prefix, out int saveDataLength, out error)"), 4);
            StringAssert.Contains("long playerPayloadLengthLong = (long)metadataBytes + saveDataLength;", payloadRead);
            StringAssert.Contains("Serialized save data exceeds the supported decoder range.", payloadRead);
            StringAssert.DoesNotContain("checked((int)prefix.SaveDataByteLength)", source);
            StringAssert.DoesNotContain("metadataBytes + checked((int)prefix.SaveDataByteLength)", source);
        }

        [Test]
        public void SaveBinaryStorage_CompressedPayloadLengthUsesBudgetBeforeCast()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveBinaryStorage.cs"));
            string payloadRead = ExtractMethodBody(source, "private static bool TryReadPayload(");

            StringAssert.Contains("long compressedPayloadLengthLong = fileLength - headerSizeBytes;", payloadRead);
            StringAssert.Contains("compressedPayloadLengthLong > MaxCompressedPayloadBytes", payloadRead);
            StringAssert.Contains("compressedPayloadLengthLong > compressedBuffer.Length", payloadRead);
            StringAssert.Contains("int compressedPayloadLength = (int)compressedPayloadLengthLong;", payloadRead);
            StringAssert.DoesNotContain("int compressedPayloadLength = checked((int)fileLength - headerSizeBytes);", payloadRead);
        }

        [Test]
        public void SaveDataMigrationAupV8_ExpandedPayloadLengthUsesBoundedArithmetic()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/SaveDataMigration_AupV8.cs"));
            string migrate = ExtractMethodBody(source, "internal static bool TryMigratePayloadToV8(");

            StringAssert.Contains("long migratedLengthLong = (long)rawLength + PayloadPrefixByteShift;", migrate);
            StringAssert.Contains("migratedLengthLong > int.MaxValue", migrate);
            StringAssert.Contains("AUP v8 migration expanded payload length exceeds the supported range.", migrate);
            StringAssert.Contains("destinationCapacity < migratedLengthLong", migrate);
            StringAssert.Contains("migratedLength = (int)migratedLengthLong;", migrate);
            StringAssert.DoesNotContain("destinationCapacity < rawLength + PayloadPrefixByteShift", migrate);
            StringAssert.DoesNotContain("migratedLength = rawLength + PayloadPrefixByteShift;", migrate);
        }

        [Test]
        public void Atlas6LiabilityConflicts_ResolveKnownDirectiveIds()
        {
            Assert.IsTrue(Atlas6Events.TryResolveDirectiveConflict(
                Atlas6Events.ActuarialLiabilityThreatConflictHash,
                out string actuarialConflictId));
            Assert.AreEqual(Atlas6Events.ActuarialLiabilityThreatConflictId, actuarialConflictId);

            Assert.IsTrue(Atlas6Events.TryResolveDirectiveConflict(
                Atlas6Events.SatoRenSilenceSeveranceConflictHash,
                out string satoRenConflictId));
            Assert.AreEqual(Atlas6Events.SatoRenSilenceSeveranceConflictId, satoRenConflictId);
        }

        [Test]
        public void AtlasRuntimeSaveHotSwap_RoutesThroughGuardedSaveRegistration()
        {
            AssertAtlasRuntimeUsesGuardedSaveHotSwap(
                "_Project/Scripts/Gameplay/Atlas6Liability/Atlas6CorporateLiabilityManager.cs");
            AssertAtlasRuntimeUsesGuardedSaveHotSwap(
                "_Project/Scripts/AtlasSignal/AtlasSignalSystem.cs");
            AssertAtlasRuntimeUsesGuardedSaveHotSwap(
                "_Project/Scripts/AtlasSignal/Atlas6DirectiveSystem.cs");
            AssertAtlasRuntimeUsesGuardedSaveHotSwap(
                "_Project/Scripts/Gameplay/EndingSystem.cs");
        }

        [Test]
        public void TelemetryRing_KeepsLastThreeHundredRecords()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();

            for (int i = 0; i < Atlas6LiabilityTelemetry.Capacity + 5; i++)
            {
                telemetry.Record(
                    Atlas6LiabilityEventCode.WorkerTagRecovered,
                    Atlas6LiabilityEventSeverity.Warning,
                    Atlas6LiabilityTelemetry.ActuarialContextHash,
                    value0: i);
            }

            Assert.AreEqual(Atlas6LiabilityTelemetry.Capacity, telemetry.Count);
            Assert.AreEqual((uint)(Atlas6LiabilityTelemetry.Capacity + 5), telemetry.LatestSequence);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.WorkerTagRecovered, latest.EventCode);
            Assert.AreEqual(Atlas6LiabilityTelemetry.Capacity + 4, latest.Value0);
            Assert.AreEqual(40, Marshal.SizeOf<Atlas6LiabilityTelemetryRecord>());
        }

        private static void AssertAtlasRuntimeUsesGuardedSaveHotSwap(string projectRelativePath)
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, projectRelativePath));
            string hotSwapBody = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string registerBody = ExtractMethodBody(source, "private void TryRegisterSaveParticipant()");

            Assert.IsTrue(
                hotSwapBody.Contains("case GlobalRegistryServiceSlot.Save:") ||
                hotSwapBody.Contains("serviceSlot == GlobalRegistryServiceSlot.Save"),
                projectRelativePath);
            StringAssert.Contains("TryUnregisterSaveParticipant();", hotSwapBody);
            StringAssert.Contains("TryRegisterSaveParticipant();", hotSwapBody);
            StringAssert.DoesNotContain("_saveService.Register(this);", hotSwapBody);
            StringAssert.Contains("!Application.isPlaying", registerBody);
            StringAssert.Contains("!isActiveAndEnabled", registerBody);
            StringAssert.Contains("_saveService.Register(this);", registerBody);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            Assert.IsNotNull(source);
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Method body not closed: " + signature);
            return string.Empty;
        }

        private static int CountOccurrences(string source, string value)
        {
            Assert.IsNotNull(source);
            Assert.IsNotEmpty(value);

            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static void SetPrivateInstanceField(object target, string fieldName, object value)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            Assert.IsNotNull(target);
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName, params object[] args)
        {
            Assert.IsNotNull(target);
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, args);
        }

        private static T GetPrivateInstanceField<T>(object target, string fieldName)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        private static void SetPrivateInstanceProperty(object target, string propertyName, object value)
        {
            Assert.IsNotNull(target);
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(target, value);
        }

        private static void SetPrivateStaticProperty(Type ownerType, string propertyName, object value)
        {
            Assert.IsNotNull(ownerType);
            PropertyInfo property = ownerType.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property, propertyName);
            property.SetValue(null, value);
        }
    }
}
