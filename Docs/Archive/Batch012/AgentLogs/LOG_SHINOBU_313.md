# SHINOBU_313 Log

Status: POLISH STATIC VERIFIED / COMPILE BLOCKED BY MACHINE GUARD

## Session Start

What was wrong: No SHINOBU_313 batch state files existed for the current assignment.
What was done: Created status, rationale, and log files. Extracted the exact 20-task XML prompt via CLI from `Docs/Tasks/CURRENT_BATCH.md`.
Cinematic Cheats used: Declared parasite hull attachment as visual GPU fake, not mesh collision or gameplay truth.
Exact Microseconds saved: PENDING VERIFICATION; no runtime profiler capture exists.

## Session 2026-05-22 - GPU Parasite Swarm Implementation

What was wrong:
- No existing `HectonVFXRuntime` parasite lane existed in VFX/Environment.
- Legacy-looking `ParticleSystem` hits were unrelated camera speed-lines and vent bubbles; deleting them would be cross-domain damage, not parasite optimization.
- Existing parasite gameplay/boid work exists in World domain (`FloraInteractionManager`, `SargassumMicroFaunaBoids`) and includes gameplay influence/readback. SHINOBU_313 did not edit that authority.

What was done:
- Added `ParasiteTargetDTO` with exact 32-byte explicit layout.
- Added Vault buffer IDs `ShinobuParasiteTargets` through `ShinobuParasiteProfileCount`.
- Added `GenerateMockThermalTargetsJob`, `ExtractParasiteTargetsJob`, and `SelectTopParasiteTargetsJob`.
- Added `ParasiteSwarmGpuRuntime` with cold Vault binding, persistent `GraphicsBuffer` ownership, target upload through `LockBufferForWrite`, compute dispatch through a reused `CommandBuffer`, AUP shift handling, telemetry ring, and fatal dump path.
- Added `Hecton_ParasiteSwarm.compute` with GPU init, clear args, advection, Dear Lie shell attachment, rebase, and cull kernels.
- Added `Hecton_ParasiteSwarm.shader` for unlit procedural parasite quads.
- Added `AbyssalParasiteTunerWindow`, `ParasiteAttractionDebugGizmo`, `Biological_Particle_Scanner`, `parasite_behavior_profiles.csv`, architecture notes, and shared rendering optimization report section.

Cinematic Cheats used:
- Dear Lie spherical hull attachment: no mesh collision, no raycasts, no rigidbodies.
- Continuous quality curve controls particle budget and curl contribution; no low/high binary switch.
- GPU-only dormant particle initialization; no CPU zero-fill of particle state.
- Target staging is a 16-entry thermal macro-target fake; the GPU handles the expensive-looking swarm.

Exact Microseconds saved:
- CPU per-particle parasite simulation removed: PENDING PROFILER.
- Managed `List<Transform>` parasite target route avoided: estimated 20-80 us saved on i3/MX350, PENDING PROFILER.
- Mesh collision/raycast parasite attachment avoided: potentially >0.1 ms per dense swarm frame, PENDING PROFILER.
- Compile/run verification not executed because guard found active `dotnet` process and CPU load above 50%.

<SELF_AUDIT>
Task 01: PASS - VFX/Environment scan complete; expanded World evidence recorded.
Task 02: PASS - no `HectonVFXRuntime`; isolated VFX/Parasites route used.
Task 03: PASS - existing signals checked; no new parasite signal.
Task 04: PASS - CPU biological swarm route replaced; unrelated ParticleSystems preserved.
Task 05: PASS - no managed target list added; fixed Vault candidate lane used.
Task 06: PASS - mock thermal target Burst job added.
Task 07: PASS WITH DEVIATION - fixed Vault candidate lane replaces NativeList to obey data sovereignty.
Task 08: PASS - compute advection shader added.
Task 09: PASS - Dear Lie shell attachment added.
Task 10: PASS - indirect args and DrawProceduralIndirect path added.
Task 11: PASS - continuous GlobalQualityWeight particle budget.
Task 12: PASS - AupShiftSignal to CS_RebaseParasites.
Task 13: PASS - rollback descriptors untouched; visual buffers excluded.
Task 14: PASS - compute initialization, no CPU particle zero-fill.
Task 15: PASS - 300-entry telemetry ring and dump path.
Task 16: PASS - UI Toolkit tuner.
Task 17: PASS - ReadOnlySpan CSV parser and FNV hashes.
Task 18: PASS - OnDrawGizmos attraction spheres.
Task 19: PASS - Biological_Particle_Scanner and shared report section.
Task 20: FAIL PENDING COMPILE - static audit done; compile blocked by active dotnet and CPU load.
ARM64: ParasiteTargetDTO offsets: LocalPosition@0, ThermalSignature@12, Velocity@16, AttractionRadius@28, size 32.
Zero-GC hot path: no `ParticleSystem.Emit`, no `List<Transform>`, no `SetData`, no `GetData`, no `AsyncGPUReadback` in VFX/Parasites runtime scan.
AUP: extraction subtracts camera `double3` AUP before casting to `float3`.
DearLie: target shell snap plus target velocity blend; no triangle collision.
Dependency: runtime caches Vault handles at bootstrap and consumes `SignalBus<AupShiftSignal>`; no new signal.
BlackBox: `SwarmTelemetryEntry[300]` active route, dump target `Docs/AgentLogs/Dump_SHINOBU_313.bin`.
CompileGuard: build not run; active `dotnet` process and CPU load >50%.
</SELF_AUDIT>

## Session 2026-05-22 - DTO Assembly Shader Re-Audit Pass

What was wrong:
- Task 20 still needs hard evidence after the lock-release patch, but Unity import/project regeneration has not occurred.

What was done:
- Re-ran DTO layout and property scans: explicit 16/32/64-byte layouts remain; no runtime `Pack` or hot DTO property hits under parasite source.
- Re-ran Burst job scan: all parasite jobs keep required Burst flags and `[NoAlias]` lane annotations.
- Re-ran asmdef scan: runtime assembly references Core/Core.Contracts/Core.Memory plus Unity Burst/Collections/Jobs/Mathematics only; no Thermodynamics/KCC sibling runtime reference or direct DTO hit exists.
- Re-ran shader risk scan: 64-wide thread groups, no raw finite intrinsic, no shader variant macros, no Append/Consume buffers, no struct-valued draw ternary, and no `UnityWorldToClipPos(float4)` route.

Cinematic Cheats used:
- No new cheat added. Existing proof remains spherical thermal shell latch plus GPU curl advection instead of CPU mesh collision.

Exact Microseconds saved:
- 0 runtime us. This is verification and compile-wall protection only.

<SELF_AUDIT polish="DTO_ASSEMBLY_SHADER_RE_AUDIT_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - source-level proof is current, but Unity import/compiler/runtime/profiler proof is still absent.
DTO Guard: `ParasiteTargetDTO` remains 32 bytes with offsets 0/12/16/28; supporting rows remain 16/32/64-byte aligned.
Compile Guard: runtime asmdef has no direct sibling Thermodynamics/KCC/World runtime reference.
</SELF_AUDIT>

