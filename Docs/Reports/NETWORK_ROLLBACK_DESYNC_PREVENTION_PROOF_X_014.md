# NETWORK_ROLLBACK_DESYNC_PREVENTION_PROOF_X_014

Status: PENDING VERIFICATION
Date: 2026-05-25
Agent: X_014 / MERKLE_STATE_AND_NETWORK_ROLLBACK_SCOUT
Evidence class: STATIC_SOURCE_ONLY
Code mutation: NONE

## 0. Hard Scope Fact

`Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` cannot prove Merkle stability by itself.

What exists in that file:

- `HectonNetworkManager.cs:5-7`: `RequireComponent(typeof(HectonRollbackNetcodeRuntime))`.
- `HectonNetworkManager.cs:10-13`: serialized mode fields only: `isServer`, `isClient`, `serverAddress`, `port`.
- `HectonNetworkManager.cs:22-48`: mode calls only: `TrySetMode(server/client)` and `TryStopMode()`.

What does not exist in that file:

- no Merkle descriptor table;
- no Merkle node struct;
- no hash algorithm;
- no state-ring serialization;
- no rollback restore;
- no transport packet serialization.

Therefore every Merkle/desync proof below follows the actual source route:

`HectonNetworkManager` -> `HectonRollbackNetcodeRuntime` -> `RollbackNetcodeContracts`.

Primary source lines:

- `HectonRollbackNetcodeRuntime.cs:480-584`: frame schedule and Merkle job construction.
- `HectonRollbackNetcodeRuntime.cs:919-968`: Merkle descriptor construction.
- `RollbackNetcodeContracts.cs:790-1006`: leaf, branch, and root hash jobs.
- `RollbackNetcodeContracts.cs:1008-1285`: snapshot write/restore.
- `RollbackNetcodeContracts.cs:1867-1935`: rollback restore pipeline.

## 1. Exact Network Merkle Leaf Set

Descriptor storage:

- Descriptor type: `RollbackVaultBufferDescriptor32`, explicit size 32.
- Fields: `BufferId@0 uint`, `ByteOffset@4 uint`, `ByteLength@8 uint`, `ElementStride@12 uint`, `ElementCount@16 uint`, `Flags@20 uint`, `LeafIndex@24 uint`, `Generation@28 uint`.
- Evidence: `RollbackNetcodeContracts.cs:375-386`.

Descriptor creation:

- `InitializeAuthoritativeMerkleDescriptors()`, `HectonRollbackNetcodeRuntime.cs:919-946`.
- `WriteMerkleDescriptor()` writes `ByteOffset = 0`, `ElementStride`, `ElementCount`, `ByteLength = ElementStride * ElementCount`, flags, leaf index, generation `1`, `HectonRollbackNetcodeRuntime.cs:948-968`.

Hash job accepted source buffers:

- `RollbackNetcodeContracts.cs:793-807`: `RigidbodyAups`, `PlayerStates`, `EntityAups`, `EntityVelocities`, `RoomWaterLevels`, `EntityFlags`, `EntityItemHashes`, `EntityQuantities`, `InventoryHashes`, `InventoryQuantities`, `InventoryDurabilities`, `QuestMasks`, `PredatorChosenStates`.

No other source buffer is accepted by `ComputeMerkleRootJob`.

