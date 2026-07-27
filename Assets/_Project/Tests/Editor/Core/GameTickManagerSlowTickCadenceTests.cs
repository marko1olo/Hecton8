#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Core
{
    /// <summary>
    /// Proves the slow-tick accumulator carries its overshoot instead of discarding it, so the
    /// SlowTick cadence stays frame-rate independent, and proves the carried debt is bounded so a
    /// long hitch cannot force a catch-up tick every frame.
    /// </summary>
    [TestFixture]
    public class GameTickManagerSlowTickCadenceTests
    {
        private const float Interval = 0.5f;
        private const float Tolerance = 1e-5f;

        private static GameTickManager CreateManager(out GameObject owner)
        {
            owner = new GameObject();
            GameTickManager manager = owner.AddComponent<GameTickManager>();

            MethodInfo ensureInitialized = typeof(GameTickManager).GetMethod(
                "EnsureInitialized",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(ensureInitialized, "Could not find EnsureInitialized method");
            ensureInitialized.Invoke(manager, null);

            SetPrivateField(manager, "slowTickInterval", Interval);

            // Profiling would pull Stopwatch and telemetry into an EditMode assertion for no
            // benefit here; the cadence maths is what is under test.
            SetPrivateField(manager, "enableSlowTickProfiling", false);

            return manager;
        }

        private static void SetPrivateField(GameTickManager manager, string fieldName, object value)
        {
            FieldInfo field = typeof(GameTickManager).GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find field " + fieldName);
            field.SetValue(manager, value);
        }

        private static float ReadAccumulator(GameTickManager manager)
        {
            FieldInfo field = typeof(GameTickManager).GetField(
                "_slowTickAccumulator",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find _slowTickAccumulator field");
            return (float)field.GetValue(manager);
        }

        private static void ProcessSlowTick(GameTickManager manager, float deltaTime)
        {
            MethodInfo process = typeof(GameTickManager).GetMethod(
                "ProcessSlowTickIfNeeded",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(process, "Could not find ProcessSlowTickIfNeeded method");
            process.Invoke(manager, new object[] { deltaTime });
        }

        [Test]
        public void ProcessSlowTickIfNeeded_BelowInterval_AccumulatesWithoutTicking()
        {
            GameTickManager manager = CreateManager(out GameObject owner);
            try
            {
                ProcessSlowTick(manager, 0.3f);

                Assert.That(
                    ReadAccumulator(manager),
                    Is.EqualTo(0.3f).Within(Tolerance),
                    "A delta below the interval must accumulate untouched.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProcessSlowTickIfNeeded_Overshoot_CarriesRemainderIntoNextWindow()
        {
            GameTickManager manager = CreateManager(out GameObject owner);
            try
            {
                // 0.7 s against a 0.5 s interval fires once and must leave 0.2 s banked.
                // Zeroing the accumulator here discarded that 0.2 s and made the cadence
                // depend on frame time.
                ProcessSlowTick(manager, 0.7f);

                Assert.That(
                    ReadAccumulator(manager),
                    Is.EqualTo(0.2f).Within(Tolerance),
                    "The overshoot must carry into the next window, not be discarded.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProcessSlowTickIfNeeded_RepeatedSubIntervalDeltas_HoldAverageCadence()
        {
            GameTickManager manager = CreateManager(out GameObject owner);
            try
            {
                // Twelve 0.1 s frames is 1.2 s of simulated time. With the remainder carried,
                // exactly two intervals are consumed and 0.2 s remains banked.
                for (int i = 0; i < 12; i++)
                    ProcessSlowTick(manager, 0.1f);

                Assert.That(
                    ReadAccumulator(manager),
                    Is.EqualTo(0.2f).Within(Tolerance),
                    "1.2 s of deltas must consume two 0.5 s intervals and bank 0.2 s.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ProcessSlowTickIfNeeded_LongHitch_ClampsCarriedDebtToOneInterval()
        {
            GameTickManager manager = CreateManager(out GameObject owner);
            try
            {
                // A 5 s hitch would otherwise bank 4.5 s and force a catch-up tick every frame
                // until it drained. The debt must be capped at one interval.
                ProcessSlowTick(manager, 5f);

                Assert.That(
                    ReadAccumulator(manager),
                    Is.EqualTo(Interval).Within(Tolerance),
                    "Carried debt after a hitch must be clamped to a single interval.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
