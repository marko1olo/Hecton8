# SHINOBU_263 Rationale

Status: PENDING VERIFICATION.

Problem: Need CPU-side Gerstner wave height/normal authority for buoyancy without GPU stalls.
Solution: Use DataVault-owned unmanaged buffers and Burst jobs in SIMULATION/POST_SIMULATION. Keep rendering waves presentation-owned; the solver is a deterministic proxy for physics queries.
Rejected Alternatives: GPU readback blocks physics; Crest/runtime material or RenderTexture sampling is forbidden as physics truth; per-object OOP wave components fragment memory and break SIMD.
Scalability potential: Low uses 1-2 octaves plus coarse macro grid. Middle uses 4 octaves. High uses 6 octaves. Ultra uses up to 8 octaves and denser telemetry/gizmo views.
Hardware Impact: On i3/MX350, removing GPU waits avoids millisecond stalls; packed 64-byte wave rows keep L1 traversal predictable.

Problem: AUP coordinates can exceed float phase precision for sine/cosine.
Solution: Convert double3 AUP to localized float XZ using wavelength modulo and phase wrapping to [-PI, PI] before trig.
Rejected Alternatives: Raw double phase into `math.sin`/`math.cos` wastes precision and ALU; Transform-space sampling is not authority.
Scalability potential: Low keeps only long wavelengths; Ultra preserves high-frequency octaves once localized.
Hardware Impact: Stable local float phase prevents edge jitter without expensive double trig on low-end silicon.

Problem: Prompt requires global authority but current source already has heavy DataVault usage and many agents editing concurrently.
Solution: Add narrowly scoped wave DTO/job/runtime files under physics buoyancy domain and BufferID entries if no existing wave route exists. Avoid cross-domain hard dependencies on weather/rendering.
Rejected Alternatives: Modifying Crest bridge, weather director, or global render shaders would invade sibling domains.
Scalability potential: Data contract can accept future weather-owned spectrum without changing solver kernel.
Hardware Impact: Cold dependency caching avoids hot registry polling on low-end CPUs.

Problem: Full trigonometric precision is too expensive for 50k buoyancy coordinates under thermal pressure.
Solution: Use continuous `GlobalQualityWeight` for both octave count and polynomial trig order: low quality uses cubic sine/cosine approximation, high/ultra blends toward seventh-order. This is a Math LOD, not a binary switch.
Rejected Alternatives: Always calling `math.sin/cos` for every octave; using lookup textures or GPU height readback for physics.
Scalability potential: Low = 1 octave + cubic polynomial + macro grid. Middle = 3-4 octaves + blended polynomial. High = 6 octaves + mostly seventh-order. Ultra = 8 octaves + dense telemetry/gizmo stress view.
Hardware Impact: On i3/MX350, low quality sheds up to 7 octave passes and replaces high-cost trig with FMAs; expected savings are hundreds of microseconds at 50k probes before Burst import proof.

Problem: Existing project still contains GPU readback symbols in Crest/visual/legacy buoyancy paths.
Solution: Do not delete sibling-agent systems. Publish a CPU analytical authority route through `Shinobu263WaveRequests/Results` and record GPU symbols in the scanner report as non-authority findings.
Rejected Alternatives: Disabling `AsyncBuoyancyReadbackRuntime` or Crest bridges would invade adjacent agent ownership and risk compile conflicts.
Scalability potential: Low devices consume CPU proxy results. High/Ultra can keep GPU visual water overkill without physics waiting on it.
Hardware Impact: Physics no longer pays GPU synchronization latency; existing async systems remain isolated until integrator decides route precedence.

Problem: Editor tuning must not mutate DataVault while Burst locks are active.
Solution: `AnalyticalWaveTunerWindow` reads generation handles and writes only through `TryAcquireWriteLock` with `SystemID.CoreDiagnostics`.
Rejected Alternatives: Direct NativeArray writes from inspector or serialized MonoBehaviour state.
Scalability potential: Lead programmers can sweep Low/Middle/High/Ultra settings in Play Mode without recompilation.
Hardware Impact: Editor-only cost; no player runtime cost.

Problem: Parallel atomic counters used adjacent `int` lanes, which can share one L1 cache line and trigger false sharing under worker contention.
Solution: Replace solver counters with `WaveMathCounterLane`, explicit 64-byte rows. `EvaluateAnalyticalWavesJob` increments `Value` through `Interlocked.Add` while evaluated/coarse/nonfinite counters occupy separate cache lines.
Rejected Alternatives: Keep `NativeArray<int>` and accept MESI invalidation; merge counters into one packed struct and serialize writes on one cache line.
Scalability potential: Low and Middle still write three lanes only; High and Ultra can increase telemetry detail without counter line contention becoming the bottleneck.
Hardware Impact: On i3/MX350 and ARM64 mobile cores, avoids cross-core cache-line invalidation around the hottest shared telemetry counters.

