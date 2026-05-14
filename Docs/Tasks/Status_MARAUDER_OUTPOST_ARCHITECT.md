# Status - MARAUDER_OUTPOST_ARCHITECT

Authority: CURRENT_BATCH.md AGENT_PROMPT id="MARAUDER_OUTPOST_ARCHITECT"
Role: HABITAT_ARCHITECT
Domain: ECHELON 6 - HABITAT & VEHICLES
State: PENDING VERIFICATION

## Prompt Extraction

- [x] Extracted XML prompt from Docs/Tasks/CURRENT_BATCH.md using PowerShell raw regex. DOD: strict tag isolation. Alternative rejected: editor/MCP partial reads. Estimate: 40 us parse after disk read.

## Mandates Loaded

- [x] ARCH_Global_Registry_ServiceLocator_DI_Init.txt. DOD: no singleton BaseGenerator; service registration through GlobalRegistry. Alternative rejected: BaseGenerator.Instance. Estimate: 0 us runtime policy.
- [x] OPT_Zero_GC_Policy_AllocFree_Mandate.txt. DOD: no managed allocation in Tick/render paths. Alternative rejected: managed WFC arrays and LINQ. Estimate: 0 B/frame target.
- [x] OPT_Native_Memory_Collections_JobSystem_Protocol.txt. DOD: NativeArray SoA, Burst jobs, tracked lifecycle. Alternative rejected: managed jagged grid. Estimate: solver target under 250 us low tier.
- [x] MATH_Deterministic_RNG_SlotMachine.txt. DOD: hash-derived deterministic LCG seed. Alternative rejected: UnityEngine.Random/System.Random. Estimate: 1-3 us seed/mask generation.
- [x] MATH_Coordinate_Precision_AUP_FloatingOrigin.txt. DOD: AUP shift applies to native matrix data, not Transform.position. Alternative rejected: scene-wide GameObject shift for shell. Estimate: 10-40 us per shift for matrix pool.
- [x] REND_GPU_Sovereignty.txt. DOD: shell rendered by GPU buffers/indirect path; no 500 wall GameObjects. Alternative rejected: prefab wall instantiation. Estimate: CPU draw overhead reduced from hundreds of renderer submissions to one family submit.
- [x] VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt. DOD: heightmap adaptation through MapMagic/global data contract. Alternative rejected: hardcoded seabed Y. Estimate: 20-80 us for bottom support pass.
- [x] DBG_Telemetry_Crash_Reporting_PostMortem.txt. DOD: 300-frame blackbox and binary dump path. Alternative rejected: Debug.Log-only failure reporting. Estimate: 0.5-2 us ring write.

## Task Checklist

### Loop 1 - Tasks 1-5

- [x] Task 1 - SINGLETON ERADICATION: `IOutpostGenerationService` is published through `GlobalRegistry.OutpostGeneration`. DOD: registry interface slot, no `BaseGenerator.Instance`. Alternative rejected: concrete singleton owner. Estimate: 0 us hot path.
- [x] Task 2 - SIGNAL MIGRATION: Runtime drains `SignalBus<SectorHydratedSignal>` and triggers only when `SectorHash == FirstBaseHash` unless debug override is enabled. DOD: native signal snapshot consumption. Alternative rejected: polling world generator singleton. Estimate: 1-5 us per hydration frame.
- [x] Task 3 - ASMDEF ISOLATION: `Hecton8.World.Outposts.asmdef` references `Hecton8.World.Contracts` and `Hecton8.Core`; contract lives in `World/Contracts`. DOD: isolated assembly boundary. Alternative rejected: dropping runtime into core assembly. Estimate: 0 us runtime.
- [x] Task 4 - GRID S.O.A.: `MarauderOutpostGenerationService.WfcGrid` is a persistent `NativeArray<byte>` sized 10x10x5. DOD: byte grid SoA, no managed cells. Alternative rejected: class-per-cell graph. Estimate: 500 B grid payload plus native header.
- [x] Task 5 - DETERMINISTIC SEED: Solver seed is `MarauderOutpostHash.LcgHash((ulong)WorldSeed + FirstBaseHash)`. DOD: deterministic LCG hash. Alternative rejected: `UnityEngine.Random` and `System.Random`. Estimate: 1-3 us cold generation seed.

