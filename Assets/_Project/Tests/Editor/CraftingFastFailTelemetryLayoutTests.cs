using System.Runtime.InteropServices;
using Hecton8.Crafting;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class CraftingFastFailTelemetryLayoutTests
    {
        [Test]
        public void CraftingFastFailTelemetryEntry_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<CraftingFastFailTelemetryEntry>(), Is.EqualTo(64));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RequirementMask)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UnlockMask)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.Frame)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RecipeWordIndex)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.RecipesEvaluated)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UnlockCullCount)).ToInt32(), Is.EqualTo(28));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.MaskCullCount)).ToInt32(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.SimdSuccessCount)).ToInt32(), Is.EqualTo(36));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.InventoryVersion)).ToInt32(), Is.EqualTo(40));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.UiPublicationBudget)).ToInt32(), Is.EqualTo(44));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.StateHash)).ToInt32(), Is.EqualTo(48));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.Flags)).ToInt32(), Is.EqualTo(52));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.ScheduleMicroseconds)).ToInt32(), Is.EqualTo(56));
            Assert.That(Marshal.OffsetOf<CraftingFastFailTelemetryEntry>(nameof(CraftingFastFailTelemetryEntry.GlobalQualityWeight)).ToInt32(), Is.EqualTo(60));
        }
    }
}
