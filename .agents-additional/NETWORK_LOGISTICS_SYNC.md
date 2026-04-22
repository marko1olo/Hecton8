# NETWORK_LOGISTICS_SYNC.md
# HECTON-8 — Logistics Network: State Compression / Bit-Pack / Reconciliation
# Authority: Principal Architecture Layer — Rev 0.9.1

---

## §1. CORE DATA CONTRACTS

### DeltaPacket (Wire Format — 8 bytes total)
```
struct DeltaPacket {
    uint  NodeID;        // 20 bits used: 0..1_048_575 addressable nodes
    uint  StateBitmask;  // 12 bits used: flags per resource channel (see §2)
    half  Value;         // IEEE 754 fp16 — normalized [0..1] resource ratio
}
```

### AbsoluteUniversePosition (Networked Transform — 12 bytes)
```
struct AUP {
    int32 SectorX;   // grid sector [-32768..32767] @ 512m resolution
    int32 SectorY;
    uint16 LocalX;   // sub-sector offset [0..65535] → maps to [0..512m]
    uint16 LocalY;
    uint16 LocalZ;
    uint16 _pad;
}
```
FORBID: float-based world-position over wire. AUP only.

### SnapshotRingBuffer (Per-Node — Interpolation Source)
```
struct NodeSnapshot {
    uint32 TickID;
    half   OxygenRatio;
    half   PowerRatio;
    half   ThermalRatio;
    half   PressureRatio;
    uint8  StatusFlags;
    uint8  _pad[3];
}
// Allocate ring: NodeSnapshot[3] per node → 3 snapshots retained
// Index: slot = TickID % 3
```

---

## §2. BITMASK CHANNEL MAP (StateBitmask — 12 active bits)

```
Bit  0  → Oxygen        changed
Bit  1  → Power         changed
Bit  2  → Thermal       changed
Bit  3  → Pressure      changed
Bit  4  → Fuel          changed
Bit  5  → CoolantFlow   changed
Bit  6  → DataLink      changed
Bit  7  → StructuralInt changed
Bit  8  → AlarmState    active
Bit  9  → ModuleOnline  flag
Bit 10  → EmergencyVent active
Bit 11  → IsolationLock active
Bits 12..31 → RESERVED (zero-fill on write, ignore on read)
```

Dirty-check kernel:
```
uint BuildStateBitmask(NodeState prev, NodeState curr):
    mask ← 0
    for i in [0..11]:
        delta ← abs(curr.channels[i] - prev.channels[i])
        if delta > DIRTY_THRESHOLD[i]:   // per-channel epsilon (see §2.1)
            mask |= (1u << i)
    return mask
```

### §2.1 Per-Channel Dirty Thresholds (half-precision units)
```
DIRTY_THRESHOLD[0]  = 0.005   // Oxygen:      0.5% change triggers delta
DIRTY_THRESHOLD[1]  = 0.008   // Power:       0.8%
DIRTY_THRESHOLD[2]  = 0.010   // Thermal:     1.0%
DIRTY_THRESHOLD[3]  = 0.010   // Pressure:    1.0%
DIRTY_THRESHOLD[4]  = 0.012   // Fuel:        1.2%
DIRTY_THRESHOLD[5]  = 0.008   // Coolant:     0.8%
DIRTY_THRESHOLD[6]  = 0.001   // DataLink:    0.1% (binary-ish)
DIRTY_THRESHOLD[7]  = 0.015   // Structural:  1.5%
DIRTY_THRESHOLD[8..11] = 0.0  // Flag bits:   any change = dirty
```

---

## §3. BIT-PACKING MATH

### Float [0..1] → uint10 encode/decode
```
uint10 Encode10(float v):
    v  = clamp(v, 0.0, 1.0)
    return (uint)(v * 1023.0 + 0.5)   // round-nearest

float  Decode10(uint10 u):
    return (float)u / 1023.0
```

