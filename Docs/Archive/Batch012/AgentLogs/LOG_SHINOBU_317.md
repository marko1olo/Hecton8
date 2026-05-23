# LOG_SHINOBU_317

## 2026-05-22 - Crafting Fast-Fail Validator

What was wrong:
- Prompt path `Assets/_Project/Scripts/Crafting/` does not exist. Active crafting authority is root-level `CraftingSystem.cs`, `Fabricator.cs`, `FabricationAssemblerRuntime.cs`, `RecipeData.cs`, `CraftingEvents.cs`, UI, and inventory SoA.
- Existing `ShinobuRecipeFastFailJob` is SoA-aware but uses `CraftingRecipeDTO`, scalar `CountQuantity` scans, and byte craftable output.
- Legacy `CraftingSystem`/`Fabricator` still reads `RecipeData.ingredients`, `List<RecipeData>`, string authoring IDs, and `NativeParallelHashMap<int,int>` availability maps.

What was done:
- Added `RecipeRequirementDTO` with explicit 32-byte layout: result hash, four ingredient hashes, packed quantities, blueprint unlock mask.
- Converted `CraftingSystem` to partial and added a cold `TryBuildFastFailRequirement` DTO bake bridge.
- Added `EvaluateCraftingAvailabilityJob`: Burst `IJobParallelFor` over recipe words, inventory `NativeArray<uint>` hash/quantity SoA, inventory mask fail-fast, unlock mask fail-fast, SIMD lane quantity compare, packed `NativeArray<ulong>` craftable output.
- Added `GenerateMockRecipesJob` for deterministic native mock recipe fill.
- Added `CraftingFastFailTransactionJob` with `Interlocked.CompareExchange` quantity deduction and rollback of prior deductions on later failure.
- Added DataVault BufferIDs for requirement DTOs, craftable words, telemetry ring/cursor, and transaction result.
- Added 300-entry `CraftingFastFailTelemetryEntry` ring and raw `ReadOnlySpan<byte>` dump helper to `Docs/AgentLogs/Dump_SHINOBU_317.bin` when caller reports >200 us.
- Added span-based CSV line parser for decimal/hex recipe DTO imports.
- Added `CraftingFastFailXRayWindowSHINOBU317` editor window and static OOP scanner report at `Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json`.
- Added `CraftingFastFailDebugGizmo` and architecture route note at `Docs/ARCHITECTURE/SHINOBU_317_CRAFTING_FAST_FAIL_ROUTE.md`.

Cinematic cheats used:
- Replaced recipe availability UI truth with a packed bit snapshot, one `ulong` per 64 recipes.
- Used inventory requirement masks before quantity scans; absent ingredient categories do not enter SIMD compare.
- Quality scaling affects UI publication budget only, not recipe DTO layout, inventory authority, save identity, or transaction truth.

Exact microseconds saved:
- Measured proof absent. Project rule blocked compile/build because CPU load was 94-100% and builds are forbidden above 50%.
- Static estimate: UI read path drops from per-recipe managed/object checks to one word load plus bit test per recipe. Expected saving is tens to hundreds of microseconds on large fabricator menus after UI migration.
- Validation worker estimate: 64 recipe outputs per job word; quantity phase performs one SoA scan with four hash lanes and one SIMD quantity compare per recipe.

Blocked:
- Full Fabricator hot-path replacement is intentionally partial. `CanCraft` also owns power, biome locks, storage capacity, scarcity adjustment, local reservations, and logistics network fallback. Replacing it blindly would break authority.
- Rollback netcode descriptor was not modified; current implementation keeps UI bitmask read-only and transaction mutation isolated.
- Compile verification not run because CPU load was 94-100%; `csc.exe` was not running.

## 2026-05-22 - Polish Audit Continuation

What was wrong:
- Fast-fail `BufferID` values `70142..70146` overlapped existing VR somatic lanes.
- Vault owner was too broad (`GameplayPlayer`) for a crafting validation route.
- The initial read/resolve helper could allocate behind a read-looking API name.
- `GlobalQualityWeight` computed a publication budget but the scheduler still launched every word worker.
- Transaction failure telemetry could label missing stock as atomic contention after a later ingredient CAS failure.

