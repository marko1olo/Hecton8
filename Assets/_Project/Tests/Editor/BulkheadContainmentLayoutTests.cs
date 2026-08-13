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
        private const string BulkheadContractsPath = "Assets/_Project/Scripts/Construction/BulkheadContainmentContracts.cs";
        private const string BulkheadEditorPath = "Assets/_Project/Scripts/Construction/Editor/BulkheadContainmentEditor.cs";
        private const string BulkheadIntentBusPath = "Assets/_Project/Scripts/Core/BulkheadContainmentIntentBus.cs";
        private const string BaseAirlockPath = "Assets/_Project/Scripts/Gameplay/BaseAirlock.cs";
        private const string HatchRuntimePath = "Assets/_Project/Scripts/Construction/BulkheadContainmentRuntime_HatchLocks.cs";
        private const string HatchJobsPath = "Assets/_Project/Scripts/Construction/HatchLockJobs.cs";

        [Test]
        public void BulkheadStateDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadStateDTO>(), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadStateDTO), nameof(BulkheadStateDTO.EdgeHashID)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadStateDTO), nameof(BulkheadStateDTO.ClosureProgress)), Is.EqualTo(4));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadStateDTO), nameof(BulkheadStateDTO.AssociatedLock)), Is.EqualTo(8));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadStateDTO), nameof(BulkheadStateDTO.SiblingNodeHash)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadStateDTO), nameof(BulkheadStateDTO.Flags)), Is.EqualTo(16));
            Assert.That(BulkheadStateLayoutGuard.ValidateLayout(), Is.True);
        }

        [Test]
        public void BulkheadPlaneDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadPlaneDTO>(), Is.EqualTo(BulkheadStateLayoutGuard.PlaneSizeBytes));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.CenterAup)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.Normal)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.WidthMeters)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.HeightMeters)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.HalfThicknessMeters)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.EdgeHashID)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.Flags)), Is.EqualTo(52));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.IntegrityIndex)), Is.EqualTo(56));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadPlaneDTO), nameof(BulkheadPlaneDTO.Reserved)), Is.EqualTo(60));
        }

        [Test]
        public void BulkheadCollisionResultDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadCollisionResultDTO>(), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.Normal)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.DepthMeters)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.EdgeHashID)), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.Flags)), Is.EqualTo(20));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.ClosureProgress)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadCollisionResultDTO), nameof(BulkheadCollisionResultDTO.Frame)), Is.EqualTo(28));
        }

        [Test]
        public void BulkheadTelemetryEntry_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadTelemetryEntry>(), Is.EqualTo(BulkheadStateLayoutGuard.TelemetrySizeBytes));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.Frame)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.ActiveCount)), Is.EqualTo(4));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.SealedCount)), Is.EqualTo(8));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.JammedCount)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.AverageClosure)), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.AuthorityCadenceHz)), Is.EqualTo(20));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.GlobalQualityWeight)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.LastScheduleMicroseconds)), Is.EqualTo(28));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.StateHash)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.CollisionEdgeHash)), Is.EqualTo(36));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.CollisionDepthMeters)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.Flags)), Is.EqualTo(44));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.Reserved0)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadTelemetryEntry), nameof(BulkheadTelemetryEntry.Reserved1)), Is.EqualTo(56));
        }

        [Test]
        public void BulkheadIntentDTO_IsCacheLineAligned()
        {
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<BulkheadContainmentIntentControlDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.WriteCursor)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.ReadCursor)), Is.EqualTo(4));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Capacity)), Is.EqualTo(8));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Dropped)), Is.EqualTo(12));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.LastEdgeHashID)), Is.EqualTo(16));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Flags)), Is.EqualTo(20));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Reserved0)), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Reserved1)), Is.EqualTo(40));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Reserved2)), Is.EqualTo(48));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(BulkheadContainmentIntentControlDTO), nameof(BulkheadContainmentIntentControlDTO.Reserved3)), Is.EqualTo(56));
        }

        [Test]
        public void BulkheadIntentPressure_IsVisibleThroughTelemetryAndEditor()
        {
            string intentBus = File.ReadAllText(BulkheadIntentBusPath);
            string runtime = File.ReadAllText(BulkheadRuntimePath);
            string jobs = File.ReadAllText(BulkheadJobsPath);
            string contracts = File.ReadAllText(BulkheadContractsPath);
            string editor = File.ReadAllText(BulkheadEditorPath);

            StringAssert.Contains("uint dropped = pending - views.Capacity + 1u;", intentBus);
            StringAssert.Contains("control.Dropped = unchecked(control.Dropped + dropped);", intentBus);
            StringAssert.Contains("public const uint IntentRejected", contracts);
            StringAssert.Contains("public const uint IntentOverflowCompensated", contracts);
            StringAssert.Contains("_lastIntentAppliedCount", runtime);
            StringAssert.Contains("_lastIntentRejectedCount", runtime);
            StringAssert.Contains("_lastIntentOverflowDroppedCount", runtime);
            StringAssert.Contains("_observedIntentControlDropped", runtime);
            StringAssert.Contains("ResetIntentFrameCounters();", runtime);
            StringAssert.Contains("Reserved0 = PackIntentTelemetryCounters(_lastIntentAppliedCount, _lastIntentRejectedCount)", runtime);
            StringAssert.Contains("Reserved1 = PackIntentTelemetryCounters(_lastIntentOverflowDroppedCount, _observedIntentControlDropped)", runtime);
            StringAssert.Contains("IntentCounters0 = PackIntentTelemetryCounters(_lastIntentAppliedCount, _lastIntentRejectedCount)", runtime);
            StringAssert.Contains("IntentCounters1 = PackIntentTelemetryCounters(_lastIntentOverflowDroppedCount, _observedIntentControlDropped)", runtime);
            StringAssert.Contains("Reserved0 = IntentCounters0", jobs);
            StringAssert.Contains("Reserved1 = IntentCounters1", jobs);
            StringAssert.Contains("out uint intentAppliedCount", runtime);
            StringAssert.Contains("out uint intentRejectedCount", runtime);
            StringAssert.Contains("out uint intentOverflowDroppedCount", runtime);
            StringAssert.Contains("out uint intentOverflowDroppedTotal", runtime);
            StringAssert.Contains(".Append(\" | Intents: \").Append(intentAppliedCount).Append('/').Append(intentRejectedCount)", editor);
            StringAssert.Contains(".Append(\" | Overflow: \").Append(intentOverflowDroppedCount).Append('/').Append(intentOverflowDroppedTotal)", editor);
        }

        [Test]
        public void BaseAirlockBulkheadProducer_RearmsOnDispatcherAndDataVaultHotSwap()
        {
            string source = File.ReadAllText(BaseAirlockPath);
            string hotSwap = ExtractMethodBlock(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("case GlobalRegistryServiceSlot.Dispatcher:", hotSwap);
            StringAssert.Contains("TryUnregister();", hotSwap);
            StringAssert.Contains("if (currentService != null)", hotSwap);
            StringAssert.Contains("TryRegister();", hotSwap);
            StringAssert.Contains("case GlobalRegistryServiceSlot.DataVault:", hotSwap);
            StringAssert.Contains("_bulkheadContainmentPublishPending = true;", hotSwap);
            StringAssert.Contains("_bulkheadContainmentRetryTicks = 0;", hotSwap);
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

        private static string ExtractMethodBlock(string source, string signature)
        {
            int start = source.IndexOf(signature, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(start, i - start + 1);
                }
            }

            Assert.Fail(signature);
            return string.Empty;
        }
    }
}
