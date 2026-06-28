using NUnit.Framework;
using UnityEngine;
using Hecton8.Environment;
using Hecton.Localization;

namespace Hecton8.Tests.Editor
{
    public class HectonBiomeFamilyProfileEditTests
    {
        [Test]
        public void Creation_SetsDefaultValuesCorrectly()
        {
            var profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();

            Assert.That(profile.familyId, Is.EqualTo("biome.family.generic"));
            Assert.That(profile.familyLabel, Is.EqualTo("Generic Biome Family"));
            Assert.That(profile.atmosphereMood, Is.EqualTo("neutral"));
            Assert.That(profile.navigationStyle, Is.EqualTo("balanced"));
            Assert.That(profile.hazardStyle, Is.EqualTo("mixed"));
            Assert.That(profile.landmarkStyle, Is.EqualTo("subtle"));
            Assert.That(profile.primaryResourceTheme, Is.EqualTo("general_minerals"));
            Assert.That(profile.secondaryResourceTheme, Is.EqualTo("general_salvage"));
            Assert.That(profile.suggestedZoneFamily, Is.EqualTo("resources.clutter.mid"));
            Assert.That(profile.progressionFeeling, Is.EqualTo("neutral"));
        }

        [Test]
        public void RuntimeProperties_ReturnAssignedValues()
        {
            var profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();

            profile.familyId = "test.family";
            profile.familyLabel = "Test Family";
            profile.atmosphereMood = "gloomy";

            Assert.That(profile.RuntimeFamilyId, Is.EqualTo("test.family"));
            Assert.That(profile.RuntimeFamilyLabel, Is.EqualTo("Test Family"));
            Assert.That(profile.RuntimeAtmosphereMood, Is.EqualTo("gloomy"));
        }

        [Test]
        public void RuntimeProperties_ReturnFallbacksWhenEmpty()
        {
            var profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();

            profile.familyId = "";
            profile.familyLabel = "  ";
            profile.atmosphereMood = null;
            profile.navigationStyle = "";

            Assert.That(profile.RuntimeFamilyId, Is.EqualTo("biome.family.generic"));
            Assert.That(profile.RuntimeFamilyLabel, Is.EqualTo("Generic Biome Family"));
            Assert.That(profile.RuntimeAtmosphereMood, Is.EqualTo("neutral"));
            Assert.That(profile.RuntimeNavigationStyle, Is.EqualTo("balanced"));
        }

        [Test]
        public void OnValidate_NormalizesAuthoringText()
        {
            var profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();

            profile.familyId = "   spaced.family.id   ";
            profile.atmosphereMood = "   ";

            // Trigger OnValidate
            var method = profile.GetType().GetMethod("OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(profile, null);

            Assert.That(profile.familyId, Is.EqualTo("spaced.family.id"));
            Assert.That(profile.atmosphereMood, Is.EqualTo("neutral"));
        }

        [Test]
        public void OnEnable_RefreshesFamilyHashId()
        {
            var profile = ScriptableObject.CreateInstance<HectonBiomeFamilyProfile>();

            profile.familyId = "hash.test";

            // Trigger OnEnable
            var method = profile.GetType().GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(profile, null);

            int expectedHash = LocHash.ComputeAsciiLowerInvariant("hash.test");
            Assert.That(profile.FamilyHashId, Is.EqualTo(expectedHash));
        }
    }
}