### Loop 2 - Tasks 6-10

- [x] Task 6 - STRUCTURAL RULES: `MarauderOutpostSolveJob` applies bitwise N/E/S/W masks, corridor room/hatch contact, and upper-floor support checks. DOD: Burst `IJob`, byte flags. Alternative rejected: managed adjacency arrays/backtracking. Estimate: 20-250 us by tier.
- [x] Task 7 - HEIGHTMAP ADAPTATION: `MarauderOutpostMatrixExtractionJob` samples MapMagic quantized height payload and emits pillar/stilt matrices; telemetry flags fallback. DOD: native height samples, visual fake supports. Alternative rejected: raycast/rigidbody settlement. Estimate: 20-80 us full grid, lower on MX350.
- [x] Task 8 - MATRIX EXTRACTION: Solved cells become `_shellMatrices : NativeArray<float4x4>` plus `_shellCellTypes`. DOD: no Transform shell. Alternative rejected: prefab shell hierarchy. Estimate: 40-120 us full grid extraction.
- [x] Task 9 - INDIRECT RENDERING: Matrices/types upload to `GraphicsBuffer`s and shell draws through `Graphics.RenderMeshIndirect`. DOD: one indirect shell path, no CPU shell draw loop. Alternative rejected: 500 renderer submissions. Estimate: steady CPU submit below 0.05 ms.
- [x] Task 10 - INTERACTABLE SPAWNING: Only `Datapad` and `SealedDoor` spawn packets hit `GlobalRegistry.ObjectPool`; proxy meshes are baked cold. DOD: bounded pooled proxies. Alternative rejected: proxy for every cell. Estimate: max 16 cold pooled spawns.

### Loop 3 - Tasks 11-15

- [x] Task 11 - RUST & WEAR: `Hecton_MarauderOutpostIndirect.shader` reads `_OutpostAge01` and calls the Hecton procedural rust/silt path; runtime also writes `_HectonMaterialDecayRuntime`. DOD: shader scalar path. Alternative rejected: per-instance material clones. Estimate: 0 B/frame, shader ALU only.
- [x] Task 12 - AUP SHIFT SAFETY: `AupShiftSignal` schedules `MarauderOutpostAupShiftJob` over native matrices and shifts pooled proxies. DOD: native matrix offset. Alternative rejected: parent transform shell shift. Estimate: 10-120 us rare shift by matrix count.
- [x] Task 13 - MATH LOD: Low/MX350/Unknown quality selects 5x5x3 before solving; other tiers use 10x10x5. DOD: dimension branch before Burst schedule. Alternative rejected: full grid on low then cull visually. Estimate: 75 cells low vs 500 full.
- [x] Task 14 - ZERO-GC: Solver/extractor/shift are NativeArray/Burst jobs; Tick/Render use spans/native buffers and no LINQ. Cold managed arrays exist only for fallback mesh/proxy handles. DOD: 0 managed bytes hot path by code audit. Alternative rejected: managed WFC arrays. Estimate: 0 B/frame hot path.
- [x] Task 15 - OMEGA COMPILE CHECK: Code audit and Unity Roslyn response-file compiles confirm constraints are bitwise byte operations and the outpost dependency chain emits. DOD: Logistics.Grid.Contracts, Logistics.Grid, World.Contracts, Core.Memory, Core, and World.Outposts compile from Bee response files. Alternative rejected: changing task to managed arrays. Estimate: no runtime cost.

### Loop 4 - Re-Read And Self-Review

- [x] Re-extract prompt after task 3 cadence. DOD: raw regex extraction from `CURRENT_BATCH.md`. Alternative rejected: memory recall. Estimate: 40 us parse after disk read.
- [x] Re-read code for singleton, managed allocation, Instantiate wall, and public API drift. DOD: `rg` audit found no `BaseGenerator`, no shell `Instantiate`, no LINQ/random; only cold fallback mesh arrays and bounded proxy handle array. Estimate: 0 us runtime.
- [x] Verify compile and console status. DOD: refreshed Bee response files now compile through `Hecton8.World.Outposts`; Unity MCP console remains unavailable. Alternative rejected: claiming runtime proof without console/profiler access. Estimate: 0 us runtime.

