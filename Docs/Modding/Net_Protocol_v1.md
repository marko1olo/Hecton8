# HECTON-8 Network Protocol v1

Date: 2026-05-17
Owner: NET_SYNC_MERKLE_ARCHITECT / BACKEND_ENGINEER
Status: NETWORK PROTOCOL READY - OFFLINE SIM VERIFIED; UNITY RUNTIME PENDING
Evidence class: STATIC_DOC / OFFLINE_SIM / STATIC_SOURCE

This document defines the lockstep, Merkle-diff, rollback, and AUP packet contract for future co-op runtime work. It is not Unity Console, Play Mode, profiler, GCMonitor, player-build, or scene-wiring proof.

## Authority

Read order used:

- `AGENTS.md`
- `.agents-skills/NET_Logistics_Sync_BitPacking_Reconciliation.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`

## Non-Negotiable Invariants

- Wire endian: little-endian.
- Transport target: UDP-like unreliable datagrams. Reliability is explicit through sequence, ack mask, redundancy, and resync packets.
- Max network payload: `1200` bytes before transport headers. Anything larger fragments through protocol records.
- Tick cadence: `50 Hz`, `20 ms`, aligned with project fixed timestep. Do not mutate Unity fixed timestep to satisfy networking.
- Tick type: `uint32`. Compare with modular arithmetic only.
- World position authority: AUP only. No `Vector3`, no `Transform.position`, no float world coordinates over wire.
- Hash input authority: integers, fixed bytes, quantized AUP millimeters, flags, masks, and stable DataVault bytes only.
- Any float math feeding `MasterStateHash` is a protocol violation named `FLOAT_HASH_CRIME`.
- Cross-domain notifications use typed signal lanes or GlobalRegistry interfaces. No string RPC.
- Runtime buffers are owned by GlobalDataVault or a named networking vault owner. No persistent NativeArray allocation inside feature logic.

## Packet Envelope

Every packet starts with `H8NetEnvelope`:

| Offset | Size | Field | Type | Rule |
|---:|---:|---|---|---|
| 0 | 4 | Magic | `uint32` | `0x314E3848` (`H8N1`) |
| 4 | 1 | ProtocolVersion | `uint8` | `1` |
| 5 | 1 | PacketType | `uint8` | `1=InputState`, `2=MerkleProbe`, `3=WorldDelta`, `4=FullSnapshot`, `5=AckOnly` |
| 6 | 1 | HeaderBytes | `uint8` | Envelope size, currently `24` |
| 7 | 1 | Flags | `uint8` | bit0 compressed, bit1 fragment, bit2 resync, bit3 dev-telemetry |
| 8 | 2 | SenderPeerId | `uint16` | Stable peer slot, not display name |
| 10 | 2 | Sequence | `uint16` | Sender-local packet sequence |
| 12 | 2 | AckSequence | `uint16` | Highest received peer sequence |
| 14 | 1 | RecordCount | `uint8` | Payload record count |
| 15 | 1 | Reserved | `uint8` | Must be zero |
| 16 | 4 | AuthorityTick | `uint32` | Tick associated with payload |
| 20 | 4 | AckMask32 | `uint32` | Bit `n` acknowledges `AckSequence - n` |

Reject packet if magic/version/header size is wrong, reserved byte is nonzero, record count overflows payload, or packet type is unknown.

## Merkle Frame Header Addendum

The modding envelope above is the gameplay packet wrapper. Sector repair and Merkle probing use the stricter architecture header in `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`:

- `H8NetMerkleFrameHeader64` is exactly `64` bytes.
- Python struct format is little-endian: `<IHHIIIIQQQHHHHHBBBBH`.
- `HeaderCrc16` uses CRC-16/CCITT-FALSE: polynomial `0x1021`, initial value `0xFFFF`, xorout `0x0000`, refin=false, refout=false.
- CRC bytes at offset `62..63` are zero during calculation, then written with `struct.pack_into("<H", ...)`.
- Current deterministic verifier vector: `HEADER_CRC16_SAMPLE=0x220C`.

If any runtime or tool writes this header without the exact CRC variant and offset rule, reject the packet before DataVault staging.

## InputState Packet

`InputStatePacket = H8NetEnvelope + InputStateRecord[RecordCount]`.

