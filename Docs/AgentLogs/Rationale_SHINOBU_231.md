# Rationale_SHINOBU_231

Date: 2026-05-20
Status: PENDING VERIFICATION
Agent: SHINOBU_231

Problem: Upgrade stat evaluation request crosses equipment, vehicles, inventory, DataVault, visual sync, and telemetry. Direct concrete dependencies on unfinished agents would create compile walls.
Solution: Start with owner-local unmanaged DTOs/jobs and adapters that can be wired to Vault buffers later. Use `GlobalDataVault` only where existing interfaces are found; otherwise expose unmanaged kernels without inventing global surface.
Rejected Alternatives: Direct `GlobalRegistry` polling for PlayerInventory or vehicle components in the evaluation loop. This violates hot-path global authority law and creates branchy concrete coupling.
Scalability potential: Low uses the same deterministic bitwise truth at minimum cost; Middle runs full gameplay truth at normal cadence; High/Ultra spend saved time in VISUAL_SYNC through shader flags/glow/extrusion, not heavier simulation.
Hardware Impact: i3/MX350 gain depends on existing branch/OOP debt. Expected static target is eliminating virtual calls and string/list checks from stat loops; measured proof absent.

Problem: Upgrade mask layout requires ARM64-safe 64-bit reads.
Solution: Define `UpgradeMaskDTO` as `[StructLayout(LayoutKind.Explicit, Size = 16)]` with `EntityHashID` offset 0, `EquipmentHashID` offset 4, `ActiveUpgradesMask` offset 8.
Rejected Alternatives: Default sequential layout or `[StructLayout(Pack=1)]`. Sequential may drift under future edits; Pack=1 is banned for runtime DTOs with 8-byte fields.
Scalability potential: Low/Middle/High/Ultra all use same deterministic truth; presentation richness is decoupled through visual bits.
Hardware Impact: Prevents ARM64 misaligned `ulong` load traps and cache penalties; estimated prevention value is structural, not a claimed measured microsecond delta.

Problem: Complex upgrades such as depth modules do not stack linearly.
Solution: Cold-build flat LUTs for narrow bit groups and read them O(1) in the hot job. Use bit extraction and shifts to form LUT indices.
Rejected Alternatives: Sequential `if/else` priority chains and polymorphic `ApplyModifier(ref stats)` methods. Both create branch/dispatch cost and hardcoded behavior spread.
Scalability potential: Low uses the same LUT; Ultra can add visual flag richness without changing survival truth.
Hardware Impact: Replaces unpredictable branches with linear memory reads; expected low-end gain is branch predictor stability and better Burst vectorization. Measured proof absent.

Problem: Environmental upgrades require AUP precision without contaminating float stat math.
Solution: Keep AUP subtraction in double/int64 authority first, then cast local delta to `float3` for thermal/pressure sampling.
Rejected Alternatives: Casting absolute coordinates to float before subtracting origin. This loses precision at long range and breaks 50km sampling.
Scalability potential: Low samples coarse fields; Middle/High/Ultra can densify environment grids without changing the stat publication contract.
Hardware Impact: Prevents precision bugs rather than raw CPU savings. Low-end cost is bounded by branchless mask gate.

Problem: Vault ownership was incomplete if the matrix lived only as BufferID constants and job structs.
Solution: Added `UpgradeMatrixVaultHandles`, `UpgradeMatrixVaultViews`, and `UpgradeMatrixVault.AcquireHandles/TryResolveViews`. Every matrix buffer is requested through `IDataVault.GetGenerationHandle(..., NativeArrayOptions.UninitializedMemory)` under `SystemID.GameplayTools`.
Rejected Alternatives: Private persistent `NativeArray` fields in a MonoBehaviour, direct edits to `ModularEquipmentEngine`, or hard-coupling to unfinished Inventory/Vehicle agents.
Scalability potential: Low uses minimal capacity with the same mask truth; Middle expands normal gameplay capacity; High/Ultra can increase LUT/profile/visual-flag capacity without changing the stat kernel.
Hardware Impact: Low-end i3/MX350 avoids zero-fill and pointer-stale hazards; exact microseconds require Unity profiler because build/profiler was blocked by CPU gate.

Problem: Static OOP scanner produced a false runtime failure on `List<SuitUpgradeData>` inside `SuitUpgradeManager.OnValidate`.
Solution: Scanner strips `UNITY_EDITOR` preprocessor blocks before matching forbidden runtime patterns. Report now records zero runtime hits and explicitly documents the editor-only ignored catalog list.
Rejected Alternatives: Deleting editor catalog sync or broadening the forbidden rule to unrelated editor authoring utilities. Both would damage workflow without improving runtime hot paths.
Scalability potential: Runtime hardware tiers unaffected; editor-only catalog scans remain outside play-mode stat evaluation.
Hardware Impact: Runtime impact 0 us. Prevents false architectural noise from hiding real regressions.

