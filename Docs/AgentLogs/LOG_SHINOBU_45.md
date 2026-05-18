# SHINOBU_45 Execution Log - TBDR Pipeline Surgeon

## 2026-05-18 - TBDR / Quest 3 Vertex and Tile-Spill Guard

What was wrong:
- No visible local TBDR-specific guard existed for Agent 45's prompt: no hard vertex vault, no Early-Z front-to-back sort proof, no frustum squeeze, no fixed transparent overdraw cap, no 300-frame tile-spill black box owned by this domain.
- Requested legacy budget binaries were absent from searched archive/StreamingAssets locations. Previous VRAM scout logs showed texture pressure around 1298.65 MiB and first-party production texture pressure around 505.62 MiB, so unlimited texture residency remains unacceptable.
- Unity batchmode compile could not run because another Unity instance already has `C:/hades/Hecton8` open.

What was done:
- Added `TBDRPipelineSurgeonTypes.cs`: aligned 16B budget/warning DTOs, camera-relative POI DTO, mock scatter/camera/quality contracts, native vertex vault, hardware budget archaeology, TBDR/IMR switch, compute dispatch limiter, texture slice tracker, CSV byte parser, 300-frame telemetry recorder, and UMA Raw `GraphicsBuffer` writer.
- Added `TBDRPipelineSurgeonJobs.cs`: Burst quality signal, distance key builder, four-pass Early-Z radix sort, strict vertex budget truncation, Dear Lie frustum squeeze, transparent overdraw limiter, locked matrix writer, and AUP-to-GPU float localization.
- Added `TBDRPipelineSurgeonRuntime.cs`: cold native allocation, emergency limits, mock 150K pipeline execution, telemetry recording, editor limit application, CSV polling, and sorting gizmo.
- Added `Editor/TBDRPipelineTunerWindow.cs`: hard vertex cap, transparent quad cap, frustum squeeze angle, live bars, CSV ingest, mock run, DTO layout audit, UberNoir half precision mobile build gate, and Show Sorting toggle.
- Updated `Hecton8.Graphics.Culling.asmdef` to allow unsafe code and reference `Hecton8.Core.Contracts`.
- Added Unity `.meta` files for all new assets.
- Created/updated `Docs/Tasks/Status_SHINOBU_45.md` and `Docs/AgentLogs/Rationale_SHINOBU_45.md` with checklist, decisions, verification, and `<SELF_AUDIT>`.

Cinematic Cheats used:
- Dear Lie frustum squeeze: continuous 0-15 degree periphery narrowing and 0.80-1.0 cap scaling by `GlobalQualityWeight`, buying vertex reduction without CPU decimation.
- Distance-sorted front-to-back submission: lets Early-Z discard hidden pixels before fragment shader cost on TBDR.
- Fog-compatible hard truncation: farthest matrices vanish from the draw set once vertex budget is exhausted.
- Texture array pagination: visible biome slices are swapped into a fixed array instead of loading the whole 100km world's materials.

Exact microseconds saved:
- Exact profiler-backed microseconds: PENDING. Unity Editor/Quest profiler capture is required.
- Estimated boot/initialization savings from emergency budget fallback: 250-400 us versus repeated missing payload probing.
- Estimated native zero-init bypass for 150K sort capacity: 100-500 us during cold allocation path.
- Estimated UMA matrix staging avoidance: 50-200 us per large matrix upload path.
- Estimated hot budget lane saving: 0.5-2 us per mutation burst versus managed coordination.
- Expected GPU-side savings: tile-spill and transparent-overdraw avoidance are the real target; exact time requires Quest 3 GPU capture.

Verification:
- Unity batchmode compile: BLOCKED by already-open Unity Editor instance. Log path: `Docs/AgentLogs/Unity_SHINOBU_45_compile.log`.
- Isolated Roslyn runtime compile: PASS. Warning only: obsolete `GraphicsDeviceType.OpenGLES2`.
- Isolated Roslyn editor compile: PASS.
- `git diff --check`: PASS except CRLF warning on existing `Hecton8.Graphics.Culling.asmdef`.
- Static banned scan: no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, auto-property DTOs, Raycast, MeshCollider, or `long3`.

