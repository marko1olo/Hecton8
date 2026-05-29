# COMPILE_MEDIC Log

## 2026-05-27 Session Start

What was wrong: Latest dotnet compile errors/warnings requested; exact error set not yet extracted.
What was done: Loaded governing AGENTS/domain documents and six relevant mandates; created operational status/rationale/log files because no matching compile-medic XML prompt exists in current batch.
Cinematic Cheats used: None; compile repair only at this stage.
Exact Microseconds saved: Build spam avoided; no measured runtime microseconds claimed.

## 2026-05-28 Compile Closure

What was wrong: Fresh dotnet logs had live first-party compile failures in combat/vault and GPU upload slices, plus generated Unity CLI project graph drift. The latest warning found `Tayx.Graphy.Editor.csproj` pulling unrelated editor assemblies from `Library\ScriptAssemblies`, producing `MSB3277` on `System.Net.Http` and vendor `CS0169`. A monolithic `Hecton8.slnx` build stalled after `GPUInstancer.Editor`, so the solution tail had to be proven project-by-project.
What was done: Fixed the active first-party compile faults, kept stale underwater/submarine log entries out of source, added `Tayx.Graphy`/`Tayx.Graphy.Editor` to the existing vendor generated warning/reference-prune policy, and built all 62 projects from `Hecton8.slnx` individually. Current proof matrix: missing=0, warnings=0, errors=0. MapMagic runtime/editor and Crest runtime/editor C# builds are green; no current source diff remains under `Assets/MapMagic` or `Assets/Crest` in this workspace.
Cinematic Cheats used: None in runtime code. The only domain decision was compile graph surgery over vendor source churn; Unity Editor shader/graph import remains outside dotnet proof.
Exact Microseconds saved: Runtime: 0 claimed. Build/diagnostic waste avoided by replacing repeated hung full-solution attempts with 62 deterministic targeted builds; measured verification wall time for the final target matrix was about 2,890,700,000 us.

## 2026-05-28 Strict Core/Post-Strict Closure

What was wrong: Strict Core audit found 760 `CS0436` duplicate-type warnings from stale `Library/ScriptAssemblies` DLLs masking source-owned Hecton contracts. Removing that mask exposed real current errors: missing XR namespace in brownout render code, missing flocking dump path constant, localization key hash signedness mismatch, and wrong Sargassum threat-grid vault view type. Follow-up `Hecton8.Editor` and `Assembly-CSharp` passes found Modding SDK static/instance state misuse, namespace shadowing of `System.Environment`, DTO field initialization warnings, and one dead Candice private field.
What was done: Pruned stale Core references for Bootstrap.Contracts, World.Contracts, BatteryChargerLogistics, UI.Navigation, Lighting, and Core.Database; fixed the active source errors; changed Sargassum upload to typed `NativeArray<uint>` write-lock ownership; made starter-kit generation return a summary string owned by the calling window; fully qualified `global::System.Environment`; initialized Modding SDK JSON DTOs; removed the unused Candice field. Rebuilt all 62 `Hecton8.slnx` projects individually after the strict pass. Final matrix artifact: `Docs/Reports/BUILD_COMPILE_MEDIC_TARGET_POST_STRICT_20260528_1.csv`, result projects=62, missing=0, warnings=0, errors=0. Strict Core proof: `Docs/Reports/BUILD_COMPILE_MEDIC_CORE_STRICT_GRAPH_STRICT_20260528_1.log`, 0/0.
Cinematic Cheats used: None. This was compiler graph and contract repair only. MapMagic/Crest status: no git diff under `Assets/MapMagic`, `Assets/Crest`, or `Assets/_Project/Scenes`; MapMagic, MapMagic.Editor, Crest, and Crest.Helpers.Editor C# logs are 0 warnings and 0 errors. Actual Unity graph/shader import still requires Unity Editor validation, not dotnet.
Exact Microseconds saved: Runtime: 0 claimed. Build proof cost for the post-strict 62-project matrix was about 1,582,700,000 us. The fix removes stale binary ambiguity and warning debt; no hot-path polling, heap allocation, or gameplay authority route was added.

