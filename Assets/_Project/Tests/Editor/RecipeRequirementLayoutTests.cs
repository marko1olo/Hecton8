using System.Runtime.InteropServices;
using Hecton8.Crafting;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class RecipeRequirementLayoutTests
    {
        [Test]
        public void RecipeRequirementDTO_MatchesCanonicalUnsafeLayout()
        {
            Assert.That(Marshal.SizeOf<RecipeRequirementDTO>(), Is.EqualTo(32));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.BlueprintUnlockMask)).ToInt32(), Is.EqualTo(0));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.ResultItemHash)).ToInt32(), Is.EqualTo(8));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashA)).ToInt32(), Is.EqualTo(12));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashB)).ToInt32(), Is.EqualTo(16));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashC)).ToInt32(), Is.EqualTo(20));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.IngredientHashD)).ToInt32(), Is.EqualTo(24));
            Assert.That(Marshal.OffsetOf<RecipeRequirementDTO>(nameof(RecipeRequirementDTO.QuantitiesPacked)).ToInt32(), Is.EqualTo(28));
        }
    }
}
