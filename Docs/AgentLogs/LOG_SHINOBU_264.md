# SHINOBU_264 Log

## Session Start
What was wrong: Assignment requires async GPU readback for large-vessel buoyancy, but implementation state is unknown.
What was done: Extracted XML prompt, read AGENTS/domain/mandates, created fresh status and rationale files.
Cinematic Cheats used: Accepted delayed buoyancy as the "Dear Lie" instead of physically exact same-frame GPU truth.
Exact Microseconds saved: PENDING VERIFICATION.

## 2026-05-21 Async Readback Polish Hardening
What was wrong: Initial readback route still contained architectural liabilities: direct Atmosphere DTO/helper coupling, lazy readiness paths callable from dispatcher phases, IMGUI XRay tooling, string-line scanner logic, managed CSV byte allocation, and BinaryWriter-style dump risk.
What was done: Rebound runtime to Physics-owned `AsyncBuoyancyWaveParametersDTO`, moved Vault/GPU buffer setup to cold enable/hot-swap, scalarized the three readback slots, rewrote XRay as UI Toolkit with retained latency bars, rewrote the scanner as Roslyn AST with `Synchronous_GPU_Scanner` wrapper, moved CSV ingest into Vault `CsvScratch`, and changed fault dump to 16-byte header plus raw telemetry span rows.
Cinematic Cheats used: The readback path keeps the "Dear Lie": no current-frame stall, no same-frame GPU truth demand. Large vessels consume delayed height data plus smoothing/dead reckoning; low quality collapses to fewer sample points and cheaper wave lane contribution while high quality spends saved stall time on denser shader-side water.
Exact Microseconds saved: Static estimate remains `1000+ us` stall avoidance versus `ReadPixels`/blocking `GetData`; low-tier bandwidth drops from 2048 bytes at 128 samples to 64 bytes at 4 samples. Exact profiler microseconds are PENDING because CPU sampled at 100 percent and build/profiler execution is forbidden by protocol.
Verification artifacts: `Docs/Tasks/Status_SHINOBU_264.md`, `Docs/AgentLogs/Rationale_SHINOBU_264.md`, `Docs/Reports/SHINOBU_264_SELF_AUDIT.xml`, `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`, `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_264.json`, and `Docs/ARCHITECTURE/ASYNC_BUOYANCY_READBACK_SHINOBU_264.md`.

## 2026-05-21 Dynamic Wake Readback Patch
What was wrong: The compute readback kernel still returned Gerstner-only height, so Task 06's dynamic wake/shoreline displacement clause was only partially satisfied.
What was done: Added `_H8OceanWakeDisplacement` and `_H8OceanShorelineDepthParams` to `Hecton_WaveHeightSampler.compute`; the readback height now includes wake.z * 0.18 * wakeStrength after finite checks. `AsyncBuoyancyReadbackRuntime` binds the current global wake texture/vector and falls back to `Texture2D.blackTexture` without introducing a Physics-to-Rendering C# dependency.
Cinematic Cheats used: Dynamic wake force remains an optical shader texture tap, not a CPU wake simulation or synchronous texture read. The Dear Lie still consumes delayed data; sample count collapses continuously through `GlobalQualityWeight`.
Exact Microseconds saved: Still removes the millisecond-scale CPU/GPU fence versus `ReadPixels`/blocking `GetData`. Added wake cost is GPU-only: one texture tap per requested point when wake strength is nonzero, so 4 taps at low-tier 4 samples and 128 taps at ultra 128 samples.
Verification artifacts: Static `rg` verified wake bindings, zero Physics/Vehicles sync GPU calls except the allowed guarded `AsyncGPUReadbackRequest.GetData<T>()`, zero sibling `Hecton8.Atmosphere` references, no hot `NativeArray` allocations, no DTO properties, and updated reports/ledger/self-audit.

