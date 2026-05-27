# Rationale_1300

Agent: 1300 MEMORY_SOVEREIGN_AI_COGNITION_EXORCIST
Domain: `Assets/_Project/Scripts/AI/Cognition`
Status: STATIC_GREEN / PRIVATE_WIDE_PAD_CLOSED / CROSS_DOMAIN_DTO_PAD_CLOSED / LOCKED_VAULT_WRITES_CLOSED / TEMP_PATH_CONCAT_CLOSED / DUMP_PATH_FAIL_CLOSED / CSV_RESOLVER_FAIL_CLOSED / LAYOUT_GUARD_ADDED / HOT_UNSAFE_BYPASS_CULLED / AUP_HASH_FAIL_CLOSED / SHELTER_GRID_OVERFLOW_GUARDED / SIGNAL_BUDGET_UNDERFLOW_CLOSED / READONLY_TUNING_ALIAS_CLOSED / TASK17_EDITOR_FUZZER_STATIC / FUZZER_LOCK_LIFETIME_CLOSED / COMPILE GATED

## Decision 2026-05-25-001: Phase 0 Evidence Route

Problem: Assignment required native memory alias eradication, but code reality was unknown.
Solution: Extracted prompt from `Docs/Tasks/CURRENT_BATCH.md`, selected eight mandates, and used scoped Roslyn evidence before mutation.
Rejected Alternatives: Direct code edits from prompt assumptions; project-wide rewrite; managed `List<T>` substitution to satisfy a scanner.
Scalability potential: Low avoids churn and preserves existing continuous quality math; Middle/High/Ultra keep DataVault relocation compatible without changing truth ownership.
Hardware Impact: Prevented blind rewrite risk on i3/MX350; measured microseconds absent until Unity profiling.

## Decision 2026-05-25-002: Editor Telemetry Alias Exorcism

Problem: `AIAnxietyTunerWindow.AnxietyTelemetryChartElement` held a persistent `NativeArray<AnxietyTelemetryEntry>` field. Even editor-only, it was a stale physical pointer pattern.
Solution: Replaced the field with `IDataVault` plus `VaultGenerationHandle<AnxietyTelemetryEntry>` and resolve the transient `NativeArray` inside `DrawChart` only.
Rejected Alternatives: Managed telemetry copy for the chart; keeping the raw `NativeArray` because it is editor code; resolving through `GlobalRegistry` during paint.
Scalability potential: Low keeps editor chart safe during vault relocation; Middle/High/Ultra allow telemetry visualization without stale pointers.
Hardware Impact: 0 hot runtime microseconds. Removes one stale-pointer class; editor repaint may pay a small vault resolve cost only when UI draws.

## Decision 2026-05-25-003: Read Accessor Purification

Problem: Public read-looking accessors used mutable `TryResolveViews`, which violates the doctrine that read accessors must be pure and must not expose write-capable aliases.
Solution: Converted Utility, Anxiety, and Apex tuning/state reads to `IDataVault.TryReadOnlyHandle` with handle validity and bounds checks.
Rejected Alternatives: Leaving `TryResolveViews` in read APIs; copying to managed DTO arrays; synchronously completing jobs before read.
Scalability potential: Low fails closed if handle is stale; Middle/High/Ultra keep readbacks decoupled from compaction and scheduling.
Hardware Impact: 0 expected hot-loop microseconds; no GC; fewer mutable aliases reduce compaction hazard on weak silicon.

## Decision 2026-05-25-004: Direct Writer Lock Discipline

Problem: Direct tuning/state writers mutated vault buffers through transient mutable views without explicit writer fences.
Solution: Converted Utility tuning, Anxiety tuning/profile sync, Apex state, and Apex tuning writers to `TryAcquireWriteLock` and `finally ReleaseWriteLock`.
Rejected Alternatives: Holding locks across frame phases; scheduling job rewrites without owner proof; returning early after acquiring a lock.
Scalability potential: Low devices can skip writes on contention; Middle/High/Ultra can preserve vault compaction safety while tuning/editor writes occur.
Hardware Impact: 0 steady hot-loop microseconds; direct editor/cold writes pay lock bookkeeping only. No managed allocation introduced.

## Decision 2026-05-25-005: Roslyn Proof Artifact

Problem: Raw scanner classifies `*VaultBuffers` fields as forbidden because it cannot infer phase-local vault view structs.
Solution: Kept raw Roslyn ledger and generated `VAULT_EXORCISM_REPORT_1300.json` with explicit domain classification: 36 transient vault views, 65 transient job views, 0 persistent aliases.
Rejected Alternatives: Hiding raw findings; regex-only proof; chat-only evidence.
Scalability potential: Low/Middle/High/Ultra all benefit from reproducible static proof before future compaction work.
Hardware Impact: 0 runtime microseconds. Editor/tool-only managed allocation.

## Decision 2026-05-25-006: Black-Box Dump Route

