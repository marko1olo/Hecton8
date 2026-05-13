# SALINITY_CORROSION_SYSTEM Status

PROMPT: SALINITY_CORROSION_SYSTEM
ROLE: SYSTEMS_ARCHITECT
DOMAIN: ECHELON 4 / Equipment Runtime / S.O.A. Inventory
STATUS: PENDING VERIFICATION - CORE VERIFIED, GLOBAL UNITY COMPILE BLOCKED BY EXTERNAL VISOR/UI DEPENDENCIES

## Loop 0 - Intake

- [x] Prompt extracted from CURRENT_BATCH.md | DOD: CLI regex read full XML tag. Rejected: IDE tab memory. Estimate: 1200 us.
- [x] Mandates selected | DOD: 8 relevant .agents-skills mandates read before coding. Rejected: generic AGENTS-only implementation. Estimate: 900 us.
- [x] Domain boundary read | DOD: Actual Domains file read; task maps to Equipment Runtime and S.O.A. Inventory. Rejected: cross-domain concrete references. Estimate: 400 us.

## Loop 1 - Tasks 1-5

- [x] 1. SINGLETON ERADICATION: Purge ItemDurabilityManager.Instance | DOD: rg found no `ItemDurabilityManager.Instance`, `DurabilityManager.Instance`, or script-side `ToolDurabilitySystem.Instance`; removed the remaining tool singleton accessor. Rejected: compatibility shim singleton. Estimate: 35 us saved per call chain.
- [x] 2. SIGNAL MIGRATION: Consume BiomeChangedSignal, emit ItemDurabilityChangedSignal | DOD: `PlayerInventory.SlowTick` drains `BiomeChangedSignal`; `GlobalSignals` has fixed 32-byte `ItemDurabilityChangedSignal`. Rejected: string event relay. Estimate: 12 us saved per signal batch.
- [x] 3. ASMDEF ISOLATION: Hecton8.Inventory.Corrosion -> Contracts | DOD: corrosion job lives in `Hecton8.Inventory.Corrosion`, pure bit helpers in `Hecton8.Inventory.Corrosion.Contracts`; Unity log shows both assemblies compiled and copied. Rejected: adding job to Hecton8.Core. Estimate: 0 runtime us, compile isolation gained.
- [x] 4. DEAD CODE HUNT: Eradicate Update loops inside Item.cs checking for damage | DOD: `rg -g "*Item*.cs" "void Update"` found no item damage polling loop. Rejected: editing non-existent `Item.cs`. Estimate: 0 us, no loop existed to remove.
- [x] 5. DURABILITY ARRAYS: NativeArray<float> ItemDurability mapped 1:1 with ItemHashes | DOD: `_itemDurability` allocated/disposed/registered beside `_itemHashes`; add/remove/sort/load paths keep anchor mapping. Rejected: per-item MonoBehaviour durability. Estimate: 18 us per 40-slot pass.

## Loop 2 - Tasks 6-10

- [x] 6. SALINITY LOOKUP: Data Monolith biome ID to SalinityFactor | DOD: biome hashes from `BiomeChangedSignal.CurrentBiomeHash` map to salinity 0..1 with brine hashes at 1.0 and fallback bands. Rejected: querying biome objects from inventory. Estimate: 4 us per biome event.
- [x] 7. BURST DEGRADATION JOB: FrostTick 5s durability decay | DOD: `ItemSalinityCorrosionJob` runs on 5 second accumulator and applies `durability -= salinity * rate`. Rejected: per-frame Update drain. Estimate: ~5 us amortized on 40 equipped slots.
- [x] 8. BITMASK FILTERING: Equipped-only bitwise AND with PlayerInventory.CurrentInventoryMask | DOD: job resolves hash bit and skips unless `(CurrentInventoryMask & bit) != 0`. Rejected: list of equipped object references. Estimate: 8 us saved, zero managed lookups.
- [x] 9. RUST SHADER: Global _HectonEquipmentRust01 scalar | DOD: `Shader.SetGlobalFloat(_HectonEquipmentRust01Id, 1 - AverageEquipmentDurability01)` updates a single global scalar. Rejected: material instance churn. Estimate: 20-80 us saved depending renderer count.
- [x] 10. MATERIAL SWAP: First-person shader rusty detail blend, no actual material swap | DOD: global scalar path supports shader-side detail blending without renderer material replacement. Rejected: swapping hand/tool materials. Estimate: 0 B GC, avoids material clone cost.

## Loop 3 - Tasks 11-15

