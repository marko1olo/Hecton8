using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Records what <see cref="WorldTerrainSurfaceMaterialResolver.Resolve"/> actually returns on a
    /// steep sedimented shelf, so the material-noise gate's behaviour there is decided by numbers
    /// rather than by reading the code.
    ///
    /// Context: the gate at <c>WorldTerrainDetailContracts.cs</c> skips three fractal noises when
    /// <c>finalRock</c> is near 1, on the stated grounds that a pure rock face has no sediment left
    /// to patch. The question this fixture answers is whether that premise holds at the slope where
    /// the gate actually closes - if <c>sedimentRoom = 1 - finalRock</c> is zero there, every
    /// sediment weight is zero whatever the noise says, and skipping the noise costs nothing.
    /// If sediment survives, the gate is discarding variation that would have been visible.
    /// </summary>
    [TestFixture]
    public sealed class WorldTerrainSteepSlopeMaterialTests
    {
        private const uint Seed = 880031u;

        private static WorldMacroGeologySample CreateSedimentShelfSample(float slope01)
        {
            return new WorldMacroGeologySample
            {
                HeightMeters = -120f,
                DepthMeters = 120f,
                ShelfMask = 1f,
                SedimentMask = 1f,
                Slope01 = slope01,
                PrimaryZone = WorldMacroGeologyZone.PhoticShelf
            };
        }

        /// <summary>
        /// At the slope where the gate closes, rock must be the dominant class and the sediment
        /// classes must be at zero. If that holds, the gate's premise is sound and the constant
        /// noise is unobservable; the assertion message carries the measured weights either way.
        /// </summary>
        [Test]
        public void SteepSlope_ResolvesToRockWithNoSedimentRoom()
        {
            WorldMacroGeologySample sample = CreateSedimentShelfSample(0.7f);

            WorldTerrainSurfaceMaterialWeights w =
                WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 1024f, 2048f, Seed);

            float sediment = w.ShellSand + w.LimestoneShelf + w.ClaySilt + w.ManganeseNodulePlain;

            Assert.That(
                w.HardRock,
                Is.GreaterThan(sediment),
                $"slope01=0.7 did not resolve rock-dominant. HardRock={w.HardRock:R} " +
                $"ShellSand={w.ShellSand:R} LimestoneShelf={w.LimestoneShelf:R} " +
                $"ClaySilt={w.ClaySilt:R} ManganeseNodulePlain={w.ManganeseNodulePlain:R} " +
                $"BrineSaltCrust={w.BrineSaltCrust:R} ReefRubble={w.ReefRubble:R} " +
                $"SeepCrust={w.SeepCrust:R}");

            Assert.That(
                sediment,
                Is.LessThan(0.02f),
                $"Sediment survived on a slope steep enough to close the material-noise gate " +
                $"(total sediment weight {sediment:R}). Any sediment left here is patched by a " +
                $"noise that the gate pinned to the constant 0.5, so the variation is lost rather " +
                $"than unobservable. Weights: HardRock={w.HardRock:R} ShellSand={w.ShellSand:R} " +
                $"LimestoneShelf={w.LimestoneShelf:R} ClaySilt={w.ClaySilt:R} " +
                $"ManganeseNodulePlain={w.ManganeseNodulePlain:R}");
        }

        /// <summary>
        /// The complementary half: where sediment DOES survive, position must still matter. This is
        /// the contract the world actually needs, stated without reference to any slope threshold.
        /// </summary>
        [Test]
        public void WhereverSedimentSurvives_PositionVariesTheWeights()
        {
            var offenders = new System.Collections.Generic.List<string>();

            for (int step = 0; step <= 20; step++)
            {
                float slope = step / 20f;
                WorldMacroGeologySample sample = CreateSedimentShelfSample(slope);

                WorldTerrainSurfaceMaterialWeights a =
                    WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 1024f, 2048f, Seed);
                WorldTerrainSurfaceMaterialWeights b =
                    WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 71680f, 43008f, Seed);

                float sedimentA = a.ShellSand + a.LimestoneShelf + a.ClaySilt + a.ManganeseNodulePlain;
                if (sedimentA < 0.02f)
                    continue;

                bool identical =
                    math.abs(a.ShellSand - b.ShellSand) < 1e-7f &&
                    math.abs(a.ClaySilt - b.ClaySilt) < 1e-7f &&
                    math.abs(a.ManganeseNodulePlain - b.ManganeseNodulePlain) < 1e-7f;

                if (identical)
                    offenders.Add($"slope01={slope:0.00} sediment={sedimentA:0.000}");
            }

            Assert.That(
                offenders,
                Is.Empty,
                "Sediment-bearing samples resolved position-independent weights at: " +
                string.Join("; ", offenders) +
                ". Sediment is present, so the material patch noise is observable there and must " +
                "not be pinned to a constant.");
        }
    }
}