## Session 2026-05-22 - GraphicsBuffer Lock Release Hardening Pass

What was wrong:
- `_emptyFlowBuffer` and `_drawParamsBuffer` used `LockBufferForWrite` without a `try/finally` release fence.
- A failed Unity buffer write/import/device path could leave a mapped buffer locked, which is a Play Mode iteration stability fault.

What was done:
- Wrapped both upload paths in `try/finally`.
- Kept unlock conditional on a successful lock.
- Re-ran local scans: the parasite runtime lock paths now show matching release fences; forbidden-token scan over parasite source returns no hot native allocation, managed CSV payload, `SetData/GetData`, variable frame time, `MaterialPropertyBlock`, or `Camera.main` hits.
- Synchronized `Biological_Particle_Scanner.BuildSection` and `RENDERING_OPTIMIZATION_REPORT.json` with the lock-release proof and current compile guard.
- Re-sampled build guard: CPU load `54`, no compiler process output, generated project files still omit parasite files.

Cinematic Cheats used:
- None added. This is GPU resource lifecycle hardening; the visual route remains compute advection plus shell-latch indirect draw.

Exact Microseconds saved:
- Runtime CPU: 0 us claimed.
- Stability gain: prevents mapped-buffer leakage across repeated Play Mode/device-loss edges; profiler/runtime proof still absent.

<SELF_AUDIT polish="GRAPHICSBUFFER_LOCK_RELEASE_HARDENING_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - GraphicsBuffer lifecycle risk reduced, but Unity import/compiler/runtime/profiler proof is still absent.
Buffer Guard: all local `LockBufferForWrite` upload paths in `ParasiteSwarmGpuRuntime` now have `try/finally` unlock fences.
Compile Guard: CPU load 54 and generated projects stale, so build was not launched.
</SELF_AUDIT>

## Session 2026-05-22 - HLSL Portability Vaccination Pass

What was wrong:
- `Hecton_ParasiteSwarm.compute` used `isfinite()` in safety branches, but local project HLSL did not provide a precedent for that helper across Unity shader backends.
- Runtime budget clamped to `configuredMaxParticles`, but not to actual allocated ping-pong `GraphicsBuffer.count`, leaving a live-config over-dispatch edge.

What was done:
- Added `H8FiniteScalar` and `H8Finite3` helpers.
- Replaced all `isfinite()` calls in parasite compute with the local predicates.
- Kept NaN rejection behavior: comparisons against max finite float fail for NaN and infinity.
- Clamped per-frame particle budget to live particle buffer capacity.
- Added fail-closed GPU resource validation before compute dispatch and preserved `TelemetryFlagNoCompute` blackbox evidence on invalid resource state.
- Expanded the scanner-generated JSON section so future editor scans preserve route/DTO/shader/capacity/compile evidence.
- Fixed scanner JSON upsert so replacing a final SHINOBU_313 section or inserting into an empty report does not emit a trailing comma.
- Added a UI Toolkit CSV profile reload button to the tuner, routed through the existing byte-span parser.
- Removed exact forbidden-token false positives from parasite source method names and scanner strings.

Cinematic Cheats used:
- No CPU repair/readback path was added. GPU particles still self-repair inside the compute advection/cull path.

Exact Microseconds saved:
- Shader predicate pass: 0 CPU us. This is shader import portability and long-session safety work; ALU cost is negligible relative to particle advection.
- GPU capacity fence: one min/clamp plus resource predicates per visual frame; no saving claimed, prevents out-of-bounds GPU dispatch without hot realloc.
- Editor scanner/tuner hardening: 0 runtime us; prevents proof drift and designer recompile cycles.
- JSON upsert hardening: editor-only; prevents shared report corruption.
- Source-grep cleanup: 0 runtime us; removes false-positive scanner noise.

<SELF_AUDIT polish="HLSL_PORTABILITY_VACCINATION_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - shader portability risk reduced, but legal Unity import/compile/profiler proof remains absent.
Shader Safety: no raw `isfinite()` calls remain in parasite compute.
Readback Guard: no CPU buffer readback was introduced.
Report Guard: shared rendering report key `shinobu_313_parasitic_fauna_particle_swarms` restored again and JSON parse verified.
Compile Guard: CPU load `100`, active `dotnet.exe` PID `7816`, and generated project files still do not include parasite assembly/scripts.
Capacity Guard: particle budget is clamped to allocated ping-pong `GraphicsBuffer.count`; invalid GPU resources set `TelemetryFlagNoCompute`.
Editor Guard: scanner regeneration keeps detailed JSON fields; tuner can reload CSV profiles without C# recompilation.
Report Writer Guard: scanner upsert is trailing-comma safe for middle, final, and empty-root insertion paths.
Source Grep Guard: forbidden-token scan no longer trips over SHINOBU_313 method names or source string literals.
</SELF_AUDIT>

## Session 2026-05-22 - Fault Telemetry And Hot Lookup Polish

What was wrong:
- Dense thermal signal frames could be truncated from 512 staged candidates to 16 GPU targets without an explicit overflow dump trigger.
- `RecordTelemetry` could set `TelemetryFlagInvalidMath` locally while the caller still evaluated the old flags and skipped the dump.
- If no material was assigned and the fallback shader was missing, `ResolveMaterial` could repeat `Shader.Find` in the visual path.

What was done:
- Added `_lastCandidateOverflowCount` and recorded exact candidate overflow in `SwarmTelemetryEntry.OverflowCount`.
- Added `TelemetryFlagTargetOverflow` to the fault mask that writes `Docs/AgentLogs/Dump_SHINOBU_313.bin`.
- Changed telemetry flag passing to `ref uint` so invalid-math detection propagates to the dump trigger.
- Added `_fallbackMaterialLookupAttempted` so fallback shader lookup is cold and one-shot until teardown.
- Added compute-side finite-state reset before particle force integration, so NaN particles are repaired in GPU state instead of only hidden during cull.
- Replaced broad CSV alpha-header detection with exact byte-token checks for `species`/`name`, preserving headerless first profiles.
- Added SHINOBU_313 binary payload ledger entry for BufferIDs, DTO sizes, render route, Dear Lie route, scalability, and fault route.
- Re-ran parasite-folder static scans: no Thermodynamics/KCC DTOs, IMGUI calls, `Camera.main`, `MaterialPropertyBlock`, `new NativeArray`, `new NativeList`, `List<Transform>`, `float.Parse`, `SetData`, `GetData`, or `AsyncGPUReadback` hits.

Cinematic Cheats used:
- Top-N thermal truncation remains a visual-only fake; it is now observable in blackbox telemetry instead of widening the shader target loop.

Exact Microseconds saved:
- Overflow dump path: no steady-state claim; one integer compare/write per visual frame.
- Invalid math propagation: 0 meaningful runtime us; correctness/forensics fix.
- Fallback shader lookup guard: avoids repeated managed shader lookup only in missing-shader error scenes.
- GPU NaN reset: adds finite checks per active particle; chosen as survival insurance for endurance sessions.
- CSV header correction: cold boot only; prevents silent profile loss.
- Binary ledger entry: 0 runtime us; integration proof.
- Build not run: guard sample showed active `dotnet` process 16552 and CPU load 100%.