### Float [0..1] → uint12 encode/decode (higher-precision channels)
```
uint12 Encode12(float v):
    v  = clamp(v, 0.0, 1.0)
    return (uint)(v * 4095.0 + 0.5)

float  Decode12(uint12 u):
    return (float)u / 4095.0
```

### Compact Logistics Frame — Multi-Channel Pack (5 channels → 60 bits → 8 bytes)
```
PackLogisticsFrame(float O2, float PW, float TH, float PR, float FU) → uint64:
    u64 ← 0
    u64 |= (uint64)Encode12(O2) <<  0    // bits  0..11
    u64 |= (uint64)Encode12(PW) << 12    // bits 12..23
    u64 |= (uint64)Encode12(TH) << 24    // bits 24..35
    u64 |= (uint64)Encode12(PR) << 36    // bits 36..47
    u64 |= (uint64)Encode10(FU) << 48    // bits 48..57
    // bits 58..63 → StatusFlags[0..5] packed inline
    return u64

UnpackLogisticsFrame(uint64 u64) → (O2, PW, TH, PR, FU):
    O2 = Decode12((uint)(u64 >>  0) & 0xFFF)
    PW = Decode12((uint)(u64 >> 12) & 0xFFF)
    TH = Decode12((uint)(u64 >> 24) & 0xFFF)
    PR = Decode12((uint)(u64 >> 36) & 0xFFF)
    FU = Decode10((uint)(u64 >> 48) & 0x3FF)
```

### Signed Delta Compression (resource flow rate — int8 encoding)
```
// Flow rate range assumed [-8.0 .. +8.0] units/sec
int8 EncodeFlowRate(float rate):
    clamped ← clamp(rate, -8.0, 8.0)
    return (int8)(clamped * 15.875)     // 127/8 = 15.875

float DecodeFlowRate(int8 v):
    return (float)v / 15.875
```

---

## §4. TICK SYNCHRONIZATION

### Tick Clock Contract
```
TICK_RATE_BASE     = 20          // Hz — nearby nodes (<200m)
TICK_RATE_DISTANT  = 2           // Hz — far nodes (>200m)
TICK_DURATION_MS   = 50          // 1000 / 20
TICK_ID_TYPE       = uint32      // ~2.7 years @ 20Hz before overflow
TICK_OVERFLOW_GUARD: compare with modular arithmetic, not raw >/<
```

### Node Update Rate Selection
```
SelectTickRate(float distanceSq, float NEAR_SQ = 200*200) → Hz:
    if distanceSq <= NEAR_SQ:  return TICK_RATE_BASE     // 20 Hz
    else:                      return TICK_RATE_DISTANT   // 2 Hz

// Scheduler: maintain per-node NextAllowedTick
ShouldSendUpdate(node, currentTick):
    interval ← TICK_RATE_BASE / SelectTickRate(node.distanceSq)
    return (currentTick % interval) == (node.NodeID % interval)
    // Phase-spread by NodeID: prevents thundering-herd on same tick
```

### Deterministic Resource Transfer (Tick-Locked)
```
TransferResources(srcNode, dstNode, currentTick):
    ASSERT currentTick == srcNode.LastConfirmedTick + 1  // strict ordering
    delta ← srcNode.OutputRate * TICK_DURATION_SEC
    dstNode.Accumulator += delta
    srcNode.StoredAmount -= delta
    // Fractional remainder carries to next tick — no float loss
    srcNode.LastConfirmedTick ← currentTick
```

---

## §5. STATE RECONCILIATION PIPELINE

