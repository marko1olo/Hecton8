using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using Hecton8.AtlasSignal;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Narrative;
using Hecton8.Power;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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
                ModuleId = HectonSubmarineOsEvents.ModuleId,
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
            SubmarineOsEventPayload wrongModulePayload = payload;
            wrongModulePayload.ModuleId = 0u;

            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot));
            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildSnapshot(in wrongModulePayload, out _));
            Assert.AreEqual(0.32f, snapshot.EngineHeat01);
            Assert.AreEqual(0.66f, snapshot.EngineHeatTrue01);
            Assert.AreEqual(0.34f, snapshot.EngineHeatMaskDelta01);
            Assert.AreEqual(ThermalSheerManager.TelemetryFlagMasked, snapshot.AtlasTelemetryFlags);
            Assert.IsTrue(snapshot.IsEngineTelemetryMasked);
            Assert.AreEqual(64, Marshal.SizeOf<SubmarineOsEventPayload>());
        }

        [Test]
        public void SubmarineOsEventPayload_SanitizesBadSnapshotData()
        {
            SubmarineOsEventPayload invalidEmergency = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.SnapshotUpdated,
                EmergencyLevel = ushort.MaxValue
            };
            SubmarineOsEventPayload noisyPayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.SnapshotUpdated,
                StatusBits = (uint)SubsystemStatus.Engines | 0x80u,
                EmergencyLevel = (ushort)SubmarineEmergencyLevel.Nominal,
                PowerNormalized = float.NaN,
                OxygenNormalized = 2f,
                CarbonDioxideNormalized = -1f,
                MaxPressureKPa = float.MaxValue,
                SpeedKnots = float.MaxValue,
                EngineHeat01 = 2f,
                EngineHeatTrue01 = float.NegativeInfinity,
                EngineHeatMaskDelta01 = -0.5f,
                AtlasTelemetryFlags = ThermalSheerManager.TelemetryFlagMasked | 0x80000000u,
                SonarContactCount = -3,
                NearestSonarContactMeters = -40,
                VocalWarningFlags = (ushort)((ushort)SubmarineVwsFlags.ThermalStress | 0x8000)
            };

            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildSnapshot(in invalidEmergency, out _));
            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildSnapshot(in noisyPayload, out HectonSubmarineOsSnapshot snapshot));
            Assert.AreEqual(SubsystemStatus.Engines, snapshot.SubsystemStatus);
            Assert.AreEqual(0f, snapshot.PowerNormalized);
            Assert.AreEqual(1f, snapshot.OxygenNormalized);
            Assert.AreEqual(0f, snapshot.CarbonDioxideNormalized);
            Assert.AreEqual(999999f, snapshot.MaxPressureKPa);
            Assert.AreEqual(9999.9f, snapshot.SpeedKnots);
            Assert.AreEqual(1f, snapshot.EngineHeat01);
            Assert.AreEqual(0f, snapshot.EngineHeatTrue01);
            Assert.AreEqual(0f, snapshot.EngineHeatMaskDelta01);
            Assert.AreEqual(ThermalSheerManager.TelemetryFlagMasked, snapshot.AtlasTelemetryFlags);
            Assert.AreEqual(0, snapshot.SonarContactCount);
            Assert.AreEqual(0, snapshot.NearestSonarContactMeters);
            Assert.AreEqual(SubmarineVwsFlags.ThermalStress, snapshot.VocalWarningFlags);

            InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            try
            {
                HectonSubmarineOsSnapshot invalidSourceSnapshot = new HectonSubmarineOsSnapshot(
                    SubsystemStatus.Engines,
                    (SubmarineEmergencyLevel)250,
                    1f,
                    1f,
                    0f,
                    100f,
                    4f,
                    0.25f,
                    0.25f,
                    0f,
                    0u,
                    0,
                    0,
                    SubmarineVwsFlags.None,
                    false,
                    false,
                    false,
                    true);

                Assert.IsFalse(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in invalidSourceSnapshot));
                Assert.AreEqual(0, HectonSubmarineOsEvents.PendingCount);

                CapturingSubmarineOsListener captureListener = new CapturingSubmarineOsListener();
                HectonSubmarineOsEvents.Register(captureListener);
                HectonSubmarineOsSnapshot noisySourceSnapshot = new HectonSubmarineOsSnapshot(
                    (SubsystemStatus)(((byte)SubsystemStatus.Engines) | 0x80),
                    SubmarineEmergencyLevel.Nominal,
                    float.NaN,
                    2f,
                    -1f,
                    float.MaxValue,
                    float.MaxValue,
                    2f,
                    float.NegativeInfinity,
                    -0.5f,
                    ThermalSheerManager.TelemetryFlagMasked | 0x80000000u,
                    -3,
                    -40,
                    (SubmarineVwsFlags)(((ushort)SubmarineVwsFlags.ThermalStress) | 0x8000),
                    true,
                    false,
                    false,
                    true);

                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in noisySourceSnapshot));
                HectonSubmarineOsEvents.FlushPending();
                Assert.AreEqual(1, captureListener.Count);
                Assert.IsTrue(HectonSubmarineOsEvents.TryBuildSnapshot(in captureListener.LastPayload, out HectonSubmarineOsSnapshot sourceSnapshot));
                Assert.AreEqual(SubsystemStatus.Engines, sourceSnapshot.SubsystemStatus);
                Assert.AreEqual(0f, sourceSnapshot.PowerNormalized);
                Assert.AreEqual(1f, sourceSnapshot.OxygenNormalized);
                Assert.AreEqual(0f, sourceSnapshot.CarbonDioxideNormalized);
                Assert.AreEqual(999999f, sourceSnapshot.MaxPressureKPa);
                Assert.AreEqual(9999.9f, sourceSnapshot.SpeedKnots);
                Assert.AreEqual(1f, sourceSnapshot.EngineHeat01);
                Assert.AreEqual(0f, sourceSnapshot.EngineHeatTrue01);
                Assert.AreEqual(0f, sourceSnapshot.EngineHeatMaskDelta01);
                Assert.AreEqual(ThermalSheerManager.TelemetryFlagMasked, sourceSnapshot.AtlasTelemetryFlags);
                Assert.AreEqual(0, sourceSnapshot.SonarContactCount);
                Assert.AreEqual(0, sourceSnapshot.NearestSonarContactMeters);
                Assert.AreEqual(SubmarineVwsFlags.ThermalStress, sourceSnapshot.VocalWarningFlags);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            }
        }

        [Test]
        public void SubmarineOsLogRequest_CarriesAtlasThermalMaskTransitions()
        {
            SubmarineOsEventPayload maskedPayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryMasked,
                Priority = 2
            };
            SubmarineOsEventPayload restoredPayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryRestored,
                Priority = 1
            };
            SubmarineOsEventPayload unknownCodePayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = ushort.MaxValue,
                Priority = 1
            };
            SubmarineOsEventPayload zeroPriorityPayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryMasked,
                Priority = 0
            };
            SubmarineOsEventPayload overflowPriorityPayload = new SubmarineOsEventPayload
            {
                ModuleId = HectonSubmarineOsEvents.ModuleId,
                EventType = (ushort)SubmarineOsEventType.LogRequested,
                LogCode = (ushort)HectonSubmarineOsLogCode.EngineTelemetryMasked,
                Priority = 256
            };
            SubmarineOsEventPayload wrongModuleLogPayload = maskedPayload;
            wrongModuleLogPayload.ModuleId = 0u;

            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildLogRequest(in maskedPayload, out HectonSubmarineOsLogRequest maskedRequest));
            Assert.AreEqual(HectonSubmarineOsLogCode.EngineTelemetryMasked, maskedRequest.Code);
            Assert.AreEqual(2, maskedRequest.Priority);
            Assert.IsTrue(HectonSubmarineOsEvents.TryBuildLogRequest(in restoredPayload, out HectonSubmarineOsLogRequest restoredRequest));
            Assert.AreEqual(HectonSubmarineOsLogCode.EngineTelemetryRestored, restoredRequest.Code);
            Assert.AreEqual(1, restoredRequest.Priority);
            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildLogRequest(in unknownCodePayload, out _));
            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildLogRequest(in zeroPriorityPayload, out _));
            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildLogRequest(in overflowPriorityPayload, out _));
            Assert.IsFalse(HectonSubmarineOsEvents.TryBuildLogRequest(in wrongModuleLogPayload, out _));

            InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            try
            {
                HectonSubmarineOsLogRequest unknownRequest = new HectonSubmarineOsLogRequest(
                    (HectonSubmarineOsLogCode)250,
                    1);
                HectonSubmarineOsLogRequest zeroPriorityRequest = new HectonSubmarineOsLogRequest(
                    HectonSubmarineOsLogCode.EngineTelemetryMasked,
                    0);
                HectonSubmarineOsLogRequest validMaskRequest = new HectonSubmarineOsLogRequest(
                    HectonSubmarineOsLogCode.EngineTelemetryMasked,
                    2);

                Assert.IsFalse(HectonSubmarineOsEvents.TryRaiseLogRequested(in unknownRequest));
                Assert.IsFalse(HectonSubmarineOsEvents.TryRaiseLogRequested(in zeroPriorityRequest));
                Assert.AreEqual(0, HectonSubmarineOsEvents.PendingCount);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseLogRequested(in validMaskRequest));
                Assert.AreEqual(1, HectonSubmarineOsEvents.PendingCount);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            }
        }

        [Test]
        public void SubmarineOsEvents_ReportsBoundedQueueDropsByEventType()
        {
            InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            try
            {
                HectonSubmarineOsLogRequest normalRequest = new HectonSubmarineOsLogRequest(
                    HectonSubmarineOsLogCode.ReactorStable,
                    1);
                for (int i = 0; i < 16; i++)
                    Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseLogRequested(in normalRequest), i.ToString());

                Assert.AreEqual(16, HectonSubmarineOsEvents.PendingCount);
                Assert.AreEqual(0, HectonSubmarineOsEvents.DroppedEventCount);
                Assert.IsFalse(HectonSubmarineOsEvents.TryRaiseLogRequested(in normalRequest));
                Assert.AreEqual(1, HectonSubmarineOsEvents.DroppedEventCount);
                Assert.AreEqual(0, HectonSubmarineOsEvents.DroppedSnapshotEventCount);
                Assert.AreEqual(1, HectonSubmarineOsEvents.DroppedLogEventCount);

                HectonSubmarineOsSnapshot snapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Caution);
                Assert.IsFalse(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in snapshot));
                Assert.AreEqual(2, HectonSubmarineOsEvents.DroppedEventCount);
                Assert.AreEqual(1, HectonSubmarineOsEvents.DroppedSnapshotEventCount);
                Assert.AreEqual(1, HectonSubmarineOsEvents.DroppedLogEventCount);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            }
        }

        [Test]
        public void BiosMessageStreamer_PreservesHigherPriorityPendingEntriesWhenQueueFull()
        {
            GameObject normalBacklogHost = new GameObject("BIOS_NormalBacklog");
            GameObject criticalBacklogHost = new GameObject("BIOS_CriticalBacklog");
            GameObject wrappedBacklogHost = new GameObject("BIOS_WrappedBacklog");
            try
            {
                Hecton8.UI.BIOSMessageStreamer normalBacklog = CreateInitializedBiosStreamer(normalBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(normalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                InvokePrivateInstanceMethod(normalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(normalBacklog, "_pendingEntryCount"));
                Assert.AreEqual(11, CountPendingSubmarineOsEntries(normalBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(1, CountPendingSubmarineOsEntries(normalBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(normalBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(normalBacklog, "_droppedIncomingPendingEntryCount"));

                Hecton8.UI.BIOSMessageStreamer criticalBacklog = CreateInitializedBiosStreamer(criticalBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(criticalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                InvokePrivateInstanceMethod(criticalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(criticalBacklog, "_pendingEntryCount"));
                Assert.AreEqual(12, CountPendingSubmarineOsEntries(criticalBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(0, CountPendingSubmarineOsEntries(criticalBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(criticalBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(criticalBacklog, "_droppedIncomingPendingEntryCount"));

                Hecton8.UI.BIOSMessageStreamer wrappedBacklog = CreateInitializedBiosStreamer(wrappedBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                InvokePrivateInstanceMethod(wrappedBacklog, "TryStartNextEntry");
                InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);
                InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(wrappedBacklog, "_pendingEntryCount"));
                Assert.AreEqual(11, CountPendingSubmarineOsEntries(wrappedBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(1, CountPendingSubmarineOsEntries(wrappedBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(wrappedBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(wrappedBacklog, "_droppedIncomingPendingEntryCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalBacklogHost);
                UnityEngine.Object.DestroyImmediate(criticalBacklogHost);
                UnityEngine.Object.DestroyImmediate(wrappedBacklogHost);
            }
        }

        [Test]
        public void SubmarineOsDisplay_PreservesHigherPriorityPendingEntriesWhenQueueFull()
        {
            GameObject normalBacklogHost = new GameObject("Display_NormalBacklog");
            GameObject criticalBacklogHost = new GameObject("Display_CriticalBacklog");
            GameObject wrappedBacklogHost = new GameObject("Display_WrappedBacklog");
            try
            {
                Hecton8.UI.HectonSubmarineOsDisplay normalBacklog = CreateInactiveSubmarineOsDisplay(normalBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(normalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                InvokePrivateInstanceMethod(normalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(normalBacklog, "_pendingEntryCount"));
                Assert.AreEqual(11, CountPendingSubmarineOsEntries(normalBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(1, CountPendingSubmarineOsEntries(normalBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(normalBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(normalBacklog, "_droppedIncomingPendingEntryCount"));

                Hecton8.UI.HectonSubmarineOsDisplay criticalBacklog = CreateInactiveSubmarineOsDisplay(criticalBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(criticalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                InvokePrivateInstanceMethod(criticalBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(criticalBacklog, "_pendingEntryCount"));
                Assert.AreEqual(12, CountPendingSubmarineOsEntries(criticalBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(0, CountPendingSubmarineOsEntries(criticalBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(criticalBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(criticalBacklog, "_droppedIncomingPendingEntryCount"));

                Hecton8.UI.HectonSubmarineOsDisplay wrappedBacklog = CreateInactiveSubmarineOsDisplay(wrappedBacklogHost);
                for (int i = 0; i < 12; i++)
                    InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);

                InvokePrivateInstanceMethod(wrappedBacklog, "TryStartNextTypedEntry");
                InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.ReactorStable, (byte)1);
                InvokePrivateInstanceMethod(wrappedBacklog, "InsertPendingEntry", HectonSubmarineOsLogCode.FatalImplosion, (byte)3);

                Assert.AreEqual(12, GetPrivateInstanceField<int>(wrappedBacklog, "_pendingEntryCount"));
                Assert.AreEqual(11, CountPendingSubmarineOsEntries(wrappedBacklog, HectonSubmarineOsLogCode.ReactorStable));
                Assert.AreEqual(1, CountPendingSubmarineOsEntries(wrappedBacklog, HectonSubmarineOsLogCode.FatalImplosion));
                Assert.AreEqual(1, GetPrivateInstanceField<int>(wrappedBacklog, "_droppedQueuedPendingEntryCount"));
                Assert.AreEqual(0, GetPrivateInstanceField<int>(wrappedBacklog, "_droppedIncomingPendingEntryCount"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(normalBacklogHost);
                UnityEngine.Object.DestroyImmediate(criticalBacklogHost);
                UnityEngine.Object.DestroyImmediate(wrappedBacklogHost);
            }
        }

        [Test]
        public void SubmarineOsEvents_DefersListenerMutationsAndIsolatesExceptions()
        {
            InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            CountingSubmarineOsListener stableListener = new CountingSubmarineOsListener();
            CountingSubmarineOsListener selfRemovingListener = new CountingSubmarineOsListener();
            ThrowingSubmarineOsListener throwingListener = new ThrowingSubmarineOsListener();
            selfRemovingListener.OnEvent = () => HectonSubmarineOsEvents.Unregister(selfRemovingListener);

            try
            {
                HectonSubmarineOsEvents.Register(stableListener);
                HectonSubmarineOsEvents.Register(selfRemovingListener);

                HectonSubmarineOsSnapshot firstSnapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Caution);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in firstSnapshot));
                HectonSubmarineOsEvents.FlushPending();

                Assert.AreEqual(1, selfRemovingListener.Count);
                Assert.AreEqual(1, stableListener.Count);
                Assert.AreEqual(0, HectonSubmarineOsEvents.PendingCount);

                HectonSubmarineOsSnapshot secondSnapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Danger);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in secondSnapshot));
                HectonSubmarineOsEvents.FlushPending();

                Assert.AreEqual(1, selfRemovingListener.Count);
                Assert.AreEqual(2, stableListener.Count);

                int duplicateCountBeforeDispatchRegister = HectonSubmarineOsEvents.DuplicateListenerRegistrationCount;
                stableListener.OnEvent = () => HectonSubmarineOsEvents.Register(stableListener);
                HectonSubmarineOsSnapshot duplicateSnapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Caution);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in duplicateSnapshot));
                HectonSubmarineOsEvents.FlushPending();
                stableListener.OnEvent = null;

                Assert.AreEqual(3, stableListener.Count);
                Assert.GreaterOrEqual(
                    HectonSubmarineOsEvents.DuplicateListenerRegistrationCount,
                    duplicateCountBeforeDispatchRegister + 1);

                HectonSubmarineOsEvents.Register(throwingListener);
                HectonSubmarineOsSnapshot thirdSnapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Evacuate);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in thirdSnapshot));
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex("Submarine OS listener test exception"));
                HectonSubmarineOsEvents.FlushPending();

                Assert.AreEqual(1, throwingListener.Count);
                Assert.AreEqual(4, stableListener.Count);
                Assert.GreaterOrEqual(HectonSubmarineOsEvents.ListenerExceptionCount, 1);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            }
        }

        [Test]
        public void SubmarineOsEvents_DefersReentrantEventsToNextFlush()
        {
            InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            CountingSubmarineOsListener reentrantListener = new CountingSubmarineOsListener();
            bool reentrantEventRaised = false;
            bool reentrantEventAccepted = false;
            reentrantListener.OnEvent = () =>
            {
                if (reentrantEventRaised)
                    return;

                reentrantEventRaised = true;
                HectonSubmarineOsLogRequest reentrantRequest = new HectonSubmarineOsLogRequest(
                    HectonSubmarineOsLogCode.EngineTelemetryMasked,
                    2);
                reentrantEventAccepted = HectonSubmarineOsEvents.TryRaiseLogRequested(in reentrantRequest);
            };

            try
            {
                HectonSubmarineOsEvents.Register(reentrantListener);
                HectonSubmarineOsSnapshot snapshot = CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel.Caution);
                Assert.IsTrue(HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in snapshot));

                HectonSubmarineOsEvents.FlushPending();
                Assert.IsTrue(reentrantEventRaised);
                Assert.IsTrue(reentrantEventAccepted);
                Assert.AreEqual(1, reentrantListener.Count);
                Assert.AreEqual(1, HectonSubmarineOsEvents.PendingCount);

                HectonSubmarineOsEvents.FlushPending();
                Assert.AreEqual(2, reentrantListener.Count);
                Assert.AreEqual(0, HectonSubmarineOsEvents.PendingCount);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(HectonSubmarineOsEvents), "ResetStaticState");
            }
        }

        [Test]
        public void PowerGridTelemetryEvents_DefersListenerMutationsAndIsolatesExceptions()
        {
            InvokePrivateStaticMethod(typeof(PowerGridTelemetryEvents), "ResetStaticState");
            CountingPowerGridTelemetryListener stableListener = new CountingPowerGridTelemetryListener();
            CountingPowerGridTelemetryListener selfRemovingListener = new CountingPowerGridTelemetryListener();
            ThrowingPowerGridTelemetryListener throwingListener = new ThrowingPowerGridTelemetryListener();
            selfRemovingListener.OnEvent = () => PowerGridTelemetryEvents.Unregister(selfRemovingListener);

            try
            {
                PowerGridTelemetryEvents.Register(stableListener);
                PowerGridTelemetryEvents.Register(selfRemovingListener);

                PowerGridTelemetrySnapshot firstSnapshot = CreatePowerGridTelemetryTestSnapshot(1f);
                Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in firstSnapshot));
                PowerGridTelemetryEvents.FlushPending();

                Assert.AreEqual(1, selfRemovingListener.Count);
                Assert.AreEqual(1, stableListener.Count);
                Assert.AreEqual(0, PowerGridTelemetryEvents.PendingCount);

                PowerGridTelemetrySnapshot secondSnapshot = CreatePowerGridTelemetryTestSnapshot(0.75f);
                Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in secondSnapshot));
                PowerGridTelemetryEvents.FlushPending();

                Assert.AreEqual(1, selfRemovingListener.Count);
                Assert.AreEqual(2, stableListener.Count);
                Assert.AreEqual(0.75f, stableListener.LastSnapshot.SupplyRatio);

                int duplicateCountBeforeDispatchRegister = PowerGridTelemetryEvents.DuplicateListenerRegistrationCount;
                stableListener.OnEvent = () => PowerGridTelemetryEvents.Register(stableListener);
                PowerGridTelemetrySnapshot duplicateSnapshot = CreatePowerGridTelemetryTestSnapshot(0.5f);
                Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in duplicateSnapshot));
                PowerGridTelemetryEvents.FlushPending();
                stableListener.OnEvent = null;

                Assert.AreEqual(3, stableListener.Count);
                Assert.GreaterOrEqual(
                    PowerGridTelemetryEvents.DuplicateListenerRegistrationCount,
                    duplicateCountBeforeDispatchRegister + 1);

                PowerGridTelemetryEvents.Register(throwingListener);
                PowerGridTelemetrySnapshot thirdSnapshot = CreatePowerGridTelemetryTestSnapshot(0.25f);
                Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in thirdSnapshot));
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex("Power grid listener test exception"));
                PowerGridTelemetryEvents.FlushPending();

                Assert.AreEqual(1, throwingListener.Count);
                Assert.AreEqual(4, stableListener.Count);
                Assert.GreaterOrEqual(PowerGridTelemetryEvents.ListenerExceptionCount, 1);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(PowerGridTelemetryEvents), "ResetStaticState");
            }
        }

        [Test]
        public void PowerGridTelemetryEvents_DefersReentrantEventsAndReportsQueueOverflow()
        {
            InvokePrivateStaticMethod(typeof(PowerGridTelemetryEvents), "ResetStaticState");
            CountingPowerGridTelemetryListener reentrantListener = new CountingPowerGridTelemetryListener();
            bool reentrantEventRaised = false;
            bool reentrantEventAccepted = false;
            reentrantListener.OnEvent = () =>
            {
                if (reentrantEventRaised)
                    return;

                reentrantEventRaised = true;
                PowerGridTelemetrySnapshot reentrantSnapshot = CreatePowerGridTelemetryTestSnapshot(0.5f);
                reentrantEventAccepted = PowerGridTelemetryEvents.TryRaise(in reentrantSnapshot);
            };

            try
            {
                PowerGridTelemetryEvents.Register(reentrantListener);
                PowerGridTelemetrySnapshot snapshot = CreatePowerGridTelemetryTestSnapshot(1f);
                Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in snapshot));

                PowerGridTelemetryEvents.FlushPending();
                Assert.IsTrue(reentrantEventRaised);
                Assert.IsTrue(reentrantEventAccepted);
                Assert.AreEqual(1, reentrantListener.Count);
                Assert.AreEqual(1, PowerGridTelemetryEvents.PendingCount);

                PowerGridTelemetryEvents.FlushPending();
                Assert.AreEqual(2, reentrantListener.Count);
                Assert.AreEqual(0, PowerGridTelemetryEvents.PendingCount);

                for (int i = 0; i < 8; i++)
                {
                    PowerGridTelemetrySnapshot queuedSnapshot = CreatePowerGridTelemetryTestSnapshot(1f - (i * 0.05f));
                    Assert.IsTrue(PowerGridTelemetryEvents.TryRaise(in queuedSnapshot), i.ToString());
                }

                Assert.AreEqual(8, PowerGridTelemetryEvents.PendingCount);
                Assert.AreEqual(0, PowerGridTelemetryEvents.DroppedEventCount);

                PowerGridTelemetrySnapshot overflowSnapshot = CreatePowerGridTelemetryTestSnapshot(0.1f);
                Assert.IsFalse(PowerGridTelemetryEvents.TryRaise(in overflowSnapshot));
                Assert.AreEqual(1, PowerGridTelemetryEvents.DroppedEventCount);
                Assert.AreEqual(8, PowerGridTelemetryEvents.PendingCount);
            }
            finally
            {
                InvokePrivateStaticMethod(typeof(PowerGridTelemetryEvents), "ResetStaticState");
            }
        }

        [Test]
        public void SubmarineAtmospherePressureEvents_DeferMutationsAndExposeFailureCounters()
        {
            string atmosphere = ReadProjectFile("Assets", "_Project", "Scripts", "SubmarineAtmosphereSystem.cs");
            string playerMovement = ReadProjectFile("Assets", "_Project", "Scripts", "HectonPlayerMovement.cs");
            string highPressureBus = ExtractMethodBody(atmosphere, "public static class HighPressureEvents");
            string fatalPressureBus = ExtractMethodBody(atmosphere, "public static class FatalPressureImplosionEvents");
            string startFatalPressureSequence = ExtractMethodBody(playerMovement, "private void StartFatalPressureSequence()");
            string pushFatalPressureWarning = ExtractMethodBody(playerMovement, "private void PushFatalPressureCorruptionWarning()");
            string tryPushFatalPressureNotification = ExtractMethodBody(playerMovement, "private void TryPushFatalPressureNotification(");
            string reportFatalPressureNotificationMiss = ExtractMethodBody(playerMovement, "private void ReportFatalPressureNotificationMiss()");
            string clearFatalPressureNotificationDiagnostics = ExtractMethodBody(playerMovement, "private void ClearFatalPressureNotificationDiagnostics()");
            string movementOnDisable = ExtractMethodBody(playerMovement, "private void OnDisable()");
            string triggerFatalPressureImplosion = ExtractMethodBody(playerMovement, "private void TriggerFatalPressureImplosion()");

            AssertPressureEventBusContract(highPressureBus, "HighPressureEvents");
            AssertPressureEventBusContract(fatalPressureBus, "FatalPressureImplosionEvents");
            Assert.That(playerMovement, Does.Contain("using Hecton8.Atmosphere;"));
            Assert.That(playerMovement, Does.Contain("_fatalPressureImplosionSourceHash"));
            Assert.That(playerMovement, Does.Contain("_fatalPressureNotificationMissWarningHash"));
            Assert.That(playerMovement, Does.Contain("_fatalPressureNotificationContextHash"));
            Assert.That(playerMovement, Does.Contain("public int FatalPressureNotificationMissCount =>"));
            AssertSourceOrder(
                startFatalPressureSequence,
                "_fatalPressureSequenceIntensity = 0.01f;",
                "ApplyFatalPressureVisorCorruption(corruptionIntensity);");
            AssertSourceOrder(
                startFatalPressureSequence,
                "ApplyFatalPressureVisorCorruption(corruptionIntensity);",
                "PushFatalPressureCorruptionWarning();");
            Assert.That(pushFatalPressureWarning, Does.Contain("TryPushFatalPressureNotification(message);"));
            Assert.That(pushFatalPressureWarning, Does.Not.Contain("NotificationEvents.TryPushCritical(message);"));
            Assert.That(tryPushFatalPressureNotification, Does.Contain("if (NotificationEvents.TryPushCritical(message))"));
            Assert.That(tryPushFatalPressureNotification, Does.Contain("ReportFatalPressureNotificationMiss();"));
            Assert.That(reportFatalPressureNotificationMiss, Does.Contain("_fatalPressureNotificationMissCount++;"));
            Assert.That(reportFatalPressureNotificationMiss, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning("));
            Assert.That(reportFatalPressureNotificationMiss, Does.Contain("_fatalPressureNotificationMissWarningHash"));
            Assert.That(reportFatalPressureNotificationMiss, Does.Contain("_fatalPressureNotificationContextHash"));
            Assert.That(reportFatalPressureNotificationMiss, Does.Contain("math.max(1, _fatalPressureNotificationMissCount)"));
            Assert.That(clearFatalPressureNotificationDiagnostics, Does.Contain("_fatalPressureNotificationMissCount = 0;"));
            Assert.That(movementOnDisable, Does.Contain("ClearFatalPressureNotificationDiagnostics();"));
            Assert.That(triggerFatalPressureImplosion, Does.Contain("Vector3 runtimePosition = ResolvePlayerAupRuntimePosition();"));
            Assert.That(triggerFatalPressureImplosion, Does.Contain("new FatalPressureImplosionEvent("));
            Assert.That(triggerFatalPressureImplosion, Does.Contain("_fatalPressureImplosionSourceHash"));
            Assert.That(triggerFatalPressureImplosion, Does.Contain("FatalPressureImplosionEvents.TryNotify(in implosionEvent);"));
            Assert.That(triggerFatalPressureImplosion, Does.Contain("runtimePosition"));
            AssertSourceOrder(
                triggerFatalPressureImplosion,
                "FatalPressureImplosionEvents.TryNotify(in implosionEvent);",
                "StartWipeout(");
        }

        [Test]
        public void DroneFleetPlayerAnchorsUseRuntimeSnapshotsBeforeFallbackRoutes()
        {
            string droneFleet = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "DroneFleetManager.cs");
            string playerPosition = ExtractMethodBody(droneFleet, "private static bool TryResolvePlayerPosition(out Vector3 position)");
            string renderReference = ExtractMethodBody(droneFleet, "private static double3 ResolveDroneRenderReferenceAup()");
            string playerAup = ExtractMethodBody(droneFleet, "private static bool TryResolvePlayerAup(out double3 playerAup)");

            AssertSourceOrder(
                playerPosition,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            Assert.That(playerPosition, Does.Contain("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u"));
            Assert.That(playerPosition, Does.Contain("snapshot.Aup.IsFinite()"));
            Assert.That(playerPosition, Does.Contain("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u"));
            Assert.That(playerPosition, Does.Contain("!movementState.PredictedAup.IsFinite()"));
            Assert.That(playerPosition, Does.Contain("!math.all(math.isfinite(movementState.WorldPosition))"));
            Assert.That(playerPosition, Does.Contain("movementState.WorldPosition.x"));
            Assert.That(playerPosition, Does.Not.Contain("playerContext.PlayerMovement"));
            Assert.That(playerPosition, Does.Not.Contain("playerMovement.CurrentAup"));

            AssertSourceOrder(
                playerAup,
                "playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot)",
                "playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState)");
            Assert.That(playerAup, Does.Contain("(snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u"));
            Assert.That(playerAup, Does.Contain("snapshot.Aup.IsFinite()"));
            Assert.That(playerAup, Does.Contain("(movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u"));
            Assert.That(playerAup, Does.Contain("!movementState.PredictedAup.IsFinite()"));
            Assert.That(playerAup, Does.Contain("playerAup = movementState.PredictedAup.ToAbsoluteDouble3();"));
            Assert.That(playerAup, Does.Not.Contain("playerContext.PlayerMovement"));
            Assert.That(playerAup, Does.Not.Contain("playerMovement.CurrentAup"));

            AssertSourceOrder(renderReference, "if (s_CachedPlayerRuntime != null)", "RuntimeOriginRoute.CurrentRuntimeOriginAup()");
        }

        [Test]
        public void RepairDroneTorchAcousticEvents_DeferMutationsAndReleaseSidecarOnListenerFailure()
        {
            string repairDrone = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "RepairDroneEntity.cs");
            string droneFleet = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "DroneFleetManager.cs");
            string repairHub = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "RepairDroneHub.cs");
            string baseLogistics = ReadProjectFile("Assets", "_Project", "Scripts", "Construction", "BaseLogisticsNetwork.cs");
            string systemDispatcher = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "SystemDispatcher.cs");
            string sceneRuntime = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "SceneRuntimeService.cs");
            string threadSafeQueue = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "ThreadSafeCommandQueue.cs");
            string saveManager = ReadProjectFile("Assets", "_Project", "Scripts", "SaveManager.cs");
            string bus = ExtractMethodBody(repairDrone, "public static class RepairDroneTorchAcousticEvents");
            string flush = ExtractMethodBody(bus, "public static void FlushPending()");
            string register = ExtractMethodBody(bus, "public static void Register(");
            string unregister = ExtractMethodBody(bus, "public static void Unregister(");
            string dispatch = ExtractMethodBody(bus, "private static void Dispatch(");
            string flushAIEventsArtery = ExtractMethodBody(systemDispatcher, "private static void FlushAIEventsArtery()");
            string clearRuntimeState = ExtractMethodBody(sceneRuntime, "private static void ClearRuntimeState()");
            string raiseStorageReservationCommitResolved = ExtractMethodBody(threadSafeQueue, "private static void RaiseStorageReservationCommitResolved(");
            string enqueueStorageReservationCommitResolved = ExtractMethodBody(threadSafeQueue, "private static bool EnqueueStorageReservationCommitResolved(");
            string dispatchStorageReservationCommitResolved = ExtractMethodBody(threadSafeQueue, "private static void DispatchStorageReservationCommitResolved(");
            string dispatchStorageReservationCommitResolvedToListener = ExtractMethodBody(threadSafeQueue, "private static void DispatchStorageReservationCommitResolvedToListener(");
            string reportStorageReservationCommitListenerException = ExtractMethodBody(threadSafeQueue, "private static void ReportStorageReservationCommitListenerException(");
            string tryEnqueue = ExtractMethodBody(threadSafeQueue, "public static bool TryEnqueue(");
            string reportCommandOverflow = ExtractMethodBody(threadSafeQueue, "private static void ReportCommandOverflowOncePerFrame()");
            string reportStorageReservationCommitOverflow = ExtractMethodBody(threadSafeQueue, "private static void ReportStorageReservationCommitOverflowOncePerFrame()");
            string reportStorageReservationCommitListenerExceptionOncePerFrame = ExtractMethodBody(threadSafeQueue, "private static void ReportStorageReservationCommitListenerExceptionOncePerFrame()");
            string publishQueueWarning = ExtractMethodBody(threadSafeQueue, "private static void PublishQueuePerformanceWarningBestEffort(");
            string clearQueue = ExtractMethodBody(threadSafeQueue, "public static void Clear()");
            string shutdownQueue = ExtractMethodBody(threadSafeQueue, "public static void Shutdown()");
            string preparePersistenceSnapshot = ExtractMethodBody(threadSafeQueue, "public static void PrepareStorageReservationCommitBridgeForPersistenceSnapshot()");
            string drainAbandonedPendingCommands = ExtractMethodBody(threadSafeQueue, "private static void DrainAbandonedPendingCommands(");
            string resolveAbandonedPendingCommand = ExtractMethodBody(threadSafeQueue, "private static void ResolveAbandonedPendingCommand(");
            string drainPendingStorageReservationCommitsForPersistence = ExtractMethodBody(threadSafeQueue, "private static void DrainPendingStorageReservationCommitsForPersistenceSnapshot()");
            string releaseAbandonedStorageReservationCommit = ExtractMethodBody(threadSafeQueue, "private static void ReleaseAbandonedStorageReservationCommit(");
            string drainAbandonedStorageReservationCommitResolvedEvents = ExtractMethodBody(threadSafeQueue, "private static void DrainAbandonedStorageReservationCommitResolvedEvents(");
            string dispatchStorageReservationCommitResolvedFailure = ExtractMethodBody(threadSafeQueue, "private static void DispatchStorageReservationCommitResolvedFailure(");
            string disposeTrackedQueue = ExtractMethodBody(threadSafeQueue, "private static void DisposeTrackedPersistentQueue<T>(");
            string markCommandQueueUnavailable = ExtractMethodBody(threadSafeQueue, "private static void MarkCommandQueueUnavailableIfNativeStorageMissing()");
            string markStorageReservationAckQueueUnavailable = ExtractMethodBody(threadSafeQueue, "private static void MarkStorageReservationCommitResolvedQueueUnavailableIfNativeStorageMissing()");
            string executeCommand = ExtractMethodBody(threadSafeQueue, "private static void ExecuteCommand(");
            string registerOneShotTarget = ExtractMethodBody(threadSafeQueue, "public static int RegisterOneShotGameObjectTarget(");
            string tryResolveTarget = ExtractMethodBody(threadSafeQueue, "private static bool TryResolveTarget(");
            string unregisterTokenLocked = ExtractMethodBody(threadSafeQueue, "private static void UnregisterTokenLocked(");
            string removeInstanceTokenValueLocked = ExtractMethodBody(threadSafeQueue, "private static bool RemoveInstanceTokenValueLocked(");
            string fleetReset = ExtractMethodBody(droneFleet, "private static void ResetStaticState()");
            string ensureDroneFleetInitialized = ExtractMethodBody(droneFleet, "private static void EnsureInitialized()");
            string ensureStorageReservationCommitResolvedBridge = ExtractMethodBody(droneFleet, "private static void EnsureStorageReservationCommitResolvedBridge()");
            string clearDroneSceneRuntime = ExtractMethodBody(droneFleet, "internal static void ClearSceneTransitionRuntimeState()");
            string resupplyCommitAck = ExtractMethodBody(droneFleet, "private static void HandleStorageReservationCommitResolved(");
            string tryApplyResolvedResupplyCommit = ExtractMethodBody(droneFleet, "private static bool TryApplyResolvedResupplyCommitToLiveSlot(");
            string tryConsumeResolvedResupplyCommit = ExtractMethodBody(droneFleet, "private static bool TryConsumeResolvedResupplyCommitAck(");
            string clearPendingResupplyCommitAck = ExtractMethodBody(droneFleet, "private static void ClearPendingResupplyCommitAck(");
            string refreshFleetStatusSnapshotFromDroneStates = ExtractMethodBody(droneFleet, "private static void RefreshFleetStatusSnapshotFromDroneStates(");
            string publishStorageReservationAckWarning = ExtractMethodBody(droneFleet, "private static void PublishStorageReservationAckWarningBestEffort(");
            string applyPendingControls = ExtractMethodBody(droneFleet, "private static void ApplyPendingControls(");
            string applyHeadlessResupply = ExtractMethodBody(droneFleet, "private static void ApplyHeadlessResupply(");
            string applyPendingLaunches = ExtractMethodBody(droneFleet, "private static void ApplyPendingLaunches(");
            string configureAcoustic = ExtractMethodBody(droneFleet, "internal static void ConfigureRepairTorchAcoustic(");
            string clearAcoustic = ExtractMethodBody(droneFleet, "internal static void ClearRepairTorchAcousticBinding()");
            string publishSparks = ExtractMethodBody(droneFleet, "private static void PublishDroneRepairSparks(");
            string publishAcoustic = ExtractMethodBody(droneFleet, "private static void PublishDroneRepairTorchAcoustic(");
            string hubBindings = ExtractMethodBody(repairHub, "private void ConfigureDroneFleetRuntimeBindings()");
            string hubRefreshAcoustic = ExtractMethodBody(repairHub, "private static void RefreshRepairTorchAcousticBindingFromActiveHubs()");
            string hubReset = ExtractMethodBody(repairHub, "private static void ResetStaticState()");
            string hubRegister = ExtractMethodBody(repairHub, "private void RegisterHubInstance()");
            string hubUnregister = ExtractMethodBody(repairHub, "private void UnregisterHubInstance()");
            string hasRepairSupply = ExtractMethodBody(repairHub, "private bool HasRepairSupplyAvailable(");
            string consumeRepairSupply = ExtractMethodBody(repairHub, "out int queuedReservationId)");
            string acquireDroneResupply = ExtractMethodBody(repairHub, "internal bool TryAcquireDroneResupply(");
            string queueDroneResupply = ExtractMethodBody(repairHub, "internal bool TryQueueDroneResupplyCommit(");
            string commitViaQueue = ExtractMethodBody(baseLogistics, "public static bool CommitReservedViaCommandQueue(LogisticsReservation reservation, int requesterId)");
            string tryCommitViaQueue = ExtractMethodBody(baseLogistics, "public static bool TryCommitReservedViaCommandQueue(");
            string nearestSupplyEndpoint = ExtractMethodBody(repairHub, "private bool TryResolveNearestSupplyEndpoint(StorageCrate[] crates, int count, Vector3 requesterPosition, ref Vector3 endpointPosition)");
            string resolveRepairSupplySlot = ExtractMethodBody(repairHub, "private bool TryResolveRepairSupplySlot(StorageCrate[] crates, int count, int requiredUnits, bool consume)");
            string resolvePrimarySupplyHash = ExtractMethodBody(repairHub, "private int ResolvePrimaryRepairSupplyHashId()");
            string resolveAvailableSupplyHash = ExtractMethodBody(repairHub, "private int ResolveAvailableRepairSupplyHashId(");
            string countRepairSupply = ExtractMethodBody(repairHub, "private int CountRepairSupplyUnits(");
            string crateContainsRepairSupply = ExtractMethodBody(repairHub, "private bool CrateContainsRepairSupply(");
            string countCrateRepairSupply = ExtractMethodBody(repairHub, "private static int CountRepairSupplyUnits(StorageCrate crate, int primaryHashId)");
            string consumeCrateRepairSupply = ExtractMethodBody(repairHub, "private static bool TryConsumeRepairSupplyUnit(");
            string saveGame = ExtractMethodBody(saveManager, "private async Awaitable SaveGameAsyncInternal(");
            int hubRegisterAddIndex = hubRegister.IndexOf("s_ActiveHubs[s_ActiveHubCount++] = this;", StringComparison.Ordinal);
            Assert.GreaterOrEqual(hubRegisterAddIndex, 0);
            string hubRegisterAfterAdd = hubRegister.Substring(hubRegisterAddIndex);

            AssertRepairDroneTorchAcousticEventBusContract(bus);
            Assert.That(register, Does.Contain("QueueDeferredRegister(listener);"));
            Assert.That(register, Does.Contain("RegisterImmediate(listener);"));
            Assert.That(unregister, Does.Contain("QueueDeferredUnregister(listener);"));
            Assert.That(unregister, Does.Contain("TryUnregisterImmediate(listener);"));
            Assert.That(dispatch, Does.Contain("DispatchToListener(listener, in acousticEvent);"));
            Assert.That(dispatch, Does.Contain("ApplyDeferredListenerMutations();"));
            AssertSourceOrder(flush, "Dispatch(in payload);", "ReleaseReferenceSlotForPayload(in payload);");
            Assert.That(flushAIEventsArtery, Does.Contain("HectonDroneFleetEvents.PendingCount +"));
            Assert.That(flushAIEventsArtery, Does.Contain("RepairDroneTorchAcousticEvents.PendingCount"));
            Assert.That(flushAIEventsArtery, Does.Contain("HectonDroneFleetEvents.FlushPending();"));
            Assert.That(flushAIEventsArtery, Does.Contain("RepairDroneTorchAcousticEvents.FlushPending();"));
            AssertSourceOrder(flushAIEventsArtery, "HectonDroneFleetEvents.FlushPending();", "RepairDroneTorchAcousticEvents.FlushPending();");
            Assert.That(clearRuntimeState, Does.Contain("DroneFleetManager.ClearSceneTransitionRuntimeState();"));
            AssertSourceOrder(clearRuntimeState, "DroneFleetManager.ClearSceneTransitionRuntimeState();", "ThreadSafeCommandQueue.Clear();");
            Assert.That(clearDroneSceneRuntime, Does.Contain("CompletePendingHeadlessJobForReset();"));
            Assert.That(clearDroneSceneRuntime, Does.Contain("ReleaseHeadlessSdfReadLease();"));
            Assert.That(clearDroneSceneRuntime, Does.Contain("ClearAllHeadlessSlots();"));
            Assert.That(clearDroneSceneRuntime, Does.Contain("ClearHeadlessManagedState();"));
            AssertSourceOrder(clearDroneSceneRuntime, "ClearAllHeadlessSlots();", "ClearHeadlessManagedState();");
            Assert.That(repairHub, Does.Contain("private AudioClip repairTorchAcousticClip;"));
            Assert.That(repairHub, Does.Contain("private float repairTorchAcousticVolume"));
            Assert.That(repairHub, Does.Contain("private float repairTorchAcousticPitch"));
            Assert.That(droneFleet, Does.Contain("internal static int SignalPushDropCount => System.Threading.Volatile.Read(ref s_SignalPushDropCount);"));
            Assert.That(fleetReset, Does.Contain("s_SignalPushDropCount = 0;"));
            Assert.That(fleetReset, Does.Contain("ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);"));
            Assert.That(fleetReset, Does.Contain("s_StorageReservationCommitResolvedListenerGeneration = -1;"));
            Assert.That(threadSafeQueue, Does.Contain("StorageReservationCommitListenerGeneration =>"));
            Assert.That(threadSafeQueue, Does.Contain("AdvanceStorageReservationCommitListenerGeneration();"));
            Assert.That(threadSafeQueue, Does.Contain("void ReleaseReservation(int reservationId);"));
            Assert.That(threadSafeQueue, Does.Contain("private static readonly EntityCommand[] _persistenceSnapshotCommandBuffer = new EntityCommand[MaxMainThreadCommandsPerDrain];"));
            Assert.That(threadSafeQueue, Does.Contain("private static int _persistenceSnapshotGate;"));
            Assert.That(clearQueue, Does.Contain("DrainAbandonedPendingCommands(dispatchStorageReservationFailures: false);"));
            Assert.That(clearQueue, Does.Contain("DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: false);"));
            Assert.That(shutdownQueue, Does.Contain("DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);"));
            Assert.That(shutdownQueue, Does.Contain("DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);"));
            AssertSourceOrder(shutdownQueue, "DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);", "DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);");
            AssertSourceOrder(shutdownQueue, "DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);", "DisposeTrackedPersistentQueue(ref _pendingCommands");
            Assert.That(preparePersistenceSnapshot, Does.Contain("EnterGate(ref _persistenceSnapshotGate);"));
            Assert.That(preparePersistenceSnapshot, Does.Contain("DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);"));
            Assert.That(preparePersistenceSnapshot, Does.Contain("DrainPendingStorageReservationCommitsForPersistenceSnapshot();"));
            AssertSourceOrder(preparePersistenceSnapshot, "DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);", "DrainPendingStorageReservationCommitsForPersistenceSnapshot();");
            Assert.That(drainPendingStorageReservationCommitsForPersistence, Does.Contain("_persistenceSnapshotCommandBuffer[drainedCommandCount++] = command;"));
            Assert.That(drainPendingStorageReservationCommitsForPersistence, Does.Contain("command.CommandType == EntityCommandType.CommitStorageReservation"));
            Assert.That(drainPendingStorageReservationCommitsForPersistence, Does.Contain("_pendingCommands.Enqueue(command);"));
            Assert.That(drainPendingStorageReservationCommitsForPersistence, Does.Contain("ResolveAbandonedPendingCommand(in command, dispatchStorageReservationFailures: true);"));
            Assert.That(saveGame, Does.Contain("ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();"));
            AssertSourceOrder(saveGame, "await Hecton8.Core.AwaitableDebtMonitor.NextFrameAsync(destroyCancellationToken);", "ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();");
            AssertSourceOrder(saveGame, "ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();", "SortRegistryIfDirty(SavePriorityComparer);");
            Assert.That(drainAbandonedPendingCommands, Does.Contain("TryDequeuePendingCommandForAbandon(out EntityCommand command)"));
            Assert.That(drainAbandonedPendingCommands, Does.Contain("ResolveAbandonedPendingCommand(in command, dispatchStorageReservationFailures);"));
            Assert.That(drainAbandonedStorageReservationCommitResolvedEvents, Does.Contain("TryDequeuePendingStorageReservationCommitResolvedForAbandon(out StorageReservationCommitResolvedPayload payload)"));
            Assert.That(drainAbandonedStorageReservationCommitResolvedEvents, Does.Contain("DispatchStorageReservationCommitResolved(in payload);"));
            Assert.That(resolveAbandonedPendingCommand, Does.Contain("EntityCommandType.CommitStorageReservation"));
            Assert.That(resolveAbandonedPendingCommand, Does.Contain("ReleaseAbandonedStorageReservationCommit(in command);"));
            Assert.That(resolveAbandonedPendingCommand, Does.Contain("DispatchStorageReservationCommitResolvedFailure(command.SecondaryToken, command.IntValue);"));
            Assert.That(releaseAbandonedStorageReservationCommit, Does.Contain("target.ReleaseReservation(command.IntValue);"));
            Assert.That(releaseAbandonedStorageReservationCommit, Does.Contain("TryUnregisterOneShotTarget(command.TargetToken);"));
            Assert.That(dispatchStorageReservationCommitResolvedFailure, Does.Contain("Committed = 0"));
            Assert.That(dispatchStorageReservationCommitResolvedFailure, Does.Contain("DispatchStorageReservationCommitResolved(in payload);"));
            Assert.That(shutdownQueue, Does.Contain("AdvanceStorageReservationCommitListenerGeneration();"));
            Assert.That(ensureDroneFleetInitialized, Does.Contain("EnsureStorageReservationCommitResolvedBridge();"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("s_StorageReservationCommitResolvedListenerGeneration == listenerGeneration"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge)"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("s_StorageReservationCommitResolvedListenerGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;"));
            Assert.That(ensureStorageReservationCommitResolvedBridge, Does.Contain("s_StorageReservationCommitResolvedListenerGeneration = -1;"));
            AssertSourceOrder(ensureStorageReservationCommitResolvedBridge, "ThreadSafeCommandQueue.Unregister(s_StorageReservationCommitResolvedBridge);", "ThreadSafeCommandQueue.Register(s_StorageReservationCommitResolvedBridge)");
            Assert.That(resupplyCommitAck, Does.Contain("s_PendingResupplyGrantBySlot == null"));
            Assert.That(resupplyCommitAck, Does.Contain("s_PendingResupplyFailureBySlot == null"));
            Assert.That(resupplyCommitAck, Does.Contain("s_PendingResupplyReservationIdsBySlot == null"));
            Assert.That(resupplyCommitAck, Does.Contain("slot >= s_PendingResupplyGrantBySlot.Length"));
            Assert.That(resupplyCommitAck, Does.Contain("slot >= s_PendingResupplyFailureBySlot.Length"));
            Assert.That(resupplyCommitAck, Does.Contain("slot >= s_PendingResupplyReservationIdsBySlot.Length"));
            AssertSourceOrder(resupplyCommitAck, "slot >= s_PendingResupplyReservationIdsBySlot.Length)", "ReportStorageReservationStaleAck(requesterId);");
            AssertSourceOrder(resupplyCommitAck, "ReportStorageReservationStaleAck(requesterId);", "int expectedReservationId = s_PendingResupplyReservationIdsBySlot[slot];");
            Assert.That(resupplyCommitAck, Does.Contain("int expectedReservationId = s_PendingResupplyReservationIdsBySlot[slot];"));
            Assert.That(resupplyCommitAck, Does.Contain("if (expectedReservationId <= 0)"));
            Assert.That(resupplyCommitAck, Does.Contain("ReportStorageReservationStaleAck(requesterId);"));
            Assert.That(resupplyCommitAck, Does.Contain("if (reservationId != expectedReservationId)"));
            Assert.That(resupplyCommitAck, Does.Contain("ReportStorageReservationMismatchAck(reservationId);"));
            AssertSourceOrder(resupplyCommitAck, "if (expectedReservationId <= 0)", "if (reservationId != expectedReservationId)");
            AssertSourceOrder(resupplyCommitAck, "if (reservationId != expectedReservationId)", "bool commitSucceeded = committed && reservationId > 0;");
            Assert.That(resupplyCommitAck, Does.Contain("bool commitSucceeded = committed && reservationId > 0;"));
            Assert.That(resupplyCommitAck, Does.Contain("TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded)"));
            AssertSourceOrder(resupplyCommitAck, "bool commitSucceeded = committed && reservationId > 0;", "TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded)");
            AssertSourceOrder(resupplyCommitAck, "TryApplyResolvedResupplyCommitToLiveSlot(slot, commitSucceeded)", "if (commitSucceeded)");
            Assert.That(resupplyCommitAck, Does.Contain("if (commitSucceeded)"));
            Assert.That(resupplyCommitAck, Does.Contain("if (s_PendingResupplyGrantBySlot[slot])"));
            AssertSourceOrder(resupplyCommitAck, "if (s_PendingResupplyGrantBySlot[slot])", "s_PendingResupplyFailureBySlot[slot] = true;");
            Assert.That(resupplyCommitAck, Does.Not.Contain("reservationId <= 0 ||"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("TryAcquireDroneCoreMirrorMutationViews("));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("TryConsumeResolvedResupplyCommitAck(slot, committed, ref drone, out bool droneChanged)"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("RefreshHeadlessCounters(droneStates);"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("RefreshFleetStatusSnapshotFromDroneStates(droneStates);"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("UpdateDrawBounds();"));
            Assert.That(tryApplyResolvedResupplyCommit, Does.Contain("PublishSnapshot();"));
            AssertSourceOrder(tryApplyResolvedResupplyCommit, "ReleaseDroneMutationGuard(coreMirrorVault, DroneCoreMirrorMutationGuardMask);", "PublishSnapshot();");
            Assert.That(tryConsumeResolvedResupplyCommit, Does.Contain("drone.State != (byte)HeadlessDroneRuntimeState.ResupplyCommitPending"));
            Assert.That(tryConsumeResolvedResupplyCommit, Does.Contain("GrantDroneResupply(ref drone, 1);"));
            Assert.That(tryConsumeResolvedResupplyCommit, Does.Contain("ReturnDroneToHub(ref drone);"));
            Assert.That(tryConsumeResolvedResupplyCommit, Does.Contain("ClearPendingResupplyCommitAck(slot);"));
            Assert.That(clearPendingResupplyCommitAck, Does.Contain("s_PendingResupplyGrantBySlot[slot] = false;"));
            Assert.That(clearPendingResupplyCommitAck, Does.Contain("s_PendingResupplyFailureBySlot[slot] = false;"));
            Assert.That(clearPendingResupplyCommitAck, Does.Contain("s_PendingResupplyReservationIdsBySlot[slot] = 0;"));
            Assert.That(applyPendingControls, Does.Contain("TryConsumeResolvedResupplyCommitAck(slot, true, ref drone, out bool resupplyDroneChanged)"));
            Assert.That(applyPendingControls, Does.Contain("TryConsumeResolvedResupplyCommitAck(slot, false, ref drone, out bool resupplyDroneChanged)"));
            Assert.That(applyPendingControls, Does.Contain("ClearPendingResupplyCommitAck(slot);"));
            Assert.That(applyPendingControls, Does.Contain("MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);"));
            Assert.That(refreshFleetStatusSnapshotFromDroneStates, Does.Contain("s_LastFleetStatusSnapshot = new FleetStatusSnapshot("));
            Assert.That(refreshFleetStatusSnapshotFromDroneStates, Does.Contain("solderReserve += math.max(0, drone.SolderUnits);"));
            Assert.That(refreshFleetStatusSnapshotFromDroneStates, Does.Contain("hostileCount++;"));
            Assert.That(publishStorageReservationAckWarning, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, s_StorageReservationAckContextHash, value);"));
            Assert.That(publishStorageReservationAckWarning, Does.Contain("catch (System.Exception exception) when (!(exception is FatalArchitectureException))"));
            Assert.That(publishStorageReservationAckWarning, Does.Contain("LogStorageReservationAckTelemetryException(exception);"));
            Assert.That(droneFleet, Does.Contain("private static int[] s_PendingResupplyReservationIdsBySlot;"));
            Assert.That(applyHeadlessResupply, Does.Contain("out int queuedReservationId"));
            Assert.That(applyHeadlessResupply, Does.Contain("s_PendingResupplyReservationIdsBySlot[slot] = queuedReservationId;"));
            AssertSourceOrder(applyHeadlessResupply, "s_PendingResupplyReservationIdsBySlot[slot] = queuedReservationId;", "drone.State = (byte)HeadlessDroneRuntimeState.ResupplyCommitPending;");
            Assert.That(applyPendingControls, Does.Contain("s_PendingResupplyReservationIdsBySlot[slot] = 0;"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("StorageReservationCommitResolvedPayload payload = new StorageReservationCommitResolvedPayload"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("try"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("enqueued = EnqueueStorageReservationCommitResolved(in payload);"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("catch (System.Exception exception) when (!(exception is FatalArchitectureException))"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("MarkStorageReservationCommitResolvedQueueUnavailableIfNativeStorageMissing();"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("LogQueueEnqueueException(exception);"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("if (!enqueued)"));
            Assert.That(raiseStorageReservationCommitResolved, Does.Contain("DispatchStorageReservationCommitResolved(in payload);"));
            AssertSourceOrder(raiseStorageReservationCommitResolved, "catch (System.Exception exception) when (!(exception is FatalArchitectureException))", "if (!enqueued)");
            AssertSourceOrder(raiseStorageReservationCommitResolved, "if (!enqueued)", "DispatchStorageReservationCommitResolved(in payload);");
            Assert.That(threadSafeQueue, Does.Contain("public static int StorageReservationCommitOverflowCount => Volatile.Read(ref _storageReservationCommitOverflowCount);"));
            Assert.That(enqueueStorageReservationCommitResolved, Does.Contain("_storageReservationCommitOverflowCount++"));
            Assert.That(enqueueStorageReservationCommitResolved, Does.Contain("ReportStorageReservationCommitOverflowOncePerFrame();"));
            Assert.That(enqueueStorageReservationCommitResolved, Does.Contain("return enqueued;"));
            Assert.That(threadSafeQueue, Does.Contain("public static int StorageReservationCommitListenerExceptionCount => Volatile.Read(ref _storageReservationCommitListenerExceptionCount);"));
            Assert.That(dispatchStorageReservationCommitResolved, Does.Contain("int dispatchIndex = count - 1;"));
            Assert.That(dispatchStorageReservationCommitResolved, Does.Contain("finally"));
            Assert.That(dispatchStorageReservationCommitResolved, Does.Contain("_storageReservationCommitDispatchBuffer[dispatchIndex] = null;"));
            Assert.That(dispatchStorageReservationCommitResolved, Does.Contain("DispatchStorageReservationCommitResolvedToListener(listener, in payload);"));
            Assert.That(dispatchStorageReservationCommitResolvedToListener, Does.Contain("try"));
            Assert.That(dispatchStorageReservationCommitResolvedToListener, Does.Contain("listener.OnStorageReservationCommitResolved(in payload);"));
            Assert.That(dispatchStorageReservationCommitResolvedToListener, Does.Contain("catch (System.Exception exception) when (!(exception is FatalArchitectureException))"));
            Assert.That(dispatchStorageReservationCommitResolvedToListener, Does.Contain("ReportStorageReservationCommitListenerException(exception);"));
            Assert.That(reportStorageReservationCommitListenerException, Does.Contain("_storageReservationCommitListenerExceptionCount++"));
            Assert.That(reportStorageReservationCommitListenerException, Does.Contain("ReportStorageReservationCommitListenerExceptionOncePerFrame();"));
            Assert.That(reportStorageReservationCommitListenerException, Does.Contain("LogStorageReservationCommitListenerException(exception);"));
            Assert.That(reportCommandOverflow, Does.Contain("PublishQueuePerformanceWarningBestEffort("));
            Assert.That(reportCommandOverflow, Does.Not.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(reportStorageReservationCommitOverflow, Does.Contain("PublishQueuePerformanceWarningBestEffort("));
            Assert.That(reportStorageReservationCommitOverflow, Does.Not.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(reportStorageReservationCommitListenerExceptionOncePerFrame, Does.Contain("PublishQueuePerformanceWarningBestEffort("));
            Assert.That(reportStorageReservationCommitListenerExceptionOncePerFrame, Does.Not.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(publishQueueWarning, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);"));
            Assert.That(publishQueueWarning, Does.Contain("catch (System.Exception telemetryException) when (!(telemetryException is FatalArchitectureException))"));
            Assert.That(publishQueueWarning, Does.Contain("LogQueueTelemetryException(telemetryException);"));
            Assert.That(shutdownQueue, Does.Contain("_pendingCommandsSentinelId > 0"));
            Assert.That(shutdownQueue, Does.Contain("_pendingCommands.IsCreated"));
            Assert.That(shutdownQueue, Does.Contain("_pendingStorageReservationCommitResolvedSentinelId > 0"));
            Assert.That(shutdownQueue, Does.Contain("_pendingStorageReservationCommitResolved.IsCreated"));
            Assert.That(disposeTrackedQueue, Does.Contain("queue = default;"));
            Assert.That(disposeTrackedQueue, Does.Contain("Volatile.Write(ref readyFlag, 0);"));
            Assert.That(disposeTrackedQueue, Does.Not.Contain("if (disposed && sentinelId <= 0)"));
            AssertSourceOrder(disposeTrackedQueue, "Volatile.Write(ref readyFlag, 0);", "NativeMemorySentinel.Unregister(sentinelId);");
            Assert.That(tryEnqueue, Does.Contain("catch (System.Exception exception) when (!(exception is FatalArchitectureException))"));
            Assert.That(tryEnqueue, Does.Contain("TryUnregisterOneShotTarget(command.TargetToken);"));
            Assert.That(tryEnqueue, Does.Contain("MarkCommandQueueUnavailableIfNativeStorageMissing();"));
            Assert.That(tryEnqueue, Does.Contain("LogQueueEnqueueException(exception);"));
            Assert.That(markCommandQueueUnavailable, Does.Contain("if (!_pendingCommands.IsCreated)"));
            Assert.That(markCommandQueueUnavailable, Does.Contain("Volatile.Write(ref _pendingCommandsReady, 0);"));
            Assert.That(markCommandQueueUnavailable, Does.Not.Contain("_pendingCommandsSentinelId <= 0"));
            Assert.That(markCommandQueueUnavailable, Does.Contain("_pendingCommandCount = 0;"));
            Assert.That(markStorageReservationAckQueueUnavailable, Does.Contain("if (!_pendingStorageReservationCommitResolved.IsCreated)"));
            Assert.That(markStorageReservationAckQueueUnavailable, Does.Contain("Volatile.Write(ref _pendingStorageReservationCommitResolvedReady, 0);"));
            Assert.That(markStorageReservationAckQueueUnavailable, Does.Not.Contain("_pendingStorageReservationCommitResolvedSentinelId <= 0"));
            Assert.That(markStorageReservationAckQueueUnavailable, Does.Contain("_pendingStorageReservationCommitResolvedCount = 0;"));
            Assert.That(threadSafeQueue, Does.Contain("private static readonly HashSet<int> _oneShotTargetTokens = new HashSet<int>(64);"));
            Assert.That(registerOneShotTarget, Does.Contain("int token = AllocateTokenLocked();"));
            Assert.That(registerOneShotTarget, Does.Contain("_oneShotTargetTokens.Add(token);"));
            Assert.That(registerOneShotTarget, Does.Not.Contain("_tokensByInstanceId[instanceId]"));
            Assert.That(commitViaQueue, Does.Contain("TryCommitReservedViaCommandQueue(reservation, requesterId, out bool committedImmediately) && committedImmediately;"));
            Assert.That(tryCommitViaQueue, Does.Contain("committedImmediately = false;"));
            Assert.That(tryCommitViaQueue, Does.Contain("ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(crate.gameObject);"));
            Assert.AreEqual(3, CountOccurrences(baseLogistics, "bool reservedAnyCost = false;"));
            Assert.AreEqual(3, CountOccurrences(baseLogistics, "if (!reservedAnyCost)"));
            Assert.AreEqual(6, CountOccurrences(baseLogistics, "RollbackReserved(preparedReservation);"));
            Assert.That(executeCommand, Does.Contain("case EntityCommandType.CommitStorageReservation:"));
            Assert.That(executeCommand, Does.Contain("if (!TryResolveTarget(command.TargetToken, out GameObject instance))"));
            Assert.That(executeCommand, Does.Contain("RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, false);"));
            AssertSourceOrder(executeCommand, "if (!TryResolveTarget(command.TargetToken, out GameObject instance))", "RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, false);");
            AssertSourceOrder(executeCommand, "RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, false);", "case EntityCommandType.CommitStorageReservation:");
            Assert.That(executeCommand, Does.Contain("finally"));
            Assert.That(executeCommand, Does.Contain("TryUnregisterOneShotTarget(command.TargetToken);"));
            Assert.That(threadSafeQueue, Does.Contain("if (command.CommandType == EntityCommandType.CommitStorageReservation && command.TargetToken > 0)"));
            Assert.That(threadSafeQueue, Does.Contain("TryUnregisterOneShotTarget(command.TargetToken);"));
            Assert.That(threadSafeQueue, Does.Not.Contain("raiseReservationRejected"));
            Assert.That(threadSafeQueue, Does.Not.Contain("rejectedRequesterId"));
            Assert.That(tryResolveTarget, Does.Contain("UnregisterTokenLocked(token, null);"));
            Assert.That(unregisterTokenLocked, Does.Contain("bool removed = _targetsByToken.Remove(token);"));
            Assert.That(unregisterTokenLocked, Does.Contain("removed |= _oneShotTargetTokens.Remove(token);"));
            Assert.That(unregisterTokenLocked, Does.Contain("removed |= _voxelVolumesByToken.Remove(token);"));
            Assert.That(unregisterTokenLocked, Does.Contain("RemoveInstanceTokenValueLocked(token);"));
            Assert.That(unregisterTokenLocked, Does.Contain("if (removed)"));
            Assert.That(unregisterTokenLocked, Does.Contain("_freeTokens.Add(token);"));
            Assert.That(removeInstanceTokenValueLocked, Does.Contain("return true;"));
            Assert.That(removeInstanceTokenValueLocked, Does.Contain("return false;"));
            AssertSourceOrder(applyPendingLaunches, "PendingDroneLaunch launch = s_PendingLaunches[i];", "s_PendingLaunches[i] = default;");
            AssertSourceOrder(applyPendingLaunches, "s_PendingLaunches[i] = default;", "if (launch.Active == 0)");
            AssertSourceOrder(applyPendingLaunches, "s_PendingLaunches[i] = default;", "ClearHeadlessSlot(slot, true");
            Assert.That(hubBindings, Does.Contain("DroneFleetManager.ConfigureHeadlessRenderSource(dronePrefab);"));
            Assert.That(hubBindings, Does.Contain("DroneFleetManager.ConfigurePhantomSwarm(phantomDroneCompute, phantomDroneMaterial);"));
            Assert.That(hubBindings, Does.Not.Contain("DroneFleetManager.ConfigureRepairTorchAcoustic("));
            Assert.That(configureAcoustic, Does.Contain("if (clip == null)"));
            Assert.That(clearAcoustic, Does.Contain("s_RepairTorchAcousticClip = null;"));
            Assert.That(clearAcoustic, Does.Contain("s_RepairTorchAcousticVolume = DefaultRepairTorchAcousticVolume;"));
            Assert.That(clearAcoustic, Does.Contain("s_RepairTorchAcousticPitch = DefaultRepairTorchAcousticPitch;"));
            Assert.That(hubRefreshAcoustic, Does.Contain("for (int i = s_ActiveHubCount - 1; i >= 0; i--)"));
            Assert.That(hubRefreshAcoustic, Does.Contain("hub.repairTorchAcousticClip == null"));
            Assert.That(hubRefreshAcoustic, Does.Contain("DroneFleetManager.ConfigureRepairTorchAcoustic("));
            Assert.That(hubRefreshAcoustic, Does.Contain("DroneFleetManager.ClearRepairTorchAcousticBinding();"));
            AssertSourceOrder(hubReset, "s_ActiveHubCount = 0;", "DroneFleetManager.ClearRepairTorchAcousticBinding();");
            Assert.That(hubRegister, Does.Contain("RefreshRepairTorchAcousticBindingFromActiveHubs();"));
            Assert.That(hubUnregister, Does.Contain("RefreshRepairTorchAcousticBindingFromActiveHubs();"));
            AssertSourceOrder(hubRegister, "if (ReferenceEquals(s_ActiveHubs[i], this))", "RefreshRepairTorchAcousticBindingFromActiveHubs();");
            AssertSourceOrder(hubRegisterAfterAdd, "s_ActiveHubs[s_ActiveHubCount++] = this;", "RefreshRepairTorchAcousticBindingFromActiveHubs();");
            AssertSourceOrder(hubUnregister, "s_ActiveHubCount = lastIndex;", "RefreshRepairTorchAcousticBindingFromActiveHubs();");
            Assert.That(configureAcoustic, Does.Contain("s_RepairTorchAcousticClip = clip;"));
            Assert.That(configureAcoustic, Does.Contain("Mathf.Clamp01(volume)"));
            Assert.That(configureAcoustic, Does.Contain("Mathf.Clamp(pitch, 0.25f, 2f)"));
            Assert.That(hasRepairSupply, Does.Not.Contain("repairSupplyItem == null"));
            Assert.That(consumeRepairSupply, Does.Not.Contain("repairSupplyItem == null"));
            Assert.That(consumeRepairSupply, Does.Contain("queuedReservationId = reservation.ReservationId;"));
            Assert.That(consumeRepairSupply, Does.Contain("if (!BaseLogisticsNetwork.TryCommitReservedViaCommandQueue(reservation, requesterId, out committedImmediately))"));
            Assert.That(consumeRepairSupply, Does.Contain("if (committedImmediately)"));
            Assert.That(consumeRepairSupply, Does.Contain("queuedReservationId = 0;"));
            Assert.That(acquireDroneResupply, Does.Contain("TryConsumeRepairSupplyInternal(safeRequestedUnits, commitViaCommandQueue: false)"));
            Assert.That(acquireDroneResupply, Does.Contain("grantedUnits = safeRequestedUnits;"));
            Assert.That(queueDroneResupply, Does.Contain("queuedReservationId = 0;"));
            Assert.That(queueDroneResupply, Does.Contain("int safeRequestedUnits = 1;"));
            Assert.That(queueDroneResupply, Does.Not.Contain("Mathf.Max(1, requestedUnits)"));
            Assert.That(queueDroneResupply, Does.Contain("TryConsumeRepairSupplyInternal("));
            Assert.That(queueDroneResupply, Does.Contain("out queuedReservationId"));
            AssertSourceOrder(queueDroneResupply, "int safeRequestedUnits = 1;", "commitViaCommandQueue: true");
            Assert.That(applyHeadlessResupply, Does.Contain("hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId)"));
            AssertSourceOrder(applyHeadlessResupply, "hub.TryQueueDroneResupplyCommit(1, drone.DroneId, out bool committedImmediately, out int queuedReservationId)", "GrantDroneResupply(ref drone, 1);");
            Assert.That(tryCommitViaQueue, Does.Contain("bool sawTouchedCrate = false;"));
            Assert.That(tryCommitViaQueue, Does.Contain("bool queuedAnyCommit = false;"));
            Assert.That(tryCommitViaQueue, Does.Contain("crate.CommitReservation(reservationId);"));
            Assert.That(tryCommitViaQueue, Does.Contain("queuedAnyCommit = true;"));
            Assert.That(tryCommitViaQueue, Does.Contain("committedImmediately = sawTouchedCrate && !queuedAnyCommit;"));
            Assert.That(tryCommitViaQueue, Does.Contain("return sawTouchedCrate;"));
            AssertSourceOrder(tryCommitViaQueue, "crate.CommitReservation(reservationId);", "committedImmediately = sawTouchedCrate && !queuedAnyCommit;");
            Assert.That(resolvePrimarySupplyHash, Does.Contain("ResolveRepairSupplyHashId();"));
            Assert.That(resolvePrimarySupplyHash, Does.Contain("DefaultRepairSupplyHashId"));
            Assert.That(resolveAvailableSupplyHash, Does.Contain("ResolvePrimaryRepairSupplyHashId();"));
            Assert.That(resolveAvailableSupplyHash, Does.Contain("LegacyRepairSupplyHashId"));
            Assert.That(nearestSupplyEndpoint, Does.Contain("int primaryHashId = ResolvePrimaryRepairSupplyHashId();"));
            Assert.That(nearestSupplyEndpoint, Does.Contain("CrateContainsRepairSupply(crate, primaryHashId)"));
            Assert.That(resolveRepairSupplySlot, Does.Contain("int primaryHashId = ResolvePrimaryRepairSupplyHashId();"));
            Assert.That(resolveRepairSupplySlot, Does.Contain("if (!consume)"));
            Assert.That(resolveRepairSupplySlot, Does.Contain("return CountRepairSupplyUnits(crates, count) >= requiredUnits;"));
            Assert.That(resolveRepairSupplySlot, Does.Contain("CountRepairSupplyUnits(crates, count) < requiredUnits"));
            Assert.That(resolveRepairSupplySlot, Does.Contain("TryConsumeRepairSupplyUnit(crate, primaryHashId)"));
            AssertSourceOrder(resolveRepairSupplySlot, "if (!consume)", "TryConsumeRepairSupplyUnit(crate, primaryHashId)");
            Assert.That(resolveRepairSupplySlot, Does.Not.Contain("ContainedItems"));
            Assert.That(resolveRepairSupplySlot, Does.Not.Contain("entries[entryIndex] = null"));
            Assert.That(countRepairSupply, Does.Contain("int primaryHashId = ResolvePrimaryRepairSupplyHashId();"));
            Assert.That(countRepairSupply, Does.Contain("availableUnits += CountRepairSupplyUnits(crate, primaryHashId);"));
            Assert.That(crateContainsRepairSupply, Does.Contain("return CountRepairSupplyUnits(crate, primaryHashId) > 0;"));
            Assert.That(countCrateRepairSupply, Does.Contain("crate.CountItemByHash(primaryHashId)"));
            Assert.That(countCrateRepairSupply, Does.Contain("LegacyRepairSupplyHashId"));
            Assert.That(consumeCrateRepairSupply, Does.Contain("crate.TryConsumeItemByHash(primaryHashId)"));
            Assert.That(consumeCrateRepairSupply, Does.Contain("LegacyRepairSupplyHashId"));
            Assert.That(consumeCrateRepairSupply, Does.Contain("crate.TryConsumeItemByHash(legacyHashId)"));
            Assert.That(repairHub, Does.Not.Contain("ReferenceEquals(entries[entryIndex], repairSupplyItem)"));
            Assert.That(publishSparks, Does.Contain("PublishDroneRepairTorchAcoustic(ToVector3(hitRuntime), safeIntensity);"));
            Assert.That(publishAcoustic, Does.Contain("AudioClip clip = s_RepairTorchAcousticClip;"));
            Assert.That(publishAcoustic, Does.Contain("new RepairDroneTorchAcousticEvent("));
            Assert.That(publishAcoustic, Does.Contain("RepairDroneTorchAcousticEvents.TryNotify(in acousticEvent)"));
            Assert.That(publishAcoustic, Does.Contain("s_SignalPushDropCount < int.MaxValue"));
            Assert.That(publishAcoustic, Does.Contain("s_SignalPushDropCount++"));
            AssertSourceOrder(publishAcoustic, "new RepairDroneTorchAcousticEvent(", "RepairDroneTorchAcousticEvents.TryNotify(in acousticEvent)");
            AssertSourceOrder(publishAcoustic, "RepairDroneTorchAcousticEvents.TryNotify(in acousticEvent)", "s_SignalPushDropCount++");
            AssertSourceOrder(ExtractMethodBody(repairHub, "private void OnEnable()"), "ConfigureDroneFleetRuntimeBindings();", "RegisterHubInstance();");
            AssertSourceOrder(ExtractMethodBody(repairHub, "private void OnEnable()"), "RegisterHubInstance();", "TryRegister();");
        }

        [Test]
        public void ThreadSafeCommandQueue_DispatchesStorageReservationAckWhenAckQueueIsFull()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                for (int i = 0; i < 64; i++)
                    InvokePrivateStaticMethod(
                        typeof(ThreadSafeCommandQueue),
                        "RaiseStorageReservationCommitResolved",
                        i + 1,
                        1000 + i,
                        true);

                Assert.AreEqual(0, listener.Count);
                Assert.AreEqual(64, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
                int overflowCountBefore = ThreadSafeCommandQueue.StorageReservationCommitOverflowCount;

                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    9001,
                    77,
                    false);

                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(9001, listener.LastRequesterId);
                Assert.AreEqual(77, listener.LastReservationId);
                Assert.IsFalse(listener.LastCommitted);
                Assert.AreEqual(64, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
                Assert.AreEqual(overflowCountBefore + 1, ThreadSafeCommandQueue.StorageReservationCommitOverflowCount);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_DispatchesStorageReservationAckWhenAckQueueEnqueueThrows()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedReady", 1);
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedSentinelId", 12345);
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex(".*"));

                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    12,
                    true);

                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(42, listener.LastRequesterId);
                Assert.AreEqual(12, listener.LastReservationId);
                Assert.IsTrue(listener.LastCommitted);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
                Assert.AreEqual(0, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedReady"));
            }
            finally
            {
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedSentinelId", 0);
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_IsolatesStorageReservationAckListenerExceptions()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener stableListener = new CapturingStorageReservationCommitListener();
            ThrowingStorageReservationCommitListener throwingListener = new ThrowingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(stableListener);
            ThreadSafeCommandQueue.Register(throwingListener);
            try
            {
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    99,
                    true);

                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex("Storage reservation listener test exception"));
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(1, throwingListener.Count);
                Assert.AreEqual(1, stableListener.Count);
                Assert.AreEqual(42, stableListener.LastRequesterId);
                Assert.AreEqual(99, stableListener.LastReservationId);
                Assert.IsTrue(stableListener.LastCommitted);
                Assert.GreaterOrEqual(ThreadSafeCommandQueue.StorageReservationCommitListenerExceptionCount, 1);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(throwingListener);
                ThreadSafeCommandQueue.Unregister(stableListener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_PropagatesFatalStorageReservationAckListenerExceptions()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener stableListener = new CapturingStorageReservationCommitListener();
            FatalStorageReservationCommitListener fatalListener = new FatalStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(stableListener);
            ThreadSafeCommandQueue.Register(fatalListener);
            try
            {
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    99,
                    true);

                Assert.Throws<FatalArchitectureException>(() => ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());
                Assert.AreEqual(1, fatalListener.Count);
                Assert.AreEqual(0, stableListener.Count);
                Assert.AreEqual(0, ThreadSafeCommandQueue.StorageReservationCommitListenerExceptionCount);

                object[] dispatchBuffer =
                    GetPrivateStaticField<object[]>(
                        typeof(ThreadSafeCommandQueue),
                        "_storageReservationCommitDispatchBuffer");
                for (int i = 0; i < dispatchBuffer.Length; i++)
                    Assert.IsNull(dispatchBuffer[i], i.ToString());
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(fatalListener);
                ThreadSafeCommandQueue.Unregister(stableListener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_StorageReservationAckListenersTolerateReentrantRegistrationChanges()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener stableListener = new CapturingStorageReservationCommitListener();
            CapturingStorageReservationCommitListener selfRemovingListener = new CapturingStorageReservationCommitListener();
            selfRemovingListener.OnEvent = () => ThreadSafeCommandQueue.Unregister(selfRemovingListener);
            ThreadSafeCommandQueue.Register(stableListener);
            ThreadSafeCommandQueue.Register(selfRemovingListener);
            try
            {
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    99,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(1, stableListener.Count);
                Assert.AreEqual(1, selfRemovingListener.Count);

                stableListener.OnEvent = () => ThreadSafeCommandQueue.Register(stableListener);
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    43,
                    100,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(2, stableListener.Count);
                Assert.AreEqual(1, selfRemovingListener.Count);

                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    44,
                    101,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(3, stableListener.Count);
                Assert.AreEqual(1, selfRemovingListener.Count);
            }
            finally
            {
                stableListener.OnEvent = null;
                selfRemovingListener.OnEvent = null;
                ThreadSafeCommandQueue.Unregister(selfRemovingListener);
                ThreadSafeCommandQueue.Unregister(stableListener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_StorageReservationListenerGenerationTracksRegistryLifecycle()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            try
            {
                int startGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
                Assert.IsTrue(ThreadSafeCommandQueue.Register(listener));
                int registeredGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
                Assert.AreNotEqual(startGeneration, registeredGeneration);

                Assert.IsTrue(ThreadSafeCommandQueue.Register(listener));
                Assert.AreEqual(registeredGeneration, ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration);

                ThreadSafeCommandQueue.Unregister(listener);
                int unregisteredGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
                Assert.AreNotEqual(registeredGeneration, unregisteredGeneration);

                Assert.IsTrue(ThreadSafeCommandQueue.Register(listener));
                int reregisteredGeneration = ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration;
                Assert.AreNotEqual(unregisteredGeneration, reregisteredGeneration);

                ThreadSafeCommandQueue.Shutdown();
                Assert.AreNotEqual(reregisteredGeneration, ThreadSafeCommandQueue.StorageReservationCommitListenerGeneration);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_ShutdownClearsNativeQueueReadyFlagsBeforeReinitialize()
        {
            ThreadSafeCommandQueue.Shutdown();
            try
            {
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateOpenPDATab(1)));
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    91,
                    92,
                    true);

                Assert.AreEqual(1, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingCommandsReady"));
                Assert.AreEqual(1, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedReady"));

                ThreadSafeCommandQueue.Shutdown();

                Assert.AreEqual(0, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingCommandsReady"));
                Assert.AreEqual(0, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedReady"));
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingCount);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);

                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateClosePDA()));
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    93,
                    94,
                    false);

                Assert.AreEqual(1, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingCommandsReady"));
                Assert.AreEqual(1, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingStorageReservationCommitResolvedReady"));
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingCount);
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
            }
            finally
            {
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_DispatchesPendingStorageReservationAckBeforeShutdownDisposesQueue()
        {
            ThreadSafeCommandQueue.Shutdown();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    7101,
                    8101,
                    true);

                Assert.AreEqual(0, listener.Count);
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);

                ThreadSafeCommandQueue.Shutdown();

                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(7101, listener.LastRequesterId);
                Assert.AreEqual(8101, listener.LastReservationId);
                Assert.IsTrue(listener.LastCommitted);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
            }
            finally
            {
                ThreadSafeCommandQueue.Shutdown();
            }
        }

        [Test]
        public void DroneFleetStorageReservationAck_RebindsBridgeAfterThreadSafeQueueShutdown()
        {
            Type droneFleetType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.DroneFleetManager");
            Assert.IsNotNull(droneFleetType);

            int[] previousDroneIds = GetPrivateStaticField<int[]>(droneFleetType, "s_DroneSlotDroneIds");
            bool[] previousGrantFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyGrantBySlot");
            bool[] previousFailureFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyFailureBySlot");
            int[] previousExpectedReservationIds = GetPrivateStaticField<int[]>(droneFleetType, "s_PendingResupplyReservationIdsBySlot");
            int previousListenerGeneration = GetPrivateStaticField<int>(droneFleetType, "s_StorageReservationCommitResolvedListenerGeneration");
            ThreadSafeCommandQueue.Shutdown();
            try
            {
                int[] droneIds = { 42 };
                bool[] grantFlags = new bool[1];
                bool[] failureFlags = new bool[1];
                int[] expectedReservationIds = { 12 };
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", droneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", grantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", failureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", expectedReservationIds);
                SetPrivateStaticField(droneFleetType, "s_StorageReservationCommitResolvedListenerGeneration", -1);

                InvokePrivateStaticMethod(droneFleetType, "EnsureStorageReservationCommitResolvedBridge");
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    12,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());
                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);

                grantFlags[0] = false;
                failureFlags[0] = false;
                expectedReservationIds[0] = 13;
                ThreadSafeCommandQueue.Shutdown();

                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    13,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());
                Assert.IsFalse(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);

                InvokePrivateStaticMethod(droneFleetType, "EnsureStorageReservationCommitResolvedBridge");
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    42,
                    13,
                    true);
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());
                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
            }
            finally
            {
                ThreadSafeCommandQueue.Shutdown();
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", previousDroneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", previousGrantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", previousFailureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", previousExpectedReservationIds);
                SetPrivateStaticField(droneFleetType, "s_StorageReservationCommitResolvedListenerGeneration", previousListenerGeneration);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_ReleasesStorageReservationWhenClearAbandonsPendingCommit()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation clear-abandon target probe");
            StorageReservationCommitTargetProbe target = host.AddComponent<StorageReservationCommitTargetProbe>();
            try
            {
                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 3101, 4101)));
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingCount);

                ThreadSafeCommandQueue.Clear();

                Assert.AreEqual(0, target.CommitCount);
                Assert.AreEqual(1, target.ReleaseCount);
                Assert.AreEqual(3101, target.LastReleasedReservationId);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingCount);

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                Assert.IsFalse(targetsByToken.ContainsKey(token));
            }
            finally
            {
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_ReleasesStorageReservationAndReportsFailureWhenShutdownAbandonsPendingCommit()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation shutdown-abandon target probe");
            StorageReservationCommitTargetProbe target = host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 3102, 4102)));
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingCount);

                ThreadSafeCommandQueue.Shutdown();

                Assert.AreEqual(0, target.CommitCount);
                Assert.AreEqual(1, target.ReleaseCount);
                Assert.AreEqual(3102, target.LastReleasedReservationId);
                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(4102, listener.LastRequesterId);
                Assert.AreEqual(3102, listener.LastReservationId);
                Assert.IsFalse(listener.LastCommitted);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingCount);
            }
            finally
            {
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_StorageReservationCommandsUseOneShotTargets()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation one-shot target probe");
            StorageReservationCommitTargetProbe target = host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                int persistentToken = ThreadSafeCommandQueue.RegisterGameObjectTarget(host);
                int firstToken = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                int secondToken = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(persistentToken, 0);
                Assert.Greater(firstToken, 0);
                Assert.Greater(secondToken, 0);
                Assert.AreNotEqual(persistentToken, firstToken);
                Assert.AreNotEqual(persistentToken, secondToken);
                Assert.AreNotEqual(firstToken, secondToken);
                Assert.IsTrue(ThreadSafeCommandQueue.TryGetGameObjectTargetToken(host, out int resolvedPersistentToken));
                Assert.AreEqual(persistentToken, resolvedPersistentToken);

                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(firstToken, 101, 501)));
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(secondToken, 102, 502)));
                Assert.IsTrue(ThreadSafeCommandQueue.DrainMainThread());
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(2, target.CommitCount);
                Assert.AreEqual(102, target.LastReservationId);
                Assert.AreEqual(2, listener.Count);
                Assert.AreEqual(501, listener.FirstRequesterId);
                Assert.AreEqual(101, listener.FirstReservationId);
                Assert.AreEqual(502, listener.LastRequesterId);
                Assert.AreEqual(102, listener.LastReservationId);
                Assert.IsTrue(listener.FirstCommitted);
                Assert.IsTrue(listener.LastCommitted);

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                Assert.IsFalse(targetsByToken.ContainsKey(firstToken));
                Assert.IsFalse(targetsByToken.ContainsKey(secondToken));
                Assert.IsTrue(ThreadSafeCommandQueue.TryGetGameObjectTargetToken(host, out resolvedPersistentToken));
                Assert.AreEqual(persistentToken, resolvedPersistentToken);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_StorageReservationCommitReportsFailureWhenOneShotTargetIsStale()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation stale one-shot target probe");
            host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            int token = 0;
            try
            {
                token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 606, 806)));

                Object.DestroyImmediate(host);
                host = null;

                Assert.IsTrue(ThreadSafeCommandQueue.DrainMainThread());
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());

                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(806, listener.LastRequesterId);
                Assert.AreEqual(606, listener.LastReservationId);
                Assert.IsFalse(listener.LastCommitted);

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                System.Collections.Generic.HashSet<int> oneShotTargetTokens =
                    GetPrivateStaticField<System.Collections.Generic.HashSet<int>>(
                        typeof(ThreadSafeCommandQueue),
                        "_oneShotTargetTokens");
                Assert.IsFalse(targetsByToken.ContainsKey(token));
                Assert.IsFalse(oneShotTargetTokens.Contains(token));
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
                if (host != null)
                    Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_ReleasesOneShotStorageTargetWhenCommitEnqueueIsRejected()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation rejected one-shot target probe");
            host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                int persistentToken = ThreadSafeCommandQueue.RegisterGameObjectTarget(host);
                Assert.Greater(persistentToken, 0);
                for (int i = 0; i < 256; i++)
                    Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateOpenPDATab(1)), i.ToString());

                System.Collections.Generic.List<int> freeTokens =
                    GetPrivateStaticField<System.Collections.Generic.List<int>>(
                        typeof(ThreadSafeCommandQueue),
                        "_freeTokens");
                int freeTokenCountBeforeBadCommand = freeTokens.Count;
                Assert.IsFalse(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(123456, 404, 0)));
                Assert.AreEqual(freeTokenCountBeforeBadCommand, freeTokens.Count);

                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.AreNotEqual(persistentToken, token);
                Assert.IsFalse(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 303, 777)));
                Assert.IsTrue(ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents());
                Assert.AreEqual(0, listener.Count);

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                Assert.IsFalse(targetsByToken.ContainsKey(token));
                Assert.IsTrue(ThreadSafeCommandQueue.TryGetGameObjectTargetToken(host, out int resolvedPersistentToken));
                Assert.AreEqual(persistentToken, resolvedPersistentToken);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_ReleasesOneShotStorageTargetWhenCommitEnqueueThrows()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation thrown one-shot target probe");
            host.AddComponent<StorageReservationCommitTargetProbe>();
            try
            {
                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingCommandsReady", 1);
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingCommandsSentinelId", 12345);

                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex(".*"));
                Assert.IsFalse(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 909, 1009)));

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                System.Collections.Generic.HashSet<int> oneShotTargetTokens =
                    GetPrivateStaticField<System.Collections.Generic.HashSet<int>>(
                        typeof(ThreadSafeCommandQueue),
                        "_oneShotTargetTokens");
                Assert.IsFalse(targetsByToken.ContainsKey(token));
                Assert.IsFalse(oneShotTargetTokens.Contains(token));
                Assert.AreEqual(0, GetPrivateStaticField<int>(typeof(ThreadSafeCommandQueue), "_pendingCommandsReady"));
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingCount);
            }
            finally
            {
                SetPrivateStaticField(typeof(ThreadSafeCommandQueue), "_pendingCommandsSentinelId", 0);
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_PreparesPersistenceSnapshotByReleasingPendingStorageCommit()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation persistence snapshot target probe");
            StorageReservationCommitTargetProbe target = host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateOpenPDATab(1)));
                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 3201, 4201)));
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateClosePDA()));
                Assert.AreEqual(3, ThreadSafeCommandQueue.PendingCount);

                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();

                Assert.AreEqual(0, target.CommitCount);
                Assert.AreEqual(1, target.ReleaseCount);
                Assert.AreEqual(3201, target.LastReleasedReservationId);
                Assert.AreEqual(1, listener.Count);
                Assert.AreEqual(4201, listener.LastRequesterId);
                Assert.AreEqual(3201, listener.LastReservationId);
                Assert.IsFalse(listener.LastCommitted);
                Assert.AreEqual(2, ThreadSafeCommandQueue.PendingCount);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);

                System.Collections.Generic.Dictionary<int, GameObject> targetsByToken =
                    GetPrivateStaticField<System.Collections.Generic.Dictionary<int, GameObject>>(
                        typeof(ThreadSafeCommandQueue),
                        "_targetsByToken");
                Assert.IsFalse(targetsByToken.ContainsKey(token));
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ThreadSafeCommandQueue_PreparesPersistenceSnapshotByDispatchingAckBeforeAbandon()
        {
            ThreadSafeCommandQueue.Shutdown();
            GameObject host = new GameObject("Storage reservation persistence snapshot ack order probe");
            StorageReservationCommitTargetProbe target = host.AddComponent<StorageReservationCommitTargetProbe>();
            CapturingStorageReservationCommitListener listener = new CapturingStorageReservationCommitListener();
            ThreadSafeCommandQueue.Register(listener);
            try
            {
                InvokePrivateStaticMethod(
                    typeof(ThreadSafeCommandQueue),
                    "RaiseStorageReservationCommitResolved",
                    5101,
                    6101,
                    true);
                int token = ThreadSafeCommandQueue.RegisterOneShotGameObjectTarget(host);
                Assert.Greater(token, 0);
                Assert.IsTrue(ThreadSafeCommandQueue.TryEnqueue(EntityCommand.CreateCommitStorageReservation(token, 6102, 5102)));
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
                Assert.AreEqual(1, ThreadSafeCommandQueue.PendingCount);

                ThreadSafeCommandQueue.PrepareStorageReservationCommitBridgeForPersistenceSnapshot();

                Assert.AreEqual(2, listener.Count);
                Assert.AreEqual(5101, listener.FirstRequesterId);
                Assert.AreEqual(6101, listener.FirstReservationId);
                Assert.IsTrue(listener.FirstCommitted);
                Assert.AreEqual(5102, listener.LastRequesterId);
                Assert.AreEqual(6102, listener.LastReservationId);
                Assert.IsFalse(listener.LastCommitted);
                Assert.AreEqual(0, target.CommitCount);
                Assert.AreEqual(1, target.ReleaseCount);
                Assert.AreEqual(6102, target.LastReleasedReservationId);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingCount);
                Assert.AreEqual(0, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
            }
            finally
            {
                ThreadSafeCommandQueue.Unregister(listener);
                ThreadSafeCommandQueue.Shutdown();
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DroneFleetStorageReservationAck_TreatsExpectedCommitFailureAsFailure()
        {
            Type droneFleetType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.DroneFleetManager");
            Assert.IsNotNull(droneFleetType);

            int[] previousDroneIds = GetPrivateStaticField<int[]>(droneFleetType, "s_DroneSlotDroneIds");
            bool[] previousGrantFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyGrantBySlot");
            bool[] previousFailureFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyFailureBySlot");
            int[] previousExpectedReservationIds = GetPrivateStaticField<int[]>(droneFleetType, "s_PendingResupplyReservationIdsBySlot");
            try
            {
                int[] droneIds = { 42 };
                bool[] grantFlags = new bool[1];
                bool[] failureFlags = new bool[1];
                int[] expectedReservationIds = { 12 };
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", droneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", grantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", failureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", expectedReservationIds);

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, false);

                Assert.IsFalse(grantFlags[0]);
                Assert.IsTrue(failureFlags[0]);

                failureFlags[0] = false;
                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, true);

                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
            }
            finally
            {
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", previousDroneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", previousGrantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", previousFailureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", previousExpectedReservationIds);
            }
        }

        [Test]
        public void DroneFleetStorageReservationAck_IgnoresMismatchedReservationIds()
        {
            Type droneFleetType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.DroneFleetManager");
            Assert.IsNotNull(droneFleetType);

            int[] previousDroneIds = GetPrivateStaticField<int[]>(droneFleetType, "s_DroneSlotDroneIds");
            bool[] previousGrantFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyGrantBySlot");
            bool[] previousFailureFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyFailureBySlot");
            int[] previousExpectedReservationIds = GetPrivateStaticField<int[]>(droneFleetType, "s_PendingResupplyReservationIdsBySlot");
            try
            {
                int[] droneIds = { 42 };
                bool[] grantFlags = new bool[1];
                bool[] failureFlags = new bool[1];
                int[] expectedReservationIds = { 12 };
                int mismatchCountBefore = GetPrivateStaticField<int>(droneFleetType, "s_StorageReservationMismatchAckCount");
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", droneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", grantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", failureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", expectedReservationIds);

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 99, true);

                Assert.IsFalse(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 13, false);

                Assert.IsFalse(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
                Assert.AreEqual(mismatchCountBefore + 2, GetPrivateStaticField<int>(droneFleetType, "s_StorageReservationMismatchAckCount"));

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, true);

                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
            }
            finally
            {
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", previousDroneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", previousGrantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", previousFailureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", previousExpectedReservationIds);
            }
        }

        [Test]
        public void DroneFleetStorageReservationAck_ReportsStaleAckWhenNoReservationIsExpected()
        {
            Type droneFleetType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.DroneFleetManager");
            Assert.IsNotNull(droneFleetType);

            int[] previousDroneIds = GetPrivateStaticField<int[]>(droneFleetType, "s_DroneSlotDroneIds");
            bool[] previousGrantFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyGrantBySlot");
            bool[] previousFailureFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyFailureBySlot");
            int[] previousExpectedReservationIds = GetPrivateStaticField<int[]>(droneFleetType, "s_PendingResupplyReservationIdsBySlot");
            try
            {
                int[] droneIds = { 42 };
                bool[] grantFlags = new bool[1];
                bool[] failureFlags = new bool[1];
                int[] expectedReservationIds = new int[1];
                int staleCountBefore = GetPrivateStaticField<int>(droneFleetType, "s_StorageReservationStaleAckCount");
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", droneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", grantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", failureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", expectedReservationIds);

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, true);

                Assert.IsFalse(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
                Assert.AreEqual(staleCountBefore + 1, GetPrivateStaticField<int>(droneFleetType, "s_StorageReservationStaleAckCount"));
            }
            finally
            {
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", previousDroneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", previousGrantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", previousFailureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", previousExpectedReservationIds);
            }
        }

        [Test]
        public void BaseLogisticsQueuedCommitReportsNotStartedWhenReservationTouchesNoCrates()
        {
            Type logisticsType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.BaseLogisticsNetwork");
            Assert.IsNotNull(logisticsType);
            Type reservationType = logisticsType.GetNestedType("LogisticsReservation", BindingFlags.NonPublic);
            Assert.IsNotNull(reservationType);

            object reservation = Activator.CreateInstance(reservationType, true);
            Assert.IsNotNull(reservation);

            MethodInfo initialize = reservationType.GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(initialize);
            initialize.Invoke(reservation, new object[] { 77, null });

            MethodInfo tryCommit = logisticsType.GetMethod("TryCommitReservedViaCommandQueue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(tryCommit);
            object[] args = { reservation, 42, false };

            bool started = (bool)tryCommit.Invoke(null, args);

            Assert.IsFalse(started);
            Assert.IsFalse((bool)args[2]);
            PropertyInfo isPrepared = reservationType.GetProperty("IsPrepared", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(isPrepared);
            Assert.IsFalse((bool)isPrepared.GetValue(reservation));
        }

        [Test]
        public void BaseLogisticsRejectsEmptyReservationCostBuffersWithoutLeakingReservation()
        {
            Type logisticsType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.BaseLogisticsNetwork");
            Assert.IsNotNull(logisticsType);
            Type reservationType = logisticsType.GetNestedType("LogisticsReservation", BindingFlags.NonPublic);
            Assert.IsNotNull(reservationType);
            InvokePrivateStaticMethod(logisticsType, "ResetStaticState");

            int poolCountBefore = GetPrivateStaticField<int>(logisticsType, "s_ReservationPoolCount");
            Type reservationOutType = reservationType.MakeByRefType();
            MethodInfo tryReserveArray = logisticsType.GetMethod(
                "TryReserveResources",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(PowerGrid),
                    typeof(int[]),
                    typeof(int[]),
                    typeof(int),
                    reservationOutType
                },
                null);
            Assert.IsNotNull(tryReserveArray);

            object[] arrayArgs =
            {
                new PowerGrid(),
                new[] { 0, 0 },
                new[] { 0, -1 },
                2,
                null
            };

            bool reservedArray = (bool)tryReserveArray.Invoke(null, arrayArgs);

            Assert.IsFalse(reservedArray);
            Assert.IsNull(arrayArgs[4]);
            Assert.AreEqual(poolCountBefore, GetPrivateStaticField<int>(logisticsType, "s_ReservationPoolCount"));

            MethodInfo tryReserveDictionary = logisticsType.GetMethod(
                "TryReserveResources",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(PowerGrid),
                    typeof(System.Collections.Generic.Dictionary<string, int>),
                    typeof(ItemCatalog),
                    reservationOutType
                },
                null);
            Assert.IsNotNull(tryReserveDictionary);
            object[] dictionaryArgs =
            {
                new PowerGrid(),
                new System.Collections.Generic.Dictionary<string, int> { { " ", 0 } },
                null,
                null
            };

            bool reservedDictionary = (bool)tryReserveDictionary.Invoke(null, dictionaryArgs);

            Assert.IsFalse(reservedDictionary);
            Assert.IsNull(dictionaryArgs[3]);
            Assert.AreEqual(poolCountBefore, GetPrivateStaticField<int>(logisticsType, "s_ReservationPoolCount"));

            Type inventoryCostType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Building.InventoryCost");
            Assert.IsNotNull(inventoryCostType);
            Type inventoryCostListType = typeof(System.Collections.Generic.List<>).MakeGenericType(inventoryCostType);
            object inventoryCostList = Activator.CreateInstance(inventoryCostListType);
            Assert.IsNotNull(inventoryCostList);
            MethodInfo addInventoryCost = inventoryCostListType.GetMethod("Add");
            Assert.IsNotNull(addInventoryCost);
            addInventoryCost.Invoke(inventoryCostList, new object[] { null });
            MethodInfo tryReserveList = logisticsType.GetMethod(
                "TryReserveResources",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(PowerGrid),
                    inventoryCostListType,
                    reservationOutType
                },
                null);
            Assert.IsNotNull(tryReserveList);
            object[] listArgs =
            {
                new PowerGrid(),
                inventoryCostList,
                null
            };

            bool reservedList = (bool)tryReserveList.Invoke(null, listArgs);

            Assert.IsFalse(reservedList);
            Assert.IsNull(listArgs[2]);
            Assert.AreEqual(poolCountBefore, GetPrivateStaticField<int>(logisticsType, "s_ReservationPoolCount"));
        }

        [Test]
        public void DroneFleetStorageReservationAck_LatchesGrantAgainstLaterFailure()
        {
            Type droneFleetType = typeof(ThreadSafeCommandQueue).Assembly.GetType("Hecton8.Construction.DroneFleetManager");
            Assert.IsNotNull(droneFleetType);

            int[] previousDroneIds = GetPrivateStaticField<int[]>(droneFleetType, "s_DroneSlotDroneIds");
            bool[] previousGrantFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyGrantBySlot");
            bool[] previousFailureFlags = GetPrivateStaticField<bool[]>(droneFleetType, "s_PendingResupplyFailureBySlot");
            int[] previousExpectedReservationIds = GetPrivateStaticField<int[]>(droneFleetType, "s_PendingResupplyReservationIdsBySlot");
            try
            {
                int[] droneIds = { 42 };
                bool[] grantFlags = new bool[1];
                bool[] failureFlags = new bool[1];
                int[] expectedReservationIds = { 12 };
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", droneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", grantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", failureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", expectedReservationIds);

                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, true);
                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 0, false);

                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);

                grantFlags[0] = false;
                failureFlags[0] = false;
                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 0, false);
                InvokePrivateStaticMethod(droneFleetType, "HandleStorageReservationCommitResolved", 42, 12, true);

                Assert.IsTrue(grantFlags[0]);
                Assert.IsFalse(failureFlags[0]);
            }
            finally
            {
                SetPrivateStaticField(droneFleetType, "s_DroneSlotDroneIds", previousDroneIds);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyGrantBySlot", previousGrantFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyFailureBySlot", previousFailureFlags);
                SetPrivateStaticField(droneFleetType, "s_PendingResupplyReservationIdsBySlot", previousExpectedReservationIds);
            }
        }

        [Test]
        public void SubmarineOs_BindsAtlas6ActiveRuntimeChangesWithoutUiDuplicateOwner()
        {
            string manager = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "Atlas6Liability", "Atlas6CorporateLiabilityManager.cs");
            string tryRegisterActive = ExtractMethodBody(manager, "private bool TryRegisterActiveRuntimeInstance()");
            string tryUnregisterActive = ExtractMethodBody(manager, "private void TryUnregisterActiveRuntimeInstance()");
            string abortDuplicateOwner = ExtractMethodBody(manager, "private void AbortDuplicateRuntimeOwner()");
            string publishActiveRuntimeChanged = ExtractMethodBody(manager, "private static void PublishActiveRuntimeInstanceChanged(");
            string uiDuplicate = Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "UI",
                "TerminalOS",
                "HectonSubmarineOS.cs");

            Assert.IsFalse(File.Exists(uiDuplicate), "Submarine OS readout must stay owned by Gameplay/HectonSubmarineOS.cs, not a second UI runtime.");
            Assert.IsFalse(File.Exists(uiDuplicate + ".meta"), "Removed duplicate Submarine OS scripts must not leave orphan Unity metadata.");
            Assert.That(manager, Does.Contain("internal static event Action<Atlas6CorporateLiabilityManager> ActiveRuntimeInstanceChanged;"));
            Assert.That(manager, Does.Contain("ActiveRuntimeInstanceChanged = null;"));
            Assert.That(tryRegisterActive, Does.Contain("if (ReferenceEquals(activeRuntime, this))"));
            Assert.That(tryRegisterActive, Does.Contain("AbortDuplicateRuntimeOwner();"));
            Assert.That(tryRegisterActive, Does.Contain("PublishActiveRuntimeInstanceChanged(null);"));
            Assert.That(tryRegisterActive, Does.Contain("PublishActiveRuntimeInstanceChanged(this);"));
            Assert.That(tryUnregisterActive, Does.Contain("PublishActiveRuntimeInstanceChanged(null);"));
            Assert.That(manager, Does.Not.Contain("ActiveRuntimeInstanceChanged?.Invoke("));
            Assert.That(publishActiveRuntimeChanged, Does.Contain("listeners.GetInvocationList()"));
            Assert.That(publishActiveRuntimeChanged, Does.Contain("try"));
            Assert.That(publishActiveRuntimeChanged, Does.Contain("catch (Exception exception)"));
            Assert.That(publishActiveRuntimeChanged, Does.Contain("LogActiveRuntimeListenerException(exception);"));
            Assert.That(abortDuplicateOwner, Does.Contain("UnregisterFromGlobalRegistry();"));
            Assert.That(abortDuplicateOwner, Does.Contain("TryUnregisterNarrativeEvents();"));
            Assert.That(abortDuplicateOwner, Does.Contain("TryUnregisterAudioLogEvents();"));
            Assert.That(abortDuplicateOwner, Does.Contain("TryUnregisterHotSwapListener();"));
            Assert.That(abortDuplicateOwner, Does.Contain("TryUnregisterSaveParticipant();"));
            Assert.That(abortDuplicateOwner, Does.Contain("UnwireSubsystemEvents();"));
            Assert.That(abortDuplicateOwner, Does.Contain("ClearCachedRuntimeServices();"));
            Assert.That(abortDuplicateOwner, Does.Contain("_runtimeOwnerAborted = true;"));
            AssertSourceOrder(abortDuplicateOwner, "TryUnregisterSaveParticipant();", "_runtimeOwnerAborted = true;");
            AssertSourceOrder(abortDuplicateOwner, "ClearCachedRuntimeServices();", "_runtimeOwnerAborted = true;");
            Assert.That(abortDuplicateOwner, Does.Not.Contain("_saveRegistered = false;"));
        }

        [Test]
        public void SubmarineOs_VerifiesHectonSubmarineOsLogic()
        {
            string subOs = ReadProjectFile("Assets", "_Project", "Scripts", "Gameplay", "HectonSubmarineOS.cs");
            string subOsSubscribe = ExtractMethodBody(subOs, "private void Subscribe()");
            string registerListener = ExtractMethodBody(subOs, "private void TryRegisterAtlas6ActiveRuntimeListener()");
            string unregisterListener = ExtractMethodBody(subOs, "private void TryUnregisterAtlas6ActiveRuntimeListener()");
            string refreshAtlas = ExtractMethodBody(subOs, "private void RefreshAtlas6ManagerReference(");
            string handleAtlasChanged = ExtractMethodBody(subOs, "private void HandleAtlas6ActiveRuntimeInstanceChanged(");
            string serviceReplaced = ExtractMethodBody(subOs, "public void OnGlobalRegistryServiceReplaced(");
            string publishSnapshotIfReady = ExtractMethodBody(subOs, "private void PublishCurrentSnapshotIfRuntimeReady()");
            string refreshPlayerDrivenState = ExtractMethodBody(subOs, "private void RefreshPlayerDrivenStateAfterServiceReplacement()");
            string refreshTelemetry = ExtractMethodBody(subOs, "private void RefreshTelemetryFromServices()");
            string resetPowerFallback = ExtractMethodBody(subOs, "private void ResetPowerTelemetryFallback()");
            string refreshAtmosphere = ExtractMethodBody(subOs, "private void RefreshAtmosphereTelemetry()");
            string resolveVwsFlags = ExtractMethodBody(subOs, "private SubmarineVwsFlags ResolveVwsFlags()");
            string resolveVwsOxygen = ExtractMethodBody(subOs, "private float ResolveVwsOxygenNormalized()");
            string resolveVitalWarning = ExtractMethodBody(subOs, "private bool ResolvePlayerVitalWarningActive()");
            string powerGridTelemetry = ExtractMethodBody(subOs, "public void OnPowerGridTelemetryUpdated(");
            string handleHighPressure = ExtractMethodBody(subOs, "private void HandleHighPressure(");
            string subOsRegister = ExtractMethodBody(subOs, "public static void Register(ISubmarineOsEventListener listener)");
            string subOsUnregister = ExtractMethodBody(subOs, "public static void Unregister(ISubmarineOsEventListener listener)");
            string subOsRegisterImmediate = ExtractMethodBody(subOs, "private static void RegisterImmediate(ISubmarineOsEventListener listener)");
            string subOsEnqueue = ExtractMethodBody(subOs, "private static bool Enqueue(in SubmarineOsEventPayload payload)");
            string subOsTryRaiseSnapshot = ExtractMethodBody(subOs, "public static bool TryRaiseSnapshotUpdated(");
            string subOsTryRaiseLogRequest = ExtractMethodBody(subOs, "public static bool TryRaiseLogRequested(");
            string subOsBuildSnapshot = ExtractMethodBody(subOs, "public static bool TryBuildSnapshot(");
            string subOsBuildLogRequest = ExtractMethodBody(subOs, "public static bool TryBuildLogRequest(");
            string subOsKnownEmergencyLevel = ExtractMethodBody(subOs, "private static bool IsKnownEmergencyLevel(");
            string subOsSanitizeNormalized = ExtractMethodBody(subOs, "private static float SanitizeNormalized(");
            string subOsSanitizeNonNegative = ExtractMethodBody(subOs, "private static float SanitizeNonNegativeFinite(");
            string subOsKnownLogCode = ExtractMethodBody(subOs, "private static bool IsKnownLogCode(");
            string recordDroppedEvent = ExtractMethodBody(subOs, "private static void RecordDroppedEvent(");
            string dispatchListeners = ExtractMethodBody(subOs, "private static void DispatchRegisteredListeners(");
            string dispatchToListener = ExtractMethodBody(subOs, "private static void DispatchToListener(");
            string logListenerException = ExtractMethodBody(subOs, "private static void LogListenerDispatchException(");
            string queueDeferredRegister = ExtractMethodBody(subOs, "private static void QueueDeferredRegister(");
            string queueDeferredUnregister = ExtractMethodBody(subOs, "private static void QueueDeferredUnregister(");
            string applyDeferredMutations = ExtractMethodBody(subOs, "private static void ApplyDeferredListenerMutations()");
            string subOsReserveTelemetryFrame = ExtractMethodBody(subOs, "private static bool TryReserveTelemetryWarningFrame(");
            string subOsResolveFrame = ExtractMethodBody(subOs, "private static int ResolveCurrentFrameIndexSafe()");
            string subOsPublishWarning = ExtractMethodBody(subOs, "private static void PublishPerformanceWarningBestEffort(");
            string publishSnapshot = ExtractMethodBody(subOs, "private void PublishCurrentSnapshotIfChanged()");
            string publishShutdownSnapshot = ExtractMethodBody(subOs, "private void PublishShutdownSnapshot()");
            string publishLog = ExtractMethodBody(subOs, "private void PublishLog(");
            string recordPublishDrop = ExtractMethodBody(subOs, "private void RecordSubOsEventPublishDrop(");
            string resolveSupplyRatio = ExtractMethodBody(subOs, "private static float ResolveSupplyRatio(");
            string saturateFinite = ExtractMethodBody(subOs, "private static float SaturateFinite(");
            string nonNegativeFinite = ExtractMethodBody(subOs, "private static float NonNegativeFinite(");
            string quantizeHeat = ExtractMethodBody(subOs, "private static float QuantizeHeat01(");

            Assert.That(registerListener, Does.Contain("ActiveRuntimeInstanceChanged -= HandleAtlas6ActiveRuntimeInstanceChanged"));
            Assert.That(registerListener, Does.Contain("ActiveRuntimeInstanceChanged += HandleAtlas6ActiveRuntimeInstanceChanged"));
            Assert.That(unregisterListener, Does.Contain("ActiveRuntimeInstanceChanged -= HandleAtlas6ActiveRuntimeInstanceChanged"));
            Assert.That(refreshAtlas, Does.Contain("Atlas6CorporateLiabilityManager.ActiveRuntimeInstance"));
            Assert.That(refreshAtlas, Does.Contain("RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);"));
            Assert.That(handleAtlasChanged, Does.Contain("_atlas6Manager = activeRuntime;"));
            Assert.That(handleAtlasChanged, Does.Contain("RefreshEngineDiagnosticsTelemetry(DiagnosticsRefreshIntervalSeconds);"));
            Assert.That(handleAtlasChanged, Does.Contain("PublishCurrentSnapshotIfChanged();"));
            Assert.That(serviceReplaced, Does.Contain("_powerGridService = currentService as IPowerGridService;"));
            Assert.That(serviceReplaced, Does.Contain("RefreshTelemetryFromServices();"));
            Assert.That(serviceReplaced, Does.Contain("PublishCurrentSnapshotIfRuntimeReady();"));
            Assert.That(serviceReplaced, Does.Contain("_spectrumRuntime = currentService as SpectrumSystem;"));
            Assert.That(serviceReplaced, Does.Contain("_playerRuntime = currentService as IPlayerRuntimeContext;"));
            Assert.That(serviceReplaced, Does.Contain("RefreshPlayerDrivenStateAfterServiceReplacement();"));
            Assert.That(publishSnapshotIfReady, Does.Contain("!_runtimeLifecycleStarted || !CanUseRuntimeDispatcher()"));
            Assert.That(publishSnapshotIfReady, Does.Contain("SetSubOsPowered(ResolveSubOsPowered());"));
            Assert.That(publishSnapshotIfReady, Does.Contain("PublishCurrentSnapshotIfChanged();"));
            Assert.That(publishSnapshotIfReady, Does.Not.Contain("wasPowered"));
            Assert.That(refreshPlayerDrivenState, Does.Contain("!_runtimeLifecycleStarted || !_subOsPowered || !CanUseRuntimeDispatcher()"));
            Assert.That(refreshPlayerDrivenState, Does.Contain("EvaluateStateMachine(false);"));
            Assert.That(refreshPlayerDrivenState, Does.Contain("PublishCurrentSnapshotIfChanged();"));
            Assert.That(refreshTelemetry, Does.Contain("ResetPowerTelemetryFallback();"));
            Assert.That(refreshTelemetry, Does.Contain("SaturateFinite(batterySnapshot.ChargeNormalized, _powerSupplyRatio)"));
            Assert.That(resetPowerFallback, Does.Contain("_powerSupplyRatio = 1f;"));
            Assert.That(resetPowerFallback, Does.Contain("_powerNormalized = 1f;"));
            Assert.That(resetPowerFallback, Does.Contain("_highestBrownoutTier = LogisticsBrownoutTier.None;"));
            Assert.That(resetPowerFallback, Does.Contain("_cascadingBrownoutActive = false;"));
            Assert.That(refreshAtmosphere, Does.Contain("math.isfinite(oxygenFraction)"));
            Assert.That(refreshAtmosphere, Does.Contain("math.isfinite(carbonDioxideFraction)"));
            Assert.That(refreshAtmosphere, Does.Contain("math.isfinite(pressureKPa)"));
            Assert.That(refreshAtmosphere, Does.Contain("_carbonDioxideNormalized = 0f;"));
            Assert.That(resolveVwsFlags, Does.Contain("SaturateFinite(survivalSystem.ThermalStressSeverity01, 0f)"));
            Assert.That(resolveVwsOxygen, Does.Contain("SaturateFinite(survivalSystem.OxygenNormalized, oxygen01)"));
            Assert.That(resolveVwsOxygen, Does.Contain("SaturateFinite(oxygen01, 1f)"));
            Assert.That(resolveVitalWarning, Does.Contain("SaturateFinite(playerHealth.HealthPercent, 1f)"));
            Assert.That(refreshAtmosphere, Does.Contain("SaturateFinite(minOxygenFraction, 1f)"));
            Assert.That(refreshAtmosphere, Does.Contain("NonNegativeFinite(maxPressureKPa, DefaultReferencePressureKPa)"));
            Assert.That(powerGridTelemetry, Does.Contain("SaturateFinite(snapshot.AvailablePowerNormalized, _powerNormalized)"));
            Assert.That(powerGridTelemetry, Does.Contain("SaturateFinite(snapshot.SupplyRatio, _powerSupplyRatio)"));
            Assert.That(handleHighPressure, Does.Contain("NonNegativeFinite(pressureEvent.PressureAKPa, _maxPressureKPa)"));
            Assert.That(handleHighPressure, Does.Contain("NonNegativeFinite(pressureEvent.PressureBKPa, _maxPressureKPa)"));
            Assert.That(subOs, Does.Contain("public static int DroppedEventCount => _droppedEventCount;"));
            Assert.That(subOs, Does.Contain("public static int DroppedSnapshotEventCount => _droppedSnapshotEventCount;"));
            Assert.That(subOs, Does.Contain("public static int DroppedLogEventCount => _droppedLogEventCount;"));
            Assert.That(subOs, Does.Contain("public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;"));
            Assert.That(subOs, Does.Contain("public static int ListenerRejectCount => _listenerRejectCount;"));
            Assert.That(subOs, Does.Contain("public static int ListenerExceptionCount => _listenerExceptionCount;"));
            Assert.That(subOs, Does.Contain("public static uint ModuleId => GlobalSubmarineOsModuleId;"));
            AssertSourceOrder(subOsSubscribe, "PowerGridTelemetryEvents.Unregister(this);", "PowerGridTelemetryEvents.Register(this);");
            AssertSourceOrder(subOsSubscribe, "HighPressureEvents.Unregister(this);", "HighPressureEvents.Register(this);");
            AssertSourceOrder(subOsSubscribe, "FatalPressureImplosionEvents.Unregister(this);", "FatalPressureImplosionEvents.Register(this);");
            Assert.That(subOsRegister, Does.Contain("QueueDeferredRegister(listener);"));
            Assert.That(subOsRegister, Does.Contain("RegisterImmediate(listener);"));
            Assert.That(subOsUnregister, Does.Contain("QueueDeferredUnregister(listener);"));
            Assert.That(subOsUnregister, Does.Contain("_listeners.TryUnregister(listener);"));
            Assert.That(subOsRegisterImmediate, Does.Contain("ReportDuplicateListenerRegistration();"));
            Assert.That(subOsRegisterImmediate, Does.Contain("ReportListenerRejected();"));
            Assert.That(subOsEnqueue, Does.Contain("RecordDroppedEvent(payload.EventType);"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("!IsKnownEmergencyLevel((ushort)snapshot.EmergencyLevel)"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("ModuleId = GlobalSubmarineOsModuleId"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("(uint)snapshot.SubsystemStatus & KnownSubsystemStatusBits"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("SanitizeNormalized(snapshot.PowerNormalized)"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("SanitizeNonNegativeFinite(snapshot.MaxPressureKPa, MaximumDecodedPressureKPa)"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("snapshot.AtlasTelemetryFlags & KnownAtlasTelemetryFlags"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("math.max(0, snapshot.SonarContactCount)"));
            Assert.That(subOsTryRaiseSnapshot, Does.Contain("snapshot.VocalWarningFlags & KnownVocalWarningFlags"));
            Assert.That(subOsTryRaiseLogRequest, Does.Contain("!IsKnownLogCode((ushort)request.Code)"));
            Assert.That(subOsTryRaiseLogRequest, Does.Contain("request.Priority == 0"));
            Assert.That(subOsTryRaiseLogRequest, Does.Contain("ModuleId = GlobalSubmarineOsModuleId"));
            Assert.That(subOsBuildSnapshot, Does.Contain("payload.ModuleId != GlobalSubmarineOsModuleId"));
            Assert.That(subOsBuildSnapshot, Does.Contain("!IsKnownEmergencyLevel(payload.EmergencyLevel)"));
            Assert.That(subOsBuildSnapshot, Does.Contain("payload.StatusBits & KnownSubsystemStatusBits"));
            Assert.That(subOsBuildSnapshot, Does.Contain("SanitizeNormalized(payload.PowerNormalized)"));
            Assert.That(subOsBuildSnapshot, Does.Contain("SanitizeNonNegativeFinite(payload.MaxPressureKPa, MaximumDecodedPressureKPa)"));
            Assert.That(subOsBuildSnapshot, Does.Contain("SanitizeNonNegativeFinite(payload.SpeedKnots, MaximumDecodedSpeedKnots)"));
            Assert.That(subOsBuildSnapshot, Does.Contain("payload.AtlasTelemetryFlags & KnownAtlasTelemetryFlags"));
            Assert.That(subOsBuildSnapshot, Does.Contain("payload.VocalWarningFlags & KnownVocalWarningFlags"));
            Assert.That(subOs, Does.Contain("KnownAtlasTelemetryFlags"));
            Assert.That(subOs, Does.Contain("ThermalSheerManager.TelemetryFlagMasked"));
            Assert.That(subOs, Does.Contain("ThermalSheerManager.TelemetryFlagCriticalDowngraded"));
            Assert.That(subOs, Does.Contain("MaximumDecodedPressureKPa = 999999f"));
            Assert.That(subOs, Does.Contain("MaximumDecodedSpeedKnots = 9999.9f"));
            Assert.That(subOsKnownEmergencyLevel, Does.Contain("SubmarineEmergencyLevel.Evacuate"));
            Assert.That(subOsSanitizeNormalized, Does.Contain("math.saturate(value)"));
            Assert.That(subOsSanitizeNonNegative, Does.Contain("math.clamp(value, 0f, math.max(0f, maxValue))"));
            Assert.That(resolveSupplyRatio, Does.Contain("math.isfinite(totalConsumption)"));
            Assert.That(resolveSupplyRatio, Does.Contain("SaturateFinite(totalGeneration / totalConsumption, 1f)"));
            Assert.That(saturateFinite, Does.Contain("math.isfinite(value) ? math.saturate(value) : fallback"));
            Assert.That(nonNegativeFinite, Does.Contain("math.isfinite(value) ? math.max(0f, value) : fallback"));
            Assert.That(quantizeHeat, Does.Contain("SaturateFinite(value, 0f)"));
            Assert.That(subOsBuildLogRequest, Does.Contain("payload.ModuleId != GlobalSubmarineOsModuleId"));
            Assert.That(subOsBuildLogRequest, Does.Contain("!IsKnownLogCode(payload.LogCode)"));
            Assert.That(subOsBuildLogRequest, Does.Contain("payload.Priority == 0"));
            Assert.That(subOsBuildLogRequest, Does.Contain("payload.Priority > byte.MaxValue"));
            Assert.That(subOsKnownLogCode, Does.Contain("HectonSubmarineOsLogCode.EngineTelemetryMasked"));
            Assert.That(subOsKnownLogCode, Does.Contain("HectonSubmarineOsLogCode.EngineTelemetryRestored"));
            Assert.That(recordDroppedEvent, Does.Contain("SubmarineOsEventType.SnapshotUpdated"));
            Assert.That(recordDroppedEvent, Does.Contain("SubmarineOsEventType.LogRequested"));
            Assert.That(recordDroppedEvent, Does.Contain("_droppedEventCount++"));
            Assert.That(dispatchListeners, Does.Contain("DispatchToListener(listener, in payload);"));
            Assert.That(dispatchListeners, Does.Contain("ApplyDeferredListenerMutations();"));
            Assert.That(dispatchToListener, Does.Contain("try"));
            Assert.That(dispatchToListener, Does.Contain("catch (System.Exception exception)"));
            Assert.That(dispatchToListener, Does.Contain("ReportListenerDispatchException();"));
            Assert.That(logListenerException, Does.Contain("try"));
            Assert.That(logListenerException, Does.Contain("H8Debug.LogException(exception);"));
            Assert.That(logListenerException, Does.Contain("catch"));
            Assert.That(queueDeferredRegister, Does.Contain("!CancelDeferredUnregister(listener)"));
            Assert.That(queueDeferredRegister, Does.Contain("ReportDuplicateListenerRegistration();"));
            Assert.That(queueDeferredRegister, Does.Contain("IsDeferredRegisterPending(listener)"));
            Assert.That(queueDeferredRegister, Does.Contain("_deferredRegisterCount >= ListenerCapacity"));
            Assert.That(queueDeferredUnregister, Does.Contain("CancelDeferredRegister(listener)"));
            Assert.That(queueDeferredUnregister, Does.Contain("IsDeferredUnregisterPending(listener)"));
            Assert.That(queueDeferredUnregister, Does.Contain("_deferredUnregisterCount >= ListenerCapacity"));
            Assert.That(applyDeferredMutations, Does.Contain("_listeners.TryUnregister(listener);"));
            Assert.That(applyDeferredMutations, Does.Contain("RegisterImmediate(listener);"));
            AssertSourceOrder(
                applyDeferredMutations,
                "_deferredUnregisterCount = 0;",
                "for (int i = 0; i < _deferredRegisterCount; i++)");
            Assert.That(subOsReserveTelemetryFrame, Does.Contain("ResolveCurrentFrameIndexSafe();"));
            Assert.That(subOsReserveTelemetryFrame, Does.Contain("lastTelemetryFrame == int.MinValue"));
            Assert.That(subOsResolveFrame, Does.Contain("SystemDispatcher.CurrentFrameIndex"));
            Assert.That(subOsResolveFrame, Does.Contain("return -1;"));
            Assert.That(subOsPublishWarning, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(subOsPublishWarning, Does.Contain("catch (System.Exception telemetryException)"));
            Assert.That(subOsPublishWarning, Does.Contain("LogTelemetryWarningException(telemetryException);"));
            Assert.That(publishSnapshot, Does.Contain("RecordSubOsEventPublishDrop(snapshotDrop: true);"));
            Assert.That(publishShutdownSnapshot, Does.Contain("RecordSubOsEventPublishDrop(snapshotDrop: true);"));
            Assert.That(publishLog, Does.Contain("RecordSubOsEventPublishDrop(snapshotDrop: false);"));
            AssertSourceOrder(
                publishSnapshot,
                "HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in nextSnapshot)",
                "_lastPublishedSnapshot = nextSnapshot");
            AssertSourceOrder(
                publishShutdownSnapshot,
                "HectonSubmarineOsEvents.TryRaiseSnapshotUpdated(in shutdownSnapshot)",
                "_lastPublishedSnapshot = shutdownSnapshot");
            Assert.That(recordPublishDrop, Does.Contain("TryReserveTelemetryWarningFrame("));
            Assert.That(recordPublishDrop, Does.Contain("PublishPerformanceWarningBestEffort("));
            Assert.That(recordPublishDrop, Does.Contain("SubOsSnapshotDropWarningHash"));
            Assert.That(recordPublishDrop, Does.Contain("SubOsLogDropWarningHash"));
            Assert.That(recordPublishDrop, Does.Contain("SubOsEventDropTelemetryCooldownFrames"));
        }

        [Test]
        public void SubmarineOs_VerifiesPowerGridTelemetryEventsLogic()
        {
            string powerEvents = ReadProjectFile("Assets", "_Project", "Scripts", "Power", "PowerGridTelemetryEvents.cs");

            Assert.That(powerEvents, Does.Contain("public static int DroppedEventCount => _droppedEventCount;"));
            Assert.That(powerEvents, Does.Contain("public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;"));
            Assert.That(powerEvents, Does.Contain("public static int ListenerRejectCount => _listenerRejectCount;"));
            Assert.That(powerEvents, Does.Contain("public static int ListenerExceptionCount => _listenerExceptionCount;"));
            Assert.That(powerEvents, Does.Contain("QueueDeferredRegister(listener);"));
            Assert.That(powerEvents, Does.Contain("QueueDeferredUnregister(listener);"));
            Assert.That(powerEvents, Does.Contain("ApplyDeferredListenerMutations();"));
            Assert.That(powerEvents, Does.Contain("ReportQueueOverflow();"));
            Assert.That(powerEvents, Does.Contain("DispatchToListener(listener, in snapshot);"));
            Assert.That(powerEvents, Does.Contain("H8Debug.LogException(exception);"));
        }

        [Test]
        public void SubmarineOs_VerifiesHectonSubmarineOsDisplayLogic()
        {
            string display = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "HectonSubmarineOsDisplay.cs");
            string displayOnEnable = ExtractMethodBody(display, "private void OnEnable()");
            string refreshMetrics = ExtractMethodBody(display, "private void RefreshMetricsLabel()");
            string refreshHeatBar = ExtractMethodBody(display, "private void RefreshEngineHeatBar(");
            string displayInsertPending = ExtractMethodBody(display, "private void InsertPendingEntry(");
            string displayDropForPriority = ExtractMethodBody(display, "private bool TryDropQueuedEntryForIncomingPriority(");
            string displayRemovePending = ExtractMethodBody(display, "private void RemovePendingEntryAtLogicalIndex(");
            string displayRecordDrop = ExtractMethodBody(display, "private void RecordPendingEntryDrop(");
            string displayReserveTelemetryFrame = ExtractMethodBody(display, "private static bool TryReserveTelemetryWarningFrame(");
            string displayPublishWarning = ExtractMethodBody(display, "private static void PublishPerformanceWarningBestEffort(");

            Assert.That(display, Does.Contain("MetricBufferLength = 160"));
            Assert.That(displayInsertPending, Does.Contain("TryDropQueuedEntryForIncomingPriority(priority)"));
            Assert.That(displayInsertPending, Does.Contain("RecordPendingEntryDrop(droppedIncoming: true);"));
            Assert.That(displayInsertPending, Does.Contain("RecordPendingEntryDrop(droppedIncoming: false);"));
            Assert.That(displayDropForPriority, Does.Contain("_pendingEntries[index].Priority > incomingPriority"));
            Assert.That(displayDropForPriority, Does.Contain("RemovePendingEntryAtLogicalIndex(i);"));
            Assert.That(displayRemovePending, Does.Contain("_pendingEntries[destination] = _pendingEntries[source];"));
            Assert.That(displayRemovePending, Does.Contain("_pendingEntryTail = (_pendingEntryHead + _pendingEntryCount) % PendingEntryCapacity;"));
            Assert.That(displayRecordDrop, Does.Contain("TryReserveTelemetryWarningFrame("));
            Assert.That(displayRecordDrop, Does.Contain("PublishPerformanceWarningBestEffort("));
            Assert.That(displayRecordDrop, Does.Contain("PendingEntryDropWarningHash"));
            Assert.That(displayRecordDrop, Does.Contain("PendingEntryDropTelemetryCooldownFrames"));
            Assert.That(displayReserveTelemetryFrame, Does.Contain("ResolveCurrentFrameIndexSafe();"));
            Assert.That(displayReserveTelemetryFrame, Does.Contain("lastTelemetryFrame == int.MinValue"));
            Assert.That(displayPublishWarning, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(displayPublishWarning, Does.Contain("catch (Exception telemetryException)"));
            AssertSourceOrder(
                displayOnEnable,
                "HectonSubmarineOsEvents.Unregister(this);",
                "HectonSubmarineOsEvents.Register(this);");
            Assert.That(refreshMetrics, Does.Contain("_snapshot.EngineHeatMaskDelta01"));
            Assert.That(refreshMetrics, Does.Contain("_snapshot.IsEngineTelemetryMasked"));
            Assert.That(refreshMetrics, Does.Contain("\"  HT \""));
            Assert.That(refreshMetrics, Does.Contain("\" MSK+\""));
            Assert.That(refreshMetrics, Does.Contain("RefreshEngineHeatBar(engineHeatPercent, engineTelemetryMasked);"));
            Assert.That(refreshHeatBar, Does.Contain("engineTelemetryMasked || engineHeatPercent >= 75"));
        }

        [Test]
        public void SubmarineOs_VerifiesBIOSMessageStreamerLogic()
        {
            string bios = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "BIOSMessageStreamer.cs");
            string biosOnEnable = ExtractMethodBody(bios, "private void OnEnable()");
            string biosInsertPending = ExtractMethodBody(bios, "private void InsertPendingEntry(");
            string biosDropForPriority = ExtractMethodBody(bios, "private bool TryDropQueuedEntryForIncomingPriority(");
            string biosRemovePending = ExtractMethodBody(bios, "private void RemovePendingEntryAtLogicalIndex(");
            string biosRecordDrop = ExtractMethodBody(bios, "private void RecordPendingEntryDrop(");
            string biosReserveTelemetryFrame = ExtractMethodBody(bios, "private static bool TryReserveTelemetryWarningFrame(");
            string biosPublishWarning = ExtractMethodBody(bios, "private static void PublishPerformanceWarningBestEffort(");
            string biosBuildMessage = ExtractMethodBody(bios, "private int BuildMessage(");

            Assert.That(biosInsertPending, Does.Contain("TryDropQueuedEntryForIncomingPriority(priority)"));
            Assert.That(biosInsertPending, Does.Contain("RecordPendingEntryDrop(droppedIncoming: true);"));
            Assert.That(biosInsertPending, Does.Contain("RecordPendingEntryDrop(droppedIncoming: false);"));
            Assert.That(biosDropForPriority, Does.Contain("_pendingEntries[index].Priority > incomingPriority"));
            Assert.That(biosDropForPriority, Does.Contain("RemovePendingEntryAtLogicalIndex(i);"));
            Assert.That(biosRemovePending, Does.Contain("_pendingEntries[destination] = _pendingEntries[source];"));
            Assert.That(biosRemovePending, Does.Contain("_pendingEntryTail = (_pendingEntryHead + _pendingEntryCount) % PendingEntryCapacity;"));
            Assert.That(biosRecordDrop, Does.Contain("TryReserveTelemetryWarningFrame("));
            Assert.That(biosRecordDrop, Does.Contain("PublishPerformanceWarningBestEffort("));
            Assert.That(biosRecordDrop, Does.Contain("PendingEntryDropWarningHash"));
            Assert.That(biosRecordDrop, Does.Contain("PendingEntryDropTelemetryCooldownFrames"));
            Assert.That(biosReserveTelemetryFrame, Does.Contain("ResolveCurrentFrameIndexSafe();"));
            Assert.That(biosReserveTelemetryFrame, Does.Contain("lastTelemetryFrame == int.MinValue"));
            Assert.That(biosPublishWarning, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning"));
            Assert.That(biosPublishWarning, Does.Contain("catch (Exception telemetryException)"));
            Assert.That(biosBuildMessage, Does.Contain("HectonSubmarineOsLogCode.EngineTelemetryMasked"));
            Assert.That(biosBuildMessage, Does.Contain("HectonSubmarineOsLogCode.EngineTelemetryRestored"));
            AssertSourceOrder(
                biosOnEnable,
                "HectonSubmarineOsEvents.Unregister(this);",
                "HectonSubmarineOsEvents.Register(this);");
        }

        [Test]
        public void SubmarineOs_VerifiesVehicleSubOsCockpitRuntimeLogic()
        {
            string cockpit = ReadProjectFile("Assets", "_Project", "Scripts", "UI", "VehicleSubOsCockpitRuntime.cs");
            string cockpitOnEnable = ExtractMethodBody(cockpit, "private void OnEnable()");
            string cockpitOnDisable = ExtractMethodBody(cockpit, "private void OnDisable()");
            string cockpitOnDestroy = ExtractMethodBody(cockpit, "private void OnDestroy()");
            string cockpitEvent = ExtractMethodBody(cockpit, "void ISubmarineOsEventListener.OnSubmarineOsEvent(");
            string cockpitStatus = ExtractMethodBody(cockpit, "private int ResolveStatusDisplayMode()");
            string cockpitStatusWriter = ExtractMethodBody(cockpit, "private bool WriteStatusLine(");
            string cockpitTelemetryFlags = ExtractMethodBody(cockpit, "private uint BuildTelemetryFlags(");
            string cockpitResetTelemetry = ExtractMethodBody(cockpit, "private void ResetRuntimeTelemetryCache()");

            Assert.That(cockpit, Does.Contain("StatusModeEngineTelemetryMasked = 4"));
            Assert.That(cockpitEvent, Does.Contain("HectonSubmarineOsEvents.TryBuildSnapshot(in payload, out HectonSubmarineOsSnapshot snapshot)"));
            Assert.That(cockpitEvent, Does.Contain("snapshot.IsEngineTelemetryMasked"));
            Assert.That(cockpitStatus, Does.Contain("if (_latestEngineTelemetryMasked)"));
            Assert.That(cockpitStatus, Does.Contain("return StatusModeEngineTelemetryMasked;"));
            Assert.That(cockpitStatusWriter, Does.Contain("\"ENGINE MASKED\""));
            Assert.That(cockpitTelemetryFlags, Does.Contain("if (_latestEngineTelemetryMasked)"));
            Assert.That(cockpitTelemetryFlags, Does.Contain("flags |= 8u;"));
            AssertSourceOrder(
                cockpitOnEnable,
                "HectonSubmarineOsEvents.Unregister(this);",
                "HectonSubmarineOsEvents.Register(this);");
            AssertSourceOrder(
                cockpitOnEnable,
                "PowerGridTelemetryEvents.Unregister(this);",
                "PowerGridTelemetryEvents.Register(this);");
            Assert.That(cockpitOnDisable, Does.Contain("ResetRuntimeTelemetryCache();"));
            Assert.That(cockpitOnDestroy, Does.Contain("HectonSubmarineOsEvents.Unregister(this);"));
            Assert.That(cockpitOnDestroy, Does.Contain("PowerGridTelemetryEvents.Unregister(this);"));
            Assert.That(cockpitOnDestroy, Does.Contain("UnregisterRuntime();"));
            Assert.That(cockpitOnDestroy, Does.Contain("ResetRuntimeTelemetryCache();"));
            Assert.That(cockpitResetTelemetry, Does.Contain("_latestPowerRatio = 1f;"));
            Assert.That(cockpitResetTelemetry, Does.Contain("_latestOxygenNormalized = 1f;"));
            Assert.That(cockpitResetTelemetry, Does.Contain("_latestEngineTelemetryMasked = false;"));
            Assert.That(cockpitResetTelemetry, Does.Contain("InvalidateOffscreenTextCache();"));
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
        public void EndingTerminalInteractable_ChoiceNotificationRefusalIsDiagnosticAndDoesNotGateChoiceOpen()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Gameplay/EndingTerminalInteractable.cs"));
            string openChoice = ExtractMethodBody(source, "private void OpenChoiceUI()");
            string push = ExtractMethodBody(source, "private void TryPushChoiceNotification(");
            string report = ExtractMethodBody(source, "private void ReportChoiceNotificationMiss()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");

            StringAssert.Contains("public int ChoiceNotificationMissCount =>", source);
            StringAssert.Contains("s_choiceNotificationMissWarningHash", source);
            StringAssert.Contains("s_choiceNotificationContextHash", source);
            AssertSourceOrder(openChoice, "_choiceOpen = true;", "NarrativeEvents.TryRaiseDiscoveryMade(s_atlasCoreDataAccessedDiscoveryHash);");
            AssertSourceOrder(openChoice, "NarrativeEvents.TryRaiseDiscoveryMade(s_atlasCoreDataAccessedDiscoveryHash);", "TryPushChoiceNotification(");
            AssertSourceOrder(openChoice, "TryPushChoiceNotification(", "LogChoiceUiOpened();");
            StringAssert.DoesNotContain("Hecton8.UI.NotificationEvents.TryPushWarning(\n                _dataLoadedTextBuffer", openChoice);

            StringAssert.Contains("Hecton8.UI.NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("ReportChoiceNotificationMiss();", push);
            StringAssert.Contains("_choiceNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("s_choiceNotificationMissWarningHash", report);
            StringAssert.Contains("s_endingTerminalContextHash ^ s_choiceNotificationContextHash", report);
            StringAssert.Contains("Math.Max(1, _choiceNotificationMissCount)", report);
            StringAssert.Contains("ClearChoiceNotificationDiagnostics();", onDisable);
        }

        [Test]
        public void QuestGraphEvaluator_SanitizesSignalIngressPayloads()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Quest/QuestGraphEvaluator.cs"));
            string constructor = ExtractMethodBody(source, "public QuestGraphEvaluator(QuestStateManager stateManager, Action onResultsAvailable)");
            string dispose = ExtractMethodBody(source, "public void Dispose()");
            string disposePendingSignals = ExtractMethodBody(source, "private void DisposePendingSignals()");
            string updateDepthContext = ExtractMethodBody(source, "public void UpdateDepthContext(float depthMeters, uint zoneHash, bool isThermalZone)");
            string depthTier = ExtractMethodBody(source, "public void OnDepthTierChanged(int depthTier, float depthMeters)");
            string atlasSignal = ExtractMethodBody(source, "public void OnAtlasSignalEvent(in AtlasSignalEventPayload payload)");
            string enqueue = ExtractMethodBody(source, "private void EnqueueSignal(in QuestSignalPayload payload)");
            string sanitize = ExtractMethodBody(source, "private static bool TrySanitizeSignalPayload(");
            string signalKind = ExtractMethodBody(source, "private static bool IsKnownSignalKind(ushort eventType)");

            StringAssert.Contains("private int _pendingSignalsSentinelId;", source);
            StringAssert.Contains("_pendingSignalsSentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(", constructor);
            StringAssert.Contains("if (_pendingSignalsSentinelId <= 0)", constructor);
            StringAssert.Contains("DisposePendingSignals();", constructor);
            StringAssert.Contains("DisposePendingSignals();", dispose);
            StringAssert.Contains("_pendingSignals.Dispose();", disposePendingSignals);
            StringAssert.DoesNotContain("bool disposed = !", disposePendingSignals);
            StringAssert.DoesNotContain("if (disposed &&", disposePendingSignals);
            StringAssert.Contains("NativeMemorySentinel.Unregister(_pendingSignalsSentinelId);", disposePendingSignals);
            StringAssert.Contains("_pendingSignalsSentinelId = 0;", disposePendingSignals);
            StringAssert.Contains("finally", disposePendingSignals);
            Assert.Less(
                disposePendingSignals.IndexOf("NativeMemorySentinel.Unregister(_pendingSignalsSentinelId);", StringComparison.Ordinal),
                disposePendingSignals.IndexOf("_pendingSignals.Dispose();", StringComparison.Ordinal));
            StringAssert.DoesNotContain("NativeMemorySentinel.UnregisterNativeQueue(nameof(QuestGraphEvaluator), _pendingSignalsSentinelLabel);", source);
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
            AssertAtlasRuntimeUsesGuardedSaveHotSwap(
                "_Project/Scripts/Gameplay/PDAExchangeSystem.cs");
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
            string unregisterBody = ExtractMethodBody(source, "private void TryUnregisterSaveParticipant()");
            string saveUsableBody = ExtractMethodBody(source, "private static bool IsSaveServiceUsable(");

            Assert.IsTrue(
                hotSwapBody.Contains("case GlobalRegistryServiceSlot.Save:") ||
                hotSwapBody.Contains("serviceSlot == GlobalRegistryServiceSlot.Save"),
                projectRelativePath);
            StringAssert.Contains("TryUnregisterSaveParticipant();", hotSwapBody);
            StringAssert.Contains("TryRegisterSaveParticipant();", hotSwapBody);
            StringAssert.DoesNotContain("_saveService.Register(this);", hotSwapBody);
            StringAssert.Contains("!Application.isPlaying", registerBody);
            StringAssert.Contains("!isActiveAndEnabled", registerBody);
            StringAssert.Contains("ISaveService saveService = _saveService;", registerBody);
            StringAssert.Contains("if (!IsSaveServiceUsable(saveService))", registerBody);
            StringAssert.Contains("_saveService = saveService;", registerBody);
            StringAssert.Contains("saveService.Register(this);", registerBody);
            StringAssert.Contains("_registeredSaveService = saveService;", registerBody);
            StringAssert.Contains("private ISaveService _registeredSaveService;", source);
            StringAssert.Contains("if (!_saveRegistered && _registeredSaveService == null)", unregisterBody);
            StringAssert.Contains("ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;", unregisterBody);
            StringAssert.Contains("_registeredSaveService = null;", unregisterBody);
            StringAssert.Contains("return saveService != null && saveService.IsInitialized;", saveUsableBody);
            StringAssert.DoesNotContain("_saveService.Register(this);", registerBody);
            StringAssert.DoesNotContain("ISaveService saveService = _saveService;", unregisterBody);
        }

        private static string ReadProjectFile(params string[] parts)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int i = 0; i < parts.Length; i++)
                path = Path.Combine(path, parts[i]);
            return File.ReadAllText(path);
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

        private static void AssertSourceOrder(string source, string first, string second)
        {
            Assert.IsNotNull(source);
            int firstIndex = source.IndexOf(first, StringComparison.Ordinal);
            int secondIndex = source.IndexOf(second, StringComparison.Ordinal);
            Assert.GreaterOrEqual(firstIndex, 0, first);
            Assert.GreaterOrEqual(secondIndex, 0, second);
            Assert.Less(firstIndex, secondIndex, first + " must appear before " + second);
        }

        private static void AssertRepairDroneTorchAcousticEventBusContract(string busSource)
        {
            Assert.IsNotNull(busSource);
            Assert.That(busSource, Does.Contain("public static int DroppedEventCount => _droppedEventCount;"));
            Assert.That(busSource, Does.Contain("public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;"));
            Assert.That(busSource, Does.Contain("public static int ListenerRejectCount => _listenerRejectCount;"));
            Assert.That(busSource, Does.Contain("public static int ListenerExceptionCount => _listenerExceptionCount;"));
            Assert.That(busSource, Does.Contain("Use TryNotify(in RepairDroneTorchAcousticEvent) so bounded queue rejection stays visible at the producer."));
            Assert.That(busSource, Does.Contain("public static bool TryNotify(in RepairDroneTorchAcousticEvent acousticEvent)"));
            Assert.That(busSource, Does.Contain("return Enqueue(in payload);"));
            Assert.That(busSource, Does.Contain("_deferredRegisterListeners"));
            Assert.That(busSource, Does.Contain("_deferredUnregisterListeners"));
            Assert.That(busSource, Does.Contain("QueueDeferredRegister(listener);"));
            Assert.That(busSource, Does.Contain("QueueDeferredUnregister(listener);"));
            Assert.That(busSource, Does.Contain("TryUnregisterImmediate(listener);"));
            Assert.That(busSource, Does.Contain("RegisterImmediate(listener);"));
            Assert.That(busSource, Does.Contain("DispatchToListener(listener, in acousticEvent);"));
            Assert.That(busSource, Does.Contain("catch (Exception exception)"));
            Assert.That(busSource, Does.Contain("H8Debug.LogException(exception);"));
            Assert.That(busSource, Does.Contain("ApplyDeferredListenerMutations();"));
            Assert.That(busSource, Does.Contain("private static readonly ushort[] _referenceSlotGenerations"));
            Assert.That(busSource, Does.Contain("payload.Reserved = referenceGeneration;"));
            Assert.That(busSource, Does.Contain("!IsReferenceSlotPayloadCurrent(in payload)"));
            Assert.That(busSource, Does.Contain("ReleaseReferenceSlotForPayload(in payload);"));
            Assert.That(busSource, Does.Contain("RecordDroppedEvent();"));
            Assert.That(busSource, Does.Contain("PublishWarningOncePerFrame("));
            Assert.That(busSource, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, math.max(1, count));"));
            Assert.That(busSource, Does.Contain("SaturatingIncrement("));
        }

        private static Hecton8.UI.BIOSMessageStreamer CreateInitializedBiosStreamer(GameObject host)
        {
            Assert.IsNotNull(host);
            host.SetActive(false);
            Hecton8.UI.BIOSMessageStreamer streamer = host.AddComponent<Hecton8.UI.BIOSMessageStreamer>();
            InvokePrivateInstanceMethod(streamer, "Awake");
            return streamer;
        }

        private static Hecton8.UI.HectonSubmarineOsDisplay CreateInactiveSubmarineOsDisplay(GameObject host)
        {
            Assert.IsNotNull(host);
            host.SetActive(false);
            return host.AddComponent<Hecton8.UI.HectonSubmarineOsDisplay>();
        }

        private static HectonSubmarineOsSnapshot CreateSubmarineOsTestSnapshot(SubmarineEmergencyLevel emergencyLevel)
        {
            return new HectonSubmarineOsSnapshot(
                SubsystemStatus.Engines,
                emergencyLevel,
                1f,
                1f,
                0f,
                100f,
                4f,
                0.25f,
                0.25f,
                0f,
                0u,
                0,
                0,
                SubmarineVwsFlags.None,
                false,
                false,
                false,
                true);
        }

        private static void AssertPressureEventBusContract(string busSource, string busName)
        {
            Assert.IsNotNull(busSource, busName);
            Assert.That(busSource, Does.Contain("public static int DroppedEventCount => _droppedEventCount;"), busName);
            Assert.That(busSource, Does.Contain("public static int DuplicateListenerRegistrationCount => _duplicateListenerRegistrationCount;"), busName);
            Assert.That(busSource, Does.Contain("public static int ListenerRejectCount => _listenerRejectCount;"), busName);
            Assert.That(busSource, Does.Contain("public static int ListenerExceptionCount => _listenerExceptionCount;"), busName);
            Assert.That(busSource, Does.Contain("_deferredRegisterListeners"), busName);
            Assert.That(busSource, Does.Contain("_deferredUnregisterListeners"), busName);
            Assert.That(busSource, Does.Contain("QueueDeferredRegister(listener);"), busName);
            Assert.That(busSource, Does.Contain("QueueDeferredUnregister(listener);"), busName);
            Assert.That(busSource, Does.Contain("ApplyDeferredListenerMutations();"), busName);
            Assert.That(busSource, Does.Contain("DispatchToListener(listener,"), busName);
            Assert.That(busSource, Does.Contain("catch (System.Exception exception)"), busName);
            Assert.That(busSource, Does.Contain("H8Debug.LogException(exception);"), busName);
            Assert.That(busSource, Does.Contain("RecordDroppedEvent();"), busName);
            Assert.That(busSource, Does.Contain("PublishWarningOncePerFrame("), busName);
            Assert.That(busSource, Does.Contain("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);"), busName);
            Assert.That(busSource, Does.Contain("LogListenerDispatchException(exception);"), busName);
            Assert.That(busSource, Does.Contain("SaturatingIncrement("), busName);
        }

        private static PowerGridTelemetrySnapshot CreatePowerGridTelemetryTestSnapshot(float supplyRatio)
        {
            return new PowerGridTelemetrySnapshot(
                1,
                supplyRatio < 1f ? 1 : 0,
                supplyRatio,
                1f,
                supplyRatio,
                supplyRatio,
                supplyRatio,
                supplyRatio < 1f ? LogisticsBrownoutTier.AmbientLightsOnly : LogisticsBrownoutTier.None,
                supplyRatio < 1f,
                false);
        }

        private static int CountPendingSubmarineOsEntries(object owner, HectonSubmarineOsLogCode code)
        {
            Assert.IsNotNull(owner);
            Array pendingEntries = GetPrivateInstanceField<Array>(owner, "_pendingEntries");
            int pendingHead = GetPrivateInstanceField<int>(owner, "_pendingEntryHead");
            int pendingCount = GetPrivateInstanceField<int>(owner, "_pendingEntryCount");
            Assert.IsNotNull(pendingEntries);
            Assert.LessOrEqual(pendingCount, pendingEntries.Length);

            int matchCount = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                int index = (pendingHead + i) % pendingEntries.Length;
                object entry = pendingEntries.GetValue(index);
                if (ReadPendingSubmarineOsEntryCode(entry) == code)
                    matchCount++;
            }

            return matchCount;
        }

        private static HectonSubmarineOsLogCode ReadPendingSubmarineOsEntryCode(object entry)
        {
            Assert.IsNotNull(entry);
            FieldInfo field = entry.GetType().GetField("Code", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "PendingEntry.Code");
            return (HectonSubmarineOsLogCode)field.GetValue(entry);
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

        private static void InvokePrivateStaticMethod(Type ownerType, string methodName)
        {
            Assert.IsNotNull(ownerType);
            MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(null, null);
        }

        private static void InvokePrivateStaticMethod(Type ownerType, string methodName, params object[] args)
        {
            Assert.IsNotNull(ownerType);
            MethodInfo method = ownerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(null, args);
        }

        private static T GetPrivateInstanceField<T>(object target, string fieldName)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(target);
        }

        private static T GetPrivateStaticField<T>(Type ownerType, string fieldName)
        {
            Assert.IsNotNull(ownerType);
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            return (T)field.GetValue(null);
        }

        private static void SetPrivateStaticField(Type ownerType, string fieldName, object value)
        {
            Assert.IsNotNull(ownerType);
            FieldInfo field = ownerType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(null, value);
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

        private sealed class CountingSubmarineOsListener : ISubmarineOsEventListener
        {
            public int Count;
            public Action OnEvent;

            public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
            {
                Count++;
                OnEvent?.Invoke();
            }
        }

        private sealed class CapturingSubmarineOsListener : ISubmarineOsEventListener
        {
            public int Count;
            public SubmarineOsEventPayload LastPayload;

            public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
            {
                Count++;
                LastPayload = payload;
            }
        }

        private sealed class ThrowingSubmarineOsListener : ISubmarineOsEventListener
        {
            public int Count;

            public void OnSubmarineOsEvent(in SubmarineOsEventPayload payload)
            {
                Count++;
                throw new InvalidOperationException("Submarine OS listener test exception.");
            }
        }

        private sealed class CountingPowerGridTelemetryListener : IPowerGridTelemetryListener
        {
            public int Count;
            public PowerGridTelemetrySnapshot LastSnapshot;
            public Action OnEvent;

            public void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
            {
                Count++;
                LastSnapshot = snapshot;
                OnEvent?.Invoke();
            }
        }

        private sealed class ThrowingPowerGridTelemetryListener : IPowerGridTelemetryListener
        {
            public int Count;

            public void OnPowerGridTelemetryUpdated(in PowerGridTelemetrySnapshot snapshot)
            {
                Count++;
                throw new InvalidOperationException("Power grid listener test exception.");
            }
        }

        private sealed class CapturingStorageReservationCommitListener : ThreadSafeCommandQueue.IStorageReservationCommitResolvedListener
        {
            public int Count;
            public int FirstRequesterId;
            public int FirstReservationId;
            public bool FirstCommitted;
            public int LastRequesterId;
            public int LastReservationId;
            public bool LastCommitted;
            public Action OnEvent;

            public void OnStorageReservationCommitResolved(in ThreadSafeCommandQueue.StorageReservationCommitResolvedPayload payload)
            {
                Count++;
                if (Count == 1)
                {
                    FirstRequesterId = payload.RequesterId;
                    FirstReservationId = payload.ReservationId;
                    FirstCommitted = payload.Committed != 0;
                }

                LastRequesterId = payload.RequesterId;
                LastReservationId = payload.ReservationId;
                LastCommitted = payload.Committed != 0;
                OnEvent?.Invoke();
            }
        }

        private sealed class ThrowingStorageReservationCommitListener : ThreadSafeCommandQueue.IStorageReservationCommitResolvedListener
        {
            public int Count;

            public void OnStorageReservationCommitResolved(in ThreadSafeCommandQueue.StorageReservationCommitResolvedPayload payload)
            {
                Count++;
                throw new InvalidOperationException("Storage reservation listener test exception.");
            }
        }

        private sealed class FatalStorageReservationCommitListener : ThreadSafeCommandQueue.IStorageReservationCommitResolvedListener
        {
            public int Count;

            public void OnStorageReservationCommitResolved(in ThreadSafeCommandQueue.StorageReservationCommitResolvedPayload payload)
            {
                Count++;
                throw new FatalArchitectureException("Storage reservation listener fatal test exception.");
            }
        }

        private sealed class StorageReservationCommitTargetProbe : MonoBehaviour, IStorageReservationCommitTarget
        {
            public int CommitCount;
            public int LastReservationId;
            public int ReleaseCount;
            public int LastReleasedReservationId;

            public bool TryCommitReservation(int reservationId)
            {
                CommitCount++;
                LastReservationId = reservationId;
                return reservationId > 0;
            }

            public void ReleaseReservation(int reservationId)
            {
                ReleaseCount++;
                LastReleasedReservationId = reservationId;
            }
        }
    }
}
