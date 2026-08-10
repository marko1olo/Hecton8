using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Prints the height field over the clean room's whole 3 km grid as text, so a render can be
    /// checked against the surface it claims to show without trusting the renderer.
    ///
    /// Why this is needed. On 2026-08-10 the clean room was aimed at P5_deepfar for the first time and
    /// returned a picture of two triangular sheets meeting at a point with the background visible
    /// between them. A heightmap is a single-valued function of x and z, so it cannot have a hole, and
    /// that left two possibilities: the renderer was wrong, or the surface really is a saddle steep
    /// enough that the far side falls out of frame. Adding the missing TerrainData.SyncHeightmap call
    /// restored the mesh detail and did NOT change the silhouette, which rules out the LOD system.
    ///
    /// So the surface itself has to be read directly. This fixture is deliberately not a render: it
    /// samples EvaluateHeightMeters on the same 3 km the clean room builds and prints it, so the two
    /// can be compared and whichever one is lying can be identified.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyGridReliefTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>
        /// The clean room lays 1000 m chunks at col * 1000 for col in -1..1 and each spans one chunk
        /// further positive, so the ground it covers is [-1000, 2000] on both axes relative to the
        /// site - 3000 m, with the site itself at the corner of the centre chunk rather than in the
        /// middle of the grid.
        /// </summary>
        private const double GridMinOffset = -1000.0;
        private const double GridSpanMeters = 3000.0;

        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (0.0, 0.0, "origin"),
            (300000.0, 90000.0, "p4"),
            (777000.0, -333000.0, "p5")
        };

        [Test]
        public void GridRelief_IsPrintedAsText()
        {
            const int cols = 60;
            const int rows = 30;
            // Darkest glyph is deepest. Chosen so a smooth ramp reads as an even sweep and a plateau
            // reads as a run of one character.
            const string ramp = "@%#*+=-:. ";

            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Height over the clean room's full 3 km grid, north up, '@' deepest.");
            sb.AppendLine();

            foreach (var site in Sites)
            {
                var grid = new float[rows, cols];
                float lo = float.MaxValue;
                float hi = float.MinValue;

                for (int r = 0; r < rows; r++)
                {
                    // Row 0 prints at the top and should be the NORTH edge, so z runs downward.
                    double z = site.Z + GridMinOffset + GridSpanMeters * (1.0 - r / (double)(rows - 1));
                    for (int c = 0; c < cols; c++)
                    {
                        double x = site.X + GridMinOffset + GridSpanMeters * (c / (double)(cols - 1));
                        float h = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                        grid[r, c] = h;
                        lo = math.min(lo, h);
                        hi = math.max(hi, h);
                    }
                }

                sb.AppendLine($"  {site.Label} at ({site.X:0}, {site.Z:0}): {lo:0} .. {hi:0} m, " +
                              $"relief {hi - lo:0} m over 3000 m of ground.");
                float span = math.max(1e-3f, hi - lo);
                for (int r = 0; r < rows; r++)
                {
                    sb.Append("    ");
                    for (int c = 0; c < cols; c++)
                    {
                        int idx = (int)math.clamp(
                            (grid[r, c] - lo) / span * (ramp.Length - 1), 0, ramp.Length - 1);
                        sb.Append(ramp[idx]);
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The single number that decides whether the clean room can frame a site at all: how far the
        /// ground drops across the grid, against how wide the grid is. A drop larger than the span is
        /// a surface steeper than 45 degrees on average, which no bird's-eye framing can show as
        /// anything but a wall seen edge-on.
        /// </summary>
        [Test]
        public void GridDropToSpanRatio_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Drop across the clean room's 3 km grid, per site.");
            sb.AppendLine();
            sb.AppendLine($"    {"site",-10}{"min m",10}{"max m",10}{"drop m",10}{"drop/span",12}{"mean deg",10}");

            foreach (var site in Sites)
            {
                const int n = 200;
                float lo = float.MaxValue;
                float hi = float.MinValue;
                for (int r = 0; r < n; r++)
                {
                    double z = site.Z + GridMinOffset + GridSpanMeters * (r / (double)(n - 1));
                    for (int c = 0; c < n; c++)
                    {
                        double x = site.X + GridMinOffset + GridSpanMeters * (c / (double)(n - 1));
                        float h = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                        lo = math.min(lo, h);
                        hi = math.max(hi, h);
                    }
                }

                double drop = hi - lo;
                double ratio = drop / GridSpanMeters;
                sb.AppendLine(
                    $"    {site.Label,-10}{lo,10:0}{hi,10:0}{drop,10:0}{ratio,12:0.00}" +
                    $"{math.degrees(math.atan(ratio)),10:0.0}");
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
