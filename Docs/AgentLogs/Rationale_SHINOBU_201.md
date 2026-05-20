# SHINOBU_201 Rationale

Date: 2026-05-20
Status: PENDING VERIFICATION

## Initial Boundary

Problem: Batch prompt demands broad SIMD rewrite across Physics and AI while 20+ agents may be mutating adjacent systems.
Solution: Execute in bounded loops. First pass covers Tasks 01-05, starting with source archaeology and only editing owner-visible hot jobs. Use `[NoAlias]`, SoA/padded DTOs, and branchless math where source ownership is clear.
Rejected Alternatives: Blanket automated replacement across all `if`/`else` sites was rejected because cold guards, finite fallbacks, and bounds checks are safety paths, not SIMD debt. New global DataVault routes were rejected without route card and owner proof.
Scalability potential: Low uses auto-vectorized scalar-width Burst over dense lanes; Middle keeps same loop shape with more active samples; High adds wider visual lanes where supported; Ultra spends saved CPU on richer presentation-only turbulence/visibility data, not gameplay truth bloat.
Hardware Impact: On i3/MX350, expected gain is reduced alias checks and cache misses in 100k-element jobs. Numeric savings remain PENDING VERIFICATION until Burst Inspector/player benchmark proof.

Problem: SIMD task touches AUP spatial data, but 64-bit/double lanes halve throughput and can break deterministic authority if mixed into inner loops.
Solution: Keep AUP localization as a separate pass, outputting finite `float3`/padded float lanes for SIMD kernels. Gameplay authority remains AUP; float lanes are workspace/presentation/vector math input.
Rejected Alternatives: Direct double3 distance math inside all vector kernels was rejected for throughput and cache pressure. Reconstructing authority from Transform/world float was rejected by AUP law.
Scalability potential: Low localizes less often with coarser probe cadence; Middle/High/Ultra increase probe/sample density while keeping the same SoA lane contract.
Hardware Impact: On i3/MX350, isolating double math prevents hot-loop SIMD width collapse. Estimated benefit is 10-60 us per 100k spatial samples; PENDING VERIFICATION.

## Loop 1 Decisions: Tasks 01-05

Problem: Existing tether Verlet jobs expose multiple `NativeArray<T>` fields to Burst without explicit alias contracts, forcing conservative memory dependency assumptions in integration, solve, spline-copy, origin-shift, and telemetry passes.
Solution: Added `[NoAlias]` to owned mutable lanes and paired `[ReadOnly]`/`[WriteOnly]` where source ownership proves direction. Kept safety branches for pinned/fault recovery paths, but converted finite sanitization, velocity clamp, rest-length sanitation, telemetry sanitation, and simple scalar gates to `math.select`/`math.step`.
Rejected Alternatives: Blanket `[WriteOnly]` on arrays that are read for length or previous contents was rejected because it lies to Burst and can mask dependency bugs. Full branch deletion in fault recovery was rejected because NaN repair and pin authority are deterministic safety exits, not steady-state SIMD debt.
Scalability potential: Low keeps deterministic Verlet with fewer alias barriers; Middle increases tether node count; High and Ultra spend recovered cycles on denser visual spline or collision sampling while solver authority remains bounded.
Hardware Impact: On i3/MX350, expected gain is 8-35 us per 100k node operations from lower alias pressure and branchless clamps. Exact Burst Inspector delta remains PENDING COMPILE.

Problem: Hydrodynamic body state is an AoS DTO suited for authority and telemetry, not SIMD lane traversal.
Solution: Added `SimdFloat3Padded`, `SimdHydrodynamicTuningDTO`, SoA conversion jobs, and a branchless `VectorizedHydrodynamicsJob` over padded local positions, velocity lanes, drag coefficients, and output-force lanes. All SIMD workspace buffers are GlobalDataVault-owned with stable `BufferID` entries.
Rejected Alternatives: Replacing the authority DTO with SoA was rejected because it would break existing black-box telemetry, editor tuning, and physics apply contracts. Allocating temporary arrays per benchmark was rejected by Zero-GC policy.
Scalability potential: Low runs the same lane contract with fewer active samples; Middle/High/Ultra raise active SIMD lane count and turbulence sampling while preserving `GlobalQualityWeight` as a continuous scalar.
Hardware Impact: On i3/MX350, expected cache saving is 25-70 us per 100k hydrodynamic samples versus wide DTO walking. The 250k benchmark ring records measured vector microseconds after Unity compile/play verification.

Problem: SIMD task requires emergency proof harness and ARM64 alignment evidence, but runtime test code must not allocate or poll global state inside hot jobs.
Solution: Added a 250000-lane deterministic Burst benchmark using Vault-owned buffers, a 300-frame SIMD telemetry ring, and an Editor-only Burst Vectorization X-Ray window for layout size/alignment and benchmark telemetry. Runtime jobs receive resolved `GlobalQualityWeight` once from Homeostasis.
Rejected Alternatives: Managed `Random`, managed arrays, LINQ, string-split CSV parsing, and standalone fake benchmark numbers were rejected. Editing generated csproj files was rejected because Unity regenerates them and the compile authority is the Unity assembly graph.
Scalability potential: Low devices use benchmark telemetry to cap active lanes; Middle holds 250k lane stress as regression guard; High/Ultra use the same telemetry to justify visual overkill lanes when frame time budget survives.
Hardware Impact: On i3/MX350, benchmark harness costs only when manually invoked from editor/runtime tool. Hot runtime allocation remains persistent Vault memory; no per-frame GC introduced. Exact measured us saved remains PENDING COMPILE/BURST INSPECTOR.

## Verification Interlock

Problem: The local protocol forbids `dotnet build` while CPU load exceeds 50% or while compiler processes are active.
Solution: Sampled CPU before compile. The machine reported 100% processor time, so no build was launched. Work continues as static implementation with compile status explicitly pending.
Rejected Alternatives: Ignoring the build guard was rejected because it can collide with other agents and poison compile results. Reporting success without compile was rejected.
Scalability potential: Not runtime-facing; protects parallel agent throughput on shared hardware.
Hardware Impact: Avoided launching another compiler under saturated CPU. No frame-time claim is made from this.

## Loop 2 Decisions: Tasks 06-10

Problem: Hydrodynamic SIMD requires a measurable vector path and a scalar comparison path without contaminating runtime frames.
Solution: Kept `VectorizedHydrodynamicsJob` as the Burst `IJobParallelFor` over SoA lanes and added a non-Burst `ScalarHydrodynamicsReferenceJob` behind a Vault-backed `ScalarFallbackWeight01` tuning field. The benchmark regenerates the deterministic 250k lanes before measuring vector work, so scalar probing does not mutate the SIMD input baseline.
Rejected Alternatives: Always running scalar reference work was rejected as frame-budget sabotage. Fake scalar microsecond numbers were rejected. Manual AVX/NEON intrinsics were deferred because no Burst Inspector evidence yet proves auto-vectorizer failure.
Scalability potential: Low leaves scalar probe at 0 and only records vector us; Middle uses occasional scalar probe for regression; High/Ultra can compare scalar/vector deltas while increasing benchmark lane count or visual turbulence externally.
Hardware Impact: On i3/MX350, scalar probe is editor-controlled and not automatic. SIMD path remains expected 25-70 us per 100k hydrodynamic samples pending verification.

Problem: Spatial query and culling tasks require branchless distance and frustum math, but direct integration with predator/culling owners would create cross-agent dependencies.
Solution: Added standalone `VectorizedSpatialQueryJob` and `VectorizedFrustumCullJob` over padded `SimdFloat3Padded` lanes, valid masks, and packed `float4` frustum planes. These are stateless kernels that can be scheduled by owner systems later without introducing direct runtime dependencies.
Rejected Alternatives: Editing predator acquisition or shadow culling ownership code was rejected because those systems have active adjacent agents and domain-specific state machines. Local BufferID casts in graphics culling were rejected by global authority boundaries.
Scalability potential: Low uses fewer candidate lanes and cheap masks; Middle/High/Ultra increase candidate counts and visual culling breadth while preserving same branchless lane contract.
Hardware Impact: On i3/MX350, expected gain is removal of scalar distance/frustum checks when owners adopt the kernels. Exact us remains PENDING INTEGRATION/BURST INSPECTOR.

Problem: Transcendental functions block predictable vectorization when used directly in hot procedural wave/current math.
Solution: Added `SimdTranscendentalApproximator` with low-degree polynomial `SinPolynomial`, `CosPolynomial`, and `ExpNegPolynomial01`, and used it inside the new hydrodynamic kernel turbulence term.
Rejected Alternatives: Replacing all project-wide `math.sin/cos/exp` occurrences was rejected because many are cold authoring/mock or owned by other domains. Direct lookup textures were rejected for this pass because the task targets Burst arithmetic auto-vectorization, not memory fetch substitution.
Scalability potential: Low multiplies high-frequency turbulence by near-zero `GlobalQualityWeight`; Middle/High/Ultra raise the same continuous weight without switching algorithms.
Hardware Impact: On i3/MX350, expected gain is avoiding scalar libm-style transcendentals in 250k-lane benchmarks. Error bounds are recorded via tolerance DTO but still require CSV ingest and verification.

## Loop 3 Decisions: Tasks 11-16

Problem: Atomic accumulation and lock-based deltas break SIMD scheduling because vector lanes serialize on shared writes.
Solution: Added `LocalResourceDeltaJob` plus `ReduceResourceDeltaJob`, separating embarrassingly parallel local math from a constrained reduction pass. This preserves a pure SIMD-friendly primary pass and confines scalar summation to the minimum surface.
Rejected Alternatives: `Interlocked.Add`, `NativeQueue` per-lane accumulation, and direct global resource writes inside the vector loop were rejected because they serialize or allocate/contend under load.
Scalability potential: Low processes fewer local deltas; Middle/High/Ultra increase resource lane counts while reduction remains a single bounded pass.
Hardware Impact: On i3/MX350, expected benefit is avoiding atomic stalls in resource-style kernels. Exact us pending owner adoption.

Problem: AUP `double3` coordinates are required for authority, but doubles collapse SIMD width for heavy spatial math.
Solution: Added `VectorizedAupLocalizationJob` to subtract origin once and write finite padded local float lanes for downstream SIMD kernels. The heavy 64-bit step is quarantined before physics/culling/spatial math.
Rejected Alternatives: Doing double distance/frustum math in every kernel was rejected. Casting world `Transform` floats back into authority was rejected by AUP determinism.
Scalability potential: Low localizes smaller active windows; Middle/High/Ultra localize larger candidate windows while preserving the same float lane contract.
Hardware Impact: On i3/MX350, expected benefit is 10-60 us per 100k spatial candidates by preventing double-lane throughput loss.

Problem: SIMD telemetry must detect real regressions and preserve the last 300 frames for autopsy, not report vector wins as drops.
Solution: Corrected `ThroughputDrop01` to measure vector regression versus scalar reference and added raw `ReadOnlySpan<byte>` dump to `Docs/AgentLogs/Dump_SHINOBU_201.bin` when drop exceeds 50% or vector time is non-finite.
Rejected Alternatives: Logging text rows, managed serialization, and fake scalar baselines were rejected. Always running scalar reference was rejected; it is controlled by Vault `ScalarFallbackWeight01`.
Scalability potential: Low disables scalar probe during normal play; Middle samples periodically; High/Ultra can run scalar probes during dedicated profiling while using SIMD saved cycles for presentation overkill.
Hardware Impact: On i3/MX350, dump path only triggers under explicit regression. Normal benchmark telemetry is fixed-size Vault memory and zero per-frame GC.

Problem: Burst synchronous compilation and zero-init bypass are mandatory, but compile verification is blocked by current CPU saturation.
Solution: All new Burst jobs use `CompileSynchronously = true`. Large SoA workspace buffers use `NativeArrayOptions.UninitializedMemory`; only single-value cursors/tuning use clear memory because they must hold deterministic control state.
Rejected Alternatives: Clearing 250k lanes with `MemClear` was rejected because every active lane is overwritten by generation/localization jobs. Asynchronous Burst was rejected for first-frame scalar fallback risk.
Scalability potential: Low writes fewer active lanes; Middle/High/Ultra write more lanes without paying OS zero-fill tax.
Hardware Impact: On i3/MX350, avoids roughly 10+ MB cold zero-fill for SIMD benchmark buffers. Exact boot delta pending measurement.

## Loop 4 Decisions: Tasks 17-20

Problem: Lead programmers need a direct view of SIMD throughput and scalar fallback comparison without recompiling or adding runtime dependencies.
Solution: Added `BurstVectorizationXRayWindow` using UI Toolkit. It reads the SIMD telemetry/tuning Vault buffers, exposes a continuous scalar-probe slider, runs the 250k benchmark, shows vector/scalar microsecond bars, entities/ms, regression drop, flags, and layout audit.
Rejected Alternatives: IMGUI polling with per-frame string formatting was rejected. A binary scalar toggle was rejected; `ScalarFallbackWeight01` remains a continuous 0..1 control.
Scalability potential: Low keeps scalar probe at zero; Middle uses sample probes; High/Ultra can stress vector lanes and justify spending saved budget on visual turbulence/culling breadth.
Hardware Impact: Editor-only facade. No runtime frame cost unless benchmark is manually invoked.

Problem: Polynomial approximation tolerances need cold-boot ingest without `string.Split`, `int.Parse`, or managed byte arrays.
Solution: Added `simd_math_tolerances.csv`, `TryLoadSimdMathTolerancesCsv`, and an allocation-free span parser writing directly into `NativeArray<SimdMathToleranceDTO>`.
Rejected Alternatives: Managed CSV libraries, `File.ReadAllBytes`, and string tokenization were rejected. Baking tolerances into code only was rejected because designers need cold data control.
Scalability potential: Low can accept lower polynomial degrees/error budgets; Middle/High/Ultra can raise precision continuously by CSV tuning while keeping kernel shape stable.
Hardware Impact: Cold-boot/editor-only ingest; zero hot-path frame cost. On i3/MX350 it avoids managed allocation spikes during tuning reload.

Problem: ARM64 pointer alignment problems are invisible until hardware faults or Burst de-optimizes.
Solution: Extended `OnDrawGizmos` to draw four Scene View alignment bars for SIMD positions, velocities, output forces, and drag coefficients. It checks pointer 16-byte alignment and stride vector-safety with `UnsafeUtility.SizeOf`; red bars indicate unsafe layout.
Rejected Alternatives: Dynamic Scene View text labels were rejected for allocation/noise. GlobalDataVault metadata edits were rejected because SHINOBU_202 owns pointer/generation internals.
Scalability potential: Low/Middle/High/Ultra share identical alignment contract. Higher tiers simply keep more lanes active.
Hardware Impact: Editor-only gizmo, no player cost. Prevents ARM64 unaligned memory faults before device deployment.

Problem: Completion requires self-audit evidence, not chat-only claims.
Solution: Status/rationale/log files are treated as durable state. Final log will include `<SELF_AUDIT>` with byte layouts, BufferIDs, compile status, and GC surface.
Rejected Alternatives: Chat-only report and unverified "done" labels were rejected.
Scalability potential: Audit documents Low/Middle/High/Ultra behavior for later owners.
Hardware Impact: Documentation only; no runtime cost.

## Loop 6 Decisions: Ultra-Think Polish Pass

Problem: The SHINOBU_201 SIMD kernel was correct for the current benchmark wiring but too trusting as a reusable owner-facing kernel; `VectorizedHydrodynamicsJob` and its scalar probe counted velocity/drag/output lanes but read `LocalPositions[index]`.
Solution: The count guard now includes `LocalPositions.Length` before any position read. This keeps the stateless kernel valid when another owner schedules it with a shorter localized workspace.
Rejected Alternatives: Leaving the guard as-is was rejected because benchmark symmetry is not an API contract. Adding an `IsCreated` select was rejected because C# evaluates `LocalPositions[index]` before `math.select` and would not protect a short/default lane.
Scalability potential: Low/Middle/High/Ultra all keep the same SoA contract; active lane count can shrink continuously without out-of-bounds reads.
Hardware Impact: On i3/MX350 this is a correctness fence, not a measured speed claim. It avoids Burst safety bailout in editor and undefined release memory reads on ARM64.

Problem: The CSV tolerance ingest existed but was effectively dead data for the hydrodynamic polynomial path.
Solution: `SimdHydrodynamicTuningDTO` now stores approximation quality weight, maximum approximation error, and sine polynomial degree inside the same explicit 64-byte row. Cold CSV ingest mutates that unmanaged tuning row from `sin_polynomial` or `hydrodynamic_turbulence` rows.
Rejected Alternatives: Passing the full tolerance table into the hot SIMD job was rejected because it adds per-lane lookup pressure. Managed dictionaries or string matching were rejected by zero-GC policy.
Scalability potential: Low collapses toward 3rd-degree sine approximation; Middle blends toward 5th-degree; High/Ultra blend toward 7th-degree. No binary quality switch was introduced.
Hardware Impact: On i3/MX350 this reduces polynomial work pressure when quality is low, pending Burst Inspector proof. On high-end hardware, saved branch/call overhead buys more turbulence detail through the same scalar.

Problem: `SinPolynomial` used a 7th-order Taylor over the wider wrapped range, which is not the correct error envelope for the authored 0.008 tolerance near +/-pi.
Solution: The polynomial now range-reduces to +/-pi/2 through branchless `math.select`, then blends 3rd/5th/7th-order results by continuous quality and authored degree.
Rejected Alternatives: Using `math.sin` was rejected because it can force scalar transcendental lowering. A texture/LUT lookup was rejected for this Burst arithmetic pass because it trades ALU for memory bandwidth and complicates determinism.
Scalability potential: Low uses cheaper visual fake turbulence; Middle/High/Ultra raise approximation fidelity smoothly. The Dear Lie remains deterministic and presentation-biased.
Hardware Impact: Expected low-quality ALU reduction remains pending measurement; the main hard gain is predictable NEON/AVX-friendly multiply-add structure.

