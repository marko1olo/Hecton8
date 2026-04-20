# HECTON-8 Gemini Reality Audit And Execution Plan

Status: `ACTIVE`
Verification: `PENDING VERIFICATION`
Date: `2026-04-19`

## Purpose

This document converts `Docs/ГЕМИНИ СОВЕТУЕТ/гемини советует - саммари.txt` from raw suggestion dump into a project-reality execution plan.

This is not a rewrite of repository authority.

Authority remains:

- `AGENTS.md`
- `Docs/README.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/2026-04-16_Autonomous_Runtime_Stabilization/HECTON8_AUTONOMOUS_RUNTIME_EXECUTION_PLAN.md`
- `Docs/SYSTEMS_CONTRACTS.md`

This file exists to answer four questions with evidence:

1. what Gemini proposed
2. what already exists in project code
3. what is still missing
4. what should actually be implemented next without breaking current architecture

---

## Evidence Base

Reviewed sources and live state:

- `AGENTS.md`
- `Docs/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
- `Docs/2026-04-16_Autonomous_Runtime_Stabilization/HECTON8_AUTONOMOUS_RUNTIME_EXECUTION_PLAN.md`
- `Docs/2026-04-17_Underwater_Visual_Audit/HECTON8_UNDERWATER_VISUAL_RUNTIME_AUDIT.md`
- `Docs/AI_Fauna/README.md`
- `Packages/manifest.json`
- live Unity Build Settings
- live Unity console
- first-party runtime scripts under `Assets/_Project/Scripts`

---

## Verified Project Truth

### Repository / package truth

- First-party asmdefs already exist.
- Current first-party assemblies include:
  - `Hecton8.Core`
  - `Hecton8.Editor`
  - `Hecton8.Input`
  - `Hecton8.Input.Generated`
  - `Hecton8.Bootstrap.Contracts`
  - `Hecton8.World.Contracts`
  - `Hecton8.World.Dots`
- `com.unity.entities` is installed.
- `com.unity.inputsystem` is installed.
- `com.unity.memoryprofiler` is installed.
- `com.unity.probuilder` is installed.
- `com.unity.animation.rigging` is not installed.

### Scene / runtime truth

- Build Settings are aligned with repository contract:
  - `00_BOOTSTRAP`
  - `01_MAIN_MENU`
  - `02_HECTON_WORLD`
- Unity editor is connected and ready for tools.
- Active scene is `Assets/_Project/Scenes/02_HECTON_WORLD.unity`.
- The scene is already dirty. Do not perform blind prefab or scene rewrites in this pass.

### System ownership truth

The major runtime owners Gemini talks about already exist:

- `GameTickManager`
- `SaveManager`
- `ObjectPoolManager`
- `HectonFluidEngine`
- `FaunaDirector`
- `FaunaBrain`
- `WorldProceduralScatterDirector`
- `WorldProceduralStateRegistry`
- `DynamicResolutionScaler`
- `CameraJuiceProcessor`
- `CameraJuiceSystem`
- `HectonUnderwaterVisuals`

Additional ownership finding from this pass:

- active interaction prompt owner on `Suit_HUD_Canvas.prefab` is `Assets/_Project/Scripts/Interaction/InteractionUI.cs`
- `Assets/_Project/Scripts/UI/InteractionUI.cs` was not found in scene/prefab references and should be treated as inactive/legacy until proven otherwise

### Live blocker truth

Unity console currently reports active runtime/editor damage:

- repeated `NullReferenceException` spam from `HectonUnderwaterVisuals.ApplySpaceCameraDepthState`
- scene contains a real `SpaceCamera`
- the script still enters a path where `_spaceCamera == null`

This is not theoretical backlog. This is current damage.

---

## Gemini Triage Matrix

| Gemini item | Status in project | Evidence | Decision |
|---|---|---|---|
| Immediate asmdef split before any new code | stale / partially already done | first-party asmdefs already exist | do not repeat blindly; continue only fact-based boundary cleanup |
| Feature-based folder migration | mostly absent | runtime still centered in `Assets/_Project/Scripts` | low priority structural improvement, not first blocker |
| Blanket ScriptableObject event architecture | conflicts with repo rules | `AGENTS.md` mandates static zero-alloc event buses | reject as default migration target |
| `IConsumableProvider` decoupling | absent | `Gameplay/ConsumableItem.cs` still talks directly to `HectonSurvivalSystem` | valid medium-risk refactor candidate |
| `HectonStringCache` for HUD | partial | `UI/HudNumericStringCache.cs` exists; several HUDs also pre-cache strings; UI still contains `.ToString()` and `string.Format()` offenders | do not add duplicate owner; extend or unify existing cache strategy |
| Stable runtime resource identity | partial | `WorldProceduralScatterDirector` already computes `StableHash`; world systems already use `runtimeKey` and `WorldProceduralStateRegistry` | audit stability before redesign |
| BitArray resource-state persistence | absent in current registry | `WorldProceduralStateRegistry` uses `HashSet<long>` and `Dictionary<long,...>` | possible future memory compression task after runtime-key audit |
| Atomic save / backups / checksum / migration | implemented | `SaveManager.cs`, `SaveMetadata.cs`, `SaveDataMigration.cs` already contain `.tmp`, `.bak`, CRC32, version migration | not a current missing system |
| Save command buffer thread queue | not present as explicit queue | current `SaveManager` already performs async save/load and integrity handling | do not add second save architecture without measured stall evidence |
| Spatial hash for fauna target search | absent in fauna layer | fauna currently uses `OverlapSphereNonAlloc`; `NativeParallelHashMap` exists elsewhere, not in fauna search | good future optimization candidate, but only after profiler proof |
| `RaycastCommand.ScheduleBatch` for AI vision | tooling exists, fauna integration absent | `RaycastBatchHelper.cs` already exists; fauna not wired to it | reuse existing owner if profiler justifies, do not invent another batch helper |
| `NoiseManager` global noise bus | absent | no first-party runtime hit found | strong candidate if fauna scan cost is proven |
| `CameraStabilizer` new script | absent as separate owner | `CameraJuiceProcessor`, `CameraJuiceSystem`, `PlayerSwimPresentationController` already own camera feel | do not add a second camera-feel owner without proof |
| Collision matrix overhaul | blocked in this pass | would modify project settings / layer matrix | requires explicit permission and measured plan |
| Animation Rigging foot IK | blocked | package absent from manifest | cannot auto-apply without package approval |
| Switch all fast rigidbodies to `Continuous Speculative` | conflicts with current player setup | `HectonPlayerMovement.cs` sets `ContinuousDynamic` | high regression risk; needs logs and collision repro before touching |
| Power grid move to jobs | absent | `PowerGridManager` remains `ISlowTickable` main-thread owner | possible later optimization, not first-wave task |

---

## What We Already Have

These are real systems, not wishlist items:

- save backup / temp / checksum / migration chain
- first-party asmdef baseline
- world procedural runtime keys and stable hashes
- `WorldProceduralStateRegistry` for suppressed placements and fauna spawn state
- non-alloc physics usage across player/tools/fauna
- `RaycastBatchHelper` already implemented
- adaptive runtime pressure systems:
  - `DynamicResolutionScaler`
  - `RuntimePerformanceProfiler`
  - underwater adaptive budget response
- underwater visual owner already exists and is the correct owner
- existing camera-feel owners already exist

Conclusion:

- the project is not missing architecture everywhere
- it is missing targeted completion, cleanup, and proof

---

## What Is Actually Missing

### Tier 0: live blockers

- active editor/runtime exception spam in `HectonUnderwaterVisuals`
- stale console noise that hides new failures

### Tier 1: safe optimization / cleanup candidates

- unify numeric string caching instead of scattered ad-hoc caches
- remove remaining UI `.ToString()` / `string.Format()` offenders where they execute in repeated refresh paths
- audit direct survival coupling in consumable flow

### Tier 2: medium-risk architecture work

- fauna perception scaling:
  - staged search cadence
  - noise bus
  - optional spatial hash
  - optional batched vision if profiler proves value
- world-state compression audit for large procedural persistence

### Tier 3: blocked or high-risk items

- project setting edits
- package additions
- collision matrix rewrites
- player movement collision mode changes
- separate camera owner invention
- broad feature-folder migration during active dirty branch state

---

## Performance / Beauty / Risk Summary

### Likely improves performance

- fixing `HectonUnderwaterVisuals` exception spam
- consolidating HUD numeric caching through existing cache ownership
- reducing repeated UI formatting churn
- reusing `RaycastBatchHelper` instead of inventing another batched raycast path
- fauna noise-event routing if scan cost is later proven

### Likely hurts performance or stability if done blindly

- another large asmdef rewrite pass without dependency cleanup proof
- blanket ScriptableObject event migration against static-event-bus contracts
- adding Animation Rigging package mid-pass
- collision mode changes for player transport stack without repro
- save architecture rewrite while current save system already has backup/integrity/migration logic

### Likely improves beauty

- continue using `HectonUnderwaterVisuals` as single owner instead of splitting presentation
- tune underwater layering and acoustic authoring inside existing owners
- use existing camera-feel stack instead of adding a parallel stabilizer owner

### Likely causes regression risk

- touching project settings
- touching scene wiring while scene is dirty
- changing player movement collision model
- creating new camera ownership or new world-state owners

---

## Execution Order For This Pass

### Workstream 1: stop live runtime/editor damage

Target:

- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`

