# SHINOBU_301 Rationale

Status: PENDING VERIFICATION / STATIC IMPLEMENTED / TASK15 TIMING PARTIAL / BUILD GUARDED

## Session Start

Problem: Existing proximity path is not yet mapped; task demands eradication of O(N^2) proximity and `Physics.OverlapSphere` gameplay scans without compile-wall guessing.
Solution: Start with mandate read, source archaeology, and owner discovery before any runtime code.
Rejected Alternatives: Creating a standalone `HectonSpatialHashManager`; prompt explicitly requires partial integration if an existing broadphase owner exists.
Scalability potential: Low uses larger cells and capped query results; Middle uses balanced cell sizing; High increases neighborhood depth; Ultra spends saved CPU on visual-only density/debug overlays, not gameplay truth.
Hardware Impact: Expected gain on i3/MX350 depends on replaced call sites; no measured microseconds yet. Estimate remains 0 us until source and profiler evidence exist.

## Loop 1 Archaeology

Problem: The prompt targeted `Physics.OverlapSphere`, but source evidence showed no allocating `Physics.OverlapSphere`, `Physics.OverlapBox`, or `Physics.SphereCast` in first-party scripts. The active SHINOBU proximity cost is the linked-bucket chain inside `ShinobuEcosystemBalancer`.
Solution: Treat `ShinobuEcosystemBalancer` Vault arrays as the owner route and replace AI neighbor lookup with a flat sorted `SpatialGridEntryDTO` path. Keep unrelated `OverlapSphereNonAlloc` systems outside this domain unchanged.
Rejected Alternatives: Deleting nonalloc physics components; creating a new standalone spatial manager; adding a new overflow signal lane.
Scalability potential: Low = larger cells, lower probe count, capped neighbor results. Middle = normal cell size and query caps. High = smaller cells and more cell probes. Ultra = richer editor/debug visualization and longer query caps without changing save/net authority.
Hardware Impact: Static estimate only. Replacing linked chain traversal removes per-neighbor pointer-style chasing through `BucketNext`; expected low-end gain is proportional to dense-cell pressure, but measured microseconds remain PENDING VERIFICATION.

## Loop 2 Spatial Grid Kernel

Problem: SHINOBU neighbor lookup used a linked bucket chain that is cache-hostile under dense fauna, and the user required a 16-byte `SpatialGridEntryDTO` plus 64-bit AUP quantization.
Solution: Added `SpatialGridEntryDTO` with explicit offsets 0/4/8 and size 16, `QuantizeEntityCoordinatesJob`, eight-pass full-fingerprint radix `SortSpatialGridJob`, open-addressed `SpatialGridBucketRangeDTO`, and `SpatialHashQuery`. Cell indices are computed from `double3` AUP with deterministic epsilon before any local float downcast.
Rejected Alternatives: `NativeParallelMultiHashMap` was rejected as the primary route because hash-map indirection weakens contiguous bucket scanning and makes designer x-ray output less direct. Managed dictionaries/lists and physics broadphase calls were rejected by mandate.
Scalability potential: Low uses larger cells, lower probe count, and lower result cap. Middle uses default 10m cells. High uses smaller cells and more local probes. Ultra spends saved cycles on denser visual flocking/debug display, not authority changes.
Hardware Impact: Static estimate is 500-2500 us saved in dense 100k-swarm proximity frames versus linked traversal or physics broadphase calls. No measured microseconds because build/profiler execution was blocked.

## Loop 3 Vault And Authority Route

Problem: The grid must not become a second authority owner or rollback payload.
Solution: All spatial entries, scratch, ranges, telemetry, tuning, profiles, and CSV scratch are GlobalDataVault buffers under `SystemID.AIEcology`. Entries/sort scratch use `NativeArrayOptions.UninitializedMemory`; range table uses frame-epoch flags to avoid hot clears. Consumers read snapshots; no hot `GlobalRegistry` polling was added.
Rejected Alternatives: Private persistent `NativeArray` fields were rejected as H-Phi violations. Adding grid DTOs to save/Merkle state was rejected because the grid is an ephemeral rebuildable acceleration structure.
Scalability potential: Weak devices shrink query depth, cadence, and optional debug work through continuous quality/stress math. Active entity truth count stays capacity-bound so quality does not change gameplay authority. High/Ultra increase probes/results without changing DTO layout, save identity, or authority route.
Hardware Impact: Uninitialized entry/scratch allocation avoids zero-fill of immediately overwritten 16-byte rows. Static low-end estimate: 50-200 us avoided during cold allocation/reallocation events; 0 measured.

