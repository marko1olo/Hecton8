# AUDIT_NATIVE_STATE Log

## 2026-05-26 Native Ownership Audit

What was wrong:
- Baseline ledger `VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json` recorded 2421 scanned files, 7324 native field declarations, 1770 forbidden persistent candidates, 358 forbidden MonoBehaviour candidates, 5490 transient job fields, 19 stack-only view fields, 45 core-memory-allowed fields, and 865 raw pointer fields.
- Current latest full ledger reviewed, `VAULT_NATIVE_ALIAS_LEDGER_1315_PASS22.json`, records 2438 scanned files, 6651 native field declarations, 837 forbidden persistent candidates, 73 forbidden MonoBehaviour candidates, 5481 transient job fields, 288 stack-only view fields, 45 core-memory-allowed fields, and 861 raw pointer fields.
- Real progress exists: forbidden persistent candidates dropped by 933 and MonoBehaviour candidates dropped by 285. This is static ledger progress, not runtime proof.
- The project is not clean. Residual MonoBehaviour/native collection violations remain in `SaveManager.cs`, `Gameplay/ContextualPhysicalIkRig.cs`, `Graphics/Culling/TBDRPipelineSurgeonRuntime.cs`, `World/FloraRegrowthDirector.cs`, `Gameplay/ContextualPhysicalIkRuntime.cs`, `ConstructionManager.cs`, and `World/AbyssalThermalManager.cs`.
- Top remaining forbidden persistent groups in the latest ledger include `ModularEquipmentEngine.cs` 28, `Gameplay/Combat/CombatDamageRuntime.cs` 24, `QA/Headless/JacobiStressFuzzer/PowerGridJacobiStressFuzzer.cs` 20, `Gameplay/ScannerDataMiningRouter.cs` 20, `Construction/ShinobuSocketConstructionData.cs` 19, `Gameplay/Combat/HectonCombatRuntime_ArmorPenetration.cs` 19, `WorldProceduralScatterWorkingMemory.cs` 18, `SaveSystem/EntityDeltaCompressionArchitecture.cs` 18, `Physiology/ShinobuRespawnReconciliationRuntime.cs` 16, `SaveSystem/VoxelDeltaCompressionArchitecture.cs` 16, and `Quest/QuestDagRuntimeTypes.cs` 16.
- Broad build state is unverified for this audit. Fresh compile was not launched because CPU sampled at 100 percent and project rules forbid `dotnet build` while system CPU is over 50 percent or another compiler is active. No runtime/profiler claim is made.

What was done:
- Read project authority files and selected native-memory mandates: native collections/job system protocol, zero-GC policy, GlobalRegistry/DI, signal lane segregation, runtime struct layout, telemetry/crash reporting, execution phases, arena allocator, and version 6.0 doctrine.
- Extracted current batch context from `Docs/Tasks/CURRENT_BATCH.md` and reviewed agent statuses/logs/reports for 1315 through 1329.
- Compared baseline ledger against latest available pass22 ledger.
- Spot-checked source files for current truth: `HectonVoxelEngine.cs`, `World/VegetationMemoryPool.cs`, `Gameplay/ContextualPhysicalIkRig.cs`, `SaveManager.cs`, `World/AbyssalThermalManager.cs`, `ConstructionManager.cs`, `EncounterDirector.cs`, `World/HectonSpatialHash.cs`, `PersistentWorldRegistry.cs`, `Core/GlobalDataVault.cs`, `Core/NativeMemorySentinel.cs`, and signal lane usage.
- Spawned two subagents for source/log cross-checks. Both failed to return within 120 seconds and were shut down. Their output was not used as evidence.

Cinematic Cheats used:
- None. This was an audit, not a simulation or rendering implementation.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No profiler run was performed.
- Static cleanup delta indicates lower leak/stall risk: forbidden persistent native candidates reduced by 933; forbidden MonoBehaviour native candidates reduced by 285.
- Any frame-time savings claim would be fake without fresh Unity profiler evidence.

Final classification:
- Evidence level: STATIC_SOURCE plus STATIC_ROSLYN_LEDGER plus STATUS_LOG_REVIEW.
- Cleaned domains with credible static target proof: voxel primary target, inventory, audio, gas/atmosphere target, fluid target, fabricator target, procedural wreck target, and several scoped exorcism files.
- Dirty remaining state: global project still has 837 forbidden persistent candidates and 73 MonoBehaviour candidates in latest full ledger.
- Current compile state: unknown. Build not run due CPU gate.

## 2026-05-26 Agent Herd Snapshot

What the active batch is doing:
- 1315-1329 are mostly memory sovereignty/native ownership exorcists by domain. They are removing persistent NativeContainer ownership from runtime managers and replacing it with Vault handles, transient views, fixed managed rings, scoped write locks, and fail-closed paths.
- 1330 is data monolith/static data bake validation.
- 1331 is workspace hygiene and stray asset/meta purge.
- 1332 is input contract/rebind UI architecture.
- 1333 is compute shader dispatch sizing and dynamic-query purge.
- 1334 is documentation consolidation/fluff purge.
- 1335 is VR comfort, visor, brownout, AR stencil, RenderGraph/foveated/XR safety.
- 1336 is shader warmup, variant collections, bootstrap PSO failure handling, and black-box dump hardening.
- 1337 is physics culling, sleep/wake, DTO layout, stale cleanup barriers, and FixedTick scan reduction.

Observed correctness:
- The agents are generally aligned with the stated doctrine: most recent logs show repeated prompt re-extraction, mandate/rationale rereads, domain-bounded edits, static scanners, `git diff --check`, and explicit refusal to claim green builds when CPU/build gates block verification.
- Several agents are doing useful defect-finding beyond mechanical scanner cleanup: 1316 fixed bad monotonic spatial handles after removing native queues; 1328 found mesh data disposal and AUP terrain snap issues; 1335 found RenderGraph/global state defects; 1337 found wake/FixedTick O(N) scan debt and AUP publication space errors.
- Weak point: global compile truth is fragmented. Many reports are scoped green or static green while full project compilation is red, blocked, timed out, or failed outside the agent's domain. Current "works" claims must be treated as scoped until a full clean build and Unity runtime pass exist.

Current risk:
- Parallelism is producing real cleanup but also foreign compile walls. Several agents correctly refuse to edit outside domain, so cross-domain breaks persist until the owner/integrator resolves them.
- CPU gate is repeatedly blocking valid build verification. This prevents fake reports, but it also means many latest patches are not compiled.
- Some older green builds are stale because newer agents changed files after those builds.

Exact Microseconds saved:
- Herd snapshot itself saved 0 us runtime.
- Claimed per-agent microsecond savings are not globally trusted unless backed by profiler/runtime artifacts. Static scan reductions and O(N) removal proofs are credible as architecture cleanup, not measured frame-time wins.

## 2026-05-27 Current NativeArray/MonoBehaviour Snapshot

What was wrong:
- The project started this cleanup window with `1770` forbidden persistent native candidates and `358` forbidden MonoBehaviour native candidates in `VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json` (`2421` files, `0` parse failures, modified 2026-05-26 00:55:25).
- Latest comparable full all-scripts proof is `VAULT_NATIVE_ALIAS_LEDGER_1325_DEEP_REAUDIT32_ALLSCRIPTS.json` (`2441` files, `0` parse failures, modified 2026-05-27 13:27:37): `6678` native fields, `819` forbidden persistent candidates, `47` forbidden MonoBehaviour candidates, `5497` allowed transient job fields, `317` stack-only ref-struct views, `45` core-memory allowed fields, `864` raw pointer fields.
- Current MonoBehaviour residuals are concentrated in four files: `SaveManager.cs` 13, `Gameplay/ContextualPhysicalIkRig.cs` 13, `Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` 11, `Gameplay/ContextualPhysicalIkRuntime.cs` 10.
- Current top broader persistent groups are `ModularEquipmentEngine.cs` 28, `CombatDamageRuntime.cs` 24, `PowerGridJacobiStressFuzzer.cs` 20, `PlayerInventory.cs` 20, `ScannerDataMiningRouter.cs` 20, `HectonCombatRuntime_ArmorPenetration.cs` 19, `ShinobuSocketConstructionData.cs` 19, `EntityDeltaCompressionArchitecture.cs` 18, and `WorldProceduralScatterWorkingMemory.cs` 18.

What was done:
- Compared only timestamped full ledgers with `0` parse failures. Rejected stale/non-comparable `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` because UNKNOWN rationale already documents it as contradicted by source lines.
- Calculated cleanup rates:
  - 2026-05-26 00:55 -> 2026-05-26 20:04: persistent `1770 -> 837` (`-933`, `48.71/hour`), MonoBehaviour `358 -> 73` (`-285`, `14.88/hour`).
  - 2026-05-26 20:04 -> 2026-05-27 08:58: persistent `837 -> 809` (`-28`, `2.17/hour`), MonoBehaviour `73 -> 56` (`-17`, `1.32/hour`).
  - 2026-05-27 08:58 -> 2026-05-27 13:27: persistent `809 -> 819` (`+10`, regression), MonoBehaviour `56 -> 47` (`-9`, `2.00/hour`).
  - Total 36.54h window: persistent `1770 -> 819` (`-951`, `53.7%` reduction, `26.03/hour` average), MonoBehaviour `358 -> 47` (`-311`, `86.9%` reduction, `8.51/hour` average).
- Checked 1325 domain report: primary `PersistentWorldRegistry` is static-clean (`0` native fields), world ledger has `6` broader residual candidates in `World/HectonMapMagicVegetationBridge.cs`, and `0` world MonoBehaviour candidates. SaveManager residual is explicitly external Data Archivist scope.

Cinematic Cheats used:
- None. This was static evidence review, not simulation/rendering work.

Exact Microseconds saved:
- Measured runtime savings: `0 us`. No Unity profiler or GCMonitor run occurred.
- Static risk reduction: `951` fewer forbidden persistent candidates and `311` fewer MonoBehaviour candidates versus 2026-05-26 baseline.
- Current frame-time/GC state is `PENDING VERIFICATION`. Current 1325 report says build was not launched because CPU was 100% and active compiler process count was 8. Local CPU sample during this audit was 88-99%, so no new build was launched.

Final classification:
- Success is real but static: MonoBehaviour native candidate cleanup is strong (`86.9%` removed), broad persistent cleanup is only half done (`53.7%` removed).
- Tempo has collapsed after the first purge wave. The tail is dependency-heavy and domain-owned, not a bulk-grep cleanup.
- Current state is not green. Remaining blockers: 47 MonoBehaviour candidates, 819 broad persistent candidates, no fresh Unity import/Console/PlayMode/profiler proof, no fresh full compile proof.

## 2026-05-27 14:59 +04 - Other Agent Polish Audit And Lock-Leak Repair

What was wrong:
- Last-24h agent work is large and mixed: roughly 1018 modified tracked paths, 455 deletions, and 1093 untracked paths in the broad workspace snapshot. Source diffs touch Fluid, Inventory, World, Voxel, Atmosphere, Audio, Physics, Input, Construction, shaders, scenes, and ProjectSettings.
- Most agent reports are static-source proofs, not runtime green. Common blockers: CPU/build-lane guards, active `dotnet`, timeout builds, generated-project cycles, plugin/vendor compile errors, missing Odin/Mono.Data/BufferID/duplicate-member errors, and absent Unity import/profiler/GCMonitor proof.
- A concrete harmful pattern existed in shared DataVault helpers: `TryAcquireWriteLock(...) && buffer.IsCreated && length...`. If DataVault granted the lock but validation failed, the lock could remain held.

What was done:
- Classified evidence from active logs and reports. Stronger static improvements exist in DataMonolith, World native ownership, Physics culling/apply validation, Input fail-closed paths, Compute dispatch guards, Fluid hot allocation gates, Inventory tiny-job removal, and terrain route cleanup.
- Marked full runtime acceptance as blocked, not green. Current host still has an active `dotnet.exe build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
- Patched DataVault write-lock failure paths in:
  - `Assets/_Project/Scripts/HectonFluidEngine.cs`
  - `Assets/_Project/Scripts/PlayerInventory.cs`
  - `Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs`
  - `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs`
  - `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`
  - `Assets/_Project/Scripts/Editor/EconomyRecipeTunerWindow.cs`
  - `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs`
  - `Assets/_Project/Scripts/Editor/VolumetricSiltTunerWindow.cs`
  - `Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalWaveTunerWindow.Editor.cs`

Cinematic Cheats used:
- None added. This pass removed ownership/lock risk only.

Verification:
- `rg "TryAcquireWriteLock...&&|return .*TryAcquireWriteLock" Assets/_Project/Scripts -g *.cs` now reports no chained write-lock validation patterns.
- Scoped `git diff --check` over all patched files reports only existing LF-to-CRLF warnings.
- One legal build was launched after CPU sampled 7-12% and no compiler processes were active: `dotnet build .\Hecton8.slnx --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false`.
- Build failed after 274.5s on generated-project graph errors: `MSB4006` circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`, then `CS0006` missing `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll` in `Hecton8.Core.csproj`.
- No patched file appeared in the errors-only output. Full project green remains false.

Exact Microseconds saved:
- 0 us measured. Static risk removed: write-lock leaks after failed post-acquire buffer validation in nine helper sites.

## 2026-05-27 15:40 +04 - Generated Unity Package Build Graph Shim

What was wrong:
- The full solution CLI gate is blocked by generated Unity package project graph, not by a surfaced gameplay-source error.
- `BUILD_UNKNOWN_EXEC_CARRIER_RECHECK_20260527.log` records `MSB4006` circular `ResolveProjectReferences` in `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj`, followed by `CS0006` for missing `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll` in `Hecton8.Core.csproj`.
- Static graph evidence: generated projects contain package-source `ProjectReference` edges `Unity.ShaderGraph.Editor -> Unity.RenderPipelines.Core.Editor` and `Unity.RenderPipelines.Universal.Editor -> Unity.RenderPipelines.Core.Editor`, `Unity.RenderPipelines.Universal.Runtime`, `Unity.ShaderGraph.Editor`. Matching Unity-produced DLLs already exist under `Library/ScriptAssemblies`.

What was done:
- Patched tracked `Directory.Build.targets`.
- Added a Unity package CLI shim list for `Unity.RenderPipelines.Core.Editor`, `Unity.RenderPipelines.Universal.Editor`, `Unity.RenderPipelines.Universal.Runtime`, and `Unity.ShaderGraph.Editor`.
- Added `HectonUseUnityPackageScriptAssembliesForCliReferences`: before `PrepareProjectReferences` and `ResolveProjectReferences`, Unity-package-to-Unity-package `ProjectReference` edges are replaced with explicit `Library/ScriptAssemblies/<assembly>.dll` references.
- Updated the existing WaveHarmonic and Hecton8.Core project-reference pruning targets to run before `PrepareProjectReferences`, not only before `ResolveProjectReferences`.
- Extended Hecton8.Core stale generated-output pruning from `Temp/bin/Debug` to `Temp/CodexBuild` paths.

Cinematic cheats used:
- None. This is build-infrastructure routing, not runtime simulation.

Exact microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Build lane: no measured save yet. Expected benefit is removing a false package graph wall so future compile attempts expose real source errors instead of Unity package `ResolveProjectReferences` loops.

Verification:
- `Directory.Build.targets` parses as XML.
- `git diff --check -- Directory.Build.targets` reports only the existing LF/CRLF warning.
- Static shim proof: all four Unity package ProjectReference edges have existing `Library/ScriptAssemblies` DLLs.
- Static graph proof: after applying the shim rule, the root `.csproj` ProjectReference graph reports `NO_PROJECT_REFERENCE_CYCLES_AFTER_SHIM_STATIC`.
- One legal post-shim build was launched after compiler processes cleared and CPU sampled at 42.49%: `dotnet build .\Hecton8.slnx --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false`.
- The shell timed out after 604s. Child `dotnet` PID 33480 continued, then exited; stdout/exit code were not recoverable from the timed-out tool session.
- Artifact check after exit: `Temp/CodexBuild/Unity.RenderPipelines.Core.Editor/Unity.RenderPipelines.Core.Editor.dll` exists, but `Temp/CodexBuild/Unity.ShaderGraph.Editor/Unity.ShaderGraph.Editor.dll`, `Temp/CodexBuild/Unity.RenderPipelines.Universal.Editor/Unity.RenderPipelines.Universal.Editor.dll`, `Temp/CodexBuild/Unity.RenderPipelines.Universal.Runtime/Unity.RenderPipelines.Universal.Runtime.dll`, `Temp/CodexBuild/Hecton8.Core/Hecton8.Core.dll`, and `Temp/CodexBuild/Assembly-CSharp/Assembly-CSharp.dll` are still missing.
- Full build remains not green. A later external `dotnet build Hecton8.Core.csproj --no-restore` started, so no second build was launched.

Latest native ledger refresh:
- Newest ledger observed during continued parallel work: `VAULT_NATIVE_ALIAS_LEDGER_1325_DEEP_REAUDIT34_ALLSCRIPTS.json`, modified 2026-05-27 16:05:53.
- Summary: `2441` scanned files, `0` parse failures, `6672` total native fields, `813` forbidden persistent candidates, `47` forbidden MonoBehaviour candidates, `5497` transient job fields, `317` stack-only ref-struct views, `45` core-memory allowed fields, `864` raw pointer fields.
- Delta versus `DEEP_REAUDIT32`: forbidden persistent `819 -> 813` (`-6`), forbidden MonoBehaviour `47 -> 47` (`0`), total native fields `6678 -> 6672` (`-6`).
- Remaining MonoBehaviour offenders unchanged: `SaveManager.cs` 13, `Gameplay/ContextualPhysicalIkRig.cs` 13, `Graphics/Culling/TBDRPipelineSurgeonRuntime.cs` 11, `Gameplay/ContextualPhysicalIkRuntime.cs` 10.

Terrain/World claim recheck:
- `Status_TERRAIN_AUDIT.md` itself now records the vegetation async route as `[BLOCKED BY PARALLEL OVERWRITE]`.
- Current source grep confirms this is not just stale status text: `VegetationChunkResidencyDirector.cs` has three runtime `.Run()` calls and `VegetationFlowFieldIntegrator.cs` has five runtime `.Run()` calls.
- This means that part of the other-agent polish is not complete and should not be counted as project improvement yet.

Additional fix:
- Patched `Assets/_Project/Scripts/World/EcosystemDirector.cs`: `RefreshRuntimeReferences` now reads `SargassumMicroFaunaBoids.ActiveRuntimeInstance` instead of `GlobalRegistry.SargassumMicroFauna`.
- Verification: `rg "GlobalRegistry\.SargassumMicroFauna" Assets/_Project/Scripts/World/EcosystemDirector.cs` reports no hits; scoped `git diff --check` reports only LF/CRLF warning.

## 2026-05-27 16:43 +04 - Continued Polish Audit: TBDR Native Owner And Audio Route Cache

What was wrong:
- `TBDRPipelineSurgeonRuntime.cs` was still one of the four current MonoBehaviour/native offenders: 11 direct `NativeArray<T>` fields on the MonoBehaviour.
- A naive property-only move would still expose writable `NativeArray<T>` aliases to callers.
- The fallback TBDR native arrays were persistent allocations without `NativeMemorySentinel` registration.
- `HectonMusicDirector.Instance/TryGetInstance`, `SoundscapeSystem.Instance`, and `SoundscapeSystem.TryResolveMusicDirector` still used GlobalRegistry runtime reads.

What was done:
- Moved the 11 TBDR native buffers into a private `RuntimeBufferSet` owner object.
- Exposed only `NativeArray<T>.ReadOnly` debug views from `TBDRPipelineSurgeonRuntime`.
- Registered and unregistered fallback TBDR persistent buffers with `NativeMemorySentinel`; production path remains DataVault-backed.
- Added owner-local active runtime caches for `HectonMusicDirector` and `SoundscapeSystem`.
- Changed Soundscape music resolution to use `HectonMusicDirector.TryGetInstance` instead of polling `GlobalRegistry.MusicDirector`.

Cinematic Cheats used:
- None added. Existing TBDR frustum squeeze/quality-weight behavior was preserved.

Verification:
- Source grep confirms `HectonMusicDirector.Instance` and `SoundscapeSystem.Instance` no longer match `Instance => GlobalRegistry`.
- Source grep confirms `SoundscapeSystem.TryResolveMusicDirector` no longer contains `GlobalRegistry.MusicDirector`.
- `VAULT_NATIVE_ALIAS_LEDGER_1325_DEEP_REAUDIT35_ALLSCRIPTS.json` confirms forbidden MonoBehaviour candidates dropped from 47 to 36. Remaining MonoBehaviour offenders: `SaveManager.cs` 13, `ContextualPhysicalIkRig.cs` 13, `ContextualPhysicalIkRuntime.cs` 10.
- The same ledger keeps the 11 TBDR buffers as non-MonoBehaviour `RuntimeBufferSet` persistent candidates, so memory ownership is visible rather than hidden.
- Scoped `git diff --check` over touched files reports only existing LF/CRLF warnings.
- Native ledger scanner and build were not launched: CPU sampled 65.73-98.93%, external `dotnet.exe build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` PID 62864 and `VBCSCompiler` PID 6448 were active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: `TBDRPipelineSurgeonRuntime` dropped out of the MonoBehaviour-native offender set by 11 direct fields; persistent total did not drop because the buffers still exist in a non-MonoBehaviour owner/fallback path.

## 2026-05-27 17:18 +04 - Remaining MonoBehaviour Native Owner Isolation

What was wrong:
- Latest official ledger `VAULT_NATIVE_ALIAS_LEDGER_1325_DEEP_REAUDIT35_ALLSCRIPTS.json` still had 36 forbidden MonoBehaviour native candidates.
- The entire remaining offender set was concentrated in three lifecycle-heavy files: `SaveManager.cs` 13, `ContextualPhysicalIkRig.cs` 13, `ContextualPhysicalIkRuntime.cs` 10.
- A full DataVault migration under current build guard would be unsafe: CPU later sampled 99.32%, and prior compile lanes are still not green.

What was done:
- `SaveManager.cs`: moved instance native arrays into `SaveManagerNativeBufferSet`; moved static load-candidate scratch into `StaticNativeBuffers`; ref-disposal now targets the managed owner fields.
- `ContextualPhysicalIkRig.cs`: moved 12 owned arrays plus the current target-frame read-only alias into `RigNativeBufferSet`; disposal clears the owner fields and the target-frame alias.
- `ContextualPhysicalIkRuntime.cs`: moved scheduled state, hit, double-buffer target frames, hand/foot SOA lanes, and telemetry ring into `RuntimeNativeBufferSet`; dependency-aware disposal now targets the owner fields.
- Existing allocation sites, sentinel owner names, job-data assignment, and public read-only routes were preserved.

Cinematic Cheats used:
- None. This was ownership/lifecycle isolation only.

Verification:
- Official scanner source `Tools/VaultNativeAliasRoslynAudit/Program.cs` was checked: it enumerates `FieldDeclarationSyntax`, so private properties are not counted as native field declarations.
- Scoped field-pattern scan now finds remaining native fields only in job structs or private managed owner classes, not as direct fields on these MonoBehaviour classes.
- Scoped `git diff --check` over `SaveManager.cs`, `ContextualPhysicalIkRig.cs`, and `ContextualPhysicalIkRuntime.cs` reports only existing LF/CRLF warnings.
- No official ledger rerun and no build were launched after the edit because CPU sampled 99.32-100% and external `dotnet` PIDs 30680/34476 were active; this remains static source proof, not compile/import/profiler proof.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Expected official ledger impact when rerun is possible: forbidden MonoBehaviour candidates should move `36 -> 0`; forbidden persistent candidates should remain roughly unchanged because these buffers are isolated from MonoBehaviour, not migrated to `GlobalDataVault`.

## 2026-05-27 18:05 +04 - Native Facade Correction, Save Buffer Isolation, Singleton Route Sweep

What was wrong:
- The remaining native owner isolation needed a contract correction: mutable `NativeArray<T>` facades must not be normal value-return properties.
- Static save repair/load helpers borrowed live manager raw/compressed buffers through `GlobalRegistry.SaveRuntime`, so static save assembly could alias active payload buffers.
- Runtime singleton accessors still polled `GlobalRegistry` directly in multiple owners, violating the cold-registry/hot-owner-route rule.

What was done:
- Converted `SaveManager`, `ContextualPhysicalIkRig`, and `ContextualPhysicalIkRuntime` native facades to `ref` returns backed by private managed owner objects.
- Changed `SaveManager.AcquireReadBuffer` and `AcquireWriteBuffers` to allocate isolated owned fallback buffers for static read/write paths.
- Replaced direct `Instance => GlobalRegistry` / `ActiveRuntimeInstance => GlobalRegistry` routes with owner-local active fields in 13 managers: `ObjectPoolManager`, `ScrapManager`, `ScanLogSystem`, `HectonSurfaceWeatherDirector`, `SuitUpgradeManager`, `AcousticZoneController`, `GameBootstrapper`, `PrefabRegistry`, `HectonDirectorAI`, `PlayerExplorationTracker`, `PlayerExpressionManager`, `PDAIntrusionManager`, `SpectrumSystem`.
- Audited `GlobalDataVault.TryGetLatestCreated` uses; current matches are editor/gizmo/diagnostic plus `SignalWardenRuntime` crash-dump fallback.

Cinematic Cheats used:
- None. This pass was ownership, save integrity, and route purity only.

Verification:
- `rg "Instance\\s*=>\\s*GlobalRegistry|ActiveRuntimeInstance\\s*=>\\s*GlobalRegistry" Assets/_Project/Scripts -g "*.cs"` now reports only `Assets/_Project/Scripts/World/DepthZoneDirector.cs:364`.
- `DepthZoneDirector.cs` remains a documented tail because a prior normal patch attempt hit invalid file encoding; no byte-level workaround was used.
- `git diff --check` over all touched source files reports only existing LF/CRLF warnings.
- `rg "TryGetLatestCreated\\(" Assets/_Project/Scripts -g "*.cs"` shows editor/gizmo/diagnostic usage and the `SignalWardenRuntime` crash-dump fallback, not a new hot runtime route.
- Fresh all-scripts Roslyn ledger and build were not run: CPU resampled at 80.74% after tool discovery, then later 26.03% with `VBCSCompiler` PID 14276 active. No green compile/import/profiler claim.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static route impact: current direct singleton registry-read accessor count dropped from 14 to 1.
- Static native-owner expected impact remains: official `forbiddenMonoBehaviourCandidates` should drop from 36 to 0 when a clean scanner lane is legal.

## 2026-05-27 18:20 +04 - DodReplay Native Owner Isolation And Official Ledger Closure

What was wrong:
- `DodReplayRecorder.cs` still declared 14 persistent `NativeArray<T>` fields directly on a `MonoBehaviour`.
- The recorder path already had `NativeMemorySentinel` registration/unregistration, but the owner shape still violated the direct MonoBehaviour native alias ban.
- The previous expected `36 -> 0` closure was not enough until a fresh all-scripts ledger existed.

What was done:
- Moved the recorder arrays into private `DodReplayNativeBufferSet`.
- Added `ref NativeArray<T>` facades so allocation, copy, disposal, and sentinel lifecycle code stayed local and unchanged in behavior.
- Left allocation labels and sentinel calls intact; this was ownership isolation, not a DataVault migration.

Cinematic Cheats used:
- None. This pass touches replay/black-box ownership only.

