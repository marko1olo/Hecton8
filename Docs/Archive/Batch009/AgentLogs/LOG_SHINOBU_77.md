# LOG_SHINOBU_77

## 2026-05-18 - Final Report

Agent: SHINOBU_77
Domain: Echelon 8 Presentation & UX / Zero-GC Subtitles (Babel)
Task Count: 20
Status: STATIC COMPLETE / BUILD BLOCKED BY GUARD

### What Was Wrong

- Assignment targeted the 1295-byte Babel anomaly that can cause unaligned ARM64 reads. Direct binary probe found current `Data/Balance/Baked/Babel_Dictionary.h8bin` is already repaired to 1296 bytes, 16-byte aligned, header `FileByteLength=1296`, CRC `0x199CAC7A`, trailing padding byte zero.
- Runtime still lacked a public zero-GC lore decryption path that binds player progress, the Babel UTF-8 slice, and caller-owned output memory. Existing low-level XOR logic was not enough for a controlled lore-gate API.
- Full compile could not be launched without violating the build guard: CPU load was 90% at final check and live `dotnet`/`csc` processes existed.

### What Was Done

- Added `BabelDictionaryStore.TryBuildProgressDecryptionMask`: deterministic 16-byte mask. If all required lore bits are present, the mask is zero and UTF-8 is revealed cleanly. Missing bits produce deterministic corruption without managed strings.
- Added `BabelDictionaryStore.TryScheduleLoreDecryption`: pointer-backed Burst XOR from the mapped/padded Babel blob into caller-owned `NativeArray<byte>`.
- Added `BabelDictionaryStore.BabelLoreXorDecryptPointerJob`: `IJobParallelFor`, Burst synchronous compile, `FloatPrecision.Standard`, no managed allocation, source bounds guarded.
- Added `LocRegistry.TrySetLoreDecryptionMask` and `LocRegistry.TryScheduleLoreDecryption`: registry-level API for player-progress masks and scheduled lore byte decryption without `Dictionary<uint,string>`.
- Wrote `Docs/AgentLogs/SelfAudit_SHINOBU_77.xml` with task count, binary alignment proof, struct layout proof, decryption behavior, forbidden-pattern scan result, and compile-guard state.
- Generated `Docs/AgentLogs/BinaryHygiene_SHINOBU_77.json`. The target Babel payload is aligned. The global verifier still fails on unrelated Bakery editor binaries and archived dump files.

### Cinematic Cheats Used

- The Dear Lie: lore remains raw UTF-8 slices addressed by hash and offset. No runtime managed text table.
- Decryption fake: locked lore uses bytewise XOR corruption from a 16-byte deterministic mask instead of expensive semantic scrambling.
- Alignment fake: payload safety is enforced by file/header validation and padded fallback memory, not by trusting authoring source length.
- Continuous quality: lookup pressure remains bounded by existing `BabelLookupScalability.ResolveFrameLookupBudget`; no binary quality switch added.

### Exact Microseconds Saved

- ARM64 unaligned access trap: crash-class fault removed from the target Babel payload. Steady-frame cost: 0 us/frame. Cold load validation/padding only.
- Managed dictionary/string hydration avoided: estimated 2-8 us per small-table lookup burst on low-end CPU, plus allocation spikes. Measured profiler proof not run.
- Sorted native binary search for 26 records: target below 1 us per lookup on i3/MX350-class CPU. Measured profiler proof not run.
- Lore XOR decryption: estimated below 10 us for a 256-byte lore fragment on weak CPU when scheduled into caller-owned output. Measured profiler proof not run.
- MMF aligned path: avoids extra full-file RAM copy on supported platforms. Exact runtime gain is payload-size and platform dependent; no measured number claimed.

### Verification

- Direct binary probe: `Data/Balance/Baked/Babel_Dictionary.h8bin` bytes=1296, remainder16=0, magic=`H8AB`, entries=26, header length=1296, CRC=`0x199CAC7A`.
- Entry scan: 26 records, 0 bad slices, index offset 32, index remainder16=0.
- Touched-file forbidden scan: clean for `Dictionary<uint,string>`, `NativeParallelHashMap<uint,long>`, `Pack=1`, weak Burst precision, `string.Format`, `FindObjectOfType`, and `GameObject.Find`.
- `git diff --check` on touched code reported only LF-to-CRLF warnings, no semantic whitespace defects.
- Compile: not executed. Guard condition active at final check: CPU load 90%, live `dotnet` and `csc` processes present.

## 2026-05-18 - Ultra-Think Polish Pass

### What Was Wrong

- The previous report used over-strong status language. This domain remains pending Unity import, Burst, player, GCMonitor, and profiler verification.
- The pointer-backed lore decrypt job had the right shape but did not state the safety invariant required by the native memory mandate.
- The public pointer-backed decrypt API allowed a default/uncreated mask. That is not a valid job scheduling contract even if the Burst body checks `IsCreated`.
- `LocRegistry` has two explicit `.Complete()` sites. They are not hidden hot-path waits, but they were undocumented.

### What Was Done