## Loop 4 Editor And Proof Artifacts

Problem: Designers need live tuning and architects need proof that allocating OOP proximity calls did not remain in AI/Interaction runtime code.
Solution: Added `Spatial Grid X-Ray` UI Toolkit window, SceneView live bucket wire cubes, cold CSV parser for `spatial_grid_profiles.csv`, and `OOP_Query_Scanner` with `Docs/Reports/AI_OPTIMIZATION_REPORT.json`. Scanner excludes editor scanner source and NonAlloc suffix false positives.
Rejected Alternatives: Chat-only proof, manual inspector edits, and managed runtime CSV parsing were rejected.
Scalability potential: Low-tier settings can be authored by CSV without recompilation; Ultra-tier can increase cell resolution and debug density visualization through the same DTOs.
Hardware Impact: Editor-only UI has no player hot-path cost. CSV parse is cold and bounded by 8192 bytes.

## Loop 5 Self-Audit And Compile Guard

Problem: Concurrent edits changed `BoidStateDTO` to local float state while another render patch still referenced `boidState.AUP`, creating a compile-wall risk.
Solution: Replaced the stale render genome input with `ShinobuEcosystemBalancer.ToAbsoluteDouble3(in meta.PositionAup)` and changed `SpatialHashQuery` to read `AmbientEntityAupDTO` snapshots, preserving the authoritative AUP route.
Rejected Alternatives: Re-adding AUP to `BoidStateDTO` was rejected because it would widen a hot 32-byte local-state DTO and conflict with another agent's ownership. Reverting the genome feature was rejected because it was unrelated work by another agent.
Scalability potential: DTO size remains stable; AUP remains in the existing 64-byte metadata lane. Low/Middle/High/Ultra behavior stays controlled by continuous grid tuning.
Hardware Impact: Avoids adding 24 bytes of double3 to every `BoidStateDTO`; at 100k entities this avoids roughly 2.4 MB of extra hot local-state traversal. Measured compile/runtime proof blocked by existing `dotnet` PID 6776; CPU guard was 60% on first check and 21% on final check.

## Known Limitation

Problem: Task 15 demanded exact per-job Burst execution times for quantize and sort.
Solution: Implemented the 300-frame ring, occupancy, query upper-bound, invalid-input count, state hash, overflow flag, and dump writer. `QuantizeMicroseconds` and `SortMicroseconds` now use `-1` sentinel plus `TelemetryFlagTimingUnavailable` until a non-blocking dispatcher/profiler timing lane exists. Removed the dead `SortMicroseconds > 1000f` dump condition because the field is not authoritative yet; overflow/invalid-input dumps remain active.
Rejected Alternatives: Blocking `.Complete()` around quantize/sort in the gameplay phase was rejected. Writing fake Stopwatch-derived split timings was rejected because it would be false telemetry. Keeping a sort-time fault condition on a zero field was rejected because it creates false confidence.
Scalability potential: The forensic buffer is ready for exact timing fields once a dispatcher-owned non-blocking measurement source exists.
Hardware Impact: No fake performance claim. Exact microseconds saved remain 0 measured; static estimates are recorded in status only.

## Loop 6 Post-Compaction Audit