What was done:
- Moved fast-fail lanes to `71203..71207` and added `SystemID.Crafting=75`.
- Split cold growth into `AcquireFastFailVaultBuffersCold`; pure consumers use `TryReadFastFailVaultBuffers`.
- Added `TryReadCraftableBit` so UI reads fail closed outside `UiPublicationBudget`.
- Changed `ScheduleAvailability` to schedule only `ResolveWordCount(UiPublicationBudget)` workers, not the full `CraftableWords` capacity at low quality.
- Expanded telemetry to record evaluated recipes, unlock culls, mask culls, SIMD successes, budget, masks, inventory version, and state hash.
- Reworked the editor window to UI Toolkit and kept `OOP_Crafting_Scanner` editor-only.
- Added `ReadOnlySpan<byte>` CSV ingestion for numeric and FNV-hashed tokens.
- Updated the architecture route and binary payload ledger with numeric IDs, ABI offsets, lifecycle, rollback exclusion, and dump contract.

Cinematic cheats used:
- Blueprint unlock mask and inventory material mask now reject before any SoA quantity scan.
- Low-quality menu publication refreshes fewer packed words; stale unpublished words are ignored by fail-closed reads instead of being cleared every frame.

Exact microseconds saved:
- Measured proof still pending guarded compile/profiler window.
- Static estimate for a 512-recipe menu at severe thermal weight: scheduled publication workers drop from 8 word workers to 1, saving roughly 7/8 of UI validation scan bandwidth.
- Transaction path adds one bounded SoA preflight only on click, not during menu polling; this buys deterministic failure classification without per-frame cost.

Blocked:
- Full Fabricator/UI call-site migration still depends on an inventory-owned `NativeArray<uint>` quantity snapshot being published as the authoritative route. Existing legacy `Fabricator.CanCraft` remains broader authority and is not removed in this pass.
- Rollback descriptor code was not edited across domain boundary; the route is documented as presentation/proof excluded, while inventory lanes remain the rollback truth.
- Compile verification was checked again and not launched: CPU sampled at `100%` and `dotnet` PID `1680` was active.

<SELF_AUDIT_LEGACY_TEXT>
Tasks:
01 PASS grep archaeology and scanner report.
02 PASS `CraftingSystem` partial bridge.
03 PASS existing signal lanes mapped; no new hot lane.
04 PARTIAL legacy `RecipeData` authoring remains until Fabricator migration.
05 PARTIAL authoring strings remain cold; runtime DTO uses hashes/masks.
06 PASS deterministic native mock recipe job.
07 PASS Burst validation kernel over DTO plus `NativeArray<uint>` SoA.
08 PASS unlock/material masks cull before quantities.
09 PASS `v128` SIMD compare plus Burst vector fallback.
10 PASS packed `ulong` UI words and fail-closed bit read helper.
11 PASS continuous quality budget now constrains scheduled word workers.
12 PASS transaction revalidates and CAS-deducts with rollback.
13 PARTIAL rollback descriptor code not edited; route documented presentation/proof excluded.
14 PASS uninitialized DTO/word buffers; full scheduled words overwritten.
15 PASS 300-entry telemetry ring and raw dump helper.
16 PASS UI Toolkit X-Ray and scanner facade.
17 PASS `ReadOnlySpan<byte>` CSV bridge.
18 PASS selected gizmo status marker; not a runtime text overlay.
19 PASS logistics scanner JSON.
20 PARTIAL static audit done; compile blocked by CPU/dotnet guard.

StructLayout:
`RecipeRequirementDTO=32`: `uint@0`, `uint@4`, `uint@8`, `uint@12`, `uint@16`, `uint@20`, `ulong@24`; total `24+8=32`, aligned to 8/16/32.
`CraftingFastFailTelemetryEntry=64`: counters `0..20`, floats `24/28`, masks `32/40`, version/budget/hash/flags `48/52/56/60`; total 64, one L1 cache line.

Vault:
No private persistent NativeArray ownership in the new validator. Cold owner buffers: `71203..71207`, owner `SystemID.Crafting`. Read route uses `TryReadHandle`; acquisition route is cold only.

DependencyGraph:
Consumes caller `JobHandle dependency`; outputs validation `JobHandle`; registers owner fence through `H8Memory.RegisterActiveJob(SystemID.Crafting, handle)`. Transaction is single `IJob`. No hidden `.Complete()`.

CompileGuard:
No new asmdef reference. Compile not launched because CPU was `100%` and `dotnet` PID `1680` was active.

DearLie:
Legacy object/list/string recipe validation is replaced by unlock/material mask tests before SoA quantity scans. Old hot shape: O(recipes * ingredients * inventory search). New UI shape: O(scheduledWords * 64) with culls; only survivors scan quantities.
</SELF_AUDIT_LEGACY_TEXT>

## 2026-05-22 - Direct Consume Fast Reservation

What was wrong:
- `ConsumeIngredients` still rebuilt direct recipe costs through `CraftingSystem.TryBuildRecipeCostBuffer` before using the existing PlayerInventory reservation owner route.
- That kept a redundant authoring/cost-buffer pass on the click path for simple recipes already represented by the scarcity-adjusted `RecipeRequirementDTO`.