Problem: Existing black-box dump routes used subsystem filenames, while the assignment mandates `Docs/AgentLogs/Dump_1300_AICognition.bin`.
Solution: Retained legacy subsystem dump names and mirrored cold dump writers to the agent filename.
Rejected Alternatives: Renaming/removing legacy files; writing managed string diagnostics on hot failure paths; spawning background work on hot update.
Scalability potential: Low devices pay no steady cost; Middle/High/Ultra keep richer post-mortem data without changing runtime truth.
Hardware Impact: 0 runtime microseconds; cold fault path writes an extra binary file only when dump is requested or fault-triggered.

## Decision 2026-05-25-007: Build Gate

Problem: Verification requires compilation, but local rule forbids launching build when active `dotnet` or `csc` exists.
Solution: Ran Roslyn parser/static audit and scoped `git diff --check`; did not start Unity/dotnet build while active `dotnet` processes were present.
Rejected Alternatives: Violating build gate; claiming compile success without running it; killing unrelated processes.
Scalability potential: Preserves parallel agent stability.
Hardware Impact: Avoided extra CPU contention on already shared machine; compile proof remains pending.

## Decision 2026-05-25-008: APEX Override Static Re-Audit

Problem: The first status over-trusted explicit layout and did not prove managed text/formatting absence with a fresh source scan.
Solution: Re-read `AGENT_PROMPT id=1300`, rescanned runtime `AI/Cognition` excluding `Editor`, and separated hot-path evidence from cold filesystem dump paths. Runtime scan found no `string.Format`, `.ToString(`, LINQ, interpolation, `.ToArray`, or `.ToList`; allocation candidates were Burst value-type constructors and cold `FileStream`/temp-path strings.
Rejected Alternatives: Reporting regex output that accidentally scanned the repo root; pretending cold crash dump `FileStream` construction is a zero-GC hot path; replacing cold file APIs with unsafe native plugin stubs without platform owner proof.
Scalability potential: Low keeps cognition ticks allocation-free; Middle/High/Ultra can retain richer black-box dump data without changing hot loop behavior.
Hardware Impact: 0 hot runtime microseconds claimed. Cold dump path still allocates managed path/FileStream objects; that is accepted only because it is crash/editor I/O, not a frame loop.

## Decision 2026-05-25-009: DTO ARM64 Source-Order Repair

Problem: Several explicit DTOs were size-correct but source-order hostile: byte/ushort fields preceded uint/ulong fields, and one public `_pad0` carried live frame data.
Solution: Reordered fields by width and semantic role in `CognitionTargetCandidateDTO`, `CognitionTelemetryEntry`, Apex DTOs, and Alpha Leviathan contracts. Replaced public fake padding in `MockPlayerAUP` with `LastAdvanceFrame`; made Apex pads private; regenerated byte offset map with every resolved DTO `Size % 8 == 0`.
Rejected Alternatives: Relying on `[FieldOffset]` while leaving misleading source order; keeping public `_pad` fields as semantic storage; adding sequential-layout shims.
Scalability potential: Low avoids ARM64 alignment traps and semantic pad misuse; Middle/High/Ultra preserve the same DTO stride, so saved cycles can remain available for visual overkill math.
Hardware Impact: Expected gain is not profiler-measured. Risk reduction is concrete: no byte-before-uint layout drift in the corrected DTO rows, and no code writes live data into a field named `_pad0`.

## Decision 2026-05-25-010: Alpha Leviathan AUP Telemetry Correction

Problem: `LeviathanStalkJob` wrote telemetry positions by directly downcasting absolute `double3` AUP coordinates to `float3`, violating AUP doctrine.
Solution: Kept all steering math in double-local form, then wrote telemetry as `Position = float3.zero` and `PlayerPosition = SanitizeTelemetryLocalPosition(anchorAbsolute - leviathanAbsolute)` after double subtraction and local clamp.
Rejected Alternatives: Clamping absolute world AUP to `float.MaxValue`; storing both absolute positions in telemetry; hiding the violation as "diagnostic only".
Scalability potential: Low devices get stable local telemetry without precision noise; Middle/High/Ultra can still draw richer debug/presentation effects from local deltas.
Hardware Impact: 0 CPU microseconds claimed. Correctness gain is deterministic: no absolute AUP float cast remains in this telemetry path.

## Decision 2026-05-25-011: Cold Dump Managed I/O Boundary

Problem: The mandate demands no managed exceptions or strings in hot paths, but Unity/C# `FileStream` dump routes require managed paths and object construction.
Solution: Left cold dump/CSV file routes isolated and documented, while preserving hot runtime zero-GC paths. Existing dump writers serialize unmanaged DTO bytes through `ReadOnlySpan<byte>` where available and mirror black-box output to the agent dump file.
Rejected Alternatives: Claiming FileStream is allocation-free; deleting crash dumps to satisfy a text scan; introducing a native plugin writer without platform approval and test surface.
Scalability potential: Low pays no steady cost; Middle/High/Ultra keep crash evidence without affecting frame loops.
Hardware Impact: 0 hot runtime microseconds. Cold crash I/O can allocate and stall; that is outside normal frame cadence and remains a known limitation.

## Decision 2026-05-25-012: Dear Lie / Overengineering Check

