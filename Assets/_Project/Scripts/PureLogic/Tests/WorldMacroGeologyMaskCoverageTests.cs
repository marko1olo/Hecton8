using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures the coverage of every macro-geology mask over a wide sample of the world, so that a
    /// mask which never fires is a number in a failure message rather than something noticed months
    /// later by looking at a picture.
    ///
    /// Motivating measurement (Logs/geology_atlas/atlas_report.txt, 2026-08-09, 15 windows across 5
    /// sites and 3 scales): Ridge 0.4-49.1%, Trench 0-31.2%, Basin 4.7-99.6%, Shelf 0-92.5%,
    /// HardRock 0-50.8%, Fault 4.7-52.1%, Dune 0-29.0%, Volcano 0.1-17.1% - all alive and varied.
    /// Against that, Canyon 0.0-0.4%, River 0.0-0.4%, Lake 0.0-0.2%, Fold 0.0-0.1% and Mesa 0.0-0.3%
    /// never rose above half a percent anywhere.
    ///
    /// terrain.md:181-183 makes the canyon layer mandatory and names its purpose: "This layer
    /// produces the readable trench and ravine routes players navigate. Output must produce
    /// branching dendritic drainage patterns visible on a 1 km slope X-Ray card." terrain.md:249
    /// repeats it as the 1 km acceptance row. A mask at 0.0% cannot produce a route.
    ///
    /// These tests assert a FLOOR, not a target. The floor is deliberately far below anything that
    /// could be called good (0.5% of sampled area) so it only fires for a feature that is
    /// effectively absent, and so tuning the geology cannot break it accidentally.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyMaskCoverageTests
    {
        private const uint Seed = 880031u;

        /// <summary>Sampling grid per site. 64x64 over each window = 4096 samples per site.</summary>
        private const int SamplesPerAxis = 64;

        /// <summary>
        /// The five probe sites used by the project's own geology atlas
        /// (Editor/Diagnostics/GeologyAtlasTask.cs), reused verbatim so this test and the atlas
        /// describe the same places. P5 is 777 km out, which also exercises double-precision
        /// coordinates far from the origin.
        /// </summary>
        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (5000.0, 5000.0, "P1_origin"),
            (50000.0, 50000.0, "P2_near"),
            (-40000.0, 15000.0, "P3_west"),
            (300000.0, 90000.0, "P4_far"),
            (777000.0, -333000.0, "P5_deepfar")
        };

        /// <summary>Window half-size in metres; 5000 gives each site a 10 km x 10 km window.</summary>
        private const double WindowHalfMeters = 5000.0;

        private struct Coverage
        {
            public double Canyon;
            public double River;
            public double Lake;
            public double Fold;
            public double Mesa;
            public double Ridge;
            public double Trench;
            public double Shelf;
            public int Samples;
        }

        private static Coverage MeasureCoverage(double centerX, double centerZ)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            Coverage c = default;

            double step = (WindowHalfMeters * 2.0) / (SamplesPerAxis - 1);
            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - WindowHalfMeters + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - WindowHalfMeters + ix * step;

                    WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);

                    // "Covered" means the mask is meaningfully present, not merely non-zero. 0.05
                    // keeps floating-point dust and the far tails of a smoothstep from counting as
                    // a feature, which is what makes a 0.0% reading trustworthy.
                    if (m.Canyon > 0.05f) c.Canyon++;
                    if (m.River > 0.05f) c.River++;
                    if (m.Lake > 0.05f) c.Lake++;
                    if (m.Fold > 0.05f) c.Fold++;
                    if (m.Mesa > 0.05f) c.Mesa++;
                    if (m.Ridge > 0.05f) c.Ridge++;
                    if (m.Trench > 0.05f) c.Trench++;
                    if (m.Shelf > 0.05f) c.Shelf++;
                    c.Samples++;
                }
            }

            return c;
        }

        /// <summary>
        /// The canyon layer must appear SOMEWHERE in the world. Asserted across all five sites
        /// combined rather than per-site, because a canyon province legitimately does not cover
        /// every place - but a canyon layer that fires nowhere is the dead layer terrain.md:183
        /// forbids.
        /// </summary>
        [Test]
        public void CanyonLayer_AppearsSomewhereInTheWorld()
        {
            double best = 0.0;
            string bestSite = "<none>";
            var perSite = new System.Collections.Generic.List<string>();

            for (int i = 0; i < Sites.Length; i++)
            {
                Coverage c = MeasureCoverage(Sites[i].X, Sites[i].Z);
                double pct = 100.0 * c.Canyon / c.Samples;
                perSite.Add($"{Sites[i].Label}={pct:0.00}%");
                if (pct > best)
                {
                    best = pct;
                    bestSite = Sites[i].Label;
                }
            }

            Assert.That(
                best,
                Is.GreaterThan(0.5),
                "The canyon/ravine layer is effectively absent everywhere sampled. Best site was " +
                $"{bestSite} at {best:0.00}%. Per site: {string.Join(" ", perSite)}. " +
                "terrain.md:181-183 requires this layer to produce the trench and ravine routes " +
                "players navigate, and terrain.md:249 makes branching dendritic drainage the 1 km " +
                "acceptance criterion. Coverage below 0.5% cannot produce a route.");
        }

        /// <summary>
        /// Control: masks already proven alive by the atlas must stay alive. Without this, the
        /// canyon test above could be "fixed" by a change that floods every mask, and the suite
        /// would still be green.
        /// </summary>
        [Test]
        public void StructuralMasks_StayAlive()
        {
            Coverage c = MeasureCoverage(Sites[1].X, Sites[1].Z);

            double ridge = 100.0 * c.Ridge / c.Samples;
            double shelf = 100.0 * c.Shelf / c.Samples;

            Assert.That(
                ridge,
                Is.GreaterThan(0.5),
                $"Ridge mask collapsed to {ridge:0.00}% at P2_near; the atlas measured 19.7% there.");
            Assert.That(
                shelf,
                Is.GreaterThan(0.5),
                $"Shelf mask collapsed to {shelf:0.00}% at P2_near; the atlas measured 78.4% there.");
        }

        /// <summary>
        /// Guards the opposite failure: a mask that covers nearly everything is as useless as one
        /// that covers nothing, because it stops discriminating between places. Applied to the
        /// masks that select terrain character rather than to the depth-province masks, which are
        /// legitimately near-total inside their own province (the atlas measured Basin at 99.6% on
        /// the abyssal plain, which is correct).
        /// </summary>
        [Test]
        public void CharacterMasks_DoNotSaturateTheWholeWindow()
        {
            for (int i = 0; i < Sites.Length; i++)
            {
                Coverage c = MeasureCoverage(Sites[i].X, Sites[i].Z);

                double canyon = 100.0 * c.Canyon / c.Samples;
                double lake = 100.0 * c.Lake / c.Samples;

                Assert.That(
                    canyon,
                    Is.LessThan(95.0),
                    $"Canyon mask covers {canyon:0.00}% of {Sites[i].Label}; a canyon everywhere is " +
                    "not a canyon.");
                Assert.That(
                    lake,
                    Is.LessThan(95.0),
                    $"Lake mask covers {lake:0.00}% of {Sites[i].Label}.");
            }
        }
    }
}
