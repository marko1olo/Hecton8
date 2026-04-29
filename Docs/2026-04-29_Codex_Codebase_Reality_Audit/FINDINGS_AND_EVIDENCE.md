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

## 1. Live Compile / Runtime Blockers

Unity Editor was reachable through MCP during this pass.
Build Settings currently contain:

- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

Loaded scene during inspection: `02_HECTON_WORLD` (dirty in editor, active scene).

### 1.1 Current first-party compile errors observed in live console

- `Assets/_Project/Scripts/SaveManager.cs:1161`
  `SaveBinaryStorage.TryWriteSaveFile(...)` call no longer matches the required signature.

- `Assets/_Project/Scripts/SaveBinaryStorage.cs:1481`
  Console reports missing `PackedQuestStateSectionHeader`; local readback shows the code now reads `QuestSaveHeader` there, which indicates the compile state and source likely drifted between edits or related declarations are broken elsewhere.

- `Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:49`
- `Assets/_Project/Scripts/Quest/QuestGraphEvaluator.cs:68`
  `NarrativeEvents` symbol not found.

- `Assets/_Project/Scripts/Quest/QuestStateManager.cs:870`
- `Assets/_Project/Scripts/Quest/QuestManager.cs:275`
  `Hecton8.Environment.NewLine` namespace/type lookup is invalid.

- `Assets/_Project/Scripts/HectonFluidEngine.cs:1444`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:1456`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:1463`
- `Assets/_Project/Scripts/HectonFluidEngine.cs:1465`
  storm / thermocline constants are referenced but not defined in scope.

- `Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs:520`
- `Assets/_Project/Scripts/UI/HectonSubmarineOsDisplay.cs:533`
  `char[]` is used with `.AsSpan(...)` without the extension method being available in the current compile context.

- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs:708`
- `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs:1003`
  missing `ItemCatalog` type and `NativeHashMap<uint, float>.Count` used as an invocable member.

- `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:314`
  by-ref usage error `CS8156`.

### 1.2 Live shader exceptions also present

- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetationDepthOnly.shader:497`
- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetationMotionVectors.shader:470`
- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetationShadow.shader:458`
  invalid subscript `color`

This matters because world/vegetation evaluation is not purely a script problem. Even after C# repair, these shader errors still threaten the runtime path.

## 2. Quantitative Codebase Snapshot

- First-party script files under `Assets/_Project/Scripts`: `936`
- Total lines in those scripts: `398253`
- Average lines per script: `425.48`
- Files directly in `Assets/_Project/Scripts` root: `306`

### 2.1 Largest owners by line count

- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` - `12793`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` - `10316`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs` - `7839`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` - `4826`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` - `4257`
- `Assets/_Project/Scripts/HectonVoxelEngine.cs` - `4132`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` - `3870`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` - `3762`

Interpretation:
These are not just large files. They are likely regression multipliers because world logic, presentation, and ownership are condensed into a few oversized files.

## 3. Static Rule-Scan Metrics

The counts below are source hits, not automatic guilt.
They are still useful because they show where manual review pressure belongs.

### 3.1 Runtime method ownership

- Runtime `Update/LateUpdate/FixedUpdate` method definitions outside editor folders: `12`
- Exact matches:
  - `Core/SystemDispatcher.cs`
  - `Core/ConnectionSplineBatchRenderer.cs`
  - `GlobalPhysicsStateManager.cs`
  - `Interaction/EquipmentInteractionHandler.cs`
  - `ObserverRelativeCelestialBody.cs`
  - `TetherManager.cs`
  - `SkySystemFollowCamera.cs`
  - `UI/LocalizedTMPAutoSizer.cs`
  - `UI/LocalizedLayoutMirror.cs`
  - `UI/SuitHUDV4CanvasOverlay.cs`

Interpretation:
The project is not drowning in raw `Update()`, which is good.
The remaining exceptions therefore matter more, not less.

### 3.2 Coroutine usage

- Runtime `StartCoroutine(...)` hits: `37`

Interpretation:
Most hits are smoke-test / verifier / debug utility code, but the project still carries coroutine-heavy validation infrastructure inside first-party scripts.

### 3.3 Runtime object/component lookup pressure

- Runtime `GetComponent*` hits: `700`
- Runtime `GetComponentInParent/GetComponentInChildren` hits: `161`
- Runtime `TryGetComponent(...)` hits: `610`

Interpretation:
The codebase knows the right API (`TryGetComponent` is common), but component lookup volume is still high enough that cold-path and hot-path separation must be treated skeptically on a per-owner basis.

### 3.4 Runtime object activation in UI / interaction

- `SetActive(...)` hits in `UI` + `Interaction`: `35`

Notable examples:
- `Assets/_Project/Scripts/Interaction/InteractionUI.cs:243`
- `Assets/_Project/Scripts/Interaction/InteractionUI.cs:270`
- `Assets/_Project/Scripts/UI/PDAConstructionTab.cs:1224`
- `Assets/_Project/Scripts/UI/PDABarterTab.cs:335`
- `Assets/_Project/Scripts/UI/PDADataLogTab.cs:755` to `763`
- `Assets/_Project/Scripts/UI/DiegeticPDAController.cs:277`

Counterpoint:
`InteractionUI.cs` at least attempts to migrate toward `CanvasGroup`, which is the correct direction.
The broader PDA/pause/UI surface is not consistently there yet.

### 3.5 Addressables / asset lifecycle

- Runtime addressable load hits: `4`
- Runtime addressable release hits: `1`
- Concrete first-party load/release pair observed in `Assets/_Project/Scripts/ItemCatalog.cs`

Interpretation:
There is not strong evidence of a project-wide runtime asset lifecycle layer matching the mandate. There is evidence of one local implementation.

### 3.6 Forbidden or high-risk API surfaces

- `Resources.UnloadUnusedAssets()` runtime hits: `1`
  - `Assets/_Project/Scripts/UI/PauseMenuController.cs:1004`

- DOTween runtime hits: `6`
  - all from `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`

- Runtime `Find* / Resources.FindObjectsOfTypeAll / FindAnyObjectByType` hits: `12`
  - includes `MapMagicBridge.cs`, `HectonUrpTextureRequirementsGuard.cs`, `HectonCrestOceanDepthCacheBootstrap.cs`, `InputManager.cs`, smoke-test probes, and runtime verification helpers

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

### 4.2 UI service fragmentation

Evidence:

- `Assets/_Project/Scripts/HectonFabricatorUI.cs`
- `Assets/_Project/Scripts/HectonSuitHUD_v4.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

