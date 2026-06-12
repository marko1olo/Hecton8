# X_013 APEX Addendum

Scope: paranoid read-only follow-up on `GlobalDataVault.cs` and `H8Memory.cs`.
Source mutation: none.
Build: not run; no C# changed.

## 1. VaultArenaBlock Disk/Native Layout

Source declaration:

```text
GlobalDataVault.cs:363 [StructLayout(LayoutKind.Explicit, Size = 32)]
GlobalDataVault.cs:364 internal struct VaultArenaBlock
GlobalDataVault.cs:366 [FieldOffset(0)] public long OffsetBytes;
GlobalDataVault.cs:367 [FieldOffset(8)] public long Bytes;
GlobalDataVault.cs:368 [FieldOffset(16)] public int BufferKey;
GlobalDataVault.cs:369 [FieldOffset(20)] public int H8BlockIndex;
GlobalDataVault.cs:370 [FieldOffset(24)] public uint Version;
GlobalDataVault.cs:371 [FieldOffset(28)] public byte State;
GlobalDataVault.cs:372 [FieldOffset(29)] public byte Reserved0;
GlobalDataVault.cs:373 [FieldOffset(30)] public ushort Reserved1;
```

Byte map:

```text
00..07  OffsetBytes   long    8 bytes
08..15  Bytes         long    8 bytes
16..19  BufferKey     int     4 bytes
20..23  H8BlockIndex  int     4 bytes
24..27  Version       uint    4 bytes
28      State         byte    1 byte
29      Reserved0     byte    1 byte
30..31  Reserved1     ushort  2 bytes
```

Size proof:

```text
32 total bytes from StructLayout Size = 32.
Last field Reserved1 occupies bytes 30..31.
32 % 8 == 0, so the struct size is 8-byte aligned.
The two long fields start at offsets 0 and 8, both 8-byte aligned.
No implicit padding can alter the layout because LayoutKind.Explicit and FieldOffset are used.
```

Runtime/source ABI validator:

```text
GlobalDataVault.cs:758 UnsafeUtility.SizeOf<VaultArenaBlock>() == VaultArenaBlockSizeBytes
GlobalDataVault.cs:885 ByteOffset(arenaBase, &arena.OffsetBytes) == 0
GlobalDataVault.cs:886 ByteOffset(arenaBase, &arena.Bytes) == 8
GlobalDataVault.cs:887 ByteOffset(arenaBase, &arena.BufferKey) == 16
GlobalDataVault.cs:888 ByteOffset(arenaBase, &arena.H8BlockIndex) == 20
GlobalDataVault.cs:889 ByteOffset(arenaBase, &arena.Version) == 24
GlobalDataVault.cs:890 ByteOffset(arenaBase, &arena.State) == 28
GlobalDataVault.cs:891 ByteOffset(arenaBase, &arena.Reserved0) == 29
GlobalDataVault.cs:892 ByteOffset(arenaBase, &arena.Reserved1) == 30
```

Verdict: `VaultArenaBlock` is byte-explicit and 8-byte-size-aligned. Hidden compiler padding cannot move fields; only bytes 28..31 are manually packed metadata bytes.

## 2. TryAcquireWriteLock and ActiveBurstLockMask

Direct finding: `TryAcquireWriteLock` does not manipulate `ActiveBurstLockMask` or `_activeLocks`.

Exact writer-lock operations in `TryAcquireWriteLock`:

```text
GlobalDataVault.cs:1464 VaultBufferMeta* metadata = (VaultBufferMeta*)NativeArrayUnsafeUtility.GetUnsafePtr(_metadataByBufferId);
GlobalDataVault.cs:1465 ref int activeWriter = ref metadata[key].ActiveWriterSystemID;
GlobalDataVault.cs:1466 if (Interlocked.CompareExchange(ref activeWriter, (int)systemID, 0) != 0)
GlobalDataVault.cs:1467     return false;
GlobalDataVault.cs:1469 block.Reserved0 |= BlockFlagLocked;
GlobalDataVault.cs:1470 block.Reserved1++;
GlobalDataVault.cs:1471 _blocks[blockIndex] = block;
```

Cleanup paths:

```text
GlobalDataVault.cs:1476 Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
GlobalDataVault.cs:1485 Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
GlobalDataVault.cs:1496 Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
GlobalDataVault.cs:1532 Interlocked.CompareExchange(ref activeWriter, 0, (int)systemID);
```

Mask-producing property:

```text
GlobalDataVault.cs:542 public uint ActiveBurstLockMask => unchecked((uint)Volatile.Read(ref _activeLocks));
```

Actual `_activeLocks` bit operations are in `TryLockBuffer` / `TryUnlockBuffer`, not `TryAcquireWriteLock`:

```text
GlobalDataVault.cs:1822 block.Reserved1++;
GlobalDataVault.cs:1823 block.Reserved0 |= BlockFlagLocked;
GlobalDataVault.cs:1841 SetActiveLockBit(ResolveActiveLockBit(bufferId));

GlobalDataVault.cs:1869 block.Reserved1--;
GlobalDataVault.cs:1871 block.Reserved0 &= unchecked((byte)~BlockFlagLocked);
GlobalDataVault.cs:1874 ClearActiveLockBitIfUnused(ResolveActiveLockBit(bufferId));

GlobalDataVault.cs:1917 int bitIndex = unchecked((int)((uint)(int)bufferId & 31u));
GlobalDataVault.cs:1918 return 1 << bitIndex;
GlobalDataVault.cs:1927 observed = Volatile.Read(ref _activeLocks);
GlobalDataVault.cs:1928 updated = observed | bit;
GlobalDataVault.cs:1932 while (Interlocked.CompareExchange(ref _activeLocks, updated, observed) != observed);
GlobalDataVault.cs:1944 observed = Volatile.Read(ref _activeLocks);
GlobalDataVault.cs:1945 updated = observed & ~bit;
GlobalDataVault.cs:1949 while (Interlocked.CompareExchange(ref _activeLocks, updated, observed) != observed);
```

Verdict: `TryAcquireWriteLock` is represented by `ActiveWriterSystemID`, `Reserved0`, and `Reserved1`; it is not represented in `ActiveBurstLockMask`.

## 3. Defrag Lock Checks Before Relocation

Correct primitive: live defrag uses `UnsafeUtility.MemMove`, not `MemCpy`.

Outer phase gate:

```text
GlobalDataVault.cs:2217 bool burstLocked = HasActiveBurstLocks(activeBurstLockMask);
GlobalDataVault.cs:2219 _memMoveBlockedByStress = stressHalted || burstLocked;
GlobalDataVault.cs:2240 if (!stressHalted && !burstLocked)
GlobalDataVault.cs:2241     TryRunLiveCompactionSlice(activeBurstLockMask);
```

Slice entry gates:

```text
GlobalDataVault.cs:3148 if (_memMoveBlockedByStress) return false;
GlobalDataVault.cs:3149 if (Volatile.Read(ref _allocationLock) != 0) return false;
GlobalDataVault.cs:3150 if (Volatile.Read(ref _compactionFence) != 0) return false;
GlobalDataVault.cs:3151 if (HasActiveBurstLocks(activeBurstLockMask)) return false;
GlobalDataVault.cs:3161 if (Interlocked.Exchange(ref _compactionFence, 1) != 0) return false;
```

Per-iteration active-lock recheck:

```text
GlobalDataVault.cs:3167 if (HasActiveBurstLocks(activeBurstLockMask))
GlobalDataVault.cs:3169     _defragFlags |= DefragFlagAliasBlocked;
```

Per-block lock/pin reject before move attempt:

```text
GlobalDataVault.cs:3181 if ((occupiedBlock.Reserved0 & BlockFlagLocked) != 0 || occupiedBlock.Reserved1 != 0)
GlobalDataVault.cs:3182     continue;
GlobalDataVault.cs:3187 if ((occupiedBlock.Reserved0 & BlockFlagExternalView) != 0)
GlobalDataVault.cs:3188     continue;
```

Final pre-move lock/pin reject inside relocation function:

```text
GlobalDataVault.cs:3254 if ((occupiedBlock.Reserved0 & (BlockFlagLocked | BlockFlagExternalView)) != 0 || occupiedBlock.Reserved1 != 0)
GlobalDataVault.cs:3255 {
GlobalDataVault.cs:3256     RecordMemoryFault(...);
GlobalDataVault.cs:3257     return false;
GlobalDataVault.cs:3258 }
```

Relocation call:

```text
GlobalDataVault.cs:3292 IntPtr newAddress = IntPtr.Add(_arenaBase, checked((int)freeBlock.OffsetBytes));
GlobalDataVault.cs:3293 UnsafeUtility.MemMove(newAddress.ToPointer(), oldAddress.ToPointer(), occupiedBlock.Bytes);
```

Risk verdict:

```text
Live compaction has active-mask, compaction-fence, block-flag, refcount, external-view, adjacency, bounds, alignment, metadata, and pointer-consistency gates before MemMove.
However, `TryAcquireWriteLock` does not check `_compactionFence` before setting `Reserved0`/`Reserved1`.
`TryLockBuffer` also does not check `_compactionFence`.
Therefore a narrow metadata race exists around `_blocks` mutation unless all callers are serialized by an owner phase outside these methods.
```

Separate higher-risk relocation path:

```text
GlobalDataVault.cs:3642 private bool TryGrowArenaForBytes(long requiredContiguousBytes)
GlobalDataVault.cs:3648 if (Volatile.Read(ref _compactionFence) != 0) return false;
GlobalDataVault.cs:3674 return TryGrowArena(desiredBytes);

GlobalDataVault.cs:3697 IntPtr newBase = new IntPtr(H8Memory.ReallocateRaw(oldBase.ToPointer(), oldArenaBytes, newArenaBytes, 64, Allocator.Persistent, H8Memory.MemoryOwner.GlobalDataVault));
GlobalDataVault.cs:3713 RefreshBlocksAfterArenaRelocation(oldBase.ToPointer(), newBase.ToPointer());
GlobalDataVault.cs:3717 BumpVaultGeneration();

H8Memory.cs:2741 UnsafeUtility.MemMove(newPointer, oldPointer, copyBytes);
H8Memory.cs:2753 UnsafeUtility.Free(oldPointer, allocator);
```

`TryGrowArenaForBytes` checks `_allocationLock` and `_compactionFence`; it does not check `HasActiveBurstLocks(...)`, `_activeLocks`, `Reserved0`, or `Reserved1` across blocks before whole-arena relocation. That is a stale-pointer risk path for Burst/job buffers if arena growth can run while a job owns a pointer.
