# SHINOBU_358 Rationale

State: POLISH PASS ACTIVE / FULL FIRST-PARTY V2 REPORT WRITES ON CACHE V3 / CURRENT SOURCE FAILS PROPERTY GATE

## Decision 001 - Domain Boundary

Problem: Validator must inspect C# struct layout and binary schema assets without becoming runtime authority.
Solution: Keep implementation in `Tools/` and outputs in `Docs/Reports/`; no C# source mutation, no asset metadata mutation, no GlobalRegistry or SignalBus additions.
Rejected Alternatives: Runtime validator component, Editor auto-fix, or source-rewriting parser. Those create compile-wall and ownership risk.
Scalability potential: Low/Middle/High/Ultra all use the same offline gate; stronger machines only get faster CI wall time, not weaker correctness.
Hardware Impact: On i3/MX350, offline preflight prevents ARM64/misaligned DTO regressions before import/build; estimated runtime gain is defensive, not frame-time-positive.

## Decision 002 - Parser Shape

Problem: Regex-based C# parsing risks catastrophic backtracking and false positives on attributes/comments.
Solution: Use deterministic byte tokenizer over `mmap`, reconstruct only the subset needed for unmanaged struct contracts.
Rejected Alternatives: Python `re` scanner, Roslyn dependency, Unity Editor reflection. Regex is fragile; Roslyn adds setup friction; reflection is post-compile and too late.
Scalability potential: Low uses same parser with fewer cores; Middle/High/Ultra benefit from thread-pool parallelism and larger codebase throughput.
Hardware Impact: Expected host-side savings versus naive full-file reads/string regex: hundreds to thousands of microseconds on i3/MX350-class hardware for large scans.

## Decision 003 - Existing Validator Integration

Problem: The repository already had `Tools/h8bin_validator.py` as the owner of `.h8bin` schema/header/section verification, so a second tool would create two truth routes for the same binary contract.
Solution: Extend `Tools/h8bin_validator.py` with AST struct reconstruction, ARM64 alignment, property bans, AUP precision checks, report emission, and explicit exit codes.
Rejected Alternatives: New `Tools/binary_schema_ast_validator.py` wrapper or C# Editor gate. A wrapper duplicates scan cost and exit semantics; Editor gate validates too late and increases compile-wall exposure.
Scalability potential: Low tier uses one mmap/token pass and no Unity boot; Middle tier uses normal thread-pool fanout; High and Ultra can scan larger source surfaces without changing schema truth ownership.
Hardware Impact: Focused Data Monolith validation processed 36,096 bytes in 19,764 us on the latest stable SHINOBU-specific report run. Saved cost versus duplicate validator route is one extra source tree pass per CI run.

## Decision 004 - ARM64 Contract

Problem: Explicit unmanaged structs can appear valid on x86 while still being unsafe for ARM64 binary IO due to field offset and total-size alignment defects.
Solution: Validate each explicit field offset against its native alignment and require struct size to be a multiple of `max(8, largest_alignment)`; map alignment failures to exit code 2.
Rejected Alternatives: Accepting `[StructLayout(Pack=1)]`, relying on `Marshal.SizeOf`, or permitting odd total sizes. Pack-based tolerance hides faults; reflection is post-compile; odd sizes poison array stride.
Scalability potential: Low/Middle/High/Ultra all receive identical binary layout truth; higher hardware gets no different data contract, only faster preflight throughput.
Hardware Impact: Prevents unaligned loads and schema drift before ARM64 deployment. Runtime gain is avoided fault/copy path, not a measured frame-time speedup.

## Decision 005 - Property Ban

Problem: Properties inside unmanaged DTOs can trigger CS1612-style copy/write bugs and hide managed access paths behind binary records.
Solution: Token-scan struct bodies for `get`/`set` accessors and emit `STRUCT_PROPERTY_BANNED`; map this class to exit code 3.
Rejected Alternatives: Allowing read-only properties, source auto-fix, or relying on code review. Read-only properties still normalize the wrong DTO style; auto-fix mutates other domains; review is not a gate.
Scalability potential: Toaster through Ultra all get deterministic rejection with no runtime overhead.
Hardware Impact: Blocks hidden defensive copies before build. Estimated low-end gain is defensive and workload-dependent.

## Decision 006 - AUP Precision Scope

Problem: A broad `Aup*` ban produced a false positive on scalar `AupSectorSizeMeters`, which is configuration scale, not position truth.
Solution: Restrict AUP precision violations to world-position/AupX/AupY/AupZ style fields and write-near float casts; require contiguous double lanes for AUP coordinate groups.
Rejected Alternatives: Blanket `Aup` keyword ban or ignoring precision entirely. Blanket ban blocks valid constants; ignoring precision permits save/schema corruption.
Scalability potential: Low uses cheap scalar constants; Middle/High/Ultra keep the same authoritative double-coordinate contract while buying visual fidelity elsewhere.
Hardware Impact: Prevents float truncation of position truth on low-end and ARM64 targets. No runtime allocation or frame cost added.

