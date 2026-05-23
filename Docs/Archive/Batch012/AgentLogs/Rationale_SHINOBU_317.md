# SHINOBU_317 Rationale

Status: POLISH_DIRECT_CONSUME_FAST_RESERVATION
Created: 2026-05-22

## Decision 001: Scope And Surface

Problem: Prompt names `Assets/_Project/Scripts/Crafting/`, but that directory is absent. Active crafting/fabrication code is rooted at `Assets/_Project/Scripts/CraftingSystem.cs`, `Fabricator.cs`, `FabricationAssemblerRuntime.cs`, `CraftingEvents.cs`, `RecipeData.cs`, and UI consumers.
Solution: Treat root-level Hecton8 crafting/fabrication files as the authoritative code surface and avoid creating a competing manager until file/class scan proves no partial integration point.
Rejected Alternatives: Creating a new `HectonFastFailCraftingManager` would duplicate authority and violate the partial integration mandate.
Scalability potential: Low uses hash/quantity SoA and deferred UI slices; middle/high/ultra reuse the same truth bitmask while presentation can spend saved time on richer UI feedback.
Hardware Impact: Avoids managed recipe object scans. Estimated gain on i3/MX350 is pending measurement; static target is tens to hundreds of microseconds saved when fabricator menus refresh.

## Decision 002: Route Discipline

Problem: Crafting validation needs inventory dirtiness and transaction completion without new hot global traffic.
Solution: Reuse existing `InventoryChangedSignal`, `InventoryCommandSignal`, `CraftingCompletedSignal`, and `PlayerActionCancelledSignal` if layouts support the payload.
Rejected Alternatives: A new single-use `DeductItemsSignal` is rejected until existing signal payloads are proven insufficient.
Scalability potential: Low coalesces dirty inventory refreshes; middle/high/ultra can consume full snapshots without changing gameplay truth.
Hardware Impact: Signal reuse avoids extra NativeQueue/SignalBus lanes and keeps MX350 cache pressure bounded.

## Decision 003: DTO Contract

Problem: Existing `CraftingRecipeDTO` supports only two direct components plus optional ingredient spans and the active `ShinobuRecipeFastFailJob` still repeats scalar quantity scans.
Solution: Add `RecipeRequirementDTO` with the required 32-byte explicit layout: result hash, four ingredient hashes, packed byte quantities, and one `ulong` blueprint unlock mask.
Rejected Alternatives: Extending `CraftingRecipeDTO` would break existing `Shinobu19EconomyLedger` binary contract and rollback netcode descriptors.
Scalability potential: Low evaluates packed requirement words for UI; middle/high/ultra reuse the same DTOs and can raise publication cadence without changing truth.
Hardware Impact: Four component hashes fit in one 128-bit compare lane. Estimated low-end gain is removal of repeated managed list walks and reduced branch count during recipe menu refresh.

## Decision 004: Packed Word Output

Problem: Writing one craftable byte per recipe is safe but creates wider UI scan bandwidth and invites stale byte reads when partial jobs are scheduled.
Solution: `EvaluateCraftingAvailabilityJob` writes `NativeArray<ulong>` words, one bit per recipe, and each parallel worker owns a full 64-recipe word to avoid atomic bit races.
Rejected Alternatives: Parallel per-recipe bit writes were rejected because OR-ing into shared words races without atomics; `NativeArray<bool>` was rejected because layout is not the required packed publication surface.
Scalability potential: Low can publish fewer words per frame; middle/high/ultra can publish full recipe pages and spend saved bandwidth on richer UI effects.
Hardware Impact: UI checks drop to one load and bit test per recipe. Expected MX350/i3 savings: tens of microseconds on large fabricator menus.

## Decision 005: SIMD And ARM Fallback

Problem: SSE-only code would satisfy desktop but leave ARM64 relying on unmanaged scalar comparisons.
Solution: Use `Unity.Burst.Intrinsics.v128`/SSE2 where available and `uint4`/`bool4` Burst math fallback elsewhere. The fallback remains vectorizable by Burst on ARM64.
Rejected Alternatives: Hand-written per-ingredient scalar branches were rejected for the hot validator. NEON-specific calls were not added because no existing project NEON helper pattern was found in the local intrinsics archaeology.
Scalability potential: Low keeps one recipe-word pass with cheap fallback; high/ultra get SIMD path and can afford more frequent refresh.
Hardware Impact: On i3/MX350 the SSE2 branch avoids four independent scalar quantity comparisons per recipe after one SoA scan.