Problem: `FixedTick` could refresh DataVault dependencies and cold boot handles, turning a read-phase solver tick into a hidden registry/bootstrap route.
Solution: Confine `GlobalRegistry.DataVault` lookup to `Awake`, `OnEnable`, and hot-swap listener. `FixedTick` now requires `_coldBootCompleted` and generation handles before scheduling.
Rejected Alternatives: Poll registry every frame to recover from missing services; allocate/grow Vault handles from the solver tick.
Scalability potential: All tiers use the same authority route; quality changes never alter Vault ownership or route identity.
Hardware Impact: Removes hot dependency churn and keeps the fixed-tick work bounded to DataVault resolve, lock, schedule, and write.

Problem: Wave phase time advanced by a constant `0.02f`, which diverges from dispatcher-controlled fixed-step timing under time dilation or test cadence.
Solution: Use the `fixedDeltaTime` argument passed by the tick dispatcher, sanitized with finite/positive guards, to advance `GerstnerWaveTuningDTO.TimeSeconds`.
Rejected Alternatives: `Time.fixedDeltaTime` read from Unity global state; hardcoded 50 Hz phase drift.
Scalability potential: Continuous quality still changes math fidelity only; cadence authority remains dispatcher-owned across Low, Middle, High, and Ultra.
Hardware Impact: Correctness improvement with no measurable ALU/GC cost; avoids rollback/test desync from implicit time source.

Problem: Mock request generation could overwrite a live external producer's first current-frame request, violating one owner for request truth.
Solution: Add a stateful fallback gate that seeds mock requests for isolation but refuses to seed over an active, non-mock, current-frame request with a valid hash and finite AUP.
Rejected Alternatives: Unconditional CI/mock request rewrite each frame; scene searches to discover a producer.
Scalability potential: Low-tier mock isolation remains available for local physics proof; production routes can publish real request arrays without changing DTOs.
Hardware Impact: One first-row check per fixed tick; prevents wasted solver work on overwritten producer data.

Problem: `FixedTick` still read `Application.isPlaying`, which is not an allocation but is unnecessary Unity global state in the solver cadence.
Solution: Add cached `_runtimeActive` lifecycle state and gate the hot tick on that field.
Rejected Alternatives: Keep Unity static property reads in every fixed solver pass.
Scalability potential: Same route across Low, Middle, High, and Ultra; lifecycle state does not alter math fidelity.
Hardware Impact: Tiny CPU cleanup, but it removes one avoidable Unity boundary touch from the hot route.

Problem: Newly added SHINOBU_263 Unity-visible scripts lacked `.meta` files, allowing Unity to mint local GUIDs during import.
Solution: Add stable `.meta` files for the five SHINOBU_263 `.cs` assets and verify GUID uniqueness.
Rejected Alternatives: Defer GUID creation to the local editor import.
Scalability potential: Import hygiene only; no runtime quality impact.
Hardware Impact: 0 runtime cost; prevents import churn and merge noise.

Problem: Generated `.csproj` files are stale and cannot currently prove SHINOBU_263 compilation.
Solution: Record that `Hecton8.Core.csproj` does not include the SHINOBU_263 sources and still references missing `HectonScannerProjectionState.cs`; compile proof must wait for Unity project regeneration/import and CPU gate clearance.
Rejected Alternatives: Run `dotnet build` anyway and claim false confidence.
Scalability potential: Verification route only; no runtime quality impact.
Hardware Impact: Avoids wasting CPU/IO on an invalid stale-project build.

Problem: Subagent audit found a hidden hot AUP route: `FixedTick` prepared tuning by reading registry-backed `HectonFloatingOrigin.CurrentTotalOffsetDouble`.
Solution: Implement `IOriginShiftListener`, cache `HectonFloatingOrigin.LastShiftEvent` into `_cachedOriginAUP/_cachedOriginShiftSequence`, and feed tuning/gizmo from the cached snapshot. `FixedTick` now consumes the owner-published shift fact without hot registry polling.
Rejected Alternatives: Keep direct static origin reads; call `GlobalRegistry.FloatingOrigin` from the solver cadence; add a direct World-domain AUP DTO dependency.
Scalability potential: Low, Middle, High, and Ultra tiers share the same AUP authority route; quality changes only reduce octave/trig cost, never origin ownership.
Hardware Impact: Removes a hidden managed registry boundary from each solver tick and prevents rollback/rebase ambiguity on low-end CPUs.