### Loop 5 - Polish Mandate

- [x] Read POLISH_MANDATE only after tasks complete or blocked. DOD: core tasks were checked/blocked before mandate parsing. Alternative rejected: premature anti-bloat work before contract completion. Estimate: 40 us parse after disk read.
- [x] OMEGA reciprocal pass. DOD: height sampling and packed-age conversion use precomputed reciprocal constants/multiplication, not runtime floating division. Alternative rejected: honest `/ TerrainSize` and `/ 65535f` math. Estimate: 2-8 us saved per full extraction depending Burst/backend.
- [x] OMEGA forbidden construct audit. DOD: scoped `rg` found no `foreach`, `string.Format`, string interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, LINQ, `System.Random`, `UnityEngine.Random`, `BaseGenerator`, or shell `Instantiate` in the outpost runtime path. Alternative rejected: manual eyeballing only. Estimate: 0 B/frame preserved.
- [x] Append final report to Docs/AgentLogs/LOG_MARAUDER_OUTPOST_ARCHITECT.md. DOD: report includes wrong/done/cheats/microseconds/diff/compile wall. Alternative rejected: chat-only report. Estimate: 0 us runtime.

### Loop 6 - Patient Re-Audit And Integration Upgrade

- [x] Re-extracted prompt with attribute-safe XML regex. DOD: `<AGENT_PROMPT ... id="MARAUDER_OUTPOST_ARCHITECT" ...>` block parsed cover-to-cover. Alternative rejected: brittle exact opening-tag regex. Estimate: 40 us parse after disk read.
- [x] Hardened job teardown. DOD: `Dispose()` defers native array disposal behind the active `JobHandle` instead of blocking on `Complete()`. Alternative rejected: main-thread shutdown stall. Estimate: saves 20-250 us worst-case generation wait on teardown.
- [x] Upgraded logistics handoff. DOD: generated WFC byte grid registers through `WfcOutpostGridRegistry`, publishes a real `WfcOutpostGeneratedSignal.GridHandle`, and exposes `TryGetWfcGrid` on `IOutpostGenerationService`. Alternative rejected: signal with fake hash handle. Estimate: one 500-byte cold copy, 0 B/frame.
- [x] Added deterministic generator cell and door power bridge. DOD: center-bottom WFC cell uses shared logistics `Generator` kind; sealed-door proxies consume `WfcOutpostDoorPowerSignal` without creating shell GameObjects. Alternative rejected: missing-generator graph fallback and per-cell power objects. Estimate: avoids graph fault pass and keeps proxy cap at 16.
- [x] Re-ran scoped audits. DOD: no forbidden managed constructs or runtime division/pow hits in owned outpost/shader paths; only `_jobHandle.Complete()` calls are post-`IsCompleted` commit points. Alternative rejected: manual-only review. Estimate: 0 B/frame preserved.
- [x] Re-ran dependency-chain compile. DOD: Unity Roslyn response-file compiles PASS for `Hecton8.Logistics.Grid.Contracts`, `Hecton8.Logistics.Grid`, `Hecton8.World.Contracts`, `Hecton8.Core.Memory`, `Hecton8.Core`, and `Hecton8.World.Outposts`. Alternative rejected: relying on stale missing-ref result. Estimate: compile-only proof.

### Loop 7 - Continued Hardening Pass