Problem: A cognition pass can drift into physical simulation if ambush, fog-ring, and anxiety math become too exact.
Solution: Kept the existing fake-first math: bounded candidate scans, local SDF contour weights, triangle-wave noise, approximate exponential decay, and continuous `GlobalQualityWeight` cadence/capacity scaling. No new physical solver was added.
Rejected Alternatives: Iterative collision/force simulation for stalking; per-target physics probes; binary low/high quality branches.
Scalability potential: Low uses reduced candidates and cheap approximations; Middle keeps stable math LOD; High/Ultra spend saved budget on visual overkill scalar outputs, not gameplay truth changes.
Hardware Impact: Avoids adding any >0.1 ms suspicious simulation. Exact microseconds require Unity profiling; no fake savings are claimed.

## Decision 2026-05-25-013: Alpha Leviathan Continuous Math LOD Repair

Problem: `LeviathanStalkJob` had `const float mathLodPressure01 = 0f`, `const float visualQuality01 = 1f`, and `const bool survivalMathLod = false`, forcing the stalk solver into permanent precision/visual-overkill mode.
Solution: Replaced the constants with `SystemStress01 -> math.saturate -> SmoothStep01`, then routed the continuous weight into steering blend, recommended cadence, SDF quality, and silhouette noise. The legacy survival bit is now an output flag derived from the continuous weight, not the algorithm switch.
Rejected Alternatives: Keeping ultra-only stalk math; driving the path from a binary runtime flag; adding per-tier branches.
Scalability potential: Low gets higher cadence spacing and lower silhouette/SDF cost under stress; Middle transitions smoothly; High/Ultra retain SDF contour and visual overkill when stress is low.
Hardware Impact: 0 GC. Expected i3/MX350 gain is reduced stalk math pressure during stress frames, but no microsecond number is claimed without Unity profiler data.

## Decision 2026-05-25-014: MockPlayerAUP 8-Byte Field Reorder

Problem: `MockPlayerAUP.LastAdvanceFrame` was a real `ulong` stored after float/uint fields, violating the 8-byte-first DTO rule even though total stride was already 128 bytes.
Solution: Moved `LastAdvanceFrame` to offset 24 immediately after `double3 AUP`, shifted velocity/forward/hash/noise/flags into the 4-byte block, and regenerated `Docs/Reports/DTO_OFFSET_MAP_1300.txt`.
Rejected Alternatives: Leaving the field late because `[FieldOffset]` makes the runtime stride explicit; storing frame in a public `_pad` field; adding a second frame field.
Scalability potential: Low/Middle/High/Ultra share one deterministic DTO shape; no quality-dependent layout drift.
Hardware Impact: 0 runtime microseconds claimed. Removes one ARM64 layout-rule violation and preserves 128-byte stride.

## Decision 2026-05-25-015: Post-Patch Proof Hash Refresh

Problem: The second-pass code edits invalidated the previous source hash in `VAULT_EXORCISM_REPORT_1300.json`; the locked-vault/layout-guard/temp-path/dump-route pass invalidated it again.
Solution: Recomputed AI/Cognition source hashes and updated the classified report at that checkpoint with full hash `d16be22096f4098f742015949fb5780cb66513ab4fa77630591310f7e647cc6b`, runtime hash `06db25c83b9a30eae7096a6196df58cd8afaacd28331ed0fe03b2c3b318732bb`, and the DTO offset map path. Later current hashes are recorded by the newest decisions and report fields.
Rejected Alternatives: Leaving stale proof artifacts; running the Roslyn/dotnet scanner while CPU and active compiler gate forbid it; claiming a Unity compile without execution.
Scalability potential: Static proof stays reproducible for all device tiers; future agents can diff source hashes before trusting old reports.
Hardware Impact: 0 runtime microseconds. No build contention added to a saturated shared machine.

## Decision 2026-05-25-016: Locked-Vault Fallback Write Closure

Problem: `TryAcquireHandles` / `TryAcquireAnxietyHandles` wrote cold defaults after `vault.IsAllocationLocked`, and `TryScheduleAnxietyFrostTick` patched tuning through `NativeArrayUnsafeUtility.GetUnsafePtr`.
Solution: Locked branches now only read existing handles and resolve transient views. Cold defaults remain in nonlocked boot registration. `PatchAnxietyFrame` was removed; jobs receive `Frame` as an explicit parameter and `HashTuning` does not depend on `AnxietyRuntimeTuningDTO.Frame`.
Rejected Alternatives: Writing defaults while allocation is locked; using reflection/private compaction hooks to prove safety; introducing a spin lock around vault views.
Scalability potential: Low avoids relocation race failures under memory pressure; Middle keeps deterministic owner flow; High/Ultra retain richer tuning while vault compaction remains sovereign.
Hardware Impact: 0 hot runtime microseconds claimed. Removes a concrete write-during-lock hazard on Quest/i3/MX350 class hardware; exact performance impact is unprofiled.

## Decision 2026-05-25-017: Executable DTO Layout Guard And Raw Ledger Identity