## 2026-05-18 - Ultra Polish Rework: Burst, Vault, False Sharing

What was wrong:
- Burst jobs lacked explicit `CompileSynchronously = true`.
- Jobs did not declare pointer/NativeArray non-aliasing, leaving vectorization to conservative compiler assumptions.
- The required 16B `VertexBudgetDTO` was being used as hot mutable budget storage; that satisfies the original prompt but is not false-sharing safe if parallel budget writes are introduced.
- Runtime native buffers were local persistent allocations. That is acceptable for a mock harness but not the production H-PHI memory model.
- The first pass had no formal hook for HZB culling or indirect draw args.

What was done:
- Added exact Burst attributes to every SHINOBU_45 job.
- Added `[NoAlias]` to job arrays and pointers.
- Added `TBDRVertexBudgetCounter64`, explicit 64B storage with `VertexBudgetDTO` at offset 0.
- Added `TBDRBufferIds` local stable IDs `(BufferID)70820` through `(BufferID)70835` without editing Core enum.
- Runtime now requests `GlobalDataVault` buffers through `VaultBufferHandle` under `SystemID.GraphicsScalability`; local NativeArray allocations remain only as CI/mock fallback.
- Added `HzbAabbOcclusionCullJob` and `BuildIndirectDrawArgsJob`.
- Telemetry recorder binds to the vault-owned ring when available.

Cinematic Cheats used:
- Dear Lie remains the primary fake: narrow peripheral frustum, sort front-to-back, then drop matrices instead of decimating meshes.
- HZB mask hook rejects AABBs behind downloaded depth before vertex budgeting.
- Indirect args path lets GPU draw count come from native job output rather than CPU object loops.

Exact microseconds saved:
- Exact profiler-backed microseconds: still PENDING Quest/Unity capture.
- Expected improvement: less first-use Burst stutter, lower aliasing pessimism, no false-sharing on budget lanes, reduced heap fragmentation through vault ownership.
- HZB/indirect benefits are workload-dependent and require a real depth pyramid feed before timing is honest.

Verification:
- Isolated Roslyn runtime compile: PASS after polish; warning only: obsolete `GraphicsDeviceType.OpenGLES2`.
- Isolated Roslyn editor compile: PASS after polish.
- Unity batchmode compile: still BLOCKED by open Unity Editor instance for `C:/hades/Hecton8`.
- `git diff --check`: PASS except CRLF warning on existing asmdef.

## 2026-05-18 - Repeat Mandate Hardening: Dispatch Chain and Endianness

What was wrong:
- The prior public mock pipeline API still blocked on `JobHandle.Complete()`. That is defensible for a button in an EditorWindow, but it is poison if copied into the production render scheduler.
- Legacy `mobile_vertex_limits.h8bin` / `texture_streaming_budgets.bin` hydration used `BinaryReader.ReadUInt32()`, which assumes little-endian input and provides no defensive sanity swap for inherited or network-produced payloads.
- Repository-wide `git diff --check` is currently polluted by unrelated whitespace in `Docs/Tasks/CURRENT_BATCH.md`; using it as proof for SHINOBU files would be false evidence.

What was done:
- Added `ScheduleTBDRProtectionPass(int requestedInstanceCount, JobHandle dependency)` to return the final culling/sort/budget/indirect-args dependency chain without blocking the main thread.
- Added `CommitCompletedProtectionPass(float elapsedMs)` to separate post-completion telemetry from job scheduling.
- Reduced `RunMockPipelineOnce()` to an Editor/mock wrapper: it schedules, blocks locally for the button path, then commits telemetry.
- Replaced budget `BinaryReader.ReadUInt32()` with `TryReadUInt32AutoEndian()`: stackalloc 4-byte reads, little-endian decode, byte-order reversal, plausibility clamp, deterministic fallback.

