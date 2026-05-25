# Rationale UNKNOWN - 13XX NativeArray / Native Ownership Audit

Date: 2026-05-25
Agent: UNKNOWN
Scope: documentation/reporting audit, no runtime source edits.

## Decision 01 - Treat NativeArray As Ownership Problem, Not Keyword Problem

Problem: User asked whether the `NativeArray` work was actually fixed. A raw text count of `NativeArray` is misleading because job structs, method-local vault views, editor validators, and core memory authority legitimately use `NativeArray`.

Solution: Classify each finding by ownership shape: persistent runtime alias, method-local vault view, job parameter, core authority allocation, raw pointer export, black-box dump route, or residual managed crash IO.

Rejected Alternatives: Banning every `NativeArray` token would falsely reject Burst/job code and DataVault read/write views. Trusting agent `STATIC_PASS` labels would miss current source conflicts and residual debt.

Scalability potential: Low devices benefit from DataVault-owned, bounded, cache-local buffers; high/ultra devices can still spend saved CPU on visual overkill without changing gameplay truth ownership.

Hardware Impact: No runtime code changed. The audit prevents false cleanup claims that would keep stale native aliases alive on i3/MX350-class hardware.

## Decision 02 - Promote Chat Audit Into Stable Reports

Problem: The previous answer existed in chat and `LOG_UNKNOWN.md`, but the user explicitly requested docs/reports. Integrator-facing evidence must live under `Docs/Reports`.

Solution: Add a markdown report for human review and a JSON summary for exact counters, verdicts, and residual classes.

Rejected Alternatives: Only appending `LOG_UNKNOWN.md` is insufficient because reports are the project evidence surface. Editing architectural policy docs was rejected because this is evidence, not new policy.

Scalability potential: Stable reports let future agents burn down real residuals instead of re-auditing the same surface.

Hardware Impact: No runtime impact. It reduces integration churn and wrong work on native memory routes.

## Decision 03 - Do Not Run Fresh Heavy Roslyn Scan Under CPU Load

Problem: The latest full ledger was not perfectly fresh because source files changed after `05/25/2026 21:31:29`. A fresh scan would improve precision, but CPU sample was 78.8%.

Solution: Mark the ledger freshness caveat explicitly and defer the heavy scan until the CPU/dotnet guard allows it.

Rejected Alternatives: Running a heavy scanner under current load violates the project build/load guard. Pretending the ledger is fully fresh would be false.

Scalability potential: Keeps concurrent agent work from fighting over CPU while preserving a clear rerun task.

Hardware Impact: Avoids adding transient load while other agents are active. No runtime change.

## Decision 04 - Update Stale 13XX Facts Instead Of Preserving The Earlier Snapshot

Problem: New reports landed after the first audit. Keeping the old 1305 claim would be false because `TerrainChunkPagerRuntime` raw pointer fields were removed by patch pass 10. Keeping the old 1313 claim would also be false because the active monolith blob now static-validates current format/schema.

Solution: Reparse the newer artifacts and update both markdown and JSON audit reports. This decision was superseded by the later `2026-05-25 23:16:10` full ledger: `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` with `2420` scanned files, `1784` persistent candidates, `364` MonoBehaviour candidates, and `0` parse failures. 1305 is now partial, not unfixed: terrain pager pointer fields are gone, but lifetime locks, managed dump IO, and 6 world residency native containers remain.

Rejected Alternatives: Reporting only the old snapshot would be stale. At that checkpoint, claiming 1305 fixed would have been wrong because `WorldChunkResidencyManager` still owned direct native containers and `TerrainChunkPagerRuntime` still had runtime-long locks and managed dump IO. This was later superseded by Decision 07: fresh `00:55` audit reports `WorldChunkResidencyManager.cs=0`, while terrain pager lock/alias proof debt remains.

Scalability potential: Correct residual ownership prevents agents from burning time on already-removed pointer fields and redirects work toward the remaining residency owner route.

Hardware Impact: No runtime code changed. The report reduces integration risk on low-end devices by pointing to the actual remaining native ownership pressure.

## Decision 05 - Keep Scripts Map Split Across Markdown, JSON, And TSV

Problem: The user requested a full `Scripts` map. The folder contains 326 directories, 5579 files, and 2420 C# files; dumping every file into the chat or one giant prose section would be unreadable.

Solution: Write `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.md` for the human directory map, `Docs/Reports/SCRIPTS_FOLDER_MAP_UNKNOWN.json` for structured consumers, and `Docs/Reports/SCRIPTS_FILE_INDEX_UNKNOWN.tsv` for every-file grep/spreadsheet use.