## 2026-05-28 XRPass/GPUInstancer Follow-Up

What was wrong: Latest recheck found two current compile breaks after the prior matrix: `Hecton8.Core` could not resolve `UnityEngine.Experimental.Rendering.XRPass` in the CLI fallback graph, and `Hecton8.Editor` failed through `GPUInstancer.csproj` because `detailMapData` is `List<int[]>` while a guard used `.Length` on the list. A fresh strict warning audit also corrected the prior report: strict audit now has 0 errors but 904 warnings when normal warning policy is disabled.
What was done: `Directory.Build.targets` now removes the wildcard SRP Core runtime item from `Hecton8.Core` and adds `Unity.RenderPipelines.Core.Runtime` by explicit `HintPath`. `GPUInstancerDetailManager` now uses `cell.detailMapData.Count` for the list boundary and keeps array `.Length` checks for layer payloads. Verification logs: `BUILD_COMPILE_MEDIC_CORE_NORMAL_XRPASS_REF_20260528_2.log`, `BUILD_COMPILE_MEDIC_GPUINSTANCER_DETAILMAP_COUNT_20260528_1.log`, `BUILD_COMPILE_MEDIC_EDITOR_NORMAL_XRPASS_REF_20260528_2.log`, `BUILD_COMPILE_MEDIC_ASSEMBLY_CSHARP_NORMAL_XRPASS_GPUINSTANCER_20260528_1.log`, `BUILD_COMPILE_MEDIC_ASSEMBLY_CSHARP_EDITOR_NORMAL_XRPASS_GPUINSTANCER_20260528_1.log`, MapMagic runtime/editor rechecks, and Crest runtime/editor rechecks all show 0 warnings and 0 errors.
Cinematic Cheats used: None. These were reference-route and container-contract repairs. MapMagic/Crest: no git diff under `Assets/MapMagic`, `Assets/Crest`, or `Assets/_Project/Scenes/02_HECTON_WORLD.unity`; C# boundaries are green. Actual graph/import validation still needs Unity Editor, not dotnet.
Exact Microseconds saved: Runtime: 0 claimed. Verification wall time for the affected normal builds was about 811,000,000 us. No hot-path allocation, polling, gameplay authority route, or quality tier behavior was changed.

## 2026-05-28 Final Dirty-Workspace Recheck

What was wrong: Parallel rebuild/edit churn produced stale or intermediate compile signals. The latest live `Hecton8.Editor` failure reported `ModuloSimulationBucketer.ReleaseRebalanceBufferPins` missing, while the settled source already contained that method. Earlier `ModularEquipmentEngine` ref-safety and `BallisticsRuntime` namespace-level job constant errors were real. Crest water files are currently modified; MapMagic and `02_HECTON_WORLD.unity` are not.
What was done: Fixed Modular lock acquisition counting by moving `acquiredCount++` to the caller after each successful write-view acquisition. Fixed Ballistics ricochet ownership by using `BallisticsRuntime.MaxRicochetsPerTrajectory` from the namespace-level job. Waited for external `dotnet/csc` work to finish, rebuilt `Hecton8.Core` and `Hecton8.Editor`, and rebuilt the affected boundary matrix: `Assembly-CSharp`, `Assembly-CSharp-Editor`, `Crest`, `Crest.Helpers.Editor`, `MapMagic`, `MapMagic.Editor`, `MapMagic.Settings`, `MapMagic.MicroSplat`, and `MapMagic.MicroSplat.Editor`. Proof artifacts: `BUILD_COMPILE_MEDIC_CORE_FINAL_AFTER_BUCKETER_20260528_1.log`, `BUILD_COMPILE_MEDIC_HECTON8_EDITOR_FINAL_AFTER_BUCKETER_SUMMARY_20260528_1.log`, and `BUILD_COMPILE_MEDIC_TARGETED_FINAL_AFTER_BUCKETER_20260528_1.csv`; all normal targets are 0 warnings and 0 errors. Strict Core audit `BUILD_COMPILE_MEDIC_CORE_STRICT_AUDIT_AFTER_BUCKETER_20260528_1.log` is 0 errors and 904 warnings.
Cinematic Cheats used: Crest changes are GPU boundary guards and dispatch sizing, not physical simulation rewrites. No MapMagic graph or scene asset was edited. Shader/import proof is not available from dotnet; Unity Editor validation is still required for Crest compute shader import and serialized graph/runtime water behavior.
Exact Microseconds saved: Runtime: 0 claimed. Core proof wall time: 27,210,000 us. Hecton8.Editor proof wall time: 239,910,000 us. Targeted 9-project matrix wall time: 326,500,000 us. Strict audit wall time: 71,840,000 us. No new hot polling, heap allocation, scene search, or gameplay truth route was added.