Verification:
- `VAULT_NATIVE_ALIAS_LEDGER_1325_DEEP_REAUDIT37_ALLSCRIPTS.json` exists with `scannedFiles=2441`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=798`, modified `2026-05-27T18:04:01`.
- Ledger progression on comparable all-scripts runs: `DEEP_REAUDIT35` forbidden MonoBehaviour `36`, `DEEP_REAUDIT36` `26`, `DEEP_REAUDIT37` `0`.
- `rg "Instance\\s*=>\\s*GlobalRegistry|ActiveRuntimeInstance\\s*=>\\s*GlobalRegistry" Assets/_Project/Scripts -g "*.cs"` still reports only `Assets/_Project/Scripts/World/DepthZoneDirector.cs:364`; that file remains blocked by invalid UTF-8 for normal patching and is not a NativeArray offender.
- `rg "NativeDisableContainerSafetyRestriction|\\.Complete\\("` over `DodReplayRecorder.cs`, `SaveManager.cs`, `ContextualPhysicalIkRig.cs`, and `ContextualPhysicalIkRuntime.cs` reports no matches.
- Scoped `git diff --check` over those four files reports only existing LF/CRLF warnings.
- No compile/import/profiler proof: CPU sampled `77%` with active `dotnet` PID `19660` and `VBCSCompiler` PID `35324`, so no competing build/scanner lane was launched by this pass.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: direct MonoBehaviour native field candidates are now `0` in the latest official all-scripts ledger.
- Remaining native debt is persistent native alias ownership (`798`) and runtime verification, not the direct MonoBehaviour field class.

## 2026-05-27 18:35 +04 - Stack-Only DataVault View Carrier Pass

What was wrong:
- Latest official ledger still had `798` forbidden persistent native alias candidates after MonoBehaviour closure.
- Several high-count entries were view carrier structs, not actual owners, but they were plain structs so the language allowed accidental storage in fields.
- `ScannerDataMiningRouter` did exactly that: `_cachedVaultViews` stored a struct containing 15 `NativeArray<T>` aliases across frames.

What was done:
- Removed `_cachedVaultViews` from `ScannerDataMiningRouter`.
- Changed scanner view reads to resolve from cached `VaultGenerationHandle<T>` values into method-scope views only.
- Converted seven private/internal DataVault view carriers to `ref struct`: `EquipmentVaultViews`, `AuxiliaryVaultViews`, `RingVaultViews`, `ArmorPenetrationVaultViews`, `AupPrecisionVaultViews`, `LadderClimbIkVaultViews`, `ScannerVaultViews`.
- Skipped public contract view structs to avoid unreviewed public API mutation.

Cinematic Cheats used:
- None. This is native alias lifetime enforcement only.

Verification:
- `rg "_cachedVaultViews"` in `ScannerDataMiningRouter.cs` reports no hits.
- Declaration grep confirms all seven targeted view carriers are now `ref struct`.
- Scoped `git diff --check` over the seven source files reports only existing LF/CRLF warnings.
- Current `DEEP_REAUDIT37` contribution from these seven carriers was 92 forbidden persistent candidates: Equipment 28, Armor 19, Scanner 15, Auxiliary 12, AUP 9, Ladder IK 5, Ring 4.
- Fresh official scanner result: `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_REFSTRUCT.json` scanned `2441` files with `0` parse failures; forbidden persistent candidates dropped `798 -> 706`; stack-only ref-struct views rose `332 -> 424`; forbidden MonoBehaviour candidates stayed `0`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: `forbiddenPersistentCandidates` dropped by 92 in the official all-scripts scanner artifact.

## 2026-05-27 18:45 +04 - Hidden Cached View Field Purge

What was wrong:
- Scanner-visible MonoBehaviour native fields were closed, but a hidden alias pattern remained: fields whose type is a non-native view struct that embeds `NativeArray<T>`.
- `PhysicalHandController` cached `VRInteractionKinematicBridgeViews` as `_kinematicBridgeViews`, keeping DataVault-resolved native aliases across frames.
- Changing public `VRInteractionKinematicBridgeViews` to `ref struct` would be a public API mutation without compile lane proof.

What was done:
- Removed `_kinematicBridgeViews` from `PhysicalHandController`.
- Added method-scope bridge view resolution from `VRInteractionKinematicBridgeVault`.
- Passed local views by `ref` into guarded kinematic stepping, telemetry, velocity-signal threshold reads, and hand-matrix writes.
- Left public `VRInteractionKinematicBridgeViews` unchanged.

Cinematic Cheats used:
- None. This pass enforces native alias lifetime only.

Verification:
- `rg "_kinematicBridgeViews" Assets/_Project/Scripts/Interaction/PhysicalHandController.cs` reports no hits.
- Project grep for field declarations matching `*Views _field` and `*VaultViews _field` reports no hits.
- Scoped `git diff --check` reports only existing LF/CRLF warnings.
- No compile/import/profiler proof yet because after scanner completion CPU sampled `76%` and `dotnet` PID `24312` was active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static risk removed: one scanner-blind cached native view field class is now absent from first-party scripts.

## 2026-05-27 20:45 +04 - Stack-Only Carrier Pass 2

What was wrong:
- The direct MonoBehaviour native field class was closed, but the persistent alias ledger still contained method-scope carriers that were plain structs.
- Plain struct carriers can later be stored in fields or collections, so they are not a hard lifetime boundary.
- Public carrier contracts and real owner buffers were mixed into the same scanner tail; treating all of them mechanically would be unsafe.

What was done:
- Converted private/internal local native carriers to `ref struct`: inventory mass/radiation/chemistry kernels, respawn `JobPointers`, voxel delta buffer set, tool kinematics buffer set, tentacle/crab IK buffer sets, retinal adaptation buffers, construction integrity graph buffers, and AUP mock entity arrays.
- Left public API carriers unchanged until a clean compile/dependency review window exists.
- Left true native owner classes unchanged where a `ref struct` conversion would be fake progress.

Cinematic Cheats used:
- None. This was native lifetime enforcement only.

Verification:
- `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_STACKONLY_2.json`: `scannedFiles=2441`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=559`, `stackOnlyRefStructViewFields=571`.
- Delta from prior official artifact: forbidden persistent `706 -> 559`, stack-only `424 -> 571`.
- Scoped `git diff --check` over edited files reports only existing LF/CRLF warnings.
- No compile/import/profiler proof: CPU later sampled `91%` and external `dotnet` PID `2448` was active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 147 fewer persistent native alias candidates in official all-scripts scanner.

## 2026-05-27 22:05 +04 - Public Transient Carrier Ref-Struct Pass

What was wrong:
- The direct MonoBehaviour native field class remained closed, but the persistent alias ledger still included public transient carrier structs with `NativeArray<T>` fields.
- These carriers were not owners, but plain public structs allowed future heap storage of Vault/native views.
- Remaining true owners (`CombatDamageRuntime`, `ScatterWorkingMemory`, scheduled fuzzer windows) need owner-route work; hiding them would be fake progress.

What was done:
- Converted six public transient carriers to `ref struct`: `EntityDeltaCompressionVaultBufferSet`, `QuestDagBuffers`, `ApexBrainVaultBuffers`, `UtilityAICognitionVaultBuffers`, `AirlockPressurizationVaultBuffers`, and `InventoryDefragCommand`.
- Verified no fields, arrays, or `List<T>` storage of those six carrier types in scripts/tests before accepting the byref-like contract.
- Inspected top remaining owner candidates and left real persistent owners unchanged.

Cinematic Cheats used:
- None. This pass is native alias lifetime enforcement only.

Verification:
- Declaration grep confirms all six target carriers are `public ref struct`.
- Storage grep for fields, arrays, and `List<T>` of these six carrier types returns no hits.
- Scoped `git diff --check` over edited files reports only existing LF/CRLF warnings.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_PUBLIC_REFSTRUCT.json`: `scannedFiles=2441`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=471`, `stackOnlyRefStructViewFields=659`.
- No compile/import/profiler proof: CPU guard later sampled `83%`; no compiler processes were active, but project law blocks build above 50% CPU.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 88 fewer persistent native alias candidates in official all-scripts scanner.

## 2026-05-27 22:25 +04 - View Carrier Ref-Struct Pass 3

What was wrong:
- After public carrier cleanup, more plain struct native view carriers remained in the persistent alias ledger.
- They were not true owners, but their plain struct shape still allowed accidental field/collection storage of `NativeArray<T>` aliases.
- Remaining true owner sets and explicit-layout unsafe pointer DTOs require separate design/compile proof, not mechanical conversion.

What was done:
- Converted these transient carriers to `ref struct`: `VRInteractionKinematicBridgeViews`, `UpgradeMatrixVaultViews`, `OceanKinematicsVaultRuntime.Views`, `SaveMerkleVaultBufferSet`, `AupNarrativePoiBuffers`, `LootMagnetVaultViews`, `UtilityAIAnxietyVaultBuffers`, `HandIkVaultViews`, `AlphaLeviathanVaultBuffers`, `ScavengingLootOracleVaultViews`, and `MockGlobalShaderDataKernel`.
- Kept `ThermodynamicsHazardGridPointers` unchanged because it is unsafe explicit-layout pointer ABI and needs compile proof before byref-like conversion.
- Kept actual owner buffer sets unchanged.

Cinematic Cheats used:
- None. This pass is native alias lifetime enforcement only.

Verification:
- Declaration grep confirms all target carriers are `ref struct`.
- Storage grep for fields, arrays, and `List<T>` of the target carrier types returns no hits.
- Scoped `git diff --check` over edited files reports only existing LF/CRLF warnings.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_VIEW_REFSTRUCT_3.json`: `scannedFiles=2441`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=386`, `stackOnlyRefStructViewFields=744`.
- Remaining carrier-shaped forbidden entries are actual owner sets plus `ThermodynamicsHazardGridPointers`.
- No compile/import/profiler proof: CPU guard sampled `100%`; no compiler processes were active, but project law blocks build above 50% CPU.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 85 fewer persistent native alias candidates in official all-scripts scanner.

## 2026-05-27 23:05 +04 - Micro Carrier Stack-Only Pass And Encounter Fence

What was wrong:
- Several remaining ledger candidates were stack-only native alias helpers but still plain structs: dispatcher context, Burst callback writer, auxiliary telemetry pass, mock scatter/spatial buffers, A*/assignment heap helpers, frame arena slice, binary save reader, subtitle pointer phases, and a mock inventory view.
- `EncounterDirector.Dispose()` chained native disposals to an active job dependency and then dropped the final dispose handle before releasing GPU sidecar buffers.

What was done:
- Converted to stack-only byref-like contracts: `DispatcherJobContext`, `BurstCallbackQueue.ParallelEventWriter`, `RecordAuxiliaryTelemetryPass`, `MockScatterBuffer`, `MockSpatialHashGrid`, `NativeMinHeap`, `MarauderNativeMinHeap`, `DroneNativeMinHeap`, `DroneTaskNativeMinHeap`, `NativeArenaSlice<T>`, `BufferReader`, `EvaluateSubtitleCuesPhase`, `ClearSubtitleCueFlagsPhase`, and `MockPlayerInventory`.
- Added final forced completion of the chained `EncounterDirector` native dispose handle before predator AUP GPU buffer release.
- Left persistent owners untouched: combat runtime, scatter working memory, foveated manager, signal/event buses, UI state store, pending streamed chunks, batch renderer state, ring buffers, and DataVault-backed runtime owners.

Cinematic Cheats used:
- None. This pass changes ownership contracts and teardown fencing only; no simulation or visual algorithm changed.

Verification:
- Storage grep found no field/array/`List<T>` storage for converted helper types.
- Declaration grep confirms all target helpers are `ref struct` or `readonly ref struct`.
- Scoped `git diff --check` reports only existing LF/CRLF warnings.
- Fresh official Roslyn ledger/build proof blocked: external `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` PID 8372 and `VBCSCompiler` PID 31136 active; CPU sampled 54-82%.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Claimed frame-time gain: 0 us.
- Risk reduction: stale native alias heap storage becomes compile-forbidden for converted helpers; Encounter teardown no longer drops a scheduled native dispose handle.

## 2026-05-27 23:35 +04 - Dependency-Scheduled Dispose Fence Sweep

What was wrong:
- Multiple native teardown helpers used `Dispose(dependency)` and discarded the returned `JobHandle`.
- Several no-dependency helpers used `Dispose(default)` instead of immediate `Dispose()`.
- `ContextualPhysicalIkRuntime.DisposeBuffers()` accumulated `_disposeHandle` but never completed it; `HectonSpatialHash.Dispose()` scheduled query scratch disposal and dropped the final handle.

What was done:
- Patched immediate/no-dependency disposal in `PlayerInventory`, `ContextualPhysicalIkRig`, `HectonVoxelEngine`, and `VoxelDeltaProcessor`.
- Patched dependency-scheduled disposal to retain and complete the returned handle in `GlobalTelemetryBus`, `SaveManager`, `VoxelDeltaProcessor`, `HectonFluidEngine`, `HectonMapMagicVegetationBridge`, and `ScatterEvaluator`.
- Patched `InventoryGrid.Dispose(JobHandle)` to chain all native array disposals and complete the final dispose handle.
- Patched `ContextualPhysicalIkRuntime.DisposeBuffers()` and `HectonSpatialHash.Dispose()` to complete their accumulated teardown disposal handles.
- Left valid returned-handle routes unchanged: save binary deferred write disposal, voxel dynamic nav obstacle snapshots, Hecton world generator pending chunk arrays, BRG vegetation culling visibility mask, H8Memory owner job registration, UIStateStore final fence, and JobFenceManager returned handle.

Cinematic Cheats used:
- None. This pass changes native teardown fencing only.

Verification:
- Targeted `rg` for `Dispose(default)` and `Dispose(dependency)` now leaves either patched/completed sites, returned-handle sites, owner-registered sites, or one false positive custom call `_grid.Dispose(default)`.
- Scoped `git diff --check` over 12 edited source files reports only existing LF/CRLF warnings.
- No build, Unity import, Play Mode, profiler, or official ledger rerun: `VBCSCompiler` PID 31136 remained active and CPU sampled 62%.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Claimed frame-time gain: 0 us.
- Risk reduction: dependency-scheduled native disposal is no longer dropped in the patched teardown helper sites.

## 2026-05-28 00:31 +04 - Stack-Only Carriers, Player Handle Purge, Verification Wall

What was wrong:
- Several transient native view contracts were still ordinary structs: native query wrappers, terrain height payload DTOs, save binary/sidecar pointer writers, and a mock queue wrapper.
- `HectonPlayerMovement` stored `PlayerKinematicsNativeState`, and that struct embedded five live `NativeArray<T>` aliases. This was a hidden MonoBehaviour native alias that the direct Roslyn MonoBehaviour counter could not see.
- `VegetationNativeMemory.Dispose(JobHandle)` was a no-op despite many `VaultGenerationHandle<T>` descriptor fields, and `IsCreated` omitted several tail handles.

What was done:
- Converted `NativeQuery<T>`, `NativeSelectQuery<TSource,TResult>`, `MapMagicBridge.QuantizedHeightmapPayload`, `TerrainHeightSamplePayloadDTO`, `BufferWriter`, `SidecarWriter`, `SidecarReader`, and `MockModQueue` to stack-only `ref struct`/`readonly ref struct` contracts.
- Reworked `PlayerKinematicsNativeState` to store only `VaultGenerationHandle<T>` descriptors plus telemetry counters. `HectonPlayerMovement` now resolves arrays only for the active snapshot, drag job, telemetry write, blackbox dump, and origin-shift phase.
- Fixed `VegetationNativeMemory.IsCreated` and made `Dispose(JobHandle)` clear every handle.
- Left explicit-layout pointer DTOs and real owners unchanged: `ThermodynamicsHazardGridPointers`, `BlackboxRingBufferDTO`, ring buffers, `PendingChunk`, combat runtime, scatter working memory, save/IK/TBDR owner sets, signal/event buses.

Cinematic Cheats used:
- None. This pass changes native lifetime contracts and descriptor hygiene only.

Verification:
- Storage grep found no field/array/list storage for the converted transient carriers.
- Grep found no remaining direct `_playerKinematicsNativeState` access to `Positions`, `Velocities`, `IntendedMovements`, `DragSolvedVelocities`, or `TelemetryRing`.
- `git diff --check` over edited source files reports only existing LF/CRLF warnings.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_POINTER_REFSTRUCT_4.json`: `scannedFiles=2442`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=349`, `stackOnlyRefStructViewFields=787`.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_HANDLE_PURGE_5.json`: `scannedFiles=2442`, `parseFailures=0`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=343`, `stackOnlyRefStructViewFields=788`, `totalNativeFieldDeclarations=6676`.
- One legal guarded `dotnet build .\Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1 /nr:false /p:UseSharedCompilation=false` ran for 00:08:27 and failed. Visible failures are pre-existing/vendor/editor graph errors in AmplifyImpostors, MapMagic duplicate `CellExpose`, MeshBaker missing companion types, Unity ShaderGraph editor/importer APIs, and Technie PhysicsCreator obsolete `MeshCollider` members. No Unity import, Play Mode, profiler, or GC proof exists.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Claimed frame-time gain: 0 us.
- Static ledger impact since `VIEW_REFSTRUCT_3`: forbidden persistent candidates `386 -> 343` and stack-only ref-struct views `744 -> 788`.
- Risk reduction: player kinematics no longer caches DataVault native aliases inside a MonoBehaviour-held state struct; transient terrain/query/save writers cannot be heap-stored.

## 2026-05-27 22:28 +04 - Foveated Prune And Native/Byref Compile-Correctness Pass

What was wrong:
- `FoveatedSimulationManager` allocated and registered deferred surface probe native buffers with no current scheduler, writer, reader, or consumer path.
- A few previous native hardening changes crossed into invalid C# byref territory: job-local world sampler data was byref-like, cartography pinned helpers used `ref` handles with byref-like buffer outputs, a `NativeArray` indexer expression was passed by `in`, and survival CSV span parsing tripped escape analysis.
- The full project still does not have green compile/import/profiler proof.

What was done:
- Removed dead foveated deferred probe buffer IDs, `NativeArray` fields, Vault handles, alias clearing, release, allocation, and memory-budget registration.
- Restored `GlobalWorldSamplerData` to ordinary `struct` because job struct methods return it.
- Changed cartography pinned helper inputs to `scoped in CartographyVaultHandles` because helpers do not mutate the handle carrier.
- Copied boid blackbox entries from `NativeArray` into a local before passing by `in`.
- Reworked cold survival CSV row parsing so token spans do not escape loop-local helper boundaries.
- Stopped compile chasing after three guarded attempts once the remaining 31 errors were outside NativeArray/MonoBehaviour ownership.

Cinematic Cheats used:
- None. This pass removed dead native memory and repaired language/lifetime contracts only.

Verification:
- Source grep found no remaining removed foveated deferred probe symbols.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_FOVEATED_PRUNE_6.json`: `scannedFiles=2442`, `parseFailures=0`, `totalNativeFieldDeclarations=6674`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=341`, `stackOnlyRefStructViewFields=788`.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_COMPILE_FIX_7.json`: `scannedFiles=2442`, `parseFailures=0`, `totalNativeFieldDeclarations=6674`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=353`, `stackOnlyRefStructViewFields=776`.
- Targeted `Hecton8.Core.csproj` build attempts reduced errors `127 -> 48 -> 31`; the native/byref cluster was cleared.
- Remaining compile failures are outside this domain: voxel dirty blend smoke tester, survival kinematics contact job, fauna compatibility, internal flood waterline, gas dynamics snapshot/padding, terrain chunk pager mock flag, persistent world registry API drift, procedural wreck uint/int, flora genome unsafe async, and world runtime reference utility/interface assembly drift.
- No Unity import, Play Mode, profiler, or GC proof exists.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Claimed frame-time gain: 0 us.
- Static ledger impact of foveated prune: `343 -> 341` forbidden persistent candidates.
- Honest post-compile-fix ledger: `341 -> 353` forbidden persistent candidates because `GlobalWorldSamplerData` had to be compilable as an ordinary job carrier. Direct MonoBehaviour native candidates remain `0`.
- Memory risk reduced by removing unused foveated persistent buffers; compile risk reduced by eliminating native/byref escape-analysis failures in the audited files.

## 2026-05-27 22:45 +04 - Foveated Deferred Probe Shell Removal

What was wrong:
- After deleting the unused native deferred surface-probe buffers, `FoveatedSimulationManager` still had a dead managed queue shell: owner arrays, pending arrays, ring counters, a schedule flag, helper accessors, and an unused completion parameter.
- There was still no producer/consumer path. This was no longer a NativeArray leak, but it was still stale architecture and cold managed memory.

What was done:
- Removed the managed deferred surface-probe owner arrays, queue counters, scheduling flag, stale helper methods, and unused `includeDeferredSurfaceProbes` parameter.
- Simplified `TryCompleteFrameJobsInternal` call sites to the actual remaining behavior: complete interpolation and importance jobs.
- Left live foveated cadence, telemetry ring, DataVault native buffers, Doppler smoothing, origin-shift, and blackbox dump routes unchanged.

Cinematic Cheats used:
- None. This pass removes dead route state only.

Verification:
- `rg` reports no remaining `DeferredSurfaceProbe`, `deferredSurfaceProbe`, `MaxDeferredProbeSlots`, `_deferredProbe`, `_queuedDeferred`, or `_pendingDeferred` symbols in `FoveatedSimulationManager`.
- Scoped `git diff --check` on `FoveatedSimulationManager.cs` reports only existing LF/CRLF warning.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_FOVEATED_DEAD_QUEUE_8.json`: `scannedFiles=2442`, `parseFailures=0`, `totalNativeFieldDeclarations=6674`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=353`, `stackOnlyRefStructViewFields=776`.
- No compile/import/profiler proof; compile wall remains the 31 non-domain Hecton8.Core errors recorded in the prior entry.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static native ledger impact: 0, because this was managed dead state after the native buffers were already removed.
- Cold memory risk reduced by removing unused object/int arrays and stale queue state from the foveated director.

## 2026-05-27 23:05 +04 - TBDR Hidden Vault Alias Isolation

What was wrong:
- `TBDRPipelineSurgeonRuntime` had already moved its direct native arrays behind a managed buffer owner, but still held `public TBDRVertexBudgetVault Vault`.
- `TBDRVertexBudgetVault` is a struct with four `NativeArray<T>` fields. That is a hidden MonoBehaviour native alias aggregate; the direct Roslyn MonoBehaviour counter does not catch it.

What was done:
- Added private `VertexBudgetVaultOwner` and moved the struct-backed vault storage there.
- Replaced the public field with `public ref TBDRVertexBudgetVault Vault => ref _vaultOwner.Vault;` so existing runtime code still works against owner-backed storage.
- Updated initialization and CSV polling to use local `ref TBDRVertexBudgetVault` variables instead of ref-passing the property directly.
- Kept `TBDRVertexBudgetVault` itself as a struct to avoid a wider public API migration.

Cinematic Cheats used:
- None. This pass is hidden native alias isolation only.

Verification:
- `rg` finds no `public TBDRVertexBudgetVault Vault` field in `TBDRPipelineSurgeonRuntime`.
- The only `TBDRVertexBudgetVault Vault;` field in that file is inside private managed `VertexBudgetVaultOwner`.
- Scoped `git diff --check` on `TBDRPipelineSurgeonRuntime.cs` reports only existing LF/CRLF warning.
- Official scanner artifact `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_TBDR_VAULT_OWNER_9.json`: `scannedFiles=2442`, `parseFailures=0`, `totalNativeFieldDeclarations=6674`, `forbiddenMonoBehaviourCandidates=0`, `forbiddenPersistentCandidates=353`, `stackOnlyRefStructViewFields=776`.
- No compile/import/profiler proof; compile wall remains outside this domain.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static native ledger impact: 0, because this was a hidden aggregate alias, not a direct native field declaration on the MonoBehaviour.
- Risk reduction: TBDR runtime no longer stores a native-array struct directly on a MonoBehaviour.

## 2026-05-27 23:18 +04 - Hidden Struct Alias Tail Classification

What was wrong:
- Direct MonoBehaviour-native scanner count is `0`, but direct syntax is not enough.
- Heuristic scan for forbidden native owner structs in MonoBehaviour files found three hits.
- Two are not current MonoBehaviour storage problems: `SignalThreadLocalWriteContext` is inside `SignalThreadLocalWriter64`, and `TBDRVertexBudgetVault` is now inside private `VertexBudgetVaultOwner`.
- One real tail remains: `HectonWorldGenerator` stores `List<PendingChunk>`, and `PendingChunk` is a struct with seven `NativeArray<T>` fields.

What was done:
- Classified the tail instead of applying a fake fix.
- Rejected `PendingChunk` `struct -> class` conversion because it would add managed allocation per streamed chunk.
- Recorded the required direction: pooled pending chunk slots or SoA owner storage, not heap objects.

Cinematic Cheats used:
- None.

Verification:
- Struct-hidden scan result: `SignalThreadLocalWriteContext`, `TBDRVertexBudgetVault`, `PendingChunk`.
- `TBDRVertexBudgetVault` direct MonoBehaviour field already removed in prior entry.
- `PendingChunk` remains a documented residual owner-shape risk.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 0.
- Residual risk remains intentionally open because the safe fix is larger than a single-field wrapper and must preserve zero-GC streaming.

## 2026-05-27 - PendingChunk Overflow Dispose Fence

What was wrong:
- `HectonWorldGenerator.ScheduleChunkJob` had a pending-list overflow fallback that disposed a freshly created `PendingChunk` with `pc.DisposeArrays(pc.combinedHandle)`.
- The returned scheduled-disposal `JobHandle` was discarded.
- That is not a frame-time optimization. It is a native lifetime proof hole: the arrays are scheduled for release after jobs, but the owner no longer has a fence to finalize or force-complete during stop/teardown.

What was done:
- Added `_pendingChunkOverflowDisposeHandle` and `_pendingChunkOverflowDisposeActive` to the world generator owner.
- Overflow disposal now flows through `AccumulatePendingChunkOverflowDisposal`.
- `LateFrameTick` drains completed overflow disposals without blocking.
- `StopStreaming` force-completes the overflow fence after pending chunk job teardown.
- Did not convert `PendingChunk` to a managed class. That would add GC allocation to world streaming and would be the wrong fix.

Cinematic Cheats used:
- None. This is native lifecycle determinism, not visual simulation.

Verification:
- Targeted grep confirms the only overflow `pc.DisposeArrays(pc.combinedHandle)` call now stores the returned handle.
- Targeted grep confirms `DrainPendingChunkOverflowDisposals(false)` in `LateFrameTick` and `DrainPendingChunkOverflowDisposals(true)` in `StopStreaming`.
- Scoped `git diff --check -- Assets/_Project/Scripts/HectonWorldGenerator.cs` reports only the existing LF/CRLF warning.
- No fresh Roslyn scanner/build/import/profiler proof: CPU sampled `94%` and external `dotnet` PID `61052` was active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: expected 0, because this is a retained JobHandle fence, not a field-shape change.
- Risk reduction: one dropped scheduled native-dispose handle removed from the world streaming saturation path.

## 2026-05-27 - DepthZone GlobalRegistry Hot Accessor Tail Removed

What was wrong:
- `DepthZoneDirector.Instance` still returned `GlobalRegistry.DepthZone`.
- That was the final direct `Instance => GlobalRegistry` route found by the owner-route grep.
- It is not a native-array leak, but it violates the same global-systems rule: read accessors should be pure owner-local reads, not hot registry polling.

What was done:
- Added `s_activeRuntimeInstance` to `DepthZoneDirector`.
- `Instance` now returns the owner-local active runtime.
- Duplicate detection still uses `s_activeRuntimeInstance ?? GlobalRegistry.DepthZone` as a cold bootstrap fallback.
- Successful registration publishes `s_activeRuntimeInstance = this`; unregister clears it if this component owns it.
- `DepthZoneDirector.cs` is not UTF-8 clean, so the edit was byte-preserving and touched only exact ASCII fragments.

Cinematic Cheats used:
- None. This is route ownership cleanup.

Verification:
- `rg "Instance\\s*=>\\s*GlobalRegistry|ActiveRuntimeInstance\\s*=>\\s*GlobalRegistry|TryGetInstance\\s*\\([^)]*\\)\\s*=>\\s*GlobalRegistry" Assets/_Project/Scripts -g "*.cs"` returns no hits.
- `rg` confirms `s_activeRuntimeInstance` is assigned after service registration and cleared on unregister.
- Scoped `git diff --check -- Assets/_Project/Scripts/World/DepthZoneDirector.cs` reports only the existing LF/CRLF warning.
- No compile/import/profiler proof; external compiler lane/CPU guard remains active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 0.
- Risk reduction: final direct runtime singleton route through `GlobalRegistry` removed from the audited grep class.

## 2026-05-27 - GlobalRegistry Rebound Count Read Hardened

What was wrong:
- `GlobalRegistry.PendingServiceReboundCount` directly summed two mutable counters.
- `SystemDispatcher.FlushCoreEventsArtery` reads that property for core late-frame queue-depth reporting.
- This was not a native ownership leak, but it was weaker than the existing global read pattern used elsewhere in `GlobalRegistry`.

What was done:
- Changed the accessor to use `Volatile.Read(ref _pendingServiceReboundCount)` plus `Volatile.Read(ref _nextFrameServiceReboundCount)`.
- No locking, allocation, queue mutation, or dispatch behavior was added.

Cinematic Cheats used:
- None.

Verification:
- `rg` confirms both counter reads now go through `Volatile.Read`.
- `rg` confirms the only current source consumer remains `SystemDispatcher.FlushCoreEventsArtery`.
- Scoped `git diff --check -- Assets/_Project/Scripts/Core/GlobalRegistry.cs` reports only the existing LF/CRLF warning.
- No compile/import/profiler proof: CPU sampled `91%` and external `dotnet` PID `61052` remained active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger impact: 0.
- Risk reduction: late-frame global rebound telemetry now uses the same volatile read discipline as other registry state accessors.