Action:

- fix the invalid null path in `ApplySpaceCameraDepthState`
- verify Unity compiles
- verify console stops reporting this exception

Why first:

- current console noise invalidates further diagnostics

### Workstream 2: HUD numeric cache audit

Targets:

- `Assets/_Project/Scripts/UI/HudNumericStringCache.cs`
- `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`
- `Assets/_Project/Scripts/UI/PauseMenuController.cs`
- `Assets/_Project/Scripts/UI/PDALoadoutTab.cs`
- `Assets/_Project/Scripts/UI/SettingsPanel.cs`
- `Assets/_Project/Scripts/Interaction/InteractionUI.cs`

Action:

- classify each offender:
  - cold-path acceptable
  - repeated-refresh but acceptable
  - must be cached
- extend existing cache owner instead of adding duplicate global cache class

Why second:

- safe, measurable, aligned with zero-GC rules

### Current execution status

- Workstream 1 started:
  - `HectonUnderwaterVisuals.ApplySpaceCameraDepthState` null path patched
  - editor script refresh completed with empty console immediately after refresh
  - `_spaceCamera` comparisons inside the owner were hardened after local `Editor.log` still showed historical null/unassigned faults
  - local `Editor.log` now shows a successful later auto-compile/domain reload with no fresh `ApplySpaceCameraDepthState` hits in the 31k+ lines after the last historical match
  - later script refreshes no longer reproduced the older historical `PlayerTransportFeelContract` errors
  - current compile tail was re-established after fixing three stale localization helper wrappers in:
    - `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`
    - `Assets/_Project/Scripts/Gameplay/HazardExposureNotifier.cs`
    - `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
  - latest `Editor.log` tail shows a successful domain reload / `CompileScripts` pass with no fresh compile errors after that helper fix
  - live runtime proof is still blocked by Unity MCP instability after entering Play Mode
- Workstream 2 started:
  - `PauseMenuController` main-menu load progress now reuses `HudNumericStringCache` instead of per-update `ToString()`
  - active `Interaction/InteractionUI` owner now caches the prompt prefix outside hover updates and null-guards `RebindingManager.Instance` during subscribe / unsubscribe
  - `PDALoadoutTab` now caches prefab `PlayerTool` owners for repeated slot/summary/preset refresh passes
  - `PDADataLogTab` now caches localized UI strings for list/detail/play-button refresh, updates static header / empty-state labels on language change, replaces count-label `string.Format` with TMP `SetText`, and avoids redundant detail-visibility `SetActive` churn
  - `PDASpectrumTab` now uses prebuilt mode-status strings plus TMP dirty guards instead of rebuilding active-mode / sonar labels during refresh
  - `PDAAtlasSignalTab` code pass now replaces dirty-refresh string interpolation with prebuilt labels, owner-local dirty guards, and `TMP.SetText` / `StringBuilder` formatting on the atlas direction path
  - live `SettingsPanel` owner in `01_MAIN_MENU` now dirty-guards localized value labels for quality level, shadow quality, anti-aliasing, and texture quality instead of rewriting identical TMP text during `RefreshAllUI`, preset clicks, and arrow clicks
  - `PauseMenuController` now dirty-guards the settings-language status label instead of writing raw `.text` on every refresh / cycle path
  - live `RelayHUDElement` owner on the active suit HUD overlay now dirty-guards repeated `CanvasGroup` visibility writes and repeated color assignments on the relay marker tick path
  - `HUDQuickBar` owner now dirty-guards repeated slot highlight and durability color writes instead of rewriting identical `Image.color` values during slot/durability invalidation refreshes
  - inactive duplicate owner `UI/InteractionUI.cs` was identified and excluded from further execution work
  - `validate_script` continues to emit duplicate-signature false positives on `PDADataLogTab`
  - manual script refresh advanced `Editor.log` past the latest `PDADataLogTab.cs` write time
  - later script refresh advanced `Editor.log` past the latest `PDASpectrumTab.cs` write time
  - latest post-`PDASpectrumTab` `Editor.log` tail shows a successful domain reload / `CompileScripts` pass with no fresh `PDASpectrumTab` errors in the final tail
  - attempted `PDAAtlasSignalTab` compile verification stalled: `refresh_unity` timed out waiting for editor readiness, MCP lost the Unity session again, and local `Editor.log` did not advance past the pre-pass timestamp
  - attempted `SettingsPanel` compile verification stalled in the same way: `refresh_unity` timed out, MCP console stayed unavailable, and local `Editor.log` remained older than the latest `SettingsPanel.cs` write time
  - attempted latest `PauseMenuController` compile verification stalled in the same way: `refresh_unity` timed out, MCP console stayed unavailable, and local `Editor.log` remained older than the latest `PauseMenuController.cs` write time
  - attempted `RelayHUDElement` compile verification still lost the MCP side in the same way (`refresh_unity` timeout + `read_console` session-not-ready), but local `Editor.log` did advance past the latest `RelayHUDElement.cs` write time and the final tail shows a successful domain reload / `CompileScripts` pass with no fresh `RelayHUDElement` errors in the final tail
  - `HUDQuickBar` compile verification completed: `read_console` returned only 2 warnings (`PDALoadoutTab` obsolete `GetInstanceID`, `BiomeMatrixDirector` unused debug field), local `Editor.log` advanced past the latest `HUDQuickBar.cs` write time, and the final log tail shows a successful domain reload / `CompileScripts` pass with no fresh `HUDQuickBar` errors in the final tail
  - Unity MCP session still drops on domain reload, so the latest compile proof for this workstream comes from `Editor.log` rather than a post-reload MCP console read

### Workstream 3: consumable decoupling audit

Targets:

- `Assets/_Project/Scripts/Gameplay/ConsumableItem.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerActionController.cs`

Action:

- design minimal `IConsumableProvider` seam only if dependency surface is controlled
- do not break existing item flow

Why third:

- good architectural cleanup, but not worth doing while console is burning

### Workstream 4: fauna scaling roadmap

Targets:

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Fauna/FaunaSensorSuite.cs`
- `Assets/_Project/Scripts/FaunaDirector.cs`
- `Assets/_Project/Scripts/RaycastBatchHelper.cs`

