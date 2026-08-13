#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Caves;
using Unity.Mathematics;
using System.Collections.Generic;

namespace Hecton8.Tests.Editor.Caves
{
    [TestFixture]
    public sealed class CaveFaunaPresetDataTests
    {
        [Test]
        public void CaveFaunaPreset_Initialization_HasValidDefaults()
        {
            var preset = new CaveFaunaPreset();

            // Check baseline defaults from standard definitions
            Assert.That(preset.faunaDensity, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.passivityLevel, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.territoriality, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.smallPassiveRatio, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.territorialRatio, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.rareCreatureRatio, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.predatorDensity, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
            Assert.That(preset.allowedSpecies, Is.Null);
        }

        [Test]
        public void CaveFaunaPreset_CreateShallowPreset_HasValidData()
        {
            var preset = CaveFaunaPreset.CreateShallowPreset();

            Assert.That(preset.faunaDensity, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(preset.passivityLevel, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(preset.territoriality, Is.EqualTo(0.1f).Within(0.001f));

            Assert.That(preset.smallPassiveRatio, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(preset.territorialRatio, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(preset.rareCreatureRatio, Is.EqualTo(0.05f).Within(0.001f));

            Assert.That(preset.floorSpawnBias, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(preset.wallSpawnBias, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(preset.openWaterBias, Is.EqualTo(0.1f).Within(0.001f));

            Assert.That(preset.predatorDensity, Is.EqualTo(0.05f).Within(0.001f));
            Assert.That(preset.allowedSpecies, Is.Not.Null);
            Assert.That(preset.allowedSpecies.Count, Is.EqualTo(2));
            Assert.That(preset.allowedSpecies, Contains.Item("small_fish"));
            Assert.That(preset.allowedSpecies, Contains.Item("biolum_jelly"));
        }

        [Test]
        public void CaveFaunaPreset_CreateMidPreset_HasValidData()
        {
            var preset = CaveFaunaPreset.CreateMidPreset();

            Assert.That(preset.faunaDensity, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(preset.passivityLevel, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(preset.territoriality, Is.EqualTo(0.4f).Within(0.001f));

            Assert.That(preset.smallPassiveRatio, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(preset.territorialRatio, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(preset.rareCreatureRatio, Is.EqualTo(0.1f).Within(0.001f));

            Assert.That(preset.floorSpawnBias, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(preset.wallSpawnBias, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(preset.openWaterBias, Is.EqualTo(0.2f).Within(0.001f));

            Assert.That(preset.predatorDensity, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(preset.allowedSpecies, Is.Not.Null);
            Assert.That(preset.allowedSpecies.Count, Is.EqualTo(3));
            Assert.That(preset.allowedSpecies, Contains.Item("crab"));
            Assert.That(preset.allowedSpecies, Contains.Item("cave_eel"));
            Assert.That(preset.allowedSpecies, Contains.Item("small_fish"));
        }

        [Test]
        public void CaveFaunaPreset_CreateDeepPreset_HasValidData()
        {
            var preset = CaveFaunaPreset.CreateDeepPreset();

            Assert.That(preset.faunaDensity, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(preset.passivityLevel, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(preset.territoriality, Is.EqualTo(0.7f).Within(0.001f));

            Assert.That(preset.smallPassiveRatio, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(preset.territorialRatio, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(preset.rareCreatureRatio, Is.EqualTo(0.2f).Within(0.001f));

            Assert.That(preset.floorSpawnBias, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(preset.wallSpawnBias, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(preset.openWaterBias, Is.EqualTo(0.4f).Within(0.001f));

            Assert.That(preset.predatorDensity, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(preset.allowedSpecies, Is.Not.Null);
            Assert.That(preset.allowedSpecies.Count, Is.EqualTo(2));
            Assert.That(preset.allowedSpecies, Contains.Item("leviathan"));
            Assert.That(preset.allowedSpecies, Contains.Item("angler_fish"));
        }

        [Test]
        public void AdjustForCaveMood_ReturnsNewInstanceAndAdjustsData()
        {
            var preset = CaveFaunaPreset.CreateMidPreset();
            var adjusted = preset.AdjustForCaveMood(0.5f, 0.5f);

            Assert.That(adjusted, Is.Not.SameAs(preset));
            Assert.That(adjusted.predatorDensity, Is.GreaterThanOrEqualTo(preset.predatorDensity));
            Assert.That(adjusted.allowedSpecies, Is.Not.SameAs(preset.allowedSpecies));
            Assert.That(adjusted.allowedSpecies.Count, Is.EqualTo(preset.allowedSpecies.Count));
        }

        [Test]
        public void CaveFaunaPreset_ManualInitialization_SupportsNullSpecies()
        {
            var preset = new CaveFaunaPreset
            {
                allowedSpecies = null
            };

            var adjusted = preset.AdjustForCaveMood(0.5f, 0.5f);
            Assert.That(adjusted.allowedSpecies, Is.Null);
        }
    }
}
#endif