## Decision 006: Transaction Fence

Problem: UI bitmask validation cannot be authoritative for crafting consumption because inventory can change between UI refresh and click.
Solution: Add `CraftingFastFailTransactionJob` using `Interlocked.CompareExchange` against `NativeArray<uint>` quantities and rollback prior deductions on later requirement failure.
Rejected Alternatives: Consuming through UI bitmask state was rejected because rollback/netcode snapshots must not own inventory truth.
Scalability potential: Low performs only the clicked recipe transaction; high/ultra still use the same authority route and only improve presentation cadence.
Hardware Impact: Atomic slot updates are bounded to four requirements and existing inventory capacity, avoiding managed reservation allocations on the validation path.

## Decision 007: Editor And Scanner Evidence

Problem: The legacy OOP/string/SO crafting paths are too broad to remove blindly in one pass without breaking Fabricator behavior.
Solution: Add an editor X-Ray and `LOGISTICS_OPTIMIZATION_REPORT.json` scanner report to make remaining managed surfaces visible and bounded.
Rejected Alternatives: Silent replacement of `Fabricator.CanCraft` was rejected because the current method includes power, biome lock, storage capacity, scarcity modifiers, reservations, and network fallback.
Scalability potential: Low keeps old gameplay authority while DTO validation is staged; middle/high/ultra can migrate UI pages incrementally to packed words.
Hardware Impact: The scanner itself is editor-only. Runtime gain comes from replacing menu validation call sites after integration.

## Decision 008: BufferID And Owner Correction

Problem: The first fast-fail lane proposal used `70142..70146`, which collided with existing VR somatic buffers, and used `SystemID.GameplayPlayer` even though crafting validation is not player kinematics.
Solution: Move the lanes out of `70142..70146`, add `SystemID.Crafting=75` for acquisition and active job registration, then re-audit all explicit `BufferID` casts before finalizing the numeric range.
Rejected Alternatives: Reusing `GameplayPlayer` was rejected because it hides crafting allocation ownership inside a broader player lane and corrupts proof routing. Keeping `70142..70146` was rejected because duplicate BufferIDs are binary payload corruption.
Scalability potential: Low/middle/high/ultra all read the same stable lanes; high quality only increases scheduled publication words.
Hardware Impact: Correct ownership prevents unrelated teardown fences from completing crafting jobs at the wrong phase. Estimated i3/MX350 gain is avoided scene-transition stalls rather than per-frame ALU savings.

## Decision 020: Full BufferID Cast Collision Guard

Problem: The enum-only duplicate scan missed direct `(BufferID)71150..71168` constants owned by `ChemicalInfluenceGrid`; the first SHINOBU_317 draft overlapped `71150..71152`, which would corrupt chemical readback lanes and fast-fail telemetry/transaction lanes.
Solution: Move SHINOBU_317 fast-fail lanes to `71203..71207`, a full-script scanned free gap after `ShinobuStressDirectorOwnedSlots=71200..71202` and before biome transition docs/source range `71220..71231`. Update `H8Memory`, X-Ray scanner constants, route docs, binary ledger, status, report, and self-audit.
Rejected Alternatives: Keeping `71148..71152` because the enum was unique was rejected; real Vault identity includes explicit casts outside the enum. Moving to `71240..71244` was rejected because archived visual-aging/degradation work documents that range. Editing chemical grid IDs was rejected as cross-domain sabotage.
Scalability potential: Low, middle, high, and ultra tiers keep identical crafting truth; only the Vault identity route changed.
Hardware Impact: Prevents cache-line/contention bugs caused by two systems opening the same Vault lane. Estimated i3/MX350 gain is correctness and avoided catastrophic telemetry/chemical buffer aliasing, not frame-time reduction.

## Decision 009: Cold Acquire Versus Pure Read Split

Problem: A method named like a read accessor could allocate or grow Vault buffers, violating read purity doctrine.
Solution: `AcquireFastFailVaultBuffersCold` is the only cold growth path. `TryReadFastFailVaultBuffers` uses existing generation handles and `TryReadHandle` only.
Rejected Alternatives: A single `Resolve` helper was rejected because it can hide allocation, mutation telemetry, or stale-handle recovery behind read-looking API names.
Scalability potential: Low-tier can skip acquisition when the menu is closed; high/ultra can keep larger recipe capacities without changing the read contract.
Hardware Impact: Avoids surprise allocator/lock work in visual/UI phases. Estimated i3/MX350 gain is removing worst-case millisecond hitches from menu refresh.