Problem: Result and telemetry payloads preserved raw AUP without a shift sequence proof, so a blind rollback snapshot could not prove which floating-origin generation authored the row.
Solution: Keep `OceanSampleResultDTO` at 64 bytes by replacing the non-authority result `ActiveOctaves` lane with `OriginShiftSequence`; telemetry keeps `ActiveOctaves` and gains `OriginShiftSequence` by splitting padding; tuning gains `OriginShiftSequence/OriginShiftFlags` in its final 16-byte lane.
Rejected Alternatives: Expand result rows to 80/96 bytes and pay higher result bandwidth; store shift sequence only in managed runtime fields; rely on frame index as a proxy for AUP generation.
Scalability potential: The DTO layout remains fixed across all quality weights while the math LOD continues to scale from coarse macro-grid to full octave evaluation.
Hardware Impact: Preserves one 64-byte result row per sample and avoids an extra cache-line touch in the hottest output buffer.

Problem: The editor scanner report overclaimed coverage because it did not check hidden origin reads or raw AUP sequence fields, and its shared-report upsert had a `math.max` compile risk outside a mathematics using context.
Solution: Add text-level gates for `HectonFloatingOrigin.CurrentTotalOffsetDouble` in SHINOBU_263 analytical files and AUP shift sequence fields in contracts; replace `math.max` with `Math.Max`.
Rejected Alternatives: Leave the scanner as trig/array-only; generate a report that passes while origin authority is unverified.
Scalability potential: Editor-only validation; no runtime quality impact.
Hardware Impact: 0 runtime cost; catches authority regressions before they become profiling/debug time loss.

Problem: The scanner conflated `AsyncGPUReadback` type references with synchronous GPU waits, which could produce noisy findings against the sibling async-readback route even when no blocking wait exists in frame loops.
Solution: Split async readback references into `AsyncGpuReadbackTypeHits` and keep `GpuWaitSymbolHits` for actual blocking invocations such as `ReadPixels`, `WaitForCompletion`, or frame-loop `GetData`.
Rejected Alternatives: Treat all readback symbols as fatal; delete or rewrite sibling SHINOBU_264 async ownership from this domain.
Scalability potential: Editor-only validation; keeps low-tier CPU authority proof separate from higher-tier nonblocking presentation/readback assist routes.
Hardware Impact: 0 runtime cost; reduces false-positive investigation time.

Problem: The cold CSV parser accepted numeric prefixes with trailing garbage, allowing malformed tuning cells to hydrate silently truncated wave profiles.
Solution: `TryParseFloat(ReadOnlySpan<byte>)` now requires the parse cursor to consume the entire trimmed span before accepting the value.
Rejected Alternatives: Use `float.Parse`/culture-sensitive parsing; accept truncation and rely on designer review.
Scalability potential: Low through Ultra all consume the same clean profile rows; quality only changes active math, not malformed source interpretation.
Hardware Impact: Cold boot-only branch, no hot-frame cost; prevents bad authored spectra from causing unnecessary debug/profiling work.

Problem: Localizing wave samples to `SampleAUP - LocalOriginAUP` removed the absolute phase contribution of the floating origin, so an origin shift could preserve local coordinates but change the evaluated Gerstner phase.
Solution: Keep the AUP jitter rule by subtracting the owner origin in double precision, then add `dot(direction, LocalOriginAUP) mod wavelength` once per octave before float4 phase evaluation. This reconstructs absolute phase modulo the wavelength without feeding large absolute coordinates into lane trig.
Rejected Alternatives: Raw absolute double trigonometry in every sample lane; wavelength-wrapping the local delta by the largest wave only; accepting origin-shift phase pops as visual-only.
Scalability potential: Low still runs 1-2 octaves and pays 1-2 origin projections; Middle/High/Ultra scale octave count continuously while sharing the same phase-preserving formula and fixed DTO layout.
Hardware Impact: One double projection per active octave group is cheaper than four double lane phases and preserves deterministic buoyancy continuity across rebases on i3/MX350 and ARM64 mobile silicon.

