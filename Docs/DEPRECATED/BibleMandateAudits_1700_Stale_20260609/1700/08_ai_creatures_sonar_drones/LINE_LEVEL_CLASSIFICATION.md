# AI / Creatures / Sonar / Drones Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING  
Date: 2026-06-02  
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 70 static suspect lines from:

- `Docs/BibleMandateAudits/1700/08_ai_creatures_sonar_drones/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/08_ai_creatures_sonar_drones/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/08_ai_creatures_sonar_drones_runtime_risks.txt`

This is not profiler proof, player-build proof, GC proof, scene wiring proof, or device proof. The system remains yellow until the required runtime artifacts exist.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 45 | The line is inside `#if UNITY_EDITOR`, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, a `[Conditional]` debug method, or the `H8Debug` compile-stripped facade. |
| `LEGAL_COLD_PATH` | 25 | The line is startup/setup/validation/fault-dump/owner-lifetime code, not proven hot by static review. These still require boot/profiler/scene proof where noted. |
| `RUNTIME_VIOLATION` | 0 new | No new unregistered AI-group violation was found in these 70 lines. Existing release blockers still apply. |
| `FALSE_POSITIVE` | 0 | All lines matched real APIs or policy-sensitive code paths. |

## Existing Blockers Still Binding This Group

