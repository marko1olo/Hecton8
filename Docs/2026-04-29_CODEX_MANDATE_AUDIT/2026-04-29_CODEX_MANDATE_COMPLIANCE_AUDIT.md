# 2026-04-29 - CODEX Mandate Compliance Audit

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit only

## Mandates Followed

This audit was produced against:

- `AGENTS.md`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/UI_Data_Streaming_ZeroGC_Optimization.txt`

## Method

- Full static scan of first-party code under `Assets/_Project`.
- Coverage: `961` C# files scanned.
- Automated pattern scans were run for ownership, event buses, tick usage, force application, job barriers, asset streaming, forbidden scene search, and UI text mutation.
- High-risk matches were then read directly in source.
- No Unity runtime session, Profiler capture, GCMonitor output, or MCP execution was used here.

## Executive Summary

The project contains substantial architecture and compliance drift against its own mandates.

The biggest objective gaps are:

1. Global ownership is not actually centralized on `GlobalRegistry`; singleton + `DontDestroyOnLoad` patterns remain widespread.
2. The mandated zero-alloc static event bus model backed by `NativeQueue<T>` is not what the project currently uses; the codebase is still dominated by static `Action` events.
3. The intended async asset streaming stack exists only partially. Governance classes exist, but the loading path is not fully wired end-to-end.
4. Zero-GC UI compliance is uneven. Some HUD paths are good, but many runtime UI controllers still mutate TMP `.text` with strings.
5. Direct gameplay-side physics force application still exists, bypassing the mandated queued force packet model.
6. The codebase has a high density of job `Complete()` barriers and native lifecycle complexity, but no proof in code that barriers are consistently confined to safe swap windows.

This is not a "few isolated misses" situation. The missing work is systemic.

## Confirmed Findings

### 1. GlobalRegistry architecture is not the real source of truth

Mandate conflict:

- `AGENTS.md`: `[FORBID] Classic Singletons and Awake() self-registration. [REQ] Managers accessed via GlobalRegistry`

Evidence:

- `140` shipping-script `Instance` declarations/matches were found outside `GlobalRegistry`.
- Confirmed examples:
  - `Assets/_Project/Scripts/SaveManager.cs`
  - `Assets/_Project/Scripts/ObjectPoolManager.cs`
  - `Assets/_Project/Scripts/SpatialAudioManager.cs`
  - `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`
  - `Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs`

Direct source evidence:

- `SaveManager.cs` documents itself as `Singleton, DontDestroyOnLoad` and exposes `public static SaveManager Instance => _instance;`
- `ObjectPoolManager.cs` documents itself as `Singleton, DontDestroyOnLoad`
- `SpatialAudioManager.cs` documents access via `SpatialAudioManager.Instance`
- `SystemDispatcher.cs` calls `DontDestroyOnLoad(gameObject);`

What is objectively missing:

- A finished ownership migration from singleton managers to one consistent bootstrap and registry path.
- A hard rule boundary separating "temporary compatibility accessors" from actual architecture.

Impact:

- Initialization order remains split-brain.
- Manager lifetime is distributed across multiple systems instead of one explicit owner.
- Regression risk is high during scene transitions and boot flow changes.

### 2. Mandated NativeQueue-backed event buses were not implemented as the dominant event model

Mandate conflict:

- `AGENTS.md`: `EventBus is backed by NativeQueue<T>. Publish() is O(1) and SAFE from Burst Jobs. Subscribe() is Awake-only. Main thread flushes queue in LateUpdate.`

Evidence:

- `108` static `Action` event declarations were found in shipping scripts.
- Confirmed canonical bus files still use direct delegates:
  - `Assets/_Project/Scripts/SaveEvents.cs`
  - `Assets/_Project/Scripts/Interaction/InteractionEvents.cs`
  - `Assets/_Project/Scripts/CraftingEvents.cs`
  - `Assets/_Project/Scripts/ModuleStatusEvents.cs`

Further examples:

- `PlayerFlashlight.cs`
- `PlayerPDA.cs`
- `NarrativeEvents.cs`
- `InventoryEvents.cs`
- `AtlasSignalEvents.cs`
- `AudioLogEvents.cs`
- `PerformanceMonitor.cs`

What is objectively missing:

- One implemented queue-backed runtime event transport for the declared bus families.
- A migration plan off direct static delegates for gameplay-scale event traffic.

Impact:

- Event delivery policy is inconsistent.
- Burst-safe publish guarantees are not established.
- Subscription lifetime rules remain manual and error-prone.

### 3. Asset streaming architecture exists on paper, but the runtime load pipeline is incomplete

Mandate conflict:

- `AGENTS.md`: heavy assets must be `Addressables async only`
- `AGENTS.md`: after scene unload, drain release queue
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

Evidence:

- Shipping-script Addressables references were found in only four files:
  - `Assets/_Project/Scripts/AsyncLoadHelper.cs`
  - `Assets/_Project/Scripts/ItemCatalog.cs`
  - `Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs`
  - `Assets/_Project/Scripts/World/ImpostorSystem.cs`

Direct source evidence:

- `AsyncLoadHelper.cs` explicitly says runtime loading "fails immediately because runtime Resources loading is disabled."
- `AssetLoadDispatcher.cs` defines `TryDequeueReadyTicket(...)`.
- Search result for `TryDequeueReadyTicket(` shows no external consumer; the only hit is its own declaration in `AssetLoadDispatcher.cs`.
- `AssetLifecycleGovernor.cs` enqueues requests into `AssetLoadDispatcher`, but the ready-ticket handoff is not proven to be consumed by any real loader in the scanned runtime.

What is objectively missing:

- One fully wired end-to-end async load path from priority queue -> dispatch ticket -> concrete Addressables request -> completion -> release lifecycle.
- Proof that world-scale heavy assets are actually entering the world through this path rather than direct scene residency or ad hoc loading.

Impact:

- The streaming governance layer is present, but the loading backend is only partially integrated.
- The codebase has policy objects without full operational closure.

### 4. Forbidden unload and scene-search APIs still exist in shipping code

Mandate conflict:

- `AGENTS.md`: `[FORBID] NEVER invoke Resources.UnloadUnusedAssets()`
- `AGENTS.md`: runtime search APIs are forbidden in gameplay architecture except cached/owned references

Evidence:

- `1` confirmed `Resources.UnloadUnusedAssets()` use in shipping scripts.
- `3` confirmed `FindAnyObjectByType` / `FindFirstObjectByType` uses in shipping scripts.

Direct source evidence:

- `Assets/_Project/Scripts/UI/PauseMenuController.cs:1004`
  - `AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();`
- `Assets/_Project/Scripts/World/HectonCaveVoxelAmbientOcclusionController.cs:166`
  - `Object.FindAnyObjectByType<Camera>(...)`
- `Assets/_Project/Scripts/Core/HectonUrpTextureRequirementsGuard.cs:57`
  - `FindAnyObjectByType<OceanRenderer>(...)`

What is objectively missing:

- Enforced static checks preventing forbidden APIs from re-entering shipping code.
- Centralized owner-provided references for camera/ocean/runtime services.

Impact:

- Main-thread memory and scene ownership rules are not locked down.
- The project can regress into search-driven wiring again.

### 5. Gameplay physics still bypasses the mandated force packet route

Mandate conflict:

- `AGENTS.md`: `[FORBID] Direct rb.AddForce() in gameplay code. [REQ] Write ForcePacket structs to physics NativeQueue during FixedUpdate gather phase. PhysicsApplySystem handles actual application.`

Evidence:

- `8` direct `.AddForce(` / `.AddTorque(` matches were found in shipping scripts.

Direct source evidence:

- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`
  - `_body.AddForce(..., ForceMode.Force);`
  - `_body.AddForce(..., ForceMode.Acceleration);`
  - `_body.AddForce(..., ForceMode.VelocityChange);`
  - `_body.AddForce(..., ForceMode.Impulse);`
  - `_body.AddTorque(..., ForceMode.Force);`
  - `_body.AddTorque(..., ForceMode.VelocityChange);`
- `Assets/_Project/Scripts/PhysicsApplySystem.cs`
  - `body.AddForce(...)`
  - `body.AddTorque(...)`

Assessment:

- `PhysicsApplySystem` is the correct application owner.
- `HectonPlayerMotor` is not.

What is objectively missing:

- Full migration of gameplay callers onto packetized physics submission.

Impact:

- Physics ownership is inconsistent.
- Deterministic force gathering is not enforced everywhere.

### 6. Tick/update discipline is incomplete and exception boundaries are not enforced

Mandate conflict:

- `AGENTS.md`: `[FORBID] Update/LateUpdate/FixedUpdate in gameplay code`

Evidence:

- `12` shipping-script direct `Update` / `LateUpdate` / `FixedUpdate` method declarations were found.

Confirmed examples:

- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`
- `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`
- `Assets/_Project/Scripts/TetherManager.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
- `Assets/_Project/Scripts/UI/LocalizedTMPAutoSizer.cs`
- `Assets/_Project/Scripts/UI/LocalizedLayoutMirror.cs`

Assessment:

- Some of these may fall under allowed exceptions.
- The omission is not "any Update exists."
- The omission is lack of a hard, enforced boundary proving which ones are exception-approved and which ones are drift.

What is objectively missing:

- One auditable whitelist for allowed native Unity loop usage.
- One enforcement pass to remove non-whitelisted loop entry points.

Impact:

- Tick policy is partly convention, not a sealed rule.

### 7. Zero-GC UI policy is only partially implemented

Mandate conflict:

- `AGENTS.md`: `Zero-GC UI: Use Span<char> + TryFormat + TMP_Text.SetCharArray(...)`
- `AGENTS.md`: `[FORBID] Updating Text/TMP_Text.text (allocates string)` in HUD paths

Evidence of compliance:

- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` uses `TryFormat(...)` and `SetCharArray(...)`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs` uses `SetCharArray(...)`

Evidence of non-compliance:

- `Assets/_Project/Scripts/UI/InteractionUI.cs:430`
  - `promptText.text = expandedPrompt;`
- `Assets/_Project/Scripts/HUDNotification.cs:290`
  - `_notifText.text = displayMessage;`
- `Assets/_Project/Scripts/HUDNotification.cs:330`
  - `_notifText.text = displayMessage;`
- `Assets/_Project/Scripts/HUDQuickBar.cs:364`
  - `keyTxt.text = SlotKeyLabels[i];`
- `Assets/_Project/Scripts/PDAInventoryTab.cs`
  - multiple live-path `.text = ...` assignments remain in detail/update code
  - examples around lines `1325`, `1350`, `1365`, `1396`, `1401`, `1404`, `1410`, `1438`, `2483`

Assessment:

- The team started the migration.
- The migration is not finished.
- UI policy is strongest in newer HUD code and weaker in broad UI/controller code.

What is objectively missing:

- A project-wide UI text mutation audit separating cold-build labels from live update paths.
- Completion of zero-GC conversion for runtime-mutating panels outside the main suit HUD.

Impact:

- The UI layer is not uniformly zero-GC.

### 8. Job barrier discipline is not proven

Mandate conflict:

- `AGENTS.md`: `[FORBID] Schedule()+Complete() in same Tick/hot path method`
- `AGENTS.md`: `JobHandle.Complete() only permitted in designated end-of-frame swap windows`

Evidence:

- `121` `.Complete(` matches were found in shipping scripts.

Confirmed example:

- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:673`
  - `_weatherJobHandle.Complete();`

Assessment:

- A raw `Complete()` count is not, by itself, proof of violation.
- It is proof that the codebase has many barrier sites and that compliance has not been mechanically sealed.
- The missing work is verification and consolidation, not blind deletion.

What is objectively missing:

- One audited list of approved end-of-frame completion windows.
- One sweep removing or relocating mid-frame barrier sites that do not belong there.

Impact:

- CPU cadence risk remains high.
- Main-thread stalls can re-enter unnoticed.

### 9. Dead and mixed-responsibility code remains inside production files

Evidence:

- `Assets/_Project/Scripts/GameTickManager.cs` still contains a disabled coroutine block:
  - `#if false`
  - `private System.Collections.IEnumerator SlowTickRoutine()`
  - `yield return null;`

Assessment:

- This block is not active runtime behavior.
- It is still production-file debt and contradicts the mandate direction away from coroutine gameplay flow.

What is objectively missing:

- Cleanup of abandoned paths after subsystem migration.
- Stronger separation between production code, experiments, smoke tools, and dead fallback logic.

Impact:

- File-level clarity and maintainability are worse than they need to be.

## System-Level Gap Matrix

| System | Objective state |
|---|---|
| Bootstrap / ownership | Hybrid model. `GlobalRegistry` exists, but singleton lifetime management still dominates. |
| Eventing | Delegate-based event topology still dominant; queue-backed contract not enforced. |
| Physics | Central apply system exists, but gameplay bypass path remains. |
| Asset streaming | Governance layer exists, backend integration incomplete or unproven. |
| UI | Mixed compliance. Main HUD paths are more mature than general UI panels/controllers. |
| Tick discipline | Policy exists, but runtime loop entry exceptions are not sealed. |
| Jobs / Burst | Heavy job usage present, but completion-window discipline is not proven. |
| Save/load | System exists, but still participates in singleton lifetime model. |
| World / third-party bridges | Some direct scene-search coupling remains. |

## What The Project Objectively Missed

- A completed architecture convergence pass.
- A hard compliance layer that prevents forbidden APIs and patterns from being reintroduced.
- End-to-end closure on async heavy-asset streaming.
- Full conversion of runtime UI mutation paths to zero-GC text updates.
- A mandatory approval map for native Unity loop usage and job completion windows.
- Consistent separation of runtime code from compatibility shims, dead code, and migration leftovers.

## Regression Model

CPU:

- Risk source: widespread `Complete()` barriers, singleton boot ambiguity, direct physics bypasses, runtime search APIs.

GC:

- Risk source: delegate event topology, string-based UI mutation outside migrated HUD code, legacy loader stubs and mixed ownership patterns.

Memory:

- Risk source: incomplete streaming closure, singleton persistence, and policy layers that may outlive scene ownership boundaries.

Cadence:

- Risk source: inconsistent tick entry points and partial dispatcher adoption.

Correctness:

- Risk source: architecture divergence between declared contracts and actual execution model.

## Hot Path Impact

- Confirmed hot-path risk areas: UI mutation, physics submission ownership, job completion barriers.
- No profiler-backed cost numbers were captured in this audit.
- Measured proof absent.

## Failure Modes

- Scene transition stalls or hidden retained state because manager lifetime is spread across multiple persistent singletons.
- Event ordering drift or missed unsubscribe failures due to direct static delegate usage.
- Runtime hitching from barrier-heavy job completion placement.
- Asset residency bugs because governance and loading backend are not fully closed together.
- HUD/UI alloc spikes when non-migrated panels update frequently.

## Why These Findings Were Kept

- Each finding is backed by direct source matches, not inference alone.
- Where evidence was incomplete, the wording was kept at "unproven" rather than overstated as a hard bug.

## Verification Status

Static verification only.

Not performed:

- Unity scene execution
- GCMonitor capture
- Profiler frame capture
- Memory retention slope test
- MCP automated self-test run

Final status: PENDING VERIFICATION