Problem: New SHINOBU_201 DataVault IDs were present in source but absent from the binary payload integration ledger.
Solution: Added a SHINOBU_201 ledger section with BufferIDs, primary DTO byte layouts, runtime boundary, scalability boundary, dump path, and verification status.
Rejected Alternatives: Relying on status/log files alone was rejected because global authority requires durable owner/range/lifetime documentation.
Scalability potential: Documentation ensures future Low/Middle/High/Ultra tuning does not create duplicate buffer IDs or shadow ownership.
Hardware Impact: Documentation only; prevents integration churn rather than frame-time cost.

Problem: The tolerance table is allocated with `UninitializedMemory`; scanning all 64 rows after a short CSV load could treat stale slack as active formula rows.
Solution: The cold parser now clears the 64-row table before parsing, and the hydrodynamic tuning applier scans only `rowsWritten`.
Rejected Alternatives: Relying on `Flags` being zero in uninitialized Vault memory was rejected because that is exactly what `UninitializedMemory` does not guarantee. Clearing the table every frame was rejected; this is cold ingest only.
Scalability potential: Low/Middle/High/Ultra tuning rows remain deterministic and authored. No phantom high-tier precision row can leak from stale slack.
Hardware Impact: 64 DTO stores on CSV load only; zero gameplay-frame cost.

Problem: The editor alignment overlay could attempt to resolve default SIMD Vault handles if `OnDrawGizmos` ran during a partial cold boot or hot-swap window.
Solution: `DrawSimdAlignmentGizmos` now returns before any resolve unless all four SIMD handles are created.
Rejected Alternatives: Relying on `_dataVault != null` alone was rejected because handle creation and service availability are separate facts.
Scalability potential: Editor-only diagnostic remains stable across Low/Middle/High/Ultra tuning and does not create false red overlays during boot.
Hardware Impact: Editor-only branch, no player cost.

Problem: `SimdHydrodynamicTuningDTO` is clear-memory initialized, so a finite zero `ApproximationQualityWeight` could incorrectly force low-fidelity polynomial math on high-tier hardware before authored tuning exists.
Solution: Approximation quality now falls back to `GlobalQualityWeight` unless the authored field is finite and greater than epsilon.
Rejected Alternatives: Treating zero as an authored override was rejected because it collapses visual overkill by default. Adding a separate boolean flag was rejected to keep the DTO inside the existing 64-byte layout.
Scalability potential: Low can still author near-zero approximation weight deliberately; Middle/High/Ultra default to the same continuous global weight.
Hardware Impact: One cold/control-path boolean, no extra buffer or per-lane memory fetch.

Problem: Task 19 originally exposed alignment bars but omitted the requested stride/capacity warning text because dynamic Scene View strings would allocate.
Solution: Added fixed editor-only `GUIContent` labels for the four SIMD Vault lanes and a red fault overlay driven by deterministic `math.step` flash cadence. The labels are static editor objects; player runtime does not see them.
Rejected Alternatives: Per-frame `string.Format`, interpolated labels, or querying Vault metadata into managed strings were rejected. Removing labels was rejected after the polish mandate explicitly required text.
Scalability potential: Low/Middle/High/Ultra share one SIMD memory contract. The overlay prevents a low-tier ARM64 deployment from accepting a buffer that a high-end desktop would silently tolerate.
Hardware Impact: Player frame cost is 0. Editor-only diagnostic prevents NEON unaligned access and Burst de-vectorization before device testing.

Problem: The latest compile gate retry still found the shared machine saturated.
Solution: Sampled CPU and compiler processes; CPU was 100%, so `dotnet build` was not launched.
Rejected Alternatives: Launching a compiler under 100% CPU was rejected by local build-protection law. Reporting compile success without running it was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects developer iteration hardware; no runtime microsecond saving is claimed.

## Loop 7 Decisions: Branchless Control Polish

[ANALYSIS]
Target: SHINOBU_201 SIMD vectorization lane only.
Affected systems: `BuoyancySimdVectorization.cs`, `BuoyancyDisplacementRuntime.cs`, and the existing X-Ray editor facade read path.
Zero GC proof: modified hot/Burst paths still operate only on pre-existing Vault `NativeArray` lanes and unmanaged DTO values; no managed collection, LINQ, string split, managed byte array, or local persistent native allocation was introduced.
State check: `Status_SHINOBU_201.md`, `Rationale_SHINOBU_201.md`, `CURRENT_BATCH.md` SHINOBU XML, `AGENTS.md`, selected `.agents-skills`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, domain map, and binary payload ledger were re-read before edits.
Rule quote: "GlobalRegistry correct use = cold bootstrap, stable service discovery, dependency injection cache"; Burst jobs continue to receive resolved Vault arrays and never poll `GlobalRegistry`.

Problem: The scalar benchmark comparison still behaved as an on/off probe, even though the editor slider was continuous.
Solution: `GenerateMockSimdBenchmark()` now maps `ScalarFallbackWeight01` to `scalarProbeCount = round(count * weight)` and runs the scalar reference over that count, then normalizes measured microseconds back to full-count comparison for telemetry.
Rejected Alternatives: Always running scalar reference over all 250000 lanes was rejected because it serializes the manual benchmark at every probe. A binary `weight > epsilon` semantic was rejected because it violates continuous scalability intent.
Scalability potential: Low keeps scalar count near zero; Middle samples a bounded slice; High/Ultra can raise the probe to full-count validation without recompilation.
Hardware Impact: Editor/manual benchmark path only. It prevents low-tier editor profiling from paying full scalar cost unless explicitly requested; no player hot-path microsecond claim.

Problem: `VectorizedFrustumCullJob` computed `planeCount` through `Planes.IsCreated ? Planes.Length : 0` inside the Burst Execute path.
Solution: Added explicit `PlaneCount` input and clamp it with `math.min/math.max` against `Planes.Length` and the six-plane culling contract.
Rejected Alternatives: Keeping the property ternary was rejected because Task 03 targets branch-shaped metadata in job inner loops. Trusting every caller to pass six planes without clamping was rejected because owner adoption is future work.
Scalability potential: Low/Middle/High/Ultra all keep the same branchless culling loop shape; owners can reduce `PlaneCount` continuously for coarse debug/camera proxies if needed.
Hardware Impact: Expected impact is small but removes a branch-shaped hot-loop metadata check. Exact AVX/NEON proof remains pending Burst Inspector.

Problem: Several runtime control helpers still used ternary/early-branch logic where arithmetic selection was equivalent and safer for future inlining.
Solution: Replaced scheduled-count early return with saturating numerator math, replaced `ResolveGlobalQualityWeight` ternary with `math.select`, replaced default sine polynomial degree ternaries with `math.select`, and converted throughput-drop validity to a selected value.
Rejected Alternatives: Removing null/file/handle guards was rejected. Those guards protect cold IO, editor UI, teardown, and DataVault resolution, not SIMD ALU lanes.
Scalability potential: Continuous `GlobalQualityWeight` remains the sole quality scalar; no hardware-tier switch was added.
Hardware Impact: Control-path cleanup only; no measured us claim.

Problem: CSV tolerance application used two `continue` branches inside its row scan.
Solution: Converted active-row and formula-match checks to one `applyRow` boolean and updated degree/error via `math.select`.
Rejected Alternatives: Managed dictionaries or pushing the tolerance table into every SIMD lane were rejected. CSV remains cold ingest, while the job receives a compact 64-byte tuning DTO.
Scalability potential: Low/Middle/High/Ultra polynomial degree and error tolerance remain authored by cold data, then consumed as branchless tuning scalars.
Hardware Impact: Cold CSV path only. Gameplay frame cost unchanged.

Problem: Build verification is still blocked by local system load.
Solution: Sampled CPU after Loop 7 edits: 100%. `Get-Process dotnet,csc` returned no active process output, but the CPU guard alone blocks build launch.
Rejected Alternatives: Launching `dotnet build` under 100% CPU or claiming compile success from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared developer hardware; no runtime saving claimed.

## Loop 11 Decisions: Buoyancy Job Ingress Vaccination

[ANALYSIS]
Target: `EvaluateBuoyancyJob` and producer-only buffer annotations in `BuoyancyDisplacementJobs.cs`.
Affected systems: buoyancy deterministic physics job, cold buffer init, mock state generation, telemetry reduction.
Zero GC proof: edits stayed inside Burst job structs and unmanaged DTO writes. No managed allocation, local persistent NativeArray, LINQ, string parsing, or file IO was introduced.
State check: source-only forbidden scan, brace/preprocessor count, Burst attribute scan, and `git diff --check` were rerun after edits. Build stayed blocked because CPU gate could not prove a safe load.
Rule quote: every division/normalization denominator is already guarded; this pass moves finite gates to the ingress so invalid values do not reach force math before the final finite check.

Problem: `EvaluateBuoyancyJob` loaded authority state through `UnsafeUtility.AsRef` and only rejected non-finite values after several force calculations had already executed.
Solution: The job now finite-gates `CurrentAUP`, `Velocity`, `MassKg`, and `VolumeCubicMeters` immediately after the state load. It marks non-finite input with `FlagNonFinite`; Loop 13 supersedes the earlier invalid-state fold by preserving `EntityHashID` and disabling physics/queue output through `simulateBody`.
Rejected Alternatives: Relying on the final force finite check was rejected because NaN can already poison intermediate force math, debug rows, and rollback snapshots. Trusting upstream owner DTOs was rejected at this domain boundary. Zeroing `EntityHashID` was rejected because it destroys forensic identity.
Scalability potential: Low/Middle/High/Ultra all share the same deterministic state sanitation; quality only changes active cadence and approximation fidelity.
Hardware Impact: Adds select guards at state ingress. Cost is lower than a NaN cascade or rollback forensic dump. Exact microseconds remain pending Burst Inspector.

Problem: Tuning values were seeded cold but still entered hot force math raw if a corrupt or partially initialized tuning row survived.
Solution: Surface AUP, sector AUP, drag coefficients, density limits, surface dampening, sleep thresholds, density-depth coefficient, seafloor Y, flow coefficient, and snap depth are now finite-gated into locals before force math.
Rejected Alternatives: Re-seeding the tuning row inside the job was rejected because jobs should not own cold authoring defaults or mutate tuning authority. Letting non-finite tuning reach math and relying on final force rejection was rejected.
Scalability potential: `GlobalQualityWeight` remains continuous; sanitized scalar locals do not introduce hardware-tier branches.
Hardware Impact: Correctness fence. Prevents a single corrupt tuning row from turning the physics pass into a NaN generator.

Problem: Several producer-only NativeArray fields were annotated as generic read/write lanes even though the jobs never read element contents.
Solution: Added `[WriteOnly, NoAlias]` to mock debug output, cold buffer init outputs, buoyancy debug output, force packet output, and telemetry ring output.
Rejected Alternatives: Blanket `[WriteOnly]` on counters/state lanes was rejected because those lanes are read or atomically updated. Removing safety guards was rejected because `IsCreated`/`Length` checks are structural.
Scalability potential: All tiers benefit from clearer alias contracts; no quality logic changed.
Hardware Impact: Gives Burst stronger non-alias/output-only proof for producer buffers. Exact vectorization gain remains pending Burst Inspector.

Problem: Compile verification remains blocked.
Solution: `Get-CimInstance` CPU probe timed out under load, `wmic` is unavailable, and compiler process query returned no process output. Build was not launched because the gate could not prove CPU <= 50%.
Rejected Alternatives: Treating a timed-out CPU probe as safe was rejected. Launching build anyway was rejected by explicit user warning.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared machine from compiler contention; no runtime saving claimed.

## Loop 8 Decisions: NaN/Rsqrt/Determinism Polish

[ANALYSIS]
Target: SHINOBU_201 SIMD and buoyancy hot math surface.
Affected systems: `BuoyancySimdVectorization.cs`, `BuoyancyDisplacementJobs.cs`, status/rationale/log evidence.
Zero GC proof: edits stayed inside Burst job structs and static math helpers; no managed allocations, managed containers, file IO, or local persistent native allocations were introduced.
State check: Status and rationale were re-read before this response and before edits. The exact `CURRENT_BATCH.md` SHINOBU_201 XML, AGENTS.md, selected mandate files, ledger, and global authority boundary were reloaded during this mandate pass.
Rule quote: default inverse-length form is `math.rsqrt(math.max(dot(v, v), EPSILON))`; authoritative state integrations use deterministic float mode.

Problem: `ScalarHydrodynamicsReferenceJob` was still a mathematical `IJob` without an explicit Burst directive, contradicting the current mandate even though it was editor/manual benchmark work.
Solution: Added `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
Rejected Alternatives: Keeping it non-Burst to preserve a managed-C# scalar comparison was rejected because the mandate requires every mathematical job to carry explicit Burst flags. Moving scalar comparison to a managed loop was rejected because it would add a worse hot-pattern example and distort the X-Ray facade.
Scalability potential: Low can still run zero or tiny scalar probe slices through `ScalarFallbackWeight01`; Middle/High/Ultra can raise probe coverage without changing compilation behavior.
Hardware Impact: Exact delta pending Burst Inspector. The concrete gain is removal of first-run scalar fallback risk for a job-shaped benchmark path.

Problem: `VectorizedSpatialQueryJob`, `LocalResourceDeltaJob`, and `ReduceResourceDeltaJob` used `FloatMode.Fast` even though AI query masks and resource deltas can feed authoritative decisions when adopted by owners.
Solution: Changed those three jobs to `FloatMode.Deterministic`; after Loop 9, black-box telemetry is also deterministic and only presentation frustum/compact jobs remain Fast.
Rejected Alternatives: Treating all helper kernels as visual-only was rejected because the original prompt explicitly names predator spatial acquisition and resource accumulation. Converting visual frustum work to deterministic was rejected because it is a Dear Lie render mask, not rollback truth.
Scalability potential: Low/Middle/High/Ultra keep identical deterministic lane contracts for AI/resource adoption; visual culling remains free to spend Fast-mode ALU on presentation overkill.
Hardware Impact: Determinism may cost some compiler freedom. That cost is accepted for authority-facing work; exact us pending benchmark.

Problem: Hydrodynamic SIMD math sanitized the final write but allowed NaN tuning inputs to enter drag, turbulence, buoyancy, and max-speed calculations first.
Solution: Raw position, velocity, drag coefficient, base drag, turbulence amplitude, buoyancy, and max speed are finite-gated before integration. Default zero approximation weight is ignored unless greater than epsilon, so clear-memory tuning cannot silently collapse high-tier math.
Rejected Alternatives: Relying on `SimdFloat3Padded.FromFloat3` after integration was rejected because NaN ALU can poison output-force deltas, masks, and telemetry before the final store. Throwing exceptions or logging strings was rejected.
Scalability potential: Low still collapses turbulence continuously through `GlobalQualityWeight`; Middle/High/Ultra keep visual overkill contribution without NaN fault amplification.
Hardware Impact: Adds a few scalar/vector select guards per lane. Expected cost is lower than a single NaN cascade or safety bailout; exact proof pending.

Problem: Spatial and frustum masks could accept non-finite predator/prey/radius/plane data and emit unstable visibility or target masks.
Solution: `VectorizedSpatialQueryJob` now finite-gates prey, predator, and radius. `VectorizedFrustumCullJob` zeros visibility contribution for non-finite planes.
Rejected Alternatives: Trusting owner callers was rejected because these kernels are designed as reusable stateless adoption surfaces. Adding `GlobalRegistry` or owner callbacks in the job was rejected by authority law.
Scalability potential: All tiers can shrink or expand candidate counts without accepting NaN masks.
Hardware Impact: Correctness fence only; exact SIMD impact pending Burst Inspector.

Problem: The broader buoyancy hot path still used `math.sqrt` in height estimate, quality-weighted speed, and telemetry length reduction.
Solution: Replaced those sites with guarded `rsqrt` forms and removed the `qualityCurve <= epsilon` branch from `FastSpeed`; quality now blends cheap dominant-axis speed with the rsqrt speed continuously.
Rejected Alternatives: Keeping `sqrt` for readability was rejected by the i3/NEON mandate. Quake-style bit hacks were rejected by mandate. Lookup tables were rejected for this deterministic physics path.
Scalability potential: Low gets dominant-axis speed approximation; Middle blends toward rsqrt speed; High/Ultra can spend saved ALU on denser visual turbulence or debug overlays while gameplay authority remains bounded.
Hardware Impact: Expected gain is fewer scalar square-root lowers on i3/MX350 and ARM64. Measured microseconds remain pending.

Problem: Build verification remains blocked.
Solution: Sampled CPU after edits: 100%. No active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` processes were reported, but CPU guard alone blocks build launch.
Rejected Alternatives: Ignoring the guard or claiming compile from static checks was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention; no runtime saving claimed.

## Loop 9 Decisions: Pascal Audit Closure

[ANALYSIS]
Target: remaining SHINOBU_201 audit findings from the independent Pascal pass.
Affected systems: `BuoyancySimdVectorization.cs`, `BuoyancyDisplacementRuntime.cs`, status/rationale/log evidence.
Zero GC proof: edits stayed inside Burst job structs, unmanaged DTO writes, fixed handle checks, and `#if UNITY_EDITOR` bridge code. No managed collection, LINQ, string split, managed byte array, or persistent local NativeArray was added.
State check: Status and rationale were re-read before edits. Static scans were rerun after edits. Build stayed blocked by CPU gate.
Rule quote: hot frame code must not query global service locators or request persistent memory; boot owns handle acquisition and jobs consume resolved unmanaged lanes.

Problem: Hydrodynamic SoA ingress trusted owner DTO fields enough to let non-finite mass, volume, base drag, or velocity feed drag lanes and SIMD velocity lanes.
Solution: `HydrodynamicStateToSoAJob` now finite-gates mass, volume, base drag, source velocity, and the final drag lane before writes.
Rejected Alternatives: Relying on upstream owner sanitation was rejected because this job is a reusable boundary between authority DTOs and vector workspaces. Throwing exceptions or logging strings was rejected because jobs must remain Burst-safe.
Scalability potential: Low/Middle/High/Ultra all share the same safe SoA lane contract; quality only changes active counts and approximation cost, not memory validity.
Hardware Impact: Adds select guards but prevents NaN/Inf from invalidating rollback snapshots or SIMD lanes. Exact microseconds remain pending Burst Inspector.

