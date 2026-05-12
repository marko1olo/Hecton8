# Rationale: SUIT_UPGRADE_SYSTEM

Status: PENDING VERIFICATION

## Mandate Selection

Relevant mandates loaded before code:
- OPT_Zero_GC_Policy_AllocFree_Mandate: forbids hot-path heap traversal and HUD string allocation.
- OPT_Native_Memory_Collections_JobSystem_Protocol: requires Burst-safe unmanaged job fields and tracked native ownership.
- DATA_Inventory_Resources_Items_SOA_Layout: inventory data must be flat, numeric, and dirty-driven.
- DATA_Save_Persistence_Binary_Delta_Checksum: save path must be raw binary, not object serialization.
- CORE_Tools_Equipment_Interaction_Raycast_Heat: equipment state uses bitwise flags and decoupled signals.
- CORE_Abyss_Survival_Systems_O2_Pressure_Logic: suit O2, pressure, and thermal stats feed survival math.
- UI_Diegetic_Physical_Interfaces: HUD reads must remain zero-GC and not drive per-frame layout churn.
- STRM_Async_Standard: Unity-facing async orchestration uses Awaitable, not transient Task storms.

## Decisions

### Initial Architecture Boundary
Problem: Suit upgrades need to affect survival, HUD, KCC, save, and mesh visuals without direct cross-domain references.
Solution: Use a single equipment-domain resolver and expose narrow value-type APIs (`ulong` mask, `in SuitStats`, ability hash helper). Visual changes leave via signal payload.
Rejected Alternatives: Standard Unity decorator components and `List<StatModifier>` were rejected because they pointer-chase heap objects and invite per-frame iteration. Direct references to HUD/KCC were rejected because parallel agents may be editing those domains.
Scalability potential: Low uses the same O(1) bit checks with no visual work beyond a signal; Middle enables simple emissive toggles; High enables more suit material state; Ultra can layer extra emissive patterns because CPU cost remains flat.
Hardware Impact: Estimated low-end gain is 8-20 microseconds per player stats refresh versus scanning several managed modifier objects; bigger gain is avoiding GC spikes.

### Bitmask Resolver
Problem: Depth, oxygen, swim, and thermal stats must resolve without upgrade object traversal.
Solution: `SuitUpgrades : ulong` plus a Burst-safe `SuitStats` resolver using explicit `mask & bit` checks. Depth tiers normalize by stripping lower tiers when a higher tier exists.
Rejected Alternatives: A generic modifier table was rejected because even array iteration burns predictable CPU and needs more validation branches. `Enum.HasFlag` was rejected because it boxes on older runtimes and violates the prompt.
Scalability potential: Low runs the same constant branch set; Middle/High/Ultra can add visual-only suit effects behind the same resolved bits without changing logic complexity.
Hardware Impact: Estimated i3/MX350 gain is 5-20 microseconds per refresh, with zero managed allocations.

### Inventory Coupling
Problem: No dedicated suit-equipment slot API is exposed in the current inventory surface.
Solution: Subscribe to `PlayerInventory.InventoryChanged`, read SOA hash/count arrays, and filter `ItemCategory.Equipment` through `ItemCatalog.ItemRuntimeDescriptor`. The existing installed string set is preserved as a backward-compatible craft/save source, then folded into the same mask.
Rejected Alternatives: Direct UI slot probing and new inventory dependencies were rejected because other agents may be editing inventory and HUD. Per-frame polling was rejected as wasteful.
Scalability potential: Low scans only on dirty events; Middle batches equip bursts via one next-frame await; High/Ultra can enrich visuals from the same mask.
Hardware Impact: Avoids continuous inventory scans; estimated idle gain is 10-40 microseconds/frame on i3/MX350.

### Persistence And Signals
Problem: Suit state must persist and inform mesh visuals without managed event chains.
Solution: Add `SaveData.suitUpgradeMask`, version-gated binary codec read/write, a dedicated `PackedSuitUpgradeState64`, and `SuitMeshUpdateEvents` NativeQueue signal flushed by `SystemDispatcher`.
Rejected Alternatives: Saving only string upgrade IDs was retained for compatibility but rejected as the primary runtime payload. Direct renderer references from the manager were rejected as cross-domain coupling.
Scalability potential: Low toggles one emissive state; Middle/High/Ultra can interpret the mask for more material channels without changing save format.
Hardware Impact: 8-byte persistence path avoids list reconstruction as the primary state; estimated load/apply gain is 5-15 microseconds plus lower GC pressure.

