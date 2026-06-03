using System.IO;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    public sealed class BulkheadContainmentLayoutTests
    {
        private const string BulkheadRuntimePath = "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime.cs";
        private const string BulkheadJobsPath = "Assets/_Project/Scripts/Construction/BulkheadContainmentJobs.cs";
        private const string HatchRuntimePath = "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs";
        private const string HatchJobsPath = "Assets/_Project/Scripts/Construction/HatchLockJobs.cs";

        [Test]
        public void BulkheadStateDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadStateDTO>(), Is.EqualTo(32));
            Assert.That(BulkheadStateLayoutGuard.ValidateLayout(), Is.True);
        }

        [Test]
        public void BulkheadIntentDTO_IsCacheLineAligned()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentControlDTO>(), Is.EqualTo(64));
        }

        [Test]
        public void BulkheadContainment_HasNoSyntheticAuthorityPublishRoute()
        {
            string bulkheadRuntime = File.ReadAllText(BulkheadRuntimePath);
            string bulkheadJobs = File.ReadAllText(BulkheadJobsPath);
            string hatchRuntime = File.ReadAllText(HatchRuntimePath);
            string hatchJobs = File.ReadAllText(HatchJobsPath);

            AssertNoToken(bulkheadRuntime, "generate", "MockBulkheads");
            AssertNoToken(bulkheadRuntime, "Schedule", "MockDataIfRequired");
            AssertNoToken(bulkheadRuntime, "BulkheadJobPinHatch", "MockFluid");
            AssertNoToken(bulkheadRuntime, "Shinobu343Hatch", "MockFluidCompartments");
            AssertNoToken(bulkheadJobs, "Generate", "MockBulkheadsJob");
            AssertNoToken(hatchRuntime, "generate", "MockHatchPressure");
            AssertNoToken(hatchRuntime, "_hatch", "MockFluidCompartmentsHandle");
            AssertNoToken(hatchRuntime, "Shinobu343Hatch", "MockFluidCompartments");
            AssertNoToken(hatchJobs, "Generate", "MockHatchPressureJob");
            AssertNoToken(hatchJobs, "FluidCompartmentFlags.Mock", "Breach");
            Assert.That(HatchLockConstants.PairedFluidRowsPerHatch, Is.EqualTo(2));
        }

        private static void AssertNoToken(string source, string prefix, string suffix)
        {
            Assert.That(source.Contains(string.Concat(prefix, suffix)), Is.False);
        }
    }
}