Rejected Alternatives: Chat-only output loses the evidence trail. Markdown-only every-file listing would bury the architectural map under thousands of rows.

Scalability potential: Future agents can identify domain size, asmdef boundaries, recent edits, and large files before touching architecture.

Hardware Impact: No runtime code changed. The map reduces accidental cross-domain edits under concurrent agent load.

## Decision 06 - Measure Burndown With Comparable Full Ledgers Only

Problem: The user asked how fast agents are removing forbidden persistent and forbidden MonoBehaviour native aliases. Some artifacts are scoped and can show zero because they scan one file or one domain, so comparing them against full-project ledgers would produce a false speed claim.

Solution: Use only full-project ledgers with comparable scanned-file counts and zero parse failures. The latest valid full ledger is `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` at `2026-05-25 23:16:10`, with `2420` scanned files, `1784` forbidden persistent candidates, and `364` forbidden MonoBehaviour candidates. The late active rate from `21:32:49` to `23:16:10` is `-69` persistent candidates and `-39` MonoBehaviour candidates, or `40.05/hour` and `22.64/hour`.

Rejected Alternatives: Treating every `NativeArray` token as debt would destroy valid Burst/job paths. Treating scoped zero reports as global proof would hide remaining owners like `HectonVoxelEngine.cs`, `VegetationMemoryPool.cs`, and `PlayerInventory.cs`.

Scalability potential: Correct classification keeps low-end devices protected from persistent alias and MonoBehaviour lifetime bugs while preserving high-end Burst throughput from legitimate transient native views.

Hardware Impact: No runtime code changed. The report prevents wrong cleanup work that would either leave stale aliases alive or remove valid data-local job memory.

## Decision 07 - Reject Stale X_000 Ledger And Run A Fresh Current Ledger

Problem: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json` was updated at `2026-05-26 00:47:20` and reported `2138` persistent / `581` MonoBehaviour candidates, but representative source checks contradicted it. It reported old pointer/native fields in `TerrainChunkPagerRuntime.cs` and `DroneFleetManager.cs` that no longer exist at the reported lines.

Solution: Under CPU/dotnet guard (`CPU=16.5%`, no compiler/dotnet process shown), run the prebuilt `Tools/VaultNativeAliasRoslynAudit/bin/Debug/net10.0/VaultNativeAliasRoslynAudit.exe` without rebuilding. New artifact: `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`, with `2421` scanned files, `0` parse failures, `1770` forbidden persistent candidates, `358` forbidden MonoBehaviour candidates, and hash `68217d9f155aeb5233cbb3cc004518df4a1eb2c1d0d222bd810ca241008bbe31`.

Rejected Alternatives: Accepting the stale `X_000` result would falsely report a massive regression. Ignoring it without rerun would leave the report unprovable. Running a rebuild was unnecessary and outside the requested check.

Scalability potential: Current proof keeps cleanup pressure on real residual owners while avoiding rollback panic over an invalid artifact.

Hardware Impact: No runtime code changed. Offline audit cost only; no build or player execution.

## Decision 08 - Fix Build From Compiler Evidence, Not From Search Guesswork

Problem: The first guarded build failed with `75` errors and `5` warnings in `Docs/Reports/BUILD_UNKNOWN_20260526_010802.log`. Failures spanned stale APIs, removed native owner fields, C# language constraints, missing imports, and warning-only build hygiene.

Solution: Repair only compiler-proven defects and re-run the guarded build after each meaningful batch. Build commands used `/m:1 /nr:false /p:UseSharedCompilation=false` to avoid persistent workers under concurrent agent load.

Rejected Alternatives: Suppressing diagnostics or excluding files would make the build green by hiding broken architecture. Reintroducing removed persistent native arrays for drone transactions was rejected because it would undo the NativeArray ownership work.

Scalability potential: A clean build is prerequisite proof for any later low/mid/high/ultra runtime scaling. No quality tier behavior was changed.

Hardware Impact: Runtime microseconds saved claimed: `0`. This was compile integrity work, not measured frame-time optimization.

## Decision 09 - Repair Drone Transactions Through Current Buffer Ownership

Problem: `DroneFleetManager_Transactions.cs` referenced removed static native arrays and stale `MirrorDroneSoA` call shapes. Re-adding those arrays would restore forbidden persistent aliases in a hot MonoBehaviour-adjacent system.

Solution: Use existing drone core and mirror buffer accessors for prepare/apply paths, copy transaction debug task snapshots from bounded views, and release read/write buffers in `finally` blocks.

Rejected Alternatives: Direct static native fields were rejected as a regression against the DataVault/owner-route doctrine. Method-local unmanaged views without release guards were rejected because they are fragile under exceptions.

Scalability potential: Low-tier devices keep one authoritative route and avoid duplicated persistent native storage; high/ultra tiers can scale drone visuals through mirror data without changing truth ownership.

Hardware Impact: Runtime microseconds saved claimed: `0` because no profiler measurement was taken. Expected effect is correctness and ownership preservation, not a measured speed claim.

## Decision 10 - Fix Warnings Instead Of Accepting A Noisy Build

Problem: Intermediate build passed but still emitted warnings: function pointer comparison (`CS8909`), obsolete Unity object search (`CS0618`), unused exception locals (`CS0168`), and missing stale `Hecton8.Input.csproj` reference (`MSB9008`).

Solution: Compare function pointers through `IntPtr`, switch editor discovery to the current Unity overload, remove unused exception variables, and conditionally remove the missing project reference when the project file is absent.

Rejected Alternatives: Warning suppression was rejected because the user explicitly requested warnings fixed. Deleting the input reference unconditionally was rejected because it could break machines where the project file exists.

Scalability potential: Warning-clean builds reduce integration noise and make future real regressions visible across device profiles.

Hardware Impact: Runtime microseconds saved claimed: `0`. Build hygiene only.

## Decision 11 - Keep Build Report Separate From Native Ownership Audit

Problem: Native ownership reports and build repair evidence solve different questions. Mixing them would make proof hard to audit.

Solution: Add `Docs/Reports/BUILD_REPAIR_UNKNOWN_20260526.md` and `.json`, then append status/log entries pointing to the final build log and proof lines.

Rejected Alternatives: Chat-only reporting was rejected. Expanding architecture docs with compile-fix details was rejected as documentation noise.

Scalability potential: Future agents can distinguish architecture debt from compile debt and avoid re-breaking clean build status.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 12 - Recheck Build And Promote The Result Into Root Docs

Problem: The user challenged whether the build claim was exact. Several active root/stable docs still cited old EXTERNAL_CODEX compile-wall facts (`NETSDK1004`, CPU/compiler block) as current compile boundary.

Solution: Re-run full `Hecton8.slnx` under the build guard (`CPU=2`, compiler process count `0`). New proof: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_013709.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`. Then update root/stable documentation with that exact CLI boundary while preserving runtime proof as pending.

