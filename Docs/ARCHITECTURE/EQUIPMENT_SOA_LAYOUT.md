# Equipment SOA Layout

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

## Scope

This contract covers hand tools and receptive equipment: scanner, laser cutter, builder, repair tool, seaglide-linked tools, and auxiliary deployables.

## Runtime Rule

Tools are data. Per-tool `MonoBehaviour.Update()` loops are rejected for gameplay truth.

Required route:

1. Tool identity and input are converted into unmanaged DTOs.
2. DTOs live in `GlobalDataVault` buffers.
3. A centralized equipment manager runs the Burst-compatible integration path.
4. Events leave through typed `SignalBus<T>` payloads.
5. Presentation components read published snapshots only.

## Active DTO Boundary

`ActiveEquipmentDTO` remains the active rollback/UI snapshot ABI. Source notes state explicit size `32` bytes.

Current active-equipment `BufferID` range:

| BufferID | Purpose |
|---:|---|
| 71300 | active equipment state writer |
| 71301 | published readback state |
| 71302 | AUP samples |
| 71303 | grid load requests |
| 71304 | telemetry ring |
| 71305 | telemetry cursor |
| 71306 | integration counters |
| 71307 | CSV scratch |
| 71308 | tuning |
| 71309 | hardware specs |
| 71310 | dump scratch |
| 71311-71315 | tool state/stat/type/status/environment mirrors |
| 71316 | wear drain rates |

Auxiliary equipment uses `71480..71491` plus `71494` for propwash telemetry.

## Signals

Equipment integrations must use typed signal lanes for overheat, depleted, haptics, VFX, audio, and interaction feedback. Private hot `NativeQueue` fields inside tool objects are rejected.

## Verification Boundary

This file is not proof that prefabs, haptics, UI, save/load, or scene wiring are implemented. Those claims need runtime artifacts.