Problem: Compile verification was requested by protocol, but hardware protection forbids dotnet when CPU is under load.
Solution: Checked `Get-Process dotnet,csc`; no compiler process was found. Checked `\Processor(_Total)\% Processor Time`; result was `100`, so no build was launched.
Rejected Alternatives: Forcing dotnet build under 100% CPU. That violates the batch mandate and risks damaging other agents' iteration loop.
Scalability potential: Not applicable to runtime tiers; protects developer workstation scheduling.
Hardware Impact: Prevented additional CPU contention. Compile/Burst proof remains pending.

Problem: Route ownership must be explicit or future agents will collide with BufferIDs.
Solution: Added `Docs/ARCHITECTURE/UPGRADE_MATRIX_COMPILER_SHINOBU_231.md` and reserved local IDs `71380..71389` plus `71410` for packed tool module rules.
Rejected Alternatives: Silent hard-cast IDs with no route card. Existing ledger already shows local hard-cast drift as an audit problem.
Scalability potential: Low/Middle/High/Ultra all consume identical truth buffers; only visual shader richness scales.
Hardware Impact: No direct microsecond delta; reduces integration collision risk.

Route Card: `SHINOBU_231_UPGRADE_MATRIX_VAULT`
Owner: SHINOBU_231 / TOOL_UPGRADE_MATRIX_COMPILER.
Phase: `PRE_SIMULATION` sync, `SIMULATION` evaluate/publish, `POST_SIMULATION` telemetry, `VISUAL_SYNC` visual flags.
Buffers: `71380 UpgradeMasks`, `71381 BaseStats`, `71382 CompiledStats`, `71383 Lut`, `71384 Rules`, `71385 TelemetryRing`, `71386 TelemetryCursor`, `71387 InventorySlots`, `71388 ItemMap`, `71389 VisualStates`, `71410 ToolModuleRules`.
Disposition: YELLOW until Unity compile, Burst Inspector, scheduler wiring, and profiler timing prove the route live.

Problem: `EvaluateUpgradeMasksJob` used `AupPrecisionMath.DowncastLocalDelta`, which performs branch guards inside a shared helper. The AUP order was correct, but the hot job proof was not strictly branchless.
Solution: Keep `AupPrecisionMath.LocalDeltaDouble` for the mandatory double-domain subtraction, then perform the float cast locally and select `float3.zero` with `math.select` when either double or float lanes are non-finite.
Rejected Alternatives: Calling the general AUP helper from the per-entity stat compiler. The helper is valid in other domains, but its `if` guards weaken the no-branch proof for this matrix kernel.
Scalability potential: Low/Middle/High/Ultra all keep identical survival truth; visual richness remains the only quality-scaled lane.
Hardware Impact: Low-end i3/MX350 saves a small branch cost and removes predictor noise from 10k-row evaluation. Estimate: 1-3 us/10k rows; Unity profiler proof pending.

Problem: VISUAL_SYNC was represented as a raw `uint` flag buffer, which could not carry continuous quality, shader extrusion, or glow intensity without additional state.
Solution: Replace the Vault visual lane with `UpgradeVisualStateDTO[64]` and add `PublishUpgradeVisualStateJob`. The job writes mask hash, entity/equipment IDs, visual flags, `GlobalQualityWeight`, smooth intensity, extrusion scale, and glow scalar.
Rejected Alternatives: Runtime mesh instantiation, GameObject fin/armor toggles, or a raw `uint` only. These either spike CPU or force later systems to invent shadow quality state.
Scalability potential: Low = flags plus near-zero smooth intensity; Middle = moderate glow/extrusion; High = full shader-driven visual overkill; Ultra = same CPU route with richer shader interpretation.
Hardware Impact: Avoids millisecond-scale managed mesh/object churn. Per-entity write is 64 bytes and false-sharing resistant.

Problem: `BuildUpgradeLUTJob.ApplyRule` still contained a `switch` lane dispatcher inside a Burst math job.
Solution: Replace the switch with twelve lane delta masks and straight-line multiplier/additive updates.
Rejected Alternatives: Keeping the cold switch because it is not per-frame. That is acceptable for runtime cost, but weak for a strict matrix compiler audit.
Scalability potential: All tiers use the same precomputed LUT. Saved cold-boot cycles are minor; the main value is deterministic branchless rule baking.
Hardware Impact: Cold boot only; removes branch table risk when generating matrices on weak devices.

