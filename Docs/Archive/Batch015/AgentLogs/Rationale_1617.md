# Rationale_1617

Agent: 1617
Domain: ASYNC_UPLOAD_AND_VRAM_DICTATOR / Echelon 7 Graphics GPU Memory & Streaming
Status: STATIC VERIFIED, UNITY BUILD NOT RUN

## Decisions

### Decision 01: VRAM Black Box Ring

Problem: VRAM monitor had sampled counters but no fixed 300-sample native black box for pressure state.
Solution: Added 64-byte `VramTelemetryEntry` and a DataVault ring at buffer 71617 owned by `SystemID.GraphicsScalability`.
Rejected Alternatives: JSON/log file per sample; managed list; GlobalRegistry hot polling. Too slow or banned by user.
Scalability potential: Low uses one slow-tick row and pressure flags. Middle adds usable diagnostics. High/Ultra keeps full 300-sample history without extra managed churn.
Hardware Impact: MX350/i3 estimate 2-6 us per slow tick, 0 B GC. No frame-rate claim until profiler run.

### Decision 02: Byte-Based Upload Grants

Problem: Dispatcher throttled by slots/time but not by upload payload size, allowing multi-asset bursts to exceed practical PCIe/upload bandwidth.
Solution: Added `EstimatedBytes` to dispatch requests/tickets and a continuous per-frame grant budget from 2 MB to 50 MB based on pressure and `GlobalQualityWeight`.
Rejected Alternatives: Only lowering concurrent Addressables slots; binary low/high quality switches. Both fail MX350 pressure cases.
Scalability potential: Low collapses toward 2-5 MB/frame. Middle raises capacity gradually. High/Ultra can spend up to 50 MB/frame when pressure is low.
Hardware Impact: MX350/i3 projected 20-80 us stall avoidance on pressured frames; measured value pending Unity profiler.

### Decision 03: Mip Bias Audit Surface

Problem: Existing mip controller worked but its pressure curve was private, making fuzzer/assertion validation awkward.
Solution: Added `ResolveMipLimitDeltaForAudit` that mirrors warning/forced/redline math without mutating `QualitySettings`.
Rejected Alternatives: Runtime test by changing live global mip state; obsolete `Texture.masterTextureLimit`; per-texture iteration.
Scalability potential: Low gets early mip downgrade around compact warning. Middle transitions smoothly. High/Ultra delays downgrade until real pressure.
Hardware Impact: Runtime cost 0 us for audit API. Existing pressure monitor avoids texture iteration.

### Decision 04: Addressables Release Ownership

Problem: World chunk residency owns handles, but lifecycle governor already owns tracked Addressables release semantics.
Solution: Kept existing release path: chunk manager clears pending load state, resolves hash, calls `ReleaseAddressableAsset`, stages external release only when untracked.
Rejected Alternatives: Direct `Addressables.Release` from world streaming in parallel with governor. That risks double-release and route ambiguity.
Scalability potential: Low drains releases immediately on cache clear. Middle/High/Ultra can keep lifecycle heuristics centralized.
Hardware Impact: No new runtime cost; avoids release contention and ownership bugs.

### Decision 05: Reporting Format

Problem: Batch asked for JSON report, but user explicitly rejected useless JSON/binary dumps.
Solution: Added editor markdown scanner and final `LOG_1617.md` append. Runtime proof is code and fixed native telemetry.
Rejected Alternatives: `VRAM_BUDGET_COMPLIANCE_1617.json`; binary dump. Conflicts with direct user order.
Scalability potential: Editor scan works across tiers; runtime path remains native and fixed-size.
Hardware Impact: 0 runtime us; editor-only scan cost depends on asset count.

### Decision 06: Late-Frame Asset Progress Publication

Problem: First pass allowed `AssetLoadProgressSignal` to be pushed from dispatcher mutation paths; this was not strict enough for phase-safe visual/diagnostic handoff.
Solution: Added a fixed 128-slot `AssetLoadProgressSignal` handoff buffer, cold-initialized the SignalBus lane on service registration, and pushed the signal only from `LateFrameTick` via `FlushProgressSignalsLateFrame`.
Rejected Alternatives: Direct SignalBus push from `Tick`, managed events, or allocating a queue. They either blur phase ownership or violate Zero-GC.
Scalability potential: Low tier gets bounded diagnostic traffic; middle/high/ultra get the same deterministic handoff without changing gameplay truth.
Hardware Impact: Runtime state transfer remains 0 B GC. Additional cost is copying one 64-byte struct per asset progress event; bounded at 128 events/frame.

### Decision 07: APEX Static AST Verifier