<SELF_AUDIT polish="FAULT_TELEMETRY_HOT_LOOKUP_PASS">
Task 15: PASS REINFORCED - overflow, GPU budget spike, and invalid math all trigger the 300-frame dump path.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static verification continues; legal compiler proof remains blocked.
Compile Guard: active `dotnet` and CPU load 100%, no build launched.
</SELF_AUDIT>

## Session 2026-05-22 - Final Static Verification

What was wrong:
- The initial local parasite asmdef was an untracked assembly island with incomplete references and no editor split.
- Compile verification remained illegal under the workstation guard.

What was done:
- Replaced the bad asmdef route with `Hecton8.VFX.Parasites.Runtime.asmdef` and `Hecton8.VFX.Parasites.Editor.asmdef`.
- Runtime asmdef references Core, Core.Contracts, Core.Memory, Burst, Collections, Jobs, and Mathematics. Thermodynamics/KCC sibling references were later purged.
- Editor asmdef references parasite runtime and keeps `allowUnsafeCode=true` because editor tooling calls the unsafe runtime contracts.
- Static forbidden-pattern scan over `Assets/_Project/Scripts/VFX/Parasites` returned no hot-path hits for `new NativeArray`, `new NativeList`, `List<Transform>`, `ParticleSystem.Emit`, `float.Parse`, `SetData`, `GetData`, or `AsyncGPUReadback`.
- Shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parses through `ConvertFrom-Json`.

Cinematic Cheats used:
- No additional physical simulation was added during final verification. Dear Lie remains the active attachment method.

Exact Microseconds saved:
- Assembly correction: 0 runtime us; reduces compile-wall risk only.
- Static scan proof: 0 runtime us; confirms no obvious CPU particle/readback regression in the new VFX parasite folder.
- Build not run: final guard sample showed `dotnet` pids 3056 and 17184 active and CPU load 100%, so compile remains blocked by project law.

## Session 2026-05-22 - Ultra Polish Boundary Pass

What was wrong:
- Parasite runtime still referenced Thermodynamics/KCC DTOs directly, creating a sibling-domain compile wall.
- Target extraction scheduled jobs and completed them in the same `LateFrameTick`.
- `CS_RebaseParasites` aliased read/write UAV state.
- Tuner graph used IMGUI/Handles instead of pure UI Toolkit.

What was done:
- Removed Thermodynamics/KCC usings, DTO handles, and the runtime asmdef Thermodynamics reference.
- Re-routed target source input through existing `ThermalSourceSignal` snapshots staged into parasite-owned Vault candidates.
- Changed target extraction/selection to one-frame-late consumption; hot path only fences after `JobHandle.IsCompleted` and does not read/upload Vault target arrays while the selection job is pending.
- Changed compute groups to 64, rebase to read/write ping-pong, and cull index write to an explicit guard.
- Replaced editor telemetry graph with `VisualElement.generateVisualContent`.
- Added a one-element `H8ParasiteDrawParams` GraphicsBuffer so the shader reconstructs world position from cameraWS + camera-relative particle state without MaterialPropertyBlock or absolute float particle coordinates.
- Static scans show no Thermodynamics/KCC DTOs, IMGUI graph calls, `Camera.main`, SetData/GetData, AsyncGPUReadback, or NativeArray allocations under `Assets/_Project/Scripts/VFX/Parasites`.

Cinematic Cheats used:
- Thermal signals are macro-attractors; parasite swarms remain GPU-only visual fake, not gameplay heat state.
- One-frame target latency is accepted because the swarm is presentation-only.

Exact Microseconds saved:
- Same-frame target job stall removed: estimated 20-80 us on i3/MX350, pending profiler.
- Compile-wall reduction: 0 runtime us; prevents parasite recompiles from Thermodynamics/KCC DTO churn.
- GPU alias fix: exact microseconds pending GPU profiler.
- Draw params route: 16 bytes/frame upload; prevents camera-relative render drift.
- Build not run: guard sample showed active `dotnet` pids 6528 and 7732 and CPU load 84%.

<SELF_AUDIT polish="ULTRA_BOUNDARY_PASS">
Task 01: PASS.
Task 02: PASS.
Task 03: PASS - `ThermalSourceSignal` and `AupShiftSignal` consumed; no new attack signal.
Task 04: PASS.
Task 05: PASS - no `List<Transform>`, fixed Vault candidates.
Task 06: PASS.
Task 07: PASS WITH ARCHITECTURAL DEVIATION - direct Thermodynamics/KCC DTO reads replaced by contract signal projection to obey compile-wall law.
Task 08: PASS.
Task 09: PASS.
Task 10: PASS.
Task 11: PASS - particle budget and curl octave limit scale from `GlobalQualityWeight`.
Task 12: PASS - rebase is GPU ping-pong, not CPU readback.
Task 13: PASS.
Task 14: PASS.
Task 15: PASS.
Task 16: PASS - UI Toolkit painter graph, no IMGUI.
Task 17: PASS.
Task 18: PASS - editor-only gizmo wrapper.
Task 19: PASS.
Task 20: FAIL PENDING COMPILE - static scans pass; build blocked by CPU/dotnet guard.
Struct Layout: `ParasiteTargetDTO` size 32; LocalPosition@0 12 bytes, ThermalSignature@12 4 bytes, Velocity@16 12 bytes, AttractionRadius@28 4 bytes.
Vault: `ShinobuParasiteTargets`, `ShinobuParasiteTargetCandidates`, `ShinobuParasiteTargetCount`, `ShinobuParasiteTuning`, `ShinobuParasiteTelemetryRing`, `ShinobuParasiteTelemetryCursor`, `ShinobuParasiteProfiles`, `ShinobuParasiteCsvScratch`, `ShinobuParasiteScannerSummary`, `ShinobuParasiteProfileCount`.
GraphicsBuffer ABI: particle A/B, target upload, visible indices, indirect args, and one-element draw params buffer.
Dependency: runtime asmdef has no Thermodynamics/KCC sibling reference.
BlackBox: 300-frame telemetry ring and dump path remain active.
</SELF_AUDIT>

## Session 2026-05-22 - Vault Fence And Build Truth Pass

What was wrong:
- Target extraction jobs wrote Vault-backed arrays without an explicit writer-fence lifecycle.
- `Task 20` could be falsely satisfied by an external build that does not include the new parasite files, because generated project files are stale.

