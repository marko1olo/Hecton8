# Migratory Flora System
Date: 2026-05-07

Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not migratory flora runtime, scatter scene wiring, fauna query correctness, profiler, or player-build proof.

- `Assets/_Project/Scripts/AI/Ecology/Migration/MacroSwarm.cs`
- `Assets/_Project/Scripts/Ecosystem/MigrationDirector.cs`
- `Assets/_Project/Scripts/Ecosystem/EcosystemMigrationProfile.cs`
- `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs`

Verification: PENDING VERIFICATION

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current static/tool boundary is R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction); R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers; AtlasCheck fails `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); Mod API static validation passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

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