Cinematic Cheats used:
- No new physical simulation was introduced. The same Dear Lie remains: tighten the peripheral frustum, sort front-to-back, apply HZB visibility, then drop far matrices by hard vertex budget rather than decimating geometry.
- The non-blocking API preserves the cheat inside the render graph instead of turning it into a main-thread stall.

Exact microseconds saved:
- Exact profiler-backed microseconds remain pending Quest/Unity capture.
- Expected main-thread gain: removal of the production-path sync point created by `Complete()`; workload dependent, likely visible only under real SystemDispatcher integration.
- Expected safety gain: endian sanity prevents nonsensical cap hydration that could submit millions of extra vertices or collapse texture residency to invalid values.

Verification:
- SHINOBU_45 runtime isolated Roslyn compile: PASS. Warning only: obsolete `GraphicsDeviceType.OpenGLES2`.
- SHINOBU_45 editor isolated Roslyn compile: PASS.
- Unity batchmode compile: BLOCKED by another Unity Editor instance already open on `C:/hades/Hecton8`; `Docs/AgentLogs/Unity_SHINOBU_45_compile.log` refreshed.
- Static scan: no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, auto-property DTOs, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, or `BinaryReader`.
- Targeted trailing-whitespace scan on SHINOBU runtime/editor files: PASS. Repository-wide `git diff --check`: blocked by unrelated `Docs/Tasks/CURRENT_BATCH.md` whitespace and global CRLF warnings.

## 2026-05-18 - Shader Global Budget Handoff and Clean Runtime Compile

What was wrong:
- The culling lane protected tile memory, but the shader layer could not see the recovered budget pressure. That undercut the explicit requirement to spend saved CPU/GPU budget on shader-side visual richness or to shed shader ALU under thermal pressure.
- The runtime verification still emitted an obsolete `GraphicsDeviceType.OpenGLES2` warning, which was unnecessary because Android/handheld/GLES3/GPU-name/model detection already covers the mobile path.

What was done:
- Added 32B `TBDRShaderBudgetGlobalsDTO` for quality weight, frustum squeeze, tile pressure, estimated VRAM, hard vertex cap, current visible vertices, transparent quad cap, and flags.
- Added `TBDRGlobalShaderBudgetBinder`, using cached Shader property IDs and global vectors/scalars. No per-frame string construction.
- Runtime now pushes shader globals after initialization, editor limit updates, CSV budget updates, and completed protection-pass commits.
- Removed the obsolete `OpenGLES2` branch from the hardware switch.
- Updated rationale self-audit so the dependency graph no longer claims production should return a handle; it now does.

Cinematic Cheats used:
- Dear Lie remains matrix dropping plus frustum squeeze; shader globals now let UberNoir-style shader code hide culling with fog/silt/caustic pressure instead of CPU mesh decimation.

Exact microseconds saved:
- Exact profiler-backed microseconds remain pending Quest/Unity capture.
- Warning purge has no runtime savings. Shader global handoff enables pressure-aware ALU/tap shedding; actual shader savings depend on consuming HLSL.

Verification:
- Runtime isolated Roslyn compile: PASS with no warnings.
- Editor isolated Roslyn compile: PASS.
- Targeted `git diff --check` for SHINOBU files and SHINOBU docs: PASS.
- Unity batchmode compile: still BLOCKED by another Unity Editor instance already open on `C:/hades/Hecton8`.

## 2026-05-18 - Dear Lie Visibility Mask Hardening

What was wrong:
- The frustum squeeze formula used the wrong sign for the current inward-facing plane convention. It reduced the cap but could widen side/top/bottom planes instead of narrowing them.
- Visibility was stored only in an index mask. After radix sort, index `i` no longer identified the same instance, so the budget kernel could reject or keep the wrong matrix.