What was done:
- Added target writer-lock acquisition for `ShinobuParasiteTargets`, `ShinobuParasiteTargetCandidates`, and `ShinobuParasiteTargetCount` before scheduling extraction/selection jobs.
- Kept those locks held until the one-frame-late completion fence; teardown also completes and releases.
- Added short telemetry writer locks for `ShinobuParasiteTelemetryRing` and `ShinobuParasiteTelemetryCursor`.
- Raised supported GPU particle ceiling to 2,000,000 while leaving serialized default allocation at 500,000.
- Removed unused `ShinobuParasiteMockKinematics` BufferID after the KCC-free signal route was locked in.
- Corrected attraction gizmo to draw player-runtime-position plus camera-local target offset.
- Added cold material pass warmup after parasite buffer binding; compute still warms through startup init dispatch.
- Verified `*.csproj/*.sln/*.slnx` contain no `VFX.Parasites`, `ParasiteSwarmGpuRuntime`, `ParasiteSwarmContracts`, or `Hecton_ParasiteSwarm` entries, so `dotnet build` would not prove these changes until Unity regenerates project files.
- Re-ran static scans: no Thermodynamics/KCC DTOs, IMGUI graph calls, `Camera.main`, `MaterialPropertyBlock`, `new NativeArray`, `new NativeList`, `List<Transform>`, `float.Parse`, `SetData`, `GetData`, or `AsyncGPUReadback` hits under `Assets/_Project/Scripts/VFX/Parasites`.
- `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` still parses with `ConvertFrom-Json`.

Cinematic Cheats used:
- No new physical simulation. Thermal targets stay macro-attractor fakes; attachment remains HLSL spherical shell latch.

Exact Microseconds saved:
- Vault fence pass: correctness hardening; no direct runtime saving claimed.
- Capacity pass: 0 CPU us; million-particle memory is paid only when configured for high-end hardware.
- Dead lane purge: 0 CPU us; removes stale kinematics coupling evidence.
- Debug gizmo correction: editor-only; no runtime frame cost.
- Shader warmup: startup-only; avoids first-draw material pass hitch.
- Fake-build rejection: avoided meaningless build I/O under CPU load 100%.
- Build not run: latest guard sample returned no `dotnet/csc` process output but CPU load 100%; compile remains prohibited by project law.

<SELF_AUDIT polish="VAULT_FENCE_BUILD_TRUTH_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static verification passes, but compiler/runtime/profiler proof is still absent.
Vault Locks: target buffers hold write locks across pending writer jobs; telemetry buffers use short write locks.
Build Truth: generated project files do not include parasite files yet, so external `dotnet build` would not validate this domain.
Compile Guard: CPU load 100%, no compile launched.
</SELF_AUDIT>

## Session 2026-05-22 - Fixed Tick And Report Restoration Pass

What was wrong:
- GPU advection and mock target motion still consumed `Time.deltaTime` / `Time.time`.
- Shared `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` no longer contained the SHINOBU_313 evidence section after neighboring report writes.
- Static prefab search showed `PFB_Support_Pocket_Hazard.prefab` had both `ParasiteA/B` names and `ParticleSystem` blocks, requiring direct classification before any deletion claim.

What was done:
- Replaced variable frame-time inputs with `SimulationTickDeltaSeconds = 1f / 60f`; later hardening moved visual phase to the runtime-owned private visual counter instead of Unity `Time.frameCount`.
- Re-ran time and forbidden-pattern scans for the parasite folder.
- Read `PFB_Support_Pocket_Hazard.prefab`: `ParasiteA` and `ParasiteB` are mesh cylinders; ParticleSystems are `VentBubbleColumn_Secondary`, `VentBubbleColumn_LOD1`, and `VentBubbleColumn_Main`.
- Restored the SHINOBU_313 section in `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` without removing other agent sections.
- Hoisted `SelectTopParasiteTargetsJob` candidate pointer resolution outside the scan loop.
- Fixed shader `SafeNormalize` zero-vector fallback, changed inverse-square force floor to radius squared, and added `Life01` finite cull.
- Updated status, rationale, and architecture notes with fixed-tick and prefab-classification evidence.

Cinematic Cheats used:
- No CPU particle fallback was added. Parasite motion remains HLSL inverse-square attraction plus curl and spherical shell latch.
- Vent bubbles were not reclassified as parasite swarms.

Exact Microseconds saved:
- Variable time purge: no CPU microsecond saving claimed; removes integration jitter and variable-delta drift.
- Prefab classification: 0 runtime us; prevents destructive edit to unrelated vent presentation.
- Report restoration: 0 runtime us; restores Task 19 proof artifact.
- Pointer hoist: sub-microsecond expected per 512-candidate pass, pending profiler.
- Shader vaccination: negligible ALU; prevents poisoned GPU particle rows from surviving long sessions.

<SELF_AUDIT polish="FIXED_TICK_REPORT_RESTORATION_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - code static checks pass, but compiler/runtime/profiler proof is still absent.
Time Discipline: no `Time.deltaTime` or `Time.time` under `Assets/_Project/Scripts/VFX/Parasites`.
ParticleSystem Evidence: scoped biological CPU particle authority remains absent; found prefab ParticleSystems are vent columns, not parasite swarms.
Report: shared rendering report restored and JSON parse verified.
</SELF_AUDIT>

## Session 2026-05-22 - Compile-Risk Static Audit Pass

What was wrong:
- A real compile remains blocked by machine guard and stale generated project files.
- The next useful work was therefore not more code churn, but verifying the new parasite files against local API and assembly evidence.

What was done:
- Used local Unity project call sites to verify `Graphics.DrawProceduralIndirect`, `GraphicsBuffer.LockBufferForWrite`, `SignalBus<T>.GetFrameSnapshot`, `IDataVault` write locks, `GlobalRegistry.Player`, and `AbsoluteUniversePosition` usage patterns.
- Parsed parasite runtime/editor asmdefs. Runtime references only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics.
- Re-ran DTO/property scan: parasite DTOs are explicit 16/32/64-byte layouts; no hot DTO auto-properties detected.
- Re-ran forbidden-pattern scan. The only `Destroy` hits are teardown-only GPU resource disposal in `OnDisable`/`DisposeGpuResources`.
- Re-ran `git diff --check`; only CRLF conversion warnings on shared files.
- Re-validated `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` with `ConvertFrom-Json`.
- Re-sampled guard with escalated CIM: CPU load 76, active MSBuild `dotnet.exe` PIDs `5652,15352,1716,22460,21912,19416,13176`.

Cinematic Cheats used:
- No new simulation added. The runtime remains thermal macro-target staging plus GPU-only particle advection and shell attachment.

Exact Microseconds saved:
- Static audit: 0 runtime us.
- Build avoided under active MSBuild: saves workstation contention only; no runtime claim.
- Task 20 remains blocked until Unity imports/regenerates project files and CPU/dotnet guard permits a meaningful compile.

<SELF_AUDIT polish="COMPILE_RISK_STATIC_AUDIT_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static compile-risk audit passes, but compiler/runtime/profiler proof is still absent.
Compile Guard: CPU 76 with seven active MSBuild `dotnet.exe` processes; build not launched.
Generated Project Guard: `rg` over `*.csproj/*.sln/*.slnx` still finds no parasite runtime/editor files, so external build remains non-proving.
Asmdef Guard: no Thermodynamics/KCC sibling reference in parasite runtime assembly.
Report Guard: rendering optimization report parses as JSON.
</SELF_AUDIT>