## Decision 010: Fail-Closed Publication Budget

Problem: `GlobalQualityWeight` originally produced a UI budget but the job still scheduled every word, so the budget was telemetry-only.
Solution: Schedule only `ResolveWordCount(UiPublicationBudget)` workers and expose `TryReadCraftableBit` that returns false outside the published budget.
Rejected Alternatives: Clearing every unpublished word each frame was rejected because it reintroduces a low-quality bandwidth tax; letting UI read stale words was rejected because stale presentation can masquerade as truth.
Scalability potential: At low weight only the first recipe word is refreshed; mid/high/ultra smoothly approach full menu publication. Transaction truth remains exact.
Hardware Impact: On i3/MX350 a 512-recipe menu at severe thermal weight schedules 1 word worker instead of 8, saving the majority of validation scan bandwidth for UI-only publication.

## Decision 011: Transaction Quantity Preflight

Problem: CAS deduction failures after a later ingredient could be reported as atomic contention even when the resource was simply missing.
Solution: Transaction job now repeats the same SIMD quantity preflight after unlock/mask checks; only then does it enter CAS deduction and rollback.
Rejected Alternatives: Trusting UI craftable words was rejected because inventory can change between presentation and click. Returning `AtomicConflict` for all late failures was rejected because telemetry becomes diagnostically useless.
Scalability potential: Low through ultra use identical gameplay truth; quality cannot skip the transaction preflight.
Hardware Impact: Adds one bounded SoA scan only on click, not menu refresh. Estimated low-end cost is microseconds per craft action, exchanged for deterministic correctness and cleaner failure telemetry.

## Decision 012: Live Fabricator Fast Path

Problem: Leaving `Fabricator.CanCraft` on the legacy `NativeParallelHashMap`/`RecipeData.ingredients` path kept the new validator as a sidecar proof instead of burning cost from real UI and start-craft checks.
Solution: Add a partial `Fabricator` gate that builds a `RecipeRequirementDTO` cold from `RecipeData`, reads `PlayerInventory`'s cached Vault SoA with `TryReadFastFailInventorySoA`, and runs `TryEvaluateRecipeAvailability` before the legacy scratch allocator is touched. Local-only fabricators treat both success and failure as authoritative. Fabricators attached to a logistics grid accept local SoA success immediately, but fall back on local-missing results so remote network resources still count.
Rejected Alternatives: Keeping `CurrentPowerGrid != null` as a hard fast-path blocker was rejected because it disables the optimization on normal powered fabricators. Returning false on local-missing networked fabricators was rejected because it would erase legitimate logistics inventory. Replacing `ConsumeIngredients` in this pass was rejected because reservation/rollback authority spans local and network resources.
Scalability potential: Low-tier menus avoid legacy scratch setup whenever the local SoA proof is enough; middle/high/ultra keep identical gameplay truth and can spend saved CPU on richer fabricator presentation without changing authority.
Hardware Impact: On i3/MX350, the common "player already has the ingredients" check avoids `NativeParallelHashMap` population and managed ingredient scanning after one dense SoA pass. Network-missing cases intentionally pay the old cost for correctness.

## Decision 013: Diegetic UI Visible-Row Snapshot

Problem: `HectonFabricatorUI.RebuildRecipeListEntries` still called `_currentFabricator.CanCraft` for each visible row. Even after the Fabricator fast path, that meant each row could separately read inventory SoA or fall into legacy exact evaluation.
Solution: Read the inventory SoA view once per recipe-list rebuild through `PlayerInventory.TryReadFastFailInventorySoA`, then pass the same `NativeArray<uint>` hash/quantity view into `Fabricator.TryCanCraftFastFailPresentation`. The presentation gate keeps power, unlock, biome, and storage checks inside Fabricator, while returning unknown for networked local-missing rows so logistics authority falls back to the exact route.
Rejected Alternatives: Caching managed per-row booleans was rejected because it creates shadow UI truth. Calling `TryReadCraftableBit` without a stable recipe index was rejected because the recipe DTO index route is not baked yet. Skipping Fabricator power/unlock/storage checks was rejected because UI would advertise invalid starts.
Scalability potential: Low-tier UI refreshes at most one SoA snapshot and eight visible row checks; middle/high/ultra keep the same truth while visual richness can increase independently through the hologram shader.
Hardware Impact: On i3/MX350, the common local-sufficient recipe list avoids repeated `CanCraft` calls and repeated SoA handle reads. Remaining legacy fallbacks are bounded to unknown or logistics-needed rows.

