# Reverification Addendum — 2026-04-30

Status: PENDING VERIFICATION

Purpose:
- re-check whether the initial forensic audit findings were still current
- explicitly mark stale findings as stale
- replace them with fresher evidence where available

## What Was Rechecked

- active Unity instance binding
- editor readiness state
- build scenes
- loaded scene state
- installed package surface
- test inventory
- current error surface in console and `Editor.log`
- disputed compile findings from the first audit pass

## Current Truth That Reconfirmed

These findings remain current after reverification:

- Active build flow is still:
  - `00_BOOTSTRAP`
  - `01_MAIN_MENU`
  - `02_HECTON_WORLD`

- Active loaded scene is still:
  - `02_HECTON_WORLD`
  - `isDirty = true`
  - `rootCount = 20`

- Active render pipeline is still URP:
  - quality: `Surface (Medium)`
  - asset: `Assets/_Project/Data/URP_Medium (PC_RPAsset).asset`

- DOTS is still not a live production backend:
  - installed packages do not include `com.unity.entities`
  - DOTS asmdef still references Entities behind constraints
  - DOTS scripts still explicitly describe fallback behavior

- Test maturity is still extremely weak:
  - current test inventory exposes only 2 tests
  - `RequiredTest` (EditMode)
  - `Submerge` (PlayMode)

## Findings From Initial Pass That Are Now Stale Or Unconfirmed

### 1. First-party compile errors in `PlayerInventory.cs`

Initial pass captured console errors claiming `InventoryDTO.itemGeneticsWords` did not exist.

Current reverification result:
- stale as a current finding
- the symbol exists in current source
- `PlayerInventory.cs` contains `dto.itemGeneticsWords`
- `SaveData.cs` contains `public uint[] itemGeneticsWords;`
- `PlayerInventory.cs` was modified at `2026-04-30 03:42:59`

Verdict:
- keep this only as evidence that compile truth was unstable during the day
- do not keep presenting it as the current live blocker

### 2. First-party compile errors in `ResourceNodeTemplate.cs`

Initial pass captured console errors claiming `MinimumDensity` / `MaximumDensity` did not exist.

Current reverification result:
- stale as a current finding
- current source contains these members
- `ResourceNodeTemplate.cs` defines `MinimumDensity` and `MaximumDensity`
- file modification time is `2026-04-30 03:46:18`

Verdict:
- same rule: historical instability, not confirmed current blocker

## New Current Top Blocker

The current live blocker is not the earlier compile snapshot.
The current live blocker is editor/runtime instability around resource spam.

Fresh evidence:
- MCP session had to be rebound manually to the active Unity instance
- `refresh_unity` timed out after 60 seconds waiting for readiness
- `mcpforunity://editor/state` became unavailable or stale during that path
- main Unity process remained `Responding=False`
- `Editor.log` tail is saturated by:
  - `Resource ID out of range in SetResource: ... (max is 1048575)`

This is not isolated console noise.
It is current editor-state degradation.

## Current Fresh Facts

### Unity / MCP Session

- active instance: `Hecton8@5898b2fd69afdd2d`
- Unity version: `6000.4.1f1`
- MCP can see the instance list
- MCP tool readiness is not stable across all operations

### Installed Packages

Fresh package list confirms:
- `com.unity.addressables 2.7.6`
- `com.unity.inputsystem 1.19.0`
- `com.unity.memoryprofiler 1.1.12`
- `com.unity.probuilder 6.0.9`
- `com.unity.render-pipelines.universal 17.4.0`
- `com.waveharmonic.crest 5.4.1`
- no `com.unity.entities`

### Rendering Stats

Fresh editor-side stats sample:
- draw calls: 0
- batches: 0
- set pass: 0
- render textures: 981
- render texture bytes: 1,534,380,437

Interpretation:
- still not valid gameplay performance proof
- still enough to flag heavy render-texture pressure in editor context

## Revised Audit Confidence

Confidence increased:
- package truth
- DOTS non-production status
- scene/build truth
- weakness of test coverage

Confidence decreased:
- any claim depending on stable editor responsiveness
- any claim depending on current compile success after the editor became non-responsive

## What This Changes In The Audit

Keep:
- architecture drift finding
- monolith risk finding
- DOTS seam finding
- weak test maturity finding
- strong subsystem-depth finding

Downgrade or replace:
- earlier compile-error specifics as current blockers

Replace with:
- editor-state instability
- `SetResource` error flood
- MCP/session/readiness volatility as current operational truth

## Updated Brutal Summary

The initial audit was directionally correct.
It was not perfectly time-stable.

The project changed underneath the audit window, and the editor itself entered a degraded state during reverification.

Current strongest verified conclusions:
- the project still has real engineering depth
- the project still has architecture drift
- DOTS is still mostly non-production
- tests are still extremely weak
- the current live operational problem is editor instability and resource-error flood, not the earlier compile snapshot