Problem: Subagent audit found request `ShiftFrameID` was only copied into results; stale-origin requests could still be localized and solved against the current origin.
Solution: Build a per-lane `shiftMatch` mask before localization, write stale lanes as `FlagStaleOrigin`, increment the 64-byte counter lane 3, and restrict full/coarse solving to `solveActive`.
Rejected Alternatives: Trust producers to clear stale requests; evaluate stale rows and rely on consumers to inspect `OriginShiftSequence`; expand the result DTO with a separate stale count.
Scalability potential: All quality tiers share the same authority fence; low tier avoids wasted stale lane work, and high/ultra keep phase-rich waves only for sequence-valid samples.
Hardware Impact: Prevents rollback/rebase contamination and skips full octave evaluation for stale lanes; no payload growth, one existing counter lane used.

Problem: Clearing four counter rows through `ResetWaveMathCountersJob` created a scheduler job smaller than the project's amortization threshold.
Solution: Clear the four `WaveMathCounterLane` rows synchronously inside the already locked owner window before scheduling mock/grid/evaluate jobs.
Rejected Alternatives: Keep a tiny scheduled reset; fold counter clear into evaluate and risk race-dependent first-writer behavior.
Scalability potential: Same across Low through Ultra; counter reset cost is fixed at four cache lines and quality-independent.
Hardware Impact: Removes one job scheduler entry per fixed tick and avoids worker wakeup overhead for 256 bytes of writes.

Problem: The scanner claimed zero solver forbidden hot-path hits while only treating `Update`, `FixedUpdate`, and `LateUpdate` as frame loops.
Solution: Expand scanner hot roots to dispatcher ticks and SHINOBU_263 solver helpers, then check those roots for `GlobalRegistry`, `Application/Time`, allocations, LINQ-like calls, foreach, and direct `Complete`.
Rejected Alternatives: Keep a frame-loop-only scanner; make a broad project-wide scanner that would flag sibling domains outside SHINOBU_263 ownership.
Scalability potential: Editor-only validation; it protects all device tiers from hidden hot-path regressions.
Hardware Impact: 0 runtime cost; reduces false proof risk before Unity import.

Problem: Packed `ResolveNormal` sanitized the length input with `math.select` but still multiplied the original vector by `rsqrt`, so an eager evaluation path could compute with NaN/Inf before the final select returned fallback.
Solution: Materialize a finite `safe` normal vector first, compute length from that vector, and multiply only `safe * rsqrt(max(lengthSq, epsilon))`. Also sanitize displacement vectors once in packed and scalar paths.
Rejected Alternatives: Trust the final `math.select` result; add exception/log paths inside Burst; clamp every component with branches.
Scalability potential: Low through Ultra keep the same DTO and quality math; the finite-vector helper protects all octave counts without adding route divergence.
Hardware Impact: Same number of hot vector stores, fewer duplicate `float3` constructions, and no NaN propagation into result rows on i3/MX350 or ARM64 mobile cores.

Problem: The hot-root scanner expanded to dispatcher ticks, but helper methods directly called from `FixedTick` for Vault resolve/lock/counter clear/unlock still had to be inferred manually.
Solution: Add `TryPrepareRuntimeVault`, `TryResolveRuntimeBuffers`, `TryLockJobBuffers`, `TryLock`, `ClearCounterLanes`, `UnlockJobBuffers`, and `Unlock` to the SHINOBU_263 scanner hot-root set.
Rejected Alternatives: Mark only public ticks as hot; run a project-wide scanner that would produce cross-agent noise; add runtime asserts to prove editor-only policy.
Scalability potential: Editor-only validation; all quality tiers benefit from catching accidental registry/time/allocation regressions in the same route.
Hardware Impact: 0 runtime cost; prevents hidden hot helper regressions before Unity import.

Problem: The Black Box dump wrote raw telemetry rows in physical ring storage order with no header, leaving the decoder to guess row size, cursor, and kernel identity.
Solution: Write a 32-byte little-endian fault header and serialize telemetry rows in oldest-to-newest order without changing the 64-byte row DTO.
Rejected Alternatives: Keep raw order; allocate a managed reordered byte array; expand `WaveMathTelemetryEntry` with dump metadata.
Scalability potential: Low through Ultra keep the same 300-row telemetry capacity and DTO stride; only fault decoding improves.
Hardware Impact: 0 normal-frame cost; fault-only dump adds two span writes and a stackalloc 32-byte header, avoiding heap allocation.

