using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using NUnit.Framework;

namespace Hecton8.Tests.PlayMode
{
    public sealed class H8StaticDataSanityTests
    {
        private const int StaticDataHeaderSizeBytes = 64;
        private const int StaticDataPayloadCrcOffset = 16;
        private const int StaticDataLookupOffsetOffset = 28;
        private const int StaticDataLookupRecordOffset = 8;
        private const int StaticDataRecordHashOffset = 0;

        [Test]
        public void BakeOpenAndScan_DefaultBalanceData_HasNoNaNs()
        {
            string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string output = Path.Combine(Path.GetTempPath(), "h8_static_data_test");
            if (Directory.Exists(output))
                Directory.Delete(output, true);

            H8DataBakeResult bake = H8DataBaker.Bake(root, output);
            Assert.IsTrue(bake.Success, bake.Message);
            Assert.Greater(bake.RecordCount, 0);

            IDataVault existingVault = GlobalRegistry.DataVault;
            GlobalDataVault ownedVault = null;
            IDataVault activeVault = existingVault;
            if (activeVault == null)
            {
                ownedVault = GlobalDataVault.Create();
                GlobalRegistry.RegisterDataVault(ownedVault);
                activeVault = ownedVault;
            }

            try
            {
                uint expectedBabelCrc32;
                using (StaticDataStore store = new StaticDataStore(activeVault))
                {
                    Assert.IsTrue(store.Open(bake.StaticDataPath));
                    expectedBabelCrc32 = store.BabelCrc32;
                    Assert.AreEqual(bake.BabelCrc32, expectedBabelCrc32);

                    H8StaticDataSanityReport report = H8StaticDataSanity.ScanForNaNs(store);
                    Assert.IsTrue(report.IsClean, report.Message);

                    uint scrapHash = H8DataHashTool.ComputeFnv1a32("scrap_metal".AsSpan());
                    ref readonly H8ItemStaticRecord scrap = ref store.FetchRecord<H8ItemStaticRecord>(scrapHash);
                    Assert.AreEqual(scrapHash, scrap.Hash);
                    Assert.AreEqual(12, scrap.Cost);

                    ref readonly H8PhysicsStaticRecord wrongType = ref store.FetchRecord<H8PhysicsStaticRecord>(scrapHash);
                    Assert.AreEqual(0u, wrongType.Hash);
                    Assert.IsTrue(store.TryReload(bake.StaticDataPath));
                    Assert.AreEqual(expectedBabelCrc32, store.BabelCrc32);

                    string dumpPath = Path.Combine(output, "Dump_CSV_DATA_MONOLITH_SYNC.bin");
                    store.DumpBlackBox(dumpPath);
                    byte[] dumpBytes = File.ReadAllBytes(dumpPath);
                    Assert.AreEqual(
                        H8StaticDataFormat.TelemetryDumpHeaderSizeBytes + (H8StaticDataFormat.TelemetryFrameCount * 64),
                        dumpBytes.Length);
                    Assert.AreEqual(H8StaticDataFormat.TelemetryDumpMagic, BitConverter.ToUInt64(dumpBytes, 0));
                    Assert.AreEqual((uint)H8StaticDataFormat.TelemetryFrameCount, BitConverter.ToUInt32(dumpBytes, 8));
                    Assert.AreEqual(64u, BitConverter.ToUInt32(dumpBytes, 12));
                    store.Shutdown();
                    store.Shutdown();
                }

                using (BabelDictionaryStore babel = new BabelDictionaryStore(activeVault))
                {
                    Assert.IsTrue(babel.Open(bake.BabelPath, expectedBabelCrc32));
                    Assert.AreEqual(expectedBabelCrc32, babel.PayloadCrc32);

                    uint nameHash = H8DataHashTool.ComputeFnv1a32Utf8("Scrap Metal".AsSpan());
                    ReadOnlySpan<byte> utf8 = babel.TrackUtf8Lookup(nameHash);
                    Assert.Greater(utf8.Length, 0);
                    Assert.AreEqual("Scrap Metal", Encoding.UTF8.GetString(utf8));

                    ReadOnlySpan<byte> missing = babel.TrackUtf8Lookup(0xDEADBEEFu);
                    Assert.AreEqual("ERROR", Encoding.UTF8.GetString(missing));
                    Assert.IsTrue(babel.TryReload(bake.BabelPath, expectedBabelCrc32));
                    Assert.AreEqual(expectedBabelCrc32, babel.PayloadCrc32);
                    babel.Shutdown();
                    babel.Shutdown();
                }
            }
            finally
            {
                if (ownedVault != null)
                {
                    GlobalRegistry.UnregisterDataVault(ownedVault);
                    ownedVault.Dispose();
                }
            }
        }

        [Test]
        public void ScanForNaNs_RejectsRecordHashMismatchEvenWhenPayloadCrcIsValid()
        {
            string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string output = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_record_hash");
            if (Directory.Exists(output))
                Directory.Delete(output, true);

            H8DataBakeResult bake = H8DataBaker.Bake(root, output);
            Assert.IsTrue(bake.Success, bake.Message);
            CorruptFirstRecordHashAndRefreshCrc(bake.StaticDataPath);

            IDataVault existingVault = GlobalRegistry.DataVault;
            GlobalDataVault ownedVault = null;
            IDataVault activeVault = existingVault;
            if (activeVault == null)
            {
                ownedVault = GlobalDataVault.Create();
                GlobalRegistry.RegisterDataVault(ownedVault);
                activeVault = ownedVault;
            }

            try
            {
                using (StaticDataStore store = new StaticDataStore(activeVault))
                {
                    Assert.IsTrue(store.Open(bake.StaticDataPath));
                    H8StaticDataSanityReport report = H8StaticDataSanity.ScanForNaNs(store);
                    Assert.IsFalse(report.IsClean);
                    StringAssert.Contains("Record hash mismatch", report.Message);
                    store.Shutdown();
                }
            }
            finally
            {
                if (ownedVault != null)
                {
                    GlobalRegistry.UnregisterDataVault(ownedVault);
                    ownedVault.Dispose();
                }
            }
        }

