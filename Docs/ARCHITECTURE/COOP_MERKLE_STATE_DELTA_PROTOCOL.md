# Co-Op Merkle State Delta Protocol

Date: 2026-05-17
Agent: NET_SYNC_MERKLE_ARCHITECT
Status: STATIC DESIGN VERIFIED / RUNTIME PENDING
Scope: Co-op state divergence detection, sparse state repair, binary packet layout, cache ownership, and deterministic hashing.

## Prompt Boundary

`Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="NET_SYNC_MERKLE_ARCHITECT">`. The recovered original assignment is in `Docs/Archive/Batch006/Tasks_Combined/Tasks_Batch006_COMBINED.txt:306-323`:

```text
<AGENT_PROMPT id="NET_SYNC_MERKLE_ARCHITECT" role="BACKEND_ENGINEER" chat_name="Co-Op Determinism Designer">
1. PACKET SCHEMA: Define the binary layout for InputState and WorldDelta packets.
2. MERKLE TREE HASHING: Provide the logic to hash GlobalDataVault chunks into a tree.
3. TICK RECONCILIATION: Define rollback logic and rewind depth.
4. COMPRESSION: Define bit-packing for 64-bit AUP coordinates.
5. NETWORK SIMULATOR: Write Tools/NetJitterSim.py. Simulate 200ms latency and 5% packet loss.
6. SELF-AUDIT LOOP 1: Verify MasterStateHash consistency; float math in hash is a crime.
7. DATA EXPORT: Generate Docs/Modding/Net_Protocol_v1.md.
STATUS: MUST BE "NETWORK PROTOCOL READY".
</AGENT_PROMPT>
```

Task count in active XML: `0`. Task count in archived XML: `7`.

## Mandates Followed

- `.agents-skills/NET_Logistics_Sync_BitPacking_Reconciliation.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Signal_Lane_Segregation.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `Docs/PROJECT_ATLAS.md`
- `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`
- `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md`
- `Docs/ARCHITECTURE/CORE_REPLAY_DETERMINISM.md`

## Existing Source Facts

- `Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` is a multiplayer placeholder. It is not a protocol owner.
- `Assets/_Project/Scripts/Core/GlobalSignals.cs` owns typed `SignalBus<T>` lanes and fixed unmanaged signal payload rules.
- `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` owns persistent native buffers through `IDataVault`.
- `Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs` already uses XXHash3-style 64-bit state hashing and save-domain separation.
- `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs` and `Assets/_Project/Scripts/Core/DodReplayRecorder.cs` are the closest deterministic validation references.
- `Docs/PROJECT_ATLAS.md` defines the 85-domain architecture and states that `Hecton8.Core` must not absorb new domain dependencies.
- Active binary payload scan outside generated/cache directories currently finds `44` `.bin` / `.h8bin` files and all are 16-byte aligned. This is verifier-owned, not a chat-only claim.

## Non-Goals

- No runtime C# implementation in this pass.
- No package choice. Mirror, Netcode, Steam sockets, or raw UDP are transport details, not state protocol authority.
- No economy recipe mutation. The protocol creates no recipes, loot tables, or resource loops; the 1,000,000-step Monte Carlo economy audit was run as a cross-domain guard.
- No physical LUT or matrix generation. The protocol hashes already-owned state and does not simulate optics, gases, or acoustics; optics and Sabine verifiers were run as cross-domain data-truth guards.
- No Transform-position authority. `Transform.position` is presentation only.

## Prime Contract

Co-op state sync is a deterministic repair protocol over authoritative, owner-provided state leaves.

The protocol never walks Unity object graphs, never serializes managed objects, never sends JSON, and never treats GameObjects as truth. State owners publish compact dirty facts through typed signal lanes or expose read-only DataVault snapshots. The network layer builds Merkle roots from those facts in `POST_SIMULATION`, sends only mismatched subtrees or leaf deltas, and applies validated repairs at the next simulation boundary.

Hot-path allocation target: `0 B` managed allocation per frame. Any implementation that allocates while collecting leaves, building hashes, packing packets, applying repairs, or writing telemetry is rejected.

Authoritative state classes:

| State class | Authority | Wire rule |
|---|---|---|
| Player input intent | Owning peer, host validated | input command stream, not transform truth |
| Player/body pose | Host or lockstep authority | full AUP hash plus quantized visual delta |
| Inventory/crafting facts | Host | bit-packed leaves, no item-name strings |
| Habitat/logistics state | Host | existing logistics dirty bitmasks, sector rooted |
| Voxel/resource deltas | Host | RLE delta payload, never full regenerated terrain |
| Quest/world flags | Host | mask leaves, monotonic tick gated |
| Presentation-only state | Local visual owner | excluded from gameplay root unless explicitly promoted |

## Merkle Topology

Tree unit: one sector-domain ledger.

Tree fanout: `16`.

Leaf capacity per sector-domain root: `4096` leaves. This matches the indexed save directory scale and gives three Merkle levels:

```text
Level 0: root
Level 1: 16 branch nodes
Level 2: 256 branch nodes
Level 3: 4096 leaves
```

Leaf slot:

```text
slot = low12(FNV1A64(LeafKey16))
```

FNV-1a is used only as a deterministic slotting and label hash. The full `LeafKey16` is always stored and compared. A slot collision does not corrupt state; it creates an ordered collision chain inside the same sector-domain page. The verifier checks zero FNV-1a collisions for fixed protocol labels and current 85-domain labels, but runtime leaf correctness never depends on FNV collision impossibility.

State content hashes use XXHash3-128 style domain-separated preimages:

```text
LeafHash128 = XXH3_128(
    "H8NET_LEAF_V1" ||
    LeafKey16 ||
    SourceTickLE ||
    QuantizationTier ||
    CanonicalPayloadBytes)