Problem: The first Dear Lie limiter bounded accepted neighbors, not candidates scanned. Dense cells with many out-of-radius or inactive entries could still force long bucket walks. The editor tuning path also used `SystemID.AIEcology` as a diagnostics writer tag, and the shared AI report had been overwritten by other agents.
Solution: Added public `SpatialHashQuery.maxEvaluated` and flocking `entryScans` hard caps so the upper bound applies to candidate reads. Changed `SpatialGridXRayWindow` tuning mutation lock owner to `SystemID.CoreDiagnostics`. Added `SHINOBU_301_AI_OPTIMIZATION_REPORT.json` stable proof and non-destructive `shinobu301SpatialHash` section in the shared report.
Rejected Alternatives: Counting only accepted neighbors was rejected because sparse/out-of-radius candidates still burn cache and ALU. Overwriting `Docs/Reports/AI_OPTIMIZATION_REPORT.json` was rejected because multiple agents share that artifact. Using `SystemID.AIEcology` for editor writes was rejected because diagnostics should not masquerade as the runtime owner.
Scalability potential: Low weight evaluates fewer candidates and larger cells; Middle keeps bounded local perception; High/Ultra can spend more capped candidates without changing DTO layout, authority route, or rollback truth.
Hardware Impact: Worst-case dense-cell scan is now bounded by candidate budget instead of bucket occupancy. Static low-end protection: prevents multi-ms pathological walks on i3/MX350 class CPUs. Measured proof remains blocked by CPU 62% and active `dotnet` PIDs 3104/13384 under the build guard.

## Loop 7 Telemetry Truth And Center-First Query

Problem: The bounded traversal still used lexicographic neighbor-cell order. Under a low probe/candidate budget, the scan could spend its budget in a corner cell before checking the entity's own cell. Task 15 also still implied usable sort/quantize timings via zero fields, and query count was not tied to actual flocking query execution.
Solution: Public `SpatialHashQuery` and Burst `BoidFlockingJob.QueryNeighbors` now process the center cell first, then scan outer cells under the same candidate budget. Spatial telemetry writes `-1` for unavailable split timings and sets `TelemetryFlagTimingUnavailable`. Flocking increments a 64-byte padded counter at actual query execution; post-job telemetry patches `SpatialGridTelemetryEntry.QueryCount` from `FlockingCounterSpatialGridQueries` and marks `TelemetryFlagQueryCountPatched`.
Rejected Alternatives: Keeping corner-first traversal was rejected because low-tier quality would randomly miss the most relevant local cell. Using zero timing values was rejected as fake precision. Counting potential entities as queries was rejected because it inflated telemetry without proving AI consumers actually queried the grid.
Scalability potential: Low/MX350 sees its tiny query budget spent on the most local truth first, then progressively wider shells as `GlobalQualityWeight` increases. Middle keeps bounded local awareness. High/Ultra spend the same continuous curve on deeper outer-cell sampling and richer x-ray visualization without changing DTO layout or save authority.
Hardware Impact: No measured runtime proof. Static protection improves low-end behavior by avoiding wasted candidate budget on far corner cells and by preventing misleading telemetry-driven tuning decisions. Build remains blocked by active `dotnet` PIDs 6880/14060 even though CPU sample was 18%.

## Loop 8 Explorer Audit Corrections

Problem: Center-first traversal fixed the worst miss, but outer cells were still lexicographic and corner-biased. Spatial telemetry dumped only on overflow, not invalid AUP input. `IsFiniteAup` accepted any finite local floats without validating sector bounds. The editor x-ray wrote tuning through a compaction lock rather than a writer fence. The shared report writer returned early when an old SHINOBU_301 section existed.
Solution: Public and flocking query loops now visit outer cells by squared-distance shells. `SpatialGridTelemetryEntry` now uses offset 56 as `InvalidInputCount`, sets `TelemetryFlagInvalidInput`, and triggers the 300-frame dump on overflow or invalid input. AUP conversion now clamps grid quantization to a safe 64-bit envelope and `IsFiniteAup` validates grid and local-sector bounds. `SpatialGridXRayWindow` mutates tuning through `TryAcquireWriteLock` and `ReleaseWriteLock`. `OOP_Query_Scanner` replaces the existing shared section deterministically instead of preserving stale embedded proof.
Rejected Alternatives: Keeping lexicographic outer traversal was rejected because low budgets still preferred corners over nearer face-neighbor cells. Expanding `SpatialGridEntryDTO` to encode invalid reasons was rejected because its 16-byte layout is mandatory. Raw editor pointer mutation was rejected after the Vault writer-fence API was confirmed. Reducing active entity count with quality was rejected because it changes gameplay truth, not just fidelity/cadence.
Scalability potential: Low uses center and nearest shells first, preventing wasted candidate budgets. Middle keeps bounded local awareness. High/Ultra expand to farther shells and richer x-ray proof without changing state ownership or rollback identity.
Hardware Impact: Static-only. The shell traversal spends the same bounded candidate budget more usefully on weak CPUs; invalid input now creates a blackbox dump instead of silent cell-zero drops. Build remains blocked by CPU 100% and active `dotnet` PIDs 7500/13920/15148.

