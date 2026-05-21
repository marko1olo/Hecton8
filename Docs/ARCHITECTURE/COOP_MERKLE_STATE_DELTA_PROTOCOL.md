# Co-Op Merkle State Delta Protocol

Date: 2026-05-21
Status: STATIC DESIGN / RUNTIME PENDING
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_SOURCE

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

State owners publish compact dirty facts through typed signal lanes or read-only DataVault snapshots. The network layer builds Merkle roots in a controlled phase and sends mismatched subtree or leaf deltas.

## Hash Rules

- leaf payloads use canonical bytes supplied by the owning domain
- AUP values are encoded as sector integers plus local offsets, not absolute floats
- presentation-only state is excluded unless explicitly promoted by a gameplay owner
- all packet structs require fixed layout, version, byte count, checksum/hash, and endian rules

## Non-Claims

No document may claim coop netcode, rollback, or Merkle repair is operational until runtime artifacts are linked.
