# LOG_CORE_DATA_VAULT_WARDEN

## 2026-05-13 - Global Data Vault & Memory Sentinel

What was wrong:
- Runtime native memory ownership was scattered across systems; persistent buffers could outlive their owner with no enforced owner table.
- `MemoryManager.Instance` infection was requested for purge; static scan found no existing hits in `Assets/_Project/Scripts`.
- Persistent `NativeArray<T>(..., Allocator.Persistent)` debt remains broad: 709 source sites. Blind rewrite was rejected because owner, disposal dependency, and job lifetime mapping are not uniform.
- Physics Rigidbody AUP state was still system-owned instead of vault-owned.
- Crash memory forensics had no global allocation table dump path.

What was done:
- Added `Assets/_Project/Scripts/Core/Memory/Hecton8.Core.Memory.asmdef`.
- Added `H8Memory`: owner-tagged native allocation API, raw allocation/free, copy/free reallocation, alias views, leak reaping, low-tier 512MB cap, allocation table text dump.
- Added `GlobalDataVault`: `UnsafeHashMap<int, IntPtr>` buffer registry, typed metadata validation, AUP allocation lock, FrostTick maintenance hook, raw memory lifetime authority.
- Registered `IDataVault` through `GlobalRegistry` and bootstrap, no Unity singleton.
- Added `GlobalRegistry` leak-reap interception on unregister/replace with `[FATAL LEAK PREVENTED]` development log.
- Routed `GlobalPhysicsStateManager` Rigidbody AUPs through `BufferID.RigidbodyAUPs`; fallback uses `H8Memory.Allocate`.
- Migrated dispatcher native buffers and physics culling arrays touched by this domain to `H8Memory.Allocate`/`Release`.
- Added bootstrap fatal-log hook dumping `Docs/AgentLogs/Dump_CORE_DATA_VAULT_WARDEN.txt` on exception/assert/fatal/crash/NaN error.
- Ran OMEGA polish: removed crash-dump `.ToString("X")`; focused memory scan has 0 hits for managed `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`.

Cinematic cheats used:
- Non-moving FrostTick defrag evaluation instead of unsafe pointer relocation.
- Copy/free raw reallocation instead of live `MemRealloc` on buffers with active `NativeArray` views.
- Low-tier 512MB cap buys predictable visual budget on i3/MX350 instead of unbounded native heap realism.

Exact microseconds saved:
- Singleton purge: 0 us hot path; no `MemoryManager.Instance` calls existed.
- Vault lookup after warm registry: estimated sub-1 us cold map/view retrieval; 0 us per Burst job iteration.
- Physics AUP vault ownership: estimated 10-40 us cold re-enable allocation spike avoided for the migrated AUP buffer.
- H8Memory tracking: estimated 1-5 us cost per cold allocation, 0 us in job hot path; paid to prevent leak/OOM.
- AUP allocation lock: one registry lookup and branch on shift frame only; 0 us normal frame.
- Fatal dump hook: 0 us normal frame; crash-only file IO.
- OMEGA `.ToString` removal: negligible crash-only time; hot path remains 0 B GC by static audit.

Verification:
- `dotnet build Hecton8.Core.Memory.CodexCheck.csproj -m:2 /nr:false` temporary focused check: passed.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: passed, 0 warnings, 0 errors.
- Status remains `PENDING VERIFICATION` by prompt mandate because Task 2 is blocked by 709 unmigrated persistent allocations outside safe single-turn scope.

## 2026-05-13 - No-Build Hardening Pass

What was wrong:
- `H8Memory.RegisterPointer` could silently fail when the fixed tracking table was full, leaving a native allocation outside the Sentinel.
- `GlobalDataVault.GetBuffer` could leak a freshly allocated raw pointer if `_buffers.TryAdd` or `_metadata.TryAdd` failed.
- Allocation-cap comparison used addition; edge cases near `long` limits should use subtraction.
- Dump output still asked `TextWriter` to format enum values; cold path only, but unnecessary.

What was done:
- Added cold native tracking-table growth from 4,096 up to 65,536 records before any allocation is made.
- Added null `UnsafeUtility.Malloc` guard.
- Replaced cap check with `bytes > _poolCapBytes - _totalBytes`.
- Added owner-byte index bounds guards.
- Replaced enum dump writes with integer owner/allocator values.
- Rejected `BufferID.Unknown` in vault retrieval.
- Added vault consistency check for pointer/metadata mismatch.
- Added rollback/free path when new vault buffer registration fails.

Cinematic cheats used:
- Cold table growth only on saturation; no per-frame allocator accounting work added.
- Fail-closed vault insertion instead of trying to repair state through managed diagnostics.
- Integer dump writes instead of formatted enum text.

Exact microseconds saved:
- Normal frame: 0 us changed; all edits are cold allocation/error paths.
- Saturation path: one-time native table copy cost replaces untracked leak risk; expected under 100 us for 4,096 records on desktop, only when allocator table is full.
- Vault insertion failure: prevents unbounded leak; cost only on failed cold allocation path.

Verification:
- No `dotnet build` launched per user instruction.
- Static forbidden-pattern scan over `Assets/_Project/Scripts/Core/Memory/*.cs`: 0 hits for managed `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`.
- `git diff --check` on touched memory/integration/docs files: no whitespace errors; only line-ending warnings reported by git.

## 2026-05-13 - Reallocation Accounting & Owner Mapping

What was wrong:
- Raw reallocation counted old and new vault buffers at the same time, so valid growth inside the 512MB cap could fail.
- Registry fallback owner mapping used `(byte)serviceSlot`, risking future owner collisions.
- `TryGetBuffer` accepted `BufferID.Unknown` through the normal miss path instead of rejecting it explicitly.

What was done:
- Reworked `H8Memory.ReallocateRaw` to reserve against the final footprint: total minus old bytes plus new bytes.
- Kept copy/free semantics and alignment from `UnsafeUtility.AlignOf<T>()`; no live pointer relocation introduced.
- Replaced byte-cast service owner fallback with direct integer owner value plus `SystemID.External` overflow guard.
- Added explicit `BufferID.Unknown` rejection in `GlobalDataVault.TryGetBuffer`.

Cinematic cheats used:
- Final-footprint accounting instead of temporary old+new cap inflation.
- Copy/free replacement instead of unsafe live `MemRealloc`.
- Integer owner fallback instead of expensive registry metadata lookup.

Exact microseconds saved:
- Normal frame: 0 us.
- Large vault resize: avoids false allocation failure; cold copy/free cost unchanged.
- Unregister leak reap: 0 us change unless fallback owner path is used; prevents future collision failures.

Verification:
- No `dotnet build` launched per user instruction.
- Static forbidden-pattern scan over `Core/Memory`: 0 hits.
- `git diff --check`: no whitespace errors; only LF/CRLF warnings.
