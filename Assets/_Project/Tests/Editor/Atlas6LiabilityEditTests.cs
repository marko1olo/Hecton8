using System.Runtime.InteropServices;
using Hecton8.AtlasSignal;
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

            Assert.AreEqual(5000f, actuarial.CorporateCreditBalance);
            Assert.IsTrue(telemetry.TryCopyLatest(out Atlas6LiabilityTelemetryRecord latest));
            Assert.AreEqual((ushort)Atlas6LiabilityEventCode.InvalidGhostPDADataReported, latest.EventCode);
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
