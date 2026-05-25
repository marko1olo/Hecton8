# NETWORK_ROLLBACK_SCOUT_REPORT_X_014_APEX_ADDENDUM

Status: PENDING VERIFICATION
Date: 2026-05-25
Agent: X_014

## Scope Correction

`Assets/_Project/Scripts/Networking/HectonNetworkManager.cs` contains no Merkle tree, no state serializer, and no rollback restore code. It only requires `HectonRollbackNetcodeRuntime` and toggles mode:

- `HectonNetworkManager.cs:5-7` requires `HectonRollbackNetcodeRuntime`.
- `HectonNetworkManager.cs:22-48` calls `TrySetMode` / `TryStopMode`.

Actual runtime proof chain:

- `HectonRollbackNetcodeRuntime.cs:480-584` schedules network Merkle jobs.
- `HectonRollbackNetcodeRuntime.cs:919-968` writes Merkle leaf descriptors.
- `RollbackNetcodeContracts.cs:790-1006` computes leaf, branch, and root hashes.
- `RollbackNetcodeContracts.cs:1008-1285` snapshots/restores state.
- `RollbackNetcodeContracts.cs:1867-1935` performs rollback restore flow.

## Leaf Inputs

Network Merkle descriptors are written at `HectonRollbackNetcodeRuntime.cs:925-944`.