Problem: Hydrodynamic SoA egress could preserve a non-finite inactive state velocity or write a non-finite SIMD velocity back into authority state.
Solution: `HydrodynamicSoAToStateJob` now sanitizes existing and SIMD velocities before selection, so inactive rows are also scrubbed to a finite value.
Rejected Alternatives: Preserving inactive NaN state was rejected because the Network Surgeon snapshots raw DTO bytes and cannot reconstruct intent during rollback.
Scalability potential: Same authority DTO layout on all tiers; no quality branch introduced.
Hardware Impact: Correctness fence. Prevents forensic ring and state snapshots from carrying poison values.

Problem: AUP localization sanitized after float conversion but not before double subtraction, so non-finite AUP/origin inputs could infect local lanes.
Solution: `VectorizedAupLocalizationJob` finite-gates absolute AUP and origin AUP in double precision before subtraction and local cast.
Rejected Alternatives: Relying on final `SimdFloat3Padded.FromFloat3` was rejected because NaN double math should not be allowed to execute unchecked at the authority boundary.
Scalability potential: Low localizes fewer active lanes; higher tiers localize more lanes, but all use the same AUP-first precision path.
Hardware Impact: Correctness fence with minimal select cost; exact us pending.

Problem: Resource delta products and reduction sums could overflow to `Infinity` even when individual inputs were finite.
Solution: `LocalResourceDeltaJob` finite-gates the product before writing. `ReduceResourceDeltaJob` finite-gates every additive step and final output, and `Output` is now `[WriteOnly, NoAlias]`.
Rejected Alternatives: Atomics, queues, or unchecked summation were rejected because they either serialize work or allow poison values into owner state.
Scalability potential: Low runs smaller local-delta windows; Middle/High/Ultra increase lane count without changing the deterministic reduction shape.
Hardware Impact: Prevents overflow state poison. SIMD gain remains pending owner adoption and profiler proof.

Problem: Black-box SIMD telemetry was still Fast-mode and allowed non-finite timing values to reach derived throughput/drop math before writing the 64-byte ring row.
Solution: `RecordSimdTelemetryJob` now uses deterministic Burst mode, writes the telemetry ring through `[WriteOnly, NoAlias]`, sanitizes vector/scalar micros, throughput, drop, and flags non-finite inputs.
Rejected Alternatives: Treating telemetry as presentation-only Fast math was rejected because crash forensics must be deterministic and byte-stable. Text logging was rejected.
Scalability potential: Low can record sparse benchmark/autopsy rows; higher tiers can increase SIMD sample pressure while preserving the same 300-row black box.
Hardware Impact: Deterministic telemetry cost is accepted; the value is postmortem proof, not a speed claim.

Problem: `FixedTick` still called `EnsureVaultBuffers()`, which can perform DataVault handle acquisition in the hot frame path.
Solution: Added `HandlesReady()` and changed `FixedTick` to verify pre-acquired handles only. Non-finite tick deltas are rejected, and the stored/scheduled tick delta is clamped.
Rejected Alternatives: Re-requesting handles every frame was rejected because boot owns persistent memory acquisition. Moving all editor/manual paths to `HandlesReady()` was rejected because those cold paths are allowed to recover after domain reload.
Scalability potential: All tiers avoid hot service-location work; quality changes remain continuous through existing tuning.
Hardware Impact: Removes hot handle-request pressure. Exact microseconds pending profiler because build/play verification is still blocked.

Problem: The active runtime bridge existed for editor tooling but was player-visible.
Solution: `_activeRuntimeInstance`, `TryGetActiveRuntimeInstance`, and assignment/clear sites are wrapped in `#if UNITY_EDITOR`.
Rejected Alternatives: Leaving a public player bridge for editor convenience was rejected. Wrapping editor view methods was deferred because the explicit finding targeted the active-instance bridge and those methods may be used by guarded tooling during editor play.
Scalability potential: Not quality-facing.
Hardware Impact: Player runtime surface is smaller; no frame-time saving claimed.

Problem: Build verification is still blocked.
Solution: Sampled CPU after Loop 9 edits: 99%. `Get-Process` returned no active compiler output, but CPU guard alone blocks build launch.
Rejected Alternatives: Launching `dotnet build` under 99% CPU or claiming compile success from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared developer hardware; no runtime saving claimed.

## Loop 10 Decisions: Buoyancy Branchless Hot-Loop Polish

[ANALYSIS]
Target: SHINOBU_201 SIMD branch eradication on buoyancy physics hot jobs.
Affected systems: `GenerateMockBuoyantObjectsJob`, `EvaluateBuoyancyJob`, `ResolveFlowVelocity`, and `ReduceBuoyancyTelemetryJob` in `Assets/_Project/Scripts/Physics/Buoyancy/BuoyancyDisplacementJobs.cs`.
Zero GC proof: edits stayed inside Burst job structs over Vault-owned `NativeArray` fields; no managed collections, class allocations, string formatting, coroutine, LINQ, or file IO were introduced.
State check: `Status_SHINOBU_201.md`, this rationale file, the full `CURRENT_BATCH.md` SHINOBU_201 XML block, `AGENTS.md`, `Actual Domains of Project.txt`, and six mandate files were re-read before edits.
Rule quote: branchless SIMD math uses `math.select`, `math.step`, squared comparisons, `math.rsqrt`, and flat native lanes; structural guards remain only when they protect invalid NativeArray access or side effects.
First-20-minutes route blocker removed: buoyant salvage/debris interaction can use the same deterministic force-packet route with less branch divergence during early underwater traversal; no new scene, UI, or cross-domain route was created.

Problem: The deterministic mock generator still had an active-lane branch that split initialized and default state emission inside every `IJobParallelFor` lane.
Solution: Replaced the active branch with a boolean lane mask and `math.select` for `double3` AUP, velocity, mass, volume, entity hash, and flags. Inactive lanes still deterministically write zero/default DTO data.
Rejected Alternatives: Leaving the branch was rejected because the emergency benchmark exists to stress vectorized lane shape. Skipping inactive writes was rejected because uninitialized Vault memory must be overwritten deterministically.
Scalability potential: Low can seed a smaller active window without stale lane state; Middle/High/Ultra can raise active mock count without changing loop topology.
Hardware Impact: Expected benefit is cleaner branch predictor/SIMD lowering in the seed pass. Exact microseconds are PENDING VERIFICATION until Burst Inspector and profiler proof.

Problem: `EvaluateBuoyancyJob.Execute` still used ternaries and nested branches in selection math: active-count fallback, strided index, tick delta, surface snap, quadratic drag, gravity-packet ownership, and seafloor sleep flag.
Solution: Converted those decisions to `math.select`, non-short-circuit boolean masks, and continuous blend math. The surface near/snap path now damps and snaps through masks, recomputes submerged fraction once, and uses `math.step` for the velocity snap threshold. Quadratic drag now computes the candidate every lane and blends by `GlobalQualityWeight`-derived scalar.
Rejected Alternatives: Removing invalid-entity exits, NativeArray bounds guards, non-created guards, and force-packet side-effect gates was rejected because C# evaluates array access before `math.select`; those branches are memory-safety and side-effect fences, not ALU divergence debt.
Scalability potential: Low uses cheap dominant-axis/low quadratic contribution and triangle-flow fake; Middle blends more exact speed and drag; High/Ultra spend the same lane shape on richer drag/current response without binary tier switches.
Hardware Impact: Expected gain is reduced unpredictable branch pressure in buoyancy integration on i3/MX350 and ARM64 NEON. Exact savings remain PENDING VERIFICATION.

Problem: `ResolveFlowVelocity` returned early from active flow samples and nested a radius branch around flow-field sampling.
Solution: Converted sample active/radius/finite checks into a `sampleMask`, sanitized the sampled velocity, computed the deterministic triangle-wave analytic fallback every call, and selected sampled versus analytic flow at the end.
Rejected Alternatives: Keeping early return was rejected because it creates mixed control flow inside a per-body hot helper. Full flow-field texture or Navier-Stokes sampling was rejected by Dear Lie law; the analytic triangle flow is the cheap visual/physics proxy.
Scalability potential: Low uses the analytic flow fake almost entirely; Middle/High/Ultra can author more valid flow samples while retaining one branchless blend point.
Hardware Impact: Removes a data-dependent early-return path; exact throughput delta pending Burst Inspector.

Problem: `ReduceBuoyancyTelemetryJob` used `continue` and multiple `if` increments, causing branchy post-simulation reduction of black-box data.
Solution: Replaced alive/frame/sleep/evaluated/non-finite tests with integer masks and selected last-entity/last-net-force values. Structural buffer guards remain before accessing optional Vault buffers.
Rejected Alternatives: Deleting telemetry reduction or moving it to managed logging was rejected because black-box forensic state is mandatory. Atomics were rejected; this reduction is a single deterministic pass after producers complete.
Scalability potential: Low records the same 300-frame forensic ring with fewer active objects; Middle/High/Ultra keep telemetry shape stable while active count scales.
Hardware Impact: Small post-simulation branch reduction; exact profiler delta pending.

Problem: Build verification cannot be run under current host load and the user explicitly warned not to launch rebuild unless needed.
Solution: Sampled CPU and compiler processes after edits. CPU load was 100%; no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Running `dotnet build` under the 100% CPU guard or reporting a compile-proven state from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects the shared machine from compile contention; no runtime microsecond saving is claimed from this guard.

## Loop 12 Decisions: Atomic Force Packet Map-Reduce Polish

[ANALYSIS]
Target: `EvaluateBuoyancyJob` force-packet emission and `FixedTick` buoyancy job chain.
Affected systems: `BuoyancyDisplacementJobs.cs`, `BuoyancyDisplacementRuntime.cs`, force-packet counter semantics consumed by `PhysicsApplySystem`.
Zero GC proof: edits stay inside Burst job structs and existing caller-resolved Vault `NativeArray` views. No managed allocation, managed collection, LINQ, string parsing, local persistent native allocation, or runtime service lookup was added.
State check: status, rationale, full SHINOBU_201 XML block, AGENTS.md, actual domain map, and binary payload ledger were re-read. Static scans were rerun after edits. Build stayed blocked by CPU 100%.
Rule quote: atomic operations inside `IJobParallelFor` serialize vector lanes; heavy math must write owner-local candidates first, then reduce in a bounded secondary job.
First-20-minutes route blocker removed: buoyant salvage/debris can still publish deterministic force packets for early underwater traversal, but the packet count is no longer produced by a contended atomic in the parallel evaluator.

Problem: Force-packet emission in the buoyancy evaluator used an atomic append path, which forces memory serialization exactly where Burst should keep independent lanes vector-friendly.
Solution: `EvaluateBuoyancyJob` now treats `ForcePackets` as a candidate lane indexed by `workIndex`. Every scheduled lane clears its own slot before safety exits and writes at most one sanitized packet candidate without mutating shared counters. This makes the primary physics job a pure map over independent rows.
Rejected Alternatives: `Interlocked.Increment`, `NativeQueue`, and `ParallelWriter` were rejected because they keep contention in the heavy parallel phase. Sparse direct draining by `PhysicsApplySystem` was rejected because it would change the consumer contract and push compaction into a main-thread bridge.
Scalability potential: Low quality schedules fewer candidates through the existing continuous stride logic. Middle/High/Ultra can schedule more active buoyancy rows without turning the evaluator into an atomic bottleneck; saved CPU budget remains available for richer flow/drag presentation.
Hardware Impact: Removes atomic read-modify-write contention from the parallel hot job. On i3/MX350 and ARM64 NEON, expected gain is lower cache-line invalidation and stronger Burst alias/vectorization freedom. Exact microseconds remain PENDING VERIFICATION.

Problem: A dense force-packet prefix is still required by the existing apply bridge, and changing that bridge would cross into a main-thread Rigidbody application surface.
Solution: Added `CompactBuoyancyForcePacketsJob` as a deterministic post-evaluate map-reduce stage. It scans the scheduled candidate range, copies valid packets into a dense prefix, writes `Counters[0].ForcePackets`, and sets overflow if the scheduled candidate window exceeds packet capacity.
Rejected Alternatives: Modifying `PhysicsApplySystem.DrainBuoyancyForcePackets` was rejected because the existing dense-prefix contract is already a narrow bridge to Unity physics. Updating counters from each evaluator lane was rejected because that reintroduces contention.
Scalability potential: Low quality shrinks `CandidateCount` through existing stride math; higher tiers grow the same compact range. There is no binary low/high hardware route.
Hardware Impact: The scalar compaction cost is O(k) over scheduled candidates, but the O(n) force math remains SIMD-friendly and uncontended. This trades a bounded serial pass for removal of per-lane atomic stalls.

Problem: Non-finite input state previously used a dedicated early branch immediately after sanitation.
Solution: Superseded by Loop 13 and later. The implementation preserves `EntityHashID` for forensics and masks simulation output through `simulateBody` / `forceOutputValid`; it does not zero the identity field.
Rejected Alternatives: Continuing force simulation after sanitizing a corrupt row was rejected because it would create fake authority motion. Zeroing identity was rejected because it damages black-box proof.
Scalability potential: All quality levels use the same sanitation route; only cadence/precision changes through continuous `GlobalQualityWeight`.
Hardware Impact: Correctness and forensic protection. Exact gain remains pending Burst Inspector.

Problem: Build verification remains blocked by local execution rules.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching `dotnet build` under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared agent hardware from compiler contention; no runtime saving is claimed from this guard.

## Loop 13 Decisions: Branchless Evaluate Body Mask Polish

[ANALYSIS]
Target: data-dependent return ladder inside `EvaluateBuoyancyJob.Execute`.
Affected systems: `BuoyancyDisplacementJobs.cs` only; runtime chain from Loop 12 remains unchanged.
Zero GC proof: edits stayed inside a Burst `IJobParallelFor` over existing caller-resolved `NativeArray` views. No managed allocation, local native allocation, LINQ, file IO, string path, or runtime registry lookup was added.
State check: status/rationale were read before responding; the exact SHINOBU_201 XML block, selected registry mandates, global authority boundary, domain map, and SHINOBU ledger slice were read before editing. Static scans and brace counts were rerun after editing. Build stayed blocked by CPU 100%.
Rule quote: safety/access branches remain only where C# would otherwise evaluate an invalid `NativeArray` access; body-state decisions use masks.
First-20-minutes route blocker removed: early buoyant salvage/debris force routing now avoids branch-divergent invalid/sleep/non-finite exits in the same hot body pass.

Problem: `EvaluateBuoyancyJob.Execute` still returned early for non-finite input, invalid body rows, already sleeping bodies, sleep-now rows, and non-finite force math. Those are data-dependent lane exits, not structural container guards.
Solution: Replaced that ladder with `hasBody`, `wasSleeping`, `simulateBody`, `simulateWeight`, `sleepNow`, `mathFinite`, and `forceOutputValid`. Force math still executes in one lane shape, while invalid/sleeping/faulted rows multiply or select to zero outputs and skip queue candidates.
Rejected Alternatives: Leaving the return ladder was rejected because it keeps unpredictable control flow in the parallel evaluator. Simulating scrubbed corrupt rows as valid authority was rejected because it creates fake physics truth.
Scalability potential: Low quality continues to reduce scheduled rows through the existing continuous stride and uses cheap analytic flow. Middle/High/Ultra can widen active rows and richer drag/flow response without reintroducing branch topology.
Hardware Impact: Removes four data-dependent exits from the heavy evaluator body. Expected benefit is reduced branch divergence and cleaner Burst vectorization surface on i3/MX350 and ARM64 NEON. Exact microseconds remain PENDING VERIFICATION.

Problem: The previous Loop 12 note implied zeroing `EntityHashID` to fold invalid ingress, which would erase the forensic identity needed by black-box telemetry.
Solution: Kept `EntityHashID` intact and used `simulateBody` to disable physics/queue output. Debug rows retain the owner-local identity while invalid math is suppressed.
Rejected Alternatives: Zeroing identity was rejected because it converts a corrupt-row report into an anonymous row and weakens postmortem proof.
Scalability potential: All tiers preserve the same telemetry identity contract.
Hardware Impact: Correctness and forensic improvement; no speed claim.

Problem: Sleeping/invalid rows could still carry nonzero debug fields if math executed after branch removal.
Solution: Force vectors are multiplied by `simulateWeight`, `debug.NetForce` is selected by `forceOutputValid`, and `SubmergedFraction`, `DepthMeters`, and `SleepScore` are masked for inactive rows.
Rejected Alternatives: Accepting stale debug magnitudes was rejected because telemetry reduction would overstate work for sleeping/invalid rows.
Scalability potential: Low/Middle/High/Ultra telemetry remains comparable as active row count changes.
Hardware Impact: Adds select/multiply guards but avoids branch exits and queue pollution. Exact us pending.

Problem: Build verification remains blocked.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared agent hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 16 Decisions: Force Packet Compaction Branch Polish

[ANALYSIS]
Target: `CompactBuoyancyForcePacketsJob` residual validity branch and stale forensic notes.
Affected systems: `BuoyancyDisplacementJobs.cs` force-packet reduction only; no change to `FixedTick` chain or Unity Rigidbody apply bridge.
Zero GC proof: edits stayed inside a Burst `IJob` over existing caller-resolved `NativeArray<BuoyancyForcePacketDTO>` and `NativeArray<BuoyancyCounterDTO>`. No managed allocation, local native allocation, LINQ, string path, file IO, registry lookup, or new persistent state was introduced.
State check: status/rationale were read before responding; the exact SHINOBU_201 XML block, AGENTS rules, Native Memory/JobSystem mandate, ARM64 layout mandate, and SHINOBU ledger slice were read before editing. Static scans and CPU/compiler gate were rerun after editing.
Rule quote: heavy parallel math already maps one force-packet candidate per lane; the scalar reduction must not reintroduce avoidable branch-shaped validity flow.
First-20-minutes route blocker removed: early buoyant salvage/debris force packets keep the dense-prefix route, but warm compaction no longer branches per candidate validity.

