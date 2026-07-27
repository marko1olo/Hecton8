using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// R99: single owner of the voxel vertex COLOR encoding.
    ///
    /// Channel contract (unchanged in meaning, fixed in continuity):
    ///   R = up-facing / depositional floor weight
    ///   G = wall + ceiling weight (1 - R)
    ///   B = reserved, always 0 — do not repurpose without Frame Debugger proof of what the voxel material
    ///       actually reads from COLOR
    ///   A = ambient occlusion (also mirrored into UV1.w; curvature travels in UV2.z)
    ///
    /// Both producers previously wrote `normal.y &gt; 0.6f ? red : green` — a 1-bit classifier that draws a
    /// hard material line at exactly 53.1 degrees around every cave mouth, ledge and boulder. The threshold
    /// is kept as the 50/50 crossover so the overall look is preserved; only the transition is now C1, so
    /// the material interpolates across the shoulder instead of snapping.
    ///
    /// The two producers are deliberately routed through ONE function: they had already been duplicated
    /// once, and duplicated field math is exactly how the live cave SDF silently drifted away from the
    /// canonical carve job.
    /// </summary>
    /// <remarks>
    /// Lives in Hecton8.Core.Contracts rather than Hecton8.Core because there are now two producers in
    /// two different assemblies: the legacy VoxelColorJob in Hecton8.Core and the Burst Surface Nets
    /// mesher in Hecton8.World.VoxelSurfaceNets. The mesher assembly cannot reference Hecton8.Core
    /// (Core consumes VoxelSurfaceNetsVault/VoxelVertexDTO/ChunkMeshingStateDTO, so that direction is a
    /// cycle), and Hecton8.Core grants InternalsVisibleTo only to the editor and save-system assemblies.
    /// Core.Contracts is referenced by both and references neither, so it is the only cycle-free home
    /// for a shared encoder. Both call sites already import this namespace.
    /// </remarks>
    public static class VoxelSurfaceColorEncoding
    {
        /// <summary>Lower edge of the floor/wall transition (n.y), ~67.9 degrees from vertical.</summary>
        private const float FloorTransitionMin = 0.375f;
        /// <summary>Width of the transition band in n.y. Centred on the historical 0.6 threshold.</summary>
        private const float FloorTransitionRange = 0.45f;

        /// <summary>Continuous up-facing weight in [0,1]. 0 = wall/ceiling, 1 = floor.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static float ResolveFloorWeight(float3 normal)
        {
            float3 safeNormal = math.select(new float3(0f, 1f, 0f), math.normalize(normal), math.lengthsq(normal) > 1e-6f && math.all(math.isfinite(normal)));
            float t = math.saturate((safeNormal.y - FloorTransitionMin) * (1f / FloorTransitionRange));
            return t * t * (3f - 2f * t);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Color32 Resolve(float3 normal, byte aoByte)
        {
            int floorByte = math.clamp((int)math.round(ResolveFloorWeight(normal) * 255f), 0, 255);
            return new Color32((byte)floorByte, (byte)(255 - floorByte), 0, aoByte);
        }

        /// <summary>
        /// Same encoding as <see cref="Resolve"/>, packed straight into a UNorm8x4-compatible uint so
        /// Burst mesher jobs can write <c>VoxelVertexDTO.ColorPacked</c> without a Color32 round-trip.
        /// Byte order is R,G,B,A from the low byte up, matching VertexAttributeFormat.UNorm8 x4.
        ///
        /// Consumed by Hecton_AbyssalVoxelRock.shader as `terrainSplatColor`: .rg are the normalised
        /// floor/wall material blend weights, .a feeds `vertexCaveAo`, which drives contact darkening
        /// and the bioluminescent crevice mask. Do not re-derive this packing at call sites.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static uint ResolvePacked(float3 normal, float ambientOcclusion)
        {
            uint floorByte = (uint)math.clamp((int)math.round(ResolveFloorWeight(normal) * 255f), 0, 255);
            uint aoByte = (uint)math.clamp((int)math.round(math.saturate(ambientOcclusion) * 255f), 0, 255);
            return floorByte | ((255u - floorByte) << 8) | (aoByte << 24);
        }
    }
}