What was done:
- Inserted `TryReserveDirectFastFailRecipeCosts` before the legacy cost-buffer build.
- The helper expands the adjusted `RecipeRequirementDTO`, unpacks four byte quantity lanes, normalizes duplicate hashes, reserves local craft locks through `PlayerInventory.TryReserveAvailableQuantityForCraft`, and reserves only missing quantities through `BaseLogisticsNetwork.TryReserveResources`.
- Unsupported DTOs, quantity overflow, reservation failure, and complex raw-cost recipes still fall back to the existing exact path after `RefundIngredients`.

Cinematic Cheats used:
- Four scalar DTO lanes replace the legacy direct cost-buffer bake for supported recipes. No physics or scene search was introduced.

Exact Microseconds saved:
- Supported direct recipe starts avoid `CraftingSystem.TryBuildRecipeCostBuffer` and a duplicate direct-cost loop before reservation.
- Complex recipe graph expansion remains exact and pays the old cost only when required.

Verification:
- `git diff --check` stayed clean for touched SHINOBU files except inherited CRLF warnings.
- `LOGISTICS_OPTIMIZATION_REPORT.json` parsed through `ConvertFrom-Json`.
- Build was not launched after this patch because CPU sampled `100%`; AGENTS forbids `dotnet build` above 50% CPU.
- Subagent static audit found no direct-reservation regression and recommended no patch. It explicitly checked local-only fallback, logistics-grid delta reservation, duplicate cost normalization, namespace access, and private partial access. This is not compile proof.

## 2026-05-22 - Structured Self-Audit Refresh And UI Bitset Boundary

What was wrong:
- The structured forensic `SELF_AUDIT` block reflected an earlier snapshot and did not mention direct reservation or the editor compile include.
- Live UI still does not consume a full recipe-indexed `CraftableWords` bitset, but forcing that from the visual refresh would create UI-owned native state or hidden Vault acquisition.

What was done:
- Refreshed the structured self-audit task text for direct reservation, editor include, latest external compile wall, and the direct-cost-buffer Dear Lie.
- Revalidated that `ScheduleAvailability` remains a staged owner-phase API and that `HectonFabricatorUI` stays on visible-row SoA snapshots until a cold Fabricator/boot owner phase can bake recipe indices into Vault lanes.

Cinematic Cheats used:
- Kept visible-row evaluation bounded to eight rows and one SoA snapshot rather than inventing a same-frame schedule/readback loop.

Exact Microseconds saved:
- No new runtime saving. This avoids a potential hidden UI hitch from `AcquireFastFailVaultBuffersCold` or `JobHandle.Complete` in a visual refresh.

Verification:
- Structured `SELF_AUDIT` parses as exactly one XML block with `20` task nodes and status `POLISH_DIRECT_CONSUME_FAST_RESERVATION`.
- JSON report parses.
- `git diff --check` stays clean except inherited CRLF warnings.
- Build guard remains closed at CPU `100%`.

## 2026-05-22 - SHINOBU_317 Polish Loop 31 - Live UI/Start-Craft Leakage Closure

What was wrong:
- `Fabricator.CanCraft()` used the fast ingredient proof but still called exact output-capacity reclaim scanning through `IsOutputStorageCapacityExceeded()`.
- `HectonFabricatorUI.RebuildRecipeListEntries()` recalculated scarcity inflation per visible row with a second `RecipeData.ingredients` pass after the adjusted DTO bake had already walked the same authoring list.
- The X-Ray editor window existed on disk but was absent from the generated `Hecton8.Editor.csproj` static surface.

What was done:
- Routed `Fabricator.CanCraft()` through `IsOutputStorageCapacityExceededFastOrExact()`, preserving exact reclaim scans only when free cells are actually tight.
- Extended `Fabricator.TryBuildAdjustedFastFailRequirement()` to return the max scarcity inflation multiplier while it packs the adjusted 32-byte DTO.
- Cached visible-row inflation and display names inside `RecipeListEntry`; display-name cache invalidates on localization language events.
- Added a conditional `Hecton8.Editor` compile include in `Directory.Build.targets` for `CraftingFastFailXRayWindow_SHINOBU317.cs`, leaving generated `.csproj` files untouched.

Cinematic cheats used:
- Storage capacity now uses a scalar free-cell rejection before exact reclaim math.
- UI row presentation reuses cached DTO/inflation/display-name state instead of repeating authoring object walks on stable rows.