Problem: Submarine transport stat resolvers still used branchy `if ((mask & bit) != 0)` chains for thrust, turn speed, depth, and integrity.
Solution: Convert installed item hashes with `math.select` into `VehicleUpgradeBits`, then resolve each stat using bit-extracted 0/1 scalars and multiplier/additive math.
Rejected Alternatives: Depending directly on the Tools namespace from the transport file. A local bit helper avoids cross-domain compile-wall coupling while preserving the same 64-bit mask pattern.
Scalability potential: Low/Middle/High/Ultra all resolve the same vehicle truth; later visual response can read `UpgradeVisualStateDTO` without changing transport physics.
Hardware Impact: Removes branch predictor work from repeated transport stat queries. Estimate: 2-8 us/transport stat refresh depending call rate; compile/profiler proof pending.

Problem: Tool runtime DTOs used sequential layout, and the only explicit tool stat compile route was the managed `ToolModuleData[]` cold loop.
Solution: Convert `ToolState`, `ToolRuntimeProfile`, and `ToolRuntimeStats` to explicit field offsets with unchanged sizes, and add `CompileToolRuntimeStatsJob` so LUT-compiled vectors can be projected into `ToolRuntimeStats` in Burst.
Rejected Alternatives: Changing hot array element sizes to 64 bytes without an integration mandate. That would break existing `ModularEquipmentEngine` size assertions and inflate memory bandwidth.
Scalability potential: Low uses the same 40-byte stat rows; Middle/High/Ultra increase visual/state richness through separate buffers, not by bloating tool stats.
Hardware Impact: ARM64 layout becomes auditable; Burst route avoids managed multiplier loops once dispatcher wiring lands. Estimate: 4-12 us/fleet pass when wired.

Problem: The polish pass changed runtime C# and normally needs a compile gate, but hardware policy still forbids dotnet under high CPU.
Solution: Rechecked compiler processes and CPU after edits. `Get-Process dotnet,csc` produced no active compiler output; `\Processor(_Total)\% Processor Time` returned `100`. No build was launched.
Rejected Alternatives: Running `dotnet build` anyway. This would violate the explicit >50% CPU gate and risk blocking other agents.
Scalability potential: Not runtime logic; protects workstation scheduling.
Hardware Impact: Prevented additional compile contention. Compile/Burst proof remains pending.

Problem: Tool module multipliers still had a managed authoring route through `ToolModuleData[]`; the existing Burst projection could consume compiled vectors, but there was no per-equipment 4-slot LUT matrix that preserved authored multipliers.
Solution: Added `ToolUpgradeModuleRuleDTO[96]`, `BuildToolModuleLUTJob`, and `EvaluateToolModuleLUTJob`. Cold facades pack ScriptableObject module data into explicit unmanaged rows; the Burst path clamps the contract to four rules / sixteen rows, then reads `toolIndex * 16 + slotMask` in O(1).
Rejected Alternatives: Directly reading `ToolModuleData[]` from runtime refresh logic or using a dictionary keyed by module ID. Both keep managed object dependencies in the stat path and block Burst.
Scalability potential: Low executes the same four-slot LUT with minimal CPU; Middle/High/Ultra do not change gameplay truth and can spend saved CPU on `UpgradeVisualStateDTO` shader intensity, glow, and extrusion.
Hardware Impact: Low-end i3/MX350 avoids per-refresh managed module iteration once wired by dispatcher. Estimate: 4-12 us/tool-fleet pass; profiler proof pending because build gate is still closed.

Problem: The hot AUP evaluator still contained a shared helper call that hides branch guards, weakening the branchless proof.
Solution: Replaced `AupPrecisionMath.DowncastLocalDelta` in `EvaluateUpgradeMasksJob` with direct localized double-to-float lane casts plus `math.isfinite` and `math.select` fallback.
Rejected Alternatives: Trusting a shared precision helper in this specific kernel. The helper is valid elsewhere, but SHINOBU_231 needs a clean hot-slice proof.
Scalability potential: All tiers keep identical stat truth; quality only controls visual scalar lanes.
Hardware Impact: Removes hidden branch risk in 10k-row upgrade evaluation. Estimate: 1-3 us/10k rows; profiler proof pending.

Problem: New tool rule DTOs need explicit ARM64 layout proof.
Solution: `ToolUpgradeModuleRuleDTO` uses `[StructLayout(LayoutKind.Explicit, Size = 96)]`: 8-byte fields at offsets 0 and 8, 4-byte scalar fields at 16..68, byte occupancy at 72, manual padding to 96. Validator checks size, `CompressedBit` offset 24, `RangeMultiplier` offset 32, and `Occupied` offset 72.
Rejected Alternatives: Sequential DTO layout or packing tool multipliers into loosely related arrays. Sequential layout risks future drift; parallel arrays complicate ownership and alias proof.
Scalability potential: Same DTO feeds every hardware tier; visual scale remains separate.
Hardware Impact: 96 bytes is a 32-byte multiple and ARM64-aligned. It is cold/pre-sim rule data, not a contested parallel write DTO.