Action:

- measure current fauna scan cost first
- if needed, stage this in order:
  1. cadence spreading
  2. noise bus
  3. spatial hash
  4. batched vision

Why fourth:

- bigger payoff, bigger regression surface

Current status:

- first centralized noise slice is now in code:
  - `NoiseSystem` no longer acts only as a per-listener calculator; it now stores a fresh player-noise snapshot for fauna consumption
  - new `Gameplay/PlayerNoiseEmitter` owner reports player movement, flashlight state, transport signature, and tool-use pulse into `NoiseSystem`
  - `FaunaSensorSuite` now ensures the emitter is attached to the resolved player root and consumes the centralized snapshot before falling back to legacy direct-player reads
- intended engine effect:
  - remove repeated per-fauna reads of player `Rigidbody`, flashlight, and transport/tool state from the major-sense path
  - preserve existing transport / stealth logic as fallback while shifting the hot owner toward one source of truth
- verification is weak:
  - Unity MCP died before structural validation could be re-run on this slice
  - local `Editor.log` did not advance past the latest noise-file write time, so there is still no Unity-side compile proof for this workstream
  - standalone batch compile was blocked because the live Unity editor already has the project open

### Workstream 5: save-state compression audit

Targets:

- `Assets/_Project/Scripts/WorldProceduralStateRegistry.cs`
- `Assets/_Project/Scripts/SaveManager.cs`
- `Assets/_Project/Scripts/SaveData.cs`