Records are redundant: every outbound input packet carries current input plus the previous `15` local inputs by default. That makes isolated packet loss recoverable without waiting for a bespoke resend.

`InputStateRecord` is `16` bytes:

| Offset | Size | Field | Type | Rule |
|---:|---:|---|---|---|
| 0 | 4 | InputTick | `uint32` | Tick the input belongs to |
| 4 | 1 | PlayerId | `uint8` | Peer/player slot |
| 5 | 2 | ButtonMask | `uint16` | Bit-packed commands, no strings |
| 7 | 1 | InputFlags | `uint8` | bit0 predicted, bit1 replayed, bit2 local-only |
| 8 | 1 | MoveX | `int8` | Quantized `[-127,127]`; deadzone already applied |
| 9 | 1 | MoveY | `int8` | Quantized `[-127,127]`; deadzone already applied |
| 10 | 2 | AimYawQ12 | `uint16` | `0..4095`; 12 active bits, top 4 bits zero |
| 12 | 2 | ToolState | `uint16` | Tool mode, trigger phase, charge bucket |
| 14 | 2 | LocalInputSeq | `uint16` | Peer-local input sequence for duplicate rejection |

Hot-path receive rule:

```text
slot = InputTick & 255
if ring[slot].Tick != InputTick:
    ring[slot].Reset(InputTick)
ring[slot].Inputs[PlayerId] = record
ring[slot].ReceivedMask |= 1u << PlayerId
```

The slot is invalid until `ReceivedMask == ExpectedPeerMask` or the prediction window explicitly marks missing peers in `PredictedMask`.

## WorldDelta Packet

`WorldDeltaPacket = H8NetEnvelope + WorldDeltaHeader + WorldDeltaRecord[RecordCount] + raw payload slices`.

World deltas are not per-frame gameplay spam. They are resync payloads requested after Merkle mismatch, rollback window breach, or join-in-progress.

`WorldDeltaHeader` is `32` bytes:

| Offset | Size | Field | Type | Rule |
|---:|---:|---|---|---|
| 0 | 4 | DeltaId | `uint32` | Monotonic authority delta id |
| 4 | 4 | BaseTick | `uint32` | Tick of base state |
| 8 | 8 | BaseMasterHash | `uint64` | Root before applying delta |
| 16 | 8 | TargetMasterHash | `uint64` | Root after applying delta |
| 24 | 4 | FirstLeafIndex | `uint32` | First Merkle leaf covered |
| 28 | 2 | PayloadBytes | `uint16` | Raw bytes after records |
| 30 | 1 | FragmentIndex | `uint8` | `0..FragmentTotal-1` |
| 31 | 1 | FragmentTotal | `uint8` | `1..255` |

`WorldDeltaRecord` is `32` bytes:

| Offset | Size | Field | Type | Rule |
|---:|---:|---|---|---|
| 0 | 4 | BufferId | `uint32` | GlobalDataVault buffer id |
| 4 | 4 | PageIndex | `uint32` | Stable page within buffer |
| 8 | 4 | Generation | `uint32` | DataVault generation; stale generation rejects |
| 12 | 2 | ByteOffset | `uint16` | Offset in page payload |
| 14 | 2 | ByteCount | `uint16` | Bytes copied from raw payload section |
| 16 | 8 | LeafHashBefore | `uint64` | Guard against applying to wrong page |
| 24 | 8 | LeafHashAfter | `uint64` | Expected leaf after apply |

Apply rule:

1. Reject stale generation.
2. Verify `LeafHashBefore`.
3. Copy bytes into back buffer only.
4. Recompute leaf hash.
5. Swap at tick boundary only if `LeafHashAfter` matches.
6. Recompute Merkle root and compare `TargetMasterHash`.

No partial merge of a desynced ring. Full reset or verified delta only.

## AUP64 Packing

The default packet does not send full `int64x3` for every position. It sends anchor-relative millimeter locals.

`AupLocal64`:

| Bits | Field | Rule |
|---:|---|---|
| 0..20 | LocalXmmS21 | signed 21-bit millimeters relative to active AUP anchor |
| 21..41 | LocalYmmS21 | signed 21-bit millimeters relative to active AUP anchor |
| 42..62 | LocalZmmS21 | signed 21-bit millimeters relative to active AUP anchor |
| 63 | Overflow | `1` means receiver must read or request a full anchor record |

