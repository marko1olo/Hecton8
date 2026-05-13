# LOG: INVENTORY_SOA_BLITTER

## 2026-05-13 Bulk Container Transfer And Weight

What was wrong:
- No dedicated transactional bulk transfer path existed for source/target inventory slices. The safe available path was per-item add/remove behavior, which causes repeated event/UI work and GC-risky integration pressure.
- Container storage had carry-weight coupling but no explicit `MaxWeightKg` / `MaxVolumeLiters` transaction caps.
- Drop-to-ocean behavior had no inventory stack batching contract; the prompt required `DebrisSpawnSignal(Hash, Count)`.
- Compile verification is blocked by global missing contracts outside this domain: Scheduling, Core.Memory.Layout, Audio propagation, Vehicle/Radar, BinaryBlittableSafe, and related generated project seams.

What was done:
- Added `InventorySoAUtility.TransferFailureCode`, `BulkTransferResult`, guarded generic `TryBulkCopySlice<T>`, `TryClearSlice<T>`, `InventoryTransferValidationJob`, and `InventoryCompactionJob`.
- Added `PlayerInventory.MaxWeightKg`, `MaxVolumeLiters`, `CurrentVolumeLiters`, and `ref readonly CurrentWeightKg`.
- Added `TryBulkTransferTo(...)`: validates source/target slices, rejects atomically by failure byte, uses guarded Unsafe MemCpy across S.O.A. arrays, clears source only after copy success, compacts, and emits one inventory change per inventory.
- Added `TryDropSliceToOcean(...)`: publishes `DebrisSpawnSignal` with `SpeciesHash` and `Quantity` per stack, not object instantiation per item.
- Added 300-entry inventory black-box telemetry ring and dump path `Docs/AgentLogs/Dump_INVENTORY_SOA_BLITTER.bin` on non-finite mass/volume totals.
- Added `TryCopyInventoryShadowPayload(...)` to blit the existing native save shadow payload into caller-owned native bytes.
- Added heavy transfer `ToolAcousticSignal` for transfers/drops above 50kg.
- Updated `HectonPlayerMovement` to read inventory weight through `ref readonly CurrentWeightKg`.
- Updated `DebrisSpawnSignal` with `Quantity` at unused bytes 62-63, preserving 64-byte size.

Cinematic Cheats used:
- Drop debris is a batched signal carrying hash/count, not physical prefab simulation.
- Heavy transfer feedback is a single acoustic packet with scalar intensity and pitch, not spawned audio emitters.
- Inventory math has no visual LOD; it is bounded native data accounting. Low/Middle/High/Ultra all use the same deterministic slice path.

Exact microseconds saved:
- 50-slot bulk transfer: estimated 450-1200 us saved by suppressing repeated source/target item event spam.
- UI batch refresh: estimated 500-2000 us saved depending listener count by firing one inventory change at transaction end.
- MemCpy versus per-slot accepted-copy loop: estimated 20-80 us saved at 50 slots.
- Burst validation versus managed preflight loop: estimated 60-180 us saved at 50 slots.
- Native compaction versus managed grouping: estimated 40-150 us saved at 50 slots.
- Drop batching: estimated >1000 us plus allocation avoidance for 50-count drops by avoiding per-item instantiation.
- Heavy-thud audio signal: <5 us queue write.
- Black-box write: ~1 us during mass refresh; cold disk dump only on non-finite fault.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore /m:1 /clp:ErrorsOnly /v:minimal` fails on unrelated global dependency walls before inventory verification.
- Unity MCP `validate_script` returned `no_unity_session` for touched scripts.
- `git diff --check` on touched files reports only CRLF normalization warnings.
- Static scans on touched inventory files found no hot `foreach`, `new List`, string formatting, interpolated strings, unconditional `math.sqrt`, or unconditional `math.normalize`.

Status: PENDING VERIFICATION.