- [x] Re-extracted prompt and domain boundary before additional work. DOD: raw XML prompt read cover-to-cover and Echelon 6 domain rechecked. Alternative rejected: acting from chat memory. Estimate: 40 us parse after disk read.
- [x] Hardened heightmap sampling. DOD: extraction now requires valid sample length, sane resolution, positive terrain height, and precomputed height scale before reading `HeightSamples`. Alternative rejected: trusting external payload validity only. Estimate: prevents Burst out-of-range crash; saves 1 multiply per height sample.
- [x] Added deterministic edge-facing yaw for sealed-door shell and proxy packets. DOD: edge doors face out of the grid while interior interactables still use missing-neighbor fallback. Alternative rejected: identity rotation for every shell cube. Estimate: cold extraction only, below 5 us full grid.
- [x] Guarded stale generation and power-signal handling. DOD: same-sector reuse also requires same world seed; door power signals are ignored until a real published grid handle exists; registry publish failure dumps blackbox. Alternative rejected: stale seed reuse and accepting handle-less door signals. Estimate: 0 B/frame, avoids cross-outpost signal bleed.
- [x] Re-ran compile and static audits. DOD: `Hecton8.World.Outposts` response-file compile passes; scoped forbidden construct audit and `git diff --check` pass. `Hecton8.Core` response-file compile is currently blocked by unrelated GPR symbol drift. Alternative rejected: editing Ground Radar from Habitat domain. Estimate: compile-only proof.

### Loop 8-10 - Recovered Hardening And Replay

- [x] Detected source/doc drift from concurrent overwrite. DOD: file readback showed prior origin/API/AUP/publish hardening missing while status/rationale described it; restored source instead of trusting stale docs. Alternative rejected: reporting completion without source verification. Estimate: prevents false-positive completion.
- [x] Restored explicit generation-origin anchoring. DOD: `outpostOriginOverride` and finite `localOriginOffsetMeters` drive sector-hydration generation. Alternative rejected: raw `transform.position` fallback from `SectorHydratedSignal`. Estimate: 0 us/frame; below 1 us per generation request.
- [x] Restored public API and AUP hardening. DOD: WFC count clamps to native buffer length, shell getters require `_generated`, and solve-phase AUP writes shift telemetry/snapshot. Alternative rejected: stale buffers and missing blackbox shift frame. Estimate: below 2 us on rare shift path.
- [x] Added generated-signal replay and stale-handle recovery. DOD: successful grid publication replays `WfcOutpostGeneratedSignal` for four Tick frames; same-sector requests validate/re-announce handles; evicted registry handles republish from the existing native grid. Alternative rejected: one-frame-only signal and full WFC re-solve. Estimate: avoids 20-250 us retry solve, 0 B/frame steady.
- [x] Re-ran compile and static audits. DOD: `Hecton8.World.Outposts` response-file compile passes; scoped audit found no managed/random/prefab wall/telemetry modulo/origin fallback regressions; `git diff --check` passes with repository LF/CRLF warning only. Alternative rejected: accepting restored code without source proof. Estimate: compile-only proof.

### Loop 11 - Late Consumer Heartbeat

- [x] Added bounded generated-signal heartbeat. DOD: after the four-frame burst replay, the outpost emits one `WfcOutpostGeneratedSignal` every 60 Tick frames while generated, validating the registry handle first and republishing from the native grid if evicted. Alternative rejected: permanent per-frame spam or one-frame-only handoff. Estimate: one typed signal per second at 60 Hz, 0 B/frame steady.
- [x] Re-ran compile and static audits. DOD: `Hecton8.World.Outposts` response-file compile passes; scoped audit found no managed/random/prefab wall/telemetry modulo/origin fallback/solve zero-shift regressions; `git diff --check` passes with repository LF/CRLF warning only. Alternative rejected: accepting signal cadence changes without source proof. Estimate: compile-only proof.

### Loop 12 - Fault Backoff And Blackbox Format