Problem: The telemetry cursor originally wrapped to a slot index, so a dump could not distinguish an early partially filled ring from a fully wrapped ring with the same slot value.
Solution: Treat `TelemetryCursor[0]` as a monotonic write count. The dump header now records write count, oldest-start slot, and valid-row count; early dumps write valid rows first and preserve zeroed unwritten rows, while wrapped dumps start at the computed oldest slot.
Rejected Alternatives: Add a second cursor buffer; expand `WaveMathTelemetryEntry`; infer ring fullness from nonzero frame hashes.
Scalability potential: Low through Ultra keep the same ring capacity and row stride; forensic decoding becomes deterministic independent of when a crash occurs.
Hardware Impact: One integer increment instead of a wrapped cursor write per telemetry frame; fault-only dump adds two header fields and no heap allocation.

Problem: Runtime authoring fields had ranges/headers but no tooltips, leaving designer-facing controls unclear and violating the local MonoBehaviour authoring contract.
Solution: Add explicit `[Tooltip]` metadata to each SHINOBU_263 serialized runtime field without changing field names, defaults, DTOs, or hot-path logic.
Rejected Alternatives: Add a separate ScriptableObject authoring layer; leave field purpose documented only in architecture docs.
Scalability potential: Low through Ultra tuning remains the same; editor-facing clarity reduces misconfiguration of capacity, macro grid, and mock/CSV boot routes.
Hardware Impact: Editor metadata only; no player runtime allocation or frame cost.

Problem: Octave quality scaling still used an integer active-octave boundary, so a thermal `GlobalQualityWeight` drift could add or remove the last octave as a visible/physical step.
Solution: Introduce `ResolveOctaveBudget` with `math.smoothstep(GlobalQualityWeight)` and fade the last active octave through `ResolveOctaveWeight` in both packed full evaluation and scalar macro-grid generation. Cache the budget once per SIMD group/scalar sample before the octave loop.
Rejected Alternatives: Keep floor-based integer octave culling; always run all octaves and multiply by zero; use a binary low/high solver profile.
Scalability potential: Low keeps one dominant swell plus at most a fractional transitional octave, Middle fades into additional detail, High/Ultra reach full octave amplitude without route or DTO changes.
Hardware Impact: On i3/MX350 the extra transitional octave is bounded to one row during quality ramps; the budget cache avoids repeated smoothstep inside every octave and buys pop-free thermal breathing without reverting to all-octave ALU.

Problem: The 32-byte Black Box dump header used `stackalloc`; live fields were written, but reserved bytes could contain undefined stack data and break deterministic binary comparison.
Solution: Clear the stack span with `header.Clear()` before writing magic, row size, capacity, cursor, and kernel hash.
Rejected Alternatives: Trust unwritten padding; allocate a managed zeroed array; add extra metadata fields into the 64-byte telemetry DTO.
Scalability potential: The dump format remains fixed across Low, Middle, High, and Ultra tiers; quality only affects recorded math fidelity, not forensic byte layout.
Hardware Impact: Fault-only 32-byte zero fill, 0 normal-frame cost; produces stable `.h8dump`/`.bin` headers for low-end and desktop crash analysis.

Problem: `PostFixedTick` wrote `WaveMathTelemetryEntry` rows and advanced the telemetry cursor through resolved Vault views without locking the telemetry ring/cursor buffers.
Solution: Add telemetry-specific Vault lock bits and lock `Shinobu263WaveTelemetryRing` plus `Shinobu263WaveTelemetryCursor` after the solver job finalizes and before telemetry write/dump readback. The existing unlock route now releases telemetry locks before job-buffer locks.
Rejected Alternatives: Treat telemetry as owner-only and leave it unlocked; hold telemetry locks across the full solver job; allocate a separate managed diagnostic buffer.
Scalability potential: Low through Ultra keep the same telemetry capacity and DTO stride; the lock route protects forensic proof without changing quality math.
Hardware Impact: 0 solver ALU cost; two post-fixed lock increments only after the main job is complete, preventing defrag/diagnostic races around the ring.

Problem: Macro swell grid generation used `EvaluateScalar`, whose amplitude formula omitted `StormWeight01`, while the full packed solver multiplied amplitude by the storm curve.
Solution: Move amplitude resolution into `AnalyticalGerstnerWaveMath.ResolveAmplitude` and call it from both packed lane evaluation and scalar macro-grid evaluation.
Rejected Alternatives: Keep coarse grid as a visibly different storm envelope; duplicate the storm formula in scalar path; remove storm scaling from full analytical lanes.
Scalability potential: Low-tier coarse samples and Ultra-tier full analytical samples now share the same authored storm amplitude curve while still varying octave count continuously by quality.
Hardware Impact: Centralized formula keeps ALU cost neutral and prevents coarse/full buoyancy disagreement during storms on low-end silicon.

