using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Foundational DOD contracts for the procedural wreckage generator mandate.
    /// This file owns blittable template layouts only. Runtime authoring/execution stays in <see cref="ProceduralWreckGenerator"/>.
    /// </summary>
    internal static class TOOL_Procedural_Wreckage_Generator
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        internal struct GridCell
        {
            public ushort PossibleModuleMask;
            public byte CollapsedModuleId;
            public byte SocketConstraints;
            public float Entropy;
            private uint _reserved0;
            private uint _reserved1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        internal struct MeshDataSlice
        {
            public uint VertexStart;
            public uint VertexCount;
            public uint IndexStart;
            public uint IndexCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 48)]
        internal struct ModuleDefinition
        {
            public ushort NorthSocket;
            public ushort EastSocket;
            public ushort SouthSocket;
            public ushort WestSocket;
            public ushort TopSocket;
            public ushort BottomSocket;
            public MeshDataSlice MeshSlice;
            public float3 LocalBoundsCenter;
            public float3 LocalBoundsSize;
            public byte DrawCallPriority;
            public byte EmitsGeometry;
            public byte EmitsNavProxy;
            public byte UniversalConnector;
        }

        internal struct WfcState : IDisposable
        {
            public NativeArray<GridCell> Grid;
            public NativeQueue<int3> PropagationQueue;
            public NativeArray<uint4> RngState;
            public NativeParallelHashMap<int3, byte> CollapseOrder;

            public WfcState(int cellCount, int collapseOrderCapacity, Allocator allocator)
            {
                int safeCellCount = math.max(1, cellCount);
                int safeCollapseCapacity = math.max(1, collapseOrderCapacity);
                Grid = new NativeArray<GridCell>(safeCellCount, allocator, NativeArrayOptions.ClearMemory);
                PropagationQueue = new NativeQueue<int3>(allocator);
                RngState = new NativeArray<uint4>(1, allocator, NativeArrayOptions.ClearMemory);
                CollapseOrder = new NativeParallelHashMap<int3, byte>(safeCollapseCapacity, allocator);

                NativeAllocationLifetime lifetime = ResolveLifetime(allocator);
                NativeMemorySentinel.RegisterNativeArray(Grid, nameof(TOOL_Procedural_Wreckage_Generator), nameof(Grid), lifetime);
                NativeMemorySentinel.RegisterNativeQueue(PropagationQueue, safeCellCount, nameof(TOOL_Procedural_Wreckage_Generator), nameof(PropagationQueue), lifetime);
                NativeMemorySentinel.RegisterNativeArray(RngState, nameof(TOOL_Procedural_Wreckage_Generator), nameof(RngState), lifetime);
                NativeMemorySentinel.RegisterNativeParallelHashMap(CollapseOrder, nameof(TOOL_Procedural_Wreckage_Generator), nameof(CollapseOrder), lifetime);
            }

            public void Dispose()
            {
                if (Grid.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(Grid);
                    Grid.Dispose();
                }

                if (PropagationQueue.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeQueue(nameof(TOOL_Procedural_Wreckage_Generator), nameof(PropagationQueue));
                    PropagationQueue.Dispose();
                }

                if (RngState.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(RngState);
                    RngState.Dispose();
                }

                if (CollapseOrder.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(TOOL_Procedural_Wreckage_Generator), nameof(CollapseOrder));
                    CollapseOrder.Dispose();
                }
            }

            private static NativeAllocationLifetime ResolveLifetime(Allocator allocator)
            {
                return allocator == Allocator.Persistent
                    ? NativeAllocationLifetime.Session
                    : NativeAllocationLifetime.TransientArena;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct XorShift32Kernel
        {
            public uint4 State;

            public XorShift32Kernel(uint seed)
            {
                uint safeSeed = seed == 0u ? 0x6E624EB7u : seed;
                State = new uint4(
                    safeSeed,
                    safeSeed ^ 0x9E3779B9u,
                    (safeSeed << 13) ^ 0x85EBCA6Bu,
                    (safeSeed >> 17) ^ 0xC2B2AE35u);
            }

            public uint NextUInt()
            {
                uint t = State.x ^ (State.x << 11);
                State.x = State.y;
                State.y = State.z;
                State.z = State.w;
                State.w = State.w ^ (State.w >> 19) ^ (t ^ (t >> 8));
                return State.w;
            }

            public float NextFloat01()
            {
                return NextUInt() * 2.3283064365386963e-10f;
            }
        }

        public static uint ComputeSeed(in AbsoluteUniversePosition aup, uint salt = 0u)
        {
            uint gridX = unchecked((uint)aup.GridX);
            uint gridY = unchecked((uint)aup.GridY);
            uint gridZ = unchecked((uint)aup.GridZ);
            uint localX = math.asuint(aup.LocalX);
            uint localY = math.asuint(aup.LocalY);
            uint localZ = math.asuint(aup.LocalZ);

            uint hash =
                (gridX * 73856093u) ^
                (gridY * 19349663u) ^
                (gridZ * 83492791u) ^
                (localX * 2654435761u) ^
                (localY * 2246822519u) ^
                (localZ * 3266489917u);

            return (hash ^ (hash >> 16)) ^ salt;
        }

        public static void InitializeUniformGrid(NativeArray<GridCell> grid, ushort possibleModuleMask)
        {
            if (!grid.IsCreated)
                return;

            uint possibleModuleCount = (uint)math.countbits((uint)possibleModuleMask);
            float entropy = math.log2(math.max(1f, (float)possibleModuleCount));
            for (int i = 0; i < grid.Length; i++)
            {
                grid[i] = new GridCell
                {
                    PossibleModuleMask = possibleModuleMask,
                    CollapsedModuleId = byte.MaxValue,
                    SocketConstraints = 0,
                    Entropy = entropy
                };
            }
        }
    }
}
