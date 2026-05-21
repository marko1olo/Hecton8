# SHINOBU_238 Status

Agent: SHINOBU_238
Domain: BIOLUMINESCENT_MATERIAL_SYNC_ARCHITECT
Task Count: 20
Status: STATIC SOURCE PASS; RUNTIME PROOF PENDING; COMPILE BLOCKED BY CPU GUARD

## Mandates Read Before Coding

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Execution_Phases.txt
- REND_Instanced_Flora_Physics.txt
- REND_GPU_Sovereignty.txt

## Loop 0 - Prompt Extraction

- [x] Extract SHINOBU_238 prompt from Docs/Tasks/CURRENT_BATCH.md
  - DOD: CLI regex scan read the full XML block by id.
  - Rejected: MCP/basic file read because batch protocol forbids truncated context.
  - Estimate: 80 us one-time CLI parse over markdown.

## Loop 1 - Tasks 01-05

- [x] Task 01: MATERIAL_INSTANCE_ERADICATION_PASS
  - DOD: `Assets/_Project/Scripts/Environment/Flora` does not exist; targeted scans over Environment, World/Biolum, VFX/Bioluminescence, indirect vegetation, and flora interaction found no per-flora `Material.SetFloat` / `sharedMaterial.SetFloat` / `GetComponent<Renderer>().material` mutation in this biolum route.
  - Rejected: editing unrelated plasma, voxel fade, impostor, radar, and resource ghost material paths outside the assigned domain.
  - Estimate: prevents an unbounded per-renderer material clone path; central route remains one matrix upload, approx 0.8-2.5 us saved per 1000 skipped renderer touches.
- [x] Task 02: MONOBEHAVIOUR_UPDATE_PURGE
  - DOD: targeted scans found no individual plant/coral/glow-rock `Update()` or `FixedUpdate()` emission animator in the assigned paths; runtime already uses dispatcher interfaces.
  - Rejected: deleting centralized dispatcher runtime because it is the required owner phase, not a per-object cosmetic loop.
  - Estimate: avoiding 10,000 cosmetic callbacks saves roughly 200-600 us/frame on weak desktop CPUs.
- [x] Task 03: CS1612_METADATA_STATE_ANNIHILATION
  - DOD: `BiolumPulseStateDTO` remains explicit 64B raw public `float4` rows; no get/set properties exist on pulse DTO rows; Burst uses unsafe refs for state access.
  - Rejected: C# properties and boxed metadata wrappers because they create copies and break direct L1 writes.
  - Estimate: direct row writes avoid four defensive struct copies, approx 0.2-0.6 us/frame at this state count.
- [x] Task 04: ARM64_PULSE_LAYOUT_ASSERTION
  - DOD: added `BiolumPulseLayoutGuard` editor assertion using `UnsafeUtility.SizeOf` and field offsets for 16/32/64B DTO contracts.
  - Rejected: runtime-only reflection logging because it allows broken constant-buffer layout to reach play mode.
  - Estimate: zero runtime cost; prevents 16B row misalignment regressions before GPU upload.
- [x] Task 05: EMERGENCY_MOCK_ECLIPSE_GENERATOR
  - DOD: mock init path now uses `GenerateMockLightingStateJob` with mandated `FloatMode.Fast` Burst attributes and depth-aware darkness scalar input.
  - Rejected: direct Celestial dependency because Agent 129 ownership is external and would create a brittle compile dependency.
  - Estimate: one 64B pulse state write at cold boot, approx 1-3 us.
- Verification after Loop 1: compile not launched; CPU guard measured 100 percent.

## Loop 2 - Tasks 06-10

- [x] Task 06: BURST_GLOBAL_OSCILLATOR_KERNEL
  - DOD: `AdvanceBiolumPhasesJob` uses mandated `FloatMode.Fast` Burst attributes, advances phases with `RepeatRadians`, and now locks/resolves sync pulse buffers before job scheduling.
  - Rejected: same-frame per-plant sine evaluation on CPU.
  - Estimate: four group rows cost approx 1-5 us CPU versus hundreds of us for large renderer traversal.
- [x] Task 07: THE_DEAR_LIE_SHADER_EVALUATION
  - DOD: `_GlobalBiolumDearLieGroups` is uploaded through `Shader.SetGlobalMatrix`; shader selects matrix row by sync group and computes apparent individual autonomy.
  - Rejected: material instance floats and keyword toggles.
  - Estimate: one 64B global upload, approx 1 us CPU-side API work excluding render thread internals.
- [x] Task 08: SPATIAL_WAVE_PROPAGATION_MATH
  - DOD: shader wave functions now consume `localAupCoord`; fragment wave uses `input.positionWS - input.originWS`, not absolute world position.
  - Rejected: absolute world floats because they jitter at large AUP offsets.
  - Estimate: no extra CPU cost; GPU ALU remains continuous-quality gated.
- [x] Task 09: ECLIPSE_AND_DEPTH_ACTIVATION_LINK
  - DOD: darkness scalar combines mock ambient eclipse threshold and AUP depth activation, then multiplies matrix amplitudes inside both mock init and oscillator jobs.
  - Rejected: polling Celestial or player transforms in the hot render loop.
  - Estimate: approx 0.1 us in the 4-row job; disables global glow via multiplication.
- [x] Task 10: PREDATOR_PROXIMITY_OVERRIDE_ROUTING
  - DOD: local `MockPredatorProximitySignal` remains DataVault-decoupled; predator strength drives panic speed, amplitude gain, and fixed-slot pulse injection.
  - Rejected: plant event listeners and direct dependency on Apex AI internals.
  - Estimate: fixed 16-slot pulse scan, approx 1-4 us worst case.
- Verification after Loop 2: compile not launched; CPU guard measured 100 percent.

## Loop 3 - Tasks 11-15

- [x] Task 11: ASYNCHRONOUS_GPU_VARIABLE_UPLOAD
  - DOD: visual sync path publishes matrix once from `PublishDearLieGroups`; fallback publishes zero matrix on disable.
  - Rejected: per-material upload loops and array allocations.
  - Estimate: constant one call/frame, approx 1 us CPU-side.
- [x] Task 12: CONTINUOUS_SCALABILITY_SHADER_MATH
  - DOD: `GlobalQualityWeight` is passed into `_GlobalBiolumParams.y`; shader blends vertex pulse, pixel wave, and overkill interference through continuous math.
  - Rejected: binary low/high quality switches.
  - Estimate: weak GPUs interpolate vertex pulse; high/ultra spend ALU on interference without C# branches.