## Loop 9 Report And Build Guard Refresh

Problem: Shared `AI_OPTIMIZATION_REPORT.json` carried the updated SHINOBU_301 shell/invalid-input proof but lacked a local verdict and still referenced the older CPU-100 guard sample.
Solution: Added `verdict: PASS_WITH_TASK15_TIMING_PARTIAL` to the SHINOBU_301 shared section and refreshed both stable/shared report compile proof to the latest guard fact: CPU 3 percent, active `dotnet` PID 15148. Re-ran stale-pattern scan against edited SHINOBU_301 files and JSON parse checks.
Rejected Alternatives: Launching `dotnet build` while PID 15148 is active was rejected by the project build guard. Editing unrelated `ShinobuFloraFaunaSymbiosisSolver.cs` to silence a broad grep hit was rejected because it belongs to another agent's domain.
Scalability potential: No runtime algorithm change in this loop. The previous continuous grid quality curve remains the active path: low = larger cells/fewer probes/fewer candidates, middle = bounded local awareness, high/ultra = deeper shells and richer diagnostics.
Hardware Impact: No measured microseconds. This loop preserves compile-wall discipline and avoids competing with an existing `dotnet` process on the developer machine.

## Loop 10 Proof Language And Payload Ledger

Problem: The source implementation and static scanners were present, but status/report language overclaimed `PASS` while Unity import, Play Mode, Burst Inspector, profiler/GCMonitor, and player build were not proven. The binary payload ledger also lacked a SHINOBU_301 route card, and one log line implied reserved BufferID `70456` was requested at boot.
Solution: Reclassified SHINOBU_301 proof language to `PENDING VERIFICATION / STATIC IMPLEMENTED`, added a concise SHINOBU_301 spatial-grid payload boundary entry to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, refreshed the guard fact to CPU 21 percent with active `dotnet` PID 15148, and documented `ShinobuSpatialGridMockCoordinates` as reserved-only. Runtime code was left unchanged because Task 15 exact split timings require a dispatcher timing lane, not fake stopwatch values.
Rejected Alternatives: Claiming final PASS without Unity/profiler proof was rejected. Adding tiny timestamp jobs around quantize/sort was rejected because the global doctrine rejects tiny jobs and it would still measure chain latency rather than exact Burst kernel time. Removing `70456` was rejected because neighboring SHINOBU ledger text already treats `70448..70456` as Agent 301's reserved lane range.
Scalability potential: No runtime curve changed in this loop. Low/Middle/High/Ultra behavior remains continuous through cell size, probe count, query cap, update stride, and optional debug richness.
Hardware Impact: 0 measured microseconds. This loop reduces integration risk and prevents false performance telemetry from driving bad tuning decisions on i3/MX350 or Quest-class targets.

## Loop 11 AUP Boundary Deadband

Problem: The original quantizer used `scaled + epsilon` before `floor`. That stabilizes positive near-boundaries but can misclassify exact negative cell boundaries, for example `-1.0 + epsilon` floors to `0`, shifting a legitimate negative boundary cell toward the origin.
Solution: Replaced positive-only bias with a symmetric deterministic deadband: compute the scaled double cell coordinate, round to the nearest integer, snap only when `abs(scaled - nearest) <= QuantizationEpsilon`, then call `floor`. Exact `-1`, `0`, and `+1` boundaries remain mathematically correct while microscopic binary representation noise around either side of a boundary is stabilized.
Rejected Alternatives: Removing epsilon was rejected because Task 12 explicitly requires deterministic stabilization. A signed epsilon was rejected because it shifts exact negative boundaries outward. Keeping the positive-only bias was rejected because it corrupts AUP cell identity for negative coordinates.
Scalability potential: No quality curve changed. The fix protects all Low/Middle/High/Ultra tiers because cell identity is authority-neutral and must not vary by quality.
Hardware Impact: 0 measured microseconds. The extra double `round/abs/select` cost is paid once per active entity during quantization and prevents downstream bucket churn, missed neighbor ranges, and false invalid telemetry on large negative AUP coordinates.

