# Migratory Flora System

Date: 2026-05-07

Status: PENDING VERIFICATION

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not migratory flora runtime, scatter scene wiring, fauna query correctness, profiler, or player-build proof.

- `Assets/_Project/Scripts/AI/Ecology/Migration/MacroSwarm.cs`

- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`

- `Assets/_Project/Scripts/Ecosystem/EcosystemMigrationProfile.cs`

- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs`

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## Historical 2026-05-04 Boundary

- Evidence limit: flora/scatter reference only; migratory islands, spatial hash records, GPUI, and fauna queries remain scene-unproven.

- Re-open `WorldProceduralScatterDirector*`, `FloraRegrowthDirector`, and current scatter assets before surgery.

## Runtime Owners

`WorldProceduralScatterDirectorMigratorySargassum` owns drifting Sargassum islands as data-only records.

It does not spawn runtime flora GameObjects. Islands are selected from deterministic canopy-kelp scatter placements, lifted, and registered into `HectonSpatialHash` as signal volumes.

`FloraRegrowthDirector` consumes island shade query during SlowTick maturation scans.

Seabed flora under migratory canopy receives external shade occlusion. Burst maturation returns negative growth, routed through `DestructibleOrganicManager.TryApplyLightStarvation` until decomposition/tombstone.

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

Spatial hash updates after job completion. Source reconciliation unregisters stale handles and updates active handles, keeping boid/fish followers bound.

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

`WorldProceduralProxySceneBuilder` snaps editor proxies with `RaycastCommand.ScheduleBatch`.

It uses the same downward ray primitive as runtime scatter snapping. Rotation aligns proxy up to hit normal, clamped to `35` degrees.

## Constraints

- No runtime micro-flora GameObjects.

- Migratory state uses AUP grid/local data, not accumulated float world coordinates.

- Scatter acceptance rejects flora above 4096 instances per stream cell.

- No managed allocations are introduced inside Burst jobs.

- Verification remains `PENDING VERIFICATION` until Unity compile and console logs are supplied.
