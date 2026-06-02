# Rationale 1627 - SCENARIOS_AND_CAMPAIGN_00_MIGRATION_VALIDATOR

Date: 2026-06-01
Evidence State: PENDING VERIFICATION

## Decision 001 - Validation First, No Scene Mutation
Problem: The task demands scene/prefab readiness for `02_HECTON_WORLD` while multiple agents are editing concurrently. Direct scene mutation can corrupt authored world data or hide defects.
Solution: Build an Editor-only validator in `Assets/_Project/Editor/QA/` and use raw YAML only for static detection, not repair. Any repair path must be non-destructive Unity Editor API and opt-in by explicit method.
Rejected Alternatives: Blind YAML find/replace was rejected because FileID/GUID/property alignment cannot be assumed. PlayMode-first validation was rejected because missing scripts can abort before useful diagnostics.
Scalability potential: Low tier gains from avoiding PlayMode churn and import stalls; middle tier gets deterministic static preflight; high tier can run broader prefab sweeps; ultra tier can add deeper report hashing without changing gameplay truth.
Hardware Impact: i3/MX350 avoids heavy compilation and PlayMode churn during early passes. Estimated gain: prevents multi-second editor stalls; frame microseconds not claimed.

## Decision 002 - Evidence Class Discipline
Problem: Static scans can prove text and serialized references, not runtime health.
Solution: Every report entry will carry evidence class: STATIC_SOURCE, STATIC_DOC, UNITY_CONSOLE, PLAYMODE, PROFILER, or CLI_COMPILE.
Rejected Alternatives: "Scene ready" prose was rejected. Readiness without Unity Console/PlayMode remains PENDING VERIFICATION.
Scalability potential: Low/middle/high/ultra tiers receive the same truth model; richer devices only expand validation depth and hashing.
Hardware Impact: No runtime cost. Editor CPU saved by avoiding unnecessary dotnet build.

## Decision 003 - Build Throttle
Problem: dotnet build can steal CPU from concurrent agent cluster and was explicitly restricted by the user.
Solution: Use static source review first. Only run build after checking CPU <= 50% and no `dotnet`/`csc` contention, and only if the Editor validator cannot be syntax-verified otherwise.
Rejected Alternatives: Routine build after every edit rejected as host-hostile and unnecessary.
Scalability potential: Cheap devices avoid compile contention; high-end machines can run final validation if contention is absent.
Hardware Impact: i3/MX350 avoids sustained CPU saturation. Estimated gain: build minutes avoided, no runtime microsecond claim.

## Decision 004 - Package Script GUID Reclassification
Problem: Static GUID indexing cannot resolve package/built-in MonoScript GUIDs such as URP, Unity UI, Crest, MapMagic, Den.Tools, and VolumetricLightBeam when their `.meta` files are not under `Assets/Packages/ProjectSettings`.
Solution: Classify unresolved script GUIDs with `m_EditorClassIdentifier` rooted in known package namespaces as external package scripts, then reserve hard failure for `m_Script fileID:0`, null Editor API components, or unresolved non-package project GUIDs.
Rejected Alternatives: Treating every absent `.meta` as a missing script was rejected because it falsely flagged 14 scene URP components and 36 prefab Unity UI components.
Scalability potential: Low tier avoids false-positive repair churn; middle tier gets deterministic sweep; high/ultra can add broader package allowlists without touching runtime.
Hardware Impact: i3/MX350 avoids unnecessary Unity import/repair passes. Estimated gain: avoids multi-second scene/prefab reload churn; no frame microsecond claim.

## Decision 005 - SceneGuard Is a Real Finding, Not a Silent Fix
Problem: `SceneGuard.cs` exists, but `02_HECTON_WORLD.unity` contains no SceneGuard GUID and the only local camera object is `World_Observer_Camera`, untagged and disabled.
Solution: Validator will fail this route explicitly and provide a Unity Editor API repair path instead of hand-editing YAML. Primary defect is missing guard serialization, not missing script.
Rejected Alternatives: Blind YAML component insertion was rejected because scene ownership is shared with 20+ agents and raw FileID insertion can collide or bypass prefab/Undo bookkeeping.
Scalability potential: All device tiers benefit from deterministic boot route enforcement; high/ultra visuals do not matter if unauthorized scene entry skips bootstrap truth.
Hardware Impact: i3/MX350 gains by catching the defect before PlayMode. Estimated saved cost: one failed PlayMode boot cycle per validation run.

## Decision 006 - Campaign 00 Route Scope
Problem: Assignment mentions laser cutter and battery cells, while current first-20-minutes route brief parks scanner/repair tool proof and names Copper Wire as V0.
Solution: Validator will require Copper route assets as fatal gates and include laser cutter/battery/tool metadata as Campaign 00 extended-content gates. It will not claim scanner/repair route readiness without matching route contract.
Rejected Alternatives: Forcing stale broader route assumptions into the gate was rejected because it can break a valid Copper Wire V0 proof.
Scalability potential: Low tier starts with cheap Copper route; middle/high/ultra can expand route checks as content is promoted without changing validator authority.
Hardware Impact: No runtime cost. Editor-only static checks avoid unnecessary monolith bake unless missing data is proven.

