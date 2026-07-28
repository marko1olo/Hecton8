// ============================================================================
// HECTON-8 - PowerDrainBilling.cs
//
// Unity-free, allocation-free accounting math for the PowerDrainSignal lane.
//
// A tool that costs nothing to run is not a machine, it is a button
// (TASTE.md:174-189, "Machines Are Verbs"). PowerDrainSignal producers publish
// an instantaneous POWER figure in watts; the grid owner debits ENERGY in
// watt-seconds. This class owns that conversion and the two filters that decide
// which published drains are actually unbilled, so the same math is callable
// from the dispatcher-driven consumer and from an EditMode test.
//
// PURE C#: no UnityEngine, no Unity.Mathematics, no Burst. Compiles and runs
// outside the editor.
// ============================================================================

namespace Hecton8.Power
{
    /// <summary>
    /// Deterministic billing math for <c>PowerDrainSignal</c> payloads.
    /// Owns no state: the caller keeps the residual accumulator and passes it back in.
    /// </summary>
    public static class PowerDrainBilling
    {
        // --------------------------------------------------------------------
        // Producer contract mirror.
        //
        // The Reason byte is the payload's own discriminator for WHY power was
        // drawn, and it is what decides whether a drain is already metered by
        // another route. Both live values are mirrored here with their source,
        // because the producing constants are private to their owners:
        //
        //   Reason 0  LaserCutterDodRuntime.cs:1302 - handheld cutter. The tool
        //             is not an IPowerComponent and declares no PowerRating, so
        //             the grid solver never sees its draw. UNBILLED.
        //   Reason 1  Fabricator.cs:323 (PowerDrainReasonFabrication) - a
        //             grid-resident machine. Fabricator.cs:502 returns
        //             -craftPowerDraw from IPowerComponent.PowerRating and
        //             PowerGrid.cs:2863 already sums that into node demand
        //             (demandWatts += max(0, -consumer.PowerRating)). Billing it
        //             again here would charge the same craft twice.
        // --------------------------------------------------------------------

        /// <summary>Drain with no attributed owning machine: a handheld tool on the wireless budget.</summary>
        public const byte ReasonUnattributedTool = 0;

        /// <summary>Fabrication draw, already metered as node demand through <c>IPowerComponent.PowerRating</c>.</summary>
        public const byte ReasonFabrication = 1;

        /// <summary>Producer flag marking a stalled draw. A paused machine is not drawing.</summary>
        public const byte FlagPaused = 1 << 0;

        /// <summary>
        /// Smallest request worth submitting to the grid owner. The owner path rejects
        /// anything at or below 0.0001 W*s (PowerGridManager.cs:161, PowerGrid.cs:299),
        /// so submitting below this floor would silently discard the charge. Amounts
        /// under it are carried to the next tick instead of being lost.
        /// </summary>
        public const float MinSubmittedEnergyWattSeconds = 0.001f;

        /// <summary>Ceiling on carried sub-floor energy, so a permanently unpayable trickle cannot grow without bound.</summary>
        public const float MaxCarriedResidualWattSeconds = 1f;

        /// <summary>
        /// Longest tick window billed in one step. A frame hitch or a first-frame delta
        /// must not bill a lump the player never spent, so anything longer is truncated:
        /// the error direction is deliberately toward undercharging.
        /// </summary>
        public const float MaxBilledDeltaSeconds = 0.25f;

        /// <summary>Saturation ceiling for the running totals, so a long session cannot drift into infinity.</summary>
        public const float MaxAccumulatedEnergyWattSeconds = 1e12f;

        /// <summary>
        /// True when a published drain is real, live, and not already metered by another route.
        /// </summary>
        /// <param name="reason">Payload <c>Reason</c> byte.</param>
        /// <param name="flags">Payload <c>Flags</c> byte.</param>
        /// <returns>True when the drain must be charged against the wireless tool budget.</returns>
        public static bool IsBillableAsWirelessToolDrain(byte reason, byte flags)
        {
            if ((flags & FlagPaused) != 0)
                return false;

            return reason != ReasonFabrication;
        }

        /// <summary>
        /// Adds one payload watt figure to a running total, rejecting non-finite and non-positive values.
        /// </summary>
        /// <param name="totalWatts">Running total.</param>
        /// <param name="signalWatts">Payload <c>Watts</c> value.</param>
        /// <returns>The updated total; unchanged when the payload value is unusable.</returns>
        public static float AccumulateBillableWatts(float totalWatts, float signalWatts)
        {
            if (!IsFinite(signalWatts) || signalWatts <= 0f)
                return totalWatts;

            float sum = totalWatts + signalWatts;
            return IsFinite(sum) ? sum : totalWatts;
        }