- [x] Task 13: AUP_PRECISION_EPICENTER_MATH
  - DOD: sync pulse job subtracts event AUP from local AUP reference using `AupPrecisionMath.LocalDeltaDouble` before float downcast.
  - Rejected: sending double AUP or absolute float world positions to shader.
  - Estimate: fixed per active pulse; prevents precision tearing at large map offsets.
- [x] Task 14: ROLLBACK_NETCODE_STATE_FENCE
  - DOD: targeted Merkle/StateRingBuffer scan found no `BiolumPulseStateDTO` inclusion; route remains presentation-only.
  - Rejected: adding biolum phase to deterministic gameplay truth.
  - Estimate: saves bandwidth/hash churn for 64B visual matrix per rollback frame.
- [x] Task 15: ZERO_INIT_OVERHEAD_BYPASS
  - DOD: pulse state handle is requested with `NativeArrayOptions.UninitializedMemory`; Burst mock/default job overwrites the row during cold boot.
  - Rejected: `MemClear` or ClearMemory for the pulse matrix.
  - Estimate: tiny at size 1, but keeps policy intact; approx 0.1 us cold boot.
- Verification after Loop 3: compile not launched; CPU guard measured 100 percent.

## Loop 4 - Tasks 16-20

- [x] Task 16: TELEMETRY_BIOLUM_RECORDER
  - DOD: 300-entry black-box ring remains in DataVault; dump path corrected to `Docs/AgentLogs/Dump_SHINOBU_238.bin`; job overrun/NaN flags write forensic dump.
  - Rejected: old BIOLUM_DIRECTOR dump name and chat-only failure reports.
  - Estimate: one 32B telemetry write/frame, approx 0.2-0.5 us.
- [x] Task 17: BIOLUM_TUNER_EDITOR_WINDOW
  - DOD: Abyssal Glow Tuner now copies telemetry through one `CopyEditorTelemetryEntries(Span<BiolumPulseTelemetryEntry>)` Vault lock and draws a preallocated bar graph.
  - Rejected: `TryReadEditor*` read-like facades that mutate locks, per-entry telemetry locks, and allocating editor arrays on refresh.
  - Estimate: editor-only cost; no player runtime cost.
- [x] Task 18: CSV_PULSE_PROFILES_INGESTOR
  - DOD: existing cold CSV parser uses byte scratch, deterministic token hash, manual float parsing, and Vault profile writes.
  - Rejected: `float.Parse`, strings, and managed row objects.
  - Estimate: cold path only; hot path zero.
- [x] Task 19: LIVE_PULSE_DEBUG_GIZMO
  - DOD: tuner reads `BiolumPulseStateDTO` and fades four UI boxes from `sin(Phase) * Amplitude`.
  - Rejected: scene probe cameras or flying to flora for verification.
  - Estimate: editor-only diagnostic; no player runtime cost.
- [x] Task 20: SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION
  - DOD: static audit checks matrix layout, `RepeatRadians`, global matrix upload, localized shader coordinates, no assigned-domain material float mutations, and no rollback inclusion.
  - Rejected: declaring runtime/profiler proof without a successful Unity compile/import.
  - Estimate: no runtime cost.
- Verification after Loop 4: compile not launched; CPU guard measured 100 percent.

## Loop 5 - Strict Self-Read

- [x] Re-read runtime/shader/editor diffs and searched exact forbidden patterns.
  - Findings: no `Material.SetFloat` in assigned biolum paths; no plant Update animator in target paths; compile-sensitive sync pulse lock bug fixed; mock lock leak fixed; dump path fixed.
  - Remaining risk: Unity compile/import and profiler proof not executed because CPU guard stayed above 50 percent.

## Loop 6 - Ultra Polish Mandate Continuation

- [x] Re-read `Status_SHINOBU_238.md`, `Rationale_SHINOBU_238.md`, `AGENTS.md`, route docs, and the full SHINOBU_238 XML block from `CURRENT_BATCH.md`.
  - DOD: corrected XML regex for extra tag attributes; prompt extraction reports 20 tasks and hash `9d5db96674f0d27a`.
  - Rejected: trusting compressed chat memory.
  - Estimate: static proof only, no runtime cost.
- [x] Purged read-like editor facades.
  - DOD: `TryReadEditor*` telemetry/tuning/weather/pulse/control APIs are now `CopyEditor*`; telemetry copy locks the black-box ring once for up to 16 UI samples.
  - Rejected: read accessor names that lock DataVault and repeated per-entry locks in the editor refresh path.
  - Estimate: editor-only; removes 15 redundant lock attempts per telemetry graph refresh.
- [x] Preserved Burst route for cold mock seed.
  - DOD: direct `GenerateMockLightingStateJob.Execute()` was replaced by `job.Run()` so the cold mock path uses the job/Burst entry route without scheduling a tiny hot-path job.
  - Rejected: scheduling and completing a one-row mock init job in a hot lane.
  - Estimate: cold boot/editor seed only; no gameplay hot-path cost.
- [x] Added SHINOBU_238 architecture route card and binary-ledger boundary row.
  - DOD: route card records owner, phases, Vault lanes, copy-facade purity, layout, failure modes, proof required before GREEN; ledger row records biolum binary profile evidence and Data Monolith absence.
  - Rejected: chat-only architecture claims.
  - Estimate: documentation-only.

## Loop 7 - Shader Consumer AUP Sweep

- [x] Localized remaining SHINOBU_238 flora/coral/bio global-biolum shader consumers.
  - DOD: `Hecton_CoralMaster`, `Hecton_CoralMaster_GPUI`, `Hecton_KelpMaster`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, and `Hecton_ProceduralBio` now derive global biolum selector/filament waves from finite local coordinates instead of injecting `_GlobalBiolumAupOffset` into phase math.
  - Rejected: absolute-float continuity tricks using `_GlobalBiolumAupOffset.x/z`, because they reintroduce precision drift at large AUP offsets.
  - Estimate: zero CPU cost; same GPU ALU shape, fewer high-magnitude float operands.
- [x] Preserved external-domain boundary.
  - DOD: Leviathan/fish organic shaders were identified as residual global-biolum consumers but not edited because they belong to fauna/creature presentation ownership, not flora/coral/biostructure.
  - Rejected: cross-domain shader edits without a route card or explicit assignment.
  - Estimate: documentation-only.
- [x] Recorded generated project-file compile-wall caveat.
  - DOD: `Hecton8.Core.csproj` still includes `Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs`, while the authoritative Unity asmdef is `Hecton8.VFX.Bioluminescence.Runtime`.
  - Rejected: editing generated `.csproj` metadata during a domain shader/runtime pass.
  - Estimate: no runtime cost; compile-wall hygiene item for project regeneration/integrator pass.

## Loop 8 - Post-Compaction Static Reconciliation