Exact microseconds saved:
- Estimated tens of microseconds on i3/MX350 per dirty fabricator menu rebuild by removing the second visible-row scarcity pass.
- Estimated tens of microseconds per normal start-craft/free-storage check by avoiding reclaimable ingredient scans after resource proof.

Verification:
- `git diff --check` passed for SHINOBU_317 touched files with only inherited CRLF warnings.
- `LOGISTICS_OPTIMIZATION_REPORT.json` parsed through `ConvertFrom-Json`.
- Self-audit XML parse still found `20` task nodes.
- Guarded `dotnet build Hecton8.Core.csproj --no-restore -m:1` ran only after CPU sampled `38.6%` and no `dotnet`/`csc` process existed. It failed on external generated-project walls in `VRSomaticProvider`, `VRSomaticProvider.Comfort`, `CombatDamageRuntime_StatusEffects`, and `HydrodynamicKccRuntime`; no SHINOBU_317 diagnostic was emitted before the wall.

## 2026-05-22 Loop 27 - BufferID Collision Correction

What was wrong:
- The earlier enum-only duplicate scan did not inspect direct `(BufferID)71150..71168` constants in `ChemicalInfluenceGrid`.
- SHINOBU_317 draft lanes overlapped chemical grid published/readback buffers at `71150..71152`.

What was done:
- Moved fast-fail lanes to `71203..71207`.
- Updated `H8Memory`, X-Ray scanner constants, route card, binary ledger, status, rationale, and self-audit references.
- Verified `71203..71207` as a full-script scanned free gap after stress director `71200..71202` and before biome transition `71220..71231`.

Cinematic cheats used:
- None. This is ABI route hardening; the Dear Lie remains the packed bitmask/UI proof route instead of managed recipe searches.

Exact microseconds saved:
- No direct hot-path saving. Prevents catastrophic Vault aliasing between crafting fast-fail telemetry/transactions and chemical influence readback buffers.

Verification:
- `rg` found `ChemicalInfluenceGrid` owns `71150..71168`.
- PowerShell free-range scan over `Assets/_Project/Scripts/**/*.cs` returned `FREE5=71203..71207`.
- Build was not launched because the guard sampled CPU at `100%`.
- Post-correction validation: `git diff --check` returned only inherited CRLF warnings; `LOGISTICS_OPTIMIZATION_REPORT.json` parsed; latest `SELF_AUDIT` XML block parsed with `20` task nodes; XPath `//H_PHI_VAULT_STATUS` reports `71203..71207`; targeted code scan finds those IDs only in `H8Memory`; prompt extraction still reports unique tasks `01..20`.
- Latest build guard after validation: CPU `82.3%`, no active `dotnet` or `csc`; build still forbidden by the >50% CPU rule.
- Final guard sample before handoff: CPU `97.9%`, no active `dotnet` or `csc`; build remains forbidden by the >50% CPU rule.

## 2026-05-22 - Live Fabricator Fast-Fail Gate

What was wrong:
- The validator was present but the real `Fabricator.CanCraft` path could still enter legacy `NativeParallelHashMap` scratch setup before any SoA proof.
- A hard `CurrentPowerGrid != null` guard would have made the fast path irrelevant on normal powered fabricators.

What was done:
- Added `Fabricator.FastFail.cs` partial gate. `CanCraft` and storage-capacity bark checks now call `HasIngredientsFastFailOrLegacy`.
- Added `PlayerInventory.TryReadFastFailInventorySoA`, which reads cached Vault SoA handles through `SoaInventoryQueryEngine.TryReadVaultBuffers`, reinterprets inventory quantities as `NativeArray<uint>`, and returns active slot count plus `CurrentInventoryMask`.
- Added `SoaInventoryQueryEngine.TryReadVaultBuffers` and `ReadLane` so the live gate can read without allocation, buffer growth, scene search, signal publish, or job completion.
- Local-only fabricators treat SoA success and failure as authoritative. Fabricators on a logistics grid accept local SoA success immediately and fall back to legacy only when local SoA says missing, preserving remote resource authority.

Cinematic cheats used:
- The live check rejects by unlock mask and inventory material mask before any quantity scan.
- Positive local proof bypasses object/list/hashmap setup entirely; network-missing cases deliberately pay legacy cost to avoid false negatives.

Exact microseconds saved:
- Measured proof remains blocked. Latest guard: CPU `100%`, active `csc` PID `12272`, active `dotnet` PID `12344`.
- Static saving on the common local-sufficient recipe path: avoids `EnsureCraftingScratch`, `TryCopyAvailableItemCountsNonAlloc`, and `NativeParallelHashMap` availability lookup; cost becomes one cached SoA read plus bounded four-lane validation.