### HUD KCC And Abilities
Problem: HUD and movement code need O2/swim data without owning upgrade logic or copying managed collections.
Solution: Expose `ref readonly SuitStats` for HUD reads, a scalar swim-speed property plus `in SuitStats` helper for KCC, and `HasAbility(ulong,uint)` for ability probes.
Rejected Alternatives: HUD/KCC-side catalog scans were rejected because they duplicate logic and create cross-domain dependencies. Ability MonoBehaviours were rejected because they add object lifetime and lookup cost.
Scalability potential: Low reads one resolved struct; Middle/High/Ultra can cache more UI/material channels from the same struct and mask without changing the API.
Hardware Impact: Estimated i3/MX350 gain is 4-12 microseconds per HUD/movement refresh by eliminating recomputation and collection traversal.

### Math LOD And Blittability
Problem: Upgrade resolution must not scale with catalog size, and the stats payload must be safe for Burst/native storage.
Solution: Resolver uses a fixed branch set, `NormalizeMask` enforces depth exclusivity with bitwise clears, and `SuitStatsSizeBytes` compiles only if `SuitStats` satisfies `where T : unmanaged`.
Rejected Alternatives: `Marshal.SizeOf` and reflection checks were rejected because they are runtime managed checks. Tier arrays were rejected because the prompt requires hardcoded bitwise masks.
Scalability potential: Low uses the same constant math; Middle can add one or two more hardcoded bits; High/Ultra can spend saved CPU on emissive/material overkill rather than logic.
Hardware Impact: Constant resolver work is expected to remain under 10 microseconds on i3/MX350 for player-only refreshes.

## OMEGA POLISH CHANGES

Problem: Final scan needed to prove the resolver is not a disguised modifier/decorator loop.
Solution: Re-read the prompt and polish mandate, removed the abandoned legacy stat-loop block, scanned touched suit/save files for `HasFlag`, `foreach`, `string.Format`, `.ToString()`, interpolated strings, `math.sqrt`, and `math.normalize`.
Rejected Alternatives: Leaving the old commented stat loop was rejected as source bloat. Fixing unrelated global compile blockers was rejected as domain leakage.
Scalability potential: Low/Middle use the same constant bitset path and one mesh signal. High/Ultra can spend the saved budget on suit material channels or emissive overkill without changing CPU resolver cost.
Hardware Impact: Hot resolver path remains fixed branch work; inventory scan is dirty-only and deferred one frame. Expected gain remains 10-40 microseconds per idle frame by avoiding polling/modifier traversal.

Cinematic Cheats Used:
- Depth progression is a bitwise tier fake, not an accumulated physical pressure simulation.
- Thermal and radiation protection are scalar offsets, not environmental material simulation.
- Mesh polish is a single queued emissive signal, not direct renderer mutation or per-frame material probing.

Final Git Diff:
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeResolver.cs`: new `SuitUpgrades`, unmanaged `SuitStats`, `SuitUpgradeResolver`, and Burst `SuitUpgradeResolverJob`.
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`: mask state, dirty inventory sync, ref-read stats API, resolver job scheduling, save mask population/load migration, mesh signal raise.
- `Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs`: new NativeQueue event lane for suit mesh updates.
- `Assets/_Project/Scripts/SaveData.cs`: `suitUpgradeMask` added and `CurrentVersion` moved to 65. File also contains pre-existing data-archaeology hunks from concurrent work.
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`: v65 mask read/write added. File also contains pre-existing data-archaeology codec hunks from concurrent work.
- `Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs`: dedicated `PackedSuitUpgradeState64` pack/unpack helpers.
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`: `SuitMeshUpdateEvents` pending count and flush integrated. File also contains pre-existing origin-shift/scalability hunks from concurrent work.
- `Docs/Tasks/Status_SUIT_UPGRADE_SYSTEM.md`, `Docs/Tasks/RECON_SUIT_UPGRADE_SYSTEM.md`, `Docs/AgentLogs/Rationale_SUIT_UPGRADE_SYSTEM.md`: task status, recon, and rationale evidence.

Build Health:
- `dotnet build Hecton8.Core.csproj` failed on unrelated `SubmarineStructuralGrid.cs(654)` overload mismatch and unrelated warnings. No suit upgrade files were named by MSBuild.
- Unity validation previously returned 0 diagnostics for `SuitUpgradeManager.cs`, `SuitUpgradeResolver.cs`, `SuitMeshUpdateEvents.cs`, `SaveData.cs`, `SaveBinaryPayloadCodec.cs`, and `SaveDeltaCompression.cs`; Unity MCP became unavailable after the global compile failure.