Problem: `DTO_OFFSET_MAP_1300.txt` was a static artifact only, and the raw ledger still carried generic `agentId=X_000` despite being copied into the 1300 proof lane.
Solution: Added editor-only `AICognitionLayoutGuard1300` with `UnsafeUtility.SizeOf<T>()` and `Marshal.OffsetOf` checks for all 33 scoped DTO structs, including private pad fields. Corrected raw ledger `agentId` to `1300` and recorded file hash `f3e65f8898e041ef587f23362bbe830d1ee305091373d4318941841735689406`.
Rejected Alternatives: Trusting a text report without an executable guard; hiding the generic raw ledger ID; moving reflection checks into runtime Burst paths.
Scalability potential: Low/Middle/High/Ultra share identical DTO stride and field ownership; no device tier can silently drift layout.
Hardware Impact: 0 runtime microseconds. Editor domain pays reflection cost only during load/menu validation; release runtime remains unchanged.

## Decision 2026-05-25-018: Cold Dump Temp Path Fail-Closed Cleanup

Problem: The agent dump mirror added a second cold temp-path string concatenation in Alpha dump routing, and existing dump writers built `path + ".tmp"` before entering their exception guards.
Solution: Replaced runtime `+ ".tmp"` temp-path construction with named `Path.ChangeExtension(..., ".bin.tmp")` helpers and moved helper calls inside the guarded `try` blocks in Alpha, Apex, Utility, and Anxiety dump writers.
Rejected Alternatives: Claiming cold string concatenation is hot-path safe and leaving proof noise; deleting dump mirrors; building an unmanaged native crash writer without platform ownership.
Scalability potential: Low/Middle/High/Ultra retain the same black-box evidence route. Normal frame loops remain unaffected; cold failure paths now fail closed on path construction faults too.
Hardware Impact: 0 hot runtime microseconds. Cold dump path still uses managed filesystem I/O and can allocate; the concrete cleanup is removal of runtime `+ ".tmp"` text matches and a tighter exception boundary.

## Decision 2026-05-25-019: Outer Dump Route Path Guard

Problem: Alpha, Apex, Utility, and Anxiety dump routers still built root/agent-log paths before entering writer-level guards, so malformed project roots could escape before returning false.
Solution: Wrapped outer dump path composition in the same fail-closed catch set used by the cold dump writers. Alpha now also builds the primary dump path inside the guarded block before temp path construction.
Rejected Alternatives: Leaving path composition outside because it is cold; adding a broad catch-all in hot code; removing black-box dump mirrors.
Scalability potential: Low/Middle/High/Ultra keep identical black-box dump semantics. Bad paths or missing cwd fail closed without touching frame-loop cognition.
Hardware Impact: 0 hot runtime microseconds. Cold failure path has a few extra catch tables and no frame-loop cost.

## Decision 2026-05-25-020: Editor Scanner Cull

Problem: `Editor/NativeAliasSovereigntyScanner1300.cs` was an unused editor-only duplicate of the existing `Tools/VaultNativeAliasRoslynAudit` route, had no `.meta`, widened `Hecton8.AI.Cognition.Editor.asmdef` with Roslyn precompiled references, and made the current `.cs` count disagree with the raw ledger proof.
Solution: Deleted the duplicate scanner and restored the editor asmdef to `overrideReferences=false` with empty `precompiledReferences`. Kept the executable DTO layout guard because it has no Roslyn dependency and enforces the actual DTO offset invariant.
Rejected Alternatives: Keeping an untracked editor script without `.meta`; forcing editor Roslyn dependencies into the asmdef to preserve a duplicate menu item; rerunning dotnet/Unity build against the user's instruction to run builds rarely.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged. Editor compile surface is narrower, and proof artifacts now match the 18 scoped `.cs` files.
Hardware Impact: 0 runtime microseconds. Reduces editor/import compile risk only; no frame-loop performance claim.

## Decision 2026-05-25-021: Exact Mock Cognition Load Job

Problem: Task 16 specifically required a deterministic Burst job named `GenerateMockCognitionLoadJob`; the runtime implementation used `GenerateMockCognitionDataJob`, which made the code functional but the proof contract false.
Solution: Renamed the Burst `IJobParallelFor` to `GenerateMockCognitionLoadJob`, updated `UtilityAICognitionVault.TryScheduleMockData`, and synchronized raw/classified report owner labels. The job still writes deterministic fake states, AUPs, and targets over `math.max(states, aups, targets)` before bucket rebuild.
Rejected Alternatives: Leaving the wrong name and explaining it away; adding a duplicate wrapper job; claiming Unity/profiler stress execution without running Unity; launching dotnet/build against the user gate.
Scalability potential: Low uses the same bounded deterministic load without managed harness objects; Middle/High/Ultra can raise capacities through existing vault sizes and continuous quality routes without changing DTO truth ownership.
Hardware Impact: 0 hot runtime microseconds claimed. The correction removes a task-contract defect; full frame/drop/leak proof still requires a legal Unity profiler run.

## Decision 2026-05-25-022: Hot Unsafe Bypass Cull