## 2026-05-27 - PendingChunk Hidden Alias Store Rebuilt

What was wrong:
- `HectonWorldGenerator` kept pending streamed chunk jobs in `List<PendingChunk>`.
- `PendingChunk` was an ordinary struct with seven `NativeArray<T>` aliases.
- The official direct MonoBehaviour ledger stayed at zero, but the MonoBehaviour still owned heap-storable native alias copies through the list.

What was done:
- Converted `PendingChunk` to a stack-only `ref struct` snapshot.
- Added one fixed-capacity `PendingChunkStore` SoA owner for coord/lod/resolution/job/cancel metadata and seven native alias lanes.
- Store insertion is `TryAdd`; capacity failure falls through to scheduled native disposal instead of silently dropping an owned chunk.
- Kept the prior overflow scheduled-dispose fence.
- Did not introduce per-chunk managed classes.

Cinematic Cheats used:
- Fixed-capacity SoA owner instead of managed object allocation per streamed chunk.

Verification:
- `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_PENDING_CHUNK_STORE_11.json` reports `2442` scanned files, `0` parse failures, `0` forbidden MonoBehaviour candidates, `353` forbidden persistent candidates, and `783` stack-only ref-struct fields.
- Ledger classification shows `PendingChunk` fields as stack-only and `PendingChunkStore` as the explicit persistent owner.
- Scoped `git diff --check` reports only existing LF/CRLF warnings.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static ledger persistent impact: 0; this is an owner-shape fix, not a counter-reduction trick.
- Risk reduction: pending chunk native aliases are no longer copied through `List<struct>` storage.

## 2026-05-27 - Dead MockUIBuffer Unsafe DTO Removed

What was wrong:
- `MockUIBuffer` was an unused internal raw-pointer DTO in `H8StaticDataContracts.cs`.
- No source code referenced it.

What was done:
- Removed `MockUIBuffer`.

Cinematic Cheats used:
- Deleted dead unsafe surface instead of wrapping it.

Verification:
- `rg "MockUIBuffer" Assets/_Project/Scripts -g "*.cs"` returns no source hits.
- Scoped `git diff --check` reports only existing LF/CRLF warnings.
- Fresh scanner/build/import proof after this deletion and the final `TryAdd` hardening is blocked by external `MapMagic.Editor.csproj` dotnet build PID `50860` and `VBCSCompiler` PID `5276`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Static risk reduction: one dead raw pointer DTO removed.

## 2026-05-27 - Runtime Manual GC Collection Removed

What was wrong:
- Two live runtime routes still requested managed garbage collection: `SystemDispatcher.DispatchCriticalMemoryPressure` and `SaveManager.Tick()` post-save VRAM GC drain.
- This violated the Zero-GC mandate and contradicted project docs claiming no live `GC.Collect()` in scripts.

What was done:
- Removed the manual `GC.Collect(0, Optimized, false)` calls.
- Removed the post-save GC pending flag, frame-budget constants, and dead SaveManager VRAM-GC request path.
- Kept pressure signaling, pool flush, crash telemetry, and DataVault defrag request routes intact.

Cinematic Cheats used:
- None. This is stability cleanup, not visual simulation.

Verification:
- `rg "System\\.GC\\.Collect\\s*\\(|\\bGC\\.Collect\\s*\\(" Assets/_Project/Scripts -g "*.cs"` returns no hits.
- Scoped `git diff --check` on `SystemDispatcher.cs` and `SaveManager.cs` reports only existing LF/CRLF warnings.
- `VAULT_NATIVE_ALIAS_LEDGER_AUDIT_NATIVE_STATE_AFTER_RUNTIME_GC_12.json` reports `2443` scanned files, `0` parse failures, `0` forbidden MonoBehaviour candidates, `352` forbidden persistent candidates, and `783` stack-only ref-struct fields.
- Guarded `dotnet build .\Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /nr:false /p:UseSharedCompilation=false` passed with `0` errors and `927` warnings in `00:01:06.63`.
- No Unity import, Play Mode, profiler, or GC allocation capture was run.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Expected benefit: fewer forced/queued managed collection hitches on save completion and critical memory events, especially on low-end devices.

## 2026-05-27 - Dispose Fence Recheck Completed

What was wrong:
- `FaunaSimulationMemory.Dispose(JobHandle)` accepted a resident LOD job dependency but released DataVault handles without completing it.
- `SargassumMicroFaunaBoids.ClearVaultHandles` accepted a cancelled leviathan node-build dependency but cleared Vault generation handles and ring aliases without completing it.
- Both paths made teardown/rebind state look clean while a job could still be touching the same native memory.

What was done:
- Added forced `DispatcherJobFence.TryComplete` before fauna residency Vault alias release.
- Added forced `DispatcherJobSwap.TryComplete` before sargassum Vault handle clearing.
- Left steady-frame completion behavior unchanged.

Cinematic Cheats used:
- None. This is native lifetime correctness.

Verification:
- Scoped diff contains only two dependency completion calls in `FaunaSimulationEngine.cs` and `SargassumMicroFaunaBoids.cs`.
- Scoped `git diff --check` reports only existing LF/CRLF warnings.
- Hidden-alias heuristic over MonoBehaviour files found no new direct MonoBehaviour native field tail; remaining hits are job writer structs, owner wrappers, or pointer out-params already classified.
- Fresh scanner/build/import/profiler proof was not run: external `dotnet build Hecton8.slnx` PID `21868` is active and CPU sampled `54-60%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us expected; new completions are teardown/rebind only.
- Risk reduction: prevents native/DataVault alias invalidation before scheduled fauna/sargassum jobs are fenced.

## 2026-05-27 - Quest DAG Dispose Closure

What was wrong:
- `QuestDagResolverService.Dispose(JobHandle)` could skip `QuestDagVault.ReleaseBuffers` permanently when called while resolver work was scheduled.
- The stale gate was computed before `_hasScheduled` was folded into the teardown dependency, then `_disposed=true` blocked any later release.

What was done:
- Removed the conditional release gate.
- Combined the active resolver handle into `disposeDependency`.
- Forced the teardown fence before releasing Quest DAG Vault handles.
- Left quest scheduling, SignalBus publication, tick dilation, and state math unchanged.

Cinematic Cheats used:
- None. This is native lifetime correctness, not presentation work.

Verification:
- Scoped source diff contains only the Quest DAG fence/release correction.
- `git diff --check -- Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` reports only the existing LF/CRLF warning.
- No fresh scanner/build/import/profiler proof: external `dotnet build Hecton8.Editor.csproj` PID `14740` is active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us; new blocking point is dispose/rebind only.
- Risk reduction: Quest DAG Vault descriptors cannot remain retained after scheduled resolver teardown through the dependency overload.

## 2026-05-27 - Fauna/Sargassum Teardown Fence Closure

What was wrong:
- `ProceduralCrabLegIKRuntime` could leave `_pendingHandle` alive after `OnDisable` removed the owner from dispatcher callbacks, then clear Vault handles on destroy or DataVault hot-swap without a guaranteed fence.
- `SargassumMicroFaunaBoids.DisposeFoveatedSimulationBuffers` had a scheduled cleanup branch that cleared `_foveatedSimulationHandle` without completing or retaining it.

What was done:
- Added crab teardown completion before `OnDisable` unregister and before DataVault handle rebind.
- Changed crab `DisposeBuffers(JobHandle)` to combine caller dependency with the active pending handle before clearing handles.
- Changed sargassum foveated buffer disposal to combine and force-complete the active foveated handle before clearing handles.

Cinematic Cheats used:
- None. This is native/job ownership closure only; no visual simulation behavior changed.

Verification:
- Scoped `git diff --check` on the two edited source files reports only existing LF/CRLF warnings.
- Source grep confirms `CompletePendingPipelineForTeardown`, crab dependency combination, and sargassum foveated dependency combination are present.
- No build/import/profiler proof: external `dotnet.exe` PID `57212` is active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us. Disable/rebind can wait for scheduled fauna/foveated jobs; that is required lifetime safety, not claimed performance gain.

## 2026-05-27 - Leviathan Tentacle Solver Lifecycle Fence

What was wrong:
- `LeviathanTentacleVerletSolver.OnDisable` could unregister update/late-frame callbacks while `_solverScheduled` stayed true.
- `DisposePersistentBuffers` cleared Vault handles and dropped `_pendingSolverHandle` without always fencing the scheduled solver.
- DataVault hot-swap was ignored despite the solver owning DataVault generation handles.

What was done:
- Force-complete pending tentacle solver work before `OnDisable` unregister.
- Force-complete pending solver work before clearing persistent Vault handles.
- Added DataVault hot-swap rebind: fence active solver, clear stale handles, cache new vault, ensure buffers, reseed socket state.

Cinematic Cheats used:
- None. This is native/job lifecycle correctness only.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Fauna/LeviathanTentacleVerletSolver.cs` reports only existing LF/CRLF warning.
- Source grep confirms the force-complete routes and DataVault rebind branch.
- No build/import/profiler proof: CPU sampled `98.44%` and external `dotnet.exe` PID `18704` is active.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us. Disable/rebind can block on scheduled tentacle solve; that is required lifetime safety.

## 2026-05-27 - Stress Spawn Director Lock Release On Vault Swap

What was wrong:
- `StressDrivenSpawnDirector` could switch `_vault` and clear handles during DataVault hot-swap while a scheduled spawn job still held write locks on the old vault.
- `Dispose()` had similar lifecycle cleanup duplicated locally instead of one consistent fence/unlock route.

What was done:
- Added a lifecycle helper that force-completes `_activeHandle`, releases `_lockedVault` write locks, and clears scheduled/lock state.
- Called that helper before DataVault hot-swap changes `_vault`.
- Reused the helper from `Dispose()`.

Cinematic Cheats used:
- None. This is lock/job lifecycle correctness only.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs` reports only existing LF/CRLF warning.
- Source grep confirms the helper is called from DataVault rebind and `Dispose`.
- Targeted compile proof passed after CPU dropped: `dotnet build .\Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /nr:false /p:UseSharedCompilation=false` completed with `0` errors and `927` warnings in `00:01:24.60`.
- Unity import, Play Mode, profiler, and GC allocation capture were not run.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us. DataVault service replacement may wait for the scheduled spawn job so write locks are released deterministically.

## 2026-05-27 - Terrain Pager DataVault Rebind Fence

What was wrong:
- `TerrainChunkPagerRuntime` ignored active `DataVault` replacement.
- It kept old Vault handles, worker queues, staging buffers, and pending residency/eviction jobs while the registry service route changed.

What was done:
- Added active DataVault rebind handling.
- The pager now stops scheduling phases, force-completes pending pager jobs, stops the worker thread, releases old Vault handles through the old Vault, binds the new Vault, reacquires native state, reloads cold streaming profile data, restarts the worker, and re-registers dispatcher phases.
- Added `_pendingLifecycleRebindVault` so deferred worker shutdown does not overwrite `_vault` before old handles are released.

Cinematic Cheats used:
- None. This is world-streaming ownership and lifecycle safety, not simulation or rendering approximation.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/World/TerrainChunkPagerRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `RebindDataVaultForLifecycle` and `CompletePendingPagerJobsForLifecycle`.
- Build/import/profiler proof not run: external `dotnet` PID `46776` was active and CPU sampled up to `64.36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us expected. DataVault service replacement can wait for active pager work and worker shutdown because releasing old Vault handles first is mandatory.

## 2026-05-27 - Material Response Job Lock Fence

What was wrong:
- `ShinobuMaterialResponseRuntime` scheduled a material response job but did not retain the returned `JobHandle`.
- `PostSimulationTick`, shutdown, and DataVault hot-swap released DataVault locks from `_simulationScheduled` alone, so locks/handles could be reset while the job still owned the buffers.

What was done:
- Added `_simulationHandle`.
- Scheduling stores the handle and refuses a new material response job until the previous one finalizes.
- `PostSimulationTick` finalizes the retained handle before unlocking.
- Shutdown and DataVault hot-swap force-complete the retained handle through `CompleteSimulationForLifecycle` before lock release and handle reset.

Cinematic Cheats used:
- None. This is DataVault lock/job lifecycle correctness.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `_simulationHandle`, `TryFinalizeCompleted(ref _simulationHandle)`, and `CompleteSimulationForLifecycle`.
- Build/import/profiler proof not run because the compiler lane was already blocked by external `dotnet` PID `46776` and CPU had sampled up to `64.36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame cost: 0 us expected when jobs complete in the normal post-simulation window. Delayed completion now skips overlap instead of unlocking unsafe buffers.

## 2026-05-28 - Fabrication Dispatcher Job Handle Fence

What was wrong:
- `FabricationAssemblerRuntime` scheduled fabrication progress/signal jobs over Vault-backed native arrays and returned the handle to the master dispatcher, but retained no local handle.
- Shutdown/DataVault hot-swap could release Vault handles outside the dispatcher post-simulation completion window.

What was done:
- Added `_simulationHandle` and `_simulationScheduled`.
- Scheduling now retires completed prior work, or returns a dependency combined with the still-active handle instead of overlapping jobs.
- `PostSimulationTick` finalizes the retained handle before native reads.
- Shutdown and DataVault hot-swap force-complete the retained handle before releasing Vault handles.

Cinematic Cheats used:
- None. This is native lifetime correctness, not simulation fidelity.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/FabricationAssemblerRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `_simulationHandle`, `_simulationScheduled`, and `CompleteSimulationForLifecycle`.
- Build/import/profiler proof was not run: external compiler lane is active (`dotnet` PID `47780`) while CPU sampled `36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame expected cost: one completed-handle poll when fabrication work was scheduled. Lifecycle force-complete runs only on shutdown/DataVault replacement.

## 2026-05-28 - Visual Pressure Aging Retained Handle And DataVault Rebind

What was wrong:
- `VisualPressureAgingRuntime` kept `_scheduledSimulationHandle` but post-simulation only checked `IsCompleted`.
- If `_simulationScheduled` was still true at the next simulation phase, it returned raw `dependsOn`, detaching the old job from the master dispatcher fence.
- Shutdown unlocked/released Vault state and then dropped the handle without completing it.
- The runtime cached DataVault once and did not subscribe to service replacement while holding owned Vault handles.

What was done:
- Replaced `IsCompleted` handling with `DispatcherJobFence.TryFinalizeCompleted`.
- Active stale work is now returned as a combined dispatcher dependency instead of detached.
- Added lifecycle force-complete before shutdown unlock/release.
- Added `IGlobalRegistryHotSwapListener` route: close editor leases, complete active work, unlock old locks, release old Vault handles, bind new Vault, reacquire state, refresh external handles.

Cinematic Cheats used:
- None. This is native lifetime and global-service route correctness.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms hot-swap listener registration, retained-handle finalization, dependency combine, and lifecycle completion routes.
- Build/import/profiler proof was not run because the compiler lane is already active (`dotnet` PID `47780`) and CPU sampled `36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame expected cost: one completed-handle poll when visual aging work was scheduled. DataVault replacement/shutdown may wait for active aging work before releasing Vault handles.

## 2026-05-28 - Atmosphere And Plasma Dispatcher Handle Fences

What was wrong:
- `BaseAtmosphereLogisticsRuntime` and `ShinobuPlasmaBeamRuntime` locked Vault buffers and scheduled jobs but tracked lifetime only with `_simulationScheduled`.
- Post-simulation assumed the master dispatcher had completed the work, but the owner had no retained handle for shutdown, stale scheduled state, or service-rebind windows.
- Plasma DataVault hot-swap returned early while scheduled and dropped the replacement request.

What was done:
- Added local `_simulationHandle` in both runtimes.
- Active stale work is now combined back into dispatcher dependencies instead of detached.
- Post-simulation finalizes retained handles through `DispatcherJobFence.TryFinalizeCompleted` before native reads/unlocks.
- Shutdown force-completes retained handles; plasma DataVault hot-swap force-completes before changing `_vault`.

Cinematic Cheats used:
- None. This is job/native lifetime and service-route correctness.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs Assets/_Project/Scripts/VFX/PlasmaBeam/ShinobuPlasmaBeamRuntime.cs` reports only existing LF/CRLF warnings.
- Source grep confirms retained handle/finalize/combine/force-complete routes in both files.
- Build/import/profiler proof was not run because external `dotnet` PID `47780` is active and CPU sampled `36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame expected cost: one completed-handle poll in post-simulation per scheduled atmosphere/plasma job. Lifecycle force-complete is shutdown/service-rebind only.

## 2026-05-28 - Async Buoyancy Readback Write-Lock Fence

What was wrong:
- `AsyncBuoyancyReadbackRuntime` acquired DataVault write locks for mock/apply jobs and released them in post-simulation, disable, or DataVault replacement without retaining the job handle locally.
- If disable/service replacement happened before the dispatcher post-simulation window, write locks could be released while jobs still wrote mock ring, completed requests, resolved heights, result states, or counters.

What was done:
- Added `_simulationHandle` and `_simulationScheduled`.
- Retain the scheduled handle whenever simulation write locks are active, including early-return paths after mock scheduling.
- Reattach still-active work to the dispatcher dependency chain.
- Finalize before post-simulation lock release and force-complete before disable/DataVault hot-swap.

Cinematic Cheats used:
- None. This is native write-lock and job lifetime correctness.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback/AsyncBuoyancyReadbackRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `_simulationHandle`, `_simulationScheduled`, `RetainSimulationHandleIfLocked`, `HasSimulationWriteLocks`, and lifecycle completion routes.
- Build/import/profiler proof was not run because external `dotnet` PID `47780` is active and CPU sampled `36%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame expected cost: one completed-handle poll only when async buoyancy write locks were acquired. Lifecycle force-complete is disable/service-rebind only.

## 2026-05-28 - Trade Marauder Lifecycle Fence Pass

What was wrong:
- `TradeMarauderDirector.OnDisable` could unregister Slow/Frost tick lanes while `_activeJobHandle` was still running, then return through `deferHandleClear` without releasing owned DataVault handles.
- After disable there was no guaranteed owner phase left to finalize the job, publish completed signals, or clear the twenty-two Vault handles.
- Public/editor routes could read or write faction standing, tuning, route views, economy weights, and counters while the scheduled economy job chain was still using those native arrays.

What was done:
- `OnDisable` now force-completes active marauder work through `CompleteActiveJobForLifecycle()` before unregistering and releasing native handles.
- DataVault hot-swap now uses the same lifecycle fence before releasing old handles and binding the new service.
- Faction reputation mutation, tuning editor read/write, editor view exposure, and CSV override now fail closed while `_jobScheduled` is true.

Cinematic Cheats used:
- None. This was native lifetime and owner-phase correctness. Marauder economy math, route solving, theft, acoustic signal, and visual proxy hydration are unchanged.

Verification:
- Static source grep confirms `CompleteActiveJobForLifecycle()` in `OnDisable` and DataVault rebind.
- Static source grep confirms no remaining `deferHandleClear` route.
- Scoped `git diff --check -- Assets/_Project/Scripts/Economy/TradeMarauderRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler proof blocked: external `dotnet` PID `47780`, CPU sampled `77%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; lifecycle force-complete runs only on disable/service rebind. Public/editor active-job guards are branch-only cold/editor routes.

## 2026-05-28 - Parasite Swarm DataVault Rebind Fence

What was wrong:
- `ParasiteSwarmGpuRuntime` cached `_vault = GlobalRegistry.DataVault` and registered as a hot-swap listener, but ignored `GlobalRegistryServiceSlot.DataVault`.
- Pending target extraction jobs and target write locks could remain tied to old Vault-backed target/candidate/count buffers during service replacement.
- Generation descriptors could stay pointed at the old Vault route after replacement.

What was done:
- Added DataVault branch in `OnGlobalRegistryServiceReplaced`.
- Added `RebindDataVaultForLifecycle()`: force-complete pending target extraction, release old target write locks against the old Vault, clear descriptors, bind the new Vault, ensure shared parasite buffers, rebind descriptors, and reseed tuning.
- Reused `CompleteTargetSelectionForLifecycle()` from `OnDisable`.

Cinematic Cheats used:
- None. This is native/Vault lifecycle correctness. Parasite compute, target selection scoring, particle budget, and visual phase are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms DataVault hot-swap branch, `RebindDataVaultForLifecycle`, `CompleteTargetSelectionForLifecycle`, and `ClearVaultDescriptors`.
- Build/import/profiler/GPU proof blocked: external `dotnet` PID `47780`, CPU sampled `71%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; rebind fence runs only on DataVault service replacement or disable.

## 2026-05-28 - Diegetic Gyro Compass DataVault Rebind Fence

What was wrong:
- `DiegeticGyroCompassRuntime` handled `GlobalRegistryServiceSlot.DataVault` by assigning `_vault` directly, resolving new buffers, and resetting presentation while a scheduled `GyroDriftJob` could still own slices from the old Vault.
- Disabled instances could miss DataVault replacement because OnEnable did not re-resolve cold dependencies after the first Start.
- `TryReadCompassState` was a read route that sanitized and wrote back to DataVault; recalibration methods could also write native state while `_jobPending` was active.

What was done:
- Added `RebindDataVaultForLifecycle()` and `ClearVaultLanes()`: complete pending drift work, clear stale generation lanes, bind the new Vault, reset fast cadence counters, re-resolve buffers, and mark presentation dirty.
- OnEnable now rechecks cold dependencies before service/tick registration.
- Snapshot reads fail closed while `_jobPending` is active.
- Recalibration requests/hold progress during active jobs are deferred in owner-local scalar fields and consumed during the next drift integration.
- `TryReadCompassState` now sanitizes the returned copy only, not the DataVault source.

Cinematic Cheats used:
- None. This is native ownership and global-route correctness. Drift math, indirect dial rendering, particle bursts, and quality-weight cadence remain unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/UI/Navigation/DiegeticGyroCompassRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `RebindDataVaultForLifecycle`, `ClearVaultLanes`, `ApplyQueuedManualRecalibration`, and no direct `_vault = currentService` hot-swap assignment.
- Build/import/profiler proof blocked: external `dotnet` PID `14348`, CPU sampled `100%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: branch-only read/write guards; lifecycle force-complete runs only on DataVault service replacement or cold rebind.

## 2026-05-28 - Acoustic Echo Static DataVault Rebind Fence

What was wrong:
- `AcousticEchoLocationRuntime` is static and cached `_dataVault` plus Vault generation handles, but only retried `GlobalRegistry.DataVault` while unbound.
- After DataVault service replacement, initialized acoustic echo could keep old Vault-backed trail, target, fault, and black-box buffers alive through stale descriptors.
- `EnsureVaultBuffers()` had unreachable same-reference rebind code because its local `vault` value was copied from `_dataVault`.

What was done:
- Added a single cold static `AcousticEchoHotSwapBridge` implementing `IGlobalRegistryHotSwapListener`.
- Initialization registers the bridge once; disposal unregisters it.
- DataVault replacement now completes the tracking fence, releases old handles through the old Vault service, clears descriptors and transient state, binds the new service, and reacquires buffers when initialized.
- Removed the dead same-reference rebind branch from `EnsureVaultBuffers()`.