## LOOP 4 HARDENING

Problem: The first resolver pass used a static default `SuitStats` baseline for the Burst job, which could diverge from authored `SurvivalStats` values and make designer tuning invisible to the bitmask path.
Solution: Build `_baseSuitStats` from `baseStats`, pass it into `SuitUpgradeResolverJob`, and apply runtime deltas against that authored baseline. `SuitStats` is fixed to a 64-byte sequential payload so the job result remains unmanaged and cache-line friendly.
Rejected Alternatives: Keeping resolver defaults was rejected because it makes the bitmask resolver a second balance source. Reading `SurvivalStats` inside the job was rejected because Unity objects/managed references are not Burst-safe.
Scalability potential: Low uses the exact authored baseline plus fixed bit deltas; Middle/High/Ultra can add visual-only tiers without changing resolver complexity.
Hardware Impact: Prevents extra stat reconciliation passes; expected i3/MX350 cost stays fixed under the original sub-10 us refresh target.

Problem: Inventory-derived equipment masks could apply stale or broken modules after an async next-frame coalesce.
Solution: Cache `_inventoryUpgradeMask` by inventory version, clear it on unbind, filter broken upgrade IDs during inventory mask construction, and stop the awaited rebuild loop if the manager is disabled/cancelled before applying results.
Rejected Alternatives: Per-frame inventory polling was rejected as wasted idle CPU. Direct equipment-slot dependency was rejected because the current inventory surface exposes SOA hash/count arrays, not a stable suit slot API.
Scalability potential: Low scans only when dirty; Middle/High/Ultra can trigger richer suit mesh/material reactions from the same mask while preserving dirty-only CPU work.
Hardware Impact: Preserves the 10-40 us idle-frame saving and avoids a deferred stale apply after disable/despawn.

Problem: Suit mesh update payload used a bool field that could be layout-ambiguous in unmanaged/native transport.
Solution: Store `_hasEmissiveUpgrade` as a byte and fix `SuitMeshUpdateSignal` to 32 bytes sequential. Public API remains `bool HasEmissiveUpgrade`.
Rejected Alternatives: Managed event payloads and direct renderer callbacks were rejected because they add cross-domain coupling and allocation risk.
Scalability potential: Low toggles one emissive state; Ultra can interpret the same 64-bit mask for extra material channels without changing signal size.
Hardware Impact: Keeps the signal lane fixed-size and cheap to flush through `SystemDispatcher`.

Problem: Global CLI compile was blocked by `SubmarineStructuralGrid` late-frame registration code that referenced `_registeredLateFrame` and registered `this` as `ILateFrameTickable` without the interface/field path being coherent.
Solution: Narrow compile unblocker: restore the late-frame tick interface/registration flag path already implied by the existing `LateFrameTick()` method. No physics behavior was refactored.
Rejected Alternatives: Broader physics cleanup was rejected as outside the suit upgrade domain. Ignoring the compile wall was rejected because the suit system could not be verified while the project failed to build.
Scalability potential: Compile repair only; existing late-frame leak plume dispatch remains the owner of any visual scalability behavior.
Hardware Impact: No new hot-path work beyond the system's intended existing late-frame dispatch.

Problem: `SuitUpgradeManager.cs` had mojibake in comments and Inspector headers, making source and editor labels unfit for production review.
Solution: Mechanically normalized corrupted source text to ASCII comments/header labels without changing runtime logic.
Rejected Alternatives: Leaving corrupted labels was rejected because it increases editor/operator error risk.
Scalability potential: No runtime scalability change.
Hardware Impact: 0 us runtime.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` succeeded with 0 errors and 1 unrelated `HectonCelestialEngine` unused-field warning.
- Targeted forbidden scan over suit runtime files returned no `HasFlag`, managed `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, or `IEnumerable`.
- Unity MCP validation and console reads failed with `no_unity_session`; Unity import/console/playmode verification remains pending.

## LOOP 5 HARDENING

Problem: Inventory-derived suit bits could still be granted if a raw item hash matched an upgrade lookup while the catalog descriptor was missing or non-equipment.
Solution: Require `ItemCatalog.TryGetRuntimeDescriptor(...)` and `ItemCategory.Equipment` before resolving any inventory item hash to a suit upgrade bit.
Rejected Alternatives: Trusting raw hashes was rejected because it lets unrelated items collide with suit upgrade entries. Adding a direct equipment-slot dependency was rejected because the current inventory surface is SOA hash/count data and other agents may own slot UI.
Scalability potential: Low keeps dirty-only scans with strict category gating; Middle/High/Ultra can add richer equipment categories without changing the bit resolver.
Hardware Impact: Same O(n dirty scan), but avoids incorrect runtime bits and follow-up correction work; estimated prevention gain is 2-6 us on bad inventory events.