Problem: The route card temporarily treated `71384 UpgradeRulesBuffer` as if it could store both `UpgradeBitRuleDTO` and `ToolUpgradeModuleRuleDTO`.
Solution: Added local owner ID `71410 UpgradeToolModuleRulesBuffer` and wired `UpgradeMatrixVaultHandles/Views` for `VaultGenerationHandle<ToolUpgradeModuleRuleDTO>`. Exact scan showed `71390..71409` occupied by ProceduralCoral and `71480..71489` occupied by Auxiliary, so `71410` is the nearest free local lane.
Rejected Alternatives: Reusing a typed Vault buffer for a second DTO type. That violates one fact -> one owner -> one typed route and risks stale-handle or layout mismatch failures.
Scalability potential: All tiers use the same rule rows; capacity can scale by equipment count without changing DTO layout or gameplay truth.
Hardware Impact: Prevents typed buffer alias corruption. Microsecond gain is not claimed; this is memory safety and route correctness.

Problem: The Burst tool LUT and the managed compatibility compiler could diverge in semantics, especially for `CoolingSink` max-bonus behavior.
Solution: Moved module-rule math into `UpgradeMatrixCompiler.ApplyToolModuleRule`, used fixed 4-rule/16-row matrix stride, and rewired the cold compatibility route through `ToolUpgradeSystem.CompileRuntimeStatsFromRules` before projecting to `ToolRuntimeStats`.
Rejected Alternatives: Keeping direct per-module lane multiplication in the compatibility compiler. That preserved a standard Unity object-array stat path and allowed semantic drift from the Burst matrix.
Scalability potential: Low/Middle/High/Ultra all consume identical stat truth. `GlobalQualityWeight` still only alters visual scalar output.
Hardware Impact: Removes a future hot-path hazard if compatibility compile is called during refresh. Estimated 4-12 us/tool-fleet pass remains scheduler-dependent; profiler proof pending.

Problem: Sidecar audit found `ModularEquipmentEngine` retaining `ToolModuleData[]` mirrors and `PlayerTool.CopyAuthoredModules` preserving a managed-object rebuild route.
Solution: Converted the owner mirror and scratch rows to `ToolUpgradeModuleRuleDTO[]`; `ToolMetadata` now packs public authoring modules through `CopyDefaultModuleRules`, `PlayerTool` exposes `CopyAuthoredModuleRules`, and `ModularEquipmentEngine` rebuilds via `CompileRuntimeStatsFromRules`.
Rejected Alternatives: Leaving ScriptableObject references in the runtime mirror because the path is cold. Cold rebuilds still feed authoritative stats, and object arrays weaken scanner proof.
Scalability potential: Low keeps the same four-slot LUT with no object traversal; Middle/High/Ultra spend saved CPU on visual shader flags and glow/extrusion, not gameplay stat divergence.
Hardware Impact: i3/MX350 avoids managed object dereference chains during registration/rebuild and removes a regression route into hot stat reads. Estimate remains 4-12 us/tool-fleet pass pending Unity profiler.

Problem: The scanner would overwrite the aggregate `EQUIPMENT_OPTIMIZATION_REPORT.json`, deleting other agents' report entries.
Solution: Added aggregate-preserving report replacement by locating the `SHINOBU_231` JSON object and replacing only that object; appended only if absent.
Rejected Alternatives: Writing a single-agent flat JSON file to the shared report path. That corrupts multi-agent audit data.
Scalability potential: Editor-only; runtime tiers unaffected.
Hardware Impact: Runtime impact 0 us.

Problem: Sidecar audit found `SuitUpgradeResolverJob` using a bare Burst attribute and `SuitUpgradeManager` calling `job.Execute()` for a one-element result.
Solution: Added deterministic Burst flags to the resolver job and removed the direct job execution from the manager. The manager now resolves the tiny scalar state directly and mirrors the result into the Vault result slot when available.
Rejected Alternatives: Scheduling a one-element `IJob` and completing/readback in the same frame. That violates the project rule against tiny jobs without profiler proof and adds scheduler overhead to a scalar resolver.
Scalability potential: Low devices avoid scheduler noise; Middle/High/Ultra keep identical suit stat truth. Batched resolver use remains available through the Burst job if a future dispatcher owns a real batch window.
Hardware Impact: Avoids a tiny-job path and preserves deterministic stat math. Exact microseconds require Unity profiler; estimate is scheduler overhead avoidance rather than ALU gain.