- `RB-007`: fauna material clone batching and collider LOD transition proof.
- `RB-008`: ecosystem runtime installer must be bootstrap recovery, not normal scene composition.
- `RB-012`: drone mock truth and procedural material routes must be disabled/proven for release.
- `RB-017`: managed audio callback synthesis/decode and mock audio content remain P0 until removed, excluded, waived with proof, or replaced by the native/DSP route.
- `RB-106`: microfauna/scatter/readback cadence proof still applies to ambient ecology presentation.
- `RB-131`: UI/localization/input proof still applies to cross-routed tools/UI lines.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `FaunaPOI.cs:53` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The log is inside `#if UNITY_EDITOR` `OnValidate()`. | None for player runtime; editor validator remains static-only. |
| `FaunaSpeciesProfile.cs:127` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The layer-mask correction log is inside `#if UNITY_EDITOR` `OnValidate()`. | None for player runtime. |
| `PredatorCognitionDomain_Steering.cs:1765` | `LEGAL_EDITOR_OR_DEV_GUARDED` | `OOP_Movement_Scanner.Run()` is under `#if UNITY_EDITOR` and `[MenuItem]`; the direct `Debug.Log` does not enter player builds. | Keep scanner editor-only. |
| `CreatureDamageManager.cs:229` | `LEGAL_COLD_PATH` | `RefreshBounds()` clears a reusable renderer list and calls `GetComponentsInChildren` for cached bounds/reference refresh. It is not shown as a per-frame scene search in the reviewed method context. | Prove refresh cadence under damage/pool/enable stress; no repeated hierarchy scans during combat hot path. |
| `FaunaBrain.cs:4425` | `LEGAL_COLD_PATH` | `CacheBiolumPresentationLights()` is called during `Awake()` bootstrap with a reusable scratch list. | Spawn/pool proof that the cache is not repeatedly rebuilt during normal behavior. |
| `FaunaBrain.cs:5063` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Slow-tick watchdog logging is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. | Runtime slow-tick backlog still needs black-box/profiler counters, but the log line is release-stripped. |
| `FaunaBrain.cs:6121` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Feed-event debug logging is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. | None for release log boundary. |
| `FaunaBrain.cs:7434` | `LEGAL_COLD_PATH` | `CacheLogicalLodComponents()` caches colliders in startup/pool setup and stores them for later toggles. | Collider enable/disable LOD transition proof under crowd stress; RB-007 remains open. |
| `FaunaBrain.cs:7756` | `LEGAL_COLD_PATH` | `ValidatePrimitiveColliderRig()` checks for forbidden `MeshCollider` during startup validation. It enforces the collision-proxy law rather than assigning a visual mesh collider. | Authoring proof that fauna prefabs ship with primitive colliders only. |
| `FaunaBrain.cs:7760`, `FaunaBrain.cs:7772` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The validation errors are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. | Runtime prefab validation artifact still required, but release logging is stripped. |
| `EcosystemRuntimeInstaller.cs:24`, `:27`, `:30`, `:33` | `LEGAL_COLD_PATH` | `TryGetComponent` checks happen inside `EnsureRuntimeSystems()` on a single scene-level runtime root. This is bootstrap recovery code, not a search loop. | RB-008: authored ecosystem runtime prefab or boot manifest proof that dynamic AddComponent recovery is not normal release composition. |
| `MacroEcosystemMathematicianRuntime_SHINOBU300_Audit.cs:121`, `:123` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Self-audit menu logs are inside `#if UNITY_EDITOR` and `[MenuItem]`. | None for player runtime. |
| `SceneTransitionVerifier.cs:162`, `:198` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Exception/report logging is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD` and/or `[Conditional]` helpers. | Keep verifier out of release scenes unless explicitly development-gated. |
| `PauseSystemVerifier.cs:217`, `:400`, `:410` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Exception/report/pause logging is development/editor gated by preprocessor and conditional helpers. | Keep verifier as dev tooling only. |
| `WfcLaserCutRuntime.cs:623` | `LEGAL_COLD_PATH` | `NativeArray<byte>(Allocator.Temp)` is in `DumpBlackBox(...)` fault/export code, not normal tool cutting. | Fault-dump trigger proof; no gameplay-frame dump spam. |
| `PerformanceMonitor.cs:360`, `:367`, `:371`, `:376` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Capture logging is inside `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; `H8Debug` also compile-strips release calls. | Performance monitor must stay dev/capture-only in release. |
| `LaserCutterDodRuntime.cs:1077` | `LEGAL_COLD_PATH` | `NativeArray<byte>(Allocator.Temp)` is in telemetry black-box dump export. | Fault-dump trigger proof; no normal-frame dump path. |
| `StateRecoveryVerifier.cs:158`, `:520` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Exception/report logging is editor/development gated and uses conditional helpers. | Keep verifier as dev tooling only. |
| `PerformanceBudgetController.cs:651`, `:656`, `:661`, `:666`, `:671`, `:676`, `:683`, `:693`, `:698` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The entire logging block is under `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. | Runtime budget state still needs telemetry/profiler artifacts, but these log calls are release-stripped. |
| `ToolKinematicsRuntime.cs:957` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Equipment stats CSV copy is inside `#if UNITY_EDITOR` cold authoring import logic. | None for player runtime. |
| `ToolKinematicsRuntime.cs:1225` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The ABI mismatch report uses `H8Debug.LogError`, whose method calls are compile-stripped outside editor/development builds. | ABI layout proof still needed in CI/runtime docs; log line is not a release hot-path allocation. |
| `AdaptiveStemAudioMixer.cs:1315`, `:1319` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Fault warnings use `H8Debug.LogWarning`, which is compile-stripped in release. The dump path itself is a fault/export path. | Audio telemetry dump artifact and no spam proof remain in audio domain. |
| `AdaptiveStemAudioMixer.cs:1372`, `:1376` | `LEGAL_EDITOR_OR_DEV_GUARDED` | CSV rule polling and parse warnings are under `#if UNITY_EDITOR` and use compile-stripped logging. | None for player runtime; release must not parse loose CSV. |
| `NativeAudioFrameRingBuffer.cs:506`, `:515`, `:527`, `:539` | `LEGAL_COLD_PATH` | Persistent native bridge buffers are allocated through `H8Memory.AllocateRaw(...)` in owner initialization, not in the audio callback line. | Native bridge registration, capacity, sentinel/H8Memory, shutdown, and underrun proof; RB-017 still governs audio route. |
| `NativeAudioFrameRingBuffer.cs:565`, `:571`, `:577`, `:583` | `LEGAL_COLD_PATH` | `H8Memory.FreeRaw(...)` occurs in owner release/shutdown of the native bridge buffers. | Leak baseline and teardown proof after scene unload. |
| `HectonMusicDirector.cs:907`, `:917` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Missing config/prefab errors are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. | Authored config/prefab proof still required; release log line is stripped. |
| `ProceduralAudioEvents.cs:1267` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Exception dispatch logging is behind conditional dev/editor helper. | Audio listener exception handling still needs dev proof only. |
| `VocalBankPlaybackRuntime.cs:306` | `LEGAL_COLD_PATH` | `TryGetComponent<AudioListener>` is part of `RejectInvalidAudioFilterHostCold()` during enable/host validation. | RB-017 still blocks managed audio callback/decode route; this line is not the main violation. |
| `VocalBankPlaybackRuntime.cs:1355`, `:1367` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Editor CSV scratch allocation/free belongs to the `#if UNITY_EDITOR` mutation/import route. | None for player runtime. |
| `DynamicMusicGranularSynthesizer.cs:681` | `LEGAL_COLD_PATH` | `TryGetComponent<AudioListener>` is host validation during enable. | RB-017 still blocks managed `OnAudioFilterRead` production route; this line is only cold host validation. |
| `PlayerCriticalProceduralAudioRenderer.cs:3493`, `:4514`, `:4561`, `:4582`, `:8763`, `:8783` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Warnings/errors are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; the native bridge and reverb fallback paths remain proof-gated separately. | Native output bridge, mixer binding, producer thread, and reverb fallback proof remain in audio reports. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

The 70 listed AI-group static suspect lines are now classified. This does not clear the group for release because the important acceptance gates are not text lines alone: fauna crowd material proof, authored ecosystem boot proof, sonar truth/cadence proof, drone mock-route exclusion, managed audio callback closure, native audio bridge proof, and 300-frame telemetry/profiler artifacts are still missing.