Problem: APEX re-audit found raw pointer mutation where normal indexed `NativeArray<T>` assignment was enough: `CalculateAnxietyDecayJob` used `NativeDisableParallelForRestriction`, `NativeArrayUnsafeUtility.GetUnsafePtr`, and `UnsafeUtility.AsRef` for `States`/`Aups`; cold direct tuning/default clear helpers used raw pointer writes or `MemClear` without a real need.
Solution: Rewrote `CalculateAnxietyDecayJob` to copy DTOs into locals and write back through `States[index]` / `Aups[index]`, removed its `unsafe` and LowLevel.Unsafe dependency, replaced Utility/Anxiety direct tuning writes with `tuningBuffer[0] = tuning`, and replaced Apex spawn/default clear raw writes with indexed `default` assignment.
Rejected Alternatives: Keeping raw pointers for style; claiming `NativeDisableParallelForRestriction` is harmless when each worker already owns its row; launching dotnet/Roslyn/build during this pass when the user explicitly requested rare build use.
Scalability potential: Low removes avoidable safety bypasses on weak devices; Middle/High/Ultra keep the same DTO layout and hot-loop math while reserving remaining unsafe only for cold CSV/dump serialization and explicitly justified queue/budget lanes.
Hardware Impact: 0 measured hot runtime microseconds. The anxiety job keeps the same O(n) work and zero-GC behavior, but eliminates unnecessary unchecked pointer aliasing; Apex `MemClearArray<T>` is now a cold indexed loop, potentially slower only on non-frame default-clear paths.

## Decision 2026-05-25-023: AUP Hash And Shelter Grid Fail-Closed Numeric Guards

Problem: `UtilityAICognitionJobMath.HashAupCell` could cast non-finite or astronomically large scaled AUP coordinates to `long`, and `ResolveShelterMultiplier` multiplied corrupted shelter dimensions before proving the product fit in `int` and inside the SDF buffer.
Solution: `HashAupCell` now returns bucket `0` for non-finite scaled coordinates and clamps finite coordinates to a safe `long` cell range before hashing. `ResolveShelterMultiplier` now validates positive dimensions and division-based capacity bounds before computing `xy`, `required`, or the flattened cell index.
Rejected Alternatives: Trusting upstream AUP producers to be finite; wrapping the multiplication in `checked` and relying on managed exceptions; launching a build/Roslyn rerun during a user-requested rare-build pass.
Scalability potential: Low devices fail closed to neutral bucket/shelter multiplier on corrupted data; Middle/High/Ultra keep deterministic hashing for valid huge-world coordinates without changing gameplay truth ownership.
Hardware Impact: 0 measured microseconds. Adds a few scalar checks on bucket/hash and shelter sample paths; prevents NaN/corrupt-header faults from becoming undefined casts or invalid SDF indexing.

## Decision 2026-05-25-024: Cold CSV Resolver Fail-Closed Guard

Problem: Utility, Anxiety, and Apex editor CSV resolver methods built `Directory.GetCurrentDirectory()` / `Path.Combine(...)` paths before any local exception boundary, so a malformed project root could throw before the cold caller reached its normal `path == null` fail-closed branch.
Solution: Wrapped `ResolveCsvPath` / `ResolvePsychologyCsvPath` path composition in explicit `IOException`, `UnauthorizedAccessException`, `ArgumentException`, and `NotSupportedException` catch blocks that return `null`.
Rejected Alternatives: Leaving the issue because it is editor-only; adding broad `catch` in hot code; replacing cold CSV I/O with an unmanaged plugin without an owner or platform proof.
Scalability potential: Low/Middle/High/Ultra runtime cognition is unchanged. Editor tuning import now degrades to no-import on bad paths instead of breaking the tool chain.
Hardware Impact: 0 hot runtime microseconds. Adds cold editor exception tables only; no frame-loop cost.

## Decision 2026-05-25-025: Bounded Signal Budget Claim Restoration

Problem: `ShinobuApexBrainJobs.TryEnqueueBounded` atomically decremented writer budget before enqueue, but on a failed claim it only incremented the dropped counter. Sustained overload could push `writerBudget[0]` negative and keep later calls fail-closed even after capacity should recover.
Solution: Added an atomic restoration of `writerBudget[0]` in the failed-claim branch before incrementing `writerBudget[1]`. The success path still performs one decrement and one enqueue; the failure path now preserves the bounded queue invariant.
Rejected Alternatives: Allowing the frame reset to hide negative drift; replacing the bounded queue lane with managed buffering; editing the matching Core patterns outside the assigned domain without an integrator route.
Scalability potential: Low devices under signal pressure now degrade by counting drops without poisoning future capacity; Middle/High/Ultra retain the same writer contract and can spend capacity on richer signal streams.
Hardware Impact: Success path unchanged. Failed overload path pays one additional atomic increment; expected cost is below frame relevance and prevents prolonged false saturation on weak silicon.

## Decision 2026-05-25-026: Mock Anxiety Tuning Read-Only Alias Closure

