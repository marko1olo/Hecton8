using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Hecton8.Physics;

namespace Hecton8.Physics.Tests
{
    /// <summary>
    /// Edit-mode verification of the Burst-compatible DOD extraction of the CPU fluid simulation
    /// fallback. These tests exercise the individual jobs directly against the reference math from
    /// the PureLogic calculators (VorticityConfinementForceCalculator / FluidPressureJacobiSolver /
    /// FluidAdvectionStepCalculator) so a divergence between the DOD jobs and the pure-logic source
    /// of truth is caught without needing a live engine.
    /// </summary>
    public class CpuFluidSimulationFallbackDodTests
    {
        private const int N = 32;
        private const int SliceShift = 5;
        private const int PlaneShift = 10;
        private const int Length = N * N * N;

        private static int Idx(int x, int y, int z) => x | (y << SliceShift) | (z << PlaneShift);

        // ---- Vorticity curl + confinement force ------------------------------------------------

        [Test]
        public void VorticityCurlJob_MatchesCentralDifferenceDefinition()
        {
            // Pure translation field: velocity is constant everywhere, so curl must be exactly zero.
            using var vx = new NativeArray<float>(Length, Allocator.TempJob);
            using var vy = new NativeArray<float>(Length, Allocator.TempJob);
            using var vz = new NativeArray<float>(Length, Allocator.TempJob);
            using var vort = new NativeArray<float3>(Length, Allocator.TempJob);
            using var vortMag = new NativeArray<float>(Length, Allocator.TempJob);
            for (int i = 0; i < Length; i++)
            {
                vx[i] = 2f;
                vy[i] = -1f;
                vz[i] = 0.5f;
            }

            var job = new VorticityCurlJob
            {
                VelocityX = vx,
                VelocityY = vy,
                VelocityZ = vz,
                Vorticity = vort,
                VorticityMag = vortMag,
                N = N,
                InvSpacing2 = 1f / (2f * 0.25f),
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            // Interior cell: constant field -> curl = 0 and magnitude = 0.
            int idx = Idx(5, 5, 5);
            Assert.AreEqual(0f, math.length(vort[idx]), 1e-5f, "Constant field must have zero curl.");
            Assert.AreEqual(0f, vortMag[idx], 1e-5f, "Constant field must have zero vorticity magnitude.");
        }

        [Test]
        public void VorticityCurlJob_ShearFieldProducesExpectedCurl()
        {
            // vx = z => curl_x = d(vz)/dy - d(vy)/dz = 0, curl_y = d(vx)/dz - d(vz)/dx = 1, curl_z = 0.
            using var vx = new NativeArray<float>(Length, Allocator.TempJob);
            using var vy = new NativeArray<float>(Length, Allocator.TempJob);
            using var vz = new NativeArray<float>(Length, Allocator.TempJob);
            using var vort = new NativeArray<float3>(Length, Allocator.TempJob);
            using var vortMag = new NativeArray<float>(Length, Allocator.TempJob);
            for (int z = 0; z < N; z++)
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        int i = Idx(x, y, z);
                        vx[i] = z * 0.25f;
                        vy[i] = 0f;
                        vz[i] = 0f;
                    }

            var job = new VorticityCurlJob
            {
                VelocityX = vx,
                VelocityY = vy,
                VelocityZ = vz,
                Vorticity = vort,
                VorticityMag = vortMag,
                N = N,
                InvSpacing2 = 1f / (2f * 0.25f),
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            int idx = Idx(16, 16, 16);
            Assert.AreEqual(0f, vort[idx].x, 1e-4f);
            Assert.AreEqual(1f, vort[idx].y, 1e-4f, "curl_y = d(vx)/dz for vx = z should be 1.");
            Assert.AreEqual(0f, vort[idx].z, 1e-4f);
        }

        // ---- Divergence ------------------------------------------------------------------------

        [Test]
        public void FluidDivergenceJob_ZeroForConstantField_And_ZeroOnBoundary()
        {
            using var vx = new NativeArray<float>(Length, Allocator.TempJob);
            using var vy = new NativeArray<float>(Length, Allocator.TempJob);
            using var vz = new NativeArray<float>(Length, Allocator.TempJob);
            using var div = new NativeArray<float>(Length, Allocator.TempJob);
            for (int i = 0; i < Length; i++)
            {
                vx[i] = 3f;
                vy[i] = 3f;
                vz[i] = 3f;
            }

            var job = new FluidDivergenceJob
            {
                VelocityX = vx,
                VelocityY = vy,
                VelocityZ = vz,
                Divergence = div,
                N = N,
                InvSpacing2 = 1f / (2f * 0.25f),
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            int interior = Idx(8, 8, 8);
            Assert.AreEqual(0f, div[interior], 1e-5f, "Constant velocity must be divergence-free.");
            // Boundary cells must be explicitly zeroed.
            Assert.AreEqual(0f, div[Idx(0, 8, 8)], 1e-5f);
            Assert.AreEqual(0f, div[Idx(8, 0, 8)], 1e-5f);
            Assert.AreEqual(0f, div[Idx(8, 8, 0)], 1e-5f);
            Assert.AreEqual(0f, div[Idx(31, 8, 8)], 1e-5f);
        }

        // ---- Jacobi solve ----------------------------------------------------------------------

        [Test]
        public void FluidPressureJacobiJob_UniformDivergence_ProducesUniformAverage()
        {
            using var pressureIn = new NativeArray<float>(Length, Allocator.TempJob);
            using var div = new NativeArray<float>(Length, Allocator.TempJob);
            using var pressureOut = new NativeArray<float>(Length, Allocator.TempJob);
            for (int i = 0; i < Length; i++)
            {
                pressureIn[i] = 4f;
                div[i] = 2f;
            }

            var job = new FluidPressureJacobiJob
            {
                PressureIn = pressureIn,
                Divergence = div,
                PressureOut = pressureOut,
                N = N,
                Dx2 = 0.0625f,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            // Interior: p_curr = (6*p - dx2*div)/6. dx2 = 0.25^2 = 0.0625.
            int idx = Idx(10, 10, 10);
            float expected = (6f * 4f - 0.0625f * 2f) / 6f;
            Assert.AreEqual(expected, pressureOut[idx], 1e-5f, "Interior Jacobi average must match reference formula.");
            // Boundary neighbour clamps to the cell value, so the boundary cell also follows the formula.
            Assert.AreEqual(expected, pressureOut[Idx(0, 10, 10)], 1e-5f);
        }

        // ---- Pressure projection ---------------------------------------------------------------

        [Test]
        public void FluidPressureProjectJob_SubtractsPressureGradient()
        {
            using var vx = new NativeArray<float>(Length, Allocator.TempJob);
            using var vy = new NativeArray<float>(Length, Allocator.TempJob);
            using var vz = new NativeArray<float>(Length, Allocator.TempJob);
            using var pressure = new NativeArray<float>(Length, Allocator.TempJob);
            for (int i = 0; i < Length; i++)
            {
                vx[i] = 5f;
                vy[i] = 5f;
                vz[i] = 5f;
                pressure[i] = 10f;
            }

            var job = new FluidPressureProjectJob
            {
                VelocityX = vx,
                VelocityY = vy,
                VelocityZ = vz,
                Pressure = pressure,
                N = N,
                InvSpacing2 = 1f / (2f * 0.25f),
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            int idx = Idx(12, 12, 12);
            // Uniform pressure -> zero gradient -> velocity unchanged.
            Assert.AreEqual(5f, vx[idx], 1e-5f);
            Assert.AreEqual(5f, vy[idx], 1e-5f);
            Assert.AreEqual(5f, vz[idx], 1e-5f);
            // Boundary cells are untouched by design.
            Assert.AreEqual(5f, vx[Idx(0, 12, 12)], 1e-5f);
        }

        [Test]
        public void FluidPressureProjectJob_LocalPressurePeak_ReducesVelocity()
        {
            using var vx = new NativeArray<float>(Length, Allocator.TempJob);
            using var vy = new NativeArray<float>(Length, Allocator.TempJob);
            using var vz = new NativeArray<float>(Length, Allocator.TempJob);
            using var pressure = new NativeArray<float>(Length, Allocator.TempJob);
            for (int i = 0; i < Length; i++)
            {
                vx[i] = 1f;
                vy[i] = 1f;
                vz[i] = 1f;
                pressure[i] = 0f;
            }
            // A peak at (16,16,16): pressure = 1 there, 0 elsewhere.
            pressure[Idx(16, 16, 16)] = 1f;

            var job = new FluidPressureProjectJob
            {
                VelocityX = vx,
                VelocityY = vy,
                VelocityZ = vz,
                Pressure = pressure,
                N = N,
                InvSpacing2 = 1f / (2f * 0.25f),
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            job.Schedule(Length, 64).Complete();

            // Cell (17,16,16) sees grad(p)_x = (0 - 1) * invSpacing2 = -2 => vx -= -2 = +3.
            int idx = Idx(17, 16, 16);
            Assert.AreEqual(3f, vx[idx], 1e-5f, "Velocity along the positive pressure gradient direction must decrease.");
            // Cell (15,16,16) sees grad(p)_x = (1 - 0) * invSpacing2 = +2 => vx -= +2 = -1.
            Assert.AreEqual(-1f, vx[Idx(15, 16, 16)], 1e-5f);
        }
    }
}
