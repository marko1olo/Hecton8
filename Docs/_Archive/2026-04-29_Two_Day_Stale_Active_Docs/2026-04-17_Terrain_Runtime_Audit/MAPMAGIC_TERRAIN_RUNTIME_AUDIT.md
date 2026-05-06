# MapMagic Terrain Runtime Audit
Date: 2026-04-29

Status: `PENDING VERIFICATION`

## Target

Audit and repair of `MapMagic 2` terrain runtime loading / fidelity path in `02_HECTON_WORLD`.

## Live Findings

- Active terrain owner in scene:
  - `--- WORLD ---/Terrain`
  - component: `MapMagic.Core.MapMagicObject`
- Live serialized `MapMagicObject` settings before this fix:
  - `mainRange = 1`
  - `DraftRange = 1`
  - `draftsInPlaymode = false`
  - `draftResolution = 33`
  - `tileResolution = 513`
  - `hideFarTerrains = true`
  - `terrainSettings.pixelError = 10`
  - `terrainSettings.baseMapDist = 1000`
  - `terrainSettings.detailDistance = 80`
  - `terrainSettings.heightmapMaximumLOD = 0`
- Scene composition readback:
  - `Main Terrain` objects found: `5`
  - `Draft Terrain` objects found: `227`

## Conclusion

The project did not have a real first-party runtime terrain-fidelity controller.

What existed:
- `WorldStreamingDirector` tuned `MapMagicObject.globals.objectsNumPerFrame`
- `WorldSliceDirector` tuned slice distance scales
- `LODSystemManager` handled `LODGroup`

What did **not** exist:
- a runtime owner that promoted Unity Terrain visual fidelity near the player
- a runtime owner that enforced a draft-to-main terrain continuum
- a runtime owner that re-applied `pixelError`, `baseMapDist`, or `detailDistance` to existing terrain tiles

The old validator logic also encoded the wrong assumption:
- `draftsInPlaymode = true` was treated as a warning

For this project and this observed symptom, that assumption was backwards.

## Root Problem

The deeper defect was not just weak quality settings.

Live tile inspection showed:
- scene/runtime was dominated by pinned `preview = true` tiles
- many player-facing tiles existed as `draft` only
- near-player tiles could still read `main = null`, `draft != null`, `ActiveTerrain = Draft Terrain`

That means MapMagic had nothing to switch to for full-detail terrain near the player. A draft-only pinned tile cannot become sharp just because `pixelError` changed.

## Code Changes

### `Assets/_Project/Scripts/MapMagicBridge.cs`

Added runtime control API:
- `SetRuntimeObjectsPerFrame(int)`
- `ConfigureRuntimeTerrainStreaming(bool, int, int, MapMagicObject.Resolution)`
- `ApplyRuntimeTerrainQuality(int, int, float, float, int)`
- `MaintainRuntimeTerrainDetailLevels(int, int, int, int, int, int, float, float, int)`

Added internal cold-path helpers:
- `ApplyTerrainSettingsToCachedTerrains()`
- `RefreshTerrainTilesForStreaming(int, bool)`
- `CreateRuntimeDetailLevel(TerrainTile, bool)`
- `ReleaseMainDetailLevel(TerrainTile)`
- `ApplyPerTileTerrainQuality(...)`

Purpose:
- keep MapMagic-specific writes inside the bridge
- allow first-party systems to drive terrain fidelity without direct ad-hoc plugin writes
- restore missing `main` detail levels around the player for draft-only pinned tiles
- tear down far `main` detail levels so terrain residency does not grow forever

### `Assets/_Project/Scripts/WorldStreamingDirector.cs`

Extended streaming policy to own terrain runtime fidelity:
- terrain playmode drafts toggle
- terrain draft resolution
- terrain main/draft ring sizes
- terrain main teardown radius
- explicit near-main and far-draft quality split
- terrain per-profile quality:
  - `terrainPixelError`
  - `terrainBaseMapDistance`
  - `terrainDetailDistance`
  - `terrainDetailDensity`

