# LOG_SHINOBU_16

## 2026-05-17 - Thermodynamics Macro-Grid Hazard Pass

What was wrong:
- Hazard damage was at risk of falling back to Unity trigger/radius thinking. That path scales with GameObject and broadphase count, not with a bounded simulation budget.
- No SHINOBU-owned heat/radiation scalar field existed for O(1) entity queries.
- Tuning and visualization would have been blind without a Vault-backed editor facade.
- Full Unity compile is currently blocked by external domains, not by `Hecton8.Thermodynamics`.

What was done:
- Added `HazardSourceDTO` with 40-byte ARM64-safe layout: `double3 AUP` 24B, `float Intensity` 4B, `float Radius` 4B, `uint HazardTypeHash` 4B, `uint _pad0` 4B.
- Added `MockHazardGenerator` with a static 1000C heat source and radiation leak.
- Added `ThermodynamicsHazardGridRuntime` with preallocated native thermodynamics buffers for temperature/radiation front/back grids, source grids, hazard sources, entity sample slots, signal staging, constants, and 300-frame telemetry. This initial ownership model is superseded by the H-Phi Vault eviction section below.
- Implemented Burst jobs: source reset, inverse-square emission with atomic float CAS, 6-neighbor diffusion, mock SDF shielding, entity trilinear damage sampling, AUP grid rebase, and telemetry scan.
- Added ping-pong front/back swaps after job completion; no per-frame grid allocation.
- Added radiation decay at 1 Hz via `RadiationDecayCoefficient`.
- Added `ThermalUpdraftSignal` and local unmanaged `MockDamageSignal`; damage and combat output are throttled to one second per entity.
- Added RFloat `Texture3D` upload for `_HectonThermoHazardHeatTex3D` plus grid metadata for shaders.
- Added `Thermodynamics Tuner` EditorWindow. Constants read/write through GlobalDataVault buffer `(BufferID)70016`; SceneView grid gizmos read Vault mirrors `(BufferID)70017/70018`.
- Added fixed-buffer CSV override ingestion for `hazard_profiles.csv`.
- Added black-box dump to `Docs/AgentLogs/Dump_THERMODYNAMICS.bin` and `Docs/AgentLogs/Dump_SHINOBU_16.bin` on NaN/fault.

Cinematic cheats used:
- Cellular automaton scalar diffusion instead of fluid convection.
- Mock SDF shielding as one multiply instead of terrain raymarching.
- 3D scalar texture heat haze instead of GameObject particle fields.
- Trilinear entity sampling over a coarse grid instead of collider residence.
- Hardware Math LOD: 32^3 high path, 16^3 toaster path.

Exact microseconds saved:
- Collider broadphase/trigger spam removed from SHINOBU path: exact saved time is scene-dependent and PENDING PROFILER PROOF.
- 16^3 decimation reduces cell iterations from 32768 to 4096, an 8x iteration cut. Exact frame-time delta is PENDING PROFILER PROOF.
- Ping-pong swap replaces full-grid copy for source-of-truth handoff; expected sub-1 us, PENDING PROFILER PROOF.
- Damage signals capped to one/sec/entity instead of 60/sec/entity, a 60x signal-rate reduction per exposed entity.
- Direct Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_r6.log` clean, emitted `Hecton8.Thermodynamics.dll` and `.ref.dll` at 2026-05-17 20:28:49.
- Full Unity compile wall: `Build_SHINOBU_16_unity_compile_r3.log` blocked by `FloraGenomeContracts.long3`, `FloraGenomeJobs.long3`, and `Shinobu19EconomyLedger.NativeMultiHashMap<,>`.

## 2026-05-17 - Ultra Polish H-Phi Eviction

What was wrong:
- The first complete pass still kept persistent thermodynamics state as private `NativeArray<T>` fields. That contradicted the Ultra mandate: persistent critical buffers belong in `GlobalDataVault`, not inside a MonoBehaviour.
- Visual sync used string shader property overloads. That is small, but it is still avoidable hot-path lookup debt.
- The previous self-audit did not prove all 20 tasks, all primary struct layouts, or H-Phi ownership with enough forensic detail.

What was done:
- Re-read `CURRENT_BATCH.md`, `Rationale_SHINOBU_16.md`, and `PROJECT_STATE_STATIC_XRAY.md`; task count remains 20.
- Replaced persistent grid/source/entity/signal/telemetry/CSV storage with `VaultBufferHandle<T>` ownership. Runtime IDs now span `(BufferID)70016-70038`.
- Kept method-local `NativeArray<T>` only as resolved Vault views; jobs still receive raw pointers, preserving Burst/L1 mutation behavior without private buffer ownership.
- Added static cached shader property IDs for `_HectonThermoHazardHeatTex3D` and `_HectonThermoHazardGridMeta`.
- Updated `Status_SHINOBU_16.md` and `Rationale_SHINOBU_16.md` with the full 20-task audit, ARM64 layout offsets, H-Phi check, AUP check, dependency check, Dear Lie check, blackbox check, and compile guard.

Cinematic cheats used:
- No new realism was added. The domain remains a bounded 16^3/32^3 cellular automaton with trilinear reads and mock SDF shielding.
- Ultra visuals stay decoupled: saved CPU budget is spent on heat texture quality and editor diagnostics, not on collider or particle truth.

Exact microseconds saved:
- Private NativeArray ownership eviction: runtime gain is not directly claimable without profiler capture; architectural gain is global buffer ownership and one memory authority.
- Cached shader property IDs: avoids repeated string property lookup on heat-grid visual sync; exact driver-side delta PENDING PROFILER PROOF.
- Collider damage remains zero in this domain; expected scene-dependent broadphase savings still PENDING PROFILER PROOF.
- Targeted Thermodynamics csc verification after Ultra polish: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_polish_r2.log` clean, 0 bytes, csc exit code 0.
- Full Unity compile was not re-run to protect iteration time; previous external wall remains documented in `Build_SHINOBU_16_unity_compile_r3.log`.

