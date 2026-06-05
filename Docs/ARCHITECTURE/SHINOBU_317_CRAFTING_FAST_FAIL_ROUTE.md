# SHINOBU_317 Crafting Fast-Fail Route

Owner: `SHINOBU_317 / CRAFTING_FAST_FAIL_VALIDATOR`
Status: STATIC_ROUTE_DOC / RUNTIME_PROOF_PENDING
Runtime owner ID: `SystemID.Crafting` (`75`)
Evidence class: static source, static docs, scanner output. Unity import/Burst Inspector/profiler proof remains pending under compile guard.

## Native Buffers

- `71203` `ShinobuFastFailRequirementDtos`: `RecipeRequirementDTO[recipeCapacity]`, 32 bytes, static/baked recipe requirements.
- `71204` `ShinobuFastFailCraftableWords`: `ulong[ceil(recipeCapacity / 64)]`, one bit per recipe. The validation job overwrites only scheduled whole words.
- `71205` `ShinobuFastFailTelemetryRing`: `CraftingFastFailTelemetryEntry[300]`, 64-byte black-box rows.
- `71206` `ShinobuFastFailTelemetryCursor`: `int[1]`, atomic telemetry cursor.
- `71207` `ShinobuFastFailTransactionResults`: `int[1]`, latest transaction status.

`AcquireFastFailVaultBuffersCold` is the only growth/acquire path: boot/editor/cold only. `TryReadFastFailVaultBuffers` calls read handles only; it never allocates, grows, completes jobs, or publishes.

## ABI

- `RecipeRequirementDTO=32`: `ResultItemHash uint@0`, `IngredientHashA uint@4`, `IngredientHashB uint@8`, `IngredientHashC uint@12`, `IngredientHashD uint@16`, `QuantitiesPacked uint@20`, `BlueprintUnlockMask ulong@24`.
- `CraftingFastFailTelemetryEntry=64`.

| Field | Offset |
| --- | ---: |
| `Frame` | 0 |
| `RecipeWordIndex` | 4 |
| `RecipesEvaluated` | 8 |
| `UnlockCullCount` | 12 |
| `MaskCullCount` | 16 |
| `SimdSuccessCount` | 20 |
| `ScheduleMicroseconds` | 24 |
| `GlobalQualityWeight` | 28 |
| `RequirementMask` | 32 |
| `UnlockMask` | 40 |
| `InventoryVersion` | 48 |
| `UiPublicationBudget` | 52 |
| `StateHash` | 56 |
| `Flags` | 60 |

Both structs are explicit-layout unmanaged lanes. No `Pack=1`. No hot-path properties.

## Phase Route

- SIMULATION: owner schedules `EvaluateCraftingAvailabilityJob` through `ScheduleAvailability`, consuming immutable recipe DTOs plus inventory `NativeArray<uint>` hash/quantity SoA views.
- LIVE FABRICATOR GATE:
  - Entry: `Fabricator.CanCraft` calls `HasIngredientsFastFailOrLegacy` before legacy scratch setup.
  - Inventory read: `PlayerInventory.TryReadFastFailInventorySoA`.
  - Source: cached Vault handles through `SoaInventoryQueryEngine.TryReadVaultBuffers`.
  - Quantity view: `int` quantities reinterpreted as `NativeArray<uint>`.
  - Return: `CurrentInventoryMask` plus active slot count.
  - Forbidden: allocation or job completion.
  - Scarcity proof: Fabricator DTO bake uses `GetAdjustedIngredientAmount`.
  - Output capacity: `IsOutputStorageCapacityExceededFastOrExact` sends only tight-space cases to exact reclaim scanning.
- LIVE RESERVATION GATE:
  - Entry: `ConsumeIngredients` first attempts `TryReserveDirectFastFailRecipeCosts`.
  - DTO: scarcity-adjusted `RecipeRequirementDTO`.
  - Duplicate hashes: normalized across four scalar lanes.
  - Local lock route: PlayerInventory craft locks through the existing owner API.
  - Logistics route: only remaining quantities enter `BaseLogisticsNetwork` reservation.
  - Fallback triggers: unsupported DTO shape, packed quantity above `255`, failed local reservation, failed logistics reservation.
  - Fallback cleanup: `RefundIngredients`, then existing direct/complex legacy cost buffers.