Problem: `CompactBuoyancyForcePacketsJob` still used `if (IsValidPacket(packet))` inside its `for` loop after the evaluator was converted to independent candidate writes.
Solution: Converted compaction validity into a mask. Each iteration loads the current candidate, sanitizes it, field-selects sanitized versus preserved prefix slot through `SelectPacket`, and advances `write` with `math.select(0, 1, valid)`.
Rejected Alternatives: Keeping the branch was rejected because it leaves a data-dependent control gate in the force-packet reduction path. Moving compaction back into the evaluator or using atomics was rejected because it would reintroduce cache-line contention in the heavy `IJobParallelFor`.
Scalability potential: Low quality schedules fewer candidates through existing continuous stride; Middle/High/Ultra can widen the same candidate range without changing reduction topology or adding a binary hardware switch.
Hardware Impact: Expected benefit is fewer unpredictable branches in the scalar reduction and no reintroduced atomics. It adds one preserved-prefix read/write per candidate; this is accepted because heavy force math remains the dominant O(n) lane and exact microseconds are PENDING VERIFICATION.

Problem: The packet-capacity calculation still used a ternary in the reduction job.
Solution: SUPERSEDED BY LOOP 17. The temporary `math.select(0, ForcePackets.Length, ForcePackets.IsCreated)` replacement was rejected because C# evaluates `ForcePackets.Length` before `math.select` can protect default NativeArray metadata. The current source uses a structural `if (ForcePackets.IsCreated)` before reading `.Length`.
Rejected Alternatives: Keeping `math.select` around `.Length` was rejected as an unsafe pseudo-branchless guard. Removing optional-buffer guards was rejected because invalid NativeArray metadata/indexers require structural protection.
Scalability potential: Not quality-facing; preserves one reduction topology across all quality weights.
Hardware Impact: Correctness fence; no measured runtime gain claimed.

Problem: Loop 11/12 rationale still contained a stale statement that invalid ingress could zero `EntityHashID`.
Solution: Updated the durable rationale to match current source: `EntityHashID` is preserved, `FlagNonFinite` is recorded, and physics/queue/debug magnitude output is disabled by `simulateBody` / `forceOutputValid`.
Rejected Alternatives: Leaving contradictory durable memory was rejected because context compression will treat disk logs as truth.
Scalability potential: All tiers preserve the same black-box identity contract.
Hardware Impact: Documentation correctness; no runtime gain.

Problem: The first Loop 16 log patch matched an earlier self-audit close tag instead of the end of `LOG_SHINOBU_201.md`.
Solution: Appended a dedicated Loop 16 bottom report after the prior Loop 15 bottom report so the CTO-readable log again follows top-old, bottom-new ordering.
Rejected Alternatives: Leaving the newest report only in the middle of the log was rejected because reporting protocol explicitly treats the bottom as current.
Scalability potential: Not runtime-facing.
Hardware Impact: Documentation ordering only.

Problem: Build verification remains blocked.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared agent hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 14 Decisions: Dewey Audit Closure

[ANALYSIS]
Target: actionable read-only audit findings from Dewey against the buoyancy Burst and drain bridge.
Affected systems: `BuoyancyDisplacementJobs.cs`, `BuoyancyDisplacementRuntime.cs`, `PhysicsApplySystem.BuoyancyQueue.cs`, `GlobalPhysicsStateManager.BuoyancyBridge.cs`.
Zero GC proof: edits use existing Vault `NativeArray` views, unmanaged `BuoyancyBodyBindingDTO` rows, and managed `Rigidbody` references already owned by the Unity force-apply bridge. No managed collection, LINQ, file IO, string parsing, local persistent native allocation, or hot service polling inside Burst jobs was added.
State check: status/rationale were read before response; the SHINOBU_201 XML block and relevant source were re-read before patching. Static scans, brace/preprocessor counts, whitespace check, and compiler-process/CPU guard were run after edits.
Rule quote: black-box fault rows must survive even when entity identity is corrupt; Unity object resolution must not be repeated as an O(N) fallback per force packet when a state-index cache can preserve the route.

Problem: `GenerateMockBuoyantObjectsJob` wrote `SurfaceAUP` directly into mock states. If cold tuning ever carried NaN from an uninitialized or corrupted row, the benchmark could seed poisoned AUP state before the real evaluator got a chance to sanitize.
Solution: The job now derives `safeSurfaceAup` with `math.select(double3.zero, SurfaceAUP, math.isfinite(SurfaceAUP))` before adding deterministic lane offsets.
Rejected Alternatives: Allocating all tuning with clear memory or trusting boot defaults was rejected because the mock generator must be robust as a standalone stress harness over possibly dirty Vault data.
Scalability potential: Low/Middle/High/Ultra mock counts still scale through active masks; finite gating does not introduce a binary quality path.
Hardware Impact: One vector select before state write; cost is negligible relative to preventing NaN contamination. Exact microseconds remain PENDING VERIFICATION.

Problem: Telemetry counted `FlagNonFinite` only through `aliveMask`. A row with zero or lost identity could carry `FlagNonFinite` and still fail to increment the black-box counter or trigger a dump.
Solution: `ReduceBuoyancyTelemetryJob` now uses `frameOnlyMask` for `nonFiniteMask`, while evaluated/sleeping/force totals stay alive-gated. `EntityHashID` remains preserved for normal corrupt rows.
Rejected Alternatives: Counting every metric independent of liveness was rejected because totals would include inactive slack. Keeping non-finite alive-gated was rejected because the crash dump is mandatory even for anonymous corrupt rows.
Scalability potential: The 300-frame ring shape is unchanged across all quality weights.
Hardware Impact: One integer mask split in a bounded reduction pass. Runtime speed claim is not made; forensic reliability improves.

Problem: `PostFixedTick` drained force packets through a folded-hash resolver that could fall back to a linear tracked-body scan for each packet.
Solution: `DrainBuoyancyForcePackets` now receives the Vault `BodyBindings` array. It validates a cached `RigidbodyIndex` by state index and entity hash first, falling back to the folded-hash resolver only on cache miss, then writes the resolved index back to the binding row. `GlobalPhysicsStateManager` exposes a direct-index validation route for the cache.
Rejected Alternatives: Storing `Rigidbody` references in unmanaged DTOs was rejected because DTOs must stay blittable. Removing the fallback was rejected because bindings are empty after cold clear or after body churn.
Scalability potential: Low quality schedules fewer packets; Middle/High/Ultra can queue more packets without multiplying O(N) fallback scans once bindings are warm.
Hardware Impact: After first resolve per state, packet body resolution becomes O(1) index validation instead of dictionary plus possible O(N) scan. Exact microseconds remain PENDING PROFILER.

Problem: Build verification remains blocked.
Solution: CPU sampled at 100% with no active compiler process output. Build was not launched.
Rejected Alternatives: Launching `dotnet build` under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 15 Decisions: Assembly Boundary / Bottom-Log Repair

Problem: The Dewey closure edited a main-thread bridge and widened one call signature, so a final static boundary pass was required before handing off.
Solution: Scanned owned buoyancy/editor files for direct sibling-domain `using` references and hot service lookups. No direct AI/Graphics/Rendering/VFX/Audio/UI/Narrative/Gameplay/Environment reference was found. `GlobalRegistry` hits remain in runtime registration/hot-swap code, not Burst job files.
Rejected Alternatives: Treating the bridge patch as isolated without verifying namespace and registry surface was rejected.
Scalability potential: No runtime quality change; preserves compile-wall discipline for future Low/Middle/High/Ultra adoption.
Hardware Impact: Documentation/static proof only; no frame-time saving claimed.

Problem: The Loop 14 report was inserted in the middle of `LOG_SHINOBU_201.md` because the patch matched an earlier self-audit close tag.
Solution: Append a bottom report block so the newest durable state is at the bottom as required.
Rejected Alternatives: Leaving the latest forensic report only in the middle of the log was rejected because the reporting protocol is top old, bottom new.
Scalability potential: Not runtime-facing.
Hardware Impact: Documentation only.

## Loop 16 Decisions: Bottom Ordering Repair

Problem: The detailed Loop 16 rationale block was present but sat above older Loop 14/15 rationale because the patch matched an earlier insertion point.
Solution: This bottom note mirrors the current decision state for anti-amnesia tail reads: `CompactBuoyancyForcePacketsJob` compaction validity is mask-selected through `SelectPacket`, the Loop 16 packet-capacity `math.select` note is superseded by Loop 17 structural metadata guarding, `EntityHashID` zeroing text was corrected, and `LOG_SHINOBU_201.md` now has a Loop 16 bottom report.
Rejected Alternatives: Leaving the current rationale only in the middle of the file was rejected because later context compression may only surface tail content.
Scalability potential: Existing continuous stride/candidate count remains the quality curve; no binary tier switch was added.
Hardware Impact: Documentation ordering only. Runtime impact remains the Loop 16 compact-loop branch reduction, pending Burst/profiler proof.

## Loop 17 Decisions: Structural Guard Safety Correction

Problem: Loop 16 converted packet capacity to `math.select(0, ForcePackets.Length, ForcePackets.IsCreated)`. That is unsafe for NativeArray metadata because C# evaluates both value arguments before the call, so `.Length` can be read even when the array is default/uncreated.
Solution: Restored a structural `if (ForcePackets.IsCreated)` guard before reading `ForcePackets.Length`. The branchless part now stays where it is actually safe: candidate validity inside the bounded compaction loop uses `SelectPacket` and `write += math.select(0, 1, valid)`.
Rejected Alternatives: Leaving the `math.select` metadata guard was rejected because it was a fake guard. Reverting the whole compaction loop to `if (IsValidPacket(packet))` was rejected because the source only needed the optional-buffer metadata guard corrected, not the candidate reduction topology undone.
Scalability potential: Low quality still shrinks the candidate window through continuous scheduled count/stride. Middle, High, and Ultra widen the same candidate window without a binary hardware switch; the structural metadata guard is quality-invariant and outside the per-candidate math.
Hardware Impact: Prevents invalid default-NativeArray metadata access. The loop remains O(k) with branchless validity compaction; exact microseconds remain PENDING VERIFICATION.

Problem: Durable files contradicted source after the safety correction.
Solution: Status, rationale, log, and ledger now mark the Loop 16 capacity note as superseded and record Loop 17 as the source-of-truth structural guard correction.
Rejected Alternatives: Leaving stale disk truth was rejected because anti-amnesia protocol treats files, not chat history, as objective state.
Scalability potential: Not runtime-facing.
Hardware Impact: Documentation correctness only.

Problem: Build verification remains blocked.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 19 Decisions: Bottom Ordering Repair

Problem: The detailed Loop 19 rationale block was present but sat above Loop 18 after patch insertion.
Solution: This bottom note mirrors the current source state for anti-amnesia tail reads: `CompactVisibleIndicesJob` retains structural NativeArray guards, uses mask-selected visible index compaction, advances `write` with `math.select`, and marks `VisibleIndices` read/write `[NoAlias]` because preservation reads the prefix slot.
Rejected Alternatives: Leaving the latest rationale only above older text was rejected because future context compression may only retain file tails.
Scalability potential: Existing visible-mask count/cadence remains continuous; no binary tier switch was added.
Hardware Impact: Documentation ordering only. Runtime impact remains pending Burst/profiler proof.

## Loop 20 Decisions: Final Tail Marker

Problem: Repeated Loop 19/20 rationale text caused earlier patches to place Loop 20 above older material.
Solution: This final marker is deliberately terse and sits at file tail: `ReduceBuoyancyTelemetryJob` uses a structural `DebugForces.IsCreated` guard before `DebugForces.Length`; the old lazy ternary is gone; telemetry semantics are unchanged.
Rejected Alternatives: Rewriting the whole rationale file for ordering was rejected because it would churn prior audit history.
Scalability potential: No quality curve change.
Hardware Impact: Documentation ordering only; no runtime gain claimed.

## Loop 20 Decisions: Bottom Ordering Repair

Problem: The detailed Loop 20 rationale block was present but the file tail still ended on Loop 19 after incremental patching.
Solution: This bottom note mirrors the current source state for anti-amnesia tail reads: `ReduceBuoyancyTelemetryJob` initializes `count` to zero and reads `DebugForces.Length` only inside `if (DebugForces.IsCreated)`. Telemetry masks, rsqrt magnitude path, ring writes, DTO layouts, Vault handles, and dependency chain remain unchanged.
Rejected Alternatives: Leaving the current rationale only above older text was rejected because future context compression may only retain file tails.
Scalability potential: Existing active-count/DebugForces-length telemetry window remains continuous; no binary quality switch was added.
Hardware Impact: Documentation ordering only. Runtime impact is guard discipline, not a measured speed gain.

## Loop 20 Decisions: Telemetry Reduce Metadata Guard Polish

[ANALYSIS]
Target: `ReduceBuoyancyTelemetryJob` count setup in the buoyancy telemetry reduction path.
Affected systems: `BuoyancyDisplacementJobs.cs` telemetry reducer only. Runtime scheduling, force-packet compaction, SIMD culling, Vault handles, and Unity apply bridge are unchanged.
Zero GC proof: edit is scalar control flow inside a Burst `IJob` over existing `NativeArray<BuoyancyDebugForceDTO>` and counter/telemetry lanes. No managed allocation, native allocation, LINQ, string path, file IO, service lookup, or persistent state was introduced.
State check: status/rationale and the exact SHINOBU_201 prompt were read before editing. The structural metadata guard pattern from Loop 17 was applied only where it removed a lazy ternary without changing telemetry semantics.
Rule quote: optional NativeArray metadata reads must be protected by structural guards; `math.select` is not a metadata guard, and a ternary is safe but less explicit than the project-standard structural route.

Problem: `ReduceBuoyancyTelemetryJob` still computed `count` with `DebugForces.IsCreated ? math.min(..., DebugForces.Length) : 0`.
Solution: Replaced the ternary with `int count = 0; if (DebugForces.IsCreated) count = math.min(math.max(0, ActiveStateCount), DebugForces.Length);`. The debug loop and telemetry math remain unchanged.
Rejected Alternatives: Replacing this with `math.select` was rejected because `.Length` would be evaluated before the select. Leaving the ternary was rejected because the recent Loop 17/19 audit standardized optional NativeArray metadata reads as explicit structural guards.
Scalability potential: No runtime quality curve changes; low/mid/high/ultra all preserve the same telemetry reduction window derived from active count and Vault lane length.
Hardware Impact: No speed claim. This is metadata-guard discipline and future-proofing against default-array execution paths; exact microseconds remain PENDING VERIFICATION.

Problem: Build verification remains blocked.
Solution: No build was launched; CPU/build guard remains active until a safe window exists and no compiler process is running.
Rejected Alternatives: Launching build or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention.

## Loop 19 Decisions: Visible Index Compaction Mask Polish

[ANALYSIS]
Target: `CompactVisibleIndicesJob` in the SHINOBU SIMD culling path.
Affected systems: `BuoyancySimdVectorization.cs` presentation culling compact pass only. Runtime buoyancy authority, force packets, and Unity apply bridge are unchanged.
Zero GC proof: edits stay inside a Burst `IJob` over existing `NativeArray<int>` lanes. No managed allocation, native allocation, LINQ, string path, file IO, service lookup, or persistent state was introduced.
State check: broad Physics/AI NativeArray scan was run for aliasing context; only SHINOBU-owned SIMD culling code was edited. Static scans and CPU/compiler gate were rerun after editing.
Rule quote: optional NativeArray metadata needs structural guards; candidate validity inside an already bounded reduction loop can be mask-selected.
First-20-minutes route blocker removed: visible-instance compact output for early route debug/proxy scenes avoids a per-candidate branch while preserving safe capacity guards.

Problem: `CompactVisibleIndicesJob` still used `if (value >= 0 && write < VisibleIndices.Length)` inside the compact loop.
Solution: Added structural guards for mask/output creation and capacity, then converted candidate validity into `(value >= 0) & (write < capacity)`. The output slot preserves existing data via `math.select(preserved, value, valid)`, and `write` advances with `math.select(0, 1, valid)`.
Rejected Alternatives: Keeping the branch was rejected because it is a data-dependent per-candidate filter in SHINOBU-owned SIMD reduction. Using `math.select` around `.Length` was rejected because C# evaluates the metadata read before the call. Writing invalid values unconditionally was rejected because it can corrupt the last valid prefix when capacity is full.
Scalability potential: Low quality shrinks the visible mask count through existing culling/cadence; Middle/High/Ultra compact a larger window with the same topology. No binary quality switch was added.
Hardware Impact: Expected benefit is fewer unpredictable branches in scalar visible-index reduction. It adds one preserved-slot read per candidate; exact microseconds remain PENDING VERIFICATION.

Problem: `VisibleIndices` was annotated `[WriteOnly]`, but branchless preservation requires reading the existing prefix slot.
Solution: Changed it to `[NoAlias] public NativeArray<int> VisibleIndices;` and retained `[WriteOnly, NoAlias]` on `VisibleCount`.
Rejected Alternatives: Reading from a `[WriteOnly]` lane was rejected as a Burst safety contract violation. Keeping write-only would require retaining the branch or risking prefix corruption.
Scalability potential: Not quality-facing.
Hardware Impact: Corrects the alias/access contract; no speed claim beyond the branch-reduction pending proof.

Problem: Build verification remains blocked.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 18 Decisions: Force Packet Padding Determinism

[ANALYSIS]
Target: `BuoyancyForcePacketDTO` compacted dense-prefix byte determinism.
Affected systems: `CompactBuoyancyForcePacketsJob` sanitization/select helper only. Runtime scheduling, Vault handles, and Unity apply bridge are unchanged.
Zero GC proof: edits are two unmanaged field assignments inside Burst helper methods over existing `NativeArray<BuoyancyForcePacketDTO>` rows. No managed allocation, native allocation, LINQ, file IO, string path, or registry lookup was introduced.
State check: status/rationale, exact SHINOBU_201 XML, SHINOBU ledger slice, Native Memory/JobSystem mandate, and ARM64 layout mandate were read before editing. DTO property scan and forbidden pattern scans were rerun after editing. Build stayed blocked by CPU 100%.
Rule quote: padding is part of the runtime memory contract; byte-stable forensic/native payloads must not preserve stale slack bytes in compacted rows.
First-20-minutes route blocker removed: salvage/debris force packets remain deterministic at byte level when compacted into the physics apply prefix.

