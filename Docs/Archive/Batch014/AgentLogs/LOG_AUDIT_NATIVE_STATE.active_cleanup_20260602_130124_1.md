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

## 2026-05-29 - Chained DataVault Write-Lock Validation Repair

What was wrong:
- Chained `TryAcquireWriteLock(...) || !buffer.IsCreated || buffer.Length...` patterns could leak a DataVault write lock when acquisition succeeded and post-acquire validation failed.
- `PropwashGpuTunerWindow` could call `ReleaseWriteLock` after failed acquire because release was gated by handle validity rather than an acquired flag.

What was done:
- Split acquisition and validation in the affected runtime/editor helpers.
- Ensured failed post-acquire validation releases in `finally` or immediate release before returning.
- Preserved existing caller-owned handoff in methods that intentionally return a locked `NativeArray` view.

Cinematic cheats used:
- None. No physical simulation, DTO layout, quality curve, phase order, or visual fidelity route was changed.

Exact microseconds saved:
- Measured: `0 us`.
- Expected steady-frame delta: `0 us`; the patch removes lock/freeze risk on existing write paths.

Evidence:
- Patched source files: `HectonInputRuntime_HapticSynth.cs`, `VegetationNavGridSynchronizer.cs`, `HectonWorldGenerator.cs`, `SpatialAudioManager.cs`, `PlayerCriticalProceduralAudioRenderer.cs`, `SubmarineOsThermalGridRuntime.cs`, `SubmarineAtmosphereSystem.cs`, `ThermodynamicsHazardGridRuntime.FileWorker.cs`, `ModuleDeconstructionResourceReturnEditor_SHINOBU336.cs`, `RadiationShieldingTunerWindow.cs`, `SubmarineBallastTunerWindow.cs`, `PropwashGpuTunerWindow.cs`.
- `rg '!.*TryAcquireWriteLock.*\\|\\|' Assets -g '*.cs'`: no source hits.
- `git diff --check` on patched files: exit `0`, LF/CRLF warnings only.
- Build/import/profiler: not run; current mandate requested static validation and no build spam.

## 2026-05-29 - GlobalPhysicsStateManager Write-Lock Group Flattening

What was wrong:
- `GlobalPhysicsStateManager` culling/scheduling paths held multiple DataVault write locks simultaneously.
- Affected slices: tracked position publish, culling scheduling, culling dispatch, Shinobu37 clear, tracked lane mutation, target wake queue, mock body generation, and runtime clear.
- This was a deadlock/stall vector against DataVault writer metadata and compaction guards.

What was done:
- Added cold cached `_nativeStateDataVault`.
- Added `VaultBufferBinding<T>.TryResolve` and `VaultBufferBinding<T>.HasValidView`.
- Replaced multi-lock groups with one `TryAcquireMutationGuard(mask)` per phase slice.
- Kept post-acquire NativeArray view validation and existing `finally` release paths.

Cinematic cheats used:
- None. No new physics or presentation simulation was added.
- Existing continuous culling quality behavior remains unchanged.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: fewer DataVault metadata lock transitions and no nested writer ownership. No profiler sample was run.

Evidence:
- Patched source files: `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`, `Assets/_Project/Scripts/Physics/GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`.
- Key anchors: `TryResolve` line `337`, `HasValidView` line `343`, cached `_nativeStateDataVault` line `684`, mutation guard helpers lines `1276-1290`, scheduling guard line `3148`, dispatch guard line `3165`, runtime clear guard line `4749`, Shinobu37 clear guard line `921`, tracked lane guard line `1548`, target wake guard line `1571`, mock body guard line `1854`.
- `git diff --check` on both patched files: exit `0`, LF/CRLF warnings only.
- Brace scanner over all `GlobalPhysicsStateManager*.cs`: `0` methods with more than one `.TryAcquireWriteLock(` call.
- Added diff Zero-GC scan: `0` hits.
- Added dependency scan for `GlobalRegistry.Get`/`GetComponent`: `0` hits.
- Build/import/profiler: not run; CPU sample was `48.27%` and current mandate requested static validation/no build spam.

## 2026-05-29 - BiomeBoundarySdfRuntime Map/Sample Write-Lock Flattening

What was wrong:
- `BiomeBoundarySdfRuntime` held nested DataVault write locks for biome map/hash refresh and map/hash/sample result execution.
- The phase is single-owner WorldStreaming mutation; multiple writer locks added lock-order risk without changing data ownership.

What was done:
- Added `BiomeMapMutationGuardMask` and `SampleMutationGuardMask`.
- Map refresh/clear and sample execution now reserve one mutation guard, validate exact `BufferID` handles, resolve native views with `TryResolveHandle`, and release one guard through the existing `finally` paths.
- The telemetry ring stayed as one direct write-lock lane because it is independent and already single-buffer.