Cinematic Cheats used:
- None. This is native lifecycle correctness. Echo scoring, acoustic trail policy, target sampling, black-box cadence, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `AcousticEchoHotSwapBridge`, register/unregister routes, `RebindDataVaultForLifecycle`, `CompleteTrackingFenceForVaultRelease`, and old-vault handle release.
- Build/import/profiler proof blocked: external `dotnet` PID `14348`, CPU sampled `83%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; bridge registration is static lifecycle work and rebind runs only on DataVault replacement/dispose.

## 2026-05-28 - Ecosystem Population Failed Schedule State Fix

What was wrong:
- `EcosystemPopulationBalancer.ScheduleBalancerJob` unlocked Vault buffers when `job.Schedule()` threw, but then continued as if scheduling succeeded.
- The failed path registered a default `_balancerHandle`, set `_jobScheduled=true`, and set `_jobLocksHeld=true`.
- Late-frame cleanup could then publish cull signals and unlock buffers for a job that never ran.

What was done:
- Failed `InvalidOperationException` and `ArgumentException` paths now clear `_balancerHandle` and return immediately after unlock/telemetry.
- `H8Memory.RegisterActiveJob` and `_jobScheduled=true` now happen only after successful scheduling.
- Removed redundant `_jobLocksHeld=true` after schedule; lock ownership remains inside `TryLockJobBuffers` and `UnlockJobBuffers`.

Cinematic Cheats used:
- None. This is native owner-state correctness. Population balancing math, cull policy, coefficients, and telemetry layout are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs` reports only existing LF/CRLF warning.
- Source diff confirms failed schedule paths return before active-job registration and scheduled-state mutation.
- Build/import/profiler proof blocked: external `dotnet` PID `14348`, CPU sampled `97%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; success path removes one redundant assignment, failure path exits earlier.

## 2026-05-28 - Audio Log Disabled DataVault Rebind Fix

What was wrong:
- `AudioLogSystem` kept DataVault handles while disabled, but disabled instances are not hot-swap listeners.
- If DataVault changed while disabled, the next `OnEnable` directly assigned `_dataVault = GlobalRegistry.DataVault`.
- That overwrote the old service route before old audio-log Vault handles were released.

What was done:
- Added `RebindDataVaultCold(IDataVault nextVault, bool ensureBuffers)`.
- DataVault hot-swap and OnEnable service refresh now share that helper.
- The helper releases handles through the cached old `_dataVault` before binding the new service.
- Direct `_dataVault = GlobalRegistry.DataVault` and direct DataVault hot-swap assignment were removed.

Cinematic Cheats used:
- None. This is native ownership cleanup. Playback, encrypted fragments, save data, and telemetry behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/AudioLog/AudioLogSystem.cs` reports only existing LF/CRLF warning.
- Source grep confirms DataVault routes use `RebindDataVaultCold` and the direct assignment routes are gone.
- Build/import/profiler proof blocked: external `dotnet` PID `14348` still active, CPU sampled `34%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; fix runs only during enable/service replacement.

## 2026-05-28 - Vocal Bank Disabled DataVault Rebind Fix

What was wrong:
- `VocalBankPlaybackRuntime` kept vocal bank DataVault handles across disable while unregistering its hot-swap listener.
- `CacheDataVaultCold()` only assigned `GlobalRegistry.DataVault` when `_dataVault` was null.
- If DataVault changed while disabled, re-enable could keep stale old Vault handles and the audio callback could keep reading old buffers.

What was done:
- Added `RebindDataVaultCold(IDataVault nextVault)`.
- `CacheDataVaultCold()` and DataVault hot-swap now use the same helper.
- The helper calls `DisposeVaultStorage()` before switching services; that path already fences `OnAudioFilterRead` through `BeginBankMutationCold()`.

Cinematic Cheats used:
- None. This is native/audio callback ownership. Vocal decode, mock bank, waveform telemetry, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` reports only existing LF/CRLF warning.
- Source grep confirms `RebindDataVaultCold`, no direct `_dataVault = GlobalRegistry.DataVault`, and no direct `_dataVault = currentService as IDataVault` route.
- Build/import/profiler/audio proof blocked: external `dotnet` PID `14348` still active, CPU sampled `35%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; service rebind may wait for an in-flight audio callback, only on cold/service replacement path.

## 2026-05-28 - Dynamic Music Disabled DataVault Rebind Fix

What was wrong:
- `DynamicMusicGranularSynthesizer` kept DataVault synth storage across disable while unregistering its hot-swap listener.
- `CacheDataVaultCold()` only rebound when `_dataVault` was null.
- If DataVault changed while disabled, re-enable could keep stale old synth handles; pending synth job/lock cleanup existed only on active hot-swap.

What was done:
- Added `RebindDataVaultCold(IDataVault nextVault)`.
- DataVault hot-swap and cold service refresh now share the helper.
- The helper force-completes pending synth jobs, releases old Vault storage through the old service, then assigns the new service.

Cinematic Cheats used:
- None. This is native/DSP ownership. Granular synthesis, tension/depth scalars, preset rules, grain bank, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Audio/Synthesis/DynamicMusic/DynamicMusicGranularSynthesizer.cs` reports only existing LF/CRLF warning.
- Source grep confirms `RebindDataVaultCold`, no direct `_dataVault = GlobalRegistry.DataVault`, and no direct `_dataVault = currentService as IDataVault` route.
- Build/import/profiler/audio proof blocked: no compiler process active, but CPU sampled `82%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; service rebind may wait for pending synth work only on cold/service replacement path.

## 2026-05-28 - Adaptive Stem Disabled DataVault Rebind Fix

What was wrong:
- `AdaptiveStemAudioMixer` kept DataVault stem mixer storage across disable while unregistering its hot-swap listener.
- `CacheDataVaultCold()` only rebound when `_dataVault` was null.
- If DataVault changed while disabled, re-enable could keep stale old stem/rule/telemetry handles.

What was done:
- Added `RebindDataVaultCold(IDataVault nextVault)`.
- DataVault hot-swap and cold service refresh now share the helper.
- The helper releases old Vault storage through the cached old service before assigning the new service.

Cinematic Cheats used:
- None. This is native/audio ownership. Streaming stems, fake depth filters, beat/biome rules, telemetry, CSV tuning, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs` reports only existing LF/CRLF warning.
- Source grep confirms `RebindDataVaultCold`, no direct `_dataVault = GlobalRegistry.DataVault`, no direct `_dataVault = currentService`, and no direct `_dataVault = replacementVault` route.
- Build/import/profiler/audio proof blocked: no compiler process active, but CPU sampled `98.3%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; service rebind runs only on cold enable/service replacement path.

## 2026-05-28 - Chemical Influence Vault Release Fix

What was wrong:
- `ChemicalInfluenceGrid` acquired 19 AISensory DataVault generation handles.
- Disable/DataVault rebind completed scheduled work, then `ResetVaultStateForRebind()` cleared those handles without `ReleaseBuffer`.
- The descriptor loss could leave chemical grid/emitter/telemetry/profile allocations resident in DataVault with no owner-side release route.

What was done:
- Added `ReleaseVaultHandles(IDataVault vault)` and `ReleaseVaultHandle<T>()`.
- `ResetVaultStateForRebind()` now releases owned AISensory buffers against the cached old `_dataVault` before clearing descriptors.
- The release helper refuses null/zero/stale non-AISensory handles and resets each descriptor after release.

Cinematic Cheats used:
- None. Chemical diffusion, scent channels, defoliant behavior, profile CSV, telemetry, and quality-weight scaling are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` reports only existing LF/CRLF warning.
- Source grep confirms `ReleaseVaultHandles`, `ReleaseVaultHandle`, and `ReleaseBuffer(in handle)` are wired before `ClearVaultHandles`.
- Build/import/profiler proof blocked: no compiler process active, but CPU sampled `81.16%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; release runs only on disable/DataVault service replacement.

## 2026-05-28 - Volcanic Updraft DataVault Rebind/Release Fix

What was wrong:
- `VolcanicUpdraftDirector` kept owned Fluid DataVault handles across disable while unregistering from hot-swap.
- Cold enable overwrote `_dataVault` directly from `GlobalRegistry.DataVault`, so disabled DataVault replacement could strand old handles.
- Active DataVault replacement and object destruction had no owned-buffer release route before handles were cleared or lost.

What was done:
- Cold dependency refresh now calls `RebindDataVault(GlobalRegistry.DataVault)`.
- DataVault rebind releases owned updraft handles before clearing descriptors and assigning the new service.
- Added `OnDestroy()` cleanup for resident handles.
- Release is filtered to `OwnerSystem`; external player/leviathan handles are only cleared, not released.

Cinematic Cheats used:
- None. Updraft physics, mock wakes, thermal service reads, CSV tuning, telemetry, and quality-weight scaling are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs` reports only existing LF/CRLF warning.
- Source grep confirms `ReleaseOwnVaultHandles`, `ReleaseOwnVaultHandle`, owner-filtered `ReleaseBuffer(in handle)`, and `RebindDataVault(GlobalRegistry.DataVault)`.
- Build/import/profiler proof blocked: external `dotnet` PID `34204`, `csc` PID `4764`, CPU `100%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; release runs only on DataVault replacement/destroy.

## 2026-05-28 - Ambient/Vehicle/Submarine DataVault Release Fixes

What was wrong:
- `AmbientBiotaDirector` cleared six owned AmbientBiota Vault handles on disable/DataVault replacement without `ReleaseBuffer`.
- `VehicleComponentDamageRuntime` kept damage Vault handles while disabled, missed DataVault hot-swap, and active hot-swap assigned the new Vault before release. Its external submarine config handle had to remain non-owned.
- `SubmarineDynamicsRuntime` had the same disabled stale DataVault route for main vehicle physics buffers, and its gyro partial owned additional Vault handles that were not cleared/released by the main lifecycle.

What was done:
- Added owner-filtered release-before-clear to `AmbientBiotaDirector`.
- Added lifecycle DataVault rebind and destroy release to `VehicleComponentDamageRuntime`.
- Added lifecycle DataVault rebind, destroy release, main handle release, gyro handle release, and gyro descriptor clearing to `SubmarineDynamicsRuntime`.
- External handles remain clear-only: vehicle damage does not release submarine config; submarine dynamics does not release vehicle damage read state.

Cinematic Cheats used:
- None. Biota drift/indirect draw, vehicle damage grid, submarine kinematics, added mass, gyro stabilization, CSV tuning, telemetry, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check` on all touched runtime files reports only existing LF/CRLF warnings.
- Source grep confirms release/rebind helpers in `AmbientBiotaDirector`, `VehicleComponentDamageRuntime`, `SubmarineDynamicsRuntime`, and `SubmarineDynamicsRuntime_Gyroscopes`.
- Build/import/profiler proof blocked: external `dotnet` PID `17744` active and CPU sampled `62%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; new work is limited to disable, destroy, cold service refresh, or DataVault replacement.

## 2026-05-28 - Abyssal Thermodynamics DataVault Release Fix

What was wrong:
- `AbyssalThermodynamicsSolver` owned main thermal Vault handles and reactor thermal Vault handles through `Acquire(... SystemID.Thermodynamics)`.
- Active DataVault replacement assigned the new Vault before releasing old handles.
- Disabled re-enable could keep a stale cached `_vault`.
- Reactor bridge descriptors were outside the main `ClearVaultHandles()` route.

What was done:
- Added `CompleteThermalJobsForLifecycle()` for solver/sample job fences and reactor shared lock release.
- Added lifecycle DataVault rebind for active hot-swap and cold `EnsureVault()`.
- Added owner-filtered release for main thermal handles and reactor thermal handles.
- Added reactor descriptor clearing and `OnDestroy()` release of resident handles.
- External optional power/fluid/airlock lanes remain lock-only inputs and are not released as owned buffers.

Cinematic Cheats used:
- None. Thermal grid, reactor heat injection, convergence telemetry, black-box dumps, visuals, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.cs Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs` reports only existing LF/CRLF warnings.
- Source grep confirms `RebindDataVaultForLifecycle`, `ReleaseOwnedVaultHandles`, `ReleaseReactorThermalVaultHandles`, and `ClearReactorThermalVaultHandles`.
- Build/import/profiler proof blocked: external `dotnet` PID `17744` active and CPU sampled `54%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; new work is limited to destroy, cold service refresh, or DataVault replacement.

## 2026-05-28 - Terminal OS DataVault Release Fix

What was wrong:
- `TerminalOsRuntime.DisposeNativeResources()` ran on disable, destroy, DataVault hot-swap, and failed native init.
- It cleared terminal/decryption/projection Vault handles without `ReleaseBuffer`.
- Terminal projection partial handles were opened by the same native lifecycle but were also only cleared.

What was done:
- Added owner-filtered release for main terminal UI Vault handles.
- Added owner-filtered release for terminal projection Vault handles.
- `DisposeNativeResources()` now releases old `_vault` handles before `ClearVaultHandles()` and `_vault = null`.

Cinematic Cheats used:
- None. Terminal rendering, terminal input projection, decryption puzzle state, telemetry, and quality-weight behavior are unchanged.

Verification:
- Scoped `git diff --check -- Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime.cs Assets/_Project/Scripts/UI/TerminalOS/TerminalOsRuntime_TerminalProjection.cs` reports only existing LF/CRLF warnings.
- Source grep confirms `ReleaseVaultHandles`, `ReleaseTerminalProjectionVaultHandles`, and `ReleaseBuffer(in handle)` before `ClearVaultHandles()`.
- Build/import/profiler proof blocked: external `dotnet` PID `17744`, `csc` PID `50392`, CPU sampled `100%`.

Exact Microseconds saved:
- Runtime: 0 us measured, 0 us claimed.
- Steady-frame delta: 0 us expected; release runs only inside existing dispose paths.
## 2026-05-28 - Content Authority Disabled DataVault Rebind

What was wrong:
- `ContentAuthorityRuntime` kept DataVault-backed content telemetry, pending-load, and bundle-ref counters resident while disabled, but disabled instances miss `GlobalRegistry.DataVault` hot-swap callbacks.
- `CacheDependencies()` only assigned `GlobalRegistry.DataVault` when `_dataVault == null`, so a re-enabled content authority could keep stale handles from the old Vault service.

What was done:
- Added `RebindDataVaultCold(...)` and routed cold dependency refresh plus active DataVault hot-swap through it.
- Rebound `ContentBundleReferenceCounter` through its own cached old Vault so bundle-ref handles release before the new Vault is assigned.
- Hardened content authority and bundle-ref release helpers to release descriptor-local `SystemID.ContentAuthority` generation handles only.

Cinematic Cheats used:
- None. This was native lifecycle ownership only; no content presentation, VRAM policy, Addressables route, or VFX prewarm behavior changed.

Exact Microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.
- Lifecycle win: prevents stale content Vault descriptors and DataVault memory growth after disabled service rebound.

## 2026-05-28 - GI Relay Disabled DataVault Rebind

What was wrong:
- `HectonGIRelaySystem` kept GI relay/DayNight Vault descriptors resident while disabled, but the disabled runtime unregisters its hot-swap listener.
- `CacheDataVaultCold()` used null-only `_vault` caching, so disabled DataVault replacement could resume lighting with stale old Vault descriptors.

What was done:
- Routed `CacheDataVaultCold()` through existing `RebindDataVault(GlobalRegistry.DataVault)`.
- Reused existing teardown: complete pending SH job, `DisposeNativeStorage()`, release GI relay and DayNight Vault descriptors, then bind the current Vault and hydrate when active.

Cinematic Cheats used:
- None. SH profile math, lightning overlay, ambient probe upload, DayNight gradient relay, and quality-weight policy are unchanged.

Exact Microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.
- Lifecycle win: prevents stale lighting Vault descriptors after disabled DataVault service rebound.

## 2026-05-28 - Toxic Outgassing Vault Release And Counter Reset

What was wrong:
- `ToxicOutgassingChemistryRuntime` cleared native handle descriptors on DataVault replacement without releasing its toxic-grid Vault buffers.
- `OnDestroy()` did not release resident toxic-grid handles.
- Disabled instances could miss DataVault replacement, and `_nativeReady` could return before comparing the cached Vault against `GlobalRegistry.DataVault`.
- Managed counters could survive after native buffers were rebound and cleared.

What was done:
- Added lifecycle rebind/release path for destroy and DataVault replacement.
- Added descriptor-local release for the toxic density/state/source/entity/signal/constants/telemetry/probe/header handles.
- Added top-of-`EnsureNativeState()` DataVault comparison to catch disabled service replacement before returning ready.
- Reset source/entity counts, telemetry cursor, density version, frame counter, accumulators, origin/rebase state, pending failure flags, and mock flag during native release/rebind.

Cinematic Cheats used:
- None. Toxic diffusion, mock flow/world sampling, toxicity signals, black-box layout, CSV/probe behavior, and quality scaling are unchanged.

Exact Microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.
- Lifecycle win: prevents toxic-grid DataVault leaks and stale managed counters after service rebound.

## 2026-05-28 - Flora/Fauna Symbiosis Vault Release

What was wrong:
- `ShinobuFloraFaunaSymbiosisSolver` cleared symbiosis Vault handles on DataVault replacement and dispose without releasing the owned buffers.
- The solver mixes owned symbiosis buffers with borrowed ambient/anomaly lanes, so a naive broad release would break other owners.

What was done:
- Added lifecycle rebind/release for dispose, DataVault hot-swap, and cold vault acquisition.
- Released descriptor-local owned `SystemID.AIEcology` symbiosis handles through the old Vault.
- Added ownership bits for fallback-created ambient entity/AUP buffers; borrowed ambient handles are only cleared.
- Left anomaly field borrowed-only.

Cinematic Cheats used:
- None. Symbiosis exchange math, scanner VFX, oxygen/adherence/seed/acoustic outputs, CSV/legacy paths, and quality behavior are unchanged.

Exact Microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.
- Lifecycle win: prevents symbiosis Vault leaks without releasing borrowed ecology lanes.

## 2026-05-28 - Shinobu Ecosystem Balancer Vault Release

What was wrong:
- `ShinobuEcosystemBalancer` owned ambient, flocking, spatial-grid, render, telemetry, CSV/legacy, dump, and species profile Vault handles but cleared descriptors on DataVault replacement/dispose without `ReleaseBuffer`.
- Cold activation used null-only `_dataVault` cache, so stale service references could survive outside the hot-swap path.

What was done:
- Added lifecycle rebind/release for dispose, DataVault hot-swap, and cold vault acquisition.
- Shutdown path now completes jobs, unlocks job buffers, stops the spatial-grid dump worker, releases descriptor-local owned `SystemID.AIEcology` handles, and clears cached state.
- DataVault service rebind preserves procedural render material/bounds/layer state; only hard dispose clears render state.
- `EnsureVaultState()` now checks `GlobalRegistry.DataVault` before trusting `_vaultBuffersReady`.

Cinematic Cheats used:
- None. Boid simulation, flocking, macro pass, render upload, spatial-grid forensics, CSV/legacy loading, and quality policy are unchanged.

Exact Microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.
- Lifecycle win: prevents ecology/spatial-grid Vault leaks and stale handles after service rebound.
## 2026-05-28 - Material/Fauna/Shadow Native Lifecycle Pass

What was wrong:
- `ShinobuMaterialResponseRuntime` cleared owned GraphicsMaterials Vault descriptors on shutdown/rebind without releasing buffers.
- `ProceduralCrabLegIKRuntime` and `LeviathanTentacleVerletSolver` could miss DataVault replacement while disabled, then clear old AnimationFauna descriptors during cold refresh.
- `AbyssalShadowCullingRuntime` had no DataVault hot-swap listener, so active shadow culling could keep a stale `_dataVault` after service replacement.

What was done:
- Added owner-filtered `ReleaseBuffer(in handle)` routes for `SystemID.GraphicsMaterials`, `SystemID.AnimationFauna`, and `SystemID.GraphicsScalability` descriptor sets.
- Routed cold startup/dependency refresh and active hot-swap through lifecycle rebind helpers instead of direct `_dataVault/_vault = GlobalRegistry.DataVault/currentService`.
- Completed pending material/fauna/shadow jobs before release, reset stale managed counters and dirty flags, and kept GraphicsBuffers on their existing hard-teardown lifetime.

Cinematic cheats used:
- No new simulation. The pass preserves existing visual fake/Math LOD behavior and spends no extra frame budget.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Risk removed is lifecycle memory growth/stale-handle writes, not a measured hot-path optimization.

Verification:
- Scoped `git diff --check` on four touched files reports only existing LF/CRLF warnings.
- Scoped grep for direct `_dataVault/_vault = GlobalRegistry.DataVault/currentService` in the touched files returns no hits.
- Scoped grep confirms owner-filtered `ReleaseBuffer(in handle)` routes.
- Build/import/profiler/GC proof blocked: no compiler processes active, but CPU sampled `97.31%`, above the project guard.
## 2026-05-28 - Macro Ecosystem Native Lifecycle Pass

What was wrong:
- `MacroEcosystemMathematicianRuntime` reset macro ecosystem DataVault descriptors on dispose/rebind without releasing owned AIEcology buffers.
- A failed resolve/reacquire path could overwrite a stale owned handle without first releasing it.

What was done:
- Added lifecycle rebind with existing job-completion and lock-unlock barriers.
- Added owner-filtered release for all macro ecosystem AIEcology handles.
- Released stale owned descriptors before reacquiring buffers.
- Reset telemetry, simulation tick, and CSV timestamp counters after replacement Vault binding.

Cinematic cheats used:
- No new simulation or fidelity cost. Existing FrostTick/quality-weight math remains unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/stale-handle risk only.

Verification:
- Scoped `git diff --check -- MacroEcosystemMathematicianRuntime.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms `RebindDataVaultForLifecycle`, owner-filtered `ReleaseOwnedVaultHandle`, and no direct `_vault = GlobalRegistry.DataVault/currentService` route.
- Build/import/profiler/GC proof blocked: external `dotnet` PID `50920` active and CPU sampled `86.47%`.

## 2026-05-28 - Laser/WFC/Habitat Native Lifecycle Pass

What was wrong:
- `LaserCutter` listened to many service replacements but not `DataVault`, leaving static `LaserCutterDodRuntime` and `WfcLaserCutRuntime` stale until a later equip/spawn initialization route.
- `WfcLaserCutRuntime` rebound native buffers without resetting managed grid/cursor/door/shader state.
- WFC/DOD/Habitat release helpers could delete a buffer from a descriptor without owner proof; Habitat could cache a non-Fluid generation handle during allocation-locked fallback.

What was done:
- Added `DataVault` hot-swap rebind in `LaserCutter` using the existing DOD/WFC initialization route and the replacement service.
- Added WFC managed-state reset on Vault null/replacement.
- Added owner-filtered `ReleaseBuffer` guards for `SystemID.GameplayTools` and `SystemID.Fluid`.
- Habitat allocation-locked fallback now rejects generation handles that do not match `BufferID` and `SystemID.Fluid` before caching them.

Cinematic cheats used:
- None. Cutter evaluation, WFC door feedback, Habitat flood math, signals, and quality-weight scaling are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale service and cross-owner release risk only.

Verification:
- Scoped `git diff --check` on `LaserCutter.cs`, `WfcLaserCutRuntime.cs`, `LaserCutterDodRuntime.cs`, and `HabitatFluidIncursionDirector.cs` reports only existing LF/CRLF warnings.
- Scoped grep confirms DataVault hot-swap trigger, WFC rebind reset, owner-filtered release guards, and Habitat borrowed-handle rejection.
- Build/import/profiler/GC proof blocked: external `dotnet` PID `56752`, `csc` PID `45932`, CPU sampled `98.52%`.

## 2026-05-28 - Sump Pump Drainage Native Lifecycle Pass

What was wrong:
- `SumpPumpPipeGridRuntime` released construction drainage Vault handles by descriptor alone.
- OnEnable and DataVault replacement assigned `_vault` directly instead of using a single lifecycle route.
- Managed drainage epoch state could survive after native buffers were released and rebound.

What was done:
- Added `BindDataVaultForLifecycle` for cold enable and DataVault service replacement.
- Replaced direct `ReleaseBuffer(in _handle)` calls with owner-filtered `ReleaseOwnedHandle`.
- Added `ResetRuntimeStateForVaultRelease` for frame index, topology/pressure state, flow dirty state, black-box flag, scheduled handles, locks, and debug counters.

Cinematic cheats used:
- None. CSR topology solve, two-pass pressure approximation, mock drainage generation, shader flow upload, and quality cadence are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle stale-state/cross-owner release risk only.

Verification:
- Scoped `git diff --check -- SumpPumpPipeGridRuntime.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms lifecycle bind helper, owner-filtered release, no direct `_vault = GlobalRegistry.DataVault/currentService`, and runtime state reset.
- Build/import/profiler/GC proof blocked by active compiler lane and CPU guard.

## 2026-05-28 - Autonomous Extractor Native Lifecycle Pass

What was wrong:
- `AutonomousExtractorSystem` still had direct DataVault assignment in active replacement/cold bind routes.
- Extractor SOA Vault release used nonzero descriptors without owner proof.
- Exact-handle validation matched buffer id and generation but not `SystemID.Construction`.

What was done:
- Added `RebindDataVaultForLifecycle` and routed active DataVault replacement plus cold bind through it.
- Owner-filtered extractor Vault releases.
- Made exact handle validation require `SystemID.Construction`.
- Removed direct `_dataVault = GlobalRegistry.DataVault/currentService` routes.

Cinematic cheats used:
- None. Extraction cadence, Burst SOA job, buffered item handling, and persistent drop commit behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle ownership risk only.

Verification:
- Scoped `git diff --check -- AutonomousExtractorSystem.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms lifecycle rebind, owner-filtered release, owner-aware exact handles, and no direct DataVault assignment route.
- Build/import/profiler/GC proof blocked by external compiler lane and CPU guard.

## 2026-05-28 - Bulkhead Containment Native Lifecycle Pass

What was wrong:
- `BulkheadContainmentRuntime.OnEnable` assigned `_vault` directly from `GlobalRegistry.DataVault`, bypassing the same lifecycle route used for active DataVault replacement.
- The shared bulkhead/hatch release helper called `ReleaseBuffer` for any nonzero descriptor.

What was done:
- Routed cold enable through `RequestDataVaultRebind(GlobalRegistry.DataVault)`.
- Hardened `ReleaseVaultHandle` so it releases only `SystemID.Construction` generation handles and always clears the local descriptor.
- Left hatch fluid/structural external handles as borrowed clear-only descriptors.

Cinematic cheats used:
- None. Bulkhead authority math, hatch pressure logic, shader upload, telemetry, CSV/profile load, and quality cadence are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle ownership risk only.

Verification:
- Scoped `git diff --check` reports only existing LF/CRLF warnings.
- Scoped grep confirms cold enable uses `RequestDataVaultRebind(GlobalRegistry.DataVault)`, no direct `_vault = GlobalRegistry.DataVault/currentService`, and owner-filtered `ReleaseBuffer(in handle)`.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `43436`, `csc` PID `10328`, CPU sampled `77.7%`.

## 2026-05-28 - Ecosystem Population Native Lifecycle Pass

What was wrong:
- `EcosystemPopulationBalancer` assigned `_dataVault` directly on cold enable and active DataVault replacement.
- Owned population buffers could be released by nonzero descriptor without `SystemID.AIEcology` proof.
- Existing owned generation handles could be cached without owner validation.

What was done:
- Added `RebindDataVaultForLifecycle` for enable, DataVault replacement, and teardown.
- Reset population managed epoch state when the native Vault changes.
- Owner-filtered releases and owned-handle reuse; borrowed entity AUP/flag handles remain clear-only.

Cinematic cheats used:
- None. Cold tick cadence, Burst population job, death signal lane, free-ring behavior, coefficient loading, and quality behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle ownership risk only.

Verification:
- Scoped `git diff --check -- EcosystemPopulationBalancer.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms lifecycle rebind route, owner checks, and no direct DataVault assignment route.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `43436`, CPU sampled `64.33%`.

## 2026-05-28 - Acoustic Echo Native Lifecycle Pass

What was wrong:
- `AcousticEchoLocationRuntime` cold bootstrap assigned `_dataVault` directly from `GlobalRegistry.DataVault`.
- Sensory-owned buffers could be released by any created descriptor without `SystemID.AISensory` proof.
- Cached sensory handles could be reused without expected buffer id/owner validation.

What was done:
- Routed bootstrap through existing `RebindDataVaultForLifecycle`.
- Added AISensory owner checks for buffer reuse and release.
- Kept echo enqueue, tracking job, read contracts, and black-box dump format unchanged.

Cinematic cheats used:
- None. Echo tap capacity, tracking behavior, black-box ring, and quality byte behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle ownership risk only.

Verification:
- Scoped `git diff --check -- AcousticEchoLocationRuntime.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms rebind bootstrap, AISensory owner checks, and no direct DataVault assignment route.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `43436`, `csc` PID `50892`, CPU sampled `100%`.

## 2026-05-28 - Procedural Bone Blender Native Lifecycle Pass

What was wrong:
- `ProceduralBoneBlenderRuntime` directly assigned `_dataVault` in cold refresh and active DataVault replacement.
- AnimationFauna Vault handles could be released without owner proof.
- Cached owned handles could be reused without expected buffer id/owner validation.

What was done:
- Added `BindDataVaultForLifecycle` for cold refresh and DataVault replacement.
- Owner-filtered AnimationFauna handle reuse and release.
- Kept GPU buffer lifetime separate; service replacement only marks upload/shader state dirty.

Cinematic cheats used:
- None. Solve job, matrix upload, emergency mock rig, shader globals, and quality behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle ownership risk only.

Verification:
- Scoped `git diff --check -- ProceduralBoneBlenderRuntime.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms lifecycle bind route, AnimationFauna owner checks, and no direct DataVault assignment route.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `43436`, CPU sampled `68.08%`.

Combined current-pass verification:
- `git diff --check` over `BulkheadContainmentRuntime.cs`, `EcosystemPopulationBalancer.cs`, `AcousticEchoLocationRuntime.cs`, `ProceduralBoneBlenderRuntime.cs`, and audit docs reports only LF/CRLF warnings.
- Direct `_dataVault/_vault = GlobalRegistry.DataVault/currentService` grep on the four source files returns no hits.
- Build/import/profiler/GC proof remains blocked by external `dotnet` PID `43436`, CPU sampled `99.42%`.

## 2026-05-28 - Stress-Driven Spawn Native Lifecycle Pass

What was wrong:
- `StressDrivenSpawnDirector` owned AIEcology Vault buffers but its dispose/DataVault replacement path could clear descriptors without releasing the owned generations.
- The same clear method also carries borrowed cognition/weather/scalability/macro descriptors, so a naive release-all patch would be a cross-owner deletion bug.

What was done:
- Added `ReleaseOwnedVaultHandles` and `ReleaseOwnedVaultHandle`.
- Released only descriptor-local owned AIEcology handles for rules, links, candidates, selection, input, tuning, telemetry, counters, CSV scratch, frustum planes, owned slots, inventory tickets, and spawn debug.
- Left borrowed handles clear-only and left spawn jobs, lock order, SignalBus routes, and simulation math unchanged.

Cinematic cheats used:
- None. This is lifecycle ownership repair only.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed service-churn leak/stale-native-state risk only.

Verification:
- Scoped grep confirms lifecycle rebind, owner-aware reuse, `ReleaseOwnedVaultHandle`, and no direct `_vault = GlobalRegistry.DataVault/currentService` in `StressDrivenSpawnDirector.cs`.
- Scoped `git diff --check` over current source/docs reports only LF/CRLF warnings.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `43436`, CPU sampled `95.35%`.

## 2026-05-28 - Base Atmosphere Native Lifecycle Pass

What was wrong:
- `BaseAtmosphereEngine` directly cached/replaced `_dataVault` from `GlobalRegistry.DataVault` and `currentService`.
- Its release helper called `ReleaseBuffer` for any created descriptor, without `SystemID.HabitatAtmosphere` ownership proof.
- The first lifecycle route version marked initial cold bind as pending rebind, which could reopen freshly allocated startup buffers on the first fixed tick.

What was done:
- Routed cold cache and DataVault hot-swap through `RebindDataVaultForLifecycle`.
- Added `IsOwnedVaultHandle` and released only descriptor-local HabitatAtmosphere handles.
- Added `HasNativeStateHandle` so pending rebind is scheduled only when an old native state actually existed.

Cinematic cheats used:
- None. Atmosphere solve math, seeding, solve budget, quality cadence, flags, and black-box format are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/stale-state risk and avoided redundant first-tick cold churn.

Verification:
- Scoped `git diff --check -- BaseAtmosphereEngine.cs` reports only existing LF/CRLF warning.
- Direct `_dataVault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms `RebindDataVaultForLifecycle`, `HasNativeStateHandle`, `IsOwnedVaultHandle`, and owner-filtered `ReleaseBuffer(in handle)`.

## 2026-05-28 - Base Atmosphere Logistics Native Lifecycle Pass

What was wrong:
- `BaseAtmosphereLogisticsRuntime` directly cached `_vault = GlobalRegistry.DataVault` on startup.
- Shutdown and service replacement cleared/nullified Vault state without releasing owned HabitatAtmosphere logistics buffers.
- Release needed descriptor-local owner proof because HabitatAtmosphere is shared by multiple atmosphere systems.

What was done:
- Routed startup through `ApplyVaultRebind`.
- Added `ReleaseVaultHandles`, `ReleaseVaultHandle`, `IsOwnedVaultHandle`, and `ClearVaultHandles`.
- Released cells, nodes, CSR edges, consumers, toxic sources, vents, counters, tuning, telemetry, delta lanes, remainders, shader payload, and editor CSV/profile handles only with `SystemID.HabitatAtmosphere` proof.

Cinematic cheats used:
- None. Logistics solve, topology, jobs, lock order, telemetry, shader payload, CSV/profile paths, and quality behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed shutdown/service-churn native leak risk only.

Verification:
- Scoped `git diff --check -- BaseAtmosphereLogisticsRuntime.cs` reports only existing LF/CRLF warning.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms `ApplyVaultRebind`, `IsOwnedVaultHandle`, `OwnerSystemId`, and owner-filtered `ReleaseBuffer(in handle)`.