Blocked:
- Full negative fast-fail for logistics fabricators requires a network resource SoA/mask lane. Current code falls back instead of lying.
- Full UI publication still needs menu call sites moved to `TryReadCraftableBit` once recipe DTO indices are bound.

Static verification:
- Prompt extraction: unique two-digit task IDs `01..20` present.
- BufferID enum scan: `NO_DUPLICATE_BUFFER_IDS`.
- `git diff --check`: no whitespace errors; only Git CRLF conversion warnings on inherited files.
- Scanner JSON: `12` files scanned, `117` SO/RecipeData hits, `85` string identity hits, `10` NativeParallelHashMap hits.
- Hot-path forbidden scan: only editor-only `TryInjectSoaVaultItemForXRay` uses `new NativeArray` and `.Complete()`.
- Build guard: compile still not launched; CPU `100%`; latest guard had no active `csc`/`dotnet`, but CPU remained above the 50% policy threshold.

## 2026-05-22 - Diegetic UI Row Fast-Fail Binding

What was wrong:
- `HectonFabricatorUI.RebuildRecipeListEntries` still called `_currentFabricator.CanCraft` once per visible row, so presentation could repeat exact crafting evaluation work just to color labels.

What was done:
- `RebuildRecipeListEntries` now reads one `PlayerInventory.TryReadFastFailInventorySoA` snapshot per list rebuild.
- Added `Fabricator.TryCanCraftFastFailPresentation`, preserving power, unlock, biome, and output-capacity gates inside the Fabricator partial.
- UI rows use the SoA presentation gate first. Local-only rows use known SoA success/failure. Networked local-missing rows fall back to exact legacy logistics evaluation.
- Added no new hot `.Complete()`, no new runtime NativeArray allocation, and no new GlobalRegistry polling in the row path.

Cinematic cheats used:
- The diegetic list color now uses one cached SoA view and bitmask/SIMD checks instead of per-row managed availability map setup when local proof is enough.

Exact microseconds saved:
- Measured proof remains blocked. Latest guard: CPU `100%`, active `dotnet` PIDs `13388,16552`.
- Static saving for the visible list: one SoA read per rebuild plus up to eight bounded row checks, instead of up to eight full `CanCraft` exact evaluations.

Blocked:
- Full `TryReadCraftableBit` UI publication still needs stable recipe DTO indices from the Data Monolith bake.
- Logistics-negative rows still need exact fallback until network resource SoA/mask lanes exist.

## 2026-05-22 - Rollback Fence Proof And Guarded Compile Attempt

What was wrong:
- Task 13 previously relied on route documentation for rollback exclusion. That did not mechanically prove that SHINOBU_317 presentation buffers were absent from Merkle descriptors and StateSnapshot copy sources.
- Compile proof had been blocked by CPU/dotnet guard; once the guard opened, the code needed one targeted runtime build, not a full-solution rebuild.

What was done:
- Extended `OOP_Crafting_Scanner` to scan `HectonRollbackNetcodeRuntime.cs` and `RollbackNetcodeContracts.cs`.
- Updated `Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json` with `rollbackDescriptorFastFailHits=0`, `stateSnapshotFastFailCopyHits=0`, and `rollbackAuthoritativeInventoryCopyHits=6`.
- Updated the route card and binary payload ledger with the mechanical rollback proof: inventory hash/quantity/durability lanes are hashed/copied; fast-fail requirement/craftable/telemetry/result lanes are not.
- Guard opened at CPU `13%` and no active `csc/dotnet`, so `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal` was launched.

Cinematic cheats used:
- Rollback stores only authoritative inventory truth. The craftability bitmask remains a presentation/proof artifact regenerated from restored inventory after rollback instead of being hashed as gameplay state.

Exact microseconds saved:
- Runtime cost is unchanged by the proof scanner.
- Snapshot bandwidth saved equals all fast-fail presentation lanes omitted from `StateRingBuffer`: recipe DTOs, packed craftable words, telemetry ring/cursor, and transaction-result proof lane.