## Loop 12 Compile-Wall Import Trim

Problem: `ShinobuEcosystemBalancer.cs` still carried `using Hecton8.Ecosystem`, but SHINOBU_301-owned symbols in the file resolve through local `Hecton8.AI.Ecosystem` DTOs, Core, Core.Contracts, Core.Memory, and World. The unused sibling-domain import weakens the compile-wall audit even when it does not add a new asmdef edge by itself.
Solution: Removed the unused import and re-ran using/asmdef scans. No AI/Ecosystem asmdef exists for this folder; no new sibling Runtime reference was added by SHINOBU_301.
Rejected Alternatives: Keeping the import as harmless was rejected because the mandate asks for aggressive using-statement verification. Moving DTOs into a new Contracts assembly was rejected because it would widen the blast radius and is not needed for this isolated spatial-grid route.
Scalability potential: No runtime behavior change. This protects iteration speed only.
Hardware Impact: 0 measured microseconds; compile graph hygiene only.

## Loop 13 Scanner Report Schema Hardening

Problem: `OOP_Query_Scanner.BuildJson()` still emitted the old minimal report schema with `OOP Proximity Queries Eradicated` and no pending-verification, AUP deadband, telemetry partial, or compile-guard fields. Running the editor scanner later could overwrite the corrected proof artifacts with overconfident stale wording.
Solution: Updated the scanner JSON generator to emit the SHINOBU_301 pending-verification schema, including runtime static scan facts, DTO layout, Dear Lie route, blackbox ring, unavailable timing sentinel, squared-shell traversal, symmetric AUP deadband, editor writer lock, import trim, and a build-not-run scanner proof object.
Rejected Alternatives: Leaving the scanner stale was rejected because the report generator must be the source of durable proof, not a one-time manually edited JSON. Running a build from the scanner was rejected; static scanner tools must not invoke compiler work.
Scalability potential: No runtime behavior change. This preserves tuning/report truth across Low/Middle/High/Ultra decisions.
Hardware Impact: 0 measured microseconds. Prevents bad tuning decisions caused by stale report wording rather than changing frame cost.

## Loop 14 Public Query AUP Delta Localization

Problem: `SpatialHashQuery.CollectRange()` subtracted AUPs in double precision but performed the final `math.lengthsq` in `double3`. The query center makes the delta local, but the project AUP law requires immediate cast to local `float3` after double subtraction so subsequent distance math cannot drift into an absolute-coordinate habit.
Solution: Changed public radius validation to compute `deltaAup = entityAup - centerAup` in double, cast `deltaAup` to `float3 localDelta`, validate both are finite, and compare `math.lengthsq(localDelta)` to a float radius squared. Quantization still uses 64-bit AUP before bucket identity.
Rejected Alternatives: Leaving double distance math was rejected because it violates the stated AUP rule even if the delta is already localized. Casting absolute positions before subtraction was rejected because it causes 100km jitter. Removing the final radius check was rejected because hash cells are an acceleration structure, not exact sphere truth.
Scalability potential: No quality curve changed. Low/Middle/High/Ultra all retain the same authoritative cell identity and bounded query caps.
Hardware Impact: 0 measured microseconds. The float local distance check is cheaper than double length-square on ARM64 and aligns with the cache/NEON path, but runtime proof remains pending.

## Loop 15 Telemetry Dump Gate Separation

Problem: The spatial telemetry patch for `QueryCount` was nested under `_dumpedSpatialGridFault == false`. After the first overflow or invalid-input dump, later frames would keep the old query count even though Task 15 requires a live 300-frame forensic ring.
Solution: Moved the `_dumpedSpatialGridFault` guard down to the binary dump branch only. The latest `SpatialGridTelemetryEntry` now refreshes `QueryCount`, `TelemetryFlagQueryCountPatched`, and the query-count contribution to `StateHash` every telemetry frame when the flocking counter buffer is readable.
Rejected Alternatives: Allowing repeated binary dumps was rejected because it can spam disk after one persistent bad state. Leaving the query patch behind the dump gate was rejected because it silently freezes forensic truth after the first fault. Adding a second telemetry writer was rejected because one fact must have one owner and one route.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this protects telemetry truth across all quality weights without changing authority, DTO layout, or query budget.
Hardware Impact: 0 measured microseconds. The branch removal from the normal telemetry path is negligible; the value is forensic correctness and avoiding bad tuning decisions after the first spatial fault. Build remains blocked by CPU 63 percent with no active dotnet/csc/VBCSCompiler process output.

