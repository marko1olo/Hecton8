using System.Runtime.InteropServices;
using Hecton8.Crafting;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class FabricatorMemoryTelemetryLayoutTests
    {
        [Test]
        public void FabricatorMemoryTelemetryEntry_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<Fabricator.FabricatorMemoryTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Sequence)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.StateHash)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Frame)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.BufferId)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.HandleGeneration)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.VaultGeneration)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Flags)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.Capacity)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.FailureStreak)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.GlobalQualityWeight)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.CpuMicroseconds)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.GpuMicroseconds)).ToInt32(), Is.EqualTo(52));
            Assert.That(Marshal.OffsetOf<Fabricator.FabricatorMemoryTelemetryEntry>(nameof(Fabricator.FabricatorMemoryTelemetryEntry.SystemId)).ToInt32(), Is.EqualTo(56));
        }
    }
}
