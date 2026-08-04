using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// A single particle advected by the fallback fluid simulation. Each particle carries its own
    /// world-space position, velocity, remaining lifetime and an activation flag word.
    /// </summary>
    public struct FluidParticle
    {
        public float3 PositionWS;
        public float3 VelocityWS;
        public float Life;
        public uint Flags;
    }

    /// <summary>
    /// Structure-of-arrays buffer set for the CPU fluid simulation fallback. Holds the three velocity
    /// component fields, the three confinement force fields, vorticity (float3) plus its magnitude,
    /// the divergence field and two pressure buffers used for ping-pong Jacobi iterations.
    /// </summary>
    public struct CpuFluidSimulationFallbackData
    {
        public NativeArray<float> VelocityX;
        public NativeArray<float> VelocityY;
        public NativeArray<float> VelocityZ;
        public NativeArray<float> ForceX;
        public NativeArray<float> ForceY;
        public NativeArray<float> ForceZ;
        public NativeArray<float3> Vorticity;
        public NativeArray<float> VorticityMag;
        public NativeArray<float> Divergence;
        public NativeArray<float> PressureA;
        public NativeArray<float> PressureB;
        public int N;
        public int SliceShift;
        public int PlaneShift;
        public int Mask;

        public void EnsureCapacity(int n)
        {
            if (N == n && VelocityX.IsCreated) return;
            Dispose();
            N = n;
            int length = n * n * n;

            int bits = 0;
            int v = n;
            while (v > 1)
            {
                v >>= 1;
                bits++;
            }
            SliceShift = bits;
            PlaneShift = 2 * bits;
            Mask = n - 1;

            VelocityX = new NativeArray<float>(length, Allocator.Persistent);
            VelocityY = new NativeArray<float>(length, Allocator.Persistent);
            VelocityZ = new NativeArray<float>(length, Allocator.Persistent);
            ForceX = new NativeArray<float>(length, Allocator.Persistent);
            ForceY = new NativeArray<float>(length, Allocator.Persistent);
            ForceZ = new NativeArray<float>(length, Allocator.Persistent);
            Vorticity = new NativeArray<float3>(length, Allocator.Persistent);
            VorticityMag = new NativeArray<float>(length, Allocator.Persistent);
            Divergence = new NativeArray<float>(length, Allocator.Persistent);
            PressureA = new NativeArray<float>(length, Allocator.Persistent);
            PressureB = new NativeArray<float>(length, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (VelocityX.IsCreated) VelocityX.Dispose();
            if (VelocityY.IsCreated) VelocityY.Dispose();
            if (VelocityZ.IsCreated) VelocityZ.Dispose();
            if (ForceX.IsCreated) ForceX.Dispose();
            if (ForceY.IsCreated) ForceY.Dispose();
            if (ForceZ.IsCreated) ForceZ.Dispose();
            if (Vorticity.IsCreated) Vorticity.Dispose();
            if (VorticityMag.IsCreated) VorticityMag.Dispose();
            if (Divergence.IsCreated) Divergence.Dispose();
            if (PressureA.IsCreated) PressureA.Dispose();
            if (PressureB.IsCreated) PressureB.Dispose();
        }

        /// <summary>
        /// Copies the xyz of a float3 vector-noise field into the three scalar velocity component
        /// buffers, so the fallback pipeline can be fed from a resolved vector noise field exactly as
        /// the monolith seeds its CPU fallback grids from the noise field.
        /// </summary>
        public JobHandle CopyNoiseFieldToVelocity(
            NativeArray<float3> noiseField,
            JobHandle inputDeps = default)
        {
            var job = new CopyNoiseToVelocityJob
            {
                NoiseField = noiseField,
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
            };
            return job.Schedule(noiseField.Length, 64, inputDeps);
        }

        /// <summary>
        /// Copies the three scalar velocity component buffers back into the xyz of a float3
        /// vector-noise field, matching the monolith's write-back after the CPU solve.
        /// </summary>
        public JobHandle CopyVelocityToNoiseField(
            NativeArray<float3> noiseField,
            JobHandle inputDeps = default)
        {
            var job = new CopyVelocityToNoiseJob
            {
                NoiseField = noiseField,
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
            };
            return job.Schedule(noiseField.Length, 64, inputDeps);
        }

        /// <summary>
        /// Schedules the complete fallback simulation step and chains every stage with job handles.
        /// Stages: vorticity curl -> confinement force -> apply force to velocity -> divergence ->
        /// pressure Jacobi (ping-pong) -> pressure-gradient projection -> particle advection.
        /// This reproduces the monolith's RunCpuFluidSimulationFallback ordering exactly.
        /// </summary>
        public JobHandle ScheduleSimulationStep(
            NativeArray<FluidParticle> particles,
            JobHandle inputDeps,
            float deltaTime,
            float gridSpacing,
            float confinementEpsilon,
            uint activeFlag,
            double3 totalOffset,
            int jacobiIterations)
        {
            int length = N * N * N;
            float safeGridSpacing = math.max(0.0001f, gridSpacing);
            float invSpacing2 = 1f / (2f * safeGridSpacing);
            float safeEpsilon = math.max(0f, confinementEpsilon);
            float dx2 = gridSpacing * gridSpacing;
            float invCellSize = 1f / gridSpacing;

            var curlJob = new VorticityCurlJob
            {
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                Vorticity = Vorticity,
                VorticityMag = VorticityMag,
                N = N,
                InvSpacing2 = invSpacing2,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            JobHandle handle = curlJob.Schedule(length, 64, inputDeps);

            var forceJob = new VorticityConfinementForceJob
            {
                Vorticity = Vorticity,
                VorticityMag = VorticityMag,
                ForceX = ForceX,
                ForceY = ForceY,
                ForceZ = ForceZ,
                N = N,
                InvSpacing2 = invSpacing2,
                SafeEpsilon = safeEpsilon,
                SafeGridSpacing = safeGridSpacing,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            handle = forceJob.Schedule(length, 64, handle);

            var applyForceJob = new ApplyVorticityForceJob
            {
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                ForceX = ForceX,
                ForceY = ForceY,
                ForceZ = ForceZ,
                DeltaTime = deltaTime,
                N = N,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            handle = applyForceJob.Schedule(length, 64, handle);

            var divergenceJob = new FluidDivergenceJob
            {
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                Divergence = Divergence,
                N = N,
                InvSpacing2 = invSpacing2,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            handle = divergenceJob.Schedule(length, 64, handle);

            NativeArray<float> src = PressureA;
            NativeArray<float> dst = PressureB;
            for (int iter = 0; iter < jacobiIterations; iter++)
            {
                var jacobiJob = new FluidPressureJacobiJob
                {
                    PressureIn = src,
                    Divergence = Divergence,
                    PressureOut = dst,
                    N = N,
                    Dx2 = dx2,
                    SliceShift = SliceShift,
                    PlaneShift = PlaneShift,
                };
                handle = jacobiJob.Schedule(length, 64, handle);
                var swap = src;
                src = dst;
                dst = swap;
            }

            var projectJob = new FluidPressureProjectJob
            {
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                Pressure = src,
                N = N,
                InvSpacing2 = invSpacing2,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            handle = projectJob.Schedule(length, 64, handle);

            var advectJob = new CpuFluidAdvectionJob
            {
                TotalOffset = totalOffset,
                VelocityX = VelocityX,
                VelocityY = VelocityY,
                VelocityZ = VelocityZ,
                Particles = particles,
                ActiveFlag = activeFlag,
                DeltaTime = deltaTime,
                GridSpacing = gridSpacing,
                InvCellSize = invCellSize,
                N = N,
                SliceShift = SliceShift,
                PlaneShift = PlaneShift,
            };
            handle = advectJob.Schedule(particles.Length, 64, handle);

            return handle;
        }
    }

    /// <summary>
    /// Phase 1 of vorticity confinement: computes the curl (vorticity) vector and its magnitude for
    /// every interior cell using central differences of the three velocity component fields. Boundary
    /// cells produce zero vorticity.
    /// </summary>
    [BurstCompile]
    public struct VorticityCurlJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> VelocityX;
        [ReadOnly] public NativeArray<float> VelocityY;
        [ReadOnly] public NativeArray<float> VelocityZ;
        [WriteOnly] public NativeArray<float3> Vorticity;
        [WriteOnly] public NativeArray<float> VorticityMag;
        public int N;
        public float InvSpacing2;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            if (x == 0 || x == N - 1 || y == 0 || y == N - 1 || z == 0 || z == N - 1)
            {
                Vorticity[outIdx] = float3.zero;
                VorticityMag[outIdx] = 0f;
                return;
            }

            float dwz_dy = (VelocityZ[x | ((y + 1) << SliceShift) | (z << PlaneShift)]
                          - VelocityZ[x | ((y - 1) << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dwy_dz = (VelocityY[x | (y << SliceShift) | ((z + 1) << PlaneShift)]
                          - VelocityY[x | (y << SliceShift) | ((z - 1) << PlaneShift)]) * InvSpacing2;
            float dwx_dz = (VelocityX[x | (y << SliceShift) | ((z + 1) << PlaneShift)]
                          - VelocityX[x | (y << SliceShift) | ((z - 1) << PlaneShift)]) * InvSpacing2;
            float dwz_dx = (VelocityZ[(x + 1) | (y << SliceShift) | (z << PlaneShift)]
                          - VelocityZ[(x - 1) | (y << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dwy_dx = (VelocityY[(x + 1) | (y << SliceShift) | (z << PlaneShift)]
                          - VelocityY[(x - 1) | (y << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dwx_dy = (VelocityX[x | ((y + 1) << SliceShift) | (z << PlaneShift)]
                          - VelocityX[x | ((y - 1) << SliceShift) | (z << PlaneShift)]) * InvSpacing2;

            float curlX = dwz_dy - dwy_dz;
            float curlY = dwx_dz - dwz_dx;
            float curlZ = dwy_dx - dwx_dy;

            float3 curl = new float3(curlX, curlY, curlZ);
            Vorticity[outIdx] = curl;
            VorticityMag[outIdx] = math.length(curl);
        }
    }

    /// <summary>
    /// Phase 2 of vorticity confinement: computes the confinement force from the gradient of the
    /// vorticity magnitude crossed with the local vorticity vector. Boundary cells are zeroed.
    /// </summary>
    [BurstCompile]
    public struct VorticityConfinementForceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Vorticity;
        [ReadOnly] public NativeArray<float> VorticityMag;
        [WriteOnly] public NativeArray<float> ForceX;
        [WriteOnly] public NativeArray<float> ForceY;
        [WriteOnly] public NativeArray<float> ForceZ;
        public int N;
        public float InvSpacing2;
        public float SafeEpsilon;
        public float SafeGridSpacing;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            if (x == 0 || x == N - 1 || y == 0 || y == N - 1 || z == 0 || z == N - 1)
            {
                ForceX[outIdx] = 0f;
                ForceY[outIdx] = 0f;
                ForceZ[outIdx] = 0f;
                return;
            }

            float dMag_dx = (VorticityMag[(x + 1) | (y << SliceShift) | (z << PlaneShift)]
                           - VorticityMag[(x - 1) | (y << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dMag_dy = (VorticityMag[x | ((y + 1) << SliceShift) | (z << PlaneShift)]
                           - VorticityMag[x | ((y - 1) << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dMag_dz = (VorticityMag[x | (y << SliceShift) | ((z + 1) << PlaneShift)]
                           - VorticityMag[x | (y << SliceShift) | ((z - 1) << PlaneShift)]) * InvSpacing2;

            float3 gradMag = new float3(dMag_dx, dMag_dy, dMag_dz);
            float gradMagLength = math.length(gradMag);

            float3 force = float3.zero;
            if (gradMagLength > 0.000001f)
            {
                float3 n = gradMag / gradMagLength;
                float3 w = Vorticity[outIdx];
                float3 nCrossW = math.cross(n, w);
                force = SafeEpsilon * SafeGridSpacing * nCrossW;
            }

            // Mirror the reference VorticityConfinementForceCalculator's numeric guard: a NaN/Infinity
            // force component is zeroed so a poisoned cell cannot inject unbounded velocity.
            if (math.isnan(force.x) || math.isinf(force.x)) force.x = 0f;
            if (math.isnan(force.y) || math.isinf(force.y)) force.y = 0f;
            if (math.isnan(force.z) || math.isinf(force.z)) force.z = 0f;

            ForceX[outIdx] = force.x;
            ForceY[outIdx] = force.y;
            ForceZ[outIdx] = force.z;
        }
    }

    /// <summary>
    /// Applies the precomputed vorticity-confinement force to the velocity field. This reproduces the
    /// monolith's `velocity += confinementForce * deltaTime` step that runs before the divergence and
    /// pressure solves, so the pressure solve sees the force-augmented velocity.
    /// </summary>
    [BurstCompile]
    public struct ApplyVorticityForceJob : IJobParallelFor
    {
        public NativeArray<float> VelocityX;
        public NativeArray<float> VelocityY;
        public NativeArray<float> VelocityZ;
        [ReadOnly] public NativeArray<float> ForceX;
        [ReadOnly] public NativeArray<float> ForceY;
        [ReadOnly] public NativeArray<float> ForceZ;
        public float DeltaTime;
        public int N;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            VelocityX[outIdx] += ForceX[outIdx] * DeltaTime;
            VelocityY[outIdx] += ForceY[outIdx] * DeltaTime;
            VelocityZ[outIdx] += ForceZ[outIdx] * DeltaTime;
        }
    }

    /// <summary>
    /// Computes the divergence of the velocity field using central differences of the three component
    /// fields. Boundary cells are zeroed, matching the monolith's divergence pass. The result feeds
    /// the pressure Jacobi solve.
    /// </summary>
    [BurstCompile]
    public struct FluidDivergenceJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> VelocityX;
        [ReadOnly] public NativeArray<float> VelocityY;
        [ReadOnly] public NativeArray<float> VelocityZ;
        [WriteOnly] public NativeArray<float> Divergence;
        public int N;
        public float InvSpacing2;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            if (x == 0 || x == N - 1 || y == 0 || y == N - 1 || z == 0 || z == N - 1)
            {
                Divergence[outIdx] = 0f;
                return;
            }

            float dVx_dx = (VelocityX[(x + 1) | (y << SliceShift) | (z << PlaneShift)]
                          - VelocityX[(x - 1) | (y << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dVy_dy = (VelocityY[x | ((y + 1) << SliceShift) | (z << PlaneShift)]
                          - VelocityY[x | ((y - 1) << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float dVz_dz = (VelocityZ[x | (y << SliceShift) | ((z + 1) << PlaneShift)]
                          - VelocityZ[x | (y << SliceShift) | ((z - 1) << PlaneShift)]) * InvSpacing2;

            Divergence[outIdx] = dVx_dx + dVy_dy + dVz_dz;
        }
    }

    /// <summary>
    /// Pressure-gradient projection: subtracts grad(pressure) * invSpacing2 from the velocity field so
    /// the result is (near) divergence-free. Boundary cells are untouched, matching the monolith's
    /// projection pass that follows the Jacobi solve.
    /// </summary>
    [BurstCompile]
    public struct FluidPressureProjectJob : IJobParallelFor
    {
        public NativeArray<float> VelocityX;
        public NativeArray<float> VelocityY;
        public NativeArray<float> VelocityZ;
        [ReadOnly] public NativeArray<float> Pressure;
        public int N;
        public float InvSpacing2;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            if (x == 0 || x == N - 1 || y == 0 || y == N - 1 || z == 0 || z == N - 1)
                return;

            float p_dx = (Pressure[(x + 1) | (y << SliceShift) | (z << PlaneShift)]
                        - Pressure[(x - 1) | (y << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float p_dy = (Pressure[x | ((y + 1) << SliceShift) | (z << PlaneShift)]
                        - Pressure[x | ((y - 1) << SliceShift) | (z << PlaneShift)]) * InvSpacing2;
            float p_dz = (Pressure[x | (y << SliceShift) | ((z + 1) << PlaneShift)]
                        - Pressure[x | (y << SliceShift) | ((z - 1) << PlaneShift)]) * InvSpacing2;

            VelocityX[outIdx] -= p_dx;
            VelocityY[outIdx] -= p_dy;
            VelocityZ[outIdx] -= p_dz;
        }
    }

    /// <summary>
    /// One Jacobi relaxation pass over the pressure field. Every cell averages its six neighbours and
    /// subtracts the scaled divergence, clamping neighbours at the domain boundary to the cell value.
    /// The caller ping-pongs PressureA/PressureB across successive passes.
    /// </summary>
    [BurstCompile]
    public struct FluidPressureJacobiJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> PressureIn;
        [ReadOnly] public NativeArray<float> Divergence;
        [WriteOnly] public NativeArray<float> PressureOut;
        public int N;
        public float Dx2;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int idx)
        {
            int x = idx % N;
            int y = (idx / N) % N;
            int z = idx / (N * N);

            int outIdx = x | (y << SliceShift) | (z << PlaneShift);

            int xp1 = math.min(x + 1, N - 1);
            int xm1 = math.max(x - 1, 0);
            int yp1 = math.min(y + 1, N - 1);
            int ym1 = math.max(y - 1, 0);
            int zp1 = math.min(z + 1, N - 1);
            int zm1 = math.max(z - 1, 0);

            float p_i1 = PressureIn[xp1 | (y << SliceShift) | (z << PlaneShift)];
            float p_i0 = PressureIn[xm1 | (y << SliceShift) | (z << PlaneShift)];
            float p_j1 = PressureIn[x | (yp1 << SliceShift) | (z << PlaneShift)];
            float p_j0 = PressureIn[x | (ym1 << SliceShift) | (z << PlaneShift)];
            float p_k1 = PressureIn[x | (y << SliceShift) | (zp1 << PlaneShift)];
            float p_k0 = PressureIn[x | (y << SliceShift) | (zm1 << PlaneShift)];

            float div = Divergence[outIdx];
            // Mirror the reference FluidPressureJacobiSolver's numeric guard: any NaN/Infinity in the
            // divergence input or the resulting pressure is clamped to 0 so a poisoned cell cannot
            // contaminate neighbouring cells across Jacobi passes.
            if (math.isnan(div) || math.isinf(div))
                div = 0f;

            float p_curr = (p_i1 + p_i0 + p_j1 + p_j0 + p_k1 + p_k0 - Dx2 * div) / 6f;
            if (math.isnan(p_curr) || math.isinf(p_curr))
                p_curr = 0f;
            // Clamp to prevent extreme values / overflow (mirrors the reference solver).
            if (p_curr > float.MaxValue) p_curr = float.MaxValue;
            if (p_curr < float.MinValue) p_curr = float.MinValue;

            PressureOut[outIdx] = p_curr;
        }
    }

    /// <summary>
    /// Semi-Lagrangian advection of particles. Each particle is back-traced through its current grid
    /// cell using the three velocity component fields, then the advected velocity is computed by
    /// trilinear interpolation of each component field at the back-traced position.
    /// </summary>
    [BurstCompile]
    public struct CpuFluidAdvectionJob : IJobParallelFor
    {
        public double3 TotalOffset;
        [ReadOnly] public NativeArray<float> VelocityX;
        [ReadOnly] public NativeArray<float> VelocityY;
        [ReadOnly] public NativeArray<float> VelocityZ;
        public NativeArray<FluidParticle> Particles;
        public uint ActiveFlag;
        public float DeltaTime;
        public float GridSpacing;
        public float InvCellSize;
        public int N;
        public int SliceShift;
        public int PlaneShift;

        public void Execute(int i)
        {
            FluidParticle particle = Particles[i];
            if ((particle.Flags & ActiveFlag) == 0) return;

            float3 pos = particle.PositionWS;
            double3 localCell = (new double3(pos.x, pos.y, pos.z) + TotalOffset) * InvCellSize;
            int x = math.clamp((int)math.floor(localCell.x), 0, N - 1);
            int y = math.clamp((int)math.floor(localCell.y), 0, N - 1);
            int z = math.clamp((int)math.floor(localCell.z), 0, N - 1);

            int idx = x | (y << SliceShift) | (z << PlaneShift);
            float invDtOverSpacing = DeltaTime / GridSpacing;
            float tx = x - VelocityX[idx] * invDtOverSpacing;
            float ty = y - VelocityY[idx] * invDtOverSpacing;
            float tz = z - VelocityZ[idx] * invDtOverSpacing;

            float advectedX = TrilinearSample(VelocityX, tx, ty, tz);
            float advectedY = TrilinearSample(VelocityY, tx, ty, tz);
            float advectedZ = TrilinearSample(VelocityZ, tx, ty, tz);

            particle.PositionWS += particle.VelocityWS * DeltaTime;
            particle.VelocityWS = new float3(advectedX, advectedY, advectedZ);
            particle.Life -= DeltaTime;
            if (particle.Life <= 0f)
                particle.Flags &= ~ActiveFlag;

            Particles[i] = particle;
        }

        private float TrilinearSample(NativeArray<float> field, float tx, float ty, float tz)
        {
            int maxCoord = N - 1;
            tx = math.clamp(tx, 0f, maxCoord);
            ty = math.clamp(ty, 0f, maxCoord);
            tz = math.clamp(tz, 0f, maxCoord);

            int x0 = (int)math.floor(tx);
            int y0 = (int)math.floor(ty);
            int z0 = (int)math.floor(tz);
            int x1 = math.min(x0 + 1, maxCoord);
            int y1 = math.min(y0 + 1, maxCoord);
            int z1 = math.min(z0 + 1, maxCoord);
            float sx = tx - x0;
            float sy = ty - y0;
            float sz = tz - z0;

            float c000 = field[x0 | (y0 << SliceShift) | (z0 << PlaneShift)];
            float c100 = field[x1 | (y0 << SliceShift) | (z0 << PlaneShift)];
            float c010 = field[x0 | (y1 << SliceShift) | (z0 << PlaneShift)];
            float c110 = field[x1 | (y1 << SliceShift) | (z0 << PlaneShift)];
            float c001 = field[x0 | (y0 << SliceShift) | (z1 << PlaneShift)];
            float c101 = field[x1 | (y0 << SliceShift) | (z1 << PlaneShift)];
            float c011 = field[x0 | (y1 << SliceShift) | (z1 << PlaneShift)];
            float c111 = field[x1 | (y1 << SliceShift) | (z1 << PlaneShift)];

            float c00 = c000 * (1f - sx) + c100 * sx;
            float c10 = c010 * (1f - sx) + c110 * sx;
            float c01 = c001 * (1f - sx) + c101 * sx;
            float c11 = c011 * (1f - sx) + c111 * sx;
            float c0 = c00 * (1f - sy) + c10 * sy;
            float c1 = c01 * (1f - sy) + c11 * sy;
            return c0 * (1f - sz) + c1 * sz;
        }
    }

    /// <summary>
    /// Seeds the three scalar velocity component buffers from the xyz of a float3 vector-noise field.
    /// The flat index is the element index in the noise array (== the same bit-packed index used by
    /// the simulation buffers), so this is a straight one-to-one copy per element.
    /// </summary>
    [BurstCompile]
    public struct CopyNoiseToVelocityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> NoiseField;
        [WriteOnly] public NativeArray<float> VelocityX;
        [WriteOnly] public NativeArray<float> VelocityY;
        [WriteOnly] public NativeArray<float> VelocityZ;

        public void Execute(int i)
        {
            float3 v = NoiseField[i];
            VelocityX[i] = v.x;
            VelocityY[i] = v.y;
            VelocityZ[i] = v.z;
        }
    }

    /// <summary>
    /// Writes the three scalar velocity component buffers back into the xyz of a float3 vector-noise
    /// field, matching the monolith's write-back after the CPU fallback solve.
    /// </summary>
    [BurstCompile]
    public struct CopyVelocityToNoiseJob : IJobParallelFor
    {
        [WriteOnly] public NativeArray<float3> NoiseField;
        [ReadOnly] public NativeArray<float> VelocityX;
        [ReadOnly] public NativeArray<float> VelocityY;
        [ReadOnly] public NativeArray<float> VelocityZ;

        public void Execute(int i)
        {
            NoiseField[i] = new float3(VelocityX[i], VelocityY[i], VelocityZ[i]);
        }
    }
}
