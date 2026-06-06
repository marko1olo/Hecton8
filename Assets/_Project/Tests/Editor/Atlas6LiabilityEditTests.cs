using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Atlas6Liability;
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
                actuarial.RegisterWorkerTagRecovery("worker");

            Assert.IsTrue(actuarial.IsPlayerActuarialThreat);
            Assert.AreEqual(1, threatEvents);
            Assert.AreEqual(1, droneHaltEvents);
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
        public void ActuarialGhostUpload_RejectsInvalidDataSize()
        {
            Atlas6LiabilityTelemetry telemetry = new Atlas6LiabilityTelemetry();
            ActuarialLiabilitySystem actuarial = new ActuarialLiabilitySystem(telemetry);
            actuarial.Initialize(5000f);

            actuarial.UploadGhostPDAData(float.NaN);
            actuarial.UploadGhostPDAData(-1f);
            actuarial.UploadGhostPDAData(float.MaxValue);

            Assert.AreEqual(5000f, actuarial.CorporateCreditBalance);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidGhostPDADataReported, latest.EventCode);
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
    }
}