The director now:
- configures runtime draft/main terrain topology through `MapMagicBridge`
- applies terrain quality profiles through `MapMagicBridge`
- on every `SlowTick`, computes terrain detail ownership from player-driven runtime state
- no longer treats terrain only as object-budget throughput

### `Assets/_Project/Scripts/Editor/MapMagicWorldValidator.cs`

Validation logic corrected:
- warns when `draftsInPlaymode` is disabled
- warns when `draftResolution < 65`
- warns when `DraftRange < mainRange`

## Intended Runtime Policy After Fix

- `draftsInPlaymode = true`
- `draftResolution = 65`
- `mainRange = 2`
- `DraftRange = 2`
- `mainTeardownRange = 3`
- near main terrain:
  - `pixelError = 2`
  - `baseMapDistance = 1000`
- far draft terrain:
  - `pixelError = 6`
  - `baseMapDistance = 384`

This creates:
- main terrain near the player
- a real draft ring beyond it
- teardown of far main terrain to cap memory growth
- runtime terrain-detail control instead of editor-preview luck

## Risks

WARNING: Regression risk in terrain generation cadence and CPU cost.

Why:
- promoting draft-only pinned tiles to live main tiles adds real generation work in runtime
- enabling playmode drafts increases terrain residency and generation work
- raising draft resolution from `33` to `65` increases cost for the draft ring
- in-range tile refresh after topology change can spike generation on startup

## Verification Required

Must be confirmed in real logs / Play Mode:
- `MapMagicWorldValidation` no longer treats runtime drafts as invalid
- tiles inside the player ring report `main != null`
- tiles outside teardown radius release `main` back to draft-only
- terrain near player sharpens consistently outside preview-authored tiles
- draft-to-main transitions occur as the player crosses tile boundaries
- no catastrophic spike/regression in terrain generation during startup traversal
- no new log spam from MapMagic terrain rebuilds

## Current Blocking Reality

Live MCP verification is still partially blocked by unstable Unity session behavior during Play Mode transitions. That means the new terrain streaming logic is coded and documented, but still `PENDING VERIFICATION` until a stable Play Mode capture proves near tiles promote to `main` and far tiles tear down correctly.

## 2026-04-17 Follow-Up Verification Attempt

### Unity-Verified Facts

- `02_HECTON_WORLD` is loaded and active.
- Live Console still reports:
  - `[MapMagicBridge] Disabled Terrain auto-connect at runtime because draft/main tiles use different heightmap resolutions. This prevents Unity neighbor-connect errors while runtime draft streaming is active.`
- Scene readback still confirms the terrain owner is:
  - `--- WORLD ---/Terrain`
  - component: `MapMagic.Core.MapMagicObject`
- Scene search still confirms `227` objects with `TerrainTile`.

### What Was Attempted

- menu-driven missing-script cleanup and validation
- direct live tile-state inspection through Unity MCP `execute_code`

### Current Tooling Blocker

`execute_code` is still failing in this workspace with:

- `mono.exe: The filename or extension is too long`

That blocks the most direct live proof path for:

- player tile coordinate
- near-ring `main` count
- near-ring `draft` count
- far-ring main teardown count

### Additional Runtime Truth

This same session also showed:

- `WorldLODSceneBootstrap` registered `0 LODGroup` components for `02_HECTON_WORLD`

That is adjacent to the terrain-fidelity problem because the world is still not fully authored around the runtime streaming stack.

### Updated Conclusion

The runtime terrain streaming owner exists in code:

- `WorldStreamingDirector`
- `MapMagicBridge`

But final proof that tiles **actually** switch `draft -> main -> teardown` around player movement is still blocked by unstable Play Mode tooling and the broken `execute_code` path.

Status remains `PENDING VERIFICATION`.
