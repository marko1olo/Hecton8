# Critical Action Queue

Status: PENDING VERIFICATION

## Priority 0

1. Restore trustworthy editor-state observability.
Reason: Right now console truth, log truth, MCP readiness, and editor responsiveness do not form one reliable verification surface.

2. Freeze authority drift.
Reason: Choose the runtime sovereign path between registry/dispatcher/bootstrap versus legacy singleton residue. Stop adding more mixed ownership.

3. Clean `02_HECTON_WORLD` as a truth surface.
Reason: Separate production runtime roots from debug, preview, trial, and temporary residue.

4. Audit the spatial-hash handle contract.
Reason: `HectonSpatialHash` currently uses monotonic `_nextHandle++` without visible allocator-boundary guard or reuse. Even if it is not the proven root of `SetResource` spam yet, it is too weak to ignore.

## Priority 1

1. Audit every `.Complete()` in hot or cadence-sensitive owners.
Reason: The project uses Jobs/Burst for real work, then repeatedly pays back the benefit with barriers.

2. Decompose the worst monoliths.
Primary suspects:
- `HectonMapMagicVegetationBridge`
- `HectonPlayerMovement`
- `WorldProceduralScatterDirector`
- `FaunaDirector`
- `HectonVoxelEngine`

3. Produce a hard migration map for `Instance` and `DontDestroyOnLoad` owners.
Reason: `GlobalRegistry` cannot become authoritative while parallel sovereignty remains normal.

## Priority 2

1. Reclassify every major subsystem as:
- production
- transitional
- experimental
- dead seam

2. Mark DOTS honestly.
Reason: It is currently a seam, not a strength.

3. Build a real first-party regression harness.
Minimum targets:
- bootstrap
- save/load
- HUD
- player inventory
- world scatter
- scene transitions

## Priority 3

1. Reverify major docs against current code and editor truth.
Reason: documentation volume is high, but drift has already happened.

2. Add explicit “reality status” headers to key docs.
Suggested values:
- production
- partial
- stale
- target architecture only

3. Establish one audit scoreboard that is maintained from code/editor evidence, not aspiration.

## What Not To Do

- Do not claim DOTS is active production architecture.
- Do not keep adding managers into already overloaded roots.
- Do not treat large files as harmless if they still absorb more responsibility.
- Do not use docs as truth without code/editor cross-check.
- Do not call the project “close” while verification truth, test truth, and authority truth are still fragmented.
