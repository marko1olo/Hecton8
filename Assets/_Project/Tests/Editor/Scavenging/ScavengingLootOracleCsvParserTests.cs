using System;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Hecton8.Scavenging;

namespace Hecton8.Tests.Scavenging
{
    [TestFixture]
    public class ScavengingLootOracleCsvParserTests
    {
        private NativeArray<byte> CreateCsvBytes(string csvContent)
        {
            if (csvContent == null) return default;
            byte[] bytes = Encoding.UTF8.GetBytes(csvContent);
            NativeArray<byte> nativeBytes = new NativeArray<byte>(bytes.Length, Allocator.Temp);
            for (int i = 0; i < bytes.Length; i++)
            {
                nativeBytes[i] = bytes[i];
            }
            return nativeBytes;
        }

        private uint HashTokenFnv1a(string token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                char b = token[i];
                if (b == ' ' || b == '\t' || b == '"')
                    continue;

                hash ^= (byte)b;
                hash *= 16777619u;
            }
            return hash;
        }



        [Test]
        public void ParseLootDistribution_WithUncreatedCsvBytes_ReturnsZero()
        {
            NativeArray<byte> csvBytes = default;
            NativeArray<LootTableEntryDTO> destination = new NativeArray<LootTableEntryDTO>(10, Allocator.Temp);

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(0, result);
            destination.Dispose();
        }

        [Test]
        public void ParseLootDistribution_WithUncreatedDestination_ReturnsZero()
        {
            NativeArray<byte> csvBytes = CreateCsvBytes("100, 10, 1");
            NativeArray<LootTableEntryDTO> destination = default;

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(0, result);
            csvBytes.Dispose();
        }

        [Test]
        public void ParseLootDistribution_WithValidNumericData_ParsesCorrectlyAndAccumulatesWeight()
        {
            string csv = "100, 10, 1\n200, 20, 2\n300, 30, 3";
            NativeArray<byte> csvBytes = CreateCsvBytes(csv);
            NativeArray<LootTableEntryDTO> destination = new NativeArray<LootTableEntryDTO>(5, Allocator.Temp);

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(3, result);

            Assert.AreEqual(100u, destination[0].ItemHashID);
            Assert.AreEqual(10u, destination[0].DropWeight); // 10
            Assert.AreEqual(1u, destination[0].ConditionMask);

            Assert.AreEqual(200u, destination[1].ItemHashID);
            Assert.AreEqual(30u, destination[1].DropWeight); // 10 + 20
            Assert.AreEqual(2u, destination[1].ConditionMask);

            Assert.AreEqual(300u, destination[2].ItemHashID);
            Assert.AreEqual(60u, destination[2].DropWeight); // 30 + 30
            Assert.AreEqual(3u, destination[2].ConditionMask);

            csvBytes.Dispose();
            destination.Dispose();
        }



        [Test]
        public void ParseLootDistribution_WithStringItemHash_UsesFnv1aHash()
        {
            string itemName = "TitaniumOre";
            string csv = $"{itemName}, 50, 1";
            NativeArray<byte> csvBytes = CreateCsvBytes(csv);
            NativeArray<LootTableEntryDTO> destination = new NativeArray<LootTableEntryDTO>(5, Allocator.Temp);

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(1, result);
            Assert.AreEqual(HashTokenFnv1a(itemName), destination[0].ItemHashID);
            Assert.AreEqual(50u, destination[0].DropWeight);

            csvBytes.Dispose();
            destination.Dispose();
        }

        [Test]
        public void ParseLootDistribution_WithZeroWeight_SkipsLine()
        {
            string csv = "100, 10, 1\n200, 0, 2\n300, 20, 3";
            NativeArray<byte> csvBytes = CreateCsvBytes(csv);
            NativeArray<LootTableEntryDTO> destination = new NativeArray<LootTableEntryDTO>(5, Allocator.Temp);

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(2, result);

            Assert.AreEqual(100u, destination[0].ItemHashID);
            Assert.AreEqual(10u, destination[0].DropWeight);

            Assert.AreEqual(300u, destination[1].ItemHashID);
            Assert.AreEqual(30u, destination[1].DropWeight); // 10 + 20

            csvBytes.Dispose();
            destination.Dispose();
        }

        [Test]
        public void ParseLootDistribution_WithNonNumericMask_DefaultsToToolMaskAny()
        {
            string csv = "100, 10, AnyTool";
            NativeArray<byte> csvBytes = CreateCsvBytes(csv);
            NativeArray<LootTableEntryDTO> destination = new NativeArray<LootTableEntryDTO>(5, Allocator.Temp);

            int result = ScavengingLootOracleCsvParser.ParseLootDistributionCsvBytes(csvBytes, destination);

            Assert.AreEqual(1, result);
            Assert.AreEqual(100u, destination[0].ItemHashID);
            Assert.AreEqual(10u, destination[0].DropWeight);
            Assert.AreEqual(ScavengingLootOracleConstants.ToolMaskAny, destination[0].ConditionMask);

            csvBytes.Dispose();
            destination.Dispose();
        }
    }
}