## 2026-05-17 - Ultra Polish I/O + ARM64 Staging

What was wrong:
- Binary constants and CSV overrides still had synchronous file-read code in cold/SlowTick paths. On Steam Deck MicroSD this can hitch the main thread even when the parser itself is fixed-buffer.
- Persistent combat damage staging used the external `CombatDamageSignal` contract directly. That weakened the ARM64 layout proof for SHINOBU-owned Vault memory.
- Fatal telemetry wrote `.bin` files only; the Ultra mandate explicitly asked for `.h8dump` outputs.

What was done:
- Added `ThermodynamicsHazardGridRuntime.FileWorker.cs`: one persistent background config worker, MMF read path, sequential stream fallback, timestamp short-circuit, and Vault-backed staging buffers.
- Changed `LoadConstantsOrEmergency()` to return emergency constants immediately; binary constants now override asynchronously when the worker stages valid bytes.
- Changed `SlowTick()` so it only enqueues CSV work. Main-thread `Tick()` only applies already-staged Vault bytes through `ApplyPendingConfigLoads()`.
- Added `ThermodynamicsCombatDamageSignal`, a local 64B sequential staging DTO. Burst jobs write this DTO; `PublishQueuedSignals()` converts to existing `CombatDamageSignal` on the stack.
- Added `Dump_THERMODYNAMICS.h8dump` and `Dump_SHINOBU_16.h8dump` alongside the existing `.bin` blackbox dumps.
- Audited existing `SignalWardenMockDamageSignal`; targeted csc showed it is not available from current referenced compiled contracts, so the prompt-required local `MockDamageSignal` remains. Production damage still uses existing `CombatDamageSignal`.

Cinematic cheats used:
- No new physical realism was added. Config loading and damage staging polish preserve the bounded 16^3/32^3 cellular automaton.
- Low tier still fakes terrain shielding with one scalar SDF multiply and smooths entity damage through trilinear interpolation.

Exact microseconds saved:
- Main-thread config file I/O removed from `Tick()`/`SlowTick()`: exact hitch avoidance requires MicroSD/player profiling; expected win is tail-latency stability, not steady-state arithmetic.
- Timestamp short-circuit prevents unchanged CSV/binary payload reads on the worker after metadata check.
- Local 64B damage staging removes persistent external-layout storage from SHINOBU Vault memory; runtime microsecond gain is not claimed without profiler proof.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_io_r8.log` clean, 0 bytes, csc exit code 0.
- Failed signal-lane attempt evidence: `Build_SHINOBU_16_thermo_csc_io_r3.log` showed `SignalWardenMockDamageSignal` unavailable; resolved locally, no compile wall.

## 2026-05-17 - Ultra Polish Stream Fallback Throughput

What was wrong:
- The background stream fallback still used `FileStream.ReadByte()`. It was not on the main thread, but it was still O(bytes) managed stream calls when MMF is unavailable.
- That path weakened the MicroSD storage audit for Task 19 even though the hot simulation path stayed clean.

What was done:
- Re-read `AGENTS.md`, re-extracted the full `SHINOBU_16` XML prompt, and re-read the relevant mandates for storage, zero-GC, native memory, signals, and AUP.
- Replaced the fallback byte loop with `Span<byte>` over the existing Vault-backed destination pointer and `FileStream.Read(Span<byte>)` chunk reads.
- Kept MMF as the primary path. No managed byte array, no `ArrayPool`, no `Task.Run`, and no main-thread file I/O were added.
- Static audit found no `ReadByte`, collider/trigger/overlap hazard damage, `System.Linq`, `foreach`, runtime find/get component calls, `Material.SetFloat`, `Instantiate`, `[StructLayout(Pack=1)]`, or persistent private `NativeArray<T>` fields in Thermodynamics.

Cinematic cheats used:
- No new physical realism was added. The solver remains the Dear Lie: coarse scalar diffusion, mock SDF shielding, trilinear sampling, and shader-fed heat haze.
- Low tier preserves the 16^3 grid and async constants bridge; Ultra tier spends freed stability on visual tuning, not gameplay collision spam.

Exact microseconds saved:
- `ReadByte()` fallback removed: expected gain is lower worker tail latency and fewer storage calls under MMF fallback. Exact milliseconds require MicroSD/player profiling.
- Main-thread hot path remains 0 file I/O for config loading.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_io_r9.log` clean, 0 bytes, csc exit code 0.

