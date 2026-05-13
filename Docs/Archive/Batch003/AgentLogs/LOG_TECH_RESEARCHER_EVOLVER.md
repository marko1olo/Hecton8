# LOG: TECH_RESEARCHER_EVOLVER

## 2026-05-13 - Mandate Researcher Purge Audit

What was wrong:
- Mandate registry inventory was stale: README claimed the wrong total, while the directory contained 75 mandate `.txt` files.
- 64 of 75 mandate `.txt` files lacked an `Engineering Data` evidence table.
- Unity 6000 guidance was incomplete in the mandates: RenderGraph, Awaitable, GPU Resident Drawer, Addressables 2.7 residency, TMP char-buffer UI, NativeMemorySentinel, Burst 1.8 SIMD limits, logistics/thermodynamics authority, EventBus state propagation, and AUP drift telemetry needed hard language.
- Static runtime audit found no core/runtime `IEnumerator`, `StartCoroutine`, or `yield return` infections outside Editor tooling.
- Static contract audit found one deletion candidate: `IScatterSimulationBackendProvider`, because usage appears confined to the registry declaration surface.
- Singleton audit found static `Instance` facade debt still present, mostly GlobalRegistry-backed compatibility surfaces. `AsyncLoadHelper.Instance => null` is a purge candidate; `EnumFastComparer<T>.Instance` is utility-cache shape, not MonoBehaviour singleton state.
- `Hecton8.Core.asmdef` does not directly reference `Hecton8.UI`, but it is still not shattered: it references broad domain/package surfaces including UI packages, URP, Addressables, Input, Logistics, Cartography, GPUInstancer, VolumetricLightBeam, TextMeshPro, and UnityEngine.UI.

What was done:
- Extracted `<AGENT_PROMPT id="TECH_RESEARCHER_EVOLVER">` from `Docs/Tasks/CURRENT_BATCH.md` by CLI and ignored neighboring prompt blocks.
- Created status, rationale, and recon files for TECH_RESEARCHER_EVOLVER.
- Audited all 75 mandate `.txt` files and updated `.agents-skills/README.md` to the correct registry count.
- Added Engineering Data sections to every mandate that lacked one; final validation returned `TOTAL_TXT=75` and `MISSING_ENGINEERING_DATA=0`.
- Upgraded the render mandate with `RecordRenderGraph`, compatibility-mode ban, declared resource lifetime gates, GPU Resident Drawer selection rules, and official Unity source URLs.
- Upgraded async mandate to prefer `Awaitable`/`Awaitable<T>` for Unity object orchestration and ban per-request `Task.Run` in hot/runtime object paths.
- Upgraded flora/render mandates with GPU Resident Drawer ownership rules.
- Upgraded Addressables mandate with `AssetLoadMode.RequestedAssetAndDependencies` residency law and restrictions on broad `AllPackedAssetsAndDependencies` usage.
- Upgraded UI zero-GC mandate with `ReadOnlySpan<char>`, `Span<char>`, `TMP_Text.SetCharArray`, and explicit bans on string-producing HUD/update paths.
- Upgraded native memory mandate with NativeMemorySentinel registration law for NativeContainers, raw `UnsafeUtility.Malloc`, queues, arenas, and staging allocations.
- Upgraded Burst math mandate with local Burst 1.8.28 SIMD boundaries: v64/v128/v256 only, v128 baseline, v256 gated for High/Ultra, no v512/Burst 2.0 claims.
- Added logistics/thermodynamics ownership boundaries to logistics and flow mandates.
- Created `Docs/PROJECT_ATLAS.md` and root `PROJECT_ATLAS.md` from current asmdef graph evidence.
- Added MX350/i3 hard gate rows to `Docs/QUALITY_GATES.md`.
- Added EventBus-vs-GlobalRegistry state update law to the GlobalRegistry mandate.
- Added 300-frame AUP snap-fence telemetry law to the coordinate precision mandate.
- Ran final OMEGA scoped diff/token audit. Hits were forbidden-pattern examples in mandate text only, not runtime code.

Cinematic Cheats used:
- RenderGraph mandate now requires declared resources and frame-data handles instead of legacy hidden render passes.
- GPU Resident Drawer is the default selection candidate for MeshRenderer-heavy static flora/props when package evidence and capture proof exist.
- Addressables residency now favors requested asset plus dependencies instead of broad bundle-resident claims.
- UI mandate forces char-buffer writes so display updates buy visual richness without per-frame managed strings.
- Logistics/thermodynamics boundary forces dirty summary/event handoff instead of per-frame cross-domain polling.
- AUP drift proof now uses a fixed 300-frame telemetry ring rather than unverifiable single-frame claims.
- Flow/weather mandate now treats heat influence as a read-only visual input, enabling cheap VFX/current fakes instead of authoritative thermal simulation.