## Session 2026-05-22 - HLSL Target-Slot NaN Fence Pass

What was wrong:
- `CS_AdvectParasites` masked inactive target slots but still read/evaluated every target row, so an empty/low-target frame could turn stale GPU target NaNs into `0 * NaN` acceleration.
- Direct compute `FindKernel` calls could throw during `OnEnable` before the no-compute telemetry path.
- Latest CPU/dotnet guard is clear, but generated project files still omit the parasite assembly/scripts, so external `dotnet build` remains non-proving.

What was done:
- Changed `CS_AdvectParasites` to branch before reading inactive target rows.
- Added active target field finite checks and final particle position/velocity/life validation before writing the ping-pong row.
- Rewrote shader `SafeNormalize` to return fallback before calculating reciprocal square root on invalid vectors.
- Replaced direct kernel lookup with `HasKernel`-guarded lookup; missing kernels stay at `-1` and route to `TelemetryFlagNoCompute`.
- Patched `Biological_Particle_Scanner.BuildSection` so future scanner runs preserve the current shader-safety and compile-gate evidence.
- Re-audited BufferID collision surface; report/scanner now list exact occupied IDs `71980..71987,71989,71990` instead of implying `71988` is occupied.
- Re-ran forbidden C# token scan, raw shader `isfinite()` scan, report JSON parse, and diff whitespace check.
- Re-sampled guard: CPU load `43`; no `dotnet`, `csc`, or `VBCSCompiler` process output; generated project files still have no parasite hits.

Cinematic Cheats used:
- No CPU physics/collision fallback added. The shader still uses thermal macro-target shell latch and curl advection.

Exact Microseconds saved:
- Target-slot NaN fence: no CPU saving claimed; prevents persistent GPU-state corruption.
- Kernel lookup guard: cold path only; runtime impact 0 us under valid content.
- Scanner regeneration patch: editor-only; runtime impact 0 us.
- BufferID evidence precision: integration-only; runtime impact 0 us.
- Build not run: avoids false compile evidence on stale generated projects.

<SELF_AUDIT polish="HLSL_TARGET_SLOT_NAN_FENCE_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static checks pass; compiler/runtime/profiler proof is still absent.
Shader: inactive target rows are not read; active target fields and final particle rows are finite-checked.
Compile Guard: CPU/dotnet guard clear, but generated project files still omit parasite assembly/scripts.
</SELF_AUDIT>

## Session 2026-05-22 - Unity Importer Meta Hardening Pass

What was wrong:
- New parasite `.meta` files were minimal `fileFormatVersion/guid` records instead of matching local Unity importer blocks.
- That could create avoidable Unity import drift before a real compiler pass.

What was done:
- Added `DefaultImporter` to `Assets/_Project/Scripts/VFX/Parasites/Editor.meta`.
- Added `AssemblyDefinitionImporter` to runtime/editor parasite asmdef metas.
- Added `MonoImporter` to parasite runtime/editor C# metas.
- Added `ComputeShaderImporter`, `ShaderImporter`, and `TextScriptImporter` to the compute, draw shader, and CSV metas.
- Preserved all GUIDs.
- Rechecked targeted GUID uniqueness and `git diff --check` for the changed parasite/meta files.

Cinematic Cheats used:
- None. This is import hygiene only; the runtime route remains GPU-only thermal target shell attachment.

Exact Microseconds saved:
- 0 runtime us. This reduces Unity import instability before the next legal compile/runtime proof window.

<SELF_AUDIT polish="UNITY_IMPORTER_META_HARDENING_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - importer hygiene improved, but compiler/runtime/profiler proof is still absent.
Importer Guard: new parasite folder/asmdef/script/compute/shader/csv metas now contain local-project importer blocks.
GUID Guard: targeted scan finds each new GUID only in its own `.meta`.
</SELF_AUDIT>

## Session 2026-05-22 - Shader ABI Compile-Risk Hardening Pass

What was wrong:
- `Hecton_ParasiteSwarm.shader` used a struct-valued ternary to pick between particle buffers.
- The vertex shader passed `float4(world, 1.0)` to `UnityWorldToClipPos`, creating an avoidable implicit-conversion risk.

What was done:
- Replaced the struct-valued ternary with an explicit branch assigning `ParasiteGpuParticleDTO`.
- Changed the clip transform to `UnityWorldToClipPos(world)`.
- Re-ran source scans for the old patterns, raw `isfinite`, shader variant macros, and shader diff whitespace.

Cinematic Cheats used:
- None added. The route remains indirect GPU quads driven by shell-latched compute particles.

Exact Microseconds saved:
- 0 CPU us. This reduces shader import risk only.

<SELF_AUDIT polish="SHADER_ABI_COMPILE_RISK_HARDENING_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - shader source risk reduced, but Unity shader import/runtime proof is still absent.
Shader Guard: no struct-valued particle-buffer ternary, no `UnityWorldToClipPos(float4`, no raw shader `isfinite`, no variant macros.
</SELF_AUDIT>

## Session 2026-05-22 - Zero-Target Draw Suppression Pass

What was wrong:
- `CS_CullParasites` rendered dormant particles even when target count was zero, so scenes without thermal sources could still show camera-local parasite quads.
- CPU guard is blocked at `67`; generated project files still omit the parasite assembly/scripts.

What was done:
- Gated cull liveness by target count with `step(0.5, _H8ParasiteFrameParams0.w)`.
- Kept GPU particle state resident; only visible instance emission is suppressed on zero-target frames.
- Re-ran shader source scans for raw `isfinite`, shader variant macros, struct-buffer ternary, and `UnityWorldToClipPos(float4`.
- Updated scanner/report/status/rationale evidence with the zero-target cull and current build guard.
- Corrected SHINOBU_311 cross-doc BufferID evidence to exact parasite-owned IDs `71980..71987,71989,71990`.

Cinematic Cheats used:
- No CPU despawn, no buffer clear, no GameObject toggles. The illusion now disappears by writing zero indirect instances.

Exact Microseconds saved:
- CPU: 0 us.
- GPU: saves all parasite quad/fragment work on zero-target frames; exact us pending Unity profiler.

<SELF_AUDIT polish="ZERO_TARGET_DRAW_SUPPRESSION_PASS">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - source proof improved, but Unity shader import/runtime/profiler proof is still absent.
Shader Guard: zero target count now produces zero visible instances without CPU buffer churn.
Compile Guard: CPU load 67 and generated projects stale, so build was not launched.
</SELF_AUDIT>

## Session 2026-05-22 - CSV Scratch Ingest Hardening Pass

What was wrong:
- `TryLoadProfilesFromDisk` used `File.ReadAllBytes`, creating a managed CSV payload despite Task 17 requiring byte-span parsing into unmanaged Vault tables.
- The editor reload button duplicated the same managed byte-array path.
- A preflight prompt extraction command was incorrectly escaped and returned `START_LINE=-1` even though the prompt block exists.