## 2026-05-17 - Ultra Polish Low-Tier Visual Bandwidth

What was wrong:
- The heat-haze `Texture3D` upload ran on every dirty grid version for both high and toaster tiers.
- Gameplay truth does need current scalar fields; low-tier visuals do not need every dirty upload when the grid is already degraded to 16^3.

What was done:
- Added `LowTierVisualUploadStride = 4` to VISUAL_SYNC.
- On the 16^3 path, the runtime now skips three out of four dirty visual uploads when the existing texture already matches the active resolution.
- Resolution changes still rebuild/upload immediately. High-tier 32^3 visual cadence remains unchanged.
- Scalar truth remains current for trilinear entity sampling, damage throttling, telemetry, updraft signals, and blackbox state.

Cinematic cheats used:
- Low-tier visual heat distortion now accepts a short visual lag while gameplay heat/radiation math stays authoritative.
- This buys bandwidth without adding particles, colliders, or richer physical simulation.

Exact microseconds saved:
- Low-tier heat texture uploads can drop by up to 75% during continuous grid changes. Exact frame-time/bandwidth savings are PENDING PROFILER PROOF.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_visual_r10.log` clean, 0 bytes, csc exit code 0.

## 2026-05-17 - Ultra Polish Phase-Safe Readback

What was wrong:
- `TrySample()` and editor Vault readback could call `CompleteForColdReadbackIfIdle()`.
- That helper routed into `LateFrameTick()`, so a read API could complete jobs, swap front/back buffers, publish signals, commit telemetry, and upload visuals outside the registered phase boundary.

What was done:
- Removed `CompleteForColdReadbackIfIdle()` entirely.
- `TrySample()` and editor grid readback now read only the stable front-buffer snapshot.
- Completed simulation work is resolved only in `LateFrameTick()` POST_SIMULATION/VISUAL_SYNC handoff, or in teardown via `ReleaseNativeState()`.
- Static scan now shows `.Complete()` only in the registered `LateFrameTick()` swap window and teardown.

Cinematic cheats used:
- The query path accepts one-frame snapshot latency instead of forcing immediate truth. That is the correct fake: smooth trilinear field reads stay believable without breaking phase order.

Exact microseconds saved:
- Removed a potential query-path sync point and hidden signal/texture fan-out. Exact savings are PENDING PROFILER PROOF.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_phase_r11.log` clean, 0 bytes, csc exit code 0.

## 2026-05-17 - Ultra Polish Front-Only Pointer Surface

What was wrong:
- `TryGetUnsafeGridPointers()` exposed owner back-buffer pointers through a public read API.
- The owner jobs need back buffers internally, but external consumers must not observe or mutate in-progress write targets.

What was done:
- Public unsafe pointer readback now populates only `TemperatureFront` and `RadiationFront`.
- `TemperatureBack` and `RadiationBack` remain in the DTO shape for compatibility but are null for public callers.
- Internal owner job scheduling still resolves back-buffer pointers privately for diffusion/rebase writes.

Cinematic cheats used:
- External consumers accept stable front-buffer snapshot reads rather than trying to chase the newest in-progress write. That preserves believable heat/radiation gradients without torn data.

