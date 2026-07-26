// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  CaveGraphGenerator.cs — Project HECTON-8 Cave Graph Builder               ║
// ║  Unity 6 | Pure C# + Unity.Mathematics | Zero GC at runtime               ║
// ║  v1.0 — Procedural cave graph from seed                                    ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Takes a seed + CavePreset and deterministically generates the complete     ║
// ║  cave topology: rooms, tunnels, and entrances. Output is a set of          ║
// ║  NativeArrays ready for direct injection into VoxelDensityJob.             ║
// ║                                                                             ║
// ║  ALGORITHM OVERVIEW:                                                        ║
// ║  ───────────────────                                                        ║
// ║  1. ROOM PLACEMENT — Constrained random walk from entrance inward.          ║
// ║     Rooms are placed sequentially along a primary path, with stochastic     ║
// ║     branching creating side-paths. Room types (sphere, ellipsoid, shaft,    ║
// ║     hall, crevice) are selected by weighted random per CavePreset.          ║
// ║                                                                             ║
// ║  2. TUNNEL ROUTING — Sequential rooms are connected first (guarantees       ║
// ║     full connectivity). Then extra connections are added between nearby     ║
// ║     non-adjacent rooms to create loops and alternative paths.              ║
// ║                                                                             ║
// ║  3. ENTRANCE GENERATION — The room(s) closest to the terrain surface       ║
// ║     receive entrance funnels. Funnels are conic capsules oriented from     ║
// ║     the surface point inward toward the room center.                       ║
// ║                                                                             ║
// ║  4. BOUNDS ENFORCEMENT — All rooms are clamped to stay within the          ║
// ║     volume cube. Rooms that would protrude above terrain surface are       ║
// ║     pushed deeper. Rooms that would exit the volume laterally are          ║
// ║     pulled inward.                                                          ║
// ║                                                                             ║
// ║  THREADING:                                                                 ║
// ║  ──────────                                                                 ║
// ║  Generate() MUST be called on the main thread (allocates NativeArrays).    ║
// ║  Caller is responsible for disposing all output NativeArrays.              ║
// ║                                                                             ║
// ║  DETERMINISM:                                                               ║
// ║  ────────────                                                               ║
// ║  Same seed + same preset = identical output. Always.                        ║
// ║  Uses Unity.Mathematics.Random (Xorshift128) exclusively.                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using Unity.Collections;
using Unity.Mathematics;
using Hecton8.Caves;
using System.Globalization;
using System.Runtime.InteropServices;

/// <summary>
/// Static procedural cave graph generator.
/// Converts (seed + preset + world context) into NativeArrays of SDF primitives.
///
/// Usage:
///   CaveGraphGenerator.TryMeasure(seed, preset, worldCenter, terrainHeight, volumeHalfExtent, out counts);
///   // Caller allocates exact NativeArrays from counts, then calls TryFill(...).
///   // ... pass arrays to VoxelDensityJob ...
///   // ... after mesh is built, Dispose all arrays ...
/// </summary>
public static class CaveGraphGenerator
{
    // ════════════════════════════════════════════════════════════════════════
    //  CONSTANTS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Maximum rooms we ever allocate for. Prevents runaway generation.</summary>
    const int MAX_ROOMS = 64;

    /// <summary>Maximum tunnels (sequential + extra). Generous upper bound.</summary>
    const int MAX_TUNNELS = 128;

    /// <summary>Maximum entrances.</summary>
    const int MAX_ENTRANCES = 8;

    /// <summary>Maximum structures kept in stack scratch. Authored presets are far below this; custom presets fail closed by truncation.</summary>
    const int MAX_STRUCTURES = 128;
    const float DEFAULT_ENTRANCE_RADIUS = 3f;
    const float MIN_ENTRANCE_RADIUS = 0.5f;
    const float MAX_ENTRANCE_RADIUS = 15f;
    const float DEFAULT_ENTRANCE_FUNNEL_LENGTH = 12f;
    const float MIN_ENTRANCE_FUNNEL_LENGTH = 3f;
    const float MAX_ENTRANCE_FUNNEL_LENGTH = 40f;

    /// <summary>Minimum distance between room centers as fraction of combined radii.
    /// Prevents rooms from overlapping so much they merge into a blob.</summary>
    const float MIN_SEPARATION_FACTOR = 0.4f;

    /// <summary>Maximum attempts to place a room before giving up on that room.</summary>
    const int PLACEMENT_ATTEMPTS = 20;