| Leaf | Descriptor line | Buffer | Source type | Element stride | Count | Byte length | Flags | Hash path |
|---:|---|---|---|---:|---:|---:|---|---|
| 0 | `HectonRollbackNetcodeRuntime.cs:925` | `BufferID.RigidbodyAUPs` | `NativeArray<double3>` | 24 | 256 | 6144 | `Authoritative`, `AupExactDouble3` | `HashDouble3Array`, `RollbackNetcodeContracts.cs:857-858`, `931-946` |
| 1 | `HectonRollbackNetcodeRuntime.cs:926` | `BufferID.PlayerKinematicState` | `NativeArray<LockstepPlayerKinematicState>` | 64 | 4 | 256 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:859-860`, `888-905` |
| 2 | `HectonRollbackNetcodeRuntime.cs:927` | `BufferID.EntityAUPs` | `NativeArray<RollbackAup48>` hashed as canonical `double3` | 24 canonical | 512 | 12288 | `Authoritative`, `AupExactDouble3` | `HashAupArray`, `RollbackNetcodeContracts.cs:861-862`, `907-929` |
| 3 | `HectonRollbackNetcodeRuntime.cs:928` | `BufferID.EntityVelocities` | `NativeArray<float3>` | 12 | 512 | 6144 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:863-864`, `888-905` |
| 4 | `HectonRollbackNetcodeRuntime.cs:929` | `BufferID.RoomWaterLevels` | `NativeArray<float>` | 4 | 256 | 1024 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:865-866`, `888-905` |
| 5 | `HectonRollbackNetcodeRuntime.cs:930` | `BufferID.EntityFlags` | `NativeArray<uint>` | 4 | 512 | 2048 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:867-868`, `888-905` |
| 6 | `HectonRollbackNetcodeRuntime.cs:931` | `BufferID.EntityItemHashes` | `NativeArray<uint>` | 4 | 512 | 2048 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:869-870`, `888-905` |
| 7 | `HectonRollbackNetcodeRuntime.cs:932` | `BufferID.EntityQuantities` | `NativeArray<ushort>` | 2 | 512 | 1024 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:871-872`, `888-905` |
| 8 | `HectonRollbackNetcodeRuntime.cs:933` | `BufferID.ShinobuInventoryHashes` | `NativeArray<uint>` | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `RollbackNetcodeContracts.cs:873-874`, `888-905` |
| 9 | `HectonRollbackNetcodeRuntime.cs:934` | `BufferID.ShinobuInventoryQuantities` | `NativeArray<int>` | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `RollbackNetcodeContracts.cs:875-876`, `888-905` |
| 10 | `HectonRollbackNetcodeRuntime.cs:935` | `BufferID.ShinobuInventoryDurabilities` | `NativeArray<float>` | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `RollbackNetcodeContracts.cs:877-878`, `888-905` |
| 11 | `HectonRollbackNetcodeRuntime.cs:936` | `BufferID.QuestDagGlobalStateMasks` | `NativeArray<ulong>` | 8 | 128 | 1024 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `RollbackNetcodeContracts.cs:879-880`, `888-905` |
| 12 | `HectonRollbackNetcodeRuntime.cs:937` | `BufferID.PredatorCognitionChosenStates` | `NativeArray<byte>` | 1 | 256 | 256 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `RollbackNetcodeContracts.cs:881-882`, `888-905` |
| 13-15 | `HectonRollbackNetcodeRuntime.cs:939-944` | none | none | n/a | n/a | 0 | `PresentationExcluded`, `SkippedByQuality` | early return at `RollbackNetcodeContracts.cs:829-837` |

### 1.1 Leaf 1 Inner Fields

`LockstepPlayerKinematicState`, explicit size 64:

- `PositionAup@0 double3`
- `Velocity@24 float3`
- `InputVector@36 float3`
- `Frame@48 uint`
- `Flags@52 uint`
- `InputActions@56 uint`
- `_pad0@60 byte`, `_pad1@61 byte`, `_pad2@62 byte`, `_pad3@63 byte`

Evidence: `Assets/_Project/Scripts/Core/Determinism/LockstepStateValidator.cs:38-52`.

### 1.2 Leaf 2 Inner Fields

`RollbackAup48`, explicit size 48:

- `GridX@0 long`
- `GridY@8 long`
- `GridZ@16 long`
- `LocalX@24 float`
- `LocalY@28 float`
- `LocalZ@32 float`
- `_pad0@36 float`
- `_pad1@40 ulong`

Evidence: `RollbackNetcodeContracts.cs:172-185`.

Hash does not hash the raw 48-byte record. It converts to canonical `double3`:

```c
absolute.x = GridX * CellSizeMeters + LocalX;
absolute.y = GridY * CellSizeMeters + LocalY;
absolute.z = GridZ * CellSizeMeters + LocalZ;
```

Evidence: `RollbackNetcodeContracts.cs:697-710`.

### 1.3 Presentation Exclusion Proof

Presentation structs exist but are not Merkle descriptors:

- `VisualStateDTO`, explicit size 64, fields `AnchorAupAbsolute@0`, `TrueLocalMeters@24`, `InterpolatedLocalMeters@36`, `Blend01@48`, `BlendStep01@52`, `EntityId@56`, `Flags@60`; evidence `RollbackNetcodeContracts.cs:297-307`.
- `VisualStateHistoryDTO`, explicit size 64, fields `Offset0@0`, `Offset1@12`, `Offset2@24`, `LastOutput@36`, `EntityId@48`, `Cursor@52`, `Flags@56`, `CorrectionFrame@60`; evidence `RollbackNetcodeContracts.cs:309-320`.
- `NetTelemetryEntry64`, explicit size 64; evidence `RollbackNetcodeContracts.cs:322-339`.
- `InputPredictionTelemetryEntry`, explicit size 64; evidence `InputDeterminismDtos.cs:73-90`.
- `RollbackAudioSuppressionDTO`, explicit size 16; evidence `RollbackNetcodeContracts.cs:353-360`.
- `RollbackTuningDTO`, explicit size 64; evidence `RollbackNetcodeContracts.cs:251-270`.

They are allocated/passed to pipeline:

- `VisualStates`, `VisualHistory`, `Telemetry`, `InputPredictionTelemetry`, `Tuning`, `AudioSuppression`: `HectonRollbackNetcodeRuntime.cs:506-510`, `861-866`.
- Pipeline fields include visual/telemetry/audio/tuning buffers: `RollbackNetcodeContracts.cs:1697-1714`.

But they are absent from:

- descriptor writes `HectonRollbackNetcodeRuntime.cs:925-944`;
- `ComputeMerkleRootJob` hash source list `RollbackNetcodeContracts.cs:793-807`;
- `HashDescriptor` switch `RollbackNetcodeContracts.cs:855-884`.

Mechanical exclusion rule:

```c
if ((descriptor.Flags & RollbackMerkleFlags.PresentationExcluded) != 0u)
{
    node.Flags = PresentationExcluded | SkippedByQuality;
    MerkleNodes[index] = node;
    return;
}
```

Evidence: `RollbackNetcodeContracts.cs:829-837`.

Conclusion: camera shake, audio fade, UI glitch, visual interpolation, telemetry, and audio suppression DTOs do not physically enter network Merkle leaves in current source.

Limit: this proves DTO/descriptors exclusion only. It does not prove that some other gameplay owner never writes presentation-derived values into authoritative arrays such as `EntityFlags` or `RoomWaterLevels`. That semantic owner proof is outside `HectonNetworkManager` and outside `RollbackNetcodeContracts`.

## 2. Hash Algorithm And Exact Math

### 2.1 Leaf Byte Hash

`RollbackNetcodeMath.HashExactBytes` calls `MemorySentinelMath.ComputeXXHash3Full64`:

- `RollbackNetcodeContracts.cs:678-688`.

Actual 64-bit packing:

```c
uint2 hash = xxHash3.Hash64(ptr, (long)byteLength);
return ((ulong)hash.y << 32) | hash.x;
```

Evidence: `MemorySentinelContracts.cs:257-263`.

### 2.2 Generic NativeArray Leaf Formula

Source: `RollbackNetcodeContracts.cs:888-905`.

```c
stride = UnsafeUtility.SizeOf<T>();
start = descriptor.ByteOffset / stride;
desired = descriptor.ElementCount == 0 ? source.Length : descriptor.ElementCount;
count = clamp(desired, 0, source.Length - min(start, source.Length));
byteLength = count * stride;
ptr = sourceBase + (start * stride);
hash = HashExactBytes(ptr, byteLength);
```

### 2.3 Entity AUP Leaf Formula

Source: `RollbackNetcodeContracts.cs:907-929`.

```c
stride = sizeof(double3); // 24, not sizeof(RollbackAup48)
start = descriptor.ByteOffset / 24;
count = clamp(descriptor.ElementCount, 0, source.Length - min(start, source.Length));
hash = 0xCBF29CE484222325 ^ descriptor.BufferId;
for each aup:
    hash = MixHash64(hash, HashExactAupDouble3(AbsoluteFromAup(aup)));
byteLength = count * 24;
```

Risk: current descriptors set `ByteOffset = 0`, so no active offset defect is proven. Future nonzero `ByteOffset` for `EntityAUPs` would be ambiguous because source indexing uses 24-byte canonical stride while storage is 48-byte `RollbackAup48`.

### 2.4 MixHash64 Formula

Source: `RollbackNetcodeContracts.cs:713-723`.

```c
state ^= value + 0x9E3779B97F4A7C15UL + (state << 6) + (state >> 2);
state ^= state >> 33;
state *= 0xff51afd7ed558ccdUL;
state ^= state >> 33;
state *= 0xc4ceb9fe1a85ec53UL;
state ^= state >> 33;
return state == 0UL ? 0xA24BAED4963EE407UL : state;
```