Combined current verification:
- `git diff --check` over the two atmosphere files and AUDIT_NATIVE_STATE docs reports only LF/CRLF warnings.
- Direct DataVault assignment grep on the two atmosphere files returns no hits.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `19756`, `csc` PID `50412`, CPU sampled `100%`.

## 2026-05-28 - Foveated Render Commander Native Lifecycle Pass

What was wrong:
- `FoveatedRenderCommander` assigned `_dataVault` directly from `GlobalRegistry.DataVault` and `currentService`.
- Telemetry black-box reuse accepted `TryGetGenerationHandle` output without checking `SystemID.GraphicsScalability`.
- Telemetry release used created-descriptor proof only.

What was done:
- Added `RebindDataVaultForLifecycle` and routed enable/DataVault replacement through it.
- Required `BufferID.FoveatedRenderBlackBox` plus `SystemID.GraphicsScalability` before caching an existing telemetry handle.
- Released telemetry only with the same owner proof and cleared local descriptor/generation state.

Cinematic cheats used:
- None. XR foveation policy, Quest lock, gaze fallback, UI camera fail-closed behavior, SignalBus reads, and telemetry DTO layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/cross-owner release risk only.

Verification:
- Scoped `git diff --check -- FoveatedRenderCommander.cs` reports only existing LF/CRLF warning.
- Direct `_dataVault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms lifecycle rebind, owner-aware existing-handle reuse, and owner-filtered `ReleaseBuffer(in handle)`.
- Build/import/profiler/XR proof blocked by external `dotnet` PID `63132`, `VBCSCompiler` PID `21776`, CPU sampled `100%`.

Combined current-pass verification:
- `git diff --check` over `BaseAtmosphereEngine.cs`, `BaseAtmosphereLogisticsRuntime.cs`, `FoveatedRenderCommander.cs`, and AUDIT_NATIVE_STATE docs reports only LF/CRLF warnings.
- Direct `_dataVault/_vault = GlobalRegistry.DataVault/currentService` grep on the three source files returns no hits.
- Build/import/profiler/GC proof remains blocked by external `dotnet` PID `56508`, `VBCSCompiler` PID `21776`, CPU sampled `100%`.

## 2026-05-28 - Visual Pressure Aging Native Lifecycle Pass

What was wrong:
- `VisualPressureAgingRuntime` startup assigned `_vault` directly from `GlobalRegistry.DataVault`.
- DataVault replacement had a separate release/bind sequence instead of one lifecycle route.

What was done:
- Added `RebindDataVaultForLifecycle`.
- Routed startup and DataVault replacement through the same release/bind route.
- Kept existing `SystemID.GraphicsMaterials` owner-filtered release/reuse checks.

Cinematic cheats used:
- None. Visual aging/degradation jobs, GPU buffers, structural/thermal borrowed inputs, dump streams, shader payloads, and quality scaling are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale Vault route risk only.

Verification:
- Scoped `git diff --check -- VisualPressureAgingRuntime.cs` reports only existing LF/CRLF warning.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms lifecycle rebind and owner-filtered `ReleaseBuffer(in handle)`.

## 2026-05-28 - Instance Culling Telemetry Native Lifecycle Pass

What was wrong:
- `InstanceCullingService` cached `_dataVault` only on null and did not react to DataVault service replacement.
- Telemetry ring validation/release used BufferID+Generation without `SystemID.GraphicsScalability` proof.

What was done:
- Implemented `IGlobalRegistryHotSwapListener`.
- Added hot-swap registration/unregistration and `RebindDataVaultForLifecycle`.
- Required `SystemID.GraphicsScalability` for telemetry handle reuse and release.

Cinematic cheats used:
- None. Compute culling, HLOD dispatch, GPU buffers, async readback, shader IDs, AUP shift job, and black-box file layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale telemetry Vault/cross-owner release risk only.

Verification:
- Scoped `git diff --check -- InstanceCullingService.cs` reports only existing LF/CRLF warning.
- Direct `_dataVault = GlobalRegistry.DataVault` grep returns no hits.
- Grep confirms listener route, lifecycle rebind, and owner-filtered `ReleaseBuffer(in handle)`.

Combined graphics telemetry verification:
- `git diff --check` over `VisualPressureAgingRuntime.cs`, `InstanceCullingService.cs`, and AUDIT docs reports only LF/CRLF warnings.
- Direct DataVault assignment grep on both graphics files returns no hits.
- Build/import/profiler/GPU proof blocked by external `dotnet` PID `37628`, `csc` PID `45708`, `VBCSCompiler` PID `21776`, CPU sampled `100%`.

## 2026-05-28 - Light Shaft VFX Vault Owner Predicate Pass

What was wrong:
- `ScreenSpaceLightShaftRuntime` could accept existing light-shaft Vault handles without explicit `SystemID.Vfx` proof.
- Owned release checked generation but did not explicitly require the VFX owner on both local and current descriptors.

What was done:
- Required `SystemID.Vfx` for local handle reuse.
- Required `SystemID.Vfx` for existing handle adoption from `TryGetGenerationHandle`.
- Required `SystemID.Vfx` before release of owned top/history/telemetry handles.

Cinematic cheats used:
- None. The screen-space shaft fake, brownout/load-shed behavior, camera route, SignalCorridor path, shader globals, and telemetry layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed ownership ambiguity only.

Verification:
- Scoped `git diff --check -- ScreenSpaceLightShaftRuntime.cs` reports only existing LF/CRLF warning.
- Grep confirms `SystemID.Vfx` owner predicates before reuse/release and owner-filtered `ReleaseBuffer(in handle)`.

## 2026-05-28 - UberNoir Shader Telemetry Native Lifecycle Pass

What was wrong:
- `HectonUberNoirRuntimeBridge` assigned `_dataVault` directly during DataVault replacement.
- Shader telemetry ring handles were accepted and released by BufferID+Generation without `SystemID.GraphicsScalability` proof.

What was done:
- Added `RebindDataVaultForLifecycle` for cold refresh and DataVault replacement.
- Required `SystemID.GraphicsScalability` for telemetry handle reuse/read/write/release.
- Reset telemetry cursor when the Vault native epoch changes.

Cinematic cheats used:
- None. Feature-mask math, stress shedding, visual-overkill scalar, shader globals, dump header, and DTO layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed shader telemetry lifecycle risk only.

Verification:
- Scoped `git diff --check -- HectonUberNoirRuntimeBridge.cs` reports only existing LF/CRLF warning.
- Direct `_dataVault = currentService/GlobalRegistry.DataVault` grep returns no hits.
- Grep confirms lifecycle rebind and owner-filtered `ReleaseBuffer(in handle)`.

Combined light/render verification:
- `git diff --check` over `ScreenSpaceLightShaftRuntime.cs`, `HectonUberNoirRuntimeBridge.cs`, and AUDIT docs reports only LF/CRLF warnings.
- Build/import/profiler/render proof remains blocked by external `dotnet` PID `33312`, CPU sampled `71%`.

Combined current continuation verification:
- `git diff --check` over 7 source files and AUDIT docs reports only LF/CRLF warnings.
- Direct `_vault/_dataVault = GlobalRegistry.DataVault/currentService` grep on the 7 source files returns no hits.
- Build/import/profiler/GC proof blocked by external `dotnet` PID `33312`, CPU sampled `71%`.

## 2026-05-28 - Interior GI Probe Native Lifecycle Pass

What was wrong:
- `InteriorGIProbeVolumeRuntime` assigned `_vault` directly from `GlobalRegistry.DataVault` and directly during DataVault service replacement.
- The GI release helper used only nonzero `BufferID`/generation before `ReleaseBuffer`, without passing the expected descriptor id or proving `SystemID.GraphicsScalability`.

What was done:
- Added `RebindDataVaultForLifecycle` and routed cold cache plus hot DataVault replacement through it.
- Kept teardown on the old Vault before binding the replacement Vault.
- Changed each GI probe release call to pass its expected `BufferID`, and release only when `IsInteriorGIHandle` proves expected buffer id, `SystemID.GraphicsScalability`, and nonzero generation.

Cinematic cheats used:
- None. Existing interior GI remains the same bounded probe/grid visual fake. Propagation, mock seeding, telemetry, CSV/profile overrides, GPU upload, and shader payload are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/cross-owner release risk only.

Verification:
- Scoped `git diff --check -- InteriorGIProbeVolumeRuntime.cs` reports only existing LF/CRLF warning.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms lifecycle rebind and owner-filtered `ReleaseBuffer(in handle)`.

## 2026-05-28 - Global Shader Dispatcher DataVault Epoch Pass

What was wrong:
- `GlobalShaderDispatcher` assigned `_vault` directly from `GlobalRegistry.DataVault` and directly on DataVault replacement.
- Service replacement invalidated the slot handle cache but preserved `_binaryProbeCompleted` and `_generatedEmergencyGlobals`, so a replacement `ShaderGlobalState` buffer could skip the emergency/global bootstrap path.

What was done:
- Added `RebindDataVaultForLifecycle` for cold cache, disable, and service replacement.
- Rebind invalidates cached shader slot handles and resets telemetry cursor/frame plus binary-probe/emergency-seed flags.
- Active DataVault replacement now ensures shader slots and reruns `RunBinaryGraveyardProbeCold` against the replacement Vault.

Cinematic cheats used:
- None. The existing shader-global route remains a bounded visual fake lane. Slot layout, thermal anomaly upload, feature masks, physiology shader fakes, and command-buffer dispatch are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale native epoch bootstrap risk only.

Verification:
- Scoped `git diff --check -- GlobalShaderDispatcher.cs` reports only existing LF/CRLF warning.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms lifecycle rebind and binary probe rerun on DataVault replacement.

Combined interior/render verification:
- `git diff --check` over `InteriorGIProbeVolumeRuntime.cs`, `GlobalShaderDispatcher.cs`, and AUDIT docs reports only LF/CRLF warnings.
- Direct `_vault/_dataVault = GlobalRegistry.DataVault/currentService` grep on both source files returns no hits.
- Remaining `ReleaseBuffer(in handle)` in the scoped grep is the Interior GI release path behind `IsInteriorGIHandle`.
- Build/import/profiler/native-ledger proof is blocked by external `dotnet` PID `40436`, CPU sampled `100%`.

## 2026-05-28 - Nutrient Drift Native Lifecycle Pass

What was wrong:
- `NutrientDriftRuntime` assigned `_vault` directly during activation and DataVault service replacement.
- Main and carrion Vault release helpers could release descriptors without proving `SystemID.AIEcology` at the final release predicate.
- Release sites did not pass the expected `BufferID`, so descriptor corruption/reuse would be harder to catch locally.

What was done:
- Added `RebindDataVaultForLifecycle` and routed `Activate`, `Dispose`, and DataVault replacement through it.
- Lifecycle rebind now fences scheduled drift work before releasing old Vault descriptors against the old service.
- `ReleaseVaultHandle` now requires an expected `BufferID`, and `IsMatchingVaultHandle` requires expected `BufferID`, nonzero generation, and `SystemID.AIEcology`.
- Updated carrion release sites to pass explicit carrion buffer ids.

Cinematic cheats used:
- None. Nutrient/carrion drift math, source profiles, attraction records, telemetry, and quality-weight behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/cross-owner release risk only.

Verification:
- Scoped `git diff --check -- NutrientDriftRuntime.cs NutrientDriftRuntime_Carrion.cs` reports only existing LF/CRLF warnings.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Grep confirms lifecycle rebind, AIEcology owner check, and expected-`BufferID` release calls.
- Build/import/profiler/GC proof is still pending behind the project compiler/CPU guard.

Combined current patch verification:
- `git diff --check` over `InteriorGIProbeVolumeRuntime.cs`, `GlobalShaderDispatcher.cs`, `NutrientDriftRuntime.cs`, `NutrientDriftRuntime_Carrion.cs`, and AUDIT docs reports only LF/CRLF warnings.
- Direct `_vault/_dataVault = GlobalRegistry.DataVault/currentService` grep on the four source files returns no hits.
- `Get-Process dotnet,csc,VBCSCompiler` listed no compiler process, but CPU sampled `100%`.
- No build, native ledger, Unity import, Play Mode, profiler, or GC proof was launched under the CPU guard.

## 2026-05-28 - Storm Propagation Native Lifecycle Pass

What was wrong:
- `ShinobuStormPropagationRuntime` replaced `_vault` directly on DataVault service replacement.
- Old owned HabitatAtmosphere storm descriptors were cleared without `ReleaseBuffer`.
- `Dispose()` completed jobs but did not release owned Vault descriptors.

What was done:
- Added `RebindDataVaultForLifecycle` for DataVault service replacement.
- Added owner-filtered release for every descriptor-local storm buffer using expected `BufferID`, nonzero generation, and `SystemID.HabitatAtmosphere`.
- Borrowed ocean weather state is cleared only, not released.
- Added `OnDestroy -> Dispose` and release-on-dispose for the owned storm buffers.
- Reset managed native-epoch counters when binding a replacement Vault.

Cinematic cheats used:
- None. Existing storm propagation remains the same deterministic visual/audio scalar fake. Propagation jobs, scalar rows, telemetry dump, and quality cadence are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/cross-owner release risk only.

Verification:
- `git diff --check -- ShinobuStormPropagationRuntime.cs` reports only existing LF/CRLF warning.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Source diff confirms lifecycle rebind and owner-filtered `ReleaseBuffer(in handle)`.
- No compiler process was listed, but CPU sampled `100%`; build/import/profiler/native-ledger proof was not launched.

## 2026-05-28 - Surface Weather Native Output Lifecycle Pass

What was wrong:
- `HectonSurfaceWeatherDirector` cached `GlobalRegistry.DataVault` directly and ignored DataVault service replacement.
- `SurfaceWeatherJobOutput` release used only nonzero descriptor proof, not `SystemID.HabitatAtmosphere`.
- `OnDisable` released the output buffer while `_runtimeStateInitialized` stayed true, so re-enable on the same Vault could skip output recreation.

What was done:
- Added `RebindDataVaultForLifecycle` for cold cache and DataVault replacement.
- Added same-Vault cold rehydrate so disable/enable recreates and seeds the output buffer when runtime state already exists.
- Added `IsWeatherJobOutputHandle` owner predicate for resolve and release.

Cinematic cheats used:
- None. Weather profile selection, screen-space rain, thunder/lightning, ocean binding, shader/VFX writes, and quality behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle output loss/stale Vault risk only.

Verification:
- `git diff --check -- HectonSurfaceWeatherDirector.cs` reports only existing LF/CRLF warning.
- Scoped grep confirms lifecycle rebind, same-Vault output rehydrate, owner-filtered release, and no direct `_dataVault = GlobalRegistry.DataVault/currentService` route.
- Build/import/profiler proof blocked by external `dotnet` PID `29512`, `csc` PID `31964`, CPU `100%`.

## 2026-05-28 - Gas Dynamics Native Owner Predicate Pass

What was wrong:
- `GasDynamicsSolver` cached `GlobalRegistry.DataVault` directly and service replacement manually assigned `_dataVault`.
- Gas lane release accepted any nonzero descriptor.
- Telemetry release checked only `GasDynamicsTelemetryRing` BufferID.
- Gas and telemetry resolve/lock paths did not reject non-`SystemID.HabitatAtmosphere` descriptors.

What was done:
- Added `RebindDataVaultForLifecycle` for cold cache and DataVault replacement.
- Added `SystemID.HabitatAtmosphere` checks to ensure/read/read-only/write-lock and telemetry routes.
- Changed gas release to require an expected `BufferID` and `SystemID.HabitatAtmosphere`.
- Passed expected buffer ids for all room/base/bulkhead gas lanes.
- Telemetry ring release now uses the same owner predicate.

Cinematic cheats used:
- None. Gas diffusion, base transition handling, toxicity signals, telemetry DTOs, and quality behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale/foreign Vault descriptor risk only.

Verification:
- `git diff --check -- GasDynamicsSolver.cs` reports only existing LF/CRLF warning.
- Direct `_dataVault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Scoped grep confirms `RebindDataVaultForLifecycle`, owner checks, and expected-`BufferID` release calls.
- Build/import/profiler proof blocked by external `dotnet` PID `16780`, CPU `100%`.

Combined current continuation verification:
- `git diff --check` over `InteriorGIProbeVolumeRuntime.cs`, `GlobalShaderDispatcher.cs`, `NutrientDriftRuntime.cs`, `NutrientDriftRuntime_Carrion.cs`, `ShinobuStormPropagationRuntime.cs`, `HectonSurfaceWeatherDirector.cs`, `GasDynamicsSolver.cs`, and AUDIT docs reports only LF/CRLF warnings.
- Direct `_vault/_dataVault = GlobalRegistry.DataVault/currentService` grep across the 7 source files returns no hits.
- `Get-Process dotnet,csc,VBCSCompiler` listed external `dotnet` PID `16464`; CPU sampled `100%`.
- No build, native ledger, Unity import, Play Mode, profiler, or GC proof was launched under the compiler/CPU guard.

## 2026-05-28 - Dynamic Point Light Culling Native Lifecycle Pass

What was wrong:
- `DynamicPointLightCullingDirector` cached `GlobalRegistry.DataVault` directly.
- DataVault service replacement manually assigned `_vault`.
- Descriptor-local releases used nonzero generation only; they did not require expected `BufferID` and `SystemID.GraphicsScalability`.
- `ShutdownRuntime` released `_profileRules` but kept `_profileRuleCount`, so same-Vault disable/enable could use a stale count against fresh/uninitialized profile storage.

What was done:
- Added `RebindDataVaultForLifecycle` for cold cache and DataVault replacement.
- The rebind route completes pending culling work, unlocks owned write windows, releases old Vault descriptors through the old Vault, and resets native-epoch counters.
- `ReleaseDynamicPointLightVaultHandle` now requires expected `DynamicPointLightCullingVaultIds.*` plus `SystemID.GraphicsScalability` before calling `ReleaseBuffer`.
- `ShutdownRuntime` now resets profile count, telemetry cursor, timeout state, pending handle, and native-ready flags after releasing Vault descriptors.

Cinematic cheats used:
- None. Dynamic light culling math, mock SDF fake, probe-bounce stream, continuous quality cadence, and GPU payload layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed lifecycle leak/cross-owner release/stale profile-read risk only.

Verification:
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep on `DynamicPointLightCullingDirector.cs` returns no hits.
- `ReleaseDynamicPointLightVaultHandle` calls all pass expected `DynamicPointLightCullingVaultIds.*`.
- `git diff --check -- DynamicPointLightCullingDirector.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `55080`, CPU `100%`.

## 2026-05-28 - Ocean Single Pass / Shoreline Foam Native Lifecycle Pass

What was wrong:
- `OceanSinglePassRuntime` cached `_vault` directly from `GlobalRegistry.DataVault`.
- Ocean did not listen for DataVault replacement.
- Ocean-owned handles resolved/released with generation-only validation.
- `ShorelineFoamGraftRuntime.Shutdown()` released GPU state but left static Vault handles alive.
- Shoreline acquire/resolve could reuse or release stale descriptors across a DataVault epoch.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `OceanSinglePassRuntime`.
- Added `RebindDataVaultForLifecycle` for cold cache and DataVault replacement.
- Ocean-owned handles now require expected `BufferID`, `SystemID.HabitatAtmosphere`, and generation before resolve/release.
- Propwash handles remain borrowed; they validate expected `BufferID` plus generation and are never released by Ocean.
- Changed `ShorelineFoamGraftRuntime.Shutdown(IDataVault)` to release static owned Vault handles through the old Vault.
- Shoreline acquire/resolve/release now requires expected `ShorelineFoamConstants.*` buffer id and `SystemID.HabitatAtmosphere`.

Cinematic cheats used:
- None. Wake resolution scaling, guillotine foam visual fake, shoreline foam shader cap, profile CSV, and telemetry layout are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed stale Vault/foreign release/native leak risk only.

Verification:
- Scoped grep finds no direct `_vault = GlobalRegistry.DataVault/currentService`.
- Scoped grep finds no old `IsHandleValid`, no no-arg `ShorelineFoamGraftRuntime.Shutdown()`, and no ownerless `ReleaseVaultHandle(vault, ref _)`.
- `git diff --check -- OceanSinglePassRuntime.cs ShorelineFoamGraftContracts.cs` reports only existing LF/CRLF warnings.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `55080`, CPU `100%`.

## 2026-05-28 - Plasma Beam Native Lifecycle Pass

What was wrong:
- `ShinobuPlasmaBeamRuntime` initialized `_vault` directly from `GlobalRegistry.DataVault`.
- DataVault replacement completed scheduled work and reset flags, but did not release owned Vault handles.
- `Shutdown()` also reset managed state without releasing nine `SystemID.Vfx` buffers.
- `TryResolveVaultBuffer` checked BufferID only, not owner or generation.

What was done:
- Added `RebindDataVaultForLifecycle` and routed initialization/DataVault replacement through it.
- Rebind completes scheduled simulation, unlocks owned write windows, releases old Vault descriptors through the old Vault, then resets native-epoch counters.
- Shutdown now releases all owned Vault handles before clearing `_vault`.
- Resolve/release now require expected `BufferID`, `SystemID.Vfx`, and nonzero generation.

Cinematic cheats used:
- None. Procedural beam mesh math, acoustic taps, SignalBus lane, indirect draw path, CSV tuning, and Math LOD are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed native leak/stale descriptor risk only.

Verification:
- Scoped grep confirms no direct `_vault = GlobalRegistry.DataVault/currentService`.
- Release helper is owner-filtered by `IsOwnedHandle`.
- `git diff --check -- ShinobuPlasmaBeamRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `55080`, CPU `100%`.

## 2026-05-28 - Parasite Swarm Native Lifecycle Pass

What was wrong:
- `ParasiteSwarmGpuRuntime` cached `_vault` directly from `GlobalRegistry.DataVault`.
- DataVault replacement completed target work and cleared descriptors, but did not release owned parasite Vault buffers from the old Vault.
- Read/resolve helpers accepted handle BufferID without proving `SystemID.Vfx` and generation.
- Write-lock routes could attempt locks on stale/foreign descriptors before failing on buffer shape.

What was done:
- Routed cold startup and DataVault service replacement through `RebindDataVaultForLifecycle`.
- Rebind now completes target extraction, releases held target write locks, releases ten parasite `SystemID.Vfx` Vault lanes through the old Vault with expected `BufferID`/owner/generation proof, then clears and rebinds descriptors.
- Startup no longer performs direct `_vault = GlobalRegistry.DataVault`.
- Descriptor bind, CSV scratch lock, tuning seed lock, target/telemetry write locks, and read/resolve helpers now fail closed unless the handle proves expected `BufferID`, `SystemID.Vfx`, and nonzero generation.

Cinematic cheats used:
- None. GPU swarm compute, target extraction, thermal/curl/noise fake, particle budget scaling, CSV profile parsing, and black-box dump format are unchanged.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed old-Vault leak, stale descriptor, and foreign handle risk only.

Verification:
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep on `ParasiteSwarmGpuRuntime.cs` returns no hits.
- Scoped grep confirms owner-filtered release/read/resolve/write-lock paths.
- `git diff --check -- ParasiteSwarmGpuRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `55080` running `dotnet build Hecton8.slnx --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`, CPU `100%`.

## 2026-05-28 - Jacobian Foam Native Lifecycle Pass

What was wrong:
- `JacobianFoamGpuRuntime` cached `_vault` directly from `GlobalRegistry.DataVault`.
- DataVault replacement assigned `_vault = currentService as IDataVault`, cleared descriptors, and did not release old foam Vault buffers.
- Resolve/read-pin/write-lock helpers accepted BufferID+generation without proving `SystemID.Vfx`.
- Deferred telemetry dump could be lost if the old Vault was cleared before flush.

What was done:
- Routed `ColdBindDataVault`, cold cache, and DataVault service replacement through `RebindDataVaultForLifecycle`.
- Rebind flushes deferred telemetry through the old Vault, releases six owned foam `SystemID.Vfx` lanes with expected `BufferID`/owner/generation proof, clears descriptors, then binds the replacement Vault.
- Descriptor binding, read pins, resolves, write locks, and releases now require `IsOwnedHandle`.
- Existing GPU buffers, RTHandles, RenderGraph payload, resolution logic, and foam shader dispatch path are unchanged.

Cinematic cheats used:
- None. Existing Jacobian foam fake, wake-count curve, resolution scaling, RenderGraph history ping-pong, and texture format fallback were preserved.

Exact microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. Removed old-Vault leak/stale descriptor/foreign handle risk only.

Verification:
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep on `JacobianFoamGpuRuntime.cs` returns no hits.
- Scoped grep confirms `IsOwnedHandle` is used by release, bind, resolve, read-pin, and write-lock paths.
- `git diff --check -- JacobianFoamGpuRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked because no compiler process was listed but CPU sampled `63%`, above the 50% build guard.

## 2026-05-28 - Combined VFX Native Lifecycle Verification

What was wrong:
- No new source defect found in the combined verification step; this was a proof gate after the ParasiteSwarm and JacobianFoam edits.

What was done:
- Ran combined whitespace/static checks over both VFX source files and AUDIT docs.
- Re-ran direct DataVault assignment grep over `ParasiteSwarmGpuRuntime.cs` and `JacobianFoamGpuRuntime.cs`.
- Sampled compiler/CPU state before any build.

Cinematic cheats used:
- None. Verification only.

Exact microseconds saved:
- Measured: 0 us.

Verification:
- Combined `git diff --check` reports only existing LF/CRLF warnings.
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep over both VFX source files returns no hits.
- Build/import/profiler/native-ledger proof blocked: external `dotnet` PID `29008` appeared during guard sampling and CPU sampled `100%`.

## 2026-05-28 - Internal Flood Waterline Native Lifecycle Pass

What was wrong:
- `InternalFloodWaterlineRuntime` directly cached `GlobalRegistry.DataVault`.
- Telemetry release could run without proving expected `BufferID.InternalFloodWaterlineTelemetryRing`, `SystemID.UI`, and generation.
- DataVault epoch replacement did not use one reset path for telemetry cursor and black-box dump state.

What was done:
- Cold cache and service replacement now route through `BindDataVaultForLifecycle`.
- `ReleaseTelemetryHandle` is the only telemetry release path and checks expected buffer, owner system, and generation.
- Telemetry write/read/dump validity now uses the same owner predicate.

Cinematic cheats used:
- None. Existing waterline/droplet shader fake and presentation cadence are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; change is cold lifecycle plus scalar descriptor guards.

Verification:
- Direct `_dataVault = GlobalRegistry.DataVault/currentService` and old `IsVaultHandleCreated` grep returns no hits.
- The remaining `ReleaseBuffer(in _telemetryHandle)` is inside `ReleaseTelemetryHandle` behind `IsTelemetryHandleOwned`.
- `git diff --check -- InternalFloodWaterlineRuntime.cs` reports only existing LF/CRLF warning.
- Combined source/doc `git diff --check` reports only existing LF/CRLF warnings.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `32028` and CPU `97%`.

## 2026-05-28 - Diegetic Visor Lens Native Lifecycle Pass

What was wrong:
- `DiegeticVisorLensRuntime` still used direct `_vault = currentService as IDataVault` and `_vault = GlobalRegistry.DataVault`.
- Native epoch replacement could keep `_binaryProbePerformed` and `_blackBoxDumped` true after their Vault buffers were released/recreated.

What was done:
- Added `RebindDataVaultForLifecycle` for cold cache and DataVault replacement.
- Rebind completes scheduled work, releases ten `SystemID.Vfx` visor Vault lanes through the old Vault, binds the replacement, then resets native epoch flags.
- Existing read/write/release helpers continue to require expected `BufferID`, `SystemID.Vfx`, and generation.

Cinematic cheats used:
- None. Existing visor condensation/crack/dirt shader fake, emergency mock data, and constant-buffer upload behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; fix is cold lifecycle/state hygiene only.