- [x] Re-read disk memory before responding.
  - DOD: `Status_SHINOBU_238.md`, `Rationale_SHINOBU_238.md`, and the SHINOBU_238 block from `CURRENT_BATCH.md` were read again after context compaction.
  - Rejected: trusting compressed chat memory.
  - Estimate: documentation/static proof only.
- [x] Corrected prompt task-count evidence.
  - DOD: verified that the source prompt uses literal `Task 01` through `Task 20` lines, not XML `<task id>` elements; the authoritative prompt hash remains `9d5db96674f0d27a`.
  - Rejected: the transient `<task id>` counter that returned zero because it targeted the wrong syntax.
  - Estimate: no runtime cost.
- [x] Re-checked assigned source and shader hot-path patterns.
  - DOD: static scans over assigned runtime/editor/shader files found no `Material.SetFloat`, `sharedMaterial.SetFloat`, renderer `.material`, `_GlobalBiolumAupOffset.x/z` selector math, `WorldPos.x` pulse math, `TryReadEditor*`, managed LINQ/ToArray, Unity random, or time-delta hot usage.
  - Rejected: expanding into fauna/Leviathan shader ownership without a route card.
  - Estimate: no runtime cost.
- [x] Re-ran CPU build guard.
  - DOD: CPU reported 100 percent; `dotnet` and `csc` process counts were zero.
  - Rejected: launching compile/import under the explicit >50 percent CPU prohibition.
  - Estimate: avoids competing with other active agents and local IO.

## Loop 9 - Legacy Float Route Retirement

- [x] Proved `_HectonLegacyBiolumIntensity` and `_BiolumPulseTime` were dead live shader routes.
  - DOD: live source scan found those identifiers only in `HectonBiolumController` and stale guide text; first-party shaders had zero readers.
  - Rejected: keeping a second biolum shader authority beside `_GlobalBiolumDearLieGroups`.
  - Estimate: removes up to two legacy `Shader.SetGlobalFloat` calls on signal/sonar pulses and one slow-tick scalar write; the larger win is authority cleanup, not steady frame time.
- [x] Removed legacy global-float publication from `HectonBiolumController`.
  - DOD: the controller no longer calls `Shader.SetGlobalFloat`, no longer stores `_BiolumPulseTime`, and no longer uses `Time.time` for biolum presentation.
  - Rejected: deleting the controller because it still owns world/lore registration and local proxy-light reactions outside this VFX matrix task.
  - Estimate: 0.2-2 us saved on affected event frames depending on Unity global-property cost and render-thread sync state.
- [x] Updated stale guide ownership text.
  - DOD: `LORE_SYSTEMS_GUIDE.md` no longer advertises `_BiolumPulseTime` or `_BiolumIntensity` as `HectonBiolumController` globals; runtime shader globals are documented under `BiolumPulseSyncRuntime`.
  - Rejected: leaving stale documentation that would invite future agents to reintroduce scalar pulse globals.
  - Estimate: documentation-only.

## Loop 10 - Cold Mock Lock Surface Trim

- [x] Re-read the runtime diff and removed an unnecessary cold-path lock.
  - DOD: `GenerateMockLightingState()` now locks only pulse state, profile floats, mock weather, and mock predator rows; sync pulse buffers are locked only by code paths that read/write sync pulses.
  - Rejected: locking sync pulse buffers during mock lighting seed merely to prove handle existence.
  - Estimate: cold boot/editor seed only; removes two DataVault lock attempts from that path and keeps lock order narrower.
- [x] Added Unity meta for the new editor layout guard.
  - DOD: `BiolumPulseLayoutGuard.cs.meta` exists with a stable GUID.
  - Rejected: relying on Unity import to generate a local GUID.
  - Estimate: integration hygiene only; avoids asset database churn.

## Loop 11 - Vault Rebind Job Fence Hardening

- [x] Removed hidden force-complete from the editor profile reload route.
  - DOD: `ReloadProfilesFromDiskEditor()` now calls `TryFinalizeScheduledJobForEditorReload()`; it returns if the oscillator job is still running instead of force-completing it.
  - Rejected: reusing teardown completion for manual editor tuning reloads.
  - Estimate: editor-only; prevents a possible frame hitch from manual profile reload while the job is active.
- [x] Fenced Vault generation/rebind handle invalidation.
  - DOD: `BindDataVault()`, `EnsureVaultBuffers()`, and `TryRefreshExistingVaultHandlesNoAllocate()` call `FenceScheduledJobBeforeVaultHandleInvalidation()` before dropping or replacing cached Vault handles.
  - Rejected: releasing handles while `_stateJobScheduled` or `_jobLocksHeld` can still point jobs at old native views.
  - Estimate: rare hotswap/compaction path; prevents stale NativeArray and locked-buffer leaks, no steady-frame cost.

## Loop 12 - Support Vector Ownership Proof

- [x] Audited `_BiolumIntensity` writers after legacy scalar retirement.
  - DOD: `BiolumPulseSyncRuntime` remains the active owner of `_BiolumIntensity` while `_GlobalBiolumParams.x > 0.5`; `HectonBiolumManager` has a source guard that suppresses its legacy write/reset in that state.
  - Rejected: deleting the manager's fallback write because it still supports non-PulseSync world/flora fallback routes when the matrix owner is not publishing.
  - Estimate: hot matrix route avoids a competing global vector write; worst affected frame saves one legacy `Shader.SetGlobalVector` call and, more importantly, preserves single-route shader authority.
- [x] Removed residual "legacy" terminology from the matrix-derived intensity helper.
  - DOD: `ResolveLegacyBiolumIntensity` is now `ResolveMatrixDerivedBiolumIntensity`.
  - Rejected: keeping old naming that suggests a second legacy shader lane.
  - Estimate: source hygiene only; no runtime delta.
- [x] Reconciled cold mock job aliasing contract.
  - DOD: `GenerateMockLightingStateJob` keeps weather and predator rows write-capable with `[NoAlias]` because the job seeds deterministic mock weather and resets predator state during cold/editor bootstrap.
  - Rejected: `[ReadOnly]` on those fields because the job mutates `WeatherSignal[0]` and `PredatorSignal[0]` and would fail compilation.
  - Estimate: cold job only; no steady hot-frame delta.
- [x] Corrected route-card capacity wording.
  - DOD: docs now state the exact ABI: 4 shader matrix rows in one `float4x4`, 16 cold profile slots, 16 sync pulse rows, and 300 telemetry rows.
  - Rejected: stale "8 group rows" wording because the GPU constant buffer cannot expose 8 rows through one `Matrix4x4`.
  - Estimate: documentation-only; prevents future agents from widening the shader ABI accidentally.