## 2026-05-21 Subagent Audit Closure
What was wrong: Readback request buffers were ringed, but wave-parameter upload had a single-buffer hazard; wake UV used only local XZ and was not AUP-stable; scanner proof missed `SetData`/allocation regressions; layout validation lacked pointer/stride proof; XRay editor labels updated every editor tick.
What was done: Added slot-matched wave parameter buffers with per-slot dirty hashes/counts, passed camera AUP modulo wake world size to the compute shader, sampled wake at `request.LocalXZ + cameraProjection`, extended Roslyn scanner coverage, enabled unsafe only for the domain editor validator, added temp NativeArray base pointer alignment proof, and throttled XRay refresh to 10Hz with unchanged-label suppression.
Cinematic Cheats used: Wake/shoreline interaction remains a single shader texture tap folded into delayed readback height. No CPU wake simulation, no synchronous texture read, and no same-frame truth demand were introduced.
Exact Microseconds saved: Still a static stall-avoidance estimate only: `1000+ us` avoided versus synchronous readback. Wave upload dirty hashes avoid redundant 64-byte slot uploads when wave rows are unchanged. Exact Burst apply worker time is not claimed; `ApplyMicros` is flagged `FlagApplyMicrosScheduleOnly` until Unity Profiler/SystemDispatcher timing exists.
Verification artifacts: `rg` checks show no runtime/job/contract `SetData`, `WaitForCompletion`, `.Complete`, `ReadPixels`, `GetPixel`, hot `new NativeArray`, runtime `new Texture2D`, runtime `new RenderTexture`, DTO properties, `Hecton8.Atmosphere`, or `HectonFloatingOrigin.CurrentTotalOffsetDouble` hits in the owned readback runtime path. Compile/build remains blocked by CPU gate.

## 2026-05-21 Fixed-Delta and Job-Admission Polish
What was wrong: The readback runtime still had a Unity `Time.fixedDeltaTime` fallback in simulation-adjacent code, empty frames could schedule a one-lane apply job, and an unused telemetry Burst job remained after direct 64-byte telemetry writing was installed.
What was done: Replaced the time fallback with dispatcher-owned `DispatcherTimingDTO.FixedDelta` plus cached last-valid delta, advanced readback/mock `_timeSeconds` by fixed delta instead of frame delta, skipped `ApplyDelayedBuoyancyReadbackJob` when there are zero active dispatched/completed samples, bounded apply lanes to actual active sample count, and deleted `RecordReadbackTelemetryJob`.
Cinematic Cheats used: The Dear Lie remains delayed height plus smoothing/dead reckoning; no current-frame GPU truth or physics-heavy water simulation was introduced.
Exact Microseconds saved: Empty frames avoid one scheduler submission. Low quality now applies actual active lanes instead of maximum configured capacity. Sync-readback stall avoidance remains the only millisecond-scale claim; exact profiler timing is still pending.
Verification artifacts: Focused async runtime/job/contract scan shows no `Time.`, no `RecordReadbackTelemetryJob`, no `WaitForCompletion`, no `.Complete`, no `SetData`, no runtime texture allocation, and one allowed `AsyncGPUReadbackRequest.GetData<T>()` guarded by `SystemDispatcher.IsAsyncReadbackReadyNoWait`.

## 2026-05-21 Second Subagent API Audit Closure
What was wrong: Vault reads/writes were not separated, teardown left stale readback metadata, saturated ring backlog could trigger mock data, runtime camera AUP could fall back to `Transform.position`, and upload depended on an internal Core helper.
What was done: Added pure `TryReadHandle` read helper, explicit `TryAcquireWriteLock` writer helper, dispatcher-window job write locks released in `PostSimulation`, `ResetReadbackRingState`, distinct `ReadbackDispatchStatus.RingBacklog`, shift-sequenced camera AUP publication, editor-only Transform fallback, and local `LockBufferForWrite` upload helpers.
Cinematic Cheats used: Backlog now preserves cached/dead-reckoned real data instead of mock injection. The Dear Lie remains latency smoothing, not fabricated water when the async ring is merely full.
Exact Microseconds saved: No new profiler claim. This pass prevents false mock force injection, stale re-enable drops, and future asmdef breakage. The established sync-readback stall avoidance remains the only millisecond-scale estimate.
Verification artifacts: Focused scans show no owned runtime `TryResolveHandle`, no `GraphicsBufferUploadUtility`, no `Time.`, no `SetData`, no `.Complete`, and one guarded `AsyncGPUReadbackRequest.GetData<T>()` at line 575.