Exact microseconds saved:
- No runtime speed claim. This is a correctness and cache-safety guard.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_frontonly_r12.log` clean, 0 bytes, csc exit code 0.

## 2026-05-18 - Ultra Polish AUP Telemetry + Source Ref Containment

What was wrong:
- `GetHazardSourceRef()` was public even though only internal mock seeding used it.
- A public ref into the Vault source array could bypass the active-job guard and race `EmissionJob`.
- Blackbox telemetry downcast absolute `_gridOriginAup` from `double3` to `float3`.

What was done:
- Made `GetHazardSourceRef()` private and implemented it through `UnsafeUtility.AsRef` over the Vault pointer.
- External source producers remain on `TryUpsertSource()`, which refuses mutation while simulation jobs are active.
- Replaced absolute AUP float telemetry with local `GridOrigin = float3.zero` plus millimeter-quantized `GridOriginHash`.
- Updated dump serialization to write `GridOriginHash`.

Cinematic cheats used:
- Telemetry stores a compact deterministic hash instead of wide absolute coordinates. It preserves crash correlation without bloating the 64B blackbox entry.

Exact microseconds saved:
- No runtime speed claim. This is race containment and AUP precision hardening.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_aup_r13.log` clean, 0 bytes, csc exit code 0.

## 2026-05-18 - Ultra Polish Blackbox Stride Integrity

What was wrong:
- `ThermodynamicsHazardTelemetryEntry` declared a 64B stride, but the field-based dump writer serialized only 56B after `GridOriginHash`.
- That makes the dump header lie to post-mortem readers and violates the forensic fixed-row expectation.

What was done:
- Added explicit telemetry tail padding fields `_pad0` and `_pad1`.
- `ScanTelemetryJob` zeroes both pads in Burst.
- `WriteDump()` now writes `GridOriginHash`, `_pad0`, and `_pad1`, so every telemetry row serializes exactly 64 bytes.
- Re-extracted the exact `SHINOBU_16` prompt with CLI; prompt length 11250 chars, task count 20.
- Static audit remains clean except for method-scoped `ResolveArray<T>()`, which is a Vault view resolver, not persistent ownership.

Cinematic cheats used:
- The blackbox stores a compact origin hash and fixed-width numeric fields instead of strings or variable payloads.
- No gameplay realism was added; the solver remains the coarse 16^3/32^3 cellular automaton.

Exact microseconds saved:
- No frame-time gain claimed. This is dump correctness and ARM64/stride discipline.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_blackbox_r14.log` clean, 0 bytes, csc exit code 0.

## 2026-05-18 - Ultra Polish Scalability Signal + Directed Updraft

What was wrong:
- `UsesLowResolution()` polled `GlobalRegistry.ScalabilityTier` from the Tick path.
- Task 13 explicitly names SystemHealthIndex pressure, but the runtime had no typed health-pressure latch.
- Vertical heat bias needed stronger proof that heat moves upward, not just an emitted updraft signal.

What was done:
- Added `IScalabilityChangedEventListener` support and `ScalabilityEvents` registration.
- Cached scalability tier on cold registration/event; Tick now uses cached tier state.
- Added `SignalBus<SystemHealthIndexSignal>` consumption. Critical/adrenaline pressure forces the 16^3 path for 120 frames.
- Added `TelemetryFlagHealthPressureLowTier` for blackbox correlation.
- Reworked vertical heat flux: gain from below and loss upward use the stronger coefficient; gain from above and loss downward use the weaker coefficient.

Cinematic cheats used:
- System pressure triggers coarse-grid load shedding instead of a richer simulation fallback.
- Updraft remains a directional scalar diffusion fake, not fluid convection.

Exact microseconds saved:
- Per-frame registry polling removed from resolution selection; exact savings are not claimed.
- Critical/adrenaline pressure can force the 16^3 path, reducing diffusion iterations from 32768 to 4096 while latched.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_scalability_r15.log` clean, 0 bytes, csc exit code 0.

## 2026-05-18 - Ultra Polish Editor AUP Facade

What was wrong:
- `ThermodynamicsTunerWindow` still cast absolute `double3 originAup` into Unity `Vector3`.
- It was editor-only, but it left a forbidden AUP precision pattern inside the required human-control facade.

What was done:
- SceneView gizmos now discard absolute origin from Vault readback and draw the macro-grid in local coordinates.
- `Task 20` evidence now covers local debug visualization: Vault mirrors are still the data source; live job-owned buffers are not read.
- Re-extracted `SHINOBU_16` prompt with CLI: prompt length 11250 chars, task count 20.
- Static audit found no `(float)originAup`, no `new Vector3((float)`, and no `(float3)_gridOriginAup` in Thermodynamics.

Cinematic cheats used:
- Editor visualization shows the shape and intensity of the hazard field locally instead of pretending absolute AUP can be represented by Unity float world coordinates.

Exact microseconds saved:
- No runtime speed claim. This is editor-only precision hardening.
- Targeted Thermodynamics csc verification: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_editoraup_r16.log` clean, 0 bytes, csc exit code 0.

