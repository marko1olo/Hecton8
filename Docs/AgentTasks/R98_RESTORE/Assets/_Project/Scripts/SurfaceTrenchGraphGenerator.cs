// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  SurfaceTrenchGraphGenerator.cs — Project HECTON-8 Surface Trench Builder   ║
// ║  Unity 6 | Pure C# + Unity.Mathematics | Zero GC at runtime               ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Generates 2D surface topologies for OpenTrench voxel integration.          ║
// ║  Instead of 3D volumetric wandering, this traces a constrained XZ river-    ║
// ║  carving path, laying down OpenTrench segments and Arch structures.         ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Caves;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Hecton8.World
{
    public static class SurfaceTrenchGraphGenerator
    {
        const int MAX_NODES = 64;
        const int MAX_SEGMENTS = 64;
        const int MAX_STRUCTURES = 32;

        [StructLayout(LayoutKind.Sequential, Size = 16)]
        public struct TrenchGraphCounts
        {
            public int Nodes;
            public int Segments;
            public int Structures;
        }

        public static bool TryMeasure(
            uint seed,
            float3 worldCenter,
            float volumeHalfExtent,
            out TrenchGraphCounts counts)
        {
            counts = default;

            System.Span<CaveNode> nodes = stackalloc CaveNode[MAX_NODES];
            System.Span<CaveTunnel> segments = stackalloc CaveTunnel[MAX_SEGMENTS];
            System.Span<CaveStructure> structures = stackalloc CaveStructure[MAX_STRUCTURES];

            return GenerateIntoScratch(
                seed,
                worldCenter,
                volumeHalfExtent,
                nodes,
                segments,
                structures,
                out counts);
        }

        public static bool TryFill(
            uint seed,
            float3 worldCenter,
            float volumeHalfExtent,
            NativeArray<CaveNode> nodes,
            NativeArray<CaveTunnel> segments,
            NativeArray<CaveStructure> structures,
            out TrenchGraphCounts counts)
        {
            counts = default;

            System.Span<CaveNode> scratchNodes = stackalloc CaveNode[MAX_NODES];
            System.Span<CaveTunnel> scratchSegments = stackalloc CaveTunnel[MAX_SEGMENTS];
            System.Span<CaveStructure> scratchStructures = stackalloc CaveStructure[MAX_STRUCTURES];

            if (!GenerateIntoScratch(
                seed,
                worldCenter,
                volumeHalfExtent,
                scratchNodes,
                scratchSegments,
                scratchStructures,
                out counts))
            {
                return false;
            }

            if (nodes.Length < counts.Nodes || 
                segments.Length < counts.Segments || 
                structures.Length < counts.Structures)
            {
                return false;
            }

            for (int i = 0; i < counts.Nodes; i++) nodes[i] = scratchNodes[i];
            for (int i = 0; i < counts.Segments; i++) segments[i] = scratchSegments[i];
            for (int i = 0; i < counts.Structures; i++) structures[i] = scratchStructures[i];

            return true;
        }

        private static bool GenerateIntoScratch(
            uint seed,
            float3 worldCenter,
            float volumeHalfExtent,
            System.Span<CaveNode> nodes,
            System.Span<CaveTunnel> segments,
            System.Span<CaveStructure> structures,
            out TrenchGraphCounts counts)
        {
            counts = default;
            if (seed == 0) return false;

            Random rng = new Random(seed);

            // Phase 1: 2D Random Walk for Nodes
            int numSteps = rng.NextInt(8, 16);
            float stepLength = volumeHalfExtent * rng.NextFloat(0.3f, 0.5f);
            
            float3 currentPos = worldCenter;
            currentPos.x -= volumeHalfExtent * 0.5f;
            currentPos.z -= volumeHalfExtent * 0.5f;
            
            float angle = rng.NextFloat(0, math.PI * 2f);
            
            nodes[0] = new CaveNode { position = currentPos, radii = new float3(5f) };
            counts.Nodes = 1;

            for (int i = 0; i < numSteps; i++)
            {
                angle += rng.NextFloat(-0.8f, 0.8f);
                float3 nextPos = currentPos;
                nextPos.x += math.cos(angle) * stepLength;
                nextPos.z += math.sin(angle) * stepLength;
                
                // Clamp XZ to keep the river roughly inside the volume
                nextPos.x = math.clamp(nextPos.x, worldCenter.x - volumeHalfExtent, worldCenter.x + volumeHalfExtent);
                nextPos.z = math.clamp(nextPos.z, worldCenter.z - volumeHalfExtent, worldCenter.z + volumeHalfExtent);

                nodes[counts.Nodes] = new CaveNode { position = nextPos, radii = new float3(5f) };
                counts.Nodes++;
                
                float width = rng.NextFloat(6f, 14f);
                float depthRadius = width * 1.5f; // Used for CaveTunnel base radii
                
                // CaveTunnelType.OpenTrench will be intercepted by the VoxelEngine 
                // to extrude vertically.
                segments[counts.Segments] = new CaveTunnel
                {
                    pointA = currentPos,
                    pointB = nextPos,
                    radiusA = depthRadius,
                    radiusB = depthRadius,
                    tunnelType = CaveTunnelType.OpenTrench,
                    widthScale = width,
                    heightScale = 1.0f,
                    blendRadius = 4f,
                    warpAmount = 1.5f // <--- KEY: Apply domain warping so trench is organic, not a primitive capsule
                };
                counts.Segments++;

                currentPos = nextPos;
            }

            // Phase 2: Arch Placement across narrow segments
            for (int i = 0; i < counts.Segments; i++)
            {
                CaveTunnel seg = segments[i];
                if (seg.widthScale < 8.0f) // Narrow trench
                {
                    if (rng.NextFloat() > 0.4f && counts.Structures < MAX_STRUCTURES)
                    {
                        float3 midPoint = math.lerp(seg.pointA, seg.pointB, 0.5f);
                        // R95 FIX: XZ clamping can collapse a walk step at the volume border into a
                        // zero-length segment; math.normalize(0) is NaN and poisoned the arch
                        // structure (voxels.md: preserve finite values). normalizesafe falls back
                        // to +X and the density job renders a valid (if arbitrary-facing) arch.
                        float3 dir = math.normalizesafe(seg.pointB - seg.pointA, new float3(1f, 0f, 0f));
                        float3 right = new float3(dir.z, 0f, -dir.x); // Perpendicular to trench
                        float spread = seg.widthScale * 1.25f;

                        structures[counts.Structures] = new CaveStructure
                        {
                            structureType = CaveStructureType.Arch,
                            position = midPoint + right * spread,
                            pointB = midPoint - right * spread,
                            size = new float3(0f, rng.NextFloat(6f, 12f), rng.NextFloat(1.5f, 3.5f)), // y=rise, z=tubeRadius
                            blendRadius = 4f,
                            noiseAmount = 1.2f // <--- KEY: Applies Layered Arch Noise + Fractal
                        };
                        counts.Structures++;
                    }
                }
            }

            return counts.Nodes > 0;
        }
    }
}