Problem: Manual `rg` checks prove the current diff but do not leave a repeatable editor gate for hot dependency, phase, and DataVault lock rules.
Solution: Added `VRAMIntegratorVerifier1617`, an editor-only in-memory Roslyn AST verifier. It writes no JSON/markdown reports and throws immediately on violation.
Rejected Alternatives: Full `dotnet build`, runtime playmode harness, or file report generation. Build was prohibited and a dotnet process was already active.
Scalability potential: Verification stays editor-only and does not affect low-tier runtime.
Hardware Impact: 0 runtime us; no player code path changed.

### Decision 08: Hardware Mip Floor Ownership

Problem: `VRAMEnforcer` applied a bootstrap hardware mip floor, while `VRAMPressureMonitor` restored to its own captured baseline. If bootstrap order changed, pressure recovery could restore below the hardware floor.
Solution: Exposed `VRAMEnforcer.RuntimeTextureMipLimitFloor` and clamped all pressure-monitor apply/restore paths to `max(capturedBaseline, hardwareFloor)`.
Rejected Alternatives: Duplicate floor math inside `VRAMPressureMonitor` or a binary low/high graphics switch. Both create route drift.
Scalability potential: Weak devices keep the floor; middle/high/ultra can restore to full resolution when the floor resolves to zero.
Hardware Impact: 0 allocations; one integer max check on slow-tick mip decision paths.

### Decision 09: Pressure-Aware Async Upload Policy

Problem: `WorldChunkResidencyManager` owned Unity async upload policy but scaled buffer/time-slice only from quality. High quality could keep a 256 MB persistent upload buffer while VRAM pressure was already high.
Solution: Cached `IVramPressureReadModel` cold and collapsed the effective upload quality with `smoothstep(0.55, 0.98, pressure)`. The actual `QualitySettings.asyncUpload*` writes remain slow-phase only.
Rejected Alternatives: Per-frame upload setting writes, hot GlobalRegistry polling, or binary low/high upload presets.
Scalability potential: Weak/pressured devices fall back to 64 MB and 1 ms. Middle devices ramp smoothly. High/Ultra keep 256 MB and 4 ms only when pressure is low.
Hardware Impact: 0 allocations; one cached interface read and scalar curve on slow tick.

### Decision 10: RenderTexture Pool Pressure Trim

Problem: Idle pooled RenderTextures are useful for reuse, but on low VRAM they become dead residency that competes with texture streaming and async upload buffers.
Solution: Cached VRAM budget/pressure read models in `RenderTexturePool` and clear idle pools on slow tick when RT or total VRAM pressure is active.
Rejected Alternatives: Clearing every frame, clearing on every return, or querying GlobalRegistry from `SlowTick`.
Scalability potential: Weak/pressured devices shed idle RTs. Middle/high/ultra retain reuse when pressure is low.
Hardware Impact: 0 allocations; slow-tick dictionary enumeration only when pressure is active.

### Decision 11: Procedural GraphicsBuffer Upload Gate

Problem: Addressables were byte-throttled, but procedural render lanes could still write large `GraphicsBuffer` payloads through `LockBufferForWrite` in the same frame.
Solution: Extended `GraphicsBufferUploadUtility` with manual upload reservation and pressure-aware frame budget. Voxel surface, coral, wreckage, and scatter uploads now defer when the frame budget is exhausted.
Rejected Alternatives: Silent truncation of buffers; global registry polling from upload dispatchers; forcing every render system through Addressables.
Scalability potential: Weak/pressured devices collapse procedural uploads toward 256 KB/frame, middle devices ramp by quality, high/ultra retain larger direct GPU payloads when pressure is low.
Hardware Impact: 0 allocations; hot path adds primitive byte math and cached pressure read from `SystemDispatcher`.

### Decision 12: Central GraphicsBuffer Upload API Gate

Problem: Many render systems use `GraphicsBufferUploadUtility.UploadArray`, `UploadNativeArray`, or `SetData` wrappers. Manual gating only fixed explicitly audited dispatchers and left the common helper as a possible PCIe burst bypass.
Solution: Moved reservation into the core helper methods. Each direct upload now calls `TryBeginManualUpload(uploadedBytes)` before writing, completes after successful unlock/SetData, and cancels the byte claim when copy guard or Unity upload throws.
Rejected Alternatives: Editing every caller one by one; caller-side boolean API churn; hard-dropping first large upload. Those either miss future callers, create wide churn, or risk bootstrap deadlock.
Scalability potential: Low devices get global byte throttling for central upload APIs. Middle devices ramp through the existing continuous budget. High/Ultra retain larger uploads when pressure is low.
Hardware Impact: 0 allocations; helper adds primitive integer checks and one branch before GPU upload. MX350 gains protection against same-frame utility upload bursts.

### Decision 13: HLOD Impostor Upload Admission

