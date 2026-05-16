using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Data;
using Hecton8.Core.Memory;
using NUnit.Framework;

namespace Hecton8.Tests.PlayMode
{
    public sealed class H8StaticDataSanityTests
    {
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
                using (StaticDataStore store = new StaticDataStore(activeVault))
                {
                    Assert.IsTrue(store.Open(bake.StaticDataPath));
                    H8StaticDataSanityReport report = H8StaticDataSanity.ScanForNaNs(store);
                    Assert.IsTrue(report.IsClean, report.Message);

                    uint scrapHash = H8DataHashTool.ComputeFnv1a32("scrap_metal".AsSpan());
                    ref readonly H8ItemStaticRecord scrap = ref store.GetRecord<H8ItemStaticRecord>(scrapHash);
                    Assert.AreEqual(scrapHash, scrap.Hash);
                    Assert.AreEqual(12, scrap.Cost);
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
    }
}