## Decision 007 - Watchdog Wall Handling

Problem: Full first-party scan can exceed the mandated 10 second watchdog under current checkout load, and hiding that would create a false compliance report.
Solution: Keep the 10 second watchdog, document the full-scan wall, and prove the focused Data Monolith path plus regression suite. Do not raise timeout or report completion without evidence.
Rejected Alternatives: Increasing watchdog, disabling it, or claiming success from a partial full-root run. Those violate the task protocol.
Scalability potential: Low machines fail fast instead of starving; Middle/High/Ultra benefit from existing mmap/threading but still keep hard stop semantics.
Hardware Impact: Caps bad CI/runtime host consumption at 10,000,000 us. Current focused path cost is 19,764 us on the stable SHINOBU-specific report run; full-root optimization remains required.

## Decision 009 - Shared Report Collision

Problem: `Docs/Reports/BINARY_SCHEMA_AUDIT_REPORT.json` was observed reverting from SHINOBU_358 `h8bin_validator.report.v2` to older `hecton8.binary_schema_audit.v1` while parallel agents were active.
Solution: Move `Tools/AssemblyDependencyAudit.py` default binary report to `Docs/Reports/ASSEMBLY_BINARY_SCHEMA_AUDIT_REPORT_SHINOBU_359.json` and keep `Docs/Reports/BINARY_SCHEMA_AUDIT_REPORT.json` as the SHINOBU_358 canonical v2 output. The explicit `--binary-report-path` override remains for manual/integrator routing.
Rejected Alternatives: Repeatedly overwriting the shared file, hiding the collision, or changing SHINOBU_358 canonical output. Rewriting in a loop wastes CPU and still loses race ownership; hiding it breaks evidence integrity; changing the canonical path violates Task 03.
Scalability potential: Low/Middle/High/Ultra CI runs need one owner per report path. The SHINOBU-specific report preserves proof without changing the shared route contract.
Hardware Impact: Latest stable focused run cost 19,764 us; extra one-off proof write is acceptable, but permanent duplicate report routes should be resolved by integrator ownership.

## Decision 008 - Domain Blocks

Problem: Tasks requesting tuner windows, CSV profile ingestion, live gizmos, and broader architectural metric policing cross the assigned offline Python preflight domain.
Solution: Mark Tasks 16-18 as `[BLOCKED BY DOMAIN]` and Task 19 as `[BLOCKED BY SCOPE]`; keep the implementation in `Tools/` and report files only.
Rejected Alternatives: Adding EditorWindow, Vault DTO, Gizmo, or OOP scanner code from this agent. That would violate authority boundaries and risk breaking C# compile while solving a Python gate task.
Scalability potential: Low/Middle/High/Ultra preserve one fact, one owner, one proof artifact for binary schema validation.
Hardware Impact: Avoids extra Unity Editor load and compile churn. Estimated saved cost is unmeasured but avoids a high-risk compile wall.

## Decision 010 - False Struct Span Repair

Problem: The original mmap finder accepted the word `struct` inside line comments and then copied the next class body as a candidate span, creating 100KB+ false parse surfaces.
Solution: Keep the fast `mmap.find(b"struct")` route but reject line-comment hits, require identifier context after `struct`, and keep line counters by byte-range counts instead of a full byte-by-byte scanner.
Rejected Alternatives: Full byte-state scanner over every file. It was more correct for strings/block comments but profiled at 18.9s for full-root struct parsing under current load, violating the 10s watchdog.
Scalability potential: Low machines avoid false spans; Middle/High/Ultra still parse the same source truth faster. No gameplay quality route changes.
Hardware Impact: Removes pathological false spans such as comment-driven 100KB class bodies. Latest warm full-root validation writes report in 427,740 us with cache.

## Decision 011 - Regex Removal

Problem: SHINOBU_358 was still carrying Python regex in legacy helper surfaces and expected-size parsing, contradicting the no-slow-RegEx mandate.
Solution: Remove Python `re` import and all `re.*` call sites from `Tools/h8bin_validator.py`; parse expected record sizes through C# tokens and parse text artifact string constants with deterministic string scanning.
Rejected Alternatives: Leaving regex outside the StructLayout path. The user mandate said Python AST parser without slow RegEx, so keeping adjacent regex debt was not defensible.
Scalability potential: Low/Middle/High/Ultra offline gates now share one deterministic parser style.
Hardware Impact: Eliminates catastrophic backtracking class from this tool. Runtime hardware impact remains zero because this is offline CI.

