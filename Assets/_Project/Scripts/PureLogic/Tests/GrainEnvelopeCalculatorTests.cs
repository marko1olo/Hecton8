using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class GrainEnvelopeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Position 0.5 symmetric: peak. (0.5 attack, 0.5 decay forms perfect Hann)
            float peak = GrainEnvelopeCalculator.Compute(0.5f, 0.5f, 0.5f);
            Assert.AreEqual(1f, peak, 0.0001f);

            // Position 0 and 1: amplitude 0
            float start = GrainEnvelopeCalculator.Compute(0f, 0.5f, 0.5f);
            Assert.AreEqual(0f, start, 0.0001f);

            float end = GrainEnvelopeCalculator.Compute(1f, 0.5f, 0.5f);
            Assert.AreEqual(0f, end, 0.0001f);

            // Quarter positions (Hann window at 0.25 is 0.5)
            float quarter = GrainEnvelopeCalculator.Compute(0.25f, 0.5f, 0.5f);
            Assert.AreEqual(0.5f, quarter, 0.0001f);

            // Sustain phase check (0.2 attack, 0.2 decay => middle 0.6 is 1.0)
            float sustain1 = GrainEnvelopeCalculator.Compute(0.3f, 0.2f, 0.2f);
            float sustain2 = GrainEnvelopeCalculator.Compute(0.7f, 0.2f, 0.2f);
            Assert.AreEqual(1f, sustain1, 0.0001f);
            Assert.AreEqual(1f, sustain2, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Fractions sum > 1
            float val = GrainEnvelopeCalculator.Compute(0.5f, 1f, 1f);
            // internally attack=0.5, decay=0.5, should be 1.0 at 0.5
            Assert.AreEqual(1f, val, 0.0001f);

            // Boundary positions slightly out of bounds
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(-0.1f, 0.5f, 0.5f), 0.0001f);
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(1.1f, 0.5f, 0.5f), 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Zero attack and zero decay
            Assert.AreEqual(1f, GrainEnvelopeCalculator.Compute(0.5f, 0f, 0f), 0.0001f);

            // Boundary at exactly 0 and 1 should still be 0 to avoid clicks
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(0f, 0f, 0f), 0.0001f);
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(1f, 0f, 0f), 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative attack/decay clamped to 0
            Assert.AreEqual(1f, GrainEnvelopeCalculator.Compute(0.5f, -0.5f, -0.5f), 0.0001f);
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(0f, -0.5f, -0.5f), 0.0001f);
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(1f, -0.5f, -0.5f), 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // NaN and Infinity
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(float.NaN, 0.5f, 0.5f));
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(0.5f, float.NaN, 0.5f));
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(0.5f, 0.5f, float.NaN));

            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(float.PositiveInfinity, 0.5f, 0.5f));
            Assert.AreEqual(0f, GrainEnvelopeCalculator.Compute(float.NegativeInfinity, 0.5f, 0.5f));

            Assert.AreEqual(1f, GrainEnvelopeCalculator.Compute(0.5f, float.PositiveInfinity, float.PositiveInfinity), 0.0001f);
        }
    }
}