Action:

- inspect actual cardinality and memory growth first
- only then decide whether `BitArray` or packed persistence is worth it

Why later:

- current save system already has stronger fundamentals than Gemini assumed
- current clean owner slice is still worth doing for the fixed 108-biome discovery matrix

Current status:

- started on the clean biome-discovery owner chain:
  - `SaveData` upgraded to v20 and now stores discovery state in packed `long[] discoveredBiomeBitWords`
  - new `BiomeDiscoveryBitMask` owner centralizes pack / unpack / fallback logic for the 108-biome matrix
  - `HectonDiscoveryManager` now writes packed words on save and reads packed words first on load
  - `SaveDataMigration` now backfills packed words from legacy `HashSet<int>` saves
- code-level verification is positive:
  - `validate_script` passed for `BiomeDiscoveryBitMask`, `SaveData`, and `SaveDataMigration`
  - `validate_script` reported a duplicate-method false positive on `HectonDiscoveryManager`; direct grep shows one `ResolveFallbackLastDiscoveredId` definition only
- Unity compile is still blocked by unrelated transport errors:
  - `PlayerTransportCoordinator` cannot resolve `IPlayerTransportLifecycleOwner` at lines 25, 81, and 238

---

## Regression Model

### Workstream 1: underwater visual null-guard

