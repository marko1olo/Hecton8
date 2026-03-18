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

/// <summary>
/// Static procedural cave graph generator.
/// Converts (seed + preset + world context) into NativeArrays of SDF primitives.
///
/// Usage:
///   CaveGraphGenerator.Generate(
///       seed, preset, worldCenter, terrainHeight, volumeHalfExtent,
///       out nodes, out tunnels, out entrances, out structures,
///       Allocator.Persistent);
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

    // ════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate complete cave graph from seed and preset.
    ///
    /// All output NativeArrays are allocated with the specified allocator.
    /// Caller MUST dispose them after use.
    ///
    /// Must be called on MAIN THREAD (NativeArray allocation).
    /// </summary>
    /// <param name="seed">Deterministic seed. Same seed = same cave.</param>
    /// <param name="preset">Cave configuration (room counts, sizes, noise, etc.).</param>
    /// <param name="worldCenter">World-space center of the voxel volume.</param>
    /// <param name="terrainHeightAtCenter">Terrain surface Y at worldCenter.
    /// Used to ensure rooms stay below surface and entrances connect to surface.</param>
    /// <param name="volumeHalfExtent">Half-size of the volume cube in meters.
    /// = (gridDimension * voxelSize) / 2. Rooms are constrained within this box.</param>
    /// <param name="nodes">OUTPUT: Array of cave rooms.</param>
    /// <param name="tunnels">OUTPUT: Array of tunnels connecting rooms.</param>
    /// <param name="entrances">OUTPUT: Array of surface entrances.</param>
    /// <param name="structures">OUTPUT: Array of internal structures.
    /// Currently empty (Length = 0). Reserved for future column/bridge/stalactite generation.</param>
    /// <param name="allocator">NativeArray allocator. Use Persistent for async jobs.</param>
    public static void Generate(
        uint seed,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeightAtCenter,
        float volumeHalfExtent,
        out NativeArray<CaveNode> nodes,
        out NativeArray<CaveTunnel> tunnels,
        out NativeArray<CaveEntrance> entrances,
        out NativeArray<CaveStructure> structures,
        Allocator allocator)
    {
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
            UnityEngine.Debug.LogWarning(
                $"[CaveGraph] Terrain height ({terrainHeightAtCenter:F1}m) is above " +
                $"volume top ({volumeTopY:F1}m). Entrances may be clipped. " +
                $"Consider raising worldCenter.y or increasing gridDimension.");
        }

        // Phase 1
        var roomList = new NativeList<CaveNode>(roomCount, Allocator.Temp);
        PlaceRooms(ref rng, preset, worldCenter, terrainHeightAtCenter,
                   volumeHalfExtent, roomCount, dynamicMargin, ref roomList);

        int actualRoomCount = roomList.Length;

        // Phase 2
        var tunnelList = new NativeList<CaveTunnel>(actualRoomCount * 2, Allocator.Temp);
        GenerateTunnels(ref rng, preset, roomList, ref tunnelList);

        // Phase 3
        var entranceList = new NativeList<CaveEntrance>(preset.maxEntrances, Allocator.Temp);
        GenerateEntrances(ref rng, preset, worldCenter, terrainHeightAtCenter,
                          volumeHalfExtent, dynamicMargin, roomList, ref entranceList);

        // Phase 4: Copy to output
        nodes = new NativeArray<CaveNode>(roomList.Length, allocator);
        for (int i = 0; i < roomList.Length; i++)
            nodes[i] = roomList[i];

        tunnels = new NativeArray<CaveTunnel>(tunnelList.Length, allocator);
        for (int i = 0; i < tunnelList.Length; i++)
            tunnels[i] = tunnelList[i];

        entrances = new NativeArray<CaveEntrance>(entranceList.Length, allocator);
        for (int i = 0; i < entranceList.Length; i++)
            entrances[i] = entranceList[i];

        structures = new NativeArray<CaveStructure>(0, allocator);

        roomList.Dispose();
        tunnelList.Dispose();
        entranceList.Dispose();
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
    static void PlaceRooms(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        int targetCount,
        float edgeMargin,        // v4.1: dynamic margin
        ref NativeList<CaveNode> roomList)
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
        roomList.Add(firstRoom);

        int branchPointCount = 0;
        var branchIndices = new NativeArray<int>(MAX_ROOMS, Allocator.Temp);

        float3 currentPos = firstPos;
        int currentRoomIdx = 0;

        for (int i = 1; i < targetCount; i++)
        {
            // bool placed = false;

            for (int attempt = 0; attempt < PLACEMENT_ATTEMPTS; attempt++)
            {
                float3 originPos = currentPos;
                if (branchPointCount > 0 && rng.NextFloat() < 0.25f)
                {
                    int branchIdx = branchIndices[rng.NextInt(0, branchPointCount)];
                    originPos = roomList[branchIdx].position;
                }

                float3 dir = rng.NextFloat3Direction();
                dir.y = math.lerp(dir.y, -math.abs(dir.y), preset.verticalSpread);
                dir = math.normalizesafe(dir, new float3(0, -1, 0));

                float prevRadius = (roomList.Length > 0)
                    ? math.cmax(roomList[roomList.Length - 1].radii)
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

                if (IsRoomTooClose(candidatePos, nextRadiusEstimate, roomList))
                    continue;

                CaveNode room = CreateRoom(ref rng, preset, candidatePos);
                roomList.Add(room);

                currentPos = candidatePos;
                currentRoomIdx = roomList.Length - 1;

                if (rng.NextFloat() < 0.35f)
                {
                    if (branchPointCount < MAX_ROOMS)
                    {
                        branchIndices[branchPointCount] = currentRoomIdx;
                        branchPointCount++;
                    }
                }

                // placed = true;
                break;
            }
        }

        branchIndices.Dispose();
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
    static void GenerateTunnels(
        ref Random rng,
        CavePreset preset,
        NativeList<CaveNode> rooms,
        ref NativeList<CaveTunnel> tunnelList)
    {
        int roomCount = rooms.Length;
        if (roomCount < 2) return;

        // ── Phase 2a: Sequential connections (guaranteed connectivity) ──
        for (int i = 0; i < roomCount - 1; i++)
        {
            CaveTunnel tunnel = CreateTunnel(ref rng, preset, rooms[i], rooms[i + 1]);
            tunnelList.Add(tunnel);

            if (tunnelList.Length >= MAX_TUNNELS) return;
        }

        // ── Phase 2b: Extra connections (loops) ──
        // Only between rooms that are spatially close but not already sequentially connected
        for (int i = 0; i < roomCount; i++)
        {
            for (int j = i + 2; j < roomCount; j++)
            {
                if (tunnelList.Length >= MAX_TUNNELS) return;

                // Skip if rooms are too far apart
                float dist = math.length(rooms[i].position - rooms[j].position);
                float combinedRadii = math.cmax(rooms[i].radii) + math.cmax(rooms[j].radii);

                // Only connect rooms that are within ~3x their combined radii
                if (dist > combinedRadii * 3.5f) continue;

                // Probability check
                if (rng.NextFloat() >= preset.extraConnectionChance) continue;

                CaveTunnel extraTunnel = CreateTunnel(ref rng, preset, rooms[i], rooms[j]);
                tunnelList.Add(extraTunnel);
            }
        }
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
    static void GenerateEntrances(
        ref Random rng,
        CavePreset preset,
        float3 worldCenter,
        float terrainHeight,
        float volumeHalfExtent,
        float edgeMargin,         // v4.1
        NativeList<CaveNode> rooms,
        ref NativeList<CaveEntrance> entranceList)
    {
        if (rooms.Length == 0) return;

        int entranceCount = rng.NextInt(preset.minEntrances, preset.maxEntrances + 1);
        entranceCount = math.min(entranceCount, rooms.Length);
        entranceCount = math.min(entranceCount, MAX_ENTRANCES);

        // v4.1: Check that terrain surface is reachable from volume
        float volumeTopY = worldCenter.y + volumeHalfExtent;

        var usedRooms = new NativeArray<bool>(rooms.Length, Allocator.Temp);

        for (int e = 0; e < entranceCount; e++)
        {
            int bestRoom = -1;
            float bestScore = float.MaxValue;

            for (int r = 0; r < rooms.Length; r++)
            {
                if (usedRooms[r]) continue;

                float3 roomPos = rooms[r].position;
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

            usedRooms[bestRoom] = true;

            float3 targetRoomPos = rooms[bestRoom].position;
            float targetRoomMaxRadius = math.cmax(rooms[bestRoom].radii);

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
            float xzMargin = edgeMargin + preset.entranceRadius;
            surfacePos.x = math.clamp(surfacePos.x,
                worldCenter.x - volumeHalfExtent + xzMargin,
                worldCenter.x + volumeHalfExtent - xzMargin);
            surfacePos.z = math.clamp(surfacePos.z,
                worldCenter.z - volumeHalfExtent + xzMargin,
                worldCenter.z + volumeHalfExtent - xzMargin);

            float3 inward = math.normalizesafe(
                targetRoomPos - surfacePos,
                new float3(0, -1, 0));

            float radius = preset.entranceRadius * rng.NextFloat(0.8f, 1.2f);
            float funnelLen = preset.entranceFunnelLength * rng.NextFloat(0.8f, 1.2f);

            float distToRoom = math.length(targetRoomPos - surfacePos);
            funnelLen = math.min(funnelLen, distToRoom * 0.8f);
            funnelLen = math.max(funnelLen, radius * 2f);

            float innerRadius = radius * rng.NextFloat(0.4f, 0.7f);

            entranceList.Add(new CaveEntrance
            {
                surfacePosition = surfacePos,
                inwardDirection = inward,
                radius          = radius,
                funnelLength    = funnelLen,
                innerRadius     = innerRadius
            });
        }

        usedRooms.Dispose();
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

    /// <summary>
    /// Check if a candidate room position is too close to any existing room.
    /// "Too close" = centers closer than MIN_SEPARATION_FACTOR × combined max radii.
    /// </summary>
    static bool IsRoomTooClose(float3 candidatePos, float candidateRadius,
                                NativeList<CaveNode> existingRooms)
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
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Node {i} at {p} is outside volume bounds [{vMin}, {vMax}]");
                valid = false;
            }

            if (math.any(nodes[i].radii <= 0))
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Node {i} has zero or negative radii: {nodes[i].radii}");
                valid = false;
            }

            if (nodes[i].blendRadius <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Node {i} has zero or negative blendRadius: {nodes[i].blendRadius}");
                valid = false;
            }
        }

        // Check tunnels reference valid positions
        for (int i = 0; i < tunnels.Length; i++)
        {
            if (tunnels[i].radiusA <= 0 || tunnels[i].radiusB <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Tunnel {i} has zero or negative radius");
                valid = false;
            }

            float tunnelLen = math.length(tunnels[i].pointB - tunnels[i].pointA);
            if (tunnelLen < 0.1f)
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Tunnel {i} is degenerate (length = {tunnelLen:F2})");
                valid = false;
            }
        }

        // Check entrances
        for (int i = 0; i < entrances.Length; i++)
        {
            float dirLen = math.length(entrances[i].inwardDirection);
            if (dirLen < 0.99f || dirLen > 1.01f)
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Entrance {i} inwardDirection is not normalized (length = {dirLen:F3})");
                valid = false;
            }

            if (entrances[i].radius <= 0 || entrances[i].funnelLength <= 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[CaveGraph] Entrance {i} has zero or negative radius/funnelLength");
                valid = false;
            }
        }

        if (valid)
        {
            UnityEngine.Debug.Log(
                $"[CaveGraph] Validation PASSED: {nodes.Length} rooms, " +
                $"{tunnels.Length} tunnels, {entrances.Length} entrances");
        }

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

        return $"[CaveGraph] {nodes.Length} rooms " +
               $"(S:{spheres} E:{ellipsoids} V:{shafts} H:{halls} C:{crevices}) | " +
               $"{tunnels.Length} tunnels (R:{roundT} T:{tallT} W:{wideT}) | " +
               $"{entrances.Length} entrances | " +
               $"Depth span: {depth:F0}m | Max radius: {maxRadius:F1}m";
    }
}