Problem: `GenerateMockAnxietySpikesJob` only reads `Tuning`, but the field was declared `[NoAlias]` without `[ReadOnly]`. That weakens the Burst job signature contract from Task 10 and makes the job appear to have mutation authority it does not need.
Solution: Marked `Tuning` as `[ReadOnly, NoAlias]`. The job still reads through `AnxietyDecayJobMath.ReadTuning` and writes only `States` / `Aups`.
Rejected Alternatives: Leaving the mutable declaration because the job body does not write; converting tuning to a managed snapshot; rerunning dotnet/build during a pass where the user explicitly requested rare build use.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The alias contract is tighter for Burst and future safety scanning.
Hardware Impact: 0 measured microseconds. This is a correctness/contract fix, not a performance claim.

## Decision 2026-05-25-027: Paranoid No-Build Re-Audit Gate

Problem: The user requested another hard re-audit but also explicitly constrained dotnet/Unity builds to rare, necessary launches. A new build would add shared-machine contention without a new compile-triggering code patch.
Solution: Re-read the 1300 prompt/status/rationale, AGENTS, domain map, Unity skill, and six relevant mandates; ran source-only gates over runtime AI/Cognition: managed-risk patterns, native allocation constructors, class-level native field declarations, AUP downcast callsites, using/asmdef isolation, NativeDisable justification context, scoped diff check, and report hash revalidation.
Rejected Alternatives: Launching dotnet/Roslyn/Unity just to produce motion; editing Core signal-budget patterns outside the assigned domain; patching editor-only AUP gizmo visualization as runtime correctness work; treating method-local `NativeArray<T>` parameters as persistent aliases.
Scalability potential: Low avoids build contention and keeps cognition hot paths unchanged; Middle/High/Ultra retain the same DTO and vault contracts while the proof layer is stricter. No binary quality switch was introduced.
Hardware Impact: 0 runtime microseconds. This pass produced evidence only; no frame-loop code was changed.

## Decision 2026-05-25-028: Cold Managed I/O Boundary Honesty

Problem: A second paranoid scan still finds `new FileStream` and `new BinaryWriter` in runtime AI/Cognition. These are not frame-loop allocations, but claiming the entire runtime source is free of managed `new` would be false. The first classifier also undercounted one value-type job constructor because it missed the `Calculate` prefix.
Solution: Classified `new` occurrences by source role: `NEW_TOTAL=92`, `NEW_MANAGED_HOT_RISK=0`, `NEW_COLD_IO_OR_SPAN=17`, `NEW_VALUE_OR_JOB_DTO=75`, `NEW_UNCLASSIFIED=0`. Runtime hot proof is limited to no managed containers/classes, no text formatting, no boxing/interface markers, no prohibited scene/component APIs, no native allocation constructors, and no class-level native aliases. Cold dump/CSV I/O remains managed and guarded fail-closed.
Rejected Alternatives: Removing black-box dumps to make text scans prettier; replacing cold file I/O with an unmanaged platform plugin without an owner route; calling cold managed I/O Zero-GC runtime proof.
Scalability potential: Low/Middle/High/Ultra frame loops keep identical unmanaged DTO/vault routes. Failure evidence remains available on all tiers; future platform-owned unmanaged dump writer can replace the cold I/O boundary without changing cognition DTO truth.
Hardware Impact: 0 runtime microseconds. No code patch was made; this is evidence hygiene and limitation disclosure.

## Decision 2026-05-25-029: Case-Sensitive Static Scanner Discipline

Problem: PowerShell `Select-String` is case-insensitive by default. A no-build LINQ scan reported 195 `.Select` matches because it matched Unity.Mathematics `math.select`, not LINQ `.Select`.
Solution: Re-ran formatting/LINQ/boxing scans with `-CaseSensitive`. Correct result: `System.Linq=0`, `Enumerable=0`, `.Where=0`, `.Select=0`, `.Any=0`, `.FirstOrDefault=0`, `.ToList=0`, `.ToArray=0`, `string.Format=0`, `.ToString=0`, `foreach=0`, boxing/interface markers=0.
Rejected Alternatives: Reporting the noisy 195 count as a real risk; suppressing `math.select`; launching Roslyn/dotnet solely to compensate for a scanner option mistake.
Scalability potential: No runtime behavior change. Better static discipline prevents false remediation work on all tiers.
Hardware Impact: 0 runtime microseconds. Evidence correction only.

## Decision 2026-05-25-030: Semantic Native Field Re-Audit

Problem: A broad `NativeArray` occurrence scan returned 166 matches because it included method parameters and helper signatures. Treating that as a field ledger would create false debt. A separate suspicion that `LeviathanStalkJob.SensoryStimuli` lacked read-only protection also needed line proof before any code patch.
Solution: Re-ran a strict runtime field-declaration scan anchored to access modifiers and terminating semicolons. Initial result was superseded by Decision 2026-05-25-035 after `NativeQueue<T>.ParallelWriter` and containing-type classification were corrected. Inspected `LeviathanStalkJob.cs:17-22`; `SensoryStimuli` is already `[ReadOnly, NoAlias]`, while writable arrays are `States` and `SteeringOutputs`.
Rejected Alternatives: Patching an already correct job signature; reporting the 166 broad occurrence count as persistent-field evidence; launching dotnet/Unity solely for a source-only semantic check.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The proof layer is stricter and avoids fake remediation work that could destabilize the cognition path.
Hardware Impact: 0 runtime microseconds. Evidence correction only; no frame-loop code was changed.