## Decision 007 - Raw SceneGuard YAML Patch [SUPERSEDED]
Problem: Static scan proved `SceneGuard` is absent from `02_HECTON_WORLD.unity`, while the assignment requires direct world-scene entry protection on the main/world camera.
Solution: Superseded. Initial text-scene patch was discarded after proving that the repository scene is Unity binary serialization. Final repair route is Decision 014.
Rejected Alternatives: Continuing raw YAML patching after binary serialization was discovered was rejected as scene corruption.
Scalability potential: Low tier avoids booting a heavy world scene outside bootstrap; middle/high/ultra tiers preserve the same authority route while visual fidelity scales independently.
Hardware Impact: i3/MX350 avoids direct-world PlayMode crash/reload churn. Estimated saved cost: one failed scene load cycle; no runtime frame microsecond claim.

## Decision 008 - Build Blocked By Host Contention
Problem: Task 15 authorizes a single build only below 50% CPU and without compiler contention; user explicitly forbids routine dotnet builds.
Solution: Sampled host state: CPU total 66.5%, active `dotnet` processes PIDs 15112 and 25728. Marked compile verification blocked by contention and used static AST/source review instead.
Rejected Alternatives: Running dotnet build under load was rejected because it violates both user and batch constraints.
Scalability potential: Cheap devices remain responsive; high-end devices can run final build later when contention clears.
Hardware Impact: i3/MX350 avoids sustained CPU saturation. Build cost deferred; no runtime microsecond claim.

## Decision 009 - Source-Only APEX Evidence
Problem: The previous validator carried a JSON report writer and SHA fields, but the current APEX directive rejects report files and treats optimized C# plus clean serialized scene state as proof.
Solution: Removed `ReportPath`, `JsonUtility`, SHA hashing, and `writeReport` flow. Validator now returns `SceneIntegrityValidationResult` in memory and logs one concise Unity Console line with `sourceOnly=1`.
Rejected Alternatives: Keeping JSON for convenience was rejected because it adds cold I/O and contradicts the current proof contract.
Scalability potential: Low tier avoids extra filesystem writes; middle tier keeps deterministic Editor validation; high/ultra tiers can run deeper checks through the same C# counters without changing artifact policy.
Hardware Impact: i3/MX350 saves cold disk/CPU overhead from hashing and report serialization. Estimated saved cost: sub-millisecond to multi-millisecond editor I/O per run depending on disk state; no player-frame claim.

## Decision 010 - APEX Static Contract Scanner
Problem: Scene readiness is not enough if hot loops reacquire dependencies, presentation writes leak into simulation phases, or DataVault write locks nest.
Solution: Added source-only scanner gates for hot `GlobalRegistry`/`GetComponent`/scene search calls, presentation writes outside `LateFrameTick`, and DataVault write/buffer lock acquisition without single-lock/try-finally discipline. Added EditMode test hooks for hot dependency and nested lock detection.
Rejected Alternatives: Blind `rg` over full files was rejected because it crosses method bodies and produces false positives. Runtime instrumentation was rejected for this pass because Unity import/compile is still blocked.
Scalability potential: Low devices avoid hidden per-frame lookups and deadlock stalls; middle devices preserve deterministic phase order; high and ultra devices can spend saved frame time on visual overkill without changing authority routes.
Hardware Impact: i3/MX350 gains by rejecting hot dependency lookups before PlayMode. Estimated saved cost per violation: one scene search or component lookup per frame, plus deadlock risk removed by lock flattening.

## Decision 011 - DataVault Lock Scanner Precision
Problem: `GraphicsBuffer.LockBufferForWrite<T>` is not a DataVault write lock. Treating GPU upload mapping as a DataVault lock would generate false fatal findings and bury the actual deadlock class.
Solution: Narrowed the DataVault lock scanner to `.TryAcquireWriteLock(`, `.AcquireWriteLock(`, and `.TryLockBuffer(` with matching `.ReleaseWriteLock(` or `.TryUnlockBuffer(`. Added tests for nested DataVault locks, valid single-lock `try/finally`, and cold `Awake` component lookup.
Rejected Alternatives: Keeping `LockBufferForWrite<T>` in the DataVault scanner was rejected because presentation/GPU mapping has a separate ownership model and must be audited separately from DataVault lock flattening.
Scalability potential: Low tier avoids false-positive validation noise; middle tier gets exact deadlock-class rejection; high/ultra tiers keep GPU upload flexibility while DataVault authority remains strict.
Hardware Impact: i3/MX350 avoids wasted validator triage and keeps actual write-lock defects visible. Runtime frame cost unchanged; this is Editor-only gate precision.