### Receive → Validate → Apply (Sequential — Job Thread)
```
ReconcilePacket(DeltaPacket pkt, uint localCurrentTick):

    STEP 1 — TIMESTAMP GATE:
        age ← localCurrentTick - pkt.TickID
        if age < 0:          discard  // future packet (clock skew)
        if age > MAX_AGE:    discard  // stale — MAX_AGE = 10 ticks (500ms @20Hz)

    STEP 2 — AUTHORITY CHECK:
        if pkt.NodeID not in AuthoritativeSet[localPeerId]:
            discard  // not owner — reject ghost write

    STEP 3 — SNAPSHOT STORE:
        slot ← pkt.TickID % 3
        ring[pkt.NodeID][slot] ← BuildSnapshot(pkt)

    STEP 4 — VISUAL PROXY LERP (Main Thread deferred):
        s0 ← ring[nodeID][(currentTick - 2) % 3]   // oldest
        s1 ← ring[nodeID][(currentTick - 1) % 3]   // middle
        s2 ← ring[nodeID][(currentTick + 0) % 3]   // newest
        t  ← fractionalTickProgress                  // [0..1] within tick
        visual.Value ← Lerp(Lerp(s0.V, s1.V, t), Lerp(s1.V, s2.V, t), t)
        // Quadratic interp from 3 points: smooth, no extrapolation overshoot
```

### Lerp Safety Clamp
```
SafeLerp(float a, float b, float t) → float:
    t ← clamp(t, 0.0, 1.0)
    return a + (b - a) * t
```

---

## §6. EXTRAPOLATION (PACKET LOSS — LINEAR FLOWS)

### Heartbeat Loss Detection
```
HEARTBEAT_TIMEOUT_TICKS = 20    // 1.0s @ 20Hz — trigger extrapolation
EXTRAP_MAX_TICKS        = 20    // hard cap: stop extrapolating after 1.0s
                                // beyond that → freeze last known + raise alarm

UpdateNodeExtrapolation(node, currentTick):
    ticksSincePacket ← currentTick - node.LastReceivedTick

    if ticksSincePacket == 0:
        return  // live data — no extrapolation needed

    if ticksSincePacket > HEARTBEAT_TIMEOUT_TICKS
    AND ticksSincePacket <= HEARTBEAT_TIMEOUT_TICKS + EXTRAP_MAX_TICKS:
        dt_sec ← (ticksSincePacket - HEARTBEAT_TIMEOUT_TICKS) * TICK_DURATION_SEC
        for each linearChannel in [Oxygen, Power]:
            node.Extrapolated[ch] ← node.LastKnown[ch]
                                  + node.FlowRate[ch] * dt_sec
            node.Extrapolated[ch] ← clamp(node.Extrapolated[ch], 0.0, 1.0)

    if ticksSincePacket > HEARTBEAT_TIMEOUT_TICKS + EXTRAP_MAX_TICKS:
        node.ExtrapolationFrozen ← true
        RaiseEvent(EventID.MODULE_LINK_LOST, node.NodeID)  // uint EventID — no strings
```

### EventID Registry (uint — no string RPCs)
```
EventID.MODULE_LINK_LOST      = 0x0001u
EventID.OXYGEN_CRITICAL       = 0x0002u
EventID.POWER_SURGE           = 0x0003u
EventID.PRESSURE_BREACH       = 0x0004u
EventID.THERMAL_RUNAWAY       = 0x0005u
EventID.ISOLATION_TRIGGERED   = 0x0006u
EventID.EMERGENCY_VENT        = 0x0007u
EventID.DELTA_BUFFER_OVERFLOW = 0x0008u
EventID.TICK_DESYNC_DETECTED  = 0x0009u
// Range 0x0010..0x00FF → gameplay events (module-specific)
// Range 0x0100..0xFFFF → reserved engine/transport layer
```

---

## §7. THROUGHPUT LIMITING & OVERFLOW PROTECTION

### Per-Frame Update Cap
```
MAX_LOGISTICS_UPDATES_PER_FRAME = 256

ProcessLogisticsQueue(queue, frameContext):
    processed ← 0
    while queue.Count > 0 AND processed < MAX_LOGISTICS_UPDATES_PER_FRAME:
        pkt ← queue.Dequeue()
        ReconcilePacket(pkt, frameContext.CurrentTick)
        processed++

    if queue.Count > 0:
        RaiseEvent(EventID.DELTA_BUFFER_OVERFLOW, 0u)
        // Remaining packets NOT dropped — deferred to next frame
        // FORBID: silent discard of non-stale overflow packets
```