- CPU: should improve by removing exception path and editor spam
- GC: no new allocation path introduced
- memory: no change expected
- cadence: no gameplay cadence change intended
- correctness failure mode: if guard is wrong, deep-water space-camera cull may stop applying in editor-only edge cases
- reason kept: current exception path is objectively broken

### Workstream 2: HUD cache cleanup

- CPU: should improve slightly in repeated UI refresh paths
- GC: should improve where `.ToString()` churn exists
- memory: small static cache growth only if unified cache expands
- cadence: no gameplay cadence change
- correctness failure mode: wrong string table indexing can display bad values

### Workstream 3: consumable provider seam

- CPU: neutral
- GC: neutral if interface is cached and not boxed
- memory: neutral
- cadence: no change expected
- correctness failure mode: item consumption can stop applying survival effects
- warning: regression risk in inventory / use-action flow

### Workstream 4: fauna scaling

- CPU: potential major win
- GC: should remain zero in hot paths
- memory: may increase if spatial cache or job buffers are added
- cadence: can alter AI responsiveness if staged badly
- correctness failure mode: missed detections, dead fauna behavior, broken threat response

### Workstream 5: save-state compression

- CPU: could improve IO, could worsen encode/decode cost
- GC: depends on representation
- memory: should improve only if large state volume is real
- cadence: save/load cadence can regress if packed format is wrong
- correctness failure mode: corrupted or mismatched procedural suppression state

---

## Immediate Actions Logged In This Pass