## Decision 2026-05-25-031: Changed-Line Zero-GC Evidence Gate

Problem: Whole-file scans can hide whether the current patch itself introduced a forbidden hot-path construct. The previous runtime `new` classifier also needed exact treatment for stack-only `Span<byte>` / `ReadOnlySpan<byte>` and value structs such as `ApexInfluenceNode` / `SdfSample`.
Solution: Re-ran the runtime `new` classifier with those types explicitly classified. Result: `NEW_TOTAL=92`, `NEW_COLD_IO_OR_SPAN=17`, `NEW_VALUE_MATH=56`, `NEW_VALUE_OR_JOB_DTO=19`, `NEW_MANAGED_HOT_RISK=0`, `NEW_UNCLASSIFIED=0`. Then scanned added `git diff` lines in changed cognition files for managed containers, delegates, LINQ, string formatting/concat/interpolation, `new Native*`, global/scene/component polling, `.Complete()`, `Debug.Log`, and runtime `throw`; result was zero hits.
Rejected Alternatives: Treating unclassified scan rows as harmless without line proof; launching dotnet/Unity for a pure text-diff question; editing runtime code when the evidence issue was scanner classification only.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra keep the same hot path; the proof now separates old cold I/O boundaries from patch-added risk.
Hardware Impact: 0 runtime microseconds. Evidence correction only.

## Decision 2026-05-25-032: Task17 Public-Route Defrag Fuzzer

Problem: Task 17 demanded continuous `GlobalDataVault.TryRunLiveCompactionSlice` pressure while AI jobs execute, but that method is private. Calling it through reflection would fabricate test coverage and bind cognition code to a private Core implementation detail.
Solution: Added editor-only `AICognitionMemorySovereigntyValidator1300`. It creates an isolated `GlobalDataVault`, acquires cognition/anxiety handles, locks every required buffer, opens a background worker, requests public editor defrag ticks through `RequestEditorForceDefragmentation` and `FrostTickDefrag(PreSimulation, ActiveBurstLockMask)` while mock cognition/anxiety jobs are scheduled, then validates public relocation through `GenerateMockVaultRelocationForValidation` and read-only handle readback.
Rejected Alternatives: Reflection/private `TryRunLiveCompactionSlice`; a runtime fuzzer thread in shipping code; claiming Unity execution without running the menu validator; launching dotnet/Unity against the user's rare-build constraint.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged because the validator is `#if UNITY_EDITOR`. The proof path stresses the same vault handles and mock load jobs used by scalable cognition data lanes without changing gameplay truth ownership.
Hardware Impact: 0 runtime microseconds. Editor-only thread/job/defrag pressure exists only when the menu validator is run. Unity execution remains pending; this entry records static implementation and source-level verification, not profiler proof.

## Decision 2026-05-25-033: Task17 Partial-Schedule Lock Lifetime Closure

Problem: The editor fuzzer used compound scheduling conditions. If an early scheduler returned success and a later scheduler returned failure, a `JobHandle` could remain active while the method returned through `finally` and released vault write locks.
Solution: Split every schedule call into a sequential step, track a single active `JobHandle`, and complete it in `finally` before any `ReleaseWriteLock` call. Also replaced the worker fault `bool` with an `int` `Volatile` flag to avoid profile/AOT ambiguity.
Rejected Alternatives: Assuming schedule failure never leaves prior jobs active; releasing locks before a safety fence to make the fuzzer shorter; running Unity/dotnet immediately despite the user's rare-build constraint.
Scalability potential: Low/Middle/High/Ultra runtime paths are unchanged. The editor validator now preserves the same lock-lifetime invariant it is supposed to test.
Hardware Impact: 0 runtime microseconds. Editor-only failure paths may wait for one active handle before cleanup; that is required correctness, not frame-loop work.

## Decision 2026-05-25-034: ARM64 Private Wide Pad Closure

Problem: The DTO byte-map still contained private `ulong` and `ushort` `_pad` fields in `AlphaLeviathanCognitionState` and `AlphaLeviathanSteeringOutput`. Sizes were `mod8=0`, but the user's ARM64 rule explicitly requires padding after byte/flag regions to be byte padding, not wide fake fields.
Solution: Converted the two Alpha Leviathan tail padding regions to explicit private byte pads, updated `AICognitionLayoutGuard1300` to assert the new pad names and terminal offsets, regenerated `Docs/Reports/DTO_OFFSET_MAP_1300.txt`, and updated `VAULT_EXORCISM_REPORT_1300.json` with current source and DTO audit hashes. The Core edit was limited to private padding fields in a Core AI contract already consumed and layout-validated by the AI/Cognition 1300 guard.
Rejected Alternatives: Leaving wide private pads because `[FieldOffset]` already fixes size; moving semantic fields in a shared Core contract; broad cross-domain refactor; launching dotnet/Unity after a source-only padding patch against the user's rare-build constraint.
Scalability potential: Low/Middle/High/Ultra share one deterministic DTO stride and one byte-accurate proof artifact. No quality tier changes layout, save identity, or authority route.
Hardware Impact: 0 runtime microseconds measured. The change removes a concrete ARM64 layout-proof defect and keeps the 192-byte and 128-byte strides unchanged; no hot-loop work was added.