- Tightened `BabelDictionaryStore.TryScheduleLoreDecryption` so callers must pass a created progress mask. Clean output is produced by a created 16-byte zero mask.
- Added three-paragraph `NativeDisableUnsafePtrRestriction` safety justifications for the Babel index pointer and pointer-backed lore source.
- Marked `MarkVisibleTextOffsetPrefetchComplete` as non-blocking fence clear and `CompleteUtf8ReadersForMutation` as a structural mutation gate.
- Reopened status as `PENDING VERIFICATION / POLISH PASS ACTIVE / BUILD BLOCKED BY GUARD`.

### Cinematic Cheats Used

- Lore corruption remains bytewise XOR over raw UTF-8. No semantic text system, no runtime managed string framework, no second source copy before decrypt.
- Babel still presents text as offsets and lengths into unmanaged memory. The UI layer gets illusion-ready bytes and owns glyph rendering.

### Exact Microseconds Saved

- Pointer decrypt still avoids a source copy: estimated saved work is one O(n) memory pass per lore fragment versus copy-then-XOR. For a 256-byte fragment, this is expected below 10 us on low-end CPU; profiler proof absent.
- Requiring a created mask adds 0 us/frame and prevents a schedule-time safety failure class.
- Annotated sync points do not reduce runtime cost; they make the two legal blocking boundaries auditable.

### Verification

- Build still not executed. Guard active during polish pass: CPU reached 100% and live `dotnet`/`csc` processes existed.

### Second Verification Pass

- Direct binary probe: bytes=1296, remainder16=0, magic=`0x42413848`, entries=26, indexOffset=32, dataOffset=448, headerFileByteLength=1296, CRC=`0x199CAC7A`, tailByte=0.
- Entry scan: badSlices=0, index remainder16=0, data remainder16=0.
- Source scan: no `Dictionary<uint,string>`, `NativeParallelHashMap<uint,long>`, `Pack=1`, `FloatPrecision.Low`, `string.Format`, `FindObjectOfType`, or `GameObject.Find` in Babel touched source/contracts.
- Burst directive count in touched files: 9 exact `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]` entries.
- `VerifyBinaryHygiene.py` result remains global failed: `binaryCount=69`, `misalignedCount=17`. Target Babel payloads are aligned; failures are Bakery editor fixtures and archived dump artifacts.
- Latest compile guard check: CPU 53%. No build launched.

## 2026-05-18 - Pointer Lifetime Fence Pass

### What Was Wrong

- Pointer-backed lore decrypt jobs read from MMF/Vault bytes. `CloseFile()` released that pointer without owning a read fence. That is a use-after-free class if reload/shutdown races a scheduled decrypt.

### What Was Done

- Added `_activeLoreReadHandle` tracking in `BabelDictionaryStore`.
- Combined every scheduled pointer decrypt handle into the store-owned fence.
- `CloseFile()` now completes that fence before releasing MMF/Vault bytes.
- Added a non-blocking clear when completed fences are observed before later schedules.

### Cinematic Cheats Used

- No new simulation. The lore system remains raw byte lookup plus XOR corruption.

### Exact Microseconds Saved

- No frame-time saving claimed. Added overhead is one `JobHandle.CombineDependencies` per scheduled pointer decrypt. The gain is correctness: reload/shutdown cannot free bytes under an active job.

### Verification

- Static forbidden scan remains clean for `Dictionary<uint,string>`, `NativeParallelHashMap<uint,long>`, `Pack=1`, `FloatPrecision.Low`, `string.Format`, `FindObjectOfType`, `GameObject.Find`, `new byte[`, and `new string` in Babel touched runtime/contracts.
- Pointer fence readback confirmed `_activeLoreReadHandle` field, `RegisterLoreReadHandle(handle)` after schedule, and `CompleteActiveLoreReadsForClose()` before pointer release.
- Target binary re-probe: 1296 bytes, rem16=0, entries=26, badSlices=0, header length=1296, CRC=`0x199CAC7A`, tail=0.
- `git diff --check`: only LF-to-CRLF warnings in the two touched C# files.
- Build not launched. CPU guard reported 100%.

## 2026-05-18 - Post-Compaction Sanity Pass

### What Was Wrong

- Context compaction can erase local reasoning state. The disk memory had to be treated as the only source of truth before responding.

### What Was Done

- Re-read `Status_SHINOBU_77.md` and `Rationale_SHINOBU_77.md`.
- Re-ran `git status --short` on the SHINOBU_77 touched surface.
- Re-ran `git diff --check` on the two touched C# files.
- Re-ran forbidden pattern scan over `BabelDictionaryStore.cs` and `LocRegistry.cs`.
- Re-checked CPU guard.

### Cinematic Cheats Used

- No new runtime cheat added in this pass. Existing cheat remains byte-slice lookup plus deterministic XOR lore corruption, not managed text simulation.

### Exact Microseconds Saved

- No new runtime saving claimed. This pass preserved build-host stability by refusing compile under 100% CPU.

### Verification

- `git status`: modified `BabelDictionaryStore.cs`, `LocRegistry.cs`; SHINOBU_77 status/rationale/log/self-audit/binary-hygiene files are untracked or modified as expected.
- `git diff --check`: only LF-to-CRLF warnings.
- Forbidden scan: no `Dictionary<uint,string>`, `NativeParallelHashMap<uint,long>`, `Pack=1`, `FloatPrecision.Low`, `string.Format`, `FindObjectOfType`, `GameObject.Find`, `new byte[`, or `new string` in the two touched runtime files.
- Build not launched. CPU guard reported 100%.
