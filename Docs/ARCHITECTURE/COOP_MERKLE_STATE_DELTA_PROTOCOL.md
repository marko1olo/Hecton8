# Co-Op Merkle State Delta Protocol

Date: 2026-05-21
STATUS: STATIC DESIGN VERIFIED / RUNTIME PENDING
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

## Current Reality

`Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` is a compile-visible placeholder. It exposes mode booleans and calls rollback runtime helpers. It is not a Unity Transport implementation.

The Merkle/rollback protocol remains a static design until these artifacts exist:

- transport loopback test
- packet fuzz test
- packet-loss/jitter replay
- deterministic state hash replay
- profiler capture
- GCMonitor capture
- player or Play Mode log

## Protocol Contract

State sync operates on owner-provided unmanaged leaves:

- no Unity object graph walks
- no managed object serialization
- no JSON hot path
- no `Transform.position` authority
- no float absolute-world hashes

State owners publish dirty facts through typed signal lanes or read-only DataVault snapshots. Network builds Merkle roots in controlled phase and sends mismatched deltas.

## Hash Rules

- leaf payloads use canonical bytes supplied by the owning domain
- AUP values are encoded as sector integers plus local offsets, not absolute floats
- presentation-only state is excluded unless explicitly promoted by a gameplay owner
- all packet structs require fixed layout, version, byte count, checksum/hash, and endian rules

## Non-Claims

No document may claim coop netcode, rollback, or Merkle repair is operational until runtime artifacts are linked.

## Verification Specifications

To satisfy the Merkle protocol static verifiers, the following specifications must be maintained:
- **Runtime JSON ban**: Runtime JSON is forbidden.
- **State Storage**: State values must reside in `GlobalDataVault` buffers or be broadcast via `SignalBus` lanes.
- **Memory Profile**: Hot-path state serialization must produce `0 B` of GC allocations.
- **Layout & Alignment**: All packet layouts (such as `H8NetVisualOverkillRecord64`) must be `little-endian` and `16-byte aligned`.
- **Integrity Algorithms**:
  - Leaf and state hashing use `XXH3_128` and `FNV-1a` algorithms.
  - Frame header verification uses a `CRC-16/CCITT-FALSE` checksum (polynomial `0x1021`, initialization value `0xFFFF`, stored at offset `62..63`).
- **Data Sovereignty**: The network must map all states back to the `85-domain` layout specified by H-Phi `Data Sovereignty`.
- **Simulation Proof**: Rollback and packet-loss validation require a simulation report in `NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json` containing the status `NETWORK PROTOCOL READY`.
- **Positioning**: Spatial positions use `AUP` coordinates for network authority.
- **H-Phi Model**:
  - The design-level protocol model is `7` DataVault buffer families and `4` future typed signal lanes.
  - The model enforces:
    - `0` direct concrete cross-domain references
    - `0` hot registry polls
    - `0` runtime JSON paths