Bit shifts:

- `state << 6`
- `state >> 2`
- `state >> 33` repeated three times
- XXHash packing uses `hash.y << 32`

### 2.5 Leaf Node Formula

Source: `RollbackNetcodeContracts.cs:840-849`.

```c
node.HashLo = hash;
node.HashHi = MixHash64(hash, ((ulong)Frame << 32) ^ descriptor.BufferId ^ descriptor.ElementCount);
node.BufferId = descriptor.BufferId;
node.ByteOffset = descriptor.ByteOffset;
node.ByteLength = byteLength;
node.Flags = descriptor.Flags & ~(Missing | SkippedByQuality);
if (hash == 0 || byteLength == 0) node.Flags |= Missing;
```

### 2.6 Branch Formula

Source: `RollbackNetcodeContracts.cs:963-978`.

For branch `b in 0..7`:

```c
left = b * 2;
right = left + 1;
nodeIndex = 16 + b;
leftNode = left < leafBudget ? MerkleNodes[left] : default;
rightNode = right < leafBudget ? MerkleNodes[right] : default;
branch.HashLo = MixHash64(leftNode.HashLo, rightNode.HashLo);
branch.HashHi = MixHash64(leftNode.HashHi, rightNode.HashHi ^ (uint)nodeIndex);
branch.ByteOffset = left;
branch.ByteLength = min(2, max(0, leafBudget - left));
branch.Flags = BranchNode;
MerkleNodes[nodeIndex] = branch;
```

### 2.7 Root Formula

Source: `RollbackNetcodeContracts.cs:981-1003`.

```c
rootLo = 0x9E3779B97F4A7C15UL ^ Frame;
rootHi = 0xC2B2AE3D27D4EB4FUL ^ (uint)leafBudget;
for i in 0..7:
    branchNode = MerkleNodes[16 + i];
    rootLo = MixHash64(rootLo, branchNode.HashLo);
    rootHi = MixHash64(rootHi, branchNode.HashHi);
root.HashLo = rootLo;
root.HashHi = rootHi;
root.ByteLength = leafBudget;
root.Flags = RootNode;
MerkleNodes[31] = root;
RuntimeState[0].LastFrameHash64 = rootLo;
RuntimeState[0].LastBranchHash64 = rootHi;
```

### 2.8 Counterexample To Stability

Quality leaf budget formula:

- `HectonRollbackNetcodeRuntime.cs:572`, `582`: jobs receive `ResolveMerkleLeafBudget(in tuning, quality)`.
- `RollbackNetcodeContracts.cs:639-645`:

```c
maxLeaves = clamp(tuning.MaxMerkleLeaves == 0 ? 16 : tuning.MaxMerkleLeaves, 1, 16);
minLeaves = min(4, maxLeaves);
qualityWeight = saturate(isfinite(quality) ? quality : 1);
normalized = saturate((qualityWeight - 0.1) * 1.1111112);
leafBudget = clamp(round(lerp(minLeaves, maxLeaves, Smooth01(normalized))), 1, maxLeaves);
```

`Smooth01(x)`:

```c
x = saturate(value);
return x * x * (3 - 2 * x);
```

Evidence: `RollbackNetcodeContracts.cs:589-592`.

Counterexample:

- Peer A quality near low: `leafBudget` can be 4.
- Peer B quality high: `leafBudget` can be 16.
- Same authoritative state arrays.
- Branch construction only reads leaves `< leafBudget`, `RollbackNetcodeContracts.cs:969-970`.
- Root seed includes `leafBudget`, `RollbackNetcodeContracts.cs:981-982`.

Therefore:

`root_A != root_B` is possible without any state divergence.

This is not stable rollback Merkle across heterogeneous quality settings. It is a desync-prevention defect.

## 3. Runtime Rollback Restore Pipeline

### 3.1 Vault Buffers

Allocation route: `HectonRollbackNetcodeRuntime.TryEnsureBuffers`, `HectonRollbackNetcodeRuntime.cs:839-893`.

Buffers:

