using System;
using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class HazardZoneRuntimeSaveEditTests
    {
        private const int BinaryPayloadScratchBytes = 1024 * 1024;

        [Test]
        public void SaveEventPayload_IsExplicitTwentyFourBytes()
        {
            StructLayoutAttribute layout = typeof(SaveEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(24, UnsafeUtility.SizeOf<SaveEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<SaveEventPayload>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.TimestampTicks)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.SlotHash)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.MessageHash)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.MessageSlot)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload.Type)));
        }

        [Test]
        public void HazardZoneRuntimeDTO_IsExplicitEightBytes()
        {
            StructLayoutAttribute layout = typeof(HazardZoneRuntimeDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(8, UnsafeUtility.SizeOf<HazardZoneRuntimeDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardZoneRuntimeDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardZoneRuntimeDTO>(nameof(HazardZoneRuntimeDTO.toxicityDose)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<HazardZoneRuntimeDTO>(nameof(HazardZoneRuntimeDTO.toxicityPulseAccumulatorSeconds)));
        }

        [Test]
        public void HazardZoneRuntime_RoundTripsThroughBinaryPayload()
        {
            SaveData data = SaveData.CreateNew(42.0);
            data.hazardZones.toxicityDose = 12.25f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

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
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion, restored.version);
                Assert.AreEqual(12.25f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0.25f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
                Assert.IsTrue(BitConverter.IsLittleEndian);
                Assert.AreEqual(1, CountLittleEndianFloatPair(payload, bytesWritten, 12.25f, 0.25f));
                Assert.AreEqual(0, CountLittleEndianFloatPair(payload, bytesWritten, 0.25f, 12.25f));
            }
        }

        [Test]
        public void HazardZoneRuntime_PreV74BinaryPayloadReadsWithoutHazardBytes()
        {
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;

            SaveData data = SaveData.CreateNew(42.0);
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;

            byte[] currentPayload = new byte[BinaryPayloadScratchBytes];
            int currentBytesWritten;
            fixed (byte* currentPayloadPtr = currentPayload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    currentPayloadPtr,
                    currentPayload.Length,
                    out currentBytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(currentBytesWritten, 0);
            }

            int hazardBytes = sizeof(float) * 2;
            int hazardOffset = FindLittleEndianFloatPairOffset(
                currentPayload,
                currentBytesWritten,
                hazardDoseMarker,
                hazardPulseMarker);
            Assert.GreaterOrEqual(hazardOffset, sizeof(int));
            Assert.AreEqual(1, CountLittleEndianFloatPair(
                currentPayload,
                currentBytesWritten,
                hazardDoseMarker,
                hazardPulseMarker));

            int legacyBytesWritten = currentBytesWritten - hazardBytes;
            byte[] legacyPayload = new byte[legacyBytesWritten];
            Buffer.BlockCopy(currentPayload, 0, legacyPayload, 0, hazardOffset);
            Buffer.BlockCopy(
                currentPayload,
                hazardOffset + hazardBytes,
                legacyPayload,
                hazardOffset,
                currentBytesWritten - hazardOffset - hazardBytes);
            Buffer.BlockCopy(
                BitConverter.GetBytes(SaveData.HazardZoneRuntimePersistenceVersion - 1),
                0,
                legacyPayload,
                0,
                sizeof(int));

            fixed (byte* legacyPayloadPtr = legacyPayload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    legacyPayloadPtr,
                    legacyPayload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(legacyBytesWritten, bytesRead);
                Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion - 1, restored.version);
                Assert.AreEqual(0f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void HazardZoneRuntime_WriteClampsOutOfRangeValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = 128f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 3f;

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
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.HazardZoneMaxPersistedToxicityDose, restored.hazardZones.toxicityDose);
                Assert.AreEqual(
                    SaveData.HazardZoneMaxPersistedToxicityPulseSeconds,
                    restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void HazardZoneRuntime_ReadClampsNonFiniteFileValues()
        {
            const float hazardDoseMarker = 12.25f;
            const float hazardPulseMarker = 0.25f;

            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = hazardDoseMarker;
            data.hazardZones.toxicityPulseAccumulatorSeconds = hazardPulseMarker;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            int hazardOffset = FindLittleEndianFloatPairOffset(
                payload,
                bytesWritten,
                hazardDoseMarker,
                hazardPulseMarker);
            Assert.GreaterOrEqual(hazardOffset, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(float.NaN), 0, payload, hazardOffset, sizeof(float));
            Buffer.BlockCopy(BitConverter.GetBytes(3f), 0, payload, hazardOffset + sizeof(float), sizeof(float));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
            }
        }

        [Test]
        public void RadiationGridRlePersistenceBounds_MatchRuntimeWorstCase()
        {
            Assert.AreEqual(32, SaveData.RadiationGridResolution);
            Assert.AreEqual(
                SaveData.RadiationGridResolution * SaveData.RadiationGridResolution * SaveData.RadiationGridResolution,
                SaveData.RadiationGridCellCount);
            Assert.AreEqual(sizeof(ushort) + sizeof(byte) + sizeof(ushort), SaveData.RadiationGridRlePacketSizeBytes);
            Assert.AreEqual(163840, SaveData.RadiationGridRleMaxBytes);
            Assert.AreEqual(SaveData.RadiationGridResolution, RadiationHazardGrid.GridResolution);
            Assert.AreEqual(SaveData.RadiationGridCellCount, RadiationHazardGrid.GridCellCount);
            Assert.AreEqual(SaveData.RadiationGridRlePacketSizeBytes, RadiationHazardGrid.RlePacketSizeBytes);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, RadiationHazardGrid.MaxRlePayloadBytes);

            SaveData data = SaveData.CreateNew(0.0);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsFutureSaveDataVersion()
        {
            byte[] payload = BitConverter.GetBytes(SaveData.CurrentVersion + 1);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    payload.Length,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsFalse(read);
                Assert.IsNull(restored);
                Assert.AreEqual(0, bytesRead);
                StringAssert.Contains("Unsupported save data version", readError);
            }
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsWritingFutureSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int futureVersion = SaveData.CurrentVersion + 1;
            data.version = futureVersion;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsFalse(wrote);
                Assert.AreEqual(0, bytesWritten);
                Assert.AreEqual(futureVersion, data.version);
                StringAssert.Contains("Unsupported save data version", writeError);
            }
        }

        [Test]
        public void SaveBinaryPayloadCodec_RejectsWritingUnmigratedOlderSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int legacyVersion = SaveData.HazardZoneRuntimePersistenceVersion - 1;
            data.version = legacyVersion;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out int bytesWritten,
                    out string writeError);

                Assert.IsFalse(wrote);
                Assert.AreEqual(0, bytesWritten);
                Assert.AreEqual(legacyVersion, data.version);
                StringAssert.Contains("must be migrated before writing", writeError);
            }
        }

        [Test]
        public void SaveDataMigration_DoesNotDowngradeFutureSaveDataVersion()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int futureVersion = SaveData.CurrentVersion + 1;
            data.version = futureVersion;
            data.hazardZones.toxicityDose = 12f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsFalse(changed);
            Assert.AreEqual(futureVersion, originalVersion);
            Assert.AreEqual(futureVersion, data.version);
            Assert.AreEqual(12f, data.hazardZones.toxicityDose);
            StringAssert.Contains("unsupported future save data version", summary);
        }

        [Test]
        public void RadiationGridRuntime_WriteClampsOversizedRlePayloadToPersistedMaximum()
        {
            SaveData data = SaveData.CreateNew(0.0);
            int oversizedLength = SaveData.RadiationGridRleMaxBytes + 16;
            data.radiationGridRle = new byte[oversizedLength];
            data.radiationGridRleLength = oversizedLength;

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
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, restored.radiationGridRleLength);
                Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, restored.radiationGridRle.Length);
            }
        }

        [Test]
        public void RadiationGridRuntime_WriteClampsNonFiniteValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.radiationDose = float.NaN;
            data.radiationGridOriginX = double.PositiveInfinity;
            data.radiationGridOriginY = double.NaN;
            data.radiationGridOriginZ = double.NegativeInfinity;
            data.radiationGridCellSizeMeters = float.NaN;

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
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.radiationDose);
                Assert.AreEqual(0d, restored.radiationGridOriginX);
                Assert.AreEqual(0d, restored.radiationGridOriginY);
                Assert.AreEqual(0d, restored.radiationGridOriginZ);
                Assert.AreEqual(4f, restored.radiationGridCellSizeMeters);
            }
        }

        [Test]
        public void RadiationGridRuntime_ReadClampsNonFiniteFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.radiationDose = 7.25f;
            data.radiationGridOriginX = 101.25d;
            data.radiationGridOriginY = -202.5d;
            data.radiationGridOriginZ = 303.75d;
            data.radiationGridCellSizeMeters = 6.5f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            byte[] marker = BuildLittleEndianRadiationGridHeader(7.25f, 101.25d, -202.5d, 303.75d, 6.5f);
            byte[] replacement = BuildLittleEndianRadiationGridHeader(
                float.NaN,
                double.PositiveInfinity,
                double.NaN,
                double.NegativeInfinity,
                float.NaN);
            int radiationOffset = FindLittleEndianByteSequenceOffset(payload, bytesWritten, marker);
            Assert.GreaterOrEqual(radiationOffset, sizeof(int));
            Buffer.BlockCopy(replacement, 0, payload, radiationOffset, replacement.Length);

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.radiationDose);
                Assert.AreEqual(0d, restored.radiationGridOriginX);
                Assert.AreEqual(0d, restored.radiationGridOriginY);
                Assert.AreEqual(0d, restored.radiationGridOriginZ);
                Assert.AreEqual(4f, restored.radiationGridCellSizeMeters);
            }
        }

        [Test]
        public void PlayerStatsRuntime_ReadClampsNonFiniteResourceFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.oxygen = 91.25f;
            data.playerStats.energy = 82.5f;
            data.playerStats.integrity = 73.75f;
            data.playerStats.weight = 14.5f;
            data.playerStats.hunger = 64.25f;
            data.playerStats.thirst = 55.5f;
            data.playerStats.currentLifeLowestOxygenNormalized = 0.21f;
            data.playerStats.currentLifeLowestEnergyNormalized = 0.32f;
            data.playerStats.currentLifeLowestIntegrityNormalized = 0.43f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            Assert.AreEqual(
                1,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 91.25f, 82.5f, 73.75f, 14.5f, 64.25f, 55.5f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.NaN, float.PositiveInfinity, float.NegativeInfinity }));
            Assert.AreEqual(
                1,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 0.21f, 0.32f, 0.43f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity }));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.playerStats.oxygen);
                Assert.AreEqual(0f, restored.playerStats.energy);
                Assert.AreEqual(0f, restored.playerStats.integrity);
                Assert.AreEqual(0f, restored.playerStats.weight);
                Assert.AreEqual(0f, restored.playerStats.hunger);
                Assert.AreEqual(0f, restored.playerStats.thirst);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestOxygenNormalized);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestEnergyNormalized);
                Assert.AreEqual(1f, restored.playerStats.currentLifeLowestIntegrityNormalized);
            }
        }

        [Test]
        public void PlayerStatsRuntime_ReadClampsNonFiniteKinematicFileValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.playerStats.posX = 1234.25f;
            data.playerStats.posY = 2345.5f;
            data.playerStats.posZ = 3456.75f;
            data.playerStats.rotX = 0.5f;
            data.playerStats.rotY = 0.5f;
            data.playerStats.rotZ = 0.5f;
            data.playerStats.rotW = 0.5f;
            data.playerStats.velX = 4.5f;
            data.playerStats.velY = 5.5f;
            data.playerStats.velZ = 6.5f;

            byte[] payload = new byte[BinaryPayloadScratchBytes];
            int bytesWritten;
            fixed (byte* payloadPtr = payload)
            {
                bool wrote = SaveBinaryPayloadCodec.TryWrite(
                    data,
                    payloadPtr,
                    payload.Length,
                    out bytesWritten,
                    out string writeError);

                Assert.IsTrue(wrote, writeError);
                Assert.Greater(bytesWritten, 0);
            }

            Assert.AreEqual(
                2,
                PatchLittleEndianFloatSequence(
                    payload,
                    bytesWritten,
                    new[] { 1234.25f, 2345.5f, 3456.75f, 0.5f, 0.5f, 0.5f, 0.5f, 4.5f, 5.5f, 6.5f },
                    new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, 0f, 0f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity }));

            fixed (byte* payloadPtr = payload)
            {
                bool read = SaveBinaryPayloadCodec.TryRead(
                    payloadPtr,
                    bytesWritten,
                    out SaveData restored,
                    out int bytesRead,
                    out string readError);

                Assert.IsTrue(read, readError);
                Assert.AreEqual(bytesWritten, bytesRead);
                Assert.AreEqual(0f, restored.playerStats.posX);
                Assert.AreEqual(0f, restored.playerStats.posY);
                Assert.AreEqual(0f, restored.playerStats.posZ);
                Assert.AreEqual(0f, restored.playerStats.rotX);
                Assert.AreEqual(0f, restored.playerStats.rotY);
                Assert.AreEqual(0f, restored.playerStats.rotZ);
                Assert.AreEqual(1f, restored.playerStats.rotW);
                Assert.AreEqual(0f, restored.playerStats.velX);
                Assert.AreEqual(0f, restored.playerStats.velY);
                Assert.AreEqual(0f, restored.playerStats.velZ);
                Assert.AreEqual(0f, restored.playerKinematicState.posX);
                Assert.AreEqual(0f, restored.playerKinematicState.posY);
                Assert.AreEqual(0f, restored.playerKinematicState.posZ);
                Assert.AreEqual(0f, restored.playerKinematicState.rotX);
                Assert.AreEqual(0f, restored.playerKinematicState.rotY);
                Assert.AreEqual(0f, restored.playerKinematicState.rotZ);
                Assert.AreEqual(1f, restored.playerKinematicState.rotW);
                Assert.AreEqual(0f, restored.playerKinematicState.velX);
                Assert.AreEqual(0f, restored.playerKinematicState.velY);
                Assert.AreEqual(0f, restored.playerKinematicState.velZ);
            }
        }

        [Test]
        public void HazardZoneRuntimeMigration_PreV74DropsUnpersistedToxicity()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion - 1;
            data.hazardZones.toxicityDose = 32f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        [Test]
        public void HazardZoneRuntimeMigration_V74ClampsNonFiniteAndOutOfRangeValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion;
            data.hazardZones.toxicityDose = float.NaN;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 3f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        private static int CountLittleEndianFloatPair(byte[] payload, int bytesWritten, float first, float second)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] firstBytes = BitConverter.GetBytes(first);
            byte[] secondBytes = BitConverter.GetBytes(second);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int count = 0;
            for (int i = 0; i <= safeLength - sizeof(float) * 2; i++)
            {
                if (payload[i] == firstBytes[0] &&
                    payload[i + 1] == firstBytes[1] &&
                    payload[i + 2] == firstBytes[2] &&
                    payload[i + 3] == firstBytes[3] &&
                    payload[i + 4] == secondBytes[0] &&
                    payload[i + 5] == secondBytes[1] &&
                    payload[i + 6] == secondBytes[2] &&
                    payload[i + 7] == secondBytes[3])
                {
                    count++;
                }
            }

            return count;
        }

        private static int FindLittleEndianFloatPairOffset(byte[] payload, int bytesWritten, float first, float second)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] firstBytes = BitConverter.GetBytes(first);
            byte[] secondBytes = BitConverter.GetBytes(second);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            for (int i = 0; i <= safeLength - sizeof(float) * 2; i++)
            {
                if (payload[i] == firstBytes[0] &&
                    payload[i + 1] == firstBytes[1] &&
                    payload[i + 2] == firstBytes[2] &&
                    payload[i + 3] == firstBytes[3] &&
                    payload[i + 4] == secondBytes[0] &&
                    payload[i + 5] == secondBytes[1] &&
                    payload[i + 6] == secondBytes[2] &&
                    payload[i + 7] == secondBytes[3])
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindLittleEndianByteSequenceOffset(byte[] payload, int bytesWritten, byte[] marker)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            Assert.IsNotNull(marker);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            for (int i = 0; i <= safeLength - marker.Length; i++)
            {
                if (ByteSequenceMatches(payload, i, marker))
                    return i;
            }

            return -1;
        }

        private static int PatchLittleEndianFloatSequence(
            byte[] payload,
            int bytesWritten,
            float[] marker,
            float[] replacement)
        {
            Assert.IsNotNull(marker);
            Assert.IsNotNull(replacement);
            Assert.AreEqual(marker.Length, replacement.Length);

            byte[] markerBytes = BuildLittleEndianFloatBytes(marker);
            byte[] replacementBytes = BuildLittleEndianFloatBytes(replacement);
            int safeLength = Math.Clamp(bytesWritten, 0, payload != null ? payload.Length : 0);
            int patchedCount = 0;

            for (int i = 0; i <= safeLength - markerBytes.Length; i++)
            {
                if (!ByteSequenceMatches(payload, i, markerBytes))
                    continue;

                Buffer.BlockCopy(replacementBytes, 0, payload, i, replacementBytes.Length);
                patchedCount++;
                i += markerBytes.Length - 1;
            }

            return patchedCount;
        }

        private static byte[] BuildLittleEndianFloatBytes(float[] values)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[values.Length * sizeof(float)];
            for (int i = 0; i < values.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(values[i]), 0, bytes, i * sizeof(float), sizeof(float));
            }

            return bytes;
        }

        private static byte[] BuildLittleEndianRadiationGridHeader(
            float dose,
            double originX,
            double originY,
            double originZ,
            float cellSizeMeters)
        {
            Assert.IsTrue(BitConverter.IsLittleEndian);
            byte[] bytes = new byte[sizeof(float) + (sizeof(double) * 3) + sizeof(float)];
            int cursor = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(dose), 0, bytes, cursor, sizeof(float));
            cursor += sizeof(float);
            Buffer.BlockCopy(BitConverter.GetBytes(originX), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(originY), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(originZ), 0, bytes, cursor, sizeof(double));
            cursor += sizeof(double);
            Buffer.BlockCopy(BitConverter.GetBytes(cellSizeMeters), 0, bytes, cursor, sizeof(float));
            return bytes;
        }

        private static bool ByteSequenceMatches(byte[] payload, int offset, byte[] marker)
        {
            if (payload == null || marker == null || offset < 0 || offset + marker.Length > payload.Length)
                return false;

            for (int i = 0; i < marker.Length; i++)
            {
                if (payload[offset + i] != marker[i])
                    return false;
            }

            return true;
        }
    }
}