- [x] Re-checked prompt source and disk state before work. DOD: `Docs/Tasks/CURRENT_BATCH.md` currently returns `PROMPT_NOT_FOUND`, so persisted `Status_`/`Rationale_` files remain the authoritative long-term assignment record. Alternative rejected: assuming chat memory or root-level `CURRENT_BATCH.md`, which is absent. Estimate: disk parse only.
- [x] Restored bounded publish-failure retry. DOD: registry publish failure leaves `_generatedSignalReplayFrames` at zero but arms `_generatedSignalHeartbeatFrames` for 60 Tick frames, preventing per-frame `RegisterGrid`/blackbox dump attempts. Alternative rejected: handleless generated state retrying and dumping every Tick. Estimate: avoids repeated fault-path file I/O and signal work; 0 B/frame steady.
- [x] Hardened blackbox dump format. DOD: dump now writes magic/version/entry-payload bytes/start index and serializes the 300-entry ring oldest-to-newest from `_telemetryWriteIndex`. Alternative rejected: raw physical ring order with no header. Estimate: fault-path only; normal telemetry write cost unchanged.
- [x] Re-ran scoped static audits. DOD: audit found no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, pow/division regressions, telemetry modulo, raw transform-origin fallback, solve zero-shift accumulation, or immediate publish-failure heartbeat reset. Alternative rejected: manual-only review. Estimate: 0 B/frame preserved.
- [ ] Compile proof [BLOCKED BY DEPENDENCY]. DOD attempted: `Hecton8.World.Outposts` response-file compile now depends on missing `Library/Bee/artifacts/1300b0aEDbg.dag/Hecton8.Core.ref.dll`; rebuilding `Hecton8.Core` fails outside this domain at `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs(237,26): xxHash3` missing. Alternative rejected: editing SaveSystem/Core from Habitat domain or claiming a clean compile from stale artifacts. Estimate: compile-only blocker.

### Loop 13 - Extraction-Phase AUP Shift Closure

- [x] Reloaded mandates without rebuilds. DOD: re-read GlobalRegistry, signal lanes, zero-GC, native jobs, telemetry, GPU sovereignty, logistics power, AUP, project domain, and persisted state. Alternative rejected: acting from compressed chat state. Estimate: disk reads only.
- [x] Closed extraction-phase AUP race. DOD: when a shift is queued while matrix extraction is running, `CommitCompletedGeneration` now consumes that pending shift before `_generated`, draw bounds, GPU upload, proxy spawn, and grid publication. Shell matrices and interactable spawn packets are shifted together with `_generationOrigin`. Alternative rejected: letting the next LateFrame fix visuals after publishing stale origin. Estimate: rare cold commit path; 0 B/frame steady.
- [x] Re-ran source-only static audits. DOD: scoped forbidden-pattern `rg` audit found no managed LINQ/random, shell `Instantiate`, `BaseGenerator`, pow/division regressions, telemetry modulo, raw transform-origin fallback, or publish-failure heartbeat reset. `git diff --check` passed with repository LF/CRLF warning only. Alternative rejected: dotnet/Unity rebuild, forbidden by user this loop. Estimate: verification only.
- [ ] Compile proof [NOT RUN BY USER REQUEST]. DOD: no `dotnet` rebuild or response-file compile was executed in Loop 13. Existing external blocker from Loop 12 remains recorded until an integrator/Core owner fixes `Hecton8.Core.ref.dll` / `SaveMasterHashV10.xxHash3`. Alternative rejected: violating explicit "do not make dotnet rebuilds". Estimate: no runtime cost.

### Loop 14 - Finite Scalar Payload Guard

- [x] Re-identified mandates before coding. DOD: GlobalRegistry lifecycle, typed signal lanes, zero-GC hot paths, native job ownership, blackbox telemetry, AUP shift integrity, logistics power handoff, and GPU/manual buffer ownership were selected for this pass. Alternative rejected: generic refactor loop. Estimate: disk reads only.
- [x] Sanitized outpost scalar inputs. DOD: cell size, floor height, stilt clearance, and age now resolve through finite-safe helpers before editor validation, Burst extraction fields, draw bounds, snapshot telemetry, and `WfcOutpostGeneratedSignal`/descriptor payloads. Alternative rejected: relying on `Mathf.Max`/`math.max` against NaN, which can leak non-finite data. Estimate: tiny scalar branch cost at payload/render boundaries; 0 B/frame.
- [x] Re-ran source-only audits. DOD: scoped forbidden-pattern audit found no LINQ/random/string/shell instantiation/division/telemetry modulo/raw origin regressions, and scalar audit found no raw `math.max(... serialized field ...)` or raw `outpostAge01` payload writes. `git diff --check` passed with repository LF/CRLF warning only. Alternative rejected: `dotnet` rebuild, forbidden by user. Estimate: verification only.
- [ ] Compile proof [NOT RUN BY USER REQUEST]. DOD: no `dotnet` rebuild or response-file compile was executed in Loop 14. Alternative rejected: violating explicit "do not make dotnet rebuilds". Estimate: no runtime cost.

