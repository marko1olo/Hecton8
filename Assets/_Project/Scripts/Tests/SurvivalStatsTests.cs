using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace Hecton8.Tests
{
    [TestFixture]
    public class SurvivalStatsTests
    {
        private SurvivalStats _stats;

        [SetUp]
        public void SetUp()
        {
            _stats = ScriptableObject.CreateInstance<SurvivalStats>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_stats);
        }

        [Test]
        public void DefaultValues_AreExposedCorrectly()
        {
            // Legacy / Required fields assertions
            Assert.That(_stats.maxHealth, Is.EqualTo(100f).Within(0.001f));
            Assert.That(_stats.oxygenCapacity, Is.EqualTo(60f).Within(0.001f));
            Assert.That(_stats.temperatureTolerance, Is.EqualTo(15f).Within(0.001f));

            Assert.That(_stats.MaxOxygen, Is.EqualTo(100f).Within(0.001f));
            Assert.That(_stats.OxygenConsumptionRate, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(_stats.MaxEnergy, Is.EqualTo(200f).Within(0.001f));
            Assert.That(_stats.EnergyConsumptionRate, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(_stats.CarryCapacityKg, Is.EqualTo(200f).Within(0.001f));
            Assert.That(_stats.MaxIntegrity, Is.EqualTo(100f).Within(0.001f));

            Assert.That(_stats.MaxHunger, Is.EqualTo(100f).Within(0.001f));
            Assert.That(_stats.HungerDrainRate, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(_stats.StarvationDamageRate, Is.EqualTo(1f).Within(0.001f));

            Assert.That(_stats.MaxThirst, Is.EqualTo(100f).Within(0.001f));
            Assert.That(_stats.ThirstDrainRate, Is.EqualTo(0.15f).Within(0.001f));
            Assert.That(_stats.DehydrationDamageRate, Is.EqualTo(1.5f).Within(0.001f));

            Assert.That(_stats.SafeDepth, Is.EqualTo(50f).Within(0.001f));
            Assert.That(_stats.PressureDamageRate, Is.EqualTo(2f).Within(0.001f));
            Assert.That(_stats.PressureScalePerMeter, Is.EqualTo(0.02f).Within(0.001f));

            Assert.That(_stats.MinSafeTemp, Is.EqualTo(-5f).Within(0.001f));
            Assert.That(_stats.MaxSafeTemp, Is.EqualTo(45f).Within(0.001f));
            Assert.That(_stats.TempDamageRate, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.TempEnergyScale, Is.EqualTo(0.05f).Within(0.001f));

            Assert.That(_stats.RadiationThreshold, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(_stats.RadiationDamageRate, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void OnValidate_ClampsValuesToMinimums()
        {
            var so = new SerializedObject(_stats);

            so.FindProperty("maxOxygen").floatValue = -10f;
            so.FindProperty("maxEnergy").floatValue = 0f;
            so.FindProperty("carryCapacityKg").floatValue = -50f;
            so.FindProperty("maxIntegrity").floatValue = -1f;
            so.FindProperty("maxHunger").floatValue = 0f;
            so.FindProperty("maxThirst").floatValue = 0f;

            so.FindProperty("oxygenConsumptionRate").floatValue = -1f;
            so.FindProperty("energyConsumptionRate").floatValue = -2f;
            so.FindProperty("hungerDrainRate").floatValue = -0.5f;
            so.FindProperty("thirstDrainRate").floatValue = -0.5f;
            so.FindProperty("starvationDamageRate").floatValue = -1f;
            so.FindProperty("dehydrationDamageRate").floatValue = -1f;
            so.FindProperty("pressureDamageRate").floatValue = -5f;
            so.FindProperty("pressureScalePerMeter").floatValue = -1f;
            so.FindProperty("safeDepth").floatValue = -10f;

            so.FindProperty("tempDamageRate").floatValue = -1f;
            so.FindProperty("tempEnergyScale").floatValue = -1f;
            so.FindProperty("radiationThreshold").floatValue = -1f;
            so.FindProperty("radiationDamageRate").floatValue = -1f;

            so.ApplyModifiedProperties();

            MethodInfo onValidateMethod = typeof(SurvivalStats).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onValidateMethod != null)
            {
                onValidateMethod.Invoke(_stats, null);
            }

            Assert.That(_stats.MaxOxygen, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.MaxEnergy, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.CarryCapacityKg, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.MaxIntegrity, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.MaxHunger, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_stats.MaxThirst, Is.EqualTo(1f).Within(0.001f));

            Assert.That(_stats.OxygenConsumptionRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.EnergyConsumptionRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.HungerDrainRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.ThirstDrainRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.StarvationDamageRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.DehydrationDamageRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.PressureDamageRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.PressureScalePerMeter, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.SafeDepth, Is.EqualTo(0f).Within(0.001f));

            Assert.That(_stats.TempDamageRate, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.TempEnergyScale, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.RadiationThreshold, Is.EqualTo(0f).Within(0.001f));
            Assert.That(_stats.RadiationDamageRate, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void OnValidate_FixesInvertedTemperatureRange()
        {
            var so = new SerializedObject(_stats);
            so.FindProperty("minSafeTemp").floatValue = 50f;
            so.FindProperty("maxSafeTemp").floatValue = -10f; // invalid, max < min
            so.ApplyModifiedProperties();

            MethodInfo onValidateMethod = typeof(SurvivalStats).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            if (onValidateMethod != null)
            {
                onValidateMethod.Invoke(_stats, null);
            }

            Assert.That(_stats.MinSafeTemp, Is.EqualTo(50f).Within(0.001f));
            Assert.That(_stats.MaxSafeTemp, Is.EqualTo(60f).Within(0.001f)); // min + 10f
        }
    }
}