## Decision 2026-05-25-035: Runtime Native Field Classifier Correction

Problem: The no-build native-field classifier had two proof defects: one variant missed `NativeQueue<T>.ParallelWriter`, and another variant classified `*VaultBuffers` fields as persistent because it ignored the containing struct name. That made the report internally noisy.
Solution: Re-ran the classifier with both generic `NativeQueue<T>.ParallelWriter` matching and containing-type classification. Current runtime `AI/Cognition` excluding `Editor` result is `FIELD_DECL_TOTAL=102`, `PERSISTENT_CLASS_ALIAS_FIELDS=0`, `VAULT_VIEW_FIELDS=39`, `JOB_VIEW_FIELDS=63`, `OTHER_FIELDS=0`.
Rejected Alternatives: Treating broad `NativeArray` occurrence counts as field proof; launching dotnet/Roslyn solely to repair a deterministic text classifier; hiding the old count instead of recording the correction.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra keep the same vault/job topology; the proof now matches source structure instead of scanner noise.
Hardware Impact: 0 runtime microseconds. Evidence correction only.

## Decision 2026-05-25-036: Editor AUP Gizmo Local-Origin Correction

Problem: Two editor gizmo paths passed absolute `CognitionAupDTO.AUP` values directly to `DowncastLocalDeltaClamped`, which is semantically wrong even though it is editor-only visualization.
Solution: `AIAnxietyTunerWindow.cs` and `CognitionUtilityTunerWindow.cs` now resolve the first finite cognition AUP as an editor-local origin and call `LocalDeltaDouble(entityAUP, originAUP)` before clamped float downcast. Runtime cognition code already used local delta routes and was not changed.
Rejected Alternatives: Leaving editor visualization as an exception; downcasting absolute AUP because the clamp hides huge values; introducing scene/camera polling as the origin source.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. Editor gizmos now display stable local offsets without corrupting AUP doctrine.
Hardware Impact: 0 runtime microseconds. Editor-only gizmo pass adds one bounded scan over at most 256 AUP rows.

## Decision 2026-05-25-037: Signal Budget Safety Comment Closure

Problem: `ApexBrainJob` had three `NativeDisableParallelForRestriction` writer-budget arrays next to well-documented MPSC writer lanes, but the budget fields themselves relied on neighboring comments for review context.
Solution: Added adjacent safety comments for `ProximitySignalWriterBudget`, `CombatDamageSignalWriterBudget`, and `PanicSignalWriterBudget`. Each lane is documented as an externally reset two-slot counter mutated only through `Interlocked` inside `TryEnqueueBounded`.
Rejected Alternatives: Leaving the proof implicit; duplicating full three-paragraph blocks for identical counter lanes; removing the restriction and breaking parallel bounded signal emission.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The budget proof is explicit enough for future reviewers without widening dependencies or adding managed queues.
Hardware Impact: 0 runtime microseconds. Comment/proof correction only.

## Decision 2026-05-25-038: Final No-Build Hash And Gate Sanity

Problem: A fresh local hash command initially reported a mismatch against `VAULT_EXORCISM_REPORT_1300.json` because the ad-hoc script hashed backslash paths instead of the report's slash-normalized relative-path method. `Status_1300.md` also still contained old "current" hash values from earlier loops.
Solution: Re-ran the canonical hash route using `Resolve-Path -Relative`, slash-normalized sorted paths, NUL separators, and file bytes. It matches the JSON current fields: full `99ef7a6599a37eddf574d9b1c6c47c55bc015ae7092cafa65e17970097c8d106`, runtime `d6d9cb36db1a6837a67a191f080dc165d96b4bc998515b813bfab6101d5442ff`, DTO `3d8df4cbed93f9ab2abc519de897d59719ec80f635c01f46cdbede968010842a`. Updated status/report/log proof text and reran source-only gates.
Rejected Alternatives: Trusting the first broken hash command; launching dotnet/Roslyn/Unity for a deterministic path-normalization issue; editing runtime code without a runtime defect.
Scalability potential: No runtime behavior change. Low/Middle/High/Ultra keep the same DataVault/job/DTO topology; the proof layer now has one reproducible hash route.
Hardware Impact: 0 runtime microseconds. Evidence correction only; no frame-loop code changed.

## Active Constraints

- No persistent `NativeArray<T>`, `NativeSlice<T>`, `NativeList<T>`, `NativeHashMap<K,V>`, `NativeQueue<T>`, or raw pointer fields in scoped runtime AI cognition code unless the code proves it is inside authorized transient vault/job structures.
- Read accessors must stay pure: no allocation, no global polling, no job completion, no scene search.
- DTO edits require explicit ARM64 layout math and size proof.
- Runtime readiness cannot be claimed without fresh Unity/profiler/GCMonitor proof.
