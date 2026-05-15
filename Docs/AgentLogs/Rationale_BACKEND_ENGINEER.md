# Rationale - BACKEND_ENGINEER - ITEM_CATALOG_FNV_GEN

## Decision 001 - Current Prompt Supersedes Stale BACKEND_ENGINEER Files

Problem: `Status_BACKEND_ENGINEER.md` and `Rationale_BACKEND_ENGINEER.md` contained `ITEM_RECIPE_GRAPH_AUDITOR` content from a prior prompt.
Solution: Reported `[HYGIENE_VIOLATION]`, then replaced the files with `ITEM_CATALOG_FNV_GEN` state to prevent stale task contamination.
Rejected Alternatives: Continuing to append under the old task would corrupt evidence; waiting would leave the explicit hash-generation task unexecuted.
Scalability potential: Low/Middle/High/Ultra unaffected; this is offline state hygiene.
Hardware Impact: 0 us/frame on i3/MX350 because no runtime path is touched.

## Decision 002 - Match Existing Runtime Hash Owners

Problem: Project code uses more than one FNV-1a variant: item IDs use `LocHash.Compute`, biome family IDs use `ComputeAsciiLowerInvariant`, and signal lanes use `ComputeStableSignalLaneHash`.
Solution: The verifier records the hash mode per category and emits constants matching the current runtime owners.
Rejected Alternatives: Forcing one universal byte-wise hash would desynchronize generated constants from live consumers.
Scalability potential: Low tier pays no runtime hashing; High/Ultra can use richer authored IDs without extra gameplay cost.
Hardware Impact: Removes repeated runtime hash dependence where constants are used later; current change itself saves 0 us/frame until consumers adopt the constants.

## Decision 003 - Constants Only In Generated CSharp

Problem: A generated helper with scanners/dictionaries/static constructors would add runtime initialization and allocation risk.
Solution: `H8Hashes.cs` is generated as `const string`, `const uint`, and `const int` only.
Rejected Alternatives: Runtime FNV methods, arrays, reflection, or dictionaries in the generated file.
Scalability potential: Low uses zero initialization cost; Ultra gets the same deterministic identity table and can spend cycles on visuals elsewhere.
Hardware Impact: 0 B/frame and 0 us/frame; compile-time constants only.

## Decision 004 - Dedupe Same ID Across Evidence Paths

Problem: The first generated pass emitted duplicate constants when the same string appeared both as an authored asset ID and a code literal or signal bus usage.
Solution: Changed the verifier to emit one record per category/value/hash-mode, with source priority favoring authoritative authored IDs and `ISignal` structs over consumers.
Rejected Alternatives: Keeping suffixed duplicate constants would bloat the table and make hand review weaker without adding identity coverage.
Scalability potential: Low avoids larger generated metadata; High/Ultra unaffected because constants compile away.
Hardware Impact: 0 us/frame direct runtime impact; reduces source and compile surface by eliminating redundant constants.

## Decision 005 - Compiler Status Downgraded To Static Evidence

Problem: The workspace has no `.sln`/`.csproj`, `dotnet` is not installed, and `csc` is unavailable in PATH.
Solution: Ran collision verification, generated-file hot-path static scan, and brace/identifier sanity checks; reported Unity/CLI compile as unavailable instead of claiming it.
Rejected Alternatives: Faking compile success or adding a temporary project to force a synthetic build would not prove Unity assembly integration.
Scalability potential: Low/Middle/High/Ultra unaffected; this is evidence classification.
Hardware Impact: 0 us/frame.

## Decision 006 - Include Authored Item Names, Not Only Persistent IDs

Problem: The prompt explicitly says "Item Names"; the first pass covered item persistent IDs and code literals but did not include authored display names or localized item-name table keys.
Solution: Expanded the verifier to scan `legacyItemName` and `localizedItemName.tableKey` from active `ItemData` assets, then regenerated `H8Hashes.cs`.
Rejected Alternatives: Treating persistent IDs as the only item names would leave prompt coverage ambiguous.
Scalability potential: Low still uses compile-time constants; High/Ultra can expose richer item name hooks without runtime string hashing.
Hardware Impact: 0 us/frame; generated constants only.

## Decision 007 - Compile Proof Via .NET Framework CSC

