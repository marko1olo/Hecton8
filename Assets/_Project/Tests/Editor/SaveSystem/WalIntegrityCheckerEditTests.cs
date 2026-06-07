using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.SaveSystem;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class WalIntegrityCheckerEditTests
    {
        [Test]
        public void EntityDeltaHeaderDTO_IsExplicitAndArm64Aligned()
        {
            StructLayoutAttribute layout = typeof(EntityDeltaHeaderDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<EntityDeltaHeaderDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.SectorHash)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.CompressedSize)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.UncompressedSize)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO.XXHash3Checksum)));
            Assert.AreEqual(24, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>("_pad0"));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>("_pad1"));
        }

        [Test]
        public void WalFuzzStateDTO_IsExplicit32AndArm64Aligned()
        {
            StructLayoutAttribute layout = typeof(WalFuzzStateDTO).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<WalFuzzStateDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<WalFuzzStateDTO>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.InterruptedByteOffset)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.FinalValidatedBytes)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<WalFuzzStateDTO>(nameof(WalFuzzStateDTO.MismatchFlags)));
        }

        [Test]
        public void Shinobu357TelemetryAndHandleDTOs_AreCacheLineAligned()
        {
            StructLayoutAttribute telemetryLayout = typeof(WalFuzzTelemetryEntry).StructLayoutAttribute;
            StructLayoutAttribute handleLayout = typeof(WalFuzzFileHandleStatusDTO).StructLayoutAttribute;

            Assert.IsNotNull(telemetryLayout);
            Assert.IsNotNull(handleLayout);
            Assert.AreEqual(LayoutKind.Explicit, telemetryLayout.Value);
            Assert.AreEqual(LayoutKind.Explicit, handleLayout.Value);
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WalFuzzTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<WalFuzzFileHandleStatusDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<WalFuzzTelemetryEntry>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<WalFuzzFileHandleStatusDTO>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<WalFuzzTelemetryEntry>() & 63);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<WalFuzzFileHandleStatusDTO>() & 63);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<WalFuzzTelemetryEntry>(nameof(WalFuzzTelemetryEntry.Frame)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<WalFuzzTelemetryEntry>(nameof(WalFuzzTelemetryEntry.PathHash)));
            Assert.AreEqual(0, (int)Marshal.OffsetOf<WalFuzzFileHandleStatusDTO>(nameof(WalFuzzFileHandleStatusDTO.PrimaryWritable)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<WalFuzzFileHandleStatusDTO>(nameof(WalFuzzFileHandleStatusDTO.FailureCode)));
        }

        [Test]
        public void Shinobu357DefaultProfile_UsesHundredIterationsAndTenMegabytePayload()
        {
            WalFuzzerProfileDTO profile = WalIntegrityFuzzerCore.BuildShinobu357DefaultProfile();

            Assert.AreEqual(10u * 1024u * 1024u, profile.PayloadBytes);
            Assert.AreEqual(100u, profile.LoopIterations);
            Assert.AreEqual(1f, profile.GlobalQualityWeight);
            Assert.AreEqual(1u, profile.EnforceZeroGcLoop);
        }

        [Test]
        public void SaveRuntimeRoute_RejectsManagedTextSerializers()
        {
            string root = Path.Combine(Application.dataPath, "_Project/Scripts");
            string unityTextToken = BuildForbiddenSerializerToken(0);
            string formatterToken = BuildForbiddenSerializerToken(1);
            AssertNoToken(Path.Combine(root, "SaveManager.cs"), unityTextToken);
            AssertNoToken(Path.Combine(root, "SaveManager.cs"), formatterToken);
            AssertNoToken(Path.Combine(root, "SaveBinaryStorage.cs"), unityTextToken);
            AssertNoToken(Path.Combine(root, "SaveBinaryStorage.cs"), formatterToken);
            AssertNoToken(Path.Combine(root, "SaveBinaryPayloadCodec.cs"), unityTextToken);
            AssertNoToken(Path.Combine(root, "SaveBinaryPayloadCodec.cs"), formatterToken);
            AssertNoToken(Path.Combine(root, "SaveSystem/EntityDeltaCompressionArchitecture.cs"), unityTextToken);
            AssertNoToken(Path.Combine(root, "SaveSystem/EntityDeltaCompressionArchitecture.cs"), formatterToken);
        }

        [Test]
        public void EntityDeltaTelemetryDumpUsesTrackedTransientPayload()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/SaveSystem/EntityDeltaCompressionArchitecture.cs");
            string source = File.ReadAllText(path);

            StringAssert.Contains("private const string TelemetryDumpPayloadLabel = \"entityDeltaTelemetryDumpPayload\";", source);
            StringAssert.Contains("NativeFaultDumpWriter.CreateTransientPayload(", source);
            StringAssert.Contains("nameof(EntityDeltaCompressionArchitecture)", source);
            StringAssert.Contains("TelemetryDumpPayloadLabel", source);
            StringAssert.Contains("NativeArrayOptions.UninitializedMemory", source);
            StringAssert.Contains("NativeFaultDumpWriter.DisposeTransientPayload(", source);
            StringAssert.DoesNotContain("new NativeArray<byte>(", source);
            StringAssert.DoesNotContain("payload.Dispose()", source);
        }

        [Test]
        public void HeadlessWalFuzzer_CorruptedPrimaryPromotesBackupAndValidatesHash()
        {
            WalFuzzerProfileDTO profile = WalIntegrityFuzzerCore.BuildDefaultProfile();
            profile.LoopPayloadBytes = 1024u;
            profile.LoopIterations = 1000u;
            profile.WriteReports = 1u;

            string root = Path.Combine(Application.temporaryCachePath, "H8_SHINOBU_256_WAL_TEST");
            bool passed = WalIntegrityFuzzerCore.RunProfile(root, in profile, out WalFuzzerResultDTO result);

            Assert.IsTrue(passed);
            Assert.AreEqual(0u, result.ErrorFlags);
            Assert.Greater(result.RecoveredBytes, 0u);
            Assert.AreEqual(result.TruthHash, result.RecoveredHash);
            Assert.AreEqual(result.RecoveredBytes, result.MerkleReplayBytes);
            Assert.Greater(result.MerkleBlockCount, 0u);
            Assert.AreEqual(1000u, result.LoopIterations);
            Assert.AreEqual(5000u, result.SectorCount);
            Assert.LessOrEqual(result.PagingBytesRead, 160L);
        }

        [Test]
        public void Shinobu357WalFuzzer_CorruptedPrimaryPromotesBackupAndValidatesHash()
        {
            WalFuzzerProfileDTO profile = WalIntegrityFuzzerCore.BuildShinobu357DefaultProfile();
            profile.PayloadBytes = 64u * 1024u;
            profile.LoopPayloadBytes = 1024u;
            profile.LoopIterations = 8u;
            profile.ChunkBytes = 4096u;
            profile.WriteReports = 0u;
            profile.StallThresholdMicros = 8000u;

            string root = Path.Combine(Application.temporaryCachePath, "H8_SHINOBU_357_WAL_TEST");
            bool passed = WalIntegrityFuzzerCore.RunShinobu357PersistenceIntegrityFuzzer(root, in profile, out WalFuzzStateDTO state, out WalFuzzerResultDTO result);

            Assert.IsTrue(passed);
            Assert.AreEqual(0u, state.MismatchFlags);
            Assert.AreEqual(0u, result.ErrorFlags);
            Assert.Greater(state.InterruptedByteOffset, 0u);
            Assert.Greater(state.FinalValidatedBytes, 0u);
            Assert.AreEqual(result.TruthHash, result.RecoveredHash);
            Assert.AreEqual(result.RecoveredBytes, state.FinalValidatedBytes);
        }

        [Test]
        public void OopWalFuzzScanner_RejectsManagedSerializerFindings()
        {
            bool passed = WalIntegrityFuzzerCore.RunOopWalFuzzScannerForProject(out OopWalFuzzScanResultDTO result);

            Assert.IsTrue(passed);
            Assert.Greater(result.FilesScanned, 0u);
            Assert.AreEqual(0u, result.FatalFindings);
            Assert.AreEqual(0u, result.StreamWriterFindings);
            Assert.AreEqual(0u, result.JsonUtilityFindings);
            Assert.AreEqual(0u, result.BinaryFormatterFindings);
        }

        [Test]
        public void ProfileCsvParser_AcceptsLegacyRowsWithoutQualityColumn()
        {
            string path = Path.Combine(Application.temporaryCachePath, "shinobu_256_legacy_profiles.csv");
            WriteLegacyProfileFixture(path);

            NativeArray<WalFuzzerProfileDTO> profiles = new NativeArray<WalFuzzerProfileDTO>(2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                bool parsed = WalIntegrityFuzzerCore.TryLoadShinobu357ProfilesCsv(path, profiles, out int count, out uint errorCode);
                Assert.IsTrue(parsed);
                Assert.AreEqual(2, count);
                Assert.AreEqual(1f, profiles[0].GlobalQualityWeight);
                Assert.AreEqual(1f, profiles[1].GlobalQualityWeight);
                Assert.AreEqual(1048576u, profiles[0].PayloadBytes);
                Assert.AreEqual(2097152u, profiles[1].PayloadBytes);
            }
            finally
            {
                if (profiles.IsCreated)
                    profiles.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void ProfileCsvParser_SaturatesOverflowingUnsignedFields()
        {
            string path = Path.Combine(Application.temporaryCachePath, "shinobu_256_overflow_profile.csv");
            WriteOverflowProfileFixture(path);

            NativeArray<WalFuzzerProfileDTO> profiles = new NativeArray<WalFuzzerProfileDTO>(1, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                bool parsed = WalIntegrityFuzzerCore.TryLoadProfilesCsv(path, profiles, out int count, out uint errorCode);
                Assert.IsTrue(parsed);
                Assert.AreEqual(1, count);
                Assert.AreEqual(uint.MaxValue, profiles[0].PayloadBytes);
                Assert.AreEqual(uint.MaxValue, profiles[0].LoopIterations);
                Assert.AreEqual(1000f, profiles[0].GlobalQualityWeight * 1000f);
            }
            finally
            {
                if (profiles.IsCreated)
                    profiles.Dispose();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        [Test]
        public void FailureDiagnostics_PreserveFirstFailureAfterLaterPhases()
        {
            WalFuzzerResultDTO result = WalIntegrityFuzzerCore.BuildFirstFailureDiagnosticRegressionResult();

            Assert.AreEqual(WalIntegrityFuzzerCore.BackupRecoveryFailure | WalIntegrityFuzzerCore.DataCorruptionFailure, result.ErrorFlags);
            Assert.AreEqual(3u, result.ErrorCode);
            Assert.AreEqual(HashAsciiForTest("local_wal"), result.PhaseHash);
            Assert.AreEqual(111L, result.CorruptionOffset);
        }

        private static void AssertNoToken(string path, string token)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            if (ContainsAsciiToken(stream, token))
                throw new global::Hecton8.Core.FatalArchitectureException(path + " contains forbidden persistence token " + token);
        }

        private static string BuildForbiddenSerializerToken(int index)
        {
            return index == 0
                ? string.Concat("Json", "Utility")
                : string.Concat("Binary", "Formatter");
        }

        private static uint HashAsciiForTest(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ (byte)value[i]) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static bool ContainsAsciiToken(FileStream stream, string token)
        {
            if (token.Length == 0)
                return false;

            Span<byte> tokenBytes = stackalloc byte[token.Length];
            for (int i = 0; i < token.Length; i++)
                tokenBytes[i] = (byte)token[i];

            Span<byte> buffer = stackalloc byte[4096];
            int matched = 0;
            while (true)
            {
                int read = stream.Read(buffer);
                if (read <= 0)
                    return false;

                for (int i = 0; i < read; i++)
                {
                    byte value = buffer[i];
                    if (value == tokenBytes[matched])
                    {
                        matched++;
                        if (matched == tokenBytes.Length)
                            return true;
                    }
                    else
                    {
                        matched = value == tokenBytes[0] ? 1 : 0;
                    }
                }
            }
        }

        private static void WriteLegacyProfileFixture(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "name,payload_bytes,loop_payload_bytes,loop_iterations,kill_percent,sector_count,chunk_bytes,stall_threshold_micros\n");
            WriteAscii(stream, "legacy_a,1048576,1024,1000,50,5000,4096,2000\n");
            WriteAscii(stream, "legacy_b,2097152,2048,1000,45,5000,4096,2000\n");
        }

        private static void WriteOverflowProfileFixture(string path)
        {
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            WriteAscii(stream, "name,payload_bytes,loop_payload_bytes,loop_iterations,kill_percent,sector_count,chunk_bytes,stall_threshold_micros,quality_per_mille\n");
            WriteAscii(stream, "overflow,999999999999999999999,1024,999999999999999999999,50,5000,4096,2000,1000\n");
        }

        private static void WriteAscii(FileStream stream, string value)
        {
            Span<byte> scratch = stackalloc byte[256];
            int cursor = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (cursor == scratch.Length)
                {
                    stream.Write(scratch.Slice(0, cursor));
                    cursor = 0;
                }

                scratch[cursor++] = (byte)(value[i] <= 127 ? value[i] : '?');
            }

            if (cursor > 0)
                stream.Write(scratch.Slice(0, cursor));
        }
    }
}