What was done:
- Replaced managed CSV byte staging with `ShinobuParasiteCsvScratch` plus `FileStream.Read(Span<byte>)`; oversized CSV files now fail closed instead of parsing truncated rows.
- Feed `ParasiteSwarmContracts.LoadProfilesFromCsv` with a pointer-backed `ReadOnlySpan<byte>`.
- Routed the UI Toolkit reload button through the same runtime CSV bridge.
- Re-ran grep gates: no `File.ReadAllBytes`, `byte[]`, `float.Parse`, `SetData/GetData`, variable frame time, or LINQ materialization tokens remain under the parasite folder.
- Changed thermal overflow telemetry to count every eligible `ThermalSourceSignal` while still writing only the fixed candidate buffer.
- Synchronized `Biological_Particle_Scanner.BuildSection` and `RENDERING_OPTIMIZATION_REPORT.json` with CSV scratch ingest and current compile-gate evidence; JSON parse passed.
- Corrected prompt extraction proof: `SHINOBU_313` block is lines `1973..2118`, 146 lines.
- Re-sampled build guard: CPU load `34`, but `VBCSCompiler.exe` PID `24996` is active and generated projects still contain no parasite files.

Cinematic Cheats used:
- None added. This is data-ingest hygiene; the visual route remains GPU shell latch plus curl advection.

Exact Microseconds saved:
- Runtime frame: 0 us claimed.
- Cold reload: avoids one managed byte-array allocation up to the 16KB Vault scratch cap.
- Overflow counter: one counter/branch per eligible macro thermal signal; exact CPU cost pending profiler.

<SELF_AUDIT polish="CSV_SCRATCH_INGEST_HARDENING_PASS">
Task 17: PASS STATIC - CSV parser now consumes unmanaged Vault scratch through `ReadOnlySpan<byte>` without managed payload allocation.
Task 15: PASS STATIC - overflow telemetry now records eligible source excess, not only staged candidate excess.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - source proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
Compile Guard: external build still not useful until Unity regenerates project files for the new parasite asmdefs/scripts; `VBCSCompiler.exe` is active.
</SELF_AUDIT>

## Session 2026-05-22 - Compile-Wall Namespace Purge

What was wrong:
- `ParasiteSwarmGpuRuntime` imported `Hecton8.World` while the runtime asmdef intentionally routes only through Core/Core.Contracts/Core.Memory.
- `HomeostasisBrain` is declared in `Hecton8.Core`, so the World import was unnecessary and would be a compile-wall leak after Unity project regeneration.

What was done:
- Removed `using Hecton8.World;`.
- Re-ran source/asmdef scans for World, Thermodynamics, KCC, HeatSourceDTO, ThermalCellDTO, and KinematicStateDTO hits under the parasite folder.
- Updated status, rationale, architecture, scanner, and report evidence.
- Reworded scanner-generated upload evidence so source grep gates no longer hit exact CPU GPU-copy/readback API names in a string literal.

Cinematic Cheats used:
- None added. The Dear Lie remains GPU shell-latched particles and indirect draw.

Exact Microseconds saved:
- Runtime: 0 us.
- Compile-wall risk reduced: avoids a sibling-domain assembly reference pressure point.

<SELF_AUDIT polish="COMPILE_WALL_NAMESPACE_PURGE">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - source routing proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
Compile Guard: no World/Thermodynamics/KCC namespace or DTO hit remains under parasite source/asmdefs.
</SELF_AUDIT>

## Session 2026-05-22 - Non-Proving Build Guard Refresh

What was wrong:
- Previous evidence still referenced CPU load `68`; current guard changed, but generated Unity project files remain stale for SHINOBU_313.

What was done:
- Re-sampled guard: CPU load `43`, no `dotnet`, `csc`, or `VBCSCompiler` process output.
- Rechecked generated `*.csproj/*.sln/*.slnx` files: no `VFX.Parasites`, `ParasiteSwarmGpuRuntime`, `ParasiteSwarmContracts`, or `Hecton_ParasiteSwarm` hits.
- Updated status, rationale, scanner, and shared rendering report to record the current guard truth.

Cinematic Cheats used:
- None added. The runtime route remains GPU shell latch, curl advection, zero-target indirect suppression, and no CPU particle simulation.

Exact Microseconds saved:
- Runtime: 0 us.
- Verification: avoided a false-positive stale-project build that would not compile this domain.

<SELF_AUDIT polish="NON_PROVING_BUILD_GUARD_REFRESH">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - CPU is currently legal, but generated project files still omit SHINOBU_313 assets, so external build would not prove the domain.
Compile Guard: CPU 43, no compiler process output, generated projects stale.
</SELF_AUDIT>

## Session 2026-05-22 - Dead Flow Buffer Purge

What was wrong:
- `_H8AbyssalFlowBuffer` and `_emptyFlowBuffer` existed as a structured-buffer fallback, but parasite compute advection samples only `_H8AbyssalFlowField`.
- That path allocated and uploaded one dead GPU buffer and kept stale proof text alive.

What was done:
- Removed `_H8AbyssalFlowBuffer` from the compute shader.
- Removed `_emptyFlowBuffer`, its cold allocation, upload, binding, and disposal from `ParasiteSwarmGpuRuntime`.
- Updated scanner/report/status/rationale evidence so upload proof names only the remaining target and draw-params lock paths.

Cinematic Cheats used:
- Kept the cheap 1x1 fallback `Texture3D`; no CPU flow simulation or second GPU route was introduced.

Exact Microseconds saved:
- Runtime frame: 0 us claimed.
- Startup: one cold `GraphicsBuffer` allocation and one cold upload removed.

<SELF_AUDIT polish="DEAD_FLOW_BUFFER_PURGE">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static source is cleaner, but Unity shader import/compiler/runtime/profiler proof is still absent.
Flow ABI: compute samples `Texture3D` only; no dead structured fallback buffer remains.
</SELF_AUDIT>

## Session 2026-05-22 - Score-Ranked Top-N Correction

What was wrong:
- Candidate scoring included heat, radius, and proximity, but the final top-16 insertion compared only `ThermalSignature`.
- That made the Burst scoring pass partially wasted and could send particles toward distant hot targets over better local latch targets.

What was done:
- `SelectTopParasiteTargetsJob` now uses a fixed stack-local 16-float score lane and shifts scores with target rows.
- Scanner/report and architecture/ledger wording now identify score-ranked top-16 target selection.

Cinematic Cheats used:
- No extra simulation. The same 16-target GPU shell latch is fed better-ranked visual targets.

Exact Microseconds saved:
- CPU: no claimed frame saving; same bounded O(16 * stagedCandidates) pass.
- GPU/visual: reduces wasted visible particles on lower-value targets; exact profiler proof pending Unity import.

<SELF_AUDIT polish="SCORE_RANKED_TOP_N">
Task 07: PASS STATIC - target extraction score is now consumed by top-16 selection.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static source proof improved, Unity compile/runtime/profiler proof still absent.
</SELF_AUDIT>

## Session 2026-05-22 - Active Compiler Guard Refresh