Problem: Earlier compile proof was missing because `dotnet` and PATH `csc` were unavailable. Visual Studio Roslyn `csc.exe` exists but hung without producing an assembly.
Solution: Killed the orphan Roslyn process and compiled `H8Hashes.cs` with `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`; the generated file compiled as a standalone library and the temp DLL was deleted.
Rejected Alternatives: Leaving the task at static syntax checks or claiming Unity import without an Editor run.
Scalability potential: Low/Middle/High/Ultra unaffected; compile proof only.
Hardware Impact: 0 us/frame.

## Decision 006 - Preserve Later BACKEND_ENGINEER State While Closing Recipe Audit

Problem: `Status_BACKEND_ENGINEER.md` and this rationale file were reused by a later `ITEM_CATALOG_FNV_GEN` prompt, while the active user thread still required `ITEM_RECIPE_GRAPH_AUDITOR` closure.
Solution: Appended a labeled continuation section instead of deleting the later hash-generation evidence.
Rejected Alternatives: Reverting the state files would destroy another prompt's audit trail; creating only chat evidence would violate the reporting protocol.
Scalability potential: Low/Middle/High/Ultra unaffected; this is evidence hygiene.
Hardware Impact: 0 us/frame.

## Decision 007 - Generate Items.csv From Existing Economy Truth

Problem: The recipe-auditor prompt required `Items.csv`, but the flat manifest was absent while equivalent truth existed in `Recipes.json.item_values` and `Resource_Distribution_Matrix.csv`.
Solution: Added `Tools/EconomyItemsCsvBake.py`, generated `Data/Economy/Items.csv`, and expanded `Tools/EconomyValidator.py` to prove all 55 rows, item hashes, category hashes, source recipe hashes, raw resource biome counts, and crafted source parity.
Rejected Alternatives: Manual CSV authoring was rejected because it invites drift; leaving the manifest absent kept the auditor in a risk state.
Scalability potential: Low tier can import a flat manifest without joining JSON and CSV at runtime; High/Ultra can use the same offline manifest to drive richer item UI without runtime string hashing.
Hardware Impact: Estimated 5-20 us saved per item lookup burst if importers consume baked hashes instead of recomputing strings; current change adds 0 us/frame.

## Decision 008 - Close Normal Inventory Capacity Bypass

Problem: Static audit showed the bulk transfer job gates weight/volume, but the normal inventory add path could accept quantity without first proving mass/volume capacity.
Solution: Patched `PlayerInventory.TryAddItemWithStateInternal` and `CanAcceptQuantity*` helpers to resolve capacity-limited quantities from current physical totals plus runtime item mass/volume before mutation.
Rejected Alternatives: Patching only bulk transfer was rejected because player pickup and direct insertion still needed a physical capacity gate. Changing public inventory APIs was rejected because this batch forbids unnecessary interface churn.
Scalability potential: Low uses the cheapest scalar capacity math; Middle/High/Ultra can spend saved correctness budget on richer inventory presentation without changing the backend gate.
Hardware Impact: Command-path O(N) scan over fixed inventory anchors only; no Tick/FixedTick work. Estimated hot-frame impact 0 us/frame.

## Decision 009 - Save Hash Width Is Compatible But Signed

Problem: Generated item hashes are displayed as unsigned 32-bit values, and 27 exceed `Int32.MaxValue`, while `SaveData.cs` stores `itemHashIds` as `int[]`.
Solution: Classified this as same bit-length storage because runtime item identities are already `int` hash IDs; documented the signed representation risk instead of mutating SaveData DTOs mid-batch.
Rejected Alternatives: Switching DTOs to `uint[]` would be a public serialization-contract change requiring migration proof and broader save-system ownership.
Scalability potential: Low/Middle/High/Ultra unaffected; this is persistence ABI discipline.
Hardware Impact: 0 us/frame.

## Decision 010 - Verification Boundary Kept Static

Problem: Economy CLI gates pass, but this shell has no `dotnet` command and no Unity executable on PATH or in common install roots.
Solution: Reported `ECONOMY SECURED` for the static economy audit only, with Unity compile/Play Mode/profiler still pending.
Rejected Alternatives: Claiming Unity compile success without the executable would be fake evidence.
Scalability potential: Low/Middle/High/Ultra pending Unity runtime validation.
Hardware Impact: Runtime profiler impact unmeasured in this shell; static analysis shows 0 B/frame additions in hot paths.