    /// <summary>Base minimum distance from volume edge for room centers.
    /// v4.1: Actual margin is computed dynamically as:
    ///   effectiveMargin = BASE_EDGE_MARGIN + warpAmplitude + maxBlendRadius
    /// This prevents domain-warped rooms from bleeding through sealed edges.</summary>
    const float BASE_EDGE_MARGIN = 4f;

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct CaveGraphCounts
    {
        public int Nodes;
        public int Tunnels;
        public int Entrances;
        public int Structures;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Measure complete cave graph counts from seed and preset.
    /// </summary>
    /// <param name="seed">Deterministic seed. Same seed = same cave.</param>
    /// <param name="preset">Cave configuration (room counts, sizes, noise, etc.).</param>
    /// <param name="worldCenter">World-space center of the voxel volume.</param>
    /// <param name="terrainHeightAtCenter">Terrain surface Y at worldCenter.
    /// Used to ensure rooms stay below surface and entrances connect to surface.</param>
    /// <param name="volumeHalfExtent">Half-size of the volume cube in meters.
    /// = (gridDimension * voxelSize) / 2. Rooms are constrained within this box.</param>
    /// <param name="counts">OUTPUT: Exact array lengths required for TryFill.</param>
    public static bool TryMeasure(
        uint seed,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeightAtCenter,
        float volumeHalfExtent,
        out CaveGraphCounts counts)
    {
        counts = default;
        if (preset == null)
            return false;

        System.Span<CaveNode> rooms = stackalloc CaveNode[MAX_ROOMS];
        System.Span<CaveTunnel> tunnels = stackalloc CaveTunnel[MAX_TUNNELS];
        System.Span<CaveEntrance> entrances = stackalloc CaveEntrance[MAX_ENTRANCES];
        System.Span<CaveStructure> structures = stackalloc CaveStructure[MAX_STRUCTURES];
        System.Span<int> branchIndices = stackalloc int[MAX_ROOMS];
        System.Span<byte> usedRooms = stackalloc byte[MAX_ROOMS];
        return GenerateIntoScratch(
            seed,
            preset,
            worldCenter,
            terrainHeightAtCenter,
            volumeHalfExtent,
            rooms,
            tunnels,
            entrances,
            structures,
            branchIndices,
            usedRooms,
            out counts);
    }

    public static bool TryFill(
        uint seed,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeightAtCenter,
        float volumeHalfExtent,
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        NativeArray<CaveStructure> structures,
        out CaveGraphCounts counts)
    {
        counts = default;
        if (preset == null)
            return false;

        System.Span<CaveNode> generatedNodes = stackalloc CaveNode[MAX_ROOMS];
        System.Span<CaveTunnel> generatedTunnels = stackalloc CaveTunnel[MAX_TUNNELS];
        System.Span<CaveEntrance> generatedEntrances = stackalloc CaveEntrance[MAX_ENTRANCES];
        System.Span<CaveStructure> generatedStructures = stackalloc CaveStructure[MAX_STRUCTURES];
        System.Span<int> branchIndices = stackalloc int[MAX_ROOMS];
        System.Span<byte> usedRooms = stackalloc byte[MAX_ROOMS];

        if (!GenerateIntoScratch(
            seed,
            preset,
            worldCenter,
            terrainHeightAtCenter,
            volumeHalfExtent,
            generatedNodes,
            generatedTunnels,
            generatedEntrances,
            generatedStructures,
            branchIndices,
            usedRooms,
            out counts))
        {
            return false;
        }

        bool hasCapacity =
            HasCapacity(nodes, counts.Nodes) &&
            HasCapacity(tunnels, counts.Tunnels) &&
            HasCapacity(entrances, counts.Entrances) &&
            HasCapacity(structures, counts.Structures);

        if (hasCapacity)
        {
            CopySpan(generatedNodes.Slice(0, counts.Nodes), nodes);
            CopySpan(generatedTunnels.Slice(0, counts.Tunnels), tunnels);
            CopySpan(generatedEntrances.Slice(0, counts.Entrances), entrances);
            CopySpan(generatedStructures.Slice(0, counts.Structures), structures);
        }

        return hasCapacity;
    }

    private static bool HasCapacity<T>(NativeArray<T> destination, int requiredLength) where T : unmanaged
    {
        return requiredLength <= 0 || (destination.IsCreated && destination.Length >= requiredLength);
    }

    private static void CopySpan<T>(System.ReadOnlySpan<T> source, NativeArray<T> destination) where T : struct
    {
        for (int i = 0; i < source.Length; i++)
            destination[i] = source[i];
    }

    private static bool GenerateIntoScratch(
        uint seed,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeightAtCenter,
        float volumeHalfExtent,
        System.Span<CaveNode> rooms,
        System.Span<CaveTunnel> tunnels,
        System.Span<CaveEntrance> entrances,
        System.Span<CaveStructure> structures,
        System.Span<int> branchIndices,
        System.Span<byte> usedRooms,
        out CaveGraphCounts counts)
    {
        counts = default;
        if (preset == null ||
            rooms.Length < MAX_ROOMS ||
            tunnels.Length < MAX_TUNNELS ||
            entrances.Length < MAX_ENTRANCES ||
            structures.Length < MAX_STRUCTURES ||
            branchIndices.Length < MAX_ROOMS ||
            usedRooms.Length < MAX_ROOMS)
        {
            return false;
        }

        var rng = new Random(seed != 0 ? seed : 1u);

        int roomCount = rng.NextInt(preset.minRooms, preset.maxRooms + 1);
        roomCount = math.clamp(roomCount, 1, MAX_ROOMS);

        // ── v4.1: Dynamic edge margin ──
        // Accounts for domain warp displacement and blend radius overflow
        float dynamicMargin = BASE_EDGE_MARGIN
                            + preset.warpAmplitude
                            + preset.globalBlendK * 0.5f;

        // ── v4.1: Ensure volume top face reaches terrain surface ──
        // If terrain is above volume top, entrances will be cut off.
        // Warn but don't crash — the volume center may need adjustment by caller.
        float volumeTopY = worldCenter.y + volumeHalfExtent;
        if (terrainHeightAtCenter > volumeTopY)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogWarning(
                "[CaveGraph] Terrain height (" + terrainHeightAtCenter.ToString("F1", CultureInfo.InvariantCulture) + "m) is above " +
                "volume top (" + volumeTopY.ToString("F1", CultureInfo.InvariantCulture) + "m). Entrances may be clipped. " +
                "Consider raising worldCenter.y or increasing gridDimension.");
#endif
        }

        int actualRoomCount = PlaceRooms(
            ref rng,
            preset,
            worldCenter,
            terrainHeightAtCenter,
            volumeHalfExtent,
            roomCount,
            dynamicMargin,
            rooms,
            branchIndices);

        int actualTunnelCount = GenerateTunnels(
            ref rng,
            preset,
            rooms.Slice(0, actualRoomCount),
            tunnels);

        int actualEntranceCount = GenerateEntrances(
            ref rng,
            preset,
            worldCenter,
            terrainHeightAtCenter,
            volumeHalfExtent,
            dynamicMargin,
            rooms.Slice(0, actualRoomCount),
            usedRooms,
            entrances);

        int actualStructureCount = 0;
        if (preset.enableStructures && preset.maxStructures > 0)
        {
            actualStructureCount = GenerateStructures(
                ref rng,
                preset,
                worldCenter,
                terrainHeightAtCenter,
                volumeHalfExtent,
                rooms.Slice(0, actualRoomCount),
                tunnels.Slice(0, actualTunnelCount),
                structures);
        }

        counts = new CaveGraphCounts
        {
            Nodes = actualRoomCount,
            Tunnels = actualTunnelCount,
            Entrances = actualEntranceCount,
            Structures = actualStructureCount
        };
        return true;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PHASE 1: ROOM PLACEMENT
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Places rooms using constrained random walk with branching.
    ///
    /// Algorithm:
    /// 1. First room placed near top of volume (close to terrain surface for entrance).
    /// 2. Each subsequent room placed at a random offset from the previous one.
    /// 3. Direction biased downward by verticalSpread parameter.
    /// 4. Occasionally branches from an earlier room instead of the latest one.
    /// 5. All rooms clamped to stay within volume bounds and below terrain.
    /// 6. Rooms that would overlap existing rooms too much are retried or skipped.
    /// </summary>
    static int PlaceRooms(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        int targetCount,
        float edgeMargin,        // v4.1: dynamic margin
        System.Span<CaveNode> rooms,
        System.Span<int> branchIndices)
    {
        float3 volumeMin = worldCenter - volumeHalfExtent;
        float3 volumeMax = worldCenter + volumeHalfExtent;

        float firstRoomDepth = math.min(
            preset.maxDepth * 0.15f,
            volumeHalfExtent * 0.3f);

        float3 firstPos = new float3(
            worldCenter.x,
            terrainHeight - firstRoomDepth - preset.maxRoomRadius,
            worldCenter.z);

        firstPos = ClampToVolume(firstPos, volumeMin, volumeMax, edgeMargin);

        CaveNode firstRoom = CreateRoom(ref rng, preset, firstPos);
        rooms[0] = firstRoom;
        int roomCount = 1;

        int branchPointCount = 0;

        float3 currentPos = firstPos;
        int currentRoomIdx = 0;

        for (int i = 1; i < targetCount; i++)
        {
            for (int attempt = 0; attempt < PLACEMENT_ATTEMPTS; attempt++)
            {
                float3 originPos = currentPos;
                if (branchPointCount > 0 && rng.NextFloat() < 0.25f)
                {
                    int branchIdx = branchIndices[rng.NextInt(0, branchPointCount)];
                    originPos = rooms[branchIdx].position;
                }

                float3 dir = rng.NextFloat3Direction();
                dir.y = math.lerp(dir.y, -math.abs(dir.y), preset.verticalSpread);
                dir = math.normalizesafe(dir, new float3(0, -1, 0));

                float prevRadius = roomCount > 0
                    ? math.cmax(rooms[roomCount - 1].radii)
                    : preset.minRoomRadius;
                float nextRadiusEstimate = rng.NextFloat(preset.minRoomRadius, preset.maxRoomRadius);
                float stepDist = (prevRadius + nextRadiusEstimate) * rng.NextFloat(0.8f, 1.6f);

                float3 candidatePos = originPos + dir * stepDist;

                // v4.1: margin includes room radius to prevent any part from reaching edge
                float roomMargin = edgeMargin + nextRadiusEstimate * 0.5f;
                candidatePos = ClampToVolume(candidatePos, volumeMin, volumeMax, roomMargin);

                // Stay below terrain
                float maxY = terrainHeight - nextRadiusEstimate * 1.2f;
                candidatePos.y = math.min(candidatePos.y, maxY);

                // Depth limit
                float minY = terrainHeight - preset.maxDepth;
                minY = math.max(minY, volumeMin.y + roomMargin);
                candidatePos.y = math.max(candidatePos.y, minY);

                if (IsRoomTooClose(candidatePos, nextRadiusEstimate, rooms.Slice(0, roomCount)))
                    continue;

                CaveNode room = CreateRoom(ref rng, preset, candidatePos);
                if (roomCount >= rooms.Length)
                    return roomCount;

                rooms[roomCount] = room;

                currentPos = candidatePos;
                currentRoomIdx = roomCount;
                roomCount++;

                if (rng.NextFloat() < 0.35f)
                {
                    if (branchPointCount < MAX_ROOMS)
                    {
                        branchIndices[branchPointCount] = currentRoomIdx;
                        branchPointCount++;
                    }
                }

                break;
            }
        }

        return roomCount;
    }

    /// <summary>
    /// Creates a single CaveNode with randomized type and dimensions.
    /// </summary>
    static CaveNode CreateRoom(ref Random rng, CavePreset preset, float3 position)
    {
        // ── Select room type by weighted random ──
        CaveRoomType roomType = SelectRoomType(ref rng, preset);

        // ── Base radius ──
        float baseRadius = rng.NextFloat(preset.minRoomRadius, preset.maxRoomRadius);

        // ── Compute radii based on type ──
        float3 radii = ComputeRoomRadii(ref rng, roomType, baseRadius);

        // ── Blend radius: proportional to room size with randomization ──
        float blendK = preset.globalBlendK * rng.NextFloat(0.6f, 1.4f);
        // Larger rooms get slightly more blending
        blendK = math.max(blendK, baseRadius * 0.3f);

        return new CaveNode
        {
            position       = position,
            radii          = radii,
            blendRadius    = blendK,
            noiseScale     = rng.NextFloat(0.7f, 1.3f),
            noiseAmplitude = rng.NextFloat(0.3f, 1.5f),
            roomType       = roomType,
            _pad0 = 0, _pad1 = 0, _pad2 = 0
        };
    }

    /// <summary>
    /// Select room type using weighted probabilities from preset.
    /// Remaining probability (after special types) goes to Ellipsoid,
    /// with 30% of Ellipsoid chance converting to Sphere.
    /// </summary>
    static CaveRoomType SelectRoomType(ref Random rng, CavePreset preset)
    {
        float roll = rng.NextFloat();
        float cumulative = 0f;

        cumulative += preset.verticalShaftChance;
        if (roll < cumulative) return CaveRoomType.VerticalShaft;

        cumulative += preset.flatHallChance;
        if (roll < cumulative) return CaveRoomType.FlatHall;

        cumulative += preset.creviceChance;
        if (roll < cumulative) return CaveRoomType.Crevice;

        // Remaining probability: Ellipsoid or Sphere
        if (rng.NextFloat() < 0.3f) return CaveRoomType.Sphere;
        return CaveRoomType.Ellipsoid;
    }

    /// <summary>
    /// Compute XYZ radii for a given room type and base size.
    /// </summary>
    static float3 ComputeRoomRadii(ref Random rng, CaveRoomType type, float baseRadius)
    {
        switch (type)
        {
            case CaveRoomType.Sphere:
                return new float3(baseRadius, baseRadius, baseRadius);

            case CaveRoomType.Ellipsoid:
                return new float3(
                    baseRadius * rng.NextFloat(0.7f, 1.4f),
                    baseRadius * rng.NextFloat(0.5f, 1.0f),
                    baseRadius * rng.NextFloat(0.7f, 1.4f));

            case CaveRoomType.VerticalShaft:
                // Tall and narrow: small XZ, large Y
                return new float3(
                    baseRadius * rng.NextFloat(0.35f, 0.6f),
                    baseRadius * rng.NextFloat(1.5f, 3.0f),
                    baseRadius * rng.NextFloat(0.35f, 0.6f));

            case CaveRoomType.FlatHall:
                // Wide and low: large XZ, small Y
                return new float3(
                    baseRadius * rng.NextFloat(1.2f, 2.0f),
                    baseRadius * rng.NextFloat(0.25f, 0.45f),
                    baseRadius * rng.NextFloat(1.2f, 2.0f));

            case CaveRoomType.Crevice:
                // Tall narrow crack: small X, large Y, medium Z
                return new float3(
                    baseRadius * rng.NextFloat(0.15f, 0.35f),
                    baseRadius * rng.NextFloat(1.0f, 1.8f),
                    baseRadius * rng.NextFloat(0.6f, 1.2f));

            default:
                return new float3(baseRadius, baseRadius, baseRadius);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PHASE 2: TUNNEL GENERATION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Connects rooms with tunnels.
    ///
    /// Strategy:
    /// 1. Sequential connections (room 0→1, 1→2, 2→3...) guarantee full connectivity.
    /// 2. Extra connections between nearby non-adjacent rooms create loops.
    /// 3. Tunnel properties (radius, cross-section, warp) randomized per preset.
    /// </summary>
    static int GenerateTunnels(
        ref Random rng,
        CavePreset preset,
        System.ReadOnlySpan<CaveNode> rooms,
        System.Span<CaveTunnel> tunnels)
    {
        int roomCount = rooms.Length;
        if (roomCount < 2)
            return 0;

        int tunnelCount = 0;

        // ── Phase 2a: Sequential connections (guaranteed connectivity) ──
        for (int i = 0; i < roomCount - 1; i++)
        {
            CaveTunnel tunnel = CreateTunnel(ref rng, preset, rooms[i], rooms[i + 1]);
            tunnels[tunnelCount] = tunnel;
            tunnelCount++;

            if (tunnelCount >= tunnels.Length)
                return tunnelCount;
        }

        // ── Phase 2b: Extra connections (loops) ──
        // Only between rooms that are spatially close but not already sequentially connected
        for (int i = 0; i < roomCount; i++)
        {
            for (int j = i + 2; j < roomCount; j++)
            {
                if (tunnelCount >= tunnels.Length)
                    return tunnelCount;

                // Skip if rooms are too far apart. These are local generator coordinates, not Transform authority.
                float3 roomDelta = rooms[i].position - rooms[j].position;
                float dist = math.length(roomDelta);
                float combinedRadii = math.cmax(rooms[i].radii) + math.cmax(rooms[j].radii);

                // Only connect rooms that are within ~3x their combined radii
                if (dist > combinedRadii * 3.5f) continue;

                // Probability check
                if (rng.NextFloat() >= preset.extraConnectionChance) continue;

                CaveTunnel extraTunnel = CreateTunnel(ref rng, preset, rooms[i], rooms[j]);
                tunnels[tunnelCount] = extraTunnel;
                tunnelCount++;
            }
        }

        return tunnelCount;
    }

    /// <summary>
    /// Creates a single tunnel between two rooms with randomized properties.
    /// </summary>
    static CaveTunnel CreateTunnel(ref Random rng, CavePreset preset,
                                    CaveNode roomA, CaveNode roomB)
    {
        // ── Select tunnel type ──
        CaveTunnelType tunnelType = SelectTunnelType(ref rng, preset);

        // ── Radii ──
        float radiusA = rng.NextFloat(preset.minTunnelRadius, preset.maxTunnelRadius);
        float radiusB = radiusA * rng.NextFloat(0.6f, 1.4f); // Slight taper variation

        // ── Cross-section scaling ──
        float heightScale = 1f;
        float widthScale = 1f;
        switch (tunnelType)
        {
            case CaveTunnelType.Tall:
                heightScale = rng.NextFloat(1.5f, 2.5f);
                widthScale = rng.NextFloat(0.4f, 0.7f);
                break;
            case CaveTunnelType.Wide:
                heightScale = rng.NextFloat(0.3f, 0.6f);
                widthScale = rng.NextFloat(1.5f, 2.5f);
                break;
        }

        // ── Blend radius ──
        float blendK = preset.globalBlendK * rng.NextFloat(0.7f, 1.3f);

        // ── Per-tunnel warp ──
        float warp = preset.tunnelWarpAmount * rng.NextFloat(0.3f, 1.5f);

        return new CaveTunnel
        {
            pointA      = roomA.position,
            pointB      = roomB.position,
            radiusA     = radiusA,
            radiusB     = radiusB,
            blendRadius = blendK,
            heightScale = heightScale,
            widthScale  = widthScale,
            warpAmount  = warp,
            tunnelType  = tunnelType,
            _pad0 = 0, _pad1 = 0, _pad2 = 0
        };
    }

    /// <summary>
    /// Select tunnel cross-section type using preset probabilities.
    /// </summary>
    static CaveTunnelType SelectTunnelType(ref Random rng, CavePreset preset)
    {
        float roll = rng.NextFloat();
        if (roll < preset.tallTunnelChance) return CaveTunnelType.Tall;
        if (roll < preset.tallTunnelChance + preset.wideTunnelChance) return CaveTunnelType.Wide;
        return CaveTunnelType.Round;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PHASE 3: ENTRANCE GENERATION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates entrance funnels connecting terrain surface to nearest rooms.
    ///
    /// Strategy:
    /// 1. Find rooms closest to the terrain surface.
    /// 2. For each entrance, project upward from room center to terrain surface.
    /// 3. Create a conic capsule from surface point inward.
    /// 4. Avoid placing multiple entrances near the same room.
    /// </summary>
    static int GenerateEntrances(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        float edgeMargin,         // v4.1
        System.ReadOnlySpan<CaveNode> rooms,
        System.Span<byte> usedRooms,
        System.Span<CaveEntrance> entrances)
    {
        if (rooms.Length == 0)
            return 0;

        if (!IsFinite(worldCenter) ||
            !math.isfinite(terrainHeight) ||
            !math.isfinite(volumeHalfExtent) ||
            volumeHalfExtent <= 0f)
        {
            return 0;
        }

        // R95 FIX: min clamp floor was 1, which force-injected a phantom entrance funnel into
        // presets that legitimately request zero entrances (e.g. SurfaceTrench min/max = 0).
        // Presets with minEntrances >= 1 keep byte-identical rng draw order and results.
        int minEntrances = math.clamp(preset.minEntrances, 0, MAX_ENTRANCES);
        int maxEntrances = math.clamp(preset.maxEntrances, minEntrances, MAX_ENTRANCES);
        if (maxEntrances <= 0)
            return 0;

        int entranceCount = rng.NextInt(minEntrances, maxEntrances + 1);
        if (entranceCount <= 0)
            return 0;
        entranceCount = math.min(entranceCount, rooms.Length);
        entranceCount = math.min(entranceCount, entrances.Length);

        // v4.1: Check that terrain surface is reachable from volume
        float safeHalfExtent = ClampFinite(volumeHalfExtent, 1f, 1f, 8192f);
        float maxRadiusForVolume = math.max(MIN_ENTRANCE_RADIUS, math.min(MAX_ENTRANCE_RADIUS, safeHalfExtent * 0.25f));
        float maxFunnelForVolume = math.max(MIN_ENTRANCE_FUNNEL_LENGTH, math.min(MAX_ENTRANCE_FUNNEL_LENGTH, safeHalfExtent * 1.5f));
        float safeEntranceRadius = ClampFinite(preset.entranceRadius, DEFAULT_ENTRANCE_RADIUS, MIN_ENTRANCE_RADIUS, maxRadiusForVolume);
        float safeEntranceFunnelLength = ClampFinite(
            preset.entranceFunnelLength,
            DEFAULT_ENTRANCE_FUNNEL_LENGTH,
            MIN_ENTRANCE_FUNNEL_LENGTH,
            maxFunnelForVolume);
        float safeEdgeMargin = ClampFinite(edgeMargin, BASE_EDGE_MARGIN, 0f, math.max(0f, safeHalfExtent * 0.4f));
        float volumeTopY = worldCenter.y + safeHalfExtent;

        for (int r = 0; r < rooms.Length; r++)
            usedRooms[r] = 0;

        int writtenCount = 0;
        for (int e = 0; e < entranceCount; e++)
        {
            int bestRoom = -1;
            float bestScore = float.MaxValue;

            for (int r = 0; r < rooms.Length; r++)
            {
                if (usedRooms[r] != 0) continue;

                float3 roomPos = rooms[r].position;
                if (!IsFinite(roomPos) || !IsFinite(rooms[r].radii))
                    continue;

                float distToSurface = terrainHeight - roomPos.y;
                if (distToSurface < 0) continue;

                float horizontalDist = math.length(roomPos.xz - worldCenter.xz);
                float horizontalPenalty = horizontalDist * 0.5f;
                float sizeBonus = -math.cmax(rooms[r].radii) * 0.3f;

                float score = distToSurface + horizontalPenalty + sizeBonus;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestRoom = r;
                }
            }

            if (bestRoom < 0) break;

            usedRooms[bestRoom] = 1;

            float3 targetRoomPos = rooms[bestRoom].position;
            float targetRoomMaxRadius = ClampFinite(
                math.cmax(rooms[bestRoom].radii),
                safeEntranceRadius,
                safeEntranceRadius,
                safeHalfExtent);

            float2 horizontalOffset = rng.NextFloat2Direction() *
                                       rng.NextFloat(0f, targetRoomMaxRadius * 0.3f);

            // v4.1: Surface position Y clamped to volume top
            // If terrain is above volume, place entrance at volume top face
            float entranceSurfaceY = math.min(terrainHeight, volumeTopY - 0.5f);

            float3 surfacePos = new float3(
                targetRoomPos.x + horizontalOffset.x,
                entranceSurfaceY,
                targetRoomPos.z + horizontalOffset.y);

            // Clamp XZ to volume bounds
            float xzMargin = math.min(safeEdgeMargin + safeEntranceRadius, math.max(0f, safeHalfExtent - 0.5f));
            float minX = worldCenter.x - safeHalfExtent + xzMargin;
            float maxX = worldCenter.x + safeHalfExtent - xzMargin;
            float minZ = worldCenter.z - safeHalfExtent + xzMargin;
            float maxZ = worldCenter.z + safeHalfExtent - xzMargin;
            if (maxX < minX)
                minX = maxX = worldCenter.x;
            if (maxZ < minZ)
                minZ = maxZ = worldCenter.z;
            surfacePos.x = math.clamp(surfacePos.x, minX, maxX);
            surfacePos.z = math.clamp(surfacePos.z, minZ, maxZ);

            float3 inward = math.normalizesafe(
                targetRoomPos - surfacePos,
                new float3(0, -1, 0));

            float radius = safeEntranceRadius * rng.NextFloat(0.8f, 1.2f);
            float funnelLen = safeEntranceFunnelLength * rng.NextFloat(0.8f, 1.2f);

            float distToRoom = math.length(targetRoomPos - surfacePos);
            if (!math.isfinite(distToRoom))
                continue;

            funnelLen = math.min(funnelLen, distToRoom * 0.8f);
            funnelLen = math.max(funnelLen, radius * 2f);

            float innerRadius = radius * rng.NextFloat(0.4f, 0.7f);

            entrances[writtenCount] = new CaveEntrance
            {
                surfacePosition = surfacePos,
                inwardDirection = inward,
                radius          = radius,
                funnelLength    = funnelLen,
                innerRadius     = innerRadius
            };
            writtenCount++;
        }

        return writtenCount;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UTILITY FUNCTIONS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clamp a point to stay within the volume box with specified margin.
    /// </summary>
    static float3 ClampToVolume(float3 pos, float3 volumeMin, float3 volumeMax, float margin)
    {
        return math.clamp(pos, volumeMin + margin, volumeMax - margin);
    }

    static float ClampFinite(float value, float fallback, float minimum, float maximum)
    {
        float safeFallback = math.select(minimum, fallback, math.isfinite(fallback));
        float safeValue = math.select(safeFallback, value, math.isfinite(value));
        return math.clamp(safeValue, minimum, maximum);
    }

    static bool IsFinite(float3 value)
    {
        return math.all(math.isfinite(value));
    }

    /// <summary>
    /// Check if a candidate room position is too close to any existing room.
    /// "Too close" = centers closer than MIN_SEPARATION_FACTOR × combined max radii.
    /// </summary>
    static bool IsRoomTooClose(float3 candidatePos, float candidateRadius,
                                System.ReadOnlySpan<CaveNode> existingRooms)
    {
        for (int i = 0; i < existingRooms.Length; i++)
        {
            float existingMaxRadius = math.cmax(existingRooms[i].radii);
            float minDist = (candidateRadius + existingMaxRadius) * MIN_SEPARATION_FACTOR;
            float actualDist = math.length(candidatePos - existingRooms[i].position);

            if (actualDist < minDist)
                return true;
        }
        return false;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DEBUG / VALIDATION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Validates generated cave data. Returns true if all data is consistent.
    /// Use in development builds only (conditional on UNITY_EDITOR or DEBUG).
    /// </summary>
    public static bool Validate(
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances,
        float3 worldCenter,
        float volumeHalfExtent)
    {
        bool valid = true;

        // Check nodes are within volume
        float3 vMin = worldCenter - volumeHalfExtent;
        float3 vMax = worldCenter + volumeHalfExtent;

        for (int i = 0; i < nodes.Length; i++)
        {
            float3 p = nodes[i].position;
            if (math.any(p < vMin) || math.any(p > vMax))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[CaveGraph] Node {i} at {p} is outside volume bounds [{vMin}, {vMax}]");
#endif
                valid = false;
            }

            if (math.any(nodes[i].radii <= 0))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[CaveGraph] Node {i} has zero or negative radii: {nodes[i].radii}");
#endif
                valid = false;
            }

            if (nodes[i].blendRadius <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[CaveGraph] Node {i} has zero or negative blendRadius: {nodes[i].blendRadius}");
#endif
                valid = false;
            }
        }

        // Check tunnels reference valid positions
        for (int i = 0; i < tunnels.Length; i++)
        {
            if (tunnels[i].radiusA <= 0 || tunnels[i].radiusB <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[CaveGraph] Tunnel {i} has zero or negative radius");
#endif
                valid = false;
            }

            float tunnelLen = math.length(tunnels[i].pointB - tunnels[i].pointA);
            if (tunnelLen < 0.1f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[CaveGraph] Tunnel " + i + " is degenerate (length = " + tunnelLen.ToString("F2", CultureInfo.InvariantCulture) + ")");
#endif
                valid = false;
            }
        }


        // Check connectivity
        if (nodes.Length > 0)
        {
            int nodeCount = nodes.Length;
            bool[,] adjacencyMatrix = new bool[nodeCount, nodeCount];

            for (int i = 0; i < tunnels.Length; i++)
            {
                float3 pointA = tunnels[i].pointA;
                float3 pointB = tunnels[i].pointB;

                int indexA = -1;
                int indexB = -1;

                for (int j = 0; j < nodeCount; j++)
                {
                    if (math.all(nodes[j].position == pointA)) indexA = j;
                    if (math.all(nodes[j].position == pointB)) indexB = j;
                }

                if (indexA != -1 && indexB != -1)
                {
                    adjacencyMatrix[indexA, indexB] = true;
                    adjacencyMatrix[indexB, indexA] = true;
                }
            }

            if (!Hecton8.PureLogic.Systems.CaveGraphConnectivityChecker.Check(nodeCount, adjacencyMatrix, out int[] disconnectedNodes))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning($"[CaveGraph] Graph is not fully connected. Isolated nodes: {disconnectedNodes.Length}");
#endif
                valid = false;
            }
        }

        // Check entrances
        for (int i = 0; i < entrances.Length; i++)
        {
            float dirLen = math.length(entrances[i].inwardDirection);
            if (dirLen < 0.99f || dirLen > 1.01f)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[CaveGraph] Entrance " + i + " inwardDirection is not normalized (length = " + dirLen.ToString("F3", CultureInfo.InvariantCulture) + ")");
#endif
                valid = false;
            }

            if (entrances[i].radius <= 0 || entrances[i].funnelLength <= 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    $"[CaveGraph] Entrance {i} has zero or negative radius/funnelLength");
#endif
                valid = false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (valid)
        {
            Hecton8.Core.H8Debug.Log(
                $"[CaveGraph] Validation PASSED: {nodes.Length} rooms, " +
                $"{tunnels.Length} tunnels, {entrances.Length} entrances");
        }
#endif

        return valid;
    }

    /// <summary>
    /// Returns a human-readable summary of the generated cave.
    /// Useful for debug UI and logging.
    /// </summary>
    public static string GetSummary(
        NativeArray<CaveNode> nodes,
        NativeArray<CaveTunnel> tunnels,
        NativeArray<CaveEntrance> entrances)
    {
        if (!nodes.IsCreated || nodes.Length == 0)
            return "[CaveGraph] Empty (no nodes)";

        // Count room types
        int spheres = 0, ellipsoids = 0, shafts = 0, halls = 0, crevices = 0;
        float minY = float.MaxValue, maxY = float.MinValue;
        float maxRadius = 0f;

        for (int i = 0; i < nodes.Length; i++)
        {
            switch (nodes[i].roomType)
            {
                case CaveRoomType.Sphere:        spheres++;   break;
                case CaveRoomType.Ellipsoid:     ellipsoids++; break;
                case CaveRoomType.VerticalShaft: shafts++;    break;
                case CaveRoomType.FlatHall:      halls++;     break;
                case CaveRoomType.Crevice:       crevices++;  break;
            }
            minY = math.min(minY, nodes[i].position.y);
            maxY = math.max(maxY, nodes[i].position.y);
            maxRadius = math.max(maxRadius, math.cmax(nodes[i].radii));
        }

        // Count tunnel types
        int roundT = 0, tallT = 0, wideT = 0;
        for (int i = 0; i < tunnels.Length; i++)
        {
            switch (tunnels[i].tunnelType)
            {
                case CaveTunnelType.Round: roundT++; break;
                case CaveTunnelType.Tall:  tallT++;  break;
                case CaveTunnelType.Wide:  wideT++;  break;
            }
        }

        float depth = maxY - minY;

        return "[CaveGraph] " + nodes.Length + " rooms " +
               "(S:" + spheres + " E:" + ellipsoids + " V:" + shafts + " H:" + halls + " C:" + crevices + ") | " +
               tunnels.Length + " tunnels (R:" + roundT + " T:" + tallT + " W:" + wideT + ") | " +
               entrances.Length + " entrances | " +
               "Depth span: " + depth.ToString("F0", CultureInfo.InvariantCulture) +
               "m | Max radius: " + maxRadius.ToString("F1", CultureInfo.InvariantCulture) + "m";
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PHASE 4: INTERIOR STRUCTURES GENERATION
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates interior structures (stalactites, boulders, columns, etc.)
    /// to add visual interest and readability cues to cave interiors.
    ///
    /// Algorithm:
    /// 1. For each room, place structures based on density and allowed types.
    /// 2. Stalactites/Stalagmites placed near ceiling/floor.
    /// 3. Boulders placed on floors.
    /// 4. Columns span floor to ceiling.
    /// 5. Structures avoid overlapping entrances and tunnels.
    /// </summary>
    static int GenerateStructures(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        System.ReadOnlySpan<CaveNode> rooms,
        System.ReadOnlySpan<CaveTunnel> tunnels,
        System.Span<CaveStructure> structures)
    {
        if (preset.allowedStructureTypes == null || preset.allowedStructureTypes.Length == 0 || rooms.Length == 0)
            return 0;

        int targetCount = (int)(preset.maxStructures * preset.structureDensity);
        targetCount = math.clamp(targetCount, 0, math.min(preset.maxStructures, structures.Length));

        int structureCount = 0;
        for (int i = 0; i < targetCount; i++)
        {
            int roomIdx = rng.NextInt(0, rooms.Length);
            CaveNode room = rooms[roomIdx];
            CaveStructureType type = preset.allowedStructureTypes[
                rng.NextInt(0, preset.allowedStructureTypes.Length)];

            CaveStructure structure = CreateStructure(ref rng, type, room, worldCenter, volumeHalfExtent);
            if (!IsStructureBlocked(structure, tunnels))
            {
                if (structureCount >= structures.Length)
                    break;

                structures[structureCount] = structure;
                structureCount++;
            }
        }

        if (preset.presetType == CavePresetType.Abyss || preset.presetType == CavePresetType.Mega)
            AddAbyssalArchways(ref rng, preset, worldCenter, terrainHeight, volumeHalfExtent, rooms, tunnels, structures, ref structureCount);

        return structureCount;
    }

    /// <summary>
    /// Creates a single structure of the specified type within the given room.
    /// </summary>
    static CaveStructure CreateStructure(
        ref Random rng,
        CaveStructureType type,
        CaveNode room,
        float3 worldCenter,
        float volumeHalfExtent)
    {
        float3 roomCenter = room.position;
        float3 roomRadii = room.radii;

        CaveStructure s = new CaveStructure
        {
            blendRadius = 2f,
            noiseAmount = rng.NextFloat(0f, 0.3f),
            structureType = type
        };

        switch (type)
        {
            case CaveStructureType.Stalactite:
                // Hanging from ceiling
                float ceilingY = roomCenter.y + roomRadii.y * 0.8f;
                float3 basePos = roomCenter + rng.NextFloat3(-roomRadii * 0.6f, roomRadii * 0.6f);
                basePos.y = ceilingY;
                s.position = basePos;
                s.size = new float3(
                    rng.NextFloat(0.5f, 1.5f),  // base radius
                    rng.NextFloat(2f, 5f),      // height
                    rng.NextFloat(0.1f, 0.5f)); // tip radius
                break;

            case CaveStructureType.Stalagmite:
                // Growing from floor
                float floorY = roomCenter.y - roomRadii.y * 0.8f;
                float3 basePos2 = roomCenter + rng.NextFloat3(-roomRadii * 0.6f, roomRadii * 0.6f);
                basePos2.y = floorY;
                s.position = basePos2;
                s.size = new float3(
                    rng.NextFloat(0.5f, 1.5f),  // base radius
                    rng.NextFloat(2f, 5f),      // height
                    rng.NextFloat(0.1f, 0.5f)); // tip radius
                break;

            case CaveStructureType.Boulder:
                // Resting on floor
                float floorY2 = roomCenter.y - roomRadii.y * 0.9f;
                float3 boulderPos = roomCenter + rng.NextFloat3(-roomRadii * 0.7f, roomRadii * 0.7f);
                boulderPos.y = floorY2;
                s.position = boulderPos;
                s.size = new float3(rng.NextFloat(1f, 3f), 0, 0); // radius
                break;

            case CaveStructureType.Column:
                // Vertical pillar from floor to ceiling
                float3 columnBase = roomCenter + rng.NextFloat3(-roomRadii * 0.5f, roomRadii * 0.5f);
                columnBase.y = roomCenter.y - roomRadii.y * 0.9f;
                s.position = columnBase;
                float height = roomRadii.y * 1.8f;
                s.size = new float3(
                    rng.NextFloat(0.3f, 0.8f),  // radius
                    height,                     // height
                    rng.NextFloat(0.05f, 0.2f)); // taper
                break;

            case CaveStructureType.Bridge:
                // Horizontal span between walls
                float3 bridgeStart = roomCenter + rng.NextFloat3(-roomRadii * 0.7f, roomRadii * 0.7f);
                float3 bridgeEnd = bridgeStart + rng.NextFloat3(-roomRadii * 0.5f, roomRadii * 0.5f);
                bridgeEnd.y = bridgeStart.y; // keep horizontal
                s.position = bridgeStart;
                s.pointB = bridgeEnd;
                s.size = new float3(rng.NextFloat(0.2f, 0.5f), 0, 0); // radius
                break;

            case CaveStructureType.Arch:
            {
                float span = rng.NextFloat(roomRadii.x * 0.9f, roomRadii.x * 1.6f);
                float rise = rng.NextFloat(roomRadii.y * 0.45f, roomRadii.y * 0.9f);
                float tubeRadius = rng.NextFloat(0.5f, 1.25f);
                float3 archDirection = rng.NextFloat3(new float3(-1f, 0f, -1f), new float3(1f, 0f, 1f));
                archDirection = math.normalizesafe(new float3(archDirection.x, 0f, archDirection.z), new float3(1f, 0f, 0f));
                float3 archCenter = roomCenter + rng.NextFloat3(-roomRadii * 0.4f, roomRadii * 0.4f);
                archCenter.y = roomCenter.y - roomRadii.y * 0.65f;
                s.position = archCenter - archDirection * (span * 0.5f);
                s.pointB = archCenter + archDirection * (span * 0.5f);
                s.size = new float3(span * 0.5f, rise, tubeRadius);
                s.blendRadius = 3f;
                s.noiseAmount = rng.NextFloat(0.15f, 0.4f);
                break;
            }

            case CaveStructureType.Block:
                // Rectangular ruin block
                float3 blockPos = roomCenter + rng.NextFloat3(-roomRadii * 0.8f, roomRadii * 0.8f);
                blockPos.y = roomCenter.y - roomRadii.y * 0.85f; // on floor
                s.position = blockPos;
                s.size = new float3(
                    rng.NextFloat(1f, 3f),     // width
                    rng.NextFloat(0.5f, 2f),   // height
                    rng.NextFloat(1f, 3f));    // depth
                break;

            case CaveStructureType.Wall:
                // Partial wall or ledge
                float3 wallPos = roomCenter + rng.NextFloat3(-roomRadii * 0.7f, roomRadii * 0.7f);
                s.position = wallPos;
                s.size = new float3(
                    rng.NextFloat(2f, 5f),     // width
                    rng.NextFloat(1f, 3f),     // height
                    rng.NextFloat(0.2f, 0.5f)); // thickness
                break;
        }

        // Clamp to volume bounds
        float3 vMin = worldCenter - volumeHalfExtent + 2f;
        float3 vMax = worldCenter + volumeHalfExtent - 2f;
        s.position = math.clamp(s.position, vMin, vMax);
        s.pointB = math.clamp(s.pointB, vMin, vMax);

        return s;
    }

    static void AddAbyssalArchways(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        System.ReadOnlySpan<CaveNode> rooms,
        System.ReadOnlySpan<CaveTunnel> tunnels,
        System.Span<CaveStructure> structures,
        ref int structureCount)
    {
        if (structureCount >= structures.Length ||
            structureCount >= preset.maxStructures ||
            rooms.Length == 0)
        {
            return;
        }

        int deepestRoomIndex = 0;
        float deepestY = rooms[0].position.y;
        for (int i = 1; i < rooms.Length; i++)
        {
            if (rooms[i].position.y < deepestY)
            {
                deepestY = rooms[i].position.y;
                deepestRoomIndex = i;
            }
        }

        CaveNode anchorRoom = rooms[deepestRoomIndex];
        int archCount = math.min(
            math.min(preset.maxStructures, structures.Length) - structureCount,
            preset.presetType == CavePresetType.Abyss ? 2 : 1);
        float3 volumeMin = worldCenter - volumeHalfExtent + 4f;
        float3 volumeMax = worldCenter + volumeHalfExtent - 4f;

        for (int i = 0; i < archCount; i++)
        {
            float maxSupportedSpan = math.max(48f, volumeHalfExtent * 1.55f);
            float maxSupportedRise = math.max(48f, volumeHalfExtent * 1.45f);
            float span = math.min(rng.NextFloat(100f, 200f), maxSupportedSpan);
            float rise = math.min(rng.NextFloat(90f, 180f), maxSupportedRise);
            float tubeRadius = math.min(rng.NextFloat(6f, 14f), math.max(4f, volumeHalfExtent * 0.12f));
            float3 direction = rng.NextFloat3(new float3(-1f, 0f, -1f), new float3(1f, 0f, 1f));
            direction = math.normalizesafe(new float3(direction.x, 0f, direction.z), new float3(1f, 0f, 0f));

            float3 center = anchorRoom.position + rng.NextFloat3(
                new float3(-volumeHalfExtent * 0.14f, 0f, -volumeHalfExtent * 0.14f),
                new float3(volumeHalfExtent * 0.14f, 0f, volumeHalfExtent * 0.14f));
            center.y = math.clamp(terrainHeight - rng.NextFloat(1.5f, 4.5f), volumeMin.y + 3f, volumeMax.y - rise * 0.82f);

            CaveStructure arch = new CaveStructure
            {
                structureType = CaveStructureType.Arch,
                position = math.clamp(center - direction * (span * 0.5f), volumeMin, volumeMax),
                pointB = math.clamp(center + direction * (span * 0.5f), volumeMin, volumeMax),
                size = new float3(span * 0.5f, rise, tubeRadius),
                blendRadius = 6f,
                noiseAmount = rng.NextFloat(0.35f, 0.7f)
            };

            if (!IsStructureBlocked(arch, tunnels))
            {
                if (structureCount >= structures.Length)
                    return;

                structures[structureCount] = arch;
                structureCount++;
            }
        }
    }

    /// <summary>
    /// Checks if a structure would be blocked by tunnels or entrances.
    /// Returns true if the structure should be skipped.
    /// </summary>
    static bool IsStructureBlocked(CaveStructure structure, System.ReadOnlySpan<CaveTunnel> tunnels)
    {
        // Simple check: if structure position is too close to any tunnel
        for (int i = 0; i < tunnels.Length; i++)
        {
            CaveTunnel tunnel = tunnels[i];
            float distToTunnel = DistanceToTunnel(structure.position, tunnel);
            if (distToTunnel < tunnel.radiusA * 1.5f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Approximate distance from point to tunnel capsule.
    /// </summary>
    static float DistanceToTunnel(float3 point, CaveTunnel tunnel)
    {
        float3 a = tunnel.pointA;
        float3 b = tunnel.pointB;
        float3 ab = b - a;
        float abLen = math.length(ab);

        if (abLen < 0.001f)
            return math.length(point - a) - tunnel.radiusA;

        float3 dir = ab / abLen;
        float t = math.dot(point - a, dir);
        t = math.clamp(t, 0, abLen);

        float3 closest = a + dir * t;
        float distToAxis = math.length(point - closest);

        float radiusAtT = math.lerp(tunnel.radiusA, tunnel.radiusB, t / abLen);
        return distToAxis - radiusAtT;
    }
}