Problem: `SelectPacket` copied all semantic fields but left `_pad0` from the preserved prefix row when selecting a valid sanitized packet.
Solution: `SanitizePacket` now zeros `packet._pad0`, and `SelectPacket` selects `_pad0` with the same validity mask as semantic fields.
Rejected Alternatives: Ignoring padding was rejected because force packets are unmanaged native rows and may be copied/dumped byte-for-byte by forensic tooling. Expanding the DTO layout was rejected because the existing 128-byte explicit layout is already aligned.
Scalability potential: All quality weights share the same deterministic packet byte layout; existing continuous scheduled count controls how many rows enter compaction.
Hardware Impact: One uint assignment and one uint select in the scalar compact helper. Runtime speed gain is not claimed; byte-stable telemetry/forensics improves.

Problem: CS1612/property debt could still be hidden in the owned buoyancy DTO/job surface.
Solution: Reran source scan for `{ get; }`, `{ get; private set; }`, and expression-bodied property patterns under `Assets/_Project/Scripts/Physics/Buoyancy`. No matches were found.
Rejected Alternatives: Relying on previous layout audits was rejected because the current mandate explicitly called out property debt in hot structs.
Scalability potential: Not quality-facing.
Hardware Impact: Static proof only; no runtime saving claimed.

Problem: Build verification remains blocked.
Solution: CPU was sampled at 100%, and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention; no runtime saving claimed from this guard.

## Loop 19 Decisions: Bottom Ordering Repair

Problem: The detailed Loop 19 rationale block was present but sat above Loop 18 after patch insertion.
Solution: This bottom note mirrors the current source state for anti-amnesia tail reads: `CompactVisibleIndicesJob` retains structural NativeArray guards, uses mask-selected visible index compaction, advances `write` with `math.select`, and marks `VisibleIndices` read/write `[NoAlias]` because preservation reads the prefix slot.
Rejected Alternatives: Leaving the latest rationale only above older text was rejected because future context compression may only retain file tails.
Scalability potential: Existing visible-mask count/cadence remains continuous; no binary tier switch was added.
Hardware Impact: Documentation ordering only. Runtime impact remains pending Burst/profiler proof.

## Loop 20 Decisions: Final Tail Marker

Problem: The rationale tail previously fell back to Loop 19 after Loop 20 was inserted above repeated material.
Solution: This final marker is deliberately placed after the trailing Loop 19 block: `ReduceBuoyancyTelemetryJob` now uses `count = 0` plus `if (DebugForces.IsCreated)` before reading `DebugForces.Length`; telemetry math and dependency chain are unchanged.
Rejected Alternatives: Reordering or rewriting the whole rationale file was rejected because it would churn earlier audit history.
Scalability potential: No quality curve change.
Hardware Impact: Documentation ordering only; no runtime gain claimed.

## Loop 23 Decisions: Frustum Fixed Plane Loop / Scheduler Ternary Polish

[ANALYSIS]
Target: SHINOBU-owned `VectorizedFrustumCullJob` and scalar FixedTick scheduling math.
Affected systems: `BuoyancySimdVectorization.cs` presentation culling kernel and `BuoyancyDisplacementRuntime.cs` scheduler/mock setup only. Authority force math, packet compaction, Vault IDs, DTO layouts, and Unity apply bridge are unchanged.
Zero GC proof: all edits are integer/float scalar expressions inside existing methods over already-resolved NativeArray lanes. No managed allocation, native allocation, LINQ, file IO, string parsing, service lookup, or persistent state was introduced.
State check: status/rationale, exact SHINOBU_201 XML, AGENTS, domain map, SHINOBU ledger lane, and relevant mandates were read before patching. Static scans and CPU/compiler guard were rerun after patching.
Rule quote: candidate plane contribution can be mask-selected; empty NativeArray access still requires structural protection.
First-20-minutes route blocker removed: early route debug/proxy scenes can consume the culling Dear Lie with a fixed six-plane kernel shape instead of a variable loop termination.

Problem: `VectorizedFrustumCullJob` used `for (int i = 0; i < planeCount; i++)`, leaving a data-dependent loop bound in the culling kernel.
Solution: Added a structural empty-plane guard, then changed the cull body to a fixed six-iteration loop. Each iteration reads a safe clamped plane slot and applies `math.select(1f, planePass * finitePlane, inRange != 0)`, so inactive plane slots are neutral and valid/non-finite in-range planes still affect visibility deterministically.
Rejected Alternatives: Blindly reading six planes without checking capacity was rejected because a short or default plane buffer would be an unsafe memory read. Keeping the variable loop was rejected because Task 08 explicitly targets packed six-plane brute-force culling and Burst is more likely to unroll a constant six-pass loop.
Scalability potential: Low quality still shrinks cull cadence/count through owner scheduling; Middle/High/Ultra use the same six-plane kernel over larger windows. No binary tier switch was added.
Hardware Impact: Static expectation is lower branch pressure and better unroll/vectorization opportunity in the cull kernel. Exact microseconds remain PENDING VERIFICATION until Burst Inspector/player benchmark.

Problem: `FixedTick` and mock seeding still used scalar `?:` fallback expressions for active count, evaluation offset, and mock count.
Solution: Replaced those with `math.select` where both operands are safe scalar values: active count selects authored count versus `_stateCapacity`, offset selects modulo versus zero, and mock count selects authored mock count versus `MockObjectCount`.
Rejected Alternatives: Rewriting lifecycle/null/lock guards was rejected because those are structural safety exits. Replacing optional NativeArray metadata guards with `math.select` was rejected because C# eagerly evaluates `.Length`.
Scalability potential: Active count and stride remain continuous with `GlobalQualityWeight`; low tier evaluates fewer rows, middle/high/ultra widen the same path without low/high switch code.
Hardware Impact: Scalar control cleanup only; no measured runtime claim.

Problem: Build verification remains blocked.
Solution: CPU sampled at 100% and no active `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process output was returned. Build was not launched.
Rejected Alternatives: Launching build under 100% CPU or claiming compile/Burst proof from static scans was rejected.
Scalability potential: Not runtime-facing.
Hardware Impact: Protects shared hardware from compiler contention.

## Loop 21 Decisions: World Import Compile-Wall Hygiene

[ANALYSIS]
Target: direct sibling-domain namespace import in `BuoyancyDisplacementRuntime.cs`.
Affected systems: managed buoyancy runtime import surface only. Burst jobs, DTO layouts, Vault handles, scheduling, and AUP math are unchanged.
Zero GC proof: edit removes a using directive only. No allocation path or runtime logic was added.
State check: status/rationale, SHINOBU_201 ledger, and source namespace for `HectonFloatingOrigin` were checked. `HectonFloatingOrigin` is in `Hecton8.Core`, already imported by the file.

Problem: `BuoyancyDisplacementRuntime.cs` imported `Hecton8.World` even though the only floating-origin calls resolve through `Hecton8.Core.HectonFloatingOrigin`.
Solution: Removed `using Hecton8.World;`. AUP precision remains intact because `ResolveSectorAUP()` and debug draw conversion still use the Core floating-origin type.
Rejected Alternatives: Keeping the stale import was rejected because it weakens compile-wall evidence. Replacing AUP origin sampling with zero or a local mock was rejected because it would break the 100 km jitter rule.
Scalability potential: No quality curve change. Low/Middle/High/Ultra all keep the same AUP-localized scheduling and debug path.
Hardware Impact: Compile-wall hygiene only; no runtime speed claim.

Problem: The Loop 20 ledger line still said static verification was required after scans had already been run.
Solution: Updated the ledger to state static scans are clean and compile/player proof remains blocked by the build guard.
Rejected Alternatives: Leaving stale verification wording was rejected because disk logs are long-term memory.
Scalability potential: Not runtime-facing.
Hardware Impact: Documentation correctness only.

## Loop 22 Decisions: ParallelFor Suppression Invariant Comments

[ANALYSIS]
Target: `NativeDisableParallelForRestriction` sites in `BuoyancyDisplacementJobs.cs`.
Affected systems: Burst job field comments only. Runtime scheduling, force math, DTO layout, Vault handles, dependency graph, and Unity apply bridge are unchanged.
Zero GC proof: comments only. No managed allocation, native allocation, LINQ, file IO, string parsing, service lookup, or persistent state was introduced.
State check: status/rationale were read before the edit; suppression sites were isolated with source scan before comments were added.
Rule quote: safety suppression must carry a partition invariant that proves per-lane uniqueness or an explicit dependency fence.

Problem: Three fields suppressed ParallelFor restriction, but only one had a vague comment and none gave the concrete index mapping needed for reviewer proof.
Solution: Added explicit invariant comments for mock state writes and evaluator state/debug writes. Mock seeding documents one scheduled lane writes `States[index]` after length guard and no later buoyancy job runs until the seed handle completes. Evaluation documents the injective `workIndex * max(1, stride) + offset` mapping and the dependency fence before debug reads.
Rejected Alternatives: Removing suppression from evaluator was rejected because state/debug writes use strided row mapping rather than the raw job index. Removing suppression from mock seed was deferred because the current implementation writes through `States.GetUnsafePtr()` and compile proof is blocked by the CPU gate.
Scalability potential: No quality curve change. Low/Middle/High/Ultra all keep the same partitioned lane ownership; only active row count/stride changes continuously through existing scheduling math.
Hardware Impact: Review safety/documentation only. No runtime microsecond claim; compile/Burst proof remains PENDING VERIFICATION.

## Loop 23 Decisions: Bottom Ordering Marker

Problem: The detailed Loop 23 frustum rationale was inserted above older Loop 21/22 material during incremental patching.
Solution: This bottom marker mirrors the current source state: `VectorizedFrustumCullJob` uses a structural empty-plane guard and a fixed six-pass culling loop with neutral out-of-range planes; `FixedTick` active-count, evaluation-offset, and mock-count fallbacks use `math.select` over safe scalar operands.
Rejected Alternatives: Rewriting older rationale history was rejected because it would churn prior forensic records.
Scalability potential: Low quality continues to shrink row cadence/count through existing continuous stride math; higher tiers widen the same kernel without binary switches.
Hardware Impact: Static expectation is branch/unroll improvement in culling; measured microseconds remain PENDING VERIFICATION because CPU stayed at 100% and no build/profiler was launched.

## Loop 23 Decisions: Final Tail Marker

Problem: Loop 23 is the latest status entry; rationale tail must identify the fixed six-plane culling loop rather than an older suppression-comment pass.
Solution: This bottom marker restores the current durable state: `VectorizedFrustumCullJob` uses a fixed six-pass masked plane loop, and `FixedTick` scalar fallbacks use `math.select` over safe operands.
Rejected Alternatives: Rewriting the entire rationale history was rejected because it would churn prior forensic records.
Scalability potential: Existing continuous active-count and stride math remain the scalability route; no low/high binary hardware switch was introduced.
Hardware Impact: Documentation ordering only. Runtime proof remains PENDING VERIFICATION because no build/profiler was launched under the CPU guard.

## Loop 24 Decisions: Math Helper Vaccination / Plane Metadata Guard

[ANALYSIS]
Target: SHINOBU-owned Burst helper math and frustum cull optional plane buffer metadata.
Affected systems: `BuoyancyDisplacementJobs.cs` helper methods and `BuoyancySimdVectorization.cs` culling/transcendental helper methods only. DTO layouts, Vault IDs, dependency chain, GlobalRegistry routes, and Unity apply bridge are unchanged.
Zero GC proof: edits are scalar arithmetic and one structural NativeArray guard inside existing Burst code. No managed allocation, native allocation, LINQ, file IO, string parsing, service lookup, or persistent state was introduced.
State check: status/rationale and exact SHINOBU_201 prompt were read; static scans were rerun after patching.
Rule quote: a helper should not depend on caller sanitation when a single non-finite value can poison rollback, telemetry, or culling masks.

Problem: `VectorizedFrustumCullJob` read `Planes.Length` before proving `Planes.IsCreated`.
Solution: Added a structural `if (!Planes.IsCreated)` guard that writes an invisible mask and returns before any metadata read. The fixed six-plane loop still uses a cached `planeCapacity` and mask-selected inactive planes.
Rejected Alternatives: Using `math.select` around `Planes.Length` was rejected because C# eagerly evaluates metadata. Assuming default `NativeArray.Length` is safe was rejected because previous Loop 17 established explicit structural guards as the project style.
Scalability potential: No binary tier switch. Low quality can still lower culling cadence/count; Middle/High/Ultra widen the same masked six-plane kernel.
Hardware Impact: Correctness fence. No speed claim; one cold structural branch prevents unsafe optional-buffer execution.

Problem: `EstimateObjectHeightMeters`, `FastSpeed`, `SinPolynomial`, and `ExpNegPolynomial01` trusted finite inputs even though they are reusable helper surfaces.
Solution: Finite-gated buoyancy volume, velocity, speed squared, quality blend, polynomial radians, polynomial degree, and negative-exp input before `rsqrt`, `floor`, `abs`, `saturate`, and `lerp`.
Rejected Alternatives: Leaving helper sanitation to callers was rejected because future owner adoption can bypass the current sanitized call sites. Using exact `math.sin`/`math.sqrt` was rejected because this pass preserves SIMD-friendly polynomial/rsqrt math.
Scalability potential: Low quality still collapses toward cheaper approximations; Middle/High/Ultra keep the same continuous quality blend with safer ingress. No low/high binary branch added.
Hardware Impact: NaN containment and deterministic helper behavior. Measured microseconds remain PENDING VERIFICATION because build/profiler stayed blocked by the CPU guard.

## Loop 25 Decisions: Bacon Audit Closure / Required Lane Guards

[ANALYSIS]
Target: Bacon read-only audit findings 1-4.
Affected systems: SHINOBU SIMD job guards, editor/manual SIMD benchmark surface, buoyancy force packet drain resolver path, and explicit-layout validation. DTO sizes, Vault IDs, Burst math semantics, and runtime dependency graph are unchanged.
Zero GC proof: hot-loop edits add structural `IsCreated` guards and pass an existing manager reference. No managed collection allocation, native allocation, LINQ, string formatting, file IO, or service lookup inside Burst jobs was introduced.
State check: status/rationale were read before applying Bacon findings; static scans and compiler-process guard were rerun after patching.
Rule quote: optional/default NativeArray metadata requires structural guards; intentional job completion must be named and isolated from frame ticks; byte-dumped DTOs need offset proof, not just size proof.

Problem: Reusable SIMD jobs read required lane `.Length` values before proving the lanes were created.
Solution: Added early `IsCreated` guards in benchmark seed, state-to-SoA, SoA-to-state, hydrodynamics, scalar reference, AUP localization, spatial query, frustum cull, local delta, and reduction jobs.
Rejected Alternatives: Treating required Vault lanes as always-created was rejected because these kernels are reusable owner-facing surfaces. Replacing guards with `math.select` was rejected because `.Length` would still be eagerly evaluated.
Scalability potential: No binary tier switch. Low quality can shrink active counts; Middle/High/Ultra widen the same guarded kernels.
Hardware Impact: Correctness fence. One structural branch per lane job protects default/stale buffer schedules; measured microseconds remain PENDING VERIFICATION.

Problem: `GenerateMockSimdBenchmark()` forced job completion several times and was exposed on the runtime component; other completion points needed explicit boot/editor labels.
Solution: Wrapped the SIMD benchmark method in `#if UNITY_EDITOR` and added blocking-sync comments stating it is X-Ray/manual only and never called from `FixedTick`. Emergency mock seeding and cold buffer clearing now carry cold/editor or cold-boot sync comments.
Rejected Alternatives: Building an async benchmark state machine was rejected in this pass because the editor X-Ray benchmark intentionally needs explicit completion points to measure microseconds. Leaving it in player builds was rejected.
Scalability potential: Editor-only. Low/Middle/High/Ultra benchmark weights remain controlled by continuous scalar-probe and quality fields.
Hardware Impact: Player runtime surface removed. Editor benchmark still blocks by design when manually invoked.

Problem: The force drain loop resolved the physics manager through static bridge calls per packet.
Solution: Resolved `GlobalPhysicsStateManager` once before the packet loop and passed it into binding/index/hash lookup overloads.
Rejected Alternatives: Removing folded-hash fallback entirely was rejected because first-time cold rebinding still needs repair when the binding cache is empty. A separate rebinding job was rejected as a larger ownership change.
Scalability potential: Same force packet budget and continuous evaluation count. Higher packet bursts avoid repeated manager resolution.
Hardware Impact: Removes one manager lookup path per packet. Dictionary/folded fallback still exists only for cache misses; measured gain pending profiler.

Problem: Layout validation checked sizes for all DTOs but offsets only for `BuoyancyStateDTO`.
Solution: Extended `BuoyancyDisplacementLayout` with explicit offset validation for tuning, force packet, flow sample, telemetry, material volume, counter, debug force, and body binding DTOs.
Rejected Alternatives: Size-only validation was rejected because padding drift can preserve total size while breaking native payloads. Runtime reflection offset discovery was avoided; offsets are manual constants beside the explicit layouts.
Scalability potential: Not quality-facing. It protects all quality tiers from binary payload drift.
Hardware Impact: Cold static validation only; no gameplay-frame cost.

## Loop 26 Decisions: SIMD DTO Layout Validator

[ANALYSIS]
Target: SHINOBU SIMD payload ABI: `SimdFloat3Padded`, `SimdMathToleranceDTO`, `SimdTelemetryEntry`, and `SimdHydrodynamicTuningDTO`.
Affected systems: SIMD layout proof, runtime handle readiness, and editor X-Ray layout audit only. Vault IDs, DTO sizes, Burst job math, scheduler dependencies, force packet drain, and physics apply bridge are unchanged.
Zero GC proof: runtime additions are static size/offset checks and boolean readiness gates. No native allocation, managed collection allocation, LINQ, gameplay file IO, string formatting, or hot-loop service lookup was introduced. The editor window already owns editor-only UI strings; this loop only adds the validation result to that facade.
State check: status/rationale and exact SHINOBU_201 prompt were read before edits. Static scans were rerun after patching; no build or rebuild was launched.
Rule quote: byte-dumped Vault payloads need offset proof, not just `[StructLayout]` intent.