Signed 21-bit range is `-1,048,576..1,048,575 mm`, enough for a 512 m sector plus adjacent safety margin. If any axis overflows, do not clamp; set overflow and send `AupAnchorRecord` in a `WorldDelta` or resync packet.

`AupAnchorRecord` is cold/resync data:

| Field | Type | Rule |
|---|---|---|
| AnchorId | `uint32` | Stable anchor for packet sequence |
| ShiftFrameId | `uint32` | Reject stale shift id |
| GridX/GridY/GridZ | `int64 x3` | Absolute sector/grid |
| LocalOriginX/Y/Zmm | `int32 x3` | Anchor local origin, millimeter quantized |

Never reconstruct AUP truth from runtime presentation position.

## Merkle Tree Hashing

`MasterStateHash` is the root hash for a tick. It is derived from sorted Merkle leaves, not from scene objects.

Leaf owners:

- `InputLeaf`: packed `InputStateRecord` values for the tick.
- `VaultPageLeaf`: GlobalDataVault page projection bytes.
- `AupFenceLeaf`: active `ShiftFrameId`, quantized AUP commit fields, and sync-fence hash.
- `SignalLaneLeaf`: bounded typed signal lane packet bytes that affect gameplay truth.

Leaf key order:

```text
(LeafFamily:uint8, BufferId:uint32, PageIndex:uint32, EntityOrLaneId:uint32)
```

Hash source contract:

```text
LeafHash64 = H8Hash64(ProtocolSalt, Tick, LeafKey, Generation, LogicalCount, Stride, Bytes)
Parent64   = H8Hash64(TreeSalt, Level, LeftHash64, RightHash64)
Root64     = final parent hash
```

The current offline simulator uses FNV64 because Python stdlib does not ship XXHash3. Runtime implementation may use the existing project hash owner, but it must preserve these invariants:

- Same input bytes produce same hash on every platform.
- No float arithmetic in hash projection.
- No managed strings in hash projection.
- All float-originated gameplay fields must be quantized before hash input.
- Raw presentation matrices, cameras, transforms, material state, and UI are excluded.

Difference detection:

1. Compare `Root64`.
2. If root differs, compare child hashes level by level.
3. Request only mismatched leaf page ranges.
4. Apply `WorldDelta` into a back buffer.
5. Swap only after root equals authority root for the target tick.

Worst case for `4096` leaves is `12` binary levels. Normal mismatch request is one path plus sibling hashes, not a full world broadcast.

## Tick Reconciliation

Default values:

| Setting | Value | Reason |
|---|---:|---|
| Tick rate | 50 Hz | Project fixed timestep |
| Input delay | 16 ticks / 320 ms | Covers 200 ms latency, 40 ms jitter, loss redundancy, and scheduling margin |
| InputRingBuffer | 256 ticks / 5.12 s | Power-of-two slot mask |
| StateSnapshotRing | 128 ticks / 2.56 s | Covers rollback plus telemetry cushion |
| Max rollback | 64 ticks / 1.28 s | Enough for the tested jitter/loss envelope; beyond this is resync |
| Redundant input records | 16 | Recovers isolated packet loss without bespoke resend |

Receive flow:

```text
drain packet queue within budget
store actual inputs by tick/player
if actual input corrects a predicted slot already simulated:
    depth = latestSimTick - correctedTick
    if depth <= 64:
        restore snapshot(correctedTick - 1)
        replay correctedTick..latestSimTick
    else:
        request Merkle diff / full resync
```

Authority simulation is never smoothed. Only presentation proxies interpolate. On high hardware tiers, saved network/system cost buys richer remote presentation interpolation, debug overlays, and replay analysis, not looser authority.

## Black Box Telemetry

Future runtime owner must keep a fixed `NativeArray<NetTelemetryEntry>[300]` circular buffer.

`NetTelemetryEntry` target size: `64` bytes:

| Field | Type |
|---|---|
| Tick | `uint32` |
| LocalMasterHashLow | `uint64` |
| AuthorityMasterHashLow | `uint64` |
| InputReceivedMask | `uint32` |
| InputPredictedMask | `uint32` |
| RollbackDepth | `uint16` |
| PacketLossBps | `uint16` |
| PingMs | `uint16` |
| JitterMs | `uint16` |
| MerkleMismatchLeaf | `uint32` |
| ShiftFrameId | `uint32` |
| Flags | `uint32` |
| Reserved | pad to 64 |

Dump path on desync, non-finite source detection, or rollback-window breach:

```text
Docs/AgentLogs/Dump_NET_SYNC_MERKLE_ARCHITECT.bin
```

## Scalability

Low / MX350:

- Input delay `16`, rollback `64`, Merkle probe at 10 Hz unless desync suspected.
- Visual smoothing only in `VISUAL_SYNC`; authority stays tick-locked.
- Telemetry ring stores compact 64-byte entries only.

Middle:

- Merkle probe at 20 Hz for active gameplay leaves.
- Keep sibling hashes for faster diff requests.

High:

- Merkle probe every fixed tick.
- Keep richer dev telemetry and remote interpolation history.

Ultra:

- Full 300-frame network black-box payload plus replay sidecar.
- Visual overkill consumers may render remote prediction confidence, packet trails, and sync diagnostics. Gameplay cost must not increase without profiler proof.

## Offline Simulator Evidence

Simulator:

```text
Tools/NetJitterSim.py
```

Baseline command:

```powershell
python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 2 --input-delay-ticks 16 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_Report.json
```

Baseline result:

| Metric | Result |
|---|---:|
| Status | NETWORK PROTOCOL READY |
| Sent packets | 1296 |
| Lost packets | 78 |
| Delivered packets | 1218 |
| Payload estimate | 26903 B/s |
| Rollback events | 0 |
| MasterStateHash mismatches | 0 |
| InputRingBuffer mismatches | 0 |
| Missing actual inputs | 0 |
| Float hash audit | PASS |
| Last MasterStateHash | `0x074212968C22BD20` |

Rollback stress command:

```powershell
python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 2 --input-delay-ticks 8 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_RollbackStress_Report.json
```

Rollback stress result:

| Metric | Result |
|---|---:|
| Status | NETWORK PROTOCOL READY |
| Rollback events | 1190 |
| Max rollback depth | 4 ticks |
| Too-old corrections | 0 |
| MasterStateHash mismatches | 0 |
| InputRingBuffer mismatches | 0 |
| Missing actual inputs | 0 |
| Float hash audit | PASS |
| Last MasterStateHash | `0x074212968C22BD20` |

Four-client sanity command:

```powershell
python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 40 --loss-percent 5 --ticks 600 --clients 4 --input-delay-ticks 16 --rollback-ticks 64 --redundancy 16 --report Docs\Reports\NetJitterSim_4Client_Report.json
```

Four-client sanity result:

| Metric | Result |
|---|---:|
| Status | NETWORK PROTOCOL READY |
| Sent packets | 7776 |
| Lost packets | 390 |
| Payload estimate | 161422 B/s |
| MasterStateHash mismatches | 0 |
| InputRingBuffer mismatches | 0 |
| Missing actual inputs | 0 |
| Float hash audit | PASS |
| Last MasterStateHash | `0x3128242EF58ACE91` |

Regression test command:

```powershell
python -m unittest Tools.test_net_jitter_sim
```

Full offline gate command:

```powershell
python -B Tools\NetProtocolGate.py
```

Gate output:

- Report: `Docs/Reports/Net_Protocol_Gate_Report.md`
- Scenarios: baseline, rollback stress, four-client sanity
- Unit tests: 8
- Status: `NETWORK PROTOCOL READY`

Regression test coverage:

| Test | Contract |
|---|---|
| `test_baseline_latency_loss_converges` | 200 ms / 5% loss baseline reaches identical `MasterStateHash` and `InputRingBuffer` sequences |
| `test_rollback_stress_corrects_predicted_inputs` | low input delay forces rollback and still converges |
| `test_four_client_fanout_converges` | packet fan-out and peer masks converge with four clients |
| `test_redundant_packet_records_clamp_to_available_ticks` | redundant input bundles do not underflow/overflow tick records |
| `test_packet_schema_offsets_sizes_and_mtu_budget_are_locked` | executable packet schema locks offsets, sizes, and MTU budget for envelope/input/world-delta records |
| `test_aup64_round_trips_boundaries_and_flags_overflow` | executable `AupLocal64` signed 21-bit millimeter pack/unpack validates min/max and overflow path |
| `test_merkle_diff_indices_localize_changed_leaves` | executable Merkle comparison localizes changed leaves, including odd leaf counts |
| `test_float_hash_crime_detector_rejects_float_math` | hash self-audit flags float constants and division in hash functions |