Exact Microseconds saved:
- Measured runtime savings: 0 us. This pass changed documentation, mandates, and audit files only.
- Purge verification savings: 0 us immediate; no runtime coroutine code was edited because the scan was already clean outside Editor.
- Singleton purge savings: 0 us immediate; facade debt was reported, not removed.
- RenderGraph/GPU Resident Drawer/Addressables/UI/Burst/NativeMemory savings: PENDING PROFILER after subsystem owners implement the enforced rules.
- Workflow savings: not measured in runtime microseconds; mandate enforcement reduces future compile/review churn but is not a frame-time metric.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore` failed on the first attempt with 24 global C# errors outside TECH_RESEARCHER documentation edits.
- Later compile verification was blocked by active external dotnet/MSBuild build lanes in the shared workspace.
- Final status is PENDING VERIFICATION / BLOCKED BY DEPENDENCY, not green.

## 2026-05-13 - Continuation Static Quality Correction

What was wrong:
- The first atlas version missed first-party assemblies that existed in the current project. A later recheck also caught concurrent workspace additions: `Hecton8.Core.Memory`, `Hecton8.Core.Time`, `Hecton8.Lighting`, `Hecton8.Physics.Determinism`, `Hecton8.QA`, `Hecton8.QA.Editor`, `Hecton8.EditModeTests`, and `Hecton8.PlayModeTests`.
- `Docs/QUALITY_GATES.md` contained a historical Core build-success artifact in language that could be misread as current source health.
- MX350 VRAM and physics rows mixed guard thresholds with hard-block thresholds.

What was done:
- Re-scanned `Assets/_Project/**/*.asmdef` without running a build.
- Updated `Docs/PROJECT_ATLAS.md` and root `PROJECT_ATLAS.md` to 24 first-party assemblies with `MISSING_IN_ATLAS=0`.
- Marked `Hecton8.Lighting` and `Hecton8.QA` direct Core dependencies as architecture debt.
- Reworded the old 2026-05-11 build artifact as historical evidence only and added the current `PENDING VERIFICATION` boundary.
- Clarified VRAM guard `1.6GB` vs hard ceiling `1.8GB`, and physics planning gate `2.0ms p95` vs absolute spike ceiling `5.0ms`.

Cinematic Cheats used:
- Quality gates now require physics overload response through noncritical body reduction, solver scope reduction, or visual fake replacement before accepting merge risk.
- VRAM guard wording requires load-shed evidence instead of pretending half-res rendering buys memory headroom.

Exact Microseconds saved:
- Measured runtime savings: 0 us. No runtime C# changed.
- Preventive savings: PENDING PROFILER. The change prevents stale acceptance gates and incomplete assembly graph decisions.

Verification:
- `dotnet build` was not launched per user constraint.
- Static atlas coverage compare returned `ASMDEF_COUNT=24`, `ATLAS_H8_ROWS=24`, `MISSING_IN_ATLAS=0`.
- `git diff --check` reported no whitespace errors for the patched docs; it only repeated existing LF-to-CRLF working-copy warnings.

## 2026-05-13 - Voxel Mandate Source Alignment

What was wrong:
- Voxel mandates were stale against current source. They still treated runtime density as `NativeArray<half>` and declared Transvoxel primary while forbidding Marching Cubes.
- Current `HectonVoxelEngine.cs` is static source evidence for Marching Cubes, float working-density buffers, sbyte quantized extraction, and a 300-entry voxel mesh pipeline Black Box.

What was done:
- Updated `VOX_Voxel_SDF_Geometry_MarchingCubes_Pipeline.txt` with a current source boundary: float working density, sbyte quantized MC extraction, MC primary, Black Box dump path.
- Updated `VOX_Voxel_World_Logic_Carving_Persistence.txt` so Transvoxel is a future migration target, not current law, and Marching Cubes is not rejected.
- Updated `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt` to target current first-party `HectonVoxelEngine` instead of Digger/VoxelPro assumptions.

Cinematic Cheats used:
- Preserved current MC/skirt/transition-strip path as the cheap stable baseline.
- Kept Transvoxel/fp16 as visual/perf upgrade paths only after seam captures and MX350 profiler proof.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Runtime code unchanged.
- Preventive savings: PENDING PROFILER. Avoids mandate-driven rewrite churn and protects current fallback architecture.

Verification:
- `dotnet build` was not launched.
- Evidence class is STATIC_SOURCE plus STATIC_DOC only.

## 2026-05-13 - Player/Pipe Static Recheck

What was wrong:
- `HectonPlayerMovement.cs` is coroutine-clean by static scan, but hot-path helper chains still read `GlobalRegistry.*` service properties from `Tick`/`FixedTick` paths. That is architecture debt even when the property is cheap.
- `HectonPlayerMovement.cs` has extensive mojibake in comments and serialized `[Header]` strings. This is maintainability/inspector debt for the player owner, not a runtime frame-time defect.
- Pipe logistics needed a fresh static read because the open status file claimed completion while old mandates still imply stricter logistics-worker shape than current source proves.

What was done:
- Scanned `HectonPlayerMovement.cs` for coroutine, scene-search, string conversion, component lookup, logging, registry, telemetry, and mojibake patterns.
- Scanned pipe logistics files for Unity loop methods, coroutine/string/search violations, dispatcher ownership, NativeMemorySentinel coverage, pump cadence, and Black Box dump behavior.
- Added `RULE-09C` to `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`: `GlobalRegistry.*` static convenience properties are now under the same hot-path cache law as `GlobalRegistry.Get<T>()`.
- Logged player and pipe findings to `Docs/AgentLogs/RECON_TECH_RESEARCHER_EVOLVER.md`.
- No runtime C# files were edited by TECH_RESEARCHER_EVOLVER.

Cinematic Cheats used:
- The mandate now pushes player/environment service data toward cached snapshots and dirty signals, leaving per-frame budget available for water response, VR comfort smoothing, and high-tier visual overkill.
- Pipe logistics remains a graph/job/visual-proxy shape: pumps and pressure are scalar cadence logic, while pipe flow visuals stay decoupled from physics.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Runtime code unchanged and no profiler run executed.
- Preventive savings: PENDING PROFILER. Expected impact is microsecond-level reduction after owning agents cache player hot-path services; the stronger gain is removal of hidden cross-domain polling.

Verification:
- `dotnet build` was not launched per user constraint.
- Static scan found no `StartCoroutine`, `IEnumerator`, `yield return`, scene search, `.ToString()`, or `string.Format` hits in `HectonPlayerMovement.cs`.
- Focused pipe logistics scan found no `Update`, `FixedUpdate`, coroutine, string conversion, or scene-search violations in the audited pipe files.

## 2026-05-13 - Logistics Mandate Source Alignment

What was wrong:
- `.agents-skills/LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt` still said `LogisticsTick` never runs on the main thread and broadly forbade MonoBehaviours per pipe/cable segment.
- Current source uses MonoBehaviours as cold adapters (`FluidPipeGraphRuntime`, `LogisticsPipeNode`, `WaterPumpModule`) while the transport/pressure truth remains in shared scheduler, native buffers, jobs, and telemetry.

What was done:
- Added a pipe runtime adapter boundary section to the logistics mandate.
- Kept the hard rule that graph/pressure solve is Burst/job/native and not main-thread truth.
- Allowed main-thread cadence only for Unity-owned object integration, job scheduling/completion gates, signal publication, and visual proxy dirties.
- Replaced the broad MonoBehaviour ban with a precise ban on per-segment simulation/update loops.
- No runtime C# files were edited.

Cinematic Cheats used:
- Pipe visuals remain shader/dirty proxy output; scalar pressure/resource truth stays detached from physics and per-segment components.
- Saved CPU budget remains available for higher-tier flow visuals instead of component-loop simulation.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Documentation/mandate edit only.
- Preventive savings: PENDING PROFILER. The change prevents false rewrites and keeps owners focused on job/native pressure solve performance.

Verification:
- `dotnet build` was not launched per user constraint.
- Static text checks confirmed the stale main-thread and MonoBehaviour wording was replaced with graph-solve and per-segment-simulation language.

## 2026-05-13 - Player Service Snapshot Mandate

What was wrong:
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt` had strong `PlatformCache` rules but no equivalent rule for player movement service dependencies.
- Static source evidence in `HectonPlayerMovement.cs` still shows movement-related helper chains reading `GlobalRegistry.*` for settings, water/ocean, thermal, sargassum, and resource-field data.

