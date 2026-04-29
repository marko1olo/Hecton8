# PERSISTENCE AND SCENE-SEARCH DRIFT

Date: 2026-04-30
Status: PENDING VERIFICATION
Scope: current source-backed audit of runtime persistence surfaces outside the save-slot stack and runtime scene-search fallback paths outside bootstrap/editor
Mandates followed: `STRM_Persistent_Object_Registry.txt`, `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`

## Purpose

The active docset already had save/load truth for the slot pipeline.

What it did not state clearly was that the project still has other runtime persistence surfaces and a few non-bootstrap runtime scene-search fallbacks living outside that slot pipeline.

This file exists to document those facts precisely.

## Method

Evidence in this pass came from:

1. source grep for file-system APIs outside editor code
2. source grep for `FindAnyObjectByType`, `FindObjectsByType`, and related scene scans outside editor code
3. line-level reread of the strongest candidates before writing findings

This pass does not claim these paths are all broken.
It proves they exist and describes what they currently do.

## Findings

| ID | File | Current issue | Severity | Evidence |
|---|---|---|---|---|
| PSD-01 | `Assets/_Project/Scripts/Input/RebindingManager.cs` | input-binding persistence is its own file-backed JSON pipeline outside `SaveManager` and outside the slot artifact stack | MEDIUM | direct `File.ReadAllText`, `File.WriteAllText`, `File.Delete`, `File.Move`, and `Path.Combine(Application.persistentDataPath, ...)` at `:349-503` |
| PSD-02 | `Assets/_Project/Scripts/Meta/GlobalProfileManager.cs` | meta profile persistence is its own JSON file pipeline outside `SaveManager` and outside the slot artifact stack | MEDIUM | direct `JsonUtility.ToJson`, `File.WriteAllText`, `File.Delete`, `File.Move`, `File.ReadAllText`, and `Path.Combine(Application.persistentDataPath, ProfileDirectoryName, ...)` at `:792-869` |
| PSD-03 | `Assets/_Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs` | tick-driven cave AO controller still falls back to runtime scene scans for camera and voxel volumes when authored/runtime references are missing | HIGH | `SlowTick()` calls `TryResolveViewerReferences()` and `RefreshVolumeCache()` at `:112-117`; fallback `FindAnyObjectByType<Camera>` at `:172-175`; fallback `FindObjectsByType<HectonVoxelVolume>` allocates at `:201-215` |

## Detailed Evidence

### PSD-01 — RebindingManager Is A Separate Persistence Stack

`RebindingManager` does not route through `SaveManager`.
It owns its own persistence lane for binding overrides.

Current file-backed behavior:

- reads `controls.json` directly from `Application.persistentDataPath`
- writes a temp file first
- deletes existing primary file
- moves temp file into place
- still knows how to migrate from a legacy PlayerPrefs payload

This is not a random unsafe write path.
It is a coherent mini-persistence stack.

The real issue is architectural breadth:

- save/load truth in the project is not only `SaveManager`
- runtime persistence is split at least across save slots, input overrides, and meta profile

### PSD-02 — GlobalProfileManager Is Another Separate Persistence Stack

`GlobalProfileManager` also bypasses `SaveManager`.
It owns a global JSON profile stored under its own profile directory.

Current behavior:

- `WriteProfileToDisk(...)` serializes via `JsonUtility.ToJson(profile, true)`
- writes to temp path
- deletes existing target file
- moves temp file into place
- `LoadProfileFromDisk()` reads directly from disk and deserializes with `JsonUtility.FromJson<GlobalProfileData>(json)`

This looks deliberate rather than accidental.
But it still matters because current runtime persistence is fragmented:

- save-slot persistence
- input override persistence
- global meta profile persistence

Any doc that compresses all runtime persistence into `SaveManager` is incomplete.

### PSD-03 — Cave AO Still Has Runtime Scene-Search Fallbacks

`HectonCaveVoxelAmbientOcclusionController` is not an editor utility.
It is runtime code implementing:

- `ITickable`
- `IUpdatable`
- `ISlowTickable`

Its regular path tries to use:

- `WorldRuntimeReferenceUtility.TryResolvePlayerTransform(...)`
- `GlobalRegistry.Player`
- `WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(...)`

That part is coherent.

The problem is the fallback path that remains active when those references are missing:

- in `TryResolveViewerReferences()`, if no camera is resolved, every `ViewerFallbackRetryIntervalSeconds` it runs `Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Exclude)`
- in `RefreshVolumeCache()`, if no `WorldCaveDirector` is resolved, every `VolumeFallbackRefreshIntervalSeconds` it runs `Object.FindObjectsByType<HectonVoxelVolume>(FindObjectsInactive.Exclude)`

The second path explicitly allocates a `HectonVoxelVolume[]` array.

This is not a per-frame infinite scan.
It is still runtime scene-search debt in a slow-tick chain, outside bootstrap/editor.

## Current Reading

What looks good:

- `RebindingManager` and `GlobalProfileManager` both use temp-write then move semantics rather than blind overwrite
- cave AO runtime tries registry/runtime-reference utilities first before falling back to scans

What looks weak:

- runtime persistence is wider than the save-slot truth layer currently emphasizes
- those persistence lanes are not unified under one persistence authority
- cave AO runtime still carries explicit scene-search fallback debt
- fallback volume scan in cave AO allocates an array in live runtime code

## What This Pass Did Not Prove

- no claim that split persistence is inherently wrong for settings/meta data
- no proof of data corruption in `RebindingManager` or `GlobalProfileManager`
- no profiler evidence for the cave AO fallback cost on target hardware
- no proof that the fallback scene scans are being hit often in the current scene

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | cave AO fallback scans can add avoidable runtime search cost when primary references are missing |
| GC | `FindObjectsByType<HectonVoxelVolume>` in the cave AO fallback path allocates an array |
| Memory | split persistence stacks increase ownership surface and make artifact inventory broader |
| Cadence | persistence/debug/support systems become harder to reason about because state is not centralized in one runtime owner |
| Correctness | improved because the active truth layer now distinguishes slot persistence from other runtime persistence surfaces |

## Verdict

Current runtime persistence and scene-search truth is broader than the existing save doc alone suggests.

Confirmed source-backed facts:

1. `RebindingManager` owns a separate file-backed input override persistence stack
2. `GlobalProfileManager` owns a separate file-backed meta profile persistence stack
3. `HectonCaveVoxelAmbientOcclusionController` still contains runtime scene-search fallback debt in its slow-tick chain

These are not speculative claims.
They are current source-backed drift surfaces outside the main save-slot pipeline.

STATUS: PENDING VERIFICATION