Verification:
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- `ReleaseVisorVaultHandle` is still guarded by `IsVisorVaultHandle`.
- `git diff --check -- DiegeticVisorLensRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PIDs `32028` and `33780`, CPU `100%`.

## 2026-05-28 - Volumetric Particulate Fog Native Lifecycle Pass

What was wrong:
- `VolumetricFogPass` used nonzero `BufferID` and raw Vault calls for four owned `SystemID.Vfx` fog lanes.
- `ReleaseVaultHandles` could release stale or foreign descriptors through `_vault`.
- Read/write/dump paths did not consistently prove expected buffer, owner, and generation before resolving or locking.

What was done:
- Added owner-filtered helpers for fog params, point lights, telemetry ring, and extinction profiles.
- Read, write-lock, release, dump, and `HasNativeState` paths now require expected `BufferID`, `SystemID.Vfx`, and generation.
- Invalid handles are replaced by allocation without releasing foreign descriptors.

Cinematic cheats used:
- None. Existing Dear Lie proxy, raymarch scale, RenderGraph textures, and point-light GPU upload behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; fix is descriptor validation and cold release hygiene only.

Verification:
- Grep for raw private-handle `TryReadOnlyHandle`, `TryAcquireWriteLock`, `ReleaseBuffer`, direct `_vault = GlobalRegistry.DataVault/currentService`, and old `BufferID == 0u` gates returns no hits.
- `git diff --check -- HectonVolumetricParticulateFogFeature.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `66408`, CPU `85%`.

## 2026-05-28 - Camera Juice Telemetry Native Lifecycle Pass

What was wrong:
- `CameraJuiceSystem` accepted telemetry handles with nonzero BufferID/generation but no owner proof.
- Hot telemetry writes used mutable `TryResolveHandle` instead of a DataVault write lock.
- Owned telemetry release did not prove `SystemID.Vfx`.
- Telemetry dump state could remain closed after native ring replacement.

What was done:
- Added `CameraJuiceOwnerSystemId` and expected `BufferID.CameraJuiceTelemetryRing`/owner/generation validation.
- Borrow/acquire/read/release paths now reject foreign or stale telemetry handles.
- `RecordCameraJuiceTelemetry` writes under `TryAcquireWriteLock` and releases in `finally`.
- Native epoch reset now clears cursor and dump request/dumped flags.

Cinematic cheats used:
- None. Procedural shake, FOV, speed-line, and postprocess behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: unmeasured scalar write-lock overhead only on telemetry write; correctness fix, not optimization.

Verification:
- Grep for old `OpenCameraJuiceTelemetryForWrite`, old `IsVaultHandleCreated`, and raw telemetry `TryResolveHandle` routes returns no hits.
- `git diff --check -- CameraJuiceSystem.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `40988`, CPU `100%`.

## 2026-05-28 - Carve Debris Native Lifecycle Pass

What was wrong:
- `CarveDebrisComputeRenderer` directly cached `_registryDataVault = GlobalRegistry.DataVault`.
- Lease invalidation released debris Vault handles by nonzero descriptor instead of expected buffer and owner proof.
- Black-box cursor/dump state could survive native lease invalidation.

What was done:
- Cold/missing-service refresh now routes DataVault through `ApplyRegistryServiceRebind`.
- Debris positions, velocities, requests, job-state, and black-box releases now require expected `BufferID`, `SystemID.Vfx`, and generation.
- Lease invalidation resets debris black-box epoch state.

Cinematic cheats used:
- None. Existing GPU-only debris fake, SDF/flow response, capacity scaling, and indirect draw behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; cold lifecycle/release predicate only.

Verification:
- Grep for direct `_registryDataVault = GlobalRegistry.DataVault` and old nonzero-descriptor release predicate returns no hits.
- `git diff --check -- CarveDebrisComputeRenderer.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `47872`, CPU `71%`.

## 2026-05-28 - GPU Scatter Native Lifecycle Pass

What was wrong:
- `GpuScatterLodManager` directly cached `_registryDataVault = GlobalRegistry.DataVault`.
- Owned black-box and CPU audit releases used nonzero descriptor checks.
- Black-box cursor/dump state could survive DataVault lease invalidation.

What was done:
- Cold/missing-service refresh now routes DataVault through `ApplyRegistryServiceRebind`.
- Owned `FloraScatterBlackBox`, `FloraScatterCpuFrustumPlanes`, and `FloraScatterCpuVisibilityMask` releases require expected `BufferID`, `SystemID.Vfx`, and generation.
- Borrowed producer-owned flora lanes are only invalidated, not released.
- Lease invalidation resets scatter black-box epoch state.

Cinematic cheats used:
- None. Flora indirect draw, culling, visual payload, and GPU upload behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; cold lifecycle/release predicate only.

Verification:
- Grep for direct `_registryDataVault = GlobalRegistry.DataVault` and old nonzero-descriptor release predicate returns no hits.
- `git diff --check -- GpuScatterLodManager.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `63280`, CPU `54%`.

## 2026-05-28 - Bilateral DRS Native Lifecycle Pass

What was wrong:
- `HectonBilateralDrsUpscalerRuntime` directly cached `_dataVault = GlobalRegistry.DataVault`.
- DataVault replacement directly assigned `_dataVault = currentService as IDataVault`.
- Owned read/release helpers accepted nonzero descriptors without proving the expected `BufferID` and `SystemID.GraphicsScalability`.
- Fault dump and seed state could remain stale across native epoch replacement.

What was done:
- Cold cache and replacement now route through `BindDataVaultForLifecycle`.
- Params, tuning, telemetry, cursor, profiles, CSV scratch, and mock-state reads require expected buffer id, owner, and generation.
- Release only fires for the expected owned lane; foreign descriptors are cleared without release.
- Native epoch reset now re-arms seed, publish, and fault-dump state.

Cinematic cheats used:
- None. Bilateral DRS filter math, GPU constant-buffer upload, CSV quality profiles, and continuous quality-weight policy are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; ownership checks are scalar guard predicates around existing Vault reads/locks.

Verification:
- Direct `_dataVault = GlobalRegistry.DataVault/currentService` and old `IsVaultHandleCreated` grep returns no hits.
- `git diff --check -- HectonBilateralDrsUpscalerRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `44208`, CPU `68%`.

## 2026-05-28 - Abyssal Deferred Caustics Native Lifecycle Pass

What was wrong:
- `AbyssalDeferredCausticsRuntime` directly cached and replaced `_dataVault`.
- Owned caustics releases used nonzero descriptor checks.
- Owned mutable writes used `TryResolveHandle` without DataVault write locks.
- Native seed/fault state could survive DataVault epoch replacement.

What was done:
- Cold cache and DataVault replacement now route through `BindDataVaultForLifecycle`.
- Params, tuning, telemetry, cursor, profiles, and CSV scratch validate expected `BufferID`, `SystemID.GraphicsScalability`, and generation.
- Seed, tuning, CSV profile, telemetry, cursor, kernel-output, and pending-parameter writes use `TryAcquireWriteLock` and release in `finally`.
- Borrowed ocean surface swell remains a borrowed read and is never released by caustics.

Cinematic cheats used:
- None. Existing deferred caustics fake, mock lighting kernel, quality-profile binding, and GPU constant-buffer payload are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: small unmeasured scalar lock overhead on caustics writes; stability fix, not optimization.

Verification:
- Direct `_dataVault = GlobalRegistry.DataVault/currentService`, old `IsVaultHandleCreated`, no-arg `RunPendingCausticsKernel(job)`, and no-arg `PublishPendingCausticsParameters()` grep returns no hits.
- `git diff --check -- AbyssalDeferredCausticsRuntime.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `57212`, CPU `46%`.

## 2026-05-28 - Visor AR Stencil Native Lifecycle Pass

What was wrong:
- `HectonVisorARStencilRendererFeature` directly assigned `_dataVault` on DataVault replacement.
- Required-handle readiness and release used nonzero descriptor checks.
- Telemetry dump reads did not prove the telemetry descriptor owner before reading.

What was done:
- Cold cache and DataVault replacement now route through `BindDataVaultForLifecycle`.
- HUD params, target source, projected target, digit params, telemetry, profile, and CSV scratch validate expected `BufferID`, `SystemID.UI`, and generation.
- Release only fires for the expected UI-owned lane.
- Telemetry dump state resets when Vault handles are released.

Cinematic cheats used:
- None. Stencil mask, RenderGraph passes, AR projection, HUD digits, and shader payloads are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; scalar descriptor predicates only.

Verification:
- Direct `_dataVault = GlobalRegistry.DataVault/currentService`, old `IsHandleCreated`, and ownerless release grep returns no hits.
- `git diff --check -- HectonVisorARStencilRendererFeature.cs` reports only existing LF/CRLF warning.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `47444`, CPU `80%`.

## 2026-05-28 - Combined Render/Visor Native Verification Guard

What was wrong:
- The DRS, Abyssal Caustics, and Visor AR Stencil edits needed one combined check after source and report updates.

What was done:
- Ran combined whitespace check over the three source files plus AUDIT status/rationale/log.
- Ran combined grep for direct DataVault assignment, old handle helpers, ownerless release, and stale no-arg caustics publish routes.
- Sampled compiler/CPU guard.

Cinematic cheats used:
- None. Verification-only step.

Exact microseconds saved:
- Measured: 0 us.

Verification:
- Combined `git diff --check` reports only LF/CRLF warnings.
- Combined bad-pattern grep returns no hits.
- Build/import/profiler/native-ledger proof blocked by external `dotnet` PID `62336`, CPU `62%`.

## 2026-05-28 - Diegetic Glitch UI DataVault Epoch And Write Ownership

What was wrong:
- `DiegeticGlitchSurgeonRuntime` directly rebound `_vault` from GlobalRegistry/currentService.
- Editor/debug `GetGlitchStateRef` and `GetTuningRef` exposed live mutable Vault refs.
- Tuning/default/mock seeding writes used mutable resolve paths outside a DataVault writer-lock helper.
- DataVault replacement/disable could release old UI Vault buffers while an external ASCII table lease still held the table lock.

What was done:
- Added `BindDataVaultForLifecycle` and native-epoch reset for shader push caches, fault/dump state, table hash, seed-stall tracking, and mock text length.
- Deferred DataVault rebind/disable while owned jobs, pending external release, or an outstanding external table lease are alive.
- Converted tuning, deterministic seed, default state/tuning/table, mock text, mock quads, and synth default writes to `TryAcquireWriteLock`/`ReleaseWriteLock`.
- Kept `Get*Ref` signatures as snapshot-backed compatibility shims and moved `DiegeticGlitchTunerWindow` to explicit snapshot reads.

Cinematic cheats used:
- Kept the existing deterministic glyph-table, terminal UV, radar ghost, and synth pitch fakes. No heavier simulation added.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; changes are lifecycle/cold/editor write discipline.

Verification:
- Direct `_vault = GlobalRegistry.DataVault/currentService` grep returns no hits.
- Editor `GetGlitchStateRef`/`GetTuningRef` call sites are removed.
- `git diff --check` over the UI runtime/editor and AUDIT docs reports only LF/CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 errors/0 warnings in 13.32s.
- Full solution proof remains pending: `dotnet build Hecton8.slnx ...` timed out after 604s and lost the original exit code; no Unity import, Play Mode, profiler, or GC capture was run.

## 2026-05-28 - Visor Fluid Black-Box Vault Owner Proof

What was wrong:
- `HectonVisorFluidDistortionFeature` accepted black-box descriptors with only nonzero `BufferID` and generation.
- DataVault hot-swap used `currentService as IDataVault`.
- Owned release checked matching generation but not `SystemID.Vfx`, so a stale or foreign descriptor could be released through the wrong ownership route.

What was done:
- Added a black-box predicate requiring `BufferID.VisorRefractionBlackBox`, `SystemID.Vfx`, and generation.
- Routed DataVault replacement through `BindBlackBoxVaultForLifecycle`.
- Reused, read, write-locked, allocated, and released the black-box ring only after owner proof.
- Reset black-box dump/native-epoch state on DataVault rebind.

Cinematic cheats used:
- None added. Existing fullscreen droplet/refraction fake, lens compute mask, thermal cull, quality pressure, and visual-overkill salt glint behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; descriptor owner checks are scalar guards around existing Vault operations.

Verification:
- Scoped grep for old `IsVaultHandleCreated`, direct `_dataVault = GlobalRegistry.DataVault/currentService`, raw `SystemID.Vfx` black-box write locks, and `currentService as IDataVault` returns no hits.
- `git diff --check -- HectonVisorFluidDistortionFeature.cs` reports only LF/CRLF warning.
- Initial compile guard blocked: external `dotnet` PID `60688` was running `dotnet build Hecton8.World.Dots.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false`; CPU sampled `50%`.
- Targeted compile passed: `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 errors/0 warnings in 51.12s.
- Unity import, Play Mode, visor refraction scene, native ledger, profiler, and GC capture remain pending.

## 2026-05-28 - Diegetic Visor HUD Black-Box Vault Rebind

What was wrong:
- `DiegeticVisorHudMesh` directly assigned `_dataVault` on DataVault replacement.
- The old `_blackBoxHandle` could then be released through the new Vault instead of the old Vault.
- Black-box validity checked only nonzero `BufferID` and generation, not `SystemID.UI`.

What was done:
- Added `RebindDataVaultForLifecycle` with previous-Vault fallback.
- Cold cache now refreshes stale disabled-state Vault references from `GlobalRegistry.DataVault`.
- Black-box read/write/dump/release paths now require `BufferID.DiegeticVisorHudBlackBox`, `SystemID.UI`, and generation.
- Freshly ensured handles are validated before telemetry starts writing.

Cinematic cheats used:
- None added. Curved HUD mesh, material state, humidity sampling, stencil behavior, and continuous quality segment scaling are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; scalar descriptor predicates only.

Verification:
- Scoped grep for old `IsVaultHandleCreated`, direct `_dataVault = GlobalRegistry.DataVault/currentService`, and `currentService as IDataVault` returns no hits.
- `git diff --check -- DiegeticVisorHudMesh.cs` reports only LF/CRLF warning.
- Build/import/profiler proof blocked: external `dotnet` PID `50524` was running `dotnet build Crest.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false`; CPU sampled `65%`.

## 2026-05-28 - Diegetic Tooltip Black-Box Vault Rebind And Lock Release

What was wrong:
- `DiegeticTooltipSystem` directly assigned `_dataVault` on DataVault replacement.
- The old tooltip `_blackBoxHandle` could be released through the replacement Vault.
- Black-box validity checked only nonzero `BufferID` and generation, not `SystemID.UI`.
- `RecordBlackBox` could acquire a write lock and return on invalid ring state before releasing it.

What was done:
- Added `RebindDataVaultForLifecycle` with previous-Vault fallback.
- Cold cache now refreshes stale disabled-state Vault references from `GlobalRegistry.DataVault`.
- Black-box read/write/dump/release paths now require `BufferID.DiegeticTooltipBlackBox`, `SystemID.UI`, and generation.
- Telemetry writes now release `ReleaseWriteLock` in `finally` for every successful lock acquisition.

Cinematic cheats used:
- None added. Glyph layout, icon buffers, material binding, input-determinism policy, and quality flags are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; scalar descriptor checks only, plus lock-leak prevention on invalid ring state.

Verification:
- Scoped grep for old `IsVaultHandleCreated`, direct `_dataVault = GlobalRegistry.DataVault/currentService`, and `currentService as IDataVault` returns no hits.
- `git diff --check -- DiegeticTooltipSystem.cs` reports only LF/CRLF warning.
- Build/import/profiler proof blocked: external `dotnet` PID `62816` was running `dotnet build Hecton8.Editor.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false`, child `csc` PID `61868` active, CPU sampled `94%`.

## 2026-05-28 - Combined Current Visor/UI Native Verification Guard

What was wrong:
- The current package touched `HectonVisorFluidDistortionFeature`, `DiegeticVisorHudMesh`, and `DiegeticTooltipSystem`.
- The last two UI files still needed compile/runtime proof after source and report updates.

What was done:
- Ran combined `git diff --check` over the three source files plus AUDIT docs.
- Re-sampled compiler/CPU guard several times.
- Stopped new source edits once the external build lane continued, to avoid growing uncompiled surface area.

Cinematic cheats used:
- None. Verification-only step.

Exact microseconds saved:
- Measured: 0 us.

Verification:
- Combined `git diff --check` reports only LF/CRLF warnings.
- Per-file bad-pattern greps for old handle helpers, direct DataVault assignment, and `currentService as IDataVault` returned no hits in touched source.
- New targeted compile/import/profiler proof blocked: external `dotnet` PID `28412` was running `dotnet build MoreMountains.Feedbacks.Cinemachine.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false`; CPU sampled `7%`.
- Later targeted compile passed: `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 errors/0 warnings in 71.15s.
- Unity import, Play Mode, visor/HUD/tooltip scene, native ledger, profiler, and GC capture remain pending.

## 2026-05-28 - OpenXR Manual Override Lever Black-Box Vault Rebind

What was wrong:
- `OpenXRManualOverrideLever` directly assigned `_dataVault` on DataVault replacement.
- The old `_blackBoxHandle` could be released through the replacement Vault.
- Black-box validity checked only `BufferID` and generation, not `SystemID.UI`.

What was done:
- Added `RebindDataVaultForLifecycle` with previous-Vault fallback.
- Cold cache now refreshes stale Vault references through the same lifecycle path.
- Black-box read/write/release paths now require `BufferID.OpenXrManualOverrideLeverBlackBox`, `SystemID.UI`, and generation.
- Native-epoch state for the black-box cursor/dump flag resets after failed ensure, rebind, dispose, or release.

Cinematic cheats used:
- None added. Grab projection, IK, haptics, non-VR fallback, and prologue signal output are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; scalar descriptor predicates only.

Verification:
- Scoped grep for direct `_dataVault = GlobalRegistry.DataVault/currentService`, `currentService as IDataVault`, and old nonzero release predicates returns no bad hits.
- `git diff --check -- OpenXRManualOverrideLever.cs` reports only LF/CRLF warning.

## 2026-05-28 - VR Brownout XRPass Compile Repair

What was wrong:
- Targeted compile after the OpenXR patch failed in `HectonVRBrownoutFeature` with `CS0246 XRPass` at lines `441` and `480`.
- The file lacked `UnityEngine.Experimental.Rendering`, while other URP XR render features in the repo use that namespace for `XRPass`.

What was done:
- Added `using UnityEngine.Experimental.Rendering;`.
- Did not rewrite brownout shader logic, RenderGraph routing, VR comfort gating, or the existing unsafe constant-buffer write path in this pass.

Cinematic cheats used:
- None. Brownout visual behavior unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Initial failed build wall time: `50.72s`.

Verification:
- `git diff --check -- HectonVRBrownoutFeature.cs OpenXRManualOverrideLever.cs` reports only LF/CRLF warnings.
- Initial repeat proof was blocked: external `dotnet` PID `25684` was running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`; CPU later spiked above guard.
- Targeted compile passed after the compiler lane cleared and CPU sampled `39%`: `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` succeeded with 0 errors/0 warnings in 93.56s.
- No Unity import, Play Mode, cockpit/visor scene, native ledger, profiler, or GC capture has been run after the targeted compile.

## 2026-05-28 - Font Streaming Prefetch Vault Lifecycle

What was wrong:
- `FontStreamingManager` directly cached `GlobalRegistry.DataVault`.
- DataVault replacement assigned `_dataVault` without a single lifecycle teardown route.
- Visible-prefetch hash/slice handles were released by nonzero descriptor state and proved only `BufferID` plus generation, not `SystemID.UI`.

What was done:
- Added `BindDataVaultForLifecycle` for cold cache and service replacement.
- Existing prefetch job/lock teardown now runs before Vault rebind.
- Hash and slice prefetch releases now require expected `BufferID`, `SystemID.UI`, and generation.
- Failed ensure paths release only the exact owned lane they are replacing.

Cinematic cheats used:
- None. TMP registry scan, localization readiness, scheduler cadence, and status UI behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; cold lifecycle and scalar descriptor predicate change only.

Verification:
- Scoped grep for direct `_dataVault = GlobalRegistry.DataVault/currentService`, `currentService as IDataVault`, old no-expected-id `ReleasePrefetchHandle` calls, and old `BufferID != 0u && Generation` release predicates returns no bad hits.
- `git diff --check -- FontStreamingManager.cs` reports only LF/CRLF warning.
- Compile proof is still not accepted: guarded `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` returned exit code 1 after 64.7s with empty stdout/stderr and did not update `Temp/CodexBuild/Hecton8.Core/Hecton8.Core.dll`.
- Repeat compile is blocked by CPU guard samples above 50% with no active compiler process.

## 2026-05-28 - FontStreaming/Brownout Compile Recheck

What was wrong:
- The next guarded compile did not validate `FontStreamingManager`; it failed earlier in `HectonVRBrownoutFeature` with `CS0246 XRPass`.
- Current worktree lacked `using UnityEngine.Experimental.Rendering;` despite the earlier report saying the namespace repair was present.

What was done:
- Verified local PackageCache and peer URP render features: `XRPass` is `UnityEngine.Experimental.Rendering.XRPass`.
- Re-added `using UnityEngine.Experimental.Rendering;` to `HectonVRBrownoutFeature`.
- Re-ran targeted compile after guard cleared.

Cinematic cheats used:
- None. No rendering behavior was changed.

Exact microseconds saved:
- Measured: 0 us.
- Failed brownout build wall time: 37.9s.
- Failed world-dependency build wall time: 71.6s.

Verification:
- Scoped `rg` confirms the brownout import and both `XRPass` method signatures.
- `git diff --check` over brownout, FontStreaming, and AUDIT docs reports only LF/CRLF warnings.
- Build progressed past brownout and failed in `HectonMapMagicVegetationBridge.cs:2876` with `CS0103 TryApplyPendingWorldOffset`.
- Scoped inspection shows `TryApplyPendingWorldOffset` exists at line 5032 in the same class/depth; because this is world-domain code and currently contradictory, proof is blocked until a guarded repeat or world-domain owner fix.
- Repeat build currently blocked by CPU guard above 50%.

## 2026-05-28 - Moving Compile Lane Dependency Check

What was wrong:
- A later guarded compile no longer failed on `TryApplyPendingWorldOffset`.
- It failed on `SubmarineAutoLevelBallastController` `NativeArray<T>` vs `.ReadOnly` conversions and `VegetationNavGridSynchronizer.ResolveActiveViewCamera`.
- Immediate source inspection showed those errors did not match current disk state.

What was done:
- Confirmed current submarine room-buffer variables, job fields, and helper signature use `NativeArray<T>.ReadOnly`.
- Confirmed current vegetation HLOD path calls `RefreshActiveViewCameraCache`, not `ResolveActiveViewCamera`.
- Did not edit those files from stale compiler text.

Cinematic cheats used:
- None. Verification-only step.

Exact microseconds saved:
- Measured: 0 us.
- Failed build wall time: 114.5s.

Verification:
- `git diff --check` over submarine, vegetation synchronizer, brownout, and FontStreaming reports only LF/CRLF warnings.
- Scoped greps find no current `ResolveActiveViewCamera(` call and no old room-buffer `out NativeArray<float>` declarations.
- External `Assembly-CSharp.csproj` build then occupied compiler lane; after it cleared CPU sampled 82%, so no immediate repeat build was launched.

## 2026-05-28 - CharBufferPool Babel Arena Owner Predicate

What was wrong:
- `CharBufferPool` created the Babel native arena under `SystemID.UI`.
- Its validity/release predicate accepted any nonzero `BufferID` and generation.
- A stale or foreign UI handle could be resolved or released as the Babel arena.

What was done:
- Replaced `IsVaultHandleCreated` with `IsBabelArenaHandle`.
- Babel arena resolve/acquire/release now requires exact `BufferID 70540`, `SystemID.UI`, and generation.
- Slot bitmap leases, TMP bridge fallback, encyclopedia pages, and localization behavior were not changed.

Cinematic cheats used:
- None. Text formatting behavior unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms old `IsVaultHandleCreated` is gone and `ReleaseBuffer(in handle)` is behind `IsBabelArenaHandle`.
- `git diff --check -- CharBufferPool.cs` reports only LF/CRLF warning.
- Build/import/profiler proof blocked by external `dotnet` PID `63204`, child `csc` PID `9840`, CPU `100%`.

## 2026-05-28 - PDA Frequency Stage And Telemetry Owner Predicates

What was wrong:
- `PDADecryptionSpectrogramPanel` used a generic nonzero handle predicate for two UI-owned DataVault lanes.
- `PdaFrequencyStageTargets` and `PdaFrequencyTelemetryRing` could be treated as valid without proving exact `BufferID` and `SystemID.UI`.

What was done:
- Added a single `VaultOwnerSystemId = SystemID.UI` constant.
- Replaced the generic predicate with `IsExactVaultHandle`.
- Stage-target and telemetry read/write-lock/release/dump paths now require exact expected `BufferID`, `SystemID.UI`, and generation.
- Frequency tuning math, shader parameters, input feedback, quality scaling, and dump layout were not changed.

Cinematic cheats used:
- None. The existing wave fake and minigame behavior were preserved.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms old `IsVaultHandleCreated` and nonzero release predicates are gone from `PDADecryptionSpectrogramPanel`.
- `git diff --check -- PDADecryptionSpectrogramPanel.cs` reports only LF/CRLF warning.
- Build/import/profiler proof blocked by external `dotnet` PIDs `45864`/`66816`, child `csc` PIDs `33620`/`23596`, CPU `93%`.

## 2026-05-28 - PDA/Suit HUD Glitch-Table Borrowed Handle Predicates

What was wrong:
- `PDAShellChrome` and `SuitHUDV4CanvasOverlay` borrow the shared glitch glyph table from DataVault.
- Their local predicates accepted any nonzero `BufferID` and generation.
- A wrong UI byte buffer could be read as the glyph table through an unsafe read-only pointer.

What was done:
- Replaced both ownerless helpers with `IsGlitchTableHandle`.
- Binding and pointer resolve now require raw `BufferID 70901`, `SystemID.UI`, and generation.
- DataVault bind callbacks now use explicit pattern matching instead of `currentService as IDataVault`.
- The borrowed table is still owned by `DiegeticGlitchSurgeonRuntime`; these consumers do not release it.

Cinematic cheats used:
- None changed. Existing glyph-table glitch fake remains intact.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms old `IsVaultHandleCreated` helpers are gone from both files and cached handle resolution revalidates `IsGlitchTableHandle`.
- No `currentService as IDataVault` remains in `PDAShellChrome` or `SuitHUDV4CanvasOverlay`.
- `git diff --check -- PDAShellChrome.cs SuitHUDV4CanvasOverlay.cs` reports only LF/CRLF warnings.
- Build/import/profiler proof blocked by external `dotnet` PID `53008`, child `csc` PID `63300`, CPU `85%`.

## 2026-05-28 - PDA Frequency DataVault Cache Rebind Route

What was wrong:
- `PDADecryptionSpectrogramPanel` still replaced `_cachedDataVault` directly from hot-swap and cold refresh paths.
- Exact handle predicates were present, but the release route could still lose the previous Vault instance.

What was done:
- Added `BindDataVaultForLifecycle`.
- DataVault hot-swap and `GlobalRegistry.DataVault` cold refresh now use the same route.
- Owned stage/telemetry handles are released through the previous Vault before `_cachedDataVault` is replaced.

Cinematic cheats used:
- None changed. Frequency tuning wave fake remains intact.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms `_cachedDataVault` assignment is isolated to `BindDataVaultForLifecycle`.
- No `currentService as IDataVault` remains in `PDADecryptionSpectrogramPanel`.
- `git diff --check -- PDADecryptionSpectrogramPanel.cs` reports only LF/CRLF warning.
- Build/import/profiler proof remains blocked by external build lane and CPU guard.

## 2026-05-28 - Topographical Sonar DataVault Lifecycle And Owner Predicates

What was wrong:
- `TopographicalSonarSynthesizer` had 11 DataVault handles but resolved/released by generic nonzero `BufferID`.
- DataVault hot-swap directly replaced `_dataVault`.
- A stale or wrong UI descriptor could be resolved or released as sonar state.

What was done:
- Added `BindDataVaultForLifecycle`.
- Added `IsTopographicalHandle`.
- Every sonar resolve/release path now includes the expected `TopographicalSonarBufferIds.*`, `SystemID.UI`, and generation.
- Raymarch math, mock SDF, LUT parsing, GPU upload, telemetry layout, and quality LOD were not changed.

Cinematic cheats used:
- None changed. Existing mock SDF and point-cloud sonar fake remain intact.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms no `currentService as IDataVault`, no direct `_dataVault = GlobalRegistry.DataVault`, and no generic `handle.BufferID != 0u` release/resolve predicate remain.
- All `TryResolveVaultBuffer` calls pass expected sonar `BufferID`.
- `git diff --check -- TopographicalSonarSynthesizer.cs` reports only LF/CRLF warning.
- Build/import/profiler proof remains blocked by external build lane and CPU guard.

## 2026-05-28 - Vehicle Sub OS Cockpit DataVault Hot-Swap And Release Predicates

What was wrong:
- `VehicleSubOsCockpitRuntime` did not handle DataVault service replacement.
- Cold cache assigned `_dataVault` directly.
- Cockpit button and telemetry teardown released by generic nonzero descriptor state.