Problem: Long sessions grow `TimeSeconds` beyond the range where a 32-bit float preserves sub-frame phase precision for `phaseVelocity * waveNumber * time`, even though the final phase is wrapped.
Solution: Repurpose the existing 8-byte tuning padding lane as `double PhaseTimeSeconds`, advance it from dispatcher fixed delta, and wrap the time phase in double through `ResolveTimePhaseModulo` before converting to float SIMD lanes.
Rejected Alternatives: Leave `float TimeSeconds`; periodically reset time and accept visible phase discontinuities; expand the tuning DTO beyond 128 bytes.
Scalability potential: Low through Ultra keep the same DTO stride and octave-quality curve; high-quality long sessions retain phase continuity without changing ownership or save identity.
Hardware Impact: One double modulo per active octave group, not per sample lane; prevents long-run precision jitter on ARM64 and desktop without increasing hot buffer bandwidth.

Problem: The new `PhaseTimeSeconds` lane defaults to zero when an older or partially hydrated tuning DTO still carries nonzero `TimeSeconds`, so the first post-upgrade tick could treat zero as authoritative and reset wave phase.
Solution: Add `ResolvePhaseTimeSeconds`; it uses the double lane only when positive and finite, otherwise seeds from sanitized legacy `TimeSeconds`. Runtime prepare and Burst phase wrapping now share this helper.
Rejected Alternatives: One-time managed migration pass over Vault state; expanding the DTO with a migration flag; accepting a single visible phase snap after hot reload.
Scalability potential: Low, Middle, High, and Ultra retain identical DTO layout and octave scaling. The migration rule affects authority continuity only, not quality routing.
Hardware Impact: One branchless select in the runtime prepare path and one per active octave group; no extra buffer bandwidth, no allocation, no job dependency change.

Problem: The forensic audit existed as status/log/report fragments but not as the explicit XML artifact requested by the batch mandate.
Solution: Add `Docs/Reports/SHINOBU_263_SELF_AUDIT.xml` with task reconciliation, exact DTO offsets, continuous scalability curve, Vault BufferIDs, NoAlias/dependency graph, compile guard, and Dear Lie complexity.
Rejected Alternatives: Rely on chat output; append XML only to LOG; treat JSON scanner report as a substitute for the mandated self-audit block.
Scalability potential: Documentation-only. It does not affect Low, Middle, High, or Ultra runtime behavior; it preserves human and integrator proof of the continuous quality route.
Hardware Impact: 0 runtime microseconds. Prevents review churn by making offset and route proof machine-readable.

Problem: `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` was concurrently rewritten by SHINOBU_274 and no longer contained the SHINOBU_263 scanner section.
Solution: Re-add a compact `shinobu263WaveMathScanner` subsection pointing to the dedicated SHINOBU_263 report and self-audit while preserving the current SHINOBU_274 root and SHINOBU_268 nested data.
Rejected Alternatives: Overwrite the shared physics report with a SHINOBU_263-only root; ignore shared report drift and leave integrators with only the dedicated JSON.
Scalability potential: Documentation-only. It keeps cross-agent physics evidence discoverable without changing runtime routes or quality curves.
Hardware Impact: 0 runtime microseconds. Reduces integration search time and avoids report ownership churn.

Problem: The SHINOBU_263 UI Toolkit tuner lived under `Physics/Buoyancy/Editor`, which is governed by an existing editor asmdef. The analytical runtime DTOs live in the parent buoyancy runtime folder with no matching runtime asmdef, so the editor facade risked an assembly-boundary import failure.
Solution: Move the tuner to `Physics/Buoyancy/AnalyticalWaveTunerWindow.Editor.cs` with `#if UNITY_EDITOR` and namespace `Hecton8.Physics`, preserving editor-only behavior while keeping it in the same predefined runtime compilation surface as the analytical DTOs.
Rejected Alternatives: Create a new runtime asmdef around the whole dirty buoyancy folder; edit the existing editor asmdef to reference a non-existent runtime assembly; fix sibling editor windows outside SHINOBU_263 ownership.
Scalability potential: Editor-only. Runtime Low, Middle, High, and Ultra quality paths are unchanged; designer tuning remains available without C# recompilation of the solver math.
Hardware Impact: 0 player runtime microseconds. Reduces Unity import risk without moving or coupling hot-path jobs.