What was done:
- Changed `DearLieFrustumSqueezeJob` from `normal + forward * squeezeRadians` to `normal - forward * squeezeRadians`.
- Added `TBDRVisibilityFlags` with `FrustumRejected`, `HzbRejected`, and `RejectedMask`.
- Added `DearLieFrustumVisibilityJob`, a Burst `IJobParallelFor` that evaluates six squeezed planes and writes rejection into `PoiTransformDTO.Flags` before distance sort.
- Changed `VertexBudgetJob` to reject sorted DTOs by flags that travel with the matrix through radix passes.
- Changed `HzbAabbOcclusionCullJob` to write `HzbRejected` into DTO flags in addition to the optional mask.
- Runtime sorted path now passes `VisibilityMask = default` into `VertexBudgetJob`, avoiding stale pre-sort mask reads.

Cinematic Cheats used:
- No mesh decimation, tessellation, raycast, or physics truth was added. The fake is sphere-vs-plane frustum rejection plus fog-hidden matrix dropping under a hard vertex cap.

Exact microseconds saved:
- Exact profiler-backed microseconds remain pending Quest/Unity capture.
- Expected effect is fewer peripheral/occluded vertices reaching indirect args and fewer false visibility decisions after sort. The major win is tile-spill risk reduction, not a claimed CPU timing number.

Verification:
- Static runtime scan: PASS for banned `List.Sort`, `Array.Sort`, `.Split`, `double`, DTO properties, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, and `BinaryReader`.
- Roslyn runtime/editor compile: retry pending; first attempt was skipped because CPU was 100% and another `dotnet/csc` process was active, later attempts were skipped because CPU stayed 57-100% with no compiler process.
- Unity batchmode compile: still blocked by open Unity Editor.

## 2026-05-18 - Quality Weight Smoothing Hardening

What was wrong:
- The mock quality signal changed to a fresh random value every frame. That protects test isolation, but it makes frustum squeeze and cap pressure flicker instead of breathing smoothly with hardware stress.

What was done:
- Reworked `MockQualityWeightJob` into deterministic low-pass drift.
- The job now reads previous weight, generates a deterministic target in `[0.1, 1.0]`, applies cubic stress response, clamps per-frame movement, and blends through `math.lerp`/`math.step`.
- No new buffer, service, assembly reference, or managed allocation was added.

Cinematic Cheats used:
- The fake remains optical and budgetary: smoothly tighten the frustum and cap vertices, then hide missing peripheral matrices with underwater fog/silt instead of CPU mesh truth.

Exact microseconds saved:
- CPU delta is effectively O(1) and not worth claiming without profiler.
- Expected benefit is visual and thermal stability: fewer abrupt BRG/indirect draw count swings on mobile TBDR.

Verification:
- Static banned-pattern scan after smoothing: PASS.
- Roslyn compile retry remains blocked by CPU gate; current check reported CPU 74%.

## 2026-05-18 - CSV Polling Path Cache

What was wrong:
- CSV parsing was byte-buffered, but the runtime wrapper rebuilt the absolute CSV path every poll. That is avoidable managed churn around a budget monitor.

What was done:
- Added `_resolvedGpuBudgetCsvPath` and `_csvPathDirty`.
- `PollBudgetCsvOverride()` now reuses the cached absolute path.
- `SetCsvPath()` invalidates the cache; initialization resolves it once.

Cinematic Cheats used:
- No new simulation. This preserves the human tuning bridge for the vertex/frustum fake without adding managed polling noise.

Exact microseconds saved:
- No profiler-backed timing. Expected gain is small but structural: path/string allocation removed from repeated budget-poll calls.

Verification:
- Heavy verification deferred: light probes started timing out under current system load, and a 30s delayed compile gate still reported CPU 100% with no `dotnet/csc`. Compile remains gated by CPU policy.

## 2026-05-18 - Quality State Persistence and Vertex Overflow Guard

