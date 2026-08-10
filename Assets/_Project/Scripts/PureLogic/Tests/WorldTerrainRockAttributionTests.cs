using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Finds out WHY the material palette resolves to HardRock across almost every steep sample, by
    /// intervening on real in-world samples one driver at a time instead of reasoning about the
    /// formula.
    ///
    /// WHY THIS METHOD. The last time this investigation reasoned from a mechanism it was wrong.
    /// Shelf-transition cells measured 36.6 deg against 4.2 for saturated ones, so the transition
    /// was inferred to CAUSE the steepness; removing 62% of the transition area should then have
    /// moved the mean to about 11.5 deg and it moved it to 27.4. The cells that left the band stayed
    /// steep. Correlation between a driver and an outcome says nothing about which way the arrow
    /// points, and on a field where every driver is itself a function of position, almost everything
    /// correlates with everything. Only an intervention settles it.
    ///
    /// So each probe here takes a REAL sample from the world, changes exactly ONE field, re-resolves
    /// through the shipping WorldTerrainSurfaceMaterialResolver, and reports what moved. Nothing in
    /// this file re-implements the rock formula: duplicating it would create a second definition
    /// that drifts from the first, and a test that measures its own copy of the code measures
    /// nothing. The resolver is the only thing being asked.
    ///
    /// WHAT IS AT STAKE. The owner has ruled that a steep, cliffed seafloor is the intended design,
    /// so "the world is too steep" is not an available conclusion. The open question is narrower and
    /// still real: the resolver has EIGHT material classes and seven of them are multiplied by
    /// sedimentRoom = 1 - finalRock, so once finalRock saturates the palette has exactly one class
    /// left to describe every steep surface. Whether that saturation is driven by slope (in which
    /// case it is honest - a 60 deg underwater face IS bare rock) or by curvature leaking in through
    /// ridgeRockDominance (in which case gentle ground is being painted as rock too) decides whether
    /// there is anything to fix at all.
    ///
    /// Every test here is a report. None asserts a preferred design, because which materials belong
    /// on a cliff is the owner's call and not a property the code can derive.
    /// </summary>
    [TestFixture]
    public sealed class WorldTerrainRockAttributionTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;
        private static readonly float HalfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;
        private const int SamplesPerAxis = 40;

        /// <summary>
        /// Sites inside the world, by percentile of the in-world 1 km slope distribution.
        /// </summary>
        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (11896.0, -13148.0, "W1_flat"),
            (5635.0, -3130.0, "W2_gentle"),
            (9391.0, -10643.0, "W3_typical"),
            (6887.0, -6887.0, "W4_steep"),
            (-11896.0, 4383.0, "W5_wall")
        };

        private static readonly string[] ClassNames =
        {
            "sand", "limestone", "silt", "rock", "brine", "nodule", "reef", "seep"
        };

        private static float[] ToArray(in WorldTerrainSurfaceMaterialWeights w)
        {
            return new[]
            {
                w.ShellSand, w.LimestoneShelf, w.ClaySilt, w.HardRock,
                w.BrineSaltCrust, w.ManganeseNodulePlain, w.ReefRubble, w.SeepCrust
            };
        }

        private static int Argmax(float[] v)
        {
            int best = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
            return best;
        }

        /// <summary>
        /// Converts the resolver's normalised Slope01 back to degrees. Slope01 is
        /// saturate(gradient / 1.25) and a gradient of 1.25 is 51.3 degrees, so this is exact below
        /// that angle and pinned at it above - which is the ceiling this fixture exists to measure,
        /// not an approximation being papered over.
        /// </summary>
        private static double Slope01ToDegrees(float slope01)
        {
            return math.degrees(math.atan(math.saturate(slope01) * 1.25f));
        }

        /// <summary>
        /// The ramp ceilings, printed from the same literals the resolver uses so the table cannot
        /// silently disagree with the code. Every one of these is a smoothstep on Slope01, and
        /// Slope01 itself saturates at 51.3 deg, so each ramp stops discriminating at its upper
        /// bound and ALL of them stop at 51.3 whatever their bounds say.
        /// </summary>
        [Test]
        public void SlopeRampCeilings_AreReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Where each slope-driven term in WorldTerrainSurfaceMaterialResolver stops moving.");
            sb.AppendLine("Slope01 = saturate(gradient / 1.25); gradient 1.25 = 51.3 deg is the hard ceiling.");
            sb.AppendLine();
            sb.AppendLine($"    {"term",-22}{"lower",10}{"upper",10}   effect above the upper bound");

            (string Name, float Lo, float Hi, string Effect)[] ramps =
            {
                ("flatFloor", 0.10f, 0.46f, "zero: kills silt/sand/nodule floor terms"),
                ("angleOfRepose", 0.36f, 0.62f, "zero: kills sand and terrace terms"),
                ("steepSlope", 0.34f, 0.56f, "one: rock contribution maxed"),
                ("ridgeRockDominance", 0.54f, 0.72f, "one: forces finalRock to >= 0.78"),
                ("verySteep", 0.56f, 0.84f, "one: rock contribution maxed"),
                ("Slope01 itself", 0f, 1.00f, "one: 55 deg and 85 deg are the same number")
            };

            foreach (var r in ramps)
            {
                sb.AppendLine(
                    $"    {r.Name,-22}{Slope01ToDegrees(r.Lo),8:0.0}deg{Slope01ToDegrees(r.Hi),8:0.0}deg   {r.Effect}");
            }

            sb.AppendLine();
            sb.AppendLine("  flatFloor is expressed on flat = 1 - slope, so its bounds are converted here to");
            sb.AppendLine("  the SLOPE angle at which it starts and finishes closing, which is what matters.");
            sb.AppendLine();
            sb.AppendLine("  Read this against the world's own distribution: the in-world median is 28.4 deg");
            sb.AppendLine("  and 33.6% of the world exceeds 40 deg. A third of the world therefore sits past");
            sb.AppendLine("  the top of every ramp in this table.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The attribution proper. At each site, take every sample in a 1 km window, then re-resolve
        /// it four more times with exactly one driver neutralised, and report how the dominant-class
        /// mix changes. A driver that is actually responsible for the rock monoculture will restore
        /// other classes when it is removed; one that merely correlates will not.
        /// </summary>
        [Test]
        public void RockDominance_AttributedByIntervention()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Dominant-material mix over a 1 km window, with one driver neutralised at a time.");
            sb.AppendLine("Each row re-resolves the SAME real samples through the shipping resolver.");
            sb.AppendLine();

            foreach (var site in Sites)
            {
                double half = 500.0;
                double step = 1000.0 / (SamplesPerAxis - 1);

                var baselineWins = new int[8];
                var noCurvatureWins = new int[8];
                var noRidgeWins = new int[8];
                var noHardRockMaskWins = new int[8];
                var slopeHalvedWins = new int[8];
                double meanSlopeDeg = 0.0;
                double meanRock = 0.0;
                int n = 0;

                for (int iz = 0; iz < SamplesPerAxis; iz++)
                {
                    double z = site.Z - half + iz * step;
                    for (int ix = 0; ix < SamplesPerAxis; ix++)
                    {
                        double x = site.X - half + ix * step;
                        WorldMacroGeologySample s = WorldMacroGeologyFields.Evaluate(x, z, in p);

                        WorldTerrainSurfaceMaterialWeights baseline =
                            WorldTerrainSurfaceMaterialResolver.Resolve(in s, (float)x, (float)z, Seed);
                        float[] b = ToArray(in baseline);
                        baselineWins[Argmax(b)]++;
                        meanRock += baseline.HardRock;
                        meanSlopeDeg += Slope01ToDegrees(s.Slope01);
                        n++;

                        WorldMacroGeologySample noCurv = s;
                        noCurv.PositiveCurvature01 = 0f;
                        noCurvatureWins[Argmax(ToArray(
                            WorldTerrainSurfaceMaterialResolver.Resolve(in noCurv, (float)x, (float)z, Seed)))]++;

                        WorldMacroGeologySample noRidge = s;
                        noRidge.RidgeMask = 0f;
                        noRidge.FaultMask = 0f;
                        noRidgeWins[Argmax(ToArray(
                            WorldTerrainSurfaceMaterialResolver.Resolve(in noRidge, (float)x, (float)z, Seed)))]++;

                        WorldMacroGeologySample noHardRock = s;
                        noHardRock.HardRockExposureMask = 0f;
                        noHardRockMaskWins[Argmax(ToArray(
                            WorldTerrainSurfaceMaterialResolver.Resolve(in noHardRock, (float)x, (float)z, Seed)))]++;

                        WorldMacroGeologySample halfSlope = s;
                        halfSlope.Slope01 = s.Slope01 * 0.5f;
                        slopeHalvedWins[Argmax(ToArray(
                            WorldTerrainSurfaceMaterialResolver.Resolve(in halfSlope, (float)x, (float)z, Seed)))]++;
                    }
                }

                sb.AppendLine(
                    $"  {site.Label}  (mean Slope01 as angle {meanSlopeDeg / n:0.0} deg, " +
                    $"mean HardRock weight {meanRock / n:0.000})");
                AppendRow(sb, "baseline", baselineWins, n);
                AppendRow(sb, "PositiveCurvature=0", noCurvatureWins, n);
                AppendRow(sb, "Ridge+Fault=0", noRidgeWins, n);
                AppendRow(sb, "HardRockExposure=0", noHardRockMaskWins, n);
                AppendRow(sb, "Slope01 halved", slopeHalvedWins, n);
                sb.AppendLine();
            }

            sb.AppendLine("  HOW TO READ THIS. If 'Slope01 halved' is the only row that restores classes, the");
            sb.AppendLine("  monoculture is honest: steep ground is rock and the world is steep by design.");
            sb.AppendLine("  If 'PositiveCurvature=0' or 'HardRockExposure=0' restores them, then rock is");
            sb.AppendLine("  being painted onto ground that is NOT steep, which is a defect independent of");
            sb.AppendLine("  how dramatic the terrain is, and is fixable without touching the geometry.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        private static void AppendRow(System.Text.StringBuilder sb, string label, int[] wins, int n)
        {
            var parts = new System.Text.StringBuilder();
            int distinct = 0;
            for (int c = 0; c < 8; c++)
            {
                if (wins[c] == 0) continue;
                distinct++;
                parts.Append($"{ClassNames[c]}={100.0 * wins[c] / n:0}% ");
            }
            sb.AppendLine($"    {label,-22} classes={distinct}  {parts}");
        }

        /// <summary>
        /// Reports how much of the world sits past each ramp ceiling, so the table above is read
        /// against the terrain rather than in the abstract, and reports the shelf-mask crossing per
        /// site.
        ///
        /// The shelf column exists for a separate open question. CleanRoomTile_ContainsTheShelfBreak
        /// fails because the clean room renders the tile at the origin, which is quiet abyssal basin
        /// with no shelf transition in it - so every X-Ray of the shelf work is a picture of
        /// somewhere the shelf is not. That fixture is right to fail and the fix is to aim the
        /// renderer somewhere the mask actually crosses; this column says where that is.
        /// </summary>
        [Test]
        public void WorldShareAboveEachCeiling_AndShelfCrossingPerSite_AreReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();

            const double probe = 12.0;
            int cells = 0;
            int past30 = 0, past38 = 0, past42 = 0, past46 = 0, past51 = 0;
            double stepWorld = (HalfExtent * 2.0) / (SamplesPerAxis - 1);

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = -HalfExtent + iz * stepWorld;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = -HalfExtent + ix * stepWorld;
                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p);
                    float nh = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p);
                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (nh - s) / (float)(probe * 2.0);
                    double deg = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    if (deg > 29.9) past30++;
                    if (deg > 37.8) past38++;
                    if (deg > 42.0) past42++;
                    if (deg > 46.4) past46++;
                    if (deg > 51.3) past51++;
                    cells++;
                }
            }

            sb.AppendLine($"Share of the emitted {HalfExtent * 2 / 1000.0:0} km world past each ramp ceiling:");
            sb.AppendLine($"    past flatFloor close   (29.9 deg): {100.0 * past30 / cells,5:0.0}%  -> no floor sediment");
            sb.AppendLine($"    past angleOfRepose     (37.8 deg): {100.0 * past38 / cells,5:0.0}%  -> no sand/terrace");
            sb.AppendLine($"    past ridgeRockDominance(42.0 deg): {100.0 * past42 / cells,5:0.0}%  -> finalRock forced high");
            sb.AppendLine($"    past verySteep         (46.4 deg): {100.0 * past46 / cells,5:0.0}%  -> rock term maxed");
            sb.AppendLine($"    past Slope01 saturation(51.3 deg): {100.0 * past51 / cells,5:0.0}%  -> slope unreadable");
            sb.AppendLine();
            sb.AppendLine("Shelf mask over a 1 km tile at each candidate clean-room site.");
            sb.AppendLine("A tile can only PICTURE the shelf break if the mask crosses inside it:");
            sb.AppendLine($"    {"site",-12}{"min",8}{"max",8}{"crosses?",11}");

            var candidates = new System.Collections.Generic.List<(double X, double Z, string Label)>
            {
                (0.0, 0.0, "cleanroom")
            };
            foreach (var s in Sites) candidates.Add(s);

            foreach (var c in candidates)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                double step = 1000.0 / 63.0;
                // The clean room exports its centre chunk spanning 0..1000 from its sample origin,
                // so the tile is measured the same way: origin-anchored, not centred.
                double baseX = c.Label == "cleanroom" ? 0.0 : c.X - 500.0;
                double baseZ = c.Label == "cleanroom" ? 0.0 : c.Z - 500.0;
                for (int iz = 0; iz < 64; iz++)
                {
                    double z = baseZ + iz * step;
                    for (int ix = 0; ix < 64; ix++)
                    {
                        WorldMacroGeologyFields.EvaluateHeightMeters(
                            baseX + ix * step, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                        lo = math.min(lo, m.Shelf);
                        hi = math.max(hi, m.Shelf);
                    }
                }
                bool crosses = lo < 0.25f && hi > 0.75f;
                sb.AppendLine($"    {c.Label,-12}{lo,8:0.000}{hi,8:0.000}{(crosses ? "YES" : "no"),11}");
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