Rejected Alternatives: Reusing `BUILD_UNKNOWN_20260526_012406.log` without rerun would not answer the challenge. Marking Unity/runtime readiness from `dotnet build` was rejected because AGENTS forbids runtime readiness from CLI compile.

Scalability potential: Clean compile proof unblocks later low/mid/high/ultra validation work; it does not prove frame-time scalability.

Hardware Impact: Runtime microseconds saved claimed: `0`. Documentation and compile proof only.

## Decision 13 - Close Documentation Gates Instead Of Reporting Partial Doc Refresh

Problem: After the root/stable doc update, `VerifyDocStructure.py` still failed on one report with untagged fences plus non-BOM active docs, and `OOP_Doc_Scanner.py` failed on two over-threshold architecture lines.

Solution: Add language tags to the two fenced blocks, split the two architecture lines without changing technical meaning, and mechanically convert the 92 active docs listed by the validator to UTF-8 BOM. Re-run both validators.

Rejected Alternatives: Reporting only the build proof would leave red documentation gates. Editing unrelated architecture policy was rejected; only validator-proven doc hygiene issues were changed.

Scalability potential: No runtime effect. Clean docs gates reduce false stale-boundary work for later agents.

Hardware Impact: Runtime microseconds saved claimed: `0`.

## Decision 14 - Repeat Build Proof After Second Challenge

Problem: The user challenged the compile/documentation claim again. Other agents can change source between replies, so the previous `013709` build log could become stale.

Solution: Re-run the full `Hecton8.slnx` build under guard (`CPU=5`, compiler process count `0`). New proof: `Docs/Reports/BUILD_UNKNOWN_RECHECK_20260526_020504.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`. Update root/stable docs from the `013709` proof to the `020504` proof.

Rejected Alternatives: Re-answering from prior proof was rejected because the user asked for another check. Running Unity runtime/player validation from a CLI compile request was rejected because it is a different gate.

Scalability potential: Clean compile remains prerequisite evidence only; it does not prove runtime frame-time scaling.

Hardware Impact: Runtime microseconds saved claimed: `0`.