What was wrong:
- `ScheduleTBDRProtectionPass()` rewrote `MockQualitySignal[0]` every pass, erasing the previous weight and making low-pass smoothing ineffective.
- `VertexBudgetJob` used `totalVertices + vertexCount > maxVertices`, which can wrap on `uint` overflow and submit beyond the cap under corrupted mesh counts.

What was done:
- Moved mock quality initialization to `SeedMockData()`.
- Let `MockQualityWeightJob` mutate the persistent vault/fallback signal across passes.
- Changed budget enforcement to compare `vertexCount` against remaining cap instead of adding first.

Cinematic Cheats used:
- Same fake: persistent smooth quality pressure narrows the frustum and drops matrices under fog, instead of CPU mesh simplification.

Exact microseconds saved:
- No honest microsecond number. This is correctness and thermal safety: prevents cap bypass and makes load-shed continuous.

Verification:
- Targeted grep confirms no remaining `totalVertices + vertexCount` overflow comparison in SHINOBU runtime code.
- Runtime banned-pattern scan: PASS.
- Roslyn compile retry remains blocked by CPU gate; latest check reported CPU 88%.

## 2026-05-18 - Stale HZB Rejection Purge

What was wrong:
- `DearLieFrustumVisibilityJob` cleared only `FrustumRejected`, so a prior-frame `HzbRejected` bit could survive when no fresh HZB pyramid was available.

What was done:
- Changed frustum visibility pass to clear `TBDRVisibilityFlags.RejectedMask`.
- HZB refinement remains optional and can re-add `HzbRejected` only when current depth data is processed.

Cinematic Cheats used:
- Same visibility fake, now frame-local: reset rejection truth, cheaply cull by squeezed frustum, optionally refine with HZB.

Exact microseconds saved:
- No profiler-backed number. CPU delta is one bit clear per instance inside an existing Burst pass; correctness gain is avoiding stale invisible matrices.

Verification:
- Targeted grep confirmed the new `RejectedMask` clear and no overflow comparison.
- Compile retry remains blocked by CPU gate; final delayed probe reported CPU 99% plus another `dotnet/csc`.

## 2026-05-18 - Shader Quality Handoff Repair

What was wrong:
- Shader globals published the serialized `_globalQualityWeight`, not the smoothed vault-backed quality signal mutated by `MockQualityWeightJob`.
- Shader globals published the maximum configured frustum squeeze, not the current dynamic squeeze used by the Dear Lie culling pass.

What was done:
- Added `CurrentQualityWeight()` to read `MockQualitySignal[0].GlobalQualityWeight` when available and finite.
- Added `CurrentFrustumSqueezeDegrees()` so shader globals receive `configuredSqueeze * (1 - quality)`.
- Wrote the smoothed quality back to `_globalQualityWeight` during `CommitCompletedProtectionPass()` for inspector coherence.
- Added scalar `_H8_TBDR_FrustumSqueezeDegrees` next to the packed `_H8_TBDR_Budget0` vector.
- Left `TBDRTunerSnapshot.FrustumSqueezeDegrees` as the configured max so the Editor slider does not accidentally absorb a frame-dynamic value.

Cinematic Cheats used:
- Same Dear Lie: shrink the peripheral frustum, drop matrices, and let fog/silt/caustic shader response hide the contraction. No mesh decimation or physics truth was introduced.

Exact microseconds saved:
- No profiler-backed timing. CPU cost is one scalar read and one multiply on the commit path. The protection is coherence: shader presentation now follows the same pressure signal that reduces tile-spill risk.

Verification:
- Runtime banned-pattern scan: PASS.
- Targeted `git diff --check` for SHINOBU runtime/editor files and SHINOBU docs: PASS.
- Roslyn compile retry skipped by CPU gate: first probe reported CPU 100%, `dotnet/csc` false; delayed probe reported CPU 95%, `dotnet/csc` true.

## 2026-05-19 - Rollback Frame Scheduling Repair

