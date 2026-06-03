# Runtime Architecture / Data / Bootstrap / Telemetry Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING
Date: 2026-06-02
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 275 static suspect lines from:

- `Docs/BibleMandateAudits/1700/04_runtime_architecture_data_telemetry/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/04_runtime_architecture_data_telemetry/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/04_runtime_architecture_data_telemetry_runtime_risks.txt`

This is not boot proof, player-build proof, profiler proof, GC proof, Memory Profiler proof, black-box dump proof, scene wiring proof, or MX350 device proof. The system remains yellow until runtime artifacts prove that owner initialization, native storage, logging, scene transition, and bootstrap routes behave as documented.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 124 | The line routes through `H8Debug` or an equivalent editor/development diagnostic facade that is stripped from non-development player builds. |
| `LEGAL_COLD_PATH` | 151 | The line is bootstrap/setup/fault/teardown/owner-lifetime/native storage/cold cache code by static method review. These lines are not release-green without proof; they are only not newly registered runtime violations. |
| `FALSE_POSITIVE` | 0 | No raw line was pure grep noise; every line matched a real API or policy-sensitive runtime boundary. |
| `RUNTIME_VIOLATION` | 0 new | No new unregistered runtime architecture violation was found in these 275 lines. Existing blocker rows still bind the proof gaps. |

## Existing Blockers Still Binding This Group

