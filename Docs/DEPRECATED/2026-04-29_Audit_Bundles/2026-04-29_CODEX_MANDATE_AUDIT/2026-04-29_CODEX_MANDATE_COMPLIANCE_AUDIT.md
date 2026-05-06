# 2026-04-29 - CODEX Mandate Compliance Audit
Date: 2026-04-29

Status: PENDING VERIFICATION
Author: Codex
Scope: static audit plus current Unity Editor readback where reachable

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

- Static scan of first-party code under `Assets/_Project`
- Coverage: `1010` C# files under `Assets/_Project`, `970` under `Assets/_Project/Scripts`
- Pattern scans for ownership, event buses, tick usage, force application, job barriers, asset streaming, forbidden scene search, and UI text mutation
- Direct source readback of high-risk owners
- Current Unity MCP readback for scene/build/console state

## Executive Summary

The project still drifts against several mandates, but the drift is not the same as earlier same-day audit text claimed.

Current highest-confidence compliance picture:

1. `GlobalRegistry` is real, but not yet the sole runtime authority. Singleton-style accessors and split bootstrap ownership still remain.
2. The mandated queue-backed event model exists in key first-party buses, but the project is still mixed rather than converged.
3. Asset streaming governance exists and is partially wired, but project-wide heavy-asset closure is not runtime-proven.
4. Zero-GC UI compliance is uneven. Core HUD paths are stronger than the long-tail UI layer.
5. Direct gameplay-side `AddForce` / `AddTorque` bypass was not found in the current global scan; `PhysicsApplySystem` is the active first-party application owner.
6. Explicit Unity loop ownership still exists outside the dispatcher ideal, and the project remains structurally oversized in world/player/UI owners.

This is still systemic debt.
It is simply not the same debt profile as the older broken-compile / direct-force-bypass narrative.

## Confirmed Findings

### 1. GlobalRegistry architecture is real, but not sovereign

Mandate pressure:

- `AGENTS.md`: managers should converge on `GlobalRegistry` ownership and explicit bootstrap sequencing

Evidence:

- `Bootstrap/BootstrapController.cs`, `Bootstrap/GameBootstrapper.cs`, and `SceneBootstrap.cs` all participate in startup authority
- singleton/DDOL style access remains visible in bootstrap- and persistence-facing systems
- direct registry/service ownership also exists and is non-trivial

What is objectively missing:

- one uncontested startup sovereign
- a hard rule boundary separating compatibility accessors from final architecture

Impact:

- initialization order remains vulnerable to drift
- lifetime ownership remains distributed across several systems

### 2. Event-bus compliance is mixed, not absent

Mandate pressure:

- `AGENTS.md`: event buses should be queue-backed, burst-safe, and flushed on the main thread

Evidence:

- `SaveEvents`, `QuestEvents`, `ScanEvents`, `NarrativeEvents`, and `AudioLogEvents` are queue-backed with `NativeQueue<T>` payload lanes
- direct static delegate buses still remain in `InteractionEvents`, `CraftingEvents`, `PDAEvents`, `FlashlightEvents`, `RandomEventEvents`, and `HectonSubmarineOsEvents`
- broad static `Action` event declarations are still numerous in first-party scripts

What is objectively missing:

- one converged event policy across gameplay/runtime layers
- elimination or hard containment of direct static delegate buses in mandate-sensitive lanes

Impact:

- publish/flush semantics remain inconsistent
- ordering and lifetime rules are harder to reason about than the mandate intends

### 3. Asset streaming architecture is partially compliant

Mandate pressure:

- heavy assets should route through async Addressables lifecycle ownership

Evidence:

- `Optimization/AssetLifecycleGovernor.cs` and `Optimization/AssetLoadDispatcher.cs` are real systems
- `ItemCatalog.cs` consumes ready tickets and calls `LoadAssetAsync<GameObject>()`
- `Addressables.Release(...)` usage is present
- `AsyncLoadHelper.cs` exists as a disabled/blocked legacy path rather than an active runtime loader

What is objectively missing:

- project-wide proof that heavy world assets consistently enter and leave through one enforced runtime path
- runtime verification of memory/VRAM behavior under real loading pressure

Impact:

- streaming governance exists
- end-to-end operational trust is still unproven

### 4. Some earlier mandate violations were stale and are now removed

Current source-backed corrections:

- no current `Resources.UnloadUnusedAssets()` hit was found under `Assets/_Project/Scripts`
- no current `DG.Tweening` / `DOTween` hit was found under `Assets/_Project/Scripts`
- current global `.AddForce(` / `.AddTorque(` scan only found `PhysicsApplySystem.cs`
- current reachable Unity console readback shows `15` package-side MCP `ManageAsset` errors on `ResourceNodeTemplate_*` assets and no visible first-party compile errors

Meaning:

- earlier same-day audit slices that still described those items as current live violations had become stale
- current compliance reporting must not preserve them as active facts

### 5. Tick/update discipline is still incomplete

Mandate pressure:

- gameplay ownership should converge on dispatcher/tick systems rather than scattered Unity loops

Evidence:

- current filtered scan found `28` explicit `Update` / `LateUpdate` / `FixedUpdate` file owners outside interface noise
- owners still include `SystemDispatcher`, `GameTickManager`, `SceneBootstrap`, `SpatialAudioManager`, `EquipmentInteractionHandler`, `SuitHUDV4CanvasOverlay`, and several gameplay/runtime helpers

What is objectively missing:

- a hard, enforced exception boundary for allowed Unity loop owners
- wider convergence onto dispatcher-only cadence

Impact:

- loop ownership is more controlled than random Unity sprawl
- it is still not mandate-clean

### 6. Zero-GC UI compliance remains uneven

Mandate pressure:

- UI hot paths should avoid string churn and activation churn

Evidence:

- strong zero-GC formatting patterns are visible in `SuitHUDV4CanvasOverlay.cs`
- current `UI` + `Interaction` `SetActive(...)` scan is low (`2` hits), which is better than older audit slices claimed
- direct `.text` mutation and broader UI ownership still remain in the long-tail interface layer

What is objectively missing:

- uniform adoption of the HUD-standard formatting approach across secondary UI
- runtime profiler proof that PDA and auxiliary UI paths are allocation-clean

Impact:

- core HUD compliance is stronger than the long-tail interface layer
- UI discipline is improving, not complete

## Current Unity Readback

Current reachable editor facts:

- active scene: `02_HECTON_WORLD`
- Build Settings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`
- console: latest reachable slice is not clean; it contains package-side MCP asset-inspection errors rather than first-party compile errors

Current warning observed:

- `Assets/_Project/Scripts/World/SedimentAccumulationManager.cs(92,22)` warning `CS0414`

This is not runtime proof.
It only means the older first-party broken-compile snapshot is no longer safe as the current compliance headline.

## Bottom Judgment

Current mandate-compliance reality:

- better than the older same-day broken-compile narrative
- still materially below the project's own declared standard

Primary remaining debt:

- split bootstrap authority
- mixed event architectures
- oversized world/player/UI owners
- incomplete dispatcher convergence
- missing runtime proof for save, streaming, and UI hot paths

## Regression Model

CPU: no runtime code changed  
GC: no runtime code changed  
Memory: no runtime code changed  
Cadence: documentation-only correction  
Correctness: improved because stale violation claims were removed and current source/editor evidence replaced them

## Hot Path Impact

None. Markdown-only pass.

## Why This Version Was Kept

Kept because mandate reporting that preserves stale violations after the code moved on is itself non-compliant with the evidence standard.
