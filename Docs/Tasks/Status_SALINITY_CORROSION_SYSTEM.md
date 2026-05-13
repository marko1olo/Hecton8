# SALINITY_CORROSION_SYSTEM Status

PROMPT: SALINITY_CORROSION_SYSTEM
ROLE: SYSTEMS_ARCHITECT
DOMAIN: ECHELON 4 / Equipment Runtime / S.O.A. Inventory
STATUS: PENDING VERIFICATION

## Loop 0 - Intake

- [x] Prompt extracted from CURRENT_BATCH.md | DOD: CLI regex read full XML tag, not MCP snippet. Rejected: IDE tab memory. Estimate: 1200 us.
- [x] Mandates selected | DOD: 8 relevant .agents-skills mandates read before coding. Rejected: generic AGENTS-only implementation. Estimate: 900 us.
- [x] Domain boundary read | DOD: Actual Domains file read; task maps to Equipment Runtime and S.O.A. Inventory. Rejected: cross-domain concrete references. Estimate: 400 us.

## Core Tasks

- [ ] 1. SINGLETON ERADICATION: Purge ItemDurabilityManager.Instance | Justification pending.
- [ ] 2. SIGNAL MIGRATION: Consume BiomeChangedSignal, emit ItemDurabilityChangedSignal | Justification pending.
- [ ] 3. ASMDEF ISOLATION: Hecton8.Inventory.Corrosion -> Contracts | Justification pending.
- [ ] 4. DEAD CODE HUNT: Eradicate Update loops inside Item.cs checking for damage | Justification pending.
- [ ] 5. DURABILITY ARRAYS: NativeArray<float> ItemDurability mapped 1:1 with ItemHashes | Justification pending.
- [ ] 6. SALINITY LOOKUP: Data Monolith biome ID to SalinityFactor | Justification pending.
- [ ] 7. BURST DEGRADATION JOB: FrostTick 5s durability decay | Justification pending.
- [ ] 8. BITMASK FILTERING: Equipped-only bitwise AND with PlayerInventory.CurrentInventoryMask | Justification pending.
- [ ] 9. RUST SHADER: Global _HectonEquipmentRust01 scalar | Justification pending.
- [ ] 10. MATERIAL SWAP: First-person shader rusty detail blend, no actual material swap | Justification pending.
- [ ] 11. TOOL FAILURE: Durability 0 flips Active bit to 0 and emits ToolAcousticSignal(Break) | Justification pending.
- [ ] 12. REPAIR TOOL COUPLING: Titanium acquired while using Repair Tool restores durability | Justification pending.
- [ ] 13. AUP SHIFT SAFETY: Inventories are data blobs, no AUP math required | Justification pending.
- [ ] 14. MATH LOD: N/A, Burst job evaluates instantly across tiers | Justification pending.
- [ ] 15. ZERO-GC: FrostTick job allocates 0 bytes | Justification pending.
- [ ] 16. BLACKBOX DUMP: Push AverageEquipmentDurability to telemetry | Justification pending.
- [ ] 17. EVENT BUS: HUDNotificationSignal(Equipment Failing) below 20 percent | Justification pending.
- [ ] 18. SAVE SYSTEM SYNC: RLE sbyte quantization append to SaveBinaryStorage | Justification pending.
- [ ] 19. OMEGA COMPILE CHECK: Burst job skips Hash == 0 | Justification pending.

## Verification

- [ ] Compile check | Pending.
- [ ] Prompt re-read after task 3 | Pending.
- [ ] Prompt re-read after task 6 | Pending.
- [ ] Prompt re-read after task 9 | Pending.
- [ ] Prompt re-read after task 12 | Pending.
- [ ] Prompt re-read after task 15 | Pending.
- [ ] Prompt re-read after task 18 | Pending.
- [ ] Quantization audit multiply-by-100 | Pending.
- [ ] OMEGA_POLISH read only after all tasks done/blocked | Pending.