What was done:
- Added DataVault hot-swap handling with button job teardown before rebind.
- Added `BindDataVaultForLifecycle`.
- Button and telemetry handles now release only with exact expected `VehicleSubOs*` `BufferID`, `SystemID.UI`, and generation.
- Cockpit radar, damage hologram, render targets, and button animation behavior were not changed by this patch.

Cinematic cheats used:
- None changed. Existing radar/hologram fakes remain intact.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms no direct `_dataVault = GlobalRegistry.DataVault`, no `currentService as IDataVault`, no old `ReleaseCockpitVaultHandle(ref ...)`, and no generic `handle.BufferID != 0u` release predicate.
- `git diff --check -- VehicleSubOsCockpitRuntime.cs` reports only LF/CRLF warning.
- Build/import/profiler proof remains blocked by external build lane and CPU guard.

## 2026-05-28 - Combined Current UI Native Lifecycle Package

What was wrong:
- Six touched files needed one combined static check after later micro-patches.
- Compiler proof stayed blocked by build-lane/CPU guard.

What was done:
- Ran combined bad-pattern grep over current UI native lifecycle package.
- Ran combined `git diff --check` over source files and AUDIT docs.
- Re-sampled compiler lane and CPU guard before build.

Cinematic cheats used:
- None. Verification-only step.

Exact microseconds saved:
- Measured: 0 us.

Verification:
- Combined grep returns no hits for direct DataVault cast/cache in touched files, ownerless `handle.BufferID != 0u`, old `IsVaultHandleCreated`, or old cockpit release helper.
- Combined `git diff --check` reports only LF/CRLF warnings.
- Targeted compile not launched: latest guard has no `dotnet/csc`, but CPU sample remains `100%`.

## 2026-05-28 - PDA Encyclopedia Streamer And H8LR Mirror Vault Lifecycle

What was wrong:
- `PDAEncyclopediaStreamer` reset ten UI Vault handles on DataVault replacement without releasing them through the previous Vault.
- Generic handle proof was unsafe for byte lanes: mock UTF-8, CSV scratch, and H8LR mirror all have `VaultGenerationHandle<byte>`.
- `PdaH8lrLoreStore` still accepted any nonzero byte mirror handle before write-lock/read-only use.

What was done:
- Added streamer DataVault lifecycle binding that releases previous Vault handles before rebinding.
- Added exact expected `BufferID` plus `SystemID.UI` plus generation checks to every streamer resolve/ref/release path.
- Added OnDestroy release for the streamer-owned PDA Vault buffers.
- Added exact H8LR mirror-handle validation inside `PdaH8lrLoreStore`.
- Lore payload streaming, B-tree lookup, Babel fallback, TMP typewriter cadence, and telemetry layout were not changed.

Cinematic cheats used:
- None changed. Existing typewriter reveal and mock lore fallback remain presentation fakes over byte-addressed payloads.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped bad-pattern grep over `PDAEncyclopediaStreamer.cs` and `PdaH8lrLoreStore.cs` returns no hits for direct DataVault cache/cast, ownerless nonzero handle checks, or old resolve/get-element signatures.
- `git diff --check -- PDAEncyclopediaStreamer.cs PdaH8lrLoreStore.cs` reports only LF/CRLF warnings.
- Targeted compile/import/profiler proof not launched: external `dotnet` PID `31336`, child `csc` PID `60436`, CPU `100%`.

## 2026-05-28 - Babel Subtitle Teardown Owner Predicate

What was wrong:
- `BabelSubtitleSyncRuntime` validated exact subtitle Vault handles for write-lock acquisition, but teardown still released by generic nonzero `BufferID`.
- Three static UI lanes could therefore be confused during subsystem reset: cue state, localization telemetry, and UI optimization telemetry.

What was done:
- `ReleaseSubtitleBuffers` now passes exact expected BufferIDs into `ReleaseVaultBuffer`.
- `ReleaseVaultBuffer` now reuses `IsSubtitleVaultHandle` before calling `ReleaseBuffer`.
- SignalBus settings, subtitle audio-frame timing, DTO layout, and telemetry cadence were not changed.

Cinematic cheats used:
- None changed. Subtitle presentation timing remains the existing audio-frame driven fake.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms the old `vault != null && handle.BufferID != 0u` release predicate is gone and all three release calls pass expected BufferIDs.
- `git diff --check -- BabelSubtitleSyncRuntime.cs` reports clean.
- Targeted compile/import/profiler proof not launched because the compiler/CPU guard was active.

## 2026-05-28 - Wrist HUD And PDA Projector Vault Teardown

What was wrong:
- Wrist HUD and PDA projector handles were defaulted during teardown instead of being released through the owning Vault.
- Shared exact-handle validation checked BufferID and generation but not `SystemID.UI`.
- The DataVault service callback still used `currentService as IDataVault`.

What was done:
- Wrist HUD state, quads, font atlas, telemetry, counters, and acoustic tap handles now release through `ReleaseWristHudVaultHandle`.
- PDA projector state, input, telemetry, cursor, tuning, profile, and editor CSV scratch handles now release through the same exact owner helper.
- `IsExactVaultHandle` now requires expected BufferID, `SystemID.UI`, and generation.
- The projector partial imports `Hecton8.Core.Data` because it now references `IDataVault` directly.
- SDF HUD rendering, PDA projection math, GPU buffer upload, CSV parsing, and quality scaling were not changed.

Cinematic cheats used:
- None changed. Existing SDF wrist HUD and PDA projection visual fakes remain intact.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep confirms no `currentService as IDataVault`, no direct `_cachedDataVault = GlobalRegistry.DataVault`, release calls carry expected BufferIDs, and exact handle proof includes `SystemID.UI`.
- `git diff --check -- WristHologramHudRuntime.cs WristHologramHudRuntime_PdaScreenProjector.cs` reports only LF/CRLF warning.
- Targeted compile/import/profiler proof not launched: external `dotnet` PID `51336`, CPU `77%`.

## 2026-05-28 - Terminal OS DataVault Hot-Swap And Release Ownership

What was wrong:
- `TerminalOsRuntime` cold cache directly assigned `_vault = GlobalRegistry.DataVault`.
- DataVault replacement used a direct cast and released native state without proving the previous Vault route.
- Release accepted any nonzero UI handle instead of exact terminal/projection/decryption BufferIDs.
- Hot-swap could release native lanes while terminal jobs were still scheduled.

What was done:
- Cold cache now calls `BindDataVaultForLifecycle`.
- DataVault hot-swap completes terminal jobs, releases old native lanes through the old/previous Vault, and then binds the replacement Vault.
- Fresh `EnsureGenerationHandle` results are rejected unless expected `BufferID`, `SystemID.UI`, and generation match.
- All 24 terminal, projection, and decryption release paths pass exact BufferIDs.
- Terminal rendering, glyph SDF upload, decryption math, projection raycast, DTO layout, and quality scaling were not changed.

Cinematic cheats used:
- None changed. The existing texture-array terminal fake and projection input fake remain.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep over `TerminalOsRuntime.cs` and `TerminalOsRuntime_TerminalProjection.cs` returns no hits for direct DataVault cast/cache, old `IsValidVaultHandle`, ownerless `handle.BufferID != 0u`, or no-expected-id release calls.
- `git diff --check -- TerminalOsRuntime.cs TerminalOsRuntime_TerminalProjection.cs` reports only LF/CRLF warnings.
- Targeted compile/import/profiler proof not launched: external `dotnet` PID `9760`, CPU `100%`.

## 2026-05-28 - UI/Visor Residual DataVault Lifecycle Sweep

What was wrong:
- Four already-fixed systems still used `currentService as IDataVault` in hot-swap callbacks.
- `HectonVisorUberPostFeature` split DataVault binding across noir/reconstruction partials and direct cold assignments.
- `SpectrumSystem` still had direct DataVault binding and nonzero handle predicates for AUP discovery and active-sonar telemetry.

What was done:
- Replaced residual direct DataVault casts in `DiegeticVisorLensRuntime`, `HectonVisorARStencilRendererFeature`, `InternalFloodWaterlineRuntime`, and `DiegeticGyroCompassRuntime`.
- Added `BindUberDataVaultForLifecycle` for Uber noir/reconstruction handles.
- Added `BindDataVaultForLifecycle` and exact `IsSpectrumVaultHandle` checks for Spectrum `71030` and `71031`.
- Preserved other agents' existing `SpectrumSystem` acoustic queue changes.

Cinematic cheats used:
- None changed. Existing sonar grid fake, noir/reconstruction visuals, gyro/waterline/stencil paths remain.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Global grep over `Assets/_Project/Scripts/UI` and `Assets/_Project/Scripts/Visor` returns no hits for `currentService as IDataVault`, direct DataVault field assignment from `GlobalRegistry.DataVault`, ownerless `handle.BufferID != 0u`, `IsVaultHandleCreated`, or `IsValidVaultHandle`.
- Combined `git diff --check` for touched UI/Visor files reports only LF/CRLF warnings.
- Targeted compile/import/profiler proof not launched: external `dotnet` PID `5428`, child `csc` PID `16076`, CPU `100%`.
- Recheck after waiting: compiler lane cleared, but CPU sampled `70%`; targeted compile still not launched.
- APEX JSON artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_20260528.json`.
- APEX JSON SHA-256: `91799EB7B3623EF83201D2C8FEA96605A6981C270A686B425C46EF94A91BA2F4`.
- JSON validation: `ConvertFrom-Json` succeeded.
- Final compile guard after JSON hashing: no `dotnet/csc/VBCSCompiler` process output, CPU `100%`; no build launched.

## 2026-05-28 - Vocal Warning DataVault Owner Predicate Tail

What was wrong:
- Global grep outside UI/Visor still found project-wide lifecycle tails; UI/Visor closure was not global project closure.
- `VocalWarningSystem` used `currentService as IDataVault` in both DataVault callbacks.
- `ReleaseVaultBuffer` released any nonzero descriptor across 12 owned audio warning lanes.

What was done:
- DataVault callbacks now pattern-match `IDataVault`.
- Queue, priority state, flags, cooldowns, severity, source IDs, current state, dispatch, profiles, tuning, editor CSV scratch, and telemetry handles now pass exact expected BufferIDs to teardown.
- `IsVocalWarningVaultHandle` requires expected `BufferID`, `SystemID.AudioVocalWarning`, and generation before `ReleaseBuffer`.

Cinematic cheats used:
- None changed. Warning priority math, subtitle dispatch, telemetry layout, and audio behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep reports no `currentService as IDataVault`, no ownerless `handle.BufferID != 0u`, and no `OwnerSystemID` typo in `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs`.
- `git diff --check -- Assets/_Project/Scripts/Audio/VocalWarningSystem.cs` reports only LF/CRLF warning.
- Compile/import/profiler proof not launched: CPU `48%`, but external `dotnet.exe build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` PID `6088` is active.

## 2026-05-28 - Vocal Bank Playback Exact Vault Gates

What was wrong:
- `VocalBankPlaybackRuntime` used `currentService as IDataVault` during DataVault replacement.
- Audio callback/control/init/bank/CSV write-lock paths did not prove exact vocal synthesis BufferID before `TryAcquireWriteLock`.
- Teardown released any nonzero vocal synthesis descriptor.

What was done:
- DataVault replacement now pattern-matches `IDataVault`.
- Write-lock acquisition now requires exact BufferID, `SystemID.AudioVocalSynthesis`, and generation for state, codec, telemetry, counters, waveform, mock bank bytes, mock records, CSV metadata, and CSV scratch.
- Read-only revalidation and teardown use the same exact descriptor gate.

Cinematic cheats used:
- None changed. DSP decode, mock bank generation, vocal cue SignalBus flow, and audio filter behavior are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Combined grep over `VocalWarningSystem.cs` and `VocalBankPlaybackRuntime.cs` reports no `currentService as IDataVault`, no ownerless `handle.BufferID != 0u`, no old handle helper, and no `OwnerSystemID` typo.
- `git diff --check -- Assets/_Project/Scripts/Audio/VocalWarningSystem.cs Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs` reports only LF/CRLF warnings.
- Diff size for the two audio files: `VocalBankPlaybackRuntime.cs +64/-46`, `VocalWarningSystem.cs +36/-16`.
- Compile/import/profiler proof not launched: external `dotnet.exe build Hecton8.Core.csproj --no-restore -nologo -v:minimal /m:1 /p:UseSharedCompilation=false /nr:false` PID `59612` active and CPU `85%`.

## 2026-05-28 - Native Audio Frame Ring Telemetry Release Predicate

What was wrong:
- `NativeAudioFrameRingBuffer` released its DataVault telemetry handle with a generic nonzero descriptor predicate.

What was done:
- Release now requires exact `BufferID.AudioFrameRingTelemetry`, `SystemID.AudioFrameRing`, and generation before `ReleaseBuffer`.
- Raw SPSC audio bridge memory, native plugin descriptor validation, overflow telemetry, and DSP write logic were not changed.

Cinematic cheats used:
- None changed. Existing bridge/ring buffer behavior is unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Scoped grep reports no ownerless `handle.BufferID != 0u` in `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`.
- `git diff --check -- Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs` reports only LF/CRLF warning.
- Compile/import/profiler proof not launched: external build lane and CPU guard remain active.

## 2026-05-28 - Audio Continuation APEX Artifact

What was wrong:
- The previous APEX JSON artifact predates the audio continuation patches and cannot prove them.

What was done:
- Created `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AUDIO_CONTINUATION_20260528.json`.
- Parsed it with `ConvertFrom-Json`: OK.
- Hashed it with SHA-256.

Cinematic cheats used:
- None; verification-only.

Exact microseconds saved:
- Measured: 0 us.

Verification:
- Audio continuation JSON SHA-256: `34D58A9FBBB9EDA1EB5AF473763F6618802D1C4EAF8BEECCBAA5CE97F1D81908`.
- Artifact records source diff hunk count `31`, diff Zero-GC scan `0`, layout diff scan `0`, scoped audio bad-pattern scan `0`, remaining audio static debt, and explicit compile/import/profiler proof absence.

## 2026-05-28 - Procedural/Adaptive/Dynamic Audio Exact Gates

What was wrong:
- `ProceduralAudioEvents` used generic `IsVaultHandleCreated` for two static event rings.
- `AdaptiveStemAudioMixer` and `DynamicMusicGranularSynthesizer` still had unsafe DataVault replacement casts and ownerless release/read/write gates.
- `PlayerCriticalProceduralAudioRenderer` still has four matching bad-pattern hits, but that file already contains active unrelated ping/haptic edits from another agent.

What was done:
- `ProceduralAudioEvents` now requires exact pending/next-frame event BufferIDs, owner, and generation before ensure/read/write/release.
- `AdaptiveStemAudioMixer` gates all owned stem handles by exact BufferID, `SystemID.AudioStemMixer`, and generation before resolve/write-lock and release.
- `DynamicMusicGranularSynthesizer` gates all owned synth handles by exact BufferID, `SystemID.AudioDynamicMusic`, and generation before resolve/write-lock and release.
- Player-critical renderer was not edited in this pass to avoid overwriting the active unrelated diff.

Cinematic cheats used:
- None changed. DSP decode, stem mix, dynamic synth scheduling, grain-bank fake, and signal semantics are unchanged.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us.

Verification:
- Audio scoped bad-pattern count is reduced to `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs:4` only.
- Scoped patched-file grep returns no `currentService as IDataVault`, ownerless `handle.BufferID != 0u`, old `IsVaultHandleCreated`, or `OwnerSystemID`.
- `git diff --check` for patched audio files reports only LF/CRLF warnings.
- Compile/import/profiler proof not launched: external `dotnet` PID `50672`, `VBCSCompiler` PID `28580`, CPU `42%`; compiler lane still occupied.

## 2026-05-28 - PlayerCritical Audio Exact-Gate Closure

What was wrong:
- `PlayerCriticalProceduralAudioRenderer` was the last audio-folder hit for the scoped native/DataVault anti-pattern grep.
- DataVault callbacks used direct `currentService as IDataVault`.
- Release/read/write helpers accepted ownerless or generic descriptors instead of proving the expected PlayerCritical buffer lane.
- The file already contained unrelated procedural ping/haptic queue edits from another agent; those are not claimed here.

What was done:
- Added `IsPlayerCriticalVaultHandle(handle, expectedBufferId)` requiring exact `BufferID`, `SystemID.AudioPlayerCritical`, and non-zero generation.
- Validated `EnsureGenerationHandle` output before `TryResolveHandle`.
- Added exact expected BufferIDs before shared write-lock acquisition, direct prologue queue writes, telemetry writes, read-only sonar/telemetry reads, and teardown release.
- Converted every PlayerCritical release call to pass the expected BufferID for all 49 owned lanes.
- Audio scoped bad-pattern grep now returns `0` for `currentService as IDataVault`, ownerless `handle.BufferID != 0u`, `handle.BufferID == 0u`, old handle helpers, and `OwnerSystemID`.

Cinematic cheats used:
- None added. DSP/reverb/sonar/granular behavior was not changed.
- Continuous audio quality behavior was preserved; no `isLowEnd` binary switch was added.

Exact microseconds saved:
- Measured: 0 us.
- Expected steady-frame delta: 0 us; this is descriptor-sovereignty hardening around existing native operations.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AUDIO_SWEEP2_20260528.json`
- SHA-256: `948D5DEC982B61E579023F1C059143414D6F4903718F464DFA4F0353FEB9A3C8`
- Zero-GC diff scan over seven audio files: `0`.
- Unsafe layout diff scan over seven audio files: `0` for `StructLayout`, `FieldOffset`, `UnsafeUtility.SizeOf`, and `UnsafeUtility.GetFieldOffset`.
- Audio bad-pattern scan: `0`.
- Build/import/profiler proof: absent. External `dotnet` PID `23460` and `csc` PID `29640` were active, CPU sampled `99%`; final guard later found external `dotnet` PID `59388` running `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`, CPU `70%`. No project build was launched by this pass.
## 2026-05-28T16:40:28+04:00 - Construction Validation Vault Exact Gates

What was wrong:
- `Assets/_Project/Scripts/Construction/ModularBaseConstructionValidator.cs` read, write-locked, and released DataVault validation handles by generic nonzero descriptor checks even though the helper received exact `BufferID`.
- The failed write-lock branch released outside `finally`.

What was done:
- Added exact `IsValidationVaultHandle` proof: expected `BufferID` + `SystemID.Construction` + nonzero generation.
- Routed tuner, telemetry, bounds, and occupancy read/release/write-lock helpers through the exact proof.
- Kept construction validation math, CSV parsing, telemetry DTO layout, and quality scaling unchanged.

Cinematic Cheats used:
- None added. Existing cheap mock terrain/bounds validation remains; no physical simulation was introduced.

Exact Microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us. This is correctness hardening around existing Vault calls, not a frame-time optimization.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_20260528.json`
- SHA-256: `F5BEFFF0AFE83B238E66BC5B05AA6C3987E12E6867E4B689577544624B3DB685`
- `rg` bad-pattern scan over the patched file returned 0 hits for ownerless handle checks, old helper signatures, chained write locks, direct DataVault callback casts, and `OwnerSystemID`.
- Diff Zero-GC scan returned 0 hits for added `new`, `string.Format`, `.ToString()`, LINQ, or `foreach`.
- Diff layout scan returned 0 hits for added `StructLayout`, `FieldOffset`, `UnsafeUtility.SizeOf`, `Marshal.OffsetOf`, or `sizeof`.
- Compile/import/profiler proof not produced: CPU was 75%, `dotnet` PID 59388 and `VBCSCompiler` PID 14544 were active.

## 2026-05-28T16:48:39+04:00 - Autonomous Extractor And VR Pipe Preview Exact Gates

What was wrong:
- `AutonomousExtractorSystem` hot-swapped DataVault through direct casts and released owned extractor handles by owner/nonzero descriptor instead of exact BufferID proof.
- `VRPipeBlueprintPreview` hot-swapped DataVault through a direct cast and used pipe preview handles in read/write/resolve paths without proving the exact pipe state, visual, and indirect-args lanes.

What was done:
- Pattern-matched previous/current DataVault services in `AutonomousExtractorSystem` and current DataVault in `VRPipeBlueprintPreview`.
- Passed exact expected BufferIDs into autonomous extractor release for job inputs, job results, cycle timers, buffered item IDs, buffered counts, and completed counts.
- Added `IsPipeVaultHandle` and required exact pipe `BufferID`, `SystemID.Construction`, and generation before VR pipe preview read reuse, post-ensure acceptance, write-lock acquisition, and locked resolve.

Cinematic Cheats used:
- None added. Existing extractor simulation and pipe hologram visual fake remain unchanged.

Exact Microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP2_20260528.json`
- SHA-256: `DEC533590AE1BF8FC966ADDA111E175402E66E2BC5FA602DDC2BEB758D47C5BC`
- Scoped grep over both patched files returned 0 hits for direct DataVault casts, ownerless handle checks, old helper names, chained write-lock expressions, and `OwnerSystemID`.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warnings.
- Compile/import/profiler proof not produced: CPU was 8%, but `VBCSCompiler` PID 14544 remained active.

## 2026-05-28T16:55:03+04:00 - Blueprint Preview Batch Exact Gates

What was wrong:
- `HectonBlueprintPreviewBatch` hot-swapped DataVault through a direct cast.
- Builder ghost state, visual, telemetry, and indirect-args handles were used before exact BufferID ownership proof.

What was done:
- Added `IsBlueprintVaultHandle` requiring exact `ShinobuSocketConstructionRuntime` BufferID, `SystemID.Construction`, and generation.
- Gated read reuse, read-only state/visual views, post-ensure acceptance, write-lock acquisition, locked resolve, and telemetry write with the exact predicate.
- Left builder ghost jobs, signal lane, graphics upload, DTO layout, and shared release semantics unchanged.

Cinematic Cheats used:
- None added. Existing hologram/indirect-args visual fake remains.

Exact Microseconds saved:
- Measured: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP3_20260528.json`
- SHA-256: `D2B634279289CF75F3465039AC2EF3E128C585915814B7DC0BE94C81DF241C0F`
- Scoped grep over the patched file returned 0 hits for direct DataVault casts, ownerless handle predicates, old handle helpers, chained write-lock expressions, and `OwnerSystemID`.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warning.
- Compile/import/profiler proof not produced: CPU was 64%, active `dotnet` PID 68368 and `VBCSCompiler` PID 53788.

## 2026-05-28T17:07:23+04:00 - Compile Repair After Targeted Build Failure

What was wrong:
- Guarded `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1 /p:UseSharedCompilation=false /nr:false` failed in 113.8s with 57 errors.
- Local audio error: `PlayerCriticalProceduralAudioRenderer.cs:7363` compared `uint` to `BufferID`.
- Dirty equipment error wall: `ModularEquipmentEngine.cs:1290-1317` passed `ref acquiredCount` while also writing `out` fields on a `ref struct`.

What was done:
- `PlayerCriticalProceduralAudioRenderer` now compares `handle.BufferID` to `unchecked((uint)(int)expectedBufferId)`.
- `ModularEquipmentEngine.TryAcquireEquipmentWriteBuffer` no longer receives `ref acquiredCount`; caller increments count only after each successful retained lock.
- Invalid acquired equipment write locks are released inside the helper before failure returns.

Cinematic Cheats used:
- None. This was compile/lifecycle repair only.

Exact Microseconds saved:
- Measured runtime: 0 us. Build wall time consumed: 113800000 us.

Evidence:
- `Docs/AgentLogs/Dump_AUDIT_NATIVE_STATE_COMPILE_REPAIR_20260528.json`
- SHA-256: `E72A8E3B96EF6D4878849EB78AF7EF96ACF06A3D0E886331A0E7499EE5C6CF92`
- Static grep over the two files returned 0 hits for `handle.BufferID == expectedBufferId` and `ref acquiredCount`.
- `git diff --check` reports only LF/CRLF warnings.
- Repeat compile not launched: CPU samples after the fixes were 100%, 79%, and 70%.

## 2026-05-28T17:19:02+04:00 - Vehicle Docking Exact Vault Gates

What was wrong:
- `VehicleDockingModule` hot-swapped DataVault through a direct cast.
- Docking telemetry ring/cursor handles were accepted by generic descriptor checks before read, write-lock, and release paths.

What was done:
- Replaced the direct callback cast with `currentService is IDataVault currentVault`.
- Added `IsDockTelemetryHandle` requiring exact `BufferID.VehicleDockingTelemetryRing` or `BufferID.VehicleDockingTelemetryCursor`, `SystemID.VehiclesPhysics`, and nonzero generation.
- Gated telemetry validation, both write-lock acquisitions, both write-lock releases, and read-handle reuse with the exact predicate.

Cinematic Cheats used:
- None added. Existing docking visuals, wake/impact/cargo behavior, and physics path were left unchanged.

Exact Microseconds saved:
- Measured runtime: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP4_20260528.json`
- SHA-256: `F6795F6803940390DB6CFE5224B27C393E293932F6AF8938A702421E447EA252`
- Source anchors: `VehicleDockingModule.cs:428`, `1089`, `1092`, `1343-1357`, `1394-1397`, `1410-1426`.
- Scoped bad-pattern grep returned 0 hits.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warning.
- Compile/import/profiler proof not produced: CPU sampled 88%, and external `dotnet` PID 21428 was running `dotnet build .\Hecton8.slnx --no-restore -m:1 -p:UseSharedCompilation=false -clp:ErrorsOnly`.

## 2026-05-28T17:30:22+04:00 - Sump Pump Drainage Exact Vault Gates

What was wrong:
- `SumpPumpPipeGridRuntime` hot-swapped DataVault through a direct cast.
- Validation, read/borrow, and release helpers accepted owner-local drainage handles through generic nonzero descriptor checks.
- The runtime owns 26 same-owner drainage lanes, so owner-only proof was not enough.

What was done:
- Replaced the DataVault callback cast with `currentService is IDataVault currentVault`.
- Added exact `SumpPumpDrainageBufferIds.*` expectations to `TryBorrowMutable`, `TryRead`, `HasResolvedBuffer`, and `ReleaseOwnedHandle`.
- Passed exact IDs for all drainage lanes `95820-95845` at validation, solver borrow, telemetry, visual upload, debug/gizmo reads, and release call sites.

Cinematic Cheats used:
- None added. The existing two-pass drainage solver, mock topology, visual flow upload, and continuous quality-weight budget were left unchanged.

Exact Microseconds saved:
- Measured runtime: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP5_20260528.json`
- SHA-256: `7847B706188B28890591CA8A79A91BA8967C5A185FDAA44188207904AD0593A8`
- Source anchors: `SumpPumpPipeGridRuntime.cs:261`, `504`, `512`, `553`, `565`, `525-550`, `578-601`, `1438-1468`.
- Scoped bad-pattern grep returned 0 hits.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warning.
- Compile/import/profiler proof not produced: CPU sampled 100%; external `dotnet` PIDs 67192/37700 and `csc` PID 36376 were active.

## 2026-05-28T17:40:59+04:00 - Foundation Pylon And Fluid Pipe Exact Vault Gates

What was wrong:
- `FoundationPylonGpuBatch` direct-cast the replacement DataVault.
- `FluidPipeGraphRuntime` used direct previous/current DataVault casts and generic nonzero handle predicates for 21 same-owner pipe graph lanes.
- Same-owner float/int lanes could be confused without exact BufferID proof.

What was done:
- Pattern-matched DataVault replacement in foundation pylon and fluid pipe hot-swap paths.
- Added exact pipe BufferID proof for read-only, ensure, write-lock acquisition, write-lock release, and buffer release paths.
- Mapped solve-lock bits back to exact pipe BufferIDs before `TryAcquireWriteLock`.

Cinematic Cheats used:
- None added. Pipe solver, rupture dispatch, room exchange, electrolysis integration, foundation SDF, GPU upload, and quality behavior were left unchanged.

Exact Microseconds saved:
- Measured runtime: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP6_20260528.json`
- SHA-256: `D3C677D6B1DB9F6031BBB209F04F05C74A61571512B9A72AB26CABA8955BEF29`
- Source anchors: `FoundationPylonGpuBatch.cs:855`; `FluidPipeGraphRuntime.cs:170`, `226-228`, `313-314`, `368-369`, `656-657`, `749-751`, `816-817`, `854-855`, `877-890`, `1069-1089`, `1213-1214`, `1229-1249`, `1262-1326`.
- Scoped bad-pattern grep returned 0 hits.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warnings.
- Compile/import/profiler proof not produced: CPU sampled 74%; external `dotnet` PID 3212 was active.

## 2026-05-28T17:47:33+04:00 - Construction Direct DataVault Cast Tail