NodeHash128 = XXH3_128(
    "H8NET_NODE_V1" ||
    LevelLE ||
    ChildMaskLE ||
    ChildHash128[0..15])

RootHash128 = XXH3_128(
    "H8NET_ROOT_V1" ||
    ProtocolVersionLE ||
    TickLE ||
    SectorKeyLE ||
    DomainId ||
    ShiftFrameIDLE ||
    NodeHash128)
```

All integer fields are little-endian. Payload bytes are canonical binary layouts owned by each source domain. No locale, string casing, dictionary order, object instance id, or managed pointer can enter a preimage.

## AUP Authority

Every gameplay-authoritative spatial leaf uses full AUP authority:

```text
H8NetAup48
int64 SectorX
int64 SectorY
int64 SectorZ
int32 LocalMillimetersX
int32 LocalMillimetersY
int32 LocalMillimetersZ
uint32 ShiftFrameID
uint32 SourceSystemID
uint32 FiniteFlags
```

Size: `48` bytes.

The compact 16-byte network logistics AUP described in the logistics bit-packing mandate is allowed only as a visual delta or sub-sector optimization after the receiver already knows the base sector and current `ShiftFrameID`. It is not sufficient for gameplay authority.

Quantization grid:

| Tier | Commit grid | Max local error | Root validation cadence |
|---|---:|---:|---:|
| Low / MX350 | 10 mm | 20 mm | every 30 frames |
| Middle | 5 mm | 10 mm | every 15 frames |
| High | 2 mm | 5 mm | every 10 frames |
| Ultra | 1 mm | 2 mm | every frame |

Non-finite AUP input never enters a hash. Fallback order is last valid same-entity AUP for the same shift id, sector origin plus zero local offset, then quarantine with a telemetry error hash.

## Binary Layout

All packet records are 16-byte aligned. Payload sections are padded with zero bytes to the next 16-byte boundary. Runtime JSON is forbidden.

The `1200` byte datagram ceiling is derived from conservative transport headroom: IPv6 minimum MTU `1280` bytes minus IP/UDP/security/session overhead and a no-fragment safety margin. The protocol rejects IP fragmentation; large sector repairs use application fragments with explicit `FragmentIndex` / `FragmentCount`.

### H8NetMerkleFrameHeader64

Magic bytes: `H8NM`
Magic value written as little-endian uint: `0x4D4E3848`
Size: `64` bytes
Python struct format: `<IHHIIIIQQQHHHHHBBBBH`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint32 | Magic |
| 4 | uint16 | Version |
| 6 | uint16 | HeaderBytes |
| 8 | uint32 | SessionId |
| 12 | uint32 | AuthorityPeerId |
| 16 | uint32 | TickId |
| 20 | uint32 | AckTickId |
| 24 | uint64 | SectorKey |
| 32 | uint64 | RootHashLo |
| 40 | uint64 | RootHashHi |
| 48 | uint16 | NodeRecordCount |
| 50 | uint16 | LeafRecordCount |
| 52 | uint16 | PayloadByteCount |
| 54 | uint16 | HeaderFlags |
| 56 | uint16 | PacketSequence |
| 58 | uint8 | FragmentIndex |
| 59 | uint8 | FragmentCount |
| 60 | uint8 | PacketKind |
| 61 | uint8 | MathLod |
| 62 | uint16 | HeaderCrc16 |

`HeaderCrc16` is computed with this field zeroed. Algorithm: CRC-16/CCITT-FALSE; polynomial `0x1021`; initial value `0xFFFF`; xorout `0x0000`; refin=false; refout=false. The two bytes at offset `62..63` are zero during calculation, then written as a little-endian uint16. It catches header corruption before the packet enters the DataVault staging lane. Full state integrity still comes from Merkle hashes and payload hashes.

### H8NetMerkleNodeRecord32

Size: `32` bytes
Python struct format: `<HHIQQIHBB`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint16 | Level |
| 2 | uint16 | ChildMask |
| 4 | uint32 | NodeIndex |
| 8 | uint64 | HashLo |
| 16 | uint64 | HashHi |
| 24 | uint32 | FirstChildOrLeaf |
| 28 | uint16 | ChildOrLeafCount |
| 30 | uint8 | DomainId |
| 31 | uint8 | Flags |

### H8NetLeafDeltaRecord64

Size: `64` bytes
Python struct format: `<QIHBBQQQQIHHIBBH`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint64 | StableObjectId |
| 8 | uint32 | SectorHash32 |
| 12 | uint16 | StateKind |
| 14 | uint8 | DomainId |
| 15 | uint8 | AuthorityClass |
| 16 | uint64 | PrevHashLo |
| 24 | uint64 | PrevHashHi |
| 32 | uint64 | NewHashLo |
| 40 | uint64 | NewHashHi |
| 48 | uint32 | PayloadOffset |
| 52 | uint16 | PayloadBytes |
| 54 | uint16 | FieldMask |
| 56 | uint32 | SourceTick |
| 60 | uint8 | QuantizationTier |
| 61 | uint8 | Flags |
| 62 | uint16 | Reserved |

The receiver applies the payload only when `PrevHash128` matches its current local leaf hash or when the packet is marked as a full repair snapshot.

### H8NetRepairRequestRecord32

Size: `32` bytes
Python struct format: `<QQQIHBB`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint64 | SectorKey |
| 8 | uint64 | WantedHashLo |
| 16 | uint64 | WantedHashHi |
| 24 | uint32 | NodeIndex |
| 28 | uint16 | Level |
| 30 | uint8 | Reason |
| 31 | uint8 | Flags |

### H8NetTelemetryEntry64

Size: `64` bytes
Python struct format: `<IIQQQQQHHHHII`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint32 | Frame |
| 4 | uint32 | Tick |
| 8 | uint64 | SectorKey |
| 16 | uint64 | LocalRootLo |
| 24 | uint64 | LocalRootHi |
| 32 | uint64 | RemoteRootLo |
| 40 | uint64 | RemoteRootHi |
| 48 | uint16 | LeafCount |
| 50 | uint16 | RepairCount |
| 52 | uint16 | PacketCount |
| 54 | uint16 | Flags |
| 56 | uint32 | ErrorHash |
| 60 | uint32 | PeerId |

Network telemetry owns exactly `300` entries in a circular buffer. Crash or desync escalation dumps `Docs/AgentLogs/Dump_NET_SYNC_MERKLE_ARCHITECT.bin`.

### H8NetVisualOverkillRecord64

Visual-only Ultra payload. Excluded from gameplay Merkle roots.
Size: `64` bytes
Python struct format: `<QQIIIHHHBBIQQQ`

| Offset | Type | Field |
|---:|---|---|
| 0 | uint64 | SectorKey |
| 8 | uint64 | VisualRootHash |
| 16 | uint32 | GradientLutHash |
| 20 | uint32 | HarmonicNoiseSeed |
| 24 | uint32 | MaterialResponseMask |
| 28 | uint16 | WakeDetailQ8 |
| 30 | uint16 | CausticDetailQ8 |
| 32 | uint16 | SiltDetailQ8 |
| 34 | uint8 | VisualTier |
| 35 | uint8 | Flags |
| 36 | uint32 | ExtraControlMask |
| 40 | uint64 | ExtraHashLo |
| 48 | uint64 | ExtraHashHi |
| 56 | uint64 | Reserved |

The record carries RTX-overkill presentation data: high-resolution gradient references, deterministic harmonic noise seed, and extra visual detail scalars. It must never repair gameplay state and must be dropped first under backpressure.

## Packet Kinds

| Id | Name | Contents | Max per datagram |
|---:|---|---|---:|
| 1 | RootSeal | header only | 1 |
| 2 | NodeProbe | header + node records | 32 node records |
| 3 | LeafDelta | header + leaf records + payload | 8 leaf records + 512 payload bytes |
| 4 | RepairRequest | header + repair records | 32 repair records |
| 5 | FullSectorWindow | fragment series of leaf records + payload | app-fragmented |
| 6 | TelemetryEcho | header + telemetry records | 16 telemetry records |
| 7 | VisualOverkill | header + visual-only records | 16 visual records |

Target datagram ceiling: `1200` bytes. The protocol uses application fragmentation. IP fragmentation is rejected.

Packet fit examples:

```text
RootSeal:     64 bytes
NodeProbe:    64 + (32 * 32) = 1088 bytes
LeafDelta:    64 + (8 * 64) + 512 = 1088 bytes
RepairRequest:64 + (32 * 32) = 1088 bytes
VisualOverkill:64 + (16 * 64) = 1088 bytes
```

## Frame Phases

| Phase | Work |
|---|---|
| PRE_SIMULATION | Receive bytes into DataVault staging pages. Validate magic, version, endian, length, and header CRC. Do not mutate gameplay state. |
| SIMULATION | State owners run normal gameplay. Network layer only records received packet availability. |
| POST_SIMULATION | Build local dirty leaf hashes, compare remote root seals, emit probe/repair/delta packets, apply validated repairs in owner command queues. |
| VISUAL_SYNC | Presentation systems consume applied snapshots and interpolate. Visual-only high-tier extras never feed gameplay roots. |

No `GlobalRegistry.Get<T>()` calls are allowed inside these hot paths. The future runtime owner must cache `IDataVault`, transport interface, tick clock, telemetry sink, and signal readers during dependency injection.

## DataVault Ownership

The runtime implementation must request these buffers from `GlobalDataVault`. Numeric `BufferID` values are intentionally not assigned in this design because `BufferID` is a public Core contract and must be reserved by the Integrator.

| Buffer | Element | Capacity Low | Capacity High/Ultra | Purpose |
|---|---|---:|---:|---|
| `NetSyncLeafKeyFront` | `H8NetLeafKey16` | 4096 | 16384 | current authoritative key set |
| `NetSyncLeafHashFront` | `uint4` | 4096 | 16384 | current leaf hashes |
| `NetSyncLeafHashBack` | `uint4` | 4096 | 16384 | next-frame leaf hashes |
| `NetSyncNodeFront` | `H8NetMerkleNodeRecord32` | 273 | 1092 | current tree nodes |
| `NetSyncNodeBack` | `H8NetMerkleNodeRecord32` | 273 | 1092 | next tree nodes |
| `NetSyncPacketStaging` | `byte` | 65536 | 262144 | receive/send staging pages |
| `NetSyncTelemetryRing` | `H8NetTelemetryEntry64` | 300 | 300 | black box |

`273` nodes is `1 + 16 + 256` for one 4096-leaf sector-domain tree. High and Ultra keep more sector-domain roots resident, not larger per-root truth.

## Signal Lanes

Future runtime should add typed lanes only after Integrator approval:

| Signal | Size | Producer | Consumer | Overflow |
|---|---:|---|---|---|
| `NetSyncRootSealSignal` | 32 | network sync | telemetry, QA, optional UI | coalesce by sector |
| `NetSyncRepairRequestSignal` | 32 | network sync | transport owner | coalesce by sector/node |
| `NetSyncDeltaAppliedSignal` | 32 | network sync | save/replay/watchdog | coalesce by leaf key |
| `NetSyncDesyncSignal` | 64 | network sync | telemetry/replay/watchdog | fail-fast in dev, ring overwrite in release |

Do not create one monolithic network event. Do not create a single-use EventID for a private caller.

## Delta Loop

1. State owners publish dirty leaves or expose read-only snapshots.
2. Network sync gathers leaves by domain and sector into DataVault back buffers.
3. A Burst job builds leaf hashes and parent hashes. It writes back buffers only.
4. End-of-frame swap exposes the completed root.
5. Peers exchange `RootSeal`.
6. Matching roots stop. No leaf payload is sent.
7. Mismatched roots exchange `NodeProbe` for the lowest mismatched branch.
8. Branch mismatch narrows to leaf records.
9. Authority sends `LeafDelta` records and canonical payload bytes.
10. Receiver validates `PrevHash128`, `NewHash128`, tick age, authority class, AUP shift id, and payload bounds.
11. Valid repair enters owner command queues for next `POST_SIMULATION`.
12. Invalid repair writes telemetry and requests `FullSectorWindow`.

Three consecutive mismatches for the same peer/sector after full repair quarantine that peer-sector pair and emit `NetSyncDesyncSignal`.

## Hysteresis And Math LOD

Math LOD switches use a minimum 3-second hold and sector-distance bands:

| Tier | Root cadence | Max datagrams per frame | Leaf delta cap | Extra data |
|---|---:|---:|---:|---|
| Low / toaster | 2 Hz distant, 10 Hz near | 1 | 32 leaves/frame | gameplay truth only |
| Middle | 10 Hz distant, 20 Hz near | 2 | 64 leaves/frame | compact telemetry |
| High | 20 Hz | 4 | 128 leaves/frame | subtree prefetch and richer black-box fields |
| Ultra / RTX-overkill | 20 Hz plus every-frame validation around player | 8 | 256 leaves/frame | diagnostic root trail and visual-only detail payloads |

Visual overkill is not gameplay authority. Ultra may send additional shimmer, wake, UI, or replay diagnostic fields, but gameplay roots remain deterministic and comparable with Low.

## Tier Payload Contracts

Low/toaster:

- no `VisualOverkill` packets
- one sector-domain root per frame budget window
- leaf payloads use mandatory gameplay fields only
- compact telemetry counters only
- root validation may run at 2 Hz for distant sectors

Middle:

- near-sector roots at 20 Hz, distant roots at 10 Hz
- full repair requests permitted for current base/habitat sectors
- telemetry includes packet and repair counters

High:

- subtree prefetch allowed for player-near sector domains
- black-box retains richer root mismatch context
- optional visual detail is still local unless requested by a presentation consumer

Ultra / RTX-overkill:

- `H8NetVisualOverkillRecord64` can ride beside gameplay sync
- high-resolution gradient hashes and harmonic noise seeds are allowed
- records are `VISUAL_SYNC` only and are dropped before gameplay repair under pressure
- gameplay Merkle roots remain byte-identical with Low when canonical gameplay payloads match

Runtime JSON remains forbidden in every tier. Offline JSON may exist as authoring input only if a binary `.h8bin` / `.bin` is the runtime artifact.

## Backpressure

When staging fills:

1. Coalesce duplicate leaf keys and keep highest `SourceTick`.
2. Keep repair requests before optional telemetry echoes.
3. Keep gameplay domains before visual-only extras.
4. Emit telemetry with overflow counters.
5. Do not silently drop authoritative state. If a repair cannot fit after coalescing, request `FullSectorWindow` next frame.

## Failure Modes

| Failure | Response |
|---|---|
| Future tick | discard and emit tick-skew telemetry |
| Stale tick | discard after max age window |
| Wrong authority | reject and emit authority violation telemetry |
| Header length/align failure | reject before staging |
| Payload overrun | reject packet and request repair |
| Hash mismatch after repair | request full sector window |
| Rebase shift mismatch | hold packet until sync fence resolves or reject stale shift |
| Non-finite payload scalar | safe fallback, telemetry hash, leaf quarantine |
| Three failed full repairs | peer-sector quarantine and dump black box |

## Data Truth Audit

Math audit: no physical LUTs or matrices are created by this protocol. All protocol constants are structural and derived from binary/cache constraints: 16-byte alignment, 64-byte header, 32/64-byte records, 16-way tree fanout, 4096 leaves from `16^3`, and 1200-byte datagram ceiling. Cross-domain guards passed for `VerifyOpticsBaker.py` (`OPTICS_LUT_VERIFIED`, little-endian `<e`, 393216 bytes, FNV collisions `0`) and `VerifySabineBaker.py` (`SABINE_LUT_VERIFIED`, little-endian `<ff`/`<ffff`, 524288 bytes, tiers `high,middle,rtx_overkill,toaster_i3`, math audit `Sabine+Thorp+BeerLambert+HydrostaticPressure`).

Economy audit: no recipe, loot, inventory quantity, barter, or resource-generation data is created. Infinite resource loop risk is unchanged by this document. The protocol can sync economy leaves, but it does not author economy values. Current cross-domain guards passed: `CraftingEconomyMonteCarlo.py --steps 1000000` returned `profit_steps=0`, `max_value_delta_milli_units=-1000`, `max_mass_delta_mg=-400000`, `max_energy_delta_mwh=-133000`; `Tools/Economy/DataTruthInquisition.py --root .` returned `status=PASS`, `monte_carlo_steps=1539943`, `recipe_cycles=0`, `binary_unaligned=0`, `binary_endian_unknown=0`, `struct_format_failures=0`, and `fnv_collisions=0`.

Lore audit: wire names use industrial state terms: root seal, repair request, sector ledger, black box, staging page. Sterile transport names are absent from the protocol. Cross-domain guard passed: `VerifyLore.py --check` returned `CHECK OK`, `entries=2`, `alignment=16`, `endian=<`.

H-Phi/Data Sovereignty audit: this design adds no local native allocations and no Unity-object truth stores. The design-level protocol model is `7` DataVault buffer families, `4` future typed signal lanes, `0` direct concrete cross-domain references, `0` hot registry polls, and `0` runtime JSON paths. Source H-Phi counters are not changed until runtime code exists; the design increases the future Data Sovereignty target by making every steady-state buffer vault-owned. Current-disk data checks passed: `VerifyMetricPhiDataTruth.py` reported `checks=37 failed=0`, `binary_files=44 unaligned=0`, `struct_format_sites=274 endian_failures=0`; `VerifyDataInquisition.py` reported `binaries=44 aligned16=true`, `manifests=11`, `structFormats=273`, `monteCarloSteps=1000000`, `hashCollisions=0`, `atlasDomains=85`; canonical `RunMetricPhiVerifySweep.py` report records `VERIFY_SWEEP_PASS`, `totalCommands=35`, `requiredFailures=0`, and `selfCheckPending=false`. The H-Phi sidecar records `5015` eligible files, `1,723,788` lines, `DataSovereignty=0.019743027`, and `StrictLocalNativeArraySovereignty=0.089045936`; runtime sovereignty did not improve in this offline pass. `HectonPhiAudit.ps1 -Summary -Json -CoreGraphOnly` completed as `STATIC_SOURCE`; the full all-surface H-Phi run is not counted as proof.

Binary/cache audit: all packet structs are verifier-checked for little-endian `<` packing and 16-byte size alignment. The verifier also scans active `.bin` / `.h8bin` payloads outside ignored generated directories and fails on any file whose byte size is not divisible by `16`.

Network gate cache audit: `NetProtocolGate.py` initially failed on stale generated `.pyc` files. The generated bytecode was deleted under the verified `C:\Hecton8\Tools` tree, `NetProtocolGate.py` was rerun with `PYTHONDONTWRITEBYTECODE=1` and `python -B`, and it returned `NETWORK PROTOCOL READY` with `3` scenarios and `8` unit tests. NET-owned gate bytecode is self-cleaned by the gate. The later broad `Tools` cache readback reported `CACHE_FILES_LEFT=0` and `PYCACHE_DIRS_LEFT=0`; stable global cache-zero still requires unrelated Python writers to stay idle or run bytecode-disabled.

Jitter simulation audit: `Tools/NetJitterSim.py` ran a deterministic lockstep stress case and wrote `Docs/AgentLogs/NetJitterSim_NET_SYNC_MERKLE_ARCHITECT.json`. Scenario: `4` clients, `600` ticks, `20 ms` tick, `200 ms` latency, `80 ms` jitter, `8%` loss, `24` redundant input records, `96` tick rollback window. Result: `NETWORK PROTOCOL READY`, `sent_packets=7848`, `lost_packets=672`, `delivered_packets=7176`, `rollback.events=245`, `rollback.max_depth_ticks=3`, `master_state_hash_mismatches=0`, `input_ring_mismatches=0`, `missing_actual_inputs=0`, `float_hash_audit=PASS`. This is an offline Python proof only; Unity transport, GC, profiler, and player-build proof remain pending.

Latest static verifier output:

```text
NET_SYNC_MERKLE_PROTOCOL_VERIFY=PASS
STRUCT_COUNT=6
DOMAIN_LABELS=85
FNV_LABELS=107
BINARY_PAYLOADS_ALIGNED=44
DATAGRAM_CEILING=1200
HEADER_CRC16_SAMPLE=0x220C
JITTER_SIM_STATUS=NETWORK PROTOCOL READY
JITTER_SIM_LOST_PACKETS=672
JITTER_SIM_ROLLBACK_MAX_DEPTH=3
```

Latest repo-root `Verify*.py` sweep:

```text
VERIFY_FAILURES=0
ACTIVE_VERIFY_COMMANDS=35
```

## Verification

Static verifier:

```text
python Tools/Architecture/VerifyNetSyncMerkleProtocol.py
```

The verifier checks:

- every declared binary struct is 16-byte aligned
- every declared binary struct uses little-endian `<` format
- sample `struct.pack` calls round-trip the header magic in little-endian byte order
- sample `HeaderCrc16` uses CRC-16/CCITT-FALSE with offset `62..63` zeroed before the little-endian write
- packet fit examples stay below the 1200-byte datagram ceiling
- FNV-1a 64 hashes have zero collisions across protocol labels and current domain labels
- active `.bin` / `.h8bin` payloads outside generated directories are 16-byte aligned
- the latest jitter simulation report exists and reports `NETWORK PROTOCOL READY`
- `Docs/PROJECT_ATLAS.md` still exposes the 85-domain map
- the protocol's design-level H-Phi model keeps data in DataVault buffers and typed signal lanes
- this document contains the required AUP, DataVault, SignalBus, zero-GC, and runtime-pending boundaries

Runtime proof still requires Unity import, Console, Play Mode, GCMonitor, profiler, save/load interaction, Unity transport packet fuzzing, and player-build validation. Offline Python jitter simulation has passed and is now verifier-gated.

STATUS: STATIC DESIGN VERIFIED / RUNTIME PENDING
