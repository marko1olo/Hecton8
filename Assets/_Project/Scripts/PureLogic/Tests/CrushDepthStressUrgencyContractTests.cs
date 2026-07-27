using NUnit.Framework;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Pins the two argument contracts that <c>HectonPlayerMovement.UpdateHullStress</c> relies on
    /// when it feeds <see cref="HudCrushDepthWarningUrgencyCalculator"/> as a floor under crush-depth
    /// hull stress.
    ///
    /// Regression guarded: the call site used to pass <c>crushDepthStart</c> (the depth where stress
    /// BEGINS) as the denominator, inside a branch that only runs once current depth already exceeds
    /// crushDepthStart. Urgency was therefore pinned at 1.0, hull stress became a constant, and
    /// crushDepthFullDepth / crushDepthRateForFullStress / PressureDamageScale were all dead. It also
    /// passed an up-positive world velocity where this calculator expects a descent-positive rate.
    /// </summary>
    [TestFixture]
    public class CrushDepthStressUrgencyContractTests
    {
        // Shipped serialized defaults on HectonPlayerMovement.
        private const float CrushDepthStart = 1000f;
        private const float CrushDepthFullDepth = 1450f;
        private const float CrushDepthImplosionThreshold = 0.985f;

        [Test]
        public void StressOnsetDepthAsDenominator_SaturatesAcrossTheWholeBand()
        {
            // Why crushDepthStart must never be the denominator: every depth the branch can observe
            // is already past it, so the result is a constant and the band carries no information.
            foreach (float depth in new[] { 1001f, 1050f, 1200f, 1449f })
            {
                float urgency = HudCrushDepthWarningUrgencyCalculator.Compute(depth, CrushDepthStart, 0f);
                Assert.AreEqual(1f, urgency, 0.001f,
                    "Dividing by the stress-onset depth saturates, which is what collapsed the gradient.");
            }
        }

        [Test]
        public void CrushLimitAsDenominator_GivesAMonotonicGradientAcrossTheBand()
        {
            float atOnset = HudCrushDepthWarningUrgencyCalculator.Compute(CrushDepthStart, CrushDepthFullDepth, 0f);
            float atMid = HudCrushDepthWarningUrgencyCalculator.Compute(1200f, CrushDepthFullDepth, 0f);
            float atLimit = HudCrushDepthWarningUrgencyCalculator.Compute(CrushDepthFullDepth, CrushDepthFullDepth, 0f);

            Assert.AreEqual(1000f / 1450f, atOnset, 0.001f);
            Assert.AreEqual(1200f / 1450f, atMid, 0.001f);
            Assert.AreEqual(1f, atLimit, 0.001f);

            Assert.Less(atOnset, atMid, "Urgency must rise with depth.");
            Assert.Less(atMid, atLimit, "Urgency must rise with depth.");
            Assert.Less(atOnset, CrushDepthImplosionThreshold,
                "Crossing the stress-onset depth must not immediately trip the implosion threshold.");
            Assert.Less(atMid, CrushDepthImplosionThreshold,
                "Mid-band depth must remain survivable.");
        }

        [Test]
        public void RateArgumentIsDescentPositive()
        {
            // The call site negates the up-positive world velocity because of this contract. If the
            // calculator's sign convention is ever flipped, that negation becomes wrong.
            float descending = HudCrushDepthWarningUrgencyCalculator.Compute(1200f, CrushDepthFullDepth, 9f);
            float stationary = HudCrushDepthWarningUrgencyCalculator.Compute(1200f, CrushDepthFullDepth, 0f);
            float ascending = HudCrushDepthWarningUrgencyCalculator.Compute(1200f, CrushDepthFullDepth, -9f);

            Assert.Greater(descending, stationary, "A positive rate must project deeper, raising urgency.");
            Assert.Less(ascending, stationary, "A negative rate must project shallower, relieving urgency.");
            Assert.AreEqual(1209f / 1450f, descending, 0.001f);
            Assert.AreEqual(1191f / 1450f, ascending, 0.001f);
        }

        /// <summary>
        /// Mirrors the band rescale in UpdateHullStress: the hull's combined pressure damage scale is
        /// clamped to the envelope PlayerTransportPreset authorises (0.25..2) and inverted, then both
        /// the onset depth and the crush limit are multiplied by it.
        /// </summary>
        private static float EffectiveCrushLimit(float pressureDamageScale)
        {
            float clamped = pressureDamageScale < 0.25f ? 0.25f : (pressureDamageScale > 2f ? 2f : pressureDamageScale);
            return CrushDepthFullDepth * (1f / clamped);
        }

        [Test]
        public void BaselineHullRatingLeavesTheAuthoredBandUntouched()
        {
            // A preset at the default pressureDamageScale of 1.0 must not shift the crush band at
            // all, so the rescale cannot regress the shipped tuning.
            Assert.AreEqual(CrushDepthFullDepth, EffectiveCrushLimit(1f), 0.001f);

            float atLimit = HudCrushDepthWarningUrgencyCalculator.Compute(
                CrushDepthFullDepth, EffectiveCrushLimit(1f), 0f);
            Assert.AreEqual(1f, atLimit, 0.001f);
        }

        [Test]
        public void BetterRatedHullSurvivesDepthThatImplodesBaseline()
        {
            // The design lock: a deep-rated hull must hold where a baseline hull is already gone.
            const float DepthThatKillsBaseline = 2000f;

            float baseline = HudCrushDepthWarningUrgencyCalculator.Compute(
                DepthThatKillsBaseline, EffectiveCrushLimit(1f), 0f);
            float deepRated = HudCrushDepthWarningUrgencyCalculator.Compute(
                DepthThatKillsBaseline, EffectiveCrushLimit(0.25f), 0f);

            Assert.GreaterOrEqual(baseline, CrushDepthImplosionThreshold, "Baseline hull must be lost here.");
            Assert.Less(deepRated, CrushDepthImplosionThreshold, "Deep-rated hull must survive here.");

            // Monotonic across tiers: a lower damage scale always means a deeper limit.
            Assert.Greater(EffectiveCrushLimit(0.25f), EffectiveCrushLimit(0.5f));
            Assert.Greater(EffectiveCrushLimit(0.5f), EffectiveCrushLimit(1f));
            Assert.Greater(EffectiveCrushLimit(1f), EffectiveCrushLimit(2f));
        }

        [Test]
        public void StackedUpgradesCannotPushTheLimitToAnAbsurdDepth()
        {
            // preset 0.25 * upgrade 0.1 = 0.025 would be a 40x band stretch without the clamp.
            Assert.AreEqual(EffectiveCrushLimit(0.25f), EffectiveCrushLimit(0.025f), 0.001f,
                "The clamp must cap the stretch at the authored envelope.");
            Assert.Less(EffectiveCrushLimit(0.025f), 6000f);
        }

        [Test]
        public void ImplosionIsReachedNearTheCrushLimitNotAtTheOnsetDepth()
        {
            // Stationary implosion depth = threshold * limit. The defect put this at the onset depth.
            float implosionDepth = CrushDepthImplosionThreshold * CrushDepthFullDepth;
            Assert.Greater(implosionDepth, 1400f,
                "Implosion must sit near the crush limit, not just past the onset depth.");

            float justBelow = HudCrushDepthWarningUrgencyCalculator.Compute(implosionDepth - 10f, CrushDepthFullDepth, 0f);
            float justAbove = HudCrushDepthWarningUrgencyCalculator.Compute(implosionDepth + 10f, CrushDepthFullDepth, 0f);
            Assert.Less(justBelow, CrushDepthImplosionThreshold);
            Assert.GreaterOrEqual(justAbove, CrushDepthImplosionThreshold);
        }
    }
}