## 2026-05-28 APEX Final Verification

What was wrong: The prior verbal completion state was insufficient. Fresh late logs still contained real current errors in Acoustic vault handle validation, battery snapshot definite assignment, retina pass disposal, Candice 2D filter/ref contracts, Tether smooth range math, and SumpPump warning cleanup. Some scary Core failures were stale products of parallel edits and contradicted by current source.
What was done: Repaired the live clusters, rebuilt the affected targets, and created proof artifacts on disk. Final consolidated dotnet C# proof: `Docs/Reports/APEX_COMPILE_MEDIC_TARGETED_CONSOLIDATED_20260528_1.csv`, 9 targets, exit=0, warnings=0, errors=0. Zero-GC scan: `Docs/Reports/APEX_COMPILE_MEDIC_ZERO_GC_SCAN_20260528_3.json`, managed `referenceNew=0`, `string.Format=0`, `.ToString()=0`, LINQ=0, `foreachLoop=0`; Crest `FFTCompute.cs:280` remains a recorded cache-miss managed constructor. Final report: `Docs/Reports/APEX_FINAL_VERIFICATION_COMPILE_MEDIC_20260528.json`; SHA-256 `1F026FC0C50FEE82C2F494A55A8659CB58420FA2F30C37CD7376D511C335029D`.
Cinematic Cheats used: No new physical simulation was introduced. Ballistics uses continuous `HomeostasisBrain.GlobalQualityWeight` for signal/primitive/ricochet budgets. Tether uses continuous `SmoothRange01`. Crest/MapMagic were not rewritten or graph-edited; their C# targets compile green, but shader/import/runtime water and serialized MapMagic graph behavior still require Unity Editor validation.
Exact Microseconds saved: Runtime: 0 claimed. Late targeted proof wall time recorded from latest green isolated logs: Core 125,770,000 us, Assembly-CSharp 38,570,000 us, plus matrix tail logs from 20260528_4. CPU guard samples before builds included CPU=49/47/36/40/46/30/19/48/34 with compilerProcessCount=0. No `Docs/AgentLogs/Dump_COMPILE_MEDIC.bin` exists because no crash/NaN dump was emitted; no runtime black-box proof is claimed.
## 2026-05-28T22:37:42+04:00 - Post-APEX Shinobu BufferID repair

What was wrong:
- Latest failing Core log `Docs/Reports/APEX_COMPILE_MEDIC_CORE_GETENTITYID_RECHECK_20260528_1.log` reported 0 warnings and 51 errors.
- Current-source comparison shows `StressDrivenSpawnDirector.cs` and `PowerGrid.cs` errors from that log are stale parallel edit states.
- Live current-source error: `ShinobuEcosystemBalancer.FlockingAvoidance.cs` still used old `TryOpenVaultView` calls without expected `BufferID`.

What was done:
- Patched `Assets/_Project/Scripts/AI/Ecosystem/ShinobuEcosystemBalancer.FlockingAvoidance.cs:37-40,144-145`.
- Added `BufferID.ShinobuFlockingThreats`, `BufferID.ShinobuFlockingThreatCount`, `BufferID.ShinobuFlockingCounters64`, and `BufferID.ShinobuFlockingTelemetryRing` to existing read-view calls.
- Created `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json`.
- SHA-256: `DEF43BC479ABB4D42DCD2F536E719E9AB258D197934488E372D13FF1BE3B9A67`.

Cinematic Cheats used:
- None. This is compile/DataVault identity repair only.