- `StateRingBuffer`: `byte`, `RollbackNetcodeVault.StateRingBuffer`, allocated at `HectonRollbackNetcodeRuntime.cs:854-856`.
- `FrameSnapshots`: `FrameSnapshotDTO`, at `857`.
- `RuntimeState`: `RollbackRuntimeStateDTO`, at `858`.
- `RemoteInputRing`: `RemoteInputFrameDTO`, at `859`.
- `TickCommands`: `MockTickCommand`, at `860`.
- `VisualStates`: `VisualStateDTO`, at `861`.
- `VisualHistory`: `VisualStateHistoryDTO`, at `862`.
- `TelemetryRing`: `NetTelemetryEntry64`, at `863`.
- `InputPredictionTelemetry`: `InputPredictionTelemetryEntry`, at `864`.
- `Tuning`: `RollbackTuningDTO`, at `865`.
- `AudioSuppression`: `RollbackAudioSuppressionDTO`, at `866`.
- `MerkleNodes`: `H8NetMerkleNodeRecord32`, at `871`.
- `RemoteMerkleNodes`: `H8NetMerkleNodeRecord32`, at `872`.
- `MerkleLeafDescriptors`: `RollbackVaultBufferDescriptor32`, at `873`.
- `LeafDeltaRecords`: `H8NetLeafDeltaRecord64`, at `874`.
- `InputJournalRing`: `RollbackInputJournalSlot64`, at `875`.
- `MockJitterPackets`: `MockNetworkJitterPacket64`, at `876`.
- `MockJitterState`: `MockNetworkJitterState64`, at `877`.

Borrowed live simulation buffers resolved in schedule:

- `RigidbodyAUPs`, `PlayerKinematicState`, `EntityAUPs`, `EntityVelocities`, `RoomWaterLevels`, `EntityFlags`, `EntityItemHashes`, `EntityQuantities`, `ShinobuInventoryHashes`, `ShinobuInventoryQuantities`, `ShinobuInventoryDurabilities`, `QuestDagGlobalStateMasks`, `PredatorCognitionChosenStates`.
- Evidence: `HectonRollbackNetcodeRuntime.cs:518-530`.

### 3.2 Snapshot Write

Job: `StateSnapshotJob`, `RollbackNetcodeContracts.cs:1008-1164`.

Page selection:

```c
pageIndex = Frame % RingFrameCapacity;
pageOffset = pageIndex * SnapshotStrideBytes;
page = StateRingBuffer + pageOffset;
```

Evidence: `RollbackNetcodeContracts.cs:1052-1059`.

Write order:

1. `RigidbodyAups`, `RollbackNetcodeContracts.cs:1070`
2. `PlayerStates`, `1071`
3. `EntityAups`, `1072`
4. `EntityVelocities`, `1073`
5. `RoomWaterLevels`, `1074`
6. `EntityFlags`, `1075`
7. `EntityItemHashes`, `1076`
8. `EntityQuantities`, `1077`
9. `InventoryHashes`, `1078`
10. `InventoryQuantities`, `1079`
11. `InventoryDurabilities`, `1080`
12. `QuestMasks`, `1081`
13. `PredatorChosenStates`, `1082`

Actual write command:

```c
UnsafeUtility.MemCpy(cursor, sourcePtr, byteCount);
```

Evidence: `RollbackNetcodeContracts.cs:1158-1160`.

Snapshot hash:

```c
merkleHash = MerkleNodes[31].HashLo;
header->FrameHash64 = merkleHash == 0 ? HashExactBytes(page, hashBytes) : merkleHash;
```

Evidence: `RollbackNetcodeContracts.cs:1099-1107`.

Frame snapshot sidecar:

- `snapshot.FrameHash64`, `snapshot.Tick`, `snapshot.InputMaskP1`, `snapshot.InputMaskP2`, `snapshot.MemoryOffset`, `snapshot.MerkleRootIndex`, `snapshot.Flags`.
- Evidence: `RollbackNetcodeContracts.cs:1109-1117`.

Runtime state update:

- `CurrentFrame`, `LastFrameHash64`, `StateSnapshotBytes`, `StateMemoryOffset`, `LastBranchHash64`.
- Evidence: `RollbackNetcodeContracts.cs:1119-1137`.

### 3.3 Rollback Restore

Entry: `ExecuteRollback`, `RollbackNetcodeContracts.cs:1867-1935`.

Rollback frame:

```c
rollbackFrame = state.LastMismatchFrame;
```

Evidence: `RollbackNetcodeContracts.cs:1869`.

Restore job construction:

- `StateRingBuffer`
- active simulation destinations: `RigidbodyAups`, `PlayerStates`, `EntityAups`, `EntityVelocities`, `RoomWaterLevels`, `EntityFlags`, `EntityItemHashes`, `EntityQuantities`, `InventoryHashes`, `InventoryQuantities`, `InventoryDurabilities`, `QuestMasks`, `PredatorChosenStates`
- `RuntimeState`
- `RollbackFrame`
- `RingFrameCapacity`
- `SnapshotStrideBytes`

Evidence: `RollbackNetcodeContracts.cs:1872-1892`.

Synchronous restore:

```c
restore.Execute();
```

Evidence: `RollbackNetcodeContracts.cs:1893`.

Restore job page selection:

```c
pageIndex = RollbackFrame % RingFrameCapacity;
pageOffset = pageIndex * SnapshotStrideBytes;
page = StateRingBuffer + pageOffset;
header = (StatePageHeaderDTO*)page;
```

Evidence: `RollbackNetcodeContracts.cs:1193-1200`.

Validation:

```c
if (header->Frame != RollbackFrame || header->PayloadBytes == 0) MarkSnapshotMissing();
if (payloadBytes <= 0 || payloadBytes > availablePayloadBytes) MarkSnapshotMissing();
```

Evidence: `RollbackNetcodeContracts.cs:1201-1213`.

Restore order is the same as write order:

- `RigidbodyAups`, `PlayerStates`, `EntityAups`, `EntityVelocities`, `RoomWaterLevels`, `EntityFlags`, `EntityItemHashes`, `EntityQuantities`, `InventoryHashes`, `InventoryQuantities`, `InventoryDurabilities`, `QuestMasks`, `PredatorChosenStates`.
- Evidence: `RollbackNetcodeContracts.cs:1217-1230`.

Actual overwrite command:

```c
UnsafeUtility.MemCpy(destinationPtr, source, copyBytes);
source += byteCount;
remainingPayloadBytes -= byteCount;
```

Evidence: `RollbackNetcodeContracts.cs:1275-1284`.

Runtime state after successful restore:

```c
state.LastRollbackFrame = RollbackFrame;
state.StateMemoryOffset = pageOffset;
state.StateSnapshotBytes = 128 + header->PayloadBytes;
state.Flags &= ~SnapshotMissing;
```

Evidence: `RollbackNetcodeContracts.cs:1236-1244`.

Risk:

`TryCopyDestination` uses:

```c
count = destination.IsCreated ? min(serializedCount, destination.Length) : 0;
```

Evidence: `RollbackNetcodeContracts.cs:1272-1274`.

If destination length is smaller than serialized count, it copies only destination length but advances by full serialized byte count and returns true. That is silent truncation risk.

### 3.4 Input Correction Before Resim Command

`ApplyRemoteInputCorrectionJob`:

- reads `RemoteInputRing`;
- overwrites `PredictedJournal[frame % PredictedJournal.Length] = remote.Input`;
- only when remote frame matches and flags include `Received | Valid`;
- skips `ModQuarantined`.

Evidence: `RollbackNetcodeContracts.cs:1617-1645`.

Exact overwrite:

```c
PredictedJournal[(int)(frame % (uint)PredictedJournal.Length)] = remote.Input;
```

Evidence: `RollbackNetcodeContracts.cs:1642`.

### 3.5 Fast-Forward Reality

`HeadlessResimulationCommandJob` does not run replay. It writes a command.

Command write:

```c
command.CurrentFrame = CurrentFrame;
command.RollbackFrame = RollbackFrame;
command.FramesToSimulate = min(frames, ushort.MaxValue);
command.PhaseMask = PhaseSimulation | PhasePostSimulation;
command.Flags = 1;
command.InputMaskP1 = InputMaskP1;
Commands[0] = command;
```

Evidence: `RollbackNetcodeContracts.cs:1659-1669`.

Audio suppression write:

```c
AudioSuppression[0] = suppression;
```

Evidence: `RollbackNetcodeContracts.cs:1672-1680`.

Runtime counters:

```c
state.RollbacksTriggered++;
state.FramesResimulated += frames;
state.LastRollbackFrame = RollbackFrame;
state.Flags |= Resimulating;
```

Evidence: `RollbackNetcodeContracts.cs:1682-1690`.

Call site:

- `ExecuteRollback` invokes correction at `RollbackNetcodeContracts.cs:1903-1910`.
- `ExecuteRollback` invokes command at `RollbackNetcodeContracts.cs:1912-1921`.
- It estimates cost and clears `RollbackRequired` at `1923-1931`.
- It writes visual correction at `1933`.

No audited line executes actual deterministic fast-forward simulation. Current code emits `MockTickCommand`; a separate replay executor is not proven in the audited source.

### 3.6 Presentation After Restore

Visual correction:

- writes `VisualStates[0]`;
- writes `VisualHistory[0]`;
- uses `Blend01`, `BlendStep01`, smoothed offsets.

Evidence: `RollbackNetcodeContracts.cs:2192-2220`.

`VisualStateInterpolatorJob` applies smoothing later:

- `RollbackNetcodeContracts.cs:2245-2302`.

This occurs after restore and command emission. It is not a Merkle leaf input because visual buffers are absent from descriptors and hash switch.

## 4. Desync Prevention Findings

### X014-DESYNC-001: Quality Changes Authoritative Root

Cause:

- `QualityLeafBudget` derives from `GlobalQualityWeight`.
- Leaves with index `>= QualityLeafBudget` are marked `SkippedByQuality`, `RollbackNetcodeContracts.cs:819-824`.
- Branches use only leaves `< leafBudget`, `RollbackNetcodeContracts.cs:969-970`.
- Root seed includes `leafBudget`, `RollbackNetcodeContracts.cs:981-982`.

Impact:

- Same authoritative arrays can hash differently on peers with different quality.
- This is a false-desync source.

Required future correction:

- Cross-peer root must hash a fixed authoritative leaf set independent of `GlobalQualityWeight`.
- `GlobalQualityWeight` may scale cadence, telemetry, packet redundancy, branch probe frequency, visual interpolation, or optional non-authoritative debug lanes, but not root truth membership.

### X014-DESYNC-002: No Full Replay Executor Proven

Cause:

- `HeadlessResimulationCommandJob` writes `MockTickCommand`, but no audited loop consumes it and replays frames.

Impact:

- The system can request replay but cannot be proven to complete rollback repair from this source slice alone.

Required future correction:

- Define one replay owner that consumes `TickCommands`, executes deterministic simulation from restored snapshot through `CurrentFrame`, and writes proof telemetry/hash after replay.

### X014-DESYNC-003: Restore Capacity Mismatch Can Truncate

Cause:

- `TryCopyDestination` copies `min(serializedCount, destination.Length)` but advances by full serialized byte count.

Impact:

- If future buffer capacity changes, restore may partially restore and still report success.

Required future correction:

- Enforce exact `destination.Length >= serializedCount` for authoritative buffers, or mark `SnapshotMissing`/capacity mismatch.

### X014-DESYNC-004: EntityAUP ByteOffset Contract Ambiguous

Cause:

- Descriptor stride for `EntityAUPs` is `sizeof(double3)` but source array is `RollbackAup48`.

Impact:

- Current `ByteOffset = 0` is safe. Future subrange hashing with nonzero byte offset can mis-index.

Required future correction:

- Explicitly define `EntityAUPs.ByteOffset` as canonical-AUP index space or storage-byte space. Do not mix both.

## 5. Final Source Verdict

Network presentation buffers do not physically enter Merkle leaves in current code.

Network Merkle stability is not proven. It is contradicted by quality-dependent `QualityLeafBudget`.

Rollback restore overwrites active simulation state from `StateRingBuffer` via `UnsafeUtility.MemCpy` into the live Vault-resolved arrays, then corrects predicted input from `RemoteInputRing`, then emits `MockTickCommand`.

Fast-forward recalculation is not proven in audited source. Only command emission is proven.

## 6. Communication Directive

User directive received: write substantive findings into documents, not chat.

Active proof artifact:

- `Docs/Reports/NETWORK_ROLLBACK_DESYNC_PREVENTION_PROOF_X_014.md`

Supporting artifacts:

- `Docs/Reports/NETWORK_ROLLBACK_SCOUT_REPORT_X_014.json`
- `Docs/Reports/NETWORK_ROLLBACK_SCOUT_REPORT_X_014_APEX_ADDENDUM.md`
- `Docs/Tasks/Status_X_014.md`
- `Docs/AgentLogs/Rationale_X_014.md`
- `Docs/AgentLogs/LOG_X_014.md`
