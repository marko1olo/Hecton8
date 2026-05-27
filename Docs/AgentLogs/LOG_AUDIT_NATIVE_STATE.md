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
