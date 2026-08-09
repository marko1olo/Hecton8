using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Pins the material-noise early-exit gate in
    /// <see cref="WorldTerrainSurfaceMaterialResolver.Resolve"/>
    /// (Scripts/World/WorldTerrainDetailContracts.cs, the <c>jitterGate</c> block).
    ///
    /// WHAT THE GATE IS FOR: when a sample is pure rock there is no sediment left to patch, so
    /// skipping three fractal-noise evaluations is free. The gate expresses that as
    /// <c>jitterGate = smoothstep(0.98, 0.85, finalRock)</c>, and when it closes the three noises
    /// stay at their placeholder value of 0.5 instead of being sampled.
    ///
    /// WHY IT NEEDS A TEST: <c>finalRock</c> is force-raised TWICE before the gate is evaluated -
    /// once by <c>ridgeRockDominance * (0.78f - finalRock * 0.42f)</c>, and <c>ridgeRockDominance</c>
    /// itself saturates from slope alone via <c>smoothstep(0.54f, 0.72f, slope)</c>. So the gate can
    /// close on ordinary sloped seafloor that still carries sediment, not only on bare cliff faces.
    /// When it does, <c>provinceJitter</c>, <c>localPatch</c> and <c>finePatch</c> are identical
    /// across every such sample, which erases the per-sample material variation that
    /// <c>ShellSand</c> (via <c>patchContrast</c>/<c>shellHash</c>) and <c>ManganeseNodulePlain</c>
    /// (via <c>provinceJitter</c>) depend on.
    ///
    /// These tests are deliberately measurements, not assertions of a desired design: they record
    /// the slope at which the gate closes and prove the resolver still produces a normalised,
    /// finite, single-dominant result on both sides of it. The vertical/authoring decision about
    /// whether the gate SHOULD close that early belongs to the owner, so no test here asserts a
    /// preferred threshold.
    /// </summary>
    [TestFixture]
    public sealed class WorldTerrainMaterialJitterGateTests
    {
        private const uint Seed = 880031u;

        /// <summary>
        /// A mid-shelf sediment sample: shallow, sedimented, no ridge/fault/trench, and no
        /// hard-rock exposure. Slope is the single variable under test.
        /// </summary>
        private static WorldMacroGeologySample CreateSedimentShelfSample(float slope01)
        {
            return new WorldMacroGeologySample
            {
                HeightMeters = -120f,
                DepthMeters = 120f,
                ShelfMask = 1f,
                ShelfBreakMask = 0f,
                RidgeMask = 0f,
                TrenchMask = 0f,
                BasinMask = 0f,
                FaultMask = 0f,
                SedimentMask = 1f,
                SeepMask = 0f,
                Slope01 = slope01,
                Curvature01 = 0f,
                PositiveCurvature01 = 0f,
                NegativeCurvature01 = 0f,
                ErosionFlow01 = 0f,
                TerraceMask = 0f,
                SlumpScarMask = 0f,
                TributaryCanyonMask = 0f,
                NodulePlainMask = 0f,
                ReefEligibilityMask = 0f,
                HardRockExposureMask = 0f,
                VoxelSeamMask = 0f,
                CraterMask = 0f,
                PrimaryZone = WorldMacroGeologyZone.PhoticShelf
            };
        }

        private static float SumWeights(in WorldTerrainSurfaceMaterialWeights w)
        {
            return w.ShellSand + w.LimestoneShelf + w.ClaySilt + w.HardRock +
                   w.BrineSaltCrust + w.ManganeseNodulePlain + w.ReefRubble + w.SeepCrust;
        }

        /// <summary>
        /// On flat sedimented shelf the gate must stay OPEN, otherwise sand/silt patching is
        /// constant across the whole seafloor. This is the case the gate was never meant to catch.
        /// </summary>
        [Test]
        public void FlatSedimentShelf_ProducesSedimentDominatedWeights()
        {
            WorldMacroGeologySample sample = CreateSedimentShelfSample(0f);

            WorldTerrainSurfaceMaterialWeights weights =
                WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 1024f, 2048f, Seed);

            Assert.Greater(
                weights.ShellSand + weights.ClaySilt,
                weights.HardRock,
                "Flat, fully sedimented shelf resolved as rock-dominant. Sediment must win where " +
                "slope is zero and HardRockExposureMask is zero.");
        }

        /// <summary>
        /// Two samples at DIFFERENT world positions must not resolve to bit-identical weights
        /// WHERE SEDIMENT SURVIVES. Identical output there is the signature of the material patch
        /// noise having collapsed to a constant, which is what the argument-binding defect in
        /// <c>DoubleFractalSimplexNoise01</c> caused before its pre-scaled-position overload existed.
        ///
        /// The sediment guard is not a softening of the assertion, it is the actual contract.
        /// Measured by <see cref="WorldTerrainSteepSlopeMaterialTests"/>: at slope01 &gt;= 0.7 this
        /// sample resolves <c>finalRock</c> to 1, so <c>sedimentRoom = 1 - finalRock</c> is 0 and
        /// every sediment weight is 0 whatever the noise returns. Demanding positional variation
        /// there would demand variation in a value that is identically zero by construction - a
        /// bare rock face is legitimately uniform. An earlier revision of this test asserted
        /// exactly that and failed for the wrong reason.
        /// </summary>
        [Test]
        public void SlopedShelf_PositionStillVariesMaterialWeights()
        {
            float[] slopes = { 0.30f, 0.50f, 0.60f, 0.70f, 0.80f, 0.90f };
            var collapsedSlopes = new System.Collections.Generic.List<string>();

            for (int i = 0; i < slopes.Length; i++)
            {
                WorldMacroGeologySample sample = CreateSedimentShelfSample(slopes[i]);

                WorldTerrainSurfaceMaterialWeights a =
                    WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 1024f, 2048f, Seed);
                WorldTerrainSurfaceMaterialWeights b =
                    WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 71680f, 43008f, Seed);

                float sediment = a.ShellSand + a.LimestoneShelf + a.ClaySilt + a.ManganeseNodulePlain;
                if (sediment < 0.02f)
                    continue;

                bool identical =
                    math.abs(a.ShellSand - b.ShellSand) < 1e-7f &&
                    math.abs(a.ClaySilt - b.ClaySilt) < 1e-7f &&
                    math.abs(a.ManganeseNodulePlain - b.ManganeseNodulePlain) < 1e-7f &&
                    math.abs(a.HardRock - b.HardRock) < 1e-7f;

                if (identical)
                    collapsedSlopes.Add($"slope01={slopes[i]:0.00} sediment={sediment:0.000}");
            }

            Assert.That(
                collapsedSlopes,
                Is.Empty,
                "Sediment-bearing material weights became position-independent at " +
                string.Join("; ", collapsedSlopes) +
                ". Two points 70 km apart resolved bit-identical weights while sediment was " +
                "present, so provinceJitter/localPatch/finePatch were constants rather than " +
                "fields.");
        }

        /// <summary>
        /// Whatever the gate does, the resolver's own contract must hold on both sides of it:
        /// weights finite, non-negative, and at least one non-zero so a dominant class exists.
        /// A gate that produced an all-zero weight set would make the splatmap undefined.
        /// </summary>
        [Test]
        public void AllSlopes_ResolveToFiniteNonDegenerateWeights()
        {
            for (int step = 0; step <= 20; step++)
            {
                float slope = step / 20f;
                WorldMacroGeologySample sample = CreateSedimentShelfSample(slope);

                WorldTerrainSurfaceMaterialWeights weights =
                    WorldTerrainSurfaceMaterialResolver.Resolve(in sample, 5120f, 9216f, Seed);

                float total = SumWeights(in weights);

                Assert.IsTrue(
                    math.isfinite(total),
                    $"Non-finite total material weight at slope01={slope}.");
                Assert.Greater(
                    total,
                    0f,
                    $"All eight material weights resolved to zero at slope01={slope}; the " +
                    "splatmap would have no dominant class to write.");
            }
        }
    }
}