### Loop 15 - Render Boundary And Pending Shift Fault Closure

- [x] Re-checked state, rationale, mandates, domain boundary, and `CURRENT_BATCH.md`. DOD: disk-backed `Status_`/`Rationale_` files remain authoritative because `CURRENT_BATCH.md` returns `PROMPT_NOT_FOUND`; selected GPU sovereignty, zero-GC, telemetry, and AUP mandates. Alternative rejected: acting from compressed chat memory. Estimate: disk reads only.
- [x] Hardened indirect draw argument generation. DOD: `UpdateIndirectArgsBuffer` now reads submesh 0 only when the resolved mesh exists and has at least one submesh; zero-index meshes emit zero instances, and `Render` skips meshes with no submeshes. Alternative rejected: trusting authored `shellMesh` validity and risking `GetIndexCount(0)` faults. Estimate: cold upload guard plus one render integer property check.
- [x] Hardened corrupt pending AUP shift handling. DOD: extraction commit now clears non-finite `_pendingShift`, writes fault/AUP telemetry, and dumps the blackbox instead of leaving sticky invalid state. Alternative rejected: returning with `_hasPendingShift` still true and retrying a poisoned shift later. Estimate: rare fault path only, 0 B/frame steady.
- [x] Re-ran source-only audits. DOD: `git diff --check` passed with repository LF/CRLF warning only; scoped `rg` found no forbidden managed/random/string/shell instantiation/division/telemetry modulo/raw origin regressions; targeted audit found no old unsafe mesh-args ternary or stale pending-shift guard. Alternative rejected: `dotnet` rebuild, forbidden by user. Estimate: verification only.
- [ ] Compile proof [NOT RUN BY USER REQUEST]. DOD: no `dotnet` rebuild or response-file compile was executed in Loop 15. Alternative rejected: violating explicit "do not make dotnet rebuilds". Estimate: no runtime cost.

### Loop 16 - AUP Signal Ingress Fault Evidence

- [x] Re-audited owned outpost source for remaining concrete risks. DOD: scanned outpost service/jobs/contracts for TODO/FIXME, hot-path allocation constructs, hardcoded shift epsilon, unsafe draw args, and silent non-finite AUP handling. Alternative rejected: broad refactor loop. Estimate: source audit only.
- [x] Hardened `ApplyAupShift` non-finite ingress. DOD: bad `AupShiftSignal` payloads now write fault/AUP telemetry and dump the blackbox before returning; tiny finite shifts use the shared `ShiftEpsilonMeters` constant. Alternative rejected: silent drop of corrupt coordinate payloads. Estimate: fault path only; steady valid shift branch unchanged except shared constant.
- [x] Re-ran source-only audits. DOD: `git diff --check` passed with repository LF/CRLF warning only; scoped forbidden audit passed; targeted audit found no hardcoded `new float3(0.0001f)`, no combined finite/tiny-shift early return, no old unsafe mesh-args ternary, and no stale pending-shift guard. Alternative rejected: `dotnet` rebuild, forbidden by user. Estimate: verification only.
- [ ] Compile proof [NOT RUN BY USER REQUEST]. DOD: no `dotnet` rebuild or response-file compile was executed in Loop 16. Alternative rejected: violating explicit "do not make dotnet rebuilds". Estimate: no runtime cost.

## Verification Ledger

- Compile status: NOT RUN IN LOOP 16 BY USER REQUEST / PRIOR BLOCKER STILL RECORDED. Last attempted compile path could not resolve `Hecton8.Core.ref.dll`; Core rebuild failed in `SaveMasterHashV10.cs(237,26)` on missing `xxHash3`, outside Habitat/Outposts ownership.
- Unity Console status: MCP unavailable; console/scene validation not accessible from this session.
- GC proof: code audit only; hot Tick/Render path uses spans/native buffers and no LINQ/managed allocation.
- Frame/VRAM proof: static estimate only; runtime profiling blocked by Unity MCP transport failure; no console/profiler capture is available from this session.