Problem: `SuitUpgradeManager` owned `NativeArray<SuitUpgradeTelemetryEntry>` as a private persistent field, violating the Vault law and scanner intent.
Solution: Replaced it with `VaultGenerationHandle<SuitUpgradeTelemetryEntry>` and buffer `71411 SuitUpgradeTelemetryRingBuffer`; the manager requests buffers during cold setup/DataVault hot-swap and resolves only phase-local `NativeArray` views for writes/dumps.
Rejected Alternatives: Keeping `Allocator.Persistent` scene ownership because the ring is small. Small private persistent arrays still fragment ownership and bypass global memory telemetry.
Scalability potential: Low keeps the 300-frame blackbox in central Vault memory; Middle/High/Ultra can inspect the same ring without creating shadow allocations.
Hardware Impact: Removes one scene-owned native allocation and stale pointer/dispose surface. Runtime telemetry write remains O(1), 64 bytes per entry.

Problem: The scanner treated `[SerializeField] SuitUpgradeData[] allUpgrades` as a forbidden runtime managed mirror even though it is an authoring catalog.
Solution: Added a serialized-authoring exemption for managed upgrade-array matches and documented that runtime mirrors must remain packed DTO/Vault, while ScriptableObject catalogs are authoring only.
Rejected Alternatives: Renaming or deleting the authoring catalog to satisfy a blunt regex. That damages designer workflow and does not improve the stat hot path.
Scalability potential: Editor-only; runtime tiers unaffected. The compiled stat path remains mask/LUT based.
Hardware Impact: Runtime impact 0 us; reduces false audit noise.

Problem: `PlayerTool.CopyAuthoredModuleRules` could return before clearing a reused scratch DTO array when metadata was null.
Solution: Clear caller-provided rule rows before the metadata null return, so stale module rows cannot survive in scratch state even if a future caller ignores the returned slot count.
Rejected Alternatives: Relying on the current caller to pass `slotCount=0`. That is true today but weaker than clearing the scratch contract at the producer boundary.
Scalability potential: Cold authoring bridge only; all hardware tiers keep the same matrix truth.
Hardware Impact: Runtime hot impact 0 us. Cold path writes at most four DTO rows.

Problem: Context compression and parallel edits repeatedly reintroduced `AupPrecisionMath.DowncastLocalDelta` into `EvaluateUpgradeMasksJob.Execute`.
Solution: Replace the helper call with direct local casts after `AupPrecisionMath.LocalDeltaDouble`, then use `math.isfinite` and `math.select` for NaN vaccination. Added scanner coverage for `HOT_EVALUATE_AUP_HELPER_DOWNCAST` so the regression becomes a report failure.
Rejected Alternatives: Accepting the shared helper because it is correct in other domains. In this hot stat compiler, the helper contains `if` guards and violates the branchless proof.
Scalability potential: Low/Middle/High/Ultra all keep identical gameplay stat truth. Visual scale remains isolated in `UpgradeVisualStateDTO`.
Hardware Impact: Removes hidden branch risk from 10k-row stat evaluation. Estimated gain: 1-3 us / 10k rows on i3/MX350-class CPUs; profiler proof pending.

Problem: Safety fixes added `if (Lut != null)` and `if (ThermalGridCelsius != null)` branches inside the hot evaluator.
Solution: Move pointer and length validation into `UpgradeMatrixScheduler.ScheduleUpgradeMaskEvaluation`. The scheduler rejects missing or empty Vault views before scheduling; the Burst `Execute` body performs straight LUT and thermal-grid reads. Missing thermodynamics must be represented by a one-cell fallback grid, not by hot-path branching.
Rejected Alternatives: Branching inside `EvaluateUpgradeMasksJob.Execute` or dereferencing unknown pointers from manually constructed jobs. The first breaks the matrix mandate; the second is unsafe without a sanctioned scheduler route.
Scalability potential: Low devices use the same branchless stat truth with a one-cell fallback grid when needed. Middle/High/Ultra can feed denser thermal grids without changing the evaluator ABI.
Hardware Impact: Per-row branch removal improves predictor stability; the exact microsecond gain is profiler-pending. The cold scheduler adds no gameplay-loop branch cost inside the data-parallel kernel.

Problem: Existing static scanner proved OOP modifier purge but did not prove the branchless hot evaluator invariant.
Solution: Extended `Polymorphic_Modifier_Scanner` with a focused `EvaluateUpgradeMasksJob.Execute` slice scanner for `if`, `else`, `switch`, `?`, and `DowncastLocalDelta`. Updated the aggregate JSON report with `hotEvaluateForbiddenMatches: 0`.
Rejected Alternatives: Manual-only grep after regressions. That is fragile under concurrent agents and context compression.
Scalability potential: Editor-only guard. Runtime tiers unaffected.
Hardware Impact: Runtime impact 0 us. Prevents future branch/helper regressions from silently returning to the hot stat path.