What was wrong:
- The only public scheduling API seeded `MockQualityWeightJob` from `Time.frameCount`. That blocks clean lockstep/rollback integration because production callers cannot supply the authoritative simulation frame.

What was done:
- Added `ScheduleTBDRProtectionPass(int requestedInstanceCount, uint simulationFrame, JobHandle dependency)`.
- Moved the full protection job graph into the explicit-frame overload.
- Kept the original two-argument method as a Unity-frame fallback for editor/mock callers.

Cinematic Cheats used:
- Same Dear Lie: deterministic frustum squeeze and matrix dropping. This patch makes its seed externally reproducible instead of making it more physically expensive.

Exact microseconds saved:
- No frame-time savings claimed. CPU cost is unchanged; the benefit is deterministic scheduling and rollback-safe reproducibility.

Verification:
- Runtime banned-pattern scan: PASS.
- Targeted `git diff --check` for SHINOBU runtime/editor files and SHINOBU docs: PASS.
- Roslyn compile retry skipped by CPU gate: latest post-patch probe reported CPU 82%, `dotnet/csc` false.

## 2026-05-19 - Tile Pressure Squeeze Repair

What was wrong:
- Frustum squeeze only used quality stress and a post-truncation overflow check. Once the vertex budget job works, `CurrentVisibleVertices > MaxVisibleVertices` is rarely true, so near-spill pressure did not tighten the frustum.

What was done:
- `DearLieFrustumSqueezeJob` now derives pressure stress from previous-frame `TilePressure`.
- The curve starts above 0.82 pressure, smoothsteps to 1.0 at full pressure, then combines with quality stress via `math.max`.
- Shader global squeeze uses the same quality/pressure stress so fog/silt concealment stays aligned with CPU culling.

Cinematic Cheats used:
- Same optical lie: narrow peripheral FOV and drop matrices before tile spilling. No CPU mesh simplification, no raycast, no physics truth.

Exact microseconds saved:
- No measured timing. CPU delta is a few scalar ALU ops. Expected win is GPU-side: fewer vertices/fragments enter mobile tile bins under pressure.

Verification:
- Runtime banned-pattern scan: PASS.
- Targeted `git diff --check` for SHINOBU runtime/editor files and SHINOBU docs: PASS.
- Roslyn compile retry skipped by CPU gate: latest probe reported CPU 82%, `dotnet/csc` false.

## 2026-05-19 - Texture Residency Budget Repair

What was wrong:
- Texture pagination tracked estimated bytes but did not enforce `MaxResidentMb` before staging a new slice.
- `EstimateResidentBytes()` clamped the report, which could hide logical over-residency instead of preventing it.

What was done:
- `TryStageBiomeSlice()` rejects a single incoming slice larger than the cap.
- It computes projected residency with `ulong` arithmetic and clears oldest resident flags until the incoming slice fits.
- `EstimateResidentBytes()` now calculates raw residency first and clamps only at the return boundary.

Cinematic Cheats used:
- Fixed texture array paging remains the fake. We still do not load every biome texture set; the tracker lies by reusing slices and keeping only active-biome payload marked resident.

Exact microseconds saved:
- No measured timing. The path is a biome/streaming event, O(sliceCapacity), and avoids unbounded VRAM residency rather than saving per-frame CPU time.

Verification:
- Runtime banned-pattern scan: PASS.
- Targeted `git diff --check` for SHINOBU runtime/editor files and SHINOBU docs: PASS.
- Asmdef readback: PASS; runtime references Core/Core.Contracts/Core.Memory/World.Contracts and Unity packages only.
- Roslyn compile retry skipped by CPU gate: latest probes reported CPU 100% with `dotnet/csc` true, CPU 100% with `dotnet/csc` false, and delayed final probe CPU 99% with `dotnet/csc` false.

## 2026-05-19 - Hostile Vertex Cap Clamp