What was done:
- Added `PLAYER SERVICE SNAPSHOT POLICY` to the kinematics/AUP mandate.
- Required owner-local cached summaries for player/vehicle kinematics service dependencies.
- Banned continuous movement cadence from reading `GlobalRegistry.Settings`, `Fluid`, `OceanKinematics`, `SargassumDrag`, `Thermodynamics`, and `ResourceDistribution`.
- Required the 300-frame player kinematics Black Box to capture snapshot validity/version/fallback evidence.
- No runtime C# files were edited.

Cinematic Cheats used:
- Movement consumes compact environmental summaries instead of live cross-domain queries, leaving budget for water response, VR comfort, and high-tier environmental visuals.
- Low tier can use fallback water and cached ocean velocity; higher tiers can increase provider fidelity without changing the movement ownership boundary.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Documentation/mandate edit only.
- Preventive savings: PENDING PROFILER. Expected value is microsecond-level reduction after player owner replaces registry reads with cached snapshots.

Verification:
- `dotnet build` was not launched per user constraint.
- Static text check confirms the new snapshot policy and explicit GlobalRegistry bans are present in the kinematics mandate.

## 2026-05-13 - Mandate Pseudocode Zero-GC Scrub

What was wrong:
- Recently touched player/vehicle and logistics mandates still contained copy-risk pseudocode: `for each` / `foreach` loops, `PlatformCache singleton` wording, and `Length(cache.delta_position)` for a high-G guard.
- These examples were not runtime code, but they conflicted with the zero-GC and singleton-purge direction future agents are expected to copy.

