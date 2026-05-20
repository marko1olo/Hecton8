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
Solution: Added `Docs/ARCHITECTURE/UPGRADE_MATRIX_COMPILER_SHINOBU_231.md` and reserved local IDs `71380..71389`.
Rejected Alternatives: Silent hard-cast IDs with no route card. Existing ledger already shows local hard-cast drift as an audit problem.
Scalability potential: Low/Middle/High/Ultra all consume identical truth buffers; only visual shader richness scales.
Hardware Impact: No direct microsecond delta; reduces integration collision risk.

Route Card: `SHINOBU_231_UPGRADE_MATRIX_VAULT`
Owner: SHINOBU_231 / TOOL_UPGRADE_MATRIX_COMPILER.
Phase: `PRE_SIMULATION` sync, `SIMULATION` evaluate/publish, `POST_SIMULATION` telemetry, `VISUAL_SYNC` visual flags.
Buffers: `71380 UpgradeMasks`, `71381 BaseStats`, `71382 CompiledStats`, `71383 Lut`, `71384 Rules`, `71385 TelemetryRing`, `71386 TelemetryCursor`, `71387 InventorySlots`, `71388 ItemMap`, `71389 VisualStates`.
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