## Loop 13 - Shader Matrix Row ABI Correction

- [x] Corrected assigned shader consumers that treated matrix rows as color payloads.
  - DOD: `Hecton_CoralMaster`, `Hecton_CoralMaster_GPUI`, `Hecton_KelpMaster`, `Hecton_KelpMaster_GPUI`, `Hecton_SargassumMaster`, and `Hecton_ProceduralBio` now use deterministic group tint helpers and read amplitude from `state.z` / `secondaryState.z`.
  - Rejected: storing RGB in `_GlobalBiolumDearLieGroups` because the established row ABI is `phase, frequency, amplitude, spatialOffset` and the route is constrained to one `Matrix4x4`.
  - Estimate: no CPU cost; GPU ALU shape is effectively unchanged, but prevents phase/frequency/amplitude data from corrupting emitted color and brightness.
- [x] Preserved legitimate spatial-offset `.w` usage.
  - DOD: post-patch scan only reports `state.w` / `secondaryState.w` in `Hecton_IndirectVegetation`, where `.w` is used as spatial wave offset, not intensity.
  - Rejected: deleting `.w` globally because `_GlobalBiolumDearLieGroups[row].w` is the intended spatial offset field for shader wave phase.
  - Estimate: zero frame cost; preserves the Dear Lie wave phase without extra constants.

## Loop 14 - Emergency Mock Glow Coverage

- [x] Removed uninitialized tail risk from mock glow Vault rows.
  - DOD: `GenerateEmergencyMockGlows()` now seeds up to `MaxGlowInstances` glow state/AUP rows instead of only `SyncGroupCount` rows.
  - Rejected: leaving 49,996 uninitialized rows in the fallback CI/mock data path after requesting a 50,000-row Vault buffer with uninitialized memory.
  - Estimate: cold bootstrap/editor seed cost increases by a bounded 50,000-row unmanaged fill; hot runtime cost remains 0 us.
- [x] Corrected black-box active glow telemetry.
  - DOD: `BiolumPulseTelemetryEntry.ActiveGlowingInstances` now records `_activeGlowingInstanceCount` clamped to `MaxGlowInstances` instead of hardcoded `SyncGroupCount`.
  - Rejected: reporting four groups as four active glow instances because the group count is shader ABI capacity, not instance capacity.
  - Estimate: one integer clamp in telemetry write, approx below 0.05 us/frame; forensic count becomes meaningful.

## Loop 15 - First 20 Minutes Route Binding

- [x] Added route-impact binding to the SHINOBU route card.
  - DOD: route card now states the served First 20 Minutes moments: World load, Swim, and Hazard readability.
  - Rejected: treating biolum sync as broad visual overkill detached from the Copper Wire route.
  - Estimate: documentation-only; prevents route-card rejection under the product slice contract.

## Loop 16 - Sidecar Review Fixes

- [x] Replaced residual object-shader `positionWS` global-biolum coordinates with object-relative local deltas.
  - DOD: coral, kelp, sargassum, and procedural-bio shader varyings now store `positionWS - objectOriginWS` or the drift-aware equivalent before global-biolum phase selection.
  - Rejected: using floating-origin `positionWS` as a proxy local coordinate because it still varies with object placement and weakens the local-AUP vertex requirement.
  - Estimate: zero CPU cost; one vertex subtraction per affected shader vertex, preserving local wave stability.
- [x] Removed player hot-path CSV file I/O.
  - DOD: `Tick()` calls `ApplyCsvOverridesIfReady()` only under `UNITY_EDITOR`; CSV watcher/path/file-stream methods and fields are editor-only compilation surface, so player runtime no longer reaches or compiles that file-I/O bridge in the SHINOBU runtime class.
  - Rejected: shipping CSV file polling or watcher methods as runtime gameplay surface; designer hot reload remains editor tooling.
  - Estimate: player hot path removes potential file-system stall/allocation; editor-only reload cost remains cold/diagnostic.
- [x] Retired dead `_GlobalBiolumPhase` scalar global.
  - DOD: live `Assets/_Project` source scan returns no `_GlobalBiolumPhase`; docs keep only audit/status references. The shader bridge and global dispatcher no longer publish that scalar, and dead coral declarations were removed.
  - Rejected: keeping a transitive scalar fallback beside `_BiolumMasterPhase` and `_GlobalBiolumDearLieGroups`.
  - Estimate: removes one legacy scalar global write from the biolum master phase dispatch path; exact render-thread saving pending profiler proof.
- [x] Corrected route-card phase wording to actual dispatcher interfaces.
  - DOD: route card now states `IUpdatable.Tick` and `ILateFrameTickable.LateFrameTick` behavior instead of claiming unproven formal POST_SIMULATION/VISUAL_SYNC phase hooks.
  - Rejected: documentation that overclaims phase placement relative to current code.
  - Estimate: documentation-only; avoids integration false proof.

## Loop 17 - CSV Player Surface Hardening

- [x] Trimmed CSV hot-reload compilation surface from player runtime.
  - DOD: `_csvOverridePath`, `_csvWatcher`, `_csvLastWriteTicks`, `_csvWorkerState`, watcher callbacks, CSV scratch file read, and CSV path resolution are now under `UNITY_EDITOR`; player build keeps the unmanaged parser code but has no callable CSV file-I/O bridge in `BiolumPulseSyncRuntime`.
  - Rejected: relying only on guarded call sites, because private runtime file-I/O methods are still accidental future call surface.
  - Estimate: no steady-frame delta; removes player compile/link surface and accidental filesystem stall risk.
- [x] Re-ran sidecar static scans.
  - DOD: live `Assets/_Project` scan has no `_GlobalBiolumPhase`; assigned shader scan has no raw `biolumLocalAupCoord = positionWS` or `positionInputs.positionWS`; matrix ABI scan reports only indirect vegetation `.w` spatial-offset use.
  - Rejected: source/docs scan for `_GlobalBiolumPhase` as a failure condition because docs intentionally keep audit/status references.
  - Estimate: static proof only.
- [x] Re-ran diff and build guards.
  - DOD: `git diff --check` passes with only existing LF-to-CRLF warnings; CPU guard reports `CpuPercent=100 Dotnet=0 Csc=0`.
  - Rejected: launching compile/import under the explicit >50 percent CPU prohibition.
  - Estimate: avoids adding compiler IO/CPU load during other active work.

## Loop 18 - CSV Route Documentation Reconciliation

- [x] Updated architecture docs to match the editor-only CSV boundary.
  - DOD: route card now calls `BiolumCsvScratch` an editor tooling bridge and says player hot path does not poll CSV; ledger says CSV hot reload is editor-only through Vault scratch.
  - Rejected: leaving stale "CSV hot reload" wording that could be read as a player runtime route.
  - Estimate: documentation-only; prevents future route drift.