What was wrong:
- The prior compile evidence said CPU 43 with no compiler process. After the score patch, the workstation state changed.

What was done:
- Re-sampled build guard: CPU load `25`, active `dotnet` processes and `VBCSCompiler` exist.
- Rechecked generated project files: still no SHINOBU_313 parasite assembly/script hits.
- Updated status, rationale, scanner, and shared rendering report to reflect the active compiler plus stale-project blocker.

Cinematic Cheats used:
- None added.

Exact Microseconds saved:
- Runtime: 0 us.
- Verification: avoided competing with an active compiler and avoided a stale-project build that would not include this domain.

<SELF_AUDIT polish="ACTIVE_COMPILER_GUARD_REFRESH">
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - build is blocked by active compiler processes and stale generated project files.
Compile Guard: CPU 25, active dotnet/VBCSCompiler, generated projects stale.
</SELF_AUDIT>

## Session 2026-05-23 - Runtime Clock And Trig SFU Purge

What was wrong:
- Runtime compute time, mock target phase, telemetry frame, and telemetry hash used Unity `Time.frameCount`.
- Parasite compute curl and dormant positions used native shader `sin`/`cos` in the per-particle path.
- Mock/fallback thermal target generation still used standard CPU trig even though the target lane is a bounded visual fake.

What was done:
- Added a private fixed-step visual counter in `ParasiteSwarmGpuRuntime`, passed one visual frame/phase through dispatch, mock extraction, and telemetry, and wrapped shader phase through a 4096-tick ramp.
- Replaced compute `sin`/`cos` calls with bounded polynomial `H8FastSin` / `H8FastCos`.
- Added `ParasiteSwarmContracts.FastSinApprox/FastCosApprox` and used it in mock target generation plus staged thermal-source velocity.
- Updated route card, Binary Payload Integration Ledger, scanner evidence, shared rendering report, status, and rationale.

Cinematic Cheats used:
- Curl and dormant phase are approximate bounded wave functions, not exact trig or CPU fluid/physics simulation.
- The visual-clock counter is presentation-only and does not enter rollback truth, save identity, or gameplay authority.

Exact Microseconds saved:
- GPU: expected SFU pressure reduction in `CS_AdvectParasites`; exact microseconds pending Unity profiler.
- CPU: fallback top-16 target lane drops standard trig calls; likely below profiler noise, but removes unnecessary scalar cost.

<SELF_AUDIT polish="RUNTIME_CLOCK_AND_TRIG_SFU_PURGE">
Task 08: PASS STATIC - compute advection no longer uses native shader trig calls.
Task 11: PASS STATIC - quality still controls budget/octave cost continuously; trig approximation does not introduce binary tier switches.
Task 15: PASS STATIC - telemetry hash/frame now uses runtime-owned visual frame, not Unity frame clock.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
Static Gate: runtime parasite C# has no Time.frameCount/math.sin/math.cos; compute/shader have no native sin/cos calls.
</SELF_AUDIT>

## Session 2026-05-23 - Zero-Target Compute Dispatch Suppression

What was wrong:
- Targetless frames wrote zero visible instances, but still ran full-budget rebase/advection/cull before draw suppression.
- The static GPU estimator could flag a budget spike and trigger a blackbox dump for an empty scene.

What was done:
- Runtime now resolves `dispatchedParticleBudget = 0` when target count is zero.
- `DispatchAndRender` clears indirect args and returns before rebase/advection/cull/draw on zero-target frames.
- `EstimateGpuMicroseconds` returns a clear-only value when particle budget or target count is zero.
- Scanner/report, route card, ledger, status, and rationale evidence were updated.

Cinematic Cheats used:
- Empty thermal scenes keep resident GPU particle buffers and only clear the indirect draw count. No CPU reset, buffer churn, or fake target injection is introduced.

Exact Microseconds saved:
- GPU: removes all per-particle advection/cull work on empty thermal frames. At 500k configured default, this prevents full-budget compute dispatch when no parasite visual can be emitted.
- CPU: avoids false fault dump I/O from formula-only budget spikes on empty scenes.

<SELF_AUDIT polish="ZERO_TARGET_COMPUTE_SUPPRESSION">
Task 08: PASS STATIC - targetless frames dispatch only clear-args, not full swarm advection.
Task 10: PASS STATIC - targetless frames skip indirect draw call after clearing args.
Task 15: PASS STATIC - targetless telemetry records dispatched particle budget 0 and clear-only estimate.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>

## Session 2026-05-23 - GPU Payload Ping-Pong Uploads

What was wrong:
- `LockBufferForWrite` upload paths were release-safe but still single-buffered for targets and draw params.
- CPU could map the same target/draw payload buffer that the GPU was still consuming from the previous frame.

What was done:
- Split target payloads into `_targetBufferA/_targetBufferB`.
- Split draw params into `_drawParamsBufferA/_drawParamsBufferB`.
- Target uploads write the alternate buffer and flip parity only after successful locked upload/unlock; compute binds the current uploaded target buffer.
- Draw-param uploads write the alternate one-row buffer, bind that exact buffer to the material for the draw, and leave static particle/visible bindings cached.
- Scanner/report, route card, ledger, status, and rationale were updated.

Cinematic Cheats used:
- None added. This is a GPU synchronization hardening pass around the existing Dear Lie particle route.

Exact Microseconds saved:
- Runtime: no measured claim until Unity profiler. Expected savings are stall avoidance, not lower arithmetic.
- Memory: adds one 16-row `ParasiteTargetDTO` buffer and one one-row `float4` draw-param buffer; below practical VRAM noise.

<SELF_AUDIT polish="GPU_PAYLOAD_PING_PONG_UPLOADS">
Task 08: PASS STATIC - compute now reads the current uploaded target buffer while CPU writes the alternate target buffer.
Task 10: PASS STATIC - draw params are uploaded to an alternate one-row buffer and rebound for the draw.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>

## Session 2026-05-23 - Headered Blackbox Dump Payload

What was wrong:
- `Dump_SHINOBU_313.bin` contained only raw telemetry rows.
- Forensic tools could not read row stride, row count, cursor, version, or payload byte count from the file itself.

What was done:
- Added a 64-byte little-endian `H8P3` dump header.
- Header fields: magic, version, header bytes, `SwarmTelemetryEntry` stride, row count, post-write cursor, and payload byte count.
- Runtime now passes the post-write telemetry cursor into `TryWriteTelemetryDump`.
- Scanner/report, route card, ledger, status, and rationale were updated.

Cinematic Cheats used:
- None. This is blackbox forensic ABI hardening.

Exact Microseconds saved:
- Runtime steady frame: 0 us.
- Fault path: adds one 64-byte stack header write when a dump is already emitted; no measured claim.

<SELF_AUDIT polish="HEADERED_BLACKBOX_DUMP">
Task 15: PASS STATIC - dump payload now carries a fixed header and cursor before raw telemetry rows.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>

## Session 2026-05-23 - Grouped GPU Frame Params