## Decision 011 - Add Economy Regression Tests And Fail CI On Risk Findings

Problem: The audit report could mark unresolved risk findings as pending while the process still exited 0, no dedicated regression test locked the new item manifest/DAG/scarcity/negative-case coverage/capacity-gate ordering, and the graph audit only checked that `Items.csv` existed instead of validating its contents directly.
Solution: Added `Tools/test_economy_integrity.py`, changed `Tools/EconomyRecipeGraphAudit.py` to return nonzero when either blocking findings or risk findings exist, and moved direct `Items.csv` manifest validation into the graph audit. The test suite now asserts the normal add capacity gate appears before stack and slot mutation in `PlayerInventory.cs`, and that item hash corruption is detected.
Rejected Alternatives: Depending on human review of the Markdown report was rejected because it allows CI to miss a pending-risk state. Depending only on the separate validator was rejected because the requested audit report must analyze both `Recipes.json` and `Items.csv`. Adding runtime Unity tests was not possible in this shell because no Unity Editor executable exists.
Scalability potential: Low/Middle/High/Ultra benefit indirectly because the economy import data remains deterministic and offline; no runtime work is added.
Hardware Impact: 0 us/frame. Tests and audit execution are offline CLI only.

## Decision 012 - Audit Effective Item Physical Metadata, Not Raw YAML Zeros

Problem: Static grep found `Data_AbyssalCrystal.asset` with serialized `massKg: 0` and `volumeM3: 0`, which looked like a capacity bypass. The asset has `autoResolvePhysicalMetadata: 1`, so runtime `ItemData.MassKg` and `VolumeM3` derive positive values from item weight/category.
Solution: Added runtime-equivalent physical metadata analysis to `Tools/EconomyRecipeGraphAudit.py` and a regression test proving no item has nonpositive effective mass/volume. The report records `Data_AbyssalCrystal` as serialized-zero-but-auto-resolved instead of treating it as a live exploit.
Rejected Alternatives: Editing the asset YAML was rejected because it would fight the authored auto-resolve contract and risk unrelated asset churn. Raw string grep was rejected because it ignores property-level runtime behavior.
Scalability potential: Low tier gets enforceable physical capacity data with no frame cost; High/Ultra can build richer inventory physics from the same positive metadata.
Hardware Impact: 0 us/frame. Offline audit only.

## Decision 011 - Preserve Concurrent Status Text And Reassert Hash Prompt At Bottom

Problem: `Status_BACKEND_ENGINEER.md` and `Rationale_BACKEND_ENGINEER.md` now contain a later `ITEM_RECIPE_GRAPH_AUDITOR` continuation in the same BACKEND_ENGINEER files.
Solution: Preserve the other block and append a bottom reconciliation section for `ITEM_CATALOG_FNV_GEN` so the latest on-disk state answers the current user prompt.
Rejected Alternatives: Deleting the other block could erase concurrent work; leaving the bottom state as recipe-auditor text would violate strict prompt isolation for the hash task.
Scalability potential: Low/Middle/High/Ultra unaffected; this is batch evidence hygiene.
Hardware Impact: 0 us/frame.

## Decision 012 - Harden Signal Coverage And Prevent Generated Self-Ingestion

Problem: The prior scanner covered `ISignal` structs and `SignalBus<T>` lane names, but missed authored Atlas/narrative IDs such as `atlas6_signal_identified`, `atlas6_core_message`, quest trigger IDs, and discovery IDs. It also scanned all first-party scripts without excluding the generated `H8Hashes.cs`, which could let output become future input.
Solution: Added explicit generated-output exclusion and expanded `scan_signal_records` to collect authored YAML signal/message/discovery/trigger/completion/quest IDs plus C# const IDs whose names identify signal/message/discovery/marker/quest/directive use. Authored IDs use `loc_utf16`, matching `AtlasSignalEvents.ComputeMessageHash -> LocHash.Compute`; lane names keep the existing `signal_label` mode.
Rejected Alternatives: Treating technical signal lane names as the complete signal namespace was rejected because gameplay already hashes authored Atlas message IDs. Hashing authored IDs with `signal_label` was rejected because that would desynchronize from `LocHash.Compute`. Scanning every localization key was rejected as unrelated bloat.
Scalability potential: Low tier gets a larger zero-runtime constant catalog with no frame cost. Middle/High/Ultra can reuse the expanded identity set for richer HUD/log/diagnostic presentation without runtime string hashing. The visual budget remains available because this is offline generation.
Hardware Impact: 0 us/frame and 0 B/frame in runtime. Offline table grew from 900 to 1007 records; generated C# remains constants only.