- [x] Verified documentation wording.
  - DOD: route-card/ledger scan shows editor-only CSV wording in the SHINOBU_238 boundary rows.
  - Rejected: broad ledger edits outside SHINOBU_238 rows.
  - Estimate: static proof only.

## Loop 19 - Continuous Matrix Row Count Hardening

- [x] Removed discrete tier switch from matrix row publication.
  - DOD: `ResolveStateCount(HectonQualityTier)` and `_activeStateCount` were removed; cold-start and hot publish now consistently use the fixed 4-row matrix ABI while `GlobalQualityWeight` controls cadence/amplitude/quality continuously.
  - Rejected: tier-based 1/4/16 active-state branch because it could produce a cold-start row-count pop and violates the continuous scalability law.
  - Estimate: no steady-frame cost; removes one cold branch and one stale field, preserving 64-byte shader payload.

## Loop 20 - Shader Variant And Cold-Route Audit

- [x] Proved SHINOBU_238 did not expand shader variant surface.
  - DOD: diff scan over assigned shaders found no added `#pragma`, `multi_compile`, or `shader_feature` lines; existing variant debt remains pre-existing and outside this patch's expansion surface.
  - Rejected: adding quality keywords or shader-feature branches for low/high glow because `GlobalQualityWeight` already drives continuous shader math.
  - Estimate: no frame-time delta; prevents first-use shader variant hitch risk from this change set.
- [x] Re-checked CSV and file I/O route context.
  - DOD: CSV watcher state, override apply, path resolution, and CSV `FileStream` read are inside `UNITY_EDITOR`; remaining `FileStream` routes are cold binary profile load and fault dump.
  - Rejected: player-runtime CSV polling or background file watcher bridge.
  - Estimate: player hot path removes unbounded filesystem stall/allocation risk; cold binary load and fault dump remain outside gameplay cadence.
- [x] Re-ran CPU build guard.
  - DOD: latest guard reported `CpuPercent=100 Dotnet=0 Csc=0`.
  - Rejected: launching compile/import under the explicit >50 percent CPU prohibition.
  - Estimate: no runtime cost; protects iteration/IO while other agents saturate the workstation.

## Loop 21 - Sidecar P1 Fault Dump I/O Fix

- [x] Removed synchronous dump file I/O from player fault call stack.
  - DOD: `DumpBlackBox()` now copies the 16-byte header plus 300 telemetry entries into a cold-allocated 9,616-byte snapshot buffer and signals `H8_SHINOBU_238_BlackBoxDump`; `Directory.CreateDirectory` and `FileStream` are isolated to the background writer.
  - Rejected: writing `Docs/AgentLogs/Dump_SHINOBU_238.bin` directly from `Tick()`/`LateFrameTick()` on NaN, AUP invalid, or overrun.
  - Estimate: fault frame removes synchronous path construction, directory creation, and 2x file writes; steady-frame cost is unchanged.
- [x] Kept dump proof artifact route.
  - DOD: worker still writes `Dump_SHINOBU_238.bin` and `.h8dump` from the copied snapshot; teardown signals and waits for the worker outside the gameplay tick lane.
  - Rejected: deleting the dump file route or making it editor-only, because the black-box mandate requires a forensic artifact.
  - Estimate: background I/O cost moves off the main frame; cold worker allocation is one thread/event/buffer at owner enable.

## Loop 22 - Sidecar P2/P3 Route Fixes

- [x] Clamped black-box dump snapshot to the fixed forensic artifact size.
  - DOD: `CopyBlackBoxDumpSnapshot()` now writes at most `BlackBoxFrameCount` entries into the 9,616-byte buffer, records the clamped `EntryCount`, and copies the newest source-ring window when the Vault buffer is larger than 300 rows.
  - Rejected: trusting `BiolumBlackBox` Vault length to stay exactly 300 after `TryLockBlackBoxBuffer()` accepts `Length >= BlackBoxFrameCount`.
  - Estimate: fault-only; prevents out-of-bounds dump-scratch writes with no steady-frame cost.
- [x] Hardened dump worker lifecycle.
  - DOD: `QueueBlackBoxDumpWrite()` rejects dead workers, `EnsureBlackBoxDumpWorker()` clears/recreates non-alive thread references, and `StopBlackBoxDumpWorker()` records timeout failure instead of silently leaving stale state.
  - Rejected: leaving a timed-out `_blackBoxDumpThread` reference that makes the next enable skip forensic worker creation.
  - Estimate: no steady-frame cost; keeps crash-dump route available after enable/disable churn.
- [x] Moved shader scalar upload out of simulation `Tick()`.
  - DOD: `Tick()` now updates CPU-side state, schedules jobs, and records telemetry only; `LateFrameTick()` publishes matrix/scalars via `UploadShaderGlobals()` after job finalization or from the cached matrix when the job is still pending.
  - Rejected: writing shader globals from the simulation/update phase while claiming VISUAL_SYNC ownership.
  - Estimate: same number of shader global writes per visual frame, but the route is phase-correct and no longer double-books presentation upload in `Tick()`.

## Loop 23 - Editor Facade Player-Surface Trim

- [x] Removed editor tuning facades from player compilation surface.
  - DOD: `CopyEditor*`, `TryWriteEditor*`, and `TryTriggerEditorGlobalPulse()` static facades are wrapped in `UNITY_EDITOR`; only the editor tuner window calls them.
  - Rejected: compiling DataVault-locking editor tooling APIs into player builds as public runtime surface.
  - Estimate: no frame-time delta; reduces accidental player call surface and strips editor-only Span/DataVault copy tooling from shipping runtime.
- [x] Corrected editor telemetry copy when black-box Vault capacity is larger than 300.
  - DOD: `CopyEditorTelemetryEntries()` now wraps indices against the source buffer length while still copying at most the 300-frame forensic window.
  - Rejected: clamping the cursor to the 300-window length when the source ring is larger.
  - Estimate: editor-only correctness; no player runtime cost.

## Loop 24 - Burst Directive Compliance

- [x] Corrected Burst float mode for presentation-only jobs.
  - DOD: `GenerateMockLightingStateJob` and `AdvanceBiolumPhasesJob` now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
  - Rejected: `FloatMode.Deterministic` on SHINOBU_238 VFX presentation jobs, because this route is not rollback, kinematics, or authoritative gameplay integration.
  - Estimate: no layout or authority change; Burst is allowed to use fast math for the four-row visual oscillator.

## Loop 25 - Matrix Row Shader Authority Completion