All implement `IUIService`.
At the same time, `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` says:

- UI layer adapter does not exist yet

Problem:
The registry contract implies one authoritative UI root.
The current code advertises several.

### 4.3 Ghost audio contract

Evidence:

- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` defines `IAudioService`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs` exposes audio registration/getter paths
- no first-party `: IAudioService` implementor was found in `Assets/_Project/Scripts`

Problem:
This is dead contract surface until someone either owns it or deletes it.

### 4.4 Runtime fail-safe spawning hides authored ownership gaps

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

### 5.4 `Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs`

Problem:

- runtime DOTween dependency directly conflicts with project package policy
- file is large and presentation-heavy
- this is a likely regression hotspot because camera, FOV, and post-process behavior converge here

### 5.5 `Assets/_Project/Scripts/UI/PauseMenuController.cs`

Problem:

- direct `Resources.UnloadUnusedAssets()` call after pending main-menu cleanup
- this is a documented hard ban in `AGENTS.md`

## 6. What Was Good In Practice

- The codebase is not fake-enterprise. There are real attempts at registry ownership, packet contracts, jobs, native containers, and zero-GC discipline.
- Several runtime systems already document their cold allocations explicitly.
- Build Settings scene order currently matches the required `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.

## 7. What Was Weak But Recoverable

- The codebase mixes old and new patterns.
- Many systems are clearly mid-migration rather than intentionally final.
- A lot of the pain comes from incomplete convergence, not from absence of technical intent.

## 8. What Was Simply Bad

- Live compile breakage.
- Shader exceptions in vegetation passes.
- Fragmented service ownership.
- Massive file concentration.
- Explicit rule violations still present in runtime code.

## 9. Runtime Verification Limits

Without repairing compile errors, the following remain blocked:

- trustworthy play-mode behavior judgement for affected quest/save/world/UI flows
- meaningful profiler validation
- meaningful GC validation in live gameplay
- meaningful smoke-test verdicts

Because of that, this report does not claim:

- that the game currently boots clean from `00_BOOTSTRAP` into a stable playable loop
- that save/load works
- that quest progression works
- that vegetation rendering is visually correct

Those would be invented claims.

## Regression Model

CPU: no code changed  
GC: no code changed  
Memory: no code changed  
Cadence: documentation only  
Correctness: improved by grounding judgments in source and live console evidence instead of assumptions

## Hot Path Impact

None. Documentation-only pass.

## Failure Modes

- Some violations counted here are cold-path acceptable but still worth review.
- Some broken files are currently masked by compile failure and may reveal second-order defects after repair.
- Large owner files can hide local good code that this report does not individually score.

## Why Kept

Kept because it is evidence-first.
Not kept because it is pleasant.