## Decision 014: Rollback Descriptor Exclusion Proof

Problem: Task 13 requires the craftability UI bitmask to stay out of `StateRingBuffer`/Merkle hashing while inventory and blueprint truth remain authoritative. A doc-only assertion is too weak because another agent can silently bind the presentation lanes later.
Solution: Extend `OOP_Crafting_Scanner` to inspect `HectonRollbackNetcodeRuntime` and `RollbackNetcodeContracts` for `ShinobuFastFail*` BufferIDs/tokens and for authoritative inventory hash/copy sites. The current report records `rollbackDescriptorFastFailHits=0`, `stateSnapshotFastFailCopyHits=0`, and `rollbackAuthoritativeInventoryCopyHits=6`.
Rejected Alternatives: Editing `InitializeAuthoritativeMerkleDescriptors()` to add explicit presentation-excluded fast-fail descriptors was rejected because omitted descriptors already fall through the default presentation-excluded policy and adding them would cross the networking domain for no runtime gain.
Scalability potential: Low through ultra keep identical rollback truth; quality can shrink craftability publication without changing StateRingBuffer bytes, Merkle leaf identity, or save authority.
Hardware Impact: Prevents hashing/copying `RecipeRequirementDTO`, `CraftableWords`, and telemetry proof lanes. On i3/MX350 this avoids unnecessary snapshot bandwidth; on high-end machines it preserves bandwidth for authoritative leaves instead of UI presentation state.

## Decision 015: Compile Wall Classification