Problem: Compile verification remains required, but the workstation gate forbids launching dotnet under heavy CPU load.
Solution: Rechecked `dotnet.exe`/`csc.exe` processes and CPU. No compiler process existed, but CPU sample was `100`, so no build was launched.
Rejected Alternatives: Forcing `dotnet build Hecton8.Core.csproj` under a 100% CPU sample. That violates the AGENTS CPU gate and can block other active agents.
Scalability potential: Not runtime logic; protects shared iteration hardware.
Hardware Impact: Prevented compile contention. Unity compile, Burst Inspector proof, and profiler timing remain pending.

Problem: Sidecar audit found the tool module matrix existed but was not live in `ModularEquipmentEngine`.
Solution: Added Vault-backed staging for `UpgradeMaskDTO`, identity base stat vectors, `ToolUpgradeModuleRuleDTO[4]`, and `ToolRuntimeProfile`, then chained `UpgradeMatrixScheduler.ScheduleToolModuleMatrix` after active equipment integration. The returned `JobHandle` replaces `_equipmentIntegrationHandle` and is registered with `H8Memory`.
Rejected Alternatives: Keeping the Burst matrix as a static library while managed compatibility compilation remains the only live route. That fails the assignment because the if/else purge would be architectural theater.
Scalability potential: Low/Middle/High/Ultra all use identical 64-bit stat truth. Low devices pay fixed 4-slot/16-row LUT math; high/ultra can spend saved CPU through `UpgradeVisualStateDTO` shader response.
Hardware Impact: Enables the 4-12 us/tool-fleet pass target on i3/MX350-class hardware, but profiler proof remains pending because the CPU gate blocked build/play-mode validation.

Problem: `MaxTrackedTools` scheduling with `UninitializedMemory` Vault buffers could read unused tool profile/rule tails.
Solution: Added `ResolveUpgradeMatrixScheduleCount()` to schedule only through the highest used slot, and cleared matrix staging on unregister and overcharge failure. Holes below the highest slot are cleared by the same owner phase that clears `_slotUsed`.
Rejected Alternatives: Per-frame clearing every matrix buffer or scheduling all 16 rows blindly. The first violates the zero-init overhead goal; the second risks stale/uninitialized stats.
Scalability potential: Low devices avoid unnecessary rows; Middle/High/Ultra keep deterministic stat truth and can increase visual response without expanding gameplay DTO layout.
Hardware Impact: Avoids up to 16 rows of unnecessary matrix work and prevents stale profile/rule reads. Microsecond gain is small at current capacity but correctness-critical.

Problem: The hot evaluator repeatedly regressed back to `AupPrecisionMath.DowncastLocalDelta` during concurrent edits.
Solution: Replaced the helper again with direct lane casts after `AupPrecisionMath.LocalDeltaDouble`, then ran the hot-slice scanner twice including a 500 ms delayed scan.
Rejected Alternatives: Trusting the helper because it is semantically safe. The helper contains branch guards and breaks this job's branchless proof.
Scalability potential: All tiers keep identical gameplay math; quality scaling remains isolated to visual scalar output.
Hardware Impact: Removes hidden branch risk from 10k-row evaluation. Estimate remains 1-3 us / 10k rows on low-end CPUs; profiler pending.

Problem: 64-bit masks were partially undermined by 32-bit LUT index clamping and a low-32-only tool state field.
Solution: Removed `0x7FFFFFFF` truncation from the matrix evaluator, added `ToolState.UpgradeBitmask64`, and kept `UpgradeBitmask` only as a low-32 compatibility mirror.
Rejected Alternatives: Continuing to cast masks down to 32 bits. That silently drops high-bit visual/upgrade state and contradicts the SHINOBU_231 64-bit authority requirement.
Scalability potential: Low through Ultra use the same mask identity; high visual bits can drive richer shader lies without touching gameplay stat ownership.
Hardware Impact: Correctness gain; no direct microsecond claim. It prevents high-bit state loss and keeps ARM64 8-byte alignment at offset 24.