Exact microseconds saved:
- Runtime: 0 claimed.
- Build launched: no. CPU guard blocked it with samples 57/76/88/60/65/100 percent and compiler process count 0.

Residual fault:
- Status remains `PENDING_VERIFICATION` until guarded `dotnet build Hecton8.Core.csproj -nologo --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` can run under CPU <= 50 percent and no compiler process.

## 2026-05-28T22:48:00+04:00 - LogisticsPipeNode obsolete identity cleanup

What was wrong:
- Static first-party scan found two remaining runtime `GetInstanceID()` calls in `Assets/_Project/Scripts/Construction/LogisticsPipeNode.cs:550,556`.

What was done:
- Replaced both calls with `EntityId.ToULong(crate.GetEntityId())` folding.
- Re-ran `rg "GetInstanceID\\(|enableWordWrapping" Assets/_Project/Scripts -g "*.cs"`; result: 0 hits.
- Updated `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json`.
- SHA-256: `1ABC36D53628DE8311B6FCCCA4C851F03E136643F710E0742BA53B43D7486EA0`.

Cinematic Cheats used:
- None. This is API compatibility cleanup only.

Exact microseconds saved:
- Runtime: 0 claimed.
- Build launched: no. Additional CPU guard samples were 99/71/88/100 percent with compiler process count 0.

Residual fault:
- Status remains `PENDING_VERIFICATION`; no post-LogisticsPipeNode compile proof exists.

## 2026-05-28T23:56:40+04:00 - Post-resume verification gate update

What was wrong:
- Post-patch compile verification is still blocked by the local resource throttle. CPU samples were 100%, 100%, and 100%; compiler process count remained 0.

What was done:
- Re-ran static scans while build was gated.
- `GetInstanceID(`/`enableWordWrapping` remains 0 hits in `Assets/_Project/Scripts`.
- Sampled global-system hits classify as editor/authoring/diagnostic/crash or dispatcher fence paths in the scanned output.
- Refreshed `Docs/Reports/APEX_COMPILE_MEDIC_POST_SHINOBU_BUFFERID_AUDIT_20260528.json`.
- SHA-256: `F43CDECCA929138BA9FA6C19A01517AADCEA642E92D266EEADCC32CB4B2D52D4`.

Cinematic Cheats used:
- None. This is verification-only work.

Exact microseconds saved:
- Runtime: 0 claimed.
- Build launched: no. CPU remained above the 50% gate.

Residual fault:
- Status remains `PENDING_VERIFICATION`; no post-Shinobu/Logistics compile proof exists.

## 2026-05-29 APEX Integrator Source Verification

Wrong: `HazardZoneManager.ScheduleExposureJob` previously held multiple DataVault writer fences across job scheduling. The first read-alias repair attempt exposed a deeper `GlobalDataVault.PinReadOnlyAlias<T>` defect: aliases were published as external views, but the public release route was the counted `TryUnlockBuffer` pin path.
Done: `GlobalDataVault.PinReadOnlyAlias<T>` now uses owner-tagged counted pins (`TryLockBuffer`/`TryUnlockBuffer`) and resolves a generation handle before returning a read-only alias. `HazardZoneManager.ScheduleExposureJob` now releases the `_jobVolumes` writer fence in `finally` before acquiring the result writer fence; job input buffers are read-only pinned aliases released after `LateFrameTick` finalization. `TryAcquireHazardStateWriteViews` now uses `HazardStateMutationGuardMask` and mutable resolves instead of four simultaneous writer fences.
Cinematic cheat used: no new physical simulation; the change preserves the cheap snapshot-and-evaluate exposure model instead of adding a richer spatial solve.
Verification: added-line scan reports zero `new` reference-constructor text, `string.Format`, `.ToString()`, LINQ, `foreach`, `GlobalRegistry.Get`, and `GetComponent` hits. Full hot-method lexical scan reports `HOT_LOOKUP_HITS=0`. `git diff --check` reports no whitespace errors, only existing LF-to-CRLF warnings.
Compile throttle: no `dotnet build` launched; CPU sample was 100 percent and compiler process count was 0. Status remains `PENDING_VERIFICATION`.