## Decision 013 - Verification Boundary After Hardening

Problem: `python -m py_compile` attempted to write into `Tools\__pycache__` and hit Windows access denied, while `.NET Framework csc` compiled the generated C# but left temp files locked under `Temp`.
Solution: Replaced Python bytecode emission with an AST parse syntax gate that writes no cache files. Kept the `csc` compile as valid because it returned `CSC_EXIT=0` and produced a 124928-byte DLL before Windows denied cleanup; recorded the temp-lock artifact instead of claiming a clean workspace.
Rejected Alternatives: Forcing deletion of locked temp artifacts or claiming Unity import success without the Editor. Adding runtime code to avoid an offline temp-file issue was rejected as architecture pollution.
Scalability potential: Low/Middle/High/Ultra unaffected; verification-only issue.
Hardware Impact: 0 us/frame.

## Decision 014 - Unity Import Boundary Is Blocked By Missing Editor

Problem: The only remaining stronger proof would be Unity import/compile, but this workspace does not expose a Unity Editor binary. `C:\Program Files\Unity\Hub\Editor` is absent, checked Unity 6000 paths are absent, `Get-Command Unity.exe` returns no source, and `C:\hades\Hecton8` is not present.
Solution: Keep the task closed at CLI source verification and record Unity import as blocked by environment. Do not invent an import result. Do not change project settings or add a synthetic Unity project.
Rejected Alternatives: Running an approved Unity command against `C:\hades\Hecton8` was rejected because that path is absent. Claiming Play Mode/import success without an Editor was rejected as fake evidence.
Scalability potential: Low/Middle/High/Ultra unaffected; this is verification boundary classification.
Hardware Impact: 0 us/frame.

## Decision 015 - Stop Creating Compiler Temp Artifacts

Problem: A second `.NET Framework csc` compile with `/nowin32manifest` also returned exit 0 but left another locked DLL and CSC temp file under `Temp`.
Solution: Treat the csc exit-0 result as compile proof and stop repeating compiler variants because they only create more locked ignored binaries in this environment.
Rejected Alternatives: Continuing compile retries was rejected because source confidence does not improve after two exit-0 compiles; cleanup risk and workspace noise increase.
Scalability potential: Low/Middle/High/Ultra unaffected; verification-only issue.
Hardware Impact: 0 us/frame.

## Decision 016 - Add Fast Hash Generator Regression Tests

Problem: The full-project collision scanner is the authoritative proof but is slow in this shell, and prior hardening changed critical filter logic without a focused regression suite.
Solution: Added `Tools/test_h8_hash_collisions.py` to verify the three runtime FNV variants against known project constants, generated-output exclusion, authored signal filtering, collision semantics for duplicate versus distinct values, and constants-only generated C# output.
Rejected Alternatives: Depending only on the full asset scan was rejected because it makes future local validation slower and less precise. Testing by importing generated C# was rejected because Unity/dotnet are unavailable in this environment.
Scalability potential: Low tier and high-tier runtime are unaffected; this is offline test coverage. The tests reduce future risk of reintroducing runtime hashing or missing authored IDs.
Hardware Impact: 0 us/frame and 0 B/frame. Test execution is offline only.

## Decision R013 - Tie Missing Crafted ItemData Gaps To Runtime Binding Plan