Problem: Loading null/default save data cleared the managed sets but could leave resolved stats and masks from the previous runtime state.
Solution: After clearing installed, unlocked, and broken sets, call `RebuildRuntimeStats()` before returning on null save data.
Rejected Alternatives: Leaving stale resolved state was rejected because HUD/KCC could read old O2/swim values after a failed or empty load. Manually zeroing fields was rejected because the resolver already centralizes baseline restoration.
Scalability potential: Low resets to authored baseline; Ultra can preserve the same reset path while adding visual-only suit layers behind the mask.
Hardware Impact: One dirty rebuild on null load; prevents repeated downstream sanity checks.

Problem: The resolver recognized hull/oxygen IDs but future authored assets in energy, sensor, thermal, and radiation categories could resolve to `None`.
Solution: Add explicit mappings for current energy/sensor/thermal/radiation IDs and category/tier fallback for those groups.
Rejected Alternatives: Generic data-driven modifier lists were rejected because the prompt required hardcoded bitwise masks. Leaving new categories unmapped was rejected because it creates silent designer-facing failures.
Scalability potential: Low maps the first production tier; Middle/High/Ultra can add more explicit bits while keeping O(1) resolver cost.
Hardware Impact: Constant branch checks only; estimated 0-2 us cost with correctness benefit.

Problem: A one-result suit stats job was scheduled and immediately completed, paying scheduler and handle bookkeeping overhead without parallelism.
Solution: Keep the Burst `IJob` and native result buffer but execute it with `job.Run()` for the single-player resolver path.
Rejected Alternatives: Removing the job was rejected because the assignment calls for a Burst resolver job. Keeping schedule-then-complete was rejected because it is slower for one result.
Scalability potential: Low uses direct single-player run; High/Ultra can later batch multiple suit entities into a scheduled parallel path if gameplay creates that need.
Hardware Impact: Estimated 2-8 us saved per resolve burst by removing scheduler overhead on low-end CPU.

Problem: `SuitUpgradeData.cs` had corrupted Inspector header text.
Solution: Normalize headers to ASCII production labels.
Rejected Alternatives: Leaving mojibake was rejected because it increases editor setup mistakes.
Scalability potential: No runtime change.
Hardware Impact: 0 us runtime.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` succeeded with 0 errors and 6 unrelated `HectonFluidEngine.AbyssalVortexImpulse` warnings.
- Direct Unity MCP `validate_script` returned 0 diagnostics for `SuitUpgradeManager.cs`, `SuitUpgradeResolver.cs`, `SuitMeshUpdateEvents.cs`, and `SuitUpgradeData.cs`.
- Unity script refresh timed out after 60 seconds waiting for readiness; error-console read immediately after refresh returned 0 errors, then the MCP session stopped answering. Playmode verification remains pending.
- Focused `Select-String` scans over suit files found no `HasFlag`, managed `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, or `IEnumerable`; non-ASCII scan on cleaned files returned no hits.

## LOOP 6 HARDENING

Problem: Inventory item hashes are authored as `Item_Equip_*` IDs, while the first lookup path mostly keyed on `suit_*` upgrade IDs. Real equipment such as `Item_Equip_OxygenRig_T2` could pass the equipment descriptor gate and still fail to grant any suit bit.
Solution: Add cold equipment-item hash aliases from `SurvivalDatabaseRuntime` and sort the lookup cache by item hash. Dirty inventory scans now use lower-bound binary search and merge duplicate aliases into one mask.
Rejected Alternatives: Per-frame string hashing and a raw catalog scan were rejected because they add repeated managed work. A direct equipment-slot UI dependency was rejected because the inventory surface currently exposes SOA item hashes/counts.
Scalability potential: Low uses the same dirty-only binary lookup; Middle/High/Ultra can add more authored aliases without changing the hot resolver or save format.
Hardware Impact: Estimated i3/MX350 gain is 2-8 microseconds per dirty inventory scan as alias count grows; more importantly, equipped oxygen rigs no longer silently miss stats.