## Decision 012 - VISUAL_SYNC Phase Recognition
Problem: The APEX directive permits presentation work in `LateFrameTick` or `VISUAL_SYNC`. The validator originally named only `LateFrameTick`, leaving `VisualSyncTick` implicit.
Solution: Added `VisualSyncTick` to the hot method scan. Dependency lookups remain fatal in `VisualSyncTick`, while presentation writes are allowed there as a legitimate visual-sync phase. Added a test proving `GlobalRegistry.Get<T>` inside `VisualSyncTick` is rejected.
Rejected Alternatives: Treating `VisualSyncTick` as entirely cold was rejected because it can run per-frame. Treating its presentation writes as fatal was rejected because it is the explicit visual synchronization lane.
Scalability potential: Low/middle/high/ultra tiers keep the same phase contract: simulation resolves first, visual overkill happens after state settles.
Hardware Impact: Prevents per-frame dependency lookups in the visual lane. No runtime cost from the validator; it is Editor-only source enforcement.

## Decision 013 - Presentation Phase Regression Hook
Problem: The validator counted presentation phase violations internally, but tests could not directly prove the two required cases: shader/global presentation writes forbidden in `Update` and allowed in `VisualSyncTick`.
Solution: Added `CountPresentationPhaseViolationsForTest` plus two EditMode source tests. The same scanner now rejects `Shader.SetGlobalFloat` inside `Update` and accepts the same write inside `VisualSyncTick`.
Rejected Alternatives: Relying on prose in the final report was rejected because APEX requires source-level proof. Running PlayMode to observe a phase ordering side effect was rejected under current compiler contention and because a static source contract is the cheaper gate.
Scalability potential: Low tier avoids simulation-phase material churn; middle tier gets stable visual sync cadence; high/ultra tiers can spend saved simulation time on visual overkill after state settles.
Hardware Impact: Editor-only cost. Estimated saved runtime cost per prevented leak: one shader/global material write outside visual sync per frame; no exact frame microseconds claimed without profiler.

## Decision 014 - Binary SceneGuard Repair Through Unity API
Problem: `02_HECTON_WORLD.unity` in `HEAD` is Unity binary serialization. The temporary text working copy was 695 KB and could not be treated as authoritative YAML; raw text mutation would either corrupt binary scene data or erase real world content.
Solution: Restored the scene from the `HEAD` binary blob, loaded it additively through Unity MCP, set it active for inspection, added `Hecton8.Guardian.SceneGuard` to root `Camera`, set `_enforceBootstrap=true`, saved the scene, verified the component readback, then restored active scene to `00_BOOTSTRAP` and unloaded `02_HECTON_WORLD`.
Rejected Alternatives: Keeping the 695 KB text scene was rejected because it was not the repository's binary world scene. Repeating the raw YAML component insertion was rejected because the final scene is not text YAML.
Scalability potential: All device tiers preserve the same bootstrap guard route. Low tier avoids direct-world entry faults; middle/high/ultra tiers can scale visuals without bypassing bootstrap ownership.
Hardware Impact: Editor-only scene mutation. i3/MX350 avoids PlayMode failure/reload churn. No player-frame microseconds claimed.

## Decision 015 - Player Missing Script Shell Removal
Problem: Unity validation of active `02_HECTON_WORLD` found 2 missing script shells on `--- GAMEPLAY ---/Player`. Static text probing had missed this because the scene is binary serialized and not reliable as YAML text.
Solution: Used Unity's scene validator with `auto_repair=true` while `02_HECTON_WORLD` was active. It removed exactly 2 missing script shells. Saved the active world scene and repeated validation; result was `totalIssues=0`, `missingScripts=0`, `brokenPrefabs=0`.
Rejected Alternatives: GUID remap was rejected because the missing shells had no resolvable script identity from the binary scene path. Manual binary mutation was rejected as corruption.
Scalability potential: Low/middle/high/ultra tiers all benefit from eliminating dead serialized components before PlayMode. The player route remains authoritative; no gameplay truth was changed.
Hardware Impact: Editor-only cleanup. Avoids runtime deserialization warnings and possible component iteration null branches; no player-frame microseconds claimed without profiler.

## Decision 016 - Diagnostic Scene Leak Probe Without Console Pollution
Problem: `MemorySecurityAudit1616.RunMockLeakDetectionProbe` intentionally creates a scene-lifetime native allocation to verify sentinel fail-closed behavior, but the production assert path still emitted `CRITICAL_MEMORY_VIOLATION` into Unity Console. That made an expected editor stress probe indistinguishable from a real scene unload leak.
Solution: Added `NativeMemorySentinel.AssertNoSceneLifetimeAllocationsForDiagnostics(string context)`. It uses the same allocation count and fatal message path, but does not call `ReportSceneLifetimeLeaks`, does not publish telemetry, does not mutate `LeakReported`, and does not write a Console error. The editor stress probe now calls this diagnostic-only API.
Rejected Alternatives: Clearing the console was rejected because it hides evidence. Weakening `PublishSceneLifetimeLeak` was rejected because real scene unload leaks must remain loud. Keeping suppression scopes was rejected because the actual console stack proved they were insufficient as a proof surface.
Scalability potential: Low/middle/high/ultra tiers keep the same runtime leak enforcement. Editor validation becomes cleaner and more deterministic without changing gameplay memory ownership.
Hardware Impact: Editor-only change. Avoids false critical console noise and repeated investigation cycles; no player-frame microseconds claimed.