- [x] Removed residual scalar phase/amplitude consumers from assigned shaders.
  - DOD: `Hecton_IndirectVegetation`, coral, kelp, sargassum, and procedural-bio assigned shaders now derive glow waves from `_GlobalBiolumDearLieGroups[row].x/y/z/w` plus local coordinates; assigned shader scan finds no `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumAupOffset`, `_GlobalBiolumClock`, or `safeClock`.
  - Rejected: global clock phase and material/global intensity override because they create a second phase or amplitude authority beside the matrix.
  - Estimate: CPU 0 us; GPU ALU stays in the same order while removing stale scalar dependencies and variant additions.
- [x] Preserved domain boundary for external fauna shaders.
  - DOD: Leviathan shaders still read legacy support globals, but they are outside SHINOBU_238 flora/coral/procedural-bio ownership and were not edited without a fauna route card.
  - Rejected: cross-domain shader surgery to make the global source scan empty.
  - Estimate: no runtime change in SHINOBU lane.

## Loop 26 - Vault-Owned Fault Dump Scratch

- [x] Removed private managed dump byte-array ownership.
  - DOD: black-box dump snapshot now writes to Vault buffer `70312` (`BiolumBlackBoxDumpScratchBufferId`) with exact 9,616-byte capacity; `_blackBoxDumpBytes` and `new byte[BlackBoxDumpByteCount]` are gone.
  - Rejected: private `byte[]` snapshot storage because H-PHI requires persistent/cross-frame diagnostic memory to be Vault-owned.
  - Estimate: steady frame 0 us; fault frame remains one bounded 9,616-byte native copy plus event signal.
- [x] Registered the new binary payload boundary.
  - DOD: route card and binary payload ledger now list `70312` as Vault-owned dump scratch and document the fixed header plus 300-entry layout.
  - Rejected: chat-only proof of H-PHI compliance.
  - Estimate: documentation/static proof only.

## Loop 27 - Build Guard Reconciliation

- [x] Re-read the SHINOBU_238 prompt block and corrected task-count evidence.
  - DOD: strict line scan confirms 20 real task headings (`Task 01` through `Task 20`), hash `9d5db96674f0d27a`; the broad `Task\s+\d{2}` counter reports 22 because it also catches non-task references inside the same XML block.
  - Rejected: changing the task count to 22 based on a coarse regex.
  - Estimate: documentation/static proof only.
- [x] Re-ran build guard before compile.
  - DOD: guard history saw active `dotnet` PID `29148`; the latest guard now reports CPU `100` percent and no `dotnet`/`csc` process output.
  - Rejected: launching dotnet/Unity compile while the CPU threshold remains violated; earlier active-dotnet state was also a hard blocker.
  - Estimate: avoids adding compiler IO/CPU load while the workstation is saturated.

## Loop 28 - Editor Pulse AUP Precision

- [x] Removed direct absolute-AUP float casts from the editor pulse trigger.
  - DOD: `TryTriggerEditorGlobalPulse()` now requires an active runtime, subtracts the runtime AUP reference through `AupPrecisionMath.LocalDeltaDouble(originAUP, aupReference)`, and only then downcasts the local delta for hash/spatial-offset math.
  - Rejected: `(float)originAUP.x/y/z` in editor tooling because it violates the same 100km jitter rule even when the facade is editor-only.
  - Estimate: editor-only; player frame cost 0 us.
- [x] Updated the SHINOBU route card with the editor-pulse AUP boundary.
  - DOD: route card now records that editor pulse triggers fail closed without an active runtime and localize AUP before float math.
  - Rejected: leaving AUP compliance documented only in the code diff.
  - Estimate: documentation-only.

## Loop 29 - Vault Rebind Dump Worker Fence

- [x] Hardened DataVault rebind and teardown against a live dump writer.
  - DOD: `BindDataVault()`, `EnsureVaultBuffers()` generation mismatch, and `TryRefreshExistingVaultHandlesNoAllocate()` generation mismatch fence scheduled jobs, stop the black-box dump worker, and abort handle invalidation if the worker cannot join; `OnDisable()`/`Dispose()` release Vault handles only when `StopBlackBoxDumpWorker()` confirms shutdown.
  - Rejected: invalidating dump scratch handles while the background writer may still hold a resolved native view.
  - Estimate: rare hotswap/teardown path; steady frame cost 0 us.

## Loop 30 - Self Audit Persistence

- [x] Appended `<SELF_AUDIT>` to `Docs/AgentLogs/LOG_SHINOBU_238.md`.
  - DOD: audit lists Tasks 01-20, primary struct byte layout, scalability curve, Vault buffer IDs, job aliasing/dependency graph, compile guard, and Dear Lie complexity.
  - Rejected: chat-only self-audit output because the project protocol says the CTO reads disk logs.
  - Estimate: documentation-only.

## Verification