Problem: Transport upgrades were mathematically branchless but still stored their authoritative installed mask and enum as 32-bit values.
Solution: Converted `VehicleUpgradeBits` to an `ulong` enum, converted `VehicleUpgradeModule._runtimeInstalledUpgradeMask` to `ulong`, and changed `SubmarineCoreDirector` installed-mask composition to `ulong`. Legacy `VehicleUpgradesChangedSignal.UpgradeMask` keeps receiving the low 32 bits only because that signal ABI is outside this domain.
Rejected Alternatives: Widening only at stat-read sites. That hides a 32-bit authority source and would lose future high transport bits before the branchless resolver sees them.
Scalability potential: Low/Middle/High/Ultra keep identical transport truth; high transport visual bits can later feed shader-only upgrade lies without another ABI migration.
Hardware Impact: Correctness and future-proofing. No direct microsecond gain claimed; branchless resolver still saves the previously estimated 2-8 us/transport refresh.

Problem: Context resume required proof that the recurrent hot-path regression had not returned.
Solution: Re-read the on-disk status/rationale, extracted the `SHINOBU_231` assignment block from `CURRENT_BATCH.md` lines 2321-2385, then re-scanned `EvaluateUpgradeMasksJob.Execute`. The hot-slice regex reported `hot_forbidden_matches=0`, and line 1013 remains a direct localized double-to-float lane cast.
Rejected Alternatives: Trusting the compressed chat summary or scanning only whole-file `rg` hits. Whole-file hits include valid scanner regex text and unrelated laser-cutter jobs; the evidence must target the upgrade matrix kernel itself.
Scalability potential: Low/Middle/High/Ultra keep identical 64-bit gameplay truth. `GlobalQualityWeight` only scales optional visual output and cadence, never stat identity.
Hardware Impact: Preserves the hidden-branch removal target in the 10k-row evaluator. Estimate remains 1-3 us / 10k rows on i3/MX350-class silicon; Unity profiler proof remains pending under the CPU gate.

Problem: Static report proof and compile gate needed a fresh post-resume sample.
Solution: Parsed `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json`; SHINOBU_231 reports `PASS`, `forbiddenPatternHits=0`, and `hotEvaluateForbiddenMatches=0`. Rechecked compiler processes and CPU: no `dotnet.exe` or `csc.exe`, CPU sample `100`, so no build was launched.
Rejected Alternatives: Running `dotnet build` under the explicit >50% CPU prohibition or reporting scanner state only in chat.
Scalability potential: Editor/build proof only; runtime tiers unaffected.
Hardware Impact: Avoided extra compiler contention on the shared workstation. Compile, Burst Inspector, and profiler timing remain the only unresolved proof artifacts.

Problem: `RecordUpgradeTelemetryJob` and `UpgradeVisualStateDTO` were documented but not live in the `ModularEquipmentEngine` owner route.
Solution: Added `71385/71386/71389` generation handles and views to the active equipment owner, included them in readiness/lifecycle release, and chained `PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob` after `CompileToolRuntimeStatsJob`. The final `JobHandle` registered with `H8Memory` now includes the visual and blackbox proof rows.
Rejected Alternatives: Leaving the telemetry job as an unused static artifact or creating a separate manager-owned native ring. The first fails Task 15; the second violates one owner -> one route -> one proof artifact.
Scalability potential: Low devices still evaluate identical gameplay truth and pay only a bounded 64-byte telemetry row plus visual row. Middle/High/Ultra can spend saved CPU on richer shader interpretation of `UpgradeVisualStateDTO` without changing mask authority.
Hardware Impact: Runtime proof remains profiler-pending. The expected cost is O(n) active mask aggregation for telemetry after the O(1) stat matrix, bounded by active tool count; correctness gain is blackbox observability and release-safe Vault descriptors.

Problem: `RecordUpgradeTelemetryJob.Execute` still contained a guard branch, and Unity `Time.frameCount` remained in upgrade-owned signal/telemetry payloads.
Solution: Moved telemetry view validation to `UpgradeMatrixScheduler.ScheduleTelemetry(...)`, added `EvaluatedMaskCount`, and made `RecordUpgradeTelemetryJob.Execute` branchless under the approved scheduler. Replaced `VehicleUpgradeModule` and `SuitUpgradeManager` frame stamps with owner-local monotonic counters.
Rejected Alternatives: Keeping defensive branches inside the Burst telemetry kernel or using Unity frame time as rollback-adjacent identity. Both undermine deterministic proof even if they are not the core stat math.
Scalability potential: Gameplay stat truth remains identical from Low through Ultra. Visual scaling remains continuous through `UpgradeVisualStateDTO`.
Hardware Impact: Removes branch proof debt from the telemetry kernel and removes Unity frame-time dependency from upgrade payloads. Microsecond gain not claimed; determinism and forensic correctness are the reason.

