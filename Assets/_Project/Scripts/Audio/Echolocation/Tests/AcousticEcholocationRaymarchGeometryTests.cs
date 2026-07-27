using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Audio.Echolocation.Tests
{
    /// <summary>
    /// Locks the echo-tap geometry contract of <see cref="AcousticEcholocationRaymarchJob"/>:
    /// a reported tap must sit on the marched ray at the range it claims, and the monostatic
    /// return leg must equal the outbound leg exactly.
    ///
    /// Regression guarded: the hit interpolant used to default to 0, which reported every
    /// threshold/initial-solid echo at the PREVIOUS sample (one whole probe step, 50 m at the
    /// shipped <c>sonarSdfProbeIntervalMeters</c>) and at <c>PingOrigin</c> itself when no
    /// in-bounds sample preceded the hit. Because tap gain carries a 1/t^2 term, a zero-range
    /// tap saturated to full scale - a max-amplitude click at zero delay.
    /// </summary>
    [TestFixture]
    public sealed class AcousticEcholocationRaymarchGeometryTests
    {
        private const int GridSide = 16;
        private const float CellSizeMeters = 16f;
        private const float SdfRangeMeters = 1f;
        private const float StepMeters = 50f;
        private const float MaxDistanceMeters = 200f;
        private const float SoundSpeedMetersPerSecond = 1500f;
        private const byte EncodedWater = 0;
        private const byte EncodedSolid = 255;
        private const byte AudioMaterialRock = 2;

        // The rayCount <= 8 lane sums right + up + forward for index 0, so collinear basis vectors
        // give a single deterministic probe straight down +Z.
        private static readonly float3 ProbeBasis = new float3(0f, 0f, 1f);
        private static readonly float3 ExpectedDirection = new float3(0f, 0f, 1f);

        [Test]
        public void SolidVolume_ReportsFirstMarchedStep_NotZeroRange()
        {
            float3 pingOrigin = new float3(64f, 64f, 64f);

            RunSingleRay(pingOrigin, SolidEverywhere, out AcousticEcholocationRayHit hit);

            Assert.AreEqual((byte)1, hit.Hit, "A ping inside solid rock must return an echo.");

            // The sample at distance 0 cannot echo (canReturnEcho requires distance > 0), so the
            // first reportable sample is exactly one step out.
            Assert.AreEqual(StepMeters, hit.RayDistanceMeters, 0.001f,
                "Echo range must be the sampled distance, not the previous sample's distance.");
            Assert.AreEqual(StepMeters, hit.ReturnDistanceMeters, 0.001f,
                "Listener sits at the ping origin, so the return leg equals the outbound leg exactly.");

            AssertTapLiesOnRay(pingOrigin, hit);

            // totalDistance = 2 * step -> delay = totalDistance / c.
            float expectedDelaySeconds = (2f * StepMeters) / SoundSpeedMetersPerSecond;
            Assert.AreEqual(expectedDelaySeconds, hit.DelaySeconds, 1e-5f,
                "Delay must follow the round trip of the reported range.");

            // The defect produced rayDistance == 0 -> totalTimeSq floored at 1e-6 -> gain saturated.
            Assert.Less(hit.Gain, 1f,
                "A 50 m echo must not saturate the tap; saturation means the range collapsed to zero.");
            Assert.Greater(hit.Gain, 0f, "A reported echo must carry audible energy.");
        }

        [Test]
        public void RayEnteringVolumeAlreadyInsideSolid_KeepsPointAndRangeConsistent()
        {
            // Samples at 0 m and 50 m fall outside the SDF grid (negative Z), so they are skipped
            // without establishing a previous sample. The 100 m sample is the first in-bounds one
            // and it is solid, which takes the initial-solid-hit branch.
            float3 pingOrigin = new float3(64f, 64f, -80f);

            RunSingleRay(pingOrigin, SolidEverywhere, out AcousticEcholocationRayHit hit);

            Assert.AreEqual((byte)1, hit.Hit, "The first in-bounds solid sample must return an echo.");
            Assert.AreEqual(100f, hit.RayDistanceMeters, 0.001f,
                "Range must be the distance of the sample that was actually read.");

            // This is the assertion the defect broke outright: Point said 0 m from the origin while
            // RayDistanceMeters said 50 m. Point drives the sonar point cloud, RayDistanceMeters
            // drives ranging, so the two readouts contradicted each other.
            AssertTapLiesOnRay(pingOrigin, hit);
        }

        [Test]
        public void SignCrossing_StillInterpolatesSubStepSurface()
        {
            // Water below Z index 8, solid at and above it. The 50 m sample reads a negative
            // density and the 100 m sample reads solid, so the surface-crossing branch owns the
            // interpolant. This path was already correct and must stay correct.
            float3 pingOrigin = new float3(64f, 64f, 64f);

            RunSingleRay(pingOrigin, WaterBelowZIndex8, out AcousticEcholocationRayHit hit);

            Assert.AreEqual((byte)1, hit.Hit, "Crossing into rock must return an echo.");
            Assert.Greater(hit.RayDistanceMeters, StepMeters,
                "The crossing lies beyond the last water sample.");
            Assert.Less(hit.RayDistanceMeters, 2f * StepMeters,
                "The crossing lies before the first solid sample; it must not snap to the sample grid.");

            // density(50 m) = -0.75, density(100 m) = +1 -> t = 0.75 / 1.75.
            float expectedRange = math.lerp(StepMeters, 2f * StepMeters, 0.75f / 1.75f);
            Assert.AreEqual(expectedRange, hit.RayDistanceMeters, 0.01f,
                "Sub-step surface position must come from the density sign crossing.");

            AssertTapLiesOnRay(pingOrigin, hit);
        }

        private static void AssertTapLiesOnRay(float3 pingOrigin, in AcousticEcholocationRayHit hit)
        {
            float3 expectedPoint = pingOrigin + (ExpectedDirection * hit.RayDistanceMeters);
            Assert.AreEqual(expectedPoint.x, hit.Point.x, 0.01f, "Tap X must lie on the marched ray.");
            Assert.AreEqual(expectedPoint.y, hit.Point.y, 0.01f, "Tap Y must lie on the marched ray.");
            Assert.AreEqual(expectedPoint.z, hit.Point.z, 0.01f, "Tap Z must lie on the marched ray.");

            Assert.AreEqual(hit.RayDistanceMeters, math.length(hit.Point - pingOrigin), 0.01f,
                "Reported range must equal the geometric distance from the ping origin to the tap.");
        }

        private static byte SolidEverywhere(int x, int y, int z)
        {
            _ = x;
            _ = y;
            _ = z;
            return EncodedSolid;
        }

        private static byte WaterBelowZIndex8(int x, int y, int z)
        {
            _ = x;
            _ = y;
            return z >= 8 ? EncodedSolid : EncodedWater;
        }

        private static void RunSingleRay(
            float3 pingOrigin,
            System.Func<int, int, int, byte> encode,
            out AcousticEcholocationRayHit hit)
        {
            int cellCount = GridSide * GridSide * GridSide;
            NativeArray<byte> encodedSdf = new NativeArray<byte>(cellCount, Allocator.TempJob);
            NativeArray<byte> audioMaterialIds = new NativeArray<byte>(cellCount, Allocator.TempJob);
            NativeArray<AcousticEcholocationRayHit> hits =
                new NativeArray<AcousticEcholocationRayHit>(1, Allocator.TempJob);

            try
            {
                for (int z = 0; z < GridSide; z++)
                {
                    for (int y = 0; y < GridSide; y++)
                    {
                        for (int x = 0; x < GridSide; x++)
                        {
                            int index = x + GridSide * (y + GridSide * z);
                            encodedSdf[index] = encode(x, y, z);
                            audioMaterialIds[index] = AudioMaterialRock;
                        }
                    }
                }

                AcousticEcholocationRaymarchJob job = new AcousticEcholocationRaymarchJob
                {
                    EncodedSdf = encodedSdf.AsReadOnly(),
                    AudioMaterialIds = audioMaterialIds.AsReadOnly(),
                    GridDimensions = new int3(GridSide, GridSide, GridSide),
                    VolumeOrigin = float3.zero,
                    CellSize = new float3(CellSizeMeters),
                    SdfRange = SdfRangeMeters,
                    PingOrigin = pingOrigin,
                    ListenerPosition = pingOrigin,
                    Forward = ProbeBasis,
                    Right = ProbeBasis,
                    Up = ProbeBasis,
                    MaxDistanceMeters = MaxDistanceMeters,
                    StepMeters = StepMeters,
                    Intensity01 = 1f,
                    ReflectivityConstant = 0.000045f,
                    SoundSpeedInv = 1f / SoundSpeedMetersPerSecond,
                    DensityThreshold01 = 0.5f,
                    MinimumLowPassHertz = 80f,
                    OpenLowPassHertz = 22000f,
                    AbsorptionCoefficient = 0.0035f,
                    ReferenceDistanceMeters = 24f,
                    RayCount = 1,
                    Hits = hits
                };

                job.Execute(0);
                hit = hits[0];
            }
            finally
            {
                hits.Dispose();
                audioMaterialIds.Dispose();
                encodedSdf.Dispose();
            }
        }
    }
}