Compile result:
- Build failed in 17s on external missing DTOs before any SHINOBU_317 diagnostic: `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, and `RadiationStateDTO`.
- Focused source scan found those DTOs in `VRSomaticProvider.HorizonLock.cs` and `Core/Contracts/Physiology/RadiationStateContract.cs`, but they are absent from the generated `Hecton8.Core.csproj` compile item list.
- I did not patch external VR/radiation project routing from the crafting domain.

## 2026-05-22 - Visible Row DTO Cache Polish

What was wrong:
- The recipe list fast path read the inventory SoA once per rebuild, but it still rebuilt `RecipeRequirementDTO` from `RecipeData.ingredients` for each row on every refresh.

What was done:
- Added cached `RecipeRequirementDTO` fields to the fixed `RecipeListEntry[8]` row cache, keyed by recipe reference and batch multiplier.
- Added `Fabricator.TryCanCraftFastFailPresentation(recipe, multiplier, in requirement, ...)` so UI can pass the cached DTO while the Fabricator still owns power/unlock/biome/storage gates.
- Updated the no-fabricator UI fallback to evaluate the cached DTO directly against the SoA view.

Cinematic cheats used:
- Stable rows now use cached requirement DTOs plus bitmask/SIMD validation instead of repeatedly walking managed ingredient lists.

Exact microseconds saved:
- Stable visible-list refresh avoids up to eight `RecipeData.ingredients` walks per rebuild after the first cache fill.
- Scanner count now reports `127` SO/RecipeData hits because the explicit cache/proof references are counted; this is static visibility, not hot-path cost.

Verification:
- `git diff --check` passed for the touched UI/Fabricator/report files with only inherited CRLF warnings.
- Build was not re-launched after this polish because the guard sampled CPU `82%`.

Compile recheck:
- Guard reopened at CPU `27%` with no active `csc/dotnet`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -v:minimal` was repeated after the DTO-cache signature changes.
- Result: external compile wall only. Errors were missing `RadiationStateDTO`, `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, and `PlayerHandIkConfigFlags`; focused source scan found all of them in non-SHINOBU_317 files that are absent from the generated Core project compile item list. No SHINOBU_317 file appeared in the diagnostics.

## 2026-05-22 - Scarcity-Corrected Fast-Fail Bake

What was wrong:
- Fabricator fast validation could pack raw `InventoryCost.amount` while the exact route uses `GetAdjustedIngredientAmount`. A scarcity-inflated recipe could therefore appear craftable in the fast path when the exact path would require more units.
- The visible-row DTO cache did not include scarcity version, so a stable row could keep stale quantities after scarcity changed.
- Storage-capacity presentation still walked recipe ingredients even when output fit in existing free cells.

What was done:
- Added `Fabricator.TryBuildAdjustedFastFailRequirement`, packing adjusted quantities into the same 32-byte `RecipeRequirementDTO`.
- `HasIngredientsFastFailOrLegacy` and the current-fabricator UI cache now use the adjusted bake.
- Added `FastFailScarcityVersion` to the fixed row cache and keyed it from `ResourceScarcityDirector.RuntimeVersion`.
- Added `IsOutputStorageCapacityExceededFastOrExact`, which skips exact reclaim math when `Grid.FreeCells` already covers output demand.
- Routed `IsStorageCapacityExceededForRecipe` through the same fast-or-exact check after the SoA ingredient proof, so interaction feedback avoids the reclaim scan in the common enough-space case.

Cinematic cheats used:
- Free-cell storage check rejects the need for a reclaimable-ingredient walk in the common case. Exact reclaim math is kept only for tight-space rows.

Exact microseconds saved:
- Stable rows avoid repeated adjusted ingredient walks until recipe, batch multiplier, or scarcity version changes.
- Common enough-space rows and storage-bark checks avoid `CountReclaimableIngredientCells` after SoA success.

Verification:
- `git diff --check` passed for Fabricator/UI touched files with only inherited CRLF warnings.
- Scanner JSON now reports `129` SO/RecipeData hits; the count rose because the corrected adjusted-bake route is explicitly scanner-visible.

## 2026-05-22 - Raw Authoring Bake Overflow Hardening

What was wrong:
- `CraftingFastFailValidator.TryBuildRequirementFromRecipeData` multiplied authored `InventoryCost.amount` by the requested multiplier as an `int` before clamping into the 8-bit packed DTO lane.
- Damaged content or debug multipliers could overflow that intermediate product and wrap into a bogus unsigned quantity.
- Truncating real requirements above `255` into the byte lane would be a false fast-positive for high batch or scarcity-inflated recipes.

What was done:
- Promoted the multiplication to `long`.
- Rejected raw and scarcity-adjusted requirements above `255` so callers fall back to exact legacy validation instead of truncating resource truth.
- Rejected char and byte CSV quantity tokens above `255` before `BuildRequirement` can pack them.
- Normalized char and byte CSV overloads to the same interleaved field order: result, hashA, quantityA, hashB, quantityB, hashC, quantityC, hashD, quantityD, unlockMask.

Cinematic cheats used:
- None. This is a data-integrity guard on the cold authoring bridge.

Exact microseconds saved:
- No hot-path runtime saving. One cold 64-bit multiply and compare per ingredient plus CSV token range checks buy deterministic failure behavior for corrupt recipe data and preserve exact validation for large requirements.

Verification:
- `git diff --check` passed for SHINOBU_317 touched files with only inherited CRLF warnings.
- `Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json` parsed through `ConvertFrom-Json`.
- Rollback proof remains `rollbackDescriptorFastFailHits=0`, `stateSnapshotFastFailCopyHits=0`, `rollbackAuthoritativeInventoryCopyHits=6`.
- Forbidden scan found only pre-existing `HectonFabricatorUI.IsMenuOpen { get; private set; }` and editor-only diagnostic `GlobalDataVault.TryGetLatestCreated`.
- Build was not relaunched: CPU later sampled `9%`, but active `dotnet` processes were still present, and AGENTS forbids build while another dotnet is running.
- Later CSV guard verification kept `git diff --check`, JSON parse, and self-audit XML parse clean. Build guard remained closed at CPU `99%` with active `dotnet` PID `7816` and `csc` PID `18088`.

<SELF_AUDIT agent="SHINOBU_317" domain="CRAFTING_FAST_FAIL_VALIDATOR" status="POLISH_DIRECT_CONSUME_FAST_RESERVATION">
  <TASK_RECONCILIATION>
    <Task id="01" status="[PASS]">rg archaeology over crafting, Fabricator, UI, inventory, rollback contracts; scanner report exists.</Task>
    <Task id="02" status="[PASS]">Partial `CraftingSystem` and partial `Fabricator` integration; no competing manager.</Task>
    <Task id="03" status="[PASS]">Existing inventory/crafting signal lanes mapped; no new hot broadcast lane.</Task>
    <Task id="04" status="[FAIL]">SO recipe authoring is not fully purged. Runtime fast path now bakes scarcity-adjusted DTOs, bypasses list walks in CanCraft/UI, and bypasses legacy direct cost-buffer bake in ConsumeIngredients for supported recipes; content migration remains required before deleting `RecipeData`.</Task>
    <Task id="05" status="[FAIL]">String item identity is not fully purged from authoring/localization. Runtime validator, visible-row gates, and direct reservation use hashes and masks; display strings are cached and invalidated by localization version. Scanner still reports legacy cold string surfaces.</Task>
    <Task id="06" status="[PASS]">Deterministic mock recipe DTO generator job exists.</Task>
    <Task id="07" status="[PASS]">Burst recipe validation kernel evaluates `RecipeRequirementDTO` against inventory SoA.</Task>
    <Task id="08" status="[PASS]">Unlock mask and inventory requirement mask short-circuit before quantity scan.</Task>
    <Task id="09" status="[PASS]">SSE2 `v128` quantity compare plus Burst `uint4` fallback implemented.</Task>
    <Task id="10" status="[PASS]">Packed `ulong` craftability words plus fail-closed bit reads implemented.</Task>
    <Task id="11" status="[PASS]">`GlobalQualityWeight` continuously maps UI publication budget; transaction truth does not degrade.</Task>
    <Task id="12" status="[PASS]">Atomic transaction job revalidates unlock, mask, SIMD quantities, then CAS-deducts bounded ingredient lanes. Live Fabricator consumption now uses packed DTO direct reservation through existing PlayerInventory/logistics owner routes.</Task>
    <Task id="13" status="[PASS]">Scanner proves no fast-fail presentation descriptors/copies in rollback; authoritative inventory lanes remain hashed/copied.</Task>
    <Task id="14" status="[PASS]">DTO/word Vault buffers use `UninitializedMemory`; active word workers overwrite their outputs.</Task>
    <Task id="15" status="[PASS]">300-row 64-byte telemetry ring and raw dump helper exist.</Task>
    <Task id="16" status="[PASS]">UI Toolkit X-Ray editor window and scanner exist; `Directory.Build.targets` includes the X-Ray window in `Hecton8.Editor` without editing generated csproj files.</Task>
    <Task id="17" status="[PASS]">`ReadOnlySpan&lt;byte&gt;` CSV DTO parser exists with FNV/hash token support, rejects packed quantities above 255, and shares field order with the char diagnostic parser.</Task>
    <Task id="18" status="[PASS]">Editor debug gizmo marker exists.</Task>
    <Task id="19" status="[PASS]">`LOGISTICS_OPTIMIZATION_REPORT.json` scanner proof exists and parses.</Task>
    <Task id="20" status="[FAIL]">Static verification passes, but guarded Core build is blocked by external generated-project/domain errors in VR somatic, combat status effects, and metabolism/KCC surfaces. SHINOBU_317 did not patch those domains.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <RecipeRequirementDTO size="32">
      <Field name="ResultItemHash" offset="0" size="4"/>
      <Field name="IngredientHashA" offset="4" size="4"/>
      <Field name="IngredientHashB" offset="8" size="4"/>
      <Field name="IngredientHashC" offset="12" size="4"/>
      <Field name="IngredientHashD" offset="16" size="4"/>
      <Field name="QuantitiesPacked" offset="20" size="4"/>
      <Field name="BlueprintUnlockMask" offset="24" size="8"/>
      <Math>4+4+4+4+4+4+8=32; offset 24 is 8-byte aligned; no Pack=1.</Math>
    </RecipeRequirementDTO>
    <CraftingFastFailTelemetryEntry size="64">
      <Fields>Frame@0, RecipeWordIndex@4, RecipesEvaluated@8, UnlockCullCount@12, MaskCullCount@16, SimdSuccessCount@20, ScheduleMicroseconds@24, GlobalQualityWeight@28, RequirementMask@32, UnlockMask@40, InventoryVersion@48, UiPublicationBudget@52, StateHash@56, Flags@60.</Fields>
      <Math>64 bytes exactly; one L1 cache line for black-box rows.</Math>
    </CraftingFastFailTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    `ResolveUiPublicationBudget(count, GlobalQualityWeight)` applies cubic smoothstep `q*q*(3-2*q)` and schedules only the required packed word count. At low quality a large menu publishes the first word budget and `TryReadCraftableBit` fail-closes outside it; at high quality the full recipe table can be refreshed. Crafting transaction truth, DTO layout, save identity, and rollback route are unchanged by quality.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Persistent fast-fail buffers are Vault-owned: 71203 requirements, 71204 craftable words, 71205 telemetry ring, 71206 telemetry cursor, 71207 transaction result. The validator owns no private persistent `NativeArray`, `NativeList`, or `NativeHashMap`. Legacy Fabricator scratch remains pre-existing scene scratch and is bypassed by CanCraft/UI/direct reservation only where correctness allows.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Validation consumes caller dependency and outputs `EvaluateCraftingAvailabilityJob` handle registered under `SystemID.Crafting`. Transaction consumes caller dependency and outputs its scheduled handle to the caller. Burst fields use `[NoAlias]`; shared word/telemetry/result lanes use `NativeDisableParallelForRestriction` only where each worker owns a full word or writes through an atomic cursor.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new sibling-domain asmdef reference was introduced. Runtime lives in existing `Hecton8.Core` until a dedicated crafting asmdef exists. Guarded Core builds reached only external missing generated-project symbols; no SHINOBU_317 diagnostic was emitted before the wall.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Crafting presentation now uses mask/packed-word proof instead of managed list and map reconstruction. Before: O(visibleRecipes * ingredients * accessibleInventorySearch) plus Fabricator scratch setup. After: O(visibleRecipes * SoA slot scan) with two bitmask culls and four-lane compare; scheduled publication is O(words * 64) and low quality can publish fewer words. Storage presentation uses free-cell scalar rejection before exact reclaim scans, and supported direct recipe starts expand four DTO lanes instead of rebuilding a legacy direct cost buffer.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-22 - XML Extractor Hygiene Recheck

What was wrong:
- A strict regex check could still catch prose mentions of the forensic XML tag before the real structured block.
- That made the log look malformed even though the runtime code and the structured block itself were not the defect.

What was done:
- Removed raw angle-bracket tag spelling from prose references and left only the single structured XML block as the real parse target.
- Re-ran the static guard set after this patch target: diff hygiene, JSON scanner parse, prompt extraction, project-surface inclusion, CPU/build guard, and XML parse.

Cinematic Cheats used:
- None. This is forensic-tooling hygiene only.

Exact Microseconds saved:
- No runtime saving. It prevents false audit-tool failures and avoids wasting a guarded compile slot on a log parsing issue.

Verification:
- Strict structured audit regex now returns exactly one parse target with `20` task nodes and status `POLISH_DIRECT_CONSUME_FAST_RESERVATION`.
- Scanner JSON parses: `files=12`, `scriptableRecipeReadHits=129`, `stringIdentityHits=85`, `nativeHashMapLegacyHits=10`, rollback fast-fail descriptor/copy hits `0/0`.
- Prompt extraction from `CURRENT_BATCH.md` returns task IDs `01..20` and source length `22688`.
- Core project surface includes SHINOBU runtime partials and `Directory.Build.targets` includes the X-Ray editor window for `Hecton8.Editor`.
- Build guard stayed closed: CPU `97%`, no active `dotnet` or `csc`.