### Bandwidth Estimation (Wire Budget — MX350 Target)
```
// Per distant node (2Hz):
//   DeltaPacket = 8 bytes × 2/s = 16 B/s
// Per nearby node (20Hz):
//   DeltaPacket = 8 bytes × 20/s = 160 B/s

// Assume: 64 nearby + 256 distant nodes (typical base layout)
// Budget: (64 × 160) + (256 × 16) = 10240 + 4096 = 14336 B/s ≈ 14 KB/s
// Well within UDP MTU budget — no fragmentation risk at 1400B MTU
```

---

## §8. JOB THREAD DECOMPOSITION (Unity Jobs / Burst Compat)

### Thread Assignment
```
MAIN THREAD:
    - Visual proxy lerp read (§5 STEP 4)
    - EventID dispatch to game systems
    - UI resource gauge update (throttled: 10Hz max)

WORKER THREAD (Job, Burst-compatible):
    - DeltaPacket deserialization + bit-unpack (§3)
    - StateBitmask dirty evaluation (§2)
    - ReconcilePacket STEP 1..3 (§5)
    - Extrapolation update (§6)
    - Queue flush up to MAX cap (§7)

FORBID: NativeCollection access from both threads without explicit sync fence
FORBID: Managed heap alloc inside Job — use NativeArray<DeltaPacket> only
```

### Job Scheduling Pseudo-Pattern
```
ScheduleLogisticsSync(currentTick):
    inputPackets  ← NativeArray<DeltaPacket>(256, Allocator.TempJob)
    snapshots     ← NativeArray<NodeSnapshot>(MAX_NODES × 3, Allocator.Persistent)

    job ← LogisticsSyncJob {
        Packets      = inputPackets,
        Snapshots    = snapshots,
        CurrentTick  = currentTick,
        MaxProcess   = 256
    }
    handle ← job.Schedule()
    JobHandle.ScheduleBatchedJobs()
    // Complete() deferred to LateUpdate — avoid main-thread stall
```

---

## §9. DESYNC DETECTION & RECOVERY

### Tick Drift Guard
```
// Run once per second on host/authority node
DetectTickDesync(localTick, remoteTick):
    drift ← abs((int32)localTick - (int32)remoteTick)
    if drift > DESYNC_THRESHOLD_TICKS:   // threshold = 5 ticks
        RaiseEvent(EventID.TICK_DESYNC_DETECTED, 0u)
        // Recovery: authority broadcasts FullStateSnapshot
        // Client: wipe ring buffer, reingest from full snapshot
        // FORBID: partial merge of desync'd ring — full reset only
```

### Full State Snapshot (Resync Packet — sent on desync only)
```
struct FullStateSnapshot {
    uint32 AuthorityTick
    uint32 NodeCount
    // Followed by NodeCount × CompactLogisticsFrame (8 bytes each)
    // Max: 512 nodes × 8B = 4096 bytes — single MTU-safe UDP payload? NO
    // → Fragment across max 3 packets if NodeCount > 175
    //   Fragment header: uint8 FragIndex | uint8 FragTotal | uint16 SnapshotID
}
```

---

## §10. ANTI-PATTERN REGISTRY (HARD FORBIDS)

```
FORBID_001: string-keyed RPC — use uint EventID always
FORBID_002: float world-position on wire — use AUP struct (§1)
FORBID_003: per-frame full state broadcast — delta only (§2 dirty mask)
FORBID_004: Update() coroutine for network decompression — Job thread only
FORBID_005: uncapped queue processing — enforce 256/frame hard cap (§7)
FORBID_006: ring buffer partial merge on desync — full reset (§9)
FORBID_007: Lerp extrapolation beyond EXTRAP_MAX_TICKS — freeze + alarm (§6)
FORBID_008: Managed alloc inside Burst Job — NativeArray only (§8)
FORBID_009: raw tick comparison (>) across overflow boundary — use modular delta
FORBID_010: sending DeltaPacket when StateBitmask == 0 — skip transmission
```