What was done:
- Replaced player penetration and logistics graph/pressure examples with indexed iteration.
- Replaced the platform delta magnitude check with `lengthsq` against a squared threshold.
- Replaced `PlatformCache singleton` wording with shared-owner cache language.
- No runtime C# files were edited.

Cinematic Cheats used:
- The high-G guard now uses cheaper squared math for the same decision.
- Logistics remains graph math with indexable buffers instead of iterator-style examples that could become allocation-prone code.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Documentation/mandate edit only.
- Preventive savings: PENDING PROFILER. The correction prevents copied iterator/sqrt debt in player and logistics hot paths.

Verification:
- `dotnet build` was not launched per user constraint.
- Static scan found no remaining `for each`, `foreach`, stale PlatformCache singleton wording, or `Length(cache.delta_position)` in the touched player/logistics mandates.

## 2026-05-13 - Black Box Export Mandate Alignment

What was wrong:
- The central telemetry mandate allocated 300 frames but still described 50-frame export/safety snapshots.
- Current critical systems already use 300-frame Black Boxes and `Docs/AgentLogs/Dump_*.bin` dump paths, so the central rule was stale.
- Telemetry examples also contained copy-risk `foreach component`, `vel.magnitude`, and per-fault managed task wording.

What was done:
- Updated `DBG_Telemetry_Crash_Reporting_PostMortem.txt` to require full 300-entry ring export oldest-to-newest.
- Added the owner-specific dump path convention.
- Replaced NaN sweep examples with indexed iteration and squared velocity state.
- Replaced per-fault task allocation language with pre-owned worker / Awaitable / persistent log-thread guidance.
- No runtime C# files were edited.

Cinematic Cheats used:
- None in visual terms. This is crash forensics discipline: bounded 300-frame binary rings buy debuggability without unbounded runtime logging.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Documentation/mandate edit only.
- Preventive savings: PENDING PROFILER. Expected value is avoiding crash-time allocation and preserving enough history to diagnose NaN/no-clip failures.

Verification:
- `dotnet build` was not launched per user constraint.
- Static scan found no stale 50-frame export wording, per-fault task wording, `foreach component`, or `vel.magnitude` in the telemetry mandate.

## 2026-05-13 - Engineering Data Header Normalization

What was wrong:
- 13 mandate evidence sections still used `Engineering Data:` labels instead of the standardized `## Engineering Data` heading.
- A first atlas coverage check was a scanner defect; it expected backticks in atlas table rows and falsely reported missing assemblies.

What was done:
- Normalized 13 evidence labels across 11 mandate files to `## Engineering Data`.
- Preserved existing evidence table contents and local duplicate tables.
- Rechecked atlas coverage with the correct row pattern: 24 first-party asmdefs, 24 atlas rows.
- No runtime C# files were edited.

Cinematic Cheats used:
- None. This is rulebook hygiene so evidence gates remain machine-searchable.

Exact Microseconds saved:
- Measured runtime savings: 0 us. Documentation/mandate edit only.
- Preventive savings: review automation avoids false negatives and missed evidence gates.

Verification:
- `dotnet build` was not launched per user constraint.
- Static atlas coverage is 24/24 after corrected scan.