- Static forbidden-pattern scan: PASS for assigned biolum/vegetation paths.
- Accessor purity scan: PASS for `TryReadEditor*` and `TryReadEditorTelemetryEntry` in assigned biolum path.
- Shader AUP scan: PASS for assigned flora/coral/procedural-bio global-biolum consumers; object shaders now pass object-relative local deltas, and external fauna consumers remain out-of-scope.
- Editor pulse AUP scan: PASS_STATIC; no direct `(float)originAUP` or `originAUP.x/y/z` math remains in `TryTriggerEditorGlobalPulse()`.
- Legacy float route scan: PASS for live source; `_BiolumPulseTime` and `_HectonLegacyBiolumIntensity` no longer appear in live Assets code/guide except SHINOBU_238 audit documentation.
- `_GlobalBiolumPhase` scalar route scan: PASS; no live `Assets/_Project` references remain, docs references are audit/status notes only.
- `Shader.SetGlobalFloat` caveat: rendering bridge still publishes non-biolum global scalars (`_AupJitterMask`, shader feature mask, supersaturation, narcosis, death fade); these are outside the retired SHINOBU pulse/material route.
- CSV hot-path scan: PASS_STATIC_PLAYER; player `Tick()` does not call CSV file I/O, and watcher/FileStream methods are wrapped in `UNITY_EDITOR`; editor tooling still can hot-reload CSV.
- Cold mock lock scan: PASS; `GenerateMockLightingState()` no longer locks sync pulse buffers.
- Unity meta scan: PASS for `BiolumPulseLayoutGuard.cs.meta`.
- Vault rebind fence scan: PASS; handle invalidation routes now fence scheduled jobs first.
- Dump worker rebind scan: PASS_STATIC; Vault handle release/rebind is skipped when the dump worker fails to stop.
- Support-vector ownership scan: PASS; only PulseSync writes `_BiolumIntensity` while `_GlobalBiolumParams.x` advertises active matrix state, and the world manager suppresses its fallback write/reset.
- Job aliasing scan: PASS; non-overlapping arrays carry `[NoAlias]`, oscillator sampled inputs carry `[ReadOnly]`, and mock weather/predator rows remain write-capable because the mock seed mutates them.
- Route capacity audit: PASS; route card now matches `SyncGroupCount = 4` and `MaxGlobalBiolumStates = 16` profile slots.
- Continuous row-count audit: PASS_STATIC; no `ResolveStateCount` tier switch remains in `BiolumPulseSyncRuntime`.
- Shader matrix ABI scan: PASS; assigned non-indirect shaders no longer read `state.rgb`, `secondaryState.rgb`, `state.w`, or `secondaryState.w` as color/intensity payloads; indirect vegetation keeps `.w` only as spatial offset.
- Shader scalar phase scan: PASS; assigned shader set has no `_BiolumMasterPhase`, `_BiolumIntensity.x`, `_GlobalBiolumAupOffset`, `_GlobalBiolumClock`, or `safeClock` usage.
- Fault dump scratch scan: PASS_STATIC; no `_blackBoxDumpBytes` or `new byte[BlackBoxDumpByteCount]` remains, and `70312` is requested through DataVault.
- Mock glow coverage scan: PASS; no hardcoded `ActiveGlowingInstances = SyncGroupCount`, and emergency mock glow fill now uses `MaxGlowInstances`.
- First 20 Minutes binding: PASS_STATIC; route card names World load, Swim, and Hazard readability plus proof requirements.
- Self audit persistence: PASS_STATIC; `LOG_SHINOBU_238.md` contains `<SELF_AUDIT>` with 20 explicit task entries and proof caveats.
- CSV route documentation: PASS_STATIC; route card and binary ledger state editor-only CSV hot reload/scratch.
- Shader variant-surface scan: PASS_STATIC; SHINOBU_238 shader diff adds no `#pragma`, `multi_compile`, or `shader_feature` lines.
- Fault dump I/O scan: PASS_STATIC; `DumpBlackBox()` no longer builds paths, creates directories, or opens `FileStream`; it queues a copied 9,616-byte snapshot to the background dump writer.
- Fault dump bounds scan: PASS_STATIC; dump snapshot is clamped to `BlackBoxFrameCount` and no longer loops over arbitrary `blackBox.Length`.
- Shader phase-route scan: PASS_STATIC; `Tick()` no longer calls `UploadShaderScalars()`, and `LateFrameTick()` owns visual scalar/matrix publication.
- Editor facade compilation-surface scan: PASS_STATIC; SHINOBU_238 editor tuning facades are inside `UNITY_EDITOR`, with editor-window callers only.
- Burst directive scan: PASS_STATIC; both SHINOBU_238 Burst jobs use mandated `FloatMode.Fast` and `FloatPrecision.Standard`.
- Cold-route I/O context scan: PASS_STATIC; CSV file I/O is editor-only, binary profile load is cold boot, and black-box file writing is background diagnostic I/O.
- Shader float caveat: `HectonBiolumManager` still owns `_HectonOceanBiolumStrength` and `_HectonFloorBiolumStrength` as world-zone strength globals; they are not the retired per-material or pulse-timing route.
- Compile-wall caveat: `Hecton8.Core.csproj` generated metadata still includes the biolum runtime source; not edited in this pass.
- Prompt extraction: PASS, 20 task-heading lines, hash `9d5db96674f0d27a`.
- Diff whitespace check: PASS; only existing LF->CRLF Git warnings.
- Compile: BLOCKED BY POLICY. Latest guard reports CPU `100` percent; user protocol forbids launching dotnet/compile above 50 percent CPU. Earlier guard also observed active `dotnet` PID `29148`.
- Existing dotnet/csc process scan: latest scan returned no `dotnet`/`csc` process output.
- Unity import: NOT RUN.
- Profiler/GCMonitor: NOT RUN.
- Frame Debugger: NOT RUN.

## Loop 31 - Legacy Master Phase Writer Suppression

- [x] Suppressed `HectonBiolumManager` legacy `_BiolumMasterPhase` writer while PulseSync owns the matrix route.
  - DOD: `PublishGlobalBiolumPhase()` now samples `_GlobalBiolumParams.x` once and skips both `PublishBiolumMasterPhase()` and `_BiolumIntensity` writes when PulseSync is active; reset path also avoids legacy phase reset under PulseSync ownership.
  - Rejected: leaving `_BiolumMasterPhase` as a second active pulse-authority writer while assigned shaders read `_GlobalBiolumDearLieGroups`.
  - Estimate: same hot call count or lower; removes one bridge write on active PulseSync frames and prevents last-writer-wins visual drift.
- [x] Updated route card with the broadened legacy suppression boundary.
  - DOD: route card now names `_BiolumMasterPhase` and `_BiolumIntensity` as suppressed legacy bridge globals while `_GlobalBiolumParams.x > 0.5`.
  - Rejected: undocumented source-only route ownership change.
  - Estimate: documentation-only.

## Loop 32 - Subagent Findings Reconciliation

- [x] Fixed indirect vegetation vertex-pulse AUP/local-coordinate drift.
  - DOD: `Hecton_IndirectVegetation` now passes `animatedPositionWS - renderOriginWS` to `ResolveIndirectVegetationGlobalBiolumVertexPulse()` and builds the authored pulse offset from local coordinates plus non-AUP deterministic template/instance seed.
  - Rejected: `stableAupSeed` in the vertex biolum phase lane because it is absolute/stable-position data, not the local coordinate the fragment lane already used.
  - Estimate: CPU 0 us; GPU ALU unchanged order, but phase math no longer consumes large-coordinate AUP data.
- [x] Repaired dump-worker lifecycle after generation refresh and failed dispose stop.
  - DOD: Vault generation refresh paths restart the dump writer after reacquiring `70312`; `Dispose()` now preserves `_dataVault` and leaves `_disposed=false` when `StopBlackBoxDumpWorker()` times out, allowing the live writer to finish against valid Vault state and a later dispose retry.
  - Rejected: nulling `_dataVault` under a still-live writer and leaving the forensic route dead after handle refresh.
  - Estimate: steady frame 0 us; owner-swap/teardown only.
- [x] Renamed impure path helpers away from read-accessor names.
  - DOD: `ResolveProfilePath`/`TryResolveProfilePath`/`ResolveCsvOverridePath` are gone; cold/editor file path methods are now `BuildColdProfilePath`, `TryFindProfilePath`, and `BuildEditorCsvOverridePath`.
  - Rejected: keeping `Resolve*` names on functions that build paths, touch `File.Exists`, or mutate cached editor path state.
  - Estimate: no runtime behavior change.

