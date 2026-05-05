# Migratory Flora System
Date: `2026-05-04`

Status: REFERENCE
Verification: PENDING VERIFICATION

## 2026-05-04 Current-State Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this system map as current runtime truth.
- This document is a flora/scatter architecture reference, not proof that migratory islands, spatial hash records, GPUI, or fauna queries are live in current scenes.
- Re-open `WorldProceduralScatterDirector*`, `FloraRegrowthDirector`, and current scatter assets before surgery.

## Runtime Owners

`WorldProceduralScatterDirectorMigratorySargassum` owns drifting Sargassum islands as data-only records. It does not spawn runtime flora GameObjects. Islands are selected from deterministic canopy-kelp scatter placements, lifted into the water column, and registered into `HectonSpatialHash` as signal volumes so fauna systems can query the moving canopy.

`FloraRegrowthDirector` consumes the island shade query during SlowTick maturation scans. Seabed flora under a migratory canopy receives external shade occlusion and the Burst maturation job returns negative growth, which routes through `DestructibleOrganicManager.TryApplyLightStarvation` until the instance enters decomposition/tombstone state.

## AUP Drift Logic

Each island stores `AbsoluteUniversePosition` directly:

```text
AUP = (GridX, GridY, GridZ, LocalX, LocalY, LocalZ)
absolute = grid * 5000m + local
```

The Burst drift job receives an AbyssalFlow sample for each active island. Vertical flow is discarded because migratory islands are canopy-drift masses, not buoyancy simulation bodies.

```text
flow.xz = AbyssalFlow(runtimeIslandPosition).xz
desiredSpeed = min(MaxSpeed, length(flow.xz) * DriftScale)
desiredVelocity = normalize(flow.xz) * desiredSpeed
velocity = lerp(velocity, desiredVelocity, saturate(dt * VelocityDamping))
local += velocity * dt
if local component crosses [0, 5000), shift Grid component and re-normalize local
```

The spatial hash is updated after the job completes. Stale handles are unregistered during source reconciliation; active handles are updated in place to keep boid/fish followers bound to the moving island volume.

## Photosynthetic Kill-Zones

Spawn rejection and maturation both use the same vertical canopy test:

```text
verticalDelta = islandY - seabedY
reject if verticalDelta <= 0
planarSq = (islandX - seabedX)^2 + (islandZ - seabedZ)^2
reject if planarSq > radius^2
occlusion = (1 - planarSq / radius^2) * saturate(verticalDelta / MinimumWaterDepth)
```

Spawn candidates in ground/cluster flora layers are rejected under shade. Existing seabed flora receives negative growth in `EvaluateMaturationJob`, preserving the delta-save protocol through the existing decomposition/tombstone path.

## Symbiotic Fungal MST

Fungal nodes are bounded to 128 active entries and resolved from authored flora HashIDs:

```text
Fungal Stalk = 0xFD5A46CC
Acid Shroom  = 0xB796CF49
Blindcap     = 0x1FB3740A
```

Fertilizer calls schedule `BuildSymbioticFungalMstJob`. The job runs Prim's algorithm over squared distances only:

```text
best[root] = 0
for each step:
    current = unvisited node with lowest best distance
    stop if current is disconnected or best > 1000m^2 edge cap
    mark current connected
    for every unvisited candidate:
        d2 = distancesq(current.position, candidate.position)
        if d2 <= maxEdgeDistanceSq and d2 < best[candidate]:
            best[candidate] = d2
            parent[candidate] = current
```

Connected results upsert growth buffs into a persistent native buff list. `EvaluateMaturationJob` reads that list and multiplies age progress before the 0.1 to 1.0 smoothstep maturation curve.

## Proxy Alignment

`WorldProceduralProxySceneBuilder` snaps editor proxies with `RaycastCommand.ScheduleBatch` using the same downward ray primitive as runtime scatter snapping. Rotation aligns proxy up to the hit normal, clamped to 35 degrees so tall flora proxies do not lie sideways on steep slopes.

## Constraints

- No runtime micro-flora GameObjects.
- Migratory state uses AUP grid/local data, not accumulated float world coordinates.
- Scatter acceptance rejects flora above 4096 instances per stream cell.
- No managed allocations are introduced inside Burst jobs.
- Verification remains `PENDING VERIFICATION` until Unity compile and console logs are supplied.