Problem: Runtime buoyancy DTOs had explicit size/offset validation, but SIMD Vault payloads still relied on visual inspection of `[FieldOffset]` declarations.
Solution: Added `SimdVectorizationLayout` with one-time cold validation for exact sizes and manual offsets. `SimdFloat3Padded=16`, `SimdMathToleranceDTO=16`, `SimdTelemetryEntry=64`, and `SimdHydrodynamicTuningDTO=64`. Runtime handle acquisition/readiness now requires both buoyancy and SIMD layout validators to pass.
Rejected Alternatives: Folding SIMD offsets into `BuoyancyDisplacementLayout` was rejected because the SIMD vectorization file owns these ABI types and should keep its byte contract beside the structs. Runtime reflection/Marshal offset discovery was rejected; the project requires explicit manual offsets that fail when a field moves.
Scalability potential: Not quality-facing. The validator protects every quality weight from corrupt Vault payload hydration; Low/Middle/High/Ultra all use the same SIMD lanes with different active counts and scalar-probe weights.
Hardware Impact: Cold static validation only. Gameplay-frame cost is a cached boolean check in handle readiness paths, not inside Burst jobs. Measured microseconds remain PENDING VERIFICATION because no compile/profiler was launched.

Problem: The X-Ray editor facade printed size/align but did not surface whether the hard-coded SIMD ABI validator passed.
Solution: Added a `Validate: OK/FAIL` line to the existing ARM64 SIMD layout audit.
Rejected Alternatives: Adding a new runtime debug HUD was rejected because this is editor-only human tuning/validation and should not expand player UI or localization surfaces.
Scalability potential: Editor-only facade. It keeps designers and technical artists informed without recompiling C# for the layout question.
Hardware Impact: Editor-only string output. No player runtime cost.

## Loop 27 Decisions: Cold IO Boundary Labels / Compile-Wall Audit

[ANALYSIS]
Target: Managed file IO review surface and SHINOBU buoyancy/SIMD assembly boundary.
Affected systems: source comments and durable architecture record only. No scheduler, DTO, Vault, math, or Unity force-apply behavior changed.
Zero GC proof: no allocation site was added. Existing `FileStream`/`Path` work is labeled as cold CSV hydration or fault/benchmark dump work; `FixedTick`, Burst jobs, and force drain remain free of file IO.
State check: status/rationale were read before edits. Static scans confirmed comments, brace/preprocessor/non-ASCII balance, and no sibling-domain `using` import in SHINOBU buoyancy/editor files. No build or rebuild was launched.
Rule quote: managed IO may exist only outside the steady-state frame path, and that boundary must survive code review and context compression.

Problem: `TryLoadMaterialVolumesCsv`, `TryLoadSimdMathTolerancesCsv`, `ReadFileIntoNativeScratch`, `DumpBlackBoxOnce`, and `TryDumpSimdTelemetry` used existing managed path/stream APIs without source-level labels proving they are not frame-loop work.
Solution: Added comments identifying cold designer/manual CSV hydration, cold scratch-buffer file reads, fault-only telemetry dump, and editor/benchmark SIMD dump surfaces.
Rejected Alternatives: Removing the dump writers was rejected because black-box postmortem output is a project mandate. Moving to async Addressables was rejected because these files are local tuning CSVs and crash dumps, not streamed gameplay assets.
Scalability potential: Not quality-facing. Low/Middle/High/Ultra runtime jobs remain unaffected; tuning rows continue to feed continuous quality math after cold hydration.
Hardware Impact: Review safety only. No gameplay microsecond claim; comments prevent future misuse of cold IO from the solver cadence.

Problem: The SHINOBU buoyancy/SIMD files lack a local physics asmdef and currently inherit the broader `Hecton8.Core` assembly scope.
Solution: Audited the parent/core/editor/physics asmdefs and sibling imports. No direct sibling-domain import remains in owned files. Creating a new local asmdef was rejected in this pass because `PhysicsApplySystem.BuoyancyQueue.cs` and `GlobalPhysicsStateManager.BuoyancyBridge.cs` are partial-class injections that must compile with their existing core class definitions unless the core ownership model is changed by an integrator.
Rejected Alternatives: Splitting the folder into `Hecton8.Physics.Buoyancy.Runtime.asmdef` was rejected as unsafe without moving the owning partial classes or replacing them with a contract/event bridge. That is beyond the SHINOBU SIMD hot-path scope and would risk a compile wall.
Scalability potential: No quality curve change. This is compile-wall boundary documentation.
Hardware Impact: No runtime impact. Iteration-time risk remains noted for integrator follow-up; no assembly reference to a sibling runtime domain was introduced.

## Loop 28 Decisions: Hydrodynamics Lane-4 SIMD Kernel

[ANALYSIS]
Target: X-Ray hydrodynamics benchmark path and SIMD math helper surface.
Affected systems: `VectorizedHydrodynamicsLane4Job`, 4-wide sine polynomial overload, and editor/manual benchmark scheduling. Gameplay buoyancy solver, force application, Vault IDs, DTO layouts, and dispatcher phases are unchanged.
Zero GC proof: the lane kernel is a Burst value-type job over existing Vault `NativeArray<T>` buffers. It introduces no managed collections, no native allocations, no file IO, no LINQ, and no hot-loop service lookup.
State check: status/rationale and exact SHINOBU_201 prompt were read before edits. Static scans confirmed braces/preprocessor/non-ASCII balance, no forbidden hot-path patterns, touched-path diff check clean, and no active compiler process. No build/rebuild was launched.
Rule quote: a SIMD benchmark must do packed lane math, not only schedule many scalar `Execute` calls.

Problem: `VectorizedHydrodynamicsJob` still processed one entity per `Execute`, leaving the X-Ray benchmark dependent on Burst's cross-iteration auto-vectorizer instead of explicitly packed math.
Solution: Added `VectorizedHydrodynamicsLane4Job`, which processes four entities per scheduled lane using `float4` x/y/z/drag registers. It performs 4-wide finite sanitation, drag integration, continuous turbulence quality weighting, max-speed clamping, and force output. The editor benchmark now rounds its count down to a multiple of four, schedules lane groups, and records the vectorized entity count.
Rejected Alternatives: Replacing the gameplay solver was rejected because this is a benchmark/proof lane and gameplay force semantics are owned by the existing fixed-tick buoyancy pipeline. Hand-writing architecture-specific X86/ARM intrinsics was deferred until Burst Inspector proof shows the `float4` path fails to emit packed instructions.
Scalability potential: Low quality still collapses turbulence amplitude through continuous quality weighting; Middle/High/Ultra widen the same packed kernel with no binary hardware switch.
Hardware Impact: Static expectation is fewer scheduled job invocations and explicit 4-wide ALU for the hydrodynamics benchmark. Exact microseconds remain PENDING VERIFICATION without Burst Inspector/profiler.

Problem: The polynomial sine helper only accepted scalar radians, forcing the new lane kernel either to call it four times or duplicate approximation code.
Solution: Added a `float4` overload of `SinPolynomial` with the same range reduction, degree gating, quality blend, and polynomial coefficients as the scalar path.
Rejected Alternatives: Calling scalar sine four times was rejected because it weakens the lane proof. Using `math.sin` was rejected because Task 10 requires polynomial approximation for vectorization.
Scalability potential: Same continuous degree/quality behavior as scalar helper.
Hardware Impact: Static expectation is packed polynomial ALU; measured throughput remains pending.

## Loop 29 Decisions: Lane-4 ParallelFor Safety Contract

[ANALYSIS]
Target: `VectorizedHydrodynamicsLane4Job` writable lane safety.
Affected systems: SHINOBU X-Ray/editor hydrodynamics benchmark job only. DTO layouts, Vault IDs, fixed-tick buoyancy force semantics, force apply bridge, and SIMD telemetry ABI are unchanged.
Zero GC proof: field attributes and comments only. No managed allocation, native allocation, LINQ, file IO, service lookup, or hot-loop string work was introduced.
State check: status/rationale, root AGENTS, the SHINOBU prompt scope, Unity MCP skill constraints, and Loop 28 source were read before patching. CPU sampled 82.6%, so no build/rebuild was launched.
Rule quote: a ParallelFor suppression must carry a concrete partition invariant that proves lane uniqueness.

Problem: The lane-4 job writes `Velocities[baseIndex..baseIndex+3]` and `OutputForces[baseIndex..baseIndex+3]` from `Execute(laneIndex)`. Unity's parallel-for safety expects writable `NativeArray` accesses to map to the scheduled index unless the owner explicitly proves a safe custom partition.
Solution: Added `[NativeDisableParallelForRestriction]` to the two writable lane arrays and placed the invariant beside the fields: schedule count is `vectorizedCount / 4`, `baseIndex = laneIndex * 4`, and each lane owns exactly four rows with no overlap.
Rejected Alternatives: Treating `[NoAlias]` as sufficient was rejected because aliasing proof and ParallelFor index-range proof are separate contracts. Reverting to one entity per `Execute` was rejected because it erases the explicit SIMD lane proof introduced in Loop 28.
Scalability potential: No quality curve change. Low quality still reduces turbulence through continuous `GlobalQualityWeight`; Middle/High/Ultra widen the same packed benchmark lane without binary hardware switches.
Hardware Impact: Prevents editor safety exceptions and avoids invalid release assumptions for the lane-4 writer. Measured microseconds remain PENDING VERIFICATION because the CPU gate blocked compile/profiler proof.

## Loop 30 Decisions: X-Ray Editor Facade Allocation Edge Polish

[ANALYSIS]
Target: `BurstVectorizationXRayWindow` event wiring and fixed-buffer readout writer.
Affected systems: editor-only X-Ray facade. Runtime Burst jobs, Vault handles, DTO layouts, benchmark scheduler, telemetry ABI, and player fixed-tick solver are unchanged.
Zero GC proof: no player path changed. Editor facade still creates UI Toolkit controls and assigns `Label.text` only in editor/manual telemetry display; the hot Burst/runtime lanes remain Vault-backed and allocation-free.
State check: status/rationale were read before this loop; scoped source scans and SHINOBU prompt extraction were rerun after patching. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: editor facades must not hide sloppy callback/string surfaces that would be rejected in runtime UI.

Problem: The X-Ray scalar fallback slider used a lambda callback. It is editor-only, but it is still a closure-shaped site in the exact editor facade requested by the task.
Solution: Replaced the lambda with a named `OnScalarFallbackChanged(ChangeEvent<float>)` method that forwards to the existing unmanaged tuning write.
Rejected Alternatives: Leaving it because it is editor-only was rejected; this facade is part of the SHINOBU task deliverable and should model the same callback discipline as runtime UI.
Scalability potential: Editor-only; no quality curve change. The slider still writes continuous `ScalarFallbackWeight01`.
Hardware Impact: Player cost 0. Editor allocation/readability hygiene only; no runtime microsecond claim.

Problem: `AppendFixed2` wrote fractional digits without checking capacity after prior append operations.
Solution: Added explicit capacity guards before the initial work and each fractional character write.
Rejected Alternatives: Relying on current readout strings staying under the 1024-char buffer was rejected because future audit lines can grow.
Scalability potential: Editor-only, no quality curve change.
Hardware Impact: Editor correctness fence only. Compile/profiler proof remains PENDING VERIFICATION.

## Loop 31 Decisions: Vault Generation Descriptor Migration

[ANALYSIS]
Target: SHINOBU-owned buoyancy/SIMD runtime Vault handles in `BuoyancyDisplacementRuntime`.
Affected systems: handle storage, cold descriptor acquisition, phase-local NativeArray resolution, owner teardown, and DataVault hot-swap handling. DTO layouts, BufferIDs, Burst jobs, quality curves, force packet semantics, and editor X-Ray controls are unchanged.
Zero GC proof: the player runtime now stores only 16-byte `VaultGenerationHandle<T>` descriptors. It does not add managed collections, private Persistent `NativeArray<T>` fields, LINQ, gameplay file IO, or hot-loop service polling. `NativeArray<T>` views are method-local and come from `IDataVault.TryResolveHandle`.
State check: status/rationale were read before edits; SHINOBU prompt extraction still identifies the 20-task block. Static scans found no `VaultBufferHandle`, `GetBufferHandle`, `.Resolve(`, handle `.IsCreated`, native allocation, random, `foreach`, `Pack=`, or hot string formatting in the runtime file. CPU sampled 70.3%, so no build/rebuild was launched.
Rule quote: persistent owner state must be descriptor-only; raw Vault views are phase-local.

Problem: `BuoyancyDisplacementRuntime` persisted pointer-bearing `VaultBufferHandle<T>` fields even after the core Vault boundary documented `VaultGenerationHandle<T>` as the new pointer-safe descriptor route.
Solution: Migrated all 22 buoyancy/SIMD handles to `VaultGenerationHandle<T>`. `EnsureVaultDescriptor` validates an existing descriptor by resolving it and checking capacity before reacquiring through `GetGenerationHandle`; `ResolveVaultBuffer` converts descriptors into method-local `NativeArray<T>` views only at the phase that schedules jobs, drains force packets, hydrates CSVs, writes telemetry, or draws editor gizmos.
Rejected Alternatives: Keeping the legacy handles because they still resolve was rejected; they carry stale pointer fields and contradict the current Vault sovereignty ledger. Replacing descriptors with private `NativeArray<T>` fields was rejected because it recreates memory ownership outside GlobalDataVault. Reacquiring descriptors blindly every call was rejected because it adds no safety when the generation still resolves and would make lifecycle accounting harder to audit.
Scalability potential: No binary tier change. Low/Middle/High/Ultra continue to use the same continuous `GlobalQualityWeight` math and lane counts; the descriptor migration protects all tiers from stale pointer ownership without touching visual quality.
Hardware Impact: Removes persistent cached pointer payload from the runtime owner. Expected gain is correctness and compaction safety, not measured frame-time speed; runtime microseconds remain PENDING VERIFICATION.

Problem: Descriptor ownership needs an explicit lifecycle route; clearing fields without returning ownership leaves the Vault ledger ambiguous.
Solution: Added `ReleaseVaultHandles` and `ReleaseVaultHandle<T>`, releasing all owned descriptors through `IDataVault.ReleaseBuffer` on teardown and DataVault replacement. Same-vault hot-swap notifications keep live descriptors instead of freeing active buffers.
Rejected Alternatives: Defaulting descriptors without `ReleaseBuffer` was rejected because it hides ownership leaks. Releasing on every `OnDisable` was rejected because disabled components may re-enable during play and should not churn global buffers unless destroyed or the DataVault changes.
Scalability potential: No quality curve change. It improves long-session memory discipline across all device tiers.
Hardware Impact: Cold lifecycle only. Prevents leaked/redundant Vault buffer ownership over long editor/runtime sessions; no steady-frame microsecond claim.

## Loop 32 Decisions: Allocation-Lock Descriptor Adoption

[ANALYSIS]
Target: `EnsureVaultDescriptor` cold acquisition edge in `BuoyancyDisplacementRuntime`.
Affected systems: descriptor acquisition during boot/editor/hot-swap recovery only. Runtime job math, DTO layouts, Vault IDs, quality curves, and force semantics are unchanged.
Zero GC proof: no managed allocation or native allocation site was added. The new branch uses `TryGetGenerationHandle` and `TryResolveHandle` only when `IDataVault.IsAllocationLocked` is true.
State check: status/rationale were read before edits. Static scans remained clean for legacy handles, obsolete resolve bridges, handle `.IsCreated`, native allocation, random, `foreach`, `Pack=`, and hot string formatting. CPU sampled 85.2%, so no build/rebuild was launched.
Rule quote: allocation fences must not be crossed to satisfy a convenience reacquire.

Problem: After the descriptor migration, `EnsureVaultDescriptor` would reacquire through `GetGenerationHandle` whenever an existing descriptor was missing, stale, or undersized. That is acceptable during cold boot, but it is not acceptable when the Vault reports `IsAllocationLocked`.
Solution: Added an allocation-lock branch. If the Vault is locked, the runtime may only adopt an already-existing descriptor through `TryGetGenerationHandle`, resolve it through `TryResolveHandle`, and verify capacity. If that route fails, acquisition fails without allocating or growing.
Rejected Alternatives: Trusting comments that the path is cold was rejected because hot-swap and editor tooling can run during memory fences. Forcing `GetGenerationHandle` through the lock was rejected because it can violate compaction/AUP shift discipline.
Scalability potential: No visual or quality curve change. Low/Middle/High/Ultra all preserve the same math; the change protects memory timing boundaries across device classes.
Hardware Impact: Cold/fault path only. It prevents allocation-lock stalls and illegal growth attempts under memory pressure; measured microseconds remain PENDING VERIFICATION.

## Loop 33 Decisions: Runtime Vault Recovery / Allocation-Lock Mutator Fence

[ANALYSIS]
Target: SHINOBU-owned `BuoyancyDisplacementRuntime` lifecycle after the Vault generation descriptor migration.
Affected systems: cold boot retry, fixed-tick readiness, DataVault service replacement, emergency mock seeding, editor SIMD benchmark generation, and cold CSV hydration. DTO layouts, Burst job math, quality curves, BufferIDs, force packet ABI, asmdefs, and core Vault code are unchanged.
Zero GC proof: no managed collection, LINQ, gameplay file IO, private persistent NativeArray, or per-frame allocation path was added. The fixed-tick recovery path resolves or reacquires Vault generation descriptors through existing owner-local helpers and refuses reacquisition while `IDataVault.IsAllocationLocked` is true.
State check: status/rationale were read before edits. Static scans found no legacy `VaultBufferHandle`, obsolete `.Resolve`, native allocation, random, `foreach`, `Pack=`, hot string formatting, or binary hardware switch in owned paths. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: a registered runtime that cannot ever leave cold boot is not a safe failure mode; it is silent data starvation.

Problem: `OnEnable` registered fixed/post/late ticks even if `EnsureColdBooted()` returned false under a Vault allocation lock. After that, `FixedTick` checked `HandlesReady()` and returned forever when descriptors were missing, creating an inert solver with no telemetry proof.
Solution: Added `TryPrepareRuntimeVault(out IDataVault vault)`. It refreshes the Vault dependency, waits while allocation is locked, retries cold boot after the lock clears, and validates handle shape before scheduling jobs.
Rejected Alternatives: Delaying registration was rejected because dispatcher registration is not the source of the memory defect and would hide the real lifecycle problem. Calling `GetGenerationHandle` directly in `FixedTick` was rejected because descriptor acquisition must remain centralized in `EnsureVaultDescriptor` with the allocation-lock branch.
Scalability potential: Low/Middle/High/Ultra still use the same continuous `GlobalQualityWeight`; this change prevents all tiers from silently losing the buoyancy solver after an allocation-lock race.
Hardware Impact: Cold/recovery branch only. Expected gain is correctness and removal of silent stall risk, not measured frame-time speed.