What was wrong:
- `BulkheadContainmentRuntime` and `DroneFleetManager` still used direct DataVault casts in hot-swap callbacks.
- This did not close all handle predicate debt; it only removed the direct callback-cast class from Construction.

What was done:
- Bulkhead rebound/replaced callbacks now pattern-match `IDataVault` before `RequestDataVaultRebind`.
- DroneFleet service replacement now pattern-matches `IDataVault` before `RebindDroneDataVault`.

Cinematic Cheats used:
- None. Bulkhead/hatch/drone/pathing/rendering behavior was left unchanged.

Exact Microseconds saved:
- Measured runtime: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP7_20260528.json`
- SHA-256: `C420A096DAC2030573A093C3577CE35439E2585AEEA8C9B8EE10795E8622AF42`
- Construction-wide direct DataVault cast scan returned 0 hits.
- Residual construction handle predicate count remains 35.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warnings.
- Compile/import/profiler proof not produced: CPU sampled 83%.

## 2026-05-28T17:51:53+04:00 - Logistics Pipe Scheduler Exact Vault Gates

What was wrong:
- `LogisticsPipeTransportScheduler` accepted seven same-owner integer DAG-sort handles by nonzero descriptor checks.
- Because every logistics lane is `NativeArray<int>`, type checks alone could not catch wrong-lane descriptors.

What was done:
- Added exact BufferID validation for lanes `72054-72060`.
- Hardened read-only, locked resolve, write-lock acquisition, write-lock release, and buffer release helpers.
- Left DAG sort, crate transfer cadence, and cycle repair behavior unchanged.

Cinematic Cheats used:
- None. Scheduling and gameplay logic were unchanged.

Exact Microseconds saved:
- Measured runtime: 0 us. Expected steady-frame delta: 0 us.

Evidence:
- `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP8_20260528.json`
- SHA-256: `F0901C1A139A43E4D72F0F4F74AAAF1F2FC7DC0EFD30598949B2643D7FE95167`
- Source anchors: `LogisticsPipeTransportScheduler.cs:215`, `435-444`, `545-551`, `554-596`, `625-637`, `644-674`.
- Scoped bad-pattern grep returned 0 hits.
- Construction residual predicate count dropped from 35 to 30.
- Diff Zero-GC scan returned 0 hits.
- Diff layout scan returned 0 hits.
- `git diff --check` reports only LF/CRLF warning.
- Compile/import/profiler proof not produced: CPU sampled 42%, but external `dotnet` PID 56280 was active.
2026-05-28T18:35+04:00 | Construction Bulkhead/hatch exact-gate sweep
What was wrong: `BulkheadContainmentRuntime` and `BulkheadContainmentRuntime_HatchLocks` still allowed generic descriptor use. Owned Bulkhead/hatch `Resolve`, `Read`, write-lock, and release paths could accept any generated descriptor rather than proving the exact lane. Hatch partial still called the old write-lock helper and old created predicate.
What was done: Added expected `BufferID` to Bulkhead `Resolve<T>`, `Read<T>`, `TryAcquireWriteLane`, and `ReleaseVaultHandle`; all owned Bulkhead 220 and Shinobu 343 hatch lanes now pass exact IDs. Borrowed external reads for `PlayerKinematicState`, `StructuralIntegrityStates`, and `ShinobuFluidCompartmentFront` use exact `BufferID`+generation without lying that Construction owns them.
Cinematic Cheats used: None added. Existing pressure/hatch visual fake and quality-weight behavior stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is stability work, not a frame-time optimization.
Evidence: construction bad-pattern grep 0 hits; Bulkhead diff Zero-GC scan 0; layout diff scan 0; `git diff --check` only LF/CRLF warnings. Artifact `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_CONSTRUCTION_SWEEP9_20260528.json`, SHA-256 `D0051A0F159EC811244B2CA908278E01864961B33E0262311F8A62E309DD43D3`.
Blocked proof: no build/import/profiler run; guard sampled CPU 67% and active external `dotnet` PID 62104.
2026-05-28T18:47+04:00 | AudioLogSystem exact-gate sweep
What was wrong: `AudioLogSystem` used direct `currentService as IDataVault` on hot-swap and generic generated-handle checks across five AudioLog Vault lanes. The queue and encrypted fragment lanes share `uint`, so wrong-lane descriptors could pass type-level checks.
What was done: Added exact `IsAudioLogVaultHandle` checks for BufferIDs `70672-70676` with `SystemID.Audio`; all AudioLog read, write-lock, release, telemetry, and handle reuse paths now carry expected BufferIDs.
Cinematic Cheats used: None added. Playback, subtitle/HUD dispatch, save mask, and atmospheric radio behavior stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is stability work, not a frame-time optimization.
Evidence: scoped bad-pattern grep 0 hits; diff Zero-GC scan 0; layout diff scan 0; `git diff --check` only LF/CRLF warning. Artifact `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AUDIOLOG_20260528.json`, SHA-256 `7003C0129AE25283D0085557EC0F3D95F798CB3147420995300F785512C74929`.
Blocked proof: no build/import/profiler run; guard sampled CPU 97% with active `dotnet` PID 3560 and `csc` PID 23236.
2026-05-28T18:51+04:00 | HUDNotification exact queue gate
What was wrong: `HUDNotification` used direct DataVault casts on hot-swap and a generic generated-handle predicate for `HudNotificationQueue`.
What was done: Added exact `IsHudQueueHandle` for BufferID `74315` with `SystemID.UI`; queue ensure/read/write-lock/release paths now require that exact descriptor.
Cinematic Cheats used: None added. Notification text, severity visuals, queue overflow behavior, and tick timing stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is stability work, not a frame-time optimization.
Evidence: scoped bad-pattern grep 0 hits; diff Zero-GC scan 0; layout diff scan 0; `git diff --check` only LF/CRLF warning. Artifact `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_HUD_NOTIFICATION_20260528.json`, SHA-256 `BB3B606708712EB79352940AE0457E22A17D7CCC6145AC51C571EA8DBFEC82EC`.
Blocked proof: no build/import/profiler run; guard sampled CPU 99% with active `dotnet` PIDs 3392 and 57880.
2026-05-28T20:10+04:00 | VFX/GameplayTools exact handle gate sweep
What was wrong: `HullDentShaderController`, `MaterialDecayRuntime`, `WfcLaserCutRuntime`, and `ToolHapticsRuntime` still had direct DataVault casts or generic nonzero/generation handle predicates before resolve/release. WFC/haptic newly acquired handle failure paths could leave exact owned buffers alive.
What was done: Exact predicates now secure `HullDents=76`, `MaterialDecayBlackBox=273`, `WfcDoorCutProgress01=96`, `WfcLaserCutBlackBox=97`, `ToolHapticFrontCommands=234`, and `ToolHapticBackCommands=235` with the expected `SystemID` and generation. Failed WFC/haptic acquisitions release only exact owned descriptors.
Cinematic Cheats used: None added. Dent, material decay, WFC heat/cut, and haptic feedback behavior stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This removes stale/wrong-lane native memory risk.
Evidence: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_VFX_TOOLS_SWEEP1_20260528.json`, SHA-256 `D31A6868B0821DA7D7895F3BEFE896AE4060D7A27B39C0E5550264CBD4A5047E`; static bad-pattern/Zero-GC/layout scans are 0.
Blocked proof: no build/import/profiler run; guard sampled CPU 100% with `csc` PID 50396 and `dotnet` PIDs 55012, 65020.
2026-05-28T20:16+04:00 | AI Sensory acoustic echo exact-gate sweep
What was wrong: `AcousticEchoLocationRuntime` used direct DataVault cast on hot-swap and generic handle-created/owner checks for four AISensory lanes. `EchoTap` lanes could be confused by type alone.
What was done: Exact predicates now secure `AcousticEchoFrameTaps=229`, `AcousticEchoPendingTaps=636`, `AcousticEchoTrailState=230`, and `AcousticEchoBlackBox=231` with `SystemID.AISensory` and generation. Failed exact acquisitions release owned handles.
Cinematic Cheats used: None added. Portal echo math, predator trail job behavior, quality byte flow, and black-box DTO layout stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is stale/wrong-lane native memory hardening.
Evidence: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AI_SENSORY_SWEEP1_20260528.json`, SHA-256 `58C0DF873C9B79EF60AB2789CFF83676C82D42583FD0C10C669EEF48E847F390`; static bad-pattern/Zero-GC/layout scans are 0.
Blocked proof: no build/import/profiler run; guard sampled CPU 100% with active `dotnet` PID 65020.
2026-05-28T20:20+04:00 | VFX direct DataVault hot-swap cast tail
What was wrong: `ParasiteSwarmGpuRuntime`, `JacobianFoamGpuRuntime`, and `ShinobuPlasmaBeamRuntime` still used direct `currentService as IDataVault` in DataVault hot-swap callbacks.
What was done: Replaced all three with pattern-matched `IDataVault` rebinding. Existing exact `IsOwnedHandle` logic for each runtime was preserved.
Cinematic Cheats used: None added. Parasite GPU visuals, Jacobian foam, and plasma beam behavior stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is callback hygiene and stale-route prevention.
Evidence: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_VFX_DIRECT_CAST_SWEEP1_20260528.json`, SHA-256 `8695A8209558D9F82152495F97A9FEBF9EF7DE7AE30D65F3E6EAAD07F7001F6A`; static bad-pattern/Zero-GC/layout scans are 0.
Blocked proof: no build/import/profiler run; guard sampled CPU 96%.
2026-05-28T20:25+04:00 | Procedural ladder climb exact-gate sweep
What was wrong: `ProceduralLadderClimbRuntime` used direct DataVault cast and generic created-handle checks for five AnimationLocomotion Vault lanes.
What was done: Exact predicates now secure `LadderClimbIkInput=155`, `LadderClimbIkOutput=156`, `LadderAUPs=121`, `LadderClimbIkTelemetryRing=157`, and `LadderClimbIkTelemetryCursor=158` with `SystemID.AnimationLocomotion` and generation. Failed exact acquisitions release owned handles.
Cinematic Cheats used: None added. Existing camera-slide fake, ladder IK, and VR grip behavior stayed unchanged.
Exact Microseconds saved: measured 0 us; expected steady-frame delta 0 us. This is stale/wrong-lane native memory hardening.
Evidence: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_LADDER_CLIMB_SWEEP1_20260528.json`, SHA-256 `F70C922ACE729342CA8F219580B08E0DCB9032000410FFB62B99884A50F915DF`; static bad-pattern/Zero-GC/layout scans are 0.
Blocked proof: no build/import/profiler run; guard sampled CPU 99% with active `dotnet` PID 46540.
## 2026-05-28 - LaserCutter DOD Exact Vault Gate Sweep

What was wrong:
- `Assets/_Project/Scripts/Tools/LaserCutterDodRuntime.cs` used a generic nonzero/generation handle predicate for cutter buffers.
- Same owner does not prove same lane. Cutter lanes include primitive `int`, `byte`, and same-capacity DTO arrays; wrong-lane descriptors could pass type/created checks.
- Old release code would release any `SystemID.GameplayTools` handle from the field, even when the expected `BufferID` for that field was different.
- Rare failed acquisition validation could leave an exact newly acquired handle alive.

What was done:
- Threaded exact `BufferID` values through owned read/reuse/release checks.
- Added `IsLaserCutterVaultHandle(handle, bufferId)` requiring exact `BufferID`, `SystemID.GameplayTools`, and nonzero generation.
- Added `IsScalabilityStateHandle` for read-only external `ShinobuScalabilityState=70481` owned by `SystemID.GraphicsScalability`.
- Released only exact owned handles and released exact newly acquired handles on failed resolve/length validation.

Exact lanes secured:
- `71320 RequestsBuffer`
- `71321 RequestCountBuffer`
- `71322 SdfSnapshotBuffer`
- `71323 SdfProbeHitsBuffer`
- `71324 HitResultsBuffer`
- `71325 DeformationBuffer`
- `71326 BatteryDrainBuffer`
- `71327 GlowDecalBuffer`
- `71328 ImpactVfxBuffer`
- `71329 CooldownBuffer`
- `71330 TelemetryRingBuffer`
- `71331 TelemetryCursorBuffer`
- `71332 TuningBuffer`
- `71333 SpecBuffer`
- `71334 CsvScratchBuffer`
- `71335 CountersBuffer`
- `71336 RequestMetaBuffer`
- external read-only `70481 ShinobuScalabilityState`

Cinematic cheats used:
- No physical molten-material simulation added.
- Existing cheap visual route preserved: glow decals, deformation DTOs, impact VFX DTOs, and scalar spark count.
- Continuous quality route preserved: `_cachedGlobalQualityWeight` drives SDF steps and spark count through smooth curves.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; checks are scalar predicates adjacent to DataVault operations.
- Risk removed: wrong-lane DataVault reuse/release after Vault churn and failed acquisition leaks.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_LASER_CUTTER_DOD_SWEEP1_20260528.json`
- SHA-256: `BE429B943889F44CEE065CCCB960AF454E7B74D11AA4CDB0806875488749A72D`
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- Compile/import/profiler: not run. CPU sampled `100%`; active compiler processes were `csc` PID `39112` and `dotnet` PID `42888`.

## 2026-05-28 - FabricationAssembler Exact Vault Gate Sweep

What was wrong:
- `Assets/_Project/Scripts/FabricationAssemblerRuntime.cs` used direct DataVault hot-swap casts.
- Generic nonzero handle checks guarded fabrication read, write-lock, and release paths.
- Same owner did not prove the expected fabrication lane for job, runtime, GPU payload, telemetry, tuning, timing, or editor CSV scratch buffers.

What was done:
- Replaced DataVault hot-swap casts with pattern matching.
- Added `HasFabricationHandle(handle, bufferId)` requiring exact `BufferID`, `SystemID.Construction`, and nonzero generation.
- Added `HasScalabilityHandle` for read-only external `ShinobuScalabilityState=70481` owned by `SystemID.GraphicsScalability`.
- Threaded exact IDs into read, read-only, write-lock, release, simulation, post-simulation, visual sync, editor stats, and CSV ingestion paths.

Exact lanes secured:
- `71140 ShinobuFabricationJobs`
- `71141 ShinobuFabricationRuntime`
- `71142 ShinobuFabricationGpuPayload`
- `71143 ShinobuFabricationTelemetryRing`
- `71145 ShinobuFabricationTuning`
- `71146 ShinobuFabricationTimingLookup`
- `71147 ShinobuFabricationCsvScratch`
- external read-only `70481 ShinobuScalabilityState`

Cinematic cheats used:
- No physical assembly simulation added.
- Existing shader payload / GPU upload visual fake preserved.
- Continuous quality preserved through visual upload count and stride scaling.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; added checks are scalar descriptor predicates.
- Risk removed: wrong-lane fabrication buffer read/write-lock/release after Vault churn.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_FABRICATION_ASSEMBLER_SWEEP1_20260528.json`
- SHA-256: `4AA063464D1DCFCE75FB881702824DC1DF74800EE7CEFEC71DC64C4982A60927`
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- Compile/import/profiler: not run. CPU sampled `100%`; active compiler process was `dotnet` PID `1252`.

## 2026-05-28 - Migratory Sargassum Exact Vault Gate Sweep

What was wrong:
- `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs` stored Vault handles in `MigratoryVaultArray<T>` without storing the expected lane id.
- Resolve, write-lock, unlock, and release accepted any nonzero generated descriptor.
- Six same-owner WorldSargassum lanes could be confused after DataVault churn.

What was done:
- Added expected `BufferID` storage to `MigratoryVaultArray<T>`.
- Added exact `IsMigratorySargassumHandle(handle, bufferId)` requiring `BufferID`, `SystemID.WorldSargassum`, and generation.
- Exact-gated resolve, write-lock, unlock, release, and newly acquired handle validation.
- Exact newly acquired handles are released if resolve/length validation fails.

Exact lanes secured:
- `74369 WorldScatterMigratorySargassumIslands`
- `74370 WorldScatterMigratorySargassumScratchIslands`
- `74371 WorldScatterMigratorySargassumSelectedSources`
- `74372 WorldScatterMigratorySargassumFlowSamples`
- `74373 WorldScatterMigratorySargassumSpatialHandles`
- `74374 WorldScatterMigratorySargassumScratchSpatialHandles`

Cinematic cheats used:
- No physical seaweed simulation added.
- Existing data-only canopy islands and AUP spatial hash volumes preserved.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; added checks are scalar descriptor predicates.
- Risk removed: wrong-lane Sargassum native buffer resolve/write-lock/release after Vault churn.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_MIGRATORY_SARGASSUM_SWEEP1_20260528.json`
- SHA-256: `E43B526E012BFB8BF7795D24755641A8A21C02C5857FAC3EC4A936DF92F89698`
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- Compile/import/profiler: not run. CPU sampled `74%`.

## 2026-05-28 - Migratory Sargassum Lock-Final Repair

What was wrong:
- Fresh APEX self-audit found the Sargassum exact-gate patch did not fully satisfy lock-finalization proof.
- `TryAcquireMigratorySargassumJobBuffers` had branch release for partial lock acquisition.
- `ReleaseMigratorySargassumJobBufferLocks` released flow then islands without `finally`, so a flow-release fault could skip island-release.

What was done:
- Added `try/finally` partial-acquisition cleanup in `WorldProceduralScatterDirectorMigratorySargassum.cs`.
- Added `try/finally` release sequencing so `WorldScatterMigratorySargassumIslands=74369` is released even if `WorldScatterMigratorySargassumFlowSamples=74372` release faults.
- Preserved cross-frame job ownership; locks remain held only while `_migratorySargassumJobBuffersLocked` is true.

Cinematic cheats used:
- No physical seaweed simulation added.
- Existing data-only canopy island fake and AUP spatial hash route preserved.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; change touches acquisition/release edges, not per-island math.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_SARGASSUM_LOCK_FINAL_20260528.json`
- SHA-256: `94B6C1BEE088579F70A3565B1E235D71438CE2B0C15BE8330C59340316449095`
- Build-failure dump: `Docs/AgentLogs/Dump_AUDIT_NATIVE_STATE_BUILD_FAIL_AFTER_SARGASSUM_LOCK_FINAL_20260528.json`
- Dump SHA-256: `D3768C6F311C5414F72FDDAF9FD12B2584DBDD5FEDB28C9DFD90B823962D8928`
- Combined diff Zero-GC scan: `0` hits for added `new`, `string.Format`, `.ToString`, LINQ, and `foreach`.
- Layout diff scan: `0` hits.
- Bad-pattern scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warnings only.
- Build: guard sample CPU `45%`, no compiler processes; one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` run; exit `1` after `123.2s`, empty stdout/stderr. No compile-green claim.

## 2026-05-28 - Animation Exact Vault Gates

What was wrong:
- `KineticCharacterAnimatorRuntime` and `ProceduralBoneBlenderRuntime` still accepted generic generated handles for many same-owner lanes.
- Hot-swap callbacks used `currentService as IDataVault` / `previousService as IDataVault`.
- Release helpers could release same-owner handles without proving the expected BufferID.

What was done:
- Added exact BufferID predicates and owned resolve helpers.
- Replaced generic resolve calls with exact owned resolve calls.
- Release helpers now require the expected BufferID.
- Failed newly acquired invalid handles are released only when exact owner + BufferID proof succeeds.

Cinematic cheats used:
- No physical animation simulation expansion.
- Existing procedural IK, bone wave fake, and GPU upload cadence preserved.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; scalar predicates wrap existing Vault paths.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_ANIMATION_EXACT_GATES_20260528.json`
- SHA-256: `F9E1F630E9A86FA4A70C235B6A587E54DEB6DB425453FAF305B83FE0D276E905`
- Kinetic BufferIDs: `13671360-13671371`
- ProceduralBone BufferIDs: `71680-71690`
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warnings only.
- Build/import/profiler: not run. CPU sampled `91%`; resource throttle blocked build.

## 2026-05-28 - AI PathFunnel / Voxel A* Exact Vault Gates

What was wrong:
- `PathFunnelNavmeshRuntime` and `PathFunnelNavmeshRuntime_VoxelAStar` accepted generic nonzero/generation handles for same-owner AIPathfinding lanes.
- Release helpers could release same-owner handles without proving the expected BufferID.
- `WfcOutpostGrid=19` was cached as an external handle without exact `SystemID.CoreDataVault` proof.
- The editor pathing-profile write locks were branch-released instead of `finally`-released.

What was done:
- Added exact BufferID + SystemID + generation predicates for owned and external DataVault handles.
- Threaded exact BufferIDs through PathFunnel owned resolves/reads/releases and Voxel A* resolves/reads/releases.
- Replaced DataVault hot-swap `as IDataVault` with pattern matching.
- Wrapped the profile table/count write-lock pair in `try/finally`.

Cinematic cheats used:
- No physical pathing simulation expansion.
- Existing bounded Voxel A* data jobs, string-pulling fake, WFC invalidation, and black-box rings preserved.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; scalar descriptor predicates wrap existing Vault paths.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AI_PATHFUNNEL_EXACT_GATES_20260528.json`
- SHA-256: `36CBFE2AB8590D5A712AFC2A989DE0F9C87EC128C620BA7F8C00A3C267EF4423`
- PathFunnel BufferIDs: `195-199`.
- Voxel A* BufferIDs: `73420-73436`.
- External read-only BufferID: `WfcOutpostGrid=19`, owner `SystemID.CoreDataVault`.
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `git diff --check`: exit `0`, LF/CRLF warnings only.
- Build/import/profiler: not run. CPU sampled `100%`; active `dotnet` PID `6896` and `csc` PID `33428` blocked build.

## 2026-05-28 - AI Ambient / Ecosystem Exact Vault Gates

What was wrong:
- `AmbientBiotaDirector` still relied on generic generated-handle proof in readiness/release paths for several same-owner AmbientBiota lanes.
- `EcosystemPopulationBalancer` used a direct `currentService as IDataVault` callback cast and a generic nonzero/generation resolver for owned AIEcology lanes.
- Ecosystem borrowed `EntityAUPs` and `EntityFlags` through the same generic resolver, without exact external BufferID proof.
- Ecosystem cross-frame `TryLockBuffer` partial acquisition released through repeated branches instead of a finally-owned partial-release path.

What was done:
- Ambient owned lanes now require exact `BufferID`, `SystemID.AmbientBiota`, and generation before readiness, resolve, and release.
- Ecosystem owned lanes now require exact `BufferID`, `SystemID.AIEcology`, and generation before readiness, resolve, failed-acquire release, and release.
- Borrowed entity lanes are exact-gated by `BufferID` plus generation only; no unproven owner was invented.
- Ecosystem partial job-lock acquisition releases from `finally`, and teardown lock state resets from `finally`.

Cinematic cheats used:
- No physical simulation expansion.
- Existing Ambient continuous `HomeostasisBrain.GlobalQualityWeight` scaling preserved.
- Ecosystem remains a data-only ecology governor; no binary low-end switch was added.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; exact predicates and finally blocks wrap existing DataVault operations.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AI_AMBIENT_ECOSYSTEM_EXACT_GATES_20260528.json`
- SHA-256: `C14241C703FBFB890CE501ECD12173CF150CB03FF52F6123891934CC05F710DC`
- Ambient BufferIDs: `91`, `92`, `93`, `225`, `159`, `160`.
- Ecosystem owned BufferIDs: `205`, `206`, `207`, `208`, `209`, `210`.
- Ecosystem borrowed BufferIDs: `EntityAUPs=13`, `EntityFlags=29`.
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Layout diff scan: `0` hits.
- `TryAcquireWriteLock` scan in scope: `0` hits; this slice uses `TryLockBuffer` job locks, not write-lock leases.
- `git diff --check`: exit `0`, LF/CRLF warnings only.
- Build/import/profiler: one build attempt ran after CPU dropped to `47%` and no compiler processes were active. `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` timed out after `244019 ms` with exit `124`; orphaned `dotnet` PID `53816` and `VBCSCompiler` PID `51936` were killed. Post-kill compiler scan was empty, CPU was `100%`. Unity import, Play Mode, profiler, GCMonitor, and native ledger proof were not run.
- Failure dump: `Docs/AgentLogs/Dump_AUDIT_NATIVE_STATE_BUILD_TIMEOUT_AI_AMBIENT_ECOSYSTEM_20260528.json`, SHA-256 `400AE15289A0D89A1AA6DE57928440A6D2ECA2D9AA2EC9CB76ED76D96673CF16`.

## 2026-05-28 - AI Symbiosis / EcosystemBalancer Exact Vault Gates

What was wrong:
- `ShinobuFloraFaunaSymbiosisSolver` still used direct DataVault hot-swap casts, generic nonzero handle checks, ownerless resolve/release, and branch-only partial `TryLockBuffer` release.
- `ShinobuEcosystemBalancer` still used direct DataVault hot-swap casts, generic same-owner resolve/release helpers, macro external handle validation without exact BufferID, and branch-only 28-lane job-lock release.

What was done:
- Added exact `BufferID + SystemID.AIEcology + generation` gates for owned handle reuse, resolve, and release in both files.
- Kept external lanes owner-honest: `ShinobuSeedShipAnomalyField=70700` is exact BufferID only in the symbiosis reader, with ownership left to `SeedShipAnomalyRuntime`; `ShinobuMacroEcosystemSectorFront=70433` is exact BufferID + generation in the ecosystem macro read.
- Converted both partial job-lock acquisition paths and both teardown unlock paths to `try/finally`.

Cinematic cheats used:
- No physical simulation added.
- Existing symbiosis/ecosystem data fakes, emergency flow, flocking, spatial grid, GPU upload, and continuous `GlobalQualityWeight` scaling preserved.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; predicates and finally blocks wrap existing Vault operations.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_AI_ECOSYSTEM_SYMBIOSIS_BALANCER_EXACT_GATES_20260528.json`
- SHA-256: `8AD238723E51F2E1874E5DF72601A6EAC0C4E0C131D0F9196F55C7C2A7572A57`
- Symbiosis owned BufferIDs: `70415-70432`; ambient borrow/claim lanes `70400`, `70401`; external anomaly field `70700`.
- Ecosystem owned BufferIDs: `70400-70414`, `70443-70459`, `70474-70476`; external macro sector front `70433`.
- Bad-pattern scan: `0` hits.
- Staged diff Zero-GC/layout scans before checkpoint: `0` hits.
- Final source scan: no string/LINQ/foreach hits; one pre-existing cold forensic `new Thread` remains outside patched hot paths.
- `git diff --check`: exit `0`.
- Build/import/profiler: not run. Compiler process samples were empty, but CPU sampled `99%` then `65%`, so `dotnet build` was blocked by resource throttle.

## 2026-05-28 - ToolKinematics Exact Vault Gates

What was wrong:
- `ToolKinematicsRuntime` still used `currentService as IDataVault`.
- Its Vault helper accepted any nonzero `BufferID` + generation and did not prove exact lane ownership for the 15 `SystemID.GameplayTools` buffers.
- Release used the same ownerless helper, so a stale/wrong-lane handle could be released if descriptor identity drifted.

What was done:
- Hot-swap now pattern-matches `IDataVault`.
- Resolve/reuse/release now requires exact `BufferID`, `SystemID.GameplayTools`, and generation.
- Secured BufferIDs: `ToolKinematicsStates=605`, `FrameInputs=606`, `HitResults=607`, `IkOutputs=608`, `RecoilStates=609`, `Tuning=610`, `ScreenExports=611`, `TelemetryRing=612`, `MockTriggerSignals=613`, `MockCarveRequests=614`, `HeatSignals=615`, `SparkRequests=616`, `BeamVertices=617`, `BeamVertexCounts=618`, `PoseOutputs=619`.
- Failed newly acquired exact handles are released if resolve/length validation fails.

Cinematic cheats used:
- No physical simulation added.
- Existing mock SDF, beam mesh, heat/recoil, and ToolKinematics visual/data fakes preserved.
- Existing continuous quality behavior preserved; no binary low-end switch introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; this is correctness hardening around existing Vault operations.

Evidence:
- Artifact: `Docs/AgentLogs/APEX_AUDIT_NATIVE_STATE_TOOL_KINEMATICS_EXACT_GATES_20260528.json`
- SHA-256: `B28F77CDF94DE43E09F06CFD16F0520001ED31DF40A1FD0BCBF6EC7516898731`
- Modified anchors: hot-swap line `870`, exact predicate line `887`, existing resolve line `910`, newly acquired validation line `925`, release calls lines `1484-1498`, release predicate line `1506`.
- Bad-pattern scan: `0` hits.
- Diff Zero-GC scan: `0` hits.
- Diff layout scan: `0` hits.
- `git diff --check`: exit `0`.
- Build/import/profiler: not run. CPU sampled `100%` and `VBCSCompiler` PID `53464` was active.