- `RB-101`: `GlobalDataVault` arena relocation, generation handles, macro payload cache growth, pinned-view safety, and hot accessor non-growth proof.
- `RB-105`: `ContentRuntimeServices` Addressables, VFX prewarm, hologram proxy pool, release ledger, and black-box path proof.
- `RB-113`: direct `Debug.Log*` callsites in bootstrap/global/fatal routes must be proven dev-gated, fault-only, or black-box mirrored. `H8Debug.*` is a separate release-stripped facade.
- `RB-120`: `H8Memory` tracking table/hash-map/block-descriptor growth proof and shutdown-only owner job completion proof.
- `RB-127`: TBDR mock/fallback DataVault storage must not be normal production rendering storage.
- `RB-128`: diagnostic visualizers and fallback GPU/material resources need build/boot policy proof.
- `RB-129`: core lazy first-use native initialization for telemetry, UI state, command queues, signal lanes, watchdogs, and burst callback queues.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| All `H8Debug` / `Hecton8.Core.H8Debug` callsites in the raw runtime scan, including `H8DataBaker`, `FoveatedSimulationManager`, `ContentRuntimeServices`, `ContentLoreBinaryProvider`, `BootstrapRouteEnforcer`, `SceneGuard`, `SceneInstantiationGate`, `BootstrapRegistryCycleValidator`, `HectonLoreSystemsRoot`, `VisorRTManager`, `UIRTManager`, `PostFXRTManager`, `RenderTexturePool`, `CameraRTManager`, `AssetLifecycleGovernor`, `VRAMMonitor`, and `RenderTextureLifecycleTracker` lines | `LEGAL_EDITOR_OR_DEV_GUARDED` | `H8Debug` methods are conditionally compiled for `UNITY_EDITOR` and `DEVELOPMENT_BUILD`. The log callsites do not create release-player logging cost in a non-development build. | Build-symbol proof that release players are non-development; underlying systems still need their own runtime evidence. |
| `H8Debug.cs:21`, `:33`, `:44`, `:56`, `:67`, `:79`, `:90`, `:102` | `LEGAL_EDITOR_OR_DEV_GUARDED` | These direct `Debug.Log*` / `Debug.LogException` calls are inside the `H8Debug` conditional facade. The direct Unity logging API is real, but the facade strips non-development player calls. | Keep direct Unity logs isolated behind the facade or explicitly covered by `RB-113`. |
| `GlobalRegistry.cs:3786`, `:5617`, `:6961`, `:6975`, `:7035`, `:7108`, `:7196`, `:7251`, `:7341`, `:7366` | `LEGAL_COLD_PATH` | Method context is registration failure, unregister mismatch, ready-lock rejection, fatal leak prevention, or bootstrap dependency failure. These are not normal hot reads by static review. | `RB-113`: prove direct logs are dev/fatal/fault-only and are mirrored to black-box/diagnostic routes where release builds need evidence. |
| `BootstrapStatus.cs:338` | `LEGAL_COLD_PATH` | `SafeHaltMessage` logging is a bootstrap halt/fatal route, not gameplay cadence. | `RB-113`: boot-fatal path must have a black-box artifact and no repeated normal-frame logging. |
| `GameBootstrapper.cs:681`, `:1572`, `:2083`, `:2355`, `:3010`, `:3120`, `:3347`, `:3390`, `:3404`, `:5148`, `:5980`, `:5987`, `:5994`, `:6002`, `:6014`, `:6028`, `:6451`, `:6809`, `:6815`, `:6821`, `:6827`, `:6832`, `:6835`, `:6924`, `:6959`, `:6991`, `:7101`, `:7390`, `:7418`, `:7422`, `:7524`, `:7527`, `:7530`, `:7533`, `:7536`, `:7631`, `:7882`, `:7910`, `:8273`, `:8289` | `LEGAL_COLD_PATH` | These direct logs/exceptions are bootstrap, scene-load, Addressables prewarm, dependency, watchdog, dirty-editor-scene, background-domain, BIOS, cleanup, or failure paths by static review. | `RB-113`: prove release build policy and black-box/fatal routing. Bootstrap cannot spam direct logs during healthy gameplay. |
| `GameBootstrapper.cs:1490` | `LEGAL_COLD_PATH` | The private `Update()` exists, but method review showed it advances bootstrap recovery while `_isBootstrapComplete` is false rather than owning normal gameplay simulation. | Boot lifecycle trace proving it becomes inert after bootstrap; no private Update loop as normal runtime scheduler. |
| `GameBootstrapper.cs:1821`, `:1837`, `:1847`, `:1973`, `:1974` | `LEGAL_COLD_PATH` | Camera/light/TMP component fetches and boot text assignment are bootstrap UI/scene construction, not recurring gameplay scene search. | Bootstrap-only proof and UI prefab proof; no repeated text/hierarchy construction after handoff. |
| `SceneRuntimeService.cs:1077` | `LEGAL_COLD_PATH` | The transition dither material assignment is activation/transition overlay setup. Static review did not show it as recurring material mutation in gameplay cadence. | Scene transition resource lifecycle proof, no material instance churn, and transition stress capture. |
| `SceneRuntimeService.cs:1169`, `HectonUrpTextureRequirementsGuard.cs:180`, `PlayerRuntimeContextService.cs:1063`, `PlayerSensoryManager.cs:353`, `H8PrefabRegistry.cs:471`, `HectonLoreSystemsRoot.cs:251` | `LEGAL_COLD_PATH` | These hierarchy/component scans are prefab validation, player context/sensory rebind, scene camera guard, or bootstrap lore root checks. Static review classifies them as setup/rebind, not hot polling. | Rebind/activation counters proving the scans do not recur during steady-state gameplay; route-card proof that hot readers use cached context. |
| `CoreLowLevelUtilities.cs:279`, `:290` | `LEGAL_COLD_PATH` | `TryComplete(...)` / `TryFinalizeCompleted(...)` helper semantics avoid blocking unless the handle is complete or the caller explicitly forces completion. The helper is not itself the violation. | Callsite proof that forced completion is teardown/fault/owner-window only; no hidden same-frame schedule/readback loop. |
| `CoreLowLevelUtilities.cs:115`, `:314`, `:315`, `:316` | `LEGAL_COLD_PATH` | These are generic native payload/ring construction helpers. The code is policy-sensitive because callers can misuse it, but the helper line is owner-lifetime storage by itself. | Callsite classification, allocator owner proof, and no hot accessor allocation/growth. |
| `BurstCallback.cs:83`, `:87`, `GlobalTelemetryBus.cs:749`, `:762`, `:773`, `UIStateStore`, `FrameTimeWatchdog`, `ThreadSafeCommandQueue`, `SignalBusRuntime`, and related fixed native queue/ring/storage lines in the raw scan | `LEGAL_COLD_PATH` | These are fixed persistent storage shapes after initialization: black-box rings, snapshots, export scratch, command/signal queues, UI state arrays, and callback queues. The unresolved risk is first-use timing, not the existence of persistent owner storage. | `RB-129`: boot prewarm proof and 300-frame counters proving no first-use native allocation, queue creation, signal-lane creation, or forced dispose completion during healthy gameplay. |
| `StaticDataStore.cs:133`, `:408`, `HectonArenaAllocator.cs:175`, `:378`, `DodReplayRecorder.cs:974`, `ConnectionSplineBatchRenderer.cs:974`, `:989`, `NativeRingBuffer`, `NativeMemorySentinel`, `PreInitAssetIdMap.cs:66`, `:68`, and other native owner-allocation/free lines outside `GlobalDataVault` / `H8Memory` | `LEGAL_COLD_PATH` | These lines are owner-lifetime native storage, allocator wrappers, replay/diagnostic storage, preinit maps, or disposal/free routes. They are not proven hot by static method review. | Owner phase map, boot prewarm, native sentinel registration, leak baseline, and no post-bootstrap growth proof. |
| `GlobalDataVault.cs:716`, `:720`, `:734`, `:735`, `:739`, `:750`, `:761`, `:772`, `:789`, `:800`, `:808`, `:809`, `:810`, `:823`, `:1391`, `:3551`, `:3553`, `:3555`, `:3611`, `:3619`, `:3642`, `:3651`, `:3659`, `:3783`, `:3862`, `:3998`, `:5292` | `LEGAL_COLD_PATH` | `GlobalDataVault` is the intended cross-domain native owner. The scan lines are initial native maps/lists, metadata allocator tags, raw macro payload allocation/free, arena disposal, or fallback owner storage. They are legal only as owner storage, not as arbitrary hot read growth. | `RB-101`: arena/macro-payload growth counters, no hot accessor allocation/relocation, pinned-view safety, deferred growth evidence, and DataVault readiness proof for systems that depend on it. |
| `H8Memory.cs:2606`, `:2608`, `:2610`, `:2612`, `:2614`, `:2616`, `:2618`, `:2620`, `:2622`, `:2624`, `:2626`, `:2785`, `:3733`, `:3916`, `:4388`, `:4390`, `:4392` | `LEGAL_COLD_PATH` | `H8Memory` owns tracking maps, owner pointer/job maps, records, black-box rings, array allocation wrappers, shutdown owner completion, owner pointer lists, and tracking growth structures. The code is valid only if capacity growth and completions stay in owner/bootstrap/shutdown windows. | `RB-120`: prewarmed tracking capacity or growth counters, no gameplay resize, and shutdown-only proof for `CompleteAllOwnerJobs()`. |
| `AssetLifecycleGovernor.cs:3712` | `LEGAL_COLD_PATH` | Temp payload allocation is a cold/fault/asset lifecycle payload route by current static classification, not a steady-state gameplay allocator. | `RB-105` / `RB-114`: callsite proof that asset lifecycle payloads are async/cold/fault-only and not gameplay-frame work. |
| `SignalStormConcurrencyFuzzer1311.cs:71` and comparable fuzzer/smoke/validator storage lines in this group | `LEGAL_COLD_PATH` | Fuzzer/smoke routes are validation tooling, not release gameplay systems, but their files still need build-boundary clarity when outside obvious editor folders. | Build/CI route proof; do not ship fuzzer loops as runtime gameplay code. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 275 listed runtime architecture/data/bootstrap/telemetry static suspect lines are now classified. This does not clear the system for release. The important remaining work is proof, not more grep triage: DataVault/H8Memory growth counters, lazy native prewarm evidence, direct debug/fatal routing policy, bootstrap `Update()` inertness, scene-transition material/UI lifecycle proof, player context rebind scan counters, ContentAuthority ledger proof, and build-symbol proof for diagnostic/dev-only routes.
