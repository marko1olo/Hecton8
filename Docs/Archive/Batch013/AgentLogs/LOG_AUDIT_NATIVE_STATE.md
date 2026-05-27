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