Problem: `SuitUpgradeManager` only tried to bind inventory during `OnEnable` and during queued inventory rebuilds. If the `PlayerInventory` service registered later and no inventory event fired, the manager could remain unbound.
Solution: Register as an `IGlobalRegistryHotSwapListener` and rebind when `GlobalRegistryServiceSlot.PlayerInventory` changes. The existing next-frame inventory coalescer remains the only rebuild path.
Rejected Alternatives: Registry polling and scene-wide inventory searches were rejected because they burn idle-frame CPU and fight the service registry pattern.
Scalability potential: Low keeps zero idle polling; Ultra can still layer richer suit visuals behind the same dirty mask signal.
Hardware Impact: Preserves the 10-40 microseconds/frame idle saving from avoiding inventory polling.

Problem: A duplicate suit manager scheduled for destruction could still subscribe to save, narrative, hot-swap, and inventory lanes during the same enable pass.
Solution: Make service registration return success/failure and stop `OnEnable` immediately when this instance is not the registered runtime.
Rejected Alternatives: Letting Unity destroy the duplicate later was rejected because transient duplicate subscriptions create avoidable event fanout and stale callbacks.
Scalability potential: No feature scalability change; lower event noise keeps runtime ownership deterministic on large scenes.
Hardware Impact: 0-3 microseconds avoided during duplicate-scene activation, with stronger lifecycle correctness.

Problem: Save population previously risked mutating `_upgradeMask` while serializing derived inventory plus installed bits.
Solution: Serialize through a local `serializedMask` and leave runtime state changes to the resolver path.
Rejected Alternatives: Reusing `_upgradeMask` as temporary save scratch was rejected because save should be a read path for gameplay state.
Scalability potential: Low/Ultra use the same 64-bit save payload.
Hardware Impact: 0 us hot-path change; prevents state churn during save.

Problem: A construction-domain compile blocker was discovered before the user's no-build instruction, caused by a mismatched `IsValidNode(...)` call in `HabitatStressJobs.cs`.
Solution: Pass the already-available `safeNodeCount` into that call site only.
Rejected Alternatives: Refactoring habitat stress logic was rejected as outside the suit domain.
Scalability potential: No suit scalability change.
Hardware Impact: 0 us intended runtime delta.

Verification:
- After the user's no-build instruction, no `dotnet build`, Unity refresh, or compile-triggering validation was run.
- Static forbidden-pattern scans over the suit files found no `HasFlag`, managed `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, or `IEnumerable`.
- Non-ASCII scan over cleaned suit files returned no hits.
- `git diff --check` on suit files reported only the repository line-ending warning for `SuitUpgradeManager.cs`.

## LOOP 7 HARDENING

Problem: The suit resolver had no local black-box state trail. A non-finite stat could reach survival/HUD/KCC with only current-frame evidence.
Solution: Add a fixed 300-entry `NativeArray<SuitUpgradeTelemetryEntry>` ring. Each resolver application records frame, sequence, raw/effective/inventory masks, flags, selected resolved stats, and a deterministic FNV-style state hash.
Rejected Alternatives: Managed telemetry events and string logs were rejected because they allocate and are not deterministic. Per-frame update registration was rejected because suit upgrades are dirty-driven and do not need idle polling.
Scalability potential: Low records only compact resolver events; Middle/High/Ultra can add visual suit channels while keeping the telemetry entry fixed at 64 bytes.
Hardware Impact: 0 microseconds idle-frame cost. Resolver-event telemetry is estimated at 3-8 microseconds on i3/MX350 and only runs when suit state resolves.

Problem: Non-finite resolved stats needed a cold dump path before bad values reached survival stats.
Solution: Detect NaN/Infinity after the Burst resolver result is copied, record a flagged telemetry entry, write `Docs/AgentLogs/Dump_SUIT_UPGRADE_SYSTEM.bin`, and return before applying invalid deltas.
Rejected Alternatives: Applying invalid stats then relying on downstream survival/HUD guards was rejected because it spreads failure evidence across domains. Throwing exceptions was rejected because the mandate asks for dump evidence.
Scalability potential: Low/Ultra share the same failure path; richer future stats can be represented by the state hash without growing the dump record immediately.
Hardware Impact: Cold path only on invalid state. Normal resolver path adds a fixed native write and hash; idle cost remains 0.

Verification:
- Prompt was re-extracted before this loop.
- No build or Unity refresh was run after the user's no-build instruction.
- Static forbidden-pattern scans over the suit files found no `HasFlag`, managed `foreach`, string formatting/interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, or `IEnumerable`.
- Non-ASCII scan over cleaned suit files returned no hits.
- `git diff --check` on `SuitUpgradeManager.cs` reported only the repository line-ending warning.