Problem: A guarded runtime build was required once CPU/dotnet policy allowed it, but the generated project currently misses DTO source files from other agents.
Solution: Launched only `dotnet build Hecton8.Core.csproj --no-restore -m:1`, not the full solution. The build failed before SHINOBU_317 diagnostics with missing external DTOs/flags: `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, `RadiationStateDTO`, and later `PlayerHandIkConfigFlags`. Focused `rg` proved the source files exist but are absent from the generated Core project compile item list.
Rejected Alternatives: Adding external VR/radiation files to `Hecton8.Core.csproj` was rejected because that is outside the crafting domain and would mask another agent's project-generation/import wall. Running a second broad solution build was rejected as compile-wall noise.
Scalability potential: No runtime scalability impact; preserving domain boundaries protects iteration time and prevents a crafting patch from owning unrelated VR/radiation assembly routing.
Hardware Impact: The targeted build consumed one guarded compile pass and stopped in 17s; no further build spam was launched.

## Decision 016: Visible Row Requirement DTO Cache

Problem: The diegetic UI row fast path still rebuilt `RecipeRequirementDTO` from `RecipeData.ingredients` on each visible-row refresh before it could use the SoA inventory snapshot.
Solution: Add a fixed per-row cache inside `HectonFabricatorUI.RecipeListEntry`: recipe reference, baked `RecipeRequirementDTO`, and batch multiplier. `Fabricator.TryCanCraftFastFailPresentation` now has an overload that accepts the prebuilt DTO while preserving power, unlock, biome, logistics, and output capacity gates.
Rejected Alternatives: A global managed `Dictionary<RecipeData, RecipeRequirementDTO>` was rejected because it creates shared shadow state and stale invalidation risk. Baking into `RecipeData` was rejected because it mutates authoring assets and crosses content ownership.
Scalability potential: Low-tier menu refreshes reuse DTOs for stable visible rows; middle/high/ultra can refresh richer hologram feedback without re-walking ingredient lists. Recipe truth and transaction authority remain unchanged.
Hardware Impact: On i3/MX350, stable visible-list refresh avoids up to eight `RecipeData.ingredients` walks per UI rebuild after the first bake; cost becomes cached DTO + SoA mask/SIMD check.

## Decision 017: Scarcity-Adjusted Fast-Fail Bake

Problem: The initial Fabricator fast path could build a DTO from raw `InventoryCost.amount`, while the exact legacy path uses `GetAdjustedIngredientAmount(cost)` to account for scarcity inflation. That creates a possible false fast-positive when scarcity raises costs.
Solution: Add `Fabricator.TryBuildAdjustedFastFailRequirement`, which preserves the 32-byte DTO contract but packs adjusted, scarcity-aware quantities. `HectonFabricatorUI` caches this adjusted DTO for current-fabricator rows and includes `ResourceScarcityDirector.RuntimeVersion` in the cache key.
Rejected Alternatives: Ignoring scarcity in fast validation was rejected as gameplay truth drift. Baking scarcity into `RecipeData` was rejected because scarcity is runtime contextual state, not authoring data.
Scalability potential: Low-tier still gets cached DTO + SoA validation; high/ultra get richer UI without divergent resource truth. Quality never changes the adjusted requirement.
Hardware Impact: Adds one adjusted bake only when row recipe/batch/scarcity version changes, avoiding repeated managed ingredient walks on stable rows while preventing false positives.

## Decision 018: Storage Capacity Fast Negative

Problem: Even after SoA ingredient success, `IsOutputStorageCapacityExceeded` can walk recipe ingredients to compute reclaimable cells, even when the output already fits in currently free cells.
Solution: Add `IsOutputStorageCapacityExceededFastOrExact` and route both presentation and storage-bark checks through it: compute output cell demand and compare against `Grid.FreeCells`; only if space is tight does it fall back to exact reclaimable-ingredient math.
Rejected Alternatives: Replacing storage capacity with a pure DTO approximation was rejected because ingredient cell-area reclaim requires item metadata and local inventory availability. Removing the capacity check from presentation was rejected because UI would advertise starts that fail immediately.
Scalability potential: Low through ultra keep exact behavior in tight-space cases; common free-space cases bypass the managed ingredient walk.
Hardware Impact: On i3/MX350, common craftable rows with enough free storage avoid `CountReclaimableIngredientCells` and `CountAvailableItemInInventory` entirely after the SoA resource proof.

## Decision 019: Raw Recipe Quantity Overflow Guard

Problem: The cold `TryBuildRequirementFromRecipeData` path multiplied `int cost.amount * int multiplier` before clamping to the packed byte quantity lane. Damaged authoring data could overflow the intermediate product, and any real requirement above `255` would be a false fast-positive if truncated to the byte cap.
Solution: Promote the multiplication to `long`; reject values above `255` so callers fall back to the exact legacy route. Apply the same `>255` rejection to the scarcity-adjusted Fabricator bake and to both char/byte CSV quantity token readers. Normalize both CSV overloads to the same result/hash/quantity interleaved field order.
Rejected Alternatives: Trusting authored recipe limits was rejected because the validator is also used by mock/CSV/debug pipelines and must fail predictably under bad data.
Scalability potential: Low through ultra keep the same 32-byte DTO and validation route; this only removes undefined edge behavior.
Hardware Impact: One cold-bake 64-bit multiply and compare per ingredient. No frame hot-path cost; prevents rare corrupt-content or scarcity-inflated false positives.

## Decision 021: Live UI/Start-Craft OOP Leakage Closure

Problem: Subagent hot-path audit found that `Fabricator.CanCraft` still called exact output-capacity reclaim scanning after the fast ingredient proof, and visible recipe rows recalculated scarcity inflation plus display-name fallback during list rebuilds.
Solution: Route `CanCraft` through `IsOutputStorageCapacityExceededFastOrExact`, extend the adjusted DTO bake to return the same max scarcity inflation multiplier from the existing ingredient pass, and cache visible-row display names by recipe reference plus localization-version invalidation. Add the X-Ray editor window to `Hecton8.Editor` through `Directory.Build.targets`, not by editing generated project files.
Rejected Alternatives: Running `GetRecipeInflationMultiplier` as a second row pass was rejected because the adjusted DTO bake already walks the same authoring list. Editing `Hecton8.Editor.csproj` directly was rejected because Unity overwrites generated project files. Deleting `RecipeData`/string authoring now was rejected because content and localization still own those cold surfaces.
Scalability potential: Low tier gets one visible-row authoring walk per recipe/batch/scarcity cache miss, then cached DTO/inflation/hash validation; middle/high/ultra spend the saved CPU on richer hologram presentation. Gameplay truth and rollback identity remain unchanged by quality.
Hardware Impact: On i3/MX350, start-craft/free-space cases skip reclaimable-ingredient scans, and stable visible rows skip the second `RecipeData.ingredients` inflation pass plus repeated localization fallback. Estimated gain is tens of microseconds per dirty fabricator menu rebuild and click path in normal free-storage cases.

## Decision 022: Direct Recipe Fast Reservation

Problem: `ConsumeIngredients` still rebuilt a direct `NativeArray<int2>` cost buffer through `CraftingSystem.TryBuildRecipeCostBuffer` before it could reserve local inventory or logistics resources, even when the same scarcity-adjusted DTO had already proven the recipe shape was <=4 ingredients and byte-packed.
Solution: Insert `TryReserveDirectFastFailRecipeCosts` before the legacy cost-buffer build. It expands the adjusted `RecipeRequirementDTO`, normalizes duplicate hashes in four scalar lanes, reserves local craft locks through the existing `PlayerInventory.TryReserveAvailableQuantityForCraft`, accumulates only local-missing quantities into the existing logistics reservation buffers, and commits through `BaseLogisticsNetwork.TryReserveResources`. Failed or unsupported fast reservations call the existing `RefundIngredients` before falling back to direct/complex legacy cost buffers.
Rejected Alternatives: Directly mutating SoA quantity cells was rejected because PlayerInventory grid anchors, craft locks, item metadata, weight, and logistics reservations remain the authoritative owner route. Deleting the complex raw-cost fallback was rejected because recipes with intermediate craft graph expansion need the existing exact graph path until content is baked into DTO indices.
Scalability potential: Low tier skips one cost-buffer bake and avoids extra authoring traversal for simple recipes; middle/high/ultra preserve the same truth while freeing CPU for richer fabricator feedback. Quality never changes reservation authority.
Hardware Impact: On i3/MX350, supported direct recipes avoid `CraftingSystem.TryBuildRecipeCostBuffer` and reserve from four packed DTO lanes. Expected gain is small but deterministic on click path, with larger benefit when frequent batch crafting starts avoid managed recipe cost buffer traffic.

## Decision 023: No UI-Owned Recipe Bitset Yet

Problem: `CraftingFastFailValidator.ScheduleAvailability` and `TryReadCraftableBit` exist, but live `HectonFabricatorUI` does not yet have a cold owner hook that can acquire/write the recipe-indexed DTO and bitset Vault lanes without hiding allocation in a visual refresh.
Solution: Keep the live UI on the visible-row SoA snapshot plus row DTO cache until a Fabricator/boot owner phase can bake recipe indices into Vault lanes. Document this as the remaining required action instead of allocating private `NativeArray<RecipeRequirementDTO>` or a local bitset inside the UI.
Rejected Alternatives: Adding persistent native arrays to `HectonFabricatorUI` was rejected because it violates the Vault law and creates UI-owned shadow truth. Calling `AcquireFastFailVaultBuffersCold` from `RebuildRecipeListEntries` was rejected because a visual/read path must not allocate, grow buffers, or mutate global state. Scheduling same-frame validation plus readback was rejected because it would force a hidden `Complete`.
Scalability potential: Low tier remains bounded to eight visible rows and one SoA snapshot; middle/high/ultra can later switch to full packed-word publication once the owner phase exists. Gameplay truth is unchanged.
Hardware Impact: Avoids a potential millisecond hitch from hidden Vault growth or job completion in UI refresh. The cost is that full menu packed-word publication remains staged rather than falsely claimed.

## Decision 024: Forensic XML Parse Hygiene

Problem: The final report file had prose references that spelled the audit tag with raw angle brackets. A strict extractor could start at the prose mention instead of the structured block and report a malformed audit even when code verification was unchanged.
Solution: Keep raw XML tag spelling only on the single structured audit block and use plain `SELF_AUDIT` wording in prose. Status and LOG now record the strict parse result from the structured block.
Rejected Alternatives: Teaching every verifier to ignore markdown prose was rejected because logs are the long-term memory surface and must be machine-readable without special cases.
Scalability potential: No gameplay-tier effect; it protects forensic automation across low and high development machines by avoiding false audit reruns.
Hardware Impact: No frame-time gain. It avoids wasting a guarded compile slot and developer CPU on a log parsing defect.