## 2026-05-21 Static Compile-Risk Audit Under CPU Gate
What was wrong: Build verification remains forbidden because CPU sampled at 100%; previous broad scans mixed neighboring buoyancy-agent findings with SHINOBU_264 files.
What was done: Re-extracted the SHINOBU_264 batch block, re-read the 20 task lines, narrowed forbidden-pattern scans to owned async readback runtime/job/contract/editor/compute files, checked dispatcher/hot-swap/origin/vault signatures against source contracts, and validated the GPU upload/readback route against existing `LockBufferForWrite`/`AsyncGPUReadback` project patterns.
Cinematic Cheats used: No new simulation; this pass preserved the existing delayed-readback Dear Lie and dead-reckoned cached-height path.
Exact Microseconds saved: Not claimed. This is proof/integration-risk work while build remains gated by CPU.
Verification artifacts: CPU gate resampled at 94% and then 99%, still above the 50% threshold. Owned file brace/preprocessor counts balanced; SHINOBU_264 JSON/XML reports parse cleanly; owned-runtime forbidden-pattern scan returned no hits. An external `dotnet build Hecton8.Core.csproj --no-restore` process was observed and waited out; SHINOBU_264 did not launch it and does not claim its result because no output stream was available.

## 2026-05-21 Generated Project-File Coverage Gap
What was wrong: Unity-generated `.csproj` files are stale: `rg AsyncBuoyancyReadback -g *.csproj` returns no SHINOBU_264 source entries, so a `dotnet build Hecton8.Core.csproj` cannot currently prove these new files compile.
What was done: Recorded the limitation in status and rationale. `Hecton8.Core.csproj` was left untouched because it is explicitly generated/overwritten by Unity. The valid next proof is Unity asset import/project-file regeneration, then a CPU-gated build or Unity console check.
Cinematic Cheats used: None added in this verification pass. Runtime still uses delayed async readback plus smoothing/dead reckoning and avoids same-frame GPU truth.
Exact Microseconds saved: Not claimed. This is verification hygiene, preventing a false compile report while CPU is at 100% and an existing `dotnet build` process is active.
Verification artifacts: `Hecton8.Core.csproj` header states generated/overwritten by Unity; `Hecton8.Core.asmdef` owns the parent script tree; no SHINOBU_264 source appears in current generated project manifests.

## 2026-05-21 Ready Readback Writer-Lock Retry
What was wrong: A ready async readback slot was cleared before the completed-results Vault write lock was acquired, so lock contention could drop ready GPU data without a stall or a diagnostic.
What was done: Moved active-slot clearing until after zero-count classification or successful payload copy. If the completed-results writer lock is unavailable, the slot remains active for a later nonblocking retry.
Cinematic Cheats used: The Dear Lie stays intact: no blocking lock wait, no same-frame truth demand, and cached/dead-reckoned data remains the visual fallback.
Exact Microseconds saved: No new timing claim. The change prevents avoidable data loss under contention while preserving the zero-stall async path.
Verification artifacts: `ConsumeGpuReadbacksNoWait()` now clears `activeRef` only on count<=0, GPU error, or after `request.GetData<ReadbackRequestDTO>()` has been copied into the Vault completed buffer.

## 2026-05-21 Runtime Asmdef Isolation
What was wrong: SHINOBU async readback runtime/job/contract files were under the parent `Hecton8.Core` assembly tree, which weakened compile-wall proof even though no sibling-domain C# dependency was present.
What was done: Moved only the SHINOBU_264 files into `Assets/_Project/Scripts/Physics/Buoyancy/AsyncReadback`, added `Hecton8.Physics.Buoyancy.Runtime.asmdef`, and updated the editor asmdef to reference the runtime assembly. The root `Buoyancy` folder was not given an asmdef because it contains neighboring agents' files.
Cinematic Cheats used: None added. The delayed readback Dear Lie and shader wake tap route are unchanged.
Exact Microseconds saved: No frame microseconds claimed. This reduces assembly rebuild blast radius after Unity regenerates project files.
Verification artifacts: Runtime asmdef references only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, `Unity.Mathematics`, `Unity.Burst`, `Unity.Collections`, and `Unity.Jobs`; no sibling runtime asmdef reference was introduced.

## 2026-05-21 Shared Optimization Report Restoration
What was wrong: The shared `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` no longer contained SHINOBU_264 after another agent's report write.
What was done: Added a nested `shinobu264AsyncBuoyancyReadbackScanner` section without overwriting the current top-level report owner. It points to the dedicated report, asmdef route, async route, Dear Lie proof, and compile gate limitation.
Cinematic Cheats used: No new runtime cheat added. The report records the existing delayed readback plus smoothing/dead-reckoning fake.
Exact Microseconds saved: Not claimed. This is report integrity work.
Verification artifacts: Shared JSON parses and contains the SHINOBU_264 section plus `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_264.json`.