        [Test]
        public void Bake_RejectsIdentityColumnNotFirst()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_identity");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string text = File.ReadAllText(itemsPath);
            File.WriteAllText(itemsPath, text.Replace("Id,version_id", "version_id,Id"));

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("[CRITICAL_DATA_SCHEMA]", bake.Message);
        }

        [Test]
        public void Bake_RejectsNonCanonicalIdentityKey()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_key");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string text = File.ReadAllText(itemsPath);
            File.WriteAllText(itemsPath, text.Replace("scrap_metal", "Scrap Metal"));

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("[CRITICAL_DATA_KEY]", bake.Message);
        }

        [Test]
        public void Bake_RejectsSnakeCaseSeparatorDrift()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_separator_key");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string text = File.ReadAllText(itemsPath);
            File.WriteAllText(itemsPath, text.Replace("scrap_metal", "scrap__metal"));

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("[CRITICAL_DATA_KEY]", bake.Message);
        }

        [Test]
        public void SchemaHash_MatchesCurrentBakeCatalog()
        {
            Assert.AreEqual(H8StaticDataFormat.SchemaHash, H8DataBaker.CurrentSchemaHash);
        }

        [Test]
        public void Bake_RejectsHeaderCaseDrift()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_header_case");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string text = File.ReadAllText(itemsPath);
            File.WriteAllText(itemsPath, text.Replace("Id,version_id", "id,version_id"));

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("must match exact header case", bake.Message);
        }

        [Test]
        public void Bake_RejectsUnclosedQuotedField()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_quote");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string[] lines = File.ReadAllLines(itemsPath);
            lines[1] = lines[1].Replace("Scrap Metal", "\"Scrap Metal");
            File.WriteAllLines(itemsPath, lines);

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("Unclosed quoted field", bake.Message);
        }

        [Test]
        public void Bake_RejectsRowWidthDrift()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_width");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string[] lines = File.ReadAllLines(itemsPath);
            lines[1] = lines[1] + ",orphan_cell";
            File.WriteAllLines(itemsPath, lines);

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("cells; expected", bake.Message);
        }

        [Test]
        public void Bake_RejectsInvalidUtf8()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_utf8");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            byte[] bytes = File.ReadAllBytes(itemsPath);
            bytes[bytes.Length - 1] = 0xFF;
            File.WriteAllBytes(itemsPath, bytes);

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("CSV read failed", bake.Message);
        }

        [Test]
        public void Bake_RejectsControlCharactersInText()
        {
            string source = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Data", "Balance"));
            string root = Path.Combine(Path.GetTempPath(), "h8_static_data_bad_control_text");
            ResetDirectory(root);
            CopyBalanceCsvs(source, root);

            string itemsPath = Path.Combine(root, "Items.csv");
            string text = File.ReadAllText(itemsPath);
            File.WriteAllText(itemsPath, text.Replace("Scrap Metal", "Scrap\tMetal"));

            H8DataBakeResult bake = H8DataBaker.Bake(root, Path.Combine(root, "Baked"));
            Assert.IsFalse(bake.Success);
            StringAssert.Contains("[CRITICAL_DATA_TEXT]", bake.Message);
        }

        [Test]
        public void TextHash_UsesUtf8BytesForBabelKeys()
        {
            uint ascii = H8DataHashTool.ComputeFnv1a32Utf8("Scrap Metal".AsSpan());
            uint cyrillic = H8DataHashTool.ComputeFnv1a32Utf8("Металл".AsSpan());
            Assert.AreNotEqual(0u, ascii);
            Assert.AreNotEqual(0u, cyrillic);
            Assert.AreNotEqual(ascii, cyrillic);
        }

        private static void CopyBalanceCsvs(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source, "*.csv");
            for (int i = 0; i < files.Length; i++)
                File.Copy(files[i], Path.Combine(destination, Path.GetFileName(files[i])), true);
        }

        private static void ResetDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);

            Directory.CreateDirectory(path);
        }

        private static void CorruptFirstRecordHashAndRefreshCrc(string staticDataPath)
        {
            byte[] bytes = File.ReadAllBytes(staticDataPath);
            int lookupOffset = (int)BitConverter.ToUInt32(bytes, StaticDataLookupOffsetOffset);
            int firstRecordOffset = (int)BitConverter.ToInt64(bytes, lookupOffset + StaticDataLookupRecordOffset);
            uint originalHash = BitConverter.ToUInt32(bytes, firstRecordOffset + StaticDataRecordHashOffset);
            uint corruptedHash = originalHash ^ 0xA5A5A5A5u;
            if (corruptedHash == 0u)
                corruptedHash = 1u;

            BitConverter.GetBytes(corruptedHash).CopyTo(bytes, firstRecordOffset + StaticDataRecordHashOffset);
            uint payloadCrc = H8Crc32.Compute(new ReadOnlySpan<byte>(
                bytes,
                StaticDataHeaderSizeBytes,
                bytes.Length - StaticDataHeaderSizeBytes));
            BitConverter.GetBytes(payloadCrc).CopyTo(bytes, StaticDataPayloadCrcOffset);
            File.WriteAllBytes(staticDataPath, bytes);
        }
    }
}