What was wrong:
- Per-frame compute inputs were still transmitted as loose vector uniforms.
- There was no explicit 64-byte GPU ABI row for frame timing, quality, attraction, curl, flow, shell radius, and latch blending.

What was done:
- Added `ParasiteFrameParamsDTO` as an explicit 64-byte row: `Frame0@0`, `Frame1@16`, `Frame2@32`, `Reserved@48`.
- Added layout validation for `ParasiteFrameParamsDTO=64`.
- Added ping-pong `GraphicsBuffer` rows for frame params and upload through `LockBufferForWrite` with a `try/finally` unlock fence.
- Bound `_H8ParasiteFrameParams` to init, rebase, advect, and cull compute kernels.
- Verified no old `ParasiteFrameParams0/1/2` C# property IDs, old bind helper, or loose frame-param vector setter remain.

Cinematic Cheats used:
- None added. This hardens the GPU payload route around the existing Dear Lie shell-latch swarm.

Exact Microseconds saved:
- Runtime: one 64-byte mapped upload per active compute frame replaces three loose vector-param writes. Exact driver-state savings pending Unity profiler and Frame Debugger.
- Memory: adds one extra 64-byte frame-param row for ping-pong safety, below practical VRAM noise.

<SELF_AUDIT polish="GROUPED_GPU_FRAME_PARAMS">
Task 08: PASS STATIC - compute advection consumes one grouped frame-param buffer row.
Task 10: PASS STATIC - init/rebase/advect/cull kernels bind the same explicit frame-param ABI row.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>

## Session 2026-05-23 - Rebase Frame Param Binding And Build Guard Refresh

What was wrong:
- Manual source reread found `CS_RebaseParasites` reads `_H8ParasiteFrameParams0.z`, but runtime initially bound the grouped frame-param row only for init, advect, and cull.
- The build guard state changed again after the patch.

What was done:
- Bound `_H8ParasiteFrameParams` to `_rebaseKernel` before AUP-shift dispatch.
- Re-ran targeted static scans for old loose frame-param IDs/helpers, runtime `Time.frameCount`, shader native trig, shader variants, and report JSON parse.
- Re-sampled build guard: CPU load `73`, no `dotnet/csc/VBCSCompiler` process output, generated project files still contain no parasite assembly/script hits.
- Updated status, rationale, scanner, and shared report compile evidence.

Cinematic Cheats used:
- None added. This is resource-binding correctness for the existing GPU rebase path.

Exact Microseconds saved:
- Runtime: no measured saving claimed. Adds one buffer bind only on AUP-shift frames and prevents undefined/stale particle-budget reads in rebase.

<SELF_AUDIT polish="REBASE_FRAME_PARAM_BINDING">
Task 12: PASS STATIC - GPU AUP rebase now binds the same explicit frame-param row as the other compute kernels.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - CPU guard is 73 percent and generated Unity project files are still stale for SHINOBU_313.
</SELF_AUDIT>

## Session 2026-05-23 - Fail-Closed Resource And Sqrt-Free Target Polish

What was wrong:
- The active compute validation did not require `_rebaseKernel`, even though AUP-shift correctness depends on the rebase pass.
- Burst target scoring still paid for a scalar square root for a visual ranking score.
- Missing `parasiteMaterial` could trigger a runtime `Shader.Find` and owned fallback `Material` allocation, then hide asset misconfiguration behind generated gameplay objects.

What was done:
- Required `_rebaseKernel >= 0` for the active compute path.
- Replaced target-score sqrt with guarded squared-distance and `math.rsqrt` proxy math.
- Removed `ShaderName`, `_fallbackMaterialLookupAttempted`, `_ownedFallbackMaterial`, and `ResolveMaterial`; runtime now uses only the serialized material and records no-compute when it is absent.
- Tightened `CreateStructuredBuffer<T>`, `TryReadHandle<T>`, and `TryResolveHandle<T>` from `struct` to `unmanaged` so future GPU/Vault payload helpers cannot carry managed references.
- Added `Docs/Reports/SHINOBU_313_SELF_AUDIT.xml` as an on-disk forensic artifact with Task 20 explicitly pending Unity/compiler/runtime proof.
- Updated status, rationale, route card, ledger, scanner report text, and shared rendering report evidence.
- Re-sampled build guard: CPU load `99`, no compiler process output, generated projects still omit SHINOBU_313 files; build was not launched.

Cinematic Cheats used:
- The target distance remains a visual macro-attractor ranking proxy, not gameplay truth. Exact Euclidean distance is rejected in favor of squared range rejection plus `rsqrt`.
- Missing draw assets now fail closed; no CPU-created fallback material or hidden shader lookup is used to fake a configured render resource.

Exact Microseconds saved:
- Valid scenes: no measured frame-time claim until Unity profiler.
- Target extraction: removes one scalar sqrt per scored candidate; exact ns/sample pending Burst player proof.
- Misconfigured scenes: avoids one managed material allocation/search and suppresses all parasite compute dispatches when no material can draw the result.
- Build guard: saves developer workstation time by refusing a non-proving compile under 99 percent CPU load.

<SELF_AUDIT polish="FAIL_CLOSED_RESOURCE_SQRT_FREE_TARGETS">
Task 07: PASS STATIC - target scoring has no sqrt/length token and uses guarded `math.rsqrt`.
Task 08: PASS STATIC - active compute requires init/clear/advect/rebase/cull kernels plus assigned material.
Task 10: PASS STATIC - no runtime fallback material is created for indirect draw.
Task 12: PASS STATIC - missing rebase kernel no longer permits active compute.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>

## Session 2026-05-23 - Camera Snapshot Authority Tightening

What was wrong:
- `TryResolveCameraAup` still had a fallback that read `renderCamera.transform.position` and reconstructed AUP from a locally cached runtime origin.
- `ResolveAupShiftSignals` maintained that local origin cache, creating a VFX-owned shadow of player/world origin state.

What was done:
- Removed `_cachedRuntimeOriginAup` and `_hasCachedRuntimeOriginAup`.
- Removed cold `GlobalSignals.CurrentRuntimeOriginAup` fallback setup.
- `TryResolveCameraAup` now succeeds only through cached `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`.
- `ResolveAupShiftSignals` now records only pending GPU rebase delta and last rebase frame.
- Active compute now also requires `renderCamera != null`, so missing camera scenes do not fall through to an ambiguous indirect draw target.

Cinematic Cheats used:
- None added. This is authority cleanup for the existing camera-local GPU fake.

Exact Microseconds saved:
- Removes a fallback hot Transform property read, local origin mutation, and compute/draw work in null-camera scenes. Exact profiler value pending Unity runtime proof.

<SELF_AUDIT polish="CAMERA_SNAPSHOT_AUTHORITY">
Task 03: PASS STATIC - camera/AUP input now uses cached player runtime context snapshot only.
Task 12: PASS STATIC - AUP shift signal is consumed only as GPU rebase delta, not as local origin truth.
Task 20: FAIL PENDING UNITY IMPORT/COMPILE - static proof improved, but Unity import/compiler/runtime/profiler proof is still absent.
</SELF_AUDIT>