Problem: The recurring `AupPrecisionMath.DowncastLocalDelta` regression appeared again inside `EvaluateUpgradeMasksJob.Execute` after the telemetry route edits.
Solution: Patched the evaluator back to direct local lane casts and reran a delayed hot-slice scan. The hot evaluator now reports zero forbidden matches; whole focused search finds `DowncastLocalDelta` only in the scanner's regex string.
Rejected Alternatives: Treating the JSON report as sufficient proof. The report was stale relative to the source line and would have hidden a real hot-path helper branch.
Scalability potential: Low/Middle/High/Ultra stat truth remains identical. No quality switch touches this path.
Hardware Impact: Preserves the 1-3 us / 10k-row branch-removal target and avoids hidden helper guards in the Burst evaluator. Profiler proof remains blocked by CPU gate.

Problem: `Polymorphic_Modifier_Scanner` could regenerate a stale SHINOBU_231 report after the live telemetry/visual route work. The existing JSON artifact had the correct 71385/71386/71389/71412 route, but the scanner source still emitted the older string.
Solution: Updated the scanner generator itself to emit `UpgradeTelemetryEntry[64] via 71385/71386`, `UpgradeVisualStateDTO[64] via 71389`, `ToolRuntimeProfile[48] via 71412`, owner-local frame counters, and the live `BuildToolModuleLUTJob -> EvaluateToolModuleLUTJob -> CompileToolRuntimeStatsJob -> PublishUpgradeVisualStateJob -> RecordUpgradeTelemetryJob` chain. Added generated `editorOnlyIgnored` proof for the `SuitUpgradeManager` `#if UNITY_EDITOR` catalog `List<SuitUpgradeData>`.
Rejected Alternatives: Manually preserving only `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json`. That would pass one static read but fail on the next scanner run, silently erasing the telemetry route proof.
Scalability potential: Editor-only audit route. Gameplay stat truth remains identical on Low/Middle/High/Ultra; visual scale still lives in shader-facing `UpgradeVisualStateDTO` and not in stat authority.
Hardware Impact: Runtime impact 0 us. Prevents future audit drift from hiding actual route regressions; hot job scan remains `forbidden_matches=0` after delayed verification.

Problem: The initial report parse during this pass looked empty because it searched for obsolete `agentId` fields instead of the current aggregate `reports[].agent` schema.
Solution: Re-validated `Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json` through `reports[]`, confirmed 3 aggregate entries and SHINOBU_231 `verdict=PASS`, `forbiddenPatternHits=0`, `hotEvaluateForbiddenMatches=0`.
Rejected Alternatives: Treating the empty parse as a failed scanner or rewriting the report schema. The file was valid; the local verification script was wrong.
Scalability potential: Documentation/audit only; no runtime tier changes.
Hardware Impact: Runtime impact 0 us. Improves proof accuracy for future resume passes.

Problem: A wider all-job scan caught `AupPrecisionMath.DowncastLocalDelta` reintroduced in `EvaluateUpgradeMasksJob.Execute` even after the previous narrow delayed scan passed. The repeated drift proves that narrow proof can miss a concurrent overwrite window.
Solution: Patched line 1064 back to direct local `float3` casts after `AupPrecisionMath.LocalDeltaDouble`, then delayed 1500 ms and scanned every `IJob` / `IJobParallelFor` `Execute` in `UpgradeMatrixCompiler.cs`. All eleven job bodies returned `forbidden_matches=0`, including the real publish jobs `PublishActiveEquipmentStatsJob` and `PublishVehicleKinematicStatsJob`.
Rejected Alternatives: Logging only the two critical jobs or accepting a shared AUP helper in the evaluator. The helper is semantically safe elsewhere but contains hidden branch guards and violates SHINOBU_231's hot-path contract.
Scalability potential: Low/Middle/High/Ultra keep identical stat truth. No quality switch touches core stat computation; `GlobalQualityWeight` remains visual-only through `UpgradeVisualStateDTO`.
Hardware Impact: Preserves the 1-3 us / 10k-row hidden-branch removal target on low-end CPUs. Runtime profiler proof remains gated by CPU load.

Problem: Compile-wall proof was incomplete because dirty `.asmdef` files exist in the shared worktree.
Solution: Checked `git diff --name-only -- '*.asmdef'` and current `Hecton8.Core.asmdef`. There are dirty assembly definition files from other agents, but this pass did not edit them or add a sibling Runtime dependency. Touched runtime source uses existing Core/Contracts-facing routes and the upgrade matrix remains in the existing Core compile surface.
Rejected Alternatives: Reverting or editing unrelated asmdefs. That would violate the multi-agent worktree rule and risk breaking other domains.
Scalability potential: Build architecture only; runtime hardware tiers unaffected.
Hardware Impact: Protects iteration speed by avoiding new assembly coupling. No runtime microsecond claim.