- [x] 11. TOOL FAILURE: Durability 0 flips Active bit to 0 and emits ToolAcousticSignal(Break) | DOD: job sets `BrokenItemStateMask`; SOA mask builder excludes broken slots; break hashes emit `ToolAcousticSignal`. Rejected: destroying inventory items. Estimate: 10 us saved per break event.
- [x] 12. REPAIR TOOL COUPLING: Titanium acquired while using Repair Tool restores durability | DOD: `ItemAcquiredSignal(Titanium)` plus active `RepairTool` restores matching tool durability to 1.0 and clears rust/degraded/broken flags. Rejected: direct repair tool dependency from item. Estimate: 6 us per acquisition scan.
- [x] 13. AUP SHIFT SAFETY: Inventories are data blobs, no AUP math required | DOD: corrosion uses hashes, flags, quality, durability, frame count only. Rejected: world-position salinity sampling from inventory. Estimate: no AUP barrier cost.
- [x] 14. MATH LOD: N/A, Burst job evaluates instantly across tiers | DOD: one scalar salinity, one contiguous SOA pass, 5-second cadence; quality tiers affect only shader richness downstream. Rejected: simulation LOD tree. Estimate: <25 us low-end target.
- [x] 15. ZERO-GC: FrostTick job allocates 0 bytes | DOD: all job inputs/results are persistent NativeArrays; no LINQ/string/list allocation in FrostTick path. Rejected: managed collections for broken items. Estimate: 0 B GC per FrostTick.

## Loop 4 - Tasks 16-19

- [x] 16. BLACKBOX DUMP: Push AverageEquipmentDurability to telemetry | DOD: fixed 300-frame `NativeArray<SalinityCorrosionTelemetryEntry>` records average durability, rust scalar, salinity, biome, mask; NaN dumps to `Docs/AgentLogs/Dump_SALINITY_CORROSION_SYSTEM.bin`. Rejected: Debug.Log trace. Estimate: deterministic crash evidence, 32 bytes/frame.
- [x] 17. EVENT BUS: HUDNotificationSignal(Equipment Failing) below 20 percent | DOD: below 0.2 emits latched `HUDNotificationSignal`; resets at 0.25. Rejected: per-frame HUD spam. Estimate: 1 signal per threshold crossing.
- [x] 18. SAVE SYSTEM SYNC: RLE sbyte quantization append to SaveBinaryStorage | DOD: runtime `_itemDurability` quantizes by `durability * 100` before sbyte/byte storage; RLE appended after inventory timestamps in binary codec; load decodes to durability/quality/byte mirrors. Rejected: raw float array save. Estimate: 128 slots compress to 2-256 bytes.
- [x] 19. OMEGA COMPILE CHECK: Burst job skips Hash == 0 | DOD: job line 49 skips `hash == 0u || StackCounts[slot] == 0`; Unity log shows corrosion asmdefs compiled before unrelated blockers. Rejected: branchless subtraction over empty cells. Estimate: prevents invalid mask/decay writes.

## Loop 5 - Verification / Missed-Thing Audit

- [x] Duplicate corrosion block audit | DOD: `rg` found one live `ItemSalinityCorrosionJob` schedule and one durability publish path. Rejected: blind removal. Estimate: avoided conflicting second FrostTick.
- [x] RLE source audit | DOD: corrected encoder to quantize runtime `_itemDurability`, not quality as indirect mirror. Rejected: assuming quality and durability stay identical forever. Estimate: correctness, no runtime cost.
- [x] SOA bounds audit | DOD: mirror count now includes `_itemDurability.Length`. Rejected: trusting all NativeArrays are always same length. Estimate: prevents out-of-range fault.
- [x] Prompt re-read after task 3 | DOD: CLI re-extracted `SALINITY_CORROSION_SYSTEM` block; no neighboring prompts used. Rejected: chat-memory prompt. Estimate: 900 us.
- [x] Prompt re-read after task 6 | DOD: same CLI extraction used during recursive verification. Rejected: stale summary. Estimate: 900 us.
- [x] Prompt re-read after task 9 | DOD: same CLI extraction used during recursive verification. Rejected: unrelated CORE prompt. Estimate: 900 us.
- [x] Prompt re-read after task 12 | DOD: same CLI extraction used during recursive verification. Rejected: adjacent SAVE prompt. Estimate: 900 us.
- [x] Prompt re-read after task 15 | DOD: same CLI extraction used during recursive verification. Rejected: local memory. Estimate: 900 us.
- [x] Prompt re-read after task 18 | DOD: same CLI extraction used during recursive verification. Rejected: inferred task list. Estimate: 900 us.
- [x] Quantization audit multiply-by-100 | DOD: `QuantizeDurabilitySByte` uses `math.saturate(durability01) * 100f` before cast. Rejected: 0..1 direct cast. Estimate: prevents save precision loss.
- [x] Compile check | BLOCKED BY DEPENDENCY: Unity log shows `Hecton8.Inventory.Corrosion` and `.Contracts` compiled/copied, then build fails on unrelated `InternalFloodWaterlineRuntime`, `HectonVisorUberPostFeature`, and `VehicleSubOsCockpitRuntime` errors. MCP session unavailable after compile failure. No corrosion compile errors found in log. Estimate: external wall.
- [x] OMEGA_POLISH read only after all tasks done/blocked | DOD: extracted `<POLISH_MANDATE id="OMEGA_POLISH">` after core task completion; replaced the Burst job division with `math.rcp`, confirmed no `math.sqrt`, `math.normalize`, `foreach`, `string.Format`, or `.ToString()` inside corrosion asmdef, and Roslyn-compiled corrosion/contracts with Unity Bee response files. Estimate: removes one float divide from FrostTick average.