## Current Reset Evidence - 2026-05-17

The original XML directive required 200 ms latency and 5% packet loss. That gate still runs through `Tools/NetProtocolGate.py`. The current reset pass also ran a harsher stress report: 200 ms latency, 80 ms jitter, and 8% loss.

```powershell
python Tools\NetJitterSim.py --latency-ms 200 --jitter-ms 80 --loss-percent 8 --ticks 600 --clients 4 --input-delay-ticks 12 --rollback-ticks 96 --redundancy 24 --seed 1313817649 --report Docs\AgentLogs\NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json
```

Current stress result:

| Metric | Result |
|---|---:|
| Status | NETWORK PROTOCOL READY |
| Sent packets | 7848 |
| Lost packets | 672 |
| Delivered packets | 7176 |
| Payload estimate | 235948 B/s |
| Rollback events | 245 |
| Max rollback depth | 3 ticks |
| MasterStateHash mismatches | 0 |
| InputRingBuffer mismatches | 0 |
| Missing actual inputs | 0 |
| Float hash audit | PASS |
| Last MasterStateHash | `0x3128242EF58ACE91` |

Current data-truth locks:

- `VerifyNetSyncMerkleProtocol.py`: `STRUCT_COUNT=6`, `DOMAIN_LABELS=85`, `FNV_LABELS=107`, `BINARY_PAYLOADS_ALIGNED=44`, `DATAGRAM_CEILING=1200`, `HEADER_CRC16_SAMPLE=0x220C`, `JITTER_SIM_STATUS=NETWORK PROTOCOL READY`.
- `VerifyMetricPhiDataTruth.py`: `checks=37`, `failed=0`, `binary_files=44`, `unaligned=0`, `struct_format_sites=274`, `endian_failures=0`.
- `VerifyH8HashCollisions.py`: 1,018 records, 0 FNV collisions.
- `CraftingEconomyMonteCarlo.py --steps 1000000`: `profit_steps=0`; value, mass, and energy deltas are negative.
- `NetProtocolGate.py`: `NETWORK PROTOCOL READY`, 3 scenarios, 8 unit tests.
- Python cache hygiene: NET-owned cache entries were absent during final scan, and the final broad `Tools` cache readback reported `CACHE_FILES_LEFT=0`, `PYCACHE_DIRS_LEFT=0`. Stable global cache-zero is blocked while unrelated Python agents continue writing `Tools/__pycache__`.

## Regression Model

CPU: current change adds no runtime code. Future runtime target is bounded queue drain plus Merkle leaf compare under `0.1 ms` per networking system tick until profiler proof says otherwise.

GC: current change adds no runtime code. Future runtime path must be fixed NativeArrays/rings only, `0 B/frame`.

Memory: current change adds offline JSON reports and docs only. Future runtime buffers are fixed rings: input ring, snapshot ring, Merkle leaf arrays, and 300-entry telemetry.

Cadence: authority cadence is 50 Hz fixed tick. Visual interpolation can run in `VISUAL_SYNC` but never mutates authority.

Correctness: simulator proves deterministic replay under the tested 200 ms / 5% loss envelope and a rollback-stress envelope. Unity runtime integration remains pending.

## Failure Modes

- More than 64 ticks of correction depth: request Merkle delta/full snapshot.
- AUP overflow bit set without anchor record: reject packet and request resync.
- Stale `ShiftFrameId`: reject position data.
- Float hash audit crime: stop authority acceptance, emit telemetry, dump black box.
- Merkle leaf hash mismatch after applying delta: discard back buffer and request full snapshot.
- Input ring slot tick mismatch: reset slot, clear masks, never merge stale input.

STATUS: NETWORK PROTOCOL READY - OFFLINE SIM VERIFIED; UNITY RUNTIME PENDING