Problem: A stale generation descriptor can pass `HandlesReady()` because that method validates descriptor shape, not the current Vault generation. `TryResolveRuntimeBuffers` then fails and the old path returned without reacquiring.
Solution: Added `TryRecoverRuntimeVaultDescriptors(ref IDataVault vault)` and a second resolve attempt. Recovery runs only when the Vault is not allocation-locked, then calls `EnsureVaultBuffers()` so stale or missing descriptors are reacquired through the existing generation-aware helper.
Rejected Alternatives: Making `HandlesReady()` resolve all 22 descriptors every frame was rejected because it would add repeated Vault metadata work to the steady-state hot path. Clearing cold buffers during descriptor recovery was rejected because stale descriptor repair must not erase live gameplay state.
Scalability potential: No visual curve change. The path keeps the solver alive across Vault generation churn on weak and high-end devices without a binary tier fork.
Hardware Impact: Steady state remains one handle-shape gate plus the existing phase-local resolve. Recovery cost is paid only on stale/missing descriptors; microseconds remain PENDING VERIFICATION.

Problem: Cold/manual mutators could call `EnsureVaultBuffers()` while allocation was locked. Loop 32 prevented creation/growth in that state, but an existing-descriptor adoption could still be followed by cold writes, CSV hydration, or benchmark scheduling during the maintenance window.
Solution: `EnsureColdBooted`, DataVault service replacement, emergency mock seeding, editor SIMD benchmark generation, material CSV hydration, and SIMD tolerance CSV hydration now return while `IsAllocationLocked` is true.
Rejected Alternatives: Allowing read-only descriptor adoption and then relying on call-site intent was rejected because these entry points are mutators, not pure readers. Locking every buffer inside the cold boot path was rejected for this loop because the correct behavior during a global allocation fence is to wait, not to create a competing cold write.
Scalability potential: No quality curve change. The patch protects all device classes from cold mutation during memory pressure or AUP-shift fences.
Hardware Impact: Prevents lock-fence stalls/corruption risk. No measured microsecond savings claimed.

## Loop 34 Decisions: Existing Descriptor First Reacquire

[ANALYSIS]
Target: SHINOBU-owned `EnsureVaultDescriptor<T>` in `BuoyancyDisplacementRuntime`.
Affected systems: stale/missing descriptor recovery and editor/cold descriptor adoption. Fixed-tick Burst jobs, DTO layouts, BufferIDs, force packet ABI, quality curves, and core Vault implementation are unchanged.
Zero GC proof: no managed allocation, native allocation site, LINQ, `foreach`, string formatting, or private persistent NativeArray was added. The helper uses only `IDataVault.TryGetGenerationHandle`, `IDataVault.TryResolveHandle`, and the existing `GetGenerationHandle` fallback for absent/undersized buffers.
State check: status/rationale and the corrected SHINOBU_201 prompt extraction were read before edits. Prompt extraction reports 20 tasks. Static source scan found no legacy handles, obsolete `.Resolve`, native allocation, random, `Pack=`, hot string formatting, or binary hardware switch in owned source. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: descriptor repair must be a metadata adoption when the buffer already exists; growth is the last path, not the first path.

Problem: After a stale local generation handle failed to resolve, `EnsureVaultDescriptor` went straight to `GetGenerationHandle` whenever the Vault was not allocation-locked. That call can enter the heavier ensure/sanitize path even if the Vault already has a valid descriptor for the same BufferID and length.
Solution: Added `TryAdoptExistingVaultDescriptor`. It first asks the Vault for the current generation descriptor, resolves it, and verifies capacity. Only if that fails and the Vault is unlocked does the helper call `GetGenerationHandle` for create/grow fallback.
Rejected Alternatives: Leaving the unlocked path as-is was rejected because stale descriptor recovery can happen after defrag/generation churn and should not pay create/grow/sanitize overhead when metadata adoption is enough. Resolving all 22 descriptors in `HandlesReady` was rejected because it would move metadata work into the steady-state frame gate.
Scalability potential: No quality curve change. Low/Middle/High/Ultra all keep the same continuous `GlobalQualityWeight`; the change reduces recovery overhead across device tiers without a binary fork.
Hardware Impact: Expected gain is lower cold/editor/stale descriptor repair cost and fewer unnecessary payload sanitation passes. Measured microseconds remain PENDING VERIFICATION.

## Loop 35 Decisions: Spatial Query Lane-4 SIMD Kernel

[ANALYSIS]
Target: SHINOBU-owned reusable spatial query kernel in `BuoyancySimdVectorization.cs`.
Affected systems: kernel library only. Existing lane-1 `VectorizedSpatialQueryJob`, runtime scheduling, AI ownership, Vault IDs, DTO layouts, telemetry ABI, and fixed-tick buoyancy force semantics are unchanged.
Zero GC proof: no managed allocation, native allocation site, LINQ, `foreach`, file IO, service lookup, private persistent `NativeArray`, or hot-loop string work was added. The new job consumes caller-owned `NativeArray<SimdFloat3Padded>` / `NativeArray<int>` lanes and writes only deterministic integer masks.
State check: status/rationale and the SHINOBU_201 prompt were read before edits. Prompt extraction reports 20 tasks. Static scans found balanced braces/preprocessor state, no non-ASCII, no forbidden allocation/random/sqrt/Pack/string parser offenders in the touched source, and clean whitespace. CPU sampled 79.2%, so no build/rebuild was launched.
Rule quote: job parallelism is not proof of SIMD lane packing; Task 07 requires packed prey-position distance tests.

Problem: `VectorizedSpatialQueryJob` processed one prey row per `Execute`, so it relied on scheduler parallelism and Burst auto-vectorization instead of explicitly proving a 4-wide distance-mask lane.
Solution: Added `VectorizedSpatialQueryLane4Job`. It loads four padded prey positions into `float4` x/y/z registers, subtracts one sanitized predator position, computes four squared distances, applies finite and radius masks branchlessly, and writes four integer valid-mask rows.
Rejected Alternatives: Replacing the existing job was rejected because external adopters may already schedule one work item per prey row. A scalar `for` loop inside one `Execute` was rejected because it does not expose packed registers cleanly. Directly wiring the job into predator AI was rejected because SHINOBU_201 does not own AI scheduling or target truth.
Scalability potential: No binary tier switch. The lane job is quality-neutral and can be combined by adopters with continuous radius or update-cadence scaling; Low/Middle/High/Ultra tiers keep the same packed mask math.
Hardware Impact: Expected gain for adopters is four prey distance checks per scheduled lane and fewer scheduler invocations. Measured microseconds remain PENDING VERIFICATION because CPU gate blocked compile/profiler/Burst Inspector proof.

## Loop 36 Decisions: Spatial Query Finite-Mask Parity

[ANALYSIS]
Target: SHINOBU-owned scalar fallback `VectorizedSpatialQueryJob` in `BuoyancySimdVectorization.cs`.
Affected systems: reusable spatial query fallback only. Lane-4 spatial query, hydrodynamics, frustum culling, Vault IDs, DTO layouts, runtime scheduling, AI ownership, and telemetry ABI are unchanged.
Zero GC proof: the change adds only boolean masks over existing local values. No managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, private persistent arrays, or string work was introduced.
State check: status/rationale and SHINOBU prompt extraction were read before edits. Prompt extraction reports 20 tasks. Static source scans show balanced braces/preprocessor/non-ASCII, no forbidden allocation/random/sqrt/Pack/string parser offenders, and clean source whitespace. CPU sampled 100% and a `dotnet` process was present, so no build/rebuild was launched.
Rule quote: NaN vaccination must reject poisoned inputs, not silently teleport them to origin.

Problem: The lane-1 spatial query sanitized non-finite prey and predator positions to `float3.zero`, then validated only the finite derived `distanceSq`. With a positive radius, a poisoned row could become a false valid target at origin.
Solution: Added explicit `preyFinite` and `predatorFinite` masks and carried them into the final branchless validity expression. The scalar fallback now matches the lane-4 finite-mask contract.
Rejected Alternatives: An early `if` reject inside the math body was rejected because the query should remain branchless after structural NativeArray/bounds guards. Relying only on the new lane-4 job was rejected because existing adopters may keep scheduling the lane-1 fallback.
Scalability potential: No quality curve change. Low/Middle/High/Ultra keep identical query semantics; continuous caller-side radius/cadence scaling remains possible without binary hardware switches.
Hardware Impact: Prevents false-positive masks from NaN/Infinity rows and keeps deterministic query output. Measured microseconds remain PENDING VERIFICATION because compile/profiler/Burst Inspector proof was blocked by active `dotnet` and CPU 100%.

## Loop 37 Decisions: Spatial Query Tail-Lane Vaccination

[ANALYSIS]
Target: SHINOBU-owned packed spatial query kernel `VectorizedSpatialQueryLane4Job` in `BuoyancySimdVectorization.cs`.
Affected systems: reusable kernel library only. AI scheduling, runtime buoyancy scheduling, Vault IDs, DTO layouts, telemetry ABI, and scalar fallback public contract are unchanged.
Zero GC proof: the change adds only value-type lane masks, clamped indices, and fixed in-range writes. No managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, private persistent arrays, or string work was introduced.
State check: status/rationale and SHINOBU prompt extraction were read before edits. Prompt extraction reports 20 tasks. Static source scans show balanced braces/preprocessor/non-ASCII, no forbidden allocation/random/non-rsqrt/Pack/string parser offenders, and clean trailing whitespace. CPU sampled 77% with no compiler processes, so no build/rebuild was launched.
Rule quote: a SIMD lane is not valid if its remainder rows retain stale truth from an earlier frame.

Problem: `VectorizedSpatialQueryLane4Job` rounded `Count` down to the previous multiple of four. If an adopter scheduled only this packed job, the final 1-3 rows of `ValidMask` could keep stale values.
Solution: The lane job now supports `ceil(Count / 4)` scheduling. Each lane still owns four logical rows, but tail lanes clamp reads to the final valid row and write only in-range mask slots. The source-adjacent partition comment now states the exact ceil scheduling contract.
Rejected Alternatives: Requiring all adopters to schedule a scalar cleanup pass was rejected because it creates a hidden integration trap. Directly migrating AI owners was rejected because SHINOBU_201 owns reusable SIMD kernels, not AI target truth or scheduling.
Scalability potential: No binary tier switch. Low/Middle/High/Ultra all use the same deterministic masks; owners can still scale radius or cadence continuously with `GlobalQualityWeight`.
Hardware Impact: Preserves four-wide packed distance math while preventing stale tail masks. Expected extra branch cost is limited to at most three tail writes per scheduled query batch. Measured microseconds remain PENDING VERIFICATION.

Problem: The packed lane computed distance registers from raw prey coordinates even when the finite mask was false. The final mask rejected poisoned rows, but NaN/Infinity still entered intermediate ALU lanes.
Solution: Added `safePx/safePy/safePz` vector selects before subtraction. Invalid or out-of-range lanes collapse to zero before squared-distance math and remain invalid through `preyFinite`.
Rejected Alternatives: Relying only on final validity masking was rejected under the NaN vaccination mandate. Early-returning the entire lane when any component was invalid was rejected because one bad prey row must not discard the other three valid rows.
Scalability potential: No visual or quality curve change. This is deterministic safety for all hardware tiers.
Hardware Impact: Adds three packed selects, preventing poisoned SIMD registers and false tail data. Compile/Burst Inspector/profiler proof remains PENDING VERIFICATION.

## Loop 38 Decisions: Spatial Query Lane-4 Tail Store Branch Removal

[ANALYSIS]
Target: `VectorizedSpatialQueryLane4Job` tail writes in `BuoyancySimdVectorization.cs`.
Affected systems: reusable packed spatial query kernel only. Lane-1 fallback, hydrodynamics, frustum culling, runtime scheduling, Vault IDs, DTO layouts, AI ownership, and telemetry ABI are unchanged.
Zero GC proof: the change uses only stack locals and `math.select`. It adds no managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, private persistent arrays, delegates, or strings.
State check: status/rationale, Unity MCP skill constraints, and SHINOBU prompt extraction were read before edits. Prompt extraction reports 20 tasks. Static scans found no tail conditional write remnants, no forbidden hot-path allocation/random/sqrt/Pack/string parser offenders, balanced braces/preprocessor/non-ASCII, and clean source whitespace. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: tail correctness cannot reintroduce scalar branch structure into the packed SIMD query body.

Problem: The lane-4 spatial query tail path from Loop 37 used three conditional stores for lanes 1..3. That avoids out-of-range writes, but it reintroduces branch structure inside the packed query `Execute`.
Solution: Replaced conditional stores with duplicate-safe branchless stores. Tail lanes clamp their index to the last valid row; cascading masks (`mask1`, `mask2`, `mask3`) preserve the last in-range value, so duplicate writes land on the same slot with the same final mask.
Rejected Alternatives: Dropping non-multiple-of-four rows was rejected because future adopters could silently miss targets. Requiring an external scalar tail job was rejected because it creates dependency/scheduling complexity outside SHINOBU ownership. Keeping `if (laneNInRange)` was rejected because the task explicitly prioritizes branchless SIMD math.
Scalability potential: No quality curve change. The lane remains compatible with continuous caller-side radius/cadence scaling for Low/Middle/High/Ultra tiers without binary switches.
Hardware Impact: Removes up to three conditional store branches from the tail lane and keeps all active rows covered. Measured microseconds remain PENDING VERIFICATION because the CPU gate blocked compile/profiler/Burst Inspector proof.

## Loop 39 Decisions: Hydrodynamics Tail-Lane / Telemetry Ring Cursor

[ANALYSIS]
Target: `VectorizedHydrodynamicsLane4Job`, editor/manual SIMD benchmark scheduling, and `RecordSimdTelemetryJob`.
Affected systems: SHINOBU SIMD benchmark and reusable hydrodynamic lane kernel only. Gameplay buoyancy solver force semantics, Vault IDs, DTO byte layouts, AI ownership, frustum culling, spatial query masks, and assembly references are unchanged.
Zero GC proof: the patch adds only stack integer indices, existing `math.min`/`math.select`, and value-type schedule arithmetic. It adds no managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, delegates, strings, private persistent arrays, or binary hardware switch.
State check: status/rationale and SHINOBU prompt extraction state were read before the patch. Scoped forbidden pattern scan returned no matches. `git diff --check` reported only existing LF/CRLF normalization warnings for touched C# files. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: packed SIMD kernels must own their tail rows; a rounded-down benchmark is not proof of a safe public kernel.

Problem: `VectorizedHydrodynamicsLane4Job` floored Count to the previous multiple of four. The current benchmark also rounded Count before scheduling, which hid the defect, but any future adopter passing a non-multiple Count would leave 1-3 velocity and force rows stale.
Solution: The lane kernel now accepts `ceil(Count / 4)` scheduling. Tail lane reads and writes clamp to the last in-range row; because clamped lanes read identical position, velocity, and drag data, duplicate stores write identical final velocity and force values inside the same Execute.
Rejected Alternatives: Keeping caller-side pre-rounding was rejected because it creates a hidden integration trap. Adding a scalar cleanup job was rejected because it introduces another dependency edge and extra schedule overhead. Reverting to one entity per Execute was rejected because it destroys the lane-4 proof for Task 06.
Scalability potential: Low/Middle/High/Ultra still consume the same continuous `GlobalQualityWeight` inside the turbulence and approximation curve. The change is quality-neutral coverage, not a binary tier branch.
Hardware Impact: Expected effect is correctness plus preserved four-wide math for non-multiple counts. No microsecond gain is claimed until Burst Inspector/profiler proof is available.

Problem: The editor/manual X-Ray benchmark generated and measured only a rounded-down `vectorizedCount`, so the benchmark could not expose tail-lane defects and telemetry under-reported entity coverage when capacity was not divisible by four.
Solution: Benchmark generation, scalar probe scaling, hydrodynamics lane scheduling, telemetry entity count, and state hash now use full `count`. The vector job schedules `laneCount = ceil(count / 4)`.
Rejected Alternatives: Leaving benchmark count rounded was rejected because a validation surface must test the public kernel's real scheduling contract. Increasing the Vault capacity to a forced multiple was rejected because capacity shape is not a substitute for kernel correctness.
Scalability potential: The same benchmark tuning DTO and continuous quality weight remain in use across device tiers.
Hardware Impact: Scheduler count increases by at most one lane for non-multiple capacities. Measured microseconds remain PENDING VERIFICATION.

Problem: `RecordSimdTelemetryJob` wrote `TelemetryCursor[0] = cursor + 1`. The read path clamped negative values, but the cursor itself could still overflow into a negative state and was not a strict circular black-box cursor.
Solution: The job now computes `slot = cursor % TelemetryRing.Length`, then stores the next cursor as `slot + 1` wrapped to zero at ring length. Cursor state remains in `[0, TelemetryRing.Length - 1]`.
Rejected Alternatives: Relying on integer overflow plus next-frame `math.max(0, ...)` was rejected because the black-box ring must be deterministic and bounded. Storing an unbounded frame counter separately was rejected because `FrameIndex` already records the simulation frame in each telemetry entry.
Scalability potential: No quality curve change. The telemetry ring behavior is deterministic for all hardware tiers.
Hardware Impact: One once-per-benchmark cursor wrap operation; prevents overflow-state forensic ambiguity. Compile/profiler/Burst Inspector proof remains PENDING VERIFICATION because CPU remained above the build gate.

## Loop 40 Decisions: Frustum Cull Lane-8 SIMD Kernel