## Decision 012 - Watchdog And AST Cache

Problem: Cold full-root parsing can still exceed 10s under parallel-agent CPU load, and raising the watchdog violates Task 11.
Solution: Keep the 10s watchdog and add an offline per-file AST cache at `Docs/Reports/.h8bin_validator_ast_cache_SHINOBU_358.json`, keyed by source file size and mtime. Cache writes are atomic and occur every 32 newly parsed files so watchdog-killed passes make forward progress.
Rejected Alternatives: Longer timeout, partial report claiming full proof, or runtime C# verifier. Longer timeout violates the task; partial proof is a lie; runtime verifier is post-compile and outside domain.
Scalability potential: Weak machines may fill cache over several bounded passes; warm CI on any tier validates full source quickly without changing schema truth.
Hardware Impact: Latest warm full-root validator wall measured `427,740 us`; cold cache fill remains host-load dependent but no longer starves beyond the 10s watchdog per pass.

## Decision 013 - Current Source Failure Classification

Problem: Full validator now writes a report but current source contains explicit-layout byte contract failures such as fields inside explicit layouts without `FieldOffset`.
Solution: Classify missing/unresolved FieldOffset/Size/Layout parse failures as ARM64/layout-class errors and return exit code `2`.
Rejected Alternatives: Generic exit `1` for structural ABI failures. Generic failure hides the failure class from CI.
Scalability potential: All hardware tiers receive the same ABI rejection; no gameplay truth changes.
Hardware Impact: Prevents unaligned or ambiguous DTOs from reaching ARM64/mobile builds. Current full report: `2771` structs parsed, `544` findings, `68` ARM64/layout-class findings.

## Decision 014 - Expression-Bodied Property Classification

Problem: The validator reported expression-bodied C# properties inside explicit unmanaged structs as `FIELD_OFFSET_MISSING`, making property policy failures look like byte-offset parser defects.
Solution: Tokenize `=>`, detect top-level expression-bodied members before field parsing, and emit `STRUCT_PROPERTY_BANNED` with exit code `3`. Existing `{ get; set; }` checks remain active.
Rejected Alternatives: Editing runtime C# accessor helpers from this offline lane, or suppressing read-only properties. Source mutation violates SHINOBU_358 authority; suppressing accessors violates CS1612/property-ban doctrine.
Scalability potential: Low/Middle/High/Ultra all receive the same DTO property gate; stronger hardware does not weaken binary contract truth.
Hardware Impact: Latest report routes `41` explicit-struct property findings to exit `3`; `49` prior false `FIELD_OFFSET_MISSING` findings were removed from parser output.

## Decision 015 - Fixed Buffer And Nested Scope Repair

Problem: `fixed byte Payload[112]` and nested explicit structs were misclassified because the parser treated fixed-buffer brackets as attribute blocks and leaked nested `FieldOffset` tokens to outer job/queue structs.
Solution: Parse fixed primitive buffers as `primitive[count]` with size `elementSize * count`, and make candidate extraction require direct-body top-level `FieldOffset` unless the current struct itself has `LayoutKind.Explicit`.
Rejected Alternatives: Source edits in Combat/Physiology/Quest/Save/Fauna/Core owner domains, or folder exceptions. The validator must be uniform and read-only.
Scalability potential: Same parser truth on all host tiers; cache warmth changes CI cost only, not validation strictness.
Hardware Impact: Removed all `FIELD_DECL_UNPARSED` and `STRUCT_LAYOUT_MISSING` findings from the current SHINOBU_358 report while preserving true property, AUP, and layout blockers.

## Decision 016 - Cache V3 And Process Pool Rejection

Problem: Windows process-pool cold parsing introduced startup cost that still collided with the 10s watchdog and complicated bounded cache fill.
Solution: Default to serial mmap/token parsing with AST cache schema `h8bin_validator.ast_cache.v3`, use process pool only when explicitly enabled by `H8BIN_VALIDATOR_USE_PROCESS_POOL=1`, and save cache in bounded batches. The watchdog remains 10 seconds.
Rejected Alternatives: Raising the watchdog, depending on multiprocessing by default, or requiring several cold passes as normal operation. Raising timeout violates Task 11; process startup is unstable under active workspace load; repeated cold passes were only an emergency cache-fill fallback.
Scalability potential: Weak hosts use the same cache route and finish warm under the warning line; high-tier hosts may explicitly enable process pool for experiments without changing CI truth.
Hardware Impact: Latest cold-cache CLI probe completed before watchdog with exit `3`; latest warm full-root report parsed `2774` structs in `253,362 us` and emitted `529` findings.