Problem: The recipe audit found 22 crafted IDs in `Items.csv` without matching `ItemData` assets. Reporting them as merely missing was not enough; an absent crafted asset is a runtime exploit surface unless runtime use is explicitly blocked.
Solution: `Tools/EconomyRecipeGraphAudit.py` now cross-checks missing crafted IDs against `Data/Economy/Runtime_Binding_Plan.json` and fails the audit if any missing crafted item is runtime-allowed or not owner-decision-required. The final audit reports `missing_crafted_assets_runtime_blocked=True`, `runtime_binding_plan_blocked_count=22`, and `unblocked_missing_crafted_assets=[]`.
Rejected Alternatives: Blocking all missing crafted IDs was rejected because the binding plan already models intentional unresolved authored assets; ignoring the gap was rejected because a runtime-allowed missing asset would bypass physical metadata and inventory capacity contracts.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; the gate is offline. Low-tier devices keep zero added frame cost. High/Ultra can consume the same secured economy data for richer UI/visual presentation later without accepting missing runtime assets.
Hardware Impact: 0 us/frame and 0 B/frame on i3/MX350. The guard runs only in CLI audit/test workflows.

## Decision R014 - Fail Closed On Nonpositive Runtime Physical Demand

Problem: The normal inventory capacity helper could still classify zero unit mass or zero unit volume as unlimited capacity if future runtime metadata regressed, even though current authored data audits as positive.
Solution: `PlayerInventory.TryResolveUnitPhysicalDemand` now returns false unless unit mass and unit volume are finite and strictly positive. `Tools/EconomyRecipeGraphAudit.py` reports and blocks on `try_add_rejects_nonpositive_unit_mass` and `try_add_rejects_nonpositive_unit_volume`; the unittest asserts both guards.
Rejected Alternatives: Relying only on offline `ItemData` audits was rejected because runtime descriptors are the actual insertion gate. Allowing weightless or volumeless items was rejected because it creates a direct capacity exploit.
Scalability potential: Low tier pays only command-path scalar checks, not Tick cost. Middle/High/Ultra keep the same backend truth and can spend visual budget on inventory presentation instead of runtime exploit recovery.
Hardware Impact: 0 us/frame; insertion command path adds two scalar comparisons after existing finite checks.

## Decision R015 - Negative-Test Runtime Binding Drift

Problem: The graph audit proved the current binding plan blocks 22 missing crafted assets, but there was no focused regression test proving the guard fails when a future edit marks one of those missing crafted assets runtime-allowed.
Solution: Added a temporary-copy test that flips one binding to `runtime_use_allowed=true`, then asserts `analyze_runtime_binding_guard` reports that ID as unblocked and reduces blocked missing count to 21. Also corrected `blocked_count` to count blocked missing crafted IDs, not every blocked row in the binding plan.
Rejected Alternatives: Trusting the validator's broader negative case was rejected because the graph audit owns the final `ECONOMY SECURED` report. Counting all blocked plan rows was rejected because future unrelated blocked rows would inflate report evidence.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; this is offline regression coverage.
Hardware Impact: 0 us/frame and 0 B/frame. Test-only change plus report-count precision.

## Decision R016 - Final Verification Without Bytecode Artifacts

Problem: Normal Python compile/test runs create ignored `.pyc` artifacts under `Tools/__pycache__`, which weakens workspace hygiene and can confuse later agents even when Git ignores them.
Solution: Removed only the economy-auditor pycache files and reran final gates with `python -B` plus an AST syntax parse for the audit tools.
Rejected Alternatives: Deleting the whole pycache directory was rejected because other agents own unrelated cache files. Keeping economy pycache files was rejected because they were generated by this pass and not needed.
Scalability potential: Low/Middle/High/Ultra unaffected; this is local verification hygiene.
Hardware Impact: 0 us/frame and 0 B/frame.

## Decision R017 - Fail Closed Inside The Capacity Resolver Itself

Problem: `TryResolveUnitPhysicalDemand` rejects zero unit mass and volume, but the lower-level `ResolveCapacityLimitedQuantity` helper still returned the requested quantity when `unitValue <= 0f`. A future caller could bypass the prefilter and reintroduce unlimited zero-demand inventory acceptance.
Solution: Changed `ResolveCapacityLimitedQuantity` to return 0 for nonpositive unit values and added a regression assertion against the helper body.
Rejected Alternatives: Trusting all future callers to prefilter zero values was rejected because this helper is the last capacity gate before quantity acceptance.
Scalability potential: Low tier pays no per-frame cost; this is command-path scalar validation. Middle/High/Ultra retain deterministic backend behavior.
Hardware Impact: 0 us/frame; one branch behavior change in an insertion helper.