| Leaf | Buffer | Type / field set | Stride | Count | Bytes | Flags | Hash route |
|---:|---|---|---:|---:|---:|---|---|
| 0 | `BufferID.RigidbodyAUPs` | `double3` raw x/y/z | 24 | 256 | 6144 | `Authoritative`, `AupExactDouble3` | `HashDouble3Array`, `RollbackNetcodeContracts.cs:857-858`, `931-946` |
| 1 | `BufferID.PlayerKinematicState` | `LockstepPlayerKinematicState`: `PositionAup@0 double3`, `Velocity@24 float3`, `InputVector@36 float3`, `Frame@48 uint`, `Flags@52 uint`, `InputActions@56 uint`, pads `60-63` | 64 | 4 | 256 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:859-860`, `888-905`; layout `LockstepStateValidator.cs:38-52` |
| 2 | `BufferID.EntityAUPs` | Source `RollbackAup48`: `GridX@0`, `GridY@8`, `GridZ@16`, `LocalX@24`, `LocalY@28`, `LocalZ@32`, pads `36/40`; hash canonical `double3 absolute = grid*CellSize + local` | 24 canonical | 512 | 12288 | `Authoritative`, `AupExactDouble3` | `HashAupArray`, `RollbackNetcodeContracts.cs:861-862`, `907-929`; layout `172-185`, formula `697-710` |
| 3 | `BufferID.EntityVelocities` | `float3` raw | 12 | 512 | 6144 | `Authoritative` | `HashNativeArray`, `RollbackNetcodeContracts.cs:863-864`, `888-905` |
| 4 | `BufferID.RoomWaterLevels` | `float` raw | 4 | 256 | 1024 | `Authoritative` | `HashNativeArray`, `865-866`, `888-905` |
| 5 | `BufferID.EntityFlags` | `uint` raw | 4 | 512 | 2048 | `Authoritative` | `HashNativeArray`, `867-868`, `888-905` |
| 6 | `BufferID.EntityItemHashes` | `uint` raw | 4 | 512 | 2048 | `Authoritative` | `HashNativeArray`, `869-870`, `888-905` |
| 7 | `BufferID.EntityQuantities` | `ushort` raw | 2 | 512 | 1024 | `Authoritative` | `HashNativeArray`, `871-872`, `888-905` |
| 8 | `BufferID.ShinobuInventoryHashes` | `uint` raw | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `873-874`, `888-905` |
| 9 | `BufferID.ShinobuInventoryQuantities` | `int` raw | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `875-876`, `888-905` |
| 10 | `BufferID.ShinobuInventoryDurabilities` | `float` raw | 4 | 512 | 2048 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `877-878`, `888-905` |
| 11 | `BufferID.QuestDagGlobalStateMasks` | `ulong` raw | 8 | 128 | 1024 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `879-880`, `888-905` |
| 12 | `BufferID.PredatorCognitionChosenStates` | `byte` raw | 1 | 256 | 256 | `Authoritative`, `OptionalQualityLeaf` | `HashNativeArray`, `881-882`, `888-905` |
| 13-15 | none | no payload | n/a | n/a | n/a | `PresentationExcluded`, `SkippedByQuality` | early return at `RollbackNetcodeContracts.cs:829-837` |

Presentation exclusion proof:

- `VisualStateDTO`, `VisualStateHistoryDTO`, `NetTelemetryEntry64`, `RollbackTuningDTO`, `RollbackAudioSuppressionDTO`, and `InputPredictionTelemetryEntry` are allocated or passed to pipeline at `HectonRollbackNetcodeRuntime.cs:506-510`, `861-866`, but `ComputeMerkleRootJob` only receives hash sources listed at `RollbackNetcodeContracts.cs:793-807`.
- Descriptors 13-15 are explicitly `PresentationExcluded | SkippedByQuality` at `HectonRollbackNetcodeRuntime.cs:939-944`.
- If a descriptor has `PresentationExcluded`, hash code returns before hashing at `RollbackNetcodeContracts.cs:829-837`.

Hard honesty: presentation buffers do not enter network Merkle leaves. But the network root is not mathematically stable across quality profiles because `QualityLeafBudget` changes leaf participation.

## Hash Formulas

Leaf byte hash:

- `HashExactBytes(ptr, byteLength) = MemorySentinelMath.ComputeXXHash3Full64(ptr, byteLength)`, `RollbackNetcodeContracts.cs:678-688`.
- `ComputeXXHash3Full64`: `uint2 hash = xxHash3.Hash64(ptr, byteLength); return ((ulong)hash.y << 32) | hash.x;`, `MemorySentinelContracts.cs:257-263`.

Native array leaf:

- `stride = UnsafeUtility.SizeOf<T>()`
- `start = descriptor.ByteOffset / stride`
- `count = clamp(descriptor.ElementCount, 0, source.Length - min(start, source.Length))`
- `byteLength = count * stride`
- `hash = HashExactBytes(ptr + start * stride, byteLength)`
- Source: `RollbackNetcodeContracts.cs:888-905`.

Entity AUP leaf:

- `absolute.x = GridX * CellSizeMeters + LocalX`
- `absolute.y = GridY * CellSizeMeters + LocalY`
- `absolute.z = GridZ * CellSizeMeters + LocalZ`
- `hash = 0xCBF29CE484222325 ^ descriptor.BufferId`
- For each element: `hash = MixHash64(hash, HashExactAupDouble3(absolute))`
- Source: `RollbackNetcodeContracts.cs:697-710`, `907-929`.

Mix function:

```c
state ^= value + 0x9E3779B97F4A7C15 + (state << 6) + (state >> 2);
state ^= state >> 33;
state *= 0xff51afd7ed558ccd;
state ^= state >> 33;
state *= 0xc4ceb9fe1a85ec53;
state ^= state >> 33;
return state == 0 ? 0xA24BAED4963EE407 : state;
```

Source: `RollbackNetcodeContracts.cs:713-723`.

Leaf node:

- `node.HashLo = hash`
- `node.HashHi = MixHash64(hash, ((ulong)Frame << 32) ^ descriptor.BufferId ^ descriptor.ElementCount)`
- Source: `RollbackNetcodeContracts.cs:840-842`.

Branch node for branch `b`:

- `left = b * 2`
- `right = left + 1`
- `nodeIndex = 16 + b`
- `branch.HashLo = MixHash64(leftNode.HashLo, rightNode.HashLo)`
- `branch.HashHi = MixHash64(leftNode.HashHi, rightNode.HashHi ^ (uint)nodeIndex)`
- Source: `RollbackNetcodeContracts.cs:963-978`.

Root:

- `leafBudget = clamp(QualityLeafBudget, 1, 16)`
- `rootLo = 0x9E3779B97F4A7C15 ^ Frame`
- `rootHi = 0xC2B2AE3D27D4EB4F ^ (uint)leafBudget`
- For each of 8 branches:
  - `rootLo = MixHash64(rootLo, branch.HashLo)`
  - `rootHi = MixHash64(rootHi, branch.HashHi)`
- Store at node 31; `RuntimeState.LastFrameHash64 = rootLo`; `RuntimeState.LastBranchHash64 = rootHi`.
- Source: `RollbackNetcodeContracts.cs:981-1003`.

Stability failure:

- `QualityLeafBudget = ResolveMerkleLeafBudget(tuning, quality)`, scheduled at `HectonRollbackNetcodeRuntime.cs:572`, `582`.
- `ResolveMerkleLeafBudget` lerps from min leaves to max leaves using `GlobalQualityWeight`, `RollbackNetcodeContracts.cs:639-645`.
- Therefore root input set and `rootHi` seed are quality-dependent. Stable cross-peer Merkle proof is impossible from current source if peers have different quality.

## Rollback Restore Pipeline

Runtime buffer ownership:

- DataVault handles are allocated in `TryEnsureBuffers`: state ring `byte`, snapshots, runtime state, remote input, tick commands, visual state/history, telemetry, Merkle nodes, remote Merkle nodes, leaf descriptors, leaf deltas, input journal, jitter state; `HectonRollbackNetcodeRuntime.cs:854-877`.

Fixed-frame schedule:

1. Resolve live authoritative buffers: `RigidbodyAUPs`, `PlayerKinematicState`, `EntityAUPs`, `EntityVelocities`, `RoomWaterLevels`, `EntityFlags`, `EntityItemHashes`, `EntityQuantities`, inventory, quest masks, predator chosen states at `HectonRollbackNetcodeRuntime.cs:518-530`.
2. Schedule jitter job at `541-553`.
3. Schedule `ComputeMerkleRootJob` at `555-575`.
4. Schedule `FinalizeMerkleRootJob` at `577-584`.
5. Schedule `RollbackFixedPipelineJob` and register job handle with `H8Memory.RegisterActiveJob`, `599-660`.

Snapshot write:

- `StateSnapshotJob` copies active simulation buffers into `StateRingBuffer` page `Frame % RingFrameCapacity`, `RollbackNetcodeContracts.cs:1047-1058`.
- Copy order is exact: RigidbodyAups, PlayerStates, EntityAups, EntityVelocities, RoomWaterLevels, EntityFlags, EntityItemHashes, EntityQuantities, InventoryHashes, InventoryQuantities, InventoryDurabilities, QuestMasks, PredatorChosenStates; `1070-1082`.
- Copy command is `UnsafeUtility.MemCpy(cursor, sourcePtr, byteCount)`, `1158-1160`.
- Frame hash uses Merkle root `HashLo` when available, else raw page hash: `header->FrameHash64 = merkleHash == 0 ? HashExactBytes(page, hashBytes) : merkleHash`, `1099-1107`.

Rollback restore:

- `ExecuteRollback` chooses `rollbackFrame = state.LastMismatchFrame`, `RollbackNetcodeContracts.cs:1867-1870`.
- It constructs `RestoreSnapshotJob` with `StateRingBuffer` plus all active simulation buffers and executes it synchronously with `restore.Execute()`, `1872-1893`.
- `RestoreSnapshotJob` selects page `RollbackFrame % RingFrameCapacity`, reads `StatePageHeaderDTO`, requires `header->Frame == RollbackFrame` and nonzero payload, `1193-1205`.
- It copies the payload back into active buffers in the same order via `TryCopyDestination`, `1215-1230`.
- Actual overwrite command: `UnsafeUtility.MemCpy(destinationPtr, source, copyBytes)`, `1275-1279`.
- Runtime state is updated with `LastRollbackFrame`, `StateMemoryOffset`, `StateSnapshotBytes`, and clears `SnapshotMissing`, `1236-1244`.

After restore:

- `ApplyRemoteInputCorrectionJob` corrects prediction inputs, invoked at `1903-1910`.
- `HeadlessResimulationCommandJob` writes `MockTickCommand` into `Commands[0]`, writes `RollbackAudioSuppressionDTO` into `AudioSuppression[0]`, increments `RollbacksTriggered` / `FramesResimulated`, and sets `Resimulating`, `1648-1692`, invoked at `1912-1921`.
- No audited line executes full fast-forward simulation replay. The code emits a command describing frames to simulate; it does not run the replay loop itself in the audited source.
- Visual correction is written after restore/resim command into `VisualStates[0]` / `VisualHistory[0]`, `1933`, `2192-2220`. This is presentation smoothing after authoritative restore, not a Merkle input.

Remote hash mismatch:

- `CheckRemoteHashFence` runs by quality-dependent cadence, `1980-1985`.
- If `LastRemoteHash64 != LastFrameHash64`, it sets `HashMismatch | BranchProbeRequested`, increments `DesyncCount` and `DesyncRepairAttempts`, and requests hard resync after 3 attempts, `1992-2010`.
- Leaf delta writes only `LeafDeltaRecords[0]`, `2102-2123`.