[ANALYSIS]
Target: `VectorizedFrustumCullLane8Job` in `BuoyancySimdVectorization.cs`.
Affected systems: reusable culling kernel library only. Existing lane-1 `VectorizedFrustumCullJob`, renderer ownership, Vault IDs, DTO byte layouts, spatial query masks, hydrodynamics, telemetry ABI, and assembly references are unchanged.
Zero GC proof: the kernel uses only caller-owned `NativeArray<SimdFloat3Padded>`, `NativeArray<float4>`, `NativeArray<int>`, stack `float4`/`bool4` locals, and `math.select`/`math.step`. No managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, delegate, string work, or private persistent array was added.
State check: status/rationale/XML/AGENTS/Unity skill were read before edits. Static scans found balanced braces/preprocessor state, zero non-ASCII in the touched source, no forbidden hot-path allocation/random/sqrt/Pack/property/parser patterns, no lane-tail conditional store remnants, and a source diff check with only repository LF/CRLF normalization warning. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: Task 08 requires eight AABBs per packed cull lane; one object per `Execute` is scheduler parallelism, not the requested SIMD cull surface.

Problem: `VectorizedFrustumCullJob` evaluates one center/extents row per `Execute`. That preserves the fallback contract, but it does not satisfy the XML requirement to process eight AABBs simultaneously for frustum culling.
Solution: Added `VectorizedFrustumCullLane8Job`. It loads eight centers/extents as two `float4` groups, evaluates up to six packed frustum planes branchlessly with projected-radius AABB tests, finite-gates centers/extents/planes, and writes visible indices through duplicate-safe tail masks.
Rejected Alternatives: Replacing the lane-1 fallback was rejected because external owners may already schedule per-object culls. A scalar `for` loop inside one Execute was rejected because it hides the lane shape from Burst. Direct renderer/BRG integration was rejected because SHINOBU_201 owns reusable SIMD kernels, not render scheduling or culling truth.
Scalability potential: No binary tier fork. Low/Middle/High/Ultra can feed the same lane-8 kernel while owner systems continuously scale candidate count, culling cadence, or render radius by `GlobalQualityWeight`; high tiers can spend the saved CPU on denser visual instance lists.
Hardware Impact: Expected adopter-side gain is up to eight AABB cull tests per scheduled lane and seven fewer scheduler invocations per eight objects. Measured microseconds remain PENDING VERIFICATION until Unity import, Burst Inspector, and profiler proof are available.

Problem: Lane-8 output writes do not map one-to-one with the `IJobParallelFor` index and tails can duplicate the final row.
Solution: Marked `VisibleIndexMask` with `[NativeDisableParallelForRestriction]` and documented the exact invariant: scheduled lane `i` owns rows `[i * 8, i * 8 + 7]`; tail rows clamp to the final in-range row and cascading `math.select` masks ensure duplicate stores write the same final value.
Rejected Alternatives: Keeping only `VisibleIndexMask[index]` writes was rejected because that would force one-object cull execution. Requiring a scalar tail cleanup pass was rejected because it creates hidden integration work for external owners.
Scalability potential: The write partition is independent of hardware tier and remains compatible with continuous candidate-count scaling.
Hardware Impact: Preserves packed write shape without extra schedule work. Compile/profiler proof remains PENDING VERIFICATION.

## Loop 41 Decisions: Frustum Plane NaN Vaccination

[ANALYSIS]
Target: `VectorizedFrustumCullJob` and `VectorizedFrustumCullLane8Job` plane evaluation loops.
Affected systems: reusable culling kernels only. Renderer ownership, hydrodynamics, spatial query, Vault IDs, DTO layout, telemetry ABI, and editor facade are unchanged.
Zero GC proof: the patch adds only stack `rawPlane`, `finitePlaneMask`, and sanitized `float4 plane` locals. No managed allocation, native allocation, LINQ, `foreach`, service lookup, file IO, delegate, string work, or private persistent array was added.
State check: status/rationale and SHINOBU prompt state were read before edits. Touched snippets were inspected. Forbidden hot-path pattern scan returned no matches. `math.select(float4, float4, bool)` is present in the installed Unity.Mathematics package. CPU briefly sampled below the gate, but the immediate pre-build retry sampled 100%, so no build/rebuild was launched.
Rule quote: NaN vaccination must happen before the ALU lane is poisoned, not after the result is masked.

Problem: The lane-1 and lane-8 frustum cull paths loaded plane coefficients, computed projected radius and signed distance, and only then converted invalid planes to an invisible result. Non-finite planes could still enter dot-product lanes before the final mask.
Solution: Both paths now read `rawPlane`, compute `finitePlaneMask`, select invalid planes to `float4.zero`, and use the sanitized plane for all projected-radius and signed-distance math. Active invalid planes still invalidate visibility through `finitePlane = 0`.
Rejected Alternatives: Relying on post-dot `finitePlane` masking was rejected because poisoned intermediate ALU lanes violate the NaN-vaccination mandate. Early-returning on one bad plane was rejected because one corrupt plane slot should not skip the structural write path or leave stale masks.
Scalability potential: No quality curve change. Low/Middle/High/Ultra all use the same sanitized plane path; owners can still continuously scale candidate count or culling cadence by `GlobalQualityWeight`.
Hardware Impact: Adds one finite mask and one select per active plane loop. Expected gain is stability and deterministic mask output, not speed. Compile/profiler/Burst Inspector proof remains PENDING VERIFICATION.

## Loop 42 Decisions: ParallelFor Safety Justification Expansion

[ANALYSIS]
Target: source-adjacent safety comments for `VectorizedHydrodynamicsLane4Job`, `VectorizedSpatialQueryLane4Job`, and `VectorizedFrustumCullLane8Job`.
Affected systems: comments on SHINOBU-owned reusable SIMD kernels only. Runtime math, Vault IDs, DTO layout, telemetry ABI, editor facade, renderer ownership, AI ownership, and buoyancy force semantics are unchanged.
Zero GC proof: comment-only source change. No managed allocation, native allocation, service lookup, file IO, delegate, string operation, private persistent array, or new job field was introduced.
State check: status/rationale, AGENTS, native memory mandate, ARM64 layout mandate, and the SHINOBU_201 prompt were read before edits. Relevant mandates: Native Memory/Job System Protocol, ARM64 Runtime Struct Layout, Zero-GC hot-path law, Global Authority boundary, and branchless SIMD task text. Post-edit scans found all four safety fields covered by paragraph markers, balanced braces/preprocessor/non-ASCII in source, no forbidden hot-path pattern matches, and only repository LF/CRLF normalization warnings from diff check. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: any disabled native safety check must carry source-local proof of why the safety warning is false positive, which alternatives were rejected, and which invariant preserves correctness.

Problem: The lane-packed jobs use `[NativeDisableParallelForRestriction]` because one scheduled lane writes four or eight contiguous rows. Existing comments stated the partition but did not provide the mandate's three-paragraph review proof. A reviewer would have to reconstruct safety from logs instead of the source file.
Solution: Expanded source comments above `Velocities`, `OutputForces`, `ValidMask`, and `VisibleIndexMask` into explicit `SAFETY_JUSTIFICATION_PARAGRAPH_1/2/3` blocks. Each block names the suppressed Unity ParallelFor index assumption, explains why the job partition makes it a false positive, lists rejected alternatives, and states the exact `ceil(Count / laneWidth)` ownership invariant.
Rejected Alternatives: Removing the attribute was rejected because Unity safety expects `array[laneIndex]` writes and would flag legal packed range writes. Reverting to one entity/AABB/prey per Execute was rejected because it destroys Tasks 06, 07, and 08 lane-width proof. Adding scalar tail cleanup jobs was rejected because it creates hidden owner scheduling obligations and extra dependency edges.
Scalability potential: No quality curve change. Low/Middle/High/Ultra keep the same continuous candidate/count/cadence scaling and same lane kernels; this loop only makes the safety proof auditable.
Hardware Impact: Runtime microseconds unchanged by comments. The practical impact is review and CI safety: it prevents a source-level mandate rejection around packed writes while preserving the SIMD lane shape required for AVX2/NEON proof. Compile/profiler/Burst Inspector proof remains PENDING VERIFICATION.

## Loop 43 Decisions: Hydrodynamic Approximation Gate Branch Removal

[ANALYSIS]
Target: approximation-weight validity gates in `VectorizedHydrodynamicsJob`, `VectorizedHydrodynamicsLane4Job`, and `ScalarHydrodynamicsReferenceJob`.
Affected systems: SHINOBU hydrodynamic SIMD kernels and scalar benchmark reference only. Vault IDs, DTO layout, telemetry ABI, editor facade, culling, spatial query, and gameplay owner routes are unchanged.
Zero GC proof: the patch changes only `&&` to `&` in value-type boolean predicate construction. No managed allocation, native allocation, service lookup, file IO, delegate, string operation, private persistent array, or new job field was introduced.
State check: status/rationale were read before the user-facing update and before edits. The SHINOBU_201 XML prompt was re-extracted and still reports 20 tasks. Static scans found no remaining hydrodynamic approximation `&&` match, balanced source braces/preprocessor/non-ASCII, no forbidden hot-path patterns, and only repository LF/CRLF normalization warnings from diff check. CPU sampled above the 50% gate and the final retry reported 100%, so no build/rebuild was launched.
Rule quote: branchless SIMD setup should evaluate predicate lanes as value math and feed `math.select`; short-circuit scalar gates are not acceptable where both sides are finite-safe.

Problem: `hasApproximationWeight` used C# `&&` in three hydrodynamic math paths. Even though it is a scalar setup predicate, `&&` permits short-circuit lowering and can introduce a branch-shaped control gate immediately before approximation selection.
Solution: Replaced `&&` with non-short-circuit `&` for the finite and epsilon predicates. Both predicates are safe to evaluate independently because they read the same scalar tuning value and do not index memory or invoke side effects.
Rejected Alternatives: Removing the finite predicate was rejected because invalid authored tuning must fall back to `GlobalQualityWeight`. Keeping `&&` was rejected because it weakens the branchless-math proof. Moving CSV/tolerance table reads into the hot job was rejected because it would add memory pressure and owner coupling.
Scalability potential: No binary tier change. Low still collapses toward cheaper polynomial behavior through continuous quality; Middle/High/Ultra still blend toward higher approximation fidelity through the same scalar.
Hardware Impact: Expected benefit is small but precise: one branch-shaped short-circuit gate is removed from each hydrodynamic setup path. Exact AVX2/NEON instruction proof remains PENDING VERIFICATION until Burst Inspector can run.

## Loop 44 Decisions: Gameplay Telemetry Cursor Ring Fence

[ANALYSIS]
Target: `ReduceBuoyancyTelemetryJob` black-box telemetry cursor in `BuoyancyDisplacementJobs.cs`.
Affected systems: gameplay buoyancy telemetry ring only. SIMD telemetry, hydrodynamics integration, force packet compaction, Vault IDs, DTO layout, editor facade, spatial query, and frustum culling are unchanged.
Zero GC proof: the patch adds only stack integer arithmetic and `math.select` inside an existing Burst job. No managed allocation, native allocation, service lookup, file IO, delegate, string operation, private persistent array, or new job field was introduced.
State check: status/rationale were read before the user-facing update and before edits. The SHINOBU_201 XML prompt was re-extracted and still reports 20 tasks. Static scans found no remaining unbounded `TelemetryCursor[0] = cursor + 1`, no forbidden hot-path patterns, clean trailing whitespace, and only repository LF/CRLF normalization warnings from diff check. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: black-box forensic rings must remain bounded after endurance runs; modulo-at-read is not a substitute for bounded persisted cursor state.

Problem: `ReduceBuoyancyTelemetryJob` used `TelemetryCursor[0] = cursor + 1`. The write slot was modulo-bounded, but the persisted cursor could grow until signed integer overflow, then be clamped back to zero by the next read path. That creates a non-forensic transient state in a 100-hour endurance run.
Solution: Mirrored the SIMD telemetry cursor rule. The job now computes `nextCursor = slot + 1` and stores zero when `nextCursor >= TelemetryRing.Length`; the cursor buffer always remains inside `[0, TelemetryRing.Length - 1]`.
Rejected Alternatives: Relying on overflow plus `math.max(0, TelemetryCursor[0])` was rejected because a telemetry cursor is itself forensic state. Adding a separate unbounded frame counter was rejected because `FrameIndex` already exists in every telemetry entry. Clearing the cursor from the manager was rejected because the job can enforce the invariant locally.
Scalability potential: No quality curve change. Low/Middle/High/Ultra all use the same bounded black-box ring; continuous `GlobalQualityWeight` remains recorded per entry without binary tier behavior.
Hardware Impact: Adds one integer increment and one `math.select` per telemetry write. This is not a speed change; it prevents cursor overflow ambiguity and keeps dump reconstruction deterministic. Compile/profiler/Burst Inspector proof remains PENDING VERIFICATION because CPU stayed above the build gate.

## Loop 45 Decisions: Evaluate Tuning Snapshot De-Aliasing

[ANALYSIS]
Target: `EvaluateBuoyancyJob` tuning input in `BuoyancyDisplacementJobs.cs` and its scheduler assignment in `BuoyancyDisplacementRuntime.cs`.
Affected systems: gameplay buoyancy evaluator scheduling only. Tuning still originates from the Vault owner in `FixedTick`; hydrodynamics SIMD benchmark, force packet compaction, telemetry ABI, DTO layout, editor facade, spatial query, and culling kernels are unchanged.
Zero GC proof: the patch replaces a `NativeArray<BuoyancyTuningDTO>` job field with a blittable DTO value field and removes one helper method. No managed allocation, native allocation, service lookup, file IO, delegate, string operation, private persistent array, or new Vault buffer was introduced.
State check: status/rationale were read before the user-facing update and before edits. Static scans found no remaining `ResolveTuning()` or evaluator `NativeArray<BuoyancyTuningDTO> Tuning`, no forbidden hot-path patterns, balanced braces in touched source, and only repository LF/CRLF normalization warnings from diff check. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: a fact has one owner, but a scheduled Burst job should consume the sanitized snapshot directly instead of re-reading a one-element NativeArray per row.

Problem: `EvaluateBuoyancyJob` resolved tuning through a `NativeArray<BuoyancyTuningDTO>` on every `Execute`. `FixedTick` already reads, sanitizes, updates, and writes `tuning[0]` before scheduling, so the per-row `ResolveTuning()` branch and NativeArray alias field were redundant.
Solution: Changed the job field to `public BuoyancyTuningDTO Tuning` and passed the sanitized `tuningDto` from runtime. The hot Execute path now uses `BuoyancyTuningDTO tuning = Tuning;` with no NativeArray metadata read and no fallback branch.
Rejected Alternatives: Keeping the NativeArray field was rejected because it leaves a needless alias candidate and branch-shaped helper in the evaluator. Adding a copied tuning Vault buffer was rejected because it creates another owner route. Reading Homeostasis or GlobalRegistry inside the job was rejected because Burst jobs must stay stateless and registry-free.
Scalability potential: No binary tier fork. The same continuous `GlobalQualityWeight`, stride, and flow quality curves are captured in the DTO snapshot for Low/Middle/High/Ultra, while lower tiers still reduce evaluated rows through continuous stride math.
Hardware Impact: Expected static benefit is one fewer NativeArray field in the evaluator, no per-row tuning array metadata read, and removal of one branch-shaped fallback helper per evaluated row. Exact AVX2/NEON and microsecond proof remains PENDING VERIFICATION until Burst Inspector/profiler can run.

## Loop 46 Decisions: Buoyancy ParallelFor Safety Proof Tightening

[ANALYSIS]
Target: `[NativeDisableParallelForRestriction]` fields in `GenerateMockBuoyantObjectsJob` and `EvaluateBuoyancyJob`.
Affected systems: gameplay buoyancy mock seeding, strided buoyancy evaluation state writes, and debug-force black-box rows only. Hydrodynamic SIMD kernels, spatial query kernels, frustum culling, Vault IDs, DTO layouts, force packet ABI, editor facade, and assembly references are unchanged.
Zero GC proof: the patch changes one attribute to add `[WriteOnly]` and expands comments. It adds no managed allocation, native allocation, private persistent array, service lookup, file IO, delegate, string operation, LINQ, `foreach`, or new job field.
State check: status/rationale/root AGENTS, the Unity MCP workflow skill, current SHINOBU prompt count, and the binary payload ledger were read before the edit. Static scans found all three gameplay safety fields covered by paragraph markers, balanced braces/preprocessor/non-ASCII in the source, no forbidden hot-path patterns, and only repository LF/CRLF normalization warnings from diff check. CPU sampled 100%, so no build/rebuild was launched.
Rule quote: disabled ParallelFor safety requires source-local proof of the false-positive check, rejected alternatives, and a closed write-partition invariant.

Problem: `GenerateMockBuoyantObjectsJob.States`, `EvaluateBuoyancyJob.States`, and `EvaluateBuoyancyJob.DebugForces` disabled Unity's ParallelFor index restriction, but their comments were shorthand. The actual code is valid because mock seeding writes `States[index]` and the evaluator maps each scheduled `workIndex` through a fixed stride/offset row function, but a reviewer had to infer that from implementation detail.
Solution: Expanded the comments into explicit three-paragraph safety blocks. `GenerateMockBuoyantObjectsJob.States` is now `[WriteOnly, NativeDisableParallelForRestriction, NoAlias]` because the seed job writes a fresh DTO row and never reads prior state. The evaluator fields now document the injective mapping `index = workIndex * max(1, stride) + fixed offset`, the active-count bounds, and the dependency requirement that reduction/telemetry waits for the evaluator handle.
Rejected Alternatives: Removing the unsafe pointer write was rejected because NativeArray indexer mutation can reintroduce copy debt on the 64-byte DTO. Dense precompaction was rejected because it duplicates the state walk and requires another Vault buffer. Debug post-remap was rejected because it doubles debug writes and weakens black-box row identity. A scalar fallback for skipped cadence rows was rejected because it creates hidden scheduling outside SHINOBU ownership.
Scalability potential: No binary tier fork. Low can continuously increase `EvaluationStride` or cadence spacing to reduce active rows; Middle/High/Ultra can lower stride and raise active rows while preserving the same fixed mapping and black-box row identity. The safety proof keeps the math-lod path auditable without introducing a low/high branch.
Hardware Impact: Runtime ALU is unchanged except that the mock seed state lane now truthfully exposes write-only access to Burst. Expected gain is review/compiler alias clarity, not measured frame time. Compile/profiler/Burst Inspector proof remains PENDING VERIFICATION because the CPU gate blocked a build.
