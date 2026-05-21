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
            Assert.AreEqual(24, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO._pad0)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<EntityDeltaHeaderDTO>(nameof(EntityDeltaHeaderDTO._pad1)));
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
        public void ProfileCsvParser_AcceptsLegacyRowsWithoutQualityColumn()
        {
            string path = Path.Combine(Application.temporaryCachePath, "shinobu_256_legacy_profiles.csv");
            WriteLegacyProfileFixture(path);

            NativeArray<WalFuzzerProfileDTO> profiles = new NativeArray<WalFuzzerProfileDTO>(2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                bool parsed = WalIntegrityFuzzerCore.TryLoadProfilesCsv(path, profiles, out int count, out uint errorCode);
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