## Loop 16 Subagent Structural Correctness Patch

Problem: Subagent audit found two authority risks: quality-driven range-table probe depth could drop valid bucket ranges on low quality, and a 32-bit `CellHash` alone could merge distinct cells into one range. It also found that `EntityHashID` was actually a row index, stale range flags could survive a Vault reset, and X-Ray read paths lacked a coherent read fence.
Solution: Split structural probing from quality. Range insert/find now use fixed `StructuralBucketProbeCount` while `GlobalQualityWeight` only scales visited cells/result caps. `SpatialGridEntryDTO` remains exactly 16 bytes but offset 8 is now `uint2 CellFingerprint`, a 64-bit secondary cell hash checked before candidate budget is consumed. The identity field is now `EntityRowIndex`. Range flags use a session-local epoch stamp and the range table is cold-cleared on Vault reset. X-Ray reads use `SystemID.CoreDiagnostics` `TryLockBuffer` read sets before resolving multiple Vault views. The scanner writes separate `scannerProof` and preserved `projectCompileProof`.
Rejected Alternatives: Widening `SpatialGridEntryDTO` was rejected because the task requires 16 bytes. Letting low quality reduce structural hash probing was rejected because quality must not change truth. Using stable entity hashes in the 16-byte entry was rejected because the query path needs row lookup; stable seeds remain in `AmbientEntityAupDTO`. Repeated binary dumps or unlocked editor snapshots were rejected for forensic stability.
Scalability potential: Low devices still get fewer visited cells/results and larger cell sizes, but structural range discovery remains authoritative. Middle/High/Ultra increase local perception budget without changing DTO layout or save authority.
Hardware Impact: 0 measured microseconds. Fixed structural probing may do more work than the old low-quality path under high collision pressure, but it prevents false negatives; the 64-bit fingerprint avoids wrong-cell budget burn before distance checks. Build remains blocked by CPU 100 percent with active dotnet PIDs 6868 and 14108.

## Loop 17 Exact Range Key Closure

Problem: Loop 16 added a 64-bit `CellFingerprint` to each 16-byte entry, but the range row and flocking lookup still had a residual 32-bit grouping risk: same `CellHash` could find a range before the full fingerprint was part of the range key, and the radix sorter still ran only four byte passes.
Solution: Widened `SpatialGridBucketRangeDTO` to an explicit 32-byte exact-key row (`CellHash@0`, `CellFingerprintX@4`, `CellFingerprintY@8`, `StartIndex@12`, `Count@16`, `Flags@20`, `Pad0@24`, `Pad1@28`), updated cold boot layout asserts, changed public and flocking range lookup to require both hash and fingerprint, and changed the radix sorter to eight passes across the full `uint2` fingerprint before range construction.
Rejected Alternatives: Keeping the range row at 16 bytes was rejected because exact-cell lookup needs both the 32-bit slot seed and 64-bit fingerprint without touching the mandatory 16-byte entry DTO. Consuming candidate budget and then filtering entries by fingerprint was rejected because wrong ranges could still burn low-tier query budgets. Sorting by 32-bit hash only was rejected because it cannot guarantee contiguous exact-cell ranges under hash collision.
Scalability potential: Low-tier devices still receive larger cells and capped visited cells/results, but structural cell identity no longer depends on quality or 32-bit collision luck. Middle/High/Ultra can expand query budget while using the same exact range key and DTO route.
Hardware Impact: 0 measured microseconds. The range row doubles from 16 to 32 bytes, adding roughly 2 MB for 131072 reserved range slots, but it prevents wrong-cell candidate scans and false neighbors under collision. Static verification passed for forbidden hot tokens, AI/Interaction physics proximity tokens, report JSON parsing, and whitespace errors except existing LF/CRLF warnings. Build not launched; latest guard sample is CPU 100 percent with active dotnet PID 5468.