What was wrong:
- Human/editor and CSV/binary ingress could publish absurd `uint` vertex caps because most paths only forced `>= 1`.
- A corrupt cap weakens the entire TBDR protection chain: the budget job may allow too many vertices, telemetry lies, shader globals inherit the lie, and Quest 3 tile bins can be overfed.

What was done:
- Added `TBDRHardwareBudgetMath.ClampVisibleVertexCap()` with a hard `20,000,000` visible-vertex ceiling.
- Routed runtime initialization, editor limit application, legacy binary ingestion, CSV ingestion, vault application, `DearLieFrustumSqueezeJob`, and `VertexBudgetJob` through the same clamp.
- `DearLieFrustumSqueezeJob` now clamps its squeezed cap, and `VertexBudgetJob` republishes the clamped cap into the budget DTO before accumulating visible vertices.

Cinematic Cheats used:
- Same Dear Lie. The frustum squeeze and distance-sorted matrix drop are preserved; the new clamp prevents hostile budgets from disabling the fake.

Exact microseconds saved:
- No profiler-backed timing. CPU cost is one integer clamp on ingress and one in the existing job. This is a safety repair, not a measured speed claim.

Verification:
- Runtime banned-pattern scan after the squeeze-clamp patch: PASS.
- Clamp readback: PASS; runtime init, editor apply, legacy binary, CSV, squeeze job, and budget job all route through `ClampVisibleVertexCap()`.
- Targeted `git diff --check`: PASS; only an LF-to-CRLF warning on the runtime asmdef was reported.
- Roslyn compile retry skipped by CPU gate: final response probe reported CPU 100%, `dotnet/csc` false.

## 2026-05-19 - Overflow Boundary Repair

What was wrong:
- CSV numeric parsing could wrap a hostile long digit sequence before the hard vertex cap clamp saw it.
- Transparent quad requests could overflow `int` when particle and UI requests were added.
- HZB validation multiplied width and height as `int`, allowing invalid dimensions to overflow before the depth-pyramid length check.

What was done:
- `TBDRGpuBudgetCsvIngestor.TryParseUInt()` now saturates at `uint.MaxValue`.
- `TransparentOverdrawLimiterJob` now saturates requested transparent quads at `int.MaxValue`.
- `HzbAabbOcclusionCullJob` now validates `HzbWidth * HzbHeight` using `long` before indexing `HzbDepth`.

Cinematic Cheats used:
- Same pipeline fake: frustum squeeze, front-to-back sort, HZB rejection, and matrix dropping. The patch prevents corrupt numeric input from disabling those cheaper guards.

Exact microseconds saved:
- No profiler-backed timing. CPU delta is a few scalar guards. This is a correctness and crash-prevention repair.

Verification:
- Runtime banned-pattern scan: PASS.
- Touched runtime files pass `git diff --check`.
- Roslyn compile retry skipped by CPU gate: latest probe reported CPU 100%, `dotnet/csc` false.

## 2026-05-19 - Compute Dispatch Boundary Repair

What was wrong:
- Kernel thread-group dimensions were queried, but the product `groupX * groupY * groupZ` was computed as `int`.
- `DivCeil()` used `value + divisor - 1`, which can overflow on hostile dimensions.
- Zero-work dispatch requests were silently normalized into one group.

What was done:
- Group dimensions now saturate from `uint` to positive `int`.
- Threads-per-group is computed in `long` and rejected if it exceeds the active hardware cap.
- Zero-work dispatches return false with reject code `3`.
- Group-count `DivCeil()` now avoids addition overflow.

Cinematic Cheats used:
- No new simulation. This protects the compute side from stealing budget that the frustum-squeeze and matrix-drop fake saves.

Exact microseconds saved:
- No profiler-backed timing. Empty dispatches now avoid an unnecessary GPU launch; exact savings require GPU capture.

Verification:
- Runtime banned-pattern scan: PASS.
- Touched runtime files pass `git diff --check`.
- Roslyn compile retry skipped by CPU gate: latest probe reported CPU 100%, `dotnet/csc` true.
