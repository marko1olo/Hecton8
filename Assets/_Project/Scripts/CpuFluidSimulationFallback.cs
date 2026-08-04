using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public struct CpuFluidSimulationFallbackData
    {
        public int N;
        public NativeArray<float3> Velocity;
        public NativeArray<float3> Force;
        public NativeArray<float3> Vorticity;
        public NativeArray<float> VorticityMag;
        public NativeArray<float> Divergence;
        public NativeArray<float> PressureA;
        public NativeArray<float> PressureB;

        public void EnsureCapacity(int n)
        {
            if (N == n && Velocity.IsCreated) return;
            Dispose();
            N = n;
            int length = n * n * n;
            Velocity = new NativeArray<float3>(length, Allocator.Persistent);
            Force = new NativeArray<float3>(length, Allocator.Persistent);
            Vorticity = new NativeArray<float3>(length, Allocator.Persistent);
            VorticityMag = new NativeArray<float>(length, Allocator.Persistent);
            Divergence = new NativeArray<float>(length, Allocator.Persistent);
            PressureA = new NativeArray<float>(length, Allocator.Persistent);
            PressureB = new NativeArray<float>(length, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (Velocity.IsCreated) Velocity.Dispose();
            if (Force.IsCreated) Force.Dispose();
            if (Vorticity.IsCreated) Vorticity.Dispose();
            if (VorticityMag.IsCreated) VorticityMag.Dispose();
            if (Divergence.IsCreated) Divergence.Dispose();
            if (PressureA.IsCreated) PressureA.Dispose();
            if (PressureB.IsCreated) PressureB.Dispose();
        }
    }

    [BurstCompile]
    public struct CpuFluidAdvectionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Velocity;
        public NativeArray<float3> Positions;
        public NativeArray<float3> Velocities;
        public NativeArray<float> Lifetimes;
        public NativeArray<uint> Flags;
        public uint ActiveFlag;
        public float DeltaTime;
        public float GridSpacing;
        public int N;
        public int SliceShift;
        public int PlaneShift;
        public int Mask;
        public double3 TotalOffset;
        public float InvCellSize;

        public void Execute(int i)
        {
            if ((Flags[i] & ActiveFlag) == 0) return;

            float3 pos = Positions[i];
            double3 localCell = (new double3(pos.x, pos.y, pos.z) + TotalOffset) * InvCellSize;
            int x = math.clamp((int)math.floor(localCell.x) & Mask, 0, N - 1);
            int y = math.clamp((int)math.floor(localCell.y) & Mask, 0, N - 1);
            int z = math.clamp((int)math.floor(localCell.z) & Mask, 0, N - 1);
            
            // Simplified nearest neighbor advection for DOD compatibility
            int idx = x | (y << SliceShift) | (z << PlaneShift);
            float3 vel = Velocity[idx];
            
            Positions[i] += Velocities[i] * DeltaTime;
            Velocities[i] = vel;
            Lifetimes[i] -= DeltaTime;
            if (Lifetimes[i] <= 0f)
                Flags[i] &= ~ActiveFlag;
        }
    }

    [BurstCompile]
    public struct VorticityJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Velocity;
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
            
            if (x == 0 || x == N - 1 || y == 0 || y == N - 1 || z == 0 || z == N - 1)
            {
                int outIdx = x | (y << SliceShift) | (z << PlaneShift);
                Vorticity[outIdx] = float3.zero;
                VorticityMag[outIdx] = 0f;
                return;
            }

            int outIdxMain = x | (y << SliceShift) | (z << PlaneShift);
            
            float3 vz1 = Velocity[x | ((y+1) << SliceShift) | (z << PlaneShift)];
            float3 vz0 = Velocity[x | ((y-1) << SliceShift) | (z << PlaneShift)];
            float dwz_dy = (vz1.z - vz0.z) * InvSpacing2;
            
            float3 vy1 = Velocity[x | (y << SliceShift) | ((z+1) << PlaneShift)];
            float3 vy0 = Velocity[x | (y << SliceShift) | ((z-1) << PlaneShift)];
            float dwy_dz = (vy1.y - vy0.y) * InvSpacing2;
            
            float3 vx1 = Velocity[x | (y << SliceShift) | ((z+1) << PlaneShift)];
            float3 vx0 = Velocity[x | (y << SliceShift) | ((z-1) << PlaneShift)];
            float dwx_dz = (vx1.x - vx0.x) * InvSpacing2;
            
            float3 vzx1 = Velocity[(x+1) | (y << SliceShift) | (z << PlaneShift)];
            float3 vzx0 = Velocity[(x-1) | (y << SliceShift) | (z << PlaneShift)];
            float dwz_dx = (vzx1.z - vzx0.z) * InvSpacing2;
            
            float3 vyx1 = Velocity[(x+1) | (y << SliceShift) | (z << PlaneShift)];
            float3 vyx0 = Velocity[(x-1) | (y << SliceShift) | (z << PlaneShift)];
            float dwy_dx = (vyx1.y - vyx0.y) * InvSpacing2;
            
            float3 vxy1 = Velocity[x | ((y+1) << SliceShift) | (z << PlaneShift)];
            float3 vxy0 = Velocity[x | ((y-1) << SliceShift) | (z << PlaneShift)];
            float dwx_dy = (vxy1.x - vxy0.x) * InvSpacing2;

            float curlX = dwz_dy - dwy_dz;
            float curlY = dwx_dz - dwz_dx;
            float curlZ = dwy_dx - dwx_dy;

            float3 curl = new float3(curlX, curlY, curlZ);
            Vorticity[outIdxMain] = curl;
            VorticityMag[outIdxMain] = math.length(curl);
        }
    }
}