Cinematic cheats used:
- None. The existing heatmap/SDF approximation remains the cheap visual route.
- No physical biome simulation, DTO layout change, or binary quality switch was added.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: fewer DataVault metadata transitions and no nested writer ownership. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/World/Biomes/BiomeBoundarySdfRuntime.cs`.
- Key anchors: mutation masks lines `37-42`, biome map guard method line `430`, biome map release line `468`, sample guard method line `477`, sample release line `520`, `MutationGuardBit` line `595`.
- `git diff --check` on the file: exit `0`, LF/CRLF warning only.
- Method scanner on the file: `OFFENDERS=0` for methods with more than one `.TryAcquireWriteLock(`.
- Added diff Zero-GC/dependency scan: `0` hits for added `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, or `GetComponent`.
- Build/import/profiler: not run; CPU sample was `19.38%` and current mandate requested static validation/no build spam.

## 2026-05-29 - SpatialAudio Acoustic Portal Write-Lock Flattening

What was wrong:
- `SpatialAudioManager` acquired six acoustic portal work write locks and two scratch write locks for synchronous path evaluation.
- It also acquired previous-velocity AUP/frame write locks together during audio velocity tracking.
- Virtual voice append acquired voice/DTO/source/previous-AUP write locks together for each accepted voice.
- Acoustic occlusion scheduling acquired selected-source, selected-previous-AUP, and DSP-output write locks across a scheduled job.
- These were real nested writer groups in audio runtime paths, separate from sequential cursor/ring telemetry false positives.

What was done:
- Added `VirtualVoiceAppendMutationGuardMask`.
- Added `AcousticOcclusionMutationGuardMask`.
- Added `AcousticPortalWorkMutationGuardMask` and `AcousticPortalScratchMutationGuardMask`.
- Added `PreviousVelocityAupMutationGuardMask`.
- Replaced virtual voice append, acoustic occlusion, portal work/scratch, and previous-velocity write-lock chains with one mutation guard per group.
- Resolved all required buffers through existing exact audio-handle `TryOpenAudioVaultBuffer` validation.
- Existing `finally` release sites now release one guard per acquired group.

Cinematic cheats used:
- None added. Existing portal pathing remains a cheap bounded path fake gated by continuous virtual voice quality weight.
- No physical acoustic simulation, DTO change, or binary quality switch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: seventeen nested write-lock acquisitions became five mutation guard reservations. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/SpatialAudioManager.cs`.
- Key anchors: virtual voice append mask lines `513-517`, acoustic occlusion mask lines `521-524`, append acquire line `3051`, append guard acquire line `3071`, append guard release line `3190`, occlusion schedule line `3354`, occlusion guard acquire line `3375`, occlusion guard release line `8718`, work acquire line `8905`, scratch acquire line `9053`, `AudioVaultMutationGuardBit` line `9295`.
- `git diff --check` on the file: exit `0`, LF/CRLF warning only.
- Targeted scans: `AppendVirtualVoice directWriteLocks=0 helperWriteLocks=0 mutationGuards=1`; `ScheduleAcousticOcclusionJob directWriteLocks=0 helperWriteLocks=1 mutationGuards=1` with the remaining helper being the independent material-rows lock; portal work/scratch and previous-velocity acquire helpers report no direct/helper write-lock calls.
- Added diff non-struct Zero-GC/dependency scan: `ADDED_NONSTRUCT_PATTERN_HITS=0`; added `new` scan reports no hits.
- Build/import/profiler: not run; current mandate requested static validation/no build spam.

## 2026-05-29 - UtilityAI Anxiety Tuning Write-Lock Flattening

What was wrong:
- `UtilityAICognitionVault.TrySetAnxietyTuning` acquired tuning and profile DataVault write locks in one method.
- This was a nested AI cognition writer path even though the work itself is small and usually editor/cold driven.

What was done:
- Added `AnxietyTuningProfileMutationGuardMask`.
- Replaced tuning/profile write locks with one mutation guard.
- Added exact tuning/profile `BufferID` checks before resolving native views.
- Kept existing tuning sanitization and default profile derivation unchanged.

Cinematic cheats used:
- None. No AI behavior math, DTO layout, telemetry layout, or CSV route was changed.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: two write-lock acquisitions became one mutation guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs`.
- Key anchors: mutation mask lines `97-99`, `TrySetAnxietyTuning` line `295`, guard acquire line `300`, guard release line `339`, `AnxietyVaultMutationGuardBit` line `535`.
- `git diff --check` on the file: exit `0`, LF/CRLF warning only.
- Targeted scan: `TrySetAnxietyTuning line=295 directWriteLocks=0 mutationGuards=1 mutationReleases=1`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- Build/import/profiler: not run; current mandate requested static validation/no build spam.

## 2026-05-29 - SpatialAudio Sort/Rebase Write-Lock Tail Flattening

What was wrong:
- `SpatialAudioManager.FastTick` still acquired virtual voice sort pool, sort key pool, selections, and statistics through helper write-lock calls before scheduling `VirtualVoiceSortJob`.
- `RebaseAcousticSourcePool` still acquired source DTO and previous-AUP buffers together during floating-origin rebasing.
- These were the last two `SpatialAudioManager` helper-lock multi-methods reported by the previous targeted scanner.

What was done:
- Added `VirtualVoiceSortMutationGuardMask` for `SpatialAudioVirtualVoiceSortPool`, `SpatialAudioVirtualVoiceSortKeyPool`, `SpatialAudioVirtualVoiceSelections`, and `SpatialAudioVirtualVoiceStatistics`.
- Replaced `FastTick` sort-buffer helper write-lock acquisition with one mutation guard plus exact `TryOpenAudioVaultBuffer` validation for all four buffers.
- Changed `ReleaseVirtualVoiceSortBufferLocks` to clear the job-held flags and release the sort mutation guard exactly once.
- Replaced `RebaseAcousticSourcePool` source/AUP helper write locks with a dynamic two-buffer mutation guard and exact handle resolution.

Cinematic cheats used:
- None added. The existing virtual voice sort, SDF sampler, portal fake, and continuous `GlobalQualityWeight` voice budget remain unchanged.
- No physical audio simulation, DSP rewrite, DTO layout change, or binary quality switch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: six helper write-lock acquisitions became two mutation guard reservations. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/SpatialAudioManager.cs`.
- Key anchors: `VirtualVoiceSortMutationGuardMask` lines `508-512`, `FastTick` line `1948`, sort guard acquire line `2074`, sort guard failure release line `2140`, sort guard final release line `8705`, `RebaseAcousticSourcePool` line `4586`, rebase guard acquire line `4598`, rebase guard release line `4627`, `AudioVaultMutationGuardBit` line `9289`.
- Targeted method scan: `FastTick DirectWriteLocks=0 MutationGuards=1 Releases=1 GlobalRegistryGet=0 GetComponent=0`; `RebaseAcousticSourcePool DirectWriteLocks=0 MutationGuards=1 Releases=1 GlobalRegistryGet=0 GetComponent=0`.
- SpatialAudio helper scanner: `SPATIAL_AUDIO_HELPER_MULTI_METHODS=0`.
- Added diff Zero-GC/dependency scans: `ADDED_NONSTRUCT_PATTERN_HITS=0`; `ADDED_DEPENDENCY_PATTERN_HITS=0`.
- Scoped `git diff --check`: exit `0`, LF/CRLF warnings only.
- Broad all-scripts write-lock scanner: timed out after `60000 ms`; broad hot dependency scanner: timed out after `60000 ms`.
- CPU/build guard: final CPU sample returned `100`; no compiler process rows were listed by that sample. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or fresh native ledger proof was launched.

## 2026-05-29 - Tether Editor Validator Write-Lock Flattening

What was wrong:
- `TetherMemorySovereigntyValidator1303.RunDefragRaceFuzzer` acquired 12 DataVault write locks at once while a defrag worker thread ran.
- The code was editor-only, but it contradicted the lock-flattening rule in the memory-sovereignty validator itself.

What was done:
- Added `StressMutationGuardMask` for `TetherVerletPositions=326`, `TetherVerletPreviousPositions=327`, `TetherVerletVelocities=328`, `TetherVerletPinnedPositions=329`, `TetherVerletPinnedMask=330`, `TetherVerletSegmentRestLengths=331`, `TetherVerletSegmentTensions=332`, `TetherVerletCorrections=333`, `TetherVerletCorrectionWeights=334`, `TetherVerletSolverStats=335`, `TetherVerletSolverFlags=336`, and `TetherVerletNodeFaultFlags=337`.
- Replaced the 12 write-lock chain with one `TryAcquireMutationGuard`.
- Added exact handle validation for `BufferID`, `SystemID.Physics`, and generation before resolving every stress buffer.
- Released the mutation guard exactly once in `finally` after stopping and joining the compaction worker.

Cinematic cheats used:
- None added. Tether runtime solver, fuzzer stress workload, and defrag worker behavior remain unchanged.
- No binary quality switch, DTO layout change, or physical simulation expansion was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: 12 editor validator write-lock acquisitions became one mutation guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Physics/TetherVerletJobs.cs`.
- Key anchors: `StressMutationGuardMask` line `530`, exact validations lines `624-635`, guard acquire line `645`, exact resolves lines `652-663`, guard release line `745`, `TryResolveStressBuffer` line `834`.
- Targeted method scan: `RUN_DEFRAG_DIRECT_WRITELOCKS=0`, `RUN_DEFRAG_MUTATION_GUARDS=1`, `RUN_DEFRAG_MUTATION_RELEASES=1`, `RUN_DEFRAG_GLOBALREGISTRY_GET=0`, `RUN_DEFRAG_GETCOMPONENT=0`, `RUN_DEFRAG_VALIDATE_EXACT=12`.
- Added diff scans for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: no hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- Broad all-scripts write-lock scanner: timed out after `64015 ms`; broad hot dependency scanner: timed out after `64025 ms`.
- CPU/build guard: CPU sample returned `100`; no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - TetherInstance Synchronous Writer Lock Flattening

What was wrong:
- `TetherInstance` synchronous runtime paths still held multi-buffer DataVault writer groups in visual fallback, cable publish/clear, telemetry failure/dump, and Verlet bootstrap.
- These paths did not need job-held ownership, so nested writer locks added deadlock/stall risk without changing gameplay truth.

What was done:
- Added `VisualFallbackMutationGuardMask`, `CableStateMutationGuardMask`, `TelemetryMutationGuardMask`, and `VerletBootstrapMutationGuardMask`.
- Converted `UpdateVisuals`, `PublishDataVaultCableState`, `ClearDataVaultCableEntry`, `TryWriteVaultFailureTelemetry`, `DumpVerletTelemetryOnce`, and `InitializeVerletRuntime` to one mutation guard plus exact `BufferID/SystemID.Physics/generation` resolve views.
- Left `RunVerletSolver` unchanged because its buffers are passed to scheduled jobs and need a separate completion-handoff design.

Cinematic cheats used:
- Preserved the existing cheap visual catenary fallback and quality-scaled upload cadence.
- No physical cable simulation, DTO layout change, or binary quality switch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: visual/cable/telemetry/bootstrap multi-lock groups now reserve one guard each. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/TetherInstance.cs`.
- Key anchors: masks lines `117-140`, `UpdateVisuals` guard `686/773`, `InitializeVerletRuntime` guard `1230/1370`, telemetry guards `2008/2039` and `3386/3446`, cable-state guards `2545/2655` and `2722/2800`.
- Targeted method scan: `UpdateVisuals helperAcquire=0 mutationGuard=1 mutationRelease=1`; `PublishDataVaultCableState helperAcquire=0 mutationGuard=1 mutationRelease=1`; `ClearDataVaultCableEntry helperAcquire=0 mutationGuard=1 mutationRelease=1`; `TryWriteVaultFailureTelemetry helperAcquire=0 mutationGuard=1 mutationRelease=1`; `DumpVerletTelemetryOnce helperAcquire=0 mutationGuard=1 mutationRelease=1`; `InitializeVerletRuntime helperAcquire=1 mutationGuard=1 mutationRelease=1` with the helper lock after guard release; hot dependency hits `0`.
- Added diff scans for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: no hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `100`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - TetherInstance RunVerletSolver Guard Handoff

What was wrong:
- `RunVerletSolver` scheduled jobs over solver/telemetry DataVault buffers but released the old write locks in the method `finally`.
- That left pending jobs with native views after writer protection was gone, and the method still held a 14-buffer lock group before scheduling.

What was done:
- Added `VerletSolveMutationGuardMask` for the solver bootstrap lanes, correction lanes, and tether telemetry ring/head.
- Replaced all `RunVerletSolver` helper write-lock acquisition with exact resolve-only views under one mutation guard.
- Added `_pendingVerletSolveGuardVault` and `_pendingVerletSolveGuardHeld` so the guard transfers with `_pendingVerletSolveHandle`.
- Added `ReleasePendingVerletSolveGuard`, called from `CommitPendingVerletSolve` after the job fence and before result publish mutations.

Cinematic cheats used:
- Preserved the existing Verlet solver and cheap visual catenary fallback.
- No physical cable simulation, quality-tier binary switch, or DTO layout change was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: 14 writer lock acquisitions became one job-held mutation guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/TetherInstance.cs`.
- Key anchors: `VerletSolveMutationGuardMask` line `142`, pending guard fields lines `348-349`, solve guard acquire line `2879`, guard transfer lines `3154-3156`, fallback release lines `3163-3164`, commit release line `3196`, `ReleasePendingVerletSolveGuard` lines `3211-3220`.
- Targeted scan: `RunVerletSolver helperAcquire=0 mutationGuard=1 mutationRelease=2 writeLockRelease=0 hotDependencyHits=0`; `ReleasePendingVerletSolveGuard helperAcquire=0 mutationGuard=0 mutationRelease=1 writeLockRelease=0 hotDependencyHits=0`.
- Helper-acquire caller scan: only `TryAcquireDataVaultCableSlice<T>` itself has more than one helper call.
- Hot dependency/LINQ/foreach scan over solver/finalize/presentation methods: `0`.
- Added diff scans for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: no hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `51`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - EcosystemDirector Job-Held Lock Flattening

What was wrong:
- `EcosystemDirector` still held four multi-buffer AIEcology writer groups with `TryLockBuffer/TryUnlockBuffer`.
- The groups covered sector solve `25` lanes, fauna genome mutation `11` lanes, macro swarm travel `15` lanes, and apex territory overlap `2` lanes.
- Partial-unlock counters were required to unwind acquisition failure, which preserved a lock-order/deadlock vector.

What was done:
- Added `SectorSolveMutationGuardMask`, `GenomeMutationGuardMask`, `MacroSwarmTravelMutationGuardMask`, and `ApexTerritoryOverlapMutationGuardMask`.
- Replaced the four multi-lock acquisition chains with one `TryAcquireMutationGuard` per job-held group.
- Added `IsOwnedHandle` and `IsAIEcologyBuffer` validation so each guard handoff proves exact `BufferID`, `SystemID.AIEcology`, generation, and a resolved native view.
- Added `_solveJobGuardVault`, `_genomeMutationJobGuardVault`, `_macroSwarmTravelJobGuardVault`, and `_apexTerritoryOverlapJobGuardVault` so unlock targets the same vault instance that granted the guard.

Cinematic cheats used:
- Preserved current Lotka-Volterra biomass approximation, macro swarm diffusion, fauna genome mutation pass, and apex overlap sampling.
- No physical ecology simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: `53` buffer lock acquisitions across the four routes became `4` mutation guard reservations. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/World/EcosystemDirector.cs`.
- Key anchors: masks lines `330-386`, guard owner fields lines `1438-1441`, `TryLockSectorSolveJobBuffers` line `4897`, `TryLockGenomeMutationJobBuffers` line `4965`, `TryLockMacroSwarmTravelJobBuffers` line `5019`, `TryLockApexTerritoryOverlapJobBuffers` line `5077`, `IsAIEcologyBuffer` lines `5122-5125`.
- Targeted scan: all eight lock/unlock methods report `DirectWriteLocks=0`, `BufferLocks=0`, `HotDependencies=0`; each `TryLock*` has `MutationGuards=1` and failure-path `MutationReleases=1`; each `Unlock*` has `MutationReleases=1`.
- Source scan for `TryLockBuffer`, `TryUnlockBuffer`, `TryLockAIEcologyBuffer`, and stale partial-unlock overload calls in `EcosystemDirector.cs`: no hits.
- Added diff scans for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: no hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU samples returned `52` then `59`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - SubmarineAtmosphere Job/Telemetry Lock Flattening

What was wrong:
- `SubmarineAtmosphereSystem.TryLockAtmosphereJobBuffers` held 26 HabitatAtmosphere writer lanes with `TryLockBuffer/TryUnlockBuffer`.
- Black-box telemetry wrote cursor and ring through two separate direct write locks instead of one exact owner route.

What was done:
- Added `AtmosphereJobMutationGuardMask`, `AtmosphereTelemetryMutationGuardMask`, and `_atmosphereJobMutationGuardVault`.
- Replaced the atmosphere step job buffer lock chain with one mutation guard plus exact `BufferID`, `SystemID.HabitatAtmosphere`, generation, and length validation.
- Stored the guard vault owner so release on completion, dispose, or DataVault hot-swap targets the same vault that granted the guard.
- Converted telemetry cold clear, black-box record, and failure record to one telemetry mutation guard resolving cursor+ring under exact validation.

Cinematic cheats used:
- Preserved compartment/Dalton pressure approximation, soot overlay, visor glitch, and pressure audio fakes.
- No physical gas simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: 26 job buffer locks plus two telemetry write-lock routes became two mutation guard routes. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs`.
- Key anchors: `AtmosphereJobMutationGuardMask` near line `1123`, `AtmosphereTelemetryMutationGuardMask` near line `1149`, `_atmosphereJobMutationGuardVault` near line `1866`, `TryLockAtmosphereJobBuffers` near line `5575`, `TryAcquireAtmosphereTelemetryWriteGuard` near line `5618`, `RecordAtmosphereBlackBox` near line `5828`, `RecordAtmosphereFailure` near line `5883`.
- Targeted scan: `TryLockAtmosphereJobBuffers directWriteLocks=0 writeReleases=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDependencies=0`; telemetry guard acquire `mutationGuards=1 mutationReleases=1`; black-box/failure/cold clear `directWriteLocks=0`.
- File scanner: `TOTAL_MULTI_DIRECT_WRITELOCK_METHODS=0`.
- Source scan for `TryLockBuffer`, `TryUnlockBuffer`, stale atmosphere job buffer helpers: no hits.
- Added diff scans for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: count `0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `82`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - AmbientBiota Job Pin Lock Flattening

What was wrong:
- `AmbientBiotaDirector.TryPinBiotaJobBuffers` still pinned `BiotaAUPs`, `BiotaVelocities`, and `BiotaStates` with three `TryLockBuffer` calls while scheduled drift/spawn jobs owned the native views.

What was done:
- Added `BiotaJobMutationGuardMask` and `_jobBufferGuardVault`.
- Replaced the three legacy pins with one mutation guard and exact `TryResolveBiotaBuffers(_capacity, ...)` validation before and after acquire.
- Released the stored guard after `LateFrameTick` job finalization or teardown; DataVault rebind now releases a finalized leftover guard before handles are released.

Cinematic cheats used:
- Preserved existing ambient drift, spawn, macro hydration, indirect draw, and quality-weight scaling.
- No physical swarm simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: three job writer pins became one guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs`.
- Key anchors: `BiotaJobMutationGuardMask` near line `76`, `_jobBufferGuardVault` near line `177`, `TryPinBiotaJobBuffers` near line `949`, `ReleaseBiotaJobBufferPins` near line `986`, rebind guard release near line `785`.
- Targeted scan: `TryPinBiotaJobBuffers directWriteLocks=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDependencies=0`; `ReleaseBiotaJobBufferPins directWriteLocks=0 bufferLocks=0 mutationReleases=1`; `Tick/SlowTick/LateFrameTick hotDependencies=0`.
- Source scan for `TryLockBuffer/TryUnlockBuffer` in `AmbientBiotaDirector.cs`: no hits.
- Added diff scan excluding value-type job/signal/telemetry constructors: count `0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `70`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - ProceduralLadderClimb Solve Pin Lock Flattening

What was wrong:
- `ProceduralLadderClimbRuntime.TryPinSolveBuffers` still pinned five AnimationLocomotion DataVault writer lanes with `TryLockBuffer/TryUnlockBuffer` while `LadderClimbIkSolveJob` owned native views.
- The exact handle gates were already present, but the solve ownership route still held a multi-buffer legacy lock chain.

What was done:
- Added `SolveMutationGuardMask`, `_solveBufferGuardVault`, and `LadderMutationGuardBit`.
- Replaced the five legacy pins for `LadderClimbIkInput`, `LadderAUPs`, `LadderClimbIkOutput`, `LadderClimbIkTelemetryRing`, and `LadderClimbIkTelemetryCursor` with one mutation guard.
- Validated `TryResolveVaultViews().HasSolveCapacity` before and after guard acquire.
- Released the stored guard after schedule failure, `LateFrameTick` job completion, or barrier teardown.

Cinematic cheats used:
- Preserved the existing cheap camera-slide presentation and IK target solve.
- No physical body climb simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: five job writer pins became one guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Animation/Locomotion/ProceduralLadderClimbRuntime.cs`.
- Key anchors: `SolveMutationGuardMask` near line `46`, `_solveBufferGuardVault` near line `74`, `TryPinSolveBuffers` near line `750`, failure release near line `790`, `ReleaseSolveBufferPins` near lines `794` and `807`, guard release near line `810`.
- Targeted scan: `TryPinSolveBuffers directWriteLocks=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDependencies=0`; schedule/fast/late/barrier methods direct write/buffer lock/hot dependency hits `0`.
- Source scan for `TryLockBuffer/TryUnlockBuffer` in `ProceduralLadderClimbRuntime.cs`: no hits.
- Added diff scan excluding value-type `LadderClimbIkInput` and `LadderClimbIkSolveJob` constructors: count `0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `97`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - ProceduralBoneBlender Job Pin Lock Flattening

What was wrong:
- `ProceduralBoneBlenderRuntime.TryLockJobBuffers` still pinned eleven AnimationFauna DataVault writer lanes with `TryLockBuffer/TryUnlockBuffer`.
- The pins covered rigs, frame inputs, parent indices, bind poses, bone states, matrices, stats, telemetry ring/cursor, tuning, and mock AI signals while scheduled jobs owned native views.

What was done:
- Added `JobMutationGuardMask`, `_jobBufferGuardVault`, and `ProceduralBoneMutationGuardBit`.
- Replaced the legacy lock chain with one mutation guard and exact `TryResolveRuntimeBuffers` validation before and after guard acquire.
- Returned the post-guard native views directly to `Tick`, avoiding an extra resolve pass before scheduling jobs.
- Released the stored guard after `FinishPendingSolverCompletion` or forced teardown through `CompletePendingSolverForTeardown`.

Cinematic cheats used:
- Preserved the existing procedural sine/spring bone animation, mock AI velocity signal, and GPU skinning upload route.
- No physical bone simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: eleven job writer pins became one guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Animation/FaunaProcedural/ProceduralBoneBlenderRuntime.cs`.
- Key anchors: `JobMutationGuardMask` near line `45`, `_jobBufferGuardVault` near line `102`, `TryLockJobBuffers` near line `904`, failure release near line `981`, `UnlockJobBuffers` near line `985`, guard release near line `995`.
- Targeted scan: `TryLockJobBuffers directWriteLocks=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDependencies=0`; `UnlockJobBuffers directWriteLocks=0 bufferLocks=0 mutationReleases=1`; `Tick/LateFrameTick/CompletePendingSolverForTeardown/FinishPendingSolverCompletion` direct write/buffer lock/hot dependency hits `0`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/private TryLock/private Unlock` in `ProceduralBoneBlenderRuntime.cs`: no hits.
- Added diff scan for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent`: no source hits.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `73`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - HapticSynthesis Native Writer Route Hardening

What was wrong:
- `HectonInputRuntime_HapticSynth` schedule/fallback haptic synthesis needed one owner for pulses, final pulse, telemetry, profiles, tuning, and optional mock impulse lanes.
- Local haptic open/read/write/release paths still passed through generic input helpers that accepted any nonzero haptic handle instead of exact `BufferID` proof.

What was done:
- Added haptic base/mock mutation guard masks for pulses, final pulse, telemetry ring, profile table, tuning, and optional mock impulses.
- `TryPinHapticSynthesisScheduleBuffers` validates exact haptic views before and after guard acquire, then releases through post-simulation/fallback paths.
- Added exact haptic open/read/write/release helpers requiring `BufferID`, `SystemID.CoreDeterminism`, and nonzero generation.
- Replaced haptic lifecycle teardown release calls with exact haptic handle release.

Cinematic cheats used:
- Preserved synthesized haptic pulse coalescing from impact/tool/signal DTOs and mock impulse fakes.
- No physical controller simulation, DTO layout change, or binary device quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: six schedule writer pins became one mutation guard. Single-buffer writes remain exact and scoped. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Core/HectonInputRuntime_HapticSynth.cs`.
- Key anchors: `HapticSynthesisBaseScheduleGuardMask` line `33`, `TryReadHapticInputBuffer` line `544`, `OpenOrAcquireHapticSynthesisBufferForOwnerRoute` line `564`, `ReleaseHapticSynthesisVaultHandle` line `860`, `TryAcquireInputWriteBuffer` line `871`, `TryPinHapticSynthesisScheduleBuffers` line `1000`, `ReleaseHapticSynthesisSchedulePins` line `1033`, `IsHapticSynthesisHandle` line `1068`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/TryPinHapticSynthesisBuffer/GlobalRegistry.Get/GetComponent`: no hits.
- Weak haptic generic-route scan for `OpenOrAcquireInputBufferForOwnerRoute`, `TryReadInputBuffer(in _haptic...)`, `ReleaseVaultHandle(vault, ref _haptic...)`, and ownerless handle zero checks: no hits.
- Targeted scan: `TryPinHapticSynthesisScheduleBuffers directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `ReleaseHapticSynthesisSchedulePins mutationReleases=1`; schedule/run/consume hot methods have no direct write-lock groups, hot dependencies, `foreach`, string formatting, `.ToString`, or reference `new`; `TryAcquireInputWriteBuffer directWriteLocks=1 writeReleases=1`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `63`; compiler process sample returned no rows, but no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - FoveatedSimulation Importance Job Lock Flattening

What was wrong:
- `FoveatedSimulationManager` still held seven DataVault writer pins for importance scoring through `TryLockBuffer/TryUnlockBuffer`.
- Vault array open/resolve/release paths accepted nonzero handles instead of proving exact `BufferID`, `SystemID.SystemDispatcher`, and generation.

What was done:
- Added `ImportanceJobMutationGuardMask` for score positions, entity AUPs, importance scores, tick-rate codes, frustum flags, sim tiers, and distance lanes.
- Replaced the seven legacy buffer pins with one mutation guard and exact validation before/after acquire.
- Stored the guard vault in `_importanceJobGuardVault` and released once after job completion or teardown.
- Tightened `OpenOrAcquireVaultArray`, `TryResolveVaultArray`, `ReleaseVaultHandle`, score-position writes, and telemetry writes to exact foveated handle gates.

Cinematic cheats used:
- Preserved distance/frustum foveation, cadence throttling, and visual interpolation.
- No binary device tier branch, physical visibility simulation, or DTO layout change was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: seven job writer pins became one mutation guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs`.
- Key anchors: `ImportanceJobMutationGuardMask` line `222`, `_importanceJobGuardVault` line `299`, `TryWriteScorePositionsForImportanceJob` line `1413`, `TryPinImportanceJobBuffers` line `1444`, `TryValidateImportanceJobBuffers` line `1475`, `ReleaseImportanceJobBufferLocks` line `1486`, `OpenOrAcquireVaultArray` line `1499`, `TryResolveVaultArray` line `1524`, `IsFoveatedVaultHandle` line `1540`, `ReleaseVaultHandle` line `1629`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/TryLockImportanceJobBuffer/ownerless handle zero checks/weak vault resolve/release patterns`: no hits.
- Targeted scan: `TryPinImportanceJobBuffers directWriteLocks=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDependencies=0`; `ReleaseImportanceJobBufferLocks mutationReleases=1`; `TryWriteScorePositionsForImportanceJob directWriteLocks=1 writeReleases=1`; `WriteTelemetryFrame directWriteLocks=1 writeReleases=1`; schedule/apply/visual-sync/complete methods have no hot dependency or allocation-pattern hits.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `56` and compiler process sample listed active `dotnet` PID `28000`; no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - GroundPenetratingRadar State Lock Flattening

What was wrong:
- `GroundPenetratingRadarRuntime` still used legacy DataVault buffer pins for GPR state snapshot and ping copy.
- The publish route needed a single guarded route, not nested write-locks or sequential partial lane updates.
- Generic GPR helpers accepted nonzero handles before this pass; they now require exact GPR `BufferID` ownership.

What was done:
- Added `ScanJobMutationGuardMask`, `PingGpuReadGuardMask`, `_scanJobGuardVault`, and `GroundRadarMutationGuardBit`.
- Replaced `TryLockScanJobBuffers`, `TryLockWorldBuffer`, partial unlock counters, and `TryUnlockBuffer` calls with one mutation guard release path.
- `TryCopyCurrentGprStateToPending` and `TryPublishRadarPendingJob` resolve all state lanes from the same guarded `IDataVault`.
- `TryCopyGprPings` uses one method-scoped ping GPU guard and exact read-only handle resolution.
- `TryOpenVaultBufferForOwnerWrite`, `TryReadVaultBuffer`, and `AreGprHandlesCreated` now require exact `BufferID`, `SystemID.WorldStreaming`, and generation.

Cinematic cheats used:
- Preserved the cheap subsurface raymarch/GPR ping visualization and existing SDF/ore read models.
- Preserved continuous `GlobalQualityWeight` scaling for ray count and raymarch step count.
- No physical subsurface simulation, DTO layout change, or binary low-end branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: seven GPR state lanes and one ping-copy lane moved from legacy pins/nested writer paths to mutation guard routes. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs`.
- Key anchors: `ScanJobMutationGuardMask` line `35`, `PingGpuReadGuardMask` line `43`, `_scanJobGuardVault` line `118`, `TryCopyGprPings` line `317`, `TryPublishRadarPendingJob` line `729`, `TryPinScanJobBuffers` line `1138`, `ReleaseScanJobBufferPins` line `1168`, `TryPinPingGpuReadBuffer` line `1181`, `GroundRadarMutationGuardBit` line `1222`, `WriteTelemetry` line `1469`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/TryLockWorldBuffer/UnlockScanJobBuffers/TryLockScanJobBuffers/ReleaseScanJobBufferLocks/_scanJobBufferLockCount/IsVaultHandleCreated`: no hits.
- Targeted scan: `TryPublishRadarPendingJob directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `TryPinScanJobBuffers directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `ReleaseScanJobBufferPins mutationReleases=1`; `TryPinPingGpuReadBuffer mutationGuards=1 mutationReleases=1`; `WriteTelemetry directWriteLocks=1 writeReleases=1`; hot dependency/allocation pattern hits `0`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `68`; compiler process sample listed active `dotnet` PID `23456`; no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched.

## 2026-05-29 - HectonSeismicTideDirector Celestial/Seismic Guard Flattening

What was wrong:
- `HectonSeismicTideDirector` still held seven celestial mechanics lanes and six seismic evaluation lanes through legacy `TryLockBuffer/TryUnlockBuffer` chains.
- Job pointer opens used `_dataVault` after acquiring pins, so the guarded vault and resolved vault were not explicitly tied.

What was done:
- Added `CelestialMechanicsMutationGuardMask` for celestial state write/read, environment state, flow modifiers, tuning, mock timeline, and orbital parameters.
- Added `SeismicEvaluationMutationGuardMask` for seismic events, states, shake offsets, turbidity spikes, telemetry ring, and mock silt.
- Added stored guard vault fields so release targets the same `IDataVault` that granted the mutation guard.
- Tightened `TryOpenVaultBuffer`/`IsHandleCreated` to require exact `BufferID`, `SystemID.HabitatAtmosphere`, and generation.
- Replaced both legacy lock/unlock chains with one guard acquire, exact view validation before/after acquire, same-vault pointer opens, and one release after job completion or teardown.

Cinematic cheats used:
- Preserved deterministic harmonic tide/orbit solve, signal-driven camera/audio shake, turbidity scalar, and shader-global visual sync.
- No physical ocean/tide/seismic simulation, DTO layout change, or binary device branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: thirteen legacy buffer pins became two mutation guard reservations. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Environment/HectonSeismicTideDirector.cs`.
- Key anchors: `CelestialMechanicsMutationGuardMask` line `998`, `SeismicEvaluationMutationGuardMask` line `1006`, `_seismicEvaluationGuardVault` line `1035`, `_celestialMechanicsGuardVault` line `1036`, `SeismicMutationGuardBit` line `1283`, `TryPinCelestialMechanicsVaultBuffers` line `2868`, `ReleaseCelestialMechanicsVaultPins` line `2919`, `TryPinSeismicEvaluationVaultBuffers` line `3382`, `ReleaseSeismicEvaluationVaultPins` line `3432`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/TryLockCelestialMechanicsVaultBuffers/UnlockCelestialMechanicsVaultBuffers/TryLockSeismicEvaluationVaultBuffers/UnlockSeismicEvaluationVaultBuffers`: no hits.
- Targeted scan: `TryPinCelestialMechanicsVaultBuffers directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `ReleaseCelestialMechanicsVaultPins mutationReleases=1`; `TryPinSeismicEvaluationVaultBuffers directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `ReleaseSeismicEvaluationVaultPins mutationReleases=1`; commit/schedule/complete hot dependency hits `0`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `25`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because current mandate requested static validation/no build spam.

## 2026-05-29 - WorldProceduralFieldSampler Sampling Guard Flattening

What was wrong:
- `WorldProceduralFieldSampler.TryPinSamplingJobBuffers` held six DataVault pins through a partial `TryLockBuffer/TryUnlockBuffer` counter.
- The sampling resolve/read helpers accepted nonzero handles without proving exact `BufferID` and `SystemID.WorldProceduralFieldSampler`.

What was done:
- Added `SamplingJobMutationGuardMask` for zones, biome matrices, matrix index, biome families, cave entrance hints, and noise lookup.
- Added `_samplingJobGuardVault` so release targets the same `IDataVault` that granted the mutation guard.
- Added exact `IsWorldProceduralFieldHandle` checks and routed sampling resolve/read/release through exact owner gates.
- Replaced the partial unlock counter with one guard release in `ReleaseSamplingJobBufferPins`.

Cinematic cheats used:
- Preserved deterministic biome/noise/cave sampling and packed biome influence upload.
- No physical terrain simulation, MapMagic behavior rewrite, DTO layout change, or binary device branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: six legacy buffer pins became one mutation guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs`.
- Key anchors: `SamplingJobMutationGuardMask` line `53`, `_samplingJobGuardVault` line `644`, `TryResolveVaultBuffer` line `5154`, `TryReadVaultBuffer` line `5168`, `IsWorldProceduralFieldHandle` line `5177`, `WorldProceduralFieldMutationGuardBit` line `5184`, `TryPinSamplingJobBuffers` line `5231`, `ReleaseSamplingJobBufferPins` line `5326`, `ReleaseVaultHandle` line `5395`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/ReleaseSamplingJobBufferPins(int)/pinnedCount`: no hits.
- Targeted scan: `TryPinSamplingJobBuffers directWriteLocks=0 mutationGuards=1 mutationReleases=1`; `ReleaseSamplingJobBufferPins mutationReleases=1`; schedule/complete/resolve helper hot dependency hits `0`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `14`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because current mandate requested static validation/no build spam.

## 2026-05-29 - BaseAtmosphereLogistics Simulation Guard Flattening

What was wrong:
- `BaseAtmosphereLogisticsRuntime.ScheduleSimulation` held nineteen HabitatAtmosphere writer pins through `_lockedBufferMask`, `TryLockJobBuffers`, `TryLock`, and `UnlockJobBuffers`.
- The scheduled job ownership window depended on partial unlock order instead of one writer reservation.

What was done:
- Added `AtmosphereJobMutationGuardMask` for front/back cells, CSR lanes, counters, telemetry, gas delta lanes, remainders, shader payload, nodes, consumers, sources, vents, and tuning.
- Replaced `_lockedVault`/`_lockedBufferMask`/front-back lock IDs with `_jobGuardVault` and `_jobBuffersPinned`.
- `ScheduleSimulation` now pins with `TryPinJobBuffers`, resolves job views from the same guarded `IDataVault`, and releases through `ReleaseJobBufferPins` on failure or after post-simulation completion.
- `QueueOrApplyVaultRebind` and `ApplyPendingVaultRebindIfSafe` now use `_jobBuffersPinned` so DataVault hot-swap cannot invalidate a guarded job.

Cinematic cheats used:
- Preserved cheap CSR gas diffusion, quantized gas deltas, and shader scalar presentation.
- Preserved continuous `GlobalQualityWeight` diffusion-iteration scaling.
- No physical gas simulation, DTO layout change, visual-sync phase change, or binary low-end branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: nineteen scheduled-job writer pins became one mutation guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs`.
- Key anchors: `AtmosphereJobMutationGuardMask` line `33`, `_jobGuardVault` line `73`, `_jobBuffersPinned` line `86`, `ScheduleSimulation` line `598`, `VisualSyncTick` line `843`, `TryResolveSimulationBuffers` line `1204`, `TryPinJobBuffers` line `1306`, `ReleaseJobBufferPins` line `1359`, `AtmosphereLogisticsMutationGuardBit` line `549`.
- Source scan for `TryLockJobBuffers/UnlockJobBuffers/private bool TryLock/_lockedBufferMask/_lockedVault/_lockedFrontBufferId/_lockedBackBufferId/ResolveActiveCellBufferId`: no hits.
- Hot dependency scan for `GlobalRegistry.Get/GetComponent`: no hits.
- Targeted scan: `TryPinJobBuffers directWriteLocks=0 bufferLocks=0 mutationGuards=1 mutationReleases=1 hotDeps=0 foreach=0 linq=0`; `ReleaseJobBufferPins mutationReleases=1`; schedule/post/complete/visual-sync hot dependency hits `0`.
- Remaining `TryLockBuffer/TryUnlockBuffer` hits: `SetEditorTuning` single `Tuning` lane only, released in `finally`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `42`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because current mandate requested static validation/no build spam.

## 2026-05-29 - StressDrivenSpawnDirector Job/Reload Guard Flattening

What was wrong:
- `StressDrivenSpawnDirector.ColdTick` held twelve AIEcology writer lanes through partial `TryLockBuffer/TryUnlockBuffer` counters across scheduled spawn director work.
- Editor CSV reload held four direct buffer pins for rules, links, counters, and scratch.

What was done:
- Added `JobMutationGuardMask`, `ReloadMutationGuardMask`, `_jobGuardVault`, and `_jobBuffersPinned`.
- Job scheduling now validates all owned views before/after a single mutation guard acquire and resolves job views from that guarded vault.
- Editor reload now uses one reload guard and releases it in `finally`.

Cinematic cheats used:
- Preserved deterministic spawn tension, hidden culling, inventory preload tickets, and continuous `GlobalQualityWeight` scaling.
- No physical fauna simulation, CSV schema change, or binary device branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: twelve scheduled-job pins and four reload pins became two mutation guard routes. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs`.
- Key anchors: `JobMutationGuardMask` line `348`, `ReloadMutationGuardMask` line `362`, `_jobGuardVault` line `412`, `_jobBuffersPinned` line `418`, `StressDirectorMutationGuardBit` line `1064`, `TryResolveJobBuffers` line `1402`, `TryPinJobBuffers` line `1449`, `ReleaseJobBufferPins` line `1477`, `TryPinReloadBuffers` line `1494`.
- Source scan for `TryLockJobBuffers/UnlockJobBuffers/TryLockBuffer/TryUnlockBuffer/_lockedVault/_lockedCount/TryLockReloadBuffers/UnlockReloadBuffers`: no hits.
- Targeted scan: `TryPinJobBuffers mutationGuards=1 mutationReleases=1`; `ReleaseJobBufferPins mutationReleases=1`; `TryPinReloadBuffers mutationGuards=1 mutationReleases=1`; `TryReloadRulesCold/TryLoadRulesCsvCold mutationReleases=1`; `ColdTick/LateFrameTick/EnsureVaultState` direct write/buffer lock/hot dependency hits `0`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `45`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because current mandate requested static validation/no build spam.

## 2026-05-29 - HectonDirectorAI Predator Spatial Guard Flattening

What was wrong:
- `HectonDirectorAI` predator spatial refresh held `PredatorSpatialAbsolutePositions` and `PredatorSpatialCellCoords` simultaneously through two direct `TryLockBuffer` pins.
- Director vault view open accepted nonzero handles without proving exact `BufferID`, `SystemID.AICognition`, and generation.

What was done:
- Added `_predatorSpatialHashMutationGuardMask`, `_predatorSpatialHashGuardVault`, and `_predatorSpatialHashBuffersPinned`.
- `SchedulePredatorSpatialHashRefresh` now acquires one mutation guard, resolves both writable views from the same guarded `IDataVault`, writes the contact mirror, and releases in `finally`.
- `TryOpenDirectorVaultView` now exact-gates every predator spatial handle by `BufferID`, owner `SystemID`, and nonzero generation.

Cinematic cheats used:
- Preserved the cheap predator sight fake: coarse cell hash, frustum/rear-view cull, three terrain samples, and deterministic cadence.
- No physical perception simulation, AI behavior rewrite, or binary low-end branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: two writer pins became one mutation guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/HectonDirectorAI.cs`.
- Key anchors: `_predatorSpatialHashMutationGuardMask` line `594`, `_predatorSpatialHashGuardVault` line `640`, `_predatorSpatialHashBuffersPinned` line `646`, `SchedulePredatorSpatialHashRefresh` line `1310`, `TryOpenDirectorVaultView` line `1516`, guarded overload line `1526`, `TryPinPredatorSpatialHashVaultBuffers` line `1553`, `ReleasePredatorSpatialHashVaultPins` line `1597`, `IsDirectorVaultHandle` line `1608`, `PredatorSpatialHashMutationGuardBit` line `1618`.
- Source scan for `TryLockBuffer/TryUnlockBuffer/TryLockPredatorSpatialHashVaultBuffers/UnlockPredatorSpatialHashVaultBuffers/_predatorSpatialHashVaultLocked`: no hits.
- Raw hot dependency scan for `GlobalRegistry.Get<` and non-`TryGetComponent` `GetComponent()`: no hits.
- Targeted scan: `SchedulePredatorSightBatch legacyBufferLocks=0 hotDeps=0 gcPatterns=0`; `SchedulePredatorSpatialHashRefresh legacyBufferLocks=0 hotDeps=0 gcPatterns=0`; `TryPinPredatorSpatialHashVaultBuffers mutationAcquire=1 mutationRelease=1 legacyBufferLocks=0 hotDeps=0 gcPatterns=0`; `ReleasePredatorSpatialHashVaultPins mutationRelease=1`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `53`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because CPU exceeded the 50% guard.

## 2026-05-29 - ProximityColliderSystem Job Guard Flattening

What was wrong:
- `ProximityColliderSystem.TryAcquireJobBuffers` held runtime job views through mixed DataVault writer routes: `TryLockBuffer` for positions and previous status, plus `TryAcquireWriteLock` for results.
- That scheduled job route could hold multiple writer-protection mechanisms simultaneously.

What was done:
- Added `_jobMutationGuardMask` for `ProximityColliderPositions`, `ProximityColliderJobResults`, and `ProximityColliderPrevStatus`.
- Added `_jobBufferGuardVault` so release targets the same vault that granted the guard.
- Previous-status managed mirror is copied under one short exact write lock and released before job pinning.
- The scheduled distance job now resolves positions, previous status, and result views under one mutation guard; `ReleaseJobBufferLocks` releases one guard.

Cinematic cheats used:
- Preserved cheap distance-squared collider activation, existing activation/deactivation hysteresis, and object-pool collider presentation.
- No physical collider simulation, broadphase rewrite, or binary device branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: mixed two buffer pins plus one result write lock became one short copy write lock followed by one mutation guard reservation. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/ProximityColliderSystem.cs`.
- Key anchors: `_jobMutationGuardMask` line `92`, `_jobBufferGuardVault` line `102`, `TryAcquireJobBuffers` line `927`, mutation guard acquire line `969`, failure release line `999`, `ReleaseJobBufferLocks` line `1035`, `ProximityMutationGuardBit` line `1078`.
- Source scan for `TryLockBuffer/TryUnlockBuffer`: no hits in the file.
- Raw hot dependency scan for `GlobalRegistry.Get<` and non-`TryGetComponent` `GetComponent()`: no hits.
- Targeted scan: `ScheduleDistanceJob legacyBufferLocks=0 hotDeps=0 gcPatterns=0`; `TryAcquireJobBuffers mutationAcquire=1 mutationRelease=1 writeLocks=1 writeReleases=2 legacyBufferLocks=0 hotDeps=0 gcPatterns=0`; `ReleaseJobBufferLocks mutationRelease=1`; `Tick/LateFrameTick legacyBufferLocks=0 hotDeps=0 gcPatterns=0`.
- Added diff Zero-GC/dependency scan: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `100`; compiler process sample returned no rows. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger proof was launched because CPU exceeded the 50% guard.

## 2026-05-29 - Voxel/Interaction/LightShaft Writer Group Flattening

What was wrong:
- `HectonVoxelEngine` still had grouped DataVault pins around marching-cubes job tables and streaming scratch job-lifetime buffers.
- `EquipmentInteractionHandler` held scheduled/staged surface-query lanes through direct buffer locks.
- `ScreenSpaceLightShaftRuntime` held top/history/telemetry light-shaft buffers through a multi-lock route.

What was done:
- Added `JobTableMutationGuardMask` and dynamic streaming scratch mutation guard ownership in `HectonVoxelEngine`.
- Added `SurfaceQueryScheduledMutationGuardMask` and `SurfaceQueryScheduleMutationGuardMask` in `EquipmentInteractionHandler`.
- Added `FrameBufferMutationGuardMask` and exact frame-buffer ownership checks in `ScreenSpaceLightShaftRuntime`.

Cinematic cheats used:
- Kept marching-cubes tables, raycast scheduling, and screen-space light shafts unchanged.
- No physical light simulation, interaction rewrite, or binary quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: several multi-buffer pin groups became one mutation guard per route. No profiler sample was run.

Evidence:
- Patched source files: `Assets/_Project/Scripts/HectonVoxelEngine.cs`, `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs`, `Assets/_Project/Scripts/Lighting/Shafts/ScreenSpaceLightShaftRuntime.cs`.
- Key anchors: `HectonVoxelEngine.cs` `JobTableMutationGuardMask` line `47`, acquire line `607`, release line `627`, `StreamingScratchMutationGuardBit` line `10065`; `EquipmentInteractionHandler.cs` `SurfaceQueryScheduledMutationGuardMask` line `28`, acquire line `1002`, release line `1061`; `ScreenSpaceLightShaftRuntime.cs` `FrameBufferMutationGuardMask` line `68`, acquire line `352`, releases lines `385` and `396`.
- Targeted scans: Hecton voxel job table range `bufferLocks=0`, streaming scratch range `bufferLocks=0`; equipment surface-query ranges `bufferLocks=0`; light-shaft file has no `TryLockBuffer/TryUnlockBuffer` hits.
- `git diff --check`: exit `0`, LF/CRLF warnings only.

## 2026-05-29 - Exosuit Optional Voxel SDF Guard Flattening

What was wrong:
- `ExosuitKinematicsRuntime` held the solver job mutation guard and then acquired two extra DataVault pins for `VoxelSdfPayloadDescriptor` and `VoxelSdfTexture3D`.

What was done:
- Added `VoxelSdfPayloadMutationGuardMask`, `_jobBufferGuardMask`, and exact `IsVoxelSdfHandle`.
- `TryAcquireJobBufferViews` now tries job+SDF guard first, then falls back to job-only guard.
- `TryAcquireVoxelSdfPayload` resolves WorldStreaming SDF handles only when the current job guard includes the SDF bits.

Cinematic cheats used:
- Preserved the analytic/mock SDF fallback when the optional voxel SDF guard cannot be acquired.
- No exosuit kinematic solver rewrite or physical terrain simulation was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: two nested SDF pins became optional bits in the existing job guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Physics/Exosuit/ExosuitKinematicsRuntime.cs`.
- Key anchors: `GuardVoxelSdfPayloadDescriptor` line `91`, `VoxelSdfPayloadMutationGuardMask` line `114`, `_jobBufferGuardMask` line `205`, `IsVoxelSdfHandle` line `462`, `TryAcquireJobBufferViews` line `962`, optional guard line `997`, `TryAcquireVoxelSdfPayload` line `1040`, SDF guard check line `1068`, release line `1130`.
- Source scan for `TryLockBuffer|TryUnlockBuffer|_voxelSdf|UnlockVoxelSdfPayloadBuffers`: no hits.
- Targeted scans: `TryAcquireJobBufferViews bufferLocks=0 mutationAcquire=2 mutationRelease=1 hotDeps=0 gcPatterns=0`; `TryAcquireVoxelSdfPayload bufferLocks=0 mutationAcquire=0 mutationRelease=0 hotDeps=0 gcPatterns=0`; `UnlockJobBuffers mutationRelease=1`.
- Added diff scan for dependency/GC patterns in this file: `ADDED_PATTERN_HITS=0`.
- `git diff --check`: exit `0`, LF/CRLF warning only.

## 2026-05-29 - Buoyancy SIMD Telemetry Guard Flattening

What was wrong:
- `BuoyancyDisplacementRuntime.WriteCompletedSimdUtilizationTelemetry` locked SIMD telemetry ring and cursor separately with direct `TryLockBuffer` calls.
- The same route did not exact-gate the telemetry/tuning handles by expected `BufferID` and `SystemID.Physics`.

What was done:
- Added `SimdTelemetryMutationGuardMask`.
- Added `HasPhysicsHandle`.
- Replaced telemetry ring/cursor direct locks with one mutation guard and exact handle gates.

Cinematic cheats used:
- Kept hydrodynamic SIMD telemetry, throughput-drop detection, and continuous `GlobalQualityWeight` reporting unchanged.
- No buoyancy math or SIMD benchmark model was rewritten.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: two telemetry pins became one mutation guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`.
- Key anchors: `SimdTelemetryMutationGuardMask` line `60`, `HasPhysicsHandle` line `1240`, `WriteCompletedSimdUtilizationTelemetry` line `1512`, exact handle gates lines `1516-1517`, guard acquire line `1522`, release line `1585`.
- Targeted scan: `WriteCompletedSimdUtilizationTelemetry bufferLocks=0 mutationAcquire=1 mutationRelease=1 hotDeps=0 gcPatterns=0`.
- Scoped `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `91`; active compiler processes existed: `csc` PID `28592`, `dotnet` PID `44644`. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or fresh native ledger proof was launched.

## 2026-05-29 - Buoyancy Main Solver Job Guard Flattening

What was wrong:
- `BuoyancyDisplacementRuntime.FixedTick` protected the scheduled buoyancy solver with `_lockedBuffers`, direct `TryLockBuffer` pins, and direct `TryUnlockBuffer` release paths.
- The route covered thirteen Physics-owned lanes and therefore violated the one writer route rule for job-held DataVault ownership.
- The no-active-state branch could enter SIMD telemetry publication before releasing the job writer group.

What was done:
- Added `JobMutationGuardMask`, `_jobGuardVault`, and `_jobBuffersPinned`.
- Moved runtime job view resolution behind one Physics mutation guard.
- Added `ResolvePhysicsVaultBuffer` exact handle resolution and reused `HasPhysicsHandle`.
- Removed `_lockedBuffers`, legacy `TryLock`, direct job `TryLockBuffer`, and job `TryUnlockBuffer` routes.
- Released the job guard before `WriteCompletedSimdUtilizationTelemetry(0f)` in the no-active-state path.

Cinematic cheats used:
- Kept buoyancy force math, sleep prepass, SIMD hydrodynamic telemetry, material settling, and force packet compaction unchanged.
- No physical water simulation, binary device tier branch, or gameplay truth migration was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: thirteen scheduled-job writer pins became one mutation guard. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementRuntime.cs`.
- Key anchors: `JobMutationGuardMask` line `38`, `_jobGuardVault` line `147`, `_jobBuffersPinned` line `148`, no-active-state release line `499`, `ResolvePhysicsVaultBuffer` line `1225`, `HasPhysicsHandle` line `1240`, exact runtime resolves lines `1383-1395`, `TryLockJobBuffers` line `1603`, `UnlockJobBuffers` line `1627`.
- Source scan for `_lockedBuffers|LockStates|TryLock\(|TryLockBuffer|TryUnlockBuffer`: no remaining job-lock hits in the file.
- Targeted scans: `FixedTick bufferLocks=0 mutationAcquire=0 mutationRelease=0 hotDeps=0 gcPatterns=0`; `TryResolveRuntimeBuffers bufferLocks=0 mutationAcquire=0 mutationRelease=0 hotDeps=0 gcPatterns=0`; `TryLockJobBuffers bufferLocks=0 mutationAcquire=1 mutationRelease=0 hotDeps=0 gcPatterns=0`; `UnlockJobBuffers bufferLocks=0 mutationAcquire=0 mutationRelease=1 hotDeps=0 gcPatterns=0`; `WriteCompletedSimdUtilizationTelemetry bufferLocks=0 mutationAcquire=1 mutationRelease=1 hotDeps=0 gcPatterns=0`.
- Scoped `git diff --check`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `91`; active compiler processes existed: `csc` PID `28592`, `dotnet` PID `44644`. No `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or fresh all-scripts native ledger proof was launched.

## 2026-05-29 - VisualPressureAging Job And Defaults Guard Flattening

What was wrong:
- `VisualPressureAgingRuntime.ScheduleSimulation` used `_lockedBufferMask` and direct `TryLockBuffer/TryUnlockBuffer` chains for core visual-aging job lanes plus optional thermal/structural input lanes.
- `WriteDefaults` wrote params, degradation, tuning, mock temperature, and runtime defaults through separate direct buffer pins.

What was done:
- Added `JobMutationGuardMask`, `DefaultsMutationGuardMask`, `ThermalInputMutationGuardMask`, `StructuralInputMutationGuardMask`, and `StructuralTuningMutationGuardMask`.
- Replaced the job-held lock mask with `_jobGuardVault`, `_jobGuardMask`, and `_jobBuffersPinned`.
- `ScheduleSimulation` now acquires one job mutation guard with optional input bits when available, validates owned/external handles exactly, and releases once through `UnlockJobBuffers`.
- `WriteDefaults` now acquires one defaults mutation guard and releases it in `finally`.
- External input handles now require exact expected owners: `SystemID.HullIntegrity` for structural lanes and `SystemID.Thermodynamics` for the temperature mirror.

Cinematic cheats used:
- Kept mock-temperature fallback when thermodynamics input is unavailable.
- Kept structural/thermal scalar degradation and shader upload path unchanged.
- No physical material-aging simulation or binary device-quality branch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: the scheduled visual-aging job group and defaults writer group now use two mutation guard routes instead of multiple legacy buffer pins. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs`.
- Key anchors: `JobMutationGuardMask` line `158`, `DefaultsMutationGuardMask` line `169`, optional masks lines `176`, `179`, `183`, `_jobGuardVault` line `226`, `_jobGuardMask` line `227`, `_jobBuffersPinned` line `228`, `TryLockJobBuffers` line `1209`, `TryAcquireJobMutationGuard` line `1236`, `UnlockJobBuffers` line `1252`, `VisualAgingMutationGuardBit` line `1322`, `IsExpectedExternalOwner` line `1334`.
- Guard-bit proof for selected lanes: `ThermodynamicsTemperatureFrontMirror=1`; visual lanes `8,9,10,11,12,14,15,16,17`; structural lanes `24,25,31`. No collision inside the selected VisualPressureAging masks.
- Stale symbol scan for `_lockedBufferMask|TryLockStructuralInputs|TryLockOptional|UnlockOptional|TryLock\(`: no hits.
- Remaining `TryLockBuffer/TryUnlockBuffer` hits in this file are single-lane editor/snapshot/visual-sync routes, not the job/default multi-pin routes patched here.
- Added diff scan for `GlobalRegistry.Get<`, direct `GetComponent(`, `string.Format`, `.ToString(`, LINQ `.Select/.Where`, and `foreach`: no hits.
- `git diff --check -- VisualPressureAgingRuntime.cs`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU sample returned `41`; active compiler lane existed: `dotnet` PID `22304`, command `dotnet build .\MapMagic.MicroSplat.Editor.csproj -nologo --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`.
- Compile/import/profiler proof: not run for this patch.

## 2026-05-29 - StructuralIntegrity Solver And Base Warning Guard Flattening

What was wrong:
- `StructuralIntegrityCalculatorRuntime` solver/boot/mock routes and base-warning spike/default/load/clear routes still used grouped direct DataVault pins or stale lock helpers.
- SDF participation was folded into the same scheduled solver window but the old lock topology made ownership harder to reason about.
- A failure path was found during self-audit: if guard acquisition succeeded and the first marked buffer failed, `UnlockSolverBuffers(0)` returned before releasing `_solverMutationGuardMask`.

What was done:
- Added structural mutation guard masks from exact low active-lock bits for HullIntegrity-owned buffers and optional `VoxelSdfTexture3D`.
- Replaced direct structural/base-warning buffer pins with one guard per writer group and exact `BufferID`, `SystemID.HullIntegrity`, and generation validation.
- Renamed stale `TryLockSolverBuffers` to `TryPinSolverBuffers`.
- Removed obsolete base-warning aggregate lock mask and direct unlock/rollback helpers.
- Fixed `UnlockSolverBuffers(int mask)` so a held guard is released even when no mask bit was marked.

Cinematic cheats used:
- Kept structural solve math, warning grouping, CSV schemas, and SDF sampling unchanged.
- No physical deformation simulation and no binary low-end/high-end branch was added; existing `GlobalQualityWeight` route remains continuous.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: steady-frame delta `0 us`; value is risk removal, not measured frame-time win.

Evidence:
- Patched source files: `Assets/_Project/Scripts/Habitat/Deformation/Runtime/StructuralIntegrityCalculatorRuntime.cs`, `Assets/_Project/Scripts/Habitat/Deformation/Runtime/BaseStructuralWarningDispatcherTypes.cs`.
- Key anchors: `StructuralMutationGuardMask` line `39`, `StructuralSolverSdfMutationGuardMask` line `59`, `TryPinSolverBuffers` line `897`, `UnlockSolverBuffers(int)` line `966`, `TryMarkBaseStructuralWarningBuffers` line `1265`, `ResolveSimulationQualityWeight` line `1527`, `IsHullIntegrityVaultHandle` lines `1547` and `1555`.
- Stale scan for `TryLockBuffer|TryUnlockBuffer|TryLockSolverBuffers|TryLockBaseStructuralWarningBuffers|UnlockBaseStructuralWarningBuffers|RollbackBaseStructuralWarningLocks|SolverLockBaseWarningMask`: no hits.
- Hot dependency/GC scan for `GlobalRegistry.Get<`, direct `GetComponent(`, `string.Format`, `.ToString(`, LINQ `.Select/.Where`, and `foreach`: no hits.
- Added diff scan for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and direct `GetComponent`: no hits.
- `git diff --check` on the two source files: exit `0`, LF/CRLF warnings only.
- Build throttling: CPU before build was `16`; compiler process sample returned no rows. One build command was attempted, timed out after `604027 ms`, and the spawned `dotnet`/`VBCSCompiler` processes were stopped. Follow-up compiler process scan returned no rows; CPU then sampled `60`.
- Compile/import/profiler proof: incomplete. No successful build, Unity import, Play Mode, profiler, GCMonitor, or fresh native ledger proof exists for this patch.

## 2026-05-29 - VoxelDelta Compaction Scratch Guard Flattening

What was wrong:
- `VoxelDeltaProcessor.TryLockCompactionScratchBuffers` held nine TerrainSeams scratch buffer pins for compaction: source SDF, dirty mask, delta SDF, material, flags, output SDF, output materials, output flags, and uniform flag.
- Failure release depended on `_compactionScratchLockCount` and ordered partial unlocks.
- DataVault rebind saw `_compactionScratchLeased`, but not the underlying guard/lock acquisition state.

What was done:
- Added `CompactionScratchMutationGuardMask`.
- Replaced the nine-buffer direct pin chain with `TryPinCompactionScratchBuffers`.
- Stored the granting vault in `_compactionScratchGuardVault`.
- Added `_compactionScratchGuardHeld` to rebind busy checks.
- Strengthened `IsExactVaultHandle` to require `BufferID`, `SystemID.TerrainSeams`, and nonzero generation.
- Kept scheduled carve write buffer direct pinning unchanged because it is a single-buffer route and not the multi-lock defect fixed here.

Cinematic cheats used:
- Kept voxel carve, thermal melt, scheduled compaction, RLE, and save DTO layout unchanged.
- No physical voxel simulation or binary device switch was added.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: steady-frame delta `0 us`; nine scratch pins became one mutation guard, reducing deadlock/stall risk rather than measured frame time.

Evidence:
- Patched source file: `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`.
- Key anchors: `CompactionScratchMutationGuardMask` line `182`, `_compactionScratchGuardHeld` line `283`, `_compactionScratchGuardVault` line `284`, rebind busy gates lines `461` and `542`, exact handle gate line `1312`, `TryPinCompactionScratchBuffers` line `5599`, `UnlockCompactionScratchBuffers` line `5631`, `VoxelDeltaMutationGuardBit` line `5741`.
- Guard-bit proof from `H8Memory.BufferID`: `SaveVoxelDeltaCompactionSourceSdfScratch=70380` through `SaveVoxelDeltaCompactionUniformFlagScratch=70388`, mapping to active-lock bits `12..20`.
- Stale scan for `TryLockCompactionScratch|TryLockCompactionScratchBuffer|UnlockCompactionScratchBuffers(IDataVault|_compactionScratchLockCount|SaveVoxelDeltaCompaction.*TryLockBuffer|SaveVoxelDeltaCompaction.*TryUnlockBuffer`: no hits.
- Direct hot dependency scan for `GlobalRegistry.Get<` and non-`TryGetComponent` `GetComponent(`: no hits.
- Added diff scan for `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get`, and direct `GetComponent`: no hits.
- `git diff --check -- VoxelDeltaProcessor.cs`: exit `0`, LF/CRLF warning only.
- Build throttling: active compiler lane exists, `dotnet` PID `50204`, command `dotnet build .\Hecton8.Editor.csproj /m:1 /p:UseSharedCompilation=false --no-restore`; CPU sample `79`.
- Compile/import/profiler proof: not run for this patch.

## 2026-05-29 - SubmarineAutoLevel Ballast And Flood Input Guard Flattening

What was wrong:
- `SubmarineAutoLevelBallastController.TryAcquireBallastSolverJobBuffers` mixed four direct write locks with one pinned command buffer for one scheduled ballast solver job.
- `TryAcquireFloodRoomInputAliases` pinned room water levels, room volumes, and room local AUPs through three separate `TryLockBuffer` calls.
- The flood alias helper accepted generic nonzero handles instead of requiring the `VehiclesPhysics` owner that publishes these shared room lanes.

What was done:
- Added `BallastSolverMutationGuardMask` and `FloodRoomInputMutationGuardMask`.
- Added `_ballastSolverGuardVault` and `_floodRoomInputGuardVault`.
- Replaced ballast solver direct locks with one mutation guard and guarded exact view resolution for tanks, commands, fluid samples, force packets, and telemetry.
- Replaced flood room input per-buffer pins with one mutation guard and exact `SystemID.VehiclesPhysics` read-only view resolution.
- Removed `_ballastCommandsReadPinHeld`, `_floodRoomInputVaultLockMask`, `TryAcquirePinnedJobReadBuffer`, and `TryAcquirePinnedReadOnlyVaultBuffer`.

Cinematic cheats used:
- Kept PID, ballast force integration, dynamic flood mass solve, room SoA publication, and Rigidbody force dispatch unchanged.
- No physical flood/water simulation or binary device quality switch was introduced.

Exact microseconds saved:
- Measured: `0 us`.
- Static-only estimate: five ballast job pins and three flood room pins became two mutation guard routes. No profiler sample was run.

Evidence:
- Patched source file: `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`.
- Key anchors: `BallastSolverMutationGuardMask` lines `408-413`, `FloodRoomInputMutationGuardMask` lines `414-417`, `_ballastSolverGuardVault` line `513`, `_floodRoomInputGuardVault` line `514`, `TryAcquireBallastSolverJobBuffers` line `3320`, `TryResolveBallastSolverGuardedBuffer` line `3415`, `ReleaseBallastSolverVaultLocks` line `3446`, `ReleaseFloodRoomInputVaultLocks` line `3476`, `TryAcquireFloodRoomInputAliases` line `3571`, `TryResolveFloodRoomInputReadOnly` line `3660`, `BallastMutationGuardBit` line `3929`.
- Source scan for `TryLockBuffer|TryUnlockBuffer|TryAcquirePinnedReadOnlyVaultBuffer|TryAcquirePinnedJobReadBuffer|_ballastCommandsReadPinHeld|_floodRoomInputVaultLockMask`: no hits.
- Targeted scans: `TryAcquireBallastSolverJobBuffers directWriteLocks=0 legacyBufferLocks=0 mutationAcquire=1 mutationRelease=1`; `TryAcquireFloodRoomInputAliases directWriteLocks=0 legacyBufferLocks=0 mutationAcquire=1 mutationRelease=1`; `ReleaseFloodRoomInputVaultLocks mutationRelease=1`; hot dependency and GC-pattern hits `0`.
- Guard-bit proof: ballast buffer IDs `71771-71775` map to bits `27,28,29,30,31`; flood input IDs map to bits `12,15,16`; total unique bits `8/8`.
- Added diff scan for `GlobalRegistry.Get<`, direct `GetComponent(`, `string.Format`, `.ToString(`, LINQ `.Select/.Where`, and `foreach`: `ADDED_PATTERN_HITS=0`.
- `git diff --check -- SubmarineAutoLevelBallastController.cs`: exit `0`, LF/CRLF warning only.
- CPU/build guard: CPU samples returned `62` then `84`; compiler process samples returned no rows, but CPU guard is above `50%`.
- Compile/import/profiler proof: not run for this patch.
## 2026-05-29 - Shinobu Material Response Job Guard Flattening

What was wrong: `Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs` had a scheduled graphics-material job route protected by `_lockedBufferMask` and eight `TryLockBuffer/TryUnlockBuffer` pins. Generic handle validation did not prove `SystemID.GraphicsMaterials`.

What was done: Added `JobMutationGuardMask`, `_jobBufferGuardVault`, and `_jobBufferGuardHeld`; replaced the eight writer pins with one mutation guard; strengthened `IsMatchingVaultHandle` to require exact `BufferID`, `SystemID.GraphicsMaterials`, and nonzero generation; moved simulation view resolution under the guard and re-read `GlobalQualityWeight` from the scalar lane inside the guarded section; release now happens after post-simulation/lifecycle completion or unscheduled failure through `finally`.

Cinematic cheats used: none added. Existing biomass scalar fake, wear-rate scalar math, visible-payload packing, and shader-buffer upload remain intact. No physical material simulation was introduced.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Removed eight DataVault buffer-pin operations and partial unlock topology from one scheduled job route. Static proof: stale lock symbol scan returns no hits; hot dependency/GC scan returns no hits; `TryAcquireWriteLock` scan returns no hits; `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run because CPU sampled 68 and the build guard is 50.

## 2026-05-29 - Abyssal Reactor Shared Buffer Guard Flattening

What was wrong: `Assets/_Project/Scripts/Thermodynamics/AbyssalThermodynamicsSolver.ReactorBridge.cs` borrowed mutable Power, Fluid, and Airlock buffers for a scheduled reactor job through three separate `TryLockBuffer` calls using the wrong owner identity (`SystemID.Thermodynamics`).

What was done: Replaced `_reactorSharedBufferLockMask` and `TryLockAndResolveOptionalBuffer` with one dynamic mutation guard. The bridge now validates exact owner identities (`SystemID.Power`, `SystemID.Fluid`, `SystemID.HabitatAtmosphere`), resolves optional pointers under the granted guard, and releases once after `LateFrameTick` job completion, lifecycle completion, or failed schedule via `finally`.

Cinematic cheats used: none added. Existing deterministic reactor heat, coolant boil-off scalar, power-node atomics, and meltdown signal fakes remain intact.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Removed up to three foreign DataVault writer pins from one scheduled job route. Static proof: stale lock symbol scan returns no hits; added diff GC/dependency scan returns no hits; `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run because CPU sampled 100 and active `csc`/`dotnet` processes existed.

## 2026-05-29 - Abyssal Reactor Empty Shared Guard Refinement

What was wrong: the dynamic reactor shared guard could remain held after optional foreign handles were discovered if all guarded buffers then resolved to null or empty pointers.

What was done: `ResolveOptionalReactorIntegrationPointers` now releases the shared guard immediately when no power, fluid, or airlock pointer survives guarded resolution. Partial-success jobs keep the guard until the scheduled fence.

Cinematic cheats used: none added. Preserved optional cheap scalar integration; no physical reactor/coolant rewrite.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is shorter unused cross-domain guard lifetime. Proof: reactor stale-lock scan and hot dependency/GC scan return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only.

## 2026-05-29 - FoundationPylonGpuBatch Scheduled Guard Flattening

What was wrong: `Assets/_Project/Scripts/Construction/FoundationPylonGpuBatch.cs` held one scheduled construction VFX route through many direct `TryLockBuffer/TryUnlockBuffer` pins stored in `_pendingVaultLocks`.

What was done: replaced the fixed lock array with `_pendingVaultGuardVault/_pendingVaultGuardMask`; `TryBeginVaultJobGuard` acquires one mutation guard for foundation core lanes plus optional socket/SDF lanes; existing failure, finalize, discard, and teardown cleanup now release one guard route.

Cinematic cheats used: none added. Kept mock SDF fallback, scalar tuning, GPU pylon upload, culling, and warning signal behavior unchanged.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a long multi-pin DataVault writer topology from a LateFrame presentation route. Proof: stale lock scan no hits, hot dependency/GC scan no hits, added diff scan no hits, scoped `git diff --check` clean except LF/CRLF warning.

## 2026-05-29 - Shinobu38 QA Watchdog Runtime Guard Flattening

What was wrong: `Assets/_Project/Scripts/QA/Headless/Shinobu38QaWatchdogRuntime.cs` held sixteen editor/headless DataVault buffers through direct buffer pins and accepted watchdog handles without checking `SystemID.External`.

What was done: added `RuntimeBufferMutationGuardMask`, `_runtimeBufferGuardVault`, and `_runtimeBufferGuardMask`; replaced the lock/unlock loop with one mutation guard; added exact owner validation to `IsWatchdogVaultHandle`.

Cinematic cheats used: none added. Kept QA bot navigation, telemetry, fast-forward, CSV/file writer, and dump schemas unchanged.

Exact microseconds saved: 0 us measured; expected player-frame delta 0 us because the runtime is `UNITY_EDITOR` gated. Static value is removal of a long editor/headless multi-pin DataVault topology. Proof: stale lock scan no hits, mutation scan one acquire/one release, added diff scan no hits, scoped `git diff --check` clean except LF/CRLF warning.

## 2026-05-29 - HullDentShaderController VFX Guard Flattening

What was wrong: `Assets/_Project/Scripts/Vehicles/VFX/HullDentShaderController.cs` still used legacy `TryLockBuffer/TryUnlockBuffer` around `BufferID.HullDents` in three LateFrame/presentation-adjacent VFX routes.

What was done: added `HullDentsMutationGuardMask` and `HullDentMutationGuardBit`; replaced the three legacy buffer pins with `TryAcquireMutationGuard`/`ReleaseMutationGuard` guarded by existing exact `BufferID.HullDents`, `SystemID.Vfx`, and generation validation.

Cinematic cheats used: preserved shader-only hull dents, fixed 16-entry global vector array, local-space impact packing, repair fade, and quality-weight scar proxy. No physical hull deformation or binary device tier was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of legacy DataVault pins from one VFX presenter. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan has three acquire/release pairs; only `GetComponent` hit is cold `ResolveBreachReadModel`; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - RadiationHazardGrid SDF Snapshot Guard Flattening

What was wrong: `Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs` held a scheduled radiation SDF snapshot through legacy `TryLockBuffer/TryUnlockBuffer`, and the readiness check did not verify `SystemID.GameplayRadiation`.

What was done: added `RadiationSdfSnapshotMutationGuardMask`, `IsRadiationSdfSnapshotHandle`, and `RadiationMutationGuardBit`; replaced the snapshot legacy pin with `TryAcquireMutationGuard` and `ReleaseMutationGuard`; exact handle validation now requires `BufferID`, owner system, and generation.

Cinematic cheats used: preserved the cheap SDF snapshot shielding fake, bulkhead sample cap, inverse-square radiation source sampling, and continuous quality-driven sample counts. No physical radiation transport simulation or binary quality switch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of one job-held legacy DataVault pin with stronger owner validation. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan has one acquire and two release sites; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - HectonShaderGlobalDataVaultBridge Shader-Global Guard Flattening

What was wrong: `Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs` still wrote `BufferID.ShaderGlobalState` through legacy `TryLockBuffer/TryUnlockBuffer`, while the owning `GlobalShaderDispatcher` already uses a mutation guard for the same shader-global lane.

What was done: added `ShaderGlobalStateMutationGuardMask`; replaced the legacy lock/unlock in `WriteReadSlot` with `TryAcquireMutationGuard` and `ReleaseMutationGuard` in `finally`; strengthened `IsSlotsHandleOwned` to require exact `BufferID.ShaderGlobalState`, `SystemID.GraphicsScalability`, and generation before resolving the mutable slot array.

Cinematic cheats used: preserved the existing shader/global-vector fake path for AUP, water extinction, physiology discomfort, power brownout, respawn cover, suit crush, and radiation mutation. No physical simulation, new renderer pass, or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of one legacy DataVault pin from a shader presentation bridge and alignment with `GlobalShaderDispatcher` guard topology. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan has one acquire/release site; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - WfcOutpostGridRegistry Slot Lease Guard Flattening

What was wrong: `Assets/_Project/Scripts/Power/WfcOutpostGridRegistry.cs` still protected fixed WFC outpost grid slots with direct `TryLockBuffer/TryUnlockBuffer` calls in registration, lease acquisition, and release/clear paths. A lease can outlive the immediate call while `WfcOutpostGraphTranslationJob` consumes it, so the old topology kept legacy per-buffer pins in a cross-domain native handoff.

What was done: replaced slot pins with mutation guard masks derived from `GridSlotBase + slot`; strengthened slot resolution to use the same granting `IDataVault`; added exact `BufferID`, `SystemID` `512`, and generation validation; preserved the existing `WfcOutpostGridLease` public API and release call shape.

Cinematic cheats used: none added. The route remains a fixed native WFC grid handoff to logistics/power boot, not a new physical simulation or quality-tier split.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of legacy DataVault buffer pins from three WFC slot lease routes. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan reports guarded acquire/release sites; hot dependency/GC scan returns no hits for `GlobalRegistry.Get<`, direct `GetComponent(`, formatting, LINQ, or `foreach`; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - HabitatConstructionManager Validation Guard Flattening

What was wrong: `Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs` held construction integrity validation buffers through ten direct `TryLockBuffer/TryUnlockBuffer` pins and `_lockedValidationBufferMask` while a scheduled validation job was pending.

What was done: replaced the per-buffer lock mask with one `ValidationMutationGuardMask`, stored the granting vault, added post-acquire graph-buffer validation with `finally` release on failure, and routed reset, failed schedule, result consume, vault release, and teardown through `ReleaseValidationBufferGuard`.

Cinematic cheats used: none added. The existing deterministic structural score and socket-compatibility model stays intact; no physical construction simulation or binary quality switch was introduced.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a ten-pin DataVault writer topology from construction validation. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` or stale validation lock helper names; mutation scan shows one acquire route and one release route; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - RepairTool Hull Dent And Black Box Guard Flattening

What was wrong: `Assets/_Project/Scripts/RepairTool.cs` still used direct `TryLockBuffer/TryUnlockBuffer` for `HullDents` repair, repair black-box recording, and repair black-box dumping. The hull-dent lock used `SystemID.GameplayTools` even though the borrowed lane is created by the VFX hull-dent controller under `SystemID.Vfx`.

What was done: replaced the three direct lock pairs with mutation guards; added exact handle validators for `HullDents` (`SystemID.Vfx`) and `RepairToolBlackBox` (`SystemID.GameplayTools`); kept repair math, signal publication, black-box entry layout, frame count, and dump path unchanged.

Cinematic cheats used: preserved the cheap 16-slot shader hull-dent state and 300-frame black-box ring. No physical hull deformation, new simulation, or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of three legacy DataVault pins and one wrong-owner hull-dent lock route. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan shows three acquire/release routes; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - HectonBiolumManager Telemetry Ring Guard Flattening

What was wrong: `Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs` still guarded `BiolumLegacyTelemetryRing` through legacy `TryLockBuffer/TryUnlockBuffer` in telemetry record and dump routes.

What was done: added `TelemetryRingMutationGuardMask`, replaced the old telemetry lock helper with `TryAcquireTelemetryRingGuard`, and releases through `ReleaseTelemetryRingGuard` in `finally` on validation failure and at both call sites.

Cinematic cheats used: preserved the fixed-size telemetry ring plus shader/ripple biolum fake. No physical light simulation, ripple rewrite, predator blackout math change, or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of one legacy DataVault pin from the VFX telemetry route. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer`, `TryLockTelemetryRing`, or `UnlockTelemetryRing` in the file; mutation scan reports one guarded acquire path and one release helper; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - FloraGenomeVaultRuntime Writer Guard Flattening

What was wrong: `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeVaultRuntime.cs` still used legacy `TryLockBuffer/TryUnlockBuffer` for raw genome binary loading, and decode/generation writer paths resolved mutable vault arrays without a writer guard.

What was done: added raw, decode, and generation mutation guard masks; raw async load now releases through `ReleaseRawBytesGuard`; decode executes under `DecodeMutationGuardMask`; scheduled plant generation holds `GenerationJobMutationGuardMask` until `TryFinalizePlantGeneration` completes the job fence.

Cinematic cheats used: preserved L-system expansion, fixed native buffer capacities, branch matrix/hazard output, and black-box ring. No physical plant simulation, binary quality switch, or DTO layout change was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of one legacy raw-byte pin and addition of explicit writer ownership around decode/job lanes. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan reports raw, decode, and generation guard routes; hot dependency scan returns no registry/component lookup hits; added `new` tokens are value-type job/ticket structs only; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof blocked by CPU sample 63.

## 2026-05-29 - ShinobuSpatialGridSolver Telemetry Guard Flattening

What was wrong: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs` locked telemetry cursor and telemetry ring through two separate legacy `TryLockBuffer/TryUnlockBuffer` pairs in `SpatialHashQuery.RecordQueryFailure`.

What was done: added `TelemetryMutationGuardMask`; cursor and ring now resolve under one mutation guard, both writes complete under the same guard, and release happens in `finally`.

Cinematic cheats used: none added. Spatial hashing, probe budget, telemetry DTOs, and deterministic AI query behavior remain unchanged.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a two-lock telemetry path from AI query failure recording. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan reports one acquire and one release route; added diff Zero-GC/dependency scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - AbyssalCavitationRuntime Simulation Guard Flattening

What was wrong: `Assets/_Project/Scripts/Physics/Cavitation/AbyssalCavitationRuntime.cs` held ten scheduled simulation lanes through `_lockedBuffers` and legacy `TryLockBuffer/TryUnlockBuffer` while the cavitation job chain was pending.

What was done: replaced lock-bit tracking with `SimulationMutationGuardMask`, `_simulationGuardVault`, and `_simulationGuardHeld`; `ScheduleSimulation` acquires one guard; schedule failure and `FinishScheduledCompletion` release through `ReleaseSimulationGuard`.

Cinematic cheats used: preserved mock SDF sampling, visual sphere shader upload, acoustic/wake signals, and continuous cavitation quality tuning. No physical fluid simulation or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a ten-pin scheduled DataVault writer topology. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; mutation scan reports simulation acquire/release plus existing targeted guards; hot dependency and added diff GC scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - AnalyticalGerstnerWaveRuntime Wave Job Guard Flattening

What was wrong: `Assets/_Project/Scripts/Physics/Buoyancy/AnalyticalGerstnerWaveRuntime.cs` held six scheduled wave-job lanes through legacy `TryLockBuffer/TryUnlockBuffer` while the analytical Gerstner job was pending.

What was done: replaced spectrum/tuning/request/result/macro-grid/counter lock bits with one `JobMutationGuardMask`, stored the granting vault, and release before telemetry guard acquisition, on unscheduled failure, and during teardown.

Cinematic cheats used: preserved analytical Gerstner waves and macro swell grid approximation. No physical fluid simulation, wave rewrite, or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a six-pin scheduled DataVault writer topology. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer` in the file; stale lock helper/name scan returns no hits; hot dependency and added diff GC scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - JacobianFoamGpuRuntime Read Pin Guard Flattening

What was wrong: `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs` still used legacy `TryLockBuffer/TryUnlockBuffer` in `TryAcquireReadPin` for tuning, wake upload, and deferred telemetry dump reads.

What was done: replaced generic read pins with one mutation guard bit derived from `BufferID`; retained exact `BufferID`, `SystemID.Vfx`, generation, creation, and length validation; failure and caller `finally` paths release the guard.

Cinematic cheats used: preserved foam history texture, wake-impact fake, compute dispatch cadence, and continuous resolution/wake-count scaling. No physical foam simulation or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of all legacy DataVault locks from the runtime foam presenter. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer`; hot dependency and added diff GC scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run.

## 2026-05-29 - QuestDagResolverRuntime Scheduled Guard Flattening

What was wrong: `Assets/_Project/Scripts/Quest/QuestDagResolverRuntime.cs` held 16 QuestDag buffers through legacy per-buffer pins while the scheduled fixed-point quest resolver job was pending.

What was done: replaced scheduled pin constants and `_scheduledBufferPinMask` with one `ScheduledMutationGuardMask`; release now runs on schedule failure, completion, dispose, and teardown, while existing exact handle validation remains in `QuestDagVault.TryResolveBuffers`.

Cinematic cheats used: none added. Preserved fixed-point quest graph, spatial hash, SignalBus emission, and continuous cadence dilation.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of a 16-pin scheduled DataVault topology from quest resolution. Proof: stale lock/pin scan, hot dependency scan, added diff GC scan, and scoped `git diff --check` are clean except LF/CRLF warning. No compile/import/profiler run.

## 2026-05-29 - BaseAtmosphereLogisticsRuntime Pending Tuning Guard

What was wrong: `Assets/_Project/Scripts/Atmosphere/BaseAtmosphereLogisticsRuntime.cs` still used legacy `TryLockBuffer/TryUnlockBuffer` for pending atmosphere tuning writes.

What was done: replaced the tuning lock with `TryAcquireMutationGuard` using `AtmosphereLogisticsMutationGuardBit(Tuning)` and released in `finally`; existing owner/handle validation remains before DTO write.

Cinematic cheats used: preserved bounded diffusion, shader payload, and quality-driven iteration policy. No physical gas simulation rewrite or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of the last legacy DataVault lock from this atmosphere logistics file. Proof: source scan has no `TryLockBuffer/TryUnlockBuffer`; hot dependency and added diff GC scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler run.

## 2026-05-29 - HomeostasisBrain Mock Terrain Sampler Guard

What was wrong: `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs` held `ShinobuScalabilityMockScatterDensity` through legacy `TryLockBuffer/TryUnlockBuffer` while the scheduled mock terrain sampler job was pending.

What was done: added `MockTerrainSamplerMutationGuardMask`, replaced the lock flag with `_mockTerrainSamplerGuardHeld`, and released the guard on schedule failure, job completion, shutdown, and vault release paths.

Cinematic cheats used: preserved the mock terrain sampler probability fake and continuous `GlobalQualityWeight` math. No binary quality branch, PID rewrite, or render policy change was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static value is removal of one scheduled legacy DataVault lock from the global scalability owner. Proof: stale lock/helper scan, hot dependency scan, added diff GC scan, and scoped `git diff --check` are clean except LF/CRLF warning. No compile/import/profiler run.

## 2026-05-29 - Current Pass Static Recheck

What was wrong: no additional code defect in this entry; this is the post-pass proof gate.

What was done: scoped scan across current-pass and prior AUDIT-native touched runtime files found no `.TryLockBuffer/.TryUnlockBuffer`; current-pass hot dependency scan found no `GlobalRegistry.Get<` or direct `GetComponent(`; added diff GC/dependency scan found no new allocation/format/LINQ/foreach/hot lookup patterns; scoped `git diff --check` returned only LF/CRLF warnings.

Cinematic cheats used: unchanged from individual patches.

Exact microseconds saved: 0 us measured. Build/import/profiler proof was not run because CPU sampled `100%`; launching `dotnet build` would violate Compilation Resource Throttling.

## 2026-05-29 - Migratory Sargassum Job Guard Flattening

What was wrong: `Assets/_Project/Scripts/WorldProceduralScatterDirectorMigratorySargassum.cs` mixed flow-sample direct write lock, flow-sample legacy buffer pin, and island write lock across the scheduled sargassum job.

What was done: replaced that route with one `MigratorySargassumJobMutationGuardMask`, exact handle resolution after acquire, and release on schedule failure, completion, dispose, or vault release. Removed the local legacy lock/write-lock wrappers from the migratory vault array facade.

Cinematic cheats used: preserved source-driven island drift and cheap abyssal flow sampling. No physical algae/fluid simulation or binary quality branch was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static proof: no legacy lock/direct write-lock symbols, hot registry/direct component lookups, or managed allocation patterns remain in the scoped file diff except value-type `new float3(...)`; `git diff --check` exits 0 with LF/CRLF warning only.

## 2026-05-29 - Shinobu Storm Propagation Guard Flattening

What was wrong: `Assets/_Project/Scripts/Atmosphere/StormPropagation/ShinobuStormPropagationRuntime.cs` still used legacy DataVault pins for tuning, profile, state, scalar, telemetry, and snapshot lanes.

What was done: added storm propagation mutation guard helpers and replaced all runtime `TryLockBuffer/TryUnlockBuffer` calls with guard acquire/release while preserving existing exact handle and length validation.

Cinematic cheats used: preserved bounded storm attenuation, mock hurricane fallback, scalar visual outputs, telemetry dump shape, and continuous `GlobalQualityWeight` cadence. No physical weather rewrite was added.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static proof: no `TryLockBuffer/TryUnlockBuffer`, direct `TryAcquireWriteLock/ReleaseWriteLock`, `GlobalRegistry.Get<`, direct `GetComponent(`, or added GC/LINQ/string-format patterns remain in the runtime file; `git diff --check` exits 0 with LF/CRLF warning only.

## 2026-05-29 - SystemDispatcher Surface Probe Guard Flattening

What was wrong: `Assets/_Project/Scripts/Core/SystemDispatcher.cs` still pinned `BufferID.DispatcherRaycastHits` through legacy DataVault lock plumbing in the scheduled surface-probe hit lane.

What was done: replaced the lock flag with a stored guard vault and guard-held flag, using `DispatcherSurfaceProbeHitsGuardMask`; existing completion and teardown release paths now release the mutation guard.

Cinematic cheats used: none added. Dispatcher phase behavior, VISUAL_SYNC cadence, and surface-probe contract were preserved.

Exact microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static proof: no legacy lock symbols remain in `SystemDispatcher.cs`; scoped hot lookup and added diff GC scans are clean; `git diff --check` exits 0 with LF/CRLF warning only. Build/import/profiler proof not run because CPU sampled `100%`.
2026-05-29 AUDIT_NATIVE_STATE continuation:
What was wrong: remaining source-local DataVault lock violations were found in `GlobalTelemetryBus.Blackbox.cs`, `MemorySentinelRuntime.cs`, `SeaglideHydrodynamicsRuntime.cs`, and `HarpoonTensionSolver328.cs`. The defects were legacy per-buffer pins around crash blackbox lanes, dynamic memory sentinel validation targets, seaglide scheduled hydrodynamics, and harpoon mock scheduled tension buffers.
What was done: replaced those per-buffer lock chains with mutation guard masks. Blackbox uses one fixed crash-lane guard; MemorySentinel builds one dynamic guard mask and rejects repeat acquisition while held; Seaglide uses one scheduled job guard; Harpoon uses one mock schedule guard and keeps the public release API used by `TetherManager`.
Cinematic Cheats used: no physical simulation was added. Existing seaglide audio/cavitation and harpoon mock schedule approximations remain cheap deterministic fakes; the work removed lock topology hazards only.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower deadlock/stall risk from replacing 11+12 scheduled pins and two diagnostic multi-pin routes with single guard acquisitions.
Verification: scoped scan over the four files returns no `.TryLockBuffer`, `.TryUnlockBuffer`, direct write-lock APIs, stale lock masks/fields, or removed helper names. Hot dependency scan returns no `GlobalRegistry.Get<` or direct `GetComponent(`. Added diff scan returns no `new`, `string.Format`, `.ToString`, LINQ, `foreach`, hot registry, or direct component hits. `git diff --check` exits 0 with LF/CRLF warnings only. Compile/import/profiler proof not run: CPU sample 85, no compiler process rows, CPU guard above 50.
2026-05-29 AUDIT_NATIVE_STATE continuation 2:
What was wrong: more source-local legacy guard routes remained in `CablePhysicsSolver132.cs`, `HectonCelestialEngine.cs`, `AupPrecisionJobs.cs`, `ShinobuStormPropagationDebugGizmo.cs`, and `PhysicsApplySystem.cs`. The defects were job/editor paths that still used old DataVault buffer pins or stale helper naming.
What was done: Cable mock schedule uses one mutation guard; Celestial orbit output job handoff uses one stored-vault guard; AUP scheduled localization lease uses one guard after the runtime-state write lock has already been released; Storm gizmo read uses one guard; PhysicsApply validation schedule uses one stored-vault guard.
Cinematic Cheats used: none added. Existing cable approximation, celestial orbit math, AUP localization, storm gizmo visualization, and physics validation contracts were preserved.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is removal of scheduled/editor pin topology and lower stall/deadlock surface.
Verification: scoped stale-symbol scans return no `.TryLockBuffer`, `.TryUnlockBuffer`, old pin fields, old lock helper names, or old pin constants in touched files. Added diff scan returns no `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get<`, or direct `GetComponent(` hits. `git diff --check` exits 0 with LF/CRLF warnings only. Whole scripts inventory no longer lists these files; top remaining offenders are `DestructibleOrganicManager` 48 and `ShinobuEcosystemBalancer` 44. Compile/import/profiler proof not run: CPU sample 88, no compiler processes, CPU guard above 50.
Build attempt: later throttle pre-check became legal (`CPU=19`, no compiler process rows). Ran exactly one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`. Result: timeout after `604008 ms`, no compiler diagnostics captured, no successful compile proof. Follow-up compiler process check cleared after the timed-out process exited. No second build launched.
External compiler lane after timeout: final process check observed `dotnet` PID `28040` running `dotnet build .\Assembly-CSharp.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore`. No additional build launched; compile proof remains unavailable.
Compiler lane cleared: later process check returned no `dotnet`, `csc`, or `VBCSCompiler` rows. No second build launched after the timeout.
External editor compiler lane observed: subsequent process check saw `dotnet` PID `43348` running `dotnet build .\Assembly-CSharp-Editor.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore`. No additional build launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 3:
What was wrong: `Assets/_Project/Scripts/SpatialAudioManager.cs` still held the acoustic occlusion SDF snapshot with legacy `TryLockBuffer/TryUnlockBuffer`, and release used the current `_dataVault` instead of the granting vault.
What was done: added `AcousticOcclusionSdfSnapshotMutationGuardMask`, stored `_acousticOcclusionSdfSnapshotGuardVault`, and released the same mutation guard from local failure, schedule failure, job completion, and telemetry/cache clear paths.
Cinematic Cheats used: preserved the existing copied SDF snapshot and cinematic low-pass occlusion fake. No physical sound propagation rewrite or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-unlock/hot-swap risk in the scheduled audio occlusion lane.
Verification: `SpatialAudioManager.cs` scan reports no `TryLockBuffer/TryUnlockBuffer`; added diff GC/hot dependency scan returns no `new`, `string.Format`, `.ToString`, LINQ, `foreach`, `GlobalRegistry.Get<`, or direct `GetComponent(` hits. `git diff --check` exits 0 with LF/CRLF warning only. Compile/import/profiler proof not run after the prior 604008 ms build timeout and external compiler-lane observations.
2026-05-29 AUDIT_NATIVE_STATE continuation 4:
What was wrong: `Assets/_Project/Scripts/World/AbyssalThermalManager.cs` used legacy `TryLockBuffer/TryUnlockBuffer` for retained thermal grid readback and released via current `_dataVault`, which is unsafe across DataVault service replacement.
What was done: added one `ThermalMapReadbackMutationGuardMask`, stored the granting vault, retained it across readback ref-counts, released only on the final `ReleaseThermalGridReadback`, and made DataVault hot-swap dispose thermal map buffers before assigning the replacement service.
Cinematic Cheats used: preserved the existing thermal grid/Jacobi approximation and visual texture projection. No physical heat simulation or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-unlock and old-vault buffer release correctness.
Verification: source scan reports no `TryLockBuffer/TryUnlockBuffer` in `AbyssalThermalManager.cs`; remaining write locks are single-buffer routes with `finally` release. `git diff --check` exits 0 with LF/CRLF warning only. Full-file diff contains unrelated pre-existing dirty scratch/dump allocation hunks, so it is not used as Zero-GC proof for this patch. No compile/import/profiler proof run.
2026-05-29 AUDIT_NATIVE_STATE continuation 5:
What was wrong: `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs` held the copied terrain SDF snapshot for the scheduled leviathan terrain IK job through legacy `TryLockBuffer/TryUnlockBuffer`.
What was done: added `TerrainSdfSnapshotMutationGuardMask`, stored the granting vault, and released the guard on local failure, schedule failure, solver completion, DataVault hot-swap completion, and disposal.
Cinematic Cheats used: preserved the SDF/heightmap terrain hugging approximation, FABRIK solve, bite IK, and continuous quality-scaled segment/iteration policy. No physical creature simulation rewrite or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-unlock risk during scheduled fauna IK.
Verification: source scan reports no `TryLockBuffer/TryUnlockBuffer` or direct write-lock APIs in `FaunaKinematicsRuntime.cs`; added diff GC/hot dependency scan returns no hits; `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run.
2026-05-29 AUDIT_NATIVE_STATE continuation 6:
What was wrong: `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` used legacy `TryLockBuffer/TryUnlockBuffer` for placement buffer reads while building wreck render payloads and proxy meshes.
What was done: added a wreck buffer mutation guard bit helper, stored the granting vault/mask, released from normal unlock and full Vault-buffer teardown, and removed all legacy lock/unlock calls from this file.
Cinematic Cheats used: preserved deterministic WFC placement, merged mesh/proxy mesh generation, disabled nav bake policy, and existing cheap debris/artifact processing. No physical wreck simulation or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release and legacy-pin removal in source-local wreck generation.
Verification: source scan reports no `TryLockBuffer/TryUnlockBuffer` in `ProceduralWreckGenerator.cs`; added guard diff has no `new`, format, `.ToString`, LINQ, `foreach`, hot registry, or direct component lookup hits. `git diff --check` exits 0 with LF/CRLF warning only. File already had unrelated dirty loot/debris tick changes; they are not claimed as this patch. No compile/import/profiler proof run.
2026-05-29 AUDIT_NATIVE_STATE continuation 7:
What was wrong: `Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs` still held 21 scheduled KCC/environment/telemetry lanes through legacy per-buffer Vault pins. `Assets/_Project/Scripts/Physics/Vehicles/Automation/SubmarineAutopilotSdfNavigator.cs` still held scheduled initialization and solver lanes through `_scheduledPinMask` and `TryPinAutopilotVaultBuffer`.
What was done: KCC now uses one stored-vault `ScheduledVaultMutationGuardMask` and releases it from schedule failure, rollback, `LateFrameTick`, clear, and teardown routes. Autopilot now uses one initialization guard and one solver guard, validates every exact handle after guard acquisition, and releases through `UnlockBuffers` using the granting vault.
Cinematic Cheats used: preserved KCC flow/SDF/metabolism approximation, autopilot mock SDF/flow sampling, and route telemetry. No physical fluid/navigation simulation or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is removal of two scheduled multi-pin deadlock/stall surfaces and stale current-vault release risk.
Verification: scoped source scan over both files reports no `.TryLockBuffer`, `.TryUnlockBuffer`, stale scheduled pin helpers, stale scheduled lock fields, `GlobalRegistry.Get<`, or direct `GetComponent(`. Added diff GC/hot dependency scan returns no hits. `git diff --check` exits 0 with LF/CRLF warnings only. Remaining `TryAcquireWriteLock/ReleaseWriteLock` hits are the pre-existing single-buffer autopilot write helper route, not a scheduled multi-lock route. No compile/import/profiler proof run after the earlier 604008 ms build timeout.
2026-05-29 AUDIT_NATIVE_STATE continuation 8:
What was wrong: `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime.cs` held the main submarine solver buffers through per-buffer Vault pins, and `Assets/_Project/Scripts/Physics/Vehicles/SubmarineDynamicsRuntime_Gyroscopes.cs` added a second gyro pin group inside the same scheduled integration route.
What was done: replaced both pin groups with one combined simulation mutation guard. The main solver validates state/control/PID/mass/force/telemetry/added-mass/hydrodynamics/hull/tuning/config/drag handles. The gyro partial validates gyro/error/force-packet/telemetry/visual/counter handles without acquiring a second guard. `UnlockSimulationBuffers` releases the single stored granting vault.
Cinematic Cheats used: preserved deterministic hydrodynamic integration, added-mass approximation, gyro auto-level fake, and visual upload path. No physical fluid rewrite or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower deadlock/stall surface from replacing 18 scheduled pins with one guard and forbidding a separate gyro guard.
Verification: stale-symbol scan over the two files returns no `.TryLockBuffer`, `.TryUnlockBuffer`, `_simulationPinMask`, `_gyroSchedulePinMask`, `TryAcquireVaultBufferPin`, `TryPinSimulationBuffer`, or gyro pin helper hits. Hot dependency scan returns no `GlobalRegistry.Get<` or direct `GetComponent(` hits. Added diff GC/hot lookup scan returns no hits. `git diff --check` exits 0 with LF/CRLF warnings only. Compile/import/profiler proof blocked: CPU sample 99 and active `csc` PID 59168 plus `dotnet` PID 42284.
2026-05-29 AUDIT_NATIVE_STATE continuation 9:
What was wrong: `Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs` still used legacy `TryLockBuffer/TryUnlockBuffer` in editor aging/degradation snapshot leases, pending editor tuning apply, GPU upload reads, and CSV tuning read/write helpers.
What was done: added aging/degradation/tuning mutation guard masks, stored granting vaults for editor leases, released all guards through close/failure/finally paths, and kept exact DataVault handle validation before native data use.
Cinematic Cheats used: preserved cheap shader-buffer aging/degradation upload and CSV tuning workflow. No physical corrosion simulation or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in visual aging tooling/upload routes.
Verification: source scan returns no `.TryLockBuffer`, `.TryUnlockBuffer`, stale gizmo lock fields, stale `OneLock` helpers, direct write-lock APIs, `GlobalRegistry.Get<`, or direct `GetComponent(` in `VisualPressureAgingRuntime.cs`. Added diff GC/hot dependency scan returns no hits. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 10:
What was wrong: `Assets/_Project/Scripts/Graphics/Scalability/ThermalDynamicResolutionAdapter.cs` still used legacy DataVault pins for DRS state, resolution-scale state, telemetry, mock reconstruction input, and scalability quality snapshot. Pointer-style routes released via current `_dataVault`, creating stale-release risk on service rebind.
What was done: added DRS/scalability mutation guard masks, stored granting vaults for DRS/scale/telemetry pointer routes, released active guards before DataVault rebind/dispose, and converted short read routes to guard acquire/release in `finally`.
Cinematic Cheats used: preserved continuous `GlobalQualityWeight` scaling, STP/URP render-scale commits, visual-overkill shader weights, and blackbox dump format. No binary device branch or physical scalability model was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in the graphics scalability service.
Verification: `ThermalDynamicResolutionAdapter.cs` source scan returns no `TryLock`, `TryUnlock`, `.TryLockBuffer`, or `.TryUnlockBuffer` hits; hot dependency scan returns no `GlobalRegistry.Get<` or direct `GetComponent(` hits; added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 11:
What was wrong: `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` still used legacy DataVault pins for profile floats, blackbox telemetry, mock weather/damage/predator rows, species tuning, and blackbox dump scratch memory.
What was done: added VFX lane mutation guard masks, converted profile/blackbox acquisition helpers to guard acquire/release, and converted every mock/species/dump scratch route to `TryAcquireBiolumGuard` with `finally` release.
Cinematic Cheats used: preserved shader-driven pulse matrix/scalars, biome/weather/damage/predator mock fakes, CSV/binary profile loading, and continuous `HomeostasisBrain.GlobalQualityWeight` cadence. No physical light simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in the VFX bioluminescence runtime.
Verification: source scan returns no `TryLock`, `TryUnlock`, `.TryLockBuffer`, or `.TryUnlockBuffer` hits in `BiolumPulseSyncRuntime.cs`; project-wide legacy pin inventory no longer lists this file. Hot dependency scan returns no `GlobalRegistry.Get<` or direct `GetComponent(` hits. Added diff GC/hot dependency scan returns no hits. Remaining direct write locks are two editor static single-buffer routes released in `finally`. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 12:
What was wrong: `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs` still used legacy DataVault pins for interaction signal queue publish/clear/consume, surface-query staging writes, and request-lane reset.
What was done: added signal/staging mutation guard masks and shared acquire/release helpers, then converted every remaining legacy lock route in the handler to guard acquire with `finally` release.
Cinematic Cheats used: preserved queued late-frame signal dispatch, one-frame-latent surface query staging, SDF/terrain cheap hit approximation, and continuous `SignalBusRegistry.GlobalQualityWeight01` SDF step scaling. No physical interaction simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in interaction signal/staging routes.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, stale `ResetRequestLaneLocked`, `GlobalRegistry.Get<`, or direct `GetComponent(` hits in `EquipmentInteractionHandler.cs`. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 13:
What was wrong: `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs` used the central cartography helper to acquire many DataVault buffers one by one with `TryLockBuffer`, and scheduled simulation/upload pins released through current `_cartographyVault` rather than the granting vault.
What was done: replaced the per-buffer lock chain with one mutation guard mask derived from the requested cartography pin mask, preserved the logical pin mask for exact buffer resolution, and stored the granting vault for long-lived simulation/upload routes.
Cinematic Cheats used: preserved Morton fog-of-war math, one-frame dispatcher phases, RLE/upload jobs, blackbox telemetry, save DTO shape, and continuous quality-weight sampling/upload cadence. No physical cartography simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower deadlock/stale-release risk from replacing a 21-buffer cartography lock chain with one mutation guard.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `TryPinCartographyBuffer`, or `GlobalRegistry.Get<` hits in `PlayerExplorationTracker.cs`; direct non-`Try` `GetComponent(` scan returns no hits. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 14:
What was wrong: `Assets/_Project/Scripts/Core/Origin/AupOriginShiftCoordinator.cs` still had legacy pins in synchronous fallback rebase helpers for `VaultHotEntityData` and supplemental tether historical `float3` buffers.
What was done: replaced those fallback pins with mutation guard bits derived from the affected `BufferID`, released in `finally`, and left the existing scheduled `RebaseScheduleMutationGuardMask` route untouched.
Cinematic Cheats used: preserved deterministic AUP sector rebasing, time-sliced batch policy, double-precision math, telemetry DTOs, and fallback synchronous slices. No physical world simulation or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in origin-shift fallback routes.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in `AupOriginShiftCoordinator.cs`. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 15:
What was wrong: `Assets/_Project/Scripts/VoxelDeltaProcessor.cs` still used legacy DataVault pins for scheduled `ShinobuDeltaCrusherCarveWrites` acquisition and commit, and released via current `ResolveDataVault()` instead of the granting vault.
What was done: added `ScheduledCarveWritesMutationGuardMask`, stored `_scheduledCarveWritesGuardVault`, converted schedule/commit acquisitions to `TryAcquireScheduledCarveWritesGuard`, and released the guard through the granting vault in `UnlockScheduledCarveWrites`.
Cinematic Cheats used: preserved sliced carve scheduling, late-frame commit, backlog budget scaling, thermal melt behavior, blackbox schema, and voxel DTO layout. No physical terrain simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower stale-release and legacy-pin risk in the voxel carve write route.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in `VoxelDeltaProcessor.cs`. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 16:
What was wrong: `Assets/_Project/Scripts/VFX/JacobianFoam/Editor/JacobianFoamTunerWindow.cs` and `Assets/_Project/Scripts/World/SeedShipAnomaly/Editor/SeedShipAnomalyTunerWindow.cs` still used legacy DataVault pins. Seed Ship held field and tuning pins together.
What was done: converted Jacobian tuning writes to one mutation guard and converted Seed Ship field+tuning writes to one combined mutation guard. Both release in `finally`.
Cinematic Cheats used: preserved editor-only visual tuning ranges, telemetry preview graph, anomaly wire-gizmo pulse approximation, and existing DTO layouts. No runtime physical simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected player runtime delta 0 us because both files are editor-only; benefit is lower editor-side DataVault stall risk during tuning.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in both files. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists either file. `git diff --check` exits 0 with LF/CRLF warnings only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 17:
What was wrong: both MemorySentry relocation fuzzer copies still exercised legacy DataVault pin APIs in their pin/job stress lanes.
What was done: converted the active 1412 fuzzer and opt-in legacy 1310 fuzzer pin lanes to mutation guards with release in `finally`, preserving slot gates, forced compaction, corruption probe behavior, and report shape.
Cinematic Cheats used: none; this is editor/test infrastructure. The runtime DataVault stress signal now targets the current guard primitive.
Exact Microseconds saved: 0 us measured. Expected player runtime delta 0 us; benefit is better editor fuzzer coverage of current lock topology.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in both fuzzer files. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists either fuzzer file. `git diff --check` exits 0 with LF/CRLF warnings only. No compile/import/fuzzer proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 18:
What was wrong: `Assets/_Project/Scripts/Atmosphere/StormPropagation/Editor/ShinobuStormPropagationTunerWindow.cs` used legacy pins for tuning reads/writes and held telemetry ring plus cursor pins together during editor graph drawing.
What was done: converted tuning operations to one mutation guard and telemetry graph reads to one combined ring+cursor mutation guard, both released in `finally`.
Cinematic Cheats used: preserved editor graph line drawing, storm tuning slider ranges, DTO layout, and runtime propagation math. No runtime physical simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected player runtime delta 0 us because this is editor-only; benefit is lower editor-side DataVault stall risk.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in the file. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory no longer lists this file. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 19:
What was wrong: `Assets/_Project/Scripts/Editor/SpatialGridXRayWindow.cs` still used legacy DataVault pins in editor telemetry, raw-grid, debug-cell, and read-set helpers.
What was done: converted telemetry read set to one mutation guard, raw-grid cursor+ring to one combined guard, and single-buffer debug/raw-grid copy routes to single mutation guards with `finally` release.
Cinematic Cheats used: preserved editor X-Ray histogram, raw spatial grid drawing, debug-cell fallback, and diagnostic latest-vault route. No runtime physical simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected player runtime delta 0 us because this is editor-only; benefit is lower editor-side DataVault stall risk.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in the file. Project-wide legacy pin inventory no longer lists it. `git diff --check` exits 0 with LF/CRLF warning only. Full-file diff GC scan is not clean because unrelated pre-existing editor scratch arrays are already dirty in this file; no compile/import/editor-run proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 20:
What was wrong: `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` used legacy DataVault pins around breach repair, compartment mapping, fatigue, and hull damage job buffer windows.
What was done: changed the job helper to validate handles only, then acquire one computed mutation guard mask for the complete job buffer set. Existing cleanup paths now release that single guard.
Cinematic Cheats used: preserved hull damage diffusion, pressure fatigue, breach repair, leak plume visual sync, and cheap visual feedback routes. No physical fluid breach simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower multi-pin and stale-release risk in structural job windows.
Verification: source scan returns no `TryLockBuffer`, `TryUnlockBuffer`, `TryLock`, `TryUnlock`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in the file. Added diff GC/hot dependency scan returns no hits. Project-wide legacy pin inventory now lists only `HectonVoxelVolume.cs`, `HectonVoxelEngine.cs`, and `DestructibleOrganicManager.cs`. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 21:
What was wrong: `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` still used legacy DataVault pins across DearLie destruction jobs, lifecycle mutation/read routes, parasite exposure reads, overgrowth mutation, regeneration records, telemetry ring, template/loot/yield caches, drop budget/output, pending yield events, destroyed UID checks, and titan root-mound state. The DearLie job route was especially risky because it held pins across schedule/complete and released through the current `_dearLieVault`, so a vault rebind could release the wrong service.
What was done: replaced per-buffer pins with computed mutation guard masks. DearLie job windows now acquire one guard mask for all job buffers, store the granting vault, and release that vault in the completion/failure cleanup path. Lifecycle mutation combines regrowth+maturation into one guard call instead of holding two guards. Read, parasite, overgrowth, yield, drop, telemetry, template, regen, and root-mound routes now acquire one guard and release it in `finally`.
Cinematic Cheats used: preserved organic destruction/decomposition/regrowth math, SignalBus debris output, shader/audio presentation, continuous quality-budget scaling, and voxel root-mound request behavior. No physical organics simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower multi-pin, stale-release, and deadlock risk in organic runtime routes.
Verification: source scan returns no `TryLock`, `TryUnlock`, `.TryLockBuffer`, `.TryUnlockBuffer`, `GlobalRegistry.Get<`, or direct non-`Try` `GetComponent(` hits in `DestructibleOrganicManager.cs`. Project-wide DataVault pin inventory no longer lists this file; remaining real DataVault pin tail is voxel SDF lease code in `HectonVoxelVolume.cs` and `HectonVoxelEngine.cs`, plus false-positive `GraphicsBuffer` helper names. `git diff --check` exits 0 with LF/CRLF warning only. Full-file diff Zero-GC is not claimed because unrelated cold managed scratch arrays were already dirty. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 22:
What was wrong: `Assets/_Project/Scripts/HectonVoxelVolume.cs` and `Assets/_Project/Scripts/HectonVoxelEngine.cs` were the last real runtime DataVault legacy pin tail. Published sonar SDF/audio/descriptor reads used `TryLockBuffer/TryUnlockBuffer`, public nearest-SDF leases in the engine released direct buffer pins, and the async SDF publisher held multiple DataVault write locks across SDF/audio/descriptor work.
What was done: replaced published sonar read pins with one descriptor+SDF+audio mutation guard mask and a static zero-GC refcount gate. Public engine leases now release through `HectonVoxelVolume.ReleasePublishedSonarPayloadReadGuard`. The async publisher now takes one combined payload mutation guard and writes SDF/audio/descriptor views via `TryResolveHandle`, eliminating the multi-write-lock path.
Cinematic Cheats used: preserved byte-encoded SDF, nearest-cell audio material lookup, raymarch shortcuts, cheap gradient sampling, and existing sonar payload DTO layout. No physical terrain simulation, capacity rewrite, or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is removal of the final runtime DataVault legacy pin route and the SDF/audio/descriptor multi-write-lock topology.
Verification: scoped scan returns no `.TryLockBuffer` or `.TryUnlockBuffer` in `HectonVoxelVolume.cs` or `HectonVoxelEngine.cs`; hot dependency scan returns no `GlobalRegistry.Get<` or direct non-`Try` `GetComponent(` hits. Project-wide DataVault pin inventory now lists only GlobalDataVault API definitions/implementation, editor scanner string literals, and non-DataVault `GraphicsBuffer.TryUnlockBufferAfterWrite` helpers. `git diff --check` exits 0 with LF/CRLF warnings only. Compile/import/profiler proof blocked: CPU sample `84`, active `dotnet` PID `58948`; no build launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 23:
What was wrong: runtime component lookup scan still showed direct non-`Try` `GetComponent` in shared/runtime code. `HullDentShaderController` also recalculated fallback `transform` from `LateFrameTick` through `ResolveRoot` when no explicit submarine root was assigned.
What was done: converted runtime type lookups in `HullDentShaderController`, `GlobalRegistryContracts.ResolveParentService`, and `MetaRuntimeInstaller` to `TryGetComponent(Type, out Component)`. `HullDentShaderController.ResolveRoot` now returns the serialized root or keeps the first cached fallback transform.
Cinematic Cheats used: preserved shader-only hull dents and late-frame visual presentation. No physical hull deformation, service ownership rewrite, or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is removal of direct runtime component lookup tail and repeated fallback transform resolution from a visual late-frame presenter.
Verification: project scan for `GlobalRegistry.Get<` and direct non-`Try` `GetComponent(` now reports only editor authoring/repair files and one `#if UNITY_EDITOR && HECTON8_AMPLIFY_IMPOSTORS` prefab scan. Scoped `git diff --check` exits 0 with LF/CRLF warnings only. Added diff scan has no reference-type allocation, LINQ, `foreach`, string formatting, hot registry, or direct component lookup. No compile/import/profiler proof run.
2026-05-29 AUDIT_NATIVE_STATE verification:
What was wrong: compile proof was still missing after the source cleanup.
What was done: waited for a legal throttle window (`CPU=40`, no compiler rows) and launched one limited build: `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: build timed out after `904012 ms` with no diagnostics in captured output. Follow-up showed lingering `dotnet` PID `58736` running the same build and `VBCSCompiler` PID `28496`; both were stopped. Later compiler process scan returned no rows. CPU sample after cleanup was `100`, so no retry was launched. Compile/import/profiler proof remains absent.
2026-05-29 AUDIT_NATIVE_STATE continuation 24:
What was wrong: `Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs` held paired direct DataVault write locks in `TryLoadBiomeProfilesFromEditorCsv` (`CsvScratch` plus `BiomeProfiles`) and `RecordTelemetry` (`TelemetryRing` plus `TelemetryCursor`).
What was done: added combined mutation guard masks for profile CSV and telemetry routes, resolved exact guarded mutable views by `BufferID` and minimum length, released guards in `finally`, and added `OnDestroy` forwarding to idempotent `OnServiceShutdown`.
Cinematic Cheats used: preserved shader global flora sway and continuous `HomeostasisBrain.GlobalQualityWeight` math. No physical plant simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is lower lock-stall/deadlock risk in flora sway profile and telemetry routes.
Verification: remaining direct write-lock releases in the file are single-buffer flow/params/tuning/generic helper releases. Stale paired profile/telemetry write-lock pattern scan returns no hits. Added diff scan returns no `string.Format`, `.ToString()`, LINQ `.Select/.Where`, `foreach`, `GlobalRegistry.Get<`, direct non-`Try` `GetComponent(`, or added reference-type `new`. `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof run in this step.
2026-05-29 AUDIT_NATIVE_STATE continuation 25:
What was wrong: several paired write routes remained after the large legacy pin cleanup. `Assets/_Project/Scripts/Input/ControlRemapper.cs` advanced telemetry cursor before proving the ring write. `Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs` wrote carrion death ingress and counters in separate phases and needed rollback. Nutrient and carrion CSV loaders split scratch/profile writes. `Assets/_Project/Scripts/ModularEquipmentEngine.cs` advanced fault telemetry cursor before ring write success. `Assets/_Project/Scripts/Visor/HectonVisorARStencilRendererFeature.cs`, `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs`, and `Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs` split CSV scratch/profile writes across two locks.
What was done: each paired route now uses one mutation guard mask over the exact logical buffer set and releases it in `finally`. Control remapping, equipment fault telemetry, carrion death ingress, nutrient/carrion profile CSV, AR stencil profile CSV, aesthetic CSV, and noir color CSV all resolve native views under the guard before mutating. Nutrient and carrion job guards now store the granting `IDataVault` and release that same vault, preventing stale release through a swapped `_vault`.
Cinematic Cheats used: preserved shader-driven visor/noir presentation, cheap nutrient/carrion scalar fields, existing equipment telemetry, and input binding DTOs. No physical simulation, DTO migration, or binary low-end/high-end branch was introduced.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is removal of lost-slot, rollback, stale-release, and multi-lock deadlock vectors.
Verification: project `.TryLockBuffer/.TryUnlockBuffer` scan now reports only `GlobalDataVault` API definitions/implementation, editor scanner strings, and non-DataVault `GraphicsBuffer.TryUnlockBufferAfterWrite`. Hot lookup scan reports only editor paths and `#if UNITY_EDITOR && HECTON8_AMPLIFY_IMPOSTORS`. Method-level write-lock scan reports only wrapper false positives and editor/fuzzer tests, no runtime multi-acquire methods. Added diff scan returns no `string.Format`, `.ToString()`, LINQ `.Select/.Where`, `foreach`, hot registry, or direct non-`Try` component lookup. Scoped `git diff --check` exits 0 with LF/CRLF warnings only. CPU sample `100`; no compiler process rows; no build/import/profiler run launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 26:
What was wrong: `ShinobuPlasmaBeamRuntime`, `SumpPumpPipeGridRuntime`, `VolcanicUpdraftDirector`, and `SpatialAudioManager` had scheduled or long-lived mutation guards that could be released through mutable `_vault`/`_dataVault` fields instead of the vault that granted the guard. `SpatialAudioManager` acoustic portal work+scratch also kept a nested guard shape inside one pathfinding route.
What was done: added stored granting-vault fields for Plasma job guards, Sump active/local drainage guards, Volcanic fixed-pipeline guards, and SpatialAudio previous-AUP/acoustic-portal guards. Replaced nested acoustic portal work+scratch acquisition with one `AcousticPortalPathMutationGuardMask` when scratch is nested under path work.
Cinematic Cheats used: no new simulation. Existing shader/DSP/proxy systems and continuous quality scalar usage were preserved.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release and deadlock-vector removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warnings only. Touched-file scans return no legacy DataVault pins, no hot registry/direct component lookup, no added direct write locks, no added GC/string/LINQ/foreach patterns, and no binary low-end switch. Project-wide DataVault pin inventory remains only GlobalDataVault API, editor scanner literals, and non-DataVault GraphicsBuffer upload helpers. CPU sample `88`; compiler process scan returned no rows; no build/import/profiler run launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 27:
What was wrong: `HabitatFluidIncursionDirector` scheduled/local fluid mutation guards released through mutable `_vault`, not the vault that granted the guard.
What was done: added `_activeMutationGuardVault`, assigned it on scheduled guard acquisition, cleared it on release/reset, and carried `guardVault` through every local topology/mock/CSV `finally` release.
Cinematic Cheats used: preserved scalar flood approximation, fixed/post-fixed phase, waterline shader upload, and acoustic muffle signal. No physical fluid simulation expansion or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release/deadlock-vector removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale local guard signature/release scan returns no old route hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct write-lock/binary-switch hits. No build launched; current CPU proof window was already above throttle.
2026-05-29 AUDIT_NATIVE_STATE continuation 28:
What was wrong: `BallisticsRuntime.FrameTick` scheduled jobs over Vault-backed native lanes without a lifetime mutation guard. Short ballistic mutation routes also released through mutable `_vault`.
What was done: acquired one ballistic mutation guard before scheduled lane resolution, stored `_activeJobMutationGuardVault`, released after completion telemetry, and converted short mutation routes to captured `guardVault` releases.
Cinematic Cheats used: preserved bounded ballistic batch jobs, signal budget, and staged impact VFX. No physical projectile over-simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is relocation/stale-release risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale `_vault.TryAcquireMutationGuard(MutationGuardBit)` and `_vault.ReleaseMutationGuard(MutationGuardBit)` scans return no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct write-lock/binary-switch hits. No build launched.
2026-05-29 AUDIT_NATIVE_STATE verification 29:
What was wrong: compile proof after the Ballistics guard patch was still absent.
What was done: used two legal throttled build windows. First window: `CPU=26`, no compiler rows; `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed after `00:16:04.19` with `Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs(72,17): error CS0246: The type or namespace name 'PagerNativeState' could not be found`. `dotnet build-server shutdown` was run. Second window: `CPU=46`, no compiler rows; build failed after about `524.5 s` with `MSB4006` circular dependency errors in `Unity.RenderPipelines.Universal.Runtime.csproj` involving `ResolveProjectReferences` and `_GetCopyToOutputDirectoryItemsFromTransitiveProjectReferences`.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: compile proof is failed, not complete. Follow-up compiler process scan returned no rows; CPU sample was `69`, so no third build was launched under the throttling rule. The observed build failures are outside the source-local NativeArray/DataVault guard edits, but they still block any green compile claim.
2026-05-29 AUDIT_NATIVE_STATE continuation 30:
What was wrong: `ChemicalInfluenceGrid` acquired the scheduled chemical solver `SimulationMutationGuardMask` through `_dataVault` but released through mutable `_dataVault` later. A DataVault replacement during scheduled work could leave the granting vault unreleased and release the wrong service.
What was done: added `_scheduledBuffersGuardVault`, stored the granting vault on acquisition, released that exact vault in `UnlockSimulationBuffers`, and cleared the field on unlock/reset.
Cinematic Cheats used: preserved the existing cheap chemical grid diffusion approximation, SDF sampling shortcut, mock emitter route, and continuous `GlobalQualityWeight` iteration scaling. No physical chemistry simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal in the scheduled solver window.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale `_dataVault?.ReleaseMutationGuard(SimulationMutationGuardMask)` and `_dataVault.TryAcquireMutationGuard(SimulationMutationGuardMask)` scans return no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct write-lock/binary-switch hits. Legacy `TryLockBuffer/TryUnlockBuffer` scan returns no hits. No compile/import/profiler run was launched because the latest compile proof is still failed by unrelated SaveSystem/URP errors.
2026-05-29 AUDIT_NATIVE_STATE continuation 31:
What was wrong: `ModuloSimulationBucketer` held `RebalanceVaultMutationGuardMask` across scheduled rebalance work but released through current `_dataVault`, not the vault that granted the guard.
What was done: added `_rebalanceVaultGuardVault`, stored it after successful acquisition, released through it in `ReleaseRebalanceVaultGuard`, and cleared it on all release paths.
Cinematic Cheats used: preserved modulo bucketing and continuous active-slow-bucket scaling. No scheduler rewrite, binary device branch, or over-engineered simulation was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal in the core rebalance window.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale direct `_dataVault.ReleaseMutationGuard(RebalanceVaultMutationGuardMask)` scans return no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct write-lock/binary-switch hits. Legacy `TryLockBuffer/TryUnlockBuffer` scan returns no hits. No compile/import/profiler run was launched because compile proof is already failed by unrelated SaveSystem/URP errors.
2026-05-29 AUDIT_NATIVE_STATE verification 32:
What was wrong: compile/import/profiler proof after ChemicalInfluenceGrid and ModuloSimulationBucketer guard fixes was still absent.
What was done: checked throttle before any possible build. CPU sample returned `100`. Compiler process scan found active `dotnet` PID `26320` running `dotnet build .\Hecton8.Core.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore`.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: no build, Unity import, Play Mode, profiler, GCMonitor, or native ledger run was launched. This is compliant with the compile throttling rule because CPU is above 50 and another dotnet build is active.
2026-05-29 AUDIT_NATIVE_STATE continuation 33:
What was wrong: `ShinobuEcosystemBalancer` stored `_jobMutationGuardVault` but release preferred a caller-supplied current vault; `HydrodynamicKccRuntime` could acquire a metabolism state read guard inside the scheduled KCC guard window.
What was done: Shinobu release now prefers `_jobMutationGuardVault`; KCC scheduled guard now covers `ShinobuMetabolismStates`, and metabolism view resolution skips the second guard when the scheduled guard is active.
Cinematic Cheats used: preserved existing ecosystem and KCC approximations, post-fixed phase ownership, and continuous quality scaling. No physical simulation expansion or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release and nested-guard risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warnings only. Stale-route scans return no old Shinobu release priority and no direct `_dataVault` metabolism guard acquire/release path. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 34:
What was wrong: `QuestDagResolverService` did not store an explicit granting vault for its scheduled resolver guard; `SolarPowerGenerationRuntime` released scheduled solar job guard through mutable static `s_vault` and read Voxel SDF payloads through that static while the job guard was active.
What was done: Quest DAG now stores `_scheduledBufferGuardVault`; Solar now stores `s_jobMutationGuardVault`, releases through it, clears it on reset/no-op release, and uses it for guarded SDF payload reads.
Cinematic Cheats used: preserved Quest cadence dilation and solar optical-depth approximation with continuous quality-weight SDF sample scaling. No physical lighting expansion or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release invariant cleanup.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warnings only. Stale release scans for Quest/Solar return no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 35:
What was wrong: `VehicleComponentDamageRuntime` held `DamageMutationGuardMask` behind `_buffersLocked` but did not store the granting vault explicitly.
What was done: added `_damageGuardVault`, stored it on successful damage guard acquisition, released through it, and cleared it on all unlock exits.
Cinematic Cheats used: preserved bounded grid damage and mock-signal routes. No physical deformation expansion or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is explicit long-lived guard release ownership.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale `_dataVault.ReleaseMutationGuard(DamageMutationGuardMask)` scan returns no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 36:
What was wrong: `BabelSubtitleSyncRuntime` released cue/telemetry mutation guards through current `s_vault`; `ParasiteSwarmGpuRuntime` released transferred telemetry owner views through current `_vault` and used `_vault` directly in tuning seed release.
What was done: Babel now stores granting vaults per mutation lane and releases through those stored services; Parasite now stores `_telemetryGuardVault` and uses a captured local vault for tuning seed writes.
Cinematic Cheats used: preserved subtitle buffer flow and GPU parasite visual fake budgets. No physical parasite simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warnings only. Stale guard-release scans return no hits for the patched routes. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 37:
What was wrong: `AudioLogSystem` transferred playback queue and encrypted fragment mutable views to callers, but release selected current `_dataVault`.
What was done: added `_playbackQueueGuardVault` and `_encryptedFragmentStateGuardVault`, stored them only after guarded resolution succeeds, and released stored guards before vault buffer teardown/rebind.
Cinematic Cheats used: preserved fixed-size queue/bitset audio-log paths. No new simulation or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale playback/encrypted guard-release scans return no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 38:
What was wrong: `ProceduralOreSpawner` kept the active geology mutation guard only as `_lockedVaultBufferMask`; parameterless unlock released through current `_dataVault`.
What was done: added `_lockedVaultGuardVault`, stored it for depletion/depletion-mask/runtime-shift guarded windows, and released through it.
Cinematic Cheats used: preserved cheap geology depletion masks, dormant visual weighting, HZB/indirect upload behavior, and continuous visual scaling.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warning only. Stale geology unlock release scan returns no hits. Added diff scans return no GC/string/LINQ/foreach/hot lookup/direct component lookup/binary-switch hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 39:
What was wrong: `SubmarineStructuralGrid` and `StructuralIntegrityCalculatorRuntime` structural mutation guard helpers released through current `_dataVault` instead of the service that granted `StructuralMutationGuardMask`.
What was done: added `_structuralMutationGuardVault` in both runtimes, stored the granting vault on successful acquire, and released through that stored service before clearing it.
Cinematic Cheats used: preserved existing structural grid, breach repair, fatigue/deformation, blackbox, and presentation paths. No physical hull simulation expansion or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-release risk removal.
Verification: scoped `git diff --check` exits 0 with LF/CRLF warnings only. Structural guard field/helper scans show stored-vault acquire/release routes in both files. Added diff scan returns no GC/string/LINQ/foreach/hot lookup/direct component lookup hits. No compile/import/profiler run was launched.
2026-05-29 AUDIT_NATIVE_STATE continuation 40:
What was wrong: residual `ReleaseMutationGuard` and short `ReleaseWriteLock` helpers in audited native lifecycle files still called mutable `_vault`, `_dataVault`, or `s_vault` directly. `JacobianFoamGpuRuntime` read pins also released via current `_vault`.
What was done: converted `VehicleComponentDamageRuntime` CSV/blackbox/editor tuning guards, `SubmarineDynamicsRuntime` boot/CSV/hull guards and write-lock helper, `SubmarineDynamicsRuntime_Gyroscopes` default/profile CSV guards, `PersistentWorldRegistry` native collection mutation/write locks, `JacobianFoamGpuRuntime` read pins/write helpers, and `HectonVoxelEngine.JobTableLease.Dispose` to captured local or stored granting-vault release.
Cinematic Cheats used: no simulation change. Continuous quality consumers remain in Jacobian foam, submarine dynamics, and voxel systems. No binary low-end switch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is release-owner determinism and stale-vault failure removal.
Verification: project direct mutable mutation release scan returns no hits. Touched-file direct mutable write-lock release scan returns no hits. Scoped `git diff --check` exits 0 with LF/CRLF warnings only. Added diff scan returns no `string.Format`, `.ToString()`, LINQ `.Select/.Where`, `foreach`, `GlobalRegistry.Get<`, direct `GetComponent(`, or reference-type `new`. Hot lookup scan reports only editor paths and `#if UNITY_EDITOR && HECTON8_AMPLIFY_IMPOSTORS`. Legacy DataVault pin scan reports only GlobalDataVault API definitions/implementation, editor scanner strings, and non-DataVault GraphicsBuffer upload helpers.
2026-05-29 AUDIT_NATIVE_STATE verification 41:
What was wrong: compile proof after continuation 40 is absent.
What was done: checked compile throttle. CPU sample returned `70`; active compiler row exists: `dotnet` PID `67300`.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger was launched. This obeys compile throttling. Last known real build proof is still failed by unrelated SaveSystem `PagerNativeState` and URP `MSB4006` circular dependency errors.

2026-05-29 AUDIT_NATIVE_STATE continuation 42:
What was wrong: generic NativeArray write-lock wrappers in `PlayerInventory`, `GlobalPhysicsStateManager`, `HectonFluidEngine`, and `EcosystemDirector` acquired locks through one DataVault reference but released through mutable/current vault state. A vault rebind between acquire and release could unlock the wrong service.
What was done: added stored granting-vault fields to `InventoryVaultLane<T>`, `VaultBufferBinding<T>`, `FluidVaultBuffer<T>`, and `VaultBufferView<T>`. Successful `TryAcquireWriteLock` stores the exact granting `IDataVault`; `ReleaseWriteLock` and teardown release through that stored service and clear it.
Cinematic Cheats used: none; ownership hygiene only. No physics/fluid/ecology/inventory simulation behavior changed.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault write-lock release removal.
Verification: direct mutable-vault `TryAcquireWriteLock` and `ReleaseWriteLock` scans over the four touched files return no hits. Scoped `git diff --check` exits 0 with LF/CRLF warnings only. Project hot lookup scan reports only editor/conditional editor paths. Project `TryLockBuffer/TryUnlockBuffer` inventory remains limited to GlobalDataVault API/editor strings/non-DataVault GraphicsBuffer helpers. Full touched-file forbidden-allocation diff scan reports one pre-existing dirty `new PostSimulationPhaseSystem(this)` in `PlayerInventory.cs`; this patch does not claim it clean.

2026-05-29 AUDIT_NATIVE_STATE verification 43:
What was wrong: compile/import/profiler proof after continuation 42 is absent.
What was done: checked compile throttle. CPU sample returned `68`; compiler process scan returned no rows.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger was launched because CPU is above the 50 percent build throttle. Last known real build proof is still failed by unrelated SaveSystem `PagerNativeState` and URP `MSB4006` circular dependency errors.

2026-05-29 AUDIT_NATIVE_STATE continuation 44:
What was wrong: `VRSomaticProvider` held two DataVault write locks simultaneously in hand/root paths and released write locks through current `_vault`. Some length-failure branches could return after acquire before marking the buffer as locked for `finally` release.
What was done: added stored `_writeLockVault` to `VaultBufferView<T>`, split hand/root writes into sequential lock windows, used read-only hand targets while writing physical positions, and moved post-acquire length checks inside `try/finally` release windows.
Cinematic Cheats used: preserved the existing VR comfort fake/spring path and continuous `_globalQualityWeight01` scaling. No physical hand solver or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is deadlock/stall and leaked-write-lock removal.
Verification: direct mutable-vault write-lock acquire/release scan in `VRSomaticProvider` returns no hits. `TryAcquireWriteNativeArray(...) || length` leak-pattern scan returns no hits. Scoped `git diff --check` exits 0 with LF/CRLF warning only. Added diff forbidden-allocation/hot-lookup scan returns no hits.

2026-05-29 AUDIT_NATIVE_STATE verification 45:
What was wrong: compile/import/profiler proof after continuation 44 is absent.
What was done: checked compile throttle. CPU sample returned `94`; compiler process scan returned no rows.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger was launched because CPU is above the 50 percent build throttle.

2026-05-29 AUDIT_NATIVE_STATE continuation 46:
What was wrong: `HazardZoneManager` state mutation views used `HazardStateMutationGuardMask` but released through current `_dataVault`.
What was done: added `_hazardStateGuardVault` and `_hazardStateGuardHeld`; nested state writes are rejected; successful state guard acquire stores the granting vault only after mutable views and capacity validation; release/teardown releases through the stored vault.
Cinematic Cheats used: preserved the existing cheap hazard sphere/LUT route and exposure job. No physical hazard field solver or binary device branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault guard release removal.
Verification: stale direct `_dataVault` acquire/release scan for `HazardStateMutationGuardMask` returns no hits. Added diff forbidden-allocation/hot-lookup scan returns no hits. Scoped `git diff --check` exits 0 with LF/CRLF warning only.

2026-05-29 AUDIT_NATIVE_STATE verification 47:
What was wrong: compile proof after continuations 42, 44, and 46 was absent.
What was done: used legal throttle window: CPU `37`, no compiler rows. Ran one `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`.
Cinematic Cheats used: none; verification only.
Exact Microseconds saved: 0 us measured.
Verification: build failed after `00:13:33.66` with 52 errors: `MSB4006` circular dependencies in `MoreMountains.Tools.csproj` and `Unity.RenderPipelines.Universal.Runtime.csproj`; missing `NativeDisableUnsafePtrRestriction`/`NativeDisableContainerSafetyRestriction` symbols in `SubmarineDynamicsContracts.cs`; missing `BufferID` symbols in `VehicleComponentDamageContracts.cs`. Follow-up CPU sample was `90`, compiler process scan returned no rows. No second build launched.

2026-05-29 AUDIT_NATIVE_STATE continuation 48:
What was wrong: build errors showed missing native-contract imports: unsafe job attributes in `SubmarineDynamicsContracts.cs` and `BufferID` in `VehicleComponentDamageContracts.cs`.
What was done: restored `using Unity.Collections.LowLevel.Unsafe;` and `using Hecton8.Core.Memory;`.
Cinematic Cheats used: none; dependency repair only.
Exact Microseconds saved: 0 us measured.
Verification: scoped `git diff --check` on both contract files exits 0 with LF/CRLF warnings only. Post-fix CPU sample was `63`; no second build launched because CPU is above throttle. Project graph circular dependency remains unproven/unfixed.

2026-05-29 AUDIT_NATIVE_STATE continuation 49:
What was wrong: `WorldGenerativeGeologyTerrainSeamApplier` still used legacy `TryLockBuffer/TryUnlockBuffer` to pin the hybrid terrain seam native buffers. The release path resolved current `_dataVault`, not the service that granted the pins.
What was done: removed the legacy per-buffer pin helper and replaced it with one computed mutation guard mask over baseline height, optional vault heightmap, native plans, patch heights, blend mask, and optional normals. The projection path stores the granting `IDataVault` locally and releases the same guard after the synchronous projection fence and again from `finally` for early exits.
Cinematic Cheats used: preserved the existing hybrid terrain seam visual fake and `GlobalQualityWeight`-driven mask/detail scaling. No physical terrain solver, binary low-end branch, or DTO layout change was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault unlock removal and project-wide legacy pin elimination.
Verification: scoped `TryLockBuffer/TryUnlockBuffer` scan in `WorldGenerativeGeologyTerrainSeamApplier` returns no hits; project-wide `.TryLockBuffer/.TryUnlockBuffer` scan over `Assets/_Project/Scripts` returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. Full-file Zero-GC proof is not claimed because unrelated pre-existing dirty hunks in this file contain cold allocations.

2026-05-29 AUDIT_NATIVE_STATE continuation 50:
What was wrong: `TerrainChunkPagerRuntime.TryAcquireWriteArray` acquired write locks through a local DataVault but `ReleaseWriteArray` released through mutable `_vault`; `WriteTelemetry` held `Counters` and `TelemetryRing` write locks simultaneously.
What was done: changed `TryAcquireWriteArray` to return the granting `IDataVault`, updated all call-sites to release through that captured lease vault, and split `WriteTelemetry` into counters-write then telemetry-write with no overlapping write locks.
Cinematic Cheats used: none; streaming math and continuous `GlobalQualityWeight` residency/ring scaling are unchanged.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal and nested write-lock topology removal.
Verification: scoped `_vault?.ReleaseWriteLock`/`_vault.ReleaseWriteLock` and `_vault.TryAcquireWriteLock` scan in `TerrainChunkPagerRuntime.cs` returns no hits; stale old-signature `TryAcquireWriteArray` scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits for this file.

2026-05-29 AUDIT_NATIVE_STATE continuation 51:
What was wrong: `PhysicsApplySystem` force packet write helpers released through current `_dataVault`, and validation scheduling acquired a front packet write lock while already holding the validation mutation guard.
What was done: write-lock helpers now return the granting `IDataVault` and all force-packet/mask release paths use that captured vault. Scheduled validation now resolves the front packet buffer read view instead of acquiring a write lock for a read-only copy into guarded validation buffers.
Cinematic Cheats used: none; force packet queueing, validation jobs, and Rigidbody application are unchanged.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal and one nested write-lock window removal.
Verification: scoped direct `_dataVault?.ReleaseWriteLock`/`_dataVault.TryAcquireWriteLock` scan in `PhysicsApplySystem.cs` returns no hits; stale helper signature/release scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits.

2026-05-29 AUDIT_NATIVE_STATE continuation 52:
What was wrong: `DebrisManager.TryAcquireVaultBuffer` acquired front/back debris state write locks through local `_dataVault` but `ReleaseVaultWrite` released through current `_dataVault`.
What was done: `TryAcquireVaultBuffer` now returns the granting `IDataVault`; all origin-shift, reset, pending-shift, and thermal petrification write windows release through the captured vault.
Cinematic Cheats used: preserved the existing cheap debris simulation and thermal petrification/additive SDF fake. No physical debris overhaul or binary quality branch was added.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal.
Verification: scoped direct `_dataVault?.ReleaseWriteLock`/`_dataVault.TryAcquireWriteLock`, stale `ReleaseVaultWrite(in ...)`, and old-signature `TryAcquireVaultBuffer` scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits.

2026-05-29 AUDIT_NATIVE_STATE continuation 53:
What was wrong: `SolarPowerGenerationRuntime` used mutable static `s_vault` directly for write-lock acquire/release in panel state, profile CSV, and conditions paths; public panel state write lease had no stored granting-vault owner.
What was done: short writes now capture local vaults and release through them. Public panel-state write leases store `s_panelStateWriteVault`, and subsystem reset releases the stored lease before clearing handles.
Cinematic Cheats used: none; solar optical/SDF sampling, power routing, and continuous `GlobalQualityWeight` scaling are unchanged.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault release removal for static solar lanes.
Verification: scoped `s_vault.ReleaseWriteLock`/`s_vault.TryAcquireWriteLock` scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. Latest CPU sample was `79`, so no build launched.

2026-05-29 AUDIT_NATIVE_STATE continuation 54:
What was wrong: `HectonBilateralDrsUpscalerRuntime` write helper acquired via local vault but call-sites released via mutable `_dataVault` across tuning, parameters, telemetry, profiles, mock state, and CSV scratch lanes.
What was done: `TryAcquireVaultWriteBuffer` now returns the granting `IDataVault`; all write paths release through that captured vault.
Cinematic Cheats used: none; Bilateral DRS/STP/URP math, GPU upload, and continuous quality scaling are unchanged.
Exact Microseconds saved: 0 us measured. Expected steady-frame delta 0 us; benefit is stale-vault write-lock release removal.
Verification: scoped direct `_dataVault?.ReleaseWriteLock`/`_dataVault.TryAcquireWriteLock` and old-signature `TryAcquireVaultWriteBuffer` scans return no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits.
## 2026-05-29 Continuation 55 - QuestDag short write-lock lease releases

What was wrong: `QuestDagResolverRuntime` retained direct `_vault.TryAcquireWriteLock`/`_vault.ReleaseWriteLock` in four short counter/telemetry patch methods. `_vault` is readonly, so this was not the same mutable-service risk as `_dataVault`, but it still failed the strict lease-release scan.
What was done: `PatchPendingScheduleDrops`, `PatchSpatialHashRebuildCount`, `InvalidateSpatialHash`, and `PatchLastComputeTime` now acquire through a local `IDataVault vault` and release through that same local in `finally`.
Cinematic Cheats used: none; quest state/counter updates are ownership cleanup only.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_vault.TryAcquireWriteLock`/`_vault.ReleaseWriteLock` scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof was run for this source-local patch.

## 2026-05-29 Continuation 56 - CombatDamage mutation-guard lease scan cleanup

What was wrong: `CombatVaultMutationGuardLease.TryAcquire` acquired through `_vault.TryAcquireMutationGuard(_mask)` even though the release path already snapshots the stored vault. This was not a runtime hot lookup, but it kept the direct mutation-guard scan noisy.
What was done: `TryAcquire` now snapshots `_vault` into a local `IDataVault vault` and acquires through that local.
Cinematic Cheats used: none; combat damage, status, armor, and signal paths are unchanged.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_vault.TryAcquireMutationGuard/_vault.ReleaseMutationGuard` scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 57 - CrashTelemetryBuffer write-lock lease field read

What was wrong: `CrashTelemetryBuffer.VaultArray<T>.this[index].set` acquired and released through the stored `_vault` field inside one diagnostic write-lock window.
What was done: the setter now snapshots `_vault` into local `IDataVault vault`, acquires through it, and releases through it in `finally`.
Cinematic Cheats used: none; crash ring and blackbox evidence path are unchanged.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_vault.TryAcquireWriteLock/_vault.ReleaseWriteLock` scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 58 - ProceduralWreckGenerator render/debris lock flattening

What was wrong: render payload and debris build paths held multiple DataVault write locks at once; `VaultArrayBuffer<T>` also had transferred write-lock releases tied to its stored `_vault` field.
What was done: render payload, placement+render payload, and debris build now use one combined mutation guard and mutate resolved native views. Remaining single-buffer write leases store `_writeLockVault` and release through it. Wreck guard release no longer falls back to current `_dataVault`.
Cinematic Cheats used: preserved the cheap BRG scatter/debris visual fake and continuous fragment caps; no physical wreck simulation expansion.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_vault.TryAcquireWriteLock/_vault.ReleaseWriteLock` and `_dataVault.TryAcquireMutationGuard/_dataVault.ReleaseMutationGuard` scans in `ProceduralWreckGenerator.cs` return no hits; stale nested render/debris lock flag scan returns no hits; `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 59 - Sargassum/Biome write-lock release tail

What was wrong: micro-fauna ring writes, cut stamp queues, global drag helper release, and biome telemetry write leases still had direct `_vault`/`_dataVault` write-lock acquire or release patterns.
What was done: micro-fauna uses a local vault lease; cut queue helpers return the granting vault to all release paths; global drag release helper snapshots `_dataVault`; biome telemetry stores `_telemetryWriteVault` for transferred write-lock release.
Cinematic Cheats used: none; existing cheap stamp queues, boid rings, and biome telemetry remain unchanged.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_vault/_dataVault.TryAcquireWriteLock` and `_vault/_dataVault.ReleaseWriteLock` scan over the four touched files returns no hits; `git diff --check` exits 0 with LF/CRLF warnings only; PCRE added-diff forbidden scan returns no hits. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 60 - AbyssalThermal write-lock release vaults

What was wrong: `AbyssalThermalManager` released thermal source, insulation, fill, and scratch-copy write locks through mutable `_dataVault`.
What was done: `TryAcquireThermalMapWriteBuffer` now returns the granting `IDataVault`; all successful write windows release through that captured vault.
Cinematic Cheats used: none; thermal source/insulation data and continuous quality behavior are unchanged.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped direct `_dataVault?.ReleaseWriteLock`/`_dataVault.TryAcquireWriteLock` scan in `AbyssalThermalManager.cs` returns no hits; `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 61 - SubmarineStructuralGrid lock flattening

What was wrong: structural current-phase writers mixed mutation guards with write locks, and job mutation guards released through current `_dataVault`.
What was done: structural current-phase writers now use resolved mutable views under the structural mutation guard; telemetry keeps one captured write lease; job guards store per-job granting vaults.
Cinematic Cheats used: none; hull diffusion, breach repair, fatigue, leak plume, and telemetry data are unchanged.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped stale write-lock, `ReleaseStructuralWriteLocks`, one-vault job unlock, and added diff forbidden-allocation/hot-lookup scans return no hits; `git diff --check` exits 0 with LF/CRLF warning only. No compile/import/profiler proof was run.

## 2026-05-29 Continuation 62 - DynamicDecal vault guard flattening

What was wrong: dynamic decal runtime paths held multiple DataVault write locks and released through static `_vault`.
What was done: decal buffer mutation now uses DataVault mutation guard bits plus fixed per-buffer granting-vault fields; releases clear and release the exact stored guard vault.
Cinematic Cheats used: preserved existing dynamic decal visual fake and continuous quality-weighted capacity/fade scaling.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: project runtime direct write-lock scan now reports only an editor validator string literal; direct field mutation-guard scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile/import/profiler proof was run.

Build throttle note: CPU sample was `56`; after a 30 second wait CPU sample was `100`. Compiler process scan found active `dotnet` PID `32068` running `dotnet build .\Hecton8.Core.csproj -nologo -v:minimal -maxcpucount:1 --no-restore /p:UseSharedCompilation=false`. No new build was launched.

## 2026-05-29 Continuation 63 - TopographicalSonar write-lock lease ownership

What was wrong: `TopographicalSonarSynthesizer` acquired UI DataVault write locks through `_dataVault`, but release sites called `ReleaseVaultWriteBuffer(_dataVault, ...)` again. A service rebind between acquire and release could leave the actual granting vault locked.
What was done: `TryAcquireVaultWriteBuffer` now returns `out IDataVault writeVault`; material LUT, telemetry ring/cursor, counter mirror, point mirror, indirect args, shader globals, editor CSV scratch, and editor LUT writes release through the captured lease vault.
Cinematic Cheats used: preserved the existing cheap topographical sonar SDF/material mirror and continuous quality-weighted ray/step cadence; no physical sonar simulation expansion.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: no `ReleaseVaultWriteBuffer(_dataVault, ...)` remains in `TopographicalSonarSynthesizer.cs`; all local helper acquires include `out IDataVault`; project runtime direct write-lock scan reports only an editor validator string literal; direct mutation-guard field scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warnings only; added diff forbidden-allocation/hot-lookup scan returns no hits.
Build throttle note: CPU sample returned `100`; no `dotnet/csc/VBCSCompiler` processes were listed, but CPU is above the `50%` compile throttle. No new build was launched.

## 2026-05-29 Continuation 64 - AsyncBuoyancyReadback write-buffer lease ownership

What was wrong: `AsyncBuoyancyReadbackRuntime` transferred physics DataVault write locks from `AcquireVaultWriteBuffer(_dataVault, ...)` and released them by rereading `_dataVault`.
What was done: `AcquireVaultWriteBuffer` now returns `out IDataVault writeVault`; requests, mock ring, completed requests, counters, resolved heights, result states, fallback waves, vehicle profiles, tuning, telemetry cursor/ring, and CSV scratch write windows release through captured vault leases.
Cinematic Cheats used: preserved async GPU readback plus cheap mock fallback; no physical water simulation expansion and no binary quality switch.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped `ReleaseVaultWriteBuffer(_dataVault, ...)` scan returns no hits; old-signature `AcquireVaultWriteBuffer(...)` scan returns no hits; project runtime direct write-lock scan reports only an editor validator string literal; direct mutation-guard field scan returns no hits; scoped `git diff --check` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits.
Build throttle note: CPU sample returned `96`; active `dotnet` PID `37024` is running `dotnet build .\Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1 --no-restore /p:UseSharedCompilation=false`. No new build was launched.

## 2026-05-29 Continuation 65 - Somatic/Autopilot/Flora write-lock lease tail

What was wrong: `SomaticKinematicsRuntime`, `SubmarineAutopilotSdfNavigator`, and `FloraInteractionManager` still had helper-level transferred write locks whose releases reread mutable `_dataVault`/`_wakeDataVault` instead of releasing through the service that granted the lock.
What was done: Somatic typed helpers now store per-buffer granting vaults returned by `TryAcquireSomaticWriteBuffer`; Autopilot write helper now returns `out IDataVault` and all target/profile/tuning/cold-default write paths release through it; Flora records granting vaults by BufferID for parasite/cascade/reactive/telemetry write locks and releases through that recorded vault.
Cinematic Cheats used: None changed; VR/KCC, autopilot SDF, parasite/cascade, flora wake/sway, and continuous `GlobalQualityWeight` behavior stayed intact.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: scoped stale release scans return no hits; project runtime direct `_dataVault/_vault/s_vault/_wakeDataVault` write-lock scan reports only `Assets/_Project/Scripts/Editor/QuestVrOptimizationValidator1406.cs:434` string literal; project runtime direct mutation-guard field scan returns no hits; added diff forbidden-allocation/hot-lookup scan returns no hits; `git diff --check` exits 0 with LF/CRLF warnings only. Build evidence: CPU precheck was 36 with no compiler rows, one throttled build was launched, timed out after about 904 seconds, left PID 20592, reached CPU sample 100, and was stopped; no compile success/failure is claimed.

## 2026-05-29 Continuation 66 - Flora cascade write-lock narrowing

What was wrong: flora cascade code held reactive/registered scratch write locks while entering cascade event/phase-seed update paths, and `RecomputeCascadePhaseSeeds` acquired event and phase-seed write locks in the same thread.
What was done: registered-handle scratch is released before phase-seed work; reactive query scratch is released before event registration/recompute; `RecomputeCascadePhaseSeeds` now compacts events under an event write lease, releases it, resolves events for read, and then acquires only the phase-seed write lease for scheduling.
Cinematic Cheats used: Preserved cheap phase-seed cascade visuals; no physical plant propagation simulation and no binary quality switch.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Evidence: nested-pattern scans for `TryAcquireCascadePhaseSeeds...TryAcquireCascadeEvents`, `TryAcquireReactiveFloraQueryHandles...RegisterCascadeEvent`, and `TryAcquireRegisteredReactiveFloraHandles...RecomputeCascadePhaseSeeds` return no hits; scoped `git diff --check -- FloraInteractionManager.cs` exits 0 with LF/CRLF warning only; added diff forbidden-allocation/hot-lookup scan returns no hits. No compile success is claimed.

## 2026-05-29 Continuation 67 - UI/VR telemetry and HUD queue write-lock releases

What was wrong: Foveated telemetry, HUD notification queue, and cockpit telemetry write locks could be released through current `_dataVault` instead of the vault that granted the lock.
What was done: Stored the granting `IDataVault` per active write lane, rejected nested active leases, and drained active write leases before lifecycle buffer release.
Cinematic Cheats used: Kept existing foveation/HUD/cockpit visual fakes and continuous quality scaling; no physical/UI simulation rewrite.
Exact Microseconds saved: 0 us measured; expected runtime delta 0 us. Evidence: release-helper `_dataVault` reread scan returns no hits; active lease guard/release line scan confirms stored-vault release sites. Static proof only; compile/import/profiler proof remains absent.

## 2026-05-29 Continuation 68 - MarauderOutpost write-buffer release ownership

What was wrong: `MarauderOutpostGenerationService` released successful outpost write-buffer leases through current `_dataVault`, not the vault that granted the lock.
What was done: Added fixed active granting-vault fields per outpost BufferID, rejected reentrant leases, and drained active writes before handle release.
Cinematic Cheats used: Kept WFC/shell render pipeline and bounded matrix visual path; no physical outpost simulation rewrite.
Exact Microseconds saved: 0 us measured; expected runtime delta 0 us. Evidence: `ReleaseWriteBuffer` no longer rereads `_dataVault`; targeted added-line scan shows only fixed lease fields/switches/release calls. Static proof only; CPU sampled 51, so no build launched.

## 2026-05-29 Continuation 69 - GasDynamics telemetry ring write-lock release

What was wrong: `GasDynamicsSolver` telemetry step write lock released through current `_dataVault` instead of the granting vault.
What was done: Stored `_telemetryRingStepVault` on successful telemetry write-lock transfer and released through it in `ReleaseTelemetryRingStepLock`.
Cinematic Cheats used: Kept analytical gas/hibernation fake and bounded telemetry; no atmosphere simulation expansion.
Exact Microseconds saved: 0 us measured; expected runtime delta 0 us. Evidence: added-diff forbidden scan returns no hits; line scan confirms stored-vault acquire/release. CPU sampled 87, so no build launched.

## 2026-05-29 Continuation 70 - Residual write-lock lease release ownership

What was wrong: `ProximityColliderSystem`, `WreckMaterialRegistry`, `HectonCaveVoxelLightingVolume`, `GPUScatterDirector`, `OpenXRManualOverrideLever`, and the dormant sargassum density-build release helper still had release paths tied to current `_dataVault` instead of the granting vault.
What was done: Added stored granting-vault fields, reentrant active-lease guards, and lifecycle drains before buffer release.
Cinematic Cheats used: Kept existing cheap proximity, BRG, cave SDF, scatter, lever, and sargassum paths; no physical simulation expansion.
Exact Microseconds saved: 0 us measured; expected runtime delta 0 us. Evidence: scoped direct `_dataVault.ReleaseWriteLock/_dataVault.TryAcquireWriteLock` scan over touched files returns no hits; added-diff forbidden scan returns no hits. CPU sampled 77, so no build launched.

## 2026-05-29 Continuation 71 - BiomeBoundary mutation/write lease ownership

What was wrong: `BiomeBoundarySdfRuntime` released transferred mutation guards through current `_dataVault`, and telemetry write acquisition lacked an active-lease guard.
What was done: Stored granting vaults for biome-map guard, sample guard, and telemetry write-lock; release helpers now take/clear stored vaults; teardown drains active leases before releasing handles.
Cinematic Cheats used: Kept cheap deterministic biome SDF sample and bounded telemetry; no heatmap or gradient signal rewrite.
Exact Microseconds saved: 0 us measured; expected runtime delta 0 us. Evidence: `git diff --check` exits 0 with LF/CRLF warnings only; line inventory confirms stored-vault release helpers. CPU sampled 77, so no build launched.

## 2026-05-29 Continuation 72 - Hot dependency lookup audit

What was wrong: The integrator mandate required proof that hot paths do not poll dependencies through `GlobalRegistry.Get<T>()` or direct `GetComponent()`.
What was done: Scanned runtime scripts for `GlobalRegistry.Get<T>` and direct non-`Try` `GetComponent(`. `GlobalRegistry.Get<T>` has no hits. Direct `GetComponent(` hits are editor authoring/repair plus `ImpostorSystem` editor MenuItem batch bake scan, not runtime Tick/Fixed/LateFrame/Execute.
Cinematic Cheats used: No system behavior changed.
Exact Microseconds saved: 0 us measured. Evidence: `GlobalRegistry.Get<T>` scan exit 1/no hits; direct `GetComponent(` inventory limited to editor/cold authoring paths. No build launched because CPU sampled 77.

## 2026-05-29 Continuation 73 - Static integrator verification after residual lease patch

What was wrong: The remaining risk was unverified source drift after the residual lease patches: hot dependency lookup, same-frame job completion, direct mutable-vault write locks, direct mutable-vault mutation guards, and forbidden added hot allocations.
What was done: Reread NativeMemory, ZeroGC, ARM64 layout, GlobalRegistry, ExecutionPhases, CinematicCheat, Performance, and PostMortem mandates. Hot-method scans found no runtime direct `GlobalRegistry.Get<T>()`, no runtime direct non-`Try` `GetComponent(`, no runtime `.Complete()` in Tick/Fixed/LateFrame/Execute paths, and only comment/editor false positives for `GlobalRegistry.DataVault`. Project direct `_dataVault/_vault/s_vault/_wakeDataVault.TryAcquireWriteLock/ReleaseWriteLock` scan reports only an editor validator string literal; direct mutation-guard field scan returns no hits. Seven residual lease files pass `git diff --check` with LF/CRLF warnings only, and added-line ZeroGC/hot-lookup scan returns no hits.
Cinematic Cheats used: No new simulation was added. Ownership/lifecycle hardening preserved the existing cheap deterministic presentation paths and continuous quality scaling.
Exact Microseconds saved: 0 us measured. Static audit estimate: 3800 us. Build note: CPU later sampled 20 with no compiler rows, so one throttled `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1 --no-restore /p:UseSharedCompilation=false` was launched. It exited `-1` after 00:03:06.7 with no captured diagnostics; follow-up compiler scan returned no rows and CPU sampled 68. Compile/import/profiler proof is not accepted.

## 2026-05-30 Continuation 74 - Native ownership and hot-path allocation tail

What was wrong: stored-vault release helpers still had unsafe `storedVault ?? currentVault` fallbacks in audio/subtitle/foam routes; foam write buffers released through mutable `_vault`; profiler/pause diagnostics allocated strings in hot diagnostic paths; multiple AsyncGPUReadback persistent arrays and forensics snapshot buffers were invisible to `NativeMemorySentinel`; `SaveData` carried a borrowed `NativeArray<byte>` from `PlayerInventory`.
What was done: `AudioLogSystem` and `BabelSubtitleSyncRuntime` now release mutation guards only through stored granting vaults. `JacobianFoamGpuRuntime` now captures write-lane vaults for params/tuning/wakes/telemetry and releases read pins only through stored vaults. `RuntimePerformanceProfiler` and `PauseSystemVerifier` use prebuilt trace/log constants for the reported hot paths. Persistent readback/snapshot arrays now register/unregister with `NativeMemorySentinel` in the affected GPU/readback/forensics owners. `SaveData.inventoryShadowPayload` is now a managed byte snapshot, copied by `PlayerInventory` and written by `SaveBinaryPayloadCodec`.
Cinematic Cheats used: no physical simulation expansion. Existing foam, scatter, culling, underwater, sargassum, buoyancy, and save routes keep their current cheap deterministic staging/readback paths and continuous `GlobalQualityWeight` behavior.
Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Static proof: combined `git diff --check` over 18 source files exits 0 with LF/CRLF warnings only; added-diff hot lookup/job barrier scan returns no hits; project `GlobalRegistry.Get<T>` scan returns no hits; project direct mutable-vault write/mutation scan reports only `Editor/QuestVrOptimizationValidator1406.cs:434` string literal; stale borrowed `NativeArray<byte> inventoryShadowPayload` scan returns no hits. Compile proof blocked: CPU sampled 100 with active `dotnet` PID 56788, so no build/import/profiler lane was launched.

## 2026-05-30 Continuation 75 - Granting-Vault Fallback Sweep

What was wrong: active DataVault mutation guards and write/readback guard windows still had `storedGuardVault ?? _dataVault/_vault` fallback paths. That is not a recovery mechanism. It can release a guard on a newly rebound vault and leave the original granting vault locked; scheduled completion can also read/commit through a current vault that never granted the job window.

What was done: removed active fallback releases across 47 runtime files. The changed routes now release or finalize only through the stored granting `IDataVault`: ladder IK, base atmosphere logistics, procedural bone blender, foundation pylon batch, sump pump grid, habitat fluid incursion, buoyancy displacement, procedural ore, chemical influence, plasma beam, ambient biota, foveated simulation, haptic synthesis, thermal DRS, macro ecosystem, nutrient drift and carrion, volcanic updraft, parasite telemetry, hazard exposure, procedural field sampling, stress spawning, seismic/celestial tide, director AI, structural integrity, visual pressure aging, suit integrity, physics apply validation, submarine dynamics/autopilot/atmosphere/structure, hand IK, loot magnet, debris, fauna terrain SDF, proximity collider, tether mock jobs, seaglide, exosuit, KCC, QA watchdog, spatial audio, reactor bridge, ground radar, abyssal thermal, and ecosystem director.

Cinematic Cheats used: none changed. Existing cheap approximations, continuous `GlobalQualityWeight`, and phase ownership were preserved. This pass bought stability, not new simulation.

Exact Microseconds saved: 0 us measured; expected steady-frame delta 0 us. Deadlock/rebind recovery risk reduced. Static checks: no runtime `GlobalRegistry.Get<T>` hits; direct non-`Try` `GetComponent(` hits are editor authoring/repair and editor impostor bake only; hot-method scan near `Tick`, `FixedUpdate`, `LateFrameTick`, and `Execute` found no `GlobalRegistry.DataVault`, `GlobalRegistry.Get<T>`, direct `GetComponent`, or `.Complete()` hit; direct mutable-vault write/mutation scan reports only an editor validator string literal; remaining granting-vault fallback is cold `WorldChunkResidencyManager` sentinel/lifecycle context. `git diff --check -- Assets/_Project/Scripts` exits 0 with LF/CRLF warnings only.

Compile proof: not accepted. Latest CPU sample was 73 with no compiler process rows, so no `dotnet build`, Unity import, Play Mode, profiler, GCMonitor, or native ledger was launched under the >50% CPU throttle.