## Loop 33 - Legacy Zone Registry Growth Removal

- [x] Replaced legacy fallback zone `List<HectonBiolumZone>` registries with fixed arrays and explicit counters.
  - DOD: `HectonBiolumManager` cave/ocean/floor active-zone registries are fixed `HectonBiolumZone[32]` arrays with `TryAddZoneNonAlloc` / `RemoveZoneNonAlloc`; duplicate checks and removals are index-based and bounded.
  - Rejected: `List<T>` capacity growth in the runtime biolum fallback bridge, because it can allocate when a scene crosses the initial capacity.
  - Estimate: steady hot path avoids List indirection/capacity checks; registration path cost is bounded `O(32)` and cold/event-driven.
- [x] Added overflow observability for the bounded registry.
  - DOD: a saturated zone registry increments `_zoneRegistryOverflowCount`, and the 300-frame telemetry `Flags` byte sets bit `16` when overflow has occurred.
  - Rejected: silently dropping excess zones after converting to fixed arrays.
  - Estimate: one branch in registration, one branch in telemetry write; 0 heap growth.
- [x] Re-ran static scans after the registry change.
  - DOD: scoped scan reports no `List<`, `new List<`, LINQ, `Pack=1`, DTO auto-properties, `UnityEngine.Random`, retired shader phase globals, or indirect-vegetation `stableAupSeed` biolum phase usage in SHINOBU_238 touched lanes.
  - Rejected: claiming zero-GC cleanup without a pattern scan.
  - Estimate: verification-only.

## Latest Guard

- Compile/build/import: BLOCKED BY POLICY.
  - DOD: latest guard reports `CPU_LOAD=100` and active `dotnet` processes `11856`, `19480`, `20304`, `26312`, `28396`, `29124`, `30516`.
  - Rejected: launching `dotnet build`, Unity import, or script compilation while CPU is above 50 percent or another dotnet is active.
  - Estimate: no build IO started.

## Loop 34 - Legacy Touch Ripple Continuous Quality

- [x] Removed binary quality gating from the legacy biolum touch-ripple upload bridge.
  - DOD: `HectonBiolumManager.PublishTouchRippleBuffer()` no longer reads `GlobalRegistry.ScalabilityTier` or `DistanceMath.IsHighQualityTier`; upload capacity is now `round(lerp(0, 16, smoothstep(0.12, 0.72, GlobalQualityWeight)))`.
  - Rejected: all-or-nothing low/high ripple publication because it creates a binary visual pop and violates continuous `GlobalQualityWeight`.
  - Estimate: same fixed upper bound of 16 ripples; low quality uploads zero or few ripples without a tier switch, high quality reaches full 16.
- [x] Re-ran continuous-quality and forbidden-pattern scans.
  - DOD: scoped scan reports no `ScalabilityTier`, `IsHighQualityTier`, `HectonQualityTier`, or `GlobalRegistry.ScalabilityTier` usage in `HectonBiolumManager`; forbidden list scan remains clean for `List<`, LINQ, `Pack=1`, DTO auto-properties, and `UnityEngine.Random`.
  - Rejected: leaving the continuous route claim documented only in PulseSync while the touched legacy fallback had a binary branch.
  - Estimate: verification-only.

## Latest Guard Refresh

- Compile/build/import: STILL BLOCKED BY POLICY.
  - DOD: refreshed guard reports `CPU_LOAD=43`, but active `dotnet` processes remain `11856`, `19480`, `20304`, `26312`, `28396`, `29124`, `30516`.
  - Rejected: launching `dotnet build`, Unity import, or script compilation while another dotnet worker is active.
  - Estimate: no build IO started.

## Loop 35 - Legacy Bridge Accessor Purity And Cold Service Cache

- [x] Removed hidden mutation from touched camera read accessors.
  - DOD: `GetCameraPosition()` and `GetCameraAup()` now return cached owner-phase snapshots only; camera reference, position, and AUP refresh run from owner phases via `RefreshCameraSnapshotForOwnerPhase(...)` during initialize/tick.
  - Rejected: `Get*` methods that search player context or mutate `_cachedCameraAup` during consumer reads.
  - Estimate: removes one hidden player/camera route check and one possible AUP rebuild from each consumer read; exact runtime microseconds need profiler proof.
- [x] Moved legacy bridge service dependencies to cold cache plus hot-swap rebind.
  - DOD: `HectonBiolumManager` caches `DataVault`, `TickDispatcher`, `Fluid`, and `Player` from `GlobalRegistry` during lifecycle and rebinds through `IGlobalRegistryHotSwapListener`; hot paths consume cached fields.
  - Rejected: polling `GlobalRegistry.DataVault`, `.TickDispatcher`, `.Fluid`, or `.Player` from tick/ensure/sample helpers.
  - Estimate: several registry property reads per frame/event are removed from the touched fallback bridge; direct gain is small, route discipline is the main value.
- [x] Renamed mutating/querying helper names and fixed a precompile self-read defect.
  - DOD: old `Resolve*`/`TryResolve*` helper names for mutating bridge work are gone from the touched manager/controller files, `TryBindSurvivalSystemFromPlayerContext()` names the survival bind explicitly, and `SampleCameraCacheClockSeconds()` is an instance method because it reads `_cachedTickDispatcher`.
  - Rejected: read-like names for methods that sample clocks, update state, select write buffers, or cache scene/player references.
  - Estimate: source hygiene and compile-risk removal; runtime delta is from avoiding hidden cache refresh in read accessors.
- [x] Scoped verification after the accessor/cache pass.
  - DOD: old-name scan returned no hits; brace count is `189/189`; `GlobalRegistry.DataVault/TickDispatcher/Fluid/Player` hits in `HectonBiolumManager` are limited to `CacheGlobalRegistryServicesCold()`. `GlobalRegistry.CelestialRuntimeSnapshot` remains as the only manager snapshot bridge because no typed service route exists in this pass.
  - Rejected: claiming zero GlobalRegistry usage in the manager while the Celestial snapshot bridge remains.
  - Estimate: verification-only.

## Latest Guard After Loop 35

- Compile/build/import: SKIPPED BY POLICY.
  - DOD: guarded compile commands rechecked before invoking `dotnet`; guard reported `CPU_LOAD=71`, then `CPU_LOAD=54`, then `CPU_LOAD=85`, all with `DOTNET_CSC_COUNT=0`, so every command exited before build execution.
  - Rejected: launching targeted `dotnet build Hecton8.Core.csproj --no-restore /m:1` above the explicit 50 percent CPU threshold.
  - Estimate: no build IO started.
