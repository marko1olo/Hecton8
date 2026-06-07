using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory.Layout;
using Hecton8.Gameplay;
using Hecton8.Inventory;
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
            Assert.AreEqual(21, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad0)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad1)));
            Assert.AreEqual(23, (int)Marshal.OffsetOf<SaveEventPayload>(nameof(SaveEventPayload._pad2)));
        }

        [Test]
        public void SaveManagerWfcDirtySignalDrain_UsesOwnerScratchInsteadOfLargeStackalloc()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/SaveManager.cs"));

            StringAssert.Contains("private readonly ulong[] _wfcDirtySectorScratch", source);
            StringAssert.Contains("private readonly ushort[] _wfcDirtyCellIndexScratch", source);
            StringAssert.Contains("private readonly byte[] _wfcDirtyCellFlagScratch", source);
            StringAssert.Contains("_wfcDirtySectorScratch.AsSpan(0, signals.Length)", source);
            Assert.IsFalse(source.Contains("stackalloc ulong[MaxWfcDirtySectorStackEntries]"));
            Assert.IsFalse(source.Contains("stackalloc ushort[MaxWfcDirtySectorStackEntries]"));
            Assert.IsFalse(source.Contains("stackalloc byte[MaxWfcDirtySectorStackEntries]"));
        }

        [Test]
        public void SaveSidecarStorage_RespectsConfiguredPersistentRoot()
        {
            string previousRoot = Hecton8.Core.HectonPersistentPathPolicy.RootPath;
            string tempRoot = Path.Combine(Path.GetTempPath(), "H8SidecarRoot_" + Guid.NewGuid().ToString("N"));
            string relativePath = Path.Combine("nested", "slot.meta");
            string expectedPath = Path.Combine(tempRoot, relativePath);

            try
            {
                Directory.CreateDirectory(tempRoot);
                SaveSidecarStorage.SetPersistentDataPathRoot(tempRoot);

                SaveMetadata metadata = SaveMetadata.CreateFallback("slot", 123456789L);
                metadata.GameVersion = "root-override-test";
                metadata.SceneName = "RootOverrideScene";
                metadata.PlayTimeSeconds = 42.5f;
                metadata.Checksum = "abc123";
                metadata.WorldSeed = 8192;
                metadata.WorldGenerationVersionId = 7;

                Assert.IsTrue(SaveSidecarStorage.SaveMetadata(metadata, relativePath, out string saveError), saveError);
                Assert.IsTrue(File.Exists(expectedPath), expectedPath);
                Assert.IsTrue(SaveSidecarStorage.LoadMetadata(relativePath, out SaveMetadata loaded, out string loadError), loadError);
                Assert.AreEqual(metadata.SlotName, loaded.SlotName);
                Assert.AreEqual(metadata.GameVersion, loaded.GameVersion);
                Assert.AreEqual(metadata.SceneName, loaded.SceneName);
                Assert.AreEqual(metadata.PlayTimeSeconds, loaded.PlayTimeSeconds);
                Assert.AreEqual(metadata.Checksum, loaded.Checksum);
                Assert.AreEqual(metadata.WorldSeed, loaded.WorldSeed);
                Assert.AreEqual(metadata.WorldGenerationVersionId, loaded.WorldGenerationVersionId);
            }
            finally
            {
                SaveSidecarStorage.SetPersistentDataPathRoot(previousRoot);
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
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
        public void HazardZoneTelemetryEntry_IsExplicitSixtyFourBytes()
        {
            StructLayoutAttribute layout = typeof(HazardZoneTelemetryEntry).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardZoneTelemetryEntry), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HazardZoneTelemetryEntry>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardZoneTelemetryEntry>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PackedOwner)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.FrameIndex)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.Sequence)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.StateHash)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.Flags)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ActiveZoneCount)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PendingMutationCount)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PublishedExposureMask)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.BufferGeneration)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ToxicityDose)));
            Assert.AreEqual(44, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.ToxicityPulseAccumulatorSeconds)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PlayerToxicity)));
            Assert.AreEqual(52, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.VehicleToxicity)));
            Assert.AreEqual(56, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.PlayerRadiation)));
            Assert.AreEqual(60, (int)Marshal.OffsetOf<HazardZoneTelemetryEntry>(nameof(HazardZoneTelemetryEntry.VehicleRadiation)));
        }

        [Test]
        public void HazardVolumeData_IsExplicitSixtyFourBytes()
        {
            StructLayoutAttribute layout = typeof(HazardVolumeData).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardVolumeData), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HazardVolumeData>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardVolumeData>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.AbsoluteUniversePosition)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Radius)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.InvRadius)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.InvRadiusSqr)));
            Assert.AreEqual(36, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Intensity)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.VisorGlitchBias)));
            Assert.AreEqual(44, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.CurveLutOffset)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.Type)));
            Assert.AreEqual(52, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.RequiresToxicMudBroadphase)));
            Assert.AreEqual(53, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.PlayerToxicMudBroadphase)));
            Assert.AreEqual(54, (int)Marshal.OffsetOf<HazardVolumeData>(nameof(HazardVolumeData.VehicleToxicMudBroadphase)));
        }

        [Test]
        public void HazardExposureJobResult_IsExplicitOneHundredTwentyEightBytes()
        {
            StructLayoutAttribute layout = typeof(HazardExposureJobResult).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.IsTrue(Attribute.IsDefined(typeof(HazardExposureJobResult), typeof(BinaryBlittableSafeAttribute)));
            Assert.AreEqual(128, UnsafeUtility.SizeOf<HazardExposureJobResult>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<HazardExposureJobResult>() & 15);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerRadiation)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerToxicity)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleRadiation)));
            Assert.AreEqual(40, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleToxicity)));
            Assert.AreEqual(64, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.PlayerExposureMask)));
            Assert.AreEqual(65, (int)Marshal.OffsetOf<HazardExposureJobResult>(nameof(HazardExposureJobResult.VehicleExposureMask)));
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
        public void HazardZoneRuntime_WriteClearsInactivePulseBelowDamageThreshold()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.hazardZones.toxicityDose = SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f;
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
                Assert.AreEqual(data.hazardZones.toxicityDose, restored.hazardZones.toxicityDose);
                Assert.AreEqual(0f, restored.hazardZones.toxicityPulseAccumulatorSeconds);
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
            Assert.AreEqual(4f, SaveData.RadiationGridDefaultCellSizeMeters);
            Assert.AreEqual(0.5f, SaveData.RadiationGridMinCellSizeMeters);
            Assert.AreEqual(1000f, SaveData.RadiationGridMaxCellSizeMeters);

            SaveData data = SaveData.CreateNew(0.0);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            Assert.IsFalse(RadiationHazardGrid.HasPersistedRadiationGridPayload(null, SaveData.RadiationGridRleMaxBytes));
            Assert.IsFalse(RadiationHazardGrid.HasPersistedRadiationGridPayload(
                new byte[SaveData.RadiationGridRlePacketSizeBytes - 1],
                SaveData.RadiationGridRlePacketSizeBytes - 1));
            Assert.IsTrue(RadiationHazardGrid.HasPersistedRadiationGridPayload(
                new byte[SaveData.RadiationGridRlePacketSizeBytes],
                SaveData.RadiationGridRlePacketSizeBytes));
        }

        [Test]
        public void RadiationGridLoad_ClearsTransientStateWithoutDroppingRegisteredSources()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));

            Assert.IsFalse(source.Contains("ClearRadiationSourcesForLoad"));
            StringAssert.Contains("ClearGrid(_gridRead);", source);
            StringAssert.Contains("ClearGrid(_gridWrite);", source);
            StringAssert.Contains("ClearGrid(_gridSource);", source);
            StringAssert.Contains("RepairRadiationSourceCountFromBuffer();", source);
            StringAssert.Contains("private void RepairRadiationSourceCountFromBuffer()", source);
            StringAssert.Contains("_hasGridOrigin = false;", source);
            StringAssert.Contains("RestoreGridOriginFromActiveSourceOrDefault();", source);
            StringAssert.Contains("TryResolveFirstActiveRadiationSourceOrigin", source);
            StringAssert.Contains("_lastExternalIntensity01 = 0f;", source);
            StringAssert.Contains("_lastSourceSignalDrainFrame = -1;", source);
            StringAssert.Contains("_lastExternalDoseSignalDrainFrame = -1;", source);
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
        public void SaveRootTime_WriteSanitizesNonFiniteSessionValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.totalPlayTime = double.NaN;
            data.firstHourSessionTime = float.PositiveInfinity;
            data.corporatePendingOrderIds.Add("order.a");
            data.corporatePendingOrderTimers.Add(float.NegativeInfinity);

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
                Assert.AreEqual(0d, restored.totalPlayTime);
                Assert.AreEqual(0f, restored.firstHourSessionTime);
                Assert.AreEqual(1, restored.corporatePendingOrderTimers.Count);
                Assert.AreEqual(0f, restored.corporatePendingOrderTimers[0]);
            }
        }

        [Test]
        public void ToolDurabilityRuntime_WriteSanitizesNonFiniteDurabilityValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.toolDurabilityMap["tool.nan"] = float.NaN;
            data.toolDurabilityMap["tool.negative"] = -12.5f;
            data.toolDurabilityMap["tool.ok"] = 42.5f;

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
                Assert.AreEqual(0f, restored.toolDurabilityMap["tool.nan"]);
                Assert.AreEqual(0f, restored.toolDurabilityMap["tool.negative"]);
                Assert.AreEqual(42.5f, restored.toolDurabilityMap["tool.ok"]);
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
        public void SaveRootTimeMigration_CurrentRepairsNonFiniteSessionValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.totalPlayTime = double.NegativeInfinity;
            data.firstHourSessionTime = float.NaN;
            data.corporatePendingOrderIds.Add("order.a");
            data.corporatePendingOrderIds.Add("order.b");
            data.corporatePendingOrderTimers.Add(-1f);
            data.corporatePendingOrderTimers.Add(float.PositiveInfinity);

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0d, data.totalPlayTime);
            Assert.AreEqual(0f, data.firstHourSessionTime);
            Assert.AreEqual(2, data.corporatePendingOrderTimers.Count);
            Assert.AreEqual(0f, data.corporatePendingOrderTimers[0]);
            Assert.AreEqual(0f, data.corporatePendingOrderTimers[1]);
            StringAssert.Contains("total play time repaired", summary);
            StringAssert.Contains("first hour session time repaired", summary);
            StringAssert.Contains("corporate order timers repaired", summary);
        }

        [Test]
        public void ToolDurabilityMigration_CurrentRepairsNonFiniteDurabilityValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.toolDurabilityMap["tool.inf"] = float.PositiveInfinity;
            data.toolDurabilityMap["tool.negative"] = -0.25f;
            data.toolDurabilityMap["tool.ok"] = 13.75f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.toolDurabilityMap["tool.inf"]);
            Assert.AreEqual(0f, data.toolDurabilityMap["tool.negative"]);
            Assert.AreEqual(13.75f, data.toolDurabilityMap["tool.ok"]);
            StringAssert.Contains("tool durability values repaired", summary);
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
        public void RadiationGridRuntimeMigration_PreV68DropsUnpersistedGrid()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.RadiationGridPersistenceVersion - 1;
            data.radiationDose = 7.25f;
            data.radiationGridOriginX = 101.25d;
            data.radiationGridOriginY = -202.5d;
            data.radiationGridOriginZ = 303.75d;
            data.radiationGridCellSizeMeters = 6.5f;
            data.radiationGridRleLength = SaveData.RadiationGridRlePacketSizeBytes;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.RadiationGridPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.radiationDose);
            Assert.AreEqual(0d, data.radiationGridOriginX);
            Assert.AreEqual(0d, data.radiationGridOriginY);
            Assert.AreEqual(0d, data.radiationGridOriginZ);
            Assert.AreEqual(4f, data.radiationGridCellSizeMeters);
            Assert.AreEqual(0, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void RadiationGridRuntimeMigration_V68ClampsNonFiniteAndOversizedPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.RadiationGridPersistenceVersion;
            data.radiationDose = float.NaN;
            data.radiationGridOriginX = double.PositiveInfinity;
            data.radiationGridOriginY = double.NaN;
            data.radiationGridOriginZ = double.NegativeInfinity;
            data.radiationGridCellSizeMeters = float.NaN;
            data.radiationGridRle = new byte[SaveData.RadiationGridRleMaxBytes + 16];
            data.radiationGridRleLength = SaveData.RadiationGridRleMaxBytes + 16;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.RadiationGridPersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.radiationDose);
            Assert.AreEqual(0d, data.radiationGridOriginX);
            Assert.AreEqual(0d, data.radiationGridOriginY);
            Assert.AreEqual(0d, data.radiationGridOriginZ);
            Assert.AreEqual(4f, data.radiationGridCellSizeMeters);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void RadiationGridRuntimeMigration_CurrentRepairsMissingPayloadBuffer()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.radiationGridRle = null;
            data.radiationGridRleLength = SaveData.RadiationGridRlePacketSizeBytes;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0, data.radiationGridRleLength);
            Assert.IsNotNull(data.radiationGridRle);
            Assert.AreEqual(SaveData.RadiationGridRleMaxBytes, data.radiationGridRle.Length);
            StringAssert.Contains("radiation grid state repaired", summary);
        }

        [Test]
        public void InventoryRuntime_WriteSanitizesMalformedInventoryState()
        {
            Assert.AreEqual(
                (byte)(
                    PlayerInventory.ItemGeneticFlags.Glow |
                    PlayerInventory.ItemGeneticFlags.Toxic |
                    PlayerInventory.ItemGeneticFlags.Edible |
                    PlayerInventory.ItemGeneticFlags.Harvestable),
                SaveData.InventoryItemGeneticsSupportedFlagsMask);
            Assert.AreEqual(1000, SaveData.InventoryDefaultQualityMilli);

            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 12345;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(2, 3);
            data.inventory.stackCounts[0] = 0;
            data.inventory.itemGeneticsWords[0] = 0xFF;
            data.inventory.qualityMilli[0] = 2000;
            data.inventory.totalWeight = float.NaN;
            data.inventory.gridColumns = -5;
            data.inventory.gridRows = InventoryDTO.MaxCells + 100;
            data.inventory.itemDurabilityRle = new byte[InventoryDTO.MaxDurabilityRleBytes + 16];
            data.inventory.itemDurabilityRleLength = data.inventory.itemDurabilityRle.Length;

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
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(1, restored.inventory.stackCounts[0]);
                Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, restored.inventory.qualityMilli[0]);
                Assert.AreEqual(SaveData.InventoryItemGeneticsSupportedFlagsMask, restored.inventory.itemGeneticsWords[0]);
                Assert.AreEqual(0f, restored.inventory.totalWeight);
                Assert.AreEqual(0, restored.inventory.gridColumns);
                Assert.AreEqual(InventoryDTO.MaxCells, restored.inventory.gridRows);
                Assert.AreEqual(InventoryDTO.MaxDurabilityRleBytes, restored.inventory.itemDurabilityRleLength);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(InventoryDTO.MaxCells, restored.inventoryShadow.gridRows);
                Assert.AreEqual(0f, restored.inventoryShadow.totalWeight);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(InventoryShadowDTO.SchemaVersion, restored.inventoryShadow.schemaVersion);
            }
        }

        [Test]
        public void InventoryRuntime_WriteFallsBackFromOversizedShadowPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 12345;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(4, 5);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 10;
            data.inventory.gridRows = 8;

            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[SaveData.InventoryShadowPayloadMaxBytes + 1];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xA5A5A5A5u;

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
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(12345, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(2, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(10, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(8, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryRuntime_WriteFallsBackFromTruncatedShadowPayload()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 67890;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(3, 4);
            data.inventory.stackCounts[0] = 4;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 9;
            data.inventory.gridRows = 7;

            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[1];
            data.inventoryShadowPayloadLength = SaveData.InventoryShadowPayloadMaxBytes;
            data.inventoryShadowPayloadHash = 0xC0FFEEu;

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
                Assert.AreEqual(1, restored.inventory.cellCount);
                Assert.AreEqual(67890, restored.inventory.itemHashIds[0]);
                Assert.AreEqual(4, restored.inventory.stackCounts[0]);
                Assert.AreEqual(0, restored.inventoryShadow.payloadLength);
                Assert.AreEqual(0u, restored.inventoryShadow.payloadHash);
                Assert.AreEqual(0, restored.inventoryShadow.flags);
                Assert.AreEqual(1, restored.inventoryShadow.cellCount);
                Assert.AreEqual(9, restored.inventoryShadow.gridColumns);
                Assert.AreEqual(7, restored.inventoryShadow.gridRows);
            }
        }

        [Test]
        public void InventoryShadowPayloadBudget_CoversWorstCaseInventoryDto()
        {
            long worstCaseBytes =
                sizeof(int) +
                EncodedStructArrayBytes<int>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<uint>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<byte>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<ushort>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<uint>(InventoryDTO.MaxCells) +
                EncodedStructArrayBytes<byte>(InventoryDTO.MaxDurabilityRleBytes) +
                sizeof(float) +
                sizeof(int) +
                sizeof(int);

            Assert.LessOrEqual(worstCaseBytes, SaveData.InventoryShadowPayloadMaxBytes);
            Assert.AreEqual(16 * 1024, SaveData.InventoryShadowPayloadMaxBytes);
        }

        [Test]
        public void InventoryShadowBuilder_DoesNotMutateInventoryArrays()
        {
            InventoryDTO inventory = default;
            inventory.EnsureCapacity();
            inventory.cellCount = 1;
            inventory.itemHashIds[0] = 123;
            inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 1);
            inventory.stackCounts[0] = 0;
            inventory.qualityMilli[0] = 2000;
            inventory.itemGeneticsWords[0] = 0xF0;
            inventory.totalWeight = float.NaN;
            inventory.gridColumns = -1;
            inventory.gridRows = InventoryDTO.MaxCells + 1;

            InventoryShadowDTO shadow = SaveDataInventorySanitizer.BuildInventoryShadow(
                in inventory,
                12,
                0x12345678u,
                true);

            Assert.AreEqual(1, shadow.cellCount);
            Assert.AreEqual(0f, shadow.totalWeight);
            Assert.AreEqual(0, shadow.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, shadow.gridRows);
            Assert.AreEqual(12, shadow.payloadLength);
            Assert.AreEqual(0x12345678u, shadow.payloadHash);
            Assert.AreEqual(0, inventory.stackCounts[0]);
            Assert.AreEqual(2000, inventory.qualityMilli[0]);
            Assert.AreEqual(0xF0, inventory.itemGeneticsWords[0]);
            Assert.IsTrue(float.IsNaN(inventory.totalWeight));
            Assert.AreEqual(-1, inventory.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells + 1, inventory.gridRows);
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentRepairsMalformedInventoryState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.cellCount = 4;
            data.inventory.itemHashIds = new[] { 101, 202 };
            data.inventory.packedCellCoordinates = new[]
            {
                InventoryDTO.PackCellCoordinate(1, 1),
                InventoryDTO.PackCellCoordinate(2, 2)
            };
            data.inventory.stackCounts = new ushort[] { 0, 3 };
            data.inventory.itemStateFlags = new ushort[] { 0, 0 };
            data.inventory.itemGeneticsWords = new byte[] { 0xFF, 0x10 };
            data.inventory.qualityMilli = new ushort[] { 0, 2000 };
            data.inventory.lastUpdateUnixSeconds = new uint[] { 0u, 123u };
            data.inventory.itemDurabilityRle = new byte[] { 11, 22 };
            data.inventory.itemDurabilityRleLength = 99;
            data.inventory.totalWeight = float.NegativeInfinity;
            data.inventory.gridColumns = -1;
            data.inventory.gridRows = InventoryDTO.MaxCells + 1;
            data.inventoryShadow.cellCount = 99;
            data.inventoryShadow.payloadLength = int.MaxValue;
            data.inventoryShadow.payloadHash = 0x12345678u;
            data.inventoryShadow.gridColumns = 99;
            data.inventoryShadow.gridRows = -99;
            data.inventoryShadow.totalWeight = float.NaN;
            data.inventoryShadow.flags = InventoryShadowDTO.FlagHasPayload;
            data.inventoryShadow.schemaVersion = 0;
            data.inventoryShadow.reserved0 = 123;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[1];
            data.inventoryShadowPayloadLength = SaveData.InventoryShadowPayloadMaxBytes;
            data.inventoryShadowPayloadHash = 0x87654321u;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(2, data.inventory.cellCount);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.itemHashIds.Length);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.stackCounts.Length);
            Assert.AreEqual(InventoryDTO.MaxDurabilityRleBytes, data.inventory.itemDurabilityRle.Length);
            Assert.AreEqual(1, data.inventory.stackCounts[0]);
            Assert.AreEqual(3, data.inventory.stackCounts[1]);
            Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, data.inventory.qualityMilli[0]);
            Assert.AreEqual(SaveData.InventoryDefaultQualityMilli, data.inventory.qualityMilli[1]);
            Assert.AreEqual(SaveData.InventoryItemGeneticsSupportedFlagsMask, data.inventory.itemGeneticsWords[0]);
            Assert.AreEqual(0x00, data.inventory.itemGeneticsWords[1]);
            Assert.AreEqual(2, data.inventory.itemDurabilityRleLength);
            Assert.AreEqual(0f, data.inventory.totalWeight);
            Assert.AreEqual(0, data.inventory.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventory.gridRows);
            Assert.AreEqual(2, data.inventoryShadow.cellCount);
            Assert.AreEqual(0, data.inventoryShadow.payloadLength);
            Assert.AreEqual(0u, data.inventoryShadow.payloadHash);
            Assert.AreEqual(0, data.inventoryShadow.gridColumns);
            Assert.AreEqual(InventoryDTO.MaxCells, data.inventoryShadow.gridRows);
            Assert.AreEqual(0f, data.inventoryShadow.totalWeight);
            Assert.AreEqual(0, data.inventoryShadow.flags);
            Assert.AreEqual(InventoryShadowDTO.SchemaVersion, data.inventoryShadow.schemaVersion);
            Assert.AreEqual(0, data.inventoryShadow.reserved0);
            Assert.IsFalse(data.hasInventoryShadowPayload);
            Assert.AreEqual(0, data.inventoryShadowPayloadLength);
            Assert.AreEqual(0u, data.inventoryShadowPayloadHash);
            StringAssert.Contains("inventory state repaired", summary);
            StringAssert.Contains("inventory shadow repaired", summary);
        }

        [Test]
        public void InventoryRuntimeMigration_CurrentPreservesBoundedShadowPayloadMetadata()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.inventory.EnsureCapacity();
            data.inventory.cellCount = 1;
            data.inventory.itemHashIds[0] = 31337;
            data.inventory.packedCellCoordinates[0] = InventoryDTO.PackCellCoordinate(1, 2);
            data.inventory.stackCounts[0] = 2;
            data.inventory.qualityMilli[0] = SaveData.InventoryDefaultQualityMilli;
            data.inventory.gridColumns = 4;
            data.inventory.gridRows = 3;
            data.hasInventoryShadowPayload = true;
            data.inventoryShadowPayload = new byte[64];
            data.inventoryShadowPayloadLength = data.inventoryShadowPayload.Length;
            data.inventoryShadowPayloadHash = 0xBADC0DEu;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(1, data.inventoryShadow.cellCount);
            Assert.AreEqual(64, data.inventoryShadow.payloadLength);
            Assert.AreEqual(0xBADC0DEu, data.inventoryShadow.payloadHash);
            Assert.AreEqual(4, data.inventoryShadow.gridColumns);
            Assert.AreEqual(3, data.inventoryShadow.gridRows);
            Assert.AreEqual(InventoryShadowDTO.FlagHasPayload, data.inventoryShadow.flags);
            Assert.AreEqual(InventoryShadowDTO.SchemaVersion, data.inventoryShadow.schemaVersion);
            StringAssert.Contains("inventory shadow repaired", summary);
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
        public void PlayerStatsRuntimeMigration_CurrentClampsNonFiniteResourceValues()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.oxygen = float.NaN;
            data.playerStats.energy = float.PositiveInfinity;
            data.playerStats.integrity = float.NegativeInfinity;
            data.playerStats.weight = float.NaN;
            data.playerStats.hunger = float.PositiveInfinity;
            data.playerStats.thirst = float.NegativeInfinity;
            data.playerStats.currentLifeLowestOxygenNormalized = float.NaN;
            data.playerStats.currentLifeLowestEnergyNormalized = float.PositiveInfinity;
            data.playerStats.currentLifeLowestIntegrityNormalized = float.NegativeInfinity;
            data.playerStats.nitrogenBuildUp = SaveData.PlayerStatsNitrogenBuildUpHardCap * 2f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(0f, data.playerStats.oxygen);
            Assert.AreEqual(0f, data.playerStats.energy);
            Assert.AreEqual(0f, data.playerStats.integrity);
            Assert.AreEqual(0f, data.playerStats.weight);
            Assert.AreEqual(0f, data.playerStats.hunger);
            Assert.AreEqual(0f, data.playerStats.thirst);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestOxygenNormalized);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestEnergyNormalized);
            Assert.AreEqual(1f, data.playerStats.currentLifeLowestIntegrityNormalized);
            Assert.AreEqual(SaveData.PlayerStatsNitrogenBuildUpHardCap, data.playerStats.nitrogenBuildUp);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerKinematicRuntimeMigration_CurrentUsesDedicatedKinematicState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.CurrentVersion;
            data.playerStats.posX = 100f;
            data.playerStats.posY = 200f;
            data.playerStats.posZ = 300f;
            data.playerKinematicState.posX = 12.25f;
            data.playerKinematicState.posY = 23.5f;
            data.playerKinematicState.posZ = 34.75f;
            data.playerKinematicState.rotX = 0f;
            data.playerKinematicState.rotY = 0f;
            data.playerKinematicState.rotZ = 0f;
            data.playerKinematicState.rotW = 0f;
            data.playerKinematicState.velX = 160f;
            data.playerKinematicState.velY = 0f;
            data.playerKinematicState.velZ = 0f;
            data.playerKinematicState.flags = 7;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.CurrentVersion, originalVersion);
            Assert.AreEqual(12.25f, data.playerKinematicState.posX);
            Assert.AreEqual(23.5f, data.playerKinematicState.posY);
            Assert.AreEqual(34.75f, data.playerKinematicState.posZ);
            Assert.AreEqual(0f, data.playerKinematicState.rotX);
            Assert.AreEqual(0f, data.playerKinematicState.rotY);
            Assert.AreEqual(0f, data.playerKinematicState.rotZ);
            Assert.AreEqual(1f, data.playerKinematicState.rotW);
            Assert.AreEqual(SaveData.PlayerKinematicVelocityHardCapMetersPerSecond, data.playerKinematicState.velX, 0.0001f);
            Assert.AreEqual(0f, data.playerKinematicState.velY);
            Assert.AreEqual(0f, data.playerKinematicState.velZ);
            Assert.AreEqual(7, data.playerKinematicState.flags);
            Assert.AreEqual(data.playerKinematicState.posX, data.playerStats.posX);
            Assert.AreEqual(data.playerKinematicState.posY, data.playerStats.posY);
            Assert.AreEqual(data.playerKinematicState.posZ, data.playerStats.posZ);
            Assert.AreEqual(data.playerKinematicState.rotW, data.playerStats.rotW);
            Assert.AreEqual(data.playerKinematicState.velX, data.playerStats.velX, 0.0001f);
            StringAssert.Contains("player survival state repaired", summary);
        }

        [Test]
        public void PlayerKinematicRuntimeMigration_PreV72CopiesLegacyStatsToKinematicState()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.FirstHourDtoLockPersistenceVersion - 1;
            data.playerStats.posX = 44.25f;
            data.playerStats.posY = 55.5f;
            data.playerStats.posZ = 66.75f;
            data.playerStats.rotX = 0.5f;
            data.playerStats.rotY = 0.5f;
            data.playerStats.rotZ = 0.5f;
            data.playerStats.rotW = 0.5f;
            data.playerStats.velX = 6f;
            data.playerStats.velY = 7f;
            data.playerStats.velZ = 8f;
            data.playerKinematicState.posX = 999f;
            data.playerKinematicState.posY = 999f;
            data.playerKinematicState.posZ = 999f;
            data.playerKinematicState.flags = 99;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.FirstHourDtoLockPersistenceVersion - 1, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(44.25f, data.playerKinematicState.posX);
            Assert.AreEqual(55.5f, data.playerKinematicState.posY);
            Assert.AreEqual(66.75f, data.playerKinematicState.posZ);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotX);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotY);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotZ);
            Assert.AreEqual(0.5f, data.playerKinematicState.rotW);
            Assert.AreEqual(6f, data.playerKinematicState.velX);
            Assert.AreEqual(7f, data.playerKinematicState.velY);
            Assert.AreEqual(8f, data.playerKinematicState.velZ);
            Assert.AreEqual(1, data.playerKinematicState.flags);
            Assert.AreEqual(data.playerKinematicState.posX, data.playerStats.posX);
            Assert.AreEqual(data.playerKinematicState.posY, data.playerStats.posY);
            Assert.AreEqual(data.playerKinematicState.posZ, data.playerStats.posZ);
            StringAssert.Contains("player survival state repaired", summary);
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

        [Test]
        public void HazardZoneRuntimeMigration_V74ClearsInactivePulseBelowDamageThreshold()
        {
            SaveData data = SaveData.CreateNew(0.0);
            data.version = SaveData.HazardZoneRuntimePersistenceVersion;
            data.hazardZones.toxicityDose = SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f;
            data.hazardZones.toxicityPulseAccumulatorSeconds = 0.25f;

            bool changed = SaveDataMigration.MigrateInPlace(data, out int originalVersion, out string summary);

            Assert.IsTrue(changed, summary);
            Assert.AreEqual(SaveData.HazardZoneRuntimePersistenceVersion, originalVersion);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(SaveData.HazardZoneToxicityDamageDoseThreshold * 0.5f, data.hazardZones.toxicityDose);
            Assert.AreEqual(0f, data.hazardZones.toxicityPulseAccumulatorSeconds);
            StringAssert.Contains("hazard zone toxicity state repaired", summary);
        }

        private static long EncodedStructArrayBytes<T>(int count) where T : unmanaged
        {
            return sizeof(int) + (long)Math.Clamp(count, 0, int.MaxValue) * UnsafeUtility.SizeOf<T>();
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