Problem: Far-field octahedral impostor binding writes instance payloads and indirect args through raw `LockBufferForWrite`, bypassing the graphics upload budget during world streaming/HLOD changes.
Solution: Added `TryUploadSingle` to `GraphicsBufferUploadUtility` and gated HLOD impostor instance uploads with byte reservation. Args now use the shared single-record helper.
Rejected Alternatives: Clearing binding on denied upload; per-impostor chunk throttling; touching unrelated UI/AI raw upload lanes. Clearing would create visible popping, chunk throttling is wider design churn, and UI/AI edits are cross-domain without integrator request.
Scalability potential: Weak/pressured devices keep previous far-field visual buffers until budget is available. Middle devices refresh gradually. High/Ultra update immediately when pressure is low.
Hardware Impact: 0 allocations; HLOD bulk upload now pays primitive byte math and avoids unbounded same-frame impostor payload bursts on MX350.

### Decision 14: Budgeted Range Upload API

Problem: Remaining cross-domain raw `LockBufferForWrite` sites often upload partial ranges. Without a shared range helper, each domain must duplicate reservation, unsafe pointer offset, unlock, and cancel logic.
Solution: Added `TryUploadNativeArrayRange` and `TryUploadArrayRange` to `GraphicsBufferUploadUtility`. Both reserve upload bytes, use guarded memcpy, and cancel the claim when copy or unlock fails.
Rejected Alternatives: Mass editing UI/AI/VFX owners in one pass; relying on full-buffer helpers; letting each domain write its own reservation code. Those create cross-domain churn or repeated unsafe bugs.
Scalability potential: Weak devices get one standard range-gated migration path. Middle/high/ultra keep the same API with larger budgets from `GlobalQualityWeight`.
Hardware Impact: 0 allocations; range helper adds primitive bounds math and one byte-reservation branch before GPU copy.

### Decision 15: Marine Snow VFX Upload Admission

Problem: `HectonMarineSnowRenderer` still had local raw `LockBufferForWrite` paths for frame constants, clears, mock wake buffers, and propwash events. These writes could bypass the central graphics upload budget during visually dense underwater scenes.
Solution: Added shared `TryClear` to `GraphicsBufferUploadUtility`, routed local single-row uploads through `TryUploadSingle`, and gated mock wake / propwash event bulk uploads with byte reservations. Propwash upload-buffer index is committed only after successful upload.
Rejected Alternatives: Leaving tiny VFX buffers ungated; clearing visible buffers when budget is denied; scheduling same-frame jobs for one-row visual DTOs. Ungated writes accumulate into the same PCIe burst class, forced clearing causes visual flicker, and same-frame jobs add fence risk.
Scalability potential: Weak devices keep previous VFX GPU state until budget is available. Middle devices refresh VFX signals gradually. High and Ultra keep immediate marine snow, wake, and propwash updates while pressure stays low.
Hardware Impact: 0 allocations. MX350/i3 gains bounded same-frame VFX upload pressure; estimated saved stall class is 10-40 us in dense wake/fog scenes, profiler pending.

### Decision 16: Source-Level Editor Assertions

Problem: `Hecton8.Optimization.Editor.asmdef` does not define a clear compile-time route to runtime Optimization classes. Direct calls from the assertion tool to `VRAMPressureMonitor`, `AssetLoadDispatcher`, or `GraphicsBufferUploadUtility` risk an editor compile dependency violation.
Solution: Converted `VRAMStreamingStaticAssertions1617` to source-text assertions for required formulas and budget gates. The Roslyn APEX verifier remains source-based and writes no report files.
Rejected Alternatives: Adding a new runtime asmdef, modifying editor asmdef references broadly, or moving assertions into a generic editor assembly. All three expand dependency surface for a validation tool.
Scalability potential: Runtime unaffected across low, middle, high, and ultra tiers. Editor validation remains cheap and deterministic.
Hardware Impact: 0 runtime us. Avoids editor compile churn and preserves the no-build throttle.

### Decision 17: Graphics Materials Upload Budget Route

Problem: Graphics material systems still contained local raw `LockBufferForWrite` memcpy upload bodies for visual aging, material visible payloads, and one-row shader constants. These paths could bypass the shared graphics upload byte budget during visual-material refresh.
Solution: Routed `VisualPressureAgingRuntime` aging/degradation uploads and `ShinobuMaterialResponseRuntime` visible/constants uploads through `GraphicsBufferUploadUtility.TryUploadNativeArrayRange` and `TryUploadSingle`.
Rejected Alternatives: Leaving graphics-material uploads as "small enough"; adding a second material-specific throttle; editing unrelated AI/physics/gameplay upload lanes in the same pass. The first keeps drift, the second splits ownership, the third violates domain boundaries.
Scalability potential: Weak devices can defer material refresh while preserving previous visible state. Middle devices refresh continuously within budget. High and Ultra retain full material update cadence when pressure is low.
Hardware Impact: 0 allocations. MX350/i3 gains one shared PCIe admission route for visual aging/material response uploads; estimated stall-class avoidance is 5-25 us in dense material-refresh frames, profiler pending.