## Loop 18 XML ABI Alias Reconciliation

Problem: The XML prompt literally named `SpatialGridEntryDTO.EntityHashID@0` and `LocalCellOffset@8`, while the current correct hot path needed `EntityRowIndex@0` for O(1) Vault row lookup and `CellFingerprint@8` for exact 64-bit cell collision defense. Dropping the XML names weakened proof even though the 16-byte offsets were correct.
Solution: Added explicit-layout aliases on the same bytes: `EntityHashID` and `EntityRowIndex` both at offset 0, `LocalCellOffset` and `CellFingerprint` both at offset 8. Cold boot now asserts all four names. Reports and ledger describe `EntityHashID`/`LocalCellOffset` as ABI aliases and keep `EntityRowIndex`/`CellFingerprint` as canonical execution lanes.
Rejected Alternatives: Renaming the canonical hot fields back to the XML names was rejected because it would hide the row-index semantics and weaken code clarity. Removing `CellFingerprint` was rejected because 32-bit `CellHash` collisions can burn low-tier query budgets or mix exact cells. Widening `SpatialGridEntryDTO` was rejected because the 16-byte requirement is absolute.
Scalability potential: No runtime curve changed. Low/Middle/High/Ultra all use the same exact-cell identity; quality still scales only cell size, cadence, visited cells, result/candidate budgets, and optional debug.
Hardware Impact: 0 measured microseconds. The alias fields do not increase struct size or memory bandwidth; they only restore ABI/proof compatibility with the original XML field names. Build not launched; latest guard sample is CPU 100 percent with active dotnet PID 5468.

## Loop 19 Post-Compaction Verification Guard

Problem: The context was compacted while SHINOBU_301 was still under a keep-working mandate, so stale chat memory could diverge from disk truth and shared `AI_OPTIMIZATION_REPORT.json` had again been overwritten by another agent.
Solution: Re-read `AGENTS.md`, domain map, authority boundaries, selected `.agents-skills`, the full SHINOBU_301 XML block, status, rationale, and payload ledger. Re-verified DTO anchors, exact-fingerprint range lookup, eight-pass sort, AUP local float distance check, forbidden hot tokens, JSON parsing, and current compiler guard. Restored SHINOBU_301 into the shared report as a nested property while preserving the current top-level SHINOBU_312 report.
Rejected Alternatives: Trusting the compacted summary was rejected by the anti-amnesia protocol. Launching a build was rejected because the latest guard sampled CPU 99.6 percent with active `dotnet` PIDs 1548/16464. Overwriting the shared AI report back to a SHINOBU_301-only object was rejected because multiple agents share that artifact.
Scalability potential: No runtime curve changed. Low/Middle/High/Ultra still scale cell size, cadence, visited cells, result/candidate caps, and optional diagnostics only; exact range discovery and DTO layout remain authority-stable.
Hardware Impact: 0 measured microseconds. This pass prevents stale proof artifacts and build contention; runtime proof remains pending.

## Loop 20 Range Padding Proof Drift

Problem: The source `SpatialGridBucketRangeDTO` already had `Pad0@24`, but cold layout asserts and several proof strings omitted that field. That is not a hot-path bug, but it makes the ABI evidence incomplete and lets future scanner runs regenerate stale layout proof.
Solution: Added the missing `AssertOffset<SpatialGridBucketRangeDTO>(nameof(SpatialGridBucketRangeDTO.Pad0), 24)` and updated the scanner-emitted range layout, stable/shared reports, status, rationale, ledger, and final log wording to include `Pad0@24`.
Rejected Alternatives: Leaving `Pad0` implicit was rejected because this project requires byte-exact ARM64 DTO evidence. Removing the pad field was rejected because the 32-byte range row needs explicit terminal padding and the source layout is already correct.
Scalability potential: No runtime curve changed. Low/Middle/High/Ultra still scale only cell size, cadence, visited cells, result/candidate caps, and optional diagnostics; exact range row ABI remains fixed.
Hardware Impact: 0 measured microseconds. This is proof hardening; build remains guarded by CPU 100 percent with active `dotnet` PIDs 992/3056.
