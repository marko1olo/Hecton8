# Findings And Evidence

Date: 2026-04-29  
Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## 1. Live Editor State

Unity Editor was reachable through MCP during this pass.

Build Settings currently contain:

- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Loaded scene during inspection: `02_HECTON_WORLD` (dirty in editor, active scene).

### 1.1 Current live console state

Latest console readback returned `15` errors.

All `15` visible entries are package-side MCP asset-inspection failures rather than first-party compile errors.

Observed pattern:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

### 1.2 What changed relative to older audit notes

Earlier same-day audit notes had captured first-party compile and shader failures.
They are not treated as current blockers in this document because the latest reachable console readback on `2026-04-29` no longer reports those first-party failures.

That means:

- earlier compile-breakage claims became stale
- current first-party editor health is improved
- console is still not clean because MCP package-side asset inspection can emit errors into the same console surface
- runtime stability is still unverified because play-mode and build validation were not run

## 2. Quantitative Codebase Snapshot

- First-party script files under `Assets/_Project/Scripts`: `970`
- Total lines in those scripts: `420468`
- Average lines per script: `433.47`
- Files directly in `Assets/_Project/Scripts` root: `312`

### 2.1 Largest owners by line count

- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` - `13279`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` - `10333`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs` - `7851`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` - `4826`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` - `4608`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` - `4200`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs` - `4138`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` - `3921`

Interpretation:
These are not just large files. They are likely regression multipliers because world logic, presentation, and ownership are condensed into a few oversized files.

## 3. Static Rule-Scan Metrics

The counts below are source hits, not automatic guilt.
They are still useful because they show where manual review pressure belongs.

### 3.1 Runtime method ownership

- Runtime `Update/LateUpdate/FixedUpdate` method definitions outside editor folders after filtering interface declarations and noise: `28`
- Exact file matches:
  - `AtlasSignal/AtlasSignalSystem.cs`
  - `BuoyancyObject.cs`
  - `Core/SystemDispatcher.cs`
  - `GameTickManager.cs`
  - `Core/ConnectionSplineBatchRenderer.cs`
  - `GlobalPhysicsStateManager.cs`
  - `Gameplay/ClimbableLadder.cs`
  - `Gameplay/DeployableFlare.cs`
  - `Gameplay/Floater.cs`
  - `Gameplay/FloraProjectile.cs`
  - `Gameplay/OxygenBubble.cs`
  - `Gameplay/OxygenPlant.cs`
  - `Gameplay/ScannableFragment.cs`
  - `Gameplay/SealedDoor.cs`
  - `Gameplay/StorageCrate.cs`
  - `Interaction/EquipmentInteractionHandler.cs`
  - `Interaction/PlayerInteraction.cs`
  - `InteractionHighlighter.cs`
  - `ObjectPoolDiagnostics.cs`
  - `ObserverRelativeCelestialBody.cs`
  - `RepairTool.cs`
  - `SceneBootstrap.cs`
  - `SkySystemFollowCamera.cs`
  - `SpatialAudioManager.cs`
  - `TetherManager.cs`
  - `UI/LocalizedLayoutMirror.cs`
  - `UI/LocalizedTMPAutoSizer.cs`
  - `UI/SuitHUDV4CanvasOverlay.cs`

Interpretation:
The project is not pure dispatcher-only runtime.
The remaining explicit Unity loop owners are concrete review targets, not theory.

### 3.2 Coroutine usage

- Runtime `StartCoroutine(...)` hits: `37`

Interpretation:
Coroutine usage still exists across first-party scripts.
That does not prove all are hot-path violations, but it does prove the migration to tick/state-machine ownership is incomplete.

### 3.3 Runtime object/component lookup pressure

- Runtime `GetComponent*` hits: `926`
- Runtime `TryGetComponent(...)` hits: `647`

Interpretation:
The codebase knows the right API (`TryGetComponent` is common), but component lookup volume is still high enough that cold-path and hot-path separation must be treated skeptically on a per-owner basis.

### 3.4 Runtime object activation in UI / interaction

- Current `SetActive(...)` hits in `UI` + `Interaction`: `2`

Observed examples:

- `Assets/_Project/Scripts/UI/DiegeticPDAController.cs:280`
- `Assets/_Project/Scripts/UI/DiegeticPanelController.cs:1031`

Counterpoint:
This is materially better than older scans implied.
It does not prove hot-path safety, but it does remove one previously overstated red flag.

### 3.5 Addressables / asset lifecycle

- Concrete first-party `LoadAssetAsync<GameObject>()` usage confirmed in `Assets/_Project/Scripts/ItemCatalog.cs`
- Runtime addressable release hits: `1`
- Legacy `AsyncLoadHelper` still exists, but its runtime path is intentionally disabled rather than serving as an active asset-loading owner

Interpretation:
There is not strong evidence of a project-wide runtime asset lifecycle layer matching the mandate. There is evidence of one local implementation.

### 3.6 Forbidden or high-risk API surfaces

- `Resources.UnloadUnusedAssets()` runtime hits: `0`
- `DG.Tweening` / `DOTween` runtime hits under `Assets/_Project/Scripts`: `0`
- Runtime `Find* / Resources.FindObjectsOfTypeAll / FindAnyObjectByType` hits: `136`

Interpretation:

- older violations around `UnloadUnusedAssets()` and DOTween are not current in this scan
- broad object-finding usage is still noisy enough to warrant skepticism

## 4. Specific Architectural Findings

### 4.1 Dual bootstrap authority

Evidence:

- `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs`
  explicit registry/service initialization layers

- `Assets/_Project/Scripts/SceneBootstrap.cs`
  async scene startup owner, pool warmup, save load, world readiness, player activation

Problem:
The codebase has more than one startup brain.
That is survivable, but it invites order drift, duplicate guarantees, and unclear failure ownership.

### 4.2 UI service reality

Evidence:

- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
  direct `IUIService` implementor

- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`
  registers itself through `GlobalRegistry.RegisterUIService(this)`

Problem:
The direct registry contract currently resolves to one owner, not several.
The remaining issue is wider UI ownership sprawl, not direct `IUIService` ambiguity.

### 4.3 Audio contract is no longer ghosted

Evidence:

- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` defines `IAudioService`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs` exposes audio registration/getter paths
- `Assets/_Project/Scripts/SpatialAudioManager.cs` directly implements `IAudioService`
- `Assets/_Project/Scripts/SpatialAudioManager.cs` registers itself through `GlobalRegistry.RegisterAudioService(this)`

Problem removed:
the current contract has a real owner.
Remaining risk is not ghosting but the size and responsibility load of `SpatialAudioManager`.

### 4.4 Event architecture is mixed

Evidence:

- queue-backed: `SaveEvents`, `QuestEvents`, `ScanEvents`, `NarrativeEvents`, `AudioLogEvents`
- direct static buses: `InteractionEvents`, `CraftingEvents`, `PDAEvents`, `FlashlightEvents`, `RandomEventEvents`, `HectonSubmarineOsEvents`

Problem:
Migration toward deferred event lanes is real, but not complete.
This complicates reasoning about ordering, burst-safety, and ownership consistency.

### 4.5 Runtime fail-safe spawning hides authored ownership gaps

Evidence:

- `Assets/_Project/Scripts/World/WorldReadabilityRuntimeBootstrap.cs`
  spawns `WorldReadabilityDirector` / `EmergencyServiceRelayDirector` if missing

Problem:
Useful emergency behavior.
Bad long-term authority model.
If runtime bootstrap keeps patching missing scene authorship, scene correctness becomes harder to reason about.

## 5. File-Level Notes Worth Keeping

### 5.1 `Assets/_Project/Scripts/Core/SystemDispatcher.cs`

What is strong:

- dense registry buckets
- explicit lane priorities
- `RaycastCommand` batching
- native queue/list/array ownership
- end-of-frame completion window

What is risky:

- it has become a central dependency magnet
- if this owner regresses, many systems regress together

### 5.2 `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`

What is strong:

- queued signal path
- scheduled raycast buffers
- explicit service registration

What is risky:

- still relies on `LateUpdate()`
- resolves targets through `GetComponentInParent<...>()` during dispatch
- central handler can become a choke point if more interaction types are stuffed into it

### 5.3 `Assets/_Project/Scripts/UI/LoadingScreenController.cs`

What is strong:

- `CanvasGroup` gating
- tick-driven fade state machine
- cached percent strings

What is weak:

- still string-based status/tip updates
- no proof yet that every caller respects load/scene activation guardrails

### 5.4 `Assets/_Project/Scripts/SpatialAudioManager.cs`

What is strong:

- direct `IAudioService` ownership is explicit
- registry registration is explicit
- cold-allocation discipline is visible in large parts of the file

What is risky:

- the file is large
- audio service ownership, radar data, pooling, and world-facing audio responsibilities are concentrated into one owner

### 5.5 `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

What is strong:

- direct `IUIService` ownership is explicit
- registry registration is explicit
- zero-GC HUD formatting intent is visible in cached buffers and `SetCharArray` style usage

What is risky:

- the file is very large
- HUD rendering, scanner visuals, threat overlays, and service-layer responsibilities are concentrated into one owner

## 6. What Was Good In Practice

- The codebase is not fake-enterprise. There are real attempts at registry ownership, packet contracts, jobs, native containers, and zero-GC discipline.
- Several runtime systems already document their cold allocations explicitly.
- Build Settings scene order currently matches the required `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
- Current reachable editor console is no longer showing the compile/shader failure set cited by older audit notes.

## 7. What Was Weak But Recoverable

- The codebase mixes old and new patterns.
- Many systems are clearly mid-migration rather than intentionally final.
- A lot of the pain comes from incomplete convergence, not from absence of technical intent.

## 8. What Was Simply Bad

- Massive file concentration.
- Split bootstrap authority.
- Root-folder ownership sprawl.
- Mixed event architecture.

## 9. Runtime Verification Limits

Even with the cleaner current console state, the following remain blocked:

- trustworthy play-mode behavior judgement for quest/save/world/UI flows
- meaningful profiler validation
- meaningful GC validation in live gameplay
- meaningful smoke-test verdicts

Because of that, this report does not claim:

- that the game currently boots clean from `00_BOOTSTRAP` into a stable playable loop
- that save/load works
- that quest progression works
- that vegetation rendering is visually correct in motion

Those would be invented claims.

## Regression Model

CPU: no code changed  
GC: no code changed  
Memory: no code changed  
Cadence: documentation only  
Correctness: improved because stale blockers were removed and current evidence replaced them

## Hot Path Impact

None. Documentation-only pass.

## Failure Modes

- console state can change between editor sessions
- static scans can overcount cold-path or tooling code
- file-size concentration can hide bugs not visible in architecture-level review

## Why This Version Was Kept

Kept because it no longer confuses historical breakage with current state.
It is still harsh, but it is harsh about things that remain visible now.