- COLD AUTHORING BAKE: raw and scarcity-adjusted quantity multiplication promotes to 64-bit before packed 8-bit requirement lane is accepted.
- Requirements above `255` reject DTO bake and fall back to exact legacy validation.
- Corrupt authoring or inflated batch quantities cannot truncate resource truth.
- CSV INGEST: char and byte CSV quantity tokens are range-checked against the same packed lane.
- Values above `255` reject before DTO construction.
- Both overloads share field order: result, hashA, quantityA, hashB, quantityB, hashC, quantityC, hashD, quantityD, unlockMask.
- DIEGETIC UI GATE:
  - `HectonFabricatorUI.RebuildRecipeListEntries` reads one SoA inventory snapshot per visible-list rebuild.
  - It caches one `RecipeRequirementDTO` plus max scarcity inflation per visible `RecipeListEntry`.
  - Key: recipe reference, batch multiplier, scarcity runtime version.
- It also caches display-name string references by recipe plus localization language event version.
- It passes the cached DTO to `Fabricator.TryCanCraftFastFailPresentation`.
- Local-known rows are colored from the SoA proof; networked local-missing rows return unknown and fall back to exact legacy logistics evaluation.
- Output storage checks and storage-bark checks first compare output demand against current free cells and fall back to exact ingredient-reclaim scanning only when space is tight.
- POST_SIMULATION: dispatcher owns completion; the job handle is returned and registered through `H8Memory.RegisterActiveJob(SystemID.Crafting, handle)`.
- VISUAL_SYNC: UI reads `CraftableWords` only through `TryReadCraftableBit`. Recipes outside the current `UiPublicationBudget` fail closed so stale low-quality words do not become gameplay truth.
- TRANSACTION: `CraftingFastFailTransactionJob` remains native CAS proof kernel for future pure-SoA owners.
- Live Fabricator consumption uses existing PlayerInventory reservation/commit owner route.
- Grid anchors, weight, craft locks, and logistics rollback remain one-fact/one-owner.
- UI bitmasks never authorize resource consumption.

Local-only fabricators treat SoA result as authoritative.

Fabricators attached to logistics grid accept local SoA success immediately. Local-missing results fall back to legacy logistics merge so remote resources remain available.

## Quality Route

`GlobalQualityWeight` maps `ResolveUiPublicationBudget` with a smooth cubic curve.

- Minimum slice: `64` recipes.
- Maximum slice: full pending recipe count.
- Low: fewer word workers.
- High/Ultra: full menu.
- Only UI publication cadence changes.
- Recipe DTO layout, inventory authority, save identity, rollback hash lanes, and transaction correctness stay fixed.

## Rollback And Lifecycle

- Inventory hash/quantity lanes remain the authoritative rollback/save facts owned by the inventory domain.
- Requirement DTOs are static payload.
- Craftable words, telemetry cursor/ring, and transaction status are presentation/proof lanes and are excluded from rollback authority; missing descriptor entries remain fail-closed under the default presentation-excluded policy.
- On stale generation reads, consumers discard the view and reacquire descriptors during the next cold owner phase; no hot fallback uses `GlobalDataVault.TryGetLatestCreated`.

- Mechanical proof:
  - `InitializeAuthoritativeMerkleDescriptors()` binds inventory hash/quantity/durability lanes.
  - It binds no `ShinobuFastFail*` buffer.
  - `StateSnapshotJob` copies the same authoritative inventory lanes.
  - Fast-fail presentation copy sources: `0`.
  - `OOP_Crafting_Scanner`: `rollbackDescriptorFastFailHits=0`, `stateSnapshotFastFailCopyHits=0`.
  - `OOP_Crafting_Scanner`: `rollbackAuthoritativeInventoryCopyHits=6`.

## Fault Route

`CraftingFastFailTelemetryEntry[300]` records recipes, unlock culls, mask culls, SIMD successes, inventory version, quality, and state hash.

Slow validation slices above `200 us` dump raw fixed rows to `Docs/AgentLogs/Dump_SHINOBU_317.bin`.

## Dear Lie

- The validator replaces legacy object/list/string recipe checks with two cheap masks before any quantity scan: `(PlayerUnlockMask & BlueprintUnlockMask)` and `(CurrentInventoryMask & RequirementMask)`.
- Only surviving recipes scan SoA quantities and compare four lanes.
- Legacy menu validation is O(recipes * ingredients * managed inventory search).
- New publication route is O(scheduledWords * 64) with bitmask culls and packed `ulong` output.
- Live Fabricator gate removes legacy `NativeParallelHashMap` scratch fill for local-proven recipes.
- Supported direct recipe starts skip legacy direct cost-buffer bake before reservation.