        /// <summary>
        /// Converts an instantaneous watt total plus any carried remainder into the energy
        /// to submit to the grid owner this tick.
        /// </summary>
        /// <param name="billableWatts">Summed live drain power in watts.</param>
        /// <param name="deltaSeconds">Tick delta; truncated to <see cref="MaxBilledDeltaSeconds"/>.</param>
        /// <param name="carriedResidualWattSeconds">Energy withheld by previous ticks for being under the submit floor.</param>
        /// <param name="nextResidualWattSeconds">Residual to carry into the next tick.</param>
        /// <returns>Energy in watt-seconds to submit, or 0 when the total is still under the floor.</returns>
        public static float ResolveSubmittedEnergyWattSeconds(
            float billableWatts,
            float deltaSeconds,
            float carriedResidualWattSeconds,
            out float nextResidualWattSeconds)
        {
            float residual = SanitizeResidual(carriedResidualWattSeconds);
            float window = SanitizeDeltaSeconds(deltaSeconds);
            float watts = IsFinite(billableWatts) && billableWatts > 0f ? billableWatts : 0f;

            float energy = (watts * window) + residual;
            if (!IsFinite(energy) || energy <= 0f)
            {
                nextResidualWattSeconds = residual;
                return 0f;
            }

            if (energy < MinSubmittedEnergyWattSeconds)
            {
                nextResidualWattSeconds = SanitizeResidual(energy);
                return 0f;
            }

            nextResidualWattSeconds = 0f;
            return energy;
        }

        /// <summary>
        /// Clamps a tick delta into the billable window.
        /// </summary>
        /// <param name="deltaSeconds">Raw tick delta.</param>
        /// <returns>A finite delta in [0, <see cref="MaxBilledDeltaSeconds"/>].</returns>
        public static float SanitizeDeltaSeconds(float deltaSeconds)
        {
            if (!IsFinite(deltaSeconds) || deltaSeconds <= 0f)
                return 0f;

            return deltaSeconds > MaxBilledDeltaSeconds ? MaxBilledDeltaSeconds : deltaSeconds;
        }

        /// <summary>
        /// Clamps a carried residual into the bounded window.
        /// </summary>
        /// <param name="residualWattSeconds">Raw residual.</param>
        /// <returns>A finite residual in [0, <see cref="MaxCarriedResidualWattSeconds"/>].</returns>
        public static float SanitizeResidual(float residualWattSeconds)
        {
            if (!IsFinite(residualWattSeconds) || residualWattSeconds <= 0f)
                return 0f;

            return residualWattSeconds > MaxCarriedResidualWattSeconds
                ? MaxCarriedResidualWattSeconds
                : residualWattSeconds;
        }

        /// <summary>
        /// Adds to a saturating lifetime energy counter.
        /// </summary>
        /// <param name="totalWattSeconds">Running total.</param>
        /// <param name="addedWattSeconds">Amount to add.</param>
        /// <returns>The updated total, clamped to <see cref="MaxAccumulatedEnergyWattSeconds"/>.</returns>
        public static float AccumulateEnergyWattSeconds(float totalWattSeconds, float addedWattSeconds)
        {
            float total = IsFinite(totalWattSeconds) && totalWattSeconds > 0f ? totalWattSeconds : 0f;
            if (!IsFinite(addedWattSeconds) || addedWattSeconds <= 0f)
                return total;

            float sum = total + addedWattSeconds;
            if (!IsFinite(sum))
                return total;

            return sum > MaxAccumulatedEnergyWattSeconds ? MaxAccumulatedEnergyWattSeconds : sum;
        }

        /// <summary>
        /// Energy the grid owner could not pay for. This is a genuine brownout shortfall,
        /// not a rounding remainder, so it is reported rather than carried: the tool never
        /// received the charge and must not be made to owe it later.
        /// </summary>
        /// <param name="submittedWattSeconds">Energy requested.</param>
        /// <param name="grantedWattSeconds">Energy the owner actually reserved.</param>
        /// <returns>The unpaid difference, or 0 when the request was met.</returns>
        public static float ResolveUnpaidEnergyWattSeconds(float submittedWattSeconds, float grantedWattSeconds)
        {
            if (!IsFinite(submittedWattSeconds) || submittedWattSeconds <= 0f)
                return 0f;

            if (!IsFinite(grantedWattSeconds) || grantedWattSeconds <= 0f)
                return submittedWattSeconds;

            float unpaid = submittedWattSeconds - grantedWattSeconds;
            return IsFinite(unpaid) && unpaid > 0f ? unpaid : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