1. Created this audit folder and master plan.
2. Verified that Gemini asmdef advice is partially obsolete in current repo state.
3. Verified current Build Settings and live Unity instance.
4. Identified active `HectonUnderwaterVisuals` exception spam as the first concrete runtime blocker.
5. Started code fix on that blocker before wider plan execution.
6. Replaced 108-biome discovery save persistence with packed 64-bit words while preserving legacy-set migration.
7. Confirmed the packed-biome pass introduced no fresh compile errors in touched save-format owners; current compile blocker is unrelated transport code.
8. Started centralized player-noise snapshot work to reduce repeated fauna reads of player movement / light / tool / transport state.
9. Removed delegate-capturing query lambdas from `WorldSpatialHashGrid` so fauna and PDA sonar paths stop allocating hidden closures in hot query calls.
10. Extended `WorldSpatialHashGrid` to register `PickupItem`, `ScannableTarget`, and `ModuleMarker`, and routed `ScannerTool` scan contact discovery through the spatial grid instead of `Physics.OverlapSphereNonAlloc`.
11. Added dynamic spatial-grid migration entry points in `WorldSpatialHashGrid` and moved `PickupItem` onto `ISlowTickable` so moving pickups can update cell membership without `Update()`.
12. Promoted player-noise delivery from pull to push: `NoiseSystem` now dispatches fresh player-noise pulses to nearby fauna through the spatial grid, and `FaunaSensorSuite` consumes cached pushed signals instead of polling the global snapshot during major-sense updates.
13. Rewired `PDASpectrumTab` to `BiomeMatrixDirector` events and replaced static filler lines with live biome matrix / depth / turbidity / absorption / thermal readouts built through the existing HUD numeric cache path.
14. Recovered Unity-side compile verification through headless batch mode after MCP session loss; latest batch log shows `*** Tundra build success`, `AssetDatabase: script compilation time`, and `Exiting batchmode successfully now!` with no `error CS` lines for this pass.
15. Added packed pickup depletion persistence:
   - `PickupItem` now resolves a deterministic world-state key + chunk key from scene path / hierarchy path / authored anchor position
   - `WorldStateManager` now tracks depleted pickups separately from depleted resource nodes and serializes pickup depletion into packed chunk bitmasks instead of string IDs
   - `SaveData` upgraded to v21 with packed pickup-chunk arrays and migration-safe capacity repair in `SaveDataMigration`
   - `WorldStateManager.cs.meta` had to be restored after file replacement because Unity stopped importing the script without it
   - earlier `SettingsManager` errors were stale compile-state artifacts, not current source errors
   - current compile proof for this slice is a Roslyn pass using Unity Bee rsp inputs patched to include the restored `WorldStateManager.cs` and exclude one stale missing file reference; a fresh `dotnet + csc.dll` pass now reports only the existing `PDALoadoutTab` warning
16. Hardened packed pickup depletion against runtime drop corruption:
   - `PickupItem` now refuses to build authored persistence identity when the instance carries `ObjectPoolManager.PoolItemMarker`
   - this prevents pooled runtime-dropped pickups from polluting authored scene depletion bitmasks
17. Shifted fauna perception further onto the player-noise bus:
   - `FaunaSensorSuite` now refreshes player distance / sleep / LOD gating on the existing major-sense cadence instead of every tick
   - noise pulses cache a last-known player position and can refresh perceived distance without a fresh direct player transform distance query
   - `FaunaStateMachine` now steers threat/stalk/aggressive/retreat/escape behavior from perceived player position memory when direct visual contact is absent
   - `FaunaBrain` now uses perceived player position for eye tracking and retreat steering target input
   - current-source compile proof for this fauna slice is clean except for the existing `PDALoadoutTab` obsolete-warning line
18. Cleared the next official Unity compile blocker in `HectonMapMagicVegetationBridge`:
   - Unity console reported `TerrainTile.isDraft` as a nonexistent API at line 1295
   - the bootstrap tile filter now uses `ResolveMainTerrain(tile) == null` instead of the invalid property
   - after an explicit Unity script refresh, console readback dropped to the existing `PDALoadoutTab` obsolete-warning line only

---

## Verification Protocol

Do not mark any item solved without evidence.

Required checks after each code pass:

1. recompile scripts
2. read Unity console
3. confirm no fresh error spam from touched owner
4. if runtime-affecting, capture profiler or GC evidence when possible

For the current `HectonUnderwaterVisuals` fix:

1. open `02_HECTON_WORLD`
2. wait 5 seconds in editor idle
3. read console
4. expected result:
   - no new `NullReferenceException` from `ApplySpaceCameraDepthState`
   - no regression in compile state

Status remains `PENDING VERIFICATION` until logs confirm.
